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
        /// </summary>
        public static float HeadingFor(Vector2 velocity, float minSpeed, float fallbackHeading)
        {
            float min = Mathf.Max(0f, minSpeed);
            if (velocity.sqrMagnitude < min * min) return fallbackHeading;
            // Atan2(x, y): 0 = +Y (North), growing clockwise toward +X (East).
            return Mathf.Atan2(velocity.x, velocity.y) * Mathf.Rad2Deg;
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
