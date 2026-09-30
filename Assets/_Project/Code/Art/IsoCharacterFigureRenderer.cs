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

        // Rig 9's face, look and ink (character PR 2a). Null / zero on a def that carries none of them,
        // which then draws exactly as it did before them.
        private int[] _faceStarts, _faceCounts;   // the bind mesh's faces as vertex runs: the Newell normals
        private Texture2D _stepRampTex;           // every ramp one step down: the rig's edge drop
        private Vector4 _faceUniform;             // (eyes, brows, mouth, 0): the one group per slot drawn
        private double _lookYaw, _lookPitch;      // the turn sampled on the last beat, as the rig rounds it
        private double _drawnYaw, _drawnPitch;    // ... and the turn the posed mesh carries
        private int _gaze;
        private bool _inkApplied, _inkJoined;

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
                SyncInk();
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

        /// <summary>
        /// <b>What a figure does between its clip's keys</b> (rig 9 README §4 and §5): where it looks and
        /// whether it is mid-blink. Handed to <see cref="SetPose(string, int, in Life)"/> by the presenter,
        /// which owns the clock, the figure's identity and the switches; the default is no look and no
        /// blink, which draws exactly the clip.
        /// </summary>
        public struct Life
        {
            /// <summary>True when <see cref="Target"/> names something to look at.</summary>
            public bool HasTarget;

            /// <summary>The point looked at, in the figure's OWN frame (rig metres, rig axes: +x right,
            /// +y forward, +z up, the feet at the origin), unrocked — <see cref="TryFigureGround"/> puts a
            /// world point there.</summary>
            public Vector3 Target;

            /// <summary>Turn the neck and head (<see cref="GameConfig.CharacterHeadLook"/>).</summary>
            public bool HeadLook;

            /// <summary>Let the open eyes finish the turn (<see cref="GameConfig.CharacterEyeLook"/>).</summary>
            public bool EyeLook;

            /// <summary>The blink's eyes group at this moment (<see cref="CharacterFigureBlink.EyesAt"/>),
            /// or <see cref="CharacterSkinDef.NoFaceGroup"/> between blinks.</summary>
            public int BlinkEyes;
        }

        /// <summary>The face drawn by the last <see cref="SetPose(string, int, in Life)"/>: the group id
        /// (1-based into <see cref="CharacterSkinDef.FaceGroups"/>) per slot — x eyes, y brows, z mouth.
        /// All zero on a def with no face.</summary>
        public Vector3Int DrawnFace => new Vector3Int((int)_faceUniform.x, (int)_faceUniform.y, (int)_faceUniform.z);

        /// <summary>The look's yaw the posed mesh carries, degrees (0 with no look).</summary>
        public double DrawnLookYaw => _drawnYaw;

        /// <summary>The look's pitch the posed mesh carries, degrees (0 with no look).</summary>
        public double DrawnLookPitch => _drawnPitch;

        /// <summary>The gaze the last beat sampled: <see cref="CharacterFigureLook.GazeOpen"/>,
        /// <see cref="CharacterFigureLook.GazeLeft"/> or <see cref="CharacterFigureLook.GazeRight"/>.</summary>
        public int DrawnGaze => _gaze;

        /// <summary>The head's mid point the snap rounds, in the figure's frame, and whether it snaps
        /// (w = 1): what the facet shader's head snap reads.</summary>
        public Vector4 HeadSnapPoint { get; private set; }

        /// <summary>True while this figure is inked by the rig's own rules
        /// (<see cref="GameConfig.MeshFigureKeyline"/> on and a def that <see cref="CharacterSkinDef.HasInk"/>).</summary>
        public bool InkLive => _inkApplied;

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
            _lookYaw = _lookPitch = _drawnYaw = _drawnPitch = 0d;
            _gaze = CharacterFigureLook.GazeOpen;
            SyncInk();
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

            ReadFaceRuns(def, bind);
        }

        /// <summary>
        /// <b>Rig 9's faces as vertex runs, for the rig's own normal.</b> A def with the face mechanism
        /// culls each face as the rig does, by its <c>toward</c>, and the facet shader decides that per
        /// VERTEX: so every corner of a face must carry the face's one normal, or a face whose corners
        /// ride two bones (a hem) would be culled at some corners and drawn at others. The posed Newell
        /// normal (<see cref="CharacterSkinPose.NewellNormals"/>) is that normal, and it is the one the
        /// rig shades by. A def without the face keeps the skinned normals it always had.
        /// </summary>
        private void ReadFaceRuns(CharacterSkinDef def, Mesh bind)
        {
            _faceStarts = _faceCounts = null;
            if (!def.HasFace) return;
            if (!CharacterSkinPose.TryFaceRuns(bind.triangles, _srcVerts.Length, out _faceStarts, out _faceCounts))
                throw new InvalidOperationException(
                    $"CharacterSkinDef '{def.Id}' carries rig 9's face, but its bind mesh is not one fan of " +
                    "consecutive vertices per face, so its faces cannot be culled whole. Re-bake it.");
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
            int count = Mathf.Min(def.Materials.Length, CharacterSkinDef.MaxMaterials(def.ToneRule));
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

            if (!def.HasInk) return;

            // THE RIG'S EDGE DROP, as a ramp: across a depth break the farther pixel shows its ramp ONE
            // step down, and step 0 stays (paint: `if (drop && !fixed && step > 0) step--`). A fixed
            // material is a one-colour ramp shown at step 0, so it needs no case of its own. Bound as
            // _DarkRampTex while the ink is live (SyncInk), in place of the hull's two-step RINDEX ramp.
            _stepRampTex = MakeRampTexture("HHCharStepRampTex", maxLen, count);
            for (int m = 0; m < count; m++)
                for (int i = 0; i < maxLen; i++)
                {
                    int k = Mathf.Min(i, ramps[m].Length - 1);
                    _stepRampTex.SetPixel(i, m, ramps[m][Mathf.Max(0, k - 1)]);
                }
            _stepRampTex.Apply(false, true);
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
        /// <para><b>⚠ Rig 7's per-material gain and bias are still NOT written.</b> A rig 7 def
        /// carries a <c>Gain</c>/<c>Bias</c> per material; the facet shader's default variant has one
        /// global pair. That single mismatch is the largest term in rig 7's measured 43–57% fidelity
        /// gap (53.61% of it) and it is the shader look pass's to close, not this one's. A
        /// <see cref="ToneRule.V9"/> def gets its own, per material, through the <c>HH_FIGURE</c>
        /// variant (<see cref="ApplyToneRule"/>).</para>
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
            _facetMaterial.SetVector(IsoFacetShaderIds.LightN, IsoFacetMath.ShaderLightVector(LightOf(def)));
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

            ApplyToneRule(def);
        }

        /// <summary>What <c>_LN</c> carries before <see cref="IsoFacetMath.ShaderLightVector"/>: rig 7's
        /// <c>LightN</c>, or v9's folded key (<see cref="IsoFacetFigureTone.FoldLight"/>). Both are in
        /// the same screen basis and neither is turned by the elevation: the tilt is the transform's
        /// <c>HullRotation(0, e)</c>.</summary>
        private static Vector3 LightOf(CharacterSkinDef def) =>
            def.ToneRule == ToneRule.V9 ? IsoFacetFigureTone.FoldLight(def.KeyScreen, def.Form) : def.LightN;

        /// <summary>
        /// <b>The tone rule, on this figure's OWN material.</b> V9 turns the <c>HH_FIGURE</c> variant on
        /// and writes its two tables and rig 9's face constants (the cull by role, the floor, the rest
        /// face; the per-draw face and head follow every pose); rig 7 turns it off, which on a material
        /// built fresh by every
        /// <see cref="Configure"/> is the state it already has, said out loud as the hull's
        /// <c>ApplyCutawayKeyword</c> says its own. The keyword is <c>_local</c>, so it is set on the
        /// instance and never through <c>Shader.EnableKeyword</c>, which does not reach it.
        ///
        /// <para>Both tables are written at their full <see cref="CharacterSkinDef.V9RampSlots"/> every
        /// time, because Unity fixes an array's size at its first set. An unused slot is a one-colour
        /// ramp with a zero gain and bias. The rig 7 uniforms written above stay on a v9 material and
        /// its variant reads none of them but <c>_LN</c>, which <see cref="LightOf"/> already folded. So
        /// a v9 figure is no longer a hull's twin, on purpose:
        /// <c>TheFigureMaterialMatchesAHullConfiguredFromTheSameSetup</c> holds rig 7.</para>
        /// </summary>
        private void ApplyToneRule(CharacterSkinDef def)
        {
            if (def.ToneRule != ToneRule.V9)
            {
                _facetMaterial.DisableKeyword(IsoFacetFigureShaderIds.FigureKeyword);
                return;
            }
            _facetMaterial.EnableKeyword(IsoFacetFigureShaderIds.FigureKeyword);

            var meta = new Vector4[CharacterSkinDef.V9RampSlots];
            var tone = new Vector4[CharacterSkinDef.V9RampSlots];
            int count = Mathf.Min(def.Materials.Length, CharacterSkinDef.V9RampSlots);
            for (int m = 0; m < meta.Length; m++)
            {
                if (m >= count)
                {
                    meta[m] = new Vector4(1f, 0f, 0f, 0f);
                    tone[m] = Vector4.zero;
                    continue;
                }
                CharacterSkinDef.Material mat = def.Materials[m];
                Color32[] c = mat.Colors;
                float gain = def.Gain * mat.Gain;
                float bias = IsoFacetFigureTone.FoldBias(gain, mat.BiasOr(def.Bias), def.Form, def.FormMid);
                meta[m] = new Vector4(c != null && c.Length > 0 ? c.Length : 1, mat.Offset, mat.ToneLo, mat.ToneHi);
                tone[m] = new Vector4(gain, bias, 0f, 0f);
            }
            _facetMaterial.SetVectorArray(IsoFacetFigureShaderIds.RampMetaFigure, meta);
            _facetMaterial.SetVectorArray(IsoFacetFigureShaderIds.RampToneFigure, tone);

            // Rig 9's face: the cull by role and the floor every face culls at, and whether this def
            // carries the face at all (y). A v9 def baked before the face writes y = 0, and the shader
            // then gates, culls and snaps nothing: it draws exactly as it did.
            _facetMaterial.SetVector(IsoFacetFigureShaderIds.FigureFaceMinT, def.FaceMinToward);
            _facetMaterial.SetVector(IsoFacetFigureShaderIds.FigureFaceParams,
                                     new Vector4(def.FaceCullFloor, def.HasFace ? 1f : 0f, 0f, 0f));
            _faceUniform = Vector4.zero;
            if (def.HasFace && def.RestFace != null && def.RestFace.Length == CharacterSkinDef.FaceSlots)
                _faceUniform = new Vector4(def.RestFace[0], def.RestFace[1], def.RestFace[2], 0f);
            _facetMaterial.SetVector(IsoFacetFigureShaderIds.FigureFace, _faceUniform);
            HeadSnapPoint = Vector4.zero;
            _facetMaterial.SetVector(IsoFacetFigureShaderIds.FigureHead, HeadSnapPoint);
            _facetMaterial.SetFloat(IsoFacetFigureShaderIds.FigureInkOn, 0f);
            _inkApplied = false;
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
        public bool SetPose(string stateKey, int frame) => SetPose(stateKey, frame, default);

        /// <summary>
        /// <see cref="SetPose(string, int)"/> with the figure's <see cref="Life"/>: the look and the
        /// blink, rig 9 README §4 and §5. Steps exactly as the plain call does, and a default
        /// <paramref name="life"/> draws exactly what it draws.
        ///
        /// <list type="bullet">
        /// <item><b>The look is sampled on the clip's BEAT</b> — a new clip or a new frame asked for — and
        /// held between beats, so a figure re-skins at its clip's rate however its target moves (rule 7).
        /// It is the rig's <c>lookAt</c> on the frame shown, unturned and unrocked
        /// (<see cref="CharacterSkinPose.LookAtFrame"/>), and the turn goes onto the neck and head
        /// locals after the clip and before the rock (<see cref="CharacterSkinPose.ApplyTurn"/>). The
        /// mesh is re-skinned only when the frame or the rounded turn changed.</item>
        /// <item><b>The face is composed every call</b> — the frame's own groups, then the gaze, then
        /// the blink (<see cref="CharacterFigureFace.Compose"/>) — and reaches the shader as one
        /// uniform, so a blink never re-skins and never costs a second draw call.</item>
        /// <item><b>The head snap</b>'s point, the head's mid point after the turn, is taken on every
        /// re-skin.</item>
        /// </list>
        /// </summary>
        public bool SetPose(string stateKey, int frame, in Life life)
        {
            if (!IsConfigured || string.IsNullOrEmpty(stateKey)) return false;
            if (!_def.TryGetClip(stateKey, out CharacterSkinDef.SkinClip clip)) return false;
            if (clip.FrameCount <= 0) return false;

            frame = Mathf.Clamp(frame, 0, clip.FrameCount - 1);
            int drawn = _honestFrames.TryGetValue(stateKey, out int[] map) && frame < map.Length
                        ? map[frame] : frame;

            bool sameClip = DrawnStateKey == stateKey;
            bool composed = false;
            if (!sameClip || RequestedFrame != frame) composed = SampleLook(clip, drawn, life);

            if (!sameClip || DrawnFrame != drawn || _lookYaw != _drawnYaw || _lookPitch != _drawnPitch)
            {
                if (!composed) CharacterSkinPose.ComposeWorld(clip, drawn, _def.Bones, _world);
                if (_lookYaw != 0d || _lookPitch != 0d)
                {
                    var limits = CharacterFigureLook.Limits.Of(_def);
                    CharacterSkinPose.ApplyTurn(
                        clip, drawn, _def.Bones, _world,
                        _def.LookNeckBone, CharacterFigureLook.TurnOf(_lookYaw, _lookPitch, _def.LookSplitNeck, limits),
                        _def.LookHeadBone, CharacterFigureLook.TurnOf(_lookYaw, _lookPitch, _def.LookSplitHead, limits));
                }
                CharacterSkinPose.FinishSkin(_world, _bindposes, _skin);
                CharacterSkinPose.Skin(_skin, _weights, _srcVerts, _srcNorms, _outVerts, _outNorms);
                if (_faceStarts != null)
                    CharacterSkinPose.NewellNormals(_faceStarts, _faceCounts, _outVerts, _outNorms);
                _posedMesh.vertices = _outVerts;
                _posedMesh.normals = _outNorms;
                _posedMesh.RecalculateBounds();

                if (_def.HasHeadSnap)
                {
                    Vector3 h = CharacterSkinPose.HeadPoint(_def, _world);
                    HeadSnapPoint = new Vector4(h.x, h.y, h.z, 1f);
                    _facetMaterial.SetVector(IsoFacetFigureShaderIds.FigureHead, HeadSnapPoint);
                }

                DrawnStateKey = stateKey;
                DrawnFrame = drawn;
                _drawnYaw = _lookYaw;
                _drawnPitch = _lookPitch;
            }
            RequestedFrame = frame;

            if (_def.HasFace)
            {
                CharacterFigureFace.Compose(_def, clip, drawn, _gaze, life.BlinkEyes,
                                            out int eyes, out int brows, out int mouth);
                if (eyes != (int)_faceUniform.x || brows != (int)_faceUniform.y || mouth != (int)_faceUniform.z)
                {
                    _faceUniform = new Vector4(eyes, brows, mouth, 0f);
                    _facetMaterial.SetVector(IsoFacetFigureShaderIds.FigureFace, _faceUniform);
                }
            }
            return true;
        }

        /// <summary>
        /// One beat's look: the rig's <c>lookAt</c> on the frame about to be shown, or no turn and the
        /// clip's own eyes when there is nothing to look at, the def carries no look, or both switches
        /// are off. With the head's turn off the head takes no share, so the eyes alone lead (the rig's
        /// <c>lookAt</c> with share 0). True when it left <see cref="_world"/> holding the frame's
        /// unturned composition, which the re-skin then reuses.
        /// </summary>
        private bool SampleLook(in CharacterSkinDef.SkinClip clip, int drawn, in Life life)
        {
            _lookYaw = _lookPitch = 0d;
            _gaze = CharacterFigureLook.GazeOpen;
            if (!_def.HasLook || !life.HasTarget || !(life.HeadLook || life.EyeLook)) return false;

            CharacterSkinPose.ComposeWorld(clip, drawn, _def.Bones, _world);
            CharacterFigureLook.Result r = CharacterSkinPose.LookAtFrame(
                _def, _world, life.Target, life.HeadLook ? _def.LookHeadShare : 0d);
            if (life.HeadLook)
            {
                _lookYaw = r.Yaw;
                _lookPitch = r.Pitch;
            }
            if (life.EyeLook) _gaze = r.Gaze;
            return true;
        }

        /// <summary>
        /// <b>A world point on this figure's own ground</b>, in its frame (rig metres; z = 0): the point
        /// of the figure's ground plane that projects to <paramref name="world"/>'s screen position,
        /// solved through the facet child's placement (heading, facing, rock and heave included). A
        /// look target is a point on the screen plane with no depth of its own, so this TAKES IT TO
        /// STAND ON THE FIGURE'S GROUND: a player on a quay beside a moored skipper is read at the deck's
        /// height. False before <see cref="Configure"/>, or for a ground plane seen edge-on.
        /// </summary>
        public bool TryFigureGround(Vector3 world, out Vector3 ground)
        {
            ground = default;
            if (_meshChild == null) return false;
            Matrix4x4 m = _meshChild.localToWorldMatrix;
            double a = m.m00, b = m.m01, c = m.m10, d = m.m11;
            double det = a * d - b * c;
            if (!(Math.Abs(det) > 1e-9)) return false;
            double dx = world.x - m.m03, dy = world.y - m.m13;
            ground = new Vector3((float)((d * dx - b * dy) / det), (float)((a * dy - c * dx) / det), 0f);
            return true;
        }

        private void LateUpdate()
        {
            // OnDisable releases the id without touching a hierarchy Unity is still deactivating.
            // If the figure survives, finish the hand-over once callbacks have unwound.
            if (!IsAshore) LeaveAshore();
            if (IsAshore) WriteAshoreProperties();
            else WriteHullProperties();
            SyncInk();
        }

        // ---------------------------------------------------------------- the rig's ink

        /// <summary>
        /// <b>The rig's own ink, read live</b> (<see cref="GameConfig.MeshFigureKeyline"/>). On, for a def
        /// that <see cref="CharacterSkinDef.HasInk"/>: the facet pass draws this figure's dark target
        /// from the one-step ramp and flags its pixels (dark alpha 0), and the figure joins
        /// <see cref="IsoFacetFigureInk"/>, which tells the resolve to ink flagged pixels by the rig's
        /// rules. Off, or a def without the ink: the RINDEX ramp and alpha 1, the hull's rules, exactly
        /// the picture before PR 2a. Two property writes when the switch flips; nothing otherwise.
        /// </summary>
        private void SyncInk()
        {
            bool on = _def != null && _def.HasInk && _facetMaterial != null && _stepRampTex != null &&
                      GameServices.MeshFigureKeyline;
            if (on != _inkApplied && _facetMaterial != null)
            {
                _facetMaterial.SetTexture(IsoFacetShaderIds.DarkRampTex, on ? _stepRampTex : _darkRampTex);
                _facetMaterial.SetFloat(IsoFacetFigureShaderIds.FigureInkOn, on ? 1f : 0f);
                _inkApplied = on;
            }

            bool join = on && isActiveAndEnabled && Visible;
            if (join == _inkJoined) return;
            _inkJoined = join;
            if (join) IsoFacetFigureInk.Join(this, _def.Edge, _def.KeylineMix);
            else IsoFacetFigureInk.Leave(this);
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
            if (!IsConfigured || !gameObject.activeInHierarchy) return false;
            if (IsAshore)
            {
                _sortSource = sortSource;
                WriteAshoreProperties();
                return true;
            }
            if (GetComponentInParent<IsoFacetHullRenderer>() != null) return false;

            LeaveAshore(); // finish any deferred cleanup before building another frame

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
        /// and destroy the frame and the overlay. An inactive hierarchy keeps its frame until the
        /// next pose or re-entry, so Unity can finish deactivation without a parenting change.
        /// A no-op when not ashore, so it never clears an aboard figure's hull properties.
        /// </summary>
        public void LeaveAshore()
        {
            if (!IsAshore && _ashoreFrame == null && _overlayChild == null) return;

            ReleaseAshoreId();
            if (!gameObject.activeInHierarchy) return;

            if (_meshChild != null && _meshChild.parent != transform)
            {
                _meshChild.SetParent(transform, false);
                _meshChild.localPosition = Vector3.zero;
                _meshChild.localRotation = Quaternion.identity;
                _meshChild.localScale = Vector3.one;
            }
            DestroyAshoreObjects();
        }

        private void ReleaseAshoreId()
        {
            if (!IsAshore && _ashoreFrame == null && _overlayChild == null) return;
            if (_figureId != 0) IsoFacetHullRegistry.UnregisterFigure(_figureId);
            _figureId = 0;
            _sortSource = null;
            if (_meshRenderer != null) _meshRenderer.SetPropertyBlock(null);
            _props?.Clear();
            if (_overlayRenderer != null) _overlayRenderer.enabled = false;
        }

        private void DestroyAshoreObjects()
        {
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

        // Unity forbids SetParent during hierarchy deactivation. Return the id now, but keep the
        // frame (and its posed mesh) until a safe hand-over or teardown. The caller re-enters.
        private void OnDisable()
        {
            ReleaseAshoreId();
            SyncInk();   // disabled: out of the ink registry until it is enabled and shown again
        }

        private void OnDestroy() => Teardown(keepDef: false);

        private void Teardown(bool keepDef)
        {
            ReleaseAshoreId();
            if (_inkJoined) IsoFacetFigureInk.Leave(this);
            _inkJoined = false;
            _inkApplied = false;
            if (_meshChild != null) DestroySafely(_meshChild.gameObject);
            DestroyAshoreObjects();
            if (_posedMesh != null) DestroySafely(_posedMesh);
            if (_facetMaterial != null) DestroySafely(_facetMaterial);
            if (_rampTex != null) DestroySafely(_rampTex);
            if (_darkRampTex != null) DestroySafely(_darkRampTex);
            if (_stepRampTex != null) DestroySafely(_stepRampTex);
            _meshChild = null;
            _meshRenderer = null;
            _meshFilter = null;
            _posedMesh = null;
            _facetMaterial = null;
            _rampTex = null;
            _darkRampTex = null;
            _stepRampTex = null;
            _faceStarts = _faceCounts = null;
            _faceUniform = Vector4.zero;
            HeadSnapPoint = Vector4.zero;
            if (!keepDef) { _def = null; _hull = null; _honestFrames.Clear(); _fenceReport.Clear(); }
        }

        private static void DestroySafely(UnityEngine.Object o)
        {
            if (Application.isPlaying) Destroy(o);
            else DestroyImmediate(o);
        }
    }

    /// <summary>
    /// <b>The figure's own facet-shader names</b>: the <c>HH_FIGURE</c> keyword, the two v9 tone
    /// tables, and rig 9's face, head snap and ink uniforms (character PR 2a). They live beside the
    /// figure renderer rather than in <see cref="IsoFacetShaderIds"/>, which every hull shares, so a v9
    /// character adds names to the shader and moves nothing a hull reads.
    /// </summary>
    public static class IsoFacetFigureShaderIds
    {
        /// <summary>The v9 variant of <c>HiddenHarbours/IsoFacet</c>. A <c>multi_compile_local</c>
        /// keyword, set on the figure's own material instance.</summary>
        public const string FigureKeyword = "HH_FIGURE";

        /// <summary><c>float4[32]</c>, per material <c>(len, off, lo, hi)</c>.</summary>
        public static readonly int RampMetaFigure = Shader.PropertyToID("_RampMetaFigure");

        /// <summary><c>float4[32]</c>, per material <c>(gain, bias', 0, 0)</c>: the effective gain and
        /// the folded bias (<see cref="IsoFacetFigureTone.FoldBias"/>).</summary>
        public static readonly int RampToneFigure = Shader.PropertyToID("_RampToneFigure");

        /// <summary><c>float4</c>, per draw: the face group drawn in each slot, <c>(eyes, brows, mouth, 0)</c>,
        /// 1-based into <see cref="CharacterSkinDef.FaceGroups"/>. A bind-mesh face whose group (UV1.x)
        /// is none of the three collapses.</summary>
        public static readonly int FigureFace = Shader.PropertyToID("_HHFigureFace");

        /// <summary><c>float4</c>, per draw: the head's mid point in the figure's frame (xyz) and 1 in w
        /// while the head snaps. Every face flagged as a head face (UV1.z) moves, in screen space, by the
        /// offset that puts this point on a pixel centre.</summary>
        public static readonly int FigureHead = Shader.PropertyToID("_HHFigureHead");

        /// <summary><c>float4</c>, per material: the face cull's thresholds by role (x near, y far,
        /// z side, w mouth — <see cref="CharacterSkinDef.FaceMinToward"/>).</summary>
        public static readonly int FigureFaceMinT = Shader.PropertyToID("_HHFigureFaceMinT");

        /// <summary><c>float4</c>, per material: x the floor every face culls at
        /// (<see cref="CharacterSkinDef.FaceCullFloor"/>), y 1 when the def carries the face.</summary>
        public static readonly int FigureFaceParams = Shader.PropertyToID("_HHFigureFaceParams");

        /// <summary><c>float</c>, per material: 1 while the rig's ink is live for this figure, which
        /// flags its pixels in the dark target's alpha (0 = the figure's rules).</summary>
        public static readonly int FigureInkOn = Shader.PropertyToID("_HHFigureInkOn");

        /// <summary><c>float4</c> on the RESOLVE material: <c>(edge, keylineMix, live, 0)</c> —
        /// <see cref="IsoFacetFigureInk"/>.</summary>
        public static readonly int FigureInk = Shader.PropertyToID("_HHFigureInk");
    }

    /// <summary>
    /// <b>The rig's own ink, for the one resolve pass</b> (character PR 2a). The resolve is one
    /// fullscreen pass per camera, so the figure's depth edge and keyline mix reach it as ONE uniform,
    /// <c>_HHFigureInk = (edge, mix, live, 0)</c>, written by the feature's render func from the value
    /// captured when the pass was recorded (<see cref="Value"/>, <see cref="Apply"/>).
    ///
    /// <para><b>Who is in.</b> A figure joins while its ink is live, it is enabled and it is shown, and
    /// leaves the moment any of those stops (<c>IsoCharacterFigureRenderer.SyncInk</c>). Edge and mix
    /// are the last live member's: every rig 9 def carries the rig's one <c>SHADING</c>, and a guard
    /// holds each def's to it. A member destroyed without leaving (EditMode runs no <c>OnDestroy</c>
    /// for a component that is not <c>ExecuteAlways</c>) is pruned when the value is read.</para>
    ///
    /// <para><b>Why live = 0 is the whole fleet unchanged.</b> With no member the resolve's figure
    /// branch never runs. With one, a HULL pixel still reads alpha 1 in the dark target and takes the
    /// 0.30 m rule it always took, and an empty pixel takes the figure's keyline only beside a FLAGGED
    /// pixel — so no hull pixel reads anything new either way.</para>
    /// </summary>
    public static class IsoFacetFigureInk
    {
        private struct Member
        {
            public IsoCharacterFigureRenderer Figure;
            public float Edge, Mix;
        }

        private static readonly List<Member> s_members = new List<Member>();

        /// <summary>How many figures are in (after pruning).</summary>
        public static int Count
        {
            get
            {
                Prune();
                return s_members.Count;
            }
        }

        /// <summary>This frame's <c>(edge, mix, live, 0)</c>: the last live member's, or zero with none.
        /// Allocation-free.</summary>
        public static Vector4 Value
        {
            get
            {
                Prune();
                if (s_members.Count == 0) return Vector4.zero;
                Member m = s_members[s_members.Count - 1];
                return new Vector4(m.Edge, m.Mix, 1f, 0f);
            }
        }

        /// <summary>Put a figure in (again: its numbers are refreshed and it becomes the last).</summary>
        public static void Join(IsoCharacterFigureRenderer figure, float edge, float mix)
        {
            if (figure == null) return;
            Remove(figure);
            s_members.Add(new Member { Figure = figure, Edge = edge, Mix = mix });
        }

        /// <summary>Take a figure out. A no-op for one that is not in.</summary>
        public static void Leave(IsoCharacterFigureRenderer figure) => Remove(figure);

        /// <summary>Write <paramref name="value"/> onto the resolve material as <c>_HHFigureInk</c>.</summary>
        public static void Apply(Material resolveMaterial, Vector4 value) =>
            resolveMaterial.SetVector(IsoFacetFigureShaderIds.FigureInk, value);

        /// <summary>Empty the registry — for tests.</summary>
        public static void Reset() => s_members.Clear();

        private static void Remove(IsoCharacterFigureRenderer figure)
        {
            for (int i = s_members.Count - 1; i >= 0; i--)
                if (ReferenceEquals(s_members[i].Figure, figure)) s_members.RemoveAt(i);
        }

        private static void Prune()
        {
            for (int i = s_members.Count - 1; i >= 0; i--)
                if (s_members[i].Figure == null) s_members.RemoveAt(i);
        }
    }
}
