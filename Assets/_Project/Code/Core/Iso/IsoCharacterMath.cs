using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// The PURE maths behind the 8-direction iso character presenter — velocity → heading, speed → gait,
    /// elapsed time → frame. Split out (the <c>PlayerHaulAnimMath</c> / <c>PlayerSubmergeMath</c> pattern)
    /// so the whole state→cell mapping is EditMode-testable headless. No engine state, no <c>Time</c>,
    /// no RNG, no allocation.
    /// </summary>
    public static class IsoCharacterMath
    {
        /// <summary>
        /// The compass heading (degrees, 0 = North/up, CLOCKWISE) a planar velocity is travelling — the
        /// project's bearing convention, the same one the boats use. Returns
        /// <paramref name="fallbackHeading"/> when the velocity is below <paramref name="minSpeed"/>, which
        /// is what makes a character HOLD its last facing when it stops rather than snapping back to North.
        ///
        /// <para><b>⚠️ Both axes of <paramref name="velocity"/> must be REAL metres.</b> That is true of the
        /// DECK frame — <see cref="DeckRiderFacingMath.DeckBearing"/>, the one frame in the game that still
        /// calls this: x abeam to starboard, y along the keel, metres of deck. It is NOT true of world XY,
        /// where the ¾ camera foreshortens the depth axis by <see cref="IsoGround.GroundDepthScale"/>; a
        /// world-space velocity wants <see cref="GroundHeadingFor"/>.</para>
        /// </summary>
        public static float HeadingFor(Vector2 velocity, float minSpeed, float fallbackHeading)
        {
            float min = Mathf.Max(0f, minSpeed);
            if (velocity.sqrMagnitude < min * min) return fallbackHeading;
            // Atan2(x, y): 0 = +Y (North), growing clockwise toward +X (East).
            return Mathf.Atan2(velocity.x, velocity.y) * Mathf.Rad2Deg;
        }

        /// <summary>
        /// The compass heading a WORLD-space velocity is travelling ACROSS THE GROUND — the same answer as
        /// <see cref="HeadingFor"/> once the world's foreshortened depth axis has been stretched back out
        /// (<see cref="IsoGround.BearingDegrees(Vector2)"/>). This is the one a character's facing row is
        /// picked from, because the baked rows are evenly-spaced GROUND bearings (measured; see
        /// <see cref="IsoGround"/>).
        ///
        /// <para><b>What it fixes.</b> Walking up-and-right at 45° in world XY is a ground bearing of only
        /// 32.7°, not 45°; the raw read is out by up to 12.5°, and since a row spans 45° that error draws
        /// the NEIGHBOURING facing across roughly a fifth of the compass — a band about 10° wide inside
        /// each octant next to N/E/S/W, and about 7.6° next to each diagonal.</para>
        ///
        /// <para><b>The stop test is taken in WORLD units, before the un-squash</b>, on purpose:
        /// <paramref name="minSpeed"/> answers "was that a real step or renderer jitter?", which is a
        /// question about the motion actually measured off the transform. Un-squashing first would quietly
        /// make the threshold 1.56× easier to clear when walking north than when walking east.</para>
        /// </summary>
        public static float GroundHeadingFor(Vector2 worldVelocity, float minSpeed, float fallbackHeading)
        {
            float min = Mathf.Max(0f, minSpeed);
            if (worldVelocity.sqrMagnitude < min * min) return fallbackHeading;
            return IsoGround.BearingDegrees(worldVelocity);
        }

        /// <summary>
        /// The gait a speed WANTS, before the def's art availability is consulted (see
        /// <see cref="CharacterVisualDef.PlayableGait"/>). Below <paramref name="walkThreshold"/> the
        /// character is standing; at or above <paramref name="runThreshold"/> it runs; between, it walks.
        /// Thresholds are clamped into a sane order, so an asset authored with run below walk degrades to
        /// "walk then run at the same speed" rather than producing a gait that can never be reached.
        /// </summary>
        public static CharacterGait GaitFor(float speed, float walkThreshold, float runThreshold)
        {
            float walk = Mathf.Max(0f, walkThreshold);
            float run = Mathf.Max(walk, runThreshold);
            if (speed >= run && run > 0f) return CharacterGait.Run;
            if (speed >= walk) return CharacterGait.Walk;
            return CharacterGait.Idle;
        }

        /// <summary>
        /// Which frame of a <paramref name="frameCount"/>-long cycle is showing after
        /// <paramref name="elapsedSeconds"/> at <paramref name="framesPerSecond"/>. Wraps; negative-safe;
        /// a zero/negative rate or count freezes on frame 0 rather than dividing by zero.
        /// </summary>
        public static int FrameFor(float elapsedSeconds, float framesPerSecond, int frameCount)
        {
            if (frameCount <= 0) return 0;
            if (framesPerSecond <= 0f || elapsedSeconds <= 0f || float.IsNaN(elapsedSeconds)) return 0;
            int step = Mathf.FloorToInt(elapsedSeconds * framesPerSecond);
            return ((step % frameCount) + frameCount) % frameCount;
        }
    }
}
