using System;
using System.Collections.Generic;
using System.IO;
using HiddenHarbours.Art;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>
    /// Bakes the rest of catch pass 2 — the two crustaceans lofted as solids, the five shellfish at
    /// true size, the clam hod, and the composed catch items the containers are filled with.
    ///
    /// <para>Sibling of <see cref="CatchPass2Baker"/> (the fish) for the same reason that one is a
    /// sibling of <see cref="FishingKitBaker"/>: these sheets land in the STORAGE folder, under
    /// <c>CatchStorageSheetSlicer</c>'s per-stem specs rather than the fight kit's prefix specs, and
    /// keeping the two bakes separable keeps the two guarded sets separable.</para>
    ///
    /// <para><b>Pass 1 is not deleted by this.</b> Every stem here carries a <c>2</c>, so the shipped
    /// <c>CatchItem_lobster</c> and friends stay on disk and keep working until their consumers
    /// switch. Nothing is orphaned mid-change.</para>
    ///
    /// <para><b>What is deliberately NOT baked here.</b> The shellfish BED textures. The beds already
    /// have a home in the painted-splat system (<c>Musselbed</c> / <c>Oysterreef</c> / <c>Eelgrass</c>)
    /// and painting a second bed system beside it would be two things to keep in step; the rig's
    /// <c>bed()</c> is handed to the splat lane as an ask instead. Fish ITEMS are also absent, and
    /// that is pass 1's standing decision, still true: the fish deck lays ARE the fish item art, and
    /// <c>CatchKit2.item</c> hands back the same 64×64 cell on the same (32,38) pivot for them.</para>
    /// </summary>
    public static class CatchPass2StorageBaker
    {
        /// <summary>The ADR-0006 recipe's facing count for the directional pieces.</summary>
        public const int Dirs = 8;

        /// <summary>Beside the pass-1 storage sheets, under the same slicer.</summary>
        public const string DefaultOutputFolder = CatchStorageBaker.DefaultOutputFolder;

        /// <summary>The kinds <c>CatchItem2_</c> owns — everything <c>CatchKit2</c> composes EXCEPT
        /// the seven fish (their deck lays are the item art, pass 1's decision) and <c>mixed</c>
        /// (a runtime roll across kinds, not a thing with a sprite of its own).</summary>
        public static readonly string[] BakedItemKinds =
            { "lobster", "crab", "mussel", "clam", "scallop", "oyster", "periwinkle" };

        // =====================================================================================
        // CRUSTACEANS — Crust2_<kind>_<pose>.png (8 rows) + Crust2Held_<kind>.png (its own pivot)
        // =====================================================================================

        /// <summary>
        /// Bakes each animal's own poses at eight headings.
        ///
        /// <para><b>Which poses a kind owns is MEASURED, not listed.</b> The rig remaps what an animal
        /// cannot do — a lobster's <c>sidle</c> and <c>burrow</c>, a crab's <c>flip</c> — to
        /// <c>walk</c>, silently and with zero pixels of difference. So the baker renders each pose
        /// against walk and bakes only what is genuinely its own. That keeps this self-maintaining:
        /// the day the art director teaches the crab to rear differently, or gives the lobster a
        /// burrow, the sheet set follows without anyone editing a table here.</para>
        ///
        /// <para><b>Held is a separate stem because it is a separate PIVOT.</b> Every other pose draws
        /// around <c>pivot</c> (32,40), the ground centre; <c>held</c> draws around <c>hpivot</c>
        /// (32,12), the grip on the animal's back — measured, the held cell's content sits at y≈14
        /// while a walking one sits at y≈38. One stem cannot carry two pivots through the slicer.</para>
        /// </summary>
        public static FishingBakeResult BakeCrustaceans(string outputFolder = DefaultOutputFolder,
                                                        Action<string, float> progress = null)
        {
            var total = Stopwatch.StartNew();
            var entry = RigCatalog.Get("crustacean2");

            using IRigScriptHost host = RigScriptHostFactory.Create();
            var geo = RigCatalog.Install(host, entry);
            string g = entry.GlobalName;

            var result = new FishingBakeResult
            {
                RigKey = "crustacean2", EngineName = host.EngineName, Geometry = geo,
                MeasuredConvention = null,
                ConventionReport =
                    "ang turns the animal and dir is the camera, and the two COMPOSE exactly " +
                    "(ang=45° at dir 0 is byte-identical to ang=0 at dir 1). The turntable is the " +
                    "shared IsoSolid one, probed where it is declared.",
            };

            var kinds = FishingKitBaker.ReadStringArray(host, $"{g}.KINDS");
            var renderClock = new Stopwatch();
            Directory.CreateDirectory(Path.Combine(RigCatalog.RepoRoot, outputFolder));

            // The held pivot, read from the rig — not the ground pivot the cell otherwise uses.
            var heldGeo = new RigGeometry(
                width: geo.Width, height: geo.Height,
                pivotX: host.EvaluateNumber($"{g}.hpivot.x"),
                pivotY: host.EvaluateNumber($"{g}.hpivot.y"),
                nativeDirs: 0, rockFrames: 0, defaultElevation: geo.DefaultElevation);

            int done = 0;
            foreach (string kind in kinds)
            {
                AssertKind(host, g, kind);
                var poses = PosesOwnedBy(host, g, kind);

                foreach (string pose in poses)
                {
                    int frames = PoseFrames(host, g, pose);
                    bool held = string.Equals(pose, "held", StringComparison.Ordinal);
                    string name = held ? $"Crust2Held_{kind}" : $"Crust2_{kind}_{pose}";
                    progress?.Invoke(name, (float)done++ / (kinds.Count * 6));

                    RigGeometry cell = held ? heldGeo : geo;
                    result.Sheets.Add(FishingKitBaker.WriteSheet(outputFolder, name, Dirs, frames, cell,
                        (d, f) => FishingKitBaker.Render(host,
                            $"{g}.render({FishingKitBaker.Js(kind)},{{dir:{d},pose:{FishingKitBaker.Js(pose)}," +
                            $"frame:{f}}})", cell, renderClock, result),
                        result));
                }
            }

            result.RenderMilliseconds = renderClock.Elapsed.TotalMilliseconds;
            total.Stop();
            result.TotalMilliseconds = total.Elapsed.TotalMilliseconds;
            return result;
        }

        /// <summary>
        /// The poses this kind genuinely has art for, measured by rendering. <c>walk</c> is always
        /// owned (it is the fallback); any other pose is owned only if it differs from walk at some
        /// facing and frame.
        /// </summary>
        public static IReadOnlyList<string> PosesOwnedBy(IRigScriptHost host, string g, string kind)
        {
            var owned = new List<string> { "walk" };

            foreach (string pose in PoseNames(host, g))
            {
                if (string.Equals(pose, "walk", StringComparison.Ordinal)) continue;
                int frames = PoseFrames(host, g, pose);

                bool differs = false;
                for (int d = 0; d < Dirs && !differs; d++)
                for (int f = 0; f < frames && !differs; f++)
                {
                    byte[] a = host.EvaluateBytes(
                        $"{g}.render({FishingKitBaker.Js(kind)},{{dir:{d},pose:{FishingKitBaker.Js(pose)},frame:{f}}})");
                    byte[] w = host.EvaluateBytes(
                        $"{g}.render({FishingKitBaker.Js(kind)},{{dir:{d},pose:'walk',frame:{f}}})");
                    for (int i = 0; i < a.Length; i++)
                        if (a[i] != w[i]) { differs = true; break; }
                }
                if (differs) owned.Add(pose);
            }
            return owned;
        }

        /// <summary>Pose names in the rig's own order — <c>POSES</c> is an object, so its keys are
        /// the order. Reads through the one shared parser rather than carrying a second copy of
        /// it.</summary>
        public static IReadOnlyList<string> PoseNames(IRigScriptHost host, string g) =>
            FishingKitBaker.ReadStringArray(host, $"Object.keys({g}.POSES)");

        public static int PoseFrames(IRigScriptHost host, string g, string pose)
        {
            if (!host.EvaluateBool($"typeof {g}.POSES[{FishingKitBaker.Js(pose)}] === 'object'"))
                throw new ArgumentException($"{g} declares no pose '{pose}'.");
            int n = (int)host.EvaluateNumber($"{g}.POSES[{FishingKitBaker.Js(pose)}].n");
            return n > 0 ? n : throw new InvalidOperationException($"pose '{pose}' declares {n} frames.");
        }

        /// <summary>
        /// ⚠️ <c>Crustacean2.render</c> takes the KIND first and resolves an unrecognised one as
        /// <c>'lobster'</c> — measured: <c>render('haddock', …)</c> is zero pixels different from
        /// <c>render('lobster', …)</c>. Without this a typo bakes a lobster under a crab's name.
        /// </summary>
        public static void AssertKind(IRigScriptHost host, string g, string kind)
        {
            if (host.EvaluateBool($"{g}.KINDS.indexOf({FishingKitBaker.Js(kind)}) >= 0")) return;
            throw new ArgumentException(
                $"{g} declares no kind '{kind}'. Known: " +
                string.Join(", ", FishingKitBaker.ReadStringArray(host, $"{g}.KINDS")) +
                ". The rig would silently render a LOBSTER for this key rather than fail.");
        }

        // =====================================================================================
        // SHELLFISH — Shell2_<kind>.png (4 lay variants) + Shell2Hand_<kind>.png (2 clutches)
        // =====================================================================================

        /// <summary>
        /// Bakes the loose shell and the handful for each of the five shellfish. Neither is
        /// directional — the rig has no camera for them at all, which is why it exposes
        /// <c>IW/IH/ipivot</c> instead of <c>W/H/pivot</c> and must be loaded with
        /// <see cref="RigCatalog.InstallModule"/>.
        ///
        /// <para>Two stems because two pivots: the loose shell pivots on GROUND CONTACT (4,6) — the
        /// odd shell dropped on a deck — and the handful pivots on THE GRIP (4,4), one clutch per
        /// hand, pinned to a character's hand anchor.</para>
        ///
        /// <para>These are tiny on purpose. At strict world scale a periwinkle is ONE PIXEL and a
        /// mussel is 2×1; the beds are what make a shellfish flat read, not the individual animal.</para>
        /// </summary>
        public static FishingBakeResult BakeShellfish(string outputFolder = DefaultOutputFolder,
                                                      Action<string, float> progress = null)
        {
            var total = Stopwatch.StartNew();
            var entry = RigCatalog.Get("shellfish2");

            using IRigScriptHost host = RigScriptHostFactory.Create();
            RigCatalog.InstallModule(host, entry);
            string g = entry.GlobalName;

            int w = (int)host.EvaluateNumber($"{g}.IW");
            int h = (int)host.EvaluateNumber($"{g}.IH");
            var itemGeo = new RigGeometry(w, h,
                host.EvaluateNumber($"{g}.ipivot.x"), host.EvaluateNumber($"{g}.ipivot.y"),
                nativeDirs: 0, rockFrames: 0, defaultElevation: 0);
            var handGeo = new RigGeometry(w, h,
                host.EvaluateNumber($"{g}.hpivot.x"), host.EvaluateNumber($"{g}.hpivot.y"),
                nativeDirs: 0, rockFrames: 0, defaultElevation: 0);

            var result = new FishingBakeResult
            {
                RigKey = "shellfish2", EngineName = host.EngineName, Geometry = itemGeo,
                MeasuredConvention = null,
                ConventionReport = "not directional — the rig takes no dir at all, nothing to probe",
            };

            var kinds = FishingKitBaker.ReadStringArray(host, $"{g}.KINDS");
            int itemVariants = (int)host.EvaluateNumber($"{g}.VARIANTS");
            if (itemVariants <= 0)
                throw new InvalidOperationException($"{g}.VARIANTS is {itemVariants}");

            // The handful's variant count is NOT VARIANTS — the sidecar says 2 where items have 4.
            // Measured rather than assumed: render upward until the rig starts repeating itself.
            int handVariants = CountDistinctHandfuls(host, g, kinds[0], w, h);

            var renderClock = new Stopwatch();
            Directory.CreateDirectory(Path.Combine(RigCatalog.RepoRoot, outputFolder));

            int done = 0;
            foreach (string kind in kinds)
            {
                progress?.Invoke($"Shell2_{kind}", (float)done++ / (kinds.Count * 2));
                result.Sheets.Add(FishingKitBaker.WriteSheet(outputFolder, $"Shell2_{kind}",
                    rows: 1, frames: itemVariants, itemGeo,
                    (d, f) => FishingKitBaker.Render(host,
                        $"{g}.renderItem({FishingKitBaker.Js(kind)},{f})", itemGeo, renderClock, result),
                    result));

                progress?.Invoke($"Shell2Hand_{kind}", (float)done++ / (kinds.Count * 2));
                result.Sheets.Add(FishingKitBaker.WriteSheet(outputFolder, $"Shell2Hand_{kind}",
                    rows: 1, frames: handVariants, handGeo,
                    (d, f) => FishingKitBaker.Render(host,
                        $"{g}.renderHandful({FishingKitBaker.Js(kind)},{f})", handGeo, renderClock, result),
                    result));
            }

            result.RenderMilliseconds = renderClock.Elapsed.TotalMilliseconds;
            total.Stop();
            result.TotalMilliseconds = total.Elapsed.TotalMilliseconds;
            return result;
        }

        /// <summary>
        /// How many DISTINCT handfuls the rig draws, found by rendering. The rig publishes no count
        /// for them (<c>VARIANTS</c> is the item count, 4, and the handful has fewer), and reading
        /// the number off the sidecar would make a derived file the source of truth for a bake.
        /// </summary>
        static int CountDistinctHandfuls(IRigScriptHost host, string g, string kind, int w, int h)
        {
            const int probeCeiling = 8;
            var seen = new List<byte[]>();
            for (int v = 0; v < probeCeiling; v++)
            {
                byte[] px = host.EvaluateBytes($"{g}.renderHandful({FishingKitBaker.Js(kind)},{v})");
                if (px.Length != w * h * 4)
                    throw new InvalidOperationException(
                        $"{g}.renderHandful came back {px.Length} bytes, expected {w * h * 4}");

                bool repeat = false;
                foreach (byte[] old in seen)
                {
                    bool same = true;
                    for (int i = 0; i < px.Length; i++) if (old[i] != px[i]) { same = false; break; }
                    if (same) { repeat = true; break; }
                }
                if (repeat) break;
                seen.Add(px);
            }

            if (seen.Count == 0)
                throw new InvalidOperationException($"{g}.renderHandful drew nothing for '{kind}'");
            return seen.Count;
        }

        // =====================================================================================
        // THE CLAM HOD — Hod2_back.png / Hod2_front.png + Hod2_heap_<band>.png, 8 rows × 1
        // =====================================================================================

        /// <summary>
        /// Bakes the wire roller basket's two layers and the HEAP that goes between them. Hollow like
        /// the tote: the back is drawn, then the clams clipped to <c>opening(dir)</c>, then the front
        /// over it — so a full hod shows shells through the wire rather than a lid of them.
        ///
        /// <para><b>Why the heap is BAKED and not composed at runtime.</b> The rig's own recipe for a
        /// filling hod is a runtime composition over <c>CatchKit2.heap</c>, which is JavaScript: at play
        /// time there is no script host, so the only ways to draw a full hod are to bake the composition
        /// or to reimplement the heap's layout in C#. The second is a second copy of a layout the rig
        /// owns, drifting from the day it is written — the same shape of guess that makes
        /// <c>Crustacean2.render</c> draw a lobster for an unknown kind. So: bake it. The runtime then
        /// swaps three sprites and owns no layout at all.</para>
        ///
        /// <para><b>The rig is not edited to do this.</b> <c>CatchKit2.heap</c> already hands back the
        /// composed surface AND its offset from the container pivot (<c>x</c>, <c>y</c>), so placing that
        /// surface in the hod's cell is the one thing the host adds — the same division of labour as
        /// <see cref="CatchStorageBaker.CanvasShimJs"/> (ADR 0021 §5: what the engine needs and the rig
        /// does not provide is the HOST's job, never a patch to the art director's file). The layout, the
        /// mask, the dome shading, the lowering and the crown all stay in the rig.</para>
        ///
        /// <para><b>The two shipped layers must not move.</b> This bake now loads the whole catchKit2
        /// chain before the hod, where it used to load the hod alone; <c>Hod2_back</c> and
        /// <c>Hod2_front</c> are byte-identical either way — verified in the standalone V8 harness
        /// against the committed sheets before this method was written, and kept that way by
        /// <c>CatchPass2KitTests</c>.</para>
        /// </summary>
        public static FishingBakeResult BakeClamHod(string outputFolder = DefaultOutputFolder,
                                                    Action<string, float> progress = null)
        {
            var total = Stopwatch.StartNew();
            var entry = RigCatalog.Get("clamHod");

            using IRigScriptHost host = RigScriptHostFactory.Create();

            // The heap's chain, in the rig's own order — the same one BakeCatchItems installs, because
            // it is the same glue rig. A missing prerequisite does not throw: CatchKit2.heap simply
            // answers null without Shellfish2, which would bake four empty sheets.
            RigCatalog.InstallModule(host, RigCatalog.Get("deckIsoSolid"));
            RigCatalog.Install(host, RigCatalog.Get("fish2"));
            RigCatalog.InstallModule(host, RigCatalog.Get("shellfish2"));
            RigCatalog.Install(host, RigCatalog.Get("crustacean2"));
            host.Execute(CatchStorageBaker.CanvasShimJs);
            RigCatalog.InstallModule(host, RigCatalog.Get("catchKit2"));

            var geo = RigCatalog.Install(host, entry);
            string g = entry.GlobalName;

            AssertHeapsClams(host);

            var result = new FishingBakeResult
            {
                RigKey = "clamHod", EngineName = host.EngineName, Geometry = geo,
                MeasuredConvention = null,
                ConventionReport = "rides the shared IsoSolid turntable, probed where it is declared",
            };

            var renderClock = new Stopwatch();
            Directory.CreateDirectory(Path.Combine(RigCatalog.RepoRoot, outputFolder));

            string[] layers = { "back", "front" };
            var bands = HeapBands(host, g);
            int steps = layers.Length + bands.Count;

            for (int i = 0; i < layers.Length; i++)
            {
                string layer = layers[i];
                progress?.Invoke($"Hod2_{layer}", (float)i / steps);
                result.Sheets.Add(FishingKitBaker.WriteSheet(outputFolder, $"Hod2_{layer}",
                    Dirs, frames: 1, geo,
                    (d, f) => FishingKitBaker.Render(host,
                        $"{g}.render({d},{{layer:{FishingKitBaker.Js(layer)}}})", geo, renderClock, result),
                    result));
            }

            for (int i = 0; i < bands.Count; i++)
            {
                string band = bands[i];
                progress?.Invoke($"Hod2_heap_{band}", (float)(layers.Length + i) / steps);
                result.Sheets.Add(FishingKitBaker.WriteSheet(outputFolder, $"Hod2_heap_{band}",
                    Dirs, frames: 1, geo,
                    (d, f) => HeapCell(host, g, d, band, geo, renderClock, result),
                    result));
            }

            result.AnchorJsonPath = WriteHodAnchors(host, g, geo, bands, outputFolder);
            result.RenderMilliseconds = renderClock.Elapsed.TotalMilliseconds;
            total.Stop();
            result.TotalMilliseconds = total.Elapsed.TotalMilliseconds;
            return result;
        }

        /// <summary>
        /// The bands that actually heap something, MEASURED against the rigs rather than listed here:
        /// the hod's own <c>FILLS</c>, minus every one <c>CatchKit2.FRAC</c> gives a zero fraction
        /// (<c>heap</c> answers null for those — an empty hod is the back+front pair with nothing
        /// between them). The day the art director adds a band, the sheet set follows.
        /// </summary>
        public static IReadOnlyList<string> HeapBands(IRigScriptHost host, string g)
        {
            var bands = new List<string>();
            foreach (string fill in FishingKitBaker.ReadStringArray(host, $"{g}.FILLS"))
                if (host.EvaluateNumber($"CatchKit2.FRAC[{FishingKitBaker.Js(fill)}] || 0") > 0)
                    bands.Add(fill);

            if (bands.Count == 0)
                throw new InvalidOperationException(
                    $"{g}.FILLS names no band CatchKit2.FRAC gives a non-zero fraction — there is " +
                    "nothing to heap. One of the two moved; do not bake until they agree.");
            return bands;
        }

        /// <summary>
        /// One heap cell: the rig's composed surface for this band, placed in the hod's cell at the
        /// offset the rig itself reports.
        ///
        /// <para><c>CatchKit2.heap</c> answers <c>{canvas, x, y, w, h}</c> where <c>x</c>/<c>y</c> are px
        /// offsets FROM THE PIVOT — the same frame <c>opening(dir)</c> is published in — so the cell
        /// position is <c>pivot + (x, y)</c> and nothing here decides where the clams sit. The canvas is
        /// the shim's mailbox, read exactly as <see cref="ItemPixelsExpr"/> reads an item's.</para>
        ///
        /// <para>Bounds are ASSERTED, not clamped: a heap that hangs out of the cell means the rim quad
        /// and the cell have stopped agreeing, and cropping it silently would ship a sliced-off heap.</para>
        /// </summary>
        static byte[] HeapCell(IRigScriptHost host, string g, int dir, string band,
                               in RigGeometry geo, Stopwatch renderClock, FishingBakeResult result)
        {
            // One evaluation, then read its fields — heap() caches, but a second call must never be
            // where the numbers come from.
            host.Execute($"globalThis.__hodHeap = CatchKit2.heap('clam', {g}.opening({dir}), " +
                         $"{g}.depthPx(), {FishingKitBaker.Js(band)});");

            if (host.EvaluateBool("__hodHeap === null || __hodHeap === undefined"))
                throw new InvalidOperationException(
                    $"CatchKit2.heap came back null for band '{band}' at dir {dir}. It answers null for " +
                    "a zero fraction and when Shellfish2 is absent — both mean this bake's chain or the " +
                    "rig's FRAC table changed under it.");

            int hx = (int)host.EvaluateNumber("__hodHeap.x"), hy = (int)host.EvaluateNumber("__hodHeap.y");
            int hw = (int)host.EvaluateNumber("__hodHeap.w"), hh = (int)host.EvaluateNumber("__hodHeap.h");

            renderClock.Start();
            byte[] heap = host.EvaluateBytes("__hodHeap.canvas.__img.data");
            renderClock.Stop();
            result.CellsRendered++;

            if (heap.Length != hw * hh * 4)
                throw new InvalidOperationException(
                    $"the heap for '{band}' at dir {dir} came back {heap.Length} bytes, expected " +
                    $"{hw * hh * 4} for {hw}×{hh} RGBA.");

            int x0 = (int)geo.PivotX + hx, y0 = (int)geo.PivotY + hy;
            if (x0 < 0 || y0 < 0 || x0 + hw > geo.Width || y0 + hh > geo.Height)
                throw new InvalidOperationException(
                    $"the heap for '{band}' at dir {dir} lands at ({x0},{y0}) {hw}×{hh}, outside the " +
                    $"{geo.Width}×{geo.Height} cell. The rim quad and the cell have stopped agreeing; " +
                    "cropping it would ship a sliced-off heap.");

            var cell = new byte[geo.Width * geo.Height * 4];
            for (int y = 0; y < hh; y++)
            for (int x = 0; x < hw; x++)
            {
                int s = (y * hw + x) * 4;
                int t = ((y0 + y) * geo.Width + (x0 + x)) * 4;
                cell[t] = heap[s]; cell[t + 1] = heap[s + 1];
                cell[t + 2] = heap[s + 2]; cell[t + 3] = heap[s + 3];
            }
            return cell;
        }

        /// <summary>
        /// ⚠️ <c>CatchKit2.heap</c> heaps SHELLFISH and nothing else — <c>Shellfish2.heap</c> is what
        /// composes the surface — and a kind it does not know does not fail loudly, exactly like the
        /// silent fallbacks elsewhere in this kit. Assert the clam before four sheets are written.
        /// </summary>
        static void AssertHeapsClams(IRigScriptHost host)
        {
            if (!host.EvaluateBool("CatchKit2.SHELL.indexOf('clam') >= 0"))
                throw new ArgumentException(
                    "CatchKit2 no longer counts the clam among its SHELL kinds. Known: " +
                    string.Join(", ", FishingKitBaker.ReadStringArray(host, "CatchKit2.SHELL")) +
                    ". Only a shellfish heaps; the hod is the clam dig's basket.");

            if (!host.EvaluateBool("typeof Shellfish2.heap === 'function'"))
                throw new InvalidOperationException(
                    "Shellfish2.heap is missing — CatchKit2.heap answers null without it and this bake " +
                    "would write four empty sheets.");
        }

        /// <summary>The rim polygon and depth a heap is clipped to, per facing, the bands that heap, and
        /// the roller grip a carried hod pins to.</summary>
        static string WriteHodAnchors(IRigScriptHost host, string g, in RigGeometry geo,
                                      IReadOnlyList<string> bands, string outputFolder)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("{\n");
            sb.Append($"  \"global\": \"{g}\",\n");
            sb.Append($"  \"cell\": {{ \"w\": {geo.Width}, \"h\": {geo.Height} }},\n");
            sb.Append($"  \"pivotTopLeft\": {{ \"x\": {geo.PivotX}, \"y\": {geo.PivotY} }},\n");
            sb.Append($"  \"dirs\": {Dirs},\n");
            sb.Append($"  \"depthPx\": {host.EvaluateNumber($"{g}.depthPx()")},\n");
            sb.Append("  \"_note\": \"opening[] is the rim quad per direction row, in px offsets from " +
                      "the pivot — the polygon a shellfish heap is clipped to. carryGrip is the roller " +
                      "grip; it is the SAME point at every facing because it projects (0,0,GZ), dead " +
                      "centre, and a turn about the vertical axis cannot move it.\",\n");

            sb.Append("  \"fills\": ");
            sb.Append(host.EvaluateString($"JSON.stringify({g}.FILLS)"));
            sb.Append(",\n  \"fillFrac\": ");
            sb.Append(host.EvaluateString(
                $"JSON.stringify({g}.FILLS.reduce(function(o,f){{o[f]=CatchKit2.FRAC[f]||0;return o;}},{{}}))"));
            sb.Append(",\n  \"heapSheets\": [");
            for (int i = 0; i < bands.Count; i++)
                sb.Append(i == 0 ? $"\"Hod2_heap_{bands[i]}\"" : $", \"Hod2_heap_{bands[i]}\"");
            sb.Append("],\n");
            sb.Append("  \"_heapNote\": \"every band with a non-zero fraction is baked as " +
                      "Hod2_heap_<band>, one 8-row sheet each, on THIS cell and pivot: draw back, then " +
                      "the band's heap, then front. 'empty' heaps nothing and has no sheet.\",\n");

            sb.Append("  \"carryGrip\": ");
            sb.Append(host.EvaluateString($"JSON.stringify({g}.cpivot(0))"));
            sb.Append(",\n  \"opening\": [\n");
            for (int d = 0; d < Dirs; d++)
            {
                sb.Append("    ");
                sb.Append(host.EvaluateString($"JSON.stringify({g}.opening({d}))"));
                sb.Append(d < Dirs - 1 ? ",\n" : "\n");
            }
            sb.Append("  ]\n}\n");
            return FishingKitBaker.WriteJson(outputFolder, "ClamHodAnchors.json", sb.ToString());
        }

        // =====================================================================================
        // CATCH ITEMS — CatchItem2_<kind>.png, 1 row × the rig's lay variants
        // =====================================================================================

        /// <summary>
        /// Bakes the composed item art the containers are filled with, for the kinds this bake owns
        /// (see <see cref="BakedItemKinds"/>).
        ///
        /// <para>Each kind carries its OWN cell and pivot — the crustaceans come back 64×64 on the
        /// ground pivot, the shellfish 8×8 on theirs — so the geometry is read per kind from the
        /// rig's own report rather than assumed once for the sheet set. That is also why the storage
        /// slicer keys these per stem rather than per prefix.</para>
        /// </summary>
        public static FishingBakeResult BakeCatchItems(string outputFolder = DefaultOutputFolder,
                                                       Action<string, float> progress = null)
        {
            var total = Stopwatch.StartNew();
            using IRigScriptHost host = RigScriptHostFactory.Create();

            // Order matters and is the rig's own: the turntable, then the three rigs catchKit2
            // composes from, then the canvas shim, then the glue. A missing prerequisite does not
            // throw here — fillItems would silently roll [0.85, 1.15] for every species.
            RigCatalog.InstallModule(host, RigCatalog.Get("deckIsoSolid"));
            RigCatalog.Install(host, RigCatalog.Get("fish2"));
            RigCatalog.InstallModule(host, RigCatalog.Get("shellfish2"));
            var crustGeo = RigCatalog.Install(host, RigCatalog.Get("crustacean2"));
            host.Execute(CatchStorageBaker.CanvasShimJs);
            RigCatalog.InstallModule(host, RigCatalog.Get("catchKit2"));

            var result = new FishingBakeResult
            {
                RigKey = "catchKit2", EngineName = host.EngineName, Geometry = crustGeo,
                MeasuredConvention = null,
                ConventionReport = "items are laid at CatchKit2's own scattered angles — no " +
                                   "turntable, nothing to probe",
            };

            // Cross-check the recipe against the rig before writing anything.
            AssertEqual("Shellfish2.VARIANTS", host.EvaluateNumber("Shellfish2.VARIANTS"),
                        CatchFillMath.Variants);
            AssertEqual("Crustacean2.POSES.walk.n", host.EvaluateNumber("Crustacean2.POSES.walk.n"),
                        CatchFillMath.Variants);
            foreach (string kind in BakedItemKinds)
                if (!host.EvaluateBool($"CatchKit2.CATCHES.indexOf({FishingKitBaker.Js(kind)}) >= 0"))
                    throw new ArgumentException(
                        $"CatchKit2 does not compose '{kind}'. Known: " +
                        string.Join(", ", FishingKitBaker.ReadStringArray(host, "CatchKit2.CATCHES")));

            var renderClock = new Stopwatch();
            Directory.CreateDirectory(Path.Combine(RigCatalog.RepoRoot, outputFolder));

            for (int k = 0; k < BakedItemKinds.Length; k++)
            {
                string kind = BakedItemKinds[k];
                progress?.Invoke($"CatchItem2_{kind}", (float)k / BakedItemKinds.Length);

                RigGeometry geo = ItemGeometry(host, kind);
                result.Sheets.Add(FishingKitBaker.WriteSheet(outputFolder, $"CatchItem2_{kind}",
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

        /// <summary>One item cell's geometry, from <c>CatchKit2.item</c>'s own report.</summary>
        public static RigGeometry ItemGeometry(IRigScriptHost host, string kind)
        {
            string item = $"CatchKit2.item({FishingKitBaker.Js(kind)},{{variant:0}})";
            return new RigGeometry(
                width: (int)host.EvaluateNumber($"{item}.w"),
                height: (int)host.EvaluateNumber($"{item}.h"),
                pivotX: host.EvaluateNumber($"{item}.ax"),
                pivotY: host.EvaluateNumber($"{item}.ay"),
                nativeDirs: 0, rockFrames: 0, defaultElevation: 0);
        }

        /// <summary>The raw pixels of one composed item, read back out of the shim's canvas mailbox.</summary>
        public static string ItemPixelsExpr(string kind, int variant) =>
            $"CatchKit2.item({FishingKitBaker.Js(kind)},{{variant:{variant}}}).canvas.__img.data";

        static void AssertEqual(string what, double got, double want)
        {
            if (Math.Abs(got - want) > 1e-9)
                throw new InvalidOperationException(
                    $"{what} is {got}, but the runtime fill math expects {want}. One of the two moved; " +
                    "do not bake until they agree.");
        }
    }
}
