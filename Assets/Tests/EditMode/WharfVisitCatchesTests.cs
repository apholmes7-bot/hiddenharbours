using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Text.RegularExpressions;
using HiddenHarbours.Core;
using HiddenHarbours.Economy;
using HiddenHarbours.Fishing;
using HiddenHarbours.Tests.Support;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.EditMode
{
    public sealed class WharfVisitCatchesTests
    {
        private const string Spot = "station.st_peters.wharf_head";
        private const string Region = "region.st_peters";
        private readonly List<Object> _owned = new List<Object>();
        private WharfVisitDef _visit;
        private FishSpeciesDef _fish;
        private Water _water;
        private WharfVisitRod _rod;

        internal sealed class Water : IFishSchoolWorld
        {
            public int Seed = 4242;
            public float Hour = 7f, Tide, Rate = 1f, Sea, Column = 8f;
            public Season Season = Season.HighSummer;
            public double SecondsPerHour = 75;
            public bool AdvanceHour;
            public int WorldSeed => Seed;
            public string RegionId => Region;
            public float WaterColumnAt(Vector2 pos, double time) => Column;
            public float SeaState01At(double time) => Sea;
            public Season SeasonAt(double time) => Season;
            public float HourOfDayAt(double time) => AdvanceHour ? (float)((time / SecondsPerHour) % 24) : Hour;
            public float TideRateMetresPerHourAt(double time) => Rate;
            public float TideAt(double time) => Tide;
        }

        private T Own<T>() where T : ScriptableObject
        {
            T value = ScriptableObject.CreateInstance<T>();
            _owned.Add(value);
            return value;
        }

        [SetUp]
        public void SetUp()
        {
            _visit = Own<WharfVisitDef>();
            _fish = Own<FishSpeciesDef>();
            _fish.Id = "fish.fixture";
            _fish.RegionIds = new[] { Region };
            _fish.AllowedGear = Gear.Handline;
            _water = new Water();
            _rod = new WharfVisitRod("gear.rod", 1f, 0f);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object value in _owned) Object.DestroyImmediate(value);
            _owned.Clear();
        }

        private WharfVisitCatches Model(IReadOnlyList<FishSpeciesDef> pool = null, Water water = null,
            bool schoolsOn = false)
        {
            Water source = water ?? _water;
            FishSchoolSettings schools = FishSchoolSettings.Default;
            if (!schoolsOn) schools.BaseAppearanceChance01 = 0f;
            return new WharfVisitCatches(_visit, pool ?? new[] { _fish }, source, source.TideAt,
                Spot, new Vector2(215f, -103f), DepthDropSettings.Default, BaitTackleSettings.Default,
                SeaFishingSettings.Default, schools, source.SecondsPerHour);
        }

        private List<WharfVisitCast> Build(WharfVisitCatches model, double time = 525)
            => model.Build(_water.Seed, time, Spot, _rod, "bait.capelin", null);

        [Test]
        public void RepeatedCallsAndIndependentPeerHaveIdenticalSnapshots()
        {
            var first = Model(schoolsOn: true);
            var peer = Model(water: new Water(), schoolsOn: true);
            var expected = Build(first);
            Build(first, 1800);
            Assert.That(Build(first), Is.EqualTo(expected));
            Assert.That(Build(peer), Is.EqualTo(expected));
            Assert.That(expected.Count, Is.EqualTo(_visit.CastsPerVisit));
        }

        [Test]
        public void FirstCastHasFixedCrossRuntimeGoldenValues()
        {
            var cast = Build(Model())[0];
            Assert.That(cast.WaitSeconds, Is.EqualTo(23.019927978515625f));
            Assert.That(cast.WeightKg, Is.EqualTo(3.37161922454834f));
            Assert.That(cast.FightSeconds, Is.EqualTo(8f));
            Assert.That(cast.Strength, Is.EqualTo(WharfFishStrength.Steady));
            Assert.That(cast.Landed, Is.True);
        }

        [Test]
        public void DifferentSeedsTimesSpotsAndKitIdsChangeTheDraws()
        {
            var model = Model();
            var first = Build(model);
            _water.Seed++;
            Assert.That(Build(model), Is.Not.EqualTo(first));
            _water.Seed--;
            Assert.That(Build(model, 525.25), Is.Not.EqualTo(first));
            var otherRod = new WharfVisitRod("gear.other_rod", 1f, 0f);
            Assert.That(model.Build(4242, 525, Spot, otherRod, "bait.capelin", null), Is.Not.EqualTo(first));
            Assert.That(model.Build(4242, 525, Spot, _rod, "bait.herring", null), Is.Not.EqualTo(first));
            var otherSpot = new WharfVisitCatches(_visit, new[] { _fish }, _water, _water.TideAt,
                "station.other_wharf", Vector2.zero, DepthDropSettings.Default, BaitTackleSettings.Default,
                SeaFishingSettings.Default, FishSchoolSettings.Default, 75);
            Assert.That(otherSpot.Build(4242, 525, "station.other_wharf", _rod, "bait.capelin", null),
                Is.Not.EqualTo(first));
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
        public void IneligibleFishAndBareHooksStayBlank(int gate)
        {
            string baitId = "bait.capelin";
            if (gate == 0) { _fish.StartHour = 8; _fish.EndHour = 10; }
            if (gate == 1) _fish.Seasons = SeasonMask.HardWinter;
            if (gate == 2) _fish.MinTide = 2;
            if (gate == 3) { _fish.MovingWaterOnly = true; _water.Rate = 0; }
            if (gate == 4) _fish.AllowedGear = Gear.Trap;
            if (gate == 5) baitId = null;
            var casts = Model().Build(4242, 525, Spot, _rod, baitId, null);
            Assert.That(casts.Count, Is.EqualTo(3));
            foreach (WharfVisitCast cast in casts)
            {
                Assert.That(cast.HasBite, Is.False);
                Assert.That(cast.SpeciesId, Is.Null);
                Assert.That(cast.WeightKg, Is.Zero);
                Assert.That(cast.FightSeconds, Is.Zero);
                Assert.That(cast.Strength, Is.EqualTo(WharfFishStrength.None));
                Assert.That(cast.Landed, Is.False);
                Assert.That(cast.WaitSeconds, Is.InRange(8f, 24f));
            }
        }

        [Test]
        public void EmptyPoolAndDryWaterCannotProduceFish()
        {
            Assert.That(Build(Model(Array.Empty<FishSpeciesDef>())).TrueForAll(c => !c.HasBite), Is.True);
            _water.Column = 0;
            Assert.That(Build(Model()).TrueForAll(c => !c.HasBite), Is.True);
        }

        [Test]
        public void RechecksTheHourAtTheBiteAndSequencesEveryCast()
        {
            _water.AdvanceHour = true;
            _visit.MinBiteWaitSeconds = _visit.MaxBiteWaitSeconds = 10;
            _fish.StartHour = 6; _fish.EndHour = 8;
            var casts = Build(Model(), 599);
            Assert.That(casts[0].CastAt, Is.EqualTo(599));
            foreach (WharfVisitCast cast in casts) Assert.That(cast.HasBite, Is.False);
            for (int i = 1; i < casts.Count; i++)
                Assert.That(casts[i].CastAt, Is.EqualTo(casts[i - 1].FinishedAt + _visit.BetweenCastsSeconds));
        }

        [TestCase(0f, WharfFishStrength.Light)]
        [TestCase(0.5f, WharfFishStrength.Steady)]
        [TestCase(1f, WharfFishStrength.Strong)]
        public void SpeciesPersonalitySelectsTheAuthoredRiskAndFight(float strength, WharfFishStrength band)
        {
            _fish.RodFight = Own<RodFightDef>();
            _fish.RodFight.Strength = strength;
            _visit.LightLossChance = _visit.SteadyLossChance = _visit.StrongLossChance = 1;
            foreach (WharfVisitCast cast in Build(Model()))
            {
                Assert.That(cast.Strength, Is.EqualTo(band));
                Assert.That(cast.FightSeconds, Is.EqualTo(Mathf.Lerp(4, 12, strength)));
                Assert.That(cast.Landed, Is.False);
            }
            _visit.LightLossChance = _visit.SteadyLossChance = _visit.StrongLossChance = 0;
            Assert.That(Build(Model()).TrueForAll(c => c.Landed), Is.True);
        }

        [TestCase(0f, 12)] [TestCase(0.5f, 35)] [TestCase(1f, 83)]
        public void EachStrengthBandUsesItsOwnLossChance(float strength, int expectedLost)
        {
            _fish.RodFight = Own<RodFightDef>();
            _fish.RodFight.Strength = strength;
            var model = Model();
            int lost = 0;
            for (int seed = 0; seed < 256; seed++)
            {
                _water.Seed = seed;
                if (!Build(model)[0].Landed) lost++;
            }
            Assert.That(lost, Is.EqualTo(expectedLost));
        }

        [Test]
        public void RodNumbersAffectFightAndLossWithoutChangingFirstFish()
        {
            _visit.LightLossChance = _visit.SteadyLossChance = _visit.StrongLossChance = 1;
            var model = Model();
            WharfVisitCast first = Build(model)[0];
            var helped = new WharfVisitRod(_rod.Id, 2f, 1f);
            WharfVisitCast second = model.Build(4242, 525, Spot, helped, "bait.capelin", null)[0];
            Assert.That(second.SpeciesId, Is.EqualTo(first.SpeciesId));
            Assert.That(second.WeightKg, Is.EqualTo(first.WeightKg));
            Assert.That(second.FightSeconds, Is.EqualTo(first.FightSeconds / 2));
            Assert.That(first.Landed, Is.False);
            Assert.That(second.Landed, Is.True);
        }

        [Test]
        public void WeatherUsesThePlayersWaitAndSizeBias()
        {
            var model = Model();
            WharfVisitCast calm = Build(model)[0];
            _water.Sea = 1f;
            WharfVisitCast storm = Build(model)[0];
            Assert.That(storm.WaitSeconds, Is.EqualTo(calm.WaitSeconds *
                SeaFightMath.BiteDelayScale(1, SeaFishingSettings.Default.SeaBoldness01)));
            Assert.That(storm.WeightKg, Is.GreaterThan(calm.WeightKg));
            Assert.That(storm.WeightKg, Is.InRange(_fish.MinWeightKg, _fish.MaxWeightKg));
        }

        [Test]
        public void ZeroWeightCandidateCannotWinEvenAtTheStartOfTheRoll()
        {
            var other = Own<FishSpeciesDef>();
            other.Id = "fish.favoured"; other.RegionIds = new[] { Region };
            BaitTackleSettings bait = BaitTackleSettings.Default;
            bait.WrongBaitDamp01 = 0;
            FishSchoolSettings schools = FishSchoolSettings.Default; schools.BaseAppearanceChance01 = 0;
            var model = new WharfVisitCatches(_visit, new[] { _fish, other }, _water, _water.TideAt,
                Spot, Vector2.zero, DepthDropSettings.Default, bait, SeaFishingSettings.Default, schools, 75);
            var favours = new[] { other.Id };
            for (int seed = 0; seed < 64; seed++)
            {
                _water.Seed = seed;
                foreach (var cast in model.Build(seed, 525, Spot, _rod, "bait.fixture", favours))
                    Assert.That(cast.SpeciesId, Is.EqualTo(other.Id));
            }
        }

        [Test]
        public void RejectsUnknownWaterIdentityAndMissingHour()
        {
            var model = Model();
            Assert.Throws<ArgumentException>(() => model.Build(1, 525, Spot, _rod, "bait.capelin", null));
            Assert.Throws<ArgumentException>(() => model.Build(4242, 525, "unknown", _rod, "bait.capelin", null));
            Assert.Throws<ArgumentException>(() => Build(model, double.NaN));
            Assert.Throws<ArgumentException>(() => model.Build(4242, 525, Spot, default, "bait.capelin", null));
            _water.Hour = -1;
            Assert.Throws<ArgumentException>(() => Build(model));
        }

        [Test]
        public void DefinitionRejectsInvalidRangesAndRisk()
        {
            _visit.Validate();
            _visit.CastsPerVisit = 0;
            Assert.Throws<ArgumentException>(() => _visit.Validate());
            _visit.CastsPerVisit = 3; _visit.MinBiteWaitSeconds = float.NaN;
            Assert.Throws<ArgumentException>(() => _visit.Validate());
            _visit.MinBiteWaitSeconds = 8; _visit.StrongLossChance = -1;
            Assert.Throws<ArgumentException>(() => _visit.Validate());
        }

        [Test]
        public void OversizedSchoolTuningIsRejectedBeforeItCanAllocateDuringAVisit()
        {
            FishSchoolSettings schools = FishSchoolSettings.Default;
            schools.MaxSpecies = 5;
            Assert.Throws<ArgumentException>(() => new WharfVisitCatches(_visit,
                new[] { _fish, _fish, _fish, _fish, _fish }, _water, _water.TideAt, Spot, Vector2.zero,
                DepthDropSettings.Default, BaitTackleSettings.Default, SeaFishingSettings.Default, schools, 75));
        }

        [Test]
        public void ShippedVisitDefinitionIsValidAndPinsItsIdentity()
        {
            var def = AssetDatabase.LoadAssetAtPath<WharfVisitDef>(
                "Assets/_Project/Data/FishingVisits/StPetersWharfVisit.asset");
            Assert.That(def, Is.Not.Null);
            Assert.That(def.Id, Is.EqualTo("wharf_visit.st_peters"));
            Assert.DoesNotThrow(() => def.Validate());
        }

        internal FishSpeciesDef[] ShippedPlayerPool()
        {
            // Read only the serialized pool block, without loading a scene or maintaining another stock table.
            var fish = new List<FishSpeciesDef>();
            bool inPool = false;
            foreach (string line in File.ReadLines("Assets/_Project/Scenes/StPeters.unity"))
            {
                if (line == "  _regionFish:") { inPool = true; continue; }
                if (!inPool) continue;
                if (!line.StartsWith("  - ", StringComparison.Ordinal)) break;
                Match guid = Regex.Match(line, @"guid: ([a-f0-9]{32})");
                Assert.That(guid.Success, Is.True);
                var def = AssetDatabase.LoadAssetAtPath<FishSpeciesDef>(AssetDatabase.GUIDToAssetPath(guid.Groups[1].Value));
                Assert.That(def, Is.Not.Null);
                fish.Add(def);
            }
            Assert.That(fish.Count, Is.EqualTo(8));
            return fish.ToArray();
        }

        [Test]
        public void ShippedPoolEverySeasonHourTideAndWeatherStaysLegalAndWithinSizeRange()
        {
            FishSpeciesDef[] pool = ShippedPlayerPool();
            BaitDef bait = AssetDatabase.LoadAssetAtPath<BaitDef>("Assets/_Project/Data/Resources/Catalog/Bait/Capelin.asset");
            Assert.That(bait, Is.Not.Null);
            var model = Model(pool, schoolsOn: true);
            int checkedCasts = 0;
            for (int season = 0; season < 4; season++)
            for (int hour = 0; hour < 24; hour++)
            foreach (float tide in new[] { -3f, 0f, 3f })
            foreach (float rate in new[] { -1f, 0f, 1f })
            foreach (float sea in new[] { 0f, 0.5f, 1f })
            {
                _water.Season = (Season)season; _water.Hour = hour; _water.Tide = tide; _water.Rate = rate; _water.Sea = sea;
                var context = new CatchContext(Region, tide, hour, _water.Season, Gear.Handline,
                    CatchContext.NoDepth, 0f, LureTag.None, bait.FavorsSpeciesIds, SchoolInfluence.None,
                    rate, FishSchoolSettings.Default.MovingWaterMetresPerHour);
                foreach (var cast in model.Build(4242, hour * 75, Spot, _rod, bait.Id, bait.FavorsSpeciesIds))
                {
                    checkedCasts++;
                    if (!cast.HasBite)
                    {
                        Assert.That(Array.Exists(pool, f => CatchResolver.Matches(f, context)), Is.False);
                        continue;
                    }
                    FishSpeciesDef fish = Array.Find(pool, f => f.Id == cast.SpeciesId);
                    Assert.That(fish, Is.Not.Null);
                    Assert.That(CatchResolver.Matches(fish, context), Is.True);
                    Assert.That(cast.WeightKg, Is.InRange(fish.MinWeightKg, fish.MaxWeightKg));
                }
            }
            Assert.That(checkedCasts, Is.EqualTo(7776));
        }

        [TestCase(false)] [TestCase(true)]
        public void CallsAllocateOnlyTheReturnedListAndItsBackingArray(bool deadHour)
        {
            if (deadHour) { _fish.StartHour = 8; _fish.EndHour = 10; }
            var model = Model(deadHour ? new[] { _fish } : ShippedPlayerPool(), schoolsOn: true);
            List<WharfVisitCast> keep = Build(model);
            using (var probe = new ManagedAllocationProbe())
            {
                probe.Begin();
                for (int i = 0; i < 32; i++) keep = new List<WharfVisitCast>(_visit.CastsPerVisit);
                long listOnly = probe.End();
                GC.KeepAlive(keep);
                probe.Begin();
                for (int i = 0; i < 32; i++) keep = Build(model, 525 + i * 75);
                long calls = probe.End();
                GC.KeepAlive(keep);
                Assert.That(listOnly, Is.EqualTo(64));
                Assert.That(calls, Is.EqualTo(listOnly));
            }
        }

        [Test]
        public void ControlledExampleWeekMatchesTheDocument()
        {
            var pool = ShippedPlayerPool();
            var def = AssetDatabase.LoadAssetAtPath<WharfVisitDef>(
                "Assets/_Project/Data/FishingVisits/StPetersWharfVisit.asset");
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/_Project/Data/Config/GameConfig.asset");
            var bait = AssetDatabase.LoadAssetAtPath<BaitDef>("Assets/_Project/Data/Resources/Catalog/Bait/Capelin.asset");
            Assert.That(def, Is.Not.Null); Assert.That(config, Is.Not.Null); Assert.That(bait, Is.Not.Null);
            var water = new Water { AdvanceHour = true, SecondsPerHour = config.SecondsPerDay / 24.0 };
            FishSchoolSettings schools = config.FishSchools;
            schools.BaseAppearanceChance01 = 0;
            var model = new WharfVisitCatches(def, pool, water, water.TideAt, Spot, Vector2.zero,
                config.DepthDrop, config.BaitTackle, config.SeaFishing, schools, water.SecondsPerHour);
            string document = File.ReadAllText("docs/design/wharf-visit-example-week.md");
            int rows = 0, landed = 0, lost = 0;
            for (int day = 1; day <= 7; day++)
            foreach (int hour in new[] { 7, 19 })
            {
                water.Sea = day % 3 == 1 ? 0 : day % 3 == 2 ? 0.5f : 1;
                water.Tide = day % 3 == 1 ? -1 : day % 3 == 2 ? 0 : 1;
                water.Rate = day == 4 ? 0 : 1;
                var descriptions = new List<string>();
                foreach (var cast in model.Build(4242, (day - 1) * config.SecondsPerDay + hour * water.SecondsPerHour,
                    Spot, _rod, bait.Id, bait.FavorsSpeciesIds))
                {
                    string description = !cast.HasBite
                        ? string.Format(CultureInfo.InvariantCulture, "blank; wait {0:F2}s", cast.WaitSeconds)
                        : string.Format(CultureInfo.InvariantCulture,
                            "{0} {1:F2}kg; wait {2:F2}s; fight {3:F2}s; {4}; {5}", cast.SpeciesId,
                            cast.WeightKg, cast.WaitSeconds, cast.FightSeconds, cast.Strength,
                            cast.Landed ? "landed" : "lost");
                    descriptions.Add(description);
                    if (cast.HasBite) { if (cast.Landed) landed++; else lost++; }
                }
                string row = string.Format(CultureInfo.InvariantCulture, "| {0} | {1:00}:00 | {2:F1} | {3:F0} / {4:F0} | {5} |",
                    day, hour, water.Sea, water.Tide, water.Rate, string.Join(" <br> ", descriptions));
                Assert.That(document, Does.Contain(row));
                rows++;
            }
            Assert.That(rows, Is.EqualTo(14));
            Assert.That(landed, Is.EqualTo(35));
            Assert.That(lost, Is.EqualTo(7));
        }
    }
}
