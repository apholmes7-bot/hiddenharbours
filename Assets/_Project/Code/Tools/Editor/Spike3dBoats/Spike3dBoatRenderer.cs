// ⚠️⚠️ THROWAWAY SPIKE CODE — spike/3d-boats. NOT A PIPELINE. NOT FOR MERGE AS-IS. ⚠️⚠️
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HiddenHarbours.Tools.Spike3dBoats
{
    /// <summary>
    /// Renders a rig's face list as a real-time 3D mesh, reproducing the rig's own rasteriser:
    /// flat facets, fixed screen-space key light, palette-ramp lookup, ordered dither, 1px keyline,
    /// no AA — into the SAME native pixel grid the sprites occupy (456×420 at 32 px/m).
    /// </summary>
    public sealed class Spike3dBoatRenderer : System.IDisposable
    {
        readonly RigMeshData _rig;
        readonly Mesh _mesh;
        readonly Material _mat;
        readonly Texture2D _rampTex;
        readonly Dictionary<int, (int ramp, int idx)> _rampIndex = new();   // packed RGB -> RINDEX
        readonly float _se, _ce;

        public int Triangles { get; }
        public int Vertices  { get; }

        public Spike3dBoatRenderer(RigMeshData rig, float elevationDeg)
        {
            _rig = rig;
            _se = Mathf.Sin(elevationDeg * Mathf.Deg2Rad);
            _ce = Mathf.Cos(elevationDeg * Mathf.Deg2Rad);

            // ---- mesh: one face -> a fan, verts UNSHARED so shading stays flat ----------------
            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var attrs = new List<Vector4>();
            var tris  = new List<int>();
            foreach (var f in _rig.Faces)
            {
                // The rig's own normal(): u = v1-v0, v = v2-v0, n = u x v, on the FIRST three verts.
                Vector3 n = Vector3.Cross(f.V[1] - f.V[0], f.V[2] - f.V[0]).normalized;
                int b0 = verts.Count;
                foreach (var v in f.V)
                {
                    verts.Add(v);
                    norms.Add(n);
                    attrs.Add(new Vector4(f.Mat, f.B, f.Db, 0));
                }
                for (int t = 1; t + 1 < f.V.Length; t++)
                {
                    tris.Add(b0); tris.Add(b0 + t); tris.Add(b0 + t + 1);
                }
            }
            _mesh = new Mesh { indexFormat = IndexFormat.UInt32, name = "SpikeHull" };
            _mesh.SetVertices(verts);
            _mesh.SetNormals(norms);
            _mesh.SetUVs(0, attrs);
            _mesh.SetTriangles(tris, 0);
            _mesh.RecalculateBounds();
            Vertices = verts.Count;
            Triangles = tris.Count / 3;

            // ---- ramp LUT + RINDEX (colour -> ramp,index, for the keyline darkening) ---------
            int maxLen = 0;
            foreach (var m in _rig.Materials) maxLen = Mathf.Max(maxLen, m.Ramp.Length);
            _rampTex = new Texture2D(maxLen, _rig.Materials.Count, TextureFormat.RGBA32, false, true)
                       { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var meta = new Vector4[16];
            for (int m = 0; m < _rig.Materials.Count; m++)
            {
                var mm = _rig.Materials[m];
                for (int i = 0; i < maxLen; i++)
                    _rampTex.SetPixel(i, m, mm.Ramp[Mathf.Min(i, mm.Ramp.Length - 1)]);
                meta[m] = new Vector4(mm.Ramp.Length, mm.Off, 0, 0);
                // RINDEX is built from the RAMPS, and blk/dark alias BOOT, so last-wins matches JS.
                for (int i = 0; i < mm.Ramp.Length; i++)
                    _rampIndex[Pack(mm.Ramp[i])] = (m, i);
            }
            _rampTex.Apply();

            var sh = Shader.Find("Hidden/HH/Spike3dBoats/IsoFacet");
            if (sh == null) throw new MissingReferenceException("IsoFacet.shader did not compile/import.");
            _mat = new Material(sh);
            _mat.SetTexture("_RampTex", _rampTex);
            // Unity is LEFT-handed and its 2D camera looks along +Z, so the iso placement matrix is
            // a reflection of the rig's right-handed frame (see ObjectMatrix). That flips the sign
            // of the third shade term: the rig's (-n1*ce + n2*se) is -Z' in this frame.
            _mat.SetVector("_LN", new Vector4(_rig.LightN.x, _rig.LightN.y, -_rig.LightN.z, 0));
            _mat.SetFloat("_Gain", _rig.Gain);
            _mat.SetFloat("_Bias", _rig.Bias);
            _mat.SetVectorArray("_RampMeta", meta);

            // BAYER[x&3][y&3], values (v+0.5)/16 — row index is X in the rig, keep it that way.
            int[,] bay = { { 0, 8, 2, 10 }, { 12, 4, 14, 6 }, { 3, 11, 1, 9 }, { 15, 7, 13, 5 } };
            var rows = new Vector4[4];
            for (int x = 0; x < 4; x++)
                rows[x] = new Vector4((bay[x, 0] + 0.5f) / 16f, (bay[x, 1] + 0.5f) / 16f,
                                      (bay[x, 2] + 0.5f) / 16f, (bay[x, 3] + 0.5f) / 16f);
            _mat.SetVectorArray("_Bayer", rows);
        }

        static int Pack(Color32 c) => (c.r << 16) | (c.g << 8) | c.b;

        /// <summary>
        /// Object matrix: Rx(elev−90) · Rz(heading), applied to the rig's own coordinates. This puts
        /// the hull into ISO-ROTATED WORLD SPACE, so the game's ordinary straight-down orthographic
        /// 2D camera reproduces the rig's projection exactly — and the hull's camera depth becomes
        /// the rig's own z-buffer depth. That equivalence is the load-bearing trick of this spike.
        /// </summary>
        public Matrix4x4 ObjectMatrix(double dir, Vector3 worldOffset)
        {
            float th = (float)dir * Mathf.PI / 4f;
            float ct = Mathf.Cos(th), st = Mathf.Sin(th);
            var m = Matrix4x4.identity;
            m.m00 =  ct;       m.m01 = -st;      m.m02 = 0;
            m.m10 =  _se * st; m.m11 = _se * ct; m.m12 =  _ce;
            m.m20 =  _ce * st; m.m21 =  _ce * ct; m.m22 = -_se;
            m.m03 = worldOffset.x; m.m13 = worldOffset.y; m.m23 = worldOffset.z;
            return m;
        }

        /// <summary>Renders one cell at native resolution and returns straight RGBA32 bytes.</summary>
        /// <summary>Unity's depth convention for a hand-built CommandBuffer + explicit matrices is
        /// not something to assume — see the note where this is set. 4 = LEqual, 7 = GEqual.</summary>
        public int ZTestOp = 4;
        public float ClearDepth = 1f;
        public Vector4 DitherPhase = Vector4.zero;   // xy = pixel offset, z = swap x/y

        public Color32[] RenderCell(double dir, out double gpuMs)
        {
            _mat.SetFloat("_ZTest", ZTestOp);
            _mat.SetVector("_DitherPhase", DitherPhase);

            int W = _rig.W, H = _rig.H, PX = _rig.PxPerMetre;

            var colRT = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32,
                                          RenderTextureReadWrite.Linear) { filterMode = FilterMode.Point };
            var depRT = new RenderTexture(W, H, 0, RenderTextureFormat.RFloat,
                                          RenderTextureReadWrite.Linear) { filterMode = FilterMode.Point };
            colRT.Create(); depRT.Create();

            // Camera: plain orthographic, straight down −Z, 32 px per world unit — i.e. the game's
            // own 2D camera. Centre it so the rig pivot (cx,cy from the cell's top-left) lands right.
            float halfH = H / (2f * PX);
            float ox = (_rig.PivotX - W / 2f) / PX;
            float oy = (H / 2f - _rig.PivotY) / PX;
            var camGo = new GameObject("SpikeCam") { hideFlags = HideFlags.HideAndDontSave };
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = halfH;
            cam.aspect = W / (float)H;
            cam.nearClipPlane = 1f; cam.farClipPlane = 400f;
            cam.transform.position = new Vector3(-ox, -oy, -100f);   // Unity's 2D convention
            cam.transform.rotation = Quaternion.identity;            // looking along +Z

            var cb = new CommandBuffer { name = "SpikeHull" };
            cb.SetRenderTarget(new RenderTargetIdentifier[] { colRT.colorBuffer, depRT.colorBuffer },
                               colRT.depthBuffer);
            cb.ClearRenderTarget(true, true, new Color(0, 0, 0, 0), ClearDepth);
            // renderIntoTexture MUST be false here. Passing true applies D3D's RT Y-flip, which then
            // double-counts against the bottom-left→top-left flip in the readback below and lands
            // the hull upside down (seen: deck under the keel, aerials pointing at the seabed).
            // It would ALSO mis-align the Bayer dither, since SV_Position.y must be the rig's y.
            cb.SetViewProjectionMatrices(cam.worldToCameraMatrix,
                GL.GetGPUProjectionMatrix(cam.projectionMatrix, false));
            cb.DrawMesh(_mesh, ObjectMatrix(dir, Vector3.zero), _mat, 0, 0);

            var sw = System.Diagnostics.Stopwatch.StartNew();
            Graphics.ExecuteCommandBuffer(cb);
            GL.Flush();

            var colTex = ReadBack(colRT, TextureFormat.RGBA32);
            var depTex = ReadBack(depRT, TextureFormat.RGBAFloat);
            gpuMs = sw.Elapsed.TotalMilliseconds;

            var col = colTex.GetPixels32();
            var depPx = depTex.GetPixels();
            cb.Release();
            Object.DestroyImmediate(camGo);
            colRT.Release(); depRT.Release();
            Object.DestroyImmediate(colTex); Object.DestroyImmediate(depTex);

            // GetPixels is BOTTOM-left origin; the rig's buffer is TOP-left. Flip once, here.
            var outPx = new Color32[W * H];
            var dep = new float[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    int src = (H - 1 - y) * W + x, dst = y * W + x;
                    outPx[dst] = col[src];
                    dep[dst] = depPx[src].r;
                }

            ApplyKeyline(outPx, dep, W, H);
            return outPx;
        }

        static Texture2D ReadBack(RenderTexture rt, TextureFormat fmt)
        {
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, fmt, false, true);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            return tex;
        }

        /// <summary>
        /// The rig's post-pass, verbatim: darken the FAR side of a depth discontinuity by two ramp
        /// steps, then flood a 1 px KEY-coloured silhouette into empty pixels touching the hull.
        /// Two fullscreen taps — trivially a shader in production; done on the CPU here so the A/B
        /// against the rig's own output is exact rather than approximately exact.
        /// </summary>
        void ApplyKeyline(Color32[] px, float[] dep, int W, int H)
        {
            var src = (Color32[])px.Clone();
            bool Solid(int i) => src[i].a > 0;

            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    int i = y * W + x;
                    if (!Solid(i)) continue;
                    for (int k = 0; k < 2; k++)
                    {
                        int nx = x + (k == 0 ? 1 : 0), ny = y + (k == 0 ? 0 : 1);
                        if (nx >= W || ny >= H) continue;
                        int j = ny * W + nx;
                        if (!Solid(j)) continue;
                        if (Mathf.Abs(dep[i] - dep[j]) > 0.30f)
                        {
                            int far = dep[i] > dep[j] ? i : j;
                            if (_rampIndex.TryGetValue(Pack(src[far]), out var e) && e.idx > 0)
                                px[far] = _rig.Materials[e.ramp].Ramp[Mathf.Max(0, e.idx - 2)];
                        }
                    }
                }

            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    int i = y * W + x;
                    if (px[i].a > 0) continue;
                    bool touch = false;
                    if (x + 1 < W && Solid(i + 1)) touch = true;
                    else if (x - 1 >= 0 && Solid(i - 1)) touch = true;
                    else if (y + 1 < H && Solid(i + W)) touch = true;
                    else if (y - 1 >= 0 && Solid(i - W)) touch = true;
                    if (touch) px[i] = _rig.Keyline;
                }
        }

        public void Dispose()
        {
            if (_mesh) Object.DestroyImmediate(_mesh);
            if (_mat) Object.DestroyImmediate(_mat);
            if (_rampTex) Object.DestroyImmediate(_rampTex);
        }
    }
}
