using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The shared deterministic wave field (ADR 0018) — the Core math BOTH the seakeeping sim and
    /// the water shader's HLSL twin read, so the boat rocks on what the player sees by construction.
    /// All pure and headless. These tests pin the model's owner-ruled requirements (dispersion is
    /// canon; glass calm is sacred), the rule-5 determinism, the analytic-slope contract, and a
    /// small grid of exact values — the parity reference a future HLSL-twin review diffs against
    /// (change WaveMath ⇒ these numbers move ⇒ the twin must move in the same PR).
    /// </summary>
    public class WaveMathTests
    {
        // A representative sweep: winds (incl. dead calm), sea states, positions, times.
        private static readonly Vector2[] Winds =
            { new Vector2(0f, 0f), new Vector2(3f, 1f), new Vector2(-6f, 4f), new Vector2(0f, -11f) };
        private static readonly float[] SeaStates = { 0f, 0.25f, 0.6f, 1f };
        private static readonly float[] GridCoords = { -40f, -15f, 0f, 12.5f, 37.25f };
        private static readonly double[] Times = { 0.0, 47.3, 12345.75, 1000000.5 };

        // ---- Determinism (rule 5): same inputs → identical trains and samples, always ------------

        [Test]
        public void SameInputs_YieldIdenticalTrainsAndSamples_AcrossTheSweep()
        {
            var settings = WaveFieldSettings.Default;
            foreach (var wind in Winds)
            foreach (var sea in SeaStates)
            {
                var trainsA = WaveMath.TrainsFrom(wind, sea, in settings);
                var trainsB = WaveMath.TrainsFrom(wind, sea, in settings);
                Assert.AreEqual(trainsA.Count, trainsB.Count, "train count must be reproducible");
                for (int i = 0; i < trainsA.Count; i++)
                {
                    Assert.AreEqual(trainsA[i].Direction.x, trainsB[i].Direction.x, "direction.x bit-stable");
                    Assert.AreEqual(trainsA[i].Direction.y, trainsB[i].Direction.y, "direction.y bit-stable");
                    Assert.AreEqual(trainsA[i].Wavelength, trainsB[i].Wavelength, "wavelength bit-stable");
                    Assert.AreEqual(trainsA[i].Amplitude, trainsB[i].Amplitude, "amplitude bit-stable");
                    Assert.AreEqual(trainsA[i].PhaseSpeed, trainsB[i].PhaseSpeed, "phase speed bit-stable");
                    Assert.AreEqual(trainsA[i].PhaseOffset, trainsB[i].PhaseOffset, "phase offset bit-stable");
                }

                foreach (var x in GridCoords)
                foreach (var t in Times)
                {
                    var pos = new Vector2(x, -x * 0.4f);
                    WaveSample a = WaveMath.Sample(pos, t, in trainsA);
                    WaveSample b = WaveMath.Sample(pos, t, in trainsB);
                    Assert.AreEqual(a.Height, b.Height, $"height must be identical at ({pos.x},{pos.y},t={t})");
                    Assert.AreEqual(a.Slope.x, b.Slope.x, "slope.x must be identical");
                    Assert.AreEqual(a.Slope.y, b.Slope.y, "slope.y must be identical");
                    Assert.AreEqual(a.CrestFactor, b.CrestFactor, "crest factor must be identical");
                }
            }
        }

        // ---- Glass calm is SACRED (owner ruling): sea state 0 → exactly nothing, everywhere ------

        [Test]
        public void SeaStateZero_IsExactlyFlatGlass_Everywhere()
        {
            var settings = WaveFieldSettings.Default;
            foreach (var wind in Winds)
            {
                var trains = WaveMath.TrainsFrom(wind, 0f, in settings);
                for (int i = 0; i < trains.Count; i++)
                    Assert.AreEqual(0f, trains[i].Amplitude, "no minimum swell, no floor — amplitudes are exactly 0");

                foreach (var x in GridCoords)
                foreach (var y in GridCoords)
                foreach (var t in Times)
                {
                    WaveSample s = WaveMath.Sample(new Vector2(x, y), t, in trains);
                    Assert.AreEqual(0f, s.Height, "glass means glass: height exactly 0");
                    Assert.AreEqual(0f, s.Slope.x, "slope.x exactly 0");
                    Assert.AreEqual(0f, s.Slope.y, "slope.y exactly 0");
                    Assert.AreEqual(0f, s.CrestFactor, "crest factor exactly 0 — nothing for foam to ride");
                }
            }
        }

        // ---- Amplitude grows monotonically with the sea-state axis -------------------------------

        [Test]
        public void MaxSampledHeight_IsNonDecreasingInSeaState()
        {
            var settings = WaveFieldSettings.Default;
            var wind = new Vector2(5f, 2f);
            float previousMax = -1f;
            for (float sea = 0f; sea <= 1.001f; sea += 0.1f)
            {
                var trains = WaveMath.TrainsFrom(wind, sea, in settings);
                float maxAbs = 0f;
                for (float x = -30f; x <= 30f; x += 5f)
                for (float y = -30f; y <= 30f; y += 5f)
                foreach (var t in Times)
                {
                    float h = Mathf.Abs(WaveMath.Sample(new Vector2(x, y), t, in trains).Height);
                    if (h > maxAbs) maxAbs = h;
                }
                Assert.GreaterOrEqual(maxAbs, previousMax - 1e-6f,
                    $"a rougher sea (SeaState01={sea:0.0}) must never sample flatter than a calmer one");
                previousMax = maxAbs;
            }
        }

        // ---- Dispersion is CANON (owner ruling): c = √(g·λ/2π), longer waves strictly faster -----

        [Test]
        public void PhaseSpeed_DerivesFromWavelength_ByDeepWaterDispersion()
        {
            var settings = WaveFieldSettings.Default;
            var trains = WaveMath.TrainsFrom(new Vector2(7f, 0f), 0.8f, in settings);
            Assert.AreEqual(4, trains.Count, "default field is 1 primary + 3 secondaries");

            for (int i = 0; i < trains.Count; i++)
            {
                float expected = Mathf.Sqrt(settings.Gravity * trains[i].Wavelength / (2f * Mathf.PI));
                Assert.AreEqual(expected, trains[i].PhaseSpeed, 1e-4f,
                    $"train {i}: speed must be √(g·λ/2π) of its own wavelength — never independent");
            }

            for (int i = 0; i < trains.Count; i++)
            for (int j = 0; j < trains.Count; j++)
            {
                if (i == j) continue;
                float speedRatioSquared = (trains[i].PhaseSpeed / trains[j].PhaseSpeed)
                                        * (trains[i].PhaseSpeed / trains[j].PhaseSpeed);
                float wavelengthRatio = trains[i].Wavelength / trains[j].Wavelength;
                Assert.AreEqual(wavelengthRatio, speedRatioSquared, 1e-3f * wavelengthRatio,
                    $"trains {i}/{j}: (ci/cj)² must equal λi/λj (c ∝ √λ)");
                if (trains[i].Wavelength > trains[j].Wavelength)
                    Assert.Greater(trains[i].PhaseSpeed, trains[j].PhaseSpeed,
                        $"trains {i}/{j}: the longer swell must strictly outrun the shorter chop");
            }
        }

        // ---- The slope is the ANALYTIC derivative of the height ----------------------------------

        [Test]
        public void Slope_AgreesWithFiniteDifferenceOfHeight()
        {
            var settings = WaveFieldSettings.Default;
            var trains = WaveMath.TrainsFrom(new Vector2(5f, 2f), 0.7f, in settings);
            const float h = 0.02f;       // central-difference step (m)
            const float tolerance = 2e-3f; // O(h²) truncation + float noise, well under a visible tilt

            foreach (var x in GridCoords)
            foreach (var y in GridCoords)
            foreach (var t in Times)
            {
                var pos = new Vector2(x, y);
                WaveSample s = WaveMath.Sample(pos, t, in trains);
                float fdX = (WaveMath.Sample(new Vector2(x + h, y), t, in trains).Height
                           - WaveMath.Sample(new Vector2(x - h, y), t, in trains).Height) / (2f * h);
                float fdY = (WaveMath.Sample(new Vector2(x, y + h), t, in trains).Height
                           - WaveMath.Sample(new Vector2(x, y - h), t, in trains).Height) / (2f * h);
                Assert.AreEqual(fdX, s.Slope.x, tolerance, $"∂H/∂x at ({x},{y},t={t})");
                Assert.AreEqual(fdY, s.Slope.y, tolerance, $"∂H/∂y at ({x},{y},t={t})");
            }
        }

        // ---- CrestFactor: always in [0,1], concentrated on crests, zero in troughs ----------------

        [Test]
        public void CrestFactor_StaysInUnitRange_AcrossTheSweep()
        {
            var settings = WaveFieldSettings.Default;
            foreach (var wind in Winds)
            foreach (var sea in SeaStates)
            {
                var trains = WaveMath.TrainsFrom(wind, sea, in settings);
                foreach (var x in GridCoords)
                foreach (var y in GridCoords)
                foreach (var t in Times)
                {
                    float crest = WaveMath.Sample(new Vector2(x, y), t, in trains).CrestFactor;
                    Assert.GreaterOrEqual(crest, 0f, "crest factor must never go negative");
                    Assert.LessOrEqual(crest, 1f, "crest factor must never exceed 1");
                }
            }
        }

        [Test]
        public void CrestFactor_PeaksOnTheCrest_AndIsZeroInTheTrough()
        {
            // One hand-built train travelling +X: λ = 10 m, A = 0.5 m, phase 0, so at t = 0 the
            // crest sits at x = λ/4 (sin(kx) = 1) and the trough at x = 3λ/4.
            var train = new WaveTrain(new Vector2(1f, 0f), 10f, 0.5f, 0f, 9.81f);
            var trains = new WaveTrains(train, default, default, default, 1, 2.2f);

            WaveSample crest = WaveMath.Sample(new Vector2(2.5f, 0f), 0.0, in trains);
            WaveSample trough = WaveMath.Sample(new Vector2(7.5f, 0f), 0.0, in trains);

            Assert.AreEqual(0.5f, crest.Height, 1e-4f, "the crest reaches +A");
            Assert.AreEqual(-0.5f, trough.Height, 1e-4f, "the trough bottoms at -A");
            Assert.AreEqual(1f, crest.CrestFactor, 1e-3f, "crest factor saturates at the crest tip");
            Assert.AreEqual(0f, trough.CrestFactor, "and is exactly 0 in the trough — foam rides crests");
            Assert.Greater(crest.CrestFactor, trough.CrestFactor, "higher water ⇒ higher crest factor");
        }

        // ---- The wave ADVANCES at its phase speed (it travels, it doesn't shimmer in place) -------

        [Test]
        public void SingleTrain_HeightTravels_AtThePhaseSpeed()
        {
            var train = new WaveTrain(new Vector2(0.6f, 0.8f), 12f, 0.4f, 1.234f, 9.81f);
            var trains = new WaveTrains(train, default, default, default, 1, 2f);
            float c = train.PhaseSpeed;

            foreach (var dt in new[] { 0.25f, 1.7f, 6f })
            foreach (var x in GridCoords)
            foreach (var t in new[] { 0.0, 47.3, 12345.75 })
            {
                var pos = new Vector2(x, x * 0.3f);
                var moved = new Vector2(pos.x + train.Direction.x * c * dt,
                                        pos.y + train.Direction.y * c * dt);
                float now = WaveMath.Sample(pos, t, in trains).Height;
                float later = WaveMath.Sample(moved, t + dt, in trains).Height;
                Assert.AreEqual(now, later, 1e-4f,
                    $"the crest at ({pos.x},{pos.y},t={t}) must be found again {dt}s later, c·dt downwave");
            }
        }

        // ---- The derivation: primary downwind at the dominant wavelength, secondaries chop --------

        [Test]
        public void TrainsFrom_PrimaryRunsDownwind_SecondariesShorterAndSmaller()
        {
            var settings = WaveFieldSettings.Default;
            var wind = new Vector2(8f, -6f); // speed 10, direction (0.8, -0.6)
            var trains = WaveMath.TrainsFrom(wind, 0.5f, in settings);

            Assert.AreEqual(0.8f, trains[0].Direction.x, 1e-5f, "primary travels downwind (x)");
            Assert.AreEqual(-0.6f, trains[0].Direction.y, 1e-5f, "primary travels downwind (y)");
            Assert.AreEqual(
                settings.DominantWavelengthBase + settings.DominantWavelengthPerWindSpeed * 10f,
                trains[0].Wavelength, 1e-4f, "dominant wavelength grows with wind speed per the mapping");

            for (int i = 1; i < trains.Count; i++)
            {
                Assert.Less(trains[i].Wavelength, trains[0].Wavelength, $"secondary {i} is shorter chop");
                Assert.Less(trains[i].Amplitude, trains[0].Amplitude, $"secondary {i} is smaller");
                Assert.AreNotEqual(trains[0].PhaseOffset, trains[i].PhaseOffset,
                    "hashed phase variety — the trains must not all crest together");
            }

            // The train count is a tunable, capped by the container.
            settings.SecondaryTrainCount = 2;
            Assert.AreEqual(3, WaveMath.TrainsFrom(wind, 0.5f, in settings).Count, "1 + 2 secondaries");
            settings.SecondaryTrainCount = 99;
            Assert.AreEqual(4, WaveMath.TrainsFrom(wind, 0.5f, in settings).Count, "clamped to MaxTrains");
        }

        // ---- Pinned parity grid (the HLSL-twin reference — ADR 0018 §(4)) ------------------------
        // Exact values for WaveFieldSettings.Default, wind (5, 2), SeaState01 = 0.6. If WaveMath's
        // math changes these numbers change, and the B1 shader twin must be re-transcribed in the
        // SAME PR. Generated from the reference implementation; tolerance absorbs runtime ulp only.

        [Test]
        public void PinnedTrains_Default_Wind5x2_Sea06()
        {
            var settings = WaveFieldSettings.Default;
            var trains = WaveMath.TrainsFrom(new Vector2(5f, 2f), 0.6f, in settings);

            Assert.AreEqual(4, trains.Count);
            Assert.AreEqual(2.2f, trains.CrestSharpening, 1e-6f);
            Assert.AreEqual(0.7747321f, trains.TotalAmplitude, 1e-5f);

            AssertTrain(trains[0], 0.9284767f, 0.37139067f, 14.077747f, 0.40141556f, 4.6882544f, 5.171592f);
            AssertTrain(trains[1], 0.5905858f, 0.8069748f, 7.742761f, 0.180637f, 3.4769022f, 0.07863445f);
            AssertTrain(trains[2], 0.90483755f, -0.42575702f, 5.349544f, 0.12042467f, 2.890034f, 0.7444053f);
            AssertTrain(trains[3], 0.8405534f, 0.5417289f, 3.0971043f, 0.07225481f, 2.198986f, 3.3062155f);
        }

        [Test]
        public void PinnedSamples_Default_Wind5x2_Sea06()
        {
            var settings = WaveFieldSettings.Default;
            var trains = WaveMath.TrainsFrom(new Vector2(5f, 2f), 0.6f, in settings);

            AssertSample(trains, 0f, 0f, 0.0, -0.49587795f, 0.16894022f, -0.013910301f, 0f);
            AssertSample(trains, 12.5f, -3.75f, 47.0, -0.44932735f, 0.124402955f, -0.17522234f, 0f);
            AssertSample(trains, -80.25f, 33.5f, 1234.5, 0.2159468f, 0.24672854f, 0.16757658f, 0.060176842f);
            AssertSample(trains, 3.2f, 7.9f, 100000.25, -0.5471003f, 0.022383735f, -0.065342605f, 0f);
            // A near-perfect crest: height ≈ the full amplitude envelope, slope ≈ 0 at the tip.
            AssertSample(trains, -19.75f, -29.5f, 40.5, 0.77405816f, 0.0014246728f, 0.00018005865f, 0.99808717f);
        }

        private static void AssertTrain(in WaveTrain train, float dirX, float dirY,
                                        float wavelength, float amplitude, float speed, float phase)
        {
            Assert.AreEqual(dirX, train.Direction.x, 1e-5f, "pinned direction.x");
            Assert.AreEqual(dirY, train.Direction.y, 1e-5f, "pinned direction.y");
            Assert.AreEqual(wavelength, train.Wavelength, 1e-4f, "pinned wavelength");
            Assert.AreEqual(amplitude, train.Amplitude, 1e-5f, "pinned amplitude");
            Assert.AreEqual(speed, train.PhaseSpeed, 1e-4f, "pinned phase speed");
            Assert.AreEqual(phase, train.PhaseOffset, 1e-4f, "pinned phase offset");
        }

        private static void AssertSample(in WaveTrains trains, float x, float y, double t,
                                         float height, float slopeX, float slopeY, float crest)
        {
            WaveSample s = WaveMath.Sample(new Vector2(x, y), t, in trains);
            Assert.AreEqual(height, s.Height, 1e-4f, $"pinned height at ({x},{y},t={t})");
            Assert.AreEqual(slopeX, s.Slope.x, 1e-4f, $"pinned slope.x at ({x},{y},t={t})");
            Assert.AreEqual(slopeY, s.Slope.y, 1e-4f, $"pinned slope.y at ({x},{y},t={t})");
            Assert.AreEqual(crest, s.CrestFactor, 1e-4f, $"pinned crest factor at ({x},{y},t={t})");
        }
    }
}
