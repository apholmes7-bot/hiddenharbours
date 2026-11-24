using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.Core;
using HiddenHarbours.Fishing;
using HiddenHarbours.Player;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// Owner ruling 2026-11-23 in a running game: a rod landing and a clam dig, each with and without the
    /// licence, against the SHIPPED licence catalog and the SHIPPED inner-voice line, with a live
    /// <see cref="InnerVoiceDirector"/> (awake, enabled, subscribed) turning the Core cue into his spoken
    /// line. Fishing and World meet only on the bus.
    ///
    /// <para>The licence wallet is the test's own (the live one is put back after), so "licensed" and
    /// "unlicensed" are the test's choice, not whatever the run's save happens to hold.</para>
    /// </summary>
    public class UnlicensedCatchReleasePlayTests
    {
        private const string Region = "region.st_peters";
        private const string CodId = "fish.atlantic_cod";
        private const string CodName = "Atlantic Cod";
        private const string CodLicenceId = "license.cod";
        private const string ClamId = "fish.soft_shell_clam";
        private const string ClamName = "Soft-shell Clam";
        private const string ClamLicenceId = "license.clam";
        private const string Shovel = "gear.shovel";

        private sealed class FakeHold : IHold
        {
            private readonly List<CatchItem> _items = new();
            public int CapacityUnits => 20;
            public int UsedUnits => _items.Count;
            public IReadOnlyList<CatchItem> Items => _items;
            public bool TryAdd(CatchItem item) { _items.Add(item); return true; }
            public void Clear() => _items.Clear();
        }

        private sealed class FakeLicenses : ILicenseService
        {
            private readonly HashSet<string> _held = new();
            public bool IsLicensed(string licenseId) => string.IsNullOrEmpty(licenseId) || _held.Contains(licenseId);
            public void Grant(string licenseId) { if (!string.IsNullOrEmpty(licenseId)) _held.Add(licenseId); }
            public int Count => _held.Count;
        }

        private sealed class FlatTerrain : ITidalTerrain
        {
            public float ElevationAt(Vector2 worldPos) => 1f;
        }

        private sealed class LowWater : IEnvironmentService
        {
            public int WorldSeed => 0;
            public TideProfile ActiveTideProfile { get; set; }
            public EnvironmentSample Sample() => default;
            public float TideHeightAt(double totalSeconds) => 0.5f;
            public float WaterLevelAt(double totalSeconds) => 0.5f;
        }

        private sealed class FakeSaveService : ISaveService
        {
            public FakeSaveService(SaveData data) { Current = data; }
            public SaveData Current { get; }
            public bool GetFlag(string key) => false;
            public void SetFlag(string key, bool value) { }
            public void Save() { }
        }

        private readonly List<Object> _spawned = new();
        private readonly List<string> _spokenLines = new();
        private int _caught;
        private int _released;
        private int _unlicensed;

        private ILicenseService _prevLicences;
        private ISaveService _prevSave;
        private IEnvironmentService _prevEnv;
        private ITidalTerrain _prevTerrain;
        private InnerVoiceDirector _director;

        private void OnSpeech(AmbientSpeechRequested e) { if (e.Kind == AmbientSpeechKind.InnerVoice) _spokenLines.Add(e.Line); }
        private void OnCaught(FishCaught e) => _caught++;
        private void OnReleased(CatchReleased e) => _released++;
        private void OnUnlicensed(UnlicensedCatchReleased e) => _unlicensed++;

        [SetUp]
        public void SetUp()
        {
            _prevLicences = GameServices.Licenses;
            _prevSave = GameServices.Save;
            _prevEnv = GameServices.Environment;
            _prevTerrain = GameServices.TidalTerrain;
            InteractionGate.Reset();
            UnlicensedCatch.ResetCatalog();   // the shipped catalog: the real data is under test
            _spokenLines.Clear();
            _caught = 0; _released = 0; _unlicensed = 0;
            EventBus.Subscribe<AmbientSpeechRequested>(OnSpeech);
            EventBus.Subscribe<FishCaught>(OnCaught);
            EventBus.Subscribe<CatchReleased>(OnReleased);
            EventBus.Subscribe<UnlicensedCatchReleased>(OnUnlicensed);

            // A director of the test's own: Awake loads the shipped library, OnEnable subscribes. (The
            // boot-installed one may speak too; the assertions read this one and the bus.)
            var go = new GameObject("InnerVoiceDirector(test)");
            _spawned.Add(go);
            _director = go.AddComponent<InnerVoiceDirector>();
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Unsubscribe<AmbientSpeechRequested>(OnSpeech);
            EventBus.Unsubscribe<FishCaught>(OnCaught);
            EventBus.Unsubscribe<CatchReleased>(OnReleased);
            EventBus.Unsubscribe<UnlicensedCatchReleased>(OnUnlicensed);
            foreach (var o in _spawned) if (o != null) Object.Destroy(o);
            _spawned.Clear();
            GameServices.Licenses = _prevLicences;
            GameServices.Save = _prevSave;
            GameServices.Environment = _prevEnv;
            GameServices.TidalTerrain = _prevTerrain;
            UnlicensedCatch.ResetCatalog();
        }

        private static string LineFor(string name) => $"I don't have a licence for this {name} yet.";

        private FakeLicenses Wallet(params string[] held)
        {
            var w = new FakeLicenses();
            foreach (string id in held) w.Grant(id);
            GameServices.Licenses = w;
            return w;
        }

        // ---- the rod --------------------------------------------------------------------------------

        private FishSpeciesDef Cod()
        {
            var f = ScriptableObject.CreateInstance<FishSpeciesDef>();
            f.Id = CodId; f.DisplayName = CodName; f.Category = FishCategory.InshoreGroundfish;
            f.RegionIds = new[] { Region };
            f.AllowedGear = Gear.Handline | Gear.Longline;
            f.Seasons = SeasonMask.AllYear;
            f.MinTide = -10f; f.MaxTide = 10f; f.StartHour = 0f; f.EndHour = 24f;
            f.MinWeightKg = 1f; f.MaxWeightKg = 6f; f.BaseValue = 12; f.SupplyElasticity = 0.2f; f.SpawnWeight = 1f;
            _spawned.Add(f);
            return f;
        }

        /// <summary>Cast, hook and fight a cod to the result, all inside one frame (so the component's own
        /// Update never interleaves), then switch the rod off before the next frame.</summary>
        private FishingController FightOneCod(IHold hold)
        {
            var go = new GameObject("Rod(test)");
            _spawned.Add(go);
            var c = go.AddComponent<FishingController>();
            c.Configure(hold, new[] { Cod() }, Region, Gear.Handline, 999);   // no handed licences: the catalog gates

            Vector2 a = c.transform.position;
            c.Tick(0.02f, true,  a + new Vector2(0f, -0.5f), true);
            c.Tick(0.02f, true,  a + new Vector2(0f, -1.0f), true);
            c.Tick(0.02f, true,  a + new Vector2(0f, -1.5f), true);
            c.Tick(0.02f, true,  a + new Vector2(0f, -0.5f), true);
            c.Tick(0.02f, true,  a + new Vector2(0f,  0.3f), true);
            c.Tick(0.02f, true,  a + new Vector2(0f,  1.0f), true);
            c.Tick(0.02f, false, a + new Vector2(0f,  1.0f), true);
            float t = 0f;
            while (c.Phase == FishingPhase.Cast && t < 5f) { c.Tick(0.05f, false); t += 0.05f; }
            t = 0f;
            while (c.Phase != FishingPhase.Fighting && c.Phase != FishingPhase.Tending && t < 30f)
            { c.Tick(0.05f, false); t += 0.05f; }
            t = 0f;
            while (c.Phase != FishingPhase.Landed && c.Phase != FishingPhase.Snapped
                   && c.Phase != FishingPhase.NoBite && c.Phase != FishingPhase.Idle && t < 120f)
            { c.Tick(0.05f, actionHeld: c.State.Tension01 < 0.5f); t += 0.05f; }
            c.enabled = false;
            return c;
        }

        [UnityTest]
        public IEnumerator Rod_Licensed_LandsTheCod_AndSaysNothing()
        {
            Wallet(CodLicenceId);
            yield return null;
            var hold = new FakeHold();

            FishingController c = FightOneCod(hold);
            yield return null;

            Assert.AreEqual(FishingPhase.Landed, c.Phase);
            Assert.AreEqual(1, hold.UsedUnits, "the cod is kept");
            Assert.AreEqual(1, _caught);
            Assert.AreEqual(0, _unlicensed);
            Assert.IsFalse(_spokenLines.Contains(LineFor(CodName)), "no licence line for a licensed catch");
        }

        [UnityTest]
        public IEnumerator Rod_Unlicensed_HeLetsTheCodGo_AndSaysSo()
        {
            Wallet();   // holds nothing
            yield return null;
            var hold = new FakeHold();

            FishingController c = FightOneCod(hold);
            yield return null;

            Assert.AreEqual(FishingPhase.Landed, c.Phase, "the fight is won; it is the release presentation");
            Assert.AreEqual(0, hold.UsedUnits, "nothing is kept");
            Assert.AreEqual(0, _caught, "no FishCaught");
            Assert.AreEqual(0, _released, "no CatchReleased: no Notebook record");
            Assert.AreEqual(1, _unlicensed);
            Assert.AreEqual(LineFor(CodName), _director.LastSpokenText, "the live director says the shipped line, named");
            Assert.Contains(LineFor(CodName), _spokenLines, "and it went out on the bus as his inner voice");
        }

        // ---- the dig --------------------------------------------------------------------------------

        private ClamDig MakeDig(IHold pail)
        {
            GameServices.TidalTerrain = new FlatTerrain();
            GameServices.Environment = new LowWater();
            SaveData save = SaveMigration.NewGame();
            save.OwnedGear.Add(Shovel);
            GameServices.Save = new FakeSaveService(save);

            // A real shovel in real hands (the tools-in-hand ruling); CarryHands publishes itself on enable.
            var handsGo = new GameObject("Player(hands, test)");
            _spawned.Add(handsGo);
            handsGo.AddComponent<SpriteRenderer>();
            var hands = handsGo.AddComponent<CarryHands>();
            GameServices.Hands = hands;
            GameServices.CatchHands = hands;
            var def = ScriptableObject.CreateInstance<ToolDef>();
            def.Id = ToolDef.ShovelId; def.DisplayName = "Shovel";
            _spawned.Add(def);
            var toolGo = new GameObject("Shovel(test)");
            _spawned.Add(toolGo);
            var tool = toolGo.AddComponent<CarriableTool>();
            tool.Configure(def, toolGo.AddComponent<SpriteRenderer>(), "gear.shovel#playtest");
            Assert.AreEqual(CarryRefusal.None, hands.TryPickUp(tool), "the shovel is in his hands");

            var clam = ScriptableObject.CreateInstance<FishSpeciesDef>();
            clam.Id = ClamId; clam.DisplayName = ClamName; clam.Category = FishCategory.Shellfish;
            clam.MinWeightKg = 0.05f; clam.MaxWeightKg = 0.2f; clam.BaseValue = 2; clam.SupplyElasticity = 0.45f;
            _spawned.Add(clam);

            var go = new GameObject("ClamHole(test)");
            _spawned.Add(go);
            var d = go.AddComponent<ClamDig>();
            d.Configure(clam, pail, go.transform, Shovel, 7);
            d.ConfigureInteract("fixture.unlicensed_playtest_hole", ToolDef.ShovelId, landInHand: false);
            return d;
        }

        [UnityTest]
        public IEnumerator Dig_Licensed_TheClamGoesInThePail()
        {
            Wallet(ClamLicenceId);
            var pail = new FakeHold();
            ClamDig dig = MakeDig(pail);
            yield return null;

            Assert.IsTrue(dig.TryDig());
            yield return null;

            Assert.AreEqual(1, pail.UsedUnits);
            Assert.AreEqual(1, _caught);
            Assert.AreEqual(0, _unlicensed);
            Assert.IsFalse(_spokenLines.Contains(LineFor(ClamName)));
        }

        [UnityTest]
        public IEnumerator Dig_Unlicensed_HeDigsIt_PutsItBack_AndSaysSo()
        {
            Wallet();
            var pail = new FakeHold();
            ClamDig dig = MakeDig(pail);
            yield return null;

            Assert.IsFalse(dig.TryDig(), "dug, but not kept");
            yield return null;

            Assert.AreEqual(0, pail.UsedUnits, "nothing in the pail");
            Assert.AreEqual(0, _caught);
            Assert.AreEqual(0, _released, "no Notebook record");
            Assert.AreEqual(1, _unlicensed);
            Assert.AreEqual(LineFor(ClamName), _director.LastSpokenText);
            Assert.Contains(LineFor(ClamName), _spokenLines);
        }
    }
}
