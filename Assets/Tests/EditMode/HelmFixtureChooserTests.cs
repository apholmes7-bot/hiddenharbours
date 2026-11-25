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
    /// The helm press with a fixture at the wheel (owner ask 2026-11-25: the cabin-light switch at the
    /// helm, reachable without leaving it). At the helm the press offers "Leave the helm" and the switch;
    /// next to the helm on deck it offers "Take the helm" and the switch. With nothing at the wheel the
    /// press is what it was on main: these cases pin that too.
    /// </summary>
    public class HelmFixtureChooserTests
    {
        private sealed class FakeHelmFixture : IInteractable, IHelmFixture
        {
            public string FixtureId;
            public string Label;
            public Vector2 At;
            public GameObject Boat;
            public int Presses;

            public string Id => FixtureId;
            public string VerbLabel => Label;
            public Vector2 WorldPosition => At;
            public float ReachMeters => 1f;
            public int Priority => InteractPriority.Fixture;
            public InteractContext Contexts => InteractContext.OnDeck | InteractContext.AtHelm;
            public bool RequiresFacing => false;
            public bool IsAvailable => true;
            public GameObject HelmBoat => Boat;
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

        // The ControlSwitcherTests rig: a switcher on foot beside a dory, dock zone at (0,-12) r=3.
        private (ControlSwitcher sw, GameObject playerGo, GameObject boatGo) Build()
        {
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

            TimedBoarding.Interact(sw);                                  // board -> deck
            Assert.AreEqual(ControlMode.OnDeck, sw.Mode, "fixture: aboard on deck");
            playerGo.transform.position = sw.HelmWorldPosition;          // walk to the tiller
            return (sw, playerGo, boatGo);
        }

        private static void TakeTheHelm(ControlSwitcher sw)
        {
            TimedBoarding.Interact(sw);
            Assert.AreEqual(ControlMode.Aboard, sw.Mode, "fixture: at the helm");
        }

        // A real switch at the wheel, on the player's boat, registered by hand (EditMode runs no OnEnable).
        private (CabinLightSwitch sw, CabinLightSwitchboard board) FitSwitch(ControlSwitcher at, GameObject boatGo,
                                                                         CabinLightSwitchRole role, bool playersBoat = true)
        {
            var board = boatGo.AddComponent<CabinLightSwitchboard>();   // once per case
            board.BeginBoat("boat.dory#1");
            var go = NewGo("CabinLightSwitch.helm", at.HelmWorldPosition);
            go.transform.SetParent(boatGo.transform, worldPositionStays: true);
            var s = go.AddComponent<CabinLightSwitch>();
            s.Configure(board, null, CabinLightSwitchRule.FixtureId("interior.dory", "helm"), role, Vector3.zero);
            Interactables.Register(s);
            GameServices.Helm.SetPlayersBoat(playersBoat ? boatGo : null);
            return (s, board);
        }

        private void NextFrame() => _frame++;

        [Test]
        public void AtTheHelm_WithASwitch_ThePressOffersBoth_AndTheSwitchKeepsHimAtTheWheel()
        {
            var (sw, _, boatGo) = Build();
            TakeTheHelm(sw);
            var (_, board) = FitSwitch(sw, boatGo, CabinLightSwitchRole.Helm);

            NextFrame();
            Assert.IsTrue(sw.TryInteract(), "the press is spent on the list");
            Assert.IsTrue(InteractChooser.IsOpen, "a switch at the wheel makes the helm press a choice");
            Assert.AreEqual(ControlMode.Aboard, sw.Mode, "opening the list leaves him at the helm");
            Assert.AreEqual(2, InteractChooser.Rows.Count);
            Assert.AreEqual(ControlStrings.LeaveHelm, InteractChooser.Rows[0].Label, "row 0 is the station");
            Assert.AreEqual(CabinLightSwitch.VerbOn, InteractChooser.Rows[1].Label, "row 1 is the switch");
            Assert.AreEqual(0, InteractChooser.Rows.Selected, "the list opens on what the press always meant");

            NextFrame();
            Assert.IsTrue(InteractChooser.Pick(1));
            Assert.AreEqual(CabinLightSwitchSetting.On, board.Setting, "choosing the switch throws it");
            Assert.AreEqual(ControlMode.Aboard, sw.Mode, "…and he is still at the wheel");

            // The press that picked the row is still down this frame: it must not also step him off.
            Assert.IsTrue(sw.TryInteract(), "the picking press is swallowed");
            Assert.AreEqual(ControlMode.Aboard, sw.Mode);
            Assert.IsFalse(InteractChooser.IsOpen);

            NextFrame();
            sw.TryInteract();
            Assert.AreEqual(CabinLightSwitch.VerbOff, InteractChooser.Rows[1].Label, "the row now names the other way");
            NextFrame();
            Assert.IsTrue(InteractChooser.Pick(0));
            Assert.AreEqual(ControlMode.OnDeck, sw.Mode, "row 0 leaves the helm as the plain press did");
            Assert.AreEqual(CabinLightSwitchSetting.On, board.Setting, "leaving the helm does not touch the lights");
        }

        [Test]
        public void NextToTheHelmOnDeck_ThePressOffersTakeTheHelm_AndTheSwitch()
        {
            var (sw, _, boatGo) = Build();
            var (_, board) = FitSwitch(sw, boatGo, CabinLightSwitchRole.Helm);

            NextFrame();
            Assert.IsTrue(sw.TryInteract());
            Assert.IsTrue(InteractChooser.IsOpen);
            Assert.AreEqual(ControlMode.OnDeck, sw.Mode, "opening the list does not take the helm");
            Assert.AreEqual(ControlStrings.TakeHelm, InteractChooser.Rows[0].Label);
            Assert.AreEqual(CabinLightSwitch.VerbOn, InteractChooser.Rows[1].Label);

            NextFrame();
            Assert.IsTrue(InteractChooser.Pick(0));
            Assert.AreEqual(ControlMode.Aboard, sw.Mode, "row 0 takes the helm");
            Assert.AreEqual(CabinLightSwitchSetting.Auto, board.Setting, "and leaves the lights alone");
        }

        [Test]
        public void NoHelmFixture_TheHelmPressIsExactlyMains()
        {
            var (sw, _, _) = Build();

            NextFrame();
            Assert.IsTrue(sw.TryInteract());
            Assert.IsFalse(InteractChooser.IsOpen, "nothing at the wheel: no list");
            Assert.AreEqual(ControlMode.Aboard, sw.Mode, "the deck press takes the helm at once");

            NextFrame();
            Assert.IsTrue(sw.TryInteract());
            Assert.IsFalse(InteractChooser.IsOpen);
            Assert.AreEqual(ControlMode.OnDeck, sw.Mode, "the helm press steps back at once");
        }

        [Test]
        public void ADoorSwitch_OrAnotherBoatsSwitch_LeavesTheHelmPressAsItWas()
        {
            var (sw, _, boatGo) = Build();
            FitSwitch(sw, boatGo, CabinLightSwitchRole.Door);                    // not at a helm
            var other = NewGo("OtherBoat", boatGo.transform.position);
            Interactables.Register(new FakeHelmFixture
            {
                FixtureId = "fixture.other", Label = "Other", At = sw.HelmWorldPosition, Boat = other,
            });

            NextFrame();
            sw.TryInteract();
            Assert.IsFalse(InteractChooser.IsOpen);
            Assert.AreEqual(ControlMode.Aboard, sw.Mode);
            NextFrame();
            sw.TryInteract();
            Assert.IsFalse(InteractChooser.IsOpen);
            Assert.AreEqual(ControlMode.OnDeck, sw.Mode);
        }

        [Test]
        public void ANpcBoatsHelmSwitch_IsNotOffered()
        {
            var (sw, _, boatGo) = Build();
            TakeTheHelm(sw);
            var (_, board) = FitSwitch(sw, boatGo, CabinLightSwitchRole.Helm, playersBoat: false);

            NextFrame();
            sw.TryInteract();
            Assert.IsFalse(InteractChooser.IsOpen, "the switch is only his on his own boat");
            Assert.AreEqual(ControlMode.OnDeck, sw.Mode);
            Assert.AreEqual(CabinLightSwitchSetting.Auto, board.Setting);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TheRows_AreStationThenFixturesById_WhateverTheRegistrationOrder(bool reversed)
        {
            var (sw, _, boatGo) = Build();
            TakeTheHelm(sw);
            var a = new FakeHelmFixture { FixtureId = "fixture.a", Label = "A", At = sw.HelmWorldPosition, Boat = boatGo };
            var b = new FakeHelmFixture { FixtureId = "fixture.b", Label = "B", At = sw.HelmWorldPosition, Boat = boatGo };
            if (reversed) { Interactables.Register(b); Interactables.Register(a); }
            else { Interactables.Register(a); Interactables.Register(b); }

            NextFrame();
            sw.TryInteract();
            Assert.AreEqual(3, InteractChooser.Rows.Count);
            Assert.AreEqual(ControlStrings.LeaveHelm, InteractChooser.Rows[0].Label);
            Assert.AreEqual("A", InteractChooser.Rows[1].Label);
            Assert.AreEqual("B", InteractChooser.Rows[2].Label);

            // A gamepad's down-press walks the same rows: station, A, B.
            Assert.IsTrue(InteractChooser.Step(1));
            Assert.AreEqual(1, InteractChooser.Rows.Selected);
            NextFrame();
            Assert.IsTrue(InteractChooser.Confirm());
            Assert.AreEqual(1, a.Presses);
            Assert.AreEqual(0, b.Presses);
            Assert.AreEqual(ControlMode.Aboard, sw.Mode);
        }
    }
}
