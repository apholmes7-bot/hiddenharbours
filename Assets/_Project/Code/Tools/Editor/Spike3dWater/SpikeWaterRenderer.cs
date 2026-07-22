// ⚠️⚠️ THROWAWAY SPIKE CODE — spike/3d-water. NOT A PIPELINE. NOT FOR MERGE AS-IS. ⚠️⚠️
using System;
using System.IO;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tools.Spike3dWater
{
    /// <summary>
    /// Off-screen renderer for the 3D-water readability experiment (mirrors Spike3dBoatRenderer's
    /// conventions: hand-built CommandBuffer, plain straight-down ortho camera at 32 px/m, PNG
    /// readback with a single bottom-left→top-left flip, runtime depth-convention calibration).
    ///
    /// Renders three things:
    ///  A. the PRODUCTION water (an in-memory copy of the owner's Water.mat on a flat quad, the
    ///     shared wave field published as the same globals WaveFieldBridge uses) — the control;
    ///  B. the DISPLACED surface (IsoWaterSpike.shader: vertex-lifted grid, quantised palette
    ///     bands, world-locked Bayer dither) — the experiment;
    ///  C. the waterline-on-hull PROBE: the committed lobster-boat hull mesh (HullMeshDef,
    ///     ADR 0022 phase 4) drawn with the PRODUCTION facet material into a private z-buffer,
    ///     then the displaced surface depth-tested against it in the rig's true iso frame.
    /// </summary>
    internal sealed class SpikeWaterRenderer : IDisposable
    {
        public const int PPU = 32;
        private const string WaterMatPath = "Assets/_Project/Art/Materials/Water.mat";
        private const string HullDefPath = "Assets/_Project/Data/Boats/HullMeshes/LobsterBoatIsoHullMesh.asset";

        private readonly Material _spikeMat;
        private readonly Material _calibNear;
        private readonly Material _calibFar;
        private readonly Material _waterMat;          // in-memory copy of the owner's Water.mat
        private Material _facetMat;                   // production facet material, built from the def
        private Texture2D _rampTex, _darkRampTex;

        public HullMeshDef HullDef { get; private set; }
        public float ZTestOp { get; private set; } = 4f;   // 4 = LEqual, 7 = GEqual (calibrated)
        public float ClearDepth { get; private set; } = 1f;
        public Color Background = new Color(0.05f, 0.135f, 0.205f, 1f);

        // Palette pulled off the owner's material so the displaced surface wears his colours.
        public Color PaletteDeep, PaletteMid, PaletteShallow, PaletteFoam;
        public Vector2 LightDir2D;

        public SpikeWaterRenderer()
        {
            var spikeShader = Shader.Find("Hidden/HH/Spike3dWater/IsoWater");
            if (spikeShader == null)
                throw new InvalidOperationException("IsoWaterSpike.shader did not compile/import.");
            _spikeMat = new Material(spikeShader) { hideFlags = HideFlags.HideAndDontSave };
            _calibNear = new Material(spikeShader) { hideFlags = HideFlags.HideAndDontSave };
            _calibFar = new Material(spikeShader) { hideFlags = HideFlags.HideAndDontSave };

            var ownersWater = AssetDatabase.LoadAssetAtPath<Material>(WaterMatPath);
            if (ownersWater == null)
                throw new InvalidOperationException("Owner Water.mat not found at " + WaterMatPath);
            _waterMat = new Material(ownersWater) { hideFlags = HideFlags.HideAndDontSave };
            // Open deep sea for the A/B: no baked height map (uniform-deep fallback), tide at datum.
            _waterMat.DisableKeyword("_USE_HEIGHTTEX");
            _waterMat.SetFloat("_WaterLevel", 0f);

            PaletteDeep = ReadColor(ownersWater, "_PaletteDeep", new Color(0.05f, 0.135f, 0.205f));
            PaletteMid = ReadColor(ownersWater, "_PaletteMid", new Color(0.14f, 0.30f, 0.38f));
            PaletteShallow = ReadColor(ownersWater, "_PaletteShallow", new Color(0.34f, 0.60f, 0.62f));
            PaletteFoam = ReadColor(ownersWater, "_PaletteFoam", new Color(0.92f, 0.96f, 0.98f));
            Vector4 ld = ownersWater.HasProperty("_LightDir") ? ownersWater.GetVector("_LightDir")
                                                              : new Vector4(-0.6f, 0.8f, 0f, 0f);
            LightDir2D = new Vector2(ld.x, ld.y);

            _spikeMat.SetColor("_ColDeep", PaletteDeep);
            _spikeMat.SetColor("_ColMid", PaletteMid);
            _spikeMat.SetColor("_ColShallow", PaletteShallow);
            _spikeMat.SetColor("_ColFoam", PaletteFoam);
            _spikeMat.SetVector("_SunDir2D", new Vector4(LightDir2D.x, LightDir2D.y, 0f, 0f));

            LoadHull();
        }

        private static Color ReadColor(Material m, string prop, Color fallback) =>
            m.HasProperty(prop) ? m.GetColor(prop) : fallback;

        // =========================================================================================
        // Hull (production facet path, built exactly as IsoFacetHullRenderer.Configure does)
        // =========================================================================================
        private void LoadHull()
        {
            foreach (Object o in AssetDatabase.LoadAllAssetsAtPath(HullDefPath))
                if (o is HullMeshDef def) { HullDef = def; break; }
            if (HullDef == null || !HullDef.IsUsable())
                throw new InvalidOperationException("LobsterBoat HullMeshDef missing/unusable at " + HullDefPath);

            int maxLen = 0;
            foreach (var ramp in HullDef.Ramps) maxLen = Mathf.Max(maxLen, ramp.Colors.Length);

            Texture2D MakeTex(string name) => new Texture2D(maxLen, HullDef.Ramps.Length,
                TextureFormat.RGBA32, false, false)
            { name = name, hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };

            _rampTex = MakeTex("SpikeRampTex");
            _darkRampTex = MakeTex("SpikeDarkRampTex");

            var ramps = new Color32[HullDef.Ramps.Length][];
            for (int m = 0; m < HullDef.Ramps.Length; m++) ramps[m] = HullDef.Ramps[m].Colors;
            Color32[][] dark = IsoFacetMath.BuildDarkenedRamps(ramps);
            for (int m = 0; m < ramps.Length; m++)
                for (int i = 0; i < maxLen; i++)
                {
                    int k = Mathf.Min(i, ramps[m].Length - 1);
                    _rampTex.SetPixel(i, m, ramps[m][k]);
                    _darkRampTex.SetPixel(i, m, dark[m][k]);
                }
            _rampTex.Apply(false, true);
            _darkRampTex.Apply(false, true);

            var facetShader = Shader.Find("HiddenHarbours/IsoFacet");
            if (facetShader == null)
                throw new InvalidOperationException("Production HiddenHarbours/IsoFacet shader not found.");
            _facetMat = new Material(facetShader) { hideFlags = HideFlags.HideAndDontSave };
            _facetMat.SetTexture("_RampTex", _rampTex);
            _facetMat.SetTexture("_DarkRampTex", _darkRampTex);
            _facetMat.SetVector("_LN", IsoFacetMath.ShaderLightVector(HullDef.LightN));
            _facetMat.SetFloat("_Gain", HullDef.Gain);
            _facetMat.SetFloat("_Bias", HullDef.Bias);
            _facetMat.SetColor("_KeyColor", ((Color)HullDef.Keyline).linear);
            _facetMat.SetVector("_PivotPx", HullDef.PivotPx);
            _facetMat.SetFloat("_PixelsPerMetre", HullDef.PxPerMetre);

            var meta = new Vector4[16];
            for (int m = 0; m < HullDef.Ramps.Length; m++)
                meta[m] = new Vector4(HullDef.Ramps[m].Colors.Length, HullDef.Ramps[m].Offset, 0, 0);
            _facetMat.SetVectorArray("_RampMeta", meta);

            var rows = new Vector4[4];
            for (int x = 0; x < 4; x++)
                rows[x] = new Vector4(HullDef.Bayer16[x * 4 + 0], HullDef.Bayer16[x * 4 + 1],
                                      HullDef.Bayer16[x * 4 + 2], HullDef.Bayer16[x * 4 + 3]);
            _facetMat.SetVectorArray("_Bayer", rows);
            _facetMat.SetFloat("_HullId", 1f / 255f);
        }

        // =========================================================================================
        // Depth-convention calibration (the boats-spike lesson: never assume; measure)
        // =========================================================================================
        public string CalibrateDepth()
        {
            // Two flat quads drawn far-then-near through the calib pass. Whichever (ZTest, clear)
            // convention leaves the NEAR quad's colour on top is the machine's convention.
            _calibFar.SetColor("_CalibColor", Color.red);
            _calibNear.SetColor("_CalibColor", Color.green);

            foreach ((float op, float clear) in new[] { (4f, 1f), (7f, 0f) })
            {
                _calibFar.SetFloat("_ZTestOp", op);
                _calibNear.SetFloat("_ZTestOp", op);

                var rt = NewColorRT(64, 64, depthBits: 24);
                var camGo = MakeCamera(Vector2.zero, 64, 64, out Camera cam);
                var cb = new CommandBuffer { name = "SpikeCalib" };
                cb.SetRenderTarget(rt.colorBuffer, rt.depthBuffer);
                cb.ClearRenderTarget(true, true, Color.black, clear);
                cb.SetViewProjectionMatrices(cam.worldToCameraMatrix,
                    GL.GetGPUProjectionMatrix(cam.projectionMatrix, false));
                cb.DrawMesh(Quad(-1f, -1f, 1f, 1f, z: 5f), Matrix4x4.identity, _calibFar, 0, 1);
                cb.DrawMesh(Quad(-1f, -1f, 1f, 1f, z: -5f), Matrix4x4.identity, _calibNear, 0, 1);
                Graphics.ExecuteCommandBuffer(cb);
                GL.Flush();
                Color32[] px = ReadBack(rt, 64, 64);
                cb.Release();
                Object.DestroyImmediate(camGo);
                rt.Release();

                Color32 centre = px[(32 * 64) + 32];
                bool nearWins = centre.g > 128 && centre.r < 128;
                if (nearWins)
                {
                    ZTestOp = op;
                    ClearDepth = clear;
                    _spikeMat.SetFloat("_ZTestOp", op);
                    return $"depth convention: ZTest={(op == 4f ? "LEqual" : "GEqual")} clearDepth={clear}";
                }
            }
            throw new InvalidOperationException("Depth calibration failed: neither convention put the near quad on top.");
        }

        // =========================================================================================
        // A: the production water (the control)
        // =========================================================================================
        public Color32[] RenderProduction(Rect view, int w, int h, float seaState, Vector2 wind,
                                          out double gpuMs)
        {
            _waterMat.SetFloat("_Chop", seaState);
            Vector2 windDir = wind.sqrMagnitude > 1e-8f ? wind.normalized : Vector2.up;
            _waterMat.SetVector("_WindDir", new Vector4(windDir.x, windDir.y, 0f, 0f));
            _waterMat.SetFloat("_Roughness", Mathf.Clamp01(wind.magnitude / 12f));

            var rt = NewColorRT(w, h, depthBits: 24);
            var camGo = MakeCamera(view.center, w, h, out Camera cam);
            var cb = new CommandBuffer { name = "SpikeWaterA" };
            cb.SetRenderTarget(rt.colorBuffer, rt.depthBuffer);
            cb.ClearRenderTarget(true, true, Background, ClearDepth);
            cb.SetViewProjectionMatrices(cam.worldToCameraMatrix,
                GL.GetGPUProjectionMatrix(cam.projectionMatrix, false));
            cb.DrawMesh(Quad(view.xMin, view.yMin, view.xMax, view.yMax, z: 0f),
                        Matrix4x4.identity, _waterMat, 0, 0);

            var sw = System.Diagnostics.Stopwatch.StartNew();
            Graphics.ExecuteCommandBuffer(cb);
            GL.Flush();
            gpuMs = sw.Elapsed.TotalMilliseconds;

            Color32[] px = ReadBack(rt, w, h);
            cb.Release();
            Object.DestroyImmediate(camGo);
            rt.Release();
            return px;
        }

        // =========================================================================================
        // B: the displaced surface (the experiment)
        // =========================================================================================
        /// <param name="isoScreen">(1,1) = game-faithful A/B framing (unforeshortened ground, wave
        /// height lifts screen-y like production heave); (sin e, cos e) = the rig's true iso frame
        /// (the probe).</param>
        public Color32[] RenderDisplaced(Rect view, int w, int h, float heightScale,
                                         Vector2 isoScreen, Vector2 refXY, float gridCell,
                                         float totalAmplitude, out double gpuMs, out int gridVerts)
        {
            ConfigureSpikeMat(heightScale, isoScreen, refXY);

            Mesh grid = BuildGrid(GridRectFor(view, isoScreen, refXY, heightScale, totalAmplitude),
                                  gridCell, out gridVerts);

            var rt = NewColorRT(w, h, depthBits: 24);
            var camGo = MakeCamera(view.center, w, h, out Camera cam);
            var cb = new CommandBuffer { name = "SpikeWaterB" };
            cb.SetRenderTarget(rt.colorBuffer, rt.depthBuffer);
            cb.ClearRenderTarget(true, true, Background, ClearDepth);
            cb.SetViewProjectionMatrices(cam.worldToCameraMatrix,
                GL.GetGPUProjectionMatrix(cam.projectionMatrix, false));
            cb.DrawMesh(grid, Matrix4x4.identity, _spikeMat, 0, 0);

            var sw = System.Diagnostics.Stopwatch.StartNew();
            Graphics.ExecuteCommandBuffer(cb);
            GL.Flush();
            gpuMs = sw.Elapsed.TotalMilliseconds;

            Color32[] px = ReadBack(rt, w, h);
            cb.Release();
            Object.DestroyImmediate(camGo);
            rt.Release();
            Object.DestroyImmediate(grid);
            return px;
        }

        // =========================================================================================
        // C: the waterline-on-hull probe (displaced water depth-tested against the mesh hull)
        // =========================================================================================
        public Color32[] RenderProbe(Rect view, int w, int h, Vector2 hullPos, float hullDirUnits,
                                     float draftMeters, float heightScale, float gridCell,
                                     float totalAmplitude, out double gpuMs)
        {
            float elev = HullDef.ElevationDeg * Mathf.Deg2Rad;
            float se = Mathf.Sin(elev), ce = Mathf.Cos(elev);
            var isoScreen = new Vector2(se, ce);
            ConfigureSpikeMat(heightScale, isoScreen, hullPos);

            Mesh grid = BuildGrid(GridRectFor(view, isoScreen, hullPos, heightScale, totalAmplitude),
                                  gridCell, out _);

            // Hull matrix: world position · the production iso projection (a reflection, det −1;
            // fine as a raw DrawMesh matrix) · a rig-frame sink of DRAFT metres down its own keel.
            Matrix4x4 hullM = Matrix4x4.Translate(new Vector3(hullPos.x, hullPos.y, 0f))
                            * IsoFacetMath.RigToWorld(hullDirUnits, HullDef.ElevationDeg)
                            * Matrix4x4.Translate(new Vector3(0f, 0f, -draftMeters));
            _facetMat.SetVector("_HullOrigin", new Vector4(hullPos.x, hullPos.y, 0f, 0f));

            // The production facet pass writes a 4-target MRT; bind all four, keep one depth.
            var facetRT = NewColorRT(w, h, depthBits: 24);
            var darkRT = NewColorRT(w, h, depthBits: 0);
            var keyRT = NewColorRT(w, h, depthBits: 0);
            var depRT = new RenderTexture(w, h, 0, RenderTextureFormat.RFloat,
                                          RenderTextureReadWrite.Linear) { filterMode = FilterMode.Point };
            depRT.Create();

            var camGo = MakeCamera(view.center, w, h, out Camera cam);
            var cb = new CommandBuffer { name = "SpikeWaterProbe" };
            cb.SetRenderTarget(new RenderTargetIdentifier[]
                { facetRT.colorBuffer, darkRT.colorBuffer, keyRT.colorBuffer, depRT.colorBuffer },
                facetRT.depthBuffer);
            cb.ClearRenderTarget(true, true, Background, ClearDepth);
            cb.SetViewProjectionMatrices(cam.worldToCameraMatrix,
                GL.GetGPUProjectionMatrix(cam.projectionMatrix, false));
            cb.DrawMesh(HullDef.Mesh, hullM, _facetMat, 0, 0);

            // Same depth buffer, single colour target: the displaced sea closes over her hull.
            cb.SetRenderTarget(facetRT.colorBuffer, facetRT.depthBuffer);
            cb.DrawMesh(grid, Matrix4x4.identity, _spikeMat, 0, 0);

            var sw = System.Diagnostics.Stopwatch.StartNew();
            Graphics.ExecuteCommandBuffer(cb);
            GL.Flush();
            gpuMs = sw.Elapsed.TotalMilliseconds;

            Color32[] px = ReadBack(facetRT, w, h);
            cb.Release();
            Object.DestroyImmediate(camGo);
            facetRT.Release(); darkRT.Release(); keyRT.Release(); depRT.Release();
            Object.DestroyImmediate(grid);
            return px;
        }

        private void ConfigureSpikeMat(float heightScale, Vector2 isoScreen, Vector2 refXY)
        {
            float elev = HullDef != null ? HullDef.ElevationDeg * Mathf.Deg2Rad : 40f * Mathf.Deg2Rad;
            _spikeMat.SetFloat("_HeightScale", heightScale);
            _spikeMat.SetVector("_IsoScreen", new Vector4(isoScreen.x, isoScreen.y, 0f, 0f));
            _spikeMat.SetVector("_IsoDepth", new Vector4(Mathf.Cos(elev), Mathf.Sin(elev), 0f, 0f));
            _spikeMat.SetVector("_RefXY", new Vector4(refXY.x, refXY.y, 0f, 0f));
            _spikeMat.SetFloat("_ZTestOp", ZTestOp);
            _spikeMat.SetFloat("_PPU", PPU);
        }

        /// <summary>The ground rect whose displaced image covers <paramref name="view"/>: undo the
        /// screen-y mapping for both view edges and pad by the tallest possible lift.</summary>
        private static Rect GridRectFor(Rect view, Vector2 isoScreen, Vector2 refXY,
                                        float heightScale, float totalAmplitude)
        {
            float se = Mathf.Max(0.2f, isoScreen.x);
            float lift = totalAmplitude * heightScale * Mathf.Max(isoScreen.y, 1f) + 1f;
            float yMin = refXY.y + (view.yMin - refXY.y - lift) / se;
            float yMax = refXY.y + (view.yMax - refXY.y + lift) / se;
            return Rect.MinMaxRect(view.xMin - 1f, yMin, view.xMax + 1f, yMax);
        }

        // =========================================================================================
        // Plumbing
        // =========================================================================================
        private static RenderTexture NewColorRT(int w, int h, int depthBits)
        {
            var rt = new RenderTexture(w, h, depthBits, RenderTextureFormat.ARGB32,
                                       RenderTextureReadWrite.sRGB) { filterMode = FilterMode.Point };
            rt.Create();
            return rt;
        }

        private static GameObject MakeCamera(Vector2 centre, int w, int h, out Camera cam)
        {
            var go = new GameObject("SpikeWaterCam") { hideFlags = HideFlags.HideAndDontSave };
            cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = h / (2f * PPU);
            cam.aspect = w / (float)h;
            cam.nearClipPlane = 1f;
            cam.farClipPlane = 400f;
            go.transform.position = new Vector3(centre.x, centre.y, -100f);   // 2D convention: look along +Z
            go.transform.rotation = Quaternion.identity;
            return go;
        }

        /// <summary>Flat grid over <paramref name="rect"/>, verts in WORLD coords (identity object
        /// matrix); the vertex shader does the lifting.</summary>
        private static Mesh BuildGrid(Rect rect, float cell, out int vertCount)
        {
            int nx = Mathf.Max(1, Mathf.CeilToInt(rect.width / cell));
            int ny = Mathf.Max(1, Mathf.CeilToInt(rect.height / cell));
            var verts = new Vector3[(nx + 1) * (ny + 1)];
            for (int j = 0; j <= ny; j++)
                for (int i = 0; i <= nx; i++)
                    verts[j * (nx + 1) + i] = new Vector3(
                        rect.xMin + rect.width * (i / (float)nx),
                        rect.yMin + rect.height * (j / (float)ny), 0f);
            var tris = new int[nx * ny * 6];
            int t = 0;
            for (int j = 0; j < ny; j++)
                for (int i = 0; i < nx; i++)
                {
                    int a = j * (nx + 1) + i, b = a + 1, c = a + nx + 1, d = c + 1;
                    tris[t++] = a; tris[t++] = c; tris[t++] = b;
                    tris[t++] = b; tris[t++] = c; tris[t++] = d;
                }
            var mesh = new Mesh { indexFormat = IndexFormat.UInt32, name = "SpikeWaterGrid" };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            vertCount = verts.Length;
            return mesh;
        }

        private static Mesh Quad(float x0, float y0, float x1, float y1, float z)
        {
            var mesh = new Mesh { name = "SpikeQuad" };
            mesh.SetVertices(new[]
            {
                new Vector3(x0, y0, z), new Vector3(x1, y0, z),
                new Vector3(x1, y1, z), new Vector3(x0, y1, z),
            });
            mesh.SetUVs(0, new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up });
            mesh.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>GetPixels is bottom-left origin; evidence PNGs are top-left. Flip once, here.</summary>
        private static Color32[] ReadBack(RenderTexture rt, int w, int h)
        {
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0, false);
            tex.Apply(false);
            RenderTexture.active = prev;
            Color32[] src = tex.GetPixels32();
            Object.DestroyImmediate(tex);
            var dst = new Color32[w * h];
            for (int y = 0; y < h; y++)
                Array.Copy(src, (h - 1 - y) * w, dst, y * w, w);
            return dst;
        }

        // ---- evidence helpers -------------------------------------------------------------------
        public static void WritePng(string path, Color32[] px, int w, int h, int scale = 1)
        {
            int W = w * scale, H = h * scale;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false, false);
            var big = scale == 1 ? px : new Color32[W * H];
            if (scale != 1)
                for (int y = 0; y < H; y++)
                    for (int x = 0; x < W; x++)
                        big[y * W + x] = px[(y / scale) * w + (x / scale)];
            // WritePng gets TOP-left-origin pixels; SetPixels32 wants bottom-left. Flip back.
            var flipped = new Color32[W * H];
            for (int y = 0; y < H; y++)
                Array.Copy(big, (H - 1 - y) * W, flipped, y * W, W);
            tex.SetPixels32(flipped);
            tex.Apply(false);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }

        /// <summary>Crosshair marker (annotated COPIES only — raw evidence stays unmarked).</summary>
        public static Color32[] Annotate(Color32[] px, int w, int h, int cx, int cy)
        {
            var outPx = (Color32[])px.Clone();
            var c = new Color32(255, 64, 192, 255);
            void Put(int x, int y)
            {
                if (x >= 0 && x < w && y >= 0 && y < h) outPx[y * w + x] = c;
            }
            for (int d = 6; d <= 18; d++)
            {
                Put(cx - d, cy); Put(cx + d, cy); Put(cx, cy - d); Put(cx, cy + d);
                Put(cx - d, cy + 1); Put(cx + d, cy + 1); Put(cx + 1, cy - d); Put(cx + 1, cy + d);
            }
            return outPx;
        }

        public static Color32[] SideBySide(Color32[] a, Color32[] b, int w, int h, out int outW)
        {
            const int gap = 4;
            outW = w * 2 + gap;
            var outPx = new Color32[outW * h];
            var divider = new Color32(20, 20, 20, 255);
            for (int y = 0; y < h; y++)
            {
                Array.Copy(a, y * w, outPx, y * outW, w);
                for (int x = 0; x < gap; x++) outPx[y * outW + w + x] = divider;
                Array.Copy(b, y * w, outPx, y * outW + w + gap, w);
            }
            return outPx;
        }

        public void Dispose()
        {
            Object.DestroyImmediate(_spikeMat);
            Object.DestroyImmediate(_calibNear);
            Object.DestroyImmediate(_calibFar);
            Object.DestroyImmediate(_waterMat);
            Object.DestroyImmediate(_facetMat);
            Object.DestroyImmediate(_rampTex);
            Object.DestroyImmediate(_darkRampTex);
        }
    }
}
