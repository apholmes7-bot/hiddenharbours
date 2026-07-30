using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Boats;
using HiddenHarbours.Player;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// M1 §7.3 — the cold chain's mode-setting pieces, each bounded its own way: ICE + LID on the
    /// deck container (protection is a DURATION; sleeping through the ice running out lands on the
    /// exact unbroken-session number), Ginny's FREEZER (arrest, and the walk up in the sun is banked
    /// forever), and the WET BUCKET (Live — shellfish only; a mackerel never qualifies). All driven
    /// through the components' public seams with a pinned fake clock — no play loop, no input.
    /// </summary>
    public class ColdChainTests
    {
        private const float SecondsPerDay = GameConfig.DefaultSecondsPerDay;
        private const float SecondsPerHour = SecondsPerDay / 24f;

        private sealed class FakeClock : IGameClock
        {
            public double TotalSeconds { get; set; }
            public GameTime Now => new GameTime(TotalSeconds);
            public Season Season => Season.EarlySpring;
            public int Year => 1;
            public int DayIndex => 0;
            public int DayOfSeason => 1;
            public Weekday Weekday => Weekday.Monday;
            public bool IsMarketDay => false;
            public float HourOfDay => 0f;
            public float DayFraction => 0f;
            public bool IsPaused { get; set; }
            public float TimeScale { get; set; } = 1f;
        }

        private readonly List<Object> _spawned = new();
        private FakeClock _clock;

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();
            _clock = new FakeClock();
            GameServices.Clock = _clock;   // Config stays null → the pinned defaults (1200 s/day)
        }

        [TearDown]
        public void TearDown()
        {
            GameServices.Reset();
            foreach (var o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        // ---- rig builders ---------------------------------------------------------------------

        // A cod on the clock: 1 spoil/day ambient, landed (stamped) at t.
        private static CatchItem CodLandedAt(double t)
            => new CatchItem("fish.cod", "Cod", FishCategory.InshoreGroundfish, 4f, 20, 0.3f,
                             1f, Freshness.Landed(t));

        private static CatchItem ClamLandedAt(double t)
            => new CatchItem("fish.clam", "Clam", FishCategory.Shellfish, 0.1f, 2, 0.45f,
                             0.5f, Freshness.Landed(t));

        private static SpoilContext At(double now)
            => new SpoilContext(now, SecondsPerDay, SpoilPolicy.Default);

        private (ShipHold hold, DeckIceBox box) MakeIcedBoat(float iceHoursPerUnit = 6f,
                                                             float lidFactor = 2f, int maxUnits = 4)
        {
            var container = ScriptableObject.CreateInstance<DeckContainerDef>();
            container.IceHoursPerUnit = iceHoursPerUnit;
            container.LidMeltFactor = lidFactor;
            container.MaxIceUnits = maxUnits;
            _spawned.Add(container);

            var hull = ScriptableObject.CreateInstance<BoatHullDef>();
            hull.HoldUnits = 10;
            hull.DeckContainer = container;
            _spawned.Add(hull);

            var go = new GameObject("Boat");
            _spawned.Add(go);
            var hold = go.AddComponent<ShipHold>();
            hold.SetHull(hull);
            var box = go.AddComponent<DeckIceBox>();
            box.Configure(hold);   // EditMode: Awake doesn't run on AddComponent
            return (hold, box);
        }

        // ---- ice: protection that runs out ----------------------------------------------------

        [Test]
        public void AddIce_MovesTheCatchOntoIce_AndArrestsIt()
        {
            var (hold, box) = MakeIcedBoat();
            hold.TryAdd(CodLandedAt(0d));

            Assert.IsTrue(box.TryAddIce(1), "the tote takes ice");
            Assert.AreEqual(StorageMode.Iced, hold.Items[0].Freshness.Mode);

            // While the ice holds (1 unit = 6 h), nothing accrues.
            _clock.TotalSeconds = SecondsPerHour * 5f;
            Assert.AreEqual(0f, At(_clock.TotalSeconds).SpoilOf(hold.Items[0]), 1e-6f,
                            "on ice, the clock is stopped");
        }

        [Test]
        public void TimeInTheSunBeforeTheIce_IsBankedForever()
        {
            var (hold, box) = MakeIcedBoat();
            hold.TryAdd(CodLandedAt(0d));

            _clock.TotalSeconds = SecondsPerDay * 0.25f;   // a quarter-day ambient first
            box.TryAddIce(1);

            Assert.AreEqual(0.25f, hold.Items[0].Freshness.SpoilAccrued, 1e-5f,
                            "packing ice settles FIRST — the morning in the sun is banked");
        }

        [Test]
        public void IceRunsOut_SleepSkipLandsOnTheUnbrokenNumber()
        {
            var (hold, box) = MakeIcedBoat(iceHoursPerUnit: 6f);
            hold.TryAdd(CodLandedAt(0d));
            box.TryAddIce(1);   // protection ends at t = 6 h
            double iceGoneAt = box.ProtectionEndsAtGameSeconds;

            // Sleep straight past the expiry to t = 18 h, then the one tick fires.
            _clock.TotalSeconds = SecondsPerHour * 18f;
            box.TickProtection();

            // The unbroken-session number, computed straight from the Core maths.
            FreshnessState expected = Freshness.SettleThroughProtection(
                Freshness.Landed(0d, StorageMode.Iced), _clock.TotalSeconds, iceGoneAt,
                1f, SecondsPerDay, SpoilPolicy.Default);

            Assert.AreEqual(StorageMode.Ambient, hold.Items[0].Freshness.Mode, "the ice is gone");
            Assert.AreEqual(expected.SpoilAccrued, hold.Items[0].Freshness.SpoilAccrued, 1e-6f,
                            "the sleep-skip guarantee survives the protection boundary");
            Assert.AreEqual(0.5f, hold.Items[0].Freshness.SpoilAccrued, 1e-5f,
                            "12 unprotected hours at 1/day = half gone");
        }

        [Test]
        public void Lid_StretchesTheRemainingIce()
        {
            var (hold, box) = MakeIcedBoat(iceHoursPerUnit: 6f, lidFactor: 2f);
            hold.TryAdd(CodLandedAt(0d));
            box.TryAddIce(1);
            double bare = box.ProtectionEndsAtGameSeconds;

            box.SetLid(true);
            Assert.AreEqual(bare * 2d, box.ProtectionEndsAtGameSeconds, 1e-6,
                            "the lid doubles what's left");
            box.SetLid(false);
            Assert.AreEqual(bare, box.ProtectionEndsAtGameSeconds, 1e-6,
                            "and taking it off shrinks it back");
        }

        [Test]
        public void Ice_CapsAtTheDefsBank()
        {
            var (hold, box) = MakeIcedBoat(iceHoursPerUnit: 6f, maxUnits: 2);
            hold.TryAdd(CodLandedAt(0d));

            Assert.IsTrue(box.TryAddIce(5), "an over-ask clamps to the cap rather than refusing");
            Assert.AreEqual(SecondsPerHour * 12f, box.ProtectionEndsAtGameSeconds, 1e-3,
                            "2 units × 6 h is the most this tote banks");
            Assert.IsFalse(box.TryAddIce(1), "fully banked takes no more");
        }

        [Test]
        public void CatchLandedWhileTheIceHolds_GoesOntoIt_WithoutLosingItsHourInTheAir()
        {
            var (hold, box) = MakeIcedBoat();
            hold.TryAdd(CodLandedAt(0d));
            box.TryAddIce(1);

            // A second fish lands an hour into the trip; more ice goes in with it (the play-mode
            // FishCaught edge runs the same ReMode this exercises — EditMode drives the public seam).
            _clock.TotalSeconds = SecondsPerHour;
            hold.TryAdd(CodLandedAt(_clock.TotalSeconds));
            Assert.IsTrue(box.TryAddIce(1));

            Assert.AreEqual(StorageMode.Iced, hold.Items[0].Freshness.Mode, "the first stays iced");
            Assert.AreEqual(StorageMode.Iced, hold.Items[1].Freshness.Mode, "the newcomer joins it");
            Assert.AreEqual(0f, hold.Items[1].Freshness.SpoilAccrued, 1e-6f,
                            "landed and iced at the same instant — nothing banked");
        }

        // ---- the freezer: arrest, home only ---------------------------------------------------

        [Test]
        public void Freezer_Arrests_AndBanksTheWalkUp()
        {
            var go = new GameObject("Freezer");
            _spawned.Add(go);
            var freezer = go.AddComponent<GinnyFreezer>();

            _clock.TotalSeconds = SecondsPerDay * 0.5f;      // half a day in the sun on the way up
            Assert.IsTrue(freezer.TryAdd(CodLandedAt(0d)));

            Assert.AreEqual(StorageMode.Frozen, freezer.Items[0].Freshness.Mode);
            Assert.AreEqual(0.5f, freezer.Items[0].Freshness.SpoilAccrued, 1e-5f,
                            "the half-day in the sun is banked, never undone");

            _clock.TotalSeconds = SecondsPerDay * 9f;        // and nine days frozen add nothing
            Assert.AreEqual(0.5f, At(_clock.TotalSeconds).SpoilOf(freezer.Items[0]), 1e-5f);
        }

        // ---- the wet bucket: live, shellfish only ---------------------------------------------

        [Test]
        public void WetBucket_WetsShellfishOnly_AndTipsOut()
        {
            var bucketGo = new GameObject("Player");
            _spawned.Add(bucketGo);
            var bucket = bucketGo.AddComponent<ClamBucket>();
            bucket.Configure(20, requireOwnedBucket: false);
            bucket.TryAdd(ClamLandedAt(0d));
            bucket.TryAdd(CodLandedAt(0d));   // finfish — a wet bucket does nothing for it

            var wetGo = new GameObject("WetSpot");
            _spawned.Add(wetGo);
            var wet = wetGo.AddComponent<WetBucketPoint>();

            wet.Toggle();
            Assert.AreEqual(StorageMode.Live, FindBySpecies(bucket, "fish.clam").Freshness.Mode,
                            "the clam keeps");
            Assert.AreEqual(StorageMode.Ambient, FindBySpecies(bucket, "fish.cod").Freshness.Mode,
                            "the cod never qualifies — that limit is why ice is worth buying");

            wet.Toggle();
            Assert.AreEqual(StorageMode.Ambient, FindBySpecies(bucket, "fish.clam").Freshness.Mode,
                            "tipping the water out puts it back on the clock");
        }

        private static CatchItem FindBySpecies(IHold hold, string id)
        {
            var items = hold.Items;
            for (int i = 0; i < items.Count; i++)
                if (items[i].SpeciesId == id) return items[i];
            Assert.Fail($"no {id} in the hold");
            return default;
        }
    }
}
