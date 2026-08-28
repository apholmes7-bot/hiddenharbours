using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>Breaking waves — the Core math (ADR 0040).</b> Shoaling, the depth-limited break criterion,
    /// the Iribarren classification and the guards, all pure and headless. These are the pinned
    /// reference the PR-2 HLSL twin is diffed against: change <c>BreakerMath</c> ⇒ these numbers move
    /// ⇒ the twin moves in the same PR (the <c>WaveMath</c>/<c>WaveFetch</c> discipline).
    ///
    /// <para>The whitewater age has its own file — <see cref="BreakerWhitewaterAgeMeasurementTests"/> —
    /// because it is a MEASUREMENT, not an assertion about intent.</para>
    /// </summary>
    public class BreakerMathTests
    {
        private const float G = 9.81f;

        // ---- terrain stubs (the authored seabed, deterministic by construction) -------------------

        /// <summary>A flat bottom at a fixed elevation — depth is whatever the tide makes it.</summary>
        private sealed class FlatBed : ITidalTerrain
        {
            private readonly float _elevation;
            public FlatBed(float elevation) { _elevation = elevation; }
            public float ElevationAt(Vector2 worldPos) => _elevation;
        }

        /// <summary>A plane beach rising toward +X at <c>slope</c>, passing through elevation
        /// <c>-depthAtOrigin</c> at x = 0. The wave runs +X, up the slope.</summary>
        private sealed class PlaneBeach : ITidalTerrain
        {
            private readonly float _slope, _depthAtOrigin;
            public PlaneBeach(float slope, float depthAtOrigin) { _slope = slope; _depthAtOrigin = depthAtOrigin; }
            public float ElevationAt(Vector2 worldPos) => _slope * worldPos.x - _depthAtOrigin;
        }

        /// <summary>An offshore BAR: deep water either side of a hump whose crest sits at
        /// <c>crestElevation</c>. The shape the owner's "boils at half-ebb, sleeps at high water"
        /// describes.</summary>
        private sealed class Bar : ITidalTerrain
        {
            private readonly float _crestElevation, _deepElevation, _centreX, _halfWidth;
            public Bar(float crestElevation, float deepElevation, float centreX, float halfWidth)
            {
                _crestElevation = crestElevation; _deepElevation = deepElevation;
                _centreX = centreX; _halfWidth = halfWidth;
            }
            public float ElevationAt(Vector2 worldPos)
            {
                float t = Mathf.Clamp01(Mathf.Abs(worldPos.x - _centreX) / _halfWidth);
                return Mathf.Lerp(_crestElevation, _deepElevation, t * t);
            }
        }

        // ---- helpers ------------------------------------------------------------------------------

        /// <summary>A representative dominant swell: an 18 m train (the field's ~8 m/s wind case) of
        /// 0.5 m amplitude, i.e. a 1 m deep-water wave height, running +X.</summary>
        private static WaveTrain Swell(float amplitude = 0.5f, float wavelength = 18f)
            => new WaveTrain(Vector2.right, wavelength, amplitude, 0f, G);

        private static BreakerSettings Settings => BreakerSettings.Default;

        // =========================================================================================
        //  Determinism (rule 5) — recomputed, never saved, same answer forever
        // =========================================================================================

        [Test]
        public void SameInputs_YieldIdenticalSamples_AcrossTheSweep()
        {
            var terrain = new PlaneBeach(0.04f, 3f);
            var train = Swell();
            var settings = Settings;

            foreach (float waterLevel in new[] { -0.8f, -0.25f, 0f, 0.6f, 1.4f })
            foreach (float x in new[] { 5f, 21.5f, 40f, 52.25f, 66f })
            {
                var pos = new Vector2(x, 3.5f);
                var a = BreakerMath.SampleAt(pos, in train, waterLevel, terrain, 1f, in settings);
                var b = BreakerMath.SampleAt(pos, in train, waterLevel, terrain, 1f, in settings);

                Assert.AreEqual(a.DepthMeters, b.DepthMeters, "depth bit-stable");
                Assert.AreEqual(a.LocalWavelength, b.LocalWavelength, "shoaled wavelength bit-stable");
                Assert.AreEqual(a.Celerity, b.Celerity, "celerity bit-stable");
                Assert.AreEqual(a.ShoalingCoefficient, b.ShoalingCoefficient, "Ks bit-stable");
                Assert.AreEqual(a.WaveHeight, b.WaveHeight, "height bit-stable");
                Assert.AreEqual(a.Breaking01, b.Breaking01, "break gate bit-stable");
                Assert.AreEqual(a.BedSlope, b.BedSlope, "bed slope bit-stable");
                Assert.AreEqual(a.Iribarren, b.Iribarren, "iribarren bit-stable");
                Assert.AreEqual(a.Class, b.Class, "breaker class bit-stable");
            }
        }

        [Test]
        public void NothingHere_ReadsAClockOrARandomNumber_TheWholeStateIsAFunctionOfItsInputs()
        {
            // The same call at two "moments" — there is no time argument at all, which is the point:
            // breaking is a function of (train, position, water level, seabed). The tide moves it by
            // moving the WATER LEVEL, which the caller supplies from the deterministic tide model.
            var terrain = new PlaneBeach(0.04f, 3f);
            var train = Swell();
            var settings = Settings;
            var pos = new Vector2(44f, 0f);

            var first = BreakerMath.SampleAt(pos, in train, 0f, terrain, 1f, in settings);
            for (int i = 0; i < 32; i++) BreakerMath.SampleAt(new Vector2(i, i), in train, i * 0.1f, terrain, 1f, in settings);
            var again = BreakerMath.SampleAt(pos, in train, 0f, terrain, 1f, in settings);

            Assert.AreEqual(first.Breaking01, again.Breaking01, "no hidden state between calls");
            Assert.AreEqual(first.WaveHeight, again.WaveHeight, "no hidden state between calls");
        }

        // =========================================================================================
        //  Shoaling — the wave feels the bottom
        // =========================================================================================

        [Test]
        public void DeepWater_ShoalsNothing_SoTheOpenSeaIsUntouched()
        {
            // The property that keeps the shipped field's tuning valid: offshore, this model is a
            // no-op. Ks = 1, L = L0, c = c0.
            var train = Swell();
            float local = BreakerMath.ShoaledWavelength(train.Wavelength, 60f);
            float ks = BreakerMath.ShoalingCoefficient(train.PhaseSpeed, train.Wavelength, local, 60f);

            Assert.AreEqual(train.Wavelength, local, 1e-3f, "deep water leaves the wavelength alone");
            Assert.AreEqual(1f, ks, 1e-3f, "deep water leaves the height alone");
            Assert.AreEqual(train.PhaseSpeed,
                            BreakerMath.ShoaledCelerity(train.PhaseSpeed, train.Wavelength, local), 1e-3f,
                            "deep water leaves the celerity alone");
        }

        [Test]
        public void WavelengthAndCelerity_ShrinkMonotonically_AsTheBottomComesUp()
        {
            var train = Swell();
            float[] depths = { 30f, 20f, 12f, 8f, 6f, 4f, 3f, 2f, 1.5f, 1f, 0.6f, 0.3f, 0.1f };

            float previousL = float.MaxValue, previousC = float.MaxValue;
            foreach (float d in depths)
            {
                float l = BreakerMath.ShoaledWavelength(train.Wavelength, d);
                float c = BreakerMath.ShoaledCelerity(train.PhaseSpeed, train.Wavelength, l);

                Assert.Less(l, previousL, $"wavelength must keep shortening as depth falls (d={d})");
                Assert.Less(c, previousC, $"celerity must keep falling as depth falls (d={d})");
                Assert.LessOrEqual(l, train.Wavelength + 1e-4f, "a shoaled wave is never longer than in deep water");
                previousL = l; previousC = c;
            }
        }

        [Test]
        public void Celerity_TendsToTheShallowWaterLimit_RootGD()
        {
            // Fenton & McKee is exact in this limit; the ~1.7% band is the approximation's own error,
            // and it is why the twin never has to iterate a dispersion solve.
            var train = Swell();
            foreach (float d in new[] { 1f, 0.5f, 0.25f, 0.1f })
            {
                float l = BreakerMath.ShoaledWavelength(train.Wavelength, d);
                float c = BreakerMath.ShoaledCelerity(train.PhaseSpeed, train.Wavelength, l);
                float shallow = Mathf.Sqrt(G * d);
                Assert.AreEqual(shallow, c, shallow * 0.06f, $"c must approach sqrt(g*d) in the shallows (d={d})");
            }
        }

        [Test]
        public void GreensLaw_GrowsTheWave_AsTheWaterShallows()
        {
            // Below the shoaling minimum the height climbs without limit as d^(-1/4) — the swell that
            // is knee-high offshore standing head-high on the bar.
            var train = Swell();
            float previous = 0f;
            foreach (float d in new[] { 2f, 1.5f, 1f, 0.7f, 0.5f, 0.3f, 0.15f })
            {
                float l = BreakerMath.ShoaledWavelength(train.Wavelength, d);
                float ks = BreakerMath.ShoalingCoefficient(train.PhaseSpeed, train.Wavelength, l, d);
                Assert.Greater(ks, previous, $"Green's law must keep building the wave (d={d})");
                previous = ks;
            }
            Assert.Greater(previous, 1.3f, "a 1 m swell in 15 cm of water stands well above its deep-water height");
        }

        [Test]
        public void TheShoalingMINIMUM_Exists_AndIsNotABug()
        {
            // Ks dips BELOW 1 in intermediate depth before Green's law takes over — textbook, ~0.913,
            // and the reason "shoaling monotonicity" is only true inside the shallow regime. Pinned so
            // nobody later "fixes" real physics away.
            var train = Swell();
            float minimum = float.MaxValue;
            for (float d = 0.5f; d <= 30f; d += 0.05f)
            {
                float l = BreakerMath.ShoaledWavelength(train.Wavelength, d);
                minimum = Mathf.Min(minimum, BreakerMath.ShoalingCoefficient(train.PhaseSpeed, train.Wavelength, l, d));
            }
            Assert.AreEqual(0.913f, minimum, 0.02f, "the classic shoaling minimum Ks ~ 0.913");
        }

        // =========================================================================================
        //  The break criterion — and the tide that moves it
        // =========================================================================================

        [Test]
        public void ABar_BoilsAtLowWater_AndSleepsAtHighWater()
        {
            // ⭐ The headline: nothing animates this. Depth is waterLevel - seabed, so the SAME bar
            // under the SAME swell breaks on one tide and not on the other, for free.
            var bar = new Bar(crestElevation: -1f, deepElevation: -6f, centreX: 0f, halfWidth: 14f);
            var train = Swell();
            var settings = Settings;
            var crest = new Vector2(0f, 0f);

            var lowWater = BreakerMath.SampleAt(crest, in train, 0f, bar, 1f, in settings);
            var highWater = BreakerMath.SampleAt(crest, in train, 1.6f, bar, 1f, in settings);

            Assert.AreEqual(1f, lowWater.Breaking01, 1e-3f, "at low water the bar boils");
            Assert.AreEqual(0f, highWater.Breaking01, 1e-3f, "at high water the same bar sleeps");
            Assert.AreNotEqual(BreakerClass.None, lowWater.Class, "a boiling bar has a breaker type");
            Assert.AreEqual(BreakerClass.None, highWater.Class, "a sleeping bar has none");
        }

        [Test]
        public void TheBreakLine_WalksShoreward_AsTheTideRises()
        {
            var beach = new PlaneBeach(0.04f, 3f);
            var train = Swell();
            var settings = Settings;

            float previous = float.MinValue;
            foreach (float waterLevel in new[] { -0.8f, -0.4f, 0f, 0.4f, 0.8f })
            {
                float breakX = float.NaN;
                for (float x = 0f; x < 120f; x += 0.05f)
                {
                    var s = BreakerMath.SampleAt(new Vector2(x, 0f), in train, waterLevel, beach, 1f, in settings);
                    if (s.Breaking01 >= 0.5f) { breakX = x; break; }
                }

                Assert.IsFalse(float.IsNaN(breakX), $"the swell must break somewhere at water level {waterLevel}");
                Assert.Greater(breakX, previous, "a rising tide carries the break line further inshore");
                previous = breakX;
            }
        }

        [Test]
        public void BreakingHeight_IsDepthLimited_SoSurfShrinksUpTheBeach()
        {
            // Once broken, H is held at gamma*d — which is why a big day and a small day look alike in
            // the last few metres, and why the height goes to zero at the water's edge.
            var beach = new PlaneBeach(0.04f, 3f);
            var settings = Settings;
            var small = Swell(0.35f);
            var big = Swell(1.2f);

            for (float x = 60f; x < 74f; x += 1f)
            {
                var pos = new Vector2(x, 0f);
                var a = BreakerMath.SampleAt(pos, in small, 0f, beach, 1f, in settings);
                var b = BreakerMath.SampleAt(pos, in big, 0f, beach, 1f, in settings);

                Assert.AreEqual(1f, a.Breaking01, 1e-3f, "both are well inside the surf zone here");
                Assert.AreEqual(1f, b.Breaking01, 1e-3f, "both are well inside the surf zone here");
                Assert.AreEqual(settings.BreakerIndex * a.DepthMeters, a.WaveHeight, 1e-4f,
                                "a broken wave stands at exactly gamma*d");
                Assert.AreEqual(a.WaveHeight, b.WaveHeight, 1e-4f,
                                "in the same depth, the big day and the small day break to the same height");
            }
        }

        [Test]
        public void TheGate_IsSmooth_NotACutoff_SoTheSurfLineCannotPopOnTheTide()
        {
            // A hard H >= gamma*d test would step the whole surf line on and off as the tide crossed a
            // bar. Sweeping the water level must move the gate continuously.
            var bar = new Bar(-1f, -6f, 0f, 14f);
            var train = Swell();
            var settings = Settings;

            float previous = BreakerMath.SampleAt(Vector2.zero, in train, 0f, bar, 1f, in settings).Breaking01;
            for (float waterLevel = 0f; waterLevel <= 1.8f; waterLevel += 0.01f)
            {
                float now = BreakerMath.SampleAt(Vector2.zero, in train, waterLevel, bar, 1f, in settings).Breaking01;
                Assert.LessOrEqual(Mathf.Abs(now - previous), 0.12f,
                                   $"the gate must not jump as the tide crosses the bar (water level {waterLevel})");
                previous = now;
            }
        }

        // =========================================================================================
        //  Breaker TYPE — the bathymetry decides, nobody paints a barrel in
        // =========================================================================================

        [Test]
        public void TheIribarrenTable_TurnsABedSlopeIntoTheOwnersVocabulary()
        {
            // H0 = 1 m over L0 = 18 m, so xi = tanB / 0.2357. Battjes' thresholds, unchanged.
            var settings = Settings;
            const float h0 = 1f, l0 = 18f;

            var expected = new (float slope, BreakerClass cls, string place)[]
            {
                (0.02f, BreakerClass.Spilling,   "1:50 sand flat"),
                (0.04f, BreakerClass.Spilling,   "1:25 shoal"),
                (0.10f, BreakerClass.Spilling,   "1:10 steep sand"),
                (0.20f, BreakerClass.Plunging,   "1:5 shingle bank"),
                (0.40f, BreakerClass.Plunging,   "1:2.5 reef edge"),
                (0.80f, BreakerClass.Collapsing, "1:1.25 rock ledge"),
                (1.50f, BreakerClass.Surging,    "a quay wall"),
            };

            foreach (var (slope, cls, place) in expected)
            {
                float xi = BreakerMath.Iribarren(slope, h0, l0);
                Assert.AreEqual(cls, BreakerMath.ClassFor(xi, in settings),
                                $"{place} (tanB {slope}, xi {xi:0.00}) must read as {cls}");
            }
        }

        [Test]
        public void AGentleShoalSpills_AndAReefEdgePlunges_OnTheSameSwell()
        {
            // ⭐ Barrels only where the bathymetry earns them. Same wave, same tide, two seabeds.
            var train = Swell();
            var settings = Settings;
            var shoal = new PlaneBeach(0.03f, 3f);      // 1:33 — a sandy flat
            var reef = new PlaneBeach(0.45f, 3f);       // 1:2.2 — a steep ledge

            BreakerClass shoalClass = FirstBreakingClass(shoal, in train, in settings);
            BreakerClass reefClass = FirstBreakingClass(reef, in train, in settings);

            Assert.AreEqual(BreakerClass.Spilling, shoalClass, "a gentle shoal crumbles");
            Assert.AreEqual(BreakerClass.Plunging, reefClass, "a steep ledge throws a lip and barrels");
        }

        private static BreakerClass FirstBreakingClass(ITidalTerrain terrain, in WaveTrain train,
                                                       in BreakerSettings settings)
        {
            for (float x = 0f; x < 200f; x += 0.05f)
            {
                var s = BreakerMath.SampleAt(new Vector2(x, 0f), in train, 0f, terrain, 1f, in settings);
                if (s.Breaking01 >= 0.5f) return s.Class;
            }
            return BreakerClass.None;
        }

        [Test]
        public void BedSlope_ReadsZero_WhereTheBottomFallsAway()
        {
            // A wave running out into deeper water is climbing nothing — no surf similarity to speak of.
            var falling = new PlaneBeach(-0.2f, 3f);        // the bed DROPS along +X
            var train = Swell();
            float slope = BreakerMath.BedSlopeAlong(new Vector2(10f, 0f), train.Direction,
                                                    Settings.SlopeProbeMeters, falling);
            Assert.AreEqual(0f, slope, 1e-6f, "a falling bottom gives no positive slope");
        }

        [Test]
        public void ALongSwellBarrels_WhereTheSameSlopeOnlySpillsUnderChop()
        {
            // xi = tanB / sqrt(H0/L0): the SAME bed reads differently under a long swell and short
            // chop, which is why a place has a season rather than a fixed breaker type.
            var settings = Settings;
            const float slope = 0.16f;
            float swell = BreakerMath.Iribarren(slope, 1f, 40f);   // long
            float chop = BreakerMath.Iribarren(slope, 1f, 6f);     // short

            Assert.AreEqual(BreakerClass.Plunging, BreakerMath.ClassFor(swell, in settings));
            Assert.AreEqual(BreakerClass.Spilling, BreakerMath.ClassFor(chop, in settings));
        }

        // =========================================================================================
        //  The sacred cases and the guards
        // =========================================================================================

        [Test]
        public void GlassCalm_BreaksNowhere()
        {
            // ADR 0018 (1): at sea state 0 every amplitude is exactly 0 and the water is the full
            // mirror. A model that put surf on a dead-calm shore would break that ruling.
            var beach = new PlaneBeach(0.04f, 3f);
            var glass = new WaveTrain(Vector2.right, 18f, 0f, 0f, G);
            var settings = Settings;

            for (float x = 0f; x < 80f; x += 2f)
            {
                var s = BreakerMath.SampleAt(new Vector2(x, 0f), in glass, 0f, beach, 1f, in settings);
                Assert.AreEqual(0f, s.Breaking01, "glass breaks nowhere");
                Assert.AreEqual(BreakerClass.None, s.Class, "glass has no breaker type");
                Assert.AreEqual(0f, BreakerMath.MetersSinceBreak(new Vector2(x, 0f), in glass, 0f, beach, 1f, in settings),
                                "glass leaves no whitewater");
            }
        }

        [Test]
        public void DryGround_AndOpenWaterWithNoSeabed_BreakNothing()
        {
            var train = Swell();
            var settings = Settings;

            var dry = BreakerMath.SampleAt(Vector2.zero, in train, 0f, new FlatBed(2f), 1f, in settings);
            Assert.AreEqual(0f, dry.Breaking01, "dry ground breaks nothing");
            Assert.AreEqual(BreakerClass.None, dry.Class);

            var open = BreakerMath.SampleAt(Vector2.zero, in train, 0f, null, 1f, in settings);
            Assert.AreEqual(0f, open.Breaking01, "no height map means open water everywhere");
            Assert.AreEqual(0f, BreakerMath.MetersSinceBreak(Vector2.zero, in train, 0f, null, 1f, in settings));
        }

        [Test]
        public void AStaleSettingsStruct_IsInert_NotWrong()
        {
            // Every GameConfig asset serialized before 2026-08-27 deserializes these as ZERO. gamma = 0
            // must mean "nothing breaks", the same safe-stale property WaveFetchSettings ships under.
            var zeroed = default(BreakerSettings);
            var beach = new PlaneBeach(0.04f, 3f);
            var train = Swell();

            for (float x = 0f; x < 80f; x += 2f)
            {
                var s = BreakerMath.SampleAt(new Vector2(x, 0f), in train, 0f, beach, 1f, in zeroed);
                Assert.AreEqual(0f, s.Breaking01, "a zeroed gamma breaks nothing");
                Assert.AreEqual(0f, s.WaveHeight, "and stands nothing");
            }
        }

        [Test]
        public void TheFetchEnvelope_ScalesTheBreak_SoALeeShoreSurfsLess()
        {
            // The lee of a headland gets a smaller wave, so it breaks closer in — the fetch model
            // (ADR 0027 #1) composes with this one instead of fighting it.
            var beach = new PlaneBeach(0.04f, 3f);
            var train = Swell();
            var settings = Settings;

            float exposedX = FirstBreakX(beach, in train, 1f, in settings);
            float leeX = FirstBreakX(beach, in train, 0.4f, in settings);

            Assert.Greater(leeX, exposedX, "a sheltered shore's smaller wave carries further before it breaks");
        }

        private static float FirstBreakX(ITidalTerrain terrain, in WaveTrain train, float envelope,
                                         in BreakerSettings settings)
        {
            for (float x = 0f; x < 200f; x += 0.05f)
            {
                var s = BreakerMath.SampleAt(new Vector2(x, 0f), in train, 0f, terrain, envelope, in settings);
                if (s.Breaking01 >= 0.5f) return x;
            }
            return float.NaN;
        }

        [Test]
        public void NoOutputIsEverNaNOrInfinite_AcrossAHostileSweep()
        {
            var settings = Settings;
            var terrains = new ITidalTerrain[]
            {
                new FlatBed(0f), new FlatBed(-40f), new PlaneBeach(0.04f, 3f),
                new PlaneBeach(9f, 3f), new Bar(-0.02f, -50f, 0f, 1f),
            };
            var trains = new[]
            {
                Swell(), Swell(0f), Swell(20f), Swell(0.5f, WaveTrain.MinWavelengthMeters), Swell(0.5f, 400f),
            };

            var seen = new List<float>();
            foreach (var terrain in terrains)
            foreach (var train in trains)
            foreach (float waterLevel in new[] { -60f, -0.001f, 0f, 0.001f, 60f })
            foreach (float x in new[] { -1000f, 0f, 0.031f, 73f, 1000f })
            {
                var s = BreakerMath.SampleAt(new Vector2(x, 0f), in train, waterLevel, terrain, 1f, in settings);
                seen.Clear();
                seen.AddRange(new[] { s.LocalWavelength, s.Celerity, s.ShoalingCoefficient, s.WaveHeight,
                                      s.ShoaledHeight, s.Breaking01, s.BedSlope, s.Iribarren });
                foreach (float v in seen)
                {
                    Assert.IsFalse(float.IsNaN(v), "no output may be NaN");
                    Assert.IsFalse(float.IsInfinity(v), "no output may be infinite");
                }
                Assert.That(s.Breaking01, Is.InRange(0f, 1f), "the gate stays in [0,1]");
            }
        }

        [Test]
        public void TheWaveField_IsNotTouched_BreakingIsAReadOverIt()
        {
            // ADR 0040's boundary: BreakerMath consumes trains, it never rewrites them. The wake
            // (#669) reads WaveMath's published phase and its contract is frozen.
            var settings = Settings;
            var beach = new PlaneBeach(0.04f, 3f);
            var before = WaveMath.TrainsFrom(new Vector2(6f, 2f), 0.6f, WaveFieldSettings.Default);
            var train = before.Dominant;

            for (float x = 0f; x < 80f; x += 1f)
                BreakerMath.SampleAt(new Vector2(x, 0f), in train, 0f, beach, 1f, in settings);

            var after = WaveMath.TrainsFrom(new Vector2(6f, 2f), 0.6f, WaveFieldSettings.Default);
            Assert.AreEqual(before.Count, after.Count);
            for (int i = 0; i < before.Count; i++)
            {
                Assert.AreEqual(before[i].Wavelength, after[i].Wavelength, "the field is untouched");
                Assert.AreEqual(before[i].Amplitude, after[i].Amplitude, "the field is untouched");
                Assert.AreEqual(before[i].PhaseSpeed, after[i].PhaseSpeed, "the field is untouched");
                Assert.AreEqual(before[i].PhaseOffset, after[i].PhaseOffset, "the field is untouched");
            }
        }

        [Test]
        public void TheDefaults_AreTheTextbookPhysics_NotArtDirection()
        {
            var d = BreakerSettings.Default;
            Assert.AreEqual(0.78f, d.BreakerIndex, 1e-6f, "the solitary-wave breaker index");
            Assert.AreEqual(0.5f, d.SpillingLimit, 1e-6f, "Battjes 1974");
            Assert.AreEqual(3.3f, d.PlungingLimit, 1e-6f, "Battjes 1974");
            Assert.AreEqual(5f, d.CollapsingLimit, 1e-6f, "Battjes 1974");
            Assert.AreEqual(16, BreakerMath.MarchSteps, "the fixed [unroll] bound the HLSL twin must match");
        }
    }
}
