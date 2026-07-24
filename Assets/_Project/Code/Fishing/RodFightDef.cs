using UnityEngine;

namespace HiddenHarbours.Fishing
{
    /// <summary>How a fish moves near the surface — what the mouse-steer reads against (design §3, §5).</summary>
    public enum RodFightMovement
    {
        Darter = 0, // short, sharp direction changes — hard to counter-steer
        Bulldog,    // digs deep and won't rise; dogged, few big runs
        Circler,    // long, sweeping circular runs
        Thrasher,   // side-to-side head-thrashing at the surface
    }

    /// <summary>What tells the angler a bite is on — tied to the tackle (design §2.1, §5).</summary>
    public enum RodBiteTell
    {
        BobberDip = 0, // a surface float dips — the cast-fishing VISUAL tell
        DeepKnock,     // a rod-tip knock / line twitch — the depth-fishing FEEL tell (no bobber)
    }

    /// <summary>
    /// The run↔slack rhythm of a fight (design §5): how long the fish RUNS (she fights, you MAINTAIN) versus
    /// how long she goes SLACK (she tires, you PULL to gain). "FightsHard" = long runs, short slacks. Data,
    /// not code — the parallel RodFightMath reads these to drive the fight's oscillation; nothing is
    /// hard-coded (rule 6).
    /// </summary>
    [System.Serializable]
    public struct StaminaCadence
    {
        [Min(0.05f)]
        [Tooltip("Average length (s) of a RUN — she's fighting, MAINTAIN and steer; reeling INTO it climbs " +
                 "tension toward a snap. Longer = fights harder.")]
        public float RunSeconds;

        [Min(0.05f)]
        [Tooltip("Average length (s) of the SLACK window between runs — she's tiring; PULL now to gain " +
                 "Landing01 safely. Shorter = fights harder (fewer chances to reel).")]
        public float SlackSeconds;

        [Range(0f, 1f)]
        [Tooltip("How much to jitter each run/slack length (0 = metronomic, 1 = ±100%) so the rhythm never " +
                 "feels mechanical. Deterministic under a seeded fight (tests can pin it).")]
        public float Jitter01;
    }

    /// <summary>
    /// A fish species' Rod Fishing v2 fight "personality", as data (ADR 0003) — the Stardew-style character
    /// of the fight made literal (design/rod-fishing-v2-brainstorm.md §5). One asset = one fight profile.
    /// Create via Assets ▸ Create ▸ Hidden Harbours ▸ Rod Fight, save in Data/RodFights.
    ///
    /// <para><b>Opt-in, same shape as TrapDef→DeckWorkDef.</b> A species references one from
    /// <see cref="FishSpeciesDef.RodFight"/>; leave that EMPTY and the species uses the simple/legacy
    /// tension fight (<see cref="FishFight"/> / <see cref="HiddenHarbours.Core.FishingPhase.Fighting"/>),
    /// exactly as today. A RodFightDef opts the species into the v2 deep→surface arc
    /// (<see cref="HiddenHarbours.Core.FishingPhase.FightDeep"/> →
    /// <see cref="HiddenHarbours.Core.FishingPhase.FightSurface"/>). Content authors tune feel per species
    /// with no code change; the per-species roster is authored later (Wave 3) — this contract ships ONE
    /// template asset only.</para>
    ///
    /// <para><b>These fields are the shared contract the parallel <c>RodFightMath</c> consumes.</b> That
    /// pure, deterministic, EditMode-testable static class (the v2 twin of <see cref="TrapHaulMath"/> /
    /// <see cref="FishFightTuning"/>, NOT in this PR) takes sea state + this Def in and returns signed
    /// tension/landing rates out — no RNG saved, nothing in the world-sim determinism contract. To keep
    /// that vocabulary stable across agents, the fight-tuning floats use the EXACT names agreed for Wave 1
    /// (<c>tensionRisePerSec</c>, …, <c>surfaceThreshold01</c>) — hence camelCase public fields here, a
    /// deliberate exception to the repo's usual PascalCase-public convention.</para>
    ///
    /// <para><b>Forgiving-cove invariants</b> RodFightMath relies on (keep them true when authoring):
    /// <c>tensionRisePerSec &gt; landingFillPerSec</c> (you can never just pin to win) and
    /// <c>runTensionPressure &lt; tensionFallPerSec</c> (easing always nets tension down, even mid-run —
    /// a run is a "back off" tell, never an unavoidable snap; P5 cozy-with-teeth).</para>
    /// </summary>
    [CreateAssetMenu(menuName = "Hidden Harbours/Rod Fight", fileName = "RodFight")]
    public class RodFightDef : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable, append-only id (e.g. \"rodfight.striped_bass\"). Never reuse or rename (rule 2).")]
        public string Id = "rodfight.template";
        public string DisplayName = "Rod Fight (template)";

        [Header("Personality (the character of the fight — Stardew-style, §5)")]
        [Range(0f, 1f)]
        [Tooltip("How hard she pulls / how fast tension climbs on a run — 0 = a gentle schoolie, 1 = a " +
                 "barn-door halibut. RodFightMath scales the run pressure by this personality dial.")]
        public float Strength = 0.5f;

        [Tooltip("How she moves near the surface — what the mouse-steer counters (§3).")]
        public RodFightMovement MovementPattern = RodFightMovement.Darter;

        [Tooltip("The run↔slack rhythm (§5): how long she runs vs. rests. FightsHard = long runs, short slacks.")]
        public StaminaCadence StaminaCadence = new StaminaCadence { RunSeconds = 2.0f, SlackSeconds = 1.4f, Jitter01 = 0.3f };

        [Tooltip("Which bite tell this fish gives, tied to the tackle — a bobber dip (cast) or a deep " +
                 "rod-tip knock (depth/floor), §2.1.")]
        public RodBiteTell Tell = RodBiteTell.BobberDip;

        [Header("Fight tuning — feeds the pure RodFightMath (0..1 space, per second; rule 6, no magic numbers)")]
        [Min(0f)]
        [Tooltip("Tension gained per second while REELING INTO a run (the snap danger). Analogue of " +
                 "FishFightTuning.TensionRisePerSec. Keep > landingFillPerSec so you can never just pin to win.")]
        public float tensionRisePerSec = 0.75f;

        [Min(0f)]
        [Tooltip("Tension bled per second while MAINTAINING / easing off. Analogue of TensionFallPerSec. " +
                 "Keep > runTensionPressure so easing always nets tension DOWN, even mid-run (cozy).")]
        public float tensionFallPerSec = 0.6f;

        [Min(0f)]
        [Tooltip("Landing01 gained per second while REELING her slack — and the ceiling on reeling against " +
                 "a run you are fully leaning into. Analogue of LandingFillPerSec.")]
        public float landingFillPerSec = 0.3f;

        [Min(0f)]
        [Tooltip("Extra tension per second she piles on DURING a run (the RDR2 'she's taking line' surge). " +
                 "Analogue of SurgePressure.")]
        public float runTensionPressure = 0.35f;

        [Min(0f)]
        [Tooltip("Tension relief per second earned by LEANING OPPOSITE her run — live from the hookup, " +
                 "deep or surfaced (the 'pull against the run' counter). This is how leaning on her lets " +
                 "you keep reeling without parting the line; going WITH her costs the same magnitude.")]
        public float counterSteerRelief = 0.4f;

        [Range(0f, 1f)]
        [Tooltip("Landing01 at which the fight rises from the deep phase to the surface phase " +
                 "(FightDeep → FightSurface): below this she's unseen and you read her run through the rod " +
                 "and the line's entry point; at/above, she's up where you can see her. Same fight either " +
                 "side — this is how long she stays down (§3).")]
        public float surfaceThreshold01 = 0.5f;
    }
}
