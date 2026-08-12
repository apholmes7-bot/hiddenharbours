using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>
    /// <b>Proof that a scheme reaches the MESH — rendered through the shipping renderer, not a
    /// reference rasteriser.</b>
    ///
    /// <para><b>Why this is the proof and a screenshot of the rig is not.</b> The rig's own JS
    /// rasteriser can paint all twelve schemes beautifully while the GAME still draws the sprite
    /// fallback, and that failure is invisible in every artefact except a picture that came out of
    /// <see cref="IsoFacetHullRenderer"/>. So this builds that component, hands it a
    /// <see cref="HullPaintSchemeDef"/> through the real
    /// <see cref="IsoFacetHullPresentationService.ToSetup(HullMeshDef, HullPaintSchemeDef)"/> seam,
    /// and photographs the result. If the seam ever stops applying paint, every cell below comes out
    /// white and the sheet says so at a glance.</para>
    ///
    /// <para>Framing is the acceptance harness's, verbatim (<c>IsoFacetLobsterEndToEndTests</c>): an
    /// ortho camera sized to the rig's cell and offset so the rig pivot lands on its exact cell pixel,
    /// HDR and MSAA off for the byte-exact 8-bit palette path, read back to the rig's top-left
    /// orientation. Output goes to <c>artifacts/</c>, which is gitignored — this is evidence for a PR,
    /// not shipped art.</para>
    /// </summary>
    public static class HullPaintSchemeProofSheet
    {
        const string HullMeshAssetPath =
            "Assets/_Project/Data/Boats/HullMeshes/LobsterBoatIsoHullMesh.asset";
        const int ProbeLayer = 31;

        /// <summary>Headings for the turntable strip, in the rig's own 8-dir order.</summary>
        static readonly float[] TurntableHeadings = { 0f, 45f, 90f, 135f, 180f, 225f, 270f, 315f };

        [MenuItem("Hidden Harbours/Dev/3D Hulls/Proof — paint schemes on the MESH", priority = 42)]
        public static void Build()
        {
            var hull = AssetDatabase.LoadAssetAtPath<HullMeshDef>(HullMeshAssetPath);
            if (hull == null) throw new FileNotFoundException($"No hull mesh def at {HullMeshAssetPath}");

            var schemes = AssetDatabase
                .FindAssets($"t:{nameof(HullPaintSchemeDef)}", new[] { HullPaintSchemeBaker.SchemeFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<HullPaintSchemeDef>)
                .Where(s => s != null && s.IsUsableFor(hull))
                .OrderBy(s => s.Id)
                .ToArray();
            if (schemes.Length == 0)
                throw new InvalidOperationException(
                    $"No usable schemes under {HullPaintSchemeBaker.SchemeFolder}. Bake them first.");

            string outDir = Path.Combine(Directory.GetParent(Application.dataPath)!.FullName, "artifacts");
            Directory.CreateDirectory(outDir);

            // 1. THE CONTACT SHEET — every scheme at one heading, plus the unpainted control FIRST so
            //    "unset scheme = today's boat" is visible beside the twelve rather than asserted.
            const float ContactHeading = 135f;   // SE: the lit and shaded sides both on screen
            var cells = new System.Collections.Generic.List<(string label, byte[] rgba)>
            {
                ("(no scheme)", Render(hull, null, ContactHeading)),
            };
            foreach (var s in schemes) cells.Add((s.RigPaintId, Render(hull, s, ContactHeading)));

            string contact = Path.Combine(outDir, "paint-schemes-on-the-mesh.png");
            WriteGrid(contact, cells.Select(c => c.rgba).ToList(), hull.CellW, hull.CellH, columns: 5);
            Debug.Log($"[paint-proof] contact sheet -> {contact}\n  cells: " +
                      string.Join(", ", cells.Select(c => c.label)));

            // 2. A TURNTABLE in one painted scheme — the mesh rotates continuously, so a repaint that
            //    only survived at one heading would show up here.
            var navy = schemes.FirstOrDefault(s => s.RigPaintId == "harbour") ?? schemes[0];
            var strip = TurntableHeadings.Select(h => Render(hull, navy, h)).ToList();
            string turntable = Path.Combine(outDir, $"paint-turntable-{navy.RigPaintId}.png");
            WriteGrid(turntable, strip, hull.CellW, hull.CellH, columns: TurntableHeadings.Length);
            Debug.Log($"[paint-proof] turntable ({navy.Id}) -> {turntable}");

            // 3. THE NEGATIVE CONTROL, stated in numbers as well as pixels.
            byte[] plain = cells[0].rgba;
            byte[] painted = cells.First(c => c.label == navy.RigPaintId).rgba;
            long differing = 0, opaque = 0;
            for (int i = 0; i < plain.Length; i += 4)
            {
                if (plain[i + 3] > 0) opaque++;
                if (plain[i] != painted[i] || plain[i + 1] != painted[i + 1] || plain[i + 2] != painted[i + 2])
                    differing++;
            }
            Debug.Log($"[paint-proof] MESH-PATH A/B at heading {ContactHeading:0}°: " +
                      $"{differing:N0} of {opaque:N0} opaque px differ between (no scheme) and " +
                      $"'{navy.Id}'. Zero here would mean the scheme never reached the renderer.");
            if (differing == 0)
                throw new InvalidOperationException(
                    "The painted render is pixel-identical to the unpainted one. The scheme is not " +
                    "reaching IsoFacetHullRenderer — this is the sprite-fallback / dead-seam failure.");
        }

        // ---- rendering (framing verbatim from the acceptance harness) ---------------------------

        static byte[] Render(HullMeshDef def, HullPaintSchemeDef scheme, float headingDeg)
        {
            var go = new GameObject("PaintProofHull") { layer = ProbeLayer };
            try
            {
                var renderer = go.AddComponent<IsoFacetHullRenderer>();
                renderer.Configure(IsoFacetHullPresentationService.ToSetup(def, scheme));
                renderer.HeadingDirUnits =
                    HullMeshMath.HeadingToDirUnits(headingDeg, 0f, def.AzimuthCounterClockwise);
                renderer.ApplyPose();
                SetLayerRecursive(go.transform, ProbeLayer);
                return RenderCell(def, go);
            }
            finally { Object.DestroyImmediate(go); }
        }

        static void SetLayerRecursive(Transform t, int layer)
        {
            t.gameObject.layer = layer;
            for (int i = 0; i < t.childCount; i++) SetLayerRecursive(t.GetChild(i), layer);
        }

        static byte[] RenderCell(HullMeshDef def, GameObject subject)
        {
            float ppu = def.PxPerMetre;
            float ox = (def.PivotPx.x - def.CellW / 2f) / ppu;
            float oy = (def.CellH / 2f - def.PivotPx.y) / ppu;

            var camGo = new GameObject("PaintProofCam");
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
                cam.allowHDR = false;
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

        /// <summary>A cold shader cache fakes exactly the regression this sheet exists to disprove.</summary>
        static void WaitOutShaderCompilation(Camera cam)
        {
            const double timeoutSeconds = 180.0;
            var clock = Stopwatch.StartNew();
            for (int renders = 0; renders < 10; renders++)
            {
                cam.Render();
                if (!ShaderUtil.anythingCompiling) return;
                while (ShaderUtil.anythingCompiling && clock.Elapsed.TotalSeconds < timeoutSeconds)
                    Thread.Sleep(25);
            }
            if (ShaderUtil.anythingCompiling)
                throw new InvalidOperationException(
                    "Shaders never finished compiling — re-run with a warm cache before believing " +
                    "anything this sheet shows.");
        }

        static byte[] ReadBackTopLeft(RenderTexture rt, int w, int h)
        {
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;

            // ReadPixels gives bottom-up rows; the rig's cell convention is top-left origin.
            byte[] src = tex.GetRawTextureData();
            var flipped = new byte[w * h * 4];
            int stride = w * 4;
            for (int y = 0; y < h; y++)
                Array.Copy(src, (h - 1 - y) * stride, flipped, y * stride, stride);

            Object.DestroyImmediate(tex);
            return flipped;
        }

        // ---- sheet assembly ----------------------------------------------------------------------

        static void WriteGrid(string path, System.Collections.Generic.List<byte[]> cells,
                              int cellW, int cellH, int columns)
        {
            int rows = Mathf.CeilToInt(cells.Count / (float)columns);
            int w = cellW * columns, h = cellH * rows;
            var sheet = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var pixels = new byte[w * h * 4];

            for (int i = 0; i < cells.Count; i++)
            {
                int cx = (i % columns) * cellW, cy = (i / columns) * cellH;
                for (int y = 0; y < cellH; y++)
                {
                    int src = y * cellW * 4;
                    int dst = ((cy + y) * w + cx) * 4;
                    Array.Copy(cells[i], src, pixels, dst, cellW * 4);
                }
            }

            // The rows were assembled top-down; flip to PNG's bottom-up convention on load.
            var flipped = new byte[pixels.Length];
            int stride = w * 4;
            for (int y = 0; y < h; y++)
                Array.Copy(pixels, (h - 1 - y) * stride, flipped, y * stride, stride);

            sheet.LoadRawTextureData(flipped);
            sheet.Apply();
            File.WriteAllBytes(path, sheet.EncodeToPNG());
            Object.DestroyImmediate(sheet);
        }
    }
}
