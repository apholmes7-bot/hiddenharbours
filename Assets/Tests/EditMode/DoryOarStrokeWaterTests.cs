using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Boats;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>The whole stroke shows on the water</b> (owner, 2026-11-17: the foam must come from the oars
    /// — a swirl, white water and spreading ripples where each blade goes in and comes out, plus drips
    /// on the recovery).
    ///
    /// <para>Driven, like <see cref="DoryOarSplashTests"/>, by the real stroke: the same
    /// <see cref="DoryOarMath.AdvanceStrokePhase"/> accumulator and <see cref="DoryOarMeshPose.Row"/>
    /// pose the mesh oars are drawn in, at the dory's own oarlock height. The release, the puddle,
    /// the rings and the drips are all keyed to the blade's own height, so they cannot fall out of
    /// step with the drawn oar.</para>
    /// </summary>
    public class DoryOarStrokeWaterTests
    {
        // The dory's own facts: oarlock z 0.58808 m (DoryOarPortPropMesh.PivotLocalMeters) over a
        // 0.11 m resting draught (DoryIsoHullMesh.RestingDraftMeters).
        private const float LockHeight = 0.58808f - 0.11f;
        private const float Dt = 1f / 60f;
        // A camera looking down at 60 degrees: a ground metre across the view is sin 60 on screen,
        // a metre of height cos 60.
        private static readonly float Squash = Mathf.Sin(60f * Mathf.Deg2Rad);
        private static readonly float Lift = Mathf.Cos(60f * Mathf.Deg2Rad);

        private static OarSplashConfig Config => OarSplashConfig.Default;

        private static DoryOarSplashSystem NewSystem(in OarSplashConfig cfg)
            => new DoryOarSplashSystem(cfg.FoamPoolSize, cfg.DropletPoolSize, cfg.RipplePoolSize, cfg.DripPoolSize);

        private static DoryOarSplashSystem.OarFrame Frame(float phase, in OarSplashConfig cfg, int side,
                                                          bool stroking = true, float direction = 1f)
        {
            DoryOarMeshPose.Pose pose = DoryOarMeshPose.Row(phase / DoryOarMath.StrokeColumns);
            var outboard = new Vector2(side, 0f);
            return new DoryOarSplashSystem.OarFrame
            {
                Stroking = stroking,
                BladeHeightMeters = DoryOarSplashMath.BladeHeightMeters(LockHeight, cfg.BladeReachMeters,
                                                                        pose.DipDegrees),
                BladeWorld = outboard * 1.2f,
                TipOnWaterWorld = outboard * 1.45f,
                Outboard = outboard,
                Aft = Vector2.down,
                Effort01 = 1f,
                StrokeDirection = direction,
                GroundSquash = Squash,
                HeightToScreen = Lift,
            };
        }

        /// <summary>Row both oars <paramref name="strokes"/> whole cycles from the top of the recovery.</summary>
        private static void Row(DoryOarSplashSystem sys, in OarSplashConfig cfg, int strokes,
                                float direction = 1f, System.Action<DoryOarSplashSystem> eachStep = null)
        {
            float fps = DoryOarMath.DefaultStrokeFramesPerSecond;
            float phase = DoryOarMath.StrokeColumns * 0.5f;
            int steps = Mathf.RoundToInt(strokes * DoryOarMath.StrokeColumns / (fps * Dt));
            for (int i = 0; i < steps; i++)
            {
                phase = DoryOarMath.AdvanceStrokePhase(phase, direction, fps, Dt, DoryOarMath.StrokeColumns);
                var port = Frame(phase, cfg, -1, true, direction);
                var star = Frame(phase, cfg, +1, true, direction);
                sys.Step(in port, in star, 1f, Vector2.zero, Dt, in cfg);
                eachStep?.Invoke(sys);
            }
        }

        /// <summary>Step both oars through the stroke until the blades have just come out of the water.</summary>
        private static void RowToRelease(DoryOarSplashSystem sys, in OarSplashConfig cfg)
        {
            float fps = DoryOarMath.DefaultStrokeFramesPerSecond;
            float phase = DoryOarMath.StrokeColumns * 0.5f;
            for (int i = 0; i < 2000 && sys.ReleaseCount(DoryOarSplashSystem.Port) == 0; i++)
            {
                phase = DoryOarMath.AdvanceStrokePhase(phase, 1f, fps, Dt, DoryOarMath.StrokeColumns);
                var port = Frame(phase, cfg, -1);
                var star = Frame(phase, cfg, +1);
                sys.Step(in port, in star, 1f, Vector2.zero, Dt, in cfg);
            }
            Assert.AreEqual(1, sys.ReleaseCount(DoryOarSplashSystem.Port), "the blade never came out of the water");
        }

        // ---- the release ---------------------------------------------------------------------------

        [Test]
        public void TheBladeComesOut_OncePerStroke_PerOar_PullingOrBacking()
        {
            OarSplashConfig cfg = Config;
            var ahead = NewSystem(cfg);
            Row(ahead, cfg, strokes: 5);
            Assert.AreEqual(5, ahead.ReleaseCount(DoryOarSplashSystem.Port));
            Assert.AreEqual(5, ahead.ReleaseCount(DoryOarSplashSystem.Starboard));

            var backing = NewSystem(cfg);
            Row(backing, cfg, strokes: 4, direction: -1f);
            Assert.AreEqual(4, backing.ReleaseCount(DoryOarSplashSystem.Port));
        }

        [Test]
        public void LiftingAnOarOutToShipIt_IsNotARelease()
        {
            // An oar lying in the water that stops being rowed is lifted, not pulled out mid-stroke.
            OarSplashConfig cfg = Config;
            var sys = NewSystem(cfg);
            var inWater = Frame(0f, cfg, -1);
            sys.Step(in inWater, in inWater, 1f, Vector2.zero, Dt, in cfg);
            var shipped = Frame(DoryOarMath.StrokeColumns * 0.5f, cfg, -1, stroking: false);
            for (int i = 0; i < 120; i++) sys.Step(in shipped, in shipped, 1f, Vector2.zero, Dt, in cfg);

            Assert.AreEqual(0, sys.ReleaseCount(DoryOarSplashSystem.Port));
            Assert.AreEqual(0, sys.Drips.AliveCount, "a shipped blade does not drip");
        }

        [Test]
        public void TheRelease_LeavesAPuddle_ThatTurns_TheTwoOarsOppositeWays()
        {
            OarSplashConfig cfg = Config;
            cfg.FoamPuffsPerSecond = 0f;   // only the puddle in the foam pool
            cfg.CatchFoamPuffs = 0;
            var sys = NewSystem(cfg);
            RowToRelease(sys, cfg);

            float portTurn = 0f, starTurn = 0f;
            int portPuffs = 0, starPuffs = 0;
            foreach (var p in sys.Foam.Pool)
            {
                if (!p.Alive) continue;
                bool port = p.Pos.x < 0f;
                Vector2 centre = new Vector2(port ? -1.2f : 1.2f, 0f);
                Vector2 r = p.Pos - centre;
                float turn = r.x * p.Vel.y - r.y * p.Vel.x;
                if (port) { portTurn += turn; portPuffs++; } else { starTurn += turn; starPuffs++; }
                Assert.LessOrEqual(r.magnitude, cfg.SwirlRadiusMeters + 0.05f,
                    "a puddle puff sits on the little ring round the hole, not scattered across the sea");
            }
            Assert.AreEqual(cfg.SwirlPuffs, portPuffs, "port: one puddle of SwirlPuffs puffs");
            Assert.AreEqual(cfg.SwirlPuffs, starPuffs, "starboard: one puddle of SwirlPuffs puffs");
            Assert.AreNotEqual(0f, portTurn, "the puddle turns");
            Assert.Less(portTurn * starTurn, 0f, "mirrored strokes turn their puddles opposite ways");
        }

        [Test]
        public void ThePuddle_LiesOnTheWater_SquashedAsTheCameraSeesIt()
        {
            const int n = 8;
            float wide = 0f, tall = 0f;
            for (int k = 0; k < n; k++)
            {
                Vector2 o = DoryOarSplashMath.SwirlOffset(k, n, 0f, 1f, Squash);
                wide = Mathf.Max(wide, Mathf.Abs(o.x));
                tall = Mathf.Max(tall, Mathf.Abs(o.y));
            }
            Assert.AreEqual(1f, wide, 1e-4f);
            Assert.AreEqual(Squash, tall, 1e-4f, "a ring on the water is an ellipse sin(elevation) tall");
        }

        [Test]
        public void TheRelease_FlicksDroplets_TheWayTheBladeWasSweeping()
        {
            OarSplashConfig cfg = Config;
            cfg.CatchDropsMin = cfg.CatchDropsMax = 0;   // only the release's droplets
            var sys = NewSystem(cfg);
            RowToRelease(sys, cfg);

            int n = 0;
            foreach (var p in sys.Droplets.Pool)
            {
                if (!p.Alive) continue;
                n++;
                Assert.Less(p.Vel.y, 0f, "pulling ahead, the blade sweeps aft and throws its water aft");
            }
            Assert.GreaterOrEqual(n, 2 * cfg.ReleaseDropsMin);
        }

        // ---- the rings -----------------------------------------------------------------------------

        [Test]
        public void TheCatch_SendsOutRings_EachLaterAndFainterThanTheLast()
        {
            OarSplashConfig cfg = Config;
            cfg.RipplesAtCatch = 3;
            var sys = NewSystem(cfg);
            var air = Frame(DoryOarMath.StrokeColumns * 0.5f, cfg, -1);
            var water = Frame(0f, cfg, -1);
            var shipped = Frame(DoryOarMath.StrokeColumns * 0.5f, cfg, +1, stroking: false);
            sys.Step(in air, in shipped, 1f, Vector2.zero, Dt, in cfg);
            sys.Step(in water, in shipped, 1f, Vector2.zero, Dt, in cfg);

            Assert.AreEqual(3, sys.Ripples.AliveCount, "one set of three rings, from the one blade that entered");
            Assert.AreEqual(1, sys.Ripples.ShownCount, "only the first ring shows at once");

            // The frame that lays the set also ages it: the first ring is already showing (its Age is
            // that frame's dt) while the others have spent that dt from their wait. So the moment a
            // ring shows, measured from now, is its remaining Delay less the Age it has already had.
            var rings = new System.Collections.Generic.List<OarWaterMarkPool.Mark>();
            foreach (var m in sys.Ripples.Pool) if (m.Alive) rings.Add(m);
            rings.Sort((a, b) => ShowsAt(a).CompareTo(ShowsAt(b)));
            for (int i = 1; i < rings.Count; i++)
            {
                Assert.AreEqual(cfg.RippleStaggerSeconds, ShowsAt(rings[i]) - ShowsAt(rings[i - 1]), 1e-4f,
                    "each ring shows one stagger after the last");
                Assert.Less(rings[i].Alpha, rings[i - 1].Alpha, "each echo is fainter");
                Assert.AreEqual(-1.2f, rings[i].Pos.x, 1e-5f, "every ring of a set spreads from the blade");
            }
        }

        /// <summary>When a ring shows, in seconds from now: negative once it is already on the water.</summary>
        private static float ShowsAt(in OarWaterMarkPool.Mark m) => m.Delay - m.Age;

        [Test]
        public void ARing_SpreadsFastThenSlow_AndOnlyEverFades()
        {
            OarSplashConfig cfg = Config;
            var pool = new OarWaterMarkPool(1);
            pool.Add(Vector2.zero, Vector2.zero, 0f, 1f, cfg.RippleStartRadius, cfg.RippleEndRadius, 0.5f, false);

            float lastR = -1f, lastA = 2f, firstStep = 0f, lastStep = 0f, prevR = cfg.RippleStartRadius;
            for (int i = 0; i < 59; i++)
            {
                pool.Step(Vector2.zero, Dt);
                var m = pool.Pool[0];
                float r = OarWaterMarkPool.Radius(in m, cfg.RippleSpreadEase);
                float a = OarWaterMarkPool.Opacity(in m, cfg.RippleFadePower);
                Assert.GreaterOrEqual(r, lastR, "a ring never shrinks");
                Assert.LessOrEqual(a, lastA, "a ring never brightens");
                if (i == 0) firstStep = r - prevR;
                lastStep = r - prevR;
                prevR = r; lastR = r; lastA = a;
            }
            Assert.Greater(firstStep, lastStep * 3f, "it runs out quickly and slows as it spreads");
            pool.Step(Vector2.zero, Dt * 2f);
            Assert.AreEqual(0, pool.AliveCount, "and it is gone at the end of its life");
        }

        [Test]
        public void AWaitingMark_ShowsOnTheFrameItIsDue_AndHoldsStillUntilThen()
        {
            var m = new OarWaterMarkPool.Mark
            {
                Alive = true, Pos = Vector2.zero, Delay = 0.25f, Lifetime = 1f,
                StartRadius = 0.1f, EndRadius = 0.5f, Alpha = 1f,
            };
            var current = new Vector2(1f, 0f);

            m = OarWaterMarkPool.Advance(m, current, 0.2f);
            Assert.IsFalse(OarWaterMarkPool.IsShown(in m));
            Assert.AreEqual(Vector2.zero, m.Pos, "a waiting mark is not on the water yet: nothing drifts it");

            m = OarWaterMarkPool.Advance(m, current, 0.1f);
            Assert.IsTrue(OarWaterMarkPool.IsShown(in m));
            Assert.AreEqual(0.05f, m.Age, 1e-5f, "only the time past the delay ages it");
            Assert.AreEqual(0.05f, m.Pos.x, 1e-5f, "and only that time drifts it on the tide");
        }

        [Test]
        public void TheRingPicture_IsBrightOnItsBand_AndClearInsideAndOut()
        {
            OarSplashConfig cfg = Config;
            int n = DoryOarSplashEmitter.RingTextureSize;
            float[] a = OarRippleTexture.RingCoverage(n, cfg.RippleBandCentre, cfg.RippleBandHalfWidth);
            Assert.AreEqual(n * n, a.Length);
            int mid = n / 2;
            float c = (n - 1) * 0.5f;
            int bandX = Mathf.RoundToInt(c + cfg.RippleBandCentre * n * 0.5f);
            Assert.Greater(a[mid * n + bandX], 0.8f, "the band is bright");
            Assert.AreEqual(0f, a[mid * n + mid], 1e-5f, "the middle is open water");
            Assert.AreEqual(0f, a[0], 1e-5f, "the corners are clear");
        }

        // ---- the drips -----------------------------------------------------------------------------

        [Test]
        public void TheRecovery_Drips_ThinningAsTheBladeDrains_AndStopsAtTheNextCatch()
        {
            OarSplashConfig cfg = Config;
            float strength = 1f;
            float early = DoryOarSplashMath.DripRate(strength, 0f, in cfg);
            float late = DoryOarSplashMath.DripRate(strength, 3f * cfg.DripDecaySeconds, in cfg);
            Assert.AreEqual(cfg.DripsPerSecond, early, 1e-4f);
            Assert.Less(late, early * 0.06f, "three decay times on, the blade has all but drained");
            Assert.AreEqual(0f, DoryOarSplashMath.DripRate(strength, float.PositiveInfinity, in cfg),
                "no release yet, no drips");

            var sys = NewSystem(cfg);
            int most = 0;
            Row(sys, cfg, strokes: 3, eachStep: s => most = Mathf.Max(most, s.Drips.AliveCount));
            Assert.Greater(most, 0, "a rowed blade drips on its way forward");
        }

        [Test]
        public void ADrip_FallsFromTheBladeTip_AndItsRingShowsWhereAndWhenItLands()
        {
            OarSplashConfig cfg = Config;
            cfg.RipplesAtRelease = 0;   // only drip rings wait in the ring pool
            cfg.DripScatterMeters = 0f;
            var sys = NewSystem(cfg);
            RowToRelease(sys, cfg);

            float fps = DoryOarMath.DefaultStrokeFramesPerSecond;
            float phase = 0f;
            // Carry on from the release until the first drip is in the air.
            for (int i = 0; i < 600 && sys.Drips.AliveCount == 0; i++)
            {
                phase = DoryOarMath.StrokeColumns * 0.2f + i * fps * Dt;
                var port = Frame(phase, cfg, -1);
                var star = Frame(phase, cfg, +1);
                sys.Step(in port, in star, 1f, Vector2.zero, Dt, in cfg);
            }
            Assert.Greater(sys.Drips.AliveCount, 0, "no drip left the blade");

            bool matched = false;
            foreach (var d in sys.Drips.Pool)
            {
                if (!d.Alive) continue;
                float remaining = d.Lifetime - d.Age;
                Vector2 lands = d.Pos + d.Vel * remaining;
                Assert.Less(d.Vel.y, 0f, "a drip falls");
                foreach (var r in sys.Ripples.Pool)
                {
                    if (!r.Alive || r.Delay <= 0f || Mathf.Abs(r.Pos.x - lands.x) > 1e-4f) continue;
                    if (Mathf.Abs(r.Pos.y - lands.y) > 1e-3f) continue;
                    Assert.AreEqual(remaining, r.Delay, 1e-3f, "its ring waits exactly as long as the fall");
                    Assert.Less(r.EndRadius, cfg.RippleEndRadius, "a drip's ring is smaller than a blade's");
                    matched = true;
                }
            }
            Assert.IsTrue(matched, "a falling drip has its ring waiting where, and for as long as, it will land");
        }

        [Test]
        public void ADripsFall_IsGravitys_FromTheHeightTheBladeIsDrawnAt()
        {
            Assert.AreEqual(0f, DoryOarSplashMath.DripFallSeconds(0f));
            Assert.AreEqual(0f, DoryOarSplashMath.DripFallSeconds(-0.2f), "a blade in the water does not drip");
            Assert.AreEqual(Mathf.Sqrt(2f * 0.3f / 9.81f), DoryOarSplashMath.DripFallSeconds(0.3f), 1e-5f);
        }

        // ---- the gates still hold ------------------------------------------------------------------

        [Test]
        public void OarsThatAreNotRowed_MakeNoRingsAndNoDrips()
        {
            OarSplashConfig cfg = Config;
            var sys = NewSystem(cfg);
            for (int i = 0; i < 480; i++)
            {
                float phase = (i * 0.1f) % DoryOarMath.StrokeColumns;
                var port = Frame(phase, cfg, -1, stroking: false);
                var star = Frame(phase, cfg, +1, stroking: false);
                sys.Step(in port, in star, 3f, Vector2.zero, Dt, in cfg);
            }
            Assert.AreEqual(0, sys.Ripples.AliveCount);
            Assert.AreEqual(0, sys.Drips.AliveCount);
            Assert.AreEqual(0, sys.ReleaseCount(DoryOarSplashSystem.Port));
        }

        [Test]
        public void SwitchedOff_LeavesTheWaterAlone()
        {
            OarSplashConfig cfg = Config;
            cfg.Enabled = false;
            var sys = NewSystem(cfg);
            Row(sys, cfg, strokes: 3);
            Assert.AreEqual(0, sys.Ripples.AliveCount);
            Assert.AreEqual(0, sys.Drips.AliveCount);
            Assert.AreEqual(0, sys.Foam.AliveCount);
        }

        [Test]
        public void EverythingTheOarsLeave_FadesOnceTheRowingStops()
        {
            OarSplashConfig cfg = Config;
            var sys = NewSystem(cfg);
            Row(sys, cfg, strokes: 2);
            Assert.Greater(sys.Ripples.AliveCount, 0);

            var port = Frame(DoryOarMath.StrokeColumns * 0.5f, cfg, -1, stroking: false);
            var star = Frame(DoryOarMath.StrokeColumns * 0.5f, cfg, +1, stroking: false);
            float jitter = 1f + cfg.LifetimeJitter;
            float longest = Mathf.Max(cfg.FoamLifetime * Mathf.Max(1f, cfg.SwirlLifetimeScale),
                                      cfg.RippleLifetime + cfg.RippleStaggerSeconds * cfg.RipplesAtCatch) * jitter + 0.5f;
            for (float t = 0f; t < longest; t += Dt) sys.Step(in port, in star, 0f, Vector2.zero, Dt, in cfg);

            Assert.AreEqual(0, sys.Foam.AliveCount, "the puddles fade");
            Assert.AreEqual(0, sys.Ripples.AliveCount, "the rings fade");
            Assert.AreEqual(0, sys.Drips.AliveCount, "the drips land");
        }

        [Test]
        public void TheNewPools_AreFixed_AndReused()
        {
            OarSplashConfig cfg = Config;
            var sys = NewSystem(cfg);
            var rings = sys.Ripples.Pool;
            var drips = sys.Drips.Pool;
            Row(sys, cfg, strokes: 100);

            Assert.AreSame(rings, sys.Ripples.Pool);
            Assert.AreSame(drips, sys.Drips.Pool);
            Assert.AreEqual(cfg.RipplePoolSize, sys.Ripples.Capacity);
            Assert.AreEqual(cfg.DripPoolSize, sys.Drips.Capacity);
            Assert.AreEqual(100, sys.ReleaseCount(DoryOarSplashSystem.Starboard), "every stroke still released");
        }
    }
}
