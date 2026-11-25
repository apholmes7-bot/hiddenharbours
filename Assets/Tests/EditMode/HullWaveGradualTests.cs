using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The owner's 11-21 note on the rowing reshoot: "the wake is very flickery and strobe like, it should be
    /// more gradual and slow" (then: "its not the foam, its the very circular wake wave pattern"). Measured:
    /// the dory's short waves (0.6 m) ran at her own speed, so their crests passed one spot about 3 times a
    /// second (a punt's about 9.5), and an oar ring was born at full height. These pin the dials that slow and
    /// soften them: the crest-rate cap, the splash ring's rise, and splash rings never pushing a hull's wave
    /// out of the pool.
    /// </summary>
    public class HullWaveGradualTests
    {
        const string ConfigPath = "Assets/_Project/Data/Config/GameConfig.asset";
        const float DoryLength = 4.5f, Rowing = 1.8f, Frame = 1f / 60f;

        static HullWaveSettings Shipped()
        {
            var cfg = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath);
            Assert.IsNotNull(cfg, ConfigPath);
            return cfg.HullWaves;
        }

        [Test]
        public void TheShippedConfig_CarriesTheGradualDials()
        {
            HullWaveSettings s = Shipped();
            Assert.Greater(s.MaxCrestHertz, 0f, "a crest-rate cap ships");
            Assert.LessOrEqual(s.MaxCrestHertz, 1f, "slow: no more than one crest a second past one spot");
            Assert.Greater(s.RippleRiseShare, 0f, "a splash ring rises, it is not born at full height");
            Assert.IsTrue(s.RipplesNeverEvict, "a splash ring never pushes out a hull's wave");
            Assert.AreEqual(0f, HullWaveSettings.Default.MaxCrestHertz, "the code default is the old law; the config ships the cap");
        }

        [Test]
        public void TheCrestSpeed_IsHeldToTheCap_TimesTheWavelength()
        {
            HullWaveSettings s = HullWaveSettings.Default;
            Assert.AreEqual(1.8f, HullWaveMath.CrestSpeed(1.8f, 0.6f, in s), 0f, "cap 0: unchanged");
            s.MaxCrestHertz = 0.5f;
            Assert.AreEqual(0.3f, HullWaveMath.CrestSpeed(1.8f, 0.6f, in s), 1e-6f, "0.5 Hz x 0.6 m");
            Assert.AreEqual(1.5f, HullWaveMath.CrestSpeed(1.5f, 4f, in s), 0f, "a long slow wave under the cap keeps its speed");
        }

        [Test]
        public void ARowedDory_LaysWavesWhoseCrests_PassASpotNoFasterThanTheCap()
        {
            HullWaveSettings s = Shipped();
            var field = new HullWaveField();
            var chain = new HullWaveChain();
            Vector2 p = Vector2.zero;
            for (int i = 0; i <= 300; i++)
            {
                chain.Step(field, p, i * Frame, Rowing, DoryLength, 1f, 15, 9, in s);
                field.Advance(i * Frame);
                p += new Vector2(-Rowing * Frame, 0f);
            }
            Assert.Greater(field.Count, 0);
            for (int i = 0; i < field.Count; i++)
            {
                float hz = field[i].Wavenumber * field[i].PhaseSpeed / (2f * Mathf.PI);
                // A ring's wavelength is scattered a little after the speed is set: a ring shortened by the
                // scatter share runs cap / (1 - scatter); allow that and no more.
                Assert.LessOrEqual(hz, s.MaxCrestHertz / (1f - s.SmallHullWavelengthScatter) + 1e-4f, $"source {i}");
            }
        }

        // The height at one fixed spot of water, a rowed dory passing, sampled every 1/60 s: with the shipped
        // dials it changes by only a small share of its swing per frame (gradual); with the old law (no cap) it
        // jumped by far more. World-anchored by construction: a source never moves once laid.
        [Test]
        public void AtOneSpot_TheWaterChangesGradually_FrameToFrame()
        {
            float shipped = WorstFrameStep(Shipped());
            HullWaveSettings old = Shipped();
            old.MaxCrestHertz = 0f;
            float before = WorstFrameStep(old);
            Assert.Less(shipped, 0.2f, "shipped: at most a fifth of the swing in one frame");
            Assert.Greater(before, shipped * 2f, "the old law stepped far harder (the strobe the owner saw)");
        }

        static float WorstFrameStep(HullWaveSettings s)
        {
            var field = new HullWaveField();
            var chain = new HullWaveChain();
            Vector2 p = new Vector2(6f, 0f);
            Vector2 spot = new Vector2(0f, 0.8f);
            float prev = 0f, swing = 0f, worst = 0f;
            bool primed = false;
            for (int i = 0; i <= 480; i++)
            {
                double now = i * Frame;
                chain.Step(field, p, now, Rowing, DoryLength, 1f, 15, 9, in s);
                field.Advance(now);
                float h = field.SampleHeight(spot, s.SpreadMetres);
                swing = Mathf.Max(swing, Mathf.Abs(h));
                if (primed) worst = Mathf.Max(worst, Mathf.Abs(h - prev));
                prev = h; primed = true;
                p += new Vector2(-Rowing * Frame, 0f);
            }
            Assert.Greater(swing, 0f, "the dory's waves reached the spot");
            return worst / swing;
        }

        [Test]
        public void ASplashRing_RisesFromNothing_ThenFadesSmoothlyToNothing()
        {
            HullWaveSettings s = Shipped();
            HullWaveSource r = HullWaves.MakeRipple(Vector2.zero, 0.05f, 0.8f, 10.0, in s);
            Assert.AreEqual(0f, r.AmplitudeAt(10.0), 1e-6f, "born at nothing, not at full height");
            float prev = 0f; bool falling = false;
            for (double t = 0; t <= r.Lifetime + 0.1; t += Frame)
            {
                float a = r.AmplitudeAt(10.0 + t);
                if (a < prev - 1e-7f) falling = true;
                else if (falling) Assert.LessOrEqual(a, prev + 1e-7f, $"once fading it never rises again (t {t:0.00})");
                Assert.LessOrEqual(Mathf.Abs(a - prev), 0.1f * r.Amplitude, $"no step over a tenth of its height in a frame (t {t:0.00})");
                prev = a;
            }
            Assert.AreEqual(0f, r.AmplitudeAt(10.0 + r.Lifetime), 0f, "gone exactly at the end of its life");
            float hz = r.Wavenumber * r.PhaseSpeed / (2f * Mathf.PI);
            Assert.LessOrEqual(hz, s.MaxCrestHertz + 1e-5f, "its crests crawl under the cap too");
        }

        [Test]
        public void ASplashRing_NeverPushesAHullsWave_OutOfAFullPool()
        {
            var field = new HullWaveField(4);
            for (int i = 0; i < 4; i++)
                field.Add(new HullWaveSource { Position = new Vector2(i, 0f), Amplitude = 0.01f, Wavenumber = 10f,
                                               PacketWidth = 0.5f, Lifetime = 3f, Owner = 9 });
            var ripple = new HullWaveSource { Amplitude = 0.05f, Wavenumber = 8f, PacketWidth = 0.5f, Lifetime = 3f };
            Assert.IsFalse(field.AddIfRoom(in ripple), "full: the ring is dropped");
            Assert.AreEqual(4, field.CountOwnedBy(9), "and no hull wave gave way");
            Assert.IsTrue(field.Add(in ripple), "the plain add still swaps out the weakest");
            Assert.AreEqual(3, field.CountOwnedBy(9));

            var roomy = new HullWaveField(4);
            Assert.IsTrue(roomy.AddIfRoom(in ripple), "a free slot takes it");
        }
    }
}
