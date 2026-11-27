using UnityEngine;
using UnityEngine.Rendering;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// <b>A lighthouse's lantern glass, lit.</b> The owner (11-26): "the beam is soft enough now but make the
    /// lighthouse glasses lenses glow as well". The lamp and its beams are <see cref="SceneLight"/>s at the lens;
    /// this is the glass round them: each pane of the lantern room drawn as lit glass, brightest in its middle
    /// (the lens behind it), with a warm white core where it burns hottest.
    ///
    /// <para><b>The cabin window's way, not a new one.</b> The panes are one static mesh drawn by the shared light
    /// material in its pane mode, exactly as <see cref="BoatWindowGlow"/> draws a lit cabin: additive, pinned at
    /// the lamps' camera depth, gated in the shader on the published day/night tint, so it is off by day and comes
    /// up with the lamps at dusk with no clock read here. No new shader, no new material.</para>
    ///
    /// <para><b>Data only (rule 6).</b> Where the panes are (metres from the tower's foot, as the tower picture
    /// draws them), the colour and how the glass burns are written by the light station's builder step from its
    /// <c>LightStationDef</c>. A tower stands still, so the mesh is laid out once; each frame only re-pins its
    /// depth and order. Visual only, saves nothing, drives nothing.</para>
    ///
    /// <para><b>The spill.</b> The owner (11-26): "you would think the deck around the lighthouse windows would be
    /// glowing a bit from the ambient light". The lit panes throw a soft warm light on the gallery deck, its rail and
    /// the top of the tower: a second mesh of spans drawn by the same material in the pane mode's spill shape. Each
    /// span is an ellipse of light on the deck: brightest in its middle just under the lantern room's foot, falling
    /// off every way at once, so it fades out before the span's sides and ends on curved upper and lower edges that
    /// follow the round gallery, never a box. Stepped in bands on the art pixel grid (the shader snaps each span to its own
    /// art pixels), the last band optionally dithered. Strength, reach and steps are data next to the glass's. One
    /// more mesh draw, which the shader's night gate leaves empty by day, as it does the glass; nothing allocated
    /// per frame.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LanternGlassGlow : MonoBehaviour
    {
        [Tooltip("The lantern's panes, metres from this object (the tower's foot): x across, y up the picture.")]
        [SerializeField] private Rect[] _panes = new Rect[0];

        [Tooltip("The colour of the lit glass (the light's own).")]
        [SerializeField] private Color _colour = new Color(0.965f, 0.839f, 0.518f, 1f);

        [Tooltip("How brightly the glass burns, before the night gate.")]
        [SerializeField] private float _intensity = 0.9f;

        [Tooltip("The brightness at a pane's head (its top), as a share.")]
        [SerializeField] private float _headLevel = 0.75f;

        [Tooltip("The brightness at a pane's sill (its foot), as a share.")]
        [SerializeField] private float _sillLevel = 1f;

        [Tooltip("How much brighter a pane is in its middle: the lens behind the glass. Above about 0.6 in all, the " +
                 "shader pulls the colour toward white: the hot core.")]
        [SerializeField] private float _centreLift = 0.6f;

        [Tooltip("How much dimmer a pane is at its sides: 0 even across, 1 dark at the edges.")]
        [SerializeField] private float _acrossFalloff = 0.35f;

        [Tooltip("Steps of brightness across a pane, for the pixel look: 0 smooth.")]
        [SerializeField] private int _bands = 3;

        [Tooltip("Where the lit glass spills onto the lantern's surroundings (the gallery deck and rail, the top of the " +
                 "tower), metres from the tower's foot: x left edge, y right edge, z the top edge (by the glass, where " +
                 "the spill is strongest), w how far down it reaches before it is gone. None: no spill.")]
        [SerializeField] private Vector4[] _spill = new Vector4[0];

        [Tooltip("How bright the spill is at its strongest, before the night gate (the glass's own scale).")]
        [SerializeField] private float _spillStrength = 0.06f;

        [Tooltip("Where the spill peaks, as a share of a span's height down from its top edge: 0 at the top edge, so " +
                 "it fades only sideways and down; above 0 it fades upward too and the top edge curves.")]
        [SerializeField] private float _spillPeakDepth = 0.3f;

        [Tooltip("Thin the spill's last (dimmest) band to every other art pixel, so its outer edge breaks up.")]
        [SerializeField] private bool _spillDither = true;

        [Tooltip("Steps the spill falls off in, for the pixel look (at least 1: stepped tones, never a smooth wash).")]
        [SerializeField] private int _spillBands = 3;

        static readonly int IdLightColor    = Shader.PropertyToID("_LightColor");
        static readonly int IdIntensity     = Shader.PropertyToID("_Intensity");
        static readonly int IdConeHalfAngle = Shader.PropertyToID("_ConeHalfAngle");
        static readonly int IdEdgeSoftness  = Shader.PropertyToID("_EdgeSoftness");
        static readonly int IdCoreBoost     = Shader.PropertyToID("_CoreBoost");
        static readonly int IdLampPos       = Shader.PropertyToID("_LampPos");
        static readonly int IdThrow         = Shader.PropertyToID("_Throw");
        static readonly int IdGlassCore     = Shader.PropertyToID("_GlassCore");
        static readonly int IdCoreRadius    = Shader.PropertyToID("_CoreRadius");
        static readonly int IdPaneMode      = Shader.PropertyToID("_PaneMode");
        static readonly int IdPaneLook      = Shader.PropertyToID("_PaneLook");
        static readonly int IdPaneFrame     = Shader.PropertyToID("_PaneFrame");
        static readonly int IdPaneLook2     = Shader.PropertyToID("_PaneLook2");
        static readonly int IdPaneTint      = Shader.PropertyToID("_PaneTint");
        static readonly int IdStreakMode    = Shader.PropertyToID("_StreakMode");

        private Transform _node;
        private MeshRenderer _renderer;
        private SortingGroup _sorting;
        private MaterialPropertyBlock _mpb;
        private Mesh _mesh;
        private Vector3 _builtAt;

        // The spill: a second mesh drawn by the same material in the same pane mode, its gradient turned to run
        // from the glass down over the deck, rail and tower top (strongest at the top edge, gone at the bottom).
        private Transform _spillNode;
        private MeshRenderer _spillRenderer;
        private SortingGroup _spillSorting;
        private Mesh _spillMesh;

        /// <summary>The panes, metres from the tower's foot.</summary>
        public Rect[] Panes => _panes ?? System.Array.Empty<Rect>();

        public Color Colour => _colour;
        public float Intensity => _intensity;
        public float CentreLift => _centreLift;

        /// <summary>The renderer the glass is drawn by; null before the first build. Tests and fixtures.</summary>
        public MeshRenderer GlassRenderer => _renderer;

        /// <summary>The laid-out mesh; null before the first build. Tests and fixtures.</summary>
        public Mesh GlassMesh => _mesh;

        /// <summary>The spill spans (x left, y right, z top by the glass, w reach), metres from the tower's foot.</summary>
        public Vector4[] Spill => _spill ?? System.Array.Empty<Vector4>();

        /// <summary>The renderer the spill is drawn by; null before the first build. Tests and fixtures.</summary>
        public MeshRenderer SpillRenderer => _spillRenderer;

        /// <summary>The spill's laid-out mesh; null before the first build. Tests and fixtures.</summary>
        public Mesh SpillMesh => _spillMesh;

        /// <summary>The spill's shape mode, handed to the shader in <c>_PaneLook2.w</c>: 1 the half-ellipse, 2 the
        /// half-ellipse with its last band dithered. 0 (every other pane) is the plain pane.</summary>
        public static float SpillShapeMode(bool dither) => dither ? 2f : 1f;

        /// <summary>The C# copy of the shader's spill shape (HiddenHarboursAdditiveLight, PaneShape's spill branch):
        /// the level of art cell (cx, cy) of a span <paramref name="w"/> by <paramref name="h"/> art pixels, cy
        /// counted up from the span's foot. The ellipse fills the span, its peak in the middle and
        /// <paramref name="peakDepth"/> of the height down from the top edge; inside it the level is 1 minus the
        /// squared distance, cut into <paramref name="bands"/> steps with ceil, so the outermost lit cells sit on the
        /// lowest step and every cell past the ellipse is dark. With <paramref name="dither"/> the lowest step keeps
        /// only every other cell.</summary>
        public static float SpillLevel(int cx, int cy, int w, int h, float peakDepth, int bands, bool dither)
        {
            float sx = Mathf.Max(1, w), sy = Mathf.Max(1, h), b = Mathf.Max(1, bands);
            float u = ((cx + 0.5f) / sx - 0.5f) * 2f;
            float d = 1f - (cy + 0.5f) / sy, pk = Mathf.Clamp01(peakDepth);
            float t = d < pk ? (pk - d) / Mathf.Max(pk, 1e-3f) : (d - pk) / Mathf.Max(1f - pk, 1e-3f);
            float level = Mathf.Clamp01(1f - (u * u + t * t));
            float band = Mathf.Ceil(level * b);
            if (dither && band == 1f && ((cx + cy) & 1) == 1) band = 0f;
            return band / b;
        }

        /// <summary>Author the glass in one call (tests; the builder step writes the same fields as YAML).</summary>
        public void Configure(Rect[] panes, Color colour, float intensity, float headLevel, float sillLevel,
                              float centreLift, float acrossFalloff, int bands)
        {
            _panes = panes ?? new Rect[0];
            _colour = colour; _intensity = intensity; _headLevel = headLevel; _sillLevel = sillLevel;
            _centreLift = centreLift; _acrossFalloff = acrossFalloff; _bands = bands;
            if (_mesh != null) Rebuild();
        }

        /// <summary>Author the spill in one call (tests; the builder step writes the same fields as YAML).</summary>
        public void ConfigureSpill(Vector4[] spans, float strength, float peakDepth, bool ditherLastBand, int bands)
        {
            _spill = spans ?? new Vector4[0];
            _spillStrength = strength; _spillPeakDepth = peakDepth; _spillDither = ditherLastBand; _spillBands = bands;
            if (_mesh != null) Rebuild();
        }

        private void OnEnable() => Rebuild();

        private void OnDisable()
        {
            if (_renderer != null) _renderer.enabled = false;
            if (_spillRenderer != null) _spillRenderer.enabled = false;
        }

        private void OnDestroy()
        {
            Release(ref _mesh);
            Release(ref _spillMesh);
            Release(ref _node);
            Release(ref _spillNode);
        }

        private static void Release(ref Mesh mesh)
        {
            if (mesh == null) return;
            if (Application.isPlaying) Destroy(mesh); else DestroyImmediate(mesh);
            mesh = null;
        }

        private static void Release(ref Transform node)
        {
            if (node == null) return;
            if (Application.isPlaying) Destroy(node.gameObject); else DestroyImmediate(node.gameObject);
            node = null;
        }

        private void LateUpdate()
        {
            if (_mesh == null || _node == null) return;
            if (transform.position != _builtAt) Rebuild();
            Draw();
        }

        /// <summary>Lay the panes out as one mesh: four world corners each, their own 0..1 uv block (the shader's
        /// pane space, v = 0 at the sill), their drawn size in art pixels in uv1, and each its own id in uv2.</summary>
        public void Rebuild()
        {
            EnsureNode();
            _builtAt = transform.position;
            LayOut(_mesh, Panes, StableSeed(name));
            Vector4[] spill = Spill;
            var spans = new Rect[spill.Length];
            for (int i = 0; i < spill.Length; i++) spans[i] = SpillRect(spill[i]);
            LayOut(_spillMesh, spans, StableSeed(name) ^ 0x5f11);
            Draw();
        }

        /// <summary>A spill span (x left, y right, z top, w reach) as the rectangle it covers, metres from the foot.</summary>
        public static Rect SpillRect(Vector4 span) =>
            new Rect(Mathf.Min(span.x, span.y), span.z - Mathf.Max(0f, span.w), Mathf.Abs(span.y - span.x), Mathf.Max(0f, span.w));

        private void LayOut(Mesh mesh, Rect[] panes, int seed)
        {
            int n = panes.Length;
            var verts = new Vector3[n * 4];
            var uvs = new Vector2[n * 4];
            var sizes = new Vector2[n * 4];
            var tris = new int[n * 6];
            float ppm = GlassLook.Current.PixelsPerMetre;
            Vector2 at = transform.position;
            for (int i = 0; i < n; i++)
            {
                Rect r = panes[i];
                int v = i * 4, t = i * 6;
                verts[v + 0] = new Vector3(at.x + r.xMin, at.y + r.yMin, 0f);
                verts[v + 1] = new Vector3(at.x + r.xMax, at.y + r.yMin, 0f);
                verts[v + 2] = new Vector3(at.x + r.xMax, at.y + r.yMax, 0f);
                verts[v + 3] = new Vector3(at.x + r.xMin, at.y + r.yMax, 0f);
                uvs[v + 0] = new Vector2(0f, 0f); uvs[v + 1] = new Vector2(1f, 0f);
                uvs[v + 2] = new Vector2(1f, 1f); uvs[v + 3] = new Vector2(0f, 1f);
                sizes[v + 0] = sizes[v + 1] = sizes[v + 2] = sizes[v + 3] = r.size * ppm;
                tris[t + 0] = v; tris[t + 1] = v + 1; tris[t + 2] = v + 2;
                tris[t + 3] = v; tris[t + 4] = v + 2; tris[t + 5] = v + 3;
            }
            mesh.Clear();
            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.SetUVs(1, sizes);
            mesh.SetUVs(2, BoatWindowGlow.WindowIds(n, seed));
            mesh.triangles = tris;
            mesh.bounds = BoatWindowGlow.PaneBounds(verts, verts.Length);
        }

        private void EnsureNode()
        {
            if (_node == null) MakeNode("LanternPanes", "LanternGlassPanes", out _node, out _mesh, out _renderer, out _sorting);
            if (_spillNode == null) MakeNode("LanternSpill", "LanternGlassSpill", out _spillNode, out _spillMesh, out _spillRenderer, out _spillSorting);
            if (_mpb == null) _mpb = new MaterialPropertyBlock();
        }

        private void MakeNode(string nodeName, string meshName, out Transform node, out Mesh mesh, out MeshRenderer renderer,
                              out SortingGroup sorting)
        {
            var go = new GameObject(nodeName) { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(transform, worldPositionStays: false);
            node = go.transform;
            mesh = new Mesh { name = meshName, hideFlags = HideFlags.DontSave };
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            renderer = go.AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.sortingOrder = SceneLight.MaxSortingOrder;
            // Sorted as 2D for the reason SceneLight's own quad is: a MeshRenderer does not reliably sort against
            // sprites by order alone in the URP 2D renderer.
            sorting = go.AddComponent<SortingGroup>();
            sorting.sortingOrder = SceneLight.MaxSortingOrder;
            sorting.sortAtRoot = true;
            Material mat = SceneLight.SharedLightMaterial();
            if (mat != null) renderer.sharedMaterial = mat;
        }

        /// <summary>Pin a node at the lamps' camera depth with no turn or scale of its own (its vertices are world x/y).</summary>
        private void PinNode(Transform node)
        {
            node.SetPositionAndRotation(new Vector3(0f, 0f, PinnedDepth()), Quaternion.identity);
            Vector3 parent = transform.lossyScale;
            node.localScale = new Vector3(
                Mathf.Approximately(parent.x, 0f) ? 1f : 1f / parent.x,
                Mathf.Approximately(parent.y, 0f) ? 1f : 1f / parent.y,
                Mathf.Approximately(parent.z, 0f) ? 1f : 1f / parent.z);
        }

        private void Draw()
        {
            DrawGlass();
            DrawSpill();
        }

        /// <summary>The spill: the pane mode's spill shape (<see cref="SpillLevel"/>), a stepped ellipse in each span,
        /// no frame. Gated on the night in the shader like the glass, so it is off by day.</summary>
        private void DrawSpill()
        {
            if (_spillRenderer == null) return;
            bool on = isActiveAndEnabled && _spillRenderer.sharedMaterial != null && Spill.Length > 0 && _spillStrength > 0f;
            _spillRenderer.enabled = on;
            if (!on) return;
            PinNode(_spillNode);
            int bands = Mathf.Max(1, _spillBands);
            _spillRenderer.GetPropertyBlock(_mpb);
            SetCommon(_spillStrength);
            _mpb.SetVector(IdPaneLook, new Vector4(1f, Mathf.Clamp01(_spillPeakDepth), 0f, bands));
            _mpb.SetVector(IdPaneLook2, new Vector4(0f, 0f, 0f, SpillShapeMode(_spillDither)));
            // Over the day/night tint, not among the world, so each step adds exactly its own small level to the
            // night frame (the in-world divide scaled the steps by the inverse of the night tint). Nothing stands
            // nearer the camera than a gallery deck to need the occlusion.
            _mpb.SetFloat(SceneLight.IdInWorld, 0f);
            int order = SceneLight.MaxSortingOrder;
            _spillRenderer.SetPropertyBlock(_mpb);
            _spillRenderer.sortingOrder = order;
            if (_spillSorting != null) _spillSorting.sortingOrder = order;
        }

        /// <summary>The fields the glass and the spill share: the light's colour, a radial with no angular cut, the
        /// pane mode with no frame, no core, no streak.</summary>
        private void SetCommon(float intensity)
        {
            _mpb.SetColor(IdLightColor, _colour);
            _mpb.SetFloat(IdIntensity, Mathf.Max(0f, intensity));
            _mpb.SetFloat(IdConeHalfAngle, 180f);       // radial: no angular cut inside a pane
            _mpb.SetFloat(IdEdgeSoftness, 1f);
            _mpb.SetFloat(IdCoreBoost, 0f);
            _mpb.SetVector(IdLampPos, Vector4.zero);
            _mpb.SetFloat(IdThrow, BoatWindowGlow.PaneFillThrow);
            _mpb.SetFloat(IdPaneMode, 1f);
            _mpb.SetVector(IdPaneFrame, new Vector4(0f, 0f, 0f, 0f));   // the picture draws the lantern's own bars
            _mpb.SetColor(IdPaneTint, _colour);
            _mpb.SetFloat(IdGlassCore, 0f);
            _mpb.SetFloat(IdCoreRadius, 0f);
            _mpb.SetFloat(IdStreakMode, 0f);
        }

        /// <summary>Among the world when the glass look says so: one rung over the tower it is set in, so whatever
        /// stands nearer the camera than the tower covers the lantern as it covers the tower. Returns the order.</summary>
        private int SetSort()
        {
            bool inWorld = GlassLook.Current.SortsInWorld;
            _mpb.SetFloat(SceneLight.IdInWorld, inWorld ? 1f : 0f);
            return inWorld ? OrderInWorld(transform.position.y) : SceneLight.MaxSortingOrder;
        }

        private void DrawGlass()
        {
            if (_renderer == null) return;
            bool on = isActiveAndEnabled && _renderer.sharedMaterial != null && Panes.Length > 0;
            _renderer.enabled = on;
            if (!on) return;

            // The vertices are world x/y; the node keeps the lamps' camera depth with no turn or scale of its own.
            PinNode(_node);

            _renderer.GetPropertyBlock(_mpb);
            SetCommon(_intensity);
            _mpb.SetVector(IdPaneLook, new Vector4(_headLevel, _sillLevel, _centreLift, Mathf.Max(0, _bands)));
            _mpb.SetVector(IdPaneLook2, new Vector4(_acrossFalloff, 0f, 0f, 0f));
            int order = SetSort();
            _renderer.SetPropertyBlock(_mpb);
            _renderer.sortingOrder = order;
            if (_sorting != null) _sorting.sortingOrder = order;
        }

        /// <summary>The order the glass sorts at among the world, for a tower whose foot is at
        /// <paramref name="footY"/>: one rung over the tower's own Y-sort.</summary>
        public static int OrderInWorld(float footY) =>
            Mathf.Min(SceneLight.CarrierOrderAtFoot(footY) + SceneLight.RungsOverCarrier, SceneLight.WorldSortCeiling);

        private float PinnedDepth()
        {
            Camera cam = SceneLight.ActiveCamera();
            if (cam == null) return transform.position.z;
            Transform ct = cam.transform;
            return LightMath.CameraDepthZ(ct.position.z, ct.forward.z, cam.nearClipPlane, SceneLight.DefaultCameraDepthOffset);
        }

        private static int StableSeed(string s)
        {
            unchecked
            {
                uint h = 2166136261u;
                for (int i = 0; i < s.Length; i++) h = (h ^ s[i]) * 16777619u;
                return (int)h;
            }
        }
    }
}
