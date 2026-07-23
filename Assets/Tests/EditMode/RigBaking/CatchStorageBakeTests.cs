using System.Globalization;
using NUnit.Framework;
using HiddenHarbours.Art;
using HiddenHarbours.Art.Editor;
using HiddenHarbours.Tools.RigBaking;
using UnityEngine;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// The catch-STORAGE bake path, proven end-to-end WITHOUT any committed sheet — V8 host
    /// CPU-side only, CI-safe under the null graphics device like every sibling suite. The 59 kit
    /// sheets are baked on the owner's machine (Hidden Harbours ▸ Art ▸ Bake Catch Storage Kit);
    /// these tests make that bake trustworthy before it runs, and
    /// <c>CatchStorageSheetSliceTests</c> takes over the moment the PNGs land.
    ///
    /// The load-bearing pins:
    /// <list type="bullet">
    /// <item><b>Runtime twins == the rig.</b> <c>CatchFillMath</c> replays <c>fillItems</c> and
    /// <c>CatchSpoilMath</c> replays <c>tintSpoil</c> against the LIVE rig, element- and
    /// byte-exact — the fill a player watches grow is the art director's recipe, not a paraphrase
    /// of it.</item>
    /// <item><b>Slicer manifest == the rigs' geometry</b> (the drift alarm the fishing kit
    /// carries in <c>SlicerKitSpecs_MatchTheLiveRigs</c>).</item>
    /// <item><b>Conventions are measured</b> and agree with the catalog declarations, so the
    /// owner's bake will not refuse.</item>
    /// </list>
    /// </summary>
    public class CatchStorageBakeTests
    {
        private static string BandJs(CatchFillBand band) => band switch
        {
            CatchFillBand.Few => "few", CatchFillBand.Half => "half",
            CatchFillBand.Full => "full", CatchFillBand.Brim => "brim", _ => "empty",
        };

        /// <summary>One host with the full CatchKit stack: dependencies, the canvas shim, then
        /// the glue — the exact install order the baker uses.</summary>
        private static IRigScriptHost CreateCatchKitHost()
        {
            var host = RigScriptHostFactory.Create();
            RigCatalog.Install(host, RigCatalog.Get("crustacean"));
            RigCatalog.InstallModule(host, RigCatalog.Get("shellfish"));
            host.Execute(CatchStorageBaker.CanvasShimJs);
            RigCatalog.InstallModule(host, RigCatalog.Get("catchKit"));
            return host;
        }

        // ---- installs (geometry-free module shapes are legitimate, not errors) ------------------

        [Test]
        public void CrustaceanRig_Installs_WithGroundCentrePivot()
        {
            using var host = RigScriptHostFactory.Create();
            var geo = RigCatalog.Install(host, RigCatalog.Get("crustacean"));
            Assert.AreEqual(64, geo.Width);
            Assert.AreEqual(64, geo.Height);
            Assert.AreEqual(32.0, geo.PivotX, 1e-9);
            Assert.AreEqual(36.0, geo.PivotY, 1e-9, "pivot = ground centre, (32,36) top-left");
            Assert.AreEqual(0, geo.NativeDirs, "continuous ang, no DIRS — Install reports 0, not a throw");
        }

        [Test]
        public void ShellfishAndCatchKit_InstallAsModules_AndExposeTheirShapes()
        {
            using var host = CreateCatchKitHost();
            Assert.AreEqual(14, (int)host.EvaluateNumber("Shellfish.IW"));
            Assert.AreEqual(12, (int)host.EvaluateNumber("Shellfish.IH"));
            Assert.AreEqual(7, (int)host.EvaluateNumber("Shellfish.ipivot.x"));
            Assert.AreEqual(10, (int)host.EvaluateNumber("Shellfish.ipivot.y"));
            Assert.AreEqual(CatchFillMath.Variants, (int)host.EvaluateNumber("Shellfish.VARIANTS"),
                "the runtime fill math draws 4 variants — the rig must agree");
            Assert.IsTrue(host.EvaluateBool("typeof CatchKit.fillItems === 'function'"));
            Assert.IsTrue(host.EvaluateBool("typeof CatchKit.item === 'function'"));
            Assert.IsTrue(host.EvaluateBool("typeof CatchKit.tintSpoil === 'function'"));
        }

        [Test]
        public void FishToteRig_Installs_WithGroundCentrePivot_AndTheStorageSlicerAgrees()
        {
            using var host = RigScriptHostFactory.Create();
            var geo = RigCatalog.Install(host, RigCatalog.Get("fishTote"));
            Assert.AreEqual(64, geo.Width);
            Assert.AreEqual(72, geo.Height);
            Assert.AreEqual(32.0, geo.PivotX, 1e-9);
            Assert.AreEqual(60.0, geo.PivotY, 1e-9, "pivot = ground under the centre, (32,60) top-left");

            foreach (string prefix in new[] { "Tote_", "ToteMask" })
            {
                var kit = CatchStorageSheetSlicer.Kits[prefix];
                Assert.AreEqual(geo.Width, kit.Cell.x, $"{prefix}: slicer cell width drifted from the rig");
                Assert.AreEqual(geo.Height, kit.Cell.y, $"{prefix}: slicer cell height drifted from the rig");
                Assert.AreEqual(geo.PivotX, (double)kit.PivotTopLeftPx.x, 1e-9, $"{prefix}: slicer pivot.x drifted");
                Assert.AreEqual(geo.PivotY, (double)kit.PivotTopLeftPx.y, 1e-9, $"{prefix}: slicer pivot.y drifted");
            }
        }

        [Test]
        public void BucketRig_InstallsAsModule_AndTheStorageSlicerMatchesItsRestPivot()
        {
            using var host = RigScriptHostFactory.Create();
            RigCatalog.InstallModule(host, RigCatalog.Get("bucket"));
            var kit = CatchStorageSheetSlicer.Kits["Bucket_"];
            Assert.AreEqual((int)host.EvaluateNumber("BucketIso.W"), kit.Cell.x);
            Assert.AreEqual((int)host.EvaluateNumber("BucketIso.H"), kit.Cell.y);
            Assert.AreEqual(host.EvaluateNumber("BucketIso.pivotRest.x"), (double)kit.PivotTopLeftPx.x, 1e-9,
                "the storage bake is REST mode — the slicer must carry pivotRest, never pivotCarry");
            Assert.AreEqual(host.EvaluateNumber("BucketIso.pivotRest.y"), (double)kit.PivotTopLeftPx.y, 1e-9);
        }

        [Test]
        public void TheGuardedAxes_AreTheLiveRigsOwnTables()
        {
            // The slice test's stem axes (colours, lids, tiers, fills, catches) restate the drop's
            // contract; this pins them to the rigs so a rig-side rename is loud before any bake.
            using var host = RigScriptHostFactory.Create();
            RigCatalog.Install(host, RigCatalog.Get("fishTote"));
            RigCatalog.InstallModule(host, RigCatalog.Get("bucket"));

            Assert.AreEqual("navy,steel,plast,rust,teal",
                            host.EvaluateString("FishTote.CORDER.join(',')"));
            Assert.AreEqual("on,off,lean", host.EvaluateString("FishTote.LIDS.join(',')"));
            Assert.AreEqual("pail,tote,tray",
                            host.EvaluateString("[1,2,3].map(function(t){return BucketIso.TIERS[t].id;}).join(',')"));
            Assert.AreEqual("empty,few,half,full,brim",
                            host.EvaluateString("BucketIso.FILLS.join(',')"));
            Assert.AreEqual("fish,shell,crust", host.EvaluateString("BucketIso.CATCHES.join(',')"));
        }

        // ---- the canvas shim: CatchKit.item composes through host-side code ---------------------

        [Test]
        public void CanvasShim_LetsCatchKitItem_ComposeEveryBakedKind()
        {
            using var host = CreateCatchKitHost();
            foreach (string kind in CatchStorageBaker.BakedItemKinds)
            {
                var geo = CatchStorageBaker.ItemGeometry(host, kind);
                for (int v = 0; v < CatchFillMath.Variants; v++)
                {
                    byte[] rgba = host.EvaluateBytes(CatchStorageBaker.ItemPixelsExpr(kind, v));
                    Assert.AreEqual(geo.Width * geo.Height * 4, rgba.Length,
                                    $"{kind} v{v}: item pixels are not one {geo.Width}×{geo.Height} cell");

                    int opaque = 0;
                    for (int i = 3; i < rgba.Length; i += 4)
                        if (rgba[i] >= 128) opaque++;
                    Assert.Greater(opaque, 20, $"{kind} v{v}: the composed item is (nearly) blank");
                }
            }
        }

        [Test]
        public void ItemGeometry_MatchesTheStorageSlicerManifest()
        {
            using var host = CreateCatchKitHost();
            foreach (string kind in CatchStorageBaker.BakedItemKinds)
            {
                var geo = CatchStorageBaker.ItemGeometry(host, kind);
                var kit = CatchStorageSheetSlicer.Kits[$"CatchItem_{kind}"];
                Assert.AreEqual(geo.Width, kit.Cell.x, $"{kind}: slicer cell width drifted from item()");
                Assert.AreEqual(geo.Height, kit.Cell.y, $"{kind}: slicer cell height drifted from item()");
                Assert.AreEqual(geo.PivotX, (double)kit.PivotTopLeftPx.x, 1e-9,
                                $"{kind}: slicer pivot.x drifted from item().ax");
                Assert.AreEqual(geo.PivotY, (double)kit.PivotTopLeftPx.y, 1e-9,
                                $"{kind}: slicer pivot.y drifted from item().ay");
            }
        }

        // ---- runtime twins == the live rig ------------------------------------------------------

        [Test]
        public void CatchFillMath_ReplaysTheRigsFillItems_Exactly()
        {
            using var host = CreateCatchKitHost();
            // FishIso is absent on purpose: fillItems never touches the item rigs, and proving
            // that here keeps the fill math's dependency surface honest.
            var bands = new[] { CatchFillBand.Few, CatchFillBand.Half, CatchFillBand.Full, CatchFillBand.Brim };
            foreach (string kind in new[] { "cod", "mackerel", "lobster", "mussel", "mixed" })
            foreach (int seed in new[] { 1, 7, 42, 20260723 })
            foreach (var band in bands)
            foreach (int capacity in new[] { -1, 32 })
            {
                string capJs = capacity < 0 ? "undefined" : capacity.ToString(CultureInfo.InvariantCulture);
                string call = $"CatchKit.fillItems('{kind}','{BandJs(band)}',{seed},{capJs})";

                var ours = CatchFillMath.FillItems(kind, band, seed, capacity);
                int rigCount = (int)host.EvaluateNumber($"{call}.length");
                Assert.AreEqual(rigCount, ours.Count, $"{call}: count");

                // Spot-exact per element (kind + variant + scale) — the recipe, not a resemblance.
                for (int i = 0; i < ours.Count; i++)
                {
                    Assert.AreEqual(host.EvaluateString($"{call}[{i}].kind"), ours[i].Kind,
                                    $"{call}[{i}].kind");
                    Assert.AreEqual((int)host.EvaluateNumber($"{call}[{i}].variant"), ours[i].Variant,
                                    $"{call}[{i}].variant");
                    Assert.AreEqual(host.EvaluateNumber($"{call}[{i}].scale"), ours[i].Scale, 1e-6,
                                    $"{call}[{i}].scale");
                }
            }
        }

        [Test]
        public void TheRigItself_IsMonotonic_SoTheContractIsTheArtDirectors()
        {
            // The monotonic guarantee is the RIG's design (seeded stream, index-stable); pin it
            // rig-side too, so an upstream drop that broke it is caught before any port drifts.
            using var host = CreateCatchKitHost();
            Assert.IsTrue(host.EvaluateBool(
                "(function(){" +
                "  var few = CatchKit.fillItems('mixed','few',42,32);" +
                "  var brim = CatchKit.fillItems('mixed','brim',42,32);" +
                "  for (var i = 0; i < few.length; i++){" +
                "    if (few[i].kind !== brim[i].kind) return false;" +
                "    if (few[i].variant !== brim[i].variant) return false;" +
                "    if (few[i].scale !== brim[i].scale) return false;" +
                "  }" +
                "  return few.length <= brim.length;" +
                "})()"),
                "catchKit.fillItems stopped being monotonic — flag upstream, do not paper over");
        }

        [Test]
        public void CatchSpoilMath_ReplaysTheRigsTintSpoil_ByteExact()
        {
            using var host = CreateCatchKitHost();
            foreach (string kind in new[] { "mussel", "clam" })
            foreach (double spoil in new[] { 0.25, 0.5, 1.0 })
            {
                string spoilJs = spoil.ToString("R", CultureInfo.InvariantCulture);
                byte[] fresh = host.EvaluateBytes($"Shellfish.renderItem('{kind}',1)");
                byte[] rigTinted = host.EvaluateBytes(
                    $"CatchKit.tintSpoil(Shellfish.renderItem('{kind}',1),Shellfish.IW,Shellfish.IH,{spoilJs})");
                byte[] ourTinted = CatchSpoilMath.Tint(fresh, 14, 12, spoil);
                CollectionAssert.AreEqual(rigTinted, ourTinted,
                    $"tintSpoil({kind}, {spoil}): the C# rot recipe drifted from the rig");
            }
        }

        // ---- the tote's fill geometry -----------------------------------------------------------

        [Test]
        public void ToteSlots_AreFourLayersOfEight_BackToFrontWithinEachLayer()
        {
            using var host = RigScriptHostFactory.Create();
            RigCatalog.Install(host, RigCatalog.Get("fishTote"));
            for (int dir = 0; dir < 8; dir++)
            {
                Assert.AreEqual(32, (int)host.EvaluateNumber($"FishTote.slots({dir}).length"),
                                $"dir {dir}: expected 4 layers × 8 slots");
                Assert.AreEqual(4, (int)host.EvaluateNumber($"FishTote.opening({dir}).length"),
                                $"dir {dir}: the opening must be a quad");
                // The rig pre-sorts each 8-slot layer by screen y — drawing in index order must
                // paint back-to-front, which is what lets the renderer use index = sort order.
                Assert.IsTrue(host.EvaluateBool(
                    "(function(){" +
                    $"  var p = FishTote.slots({dir});" +
                    "  for (var layer = 0; layer < 4; layer++)" +
                    "    for (var i = 1; i < 8; i++)" +
                    "      if (p[layer*8+i].sy < p[layer*8+i-1].sy) return false;" +
                    "  return true;" +
                    "})()"),
                    $"dir {dir}: slots are no longer back-to-front within each layer");
            }
        }

        [Test]
        public void ToteAnchors_RoundTripThroughTheRuntimeSchema()
        {
            using var host = RigScriptHostFactory.Create();
            var entry = RigCatalog.Get("fishTote");
            var geo = RigCatalog.Install(host, entry);

            CatchStorageAnchors built = CatchStorageBaker.BuildToteAnchors(
                host, entry, geo, AzimuthConvention.Clockwise);
            string json = JsonUtility.ToJson(built, prettyPrint: true);
            CatchStorageAnchors parsed = CatchStorageAnchors.Parse(json);

            Assert.IsNotNull(parsed?.tote, "the anchors did not survive the JSON round trip");
            Assert.AreEqual(geo.Width, parsed.tote.cellW);
            Assert.AreEqual(geo.Height, parsed.tote.cellH);
            Assert.AreEqual(8, parsed.tote.byDir.Length);
            Assert.AreEqual(32, parsed.tote.Capacity);
            for (int d = 0; d < 8; d++)
            {
                Assert.AreEqual(built.tote.byDir[d].slots.Length, parsed.tote.byDir[d].slots.Length);
                for (int i = 0; i < built.tote.byDir[d].slots.Length; i++)
                {
                    Assert.AreEqual(built.tote.byDir[d].slots[i].dx, parsed.tote.byDir[d].slots[i].dx);
                    Assert.AreEqual(built.tote.byDir[d].slots[i].dy, parsed.tote.byDir[d].slots[i].dy);
                }
                Assert.AreEqual(4, parsed.tote.byDir[d].opening.Length);
            }
        }

        [Test]
        public void ToteMaskCells_FillTheOpening_AndOnlyTheOpening()
        {
            using var host = RigScriptHostFactory.Create();
            var entry = RigCatalog.Get("fishTote");
            var geo = RigCatalog.Install(host, entry);
            CatchStorageAnchors anchors = CatchStorageBaker.BuildToteAnchors(
                host, entry, geo, AzimuthConvention.Clockwise);

            for (int d = 0; d < 8; d++)
            {
                byte[] cell = CatchStorageBaker.MaskCell(anchors.tote, d);
                Assert.AreEqual(geo.Width * geo.Height * 4, cell.Length);

                // Bounding box of the opening quad, in absolute cell px.
                double minX = double.MaxValue, maxX = double.MinValue;
                double minY = double.MaxValue, maxY = double.MinValue;
                foreach (var p in anchors.tote.byDir[d].opening)
                {
                    double x = anchors.tote.pivotX + p.dx, y = anchors.tote.pivotY + p.dy;
                    if (x < minX) minX = x; if (x > maxX) maxX = x;
                    if (y < minY) minY = y; if (y > maxY) maxY = y;
                }

                int opaque = 0;
                for (int y = 0; y < geo.Height; y++)
                for (int x = 0; x < geo.Width; x++)
                {
                    if (cell[(y * geo.Width + x) * 4 + 3] == 0) continue;
                    opaque++;
                    Assert.IsTrue(x + 0.5 >= minX && x + 0.5 <= maxX && y + 0.5 >= minY && y + 0.5 <= maxY,
                                  $"dir {d}: mask pixel ({x},{y}) escapes the opening's bounds");
                }
                Assert.Greater(opaque, 200, $"dir {d}: the opening mask is (nearly) empty — " +
                                            "the rim quad should cover most of a ~27 px square");
            }
        }

        // ---- measured conventions ---------------------------------------------------------------

        [Test]
        public void ToteConvention_MeasuresClockwise_AsTheCatalogDeclares()
        {
            using var host = RigScriptHostFactory.Create();
            var entry = RigCatalog.Get("fishTote");
            var geo = RigCatalog.Install(host, entry);
            var probe = StorageRigAzimuthProbe.MeasureTote(host, entry.GlobalName, geo, 8);
            Assert.AreEqual(entry.DeclaredConvention, probe.Convention,
                "the tote's measured convention disagrees with the catalog — correct the catalog " +
                "(and the README group) from the pixels, never the other way:\n" + probe.Report);
        }

        [Test]
        public void BucketConvention_MeasuresCounterClockwise_AsTheCatalogDeclares()
        {
            using var host = RigScriptHostFactory.Create();
            var entry = RigCatalog.Get("bucket");
            RigCatalog.InstallModule(host, entry);
            var geo = new RigGeometry(
                (int)host.EvaluateNumber("BucketIso.W"), (int)host.EvaluateNumber("BucketIso.H"),
                host.EvaluateNumber("BucketIso.pivotRest.x"), host.EvaluateNumber("BucketIso.pivotRest.y"),
                nativeDirs: 8, rockFrames: 0, defaultElevation: 40);
            var probe = StorageRigAzimuthProbe.MeasureBucket(host, entry.GlobalName, geo, 8);
            Assert.AreEqual(entry.DeclaredConvention, probe.Convention,
                "the bucket's measured convention disagrees with the catalog — correct the " +
                "catalog (and the README group) from the pixels, never the other way:\n" + probe.Report);
        }
    }
}
