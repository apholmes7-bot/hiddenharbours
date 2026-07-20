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
            const int W = 960, H = 560, PX = 32;
            var canvas = new Color32[W * H];
            string art = Path.Combine(RigCatalog.RepoRoot, "Assets/_Project/Art");

            Tile(canvas, W, H, Load(art + "/Tilesets/Water/WaterDeep.png") ?? Load(art + "/Tilesets/Sand.png"),
                 0, 0, W, H, new Color32(28, 58, 74, 255));
            Tile(canvas, W, H, Load(art + "/Tilesets/Grass.png"), 0, 0, W, 150, new Color32(58, 82, 52, 255));
            Tile(canvas, W, H, Load(art + "/Tilesets/Sand.png"), 0, 150, W, 40, new Color32(160, 148, 116, 255));
            Tile(canvas, W, H, Load(art + "/Tilesets/ShoreEdge.png"), 0, 182, W, 32, default);
            Tile(canvas, W, H, Load(art + "/Tilesets/WharfDeck.png"), 600, 150, 260, 96, default);

            // sprite props, painter-ordered back to front
            Blit(canvas, W, H, Load(art + "/Sprites/Buildings/LighthouseIso.png"), 40, -240);
            Blit(canvas, W, H, Load(art + "/Sprites/Buildings/CottageIso.png"), 300, -100);
            Blit(canvas, W, H, Load(art + "/Sprites/Shore/RockCluster.png"), 170, 150);
            Blit(canvas, W, H, Load(art + "/Sprites/GrassTuft.png"), 250, 130);
            Blit(canvas, W, H, Load(art + "/Sprites/WharfPost.png"), 620, 214);
            Blit(canvas, W, H, Load(art + "/Sprites/WharfPost.png"), 810, 214);

            // THE COMPARISON: the baked sprite hull and the real-time 3D hull, side by side, in the
            // same frame, at the same heading, among the same sprite art.
            var sheet = Load(art + "/Boats/LobsterBoatIso.png");
            const int cell = 20;
            if (sheet != null)
                BlitCell(canvas, W, H, sheet, cell % 8, cell / 8, rig.W, rig.H, 40, 250);
            var hull3d = ren.RenderCell(RigBaker.DirForCell(cell, 32, AzimuthConvention.CounterClockwise), out _);
            BlitRaw(canvas, W, H, hull3d, rig.W, rig.H, 470, 250);

            // sorting probe: a buoy that should read as IN FRONT of the 3D hull, and one BEHIND it
            Blit(canvas, W, H, Load(art + "/Sprites/LobsterBuoy.png"), 690, 470);
            Blit(canvas, W, H, Load(art + "/Sprites/LobsterBuoy.png"), 700, 330);

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
        static void WritePng(string path, Color32[] px, int w, int h, int scale)
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
            Debug.Log($"[3D SPIKE] wrote {path} ({w * scale}x{h * scale})");
        }
    }
}
