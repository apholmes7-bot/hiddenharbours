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
    /// <c>&lt;Stem&gt;_d&lt;dir&gt;_f&lt;frame&gt;</c> sub-sprite names say. Cell 64×88 at PPU 32 with the
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
                 "−45°·i, not +45°·i. The 3D rigs that bake the iso character kits rotate the model CCW but " +
                 "LABEL the rows clockwise (N, NE, E...), so their sheets are mirrored: the row called 'E' is " +
                 "really a fisher facing West. This flag is the un-mirror, and it lives HERE — per artwork — " +
                 "for the same reason BoatVisualDef.FacingsAreCounterClockwise does: two art lineages can " +
                 "genuinely disagree, and a blanket fix in code would repair one and break the other. " +
                 "⚠️ IF THE ART DIRECTOR RE-BAKES THE RIG THE RIGHT WAY ROUND, THIS FLAG MUST FLIP TO FALSE — " +
                 "doing both re-mirrors it. Default false so no future asset silently flips.")]
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
        {
            var sheet = SheetFor(gait);
            if (sheet.Length == 0) return null;
            int facings = Mathf.Max(1, FacingCount);
            int frames = FrameCountFor(gait);
            int row = ((facingRow % facings) + facings) % facings;
            int col = ((frame % frames) + frames) % frames;
            int idx = row * frames + col;
            return (idx >= 0 && idx < sheet.Length) ? sheet[idx] : null;
        }
    }
}
