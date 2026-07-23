using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using HiddenHarbours.Art;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>
    /// Bakes the fishing kit's STORAGE wave — the sprites that let containers visibly fill with
    /// the player's actual catch (the kit's whole diegetic point: no icons):
    ///
    /// <list type="bullet">
    /// <item><b>Catch items</b> — <c>CatchItem_&lt;kind&gt;.png</c> (lobster / crab / mussel /
    /// clam), one row × 4 lay variants, composed by the rig's own glue (<c>CatchKit.item</c>) so
    /// the exact per-variant pose/angle recipe stays in the art director's file. Fish items are
    /// NOT re-baked: <c>CatchKit.item</c> composes them from FishIso's dry <c>deck</c> lays, which
    /// #265 already shipped (variant v = deck row [2,6,3,5][v], frame v — the mapping lives in
    /// <c>CatchItemLibrary</c> wiring, not in new pixels).</item>
    /// <item><b>The tote</b> — <c>Tote_&lt;colour&gt;_&lt;lid&gt;.png</c> (5 colours × on/off/lean,
    /// 8 direction rows), the genuinely hollow shell the items stack inside, plus
    /// <c>ToteMask.png</c> (8 rows), a white fill of the projected rim opening per direction —
    /// the runtime <c>SpriteMask</c> that lets the front wall occlude the low layers.</item>
    /// <item><b>The buckets</b> — <c>Bucket_&lt;tier&gt;_&lt;fill&gt;[_&lt;catch&gt;].png</c>
    /// (pail/tote/tray × empty + 4 fills × 3 catches, 8 rows, REST mode). BucketIso bakes its
    /// fills INTO the render (the rig README's own note: "abstract fills — retrofit onto CatchKit
    /// planned"), so these are swap-states for the existing <c>DeckContainerDef.FillSprites</c>
    /// path, not slot-drawn fills. The empty state renders identically for every catch, so it
    /// bakes once per tier.</item>
    /// <item><b>The anchors</b> — <c>CatchStorageAnchors.json</c>, serialized from the SAME
    /// <see cref="CatchStorageAnchors"/> class the runtime parses (no schema drift possible):
    /// the tote's 4-layer × 8-point slot stack and rim opening per direction row, exported with
    /// the measured convention applied exactly like the fish's <c>mouth()</c> export.</item>
    /// </list>
    ///
    /// <para><b>Conventions are MEASURED, never trusted:</b> the tote and bucket are directional,
    /// so <see cref="StorageRigAzimuthProbe"/> reads each from rendered pixels and the bake
    /// REFUSES on a catalog mismatch — the same rule as every sibling baker.</para>
    ///
    /// <para><b>The canvas shim:</b> <c>CatchKit.item</c> wraps its pixels in
    /// <c>document.createElement('canvas')</c>. Bare V8 has no DOM, and the rig must run
    /// UNMODIFIED (ADR 0021 §5), so <see cref="CanvasShimJs"/> — host-side code, executed before
    /// the rig — provides a capture-only canvas/ImageData pair; the baker then reads the raw
    /// <c>Uint8ClampedArray</c> straight back out of it. Nothing is rendered through the shim; it
    /// is a mailbox.</para>
    ///
    /// Like its siblings this stops at "PNG (+ anchors JSON) on disk": slicing is
    /// <c>CatchStorageSheetSlicer</c>'s job and import settings are <c>ArtImportPipeline</c>'s.
    /// </summary>
    public static class CatchStorageBaker
    {
        /// <summary>The ADR-0006 recipe's facing count (neither container rig declares DIRS).</summary>
        public const int Dirs = 8;

        /// <summary>Sibling of the fight kit's Iso folder, NOT inside it: <c>FishingSheetSlicer</c>
        /// slices (and its slice tests close over) every png under Iso/ — landing new stems there
        /// would fail its closed-set guard. A sibling folder keeps both guarded sets honest.</summary>
        public const string DefaultOutputFolder = "Assets/_Project/Art/Fishing/Storage";

        public const string AnchorsFileName = "CatchStorageAnchors.json";

        /// <summary>The item kinds this bake OWNS. Fish kinds are deliberately absent — see the
        /// class remarks (#265's deck lays are the fish item art).</summary>
        public static readonly string[] BakedItemKinds = { "lobster", "crab", "mussel", "clam" };

        /// <summary>
        /// Host-side environment shim for <c>catchKit.js</c> (see class remarks). Executed BEFORE
        /// the rig source; provides exactly what the rig touches — <c>document.createElement
        /// ('canvas')</c> and <c>ImageData</c> — as a capture mailbox (<c>putImageData</c> stores
        /// the ImageData on the canvas as <c>__img</c>; no drawing ever happens). Defined only when
        /// absent, throws on any non-canvas element so a future rig change is loud, and never edits
        /// the rig file itself.
        /// </summary>
        public const string CanvasShimJs = @"
(function (g) {
  if (typeof g.ImageData === 'undefined') {
    g.ImageData = function ImageData(data, w, h) { this.data = data; this.width = w; this.height = h; };
  }
  if (typeof g.document === 'undefined') {
    g.document = {
      createElement: function (tag) {
        if (String(tag).toLowerCase() !== 'canvas')
          throw new Error('host canvas shim provides only <canvas>, got <' + tag + '>');
        var cv = { width: 0, height: 0, __img: null };
        cv.getContext = function () {
          return { putImageData: function (img) { cv.__img = img; } };
        };
        return cv;
      }
    };
  }
})(globalThis);
";

        // =====================================================================================
        // CATCH ITEMS — CatchItem_<kind>.png, 1 row × 4 lay variants, via CatchKit.item
        // =====================================================================================

        public static FishingBakeResult BakeCatchItems(string outputFolder = DefaultOutputFolder,
                                                       Action<string, float> progress = null)
        {
            var total = Stopwatch.StartNew();
            using IRigScriptHost host = RigScriptHostFactory.Create();

            // CatchKit composes items from the catch rigs it names; install its dependencies
            // first, then the shim, then the glue itself.
            var crustGeo = RigCatalog.Install(host, RigCatalog.Get("crustacean"));
            RigCatalog.InstallModule(host, RigCatalog.Get("shellfish"));
            host.Execute(CanvasShimJs);
            RigCatalog.InstallModule(host, RigCatalog.Get("catchKit"));

            var result = new FishingBakeResult
            {
                RigKey = "catchItems", EngineName = host.EngineName, Geometry = crustGeo,
                MeasuredConvention = null,
                ConventionReport = "items are laid at CatchKit's own scattered angles — no " +
                                   "turntable, nothing to probe",
            };

            // Recipe cross-checks before writing anything: the runtime fill math draws variants
            // 0..3 (CatchFillMath.Variants, the rig's own literal); the item rigs must agree.
            AssertEqual("Shellfish.VARIANTS", host.EvaluateNumber("Shellfish.VARIANTS"),
                        CatchFillMath.Variants);
            AssertEqual("Crustacean.POSES.walk.n", host.EvaluateNumber("Crustacean.POSES.walk.n"),
                        CatchFillMath.Variants);

            var renderClock = new Stopwatch();
            System.IO.Directory.CreateDirectory(
                System.IO.Path.Combine(RigCatalog.RepoRoot, outputFolder));

            for (int k = 0; k < BakedItemKinds.Length; k++)
            {
                string kind = BakedItemKinds[k];
                progress?.Invoke($"CatchItem_{kind}", (float)k / BakedItemKinds.Length);

                RigGeometry geo = ItemGeometry(host, kind);
                result.Sheets.Add(FishingKitBaker.WriteSheet(outputFolder, $"CatchItem_{kind}",
                    rows: 1, frames: CatchFillMath.Variants, geo,
                    (d, f) => FishingKitBaker.Render(host, ItemPixelsExpr(kind, f), geo,
                                                     renderClock, result),
                    result));
            }

            result.RenderMilliseconds = renderClock.Elapsed.TotalMilliseconds;
            total.Stop();
            result.TotalMilliseconds = total.Elapsed.TotalMilliseconds;
            return result;
        }

        /// <summary>One item cell's geometry, read from <c>CatchKit.item</c>'s own report
        /// (w/h + the ground-contact anchor ax/ay — the slice pivot).</summary>
        public static RigGeometry ItemGeometry(IRigScriptHost host, string kind)
        {
            string item = $"CatchKit.item({FishingKitBaker.Js(kind)},{{variant:0}})";
            return new RigGeometry(
                width: (int)host.EvaluateNumber($"{item}.w"),
                height: (int)host.EvaluateNumber($"{item}.h"),
                pivotX: host.EvaluateNumber($"{item}.ax"),
                pivotY: host.EvaluateNumber($"{item}.ay"),
                nativeDirs: 0, rockFrames: 0, defaultElevation: 0);
        }

        /// <summary>The raw pixels of one composed item, read back out of the shim's canvas
        /// mailbox.</summary>
        public static string ItemPixelsExpr(string kind, int variant) =>
            $"CatchKit.item({FishingKitBaker.Js(kind)},{{variant:{variant}}}).canvas.__img.data";

        // =====================================================================================
        // TOTE — Tote_<colour>_<lid>.png + ToteMask.png (8 rows × 1) + the anchors JSON
        // =====================================================================================

        public static FishingBakeResult BakeTote(string outputFolder = DefaultOutputFolder,
                                                 Action<string, float> progress = null)
        {
            var total = Stopwatch.StartNew();
            var entry = RigCatalog.Get("fishTote");

            using IRigScriptHost host = RigScriptHostFactory.Create();
            var geo = RigCatalog.Install(host, entry);
            string g = entry.GlobalName;

            var result = new FishingBakeResult
            {
                RigKey = "fishTote", EngineName = host.EngineName, Geometry = geo,
            };

            // ---- MEASURE the convention from pixels, then cross-check the declaration --------
            var probe = StorageRigAzimuthProbe.MeasureTote(host, g, geo, Dirs);
            result.MeasuredConvention = probe.Convention;
            result.ConventionReport = probe.Report;
            FishingKitBaker.RefuseOnMismatch("fishTote", entry.DeclaredConvention,
                                             probe.Convention, probe.Report);

            var colours = FishingKitBaker.ReadStringArray(host, $"{g}.CORDER");
            var lids = FishingKitBaker.ReadStringArray(host, $"{g}.LIDS");

            var renderClock = new Stopwatch();
            System.IO.Directory.CreateDirectory(
                System.IO.Path.Combine(RigCatalog.RepoRoot, outputFolder));

            int done = 0, plan = colours.Count * lids.Count + 1;
            foreach (var colour in colours)
            foreach (var lid in lids)
            {
                progress?.Invoke($"Tote_{colour}_{lid}", (float)done++ / plan);
                result.Sheets.Add(FishingKitBaker.WriteSheet(outputFolder,
                    $"Tote_{colour}_{lid}", rows: Dirs, frames: 1, geo,
                    (d, f) =>
                    {
                        double dir = RigBaker.DirForCell(d, Dirs, probe.Convention);
                        return FishingKitBaker.Render(host,
                            $"{g}.render({Num(dir)},{{colour:{FishingKitBaker.Js(colour)}," +
                            $"lid:{FishingKitBaker.Js(lid)}}})",
                            geo, renderClock, result);
                    }, result));
            }

            // ---- the anchors (rig-derived data, convention applied like the fish's mouth) ----
            CatchStorageAnchors anchors = BuildToteAnchors(host, entry, geo, probe.Convention);

            // ---- the opening mask: white fill of the projected rim quad, per direction row ----
            progress?.Invoke("ToteMask", (float)done / plan);
            result.Sheets.Add(FishingKitBaker.WriteSheet(outputFolder, "ToteMask",
                rows: Dirs, frames: 1, geo,
                (d, f) => MaskCell(anchors.tote, d), result));

            result.AnchorJsonPath = FishingKitBaker.WriteJson(outputFolder, AnchorsFileName,
                JsonUtility.ToJson(anchors, prettyPrint: true) + "\n");

            result.RenderMilliseconds = renderClock.Elapsed.TotalMilliseconds;
            total.Stop();
            result.TotalMilliseconds = total.Elapsed.TotalMilliseconds;
            return result;
        }

        /// <summary>
        /// The tote's fill geometry as the RUNTIME schema object (serializer == parser, no
        /// drift): per baked row d, <c>slots(dir)</c> (4 layers × 8 points, floor first,
        /// back-to-front within each layer — the rig pre-sorts) and <c>opening(dir)</c>, with
        /// <paramref name="convention"/> applied so row d's anchors belong to row d's sprite.
        /// </summary>
        public static CatchStorageAnchors BuildToteAnchors(IRigScriptHost host, in RigEntry entry,
                                                           in RigGeometry geo,
                                                           AzimuthConvention convention)
        {
            string g = entry.GlobalName;
            var byDir = new CatchDirAnchors[Dirs];
            for (int d = 0; d < Dirs; d++)
            {
                double dir = RigBaker.DirForCell(d, Dirs, convention);
                CatchAnchorPoint[] slots = ParsePoints(
                    host.EvaluateString($"JSON.stringify({g}.slots({Num(dir)}))"));
                CatchAnchorPoint[] opening = ParsePoints(
                    host.EvaluateString($"JSON.stringify({g}.opening({Num(dir)}))"));

                if (slots.Length != 32)
                    throw new InvalidOperationException(
                        $"FishTote.slots({dir}) returned {slots.Length} points, expected 32 " +
                        "(4 layers × 8) — the rig changed shape; update the storage kit contract " +
                        "before baking.");
                if (opening.Length != 4)
                    throw new InvalidOperationException(
                        $"FishTote.opening({dir}) returned {opening.Length} points, expected a quad.");

                byDir[d] = new CatchDirAnchors { slots = slots, opening = opening };
            }

            return new CatchStorageAnchors
            {
                rig = entry.ScriptPath,
                convention = convention.ToString(),
                note = "Baked in-engine with the rig's measured convention applied, so byDir[d] " +
                       "belongs to sheet row d (heading 45°*d). dx/dy are cell px from the " +
                       "container pivot, screen-down-positive; slots are floor-layer first, " +
                       "back-to-front within each layer — draw the first N in index order and " +
                       "the catch visibly stacks. opening is the projected rim quad items clip to.",
                tote = new CatchContainerAnchors
                {
                    cellW = geo.Width, cellH = geo.Height,
                    pivotX = (float)geo.PivotX, pivotY = (float)geo.PivotY,
                    byDir = byDir,
                },
            };
        }

        /// <summary>One mask cell: opaque white inside the direction's projected rim quad,
        /// transparent outside. Pixel centres against a convex quad, either winding. Public so
        /// the bake tests can pin it without a PNG on disk.</summary>
        public static byte[] MaskCell(CatchContainerAnchors tote, int d)
        {
            CatchAnchorPoint[] q = tote.byDir[d].opening;
            var poly = new (double x, double y)[q.Length];
            for (int i = 0; i < q.Length; i++)
                poly[i] = (tote.pivotX + q[i].dx, tote.pivotY + q[i].dy);

            var rgba = new byte[tote.cellW * tote.cellH * 4];
            for (int y = 0; y < tote.cellH; y++)
            for (int x = 0; x < tote.cellW; x++)
            {
                if (!InsideConvex(poly, x + 0.5, y + 0.5)) continue;
                int i = (y * tote.cellW + x) * 4;
                rgba[i] = rgba[i + 1] = rgba[i + 2] = rgba[i + 3] = 255;
            }
            return rgba;
        }

        private static bool InsideConvex((double x, double y)[] poly, double px, double py)
        {
            int sign = 0;
            for (int i = 0; i < poly.Length; i++)
            {
                var a = poly[i];
                var b = poly[(i + 1) % poly.Length];
                double cross = (b.x - a.x) * (py - a.y) - (b.y - a.y) * (px - a.x);
                if (cross == 0) continue;                      // on an edge counts as inside
                int s = cross > 0 ? 1 : -1;
                if (sign == 0) sign = s;
                else if (s != sign) return false;
            }
            return true;
        }

        private static CatchAnchorPoint[] ParsePoints(string jsonArray)
        {
            // The rigs emit arrays of {dx,dy(,sy)} objects; JsonUtility needs an object root.
            var list = JsonUtility.FromJson<PointList>("{\"pts\":" + jsonArray + "}");
            if (list?.pts == null)
                throw new InvalidOperationException($"Could not parse anchor points from: {jsonArray}");
            return list.pts;
        }

        [Serializable]
        private sealed class PointList { public CatchAnchorPoint[] pts; }

        // =====================================================================================
        // BUCKETS — Bucket_<tierId>_empty.png / Bucket_<tierId>_<fill>_<catch>.png, 8 rows × 1
        // =====================================================================================

        public static FishingBakeResult BakeBuckets(string outputFolder = DefaultOutputFolder,
                                                    Action<string, float> progress = null)
        {
            var total = Stopwatch.StartNew();
            var entry = RigCatalog.Get("bucket");

            using IRigScriptHost host = RigScriptHostFactory.Create();
            RigCatalog.InstallModule(host, entry);
            string g = entry.GlobalName;

            // BucketIso exposes pivotCarry/pivotRest, not pivot — rest is the only mode baked
            // here (deck placement; the carry overlays are a character-kit wave, not storage).
            var geo = new RigGeometry(
                width: (int)host.EvaluateNumber($"{g}.W"),
                height: (int)host.EvaluateNumber($"{g}.H"),
                pivotX: host.EvaluateNumber($"{g}.pivotRest.x"),
                pivotY: host.EvaluateNumber($"{g}.pivotRest.y"),
                nativeDirs: (int)host.EvaluateNumber($"{g}.DIRS"),
                rockFrames: 0,
                defaultElevation: host.EvaluateNumber($"{g}.defaultElev"));

            var result = new FishingBakeResult
            {
                RigKey = "bucket", EngineName = host.EngineName, Geometry = geo,
            };

            var probe = StorageRigAzimuthProbe.MeasureBucket(host, g, geo, Dirs);
            result.MeasuredConvention = probe.Convention;
            result.ConventionReport = probe.Report;
            FishingKitBaker.RefuseOnMismatch("bucket", entry.DeclaredConvention,
                                             probe.Convention, probe.Report);

            var tierKeys = FishingKitBaker.ReadStringArray(host,
                $"Object.keys({g}.TIERS)");
            var fills = FishingKitBaker.ReadStringArray(host, $"{g}.FILLS");
            var catches = FishingKitBaker.ReadStringArray(host, $"{g}.CATCHES");

            var renderClock = new Stopwatch();
            System.IO.Directory.CreateDirectory(
                System.IO.Path.Combine(RigCatalog.RepoRoot, outputFolder));

            int done = 0, plan = tierKeys.Count * (1 + (fills.Count - 1) * catches.Count);
            foreach (var tier in tierKeys)
            {
                string id = host.EvaluateString($"{g}.TIERS[{tier}].id");
                foreach (var fill in fills)
                {
                    // An empty vessel renders identically whatever the catch — bake it once.
                    IReadOnlyList<string> perCatch = fill == "empty" ? new[] { (string)null } : (IReadOnlyList<string>)catches;
                    foreach (var catchKind in perCatch)
                    {
                        string stem = catchKind == null
                            ? $"Bucket_{id}_empty"
                            : $"Bucket_{id}_{fill}_{catchKind}";
                        progress?.Invoke(stem, (float)done++ / plan);

                        string opts = catchKind == null
                            ? $"{{tier:{tier},rest:true,fill:'empty'}}"
                            : $"{{tier:{tier},rest:true,fill:{FishingKitBaker.Js(fill)}," +
                              $"catch:{FishingKitBaker.Js(catchKind)}}}";

                        result.Sheets.Add(FishingKitBaker.WriteSheet(outputFolder, stem,
                            rows: Dirs, frames: 1, geo,
                            (d, f) =>
                            {
                                double dir = RigBaker.DirForCell(d, Dirs, probe.Convention);
                                return FishingKitBaker.Render(host,
                                    $"{g}.render({Num(dir)},{opts})", geo, renderClock, result);
                            }, result));
                    }
                }
            }

            result.RenderMilliseconds = renderClock.Elapsed.TotalMilliseconds;
            total.Stop();
            result.TotalMilliseconds = total.Elapsed.TotalMilliseconds;
            return result;
        }

        // =====================================================================================
        // shared
        // =====================================================================================

        private static void AssertEqual(string what, double actual, double expected)
        {
            if (Math.Abs(actual - expected) > 1e-9)
                throw new InvalidOperationException(
                    $"Recipe drift: {what} is {actual} but the runtime fill math is built on " +
                    $"{expected}. Update CatchFillMath (and its parity tests) with the rig, not " +
                    "the other way round.");
        }

        private static string Num(double d) => d.ToString("R", CultureInfo.InvariantCulture);
    }
}
