using System.Collections.Generic;
using HiddenHarbours.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// <b>A held prop's drawable mesh</b> (ADR 0053): the def's faces plus its plotted thin parts (the
    /// rod blank, a shaft, a bail) built as thin square tubes, each in one flat colour of the rig's own
    /// line ramp.
    ///
    /// <para><b>Why tubes, and what they stand in for.</b> D2 rules the thin parts a 1–2 px line pass in
    /// the facet shader, which art-pipeline signs (held props PR 3), and that pass does not exist yet.
    /// Without it a held rod is its grip and reel alone. A tube <c>lineWidth</c> wide along the rig's own
    /// samples, written into the same depth buffer as the figure, draws the blank at the rig's place and
    /// colour; it is honest geometry, not a bake, and the day the line pass lands it replaces this.</para>
    ///
    /// <para>Each line colour is one more ramp slot of a single entry, so every shade index lands on it
    /// (the shader clamps into the ramp). A def whose faces and line colours pass the 16 slots is
    /// refused. Built once per def; never per frame.</para>
    /// </summary>
    public static class IsoFacetHeldPropMesh
    {
        /// <summary>UV0 = (material id, face bias, depth bias, side code): RigMeshBuilder's layout.</summary>
        private const int AttrUvChannel = 0;

        /// <summary>What <see cref="TryBuild"/> makes: the mesh and the ramp table its ids index.</summary>
        public struct Built
        {
            public Mesh Mesh;
            public Color32[][] Ramps;
            public int[] RampOffsets;
            public float[] RampDitherWeights;
            public int TubeSegments;
        }

        /// <summary>
        /// Build the def's drawable mesh with its thin parts as tubes
        /// <paramref name="lineWidthMeters"/> across. False, with the reason, when the def is not usable,
        /// its mesh cannot be read, or its colours pass the slot law. The caller owns the mesh.
        /// </summary>
        public static bool TryBuild(HeldPropMeshDef def, float lineWidthMeters, out Built built, out string why)
        {
            built = default;
            if (def == null) { why = "no def"; return false; }
            if (!def.IsUsable()) { why = $"{def.Id} is not usable"; return false; }
            Mesh src = def.Mesh;
            if (!src.isReadable) { why = $"{def.Id}'s mesh is not readable"; return false; }

            var ramps = new List<Color32[]>();
            var offsets = new List<int>();
            var dither = new List<float>();
            foreach (HullMeshDef.Ramp r in def.Ramps)
            {
                ramps.Add(r.Colors);
                offsets.Add(r.Offset);
                dither.Add(r.DitherWeight);
            }

            var verts = new List<Vector3>(src.vertices);
            var norms = new List<Vector3>(src.normals);
            var attrs = new List<Vector4>();
            src.GetUVs(AttrUvChannel, attrs);
            if (attrs.Count != verts.Count) { why = $"{def.Id}'s mesh has no UV0 per vertex"; return false; }
            if (norms.Count != verts.Count) { why = $"{def.Id}'s mesh has no normal per vertex"; return false; }
            var tris = new List<int>(src.triangles);

            // (line ramp, entry) -> slot, so a colour used by many samples takes one slot.
            var slotOf = new Dictionary<long, int>();
            float h = Mathf.Max(0f, lineWidthMeters) * 0.5f;
            int segments = 0;
            if (h > 0f && def.Lines != null)
            {
                foreach (HeldPropMeshDef.Line line in def.Lines)
                {
                    if (line.Dots || line.Points == null) continue;
                    for (int i = 0; i + 1 < line.Points.Length; i++)
                    {
                        HeldPropMeshDef.LinePoint a = line.Points[i];
                        Vector3 b = line.Points[i + 1].Position;
                        if ((b - a.Position).sqrMagnitude < 1e-12f) continue;
                        if (!TrySlot(def, a, slotOf, ramps, offsets, dither, out int slot, out why)) return false;
                        AddTube(a.Position, b, h, new Vector4(slot, 0f, line.DepthBiasMeters, 0f), verts, norms, attrs, tris);
                        segments++;
                    }
                }
            }

            var mesh = new Mesh { name = "HHHeldPropDrawn", hideFlags = HideFlags.HideAndDontSave };
            if (verts.Count > 65535) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetUVs(AttrUvChannel, attrs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();

            built = new Built
            {
                Mesh = mesh,
                Ramps = ramps.ToArray(),
                RampOffsets = offsets.ToArray(),
                RampDitherWeights = dither.ToArray(),
                TubeSegments = segments,
            };
            why = null;
            return true;
        }

        private static bool TrySlot(HeldPropMeshDef def, in HeldPropMeshDef.LinePoint p, Dictionary<long, int> slotOf,
                                    List<Color32[]> ramps, List<int> offsets, List<float> dither,
                                    out int slot, out string why)
        {
            why = null;
            long key = ((long)p.Ramp << 32) | (uint)p.Index;
            if (slotOf.TryGetValue(key, out slot)) return true;
            if (def.LineRamps == null || p.Ramp < 0 || p.Ramp >= def.LineRamps.Length
                || def.LineRamps[p.Ramp].Colors == null || def.LineRamps[p.Ramp].Colors.Length == 0)
            {
                why = $"{def.Id} plots a line sample from line ramp {p.Ramp}, which it does not carry";
                return false;
            }
            if (ramps.Count >= HeldPropMeshDef.RampSlots)
            {
                why = $"{def.Id}'s faces and line colours need more than {HeldPropMeshDef.RampSlots} ramp slots";
                return false;
            }
            Color32[] line = def.LineRamps[p.Ramp].Colors;
            slot = ramps.Count;
            ramps.Add(new[] { line[Mathf.Clamp(p.Index, 0, line.Length - 1)] });
            offsets.Add(0);
            dither.Add(1f);
            slotOf.Add(key, slot);
            return true;
        }

        /// <summary>A square tube from <paramref name="a"/> to <paramref name="b"/>, half-width
        /// <paramref name="h"/>: four flat sides, each wound both ways so it draws whichever way the
        /// mirrored frame turns it.</summary>
        private static void AddTube(Vector3 a, Vector3 b, float h, Vector4 attr,
                                    List<Vector3> verts, List<Vector3> norms, List<Vector4> attrs, List<int> tris)
        {
            Vector3 d = (b - a).normalized;
            Vector3 u = Vector3.Cross(d, Vector3.forward);   // the rig frame is z-up
            if (u.sqrMagnitude < 1e-8f) u = Vector3.Cross(d, Vector3.right);
            u.Normalize();
            Vector3 v = Vector3.Cross(d, u);
            Vector3 c0 = (u + v) * h, c1 = (-u + v) * h, c2 = (-u - v) * h, c3 = (u - v) * h;
            AddSide(a, b, c0, c1, attr, verts, norms, attrs, tris);
            AddSide(a, b, c1, c2, attr, verts, norms, attrs, tris);
            AddSide(a, b, c2, c3, attr, verts, norms, attrs, tris);
            AddSide(a, b, c3, c0, attr, verts, norms, attrs, tris);
        }

        private static void AddSide(Vector3 a, Vector3 b, Vector3 o0, Vector3 o1, Vector4 attr,
                                    List<Vector3> verts, List<Vector3> norms, List<Vector4> attrs, List<int> tris)
        {
            int i = verts.Count;
            Vector3 n = (o0 + o1).normalized;
            verts.Add(a + o0); verts.Add(b + o0); verts.Add(b + o1); verts.Add(a + o1);
            for (int k = 0; k < 4; k++) { norms.Add(n); attrs.Add(attr); }
            tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
            tris.Add(i); tris.Add(i + 2); tris.Add(i + 3);
            tris.Add(i); tris.Add(i + 2); tris.Add(i + 1);
            tris.Add(i); tris.Add(i + 3); tris.Add(i + 2);
        }
    }

    /// <summary>
    /// <b>One held prop drawn as a facet mesh on a mesh figure's hand</b> (ADR 0053, the figure mount of
    /// PR 1 and the first visible half of PR 4, for one prop at a time).
    ///
    /// <para><b>The parenting is the design, as it is for a hull's fitting</b>
    /// (<see cref="IsoFacetPropRenderer"/>): the prop's renderer sits UNDER the figure's posed facet child,
    /// so it takes the figure's heading, the ashore frame's projection, and aboard the hull's rock, heave
    /// and tide for free, and it shares the facet pass's depth buffer with the body it is held by. Its
    /// local position is the socket's point in the figure's frame and its local rotation the rig's aim,
    /// both stated by the caller every frame.</para>
    ///
    /// <para><b>The uniforms that belong to the figure</b> — the dither origin, the depth shear and the id
    /// the keyline resolve reads — are copied off the figure's own renderer every frame, so the prop is on
    /// the figure's dither grid and inside its id (a renderer with no id draws as a hole). Its material
    /// joins the figure pixel look with the figure (<see cref="IsoFacetFigureTarget.Sync"/>).</para>
    ///
    /// <para>Allocation-free after <see cref="Configure"/> (rule 7).</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class IsoFacetHeldPropRenderer : MonoBehaviour
    {
        private HeldPropMeshDef _def;
        private Mesh _mesh;
        private Material _facetMaterial;
        private Texture2D _rampTex, _darkRampTex;
        private MeshRenderer _meshRenderer;
        private MaterialPropertyBlock _props;
        private Vector3 _tipLocal;
        private Quaternion _restLocalRotation = Quaternion.identity;
        private Transform _home;

        /// <summary>The def this renderer draws, or null.</summary>
        public HeldPropMeshDef Def => _def;

        /// <summary>True once a def is built and drawable.</summary>
        public bool IsConfigured => _def != null && _mesh != null && _facetMaterial != null;

        /// <summary>The drawn mesh (faces and tubes). For tests.</summary>
        public Mesh DrawnMesh => _mesh;

        /// <summary>How many ramp slots the drawn mesh uses. For tests.</summary>
        public int RampCount { get; private set; }

        /// <summary>The material this prop draws with. For tests.</summary>
        public Material FacetMaterial => _facetMaterial;

        /// <summary>True on a frame the prop is drawn.</summary>
        public bool Visible => _meshRenderer != null && _meshRenderer.enabled;

        /// <summary>The prop's tip in its own metres (<see cref="HeldPropPose.TryTipLocal"/>).</summary>
        public Vector3 TipLocal => _tipLocal;

        /// <summary>The grip, in world space (the camera looks along +Z: x, y are the screen).</summary>
        public Vector3 GripWorld => transform.position;

        /// <summary>The tip, in world space, as drawn this frame (lean included).</summary>
        public Vector3 TipWorld => transform.TransformPoint(_tipLocal);

        /// <summary>
        /// Build the drawable mesh and material for <paramref name="def"/>. The dither is phased on the
        /// FIGURE's cell (<paramref name="ditherPivotPx"/>, <paramref name="ditherPxPerMetre"/>), never the
        /// prop's, for the reason a fitting is phased on its hull's. False, drawing nothing, when the def
        /// cannot be built or plots no tip.
        /// </summary>
        public bool Configure(HeldPropMeshDef def, float lineWidthPx, Vector2 ditherPivotPx, float ditherPxPerMetre,
                              out string why)
        {
            Teardown();
            if (def == null) { why = "no def"; return false; }
            if (!HeldPropPose.TryTipLocal(def, out _tipLocal)) { why = $"{def.Id} plots no tip"; return false; }
            float widthMeters = def.PxPerMetre > 0 ? lineWidthPx / def.PxPerMetre : 0f;
            if (!IsoFacetHeldPropMesh.TryBuild(def, widthMeters, out IsoFacetHeldPropMesh.Built built, out why))
                return false;

            var facetShader = Shader.Find("HiddenHarbours/IsoFacet");
            if (facetShader == null)
            {
                DestroySafely(built.Mesh);
                why = "IsoFacet shader not found";
                return false;
            }

            _def = def;
            _mesh = built.Mesh;
            RampCount = built.Ramps.Length;
            BuildRampTextures(built.Ramps);
            BuildMaterial(facetShader, def, built, ditherPivotPx, ditherPxPerMetre);
            BuildRenderer();
            why = null;
            return true;
        }

        private void BuildRampTextures(Color32[][] ramps)
        {
            int maxLen = 1;
            foreach (Color32[] r in ramps) maxLen = Mathf.Max(maxLen, r.Length);
            _rampTex = MakeRampTexture("HHHeldPropRampTex", maxLen, ramps.Length);
            _darkRampTex = MakeRampTexture("HHHeldPropDarkRampTex", maxLen, ramps.Length);
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
        }

        private static Texture2D MakeRampTexture(string name, int w, int h) =>
            new Texture2D(w, h, TextureFormat.RGBA32, false, false)
            {
                name = name,
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
            };

        private void BuildMaterial(Shader shader, HeldPropMeshDef def, in IsoFacetHeldPropMesh.Built built,
                                   Vector2 ditherPivotPx, float ditherPxPerMetre)
        {
            _facetMaterial = new Material(shader) { name = "HHHeldPropFacet", hideFlags = HideFlags.HideAndDontSave };
            _facetMaterial.SetTexture(IsoFacetShaderIds.RampTex, _rampTex);
            _facetMaterial.SetTexture(IsoFacetShaderIds.DarkRampTex, _darkRampTex);
            _facetMaterial.SetVector(IsoFacetShaderIds.LightN, IsoFacetMath.ShaderLightVector(def.LightN));
            _facetMaterial.SetFloat(IsoFacetShaderIds.Gain, def.Gain);
            _facetMaterial.SetFloat(IsoFacetShaderIds.Bias, def.Bias);
            _facetMaterial.SetColor(IsoFacetShaderIds.KeyColor, ((Color)def.Keyline).linear);
            _facetMaterial.SetVector(IsoFacetShaderIds.PivotPx, ditherPivotPx);
            _facetMaterial.SetFloat(IsoFacetShaderIds.PixelsPerMetre, ditherPxPerMetre);

            var meta = new Vector4[HeldPropMeshDef.RampSlots];
            for (int m = 0; m < built.Ramps.Length; m++)
                meta[m] = IsoFacetRampMeta.Pack(built.Ramps[m].Length, built.RampOffsets[m], built.RampDitherWeights, m);
            _facetMaterial.SetVectorArray(IsoFacetShaderIds.RampMeta, meta);

            var rows = new Vector4[4];
            for (int x = 0; x < 4; x++)
                rows[x] = new Vector4(def.Bayer16[x * 4 + 0], def.Bayer16[x * 4 + 1],
                                      def.Bayer16[x * 4 + 2], def.Bayer16[x * 4 + 3]);
            _facetMaterial.SetVectorArray(IsoFacetShaderIds.Bayer, rows);
        }

        private void BuildRenderer()
        {
            if (!TryGetComponent(out MeshFilter filter)) filter = gameObject.AddComponent<MeshFilter>();
            filter.sharedMesh = _mesh;
            if (!TryGetComponent(out _meshRenderer)) _meshRenderer = gameObject.AddComponent<MeshRenderer>();
            _meshRenderer.sharedMaterial = _facetMaterial;
            _meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            _meshRenderer.receiveShadows = false;
            _meshRenderer.lightProbeUsage = LightProbeUsage.Off;
            _meshRenderer.allowOcclusionWhenDynamic = false;
            _meshRenderer.enabled = false;
        }

        /// <summary>
        /// Draw the prop on <paramref name="figure"/> this frame: under its facet child, the grip at
        /// <paramref name="gripLocal"/> and turned by <paramref name="rotationLocal"/>, both in the
        /// figure's frame. Re-parents only when the facet child changed (boarding, going ashore). False,
        /// and hidden, when the figure has no facet child to hold it.
        /// </summary>
        public bool Mount(IsoCharacterFigureRenderer figure, Vector3 gripLocal, Quaternion rotationLocal)
        {
            Transform facet = figure != null ? figure.FacetTransform : null;
            MeshRenderer body = figure != null ? figure.FacetRenderer : null;
            if (!IsConfigured || facet == null || body == null) { Hide(); return false; }

            if (transform.parent != facet)
            {
                if (_home == null) _home = transform.parent;
                transform.SetParent(facet, false);
                gameObject.layer = facet.gameObject.layer;
            }
            transform.localScale = Vector3.one;
            transform.SetLocalPositionAndRotation(gripLocal, rotationLocal);
            _restLocalRotation = rotationLocal;

            _props ??= new MaterialPropertyBlock();
            body.GetPropertyBlock(_props);
            _meshRenderer.SetPropertyBlock(_props);
            if (!_meshRenderer.enabled) _meshRenderer.enabled = true;
            if (Application.isPlaying) IsoFacetFigureTarget.Sync(_facetMaterial, true);
            return true;
        }

        /// <summary>
        /// Lean the prop in the PICTURE by <paramref name="degrees"/> (counter-clockwise on screen), about
        /// its grip, off the pose <see cref="Mount"/> stated this frame: the fight's load lean, which is a
        /// screen-space read. A turn about the view axis moves no depth. The parent frame mirrors (the
        /// facet projection's <see cref="IsoFacetMath.HullScale"/>), and a mirror reverses a turn's sense,
        /// so the local angle takes the frame's handedness.
        /// </summary>
        public void LeanInPicture(float degrees)
        {
            Transform parent = transform.parent;
            if (parent == null || Mathf.Abs(degrees) < 1e-4f) return;
            Matrix4x4 m = parent.localToWorldMatrix;
            // Through the full matrix, mirror included: InverseTransformDirection drops the scale.
            Vector3 axis = parent.worldToLocalMatrix.MultiplyVector(Vector3.forward);
            if (axis.sqrMagnitude < 1e-12f) return;
            float sense = m.determinant < 0f ? -1f : 1f;
            transform.localRotation = Quaternion.AngleAxis(sense * degrees, axis.normalized) * _restLocalRotation;
        }

        /// <summary>Stop drawing it, and give it back to where it was made, so a figure torn down
        /// under it does not take it along.</summary>
        public void Hide()
        {
            if (_meshRenderer != null && _meshRenderer.enabled) _meshRenderer.enabled = false;
            if (_home != null && transform.parent != _home && _home.gameObject.activeInHierarchy)
                transform.SetParent(_home, false);
        }

        private void OnDestroy() => Teardown();

        private void Teardown()
        {
            if (_mesh != null) DestroySafely(_mesh);
            if (_facetMaterial != null) DestroySafely(_facetMaterial);
            if (_rampTex != null) DestroySafely(_rampTex);
            if (_darkRampTex != null) DestroySafely(_darkRampTex);
            _mesh = null;
            _facetMaterial = null;
            _rampTex = null;
            _darkRampTex = null;
            _def = null;
            RampCount = 0;
            if (_meshRenderer != null) _meshRenderer.enabled = false;
        }

        private static void DestroySafely(UnityEngine.Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Destroy(o);
            else DestroyImmediate(o);
        }
    }
}
