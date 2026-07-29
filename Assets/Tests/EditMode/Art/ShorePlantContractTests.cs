using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Art.Editor;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// The Shoreline Plant kit's contract, held to the three rulings this import was gated on and to
    /// the drop's own README. No V8 and no pixels: everything here reads the committed contract, so
    /// it runs anywhere — including CI's null graphics device.
    ///
    /// <para><c>ShorePlantRigBakeTests</c> is the sibling: it asserts the contract against the RIG,
    /// live. This one asserts the contract against the DECISIONS. Between them a stale or
    /// hand-edited contract has nowhere to hide.</para>
    ///
    /// <para><b>The three rulings, in order:</b>
    /// <list type="number">
    ///   <item>Cells stay UNIONED over tide states — one cell and one ground-contact pivot per
    ///   species — gated on every sheet axis fitting under 2048 px, with the per-species ink waste
    ///   the union policy costs MEASURED rather than assumed.</item>
    ///   <item>The STRAP material must be DECLARED, and <c>light.G</c>'s body-and-wood-only meaning
    ///   pinned, because the sprite-light include branches on it.</item>
    ///   <item>Submerged states bake NO moving caustic — colour only.</item>
    /// </list></para>
    /// </summary>
    public class ShorePlantContractTests
    {
        private static ShorePlantCatalog.Contract _c;

        [OneTimeSetUp]
        public void LoadTheContract()
        {
            _c = ShorePlantCatalog.Load();
            Assert.IsNotNull(_c,
                $"{ShorePlantCatalog.ContractPath} failed to load — it ships with the kit.");
        }

        // =====================================================================================
        // 0. the contract is the one the kit shipped
        // =====================================================================================

        [Test]
        public void TheContractIdentifiesItselfAsThisRig_AtTheProjectsLockedProjection()
        {
            Assert.AreEqual(ShorePlantCatalog.RigName, _c.Rig, "contract.rig");
            Assert.AreEqual(1, _c.Version, "contract.version");

            // ADR 0006/0022 — the same camera as the boat, rock, shoreline and tree bakes. A kit
            // that drifted off it would composite against the shore at a different foreshortening.
            Assert.AreEqual(ShorePlantCatalog.Ppu, _c.Projection.Ppu, "PPU (32 px = 1 m)");
            Assert.AreEqual(40f, _c.Projection.ElevDeg, 0.001f, "camera elevation");
            Assert.AreEqual(Mathf.Cos(40f * Mathf.Deg2Rad), _c.Projection.HeightScale, 0.001f,
                            "height scale = cos(elev)");
            Assert.AreEqual(Mathf.Sin(40f * Mathf.Deg2Rad), _c.Projection.DepthScale, 0.001f,
                            "ground depth scale = sin(elev)");

            Assert.IsFalse(_c.Projection.Antialias, "no AA — the pixel-art floor");
            Assert.AreEqual("binary", _c.Projection.Alpha, "binary alpha");
            StringAssert.Contains("ground contact", _c.Projection.Pivot,
                "the pivot must be declared as the ground contact, not the cell bottom");
        }

        [Test]
        public void EverySpeciesTheCatalogNames_IsInTheContract_AndViceVersa()
        {
            CollectionAssert.AreEquivalent(
                ShorePlantCatalog.SpeciesKeys,
                _c.Species.Select(s => s.Key).ToArray(),
                "the catalog's species keys vs the contract's — these must not drift apart, because " +
                "the catalog's order is what every menu, slicer and placement pass iterates.");

            CollectionAssert.AreEqual(ShorePlantCatalog.SpeciesKeys,
                                      _c.Species.Select(s => s.Key).ToArray(),
                                      "…in the same ORDER (fringe → mid → low → high → upland)");
        }

        // =====================================================================================
        // 1. RULING ONE — unioned cells, gated on 2048, waste MEASURED
        // =====================================================================================

        /// <summary>
        /// 🔴 The silent killer. Over 2048 px on any axis Unity imports the sheet DOWNSCALED with
        /// the sprite COUNT still matching, so the slice succeeds and only a pivot assert much later
        /// notices. The union-cell policy trades sheet area for pivot stability, which is exactly
        /// what makes this cap the number that could turn the trade into a silent bug.
        /// </summary>
        [Test]
        public void EverySheetAxisFitsUnder2048_AndTheUnionCellsCostIsMeasuredPerSpecies()
        {
            var table = new StringBuilder(
                "[shore-plants] union-cell audit — the numbers a per-tide-cell exception would be " +
                "argued on:\n" +
                "  species          zone    cell      pivot     drape   variant×sway  tide×sway   " +
                "ink%  waste%  headroom    KiB\n");

            int totalKb = 0, worstWaste = 0;
            string worstKey = null;

            foreach (string key in ShorePlantCatalog.SpeciesKeys)
            {
                var s = _c.Find(key);
                Assert.IsNotNull(s, $"{key} missing from the contract");

                Assert.IsTrue(s.Fits2048,
                    $"{key}: the contract itself says this species does NOT fit under " +
                    $"{ShorePlantCatalog.ImportSizeCap} px. Flag it for a per-tide-cell exception — " +
                    "that is an owner decision, not something an import may take.");

                foreach (var (w, h, axis) in new[]
                         {
                             (s.VariantSheetW, s.VariantSheetH, "variant × sway"),
                             (s.TideSheetW, s.TideSheetH, "tide × sway"),
                         })
                {
                    Assert.LessOrEqual(w, ShorePlantCatalog.ImportSizeCap,
                        $"{key} {axis} sheet is {w} px wide — over the cap Unity DOWNSCALES it " +
                        "silently and the sprite count still comes out right.");
                    Assert.LessOrEqual(h, ShorePlantCatalog.ImportSizeCap,
                        $"{key} {axis} sheet is {h} px tall — same silent downscale.");
                }

                // The sheets must be exactly the union cell times the published axes, or the
                // slicer's rects would land off-cell while every dimension still "looked right".
                Assert.AreEqual(s.CellW * ShorePlantCatalog.Variants, s.VariantSheetW,
                                $"{key}: variant sheet width = cell × {ShorePlantCatalog.Variants}");
                Assert.AreEqual(s.CellH * ShorePlantCatalog.SwayFrames, s.VariantSheetH,
                                $"{key}: variant sheet height = cell × sway frames");
                Assert.AreEqual(s.CellW * ShorePlantCatalog.TideKeys.Length, s.TideSheetW,
                                $"{key}: tide sheet width = cell × {ShorePlantCatalog.TideKeys.Length} tide states");
                Assert.AreEqual(s.CellH * ShorePlantCatalog.SwayFrames, s.TideSheetH,
                                $"{key}: tide sheet height = cell × sway frames");

                CollectionAssert.Contains(ShorePlantCatalog.TideKeys, s.WorstInkTide,
                    $"{key}: worstInkTide names a tide state that does not exist");

                totalKb += s.SheetKb;
                if (s.WorstWastePct > worstWaste) { worstWaste = s.WorstWastePct; worstKey = key; }

                table.AppendLine(
                    $"  {key,-16} {s.Zone,-7} {s.CellW,3}×{s.CellH,-3}   " +
                    $"{s.Pivot.x,3},{s.Pivot.y,-3}   {ShorePlantCatalog.DrapeMetres(s),4:F2} m  " +
                    $"{s.VariantSheetW,4}×{s.VariantSheetH,-4}    {s.TideSheetW,4}×{s.TideSheetH,-4}  " +
                    $"{s.WorstInkPct,4}  {s.WorstWastePct,5}   {s.Headroom,7}  {s.SheetKb,6}" +
                    $"   (worst at {s.WorstInkTide})");
            }

            Assert.IsTrue(_c.Sheets.AllFit, "contract.sheets.allFit");
            Assert.AreEqual(ShorePlantCatalog.ImportSizeCap, _c.Sheets.Cap, "contract.sheets.cap");
            StringAssert.Contains("ONE union cell", _c.Sheets.CellPolicy,
                "the cell policy must be stated in the contract, not only in the README — the " +
                "slicer writes one pivot per sheet BECAUSE of it.");

            table.AppendLine(
                $"  ── both axes, all 16 species: {totalKb} KiB = {totalKb / 1024.0:F2} MiB RGBA " +
                $"uncompressed (contract says {_c.Sheets.TotalMb:F2} MB); worst waste " +
                $"{worstWaste}% on {worstKey}; smallest headroom " +
                $"{ShorePlantCatalog.SpeciesKeys.Min(k => _c.Find(k).Headroom)} px.");
            Debug.Log(table.ToString());
        }

        /// <summary>
        /// The union-cell invariant, in the only form the contract can express it: ONE pivot per
        /// species, and it must land inside its own cell. The live per-tide-state proof is in
        /// <c>ShorePlantRigBakeTests</c>, where the rig can be asked five times.
        /// </summary>
        [Test]
        public void EveryPivotIsInsideItsOwnCell_AndTheDrapeHangsBelowIt()
        {
            foreach (var s in _c.Species)
            {
                Assert.That(s.Pivot.x, Is.InRange(0, s.CellW - 1),
                            $"{s.Key}: pivot x outside the cell");
                Assert.That(s.Pivot.y, Is.InRange(0, s.CellH - 1),
                            $"{s.Key}: pivot y outside the cell");

                // The drape pad projects BELOW the ground contact — a drained kelp folds onto its
                // rock and puddles downslope — so a bottom-of-cell pivot would float every plant by
                // exactly this. Positive on all sixteen is what makes the rule worth stating.
                Assert.Greater(ShorePlantCatalog.PadPx(s), 0,
                    $"{s.Key}: no rows below the ground contact — either the pivot is at the cell " +
                    "floor (in which case BottomCenter would have done) or the cell is wrong.");
            }
        }

        /// <summary>
        /// ⚠ SABOTAGE, MEASURED. ADR 0026 / #300 settled that the tree kit's <c>pad/cellH</c> and
        /// the shared <c>(H − y)/H</c> are DIFFERENT quantities, and that the rule is to use whatever
        /// the contract publishes — here, the pivot. This proves the two are actually
        /// distinguishable on this kit's numbers (they differ by exactly one row), so an assert on
        /// the pivot has teeth rather than passing under either convention.
        /// </summary>
        [Test]
        public void Sabotage_ThePadConventionAndThePivotConvention_DifferByExactlyOneRow()
        {
            var report = new StringBuilder("[shore-plants] SABOTAGE — pivot convention:\n");
            foreach (var s in _c.Species)
            {
                float correct = ShorePlantCatalog.NormalizedPivot(s).y;
                float padded = ShorePlantCatalog.PadPx(s) / (float)s.CellH;   // the tree kit's formula

                Assert.AreEqual(1f / s.CellH, correct - padded, 1e-5f,
                    $"{s.Key}: the two conventions must differ by exactly one row — if they ever " +
                    "agree, no test can tell a mistaken swap from the correct choice.");

                Assert.AreNotEqual(correct, padded,
                    $"{s.Key}: SABOTAGE NOT DETECTED — the pad convention produced the same pivot.");

                report.AppendLine(
                    $"  {s.Key,-16} cell h {s.CellH,3}  pivotY {s.Pivot.y,3}  pad {ShorePlantCatalog.PadPx(s),3}" +
                    $"   (H−y)/H = {correct:F4}   pad/H = {padded:F4}   Δ = {correct - padded:F4} " +
                    $"= {1f / s.CellH * ShorePlantCatalog.Ppu:F3} px");
            }
            Debug.Log(report.ToString());
        }

        // =====================================================================================
        // 2. RULING TWO — the strap material is DECLARED, and light.G is gated on it
        // =====================================================================================

        /// <summary>
        /// ⭐ The declaration the sprite-light include (#314) will branch on. A grass blade IS 2 px
        /// wide, so the mass floor cannot police it; the exemption is bought by forbidding the rim.
        /// If this is not in the contract, a consuming shader has to GUESS strap-vs-mass from the
        /// sprite — which is exactly the failure this ruling exists to prevent.
        /// </summary>
        [Test]
        public void TheStrapMaterialIsDeclared_ExemptFromTheMassFloor_AndForbiddenARim()
        {
            var m = _c.Materials;
            Assert.IsNotNull(m.Body, "materials.body");
            Assert.IsNotNull(m.Strap, "materials.strap — the declaration this ruling is about");
            Assert.IsNotNull(m.Wood, "materials.wood");
            Assert.IsNotNull(m.Fleck, "materials.fleck");

            // Body is the only material the floor polices, and the only one that carries a rim
            // unconditionally.
            Assert.IsFalse(m.Body.Linear, "body is a mass, not linear");
            Assert.AreEqual(5, m.Body.MassFloorPx,
                "the body mass floor: RIM_PX 2 must leave MIN_BODY 6 behind it → a 5 px radius");
            Assert.IsTrue(m.Body.CarriesRim, "body carries a rim");

            // ⭐ The exemption, both halves. Exempt from the floor AND forbidden a rim — either half
            // alone is a fudge; together they are auditable.
            Assert.IsTrue(m.Strap.Linear, "strap is linear (blades, culms, fronds, sheets)");
            Assert.IsNull(m.Strap.MassFloorPx,
                "strap must carry NO mass floor — a null, not a zero. The exemption is the point.");
            Assert.IsTrue(m.Strap.RimForbidden,
                "strap must be forbidden a rim — that is what buys the mass-floor exemption.");
            StringAssert.Contains("Exempt from the mass floor", m.Strap.Note,
                "the strap note must state the exemption in words a reviewer can check");

            Assert.IsTrue(m.Fleck.RimForbidden, "fleck is flat single-pixel detail — no rim");
            Assert.IsTrue(m.Wood.RimThicknessGated,
                "wood is linear but not rim-free: the thickness gate decides per pixel");

            // Ids must be distinct and non-zero — they are what state.B is derived from.
            var ids = m.All.Select(x => x.Id).ToArray();
            CollectionAssert.AllItemsAreUnique(ids, "material ids");
            CollectionAssert.DoesNotContain(ids, 0, "0 is NONE — no material may claim it");
        }

        /// <summary>
        /// ⭐ The channel semantics, pinned. This is the one thing a consuming shader must be TOLD
        /// rather than guess, so the wording is load-bearing: <c>light.G</c> is body-and-wood only
        /// and identically zero on strap, and <c>state.B</c> is the flag that gates every read of it.
        /// </summary>
        [Test]
        public void LightGIsRimForBodyAndWoodOnly_AndStateBIsTheStrapFlagThatGatesIt()
        {
            Assert.IsNotNull(_c.Light, "bakes.light");
            Assert.IsNotNull(_c.State, "bakes.state");

            Assert.AreEqual("<key>_light.png", _c.Light.File, "the light bake's filename");
            Assert.AreEqual("<key>_tide.png", _c.State.File, "the state bake's filename");

            // R is key light for ALL materials — a strap's lit spine is real N·L off its bowed
            // cross-section normal, so it arrives here with everything else and no channel changes
            // meaning per material.
            StringAssert.Contains("key light", _c.Light.R);
            StringAssert.Contains("ALL materials", _c.Light.R,
                "light.R must be declared as covering every material, straps included");

            // 🔴 The branch. If this clause ever softens, the include has licence to reinterpret G.
            StringAssert.Contains("BODY AND WOOD ONLY", _c.Light.G.ToUpperInvariant(),
                "light.G must be declared for body and wood ONLY");
            StringAssert.Contains("identically 0 on strap", _c.Light.G,
                "light.G must be declared identically zero on strap pixels");
            StringAssert.Contains("state.B", _c.Light.G,
                "light.G's declaration must name the flag that gates it");

            StringAssert.Contains("STRAP", _c.State.B.ToUpperInvariant(),
                "state.B is the strap flag");
            StringAssert.Contains("no-rim flag", _c.State.B);

            Assert.AreEqual("coverage", _c.Light.A, "light.A");
            Assert.AreEqual("coverage", _c.State.A, "state.A");
            StringAssert.Contains("depth", _c.Light.B, "light.B is surface depth");
            StringAssert.Contains("wet", _c.State.R.ToLowerInvariant(), "state.R is wetness");

            // The contract's own importer asserts must carry the gating rule — the README can be
            // skimmed; this list is what a future importer reads.
            string joined = string.Join(" | ", _c.Asserts);
            StringAssert.Contains("gate", joined.ToLowerInvariant(),
                "the contract's asserts must include the state.B gating rule");
            Assert.IsTrue(_c.Asserts.Any(a => a.Contains("light.G")),
                $"no assert mentions light.G. Got: {joined}");
        }

        /// <summary>
        /// SABOTAGE for the strap declaration: the reader must not fold a missing or wrong
        /// <c>carriesRim</c> into "forbidden". A null must not read as false, and true must not read
        /// as forbidden — otherwise the assert above would pass on a contract that never made the
        /// declaration at all.
        /// </summary>
        [Test]
        public void Sabotage_ARimDeclarationThatIsMissingOrTrue_DoesNotReadAsForbidden()
        {
            var missing = new ShorePlantCatalog.Material { Key = "x", CarriesRimRaw = null };
            var carries = new ShorePlantCatalog.Material { Key = "x", CarriesRimRaw = "true" };
            var gated = new ShorePlantCatalog.Material { Key = "x", CarriesRimRaw = "thickness-gated" };
            var forbidden = new ShorePlantCatalog.Material { Key = "x", CarriesRimRaw = "false" };

            Assert.IsFalse(missing.RimForbidden,
                "SABOTAGE NOT DETECTED — an ABSENT declaration read as a rim ban. A contract that " +
                "simply forgot to declare the strap would pass the ruling-2 test.");
            Assert.IsFalse(carries.RimForbidden, "SABOTAGE NOT DETECTED — carriesRim:true read as a ban");
            Assert.IsFalse(gated.RimForbidden, "SABOTAGE NOT DETECTED — thickness-gated read as a ban");
            Assert.IsTrue(forbidden.RimForbidden, "the real declaration must still read as a ban");

            Assert.IsFalse(missing.CarriesRim);
            Assert.IsTrue(carries.CarriesRim);
            Assert.IsFalse(gated.CarriesRim, "thickness-gated is not an unconditional rim");

            // …and the null mass floor must not fold to 0, which would read as "a floor of zero"
            // rather than "no floor applies".
            Assert.IsNull(_c.Materials.Strap.MassFloorPx);
            Assert.IsNull(_c.Materials.Wood.MassFloorPx);
            Assert.IsNull(_c.Materials.Fleck.MassFloorPx);
            Assert.IsNotNull(_c.Materials.Body.MassFloorPx);

            Debug.Log("[shore-plants] SABOTAGE — rim declaration: null → not forbidden, " +
                      "true → not forbidden, thickness-gated → not forbidden, false → forbidden. " +
                      "Only an explicit ban satisfies ruling 2.");
        }

        // =====================================================================================
        // 3. RULING THREE — no baked water, no baked moving light
        // =====================================================================================

        [Test]
        public void TheContractDeclaresNoBakedWater_NoCaustics_AndNoMovingLight()
        {
            Assert.IsFalse(_c.Water.BakesWater,
                "ADR 0010/0012/0023 — the engine shader owns every water pixel.");
            Assert.IsFalse(_c.Water.BakesCaustics,
                "⭐ the ruling: a baked dapple would put two uncorrelated patterns on one frond, " +
                "because the live shader's caustics are swell-driven.");
            Assert.IsFalse(_c.Water.BakesMovingLight,
                "nothing per-frame may be baked into the water response.");

            StringAssert.Contains("COLOUR only", _c.Water.Note,
                "submergence must be declared as colour only");
            Assert.IsTrue(_c.Asserts.Any(a => a.ToLowerInvariant().Contains("caustic")),
                "the contract's importer asserts must carry the no-second-caustic rule");
        }

        // =====================================================================================
        // 4. the tide model and the zone staircase
        // =====================================================================================

        [Test]
        public void TheZoneStaircaseClimbsMonotonically_AndEverySpeciesSitsOnItsOwnZone()
        {
            CollectionAssert.AreEqual(ShorePlantCatalog.ZoneKeys,
                                      _c.Zones.Select(z => z.Key).ToArray(),
                                      "zones, low → high");

            for (int i = 1; i < _c.Zones.Count; i++)
                Assert.Greater(_c.Zones[i].BaseM, _c.Zones[i - 1].BaseM,
                    $"the staircase must climb: {_c.Zones[i].Key} is not above {_c.Zones[i - 1].Key}");

            // Every zone base must lie inside the nominal range, or a plant's whole tide axis is
            // degenerate (always drowned or never wetted).
            foreach (var z in _c.Zones)
                Assert.That(z.BaseM, Is.InRange(0f, _c.Tide.RangeM),
                            $"{z.Key} sits outside the 0–{_c.Tide.RangeM} m nominal range");

            foreach (var s in _c.Species)
            {
                var zone = _c.Zone(s.Zone);
                Assert.IsNotNull(zone, $"{s.Key} names zone '{s.Zone}', which the contract lacks");
                Assert.AreEqual(zone.BaseM, s.BaseM, 1e-4f,
                    $"{s.Key}: its own baseM must equal its zone's — they are the same ground, and " +
                    "the tide is resolved against it.");
            }
        }

        [Test]
        public void TheTideStepsSpanTheWholeRange_AndMapOntoWaterOverEachPlantsOwnGround()
        {
            CollectionAssert.AreEqual(ShorePlantCatalog.TideKeys,
                                      _c.Tide.Steps.Select(s => s.Key).ToArray(),
                                      "tide steps, low → high");

            Assert.AreEqual(0f, _c.Tide.Steps.First().T, 1e-4f, "step 'low' is mean low water");
            Assert.AreEqual(1f, _c.Tide.Steps.Last().T, 1e-4f, "step 'high' is mean high water");

            for (int i = 1; i < _c.Tide.Steps.Count; i++)
                Assert.Greater(_c.Tide.Steps[i].T, _c.Tide.Steps[i - 1].T, "steps must rise");

            foreach (var step in _c.Tide.Steps)
                Assert.AreEqual(step.T * _c.Tide.RangeM, step.WaterM, 0.01f,
                    $"step '{step.Key}': waterM must be t × range, or the two disagree about the sea");

            // The four things the tide drives — if any of them stopped deriving from this one input
            // they could disagree, which is the bug the axis exists to make impossible.
            foreach (string d in new[] { "lay", "wet", "submergence", "swayMode" })
                CollectionAssert.Contains(_c.Tide.Derives, d,
                    "the tide must derive " + d + " from the same resolved state");

            StringAssert.Contains("painted seabed", _c.Tide.Input,
                "the tide input must map onto the painted seabed (ADR 0014) plus the deterministic " +
                "tide — no new simulation (rule 5).");

            // The catalog's own restatement of that input, so placement code can pick a tide column
            // without spinning up a V8 host. Checked against the contract's numbers, not literals.
            var kelp = _c.Find("SugarKelp");
            var pea = _c.Find("BeachPea");
            Assert.Greater(ShorePlantCatalog.WaterOverGroundM(_c, kelp, 1f), 0f,
                "sugar kelp on the subtidal fringe must be under water at high tide");
            Assert.Less(ShorePlantCatalog.WaterOverGroundM(_c, pea, 1f), 0f,
                "beach pea on the dune must be dry even at mean high water");
            Assert.Less(ShorePlantCatalog.WaterOverGroundM(_c, kelp, 0f), 0f,
                "…and the fringe drains at dead low");
        }

        [Test]
        public void TheNearestBakedTideState_IsChosenByProximity_NotByFlooring()
        {
            // A floor would leave a plant a state behind the water on the flood — the state hop the
            // union cell exists to make invisible would become a visible LAG instead.
            Assert.AreEqual("low", ShorePlantCatalog.NearestTideKey(_c, 0f));
            Assert.AreEqual("high", ShorePlantCatalog.NearestTideKey(_c, 1f));
            Assert.AreEqual("ebb", ShorePlantCatalog.NearestTideKey(_c, 0.29f));
            Assert.AreEqual("half", ShorePlantCatalog.NearestTideKey(_c, 0.50f),
                "0.50 is nearer half (0.55) than ebb (0.30) — a floor would have said ebb");
            Assert.AreEqual("flood", ShorePlantCatalog.NearestTideKey(_c, 0.78f));

            for (int i = 0; i < ShorePlantCatalog.TideKeys.Length; i++)
                Assert.AreEqual(i, ShorePlantCatalog.TideColumn(ShorePlantCatalog.TideKeys[i]),
                                "tide column order must match the baked sheet's columns");
        }

        // =====================================================================================
        // 5. sheet naming — the _tide suffix trap
        // =====================================================================================

        /// <summary>
        /// ⚠ The state channel's contract-declared filename is <c>&lt;key&gt;_tide.png</c>, and the
        /// tide is also a sheet AXIS — so a stem that ended in a tide key would be indistinguishable
        /// from another sheet's state channel. Both stem builders name the FIXED dimension for
        /// exactly this reason; this proves no legal stem can collide.
        /// </summary>
        [Test]
        public void NoLegalSheetStem_CanBeMistakenForAnotherSheetsDataChannel()
        {
            string[] suffixes = { "_light", "_tide" };
            var stems = new List<string>();

            foreach (string sp in ShorePlantCatalog.SpeciesKeys)
            foreach (string season in ShorePlantCatalog.SeasonKeys)
            foreach (string stage in ShorePlantCatalog.StageKeys)
            {
                for (int v = 0; v < ShorePlantCatalog.Variants; v++)
                    stems.Add(ShorePlantCatalog.TideSheetStem(sp, season, stage, v));
                foreach (string tide in ShorePlantCatalog.TideKeys)
                    stems.Add(ShorePlantCatalog.VariantSheetStem(sp, season, stage, tide));
            }

            CollectionAssert.AllItemsAreUnique(stems, "sheet stems");

            foreach (string stem in stems)
                foreach (string suffix in suffixes)
                    Assert.IsFalse(stem.EndsWith(suffix, StringComparison.Ordinal),
                        $"'{stem}' ends in '{suffix}' — it would be read as another sheet's data " +
                        "channel and sliced with the wrong colour space.");

            Debug.Log($"[shore-plants] {stems.Count} legal sheet stems, all unique, none colliding " +
                      "with a data-channel suffix.");
        }

        [Test]
        public void EveryChannelPathRoundTrips_BackToItsSheetStemAndChannel()
        {
            string stem = ShorePlantCatalog.TideSheetStem("SugarKelp", "summer", "full", 0);
            Assert.AreEqual("SugarKelp_summer_full_v0", stem);

            foreach (var channel in ShorePlantCatalog.Channels)
            {
                string path = ShorePlantCatalog.SheetPath(stem, channel);
                string fileStem = Path.GetFileNameWithoutExtension(path);

                var read = ShorePlantCatalog.ChannelForFileStem(fileStem, out string back);
                Assert.AreEqual(channel, read, $"channel of '{fileStem}'");
                Assert.AreEqual(stem, back, $"sheet stem of '{fileStem}'");
                Assert.AreEqual("SugarKelp", ShorePlantCatalog.SpeciesForStem(back));
                Assert.IsTrue(ShorePlantCatalog.IsColourChannel(channel) == (channel == ShorePlantCatalog.Channel.Albedo),
                              "only the albedo is colour");
            }

            Assert.AreEqual("SugarKelp_summer_full_atLow",
                            ShorePlantCatalog.VariantSheetStem("SugarKelp", "summer", "full", "low"),
                            "the variant-axis stem names its FIXED tide state, capitalised so it " +
                            "cannot be confused with the _tide channel suffix");
        }

        /// <summary>
        /// SABOTAGE for the stem reader: a file that merely STARTS with a species name must not be
        /// claimed. Guessing here is how a stray PNG gets sliced against the wrong cell and every
        /// sprite in it lands a few pixels off, which reads as an art bug.
        /// </summary>
        [Test]
        public void Sabotage_AStemThatMerelyResemblesASpecies_IsNotClaimed()
        {
            Assert.AreEqual("SeaLettuce",
                            ShorePlantCatalog.SpeciesForStem("SeaLettuce_summer_full_v0"),
                            "the real thing must still be claimed");

            foreach (string impostor in new[]
                     {
                         "SeaLettuceExtra_summer_full_v0",   // no separator after the key
                         "MySugarKelp_summer_full_v0",       // key not at the start
                         "SugarKelp",                        // no sheet suffix at all
                         "_preview-hero",                    // a preview that must never import
                         "Cattails_summer_full_v0",          // one letter off
                     })
                Assert.IsNull(ShorePlantCatalog.SpeciesForStem(impostor),
                    $"SABOTAGE NOT DETECTED — '{impostor}' was claimed as a plant sheet.");

            Debug.Log("[shore-plants] SABOTAGE — stem matching: 5 near-miss filenames rejected, " +
                      "the true stem accepted.");
        }

        // =====================================================================================
        // 6. the slicer's rects, off the contract
        // =====================================================================================

        /// <summary>
        /// ⭐ ONE pivot per sheet, on every rect. This is the whole purchase the union cell makes:
        /// per-column pivots would reintroduce the 1 px hop the moment the tide crossed a threshold.
        /// The measured sabotage is the 1 px shift itself.
        /// </summary>
        [Test]
        public void EveryRectOnASheet_CarriesTheSameGroundContactPivot_AndTheGridIsBottomOriginRowMajor()
        {
            var s = _c.Find("KnottedWrack");
            const int cols = 5, rows = ShorePlantCatalog.SwayFrames;

            var rects = ShorePlantSheetSlicer.BuildRects("KnottedWrack_summer_full_v0", cols, rows, s);
            Assert.AreEqual(cols * rows, rects.Length);

            Vector2 pivot = ShorePlantCatalog.NormalizedPivot(s);
            foreach (var r in rects)
                Assert.AreEqual(pivot, r.pivot,
                    $"{r.name}: every rect must carry the species' ONE ground contact.");

            // Row 0 is the TOP row of the sheet, which is the HIGHEST Y in Unity's bottom-origin
            // rects — the convention every slicer in the repo uses.
            var topLeft = rects.First(r => r.name.EndsWith("_c0_f0", StringComparison.Ordinal));
            var bottomLeft = rects.First(r => r.name.EndsWith($"_c0_f{rows - 1}", StringComparison.Ordinal));
            Assert.AreEqual((rows - 1) * s.CellH, topLeft.rect.y, "sway frame 0 is the TOP row");
            Assert.AreEqual(0, bottomLeft.rect.y, "the last sway frame is the BOTTOM row");

            var col3 = rects.First(r => r.name.EndsWith("_c3_f0", StringComparison.Ordinal));
            Assert.AreEqual(3 * s.CellW, col3.rect.x, "columns run left to right");

            // Rects must tile the sheet exactly — no overlap, no gap.
            Assert.AreEqual(cols * rows, rects.Select(r => (r.rect.x, r.rect.y)).Distinct().Count(),
                            "two rects share an origin");
            Assert.IsTrue(rects.All(r => Mathf.Approximately(r.rect.width, s.CellW) &&
                                         Mathf.Approximately(r.rect.height, s.CellH)),
                          "every rect is one union cell");
        }

        [Test]
        public void Sabotage_APivotOffByOnePixel_ChangesEveryRectOnTheSheet()
        {
            var real = _c.Find("Cattail");
            var shifted = new ShorePlantCatalog.SpeciesEntry
            {
                Key = real.Key, CellW = real.CellW, CellH = real.CellH,
                Pivot = new Vector2Int(real.Pivot.x, real.Pivot.y + 1),
            };

            var a = ShorePlantSheetSlicer.BuildRects("x", 5, 4, real);
            var b = ShorePlantSheetSlicer.BuildRects("x", 5, 4, shifted);

            for (int i = 0; i < a.Length; i++)
                Assert.AreNotEqual(a[i].pivot, b[i].pivot,
                    "SABOTAGE NOT DETECTED — a 1 px pivot shift left the rects identical, so the " +
                    "slicer's pivot assert could not tell a drifted contract from a good one.");

            float deltaM = 1f / ShorePlantCatalog.Ppu;
            Assert.AreEqual(1f / real.CellH, b[0].pivot.y - a[0].pivot.y, 1e-5f);
            Debug.Log($"[shore-plants] SABOTAGE — pivot: +1 px on Cattail moves all 20 rects by " +
                      $"{1f / real.CellH:F4} of the cell = {deltaM:F4} m at PPU " +
                      $"{ShorePlantCatalog.Ppu}. Every rect changed.");
        }

        // =====================================================================================
        // 7. the rig source is committed, verbatim and unpatched
        // =====================================================================================

        [Test]
        public void TheRigSourceIsCommitted_AndInstallsTheGlobalTheCatalogNames()
        {
            string path = ShorePlantCatalog.RigScriptPath;
            Assert.IsTrue(File.Exists(path),
                $"'{path}' is missing — the rig is the deliverable of this kit, and the baker " +
                "reads it from there.");

            string src = File.ReadAllText(path);
            StringAssert.Contains($"root.{ShorePlantCatalog.RigGlobalName} = {{", src,
                "the rig must install the global the catalog and baker address it by. It runs " +
                "UNMODIFIED (ADR 0021 §5) — if this fired, either the rig changed shape or " +
                "somebody patched a committed art-director file.");

            // The rig is the art-director's lane: it is committed VERBATIM and flagged, never
            // patched. A marker comment would be the visible half of a patch.
            foreach (string marker in new[] { "HIDDEN HARBOURS PATCH", "TODO(import)", "// PATCHED" })
                StringAssert.DoesNotContain(marker, src,
                    "the rig source carries an import-side edit — flag it to the art-director " +
                    "instead (rails §1).");
        }
    }
}
