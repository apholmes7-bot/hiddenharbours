using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace HiddenHarbours.Art
{
    /// <summary>Everything a mesh hull needs to draw — the payload ADR 0022 phase 2's extraction
    /// produces, in plain runtime types. Phase 4 owns turning this into a baked asset; phase 3
    /// deliberately does not invent that format.</summary>
    public sealed class IsoFacetHullSetup
    {
        /// <summary>The extracted hull mesh (RigMeshBuilder layout: flat per-face normals, UV0 =
        /// (materialId, faceBias b, depthBias db, 0)).</summary>
        public Mesh Mesh;
        /// <summary>Palette ramp per rig material, in rig MATS order.</summary>
        public Color32[][] Ramps;
        /// <summary>Per-material constant ramp-index offset (the rig's <c>off</c>).</summary>
        public int[] RampOffsets;
        /// <summary>The rig's LN, normalised, in the rig's own right-handed frame. The component
        /// applies the reflection sign for the shader — hand it over untouched.</summary>
        public Vector3 LightN;
        public float Gain, Bias;
        /// <summary>The rig's 4×4 ordered-dither thresholds, already (v+0.5)/16, row-major
        /// <c>[x*4 + y]</c> — indexed <c>BAYER[x&amp;3][y&amp;3]</c> like the rig.</summary>
        public float[] Bayer16;
        public Color32 Keyline;
        /// <summary>Cell pivot in pixels from the cell's TOP-LEFT — the rig's screen origin, and
        /// the origin the hull-frame dither is phased against (ADR 0022's <c>_DitherPhase</c>).</summary>
        public Vector2 PivotPx;
        public int PxPerMetre;
        public int CellW, CellH;
        /// <summary>The rig's bake elevation (degrees above the horizon; 40 for the boat rigs).</summary>
        public float ElevationDeg;
    }

    /// <summary>
    /// Draws one rig-extracted hull mesh through the facet pipeline (ADR 0022 phase 3).
    ///
    /// <para><b>How the image reaches the screen.</b> This component owns two children:</para>
    /// <list type="bullet">
    /// <item><b>"FacetMesh"</b> — a MeshRenderer whose only shader pass is LightMode
    /// <c>HHHullFacet</c>. The 2D renderer's own passes skip it (no Universal2D/SRPDefaultUnlit
    /// pass); it exists so URP culls it and <see cref="IsoFacetHullFeature"/>'s renderer list can
    /// draw it OFF-SCREEN with a private z-buffer. It must not draw in-scene: a mesh writing the
    /// shared depth buffer punches holes in every later sprite that z-tests (the hull's bow sits
    /// at z &lt; 0, closer than every sprite's z = 0 plane).</item>
    /// <item><b>"HullOverlay"</b> — a cell-sized quad with an ordinary Universal2D pass that
    /// re-composes THIS hull's pixels (keyline included) from the feature's resolved screen
    /// texture. The quad is what sorts against SpriteRenderers, whole-object, through the same
    /// SortingGroup workaround mesh renderers already need — so a crew sprite above the boat
    /// covers hull AND keyline, exactly as it covers a baked sprite's inked outline.</item>
    /// </list>
    ///
    /// <para><b>Hull-frame dither with no calibration.</b> The spike phased a screen-space Bayer
    /// lookup per render-target convention (<c>_DitherPhase</c>, probed at runtime). Production
    /// derives the dither index in the FRAGMENT from world position instead:
    /// <c>cell = (worldXY − hullOrigin)·PPU (+pivot, y flipped)</c> — the same number by
    /// construction, immune to render-target y-flip conventions, and locked to the hull's frame
    /// so translation cannot make it crawl (the 13–16% crawl class ADR 0022 measured).</para>
    ///
    /// <para><b>Pose inputs are RIG units</b> (<see cref="HeadingDirUnits"/>: 1 = 45° CCW,
    /// fractional = continuous heading; roll/pitch degrees; heave pixels). Mapping the game's
    /// compass heading (and the per-artwork mirror question) onto rig units is phase 4's seam —
    /// keeping it out of here keeps the golden master a pure statement about rendering.</para>
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class IsoFacetHullRenderer : MonoBehaviour
    {
        [Tooltip("Heading in RIG dir units (1 unit = 45°, CCW, fractional allowed).")]
        [SerializeField] private float _headingDirUnits;
        [SerializeField] private float _rollDegrees;
        [SerializeField] private float _pitchDegrees;
        [SerializeField] private float _heavePixels;

        private IsoFacetHullSetup _setup;
        private Material _facetMaterial;
        private Material _overlayMaterial;
        private Texture2D _rampTex;
        private Texture2D _darkRampTex;
        private Mesh _overlayQuad;
        private Transform _meshChild;
        private MeshRenderer _meshRenderer;
        private MeshRenderer _overlayRenderer;
        private MaterialPropertyBlock _props;
        private int _hullId;
        private bool _poseDirty = true;

        /// <summary>Heading in rig dir units (1 = 45° CCW). Continuous — that is the point.</summary>
        public float HeadingDirUnits
        {
            get => _headingDirUnits;
            set { if (!Mathf.Approximately(_headingDirUnits, value)) { _headingDirUnits = value; _poseDirty = true; } }
        }

        public float RollDegrees
        {
            get => _rollDegrees;
            set { if (!Mathf.Approximately(_rollDegrees, value)) { _rollDegrees = value; _poseDirty = true; } }
        }

        public float PitchDegrees
        {
            get => _pitchDegrees;
            set { if (!Mathf.Approximately(_pitchDegrees, value)) { _pitchDegrees = value; _poseDirty = true; } }
        }

        /// <summary>Heave in rig PIXELS (the rig's own unit; world metres = px / PPU).</summary>
        public float HeavePixels
        {
            get => _heavePixels;
            set { if (!Mathf.Approximately(_heavePixels, value)) { _heavePixels = value; _poseDirty = true; } }
        }

        /// <summary>The id in [1,255] this hull writes into the facet buffer's alpha. 0 = not registered.</summary>
        public int HullId => _hullId;

        public bool IsConfigured => _setup != null;

        /// <summary>The overlay quad's renderer — set sorting layer/order here (it is the only
        /// child that draws in-scene, and the thing that must sort against sprites).</summary>
        public MeshRenderer OverlayRenderer => _overlayRenderer;

        /// <summary>
        /// Build all GPU-side state from an extracted hull. Call once (idempotent: re-configuring
        /// releases the previous state). Everything created here is owned and destroyed by this
        /// component.
        /// </summary>
        public void Configure(IsoFacetHullSetup setup)
        {
            if (setup == null) throw new ArgumentNullException(nameof(setup));
            if (setup.Mesh == null) throw new ArgumentException("Setup has no mesh.", nameof(setup));
            if (setup.Ramps == null || setup.Ramps.Length == 0)
                throw new ArgumentException("Setup has no ramps.", nameof(setup));
            if (setup.Ramps.Length > 16)
                throw new ArgumentException($"{setup.Ramps.Length} materials; the facet shader's _RampMeta holds 16.");
            if (setup.Bayer16 == null || setup.Bayer16.Length != 16)
                throw new ArgumentException("Bayer16 must be exactly 16 thresholds.", nameof(setup));

            ReleaseOwned();
            _setup = setup;

            BuildRampTextures(setup);
            BuildFacetMaterial(setup);
            BuildChildren(setup);
            _poseDirty = true;
            ApplyPose();
        }

        private void BuildRampTextures(IsoFacetHullSetup setup)
        {
            int maxLen = 0;
            foreach (var ramp in setup.Ramps) maxLen = Mathf.Max(maxLen, ramp.Length);

            // sRGB textures (linear:false): the ramp bytes are sRGB palette values; the GPU decodes
            // on Load and re-encodes into the sRGB targets, so palette bytes survive the trip.
            _rampTex = MakeRampTexture("HHRampTex", maxLen, setup.Ramps.Length);
            _darkRampTex = MakeRampTexture("HHDarkRampTex", maxLen, setup.Ramps.Length);

            Color32[][] dark = IsoFacetMath.BuildDarkenedRamps(setup.Ramps);
            for (int m = 0; m < setup.Ramps.Length; m++)
            {
                var ramp = setup.Ramps[m];
                for (int i = 0; i < maxLen; i++)
                {
                    int k = Mathf.Min(i, ramp.Length - 1);
                    _rampTex.SetPixel(i, m, ramp[k]);
                    _darkRampTex.SetPixel(i, m, dark[m][k]);
                }
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

        private void BuildFacetMaterial(IsoFacetHullSetup setup)
        {
            var facetShader = Shader.Find("HiddenHarbours/IsoFacet");
            var overlayShader = Shader.Find("HiddenHarbours/IsoFacetOverlay");
            if (facetShader == null || overlayShader == null)
                throw new InvalidOperationException(
                    "IsoFacet shaders not found — HiddenHarbours/IsoFacet and " +
                    "HiddenHarbours/IsoFacetOverlay must both import (see the compile guard test).");

            _facetMaterial = new Material(facetShader) { hideFlags = HideFlags.HideAndDontSave };
            _facetMaterial.SetTexture(IsoFacetShaderIds.RampTex, _rampTex);
            _facetMaterial.SetTexture(IsoFacetShaderIds.DarkRampTex, _darkRampTex);
            _facetMaterial.SetVector(IsoFacetShaderIds.LightN, IsoFacetMath.ShaderLightVector(setup.LightN));
            _facetMaterial.SetFloat(IsoFacetShaderIds.Gain, setup.Gain);
            _facetMaterial.SetFloat(IsoFacetShaderIds.Bias, setup.Bias);
            // The keyline colour crosses the CPU→GPU boundary as a colour PROPERTY, which is raw
            // floats: hand it over pre-linearised so the sRGB render target re-encodes the exact
            // palette byte.
            _facetMaterial.SetColor(IsoFacetShaderIds.KeyColor, ((Color)setup.Keyline).linear);
            _facetMaterial.SetVector(IsoFacetShaderIds.PivotPx, setup.PivotPx);
            _facetMaterial.SetFloat(IsoFacetShaderIds.PixelsPerMetre, setup.PxPerMetre);

            var meta = new Vector4[16];
            for (int m = 0; m < setup.Ramps.Length; m++)
                meta[m] = new Vector4(setup.Ramps[m].Length, setup.RampOffsets[m], 0, 0);
            _facetMaterial.SetVectorArray(IsoFacetShaderIds.RampMeta, meta);

            // BAYER[x&3][y&3]: row index is X, exactly as the rig holds it.
            var rows = new Vector4[4];
            for (int x = 0; x < 4; x++)
                rows[x] = new Vector4(setup.Bayer16[x * 4 + 0], setup.Bayer16[x * 4 + 1],
                                      setup.Bayer16[x * 4 + 2], setup.Bayer16[x * 4 + 3]);
            _facetMaterial.SetVectorArray(IsoFacetShaderIds.Bayer, rows);

            _overlayMaterial = new Material(overlayShader) { hideFlags = HideFlags.HideAndDontSave };
        }

        private void BuildChildren(IsoFacetHullSetup setup)
        {
            var meshGo = new GameObject("FacetMesh") { hideFlags = HideFlags.DontSave };
            meshGo.transform.SetParent(transform, false);
            meshGo.AddComponent<MeshFilter>().sharedMesh = setup.Mesh;
            _meshRenderer = meshGo.AddComponent<MeshRenderer>();
            _meshRenderer.sharedMaterial = _facetMaterial;
            _meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            _meshRenderer.receiveShadows = false;
            _meshRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            _meshRenderer.allowOcclusionWhenDynamic = false;
            _meshChild = meshGo.transform;

            // The overlay quad: the cell rectangle in world metres around the pivot, padded 1 px
            // on every side so the keyline (which floods 1 px OUTSIDE the silhouette) is inside it
            // even when the silhouette touches the cell edge.
            float ppu = setup.PxPerMetre;
            float pad = 1f / ppu;
            float left = -setup.PivotPx.x / ppu - pad;
            float right = (setup.CellW - setup.PivotPx.x) / ppu + pad;
            float top = setup.PivotPx.y / ppu + pad;
            float bottom = -(setup.CellH - setup.PivotPx.y) / ppu - pad;

            _overlayQuad = new Mesh { name = "HHHullOverlayQuad", hideFlags = HideFlags.HideAndDontSave };
            _overlayQuad.SetVertices(new[]
            {
                new Vector3(left, bottom, 0f), new Vector3(right, bottom, 0f),
                new Vector3(right, top, 0f), new Vector3(left, top, 0f),
            });
            _overlayQuad.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0);

            var overlayGo = new GameObject("HullOverlay") { hideFlags = HideFlags.DontSave };
            overlayGo.transform.SetParent(transform, false);
            overlayGo.AddComponent<MeshFilter>().sharedMesh = _overlayQuad;
            _overlayRenderer = overlayGo.AddComponent<MeshRenderer>();
            _overlayRenderer.sharedMaterial = _overlayMaterial;
            _overlayRenderer.shadowCastingMode = ShadowCastingMode.Off;
            _overlayRenderer.receiveShadows = false;
            _overlayRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;

            // Mesh renderers do not sort against sprites on their own (they fall back to world z)
            // — the documented workaround is a SortingGroup ("sort as 2D"), same as the water.
            if (GetComponent<SortingGroup>() == null)
                gameObject.AddComponent<SortingGroup>();
        }

        private void OnEnable()
        {
            if (_hullId == 0)
                _hullId = IsoFacetHullRegistry.Register(this);
            _poseDirty = true;
        }

        private void OnDisable()
        {
            if (_hullId != 0)
            {
                IsoFacetHullRegistry.Unregister(this, _hullId);
                _hullId = 0;
            }
        }

        private void OnDestroy() => ReleaseOwned();

        private void OnValidate() => _poseDirty = true;

        private void LateUpdate() => ApplyPose();

        /// <summary>
        /// Push the current pose to the transform and the per-draw material properties. Runs in
        /// LateUpdate (after gameplay wrote the pose) and allocates nothing — the property block
        /// and children are cached.
        /// </summary>
        public void ApplyPose()
        {
            if (_setup == null || _meshChild == null) return;

            if (_poseDirty)
            {
                _meshChild.localRotation = IsoFacetMath.HullRotation(
                    _headingDirUnits, _setup.ElevationDeg, _rollDegrees, _pitchDegrees);
                _meshChild.localScale = IsoFacetMath.HullScale;
                _meshChild.localPosition = IsoFacetMath.HeaveOffset(_heavePixels, _setup.PxPerMetre);
                _poseDirty = false;
            }

            _props ??= new MaterialPropertyBlock();
            // The hull ORIGIN the dither grid is phased against is this root — NOT the heaved
            // child: the rig subtracts heave from screen y AFTER projecting, so its dither stays
            // indexed by the final screen pixel, which the world-derived cell coordinate
            // reproduces only when the origin excludes the heave offset.
            Vector3 p = transform.position;
            _props.SetVector(IsoFacetShaderIds.HullOrigin, new Vector4(p.x, p.y, 0f, 0f));
            _props.SetFloat(IsoFacetShaderIds.HullId, _hullId / 255f);
            _meshRenderer.SetPropertyBlock(_props);
            _overlayRenderer.SetPropertyBlock(_props);
        }

        private void ReleaseOwned()
        {
            static void Kill(UnityEngine.Object o)
            {
                if (o == null) return;
                if (Application.isPlaying) Destroy(o); else DestroyImmediate(o);
            }

            if (_meshChild != null) Kill(_meshChild.gameObject);
            if (_overlayRenderer != null) Kill(_overlayRenderer.gameObject);
            Kill(_facetMaterial);
            Kill(_overlayMaterial);
            Kill(_rampTex);
            Kill(_darkRampTex);
            Kill(_overlayQuad);
            _meshChild = null;
            _meshRenderer = null;
            _overlayRenderer = null;
            _facetMaterial = null;
            _overlayMaterial = null;
            _rampTex = null;
            _darkRampTex = null;
            _overlayQuad = null;
            _setup = null;
        }
    }
}
