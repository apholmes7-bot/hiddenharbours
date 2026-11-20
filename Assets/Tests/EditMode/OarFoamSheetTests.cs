using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>The oars' foam is the one foam</b> (the owner, 2026-11-19: "wake foam should be the same as any
    /// foam, not a drawn special piece speical to just the wake. there needs to be consistency amongst the
    /// in game foam"). With the sheet's oar dial on, a blade going in, pulling and coming out lays foam into
    /// the sea's foam buffer (through the Core <see cref="FoamSplashes"/> seam) and the oars draw no foam
    /// pictures or rings of their own. Driven by the real stroke, as <see cref="DoryOarSplashTests"/> is.
    /// </summary>
    public class OarFoamSheetTests
    {
        private const float LockHeight = 0.5156f - 0.11f;
        private const float Dt = 1f / 60f;
        private const string EmitterPath = "Assets/_Project/Code/Boats/DoryOarSplashEmitter.cs";

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

        private delegate void StepWatch(DoryOarSplashSystem sys, bool portIn);

        private static void Row(DoryOarSplashSystem sys, in OarSplashConfig cfg, in FoamSheetSettings sheet,
                                int strokes, StepWatch each = null)
        {
            float fps = DoryOarMath.DefaultStrokeFramesPerSecond;
            float phase = DoryOarMath.StrokeColumns * 0.5f;
            int steps = Mathf.RoundToInt(strokes * DoryOarMath.StrokeColumns / (fps * Dt));
            Vector2 boat = Vector2.zero;
            for (int i = 0; i < steps; i++)
            {
                phase = DoryOarMath.AdvanceStrokePhase(phase, 1f, fps, Dt, DoryOarMath.StrokeColumns);
                boat += Vector2.up * (2f * Dt);
                var port = Frame(phase, cfg, -1, boat);
                var star = Frame(phase, cfg, +1, boat);
                sys.Step(in port, in star, 2f, Vector2.zero, Dt, in cfg, in sheet);
                each?.Invoke(sys, DoryOarSplashMath.IsImmersed(port.BladeHeightMeters));
            }
        }

        [Test]
        public void TheOarSplash_DepositsIntoTheSharedSheet_AtTheCatchThePullAndTheRelease()
        {
            OarSplashConfig cfg = OarSplashConfig.Default;
            FoamSheetSettings sheet = FoamSheetSettings.Default;
            Assert.IsTrue(sheet.OarJoinsSheet, "the shipped dials join the oars to the sheet");
            var sys = NewSystem(in cfg);
            int pull = 0, release = 0, catches = 0, lastCatch = 0, lastRelease = 0;
            float biggest = 0f;
            Row(sys, cfg, sheet, 3, (s, portIn) =>
            {
                bool laid = s.TryGetSheetDeposit(DoryOarSplashSystem.Port, out var d);
                int c = s.CatchCount(DoryOarSplashSystem.Port), r = s.ReleaseCount(DoryOarSplashSystem.Port);
                if (laid)
                {
                    Assert.Greater(d.Vigour, 0f, "a working blade marks its foam fresh");
                    // The catch and the release lay the burst's size; the pull lays the narrower smear.
                    if (c > lastCatch)
                    {
                        catches++;
                        biggest = Mathf.Max(biggest, d.Amount);
                        Assert.AreEqual(sheet.OarCatchRadius, d.Radius, 1e-6f, "the catch's burst is the dial's size");
                    }
                    else if (r > lastRelease)
                    {
                        release++;
                        Assert.AreEqual(sheet.OarCatchRadius, d.Radius, 1e-6f, "the release's burst is the dial's size");
                    }
                    else if (portIn)
                    {
                        pull++;
                        Assert.AreEqual(sheet.OarRadius, d.Radius, 1e-6f, "the pull's smear is the dial's size");
                    }
                }
                else
                {
                    Assert.IsFalse(portIn && c == lastCatch && c > 0, "a blade in the water always lays foam");
                    Assert.IsFalse(r > lastRelease, "a release always leaves foam");
                }
                lastCatch = c; lastRelease = r;
            });
            Assert.Greater(catches, 0, "no catch laid foam");
            Assert.Greater(pull, 0, "no pull laid foam");
            Assert.Greater(release, 0, "no release laid foam");
            Assert.GreaterOrEqual(biggest, sheet.OarCatch * cfg.MinStrength, "the catch lays its own white");
        }

        [Test]
        public void NoFoamPuffsPuddlesOrRings_AreLaidByTheOars_WhenTheyJoinTheSheet()
        {
            OarSplashConfig cfg = OarSplashConfig.Default;
            var sys = NewSystem(in cfg);
            int droplets = 0;
            Row(sys, cfg, FoamSheetSettings.Default, 3, (s, _) =>
            {
                Assert.AreEqual(0, s.Foam.AliveCount, "no foam puff (a FoamRaft blob) is laid by the oars");
                Assert.AreEqual(0, s.Ripples.AliveCount, "no drawn ring is laid by the oars");
                droplets = Mathf.Max(droplets, s.Droplets.AliveCount);
            });
            Assert.Greater(droplets, 0, "the catch still throws a little water");
        }

        [Test]
        public void WithTheOarDialOff_TheOlderPuffsComeBack_AndNothingGoesToTheSheet()
        {
            OarSplashConfig cfg = OarSplashConfig.Default;
            FoamSheetSettings sheet = FoamSheetSettings.Default;
            sheet.Oar = 0f;
            var sys = NewSystem(in cfg);
            int foam = 0;
            Row(sys, cfg, sheet, 2, (s, portIn) =>
            {
                Assert.IsFalse(s.TryGetSheetDeposit(DoryOarSplashSystem.Port, out var p));
                Assert.IsFalse(s.TryGetSheetDeposit(DoryOarSplashSystem.Starboard, out var q));
                foam = Mathf.Max(foam, s.Foam.AliveCount);
            });
            Assert.Greater(foam, 0, "the A/B path still draws its puffs");
            Assert.IsTrue(DoryOarSplashEmitter.DrawsFoamPictures(in sheet));
            Assert.IsFalse(DoryOarSplashEmitter.DrawsFoamPictures(FoamSheetSettings.Default));
        }

        [Test]
        public void NoFoamRaftSprite_IsSpawnedByTheOars_WhenTheyJoinTheSheet()
        {
            string src = File.ReadAllText(EmitterPath, Encoding.UTF8).Replace("\r\n", "\n");
            // The droplets and drips draw the emitter's own dot, never the wake's foam rafts.
            StringAssert.Contains("_dropletRenderers[i].sprite = _dotSprite", src);
            StringAssert.Contains("_dripRenderers[i].sprite = _dotSprite", src);
            StringAssert.DoesNotContain("BindSprites(_dropletRenderers", src);
            StringAssert.DoesNotContain("BindSprites(_dripRenderers", src);
            // The foam rafts are bound only on the A/B path, after the joined early return.
            int joinedReturn = src.IndexOf("if (!foamPictures)", System.StringComparison.Ordinal);
            int rafts = src.IndexOf("BoatWakeEmitter.SharedFoamRafts;", System.StringComparison.Ordinal);
            Assert.Greater(joinedReturn, 0);
            Assert.Greater(rafts, joinedReturn, "the foam rafts are only reached with the oar dial off");
            // And the dot is a round soft dot: bright at the centre, clear at the corners.
            float[] dot = DoryOarSplashEmitter.DotCoverage(DoryOarSplashEmitter.DotTextureSize);
            int n = DoryOarSplashEmitter.DotTextureSize;
            Assert.Greater(dot[(n / 2) * n + n / 2], 0.9f);
            Assert.AreEqual(0f, dot[0], 1e-6f);
        }

        [Test]
        public void TheOarFoamDials_ComeFromGameConfig()
        {
            var config = ScriptableObject.CreateInstance<GameConfig>();
            try
            {
                config.FoamSheet.OarRadius = 0.41f;
                config.FoamSheet.OarCatchRadius = 0.53f;
                config.FoamSheet.OarCatch = 0.9f;
                OarSplashConfig cfg = OarSplashConfig.Default;
                var sys = NewSystem(in cfg);
                bool smear = false, burst = false;
                Row(sys, cfg, config.FoamSheet, 2, (s, _) =>
                {
                    if (!s.TryGetSheetDeposit(DoryOarSplashSystem.Port, out var d)) return;
                    bool isSmear = Mathf.Abs(d.Radius - 0.41f) < 1e-6f, isBurst = Mathf.Abs(d.Radius - 0.53f) < 1e-6f;
                    Assert.IsTrue(isSmear || isBurst, "every deposit's size is one of the config's two dials");
                    smear |= isSmear;
                    burst |= isBurst;
                });
                Assert.IsTrue(smear, "the pull's smear is the config's OarRadius");
                Assert.IsTrue(burst, "the catch's burst is the config's OarCatchRadius");
            }
            finally { Object.DestroyImmediate(config); }

            string src = File.ReadAllText(EmitterPath, Encoding.UTF8);
            StringAssert.Contains("GameServices.FoamSheet", src, "the emitter reads the live config's dials");
        }

        [Test]
        public void TheSplashSeam_KeepsOneFramesDeposits_UpToItsCap()
        {
            FoamSplashes.Clear();
            try
            {
                const int frame = 1000;
                for (int i = 0; i < FoamSplashes.Capacity; i++)
                    Assert.IsTrue(FoamSplashes.AddOn(frame, Vector2.zero, Vector2.one, 0.2f, 0.1f, 1f));
                Assert.IsFalse(FoamSplashes.AddOn(frame, Vector2.zero, Vector2.one, 0.2f, 0.1f, 1f), "over the cap");
                Assert.AreEqual(FoamSplashes.Capacity, FoamSplashes.CountFor(frame));
                Assert.AreEqual(0, FoamSplashes.CountFor(frame + 1), "a deposit belongs to its own frame only");
                Assert.IsFalse(FoamSplashes.AddOn(frame + 1, Vector2.zero, Vector2.one, 0.2f, 0f, 1f), "nothing to lay");
                Assert.AreEqual(0, FoamSplashes.CountFor(frame), "a new frame starts empty");
            }
            finally { FoamSplashes.Clear(); }
        }

        [Test]
        public void TheWakeBubbles_RetireWithTheDrawnFoam()
        {
            WakeBubbleConfig bubbles = WakeBubbleConfig.Default;
            HullWaveSettings waves = HullWaveSettings.Default;
            Assert.IsTrue(bubbles.Enabled);
            Assert.IsFalse(BoatWakeEmitter.BubblesDrawn(in bubbles, in waves),
                "while the water draws the wake's foam, the bubble blobs are not drawn beside it");
            waves.RetireDrawnFoam = false;
            Assert.IsTrue(BoatWakeEmitter.BubblesDrawn(in bubbles, in waves), "the A/B switch brings them back");
        }
    }
}
