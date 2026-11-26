using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Boats;
using HiddenHarbours.Player;
using HiddenHarbours.Tests.Support;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The cabin-light switch takes no press from anything else (2026-11-25: the first draft put a list
    /// in front of the helm and the door, and E at both stopped doing what it does on main). The helm
    /// keeps E at the wheel and beside it; a door beside the switch keeps E and no list opens; the
    /// switch answers only when it is the one thing in reach.
    /// </summary>
    public class CabinLightSwitchPressTests
    {
        private sealed class FakeDoor : IInteractable
        {
            public Vector2 At;
            public int Presses;
            public bool Available = true;
            public string Id => "fixture.test.door";
            public string VerbLabel => "Open the door";
            public Vector2 WorldPosition => At;
            public float ReachMeters => 1.2f;
            public int Priority => InteractPriority.Fixture;
            public InteractContext Contexts => InteractContext.OnDeck;
            public bool RequiresFacing => false;
            public bool IsAvailable => Available;
            public void Interact(in InteractActor actor) => Presses++;
        }

        private readonly List<Object> _spawned = new();
        private int _frame;

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();
            ResetTheInteractSeam();
            _frame = 1;
            InteractChooser.FrameClock = () => _frame;
        }

        [TearDown]
        public void TearDown()
        {
            ResetTheInteractSeam();
            EventBus.Clear<ControlModeChanged>();
            EventBus.Clear<ActiveBoatChanged>();
            EventBus.Clear<CabinLightSwitchThrown>();
            GameServices.Reset();
            foreach (var o in _spawned)
                if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        private static void ResetTheInteractSeam()
        {
            InteractChooser.Reset();
            Interactables.Clear();
            InteractVerb.Reset();
            InteractionGate.Reset();
            InteractActionClaim.Reset();
            InteractOffer.Reset();
            InteractActorProbe.Reset();
        }

        private GameObject NewGo(string name, Vector3 pos)
        {
            var g = new GameObject(name);
            g.transform.position = pos;
            _spawned.Add(g);
            return g;
        }

        // A switch on the player's boat at a world point, registered by hand (EditMode runs no OnEnable).
        private (CabinLightSwitch sw, CabinLightSwitchboard board) Switch(GameObject boatGo, Vector3 at,
                                                                         CabinLightSwitchRole role)
        {
            var board = boatGo.AddComponent<CabinLightSwitchboard>();
            board.BeginBoat("boat.test#1");
            var go = NewGo("CabinLightSwitch." + role, at);
            go.transform.SetParent(boatGo.transform, worldPositionStays: true);
            var s = go.AddComponent<CabinLightSwitch>();
            s.Configure(board, null, CabinLightSwitchRule.FixtureId("interior.test", role.ToString()), role, Vector3.zero);
            Interactables.Register(s);
            GameServices.Helm.SetPlayersBoat(boatGo);
            return (s, board);
        }

        private static InteractActor OnDeckAt(Vector3 p) => InteractActor.For(p, Vector2.zero, ControlMode.OnDeck);

        [Test]
        public void ASwitchAtTheHelm_LeavesTheHelmPressExactlyAsOnMain()
        {
            // The ControlSwitcherTests rig: aboard a dory on deck, walked to the tiller.
            var playerGo = NewGo("Player", new Vector3(0f, -11.5f, 0f));
            var walk = playerGo.AddComponent<PlayerWalkController>();
            playerGo.AddComponent<DeckWalkController>().enabled = false;
            var boatGo = NewGo("Boat", new Vector3(0f, -13.8f, 0f));
            var boat = boatGo.AddComponent<BoatController>();
            var input = boatGo.AddComponent<DevBoatInput>();
            var hull = ScriptableObject.CreateInstance<BoatHullDef>();
            hull.Id = "boat.dory"; hull.CameraWorldHeightMeters = 14f;
            _spawned.Add(hull);
            boat.SetHull(hull);
            walk.enabled = true; boat.enabled = false; input.enabled = false;
            var dock = NewGo("DockZone", new Vector3(0f, -12f, 0f));
            var disembark = NewGo("Disembark", new Vector3(0f, -10.5f, 0f));
            var sw = NewGo("Switcher", Vector3.zero).AddComponent<ControlSwitcher>();
            sw.Configure(walk, boat, input, dock.transform, 3f, disembark.transform);
            TimedBoarding.Interact(sw);
            Assert.AreEqual(ControlMode.OnDeck, sw.Mode, "fixture: aboard on deck");
            playerGo.transform.position = sw.HelmWorldPosition;

            var (_, board) = Switch(boatGo, sw.HelmWorldPosition, CabinLightSwitchRole.Helm);

            Assert.IsTrue(sw.BeginInteract());
            Assert.IsFalse(InteractChooser.IsOpen, "no list in front of the helm");
            Assert.AreEqual(ControlMode.Aboard, sw.Mode, "E beside the helm takes it at once");
            _frame++;
            Assert.IsTrue(sw.BeginInteract());
            Assert.IsFalse(InteractChooser.IsOpen);
            Assert.AreEqual(ControlMode.OnDeck, sw.Mode, "E at the wheel steps back at once");
            Assert.AreEqual(CabinLightSwitchSetting.Auto, board.Setting, "neither press threw the switch");
        }

        [Test]
        public void BesideADoor_TheDoorKeepsThePress_AndNoListOpens()
        {
            var boatGo = NewGo("Boat", Vector3.zero);
            var (_, board) = Switch(boatGo, Vector3.zero, CabinLightSwitchRole.Door);
            var door = new FakeDoor { At = new Vector2(0.3f, 0f) };
            Interactables.Register(door);

            Assert.IsTrue(InteractVerb.TryPress(OnDeckAt(Vector3.zero), InteractResolver.DefaultFacingArcDegrees));
            Assert.IsFalse(InteractChooser.IsOpen, "the switch is never a row beside the door");
            Assert.AreEqual(1, door.Presses, "the door had the press");
            Assert.AreEqual(CabinLightSwitchSetting.Auto, board.Setting);
        }

        [Test]
        public void AloneInReach_ThePressThrowsTheSwitch()
        {
            var boatGo = NewGo("Boat", Vector3.zero);
            var (_, board) = Switch(boatGo, Vector3.zero, CabinLightSwitchRole.Door);
            var door = new FakeDoor { At = new Vector2(5f, 0f) };   // out of reach
            Interactables.Register(door);

            Assert.IsTrue(InteractVerb.TryPress(OnDeckAt(Vector3.zero), InteractResolver.DefaultFacingArcDegrees));
            Assert.IsFalse(InteractChooser.IsOpen);
            Assert.AreEqual(0, door.Presses);
            Assert.AreEqual(CabinLightSwitchSetting.On, board.Setting, "with nothing else in reach, E throws it");
        }

        [Test]
        public void TheSwitch_IsNeverOfferedAtTheHelm()
        {
            var boatGo = NewGo("Boat", Vector3.zero);
            var (sw, _) = Switch(boatGo, Vector3.zero, CabinLightSwitchRole.Helm);
            Assert.AreEqual(InteractPriority.LastResort, sw.Priority);
            Assert.AreEqual(InteractContext.OnDeck, sw.Contexts);
            var atHelm = InteractActor.For(Vector2.zero, Vector2.zero, ControlMode.Aboard);
            Assert.IsFalse(InteractResolver.Qualifies(sw, atHelm, Vector2.zero, 1f, out _));
        }
    }
}
