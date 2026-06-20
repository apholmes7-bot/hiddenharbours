using System;

namespace HiddenHarbours.UI
{
    /// <summary>
    /// A glanceable summary of the tide for the HUD: is it making or ebbing, how high it is now,
    /// and how long until the next turn (high or low water). (VS-17.)
    /// </summary>
    public readonly struct TideState
    {
        /// <summary>True if the water is rising (making) at <see cref="HeightMeters"/>.</summary>
        public readonly bool Rising;

        /// <summary>Current water level, metres relative to chart datum.</summary>
        public readonly float HeightMeters;

        /// <summary>In-game seconds until the next high/low water (the next turn of the tide).
        /// Negative if no turn was found within the search horizon (treated as "unknown").</summary>
        public readonly double SecondsToTurn;

        public TideState(bool rising, float heightMeters, double secondsToTurn)
        {
            Rising = rising;
            HeightMeters = heightMeters;
            SecondsToTurn = secondsToTurn;
        }

        /// <summary>True when a turn was located within the horizon.</summary>
        public bool HasTurn => SecondsToTurn >= 0.0;
    }

    /// <summary>
    /// Pure, Unity-light tide derivation for the HUD. No Core interface exposes tide *state*
    /// (rising/falling) or *time-to-turn*, so the HUD derives them here from the height function
    /// the environment service already provides (<c>IEnvironmentService.TideHeightAt</c>).
    ///
    /// Kept allocation-free and engine-free so it is trivially EditMode-testable against a
    /// synthetic sine with analytically-known turning points. Mirrors
    /// <c>HiddenHarbours.Environment.TideModel.IsRising</c> for the rising test (a small forward
    /// finite-difference), and scans forward up to one tidal period to locate the next turn by the
    /// sign-flip of the local derivative.
    /// </summary>
    public static class TideReadout
    {
        /// <summary>
        /// Derive the tide state at <paramref name="nowSeconds"/>.
        /// </summary>
        /// <param name="heightAt">Water level (m) as a function of in-game seconds. In the HUD this
        /// is <c>GameServices.Environment.TideHeightAt</c>.</param>
        /// <param name="nowSeconds">Current in-game seconds (the master clock value).</param>
        /// <param name="risingDt">Forward finite-difference step (in-game seconds) used to decide
        /// rising-vs-falling and to detect the derivative sign-flip. Mirror TideModel:
        /// <c>SecondsPerHour * 0.05</c> (~3 in-game minutes). Must be &gt; 0.</param>
        /// <param name="scanStepSeconds">Forward scan granularity (in-game seconds) for locating the
        /// next turn. Smaller = more precise but more samples. Must be &gt; 0.</param>
        /// <param name="horizonSeconds">How far forward to look for the next turn (in-game seconds).
        /// One tidal period (~12.42 in-game h) is enough — a turn always occurs within half a period.</param>
        public static TideState Derive(Func<double, float> heightAt, double nowSeconds,
                                       double risingDt, double scanStepSeconds, double horizonSeconds)
        {
            if (heightAt == null) throw new ArgumentNullException(nameof(heightAt));
            if (risingDt <= 0.0) throw new ArgumentOutOfRangeException(nameof(risingDt));
            if (scanStepSeconds <= 0.0) throw new ArgumentOutOfRangeException(nameof(scanStepSeconds));

            float hereHeight = heightAt(nowSeconds);

            // Rising/falling: the sign of the local forward derivative (mirrors TideModel.IsRising).
            bool rising = heightAt(nowSeconds + risingDt) > hereHeight;

            // Find the next turn: the first place the derivative flips sign relative to now.
            // We sample the signed slope across small steps and stop when its sign changes.
            double secondsToTurn = ScanForTurn(heightAt, nowSeconds, rising, scanStepSeconds, horizonSeconds, risingDt);

            return new TideState(rising, hereHeight, secondsToTurn);
        }

        private static double ScanForTurn(Func<double, float> heightAt, double nowSeconds, bool risingNow,
                                          double scanStepSeconds, double horizonSeconds, double slopeDt)
        {
            // Walk forward; at each sample test the local slope sign. The first sample whose slope
            // sign disagrees with "now" brackets the turn. We then bisect for a tighter estimate.
            double prevT = nowSeconds;
            for (double t = nowSeconds + scanStepSeconds; t <= nowSeconds + horizonSeconds; t += scanStepSeconds)
            {
                bool slopeRising = LocalRising(heightAt, t, slopeDt);
                if (slopeRising != risingNow)
                {
                    // Turn lies in (prevT, t]. Bisect to refine.
                    double turnT = BisectTurn(heightAt, prevT, t, risingNow, slopeDt);
                    return turnT - nowSeconds;
                }
                prevT = t;
            }
            return -1.0; // no turn within the horizon
        }

        private static double BisectTurn(Func<double, float> heightAt, double lo, double hi,
                                         bool risingNow, double slopeDt)
        {
            // lo keeps the "now" slope sign; hi has flipped. Narrow the bracket.
            for (int i = 0; i < 24; i++) // ~24 halvings is far below float precision limits
            {
                double mid = 0.5 * (lo + hi);
                bool midRising = LocalRising(heightAt, mid, slopeDt);
                if (midRising == risingNow) lo = mid;
                else hi = mid;
            }
            return 0.5 * (lo + hi);
        }

        /// <summary>Local forward-difference slope sign at <paramref name="t"/>.</summary>
        private static bool LocalRising(Func<double, float> heightAt, double t, double slopeDt)
            => heightAt(t + slopeDt) > heightAt(t);
    }
}
