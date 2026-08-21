using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using HiddenHarbours.Art.Editor;
using HiddenHarbours.Tools.RigBaking;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// The gas-station kit's registration probe, as tests.
    ///
    /// <para>Every claim this kit makes about its own conventions is re-derived here from the live
    /// rigs — handedness, the shared turntable, the seam between shell and room, the facing fold —
    /// because every kit imported into this repo has had at least one declared convention turn out
    /// wrong, and the failure mode is always a plausible picture rather than an exception.</para>
    ///
    /// <para><b>This drop shipped no reference sheets, so the SIDECAR is the oracle.</b>
    /// <c>gasStationRig.gameplay.json</c> is generated from the rigs, and regenerating it here has to
    /// reproduce the committed 83,650 bytes exactly. That single check proves the host loads the kit
    /// faithfully, that the committed file was never hand-edited, that both stamps sit where the
    /// export tool puts them, and that the bake's grade list is the one the sidecar describes — which
    /// is what makes every other measurement below admissible rather than merely self-consistent.</para>
    /// </summary>
    public class GasStationKitTests
    {
        const string G = GasStationKit.GlobalName;
        const string I = GasStationKit.InteriorGlobalName;

        const string KitDir = "docs/art/rigs/gas-station-rig";
        const string ShellRig = KitDir + "/rig/gasStationRig.js";
        const string InteriorRig = KitDir + "/rig/stationInteriorRig.js";
        const string Sidecar = KitDir + "/gameplay/gasStationRig.gameplay.json";

        /// <summary>The grade list the committed sidecar was generated with, and the bake's own.</summary>
        static string BakedGradesJs =>
            string.Join(",", GasStationKit.BakedGrades.Select(x => $"'{x}'"));

        static string Abs(string rel) => Path.Combine(RigCatalog.RepoRoot, rel);

        /// <summary>
        /// A host with the WHOLE kit up, in the declared order. Installing the interior key pulls the
        /// shell, the grade table and the turntable in front of it.
        /// </summary>
        static IRigScriptHost NewHost()
        {
            var host = RigScriptHostFactory.Create();
            RigCatalog.InstallModule(host, RigCatalog.Get(GasStationKit.InteriorRigKey));
            return host;
        }

        /// <summary>
        /// SHA-256 of a file with CRLF normalised to LF — the form git stores, and the one both of
        /// this kit's stamps are taken over.
        ///
        /// <para>⚠️ Not optional here. No <c>.gitattributes</c> rule covers <c>*.js</c> and
        /// <c>core.autocrlf</c> is on, so every rig in this kit is stored LF and checked out CRLF:
        /// hashing the working-tree bytes reports a mismatch against a stamp that is perfectly
        /// correct.</para>
        /// </summary>
        static string LfSha256(string absPath)
        {
            byte[] raw = File.ReadAllBytes(absPath);
            var lf = new System.Collections.Generic.List<byte>(raw.Length);
            for (int k = 0; k < raw.Length; k++)
            {
                if (raw[k] == (byte)'\r' && k + 1 < raw.Length && raw[k + 1] == (byte)'\n') continue;
                lf.Add(raw[k]);
            }

            using var sha = SHA256.Create();
            return string.Concat(sha.ComputeHash(lf.ToArray()).Select(b => b.ToString("x2")));
        }

        /// <summary>A bare host for <see cref="StationRegistrationProbe.Measure"/>, which does its own
        /// installing (the kit plus the counter-clockwise reference it measures against).</summary>
        static IRigScriptHost NewBareHost() => RigScriptHostFactory.Create();

        // ---- registration ------------------------------------------------------------------------

        [Test]
        public void Catalog_registers_both_rigs_clockwise_in_the_declared_load_order()
        {
            var shell = RigCatalog.Get(GasStationKit.RigKey);
            var room = RigCatalog.Get(GasStationKit.InteriorRigKey);

            Assert.That(shell.GlobalName, Is.EqualTo("StationIso"));
            Assert.That(shell.ScriptPath, Is.EqualTo(ShellRig));
            Assert.That(room.GlobalName, Is.EqualTo("StationInterior"));
            Assert.That(room.ScriptPath, Is.EqualTo(InteriorRig));

            Assert.That(shell.DeclaredConvention, Is.EqualTo(AzimuthConvention.Clockwise),
                "This kit rides the deck-loop turntable, which is MEASURED clockwise. Declaring the " +
                "fleet's counter-clockwise here would mirror all eight cells of every piece.");
            Assert.That(room.DeclaredConvention, Is.EqualTo(shell.DeclaredConvention),
                "The room paints into the shell's own cell through the shell's own camera; they " +
                "cannot turn opposite ways.");

            Assert.That(shell.Prerequisites, Is.EquivalentTo(new[] { "deckIsoSolid", "fuel" }),
                "gasStationRig throws by name on a missing IsoSolid or FuelIso, and the grade table " +
                "is a HARD dependency — the pump band and the jerry can under it must agree about " +
                "what red means.");
            Assert.That(room.Prerequisites, Is.EquivalentTo(new[] { "gasStation" }),
                "The interior asks the shell for shell(size) and cell('store', size). Declaring the " +
                "shell here is what makes installing this one key bring the whole kit up in order.");
        }

        /// <summary>
        /// The drop shipped its own <c>isoSolid.js</c> and <c>fuelRig.js</c>. Both are the SAME FILES
        /// as globals this repo already registers, so the kit's copies are gitignored and the catalog
        /// points at the registered ones. If a future drop ever diverges, this goes red and the
        /// prerequisite has to be reconsidered rather than silently rendering different art.
        ///
        /// <para>⚠️ Compared LF-normalised. No <c>.gitattributes</c> rule covers <c>*.js</c> and
        /// <c>core.autocrlf</c> is on, so the checked-out registered copies are CRLF while the drop's
        /// are LF: a raw byte comparison reports a drift that does not exist.</para>
        /// </summary>
        [Test]
        public void The_drops_copies_of_registered_globals_are_byte_identical_to_them()
        {
            foreach (var (dropRel, registeredRel, global) in new[]
                     {
                         (KitDir + "/rig/isoSolid.js", "docs/art/rigs/deck-loop-kit/Art/isoSolid.js", "IsoSolid"),
                         (KitDir + "/rig/fuelRig.js", "docs/art/rigs/fuel-storage-kit/fuelRig.js", "FuelIso"),
                     })
            {
                if (!File.Exists(Abs(dropRel)))
                {
                    Assert.Ignore($"The drop's copy of {global} is gitignored; materialise it to run " +
                                  $"this (see {KitDir}/IMPORT.md).");
                    return;
                }

                Assert.That(LfSha256(Abs(dropRel)), Is.EqualTo(LfSha256(Abs(registeredRel))),
                    $"The drop's {global} source has DIVERGED from the copy this repo registers. Two " +
                    "live versions of one global is a refusal, not a merge — edit one and the other " +
                    "silently keeps rendering the old art.");
            }
        }

        // ---- the oracle --------------------------------------------------------------------------

        /// <summary>
        /// ⭐ THE CONTROL. Regenerate the committed sidecar from the live rigs and require it back
        /// byte-for-byte.
        ///
        /// <para>The export tool that made it is browser-only (it hashes over SubtleCrypto and saves
        /// through an anchor element), but its serialisation is plain
        /// <c>JSON.stringify(obj, null, 1)</c> and its stamp placement is "straight after
        /// <c>exportSymbol</c>" — both reproducible here. What this pins, all at once: the host loads
        /// the four files in an order that produces the right art; the committed JSON is generated
        /// and not hand-edited; both stamps are in canonical position; and the bake's grade list is
        /// the one the sidecar describes. Without the reference sheets this drop did not ship, this
        /// is the only thing that makes the rest of these measurements admissible.</para>
        /// </summary>
        [Test]
        public void The_committed_sidecar_regenerates_from_the_rigs_byte_for_byte()
        {
            using var host = NewHost();

            string js = $@"(function(){{
              var SHA='{LfSha256(Abs(ShellRig))}', ISHA='{LfSha256(Abs(InteriorRig))}';
              function stamp(o){{ var out={{}}, placed=false;
                for (var k in o) {{
                  if (k==='derivedFromRigSha256'||k==='interiorDerivedFromRigSha256') continue;
                  out[k]=o[k];
                  if (!placed && k==='exportSymbol') {{
                    out.derivedFromRigSha256=SHA; out.interiorDerivedFromRigSha256=ISHA; placed=true; }}
                }}
                return out; }}
              return JSON.stringify(stamp({G}.gameplayAll({{grades:[{BakedGradesJs}]}})), null, 1);
            }})()";

            string regenerated = host.EvaluateString(js).Replace("\r\n", "\n");
            string committed = File.ReadAllText(Abs(Sidecar)).Replace("\r\n", "\n").TrimEnd('\n');

            Assert.That(regenerated, Is.EqualTo(committed),
                "The committed sidecar no longer reproduces from the rigs it names. Either a rig " +
                "changed without a re-bake, the JSON was hand-edited (it is GENERATED — never edit " +
                "it), or the grade list the bake uses is no longer the one it was generated with.");
        }

        /// <summary>
        /// Both stamps name the bytes they were taken over. An ABSENT stamp is a refusal, not a
        /// mismatch — so this checks presence as well as value.
        /// </summary>
        [Test]
        public void Both_sidecar_stamps_match_the_rig_files_they_name()
        {
            string json = File.ReadAllText(Abs(Sidecar));

            foreach (var (field, rig) in new[]
                     {
                         ("derivedFromRigSha256", ShellRig),
                         ("interiorDerivedFromRigSha256", InteriorRig),
                     })
            {
                string marker = $"\"{field}\": \"";
                int at = json.IndexOf(marker, StringComparison.Ordinal);
                Assert.That(at, Is.GreaterThanOrEqualTo(0),
                    $"The sidecar carries no {field}. An absent stamp is refused exactly like a stale " +
                    "one — there is nothing to check the rig against.");

                string claimed = json.Substring(at + marker.Length, 64);
                Assert.That(claimed, Is.EqualTo(LfSha256(Abs(rig))),
                    $"{field} does not match {rig}. A stamp that no longer matches its rig is the " +
                    "defect the field exists to catch: re-bake the sidecar, never edit the JSON.");
            }
        }

        // ---- handedness --------------------------------------------------------------------------

        /// <summary>
        /// Measured against <c>utilityIso</c> — registered counter-clockwise — in ONE host, on the
        /// UN-SQUASHED ground plane, and read two independent ways.
        ///
        /// <para>⚠️ The screen-space rotation per dir step is ∓46.7525° for the two families: SAME
        /// MAGNITUDE, opposite sign only. A probe comparing magnitudes calls two opposite-turning
        /// kits identical, which is why that number is measured here too and asserted to be useless.</para>
        /// </summary>
        [Test]
        public void The_turntable_steps_clockwise_and_the_screen_angle_cannot_tell_you_that()
        {
            using var host = NewBareHost();
            var r = StationRegistrationProbe.Measure(host);

            Assert.That(r.Measured, Is.EqualTo(AzimuthConvention.Clockwise));
            Assert.That(r.GroundStepPerDir, Is.EqualTo(-45.0).Within(1e-6),
                "The kit's own project(), un-squashed.");
            Assert.That(r.SlotPairGroundStepPerDir, Is.EqualTo(-45.0).Within(1e-6),
                "The island's slot0→slot3 mounts — a real abeam pair in the geometry rather than an " +
                "abstract axis. Two routes, one answer.");
            Assert.That(r.ReferenceGroundStepPerDir, Is.EqualTo(45.0).Within(1e-6),
                "utilityIso, the counter-clockwise reference, in the same host under the same sign " +
                "convention. Without it this is an absolute claim about a camera basis.");

            Assert.That(Math.Abs(r.ScreenStepMean),
                        Is.EqualTo(Math.Abs(r.ReferenceScreenStepMean)).Within(1e-4),
                "THE TRAP, pinned: in raw screen space the two families are ∓46.7525° — the same " +
                "magnitude. Anyone who 'simplifies' the un-squash away gets a handedness test that " +
                "cannot distinguish them, and eight cells come out mirrored.");
        }

        /// <summary>
        /// The rig destructures <c>camBasis</c>/<c>proj</c> out of <c>IsoSolid</c>, so it should
        /// project through the registered turntable BY IDENTITY, not by resemblance.
        /// </summary>
        [Test]
        public void The_kit_shares_the_registered_turntable_by_identity()
        {
            using var host = NewBareHost();
            var r = StationRegistrationProbe.Measure(host);

            Assert.That(r.TurntableMaxDxPx, Is.LessThan(1e-6));
            Assert.That(r.TurntableDySpreadPx, Is.LessThan(1e-6),
                "A CONSTANT y residual would only mean a different assumed height. A VARYING one " +
                "means two turntables, and every mount the gameplay side reads would be off.");
        }

        // ---- the seam ------------------------------------------------------------------------------

        /// <summary>
        /// Seamless means ONE CELL. The room's quad and pivot must be the shell's own, at every
        /// storefront size — that identity is what lets the two sheets crop to their own ink (the
        /// room's is 34–47% smaller) and still register to the pixel.
        /// </summary>
        [Test]
        public void The_sales_floor_uses_the_storefronts_own_cell_and_pivot()
        {
            using var host = NewHost();

            foreach (string size in GasStationKit.InteriorSizes)
            {
                string js = $@"(function(){{
                  var a={I}.cell('{size}',{{}}),
                      b={G}.cell('store','{size}',{{size:'{size}',grades:[{BakedGradesJs}],wear:'working'}});
                  return [a.W,a.H,a.cx,a.cy,b.W,b.H,b.cx,b.cy].join(',');
                }})()";

                var v = host.EvaluateString(js).Split(',');
                Assert.That(new[] { v[0], v[1], v[2], v[3] }, Is.EqualTo(new[] { v[4], v[5], v[6], v[7] }),
                    $"The room's cell for '{size}' is no longer the shell's. Blit the room, blit the " +
                    "shell over it, and the doorway only lines up because there is one cell in play.");
            }
        }

        /// <summary>Interiors ashore do not move — there is no <c>rock()</c> here, and that is a law
        /// of the kit rather than an omission. Pinned so nobody adds one by analogy with the boat
        /// interiors, whose rooms ride their hull.</summary>
        [Test]
        public void The_interior_rig_publishes_no_rock_parameter()
        {
            using var host = NewHost();
            Assert.That(host.EvaluateBool($"typeof {I}.rock === 'undefined'"), Is.True,
                "A building is bolted to the ground. That one difference is why this is its own rig " +
                "rather than a boat-interior family member.");
        }

        // ---- the fold ------------------------------------------------------------------------------

        /// <summary>
        /// The 180° symmetry that half the island and canopy pixels rest on, re-measured in PIXELS.
        ///
        /// <para>⚠️ Not by hash. The canopies are byte-identical between <c>d</c> and <c>d+4</c>; the
        /// islands differ by one to four pixels of tens of thousands — rasteriser tie-breaking on a
        /// symmetric kerb. A hash comparison calls those islands 5- and 8-distinct, and believing it
        /// costs 12.8 MB of duplicate art.</para>
        /// </summary>
        [Test]
        public void Folded_pieces_are_still_the_same_picture_half_a_turn_apart()
        {
            using var host = NewBareHost();
            var r = StationRegistrationProbe.Measure(host);

            Assert.That(r.Fold180WorstPixelDelta,
                        Is.LessThanOrEqualTo(GasStationKit.Fold180TolerancePixels),
                $"{r.Fold180WorstPiece} is no longer 180°-symmetric, so folding it to four facings " +
                "would show the wrong side. Move its type out of Fold180Types and re-bake at eight " +
                "rather than raising the tolerance.");
        }

        // ---- the three documented-name traps ---------------------------------------------------------

        /// <summary>
        /// All three of this kit's live traps are in its own DOCUMENTATION, and every one of them
        /// renders rather than throwing. Pinned so the baker's defences are known to still be needed
        /// — and so the day a drop fixes one, this says which.
        /// </summary>
        [Test]
        public void The_kits_own_documentation_names_three_things_that_render_the_wrong_piece()
        {
            using var host = NewBareHost();
            var r = StationRegistrationProbe.Measure(host);

            Assert.That(r.CardlockNameDefectPresent, Is.True,
                "The rig's header table calls the seventh dispenser 'sCardlock'; the size key is " +
                "'sLock'. While that holds, a build table copied from the documentation bakes a " +
                "25×52 KERB POST under the cardlock's name and reports success.");

            Assert.That(r.ReadmeRenderSignatureDefectPresent, Is.True,
                "The export README documents render(type, size, dir, opts); the rig is " +
                "render(type, dir, opts). Called the README's way the size string lands where dir " +
                "belongs and it renders — it does not throw.");

            Assert.That(r.InteriorSectionsObjectDefectPresent, Is.True,
                "The interior README documents gameplaySections({size}); the rig's own call site " +
                "passes a bare string, and the object form returns the PAY BOOTH for every size. That " +
                "one is load-bearing — it writes the storefront's SOLE and INTERACT into the sidecar.");

            Assert.That(r.SilentSizeFallbackPresent, Is.True);
            Assert.That(r.UnknownTypeThrows, Is.True,
                "The asymmetry underneath all three: an unknown TYPE throws, an unknown SIZE does not.");
        }

        /// <summary>
        /// ⭐ The fuel kit's worst defect, checked for here rather than assumed absent: an omitted
        /// render option must be the DEFAULT, not a phantom state that shares the default's
        /// geometry-cache key. Measured on every piece in BOTH call orders, because an
        /// order-dependent defect is invisible to a probe that only ever calls one way round.
        /// </summary>
        [Test]
        public void An_omitted_option_is_the_default_and_not_a_phantom_state()
        {
            using var host = NewBareHost();
            var r = StationRegistrationProbe.Measure(host);

            Assert.That(r.OptionsFunnelThroughResolve, Is.True,
                "resolve() funnels every option before both wearPass and the cache key, so an " +
                "omission and an explicit default are one picture under one key. If this goes red, " +
                "the fuel kit's phantom-state defect has appeared here and the catalog comment " +
                "saying otherwise is now wrong.");
        }

        // ---- the build table ---------------------------------------------------------------------------

        [Test]
        public void The_build_table_asks_for_exactly_the_21_pieces_the_rig_draws()
        {
            using var host = NewBareHost();
            var r = StationRegistrationProbe.Measure(host);

            Assert.DoesNotThrow(() => StationRegistrationProbe.AssertVocabularyMatches(host, G, r));

            Assert.That(GasStationKit.Sizes.Sum(kv => kv.Value.Length), Is.EqualTo(21));
            Assert.That(GasStationKit.Builds.Count, Is.EqualTo(23),
                "21 exterior pieces plus the two sales floors.");
            Assert.That(GasStationKit.Sizes["dispenser"], Does.Contain("sLock"));
            Assert.That(GasStationKit.Sizes["dispenser"], Does.Not.Contain("sCardlock"),
                "The name the rig's own header uses, which resolves to a different machine.");
        }

        /// <summary>
        /// The published grade ids are a fixed contract the economy lane builds against. The rig's
        /// internal key for stove oil is <c>kero</c>; the published id is <c>stove_oil</c>.
        /// </summary>
        [Test]
        public void The_five_grade_ids_are_the_published_contract()
        {
            using var host = NewHost();

            Assert.That(host.EvaluateString($"{G}.GRADE_ORDER.join(',')"),
                        Is.EqualTo("gas,diesel,mixed,oil,kero"));
            Assert.That(GasStationKit.RigGradeKeys.Select(GasStationKit.GradeId).ToArray(),
                        Is.EqualTo(new[] { "gas", "diesel", "mixed", "oil", "stove_oil" }));
        }

        // ---- packing -------------------------------------------------------------------------------------

        /// <summary>
        /// Every planned sheet must pack inside the import cap in SOME layout. A sheet over the cap
        /// bakes fine and then imports silently downscaled with the sprite count still correct, which
        /// poisons every slice rect — so this is checked from the live cells, not from the contract a
        /// bake wrote.
        /// </summary>
        [Test]
        public void Every_planned_sheet_packs_inside_the_import_cap()
        {
            using var host = NewHost();

            foreach (var b in GasStationKit.Builds)
            {
                string opts = $"{{size:'{b.Size}',grades:[{BakedGradesJs}],wear:'working'}}";
                string expr = b.Interior
                                  ? $"{I}.cell('{b.Size}',{opts})"
                                  : $"{G}.cell('{b.Type}','{b.Size}',{opts})";

                int w = (int)host.EvaluateNumber($"{expr}.W");
                int h = (int)host.EvaluateNumber($"{expr}.H");
                int facings = b.Interior
                                  ? GasStationKit.Facings
                                  : GasStationKit.FacingsFor(b.Type);

                // The UNCROPPED cell is the upper bound; the bake crops before packing, so a layout
                // that fits here fits after cropping too.
                Assert.That(GasStationKit.Columns(facings, w, h), Is.GreaterThan(0),
                    $"{b.Key} ({w}×{h} × {facings}) will not pack under the " +
                    $"{GasStationKit.ImportSizeCap} px cap in any layout.");
            }
        }

        [Test]
        public void The_column_solver_prefers_one_strip_and_folds_only_when_it_must()
        {
            // A small piece stays the familiar single strip.
            Assert.That(GasStationKit.Columns(8, 50, 78), Is.EqualTo(8));

            // The C-store cannot: 8 × 556 is 4448 px. Two columns of four rows is 1112 × 1724.
            Assert.That(GasStationKit.Columns(8, 556, 431), Is.EqualTo(2),
                "⚠️ Widest-that-fits alone answers THREE here (1668 × 1293, which fits) — and a 3×3 " +
                "grid holds NINE slots for eight facings, so a fully transparent 556×431 cell ships " +
                "as 239,636 dead pixels no sprite rect ever names. An exact divisor comes first.");

            // The sales floor is smaller and four columns divide eight exactly: 1792 × 716.
            Assert.That(GasStationKit.Columns(8, 448, 358), Is.EqualTo(4));

            // Nothing fits when one cell alone is over the cap.
            Assert.That(GasStationKit.Columns(8, 4096, 64), Is.EqualTo(0));
        }

        /// <summary>
        /// No sheet may carry a slot that no facing occupies — the blank-cell check, stated as a
        /// property rather than as the one case that caught it.
        /// </summary>
        [Test]
        public void No_committed_sheet_carries_a_blank_slot()
        {
            var contract = GasStationSheetSlicer.LoadContract();
            if (contract == null)
                Assert.Ignore("The kit has not been baked in this checkout.");

            foreach (var s in contract.sheets)
                Assert.That(s.columns * s.rows, Is.EqualTo(s.facings),
                    $"{s.key} packs {s.facings} facings into {s.columns}×{s.rows} = " +
                    $"{s.columns * s.rows} slots. The spare cells are transparent pixels nothing " +
                    "names, and a blank cell on a sheet later reads as a missing facing.");
        }

        /// <summary>
        /// A folded sheet's four cells cover all EIGHT facings, by <c>dir % 4</c>. This is the
        /// difference between a fold and a decimation, and getting it wrong shows the wrong side of a
        /// canopy for half the compass.
        /// </summary>
        [Test]
        public void A_folded_sheet_maps_all_eight_facings_onto_its_four_cells()
        {
            var folded = new GasStationKit.SheetEntry { facings = 4, columns = 4, rows = 1 };
            Assert.That(Enumerable.Range(0, 8).Select(folded.CellForDir).ToArray(),
                        Is.EqualTo(new[] { 0, 1, 2, 3, 0, 1, 2, 3 }),
                "dir % 4 — the fold. A decimation would be dirs 0·2·4·6, which on a 180°-symmetric " +
                "piece renders only two distinct pictures.");

            var full = new GasStationKit.SheetEntry { facings = 8, columns = 2, rows = 4 };
            Assert.That(Enumerable.Range(0, 8).Select(full.CellForDir).ToArray(),
                        Is.EqualTo(new[] { 0, 1, 2, 3, 4, 5, 6, 7 }));

            // Grid addressing: left to right, then top to bottom.
            Assert.That(Enumerable.Range(0, 8).Select(full.ColumnOf).ToArray(),
                        Is.EqualTo(new[] { 0, 1, 0, 1, 0, 1, 0, 1 }));
            Assert.That(Enumerable.Range(0, 8).Select(full.RowOf).ToArray(),
                        Is.EqualTo(new[] { 0, 0, 1, 1, 2, 2, 3, 3 }));
        }

        /// <summary>The pivot is a projected POINT, so the y term is <c>(H − pivotY)/H</c> per
        /// ADR 0026 — not a chosen row and not the sprite's centre.</summary>
        [Test]
        public void The_normalised_pivot_follows_the_hull_convention()
        {
            var e = new GasStationKit.SheetEntry { cellW = 100, cellH = 200, pivotX = 50, pivotY = 180 };
            Assert.That(e.NormalisedPivot.x, Is.EqualTo(0.5f).Within(1e-6f));
            Assert.That(e.NormalisedPivot.y, Is.EqualTo(0.1f).Within(1e-6f));
        }

        // ---- what actually shipped ---------------------------------------------------------------------

        /// <summary>
        /// The committed sheets, checked the way the silent-downscale bug is actually caught: native
        /// resolution AND the contract's pivots, not just the sprite count. Multiple-mode with zero
        /// rects reads as "imported" everywhere else.
        /// </summary>
        [Test]
        public void The_committed_sheets_imported_at_native_resolution_with_the_contract_pivots()
        {
            var contract = GasStationSheetSlicer.LoadContract();
            if (contract == null)
                Assert.Ignore("The kit has not been baked in this checkout.");

            Assert.That(contract.sheets.Length, Is.EqualTo(GasStationKit.Builds.Count),
                "The contract describes a different number of sheets than the build table plans.");

            string report = GasStationSheetSlicer.Verify(contract, out bool ok);
            Assert.That(ok, Is.True, report);
        }

        /// <summary>
        /// The pixel-art import lock, read back off what shipped: PPU 32, Point, uncompressed, no
        /// mips. Scale is honest — a sprite's pixels are its metres × 32.
        /// </summary>
        [Test]
        public void The_committed_sheets_carry_the_locked_import_settings()
        {
            var contract = GasStationSheetSlicer.LoadContract();
            if (contract == null)
                Assert.Ignore("The kit has not been baked in this checkout.");

            foreach (var sheet in contract.sheets)
            {
                string path = $"{GasStationKit.OutputFolder}/{sheet.key}.png";
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                Assert.That(importer, Is.Not.Null, $"{path} has no TextureImporter.");

                Assert.That(importer.spritePixelsPerUnit, Is.EqualTo(32f), path);
                Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point), path);
                Assert.That(importer.textureCompression,
                            Is.EqualTo(TextureImporterCompression.Uncompressed), path);
                Assert.That(importer.mipmapEnabled, Is.False, path);
                Assert.That(importer.spriteImportMode, Is.EqualTo(SpriteImportMode.Multiple),
                    $"{path}: Multiple is what the slicer needs — and the property write is lost if " +
                    "anything follows it with SetTextureSettings of a stale settings object.");
            }
        }
    }
}
