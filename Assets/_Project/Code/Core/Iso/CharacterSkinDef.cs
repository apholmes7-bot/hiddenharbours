using System;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>A rig-extracted character as ONE SKINNED MESH — a bind mesh, a skeleton, and bone clips
    /// (ADR 0044 §3.6, owner ruling 2026-09-09 option (d)).</b> Everything
    /// <see cref="CharacterMeshDef"/> carries about how a character is SHADED, carried verbatim,
    /// plus the three things a flipbook does not have: a bind pose, a bone hierarchy, and one clip
    /// of bone transforms per <c>ANIMS</c> row.
    ///
    /// <para><b>Why this exists next to <see cref="CharacterMeshDef"/> rather than replacing it.</b>
    /// The flipbook is the FALLBACK until a presenter proves parity state for state (ADR 0041's
    /// rule, restated in ADR 0044 §2). Two defs, two paths, one vocabulary: both spell a clip's
    /// identity with the same <c>StateKey</c>, both carry the same <c>Materials</c> table in the
    /// same rig-MATS order, and both record the same measured
    /// <see cref="AzimuthCounterClockwise"/>. Nothing here reads the other; a presenter picks.</para>
    ///
    /// <para><b>The measurement that made one mesh possible.</b> <c>characterIsoRig6.js</c> re-lathes
    /// its geometry per pose, which is why ADR 0044 shipped a flipbook: there is no static face
    /// list to widen into. What the rigidity pass found is that the RESULT is nonetheless a rigid
    /// skin — welded per vertex, never per face, 2,992 corners fold to 920 reference positions and
    /// every one of them tracks a bone to well under a 0.5 mm floor on a ~1.75 m figure. The art
    /// director shipped that as <c>characterIsoRig7.js</c> rev 7.1, and this repo re-ran it rather
    /// than citing it: 10 builds × 56 golden rows × 462 frames, worst vertex error 4.02e-13 m
    /// against the rig's own 1e-4 m tolerance, 0 failing rows.</para>
    ///
    /// <para><b>The budget, which is the whole argument.</b> For the player preset, measured on the
    /// rigs as they sit in this repo: 711 faces → 2,992 vertices, 1,570 triangles, 45 bones,
    /// 35 clips of 308 frames. <b>232 KB of bind mesh + 379 KB of clips = 611 KB</b>, against the
    /// flipbook's <b>44.1 MB</b> for the same player recipe — <b>74× smaller</b>, and ~6 MB for a
    /// ten-preset cast instead of ~318 MB.</para>
    ///
    /// <para>⚠️ <b><see cref="MaxBoneInfluences"/> is TWO, and that is a measurement, not a
    /// default.</b> Most of the skin is rigid — 2,920 of the fisher's 2,992 corners ride one bone
    /// — but 72 corners on the BLENDED rings (the apron and skirt hems lerping to the torso, the
    /// short-sleeve hem lerping to the shoulder tip) carry two weights, and collapsing each onto
    /// its heavier bone costs <b>4.52e-2 m on the fisher: 452× tolerance, every one of the 56
    /// golden rows failing</b> (2.53e-1 m on nan). A renderer that quietly resolves
    /// <c>SkinQuality.Auto</c> down to one bone reproduces that catastrophe exactly, silently, and
    /// only in the hems. A presenter MUST set the blend-weight quality explicitly to at least two
    /// bones; <see cref="IsUsable"/> refuses a def that claims fewer.</para>
    ///
    /// <para>⚠️ <b>The bind mesh is the UNION of two topologies.</b> The rig draws 711 faces for the
    /// 308 on-deck poses and 705 for <c>swim</c>, <c>tread</c>, <c>sleep</c> and <c>drive</c>: the
    /// six-face <c>overD</c> inseam panel is not built for those. One mesh cannot have two face
    /// counts, so the bind mesh carries all 711 and rig 7 parks both inseam bones at the pelvis
    /// centre — inside the pelvis lathe — on exactly those four clips, where the panel collapses
    /// behind the z-buffer instead of being culled. <see cref="SkinClip.ParkedParts"/> records
    /// which parts a clip parks so QA can see it; it is information, not a runtime switch, and a
    /// presenter must not grow one.</para>
    ///
    /// <para>⚠️ <b>The face is a raster STAMP and this mesh does not have one.</b> Eyes, brows,
    /// lashes, lips and iris are painted by the head rig after the polygons, on materials marked
    /// <see cref="Material.FixedIndex"/> ≥ 0 that no face of this mesh references. Small in pixels
    /// (0.00–2.82% measured) and large in the reading. A mesh character has no face until a
    /// presenter re-adds one — the same debt <see cref="CharacterMeshDef"/> carries.</para>
    ///
    /// <para>⚠️ <b>Vertices are in RIG SPACE, verbatim: +x right (curb), +y forward (nose), +z up,
    /// origin at the cell pivot on the ground.</b> No axis swap is applied anywhere in this repo's
    /// mesh path (<c>RigMeshExtractor.Vector3d.ToVector3</c> is the identity and the facet renderer
    /// parents its mesh child at local identity), so the OBJECT TRANSFORM carries heading and the
    /// mesh keeps the rig's own frame. The rig 7 header documents a "Unity map: pos (x, z, y),
    /// quaternion (−x, −z, −y, w)" for a y-up importer — <b>this repo does not apply it</b>, and
    /// applying it here would put the skinned figure in a different space from every pose mesh
    /// <see cref="CharacterMeshDef"/> already bakes and silently break the swap this def exists
    /// to enable.</para>
    ///
    /// <para><b>Why it lives in Core.</b> Art draws it, the player/NPC controllers drive it, and
    /// neither may reference the other (CLAUDE.md rule 4) — the same reasoning as
    /// <see cref="CharacterMeshDef"/> and <c>HullMeshDef</c>. It contains no URP type.</para>
    /// </summary>
    public class CharacterSkinDef : ScriptableObject
    {
        /// <summary>The facet resolve pass's <c>_RampMeta</c> is a <c>float4[16]</c>. Same cap, same
        /// reason, same number as <see cref="CharacterMeshDef.RampSlots"/> — deliberately not an
        /// alias, so a widening of one path is a deliberate decision about the other.</summary>
        public const int RampSlots = 16;

        /// <summary>The rig's measured maximum influences per vertex, across all ten builds. See the
        /// class remarks: this is the number a renderer's blend-weight quality must MEET, and
        /// dropping to one costs 452× the rig's own tolerance in the hems.</summary>
        public const int MaxBoneInfluences = 2;

        /// <summary>A bone index that owns nothing / has no parent.</summary>
        public const int NoBone = -1;

        // -----------------------------------------------------------------------------------
        // Shading — the same shape CharacterMeshDef carries, for the same measured reasons.
        // -----------------------------------------------------------------------------------

        /// <summary>
        /// One rig material as the facet path uses it: a palette ramp, its constant index offset,
        /// and — unlike a hull — its OWN shade gain and bias.
        ///
        /// <para>Per-material gain/bias are not decoration: 27 of the fisher's 36 materials declare
        /// their own off a <c>SPAN</c> table, and flattening them to one global gain measured
        /// 35.5–53.6% differing inked pixels against a hull-path reference band of 2.5–4.8%.</para>
        /// </summary>
        [Serializable]
        public struct Material
        {
            [Tooltip("The rig's MATS key, for the bake log and for reading a def by eye.")]
            public string Name;

            [Tooltip("The palette ramp, dark to light, exactly as the rig's MATS entry holds it.")]
            public Color32[] Colors;

            [Tooltip("The material's constant ramp-index offset ('off'; dark aliases are negative).")]
            public int Offset;

            [Tooltip("The rig's per-material shade gain (MATS[k].gain). 1 where the rig declares " +
                     "none. Multiplies the GLOBAL Gain: fidx = sh*Gain*this + Bias + faceBias.")]
            public float Gain;

            [Tooltip("The rig's per-material shade bias (MATS[k].bias). NaN where the rig declares " +
                     "none, in which case the def's global Bias applies.")]
            public float Bias;

            [Tooltip("True when this material ordered-dithers between ramp steps (MATS[k].dith). " +
                     "FALSE on every material the shipped pass-6 cast uses.")]
            public bool OrderedDither;

            [Tooltip("≥ 0 when the rig pins this material to a constant ramp index — the head rig's " +
                     "STAMP materials. No polygon references them; recorded so a presenter that " +
                     "re-adds the face has the rig's own numbers. −1 = shaded normally.")]
            public int FixedIndex;

            /// <summary>The material's effective shade bias — its own, or the def's global one.</summary>
            public float BiasOr(float globalBias) => float.IsNaN(Bias) ? globalBias : Bias;

            /// <summary>True when no polygon can reference this material (a face-stamp material).</summary>
            public bool IsStampOnly => FixedIndex >= 0;
        }

        /// <summary>
        /// How the rig resolves the fractional part of a shade index into a ramp step. The
        /// character rig hard-steps at 0.55 and ordered-dithers nothing; forcing the facet pass's
        /// Bayer dither onto it measured 19.7–30.2% differing inked pixels.
        /// </summary>
        public enum ShadeStep
        {
            /// <summary>Hard threshold at <see cref="HardStepThreshold"/> — what the character rig does.</summary>
            HardThreshold = 0,
            /// <summary>4×4 ordered dither against <see cref="Bayer16"/> — what the hull rigs do.</summary>
            OrderedDither = 1,
        }

        // -----------------------------------------------------------------------------------
        // The skeleton
        // -----------------------------------------------------------------------------------

        /// <summary>
        /// One bone of the rig's skeleton, in the rig's own declaration order. That order IS the
        /// index space: <see cref="Parent"/> is an index into this array, the mesh's
        /// <c>boneWeights</c> index it, and <see cref="Bindposes"/> is parallel to it.
        ///
        /// <para><b>Twelve of the 45 own no vertex</b> — <c>root</c>, both wrists, six <c>tool_*</c>
        /// and three <c>carry_*</c>. They are MOUNTS: places for a presenter to read a held prop's
        /// transform off, not deformers. They are kept in the table (and in every clip) precisely
        /// because reading them is the point; <see cref="OwnsVertex"/> says which is which so a
        /// guard can tell a deformer that stopped deforming from a mount that never did.</para>
        ///
        /// <para>Every <c>limb(A,B)</c> in the rig is a bone at A plus a TIP child at B, because the
        /// drawn lengths are not constant and the tip lerps between frames. A tip is a pure
        /// translation along its parent's local axis, so its world rotation equals its parent's —
        /// which is why a tube quad spanning a bone and its tip stays rigid under linear blend
        /// skinning and its flat normal survives.</para>
        /// </summary>
        [Serializable]
        public struct Bone
        {
            [Tooltip("The rig's own bone id ('pelvis', 'upper_L', 'tool_rod_tip', …).")]
            public string Id;

            [Tooltip("Index into Bones of this bone's parent, or −1 at the root.")]
            public int Parent;

            [Tooltip("Rest position, in METRES, LOCAL to the parent bone. The bind pose is the rig's " +
                     "own pose('idle', 0, build).")]
            public Vector3 RestPosition;

            [Tooltip("Rest rotation, LOCAL to the parent. A unit quaternion, because the rig says so: " +
                     "the tube frames pass through vertical on every walk cycle, where any Euler " +
                     "order has a pole.")]
            public Quaternion RestRotation;

            [Tooltip("True when at least one vertex of the bind mesh is weighted to this bone. " +
                     "33 of the fisher's 45; the other 12 are MOUNTS to be read, not deformers.")]
            public bool OwnsVertex;
        }

        /// <summary>One bone's transform on one clip frame, LOCAL to its parent — the same frame
        /// the rest pose is expressed in, so a player writes it straight onto the transform.</summary>
        [Serializable]
        public struct BoneKey
        {
            public Vector3 Position;
            public Quaternion Rotation;
        }

        /// <summary>
        /// One <c>ANIMS</c> row as bone animation: <see cref="FrameCount"/> frames × the def's bone
        /// count of local transforms.
        ///
        /// <para><b><see cref="Keys"/> is FLAT and frame-major</b> — <c>Keys[frame * boneCount +
        /// bone]</c>, which <see cref="KeyOf"/> spells so nobody has to. Flat because Unity does not
        /// serialize a jagged array, and frame-major because a player walks one frame's bones
        /// together.</para>
        ///
        /// <para>⚠️ <b>The rock and the head look are IN the pose, not in a transform.</b> Rig 6's
        /// <c>pose()</c> reads its counter-lean and the head's look direction as arguments and bakes
        /// them into the skeleton it returns, so they arrive here as bone keys like everything else.
        /// ADR 0024's "rock remains a transform" is false of this rig. A skinned figure therefore
        /// gets both for free at runtime — which is an argument FOR this path, and something a
        /// presenter must not fight by re-applying them on the object transform.</para>
        ///
        /// <para>⚠️ <b>These are DISCRETE SAMPLES, not keyframes on a curve — STEP them, never
        /// blend them.</b> The rig's <c>solveAt(anim, u)</c> is a closed-form pose function
        /// evaluated at <c>u = k/den</c>; consecutive samples are under no obligation to be near
        /// each other in quaternion space, and measured over all 35 clips <b>adjacent frames step up
        /// to 178.3° on a single bone</b> (20 of 315 adjacent bone-steps in <c>walk</c> alone exceed
        /// 90°). A slerp across a 178° step passes through an arbitrary orientation on the wrong side
        /// of the sphere — a pose the rig never authored. So a presenter samples and holds at
        /// <see cref="SkinClip.FramesPerSecond"/>: no interpolation between frames, no cross-fade
        /// between clips, no <c>Animator</c> humanoid retarget that assumes a continuous curve.
        /// Smoothing has to come from the rig authoring more frames. (The flipbook has always
        /// stepped; this is the same character, not a downgrade.)</para>
        /// </summary>
        [Serializable]
        public struct SkinClip
        {
            [Tooltip("The rig ANIMS key this clip carries ('idle', 'walk', 'haul', …).")]
            public string Anim;

            [Tooltip("The STATE key, in the sheet baker's own spelling ('walk', 'cast_long', " +
                     "'walk_buckets'). Same vocabulary as CharacterMeshDef.PoseClip.State, so a " +
                     "skin clip, a flipbook clip and a baked sheet all name the same thing.")]
            public string State;

            [Tooltip("Playback rate, from the rig's own ANIMS.ms (fps = 1000/ms).")]
            public float FramesPerSecond;

            [Tooltip("True when the rig marks this clip 'settle' — it spans its frames INCLUSIVELY " +
                     "(u = f/(frames−1)) rather than cyclically. A player that loops a settle clip " +
                     "plays a different pose on the last frame than the bake did.")]
            public bool Settle;

            [Tooltip("The rig's own loop flag for this clip.")]
            public bool Loop;

            [Tooltip("Frames in this clip.")]
            public int FrameCount;

            [Tooltip("FrameCount × BoneCount local bone transforms, frame-major. Use KeyOf().")]
            public BoneKey[] Keys;

            [Tooltip("Named parts the rig PARKS for this clip — their bones sit inside the body so " +
                     "the geometry collapses. 'inseam' on swim/tread/sleep/drive, which is the " +
                     "705-vs-711-face topology difference the union bind mesh absorbs. " +
                     "INFORMATION for QA, not a runtime visibility switch.")]
            public string[] ParkedParts;

            [Tooltip("The rig's mount context for this clip ('free', 'astride', 'cab', …).")]
            public string Mount;

            [Tooltip("The carry stance the rig resolved this clip against, or empty.")]
            public string Carry;

            [Tooltip("The power axis the rig resolved this clip against ('short' / 'long').")]
            public string Power;

            /// <summary>The key a caller looks this clip up by — <see cref="State"/> when the bake
            /// set one, else the bare anim. Deliberately identical to
            /// <see cref="CharacterMeshDef.PoseClip.StateKey"/>.</summary>
            public string StateKey => string.IsNullOrEmpty(State) ? Anim : State;

            /// <summary>One bone's local transform on one frame.</summary>
            public BoneKey KeyOf(int frame, int bone, int boneCount) =>
                Keys[frame * boneCount + bone];

            /// <summary>True when <see cref="Keys"/> is exactly the length the frame and bone counts
            /// demand — the one invariant a flat array cannot express in its type.</summary>
            public bool KeysWellFormed(int boneCount) =>
                Keys != null && boneCount > 0 && FrameCount > 0 &&
                Keys.Length == FrameCount * boneCount;
        }

        // -----------------------------------------------------------------------------------
        // Identity and provenance
        // -----------------------------------------------------------------------------------

        [Tooltip("Stable id (append-only), 'charskin.<preset>'.")]
        public string Id = "";

        [Header("Provenance (the stale-bake guard — TWO rigs, not one)")]
        [Tooltip("The skinned-export rig this was baked from, repo-relative (characterIsoRig7.js).")]
        public string SourceRigPath = "";

        [Tooltip("The export rig's own revision string (CharacterIso7.revision), e.g. '7.1'.")]
        public string SourceRigRevision = "";

        [Tooltip("SHA-256 of the export rig source, newline-normalised to LF. A bake whose hash no " +
                 "longer matches the working tree is STALE.")]
        public string SourceRigSha256 = "";

        [Tooltip("The BODY rig the export re-expresses, repo-relative (characterIsoRig6.js). " +
                 "⚠️ CharacterIso7 is Object.create(CharacterIso6): every vertex here is rig 6's " +
                 "geometry wearing rig 7's bones. A body bump that forgets the export costs nothing " +
                 "at load and everything at draw, so BOTH rigs are pinned — pinning only the export " +
                 "would let the thing it re-expresses move underneath it.")]
        public string BaseRigPath = "";

        [Tooltip("The body rig's revision (CharacterIso6.revision). The rig itself names it: " +
                 "CharacterIso7.base must equal this.")]
        public string BaseRigRevision = "";

        [Tooltip("SHA-256 of the body rig source, LF-normalised.")]
        public string BaseRigSha256 = "";

        [Tooltip("The BUILDS preset this skin is (fisher, ginny, skipper, …).")]
        public string Preset = "";

        [Header("Cell geometry (extracted from the rig)")]
        public int CellW = 64;
        public int CellH = 92;
        [Tooltip("The rig's own pivot, in cell pixels.")]
        public Vector2 PivotPx;
        public int PxPerMetre = 32;
        public float ElevationDeg = 40f;

        [Header("Shading (the rig's own pipeline, verbatim)")]
        [Tooltip("The rig's LN, already normalised by the rig itself.")]
        public Vector3 LightN;
        [Tooltip("The rig's global GAIN. Every material multiplies it by its own Material.Gain.")]
        public float Gain;
        [Tooltip("The rig's global BIAS, used only by materials that declare none of their own.")]
        public float Bias;
        [Tooltip("How the fractional shade index resolves. HardThreshold on the character rig.")]
        public ShadeStep StepMode = ShadeStep.HardThreshold;
        [Tooltip("The rig's hard step threshold (pass 6: 0.55).")]
        public float HardStepThreshold = 0.55f;
        [Tooltip("4×4 ordered-dither thresholds, (v+0.5)/16, row-major [x*4+y].")]
        public float[] Bayer16 = Array.Empty<float>();
        [Tooltip("The rig's keyline colour.")]
        public Color32 Keyline;
        [Tooltip("The materials this bind mesh's faces reference, in the rig's own MATS order — the " +
                 "index a face's UV0.x carries.")]
        public Material[] Materials = Array.Empty<Material>();

        [Header("Pose facts (MEASURED, never declared)")]
        [Tooltip("True when the mapping from heading to rig dir-units must NEGATE. Adjudicated in " +
                 "pixels at bake time against the rig's own East view, on the SILHOUETTE, with a " +
                 "≥4× sabotage margin. Declaring it would ship a mirrored character; this lane has " +
                 "been CCW-mislabelled twice.")]
        public bool AzimuthCounterClockwise;

        [Header("The skin")]
        [Tooltip("The BIND MESH: the rig's pose('idle', 0, build) geometry, one vertex per face " +
                 "corner (flat normals, per-face UV0 facet attributes), carrying boneWeights and " +
                 "bindposes. In RIG SPACE — the object transform carries heading.")]
        public Mesh BindMesh;

        [Tooltip("The skeleton, in the rig's own declaration order. Parent is an index into this " +
                 "array; the mesh's boneWeights index it too.")]
        public Bone[] Bones = Array.Empty<Bone>();

        [Tooltip("The MEASURED maximum influences any vertex of BindMesh carries. Must be ≥ 1 and " +
                 "≤ MaxBoneInfluences; a presenter sets its renderer's blend-weight quality to at " +
                 "LEAST this and never leaves it on Auto.")]
        public int MaxInfluences = MaxBoneInfluences;

        [Tooltip("One clip per rig ANIMS row.")]
        public SkinClip[] Clips = Array.Empty<SkinClip>();

        [Header("Adoption (ADR 0041: sheets retire per state, at parity)")]
        [Tooltip("The state keys this character is DRAWN AS A SKINNED MESH for. Every other state " +
                 "still plays its baked sheet or its flipbook. EMPTY means the skin is baked and " +
                 "inspectable but nothing draws from it yet — which is this PR's shipped state.")]
        public string[] MeshStates = Array.Empty<string>();

        // -----------------------------------------------------------------------------------

        /// <summary>Bones in the skeleton — the stride of every clip's <see cref="SkinClip.Keys"/>.</summary>
        public int BoneCount => Bones?.Length ?? 0;

        /// <summary>Bones that actually deform geometry (33 of the fisher's 45).</summary>
        public int DeformingBoneCount
        {
            get
            {
                int n = 0;
                if (Bones != null)
                    foreach (Bone b in Bones)
                        if (b.OwnsVertex) n++;
                return n;
            }
        }

        /// <summary>Total animation frames across every clip — the budget number, in one place.</summary>
        public int TotalFrames
        {
            get
            {
                int n = 0;
                if (Clips != null)
                    foreach (SkinClip c in Clips) n += c.FrameCount;
                return n;
            }
        }

        /// <summary>Index of a bone by its rig id, or <see cref="NoBone"/>. Linear over 45 entries;
        /// a presenter resolves its mounts once at install, not per frame.</summary>
        public int IndexOfBone(string boneId)
        {
            if (Bones == null || string.IsNullOrEmpty(boneId)) return NoBone;
            for (int i = 0; i < Bones.Length; i++)
                if (string.Equals(Bones[i].Id, boneId, StringComparison.Ordinal)) return i;
            return NoBone;
        }

        public bool TryGetClip(string stateKey, out SkinClip clip)
        {
            if (Clips != null)
                foreach (SkinClip c in Clips)
                    if (string.Equals(c.StateKey, stateKey, StringComparison.Ordinal))
                    {
                        clip = c;
                        return true;
                    }
            clip = default;
            return false;
        }

        /// <summary>
        /// Whether this state is drawn from the skin rather than from its baked sheet. The
        /// per-state switch ADR 0041 requires.
        /// </summary>
        public bool DrawsAsMesh(string stateKey)
        {
            if (MeshStates == null || string.IsNullOrEmpty(stateKey)) return false;
            foreach (string s in MeshStates)
                if (string.Equals(s, stateKey, StringComparison.Ordinal)) return true;
            return false;
        }

        /// <summary>
        /// Everything that must be true for a presenter to draw this def. Deliberately strict about
        /// the two things that fail SILENTLY: a clip whose key array does not match its own frame
        /// and bone counts (which reads as another frame's pose, not as an error), and an influence
        /// count below two (which is the 452×-tolerance hem collapse).
        /// </summary>
        public bool IsUsable()
        {
            if (string.IsNullOrEmpty(Id)) return false;
            if (BindMesh == null) return false;
            if (Bones == null || Bones.Length == 0) return false;
            if (MaxInfluences < 1 || MaxInfluences > MaxBoneInfluences) return false;
            if (Materials == null || Materials.Length == 0) return false;
            if (Materials.Length > RampSlots) return false;
            foreach (Material m in Materials)
                if (m.Colors == null || m.Colors.Length == 0) return false;
            if (Bayer16 == null || Bayer16.Length != 16) return false;
            if (CellW <= 0 || CellH <= 0 || PxPerMetre <= 0) return false;

            for (int i = 0; i < Bones.Length; i++)
            {
                if (string.IsNullOrEmpty(Bones[i].Id)) return false;
                int p = Bones[i].Parent;
                // A parent must be EARLIER in the array: the rig declares parents first, and a
                // forward reference would make a single ordered pass compose the wrong matrix.
                if (p < NoBone || p >= i) return false;
            }
            if (Bones[0].Parent != NoBone) return false;

            if (Clips == null || Clips.Length == 0) return false;
            foreach (SkinClip clip in Clips)
            {
                if (string.IsNullOrEmpty(clip.Anim)) return false;
                if (string.IsNullOrEmpty(clip.StateKey)) return false;
                if (clip.FramesPerSecond <= 0f) return false;
                if (!clip.KeysWellFormed(Bones.Length)) return false;
            }
            return true;
        }
    }
}
