using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// The deck fish tray end-to-end over the real runtime lifecycle: a boat rig with a
    /// <see cref="ShipHold"/> grows its <see cref="DeckContainerPresenter"/> at Awake (no builder run),
    /// the tray reads EMPTY, banding keepers through the unchanged land path (IHold.TryAdd +
    /// <see cref="FishCaught"/>) steps the fill states up, filling the hold reads BRIM, and the market's
    /// sell path (IHold.Clear then <see cref="CatchSold"/> — the SellService order) resets it to EMPTY.
    /// Placement is verified in the drawn hull's deck frame: the authored anchor rides the boat's
    /// heading, and the tray sprite itself stays screen-upright (the DirectionalBoatSprite convention).
    /// </summary>
    public class DeckTrayPlayTests
    {
        private readonly List<Object> _spawned = new();

        [SetUp]
        public void SetUp()
        {
            EventBus.Clear<FishCaught>();
            EventBus.Clear<CatchSold>();
            EventBus.Clear<GameLoaded>();
            EventBus.Clear<BoatPurchased>();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _spawned) if (o != null) Object.Destroy(o);
            _spawned.Clear();
            EventBus.Clear<FishCaught>();
            EventBus.Clear<CatchSold>();
            EventBus.Clear<GameLoaded>();
            EventBus.Clear<BoatPurchased>();
        }

        private (GameObject boat, ShipHold hold, BoatHullDef hull) MakeBoat(int holdUnits)
        {
            var container = ScriptableObject.CreateInstance<DeckContainerDef>();
            container.Id = "container.play_test_tray";
            container.DisplayName = "Play-test tray";
            container.FillSprites = null;   // greybox silhouettes — the shipped fallback path
            _spawned.Add(container);

            var hull = ScriptableObject.CreateInstance<BoatHullDef>();
            hull.Id = "boat.play_test";
            hull.DisplayName = "Play-test skiff";
            hull.HoldUnits = holdUnits;
            hull.DeckContainer = container;
            hull.DeckContainerOffset = new Vector2(0.35f, -0.9f);
            _spawned.Add(hull);

            var boatGo = new GameObject("PlayTestBoat");
            _spawned.Add(boatGo);
            var hold = boatGo.AddComponent<ShipHold>();   // Awake spawns the presenter (runtime, no builder)
            hold.SetHull(hull);
            return (boatGo, hold, hull);
        }

        private static SpriteRenderer TrayRenderer(GameObject boat)
        {
            Transform t = boat.transform.Find("DeckContainer");
            return t != null ? t.GetComponent<SpriteRenderer>() : null;
        }

        private static CatchItem Lobster()
            => new CatchItem("fish.lobster", "American Lobster", FishCategory.Shellfish, 1.1f, 28, 0.35f);

        [UnityTest]
        public IEnumerator TrayFillsWithBandedKeepers_AndEmptiesOnTheSale()
        {
            (GameObject boatGo, ShipHold hold, BoatHullDef _) = MakeBoat(holdUnits: 6);

            yield return null;   // a real frame: Awake spawned the presenter, LateUpdate placed the tray

            var presenter = boatGo.GetComponent<DeckContainerPresenter>();
            Assert.IsNotNull(presenter, "a hold GROWS its diegetic read at Awake — no builder re-run needed");
            SpriteRenderer tray = TrayRenderer(boatGo);
            Assert.IsNotNull(tray, "the tray prop exists on the deck");
            Assert.IsTrue(tray.enabled, "a hull with a container shows its tray");
            Sprite emptySprite = tray.sprite;
            Assert.IsNotNull(emptySprite, "the empty tray is drawn (greybox state 0)");

            // BAND A KEEPER — the unchanged #193 land path: the hold takes it, FishCaught announces it.
            CatchItem keeper = Lobster();
            Assert.IsTrue(hold.TryAdd(keeper));
            EventBus.Publish(new FishCaught(keeper));
            yield return null;
            Assert.AreNotEqual(emptySprite, tray.sprite, "one banded keeper VISIBLY gains — the tray never reads empty with catch aboard");
            Sprite lowSprite = tray.sprite;

            // FILL TO THE BRIM — the full hold heaps the tray (the last pinned state).
            while (hold.UsedUnits < hold.CapacityUnits)
            {
                CatchItem item = Lobster();
                Assert.IsTrue(hold.TryAdd(item));
                EventBus.Publish(new FishCaught(item));
            }
            yield return null;
            Sprite brimSprite = tray.sprite;
            Assert.AreNotEqual(lowSprite, brimSprite, "a full hold reads fuller than one keeper");

            // SELL AT THE WHARF — the SellService order: the hold is CLEARED, then one CatchSold.
            hold.Clear();
            EventBus.Publish(new CatchSold(168, 6));
            yield return null;
            Assert.AreEqual(emptySprite, tray.sprite, "sold out — the tray reads empty again");
        }

        [UnityTest]
        public IEnumerator TrayRidesTheDeckFrame_AndStaysScreenUpright()
        {
            (GameObject boatGo, ShipHold hold, BoatHullDef hull) = MakeBoat(holdUnits: 6);

            yield return null;
            SpriteRenderer tray = TrayRenderer(boatGo);
            Assert.IsNotNull(tray);

            // Bow north (transform.up = +Y → drawn heading 0): the deck frame IS world axes.
            Vector2 rel = (Vector2)tray.transform.position - (Vector2)boatGo.transform.position;
            Assert.AreEqual(0.35f, rel.x, 1e-3f, "starboard anchor, bow north");
            Assert.AreEqual(-0.9f, rel.y, 1e-3f, "aft anchor, bow north");

            // Yaw the physics root to bow EAST (no facing art → the true heading drives the frame):
            // the tray swings to the same spot of the TURNED deck…
            boatGo.transform.rotation = Quaternion.Euler(0f, 0f, -90f);   // +Y spun to +X
            yield return null;
            rel = (Vector2)tray.transform.position - (Vector2)boatGo.transform.position;
            Vector2 expected = DeckContainerPresenter.DeckOffsetToWorld(hull.DeckContainerOffset, 90f);
            Assert.AreEqual(expected.x, rel.x, 1e-3f, "the anchor turned with the drawn bow");
            Assert.AreEqual(expected.y, rel.y, 1e-3f, "the anchor turned with the drawn bow");

            // …but the tray PICTURE never rotates (screen-upright, the DirectionalBoatSprite convention).
            Assert.AreEqual(Quaternion.identity, tray.transform.rotation, "the tray sprite stays screen-upright");
        }

        [UnityTest]
        public IEnumerator HullSwap_MovesTheReadToTheNewHull_AndNoContainerHidesIt()
        {
            (GameObject boatGo, ShipHold hold, BoatHullDef hull) = MakeBoat(holdUnits: 6);
            yield return null;
            SpriteRenderer tray = TrayRenderer(boatGo);
            Assert.IsTrue(tray.enabled);

            // A hull with NO container (e.g. a future big hull awaiting its totes) hides the prop —
            // the swap needs no event: the presenter's hull ref-compare catches a direct SetHull.
            var bare = ScriptableObject.CreateInstance<BoatHullDef>();
            bare.Id = "boat.play_test_bare";
            bare.HoldUnits = 14;
            bare.DeckContainer = null;
            _spawned.Add(bare);
            hold.SetHull(bare);
            yield return null;
            Assert.IsFalse(tray.enabled, "no container on the hull → no tray prop");

            // Swap back: the read returns.
            hold.SetHull(hull);
            yield return null;
            Assert.IsTrue(tray.enabled, "the tray returns with the container-carrying hull");
        }
    }
}
