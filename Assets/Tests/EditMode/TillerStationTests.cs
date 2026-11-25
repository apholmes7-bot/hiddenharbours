using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using HiddenHarbours.Player;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The skipper at a tiller outboard: the station (<see cref="OutboardTillerStation.Targets"/>, each
    /// tiller hull's <see cref="BoatVisualDef.Tiller"/> data) and the seated solve
    /// (<see cref="RowingPoseSolver"/>) run on every committed skin's seated clip, across the engine's
    /// whole steering range (his shoulders and head turning into the turn as they do in play), at idle
    /// and at full throttle, in every facing the figure can be drawn at on the hull. Every hull whose
    /// visual enables a tiller seat is a case: the outboard dory and both punts. Pure arithmetic on
    /// committed assets; no scene, no clock.
    /// </summary>
    public class TillerStationTests
    {
        private const string SkinFolder = "Assets/_Project/Data/Characters/Skin";
        private const string VisualFolder = "Assets/_Project/Data/Boats/Visuals";

        /// <summary>Half a pixel at the skins' 32 px per metre: a hand closer than this is drawn ON the grip.</summary>
        private const float HalfPixelMeters = 0.5f / 32f;

        private const float Rigid = 1e-4f;

        /// <summary>Steering samples across the engine's range, each side.</summary>
        private const int SteerSteps = 16;

        /// <summary>Points sampled along the tiller arm, grip to pivot.</summary>
        private const int BarSamples = 20;

        /// <summary>The least the head must turn toward the bow's swing with the tiller hard over, degrees:
        /// enough to read at game zoom, whatever the clear step does to his shoulders.</summary>
        private const float LeastHardOverLookDegrees = 3f;

        /// <summary>The figure heading dial can twist him on the hull by up to half a step of a
        /// 16-step dial either way.</summary>
        private static readonly float[] DialTwist = { 0f, -11.25f, 11.25f };

        private static readonly float[] Throttles = { 0f, 0.5f, 1f, -1f };

        /// <summary>A tiller hull and the bench his seat must be on (rig facts, read off each rig).</summary>
        public sealed class TillerHull
        {
            public string Visual;
            public float BenchTopZ, BenchHalfWidth, BenchAftY, BenchForeY;
            /// <summary>True when the bench is a fitting hung with the engine (the dory), false when it is
            /// part of the hull (the punt).</summary>
            public bool BenchIsMotorFit;
            public override string ToString() => Visual;
        }

        /// <summary>
        /// The dory: the motor_fit stern bench in docs/art/rigs/doryIsoRig.js (part 'motor_fit').
        /// The punt (both builds): the stern bench in docs/art/rigs/gameplay/puntIsoRig.gameplay.json
        /// (DECK obstruction 'stern_bench', top = STATIONS.helm_seat z).
        /// </summary>
        public static IEnumerable<TillerHull> Hulls()
        {
            yield return new TillerHull { Visual = "DoryIso", BenchTopZ = 0.3584f, BenchHalfWidth = 0.413f,
                                          BenchAftY = -1.795f, BenchForeY = -1.535f, BenchIsMotorFit = true };
            yield return new TillerHull { Visual = "PuntIsoBasic", BenchTopZ = 0.316f, BenchHalfWidth = 0.622f,
                                          BenchAftY = -1.99f, BenchForeY = -1.65f };
            yield return new TillerHull { Visual = "PuntIsoUpgraded", BenchTopZ = 0.316f, BenchHalfWidth = 0.622f,
                                          BenchAftY = -1.99f, BenchForeY = -1.65f };
        }

        private static BoatVisualDef Load(TillerHull hull)
        {
            string path = $"{VisualFolder}/{hull.Visual}.asset";
            var visual = AssetDatabase.LoadAssetAtPath<BoatVisualDef>(path);
            Assert.That(visual, Is.Not.Null, path);
            Assert.That(visual.MotorMesh, Is.Not.Null, $"{hull}: the outboard fitting");
            Assert.That(visual.Tiller.Enabled, Is.True, $"{hull}: the tiller station is enabled");
            return visual;
        }

        private static IEnumerable<CharacterSkinDef> SeatedSkins(BoatVisualDef d)
        {
            string state = d.Tiller.SeatedState;
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

        /// <summary>The station exactly as play asks it: the engine posed at <paramref name="steer"/>, and
        /// the same steer (over the hull's authority) turning him into the turn.</summary>
        private static RowingTargets Station(BoatVisualDef d, float steer, float throttle) =>
            OutboardTillerStation.Targets(in d.Tiller, Pivot(d), Mount(d), OutboardMotorMeshPose.Rotation(steer, 0f), throttle,
                                          steer, d.MotorMaxSteerDegrees);

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
        private static void EverySolve(BoatVisualDef d,
            System.Action<CharacterSkinDef, RowingPoseSolver, Matrix4x4[], RowingPoseSolver.Targets, RowingPoseSolver.Result, float, float, string> check)
        {
            int skins = 0;
            foreach (CharacterSkinDef skin in SeatedSkins(d))
            {
                skins++;
                var solver = new RowingPoseSolver(skin.Bones, RowingBodyVolume.Of(skin));
                Assert.That(solver.IsUsable, Is.True, $"{skin.Id}: the solve needs pelvis, spine, legs, arms and carry sockets");
                skin.TryGetClip(d.Tiller.SeatedState, out var clip);
                var world = new Matrix4x4[skin.Bones.Length];
                foreach (float twist in DialTwist)
                {
                    float yaw = Yaw(skin, d, twist);
                    for (int i = -SteerSteps; i <= SteerSteps; i++)
                    foreach (float throttle in Throttles)
                    {
                        float steer = SteerAt(d, i);
                        CharacterSkinPose.ComposeWorld(clip, 0, skin.Bones, world);
                        var t = RowingPoseSolver.ToFigure(Station(d, steer, throttle), d.Tiller.FloorOriginLocalMeters, yaw, 0f);
                        var r = solver.Solve(world, in t);
                        check(skin, solver, world, t, r, steer, yaw,
                              $"{d.Id} {skin.Id} steer {steer:0.0} throttle {throttle:0.0} yaw {yaw:0.00}");
                    }
                }
            }
            Assert.That(skins, Is.GreaterThan(0), $"no skin carries the seated clip '{d.Tiller.SeatedState}'");
        }

        // ---------------------------------------------------------------- the station, pure

        [TestCaseSource(nameof(Hulls))]
        public void TheGrip_TurnsWithTheEngine_ExactlyAsTheFittingIsDrawn(TillerHull hull)
        {
            BoatVisualDef d = Load(hull);
            Vector3 pivot = Pivot(d), mount = Mount(d), rest = d.Tiller.TillerGripAtRestLocalMeters;
            for (int i = -SteerSteps; i <= SteerSteps; i++)
            {
                Quaternion engine = OutboardMotorMeshPose.Rotation(SteerAt(d, i), 0f);
                Vector3 drawn = pivot + mount + engine * (rest - pivot);
                Vector3 grip = TillerStationMath.Grip(rest, pivot, engine, mount);
                Assert.That((grip - drawn).magnitude, Is.LessThan(1e-6f), $"step {i}");
                RowingTargets t = Station(d, SteerAt(d, i), 0f);
                Vector3 held = t.StarboardGripValid ? t.StarboardGrip : t.PortGrip;
                Assert.That((held - grip).magnitude, Is.LessThan(1e-6f), $"step {i}: the held grip is the tiller");
                Assert.That(t.StarboardGripValid ^ t.PortGripValid, Is.True, $"step {i}: exactly one hand holds it");
            }
            Vector3 centre = TillerStationMath.Grip(rest, pivot, OutboardMotorMeshPose.Rotation(0f, 0f), mount);
            Assert.That((centre - (rest + mount)).magnitude, Is.LessThan(1e-6f), "amidships the grip is at rest on the transom");
        }

        [TestCaseSource(nameof(Hulls))]
        public void TheSameHand_HoldsTheTiller_ThroughTheWholeSweep(TillerHull hull)
        {
            BoatVisualDef d = Load(hull);
            bool first = Station(d, SteerAt(d, -SteerSteps), 0f).StarboardGripValid;
            for (int i = -SteerSteps; i <= SteerSteps; i++)
                Assert.That(Station(d, SteerAt(d, i), 1f).StarboardGripValid, Is.EqualTo(first), $"step {i}");
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
            foreach (TillerHull hull in Hulls())
            {
                BoatVisualDef d = Load(hull);
                Assert.That(Station(d, 0f, 1f).RestSlumpDegrees,
                    Is.EqualTo(d.Tiller.IdleLeanDegrees - d.Tiller.SitUpPerThrottleDegrees).Within(1e-6f), hull.Visual);
                Assert.That(Station(d, 0f, 0f).RestSlumpDegrees, Is.EqualTo(d.Tiller.IdleLeanDegrees).Within(1e-6f), hull.Visual);
                Assert.That(Station(d, 0f, 1f).RestWeight, Is.EqualTo(1f), $"{hull}: the free hand rests on his thigh");
            }
        }

        [Test]
        public void HeLooks_TheWayTheBowSwings_ByTheSteersShareOfItsAuthority()
        {
            // +steer swings the prop to starboard, so the bow to starboard: he looks to starboard.
            Assert.That(TillerStationMath.Look(10f, 32f, 32f), Is.EqualTo(10f).Within(1e-6f));
            Assert.That(TillerStationMath.Look(10f, -32f, 32f), Is.EqualTo(-10f).Within(1e-6f));
            Assert.That(TillerStationMath.Look(10f, 16f, 32f), Is.EqualTo(5f).Within(1e-6f));
            Assert.That(TillerStationMath.Look(10f, 0f, 32f), Is.EqualTo(0f).Within(1e-6f));
            Assert.That(TillerStationMath.Look(10f, 90f, 32f), Is.EqualTo(10f).Within(1e-6f), "clamped to the authority");
            Assert.That(TillerStationMath.Look(10f, float.NaN, 32f), Is.EqualTo(0f), "NaN reads dead ahead");
            Assert.That(TillerStationMath.Look(10f, 20f, 0f), Is.EqualTo(0f), "no authority reads dead ahead");

            foreach (TillerHull hull in Hulls())
            {
                BoatVisualDef d = Load(hull);
                Assert.That(d.Tiller.LookIntoTurnDegrees, Is.GreaterThan(0f), $"{hull}: he turns his shoulders into a turn");
                Assert.That(d.Tiller.HeadLookIntoTurnDegrees, Is.GreaterThan(0f), $"{hull}: his head turns further");
                RowingTargets hard = Station(d, d.MotorMaxSteerDegrees, 0f);
                Assert.That(hard.TurnStarboardDegrees, Is.EqualTo(d.Tiller.LookIntoTurnDegrees).Within(1e-5f), hull.Visual);
                Assert.That(hard.HeadTurnStarboardDegrees, Is.EqualTo(d.Tiller.HeadLookIntoTurnDegrees).Within(1e-5f), hull.Visual);
                RowingTargets ahead = Station(d, 0f, 0f);
                Assert.That(ahead.TurnStarboardDegrees, Is.EqualTo(0f), $"{hull}: dead ahead he sits square");
                Assert.That(ahead.HeadTurnStarboardDegrees, Is.EqualTo(0f), $"{hull}: dead ahead he looks ahead");
            }
        }

        [Test]
        public void ARower_NeverTurns_TheLookIsZeroUnlessAStationAsks()
        {
            var oars = new RowingTargets { PortGripValid = true, StarboardGripValid = true, RestWeight = 0f };
            RowingPoseSolver.Targets t = RowingPoseSolver.ToFigure(oars, Vector3.zero, 180f, 0.5f);
            Assert.That(t.TurnDegrees, Is.EqualTo(0f));
            Assert.That(t.HeadTurnDegrees, Is.EqualTo(0f));
            // And a turn toward starboard is clockwise seen from above, whichever way he faces.
            var look = new RowingTargets { TurnStarboardDegrees = 5f, HeadTurnStarboardDegrees = 9f };
            foreach (float yaw in new[] { 0f, 25f, -25f, 180f })
            {
                RowingPoseSolver.Targets f = RowingPoseSolver.ToFigure(look, Vector3.zero, yaw, 0f);
                Assert.That(f.TurnDegrees, Is.EqualTo(-5f), $"yaw {yaw}");
                Assert.That(f.HeadTurnDegrees, Is.EqualTo(-9f), $"yaw {yaw}");
            }
        }

        [TestCaseSource(nameof(Hulls))]
        public void TheStation_IsDeterministic(TillerHull hull)
        {
            BoatVisualDef d = Load(hull);
            for (int i = -SteerSteps; i <= SteerSteps; i++)
            {
                RowingTargets a = Station(d, SteerAt(d, i), 0.7f), b = Station(d, SteerAt(d, i), 0.7f);
                Assert.That(a.StarboardGrip, Is.EqualTo(b.StarboardGrip));
                Assert.That(a.PortGrip, Is.EqualTo(b.PortGrip));
                Assert.That(a.RestSlumpDegrees, Is.EqualTo(b.RestSlumpDegrees));
                Assert.That(a.TurnStarboardDegrees, Is.EqualTo(b.TurnStarboardDegrees));
                Assert.That(a.HeadTurnStarboardDegrees, Is.EqualTo(b.HeadTurnStarboardDegrees));
            }
        }

        [Test]
        public void TheHandReadsTheTillerOfTheSameFrame_TheEngineIsPosedBeforeTheFigure()
        {
            // The engine's LateUpdate sets the steer the station reads; the rider's LateUpdate poses the
            // figure from it. A lower execution order runs first, so the hand never lags the tiller.
            int Order(System.Type type)
            {
                var a = type.GetCustomAttribute<DefaultExecutionOrder>();
                return a != null ? a.order : 0;
            }
            Assert.That(Order(typeof(OutboardMotorMeshLayer)), Is.LessThan(Order(typeof(DeckRiderVisual))),
                "the engine must be posed before the figure that holds its tiller");
        }

        // ---------------------------------------------------------------- each hull's data

        [TestCaseSource(nameof(Hulls))]
        public void TheTiller_IsData_SeatOnTheSternBench_OnlyUnderEngine(TillerHull hull)
        {
            BoatVisualDef d = Load(hull);
            Assert.That(d.SeatsAtTiller(PropulsionType.Engine), Is.True, $"{hull}: under engine he sits at the tiller");
            Assert.That(d.SeatsAtTiller(PropulsionType.Oars), Is.False, $"{hull}: rowed, she keeps her rowing station");
            if (hull.BenchIsMotorFit)
            {
                Assert.That(d.MotorFitMesh, Is.Not.Null, $"{hull}: the stern bench and motor board are baked");
                Assert.That(d.MotorFitMesh.IsUsable(), Is.True, $"{hull}: the motor fit mesh is usable");
            }
            Assert.That(string.IsNullOrEmpty(d.Tiller.SeatedState), Is.False, $"{hull}: the seated state is named");

            Vector3 seat = d.Tiller.SeatLocalMeters;
            Assert.That(seat.z, Is.EqualTo(hull.BenchTopZ).Within(Rigid), $"{hull}: the seat is the bench top");
            Assert.That(Mathf.Abs(seat.x), Is.LessThan(hull.BenchHalfWidth), $"{hull}: on the bench athwartships");
            Assert.That(seat.y, Is.InRange(hull.BenchAftY, hull.BenchForeY), $"{hull}: on the bench fore and aft");
            Assert.That(d.Tiller.PortFootBraceLocalMeters.z, Is.LessThan(seat.z), $"{hull}: his feet are below the bench");
            Assert.That(d.Tiller.StarboardFootBraceLocalMeters.z, Is.LessThan(seat.z), $"{hull}: his feet are below the bench");
            Assert.That(d.Tiller.PortFootBraceLocalMeters.y, Is.GreaterThan(hull.BenchForeY), $"{hull}: his feet are ahead of the bench");
            Assert.That(d.Tiller.StarboardFootBraceLocalMeters.y, Is.GreaterThan(hull.BenchForeY), $"{hull}: his feet are ahead of the bench");
            Assert.That(d.PilotDeckBearingDegrees(PropulsionType.Engine),
                Is.EqualTo(d.Tiller.FacingDeckBearingDegrees).Within(Rigid), $"{hull}: he faces the station's bearing");
        }

        // ---------------------------------------------------------------- the solve on every skin

        [TestCaseSource(nameof(Hulls))]
        public void EverySeatedSkin_HandOnTheTillerGrip_AcrossTheWholeSweep(TillerHull hull)
        {
            BoatVisualDef d = Load(hull);
            var failures = new StringBuilder();
            EverySolve(d, (skin, solver, world, t, r, steer, yaw, at) =>
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

        [TestCaseSource(nameof(Hulls))]
        public void HeSitsOnTheBench_WithHisFeetBraced(TillerHull hull)
        {
            BoatVisualDef d = Load(hull);
            float seatZ = d.Tiller.SeatLocalMeters.z - d.Tiller.FloorOriginLocalMeters.z;
            var failures = new StringBuilder();
            foreach (CharacterSkinDef skin in SeatedSkins(d))
            {
                var solver = new RowingPoseSolver(skin.Bones, RowingBodyVolume.Of(skin));
                skin.TryGetClip(d.Tiller.SeatedState, out var clip);
                var world = new Matrix4x4[skin.Bones.Length];
                for (int i = -SteerSteps; i <= SteerSteps; i += 4)
                {
                    CharacterSkinPose.ComposeWorld(clip, 0, skin.Bones, world);
                    float ankleL = world[solver.FootLeft].m23, ankleR = world[solver.FootRight].m23;
                    var t = RowingPoseSolver.ToFigure(Station(d, SteerAt(d, i), 0f),
                                                      d.Tiller.FloorOriginLocalMeters, Yaw(skin, d, 0f), 0f);
                    solver.Solve(world, in t);
                    string at = $"{hull} {skin.Id} step {i}";
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

        [TestCaseSource(nameof(Hulls))]
        public void NoSelfClip_HisArmsAndTheTillerStayOutsideHisBody_AcrossTheWholeSweep(TillerHull hull)
        {
            BoatVisualDef d = Load(hull);
            Vector3 pivot = Pivot(d), mount = Mount(d);
            var failures = new StringBuilder();
            float worstArm = float.PositiveInfinity, worstBar = float.PositiveInfinity;
            EverySolve(d, (skin, solver, world, t, r, steer, yaw, at) =>
            {
                float arm = Mathf.Min(solver.ArmClearance(world, true), solver.ArmClearance(world, false));
                worstArm = Mathf.Min(worstArm, arm);
                if (arm < 0f) failures.AppendLine($"{at}: an arm is {-arm:0.0000} m inside his body");
                Quaternion engine = OutboardMotorMeshPose.Rotation(steer, 0f);
                Vector3 grip = TillerStationMath.Grip(d.Tiller.TillerGripAtRestLocalMeters, pivot, engine, mount);
                Quaternion toFigure = Quaternion.Inverse(Quaternion.AngleAxis(yaw, Vector3.forward));
                for (int k = 0; k <= BarSamples; k++)
                {
                    Vector3 onHull = Vector3.Lerp(grip, pivot + mount, (float)k / BarSamples);
                    float o = solver.Body.Outside(world, toFigure * (onHull - d.Tiller.FloorOriginLocalMeters));
                    worstBar = Mathf.Min(worstBar, o);
                    if (o < 0f) { failures.AppendLine($"{at}: the tiller passes {-o:0.0000} m inside his body at {k}/{BarSamples}"); break; }
                }
            });
            TestContext.WriteLine($"{hull}: worst arm clearance {worstArm:0.0000} m, worst tiller clearance {worstBar:0.0000} m");
            Assert.That(failures.Length, Is.EqualTo(0), failures.ToString());
        }

        /// <summary>The signed turn about the vertical (degrees, positive counter-clockwise seen from
        /// above) carrying bone <paramref name="b"/>'s horizontal heading in <paramref name="from"/> to
        /// its heading in <paramref name="to"/>. Uses whichever of the bone's x and y axes lies flatter,
        /// so it does not depend on which axis a skin calls forward.</summary>
        private static float YawBetween(Matrix4x4[] from, Matrix4x4[] to, int b)
        {
            Vector3 ax = new Vector3(from[b].m00, from[b].m10, 0f), ay = new Vector3(from[b].m01, from[b].m11, 0f);
            bool useX = ax.sqrMagnitude >= ay.sqrMagnitude;
            Vector3 a = useX ? ax : ay;
            Vector3 c = useX ? new Vector3(to[b].m00, to[b].m10, 0f) : new Vector3(to[b].m01, to[b].m11, 0f);
            return Mathf.Atan2(a.x * c.y - a.y * c.x, a.x * c.x + a.y * c.y) * Mathf.Rad2Deg;
        }

        [TestCaseSource(nameof(Hulls))]
        public void HeLooksIntoTheTurn_HisHeadTurnsTheWayTheBowSwings_OnEverySkin(TillerHull hull)
        {
            BoatVisualDef d = Load(hull);
            var failures = new StringBuilder();
            int checkedSkins = 0;
            foreach (CharacterSkinDef skin in SeatedSkins(d))
            {
                var solver = new RowingPoseSolver(skin.Bones, RowingBodyVolume.Of(skin));
                if (solver.NeckBone < 0) continue;
                checkedSkins++;
                skin.TryGetClip(d.Tiller.SeatedState, out var clip);
                var square = new Matrix4x4[skin.Bones.Length];
                var turned = new Matrix4x4[skin.Bones.Length];
                float yaw = Yaw(skin, d, 0f);
                foreach (float throttle in Throttles)
                {
                    CharacterSkinPose.ComposeWorld(clip, 0, skin.Bones, square);
                    solver.Solve(square, RowingPoseSolver.ToFigure(Station(d, 0f, throttle), d.Tiller.FloorOriginLocalMeters, yaw, 0f));
                    foreach (int side in new[] { -1, 1 })
                    {
                        CharacterSkinPose.ComposeWorld(clip, 0, skin.Bones, turned);
                        float steer = side * d.MotorMaxSteerDegrees;
                        solver.Solve(turned, RowingPoseSolver.ToFigure(Station(d, steer, throttle), d.Tiller.FloorOriginLocalMeters, yaw, 0f));
                        // +steer swings the bow to starboard: clockwise seen from above, a NEGATIVE turn.
                        float look = -side * YawBetween(square, turned, solver.NeckBone);
                        if (look < LeastHardOverLookDegrees)
                            failures.AppendLine($"{hull} {skin.Id} steer {steer:0} throttle {throttle:0.0}: head turned {look:0.0} deg into the turn");
                    }
                }
            }
            Assert.That(checkedSkins, Is.GreaterThan(0), "no seated skin carries a neck bone");
            Assert.That(failures.Length, Is.EqualTo(0), failures.ToString());
        }
    }
}
