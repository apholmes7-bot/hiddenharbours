using UnityEngine;

namespace HiddenHarbours.Fishing
{
    /// <summary>The arc of a rod fight (Rod Fishing v2, brainstorm §3). The fight starts <b>Deep</b> and
    /// crosses to <b>Surface</b> as it is won.</summary>
    public enum RodFightPhase
    {
        /// <summary>She's down deep and <b>unseen</b> — you fight her through the tackle: the rod's load, the
        /// sound of the line, and the entry point sliding on the water are how you read which way she's going.
        /// The full fight is live here (lean against her while you reel); the only thing you're missing is the
        /// sight of her.</summary>
        Deep = 0,

        /// <summary>She's risen and is <b>visible</b>, darting across the water at the end of your line. Same
        /// fight, but now you can see the answer instead of feeling for it.</summary>
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
    /// <para><b>The fight is: OPPOSE HER WHILE YOU REEL</b> (owner's ruling, 2026-07-23 — replacing the
    /// earlier "deep half is pure timing" arc, and replacing the two HUD bars, which are gone; the rod, the
    /// line, the sound and the camera are the only instruments now).
    /// <list type="number">
    ///   <item><b>The lean is live from the hookup.</b> Her run has a DIRECTION at every moment of the fight
    ///   (<c>steerAlignment</c>: −1 = leaning fully against her, +1 = going with her). Leaning against her
    ///   bleeds tension and buys line; going with her loads the rod and buys nothing. It scales by
    ///   <c>fishEffort01</c> — you can only lean on a fish that's actually pulling. There is no phase where
    ///   this sits out: deep, you read her through the rod and the line's entry point instead of seeing her.</item>
    ///   <item><b>Only the reel lands her.</b> The lean never lands a fish by itself; it decides what the reel
    ///   is worth (<see cref="LandingRatePerSec"/>). Reel her slack and line comes free; reel against a run
    ///   and you're paid for the part of it you're leaning against; reel WITH her and you get strain, nothing
    ///   else. And because tension charges the whole time you're on the reel (invariant 1), the reel can never
    ///   simply be held down — the fight is a rhythm of taking line and easing off.</item>
    ///   <item><b>Deep→Surface arc.</b> <see cref="PhaseFor"/> crosses from <see cref="RodFightPhase.Deep"/> to
    ///   <see cref="RodFightPhase.Surface"/> once <c>Landing01</c> climbs past <c>surfaceThreshold01</c>. The
    ///   phase is now about what you can SEE — unseen and read through the tackle, then up and visible — not
    ///   about which mechanics are switched on. Landing never falls, so the crossing is one-way (no
    ///   hysteresis).</item>
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
    /// <para><b>Built on the stable dock first (owner's locked call) — and the deck term now rides the
    /// reserved seam (Wave 4).</b> The rates are a SUM of independent terms; the position/angle term of the
    /// weathervaning deck (brainstorm §4.2, the "light real factor") arrives as the additive
    /// <c>deckAnglePressurePerSec</c> parameter — computed by <see cref="DeckAngleMath"/>, exactly 0 off a
    /// deck (or with the owner's <c>DeckAngleFactor</c> at 0), so the dock model is bit-for-bit unchanged.</para>
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
        ///   <item><b>Lean term (both phases)</b> — <c>counterSteerRelief · steerAlignment · fishEffort01</c>:
        ///   leaning OPPOSITE her run (−) bleeds tension (you're taking her weight on the rod), going WITH her
        ///   (+) climbs it, and it only bites while she's actually pulling.</item>
        /// </list>
        /// Returns an unclamped signed rate (the caller clamps the accumulator, mirroring
        /// <see cref="TrapHaulMath.HoldLineRate"/>). Pure, deterministic, NaN-safe.
        /// </summary>
        /// <param name="reeling">PULL (true = reel) vs MAINTAIN (false = hold steady, keep her honest).</param>
        /// <param name="fishEffort01">How hard she runs THIS tick, 0 (slack window) .. 1 (a hard run).</param>
        /// <param name="steerAlignment">Rod vs her run: +1 = WITH her (bad), −1 = OPPOSITE (good), 0 = neutral.
        /// Live in both phases.</param>
        /// <param name="phase">Deep (unseen) or Surface (visible) — no longer gates the lean.</param>
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
            => TensionRatePerSec(reeling, fishEffort01, steerAlignment, phase, tensionRisePerSec,
                                 tensionFallPerSec, runTensionPressure, counterSteerRelief,
                                 deckAnglePressurePerSec: 0f);

        /// <summary>
        /// The full tension rate including the <b>deck-angle term</b> (Rod Fishing v2 Wave 4 — the seam
        /// the dock-first model reserved, now filled): <paramref name="deckAnglePressurePerSec"/> is the
        /// slow extra pressure of a line running ACROSS the hull from a bad deck stance
        /// (<see cref="DeckAngleMath.TensionPerSec"/> — already factor × stance, so it arrives here as a
        /// plain ≥ 0 rate). It is ADDITIVE and one-sided: it only ever loads the line (walking the rail
        /// relieves it back to 0 — never a bonus), it never touches landing, and at 0 this overload is
        /// bit-for-bit the dock model above (the dock-parity contract, test-pinned). Off a deck the
        /// caller passes 0 by construction (no published stance). Pure, deterministic, NaN-safe.
        /// </summary>
        /// <param name="deckAnglePressurePerSec">Extra tension per second from the deck stance (≥ 0;
        /// negative/NaN reads 0). Keep the owner's factor below <c>tensionFallPerSec −
        /// runTensionPressure</c> so a MAINTAIN still bleeds at the worst stance mid-run
        /// (<see cref="MaintainOutbleedsTheRunAtTheWorstStance"/>).</param>
        public static float TensionRatePerSec(bool reeling, float fishEffort01, float steerAlignment,
            RodFightPhase phase, float tensionRisePerSec, float tensionFallPerSec,
            float runTensionPressure, float counterSteerRelief, float deckAnglePressurePerSec)
        {
            float effort = Mathf.Clamp01(Safe(fishEffort01));

            // Timing: the reel loads the line, the ease bleeds it (the base pull/maintain axis).
            float timing = reeling ? Mathf.Max(0f, Safe(tensionRisePerSec))
                                    : -Mathf.Max(0f, Safe(tensionFallPerSec));

            // The fish's run loads the line whether you pull or ease (she's fighting too). Kept below the ease
            // by MaintainOutbleedsTheRun so a MAINTAIN still nets downward at a full run.
            float run = Mathf.Max(0f, Safe(runTensionPressure)) * effort;

            // The lean, live from the hookup (owner's call 2026-07-23 — "lean against her, ALWAYS"):
            // leaning OPPOSITE her run bleeds tension (−), leaning WITH her climbs it (+), scaled by how
            // hard she's pulling. Deep no longer sits this out — you can't SEE her down there, but you can
            // feel which way she's going through the rod, and the line's entry point shows it.
            float steer = Mathf.Max(0f, Safe(counterSteerRelief))
                        * Mathf.Clamp(Safe(steerAlignment), -1f, 1f) * effort;

            // The deck-angle term (brainstorm §4.2, the owner's "light real factor" — the seam this sum
            // reserved): a line across the hull adds slow pressure; a clean stance adds exactly nothing.
            float deck = Mathf.Max(0f, Safe(deckAnglePressurePerSec));

            return timing + run + steer + deck;
        }

        /// <summary>
        /// Signed <b>landing</b> gain per second (progress to aboard at 1). The caller does
        /// <c>Landing01 = clamp01(Landing01 + rate · dt)</c>; a full gauge lands the fish. Non-negative in this
        /// model (landing never slips — the crossing to Surface is one-way).
        ///
        /// <para><b>You land her by REELING WHILE YOU LEAN ON HER</b> (owner's call 2026-07-23). Nothing but
        /// the reel wins line — a lean on its own, however perfect, never lands a fish; it only takes the
        /// strain off (that lives in <see cref="TensionRatePerSec"/>). What the lean decides is how much the
        /// reel is WORTH:</para>
        /// <list type="bullet">
        ///   <item><b>Reeling her slack</b> pays <c>landingFillPerSec · (1 − fishEffort01)</c> — when she
        ///   gives, line comes for free, whichever way you lean.</item>
        ///   <item><b>Reeling against her run</b> pays for the part of her effort you are LEANING AGAINST:
        ///   <c>landingFillPerSec · max(0, −steerAlignment) · fishEffort01</c>. Lean fully into a full run and
        ///   the reel pays exactly what free slack pays — that's the gutsy line, and the tension side is
        ///   charging the whole time you take it.</item>
        ///   <item><b>Reeling WITH her</b> (steering into the run) pays nothing at all during a run. You are
        ///   just feeding her line and loading the rod.</item>
        /// </list>
        /// The sum is clamped to <c>landingFillPerSec</c>, so no combination out-earns a clean slack reel and
        /// invariant 1 (snap before land) holds at every steer. Live in BOTH phases — the deep half is no
        /// longer a steer-free waiting game. Pure, deterministic, NaN-safe.
        /// </summary>
        /// <param name="reeling">PULL (true) vs MAINTAIN (false). False ⇒ no landing, ever.</param>
        /// <param name="fishEffort01">How hard she runs THIS tick, 0 (slack) .. 1 (hard run).</param>
        /// <param name="steerAlignment">Rod vs her run, −1..+1 (only the OPPOSITE side, −, buys line).</param>
        /// <param name="phase">Retained for the caller's symmetry with <see cref="TensionRatePerSec"/>; the
        /// phase no longer gates the lean (it is live from the hookup).</param>
        /// <param name="landingFillPerSec">Landing gained per second by a clean PULL in a full slack (≥ 0). Must
        /// stay below <c>tensionRisePerSec</c> (invariant 1).</param>
        public static float LandingRatePerSec(bool reeling, float fishEffort01, float steerAlignment,
            RodFightPhase phase, float landingFillPerSec)
        {
            if (!reeling) return 0f;   // line only comes in on the REEL — the lean alone never lands her

            float effort = Mathf.Clamp01(Safe(fishEffort01));
            float fill = Mathf.Max(0f, Safe(landingFillPerSec));
            float counter = Mathf.Clamp01(-Mathf.Clamp(Safe(steerAlignment), -1f, 1f)); // only the OPPOSITE side

            // Reeling the slack wins line for free; reeling into her run wins line only for the part of
            // her effort you are LEANING AGAINST. Lean fully against a full run and the reel pays exactly
            // as well as free slack does — the gutsy line — but the tension side is charging the whole
            // time (invariant 1), so it can't be held. Reel WITH her (counter = 0) and a full run gives
            // nothing but strain.
            return fill * Mathf.Clamp01((1f - effort) + counter * effort);
        }

        // ---- the diegetic reads (no HUD bars — the rod & line are the instrument) ----------------
        //
        // Pure, tuning-free reads of the CURRENT tick so art/audio/UI render the fight without touching its
        // logic (the twins of TrapHaulMath.SwellRopeLoad01 / FightStrain01). A read never feeds the rates back.

        /// <summary>
        /// How <b>bent the rod / taut the line</b> reads right now, 0 (slack, straight) → 1 (bar-taut) — the
        /// always-on diegetic instrument (shown whether or not the player acts, so a run can be read coming).
        /// With the fight's bars gone this read IS the gauge, so it answers to the lean from the hookup: the
        /// rod carries her run (<c>fishEffort01</c>), and leaning WITH her tightens the arc further
        /// (the counter-lean's easing shows in <see cref="LineStrain01"/>, not here — the rod is still loaded
        /// by her weight). Pure, NaN-safe.
        /// </summary>
        public static float RodBend01(float fishEffort01, float steerAlignment, RodFightPhase phase)
        {
            float effort = Mathf.Clamp01(Safe(fishEffort01));
            float into = Mathf.Clamp01(Mathf.Clamp(Safe(steerAlignment), -1f, 1f)); // only the INTO side tightens
            return Mathf.Clamp01(effort + into * effort);
        }

        /// <summary>
        /// The active <b>over-strain</b> read, 0..1 — high ONLY when the player's current action is loading the
        /// line toward a snap: PULLING into her run, or leaning WITH her. Zero when doing it right —
        /// MAINTAINING through a run, or PULLING in the slack. This is the "ease off, you'll pull the hook"
        /// tell that whitens/shudders the line and voices the strain groan, distinct from the always-on
        /// <see cref="RodBend01"/>. A counter-lean bleeds it back down — in both phases now, so the deep half
        /// of the fight answers to the lean the same way the surfaced half does. Pure, NaN-safe.
        /// </summary>
        public static float LineStrain01(bool reeling, float fishEffort01, float steerAlignment, RodFightPhase phase)
        {
            float effort = Mathf.Clamp01(Safe(fishEffort01));
            float strain = reeling ? effort : 0f;                             // pulling into her run over-strains
            strain += Mathf.Clamp(Safe(steerAlignment), -1f, 1f) * effort;    // into (+) adds, counter (−) bleeds
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

        /// <summary>
        /// Invariant 2, extended onto the deck (Rod Fishing v2 Wave 4) — MAINTAIN still nets tension
        /// DOWNWARD at the WORST deck stance mid-run. True iff <paramref name="runTensionPressure"/> +
        /// <paramref name="deckAngleFactor"/> (the deck term's ceiling — the pressure at a line fully
        /// across the hull) stays below <paramref name="tensionFallPerSec"/>: even standing at the wrong
        /// rail through her hardest run, backing off recovers — the bad angle is a slow "walk the rail"
        /// nudge, never an unavoidable snap (the owner's cozy rule; the tuning guard the shipped
        /// <c>GameConfig.RodFight.DeckAngleFactor</c> default is test-pinned against).
        /// </summary>
        public static bool MaintainOutbleedsTheRunAtTheWorstStance(
            float runTensionPressure, float deckAngleFactor, float tensionFallPerSec)
            => Safe(runTensionPressure) + Mathf.Max(0f, Safe(deckAngleFactor)) < Safe(tensionFallPerSec);

        // ---- guards -----------------------------------------------------------------------------

        /// <summary>NaN → 0 (the safe, neutral value). Unity's <c>Mathf.Clamp</c> passes NaN through, so inputs
        /// are sanitized here first, mirroring <see cref="TrapHaulMath"/>'s explicit NaN guards (rule 5).</summary>
        private static float Safe(float x) => float.IsNaN(x) ? 0f : x;
    }
}
