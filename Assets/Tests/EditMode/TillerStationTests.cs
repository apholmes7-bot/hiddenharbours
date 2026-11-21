using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The skipper at the dory's tiller: the station (<see cref="OutboardTillerStation.Targets"/>, the
    /// dory's <see cref="BoatVisualDef.Tiller"/> data and her baked motor fit) and the seated solve
    /// (<see cref="RowingPoseSolver"/>) run on every committed skin's seated clip, across the engine's
    /// whole steering range, at idle and at full throttle, in every facing the figure can be drawn at
    /// on the hull. Pure arithmetic on committed assets; no scene, no clock.
    /// </summary>
    public class TillerStationTests
    {
        private const string SkinFolder = "Assets/_Project/Data/Characters/Skin";
        private const string DoryVisualPath = "Assets/_Project/Data/Boats/Visuals/DoryIso.asset";

        /// <summary>Half a pixel at the skins' 32 px per metre: a hand closer than this is drawn ON the grip.</summary>
        private const float HalfPixelMeters = 0.5f / 32f;

        private const float Rigid = 1e-4f;

        /// <summary>Steering samples across the engine's range, each side.</summary>
        private const int SteerSteps = 16;

        /// <summary>Points sampled along the tiller arm, grip to pivot.</summary>
        private const int BarSamples = 20;

        /// <summary>The figure heading dial can twist him on the hull by up to half a step of a
        /// 16-step dial either way.</summary>
        private static readonly float[] DialTwist = { 0f, -11.25f, 11.25f };

        private static readonly float[] Throttles = { 0f, 0.5f, 1f, -1f };

        // The motor_fit stern bench in the dory rig (docs/art/rigs/doryIsoRig.js, part 'motor_fit'):
        // its top, and its extent fore and aft and athwartships.
        private const float BenchTopZ = 0.3584f;
        private const float BenchHalfWidth = 0.413f;
        private const float BenchAftY = -1.795f, BenchForeY = -1.535f;

        private static BoatVisualDef Dory()
        {
            var visual = AssetDatabase.LoadAssetAtPath<BoatVisualDef>(DoryVisualPath);
            Assert.That(visual, Is.Not.Null, DoryVisualPath);
            Assert.That(visual.MotorMesh, Is.Not.Null, "the dory's outboard fitting");
            Assert.That(visual.Tiller.Enabled, Is.True, "the dory's tiller station is enabled");
            return visual;
        }

        private static IEnumerable<CharacterSkinDef> SeatedSkins(BoatVisualDef dory)
        {
            string state = dory.Tiller.SeatedState;
            foreach (string file in Directory.GetFiles(SkinFolder, "*.asset"))
            {
                var skin = AssetDatabase.LoadAssetAtPath<CharacterSkinDef>(file.Replace('\\', '/'));
                if (skin == null || !skin.TryGetClip(state, out var clip) || clip.FrameCount <= 0) continue;
                yield return skin;
            }
        }

        private static Vector3 Pivot(BoatVisualDef d) => d.MotorMesh.PivotLocalMeters;
        private static Vector3 Mount(BoatVisualDef d) => HullPropFitment.Mount(0f, d.MotorMeshFitmentOffsetMeters);

        private static float SteerAt(BoatVisualDef d, int i) => d.MotorMaxSteerDegrees * i / SteerSteps;

        private static RowingTargets Station(BoatVisualDef d, float steer, float throttle) =>
            OutboardTillerStation.Targets(in d.Tiller, Pivot(d), Mount(d), OutboardMotorMeshPose.Rotation(steer, 0f), throttle);

        private static float Yaw(CharacterSkinDef skin, BoatVisualDef d, float twist) =>
            (skin.AzimuthCounterClockwise ? -d.Tiller.FacingDeckBearingDegrees : d.Tiller.FacingDeckBearingDegrees) + twist;

        private static Vector3 P(Matrix4x4[] w, int b) => new Vector3(w[b].m03, w[b].m13, w[b].m23);

        private static bool LegStraight(CharacterSkinDef skin, Matrix4x4[] w, string hipId, string kneeId, int foot)
        {
            int hip = skin.IndexOfBone(hipId), knee = skin.IndexOfBone(kneeId);
            float length = (P(w, knee) - P(w, hip)).magnitude + (P(w, foot) - P(w, knee)).magnitude;
            return (P(w, foot) - P(w, hip)).magnitude >= length - 1e-3f;
        }

        /// <summary>Runs <paramref name="check"/> on every seated skin, solved onto the tiller, at every
        /// steer, throttle and dial twist.</summary>
        private static void EverySolve(BoatVisualDef dory,
            System.Action<CharacterSkinDef, RowingPoseSolver, Matrix4x4[], RowingPoseSolver.Targets, RowingPoseSolver.Result, float, float, string> check)
        {
            int skins = 0;
            foreach (CharacterSkinDef skin in SeatedSkins(dory))
            {
                skins++;
                var solver = new RowingPoseSolver(skin.Bones, RowingBodyVolume.Of(skin));
                Assert.That(solver.IsUsable, Is.True, $"{skin.Id}: the solve needs pelvis, spine, legs, arms and carry sockets");
                skin.TryGetClip(dory.Tiller.SeatedState, out var clip);
                var world = new Matrix4x4[skin.Bones.Length];
                foreach (float twist in DialTwist)
                {
                    float yaw = Yaw(skin, dory, twist);
                    for (int i = -SteerSteps; i <= SteerSteps; i++)
                    foreach (float throttle in Throttles)
                    {
                        float steer = SteerAt(dory, i);
                        CharacterSkinPose.ComposeWorld(clip, 0, skin.Bones, world);
                        var t = RowingPoseSolver.ToFigure(Station(dory, steer, throttle), dory.Tiller.FloorOriginLocalMeters, yaw, 0f);
                        var r = solver.Solve(world, in t);
                        check(skin, solver, world, t, r, steer, yaw,
                              $"{skin.Id} steer {steer:0.0} throttle {throttle:0.0} yaw {yaw:0.00}");
                    }
                }
            }
            Assert.That(skins, Is.GreaterThan(0), $"no skin carries the seated clip '{dory.Tiller.SeatedState}'");
        }

        // ---------------------------------------------------------------- the station, pure

        [Test]
        public void TheGrip_TurnsWithTheEngine_ExactlyAsTheFittingIsDrawn()
        {
            BoatVisualDef dory = Dory();
            Vector3 pivot = Pivot(dory), mount = Mount(dory), rest = dory.Tiller.TillerGripAtRestLocalMeters;
            for (int i = -SteerSteps; i <= SteerSteps; i++)
            {
                Quaternion engine = OutboardMotorMeshPose.Rotation(SteerAt(dory, i), 0f);
                Vector3 drawn = pivot + mount + engine * (rest - pivot);
                Vector3 grip = TillerStationMath.Grip(rest, pivot, engine, mount);
                Assert.That((grip - drawn).magnitude, Is.LessThan(1e-6f), $"step {i}");
                RowingTargets t = Station(dory, SteerAt(dory, i), 0f);
                Vector3 held = t.StarboardGripValid ? t.StarboardGrip : t.PortGrip;
                Assert.That((held - grip).magnitude, Is.LessThan(1e-6f), $"step {i}: the held grip is the tiller");
                Assert.That(t.StarboardGripValid ^ t.PortGripValid, Is.True, $"step {i}: exactly one hand holds it");
            }
            Vector3 centre = TillerStationMath.Grip(rest, pivot, OutboardMotorMeshPose.Rotation(0f, 0f), mount);
            Assert.That((centre - (rest + mount)).magnitude, Is.LessThan(1e-6f), "amidships the grip is at rest on the transom");
        }

        [Test]
        public void TheSameHand_HoldsTheTiller_ThroughTheWholeSweep()
        {
            BoatVisualDef dory = Dory();
            bool first = Station(dory, SteerAt(dory, -SteerSteps), 0f).StarboardGripValid;
            for (int i = -SteerSteps; i <= SteerSteps; i++)
                Assert.That(Station(dory, SteerAt(dory, i), 1f).StarboardGripValid, Is.EqualTo(first), $"step {i}");
        }

        [Test]
        public void HeSitsUp_ByTheThrottlesSize_AheadOrAstern()
        {
            Assert.That(TillerStationMath.Lean(4f, 3f, 0f), Is.EqualTo(4f).Within(1e-6f));
            Assert.That(TillerStationMath.Lean(4f, 3f, 1f), Is.EqualTo(1f).Within(1e-6f));
            Assert.That(TillerStationMath.Lean(4f, 3f, -1f), Is.EqualTo(1f).Within(1e-6f));
            Assert.That(TillerStationMath.Lean(4f, 3f, 0.5f), Is.EqualTo(2.5f).Within(1e-6f));
            Assert.That(TillerStationMath.Lean(4f, 3f, 3f), Is.EqualTo(1f).Within(1e-6f), "clamped");
            Assert.That(TillerStationMath.Lean(4f, 3f, float.NaN), Is.EqualTo(4f).Within(1e-6f), "NaN reads idle");
            BoatVisualDef dory = Dory();
            Assert.That(Station(dory, 0f, 1f).RestSlumpDegrees,
                Is.EqualTo(dory.Tiller.IdleLeanDegrees - dory.Tiller.SitUpPerThrottleDegrees).Within(1e-6f));
            Assert.That(Station(dory, 0f, 0f).RestSlumpDegrees, Is.EqualTo(dory.Tiller.IdleLeanDegrees).Within(1e-6f));
            Assert.That(Station(dory, 0f, 1f).RestWeight, Is.EqualTo(1f), "the free hand rests on his thigh");
        }

        [Test]
        public void TheStation_IsDeterministic()
        {
            BoatVisualDef dory = Dory();
            for (int i = -SteerSteps; i <= SteerSteps; i++)
            {
                RowingTargets a = Station(dory, SteerAt(dory, i), 0.7f), b = Station(dory, SteerAt(dory, i), 0.7f);
                Assert.That(a.StarboardGrip, Is.EqualTo(b.StarboardGrip));
                Assert.That(a.PortGrip, Is.EqualTo(b.PortGrip));
                Assert.That(a.RestSlumpDegrees, Is.EqualTo(b.RestSlumpDegrees));
            }
        }

        // ---------------------------------------------------------------- the dory's data

        [Test]
        public void TheDorysTiller_IsData_SeatOnTheSternBench_OnlyWhenTheEngineIsFitted()
        {
            BoatVisualDef dory = Dory();
            Assert.That(dory.SeatsAtTiller(PropulsionType.Engine), Is.True, "with her motor he sits at the tiller");
            Assert.That(dory.SeatsAtTiller(PropulsionType.Oars), Is.False, "rowed, she keeps her rowing station");
            Assert.That(dory.MotorFitMesh, Is.Not.Null, "the stern bench and motor board are baked");
            Assert.That(dory.MotorFitMesh.IsUsable(), Is.True, "the motor fit mesh is usable");
            Assert.That(string.IsNullOrEmpty(dory.Tiller.SeatedState), Is.False, "the seated state is named");

            Vector3 seat = dory.Tiller.SeatLocalMeters;
            Assert.That(seat.z, Is.EqualTo(BenchTopZ).Within(Rigid), "the seat is the bench top");
            Assert.That(Mathf.Abs(seat.x), Is.LessThan(BenchHalfWidth), "on the bench athwartships");
            Assert.That(seat.y, Is.InRange(BenchAftY, BenchForeY), "on the bench fore and aft");
            Assert.That(dory.Tiller.PortFootBraceLocalMeters.z, Is.LessThan(seat.z), "his feet are below the bench");
            Assert.That(dory.Tiller.StarboardFootBraceLocalMeters.z, Is.LessThan(seat.z), "his feet are below the bench");
            Assert.That(dory.Tiller.PortFootBraceLocalMeters.y, Is.GreaterThan(seat.y), "his feet are ahead of the bench");
            Assert.That(dory.Tiller.StarboardFootBraceLocalMeters.y, Is.GreaterThan(seat.y), "his feet are ahead of the bench");
            Assert.That(dory.PilotDeckBearingDegrees(PropulsionType.Engine),
                Is.EqualTo(dory.Tiller.FacingDeckBearingDegrees).Within(Rigid), "he faces the station's bearing");
        }

        // ---------------------------------------------------------------- the solve on every skin

        [Test]
        public void EverySeatedSkin_HandOnTheTillerGrip_AcrossTheWholeSweep()
        {
            BoatVisualDef dory = Dory();
            var failures = new StringBuilder();
            EverySolve(dory, (skin, solver, world, t, r, steer, yaw, at) =>
            {
                bool right = t.HasGripRight;
                Vector3 grip = right ? t.GripRight : t.GripLeft;
                float miss = (P(world, right ? solver.PalmRight : solver.PalmLeft) - grip).magnitude;
                if (miss > HalfPixelMeters) failures.AppendLine($"{at}: hand {miss:0.0000} m off the grip");
                float use = right ? r.ReachUseRight : r.ReachUseLeft;
                if (use > 1f + 1e-4f) failures.AppendLine($"{at}: the arm is stretched ({use:0.000})");
            });
            Assert.That(failures.Length, Is.EqualTo(0), failures.ToString());
        }

        [Test]
        public void HeSitsOnTheBench_WithHisFeetBraced()
        {
            BoatVisualDef dory = Dory();
            float seatZ = dory.Tiller.SeatLocalMeters.z - dory.Tiller.FloorOriginLocalMeters.z;
            var failures = new StringBuilder();
            foreach (CharacterSkinDef skin in SeatedSkins(dory))
            {
                var solver = new RowingPoseSolver(skin.Bones, RowingBodyVolume.Of(skin));
                skin.TryGetClip(dory.Tiller.SeatedState, out var clip);
                var world = new Matrix4x4[skin.Bones.Length];
                for (int i = -SteerSteps; i <= SteerSteps; i += 4)
                {
                    CharacterSkinPose.ComposeWorld(clip, 0, skin.Bones, world);
                    float ankleL = world[solver.FootLeft].m23, ankleR = world[solver.FootRight].m23;
                    var t = RowingPoseSolver.ToFigure(Station(dory, SteerAt(dory, i), 0f),
                                                      dory.Tiller.FloorOriginLocalMeters, Yaw(skin, dory, 0f), 0f);
                    solver.Solve(world, in t);
                    string at = $"{skin.Id} step {i}";
                    if ((P(world, solver.PelvisBone) - t.Pelvis).magnitude > Rigid) failures.AppendLine($"{at}: pelvis off the seat");
                    if (Mathf.Abs(P(world, solver.PelvisBone).z - (seatZ + RowingPoseSolver.PelvisAboveSeatMeters)) > Rigid)
                        failures.AppendLine($"{at}: pelvis height wrong");
                    float footL = (P(world, solver.FootLeft) - (t.BraceLeft + new Vector3(0f, 0f, ankleL))).magnitude;
                    float footR = (P(world, solver.FootRight) - (t.BraceRight + new Vector3(0f, 0f, ankleR))).magnitude;
                    bool reachedL = footL <= HalfPixelMeters || LegStraight(skin, world, "hip_L", "knee_L", solver.FootLeft);
                    bool reachedR = footR <= HalfPixelMeters || LegStraight(skin, world, "hip_R", "knee_R", solver.FootRight);
                    if (!reachedL || !reachedR)
                        failures.AppendLine($"{at}: foot off its brace L {footL:0.0000} R {footR:0.0000}, leg not straight toward it");
                }
            }
            Assert.That(failures.Length, Is.EqualTo(0), failures.ToString());
        }

        [Test]
        public void NoSelfClip_HisArmsAndTheTillerStayOutsideHisBody_AcrossTheWholeSweep()
        {
            BoatVisualDef dory = Dory();
            Vector3 pivot = Pivot(dory), mount = Mount(dory);
            var failures = new StringBuilder();
            float worstArm = float.PositiveInfinity, worstBar = float.PositiveInfinity;
            EverySolve(dory, (skin, solver, world, t, r, steer, yaw, at) =>
            {
                float arm = Mathf.Min(solver.ArmClearance(world, true), solver.ArmClearance(world, false));
                worstArm = Mathf.Min(worstArm, arm);
                if (arm < 0f) failures.AppendLine($"{at}: an arm is {-arm:0.0000} m inside his body");
                Quaternion engine = OutboardMotorMeshPose.Rotation(steer, 0f);
                Vector3 grip = TillerStationMath.Grip(dory.Tiller.TillerGripAtRestLocalMeters, pivot, engine, mount);
                Quaternion toFigure = Quaternion.Inverse(Quaternion.AngleAxis(yaw, Vector3.forward));
                for (int k = 0; k <= BarSamples; k++)
                {
                    Vector3 onHull = Vector3.Lerp(grip, pivot + mount, (float)k / BarSamples);
                    float o = solver.Body.Outside(world, toFigure * (onHull - dory.Tiller.FloorOriginLocalMeters));
                    worstBar = Mathf.Min(worstBar, o);
                    if (o < 0f) { failures.AppendLine($"{at}: the tiller passes {-o:0.0000} m inside his body at {k}/{BarSamples}"); break; }
                }
            });
            TestContext.WriteLine($"worst arm clearance {worstArm:0.0000} m, worst tiller clearance {worstBar:0.0000} m");
            Assert.That(failures.Length, Is.EqualTo(0), failures.ToString());
        }
    }
}
