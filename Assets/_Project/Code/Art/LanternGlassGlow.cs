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

        /// <summary>The panes, metres from the tower's foot.</summary>
        public Rect[] Panes => _panes ?? System.Array.Empty<Rect>();

        public Color Colour => _colour;
        public float Intensity => _intensity;
        public float CentreLift => _centreLift;

        /// <summary>The renderer the glass is drawn by; null before the first build. Tests and fixtures.</summary>
        public MeshRenderer GlassRenderer => _renderer;

        /// <summary>The laid-out mesh; null before the first build. Tests and fixtures.</summary>
        public Mesh GlassMesh => _mesh;

        /// <summary>Author the glass in one call (tests; the builder step writes the same fields as YAML).</summary>
        public void Configure(Rect[] panes, Color colour, float intensity, float headLevel, float sillLevel,
                              float centreLift, float acrossFalloff, int bands)
        {
            _panes = panes ?? new Rect[0];
            _colour = colour; _intensity = intensity; _headLevel = headLevel; _sillLevel = sillLevel;
            _centreLift = centreLift; _acrossFalloff = acrossFalloff; _bands = bands;
            if (_mesh != null) Rebuild();
        }

        private void OnEnable() => Rebuild();

        private void OnDisable()
        {
            if (_renderer != null) _renderer.enabled = false;
        }

        private void OnDestroy()
        {
            if (_mesh != null)
            {
                if (Application.isPlaying) Destroy(_mesh); else DestroyImmediate(_mesh);
                _mesh = null;
            }
            if (_node != null)
            {
                if (Application.isPlaying) Destroy(_node.gameObject); else DestroyImmediate(_node.gameObject);
                _node = null;
            }
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
            Rect[] panes = Panes;
            EnsureNode();
            _builtAt = transform.position;
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
            _mesh.Clear();
            _mesh.vertices = verts;
            _mesh.uv = uvs;
            _mesh.SetUVs(1, sizes);
            _mesh.SetUVs(2, BoatWindowGlow.WindowIds(n, StableSeed(name)));
            _mesh.triangles = tris;
            _mesh.bounds = BoatWindowGlow.PaneBounds(verts, verts.Length);
            Draw();
        }

        private void EnsureNode()
        {
            if (_node != null) return;
            var go = new GameObject("LanternPanes") { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(transform, worldPositionStays: false);
            _node = go.transform;
            _mesh = new Mesh { name = "LanternGlassPanes", hideFlags = HideFlags.DontSave };
            go.AddComponent<MeshFilter>().sharedMesh = _mesh;
            _renderer = go.AddComponent<MeshRenderer>();
            _renderer.shadowCastingMode = ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            _renderer.lightProbeUsage = LightProbeUsage.Off;
            _renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            _renderer.sortingOrder = SceneLight.MaxSortingOrder;
            // Sorted as 2D for the reason SceneLight's own quad is: a MeshRenderer does not reliably sort against
            // sprites by order alone in the URP 2D renderer.
            _sorting = go.AddComponent<SortingGroup>();
            _sorting.sortingOrder = SceneLight.MaxSortingOrder;
            _sorting.sortAtRoot = true;
            Material mat = SceneLight.SharedLightMaterial();
            if (mat != null) _renderer.sharedMaterial = mat;
            _mpb = new MaterialPropertyBlock();
        }

        private void Draw()
        {
            if (_renderer == null) return;
            bool on = isActiveAndEnabled && _renderer.sharedMaterial != null && Panes.Length > 0;
            _renderer.enabled = on;
            if (!on) return;

            // The vertices are world x/y; the node keeps the lamps' camera depth with no turn or scale of its own.
            _node.SetPositionAndRotation(new Vector3(0f, 0f, PinnedDepth()), Quaternion.identity);
            Vector3 parent = transform.lossyScale;
            _node.localScale = new Vector3(
                Mathf.Approximately(parent.x, 0f) ? 1f : 1f / parent.x,
                Mathf.Approximately(parent.y, 0f) ? 1f : 1f / parent.y,
                Mathf.Approximately(parent.z, 0f) ? 1f : 1f / parent.z);

            _renderer.GetPropertyBlock(_mpb);
            _mpb.SetColor(IdLightColor, _colour);
            _mpb.SetFloat(IdIntensity, Mathf.Max(0f, _intensity));
            _mpb.SetFloat(IdConeHalfAngle, 180f);       // radial: no angular cut inside a pane
            _mpb.SetFloat(IdEdgeSoftness, 1f);
            _mpb.SetFloat(IdCoreBoost, 0f);
            _mpb.SetVector(IdLampPos, Vector4.zero);
            _mpb.SetFloat(IdThrow, BoatWindowGlow.PaneFillThrow);
            _mpb.SetFloat(IdPaneMode, 1f);
            _mpb.SetVector(IdPaneLook, new Vector4(_headLevel, _sillLevel, _centreLift, Mathf.Max(0, _bands)));
            _mpb.SetVector(IdPaneFrame, new Vector4(0f, 0f, 0f, 0f));   // the picture draws the lantern's own bars
            _mpb.SetVector(IdPaneLook2, new Vector4(_acrossFalloff, 0f, 0f, 0f));
            _mpb.SetColor(IdPaneTint, _colour);
            _mpb.SetFloat(IdGlassCore, 0f);
            _mpb.SetFloat(IdCoreRadius, 0f);
            _mpb.SetFloat(IdStreakMode, 0f);

            // Among the world when the glass look says so: one rung over the tower it is set in, so whatever stands
            // nearer the camera than the tower covers the lantern as it covers the tower.
            bool inWorld = GlassLook.Current.SortsInWorld;
            int order = inWorld ? OrderInWorld(transform.position.y) : SceneLight.MaxSortingOrder;
            _mpb.SetFloat(SceneLight.IdInWorld, inWorld ? 1f : 0f);
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
