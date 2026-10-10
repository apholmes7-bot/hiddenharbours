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

namespace HiddenHarbours.Tests.EditMode
{
    public class EventBusHelmTests
    {
        private readonly List<Object> _objects = new();

        [SetUp]
        public void SetUp() => Reset();

        [TearDown]
        public void TearDown()
        {
            for (int i = _objects.Count - 1; i >= 0; i--)
                if (_objects[i] != null) Object.DestroyImmediate(_objects[i]);
            _objects.Clear();
            Reset();
        }

        private static void Reset()
        {
            EventBus.Clear<ActiveBoatChanged>(); EventBus.Clear<ControlModeChanged>();
            GameServices.Reset(); ShellFlow.Reset();
            Interactables.Clear(); InteractVerb.Reset(); InteractionGate.Reset();
            InteractActionClaim.Reset(); InteractOffer.Reset(); InteractActorProbe.Reset();
        }

        private GameObject Go(string name, Vector3 position)
        {
            var go = new GameObject(name);
            _objects.Add(go);
            go.transform.position = position;
            return go;
        }

        [Test]
        public void TakeHelm_WhenActiveBoatSaveHandlerThrows_StillPublishesMode()
        {
            var player = Go("Player", new Vector3(0, -11.5f, 0));
            var walk = player.AddComponent<PlayerWalkController>();
            var deck = player.AddComponent<DeckWalkController>();
            deck.enabled = false;
            var boatGo = Go("Boat", new Vector3(0, -13.8f, 0));
            var boat = boatGo.AddComponent<BoatController>();
            var input = boatGo.AddComponent<DevBoatInput>();
            var hull = ScriptableObject.CreateInstance<BoatHullDef>();
            _objects.Add(hull);
            hull.Id = "boat.eventbus_test"; hull.CameraWorldHeightMeters = 14f;
            boat.SetHull(hull);
            boat.enabled = false; input.enabled = false;
            var dock = Go("Dock", new Vector3(0, -12, 0));
            var exit = Go("Exit", new Vector3(0, -10.5f, 0));
            var sw = Go("Switcher", Vector3.zero).AddComponent<ControlSwitcher>();
            sw.Configure(walk, boat, input, dock.transform, 3f, exit.transform);
            Assert.That(sw.TryInteract(), Is.True, "fixture boards through the public interaction");
            Assert.That(sw.Mode, Is.EqualTo(ControlMode.OnDeck));

            // Subscribe the failure before the camera and recorder. EditMode wiring is explicit.
            EventBus.Subscribe<ActiveBoatChanged>(_ => throw new IOException("helm-save"));
            var cameraGo = Go("Camera", Vector3.zero);
            cameraGo.AddComponent<Camera>().orthographic = true;
            var camera = cameraGo.AddComponent<CameraFollow>();
            camera.enabled = false;
            typeof(CameraFollow).GetField("_onFootTarget", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(camera, player.transform);
            typeof(CameraFollow).GetField("_boatTarget", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(camera, boatGo.transform);
            camera.Target = player.transform;
            EventBus.Subscribe<ActiveBoatChanged>(camera.OnActiveBoatChanged);
            EventBus.Subscribe<ControlModeChanged>(camera.OnControlModeChanged);
            var boats = new List<ActiveBoatChanged>();
            var modes = new List<ControlModeChanged>();
            EventBus.Subscribe<ActiveBoatChanged>(boats.Add);
            EventBus.Subscribe<ControlModeChanged>(modes.Add);
            player.transform.position = sw.HelmWorldPosition;
            Assert.That(sw.WithinHelmReach(), Is.True);

            LogAssert.Expect(LogType.Exception, new Regex("^IOException: helm-save(?:\\r?\\n|$)"));
            bool returned = sw.TryInteract();

            Assert.That(returned, Is.True);
            Assert.That(sw.Mode, Is.EqualTo(ControlMode.Aboard));
            Assert.That(boat.enabled && input.enabled, Is.True);
            Assert.That(walk.enabled || deck.enabled, Is.False);
            Assert.That(player.GetComponent<SpriteRenderer>().enabled, Is.False);
            Assert.That(boats.Count, Is.EqualTo(1));
            Assert.That(boats[0].BoatId, Is.EqualTo("boat.eventbus_test"));
            Assert.That(modes.Count, Is.EqualTo(1));
            Assert.That(modes[0].Mode, Is.EqualTo(ControlMode.Aboard));
            Assert.That(camera.Target, Is.SameAs(boatGo.transform));
        }
    }
}
