using System.Collections.Generic;
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
        /// <summary>The shader's own normalisation reference (<c>WAVE_LEGACY_SCALE_REF</c>): the
        /// <c>_OceanSwellScale</c> at which the drawn sea runs the field's TRUE wavelengths.</summary>
        private const float LegacyScaleRef = 0.025f;

        /// <summary>Every water material the game ships, hero first. Read, never assumed — see
        /// <see cref="ShippedFreqScale"/>.</summary>
        private static readonly string[] WaterMaterials =
        {
            "Assets/_Project/Art/Materials/Water.mat",
            "Assets/_Project/Art/Materials/WaterPresets/Water_DeepBlue.mat",
            "Assets/_Project/Art/Materials/WaterPresets/Water_FoggySmother.mat",
            "Assets/_Project/Art/Materials/WaterPresets/Water_GlassyCalm.mat",
            "Assets/_Project/Art/Materials/WaterPresets/Water_NorthAtlantic.mat",
            "Assets/_Project/Art/Materials/WaterPresets/Water_StirredBrown.mat",
            "Assets/_Project/Art/Materials/WaterPresets/Water_StormGrey.mat",
            "Assets/_Project/Art/Materials/WaterPresets/Water_Tropical.mat",
            "Assets/_Project/Art/Materials/WaterPresets/Water_WarmShelter.mat",
        };

        /// <summary>
        /// The frequency scale the game actually DRAWS at, READ from the hero material rather than
        /// typed here.
        ///
        /// <para>⚠️ It used to be typed — <c>0.07f / 0.025f</c>, with a comment saying "not a
        /// hypothetical, this is what every preset carries". It was true when it was written and it
        /// stopped being true the day the owner ruled the swell longer (2026-09-02), which is exactly
        /// the failure mode a typed constant has: the test goes on passing while the sentence next to
        /// it becomes false. Reading the asset cannot go stale.</para>
        ///
        /// <para>⚠️⚠️ And it must come from the PRESETS' family, not from a code default:
        /// <c>_OceanSwellScale</c> is in <c>WaterSurface.MoodFloatNames</c>, so the live value is
        /// eased between the preset anchors and <c>Water.mat</c> is not authoritative on its own.
        /// <see cref="EveryWaterMaterial_DrawsTheFieldsTrueWavelengths"/> is what makes reading one of
        /// them sound: it requires all nine to agree.</para>
        /// </summary>
        private static float ShippedFreqScale()
        {
            var hero = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(WaterMaterials[0]);
            Assert.IsNotNull(hero, $"{WaterMaterials[0]} must exist — it is the sea the game draws");
            Assert.IsTrue(hero.HasProperty("_OceanSwellScale"), "_OceanSwellScale must be a water-shader property");
            return Mathf.Max(hero.GetFloat("_OceanSwellScale"), 1e-4f) / LegacyScaleRef;
        }

        /// <summary>The scale the sea was DRAWN at before the 2026-09-02 ruling — kept as a literal on
        /// purpose. The sabotage arm below is about a defect that HAPPENED, and history does not get to
        /// be re-read off today's assets.</summary>
        private const float HistoricFreqScale = 0.07f / 0.025f;   // 2.8, the pre-ruling drawn scale

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
        public void TheRide_MatchesTheDrawnSurface_AtTheShippedFrequencyScale()
        {
            // The claim, stated as the one that matters: at every probe the hull's ride height equals
            // the height the shader lifts its vertices to. Same sea, same wave, same crest — at
            // whatever scale the shipped materials are drawing at today.
            Field(out WaveTrains trains, out PackedWaveField packed);
            float shipped = ShippedFreqScale();

            foreach (Vector2 p in Probes)
            {
                float drawn = WaveFieldBridge.ShaderTwinSample(p, in packed, shipped).Height;
                float ride = RideSample(p, in trains, shipped).Height;
                Assert.AreEqual(drawn, ride, 1e-3f,
                    $"at {p} the hull must ride the height the surface DRAWS ({drawn:F4} m), " +
                    $"not {ride:F4} m — that gap is the water climbing the hull");
            }
        }

        [Test]
        public void EveryWaterMaterial_DrawsTheFieldsTrueWavelengths()
        {
            // ⭐⭐ THE 2026-09-02 RULING, as the one number it comes down to: *"swell scale you can make
            // the changes for a longer slower swell, thats fine."* One sea at the field's TRUE
            // wavelengths means _OceanSwellScale sits exactly on the shader's own normalisation
            // reference, so the frequency scale threaded through the DisplacedSea seam is 1 — and the
            // drawn sea stops being a different WAVE from the one the rock was tuned on.
            //
            // ⚠️ ALL NINE, and that is not belt-and-braces: _OceanSwellScale is MOOD-EASED (it is in
            // WaterSurface.MoodFloatNames), so the live value is lerped between the preset anchors by
            // the weather. One preset left behind would not be a stale key — it would be a sea whose
            // wavelength changes with the weather for no physical reason, and `Apply water preset`
            // would stamp it over the hero material besides.
            var offenders = new List<string>();
            foreach (string path in WaterMaterials)
            {
                var mat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(path);
                Assert.IsNotNull(mat, $"{path} must exist");
                float scale = mat.GetFloat("_OceanSwellScale");
                if (Mathf.Abs(scale / LegacyScaleRef - 1f) > 1e-4f)
                    offenders.Add($"{System.IO.Path.GetFileName(path)}: _OceanSwellScale {scale} " +
                                  $"(freq scale {scale / LegacyScaleRef:F2})");
            }
            Assert.IsEmpty(offenders,
                "every water material must draw the field's TRUE wavelengths (_OceanSwellScale = " +
                $"{LegacyScaleRef}, frequency scale 1) — these do not, so the sea they draw is not the " +
                "sea the hulls ride:\n  " + string.Join("\n  ", offenders));
        }

        [Test]
        public void AtTheShippedScale_TheDrawnSeaAndTheSIMSeaAreOneFunction()
        {
            // What the ruling buys, said as strongly as it can be: the drawn height is no longer merely
            // AGREEING with the sim's — at scale 1 the two are the same evaluation, bit for bit, with no
            // scaling of the position and no chain-rule factor on the slope. The one-sea rule stops
            // needing a seam to hold it together.
            Field(out WaveTrains trains, out _);
            float shipped = ShippedFreqScale();
            Assert.AreEqual(1f, shipped, 1e-4f, "the shipped materials must draw at the field's own scale");

            foreach (Vector2 p in Probes)
            {
                WaveSample sim = WaveMath.Sample(p, 0.0, in trains);
                WaveSample drawn = RideSample(p, in trains, shipped);
                Assert.AreEqual(sim.Height, drawn.Height, 0f, $"height at {p}");
                Assert.AreEqual(sim.Slope.x, drawn.Slope.x, 0f, $"slope.x at {p}");
                Assert.AreEqual(sim.Slope.y, drawn.Slope.y, 0f, $"slope.y at {p}");
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
                // HistoricFreqScale, not the shipped one: this arm is about the defect that HAPPENED,
                // and since 2026-09-02 the shipped scale IS 1 — reading it here would compare the sea
                // with itself and quietly assert nothing.
                float drawn = WaveFieldBridge.ShaderTwinSample(p, in packed, HistoricFreqScale).Height;
                float oldRide = RideSample(p, in trains, 1f).Height;      // the pre-#331 behaviour
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
            // A surface with no displaced sea at all publishes 1, and then the scaling must be exactly
            // inert — the flat-water/A-B contract every ADR 0023 change has kept. Since the 2026-09-02
            // ruling this is also the SHIPPED path rather than only the fallback one, which is why the
            // test above asserts the same identity against the assets.
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
                Vector2 drawn = WaveFieldBridge.ShaderTwinSample(p, in packed, HistoricFreqScale).Slope;
                Vector2 ride = RideSample(p, in trains, HistoricFreqScale).Slope;
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
            var published = new DisplacedSeaState(1.5f, 2f, HistoricFreqScale);
            Assert.AreEqual(HistoricFreqScale, published.FreqScale, 1e-6f);

            var legacy = new DisplacedSeaState(1.5f, 2f);
            Assert.AreEqual(1f, legacy.FreqScale, 1e-6f,
                "a state constructed without a scale must read as 'sample the field as-is', never 0");

            Assert.Greater(new DisplacedSeaState(1f, 1f, 0f).FreqScale, 0f,
                "a zero scale would collapse every wavelength to nothing — floored, not honoured");
        }
    }
}
