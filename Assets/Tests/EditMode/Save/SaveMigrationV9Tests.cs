using System.Text.RegularExpressions;
using NUnit.Framework;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// SaveData v8 → v9 (ADR 0025 S3 — the fish finder's vertical RANGE joins the per-hull sounder
    /// preferences).
    ///
    /// <para><b>The load-bearing assertion is that the step HEALS rather than merely DEFAULTS.</b> A v8
    /// row read under the v9 struct arrives with <c>RangeMetres = 0</c> — JsonUtility zero-fills an absent
    /// value field — and zero is not a small range: every fish mark and the bottom contour are drawn at
    /// <c>depth / range</c>, so it is Inf/NaN and a garbage picture on exactly the hull whose glass the
    /// player had already touched. Defaulting the field on NEW rows would leave every one of those rows
    /// broken. The tests below therefore never assert "the field exists"; they assert an EXISTING zero row
    /// comes out positive, and that a dialled range is not trampled on the way.</para>
    ///
    /// <para><b>Negative-controlled</b> two ways: the arithmetic the heal prevents is pinned directly
    /// (<see cref="NegativeControl_AnUnhealedZeroRange_IsInfinity_NotASmallPicture"/>), and the JSON the
    /// round-trip test loads is proved to be genuinely v8-shaped (the key is absent) before it is trusted —
    /// so the test cannot pass by accidentally exercising an already-v9 blob.</para>
    /// </summary>
    public class SaveMigrationV9Tests
    {
        private const string Skiff = "boat.skiff";
        private const string Punt = "boat.punt";

        /// <summary>The shipped default the heal repairs to. Read from the settings block, not retyped —
        /// rule 6, and it is the same value a freshly fitted finder starts at.</summary>
        private static float DefaultRange => FishFinderSettings.Default.DefaultRangeMetres;

        /// <summary>A v8 save whose glass has been touched on one hull: the row exists, carries a dialled
        /// alarm and display toggles, and has NO range — the exact shape a v8 file deserializes into under
        /// the v9 struct.</summary>
        private static SaveData V8BlobWithATouchedGlass()
        {
            var d = new SaveData
            {
                SchemaVersion = 8,
                WorldSeed = 4242,
                GameTimeSeconds = 9001.5,
                Money = 175,
                ActiveHullId = Skiff,
            };
            d.OwnedBoats.Add("boat.dory");
            d.OwnedBoats.Add(Skiff);
            d.HullInstruments.Add(new HullInstrument(Skiff, "instrument.fish_finder"));
            // ⚠ Field initialiser syntax on purpose: `new SounderPrefsDto(hull, prefs)` would carry the v9
            // default in and there would be nothing left to heal. This is the v8 row.
            d.HullSounderPrefs.Add(new SounderPrefsDto
            {
                HullId = Skiff,
                AlarmMetres = 4.5f,
                Armed = true,
                Feet = true,
                Night = false,
                // RangeMetres deliberately left at its zero default.
            });
            return d;
        }

        private static SounderPrefsDto RowFor(SaveData d, string hullId)
        {
            for (int i = 0; i < d.HullSounderPrefs.Count; i++)
                if (d.HullSounderPrefs[i].HullId == hullId) return d.HullSounderPrefs[i];
            Assert.Fail($"no sounder-prefs row for {hullId}");
            return default;
        }

        // ---- the heal ------------------------------------------------------------------------------

        [Test]
        public void V8_MigratesToCurrent_AndHealsTheZeroRange_OnTheRowThatAlreadyExisted()
        {
            SaveData d = SaveMigration.Migrate(V8BlobWithATouchedGlass());

            // VERSION-AGNOSTIC on the counter (the SaveMigrationV3Tests rule): what this test owns is the
            // v9 CONTRACT, not the value of a counter every later lane bumps.
            Assert.AreEqual(SaveMigration.CurrentVersion, d.SchemaVersion,
                "a v8 blob climbs the whole chain to the current schema");
            Assert.GreaterOrEqual(d.SchemaVersion, 9, "and v9's finder range exists from that climb");

            SounderPrefsDto row = RowFor(d, Skiff);
            Assert.Greater(row.RangeMetres, 0f,
                "THE point of the step: the pre-existing row's range must come out POSITIVE. A zero here " +
                "is a divide-by-zero in depth/range, not a small picture.");
            Assert.AreEqual(DefaultRange, row.RangeMetres, 1e-6f, "healed to the shipped default");

            // …and the step reinterprets nothing else.
            Assert.AreEqual(4.5f, row.AlarmMetres, 1e-6f, "the set-point the fisherman dialled is untouched");
            Assert.IsTrue(row.Armed);
            Assert.IsTrue(row.Feet);
            Assert.IsFalse(row.Night);
            Assert.AreEqual(4242, d.WorldSeed);
            Assert.AreEqual(9001.5, d.GameTimeSeconds, 1e-9);
            Assert.AreEqual(175, d.Money);
            Assert.AreEqual(Skiff, d.ActiveHullId);
            CollectionAssert.AreEqual(new[] { "boat.dory", Skiff }, d.OwnedBoats);
            Assert.IsTrue(InstrumentLocker.Owns(d, Skiff, "instrument.fish_finder"),
                "and the v8 ownership rows survive the bump untouched (ADR 0030's shape is unchanged)");
        }

        [Test]
        public void TheHealedRange_MakesTheContourFraction_Finite()
        {
            // The defect stated as arithmetic: this is the expression the finder draws with
            // (fishRig.js:239 — clamp(depth/range, …)). It must be a real number for every stored row.
            SaveData d = SaveMigration.Migrate(V8BlobWithATouchedGlass());
            SounderPrefs prefs = InstrumentLocker.PrefsFor(d, Skiff,
                SounderPrefs.FromDefaults(DepthSounderSettings.Default));

            float depthFrac = 6.2f / prefs.RangeMetres;
            Assert.IsFalse(float.IsInfinity(depthFrac), "depth/range must not be infinite");
            Assert.IsFalse(float.IsNaN(depthFrac), "depth/range must not be NaN");
            Assert.Greater(depthFrac, 0f);
        }

        [Test]
        public void ADialledRange_IsNeverTrampledByTheHeal()
        {
            SaveData d = V8BlobWithATouchedGlass();
            d.HullSounderPrefs.Add(new SounderPrefsDto
            {
                HullId = Punt, AlarmMetres = 3f, Armed = true, Feet = false, Night = true,
                RangeMetres = 40f,        // a player who already stepped the RANGE pushers up
            });

            SaveData migrated = SaveMigration.Migrate(d);
            Assert.AreEqual(40f, RowFor(migrated, Punt).RangeMetres, 1e-6f,
                "the heal repairs a non-positive range and touches nothing else");
            Assert.AreEqual(DefaultRange, RowFor(migrated, Skiff).RangeMetres, 1e-6f,
                "…while the broken row beside it is still fixed");
        }

        [Test]
        public void ANegativeRange_IsAlsoHealed_NotJustZero()
        {
            // A hand-edited file, or a sign slip in a future RANGE-step routine. `<= 0` is the rule, not
            // `== 0`: a negative range flips the whole picture upside down instead of crashing, which is
            // the worse failure because nothing throws.
            var d = new SaveData { SchemaVersion = 8 };
            d.HullSounderPrefs.Add(new SounderPrefsDto { HullId = Skiff, RangeMetres = -12f });

            Assert.AreEqual(DefaultRange, RowFor(SaveMigration.Migrate(d), Skiff).RangeMetres, 1e-6f);
        }

        [Test]
        public void AnOlderBlob_ClimbsTheWholeChain_AndStillLandsWithAUsableRange()
        {
            var v2 = new SaveData { SchemaVersion = 2, Money = 10, ActiveHullId = "boat.dory" };
            v2.OwnedBoats.Add("boat.dory");
            SaveData d = SaveMigration.Migrate(v2);

            Assert.AreEqual(SaveMigration.CurrentVersion, d.SchemaVersion);
            Assert.IsEmpty(d.HullSounderPrefs, "a save from before the glass existed stores no preferences");
            Assert.Greater(InstrumentLocker.PrefsFor(d, "boat.dory",
                               SounderPrefs.FromDefaults(DepthSounderSettings.Default)).RangeMetres, 0f,
                "and an untouched hull falls back to defaults that are themselves positive");
            Assert.AreEqual(10, d.Money);
        }

        // ---- the real serialization path ------------------------------------------------------------

        [Test]
        public void AV8JsonWithNoRangeKey_LoadsThroughTheRealPath_AndComesOutHealed()
        {
            // Build a genuinely v8-shaped JSON: serialize, then STRIP the key v8 never had. (The real
            // writer pretty-prints, so the comma and the key are separated by a newline + indent.)
            string v9Json = SaveSerialization.ToJson(V8BlobWithATouchedGlass());
            string v8Json = Regex.Replace(v9Json, ",\\s*\"RangeMetres\"\\s*:\\s*[^,\\s}]*", "");
            Assert.IsFalse(v8Json.Contains("RangeMetres"),
                "the strip must actually have worked, or this test is quietly loading a v9 blob and " +
                "proving nothing");

            // The DEFECT, straight from the deserializer — deliberately NOT through
            // SaveSerialization.FromJson, which already runs Migrate for the caller (that is the whole
            // load path). This is the only place the un-migrated value is observable.
            SaveData raw = UnityEngine.JsonUtility.FromJson<SaveData>(v8Json);
            Assert.AreEqual(0f, RowFor(raw, Skiff).RangeMetres, 0f,
                "an absent JSON key deserializes to ZERO — this IS the defect the step exists to heal");
            Assert.AreEqual(8, raw.SchemaVersion, "and it really is a v8 blob at this point");

            // …and the REAL load path repairs it, migration included, in one call.
            SaveData d = SaveSerialization.FromJson(v8Json);
            Assert.IsNotNull(d);
            Assert.AreEqual(SaveMigration.CurrentVersion, d.SchemaVersion);
            Assert.AreEqual(DefaultRange, RowFor(d, Skiff).RangeMetres, 1e-6f,
                "the range a v8 file never had comes back positive off the real loader");
            Assert.AreEqual(4.5f, RowFor(d, Skiff).AlarmMetres, 1e-6f, "with the rest of the row intact");
        }

        [Test]
        public void ARangeTheFishermanSets_SurvivesAWriteAndAReload()
        {
            SaveData d = SaveMigration.NewGame();
            var defaults = SounderPrefs.FromDefaults(DepthSounderSettings.Default);

            var dialled = new SounderPrefs(4.5f, true, false, false, 40f);
            Assert.IsTrue(InstrumentLocker.SetPrefs(d, Skiff, in dialled));

            SaveData reloaded = SaveMigration.Migrate(SaveSerialization.FromJson(SaveSerialization.ToJson(d)));
            Assert.AreEqual(40f, InstrumentLocker.PrefsFor(reloaded, Skiff, in defaults).RangeMetres, 1e-6f,
                "the scale a fisherman chose is still there tomorrow");
        }

        [Test]
        public void ChangingOnlyTheRange_CountsAsAChange_SoItActuallyPersists()
        {
            // The InstrumentLocker's no-op guard compares every field. If RangeMetres were left out of
            // that comparison the RANGE pushers would move the glass and never the save — a silent,
            // reload-only failure.
            SaveData d = SaveMigration.NewGame();
            var a = new SounderPrefs(3f, true, false, false, 20f);
            var b = new SounderPrefs(3f, true, false, false, 40f);

            Assert.IsTrue(InstrumentLocker.SetPrefs(d, Skiff, in a));
            Assert.IsFalse(InstrumentLocker.SetPrefs(d, Skiff, in a), "an identical write is still a no-op");
            Assert.IsTrue(InstrumentLocker.SetPrefs(d, Skiff, in b),
                "but a range-only change must be recorded");
            Assert.AreEqual(40f, RowFor(d, Skiff).RangeMetres, 1e-6f);
        }

        // ---- defaults ------------------------------------------------------------------------------

        [Test]
        public void EveryWayToMakePrefs_ProducesAPositiveRange()
        {
            // There must be NO constructor, default or fallback anywhere that hands out a zero range —
            // that is what makes the migration the only place a zero can ever come from.
            Assert.Greater(FishFinderSettings.Default.DefaultRangeMetres, 0f, "the settings default");
            Assert.Greater(SounderPrefs.FromDefaults(DepthSounderSettings.Default).RangeMetres, 0f,
                "the one-settings overload S2 already calls");
            Assert.Greater(SounderPrefs.FromDefaults(DepthSounderSettings.Default,
                                                     FishFinderSettings.Default).RangeMetres, 0f,
                "the two-settings overload");
            Assert.Greater(new SounderPrefs(3f, true, false, false).RangeMetres, 0f,
                "and the v8-shaped four-argument constructor, which S2's call sites still use");
        }

        [Test]
        public void ANewGame_StartsAtTheCurrentVersion_WithNoStalePrefs()
        {
            Assert.GreaterOrEqual(SaveMigration.CurrentVersion, 9,
                "v9 (the finder's range) has shipped, and the counter only ever goes up");
            SaveData fresh = SaveMigration.NewGame();
            Assert.AreEqual(SaveMigration.CurrentVersion, fresh.SchemaVersion);
            Assert.IsNotNull(fresh.HullSounderPrefs);
            Assert.IsEmpty(fresh.HullSounderPrefs);
        }

        [Test]
        public void TheSaveStillNeverCarriesASchool_NorADepth()
        {
            // Rule 5 for S3, stated the way SaveMigrationV8Tests states it for the sounding: schools are
            // recomputed from (worldSeed, gameTime) + place + weather like the tide is. If a field naming
            // one ever appears on the save, this assertion is the thing that has to be argued with.
            foreach (var f in typeof(SaveData).GetFields())
            {
                Assert.That(f.Name, Does.Not.Contain("School"),
                    $"SaveData.{f.Name} — fish schools are recomputed, never stored (rule 5).");
                Assert.That(f.Name, Does.Not.Contain("Depth"), $"SaveData.{f.Name} — likewise a sounding.");
            }
            foreach (var f in typeof(SounderPrefsDto).GetFields())
            {
                Assert.That(f.Name, Does.Not.Contain("School"),
                    $"SounderPrefsDto.{f.Name} — the glass persists PREFERENCES, never what it saw.");
                Assert.That(f.Name, Does.Not.Contain("Depth"),
                    $"SounderPrefsDto.{f.Name} — RangeMetres is a display SCALE, not a reading.");
            }
        }

        // ---- NEGATIVE CONTROL: prove the heal is load-bearing ---------------------------------------

        [Test]
        public void NegativeControl_AnUnhealedZeroRange_IsInfinity_NotASmallPicture()
        {
            // Pin the MECHANISM, not the symptom: this is precisely what the finder would compute for a
            // v8 row if the migration only defaulted new rows instead of healing old ones. If this ever
            // stops being infinite the heal has stopped being necessary — and the tests above would be
            // pinning nothing.
            const float unhealed = 0f;
            float depthFrac = 6.2f / unhealed;
            Assert.IsTrue(float.IsInfinity(depthFrac),
                "an unhealed zero range makes depth/range infinite — the garbage contour this step exists " +
                "to prevent");
            Assert.IsTrue(float.IsNaN(0f / unhealed),
                "and at the surface it is NaN, which draws nothing at all");
        }
    }
}
