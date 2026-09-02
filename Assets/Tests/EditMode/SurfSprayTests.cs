using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Art;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>The lip spray's pure half (ADR 0040 rev 3) and the two reads that make it follow the
    /// DRAWN bore: the published field unpacked from the bridge, and the drawn wave scale.</summary>
    public class SurfSprayTests
    {
        const string EmitterPath = "Assets/_Project/Code/Art/SurfSprayEmitter.cs";

        [Test]
        public void Emission_IsZeroOnASpillingBeach_BetweenBores_AndOnDeadWater()
        {
            const float pg = 0.25f, bg = 0.7f, wg = 0.3f;
            Assert.AreEqual(0f, SurfSprayMath.Emission01(0.1f, 1f, 1f, pg, bg, wg), "a spilling bed throws no lip");
            Assert.AreEqual(0f, SurfSprayMath.Emission01(1f, 0.5f, 1f, pg, bg, wg), "between bores there is nothing to throw");
            Assert.AreEqual(0f, SurfSprayMath.Emission01(1f, 1f, 0.2f, pg, bg, wg), "dead whitewater throws nothing");
            Assert.AreEqual(1f, SurfSprayMath.Emission01(1f, 1f, 1f, pg, bg, wg), 1e-6f, "a plunging crest arriving on live water: full");
        }

        [Test]
        public void Emission_RampsWithThePlunge_AndWithTheArrival()
        {
            const float pg = 0.25f, bg = 0.7f, wg = 0.3f;
            float last = -1f;
            for (float p = 0f; p <= 1f; p += 0.05f)
            {
                float e = SurfSprayMath.Emission01(p, 1f, 1f, pg, bg, wg);
                Assert.GreaterOrEqual(e, last, "monotone in the plunging weight");
                last = e;
            }
            last = -1f;
            for (float b = 0f; b <= 1f; b += 0.05f)
            {
                float e = SurfSprayMath.Emission01(1f, b, 1f, pg, bg, wg);
                Assert.GreaterOrEqual(e, last, "monotone in the bore's pulse");
                last = e;
            }
            Assert.AreEqual(0.5f, SurfSprayMath.Ramp01(0.5f, 0f), 1e-6f, "no gate: the ramp is the value");
            Assert.AreEqual(0f, SurfSprayMath.Ramp01(0.5f, 0.5f), 1e-6f, "at the gate: nothing yet");
            Assert.AreEqual(1f, SurfSprayMath.Ramp01(1f, 0.999f), 1e-6f, "a gate at 1 is floored, never a division by zero");
        }

        [Test]
        public void Launch_IsShoreward_AtAMultipleOfTheBoreSpeed_AndFansSymmetrically()
        {
            var shoreward = new Vector2(0f, 1f);
            const float g = 9.81f, depth = 1f;
            Vector2 straight = SurfSprayMath.Launch(shoreward, depth, g, 1.3f, 30f, 0.5f);
            Assert.AreEqual(Mathf.Sqrt(g * depth) * 1.3f, straight.magnitude, 1e-4f, "1.3 bore speeds");
            Assert.AreEqual(0f, straight.x, 1e-4f, "hash 0.5 is dead ahead");
            Vector2 left = SurfSprayMath.Launch(shoreward, depth, g, 1.3f, 30f, 0f);
            Vector2 right = SurfSprayMath.Launch(shoreward, depth, g, 1.3f, 30f, 1f);
            Assert.AreEqual(-left.x, right.x, 1e-4f, "the fan is symmetric about the shoreward line");
            Assert.AreEqual(left.y, right.y, 1e-4f);
            Assert.Greater(Vector2.Dot(left.normalized, shoreward), Mathf.Cos(31f * Mathf.Deg2Rad), "within the spread");
            Vector2 shallow = SurfSprayMath.Launch(shoreward, 0f, g, 1.3f, 30f, 0.5f);
            Assert.AreEqual(Mathf.Sqrt(g * BreakerMath.MinDepthMeters) * 1.3f, shallow.magnitude, 1e-4f,
                "at the edge the bore speed is floored, so a lip in the last centimetres barely lifts");
        }

        [Test]
        public void TheProbeLattice_CoversTheFrame_Symmetrically()
        {
            var centre = new Vector2(100f, -50f);
            var half = new Vector2(20f, 12f);
            const int n = 12;
            Vector2 first = SurfSprayMath.ProbePoint(centre, half, n, 0, 0);
            Vector2 last = SurfSprayMath.ProbePoint(centre, half, n, n - 1, n - 1);
            Assert.AreEqual(centre.x - half.x + half.x / n, first.x, 1e-4f, "the first cell's centre is half a cell in");
            Assert.AreEqual(centre.x + half.x - half.x / n, last.x, 1e-4f);
            Assert.AreEqual(centre.y - half.y + half.y / n, first.y, 1e-4f);
            Assert.AreEqual(centre.y + half.y - half.y / n, last.y, 1e-4f);
            Vector2 spawn = SurfSprayMath.SpawnPoint(first, half, n, 0f, 1f);
            Assert.AreEqual(first.x - half.x / n, spawn.x, 1e-4f, "a spawn is jittered within its own cell");
            Assert.AreEqual(first.y + half.y / n, spawn.y, 1e-4f);
        }

        [Test]
        public void UnpackTrains_RoundTripsThePublishedField_SoTheSprayReadsTheDrawnSea()
        {
            WaveTrains sea = WaveMath.TrainsFrom(new Vector2(6f, -5.3f), 0.55f, WaveFieldSettings.Default);
            PackedWaveField packed = WaveFieldBridge.Pack(in sea);
            WaveTrains back = WaveFieldBridge.UnpackTrains(in packed, WaveFieldSettings.Default.Gravity);
            Assert.AreEqual(sea.Count, back.Count);
            Assert.AreEqual(sea.DominantIndex, back.DominantIndex);
            Assert.AreEqual(sea.CrestSharpening, back.CrestSharpening, 1e-6f);
            for (int i = 0; i < sea.Count; i++)
            {
                WaveTrain a = sea[i], b = back[i];
                Assert.AreEqual(a.Wavelength, b.Wavelength, a.Wavelength * 1e-5f, $"train {i} wavelength");
                Assert.AreEqual(a.Amplitude, b.Amplitude, 1e-6f, $"train {i} amplitude");
                Assert.AreEqual(a.PhaseSpeed, b.PhaseSpeed, a.PhaseSpeed * 1e-5f, $"train {i} celerity");
                Assert.AreEqual(a.Direction.x, b.Direction.x, 1e-6f);
                Assert.AreEqual(a.Direction.y, b.Direction.y, 1e-6f);
            }
            // …and the field they describe is the same field: the same height under the same point.
            var at = new Vector2(37f, -12f);
            WaveSample sa = WaveMath.Sample(at, 0.0, in sea, 1f);
            WaveSample sb = WaveMath.Sample(at, 0.0, in back, 1f);
            Assert.AreEqual(sa.Height, sb.Height, 1e-4f, "the unpacked field samples as the packed one");
            Assert.AreEqual(0, WaveFieldBridge.UnpackTrains(PackedWaveField.Empty, 9.81f).Count, "an empty publish is an empty sea");
        }

        [Test]
        public void TheEmitter_ReadsThePublishedField_AtTheDrawnScale()
        {
            string src = File.ReadAllText(EmitterPath, Encoding.UTF8);
            StringAssert.Contains("WaveFieldBridge.UnpackTrains(WaveFieldBridge.ReadPublishedField()", src,
                "the spray must read the PUBLISHED field — the animator's travel is in it — not the pure sim's");
            StringAssert.Contains("FoamInjectionRegistry.DrawnWaveScale", src,
                "…at the DRAWN scale, or it would throw spray off a bore the water is not drawing");
            StringAssert.Contains("BreakerMath.SurfAt(", src, "one bore, one computation");
            StringAssert.DoesNotContain("new System.Random", src);
            StringAssert.DoesNotContain("Random.value", src);
        }

        [Test]
        public void TheDefaults_ShipTheSprayOn_ButOnlyOffAPlungingCrest()
        {
            SurfSprayConfig d = SurfSprayConfig.Default;
            Assert.GreaterOrEqual(d.PlungingGate, BreakerSettings.Default.SpillingLimit * 0.4f,
                "the plunging gate must sit past the spilling regime, or a beach would throw spray");
            Assert.Greater(d.BoreGate, 0.5f, "the lip throws AT arrival, not all through the pulse");
            Assert.Greater(d.MaxWisps, 0);
            Assert.That(d.ProbeCells, Is.InRange(4, 24));
        }

        // =========================================================================================
        //  The MASTER left the code for GameConfig (asked at #699's review) — and the shape it left in
        // =========================================================================================

        [Test]
        public void WithNoConfigWiredAtAll_TheSprayStillShipsON()
        {
            // "No config" must not be a silent way to switch off a feature the owner ruled ON. A burst
            // cannot be judged from a plate, so the emitter's default has to be the ruling itself.
            GameServices.Reset();
            try
            {
                Assert.AreEqual(1f, GameServices.SurfSprayIntensity, 1e-6f,
                    "with no GameConfig wired the lip spray reads the shipped burst, not silence");
            }
            finally { GameServices.Reset(); }
        }

        [Test]
        public void AConfigThatPredatesTheDial_StillShipsTheSprayON()
        {
            // ⭐ THE WHOLE REASON THE DIAL IS STORED AS AN OFFSET. A serialized field the shipped YAML
            // does not carry deserializes to ZERO (that is what GameConfigAssetCoverageTests exists to
            // catch), so a plain "SurfSprayIntensity" would have read 0 — silence — in every asset older
            // than the PR that added it, against the ruling, with nothing anywhere saying so. Zero is a
            // value this system produces by accident; it must therefore mean "as shipped".
            var stale = ScriptableObject.CreateInstance<GameConfig>();
            try
            {
                stale.SurfSprayIntensityOffset = 0f;      // exactly what a missing YAML key produces
                Assert.AreEqual(1f, stale.SurfSprayIntensity, 1e-6f,
                    "an asset with no key for the dial must read the SHIPPED burst, not 0");

                GameServices.Config = stale;
                Assert.AreEqual(1f, GameServices.SurfSprayIntensity, 1e-6f,
                    "…and so must the service the emitter actually reads");
            }
            finally { GameServices.Reset(); Object.DestroyImmediate(stale); }
        }

        [Test]
        public void TheDialReachesSilenceAndDouble()
        {
            var config = ScriptableObject.CreateInstance<GameConfig>();
            try
            {
                config.SurfSprayIntensityOffset = -1f;
                Assert.AreEqual(0f, config.SurfSprayIntensity, 1e-6f, "-1 is silence — the owner can turn it off");
                config.SurfSprayIntensityOffset = 1f;
                Assert.AreEqual(2f, config.SurfSprayIntensity, 1e-6f, "+1 is twice the shipped burst");
                config.SurfSprayIntensityOffset = -0.5f;
                Assert.AreEqual(0.5f, config.SurfSprayIntensity, 1e-6f, "and it is linear in between");
            }
            finally { Object.DestroyImmediate(config); }
        }

        [Test]
        public void TheEmitterReadsTheOwnersDial_NotACodeDefault()
        {
            // The emitter installs its own host at runtime, so anything serialized ON it is a code
            // default nobody can reach. Source-read, because the alternative is a Play-mode particle
            // count: what matters is WHERE the number comes from.
            string src = File.ReadAllText(EmitterPath, Encoding.UTF8);
            StringAssert.Contains("GameServices.SurfSprayIntensity", src,
                "the master must come from GameConfig through GameServices (rule 6)");
            StringAssert.DoesNotContain("_config.Intensity", src,
                "…and never from a value serialized on a runtime-installed host");
        }
    }
}
