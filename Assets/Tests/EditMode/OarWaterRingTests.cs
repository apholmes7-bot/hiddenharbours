using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>Each catch sends a ring</b> (the owner's 11-21 reshoot, "Yes, fix then reshoot": the oar splashes
    /// had shrunk to pale specks with no rings). With the oars joined to the one foam sheet the ring is not a
    /// drawn picture: each blade going in (and, smaller, coming out) asks the hull-wave field for a real
    /// ripple, and the water itself draws it with its lit faces and troughs. Driven by the real stroke, as
    /// <see cref="OarFoamSheetTests"/> is.
    /// </summary>
    public class OarWaterRingTests
    {
        private const float LockHeight = 0.58808f - 0.11f;
        private const float Dt = 1f / 60f;

        private static DoryOarSplashSystem NewSystem(in OarSplashConfig cfg)
            => new DoryOarSplashSystem(cfg.FoamPoolSize, cfg.DropletPoolSize, cfg.RipplePoolSize, cfg.DripPoolSize);

        private static DoryOarSplashSystem.OarFrame Frame(float phase, in OarSplashConfig cfg, int side, Vector2 boat)
        {
            DoryOarMeshPose.Pose pose = DoryOarMeshPose.Row(phase / DoryOarMath.StrokeColumns);
            var outboard = new Vector2(side, 0f);
            return new DoryOarSplashSystem.OarFrame
            {
                Stroking = true,
                BladeHeightMeters = DoryOarSplashMath.BladeHeightMeters(LockHeight, cfg.BladeReachMeters, pose.DipDegrees),
                BladeWorld = boat + outboard * 1.2f + new Vector2(0f, -0.3f * Mathf.Sin(phase)),
                TipOnWaterWorld = boat + outboard * 1.45f,
                Outboard = outboard,
                Aft = Vector2.down,
                Effort01 = 1f,
                StrokeDirection = 1f,
                GroundSquash = 0.87f,
                HeightToScreen = 0.5f,
            };
        }

        private delegate void StepWatch(DoryOarSplashSystem sys, DoryOarSplashSystem.OarFrame port);

        private static void Row(DoryOarSplashSystem sys, in OarSplashConfig cfg, in FoamSheetSettings sheet,
                                int strokes, StepWatch each)
        {
            float fps = DoryOarMath.DefaultStrokeFramesPerSecond;
            float phase = DoryOarMath.StrokeColumns * 0.5f;
            int steps = Mathf.RoundToInt(strokes * DoryOarMath.StrokeColumns / (fps * Dt));
            Vector2 boat = Vector2.zero;
            for (int i = 0; i < steps; i++)
            {
                phase = DoryOarMath.AdvanceStrokePhase(phase, 1f, fps, Dt, DoryOarMath.StrokeColumns);
                boat += Vector2.up * (1.75f * Dt);
                var port = Frame(phase, cfg, -1, boat);
                var star = Frame(phase, cfg, +1, boat);
                sys.Step(in port, in star, 1.75f, Vector2.zero, Dt, in cfg, in sheet);
                each(sys, port);
            }
        }

        [Test]
        public void EveryCatch_AsksForOneWaterRing_AtTheBlade_SizedByTheDial()
        {
            OarSplashConfig cfg = OarSplashConfig.Default;
            FoamSheetSettings sheet = FoamSheetSettings.Default;
            Assert.Greater(sheet.OarRingHeightMetres, 0f, "the shipped dials send a ring");
            var sys = NewSystem(in cfg);
            int lastCatches = 0, lastReleases = 0, catches = 0, catchRings = 0, releaseRings = 0, releases = 0;
            Row(sys, cfg, sheet, 4, (s, port) =>
            {
                int c = s.CatchCount(0) + s.CatchCount(1), r = s.ReleaseCount(0) + s.ReleaseCount(1);
                int newCatchRings = 0, newReleaseRings = 0;
                for (int i = 0; i < s.WaveRingCount; i++)
                {
                    DoryOarSplashSystem.WaveRing ring = s.GetWaveRing(i);
                    Assert.LessOrEqual(ring.HeightMetres, sheet.OarRingHeightMetres + 1e-6f, "never taller than the dial");
                    if (ring.Catch)
                    {
                        newCatchRings++;
                        Assert.GreaterOrEqual(ring.HeightMetres, sheet.OarRingHeightMetres * cfg.MinStrength - 1e-6f,
                            "a catch's ring is at least the gentlest stroke's share of the dial");
                    }
                    else
                    {
                        newReleaseRings++;
                        Assert.LessOrEqual(ring.HeightMetres, sheet.OarRingHeightMetres * sheet.OarReleaseRingShare + 1e-6f,
                            "the release ring is the smaller share");
                    }
                }
                Assert.AreEqual(c - lastCatches, newCatchRings, "one ring per catch, laid the frame it fires");
                catches += c - lastCatches; catchRings += newCatchRings;
                releases += r - lastReleases; releaseRings += newReleaseRings;
                lastCatches = c; lastReleases = r;
            });
            Assert.Greater(catches, 3, "the stroke fired its catches");
            Assert.AreEqual(catches, catchRings);
            Assert.Greater(releaseRings, 0, "a blade coming out sends its smaller ring too");
            Assert.LessOrEqual(releaseRings, releases);
        }

        [Test]
        public void TheRingDialAtZero_SendsNoRing_AndTheOlderPicturePathSendsNone()
        {
            OarSplashConfig cfg = OarSplashConfig.Default;
            FoamSheetSettings sheet = FoamSheetSettings.Default;
            sheet.OarRingHeightMetres = 0f;
            var sys = NewSystem(in cfg);
            Row(sys, cfg, sheet, 2, (s, _) => Assert.AreEqual(0, s.WaveRingCount, "ring dial 0: none"));

            // With the oar dial off the oars draw their own ring pictures instead: no water ring on top.
            sheet = FoamSheetSettings.Default;
            sheet.Oar = 0f;
            sys = NewSystem(in cfg);
            int pictures = 0;
            Row(sys, cfg, sheet, 2, (s, _) =>
            {
                Assert.AreEqual(0, s.WaveRingCount, "the A/B path keeps its drawn rings only");
                pictures = Mathf.Max(pictures, s.Ripples.AliveCount);
            });
            Assert.Greater(pictures, 0);
        }

        [Test]
        public void TheEmitter_HandsEachRing_ToTheHullWaveField_UnderTheDorysOwnId()
        {
            var field = new HullWaveField();
            GameConfig keepConfig = GameServices.Config;
            var config = ScriptableObject.CreateInstance<GameConfig>();
            config.HullWaves = HullWaveSettings.Default;
            config.FoamSheet = FoamSheetSettings.Default;
            HullWaves.Install(field);
            GameServices.Config = config;
            try
            {
                Assert.IsTrue(GameServices.HullWaves.Enabled, "hull waves ship on");
                OarSplashConfig cfg = OarSplashConfig.Default;
                FoamSheetSettings sheet = FoamSheetSettings.Default;
                var sys = NewSystem(in cfg);
                const int owner = 4242;
                int asked = 0, sent = 0;
                Row(sys, cfg, sheet, 3, (s, _) =>
                {
                    asked += s.WaveRingCount;
                    sent += DoryOarSplashEmitter.SendWaveRings(s, in sheet, owner);
                });
                Assert.Greater(asked, 0);
                Assert.AreEqual(asked, sent, "the field takes every ring");
                Assert.AreEqual(field.Count, field.CountOwnedBy(owner), "every ripple is the dory's own");
                HullWaveSource src = HullWaves.MakeRipple(Vector2.one, sheet.OarRingHeightMetres,
                    sheet.OarRingWavelengthMetres, 0.0, GameServices.HullWaves, owner);
                Assert.AreEqual(owner, src.Owner);
                Assert.Greater(src.Amplitude, 0f);

                // Ring dial 0 on the live config: nothing reaches the field.
                FoamSheetSettings off = sheet; off.OarRingWavelengthMetres = 0f;
                Assert.AreEqual(0, DoryOarSplashEmitter.SendWaveRings(sys, in off, owner));
            }
            finally
            {
                HullWaves.Uninstall(field);
                GameServices.Config = keepConfig;
                Object.DestroyImmediate(config);
            }
        }

        // The owner's 11-21 reshoot: "no hull wake at rowing speed". At the dory's rowing speed both of her
        // wakes run while she moves (a recapture with the hour HELD, not the clock paused, shows them): the
        // water's own V, laid above the HullWaves.MinSpeed threshold, and the thinner stern trail of the
        // one foam, both growing with speed from data.
        [Test]
        public void AtRowingSpeed_TheDoryLaysHerWaveVAndHerTrail_AboveTheDataThreshold()
        {
            const float rowing = 1.75f;            // the 11-20 clip's steady rowing speed, m/s
            const float doryLength = 4.5f;         // her injector's hull: twice its 2.25 m stern offset (live probe)
            HullWaveSettings s = HullWaveSettings.Default;
            Assert.IsTrue(s.Enabled);
            Assert.Greater(rowing, s.MinSpeed, "rowing speed is above the wave threshold");
            Assert.IsTrue(HullWaveMath.IsSmallHull(doryLength, in s));
            float crest = HullWaveMath.CrestHeight(rowing, doryLength, in s);
            Assert.Greater(crest, 0f, "she lays a V of real waves at rowing speed");
            Assert.LessOrEqual(crest, s.MaxHeightMetres + 1e-6f, "held under the wave cap");
            Assert.AreEqual(0f, HullWaveMath.CrestHeight(s.MinSpeed * 0.5f, doryLength, in s), "drifting: none");

            float trail = HullWaveMath.TrailFoamStrength(1f, true, doryLength, in s);
            Assert.AreEqual(s.SmallHullTrailFoamShare, trail, 1e-6f, "her trail's share is the data's");
            Assert.Greater(trail, 0f, "she lays a stern trail of the one foam while she moves");
            Assert.Less(trail, s.TrailFoamShare + 1e-6f, "and a gentler one than a big hull's");

            // Both scale with speed: slower rows lay less, harder rows more (the FoamInjector's wake knee).
            var host = new GameObject("oarwake-test-injector");
            try
            {
                var inj = host.AddComponent<HiddenHarbours.Art.FoamInjector>();
                float knee = Field(inj, "_wakeSpeedKnee"), exponent = Field(inj, "_wakeExponent");
                float slow = HiddenHarbours.Art.FoamBuffer.Shape01(1f, knee, exponent);
                float row = HiddenHarbours.Art.FoamBuffer.Shape01(rowing, knee, exponent);
                float hard = HiddenHarbours.Art.FoamBuffer.Shape01(2.5f, knee, exponent);
                Assert.Greater(row, 0f, "the trail is laid at rowing speed, not only while she gathers way");
                Assert.Less(slow, row);
                Assert.Less(row, hard);
            }
            finally { Object.DestroyImmediate(host); }
            Assert.Less(HullWaveMath.CrestHeight(1f, doryLength, in s), crest + 1e-6f);
        }

        private static float Field(object o, string name)
        {
            var f = o.GetType().GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.IsNotNull(f, name);
            return (float)f.GetValue(o);
        }

        [Test]
        public void TheRingDials_ComeFromGameConfig()
        {
            var config = ScriptableObject.CreateInstance<GameConfig>();
            try
            {
                config.FoamSheet = FoamSheetSettings.Default;
                config.FoamSheet.OarRingHeightMetres = 0.11f;
                config.FoamSheet.OarReleaseRingShare = 0.25f;
                OarSplashConfig cfg = OarSplashConfig.Default;
                var sys = NewSystem(in cfg);
                float tallest = 0f, tallestRelease = 0f;
                Row(sys, cfg, config.FoamSheet, 3, (s, _) =>
                {
                    for (int i = 0; i < s.WaveRingCount; i++)
                    {
                        var r = s.GetWaveRing(i);
                        if (r.Catch) tallest = Mathf.Max(tallest, r.HeightMetres);
                        else tallestRelease = Mathf.Max(tallestRelease, r.HeightMetres);
                    }
                });
                Assert.Greater(tallest, 0.11f * cfg.MinStrength - 1e-6f);
                Assert.LessOrEqual(tallest, 0.11f + 1e-6f);
                Assert.LessOrEqual(tallestRelease, 0.11f * 0.25f + 1e-6f);
            }
            finally { Object.DestroyImmediate(config); }
        }
    }
}
