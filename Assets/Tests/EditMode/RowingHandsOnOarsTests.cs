using System.Collections.Generic;
using System.IO;
using System.Text;
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
    /// The rower's hands are on the oars: the dory's rowing station (<see cref="DoryOarMeshLayer.StationTargets"/>,
    /// the dory's <see cref="BoatVisualDef.Rowing"/> data and its oarlocks) and the seated solve
    /// (<see cref="RowingPoseSolver"/>) run on every committed skin's real <c>row_oars</c> clip, at many
    /// phases of the stroke and in every facing the figure can be drawn at on the hull. Pure
    /// arithmetic on committed assets; no scene, no clock.
    /// </summary>
    public class RowingHandsOnOarsTests
    {
        private const string SkinFolder = "Assets/_Project/Data/Characters/Skin";
        private const string DoryVisualPath = "Assets/_Project/Data/Boats/Visuals/DoryIso.asset";

        /// <summary>Stroke phases sampled per test: twice the clip's 16 frames, so every frame and every
        /// hold between frames is seen.</summary>
        private const int Phases = 32;

        /// <summary>The figure's root on the dory at the oars: the after thwart's station (the deck's
        /// helm station, x 0 y -0.72) at the deck plane (z 0.09). The solve is exact for any root; this
        /// is only where the clip's unsolved bones start.</summary>
        private static readonly Vector3 Root = new Vector3(0f, -0.72f, 0.09f);

        /// <summary>The rower's deck bearing (the def's OarsPilotDeckBearingDegrees, the stern), and
        /// that bearing turned by up to half a step of the figure heading dial at 16 and at 8 steps,
        /// the most a dial can twist him on the hull.</summary>
        private static readonly float[] Facings = { 180f, 168.75f, 191.25f, 157.5f, 202.5f };

        /// <summary>Half a pixel at the skins' 32 px per metre: a hand closer than this is drawn ON
        /// the handle.</summary>
        private const float HalfPixelMeters = 0.5f / 32f;

        private const float Rigid = 1e-4f;

        private static BoatVisualDef Dory()
        {
            var visual = AssetDatabase.LoadAssetAtPath<BoatVisualDef>(DoryVisualPath);
            Assert.That(visual, Is.Not.Null, DoryVisualPath);
            Assert.That(visual.OarPortMesh, Is.Not.Null, "the dory's port oar fitting");
            Assert.That(visual.OarStarMesh, Is.Not.Null, "the dory's starboard oar fitting");
            return visual;
        }

        private static IEnumerable<CharacterSkinDef> RowingSkins()
        {
            foreach (string file in Directory.GetFiles(SkinFolder, "*.asset"))
            {
                var skin = AssetDatabase.LoadAssetAtPath<CharacterSkinDef>(file.Replace('\\', '/'));
                if (skin == null || !skin.TryGetClip(RowingFigure.RowStrokeKey, out var clip) || clip.FrameCount <= 0) continue;
                yield return skin;
            }
        }

        private static RowingTargets StationAt(BoatVisualDef dory, float strokePhase, bool portRowing = true, bool starRowing = true)
        {
            DoryOarMeshPose.Pose pose = DoryOarMeshPose.Row(strokePhase + DoryOarMeshPose.RowCatchTurns);
            return DoryOarMeshLayer.StationTargets(dory.OarPortMesh.PivotLocalMeters, dory.OarStarMesh.PivotLocalMeters,
                                                   in dory.Rowing, pose, portRowing, pose, starRowing);
        }

        private static RowingPoseSolver.Targets FigureTargets(in RowingTargets onHull, float strokePhase, float yaw)
        {
            bool facesBow = RowingFigure.FacesBow(yaw);
            float drive = RowingFigure.RigDrive(RowingFigure.BodyPhase(strokePhase, facesBow));
            return RowingPoseSolver.ToFigure(onHull, Root, yaw, drive);
        }

        private static void Compose(CharacterSkinDef skin, float strokePhase, float yaw, Matrix4x4[] world)
        {
            skin.TryGetClip(RowingFigure.RowStrokeKey, out var clip);
            float body = RowingFigure.BodyPhase(strokePhase, RowingFigure.FacesBow(yaw));
            int frame = RowingFigure.FrameFor(body, clip.FrameCount, RowingFigure.RowStrokeStrokesPerLoop);
            CharacterSkinPose.ComposeWorld(clip, frame, skin.Bones, world);
        }

        private static Vector3 P(Matrix4x4[] w, int b) => new Vector3(w[b].m03, w[b].m13, w[b].m23);

        /// <summary>True when the leg hip-knee-foot is straight (within a millimetre of its full length).</summary>
        private static bool LegStraight(CharacterSkinDef skin, Matrix4x4[] w, string hipId, string kneeId, int foot)
        {
            int hip = skin.IndexOfBone(hipId), knee = skin.IndexOfBone(kneeId);
            float length = (P(w, knee) - P(w, hip)).magnitude + (P(w, foot) - P(w, knee)).magnitude;
            return (P(w, foot) - P(w, hip)).magnitude >= length - 1e-3f;
        }

        // ---------------------------------------------------------------- the cause, pinned

        [Test]
        public void TheClipAlone_HoldsTheAirBesideTheHandles()
        {
            // Why the playtest saw his hands off the oars: the clip is a standing stroke authored in the
            // chest's frame, and nothing tied its palms to the handles. On the skipper they miss by
            // 0.22 to 0.77 m. This pins that the solve is what puts them there, not the clip.
            BoatVisualDef dory = Dory();
            var skin = AssetDatabase.LoadAssetAtPath<CharacterSkinDef>(SkinFolder + "/skipper.asset");
            Assert.That(skin, Is.Not.Null);
            var solver = new RowingPoseSolver(skin.Bones);
            var world = new Matrix4x4[skin.Bones.Length];

            float worst = 0f;
            for (int i = 0; i < Phases; i++)
            {
                float s = (float)i / Phases;
                Compose(skin, s, 180f, world);
                RowingTargets hull = StationAt(dory, s);
                RowingPoseSolver.Targets t = FigureTargets(hull, s, 180f);
                worst = Mathf.Max(worst, (P(world, solver.PalmLeft) - t.GripLeft).magnitude);
                worst = Mathf.Max(worst, (P(world, solver.PalmRight) - t.GripRight).magnitude);
            }
            Assert.That(worst, Is.GreaterThan(0.2f), "the unsolved clip's palms should be well off the grips");
        }

        // ---------------------------------------------------------------- the hands

        [Test]
        public void EveryRowingSkin_HandsOnTheGrips_ThroughTheStroke_InEveryFacing()
        {
            BoatVisualDef dory = Dory();
            var failures = new StringBuilder();
            int skins = 0;
            foreach (CharacterSkinDef skin in RowingSkins())
            {
                skins++;
                var solver = new RowingPoseSolver(skin.Bones);
                Assert.That(solver.IsUsable, Is.True, $"{skin.Id}: the solve needs pelvis, spine, legs, arms and carry sockets");
                var world = new Matrix4x4[skin.Bones.Length];
                foreach (float yaw in Facings)
                {
                    for (int i = 0; i < Phases; i++)
                    {
                        float s = (float)i / Phases;
                        Compose(skin, s, yaw, world);
                        RowingPoseSolver.Targets t = FigureTargets(StationAt(dory, s), s, yaw);
                        RowingPoseSolver.Result r = solver.Solve(world, in t);
                        Assert.That(r.Solved, Is.True);

                        float missL = (P(world, solver.PalmLeft) - t.GripLeft).magnitude;
                        float missR = (P(world, solver.PalmRight) - t.GripRight).magnitude;
                        if (missL > HalfPixelMeters || missR > HalfPixelMeters)
                            failures.AppendLine($"{skin.Id} facing {yaw} phase {s:0.000}: palm miss L {missL:0.0000} R {missR:0.0000} m (lean {r.LeanDegrees:0.0})");
                    }
                }
            }
            Assert.That(skins, Is.GreaterThan(0), "no committed skin carries row_oars");
            Assert.That(failures.Length, Is.EqualTo(0), failures.ToString());
        }

        [Test]
        public void TheArmIsNeverStretched_AndEveryLimbKeepsItsLength()
        {
            BoatVisualDef dory = Dory();
            var failures = new StringBuilder();
            foreach (CharacterSkinDef skin in RowingSkins())
            {
                var solver = new RowingPoseSolver(skin.Bones);
                int hipL = skin.IndexOfBone(RowingPoseSolver.HipLId), kneeL = skin.IndexOfBone(RowingPoseSolver.KneeLId);
                var world = new Matrix4x4[skin.Bones.Length];
                foreach (float yaw in Facings)
                {
                    for (int i = 0; i < Phases; i++)
                    {
                        float s = (float)i / Phases;
                        Compose(skin, s, yaw, world);
                        float upperL = (P(world, solver.ElbowLeft) - P(world, solver.ShoulderLeft)).magnitude;
                        float lowerL = (P(world, solver.PalmLeft) - P(world, solver.ElbowLeft)).magnitude;
                        float upperR = (P(world, solver.ElbowRight) - P(world, solver.ShoulderRight)).magnitude;
                        float lowerR = (P(world, solver.PalmRight) - P(world, solver.ElbowRight)).magnitude;
                        float thigh = (P(world, kneeL) - P(world, hipL)).magnitude;
                        float shin = (P(world, solver.FootLeft) - P(world, kneeL)).magnitude;

                        RowingPoseSolver.Targets t = FigureTargets(StationAt(dory, s), s, yaw);
                        solver.Solve(world, in t);

                        float[] before = { upperL, lowerL, upperR, lowerR, thigh, shin };
                        float[] after =
                        {
                            (P(world, solver.ElbowLeft) - P(world, solver.ShoulderLeft)).magnitude,
                            (P(world, solver.PalmLeft) - P(world, solver.ElbowLeft)).magnitude,
                            (P(world, solver.ElbowRight) - P(world, solver.ShoulderRight)).magnitude,
                            (P(world, solver.PalmRight) - P(world, solver.ElbowRight)).magnitude,
                            (P(world, kneeL) - P(world, hipL)).magnitude,
                            (P(world, solver.FootLeft) - P(world, kneeL)).magnitude,
                        };
                        for (int k = 0; k < before.Length; k++)
                            if (Mathf.Abs(after[k] - before[k]) > Rigid)
                                failures.AppendLine($"{skin.Id} facing {yaw} phase {s:0.000}: segment {k} {before[k]:0.0000} -> {after[k]:0.0000}");

                        float spanL = (P(world, solver.PalmLeft) - P(world, solver.ShoulderLeft)).magnitude;
                        float spanR = (P(world, solver.PalmRight) - P(world, solver.ShoulderRight)).magnitude;
                        if (spanL > upperL + lowerL + Rigid || spanR > upperR + lowerR + Rigid)
                            failures.AppendLine($"{skin.Id} facing {yaw} phase {s:0.000}: arm past its reach");
                    }
                }
            }
            Assert.That(failures.Length, Is.EqualTo(0), failures.ToString());
        }

        // ---------------------------------------------------------------- the seat and the feet

        [Test]
        public void HeSitsOnTheThwart_WithHisFeetBraced()
        {
            BoatVisualDef dory = Dory();
            var failures = new StringBuilder();
            foreach (CharacterSkinDef skin in RowingSkins())
            {
                var solver = new RowingPoseSolver(skin.Bones);
                var world = new Matrix4x4[skin.Bones.Length];
                for (int i = 0; i < Phases; i++)
                {
                    float s = (float)i / Phases;
                    Compose(skin, s, 180f, world);
                    float ankleL = world[solver.FootLeft].m23, ankleR = world[solver.FootRight].m23;
                    RowingPoseSolver.Targets t = FigureTargets(StationAt(dory, s), s, 180f);
                    solver.Solve(world, in t);

                    if ((P(world, solver.PelvisBone) - t.Pelvis).magnitude > Rigid)
                        failures.AppendLine($"{skin.Id} phase {s:0.000}: pelvis off the seat");
                    // The seat is the thwart top in the hull frame; the pelvis rides the rig's 0.10 above it.
                    float seatZ = dory.Rowing.SeatLocalMeters.z - Root.z;
                    if (Mathf.Abs(P(world, solver.PelvisBone).z - (seatZ + RowingPoseSolver.PelvisAboveSeatMeters)) > Rigid)
                        failures.AppendLine($"{skin.Id} phase {s:0.000}: pelvis height wrong");
                    float footL = (P(world, solver.FootLeft) - (t.BraceLeft + new Vector3(0f, 0f, ankleL))).magnitude;
                    float footR = (P(world, solver.FootRight) - (t.BraceRight + new Vector3(0f, 0f, ankleR))).magnitude;
                    // A short-legged figure (the children) cannot reach the adult's brace: the leg then
                    // reaches TOWARD it, straight, and is never stretched, like an arm short of a grip.
                    bool reachedL = footL <= HalfPixelMeters || LegStraight(skin, world, "hip_L", "knee_L", solver.FootLeft);
                    bool reachedR = footR <= HalfPixelMeters || LegStraight(skin, world, "hip_R", "knee_R", solver.FootRight);
                    if (!reachedL || !reachedR)
                        failures.AppendLine($"{skin.Id} phase {s:0.000}: foot off its brace L {footL:0.0000} R {footR:0.0000}, leg not straight toward it");
                }
            }
            Assert.That(failures.Length, Is.EqualTo(0), failures.ToString());
        }

        // ---------------------------------------------------------------- port and starboard

        [Test]
        public void PortAndStarboard_Mirror()
        {
            BoatVisualDef dory = Dory();
            Vector3 portLock = dory.OarPortMesh.PivotLocalMeters, starLock = dory.OarStarMesh.PivotLocalMeters;
            Assert.That(portLock, Is.EqualTo(RowingStationMath.MirrorAcrossCentreline(starLock)).Using(Vector3EqualityComparer(1e-5f)),
                        "the dory's oarlocks are a mirror pair");

            var skin = AssetDatabase.LoadAssetAtPath<CharacterSkinDef>(SkinFolder + "/skipper.asset");
            var solver = new RowingPoseSolver(skin.Bones);
            var world = new Matrix4x4[skin.Bones.Length];
            for (int i = 0; i < Phases; i++)
            {
                float s = (float)i / Phases;
                RowingTargets hull = StationAt(dory, s);
                Vector3 mirrored = RowingStationMath.MirrorAcrossCentreline(hull.StarboardGrip);
                Assert.That((hull.PortGrip - mirrored).magnitude, Is.LessThan(1e-5f), $"phase {s}: grips mirror");
                Assert.That((hull.PortBrace - RowingStationMath.MirrorAcrossCentreline(hull.StarboardBrace)).magnitude,
                            Is.LessThan(1e-6f));

                // Facing aft his right hand is on the PORT oar.
                RowingPoseSolver.Targets t = FigureTargets(hull, s, 180f);
                Vector3 portInFigure = Quaternion.Inverse(Quaternion.AngleAxis(180f, Vector3.forward)) * (hull.PortGrip - Root);
                Assert.That((t.GripRight - portInFigure).magnitude, Is.LessThan(1e-5f), "facing aft, the right hand rows port");

                Compose(skin, s, 180f, world);
                solver.Solve(world, in t);
                Vector3 l = P(world, solver.PalmLeft), r = P(world, solver.PalmRight);
                Assert.That(Mathf.Abs(l.x + r.x), Is.LessThan(2f * HalfPixelMeters), $"phase {s}: palms mirror across him (x)");
                Assert.That(Mathf.Abs(l.y - r.y), Is.LessThan(2f * HalfPixelMeters), $"phase {s}: palms level fore and aft");
                Assert.That(Mathf.Abs(l.z - r.z), Is.LessThan(2f * HalfPixelMeters), $"phase {s}: palms level in height");
            }
        }

        private static IEqualityComparer<Vector3> Vector3EqualityComparer(float eps) => new Vec3Eq(eps);

        private sealed class Vec3Eq : IEqualityComparer<Vector3>
        {
            private readonly float _eps;
            public Vec3Eq(float eps) { _eps = eps; }
            public bool Equals(Vector3 a, Vector3 b) => (a - b).magnitude <= _eps;
            public int GetHashCode(Vector3 v) => 0;
        }

        // ---------------------------------------------------------------- letting go, determinism, cost

        [Test]
        public void AnOarNotRowed_IsLetGo_AndTheOtherHandStillHolds()
        {
            BoatVisualDef dory = Dory();
            var skin = AssetDatabase.LoadAssetAtPath<CharacterSkinDef>(SkinFolder + "/skipper.asset");
            var solver = new RowingPoseSolver(skin.Bones);
            var world = new Matrix4x4[skin.Bones.Length];
            const float s = 0.25f;
            RowingTargets hull = StationAt(dory, s, portRowing: true, starRowing: false);
            RowingPoseSolver.Targets t = FigureTargets(hull, s, 180f);
            Assert.That(t.HasGripRight, Is.True, "port is rowed: facing aft that is his right hand");
            Assert.That(t.HasGripLeft, Is.False);
            Compose(skin, s, 180f, world);
            RowingPoseSolver.Result r = solver.Solve(world, in t);
            Assert.That(r.ReachUseLeft, Is.EqualTo(0f), "the free hand is not solved");
            Assert.That((P(world, solver.PalmRight) - t.GripRight).magnitude, Is.LessThan(HalfPixelMeters));
        }

        [Test]
        public void TheSolve_IsDeterministic()
        {
            BoatVisualDef dory = Dory();
            var skin = AssetDatabase.LoadAssetAtPath<CharacterSkinDef>(SkinFolder + "/skipper.asset");
            var solver = new RowingPoseSolver(skin.Bones);
            var a = new Matrix4x4[skin.Bones.Length];
            var b = new Matrix4x4[skin.Bones.Length];
            for (int i = 0; i < Phases; i++)
            {
                float s = (float)i / Phases;
                RowingPoseSolver.Targets t = FigureTargets(StationAt(dory, s), s, 180f);
                Compose(skin, s, 180f, a);
                solver.Solve(a, in t);
                Compose(skin, s, 180f, b);
                new RowingPoseSolver(skin.Bones).Solve(b, in t);
                for (int k = 0; k < a.Length; k++)
                    Assert.That(a[k], Is.EqualTo(b[k]), $"phase {s} bone {k}: the same inputs give the same pose");
            }
        }

        [Test]
        public void TheSolve_DoesNotAllocate()
        {
            BoatVisualDef dory = Dory();
            var skin = AssetDatabase.LoadAssetAtPath<CharacterSkinDef>(SkinFolder + "/skipper.asset");
            var solver = new RowingPoseSolver(skin.Bones);
            var world = new Matrix4x4[skin.Bones.Length];
            RowingPoseSolver.Targets t = FigureTargets(StationAt(dory, 0f), 0f, 180f);
            Compose(skin, 0f, 180f, world);
            solver.Solve(world, in t);   // warm
            Compose(skin, 0f, 180f, world);
            Assert.That(() => { solver.Solve(world, in t); }, Is.Not.AllocatingGCMemory());
        }

        // ---------------------------------------------------------------- the rig's drive and the data

        [Test]
        public void RigDrive_IsOutAtZero_InAtTheHandsInShare_AndContinuous()
        {
            Assert.That(RowingFigure.RigDrive(0f), Is.EqualTo(0f).Within(1e-6f));
            Assert.That(RowingFigure.RigDrive(RowingFigure.RigHandsInShare), Is.EqualTo(1f).Within(1e-6f));
            Assert.That(RowingFigure.RigDrive(0.999999f), Is.EqualTo(0f).Within(1e-4f));
            float prev = RowingFigure.RigDrive(0f);
            for (int i = 1; i <= 1000; i++)
            {
                float d = RowingFigure.RigDrive(i / 1000f);
                Assert.That(Mathf.Abs(d - prev), Is.LessThan(0.01f), $"jump at {i / 1000f}");
                prev = d;
            }
        }

        [Test]
        public void TheDorysStation_IsData_AndHoldsTheLoomInboard()
        {
            BoatVisualDef dory = Dory();
            Assert.That(dory.Rowing.Enabled, Is.True, "the dory's rower is solved onto her oars");
            // The rig's inboard loom (doryIsoRig.js O_LIN) is 0.85 m: the grip must be on it.
            Assert.That(dory.Rowing.GripInboardMeters, Is.GreaterThan(0f).And.LessThan(0.85f));
            Assert.That(dory.Rowing.SeatLocalMeters.z, Is.GreaterThan(dory.Rowing.FootBraceLocalMeters.z),
                        "he sits above where his feet rest");
        }
    }
}
