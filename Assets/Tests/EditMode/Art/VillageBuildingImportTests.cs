using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Art;
using HiddenHarbours.Art.Editor;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// Acceptance for the VILLAGE BUILDING KIT's consumer chain: the committed contract, the sliced
    /// sheets, and the one place a building is turned into a scene object.
    ///
    /// <para>The chain's failure modes are almost all silent, which is what shapes this file:</para>
    /// <list type="bullet">
    ///   <item>A <b>bottom-centre pivot</b> passes every other import check and just stands the building
    ///   in the dirt — so the pivot is asserted per sprite, against the contract, and the size of the
    ///   error is measured rather than assumed.</item>
    ///   <item>A sheet <b>over the 2048 import cap</b> arrives DOWNSCALED with the correct sprite count —
    ///   so the cell size is checked on the imported sprite, not just the file.</item>
    ///   <item>A <b>hand-edited contract</b> would be silently obeyed — so the derived fields are
    ///   recomputed and the sidecar the same bake wrote is cross-checked against it.</item>
    /// </list>
    ///
    /// <para>Everything here is skipped, not failed, when the kit has not been baked on this machine: the
    /// sheets are committed, so a fresh checkout has them — but a branch mid-rebake should not turn the
    /// whole suite red.</para>
    /// </summary>
    public class VillageBuildingImportTests
    {
        VillageBuildingKit.Contract _contract;

        [SetUp]
        public void LoadContract()
        {
            if (!File.Exists(VillageBuildingKit.ContractPath))
                Assert.Ignore($"{VillageBuildingKit.ContractPath} is not present — run " +
                              "Hidden Harbours ▸ Art ▸ Bake Village Buildings.");

            _contract = VillageBuildingKit.Load();
            Assert.IsNotNull(_contract, "the contract is present but did not parse");
        }

        // ---- the contract ----------------------------------------------------------------------

        [Test]
        public void TheContractCoversEveryBuildTheKitDeclares()
        {
            // ⚠️ AllBuilds, not M1Set: the kit bakes the M1 village AND the lifecycle set (the three
            // derelicts on Ginny's plot and the shut cannery), into the same folder and the same
            // contract. M1Set alone would have let a derelict go missing from the contract in silence
            // and, worse, would have failed the count check the moment the first one was baked.
            foreach (var build in VillageBuildingKit.AllBuilds)
                Assert.IsNotNull(VillageBuildingKit.Find(_contract, build.Key),
                    $"'{build.Key}' is a build this kit declares but the contract does not carry it. " +
                    "Re-bake — do not hand-edit the contract.");

            Assert.AreEqual(VillageBuildingKit.AllBuilds.Length, _contract.buildings.Length,
                "the contract should carry exactly what the kit declares: a stranger in it means a " +
                "stale bake, and the slicer would happily slice a sheet nothing places");
        }

        [Test]
        public void TheContractRecordsTheBakeScale_NotTheImportDefault()
        {
            Assert.AreEqual((int)ArtImportPipeline.PixelsPerUnit, _contract.ppu,
                "the rigs' own PX and the locked import PPU must agree — the sheet's pixels ARE its " +
                "metres × 32, and a disagreement here breaks the P2 scale ladder silently");
            Assert.AreEqual(VillageBuildingKit.Facings, _contract.facings);
        }

        [Test]
        public void EveryEntrysDerivedFieldsMatchTheirDefinition()
        {
            // The contract stores the Unity pivot as well as the cropped one, which invites the two to
            // drift — and a hand edit to either would be obeyed in silence. Recomputed here from the ONE
            // definition, so an edit is a failing test.
            foreach (var e in _contract.buildings)
            {
                Vector2 expected = VillageBuildingKit.NormalizedPivot(e);
                Assert.AreEqual(expected.x, e.unityPivotX, 1e-4f, $"{e.key} unityPivotX");
                Assert.AreEqual(expected.y, e.unityPivotY, 1e-4f, $"{e.key} unityPivotY");

                Assert.AreEqual(e.cols * e.cellW, e.sheetW, $"{e.key}: sheetW must be cols × cellW");
                Assert.AreEqual(e.rows * e.cellH, e.sheetH, $"{e.key}: sheetH must be rows × cellH");
                Assert.GreaterOrEqual(e.cols * e.rows, e.facings,
                                      $"{e.key}: the grid must hold every facing");
                Assert.AreEqual(VillageBuildingKit.Facings, e.facings, $"{e.key} facings");

                // The pivot moved with the crop, and the world point is unchanged.
                Assert.LessOrEqual(e.cellW, e.nativeCellW, $"{e.key}: the crop cannot grow the cell");
                Assert.LessOrEqual(e.cellH, e.nativeCellH);
                Assert.Greater(e.cropSaving, 0f, $"{e.key}: the crop should have saved something");
            }
        }

        [Test]
        public void EverySheetIsInsideTheImportCap()
        {
            // ⚠️ This is the assert the FIRST real bake of this kit needed. Solved against the baker's own
            // 4096 limit, all five sheets came out 2800–3876 px wide — legal to bake, and every one would
            // have imported DOWNSCALED with the sprite count still correct. The kit now solves the pack
            // for the cap it imports at.
            // ⚠️ Against the cap the ENTRY was packed at, not the kit constant: one build (the cannery)
            // declares its own 4096 because eight facings of a 9.5 × 15.4 m building do not pack under
            // 2048 in any state. Reading the entry's own number is what stops that exception being
            // either a false red here or a licence for the next build to quietly take one.
            foreach (var e in _contract.buildings)
            {
                int cap = VillageBuildingKit.ImportCapFor(e);
                Assert.LessOrEqual(e.sheetW, cap,
                    $"{e.key} is {e.sheetW} px wide against its own {cap} px cap — over it the import " +
                    "DOWNSCALES silently and the sprite count still comes out right");
                Assert.LessOrEqual(e.sheetH, cap, $"{e.key} height against its own {cap} px cap");

                if (cap != VillageBuildingKit.ImportSizeCap)
                    Debug.Log($"[village-buildings] {e.key} imports at {cap} px " +
                              $"({e.sheetW}×{e.sheetH}, {e.runtimeBytesRgba32 / 1024.0 / 1024.0:F1} MB " +
                              "RGBA32) — the kit's one documented cap exception.");
            }
        }

        [Test]
        public void EveryFootprintIsAPlausibleBuildingInMetres()
        {
            // Honest metric scale is the art-pipeline charter's first guardrail and pillar P2's load-
            // bearing property. The azimuth probe already cross-checked these against the rendered
            // silhouette at 32 px = 1 m and refuses on disagreement; this pins the range so a rig drop
            // that halves or doubles the model is loud.
            foreach (var e in _contract.buildings)
            {
                Assert.That(e.footprintWidthMetres, Is.InRange(3f, 20f),
                            $"{e.key} is {e.footprintWidthMetres} m wide");
                Assert.That(e.footprintLengthMetres, Is.InRange(3f, 25f),
                            $"{e.key} is {e.footprintLengthMetres} m long");
            }
        }

        [Test]
        public void EveryEntryMatchesTheSidecarTheSameBakeWrote()
        {
            // The bake writes TWO files per build: this kit's aggregate contract and BuildingRigBaker's
            // per-build sidecar (which carries the per-facing overlay anchors nothing consumes yet). They
            // come from one BuildingBakeResult, so they cannot drift WITHIN a bake — but a hand edit to
            // either, or a half-finished re-bake, would show up here and nowhere else.
            foreach (var e in _contract.buildings)
            {
                string sidecar = VillageBuildingKit.BuildingsRoot +
                                 VillageBuildingKit.StemFor(e.key) + ".json";
                if (!File.Exists(sidecar))
                    Assert.Ignore($"{sidecar} is missing — this branch predates the sidecar, or the " +
                                  "bake was interrupted.");

                string json = File.ReadAllText(sidecar);
                StringAssert.Contains($"\"cellW\": {e.cellW}", json, $"{e.key} cellW");
                StringAssert.Contains($"\"cellH\": {e.cellH}", json, $"{e.key} cellH");
                StringAssert.Contains($"\"cropX\": {e.cropX}", json, $"{e.key} cropX");
                StringAssert.Contains($"\"cropY\": {e.cropY}", json, $"{e.key} cropY");
                StringAssert.Contains($"\"convention\": \"{e.convention}\"", json, $"{e.key} convention");
            }
        }

        [Test]
        public void TheMeasuredConventionIsRecorded_NotLeftBlank()
        {
            // Both building rigs sat in the rigs README's INFERRED counter-clockwise group and were
            // explicitly listed as UNMEASURED until a bake ran. This kit's bake is that bake: the probe
            // measures from the door anchor and REFUSES on a mismatch, so a recorded value here is a
            // measurement rather than a restatement of the prior.
            foreach (var e in _contract.buildings)
                Assert.IsFalse(string.IsNullOrEmpty(e.convention),
                               $"{e.key} has no measured convention recorded");
        }

        // ---- the slice -------------------------------------------------------------------------

        [Test]
        public void EverySheetImportedTheWayThisKitNeeds()
        {
            // One call, because VerifyAll is the single shared definition of "imported correctly" — the
            // batch entry point runs the same code, so the suite and a headless slice cannot disagree
            // about what passing means.
            RequireSheetsOnDisk();
            Assert.IsTrue(VillageBuildingSheetSlicer.VerifyAll(logEachPass: false),
                          "VerifyAll failed — see the logged mismatches. Re-run " +
                          "Hidden Harbours ▸ Art ▸ Import (after a new drop) ▸ Slice Village Building Sheets.");
        }

        [Test]
        public void EverySheetSlicesToExactlyItsFacings_AtTheContractsCell()
        {
            RequireSheetsOnDisk();
            foreach (var e in _contract.buildings)
            {
                string path = VillageBuildingKit.SheetPath(e.key);
                var sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToArray();

                Assert.AreEqual(e.facings, sprites.Length,
                    $"{e.key}: {e.facings} facings expected. (A grid can be wider than the facing " +
                    "count — 8 facings in a 3×3 leaves one empty — and a sprite over an empty cell is " +
                    "a transparent rect a placement tool would happily offer.)");

                foreach (var s in sprites)
                {
                    Assert.AreEqual(e.cellW, Mathf.RoundToInt(s.rect.width), $"{s.name} cell width");
                    Assert.AreEqual(e.cellH, Mathf.RoundToInt(s.rect.height), $"{s.name} cell height");
                }

                // Named by FACING, and every facing present exactly once.
                var names = sprites.Select(s => s.name).OrderBy(n => n).ToArray();
                var expected = Enumerable.Range(0, e.facings)
                                         .Select(f => VillageBuildingKit.SpriteNameFor(e.key, f))
                                         .OrderBy(n => n).ToArray();
                CollectionAssert.AreEqual(expected, names, $"{e.key} facing names");
            }
        }

        [Test]
        public void EveryFacingSharesOneCellAndOnePivot_SoABuildingDoesNotShiftWhenItTurns()
        {
            // THE property the union crop exists for, and the one whose failure is invisible in the
            // sheet: crop each facing to its own bounds and you get eight different cells and a building
            // that jumps as it is re-faced. Asserted across the sprites, not the contract, so it covers
            // the slice as well as the bake.
            RequireSheetsOnDisk();
            foreach (var e in _contract.buildings)
            {
                var sprites = AssetDatabase.LoadAllAssetsAtPath(VillageBuildingKit.SheetPath(e.key))
                                           .OfType<Sprite>().ToArray();
                if (sprites.Length == 0) continue;

                Vector2 pivot = sprites[0].pivot;
                Vector2 size = sprites[0].rect.size;
                foreach (var s in sprites)
                {
                    Assert.AreEqual(pivot.x, s.pivot.x, 0.01f, $"{s.name} pivot x differs from facing 0");
                    Assert.AreEqual(pivot.y, s.pivot.y, 0.01f, $"{s.name} pivot y differs from facing 0");
                    Assert.AreEqual(size.x, s.rect.width, 0.01f, $"{s.name} cell width differs");
                    Assert.AreEqual(size.y, s.rect.height, 0.01f, $"{s.name} cell height differs");
                }
            }
        }

        [Test]
        public void ThePivotIsTheGroundCentre_AndBottomCentreWouldSinkEveryBuilding()
        {
            // Measured, not asserted in the abstract: for each building, how far bottom-centre would put
            // it into the dirt. ArtImportPipeline.PivotFor sends everything under /buildings/ to
            // BottomCenter, so this is the specific default being overridden and the specific error being
            // avoided.
            RequireSheetsOnDisk();
            int ppu = _contract.ppu;
            bool anySink = false;

            foreach (var e in _contract.buildings)
            {
                var sprites = AssetDatabase.LoadAllAssetsAtPath(VillageBuildingKit.SheetPath(e.key))
                                           .OfType<Sprite>().ToArray();
                Vector2 expected = VillageBuildingKit.PivotPixels(e);

                foreach (var s in sprites)
                {
                    Assert.AreEqual(expected.x, s.pivot.x, 0.01f, $"{s.name} pivot x (ground CENTRE)");
                    Assert.AreEqual(expected.y, s.pivot.y, 0.01f, $"{s.name} pivot y (GROUND line)");
                }

                float sink = VillageBuildingCatalog.BelowGroundMetres(e, ppu);
                if (sink > 0f)
                {
                    anySink = true;
                    Assert.Greater(expected.y, 0f,
                        $"{e.key} draws {VillageBuildingKit.BelowGroundPad(e)} px below its ground " +
                        $"line ({sink:F2} m), so its pivot must sit above the cell's bottom row");
                }
            }

            Assert.IsTrue(anySink,
                "not one building in the kit has art below its ground line — either the rigs stopped " +
                "projecting the near eave toward the camera, or the crop is no longer taking the union " +
                "of all eight facings. Both would mean this whole override is now pointless, which is " +
                "worth finding out deliberately rather than by a placement bug.");
        }

        // ---- SABOTAGE: prove the guards have teeth ----------------------------------------------

        [Test]
        public void SABOTAGE_ASheetThatDisagreesWithItsContractCellIsRefused()
        {
            // Corrupt the CELL and the slicer must decline, because a wrong cell is exactly what a
            // silently-downscaled import looks like: the grid still divides, the sprite count still comes
            // out right, and every rect is the wrong size.
            RequireSheetsOnDisk();
            var real = _contract.buildings[0];

            var sabotaged = Clone(real);
            sabotaged.cellW = real.cellW - 7;                 // still divides into something plausible
            sabotaged.sheetW = sabotaged.cols * sabotaged.cellW;

            string path = VillageBuildingKit.SheetPath(real.key);
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            Assert.IsNotNull(tex);

            Assert.AreNotEqual(tex.width, sabotaged.sheetW,
                "the sabotage must actually change the expected sheet width, or it proves nothing");

            UnityEngine.TestTools.LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex(
                "VillageBuildingSheetSlicer.*but the contract says"));
            Assert.IsFalse(
                VillageBuildingSheetSlicer.SliceOne(path, VillageBuildingKit.StemFor(real.key), sabotaged),
                "the slicer must REFUSE a sheet whose size disagrees with its contract, not re-slice it " +
                "against the wrong cell");
        }

        [Test]
        public void SABOTAGE_MovingThePivotToBottomCentreIsAMeasurableError()
        {
            // The trap has to be worth guarding, so measure it: bottom-centre versus the ground centre,
            // in pixels and in metres, on the REAL committed contract.
            var worst = _contract.buildings.OrderByDescending(VillageBuildingKit.BelowGroundPad).First();

            Vector2 correct = VillageBuildingKit.NormalizedPivot(worst);
            var bottomCentre = new Vector2(0.5f, 0f);

            float errorPx = Mathf.Abs(correct.y - bottomCentre.y) * worst.cellH;
            float errorMetres = errorPx / _contract.ppu;

            Assert.AreEqual(VillageBuildingKit.BelowGroundPad(worst), Mathf.RoundToInt(errorPx),
                            "the error IS the below-ground pad");
            Assert.Greater(errorMetres, 0.1f,
                $"'{worst.key}' would sink {errorMetres:F2} m under a bottom-centre pivot — if this ever " +
                "falls below a tenth of a metre the override stops being load-bearing and should be " +
                "reconsidered rather than kept out of habit");
        }

        [Test]
        public void SABOTAGE_BuildRectsWithNoContractEntryCannotBeFaked()
        {
            // A zero cell is what an unparsed / hand-emptied contract looks like. BuildRects must not
            // invent a grid from it.
            var empty = new VillageBuildingKit.Entry { key = "saboteur", cellW = 0, cellH = 0,
                                                       cols = 0, rows = 0, facings = 8 };
            SpriteRect[] rects = VillageBuildingSheetSlicer.BuildRects(empty);
            Assert.AreEqual(0, rects.Length,
                            "a contract with no cell must produce no rects, not a degenerate grid");
        }

        // ---- Configure: the one place a building is built ---------------------------------------

        [Test]
        public void ConfigureBuildsTheCanonicalBuilding()
        {
            RequireSheetsOnDisk();
            var placement = VillageBuildingCatalog.Find(_contract.buildings[0].key);
            Assert.IsTrue(placement.IsValid);

            var sprite = VillageBuildingCatalog.LoadFacing(placement, placement.FrontFacing);
            Assert.IsNotNull(sprite, "the front facing's sprite should be sliced");

            var go = new GameObject("village-building-test");
            try
            {
                var sr = VillageBuildingCatalog.Configure(go, placement, sprite);

                Assert.AreSame(sprite, sr.sprite);
                Assert.IsNotNull(go.GetComponent<YSortSprite>(),
                                 "a building layers around the player by world Y");

                // ⚠️ NOT `sortingOrder == SortingOrder`. YSortSprite OWNS the final value and recomputes
                // it from world Y the moment it is enabled, so the seeded 4 is already gone by the time
                // Configure returns (it comes back SortingBands.DecorBase, the band's value at Y = 0).
                // What is worth pinning is that the result lands inside the decor band — above the wharf
                // deck's −4..1 so a building on a wharf never sinks into the planking, and below the
                // ropes and rain. ⚠ Asks SortingBands rather than restating its numbers: this assertion
                // read `InRange(2, 40)` until ADR 0032 re-based the band, and a hard-coded range is
                // exactly how a guard outlives the thing it guards.
                Assert.That(sr.sortingOrder, Is.InRange(SortingBands.DecorFloor, SortingBands.DecorCeiling),
                            "the Y-sorted order must stay inside the world-decor band");

                // Honest metric size — never scale a real sprite (the charter's first guardrail).
                Assert.AreEqual(Vector3.one, go.transform.localScale);

                // Baked at a fixed camera: rotating the transform would tip the projection.
                Assert.AreEqual(Quaternion.identity, go.transform.localRotation);

                // No ReflectiveObject: the default sprite material carries no HHReflect pass, so one
                // would join the reflective set and draw nothing (the same reason DecorPrefabBuilder
                // withholds it from its buildings).
                Assert.IsNull(go.GetComponent<ReflectiveObject>(),
                              "a building's material has no reflect pass to join");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void ABuildingLowerOnScreenDrawsInFrontOfOneFurtherUp()
        {
            // What the YSortSprite is actually FOR, and the reason Configure adds it rather than leaving
            // a fixed order: a building nearer the camera (smaller world Y) must draw in front of one
            // behind it, so a village reads as depth instead of as a flat row.
            RequireSheetsOnDisk();
            var placement = VillageBuildingCatalog.Find(_contract.buildings[0].key);
            var sprite = VillageBuildingCatalog.LoadFacing(placement, placement.FrontFacing);

            var near = new GameObject("village-near");
            var far = new GameObject("village-far");
            try
            {
                near.transform.position = new Vector3(0f, -3f, 0f);
                far.transform.position = new Vector3(0f, 3f, 0f);

                var nearSr = VillageBuildingCatalog.Configure(near, placement, sprite);
                var farSr = VillageBuildingCatalog.Configure(far, placement, sprite);

                Assert.Greater(nearSr.sortingOrder, farSr.sortingOrder,
                    "the nearer building (smaller world Y) must draw in front of the further one");
            }
            finally { Object.DestroyImmediate(near); Object.DestroyImmediate(far); }
        }

        [Test]
        public void ConfigureIsIdempotent_SoRebuildingAPrefabDoesNotStackComponents()
        {
            RequireSheetsOnDisk();
            var placement = VillageBuildingCatalog.Find(_contract.buildings[0].key);
            var sprite = VillageBuildingCatalog.LoadFacing(placement, 0);

            var go = new GameObject("village-building-idempotent");
            try
            {
                VillageBuildingCatalog.Configure(go, placement, sprite);
                VillageBuildingCatalog.Configure(go, placement, sprite);

                Assert.AreEqual(1, go.GetComponents<SpriteRenderer>().Length);
                Assert.AreEqual(1, go.GetComponents<YSortSprite>().Length);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void ConfigureRefusesAPlacementWithNoContractEntry()
        {
            var go = new GameObject("village-building-invalid");
            try
            {
                Assert.Throws<System.ArgumentException>(
                    () => VillageBuildingCatalog.Configure(go, default, null),
                    "placing on a plausible default pivot is exactly the reading this contract replaces");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void SetFacingRoundTrips_AndTurningNeverRotatesTheTransform()
        {
            RequireSheetsOnDisk();
            var placement = VillageBuildingCatalog.Find(_contract.buildings[0].key);
            var sprite = VillageBuildingCatalog.LoadFacing(placement, 0);

            var go = new GameObject("village-building-facing");
            try
            {
                VillageBuildingCatalog.Configure(go, placement, sprite);
                Assert.AreEqual(0, VillageBuildingCatalog.FacingOf(go, placement));

                for (int f = 0; f < placement.Entry.facings; f++)
                {
                    Assert.IsTrue(VillageBuildingCatalog.SetFacing(go, placement, f), $"facing {f}");
                    Assert.AreEqual(f, VillageBuildingCatalog.FacingOf(go, placement));
                    Assert.AreEqual(Quaternion.identity, go.transform.localRotation,
                                    "turning a building swaps the sprite; it never rotates the transform");
                }

                Assert.IsFalse(VillageBuildingCatalog.SetFacing(go, placement, placement.Entry.facings),
                               "a facing past the baked count must fail rather than blank the sprite");
                Assert.IsFalse(VillageBuildingCatalog.SetFacing(go, placement, -1));
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void TheFrontFacingActuallyHasASprite()
        {
            RequireSheetsOnDisk();
            foreach (var placement in VillageBuildingCatalog.Scan())
            {
                Assert.That(placement.FrontFacing,
                            Is.InRange(0, placement.Entry.facings - 1),
                            $"{placement.Key}: the derived front facing must be a baked one");
                Assert.IsNotNull(VillageBuildingCatalog.LoadFacing(placement, placement.FrontFacing),
                                 $"{placement.Key}: the front facing has no sprite, so its prefab would " +
                                 "be built blank");
            }
        }

        [Test]
        public void ScanReturnsEveryBuildingInTheKitsOwnOrder()
        {
            var scanned = VillageBuildingCatalog.Scan().Select(p => p.Key).ToArray();
            var declared = _contract.buildings.Select(b => b.key).ToArray();
            CollectionAssert.AreEqual(declared, scanned,
                "Scan preserves the contract's order, which is the kit's declared order — the order a " +
                "village reads in, not alphabetical");
        }

        [Test]
        public void TheKitsTextureBudgetIsReported_NotPresumed()
        {
            // Not a threshold — a number, printed where the next reader will find it. 8 facings per
            // building is the ADR-0006 recipe; whether M1 should pay for all eight is the owner's call,
            // and this is the number that call needs.
            float pngMib = VillageBuildingKit.TotalPngMib(_contract);
            float runtimeMib = VillageBuildingKit.TotalRuntimeMib(_contract);

            Assert.Greater(pngMib, 0f);
            Assert.Greater(runtimeMib, pngMib, "RGBA32 in memory always costs more than the PNG on disk");

            Debug.Log($"[VillageBuildingImportTests] kit budget: {_contract.buildings.Length} building(s) " +
                      $"× {_contract.facings} facings = {pngMib:F2} MiB of committed PNG, " +
                      $"{runtimeMib:F1} MiB of RGBA32 at runtime. Per building: " +
                      string.Join(", ", _contract.buildings.Select(
                          b => $"{b.key} {b.runtimeBytesRgba32 / 1024f / 1024f:F1} MiB")) + ".");
        }

        // ---- helpers ---------------------------------------------------------------------------

        void RequireSheetsOnDisk()
        {
            foreach (string path in VillageBuildingKit.AllSheetPaths(_contract))
                if (!File.Exists(path))
                    Assert.Ignore($"{path} is not on disk — run Hidden Harbours ▸ Art ▸ Bake Village " +
                                  "Buildings, then ▸ Import (after a new drop) ▸ Slice Village " +
                                  "Building Sheets.");
        }

        static VillageBuildingKit.Entry Clone(VillageBuildingKit.Entry src) =>
            JsonUtility.FromJson<VillageBuildingKit.Entry>(JsonUtility.ToJson(src));
    }
}
