// ⚠️⚠️ THROWAWAY SPIKE CODE — spike/3d-water. NOT A PIPELINE. NOT FOR MERGE AS-IS. ⚠️⚠️
using System;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.Tools.Spike3dWater
{
    /// <summary>
    /// The DETERMINISTIC sea this spike renders (rule 5: constants in, pixels out — no RNG, no
    /// wall clock). One fixed weather (wind + sea state) through the production derivation
    /// (<see cref="WaveMath.TrainsFrom"/> with <see cref="WaveFieldSettings.Default"/>), sampled
    /// at explicit game times. The "big wave" is FOUND, not authored: a deterministic scan for
    /// the moment/place the four trains constructively pile up inside the viewport.
    /// </summary>
    internal static class SpikeWaveScenario
    {
        /// <summary>Sea state 0..1 — a working sea, well past the calm gate, short of a storm.</summary>
        public const float SeaState = 0.75f;

        /// <summary>Wind ≈ 10.8 m/s toward the lower-left, so crests march toward the camera's
        /// near corner and the "approach" sequence reads naturally.</summary>
        public static readonly Vector2 Wind = new Vector2(-5.4f, -9.33f);

        private const double TwoPi = Math.PI * 2.0;

        /// <summary>The scenario's wave field (pure; same call production makes).</summary>
        public static WaveTrains Trains() =>
            WaveMath.TrainsFrom(Wind, SeaState, WaveFieldSettings.Default);

        /// <summary>
        /// Pack the field FOR A GIVEN GAME TIME into the six shader globals, using the production
        /// packing (<see cref="WaveFieldBridge.Pack"/>) after baking the closed-form travel into
        /// each train's phase offset in DOUBLE (the WaveFieldBridge discipline): the shader then
        /// evaluates theta = k·(dir·pos) + phi with no time uniform, exactly like production.
        /// </summary>
        public static void PackAtTime(in WaveTrains trains, double timeSeconds,
                                      out Vector4 t0, out Vector4 t1, out Vector4 t2, out Vector4 t3,
                                      out Vector4 phases, out Vector4 fieldParams)
        {
            WaveTrains src = trains;   // local copy: an in-parameter cannot be captured by a local function
            WaveTrain P(int i)
            {
                WaveTrain tr = src[i];
                double k = TwoPi / tr.Wavelength;
                double phase = tr.PhaseOffset - k * tr.PhaseSpeed * timeSeconds;
                phase -= Math.Floor(phase / TwoPi) * TwoPi;
                return new WaveTrain(tr.Direction, tr.Wavelength, tr.Amplitude, (float)phase,
                                     WaveFieldSettings.Default.Gravity);
            }

            int n = trains.Count;
            var shifted = new WaveTrains(
                n > 0 ? P(0) : default, n > 1 ? P(1) : default,
                n > 2 ? P(2) : default, n > 3 ? P(3) : default,
                n, trains.CrestSharpening);
            WaveFieldBridge.Pack(in shifted, out t0, out t1, out t2, out t3, out phases, out fieldParams);
        }

        /// <summary>CPU-side surface sample at (pos, t) — the sim reference, byte-for-byte the
        /// maths the shader twin mirrors.</summary>
        public static WaveSample Sample(in WaveTrains trains, Vector2 pos, double t) =>
            WaveMath.Sample(pos, t, in trains);

        /// <summary>
        /// Deterministic scan for the biggest crest that occurs INSIDE <paramref name="rect"/>
        /// during the time window — the "known large wave among normal ones" the readability
        /// experiment needs. Also reports the RMS crest height over the same window so the
        /// verdict can say HOW exceptional the event is.
        /// </summary>
        public static void FindBigWave(in WaveTrains trains, Rect rect,
                                       double tMin, double tMax, double tStep, float posStep,
                                       out double bestT, out Vector2 bestPos, out float bestH,
                                       out float rmsCrest)
        {
            bestT = tMin; bestPos = rect.center; bestH = float.MinValue;
            double sumSq = 0; long nPos = 0;

            for (double t = tMin; t <= tMax; t += tStep)
            {
                float frameMax = float.MinValue;
                Vector2 frameMaxPos = rect.center;
                for (float y = rect.yMin; y <= rect.yMax; y += posStep)
                {
                    for (float x = rect.xMin; x <= rect.xMax; x += posStep)
                    {
                        float h = WaveMath.Sample(new Vector2(x, y), t, in trains).Height;
                        if (h > frameMax) { frameMax = h; frameMaxPos = new Vector2(x, y); }
                    }
                }
                sumSq += (double)frameMax * frameMax;
                nPos++;
                if (frameMax > bestH) { bestH = frameMax; bestPos = frameMaxPos; bestT = t; }
            }
            rmsCrest = nPos > 0 ? (float)Math.Sqrt(sumSq / nPos) : 0f;
        }
    }
}
