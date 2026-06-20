using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Fishing;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>Catch gating + weighted resolution. Seeded RNG keeps results reproducible in tests.</summary>
    public class CatchResolverTests
    {
        private static FishSpeciesDef Fish(string id, Gear gear = Gear.Handline,
            float minTide = -10f, float maxTide = 10f, float startH = 0f, float endH = 24f,
            SeasonMask seasons = SeasonMask.AllYear, float weight = 1f,
            string region = "region.coddle_cove")
        {
            var f = ScriptableObject.CreateInstance<FishSpeciesDef>();
            f.Id = id;
            f.AllowedGear = gear;
            f.MinTide = minTide; f.MaxTide = maxTide;
            f.StartHour = startH; f.EndHour = endH;
            f.Seasons = seasons;
            f.SpawnWeight = weight;
            f.RegionIds = new[] { region };
            f.MinWeightKg = 1f; f.MaxWeightKg = 2f;
            return f;
        }

        private static CatchContext Ctx(float tide = 0f, float hour = 12f,
            Season season = Season.HighSummer, Gear gear = Gear.Handline,
            string region = "region.coddle_cove")
            => new CatchContext(region, tide, hour, season, gear);

        [Test]
        public void Matches_RespectsTideWindow()
        {
            var f = Fish("a", minTide: 1f, maxTide: 3f);
            Assert.IsFalse(CatchResolver.Matches(f, Ctx(tide: 0f)));
            Assert.IsTrue(CatchResolver.Matches(f, Ctx(tide: 2f)));
        }

        [Test]
        public void Matches_RespectsGear()
        {
            var f = Fish("a", gear: Gear.Trap);
            Assert.IsFalse(CatchResolver.Matches(f, Ctx(gear: Gear.Handline)));
            Assert.IsTrue(CatchResolver.Matches(f, Ctx(gear: Gear.Trap)));
        }

        [Test]
        public void Matches_RespectsWrappingNightWindow()
        {
            var f = Fish("a", startH: 20f, endH: 4f);  // 8pm → 4am
            Assert.IsTrue(CatchResolver.Matches(f, Ctx(hour: 23f)));
            Assert.IsTrue(CatchResolver.Matches(f, Ctx(hour: 2f)));
            Assert.IsFalse(CatchResolver.Matches(f, Ctx(hour: 12f)));
        }

        [Test]
        public void Resolve_IsReproducibleWithSeed()
        {
            var pool = new List<FishSpeciesDef> { Fish("a"), Fish("b"), Fish("c") };
            var r1 = CatchResolver.Resolve(pool, Ctx(), new System.Random(123));
            var r2 = CatchResolver.Resolve(pool, Ctx(), new System.Random(123));
            Assert.AreSame(r1, r2);
        }

        [Test]
        public void Resolve_ReturnsNullWhenNothingMatches()
        {
            var pool = new List<FishSpeciesDef> { Fish("a", region: "region.elsewhere") };
            Assert.IsNull(CatchResolver.Resolve(pool, Ctx(region: "region.coddle_cove"), new System.Random(1)));
        }

        [Test]
        public void Resolve_FavoursHigherSpawnWeight()
        {
            var pool = new List<FishSpeciesDef> { Fish("rare", weight: 1f), Fish("common", weight: 99f) };
            var rng = new System.Random(7);
            int common = 0;
            for (int i = 0; i < 200; i++)
                if (CatchResolver.Resolve(pool, Ctx(), rng).Id == "common") common++;
            Assert.Greater(common, 150);
        }
    }
}
