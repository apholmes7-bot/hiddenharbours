using System;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>The PURE maths of getting down a wharf ladder</b> — when the tide has opened a gap the boarding
    /// step cannot honestly cross, how long the climb takes, and (the part that had to be got right rather
    /// than merely built) <b>how far the body has sunk at any moment of it</b>.
    ///
    /// <para><b>The law, in one sentence.</b> A wharf deck stands still above chart datum; a boat floats.
    /// The vertical gap between them is therefore tide-driven, and once it beats what the rig's
    /// <c>board</c> clip can depict as a step, boarding has to go down a ladder instead. Everything here
    /// is a pure function of its arguments, so the whole rule is EditMode-testable headless and the tide
    /// enters only through the water level the caller passes in (recomputed from
    /// <c>(worldSeed, gameTime)</c>, never saved — rule 5).</para>
    ///
    /// <para><b>⚠ THE BODY DOES NOT SINK AT A CONSTANT RATE.</b> This is the one thing an engine driving
    /// the rig's <c>ladderDown</c> clip has to respect, and the reason this file exists rather than a
    /// multiply by a rate. A climber drops when a LEG EXTENDS: the descent is a two-step STAIR — a rung
    /// eased through each foot swing, dead flat while both feet are planted — not a ramp. A planted foot
    /// is fixed in the WORLD, so its height inside the cell is <c>(hold − descend)</c>; translate the
    /// sprite by <see cref="DescendMetresAt"/> and the soles sit still on real rungs, translate it
    /// linearly at the clip's average rate instead and they CREEP. Measured against the shipped bake that
    /// creep peaks at <b>0.086 m — 2.7 px at the locked 32 px = 1 m, better than a quarter of a rung</b>
    /// (<c>LadderBoardingMathTests.TheStair_IsNotARamp_AndTheDifferenceIsVisible</c>), which on a
    /// four-loop climb is a foot visibly sliding down every rung it lands on.</para>
    ///
    /// <para><b>A twin, and pinned as one.</b> <see cref="DescendMetresAt"/> reproduces
    /// <c>characterIsoRig6.js → ladderCurve()</c>'s <c>D(t)</c> exactly, in the repo's established
    /// twin-constant idiom (the shader/config/twin lockstep). It is NOT trusted on its word:
    /// <c>LadderBoardingMathTests.TheStair_ReproducesTheBakedDescendTable</c> parses the shipped
    /// <c>FisherFightAnchors.json</c> and asserts this function against every baked frame of the real
    /// sidecar, so a rig revision that moves the stair fails the build instead of quietly animating
    /// wrong. Reading the sidecar at RUNTIME was the alternative and was rejected: it would need a
    /// serialized <c>TextAsset</c> wired into a scene, and a scene wiring is exactly what cannot be
    /// trusted while the region rebuild is pending — a compile-time twin with a test-time pin needs no
    /// wiring at all and fails louder.</para>
    ///
    /// <para><b>No magic numbers (rule 6).</b> Every constant the stair needs — the rung, the swing
    /// fraction, the two foot phases — arrives as a parameter, sourced from
    /// <see cref="LadderBoardingSettings"/> on <c>GameConfig</c>. The <c>Rig*</c> constants below are the
    /// rig's own published defaults, named so a test can pin them, not so callers can skip the config.</para>
    /// </summary>
    public static class LadderBoardingMath
    {
        // ---- the rig's published ladder constants (characterIsoRig6.js §LADDER) --------------------
        // Named so the config defaults and the tests can cite ONE source rather than three copies of a
        // literal. These are the rig's DEFAULTS: a kit that re-authors the climb changes them here, in
        // the config's Default, and the sidecar pin catches anything that forgot.

        /// <summary>The rig's default rung spacing (m) — <c>RUNG_DEF</c>, and <c>WharfIso.FIT.ladder.rung</c>.</summary>
        public const float RigRungMetres = 0.30f;

        /// <summary>How much of the loop one foot's SWING occupies — <c>LAD_SW</c>. The stair's tread
        /// width: the descent is eased across this fraction and flat outside it.</summary>
        public const float RigSwingFraction = 0.24f;

        /// <summary>Loop phase at which the RIGHT foot's swing begins — <c>LAD_RF</c>.</summary>
        public const float RigLeadFootPhase = 0.00f;

        /// <summary>Loop phase at which the LEFT foot's swing begins — <c>LAD_LF</c>. Half a loop later:
        /// two steps per cycle, which is why the loop descends <see cref="StepZFor"/> = 2 × rung.</summary>
        public const float RigTrailFootPhase = 0.50f;

        /// <summary>How far the climber's pivot sits off the ladder PLANE (m) — <c>ladderMount().standoff</c>.
        /// Seat the sprite this far out and a face-mounted ladder needs no eyeballing; the art was measured
        /// for it, so it is never a number to tune by eye.</summary>
        public const float RigStandoffMetres = 0.275f;

        /// <summary>The rig's <c>ladderDown</c> loop: 10 frames × 110 ms.</summary>
        public const float RigLoopSeconds = 1.10f;

        // ---- the gap, and whether it needs a ladder ------------------------------------------------

        /// <summary>
        /// The vertical gap (m) a fisher must cross to board: how far the boat's deck lies BELOW the
        /// wharf top. <b>Signed on purpose</b> — a hull whose deck rides above the wharf at high water
        /// gives a negative gap, which is a step UP and the <c>board</c> clip's own business, not a
        /// ladder's. This is the wharf kit's <c>gunwale</c> quantity (<c>drop − freeboard</c>), measured
        /// in the one frame the tide, the terrain and <see cref="IStandableSurface"/> all speak.
        /// </summary>
        public static float VerticalGap(float wharfTopElevation, float boatDeckElevation)
            => Safe(wharfTopElevation) - Safe(boatDeckElevation);

        /// <summary>
        /// A boat deck's elevation above chart datum right now — the water she floats in plus how high
        /// that deck stands above her own floating waterline.
        ///
        /// <para>Deliberately composed from <see cref="MooringLineMath"/> rather than re-derived: the
        /// mooring feature already answers "how high is a fitting on this hull above datum", and a second
        /// spelling of it would let the ladder and the ropes disagree about where the same deck is.</para>
        /// </summary>
        public static float BoatDeckElevation(float waterLevel, float deckHeightAboveKeel, float draughtMetres)
            => MooringLineMath.BoatCleatElevation(
                   waterLevel, MooringLineMath.CleatHeightAboveWaterline(deckHeightAboveKeel, draughtMetres));

        /// <summary>
        /// <b>THE THRESHOLD.</b> True when the gap has beaten what a boarding STEP can honestly depict and
        /// the ladder is the answer.
        ///
        /// <para><paramref name="boardClampMetres"/> is owner data
        /// (<see cref="LadderBoardingSettings.BoardClampMetres"/>), never a literal here — see that
        /// field's tooltip for why the kit states two different numbers for it and which one ships.</para>
        /// </summary>
        public static bool NeedsLadder(float gapMetres, float boardClampMetres)
            => Safe(gapMetres) > Mathf.Max(0f, Safe(boardClampMetres));

        // ---- the descent stair ----------------------------------------------------------------------

        /// <summary>How far one whole loop of the clip descends (m): two rungs, because the cycle takes
        /// one step on each foot.</summary>
        public static float StepZFor(float rungMetres) => 2f * Mathf.Max(0f, Safe(rungMetres));

        /// <summary>
        /// <b>THE STAIR.</b> Metres descended by loop phase <paramref name="phase"/> (1.0 = one whole
        /// loop = <see cref="StepZFor"/>), reproducing the rig's <c>ladderCurve().drop</c> exactly.
        ///
        /// <para>Defined PAST the ends of the loop, like the rig's own <c>D(t)</c> — a foot planted across
        /// the wrap must read as one continuous stair rather than snapping back at phase 0, so whole
        /// loops contribute <c>stepZ</c> each and only the fraction runs the two eased treads.</para>
        ///
        /// <para>Negative phase reads as 0 (the climb has not started), so a caller easing into the move
        /// cannot drive the body upward through the wharf.</para>
        /// </summary>
        public static float DescendMetresAt(double phase, float rungMetres, float swingFraction,
                                            float leadFootPhase, float trailFootPhase)
        {
            float r = Mathf.Max(0f, Safe(rungMetres));
            if (r <= 0f) return 0f;
            if (double.IsNaN(phase) || phase <= 0.0) return 0f;

            float sw = Mathf.Max(1e-4f, Safe(swingFraction));
            double whole = Math.Floor(phase);
            float f = (float)(phase - whole);

            return (float)(2.0 * r * whole)
                 + r * Smooth(Tread(Safe(leadFootPhase), f, sw))
                 + r * Smooth(Tread(Safe(trailFootPhase), f, sw));
        }

        /// <summary>
        /// <b>Where the climber is, relative to where the climb started</b> — the signed height change (m)
        /// at loop phase <paramref name="phase"/>, clamped to the gap being crossed. Negative going down,
        /// positive going up. The one call a presenter needs: it carries the stair, the direction and the
        /// end of the climb together, so none of the three can be got right in one place and wrong in
        /// another.
        ///
        /// <para>The CLAMP is what stops the last part-rung carrying the fisher past the deck she is
        /// climbing to: a gap is a tide reading and almost never lands on a whole number of rungs, so the
        /// final tread would otherwise overshoot by up to most of one.</para>
        ///
        /// <para><b>⚠ Ascent is the kit's own known gap.</b> There is no <c>ladderUp</c> clip and going up
        /// is NOT this reversed (climbing, the arms pull and the hips stay in). The rung QUANTIZATION is
        /// the half of it that carries over unchanged — a climber going up plants on the same rungs at the
        /// same tread spacing — so ascent reuses this stair sign-flipped and the body still rises
        /// rung-by-rung rather than creeping. The limb choreography stays descent-flavoured until
        /// <c>ladderUp</c> bakes, at which point that becomes a clip swap and this maths does not move.</para>
        /// </summary>
        public static float TravelMetresAt(double phase, float rungMetres, float swingFraction,
                                           float leadFootPhase, float trailFootPhase,
                                           float gapMetres, bool ascending)
        {
            float travelled = Mathf.Min(
                Mathf.Max(0f, Safe(gapMetres)),
                DescendMetresAt(phase, rungMetres, swingFraction, leadFootPhase, trailFootPhase));
            return ascending ? travelled : -travelled;
        }

        // ---- how long the climb takes ---------------------------------------------------------------

        /// <summary>
        /// How many loops of the clip a <paramref name="gapMetres"/> climb takes — fractional, because a
        /// gap is a tide reading and never lands on a whole number of rungs. Zero for a gap that does not
        /// need climbing at all.
        /// </summary>
        public static float LoopsForGap(float gapMetres, float rungMetres)
        {
            float step = StepZFor(rungMetres);
            if (step <= 1e-5f) return 0f;
            return Mathf.Max(0f, Safe(gapMetres)) / step;
        }

        /// <summary>
        /// How long the climb itself lasts (real seconds) for a gap — the loops it takes at the clip's own
        /// loop length. <b>The clip is never rate-scaled to fit a duration</b>, unlike the vault: a ladder
        /// has real rungs a real distance apart, so a deeper gap must take LONGER rather than the same
        /// time faster. That is what keeps the tide legible (P1) — the same wharf is a step at high water
        /// and a long climb at low.
        /// </summary>
        public static float ClimbSeconds(float gapMetres, float rungMetres, float loopSeconds)
            => LoopsForGap(gapMetres, rungMetres) * Mathf.Max(0f, Safe(loopSeconds));

        /// <summary>The loop phase reached after <paramref name="elapsedSeconds"/> of climbing. A
        /// zero/degenerate loop length reads as phase 0 rather than as infinity.</summary>
        public static double PhaseAt(float elapsedSeconds, float loopSeconds)
        {
            float loop = Mathf.Max(0f, Safe(loopSeconds));
            if (loop <= 1e-5f) return 0.0;
            return Mathf.Max(0f, Safe(elapsedSeconds)) / (double)loop;
        }

        // ---- seating the climber on the ladder ------------------------------------------------------

        /// <summary>
        /// Where the climber's pivot sits in PLAN, given the ladder's own position and the compass heading
        /// its face looks along: <paramref name="standoffMetres"/> out from the ladder plane, along that
        /// facing.
        ///
        /// <para>The standoff is a MEASURED rig quantity (<c>ladderMount().standoff</c>, 0.275 m), not a
        /// nudge — the pose was authored with the body that far off the stringers, so seating the sprite
        /// on the ladder line puts its hands inside the wall.</para>
        /// </summary>
        public static Vector2 SeatOffset(float faceHeadingDegrees, float standoffMetres)
        {
            float rad = Safe(faceHeadingDegrees) * Mathf.Deg2Rad;
            float d = Safe(standoffMetres);
            // Compass: 0 = +Y (north), 90 = +X (east), clockwise — the convention DeckToWorld uses.
            return new Vector2(Mathf.Sin(rad) * d, Mathf.Cos(rad) * d);
        }

        /// <summary>The compass heading a climber FACES on a ladder whose face looks along
        /// <paramref name="faceHeadingDegrees"/>: into the wall, i.e. the reciprocal. A climber has their
        /// back to the water.</summary>
        public static float ClimberHeadingFor(float faceHeadingDegrees)
        {
            float h = Safe(faceHeadingDegrees) + 180f;
            h %= 360f;
            return h < 0f ? h + 360f : h;
        }

        // ---- helpers ---------------------------------------------------------------------------------

        /// <summary>The rig's own easing — <c>sm = t*t*(3-2*t)</c>, plain smoothstep.</summary>
        private static float Smooth(float t) => t * t * (3f - 2f * t);

        /// <summary>One foot's tread progress at loop fraction <paramref name="f"/>: 0 before its swing
        /// starts, 1 once it has finished, eased between. The rig's <c>seg(a,f)</c>.</summary>
        private static float Tread(float startPhase, float f, float swingFraction)
            => Mathf.Clamp01((f - startPhase) / swingFraction);

        /// <summary>NaN → 0 (the neutral value) — the guard every pure helper in this repo carries.</summary>
        private static float Safe(float x) => float.IsNaN(x) ? 0f : x;
    }
}
