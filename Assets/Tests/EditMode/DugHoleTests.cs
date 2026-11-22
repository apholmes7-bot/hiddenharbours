using System;
using System.Collections.Generic;
using System.Linq;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using HiddenHarbours.Environment;
using HiddenHarbours.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The shovel's hole (owner ask 2026-11-22): every dig, the player's or a working villager's, leaves a
    /// dark hole that stays for the rest of that game day or until the tide covers it, whichever is first;
    /// and the toss is bigger. Pure rules (<see cref="DugHoleMath"/>, <see cref="DugHoleBook"/>,
    /// <see cref="BeachLifeDirector.AppendDigHoles"/>) plus the pooled presenter, on a fake ground and clock.
    /// </summary>
    public class DugHoleTests
    {
        private const string Region = "region.dug_hole_test";

        private sealed class FakeGround : IDugHoleGround
        {
            public float Elevation = 1f;
            public Func<double, float> Water = _ => -1f;
            public bool HasTide => true;
            public float ElevationAt(Vector2 worldPos) => Elevation;
            public float WaterLevelAt(double totalSeconds) => Water(totalSeconds);
        }

        private sealed class FakeClock : IGameClock
        {
            public double T;
            public double TotalSeconds => T;
            public GameTime Now => new GameTime(T);
            public Season Season => Season.HighSummer;
            public int Year => 1;
            public int DayIndex => (int)(T / GameServices.SecondsPerDay);
            public int DayOfSeason => 1;
            public Weekday Weekday => Weekday.Monday;
            public bool IsMarketDay => false;
            public float HourOfDay => (float)(T % GameServices.SecondsPerDay / GameServices.SecondsPerDay * 24.0);
            public float DayFraction => HourOfDay / 24f;
            public bool IsPaused { get; set; }
            public float TimeScale { get; set; } = 1f;
        }

        /// <summary>A villager's digging outing, asked the way the director asks it.</summary>
        private sealed class OutingSource : IDugHoleSource
        {
            public RoutinePlan Plan;
            public BeachOuting Outing;
            public uint Seed = 0xC1A35EEDu;
            public void CollectDugHoles(double nowSeconds, List<DugHole> into)
            {
                double spd = GameServices.SecondsPerDay;
                BeachLifeDirector.AppendDigHoles(Plan, Outing, Seed, DugHoleMath.DayStartSeconds(nowSeconds, spd),
                                                 (float)(spd / 24.0), nowSeconds, DugHoleSettings.Fallback, spd, into);
            }
        }

        private readonly List<Object> _spawned = new List<Object>();
        private IGameClock _previousClock;
        private GameConfig _previousConfig;
        private string _previousRegion;
        private FakeClock _clock;

        [SetUp]
        public void SetUp()
        {
            _previousClock = GameServices.Clock;
            _previousConfig = GameServices.Config;
            _previousRegion = GameServices.CurrentRegionId;
            GameServices.Config = null;   // the shipped numbers, whatever ran before
            _clock = new FakeClock();
            GameServices.Clock = _clock;
            GameServices.CurrentRegionId = Region;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
            GameServices.Clock = _previousClock;
            GameServices.Config = _previousConfig;
            GameServices.CurrentRegionId = _previousRegion;
        }

        private static double Spd => GameServices.SecondsPerDay;
        private static double At(int day, float hour) => day * Spd + hour * Spd / 24.0;

        private DugHolePresenter Presenter(FakeGround ground, IDugHoleSource source = null)
        {
            var go = new GameObject("DugHolePresenter (test)");
            _spawned.Add(go);
            var p = go.AddComponent<DugHolePresenter>();
            p.ConfigureGround(ground);
            p.ConfigureSource(source ?? new OutingSource());   // never the live registry
            p.OverrideSettings(DugHoleSettings.Default);
            return p;
        }

        private static SpriteRenderer[] Shown(DugHolePresenter p) => p.Pool.Where(sr => sr.enabled).ToArray();

        // ---- the player's hole -----------------------------------------------------------------------

        [Test]
        public void A_dig_on_the_dry_flat_leaves_a_dark_hole_where_the_blade_went_in()
        {
            var p = Presenter(new FakeGround());
            _clock.T = At(2, 10f);
            var blade = new Vector2(12.5f, -3.25f);
            p.OnMoment(new JuiceMomentCue(JuiceMoment.DigImpact, blade, 0.1f));

            SpriteRenderer[] shown = Shown(p);
            Assert.AreEqual(1, shown.Length, "one dig, one hole");
            Assert.AreEqual(blade.x, shown[0].transform.position.x, 1e-4f);
            Assert.AreEqual(blade.y, shown[0].transform.position.y, 1e-4f);
            Assert.AreEqual(DugHolePresenter.HoleScale(DugHoleSettings.Default), shown[0].transform.localScale);

            // Dark: the middle of the sprite is near black, darker than the clam flat's dirt.
            Texture2D tex = shown[0].sprite.texture;
            Color middle = tex.GetPixel(tex.width / 2, tex.height / 2 - 1);
            Assert.Less(middle.r + middle.g + middle.b, 0.4f, $"a dark hole, not a patch ({middle})");
            Assert.Greater(middle.a, 0.8f);
            Assert.AreEqual(0f, tex.GetPixel(0, 0).a, "round: the corners are clear");

            // Sorted as ground: above the footprints, under the sea plane and everything standing.
            var group = p.GetComponent<SortingGroup>();
            Assert.IsNotNull(group);
            Assert.Greater(group.sortingOrder, GroundPrintSettings.Default.SortingOrder, "over the footprints");
            Assert.Less(group.sortingOrder, -5, "under the sea plane (-5), so covering water hides it");
        }

        [Test]
        public void Only_the_blade_entering_the_ground_digs_a_hole()
        {
            var p = Presenter(new FakeGround());
            _clock.T = At(0, 11f);
            p.OnMoment(new JuiceMomentCue(JuiceMoment.DigStrike, Vector2.zero, 0.1f));
            p.OnMoment(new JuiceMomentCue(JuiceMoment.DigToss, Vector2.zero, 0.1f, 0.3f, Vector2.right));
            p.OnMoment(new JuiceMomentCue(JuiceMoment.Landing, Vector2.zero, 2f));
            Assert.AreEqual(0, p.Book.PlayerHoleCount, "the press, the toss and a landing dig nothing");
            p.OnMoment(new JuiceMomentCue(JuiceMoment.DigImpact, Vector2.zero, 0.1f));
            Assert.AreEqual(1, p.Book.PlayerHoleCount);
        }

        [Test]
        public void The_hole_stays_for_the_rest_of_the_day_and_is_gone_at_midnight()
        {
            var p = Presenter(new FakeGround());   // dry all day
            double dug = At(3, 9.5f);
            Assert.IsTrue(p.DigAt(new Vector2(1f, 1f), dug, Region));
            Assert.AreEqual(1, p.RefreshAt(At(3, 15f), Region));
            Assert.AreEqual(1, p.RefreshAt(At(4, 0f) - 1.0, Region), "still there a second before midnight");
            Assert.AreEqual(0, p.RefreshAt(At(4, 0f), Region), "gone with the day");
            Assert.AreEqual(0, p.RefreshAt(At(4, 9.5f), Region), "and not back the next day");
            Assert.AreEqual(At(4, 0f), DugHoleMath.EndsAt(new FakeGround(), Vector2.one, dug, Spd,
                                                          DugHoleSettings.Default.TideStepSeconds(Spd)),
                            1e-6, "its end is the next midnight");
        }

        [Test]
        public void The_hole_is_gone_once_the_tide_covers_it_and_stays_gone_when_it_falls_again()
        {
            // The flat at 0 m; the water is below it except from 14:00 to 15:00 (a flood that comes and goes).
            var ground = new FakeGround
            {
                Elevation = 0f,
                Water = t => t >= At(1, 14f) && t < At(1, 15f) ? 0.2f : -0.5f,
            };
            var p = Presenter(ground);
            Assert.IsTrue(p.DigAt(new Vector2(4f, 4f), At(1, 10f), Region));
            Assert.AreEqual(1, p.RefreshAt(At(1, 13.9f), Region), "open while the flat is bare");
            double ends = p.Book.Open[0].EndsAtSeconds;
            Assert.GreaterOrEqual(ends, At(1, 14f));
            Assert.LessOrEqual(ends, At(1, 14f) + DugHoleSettings.Default.TideStepSeconds(Spd) + 1e-6,
                               "within one tide reading of the water reaching it");
            Assert.AreEqual(0, p.RefreshAt(At(1, 14.5f), Region), "washed over");
            Assert.AreEqual(0, p.RefreshAt(At(1, 16f), Region), "the sea filled it: bare again, still gone");
        }

        [Test]
        public void A_dig_under_water_leaves_no_hole()
        {
            var p = Presenter(new FakeGround { Elevation = 0f, Water = _ => 0.3f });
            Assert.IsFalse(p.DigAt(Vector2.zero, At(0, 12f), Region));
            Assert.AreEqual(0, p.RefreshAt(At(0, 12f), Region));
        }

        [Test]
        public void Digging_the_same_spot_again_is_one_hole_and_a_load_forgets_the_players_holes()
        {
            var p = Presenter(new FakeGround());
            double t = At(0, 8f);
            p.DigAt(new Vector2(2f, 2f), t, Region);
            p.DigAt(new Vector2(2.05f, 2f), t + 5.0, Region);
            Assert.AreEqual(1, p.RefreshAt(t + 10.0, Region), "the same hole, dug deeper");
            p.DigAt(new Vector2(5f, 2f), t + 20.0, Region);
            Assert.AreEqual(2, p.RefreshAt(t + 30.0, Region));
            Assert.AreEqual(0, p.RefreshAt(t + 30.0, "region.somewhere_else"), "a hole belongs to its region");

            p.OnGameLoaded(default);
            Assert.AreEqual(0, p.RefreshAt(t + 40.0, Region), "session state: a load forgets the player's holes");
        }

        [Test]
        public void The_pool_is_built_once_and_never_grows()
        {
            var p = Presenter(new FakeGround());
            int size = p.Pool.Length;
            Assert.AreEqual(DugHoleSettings.Default.PoolSize, size);
            double t = At(0, 9f);
            for (int i = 0; i < DugHoleSettings.Default.MaxPlayerHoles * 3; i++)
                p.DigAt(new Vector2(i, 0f), t + i, Region);
            Assert.LessOrEqual(p.RefreshAt(t + 500.0, Region), size);
            Assert.AreEqual(DugHoleSettings.Default.MaxPlayerHoles, p.Book.PlayerHoleCount, "the oldest are forgotten");
            Assert.AreEqual(size, p.GetComponentsInChildren<SpriteRenderer>(true).Length, "no renderer added");
        }

        // ---- the villagers' holes --------------------------------------------------------------------

        private BeachActivityDef Activity(BeachActivityKind kind)
        {
            var def = ScriptableObject.CreateInstance<BeachActivityDef>();
            def.Kind = kind;
            _spawned.Add(def);
            return def;
        }

        private static readonly Vector2 Home = new Vector2(0f, 20f);
        private static readonly Vector2 Place = new Vector2(3f, 0f);

        /// <summary>Home until 10:00, dig at <see cref="Place"/> until 13:30, then home.</summary>
        private static RoutinePlan DigDay()
        {
            float walk = Vector2.Distance(Home, Place);
            return new RoutinePlan(
                departureHours: new[] { 0f, 10f, 13.5f },
                waypoints: new[] { Home, Home, Place, Place, Home },
                routeStart: new[] { 0, 1, 3 },
                routeCount: new[] { 1, 2, 2 },
                routeLengthMetres: new[] { 0f, walk, walk },
                activities: new[] { RoutineActivity.Home, RoutineActivity.Recreation, RoutineActivity.Home },
                standHeadingDegrees: new[] { 180f, 180f, 0f },
                exitAtMetres: new[] { 0f, 0f, 0f },
                enterAtMetres: new[] { float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity },
                walkSpeedMetresPerSecond: 1.2f);
        }

        private BeachOuting DigOuting(BeachActivityKind kind = BeachActivityKind.Dig)
            => new BeachOuting { Activity = Activity(kind), FirstStop = 1, StopCount = 1 };

        private static List<DugHole> Ask(RoutinePlan plan, BeachOuting outing, double now, uint seed = 0xC1A35EEDu)
        {
            var into = new List<DugHole>();
            double spd = Spd;
            BeachLifeDirector.AppendDigHoles(plan, outing, seed, DugHoleMath.DayStartSeconds(now, spd),
                                             (float)(spd / 24.0), now, DugHoleSettings.Default, spd, into);
            return into;
        }

        [Test]
        public void A_villager_digging_on_the_flats_leaves_holes_in_front_of_where_they_stand()
        {
            RoutinePlan plan = DigDay();
            BeachOuting outing = DigOuting();
            DugHoleSettings s = DugHoleSettings.Default;
            float sph = (float)(Spd / 24.0);
            double arrive = At(0, 10f + plan.TravelHours(1, sph));

            Assert.AreEqual(0, Ask(plan, outing, At(0, 9f)).Count, "not before they go");
            Assert.AreEqual(0, Ask(plan, outing, arrive).Count, "not the moment they arrive");

            List<DugHole> holes = Ask(plan, outing, At(0, 12f));
            Assert.Greater(holes.Count, 1, "a couple of hours on the flats: several holes");
            Assert.LessOrEqual(holes.Count, s.NpcMaxHolesPerPlace);
            foreach (DugHole h in holes)
            {
                Assert.GreaterOrEqual(h.DugAtSeconds, arrive, "dug after they reached the place");
                Assert.LessOrEqual(h.DugAtSeconds, At(0, 12f), "dug by now");
                Vector2 ground = IsoGround.ToGround(h.Position - Place);
                Assert.LessOrEqual(ground.magnitude, s.NpcHoleReachMetres + s.NpcHoleScatterMetres + 1e-3f,
                                   "at the place, within reach");
                Assert.AreNotEqual(0UL, h.Key);
            }
            Assert.Less(holes.Average(h => IsoGround.ToGround(h.Position - Place).y), 0f,
                        "in front of them: they stand facing south (180)");
            Assert.AreEqual(holes.Count, holes.Select(h => h.Key).Distinct().Count(), "one key per hole");

            List<DugHole> after = Ask(plan, outing, At(0, 18f));
            Assert.IsTrue(after.All(h => h.DugAtSeconds < At(0, 13.5f)), "no holes once they have left");
        }

        [Test]
        public void Only_a_digging_outing_makes_holes()
        {
            Assert.AreEqual(0, Ask(DigDay(), DigOuting(BeachActivityKind.Watch), At(0, 12f)).Count);
            Assert.AreEqual(0, Ask(DigDay(), DigOuting(BeachActivityKind.Stroll), At(0, 12f)).Count);
        }

        [Test]
        public void A_villagers_holes_show_until_midnight_or_the_tide_like_the_players()
        {
            var source = new OutingSource { Plan = DigDay(), Outing = DigOuting() };
            var dry = new FakeGround();
            var p = Presenter(dry, source);
            int atNoon = p.RefreshAt(At(5, 12f), Region);
            Assert.Greater(atNoon, 0, "the villager's holes are drawn");
            Assert.IsTrue(p.Book.Open.All(h => h.Villager));
            Assert.AreEqual(atNoon, p.RefreshAt(At(5, 23.9f), Region), "still there in the evening");
            Assert.AreEqual(0, p.RefreshAt(At(6, 0f) + 1.0, Region), "gone at midnight (the new day's dig is later)");

            // The flood reaches the flat at 16:00 on day 7.
            var wet = new FakeGround { Elevation = 0f, Water = t => t % Spd >= Spd * 16.0 / 24.0 ? 0.1f : -0.4f };
            var q = Presenter(wet, source);
            Assert.Greater(q.RefreshAt(At(7, 15.5f), Region), 0);
            Assert.AreEqual(0, q.RefreshAt(At(7, 16.25f), Region), "the tide washed them away");
        }

        // ---- determinism -----------------------------------------------------------------------------

        [Test]
        public void Holes_are_a_function_of_the_seed_and_the_clock_only()
        {
            RoutinePlan plan = DigDay();
            BeachOuting outing = DigOuting();
            List<DugHole> a = Ask(plan, outing, At(0, 12f));
            List<DugHole> b = Ask(DigDay(), outing, At(0, 12f));
            CollectionAssert.AreEqual(a.Select(h => (h.Position, h.DugAtSeconds, h.Key)).ToList(),
                                      b.Select(h => (h.Position, h.DugAtSeconds, h.Key)).ToList(), "same day, same holes");

            // Asked later (as after a load in the evening), the morning's holes are the very same holes.
            List<DugHole> evening = Ask(plan, outing, At(0, 20f));
            CollectionAssert.AreEqual(a.Select(h => (h.Position, h.DugAtSeconds, h.Key)).ToList(),
                                      evening.Where(h => h.DugAtSeconds <= At(0, 12f))
                                             .Select(h => (h.Position, h.DugAtSeconds, h.Key)).ToList(),
                                      "derived, not remembered: a load mid-day shows the same holes");

            List<DugHole> other = Ask(plan, outing, At(0, 12f), seed: 0x0BADF00Du);
            Assert.AreEqual(a.Count, other.Count, "another villager digs as often");
            Assert.IsFalse(a.Zip(other, (x, y) => x.Position == y.Position).All(same => same), "but somewhere else");

            var ground = new FakeGround { Elevation = 0f, Water = t => (float)Math.Sin(t * 0.001) * 0.5f - 0.2f };
            double e1 = DugHoleMath.EndsAt(ground, Vector2.one, At(0, 2f), Spd, DugHoleSettings.Default.TideStepSeconds(Spd));
            double e2 = DugHoleMath.EndsAt(ground, Vector2.one, At(0, 2f), Spd, DugHoleSettings.Default.TideStepSeconds(Spd));
            Assert.AreEqual(e1, e2, "the end of a hole is a pure function of the tide");
            Assert.LessOrEqual(e1, DugHoleMath.DayEndSeconds(At(0, 2f), Spd));
        }

        // ---- the bigger toss -------------------------------------------------------------------------

        [Test]
        public void The_toss_is_a_shovelful_thrown_twice_as_far_that_lies_a_while()
        {
            JuiceSettings bigger = JuiceSettings.Default;
            Assert.GreaterOrEqual(bigger.DigTossChunkCount, 6, "a shovelful: several clods");

            // The first toss, as it shipped before the owner's ask: five clods, all scales 1, under a second.
            JuiceSettings before = bigger;
            before.DigTossChunkCount = 5;
            before.DigTossChunkSeconds = 0.9f;
            before.DigTossScale = before.DigTossReachScale = before.DigTossUpScale = before.DigTossSpreadScale = 1f;
            before.DigTossReleaseSeconds = 0f;

            // Mid-flight of the new toss, every clod off the blade: the last leaves at the release.
            float airAt = bigger.DigTossReleaseSeconds + 0.12f;
            (float size, float reach, float air, float gap, int lying) Toss(JuiceSettings j)
            {
                var go = new GameObject("MomentBurstEmitter (test)");
                _spawned.Add(go);
                var em = go.AddComponent<MomentBurstEmitter>();
                em.OverrideSettings(j);
                em.OnMoment(new JuiceMomentCue(JuiceMoment.DigToss, Vector2.zero, 0.1f, 0.3f, Vector2.right));
                SpriteRenderer[] Live() => em.GetComponentsInChildren<SpriteRenderer>().Where(sr => sr.enabled).ToArray();
                float t = 0f;
                for (; t < airAt; t += 0.01f) em.Tick(0.01f);
                SpriteRenderer[] up = Live();
                float size = up.Average(sr => sr.bounds.size.x);
                Vector3[] p = up.Select(sr => sr.transform.position).ToArray();
                float air = (new Vector2(p.Max(v => v.x) - p.Min(v => v.x), p.Max(v => v.y) - p.Min(v => v.y))).magnitude;
                // How far each clod is from its nearest neighbour, on average: apart, or one lump.
                float gap = p.Average(v => p.Where(w => w != v).Select(w => Vector2.Distance(v, w)).DefaultIfEmpty(0f).Min());
                float reachAt = j.DigTossReleaseSeconds + 0.7f;   // every clod down, the first toss not yet faded
                for (; t < reachAt; t += 0.01f) em.Tick(0.01f);
                float reach = Live().Average(sr => sr.transform.position.x);
                for (; t < 2.0f; t += 0.01f) em.Tick(0.01f);
                return (size, reach, air, gap, Live().Length);
            }

            var b = Toss(before);
            var a = Toss(bigger);
            Assert.GreaterOrEqual(a.size / b.size, 1.15f, "bigger clods (each clod's size varies, so not exactly DigTossScale)");
            Assert.GreaterOrEqual(bigger.DigTossChunkCount * a.size * a.size, 2f * before.DigTossChunkCount * b.size * b.size,
                                  "a real shovelful: over twice the dirt");
            Assert.GreaterOrEqual(a.reach, b.reach * 1.8f, "thrown about twice as far across the ground");
            Assert.Greater(a.air, b.air * 2f, "the clods fly apart, not as one lump");
            Assert.Greater(a.air, a.size * 3f, "the shovelful strung out in the air over several clods' length");
            Assert.Greater(a.gap, a.size * 0.6f, "each clod mostly clear of its neighbour: separate clods");
            Assert.AreEqual(0, b.lying, "the first toss was gone in under a second");
            Assert.AreEqual(bigger.DigTossChunkCount, a.lying, "the spoil pile still lies there two seconds on");
        }
    }
}
