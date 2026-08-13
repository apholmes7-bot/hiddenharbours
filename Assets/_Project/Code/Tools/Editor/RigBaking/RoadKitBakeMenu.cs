using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Art.Editor;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>
    /// Owner-facing entry points for the ROAD KIT v3: bake the eleven atlases, slice them, and render
    /// the adjacency proof sheets.
    ///
    /// <para><b>Priorities 153–155</b> continue the Dev group's unbroken run (the last item before
    /// these is Verify Shop Fixture Slices at 152). Unity draws a separator at any priority gap over
    /// 10, so an item numbered out of its neighbourhood renders in the wrong group and reads as
    /// missing — check the neighbours before anything else if one of these "doesn't appear".</para>
    /// </summary>
    public static class RoadKitBakeMenu
    {
        [MenuItem("Hidden Harbours/Dev/Bake Road Kit v3 (11 surfaces × 47 blob tiles)", priority = 153)]
        public static void Bake() => RunBake(fromCommandLine: false);

        /// <summary>Batch entry point for <c>-executeMethod</c>.</summary>
        public static void BakeFromCommandLine()
        {
            try { RunBake(fromCommandLine: true); }
            catch (Exception e)
            {
                Debug.LogError($"[rig-baker] batch road kit bake threw: {e}");
                EditorApplication.Exit(1);
            }
        }

        static void RunBake(bool fromCommandLine)
        {
            RoadKitBakeResult result;
            try
            {
                result = RoadKitSheetBaker.BakeAll(progress: (label, t) =>
                {
                    if (!fromCommandLine)
                        EditorUtility.DisplayProgressBar("Baking road kit v3", label, t);
                });
            }
            catch (Exception ex)
            {
                Debug.LogError($"[rig-baker] road kit bake FAILED: {ex.Message}\n{ex}");
                throw;
            }
            finally
            {
                if (!fromCommandLine) EditorUtility.ClearProgressBar();
            }

            var sb = new StringBuilder();
            sb.AppendLine("[rig-baker] Road kit v3 bake:");
            foreach (var s in result.Sheets) sb.AppendLine("  " + s);
            sb.AppendLine($"  {result.Sheets.Count} sheet(s) rendered in {result.RenderMs:F0} ms " +
                          $"(total {result.TotalMs:F0} ms), {result.TotalPngBytes / 1024.0:F0} KiB of PNG.");
            sb.Append($"  contract → {result.ContractPath}");
            Debug.Log(sb.ToString());

            AssetDatabase.Refresh();

            // ⚠️ Re-import from disk BEFORE anything reads a texture: the PNGs were written behind the
            // AssetDatabase's back, and a mid-build import invalidates any reference taken before it.
            foreach (var s in result.Sheets)
                AssetDatabase.ImportAsset(s.AssetPath, ImportAssetOptions.ForceUpdate);

            RoadKitContract.Invalidate();
            Slice();
        }

        [MenuItem("Hidden Harbours/Dev/Slice Road Kit v3 (47 tops + 47 skirts per sheet)", priority = 154)]
        public static void Slice()
        {
            var written = RoadKitSlicer.SliceAll();
            var (found, expected, missing) = RoadKitSlicer.VerifyAll();

            var sb = new StringBuilder();
            sb.AppendLine($"[rig-baker] Road kit v3 sliced: {written.Count}/" +
                          $"{RoadKitV3.Surfaces.Length} sheets, " +
                          $"{written.Values.Sum()} sprite rects written.");
            sb.Append($"  verified {found}/{expected} sprites resolve back off the imported sheets " +
                      $"(2 per cell — {RoadKitV3.TopSuffix} + {RoadKitV3.SkirtSuffix}).");
            if (missing.Count > 0)
                sb.Append($"\n  ⚠️ {missing.Count} missing, first few: " +
                          string.Join(", ", missing.Take(6)));

            if (missing.Count > 0) Debug.LogWarning(sb.ToString()); else Debug.Log(sb.ToString());
        }

        [MenuItem("Hidden Harbours/Dev/Road Kit v3 — adjacency proof sheets", priority = 155)]
        public static void ProofSheets() => RunProof(fromCommandLine: false);

        /// <summary>Batch entry point for <c>-executeMethod</c>.</summary>
        public static void ProofSheetsFromCommandLine()
        {
            try { RunProof(fromCommandLine: true); }
            catch (Exception e)
            {
                Debug.LogError($"[rig-baker] batch road proof sheets threw: {e}");
                EditorApplication.Exit(1);
            }
        }

        /// <summary>
        /// The surfaces the proof sheets are rendered for. Not all eleven: the proof is about the
        /// blob INDEXING, which is surface-independent, so one soft surface and one hard one with a
        /// kerb is the honest coverage — and saying so beats implying eleven were graded.
        /// </summary>
        static readonly string[] ProofSurfaces = { "gravel", "asphalt" };

        static void RunProof(bool fromCommandLine)
        {
            string outDir = Path.Combine(RigCatalog.RepoRoot, RoadKitProofSheet.OutputFolder);
            Directory.CreateDirectory(outDir);

            var sb = new StringBuilder();
            sb.AppendLine("[rig-baker] Road kit v3 adjacency proof:");

            using (IRigScriptHost host = new V8RigScriptHost())
            {
                RoadKitSheetBaker.LoadRig(host);
                RoadKitSheetBaker.AssertRigGeometry(host);

                foreach (string surface in ProofSurfaces)
                {
                    Write($"maskgrid_{surface}",
                          RoadKitProofSheet.RenderMaskGrid(host, surface, out int gw, out int gh),
                          gw, gh, sb);
                    Write($"network_{surface}",
                          RoadKitProofSheet.RenderNetwork(host, surface, out int nw, out int nh),
                          nw, nh, sb);
                }
            }

            sb.Append("  " + RoadKitProofSheet.CoverageReport());
            Debug.Log(sb.ToString());
            if (!fromCommandLine) AssetDatabase.Refresh();
        }

        static void Write(string name, byte[] rgba, int w, int h, StringBuilder sb)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, mipChain: false, linear: false);
            try
            {
                tex.LoadRawTextureData(RoadKitSheetBaker.FlipRows(rgba, w, h));
                tex.Apply(false, false);
                byte[] png = tex.EncodeToPNG();
                string rel = $"{RoadKitProofSheet.OutputFolder}/{name}.png";
                File.WriteAllBytes(Path.Combine(RigCatalog.RepoRoot, rel), png);
                sb.AppendLine($"  {rel}  {w}×{h}  {png.Length / 1024.0:F1} KiB");
            }
            finally { UnityEngine.Object.DestroyImmediate(tex); }
        }
    }
}
