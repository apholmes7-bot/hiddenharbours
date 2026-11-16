using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Fishing;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Per-port fishing seasons: the pure open/closed read (<see cref="FishingSeasonMath"/>), its deck-sort
    /// wiring (<see cref="DeckPot"/>: an out-of-season would-be keeper goes back, never lands), and the
    /// authored lobster seasons (<c>Data/FishingSeasons</c>). Open or closed is a pure function of
    /// (species, region, calendar day), so every assertion here is deterministic (rule 5).
    /// </summary>
    public class FishingSeasonTests
    {
        private const int Len = GameConfig.DefaultDaysPerSeason;
        private const string Lobster = "fish.lobster";
        private const string Port = "region.test_port";
        private const string SeasonsFolder = "Assets/_Project/Data/FishingSeasons";
        private const string PotDeckWorkPath = "Assets/_Project/Data/Traps/PotDeckWork.asset";
        private const string GameConfigPath = "Assets/_Project/Data/Config/GameConfig.asset";

        private readonly List<Object> _spawned = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        private FishingSeasonDef MakeSeason(string region, Season open, int openDay, Season close, int closeDay,
                                            string species = Lobster)
        {
            var s = ScriptableObject.CreateInstance<FishingSeasonDef>();
            s.Id = "season.test_" + _spawned.Count;
            s.SpeciesId = species;
            s.RegionId = region;
            s.OpenSeason = open;
            s.OpenDayOfSeason = openDay;
            s.CloseSeason = close;
            s.CloseDayOfSeason = closeDay;
            _spawned.Add(s);
            return s;
        }

        private static SeasonDate On(Season season, int day) => new SeasonDate(season, day, Len);

        // ---- the pure day-of-year math -------------------------------------------------------------

        [Test]
        public void DayOfYear_CountsSeasonsThenDays_AndClampsTheDay()
        {
            Assert.AreEqual(0, FishingSeasonMath.DayOfYear(Season.EarlySpring, 1, Len));
            Assert.AreEqual(Len, FishingSeasonMath.DayOfYear(Season.HighSummer, 1, Len));
            Assert.AreEqual(4 * Len - 1, FishingSeasonMath.DayOfYear(Season.HardWinter, Len, Len));
            Assert.AreEqual(Len - 1, FishingSeasonMath.DayOfYear(Season.EarlySpring, Len + 9, Len),
                "a day past the season length clamps to its last day (survives a DaysPerSeason retune)");
            Assert.AreEqual(0, FishingSeasonMath.DayOfYear(Season.EarlySpring, 0, Len), "day 0 clamps to day 1");
        }

        [Test]
        public void Window_IsInclusiveAtBothEnds()
        {
            Assert.IsTrue(FishingSeasonMath.IsDayInWindow(10, 20, 10), "open day is open");
            Assert.IsTrue(FishingSeasonMath.IsDayInWindow(10, 20, 15), "inside");
            Assert.IsTrue(FishingSeasonMath.IsDayInWindow(10, 20, 20), "close day is still open");
            Assert.IsFalse(FishingSeasonMath.IsDayInWindow(10, 20, 9), "the day before opening is closed");
            Assert.IsFalse(FishingSeasonMath.IsDayInWindow(10, 20, 21), "the day after closing is closed");
            Assert.IsTrue(FishingSeasonMath.IsDayInWindow(7, 7, 7), "equal ends are a one-day season");
            Assert.IsFalse(FishingSeasonMath.IsDayInWindow(7, 7, 8));
        }

        [Test]
        public void Window_OpenAfterClose_WrapsTheYearEnd()
        {
            Assert.IsTrue(FishingSeasonMath.IsDayInWindow(100, 5, 100));
            Assert.IsTrue(FishingSeasonMath.IsDayInWindow(100, 5, 111), "late in the year");
            Assert.IsTrue(FishingSeasonMath.IsDayInWindow(100, 5, 0), "the year turns, still open");
            Assert.IsTrue(FishingSeasonMath.IsDayInWindow(100, 5, 5), "close day");
            Assert.IsFalse(FishingSeasonMath.IsDayInWindow(100, 5, 6));
            Assert.IsFalse(FishingSeasonMath.IsDayInWindow(100, 5, 99));
            Assert.IsFalse(FishingSeasonMath.IsDayInWindow(100, 5, 50), "mid-year is closed");
        }

        [Test]
        public void IsOpen_ReadsSeasonAndDay_ForOneEntry()
        {
            var seasons = new[] { MakeSeason(Port, Season.EarlySpring, 15, Season.HighSummer, 21) };

            Assert.IsFalse(FishingSeasonMath.IsOpen(seasons, Lobster, Port, On(Season.EarlySpring, 14)), "eve of opening");
            Assert.IsTrue(FishingSeasonMath.IsOpen(seasons, Lobster, Port, On(Season.EarlySpring, 15)), "opening day");
            Assert.IsTrue(FishingSeasonMath.IsOpen(seasons, Lobster, Port, On(Season.HighSummer, 1)), "across a season turn");
            Assert.IsTrue(FishingSeasonMath.IsOpen(seasons, Lobster, Port, On(Season.HighSummer, 21)), "closing day");
            Assert.IsFalse(FishingSeasonMath.IsOpen(seasons, Lobster, Port, On(Season.HighSummer, 22)), "day after");
            Assert.IsFalse(FishingSeasonMath.IsOpen(seasons, Lobster, Port, On(Season.HardWinter, 10)), "winter");
        }

        [Test]
        public void IsOpen_WrappingEntry_IsOpenAcrossTheWinterTurn()
        {
            var seasons = new[] { MakeSeason(Port, Season.HardWinter, 8, Season.EarlySpring, 21) };

            Assert.IsTrue(FishingSeasonMath.IsOpen(seasons, Lobster, Port, On(Season.HardWinter, 8)));
            Assert.IsTrue(FishingSeasonMath.IsOpen(seasons, Lobster, Port, On(Season.HardWinter, Len)));
            Assert.IsTrue(FishingSeasonMath.IsOpen(seasons, Lobster, Port, On(Season.EarlySpring, 1)));
            Assert.IsTrue(FishingSeasonMath.IsOpen(seasons, Lobster, Port, On(Season.EarlySpring, 21)));
            Assert.IsFalse(FishingSeasonMath.IsOpen(seasons, Lobster, Port, On(Season.EarlySpring, 22)));
            Assert.IsFalse(FishingSeasonMath.IsOpen(seasons, Lobster, Port, On(Season.HardWinter, 7)));
            Assert.IsFalse(FishingSeasonMath.IsOpen(seasons, Lobster, Port, On(Season.HighSummer, 10)));
        }

        [Test]
        public void IsOpen_NoEntryForThePortOrSpecies_IsOpenAllYear()
        {
            var seasons = new[] { MakeSeason(Port, Season.EarlySpring, 15, Season.HighSummer, 21) };
            SeasonDate winter = On(Season.HardWinter, 10);

            Assert.IsFalse(FishingSeasonMath.IsOpen(seasons, Lobster, Port, winter), "control: closed here");
            Assert.IsTrue(FishingSeasonMath.IsOpen(seasons, Lobster, "region.other_port", winter),
                "a port with no entry is open (only an explicit entry closes a fishery)");
            Assert.IsTrue(FishingSeasonMath.IsOpen(seasons, "fish.rock_crab", Port, winter),
                "a species with no entry is open");
            Assert.IsTrue(FishingSeasonMath.IsOpen(System.Array.Empty<FishingSeasonDef>(), Lobster, Port, winter));
            Assert.IsTrue(FishingSeasonMath.IsOpen(null, Lobster, Port, winter));
        }

        [Test]
        public void IsOpen_UnknownDate_IsOpen()
        {
            var seasons = new[] { MakeSeason(Port, Season.EarlySpring, 15, Season.HighSummer, 21) };
            Assert.IsTrue(FishingSeasonMath.IsOpen(seasons, Lobster, Port, default(SeasonDate)),
                "no clock (an unknown date) never closes a fishery");
            Assert.IsFalse(SeasonDate.From(null, Len).IsKnown);
        }

        [Test]
        public void IsOpen_TwoEntriesForOnePort_AreAUnion()
        {
            var seasons = new[]
            {
                MakeSeason(Port, Season.EarlySpring, 1, Season.EarlySpring, 10),
                MakeSeason(Port, Season.TheTurn, 5, Season.TheTurn, 20),
            };
            Assert.IsTrue(FishingSeasonMath.IsOpen(seasons, Lobster, Port, On(Season.EarlySpring, 5)), "spring window");
            Assert.IsTrue(FishingSeasonMath.IsOpen(seasons, Lobster, Port, On(Season.TheTurn, 20)), "fall window");
            Assert.IsFalse(FishingSeasonMath.IsOpen(seasons, Lobster, Port, On(Season.HighSummer, 5)), "between them");
        }

        [Test]
        public void IsOpen_IdsCompareIgnoringCase()
        {
            var seasons = new[] { MakeSeason(Port, Season.EarlySpring, 15, Season.HighSummer, 21) };
            Assert.IsFalse(FishingSeasonMath.IsOpen(seasons, "FISH.LOBSTER", "REGION.TEST_PORT", On(Season.HardWinter, 1)));
        }

        // ---- the deck-sort wiring ------------------------------------------------------------------

        private DeckWorkDef MakeDeckWork(float minKeepMm, params FishingSeasonDef[] seasons)
        {
            var d = ScriptableObject.CreateInstance<DeckWorkDef>();
            d.Id = "deckwork.test";
            d.QuickGrabSeconds = 0.15f;
            d.FullGrabSeconds = 0.9f;
            d.NipChanceRushed01 = 0f;
            d.NipChanceCareful01 = 0f;
            d.SpeciesRules = new[]
            {
                new SpeciesDeckRule
                {
                    SpeciesId = Lobster, MinKeepSizeMm = minKeepMm, SizeMinMm = 62f, SizeMaxMm = 140f,
                    CanBeBerried = false, BerriedChance01 = 0f, Shape = DeckAnimalShape.Lobster,
                },
            };
            d.Seasons = seasons;
            _spawned.Add(d);
            return d;
        }

        private TrapDef MakeTrap(DeckWorkDef def)
        {
            var t = ScriptableObject.CreateInstance<TrapDef>();
            t.Id = "trap.lobster";
            t.AllowedCatchFishIds = new[] { Lobster };
            t.DeckWork = def;
            _spawned.Add(t);
            return t;
        }

        private static CatchItem LobsterItem() => new CatchItem(Lobster, "American Lobster", FishCategory.Shellfish, 0.9f, 28, 0.35f);

        private DeckPot MakePot(DeckWorkDef def, string region, SeasonDate date)
            => new DeckPot(MakeTrap(def), def, "trap.lobster#5000.1", 5000.0, 4242,
                           new[] { LobsterItem(), LobsterItem() }, region, date);

        [Test]
        public void OutOfSeason_ALegalLobster_IsCaughtButGoesBack()
        {
            DeckWorkDef def = MakeDeckWork(0f, MakeSeason(Port, Season.EarlySpring, 15, Season.HighSummer, 21));
            DeckPot pot = MakePot(def, Port, On(Season.HardWinter, 3));

            Assert.AreEqual(2, pot.Animals.Count, "still caught: the pot comes up with them in her");
            Assert.IsTrue(pot.Animals.All(a => a.OutOfSeason && !a.Keeper), "legal size, but closed here");
            Assert.AreEqual(GrabOutcome.ReturnedOutOfSeason, pot.TryGrabNext(1f, out DeckPot.Animal grabbed));
            Assert.AreEqual(DeckAnimalFate.Returned, grabbed.Fate);
            Assert.AreEqual(BandOutcome.None, pot.TryBandNext(new FakeHold(), out _), "nothing to band, nothing kept");
        }

        [Test]
        public void OutOfSeason_AutoResolve_LandsNothing()
        {
            DeckWorkDef def = MakeDeckWork(0f, MakeSeason(Port, Season.EarlySpring, 15, Season.HighSummer, 21));
            DeckPot pot = MakePot(def, Port, On(Season.TheTurn, 10));
            var hold = new FakeHold();
            var landed = new List<CatchItem>();

            pot.AutoResolve(hold, landed, out int kept, out int returned, out int noRoom);

            Assert.AreEqual(0, kept);
            Assert.AreEqual(2, returned);
            Assert.AreEqual(0, noRoom);
            Assert.AreEqual(0, hold.Items.Count, "not kept, so never in the hold and never sellable");
            Assert.AreEqual(0, landed.Count);
        }

        [Test]
        public void InSeason_ALegalLobster_IsAKeeper()
        {
            DeckWorkDef def = MakeDeckWork(0f, MakeSeason(Port, Season.EarlySpring, 15, Season.HighSummer, 21));
            DeckPot pot = MakePot(def, Port, On(Season.HighSummer, 21));

            Assert.AreEqual(GrabOutcome.Keeper, pot.TryGrabNext(1f, out DeckPot.Animal grabbed));
            Assert.IsFalse(grabbed.OutOfSeason);
            var hold = new FakeHold();
            Assert.AreEqual(BandOutcome.Banded, pot.TryBandNext(hold, out _));
            Assert.AreEqual(1, hold.Items.Count);
        }

        [Test]
        public void OutOfSeason_AShort_StillReadsAsAShort()
        {
            DeckWorkDef def = MakeDeckWork(1000f, MakeSeason(Port, Season.EarlySpring, 15, Season.HighSummer, 21));
            DeckPot pot = MakePot(def, Port, On(Season.HardWinter, 3));

            Assert.AreEqual(GrabOutcome.ReturnedShort, pot.TryGrabNext(1f, out DeckPot.Animal grabbed),
                "the season only re-sorts a would-be keeper; a short keeps its own reason");
            Assert.IsFalse(grabbed.OutOfSeason);
        }

        [Test]
        public void ThePortWherethePotWasSet_DecidesTheSeason()
        {
            DeckWorkDef def = MakeDeckWork(0f, MakeSeason(Port, Season.EarlySpring, 15, Season.HighSummer, 21));

            Assert.AreEqual(GrabOutcome.ReturnedOutOfSeason,
                MakePot(def, Port, On(Season.HardWinter, 3)).TryGrabNext(1f, out _));
            Assert.AreEqual(GrabOutcome.Keeper,
                MakePot(def, "region.other_port", On(Season.HardWinter, 3)).TryGrabNext(1f, out _),
                "a port with no entry is open");
        }

        [Test]
        public void PlacementFactsConstructor_NoDate_KeepsTheOldVerdict()
        {
            DeckWorkDef def = MakeDeckWork(0f, MakeSeason(Port, Season.EarlySpring, 15, Season.HighSummer, 21));
            var pot = new DeckPot(MakeTrap(def), def, "trap.lobster#5000.1", 5000.0, 4242, new[] { LobsterItem() });
            Assert.AreEqual(GrabOutcome.Keeper, pot.TryGrabNext(1f, out _),
                "callers without a date (NPC pots, older paths) are unchanged");
        }

        [Test]
        public void SameFacts_SameSeasonVerdict_EveryTime()
        {
            DeckWorkDef def = MakeDeckWork(83f, MakeSeason(Port, Season.EarlySpring, 15, Season.HighSummer, 21));
            for (int day = 1; day <= Len; day++)
            {
                DeckPot a = MakePot(def, Port, On(Season.HighSummer, day));
                DeckPot b = MakePot(def, Port, On(Season.HighSummer, day));
                for (int i = 0; i < a.Animals.Count; i++)
                {
                    Assert.AreEqual(a.Animals[i].Keeper, b.Animals[i].Keeper, $"day {day} animal {i}");
                    Assert.AreEqual(a.Animals[i].OutOfSeason, b.Animals[i].OutOfSeason, $"day {day} animal {i}");
                }
            }
        }

        private sealed class FakeHold : IHold
        {
            private readonly List<CatchItem> _items = new();
            public int CapacityUnits => 6;
            public int UsedUnits => _items.Count;
            public IReadOnlyList<CatchItem> Items => _items;
            public bool TryAdd(CatchItem item) { if (_items.Count >= CapacityUnits) return false; _items.Add(item); return true; }
            public void Clear() => _items.Clear();
        }

        // ---- the authored content ------------------------------------------------------------------

        private static List<FishingSeasonDef> AuthoredSeasons() => AssetDatabase
            .FindAssets("t:FishingSeasonDef", new[] { SeasonsFolder })
            .Select(g => AssetDatabase.LoadAssetAtPath<FishingSeasonDef>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(s => s != null)
            .ToList();

        private static HashSet<string> IdsOf<T>(System.Func<T, string> id) where T : Object
            => new HashSet<string>(AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { "Assets/_Project/Data" })
                .Select(g => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(a => a != null).Select(id), System.StringComparer.OrdinalIgnoreCase);

        [Test]
        public void AuthoredSeasons_HaveUniqueIds_AndResolvableRegionsAndSpecies()
        {
            List<FishingSeasonDef> seasons = AuthoredSeasons();
            Assert.That(seasons.Count, Is.GreaterThan(0), "the lobster seasons are authored");

            HashSet<string> regions = IdsOf<RegionDef>(r => r.Id);
            HashSet<string> species = IdsOf<FishSpeciesDef>(f => f.Id);
            var ids = new HashSet<string>();
            foreach (FishingSeasonDef s in seasons)
            {
                Assert.That(s.Id, Does.StartWith("season."), s.name);
                Assert.IsTrue(ids.Add(s.Id), "duplicate id " + s.Id);
                Assert.IsTrue(regions.Contains(s.RegionId), $"{s.Id}: region {s.RegionId} must be an authored RegionDef");
                Assert.IsTrue(species.Contains(s.SpeciesId), $"{s.Id}: species {s.SpeciesId} must be an authored FishSpeciesDef");
                Assert.That(s.OpenDayOfSeason, Is.InRange(1, Len), s.Id);
                Assert.That(s.CloseDayOfSeason, Is.InRange(1, Len), s.Id);
            }
        }

        [Test]
        public void ThePotDeckWork_ReferencesEveryAuthoredSeason()
        {
            var deckWork = AssetDatabase.LoadAssetAtPath<DeckWorkDef>(PotDeckWorkPath);
            Assert.IsNotNull(deckWork, PotDeckWorkPath);
            Assert.IsNotNull(deckWork.Seasons);
            CollectionAssert.AllItemsAreNotNull(deckWork.Seasons, "no broken season reference");
            CollectionAssert.AreEquivalent(AuthoredSeasons().Select(s => s.Id), deckWork.Seasons.Select(s => s.Id),
                "an authored season that no deck ruleset references would never close anything");
        }

        [Test]
        public void TheHomePort_IsOpenForLobsterOnTheFirstMorning()
        {
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(GameConfigPath);
            Assert.IsNotNull(config, GameConfigPath);
            var deckWork = AssetDatabase.LoadAssetAtPath<DeckWorkDef>(PotDeckWorkPath);
            var start = new SeasonDate(config.StartSeason, config.StartDayOfSeason, config.DaysPerSeason);

            foreach (string home in new[] { "region.st_peters", "region.east_water" })
                Assert.IsTrue(deckWork.IsInSeason(Lobster, home, start),
                    $"the starter pots must be able to land a lobster at {home} when a new game begins");
        }

        [Test]
        public void TheAuthoredPorts_StaggerTheirSeasons()
        {
            var deckWork = AssetDatabase.LoadAssetAtPath<DeckWorkDef>(PotDeckWorkPath);
            int len = Len;
            bool differ = false;
            for (int doy = 0; doy < 4 * len; doy++)
            {
                var date = new SeasonDate((Season)(doy / len), doy % len + 1, len);
                bool anyOpen = AuthoredSeasons().Any(s => FishingSeasonMath.IsOpen(s, date));
                Assert.IsTrue(anyOpen, $"day {doy + 1}: some port is open (sailing to it is the answer)");
                if (deckWork.IsInSeason(Lobster, "region.st_peters", date)
                    != deckWork.IsInSeason(Lobster, "region.rice_point", date)) differ = true;
            }
            Assert.IsTrue(differ, "neighbouring ports must not share one calendar");
        }
    }
}
