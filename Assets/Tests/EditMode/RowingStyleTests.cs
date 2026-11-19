using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>Rowing style is data</b> (owner, 2026-11-19: "more casual rowing, a pull with more rest between
    /// rows ... the old man probably doesn't row like an olympic rower"; and "the hull should react to
    /// each stroke moving faster as the row happens then slowing slightly as the oar moves").
    ///
    /// <para>The steady style is the stroke the game always rowed: same oar angles, same hand targets,
    /// same tempo, same average speed, with only a subtle surge on the pull. The casual style rests
    /// between strokes with the blades clear and the hands on the grips. The player rows steady; the old
    /// fisher and the game's other rowers row casual.</para>
    /// </summary>
    public class RowingStyleTests
    {
        private const string SteadyPath = "Assets/_Project/Data/Boats/RowingStyles/RowingStyleSteady.asset";
        private const string CasualPath = "Assets/_Project/Data/Boats/RowingStyles/RowingStyleCasual.asset";
        private const string ConfigPath = "Assets/_Project/Data/Config/GameConfig.asset";
        private const string SkipperVisualPath = "Assets/_Project/Data/Characters/SkipperIso.asset";

        // The dory's own facts, as DoryOarSplashTests reads them: oarlock z 0.5156 m over a 0.11 m
        // resting draught.
        private const float LockHeight = 0.5156f - 0.11f;
        private const float Dt = 1f / 60f;
        private const int Samples = 4000;

        // The dory's physics (boat.dory: MassKg 400 -> body 4, OarPower 300, ForwardDrag 215) through
        // BoatController's scales; drag is linear in speed, as the controller's hull drag and body
        // damping are.
        private static float DoryMass => BoatController.BodyMassFor(400f);
        private static float DoryFullThrust => BoatController.OarThrust(1f, 1f, 300f) * BoatController.ForceFeelScale;
        private static float DoryDrag => 215f * BoatController.ForceFeelScale + BoatController.HullLinearDamping * DoryMass;

        private static RowingStyleDef Load(string path)
        {
            var def = AssetDatabase.LoadAssetAtPath<RowingStyleDef>(path);
            Assert.IsNotNull(def, path + " is missing");
            return def;
        }

        private static RowingStyle Steady => RowingStyle.From(Load(SteadyPath));
        private static RowingStyle Casual => RowingStyle.From(Load(CasualPath));

        private static float WrapDistance(float a, float b)
        {
            float d = Mathf.Abs(a - b) % 1f;
            return Mathf.Min(d, 1f - d);
        }

        private static float StrokePeriodSeconds(in RowingStyle style) =>
            DoryOarMath.StrokeColumns / (DoryOarMath.DefaultStrokeFramesPerSecond * style.TempoScale);

        /// <summary>Row the dory model at full effort for <paramref name="seconds"/>, sampling speed each
        /// step. Returns the mean speed over the last <paramref name="measureStrokes"/> strokes.</summary>
        private static float MeanRowedSpeed(in RowingStyle style, float seconds, int measureStrokes,
                                            System.Action<float, float> eachStep = null)
        {
            float period = StrokePeriodSeconds(style);
            float v = 0f, raw = 0f, sum = 0f;
            int n = 0;
            int steps = Mathf.RoundToInt(seconds / Dt);
            int measureFrom = steps - Mathf.RoundToInt(measureStrokes * period / Dt);
            for (int i = 0; i < steps; i++)
            {
                raw = OarStrokeMath.Wrap01(raw + Dt / period);
                float force = DoryFullThrust * style.ThrustMultiplier(raw);
                v += (force - DoryDrag * v) / DoryMass * Dt;
                eachStep?.Invoke(raw, v);
                if (i >= measureFrom) { sum += v; n++; }
            }
            return sum / Mathf.Max(1, n);
        }

        // ---- steady is today ----------------------------------------------------------------------

        [Test]
        public void SteadyStyle_ReproducesTodaysOarAnglesAndHandTargets_AtEveryPhase()
        {
            RowingStyle steady = Steady;
            Assert.IsTrue(steady.IdentityTiming, "steady's shares must be the stroke's own parts");
            Assert.AreEqual(1f, steady.TempoScale, "steady rows at the tempo the game always rowed");
            Assert.AreEqual(1f, steady.LaybackScale, "steady leans back as far as the station says");

            RowingStationConfig station = RowingStationConfig.Default;
            var portLock = new Vector3(-0.714f, -0.855f, 0.516f);
            var starLock = new Vector3(0.714f, -0.855f, 0.516f);
            for (int i = 0; i < Samples; i++)
            {
                float raw = DoryOarMath.StrokeColumns * i / (float)Samples;
                float styled = DoryOarMeshLayer.StyledColumns(in steady, raw);
                Assert.AreEqual(raw, styled, 0f, "steady must hand the phase back untouched");

                int column = DoryOarMath.StrokeColumnFromPhase(raw, DoryOarMath.StrokeColumns);
                DoryOarMeshPose.Pose today = DoryOarMeshLayer.PoseFor(column, raw);
                DoryOarMeshPose.Pose now = DoryOarMeshLayer.PoseFor(column, styled);
                Assert.AreEqual(today.SweepDegrees, now.SweepDegrees, 0f);
                Assert.AreEqual(today.DipDegrees, now.DipDegrees, 0f);

                RowingTargets a = DoryOarMeshLayer.StationTargets(portLock, starLock, in station, today, true, today, true);
                RowingTargets b = DoryOarMeshLayer.StationTargets(portLock, starLock, in station, now, true, now, true);
                Assert.AreEqual(a.PortGrip, b.PortGrip);
                Assert.AreEqual(a.StarboardGrip, b.StarboardGrip);
                Assert.AreEqual(a.LaybackDegrees * steady.LaybackScale, b.LaybackDegrees, 0f);
                Assert.AreEqual(DoryOarMeshLayer.StrokePhaseOf(raw), DoryOarMeshLayer.StrokePhaseOf(styled), 0f);
            }
        }

        [Test]
        public void TheStrokeClock_AdvancesExactlyAsTheOarLayerDidBeforeIt()
        {
            var clock = new RowStrokeClock();
            RowStrokeTempo tempo = RowStrokeTempo.Default;
            float port = 0f, star = 0f;
            float[] portStates = { 1f, 1f, 0.4f, -1f, 0f, 1f, 0.7f };
            float[] starStates = { 1f, 0.3f, 0.4f, -1f, 0f, 0f, 1f };
            float steadyTempo = Steady.TempoScale;
            for (int i = 0; i < 600; i++)
            {
                float ps = portStates[(i / 90) % portStates.Length], ss = starStates[(i / 90) % starStates.Length];
                clock.Step(ps, ss, Dt, in tempo, steadyTempo);

                // The mesh layer's own accumulator, as it was.
                float pd = DoryOarMath.StrokeDirection(ps, tempo.Deadzone), sd = DoryOarMath.StrokeDirection(ss, tempo.Deadzone);
                port = DoryOarMath.AdvanceStrokePhase(port, pd,
                    DoryOarMath.StrokeFramesPerSecond(tempo.FramesPerSecond, ps, tempo.EffortInfluence), Dt, DoryOarMath.StrokeColumns);
                star = DoryOarMath.AdvanceStrokePhase(star, sd,
                    DoryOarMath.StrokeFramesPerSecond(tempo.FramesPerSecond, ss, tempo.EffortInfluence), Dt, DoryOarMath.StrokeColumns);
                if (pd != 0f && pd == sd)
                {
                    float step = tempo.PhaseLockColumnsPerSecond * Dt;
                    if (Mathf.Abs(ps) >= Mathf.Abs(ss)) star = DoryOarMath.LockPhaseToward(star, port, step, DoryOarMath.StrokeColumns);
                    else port = DoryOarMath.LockPhaseToward(port, star, step, DoryOarMath.StrokeColumns);
                }
                Assert.AreEqual(port, clock.PortPhase, 0f, "step " + i);
                Assert.AreEqual(star, clock.StarboardPhase, 0f, "step " + i);
            }
        }

        // ---- casual rests -------------------------------------------------------------------------

        [Test]
        public void CasualStyle_RestsAtLeastItsShare_BladesClear_HandsOnTheGrips()
        {
            RowingStyle casual = Casual;
            OarSplashConfig cfg = OarSplashConfig.Default;
            Assert.Greater(casual.RestShare, 0f, "casual must have a rest");
            Assert.Less(casual.TempoScale, Steady.TempoScale, "casual strokes slower than steady");

            int resting = 0;
            float restPhase = float.NaN;
            for (int i = 0; i < Samples; i++)
            {
                float raw = (i + 0.5f) / Samples;
                if (!casual.IsResting(raw)) continue;
                resting++;
                float drawn = casual.WarpStrokePhase(raw);
                if (float.IsNaN(restPhase)) restPhase = drawn;
                Assert.AreEqual(restPhase, drawn, 0f, "the oars are held still through the rest");

                float columns = OarStrokeMath.Wrap01(drawn + DoryOarMeshPose.RowCatchTurns) * DoryOarMath.StrokeColumns;
                DoryOarMeshPose.Pose pose = DoryOarMeshPose.Row(columns / DoryOarMath.StrokeColumns);
                Assert.Greater(DoryOarSplashMath.BladeHeightMeters(LockHeight, cfg.BladeReachMeters, pose.DipDegrees), 0f,
                               "the blade is clear of the water in the rest");

                // The oar is still rowed (a stroke column), so the station holds both grips.
                float rawColumns = OarStrokeMath.Wrap01(raw + DoryOarMeshPose.RowCatchTurns) * DoryOarMath.StrokeColumns;
                int column = DoryOarMeshLayer.ColumnForOar(false, true, rawColumns, true, 0f,
                                                           DoryOarMath.DefaultRestGraceSeconds, DoryOarMath.StrokeColumns);
                Assert.IsTrue(DoryOarMeshLayer.IsStrokeColumn(column), "hands stay on the grips in the rest");
                RowingTargets t = DoryOarMeshLayer.StationTargets(Vector3.left, Vector3.right, RowingStationConfig.Default,
                                                                   pose, DoryOarMeshLayer.IsStrokeColumn(column),
                                                                   pose, DoryOarMeshLayer.IsStrokeColumn(column));
                Assert.IsTrue(t.PortGripValid && t.StarboardGripValid);
            }
            Assert.GreaterOrEqual(resting / (float)Samples, casual.RestShare - 1f / Samples,
                                  "the rest lasts at least the style's share of the stroke");
        }

        [Test]
        public void StrokePhases_AreContinuous_WithNoPopAtTheWrap()
        {
            foreach (RowingStyle style in new[] { Steady, Casual })
            {
                float previous = style.WarpStrokePhase(0f);
                for (int i = 1; i <= Samples; i++)
                {
                    float drawn = style.WarpStrokePhase(i / (float)Samples);
                    Assert.Less(WrapDistance(previous, drawn), 10f / Samples, "phase jumped at raw " + i / (float)Samples);
                    previous = drawn;
                }
                Assert.Less(WrapDistance(style.WarpStrokePhase(1f - 1e-5f), style.WarpStrokePhase(0f)), 1e-3f);
                Assert.AreEqual(style.ThrustMultiplier(1f - 1e-5f), style.ThrustMultiplier(0f), 1e-3f,
                                "the push has no pop at the wrap");
            }
        }

        [Test]
        public void TheSplash_FiresOncePerStroke_AtTheCatch_RowingCasual()
        {
            RowingStyle casual = Casual;
            OarSplashConfig cfg = OarSplashConfig.Default;
            var sys = new DoryOarSplashSystem(cfg.FoamPoolSize, cfg.DropletPoolSize, cfg.RipplePoolSize, cfg.DripPoolSize);
            float fps = DoryOarMath.DefaultStrokeFramesPerSecond * casual.TempoScale;
            float raw = DoryOarMath.StrokeColumns * 0.5f;
            const int strokes = 5;
            int steps = Mathf.RoundToInt(strokes * DoryOarMath.StrokeColumns / (fps * Dt));
            int catches = 0;
            for (int i = 0; i < steps; i++)
            {
                raw = DoryOarMath.AdvanceStrokePhase(raw, 1f, fps, Dt, DoryOarMath.StrokeColumns);
                float drawnColumns = DoryOarMeshLayer.StyledColumns(in casual, raw);
                DoryOarMeshPose.Pose pose = DoryOarMeshPose.Row(drawnColumns / DoryOarMath.StrokeColumns);
                var frame = new DoryOarSplashSystem.OarFrame
                {
                    Stroking = true,
                    BladeHeightMeters = DoryOarSplashMath.BladeHeightMeters(LockHeight, cfg.BladeReachMeters, pose.DipDegrees),
                    BladeWorld = new Vector2(1f, 0f),
                    Outboard = Vector2.right,
                    Aft = Vector2.down,
                    Effort01 = 1f,
                    StrokeDirection = 1f,
                };
                sys.Step(in frame, in frame, 1f, Vector2.zero, Dt, in cfg);
                int now = sys.CatchCount(DoryOarSplashSystem.Port);
                if (now != catches)
                {
                    catches = now;
                    float strokePhase = DoryOarMeshLayer.StrokePhaseOf(drawnColumns);
                    OarStrokePart part = OarStrokeMath.PartOf(true, strokePhase);
                    Assert.IsTrue(part == OarStrokePart.Catch || part == OarStrokePart.Drive,
                                  "the splash fires as the blade goes in, not at " + part);
                }
            }
            Assert.AreEqual(strokes, sys.CatchCount(DoryOarSplashSystem.Port), "one catch per stroke");
            Assert.AreEqual(strokes, sys.CatchCount(DoryOarSplashSystem.Starboard), "one catch per stroke");
        }

        // ---- the hull surges --------------------------------------------------------------------

        [Test]
        public void TheHull_IsFasterDuringThePull_ThanInTheRest()
        {
            RowingStyle casual = Casual;
            float pullSum = 0f, restSum = 0f;
            int pullN = 0, restN = 0;
            float period = StrokePeriodSeconds(casual);
            int step = 0;
            int upToSpeed = Mathf.RoundToInt(20f / Dt);
            MeanRowedSpeed(casual, 40f, 4, (raw, v) =>
            {
                // Measure only once the boat is up to speed (after 20 s of rowing).
                if (step++ < upToSpeed) return;
                float drawn = casual.WarpStrokePhase(raw);
                if (casual.IsResting(raw)) { restSum += v; restN++; }
                else if (drawn > RowingStyle.CatchEnd && drawn < OarStrokeMath.ReleasePhase) { pullSum += v; pullN++; }
            });
            Assert.Greater(pullN, 0);
            Assert.Greater(restN, 0);
            Assert.Greater(pullSum / pullN, restSum / restN, "the hull is faster on the pull than gliding in the rest");
            Assert.Greater(casual.ThrustMultiplier(0.25f), casual.ThrustMultiplier(0.8f), "the push gathers on the pull");
            Assert.Greater(Steady.ThrustMultiplier(0.25f), Steady.ThrustMultiplier(0.75f), "steady surges too, subtly");
            Assert.Greater(period, StrokePeriodSeconds(Steady));
        }

        [Test]
        public void SteadyStyle_AveragesTheOldConstantSpeed_OverAStroke()
        {
            RowingStyle steady = Steady;
            float mean = 0f;
            for (int i = 0; i < Samples; i++) mean += steady.ThrustMultiplier((i + 0.5f) / Samples);
            mean /= Samples;
            Assert.AreEqual(1f, mean, 1e-3f, "steady pushes the old push on average");
            Assert.Greater(steady.SurgeDepth, 0f, "steady still surges");

            float oldSpeed = MeanRowedSpeed(RowingStyle.Unstyled, 40f, 10);
            float newSpeed = MeanRowedSpeed(steady, 40f, 10);
            Assert.AreEqual(DoryFullThrust / DoryDrag, oldSpeed, oldSpeed * 1e-3f, "the old even push settles where it always did");
            Assert.AreEqual(oldSpeed, newSpeed, oldSpeed * 5e-3f, "same distance per stroke on average");
            Assert.AreEqual(1f, RowingStyle.Unstyled.ThrustMultiplier(0.3f), 0f, "no style = the old even push");
        }

        [Test]
        public void CasualStyle_AveragesItsOwnDataShare_ABitSlower()
        {
            RowingStyle casual = Casual;
            float mean = 0f;
            for (int i = 0; i < Samples; i++) mean += casual.ThrustMultiplier((i + 0.5f) / Samples);
            mean /= Samples;
            Assert.AreEqual(casual.AverageThrustScale, mean, 2e-3f);
            Assert.Less(casual.AverageThrustScale, Steady.AverageThrustScale, "casual is a bit slower on average");
            float speed = MeanRowedSpeed(casual, 60f, 10);
            Assert.AreEqual(casual.AverageThrustScale * DoryFullThrust / DoryDrag, speed, speed * 1e-2f);
        }

        [Test]
        public void TheSpeed_HasNoPop_AsTheStrokeWraps()
        {
            foreach (RowingStyle style in new[] { Steady, Casual })
            {
                float maxStep = 0f, last = float.NaN;
                MeanRowedSpeed(style, 30f, 2, (raw, v) =>
                {
                    if (!float.IsNaN(last)) maxStep = Mathf.Max(maxStep, Mathf.Abs(v - last));
                    last = v;
                });
                float peakMultiplier = 0f;
                for (int i = 0; i < Samples; i++) peakMultiplier = Mathf.Max(peakMultiplier, style.ThrustMultiplier(i / (float)Samples));
                // The speed is integrated from the push, so one step can change it by at most the peak
                // push's acceleration over that step: never a jump.
                Assert.LessOrEqual(maxStep, DoryFullThrust * peakMultiplier / DoryMass * Dt * 1.01f);
            }
        }

        [Test]
        public void StoppingRowing_CoastsDownAsBefore()
        {
            var clock = new RowStrokeClock();
            RowStrokeTempo tempo = RowStrokeTempo.Default;
            for (int i = 0; i < 37; i++) clock.Step(1f, 1f, Dt, in tempo, Casual.TempoScale);
            float heldPort = clock.PortPhase;
            clock.Step(0f, 0f, Dt, in tempo, Casual.TempoScale);
            Assert.AreEqual(heldPort, clock.PortPhase, 0f, "an idle oar's phase holds");
            Assert.AreEqual(0f, clock.PortRate);

            RowingStyle casual = Casual;
            BoatController.OarDriveMultipliers(in casual, in clock, out float portDrive, out float starDrive);
            Assert.AreEqual(0f, BoatController.OarThrust(0f * portDrive, 0f * starDrive, 300f), 0f,
                            "no oar pulled, no push, whatever the phase: she coasts exactly as before");
            Assert.AreEqual(0f, BoatController.OarYawTorque(0f * portDrive, 0f * starDrive, 300f, 0.6f), 0f);
        }

        [Test]
        public void TheStyleMath_IsDeterministic_AndAllocationFree()
        {
            RowingStyle casual = Casual;
            float a = 0f, b = 0f;
            for (int i = 0; i < 512; i++) a += casual.ThrustMultiplier(i / 512f) + casual.WarpStrokePhase(i / 512f);
            for (int i = 0; i < 512; i++) b += casual.ThrustMultiplier(i / 512f) + casual.WarpStrokePhase(i / 512f);
            Assert.AreEqual(a, b, 0f);
            var clock = new RowStrokeClock();
            RowStrokeTempo tempo = RowStrokeTempo.Default;
            Assert.That(() =>
            {
                for (int i = 0; i < 256; i++)
                {
                    clock.Step(1f, 0.8f, Dt, in tempo, casual.TempoScale);
                    BoatController.OarDriveMultipliers(in casual, in clock, out float p, out float s);
                    DoryOarMeshLayer.StyledColumns(in casual, clock.PortAt(-0.01f));
                }
            }, Is.Not.AllocatingGCMemory());
        }

        // ---- who rows which -----------------------------------------------------------------------

        [Test]
        public void ThePlayersDefault_IsSteady()
        {
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath);
            Assert.IsNotNull(config);
            RowingStyleDef player = RowingStyle.ChooseForPlayer(config);
            Assert.IsNotNull(player, "GameConfig.PlayerRowingStyle must be set");
            Assert.AreEqual("rowstyle.steady", player.Id);
            Assert.IsNull(RowingStyle.ChooseForPlayer(null), "no config = the plain stroke");
        }

        [Test]
        public void TheOldFisher_RowsCasual_AndSoDoTheGamesOtherRowers()
        {
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath);
            var skipper = AssetDatabase.LoadAssetAtPath<CharacterVisualDef>(SkipperVisualPath);
            Assert.IsNotNull(config);
            Assert.IsNotNull(skipper);
            Assert.AreEqual("visual.skipper_iso", skipper.Id);
            Assert.AreEqual("rowstyle.casual", RowingStyle.ChooseForNpc(skipper.RowingStyle, config).Id);
            Assert.AreEqual("rowstyle.casual", RowingStyle.ChooseForNpc(null, config).Id,
                            "a rower with no style of their own rows the NPC default");
        }

        // ---- content ------------------------------------------------------------------------------

        [Test]
        public void RowingStyleContent_HasStableUniqueIds_AndSaneNumbers()
        {
            string[] guids = AssetDatabase.FindAssets("t:" + nameof(RowingStyleDef), new[] { "Assets/_Project/Data" });
            Assert.GreaterOrEqual(guids.Length, 2);
            var seen = new System.Collections.Generic.HashSet<string>();
            foreach (string guid in guids)
            {
                var def = AssetDatabase.LoadAssetAtPath<RowingStyleDef>(AssetDatabase.GUIDToAssetPath(guid));
                Assert.IsNotNull(def);
                StringAssert.StartsWith("rowstyle.", def.Id);
                StringAssert.IsMatch("^rowstyle\\.[a-z0-9_]+$", def.Id);
                Assert.IsTrue(seen.Add(def.Id), "duplicate id " + def.Id);
                Assert.GreaterOrEqual(def.TempoScale, RowingStyle.MinTempoScale, def.Id);
                Assert.Greater(def.CatchShare + def.PullShare + def.ReleaseShare + def.RecoveryShare + def.RestShare, 0f, def.Id);
                Assert.Greater(def.PullShare, 0f, def.Id + " must pull");
                Assert.That(def.SurgeDepth, NUnit.Framework.Is.InRange(0f, 1f), def.Id);
                Assert.Greater(def.AverageThrustScale, 0f, def.Id);
                Assert.That(def.RestAtStrokePhase, NUnit.Framework.Is.InRange(RowingStyle.ReleaseEnd, 1f), def.Id);
            }
            CollectionAssert.IsSubsetOf(new[] { "rowstyle.steady", "rowstyle.casual" }, seen);
            Assert.AreEqual("rowstyle.steady", Load(SteadyPath).Id);
            Assert.AreEqual("rowstyle.casual", Load(CasualPath).Id);
        }
    }
}
