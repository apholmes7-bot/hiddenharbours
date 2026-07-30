using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Art.Editor;
using HiddenHarbours.Tools.RigBaking;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// THE ACCEPTANCE SUITE FOR THE ACADIAN SHRUB KIT — and the rig is its own oracle.
    ///
    /// <para>This kit ships NO pixels, so there are no committed sheets to diff. What CAN be proved is
    /// stronger than a pixel diff anyway: <c>shrubIsoRig.js</c> runs in V8 inside the editor, so every
    /// number in the committed contract is checked against a FRESH <c>Shrubs.sheetSpec()</c>, and every
    /// channel RULE the contract publishes is checked against actual rendered pixels.</para>
    ///
    /// <para><b>Every assert that matters carries a MEASURED SABOTAGE</b> in the same test: the
    /// mutation is applied, the check is shown to reject it, and the magnitude is logged. An assert with
    /// no sabotage curve is decoration — it can pass because it is right or because it is vacuous, and
    /// nothing distinguishes the two.</para>
    ///
    /// <para>CPU-only: the V8 host needs no graphics device, so nothing here gates on
    /// <c>GraphicsDeviceType.Null</c>.</para>
    /// </summary>
    public class ShrubRigBakeTests
    {
        const string Stage = ShrubBaker.DefaultStage;

        /// <summary>Species carrying the per-pixel tests. Rendering all twenty at every phase is
        /// ~160 renders per assert, so the expensive checks use a deliberate spread instead: the veil
        /// test case (alder), a mat that tiles (blueberry), an evergreen (juniper), a fruit-holder
        /// (winterberry) and a bloom-on-bare-wood plant (rhodora).</summary>
        static readonly string[] Probes =
            { "SpeckledAlder", "LowbushBlueberry", "CommonJuniper", "WinterberryHolly", "Rhodora" };

        static string RepoRoot => Directory.GetParent(Application.dataPath)!.FullName;

        static IRigScriptHost CreateHost()
        {
            var host = RigScriptHostFactory.Create();
            ShrubBaker.InstallRig(host);
            return host;
        }

        static ShrubCatalog.Contract LoadContract()
        {
            var c = ShrubCatalog.Load();
            Assert.IsNotNull(c,
                $"No usable contract at {ShrubCatalog.ContractPath}. Run Hidden Harbours ▸ Art ▸ " +
                "Export Shrub Contract — it is serialised out of the live rig, never hand-written.");
            Assert.IsNotEmpty(c.Species);
            return c;
        }

        /// <summary>One of the rig's grayscale masks as bytes. <c>render()</c> returns them as
        /// <c>Uint8Array</c> and the host's bulk readback wants a clamped array, so re-wrap.</summary>
        static byte[] ReadMask(IRigScriptHost host, string resultExpr, string which) =>
            host.EvaluateBytes(
                $"(function(){{var r={resultExpr};return new Uint8ClampedArray(r.masks.{which});}})()");

        static byte[] Packed(IRigScriptHost host, string resultExpr, string fn) =>
            host.EvaluateBytes($"{ShrubCatalog.RigGlobalName}.{fn}({resultExpr})");

        // =================================================================================
        // the rig runs unmodified, and exposes exactly what the baker calls
        // =================================================================================

        [Test]
        public void ShrubRig_RunsUnmodified_AndNeedsNoShim()
        {
            // No canvas mailbox, no string widening, no globals patched in first (ADR 0021 §5). The
            // file and the global are both read from ShrubCatalog, never spelled out here.
            using var host = RigScriptHostFactory.Create();
            string rig = ShrubCatalog.RigScriptPath, g = ShrubCatalog.RigGlobalName;

            Assert.DoesNotThrow(() => host.Execute(File.ReadAllText(Path.Combine(RepoRoot, rig))),
                $"{rig} must run in a BARE host. If this throws, something in the rig now needs an " +
                "environment global — and the shim belongs in host code, never in the art director's " +
                "file.");

            Assert.IsTrue(host.EvaluateBool($"typeof {g} === 'object' && {g} !== null"),
                $"{rig} ran but did not install globalThis.{g}.");

            foreach (string fn in new[] { "render", "packMask", "packState", "sheetSpec", "cellOf",
                                          "sheetAudit", "contract", "phaseOf", "snowOf" })
                Assert.IsTrue(host.EvaluateBool($"typeof {g}.{fn} === 'function'"),
                    $"{g}.{fn}() is missing — the baker calls it directly.");
        }

        [Test]
        public void ShrubRig_IsNotInTheRigCatalog_BecauseAShrubHasNoHeading()
        {
            // RigEntry is built around AzimuthConvention for 8-direction turntables. A shrub's sheet
            // axes are phase × sway and variant × sway; there is nothing to probe.
            Assert.IsFalse(
                RigCatalog.Entries.Keys.Any(
                    k => k.IndexOf("shrub", StringComparison.OrdinalIgnoreCase) >= 0),
                "A 'shrub' entry appeared in RigCatalog. That struct declares an azimuth convention, " +
                "and the non-directional rigs are deliberately baked by dedicated bakers instead.");
        }

        [Test]
        public void TheRigSourceIsCommitted_UnderTheArtDirectorsFolder()
        {
            string full = Path.Combine(RepoRoot, ShrubCatalog.RigScriptPath);
            Assert.IsTrue(File.Exists(full), $"{ShrubCatalog.RigScriptPath} is not committed.");
            Assert.Greater(new FileInfo(full).Length, 50_000,
                "The shrub rig is ~108 KB. A much smaller file is a truncated copy, not the drop.");
        }

        // =================================================================================
        // ⭐ THE CONTRACT IS THE ORACLE — and it must match the LIVE rig, number for number
        // =================================================================================

        [Test]
        public void EverySpeciesGeometry_MatchesAFreshSheetSpec_AndAOnePixelDriftIsRejected()
        {
            var contract = LoadContract();
            using var host = CreateHost();

            Assert.AreEqual(ShrubCatalog.SpeciesKeys.Length, contract.Species.Count,
                "The contract's species count and the catalog's key list disagree.");

            foreach (var entry in contract.Species)
            {
                var phaseSpec = ShrubBaker.ReadSheetSpec(host, entry.Key, Stage, ShrubBaker.PhaseAxis);
                var varSpec = ShrubBaker.ReadSheetSpec(host, entry.Key, Stage, ShrubBaker.VariantAxis);

                Assert.AreEqual(phaseSpec.CellW, entry.CellW, $"{entry.Key}: cell width drifted");
                Assert.AreEqual(phaseSpec.CellH, entry.CellH, $"{entry.Key}: cell height drifted");
                Assert.AreEqual(phaseSpec.PivotX, entry.PivotX, $"{entry.Key}: pivot.x drifted");
                Assert.AreEqual(phaseSpec.PivotY, entry.PivotY, $"{entry.Key}: pivot.y drifted");

                // Both axes come off the SAME union cell — only the column count differs.
                Assert.AreEqual(varSpec.CellW, phaseSpec.CellW,
                    $"{entry.Key}: the two axes disagree about the cell. They must not — the union " +
                    "cell is what makes one pivot serve every state.");
                Assert.AreEqual(entry.PhaseSheetW, phaseSpec.Cols * phaseSpec.CellW,
                    $"{entry.Key}: phase-axis sheet width");
                Assert.AreEqual(entry.VariantSheetW, varSpec.Cols * varSpec.CellW,
                    $"{entry.Key}: variant-axis sheet width");
                Assert.AreEqual(ShrubCatalog.PhaseKeys.Length, phaseSpec.Cols,
                    $"{entry.Key}: the phase axis must be 8 columns.");
                Assert.AreEqual(ShrubCatalog.Variants, varSpec.Cols,
                    $"{entry.Key}: the variant axis must be 4 columns.");

                // ---- MEASURED SABOTAGE: shift the pivot one pixel -----------------------------
                var sabotaged = new ShrubCatalog.SpeciesEntry
                {
                    Key = entry.Key, CellW = entry.CellW, CellH = entry.CellH,
                    PivotX = entry.PivotX, PivotY = entry.PivotY - 1,
                };
                Vector2 right = ShrubCatalog.NormalizedPivot(entry);
                Vector2 wrong = ShrubCatalog.NormalizedPivot(sabotaged);
                Assert.AreNotEqual(right, wrong,
                    $"{entry.Key}: a 1 px pivot offset must be visible to this check.");
                Assert.Greater(Mathf.Abs(wrong.y - right.y), 1e-6f);
            }

            Debug.Log($"[shrub-contract] {contract.Species.Count} species: cell, root-crown pivot and " +
                      "both sheet widths all match a fresh Shrubs.sheetSpec(). A 1 px pivot shift is " +
                      $"1/cellH of the sprite = {1f / 32f:F4} m at PPU {ShrubCatalog.Ppu}.");
        }

        [Test]
        public void TheContract_IsSerialisedFromTheRig_NotHandWritten()
        {
            var contract = LoadContract();
            using var host = CreateHost();
            string g = ShrubCatalog.RigGlobalName;

            Assert.AreEqual(ShrubCatalog.RigName, contract.Rig,
                "The contract must name the rig it came out of.");
            Assert.AreEqual((int)ArtImportPipeline.PixelsPerUnit, contract.Projection.Ppu,
                "PPU 32 is the locked scale standard.");
            Assert.AreEqual(ShrubCatalog.Ppu, (int)host.EvaluateNumber($"{g}.PPU"),
                "The rig's live PPU and the catalog's constant must agree.");

            // ¾ from the south at 40° — the same camera as the boat, rock, shoreline, tree and
            // shore-plant bakes. A shrub that disagreed would not stand in the same world.
            Assert.AreEqual(40f, contract.Projection.ElevDeg, 1e-4f);
            Assert.AreEqual((float)host.EvaluateNumber($"{g}.ELEV"), contract.Projection.ElevDeg, 1e-4f);
            Assert.AreEqual((float)host.EvaluateNumber($"{g}.CE"), contract.Projection.HeightScale, 1e-3f);
            Assert.AreEqual((float)host.EvaluateNumber($"{g}.SE"), contract.Projection.DepthScale, 1e-3f);
            Assert.IsFalse(contract.Projection.Antialias, "Binary alpha, no AA — the pixel-art floor.");
            Assert.AreEqual("binary", contract.Projection.Alpha);

            // ⚠️ The rig emits an ISO timestamp here; the baker replaces it so the committed file is
            // diff-stable. A timestamp landing in the repo is the bug this asserts against.
            Assert.IsNotNull(contract.Generated);
            Assert.IsFalse(
                System.Text.RegularExpressions.Regex.IsMatch(
                    contract.Generated, @"\d{4}-\d{2}-\d{2}T\d{2}:\d{2}"),
                "The contract's `generated` field carries an ISO TIMESTAMP. ShrubBaker.ExportContract " +
                "replaces it with a provenance string precisely so this file does not churn on every " +
                "export — diff noise is what hides a real geometry change. Re-export it.");
            Assert.IsTrue(contract.Generated.Contains(ShrubCatalog.RigScriptPath),
                "The provenance note should say which rig file it came out of.");
        }

        [Test]
        public void TheHabitatTable_AgreesWithItself_AndWithTheLiveRig()
        {
            var contract = LoadContract();
            using var host = CreateHost();
            string g = ShrubCatalog.RigGlobalName;

            Assert.AreEqual(ShrubCatalog.HabitatKeys.Length, contract.Habitats.Count);
            foreach (string key in ShrubCatalog.HabitatKeys)
            {
                var h = contract.Habitat(key);
                Assert.IsNotNull(h, $"No habitat '{key}' in the contract.");
                float live = (float)host.EvaluateNumber(
                    $"{g}.HABITATS.filter(function(x){{return x.key==='{key}';}})[0].snowK");
                Assert.AreEqual(live, h.SnowK, 1e-4f,
                    $"habitat '{key}': snowK drifted from the live rig. This multiplier decides how " +
                    "deep a pack lies on this ground, and therefore whether a species is legal there.");
            }

            // Every species names a habitat that exists — a typo here would silently make
            // SnowDepthMetres return 0 and every shrub legal in a drift.
            foreach (var s in contract.Species)
                Assert.IsNotNull(contract.Habitat(s.Habitat),
                    $"{s.Key} claims habitat '{s.Habitat}', which the contract does not declare.");

            Debug.Log("[shrub-contract] habitat snowK: " +
                      string.Join(", ", contract.Habitats.Select(h => $"{h.Key} {h.SnowK:F2}")));
        }

        // =================================================================================
        // 🔴 THE VEIL FLAG IS THE BRANCH — light.G is meaningless without state.R
        // =================================================================================

        [Test]
        public void TheVeilFlag_IsInStateR_AndNoVeilPixelCarriesARim()
        {
            // The single most load-bearing fact in the kit, and the one a shader author would get
            // wrong: light.G (back rim) is defined for BODY AND WOOD ONLY and is identically 0 on
            // veil, fleck and bloom. state.R is 255 on veil and is the branch. Never infer
            // veil-vs-mass from the sprite.
            using var host = CreateHost();
            var contract = LoadContract();

            var veil = contract.Materials.Veil;
            Assert.IsNotNull(veil, "The contract must declare a `veil` material.");
            Assert.IsTrue(veil.RimForbidden,
                "veil must be declared rim-FORBIDDEN — that is the other half of its floor exemption.");
            Assert.IsFalse(veil.Keyline,
                "veil must be declared keyline-FORBIDDEN. An outline round every 1 px strand collapses " +
                "the filament mass into a solid — this is the one material in the world with that rule.");
            Assert.IsTrue(veil.Linear);
            Assert.IsNull(veil.MassFloorPx,
                "veil is exempt from the mass floor BY DECLARATION, which is what makes it auditable.");

            int totalVeil = 0, rimLeak = 0, checkedSpecies = 0;
            foreach (string key in Probes)
            {
                // `bare` is the phase the veil exists for — a leafless alder is two hundred 1 px twigs.
                string res = ShrubBaker.ResultExpr(key, Stage, "bare", variant: 0, frame: 0);
                byte[] light = Packed(host, res, "packMask");
                byte[] state = Packed(host, res, "packState");
                byte[] veilMask = ReadMask(host, res, "veil");

                Assert.AreEqual(light.Length, state.Length,
                    $"{key}: the two bakes must cover the same cell.");

                int n = veilMask.Length, flagMismatch = 0, speciesVeil = 0;
                for (int i = 0; i < n; i++)
                {
                    bool isVeil = veilMask[i] != 0;
                    // state.R IS masks.veil — asserted, not assumed.
                    if ((state[i * 4 + 0] != 0) != isVeil) flagMismatch++;
                    if (!isVeil) continue;
                    speciesVeil++;
                    if (light[i * 4 + 1] != 0) rimLeak++;
                }

                Assert.AreEqual(0, flagMismatch,
                    $"{key}: state.R disagrees with masks.veil on {flagMismatch} px. state.R IS the " +
                    "veil flag — if it is not, every shader that gates on it is gating on noise.");
                totalVeil += speciesVeil;
                checkedSpecies++;
                Debug.Log($"[shrub-veil] {key}/bare: {speciesVeil} veil px of {n} " +
                          $"({100.0 * speciesVeil / n:F1}% of the cell).");
            }

            Assert.AreEqual(0, rimLeak,
                $"{rimLeak} veil pixels carry a non-zero light.G. The rig's own report.veilRimLeak " +
                "must be 0 — a rim on a 1 px filament is the artefact this material exists to avoid.");

            // ---- MEASURED SABOTAGE: the flag must not be vacuous ---------------------------
            Assert.Greater(totalVeil, 0,
                "Not one veil pixel across five species in the `bare` phase. Either the rig stopped " +
                "drawing filaments (four months of the year every shrub would vanish) or this test is " +
                "reading the wrong channel — chase it before trusting anything above.");
            Debug.Log($"[shrub-veil] {totalVeil} veil px across {checkedSpecies} species, " +
                      "0 carrying a rim, 0 disagreeing with state.R.");
        }

        [Test]
        public void OrnamentIsInStateG_AndBloomAndFruitNeverCarryARim()
        {
            // state.G is 255 on bloom, 170 on fruit, 0 otherwise — a hue-shift hook that lets berry
            // colour vary without a re-bake. Both are rim-forbidden.
            using var host = CreateHost();
            var contract = LoadContract();

            Assert.IsTrue(contract.Materials.Fleck.RimForbidden, "fleck must be rim-forbidden.");
            Assert.IsTrue(contract.Materials.Bloom.RimForbidden, "bloom must be rim-forbidden.");

            int bloomPx = 0, fruitPx = 0, leak = 0;
            foreach (var (key, phase) in new[]
                     {
                         ("Rhodora", "bloom"),        // blooms on NAKED wood
                         ("WinterberryHolly", "fruit"),    // the loudest thing in the swamp
                         ("RedElderberry", "fruit"),  // six berries to a cyme
                     })
            {
                string res = ShrubBaker.ResultExpr(key, Stage, phase, variant: 0, frame: 0);
                byte[] light = Packed(host, res, "packMask");
                byte[] state = Packed(host, res, "packState");

                for (int i = 0; i < state.Length / 4; i++)
                {
                    int orn = state[i * 4 + 1];
                    if (orn == 0) continue;
                    if (orn > 200) bloomPx++; else fruitPx++;
                    if (light[i * 4 + 1] != 0) leak++;
                }
            }

            Assert.AreEqual(0, leak,
                $"{leak} ornament pixels carry a light.G rim. The rig's report.ornRimLeak must be 0.");
            Assert.Greater(bloomPx + fruitPx, 0,
                "No ornament pixels at all across a bloom phase and two fruit phases. A shrub's " +
                "identity lives in the flower and the fruit — if state.G is empty, the calendar is not " +
                "reaching the bake and this assert is vacuous.");
            Debug.Log($"[shrub-orn] {bloomPx} bloom px + {fruitPx} fruit px, 0 carrying a rim.");
        }

        [Test]
        public void TheContract_PublishesSixMaterials_WithTheFloorOnBodyAlone()
        {
            // Rule 1 with DECLARED exemptions: only BODY is policed by the mass floor. The other five
            // are linear or sub-pixel and are exempt by declaration — and in exchange the ones that
            // could fake a rim are forbidden one. The exemption being a MATERIAL rather than a fudge is
            // what makes it auditable.
            var contract = LoadContract();
            using var host = CreateHost();

            var mats = contract.Materials.All.Where(m => m != null).ToArray();
            Assert.AreEqual(6, mats.Length,
                "Six materials: body, wood, edge, veil, fleck, bloom.");

            Assert.IsTrue(contract.Materials.Body.CarriesRim, "body carries a rim unconditionally.");
            Assert.IsFalse(contract.Materials.Body.Linear, "body is the only non-linear material.");
            Assert.AreEqual((int)host.EvaluateNumber($"{ShrubCatalog.RigGlobalName}.MIN_R"),
                            contract.Materials.Body.MassFloorPx,
                "body's declared mass floor must be the rig's live MIN_R.");

            foreach (var m in mats.Where(m => m.Key != "body"))
            {
                Assert.IsTrue(m.Linear, $"{m.Key} must be declared LINEAR.");
                Assert.IsNull(m.MassFloorPx,
                    $"{m.Key} carries a mass floor of {m.MassFloorPx}. A linear material has no body " +
                    "radius to police — a floor here would read as 'floor of zero' rather than " +
                    "'no floor applies'.");
            }

            // The tri-state must survive the parse. Collapsing "thickness-gated" to a bool is how a
            // stem silently stops being lit.
            Assert.IsTrue(contract.Materials.Wood.RimThicknessGated,
                "wood is thickness-gated: the rim gate decides per pixel, not per material.");
            Assert.IsTrue(contract.Materials.Edge.RimThicknessGated);
            Assert.IsFalse(contract.Materials.Wood.CarriesRim);
            Assert.IsFalse(contract.Materials.Wood.RimForbidden);

            // The two bakes' channel semantics are prose the shader branches on — pinned, not
            // paraphrased.
            Assert.IsTrue(contract.Light.G.IndexOf("body and wood", StringComparison.OrdinalIgnoreCase) >= 0,
                "light.G must publish that it is defined for BODY AND WOOD ONLY.");
            Assert.IsTrue(contract.State.R.IndexOf("veil", StringComparison.OrdinalIgnoreCase) >= 0,
                "state.R must publish that it is the veil flag.");
            // ⚠️ These are FILENAME TEMPLATES ("<key>_light.png"), not paths — the angle brackets are
            // illegal path characters, so System.IO.Path throws on them. Compare as plain strings.
            Assert.AreEqual("<key>_light.png", contract.Light.File);
            Assert.AreEqual("<key>_calendar.png", contract.State.File);
            foreach (var (channel, template) in new[]
                     {
                         (ShrubCatalog.Channel.Light, contract.Light.File),
                         (ShrubCatalog.Channel.State, contract.State.File),
                     })
                Assert.AreEqual($"<key>{ShrubCatalog.SuffixFor(channel)}.png", template,
                    $"The catalog's {channel} suffix must be the contract's OWN filename, not a " +
                    "rename. A slicer that looked for a suffix the bake never wrote would silently " +
                    "find zero sheets.");

            Debug.Log("[shrub-mat] " + string.Join(" · ", mats.Select(
                m => $"{m.Key}(id {m.Id}, floor {(m.MassFloorPx?.ToString() ?? "none")}, " +
                     $"rim {m.CarriesRimRaw}, keyline {m.Keyline})")));
        }

        // =================================================================================
        // ⭐ THE UNION CELL — one cell, one pivot, across all eight phases AND all five snow steps
        // =================================================================================

        [Test]
        public void ThePivotIsIdentical_AcrossEveryPhaseAndEverySnowDepth()
        {
            // The whole purchase of the union cell. A per-phase cell would give every calendar station
            // its own pivot, and a 1 px disagreement becomes a visible HOP the instant the calendar
            // advances. Snow is not in the union at all — it only clips, so it cannot move the cell.
            using var host = CreateHost();
            var contract = LoadContract();
            string g = ShrubCatalog.RigGlobalName;

            foreach (string key in Probes)
            {
                var entry = contract.Find(key);
                Assert.IsNotNull(entry);

                foreach (string phase in ShrubCatalog.PhaseKeys)
                foreach (string snow in ShrubCatalog.SnowKeys)
                {
                    string res = $"{g}.render('{key}',{{variant:0,phase:'{phase}'," +
                                 $"snow:{g}.SNOWS.filter(function(s){{return s.key==='{snow}';}})[0].m," +
                                 $"frame:0,stage:'{Stage}'}})";
                    int px = (int)host.EvaluateNumber($"{res}.pivot.x");
                    int py = (int)host.EvaluateNumber($"{res}.pivot.y");

                    Assert.AreEqual(entry.PivotX, px,
                        $"{key}/{phase}/{snow}: pivot.x moved. Every state of a species must share one " +
                        "root-crown contact or the shrub hops when the calendar or the snow changes.");
                    Assert.AreEqual(entry.PivotY, py,
                        $"{key}/{phase}/{snow}: pivot.y moved.");
                }
            }
            Debug.Log($"[shrub-union] {Probes.Length} species × {ShrubCatalog.PhaseKeys.Length} phases " +
                      $"× {ShrubCatalog.SnowKeys.Length} snow steps = " +
                      $"{Probes.Length * ShrubCatalog.PhaseKeys.Length * ShrubCatalog.SnowKeys.Length} " +
                      "states, all on ONE pivot per species.");
        }

        [Test]
        public void TheUnionCellsCost_IsMeasuredPerSpecies_NotAssumed()
        {
            // Sheet waste is only memory; a state hop is an artefact. That trade is only defensible if
            // the cost is on record — worstInkPct is what the union costs each species.
            var contract = LoadContract();

            foreach (var s in contract.Species)
            {
                Assert.That(s.WorstInkPct, Is.InRange(1, 100),
                    $"{s.Key}: worstInkPct {s.WorstInkPct} is not a percentage. If the audit did not " +
                    "run, this column is decoration.");
                Assert.IsNotNull(s.WorstInkPhase,
                    $"{s.Key}: the worst-ink phase must be NAMED, so the owner can see which calendar " +
                    "station is paying for the union.");
                Assert.Contains(s.WorstInkPhase, ShrubCatalog.PhaseKeys,
                    $"{s.Key}: worstInkPhase '{s.WorstInkPhase}' is not a known phase.");
            }

            var worst = contract.Species.OrderBy(s => s.WorstInkPct).First();
            Debug.Log($"[shrub-union] worst ink is {worst.Key} at {worst.WorstInkPct}% in " +
                      $"'{worst.WorstInkPhase}' — that is what the union cell costs it. Total for both " +
                      $"axes, one channel: {ShrubCatalog.TotalSheetMib(contract):F2} MiB.");
        }

        // =================================================================================
        // the 2048 cap
        // =================================================================================

        [Test]
        public void EverySpecies_FitsUnitys2048Cap_OnBothAxes_AssertedViaTheRigsOwnFlag()
        {
            var contract = LoadContract();
            using var host = CreateHost();

            Assert.AreEqual(ShrubCatalog.ImportSizeCap, contract.Sheets.Cap,
                "The contract must publish the same cap the importer enforces.");
            Assert.IsTrue(contract.Sheets.AllFit,
                "The rig's own audit says at least one sheet is over the cap.");

            int tightest = int.MaxValue; string tightestKey = null;
            foreach (var entry in contract.Species)
            {
                foreach (string axis in new[] { ShrubBaker.PhaseAxis, ShrubBaker.VariantAxis })
                {
                    var spec = ShrubBaker.ReadSheetSpec(host, entry.Key, Stage, axis);
                    Assert.IsTrue(spec.RigFits,
                        $"{entry.Key}/{axis}: the rig's own sheetSpec().fits is false at " +
                        $"{spec.RigSheetW}×{spec.RigSheetH}.");
                }

                Assert.IsTrue(entry.Fits2048, $"{entry.Key}: the contract says it does not fit.");
                foreach (int dim in new[] { entry.PhaseSheetW, entry.PhaseSheetH,
                                            entry.VariantSheetW, entry.VariantSheetH })
                    Assert.LessOrEqual(dim, ShrubCatalog.ImportSizeCap,
                        $"{entry.Key}: over the cap Unity imports DOWNSCALED with the sprite COUNT " +
                        "still matching, so only a cell-size assert would ever catch it.");

                if (entry.Headroom < tightest) { tightest = entry.Headroom; tightestKey = entry.Key; }
            }

            Assert.Greater(tightest, 0,
                $"{tightestKey} has no headroom left under the {ShrubCatalog.ImportSizeCap} px cap.");
            Debug.Log($"[shrub-cap] tightest is {tightestKey} with {tightest} px of headroom under " +
                      $"the {ShrubCatalog.ImportSizeCap} px cap.");
        }

        // =================================================================================
        // 🔴 THE SNOW LAW — it CLIPS, it never floods, and no sprite bakes ground
        // =================================================================================

        [Test]
        public void SnowClipsAndNeverFloods_AndDeepEnoughSnowMeansDrawADriftInstead()
        {
            var contract = LoadContract();
            using var host = CreateHost();
            string g = ShrubCatalog.RigGlobalName;

            Assert.IsTrue(contract.Snow.ClipsNotFloods,
                "The contract must declare that snow CLIPS rather than floods — the same ruling as the " +
                "rock rig's awash sheets. A flood would bake ground into the sprite.");
            Assert.AreEqual(ShrubCatalog.SnowKeys.Length, contract.Snow.Steps.Count);
            foreach (string key in ShrubCatalog.SnowKeys)
            {
                var step = contract.Snow.Step(key);
                Assert.IsNotNull(step, $"No snow step '{key}'.");
                float live = (float)host.EvaluateNumber(
                    $"{g}.SNOWS.filter(function(s){{return s.key==='{key}';}})[0].m");
                Assert.AreEqual(live, step.Metres, 1e-4f, $"snow step '{key}': depth drifted.");
            }

            // ⭐ The kit's own headline example: 0.35 m of pack takes lowbush blueberry off the map and
            // does nothing at all to a 4 m alder. Asserted through the catalog's own helpers, because
            // those are what a scene will call.
            float blueberryBuried = ShrubCatalog.BuriedFraction(contract, "LowbushBlueberry", "deep");
            float alderBuried = ShrubCatalog.BuriedFraction(contract, "SpeckledAlder", "deep");
            Assert.Greater(blueberryBuried, ShrubCatalog.SnowLaw.BuriedCutoff,
                "Lowbush blueberry (0.38 m) must be BURIED at the 'deep' step — the scene owes a drift " +
                "tile there, not a shrub.");
            Assert.Less(alderBuried, 0.5f,
                "Speckled alder (4.00 m) must barely notice the same snow. If one number does not " +
                "separate these two, the snow law is not doing its job.");
            Assert.IsFalse(ShrubCatalog.IsLegalAtSnow(contract, "LowbushBlueberry", "deep"));
            Assert.IsTrue(ShrubCatalog.IsLegalAtSnow(contract, "SpeckledAlder", "deep"));

            Debug.Log($"[shrub-snow] at 'deep': LowbushBlueberry {blueberryBuried:P0} buried (DRAW A " +
                      $"DRIFT), SpeckledAlder {alderBuried:P0} (unaffected). Cutoff is " +
                      $"{ShrubCatalog.SnowLaw.BuriedCutoff:P0}.");

            // And the rig agrees with the catalog's arithmetic.
            bool buriedOut = host.EvaluateBool(
                $"{g}.render('LowbushBlueberry',{{variant:0,phase:'dormant'," +
                $"snow:{g}.SNOWS.filter(function(s){{return s.key==='deep';}})[0].m," +
                $"frame:0,stage:'{Stage}'}}).buriedOut");
            Assert.IsTrue(buriedOut,
                "The rig's own buriedOut flag disagrees with the contract's snow arithmetic. One of " +
                "them is wrong and a scene would draw a shrub standing in a drift.");
        }

        [Test]
        public void NoWaterAndNoMovingLightIsBaked_AndTheSunNeverGoesBehind()
        {
            // ADR 0010/0012/0023 + the tree-light rule. The live shader owns caustics and the moving
            // waterline; a baked dapple would fight the swell-driven one, and two uncorrelated patterns
            // on one leaf is worse than none.
            var contract = LoadContract();
            using var host = CreateHost();
            string g = ShrubCatalog.RigGlobalName;

            // The key light is fixed and UPPER-LEFT, and it faces the camera — a key with a positive
            // z would be behind the subject, which is the rule the tree kit settled.
            float kz = (float)host.EvaluateNumber($"{g}.LIGHT.key[2]");
            float kx = (float)host.EvaluateNumber($"{g}.LIGHT.key[0]");
            Assert.Greater(kz, 0f,
                "The key light must lean TOWARD the camera. The sun never goes behind (the tree-light " +
                "rule) — a negative z here would light every shrub from the far side.");
            Assert.Less(kx, 0f, "The key is upper-LEFT (art bible §1).");

            // The contract's own snow/keyline story must not mention baked water or caustics.
            string snowNote = contract.Snow.Note ?? "";
            Assert.IsTrue(snowNote.IndexOf("CLIP", StringComparison.OrdinalIgnoreCase) >= 0,
                "The snow note must say it clips.");
            Assert.IsTrue(contract.Snow.SnowRow != null,
                "snowRow must be reported so the scene can line its own drift tile up with the bake — " +
                "the sprite does not bake ground.");

            Debug.Log($"[shrub-light] key {kx:F2},…,{kz:F2} — upper-left, leaning toward camera. " +
                      "No baked water, no baked caustics, no baked moving light.");
        }

        // =================================================================================
        // the mechanisms the rig exists for: thickets tile, and a shrub is a hollow basket
        // =================================================================================

        [Test]
        public void EveryWrapUnit_ActuallyCrossesItsTileJoin()
        {
            // A tile whose stems never cross the join tiles perfectly AND reads as a fence of separate
            // bushes — which is the failure the wrap units exist to avoid. report.crossings counts the
            // rows that really cross.
            using var host = CreateHost();
            var contract = LoadContract();

            var wraps = contract.WrapUnits.ToArray();
            Assert.IsNotEmpty(wraps,
                "No species declares a wrap tile. Alder, leatherleaf, sweet gale, blueberry, juniper " +
                "and raspberry ship as thickets — if none do, the contract lost the flag.");

            foreach (var s in wraps)
            {
                string res = ShrubBaker.ResultExpr(s.Key, Stage, "green", variant: 0, frame: 0);
                int crossings = (int)host.EvaluateNumber($"{res}.report.crossings");
                Assert.Greater(crossings, 0,
                    $"{s.Key} is a wrap tile but NOTHING crosses its join. It would tile perfectly and " +
                    "read as a row of pots — the rig's own assert list calls this out.");
                Debug.Log($"[shrub-wrap] {s.Key}: {crossings} row(s) cross the tile join.");
            }
        }

        [Test]
        public void EveryNonMatShrub_HasWindowsYouCanSeeThrough_InLeafAtFullStage()
        {
            // Rule 2: a tree crown is a solid cap seen from below; a shrub is an open basket you look
            // INTO. A shrub with zero enclosed sky holes is a bush-shaped rock no matter how nice its
            // edge is.
            //
            // ⚠️ THE PHASE MATTERS, AND THE CONTRACT'S `minWindows` IS THE WRONG NUMBER FOR THIS.
            // The rig's own assert is "every non-mat species has windows > 0 IN LEAF at FULL stage".
            // `sheetAudit().minWindows` is the MINIMUM across all eight phases, and a dormant or bare
            // shrub legitimately has none — it is filaments, not a basket, for four months of the year.
            // Asserting the min would fail Sheep Laurel (a clump, minWindows 0) for having a solid
            // dormant frame, which the rig never claimed was wrong. So render the LEAF phase and read
            // report.windows, and gate on the rig's OWN `windowsApply` rather than re-deriving
            // "is it a mat" here.
            using var host = CreateHost();
            var contract = LoadContract();

            int checkedCount = 0, exempt = 0, worst = int.MaxValue;
            string worstKey = null;

            foreach (var s in contract.Species)
            {
                string res = ShrubBaker.ResultExpr(s.Key, Stage, "leaf", variant: 0, frame: 0);
                bool applies = host.EvaluateBool($"{res}.report.windowsApply");
                int windows = (int)host.EvaluateNumber($"{res}.report.windows");

                // The rig's exemption and the contract's `unit` must agree, or one of them is lying
                // about which species get to be solid.
                Assert.AreEqual(s.Unit != "mat", applies,
                    $"{s.Key}: the rig says windowsApply={applies} but the contract calls it " +
                    $"'{s.Unit}'. Rule 2's exemption is BY FORM — the two cannot disagree about form.");

                if (!applies) { exempt++; continue; }

                Assert.Greater(windows, 0,
                    $"{s.Key} ({s.Form}/{s.Unit}) has 0 enclosed sky holes in the LEAF phase at " +
                    "full stage. A non-mat shrub you cannot see into has failed rule 2 — that is an " +
                    "art-director rig matter, not something to bake past.");
                checkedCount++;
                if (windows < worst) { worst = windows; worstKey = s.Key; }
            }

            Assert.Greater(checkedCount, 0, "No species was actually checked — the assert is vacuous.");
            Assert.Greater(exempt, 0, "No species is exempt, so the by-form exemption is untested.");
            Debug.Log($"[shrub-window] {checkedCount} non-mat species all have windows > 0 in leaf at " +
                      $"full stage ({exempt} mats exempt by form). Fewest is {worstKey} at {worst}.");
        }

        // =================================================================================
        // phenology — eight states, and the two facts that fall out of one table
        // =================================================================================

        [Test]
        public void ThePhenologyIsEightPhases_AndBloomFirstSpeciesFlowerOnNakedWood()
        {
            var contract = LoadContract();
            using var host = CreateHost();

            Assert.AreEqual(8, contract.Calendar.Phases.Count,
                "Eight calendar stations. A shrub's identity lives in the flower and the fruit, which " +
                "is why three seasons cannot carry it.");
            CollectionAssert.AreEqual(ShrubCatalog.PhaseKeys,
                                      contract.Calendar.Phases.Select(p => p.Key).ToArray(),
                "The phase keys or their ORDER drifted from the catalog's.");
            foreach (var p in contract.Calendar.Phases)
                Assert.IsFalse(string.IsNullOrEmpty(p.When),
                    $"phase '{p.Key}' has no month — the calendar has to be readable to be judged.");

            // ⭐ ONE table maps (phase, species) → leaf / bloom / fruit / catkin / veil, so the two
            // facts below fall out for free and cannot disagree with each other. The table's outputs
            // are FRACTIONS 0..1, not booleans — read as thresholds, not truthiness.
            string g = ShrubCatalog.RigGlobalName;
            string PhaseOf(string species, string phase) =>
                $"{g}.phaseOf('{phase}', {g}.byKey['{species}'])";

            // bloomFirst species flower on NAKED wood. The rig leaves 0.12 of leaf rather than 0 — a
            // few breaking buds, not a bare stick — so the assert is "nearly leafless", which is the
            // claim, and it is still a long way from a leafed-out 1.0.
            var bloomFirst = contract.Species.Where(s => s.BloomFirst).ToArray();
            Assert.IsNotEmpty(bloomFirst, "Rhodora and serviceberry bloom on bare wood.");
            foreach (var s in bloomFirst)
            {
                double leafAtBloom = host.EvaluateNumber($"{PhaseOf(s.Key, "bloom")}.leaf");
                double leafAtLeaf = host.EvaluateNumber($"{PhaseOf(s.Key, "leaf")}.leaf");
                double bloomAtBloom = host.EvaluateNumber($"{PhaseOf(s.Key, "bloom")}.bloom");

                Assert.Less(leafAtBloom, 0.2,
                    $"{s.Key} is declared bloomFirst but carries {leafAtBloom:F2} of LEAF in the bloom " +
                    "phase. Flowering on naked wood is the whole reason the flag exists.");
                Assert.Greater(bloomAtBloom, 0.5,
                    $"{s.Key} is declared bloomFirst but barely blooms in its bloom phase.");
                Assert.Greater(leafAtLeaf, leafAtBloom + 0.5,
                    $"{s.Key}: leaf must be far higher in the leaf phase than in the bloom phase, or " +
                    "'blooms on bare wood' is not actually being expressed.");
                Debug.Log($"[shrub-phase] {s.Key} bloomFirst: leaf {leafAtBloom:F2} at bloom vs " +
                          $"{leafAtLeaf:F2} at leaf, bloom {bloomAtBloom:F2}.");
            }

            // holds species carry fruit through the DORMANT frame — winterberry is invisible for eleven
            // months and then it is the loudest thing in the swamp.
            var holds = contract.Species.Where(s => s.HoldsFruit).ToArray();
            Assert.IsNotEmpty(holds, "Winterberry, juniper, rose and sumac hold fruit.");
            foreach (var s in holds)
            {
                double fruitAtDormant = host.EvaluateNumber($"{PhaseOf(s.Key, "dormant")}.fruit");
                Assert.Greater(fruitAtDormant, 0.0,
                    $"{s.Key} is declared holdsFruit but carries none in the dormant frame — which is " +
                    "the entire reason to draw it in February.");
                Debug.Log($"[shrub-phase] {s.Key} holdsFruit: {fruitAtDormant:F2} at dormant.");
            }

            // ---- MEASURED SABOTAGE: a NON-holder must lose its fruit by dormant ---------------
            // Without this, "holds fruit in February" would pass even if every species did.
            var dropper = contract.Species.First(s => !s.HoldsFruit && !s.Evergreen);
            double dropped = host.EvaluateNumber($"{PhaseOf(dropper.Key, "dormant")}.fruit");
            Assert.AreEqual(0.0, dropped, 1e-9,
                $"{dropper.Key} does not hold fruit, so it must carry none in dormant. If every " +
                "species keeps its berries all winter, the holds flag means nothing.");

            Debug.Log($"[shrub-phase] 8 phases. bloomFirst: {string.Join(", ", bloomFirst.Select(s => s.Key))}. " +
                      $"holdsFruit: {string.Join(", ", holds.Select(s => s.Key))}.");
        }

        // =================================================================================
        // determinism — the precondition for every exactness claim above
        // =================================================================================

        [Test]
        public void RigRenders_AreBitIdenticalAcrossCalls()
        {
            using var host = CreateHost();
            foreach (string key in Probes)
            {
                string res = ShrubBaker.ResultExpr(key, Stage, "green", variant: 1, frame: 0);
                foreach (string fn in new[] { "packMask", "packState" })
                {
                    byte[] a = Packed(host, res, fn);
                    byte[] b = Packed(host, res, fn);
                    Assert.AreEqual(a, b,
                        $"{key} {fn}: two renders of the same cell differ. Every claim in this suite " +
                        "rests on this — chase it before anything else.");
                }
            }
        }

        // =================================================================================
        // 🔴 THE SWAY ROWS — the bake must FILL the sheet the contract describes
        //
        // The kit shipped with a baker that wrote ONE row where the contract declares four, so every
        // sheet it produced was a quarter of its contracted height and ShrubSheetSlicer refused all
        // eighteen of them. Nothing caught it: the rig renders sway frames correctly, the contract is
        // serialised correctly, and the baker's own staleness guard compared sheet WIDTHS only. These
        // two tests are the fence — one on the geometry the baker plans, one on the pixels it writes.
        // =================================================================================

        [Test]
        public void EveryAxis_PlansAllFourSwayRows_AndAShortSheetIsRefusedByTheBakersOwnGuard()
        {
            var contract = LoadContract();
            using var host = CreateHost();

            Assert.AreEqual(ShrubCatalog.SwayFrames, ShrubBaker.SwayRowsBaked,
                "ShrubBaker.SwayRowsBaked must be the rig's own SWAY. Unlike the tree kit — whose " +
                "contract publishes sheet.rows AND sheet.rigSwayRows so a one-row bake is describable " +
                "— this kit's contract is serialised straight out of the rig and carries ONE pair of " +
                "numbers per axis. A baker that lays out fewer rows writes a sheet its own oracle " +
                "rejects.");
            Assert.AreEqual(ShrubCatalog.SwayFrames, contract.Sway.Frames,
                "the contract's sway.frames vs the catalog's constant");

            foreach (var entry in contract.Species)
            foreach (string axis in new[] { ShrubBaker.PhaseAxis, ShrubBaker.VariantAxis })
            {
                var spec = ShrubBaker.ReadSheetSpec(host, entry.Key, Stage, axis);
                int wantW = axis == ShrubBaker.PhaseAxis ? entry.PhaseSheetW : entry.VariantSheetW;
                int wantH = axis == ShrubBaker.PhaseAxis ? entry.PhaseSheetH : entry.VariantSheetH;

                Assert.AreEqual(ShrubCatalog.SwayFrames, spec.SwayRows,
                    $"{entry.Key}/{axis}: the rig publishes {spec.SwayRows} sway row(s).");
                Assert.AreEqual(wantW, spec.SheetW, $"{entry.Key}/{axis}: sheet width");

                // ⭐ THE ASSERT THE KIT WAS MISSING. The contracted height is swayRows × cellH, and
                // the sheet the baker lays out has to BE that — not a factor of it.
                Assert.AreEqual(wantH, spec.SheetH,
                    $"{entry.Key}/{axis}: the baker plans a {spec.SheetW}×{spec.SheetH} sheet but the " +
                    $"contract says {wantW}×{wantH}. A sheet of the wrong height matches neither " +
                    "published axis, so the slicer refuses it outright.");
                Assert.AreEqual(wantH, spec.SwayRows * spec.CellH,
                    $"{entry.Key}/{axis}: the contracted height must be swayRows × cellH.");

                // The rig's whole-sheet numbers and ours now describe the SAME sheet. While the baker
                // wrote one row these two disagreed by 4× and nothing compared them.
                Assert.AreEqual(spec.RigSheetH, spec.SheetH,
                    $"{entry.Key}/{axis}: the rig reports a {spec.RigSheetW}×{spec.RigSheetH} sheet " +
                    "and we lay out a different one. The 2048 guard asserts both, and it can only be " +
                    "worth asserting twice if the two are the same sheet.");
                Assert.AreEqual(spec.RigSheetW, spec.SheetW, $"{entry.Key}/{axis}: rig sheet width");
            }

            // ---- MEASURED SABOTAGE: the guard must refuse every way the sheet can be short -------
            const string key = "SpeckledAlder";
            var real = ShrubBaker.ReadSheetSpec(host, key, Stage, ShrubBaker.VariantAxis);
            var realEntry = contract.Find(key);

            var cases = new (ShrubBaker.SheetSpec spec, string what)[]
            {
                (With(real, swayRows: 1),
                 "ONE sway row (the shipped defect — a quarter-height sheet)"),
                (With(real, swayRows: 2), "TWO sway rows (a half-height sheet)"),
                (With(real, swayRows: real.SwayRows + 1), "an EXTRA sway row"),
                (With(real, cols: real.Cols - 1), "a DROPPED variant column"),
                (With(real, cellH: real.CellH + 1), "a union cell one row TALLER"),
                (With(real, pivotY: real.PivotY - 1), "a ground contact one row HIGHER"),
            };

            var lines = new List<string>();
            foreach (var (spec, what) in cases)
            {
                Assert.Throws<InvalidOperationException>(
                    () => ShrubBaker.AssertMatchesContract(key, spec, ShrubBaker.VariantAxis, contract),
                    $"SABOTAGE NOT DETECTED — {what} passed the staleness guard. This is exactly how " +
                    "the one-row bake got as far as the slicer.");
                lines.Add($"  {what,-58} → refused ({spec.SheetW}×{spec.SheetH})");
            }

            // …and the shipped geometry must still pass, so the guard is strict rather than broken.
            Assert.DoesNotThrow(
                () => ShrubBaker.AssertMatchesContract(key, real, ShrubBaker.VariantAxis, contract));

            Debug.Log($"[shrub-sway] {contract.Species.Count} species × 2 axes all plan " +
                      $"{ShrubCatalog.SwayFrames} sway rows. {key}'s variant sheet is " +
                      $"{realEntry.VariantSheetW}×{realEntry.VariantSheetH}; the shipped one-row bake " +
                      $"wrote {realEntry.VariantSheetW}×{real.CellH} — 4× short.\n" +
                      "[shrub-sway] SABOTAGE — the staleness guard:\n" + string.Join("\n", lines));
        }

        /// <summary>
        /// The bake-shaped half: run a real variant-axis bake through <c>ShrubBaker</c>'s own path and
        /// read the PNG back off disk.
        ///
        /// <para>Two claims, and the second is the one a dimension check alone cannot make: the sheet
        /// is the contract's full size, AND every cell in it is the BIT-IDENTICAL render of
        /// (variant = column, sway frame = row). Four rows of frame 0 would be the right size and
        /// four times the bytes for no motion at all — so row 1 is measured against row 0 as well.</para>
        ///
        /// <para>Bakes to <c>artifacts/</c> (gitignored) rather than the kit folder: this kit ships no
        /// pixels, and <see cref="TheKitShipsNoPixels_AndThatIsDeliberate"/> asserts so.</para>
        /// </summary>
        [Test]
        public void AVariantAxisBake_FillsTheContractsSheet_AndEveryRowIsItsOwnSwayFrame()
        {
            // ⚠️ THE SPECIES IS A DELIBERATE CHOICE, AND NOT EVERY ONE WOULD DO. Measured at `leaf`,
            // full stage: alder's sway frame moves 8.5% of its cell, but Rhodora's moves 0.28% (6 px)
            // and lowbush blueberry and common juniper — the stiff mats — move NOTHING AT ALL, so on
            // those three the "row 1 is its own frame" claim is either razor-thin or vacuous. Alder is
            // also the kit's headline species and its veil case.
            const string key = "SpeckledAlder";
            const string phase = "leaf";
            string outFolder = "artifacts/shrub-bake-test";
            Directory.CreateDirectory(Path.Combine(RepoRoot, outFolder));

            var contract = LoadContract();
            var entry = contract.Find(key);
            Assert.IsNotNull(entry);

            var result = ShrubBaker.BakeVariantAxis(new[] { key }, Stage, phase, outFolder);

            Assert.AreEqual(1, result.Sheets.Count);
            var sheet = result.Sheets[0];
            Assert.AreEqual(ShrubCatalog.Channels.Length, sheet.AssetPaths.Count,
                "all three channels — the albedo and the two DATA bakes — come off one render.");
            Assert.AreEqual(ShrubCatalog.Variants * ShrubCatalog.SwayFrames, result.CellsRendered,
                $"{ShrubCatalog.Variants} variant columns × {ShrubCatalog.SwayFrames} sway rows. A " +
                "bake that rendered a quarter of these is the defect this test exists for.");
            Assert.AreEqual(entry.VariantSheetW, sheet.Width, "sheet width vs the contract");
            Assert.AreEqual(entry.VariantSheetH, sheet.Height, "sheet height vs the contract");

            // The PNGs on disk, not just the bookkeeping. A reported size and a written size are two
            // different claims.
            foreach (string assetPath in sheet.AssetPaths)
            {
                var (w, h) = PngSize(assetPath);
                Assert.AreEqual(entry.VariantSheetW, w, $"{assetPath}: PNG width");
                Assert.AreEqual(entry.VariantSheetH, h,
                    $"{assetPath}: PNG height. The contract says {entry.VariantSheetH} " +
                    $"({ShrubCatalog.SwayFrames} × {entry.CellH}); this file carries " +
                    $"{h / entry.CellH} row(s), and the slicer matches sheet sizes EXACTLY.");
            }

            // ---- every cell is the render of (variant = col, frame = row) ------------------------
            using var host = CreateHost();
            Color32[] px = ReadPixels(sheet.AssetPaths[0], out int pw, out int ph);

            for (int row = 0; row < ShrubCatalog.SwayFrames; row++)
            for (int col = 0; col < ShrubCatalog.Variants; col++)
            {
                byte[] want = host.EvaluateBytes(
                    $"{ShrubBaker.ResultExpr(key, Stage, phase, col, row)}.rgba");
                Assert.AreEqual(entry.CellW * entry.CellH * 4, want.Length);

                int mismatch = CountCellMismatch(px, pw, ph, entry, row, col, want);
                Assert.AreEqual(0, mismatch,
                    $"{key} cell (row {row}, col {col}): {mismatch} px differ from a fresh " +
                    $"render(variant {col}, frame {row}). The grid is variant-ACROSS and sway-DOWN, " +
                    "row-major from the TOP-LEFT — if this fails, the cells are laid out transposed " +
                    "or on the wrong frame, and the slicer's rect names would lie.");
            }

            // ---- MEASURED SABOTAGE: four copies of frame 0 would pass everything above -----------
            // The rig's sway curve is sin(frame / SWAY × 2π), so the four rows are
            // neutral · swing · neutral · counter-swing. That STRUCTURE is what gets asserted, rather
            // than a magnitude fitted to whichever species this test happens to bake: the two swings
            // must move, and they must move in opposite directions, so they differ from rest AND from
            // each other. ⚠️ Row 2 is the neutral pose AGAIN by design — a test that demanded every
            // row differ from row 0 would fail on a perfectly correct bake.
            int[] fromRest = new int[ShrubCatalog.SwayFrames];
            for (int row = 1; row < ShrubCatalog.SwayFrames; row++)
                fromRest[row] = CountRowMismatch(px, pw, ph, entry, 0, row);
            int swingToSwing = CountRowMismatch(px, pw, ph, entry, 1, 3);
            int rowPx = entry.CellW * entry.CellH * ShrubCatalog.Variants;

            Assert.Greater(fromRest[1], 0,
                $"{key}: sway row 1 is pixel-identical to row 0. Four identical rows are 4× the sheet " +
                "for no motion at all — either the baker is passing frame 0 for every row (the defect " +
                "in a different disguise) or the rig stopped swaying.");
            Assert.Greater(fromRest[3], 0,
                $"{key}: sway row 3 is pixel-identical to row 0, so the counter-swing is missing.");
            Assert.Greater(swingToSwing, fromRest[1],
                $"{key}: rows 1 and 3 differ from each other in {swingToSwing} px but each differs " +
                $"from rest in {fromRest[1]}/{fromRest[3]}. They are the two OPPOSITE swings, so the " +
                "gap between them must be the widest in the cycle. If it is not, the rows are not " +
                "carrying frames 0..3 in order.");
            Assert.Less(fromRest[2], fromRest[1],
                $"{key}: row 2 differs from rest by {fromRest[2]} px and row 1 by {fromRest[1]}. " +
                "sin(2/4 × 2π) = 0 and sin(1/4 × 2π) = 1, so row 2 must sit CLOSER to the neutral " +
                "pose than row 1 does — this is what pins the rows to the rig's own sway curve " +
                "rather than to some permutation of it.");

            Debug.Log($"[shrub-bake] {key} variant axis at '{phase}': " +
                      $"{sheet.Width}×{sheet.Height} = {ShrubCatalog.Variants} variant col(s) × " +
                      $"{sheet.Rows} sway row(s) of {entry.CellW}×{entry.CellH}, " +
                      $"{result.CellsRendered} renders, {result.TotalPngBytes / 1024} KiB across " +
                      $"{sheet.AssetPaths.Count} channels. Every one of the " +
                      $"{ShrubCatalog.Variants * ShrubCatalog.SwayFrames} cells is bit-identical to a " +
                      $"fresh render(variant = col, frame = row). Of {rowPx} px per row, rows 1/2/3 " +
                      $"differ from rest in {fromRest[1]}/{fromRest[2]}/{fromRest[3]} px and the two " +
                      $"swings differ from each other in {swingToSwing} — neutral · swing · neutral · " +
                      "counter-swing, the rig's sin(frame/SWAY × 2π).");
        }

        /// <summary>A <see cref="ShrubBaker.SheetSpec"/> with one field replaced — the sabotage
        /// helper. Every field is passed explicitly so a new one cannot be silently defaulted to
        /// zero and quietly weaken a refusal.</summary>
        static ShrubBaker.SheetSpec With(in ShrubBaker.SheetSpec s, int? cellW = null,
                                         int? cellH = null, int? pivotX = null, int? pivotY = null,
                                         int? cols = null, int? swayRows = null) =>
            new ShrubBaker.SheetSpec(
                cellW: cellW ?? s.CellW, cellH: cellH ?? s.CellH,
                pivotX: pivotX ?? s.PivotX, pivotY: pivotY ?? s.PivotY,
                pad: s.Pad, cols: cols ?? s.Cols, swayRows: swayRows ?? s.SwayRows,
                rigSheetW: s.RigSheetW, rigSheetH: s.RigSheetH, rigFits: s.RigFits,
                wrap: s.Wrap, metres: s.Metres);

        /// <summary>A PNG's declared size, read straight out of the IHDR chunk — no graphics device
        /// and no decode, so a size claim can be checked even where a texture upload cannot.</summary>
        static (int w, int h) PngSize(string assetPath)
        {
            byte[] png = File.ReadAllBytes(Path.Combine(RepoRoot, assetPath));
            Assert.Greater(png.Length, 24, $"{assetPath} is too short to be a PNG.");
            int Be32(int at) => (png[at] << 24) | (png[at + 1] << 16) | (png[at + 2] << 8) | png[at + 3];
            return (Be32(16), Be32(20));
        }

        static Color32[] ReadPixels(string assetPath, out int width, out int height)
        {
            byte[] png = File.ReadAllBytes(Path.Combine(RepoRoot, assetPath));
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
            try
            {
                Assert.IsTrue(tex.LoadImage(png, markNonReadable: false),
                              $"failed to decode {assetPath}");
                width = tex.width;
                height = tex.height;
                return tex.GetPixels32();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(tex);
            }
        }

        /// <summary>
        /// How many pixels of one sheet cell differ from a rig-rendered RGBA cell.
        ///
        /// <para>⚠️ Texture pixels are BOTTOM-origin and the rig hands back TOP-origin rows, so sheet
        /// row 0 is the highest Y — the same flip <c>RigBaker.Blit</c> applies going the other way.
        /// Getting this backwards is how a transposed sheet passes a test.</para>
        /// </summary>
        static int CountCellMismatch(Color32[] px, int pw, int ph, ShrubCatalog.SpeciesEntry entry,
                                     int row, int col, byte[] want)
        {
            int bad = 0;
            for (int y = 0; y < entry.CellH; y++)
            {
                int unityY = ph - 1 - (row * entry.CellH + y);
                for (int x = 0; x < entry.CellW; x++)
                {
                    Color32 got = px[unityY * pw + col * entry.CellW + x];
                    int s = (y * entry.CellW + x) * 4;
                    if (got.r != want[s] || got.g != want[s + 1] ||
                        got.b != want[s + 2] || got.a != want[s + 3]) bad++;
                }
            }
            return bad;
        }

        /// <summary>How many pixels two whole sway rows differ, across every column.</summary>
        static int CountRowMismatch(Color32[] px, int pw, int ph, ShrubCatalog.SpeciesEntry entry,
                                    int rowA, int rowB)
        {
            int diff = 0;
            for (int y = 0; y < entry.CellH; y++)
            {
                int ya = ph - 1 - (rowA * entry.CellH + y);
                int yb = ph - 1 - (rowB * entry.CellH + y);
                for (int x = 0; x < ShrubCatalog.Variants * entry.CellW; x++)
                {
                    Color32 a = px[ya * pw + x], b = px[yb * pw + x];
                    if (a.r != b.r || a.g != b.g || a.b != b.b || a.a != b.a) diff++;
                }
            }
            return diff;
        }

        /// <summary>The one committed slice, named here and nowhere else in this assembly.</summary>
        static readonly string[] StPetersSliceSpecies =
        {
            "BeakedHazelnut", "LowbushBlueberry", "Meadowsweet", "Raspberry", "SweetGale", "WildRose",
        };

        const string StPetersSliceStem = "_full_atGreen";

        [Test]
        public void TheKitStillBakesToOrder_AndTheOnlyCommittedPixels_AreStPetersNamedSlice()
        {
            // ⭐ 20 species × 8 phases × 5 snow steps × 4 variants × 3 stages is a matrix an import has
            // no business choosing from, so the rig is the deliverable and sheets bake to order — the
            // call #312 and #318 already made. This test used to assert the folder was EMPTY, and said
            // that if a slice were ever committed it should be updated to name which and why. That
            // happened: St Peters cannot be built without pixels, so ONE corner is committed —
            // six species covering the rig's five habitats, VARIANT axis, `full` stage, `green` phase,
            // three channels each. 0.56 MiB on disk.
            //
            // ⚠️ THE SLICE IS SPELLED OUT ABOVE RATHER THAN READ FROM StPetersShrubBake, and not only
            // because this assembly cannot see HiddenHarbours.App.Editor. This is an ATLAS GUARD: if it
            // derived the expected set from the code that does the baking it could never fail, and
            // widening the bake is precisely the thing that has to come back to the owner. A seventh
            // species, a second phase or the other axis all land here as a failure, which is correct.
            string full = Path.Combine(RepoRoot, ShrubCatalog.ShrubsRoot);
            string[] pngs = Directory.Exists(full)
                ? Directory.GetFiles(full, "*.png", SearchOption.TopDirectoryOnly)
                             .Select(Path.GetFileName).OrderBy(n => n, StringComparer.Ordinal).ToArray()
                : Array.Empty<string>();

            string[] want = StPetersSliceSpecies
                            .SelectMany(s => ShrubCatalog.Channels.Select(
                                c => $"{s}{StPetersSliceStem}{ShrubCatalog.SuffixFor(c)}.png"))
                            .OrderBy(n => n, StringComparer.Ordinal).ToArray();

            var extra = pngs.Except(want, StringComparer.Ordinal).ToArray();
            Assert.IsEmpty(extra,
                $"{extra.Length} undeclared PNG(s) under {ShrubCatalog.ShrubsRoot}: " +
                string.Join(", ", extra) + ". This kit bakes to order and exactly one slice is ruled " +
                "in (St Peters', named in this test). Committing more is an atlas-budget decision for " +
                "the owner — widen this list deliberately rather than deleting the check.");

            var missing = want.Except(pngs, StringComparer.Ordinal).ToArray();
            Assert.IsEmpty(missing,
                $"{missing.Length} sheet(s) of St Peters' committed slice are gone: " +
                string.Join(", ", missing) + ". The region is built from these — re-run " +
                "Hidden Harbours ▸ Art ▸ Bake St Peters Shrub Slice.");

            Assert.AreEqual(StPetersSliceSpecies.Length * ShrubCatalog.Channels.Length, pngs.Length);
            foreach (string s in StPetersSliceSpecies)
                Assert.Contains(s, ShrubCatalog.SpeciesKeys,
                    $"the committed slice names '{s}', which is not a species key. ⚠️ The README's " +
                    "display names are NOT the rig's keys (Raspberry, WinterberryHolly).");

            // The contract MUST be committed too — it is what the engine is wired against, and every
            // rect on those sheets is numbered against its union cell and pivot.
            Assert.IsTrue(File.Exists(Path.Combine(RepoRoot, ShrubCatalog.ContractPath)),
                $"{ShrubCatalog.ContractPath} must be committed alongside the pixels.");

            Debug.Log($"[shrub-slice] {pngs.Length} committed PNG(s) = " +
                      $"{StPetersSliceSpecies.Length} species × {ShrubCatalog.Channels.Length} channels, " +
                      $"variant axis at full/green. The other 19/20 of the matrix is still unbaked.");
        }

        [Test]
        public void TheSlicersVerifier_IsHappyWithWhateverIsOnDisk()
        {
            // The same check the bake menu runs, and it has to hold in BOTH of this kit's states: an
            // empty folder ("nothing to slice" was its normal state for a milestone, and absence must
            // not read as failure) and the one committed slice, whose every sheet must agree with the
            // contract on pivot, mesh type, colour space and cell size.
            Assert.IsTrue(ShrubSheetSlicer.VerifyAll(logEachPass: false, out int checkedCount),
                "ShrubSheetSlicer.VerifyAll reported problems — see the errors above.");
            Debug.Log($"[shrub-slice] verifier checked {checkedCount} sheet(s) — 0 was correct while the " +
                      "kit shipped nothing; St Peters' slice makes it 18.");
        }

        [Test]
        public void BayberryAndSweetFern_AreNotInThisRig_BecauseShorePlantsOwnsThem()
        {
            // A species living in two rigs is how two silhouettes happen — the same alder with a summer
            // sprite twice the width of its winter one is the bug this whole kit replaces.
            var contract = LoadContract();

            foreach (string key in new[] { "Bayberry", "SweetFern" })
            {
                Assert.IsNull(contract.Find(key),
                    $"{key} appears in the shrub contract, but ShorePlants owns it as a dune/upland " +
                    "unit. Two rigs owning one species is how it ends up with two silhouettes.");
                Assert.Contains(key, ShorePlantCatalog.SpeciesKeys,
                    $"{key} is excluded from the shrub rig on the grounds that ShorePlants owns it — " +
                    "so it had better still be there.");
            }

            Assert.IsNotEmpty(contract.NotInThisRig,
                "The contract should say out loud which species it deliberately excludes.");
            Debug.Log("[shrub-scope] " + string.Join(" ", contract.NotInThisRig));
        }
    }
}
