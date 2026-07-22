using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using HiddenHarbours.Tools.RigBaking;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// <b>ADR 0022 phase 4 acceptance: the lobster boat's MESH render vs her own BAKED SHEET, at
    /// matching headings, through the committed data path.</b>
    ///
    /// <para>Phase 3 proved the URP pass reproduces the CPU oracle from a freshly-extracted mesh.
    /// This fixture proves the two things phase 4 added on top, using the assets the GAME loads:
    /// the committed <see cref="HullMeshDef"/> (ramps, mesh, dither — the baked format), and the
    /// compass→dir HEADING MAPPING through the def's MEASURED azimuth flag
    /// (<see cref="HullMeshMath.HeadingToDirUnits"/>). The truth it compares against is the baked
    /// 32-facing sheet — the picture the sprite path shows for the same compass heading — so a wrong
    /// mapping sign (the CCW saga's defect class), a stale committed def, or a broken cell framing
    /// all land as pixels, not as green ticks.</para>
    ///
    /// <para><b>Noise floor.</b> Same metric and reasoning as IsoFacetUrpPassTests: connected
    /// cluster size, floors MEASURED (D3D11, 2026-07-22), then pinned:
    /// <code>
    ///   cell  0 (0°,      cardinal)   2.58%  cluster 114
    ///   cell  8 (90°,     cardinal)   3.04%  cluster 253   ← beam-on, longest straight edges
    ///   cell 16 (180°,    cardinal)   2.38%  cluster 116
    ///   cell 24 (270°,    cardinal)   3.04%  cluster 253
    ///   cell  3 (33.75°,  fractional) 2.55%  cluster  51
    ///   cell 13 (146.25°, fractional) 2.59%  cluster  50
    ///   cell 22 (247.5°,  fractional) 2.71%  cluster 112   ← the 22.5° multiple still runs straightish edges
    ///   cell 29 (326.25°, fractional) 2.53%  cluster  51
    /// </code>
    /// All of it is the ADR's facet-/dither-boundary single-step class (percentages inside the
    /// spike's 1.3–4.4% band). Cardinal floor 300 (shared with the phase-3 fixture); fractional
    /// floor 150, a third above its worst measurement and TWO-TO-THREE ORDERS below the
    /// flipped-azimuth sabotage (measured 44,822 at East). CI has no GPU: every test gates on
    /// <see cref="RequireAGraphicsDevice"/> and skips loudly.</para>
    /// </summary>
    public class IsoFacetLobsterEndToEndTests
    {
        const string HullMeshAssetPath = "Assets/_Project/Data/Boats/HullMeshes/LobsterBoatIsoHullMesh.asset";
        const string SheetPath = "Assets/_Project/Art/Boats/LobsterBoatIso.png";
        const int ProbeLayer = 31;

        /// <summary>Cardinal floor — long axis-aligned edges, the measured worst class (see doc).</summary>
        const int MaxCardinalNoiseCluster = 300;
        /// <summary>Fractional-heading floor (see doc — measured 50–112, sabotage 44,822).</summary>
        const int MaxFractionalNoiseCluster = 150;
        /// <summary>Whole-cell backstop, comparability with ADR 0022's 1.3–4.4% band.</summary>
        const double MaxPercent = 5.0;

        static void RequireAGraphicsDevice()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                Assert.Ignore(
                    "SKIPPED, NOT VERIFIED — this run has no graphics device (Renderer: Null Device), " +
                    "so the mesh-vs-sheet acceptance could not render and proved nothing. Expected on " +
                    "CI; run on a machine with a GPU to actually verify phase 4.");
            }
        }

        static HullMeshDef LoadDef()
        {
            var def = AssetDatabase.LoadAssetAtPath<HullMeshDef>(HullMeshAssetPath);
            Assert.IsNotNull(def, $"missing {HullMeshAssetPath} — bake it first (3D Hulls menu)");
            Assert.IsTrue(def.IsUsable(), "the committed def must be usable");
            return def;
        }

        static Sprite[] LoadSheet(HullMeshDef def)
        {
            var sprites = AssetDatabase.LoadAllAssetsAtPath(SheetPath).OfType<Sprite>()
                                       .OrderBy(SpriteIndex).ToArray();
            Assert.AreEqual(32, sprites.Length, "her sheet slices 32 facings");
            Assert.AreEqual(def.CellW, (int)sprites[0].rect.width,
                "sheet cell ≠ def cell — a downscaled import (the >2048 max-size trap) or a stale bake; " +
                "this comparison would be meaningless");
            return sprites;
        }

        static int SpriteIndex(Sprite s)
        {
            int u = s.name.LastIndexOf('_');
            return (u >= 0 && int.TryParse(s.name.Substring(u + 1), out int n)) ? n : 0;
        }

        // ------------------------------------------------------------------ the acceptance

        [Test]
        public void MeshRender_MatchesHerBakedSheet_AtMatchingHeadings()
        {
            RequireAGraphicsDevice();
            var def = LoadDef();
            var sheet = LoadSheet(def);

            // Cells of the 32-facing sheet: heading = cell·11.25° (her sheet is TRUE clockwise —
            // the sprite baker corrected at bake time, LobsterBoatFacingTests guards it). Cardinals
            // and deliberate fractionals, including diagonals-adjacent cells where the old 8-way
            // stepping was worst.
            var cardinals = new[] { 0, 8, 16, 24 };
            var fractionals = new[] { 3, 13, 22, 29 };

            var report = new System.Text.StringBuilder();
            int worstCardinal = 0, worstFractional = 0;
            double worstPercent = 0;

            foreach (int cell in cardinals.Concat(fractionals))
            {
                float headingDeg = cell * (360f / 32f);
                byte[] mesh = RenderMesh(def, headingDeg);
                byte[] sprite = RenderSprite(def, sheet[cell]);
                var diff = RigMeshReferenceRasterizer.Compare(sprite, mesh, def.CellW, def.CellH);
                bool cardinal = cardinals.Contains(cell);
                report.AppendLine($"  cell {cell} (heading {headingDeg:0.##}°, {(cardinal ? "cardinal" : "fractional")}): {diff}");
                Debug.Log($"[lobster-e2e] cell {cell} @ {headingDeg:0.##}°: {diff}");
                if (cardinal) worstCardinal = Math.Max(worstCardinal, diff.LargestDifferingCluster);
                else worstFractional = Math.Max(worstFractional, diff.LargestDifferingCluster);
                worstPercent = Math.Max(worstPercent, diff.PercentDiffering);
            }

            Assert.LessOrEqual(worstCardinal, MaxCardinalNoiseCluster,
                $"mesh vs baked sheet diverged beyond the cardinal noise floor:\n{report}A patch this " +
                "size is a real defect — in the committed def, the heading mapping, or the framing.");
            Assert.LessOrEqual(worstFractional, MaxFractionalNoiseCluster,
                $"mesh vs baked sheet diverged beyond the fractional-heading noise floor:\n{report}");
            Assert.Less(worstPercent, MaxPercent, $"whole-cell backstop:\n{report}");
        }

        // ------------------------------------------------------------------ SABOTAGE

        /// <summary>
        /// ⚠️ The phase-4-shaped defect: the mapping SIGN. Feed the drawn heading through the WRONG
        /// convention (as if the rig were clockwise) and the comparison must explode — this is the
        /// exact mistake "tidying up the minus sign" (or trusting the rig's declared order over the
        /// measurement) would ship, and it has shipped five times in sprite form.
        /// </summary>
        [Test]
        public void Sabotage_FlippedAzimuthMapping_IsCaught()
        {
            RequireAGraphicsDevice();
            var def = LoadDef();
            var sheet = LoadSheet(def);

            const int cell = 8;   // East — where the mirror is a full 180° of drawn heading
            float headingDeg = cell * (360f / 32f);
            byte[] mesh = RenderMesh(def, headingDeg, flipAzimuth: true);
            byte[] sprite = RenderSprite(def, sheet[cell]);
            var diff = RigMeshReferenceRasterizer.Compare(sprite, mesh, def.CellW, def.CellH);
            Debug.Log($"[lobster-e2e][SABOTAGE] flipped azimuth mapping @ {headingDeg}°: {diff}");

            Assert.Greater(diff.LargestDifferingCluster, MaxCardinalNoiseCluster,
                "SABOTAGE NOT DETECTED — a mirrored heading mapping stayed under the noise floor. " +
                "The acceptance cannot see the CCW defect class and every green run above is " +
                "worth less than it looks.");
        }

        // ------------------------------------------------------------------ plumbing

        /// <summary>Render the committed def through the REAL production components — driver-level
        /// mapping included — into a cell-sized readback. Scenes are sequential, never simultaneous
        /// (two live hulls photobomb one renderer list — phase 3's lesson).</summary>
        static byte[] RenderMesh(HullMeshDef def, float headingDeg, bool flipAzimuth = false)
        {
            var go = new GameObject("E2EHull") { layer = ProbeLayer };
            try
            {
                var renderer = go.AddComponent<IsoFacetHullRenderer>();
                renderer.Configure(IsoFacetHullPresentationService.ToSetup(def));
                bool ccw = flipAzimuth ? !def.AzimuthCounterClockwise : def.AzimuthCounterClockwise;
                renderer.HeadingDirUnits = HullMeshMath.HeadingToDirUnits(headingDeg, 0f, ccw);
                renderer.ApplyPose();
                SetLayerRecursive(go.transform, ProbeLayer);
                return RenderCell(def, go);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        /// <summary>Render one baked sheet cell through a SpriteRenderer at the shared pivot — the
        /// picture the sprite path presents for this heading, pixel-for-pixel.</summary>
        static byte[] RenderSprite(HullMeshDef def, Sprite cell)
        {
            var go = new GameObject("E2ESprite") { layer = ProbeLayer };
            try
            {
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = cell;
                // The project's DEFAULT sprite material is LIT and this scene has no Light2D — a lit
                // sprite renders black and fakes a mismatch. The pixels are identical unlit.
                var unlit = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                Assert.IsNotNull(unlit, "URP's Sprite-Unlit-Default shader is missing?");
                sr.sharedMaterial = new Material(unlit);
                return RenderCell(def, go);
            }
            finally
            {
                var mat = go.GetComponent<SpriteRenderer>().sharedMaterial;
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(mat);
            }
        }

        /// <summary>The spike's framing: an ortho camera sized to the cell, offset so the rig pivot
        /// lands on its exact cell pixel; readback flipped to the rig's top-left orientation. Waits
        /// out shader compilation (the cold-cache trap fakes exactly the regressions we hunt).</summary>
        static byte[] RenderCell(HullMeshDef def, GameObject subject)
        {
            float ppu = def.PxPerMetre;
            float ox = (def.PivotPx.x - def.CellW / 2f) / ppu;
            float oy = (def.CellH / 2f - def.PivotPx.y) / ppu;

            var camGo = new GameObject("E2ECam");
            var rt = new RenderTexture(def.CellW, def.CellH, 24, RenderTextureFormat.ARGB32)
            {
                filterMode = FilterMode.Point,
            };
            try
            {
                var cam = camGo.AddComponent<Camera>();
                cam.orthographic = true;
                cam.orthographicSize = def.CellH / (2f * ppu);
                cam.transform.position = new Vector3(-ox, -oy, -100f);
                cam.nearClipPlane = 1f;
                cam.farClipPlane = 400f;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Color.clear;
                cam.cullingMask = 1 << ProbeLayer;
                cam.allowHDR = false;      // byte-exact palette needs the 8-bit sRGB path
                cam.allowMSAA = false;
                cam.targetTexture = rt;

                WaitOutShaderCompilation(cam);
                cam.Render();
                return ReadBackTopLeft(rt, def.CellW, def.CellH);
            }
            finally
            {
                RenderTexture.active = null;
                camGo.GetComponent<Camera>().targetTexture = null;
                Object.DestroyImmediate(camGo);
                rt.Release();
                Object.DestroyImmediate(rt);
            }
        }

        static void WaitOutShaderCompilation(Camera cam)
        {
            const double timeoutSeconds = 180.0;
            const int maxWarmUps = 10;
            var clock = Stopwatch.StartNew();
            int renders = 0;
            for (; renders < maxWarmUps; renders++)
            {
                cam.Render();
                if (!ShaderUtil.anythingCompiling) break;
                while (ShaderUtil.anythingCompiling && clock.Elapsed.TotalSeconds < timeoutSeconds)
                    Thread.Sleep(25);
            }
            if (ShaderUtil.anythingCompiling || renders >= maxWarmUps)
                Assert.Fail(
                    "SHADERS NEVER FINISHED COMPILING — not an acceptance regression. Re-run with a " +
                    "warm shader cache (the cold-cache trap fakes exactly this class of red).");
        }

        static byte[] ReadBackTopLeft(RenderTexture rt, int w, int h)
        {
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;

            var px = tex.GetPixels32();
            Object.DestroyImmediate(tex);

            var bytes = new byte[w * h * 4];
            for (int y = 0; y < h; y++)
            {
                int srcRow = (h - 1 - y) * w;   // GetPixels32 is bottom-left; the cell is top-left
                int dstRow = y * w;
                for (int x = 0; x < w; x++)
                {
                    var c = px[srcRow + x];
                    int d = (dstRow + x) * 4;
                    bytes[d] = c.r; bytes[d + 1] = c.g; bytes[d + 2] = c.b; bytes[d + 3] = c.a;
                }
            }
            return bytes;
        }

        static void SetLayerRecursive(Transform t, int layer)
        {
            t.gameObject.layer = layer;
            for (int i = 0; i < t.childCount; i++)
                SetLayerRecursive(t.GetChild(i), layer);
        }
    }
}
