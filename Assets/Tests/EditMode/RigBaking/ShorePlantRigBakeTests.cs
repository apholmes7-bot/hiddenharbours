using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Art.Editor;
using HiddenHarbours.Tools.RigBaking;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// The Shoreline Plant bake path, proven end-to-end WITHOUT any committed sheet — everything
    /// here runs the V8 host CPU-side, already established as safe on CI's null graphics device by
    /// this suite.
    ///
    /// <para><b>⭐ THE RIG IS THE ORACLE.</b> This kit ships a contract and no pixels, so the one
    /// question that matters is whether the committed contract still describes what
    /// <c>shorePlantRig.js</c> actually renders. The cell is UNIONED over 4 variants × 2 seasons ×
    /// 5 tides, so a cell that drifted by one row silently moves the ground-contact pivot of every
    /// baked state of that species at once — and nothing downstream would notice, because the sheets
    /// do not exist yet to disagree. These tests re-derive the geometry from the rig on every run
    /// and hold the contract to it.</para>
    ///
    /// <para><c>ShorePlantContractTests</c> is the sibling: it asserts the contract against the
    /// RULINGS. This one asserts it against the RIG, and measures the two rulings that can only be
    /// checked on rendered pixels — <b>no strap pixel carries a rim</b>, and <b>no submerged cell
    /// bakes a caustic</b>. Once sheets land, <c>ShorePlantSheetSlicer.VerifyAll</c> takes over.</para>
    ///
    /// <para><b>⚠ Renders are expensive here</b> — every channel is recomputed from the geometry on
    /// each call. The whole-kit checks therefore use <c>sheetSpec</c>/<c>cellOf</c> (geometry only),
    /// and the pixel scans use <see cref="ZoneProbes"/>: one species per tidal zone, which is the
    /// axis the rulings actually vary along.</para>
    /// </summary>
    public class ShorePlantRigBakeTests
    {
        private static IRigScriptHost _host;
        private static ShorePlantCatalog.Contract _contract;

        /// <summary>One species per tidal zone — fringe algae, mid-intertidal wrack, low-marsh
        /// cordgrass, high-marsh cattail and dune marram that never wets. The pixel scans run on
        /// these, because the zone is what the tide rulings vary along. All five are strapped
        /// families; <see cref="WoodyProbe"/> covers the other case.</summary>
        static readonly string[] ZoneProbes =
            { "SugarKelp", "KnottedWrack", "Cordgrass", "Cattail", "MarramGrass" };

        /// <summary>
        /// A woody upland shrub — body, wood and single-pixel FLECK berries, and <b>no strap at
        /// all</b>. It is the control for the strap scan: if <c>state.B</c> were painted everywhere
        /// (or nowhere) rather than tracking the material, this species and the five above could not
        /// disagree.
        /// </summary>
        const string WoodyProbe = "Bayberry";

        const string Season = "summer";
        const string Stage = "full";

        [OneTimeSetUp]
        public void InstallTheRigOnce()
        {
            // One V8 host for the fixture: installing the rig is the expensive part, and every test
            // here is a read against the same immutable rig state.
            _host = RigScriptHostFactory.Create();
            ShorePlantBaker.InstallRig(_host);
            _contract = ShorePlantCatalog.Load();
            Assert.IsNotNull(_contract, $"{ShorePlantCatalog.ContractFileName} failed to load.");
        }

        [OneTimeTearDown]
        public void DisposeHost() => _host?.Dispose();

        // =====================================================================================
        // 1. the rig runs unmodified and declares what the contract claims
        // =====================================================================================

        [Test]
        public void TheRigInstalls_AndDeclaresTheSameAxesAsTheContract()
        {
            Debug.Log($"[plant-bake] engine: {_host.EngineName}");

            CollectionAssert.AreEqual(ShorePlantCatalog.SpeciesKeys,
                                      ShorePlantBaker.ReadSpeciesKeys(_host).ToArray(),
                                      "the rig's species keys, in its own order");

            string g = ShorePlantCatalog.RigGlobalName;

            CollectionAssert.AreEqual(ShorePlantCatalog.TideKeys,
                                      ShorePlantBaker.ReadStringArray(_host, $"{g}.TIDE_KEYS"),
                                      "tide states: rig vs catalog");
            CollectionAssert.AreEqual(_c(_contract.Tide.Steps.Select(s => s.Key)),
                                      ShorePlantBaker.ReadStringArray(_host, $"{g}.TIDE_KEYS"),
                                      "tide states: rig vs contract");
            CollectionAssert.AreEqual(_c(_contract.Zones.Select(z => z.Key)),
                                      ShorePlantBaker.ReadStringArray(_host, $"{g}.ZONES.map(z=>z.key)"),
                                      "zones: rig vs contract");
            CollectionAssert.AreEqual(ShorePlantCatalog.SeasonKeys,
                                      ShorePlantBaker.ReadStringArray(_host, $"{g}.SEASONS"),
                                      "seasons: rig vs catalog");
            CollectionAssert.AreEqual(ShorePlantCatalog.StageKeys,
                                      ShorePlantBaker.ReadStringArray(_host, $"{g}.STAGE_KEYS"),
                                      "growth stages: rig vs catalog");

            Assert.AreEqual(ShorePlantCatalog.Ppu, (int)_host.EvaluateNumber($"{g}.PPU"), "PPU");
            Assert.AreEqual(ShorePlantCatalog.SwayFrames, (int)_host.EvaluateNumber($"{g}.SWAY"),
                            "sway frames");
            Assert.AreEqual(ShorePlantCatalog.Variants, (int)_host.EvaluateNumber($"{g}.VARIANTS"),
                            "variants");
            Assert.AreEqual(_contract.Sway.Frames, (int)_host.EvaluateNumber($"{g}.SWAY"),
                            "sway frames: rig vs contract");
            Assert.AreEqual(_contract.Tide.RangeM, (float)_host.EvaluateNumber($"{g}.TIDE_M"), 1e-4f,
                            "nominal tidal range");
            Assert.AreEqual(_contract.Tide.DryFallM, (float)_host.EvaluateNumber($"{g}.DRY_M"), 1e-4f,
                            "the fall a plant dries out over");

            // The catalog restates the stage → size table so placement code need not spin up V8.
            foreach (var kv in ShorePlantCatalog.StageSizes)
                Assert.AreEqual(kv.Value, (float)_host.EvaluateNumber($"{g}.STAGES['{kv.Key}']"),
                                1e-4f, $"stage '{kv.Key}' size scalar: catalog vs rig");

            // The material ids state.B is derived from.
            Assert.AreEqual(_contract.Materials.Body.Id, (int)_host.EvaluateNumber($"{g}.M.BODY"));
            Assert.AreEqual(_contract.Materials.Strap.Id, (int)_host.EvaluateNumber($"{g}.M.STRAP"));
            Assert.AreEqual(_contract.Materials.Wood.Id, (int)_host.EvaluateNumber($"{g}.M.WOOD"));
            Assert.AreEqual(_contract.Materials.Fleck.Id, (int)_host.EvaluateNumber($"{g}.M.FLECK"));
        }

        /// <summary>
        /// ⭐ The staleness check. The union cell and its ONE pivot are what every slice rect is
        /// numbered against, so this is the assert that stands between a re-baked rig and sixteen
        /// silently-wrong ground contacts.
        /// </summary>
        [Test]
        public void EverySpeciesCellPivotAndSheetSize_StillMatchesTheCommittedContract()
        {
            var table = new StringBuilder("[plant-bake] rig geometry vs the committed contract:\n" +
                                          "  species          cell      pivot     pad   variant sheet  tide sheet\n");

            foreach (string key in ShorePlantCatalog.SpeciesKeys)
            {
                var entry = _contract.Find(key);
                Assert.IsNotNull(entry, $"{key} missing from the contract");

                var tideSpec = ShorePlantBaker.ReadSheetSpec(_host, key, Stage, ShorePlantBaker.TideAxis);
                var varSpec = ShorePlantBaker.ReadSheetSpec(_host, key, Stage, ShorePlantBaker.VariantAxis);

                Assert.AreEqual(entry.CellW, tideSpec.CellW, $"{key} union cell width");
                Assert.AreEqual(entry.CellH, tideSpec.CellH, $"{key} union cell height");
                Assert.AreEqual(entry.Pivot.x, tideSpec.PivotX, $"{key} ground contact x");
                Assert.AreEqual(entry.Pivot.y, tideSpec.PivotY, $"{key} ground contact y");

                // The two axes are the SAME cell — a cell that differed between them would mean the
                // union was computed per axis, which is exactly the per-tide-cell failure mode the
                // union policy was bought to avoid.
                Assert.AreEqual(tideSpec.CellW, varSpec.CellW, $"{key}: both axes share one cell (w)");
                Assert.AreEqual(tideSpec.CellH, varSpec.CellH, $"{key}: both axes share one cell (h)");
                Assert.AreEqual(tideSpec.PivotY, varSpec.PivotY, $"{key}: both axes share one pivot");

                Assert.AreEqual(ShorePlantCatalog.TideKeys.Length, tideSpec.Cols,
                                $"{key} tide-axis columns");
                Assert.AreEqual(ShorePlantCatalog.Variants, varSpec.Cols, $"{key} variant-axis columns");
                Assert.AreEqual(ShorePlantCatalog.SwayFrames, tideSpec.Rows, $"{key} sway rows");

                Assert.AreEqual(entry.TideSheetW, tideSpec.SheetW, $"{key} tide sheet width");
                Assert.AreEqual(entry.TideSheetH, tideSpec.SheetH, $"{key} tide sheet height");
                Assert.AreEqual(entry.VariantSheetW, varSpec.SheetW, $"{key} variant sheet width");
                Assert.AreEqual(entry.VariantSheetH, varSpec.SheetH, $"{key} variant sheet height");

                // ⚠ ADR 0026: the rig publishes BOTH a pivot and a pad, and they are different
                // quantities. Pin the relation so a future reader cannot mistake one for the other —
                // the contract publishes the PIVOT, so that is what normalises.
                Assert.AreEqual(tideSpec.CellH - 1 - tideSpec.PivotY, tideSpec.Pad,
                    $"{key}: the rig's pad must be cellH − 1 − pivotY. If this ever stops holding, " +
                    "the two quantities have genuinely diverged and ShorePlantCatalog.PadPx is a lie.");
                Assert.AreEqual(ShorePlantCatalog.PadPx(entry), tideSpec.Pad,
                    $"{key}: the catalog's pad vs the rig's");

                table.AppendLine(
                    $"  {key,-16} {tideSpec.CellW,3}×{tideSpec.CellH,-3}   " +
                    $"{tideSpec.PivotX,3},{tideSpec.PivotY,-3}   {tideSpec.Pad,3}   " +
                    $"{varSpec.SheetW,4}×{varSpec.SheetH,-4}     {tideSpec.SheetW,4}×{tideSpec.SheetH,-4}");
            }

            Debug.Log(table.ToString());
        }

        [Test]
        public void TheBakersContractGuard_PassesOnTheShippedPair()
        {
            foreach (string key in ShorePlantCatalog.SpeciesKeys)
            foreach (string axis in new[] { ShorePlantBaker.TideAxis, ShorePlantBaker.VariantAxis })
            {
                var spec = ShorePlantBaker.ReadSheetSpec(_host, key, Stage, axis);
                Assert.DoesNotThrow(() => ShorePlantBaker.AssertFits(key, spec),
                                    $"{key} {axis}: the 2048 gate rejected the shipped sheet");
                Assert.DoesNotThrow(
                    () => ShorePlantBaker.AssertMatchesContract(key, spec, axis, _contract),
                    $"{key} {axis}: the staleness guard rejected the shipped pair");
            }
        }

        [Test]
        public void ARenderedCell_IsExactlyTheCellTheContractPromises_OnAllThreeChannels()
        {
            foreach (string key in ZoneProbes)
            {
                var entry = _contract.Find(key);
                int expected = entry.CellW * entry.CellH * 4;
                var cell = Render(key, variant: 0, tide: "half", frame: 0);

                Assert.AreEqual(expected, cell.Rgba.Length, $"{key} albedo buffer");
                Assert.AreEqual(expected, cell.Light.Length, $"{key} light-mask buffer");
                Assert.AreEqual(expected, cell.State.Length, $"{key} state-mask buffer");

                Assert.AreEqual(entry.CellW, (int)_host.EvaluateNumber($"{Res}.w"), $"{key} cell w");
                Assert.AreEqual(entry.CellH, (int)_host.EvaluateNumber($"{Res}.h"), $"{key} cell h");
            }
        }

        [Test]
        public void TheSameCell_RendersIdenticallyTwice()
        {
            // Determinism (rule 5) — a rig that drifted between calls would make every downstream
            // assert here a coin flip, and the bake itself unreproducible.
            var a = Render("KnottedWrack", 1, "ebb", 2);
            var b = Render("KnottedWrack", 1, "ebb", 2);
            CollectionAssert.AreEqual(a.Rgba, b.Rgba, "albedo differed between identical renders");
            CollectionAssert.AreEqual(a.Light, b.Light, "light mask differed");
            CollectionAssert.AreEqual(a.State, b.State, "state mask differed");
        }

        // =====================================================================================
        // 2. RULING ONE, live — one pivot, every tide state
        // =====================================================================================

        /// <summary>
        /// ⭐ The union-cell invariant, proven on actual renders rather than read off the contract.
        /// The runtime tide axis is CONTINUOUS, so a plant swaps sprite state constantly as the
        /// water rises over its ground; one pivot is what makes those swaps anchored by
        /// construction. A 1 px disagreement here is a visible hop the moment the tide crosses a
        /// threshold — which is why sheet waste was accepted as the cheaper cost.
        /// </summary>
        [Test]
        public void OnePivotAndOneCell_ServeEveryTideStateSeasonAndVariant()
        {
            var report = new StringBuilder("[plant-bake] union-cell invariant, live:\n");

            foreach (string key in ZoneProbes)
            {
                var entry = _contract.Find(key);
                var seen = new HashSet<string>();

                foreach (string tide in ShorePlantCatalog.TideKeys)
                foreach (int variant in new[] { 0, 3 })
                {
                    Render(key, variant, tide, frame: 0);
                    int w = (int)_host.EvaluateNumber($"{Res}.w");
                    int h = (int)_host.EvaluateNumber($"{Res}.h");
                    int px = (int)_host.EvaluateNumber($"{Res}.pivot.x");
                    int py = (int)_host.EvaluateNumber($"{Res}.pivot.y");

                    Assert.AreEqual(entry.CellW, w, $"{key} @ {tide} v{variant}: cell width moved");
                    Assert.AreEqual(entry.CellH, h, $"{key} @ {tide} v{variant}: cell height moved");
                    Assert.AreEqual(entry.Pivot.x, px, $"{key} @ {tide} v{variant}: pivot x moved");
                    Assert.AreEqual(entry.Pivot.y, py,
                        $"{key} @ {tide} v{variant}: the ground contact moved with the tide. That " +
                        "is the state hop the union cell exists to prevent.");

                    seen.Add($"{w}×{h}@{px},{py}");
                }

                Assert.AreEqual(1, seen.Count,
                    $"{key}: {seen.Count} distinct cell/pivot pairs across the tide × variant " +
                    $"matrix — {string.Join(" / ", seen)}");
                report.AppendLine(
                    $"  {key,-16} 10 states → 1 cell/pivot: {seen.Single()}  " +
                    $"(drape {ShorePlantCatalog.DrapeMetres(entry):F2} m below the contact)");
            }

            Debug.Log(report.ToString());
        }

        // =====================================================================================
        // 3. RULING TWO, live — no strap pixel carries a rim
        // =====================================================================================

        /// <summary>
        /// ⭐ The strap exemption's other half, measured on pixels. A grass blade is exempt from the
        /// body mass floor and pays for it by being forbidden a rim — so <c>light.G</c> must be
        /// identically 0 wherever <c>state.B</c> flags a strap. This is the branch the sprite-light
        /// include (#314) will take; if a single strap pixel leaked a rim, a shader that trusted the
        /// declaration would light it wrong and it would read as an art bug about rims on grass.
        /// </summary>
        [Test]
        public void NoStrapPixelCarriesARim_OnAnySpeciesOrTideState()
        {
            var report = new StringBuilder(
                "[plant-bake] strap-rim scan (light.G where state.B == 255):\n");
            long totalStrapPx = 0;

            foreach (string key in ZoneProbes)
            foreach (string tide in new[] { "low", "half", "high" })
            {
                var cell = Render(key, variant: 0, tide: tide, frame: 0);
                int strapPx = 0, leaks = 0, maxLeak = 0, bodyRim = 0;

                for (int i = 0; i < cell.State.Length; i += 4)
                {
                    if (cell.State[i + 3] == 0) continue;              // no coverage
                    bool strap = cell.State[i + 2] == 255;
                    int rim = cell.Light[i + 1];
                    if (strap)
                    {
                        strapPx++;
                        if (rim > 0) { leaks++; maxLeak = Mathf.Max(maxLeak, rim); }
                    }
                    else if (rim > 0) bodyRim++;
                }

                Assert.AreEqual(0, leaks,
                    $"{key} @ {tide}: {leaks} strap pixel(s) carry light.G (worst {maxLeak}). The " +
                    "strap exemption is bought by forbidding the rim — a leak breaks the deal the " +
                    "mass-floor exemption was granted on.");

                // The rig's own audit of the same thing, read back so a future rig that stopped
                // reporting it fails here rather than silently.
                Assert.AreEqual(0, (int)_host.EvaluateNumber($"{Res}.report.strapRimLeak"),
                                $"{key} @ {tide}: the rig's own strapRimLeak counter");

                report.AppendLine($"  {key,-16} @ {tide,-5} {strapPx,6} strap px, {leaks} rim leak(s); " +
                                  $"{bodyRim,6} body/wood px DO carry rim");

                // …and the scan must be looking at something. A cell with no strap pixels at all
                // would pass the leak assert vacuously — and every ZoneProbe is a strapped family
                // (blades, fronds, culms), so a zero here means the flag stopped being written.
                Assert.Greater(strapPx, 0,
                    $"{key} @ {tide}: no strap pixels found, but this is a strapped family — the " +
                    "scan proved nothing. state.B has stopped tracking the material.");
                totalStrapPx += strapPx;
            }

            // ⭐ The control. A woody shrub is legitimately strap-FREE, so it must report zero
            // straps while still carrying rim — which is what proves state.B tracks the material
            // rather than being painted on (or off) wholesale. Without this, "0 leaks" everywhere
            // would be equally consistent with a flag nobody writes.
            var woody = Render(WoodyProbe, variant: 0, tide: "half", frame: 0);
            int woodyStrap = 0, woodyRim = 0, woodyInk = 0;
            for (int i = 0; i < woody.State.Length; i += 4)
            {
                if (woody.State[i + 3] == 0) continue;
                woodyInk++;
                if (woody.State[i + 2] == 255) woodyStrap++;
                if (woody.Light[i + 1] > 0) woodyRim++;
            }

            Assert.AreEqual(0, woodyStrap,
                $"{WoodyProbe} is a woody shrub — body, wood and fleck berries, no strap. " +
                $"{woodyStrap} strap pixel(s) means state.B is being painted where no strap is.");
            Assert.Greater(woodyRim, 0,
                $"{WoodyProbe} carries no rim at all — a strap-free species must still rim, or the " +
                "scan cannot tell 'no straps' from 'no light mask'.");

            report.AppendLine(
                $"  {WoodyProbe,-16} @ half  {woodyStrap,6} strap px (CONTROL: woody, expected 0), " +
                $"{woodyRim} rim px of {woodyInk} ink — state.B tracks the material, it is not painted on.");
            report.AppendLine($"  ── {totalStrapPx} strap pixels scanned across 15 cells, 0 leaks.");

            Debug.Log(report.ToString());
        }

        /// <summary>
        /// SABOTAGE, MEASURED. Flip ONE strap pixel's rim and prove the scan finds exactly it — an
        /// assert that has never been shown to fire on a single-pixel violation is decoration, and a
        /// single pixel is the size a real rim leak would be.
        /// </summary>
        [Test]
        public void Sabotage_ASingleStrapPixelWithARim_IsCaughtByTheScan()
        {
            var cell = Render("Cordgrass", variant: 0, tide: "low", frame: 0);
            var tampered = (byte[])cell.Light.Clone();

            int target = -1;
            for (int i = 0; i < cell.State.Length; i += 4)
                if (cell.State[i + 3] != 0 && cell.State[i + 2] == 255) { target = i; break; }
            Assert.GreaterOrEqual(target, 0, "no strap pixel to sabotage — the scan is vacuous");

            Assert.AreEqual(0, CountStrapRim(cell.State, cell.Light), "the honest bake leaks nothing");
            tampered[target + 1] = 1;   // the smallest possible leak: G = 1 out of 255
            Assert.AreEqual(1, CountStrapRim(cell.State, tampered),
                "SABOTAGE NOT DETECTED — a single strap pixel with G = 1 slipped past the scan.");

            Debug.Log($"[plant-bake] SABOTAGE — strap rim: one pixel raised to G=1 out of " +
                      $"{cell.State.Length / 4} → detected. Honest bake: 0 leaks.");
        }

        static int CountStrapRim(byte[] state, byte[] light)
        {
            int n = 0;
            for (int i = 0; i < state.Length; i += 4)
                if (state[i + 3] != 0 && state[i + 2] == 255 && light[i + 1] > 0) n++;
            return n;
        }

        // =====================================================================================
        // 3b. ADR 0031, live — the keyline is retired, and retiring it touches nothing else
        // =====================================================================================

        /// <summary>
        /// ⭐ The outline retirement (ADR 0031), pinned on rendered pixels — and pinned as the
        /// STRUCTURAL claim, not as a colour match.
        ///
        /// <para><b>The definition used here is exact.</b> A keyline pixel is one the shade pass
        /// made opaque where the rig has <i>no geometry</i> — <c>rgba.a != 0</c> while
        /// <c>res.alpha == 0</c>. That is what the ring pass does and the only thing it does, so it
        /// cannot be confused with a plant pixel that happens to be dark, and it stays true if the
        /// keyline colour is ever re-tuned for the A/B arm.</para>
        ///
        /// <para><b>Why the second half matters more than the first.</b> "No keyline" alone would
        /// also pass if the ring pass were broken, or the renderer returned nothing. So the test
        /// carries its own control: <c>{outline:true}</c> must bring the ring BACK, and — the real
        /// assertion — <b>every pixel that differs between the two arms must be a ring pixel</b>.
        /// That is what makes the retirement provably a pure ring deletion: no painted pixel of any
        /// plant changes value, so no colour, band, rim or tide state can have moved with it.
        /// Measured across all sixteen species before the switch was flipped: 4,072 ring px against
        /// 10,482 painted px (0.39×), 0.94× on glasswort — and 0 violations.</para>
        ///
        /// <para><b>⚠ Do not "simplify" the strap's dark margin away chasing the same goal.</b> It
        /// is an interior shading term (the form's own dark side, ADR 0031 §1) and it is what holds
        /// a 2 px blade's edge now the ring is gone — it is the replacement, not the offender.</para>
        /// </summary>
        [Test]
        public void TheKeylineIsRetired_AndTurningItBackOn_ChangesOnlyTheRing()
        {
            var report = new StringBuilder("[plant-bake] ADR 0031 — keyline retirement, live:\n");
            int totalRing = 0, totalPainted = 0;

            foreach (string key in ZoneProbes.Concat(new[] { WoodyProbe }))
            {
                var shipped = RenderWithOpts(key, variant: 0, tide: "half", frame: 0, opts: null);
                byte[] shippedGeom = _host.EvaluateBytes($"{Res}.alpha");
                var restored = RenderWithOpts(key, variant: 0, tide: "half", frame: 0,
                                              opts: "outline:true");
                byte[] restoredGeom = _host.EvaluateBytes($"{Res}.alpha");

                // The geometry itself must be untouched by the flag — otherwise "only the ring
                // changed" would be comparing two different plants.
                CollectionAssert.AreEqual(shippedGeom, restoredGeom,
                    $"{key}: the outline flag moved the GEOMETRY. The ring pass writes only where " +
                    "there is no geometry; if this fired it is doing something else as well.");

                int ringDefault = 0, ringRestored = 0, painted = 0, violations = 0;
                for (int i = 0, p = 0; i < shipped.Rgba.Length; i += 4, p++)
                {
                    bool hasGeometry = shippedGeom[p] != 0;
                    if (hasGeometry) painted++;
                    if (!hasGeometry && shipped.Rgba[i + 3] != 0) ringDefault++;
                    if (!hasGeometry && restored.Rgba[i + 3] != 0) ringRestored++;

                    bool differs = shipped.Rgba[i] != restored.Rgba[i] ||
                                   shipped.Rgba[i + 1] != restored.Rgba[i + 1] ||
                                   shipped.Rgba[i + 2] != restored.Rgba[i + 2] ||
                                   shipped.Rgba[i + 3] != restored.Rgba[i + 3];
                    if (differs && hasGeometry) violations++;
                }

                Assert.AreEqual(0, ringDefault,
                    $"{key}: {ringDefault} pixel(s) are opaque where the rig has no geometry, on a " +
                    "DEFAULT render. The keyline is retired (ADR 0031) — a default bake must draw " +
                    "no ring at all.");

                // The control. Without this, "0 ring pixels" would also be satisfied by a ring pass
                // that no longer works, or a species that renders nothing.
                Assert.Greater(ringRestored, 0,
                    $"{key}: {{outline:true}} produced no ring either — the A/B arm is broken, so " +
                    "the zero above proves nothing about the default.");

                Assert.AreEqual(0, violations,
                    $"{key}: {violations} PAINTED pixel(s) differ between the retired and restored " +
                    "arms. Retiring the keyline must be a pure ring deletion — if a plant's own " +
                    "pixels move with the flag, the ring pass is writing inside the silhouette.");

                totalRing += ringRestored;
                totalPainted += painted;
                report.AppendLine(
                    $"  {key,-16} {painted,6} painted px · ring {ringRestored,5} px " +
                    $"({ringRestored / (float)Math.Max(1, painted):F2}× painted) · " +
                    $"default ring {ringDefault} · painted-pixel diffs {violations}");
            }

            report.AppendLine(
                $"  ── {totalRing} ring px against {totalPainted} painted px across " +
                $"{ZoneProbes.Length + 1} probes ({totalRing / (float)totalPainted:F2}×), " +
                "0 painted pixels touched. The ring was a PERIMETER cost, which is why this " +
                "strap-heavy kit paid the most for it.");
            Debug.Log(report.ToString());
        }

        // =====================================================================================
        // 4. RULING THREE, live — submerged cells bake NO caustic
        // =====================================================================================

        /// <summary>
        /// ⭐ The kit's water contract, measured on rendered pixels rather than read off the
        /// contract's flags. Submergence bakes COLOUR only — darker, cooler, contrast falling with
        /// depth — because the live sprite-light shader's caustics are swell-driven, and a baked
        /// dapple would put two uncorrelated patterns on the same frond.
        ///
        /// <para><b>What this measures, and why it is not a scanline scan.</b> The first version of
        /// this test counted luminance direction changes along each submerged scanline — the obvious
        /// "is it rippled?" measure. It does not work here, and the numbers say so: a <i>clump</i>
        /// species is many separate blades side by side, so a scanline crosses genuine
        /// blade-to-blade steps. Cordgrass scored 21.1 honest against 30.6 with an injected dapple —
        /// a 1.45× separation, which is not a test. <b>A horizontal scan cannot tell a row of thin
        /// blades from a ripple.</b>
        ///
        /// <para>So measure the contract's actual claim instead: submergence is a <b>colour ramp
        /// applied per pixel</b> — "darker, cooler, contrast falling monotonically with depth". A
        /// ramp maps a quantised palette onto another quantised palette, so a submerged region holds
        /// a BOUNDED SET OF DISTINCT COLOURS however many blades it contains. A dapple is a spatial
        /// term added on top, which multiplies that palette by its own amplitude.</para>
        ///
        /// <para><b>⚠ And the threshold is per-sprite, not a constant.</b> The species' palettes
        /// differ by an order of magnitude honestly — a single broad kelp blade holds ~8 distinct
        /// submerged colours, a cordgrass clump 84 — so any global ceiling either waves the simple
        /// species through or fails the complex one. This test therefore <b>carries its own
        /// control</b>: it dapples each sprite and requires the honest bake to sit a multiple below
        /// what a caustic would have produced <i>on that same sprite</i>. Nothing is tuned.</para>
        /// </summary>
        [Test]
        public void ASubmergedPlantIsTINTED_NotDAPPLED_SoNoCausticIsEverBaked()
        {
            var report = new StringBuilder(
                "[plant-bake] submerged-cell caustic scan (distinct colours per 100 submerged px):\n");

            foreach (string key in ZoneProbes)
            {
                // 'high' floods every zone that the tide can reach at all; the upland probe is
                // expected to stay dry and is reported as such rather than skipped silently.
                var cell = Render(key, variant: 0, tide: "high", frame: 0);
                var scan = Scan(cell);

                if (scan.SubmergedPx == 0)
                {
                    report.AppendLine($"  {key,-16} DRY at high water (upland) — nothing submerged " +
                                      "to scan, which is itself the zone rule.");
                    Assert.AreEqual("upland", _contract.Find(key).Zone,
                        $"{key} has no submerged pixels at mean high water but is not an upland " +
                        "plant — either the zone table or the tide resolution is wrong.");
                    continue;
                }

                // A submerged frond must be COLOUR-shifted — cooler and darker. If nothing changed,
                // the scan is measuring an unsubmerged sprite and proves nothing.
                Assert.Greater(scan.SubmergedPx, 20,
                    $"{key}: only {scan.SubmergedPx} submerged pixels — too few to scan.");

                // …and it must actually be tinted, or "no dapple" is trivially true of a sprite the
                // tide never touched.
                Assert.AreNotEqual(0f, scan.MeanLuma, $"{key}: submerged pixels are black");

                // ⭐ The control, computed on this very sprite: what its submerged palette WOULD
                // look like with a caustic in it.
                var dappled = Scan(Dapple(cell, (int)_host.EvaluateNumber($"{Res}.w")));
                float headroom = dappled.DistinctColours / (float)Mathf.Max(1, scan.DistinctColours);

                Assert.GreaterOrEqual(headroom, MinDappleHeadroom,
                    $"{key}: {scan.DistinctColours} distinct submerged colours against " +
                    $"{dappled.DistinctColours} for the same sprite WITH a caustic — only " +
                    $"{headroom:F1}× apart. Either a spatial term is already baked in, or the " +
                    "palette is too rich for this measure to separate them. The live shader owns " +
                    "the caustics (ADR 0010/0012/0023).");

                report.AppendLine(
                    $"  {key,-16} {scan.SubmergedPx,6} submerged px  " +
                    $"{scan.DistinctColours,4} distinct colours ({scan.DistinctPer100,5:F1}/100)  " +
                    $"mean luma {scan.MeanLuma,5:F1} (dry {scan.DryLuma,5:F1})  " +
                    $"— a caustic would make it {dappled.DistinctColours,4} ({headroom:F1}×)");
            }

            Debug.Log(report.ToString());
        }

        /// <summary>
        /// How far below "the same sprite with a caustic in it" the honest bake must sit.
        ///
        /// <para>The injected ripple takes exactly five distinct values at a 5 px integer period, so
        /// a clean ramp multiplies its palette by at most 5×, less whatever the two palettes already
        /// share. <b>Measured across the four submerged probes: SugarKelp 7→32 (4.6×),
        /// KnottedWrack 56→167 (3.0×), Cordgrass 84→283 (3.4×), Cattail 48→153 (3.2×).</b> A bake
        /// that already carried a dapple would score near 1× — adding a second ripple to a rippled
        /// palette mostly collides. 2.5 sits between the two with room on both sides rather than
        /// being tuned to the tightest observation.</para>
        /// </summary>
        const float MinDappleHeadroom = 2.5f;

        /// <summary>
        /// A 5 px-period brightness ripple over the submerged pixels — the coarsest a caustic would
        /// ever be at 32 px/m, and therefore the easiest for a scan to MISS. A finer one would only
        /// be more obvious.
        /// </summary>
        static Cell Dapple(Cell cell, int w)
        {
            var rgba = (byte[])cell.Rgba.Clone();
            for (int i = 0, p = 0; i < rgba.Length; i += 4, p++)
            {
                if (cell.State[i + 3] == 0 || cell.State[i + 1] != 255) continue;
                int x = p % w, y = p / w;
                float ripple = 40f * Mathf.Sin((x + y) * 2f * Mathf.PI / 5f);
                for (int ch = 0; ch < 3; ch++)
                    rgba[i + ch] = (byte)Mathf.Clamp(rgba[i + ch] + ripple, 0, 255);
            }
            return new Cell { Rgba = rgba, Light = cell.Light, State = cell.State };
        }

        /// <summary>
        /// SABOTAGE, MEASURED — the reason the ceiling above is not decoration. Inject a synthetic
        /// caustic into the submerged pixels of a real render and prove the same scan flags it, by a
        /// wide margin. A metric that has never been shown to separate the two cases is a number,
        /// not a test.
        /// </summary>
        [Test]
        public void Sabotage_AnInjectedDapple_IsSeparatedFromTheHonestBakeByAWideMargin()
        {
            // Both ends of the honest range: the quietest submerged probe (a single broad kelp
            // blade) and the noisiest (a clump of many separate cordgrass blades, whose real
            // geometry produces the most direction changes of any species measured). Proving the
            // separation only on the easy one would leave the tight case untested.
            var report = new StringBuilder("[plant-bake] SABOTAGE — caustic scan:\n");

            foreach (string key in new[] { "SugarKelp", "Cordgrass" })
            {
                var cell = Render(key, variant: 0, tide: "high", frame: 0);
                int w = (int)_host.EvaluateNumber($"{Res}.w");
                var honest = Scan(cell);
                Assert.Greater(honest.SubmergedPx, 100,
                               $"{key}: need a properly submerged cell to sabotage");

                var sabotaged = Scan(Dapple(cell, w));

                Assert.GreaterOrEqual(sabotaged.DistinctColours,
                                      honest.DistinctColours * MinDappleHeadroom,
                    $"SABOTAGE NOT DETECTED — on {key} an injected 5 px dapple produced " +
                    $"{sabotaged.DistinctColours} distinct submerged colours against the honest " +
                    $"bake's {honest.DistinctColours}. The scan cannot tell a caustic from a colour " +
                    "ramp, so the ruling-3 assert is decoration.");

                // ⚠ The per-100 ratio is reported but NOT asserted on: distinct-colour count
                // saturates at the palette while the pixel count does not, so dividing by area
                // measures how big the sprite is, not how dappled it is. Measured: a dappled
                // SugarKelp scores 4.1/100 — LOWER than an honest Cordgrass at 13.1 — which is why
                // the assert above compares a sprite only against itself.
                report.AppendLine(
                    $"  {key,-12} honest {honest.DistinctColours,4} distinct " +
                    $"({honest.DistinctPer100,5:F1}/100) → dappled {sabotaged.DistinctColours,4} " +
                    $"({sabotaged.DistinctPer100,5:F1}/100) — " +
                    $"{sabotaged.DistinctColours / (float)Mathf.Max(1, honest.DistinctColours):F1}× " +
                    $"separation, floor {MinDappleHeadroom}×");
            }

            Debug.Log(report.ToString());
        }

        readonly struct SubmergedScan
        {
            public readonly int SubmergedPx, DistinctColours;
            public readonly float DistinctPer100, MeanLuma, DryLuma;

            public SubmergedScan(int submerged, int distinct, float mean, float dry)
            {
                SubmergedPx = submerged;
                DistinctColours = distinct;
                DistinctPer100 = submerged == 0 ? 0f : distinct * 100f / submerged;
                MeanLuma = mean;
                DryLuma = dry;
            }
        }

        /// <summary>
        /// Distinct RGB values among the SUBMERGED pixels of a cell. Geometry-blind by
        /// construction: a thousand blades that all take the same water ramp still produce the same
        /// handful of colours, while a spatial term added on top multiplies them.
        /// </summary>
        static SubmergedScan Scan(Cell cell)
        {
            var colours = new HashSet<int>();
            int submerged = 0, dryPx = 0;
            double lumaSum = 0, drySum = 0;

            for (int i = 0; i < cell.Rgba.Length; i += 4)
            {
                if (cell.State[i + 3] == 0) continue;                  // no coverage

                float luma = 0.299f * cell.Rgba[i] + 0.587f * cell.Rgba[i + 1] +
                             0.114f * cell.Rgba[i + 2];

                if (cell.State[i + 1] != 255)                          // above the waterline
                {
                    dryPx++; drySum += luma;
                    continue;
                }

                submerged++; lumaSum += luma;
                colours.Add((cell.Rgba[i] << 16) | (cell.Rgba[i + 1] << 8) | cell.Rgba[i + 2]);
            }

            return new SubmergedScan(
                submerged, colours.Count,
                submerged == 0 ? 0f : (float)(lumaSum / submerged),
                dryPx == 0 ? 0f : (float)(drySum / dryPx));
        }

        // =====================================================================================
        // 5. SABOTAGE — the staleness guard
        // =====================================================================================

        /// <summary>
        /// Sabotage the staleness guard: hand it the geometry a drifted rig would produce and prove
        /// it refuses. This guard is the only thing standing between a re-baked union cell and
        /// sixteen wrong ground contacts, because no sheet exists yet to disagree with the contract.
        /// </summary>
        [Test]
        public void Sabotage_ADriftedCellPivotOrSheet_MakesTheBakerRefuse()
        {
            const string key = "KnottedWrack";
            var real = ShorePlantBaker.ReadSheetSpec(_host, key, Stage, ShorePlantBaker.TideAxis);

            var cases = new (ShorePlantBaker.SheetSpec spec, string what)[]
            {
                (With(real, cellH: real.CellH + 1),
                 "union cell one row TALLER (every pivot y is now off by one)"),
                (With(real, cellW: real.CellW + 2),
                 "union cell two px WIDER (the centred pivot is off by one)"),
                (With(real, pivotY: real.PivotY - 1),
                 "ground contact one row HIGHER (a 3 cm hop at every tide crossing)"),
                (With(real, pivotX: real.PivotX + 1),
                 "ground contact one px SIDEWAYS"),
                (With(real, cols: real.Cols + 1),
                 "an EXTRA tide column (the columns the slicer numbers rects against moved)"),
                (With(real, rows: real.Rows - 1),
                 "a DROPPED sway row"),
            };

            var table = new StringBuilder("[plant-bake] SABOTAGE — the staleness guard:\n");
            foreach (var (spec, what) in cases)
            {
                Assert.Throws<InvalidOperationException>(
                    () => ShorePlantBaker.AssertMatchesContract(key, spec, ShorePlantBaker.TideAxis,
                                                                _contract),
                    $"SABOTAGE NOT DETECTED — {what} passed the guard.");
                table.AppendLine($"  {what,-62} → refused");
            }

            Assert.Throws<InvalidOperationException>(
                () => ShorePlantBaker.AssertMatchesContract("Rockweed", real,
                                                            ShorePlantBaker.TideAxis, _contract),
                "SABOTAGE NOT DETECTED — a species absent from the contract passed the guard.");
            table.AppendLine("  a species the contract does not name                            → refused");

            // The true pair must still pass, or the guard is broken rather than strict.
            Assert.DoesNotThrow(
                () => ShorePlantBaker.AssertMatchesContract(key, real, ShorePlantBaker.TideAxis,
                                                            _contract));
            table.AppendLine($"  the SHIPPED geometry ({real.CellW}×{real.CellH} @ " +
                             $"{real.PivotX},{real.PivotY})                        → accepted");

            Debug.Log(table.ToString());
        }

        /// <summary>
        /// 🔴 Sabotage the 2048 gate. Over the cap Unity imports the sheet DOWNSCALED with the
        /// sprite COUNT still matching, so nothing downstream notices until a pivot lands wrong.
        /// The union-cell policy is what makes this reachable at all — it trades sheet area for
        /// pivot stability — so the gate has to fire on the axis that would grow.
        /// </summary>
        [Test]
        public void Sabotage_ASheetOverTheImportCap_MakesTheBakerRefuse()
        {
            var real = ShorePlantBaker.ReadSheetSpec(_host, "SugarKelp", Stage,
                                                     ShorePlantBaker.TideAxis);
            Assert.DoesNotThrow(() => ShorePlantBaker.AssertFits("SugarKelp", real),
                                "the shipped sheet must pass the gate");

            int overW = ShorePlantCatalog.ImportSizeCap / real.Cols + 1;
            int overH = ShorePlantCatalog.ImportSizeCap / real.Rows + 1;

            Assert.Throws<InvalidOperationException>(
                () => ShorePlantBaker.AssertFits("SugarKelp", With(real, cellW: overW)),
                "SABOTAGE NOT DETECTED — a sheet wider than the cap passed the gate.");
            Assert.Throws<InvalidOperationException>(
                () => ShorePlantBaker.AssertFits("SugarKelp", With(real, cellH: overH)),
                "SABOTAGE NOT DETECTED — a sheet taller than the cap passed the gate.");

            // …and one pixel under must still pass, so the gate is a cap rather than a margin.
            Assert.DoesNotThrow(
                () => ShorePlantBaker.AssertFits(
                    "SugarKelp", With(real, cellW: ShorePlantCatalog.ImportSizeCap / real.Cols)));

            Debug.Log($"[plant-bake] SABOTAGE — 2048 gate: SugarKelp ships " +
                      $"{real.SheetW}×{real.SheetH} ({ShorePlantCatalog.ImportSizeCap - Mathf.Max(real.SheetW, real.SheetH)} px " +
                      $"headroom); a {overW}px cell ({overW * real.Cols} wide) is refused.");
        }

        static ShorePlantBaker.SheetSpec With(in ShorePlantBaker.SheetSpec s, int cellW = -1,
                                              int cellH = -1, int cols = -1, int rows = -1,
                                              int pivotX = -1, int pivotY = -1) =>
            new ShorePlantBaker.SheetSpec(
                cellW < 0 ? s.CellW : cellW, cellH < 0 ? s.CellH : cellH,
                cols < 0 ? s.Cols : cols, rows < 0 ? s.Rows : rows,
                pivotX < 0 ? s.PivotX : pivotX, pivotY < 0 ? s.PivotY : pivotY, s.Pad);

        // =====================================================================================
        // render plumbing
        // =====================================================================================

        const string Res = "__hhPlantTestRender";

        struct Cell
        {
            public byte[] Rgba, Light, State;
        }

        /// <summary>
        /// One render, three channels. Parked on a global and read three times rather than rendered
        /// three times: the rig recomputes every channel from the geometry on each call, so this is
        /// the difference between a fast fixture and a slow one.
        /// </summary>
        Cell Render(string species, int variant, string tide, int frame)
            => RenderWithOpts(species, variant, tide, frame, opts: null);

        /// <summary>
        /// <see cref="Render"/> with extra render options spliced into the SAME expression the
        /// baker builds, so an A/B arm cannot drift from the production call in any other respect.
        /// The splice targets the trailing <c>}</c> of the baker's own options object rather than
        /// restating it — <c>opts</c> is a bare JS fragment, e.g. <c>"outline:true"</c>.
        /// </summary>
        Cell RenderWithOpts(string species, int variant, string tide, int frame, string opts)
        {
            string g = ShorePlantCatalog.RigGlobalName;
            string expr = ShorePlantBaker.RenderExpr(species, variant, Season, Stage, tide, frame);

            if (!string.IsNullOrEmpty(opts))
            {
                int close = expr.LastIndexOf("})", StringComparison.Ordinal);
                Assert.Greater(close, 0,
                    $"ShorePlantBaker.RenderExpr no longer ends in an options object — this splice " +
                    $"is built on that shape. Got: {expr}");
                expr = expr.Substring(0, close) + "," + opts + expr.Substring(close);
            }

            _host.Execute($"globalThis.{Res} = {expr};");
            return new Cell
            {
                Rgba = _host.EvaluateBytes($"{Res}.rgba"),
                Light = _host.EvaluateBytes($"{g}.packMask({Res})"),
                State = _host.EvaluateBytes($"{g}.packState({Res})"),
            };
        }

        static string[] _c(IEnumerable<string> xs) => xs.ToArray();
    }
}
