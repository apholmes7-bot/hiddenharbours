using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Fishing
{
    /// <summary>
    /// THE SEA'S HAND IN THE CATCH (owner's ruling 2026-07-25) — the pure maths that finally makes
    /// Pillar 1 true of fishing. Until now the rod loop read no wind, no swell and no weather: a fight
    /// in a flat calm played bit-for-bit identically to one in a gale, which is precisely the
    /// "weather as pure cosmetics" anti-pattern the canon names against P1.
    ///
    /// <para><b>The owner's design: rough water is a TRADE, not a tax.</b> Broken water emboldens fish —
    /// they lose their caution under a chopped surface and the better ones come up to feed. So a blow
    /// <b>fishes better</b>: bites come quicker (<see cref="BiteDelayScale"/>) and run bigger
    /// (<see cref="WeightBias01"/>). It also <b>fights harder</b> (<see cref="TensionPerSec"/>): the swell
    /// works the rod, snatches at the line and loads it whether or not the angler does anything right.
    /// Weather becomes a decision — "is this sea worth fishing?" — instead of a reason to stay in.</para>
    ///
    /// <para><b>Teeth (the owner's explicit call).</b> A real blow may genuinely BEAT you: the forgiving
    /// invariants that guarantee a competent hand always lands the fish are guaranteed only up to
    /// <see cref="CozySeaCeiling01"/>. Above that the sea can take a fish you would have landed in calm
    /// water. That is deliberate — see the guard-rails at the foot of this class, which pin exactly where
    /// the cozy promise ends so the teeth can never creep down into the everyday weather by accident.</para>
    ///
    /// <para><b>Flat calm is bit-for-bit unchanged.</b> Every function here returns its neutral value at
    /// <c>seaState01 = 0</c> (or with its factor at 0), so a glassy day plays EXACTLY as the shipped
    /// fight does — the same discipline <see cref="DeckAngleMath"/> established for the deck term, and
    /// the reason the daily loop stays dependable.</para>
    ///
    /// <para>Pure, deterministic, NaN-safe, engine-light (rule 5): sea state in, floats out — no
    /// <c>Time</c>, no RNG, no scene, nothing saved. The caller samples the live
    /// <see cref="EnvironmentSample.SeaState01"/> and passes it in, exactly as the deck term arrives.</para>
    /// </summary>
    public static class SeaFightMath
    {
        /// <summary>
        /// The sea state up to which the fight keeps its <b>cozy guarantee</b> — the everyday weather in
        /// which a competent hand still always lands the fish. Above this the sea has teeth and a big fish
        /// in a big sea can genuinely be lost (the owner's call, 2026-07-25).
        ///
        /// <para>0.5 of the 0..1 axis is roughly the middle of the canon's stepped scale — so a moderate
        /// day is still dependable fishing and it is only a real blow that can beat you. The content sweep
        /// asserts every shipped tuning stays forgiving at and below this line, which is what stops the
        /// teeth quietly creeping into ordinary weather.</para>
        /// </summary>
        public const float CozySeaCeiling01 = 0.5f;

        // ---- the sea fights you (the cost side of the trade) --------------------------------------

        /// <summary>
        /// The extra tension per second (0..1-gauge units) the SEA itself puts through the line — the
        /// swell working the rod tip, the boat lifting under you, a wave snatching the belly of the line.
        /// Additive and one-sided in <see cref="RodFightMath.TensionRatePerSec"/>, exactly like the deck
        /// term: it only ever loads the line, it never touches landing, and it is exactly 0 in a flat calm
        /// or with the owner's factor at 0.
        ///
        /// <para>It rises with the SQUARE of the sea state, not linearly. That is the whole shape of the
        /// owner's "teeth": a chop is a nuisance you barely notice, while a real sea is genuinely
        /// dangerous — which is what makes the decision to go out mean something. A linear ramp would tax
        /// every pleasant day and still not bite in a gale.</para>
        /// </summary>
        /// <param name="seaState01">The live continuous sea state, 0 (glass) .. 1 (storm).</param>
        /// <param name="seaFightFactor">Owner dial: the pressure at a full storm (≥ 0; 0 = OFF, the
        /// weather-blind fight exactly as it shipped).</param>
        public static float TensionPerSec(float seaState01, float seaFightFactor)
        {
            float sea = Mathf.Clamp01(Safe(seaState01));
            return Mathf.Max(0f, Safe(seaFightFactor)) * sea * sea;
        }

        // ---- the sea fishes for you (the reward side of the trade) --------------------------------

        /// <summary>
        /// How the wait for a bite SCALES with the sea: 1 in a flat calm, falling toward
        /// <c>1 − boldness01</c> in a storm. Broken water hides the angler and emboldens the fish, so a
        /// blow simply fishes faster — more casts answered, less staring at a still bobber.
        ///
        /// <para>Linear in sea state (unlike the tension term's square) on purpose: the REWARD should
        /// arrive early and steadily so a mere chop already feels worth fishing, while the COST stays back
        /// until the sea is genuinely rough. That asymmetry is what makes the middle of the range the
        /// sweet spot to fish rather than a wash.</para>
        /// </summary>
        /// <param name="boldness01">Owner dial 0..1: how much quicker a full storm bites (0 = OFF;
        /// 0.5 = bites arrive in half the time at a storm). Clamped so the scale can never reach 0.</param>
        /// <returns>A multiplier for the bite delay, in (0, 1].</returns>
        public static float BiteDelayScale(float seaState01, float boldness01)
        {
            float sea = Mathf.Clamp01(Safe(seaState01));
            float bold = Mathf.Clamp01(Safe(boldness01));
            return Mathf.Clamp(1f - bold * sea, MinBiteDelayScale, 1f);
        }

        /// <summary>Floor under <see cref="BiteDelayScale"/> — the wait may shorten a lot, but never to
        /// nothing (an instant bite reads as a bug, and the cast/settle beat needs room to play).</summary>
        public const float MinBiteDelayScale = 0.15f;

        /// <summary>
        /// How far UP the species' weight range a catch is nudged by the sea, 0 (roll the whole range as
        /// ever) .. 1 (the top of the range). The better fish feed in broken water, so a blow brings up
        /// the ones worth catching — the reward that makes the harder fight worth accepting, and the
        /// reason a storm fish is a story.
        ///
        /// <para>Applied by the caller as a one-sided bias on the EXISTING uniform roll (see
        /// <see cref="BiasedWeight01"/>), never as a separate random draw, so the RNG stream and the
        /// authored min/max range are both untouched (rule 5).</para>
        /// </summary>
        /// <param name="bigFishBias01">Owner dial 0..1: how strongly a full storm favours the big ones
        /// (0 = OFF — the weight roll is exactly the shipped uniform one).</param>
        public static float WeightBias01(float seaState01, float bigFishBias01)
            => Mathf.Clamp01(Safe(bigFishBias01)) * Mathf.Clamp01(Safe(seaState01));

        /// <summary>
        /// Push a uniform 0..1 weight roll toward the top of the range by <paramref name="bias01"/>,
        /// preserving the roll's own randomness (the same draw still maps to the same relative size — a
        /// low roll is still a small fish, just less small). At bias 0 this is the identity, so a calm-day
        /// catch is bit-for-bit the shipped roll. Pure, NaN-safe.
        /// </summary>
        public static float BiasedWeight01(float uniform01, float bias01)
        {
            float u = Mathf.Clamp01(Safe(uniform01));
            float b = Mathf.Clamp01(Safe(bias01));
            return Mathf.Clamp01(u + (1f - u) * b);
        }

        // ---- the guard-rails (where the cozy promise ends, named and testable) ---------------------

        /// <summary>
        /// The forgiving-cove promise, extended onto the water: <b>easing off still nets tension DOWN,
        /// even mid-run, in any EVERYDAY sea</b> (at or below <see cref="CozySeaCeiling01"/>). True iff
        /// <c>runTensionPressure + deckAngleFactor + seaPressureAtTheCeiling &lt; tensionFallPerSec</c>.
        ///
        /// <para>This is the line the owner's teeth sit above: the content sweep asserts it for every
        /// shipped tuning, so ordinary weather can never quietly become unwinnable, while a real blow
        /// (above the ceiling) is free to beat you. It is the exact analogue of
        /// <see cref="RodFightMath.MaintainOutbleedsTheRunAtTheWorstStance"/>, with the sea's worst
        /// EVERYDAY contribution added to the worst stance.</para>
        /// </summary>
        public static bool MaintainOutbleedsTheEverydaySea(
            float runTensionPressure, float deckAngleFactor, float seaFightFactor, float tensionFallPerSec)
            => Safe(runTensionPressure)
             + Mathf.Max(0f, Safe(deckAngleFactor))
             + TensionPerSec(CozySeaCeiling01, seaFightFactor)
             < Safe(tensionFallPerSec);

        /// <summary>
        /// The teeth are REAL: a full storm genuinely out-pressures the ease, so easing off no longer
        /// saves you and a big fish can take the line. True iff the storm's sea pressure alone closes the
        /// gap between the ease and her run.
        ///
        /// <para>Asserted POSITIVELY in the tests: if this ever goes false, the owner's "a real blow can
        /// cost you the fish" has been tuned away to nothing and the sea has quietly gone back to being
        /// cosmetic — the very thing this whole change set to fix.</para>
        /// </summary>
        public static bool AStormCanBeatYou(
            float runTensionPressure, float seaFightFactor, float tensionFallPerSec)
            => Safe(runTensionPressure) + TensionPerSec(1f, seaFightFactor) >= Safe(tensionFallPerSec);

        /// <summary>NaN → 0 (the neutral value) — the module's standard input guard (rule 5).</summary>
        private static float Safe(float x) => float.IsNaN(x) ? 0f : x;
    }
}
