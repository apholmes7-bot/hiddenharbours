using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Art;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// **THE HULL RIDES THE SEA THAT IS DRAWN** — the "boats are nearly submerged" fix
    /// (owner playtest 2026-07-27: *"a lot of the boats seem to be sitting very low… a lot are
    /// nearly submerged"*).
    ///
    /// <para>The displaced surface's vertex stage samples the shared wave field at
    /// <c>_OceanSwellScale / 0.025</c> — <b>2.8</b> at every one of the owner's tuned water materials.
    /// <c>BoatWaveMotion</c> sampled the same field at <b>1</b>. That is not a small discrepancy in one
    /// sea: it is two DIFFERENT seas with the same amplitude envelope and wavelengths 2.8× apart. They
    /// line up only by coincidence, so the drawn water stands at a crest where the hull sits in a
    /// trough and climbs straight over it.</para>
    ///
    /// <para>Same <c>_OceanSwellScale</c> incident that already bit the watertight clamp — fixed there
    /// (it scans at <c>WaterIsoDepthFrame.FreqScale</c>), never fixed for the RIDE. These tests pin
    /// both halves: that the ride now agrees with the drawn surface, and that at scale 1 it
    /// measurably did not.</para>
    /// </summary>
    public class HullRidesTheDrawnSeaTests
    {
        /// <summary>The owner's shipped water materials: _OceanSwellScale 0.07 over the 0.025
        /// reference. Not a hypothetical — this is what every preset carries.</summary>
        private const float OwnerFreqScale = 0.07f / 0.025f;   // 2.8

        private static readonly Vector2 Wind = new Vector2(8f, 0f);
        private const float Sea = 0.7f;

        private static readonly Vector2[] Probes =
        {
            new Vector2(0f, 0f), new Vector2(3.5f, -2.25f), new Vector2(11.75f, 6.5f),
            new Vector2(-8.25f, 14f), new Vector2(27f, -19.5f), new Vector2(-33.5f, -4f),
        };

        private static void Field(out WaveTrains trains, out PackedWaveField packed)
        {
            trains = WaveMath.TrainsFrom(Wind, Sea, WaveFieldSettings.Default);
            packed = WaveFieldBridge.Pack(in trains);
        }

        /// <summary>What <c>BoatWaveMotion.SampleTheDrawnSea</c> now does: sample the field at the
        /// SCALED position, which is identically the field at that frequency scale
        /// (θ = k·s·(dir·pos) + φ ≡ θ at pos·s), and put the chain-rule factor back on the slope.</summary>
        private static WaveSample RideSample(Vector2 pos, in WaveTrains trains, float freqScale)
        {
            WaveSample raw = WaveMath.Sample(pos * freqScale, 0.0, in trains);
            return new WaveSample(raw.Height, raw.Slope * freqScale, raw.CrestFactor);
        }

        // ===== the fix ====================================================================================

        [Test]
        public void TheRide_MatchesTheDrawnSurface_AtTheOwnersFrequencyScale()
        {
            // The claim, stated as the one that matters: at every probe the hull's ride height equals
            // the height the shader lifts its vertices to. Same sea, same wave, same crest.
            Field(out WaveTrains trains, out PackedWaveField packed);

            foreach (Vector2 p in Probes)
            {
                float drawn = WaveFieldBridge.ShaderTwinSample(p, in packed, OwnerFreqScale).Height;
                float ride = RideSample(p, in trains, OwnerFreqScale).Height;
                Assert.AreEqual(drawn, ride, 1e-3f,
                    $"at {p} the hull must ride the height the surface DRAWS ({drawn:F4} m), " +
                    $"not {ride:F4} m — that gap is the water climbing the hull");
            }
        }

        [Test]
        public void Sabotage_TheOldScaleOfOne_DisagreesWithTheDrawnSurface_ByMoreThanAHull()
        {
            // ⚠️ THE DEFECT, MEASURED. Without this the fix above could be vacuous — if the two scales
            // happened to agree, there was never a bug and nothing to fix. They do not: the error is a
            // large fraction of the wave envelope, which for a dory (0.11 m draft, ~0.5 m deep) is the
            // difference between floating and swamped.
            Field(out WaveTrains trains, out PackedWaveField packed);

            float worst = 0f;
            foreach (Vector2 p in Probes)
            {
                float drawn = WaveFieldBridge.ShaderTwinSample(p, in packed, OwnerFreqScale).Height;
                float oldRide = RideSample(p, in trains, 1f).Height;      // the shipped behaviour
                worst = Mathf.Max(worst, Mathf.Abs(drawn - oldRide));
            }

            float envelope = trains.TotalAmplitude;
            Assert.Greater(worst, 0.25f * envelope,
                $"the old scale-1 ride was within {worst:F3} m of the drawn sea (envelope " +
                $"{envelope:F3} m) — if the two agree this closely there was no defect and this " +
                "whole fix is measuring noise");
        }

        [Test]
        public void AtScaleOne_TheRideIsUnchanged_TheABContract()
        {
            // A surface at the shipped default scale (or no displaced sea at all) publishes 1, and
            // then the fix must be exactly inert — the flat-water/A-B contract every ADR 0023 change
            // has kept.
            Field(out WaveTrains trains, out _);
            foreach (Vector2 p in Probes)
            {
                WaveSample plain = WaveMath.Sample(p, 0.0, in trains);
                WaveSample scaled = RideSample(p, in trains, 1f);
                Assert.AreEqual(plain.Height, scaled.Height, 0f, $"height at {p}");
                Assert.AreEqual(plain.Slope.x, scaled.Slope.x, 0f, $"slope.x at {p}");
                Assert.AreEqual(plain.Slope.y, scaled.Slope.y, 0f, $"slope.y at {p}");
            }
        }

        [Test]
        public void TheSlopeCarriesTheChainRuleFactor_SoTheRockMatchesTheDrawnSteepness()
        {
            // Shorter waves of the same height are STEEPER, and the hull should roll to what is drawn.
            // Sampling at a scaled position alone loses the factor (d/dpos of a scaled argument), so
            // the rock would read 2.8× too gentle against a sea that visibly is not.
            Field(out WaveTrains trains, out PackedWaveField packed);

            foreach (Vector2 p in Probes)
            {
                Vector2 drawn = WaveFieldBridge.ShaderTwinSample(p, in packed, OwnerFreqScale).Slope;
                Vector2 ride = RideSample(p, in trains, OwnerFreqScale).Slope;
                Assert.AreEqual(drawn.x, ride.x, 1e-2f, $"slope.x at {p}");
                Assert.AreEqual(drawn.y, ride.y, 1e-2f, $"slope.y at {p}");
            }
        }

        // ===== the seam that carries it ===================================================================

        [Test]
        public void TheSeamCarriesTheFreqScale_AndDefaultsToOne()
        {
            // The ride's ONLY route to this number is the Core seam — FreqScale lives on
            // WaterIsoDepthFrame in Art, which Boats cannot reference (rule 4). That is precisely why
            // the clamp got fixed and the ride did not.
            var published = new DisplacedSeaState(1.5f, 2f, OwnerFreqScale);
            Assert.AreEqual(OwnerFreqScale, published.FreqScale, 1e-6f);

            var legacy = new DisplacedSeaState(1.5f, 2f);
            Assert.AreEqual(1f, legacy.FreqScale, 1e-6f,
                "a state constructed without a scale must read as 'sample the field as-is', never 0");

            Assert.Greater(new DisplacedSeaState(1f, 1f, 0f).FreqScale, 0f,
                "a zero scale would collapse every wavelength to nothing — floored, not honoured");
        }
    }
}
