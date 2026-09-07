namespace HiddenHarbours.Core
{
    /// <summary>
    /// THE ONE JUMP LAW — how high a fish is out of the water at an instant, how far she travels doing
    /// it, and which frame of the rig's <c>jump</c> strip is showing.
    ///
    /// <para><b>Why it lives in Core.</b> Two different drawers make a fish leave the water: the shoal
    /// (<c>FishSchoolPresenter</c>, in Fishing) and the hooked fish on the line
    /// (<c>RodFightPresenter</c>, in Player). The owner's 2026-09-06 ruling is that these are the same
    /// jump — "a hooked bass leaves the water the way a free one does" — and the only way two modules
    /// that cannot reference each other can be held to that is for the arithmetic to sit under both of
    /// them. <c>ShoalEventMath</c> forwards its own jump members here rather than keeping a second copy
    /// (rule 4, and the plain fact that two transcriptions of one law drift).</para>
    ///
    /// <para><b>The numbers are the rig's</b>, not invented here: <c>ANIMS.jump</c> is 6 frames at 95 ms
    /// and <c>MOTION.jump.travel</c> is 0.55 m in <c>fishIsoRig2.js</c>. A presenter that plays the strip
    /// on the spot is drawing it wrong — she covers ground across those frames.</para>
    ///
    /// <para>Pure arithmetic over doubles, no engine types, no state: EditMode-testable with no scene.</para>
    /// FLAG lead-architect: new Core contract (the shared jump law for the visible-fish arc).
    /// </summary>
    public static class FishJumpArc
    {
        /// <summary>Frames in the rig's <c>jump</c> anim (<c>ANIMS.jump.n</c>).</summary>
        public const int Frames = 6;

        /// <summary>Milliseconds per <c>jump</c> frame (<c>ANIMS.jump.ms</c>).</summary>
        public const double FrameMs = 95.0;

        /// <summary>How far she travels across the whole jump, in metres (<c>MOTION.jump.travel</c>).</summary>
        public const float TravelMetres = 0.55f;

        /// <summary>How long one jump lasts, in seconds.</summary>
        public static double DurationSeconds => Frames * FrameMs / 1000.0;

        /// <summary>
        /// How far out of the water she is, 0..1 — <b>0 at the surface, 1 at the top of the arc, 0 again
        /// on re-entry</b>, and 0 outside the jump entirely. A half-sine, so the leaving and the landing
        /// are symmetric and there is no step at either end for the eye to catch.
        ///
        /// <para>Multiply by <see cref="TravelMetres"/> for a world-space lift.</para>
        /// </summary>
        public static float Arc01(double startSeconds, double gameSeconds)
        {
            double d = DurationSeconds;
            if (d <= 0.0) return 0f;
            double u = (gameSeconds - startSeconds) / d;
            if (u <= 0.0 || u >= 1.0) return 0f;
            return (float)System.Math.Sin(u * System.Math.PI);
        }

        /// <summary>How far ALONG the jump she is, 0..1, clamped — the term a presenter slides her
        /// forward by so the strip is not played on the spot. Unlike <see cref="Arc01"/> this is
        /// monotonic: she does not travel backwards on the way down.</summary>
        public static float Travel01(double startSeconds, double gameSeconds)
        {
            double d = DurationSeconds;
            if (d <= 0.0) return 0f;
            double u = (gameSeconds - startSeconds) / d;
            if (u <= 0.0) return 0f;
            return u >= 1.0 ? 1f : (float)u;
        }

        /// <summary>Which frame of the strip is showing, clamped to the last frame once the jump is
        /// over (a caller that keeps asking gets the landing pose, never a wrapped-around take-off).</summary>
        public static int FrameAt(double startSeconds, double gameSeconds)
        {
            if (Frames <= 1) return 0;
            double elapsedMs = (gameSeconds - startSeconds) * 1000.0;
            if (elapsedMs <= 0.0) return 0;
            int f = (int)(elapsedMs / FrameMs);
            return f >= Frames ? Frames - 1 : f;
        }

        /// <summary>Is a jump that started at <paramref name="startSeconds"/> still in the air?</summary>
        public static bool InAir(double startSeconds, double gameSeconds)
            => gameSeconds >= startSeconds && gameSeconds - startSeconds < DurationSeconds;

        /// <summary>
        /// WHEN THE NEXT JUMP STARTS, deterministically — the index of the period
        /// <paramref name="elapsedSeconds"/> falls in, and the moment inside it the jump begins.
        ///
        /// <para>Seeded from the caller's own seed (rule 5: no <c>UnityEngine.Random</c> anywhere near
        /// this), so a fight replays the same way twice and a test can state the answer rather than
        /// sample it. The offset is hashed inside the period and always leaves room for the whole jump,
        /// so a jump never straddles two periods and two jumps can never overlap.</para>
        /// </summary>
        /// <returns>False when the species does not jump or the cadence is off.</returns>
        public static bool TryNextJump(int seed, double elapsedSeconds, double periodSeconds,
                                       out double startSeconds)
        {
            startSeconds = 0.0;
            if (periodSeconds <= 0.0 || elapsedSeconds < 0.0) return false;

            double duration = DurationSeconds;
            if (duration >= periodSeconds) { startSeconds = 0.0; return true; }

            long period = (long)System.Math.Floor(elapsedSeconds / periodSeconds);
            uint h = Hash((uint)seed, (uint)period);
            double slack = periodSeconds - duration;
            startSeconds = period * periodSeconds + slack * (h / 4294967296.0);
            return true;
        }

        /// <summary>A small integer hash — the same shape the school sim's streams use, restated here so
        /// Core owns its own determinism and does not reach into a feature module for it.</summary>
        private static uint Hash(uint a, uint b)
        {
            unchecked
            {
                uint h = 2166136261u;
                h = (h ^ a) * 16777619u;
                h = (h ^ b) * 16777619u;
                h ^= h >> 15; h *= 2246822519u;
                h ^= h >> 13; h *= 3266489917u;
                h ^= h >> 16;
                return h;
            }
        }
    }
}
