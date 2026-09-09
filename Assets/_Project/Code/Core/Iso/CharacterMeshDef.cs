using System;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>A rig-extracted 3D character, as a committed asset (ADR 0044).</b> One preset's whole
    /// pose flipbook — one mesh per (animation, frame) — plus everything the facet render path
    /// needs to draw it: the rig's palette ramps and per-material shading, its lighting constants,
    /// its dither matrix, its cell geometry, and the two facts a caller needs to POSE it (the
    /// MEASURED azimuth sign and the pivot).
    ///
    /// <para><b>Why a flipbook and not one mesh.</b> The boat rigs build a static face list once
    /// and apply heading/rock as transforms — which is why a hull is ONE mesh (ADR 0022's
    /// load-bearing fact). The character rig does not have that shape: <c>facesOf(pose(anim,u,b))</c>
    /// bakes the pose into the vertex positions, so there is no static <c>F</c>. One mesh per frame
    /// keeps the pose exactly as discrete as the baked sheets play it, while the HEADING becomes a
    /// live transform. Continuous heading is the whole point; continuous pose interpolation is
    /// explicitly not attempted.</para>
    ///
    /// <para><b>Why it lives in Core.</b> The Art module draws it (the facet URP pass) and the
    /// player/NPC controllers drive it by (anim, frame, heading), and neither may reference the
    /// other (CLAUDE.md rule 4). This asset is the data they share — the same reasoning as
    /// <see cref="HullMeshDef"/> and <c>CharacterVisualDef</c>. It deliberately contains no URP
    /// type: Art converts it to its own runtime setup on install.</para>
    ///
    /// <para>⚠️ <b><see cref="AzimuthCounterClockwise"/> is MEASURED, never assumed.</b> The
    /// character rig's turntable is <c>th = −dir·π/4</c> where the boats' is <c>+dir·π/4</c>, so the
    /// two lineages' conventions genuinely disagree and <c>RigCatalog.CharacterKit</c> records that
    /// this lane has been CCW-mislabelled twice. The baker renders the rig's own East view and asks
    /// which signed dir the reference rasteriser must pose to reproduce it, with a sabotage margin;
    /// the winner is stored here. Declaring it would ship a mirrored character.</para>
    ///
    /// <para>⚠️ <b>Per-material <see cref="Material.Gain"/> and <see cref="Material.Bias"/> are not
    /// decoration.</b> The shipped character rig (pass 6) gives 27 of its 36 materials their own
    /// shade gain and bias off a <c>SPAN</c> table — skin spans 0.40, leather 0.60 — where every
    /// hull rig shades from one global pair. Flattening them to the global gain was measured at
    /// 35.5–53.6% differing inked pixels against the rig's own render, against a hull-path reference
    /// band of 2.5–4.8%. They are carried here because the pixels say the character cannot be drawn
    /// without them. See ADR 0044 §"what rig 6 costs".</para>
    ///
    /// <para>⚠️ <b>The face is a raster STAMP, not geometry.</b> The rig's eyes, brows, lashes, lips
    /// and iris are painted by the head rig's <c>stamp()</c> pass after the polygons are drawn, and
    /// the materials that carry them are marked <see cref="Material.FixedIndex"/> ≥ 0. No face of
    /// this mesh references them. A mesh character therefore has no face until a presenter re-adds
    /// one — small in pixels (0.00–2.82% measured), owner-visible, and PR 2's problem.</para>
    /// </summary>
    public class CharacterMeshDef : ScriptableObject
    {
        /// <summary>The facet shader's <c>_RampMeta</c> is a <c>float4[16]</c>, so a preset whose
        /// faces reference more than sixteen distinct materials cannot be drawn without widening a
        /// real uniform array. Measured on the pass-6 cast: fisher uses 12, deckboss and packer 17.
        /// Guarded rather than discovered at draw time — the zodiac hit this exact wall.</summary>
        public const int RampSlots = 16;

        /// <summary>
        /// One rig material as the pose renderer uses it: a palette ramp, its constant index
        /// offset, and — unlike a hull — its OWN shade gain and bias.
        /// </summary>
        [Serializable]
        public struct Material
        {
            [Tooltip("The rig's MATS key, for the bake log and for reading a def by eye.")]
            public string Name;

            [Tooltip("The palette ramp, dark to light, exactly as the rig's MATS entry holds it.")]
            public Color32[] Colors;

            [Tooltip("The material's constant ramp-index offset ('off'; the dark aliases are negative).")]
            public int Offset;

            [Tooltip("The rig's per-material shade gain (MATS[k].gain, off the SPAN table). " +
                     "1 where the rig declares none. Multiplies the GLOBAL Gain, it does not replace it: " +
                     "fidx = sh*Gain*this + Bias + faceBias.")]
            public float Gain;

            [Tooltip("The rig's per-material shade bias (MATS[k].bias). NaN where the rig declares " +
                     "none, in which case the def's global Bias applies.")]
            public float Bias;

            [Tooltip("True when this material ordered-dithers between ramp steps (MATS[k].dith). " +
                     "FALSE on every material the shipped pass-6 cast uses — see DitherMode.")]
            public bool OrderedDither;

            [Tooltip("≥ 0 when the rig pins this material to a constant ramp index (MATS[k].idx). " +
                     "These are the head rig's STAMP materials (lash, lip, brow, iris, skinD/L); no " +
                     "polygon references them, so they colour no vertex of this mesh. Recorded so a " +
                     "presenter that re-adds the face has the rig's own numbers. −1 = shaded normally.")]
            public int FixedIndex;

            /// <summary>The material's effective shade bias — its own, or the def's global one.</summary>
            public float BiasOr(float globalBias) => float.IsNaN(Bias) ? globalBias : Bias;

            /// <summary>True when no polygon can reference this material (a face-stamp material).</summary>
            public bool IsStampOnly => FixedIndex >= 0;
        }

        /// <summary>
        /// How the rig resolves the fractional part of a shade index into a ramp step.
        ///
        /// <para>⚠️ <b>The character rig does NOT ordered-dither, and that is a decision, not an
        /// omission.</b> Pass 6 snaps at a hard 0.55 threshold for every material bar one, with the
        /// rig's own comment saying why: "ordered dither is a HAIR texture, not a global effect;
        /// garments/skin snap to a step, so flat panels read as clean colour blocks instead of a 50%
        /// transparency checker." Zero of the 36 materials a shipped preset resolves carry
        /// <c>dith</c>. Forcing the facet pass's Bayer dither onto her was measured at 19.7–30.2%
        /// differing inked pixels — it reintroduces exactly the checker pass 6 removed.</para>
        /// </summary>
        public enum ShadeStep
        {
            /// <summary>Hard threshold at <see cref="HardStepThreshold"/> — what the character rig does.</summary>
            HardThreshold = 0,
            /// <summary>4×4 ordered dither against <see cref="Bayer16"/> — what the hull rigs do.</summary>
            OrderedDither = 1,
        }

        /// <summary>One animation's worth of pose meshes, in the rig's own frame order.</summary>
        [Serializable]
        public struct PoseClip
        {
            [Tooltip("The rig ANIMS key this clip snapshots (e.g. 'idle', 'walk', 'haul').")]
            public string Anim;

            [Tooltip("The STATE key, in the sheet baker's own spelling: 'walk', 'cast_long', " +
                     "'walk_buckets', 'reach_stowV'. One anim can be several states — the rig " +
                     "re-solves the whole pose per power, per carry stance and per rest height — " +
                     "so the state, not the anim, is what a clip answers to.")]
            public string State;

            [Tooltip("Playback rate, from the rig's own ANIMS.ms (fps = 1000/ms).")]
            public float FramesPerSecond;

            [Tooltip("True when the rig marks this clip 'settle' — it spans its frames INCLUSIVELY " +
                     "(u = f/(frames-1)) rather than cyclically. Carried because a player that " +
                     "loops a settle clip plays a different pose on the last frame than the bake did.")]
            public bool Settle;

            [Tooltip("One baked mesh per frame, pose baked into the vertices; heading stays live.")]
            public Mesh[] Frames;

            /// <summary>The key a caller looks this clip up by. <see cref="State"/> when the bake
            /// set one, else the bare anim — the same string the sprite path names its sheet with,
            /// which is what lets <see cref="MeshStates"/> retire a sheet state for state.</summary>
            public string StateKey => string.IsNullOrEmpty(State) ? Anim : State;
        }

        [Tooltip("Stable id (append-only), 'charmesh.<preset>'.")]
        public string Id = "";

        [Header("Provenance (the stale-bake guard)")]
        [Tooltip("The rig this was baked from, repo-relative.")]
        public string SourceRigPath = "";

        [Tooltip("The rig's own revision string (CharacterIso6.revision), e.g. '6.9'.")]
        public string SourceRigRevision = "";

        [Tooltip("SHA-256 of the rig source, newline-normalised to LF. A bake whose hash no longer " +
                 "matches the working tree is STALE — the guard says so rather than letting a " +
                 "silently-outdated character ship, which is the failure HullMeshDef has no defence " +
                 "against.")]
        public string SourceRigSha256 = "";

        [Tooltip("The BUILDS preset this flipbook is (fisher, ginny, skipper, …).")]
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
        [Tooltip("The rig's hard step threshold (pass 6: 0.55). Unused when StepMode is OrderedDither.")]
        public float HardStepThreshold = 0.55f;
        [Tooltip("4×4 ordered-dither thresholds, (v+0.5)/16, row-major [x*4+y]. Carried even when " +
                 "StepMode is HardThreshold, because individual materials may still opt in.")]
        public float[] Bayer16 = Array.Empty<float>();
        [Tooltip("The rig's keyline colour.")]
        public Color32 Keyline;
        [Tooltip("The materials the faces of this flipbook actually reference, in the rig's own " +
                 "MATS order — the index a face's UV0.x carries.")]
        public Material[] Materials = Array.Empty<Material>();

        [Header("Pose facts (MEASURED, never declared)")]
        [Tooltip("True when the mapping from heading to rig dir-units must NEGATE. Adjudicated in " +
                 "pixels at bake time against the rig's own East view, with a 4× sabotage margin.")]
        public bool AzimuthCounterClockwise;

        [Header("The flipbook")]
        public PoseClip[] Clips = Array.Empty<PoseClip>();

        [Header("Adoption (ADR 0041: sheets retire per state, at parity)")]
        [Tooltip("The state keys ('idle', 'walk|buckets', …) this character is DRAWN AS A MESH for. " +
                 "Every other state still plays its baked sheet. Empty means the flipbook is baked " +
                 "and inspectable but nothing draws from it yet — which is PR 1's shipped state.")]
        public string[] MeshStates = Array.Empty<string>();

        /// <summary>Total pose meshes across every clip — the budget number, in one place.</summary>
        public int TotalMeshes
        {
            get
            {
                int n = 0;
                if (Clips != null)
                    foreach (var c in Clips)
                        if (c.Frames != null) n += c.Frames.Length;
                return n;
            }
        }

        public bool IsUsable()
        {
            if (string.IsNullOrEmpty(Id)) return false;
            if (Materials == null || Materials.Length == 0) return false;
            if (Materials.Length > RampSlots) return false;
            foreach (var m in Materials)
                if (m.Colors == null || m.Colors.Length == 0) return false;
            if (Bayer16 == null || Bayer16.Length != 16) return false;
            if (CellW <= 0 || CellH <= 0 || PxPerMetre <= 0) return false;
            if (Clips == null || Clips.Length == 0) return false;
            foreach (var clip in Clips)
            {
                if (string.IsNullOrEmpty(clip.Anim)) return false;
                if (string.IsNullOrEmpty(clip.StateKey)) return false;
                if (clip.FramesPerSecond <= 0f) return false;
                if (clip.Frames == null || clip.Frames.Length == 0) return false;
                foreach (var m in clip.Frames)
                    if (m == null) return false;
            }
            return true;
        }

        public bool TryGetClip(string stateKey, out PoseClip clip)
        {
            if (Clips != null)
                foreach (var c in Clips)
                    if (string.Equals(c.StateKey, stateKey, StringComparison.Ordinal))
                    {
                        clip = c;
                        return true;
                    }
            clip = default;
            return false;
        }

        /// <summary>
        /// Whether this state is drawn from the flipbook rather than from its baked sheet. The
        /// per-state switch ADR 0041 requires: a sheet retires only once its mesh state is on and at
        /// parity, so both paths stand in parallel while the cast crosses over.
        /// </summary>
        public bool DrawsAsMesh(string stateKey)
        {
            if (MeshStates == null || string.IsNullOrEmpty(stateKey)) return false;
            foreach (string s in MeshStates)
                if (string.Equals(s, stateKey, StringComparison.Ordinal)) return true;
            return false;
        }
    }
}
