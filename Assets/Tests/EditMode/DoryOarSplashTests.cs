using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Boats;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>The oar splash fires from the stroke the oar is actually drawn in</b> (owner ruling
    /// 2026-11-13: the dory's oars "MUST splash and foam").
    ///
    /// <para>These drive <see cref="DoryOarSplashSystem"/> with the real stroke: the same
    /// <see cref="DoryOarMath.AdvanceStrokePhase"/> accumulator and the same
    /// <see cref="DoryOarMeshPose.Row"/> pose the mesh oars are posed from, at the dory's own
    /// oarlock height. If the rig's stroke or the lock ever changes so the blade no longer reaches
    /// the water, the first test says so instead of the game going quietly dry.</para>
    /// </summary>
    public class DoryOarSplashTests
    {
        // The dory's own facts: oarlock z 0.5156 m (DoryOarPortPropMesh.PivotLocalMeters) over a
        // 0.11 m resting draught (DoryIsoHullMesh.RestingDraftMeters).
        private const float LockHeight = 0.5156f - 0.11f;
        private const float Dt = 1f / 60f;

        private static OarSplashConfig Config => OarSplashConfig.Default;

        private static DoryOarSplashSystem NewSystem(in OarSplashConfig cfg)
            => new DoryOarSplashSystem(cfg.FoamPoolSize, cfg.DropletPoolSize);

        private static DoryOarSplashSystem.OarFrame Rowing(float phase, in OarSplashConfig cfg,
                                                           float effort = 1f, float direction = 1f)
        {
            DoryOarMeshPose.Pose pose = DoryOarMeshPose.Row(phase / DoryOarMath.StrokeColumns);
            return FrameFor(pose, true, cfg, effort, direction);
        }

        private static DoryOarSplashSystem.OarFrame FrameFor(in DoryOarMeshPose.Pose pose, bool stroking,
                                                             in OarSplashConfig cfg, float effort = 1f,
                                                             float direction = 1f)
            => new DoryOarSplashSystem.OarFrame
            {
                Stroking = stroking,
                BladeHeightMeters = DoryOarSplashMath.BladeHeightMeters(LockHeight, cfg.BladeReachMeters,
                                                                        pose.DipDegrees),
                BladeWorld = new Vector2(1f, 0f),
                Outboard = Vector2.right,
                Aft = Vector2.down,
                Effort01 = effort,
                StrokeDirection = direction,
            };

        /// <summary>Row both oars for <paramref name="strokes"/> whole cycles from the top of the
        /// recovery (blade highest), the port oar at <paramref name="direction"/>.</summary>
        private static void Row(DoryOarSplashSystem sys, in OarSplashConfig cfg, int strokes, float direction)
        {
            float fps = DoryOarMath.DefaultStrokeFramesPerSecond;
            float phase = DoryOarMath.StrokeColumns * 0.5f;   // dip = 6 − 22 = −16°: clear of the water
            int steps = Mathf.RoundToInt(strokes * DoryOarMath.StrokeColumns / (fps * Dt));
            for (int i = 0; i < steps; i++)
            {
                phase = DoryOarMath.AdvanceStrokePhase(phase, direction, fps, Dt, DoryOarMath.StrokeColumns);
                var frame = Rowing(phase, cfg, 1f, direction);
                sys.Step(in frame, in frame, 1f, Vector2.zero, Dt, in cfg);
            }
        }

        [Test]
        public void TheDorysStroke_PutsTheBladeInTheWater_AndTakesItOutAgain()
        {
            OarSplashConfig cfg = Config;
            float deepest = DoryOarSplashMath.BladeHeightMeters(LockHeight, cfg.BladeReachMeters,
                                                                DoryOarMeshPose.Row(0f).DipDegrees);
            float highest = DoryOarSplashMath.BladeHeightMeters(LockHeight, cfg.BladeReachMeters,
                                                                DoryOarMeshPose.Row(0.5f).DipDegrees);
            Assert.Less(deepest, 0f, "at the bottom of the stroke the dory's blade must be under the " +
                "waterline, or no stroke ever splashes. Lock height or blade reach has drifted from the rig.");
            Assert.Greater(highest, 0f, "on the recovery the blade must clear the water, or it never " +
                "re-enters and the catch fires once and never again.");
        }

        [Test]
        public void ACatch_FiresOncePerStroke_PerOar()
        {
            OarSplashConfig cfg = Config;
            var sys = NewSystem(cfg);
            Row(sys, cfg, strokes: 5, direction: 1f);

            Assert.AreEqual(5, sys.CatchCount(DoryOarSplashSystem.Port), "port: one catch per stroke");
            Assert.AreEqual(5, sys.CatchCount(DoryOarSplashSystem.Starboard), "starboard: one catch per stroke");
        }

        [Test]
        public void BackingWater_AlsoCatchesOncePerStroke()
        {
            OarSplashConfig cfg = Config;
            var sys = NewSystem(cfg);
            Row(sys, cfg, strokes: 4, direction: -1f);

            Assert.AreEqual(4, sys.CatchCount(DoryOarSplashSystem.Port));
        }

        [Test]
        public void ACatch_ThrowsDroplets_AndThePullLeavesFoam()
        {
            OarSplashConfig cfg = Config;
            var sys = NewSystem(cfg);
            Row(sys, cfg, strokes: 1, direction: 1f);

            Assert.Greater(sys.Foam.AliveCount, 0, "a stroke that entered the water leaves foam behind it");
            // The droplets live 0.4 s and the stroke ended on the recovery, so look for them at the catch.
            var fresh = NewSystem(cfg);
            var air = Rowing(DoryOarMath.StrokeColumns * 0.5f, cfg);
            var water = Rowing(0f, cfg);
            fresh.Step(in air, in air, 1f, Vector2.zero, Dt, in cfg);
            int fired = fresh.Step(in water, in water, 1f, Vector2.zero, Dt, in cfg);
            Assert.AreEqual(2, fired, "both blades entered on this frame");
            Assert.GreaterOrEqual(fresh.Droplets.AliveCount, 2 * cfg.CatchDropsMin);
        }

        [Test]
        public void OneOarRowing_SplashesOnThatSideOnly()
        {
            OarSplashConfig cfg = Config;
            var sys = NewSystem(cfg);
            var air = Rowing(DoryOarMath.StrokeColumns * 0.5f, cfg);
            var water = Rowing(0f, cfg);
            var trailing = FrameFor(DoryOarMeshPose.Trailing, false, cfg);

            sys.Step(in air, in trailing, 1f, Vector2.zero, Dt, in cfg);
            sys.Step(in water, in trailing, 1f, Vector2.zero, Dt, in cfg);

            Assert.AreEqual(1, sys.CatchCount(DoryOarSplashSystem.Port));
            Assert.AreEqual(0, sys.CatchCount(DoryOarSplashSystem.Starboard));
        }

        [Test]
        public void ShippedOars_NeverSplash()
        {
            OarSplashConfig cfg = Config;
            var sys = NewSystem(cfg);
            var shipped = FrameFor(DoryOarMeshPose.Resting, false, cfg);
            for (int i = 0; i < 600; i++) sys.Step(in shipped, in shipped, 3f, Vector2.zero, Dt, in cfg);

            Assert.AreEqual(0, sys.CatchCount(DoryOarSplashSystem.Port));
            Assert.AreEqual(0, sys.Foam.AliveCount);
            Assert.AreEqual(0, sys.Droplets.AliveCount);
        }

        [Test]
        public void AnOarThatIsNotBeingRowed_NeverSplashes_EvenAsItsBladeGoesUnder()
        {
            // Under power the layer stows the oars and reports "not stroking"; the same gate covers
            // an empty helm and a boat aground. Drive the blade through the water anyway.
            OarSplashConfig cfg = Config;
            var sys = NewSystem(cfg);
            for (int i = 0; i < 240; i++)
            {
                float phase = (i * 0.1f) % DoryOarMath.StrokeColumns;
                var frame = FrameFor(DoryOarMeshPose.Row(phase / DoryOarMath.StrokeColumns), false, cfg);
                sys.Step(in frame, in frame, 3f, Vector2.zero, Dt, in cfg);
            }

            Assert.AreEqual(0, sys.CatchCount(DoryOarSplashSystem.Port));
            Assert.AreEqual(0, sys.Foam.AliveCount);
            Assert.AreEqual(0, sys.Droplets.AliveCount);
        }

        [Test]
        public void SwitchedOff_RowsDry()
        {
            OarSplashConfig cfg = Config;
            cfg.Enabled = false;
            var sys = NewSystem(cfg);
            Row(sys, cfg, strokes: 3, direction: 1f);

            Assert.AreEqual(0, sys.CatchCount(DoryOarSplashSystem.Port));
            Assert.AreEqual(0, sys.Foam.AliveCount);
        }

        [Test]
        public void ThePools_AreFixed_AndReused()
        {
            OarSplashConfig cfg = Config;
            var sys = NewSystem(cfg);
            var foamPool = sys.Foam.Pool;
            var dropletPool = sys.Droplets.Pool;

            Row(sys, cfg, strokes: 200, direction: 1f);

            Assert.AreSame(foamPool, sys.Foam.Pool, "the foam pool is built once and recycled");
            Assert.AreSame(dropletPool, sys.Droplets.Pool, "the droplet pool is built once and recycled");
            Assert.AreEqual(cfg.FoamPoolSize, sys.Foam.Capacity);
            Assert.AreEqual(cfg.DropletPoolSize, sys.Droplets.Capacity);
            Assert.LessOrEqual(sys.Foam.AliveCount, cfg.FoamPoolSize);
            Assert.AreEqual(200, sys.CatchCount(DoryOarSplashSystem.Port), "every stroke still caught");
        }

        [Test]
        public void TheFoamFades_OnceTheRowingStops()
        {
            OarSplashConfig cfg = Config;
            var sys = NewSystem(cfg);
            Row(sys, cfg, strokes: 2, direction: 1f);
            Assert.Greater(sys.Foam.AliveCount, 0);

            var shipped = FrameFor(DoryOarMeshPose.Resting, false, cfg);
            float longest = cfg.FoamLifetime * (1f + cfg.LifetimeJitter) + 0.5f;
            for (float t = 0f; t < longest; t += Dt) sys.Step(in shipped, in shipped, 0f, Vector2.zero, Dt, in cfg);

            Assert.AreEqual(0, sys.Foam.AliveCount, "foam left by the oars fades out; it is not a permanent mark");
        }

        [Test]
        public void AHarderPull_AndAFasterBoat_SplashMore()
        {
            OarSplashConfig cfg = Config;
            float gentle = DoryOarSplashMath.Strength01(0.1f, 0f, in cfg);
            float hard = DoryOarSplashMath.Strength01(1f, 0f, in cfg);
            float hardAndFast = DoryOarSplashMath.Strength01(1f, cfg.SpeedForFullSplash, in cfg);

            Assert.GreaterOrEqual(gentle, cfg.MinStrength);
            Assert.Greater(hard, gentle);
            Assert.Greater(hardAndFast, hard);
            Assert.AreEqual(1f, hardAndFast, 1e-5f);
            Assert.Greater(DoryOarSplashMath.CatchDrops(hardAndFast, in cfg),
                           DoryOarSplashMath.CatchDrops(gentle, in cfg));
        }

        [Test]
        public void TheSplash_IsLaidOutboardOfTheLock_OnTheOarsOwnSide()
        {
            OarSplashConfig cfg = Config;
            var lockPort = new Vector3(-0.71395f, -0.855f, 0.5156f);
            var lockStar = new Vector3(0.71395f, -0.855f, 0.5156f);
            DoryOarMeshPose.Pose catchPose = DoryOarMeshPose.Row(0f);

            Vector3 port = DoryOarSplashMath.BladeOnWaterLocal(lockPort, DoryOarMeshLayer.PortSide, in catchPose,
                                                               cfg.SplashReachMeters, 0.11f);
            Vector3 star = DoryOarSplashMath.BladeOnWaterLocal(lockStar, DoryOarMeshLayer.StarboardSide, in catchPose,
                                                               cfg.SplashReachMeters, 0.11f);

            Assert.Less(port.x, lockPort.x, "the port blade is to port of its lock");
            Assert.Greater(star.x, lockStar.x, "the starboard blade is to starboard of its lock");
            Assert.AreEqual(-port.x, star.x, 1e-4f, "the two sides mirror");
            Assert.AreEqual(0.11f, port.z, 1e-6f, "the splash is laid on the waterline");
        }

        [Test]
        public void StrokeColumns_AreRowing_TrailingAndShippedAreNot()
        {
            Assert.IsTrue(DoryOarMeshLayer.IsStrokeColumn(0));
            Assert.IsTrue(DoryOarMeshLayer.IsStrokeColumn(DoryOarMath.StrokeColumns - 1));
            Assert.IsFalse(DoryOarMeshLayer.IsStrokeColumn(DoryOarMath.RestingColumn));
            Assert.IsFalse(DoryOarMeshLayer.IsStrokeColumn(DoryOarMath.TrailingColumn));
            // Under power every oar draws the shipped column, whatever the keys say.
            int underPower = DoryOarMeshLayer.ColumnForOar(true, true, 3f, true, 0f,
                                                           DoryOarMath.DefaultRestGraceSeconds,
                                                           DoryOarMath.StrokeColumns);
            Assert.IsFalse(DoryOarMeshLayer.IsStrokeColumn(underPower));
        }
    }
}
