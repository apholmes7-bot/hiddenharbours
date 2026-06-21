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
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Clear<BoatPurchased>();
            EventBus.Clear<ActiveBoatChanged>();
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
        public void Purchase_RepointsCamera_ViaActiveBoatChanged_FromTheNewHull()
        {
            var dory = MakeHull("boat.dory", 6);
            var punt = MakeHull("boat.punt", 14);
            punt.CameraWorldHeightMeters = 17f;     // bigger boat → the camera should frame more water
            MakeFleet(new[] { dory, punt }, dory);

            var events = new List<ActiveBoatChanged>();
            void Capture(ActiveBoatChanged e) => events.Add(e);
            EventBus.Subscribe<ActiveBoatChanged>(Capture);
            try
            {
                EventBus.Publish(new BoatPurchased("boat.punt", 1800));

                Assert.AreEqual(1, events.Count, "the swap should re-point the camera exactly once");
                Assert.AreEqual("boat.punt", events[0].BoatId);
                Assert.AreEqual(17f, events[0].CameraWorldHeightMeters, 1e-4f,
                    "camera framing is data-driven from the new hull");
            }
            finally { EventBus.Unsubscribe<ActiveBoatChanged>(Capture); }
        }

        [Test]
        public void Purchase_UnknownId_DoesNotRepointCamera()
        {
            var dory = MakeHull("boat.dory", 6);
            var punt = MakeHull("boat.punt", 14);
            MakeFleet(new[] { dory, punt }, dory);

            int count = 0;
            void Capture(ActiveBoatChanged e) => count++;
            EventBus.Subscribe<ActiveBoatChanged>(Capture);
            try
            {
                EventBus.Publish(new BoatPurchased("boat.galleon", 1));
                Assert.AreEqual(0, count, "an unknown id must not re-point the camera");
            }
            finally { EventBus.Unsubscribe<ActiveBoatChanged>(Capture); }
        }
    }
}
