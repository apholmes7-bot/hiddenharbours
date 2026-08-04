using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Fishing;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// THE FISH-SCHOOL SIM (ADR 0025 S3) — the deterministic model behind the Core
    /// <see cref="IFishSchools"/> seam: <see cref="FishSchoolMath"/>'s lattice and
    /// <see cref="FishSchoolModel"/>'s composition of it with a region's species pool.
    ///
    /// <para><b>Determinism is the load-bearing property</b> (rule 5). Schools are recomputed from
    /// <c>(worldSeed, gameTime, place, weather, season)</c> and never saved — the tide's own discipline —
    /// so two players on one seed see the same fish and a save/load lands you back among them. Every test
    /// here runs with no scene, no clock, no terrain and no environment service, which is only possible
    /// BECAUSE the sim is a pure function of a small stated world (<see cref="IFishSchoolWorld"/>).</para>
    ///
    /// <para>The owner's honesty invariant — the marks on the glass being the same object that changes the
    /// fishing — is pinned separately in <c>FishSchoolHonestyTests</c>, including the sabotage arms that
    /// prove the check can fail.</para>
    /// </summary>
    public class FishSchoolModelTests
    {
        private const int Seed = 4242;
        private const string Region = "region.st_peters";
        private const double SecondsPerHour = 75.0;    // GameConfig.SecondsPerDay 1800 / 24

        [SetUp]
        public void SetUp() => GameServices.Reset();

        [TearDown]
        public void TearDown() => GameServices.Reset();

        // ---- fixtures ---------------------------------------------------------------------------------

        /// <summary>A world with nothing live in it: every determinism input stated as a field so a test
        /// can hold all of them still and move exactly one.</summary>
        private sealed class FakeWorld : IFishSchoolWorld
        {
            public int Seed = FishSchoolModelTests.Seed;
            public string Region = FishSchoolModelTests.Region;
            public float Column = 18f;
            public float SeaState = 0f;
            public Season Time = Season.HighSummer;

            public int WorldSeed => Seed;
            string IFishSchoolWorld.RegionId => Region;
            public float WaterColumnAt(Vector2 worldPos, double gameSeconds) => Column;
            public float SeaState01At(double gameSeconds) => SeaState;
            public Season SeasonAt(double gameSeconds) => Time;
        }

        private static FishSchoolSettings Settings(float chance = 1f)
        {
            FishSchoolSettings s = FishSchoolSettings.Default;
            s.BaseAppearanceChance01 = chance;
            return FishSchoolMath.Sanitized(in s);
        }

        private static FishSpeciesDef Fish(string id, SeasonMask seasons = SeasonMask.AllYear,
                                           FishDepthBand bands = FishDepthBand.None,
                                           string region = Region)
        {
            var f = ScriptableObject.CreateInstance<FishSpeciesDef>();
            f.Id = id;
            f.RegionIds = new[] { region };
            f.AllowedGear = Gear.Handline | Gear.Jig;
            f.Seasons = seasons;
            f.DepthBands = bands;
            f.SpawnWeight = 1f;
            f.MinWeightKg = 1f; f.MaxWeightKg = 2f;
            return f;
        }

        private static FishSchoolModel Model(FakeWorld world, in FishSchoolSettings s,
                                             params FishSpeciesDef[] pool)
            => new FishSchoolModel(world, pool, in s, DepthDropSettings.Default, SecondsPerHour);

        /// <summary>Where and when cell (0,0)'s school is up, computed from the PUBLIC maths rather than
        /// asked of the model — so a test knows the answer independently of the thing it is judging.</summary>
        private static void SchoolAt00(in FishSchoolSettings s, long slot, float column, float seaState,
                                       out Vector2 centre, out double midWindow, out float depth)
        {
            uint key = FishSchoolMath.CellSlotKey(Seed, Region, 0, 0, slot);
            centre = FishSchoolMath.CentreFor(key, 0, 0, s.CellSizeMetres);
            depth = FishSchoolMath.DepthFor(key, column, seaState, in s);
            FishSchoolMath.WindowFor(key, slot, SecondsPerHour, in s, out double a, out double b);
            midWindow = (a + b) * 0.5;
        }

        /// <summary>Settings under which EVERY school in the world sits at the same depth — so a species
        /// test can reason about one depth band rather than nine. (The band is what the species filter
        /// reads, and neighbouring schools legitimately sit at different depths.)</summary>
        private static FishSchoolSettings FlatDepthSettings(float fraction01)
        {
            FishSchoolSettings s = FishSchoolSettings.Default;
            s.BaseAppearanceChance01 = 1f;
            s.MinDepthFraction01 = fraction01;
            s.MaxDepthFraction01 = fraction01;
            s.SeaStateDepthBias01 = 0f;
            return FishSchoolMath.Sanitized(in s);
        }

        private static FishDepthBand BandOf(float depthM)
        {
            DepthDropSettings d = DepthDropSettings.Default;
            return DepthDropMath.ZoneForDepth(depthM, d.TidepoolMaxMeters, d.ShallowsMaxMeters,
                                              d.InshoreMaxMeters, d.MidwaterMaxMeters, d.DeepMaxMeters);
        }

        private static void AssertSameSchools(List<FishSchool> a, List<FishSchool> b, string because)
        {
            Assert.AreEqual(a.Count, b.Count, because);
            for (int i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(a[i].Centre.x, b[i].Centre.x, 0f, because);
                Assert.AreEqual(a[i].Centre.y, b[i].Centre.y, 0f, because);
                Assert.AreEqual(a[i].RadiusMetres, b[i].RadiusMetres, 0f, because);
                Assert.AreEqual(a[i].DepthMetres, b[i].DepthMetres, 0f, because);
                Assert.AreEqual(a[i].MarkCount, b[i].MarkCount, because);
                Assert.AreEqual(a[i].StartSeconds, b[i].StartSeconds, 0d, because);
                Assert.AreEqual(a[i].EndSeconds, b[i].EndSeconds, 0d, because);
                CollectionAssert.AreEqual(a[i].SpeciesIds, b[i].SpeciesIds, because);
            }
        }

        // ---- determinism (rule 5) -----------------------------------------------------------------------

        [Test]
        public void SameSeedTimePlaceWeatherAndDate_GiveIdenticalSchools()
        {
            FishSchoolSettings s = Settings();
            SchoolAt00(in s, 30, 18f, 0f, out Vector2 centre, out double t, out _);

            // Two independently constructed models — nothing shared but the stated world.
            var a = new List<FishSchool>();
            var b = new List<FishSchool>();
            Model(new FakeWorld(), in s, Fish("fish.atlantic_cod"), Fish("fish.mackerel"))
                .SchoolsAt(centre, t, a);
            Model(new FakeWorld(), in s, Fish("fish.atlantic_cod"), Fish("fish.mackerel"))
                .SchoolsAt(centre, t, b);

            Assert.GreaterOrEqual(a.Count, 1, "the fixture must actually be over fish for this to mean anything");
            AssertSameSchools(a, b, "two players on one seed, same water, same instant — the same fish");
        }

        [Test]
        public void ARepeatedQuery_IsUnchangedByEverythingAskedInBetween()
        {
            FishSchoolSettings s = Settings();
            SchoolAt00(in s, 30, 18f, 0f, out Vector2 centre, out double t, out _);
            FishSchoolModel model = Model(new FakeWorld(), in s, Fish("fish.atlantic_cod"));

            var first = new List<FishSchool>();
            model.SchoolsAt(centre, t, first);

            // Wander the sim: other places, other times, the other read. If any of it left state behind,
            // the sim is not a pure function and "recomputed, never saved" is a lie.
            var scratch = new List<FishSchool>();
            var marks = new List<FishMark>();
            for (int i = 0; i < 40; i++)
            {
                model.SchoolsAt(centre + new Vector2(i * 37f, i * -19f), t + i * 211.0, scratch);
                model.MarksAt(centre + new Vector2(i * -13f, i * 41f), t - i * 97.0, marks);
            }

            var again = new List<FishSchool>();
            model.SchoolsAt(centre, t, again);
            AssertSameSchools(first, again, "the sim carries no state between queries");
        }

        [Test]
        public void ADifferentWorldSeed_MovesTheFish()
        {
            FishSchoolSettings s = Settings();
            SchoolAt00(in s, 30, 18f, 0f, out Vector2 centre, out double t, out _);

            var a = new List<FishSchool>();
            var b = new List<FishSchool>();
            Model(new FakeWorld(), in s, Fish("fish.atlantic_cod")).SchoolsAt(centre, t, a);
            Model(new FakeWorld { Seed = Seed + 1 }, in s, Fish("fish.atlantic_cod")).SchoolsAt(centre, t, b);

            Assert.IsFalse(Same(a, b), "a different world seed must be a different sea");
        }

        [Test]
        public void ADifferentRegion_MovesTheFish()
        {
            // The region id is folded into every key so two regions never mirror each other's shoals just
            // because their world rectangles overlap in coordinates.
            FishSchoolSettings s = Settings();
            SchoolAt00(in s, 30, 18f, 0f, out Vector2 centre, out double t, out _);

            var a = new List<FishSchool>();
            var b = new List<FishSchool>();
            Model(new FakeWorld(), in s, Fish("fish.atlantic_cod")).SchoolsAt(centre, t, a);
            Model(new FakeWorld { Region = "region.nine_mile_creek" }, in s,
                  Fish("fish.atlantic_cod", region: "region.nine_mile_creek")).SchoolsAt(centre, t, b);

            Assert.IsFalse(Same(a, b), "two regions must not share a shoal map");
        }

        private static bool Same(List<FishSchool> a, List<FishSchool> b)
        {
            if (a.Count != b.Count) return false;
            for (int i = 0; i < a.Count; i++)
                if (a[i].Centre != b[i].Centre || !Mathf.Approximately(a[i].RadiusMetres, b[i].RadiusMetres)
                    || a[i].MarkCount != b[i].MarkCount) return false;
            return true;
        }

        // ---- the owner's three gates: location, weather, date -------------------------------------------

        [Test]
        public void TheLocationGate_BarsWaterTooShallowToHoldFish()
        {
            FishSchoolSettings s = Settings();
            Assert.Greater(s.MinWaterColumnMetres, 0f, "the default must actually bar something");

            // A bared flat as the tide falls: no fish, anywhere, ever — a hard bar, not a rarity.
            FishSchoolModel dry = Model(new FakeWorld { Column = s.MinWaterColumnMetres * 0.5f }, in s,
                                        Fish("fish.atlantic_cod"));
            var found = new List<FishSchool>();
            int total = 0;
            for (int i = 0; i < 40; i++)
                total += dry.SchoolsAt(new Vector2(i * 53f, i * 31f), 100.0 + i * 400.0, found);
            Assert.AreEqual(0, total, "fish are not on the beach");

            // The same water, deep enough: fish.
            FishSchoolModel wet = Model(new FakeWorld { Column = s.MinWaterColumnMetres + 5f }, in s,
                                        Fish("fish.atlantic_cod"));
            total = 0;
            for (int i = 0; i < 40; i++)
                total += wet.SchoolsAt(new Vector2(i * 53f, i * 31f), 100.0 + i * 400.0, found);
            Assert.Greater(total, 0, "and deeper water does hold them");
        }

        [Test]
        public void TheWeatherGate_ShowsMoreFishInBrokenWater()
        {
            // The gate itself, counted over a block of cells — deterministic, so these are fixed numbers,
            // not a sample. Direction matches SeaFishingSettings.SeaBoldness01: broken water emboldens fish.
            FishSchoolSettings s = Settings(chance: 0.5f);
            Assert.Greater(s.SeaStateAppearanceBias, 0f, "the shipped default leans this way");

            int calm = PresentCells(in s, seaState: 0f, Season.HighSummer);
            int blow = PresentCells(in s, seaState: 1f, Season.HighSummer);
            Assert.Greater(blow, calm, "a blow must show MORE fish, not fewer");

            // ...and turning the dial off makes the weather stop mattering.
            FishSchoolSettings blind = s;
            blind.SeaStateAppearanceBias = 0f;
            Assert.AreEqual(PresentCells(in blind, 0f, Season.HighSummer),
                            PresentCells(in blind, 1f, Season.HighSummer),
                            "bias 0 = weather-blind schools (the A/B baseline)");
        }

        [Test]
        public void TheDateGate_MakesHardWinterLeanerThanHighSummer()
        {
            FishSchoolSettings s = Settings(chance: 0.5f);
            Assert.Less(s.HardWinterAppearance, s.HighSummerAppearance, "the shipped default leans this way");

            Assert.Less(PresentCells(in s, 0f, Season.HardWinter),
                        PresentCells(in s, 0f, Season.HighSummer),
                        "the winter sea is meant to be hard fishing");
        }

        /// <summary>How many of a 20×20 block of cells hold a school in slot 0, under these settings.</summary>
        private static int PresentCells(in FishSchoolSettings s, float seaState, Season season)
        {
            float chance = FishSchoolMath.AppearanceChance01(18f, seaState, season, in s);
            int n = 0;
            for (int y = 0; y < 20; y++)
            for (int x = 0; x < 20; x++)
                if (FishSchoolMath.IsPresent(FishSchoolMath.CellSlotKey(Seed, Region, x, y, 0), chance)) n++;
            return n;
        }

        [Test]
        public void TheOffSwitch_IsAnHonestEmptySea()
        {
            FishSchoolSettings s = Settings(chance: 0f);
            FishSchoolModel model = Model(new FakeWorld(), in s, Fish("fish.atlantic_cod"));

            var schools = new List<FishSchool>();
            var marks = new List<FishMark>();
            int total = 0;
            for (int i = 0; i < 40; i++)
            {
                total += model.SchoolsAt(new Vector2(i * 53f, i * 31f), 100.0 + i * 400.0, schools);
                total += model.MarksAt(new Vector2(i * 53f, i * 31f), 100.0 + i * 400.0, marks);
            }
            Assert.AreEqual(0, total, "BaseAppearanceChance01 = 0 is the owner's A/B off switch");
        }

        [Test]
        public void WeatherPushesTheSchoolDeeper_WhichIsHowItChangesWhichFishYouFind()
        {
            FishSchoolSettings s = Settings();
            uint key = FishSchoolMath.CellSlotKey(Seed, Region, 3, 5, 11);

            float calm = FishSchoolMath.DepthFor(key, 40f, 0f, in s);
            float blow = FishSchoolMath.DepthFor(key, 40f, 1f, in s);
            Assert.Greater(blow, calm, "fish go down when it blows");
            Assert.LessOrEqual(blow, 40f, "and never below the bottom");

            FishSchoolSettings blind = s;
            blind.SeaStateDepthBias01 = 0f;
            Assert.AreEqual(FishSchoolMath.DepthFor(key, 40f, 0f, in blind),
                            FishSchoolMath.DepthFor(key, 40f, 1f, in blind), 1e-5f,
                            "bias 0 = weather-blind depth");
        }

        // ---- the two search invariants the O(9) read rests on --------------------------------------------

        [Test]
        public void ASchoolNeverSpillsMoreThanOneCell()
        {
            // The invariant is ENFORCED, not trusted: the owner can type 999 into the radius and the sim
            // still only has to look at the 3×3 neighbourhood.
            FishSchoolSettings wild = FishSchoolSettings.Default;
            wild.CellSizeMetres = 80f;
            wild.MinRadiusMetres = 400f;
            wild.MaxRadiusMetres = 999f;
            FishSchoolSettings s = FishSchoolMath.Sanitized(in wild);

            Assert.LessOrEqual(s.MaxRadiusMetres, s.CellSizeMetres);
            Assert.LessOrEqual(s.MinRadiusMetres, s.MaxRadiusMetres);
            for (int i = 0; i < 200; i++)
                Assert.LessOrEqual(FishSchoolMath.RadiusFor(FishSchoolMath.CellSlotKey(Seed, Region, i, -i, i),
                                                            in s),
                                   s.CellSizeMetres);
        }

        [Test]
        public void AWindowNeverLeavesItsSlot()
        {
            // The other half of the same story: one slot to search, so a query is O(9) rather than O(the sea).
            FishSchoolSettings wild = FishSchoolSettings.Default;
            wild.SlotHours = 1.5f;
            wild.MinWindowHours = 9f;      // nonsense on purpose
            wild.MaxWindowHours = 40f;
            FishSchoolSettings s = FishSchoolMath.Sanitized(in wild);
            double period = FishSchoolMath.SlotSeconds(in s, SecondsPerHour);

            for (long slot = 0; slot < 200; slot++)
            {
                uint key = FishSchoolMath.CellSlotKey(Seed, Region, 2, -3, slot);
                FishSchoolMath.WindowFor(key, slot, SecondsPerHour, in s, out double a, out double b);
                double start = FishSchoolMath.SlotStartSeconds(slot, period);
                Assert.GreaterOrEqual(a, start - 1e-6, $"slot {slot} window starts before its slot");
                Assert.LessOrEqual(b, start + period + 1e-6, $"slot {slot} window outlives its slot");
                Assert.Greater(b, a, "and is never empty");
            }
        }

        [Test]
        public void CellIndex_IsContinuousThroughTheOrigin()
        {
            // Truncation instead of floor would make the two cells either side of 0 one double-wide cell —
            // a seam down the middle of every region authored around 0,0.
            Assert.AreEqual(-1, FishSchoolMath.CellIndex(-1f, 100f));
            Assert.AreEqual(-1, FishSchoolMath.CellIndex(-100f, 100f));
            Assert.AreEqual(-2, FishSchoolMath.CellIndex(-101f, 100f));
            Assert.AreEqual(0, FishSchoolMath.CellIndex(0f, 100f));
            Assert.AreEqual(0, FishSchoolMath.CellIndex(99f, 100f));
            Assert.AreEqual(1, FishSchoolMath.CellIndex(100f, 100f));
        }

        // ---- density → bite rate -------------------------------------------------------------------------

        [Test]
        public void DensityIsTheBiteRate_OneNumber_Capped()
        {
            FishSchoolSettings s = Settings();

            Assert.AreEqual(1f, FishSchoolMath.BiteRateMultiplier(0f, in s), 1e-6f,
                "no fish under you = fish exactly as before");
            Assert.AreEqual(1f + s.BiteRatePerMark, FishSchoolMath.BiteRateMultiplier(1f, in s), 1e-5f,
                "the owner's 'one fish, lower bite rate'");
            Assert.AreEqual(1f + 5f * s.BiteRatePerMark, FishSchoolMath.BiteRateMultiplier(5f, in s), 1e-5f,
                "'several fish, higher'");
            Assert.Greater(FishSchoolMath.BiteRateMultiplier(5f, in s),
                           FishSchoolMath.BiteRateMultiplier(1f, in s),
                           "monotonic in density — the whole point of the mark count");
            Assert.AreEqual(s.MaxBiteRateMultiplier, FishSchoolMath.BiteRateMultiplier(10000f, in s), 1e-4f,
                "capped, so a freak school never makes bites instant");
            Assert.AreEqual(1f, FishSchoolMath.BiteRateMultiplier(-5f, in s), 1e-6f,
                "and never below 1 — being over fish can't be a punishment");
        }

        [Test]
        public void HoldingAtTheDepthShown_ReusesTheDepthDropBands()
        {
            FishSchoolSettings s = Settings();
            DepthDropSettings dd = DepthDropSettings.Default;

            // Two depths in the SAME band (whatever the owner's thresholds are) — full value.
            float schoolDepth = (dd.InshoreMaxMeters + dd.ShallowsMaxMeters) * 0.5f;
            Assert.AreEqual(FishDepthBand.Inshore,
                            DepthDropMath.ZoneForDepth(schoolDepth, dd.TidepoolMaxMeters, dd.ShallowsMaxMeters,
                                                       dd.InshoreMaxMeters, dd.MidwaterMaxMeters,
                                                       dd.DeepMaxMeters),
                            "fixture sanity");
            Assert.AreEqual(1f, FishSchoolMath.DepthMatch01(schoolDepth, schoolDepth, in dd, in s), 1e-6f);

            // A different band — damped, never zero.
            float wrong = dd.MidwaterMaxMeters + (dd.DeepMaxMeters - dd.MidwaterMaxMeters) * 0.5f;
            Assert.AreEqual(s.OffDepthMatch01, FishSchoolMath.DepthMatch01(wrong, schoolDepth, in dd, in s),
                            1e-6f);
            Assert.Greater(s.OffDepthMatch01, 0f, "fishing a school badly is still fishing a school");

            // No depth game at all (the bobber/legacy branch) — not judged on depth.
            Assert.AreEqual(s.DepthlessMatch01,
                            FishSchoolMath.DepthMatch01(CatchContext.NoDepth, schoolDepth, in dd, in s), 1e-6f);
            Assert.AreEqual(1f, s.DepthlessMatch01, 1e-6f,
                "shipped default: the old way of fishing is never made worse by this feature");
        }

        // ---- which fish are down there ------------------------------------------------------------------

        [Test]
        public void SpeciesAreFilteredByRegionSeasonAndDepthBand()
        {
            // Flat depth so every school in the world shares one band — the filter under test reads the
            // band, and neighbouring schools legitimately sit at different depths.
            FishSchoolSettings s = FlatDepthSettings(0.4f);
            var world = new FakeWorld();
            SchoolAt00(in s, 30, world.Column, world.SeaState, out Vector2 centre, out double t,
                       out float depth);
            FishDepthBand band = BandOf(depth);

            FishSchoolModel model = Model(world, in s,
                Fish("fish.right_here", bands: band),                                   // fits
                Fish("fish.wrong_season", SeasonMask.HardWinter, band),                 // wrong date
                Fish("fish.wrong_region", region: "region.somewhere_else"),             // wrong place
                Fish("fish.wrong_band", bands: OtherBand(band)));                       // wrong depth

            var schools = new List<FishSchool>();
            Assert.GreaterOrEqual(model.SchoolsAt(centre, t, schools), 1);
            foreach (FishSchool school in schools)
            {
                Assert.IsNotEmpty(school.SpeciesIds);
                foreach (string id in school.SpeciesIds)
                    Assert.AreEqual("fish.right_here", id,
                        "location, weather and date decide WHICH species are found there");
            }
        }

        private static FishDepthBand OtherBand(FishDepthBand band)
            => band == FishDepthBand.Tidepool ? FishDepthBand.Abyssal : FishDepthBand.Tidepool;

        [Test]
        public void WhenNothingLivesAtThatDepth_TheBandFilterRelaxesRatherThanStarving()
        {
            FishSchoolSettings s = FlatDepthSettings(0.4f);
            var world = new FakeWorld();
            SchoolAt00(in s, 30, world.Column, world.SeaState, out Vector2 centre, out double t,
                       out float depth);

            // A region that only authors fish for a band this school isn't in: it holds THEM rather than
            // silently stating nothing.
            FishSchoolModel model = Model(world, in s, Fish("fish.only_one", bands: OtherBand(BandOf(depth))));
            var schools = new List<FishSchool>();
            Assert.GreaterOrEqual(model.SchoolsAt(centre, t, schools), 1);
            Assert.AreEqual(1, schools[0].SpeciesIds.Count);
            Assert.AreEqual("fish.only_one", schools[0].SpeciesIds[0]);
        }

        [Test]
        public void AnEmptyRegionPool_LeavesTheSpeciesUnstated_WhichTheSeamDefinesAsFallBack()
        {
            FishSchoolSettings s = Settings();
            var world = new FakeWorld();
            SchoolAt00(in s, 30, world.Column, world.SeaState, out Vector2 centre, out double t, out _);

            FishSchoolModel model = Model(world, in s);   // no species authored here yet
            var schools = new List<FishSchool>();
            Assert.GreaterOrEqual(model.SchoolsAt(centre, t, schools), 1,
                "the fish are still there — we just can't say what they are");
            Assert.IsEmpty(schools[0].SpeciesIds);

            // Which the seam defines as "no species stated": the bite rate still lifts, the roll falls back.
            var species = new List<string>();
            var scratch = new List<FishSchool>();
            SchoolInfluence bite = SchoolInfluence.At(model, centre, t, CatchContext.NoDepth,
                                                     DepthDropSettings.Default, in s, scratch, species);
            Assert.IsTrue(bite.OnFish);
            Assert.Greater(bite.BiteRateMultiplier, 1f);
            Assert.AreEqual(1f, bite.AffinityFor("fish.anything"), 0f, "an unstated school weights nothing");
        }

        [Test]
        public void TheSpeciesPickIsKeyedByTheStableId_NotByPoolOrder()
        {
            // Otherwise "a content author adds a fish to St Peters" would silently re-pick the species of
            // every school in the region, in every existing save.
            FishSchoolSettings s = Settings();
            var world = new FakeWorld();
            SchoolAt00(in s, 30, world.Column, world.SeaState, out Vector2 centre, out double t, out _);

            var a = new List<FishSchool>();
            var b = new List<FishSchool>();
            Model(world, in s, Fish("fish.a"), Fish("fish.b"), Fish("fish.c")).SchoolsAt(centre, t, a);
            Model(world, in s, Fish("fish.c"), Fish("fish.a"), Fish("fish.b")).SchoolsAt(centre, t, b);

            Assert.GreaterOrEqual(a.Count, 1);
            CollectionAssert.AreEquivalent(a[0].SpeciesIds, b[0].SpeciesIds,
                "re-ordering the region pool must not move a single fish");
        }

        // ---- the seam's own contract ----------------------------------------------------------------------

        [Test]
        public void BothReads_ClearTheCallersListAndToleratesNull()
        {
            FishSchoolSettings s = Settings();
            FishSchoolModel model = Model(new FakeWorld(), in s, Fish("fish.atlantic_cod"));

            var schools = new List<FishSchool> { default };
            var marks = new List<FishMark> { new FishMark(1f, 9, 1f) };

            // Somewhere with nothing showing: stale contents must not survive to be re-drawn.
            model.SchoolsAt(new Vector2(0f, 0f), -50000.0, schools);
            model.MarksAt(new Vector2(0f, 0f), -50000.0, marks);
            Assert.IsEmpty(schools);
            Assert.IsEmpty(marks);

            Assert.DoesNotThrow(() => model.SchoolsAt(Vector2.zero, 100.0, null));
            Assert.DoesNotThrow(() => model.MarksAt(Vector2.zero, 100.0, null));
        }

        // ---- the shipped asset (rule 6) ---------------------------------------------------------------------

        [Test]
        public void TheShippedConfigAsset_ActuallyCarriesTheSchoolBlock_AndItsInvariantsHold()
        {
            // ⚠ The exact failure this guards: GameConfig.asset LAGS GameConfig.cs. A block the code has
            // and the asset doesn't deserializes to ALL ZEROES on the owner's config — which here is not
            // "slightly off tuning" but a 0 m cell grid and a 0 h slot, i.e. a sim that cannot run. This is
            // deliberately NOT an equality test: the whole point of the block is that the owner tunes it.
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(
                "Assets/_Project/Data/Config/GameConfig.asset");
            Assert.IsNotNull(config, "the shared GameConfig asset must exist");

            FishSchoolSettings a = config.FishSchools;
            Assert.Greater(a.CellSizeMetres, 0f,
                "GameConfig.asset has no FishSchools block — add it in the same PR as the code default");
            Assert.Greater(a.SlotHours, 0f);
            Assert.Greater(a.OpenWaterColumnMetres, 0f);

            // The owner's own guard-rails: the search invariants and the never-a-wall promise.
            Assert.LessOrEqual(a.MaxRadiusMetres, a.CellSizeMetres, "a school must not spill past one cell");
            Assert.LessOrEqual(a.MaxWindowHours, a.SlotHours, "a window must not outlive its slot");
            Assert.LessOrEqual(a.MinWindowHours, a.MaxWindowHours);
            Assert.LessOrEqual(a.MinRadiusMetres, a.MaxRadiusMetres);
            Assert.LessOrEqual(a.MinDepthFraction01, a.MaxDepthFraction01);
            Assert.GreaterOrEqual(a.MinMarks, 1);
            Assert.GreaterOrEqual(a.MaxMarks, a.MinMarks);
            Assert.GreaterOrEqual(a.MaxBiteRateMultiplier, 1f);
            Assert.Greater(a.OffDepthMatch01, 0f, "the wrong depth is worth less, never nothing");
            Assert.Greater(a.OffSchoolSpeciesDamp01, 0f, "the wrong species is unlikely, never barred");
            Assert.GreaterOrEqual(a.SchoolSpeciesBoost, 1f);
        }

        // ---- rule 5: nothing about a school reaches the save ------------------------------------------------

        [Test]
        public void NothingAboutASchoolReachesTheSave()
        {
            // The way SaveMigrationV8Tests states it for the sounding: schools are recomputed from
            // (worldSeed, gameTime) like the tide, so a stored one would be stale the moment the day turned
            // — and would also be a second record for the finder and the fishing to disagree over.
            string[] banned = { "FishSchool", "FishMark", "SchoolInfluence", "FishSchoolSettings" };

            foreach (System.Type t in typeof(SaveData).Assembly.GetTypes())
            {
                if (t.Name != "SaveData" && !t.Name.EndsWith("Dto")) continue;
                foreach (FieldInfo f in t.GetFields(BindingFlags.Public | BindingFlags.Instance))
                {
                    Assert.That(f.Name, Does.Not.Contain("School"),
                        $"{t.Name}.{f.Name} — fish schools are recomputed, never stored (rule 5).");
                    foreach (string b in banned)
                        Assert.AreNotEqual(b, f.FieldType.Name,
                            $"{t.Name}.{f.Name} is a {b} — that type must never be serialized into a save.");
                }
            }
        }
    }
}
