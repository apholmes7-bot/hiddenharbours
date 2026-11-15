using HiddenHarbours.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// <b>Draws every footprint and tyre mark in the active region as ONE mesh: one draw call.</b>
    ///
    /// <para><b>Why one dynamic mesh, not an accumulation texture.</b> A per-region print texture fine
    /// enough for a 0.28 m boot at the game's pixel density would cost megabytes of texture memory per
    /// region (St Peters is ~760 x 520 m), would need a render-to-texture pass per print, and could not
    /// age each print on its own clock. A fixed ring of quads costs four vertices a print
    /// (<see cref="GroundPrintSettings.Capacity"/> 2048 = 8192 vertices, ~200 KB), one tiny generated
    /// texture, one draw call, and fades for free because strength is a function of the clock.
    /// Mobile-safe: no RT, no instancing requirement, no per-frame allocation.</para>
    ///
    /// <para><b>No scene wiring.</b> A <see cref="RuntimeInitializeOnLoadMethod"/> hook subscribes to
    /// <see cref="GroundPrintMade"/> at boot; the hidden host is created only when the first print is
    /// actually kept, so a scene where nobody walks on sand never gets one.</para>
    ///
    /// <para><b>Cosmetic, never saved.</b> Prints live only in memory; a region change clears them.
    /// (Synthesising the prints the villagers' routines would have left is the next slice.)</para>
    /// </summary>
    [DefaultExecutionOrder(100)]   // after the movers have published this frame
    public sealed class GroundPrintRenderer : MonoBehaviour
    {
        private static GroundPrintRenderer s_instance;
        private static bool s_hooked;
        private static readonly LiveGroundPrintGround s_ground = new LiveGroundPrintGround();

        private GroundPrintLayer _layer;
        private Mesh _mesh;
        private Material _material;
        private Texture2D _texture;
        private MeshRenderer _renderer;
        private Vector3[] _vertices;
        private Color32[] _colours;
        private Vector2[] _uvs;
        private string _regionId;
        private bool _dirty;
        private float _nextSlowTick;
        private float _shapeHalfTexel;
        private bool _linearColourSpace;

        /// <summary>The live host, or null until the first print is kept.</summary>
        public static GroundPrintRenderer Instance => s_instance;

        /// <summary>The layer this host draws (for diagnostics and PlayMode probes).</summary>
        public GroundPrintLayer Layer => _layer;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            EventBus.Unsubscribe<GroundPrintMade>(OnPrint);
            s_hooked = false;
            s_instance = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Hook()
        {
            if (s_hooked) return;
            EventBus.Subscribe<GroundPrintMade>(OnPrint);
            s_hooked = true;
        }

        private static void OnPrint(GroundPrintMade m)
        {
            GroundPrintSettings s = GroundPrintSettings.Current;
            if (!s.Enabled) return;
            if (s_instance == null)
            {
                // Only build the host for a print that would actually be kept.
                // (A region with no registered ground, such as one without a zone source, never gets one.)
                if (s_ground.SurfaceAt(m.Position) == GroundSurface.None) return;
                var go = new GameObject("[GroundPrints]");
                DontDestroyOnLoad(go);
                s_instance = go.AddComponent<GroundPrintRenderer>();
                if (!s_instance.Build(s)) { Destroy(go); s_instance = null; return; }
            }
            s_instance.Accept(m);
        }

        private bool Build(in GroundPrintSettings s)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                Debug.LogWarning("[GroundPrints] Sprites/Default shader not found; prints are off.");
                return false;
            }

            int capacity = Mathf.Max(1, s.Capacity);
            _layer = new GroundPrintLayer(capacity);
            _vertices = new Vector3[capacity * 4];
            _colours = new Color32[capacity * 4];
            _uvs = new Vector2[capacity * 4];
            var indices = new int[capacity * 6];
            for (int q = 0; q < capacity; q++)
            {
                int v = q * 4, i = q * 6;
                indices[i] = v; indices[i + 1] = v + 1; indices[i + 2] = v + 2;
                indices[i + 3] = v; indices[i + 4] = v + 2; indices[i + 5] = v + 3;
            }

            _mesh = new Mesh { name = "GroundPrints", hideFlags = HideFlags.DontSave };
            _mesh.indexFormat = capacity * 4 > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            _mesh.MarkDynamic();
            _mesh.SetVertices(_vertices);
            _mesh.SetColors(_colours);
            _mesh.SetUVs(0, _uvs);
            _mesh.SetIndices(indices, MeshTopology.Triangles, 0, false);

            _texture = GroundPrintShapes.BuildTexture(s.ShapeTexturePixels);
            _shapeHalfTexel = 0.5f / _texture.width;
            _linearColourSpace = QualitySettings.activeColorSpace == ColorSpace.Linear;
            _material = new Material(shader) { name = "GroundPrints", hideFlags = HideFlags.DontSave, mainTexture = _texture };

            gameObject.AddComponent<MeshFilter>().sharedMesh = _mesh;
            _renderer = gameObject.AddComponent<MeshRenderer>();
            _renderer.sharedMaterial = _material;
            _renderer.shadowCastingMode = ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            // Mesh renderers only compete with sprites by order inside a SortingGroup (TerrainSplatSurface).
            _renderer.sortingOrder = s.SortingOrder;
            gameObject.AddComponent<SortingGroup>().sortingOrder = s.SortingOrder;
            _regionId = GameServices.CurrentRegionId;
            return true;
        }

        private void Accept(in GroundPrintMade m)
        {
            CheckRegion();
            if (_layer.Accept(m, s_ground) > 0) _dirty = true;
        }

        private void CheckRegion()
        {
            string region = GameServices.CurrentRegionId;
            if (string.Equals(region, _regionId, System.StringComparison.Ordinal)) return;
            _regionId = region;
            _layer.Clear();
            _dirty = true;
        }

        private void LateUpdate()
        {
            IGameClock clock = GameServices.Clock;
            if (_layer == null || clock == null) return;
            GroundPrintSettings s = GroundPrintSettings.Current;
            _renderer.enabled = s.Enabled;
            if (!s.Enabled) return;

            double now = clock.TotalSeconds;
            double lifetime = s.LifetimeSeconds(GameServices.SecondsPerDay);
            if (Time.unscaledTime >= _nextSlowTick)
            {
                _nextSlowTick = Time.unscaledTime + s.SlowTickRealSeconds;
                CheckRegion();
                _layer.Sweep(now, lifetime, s_ground, s.TideChecksPerTick);
                _dirty = true;   // the fade steps are re-read from the clock
            }
            if (_dirty)
            {
                _dirty = false;
                Rebuild(now, lifetime, s);
            }
        }

        private void Rebuild(double now, double lifetime, in GroundPrintSettings s)
        {
            GroundPrintRing ring = _layer.Ring;
            float pixel = GameServices.WorldUnitsPerRenderedPixel;
            int written = ring.Written;
            for (int q = 0; q < ring.Capacity; q++)
            {
                int v = q * 4;
                if (q >= written) { Collapse(v); continue; }
                ref GroundPrint p = ref ring[q];
                if (!p.Alive || !s.TryGetStyle(p.Surface, out GroundPrintSurfaceStyle style))
                {
                    Collapse(v);
                    continue;
                }
                float strength = GroundPrintMath.StyledStrength(p.MadeAt, now, lifetime, s.FadeExponent,
                                                                s.FadeSteps, style.StartStrength);
                if (strength <= 0f)
                {
                    Collapse(v);
                    continue;
                }
                WriteQuad(v, p, strength, style.Tint, pixel, s);
            }
            _mesh.SetVertices(_vertices);
            _mesh.SetColors(_colours);
            _mesh.SetUVs(0, _uvs);
            _mesh.RecalculateBounds();
        }

        private void Collapse(int v)
        {
            for (int k = 0; k < 4; k++) { _vertices[v + k] = Vector3.zero; _colours[v + k] = default; }
        }

        private void WriteQuad(int v, in GroundPrint p, float strength, Color tint, float pixel, in GroundPrintSettings s)
        {
            Vector2 f = GroundPrintMath.SnapHeading(p.Heading, s.HeadingSteps);
            Vector2 r = new Vector2(f.y, -f.x);
            Vector2 c = GroundPrintMath.SnapToPixel(p.Position, pixel);
            Vector2 along = f * (p.LengthMetres * 0.5f);
            Vector2 across = r * (p.WidthMetres * 0.5f);
            _vertices[v] = c - along - across;
            _vertices[v + 1] = c + along - across;
            _vertices[v + 2] = c + along + across;
            _vertices[v + 3] = c - along + across;

            // The tint is sRGB data; a raw mesh's vertex colour is not converted for a linear project.
            var colour = (Color32)GroundPrintMath.InkColour(tint, strength, _linearColourSpace);
            for (int k = 0; k < 4; k++) _colours[v + k] = colour;

            float u0, u1, v1;
            if (p.Kind == GroundPrintKind.TirePair)
            {
                u0 = 0.5f + _shapeHalfTexel; u1 = 1f - _shapeHalfTexel;
                v1 = s.TreadRepeatMetres > 0f ? p.LengthMetres / s.TreadRepeatMetres : 1f;
            }
            else
            {
                u0 = _shapeHalfTexel; u1 = 0.5f - _shapeHalfTexel; v1 = 1f;
                if (p.Kind == GroundPrintKind.FootLeft) { float t = u0; u0 = u1; u1 = t; }   // mirror the left boot
            }
            _uvs[v] = new Vector2(u0, 0f);
            _uvs[v + 1] = new Vector2(u0, v1);
            _uvs[v + 2] = new Vector2(u1, v1);
            _uvs[v + 3] = new Vector2(u1, 0f);
        }

        private void OnDestroy()
        {
            if (s_instance == this) s_instance = null;
            if (_mesh != null) Destroy(_mesh);
            if (_material != null) Destroy(_material);
            if (_texture != null) Destroy(_texture);
        }
    }
}
