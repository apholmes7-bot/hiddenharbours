// ⚠️⚠️ THROWAWAY SPIKE CODE — spike/3d-boats. NOT A PIPELINE. NOT FOR MERGE AS-IS. ⚠️⚠️
// Produces the images that answer "does a real-time 3D hull look like the baked sprites?".
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using HiddenHarbours.Tools.RigBaking;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tools.Spike3dBoats
{
    public static class Spike3dBoatMenu
    {
        const string RigKey = "lobsterBoat";
        static string OutDir =>
            Environment.GetEnvironmentVariable("HH_SPIKE_OUT") ??
            Path.Combine(Path.GetTempPath(), "hh3dspike");

        [MenuItem("Hidden Harbours/Spikes/3D Boats — run all")]
        public static void RunAll()
        {
            Directory.CreateDirectory(OutDir);
            var log = new StringBuilder();
            try
            {
                using var host = new V8RigScriptHost();
                log.AppendLine($"engine: {host.EngineName}");
                var rig = RigMeshExtractor.Extract(host, RigKey);
                log.AppendLine($"faces={rig.Faces.Count} mats={rig.Materials.Count} " +
                               $"cell={rig.W}x{rig.H} px/m={rig.PxPerMetre} elev={rig.DefaultElev} " +
                               $"gain={rig.Gain} bias={rig.Bias} LN={rig.LightN}");

                using var ren = new Spike3dBoatRenderer(rig, rig.DefaultElev);
                log.AppendLine($"mesh: {ren.Vertices} verts, {ren.Triangles} tris");
                MemoryReport(rig, ren, log);

                CalibrateDepth(host, rig, ren, log);
                AbComparison(host, rig, ren, log);
                RotationSweep(rig, ren, log);
                InScene(rig, ren, log);
            }
            catch (Exception e)
            {
                log.AppendLine("FAILED: " + e);
            }
            File.WriteAllText(Path.Combine(OutDir, "spike-log.txt"), log.ToString());
            Debug.Log("[3D SPIKE]\n" + log);
        }

        // ==================================================================================
        // MOTION TESTS. The stills said "3D matches the sprite exactly". These ask the harder
        // question: does it still read as the same game when it MOVES? Two named risks:
        //   1. dither crawl   — the rig's Bayer dither is indexed by SCREEN pixel, so a naive
        //                       implementation leaves the stipple pinned to the display while the
        //                       hull slides underneath it. A baked sprite carries its dither in the
        //                       texture, so the stipple rides along. Fix + A/B, don't just assert.
        //   2. shade popping  — flat facets quantised to a 5–7 entry ramp mean a big panel jumps a
        //                       whole shade step. At 32 discrete facings you read that as an
        //                       expected snap; rotating continuously it may read as flicker.
        // Risk 2 is the one that could INVERT the case for 3D, so it is rendered against the
        // 32-facing sprite doing the same turn, at the same speed, in the same frame.
        // ==================================================================================
        [MenuItem("Hidden Harbours/Spikes/3D Boats — motion tests")]
        public static void RunMotion()
        {
            Directory.CreateDirectory(OutDir);
            var log = new StringBuilder();
            try
            {
                using var host = new V8RigScriptHost();
                var rig = RigMeshExtractor.Extract(host, RigKey);
                using var ren = new Spike3dBoatRenderer(rig, rig.DefaultElev);
                CalibrateDepth(host, rig, ren, log);
                Vector4 calib = ren.DitherPhase;
                string art = Path.Combine(RigCatalog.RepoRoot, "Assets/_Project/Art");
                var sheet = Load(art + "/Boats/LobsterBoatIso.png");

                // ---------- A. translation, for the dither-crawl A/B ----------
                // Broadside (cell 8, heading 090° = due East = screen right): the largest area of
                // flat white topside in the kit, which is exactly where a swimming stipple shows.
                const int CW = 800, CH = 420, Fps = 30, NF = 60;
                const float SpeedMps = 4.24f;                 // her measured cruise
                float pxPerFrame = SpeedMps * rig.PxPerMetre / Fps;
                const int Cell = 8, PivotY = 258;
                // Start far enough in that the 456-wide cell never hangs off the left edge — a
                // clipped hull silently poisons any hull-relative frame-to-frame comparison.
                const int StartX = 240;
                double dirT = RigBaker.DirForCell(Cell, 32, AzimuthConvention.CounterClockwise);
                log.AppendLine($"\n-- translation: {SpeedMps} m/s @ {rig.PxPerMetre} PPU / {Fps} fps " +
                               $"= {pxPerFrame:0.000} px per frame, {NF} frames --");

                var track = new StringBuilder("frame,pivotX\n");
                foreach (string d in new[] { "dither_before", "dither_after", "dither_sprite" })
                    Directory.CreateDirectory(Path.Combine(OutDir, "frames", d));

                for (int f = 0; f < NF; f++)
                {
                    // Whole-pixel hull motion, exactly as a pixel-perfect 2D game moves a sprite.
                    // This ISOLATES dither crawl: the geometry steps cleanly, so anything that
                    // shimmers is the stipple and nothing else.
                    int px = StartX + Mathf.RoundToInt(f * pxPerFrame);

                    ren.DitherPhase = calib;                                   // screen-pinned (naive)
                    WritePng(FramePath("dither_before", f),
                             ren.RenderAt(dirT, CW, CH, new Vector2(px, PivotY), out _), CW, CH, 1, true);

                    // THE FIX: index the Bayer lookup in the hull's own CELL coordinates — screen
                    // pixel minus the hull's snapped screen origin, plus the rig's pivot. The pivot
                    // term matters: locking to the hull alone kills the crawl but leaves an
                    // arbitrary phase, so the mesh would not be bit-identical to a baked sprite of
                    // the same hull. With the pivot in, mesh and sprite hulls can share a scene.
                    ren.DitherPhase = new Vector4(Mod4(calib.x + rig.PivotX - px),
                                                  Mod4(calib.y + rig.PivotY - PivotY), calib.z, 0);
                    WritePng(FramePath("dither_after", f),
                             ren.RenderAt(dirT, CW, CH, new Vector2(px, PivotY), out _), CW, CH, 1, true);

                    var canvas = new Color32[CW * CH];                          // baked-sprite control
                    if (sheet != null)
                        BlitCell(canvas, CW, CH, sheet, Cell % 8, Cell / 8, rig.W, rig.H,
                                 px - Mathf.RoundToInt(rig.PivotX), PivotY - Mathf.RoundToInt(rig.PivotY));
                    WritePng(FramePath("dither_sprite", f), canvas, CW, CH, 1, true);
                    track.Append($"{f},{px}\n");
                }
                File.WriteAllText(Path.Combine(OutDir, "frames", "track.csv"), track.ToString());

                // ---------- B. rotation: continuous mesh vs 32-facing sprite ----------
                const int RF = 120;                       // 3° per frame, 4 s at 30 fps
                foreach (string d in new[] { "rot_mesh", "rot_sprite" })
                    Directory.CreateDirectory(Path.Combine(OutDir, "frames", d));
                ren.DitherPhase = calib;                  // hull is stationary here; phase is moot
                log.AppendLine($"-- rotation: 360° over {RF} frames = {360.0 / RF:0.0}°/frame, " +
                               "mesh continuous vs sprite snapping between 32 cells (11.25° each) --");

                for (int f = 0; f < RF; f++)
                {
                    double heading = f * 360.0 / RF;
                    // Cell k depicts heading 360k/32; the rig's dir runs the other way (8 dir units
                    // = 360°), which is the CCW correction the baker applies at bake time.
                    WritePng(FramePath("rot_mesh", f),
                             ren.RenderCell(8.0 - heading / 45.0, out _), rig.W, rig.H, 1, true);

                    int k = Mathf.RoundToInt((float)(heading / 11.25)) % 32;
                    var canvas = new Color32[rig.W * rig.H];
                    if (sheet != null)
                        BlitCell(canvas, rig.W, rig.H, sheet, k % 8, k / 8, rig.W, rig.H, 0, 0);
                    WritePng(FramePath("rot_sprite", f), canvas, rig.W, rig.H, 1, true);
                }
                log.AppendLine("frames written; assemble with make-gifs.py");
            }
            catch (Exception e) { log.AppendLine("FAILED: " + e); }
            File.WriteAllText(Path.Combine(OutDir, "motion-log.txt"), log.ToString());
            Debug.Log("[3D SPIKE MOTION]\n" + log);
        }

        static float Mod4(float v) => ((v % 4f) + 4f) % 4f;
        static string FramePath(string set, int f) =>
            Path.Combine(OutDir, "frames", set, f.ToString("D4") + ".png");

        /// <summary>
        /// Unity's depth convention for a hand-rolled CommandBuffer with explicit matrices depends
        /// on reversed-Z, and guessing it cost a render pass showing the hull's BOTTOM through the
        /// deck. So measure it: whichever (ZTest, clear) pair agrees with the rig's own z-buffer is
        /// by definition the right one. Cheap, and it can never silently rot.
        /// </summary>
        static void CalibrateDepth(IRigScriptHost host, RigMeshData rig, Spike3dBoatRenderer ren,
                                   StringBuilder log)
        {
            double dir = RigBaker.DirForCell(8, 32, AzimuthConvention.CounterClockwise);
            var truth = ToPixels(host.EvaluateBytes(
                $"LobsterBoatIso.render({dir.ToString("R", CultureInfo.InvariantCulture)})"), rig.W, rig.H);
            (int z, float c) best = (4, 1f); double bestScore = double.MaxValue;
            log.AppendLine("\n-- depth-convention probe --");
            foreach (var cand in new[] { (z: 4, c: 1f), (z: 7, c: 0f) })
            {
                ren.ZTestOp = cand.z; ren.ClearDepth = cand.c;
                var mine = ren.RenderCell(dir, out _);
                long err = 0; int n = 0;
                for (int i = 0; i < truth.Length; i++)
                {
                    if (truth[i].a == 0 && mine[i].a == 0) continue;
                    n++;
                    err += Mathf.Abs(truth[i].r - mine[i].r) + Mathf.Abs(truth[i].g - mine[i].g)
                         + Mathf.Abs(truth[i].b - mine[i].b) + (truth[i].a == mine[i].a ? 0 : 255);
                }
                double score = err / (double)Mathf.Max(1, n);
                log.AppendLine($"  ZTest={(cand.z == 4 ? "LEqual" : "GEqual")} clear={cand.c} -> mean err {score:0.00}");
                if (score < bestScore) { bestScore = score; best = cand; }
            }
            ren.ZTestOp = best.z; ren.ClearDepth = best.c;
            log.AppendLine($"  chose ZTest={(best.z == 4 ? "LEqual" : "GEqual")} clear={best.c}");

            // --- dither phase. The residual after depth is a pure Bayer stipple, i.e. the 4×4
            // ordered-dither grid is offset relative to the rig's. Probe all 32 alignments.
            log.AppendLine("-- dither-phase probe --");
            Vector4 bestPhase = Vector4.zero; double bestD = double.MaxValue;
            for (int swap = 0; swap < 2; swap++)
                for (int dy = 0; dy < 4; dy++)
                    for (int dx = 0; dx < 4; dx++)
                    {
                        ren.DitherPhase = new Vector4(dx, dy, swap, 0);
                        var mine = ren.RenderCell(dir, out _);
                        int d = 0;
                        for (int i = 0; i < truth.Length; i++)
                            if (truth[i].r != mine[i].r || truth[i].g != mine[i].g ||
                                truth[i].b != mine[i].b || truth[i].a != mine[i].a) d++;
                        if (d < bestD) { bestD = d; bestPhase = ren.DitherPhase; }
                    }
            ren.DitherPhase = bestPhase;
            log.AppendLine($"  chose offset ({bestPhase.x},{bestPhase.y}) swap={bestPhase.z} " +
                           $"-> {bestD} differing px");
        }

        // ---------------------------------------------------------------- 1. A/B vs baked sprite
        static void AbComparison(IRigScriptHost host, RigMeshData rig, Spike3dBoatRenderer ren,
                                 StringBuilder log)
        {
            // Cells of LobsterBoatIso.png, whose facings are genuinely clockwise: cell k depicts
            // heading 360*k/32. Pick bow-on, a diagonal, beam-on, and an off-axis quarter.
            int[] cells = { 0, 4, 8, 12, 20, 28 };
            var strips = new List<Color32[]>();
            log.AppendLine("\n-- A/B: rig software render vs real-time 3D mesh, same heading --");

            foreach (int cell in cells)
            {
                double dir = RigBaker.DirForCell(cell, 32, AzimuthConvention.CounterClockwise);
                string d = dir.ToString("R", CultureInfo.InvariantCulture);
                var truth = ToPixels(host.EvaluateBytes($"LobsterBoatIso.render({d})"), rig.W, rig.H);
                var mine = ren.RenderCell(dir, out double ms);

                int diff = 0; long err = 0; int inked = 0;
                var diffImg = new Color32[rig.W * rig.H];
                for (int i = 0; i < truth.Length; i++)
                {
                    bool a = truth[i].a > 0, b = mine[i].a > 0;
                    if (a || b) inked++;
                    int e = Mathf.Abs(truth[i].r - mine[i].r) + Mathf.Abs(truth[i].g - mine[i].g)
                          + Mathf.Abs(truth[i].b - mine[i].b) + (a == b ? 0 : 255);
                    if (e > 0) { diff++; err += e; }
                    diffImg[i] = e == 0
                        ? (a ? new Color32(24, 28, 32, 255) : new Color32(0, 0, 0, 0))
                        : new Color32(255, (byte)Mathf.Max(0, 200 - e), 0, 255);
                }
                log.AppendLine($"cell {cell,2} (heading {cell * 360 / 32,3}°, dir {d,-8}) " +
                               $"differing px {diff,6} / {inked,6} inked = {100f * diff / Mathf.Max(1, inked),5:0.00}%  " +
                               $"mean|err| {(diff == 0 ? 0 : err / (double)diff),6:0.0}  gpu {ms:0.0} ms");

                strips.Add(Row(new[] { truth, mine, diffImg }, rig.W, rig.H));
            }
            WritePng(Path.Combine(OutDir, "1-ab-sprite-vs-3d.png"),
                     Stack(strips, rig.W * 3, rig.H), rig.W * 3, rig.H * cells.Length, 1);
            log.AppendLine("columns: [baked sprite | real-time 3D | diff]  rows: the headings above");
        }

        // ------------------------------------------------------------------- 2. rotation sweep
        static void RotationSweep(RigMeshData rig, Spike3dBoatRenderer ren, StringBuilder log)
        {
            // 32 baked facings step 11.25°. Show 8 steps of 2.8125° — seven of which no sheet holds.
            // Cell 20 sits at dir 3.0; one baked step is 8/32 = 0.25 dir. Walk it in 1/8 of a step.
            var cellsOut = new List<Color32[]>();
            double d0 = RigBaker.DirForCell(20, 32, AzimuthConvention.CounterClockwise);
            for (int i = 0; i < 8; i++)
                cellsOut.Add(ren.RenderCell(d0 - i * (0.25 / 8.0), out _));
            WritePng(Path.Combine(OutDir, "2-sweep-fine.png"),
                     Row(cellsOut, rig.W, rig.H), rig.W * 8, rig.H, 1);

            var coarse = new List<Color32[]>();
            for (int k = 0; k < 16; k++)
                coarse.Add(ren.RenderCell(RigBaker.DirForCell(k * 2, 32, AzimuthConvention.CounterClockwise), out _));
            WritePng(Path.Combine(OutDir, "2-sweep-coarse.png"),
                     Grid(coarse, 8, rig.W, rig.H), rig.W * 8, rig.H * 2, 1);
            log.AppendLine("\n-- sweep: 2-sweep-fine.png steps 2.8125° (7 of 8 unrepresentable in the 32-cell sheet)");
        }

        // ------------------------------------------------------------------ 3. in-scene consistency
        static void InScene(RigMeshData rig, Spike3dBoatRenderer ren, StringBuilder log)
        {
            const int W = 1000, H = 700;
            var canvas = new Color32[W * H];
            string art = Path.Combine(RigCatalog.RepoRoot, "Assets/_Project/Art");

            Tile(canvas, W, H, Load(art + "/Tilesets/Water/SeaTile.png"), 0, 0, W, H,
                 new Color32(28, 58, 74, 255));
            Tile(canvas, W, H, Load(art + "/Tilesets/Grass.png"), 0, 0, W, 128, new Color32(58, 82, 52, 255));
            Tile(canvas, W, H, Load(art + "/Tilesets/Sand.png"), 0, 128, W, 64, new Color32(160, 148, 116, 255));
            Tile(canvas, W, H, Load(art + "/Tilesets/ShoreEdge.png"), 0, 176, W, 32, default);
            Tile(canvas, W, H, Load(art + "/Tilesets/WharfDeck.png"), 660, 120, 320, 128, default);

            // sprite props, painter-ordered back to front
            Blit(canvas, W, H, Load(art + "/Sprites/Buildings/LighthouseIso.png"), 40, -260);
            Blit(canvas, W, H, Load(art + "/Sprites/Buildings/CottageIso.png"), 330, -128);
            Blit(canvas, W, H, Load(art + "/Sprites/Shore/RockCluster.png"), 170, 150);
            Blit(canvas, W, H, Load(art + "/Sprites/GrassTuft.png"), 250, 110);
            Blit(canvas, W, H, Load(art + "/Sprites/WharfPost.png"), 690, 232);
            Blit(canvas, W, H, Load(art + "/Sprites/WharfPost.png"), 930, 232);

            // THE COMPARISON: the baked sprite hull and the real-time 3D hull, side by side, in the
            // same frame, at the same heading, among the same sprite art. One of these two is a
            // 117 MB sheet lookup and the other is 1,384 triangles. Which is which is the finding.
            var sheet = Load(art + "/Boats/LobsterBoatIso.png");
            const int cell = 20;
            if (sheet != null)
                BlitCell(canvas, W, H, sheet, cell % 8, cell / 8, rig.W, rig.H, 10, 250);
            var hull3d = ren.RenderCell(RigBaker.DirForCell(cell, 32, AzimuthConvention.CounterClockwise), out _);
            BlitRaw(canvas, W, H, hull3d, rig.W, rig.H, 520, 250);

            // sorting probe: a buoy drawn AFTER the hulls (so it reads in front) and one drawn
            // before would read behind. Whole-object painter order is all a sprite pipeline has.
            Blit(canvas, W, H, Load(art + "/Sprites/LobsterBuoy.png"), 905, 560);
            Blit(canvas, W, H, Load(art + "/Sprites/LobsterBuoy.png"), 400, 560);

            WritePng(Path.Combine(OutDir, "3-in-scene.png"), canvas, W, H, 2);
            log.AppendLine("\n-- 3-in-scene.png: left = baked sprite hull, right = real-time 3D hull, " +
                           "same heading, same sprite world. Buoys probe sorting in front/behind.");
        }

        // ------------------------------------------------------------------------ memory report
        static void MemoryReport(RigMeshData rig, Spike3dBoatRenderer ren, StringBuilder log)
        {
            double cellMb = rig.W * rig.H * 4 / 1048576.0;
            double sheet32x4 = cellMb * 32 * 4;
            // pos(12) + normal(12) + uv4(16) = 40 B/vertex, + 4 B/index
            double meshKb = (ren.Vertices * 40 + ren.Triangles * 3 * 4) / 1024.0;
            log.AppendLine($"\n-- memory --\ncell {rig.W}x{rig.H} RGBA32 = {cellMb:0.00} MB");
            log.AppendLine($"32 dir x 4 rock sheet (what she ships today) = {sheet32x4:0.0} MB uncompressed");
            log.AppendLine($"mesh = {meshKb:0.0} KB ({ren.Vertices} verts / {ren.Triangles} tris) " +
                           $"= {sheet32x4 * 1024 / meshKb:0} x smaller");
        }

        // ------------------------------------------------------------------------------ helpers
        static Color32[] ToPixels(byte[] rgba, int w, int h)
        {
            var px = new Color32[w * h];
            for (int i = 0; i < px.Length; i++)
                px[i] = new Color32(rgba[i * 4], rgba[i * 4 + 1], rgba[i * 4 + 2], rgba[i * 4 + 3]);
            return px;
        }

        static Texture2D Load(string path)
        {
            if (!File.Exists(path)) { Debug.LogWarning("[3D SPIKE] missing art: " + path); return null; }
            var t = new Texture2D(2, 2, TextureFormat.RGBA32, false, true) { filterMode = FilterMode.Point };
            t.LoadImage(File.ReadAllBytes(path));   // straight off disk: no import settings in the way
            return t;
        }

        /// <summary>Top-left-origin blit of a Texture2D (which is bottom-left) onto the canvas.</summary>
        static void Blit(Color32[] dst, int dw, int dh, Texture2D src, int x0, int y0)
        {
            if (src == null) return;
            var sp = src.GetPixels32();
            for (int y = 0; y < src.height; y++)
                for (int x = 0; x < src.width; x++)
                {
                    var c = sp[(src.height - 1 - y) * src.width + x];
                    if (c.a == 0) continue;
                    int X = x0 + x, Y = y0 + y;
                    if (X < 0 || Y < 0 || X >= dw || Y >= dh) continue;
                    dst[Y * dw + X] = c;
                }
        }

        static void BlitRaw(Color32[] dst, int dw, int dh, Color32[] src, int sw, int sh, int x0, int y0)
        {
            for (int y = 0; y < sh; y++)
                for (int x = 0; x < sw; x++)
                {
                    var c = src[y * sw + x];
                    if (c.a == 0) continue;
                    int X = x0 + x, Y = y0 + y;
                    if (X < 0 || Y < 0 || X >= dw || Y >= dh) continue;
                    dst[Y * dw + X] = c;
                }
        }

        static void BlitCell(Color32[] dst, int dw, int dh, Texture2D sheet, int col, int rowFromTop,
                             int cw, int ch, int x0, int y0)
        {
            var sp = sheet.GetPixels32();
            for (int y = 0; y < ch; y++)
                for (int x = 0; x < cw; x++)
                {
                    int sx = col * cw + x, sy = rowFromTop * ch + y;
                    var c = sp[(sheet.height - 1 - sy) * sheet.width + sx];
                    if (c.a == 0) continue;
                    int X = x0 + x, Y = y0 + y;
                    if (X < 0 || Y < 0 || X >= dw || Y >= dh) continue;
                    dst[Y * dw + X] = c;
                }
        }

        static void Tile(Color32[] dst, int dw, int dh, Texture2D src, int x0, int y0, int w, int h,
                         Color32 fallback)
        {
            if (src == null)
            {
                if (fallback.a == 0) return;
                for (int y = y0; y < y0 + h && y < dh; y++)
                    for (int x = x0; x < x0 + w && x < dw; x++)
                        if (x >= 0 && y >= 0) dst[y * dw + x] = fallback;
                return;
            }
            for (int y = y0; y < y0 + h; y += src.height)
                for (int x = x0; x < x0 + w; x += src.width)
                    Blit(dst, dw, dh, src, x, y);
        }

        static Color32[] Row(IList<Color32[]> cells, int w, int h)
        {
            var o = new Color32[w * cells.Count * h];
            for (int c = 0; c < cells.Count; c++)
                for (int y = 0; y < h; y++)
                    Array.Copy(cells[c], y * w, o, y * w * cells.Count + c * w, w);
            return o;
        }

        static Color32[] Grid(IList<Color32[]> cells, int cols, int w, int h)
        {
            int rows = Mathf.CeilToInt(cells.Count / (float)cols);
            var o = new Color32[w * cols * h * rows];
            for (int i = 0; i < cells.Count; i++)
            {
                int cx = (i % cols) * w, cy = (i / cols) * h;
                for (int y = 0; y < h; y++)
                    Array.Copy(cells[i], y * w, o, (cy + y) * w * cols + cx, w);
            }
            return o;
        }

        static Color32[] Stack(IList<Color32[]> rows, int w, int h)
        {
            var o = new Color32[w * h * rows.Count];
            for (int r = 0; r < rows.Count; r++) Array.Copy(rows[r], 0, o, r * w * h, w * h);
            return o;
        }

        /// <summary>Writes top-left-origin pixels as a PNG, optionally nearest-neighbour upscaled.</summary>
        static void WritePng(string path, Color32[] px, int w, int h, int scale, bool quiet = false)
        {
            var tex = new Texture2D(w * scale, h * scale, TextureFormat.RGBA32, false, true);
            var o = new Color32[w * scale * h * scale];
            for (int y = 0; y < h * scale; y++)
                for (int x = 0; x < w * scale; x++)
                    o[(h * scale - 1 - y) * w * scale + x] = px[(y / scale) * w + (x / scale)];
            tex.SetPixels32(o);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            if (!quiet) Debug.Log($"[3D SPIKE] wrote {path} ({w * scale}x{h * scale})");
        }
    }
}
