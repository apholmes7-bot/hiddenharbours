using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Economy;
using HiddenHarbours.Fishing;
using HiddenHarbours.Player;   // ToolDef: the shovel the dig needs in hand
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Owner ruling 2026-11-23, pinned: <i>nothing is blocked from the player first digging, catching or
    /// trapping, but he automatically releases what he holds no licence for, saying "I don't have a
    /// licence for this &lt;species&gt; yet"</i>, and he takes no notes on it.
    ///
    /// <para>One shared rule (<see cref="UnlicensedCatch"/>) on three paths: the rod, the clam dig and the
    /// trap haul (instant and deck-worked). For each: licensed lands and records; unlicensed is let go,
    /// he says the one line naming the species, and nothing a record counts is published (no
    /// <see cref="FishCaught"/>, <see cref="CatchLanded"/> or <see cref="CatchReleased"/>); a species no
    /// licence lists still lands.</para>
    /// </summary>
    public class UnlicensedCatchReleaseTests
    {
        private const string Region = "region.coddle_cove";
        private const string ClamId = "fish.soft_shell_clam";
        private const string ClamName = "Soft-shell Clam";
        private const string ClamLicenceId = "license.clam";
        private const string CodId = "fish.atlantic_cod";
        private const string CodLicenceId = "license.cod";
        private const string GatedPotId = "fish.test_gated_crab";
        private const string GatedPotName = "Test Crab";
        private const string OpenPotId = "fish.test_open_lobster";
        private const string PotLicenceId = "license.test_crab";

        // ---- fakes ---------------------------------------------------------------------------------

        private sealed class FakeHold : IHold
        {
            private readonly List<CatchItem> _items = new();
            public int Capacity = 20;
            public int CapacityUnits => Capacity;
            public int UsedUnits => _items.Count;
            public IReadOnlyList<CatchItem> Items => _items;
            public bool TryAdd(CatchItem item) { if (_items.Count >= Capacity) return false; _items.Add(item); return true; }
            public void Clear() => _items.Clear();
        }

        private sealed class FakeLicenses : ILicenseService
        {
            private readonly HashSet<string> _held = new();
            public bool IsLicensed(string licenseId) => string.IsNullOrEmpty(licenseId) || _held.Contains(licenseId);
            public void Grant(string licenseId) { if (!string.IsNullOrEmpty(licenseId)) _held.Add(licenseId); }
            public int Count => _held.Count;
        }

        private sealed class FakeConservation : IFishConservationFacts
        {
            private readonly HashSet<string> _protected;
            public FakeConservation(params string[] ids) { _protected = new HashSet<string>(ids); }
            public bool IsProtected(string fishId) => fishId != null && _protected.Contains(fishId);
        }

        private sealed class FlatTerrain : ITidalTerrain
        {
            public float Elevation;
            public float ElevationAt(Vector2 worldPos) => Elevation;
        }

        private sealed class FakeEnv : IEnvironmentService
        {
            public float Level;
            public int WorldSeed => 4242;
            public TideProfile ActiveTideProfile { get; set; }
            public EnvironmentSample Sample() => default;
            public float TideHeightAt(double totalSeconds) => Level;
            public float WaterLevelAt(double totalSeconds) => Level;
        }

        private sealed class FakeClock : IGameClock
        {
            public double Seconds;
            public double TotalSeconds => Seconds;
            public GameTime Now => default;
            public Season Season => Season.HighSummer;
            public int Year => 1;
            public int DayIndex => 0;
            public int DayOfSeason => 1;
            public Weekday Weekday => default;
            public bool IsMarketDay => false;
            public float HourOfDay => 12f;
            public float DayFraction => 0.5f;
            public bool IsPaused { get; set; }
            public float TimeScale { get; set; } = 1f;
        }

        private sealed class FakeSaveService : ISaveService
        {
            public FakeSaveService(SaveData data) { Current = data; }
            public SaveData Current { get; }
            public bool GetFlag(string key) => false;
            public void SetFlag(string key, bool value) { }
            public void Save() { }
        }

        // ---- what the bus heard ---------------------------------------------------------------------

        private readonly List<Object> _spawned = new();
        private readonly List<CatchItem> _caught = new();
        private readonly List<CatchItem> _released = new();
        private readonly List<UnlicensedCatchReleased> _unlicensed = new();
        private readonly List<InnerVoiceCue> _cues = new();
        private readonly List<JuiceMoment> _moments = new();
        private int _inHand;

        private void OnCaught(FishCaught e) => _caught.Add(e.Item);
        private void OnReleased(CatchReleased e) => _released.Add(e.Item);
        private void OnUnlicensed(UnlicensedCatchReleased e) => _unlicensed.Add(e);
        private void OnCue(InnerVoiceCue e) => _cues.Add(e);
        private void OnMoment(JuiceMomentCue e) => _moments.Add(e.Kind);
        private void OnInHand(CatchLanded e) => _inHand++;

        private FakeLicenses _wallet;

        [SetUp]
        public void SetUp()
        {
            ClearBus();
            GameServices.Reset();
            FishSpeciesRegistry.Reset();
            InteractionGate.Reset();
            UnlicensedCatch.ResetCatalog();
            _caught.Clear(); _released.Clear(); _unlicensed.Clear(); _cues.Clear(); _moments.Clear();
            _inHand = 0;
            EventBus.Subscribe<FishCaught>(OnCaught);
            EventBus.Subscribe<CatchReleased>(OnReleased);
            EventBus.Subscribe<UnlicensedCatchReleased>(OnUnlicensed);
            EventBus.Subscribe<InnerVoiceCue>(OnCue);
            EventBus.Subscribe<JuiceMomentCue>(OnMoment);
            EventBus.Subscribe<CatchLanded>(OnInHand);
            _wallet = new FakeLicenses();
        }

        [TearDown]
        public void TearDown()
        {
            ClearBus();
            GameServices.Reset();
            FishSpeciesRegistry.Reset();
            InteractionGate.Reset();
            UnlicensedCatch.ResetCatalog();
            foreach (var o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        private static void ClearBus()
        {
            EventBus.Clear<FishCaught>();
            EventBus.Clear<CatchReleased>();
            EventBus.Clear<UnlicensedCatchReleased>();
            EventBus.Clear<InnerVoiceCue>();
            EventBus.Clear<JuiceMomentCue>();
            EventBus.Clear<CatchLanded>();
            EventBus.Clear<FishingStateChanged>();
            EventBus.Clear<DevNotice>();
            EventBus.Clear<AmbientSpeechRequested>();
            EventBus.Clear<InnerVoiceCueSpoken>();
        }

        private void AssertNothingRecorded(string path)
        {
            Assert.AreEqual(0, _caught.Count, $"{path}: no FishCaught (nothing was kept)");
            Assert.AreEqual(0, _inHand, $"{path}: no CatchLanded (nothing in his hands)");
            Assert.AreEqual(0, _released.Count, $"{path}: no CatchReleased: the Notebook counts that, and he takes no notes");
        }

        private void AssertSaidOnce(string name)
        {
            Assert.AreEqual(1, _cues.Count, "he says exactly one line");
            Assert.AreEqual(InnerVoiceCues.UnlicensedCatch, _cues[0].CueId);
            Assert.AreEqual(name, _cues[0].Name, "the line names the species");
        }

        // ---- fixtures -------------------------------------------------------------------------------

        private LicenseDef MakeLicence(string id, params string[] speciesIds)
        {
            var lic = ScriptableObject.CreateInstance<LicenseDef>();
            lic.Id = id; lic.DisplayName = id; lic.PermittedSpeciesIds = speciesIds;
            _spawned.Add(lic);
            return lic;
        }

        private FishSpeciesDef MakeRodFish(string id, string name)
        {
            var f = ScriptableObject.CreateInstance<FishSpeciesDef>();
            f.Id = id; f.DisplayName = name; f.Category = FishCategory.InshoreGroundfish;
            f.RegionIds = new[] { Region };
            f.AllowedGear = Gear.Handline | Gear.Longline;
            f.Seasons = SeasonMask.AllYear;
            f.MinTide = -10f; f.MaxTide = 10f; f.StartHour = 0f; f.EndHour = 24f;
            f.MinWeightKg = 1f; f.MaxWeightKg = 6f; f.BaseValue = 12; f.SupplyElasticity = 0.2f; f.SpawnWeight = 1f;
            _spawned.Add(f);
            return f;
        }

        /// <summary>The licence-gate harness: flick-cast, then a paced fight to the result.</summary>
        private FishingController LandOne(IHold hold, FishSpeciesDef fish, LicenseDef[] handed)
        {
            var go = new GameObject("Fisher");
            _spawned.Add(go);
            var c = go.AddComponent<FishingController>();
            c.Configure(hold, new[] { fish }, Region, Gear.Handline, 999, handed);
            FlickGestures.CastLine(c);
            float t = 0f;
            while (c.Phase != FishingPhase.Fighting && c.Phase != FishingPhase.Tending && t < 30f)
            { c.Tick(0.05f, false); t += 0.05f; }
            t = 0f;
            while (c.Phase != FishingPhase.Landed && c.Phase != FishingPhase.Snapped
                   && c.Phase != FishingPhase.NoBite && c.Phase != FishingPhase.Idle && t < 120f)
            { c.Tick(0.05f, actionHeld: c.State.Tension01 < 0.5f); t += 0.05f; }
            return c;
        }

        // ---- the one rule ---------------------------------------------------------------------------

        [Test]
        public void MustRelease_OnlyAGatedSpecies_WithoutItsLicence()
        {
            var cod = new[] { MakeLicence(CodLicenceId, CodId) };
            var none = new FakeLicenses();
            var held = new FakeLicenses();
            held.Grant(CodLicenceId);

            Assert.IsTrue(UnlicensedCatch.MustRelease(CodId, cod, null, none), "gated, not held: back it goes");
            Assert.IsFalse(UnlicensedCatch.MustRelease(CodId, cod, null, held), "gated and held: kept");
            Assert.IsFalse(UnlicensedCatch.MustRelease("fish.herring", cod, null, none), "a species no licence lists is free");
            Assert.IsTrue(UnlicensedCatch.MustRelease(CodId, null, cod, none), "the catalog gates as well as the handed list");
            Assert.IsTrue(UnlicensedCatch.MustRelease(CodId, cod, null, null), "no wallet holds nothing (fail closed)");
            Assert.IsFalse(UnlicensedCatch.MustRelease(CodId, null, null, none), "no licence data: nothing is gated");
            Assert.IsFalse(UnlicensedCatch.MustRelease("", cod, cod, none), "no id: nothing known to release");
            Assert.IsFalse(UnlicensedCatch.MustRelease(null, cod, cod, none));
        }

        [Test]
        public void RequiredLicence_IsTheOneThatListsTheSpecies()
        {
            var handed = new[] { MakeLicence("license.mackerel", "fish.mackerel") };
            var catalog = new[] { MakeLicence(CodLicenceId, CodId) };
            Assert.AreEqual("license.mackerel", UnlicensedCatch.RequiredLicenceFor("fish.mackerel", handed, catalog));
            Assert.AreEqual(CodLicenceId, UnlicensedCatch.RequiredLicenceFor(CodId, handed, catalog));
            Assert.IsNull(UnlicensedCatch.RequiredLicenceFor("fish.herring", handed, catalog));
        }

        [Test]
        public void TheLiveCatalog_IsAskedOnlyWhenAWalletIsInstalled()
        {
            GameServices.Licenses = null;
            Assert.IsNull(UnlicensedCatch.LiveCatalog(), "no wallet (an EditMode rig, a bare scene): only handed lists gate");
            Assert.IsFalse(UnlicensedCatch.MustRelease(ClamId), "so a dig rig with no wallet keeps working as before");

            GameServices.Licenses = _wallet;
            IReadOnlyList<LicenseDef> shipped = UnlicensedCatch.LiveCatalog();
            Assert.IsNotNull(shipped, "with a wallet, the shipped licence catalog is the gate");
            Assert.AreEqual(ClamLicenceId, CatchLicensePolicy.RequiredLicenseFor(ClamId, shipped),
                "the shipped data gates the clam behind the shellfish licence");
            Assert.AreEqual(CodLicenceId, CatchLicensePolicy.RequiredLicenseFor(CodId, shipped),
                "and the cod behind the cod licence");
            Assert.IsTrue(UnlicensedCatch.MustRelease(ClamId), "an empty wallet lets the clam go");
            _wallet.Grant(ClamLicenceId);
            Assert.IsFalse(UnlicensedCatch.MustRelease(ClamId), "and the shellfish licence keeps it");
        }

        [Test]
        public void TheShippedLine_IsInTheLibrary_AndPutsTheNameIn()
        {
            var lib = Resources.Load<InnerVoiceLibrary>(InnerVoiceLibrary.ResourcesPath);
            Assert.IsNotNull(lib, "the director's library loads");
            InnerVoiceLineDef found = null;
            foreach (InnerVoiceLineDef line in lib.Lines)
                if (line != null && line.SubjectId == InnerVoiceCues.UnlicensedCatch) { found = line; break; }

            Assert.IsNotNull(found, "a line answers the unlicensed-catch cue");
            Assert.AreEqual(InnerVoiceTrigger.Cue, found.Trigger);
            Assert.IsFalse(found.OnceOnly, "he says it every time it happens");
            Assert.AreEqual("I don't have a licence for this Atlantic Cod yet.", found.TextFor("Atlantic Cod"));
            Assert.AreEqual("I don't have a licence for this Soft-shell Clam yet.", found.TextFor(ClamName));
        }

        [Test]
        public void TheDirector_SpeaksTheLine_WithTheSpeciesNamed()
        {
            var lib = Resources.Load<InnerVoiceLibrary>(InnerVoiceLibrary.ResourcesPath);
            var go = new GameObject("InnerVoiceDirector");
            _spawned.Add(go);
            var director = go.AddComponent<InnerVoiceDirector>();
            director.LoadLibrary(lib);

            director.HearCue(InnerVoiceCues.UnlicensedCatch, "Atlantic Cod");
            Assert.AreEqual("I don't have a licence for this Atlantic Cod yet.", director.LastSpokenText);
            director.HearCue(InnerVoiceCues.UnlicensedCatch, ClamName);
            Assert.AreEqual("I don't have a licence for this Soft-shell Clam yet.", director.LastSpokenText,
                "no cooldown: the next release says it again, naming the new species");
        }

        // ---- the rod --------------------------------------------------------------------------------

        [Test]
        public void Rod_Licensed_LandsAndRecords()
        {
            _wallet.Grant(CodLicenceId);
            GameServices.Licenses = _wallet;
            UnlicensedCatch.UseCatalog(null);
            var hold = new FakeHold();

            FishingController c = LandOne(hold, MakeRodFish(CodId, "Atlantic Cod"), new[] { MakeLicence(CodLicenceId, CodId) });

            Assert.AreEqual(FishingPhase.Landed, c.Phase);
            Assert.AreEqual(1, hold.UsedUnits);
            Assert.AreEqual(1, _caught.Count, "kept and recorded");
            Assert.AreEqual(0, _unlicensed.Count);
            Assert.AreEqual(0, _cues.Count);
        }

        [Test]
        public void Rod_Unlicensed_IsLetGo_WithHisLine_AndNothingRecorded()
        {
            GameServices.Licenses = _wallet;
            UnlicensedCatch.UseCatalog(null);
            var hold = new FakeHold();

            FishingController c = LandOne(hold, MakeRodFish(CodId, "Atlantic Cod"), new[] { MakeLicence(CodLicenceId, CodId) });

            Assert.AreEqual(FishingPhase.Landed, c.Phase, "the bite and the fight are kept; it comes to hand");
            Assert.AreEqual(0, hold.UsedUnits);
            AssertNothingRecorded("rod");
            Assert.AreEqual(1, _unlicensed.Count);
            Assert.AreEqual(CodLicenceId, _unlicensed[0].RequiredLicenceId);
            AssertSaidOnce("Atlantic Cod");
            Assert.Contains(JuiceMoment.Landing, _moments, "the landing beat plays, as for a protected release");
        }

        [Test]
        public void Rod_GatedByTheCatalogAlone_IsLetGo()
        {
            // A scene that was never handed the licence list still gates: the catalog is enough.
            GameServices.Licenses = _wallet;
            UnlicensedCatch.UseCatalog(new[] { MakeLicence(CodLicenceId, CodId) });
            var hold = new FakeHold();

            FishingController c = LandOne(hold, MakeRodFish(CodId, "Atlantic Cod"), handed: null);

            Assert.AreEqual(FishingPhase.Landed, c.Phase);
            Assert.AreEqual(0, hold.UsedUnits);
            AssertNothingRecorded("rod, catalog gate");
            AssertSaidOnce("Atlantic Cod");
        }

        [Test]
        public void Rod_ASpeciesInNoLicence_StillLands()
        {
            GameServices.Licenses = _wallet;   // holds nothing
            UnlicensedCatch.UseCatalog(new[] { MakeLicence(CodLicenceId, CodId) });
            var hold = new FakeHold();

            FishingController c = LandOne(hold, MakeRodFish("fish.herring", "Herring"), new[] { MakeLicence(CodLicenceId, CodId) });

            Assert.AreEqual(FishingPhase.Landed, c.Phase);
            Assert.AreEqual(1, hold.UsedUnits);
            Assert.AreEqual(1, _caught.Count);
            Assert.AreEqual(0, _cues.Count);
        }

        // ---- the clam dig ---------------------------------------------------------------------------

        private FishSpeciesDef MakeClam(string id = ClamId, string name = ClamName)
        {
            var f = ScriptableObject.CreateInstance<FishSpeciesDef>();
            f.Id = id; f.DisplayName = name;
            f.Category = FishCategory.Shellfish;
            f.MinWeightKg = 0.05f; f.MaxWeightKg = 0.2f; f.BaseValue = 2; f.SupplyElasticity = 0.45f;
            _spawned.Add(f);
            return f;
        }

        private ClamDig MakeDig(IHold bucket, FishSpeciesDef clam)
        {
            GameServices.TidalTerrain = new FlatTerrain { Elevation = 1.0f };
            GameServices.Environment = new FakeEnv { Level = 0.5f };   // low water: the flat is bared
            SaveData save = SaveMigration.NewGame();
            save.OwnedGear.Add("gear.shovel");
            GameServices.Save = new FakeSaveService(save);
            ToolInHand.Holding(ToolDef.ShovelId, _spawned);

            var go = new GameObject("ClamHole");
            _spawned.Add(go);
            var d = go.AddComponent<ClamDig>();
            d.Configure(clam, bucket, go.transform, "gear.shovel", 7);
            d.ConfigureInteract($"fixture.unlicensed_clam_hole#{_spawned.Count}", ToolDef.ShovelId, landInHand: false);
            return d;
        }

        [Test]
        public void Dig_Licensed_PutsTheClamInThePail()
        {
            _wallet.Grant(ClamLicenceId);
            GameServices.Licenses = _wallet;
            UnlicensedCatch.UseCatalog(new[] { MakeLicence(ClamLicenceId, ClamId) });
            var pail = new FakeHold();

            Assert.IsTrue(MakeDig(pail, MakeClam()).TryDig());

            Assert.AreEqual(1, pail.UsedUnits);
            Assert.AreEqual(1, _caught.Count, "kept and recorded");
            Assert.AreEqual(0, _cues.Count);
        }

        [Test]
        public void Dig_Unlicensed_IsAllowed_ButTheClamGoesBack_WithHisLine()
        {
            GameServices.Licenses = _wallet;   // holds nothing
            UnlicensedCatch.UseCatalog(new[] { MakeLicence(ClamLicenceId, ClamId) });
            var pail = new FakeHold();
            ClamDig dig = MakeDig(pail, MakeClam());

            Assert.IsFalse(dig.TryDig(), "nothing was kept from this dig");

            Assert.AreEqual(0, pail.UsedUnits, "nothing enters the pail");
            AssertNothingRecorded("dig");
            Assert.AreEqual(1, _unlicensed.Count);
            Assert.AreEqual(ClamId, _unlicensed[0].Item.SpeciesId);
            Assert.AreEqual(ClamLicenceId, _unlicensed[0].RequiredLicenceId);
            AssertSaidOnce(ClamName);
            Assert.Contains(JuiceMoment.DigStrike, _moments, "the dig itself happened: the strike plays");
        }

        [Test]
        public void Dig_ASpeciesInNoLicence_StillLands()
        {
            GameServices.Licenses = _wallet;
            UnlicensedCatch.UseCatalog(new[] { MakeLicence(ClamLicenceId, ClamId) });
            var pail = new FakeHold();

            Assert.IsTrue(MakeDig(pail, MakeClam("fish.test_free_clam", "Free Clam")).TryDig());

            Assert.AreEqual(1, pail.UsedUnits);
            Assert.AreEqual(1, _caught.Count);
            Assert.AreEqual(0, _cues.Count);
        }

        // ---- the trap haul (instant) ----------------------------------------------------------------

        private FishSpeciesDef MakePotSpecies(string id, string name)
        {
            var f = ScriptableObject.CreateInstance<FishSpeciesDef>();
            f.Id = id; f.DisplayName = name; f.Category = FishCategory.Shellfish;
            f.RegionIds = new[] { Region };
            f.AllowedGear = Gear.Trap; f.Seasons = SeasonMask.AllYear;
            f.MinTide = -10f; f.MaxTide = 10f; f.StartHour = 0f; f.EndHour = 24f;
            f.MinWeightKg = 0.5f; f.MaxWeightKg = 1.5f; f.BaseValue = 12; f.SupplyElasticity = 0.2f;
            f.SpawnWeight = 1f;
            _spawned.Add(f);
            return f;
        }

        private PlacedTrap MakeSoakedTrap()
        {
            const double placeTime = 5000.0;
            FishSpeciesRegistry.Register(MakePotSpecies(GatedPotId, GatedPotName));

            var t = ScriptableObject.CreateInstance<TrapDef>();
            t.Id = "trap.test_unlicensed"; t.DisplayName = "Test Pot";
            t.AllowedCatchFishIds = new[] { GatedPotId };
            t.SoakHours = 12f; t.CapacityUnits = 4; t.HoursToFullPot = 36f;
            t.MinSoakDepthMeters = 3f; t.MaxSoakDepthMeters = 40f;
            _spawned.Add(t);

            var clock = new FakeClock { Seconds = placeTime };
            GameServices.Clock = clock;
            GameServices.Environment = new FakeEnv { Level = 2f };

            var go = new GameObject("PlacedTrap");
            _spawned.Add(go);
            var trap = go.AddComponent<PlacedTrap>();
            trap.Configure(t, null, "trap.test_unlicensed#1", Region, placeTime, 4242);
            clock.Seconds = placeTime + 36.0 * 75.0 * 4.0;   // long past the soak and the full-pot mark
            return trap;
        }

        private static CatchContext TrapContext() => new CatchContext(Region, 2f, 12f, Season.HighSummer, Gear.Trap);

        [Test]
        public void TrapHaul_Licensed_LandsEveryAnimal()
        {
            _wallet.Grant(PotLicenceId);
            GameServices.Licenses = _wallet;
            UnlicensedCatch.UseCatalog(new[] { MakeLicence(PotLicenceId, GatedPotId) });
            PlacedTrap trap = MakeSoakedTrap();
            var hold = new FakeHold();

            Assert.IsTrue(trap.TryHaul(hold, TrapContext()));

            Assert.Greater(hold.UsedUnits, 0, "the soaked pot comes up with something");
            Assert.AreEqual(hold.UsedUnits, _caught.Count, "each animal kept is recorded");
            Assert.AreEqual(0, _cues.Count);
        }

        [Test]
        public void TrapHaul_Unlicensed_IsAllowed_EveryAnimalGoesBack_OneLine()
        {
            GameServices.Licenses = _wallet;
            UnlicensedCatch.UseCatalog(new[] { MakeLicence(PotLicenceId, GatedPotId) });
            PlacedTrap trap = MakeSoakedTrap();
            var hold = new FakeHold();

            Assert.IsTrue(trap.TryHaul(hold, TrapContext()), "hauling is allowed");

            Assert.AreEqual(0, hold.UsedUnits);
            AssertNothingRecorded("trap haul");
            Assert.Greater(_unlicensed.Count, 0, "each animal is let go through the shared rule");
            AssertSaidOnce(GatedPotName);
        }

        [Test]
        public void TrapHaul_UnlicensedAndProtected_TheLicenceWins_NothingDocumented()
        {
            GameServices.Licenses = _wallet;
            UnlicensedCatch.UseCatalog(new[] { MakeLicence(PotLicenceId, GatedPotId) });
            PlacedTrap trap = MakeSoakedTrap();
            GameServices.FishConservation = new FakeConservation(GatedPotId);   // after the registry installs its own facts
            var hold = new FakeHold();

            Assert.IsTrue(trap.TryHaul(hold, TrapContext()));

            AssertNothingRecorded("unlicensed protected haul");
            AssertSaidOnce(GatedPotName);
        }

        [Test]
        public void TrapHaul_Protected_ButLicensed_IsStillDocumented()
        {
            _wallet.Grant(PotLicenceId);
            GameServices.Licenses = _wallet;
            UnlicensedCatch.UseCatalog(new[] { MakeLicence(PotLicenceId, GatedPotId) });
            PlacedTrap trap = MakeSoakedTrap();
            GameServices.FishConservation = new FakeConservation(GatedPotId);   // after the registry installs its own facts
            var hold = new FakeHold();

            Assert.IsTrue(trap.TryHaul(hold, TrapContext()));

            Assert.AreEqual(0, hold.UsedUnits);
            Assert.Greater(_released.Count, 0, "the protected rule is unchanged when the licence is held");
            Assert.AreEqual(0, _unlicensed.Count);
            Assert.AreEqual(0, _cues.Count);
        }

        // ---- the pot on the deck --------------------------------------------------------------------

        private static CatchItem PotItem(string id, string name)
            => new CatchItem(id, name, FishCategory.Shellfish, 0.9f, 12, 0.2f, 0f, Freshness.Landed(0d));

        [Test]
        public void DeckPot_AnUnlicensedAnimal_IsBornReturned_AndNeverStowedOrDocumented()
        {
            GameServices.Licenses = _wallet;
            GameServices.FishConservation = new FakeConservation(GatedPotId);   // and protected: licence wins
            UnlicensedCatch.UseCatalog(new[] { MakeLicence(PotLicenceId, GatedPotId) });

            var pot = new DeckPot(null, null, "trap-unlicensed", 0d, 7,
                                  new[] { PotItem(GatedPotId, GatedPotName), PotItem(OpenPotId, "Lobster") });

            Assert.IsTrue(pot.Animals[0].Unlicensed);
            Assert.IsFalse(pot.Animals[0].Keeper);
            Assert.AreEqual(DeckAnimalFate.Returned, pot.Animals[0].Fate, "it went back at the haul, before deck work");
            Assert.IsFalse(pot.Animals[1].Unlicensed, "a species no licence lists is untouched");
            Assert.IsTrue(pot.Animals[1].Keeper);

            var hold = new FakeHold();
            var released = new List<CatchItem>();
            pot.AutoResolve(hold, null, released, out int kept, out _, out _);

            Assert.AreEqual(1, kept);
            Assert.AreEqual(OpenPotId, hold.Items[0].SpeciesId);
            Assert.AreEqual(0, released.Count, "the auto-sort never documents the unlicensed animal");
        }

        [Test]
        public void DeckPot_Licensed_IsAnOrdinaryKeeper()
        {
            _wallet.Grant(PotLicenceId);
            GameServices.Licenses = _wallet;
            UnlicensedCatch.UseCatalog(new[] { MakeLicence(PotLicenceId, GatedPotId) });

            var pot = new DeckPot(null, null, "trap-licensed", 0d, 7, new[] { PotItem(GatedPotId, GatedPotName) });

            Assert.IsFalse(pot.Animals[0].Unlicensed);
            Assert.IsTrue(pot.Animals[0].Keeper);
            Assert.AreEqual(DeckAnimalFate.InPot, pot.Animals[0].Fate);
        }
    }
}
