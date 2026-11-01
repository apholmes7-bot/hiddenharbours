#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HiddenHarbours.App;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using HiddenHarbours.Environment;
using HiddenHarbours.UI;
using HiddenHarbours.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace HiddenHarbours.Tests.PlayMode
{
    public class ArrivalAtSeaPlayTests
    {
        readonly List<PlateRead> _firstFrames = new List<PlateRead>();
        bool _guardFirstFrames;

        [SetUp] public void PublishAFreshSave() => GameServices.Save = new FakeSave();

        private sealed class FakeSave : ISaveService
        {
            private readonly Dictionary<string, bool> _flags = new Dictionary<string, bool>();
            public SaveData Current { get; } = new SaveData();
            public bool GetFlag(string key) => _flags.TryGetValue(key, out bool v) && v;
            public void SetFlag(string key, bool value) => _flags[key] = value;
            public void Save() { }
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator TheFirstFrameAtEachHop_FramesTheCape()
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                Assert.Ignore("First-frame camera proof requires a graphics device.");
            _firstFrames.Clear();
            _guardFirstFrames = true;
            try
            {
                yield return NewGameOpensAtSeaCrossesWithoutASeatSnapAndStepsAshore();
                Assert.AreEqual(2, _firstFrames.Count);
                foreach (var read in _firstFrames)
                {
                    Assert.That(read.BoatViewport.x, Is.InRange(0.25f, 0.75f), read.Name + " viewport X");
                    Assert.That(read.BoatViewport.y, Is.InRange(0.25f, 0.75f), read.Name + " viewport Y");
                    Assert.AreEqual(1, read.FollowCount, read.Name + " camera follow count");
                }
            }
            finally { _guardFirstFrames = false; }
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            ShellPause.Resume();
            ShellRestart.DestroyPersistentCore();
            // A GPU skip can reach teardown before replacing the fixture's existing empty scene.
            var empty = SceneManager.GetSceneByName("AfterSeaOpening");
            if (!empty.IsValid() || !empty.isLoaded) empty = SceneManager.CreateScene("AfterSeaOpening");
            SceneManager.SetActiveScene(empty);
            foreach (string name in new[] { EastWaterPlan.SceneName, "StPeters" })
            {
                var scene = SceneManager.GetSceneByName(name);
                if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            }
            GameServices.Reset();
            ShellFlow.Reset();
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator NewGameOpensAtSeaCrossesWithoutASeatSnapAndStepsAshore()
        {
            ShellRestart.DestroyPersistentCore();
            yield return null;
            yield return SceneManager.LoadSceneAsync("StPeters", LoadSceneMode.Single);
            yield return null;
            yield return null;
            Assert.IsTrue(ShellFlow.AtTitle);
            var clock = GameServices.Clock as GameClock;
            Assert.IsNotNull(clock);
            var def = AssetDatabase.LoadAssetAtPath<ArrivalSeaDef>(ArrivalSeaAuthoring.DefPath);
            Assert.IsNotNull(def);
            ShellFlow.StartNewGame();
            double deadline = Time.realtimeSinceStartupAsDouble + 240d;
            while (ArrivalOpening.SeaJourney == null)
            {
                Assert.Less(Time.realtimeSinceStartupAsDouble, deadline, "the boot hop did not seat the player");
                yield return null;
            }
            var sea = ArrivalOpening.SeaJourney;
            Assert.IsTrue(sea.IsAtSea);
            Assert.AreEqual(def.SeaRegion.SceneName, SceneManager.GetActiveScene().name);
            Assert.AreEqual(clock.StartSeconds, clock.TotalSeconds);
            Assert.IsTrue(clock.IsHeld);
            Assert.IsFalse(sea.IsBelowDecks);
            Assert.Less(Vector2.Distance(sea.Boat.transform.position, def.LiePosition), 0.1f);
            Assert.Less(sea.Boat.Velocity.magnitude, 0.1f, "the cape starts lying, then gathers way");
            yield return null;
            AssertMesh(sea);
            var hud = Object.FindFirstObjectByType<HudController>();
            Assert.IsNotNull(hud);
            var watch = typeof(HudController).GetField("_watchImage", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(hud) as RawImage;
            Assert.IsNotNull(watch);
            Assert.IsFalse(watch.enabled, "the held hour hides the actual watch face");
            yield return Plate("opening-at-sea.png");

            double held = clock.TotalSeconds;
            double flow = clock.FlowSeconds;
            ShellPause.Pause();
            yield return null;
            Assert.AreEqual(flow, clock.FlowSeconds);
            ShellPause.Resume();
            Assert.IsTrue(clock.IsHeld);

            bool crossed = false;
            float sourceHeading = 0f, targetHeading = 0f, sourceSpeed = 0f, targetSpeed = 0f;
            float sourceFrameDt = 0f;
            Vector2 sourceSeat = default, targetSeat = default;
            void OnCrossing(Scene previous, Scene next)
            {
                if (next.name != def.HomeRegion.SceneName) return;
                var home = ArrivalOpening.SeaJourney;
                Assert.IsNotNull(home);
                sourceHeading = ArrivalPilot.HeadingOf(sea.Boat.transform);
                targetHeading = ArrivalPilot.HeadingOf(home.Boat.transform);
                targetSpeed = home.Boat.Velocity.magnitude;
                sourceSeat = sea.DeckLocalPosition;
                targetSeat = home.DeckLocalPosition;
                crossed = true;
            }
            SceneManager.activeSceneChanged += OnCrossing;
            try
            {
                while (!crossed)
                {
                    Assert.Less(Time.realtimeSinceStartupAsDouble, deadline, "the cape did not sail through the west door");
                    Assert.AreEqual(held, clock.TotalSeconds);
                    AssertMesh(sea);
                    sourceSpeed = sea.Boat.Velocity.magnitude;
                    sourceFrameDt = Time.unscaledDeltaTime;
                    yield return null;
                }
            }
            finally { SceneManager.activeSceneChanged -= OnCrossing; }
            Debug.Log($"[ArrivalAtSeaPlayTests] seam source {sourceSpeed:R} m/s, replacement {targetSpeed:R} m/s, "
                + $"last source frame dt {sourceFrameDt:R} s, crossing frame dt {Time.unscaledDeltaTime:R} s");
            Assert.Less(Mathf.Abs(ArrivalPilot.Wrap180(sourceHeading - targetHeading)), 0.1f);
            Assert.AreEqual(sourceSpeed, targetSpeed, 0.05f,
                $"the replacement copy carries her way through; last source frame dt {sourceFrameDt:R} s, "
                + $"crossing frame dt {Time.unscaledDeltaTime:R} s");
            Assert.Less(Vector2.Distance(sourceSeat, targetSeat), 1e-4f, "the passenger keeps her metres of deck");
            Assert.Greater(clock.FlowSeconds, flow, "the world flowed while the hour stood");
            var arrival = ArrivalOpening.SeaJourney;
            Assert.IsFalse(arrival.IsAtSea);
            yield return null;
            AssertMesh(arrival);
            yield return Plate("opening-first-st-peters-frame.png");
            while (!arrival.CanStepAshore)
            {
                Assert.Less(Time.realtimeSinceStartupAsDouble, deadline, "the existing run-in did not reach the wharf");
                Assert.AreEqual(held, clock.TotalSeconds);
                AssertMesh(arrival);
                yield return null;
            }
            Assert.IsTrue(arrival.StepAshore(), "the same verb E calls starts the existing step");
            while (arrival.Current != ArrivalOpening.Phase.HandedOver) yield return null;
            Assert.IsFalse(clock.IsHeld);
            Assert.AreEqual(held, clock.TotalSeconds, 0.1d);
            yield return null;
            yield return null;
            Assert.Greater(clock.TotalSeconds, held);
            Assert.IsTrue(GameServices.Save.GetFlag(ArrivalOpening.ArrivedFlagKey));

            // The boot used this loader once already; coming home must leave its east door usable.
            RegionPassage outward = null;
            foreach (var passage in Object.FindObjectsByType<RegionPassage>(FindObjectsSortMode.None))
                if (passage.Target == def.SeaRegion) outward = passage;
            Assert.IsNotNull(outward);
            outward.Activate();
            while (SceneManager.GetActiveScene().name != def.SeaRegion.SceneName)
            {
                Assert.Less(Time.realtimeSinceStartupAsDouble, deadline, "the boot left the later sea door consumed");
                yield return null;
            }
            Assert.IsFalse(clock.IsHeld, "an ordinary later visit has no opening hold");
            yield return null;
            yield return null;
            Assert.IsTrue(watch.enabled, "the ordinary sea visit shows the watch again");
        }

        IEnumerator Plate(string name)
        {
            if (!_guardFirstFrames) yield break;
            var args = System.Environment.GetCommandLineArgs();
            int index = System.Array.IndexOf(args, "-introSeaPlates");
            bool savePlate = index >= 0 && index + 1 < args.Length;
            string path = savePlate ? Path.Combine(args[index + 1], name) : null;
            // Let the game's late presentation fit its own lens before reading the normal render.
            var camera = Camera.main;
            Assert.IsNotNull(camera);
            var oldTarget = camera.targetTexture;
            int width = camera.pixelWidth, height = camera.pixelHeight;
            var target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
            bool captured = false;
            System.Exception failure = null;
            void CameraEnded(UnityEngine.Rendering.ScriptableRenderContext context, Camera rendered)
            {
                if (rendered != camera || captured) return;
                captured = true;
                var previous = RenderTexture.active;
                var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
                try
                {
                    RenderTexture.active = target;
                    texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                    texture.Apply();
                    if (savePlate) File.WriteAllBytes(path, texture.EncodeToPNG());
                    var follow = camera.GetComponent<CameraFollow>();
                    var opening = ArrivalOpening.SeaJourney;
                    var read = new PlateRead
                    {
                        Name = name,
                        Frame = Time.frameCount,
                        FollowCount = Object.FindObjectsByType<CameraFollow>(FindObjectsSortMode.None).Length,
                        Realtime = Time.realtimeSinceStartupAsDouble,
                        CameraPosition = camera.transform.position,
                        TargetPosition = follow != null && follow.Target != null ? follow.Target.position : Vector3.zero,
                        BoatPosition = opening.Boat.transform.position,
                        BoatViewport = camera.WorldToViewportPoint(opening.Boat.transform.position),
                        OrthographicSize = camera.orthographicSize,
                        Aspect = camera.aspect,
                        Hour = GameServices.Clock.HourOfDay,
                        Held = GameServices.Clock.IsHeld,
                    };
                    _firstFrames.Add(read);
                    if (savePlate) File.WriteAllText(path + ".json", JsonUtility.ToJson(read, true));
                }
                catch (System.Exception ex) { failure = ex; }
                finally
                {
                    RenderTexture.active = previous;
                    Object.Destroy(texture);
                }
            }
            try
            {
                camera.targetTexture = target;
                UnityEngine.Rendering.RenderPipelineManager.endCameraRendering += CameraEnded;
                double deadline = Time.realtimeSinceStartupAsDouble + 10d;
                while (!captured)
                {
                    Assert.Less(Time.realtimeSinceStartupAsDouble, deadline, "the game camera did not render its plate frame");
                    yield return null;
                }
                Assert.IsNull(failure, failure?.ToString());
            }
            finally
            {
                UnityEngine.Rendering.RenderPipelineManager.endCameraRendering -= CameraEnded;
                if (camera != null) camera.targetTexture = oldTarget;
                RenderTexture.ReleaseTemporary(target);
            }
            if (savePlate) Assert.IsTrue(File.Exists(path), "the game camera did not write its plate");
        }

        [System.Serializable]
        sealed class PlateRead
        {
            public string Name;
            public int Frame;
            public int FollowCount;
            public double Realtime;
            public Vector3 CameraPosition, TargetPosition, BoatPosition, BoatViewport;
            public float OrthographicSize, Aspect, Hour;
            public bool Held;
        }

        static void AssertMesh(ArrivalOpening opening)
        {
            Assert.IsNotNull(opening.Boat.GetComponentInChildren<IsoFacetHullRenderer>(), "the cape must draw its hull mesh");
            var crew = opening.Boat.GetComponentsInChildren<CharacterFigurePresenter>();
            Assert.IsNotEmpty(crew, "the captain must have a mesh figure presenter");
            foreach (var figure in crew)
                Assert.IsTrue(figure.DrawsInsteadOfSprite, "the captain must remain a mesh figure: " + figure.WhyNot);
        }
    }
}
#endif
