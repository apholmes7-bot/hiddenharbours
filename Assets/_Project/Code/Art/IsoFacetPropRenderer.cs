using System;
using HiddenHarbours.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace HiddenHarbours.Art
{
    /// <summary>Everything the facet pass needs to draw one articulated fitting.</summary>
    public sealed class IsoFacetPropSetup
    {
        public Mesh Mesh;
        public Color32[][] Ramps;
        public int[] RampOffsets;
        public Vector3 LightN;
        public float Gain, Bias;
        public float[] Bayer16;
        public Color32 Keyline;
        public Vector2 PivotPx;
        public int PxPerMetre;
        public int CellW, CellH;
        public float ElevationDeg;

        /// <summary>The point the fitting turns about, in hull-rig metres.</summary>
        public Vector3 PivotLocalMeters;
    }

    /// <summary>
    /// <b>One articulated fitting on a mesh hull (ADR 0022 phase 7)</b> — an oar, an outboard. A
    /// MeshRenderer carrying the same <c>HHHullFacet</c> pass the hull does, parented UNDER the
    /// hull's posed mesh child so it inherits heading, rock and heave for free.
    ///
    /// <para><b>Why that parenting is the whole design.</b> The facet pass does not draw from a list
    /// this code controls — it builds a RendererList filtered by LightMode, so anything wearing the
    /// facet material is drawn into the same off-screen MRT against the same private depth buffer.
    /// A fitting therefore occludes and is occluded by its hull PER PIXEL, for nothing. That single
    /// fact retires both of the sprite path's fitting hacks: the <c>upper</c>/<c>lower</c> part split
    /// (which existed only so the engine's leg could be drawn under the hull on stern-away headings)
    /// and the twin outboard's draw-the-far-engine-first rule. Neither is reimplemented here, because
    /// neither is a real problem once depth is real.</para>
    ///
    /// <para><b>It owns its own material.</b> A fitting usually comes from its own rig with its own
    /// palette, and the facet shader reads material ids as an index into ONE ramp texture — so a
    /// fitting cannot share the hull's. That is one extra material and one extra draw per fitting:
    /// three at most on any boat in the game, against a 60 fps budget that the eleven-hull fleet
    /// does not trouble (rule 7).</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class IsoFacetPropRenderer : MonoBehaviour, IHullPropRenderer
    {
        private IsoFacetPropSetup _setup;
        private Material _facetMaterial;
        private Texture2D _rampTex, _darkRampTex;
        private MeshRenderer _meshRenderer;
        private Transform _meshChild;

        private Quaternion _rotation = Quaternion.identity;
        private float _lateral;
        private bool _dirty = true;

        public bool IsConfigured => _setup != null;

        public Quaternion LocalRotation
        {
            get => _rotation;
            set { if (value != _rotation) { _rotation = value; _dirty = true; } }
        }

        public float LateralOffsetMeters
        {
            get => _lateral;
            set { if (!Mathf.Approximately(value, _lateral)) { _lateral = value; _dirty = true; } }
        }

        public bool Visible
        {
            get => _meshRenderer != null && _meshRenderer.enabled;
            set { if (_meshRenderer != null) _meshRenderer.enabled = value; }
        }

        public void Configure(IsoFacetPropSetup setup)
        {
            _setup = setup ?? throw new ArgumentNullException(nameof(setup));
            Teardown(keepSetup: true);
            BuildRampTextures(setup);
            BuildMaterial(setup);
            BuildChild(setup);
            _dirty = true;
            Apply();
        }

        private void BuildRampTextures(IsoFacetPropSetup setup)
        {
            int maxLen = 0;
            foreach (var ramp in setup.Ramps) maxLen = Mathf.Max(maxLen, ramp.Length);

            _rampTex = MakeRampTexture("HHPropRampTex", maxLen, setup.Ramps.Length);
            _darkRampTex = MakeRampTexture("HHPropDarkRampTex", maxLen, setup.Ramps.Length);

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

        private void BuildMaterial(IsoFacetPropSetup setup)
        {
            var facetShader = Shader.Find("HiddenHarbours/IsoFacet");
            if (facetShader == null)
                throw new InvalidOperationException(
                    "IsoFacet shader not found — see the shader compile-guard test.");

            _facetMaterial = new Material(facetShader) { hideFlags = HideFlags.HideAndDontSave };
            _facetMaterial.SetTexture(IsoFacetShaderIds.RampTex, _rampTex);
            _facetMaterial.SetTexture(IsoFacetShaderIds.DarkRampTex, _darkRampTex);
            _facetMaterial.SetVector(IsoFacetShaderIds.LightN, IsoFacetMath.ShaderLightVector(setup.LightN));
            _facetMaterial.SetFloat(IsoFacetShaderIds.Gain, setup.Gain);
            _facetMaterial.SetFloat(IsoFacetShaderIds.Bias, setup.Bias);
            _facetMaterial.SetColor(IsoFacetShaderIds.KeyColor, ((Color)setup.Keyline).linear);
            // ⚠️ The dither is derived from world position in the HULL-CELL frame, so a fitting must
            // be phased against the SAME pivot and px/metre as the hull it rides — otherwise its
            // dither sits on a different grid and the engine reads as a sticker on the boat. The
            // fitting's own cell is used for its overlay extent, never for its dither phase.
            _facetMaterial.SetVector(IsoFacetShaderIds.PivotPx, setup.PivotPx);
            _facetMaterial.SetFloat(IsoFacetShaderIds.PixelsPerMetre, setup.PxPerMetre);

            var meta = new Vector4[16];
            for (int m = 0; m < setup.Ramps.Length; m++)
                meta[m] = new Vector4(setup.Ramps[m].Length, setup.RampOffsets[m], 0, 0);
            _facetMaterial.SetVectorArray(IsoFacetShaderIds.RampMeta, meta);

            var rows = new Vector4[4];
            for (int x = 0; x < 4; x++)
                rows[x] = new Vector4(setup.Bayer16[x * 4 + 0], setup.Bayer16[x * 4 + 1],
                                      setup.Bayer16[x * 4 + 2], setup.Bayer16[x * 4 + 3]);
            _facetMaterial.SetVectorArray(IsoFacetShaderIds.Bayer, rows);
        }

        private void BuildChild(IsoFacetPropSetup setup)
        {
            var go = new GameObject("FacetProp") { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = setup.Mesh;
            _meshRenderer = go.AddComponent<MeshRenderer>();
            _meshRenderer.sharedMaterial = _facetMaterial;
            _meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            _meshRenderer.receiveShadows = false;
            _meshRenderer.lightProbeUsage = LightProbeUsage.Off;
            _meshRenderer.allowOcclusionWhenDynamic = false;
            _meshChild = go.transform;
        }

        private void LateUpdate() => Apply();

        /// <summary>
        /// Rotate about the pivot, in the hull's frame. A Transform applies rotation THEN translation
        /// (<c>v' = R·v + t</c>), and what a fitting needs is <c>v' = P + R·(v − P)</c>, so the
        /// translation is <c>P − R·P</c> — the classic rotate-about-a-point, and the classic place to
        /// get it wrong. Omitting it rotates the fitting about the hull ORIGIN instead of its own
        /// mount, which swings an outboard clean through the boat; this project has already shipped
        /// that exact bug once on the sprite path.
        /// </summary>
        private void Apply()
        {
            if (!_dirty || _meshChild == null || _setup == null) return;
            Vector3 pivot = _setup.PivotLocalMeters + new Vector3(_lateral, 0f, 0f);
            _meshChild.SetLocalPositionAndRotation(pivot - _rotation * pivot, _rotation);
            _dirty = false;
        }

        private void OnDestroy() => Teardown(keepSetup: false);

        private void Teardown(bool keepSetup)
        {
            if (_meshChild != null) DestroySafely(_meshChild.gameObject);
            if (_facetMaterial != null) DestroySafely(_facetMaterial);
            if (_rampTex != null) DestroySafely(_rampTex);
            if (_darkRampTex != null) DestroySafely(_darkRampTex);
            _meshChild = null;
            _meshRenderer = null;
            _facetMaterial = null;
            _rampTex = null;
            _darkRampTex = null;
            if (!keepSetup) _setup = null;
        }

        static void DestroySafely(UnityEngine.Object o)
        {
            if (Application.isPlaying) Destroy(o);
            else DestroyImmediate(o);
        }
    }
}
