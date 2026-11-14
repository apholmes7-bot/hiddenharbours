using System.Collections.Generic;
using HiddenHarbours.Core;
using HiddenHarbours.Fishing;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>Native EditMode coverage of actual species Defs and the unchanged catch/deck path.
    /// These cases require Unity and are compile-only in a no-editor lane.</summary>
    public class HabitatTrapCatchIntegrationTests
    {
        private FishSpeciesDef _species;
        private TrapDef _trap;
        private IBottomMap _rock, _mud;
        private TrapHabitatSettings _habitat;
        private CatchContext _context;
        private List<CatchItem> _items;

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();
            _species = ScriptableObject.CreateInstance<FishSpeciesDef>();
            _species.Id = "fish.lobster"; _species.DisplayName = "Lobster";
            _species.RegionIds = new[] { "region.test" }; _species.AllowedGear = Gear.Trap;
            _species.Seasons = SeasonMask.AllYear; _species.MinTide = -10f; _species.MaxTide = 10f;
            _species.StartHour = 0f; _species.EndHour = 24f; _species.SpawnWeight = 1f;
            _trap = ScriptableObject.CreateInstance<TrapDef>();
            _trap.AllowedCatchFishIds = new[] { _species.Id }; _trap.CapacityUnits = 4;
            _trap.SoakHours = 1f; _trap.HoursToFullPot = 2f;
            var r = new BottomMapSettings { CellMetres = 10f, RockWeight = 1f };
            _rock = new DeterministicBottomMap(42, in r); r.RockWeight = 0f; r.MudWeight = 1f;
            _mud = new DeterministicBottomMap(42, in r);
            _habitat = new TrapHabitatSettings { SpeciesId = _species.Id, RockAffinity01 = 1f,
                RadiusMetres = 10f, OffCentreDensity01 = 1f, PeriodGameSeconds = 1000d };
            _context = new CatchContext("region.test", 0f, 12f, Season.HighSummer, Gear.Trap);
            _items = new List<CatchItem>(4);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_species); Object.DestroyImmediate(_trap); GameServices.Reset();
        }

        private int Resolve(IBottomMap map, double hauledAt, IReadOnlyList<FishSpeciesDef> pool = null)
            => HabitatTrapCatch.Resolve(_trap, pool ?? new[] { _species }, in _context, null, 1, map,
                Vector2.zero, in _habitat, 42, "pot.test", 0d, hauledAt, _items);

        [Test]
        public void ActualTrapPath_ResolvesIndividualAnimalsIntoUnmodifiedDeckPot()
        {
            Assert.AreEqual(4, Resolve(_rock, TrapSoak.SoakSeconds(2f)));
            var deck = new DeckPot(_trap, null, "pot.test", 0d, 42, _items);
            Assert.AreEqual(4, deck.Animals.Count); Assert.AreEqual(4, deck.InPotCount);
            for (int i = 0; i < 4; i++)
            { Assert.AreEqual(_species.Id, deck.Animals[i].Item.SpeciesId); Assert.AreEqual(i, deck.Animals[i].Index); }
            Assert.AreEqual(1, Resolve(_mud, TrapSoak.SoakSeconds(2f)));
        }

        [Test]
        public void ActualTrapPath_NotReadyHasNoCatch_ReadyHasOneAnimal()
        {
            Assert.AreEqual(0, Resolve(_rock, 0d));
            Assert.AreEqual(1, Resolve(_rock, TrapSoak.SoakSeconds(1f)));
        }

        [Test]
        public void ActualTrapPath_RetainsIndexedSpeciesAndWeightStreams()
        {
            double hauledAt = TrapSoak.SoakSeconds(2f); Assert.AreEqual(4, Resolve(_rock, hauledAt));
            var baseline = new List<CatchItem>(4);
            PlacedTrapCatch.ResolveMany(new[] { _species }, in _context, null, 1, 4, 42, "pot.test", 0d, baseline);
            Assert.AreEqual(4, baseline.Count);
            for (int i = 0; i < 4; i++)
            { Assert.AreEqual(baseline[i].SpeciesId, _items[i].SpeciesId); Assert.AreEqual(baseline[i].WeightKg, _items[i].WeightKg); }
        }

        [Test]
        public void ActualTrapPath_MixedPoolsAndMismatchedSpeciesAreRejected()
        {
            Assert.AreEqual(0, Resolve(_rock, TrapSoak.SoakSeconds(2f), new[] { _species, _species }));
            _habitat.SpeciesId = "fish.other"; Assert.AreEqual(0, Resolve(_rock, TrapSoak.SoakSeconds(2f)));
            _habitat.SpeciesId = _species.Id; _trap.AllowedCatchFishIds = new[] { "fish.other" };
            Assert.AreEqual(0, Resolve(_rock, TrapSoak.SoakSeconds(2f)));
        }

        [Test]
        public void ActualTrapPath_ExistingRegionAndGearGatesStillApply()
        {
            _context = new CatchContext("region.other", 0f, 12f, Season.HighSummer, Gear.Trap);
            Assert.AreEqual(0, Resolve(_rock, TrapSoak.SoakSeconds(2f)));
            _context = new CatchContext("region.test", 0f, 12f, Season.HighSummer, Gear.Handline);
            Assert.AreEqual(0, Resolve(_rock, TrapSoak.SoakSeconds(2f)));
        }
    }
}
