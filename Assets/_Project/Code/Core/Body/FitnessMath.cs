using System;

namespace HiddenHarbours.Core
{
    /// <summary>The four kinds of fitness the body keeps (body slice 2).</summary>
    public enum FitnessKind
    {
        /// <summary>Grip and arms: the rod.</summary>
        Grip = 0,
        /// <summary>Back and pull: hauling pots, lines and nets.</summary>
        Back = 1,
        /// <summary>Legs: carrying, lifting from the ground, footwork.</summary>
        Legs = 2,
        /// <summary>Heart and lungs: moving well and cheaply.</summary>
        Cardio = 3,
    }

    /// <summary>
    /// <b>One kind of fitness, as saved</b> (body slice 2, save schema v21). A (level, instant) pair like a
    /// need, plus the remembered peak (muscle memory) and the last time it was kept up. Flat and
    /// JsonUtility-friendly; written only through <see cref="IBodyService"/>.
    /// </summary>
    [Serializable]
    public struct FitnessStateDto
    {
        /// <summary>Level 0..1 at <see cref="SinceGameSeconds"/>.</summary>
        public float Level;

        /// <summary>The highest level ever reached. Never fades: muscle memory.</summary>
        public float Peak;

        /// <summary>The game clock <see cref="Level"/> was true at, or negative (never trained).</summary>
        public double SinceGameSeconds;

        /// <summary>The last time enough training landed to count as keeping it up, or negative (never).</summary>
        public double MaintainedAtGameSeconds;

        /// <summary>Doses since that last maintenance, short of <see cref="FitnessCurve.MaintainDose"/>.</summary>
        public float PendingDose;

        /// <summary>The untrained state: level 0, never anchored, never maintained.</summary>
        public static FitnessStateDto Untrained => new FitnessStateDto
        {
            Level = 0f, Peak = 0f, SinceGameSeconds = -1d, MaintainedAtGameSeconds = -1d, PendingDose = 0f,
        };
    }

    /// <summary>
    /// <b>How fitness grows, holds, fades and comes back</b> (body slice 2, owner ask 2026-11-15: "growing
    /// stronger is harder than maintaining, muscle memory exists, meaning it's much faster to return to
    /// previous strengths"). Pure functions of the saved state, the clock and the curve (rule 5).
    ///
    /// <list type="bullet">
    /// <item><b>Gain</b>, with diminishing returns: <c>level' = 1 - (1 - level) x e^(-k x dose)</c>.</item>
    /// <item><b>Memory</b>: below the remembered peak <c>k</c> is multiplied by the regain factor, up to the
    /// peak exactly, then the rest of the dose trains at the first-time rate.</item>
    /// <item><b>Hold</b>: a maintaining dose holds the level for the grace days.</item>
    /// <item><b>Fade</b>: after the grace with no maintaining dose the level falls in a straight line at the
    /// decay per day, never below 0. The peak never fades.</item>
    /// </list>
    /// Reading the level is a straight line from the anchor, so a long skip and many short steps agree.
    /// </summary>
    public static class FitnessMath
    {
        /// <summary>The level now: the anchor's level less the fade since the later of the anchor and the
        /// end of the grace, never below 0.</summary>
        public static float LevelAt(in FitnessStateDto state, in FitnessCurve curve, double now, double secondsPerGameDay)
        {
            if (state.SinceGameSeconds < 0d || !(state.Level > 0f)) return state.Level > 0f ? state.Level : 0f;
            double fadeFrom = FadeStart(state, curve, secondsPerGameDay);
            if (now <= fadeFrom || !(curve.DecayPerGameDay > 0f) || !(secondsPerGameDay > 0d)) return state.Level;
            double level = state.Level - curve.DecayPerGameDay * ((now - fadeFrom) / secondsPerGameDay);
            return level > 0d ? (float)level : 0f;
        }

        /// <summary>
        /// <b>Train</b> with <paramref name="dose"/> at <paramref name="now"/>: settle the fade, grow the level
        /// (memory-boosted up to the peak), raise the peak, count the dose toward maintenance. A non-positive
        /// dose changes nothing.
        /// </summary>
        public static void Train(ref FitnessStateDto state, float dose, double now, in FitnessCurve curve,
                                 float memoryRegainFactor, double secondsPerGameDay)
        {
            if (!(dose > 0f)) return;
            float level = LevelAt(state, curve, now, secondsPerGameDay);
            state.Level = Grow(level, state.Peak, dose, curve.GainPerDose, memoryRegainFactor);
            if (state.Level > state.Peak) state.Peak = state.Level;
            state.SinceGameSeconds = now;

            state.PendingDose += dose;
            if (state.PendingDose >= curve.MaintainDose)
            {
                state.PendingDose = 0f;
                state.MaintainedAtGameSeconds = now;
            }
        }

        /// <summary>The level after <paramref name="dose"/>: at <c>k x regain</c> up to the peak, at <c>k</c>
        /// beyond it. Monotonic in the dose, never above 1.</summary>
        public static float Grow(float level, float peak, float dose, float gainPerDose, float memoryRegainFactor)
        {
            double l = Clamp01(level);
            double k = gainPerDose > 0f ? gainPerDose : 0d;
            double s = dose > 0f ? dose : 0d;
            if (k <= 0d || s <= 0d) return (float)l;
            double regain = memoryRegainFactor > 1f ? memoryRegainFactor : 1d;
            double p = Clamp01(peak);

            if (l < p && regain > 1d)
            {
                double fast = k * regain;
                if (p >= 1d) return (float)(1d - (1d - l) * Math.Exp(-fast * s));
                double toPeak = Math.Log((1d - l) / (1d - p)) / fast;   // the dose that reaches the peak
                if (s <= toPeak) return (float)(1d - (1d - l) * Math.Exp(-fast * s));
                l = p;
                s -= toPeak;
            }
            return (float)(1d - (1d - l) * Math.Exp(-k * s));
        }

        /// <summary>The doses that take <paramref name="from"/> to <paramref name="to"/> at the first-time
        /// rate (no memory): <c>ln((1 - from) / (1 - to)) / k</c>.</summary>
        public static double DosesToGain(float from, float to, float gainPerDose)
        {
            if (!(gainPerDose > 0f) || to <= from) return 0d;
            if (to >= 1f) return double.PositiveInfinity;
            return Math.Log((1d - Clamp01(from)) / (1d - to)) / gainPerDose;
        }

        /// <summary>At a save write: carry the level to <paramref name="now"/> and re-anchor it there (the
        /// grace still running is kept). Values never change.</summary>
        public static void Settle(ref FitnessStateDto state, in FitnessCurve curve, double now, double secondsPerGameDay)
        {
            if (state.SinceGameSeconds < 0d || now < state.SinceGameSeconds) return;
            state.Level = LevelAt(state, curve, now, secondsPerGameDay);
            state.SinceGameSeconds = now;
        }

        /// <summary>At a clock seek (a restore): keep the level and the grace left, anchored at
        /// <paramref name="now"/>.</summary>
        public static void Rebase(ref FitnessStateDto state, double now)
        {
            if (state.SinceGameSeconds < 0d || now < 0d) return;
            double delta = now - state.SinceGameSeconds;
            state.SinceGameSeconds = now;
            if (state.MaintainedAtGameSeconds >= 0d) state.MaintainedAtGameSeconds += delta;
        }

        private static double FadeStart(in FitnessStateDto state, in FitnessCurve curve, double secondsPerGameDay)
        {
            double since = state.SinceGameSeconds;
            if (state.MaintainedAtGameSeconds < 0d) return since;
            double graceEnd = state.MaintainedAtGameSeconds + Math.Max(0d, curve.GraceGameDays) * secondsPerGameDay;
            return graceEnd > since ? graceEnd : since;
        }

        private static double Clamp01(double v) => v < 0d ? 0d : v > 1d ? 1d : v;
    }
}
