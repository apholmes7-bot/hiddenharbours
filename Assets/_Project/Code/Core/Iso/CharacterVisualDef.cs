using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>Which locomotion sheet a character is showing this frame. Ordered by speed.</summary>
    public enum CharacterGait
    {
        /// <summary>Standing still — the idle sheet.</summary>
        Idle = 0,
        /// <summary>Moving at an ordinary walking pace — the walk sheet.</summary>
        Walk = 1,
        /// <summary>Moving fast enough to run — the run sheet.</summary>
        Run = 2,
    }

    /// <summary>
    /// <b>What the character's BODY is doing besides travelling</b> — the second axis of the character
    /// sheets, orthogonal to <see cref="CharacterGait"/>. The rig bakes these as separate sheets because
    /// they are separate poses, not tints of one: a fisher braced against a rolling deck stands
    /// differently from one strolling the wharf, and one with both hands on a wheel differently again.
    ///
    /// <para><b>Append-only</b> (CLAUDE.md §5): these name ART, and art ids are stable. The rig's own
    /// <c>CARRIES</c> table (<c>characterIsoRig6.js</c>) is the source — <c>helm</c> and <c>oars</c> are
    /// carry stances there, <c>balance</c> is a deck anim — so a stance added here must exist there first.</para>
    /// </summary>
    public enum CharacterStance
    {
        /// <summary>Hands empty, feet on solid ground — the plain idle / walk / run sheets. The default,
        /// and what every character showed before stances existed.</summary>
        Free = 0,
        /// <summary>Braced on a working deck: the rig's <c>balance</c> clip, a gentle ±10° list that reads
        /// as keeping your feet under you. Idle only — a character CROSSING the deck walks normally.</summary>
        Balance = 1,
        /// <summary>Piloting from a wheel or tiller: the rig's <c>helm</c> carry, hands pinned to the helm
        /// and the body leaned 6° into it.</summary>
        Helm = 2,
        /// <summary>At the oars: the rig's <c>oars</c> carry, hands pinned to the looms and a 10° lean.
        /// The rowed hull's answer to <see cref="Helm"/>.</summary>
        Oars = 3,
    }

    /// <summary>
    /// One STANCE's worth of art: the same idle/walk pair the free body carries, for a character whose
    /// hands and posture are committed to something. Its own frame counts and rates because the rig bakes
    /// its own — <c>balance</c> is 8 frames at 150 ms where the free idle is 6 at 170.
    ///
    /// <para><b>Why idle + walk and no run.</b> The rig's <c>CARRIES</c> table declares
    /// <c>anims:['idle','walk']</c> for both piloting stances, and <c>balance</c> is an idle clip with no
    /// walking twin at all. A stance that leaves a sheet empty is not broken — the presenter falls back to
    /// the FREE body for that gait, which is exactly right: you cross a rolling deck at an ordinary walk
    /// and only brace when you stop.</para>
    ///
    /// <para><b>All-or-nothing, like every other sheet here</b> (<see cref="CharacterVisualDef.HasGait"/>):
    /// a stance sheet counts as wired only when it is COMPLETE. A short one is dropped whole rather than
    /// indexing a stale cell mid-cycle.</para>
    /// </summary>
    [System.Serializable]
    public class CharacterStanceSheets
    {
        [Tooltip("Idle frames in this stance, element [direction·IdleFrameCount + frame]. Empty/short = " +
                 "this stance has no idle art and the FREE idle draws instead.")]
        public Sprite[] IdleSheet = System.Array.Empty<Sprite>();
        [Tooltip("Frames per direction on this stance's idle sheet (from the rig's own ANIMS table).")]
        [Min(1)] public int IdleFrameCount = 6;
        [Tooltip("Playback rate of this stance's idle sheet, in frames per second (1000 / the rig's ms).")]
        [Min(0f)] public float IdleFramesPerSecond = 6f;

        [Tooltip("Walk frames in this stance, element [direction·WalkFrameCount + frame]. Empty/short = " +
                 "this stance has no walk art and the FREE walk draws instead — which is what the deck " +
                 "'balance' stance wants: you brace standing still and walk normally when you cross.")]
        public Sprite[] WalkSheet = System.Array.Empty<Sprite>();
        [Tooltip("Frames per direction on this stance's walk sheet.")]
        [Min(1)] public int WalkFrameCount = 8;
        [Tooltip("Playback rate of this stance's walk sheet, in frames per second.")]
        [Min(0f)] public float WalkFramesPerSecond = 10f;
    }

    /// <summary>
    /// <b>A whole-body CLIP the character plays as an EVENT</b> — the third axis, and the one neither
    /// <see cref="CharacterGait"/> nor <see cref="CharacterStance"/> can express. A gait is chosen from
    /// measured speed and a stance from context; a clip is <b>started</b>, runs on its own clock, and
    /// ends. Nothing about "climbing over a rail" can be derived from how fast the fisher is moving.
    ///
    /// <para><b>Append-only</b> (CLAUDE.md §5): these name ART. Each maps to one anim in the rig's own
    /// <c>ANIMS</c> table (<c>characterIsoRig6.js</c> rev 6.2), so a clip added here must exist there
    /// first — and the rig's <c>ANIM_MOUNT</c> decides whether a carry stance or a prop layer may ride
    /// it (<see cref="LadderDown"/> is the rig's <c>'ladder'</c> mount: both hands are committed).</para>
    /// </summary>
    public enum CharacterClip
    {
        /// <summary>No clip — the gait/stance presenter owns the renderer. The default.</summary>
        None = 0,
        /// <summary>Stepping up and over a rail, from the rig's <c>board</c> (10 f, 90 ms, one-shot).
        /// The clip the boarding move's vault plays.</summary>
        Board = 1,
        /// <summary>Stepping ashore off a rail, from the rig's <c>boardDown</c> (6 f, 95 ms, one-shot).
        /// Authored, not a mirror of <see cref="Board"/> — going down the hand only steadies.</summary>
        BoardDown = 2,
        /// <summary>A two-handed heave on a line, from the rig's <c>haul</c> (8 f, 120 ms, LOOPING).
        /// The pass-6 answer to the legacy hand-pixelled <c>PlayerHaul.png</c>, which was one facing at
        /// a different cell and unusable at seven of the eight headings.</summary>
        Haul = 3,
        /// <summary>Climbing DOWN a fixed ladder, from the rig's <c>ladderDown</c> (10 f, 110 ms,
        /// LOOPING). Locomotion, not a transition: the cell plays in place and the engine translates the
        /// sprite down the ladder. There is no <c>ladderUp</c> and it is not this reversed.</summary>
        LadderDown = 4,
    }

    /// <summary>
    /// One CLIP's worth of art: a direction × frame sheet like every other, plus the two facts a clip
    /// carries that a gait sheet does not — whether it LOOPS, and how many directions it was baked at.
    ///
    /// <para><b>Why its own facing count.</b> The gait sheets all bake the def's
    /// <see cref="CharacterVisualDef.FacingCount"/>, but a clip need not: a ladder climb is often
    /// authored at one or two facings because a climber faces the ladder, and a kit is free to ship it
    /// that way. Leave this <b>0</b> to inherit the def's count (what the pass-6 kit wants — it bakes
    /// all four clips at the full eight). Set it and the row snap re-solves against THAT many
    /// directions, so a 2-facing climb never indexes row 5 of a 2-row sheet.</para>
    ///
    /// <para><b>All-or-nothing</b>, like every other sheet here
    /// (<see cref="CharacterVisualDef.HasClip"/>): a clip counts as wired only when it is COMPLETE.
    /// A short one is dropped whole and the clip simply never plays — the caller's fallback is
    /// whatever drew the character already, which is always a legal picture.</para>
    /// </summary>
    [System.Serializable]
    public class CharacterClipSheets
    {
        [Tooltip("Clip frames, element [direction·FrameCount + frame]. Empty/short = this clip has no " +
                 "art and never plays.")]
        public Sprite[] Sheet = System.Array.Empty<Sprite>();
        [Tooltip("Frames per direction on this clip's sheet (from the rig's own ANIMS table).")]
        [Min(1)] public int FrameCount = 1;
        [Tooltip("The clip's NATURAL playback rate in frames per second (1000 / the rig's ms). Used when " +
                 "a caller plays the clip unscaled; a caller that scales the clip to a move's own " +
                 "duration overrides it.")]
        [Min(0f)] public float FramesPerSecond = 10f;
        [Tooltip("LOOPS (haul, ladderDown) rather than playing once and holding its last frame (board, " +
                 "boardDown). The rig's ANIMS table states this per clip — oneShot there is loop=false " +
                 "here.")]
        public bool Loops = false;
        [Tooltip("Directions this clip was baked at, or 0 to inherit the def's FacingCount. Set it ONLY " +
                 "for a clip whose kit baked fewer rows than the body (a 1–2 facing ladder climb); the " +
                 "pass-6 kit bakes all four clips at the full eight, so 0 is right for it.")]
        [Min(0)] public int FacingCount = 0;
    }

    /// <summary>
    /// The stance + gait a character is ACTUALLY drawing, after the availability ladders have run — what
    /// <see cref="CharacterVisualDef.Playable"/> answers. A tiny pair so the resolution is one pure,
    /// EditMode-testable call rather than two out-params that a caller can combine wrongly.
    /// </summary>
    public readonly struct CharacterPose
    {
        /// <summary>The stance whose sheets will actually be indexed.</summary>
        public readonly CharacterStance Stance;
        /// <summary>The gait within that stance.</summary>
        public readonly CharacterGait Gait;

        public CharacterPose(CharacterStance stance, CharacterGait gait)
        {
            Stance = stance;
            Gait = gait;
        }
    }

    /// <summary>
    /// <b>How a CHARACTER looks on foot — as data, not as a const (ADR 0003, rule 2).</b> One of these
    /// describes a complete 8-direction ¾-iso character skin: the three locomotion sheets (idle / walk /
    /// run), each laid out as <c>direction × frame</c>, plus the facts about how the art was BAKED (how
    /// many directions, which heading cell 0 depicts, and which way round the cells run) and the speed
    /// thresholds that pick a gait.
    ///
    /// <para><b>Why this lives in Core.</b> The on-foot player (<c>HiddenHarbours.Player</c>) and, later,
    /// the harbour's NPCs (<c>world-content</c>) are different feature modules and neither may reference
    /// the other's concrete classes (CLAUDE.md rule 4). They both need the SAME presenter and the SAME
    /// heading→cell rule, so both live here beside <see cref="IsoFacing"/> — which PR #216 already put in
    /// Core for exactly this reason ("the CHARACTER rigs are baked to the same 8-way convention"). Core is
    /// also the only module every feature module already references, so nothing gains a new asmdef edge.</para>
    ///
    /// <para><b>Conventions this asset must honour.</b> Heading 0 = North, degrees CLOCKWISE.
    /// <see cref="IdleSheet"/>/<see cref="WalkSheet"/>/<see cref="RunSheet"/> are flat arrays in the
    /// sheets' own row-major slice order, element <c>direction·frameCount + frame</c> — i.e. one ROW per
    /// direction, one COLUMN per frame, exactly as <c>CharacterSheetSlicer</c> cuts them and as the
    /// <c>&lt;Stem&gt;_d&lt;dir&gt;_f&lt;frame&gt;</c> sub-sprite names say. Cell 64×92 at PPU 32 (the
    /// slicer's <c>CellW</c>×<c>CellH</c>; 64×88 was the pass-1 cell, retired 2026-08-02) with the
    /// pivot on GROUND CONTACT, so swapping frames never moves the feet — do not add per-frame Y offsets.</para>
    ///
    /// <para><b>All-or-nothing per sheet</b>, mirroring <c>BoatVisualDef</c>: a sheet counts as wired only
    /// when it is COMPLETE (<see cref="HasGait"/>). A partial set would index a stale cell mid-stride, so a
    /// short sheet is dropped whole and the presenter falls back down the gait ladder (run → walk → idle),
    /// ending at "no art at all", where the presenter is inert and whatever drew the character before still
    /// draws it.</para>
    ///
    /// Create via Assets &gt; Create &gt; Hidden Harbours &gt; Character Visual; the committed assets are
    /// written by <c>CharacterVisualLibraryBuilder</c>.
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Character Visual", fileName = "CharacterVisual")]
    public class CharacterVisualDef : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable id, append-only (CLAUDE.md §5): type.snake_case, e.g. visual.fisher_iso.")]
        public string Id = "visual.fisher_iso";

        [Header("How the art was BAKED (art facts — not feel knobs)")]
        [Tooltip("How many pre-drawn directions each sheet carries — one ROW per direction. The shipped " +
                 "character kits bake 8. The snap math is generalised to any count, so a 16-way re-bake " +
                 "drops in with no code change.")]
        [Min(1)] public int FacingCount = 8;

        [Tooltip("The compass heading (degrees, 0 = North/up, CW) that direction row 0 is drawn for. 0 " +
                 "because row 0 is the North-facing picture — the project's bearing convention.")]
        public float ZeroHeadingDegrees = 0f;

        [Tooltip("Tick ONLY for art whose direction rows run COUNTER-CLOCKWISE — i.e. row i depicts heading " +
                 "−45°·i, not +45°·i. ✅ The shipped CHARACTER kits are false: the rig that bakes them used " +
                 "to rotate the model CCW while LABELLING the rows clockwise (so the row called 'E' was " +
                 "really a fisher facing West), but that rig has been corrected at source and all twelve " +
                 "body sheets re-baked, so their rows now mean what they say. ⚠️ The iso BOAT kits were NOT " +
                 "re-baked and are still true — which is exactly why this is per-artwork DATA and not one " +
                 "global const: two art lineages genuinely disagree, and a blanket fix in code would repair " +
                 "one and break the other. Ticking this on already-correct art re-mirrors it.")]
        public bool FacingsAreCounterClockwise = false;

        [Header("Idle sheet (direction × frame, row-major)")]
        [Tooltip("Idle frames, element [direction·IdleFrameCount + frame]. Empty/short = no idle art.")]
        public Sprite[] IdleSheet = System.Array.Empty<Sprite>();
        [Tooltip("Frames per direction on the idle sheet (the shipped Fisher kit bakes 6).")]
        [Min(1)] public int IdleFrameCount = 6;
        [Tooltip("Playback rate of the idle sheet, in frames per second. A slow breathing idle.")]
        [Min(0f)] public float IdleFramesPerSecond = 6f;

        [Header("Walk sheet (direction × frame, row-major)")]
        [Tooltip("Walk frames, element [direction·WalkFrameCount + frame]. Empty/short = no walk art " +
                 "(the presenter then shows idle whatever the speed).")]
        public Sprite[] WalkSheet = System.Array.Empty<Sprite>();
        [Tooltip("Frames per direction on the walk sheet (the shipped Fisher kit bakes 8).")]
        [Min(1)] public int WalkFrameCount = 8;
        [Tooltip("Playback rate of the walk sheet, in frames per second.")]
        [Min(0f)] public float WalkFramesPerSecond = 10f;

        [Header("Run sheet (direction × frame, row-major)")]
        [Tooltip("Run frames, element [direction·RunFrameCount + frame]. Empty/short = no run art (the " +
                 "presenter then tops out at the walk cycle however fast the character moves).")]
        public Sprite[] RunSheet = System.Array.Empty<Sprite>();
        [Tooltip("Frames per direction on the run sheet (the shipped Fisher kit bakes 6).")]
        [Min(1)] public int RunFrameCount = 6;
        [Tooltip("Playback rate of the run sheet, in frames per second.")]
        [Min(0f)] public float RunFramesPerSecond = 12f;

        [Header("Gait thresholds (owner-tunable — rule 6, no magic numbers)")]
        [Tooltip("Speed (m/s) at or above which the character reads as MOVING rather than standing. A small " +
                 "dead-band so physics jitter, a shove from a collider, or a wave nudge on deck never " +
                 "twitches the idle into a step.")]
        [Min(0f)] public float WalkSpeedThreshold = 0.35f;

        [Tooltip("Speed (m/s) at or above which the character breaks into a RUN. Set ABOVE the on-foot walk " +
                 "speed (GameConfig / PlayerWalkController._moveSpeed, 3 m/s today) so the ordinary walk " +
                 "never triggers it; the moment a sprint speed exists on the controller, the run sheet plays " +
                 "with no code change. Ignored when no run art is wired.")]
        [Min(0f)] public float RunSpeedThreshold = 4.5f;

        [Header("Stances (append-only — the body doing something besides travelling)")]
        [Tooltip("BRACED ON A DECK — the rig's 'balance' clip. Idle only: a character crossing a rolling " +
                 "deck walks on the free walk sheet and braces when they stop. Leave empty and the deck " +
                 "rider simply idles as they do ashore.")]
        public CharacterStanceSheets BalanceStance = new CharacterStanceSheets();

        [Tooltip("AT THE WHEEL / TILLER — the rig's 'helm' carry. What a pilot shows on a hull steered by " +
                 "hand. Leave empty and the pilot falls back to the free body.")]
        public CharacterStanceSheets HelmStance = new CharacterStanceSheets();

        [Tooltip("AT THE OARS — the rig's 'oars' carry. What a rower shows on a pulled hull (the dory, the " +
                 "punt), drawn alongside the hull's own oar overlays, which are separate sprites by design.")]
        public CharacterStanceSheets OarsStance = new CharacterStanceSheets();

        [Header("Clips (append-only — whole-body actions played as EVENTS, not chosen from speed)")]
        [Tooltip("STEPPING OVER A RAIL — the rig's 'board' clip (one-shot). What the boarding move's vault " +
                 "plays. Leave empty and the vault falls back to the measured-speed walk it showed before.")]
        public CharacterClipSheets BoardClip = new CharacterClipSheets
        { FrameCount = 10, FramesPerSecond = 1000f / 90f, Loops = false };

        [Tooltip("STEPPING ASHORE — the rig's 'boardDown' clip (one-shot). The disembarking half of the " +
                 "same move; authored separately rather than mirrored.")]
        public CharacterClipSheets BoardDownClip = new CharacterClipSheets
        { FrameCount = 6, FramesPerSecond = 1000f / 95f, Loops = false };

        [Tooltip("HEAVING ON A LINE — the rig's 'haul' clip (LOOPING). The 8-direction replacement for the " +
                 "legacy one-facing PlayerHaul sheet.")]
        public CharacterClipSheets HaulClip = new CharacterClipSheets
        { FrameCount = 8, FramesPerSecond = 1000f / 120f, Loops = true };

        [Tooltip("CLIMBING DOWN A LADDER — the rig's 'ladderDown' clip (LOOPING). The art for the tide-gap " +
                 "boarding slice; playing it is this def's job, deciding WHEN is the gameplay slice's.")]
        public CharacterClipSheets LadderDownClip = new CharacterClipSheets
        { FrameCount = 10, FramesPerSecond = 1000f / 110f, Loops = true };

        // ---- the all-or-nothing gates + lookups (pure; EditMode-testable without a scene) ----------

        /// <summary>The sheet for a gait (never null — an unwired gait returns an empty array).</summary>
        public Sprite[] SheetFor(CharacterGait gait) => gait switch
        {
            CharacterGait.Run => RunSheet ?? System.Array.Empty<Sprite>(),
            CharacterGait.Walk => WalkSheet ?? System.Array.Empty<Sprite>(),
            _ => IdleSheet ?? System.Array.Empty<Sprite>(),
        };

        /// <summary>Frames per direction on a gait's sheet.</summary>
        public int FrameCountFor(CharacterGait gait) => gait switch
        {
            CharacterGait.Run => Mathf.Max(1, RunFrameCount),
            CharacterGait.Walk => Mathf.Max(1, WalkFrameCount),
            _ => Mathf.Max(1, IdleFrameCount),
        };

        /// <summary>Playback rate (fps) for a gait's sheet.</summary>
        public float FramesPerSecondFor(CharacterGait gait) => gait switch
        {
            CharacterGait.Run => Mathf.Max(0f, RunFramesPerSecond),
            CharacterGait.Walk => Mathf.Max(0f, WalkFramesPerSecond),
            _ => Mathf.Max(0f, IdleFramesPerSecond),
        };

        /// <summary>
        /// True when a gait's sheet is COMPLETE — exactly <see cref="FacingCount"/> × its frame count, with
        /// every slot assigned. The gate every consumer plays that gait behind; anything short is dropped
        /// whole rather than half-played.
        /// </summary>
        public bool HasGait(CharacterGait gait)
        {
            var sheet = SheetFor(gait);
            int expected = Mathf.Max(1, FacingCount) * FrameCountFor(gait);
            if (sheet.Length != expected || expected <= 0) return false;
            for (int i = 0; i < sheet.Length; i++)
                if (sheet[i] == null) return false;
            return true;
        }

        /// <summary>True when at least the idle sheet is wired — i.e. this def can draw the character at
        /// all. False means the presenter must stay inert and leave the renderer alone.</summary>
        public bool HasAnyArt() => HasGait(CharacterGait.Idle);

        /// <summary>
        /// The gait actually PLAYABLE given what art is wired, walking down the ladder run → walk → idle.
        /// A character with no run sheet tops out at walk however fast it moves; one with no walk sheet
        /// stands still-looking rather than showing a stale cell.
        /// </summary>
        public CharacterGait PlayableGait(CharacterGait wanted)
        {
            if (wanted == CharacterGait.Run && !HasGait(CharacterGait.Run)) wanted = CharacterGait.Walk;
            if (wanted == CharacterGait.Walk && !HasGait(CharacterGait.Walk)) wanted = CharacterGait.Idle;
            return wanted;
        }

        /// <summary>
        /// The DIRECTION ROW that actually depicts a compass heading — the one place this def's bake facts
        /// (<see cref="FacingCount"/>, <see cref="ZeroHeadingDegrees"/>,
        /// <see cref="FacingsAreCounterClockwise"/>) meet the shared, tested
        /// <see cref="IsoFacing.HeadingToFacingIndex"/>. Nothing re-implements the snap.
        /// </summary>
        public int FacingRowFor(float headingDegrees) =>
            IsoFacing.HeadingToFacingIndex(headingDegrees, Mathf.Max(1, FacingCount), ZeroHeadingDegrees,
                                           FacingsAreCounterClockwise);

        /// <summary>The sprite for a gait / direction row / frame, or null if that cell isn't wired.
        /// Row and frame wrap (negative-safe), so a mid-stride sheet swap can never index out of range.</summary>
        public Sprite SpriteFor(CharacterGait gait, int facingRow, int frame)
            => SpriteFor(CharacterStance.Free, gait, facingRow, frame);

        // ---- the STANCE axis: the same four questions, asked of a stance's sheets ------------------
        // Every one of these is Free-identical by construction — CharacterStance.Free routes straight
        // back to the flat fields above, so a def with no stance art answers exactly as it did before
        // stances existed. That is the A/B contract the EditMode tests pin.

        /// <summary>The stance block for a stance, or null for <see cref="CharacterStance.Free"/> (whose
        /// sheets are the flat fields) and for anything unrecognised.</summary>
        public CharacterStanceSheets StanceSheetsFor(CharacterStance stance) => stance switch
        {
            CharacterStance.Balance => BalanceStance,
            CharacterStance.Helm => HelmStance,
            CharacterStance.Oars => OarsStance,
            _ => null,
        };

        /// <summary>The sheet for a stance + gait (never null — an unwired slot returns an empty array).
        /// A stance has no RUN art by design (see <see cref="CharacterStanceSheets"/>), so asking for one
        /// returns empty and the caller's ladder sends it back to the free body.</summary>
        public Sprite[] SheetFor(CharacterStance stance, CharacterGait gait)
        {
            var s = StanceSheetsFor(stance);
            if (s == null) return SheetFor(gait);
            return gait switch
            {
                CharacterGait.Walk => s.WalkSheet ?? System.Array.Empty<Sprite>(),
                CharacterGait.Idle => s.IdleSheet ?? System.Array.Empty<Sprite>(),
                _ => System.Array.Empty<Sprite>(),   // no stance bakes a run
            };
        }

        /// <summary>Frames per direction on a stance + gait's sheet.</summary>
        public int FrameCountFor(CharacterStance stance, CharacterGait gait)
        {
            var s = StanceSheetsFor(stance);
            if (s == null) return FrameCountFor(gait);
            return gait switch
            {
                CharacterGait.Walk => Mathf.Max(1, s.WalkFrameCount),
                _ => Mathf.Max(1, s.IdleFrameCount),
            };
        }

        /// <summary>Playback rate (fps) for a stance + gait's sheet.</summary>
        public float FramesPerSecondFor(CharacterStance stance, CharacterGait gait)
        {
            var s = StanceSheetsFor(stance);
            if (s == null) return FramesPerSecondFor(gait);
            return gait switch
            {
                CharacterGait.Walk => Mathf.Max(0f, s.WalkFramesPerSecond),
                _ => Mathf.Max(0f, s.IdleFramesPerSecond),
            };
        }

        /// <summary>True when a stance's sheet for a gait is COMPLETE — the same all-or-nothing gate
        /// <see cref="HasGait"/> applies to the free body, for the same reason.</summary>
        public bool HasStanceGait(CharacterStance stance, CharacterGait gait)
        {
            if (StanceSheetsFor(stance) == null) return HasGait(gait);
            var sheet = SheetFor(stance, gait);
            int expected = Mathf.Max(1, FacingCount) * FrameCountFor(stance, gait);
            if (sheet.Length != expected || expected <= 0) return false;
            for (int i = 0; i < sheet.Length; i++)
                if (sheet[i] == null) return false;
            return true;
        }

        /// <summary>
        /// <b>The one resolution rule</b> — what a character asking for <paramref name="stance"/> at
        /// <paramref name="wanted"/> speed actually DRAWS, given what art is wired.
        ///
        /// <para>The stance is tried FIRST and at the wanted gait only: if it bakes that gait, that is the
        /// answer. Otherwise the whole thing falls back to the FREE body and the existing gait ladder
        /// (<see cref="PlayableGait"/>: run → walk → idle). That order is the point — a deck-braced
        /// character who starts WALKING must show the free WALK, never the braced idle held while they
        /// slide across the deck. Laddering the gait first would do exactly that.</para>
        ///
        /// <para>Pure and total: every input answers with a pose, and a def with no stance art at all
        /// always answers <see cref="CharacterStance.Free"/> — byte-identical to the pre-stance
        /// presenter.</para>
        /// </summary>
        public CharacterPose Playable(CharacterStance stance, CharacterGait wanted)
        {
            if (stance != CharacterStance.Free && HasStanceGait(stance, wanted))
                return new CharacterPose(stance, wanted);
            return new CharacterPose(CharacterStance.Free, PlayableGait(wanted));
        }

        /// <summary>The sprite for a stance / gait / direction row / frame, or null if that cell isn't
        /// wired. Row and frame wrap (negative-safe), so a mid-cycle sheet swap can never index out of
        /// range — a stance change mid-stride is exactly that.</summary>
        public Sprite SpriteFor(CharacterStance stance, CharacterGait gait, int facingRow, int frame)
        {
            var sheet = SheetFor(stance, gait);
            if (sheet.Length == 0) return null;
            int facings = Mathf.Max(1, FacingCount);
            int frames = FrameCountFor(stance, gait);
            int row = ((facingRow % facings) + facings) % facings;
            int col = ((frame % frames) + frames) % frames;
            int idx = row * frames + col;
            return (idx >= 0 && idx < sheet.Length) ? sheet[idx] : null;
        }

        // ---- the CLIP axis: the same questions, asked of a clip's sheet ----------------------------
        // Deliberately parallel to the stance block above rather than folded into it: a clip is not a
        // gait, has no gait ladder to fall down, and carries two facts a stance sheet does not (it may
        // LOOP, and it may have been baked at fewer directions than the body).

        /// <summary>The block for a clip, or null for <see cref="CharacterClip.None"/> and anything
        /// unrecognised.</summary>
        public CharacterClipSheets ClipSheetsFor(CharacterClip clip) => clip switch
        {
            CharacterClip.Board => BoardClip,
            CharacterClip.BoardDown => BoardDownClip,
            CharacterClip.Haul => HaulClip,
            CharacterClip.LadderDown => LadderDownClip,
            _ => null,
        };

        /// <summary>Frames per direction on a clip's sheet (1 for an unknown clip — never 0, so no
        /// caller divides by it).</summary>
        public int ClipFrameCount(CharacterClip clip)
        {
            var c = ClipSheetsFor(clip);
            return c == null ? 1 : Mathf.Max(1, c.FrameCount);
        }

        /// <summary>A clip's NATURAL playback rate (fps) — what it plays at unscaled.</summary>
        public float ClipFramesPerSecond(CharacterClip clip)
        {
            var c = ClipSheetsFor(clip);
            return c == null ? 0f : Mathf.Max(0f, c.FramesPerSecond);
        }

        /// <summary>True when a clip LOOPS (haul, ladderDown) rather than playing once and holding its
        /// last frame (board, boardDown).</summary>
        public bool ClipLoops(CharacterClip clip)
        {
            var c = ClipSheetsFor(clip);
            return c != null && c.Loops;
        }

        /// <summary>
        /// How many direction rows a clip's sheet actually carries — its own
        /// <see cref="CharacterClipSheets.FacingCount"/> when set, else the def's
        /// <see cref="FacingCount"/>. <b>The one place the "clips need not bake eight" rule lives</b>:
        /// every clip row snap and every clip index goes through this, so a kit that ships a 2-facing
        /// ladder climb needs no code change and can never index a row its sheet does not have.
        /// </summary>
        public int ClipFacingCount(CharacterClip clip)
        {
            var c = ClipSheetsFor(clip);
            int own = c == null ? 0 : c.FacingCount;
            return Mathf.Max(1, own > 0 ? own : FacingCount);
        }

        /// <summary>True when a clip's sheet is COMPLETE — exactly <see cref="ClipFacingCount"/> ×
        /// its frame count, every slot assigned. The gate a clip plays behind; anything short means the
        /// clip never plays and the caller keeps whatever was drawing the character.</summary>
        public bool HasClip(CharacterClip clip)
        {
            var c = ClipSheetsFor(clip);
            if (c == null) return false;
            var sheet = c.Sheet ?? System.Array.Empty<Sprite>();
            int expected = ClipFacingCount(clip) * ClipFrameCount(clip);
            if (sheet.Length != expected || expected <= 0) return false;
            for (int i = 0; i < sheet.Length; i++)
                if (sheet[i] == null) return false;
            return true;
        }

        /// <summary>
        /// The DIRECTION ROW of a CLIP's sheet that depicts a compass heading — the clip twin of
        /// <see cref="FacingRowFor"/>, snapped against <see cref="ClipFacingCount"/> rather than the
        /// body's. A clip baked at two facings snaps the whole compass into those two rows; one baked
        /// at the body's eight answers exactly what <see cref="FacingRowFor"/> answers.
        /// </summary>
        public int ClipFacingRowFor(CharacterClip clip, float headingDegrees) =>
            IsoFacing.HeadingToFacingIndex(headingDegrees, ClipFacingCount(clip), ZeroHeadingDegrees,
                                           FacingsAreCounterClockwise);

        /// <summary>The sprite for a clip / direction row / frame, or null if that cell isn't wired. Row
        /// and frame wrap (negative-safe) against the CLIP's own counts.</summary>
        public Sprite ClipSpriteFor(CharacterClip clip, int facingRow, int frame)
        {
            var c = ClipSheetsFor(clip);
            var sheet = c?.Sheet;
            if (sheet == null || sheet.Length == 0) return null;
            int facings = ClipFacingCount(clip);
            int frames = ClipFrameCount(clip);
            int row = ((facingRow % facings) + facings) % facings;
            int col = ((frame % frames) + frames) % frames;
            int idx = row * frames + col;
            return (idx >= 0 && idx < sheet.Length) ? sheet[idx] : null;
        }
    }
}
