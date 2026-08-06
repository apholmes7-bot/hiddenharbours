using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>The PURE maths of a mooring line's SCOPE against a moving tide</b> — M2-38's design consideration,
    /// and the one piece of this feature that had to be got right rather than merely built.
    ///
    /// <para><b>The law, in one sentence.</b> A line has a fixed LENGTH (its scope). The two ends do not:
    /// the shore cleat stands still while the boat's cleat rides the water. So the line must span a
    /// <b>three-dimensional</b> gap whose vertical component the tide drives — and whatever the drop spends
    /// is no longer available to reach ACROSS the water. <see cref="HorizontalReach"/> is that trade,
    /// <c>√(scope² − drop²)</c>, and it is the only place the rule is written down.</para>
    ///
    /// <para><b>Why that single function covers both hazards the owner named.</b> The vertical term is
    /// <c>|boatCleat − shoreCleat|</c>, which grows as the boat falls BELOW the cleat and equally as she
    /// rises ABOVE it — it is smallest when the two are level. So:
    /// <list type="bullet">
    ///   <item><b>Falling tide, short line</b> (tied bar-taut at high water to a wharf): the boat drops
    ///   away, the drop grows, the horizontal reach collapses, and she is hauled in against her own
    ///   sheer until the loop surrenders. The classic way to hang a boat off a wharf.</item>
    ///   <item><b>Rising tide, short line</b> (tied bar-taut at low water to something LOW — a float, a
    ///   ring at the waterline): she now rises PAST her cleat, the drop grows again from the other
    ///   direction, and the line pins her down as the water lifts her. The owner's mirrored case, and it
    ///   falls out of the same absolute value rather than needing a second rule.</item>
    /// </list>
    /// Tie to a high wharf at low water, though, and a rising tide makes the line <i>slacker</i> — which is
    /// exactly what real seamanship says, and is the reward P1 is teaching: leave scope for the tide you
    /// expect, and mind where your cleat sits relative to your deck.</para>
    ///
    /// <para><b>Deterministic (rule 5).</b> Everything here is a pure function of its arguments. The only
    /// time-varying input is the water level, which the caller reads from
    /// <see cref="IEnvironmentService.WaterLevelAt"/> — itself recomputed from <c>(worldSeed, gameTime)</c>
    /// and never saved. Nothing here allocates, holds state, or touches <c>Time</c>, so the whole tide law
    /// is EditMode-testable headless. NaN-safe throughout, in the house style: junk in reads as the neutral
    /// value rather than propagating.</para>
    ///
    /// <para><b>No magic numbers (rule 6).</b> Every threshold — scope limits, the step, the working load —
    /// arrives as a parameter, sourced from <see cref="MooringLineSettings"/> on <c>GameConfig</c>.</para>
    /// </summary>
    public static class MooringLineMath
    {
        /// <summary>
        /// The straight-line distance the rope actually has to cover between two cleats: the horizontal
        /// separation and the vertical drop, combined. This — not the map distance — is what a rope of a
        /// given scope is measured against.
        /// </summary>
        public static float Span(Vector2 a, float aElevation, Vector2 b, float bElevation)
        {
            float dx = Safe(b.x) - Safe(a.x);
            float dy = Safe(b.y) - Safe(a.y);
            float dz = Safe(bElevation) - Safe(aElevation);
            return Mathf.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        /// <summary>The vertical gap between two cleats (m), always non-negative — the tide-driven term.
        /// Symmetric on purpose: a boat hanging below her cleat and a boat lifted above it are the same
        /// geometry and the same hazard (see the class doc).</summary>
        public static float VerticalDrop(float aElevation, float bElevation)
            => Mathf.Abs(Safe(bElevation) - Safe(aElevation));

        /// <summary>
        /// <b>THE TIDE LAW.</b> How much HORIZONTAL room a line of <paramref name="scope"/> still has once
        /// <paramref name="verticalDrop"/> metres of it are spent climbing the gap between the two cleats:
        /// <c>√(scope² − drop²)</c>.
        ///
        /// <para>Returns <b>0</b> once the drop equals or exceeds the scope — the rope cannot even reach
        /// vertically, let alone across. That is not a clamp for safety; it is the physical statement that
        /// the boat is now hanging on the line, and it is precisely the condition
        /// <see cref="Slips"/> turns into the cozy fail.</para>
        ///
        /// <para>This is the ONE function the constraint, the visual and the readout all go through, so the
        /// rope the player sees straining can never disagree with the circle the physics is holding her
        /// inside of.</para>
        /// </summary>
        public static float HorizontalReach(float scope, float verticalDrop)
        {
            float s = Mathf.Max(0f, Safe(scope));
            float d = Mathf.Abs(Safe(verticalDrop));
            float remaining = s * s - d * d;
            return remaining <= 0f ? 0f : Mathf.Sqrt(remaining);
        }

        /// <summary>
        /// How hard the line is working: <c>0</c> = the boat sits on top of her cleat (all slack),
        /// <c>1</c> = bar-taut at exactly her scope, <c>&gt;1</c> = the sea is asking for more rope than
        /// there is. Drives the taut/slack read on the rope visual and the creak-audio hook.
        /// A zero/degenerate scope reads as fully loaded — a line with no length is always taut.
        /// </summary>
        public static float Load01(float span, float scope)
        {
            float s = Mathf.Max(0f, Safe(scope));
            if (s <= 1e-5f) return 1f;
            return Mathf.Max(0f, Safe(span)) / s;
        }

        /// <summary>
        /// <b>The cozy fail.</b> True when the loop surrenders and the boat goes adrift: the span has
        /// exceeded the scope by more than the working-load margin.
        ///
        /// <para><paramref name="workingLoadFactor"/> is how far past bar-taut the line is willing to be
        /// worked before it lets go (1.0 = it slips the instant it comes taut, 1.25 = it will take a
        /// quarter again its length of strain first). Above 1 by design: a rope that surrenders at the
        /// first creak would make every mooring a coin-toss, and P5's teeth are supposed to be earned by
        /// misjudging the tide, not by touching the water.</para>
        ///
        /// <para>No damage and no parted rope — the loop slips off the cleat, and the player coils it and
        /// tries again (backlog M2-38's own wording).</para>
        /// </summary>
        public static bool Slips(float span, float scope, float workingLoadFactor)
            => Load01(span, scope) > Mathf.Max(1f, Safe(workingLoadFactor));

        /// <summary>
        /// A boat cleat's elevation above chart datum: the water level, plus how high that fitting stands
        /// above her floating waterline. <b>This is what makes a boat cleat tidal and a shore cleat not</b>
        /// — she floats, so her fittings rise and fall with the sea by construction, no per-hull bookkeeping.
        /// </summary>
        public static float BoatCleatElevation(float waterLevel, float cleatHeightAboveWaterline)
            => Safe(waterLevel) + Safe(cleatHeightAboveWaterline);

        /// <summary>
        /// How high a rig's cleat stands above the water she floats in: its authored height above the KEEL
        /// (the sidecars' hull-local <c>+z</c>) less the hull's draught. Never negative — a fitting modelled
        /// below the waterline is data we cannot honour, and a rope tied at the waterline is the honest
        /// degenerate reading of it.
        ///
        /// <para>The draught is subtracted rather than scaled: this is the tide-pacing ruling's split, where
        /// a tide-fraction elevation moves with the water and a draught elevation is a fixed property of
        /// the hull.</para>
        /// </summary>
        public static float CleatHeightAboveWaterline(float cleatZAboveKeel, float draughtMeters)
            => Mathf.Max(0f, Safe(cleatZAboveKeel) - Mathf.Max(0f, Safe(draughtMeters)));

        /// <summary>
        /// Tighten (negative <paramref name="steps"/>) or slacken (positive) the line by whole steps of
        /// <paramref name="stepMetres"/>, clamped into <c>[minScope, maxScope]</c>. Stepped rather than
        /// continuous so the choice is legible — the player can count the scope they are paying out and
        /// learn what a given tide costs — and so a keypress is a decision rather than a drag.
        /// </summary>
        public static float StepScope(float scope, int steps, float stepMetres, float minScope, float maxScope)
        {
            float lo = Mathf.Max(0f, Safe(minScope));
            float hi = Mathf.Max(lo, Safe(maxScope));
            float next = Safe(scope) + steps * Mathf.Max(0f, Safe(stepMetres));
            return Mathf.Clamp(next, lo, hi);
        }

        /// <summary>
        /// The scope a careful skipper would pay out for a given tidal range: enough that when the water
        /// has fallen by <paramref name="expectedFallMetres"/> the line still has
        /// <paramref name="horizontalRoomMetres"/> of reach left across the water. The inverse of
        /// <see cref="HorizontalReach"/>, and the number the tide-table readout can teach with.
        ///
        /// <para>Nothing in the sim consumes this — it exists so the advice the game gives and the physics
        /// it runs come from the same algebra rather than from a designer's estimate.</para>
        /// </summary>
        public static float ScopeForFall(float currentDrop, float expectedFallMetres, float horizontalRoomMetres)
        {
            float drop = Mathf.Abs(Safe(currentDrop)) + Mathf.Max(0f, Safe(expectedFallMetres));
            float room = Mathf.Max(0f, Safe(horizontalRoomMetres));
            return Mathf.Sqrt(drop * drop + room * room);
        }

        /// <summary>NaN → 0 (the neutral value) — the guard every pure helper in this repo carries.</summary>
        private static float Safe(float x) => float.IsNaN(x) ? 0f : x;
    }
}
