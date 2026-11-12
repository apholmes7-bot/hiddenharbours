#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using HiddenHarbours.App;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using HiddenHarbours.CoopSpike;
using HiddenHarbours.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace HiddenHarbours.Tests.CoopSpike
{
    /// <summary>The authored boot/core path, separately from the two network-peer fixtures.</summary>
    public sealed class CoopSpikeDevBootTests
    {
        private CoopDevStart _boot;
        private ISaveService _previousSave;

        private sealed class MemorySave : ISaveService
        {
            private readonly Dictionary<string, bool> _flags = new();
            public SaveData Current { get; } = SaveMigration.NewGame();
            public bool GetFlag(string key) => _flags.TryGetValue(key, out bool value) && value;
            public void SetFlag(string key, bool value) => _flags[key] = value;
            public void Save() { }
        }

        [UnityTest]
        public IEnumerator AuthoredStartingCoreBootsTheHostAtSeaWithARod()
        {
            Assert.That(Application.productName, Is.EqualTo(CoopDevStart.SpikeProduct));
            _previousSave = GameServices.Save;
            GameServices.Save = new MemorySave();
            yield return SceneManager.LoadSceneAsync("Assets/_Project/Scenes/StPeters.unity", LoadSceneMode.Single);
            yield return null;
            Assert.That(GameServices.Ready, Is.True, "Use the authored core rather than reconstructing one in the test.");
            var clock = GameServices.Clock;
            var bootObject = new GameObject("CoopDevBootFixture");
            Object.DontDestroyOnLoad(bootObject);
            _boot = bootObject.AddComponent<CoopDevStart>();
            float deadline = Time.realtimeSinceStartup + 60;
            while (!_boot.Ready && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(_boot.Ready, Is.True, "The real dev-start coroutine must finish.");
            Assert.That(Object.FindAnyObjectByType<SaveService>(), Is.Null, "No disk writer may survive the trial boot.");
            Assert.That(GameServices.Save.LoadedExistingSave, Is.False);
            Assert.That(Application.runInBackground, Is.True);
            Assert.That(_boot.Manager.GetComponent<Unity.Netcode.Transports.UTP.UnityTransport>()
                .ConnectionData.ServerListenAddress, Is.EqualTo("0.0.0.0"));
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("EastWater"));
            Assert.That(GameServices.CurrentRegionId, Is.EqualTo("region.east_water"));
            Assert.That(GameServices.Clock, Is.SameAs(clock), "The starting core must survive the water-region hop.");
            Assert.That(_boot.Manager.IsHost && _boot.Manager.IsListening, Is.True);
            while (_boot.Manager.LocalClient.PlayerObject == null && Time.realtimeSinceStartup < deadline) yield return null;
            var playerObject = _boot.Manager.LocalClient.PlayerObject;
            Assert.That(playerObject, Is.Not.Null);
            Assert.That(playerObject.GetComponent<CoopBoatReplica>().IsOwner, Is.True);
            Assert.That(_boot.Manager.SpawnManager.SpawnedObjects.Count, Is.EqualTo(1));
            var switcher = Object.FindAnyObjectByType<ControlSwitcher>();
            Assert.That(switcher.Mode, Is.EqualTo(ControlMode.Aboard));
            var settings = Resources.Load<CoopSpikeSettings>(CoopSpikeSettings.ResourcePath);
            Assert.That(Object.FindAnyObjectByType<BoatController>().GetComponent<HullTideRide>()
                .BakedWaterlineElevation, Is.EqualTo(settings.PictureDatumElevation),
                "The open-water spawn must not inherit the landing's elevated picture datum.");
            Assert.That(CarriedItem.InHand(GameServices.Hands, settings.Rod.Id), Is.True);
            Assert.That(Vector2.Distance(Object.FindAnyObjectByType<BoatController>().transform.position,
                settings.HostStart), Is.LessThan(0.1f));
            TestContext.WriteLine("Authored StPeters core -> real dev start -> EastWater; host listening, owned proxy, helm and rod.");
            // Opt-in evidence of the actual authored transforms; no camera or pose is changed.
            string trace = System.Environment.GetEnvironmentVariable("HH_COOP_TEST_TRACE");
            if (!string.IsNullOrEmpty(trace))
            {
                yield return new WaitForSeconds(2);
                var report = new StringBuilder();
                var boat = Object.FindAnyObjectByType<BoatController>();
                var player = Object.FindAnyObjectByType<PlayerWalkController>();
                var authoredControl = new UnityEditor.SerializedObject(switcher);
                Assert.That(authoredControl.FindProperty("_boatController").objectReferenceValue, Is.SameAs(boat));
                Assert.That(authoredControl.FindProperty("_playerWalk").objectReferenceValue, Is.SameAs(player));
                foreach (var root in new[] { boat.transform, player.transform, Camera.main.transform })
                foreach (var node in root.GetComponentsInChildren<Transform>(true))
                {
                    report.AppendLine($"{node.name} parent={node.parent?.name} world={node.position:F5} local={node.localPosition:F5} scale={node.lossyScale:F5} rotation={node.eulerAngles:F5}");
                    var renderer = node.GetComponent<Renderer>();
                    if (renderer != null) report.AppendLine($"  {renderer.GetType().Name} enabled={renderer.enabled} off={renderer.forceRenderingOff} bounds={renderer.bounds} shader={renderer.sharedMaterial?.shader?.name}");
                }
                var rider = player.GetComponent<DeckRiderVisual>();
                var presenter = player.GetComponent<DeckRiderMeshPresenter>();
                report.AppendLine($"rider pose={rider.Pose} ride={rider.HullDrawnRideMeters} stand={rider.DeckStandRigLocal:F5} body={rider.BodyShownOnRoot} suppressed={rider.SpriteSuppressed}");
                report.AppendLine($"mesh present={presenter != null} drawing={presenter?.DrawsInsteadOfSprite} reason={presenter?.NotDrawingReason}");
                Directory.CreateDirectory(trace);
                File.WriteAllText(Path.Combine(trace, "boot-geometry.txt"), report.ToString());
            }
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_boot != null && _boot.Manager != null)
            {
                var manager = _boot.Manager;
                manager.Shutdown();
                float deadline = Time.realtimeSinceStartup + 5;
                while (manager != null && manager.IsListening && Time.realtimeSinceStartup < deadline) yield return null;
            }
            if (_boot != null) Object.Destroy(_boot.gameObject);
            ShellRestart.DestroyPersistentCore();
            yield return null;
            var clean = SceneManager.CreateScene("CoopDevBootCleanup");
            SceneManager.SetActiveScene(clean);
            var water = SceneManager.GetSceneByName("EastWater");
            if (water.IsValid() && water.isLoaded) yield return SceneManager.UnloadSceneAsync(water);
            var start = SceneManager.GetSceneByName("StPeters");
            if (start.IsValid() && start.isLoaded) yield return SceneManager.UnloadSceneAsync(start);
            GameServices.Reset();
            GameServices.Save = _previousSave is Object prior && prior == null ? new MemorySave() : _previousSave;
        }
    }
}
#endif
