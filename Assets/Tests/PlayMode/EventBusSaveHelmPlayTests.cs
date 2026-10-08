#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using HiddenHarbours.App;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using HiddenHarbours.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>Uses the assembly's TestRunSave prebuild redirect, installed BEFORE SaveService.Awake.
    /// No path to the player's save is resolved or read. The replacement fault is platform independent.</summary>
    public class EventBusSaveHelmPlayTests
    {
        private readonly List<Object> _objects = new();
        private GameObject Go(string name, Vector3 position)
        {
            var go = new GameObject(name);
            _objects.Add(go);
            go.transform.position = position;
            return go;
        }

        [Test]
        public void TakeHelm_WhenSaveStoreReplaceFails_StillNotifiesMode()
        {
            // The bootstrap has already awakened under TestRunSave's redirect. Fail closed before IO
            // if that runner hook was absent, or a previous fixture left the live service elsewhere.
            string runPath = System.Environment.GetEnvironmentVariable(SaveStore.TestRunSaveVariable);
            Assert.That(runPath, Is.Not.Null.And.Not.Empty);
            string runDirectory = Path.GetDirectoryName(Path.GetFullPath(runPath));
            Assert.That(Path.GetDirectoryName(runDirectory), Is.EqualTo(TestRunSave.RunsRoot));
            // Earlier fixtures can clear the locator without destroying the bootstrap object.
            // Find that already-redirected object; never create another service here.
            var service = Object.FindAnyObjectByType<SaveService>();
            Assert.That(service, Is.Not.Null, "the real BeforeSceneLoad bootstrap must have run");
            Assert.That(service.SavePath, Is.EqualTo(runPath));
            Assert.That(SaveStore.ActivePath, Is.EqualTo(runPath));
            var previousSaveService = GameServices.Save;

            // Give this case a separate file under the already-verified run directory. This uses the
            // real service's internal Open seam; its original Awake was also redirected, never live.
            string testPath = Path.Combine(runDirectory, "eventbus-" + Guid.NewGuid().ToString("N"), "savegame.json");
            var previousData = service.Current;
            bool previousLoaded = service.LoadedExistingSave;
            var previousFault = SaveStore.BeforeReplaceForTests;
            var currentProperty = typeof(SaveService).GetProperty(nameof(SaveService.Current));
            var loadedProperty = typeof(SaveService).GetProperty(nameof(SaveService.LoadedExistingSave));
            // Restore even the unreadable-file preservation obligation after this temporary Open.
            var unreadableField = typeof(SaveService).GetField("_unreadableOnDisk", BindingFlags.Instance | BindingFlags.NonPublic);
            bool previousUnreadable = (bool)unreadableField.GetValue(service);
            Action<ActiveBoatChanged> saveHandler = (Action<ActiveBoatChanged>)Delegate.CreateDelegate(
                typeof(Action<ActiveBoatChanged>), service, "OnActiveBoatChanged");
            bool addedSaveHandler = false;
            Action<ActiveBoatChanged> observeBoat = null;
            Action<ControlModeChanged> observeMode = null;
            Action<GameSaved> observeSaved = null;
            try
            {
                GameServices.Save = service;
                ShellFlow.Reset();
                Interactables.Clear(); InteractVerb.Reset(); InteractionGate.Reset();
                InteractActionClaim.Reset(); InteractOffer.Reset(); InteractActorProbe.Reset();
                // Any setup-time component saves also stay inside this case's own directory.
                service.Open(testPath + ".setup");

                // Other fixtures may have cleared this channel. Isolate this subscription without
                // clearing unrelated listeners; remove the one installed by Awake, then put it first
                // relative to all observers that THIS test creates. Restore one subscription below.
                EventBus.Unsubscribe(saveHandler);
                EventBus.Subscribe(saveHandler);
                addedSaveHandler = true;

                var player = Go("EventBus player", new Vector3(0, -11.5f, 0));
                var walk = player.AddComponent<PlayerWalkController>();
                var deck = player.AddComponent<DeckWalkController>(); deck.enabled = false;
                var boatGo = Go("EventBus boat", new Vector3(0, -13.8f, 0));
                var boat = boatGo.AddComponent<BoatController>();
                var input = boatGo.AddComponent<DevBoatInput>();
                var hull = ScriptableObject.CreateInstance<BoatHullDef>(); _objects.Add(hull);
                hull.Id = "boat.eventbus_test"; hull.CameraWorldHeightMeters = 14f;
                boat.SetHull(hull); boat.enabled = false; input.enabled = false;
                var dock = Go("EventBus dock", new Vector3(0, -12, 0));
                var exit = Go("EventBus exit", new Vector3(0, -10.5f, 0));
                var sw = Go("EventBus switcher", Vector3.zero).AddComponent<ControlSwitcher>();
                sw.Configure(walk, boat, input, dock.transform, 3f, exit.transform);
                var cameraGo = Go("EventBus camera", Vector3.zero);
                cameraGo.AddComponent<Camera>().orthographic = true;
                var camera = cameraGo.AddComponent<CameraFollow>();
                typeof(CameraFollow).GetField("_onFootTarget", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(camera, player.transform);
                typeof(CameraFollow).GetField("_boatTarget", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(camera, boatGo.transform);
                Assert.That(sw.TryInteract(), Is.True);
                Assert.That(sw.Mode, Is.EqualTo(ControlMode.OnDeck));
                Assert.That(camera.Target, Is.SameAs(player.transform));
                // Establish the failure premise AFTER setup. Open an absent TEST path so a failed
                // attempt cannot set LoadedExistingSave, then independently seed a valid old file.
                // The attempted save must Replace it; no setup write can make this a false green.
                service.Open(testPath);
                var old = SaveMigration.NewGame();
                old.ActiveHullId = "boat.previous_test_save";
                SaveStore.Write(old, testPath);
                byte[] oldBytes = File.ReadAllBytes(testPath);
                Assert.That(service.LoadedExistingSave, Is.False);
                int attempts = 0, boatNotices = 0, savedSignals = 0;
                bool tempWasComplete = false;
                var modes = new List<ControlMode>();
                observeBoat = _ => boatNotices++;
                observeMode = e => modes.Add(e.Mode);
                observeSaved = _ => savedSignals++;
                EventBus.Subscribe(observeBoat); EventBus.Subscribe(observeMode); EventBus.Subscribe(observeSaved);
                SaveStore.BeforeReplaceForTests = path =>
                {
                    if (path != testPath) { previousFault?.Invoke(path); return; }
                    attempts++;
                    tempWasComplete = SaveStore.Read(path + ".tmp")?.ActiveHullId == "boat.eventbus_test";
                    throw new IOException("eventbus-replace-denied");
                };
                player.transform.position = sw.HelmWorldPosition;
                LogAssert.Expect(LogType.Exception, new Regex("^IOException: eventbus-replace-denied(?:\\r?\\n|$)"));

                bool returned = sw.TryInteract();

                Assert.That(attempts, Is.EqualTo(1), "the real SaveService reached the replacement boundary");
                Assert.That(tempWasComplete, Is.True, "serialization and temp write preceded the injected failure");
                CollectionAssert.AreEqual(oldBytes, File.ReadAllBytes(testPath));
                Assert.That(service.LoadedExistingSave, Is.False, "the failed write never reached its success assignment");
                Assert.That(savedSignals, Is.Zero, "isolation does not manufacture a successful save response");
                Assert.That(returned, Is.True);
                Assert.That(sw.Mode, Is.EqualTo(ControlMode.Aboard));
                Assert.That(boat.enabled && input.enabled, Is.True);
                Assert.That(walk.enabled || deck.enabled, Is.False);
                Assert.That(boatNotices, Is.EqualTo(1));
                CollectionAssert.AreEqual(new[] { ControlMode.Aboard }, modes);
                Assert.That(camera.Target, Is.SameAs(boatGo.transform));
            }
            finally
            {
                // Restore the locator even if later cleanup fails; it may originally have been null.
                GameServices.Save = previousSaveService;
                SaveStore.BeforeReplaceForTests = previousFault;
                EventBus.Unsubscribe(observeBoat); EventBus.Unsubscribe(observeMode); EventBus.Unsubscribe(observeSaved);
                if (addedSaveHandler) { EventBus.Unsubscribe(saveHandler); EventBus.Subscribe(saveHandler); }
                for (int i = _objects.Count - 1; i >= 0; i--)
                    if (_objects[i] != null) Object.DestroyImmediate(_objects[i]);
                _objects.Clear();
                service.Open(runPath);
                currentProperty.SetValue(service, previousData);
                loadedProperty.SetValue(service, previousLoaded);
                unreadableField.SetValue(service, previousUnreadable);
                ShellFlow.Reset();
                Interactables.Clear(); InteractVerb.Reset(); InteractionGate.Reset();
                InteractActionClaim.Reset(); InteractOffer.Reset(); InteractActorProbe.Reset();
            }
        }
    }
}
#endif
