using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using HiddenHarbours.Art.Editor;
using HiddenHarbours.Tools.RigBaking;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// Guards the M1 VILLAGE BUILDING SET — <see cref="VillageBuildingKit.M1Set"/> — and the bake request
    /// each of its builds turns into.
    ///
    /// <para>These are the tests that matter most about this table, because <b>its failure mode is
    /// silent</b>: both building rigs resolve options as <c>opts[k] != null ? opts[k] : fallback</c>, so a
    /// misspelled KEY is accepted without a word and renders something else. The recorded worked example is
    /// <c>winD</c> (the rig's internal field) versus <c>winDensity</c> (the option it actually reads); the
    /// famous one is <c>{preset:'netShed'}</c>, which no building rig reads at all. So every check below
    /// resolves against the RIG SOURCE or against <see cref="BuildingAxes"/> — which
    /// <c>BuildingAxesTests</c> already greps out of the rig source — and never against a restatement.</para>
    ///
    /// <para>No JS engine needed: all of this is text and arithmetic, so it runs on any machine.</para>
    /// </summary>
    public class VillageBuildingSetTests
    {
        static string RigSource(string rigKey) =>
            File.ReadAllText(Path.Combine(RigCatalog.RepoRoot, RigCatalog.Get(rigKey).ScriptPath));

        // ---- what M1 actually asked for ---------------------------------------------------------

        [Test]
        public void TheKitIsExactlyTheFiveBuildingsM1Asked_For()
        {
            // docs/design/world-and-regions.md §6 (St Peters): "three clapboard houses, a one-room
            // school, and a general store". docs/art/asset-manifest.md lists the same five as the P1
            // "slice gap". Pinned here so a future addition is a deliberate decision rather than drift
            // — CLAUDE.md rule 8, stay in your phase.
            var keys = VillageBuildingKit.M1Set.Select(b => b.Key).ToArray();

            CollectionAssert.AreEquivalent(
                new[] { "school", "generalStore", "whiteFarmhouse", "redSaltbox", "sageCottage" },
                keys,
                "the M1 village is a school, a store and three houses — nothing else. The wharf rig's " +
                "sheds and the cannery are M2 and are baked (uncommitted) by BuildingBakeMenu instead.");
        }

        [Test]
        public void EveryHouseInTheSetIsClapboard()
        {
            // The canon says CLAPBOARD houses. Only two of the house rig's five presets are — which is
            // why the third house is dialled rather than quietly substituted with a shingle one.
            foreach (var build in VillageBuildingKit.M1Set)
            {
                string siding = build.IsPreset
                    ? SidingOfPreset(build)
                    : build.Dialled["siding"] as string;

                Assert.AreEqual("clapboard", siding,
                    $"'{build.Key}' is in the village set but its siding is '{siding}'. The canon asks " +
                    "for clapboard; substituting shingle silently is how a region stops matching its " +
                    "own design doc.");
            }
        }

        /// <summary>The siding a preset declares, read out of the rig source rather than restated.</summary>
        static string SidingOfPreset(VillageBuildingKit.Build build)
        {
            string src = RigSource(build.RigKey);
            int at = src.IndexOf($"{build.Preset}:", StringComparison.Ordinal);
            Assert.Greater(at, -1, $"'{build.Preset}' is not declared in {build.RigKey}'s PRESETS table");

            int end = src.IndexOf('}', at);
            string entry = src.Substring(at, end - at);

            var m = System.Text.RegularExpressions.Regex.Match(entry, @"siding:\s*'([a-zA-Z]+)'");
            Assert.IsTrue(m.Success, $"'{build.Preset}' declares no siding: {entry}");
            return m.Groups[1].Value;
        }

        [Test]
        public void EveryBuildKeyIsUnique_SoNoTwoOverwriteTheSameSheet()
        {
            var keys = VillageBuildingKit.M1Set.Select(b => b.Key).ToArray();
            CollectionAssert.AllItemsAreUnique(keys,
                "two builds sharing a key would silently overwrite each other's sheet AND collide in " +
                "the contract, where the second would win the lookup");

            var stems = keys.Select(VillageBuildingKit.StemFor).ToArray();
            CollectionAssert.AllItemsAreUnique(stems);
        }

        [Test]
        public void EveryBuildIsEitherAPresetOrDialled_NeverBoth_NeverNeither()
        {
            foreach (var build in VillageBuildingKit.M1Set)
            {
                if (build.IsPreset)
                {
                    Assert.IsNull(build.Dialled,
                        $"'{build.Key}' carries both a preset and dialled axes — the baker would use " +
                        "one and silently ignore the other.");
                }
                else
                {
                    Assert.IsNotNull(build.Dialled, $"'{build.Key}' has neither a preset nor any axes.");
                    Assert.Greater(build.Dialled.Count, 0,
                        $"'{build.Key}' dials nothing, so it would render the rig default — which the " +
                        "bake refuses (BuildingRigBaker's differs-from-default tripwire).");
                }
            }
        }

        [Test]
        public void EveryBuildNamesARigTheCatalogKnows()
        {
            foreach (var build in VillageBuildingKit.M1Set)
            {
                var entry = RigCatalog.Get(build.RigKey);          // throws on an unknown key
                string abs = Path.Combine(RigCatalog.RepoRoot, entry.ScriptPath);
                Assert.IsTrue(File.Exists(abs),
                              $"'{entry.ScriptPath}' is missing — {build.Key} cannot bake.");
            }
        }

        // ---- THE SILENT-KEY TRAP ----------------------------------------------------------------

        [Test]
        public void EveryDialledKeyIsADeclaredRigAxis()
        {
            // ⚠️ THE load-bearing test in this file. An unknown option key is not an error in either
            // rig — it is ignored, and the build renders something else with no warning. BuildingAxes
            // is the vetted key list (BuildingAxesTests greps every one of its keys out of the rig
            // source), so resolving against it is resolving against measured evidence rather than
            // against another hand-written list.
            foreach (var build in VillageBuildingKit.M1Set)
            {
                if (build.IsPreset) continue;

                var known = BuildingAxes.For(build.RigKey).Select(a => a.Key).ToHashSet(StringComparer.Ordinal);
                foreach (string key in build.Dialled.Keys)
                    Assert.IsTrue(known.Contains(key),
                        $"'{build.Key}' dials '{key}', which is not a declared axis of the " +
                        $"{build.RigKey} rig. These rigs IGNORE unknown keys in silence — the recorded " +
                        "worked example is winD (the rig's internal field) vs winDensity (the option). " +
                        $"Known: {string.Join(", ", known.OrderBy(k => k))}.");
            }
        }

        [Test]
        public void EveryDialledChoiceValueAppearsInItsRigSource()
        {
            // The other half of the same trap: a known key with an unknown VALUE also falls through to a
            // default, because the rigs look the string up in a table. Grepped out of the rig, so a rig
            // drop that renames a paint colour fails loudly instead of quietly repainting a house.
            foreach (var build in VillageBuildingKit.M1Set)
            {
                if (build.IsPreset) continue;
                string src = RigSource(build.RigKey);

                foreach (var kv in build.Dialled)
                {
                    if (!(kv.Value is string value)) continue;
                    Assert.IsTrue(src.Contains($"{value}:") || src.Contains($"'{value}'"),
                        $"'{build.Key}' sets {kv.Key} = '{value}', but the {build.RigKey} rig source " +
                        "never mentions that value — it would fall through to a default in silence.");
                }
            }
        }

        [Test]
        public void EveryDialledUnitValueIsInRange()
        {
            // size / winDensity / weather are 0..1 dials. Out of range is not an error in the rig either;
            // it just draws something the kit did not intend (a negative size inverts the massing).
            var unitKeys = new[] { "size", "winDensity", "weather" };

            foreach (var build in VillageBuildingKit.M1Set)
            {
                if (build.IsPreset) continue;
                foreach (string key in unitKeys)
                {
                    if (!build.Dialled.TryGetValue(key, out object raw)) continue;
                    double v = Convert.ToDouble(raw);
                    Assert.That(v, Is.InRange(0.0, 1.0),
                                $"'{build.Key}' sets {key} = {v}, outside the rig's 0..1 dial");
                }
            }
        }

        [Test]
        public void EveryPresetBuildNamesAPresetItsRigDeclares()
        {
            foreach (var build in VillageBuildingKit.M1Set)
            {
                if (!build.IsPreset) continue;
                StringAssert.Contains($"{build.Preset}:", RigSource(build.RigKey),
                    $"'{build.Preset}' is not declared in {build.RigKey}'s PRESETS table");
            }
        }

        // ---- the bake request ------------------------------------------------------------------

        [Test]
        public void APresetBuildSpreadsThePresetsTable_NeverPassesAPresetKey()
        {
            // ⚠️ render(dir,{preset:'name'}) is the classic silent bug: no building rig's resolve() reads
            // a `preset` key, so it renders the DEFAULT build with no error and you ship five identical
            // houses under five names. Pinned at the request level, where the spelling is decided.
            foreach (var build in VillageBuildingKit.M1Set)
            {
                if (!build.IsPreset) continue;

                var req = VillageBuildingBakeMenu.RequestFor(build, "Out", "Stem");

                StringAssert.Contains("Object.assign(", req.OptsJs,
                    "a preset must be SPREAD into the options, not named");
                StringAssert.Contains($".PRESETS['{build.Preset}']", req.OptsJs);
                Assert.IsFalse(req.OptsJs.Contains("preset:"),
                    "passing {preset:'name'} renders the rig DEFAULT in total silence");
                Assert.IsTrue(req.IsPreset);
            }
        }

        [Test]
        public void EveryVillageBuildOptsIntoTheDiffersFromDefaultTripwire()
        {
            // A preset gets this for free. A DIALLED build does not by default — the Building Studio may
            // legitimately bake the rig default — so a committed kit has to ask for it, or a table whose
            // every key was misspelled would bake five identical default houses without a word.
            foreach (var build in VillageBuildingKit.M1Set)
            {
                var req = VillageBuildingBakeMenu.RequestFor(build, "Out", "Stem");
                Assert.IsTrue(req.RequireDistinctFromDefault,
                    $"'{build.Key}' does not opt into the differs-from-default check");
            }
        }

        [Test]
        public void ADialledBuildSerialisesEveryAxisItDials()
        {
            foreach (var build in VillageBuildingKit.M1Set)
            {
                if (build.IsPreset) continue;

                string js = VillageBuildingBakeMenu.OptionsLiteralFor(build);
                Assert.IsTrue(js.StartsWith("{") && js.EndsWith("}"), $"'{build.Key}': {js}");

                foreach (string key in build.Dialled.Keys)
                    StringAssert.Contains($"{key}:", js,
                        $"'{build.Key}' dials {key} but it is missing from the options literal");

                Assert.AreEqual(build.Dialled.Count, js.Count(c => c == ':'),
                    $"'{build.Key}': the literal should carry exactly one colon per dialled axis — {js}");
            }
        }

        [Test]
        public void ADialledLiteralUsesInvariantDecimals()
        {
            // A comma decimal separator on a European machine would split one option into two, and the
            // extra key would be ignored in silence like every other unknown key.
            foreach (var build in VillageBuildingKit.M1Set)
            {
                if (build.IsPreset) continue;
                string js = VillageBuildingBakeMenu.OptionsLiteralFor(build);
                foreach (var kv in build.Dialled)
                {
                    if (!(kv.Value is double)) continue;
                    Assert.IsFalse(System.Text.RegularExpressions.Regex.IsMatch(js, $@"{kv.Key}:\d+,\d"),
                                   $"'{build.Key}' serialised {kv.Key} with a comma decimal: {js}");
                }
            }
        }

        [Test]
        public void OptionsLiteralRefusesAPresetBuild()
        {
            var preset = VillageBuildingKit.M1Set.First(b => b.IsPreset);
            Assert.Throws<ArgumentException>(
                () => VillageBuildingBakeMenu.OptionsLiteralFor(preset),
                "a preset's options come from the rig's table — asking for a dialled literal is a bug " +
                "in the caller, not something to paper over with an empty object");
        }

        [Test]
        public void EveryRequestBakesAllEightFacings()
        {
            foreach (var build in VillageBuildingKit.M1Set)
            {
                var req = VillageBuildingBakeMenu.RequestFor(build, "Out", "Stem");
                Assert.AreEqual(VillageBuildingKit.Facings, req.Facings,
                    "8 at 45° is the ADR-0006 recipe and what the rigs are drawn for");
                Assert.AreEqual(8, VillageBuildingKit.Facings);
            }
        }

        [Test]
        public void TheKitBakesIntoItsOwnFolder_NotOverTheWorstCaseBatch()
        {
            // BuildingBakeMenu writes HouseIso_*/WharfBuildingIso_* into the PARENT folder and its sheets
            // are not committed. If this kit shared that folder, its slicer would see twelve sheets no
            // contract claims and fail on every one of them.
            Assert.AreNotEqual(BuildingBakeMenu.BuildingsFolder.TrimEnd('/'),
                               VillageBuildingBakeMenu.OutputFolder,
                               "the village kit and the worst-case preset batch must not share a folder");

            StringAssert.StartsWith(BuildingBakeMenu.BuildingsFolder,
                                    VillageBuildingKit.BuildingsRoot,
                                    "the kit folder should still sit under the building art root");

            foreach (var build in VillageBuildingKit.M1Set)
                StringAssert.StartsWith(VillageBuildingKit.StemPrefix,
                                        VillageBuildingKit.StemFor(build.Key),
                                        "every kit sheet carries the kit prefix");
        }

        // ---- the pivot arithmetic (no bake needed) -----------------------------------------------

        [Test]
        public void TheNormalizedPivotFlipsY_NotX()
        {
            // The rigs measure the pivot from the cell's TOP-left; Unity normalises from the BOTTOM-left.
            // Only y flips, and getting it upside-down is silent. Pinned against the hull convention
            // (RigGeometry.UnityNormalisedPivot / ADR 0026), which is what a building's ground centre —
            // a projected POINT, not a chosen row — takes.
            var e = new VillageBuildingKit.Entry { cellW = 260, cellH = 300, pivotX = 130, pivotY = 168 };

            var unity = VillageBuildingKit.NormalizedPivot(e);
            Assert.AreEqual(0.5f, unity.x, 1e-5f, "a ground-centre pivot is horizontally centred");
            Assert.AreEqual((300f - 168f) / 300f, unity.y, 1e-5f);
            Assert.AreNotEqual(e.pivotY / e.cellH, unity.y, "y must FLIP, not merely normalise");

            var geo = new RigGeometry(260, 300, 130, 168, 8, 0, 40);
            Assert.AreEqual(geo.UnityNormalisedPivot.x, unity.x, 1e-6f,
                            "a building takes the same pivot convention as a hull");
            Assert.AreEqual(geo.UnityNormalisedPivot.y, unity.y, 1e-6f);
        }

        [Test]
        public void ABuildingDoesNotPivotBottomCentre_AndTheGapIsMeasurable()
        {
            // The trap this kit exists to avoid. ArtImportPipeline defaults /buildings/ to BottomCenter;
            // the rig's pivot is the GROUND CENTRE, with the near eave and foundation drawn below it.
            // Nothing throws if you take bottom-centre — the building just stands in the dirt.
            var e = new VillageBuildingKit.Entry { cellW = 260, cellH = 300, pivotX = 130, pivotY = 268 };

            Assert.AreEqual(32, VillageBuildingKit.BelowGroundPad(e),
                            "cellH − pivotY is how much art hangs below the ground line");

            var unity = VillageBuildingKit.NormalizedPivot(e);
            Assert.Greater(unity.y, 0f, "bottom-centre would be y = 0; the ground centre is above it");
            Assert.AreEqual(32f / 300f, unity.y, 1e-5f,
                            "the gap between the two conventions IS the below-ground pad");
        }

        [Test]
        public void TheFrontFacingIsTheDoorFacing_DerivedFromTheAnchors()
        {
            // Both rigs put the door on the +Y gable and +Y projects AWAY from the camera, so cell 0 is
            // the building's back. Deriving the front from the baked anchors (largest door y in the rigs'
            // top-left space = lowest on screen = nearest the camera) is a measurement; declaring "the
            // front is cell 4" is the kind of statement that has been wrong five times in this repo.
            var e = new VillageBuildingKit.Entry
            {
                doorX = new[] { 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f },
                doorY = new[] { 100f, 130f, 180f, 230f, 260f, 230f, 180f, 130f },
            };
            Assert.AreEqual(4, VillageBuildingKit.FrontFacing(e));

            // A contract with no anchors gives the back view rather than an exception.
            Assert.AreEqual(0, VillageBuildingKit.FrontFacing(new VillageBuildingKit.Entry()));
        }

        // ---- SABOTAGE: prove the guards have teeth ----------------------------------------------

        [Test]
        public void SABOTAGE_MisspellingADialledKeyIsCaught()
        {
            // The whole point. Take a real village build, misspell ONE key the way the recorded mistake
            // did (winDensity → winD, the rig's internal field name), and the axis check must catch it.
            // If this test can be made to pass with the sabotage in place, the guard is decoration.
            var real = VillageBuildingKit.M1Set.First(b => !b.IsPreset);
            Assert.IsTrue(real.Dialled.ContainsKey("winDensity"),
                          "this sabotage assumes a dialled build sets winDensity");

            var sabotaged = new Dictionary<string, object>(
                (IDictionary<string, object>)real.Dialled, StringComparer.Ordinal);
            sabotaged.Remove("winDensity");
            sabotaged["winD"] = 0.85;                       // the rig's INTERNAL field, not the option

            var known = BuildingAxes.For(real.RigKey).Select(a => a.Key).ToHashSet(StringComparer.Ordinal);

            Assert.IsFalse(known.Contains("winD"),
                "if 'winD' were a declared axis the guard could not tell the mistake from the truth");
            Assert.IsTrue(known.Contains("winDensity"));

            var strangers = sabotaged.Keys.Where(k => !known.Contains(k)).ToArray();
            CollectionAssert.AreEqual(new[] { "winD" }, strangers,
                "the axis check must name exactly the misspelled key");
        }

        [Test]
        public void SABOTAGE_ADialledBuildThatSetsNothingWouldBeRefused()
        {
            // A dialled build with no axes renders the rig default. The bake's differs-from-default
            // tripwire is what catches it at bake time; here we prove the request still asks for that
            // check, so removing the flag is a failing test rather than a silent five-identical-houses
            // bake.
            var empty = VillageBuildingKit.Build.FromDialled(
                "saboteur", "Saboteur", "house", new Dictionary<string, object>(), "sabotage");

            var req = VillageBuildingBakeMenu.RequestFor(empty, "Out", "Stem");
            Assert.AreEqual("{}", req.OptsJs, "no axes means an empty options object");
            Assert.IsTrue(req.RequireDistinctFromDefault,
                          "…and an empty options object is exactly what the tripwire must refuse");
        }
    }
}
