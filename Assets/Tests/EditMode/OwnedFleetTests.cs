using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Boats;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// VS-16 boat grant — the gameplay-systems half of the buy-the-Punt loop. The Economy Shipwright
    /// raises <see cref="BoatPurchased"/> by stable id; <see cref="OwnedFleet"/> listens and swaps the
    /// active boat. Covers: a known id swaps the BoatController + ShipHold to that hull via a
    /// data-driven registry lookup (hold grows 6→14) and swaps the sprite; an unknown id is a graceful
    /// no-op (no swap, no throw); the lookup is by Id, not DisplayName (no name special-casing). Defs
    /// are built in-code (CreateInstance) — no asset files needed. Drives the real handler through the
    /// real EventBus so the publish→swap path is what's under test.
    /// </summary>
    public class OwnedFleetTests
    {
        private readonly List<Object> _spawned = new();

        [SetUp]
        public void SetUp()
        {
            EventBus.Clear<BoatPurchased>();
            EventBus.Clear<ActiveBoatChanged>();
            EventBus.Clear<ControlModeChanged>();
            EventBus.Clear<GameLoaded>();
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Clear<BoatPurchased>();
            EventBus.Clear<ActiveBoatChanged>();
            EventBus.Clear<ControlModeChanged>();
            EventBus.Clear<GameLoaded>();
            foreach (var o in _spawned)
                if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        // ---- helpers ------------------------------------------------------------------------
        private BoatHullDef MakeHull(string id, int holdUnits, string displayName = null)
        {
            var h = ScriptableObject.CreateInstance<BoatHullDef>();
            h.Id = id;
            h.DisplayName = displayName ?? id;
            h.HoldUnits = holdUnits;
            _spawned.Add(h);
            return h;
        }

        private Sprite MakeSprite()
        {
            var tex = new Texture2D(4, 4);
            _spawned.Add(tex);
            var spr = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
            _spawned.Add(spr);
            return spr;
        }

        // Build a boat GO wired to a fleet, starting in the given hull, and subscribe the fleet to the
        // bus exactly once (EditMode doesn't run Awake, so we mirror its one-line subscription here).
        private (OwnedFleet fleet, BoatController boat, ShipHold hold, SpriteRenderer sr) MakeFleet(
            BoatHullDef[] registry, BoatHullDef startHull)
        {
            var go = new GameObject("Boat");
            _spawned.Add(go);
            go.AddComponent<Rigidbody2D>();
            var boat = go.AddComponent<BoatController>();
            var hold = go.AddComponent<ShipHold>();
            var sr = go.AddComponent<SpriteRenderer>();
            var fleet = go.AddComponent<OwnedFleet>();

            boat.SetHull(startHull);
            hold.SetHull(startHull);
            fleet.Configure(registry, boat, hold, sr);

            EventBus.Clear<BoatPurchased>();
            EventBus.Subscribe<BoatPurchased>(fleet.OnBoatPurchased);
            return (fleet, boat, hold, sr);
        }

        // ---- tests --------------------------------------------------------------------------

        [Test]
        public void Purchase_KnownId_SwapsHull_AndGrowsHold()
        {
            var dory = MakeHull("boat.dory", 6);
            var punt = MakeHull("boat.punt", 14);
            var (_, boat, hold, _) = MakeFleet(new[] { dory, punt }, dory);

            Assert.AreSame(dory, boat.Hull, "starts in the dory");
            Assert.AreEqual(6, hold.CapacityUnits, "dory hold is 6 HU");

            EventBus.Publish(new BoatPurchased("boat.punt", 1800));

            Assert.AreSame(punt, boat.Hull, "BoatController should be swapped to the Punt hull");
            Assert.AreEqual(14, hold.CapacityUnits, "hold should grow 6→14 with the Punt");
        }

        [Test]
        public void Purchase_UnknownId_IsGracefulNoOp()
        {
            var dory = MakeHull("boat.dory", 6);
            var punt = MakeHull("boat.punt", 14);
            var (_, boat, hold, _) = MakeFleet(new[] { dory, punt }, dory);

            Assert.DoesNotThrow(() => EventBus.Publish(new BoatPurchased("boat.galleon", 99999)),
                "an unknown boat id must not throw");

            Assert.AreSame(dory, boat.Hull, "unknown id must not swap the boat");
            Assert.AreEqual(6, hold.CapacityUnits, "hold unchanged on an unknown id");
        }

        [Test]
        public void Purchase_LooksUpById_NotByDisplayName()
        {
            // A hull whose DISPLAY NAME is "The Punt" but whose Id differs must NOT match "boat.punt".
            var dory = MakeHull("boat.dory", 6);
            var impostor = MakeHull("boat.impostor", 14, displayName: "The Punt");
            var (_, boat, hold, _) = MakeFleet(new[] { dory, impostor }, dory);

            EventBus.Publish(new BoatPurchased("boat.punt", 1800));

            Assert.AreSame(dory, boat.Hull, "matching must be by stable Id, never DisplayName");
            Assert.AreEqual(6, hold.CapacityUnits, "no swap → hold unchanged");
        }

        [Test]
        public void Purchase_SwapsSprite_WhenHullHasOne()
        {
            var dory = MakeHull("boat.dory", 6);
            var punt = MakeHull("boat.punt", 14);
            punt.Sprite = MakeSprite();
            var (_, _, _, sr) = MakeFleet(new[] { dory, punt }, dory);

            EventBus.Publish(new BoatPurchased("boat.punt", 1800));

            Assert.AreSame(punt.Sprite, sr.sprite, "the boat's sprite should swap to the Punt's");
        }

        [Test]
        public void Purchase_NoSpriteOnHull_LeavesRendererUntouched()
        {
            var dory = MakeHull("boat.dory", 6);
            var punt = MakeHull("boat.punt", 14);   // no sprite assigned
            var (_, _, _, sr) = MakeFleet(new[] { dory, punt }, dory);
            var startSprite = MakeSprite();
            sr.sprite = startSprite;

            EventBus.Publish(new BoatPurchased("boat.punt", 1800));

            Assert.AreSame(startSprite, sr.sprite, "no hull sprite → renderer left as-is (no null-swap)");
        }

        [Test]
        public void Purchase_OnFoot_DoesNotRepointCamera()
        {
            // The camera zoom keys off PILOTING, not OWNERSHIP. A buy at the wharf is the on-foot player
            // (default mode), so the grant must NOT zoom the on-foot view — the bug the owner hit was the
            // camera snapping to the Punt's framing the instant they bought it. The boat's framing instead
            // arrives via ControlSwitcher.Board() when they next step aboard.
            var dory = MakeHull("boat.dory", 6);
            var punt = MakeHull("boat.punt", 14);
            punt.CameraWorldHeightMeters = 17f;
            MakeFleet(new[] { dory, punt }, dory);   // fleet starts on foot (no ControlModeChanged seen yet)

            var events = new List<ActiveBoatChanged>();
            void Capture(ActiveBoatChanged e) => events.Add(e);
            EventBus.Subscribe<ActiveBoatChanged>(Capture);
            try
            {
                EventBus.Publish(new BoatPurchased("boat.punt", 1800));
                Assert.AreEqual(0, events.Count,
                    "buying a boat while on foot must NOT reframe the camera (zoom keys off piloting, not ownership)");
            }
            finally { EventBus.Unsubscribe<ActiveBoatChanged>(Capture); }
        }

        [Test]
        public void Purchase_WhileAboard_RepointsCamera_FromTheNewHull()
        {
            // The upgrade-at-sea case: if you take the grant WHILE already piloting, the view should reframe
            // to the new hull (bigger boat → more water). The fleet learns the mode off the Core seam.
            var dory = MakeHull("boat.dory", 6);
            var punt = MakeHull("boat.punt", 14);
            punt.CameraWorldHeightMeters = 17f;     // bigger boat → the camera should frame more water
            var (fleet, _, _, _) = MakeFleet(new[] { dory, punt }, dory);

            // Tell the fleet we're aboard, the way the ControlSwitcher would on boarding.
            fleet.OnControlModeChanged(new ControlModeChanged(ControlMode.Aboard));

            var events = new List<ActiveBoatChanged>();
            void Capture(ActiveBoatChanged e) => events.Add(e);
            EventBus.Subscribe<ActiveBoatChanged>(Capture);
            try
            {
                EventBus.Publish(new BoatPurchased("boat.punt", 1800));

                Assert.AreEqual(1, events.Count, "an upgrade taken while aboard reframes the camera exactly once");
                Assert.AreEqual("boat.punt", events[0].BoatId);
                Assert.AreEqual(17f, events[0].CameraWorldHeightMeters, 1e-4f,
                    "camera framing is data-driven from the new hull");
            }
            finally { EventBus.Unsubscribe<ActiveBoatChanged>(Capture); }
        }

        [Test]
        public void Purchase_BackOnFootAfterDisembark_DoesNotRepointCamera()
        {
            // Boarding then disembarking returns to on-foot; a later purchase must again not reframe.
            var dory = MakeHull("boat.dory", 6);
            var punt = MakeHull("boat.punt", 14);
            var (fleet, _, _, _) = MakeFleet(new[] { dory, punt }, dory);

            fleet.OnControlModeChanged(new ControlModeChanged(ControlMode.Aboard));
            fleet.OnControlModeChanged(new ControlModeChanged(ControlMode.OnFoot));

            var events = new List<ActiveBoatChanged>();
            void Capture(ActiveBoatChanged e) => events.Add(e);
            EventBus.Subscribe<ActiveBoatChanged>(Capture);
            try
            {
                EventBus.Publish(new BoatPurchased("boat.punt", 1800));
                Assert.AreEqual(0, events.Count, "after disembarking, a purchase on foot must not reframe");
            }
            finally { EventBus.Unsubscribe<ActiveBoatChanged>(Capture); }
        }

        [Test]
        public void Purchase_UnknownId_DoesNotRepointCamera()
        {
            var dory = MakeHull("boat.dory", 6);
            var punt = MakeHull("boat.punt", 14);
            var (fleet, _, _, _) = MakeFleet(new[] { dory, punt }, dory);
            fleet.OnControlModeChanged(new ControlModeChanged(ControlMode.Aboard));   // even while aboard…

            int count = 0;
            void Capture(ActiveBoatChanged e) => count++;
            EventBus.Subscribe<ActiveBoatChanged>(Capture);
            try
            {
                EventBus.Publish(new BoatPurchased("boat.galleon", 1));
                Assert.AreEqual(0, count, "an unknown id must not re-point the camera (no swap happened)");
            }
            finally { EventBus.Unsubscribe<ActiveBoatChanged>(Capture); }
        }

        // ---- VS-08 load-restore: the fleet re-grants the saved active hull -------------------

        [Test]
        public void RestoreFromSave_RestoresTheSavedActiveHull()
        {
            // A save where the player bought up to the Punt: restoring must put them back in the Punt
            // (hull + grown hold), through the same swap a live purchase uses — so reloading resumes you
            // aboard the boat you saved in, not back in the scene-default Dory.
            var dory = MakeHull("boat.dory", 6);
            var punt = MakeHull("boat.punt", 14);
            var (fleet, boat, hold, _) = MakeFleet(new[] { dory, punt }, dory);

            Assert.AreSame(dory, boat.Hull, "starts in the scene-default dory");

            fleet.RestoreFromSave(new SaveData
            {
                OwnedBoats   = new List<string> { "boat.dory", "boat.punt" },
                ActiveHullId = "boat.punt",
            });

            Assert.AreSame(punt, boat.Hull, "restore swaps to the saved active hull (the Punt)");
            Assert.AreEqual(14, hold.CapacityUnits, "the restored hull's hold capacity is applied (6→14)");
        }

        [Test]
        public void RestoreFromSave_EmptyOrUnknownActiveId_IsGracefulNoOp()
        {
            var dory = MakeHull("boat.dory", 6);
            var punt = MakeHull("boat.punt", 14);
            var (fleet, boat, hold, _) = MakeFleet(new[] { dory, punt }, dory);

            Assert.DoesNotThrow(() => fleet.RestoreFromSave(new SaveData { ActiveHullId = "" }),
                "an empty active id (fresh save) must not throw");
            Assert.AreSame(dory, boat.Hull, "empty active id → no swap, the scene-default hull stands");

            Assert.DoesNotThrow(() => fleet.RestoreFromSave(new SaveData { ActiveHullId = "boat.galleon" }),
                "an unknown active id must not throw");
            Assert.AreSame(dory, boat.Hull, "unknown active id → no swap (graceful, never null-swaps the player)");
            Assert.AreEqual(6, hold.CapacityUnits, "hold unchanged on a no-op restore");
        }

        [Test]
        public void RestoreFromSave_NullData_IsGracefulNoOp()
        {
            var dory = MakeHull("boat.dory", 6);
            var punt = MakeHull("boat.punt", 14);
            var (fleet, boat, _, _) = MakeFleet(new[] { dory, punt }, dory);

            Assert.DoesNotThrow(() => fleet.RestoreFromSave(null), "a null save must not throw");
            Assert.AreSame(dory, boat.Hull, "null save → no swap");
        }

        [Test]
        public void GameLoaded_Signal_DrivesTheFleetRestore_OffTheLiveSave()
        {
            // The runtime path: GameRoot publishes GameLoaded after restoring scalars; the fleet handles it
            // by reading GameServices.Save.Current and re-granting the active hull. Here we inject a fake
            // save into the Core seam and fire the signal through the real bus (EditMode doesn't run Awake,
            // so we subscribe the fleet's GameLoaded handler by hand — the way Awake does at runtime).
            var dory = MakeHull("boat.dory", 6);
            var punt = MakeHull("boat.punt", 14);
            var (fleet, boat, hold, _) = MakeFleet(new[] { dory, punt }, dory);
            EventBus.Subscribe<GameLoaded>(fleet.OnGameLoaded);

            var prevSave = GameServices.Save;
            GameServices.Save = new RestoreSaveStub(new SaveData { ActiveHullId = "boat.punt" });
            try
            {
                EventBus.Publish(new GameLoaded());
                Assert.AreSame(punt, boat.Hull, "GameLoaded → fleet reads the live save and restores the active hull");
                Assert.AreEqual(14, hold.CapacityUnits, "the restored hull's hold capacity is applied");
            }
            finally
            {
                EventBus.Unsubscribe<GameLoaded>(fleet.OnGameLoaded);
                GameServices.Save = prevSave;
            }
        }

        /// <summary>A bare ISaveService that just hands back a fixed blob — enough to drive the fleet's
        /// GameLoaded restore off the Core save seam in EditMode.</summary>
        private sealed class RestoreSaveStub : ISaveService
        {
            private readonly SaveData _data;
            public RestoreSaveStub(SaveData data) { _data = data; }
            public SaveData Current => _data;
            public bool GetFlag(string key) => false;
            public void SetFlag(string key, bool value) { }
            public void Save() { }
        }
    }
}
