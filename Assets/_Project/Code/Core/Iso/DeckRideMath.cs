using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>The screen-space pose a body takes to RIDE a rocking deck: a lean and a lift.</summary>
    public readonly struct DeckRidePose
    {
        /// <summary>Additive z-rotation (degrees, +CCW) about the body's FEET — the sheets' pivot is
        /// ground contact, so rotating the visual child about its own origin leans the figure without
        /// sliding it.</summary>
        public readonly float RollDegrees;

        /// <summary>Screen-vertical (world +Y) offset in METRES — the deck coming up to meet the feet.</summary>
        public readonly float LiftMeters;

        public DeckRidePose(float rollDegrees, float liftMeters)
        {
            RollDegrees = rollDegrees;
            LiftMeters = liftMeters;
        }

        /// <summary>Standing square on a level deck — no lean, no lift. What a calm hull, a hull ashore,
        /// or a switched-off ride all answer.</summary>
        public static DeckRidePose Level => new DeckRidePose(0f, 0f);
    }

    /// <summary>
    /// <b>THE CHARACTER RIDES THE BOAT.</b> A figure standing on a rolling deck is not standing on the
    /// ground: the deck lifts them at the crest, drops them into the trough, and leans under their feet.
    /// Until this existed the on-deck fisher stood bolt upright on a hull that visibly rolled around them
    /// — the deck moved and its passenger did not.
    ///
    /// <para><b>The same cycle the hull draws, read one way.</b> The hull's rock is a phase: crest at 90°,
    /// trough at 270° (<c>DoryRockMath</c>'s convention, which both hull paths speak — the sprite hull
    /// rounds it to baked frames, the mesh hull poses it continuously). Given that phase this returns what
    /// a body ON that deck does, in exactly the decomposition the boat's own overlays already use
    /// (<c>DoryOarMath.RockPose</c>): <c>sin</c> carries roll and heave, <c>cos</c> carries the pitch's
    /// screen-vertical travel. Reading the same phase through the same trig is what keeps the rider
    /// locked to the hull instead of merely wobbling near it.</para>
    ///
    /// <para><b>Heave is EXACT; roll is BRACED.</b> The two channels are deliberately not treated alike:
    /// <list type="bullet">
    ///   <item><b>Lift</b> is reproduced at the deck's own amplitude, because the character's feet are ON
    ///   the deck. Any gain here would sink them through the planking at the crest.</item>
    ///   <item><b>Lean</b> is scaled by <paramref name="footing01"/>, because a fisher braces. 1 = bolted
    ///   to the deck (the full baked roll — a crate, a bollard); 0 = perfectly upright whatever the sea
    ///   does (the old bug). The tuned middle reads as someone keeping their feet.</item>
    /// </list>
    /// </para>
    ///
    /// <para><b>Relationship to the rig's <c>counterLean</c>.</b> The character rig can bake a genuine
    /// counter-lean — <c>characterIsoRig6.js</c>'s <c>pose()</c> takes a <c>rock</c> argument and applies
    /// <c>list = −roll·k</c>, the fisher's body listing AGAINST the deck. The shipped sheets are baked
    /// WITHOUT it (one upright body per direction), so the runtime cannot draw a real counter-lean — it
    /// can only lean the finished picture. <paramref name="footing01"/> is that counter-lean expressed the
    /// only way a baked sheet can express it: <c>footing01 ≈ 1 − counter</c>. A truer brace (the shoulders
    /// levelling while the hips follow the deck) needs a re-bake with the <c>rock</c> opts — art-director
    /// lane, flagged rather than faked here.</para>
    ///
    /// <para><b>Facing does not enter.</b> The hull states its rock as roll about the keel and pitch about
    /// the beam, and a MESH character riding it must rotate that tilt vector into its own frame (the
    /// spike's <c>DeckCharacterSpikeMath.DeckTiltToCharacter</c>). A SPRITE character must not: its lean
    /// is a screen-space z-rotation of a finished picture, and the screen-space tilt of a body standing
    /// on a tilting plane is the same whichever way the body faces — only which sheet ROW is drawn
    /// changes. Rotating the tilt by the local heading here would make the fisher lean the wrong way every
    /// time they turned round on deck.</para>
    ///
    /// <para><b>Rules.</b> Pure, static, allocation-free and deterministic — same discipline (and EditMode
    /// coverage) as <c>DoryOarMath</c> / <c>DoryRockMath</c>. Visual-only: nothing here feeds the sim
    /// (rule 5), and every amplitude arrives as a caller-owned tunable (rule 6).</para>
    /// </summary>
    public static class DeckRideMath
    {
        /// <summary>The phase value that means "this deck is not rocking" — a calm sea, a hull ashore, or
        /// the ride switched off. Negative because the cycle itself is [0, 360).</summary>
        public const float LevelPhaseDegrees = -1f;

        /// <summary>Guard floor on pixels-per-unit so a mis-authored 0 can never divide by zero. A guard,
        /// not a tunable.</summary>
        public const float MinPixelsPerUnit = 1e-3f;

        /// <summary>True when a phase describes an actually-rocking deck rather than the level pose.
        /// Anything negative, or not finite, is level — a NaN must park the rider, never launch them.</summary>
        public static bool IsRocking(float phaseDegrees)
            => phaseDegrees >= 0f && !float.IsNaN(phaseDegrees) && !float.IsInfinity(phaseDegrees);

        /// <summary>
        /// The pose a body takes on a deck at wave phase <paramref name="phaseDegrees"/> (crest 90°,
        /// trough 270°).
        /// </summary>
        /// <param name="phaseDegrees">The hull's current rock phase. <see cref="LevelPhaseDegrees"/> (or
        /// any negative / non-finite value) returns <see cref="DeckRidePose.Level"/>, so a still hull
        /// never gets a swaying passenger.</param>
        /// <param name="deckRollDegrees">Peak roll of the deck under the body, in degrees — the art fact
        /// of the hull's baked rock cycle (the iso dory bakes 5°).</param>
        /// <param name="deckHeavePixels">Peak heave of the deck in PIXELS at the sheets' own resolution
        /// (the iso dory bakes 1.6 px), converted through <paramref name="pixelsPerUnit"/> so the rider
        /// rises with the gunwale to the pixel.</param>
        /// <param name="deckPitchLiftMeters">Screen-vertical travel at the peak of the PITCH, in metres —
        /// a ¾ view reads a bow-up/bow-down tip mostly as vertical movement at this scale.</param>
        /// <param name="pixelsPerUnit">PPU the sheets import at (32), for the pixel→metre conversion.</param>
        /// <param name="footing01">How much of the deck's LEAN the body takes: 1 = rigidly attached,
        /// 0 = stays upright. Clamped.</param>
        /// <param name="strength">Master scale on the whole ride. 0 restores the old bolt-upright
        /// behaviour exactly, which is what makes this an owner-checkable A/B (rule 6).</param>
        public static DeckRidePose Ride(float phaseDegrees, float deckRollDegrees, float deckHeavePixels,
                                        float deckPitchLiftMeters, float pixelsPerUnit,
                                        float footing01, float strength)
        {
            if (!IsRocking(phaseDegrees) || strength <= 0f) return DeckRidePose.Level;

            float a = phaseDegrees * Mathf.Deg2Rad;
            float sin = Mathf.Sin(a);
            float cos = Mathf.Cos(a);

            float heaveMeters = deckHeavePixels / Mathf.Max(MinPixelsPerUnit, pixelsPerUnit);
            float brace = Mathf.Clamp01(footing01);

            return new DeckRidePose(
                deckRollDegrees * sin * brace * strength,
                (heaveMeters * sin + deckPitchLiftMeters * cos) * strength);
        }

        /// <summary>
        /// <b>…AND THE DECK IS ALSO GOING UP AND DOWN.</b> <see cref="Ride"/> describes the ROCK CYCLE
        /// — the in-place roll/heave/pitch a hull's own art draws, a pixel or two of heave at most. On a
        /// displaced sea (ADR 0023 — the default sea) the whole boat is ALSO translated bodily by the
        /// water's real lift under her, tens of times larger, and on any real sea that translation IS
        /// the motion of the deck. A body standing on it takes the whole of it.
        ///
        /// <para><b>The lift ADDS; the lean does not change.</b> A world translation moves a deck
        /// without tilting it, so <paramref name="hullRideMeters"/> touches
        /// <see cref="DeckRidePose.LiftMeters"/> alone. There is no bracing fraction on it either:
        /// bracing is what a body does about the TILT of a deck, and no amount of it keeps a fisher at
        /// one altitude while the planking under their boots rises a metre.</para>
        ///
        /// <para><b>It must be the ride the hull APPLIED, never one recomputed here.</b> The storm
        /// heave filter is a stateful spring-damper (<c>StormRockMath.StepHeaveWeight</c> — hulls
        /// unweight and fall at g over a sharpened crest), and a stateful smoother agrees only with
        /// itself: a second instance fed the same sea diverges within a few frames, and it diverges
        /// hardest exactly when the sea is worst. The caller reads what the hull published having
        /// drawn; this function only composes it.</para>
        ///
        /// <para>A non-finite ride parks the rider rather than launching them — the same discipline
        /// <see cref="IsRocking"/> keeps for the phase.</para>
        /// </summary>
        /// <param name="deckPose">The rock-cycle pose from <see cref="Ride"/> (or
        /// <see cref="DeckRidePose.Level"/> on a hull drawing no cycle at all).</param>
        /// <param name="hullRideMeters">The world-vertical metres the hull's drawn image is riding at
        /// right now, as the hull itself reports having applied them.</param>
        /// <param name="strength">Scale on this term alone. 0 returns <paramref name="deckPose"/>
        /// unchanged — exactly the picture before the ride was added (rule 6's A/B).</param>
        public static DeckRidePose RidingHull(in DeckRidePose deckPose, float hullRideMeters,
                                              float strength)
        {
            if (strength <= 0f || hullRideMeters == 0f) return deckPose;
            if (float.IsNaN(hullRideMeters) || float.IsInfinity(hullRideMeters)) return deckPose;
            return new DeckRidePose(deckPose.RollDegrees,
                                    deckPose.LiftMeters + hullRideMeters * strength);
        }

        /// <summary>
        /// The phase a hull drawing baked rock FRAME <paramref name="rockFrame"/> is at:
        /// <c>a = i·(360/frameCount)</c>, so the shipped 8-frame sheet puts the crest on frame 2 (90°) and
        /// the trough on frame 6 (270°). The frame-space twin of <c>DoryOarMath.RockPhaseDegrees</c>,
        /// restated here because Core may not reference Boats (rule 4) — and pinned against it by
        /// <c>DeckRideMathTests</c> so the two can never drift.
        ///
        /// <para>A negative frame is the documented level/calm pose and answers
        /// <see cref="LevelPhaseDegrees"/>, so it flows straight into <see cref="Ride"/>.</para>
        /// </summary>
        public static float PhaseForRockFrame(int rockFrame, int frameCount)
        {
            if (rockFrame < 0) return LevelPhaseDegrees;
            int count = Mathf.Max(1, frameCount);
            int frame = rockFrame % count;
            if (frame < 0) frame += count;
            return frame * (360f / count);
        }
    }
}
