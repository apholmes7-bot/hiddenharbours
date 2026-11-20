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
            // The rig's inboard loom (doryIsoRig.js O_LIN) is 0.65 m: the station must say so, and the grip must be on it.
            Assert.That(dory.Rowing.LoomInboardMeters, Is.EqualTo(RigLoomInboardMeters).Within(Rigid),
                        "the station's loom is the rig's O_LIN");
            Assert.That(dory.Rowing.GripInboardMeters, Is.GreaterThan(0f).And.LessThan(RigLoomInboardMeters));
            Assert.That(dory.Rowing.SeatLocalMeters.z, Is.GreaterThan(dory.Rowing.FootBraceLocalMeters.z),
                        "he sits above where his feet rest");
        }

        // ---------------------------------------------------------------- clear of himself

        /// <summary>The rig's inboard loom, oarlock to handle end (doryIsoRig.js O_LIN).</summary>
        private const float RigLoomInboardMeters = 0.65f;

        /// <summary>The closest approach of two segments (sampled along the first, exact onto the second).</summary>
        private static float SegmentToSegment(Vector3 a0, Vector3 a1, Vector3 b0, Vector3 b1)
        {
            float d = float.MaxValue;
            for (int q = 0; q <= 64; q++) d = Mathf.Min(d, SegmentDistance(Vector3.Lerp(a0, a1, q / 64f), b0, b1));
            return d;
        }

        [Test]
        public void TheTwoLooms_NeverMeet_TheHandlesStayApartThroughTheWholeStroke()
        {
            // The owner: "fix the overlapping oars". At the finish the two looms crossed in front of him
            // and drew as one shaft: the inboard (0.85) was longer than the tholes' half-beam. With the
            // rig's 0.65 m loom the two looms, wood to wood, keep the station's air at every phase, and
            // the handle ends never reach the centreline.
            BoatVisualDef dory = Dory();
            RowingStationConfig st = dory.Rowing;
            Assert.That(st.LoomClearanceMeters, Is.GreaterThan(0f), "the loom clearance is data");
            Assert.That(st.LoomHalfThicknessMeters, Is.GreaterThan(0f), "the loom thickness is data");
            Vector3 portLock = dory.OarPortMesh.PivotLocalMeters, starLock = dory.OarStarMesh.PivotLocalMeters;
            var failures = new StringBuilder();
            float worst = float.MaxValue;
            const int Samples = 128;
            for (int i = 0; i < Samples; i++)
            {
                float s = (float)i / Samples;
                RowingTargets t = StationAt(dory, s);
                float air = SegmentToSegment(portLock, t.PortHandleEnd, starLock, t.StarboardHandleEnd) - 2f * st.LoomHalfThicknessMeters;
                worst = Mathf.Min(worst, air);
                if (air < st.LoomClearanceMeters)
                    failures.AppendLine($"phase {s:0.000}: looms {air:0.000} m apart, wood to wood (< {st.LoomClearanceMeters})");
                if (t.PortHandleEnd.x + st.LoomHalfThicknessMeters >= 0f || t.StarboardHandleEnd.x - st.LoomHalfThicknessMeters <= 0f)
                    failures.AppendLine($"phase {s:0.000}: a handle end reaches the centreline (port {t.PortHandleEnd.x:0.000}, star {t.StarboardHandleEnd.x:0.000})");
            }
            Assert.That(failures.Length, Is.EqualTo(0), failures.ToString());
            Assert.That(worst, Is.LessThan(1f), "the looms were measured");
        }

        [Test]
        public void HisHands_RideAboveHisKnees_ThroughTheRecovery_ByTheStationsMargin()
        {
            // The owner: "fix the low hands". On the recovery the handles must travel clearly over his
            // thighs, not down in his lap: every palm rides at least the station's margin above the top
            // of either knee (the thigh's highest point), at every recovery phase, facing and skin.
            BoatVisualDef dory = Dory();
            float margin = dory.Rowing.HandAboveKneeMeters;
            Assert.That(margin, Is.GreaterThan(0f), "the hand height over the knees is data");
            var failures = new StringBuilder();
            int skins = 0, checks = 0;
            foreach (CharacterSkinDef skin in RowingSkins())
            {
                skins++;
                var solver = new RowingPoseSolver(skin.Bones, RowingBodyVolume.Of(skin));
                int kneeL = skin.IndexOfBone(RowingPoseSolver.KneeLId), kneeR = skin.IndexOfBone(RowingPoseSolver.KneeRId);
                var world = new Matrix4x4[skin.Bones.Length];
                foreach (float yaw in Facings)
                {
                    // the recovery: the second half of the stroke, finish to catch
                    for (int i = Phases / 2; i < Phases; i++)
                    {
                        float s = (float)i / Phases;
                        Compose(skin, s, yaw, world);
                        RowingPoseSolver.Targets t = FigureTargets(StationAt(dory, s), s, yaw);
                        solver.Solve(world, in t);
                        float palmZ = Mathf.Min(P(world, solver.PalmLeft).z, P(world, solver.PalmRight).z);
                        float kneeZ = Mathf.Max(P(world, kneeL).z, P(world, kneeR).z);
                        checks++;
                        if (palmZ - kneeZ < margin)
                            failures.AppendLine($"{skin.Id} facing {yaw} phase {s:0.000}: palms {palmZ - kneeZ:0.000} m over the knees (< {margin})");
                    }
                }
            }
            Assert.That(skins, Is.GreaterThan(0), "no committed skin carries row_oars");
            Assert.That(checks, Is.GreaterThan(0));
            Assert.That(failures.Length, Is.EqualTo(0), failures.ToString());
        }

        /// <summary>A millimetre of numerical slack on a clearance the solve bisects toward.</summary>
        private const float ClearSlack = 1e-3f;

        private static float SegmentDistance(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / Mathf.Max(1e-9f, ab.sqrMagnitude));
            return (p - (a + t * ab)).magnitude;
        }

        [Test]
        public void TheLoomPassesOverHisKnees_ByTheStationsMargin_AndHisHandsRideAboveThem()
        {
            // The owner saw the near oar's shaft drawn across his knees and his hands in his lap on the
            // recovery. The loom, lock to handle end, must clear each knee centre by the station's data
            // margin, and the palms must ride above the knees, through the whole stroke, for every skin.
            BoatVisualDef dory = Dory();
            float margin = dory.Rowing.KneeClearanceMeters;
            Assert.That(margin, Is.GreaterThan(0f), "the dory's knee clearance is data");
            Vector3 portLock = dory.OarPortMesh.PivotLocalMeters, starLock = dory.OarStarMesh.PivotLocalMeters;
            var failures = new StringBuilder();
            int skins = 0;
            foreach (CharacterSkinDef skin in RowingSkins())
            {
                skins++;
                var solver = new RowingPoseSolver(skin.Bones, RowingBodyVolume.Of(skin));
                int kneeL = skin.IndexOfBone(RowingPoseSolver.KneeLId), kneeR = skin.IndexOfBone(RowingPoseSolver.KneeRId);
                var world = new Matrix4x4[skin.Bones.Length];
                foreach (float yaw in Facings)
                {
                    Quaternion inv = Quaternion.Inverse(Quaternion.AngleAxis(yaw, Vector3.forward));
                    for (int i = 0; i < Phases; i++)
                    {
                        float s = (float)i / Phases;
                        DoryOarMeshPose.Pose pose = DoryOarMeshPose.Row(s + DoryOarMeshPose.RowCatchTurns);
                        Compose(skin, s, yaw, world);
                        RowingPoseSolver.Targets t = FigureTargets(StationAt(dory, s), s, yaw);
                        solver.Solve(world, in t);

                        Vector3 pDir = DoryOarMeshPose.Direction(DoryOarMeshLayer.PortSide, pose.SweepDegrees, pose.DipDegrees);
                        Vector3 sDir = DoryOarMeshPose.Direction(DoryOarMeshLayer.StarboardSide, pose.SweepDegrees, pose.DipDegrees);
                        Vector3 pA = inv * (portLock - Root), pB = inv * (portLock - RigLoomInboardMeters * pDir - Root);
                        Vector3 sA = inv * (starLock - Root), sB = inv * (starLock - RigLoomInboardMeters * sDir - Root);
                        float palmZ = Mathf.Min(P(world, solver.PalmLeft).z, P(world, solver.PalmRight).z);
                        foreach (int knee in new[] { kneeL, kneeR })
                        {
                            Vector3 k = P(world, knee);
                            float d = Mathf.Min(SegmentDistance(k, pA, pB), SegmentDistance(k, sA, sB));
                            if (d < margin)
                                failures.AppendLine($"{skin.Id} facing {yaw} phase {s:0.000}: loom {d:0.000} m from a knee (< {margin})");
                            if (palmZ <= k.z)
                                failures.AppendLine($"{skin.Id} facing {yaw} phase {s:0.000}: palms {palmZ - k.z:0.000} m below a knee (in his lap)");
                        }
                    }
                }
            }
            Assert.That(skins, Is.GreaterThan(0), "no committed skin carries row_oars");
            Assert.That(failures.Length, Is.EqualTo(0), failures.ToString());
        }

        [Test]
        public void EveryRowingSkin_HandsAndForearms_StayOutsideHisTorso_ByTheMargin_HandsStillOnTheGrips()
        {
            // The owner: "his arms transfer through his body". With the skin's own torso the solve keeps
            // the palm, the forearm and the elbow end of the upper arm outside it by the station's margin
            // (elbows back and out at the finish), and the hands still hold the handles.
            BoatVisualDef dory = Dory();
            float margin = dory.Rowing.BodyClearanceMeters;
            Assert.That(margin, Is.GreaterThan(0f), "the dory's body clearance is data");
            var failures = new StringBuilder();
            int skins = 0;
            foreach (CharacterSkinDef skin in RowingSkins())
            {
                skins++;
                RowingBodyVolume body = RowingBodyVolume.Of(skin);
                Assert.That(body, Is.Not.Null, $"{skin.Id}: the bind mesh must be readable to measure the torso");
                Assert.That(body.IsUsable, Is.True, $"{skin.Id}: a trunk volume");
                var solver = new RowingPoseSolver(skin.Bones, body);
                var world = new Matrix4x4[skin.Bones.Length];
                foreach (float yaw in Facings)
                {
                    for (int i = 0; i < Phases; i++)
                    {
                        float s = (float)i / Phases;
                        Compose(skin, s, yaw, world);
                        RowingPoseSolver.Targets t = FigureTargets(StationAt(dory, s), s, yaw);
                        RowingPoseSolver.Result r = solver.Solve(world, in t);
                        float fL = solver.ForearmClearance(world, true), fR = solver.ForearmClearance(world, false);
                        float uL = solver.UpperArmClearance(world, true), uR = solver.UpperArmClearance(world, false);
                        float ends = solver.HandleEndClearance(world, in t);
                        if (fL < margin - ClearSlack || fR < margin - ClearSlack || uL < -ClearSlack || uR < -ClearSlack || ends < margin - ClearSlack)
                            failures.AppendLine($"{skin.Id} facing {yaw} phase {s:0.000}: hand/forearm L {fL:0.000} R {fR:0.000}, elbow L {uL:0.000} R {uR:0.000}, " +
                                                $"handle ends {ends:0.000} m (lean back {r.ClearLeanDegrees:0.0}, turn {r.ClearTwistDegrees:0.0}, " +
                                                $"elbows out {r.ElbowOutLeft:0.00}/{r.ElbowOutRight:0.00})");
                        float missL = (P(world, solver.PalmLeft) - t.GripLeft).magnitude;
                        float missR = (P(world, solver.PalmRight) - t.GripRight).magnitude;
                        if (missL > HalfPixelMeters || missR > HalfPixelMeters)
                            failures.AppendLine($"{skin.Id} facing {yaw} phase {s:0.000}: palm miss L {missL:0.0000} R {missR:0.0000} m");
                    }
                }
            }
            Assert.That(skins, Is.GreaterThan(0));
            Assert.That(failures.Length, Is.EqualTo(0), failures.ToString());
        }

        [Test]
        public void TheTorsoVolume_ExcludesASkirt_AndItsInsideReadsNegative()
        {
            var skin = AssetDatabase.LoadAssetAtPath<CharacterSkinDef>(SkinFolder + "/skipper.asset");
            RowingBodyVolume body = RowingBodyVolume.Of(skin);
            Assert.That(body.IsUsable, Is.True);
            var world = new Matrix4x4[skin.Bones.Length];
            skin.TryGetClip(RowingFigure.RowStrokeKey, out var clip);
            CharacterSkinPose.ComposeWorld(clip, 0, skin.Bones, world);
            int chestBone = skin.IndexOfBone(RowingBodyVolume.ChestId);
            Assert.That(body.TryGetBox(2, out Vector3 cc, out _), Is.True, "the chest carries a volume");
            Vector3 chest = world[chestBone].MultiplyPoint3x4(cc);
            Assert.That(body.Outside(world, chest), Is.LessThan(0f), "the middle of the chest is inside the trunk");
            Assert.That(body.Outside(world, chest + new Vector3(1f, 0f, 0f)), Is.GreaterThan(0.5f), "a metre to the side is outside");
            Assert.That(body.TryGetBox(0, out Vector3 c, out Vector3 e), Is.True, "the pelvis carries a volume");
            Assert.That(c.z - e.z, Is.GreaterThanOrEqualTo(
                (P(world, skin.IndexOfBone(RowingPoseSolver.HipLId)) - P(world, skin.IndexOfBone(RowingPoseSolver.PelvisId))).z - Rigid),
                "the pelvis volume stops at the hips (no skirt)");
        }

        [Test]
        public void OarsLetGo_HeRestsHisHandsOnHisThighs_Slumped_AndBreathes()
        {
            BoatVisualDef dory = Dory();
            RowingStationConfig st = dory.Rowing;
            var failures = new StringBuilder();
            foreach (CharacterSkinDef skin in RowingSkins())
            {
                var solver = new RowingPoseSolver(skin.Bones, RowingBodyVolume.Of(skin));
                int hipL = skin.IndexOfBone(RowingPoseSolver.HipLId), kneeL = skin.IndexOfBone(RowingPoseSolver.KneeLId);
                int hipR = skin.IndexOfBone(RowingPoseSolver.HipRId), kneeR = skin.IndexOfBone(RowingPoseSolver.KneeRId);
                int head = skin.IndexOfBone(RowingBodyVolume.ChestId);
                var world = new Matrix4x4[skin.Bones.Length];
                foreach (float yaw in Facings)
                {
                    float chestIn = 0f, chestOut = 0f;
                    foreach (float breath in new[] { -1f, 1f })
                    {
                        RowingTargets hull = StationAt(dory, 0.5f, portRowing: false, starRowing: false);
                        hull.RestWeight = 1f;
                        hull.RestBreath = breath;
                        RowingPoseSolver.Targets t = RowingPoseSolver.ToFigure(hull, Root, yaw, 0f);
                        Assert.That(t.ThighRest, Is.True);
                        Assert.That(t.RestLeanDegrees, Is.EqualTo(breath * st.RestBreathDegrees - st.RestSlumpDegrees).Within(1e-4f));
                        Compose(skin, 0f, yaw, world);
                        solver.Solve(world, in t);

                        Vector3 wantL = Vector3.Lerp(P(world, hipL), P(world, kneeL), st.RestHandAlongThigh) + new Vector3(0f, 0f, st.RestHandAboveThighMeters);
                        Vector3 wantR = Vector3.Lerp(P(world, hipR), P(world, kneeR), st.RestHandAlongThigh) + new Vector3(0f, 0f, st.RestHandAboveThighMeters);
                        float missL = (P(world, solver.PalmLeft) - wantL).magnitude, missR = (P(world, solver.PalmRight) - wantR).magnitude;
                        if (missL > HalfPixelMeters || missR > HalfPixelMeters)
                            failures.AppendLine($"{skin.Id} facing {yaw} breath {breath}: hands off the thighs L {missL:0.000} R {missR:0.000}");
                        float fL = solver.ForearmClearance(world, true), fR = solver.ForearmClearance(world, false);
                        float uL = solver.UpperArmClearance(world, true), uR = solver.UpperArmClearance(world, false);
                        if (fL < st.BodyClearanceMeters - ClearSlack || fR < st.BodyClearanceMeters - ClearSlack || uL < -ClearSlack || uR < -ClearSlack)
                            failures.AppendLine($"{skin.Id} facing {yaw} breath {breath}: resting arm in the torso: hand/forearm L {fL:0.000} R {fR:0.000}, elbow L {uL:0.000} R {uR:0.000}");
                        if (breath < 0f) chestIn = P(world, head).y; else chestOut = P(world, head).y;
                    }
                    if (Mathf.Abs(chestOut - chestIn) < 1e-3f)
                        failures.AppendLine($"{skin.Id} facing {yaw}: the breath does not move him (a held pose)");
                }
            }
            Assert.That(failures.Length, Is.EqualTo(0), failures.ToString());
        }

        [Test]
        public void ResterBlades_SettleToTheRestDip_ByTheRestWeight()
        {
            BoatVisualDef dory = Dory();
            var stroke = DoryOarMeshPose.Row(0.3f);
            Assert.That(DoryOarMeshLayer.Rested(stroke, 0f, dory.Rowing.RestBladeDipDegrees).DipDegrees, Is.EqualTo(stroke.DipDegrees));
            var full = DoryOarMeshLayer.Rested(stroke, 1f, dory.Rowing.RestBladeDipDegrees);
            Assert.That(full.DipDegrees, Is.EqualTo(dory.Rowing.RestBladeDipDegrees).Within(1e-5f));
            Assert.That(full.SweepDegrees, Is.EqualTo(stroke.SweepDegrees), "the rest keeps the stroke's sweep");
            var half = DoryOarMeshLayer.Rested(stroke, 0.5f, dory.Rowing.RestBladeDipDegrees);
            Assert.That(half.DipDegrees, Is.EqualTo(0.5f * (stroke.DipDegrees + dory.Rowing.RestBladeDipDegrees)).Within(1e-4f));
        }

        [Test]
        public void TheCasualStyle_RestsWithAWeightThatEasesInAndOut_AndBreathesOncePerRest()
        {
            // The casual style's shape (Data/Boats rowing_style casual: rest 0.35 at 0.75, ease 0.6).
            RowingStyle casual = RowingStyle.Build(0.5f, RowingStyle.CatchSpan, RowingStyle.PullSpan, RowingStyle.ReleaseSpan,
                                                   RowingStyle.RecoverySpan, 0.35f, 0.75f, 0.6f, 0.6f, 0f, 1f);
            Assert.That(casual.RestShare, Is.GreaterThan(0f));
            float prev = casual.RestWeight(0f);
            float peak = 0f, minBreath = 0f, maxBreath = 0f;
            for (int i = 1; i <= 2000; i++)
            {
                float s = i / 2000f;
                float w = casual.RestWeight(s);
                Assert.That(w, Is.InRange(0f, 1f));
                Assert.That(Mathf.Abs(w - prev), Is.LessThan(0.02f), $"rest weight jumps at {s}");
                prev = w;
                peak = Mathf.Max(peak, w);
                float b = casual.RestBreath(s);
                minBreath = Mathf.Min(minBreath, b);
                maxBreath = Mathf.Max(maxBreath, b);
            }
            Assert.That(peak, Is.EqualTo(1f).Within(1e-4f), "a full rest");
            Assert.That(maxBreath, Is.GreaterThan(0.9f));
            Assert.That(minBreath, Is.LessThan(-0.9f));
            Assert.That(RowingStyle.Unstyled.RestWeight(0.8f), Is.EqualTo(0f), "a stroke with no rest never rests");
        }

        [Test]
        public void LetGoBreath_IsASlowSine_OfThePresentationClock()
        {
            Assert.That(RowingFigureReader.LetGoBreath(0d, 4f), Is.EqualTo(0f).Within(1e-5f));
            Assert.That(RowingFigureReader.LetGoBreath(1d, 4f), Is.EqualTo(1f).Within(1e-4f));
            Assert.That(RowingFigureReader.LetGoBreath(3d, 4f), Is.EqualTo(-1f).Within(1e-4f));
            Assert.That(RowingFigureReader.LetGoBreath(1d + 4e6, 4f), Is.EqualTo(1f).Within(1e-3f), "stable after days of play");
            Assert.That(RowingFigureReader.LetGoBreath(5d, 0f), Is.EqualTo(0f));
        }

        [Test]
        public void TheSolveWithHisTorso_IsDeterministic_AndDoesNotAllocate()
        {
            BoatVisualDef dory = Dory();
            var skin = AssetDatabase.LoadAssetAtPath<CharacterSkinDef>(SkinFolder + "/skipper.asset");
            RowingBodyVolume body = RowingBodyVolume.Of(skin);
            var solver = new RowingPoseSolver(skin.Bones, body);
            var a = new Matrix4x4[skin.Bones.Length];
            var b = new Matrix4x4[skin.Bones.Length];
            for (int i = 0; i < Phases; i++)
            {
                float s = (float)i / Phases;
                RowingPoseSolver.Targets t = FigureTargets(StationAt(dory, s), s, 180f);
                Compose(skin, s, 180f, a);
                solver.Solve(a, in t);
                Compose(skin, s, 180f, b);
                new RowingPoseSolver(skin.Bones, RowingBodyVolume.Of(skin)).Solve(b, in t);
                for (int k = 0; k < a.Length; k++)
                    Assert.That(a[k], Is.EqualTo(b[k]), $"phase {s} bone {k}");
            }
            // The finish, where the clear step works hardest.
            const float finish = 0.5f;
            RowingPoseSolver.Targets tf = FigureTargets(StationAt(dory, finish), finish, 180f);
            Compose(skin, finish, 180f, a);
            solver.Solve(a, in tf);
            Compose(skin, finish, 180f, a);
            Assert.That(() => { solver.Solve(a, in tf); }, Is.Not.AllocatingGCMemory());
        }

        [Test]
        public void TheNpcFigurePath_IsSolvedOntoTheStation_LikeThePlayers()
        {
            // Computer-run rowers draw through CharacterFigurePresenter; the player through
            // DeckRiderMeshPresenter. Both must hand the station's targets to the figure's solve, or the
            // NPC holds the air beside the handles again. A text guard on the one call that does it.
            string npc = File.ReadAllText("Assets/_Project/Code/Art/CharacterFigurePresenter.cs");
            string player = File.ReadAllText("Assets/_Project/Code/Player/DeckRiderMeshPresenter.cs");
            foreach (var (name, text) in new[] { ("CharacterFigurePresenter", npc), ("DeckRiderMeshPresenter", player) })
            {
                Assert.That(text, Does.Contain("_rowing.TryTargets("), $"{name} reads the rowing station");
                Assert.That(text, Does.Contain("life.Rowing = true"), $"{name} hands the targets to the solve");
                Assert.That(text, Does.Contain("life.Row = row"), $"{name} hands the targets to the solve");
            }
            string renderer = File.ReadAllText("Assets/_Project/Code/Art/IsoCharacterFigureRenderer.cs");
            Assert.That(renderer, Does.Contain("RowingBodyVolume.Of(def)"), "every figure's solve knows its own torso");
        }
    }
}
