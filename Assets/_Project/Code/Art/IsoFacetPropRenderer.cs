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

        /// <summary>The part of the fitting that does NOT articulate (the outboard's clamp bracket),
        /// or null. Same material, same lateral mount, no rotation.</summary>
        public Mesh FixedMesh;

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
        // The bolted half's renderer. ⚠️ It MUST be held, not discarded: it needs the same per-boat
        // property block as the swivelling half, and a facet renderer with no _HullId writes alpha 0
        // — the reserved "no hull here" — which the keyline resolve reads as EMPTY and every overlay
        // quad then clips. See WriteHullProperties.
        private MeshRenderer _fixedRenderer;
        private Transform _meshChild, _fixedChild;
        private IsoFacetHullRenderer _hull;
        private MaterialPropertyBlock _props;

        private Quaternion _rotation = Quaternion.identity;
        private float _lateral;
        private Vector3 _fitment;
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

        public Vector3 FitmentOffsetMeters
        {
            get => _fitment;
            set { if (value != _fitment) { _fitment = value; _dirty = true; } }
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
            // The hull this fitting is bolted to — for the dither ORIGIN and the hull id, both of
            // which are facts about the boat rather than about the part. See WriteHullProperties.
            _hull = GetComponentInParent<IsoFacetHullRenderer>();
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
            _meshRenderer = MakeChild("FacetProp", setup.Mesh, out _meshChild);
            // The bolted-down half — the outboard's clamp bracket, which the engine swivels ON. A
            // second child rather than a second fitting: same asset, same palette, same material,
            // same lateral mount, and it simply never takes the rotation. Both children join the same
            // LightMode renderer list, so they share the hull's depth buffer like everything else.
            if (setup.FixedMesh != null)
                _fixedRenderer = MakeChild("FacetPropFixed", setup.FixedMesh, out _fixedChild);
        }

        private MeshRenderer MakeChild(string name, Mesh mesh, out Transform child)
        {
            var go = new GameObject(name) { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = _facetMaterial;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.lightProbeUsage = LightProbeUsage.Off;
            r.allowOcclusionWhenDynamic = false;
            child = go.transform;
            return r;
        }

        private void LateUpdate()
        {
            Apply();
            WriteHullProperties();
        }

        /// <summary>
        /// <b>The two uniforms that belong to the BOAT, not to the part</b>, written per frame into a
        /// property block exactly as <see cref="IsoFacetHullRenderer"/> writes them for the hull.
        ///
        /// <para><b>Why a fitting needs them at all.</b> The facet shader derives its ordered-dither
        /// index from WORLD position relative to <c>_HullOrigin</c> — that is the whole fix ADR 0022
        /// measured at 13–16% dither crawl and drove to 0.00%. A renderer that never writes it leaves
        /// the uniform at the world origin, so the dither slides across the part as the boat sails: on
        /// a large flat cowl panel that is the crawl the ADR eliminated, reintroduced on the one piece
        /// of the boat with the biggest unbroken panels. <c>_HullId</c> is the same story for the
        /// keyline resolve, which floods the neighbour's key colour AND id: id 0 reads as "no hull".</para>
        ///
        /// <para>⚠️ The origin is the hull's ROOT, not this fitting's transform and not the heaved mesh
        /// child — the rig subtracts heave from screen y AFTER projecting, so the dither stays indexed
        /// by the final screen pixel only when the origin excludes it. Same reasoning, same value, as
        /// the hull's own write; taking it from the hull is what keeps them on ONE grid.</para>
        /// </summary>
        private void WriteHullProperties()
        {
            if (_meshRenderer == null || _hull == null) return;
            _props ??= new MaterialPropertyBlock();
            Vector3 p = _hull.transform.position;
            _props.SetVector(IsoFacetShaderIds.HullOrigin, new Vector4(p.x, p.y, 0f, 0f));
            _props.SetFloat(IsoFacetShaderIds.HullId, _hull.HullId / 255f);
            _meshRenderer.SetPropertyBlock(_props);
            // ⚠️ AND THE BOLTED HALF (owner playtest 2026-07-25). Its renderer used to be discarded
            // at the MakeChild call, so it never received this block. `_HullId` is not in the
            // shader's Properties list and nothing sets it globally, so it rasterised at 0 — the
            // reserved "no hull here" alpha. The keyline resolve reads those pixels as EMPTY and
            // every overlay quad clips them, while the bracket's `ZWrite On` has already won the
            // shared z-test against the transom it clamps onto: a bracket-shaped hole punched clean
            // through the stern, showing whatever sorts under every boat — the sea. Live on all four
            // motor defs (punt basic/upgraded, skiff work/sport); the dory's oars carry no FixedMesh,
            // which is why rowing her never showed it. The same omission left _HullOrigin at the
            // world origin, so its dither crawled too — the ADR 0022 defect, reintroduced.
            if (_fixedRenderer != null)
                _fixedRenderer.SetPropertyBlock(_props);
        }

        /// <summary>
        /// Rotate about the pivot, in the hull's frame — the arithmetic itself lives in
        /// <see cref="HullPropFitment"/>, where it is proven headlessly, because this is the classic
        /// place to get a rotate-about-a-point wrong. Omitting the translation rotates the fitting
        /// about the hull ORIGIN instead of its own mount, which swings an outboard clean through the
        /// boat; this project has already shipped that exact bug once on the sprite path.
        /// </summary>
        private void Apply()
        {
            if (!_dirty || _meshChild == null || _setup == null) return;
            _meshChild.SetLocalPositionAndRotation(
                HullPropFitment.LocalPosition(_setup.PivotLocalMeters, _lateral, _fitment, _rotation),
                _rotation);
            // The bolted-down half takes the clamp offset and NOTHING else — it is fixed to the
            // transom, which is the whole reason it is a separate child.
            if (_fixedChild != null)
                _fixedChild.SetLocalPositionAndRotation(
                    HullPropFitment.FixedLocalPosition(_lateral, _fitment), Quaternion.identity);
            _dirty = false;
        }

        private void OnDestroy() => Teardown(keepSetup: false);

        private void Teardown(bool keepSetup)
        {
            if (_meshChild != null) DestroySafely(_meshChild.gameObject);
            if (_fixedChild != null) DestroySafely(_fixedChild.gameObject);
            if (_facetMaterial != null) DestroySafely(_facetMaterial);
            if (_rampTex != null) DestroySafely(_rampTex);
            if (_darkRampTex != null) DestroySafely(_darkRampTex);
            _meshChild = null;
            _fixedChild = null;
            _meshRenderer = null;
            _fixedRenderer = null;
            _facetMaterial = null;
            _rampTex = null;
            _darkRampTex = null;
            if (!keepSetup) { _setup = null; _hull = null; }
        }

        static void DestroySafely(UnityEngine.Object o)
        {
            if (Application.isPlaying) Destroy(o);
            else DestroyImmediate(o);
        }
    }
}
