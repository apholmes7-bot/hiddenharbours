// SHORE-SEAM PROOF HARNESS (ADR 0023) — editor-only evidence tooling, never shipped in a build.
// Off-screen renderer following the 3D-water spike's conventions (branch spike/3d-water):
// hand-built CommandBuffer, plain straight-down ortho camera at 32 px/m, PNG readback with a
// single bottom-left/top-left flip, and RUNTIME depth-convention calibration (the spike-measured
// trap: outside URP's frame there is no reversed-Z translation, so ZTest must be measured).
using System;
using System.IO;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tools.Editor
{
    /// <summary>One shore configuration under proof: an analytic transect elevation(y) =
    /// Base + Gradient * y (mirrored exactly by the shader's SeabedElevation).</summary>
    internal readonly struct ShoreProfile
    {
        public readonly string Name;
        public readonly float Base;
        public readonly float Gradient;   // + : land toward +y (north shore); − : south shore

        public ShoreProfile(string name, float baseElevation, float gradient)
        {
            Name = name;
            Base = baseElevation;
            Gradient = gradient;
        }

        /// <summary>Ground y of the depth-0 contour (the walkable waterline) at a tide level.</summary>
        public float WaterlineY(float waterLevel) => (waterLevel - Base) / Gradient;
    }

    /// <summary>Per-frame boundary measurement from a mask render: the rendered water/land
    /// boundary versus the analytic waterline contour, per screen column.</summary>
    internal struct BoundaryStats
    {
        public int Columns;          // columns that had any water at all
        public int MaxTearPx;        // worst water-past-the-contour, toward land (tear). ≤0 = never
        public int MaxGapPx;         // worst water-short-of-the-contour (bared strip at the edge)
    }

    internal sealed class ShoreSeamRenderer : IDisposable
    {
        public const int PPU = 32;

        private readonly Material _waterMat;
        private readonly Material _landMat;
        private readonly Material _calibNear;
        private readonly Material _calibFar;

        public float ZTestOp { get; private set; } = 4f;    // 4 = LEqual, 7 = GEqual (calibrated)
        public float ClearDepth { get; private set; } = 1f;

        public ShoreSeamRenderer()
        {
            var shader = Shader.Find("Hidden/HH/ShoreSeamProof/Water");
            if (shader == null)
                throw new InvalidOperationException("ShoreSeamWater.shader did not compile/import.");
            _waterMat = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            _landMat = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            _calibNear = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            _calibFar = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };

            // Iso depth rows at the fleet's 40° elevation (only self-occlusion ordering needs them).
            float elev = 40f * Mathf.Deg2Rad;
            Vector4 isoDepth = new Vector4(Mathf.Cos(elev), Mathf.Sin(elev), 0f, 0f);
            _waterMat.SetVector("_IsoDepth", isoDepth);
            _waterMat.SetVector("_IsoScreen", new Vector4(1f, 1f, 0f, 0f));   // game-faithful framing
        }

        // ---- wave globals (the ONE shared field, packed exactly like production) ----------------

        /// <summary>Pack the field for a game time into the six shader globals via the production
        /// packing (WaveFieldBridge.Pack), baking the closed-form travel into each train's phase in
        /// DOUBLE — the WaveFieldBridge discipline, copied from the spike's SpikeWaveScenario.</summary>
        public static void PublishGlobals(in WaveTrains trains, double timeSeconds)
        {
            const double twoPi = Math.PI * 2.0;
            WaveTrains src = trains;
            WaveTrain Shifted(int i)
            {
                WaveTrain tr = src[i];
                double k = twoPi / tr.Wavelength;
                double phase = tr.PhaseOffset - k * tr.PhaseSpeed * timeSeconds;
                phase -= Math.Floor(phase / twoPi) * twoPi;
                return new WaveTrain(tr.Direction, tr.Wavelength, tr.Amplitude, (float)phase,
                                     WaveFieldSettings.Default.Gravity);
            }

            int n = trains.Count;
            var shifted = new WaveTrains(
                n > 0 ? Shifted(0) : default, n > 1 ? Shifted(1) : default,
                n > 2 ? Shifted(2) : default, n > 3 ? Shifted(3) : default,
                n, trains.CrestSharpening);
            WaveFieldBridge.Pack(in shifted, out Vector4 t0, out Vector4 t1, out Vector4 t2,
                                 out Vector4 t3, out Vector4 phases, out Vector4 fieldParams);
            Shader.SetGlobalVector("_WaveTrain0", t0);
            Shader.SetGlobalVector("_WaveTrain1", t1);
            Shader.SetGlobalVector("_WaveTrain2", t2);
            Shader.SetGlobalVector("_WaveTrain3", t3);
            Shader.SetGlobalVector("_WavePhases", phases);
            Shader.SetGlobalVector("_WaveFieldParams", fieldParams);
        }

        // ---- depth-convention calibration (the boats/water-spike lesson: never assume; measure) --

        public string CalibrateDepth()
        {
            _calibFar.SetColor("_CalibColor", Color.red);
            _calibNear.SetColor("_CalibColor", Color.green);

            foreach ((float op, float clear) in new[] { (4f, 1f), (7f, 0f) })
            {
                _calibFar.SetFloat("_ZTestOp", op);
                _calibNear.SetFloat("_ZTestOp", op);

                var rt = NewColorRT(64, 64, depthBits: 24);
                var camGo = MakeCamera(Vector2.zero, 64, 64, out Camera cam);
                var cb = new CommandBuffer { name = "SeamCalib" };
                cb.SetRenderTarget(rt.colorBuffer, rt.depthBuffer);
                cb.ClearRenderTarget(true, true, Color.black, clear);
                cb.SetViewProjectionMatrices(cam.worldToCameraMatrix,
                    GL.GetGPUProjectionMatrix(cam.projectionMatrix, false));
                cb.DrawMesh(Quad(-1f, -1f, 1f, 1f, z: 5f), Matrix4x4.identity, _calibFar, 0, 2);
                cb.DrawMesh(Quad(-1f, -1f, 1f, 1f, z: -5f), Matrix4x4.identity, _calibNear, 0, 2);
                Graphics.ExecuteCommandBuffer(cb);
                GL.Flush();
                Color32[] px = ReadBack(rt, 64, 64);
                cb.Release();
                Object.DestroyImmediate(camGo);
                rt.Release();

                Color32 centre = px[(32 * 64) + 32];
                if (centre.g > 128 && centre.r < 128)
                {
                    ZTestOp = op;
                    ClearDepth = clear;
                    _waterMat.SetFloat("_ZTestOp", op);
                    return $"depth convention: ZTest={(op == 4f ? "LEqual" : "GEqual")} clearDepth={clear}";
                }
            }
            throw new InvalidOperationException(
                "Depth calibration failed: neither convention put the near quad on top.");
        }

        // ---- the render: land layer (flat) + displaced water over it ----------------------------

        public Color32[] Render(Rect view, int w, int h, in ShoreProfile profile, float waterLevel,
                                float bandMeters, float heightScale, float totalAmplitude,
                                bool seamEnabled, bool maskMode)
        {
            foreach (Material m in new[] { _waterMat, _landMat })
            {
                m.SetFloat("_ProfileBase", profile.Base);
                m.SetFloat("_ProfileGrad", profile.Gradient);
                m.SetFloat("_WaterLevel", waterLevel);
                m.SetFloat("_Band", bandMeters);
                m.SetFloat("_SeamEnabled", seamEnabled ? 1f : 0f);
                m.SetFloat("_MaskMode", maskMode ? 1f : 0f);
                m.SetFloat("_HeightScale", heightScale);
                m.SetFloat("_PPU", PPU);
            }
            _waterMat.SetVector("_RefXY", new Vector4(view.center.x, view.center.y, 0f, 0f));

            // Ground rect whose displaced image covers the view: pad by the tallest possible lift.
            float lift = totalAmplitude * heightScale + 1f;
            var gridRect = Rect.MinMaxRect(view.xMin - 1f, view.yMin - lift, view.xMax + 1f, view.yMax + lift);
            Mesh grid = BuildGrid(gridRect, cell: 0.125f, out _);
            Mesh land = Quad(view.xMin, view.yMin, view.xMax, view.yMax, z: 200f);

            var rt = NewColorRT(w, h, depthBits: 24);
            var camGo = MakeCamera(view.center, w, h, out Camera cam);
            var cb = new CommandBuffer { name = "SeamProof" };
            cb.SetRenderTarget(rt.colorBuffer, rt.depthBuffer);
            cb.ClearRenderTarget(true, true, Color.black, ClearDepth);
            cb.SetViewProjectionMatrices(cam.worldToCameraMatrix,
                GL.GetGPUProjectionMatrix(cam.projectionMatrix, false));
            cb.DrawMesh(land, Matrix4x4.identity, _landMat, 0, 1);    // land first (ZTest Always)
            cb.DrawMesh(grid, Matrix4x4.identity, _waterMat, 0, 0);   // displaced water over it
            Graphics.ExecuteCommandBuffer(cb);
            GL.Flush();

            Color32[] px = ReadBack(rt, w, h);
            cb.Release();
            Object.DestroyImmediate(camGo);
            rt.Release();
            Object.DestroyImmediate(grid);
            Object.DestroyImmediate(land);
            return px;
        }

        // ---- boundary measurement (mask renders: water = red, land = green) ---------------------

        /// <summary>
        /// Per screen column, find the water pixel nearest the land side and compare it with the
        /// analytic waterline row. Positive tear = water drawn PAST the contour toward land (the
        /// overlap failure); positive gap = the water edge sitting short of the contour (the bared
        /// strip / pull-away failure). Pixels are top-left origin (ReadBack's convention).
        /// </summary>
        public static BoundaryStats MeasureBoundary(Color32[] px, int w, int h, Rect view,
                                                    in ShoreProfile profile, float waterLevel)
        {
            bool landIsUp = profile.Gradient > 0f;
            float contourY = profile.WaterlineY(waterLevel);
            var stats = new BoundaryStats { Columns = 0, MaxTearPx = int.MinValue, MaxGapPx = int.MinValue };

            for (int x = 0; x < w; x++)
            {
                int edgeRow = -1;
                if (landIsUp)
                {
                    for (int y = 0; y < h; y++)                       // top→down: first water pixel
                        if (IsWater(px[y * w + x])) { edgeRow = y; break; }
                }
                else
                {
                    for (int y = h - 1; y >= 0; y--)                  // bottom→up: first water pixel
                        if (IsWater(px[y * w + x])) { edgeRow = y; break; }
                }
                if (edgeRow < 0) continue;                            // no water in this column
                stats.Columns++;

                // The contour's screen row (top-left origin): row = (view.yMax − y) · PPU.
                float contourRowF = (view.yMax - contourY) * PPU;
                // Signed distance from contour to the water edge, in px, positive toward land.
                float tear = landIsUp ? contourRowF - edgeRow : edgeRow - contourRowF;
                int tearPx = Mathf.RoundToInt(tear);
                stats.MaxTearPx = Mathf.Max(stats.MaxTearPx, tearPx);
                stats.MaxGapPx = Mathf.Max(stats.MaxGapPx, -tearPx);
            }
            return stats;
        }

        private static bool IsWater(Color32 c) => c.r > 200 && c.g < 60 && c.b < 60;

        /// <summary>Count exactly-differing pixels between two same-size renders.</summary>
        public static int DiffCount(Color32[] a, Color32[] b)
        {
            int n = 0;
            for (int i = 0; i < a.Length; i++)
                if (a[i].r != b[i].r || a[i].g != b[i].g || a[i].b != b[i].b) n++;
            return n;
        }

        // ---- plumbing (the spike's conventions, verbatim where possible) ------------------------

        private static RenderTexture NewColorRT(int w, int h, int depthBits)
        {
            var rt = new RenderTexture(w, h, depthBits, RenderTextureFormat.ARGB32,
                                       RenderTextureReadWrite.sRGB) { filterMode = FilterMode.Point };
            rt.Create();
            return rt;
        }

        private static GameObject MakeCamera(Vector2 centre, int w, int h, out Camera cam)
        {
            var go = new GameObject("SeamProofCam") { hideFlags = HideFlags.HideAndDontSave };
            cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = h / (2f * PPU);
            cam.aspect = w / (float)h;
            cam.nearClipPlane = 1f;
            cam.farClipPlane = 400f;
            go.transform.position = new Vector3(centre.x, centre.y, -100f);
            go.transform.rotation = Quaternion.identity;
            return go;
        }

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
            var mesh = new Mesh { indexFormat = IndexFormat.UInt32, name = "SeamProofGrid" };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            vertCount = verts.Length;
            return mesh;
        }

        private static Mesh Quad(float x0, float y0, float x1, float y1, float z)
        {
            var mesh = new Mesh { name = "SeamProofQuad" };
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

        public static void WritePng(string path, Color32[] px, int w, int h)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false, false);
            var flipped = new Color32[w * h];
            for (int y = 0; y < h; y++)
                Array.Copy(px, (h - 1 - y) * w, flipped, y * w, w);
            tex.SetPixels32(flipped);
            tex.Apply(false);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
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
            Object.DestroyImmediate(_waterMat);
            Object.DestroyImmediate(_landMat);
            Object.DestroyImmediate(_calibNear);
            Object.DestroyImmediate(_calibFar);
        }
    }
}
