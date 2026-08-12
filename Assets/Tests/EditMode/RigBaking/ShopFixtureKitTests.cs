using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HiddenHarbours.Art.Editor;
using HiddenHarbours.Tools.RigBaking;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// The shop kit's FIXTURE family, measured against the live rig.
    ///
    /// <para>The <b>committed sheet is the oracle</b>: rendered through the registered turntable with
    /// the contract's own recorded options, the rig must reproduce it byte-for-byte. That is what makes
    /// every other measurement here admissible rather than merely self-consistent — and it is
    /// sabotage-proved below, because an oracle nobody has watched go red is an oracle nobody knows
    /// works.</para>
    /// </summary>
    public class ShopFixtureKitTests
    {
        const string G = ShopFixtureKit.GlobalName;

        static IRigScriptHost NewHost()
        {
            var host = RigScriptHostFactory.Create();
            RigCatalog.InstallModule(host, RigCatalog.Get(ShopFixtureKit.RigKey));
            ShopFixtureRegistrationProbe.InstallByteCompare(host);
            return host;
        }

        static ShopFixtureKit.Build TheCounter =>
            ShopFixtureKit.Builds.First(b => b.Fixture == "counter");

        static ShopFixtureKit.Contract Contract()
        {
            if (!File.Exists(ShopFixtureKit.AbsoluteContractPath))
                Assert.Ignore($"No contract at {ShopFixtureKit.ContractPath} — run " +
                              "Hidden Harbours ▸ Art ▸ Bake Shop Fixtures.");
            var c = ShopFixtureKit.Load();
            Assert.That(c, Is.Not.Null, "the contract exists but did not parse");
            return c;
        }

        // ---- registration -------------------------------------------------------------------------

        [Test]
        public void The_family_rides_the_shop_kits_already_registered_interior_rig()
        {
            var e = RigCatalog.Get(ShopFixtureKit.RigKey);

            Assert.That(e.GlobalName, Is.EqualTo("ShopInterior"));
            Assert.That(e.ScriptPath, Is.EqualTo("docs/art/rigs/shop-building-kit/shopInteriorRig.js"));
            Assert.That(e.DeclaredConvention, Is.EqualTo(AzimuthConvention.CounterClockwise),
                "The shop rigs are registered counter-clockwise. Declaring the deck-loop kit's " +
                "clockwise here would mirror six of the eight cells of every fixture.");
            Assert.That(e.Prerequisites == null || e.Prerequisites.Count == 0, Is.True,
                "shopInterior needs nothing loaded to install — it is the root of the commercial " +
                "block, and the other two read ITS 0.5 m cell snap back.");
        }

        // ---- THE ORACLE ----------------------------------------------------------------------------

        /// <summary>
        /// Reproduce the committed sheet, byte-for-byte, from the registered turntable, using the
        /// options the CONTRACT recorded rather than the options this test would like to have been used.
        ///
        /// <para>Every cell, every facing. The crop is the contract's own <c>cropX/cropY</c>, so a bake
        /// that moved the crop and forgot to say so fails here rather than shipping a fixture that
        /// stands half a metre off its own pivot.</para>
        /// </summary>
        [Test]
        public void The_committed_sheet_reproduces_byte_for_byte()
        {
            var e = Contract().Find(TheCounter.Key);
            Assert.That(e, Is.Not.Null, $"{TheCounter.Key} is not in the contract");

            using var host = NewHost();
            Assert.That(CountDifferingPixels(host, e, e.optionsJs), Is.Zero,
                $"{e.key} does not reproduce from the rig using the options the contract itself " +
                $"records ({e.optionsJs}). Either the rig moved under the sheet or the sheet was baked " +
                "with something the contract does not say.");
        }

        /// <summary>
        /// ⭐ THE SABOTAGE PROOF. Perturb one solved parameter and the oracle above must go red.
        ///
        /// <para><b>⚠️ And the perturbation has to be proved to change the picture first</b>, because
        /// this kit has a parameter where a plausible perturbation is a NO-OP: <c>counterTone</c>. The
        /// general store's own trade default IS <c>'red'</c>, so "sabotaging" the tone to red renders
        /// the identical image and the oracle stays green — a sabotage run that proves nothing while
        /// looking exactly like one that proves everything. That case is pinned below rather than
        /// described. The perturbations used here are each shown to move pixels before they are asked
        /// to move the oracle.</para>
        /// </summary>
        [TestCase("stock", "0.5", TestName = "sabotage_the_shelf_stock")]
        [TestCase("weather", "0.9", TestName = "sabotage_the_grime")]
        [TestCase("seed", "99", TestName = "sabotage_the_dressing_seed")]
        [TestCase("size", "0.9", TestName = "sabotage_the_trade_size")]
        public void Perturbing_a_solved_parameter_turns_the_oracle_red(string key, string value)
        {
            var e = Contract().Find(TheCounter.Key);
            Assert.That(e, Is.Not.Null);

            using var host = NewHost();
            string sabotaged = Replace(e.optionsJs, key, value);
            Assert.That(sabotaged, Is.Not.EqualTo(e.optionsJs),
                $"'{key}' is not in the recorded options, so this test edited nothing and would pass " +
                "on any bake whatsoever.");

            // 1. the sabotage really is a different picture — the control this kit's counterTone needs
            Assert.That(host.EvaluateBool(
                    $"__sameBytes({RenderJs(e, e.optionsJs, 0)},{RenderJs(e, sabotaged, 0)})"),
                Is.False,
                $"Perturbing {key} to {value} rendered the IDENTICAL image, so it cannot sabotage " +
                "anything. Pick a parameter the rig is actually sensitive to (see the counterTone " +
                "case below for why this control exists).");

            // 2. …and the oracle sees it
            Assert.That(CountDifferingPixels(host, e, sabotaged), Is.GreaterThan(0),
                $"The committed sheet still matches after {key} was perturbed to {value}. The oracle " +
                "cannot see a change it was given, so its green is worthless.");
        }

        /// <summary>
        /// ⚠️ The QUANTISED-PARAMETER trap, pinned rather than remembered. <c>counterTone: 'red'</c> is
        /// the general store's own default, so it is a perturbation that changes nothing — exactly the
        /// shape of a sabotage run that passes and proves nothing.
        /// </summary>
        [Test]
        public void Some_perturbations_are_no_ops_and_would_fake_a_sabotage()
        {
            var e = Contract().Find(TheCounter.Key);
            using var host = NewHost();

            string sameTone = e.optionsJs.TrimEnd('}') + ",counterTone:'red'}";
            Assert.That(host.EvaluateBool(
                    $"__sameBytes({RenderJs(e, e.optionsJs, 0)},{RenderJs(e, sameTone, 0)})"),
                Is.True,
                "counterTone 'red' has stopped being the general store's trade default. That is fine " +
                "in itself — but it means the cautionary example in the sabotage test above no longer " +
                "illustrates anything, and the warning should move to whichever parameter is now " +
                "quantised or defaulted.");

            string otherTone = e.optionsJs.TrimEnd('}') + ",counterTone:'sage'}";
            Assert.That(host.EvaluateBool(
                    $"__sameBytes({RenderJs(e, e.optionsJs, 0)},{RenderJs(e, otherTone, 0)})"),
                Is.False,
                "the POSITIVE CONTROL: without it, 'counterTone red is a no-op' is indistinguishable " +
                "from 'counterTone does nothing at all'.");
        }

        // ---- the two silent fall-throughs ------------------------------------------------------------

        /// <summary>
        /// ⚠️ GOES RED WHEN A FUTURE DROP FIXES THE RIG — which is the point. The baker works around
        /// this with <c>AssertKnown</c>; when the workaround stops being necessary this says so, rather
        /// than the workaround quietly outliving its reason.
        /// </summary>
        [Test]
        public void An_unknown_fixture_name_still_renders_a_silent_empty_sheet()
        {
            using var host = NewHost();
            Assert.That(ShopFixtureRegistrationProbe.MeasureSilentUnknownFixture(host, G), Is.True,
                "placeItem opens `const P=FIXTURES[name]; if(!P) return;` so an unknown name returns a " +
                "full-size, fully TRANSPARENT buffer and does not throw. Baked, that is a valid sheet " +
                "of nothing that slices into the right number of empty sprites. " +
                "ShopFixtureSheetBaker.AssertKnown is the defence; this pins that the hazard is still " +
                "there to defend against.");
        }

        [Test]
        public void An_unknown_trade_still_falls_through_to_the_general_store()
        {
            using var host = NewHost();
            Assert.That(
                ShopFixtureRegistrationProbe.MeasureSilentUnknownTrade(
                    host, G, TheCounter.Fixture, TheCounter.Size),
                Is.True,
                "resolve() opens `TYPES[tk]||TYPES.generalStore`, so a misspelled trade bakes a real, " +
                "plausible fixture in the WRONG shop's paint under the right file name — a defect every " +
                "downstream check survives because the sheet is valid.");
        }

        [Test]
        public void The_baker_refuses_an_unknown_fixture_and_an_unknown_trade()
        {
            using var host = NewHost();

            Assert.That(
                () => ShopFixtureSheetBaker.AssertKnown(
                    host, G, new ShopFixtureKit.Build("counterr", "generalStore", 0.35, "typo")),
                Throws.InvalidOperationException.With.Message.Contains("not a fixture this rig draws"));

            Assert.That(
                () => ShopFixtureSheetBaker.AssertKnown(
                    host, G, new ShopFixtureKit.Build("counter", "generalStoer", 0.35, "typo")),
                Throws.InvalidOperationException.With.Message.Contains("not a trade this rig knows"));

            Assert.That(
                () => ShopFixtureSheetBaker.AssertKnown(host, G, TheCounter),
                Throws.Nothing, "the POSITIVE CONTROL: a guard that refuses everything proves nothing");
        }

        // ---- rot is redundant with dir ----------------------------------------------------------------

        /// <summary>
        /// <c>rot</c> turns the fixture in the world in 90° steps and <c>dir</c> turns the camera in 45°
        /// steps — so <c>rot = r</c> is exactly <c>dir = 2r</c>. The sheet needs ONE angular axis, and a
        /// <c>rot</c> column would ship four exact duplicates of four of the eight facings.
        /// </summary>
        [Test]
        public void Rot_is_redundant_with_dir_which_is_why_the_bake_pins_it_to_zero()
        {
            using var host = NewHost();
            var b = TheCounter;

            for (int rot = 0; rot < 4; rot++)
                Assert.That(
                    ShopFixtureRegistrationProbe.MeasureRotEquivalentDir(host, G, b.Fixture, b.Trade,
                                                                         b.Size, rot),
                    Is.EqualTo((2 * rot) % 8),
                    $"rot {rot} no longer reproduces dir {(2 * rot) % 8} exactly. If rot has gained a " +
                    "meaning a camera turn cannot express, ShopFixtureKit.BakedRot stops being a free " +
                    "choice and the sheet needs a second axis.");

            Assert.That(ShopFixtureKit.BakedRot, Is.Zero);
        }

        // ---- load order ---------------------------------------------------------------------------------

        /// <summary>
        /// ⭐ The claim <see cref="ShopFixtureSheetBaker"/> installs on: the shop kit's three documented,
        /// SILENT load-order divergences do not reach a fixture.
        ///
        /// <para>They are real — the assertion message carries the live <c>dims()</c> figures both ways
        /// so a green here can never be misread as "the load order does not diverge". What is measured
        /// is that the divergence does not reach <c>renderItem</c>, which never builds a shell. This
        /// goes red the day a drop makes a fixture read the room it stands in, and that is the day the
        /// baker has to install the whole block.</para>
        /// </summary>
        [Test]
        public void A_fixture_renders_identically_with_the_interior_rig_alone_and_with_the_whole_kit()
        {
            var b = TheCounter;
            bool same = ShopFixtureRegistrationProbe.MeasureLoadOrderInvariance(
                b.Fixture, b.Trade, b.Size, ShopFixtureKit.Facings,
                RigCatalog.Get(ShopFixtureKit.RigKey).DeclaredConvention,
                out string dimsReport);

            Assert.That(same, Is.True,
                "A fixture now renders differently depending on which of the kit's three rigs are " +
                $"loaded. The divergence is real and measured here: {dimsReport}. Until now it reached " +
                "render() and not renderItem(); if it reaches a fixture, ShopFixtureSheetBaker must " +
                "install the whole commercial block rather than shopInterior alone.");

            StringAssert.Contains("planned=", dimsReport,
                "the report must carry the live figures, or the green above is unattributable");
        }

        // ---- the probe ------------------------------------------------------------------------------------

        [Test]
        public void The_probe_measures_the_pivot_the_handedness_and_the_service_face()
        {
            using var host = NewHost();
            var b = TheCounter;
            var r = ShopFixtureRegistrationProbe.Measure(
                host, G, b.Fixture, b.Trade, b.Size, ShopFixtureKit.Facings,
                RigCatalog.Get(ShopFixtureKit.RigKey).DeclaredConvention);

            Assert.That(r.PixelsPerMetre, Is.EqualTo(ShopFixtureKit.PixelsPerUnit));
            Assert.That(r.Dirs, Is.EqualTo(ShopFixtureKit.Facings));
            Assert.That(r.PivotDeviationPx, Is.LessThan(1e-6),
                "renderItem places a fixture at world (0,0), so the projected origin must not move " +
                "between facings — the whole family pins ONE pivot for all eight cells.");
            Assert.That(r.CellStepDeg, Is.EqualTo(-45.0).Within(ShopFixtureRegistrationProbe.StepToleranceDeg),
                "The BAKED CELLS must step −45° on the un-squashed ground plane. That sign is what " +
                "entitles BuildingFacing to its bearing table; the rig's own dir frame turns the other " +
                "way and RigBaker.DirForCell is the correction.");
            Assert.That(r.MeasuredInCellFrame, Is.EqualTo(AzimuthConvention.CounterClockwise));
            Assert.That(r.ServiceFaceIsPlusY, Is.True,
                "the rig's anchors() puts the customer queue at +dv and the keeper's station at −dv, " +
                "so a fixture at rot 0 serves +y");
        }

        // ---- the table --------------------------------------------------------------------------------------

        [Test]
        public void Every_baked_fixture_is_one_the_rig_draws_and_the_keys_are_unique()
        {
            using var host = NewHost();
            var known = host.EvaluateString($"{G}.list().join(',')").Split(',');

            foreach (var b in ShopFixtureKit.Builds)
            {
                Assert.That(known, Does.Contain(b.Fixture), $"'{b.Fixture}' is not in the rig's catalog");
                Assert.That(host.EvaluateBool($"typeof {G}.TYPES['{b.Trade}'] === 'object'"), Is.True,
                    $"'{b.Trade}' is not a trade this rig knows");
            }

            Assert.That(ShopFixtureKit.Builds.Select(b => b.Key).Distinct().Count(),
                        Is.EqualTo(ShopFixtureKit.Builds.Length),
                "sheet keys must be unique — a collision would overwrite a sheet on disk");
        }

        /// <summary>
        /// A fixture and the building it stands in must bake at the SAME size. <c>size</c> is not only a
        /// footprint dial: it seeds the weather speckle in the rig's post pass, so two different sizes
        /// are two different RNG streams dressing one shop.
        /// </summary>
        [Test]
        public void A_fixtures_size_matches_the_shop_its_trade_builds_at()
        {
            foreach (var b in ShopFixtureKit.Builds)
            {
                var shop = ShopKit.ShopSet.FirstOrDefault(s => s.Trade == b.Trade);
                if (string.IsNullOrEmpty(shop.Key)) continue;      // a trade St Peters does not build

                Assert.That(b.Size, Is.EqualTo(shop.Size).Within(1e-9),
                    $"{b.Key} bakes at size {b.Size} but ShopKit builds '{b.Trade}' at {shop.Size}. " +
                    "Two sizes are two different weather-speckle streams dressing one shop.");
            }
        }

        [Test]
        public void Every_planned_sheet_fits_the_import_cap()
        {
            using var host = NewHost();
            int nativeW = (int)host.EvaluateNumber($"{G}.W");
            int nativeH = (int)host.EvaluateNumber($"{G}.H");

            // The NATIVE buffer bounds the cropped cell, so this is the conservative check — and it is
            // the one that would notice a rig whose buffer grew past what any crop could rescue.
            Assert.That(nativeH, Is.LessThanOrEqualTo(ShopFixtureKit.ImportSizeCap),
                "a single row of the native cell already exceeds the cap");

            foreach (var e in Contract().fixtures)
            {
                Assert.That(e.sheetW, Is.LessThanOrEqualTo(ShopFixtureKit.ImportSizeCap),
                    $"{e.key} is {e.sheetW} px wide. A sheet between the cap and Unity's hard 4096 " +
                    "bakes fine and imports SILENTLY DOWNSCALED with the sprite count still correct.");
                Assert.That(e.sheetH, Is.LessThanOrEqualTo(ShopFixtureKit.ImportSizeCap),
                    $"{e.key} is {e.sheetH} px tall");
                Assert.That(e.sheetW, Is.EqualTo(e.cellW * e.columns));
                Assert.That(e.sheetH, Is.EqualTo(e.cellH * Mathf.Max(1, e.rows)));
                Assert.That(nativeW, Is.GreaterThanOrEqualTo(e.cellW),
                    $"{e.key}'s cropped cell is wider than the buffer it was cropped out of");
            }
        }

        // ---- the contract -------------------------------------------------------------------------------------

        [Test]
        public void The_contract_round_trips_and_puts_the_pivot_the_ADR_0026_way_up()
        {
            var c = new ShopFixtureKit.Contract
            {
                rigKey = ShopFixtureKit.RigKey, globalName = G, convention = "CounterClockwise",
                ppu = 32, facings = 8, importSizeCap = ShopFixtureKit.ImportSizeCap,
                nativeCellW = 1180, nativeCellH = 900,
                fixtures = new[]
                {
                    new ShopFixtureKit.Entry
                    {
                        key = "counter_generalStore", fixture = "counter", trade = "generalStore",
                        label = "Shop counter", cat = "service", size = 0.35f,
                        facings = 8, columns = 8, rows = 1,
                        cellW = 95, cellH = 93, pivotX = 48, pivotY = 63,
                        sheetW = 760, sheetH = 93,
                        frontX = new float[8], frontY = new float[8],
                    },
                },
            };

            var back = JsonUtility.FromJson<ShopFixtureKit.Contract>(JsonUtility.ToJson(c));
            var e = back.Find("counter_generalStore");

            Assert.That(e, Is.Not.Null);
            Assert.That(e.cellW, Is.EqualTo(95));
            Assert.That(e.SpriteName(3), Is.EqualTo("ShopFixture_counter_generalStore_d3"));
            Assert.That(e.Stem, Is.EqualTo("ShopFixture_counter_generalStore"));

            // ADR 0026: the y term is (H − pivotY)/H, and getting it upside-down is silent.
            Assert.That(e.NormalisedPivot.y, Is.EqualTo((93 - 63) / 93f).Within(1e-6));
            Assert.That(e.NormalisedPivot.x, Is.EqualTo(48 / 95f).Within(1e-6));
            Assert.That(e.NormalisedPivot.y, Is.Not.EqualTo(63 / 93f).Within(1e-6),
                "the POSITIVE CONTROL for the flip: an upside-down pivot is a legal-looking number");
        }

        [Test]
        public void The_contract_records_what_it_was_baked_with()
        {
            var e = Contract().Find(TheCounter.Key);
            Assert.That(e, Is.Not.Null);

            foreach (string key in new[] { "type:", "size:", "stock:", "weather:", "night:", "seed:",
                                           "rot:", "elev:" })
                StringAssert.Contains(key, e.optionsJs,
                    $"the recorded options omit '{key}'. Every one of these moves a fixture's pixels " +
                    "(measured), so an omitted one makes the contract an incomplete statement of what " +
                    "these pixels are — and the oracle above renders FROM this string.");

            Assert.That(e.frontX?.Length, Is.EqualTo(e.facings));
            Assert.That(e.frontY?.Length, Is.EqualTo(e.facings));
            Assert.That(e.footprintWidthMetres, Is.GreaterThan(0));
            Assert.That(e.footprintDepthMetres, Is.GreaterThan(0));
        }

        // ---- the slice ------------------------------------------------------------------------------------------

        /// <summary>
        /// The sheets imported at native resolution, sliced into the contract's cells, with the
        /// contract's pivots and the locked import settings.
        ///
        /// <para><b>spriteMode Multiple is NOT the same as sliced.</b> A fresh import is Multiple with
        /// ZERO rects, and every downstream <c>LoadAllAssetsAtPath</c> then returns nothing while the
        /// importer reports exactly what was asked for.</para>
        /// </summary>
        [Test]
        public void The_committed_sheets_are_imported_and_sliced_the_contracts_way()
        {
            Contract();     // Ignore early if the bake has not run
            var problems = ShopFixtureSheetSlicer.Verify(out int checkedSheets);

            Assert.That(checkedSheets, Is.GreaterThan(0));
            Assert.That(problems, Is.Empty, string.Join("\n", problems));
        }

        [Test]
        public void The_slicer_builds_one_rect_per_facing_at_the_contracts_pivot()
        {
            var e = Contract().Find(TheCounter.Key);
            var rects = ShopFixtureSheetSlicer.BuildRects(e);

            Assert.That(rects.Length, Is.EqualTo(e.facings));
            for (int facing = 0; facing < e.facings; facing++)
            {
                var r = rects[facing];
                Assert.That(r.name, Is.EqualTo(e.SpriteName(facing)));
                Assert.That(r.rect, Is.EqualTo(new Rect(facing * e.cellW, 0, e.cellW, e.cellH)));
                Assert.That(r.alignment, Is.EqualTo(SpriteAlignment.Custom),
                    "anything else throws the contract's pivot away and re-centres the fixture");
                Assert.That(r.pivot, Is.EqualTo(e.NormalisedPivot));
            }
        }

        // The CONSUMER-side tests (does the store's counter face the green it is walked up from) live
        // in HiddenHarbours.Tests.EditMode/StPetersStoreCounterTests.cs, which is the assembly that
        // references App.Editor. This one may not — rule 4, and the split is the point.

        // ---- helpers -----------------------------------------------------------------------------------------------

        /// <summary>
        /// Renders every facing through the registered turntable with <paramref name="optionsJs"/> and
        /// counts the pixels that differ from the committed sheet.
        /// </summary>
        static int CountDifferingPixels(IRigScriptHost host, ShopFixtureKit.Entry e, string optionsJs)
        {
            string path = ShopFixtureKit.SheetPath(e.key);
            string abs = Path.Combine(RigCatalog.RepoRoot, path);
            Assert.That(File.Exists(abs), Is.True, $"missing baked sheet {path}");

            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false, linear: false);
            try
            {
                Assert.That(tex.LoadImage(File.ReadAllBytes(abs)), Is.True, "the sheet PNG did not decode");
                Assert.That(tex.width, Is.EqualTo(e.sheetW), "sheet width vs contract");
                Assert.That(tex.height, Is.EqualTo(e.sheetH), "sheet height vs contract");

                // The PNG's rows run TOP-DOWN; GetPixels32 is bottom-origin.
                var reference = tex.GetPixels32();
                int nativeW = (int)host.EvaluateNumber($"{G}.W");
                var convention = RigCatalog.Get(ShopFixtureKit.RigKey).DeclaredConvention;
                int differing = 0;

                for (int facing = 0; facing < e.facings; facing++)
                {
                    double dir = RigBaker.DirForCell(facing, e.facings, convention);
                    byte[] cell = host.EvaluateBytes(
                        $"{G}.renderItem('{e.fixture}'," +
                        $"{dir.ToString("R", System.Globalization.CultureInfo.InvariantCulture)},{optionsJs})");

                    for (int y = 0; y < e.cellH; y++)
                        for (int x = 0; x < e.cellW; x++)
                        {
                            int s = ((e.cropY + y) * nativeW + (e.cropX + x)) * 4;
                            var r = reference[(e.sheetH - 1 - y) * e.sheetW + facing * e.cellW + x];
                            if (cell[s] != r.r || cell[s + 1] != r.g ||
                                cell[s + 2] != r.b || cell[s + 3] != r.a) differing++;
                        }
                }
                return differing;
            }
            finally { UnityEngine.Object.DestroyImmediate(tex); }
        }

        static string RenderJs(ShopFixtureKit.Entry e, string optionsJs, int dir) =>
            $"{G}.renderItem('{e.fixture}',{dir},{optionsJs})";

        /// <summary>Replaces one <c>key:value</c> in a recorded options expression. Returns the input
        /// unchanged when the key is absent, which the caller asserts against.</summary>
        static string Replace(string optionsJs, string key, string value)
        {
            int at = optionsJs.IndexOf(key + ":", StringComparison.Ordinal);
            if (at < 0) return optionsJs;
            int end = optionsJs.IndexOfAny(new[] { ',', '}' }, at);
            if (end < 0) return optionsJs;
            return optionsJs.Substring(0, at) + key + ":" + value + optionsJs.Substring(end);
        }
    }
}
