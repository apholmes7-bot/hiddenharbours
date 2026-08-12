using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using HiddenHarbours.Art.Editor;
using HiddenHarbours.Tools.RigBaking;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// The fuel storage kit's registration probe, as tests.
    ///
    /// <para>Every claim this kit makes about its own conventions is re-derived here from the live
    /// rig — handedness, the shared turntable, the pivot, the fill axis — because every kit imported
    /// into this repo has had at least one declared convention turn out wrong, and the failure mode
    /// is always a plausible picture rather than an exception.</para>
    ///
    /// <para>The kit's own <b>reference sheets are the oracle</b>: rendered through the REGISTERED
    /// turntable, the rig must reproduce them byte-for-byte. That is what makes the rest of these
    /// measurements admissible rather than merely self-consistent.</para>
    /// </summary>
    public class FuelStorageKitTests
    {
        const string G = FuelStorageKit.GlobalName;

        static string ReferenceDir =>
            Path.Combine(RigCatalog.RepoRoot, "docs/art/rigs/fuel-storage-kit/reference");

        static IRigScriptHost NewHost()
        {
            var host = RigScriptHostFactory.Create();
            RigCatalog.InstallModule(host, RigCatalog.Get(FuelStorageKit.RigKey));
            return host;
        }

        // ---- registration --------------------------------------------------------------------

        [Test]
        public void Catalog_registers_the_fuel_rig_clockwise_on_the_deck_loop_turntable()
        {
            var e = RigCatalog.Get(FuelStorageKit.RigKey);

            Assert.That(e.GlobalName, Is.EqualTo("FuelIso"));
            Assert.That(e.ScriptPath, Is.EqualTo("docs/art/rigs/fuel-storage-kit/fuelRig.js"));
            Assert.That(e.DeclaredConvention, Is.EqualTo(AzimuthConvention.Clockwise),
                "The fuel kit rides the deck-loop turntable, which is MEASURED clockwise. Declaring " +
                "the fleet's counter-clockwise here would mirror all eight cells of every vessel.");
            Assert.That(e.Prerequisites, Does.Contain("deckIsoSolid"),
                "fuelRig.js opens with `if (!IS) throw` on a missing IsoSolid, so a bake without the " +
                "turntable fails loudly — but the prerequisite is declared so no caller has to know.");
        }

        /// <summary>
        /// The drop shipped its own <c>isoSolid.js</c>. It is the SAME FILE as the registered
        /// <c>deckIsoSolid</c> — 216 lines of CRLF-vs-LF apart — so the kit's copy is gitignored and
        /// the catalog points at the registered one. If a future drop ever diverges, this goes red
        /// and the prerequisite has to be reconsidered rather than silently rendering different art.
        /// </summary>
        [Test]
        public void The_drops_turntable_is_byte_identical_to_the_registered_one()
        {
            string dropPath = Path.Combine(RigCatalog.RepoRoot,
                                           "docs/art/rigs/fuel-storage-kit/isoSolid.js");
            if (!File.Exists(dropPath))
                Assert.Ignore("The drop's isoSolid.js is gitignored; materialise it to run this " +
                              "(see docs/art/rigs/fuel-storage-kit/IMPORT.md).");

            string registered = Path.Combine(RigCatalog.RepoRoot,
                                             "docs/art/rigs/deck-loop-kit/Art/isoSolid.js");
            Assert.That(NormalisedSha(dropPath), Is.EqualTo(NormalisedSha(registered)),
                "The fuel kit's turntable copy has diverged from the registered deckIsoSolid. The " +
                "catalog entry declares deckIsoSolid as prerequisite on the strength of them being " +
                "one file; if they are not, the fuel sheets bake against a different camera.");
        }

        static string NormalisedSha(string path)
        {
            string text = File.ReadAllText(path).Replace("\r\n", "\n");
            using var sha = SHA256.Create();
            return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(text)));
        }

        // ---- THE ORACLE ------------------------------------------------------------------------

        /// <summary>
        /// Reproduce the drop's own reference sheets, byte-for-byte, from the REGISTERED turntable.
        ///
        /// <para>This is the faithfulness control, and it is not optional: without it every other
        /// measurement here is a statement about some renderer, not about the one that shipped these
        /// pixels. All twelve of the drop's sheets reproduce; two small ones are checked in-editor to
        /// keep the suite quick, and their parameters were SOLVED by exhaustive search rather than
        /// read off the README — which is how the nozzle turned out to be diesel, in its carry cell,
        /// and the jug 54.17% full.</para>
        /// </summary>
        // ⚠️ 0.62 and 0.625 are the SAME PICTURE — geo() quantises fill to Math.round(fill*96)/96 and
        // both land on 60/96. A sabotage run that perturbed 0.62 to 0.625 passed for that reason and
        // proved nothing; 0.5 (48/96) is a genuinely different render and does go red.
        [TestCase("FuelIso_JerryCan_s20_gas", "jerry", "s20", "gas", 0.62f, true)]
        [TestCase("FuelIso_Drum_s205_diesel", "drum", "s205", "diesel", 60f / 96f, false)]
        public void Reference_sheets_reproduce_byte_for_byte(string sheet, string type, string size,
                                                             string grade, float fill, bool rest)
        {
            string png = Path.Combine(ReferenceDir, sheet + ".png");
            Assert.That(File.Exists(png), Is.True, $"missing reference sheet {png}");

            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false, linear: false);
            try
            {
                Assert.That(tex.LoadImage(File.ReadAllBytes(png)), Is.True, "reference PNG did not decode");

                using var host = NewHost();
                int w = (int)host.EvaluateNumber($"{G}.cell('{type}','{size}',{{size:'{size}'{(rest ? ",rest:true" : "")}}}).W");
                int h = (int)host.EvaluateNumber($"{G}.cell('{type}','{size}',{{size:'{size}'{(rest ? ",rest:true" : "")}}}).H");

                Assert.That(tex.width, Is.EqualTo(w * 8), "reference sheet is 8 cells wide");
                Assert.That(tex.height, Is.EqualTo(h));

                // The reference PNG's rows run TOP-DOWN; Texture2D.GetPixels32 is bottom-origin.
                var refPixels = tex.GetPixels32();

                for (int dir = 0; dir < 8; dir++)
                {
                    string opts = $"{{size:'{size}',fuel:'{grade}',fill:{fill.ToString("R", System.Globalization.CultureInfo.InvariantCulture)}," +
                                  $"wear:'{FuelStorageKit.BakedWear}'{(rest ? ",rest:true" : "")}}}";
                    byte[] cell = host.EvaluateBytes($"{G}.render('{type}',{dir},{opts})");
                    Assert.That(cell.Length, Is.EqualTo(w * h * 4));

                    for (int y = 0; y < h; y++)
                        for (int x = 0; x < w; x++)
                        {
                            int s = (y * w + x) * 4;
                            var r = refPixels[(h - 1 - y) * (w * 8) + dir * w + x];
                            Assert.That(new[] { cell[s], cell[s + 1], cell[s + 2], cell[s + 3] },
                                        Is.EqualTo(new[] { r.r, r.g, r.b, r.a }),
                                        $"{sheet} dir {dir} pixel ({x},{y}) differs from the drop's " +
                                        "own reference render.");
                        }
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(tex); }
        }

        // ---- handedness --------------------------------------------------------------------------

        [Test]
        public void Handedness_measures_clockwise_against_a_registered_counter_clockwise_rig()
        {
            using var host = RigScriptHostFactory.Create();
            var report = FuelRegistrationProbe.Measure(host);

            Assert.That(report.Measured, Is.EqualTo(AzimuthConvention.Clockwise));
            Assert.That(report.GroundStepPerDir, Is.EqualTo(-45.0).Within(0.001),
                "the fuel turntable steps -45 deg/dir on the un-squashed ground plane");
            Assert.That(report.ReferenceGroundStepPerDir, Is.EqualTo(+45.0).Within(0.001),
                $"{FuelRegistrationProbe.ReferenceRigKey} is registered CounterClockwise and must " +
                "measure the opposite sign, or the comparison proves nothing");
        }

        /// <summary>
        /// ⚠️ Pins the TRAP, not just the answer. The screen-space rotation per dir step is the
        /// obvious thing to measure and it is USELESS as a handedness test: both families sit at
        /// 46.7525° of it. This test exists so that anyone who "simplifies" the probe to the screen
        /// figure discovers immediately that it cannot tell the two apart.
        /// </summary>
        [Test]
        public void The_screen_step_does_not_distinguish_the_two_turntables()
        {
            using var host = RigScriptHostFactory.Create();
            var r = FuelRegistrationProbe.Measure(host);

            Assert.That(Math.Abs(r.ScreenStepMean), Is.EqualTo(Math.Abs(r.ReferenceScreenStepMean))
                                                      .Within(0.001),
                "If these magnitudes ever differ, the screen figure has become informative and this " +
                "test's premise needs revisiting. Until then it is 46.7525 for both.");
            Assert.That(Math.Abs(r.ScreenStepMean), Is.EqualTo(46.7525).Within(0.01));
        }

        [Test]
        public void The_rig_projects_through_the_registered_turntable_by_identity()
        {
            using var host = RigScriptHostFactory.Create();
            var r = FuelRegistrationProbe.Measure(host);

            Assert.That(r.TurntableMaxDxPx, Is.LessThan(1e-9),
                "FuelIso.mount() must agree with IsoSolid.proj exactly in x");
            Assert.That(r.TurntableDySpreadPx, Is.LessThan(1e-9),
                "A CONSTANT y residual is a different assumed height; a VARYING one would be a " +
                "second camera, and that is what this bounds.");
        }

        // ---- the two upstream defects, recorded not patched ---------------------------------------

        /// <summary>
        /// ⚠️ GOES RED WHEN A FUTURE DROP FIXES THE RIG — which is the point. The baker works around
        /// this by passing an explicit <c>wear</c> on every render; when the workaround stops being
        /// necessary this test says so, rather than the workaround quietly outliving its reason.
        /// </summary>
        [Test]
        public void Omitting_wear_still_collides_with_working_in_the_geometry_cache()
        {
            using var host = NewHost();
            Assert.That(FuelRegistrationProbe.MeasureWearCacheDefect(host, G), Is.True,
                "The rig's streak passes read `o.wear` RAW (bailing on undefined) while wearPass and " +
                "the cache key normalise it to 'working', so an omitted wear renders a phantom state " +
                "under 'working''s cache key and the first call of a session wins. If this is now " +
                "false the drop has fixed it: drop the explicit-wear workaround in FuelSheetBaker.");
        }

        [Test]
        public void An_unknown_size_still_falls_through_silently()
        {
            using var host = NewHost();
            Assert.That(FuelRegistrationProbe.MeasureSilentSizeFallback(host, G), Is.True,
                "resolveSize substitutes a default for anything it does not know, so a typo bakes a " +
                "real vessel of the WRONG size under the right filename. FuelSheetBaker.AssertKnown " +
                "is the defence; this pins that the hazard is still there to defend against.");
        }

        // ---- the ring pass -------------------------------------------------------------------------

        [Test]
        public void The_kit_is_ringless_and_its_ab_arm_actually_works()
        {
            using var host = RigScriptHostFactory.Create();
            var r = FuelRegistrationProbe.Measure(host);

            Assert.That(r.TurntableExportsKeylineDefault, Is.True,
                "FuelIso declares no KEYLINE_DEFAULT of its own, so the gate binds to IsoSolid's — " +
                "which owns the only ring pass this kit has.");
            Assert.That(r.TurntableKeylineDefaultValue, Is.False, "ADR 0031 retired the ring");
            Assert.That(r.KeylineTruePixelDelta, Is.GreaterThan(0),
                "the POSITIVE CONTROL: without it, 'the default is ringless' is unfalsifiable");
        }

        /// <summary>
        /// ⚠️ This kit spells its A/B arm <c>{keyline:true}</c>. The repo's usual
        /// <c>{outline:true}</c> is silently ignored and comes back ringless — indistinguishable from
        /// a successful gate-off render, which is precisely the false pass the positive control
        /// exists to catch. Pinned so nobody re-derives it the hard way.
        /// </summary>
        [Test]
        public void The_repo_standard_outline_spelling_is_silently_ignored_by_this_kit()
        {
            using var host = RigScriptHostFactory.Create();
            var r = FuelRegistrationProbe.Measure(host);
            Assert.That(r.OutlineTruePixelDelta, Is.Zero,
                "If {outline:true} now does something, a drop has wired it and an A/B driven with " +
                "the repo-standard name is no longer a false pass.");
        }

        // ---- the fill axis ---------------------------------------------------------------------

        /// <summary>
        /// The handoff's question: overlay layer, or whole-container frames? MEASURED: whole frames.
        /// The rig exposes no overlay entry point at all — <c>fill</c> is an argument to the geometry
        /// builder, and the wall is split at the surface before projection. So the fill axis costs a
        /// full cell per state, which is what sets this kit's budget.
        /// </summary>
        [Test]
        public void Fill_is_a_whole_container_axis_with_no_overlay_entry_point()
        {
            using var host = NewHost();
            foreach (string fn in new[] { "renderFill", "fillOverlay", "renderLayers", "gaugeOverlay" })
                Assert.That(host.EvaluateBool($"typeof {G}.{fn} === 'function'"), Is.False,
                    $"{G}.{fn} now exists — the kit may have gained an overlay path, which would " +
                    "collapse the fill budget from a cell per state to a gauge-sized patch.");

            Assert.That(host.EvaluateBool($"typeof {G}.render === 'function'"), Is.True);
        }

        /// <summary>
        /// A vessel that holds nothing has ONE fill frame, measured across all 97 quanta the rig
        /// resolves. Baking the full ladder for those would ship four duplicate dispensers per grade.
        /// </summary>
        [Test]
        public void Vessels_that_hold_nothing_render_one_frame_for_every_fill()
        {
            using var host = NewHost();
            foreach (string type in FuelStorageKit.HoldsNothing)
            {
                string size = host.EvaluateString($"{G}.sizesOf('{type}')[0]");
                Assert.That(DistinctFillFrames(host, type, size), Is.EqualTo(1),
                    $"{type} has read '{host.EvaluateString($"{G}.TYPES['{type}'].read")}' and must " +
                    "be invariant under fill");
                Assert.That(FuelStorageKit.FillsFor(type).Count, Is.EqualTo(1));
            }
        }

        [Test]
        public void Vessels_that_hold_a_volume_render_distinctly_at_every_baked_fill()
        {
            using var host = NewHost();
            foreach (string type in new[] { "jug", "jerry", "drum", "tote", "skid", "bulk" })
            {
                string size = host.EvaluateString($"{G}.sizesOf('{type}')[0]");
                var shas = FuelStorageKit.Fills
                    .Select(f => Sha(host.EvaluateBytes(
                        $"{G}.render('{type}',3,{{size:'{size}',fuel:'diesel',fill:{f.ToString("R", System.Globalization.CultureInfo.InvariantCulture)}," +
                        $"wear:'{FuelStorageKit.BakedWear}'{(FuelStorageKit.IsCarry(type) ? ",rest:true" : "")}}})")))
                    .ToArray();

                Assert.That(shas.Distinct().Count(), Is.EqualTo(FuelStorageKit.Fills.Count),
                    $"{type} {size} repeats a frame across the baked fill ladder — that state is " +
                    "shipping twice and the ladder should lose a rung rather than duplicate one.");
            }
        }

        static int DistinctFillFrames(IRigScriptHost host, string type, string size)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int q = 0; q <= 96; q++)
                seen.Add(Sha(host.EvaluateBytes(
                    $"{G}.render('{type}',3,{{size:'{size}',fuel:'gas',fill:{q}/96," +
                    $"wear:'{FuelStorageKit.BakedWear}'}})")));
            return seen.Count;
        }

        static string Sha(byte[] b)
        {
            using var sha = SHA256.Create();
            return Convert.ToBase64String(sha.ComputeHash(b));
        }

        /// <summary>
        /// The cell must not move with fill, or the fill ROWS of a sheet would slice into different
        /// rects and a container would appear to hop as it drained — silently, because every cell
        /// looks correct on its own. The rig measures its cell at <c>fill: 1</c> for this reason;
        /// this checks it actually holds.
        /// </summary>
        [Test]
        public void The_cell_is_the_same_at_every_fill()
        {
            using var host = NewHost();
            foreach (var kv in FuelStorageKit.Sizes)
                foreach (string size in kv.Value)
                {
                    string rest = FuelStorageKit.IsCarry(kv.Key) ? ",rest:true" : "";
                    var first = Cell(host, kv.Key, size, FuelStorageKit.Fills[0], rest);
                    foreach (float f in FuelStorageKit.Fills.Skip(1))
                        Assert.That(Cell(host, kv.Key, size, f, rest), Is.EqualTo(first),
                            $"{kv.Key} {size} changes cell between fills");
                }
        }

        static string Cell(IRigScriptHost host, string type, string size, float fill, string rest) =>
            host.EvaluateString(
                $"(function(){{var c={G}.cell('{type}','{size}',{{size:'{size}',fill:" +
                $"{fill.ToString("R", System.Globalization.CultureInfo.InvariantCulture)}," +
                $"wear:'{FuelStorageKit.BakedWear}'{rest}}});" +
                "return c.W+'x'+c.H+'@'+c.cx+','+c.cy;})()");

        // ---- the kit table ---------------------------------------------------------------------

        [Test]
        public void The_kit_table_names_exactly_what_the_rig_draws()
        {
            using var host = RigScriptHostFactory.Create();
            var report = FuelRegistrationProbe.Measure(host);
            Assert.DoesNotThrow(() =>
                FuelRegistrationProbe.AssertVocabularyMatches(host, G, report));
        }

        [Test]
        public void Every_planned_sheet_fits_the_import_cap()
        {
            using var host = NewHost();
            foreach (var b in FuelStorageKit.Builds)
            {
                string rest = FuelStorageKit.IsCarry(b.Type) ? ",rest:true" : "";
                int w = (int)host.EvaluateNumber(
                    $"{G}.cell('{b.Type}','{b.Size}',{{size:'{b.Size}'{rest}}}).W");
                int h = (int)host.EvaluateNumber(
                    $"{G}.cell('{b.Type}','{b.Size}',{{size:'{b.Size}'{rest}}}).H");

                // The NATIVE cell bounds the cropped one, so checking it is the conservative test.
                int sheetW = w * FuelStorageKit.FacingsFor(b.Type);
                int sheetH = h * FuelStorageKit.FillsFor(b.Type).Count;

                Assert.That(sheetW, Is.LessThanOrEqualTo(FuelStorageKit.ImportSizeCap),
                    $"{b.Key} is {sheetW} px wide before cropping. A sheet between the cap and " +
                    "Unity's hard 4096 bakes fine and imports SILENTLY DOWNSCALED.");
                Assert.That(sheetH, Is.LessThanOrEqualTo(FuelStorageKit.ImportSizeCap),
                    $"{b.Key} is {sheetH} px tall before cropping");
            }
        }

        [Test]
        public void The_build_table_covers_every_vessel_size_and_grade_once()
        {
            var builds = FuelStorageKit.Builds;
            int expected = FuelStorageKit.Sizes.Sum(kv => kv.Value.Length) * FuelStorageKit.Grades.Count;

            Assert.That(builds.Count, Is.EqualTo(expected), "21 sizes x 4 grades = 84 sheets");
            Assert.That(builds.Select(b => b.Key).Distinct().Count(), Is.EqualTo(builds.Count),
                "sheet keys must be unique — a collision would overwrite a sheet on disk");
        }

        [Test]
        public void The_contract_round_trips_through_JsonUtility()
        {
            var c = new FuelStorageKit.Contract
            {
                generated = "2026-08-12", rigKey = FuelStorageKit.RigKey,
                globalName = G, convention = "Clockwise",
                pixelsPerUnit = 32, importSizeCap = FuelStorageKit.ImportSizeCap,
                bakedWear = FuelStorageKit.BakedWear,
                carryFacings = 8, storeFacings = 4,
                sheets = new[]
                {
                    new FuelStorageKit.SheetEntry
                    {
                        key = "jerry_s20_gas", type = "jerry", size = "s20", grade = "gas",
                        kind = "carry", read = "body", shape = "box", wear = "working", rest = true,
                        facings = 8, cellW = 12, cellH = 20, pivotX = 6, pivotY = 17,
                        columns = 8, rows = 5, sheetW = 96, sheetH = 100,
                        fills = new[] { 0f, .25f, .5f, .75f, 1f }, litres = 20, tare = 2.2f,
                        fullKg = 17.1f, pivotInsideInk = true,
                    },
                },
            };

            var back = JsonUtility.FromJson<FuelStorageKit.Contract>(JsonUtility.ToJson(c));
            Assert.That(back.sheets.Length, Is.EqualTo(1));
            var s = back.Find("jerry_s20_gas");
            Assert.That(s, Is.Not.Null);
            Assert.That(s.cellW, Is.EqualTo(12));
            Assert.That(s.fills, Is.EqualTo(new[] { 0f, .25f, .5f, .75f, 1f }));
            Assert.That(s.SpriteName(2, 3), Is.EqualTo("jerry_s20_gas_f2_d3"));

            // ADR 0026: the y term is (H − pivotY)/H, and getting it upside-down is silent.
            Assert.That(s.NormalisedPivot.y, Is.EqualTo((20 - 17) / 20f).Within(1e-6));
            Assert.That(s.NormalisedPivot.x, Is.EqualTo(6 / 12f).Within(1e-6));
        }
    }
}
