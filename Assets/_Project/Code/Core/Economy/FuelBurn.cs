using System;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// <b>What an engine drinks, and how much of it a stretch of running costs</b> — canon's
    /// <c>burn = f(throttle, engineLoad, seaState)</c> (<c>boats-and-navigation.md</c> §2.6) as one pure
    /// static function. <c>docs/design/fuel-and-refuelling.md</c> §9.5.
    ///
    /// <para><b>The whole model:</b></para>
    /// <code>
    /// litresPerHour = R × throttleTerm × loadTerm × seaTerm × BurnScale
    ///
    ///   throttleTerm = IdleFraction + (1 − IdleFraction) · |throttle| ^ ThrottleExponent
    ///   engineLoad01 = clamp01( HoldLoadWeight · holdFraction + SlipLoadWeight · slip01 )
    ///       loadTerm = 1 + LoadSurcharge · engineLoad01
    ///        seaTerm = 1 + SeaSurcharge  · seaState01
    /// </code>
    ///
    /// <para><b>R is the hull's, everything else is the fleet's.</b> <c>R</c> is
    /// <c>BoatHullDef.FullThrottleLitresPerHour</c> — one number per boat. Every other factor is a
    /// dimensionless multiplier out of <see cref="FuelSettings"/>, so one hull's thirst is one Def field
    /// the owner can move and the SHAPE of the curve is shared (rule 6).</para>
    ///
    /// <para><b>Pure, in the shape of <c>FuelPricing.Quote</c>.</b> No Unity types, no randomness, no
    /// clock, no state — the same inputs give the same litres forever, which is what rule 5 asks of
    /// anything the sim touches and what makes every number below EditMode-testable. Deliberately uses
    /// <see cref="Math"/> and not <c>Mathf</c>: this file has no reason to know Unity exists.</para>
    ///
    /// <para><b>⚠ Slip is one term for every cause, and that is the elegant part.</b> A head sea, a foul
    /// tide, a fouled bottom, a towed load and a dragging anchor all produce the SAME physical signature
    /// — she is not making the speed her throttle asked for — so one term prices all of them without
    /// enumerating any. It ships at weight 0 until a MEASURED per-hull top speed exists to compute it
    /// from (<see cref="FuelSettings.SlipLoadWeight"/>); at 0 the formula reduces exactly to the simple
    /// version, with no branch.</para>
    /// </summary>
    public static class FuelBurn
    {
        /// <summary>
        /// Seconds in an hour. A UNIT, not a tunable — the sibling of <c>FuelPricing.MinLitres</c>, and
        /// the only bare number in this file.
        ///
        /// <para><b>⚠ THIS IS DELIBERATELY NOT <c>GameConfig.SecondsPerHour</c>, AND THE DIFFERENCE IS A
        /// BUG WAITING TO BE WRITTEN.</b> That property is a GAME hour, derived from the owner-tunable
        /// <c>SecondsPerDay</c> (1,800 today — a 30-minute game day). Bill fuel per game hour and
        /// <b>doubling the day length silently halves the fuel cost of every crossing in the game</b>: no
        /// owner tuning tide pacing would expect to re-price fuel, and nothing would report it. The boat
        /// moves at world metres per second in real time, so fuel is metered against REAL seconds of the
        /// engine actually turning, and litres-per-kilometre stays fixed no matter what the clock does.
        /// <c>FuelBurnTests</c> is the guard.</para>
        /// </summary>
        public const float SecondsPerHour = 3600f;

        /// <summary>
        /// The rate, in litres per hour, at one instant of one duty.
        /// </summary>
        /// <param name="fullThrottleLitresPerHour">The hull's <c>R</c> — her burn flat out, light, in a
        /// glass sea. 0 (no tank / no engine authored) answers 0, so an un-enrolled hull is inert.</param>
        /// <param name="throttle">The helm's throttle, −1..+1. The ABSOLUTE value is used: backing down
        /// burns fuel too, and astern is weaker thrust for the same gulp, which is fair.</param>
        /// <param name="holdFraction">Catch aboard, used/capacity, 0..1. A full hold is a heavy boat.</param>
        /// <param name="slip01">How badly she is failing to make the speed her throttle asked for, 0..1.
        /// See <see cref="SlipFraction"/>; pass 0 while <see cref="FuelSettings.SlipLoadWeight"/> is 0.</param>
        /// <param name="seaState01">The CONTINUOUS sea axis (<c>EnvironmentSample.SeaState01</c>), 0..1 —
        /// never the stepped enum, which would make fuel cost lurch at a band edge.</param>
        /// <param name="cfg">The fleet-wide curve.</param>
        public static float LitresPerHour(float fullThrottleLitresPerHour, float throttle,
                                          float holdFraction, float slip01, float seaState01,
                                          in FuelSettings cfg)
        {
            float r = fullThrottleLitresPerHour;
            if (!(r > 0f)) return 0f;          // no tank, no engine, or a NaN Def field: she burns nothing

            float idle = Clamp01(cfg.IdleFraction);
            float t = Clamp01(Abs(throttle));
            float throttleTerm = idle + (1f - idle) * Pow01(t, cfg.ThrottleExponent);

            float engineLoad01 = Clamp01(Clamp01(cfg.HoldLoadWeight) * Clamp01(holdFraction) +
                                         Clamp01(cfg.SlipLoadWeight) * Clamp01(slip01));
            float loadTerm = 1f + AtLeastZero(cfg.LoadSurcharge) * engineLoad01;

            float seaTerm = 1f + AtLeastZero(cfg.SeaSurcharge) * Clamp01(seaState01);

            return r * throttleTerm * loadTerm * seaTerm * AtLeastZero(cfg.BurnScale);
        }

        /// <summary>
        /// Litres consumed by holding <paramref name="litresPerHour"/> for
        /// <paramref name="deltaSeconds"/> of REAL, engine-running time.
        ///
        /// <para><b>⚠ <paramref name="deltaSeconds"/> must be the FIXED step
        /// (<c>Time.fixedDeltaTime</c>), never a frame delta.</b> A frame-rate spike must not change how
        /// much fuel a crossing costs. That is the one implementation detail that can silently break
        /// determinism, it is cheap to get right, and it is invisible when wrong —
        /// <c>FuelBurnTests</c> is the guard.</para>
        /// </summary>
        public static float LitresBurned(float litresPerHour, float deltaSeconds)
        {
            if (!(litresPerHour > 0f) || !(deltaSeconds > 0f)) return 0f;
            return litresPerHour * (deltaSeconds / SecondsPerHour);
        }

        /// <summary>
        /// One fixed tick of one duty, in litres — <see cref="LitresPerHour"/> and
        /// <see cref="LitresBurned"/> composed, so a caller integrating on the fixed tick has one call to
        /// make and one place for the two halves to agree.
        /// </summary>
        public static float LitresBurnedOverTick(float fullThrottleLitresPerHour, float throttle,
                                                 float holdFraction, float slip01, float seaState01,
                                                 float deltaSeconds, in FuelSettings cfg)
        {
            return LitresBurned(
                LitresPerHour(fullThrottleLitresPerHour, throttle, holdFraction, slip01, seaState01, cfg),
                deltaSeconds);
        }

        /// <summary>
        /// How badly she is failing to make the speed her throttle asked for, 0..1:
        /// <c>clamp01( 1 − wayThroughWater / (|throttle| · measuredTopSpeedMps) )</c>.
        ///
        /// <para><b>⚠ <paramref name="measuredTopSpeedMps"/> must be MEASURED, never derived from the
        /// hull stats.</b> The fleet tests warn about exactly this mistake in exactly this repo —
        /// <c>BowSprayGrading</c>'s remarks record a derived figure that was "wrong on both counts", and
        /// the lobster boat derives 4.35 m/s against a measured 4.24. Until
        /// <c>BoatHullDef.MeasuredTopSpeedMps</c> exists and has been measured on the fleet harness, this
        /// stays unused and <see cref="FuelSettings.SlipLoadWeight"/> stays 0 (gameplay-systems' lane,
        /// §9.15 call 11). It is written now so the far side of that seam is unambiguous.</para>
        ///
        /// <para>Answers 0 — no slip — whenever the question is meaningless: at rest, at zero throttle, or
        /// with no reference speed authored. That is the forgiving direction: an un-measured hull is never
        /// charged a slip surcharge it cannot justify.</para>
        /// </summary>
        public static float SlipFraction(float wayThroughWaterMps, float throttle, float measuredTopSpeedMps)
        {
            float asked = Abs(throttle) * measuredTopSpeedMps;
            if (!(asked > 0f)) return 0f;
            return Clamp01(1f - Abs(wayThroughWaterMps) / asked);
        }

        // ---- small helpers, so the formula above reads as the formula in the doc ------------------

        private static float Abs(float v) => v < 0f ? -v : v;

        /// <summary>Clamp to 0..1, and map NaN to 0 rather than letting it poison a tank level. The
        /// comparison order matters: <c>NaN &gt; 0f</c> is false, so NaN falls out as 0.</summary>
        private static float Clamp01(float v)
        {
            if (!(v > 0f)) return 0f;
            return v > 1f ? 1f : v;
        }

        private static float AtLeastZero(float v) => v > 0f ? v : 0f;

        /// <summary>
        /// <c>t^exponent</c> for t already in 0..1. Special-cased at the ends because
        /// <see cref="Math.Pow"/> is the one call here that can answer NaN/∞ from an odd Def value, and a
        /// NaN reaching a saved tank level is unrecoverable in a way a wrong number is not.
        /// </summary>
        private static float Pow01(float t, float exponent)
        {
            if (t <= 0f) return 0f;
            if (t >= 1f) return 1f;
            if (!(exponent > 0f)) return 1f;                    // a zero/NaN exponent means "no curve"
            double p = Math.Pow(t, exponent);
            if (double.IsNaN(p) || double.IsInfinity(p)) return t;
            return (float)p;
        }
    }
}
