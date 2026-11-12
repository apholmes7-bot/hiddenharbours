using System;
using System.Collections.Generic;
using HiddenHarbours.Core;
using HiddenHarbours.Fishing;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.EditMode
{
    public class CatchResolverBoundaryTests
    {
        private readonly List<Object> _made = new();

        private sealed class Roll : System.Random
        {
            private readonly double _value;
            public int Draws { get; private set; }
            public Roll(double value) : base(0) => _value = value;
            public override double NextDouble() { Draws++; return _value; }
        }

        private FishSpeciesDef Fish(string id, float weight = 1f)
        {
            var fish = ScriptableObject.CreateInstance<FishSpeciesDef>();
            _made.Add(fish);
            fish.Id = id;
            fish.RegionIds = new[] { "region.fixture" };
            fish.AllowedGear = Gear.Handline;
            fish.Seasons = SeasonMask.AllYear;
            fish.MinTide = -2f;
            fish.MaxTide = 2f;
            fish.StartHour = 0f;
            fish.EndHour = 24f;
            fish.SpawnWeight = weight;
            fish.MinWeightKg = 2f;
            fish.MaxWeightKg = 6f;
            return fish;
        }

        private static CatchContext Context(float tide = 0f, float hour = 12f,
            Season season = Season.HighSummer, string region = "region.fixture")
            => new CatchContext(region, tide, hour, season, Gear.Handline);

        [TearDown]
        public void TearDown()
        {
            foreach (Object item in _made) Object.DestroyImmediate(item);
            _made.Clear();
        }

        [TestCase(0d, "fish.first")]
        [TestCase(0.25d, "fish.first")]
        [TestCase(0.250001d, "fish.second")]
        [TestCase(0.999999d, "fish.second")]
        public void WeightedPick_UsesOneDraw_AndIncludesTheCumulativeBoundary(double roll, string expected)
        {
            var first = Fish("fish.first", 1f);
            var second = Fish("fish.second", 3f);
            var random = new Roll(roll);
            Assert.AreEqual(expected, CatchResolver.Resolve(new[] { first, second }, Context(), random).Id);
            Assert.AreEqual(1, random.Draws);
        }

        [Test]
        public void EmptyOrEntirelyGatedPool_DoesNotAdvanceTheCatchStream()
        {
            var wrongRegion = Fish("fish.absent");
            wrongRegion.RegionIds = new[] { "region.other" };
            var random = new Roll(0.5d);
            Assert.IsNull(CatchResolver.Resolve(null, Context(), random));
            Assert.IsNull(CatchResolver.Resolve(Array.Empty<FishSpeciesDef>(), Context(), random));
            Assert.IsNull(CatchResolver.Resolve(new[] { null, wrongRegion }, Context(), random));
            Assert.AreEqual(0, random.Draws);
        }

        [TestCase(0f)]
        [TestCase(-3f)]
        public void NonPositiveSpawnWeight_RemainsCatchableAtTheExistingFloor(float authoredWeight)
        {
            var fish = Fish("fish.floor", authoredWeight);
            var context = Context();
            Assert.AreEqual(0.0001f, CatchResolver.EffectiveWeight(fish, in context, DepthDropSettings.Default));
            Assert.AreSame(fish, CatchResolver.Resolve(new[] { fish }, context, new Roll(0.5d)));
        }

        [TestCase(-2f, true)]
        [TestCase(2f, true)]
        [TestCase(-2.001f, false)]
        [TestCase(2.001f, false)]
        public void TideGate_IncludesBothEndpoints(float tide, bool expected)
            => Assert.AreEqual(expected, CatchResolver.Matches(Fish("fish.tide"), Context(tide: tide)));

        [TestCase(Season.EarlySpring, false)]
        [TestCase(Season.HighSummer, false)]
        [TestCase(Season.TheTurn, true)]
        [TestCase(Season.HardWinter, false)]
        public void SeasonGate_UsesTheAuthoredMask(Season season, bool expected)
        {
            var fish = Fish("fish.season");
            fish.Seasons = SeasonMask.TheTurn;
            Assert.AreEqual(expected, CatchResolver.Matches(fish, Context(season: season)));
        }

        [TestCase(6f, true)]
        [TestCase(7f, false)]
        [TestCase(18f, true)]
        [TestCase(19f, false)]
        [TestCase(12f, false)]
        public void TwoFeedingWindows_IncludeStartsAndExcludeEnds(float hour, bool expected)
        {
            var fish = Fish("fish.windows");
            fish.StartHour = 6f; fish.EndHour = 7f;
            fish.SecondStartHour = 18f; fish.SecondEndHour = 19f;
            Assert.AreEqual(expected, CatchResolver.Matches(fish, Context(hour: hour)));
        }

        [Test]
        public void EqualPrimaryWindow_IsAllDay_EvenWithAnAuthoredSecondWindow()
        {
            var fish = Fish("fish.all_day");
            fish.StartHour = fish.EndHour = 6f;
            fish.SecondStartHour = 18f; fish.SecondEndHour = 19f;
            Assert.IsTrue(fish.TimeAllowed(12f));
            Assert.IsTrue(fish.TimeAllowed(0f));
        }

        [TestCase(0.49f, false)]
        [TestCase(0.5f, true)]
        [TestCase(-0.5f, true)]
        [TestCase(float.NaN, true)]
        [TestCase(float.PositiveInfinity, true)]
        public void MovingWaterGate_UsesMagnitude_IncludesThreshold_AndAllowsUnknown(float rate, bool expected)
        {
            var fish = Fish("fish.moving");
            fish.MovingWaterOnly = true;
            var context = new CatchContext("region.fixture", 0f, 12f, Season.HighSummer, Gear.Handline,
                CatchContext.NoDepth, 0f, LureTag.None, null, SchoolInfluence.None, rate, 0.5f);
            Assert.AreEqual(expected, CatchResolver.Matches(fish, in context));
        }

        [Test]
        public void RegionAndBaitIds_AreOrdinal_WhileCombinedGearAcceptsAnyAllowedBit()
        {
            var fish = Fish("fish.fixture");
            Assert.IsFalse(fish.RegionAllowed("REGION.FIXTURE"));
            Assert.IsTrue(fish.GearAllowed(Gear.Handline | Gear.Trap));
            Assert.IsFalse(fish.GearAllowed(Gear.None));
            var context = new CatchContext("region.fixture", 0f, 12f, Season.HighSummer, Gear.Handline,
                -1f, 0f, LureTag.None, new[] { "fish.fixture" });
            Assert.IsTrue(context.BaitFavours_Contains("fish.fixture"));
            Assert.IsFalse(context.BaitFavours_Contains("FISH.FIXTURE"));
            Assert.IsFalse(context.BaitFavours_Contains(null));
        }

        [TestCase(0d, 2f)]
        [TestCase(0.25d, 3f)]
        [TestCase(0.5d, 4f)]
        public void SizeRoll_UsesOneDraw_AndTheSpeciesRange(double roll, float expected)
        {
            var random = new Roll(roll);
            Assert.AreEqual(expected, CatchResolver.RollWeight(Fish("fish.size"), random));
            Assert.AreEqual(1, random.Draws);
        }

        [Test]
        public void CalmSizeOverload_PreservesBothWeightAndStream()
        {
            var fish = Fish("fish.size");
            var plain = new Roll(0.25d);
            var calm = new Roll(0.25d);
            var rough = new Roll(0.25d);
            Assert.AreEqual(CatchResolver.RollWeight(fish, plain), CatchResolver.RollWeight(fish, calm, 0f));
            Assert.Greater(CatchResolver.RollWeight(fish, rough, 1f), 3f);
            Assert.AreEqual(1, plain.Draws);
            Assert.AreEqual(1, calm.Draws);
            Assert.AreEqual(1, rough.Draws);
        }
    }
}
