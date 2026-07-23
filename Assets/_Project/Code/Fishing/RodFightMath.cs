using UnityEngine;

namespace HiddenHarbours.Fishing
{
    /// <summary>The arc of a rod fight (Rod Fishing v2, brainstorm §3). The fight starts <b>Deep</b> and
    /// crosses to <b>Surface</b> as it is won.</summary>
    public enum RodFightPhase
    {
        /// <summary>She's down deep and unseen — the line runs straight down. <b>Timing only</b>: read her
        /// runs through the rod-tip and PULL in the slack, MAINTAIN through a run. Steer is ignored (you
        /// can't see her to steer against). <see cref="RodFightMath"/> ignores <c>steerAlignment</c> here.</summary>
        Deep = 0,

        /// <summary>She's risen and is darting across the screen — the line's entry point moves. <b>Timing AND
        /// steer</b>: MAINTAIN + steer OPPOSITE her dart to tire her, PULL in the slack to land. Steer is
        /// live.</summary>
        Surface = 1,
    }

    /// <summary>
    /// The PURE, engine-light maths of the <b>rod fight</b> (Rod Fishing v2 — the deep→surface arc, brainstorm
    /// §3/§8). The v2 twin of <see cref="TrapHaulMath"/>: split out so the fight's SIGNED per-second rates and
    /// its diegetic rod/line reads are fully EditMode-testable with no scene, no clock, no <c>Time</c> — a pure
    /// function of the current inputs, no RNG, nothing saved, NaN-safe (rule 5). The caller integrates the
    /// rates each tick (<c>accum = clamp01(accum + rate · dt)</c>); this class never accumulates, never touches
    /// a <c>MonoBehaviour</c>, never reads the world seed/clock (the fight is real-time, not the deterministic
    /// world-sim). Tuning comes in as floats — every constant is a passed param or a <c>GameConfig</c> field, no
    /// magic numbers (rule 6). It takes floats, NOT a Def, so a species' <c>RodFightDef</c> (lead-architect's
    /// contract) or the <c>GameConfig.RodFight</c> cove defaults both feed it the same way.
    ///
    /// <para><b>Three owner ideas combine into one escalating fight (brainstorm §3).</b>
    /// <list type="number">
    ///   <item><b>Pull-on-slack / maintain-on-run.</b> The fish's run rhythm arrives as <c>fishEffort01</c>
    ///   (1 = a hard run, 0 = a slack window — generated elsewhere, the "personality"; this math just reads the
    ///   current value). <b>PULL (reel) during a run</b> drives <c>Tension01</c> toward the snap (you're hauling
    ///   against a running fish). <b>MAINTAIN during a run</b> bleeds tension — a run is a "back off" tell, never
    ///   an unavoidable snap. <b>PULL during a slack window</b> gains <c>Landing01</c> cheaply (she's not
    ///   fighting, so line comes without loading up).</item>
    ///   <item><b>Counter-steer (Surface only).</b> Steering the rod OPPOSITE her dart (<c>steerAlignment</c> →
    ///   −1) tires her: tension bleeds AND landing gains. Steering INTO her run (<c>steerAlignment</c> → +1)
    ///   climbs tension. The steer only bites while she's actually darting (it scales by <c>fishEffort01</c>) and
    ///   only in the Surface phase (Deep can't see her).</item>
    ///   <item><b>Deep→Surface arc.</b> <see cref="PhaseFor"/> crosses from <see cref="RodFightPhase.Deep"/> to
    ///   <see cref="RodFightPhase.Surface"/> once <c>Landing01</c> climbs past <c>surfaceThreshold01</c>. Deep is
    ///   pure timing; Surface adds the steer axis. Landing never falls in this model, so the crossing is one-way
    ///   (no hysteresis needed).</item>
    /// </list></para>
    ///
    /// <para><b>Forgiving-cove invariants (kept true across the whole tuning range — the v2 twin of
    /// <c>FishFightTuning</c>'s):</b>
    /// <list type="bullet">
    ///   <item><b>A sustained blind PULL SNAPS before it lands</b> — skill is a <i>pulse</i>, not a pin. Held to
    ///   the wall, tension outruns landing and parts the line. Guaranteed by
    ///   <see cref="PullAloneSnapsBeforeLanding"/> (<c>tensionRisePerSec &gt; landingFillPerSec</c>): the worst
    ///   case for the player is a fish that never runs (effort ≡ 0), and there a blind pull climbs tension at
    ///   <c>tensionRisePerSec</c> while landing fills at only <c>landingFillPerSec</c> — so it snaps first. Any
    ///   real run only makes the pull snap sooner (run pressure adds, slack-gated landing pauses).</item>
    ///   <item><b>MAINTAIN always nets tension DOWNWARD, even mid-run</b> — a run is a "back off" tell, never an
    ///   unavoidable snap. Guaranteed by <see cref="MaintainOutbleedsTheRun"/>
    ///   (<c>runTensionPressure &lt; tensionFallPerSec</c>): maintaining with neutral-or-counter steer nets
    ///   negative at any run intensity. (Steering INTO her while maintaining can still creep tension up — but
    ///   that's an <i>active</i> mistake the player chooses, and it's gentle, not a wall.)</item>
    /// </list></para>
    ///
    /// <para><b>Cozy — throw the hook, cost time, never gear (owner's call, brainstorm §7).</b> A lost fight
    /// "threw the hook": it costs the catch + bait time, never damage or lost gear. Real danger stays in the
    /// weather/tide/grounding lane. This class only models the <i>act</i> of the fight.</para>
    ///
    /// <para><b>Built on the stable dock first (owner's locked call).</b> No boat/deck motion is modelled yet.
    /// The rates are a SUM of independent terms, so a later position/angle term (the weathervaning deck,
    /// brainstorm §4) slots in additively — see the seam in <see cref="TensionRatePerSec"/> — without a rewrite.</para>
    /// </summary>
    public static class RodFightMath
    {
        // ---- the deep→surface crossing ----------------------------------------------------------

        /// <summary>
        /// Which phase the fight is in given how landed it is: <see cref="RodFightPhase.Surface"/> once
        /// <paramref name="landing01"/> reaches <paramref name="surfaceThreshold01"/>, else
        /// <see cref="RodFightPhase.Deep"/>. Landing never falls in this model, so the crossing is one-way. Pure,
        /// NaN-safe (a NaN landing reads as Deep — the safe, steer-ignoring default).
        /// </summary>
        /// <param name="landing01">How landed the fish is, 0..1 (the caller's accumulator).</param>
        /// <param name="surfaceThreshold01">Landing fraction at which she breaks the surface and steer goes live (0..1).</param>
        public static RodFightPhase PhaseFor(float landing01, float surfaceThreshold01)
        {
            float landing = Mathf.Clamp01(Safe(landing01));
            float threshold = Mathf.Clamp01(Safe(surfaceThreshold01));
            return landing >= threshold ? RodFightPhase.Surface : RodFightPhase.Deep;
        }

        // ---- the two signed per-second rates (the heart — caller integrates + clamps to [0,1]) --

        /// <summary>
        /// Signed <b>tension</b> change per second (the line's strain toward the snap at 1). The caller does
        /// <c>Tension01 = clamp01(Tension01 + rate · dt)</c>; a maxed line snaps. Built as a SUM of independent
        /// terms so the model stays legible and extensible:
        /// <list type="bullet">
        ///   <item><b>Timing term</b> — PULL raises at <paramref name="tensionRisePerSec"/>; MAINTAIN lowers at
        ///   <paramref name="tensionFallPerSec"/> (the base "reel loads / ease bleeds").</item>
        ///   <item><b>Run term</b> — the fish's run loads the line at
        ///   <c>runTensionPressure · fishEffort01</c>, whether you pull or maintain (she's pulling too). The
        ///   <see cref="MaintainOutbleedsTheRun"/> invariant keeps this below the ease, so MAINTAIN still nets
        ///   downward at a full run.</item>
        ///   <item><b>Steer term (Surface only)</b> — <c>counterSteerRelief · steerAlignment · fishEffort01</c>:
        ///   steering OPPOSITE her dart (−) bleeds tension (tires her), steering INTO her (+) climbs it, and it
        ///   only bites while she's darting. Deep ignores <paramref name="steerAlignment"/> entirely.</item>
        /// </list>
        /// Returns an unclamped signed rate (the caller clamps the accumulator, mirroring
        /// <see cref="TrapHaulMath.HoldLineRate"/>). Pure, deterministic, NaN-safe.
        /// </summary>
        /// <param name="reeling">PULL (true = reel) vs MAINTAIN (false = hold steady, keep her honest).</param>
        /// <param name="fishEffort01">How hard she runs THIS tick, 0 (slack window) .. 1 (a hard run).</param>
        /// <param name="steerAlignment">Rod vs her dart: +1 = WITH her (bad), −1 = OPPOSITE (good), 0 = neutral.
        /// Meaningful only in <see cref="RodFightPhase.Surface"/>.</param>
        /// <param name="phase">Deep (steer ignored) or Surface (steer live).</param>
        /// <param name="tensionRisePerSec">Tension gained per second while PULLING (≥ 0). Must exceed
        /// <paramref name="landingFillPerSec"/> so a blind hold snaps (invariant 1).</param>
        /// <param name="tensionFallPerSec">Tension bled per second while MAINTAINING (≥ 0). Must exceed
        /// <c>runTensionPressure</c> so a maintain always recovers (invariant 2).</param>
        /// <param name="runTensionPressure">Extra tension per second at a full run (fishEffort01 = 1), applied
        /// pull or maintain (≥ 0).</param>
        /// <param name="counterSteerRelief">Tension bled per second by a full counter-steer against a full dart
        /// (≥ 0); the same magnitude is the penalty for steering INTO her.</param>
        public static float TensionRatePerSec(bool reeling, float fishEffort01, float steerAlignment,
            RodFightPhase phase, float tensionRisePerSec, float tensionFallPerSec,
            float runTensionPressure, float counterSteerRelief)
        {
            float effort = Mathf.Clamp01(Safe(fishEffort01));

            // Timing: the reel loads the line, the ease bleeds it (the base pull/maintain axis).
            float timing = reeling ? Mathf.Max(0f, Safe(tensionRisePerSec))
                                    : -Mathf.Max(0f, Safe(tensionFallPerSec));

            // The fish's run loads the line whether you pull or ease (she's fighting too). Kept below the ease
            // by MaintainOutbleedsTheRun so a MAINTAIN still nets downward at a full run.
            float run = Mathf.Max(0f, Safe(runTensionPressure)) * effort;

            // Surface only: counter-steer against her dart bleeds tension (−), steering into her climbs it (+),
            // scaled by how hard she's darting. Deep can't see her, so no steer.
            float steer = 0f;
            if (phase == RodFightPhase.Surface)
                steer = Mathf.Max(0f, Safe(counterSteerRelief)) * Mathf.Clamp(Safe(steerAlignment), -1f, 1f) * effort;

            // Seam for a later, dock-motion-free extension (brainstorm §4 — the weathervaning deck adds a
            // position/angle term here; it stays additive, so no rewrite):
            //   + BoatMotionTensionPerSec(...);
            return timing + run + steer;
        }

        /// <summary>
        /// Signed <b>landing</b> gain per second (progress to aboard at 1). The caller does
        /// <c>Landing01 = clamp01(Landing01 + rate · dt)</c>; a full gauge lands the fish. Non-negative in this
        /// model (landing never slips — the crossing to Surface is one-way). Two ways line is won:
        /// <list type="bullet">
        ///   <item><b>PULL in the slack window</b> — reeling gains <c>landingFillPerSec · (1 − fishEffort01)</c>:
        ///   full when she's slack, nothing when she's running hard (reel into a run and you get tension, not
        ///   line). This is the timing gate, live in both phases.</item>
        ///   <item><b>Counter-steer tires her (Surface only)</b> — steering OPPOSITE her dart adds
        ///   <c>landingFillPerSec · max(0, −steerAlignment) · fishEffort01</c>, so a good counter-steer during a
        ///   run makes progress even while MAINTAINING (the RDR2 "pull against the run"). Deep ignores steer.</item>
        /// </list>
        /// A blind PULL with neutral steer gets only the slack-gated term — so invariant 1 (snap before land)
        /// holds; the counter-steer bonus is an <i>earned</i> Surface skill, never part of a blind hold. Pure,
        /// deterministic, NaN-safe.
        /// </summary>
        /// <param name="reeling">PULL (true) vs MAINTAIN (false).</param>
        /// <param name="fishEffort01">How hard she runs THIS tick, 0 (slack) .. 1 (hard run).</param>
        /// <param name="steerAlignment">Rod vs her dart, −1..+1 (only the OPPOSITE side, −, tires her; Surface only).</param>
        /// <param name="phase">Deep (steer ignored) or Surface (counter-steer tires).</param>
        /// <param name="landingFillPerSec">Landing gained per second by a clean PULL in a full slack (≥ 0). Must
        /// stay below <c>tensionRisePerSec</c> (invariant 1).</param>
        public static float LandingRatePerSec(bool reeling, float fishEffort01, float steerAlignment,
            RodFightPhase phase, float landingFillPerSec)
        {
            float effort = Mathf.Clamp01(Safe(fishEffort01));
            float fill = Mathf.Max(0f, Safe(landingFillPerSec));

            // PULL in the slack window wins line cheaply; reeling into a run wins ~nothing (that's tension).
            float slackGain = reeling ? fill * (1f - effort) : 0f;

            // Surface only: a counter-steer against her dart tires her — landing creeps even on a MAINTAIN.
            float tireGain = 0f;
            if (phase == RodFightPhase.Surface)
            {
                float counter = Mathf.Clamp01(-Mathf.Clamp(Safe(steerAlignment), -1f, 1f)); // only the OPPOSITE side
                tireGain = fill * counter * effort;
            }

            return slackGain + tireGain;
        }

        // ---- the diegetic reads (no HUD bars — the rod & line are the instrument) ----------------
        //
        // Pure, tuning-free reads of the CURRENT tick so art/audio/UI render the fight without touching its
        // logic (the twins of TrapHaulMath.SwellRopeLoad01 / FightStrain01). A read never feeds the rates back.

        /// <summary>
        /// How <b>bent the rod / taut the line</b> reads right now, 0 (slack, straight) → 1 (bar-taut) — the
        /// always-on diegetic instrument (shown whether or not the player acts, so a run can be read coming).
        /// The rod carries her run (<c>fishEffort01</c>); in the Surface phase, steering INTO her dart tightens
        /// the arc further (counter-steer's easing shows in <see cref="LineStrain01"/>, not here — the rod is
        /// still loaded by her weight). Pure, NaN-safe.
        /// </summary>
        public static float RodBend01(float fishEffort01, float steerAlignment, RodFightPhase phase)
        {
            float effort = Mathf.Clamp01(Safe(fishEffort01));
            float into = (phase == RodFightPhase.Surface)
                ? Mathf.Clamp01(Mathf.Clamp(Safe(steerAlignment), -1f, 1f)) // only the INTO side tightens
                : 0f;
            return Mathf.Clamp01(effort + into * effort);
        }

        /// <summary>
        /// The active <b>over-strain</b> read, 0..1 — high ONLY when the player's current action is loading the
        /// line toward a snap: PULLING into her run, or (Surface) steering INTO her dart. Zero when doing it
        /// right — MAINTAINING through a run, or PULLING in the slack. This is the "ease off, you'll pull the
        /// hook" tell that whitens/shudders the line and voices the strain groan, distinct from the always-on
        /// <see cref="RodBend01"/>. A Surface counter-steer bleeds it back down. Pure, NaN-safe.
        /// </summary>
        public static float LineStrain01(bool reeling, float fishEffort01, float steerAlignment, RodFightPhase phase)
        {
            float effort = Mathf.Clamp01(Safe(fishEffort01));
            float strain = reeling ? effort : 0f;                         // pulling into her run over-strains
            if (phase == RodFightPhase.Surface)
                strain += Mathf.Clamp(Safe(steerAlignment), -1f, 1f) * effort; // into (+) adds, counter (−) bleeds
            return Mathf.Clamp01(strain);
        }

        /// <summary>
        /// How <b>open the slack window is</b> right now, 0 (a hard run — MAINTAIN) → 1 (dead slack — PULL to
        /// land): simply <c>1 − fishEffort01</c>. The diegetic cue for the timing game (the line easing, the
        /// rod-tip settling). Pure, NaN-safe.
        /// </summary>
        public static float SlackWindowOpen01(float fishEffort01)
            => 1f - Mathf.Clamp01(Safe(fishEffort01));

        // ---- the forgiving-cove invariants, as checkable relationships --------------------------
        //
        // Exposed so a caller / content validation / tests can assert any tuning set (a GameConfig default or a
        // species RodFightDef) keeps the cove forgiving, and so the guarantees are named where they're relied on.

        /// <summary>
        /// Invariant 1 — a sustained blind PULL SNAPS before it lands (skill is a pulse, not a pin). True iff
        /// <paramref name="tensionRisePerSec"/> &gt; <paramref name="landingFillPerSec"/>: held to the wall,
        /// tension (rising at the reel rate) reaches 1 before landing (filling at the slack rate) does. The
        /// binding case is a fish that never runs; any real run only snaps the blind pull sooner.
        /// </summary>
        public static bool PullAloneSnapsBeforeLanding(float tensionRisePerSec, float landingFillPerSec)
            => Safe(tensionRisePerSec) > Safe(landingFillPerSec);

        /// <summary>
        /// Invariant 2 — MAINTAIN always nets tension DOWNWARD, even mid-run (a run is a "back off" tell, never
        /// an unavoidable snap). True iff <paramref name="runTensionPressure"/> &lt;
        /// <paramref name="tensionFallPerSec"/>: with neutral-or-counter steer, the ease outbleeds the fish's
        /// run at any intensity. (Steering INTO her while maintaining is an active, gentle, avoidable exception.)
        /// </summary>
        public static bool MaintainOutbleedsTheRun(float runTensionPressure, float tensionFallPerSec)
            => Safe(runTensionPressure) < Safe(tensionFallPerSec);

        // ---- guards -----------------------------------------------------------------------------

        /// <summary>NaN → 0 (the safe, neutral value). Unity's <c>Mathf.Clamp</c> passes NaN through, so inputs
        /// are sanitized here first, mirroring <see cref="TrapHaulMath"/>'s explicit NaN guards (rule 5).</summary>
        private static float Safe(float x) => float.IsNaN(x) ? 0f : x;
    }
}
