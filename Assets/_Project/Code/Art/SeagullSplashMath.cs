using System;

namespace HiddenHarbours.Art
{
    /// <summary>
    /// THE TWO ARITHMETIC FACTS BEHIND A SPLASH — which frame of the arrival the water actually breaks
    /// on, and whether the bird came up with anything.
    ///
    /// <para><b>Engine-free and static on purpose.</b> Both answers are pure functions of numbers the
    /// caller already has, so an EditMode test can pin them without a scene, a camera or a bird, and the
    /// PlayMode integration can assert the SAME frame number the sim fires on rather than a frame it
    /// guessed. Nothing here allocates, nothing here remembers.</para>
    ///
    /// <para><b>No <c>UnityEngine.Random</c>, ever</b> (rule 5). The catch roll is a closed-form hash of
    /// <c>(seed, where, when)</c> — the same flock seed, the same splash, the same answer, on every run
    /// and every machine. A gull that came up with a fish on one playthrough comes up with a fish on the
    /// next, which is what makes the flock reproducible at all.</para>
    /// </summary>
    public static class SeagullSplashMath
    {
        /// <summary>
        /// The frame the burst fires on, clamped into the row that is actually playing.
        ///
        /// <para>The rig states it (<c>water.splash_burst_frame</c>, frame 2 in the drop) and
        /// <c>SeagullRigKitTests.TheWaterSectionNamesTheFrameTheSplashFiresOn</c> pins it. The clamp is
        /// for the day a shorter arrival anim is baked: a burst frame past the end would never arrive and
        /// the signal would silently stop firing, so it fires on the last frame instead. A row with one
        /// frame bursts on frame 0.</para>
        /// </summary>
        public static int BurstFrame(int splashBurstFrame, int frameCount)
        {
            if (frameCount <= 1) return 0;
            int f = splashBurstFrame < 0 ? 0 : splashBurstFrame;
            int last = frameCount - 1;
            return f > last ? last : f;
        }

        /// <summary>
        /// A deterministic roll in <c>[0,1)</c> from the flock seed, the place and the moment.
        ///
        /// <para>Position is quantised to the centimetre and time to the millisecond before hashing, so
        /// the answer is stable against the last bits of a double that a different tick order might round
        /// differently. FNV-1a over the eight bytes, then the low 24 bits over 2^24 — plenty of grain for
        /// a probability and no floating-point in the hash itself.</para>
        /// </summary>
        public static float Roll01(int seed, double x, double y, double milliseconds)
        {
            unchecked
            {
                uint h = 2166136261u;
                h = Mix(h, (uint)seed);
                h = Mix(h, (uint)(long)Math.Round(x * 100.0));
                h = Mix(h, (uint)(long)Math.Round(y * 100.0));
                h = Mix(h, (uint)(long)Math.Round(milliseconds));
                return (h & 0x00FFFFFFu) / 16777216f;
            }
        }

        /// <summary>
        /// Did she come up with a fish? The drop's dive yield (p 0.35) applied to one strike.
        ///
        /// <para>A probability of 0 never catches and a probability of 1 always does, without consulting
        /// the hash — so an owner who turns the yield off turns it off completely rather than leaving a
        /// one-in-a-billion bird.</para>
        /// </summary>
        public static bool RollsCatch(int seed, double x, double y, double milliseconds, float probability)
        {
            if (!(probability > 0f)) return false;
            if (probability >= 1f) return true;
            return Roll01(seed, x, y, milliseconds) < probability;
        }

        private static uint Mix(uint h, uint v)
        {
            unchecked
            {
                for (int i = 0; i < 4; i++)
                {
                    h ^= (v >> (i * 8)) & 0xFFu;
                    h *= 16777619u;
                }
                return h;
            }
        }
    }
}
