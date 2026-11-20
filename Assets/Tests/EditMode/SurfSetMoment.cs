using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>A shot that asks "does the surf draw" waits for a set (see <see cref="AtASet"/>).</summary>
    internal static class SurfSetMoment
    {
        /// <summary>Half a dominant period between scanned moments.</summary>
        const float ScanStepPeriods = 0.5f;
        /// <summary>Moments scanned: 32 periods, longer than any set group the trains beat into.</summary>
        const int ScanMoments = 64;
        /// <summary>A later moment must beat the best birth by more than this to win (float noise).</summary>
        const float BirthTieEpsilon = 1e-4f;

        /// <summary>
        /// The sea at the first moment a SET arrives, travel baked into every train's phase the way
        /// WaveFieldAnimator hands the bridge. On the refracted bore clock (BreakerSettings.BoreRefraction) the
        /// whole beach shares one birth, read at the clock point, so at an arbitrary moment the eight-train sea
        /// can sit in a lull across the entire frame. A shot that asks "does the surf draw" waits for the set;
        /// the lull is a real state of the sea, not a missing drop. Deterministic: the first moment in the scan
        /// window whose birth reaches the scan's own best.
        /// </summary>
        internal static WaveTrains AtASet(WaveTrains now)
        {
            if (now.Count == 0) return now;
            BreakerSettings settings = GameServices.Breakers;
            WaveTrain dominant = now.Dominant;
            float period = BreakerMath.PeriodSeconds(dominant);
            if (!(period > 0f)) return now;
            Vector2 clock = BreakerMath.BoreClockPoint(Vector2.zero, in settings);
            float bestT = 0f, best = -1f;
            for (int i = 0; i < ScanMoments; i++)
            {
                float t = period * i * ScanStepPeriods;
                float phase = BreakerMath.BoreClockPhaseDegrees(in dominant, Vector2.zero, 0f, in settings, 1f, t);
                float birth = BreakerMath.BoreBirthEnergy01(in now, clock, 0f, phase, period, 1f,
                                                            settings.BoreSetStrength, 1f, t);
                if (birth > best + BirthTieEpsilon) { best = birth; bestT = t; }
            }
            float gravity = GameServices.WaveField.Gravity;
            var advanced = new WaveTrain[WaveTrains.MaxTrains];
            for (int i = 0; i < now.Count; i++)
            {
                WaveTrain tr = now[i];
                float k = 2f * Mathf.PI / Mathf.Max(tr.Wavelength, WaveTrain.MinWavelengthMeters);
                advanced[i] = new WaveTrain(tr.Direction, tr.Wavelength, tr.Amplitude,
                                            tr.PhaseOffset - k * tr.PhaseSpeed * bestT, gravity);
            }
            return WaveTrains.From(advanced, now.Count, now.CrestSharpening, now.DominantIndex);
        }
    }
}
