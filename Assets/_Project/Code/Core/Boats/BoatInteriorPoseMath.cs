using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>THE CABIN RIDES THE BOAT — with a comfort filter on the one draw</b> (ADR 0038 proposal 1,
    /// ruled 2026-08-19).
    ///
    /// <para>The interior sheets bake into the SAME cell at the SAME pivot as the exterior
    /// (<see cref="BoatInteriorDef.CellPixels"/> / <see cref="BoatInteriorDef.PivotPixels"/>), so the way
    /// to keep a room in register with the hull it lives in is to pose it with the hull's OWN
    /// roll/pitch/heave rather than with a correction term. That is the whole of the motion ruling, and
    /// this class is the only place the ruling's one qualifier lives: a cabin fills the frame in a way a
    /// deck does not, so the same rock that reads as life outdoors reads as nausea indoors, and the
    /// interior draw takes a scalar fraction of it.</para>
    ///
    /// <para><b>Why this is a thin skin on <see cref="DeckRideMath"/> rather than new trig.</b> "Ride the
    /// pose this hull is actually drawing" is a question already answered, once, for the fisher standing
    /// on her deck — the same published phase, the same decomposition, the same read-never-recompute
    /// discipline for the displaced ride. A cabin is a passenger too. Re-deriving it here would be a
    /// second opinion about one motion, and the two would agree until the sea got interesting. What this
    /// class adds is the three things that are TRUE OF A CABIN and of nothing else:</para>
    /// <list type="number">
    ///   <item><b>Footing is exactly 1</b> (<see cref="CabinFooting01"/>). A fisher braces against a
    ///   tilting deck and takes a fraction of its lean; a wheelhouse is bolted to the hull and takes all
    ///   of it. There is no bracing fraction to tune here and none is offered.</item>
    ///   <item><b>One scale governs every channel.</b> Roll, heave and the pitch's screen travel are
    ///   multiplied by the same number, so the motion is the hull's motion made smaller — never a
    ///   different motion. A per-channel cap would have changed the SHAPE of the ride and broken the
    ///   registration the sheets are baked for.</item>
    ///   <item><b>0 is dead flat and 1 is the hull's own pose</b>, exactly — the accessibility floor and
    ///   the full-fidelity ceiling the ruling names. Both are exact, not approached: at 0 nothing is
    ///   computed at all, and at 1 every term is multiplied by the float identity.</item>
    /// </list>
    ///
    /// <para><b>⚠ It is a filter on ONE DRAW, and rule 5 is the boundary.</b> Nothing here may reach the
    /// hull's pose, her handling, or the save. The boat moves identically at every scale — she is
    /// computed from <c>(worldSeed, gameTime)</c> like everything else — and only the picture of her
    /// inside is calmed. The scale is a player setting
    /// (<c>GameServices.InteriorRockScale</c>), which is why <see cref="BoatInteriorDef"/> does not carry
    /// it: it is not hull data.</para>
    ///
    /// <para><b>Why posing the two differently is SAFE.</b> A scale means the interior and the exterior
    /// are posed differently — which would be a registration bug if they were ever drawn together. They
    /// are not: entering is a LAYER SWAP (ADR 0038 proposal 3), exactly one of the two is on, and the
    /// coupling between the two proposals is the reason they were ruled as a set.</para>
    ///
    /// <para><b>Rules.</b> Pure, static, allocation-free and deterministic — the same discipline as
    /// <see cref="DeckRideMath"/>, and the same reason: it is read every LateUpdate by something that
    /// must not churn the heap (rule 7), and an EditMode test must be able to ask it the same question
    /// twice and get the same bits.</para>
    /// </summary>
    public static class BoatInteriorPoseMath
    {
        /// <summary>
        /// A cabin's footing: <b>1, always</b>. <see cref="DeckRideMath.Ride"/>'s bracing fraction is what
        /// a BODY does about a tilting deck — the shoulders levelling while the boots follow the planking.
        /// A wheelhouse does not brace. It is part of the hull, and it takes the whole lean.
        ///
        /// <para>Named rather than written as a literal 1 at the call site so that the reason survives:
        /// a future reader who sees <see cref="DeckRideMath.Ride"/>'s <c>footing01</c> parameter and
        /// wonders why the interior does not tune it has the answer here, and not in a commit message.</para>
        /// </summary>
        public const float CabinFooting01 = 1f;

        /// <summary>
        /// <b>The pose the interior draw takes this frame.</b>
        ///
        /// <para>Composed exactly as the deck rider's is: the hull's ROCK CYCLE (the in-place lean and
        /// heave her own art draws, from the phase she publishes) plus the DISPLACED RIDE (the whole boat
        /// translated bodily by the sea under her, which on any real sea is the larger motion) — the
        /// second read from what the hull reports having APPLIED, never recomputed, because the storm
        /// heave filter is a stateful spring and a second instance of a stateful smoother agrees only
        /// with itself.</para>
        /// </summary>
        /// <param name="phaseDegrees">The hull's published rock phase (crest 90°, trough 270°);
        /// <see cref="DeckRideMath.LevelPhaseDegrees"/> or any negative/non-finite value means she is
        /// drawing no cycle, which is a calm sea, a hull ashore, or the ride switched off.</param>
        /// <param name="hullRideMeters">The world-vertical metres the hull's drawn image is riding at
        /// right now, <b>as the hull itself reports having applied them</b>
        /// (<c>IBoatHullPresenter.DrawnRideMeters</c>). 0 with the displaced sea off.</param>
        /// <param name="deckRollDegrees">Peak roll of this hull's baked rock cycle, degrees — an ART FACT
        /// of her sheets/rig, passed in rather than held here (rule 6).</param>
        /// <param name="deckHeavePixels">Peak heave of the same cycle, in the sheets' own pixels.</param>
        /// <param name="deckPitchLiftMeters">Screen-vertical travel at the peak of the pitch, metres.</param>
        /// <param name="pixelsPerUnit">PPU the sheets import at, for the pixel→metre conversion.
        /// <b>Per hull</b>: the tanker's interior bakes at 16 where the fleet is 32
        /// (<see cref="BoatInteriorDef.PixelsPerMetre"/>) — never assume the fleet default.</param>
        /// <param name="interiorRockScale">The comfort clamp, clamped to [0, 1] here so a hand-edited
        /// YAML cannot hand a cabin a 2 (out-rocking the hull it lives in) or a −1 (rocking against
        /// her).</param>
        public static DeckRidePose Ride(float phaseDegrees, float hullRideMeters,
                                        float deckRollDegrees, float deckHeavePixels,
                                        float deckPitchLiftMeters, float pixelsPerUnit,
                                        float interiorRockScale)
        {
            float scale = Mathf.Clamp01(interiorRockScale);

            // The accessibility floor, taken before anything is computed: dead flat is not "very nearly
            // level", it is the level pose itself, bit for bit. DeckRideMath would answer the same way
            // through its own strength gate — this states it where the setting is named.
            if (scale <= 0f) return DeckRidePose.Level;

            DeckRidePose cycle = DeckRideMath.Ride(phaseDegrees, deckRollDegrees, deckHeavePixels,
                                                  deckPitchLiftMeters, pixelsPerUnit,
                                                  CabinFooting01, scale);

            return DeckRideMath.RidingHull(cycle, hullRideMeters, scale);
        }

        /// <summary>
        /// The same pose for a hull on the LEGACY TRANSFORM ROCK, which publishes no phase because there
        /// is no baked cycle to publish — she leans her whole visual instead
        /// (<c>IBoatHullPresenter.VisualTiltDegrees</c>).
        ///
        /// <para>The fallback the deck rider already takes, for the same reason and with the same honesty
        /// about what it is: leaning the finished picture by the tilt the hull is applying is not the
        /// baked cycle, but it is strictly better than a cabin standing square inside a hull that visibly
        /// leans. A hull drawing neither (tilt 0, ride 0) gets <see cref="DeckRidePose.Level"/>, which is
        /// the correct picture of a boat lying still.</para>
        /// </summary>
        public static DeckRidePose RideTilt(float hullTiltDegrees, float hullRideMeters,
                                            float interiorRockScale)
        {
            float scale = Mathf.Clamp01(interiorRockScale);
            if (scale <= 0f) return DeckRidePose.Level;

            bool tiltUsable = hullTiltDegrees != 0f &&
                              !float.IsNaN(hullTiltDegrees) && !float.IsInfinity(hullTiltDegrees);
            var cycle = tiltUsable ? new DeckRidePose(hullTiltDegrees * scale, 0f) : DeckRidePose.Level;

            return DeckRideMath.RidingHull(cycle, hullRideMeters, scale);
        }
    }
}
