// ⚠️⚠️ THROWAWAY SPIKE CODE — spike/3d-boats. NOT A PIPELINE. NOT FOR MERGE AS-IS. ⚠️⚠️
//
// The SIDE DRAGGER battery. The lobster boat answered "does 3D look like the sprite" and "does it
// survive motion". She cannot answer "does that still hold on a 25 m hull", because the whole risk
// on a big hull is PANEL SIZE: flat facets quantised to a 5–7 entry ramp mean a whole-shade-step
// jump spans proportionally more screen area the larger the uninterrupted panel. Her cream lower
// house, wheelhouse roof and long open working deck are the largest flat areas in the kit.
//
// Ground truth here is the rig's OWN software render, not a baked sheet — she has no sheet in the
// repo, and a baked sheet is only ever render() written to a PNG, so this is the purer comparison.
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
    public static class Spike3dDragger
    {
        const string ScriptPath = "docs/art/rigs/sideDraggerIsoRig.js";
        const string Global = "SideDraggerIso";
        const int Facings = 32;

        static string OutDir =>
            Environment.GetEnvironmentVariable("HH_SPIKE_OUT") ??
            Path.Combine(Path.GetTempPath(), "hh3dspike");

        static string FramePath(string set, int f) =>
            Path.Combine(OutDir, "frames", set, f.ToString("D4") + ".png");

        [MenuItem("Hidden Harbours/Spikes/3D Boats — SIDE DRAGGER battery")]
        public static void Run()
        {
            Directory.CreateDirectory(OutDir);
            var log = new StringBuilder();
            try
            {
                using var host = new V8RigScriptHost();
                var rig = RigMeshExtractor.ExtractFrom(host, ScriptPath, Global);
                log.AppendLine($"engine: {host.EngineName}");
                log.AppendLine($"SIDE DRAGGER: faces={rig.Faces.Count} mats={rig.Materials.Count} " +
                               $"cell={rig.W}x{rig.H} px/m={rig.PxPerMetre} elev={rig.DefaultElev} " +
                               $"pivot=({rig.PivotX},{rig.PivotY})");

                using var ren = new Spike3dBoatRenderer(rig, rig.DefaultElev);
                log.AppendLine($"mesh: {ren.Vertices} verts, {ren.Triangles} tris");

                double cellMb = rig.W * rig.H * 4 / 1048576.0;
                double meshKb = (ren.Vertices * 40 + ren.Triangles * 3 * 4) / 1024.0;
                log.AppendLine($"\n-- memory --");
                log.AppendLine($"cell {rig.W}x{rig.H} RGBA32 = {cellMb:0.00} MB");
                log.AppendLine($"32 facings           = {cellMb * 32:0.0} MB uncompressed");
                log.AppendLine($"64 facings           = {cellMb * 64:0.0} MB uncompressed");
                log.AppendLine($"32 dir x 4 rock      = {cellMb * 128:0.0} MB uncompressed");
                log.AppendLine($"mesh = {meshKb:0.0} KB ({ren.Vertices} verts / {ren.Triangles} tris)");
                log.AppendLine($"  vs 32 facings: {cellMb * 32 * 1024 / meshKb:0} x smaller");
                log.AppendLine($"  vs 64 facings: {cellMb * 64 * 1024 / meshKb:0} x smaller");

                Spike3dBoatMenu.CalibrateDepthPublic(host, rig, ren, log, Global);
                Vector4 calib = ren.DitherPhase;

                // ---------- 0. the 32 "baked" facings, rendered once and cached ----------
                // This IS the sheet: the baker does exactly this and writes the bytes to a PNG.
                var sheet = new Color32[Facings][];
                var swAll = System.Diagnostics.Stopwatch.StartNew();
                for (int k = 0; k < Facings; k++)
                {
                    double d = RigBaker.DirForCell(k, Facings, AzimuthConvention.CounterClockwise);
                    sheet[k] = ToPixels(host.EvaluateBytes(
                        $"{Global}.render({d.ToString("R", CultureInfo.InvariantCulture)})"), rig.W, rig.H);
                }
                log.AppendLine($"\nrendered {Facings} ground-truth facings via V8 in " +
                               $"{swAll.Elapsed.TotalSeconds:0.0} s " +
                               $"({swAll.Elapsed.TotalMilliseconds / Facings:0} ms/facing)");

                // ---------- 1. still A/B, mesh vs the rig's own render ----------
                log.AppendLine("\n-- A/B: rig software render vs real-time 3D mesh --");
                int[] cells = { 0, 4, 8, 12, 20, 28 };
                var strips = new List<Color32[]>();
                foreach (int cell in cells)
                {
                    double dir = RigBaker.DirForCell(cell, Facings, AzimuthConvention.CounterClockwise);
                    var truth = sheet[cell];
                    var mine = ren.RenderCell(dir, out double ms);
                    int diff = 0, inked = 0; long err = 0;
                    var diffImg = new Color32[rig.W * rig.H];
                    for (int i = 0; i < truth.Length; i++)
                    {
                        bool a = truth[i].a > 0, b = mine[i].a > 0;
                        if (a || b) inked++;
                        int e = Mathf.Abs(truth[i].r - mine[i].r) + Mathf.Abs(truth[i].g - mine[i].g)
                              + Mathf.Abs(truth[i].b - mine[i].b) + (a == b ? 0 : 255);
                        if (e > 0) { diff++; err += e; }
                        diffImg[i] = e == 0 ? (a ? new Color32(24, 28, 32, 255) : new Color32(0, 0, 0, 0))
                                            : new Color32(255, (byte)Mathf.Max(0, 200 - e), 0, 255);
                    }
                    log.AppendLine($"cell {cell,2} (heading {cell * 360 / Facings,3}°) " +
                                   $"differing px {diff,7} / {inked,7} inked = " +
                                   $"{100f * diff / Mathf.Max(1, inked),5:0.00}%  gpu {ms:0.0} ms");
                    strips.Add(Row(new[] { truth, mine, diffImg }, rig.W, rig.H));
                }
                WritePng(Path.Combine(OutDir, "6-dragger-ab.png"),
                         Stack(strips, rig.W * 3, rig.H), rig.W * 3, rig.H * cells.Length);

                // ---------- 2. dither crawl at her scale ----------
                const int Fps = 30, NF = 40;
                const float SpeedMps = 4.24f;
                float pxPerFrame = SpeedMps * rig.PxPerMetre / Fps;
                int pvX = Mathf.RoundToInt(rig.PivotX), pvY = Mathf.RoundToInt(rig.PivotY);
                int CW = rig.W + 260, CH = rig.H;
                int startX = pvX + 8;
                const int TCell = 8;                       // broadside: house + working deck face us
                double dirT = RigBaker.DirForCell(TCell, Facings, AzimuthConvention.CounterClockwise);
                foreach (string d in new[] { "dr_before", "dr_after", "dr_sprite" })
                    Directory.CreateDirectory(Path.Combine(OutDir, "frames", d));
                var track = new StringBuilder("frame,pivotX\n");
                log.AppendLine($"\n-- translation: {SpeedMps} m/s = {pxPerFrame:0.000} px/frame, {NF} frames --");

                for (int f = 0; f < NF; f++)
                {
                    int px = startX + Mathf.RoundToInt(f * pxPerFrame);
                    ren.DitherPhase = calib;                                     // naive, screen-pinned
                    WritePng(FramePath("dr_before", f),
                             ren.RenderAt(dirT, CW, CH, new Vector2(px, pvY), out _), CW, CH);
                    ren.DitherPhase = new Vector4(Mod4(calib.x + rig.PivotX - px),
                                                  Mod4(calib.y + rig.PivotY - pvY), calib.z, 0);
                    WritePng(FramePath("dr_after", f),
                             ren.RenderAt(dirT, CW, CH, new Vector2(px, pvY), out _), CW, CH);
                    var canvas = new Color32[CW * CH];                            // baked control
                    BlitRaw(canvas, CW, CH, sheet[TCell], rig.W, rig.H, px - pvX, pvY - pvY);
                    WritePng(FramePath("dr_sprite", f), canvas, CW, CH);
                    track.Append($"{f},{px}\n");
                }
                File.WriteAllText(Path.Combine(OutDir, "frames", "dr_track.csv"), track.ToString());

                // ---------- 3. rotation: continuous mesh vs 32-facing sheet ----------
                const int RF = 120;
                foreach (string d in new[] { "dr_rot_mesh", "dr_rot_sprite" })
                    Directory.CreateDirectory(Path.Combine(OutDir, "frames", d));
                ren.DitherPhase = calib;
                log.AppendLine($"-- rotation: 360° over {RF} frames = {360.0 / RF:0.0}°/frame --");
                for (int f = 0; f < RF; f++)
                {
                    double heading = f * 360.0 / RF;
                    WritePng(FramePath("dr_rot_mesh", f),
                             ren.RenderCell(8.0 - heading / 45.0, out _), rig.W, rig.H);
                    int k = Mathf.RoundToInt((float)(heading / (360.0 / Facings))) % Facings;
                    WritePng(FramePath("dr_rot_sprite", f), sheet[k], rig.W, rig.H);
                }
                log.AppendLine("frames written; assemble with make-dragger-gifs.py");
            }
            catch (Exception e) { log.AppendLine("FAILED: " + e); }
            File.WriteAllText(Path.Combine(OutDir, "dragger-log.txt"), log.ToString());
            Debug.Log("[3D SPIKE DRAGGER]\n" + log);
        }

        static float Mod4(float v) => ((v % 4f) + 4f) % 4f;

        static Color32[] ToPixels(byte[] rgba, int w, int h)
        {
            var px = new Color32[w * h];
            for (int i = 0; i < px.Length; i++)
                px[i] = new Color32(rgba[i * 4], rgba[i * 4 + 1], rgba[i * 4 + 2], rgba[i * 4 + 3]);
            return px;
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

        static Color32[] Row(IList<Color32[]> cells, int w, int h)
        {
            var o = new Color32[w * cells.Count * h];
            for (int c = 0; c < cells.Count; c++)
                for (int y = 0; y < h; y++)
                    Array.Copy(cells[c], y * w, o, y * w * cells.Count + c * w, w);
            return o;
        }

        static Color32[] Stack(IList<Color32[]> rows, int w, int h)
        {
            var o = new Color32[w * h * rows.Count];
            for (int r = 0; r < rows.Count; r++) Array.Copy(rows[r], 0, o, r * w * h, w * h);
            return o;
        }

        static void WritePng(string path, Color32[] px, int w, int h)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false, true);
            var o = new Color32[w * h];
            for (int y = 0; y < h; y++)                 // top-left origin -> Texture2D bottom-left
                Array.Copy(px, y * w, o, (h - 1 - y) * w, w);
            tex.SetPixels32(o);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
        }
    }
}
