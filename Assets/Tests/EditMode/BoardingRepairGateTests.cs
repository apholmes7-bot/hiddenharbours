using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Boats;
using HiddenHarbours.Economy;
using HiddenHarbours.Player;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// St Peters DAMAGED-DORY BOARDING GATE (P5): the dory bought damaged is owned but can't be boarded
    /// until the player pays the shipwright to repair it; after <c>BoatRepaired</c> it boards normally.
    /// The ControlSwitcher reads the single source of truth (<see cref="RepairLedger.IsRepaired"/> over
    /// the live save). Covers: owned-but-damaged blocks boarding; repairing flips it boardable; the gate
    /// self-disables with no save / for an un-owned hull (so the greybox start and tests aren't gated).
    /// Driven through the public API + the save seam — no play-mode lifecycle.
    /// </summary>
    public class BoardingRepairGateTests
    {
        private sealed class FakeSaveService : ISaveService
        {
            public FakeSaveService(SaveData data) { Current = data; }
            public SaveData Current { get; }
            public bool GetFlag(string key) => false;
            public void SetFlag(string key, bool value) { }
            public void Save() { }
        }

        private readonly List<Object> _spawned = new();

        [SetUp]
        public void SetUp() => GameServices.Reset();

        [TearDown]
        public void TearDown()
        {
            GameServices.Reset();
            foreach (var o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        private GameObject NewGo(string name, Vector3 pos)
        {
            var g = new GameObject(name);
            g.transform.position = pos;
            _spawned.Add(g);
            return g;
        }

        // A switcher with the player standing in the dock zone, started on foot. Hull id "boat.dory".
        private ControlSwitcher BuildInBoardZone()
        {
            var playerGo = NewGo("Player", new Vector3(0f, -11.5f, 0f));
            var walk = playerGo.AddComponent<PlayerWalkController>();
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
            var swGo = NewGo("Switcher", Vector3.zero);
            var sw = swGo.AddComponent<ControlSwitcher>();
            sw.Configure(walk, boat, input, dock.transform, 3f, disembark.transform);
            return sw;
        }

        [Test]
        public void OwnedButDamagedDory_CannotBoard()
        {
            var save = SaveMigration.NewGame();
            save.OwnedBoats.Add("boat.dory");          // owned...
            // ...but NOT in RepairedBoats → damaged.
            GameServices.Save = new FakeSaveService(save);

            var sw = BuildInBoardZone();

            Assert.IsTrue(sw.InBoardZone(), "the player is standing at the mooring");
            Assert.IsFalse(sw.BoardableNow(), "a damaged, unrepaired dory can't be boarded");
            Assert.IsFalse(sw.CanInteract(), "so INTERACT won't board");
            Assert.IsFalse(sw.TryInteract(), "the board is refused");
            Assert.AreEqual(ControlMode.OnFoot, sw.Mode, "still on foot — she needs repairs first");
        }

        [Test]
        public void AfterRepair_BoardsNormally()
        {
            var save = SaveMigration.NewGame();
            save.OwnedBoats.Add("boat.dory");
            GameServices.Save = new FakeSaveService(save);
            var sw = BuildInBoardZone();
            Assert.IsFalse(sw.BoardableNow(), "starts un-boardable (damaged)");

            // The shipwright repairs her (what TryRepair does): mark repaired, raise BoatRepaired.
            RepairLedger.MarkRepaired(save, "boat.dory");

            Assert.IsTrue(sw.BoardableNow(), "after repair the dory is boardable");
            Assert.IsTrue(sw.TryInteract(), "and INTERACT boards her");
            Assert.AreEqual(ControlMode.Aboard, sw.Mode);
        }

        [Test]
        public void NoSave_GateOff_BoardsNormally()
        {
            // No GameServices.Save wired (the greybox start before any purchase flow / tests).
            var sw = BuildInBoardZone();
            Assert.IsTrue(sw.BoardableNow(), "no save → gate off, ordinary boarding");
            Assert.IsTrue(sw.TryInteract());
            Assert.AreEqual(ControlMode.Aboard, sw.Mode);
        }

        [Test]
        public void UnownedHull_NotGated()
        {
            // A save exists, but this hull isn't in OwnedBoats (e.g. the inherited start dory before any
            // buy+repair flow). It isn't part of the damaged-buy gate, so it stays boardable.
            var save = SaveMigration.NewGame();   // empty fleet
            GameServices.Save = new FakeSaveService(save);
            var sw = BuildInBoardZone();

            Assert.IsTrue(sw.BoardableNow(), "an un-owned hull isn't subject to the buy+repair gate");
            Assert.IsTrue(sw.TryInteract());
            Assert.AreEqual(ControlMode.Aboard, sw.Mode);
        }
    }
}
