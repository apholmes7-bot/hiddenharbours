using System;
using System.Collections.Generic;
using HiddenHarbours.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// <b>The player as ONE skinned mesh in the facet pass (ADR 0044 d).</b> A CPU-skinned
    /// <c>MeshRenderer</c> wearing the same <c>HHHullFacet</c> pass the hull does, parented under the
    /// hull's posed mesh child so it inherits heading, rock and heave for free — the same shape as
    /// <see cref="IsoFacetPropRenderer"/>, which is an oar or an outboard by the same argument.
    ///
    /// <para><b>Why that parenting IS the deck occlusion.</b> The facet pass builds a RendererList
    /// filtered by LightMode, so everything wearing the facet material is drawn into the same
    /// off-screen MRT against the same private depth buffer, <c>ZWrite On / ZTest LEqual</c>. A figure
    /// standing behind a wheelhouse is therefore hidden by it PER PIXEL, for nothing — no discard, no
    /// second occlusion path, no shader change. The hull's twelve-slot
    /// <c>DeckOccupants</c> publication goes on exactly as before, fed by <c>DeckRiderVisual</c> and
    /// by nobody else: it is what splits the hull's id for the SPRITE path and for the keyline
    /// resolve, and this renderer consumes it without ever claiming a slot of its own.</para>
    ///
    /// <para><b>⚠ IT NEVER REGISTERS AS A HULL.</b> <see cref="IsoFacetHullRenderer"/> registers
    /// itself in <see cref="IsoFacetHullRegistry"/> from <c>OnEnable</c>, unconditionally, and a hull
    /// spends an id plus a twelve-wide fore block out of the 255 the whole fleet shares. A figure
    /// spends neither: configuring her registers NOTHING, and <see cref="IsoFacetHullRegistry.Count"/>
    /// never moves for her. There is a test that says so.</para>
    ///
    /// <para><b>ASHORE — and only through <see cref="EnterAshore"/> — she takes ONE figure id</b>
    /// (<see cref="IsoFacetHullRegistry.RegisterFigure"/>). A single, from the singles stack only, so
    /// it can never be a recycled fore block nor become one; never the shared overflow id 255; and
    /// REFUSED at exhaustion, in which case <see cref="EnterAshore"/> returns false and the caller
    /// keeps her sprite. Holding it raises <see cref="IsoFacetHullRegistry.FigureCount"/>, which is
    /// what opens the facet gate with no hull in the frame. With her id she carries what a hull
    /// carries for herself: a frame that stands in for the posed mesh child (the iso rotation and the
    /// mirror scale), the depth bias and shear at her own root, and a cell-sized overlay quad at the
    /// sort of the sprite she replaces. Nothing in a shipped scene calls it yet (ADR 0044, ashore
    /// PR 1): the switch and the presenter are the next PR's.</para>
    ///
    /// <para><b>⚠ ABOARD SHE IS INKED AS HULL GEOMETRY, with no keyline between her and the deck.</b>
    /// She writes the hull's own <c>_HullId</c>, exactly as a prop does, so the resolve inks her
    /// against the sea and the sky but not against the boat she is standing on. The registry CAN now
    /// hand out a single id safely — the ashore path is exactly that — but an id of her own aboard
    /// would also need an overlay of her own inside the hull's sort, which is a picture decision, not
    /// a bookkeeping one. Aboard is unchanged by the ashore path; that debt is still owed.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class IsoCharacterFigureRenderer : MonoBehaviour
    {
        private CharacterSkinDef _def;
        private Material _facetMaterial;
        private Texture2D _rampTex, _darkRampTex;
        private MeshRenderer _meshRenderer;
        private MeshFilter _meshFilter;
        private Transform _meshChild;
        private Mesh _posedMesh;
        private IsoFacetHullRenderer _hull;
        private MaterialPropertyBlock _props;

        // Ashore only (EnterAshore → LeaveAshore). _figureId 0 ⇔ not ashore.
        private int _figureId;
        private Renderer _sortSource;
        private Transform _ashoreFrame;
        private Transform _overlayChild;
        private MeshRenderer _overlayRenderer;
        private SortingGroup _overlaySortingGroup;
        private Mesh _overlayQuad;
        private Material _overlayMaterial;

        // Bind-mesh sources, read once. The posed mesh is written into every frame the pose changes.
        private Vector3[] _srcVerts, _srcNorms, _outVerts, _outNorms;
        private BoneWeight[] _weights;
        private Matrix4x4[] _bindposes, _world, _skin;

        // stateKey -> the fenced frame map for that clip, built once at Configure.
        private readonly Dictionary<string, int[]> _honestFrames = new Dictionary<string, int[]>();
        private readonly List<string> _fenceReport = new List<string>();

        public bool IsConfigured => _def != null && _posedMesh != null;

        /// <summary>The clip key drawn on the last <see cref="SetPose"/>, or null.</summary>
        public string DrawnStateKey { get; private set; }

        /// <summary>The clip frame ASKED for on the last <see cref="SetPose"/>.</summary>
        public int RequestedFrame { get; private set; } = -1;

        /// <summary>The clip frame actually DRAWN — the same as <see cref="RequestedFrame"/> unless the
        /// fence held an earlier one.</summary>
        public int DrawnFrame { get; private set; } = -1;

        /// <summary>True when the last <see cref="SetPose"/> asked for a poisoned frame and got an
        /// honest one instead.</summary>
        public bool LastFrameWasFenced => RequestedFrame >= 0 && DrawnFrame != RequestedFrame;

        /// <summary>One line per clip that lost frames to the fence, for the PR body and the tests.
        /// Empty when every frame of every clip is honest.</summary>
        public IReadOnlyList<string> FenceReport => _fenceReport;

        public bool Visible
        {
            get => _meshRenderer != null && _meshRenderer.enabled;
            set
            {
                if (_meshRenderer != null) _meshRenderer.enabled = value;
                if (_overlayRenderer != null) _overlayRenderer.enabled = value;
            }
        }

        /// <summary>The hull this figure is riding, or null ashore. Read from the parent chain, not
        /// bound, because a hull re-skinned under her feet is a NEW component.</summary>
        public IsoFacetHullRenderer Hull => _hull;

        /// <summary>True while she holds a figure id and draws with no hull (<see cref="EnterAshore"/>).</summary>
        public bool IsAshore => _figureId != 0;

        /// <summary>Her facet id ashore, in [1, 254]; 0 when not ashore.</summary>
        public int FigureId => _figureId;

        /// <summary>Her overlay quad ashore — the renderer that competes with sprites — or null.</summary>
        public MeshRenderer AshoreOverlay => _overlayRenderer;

        public void Configure(CharacterSkinDef def)
        {
            _def = def ?? throw new ArgumentNullException(nameof(def));
            if (!def.IsUsable())
                throw new InvalidOperationException(
                    $"CharacterSkinDef '{def.Id}' is not usable — it carries no bind mesh, no bones or " +
                    "no clips. Re-bake it (Tools ▸ Hidden Harbours ▸ Bake character SKIN).");

            Teardown(keepDef: true);
            _hull = GetComponentInParent<IsoFacetHullRenderer>();

            ReadBindMesh(def);
            BuildFenceMaps(def);
            BuildRampTextures(def);
            BuildMaterial(def);
            BuildChild();

            DrawnStateKey = null;
            RequestedFrame = DrawnFrame = -1;
        }

        // ---------------------------------------------------------------- the bind mesh

        private void ReadBindMesh(CharacterSkinDef def)
        {
            Mesh bind = def.BindMesh;
            _srcVerts = bind.vertices;
            _srcNorms = bind.normals;
            if (_srcNorms == null || _srcNorms.Length != _srcVerts.Length)
            {
                bind.RecalculateNormals();
                _srcNorms = bind.normals;
            }
            _weights = bind.boneWeights;
            _bindposes = bind.bindposes;

            int bones = def.Bones.Length;
            if (_bindposes == null || _bindposes.Length < bones)
                throw new InvalidOperationException(
                    $"CharacterSkinDef '{def.Id}' has {bones} bones and " +
                    $"{(_bindposes == null ? 0 : _bindposes.Length)} bindposes on its bind mesh.");

            _outVerts = new Vector3[_srcVerts.Length];
            _outNorms = new Vector3[_srcVerts.Length];
            _world = new Matrix4x4[bones];
            _skin = new Matrix4x4[bones];

            // ONE mesh, written into every frame — MarkDynamic so the driver keeps it in a buffer it
            // expects to be rewritten. The topology and every UV channel are copied once, here: only
            // positions and normals move under skinning, and re-uploading the material ids and the
            // triangle list 30 times a second would be the whole cost of the feature (rule 7).
            _posedMesh = new Mesh
            {
                name = "HHCharacterSkinPosed",
                hideFlags = HideFlags.HideAndDontSave,
                indexFormat = bind.indexFormat,
            };
            _posedMesh.MarkDynamic();
            _posedMesh.vertices = _srcVerts;
            _posedMesh.normals = _srcNorms;
            CopyUv(bind, 0); CopyUv(bind, 1); CopyUv(bind, 2); CopyUv(bind, 3);
            _posedMesh.subMeshCount = bind.subMeshCount;
            for (int s = 0; s < bind.subMeshCount; s++)
                _posedMesh.SetTriangles(bind.GetTriangles(s), s, false);
            _posedMesh.bounds = bind.bounds;
        }

        private static readonly List<Vector4> s_Uv = new List<Vector4>();

        private void CopyUv(Mesh bind, int channel)
        {
            bind.GetUVs(channel, s_Uv);
            if (s_Uv.Count > 0) _posedMesh.SetUVs(channel, s_Uv);
        }

        // ---------------------------------------------------------------- the fence

        /// <summary>
        /// Build every clip's honest-frame map once, at Configure, and record which clips lost
        /// frames. Doing it here rather than per-frame is what lets the pose step be a single array
        /// lookup, and it is what makes the fenced span a FACT the PR body can quote rather than a
        /// behaviour somebody has to catch in the act.
        /// </summary>
        private void BuildFenceMaps(CharacterSkinDef def)
        {
            _honestFrames.Clear();
            _fenceReport.Clear();
            int bones = def.Bones.Length;

            foreach (CharacterSkinDef.SkinClip clip in def.Clips)
            {
                int[] map = CharacterSkinPose.BuildHonestFrameMap(
                    clip, bones, CharacterSkinPose.FenceMetres, out int fenced);
                _honestFrames[clip.StateKey] = map;
                if (fenced <= 0) continue;

                _fenceReport.Add($"{clip.StateKey}: {fenced}/{clip.FrameCount} frames fenced " +
                                 $"({FencedSpans(map)})");
            }
        }

        /// <summary>The fenced frames as readable spans ("14-19, 33") — the form the PR body wants.</summary>
        private static string FencedSpans(int[] map)
        {
            var sb = new System.Text.StringBuilder();
            int runStart = -1;
            for (int f = 0; f <= map.Length; f++)
            {
                bool fenced = f < map.Length && map[f] != f;
                if (fenced && runStart < 0) runStart = f;
                else if (!fenced && runStart >= 0)
                {
                    if (sb.Length > 0) sb.Append(", ");
                    sb.Append(runStart);
                    if (f - 1 > runStart) sb.Append('-').Append(f - 1);
                    runStart = -1;
                }
            }
            return sb.ToString();
        }

        // ---------------------------------------------------------------- material

        private void BuildRampTextures(CharacterSkinDef def)
        {
            int count = Mathf.Min(def.Materials.Length, CharacterSkinDef.RampSlots);
            int maxLen = 1;
            for (int m = 0; m < count; m++)
                maxLen = Mathf.Max(maxLen, def.Materials[m].Colors != null ? def.Materials[m].Colors.Length : 1);

            _rampTex = MakeRampTexture("HHCharRampTex", maxLen, count);
            _darkRampTex = MakeRampTexture("HHCharDarkRampTex", maxLen, count);

            var ramps = new Color32[count][];
            for (int m = 0; m < count; m++)
            {
                Color32[] c = def.Materials[m].Colors;
                ramps[m] = c != null && c.Length > 0 ? c : new[] { def.Keyline };
            }
            Color32[][] dark = IsoFacetMath.BuildDarkenedRamps(ramps);

            for (int m = 0; m < count; m++)
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

        /// <summary>
        /// The figure's own facet material, written property for property the way
        /// <see cref="IsoFacetHullRenderer"/> and <see cref="IsoFacetPropRenderer"/> write theirs.
        ///
        /// <para><b>Why this is duplicated rather than shared.</b> Extracting the hull's builder would
        /// be the tidier code and the worse trade for THIS pull request, whose acceptance includes a
        /// byte-identical hull plate: a pure move that turns out not to be pure costs the editor slot
        /// and the fleet. It is held to one authority by a test instead —
        /// <c>TheFigureMaterialMatchesAHullConfiguredFromTheSameSetup</c> compares every property a
        /// facet material carries. Extract it when the hull is not also being plated.</para>
        ///
        /// <para><b>⚠ The per-material gain and bias are NOT written, and cannot be.</b> The def
        /// carries a <c>Gain</c>/<c>Bias</c> per material; the facet shader has one global pair. That
        /// single mismatch is the largest term in the measured 43–57% fidelity gap (53.61% of it) and
        /// it is the shader look pass's to close, not this one's.</para>
        /// </summary>
        private void BuildMaterial(CharacterSkinDef def)
        {
            var facetShader = Shader.Find("HiddenHarbours/IsoFacet");
            if (facetShader == null)
                throw new InvalidOperationException(
                    "IsoFacet shader not found — see the shader compile-guard test.");

            _facetMaterial = new Material(facetShader) { hideFlags = HideFlags.HideAndDontSave };
            _facetMaterial.SetTexture(IsoFacetShaderIds.RampTex, _rampTex);
            _facetMaterial.SetTexture(IsoFacetShaderIds.DarkRampTex, _darkRampTex);
            _facetMaterial.SetVector(IsoFacetShaderIds.LightN, IsoFacetMath.ShaderLightVector(def.LightN));
            _facetMaterial.SetFloat(IsoFacetShaderIds.Gain, def.Gain);
            _facetMaterial.SetFloat(IsoFacetShaderIds.Bias, def.Bias);
            _facetMaterial.SetColor(IsoFacetShaderIds.KeyColor, ((Color)def.Keyline).linear);
            // ⚠️ The dither is indexed from world position in the HULL-CELL frame. A figure phased on
            // her own cell would carry a dither grid that slid across the deck she stands on, which is
            // the ADR 0022 crawl in the one place the eye is always looking. Hers is overwritten from
            // the hull every frame in WriteHullProperties; this is the ashore/no-hull value.
            _facetMaterial.SetVector(IsoFacetShaderIds.PivotPx, def.PivotPx);
            _facetMaterial.SetFloat(IsoFacetShaderIds.PixelsPerMetre, def.PxPerMetre);

            var meta = new Vector4[CharacterSkinDef.RampSlots];
            int count = Mathf.Min(def.Materials.Length, CharacterSkinDef.RampSlots);
            for (int m = 0; m < count; m++)
            {
                Color32[] c = def.Materials[m].Colors;
                meta[m] = new Vector4(c != null && c.Length > 0 ? c.Length : 1, def.Materials[m].Offset, 0, 0);
            }
            _facetMaterial.SetVectorArray(IsoFacetShaderIds.RampMeta, meta);

            // BAYER[x&3][y&3]: row index is X, exactly as the rig holds it.
            var rows = new Vector4[4];
            for (int x = 0; x < 4; x++)
                rows[x] = new Vector4(def.Bayer16[x * 4 + 0], def.Bayer16[x * 4 + 1],
                                      def.Bayer16[x * 4 + 2], def.Bayer16[x * 4 + 3]);
            _facetMaterial.SetVectorArray(IsoFacetShaderIds.Bayer, rows);
        }

        private void BuildChild()
        {
            var go = new GameObject("FacetFigure") { hideFlags = HideFlags.DontSave };
            go.transform.SetParent(transform, false);
            _meshFilter = go.AddComponent<MeshFilter>();
            _meshFilter.sharedMesh = _posedMesh;
            _meshRenderer = go.AddComponent<MeshRenderer>();
            _meshRenderer.sharedMaterial = _facetMaterial;
            _meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            _meshRenderer.receiveShadows = false;
            _meshRenderer.lightProbeUsage = LightProbeUsage.Off;
            _meshRenderer.allowOcclusionWhenDynamic = false;
            _meshChild = go.transform;
        }

        // ---------------------------------------------------------------- posing

        /// <summary>
        /// Draw one frame of one clip. <b>Steps — it never blends.</b> The frame asked for is passed
        /// through the clip's fence map first, so a poisoned frame draws the last honest pose and
        /// <see cref="LastFrameWasFenced"/> says so.
        ///
        /// <para>Re-posing to the same (clip, frame) is free: the skin and the upload are both
        /// skipped. That matters because the clock holds a frame for its whole interval, so most
        /// render frames are repeats.</para>
        /// </summary>
        public bool SetPose(string stateKey, int frame)
        {
            if (!IsConfigured || string.IsNullOrEmpty(stateKey)) return false;
            if (!_def.TryGetClip(stateKey, out CharacterSkinDef.SkinClip clip)) return false;
            if (clip.FrameCount <= 0) return false;

            frame = Mathf.Clamp(frame, 0, clip.FrameCount - 1);
            int drawn = _honestFrames.TryGetValue(stateKey, out int[] map) && frame < map.Length
                        ? map[frame] : frame;

            if (DrawnStateKey == stateKey && DrawnFrame == drawn)
            {
                RequestedFrame = frame;
                return true;
            }

            CharacterSkinPose.ComposeSkinMatrices(clip, drawn, _def.Bones, _bindposes, _world, _skin);
            CharacterSkinPose.Skin(_skin, _weights, _srcVerts, _srcNorms, _outVerts, _outNorms);
            _posedMesh.vertices = _outVerts;
            _posedMesh.normals = _outNorms;
            _posedMesh.RecalculateBounds();

            DrawnStateKey = stateKey;
            RequestedFrame = frame;
            DrawnFrame = drawn;
            return true;
        }

        private void LateUpdate()
        {
            if (IsAshore) WriteAshoreProperties();
            else WriteHullProperties();
        }

        // ---------------------------------------------------------------- ashore

        /// <summary>
        /// <b>Draw her with no hull under her.</b> Takes one figure id, stands a frame in for the
        /// hull's posed mesh child, and builds a cell-sized overlay quad that copies
        /// <paramref name="sortSource"/>'s sorting layer and order every frame. Returns false — and
        /// changes nothing — when she is not configured, when she is standing on a hull (aboard is the
        /// hull's frame, not this one), or when the registry refuses the id at exhaustion; the caller
        /// keeps her sprite in every one of those cases.
        ///
        /// <para>Calling it again while ashore only re-points the sort source. Re-configuring,
        /// disabling or destroying her leaves ashore and gives the id back.</para>
        ///
        /// <para>⚠️ The caller hides the sprite and owns the frame order: this copies the source's
        /// sort in <c>LateUpdate</c>, and a source re-sorted later in the same frame is one frame
        /// stale. Boarding must <see cref="LeaveAshore"/> BEFORE she is parented under a hull.</para>
        /// </summary>
        public bool EnterAshore(Renderer sortSource)
        {
            if (sortSource == null) throw new ArgumentNullException(nameof(sortSource));
            if (!IsConfigured) return false;
            if (IsAshore)
            {
                _sortSource = sortSource;
                WriteAshoreProperties();
                return true;
            }
            if (GetComponentInParent<IsoFacetHullRenderer>() != null) return false;

            // Find the shader BEFORE taking an id, so a missing import cannot leak one.
            var overlayShader = Shader.Find("HiddenHarbours/IsoFacetOverlay");
            if (overlayShader == null)
                throw new InvalidOperationException(
                    "IsoFacetOverlay shader not found — see the shader compile-guard test.");

            int id = IsoFacetHullRegistry.RegisterFigure();
            if (id == 0) return false;

            _figureId = id;
            _sortSource = sortSource;
            _hull = null;
            BuildAshoreFrame();
            BuildAshoreOverlay(overlayShader);
            WriteAshoreProperties();
            return true;
        }

        /// <summary>
        /// Give the figure id back, put the facet child back where <see cref="Configure"/> built it
        /// and destroy the frame and the overlay. A no-op when not ashore, so it never clears an
        /// aboard figure's hull properties.
        /// </summary>
        public void LeaveAshore()
        {
            if (!IsAshore && _ashoreFrame == null && _overlayChild == null) return;

            if (_figureId != 0) IsoFacetHullRegistry.UnregisterFigure(_figureId);
            _figureId = 0;
            _sortSource = null;

            if (_meshChild != null && _meshChild.parent != transform)
            {
                _meshChild.SetParent(transform, false);
                _meshChild.localPosition = Vector3.zero;
                _meshChild.localRotation = Quaternion.identity;
                _meshChild.localScale = Vector3.one;
            }
            if (_meshRenderer != null) _meshRenderer.SetPropertyBlock(null);
            _props?.Clear();

            if (_overlayChild != null) DestroySafely(_overlayChild.gameObject);
            if (_ashoreFrame != null) DestroySafely(_ashoreFrame.gameObject);
            if (_overlayQuad != null) DestroySafely(_overlayQuad);
            if (_overlayMaterial != null) DestroySafely(_overlayMaterial);
            _overlayChild = null;
            _overlayRenderer = null;
            _overlaySortingGroup = null;
            _ashoreFrame = null;
            _overlayQuad = null;
            _overlayMaterial = null;
        }

        /// <summary>Ashore, her facing: the angle the deck presenter writes onto her own transform
        /// aboard (about the rig's up), written here inside the ashore frame instead. No-op aboard.</summary>
        public void SetAshoreYaw(float degrees)
        {
            if (!IsAshore || _meshChild == null) return;
            _meshChild.localRotation = Quaternion.AngleAxis(degrees, Vector3.forward);
        }

        /// <summary>
        /// The frame a hull's posed mesh child would be: <c>HullRotation</c> at heading 0 and the
        /// def's bake elevation, and the <c>HullScale</c> mirror. Her facet child moves under it, so
        /// aboard and ashore she is the same mesh under the same projection.
        /// </summary>
        private void BuildAshoreFrame()
        {
            var go = new GameObject("AshoreFrame") { hideFlags = HideFlags.DontSave, layer = gameObject.layer };
            go.transform.SetParent(transform, false);
            go.transform.localRotation = IsoFacetMath.HullRotation(0d, _def.ElevationDeg);
            go.transform.localScale = IsoFacetMath.HullScale;
            _ashoreFrame = go.transform;

            _meshChild.SetParent(_ashoreFrame, false);
            _meshChild.localPosition = Vector3.zero;
            _meshChild.localRotation = Quaternion.identity;
            _meshChild.localScale = Vector3.one;
        }

        /// <summary>
        /// Her overlay: the def's cell rectangle around the pivot, padded 1 px, built exactly as
        /// <see cref="IsoFacetHullRenderer"/> builds a hull's from its setup — and, like it, under a
        /// SortingGroup, because a mesh renderer does not sort against sprites without one.
        /// </summary>
        private void BuildAshoreOverlay(Shader overlayShader)
        {
            _overlayMaterial = new Material(overlayShader) { hideFlags = HideFlags.HideAndDontSave };

            float ppu = _def.PxPerMetre;
            float pad = 1f / ppu;
            float left = -_def.PivotPx.x / ppu - pad;
            float right = (_def.CellW - _def.PivotPx.x) / ppu + pad;
            float top = _def.PivotPx.y / ppu + pad;
            float bottom = -(_def.CellH - _def.PivotPx.y) / ppu - pad;

            _overlayQuad = new Mesh { name = "HHFigureOverlayQuad", hideFlags = HideFlags.HideAndDontSave };
            _overlayQuad.SetVertices(new[]
            {
                new Vector3(left, bottom, 0f), new Vector3(right, bottom, 0f),
                new Vector3(right, top, 0f), new Vector3(left, top, 0f),
            });
            _overlayQuad.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0);

            var go = new GameObject("FigureOverlay") { hideFlags = HideFlags.DontSave, layer = gameObject.layer };
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = _overlayQuad;
            _overlayRenderer = go.AddComponent<MeshRenderer>();
            _overlayRenderer.sharedMaterial = _overlayMaterial;
            _overlayRenderer.shadowCastingMode = ShadowCastingMode.Off;
            _overlayRenderer.receiveShadows = false;
            _overlayRenderer.lightProbeUsage = LightProbeUsage.Off;
            _overlayRenderer.enabled = _meshRenderer.enabled;
            _overlaySortingGroup = go.AddComponent<SortingGroup>();
            _overlayChild = go.transform;
        }

        /// <summary>
        /// <b>Ashore, what a hull writes for itself</b> — for her own root, with her own id: the
        /// dither origin, the depth shear, the id with NO fore block, the frame's calibrated depth
        /// (bias plus shear compensation, at zero heave, exactly as <c>IsoFacetHullRenderer.ApplyPose</c>
        /// places a hull; no displaced sea ⇒ no frame ⇒ no bias and no shear), and the sort copied
        /// from the source onto the SortingGroup and the overlay, as <c>SetSorting</c> writes a hull's.
        /// </summary>
        internal void WriteAshoreProperties()
        {
            if (!IsAshore || _meshRenderer == null || _overlayRenderer == null) return;

            Vector3 p = transform.position;
            Vector4 shear = Vector4.zero;
            Vector3 framePosition = p;
            if (DisplacedWaterRegistry.TryGetIsoDepthFrame(out WaterIsoDepthFrame frame))
            {
                shear = new Vector4(DisplacedWaterMath.HullDepthShear(_def.ElevationDeg), frame.ReferenceY, 0f, 0f);
                framePosition.z = DisplacedWaterMath.HullDepthBias(p.y, 0f, in frame)
                                  + DisplacedWaterMath.HullShearCompensation(p.y, 0f, shear.x, in frame);
            }
            if (_ashoreFrame.position != framePosition)
                _ashoreFrame.position = framePosition;

            _props ??= new MaterialPropertyBlock();
            _props.SetVector(IsoFacetShaderIds.HullOrigin, new Vector4(p.x, p.y, 0f, 0f));
            _props.SetVector(IsoFacetShaderIds.HullShear, shear);
            _props.SetFloat(IsoFacetShaderIds.HullId, _figureId / 255f);
            _props.SetFloat(IsoFacetShaderIds.HullIdFore, 0f);
            _props.SetFloat(IsoFacetShaderIds.HullIdForeSpan, 0f);
            _meshRenderer.SetPropertyBlock(_props);
            _overlayRenderer.SetPropertyBlock(_props);

            if (_sortSource == null) return;
            int layerId = _sortSource.sortingLayerID;
            int order = _sortSource.sortingOrder;
            if (_overlaySortingGroup.sortingLayerID != layerId) _overlaySortingGroup.sortingLayerID = layerId;
            if (_overlaySortingGroup.sortingOrder != order) _overlaySortingGroup.sortingOrder = order;
            if (_overlayRenderer.sortingLayerID != layerId) _overlayRenderer.sortingLayerID = layerId;
            if (_overlayRenderer.sortingOrder != order) _overlayRenderer.sortingOrder = order;
        }

        // ---------------------------------------------------------------- aboard

        /// <summary>
        /// <b>The uniforms that belong to the BOAT, not to the figure</b> — the dither origin, the
        /// depth shear and the hull id, written per frame into a property block exactly as
        /// <see cref="IsoFacetPropRenderer"/> writes them for a fitting, and for the same reasons:
        /// one dither grid for the whole boat, one depth gradient, and an id the keyline resolve does
        /// not read as empty.
        ///
        /// <para>The hull is re-read every frame rather than bound at Configure, because a boat
        /// re-skinned under her feet (the dev picker does exactly that) is a new component. With no
        /// hull nothing is written; and unless <see cref="EnterAshore"/> gave her a figure id, a frame
        /// with no hull and no figure does not record the facet block at all, so she is not drawn.</para>
        /// </summary>
        private void WriteHullProperties()
        {
            if (_meshRenderer == null) return;
            _hull = GetComponentInParent<IsoFacetHullRenderer>();
            if (_hull == null) return;

            _props ??= new MaterialPropertyBlock();
            Vector3 p = _hull.transform.position;
            _props.SetVector(IsoFacetShaderIds.HullOrigin, new Vector4(p.x, p.y, 0f, 0f));
            _props.SetVector(IsoFacetShaderIds.HullShear, _hull.DepthShear);
            _props.SetFloat(IsoFacetShaderIds.HullId, _hull.HullId / 255f);
            _meshRenderer.SetPropertyBlock(_props);
        }

        // Disabling her must not leave an id holding the facet gate open: leave ashore. The caller
        // re-enters. (Neither callback runs in EditMode for this component — tests call LeaveAshore.)
        private void OnDisable() => LeaveAshore();

        private void OnDestroy() => Teardown(keepDef: false);

        private void Teardown(bool keepDef)
        {
            LeaveAshore();
            if (_meshChild != null) DestroySafely(_meshChild.gameObject);
            if (_posedMesh != null) DestroySafely(_posedMesh);
            if (_facetMaterial != null) DestroySafely(_facetMaterial);
            if (_rampTex != null) DestroySafely(_rampTex);
            if (_darkRampTex != null) DestroySafely(_darkRampTex);
            _meshChild = null;
            _meshRenderer = null;
            _meshFilter = null;
            _posedMesh = null;
            _facetMaterial = null;
            _rampTex = null;
            _darkRampTex = null;
            if (!keepDef) { _def = null; _hull = null; _honestFrames.Clear(); _fenceReport.Clear(); }
        }

        private static void DestroySafely(UnityEngine.Object o)
        {
            if (Application.isPlaying) Destroy(o);
            else DestroyImmediate(o);
        }
    }
}
