using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Taking and leaving a helm as a short move (<see cref="HelmTransition"/>, <see cref="FigurePoseBlend"/>,
    /// <see cref="HelmTransitionSettings"/>): the eased weights, the snap cases, the root's way there, an
    /// interruption, and the blend itself, first on a toy chain and then on every committed skin going
    /// from standing on the dory's deck to seated at her tiller with his hand on the grip, and back, at
    /// 60 frames a second. Pure arithmetic on committed assets; no scene, no clock.
    /// </summary>
    public class HelmTransitionTests
    {
        private const string SkinFolder = "Assets/_Project/Data/Characters/Skin";
        private const string DoryVisual = "Assets/_Project/Data/Boats/Visuals/DoryIso.asset";
        private const float Frame = 1f / 60f;
        private const float Rigid = 1e-4f;

        /// <summary>The standing clip he is drawn in on deck before the hop.</summary>
        private const string StandingState = "idle";

        /// <summary>Where he stands on the dory's deck when he presses for the tiller: forward of the
        /// stern bench and a little to port, in reach of the helm.</summary>
        private static readonly Vector3 DeckStand = new Vector3(-0.25f, -0.9f, 0.15f);

        private static HelmTransitionSettings Settings => HelmTransitionSettings.Default;

        // ---------------------------------------------------------------- the weights

        [Test]
        public void Ease_StartsAndEndsAtRest()
        {
            Assert.That(HelmTransition.Ease(0f), Is.EqualTo(0f));
            Assert.That(HelmTransition.Ease(1f), Is.EqualTo(1f));
            Assert.That(HelmTransition.Ease(-3f), Is.EqualTo(0f), "clamped");
            Assert.That(HelmTransition.Ease(4f), Is.EqualTo(1f), "clamped");
            const float h = 1e-3f;
            Assert.That(HelmTransition.Ease(h) / h, Is.LessThan(0.01f), "no speed leaving");
            Assert.That((1f - HelmTransition.Ease(1f - h)) / h, Is.LessThan(0.01f), "no speed arriving");
            float last = 0f;
            for (int i = 1; i <= 100; i++)
            {
                float e = HelmTransition.Ease(i / 100f);
                Assert.That(e, Is.GreaterThanOrEqualTo(last), "monotonic");
                last = e;
            }
        }

        [Test]
        public void Taking_StepsToTheStationFirst_ThenSettles()
        {
            var s = Settings;
            HelmTransition.Phase(true, 0f, s.RootShare, s.BodyStart, out float r0, out float b0);
            Assert.That(r0, Is.EqualTo(0f)); Assert.That(b0, Is.EqualTo(0f));
            HelmTransition.Phase(true, 1f, s.RootShare, s.BodyStart, out float r1, out float b1);
            Assert.That(r1, Is.EqualTo(1f)); Assert.That(b1, Is.EqualTo(1f));
            HelmTransition.Phase(true, s.RootShare, s.RootShare, s.BodyStart, out float rAt, out _);
            Assert.That(rAt, Is.EqualTo(1f), "at the station by the root's share");
            HelmTransition.Phase(true, s.BodyStart * 0.99f, s.RootShare, s.BodyStart, out _, out float bEarly);
            Assert.That(bEarly, Is.EqualTo(0f), "still standing before the body's start");
            HelmTransition.Phase(true, 0.5f, s.RootShare, s.BodyStart, out float rMid, out float bMid);
            Assert.That(rMid, Is.GreaterThan(bMid), "the walk up leads the sit");
        }

        [Test]
        public void Leaving_StandsUpFirst_ThenStepsOff()
        {
            var s = Settings;
            HelmTransition.Phase(false, 0f, s.RootShare, s.BodyStart, out float r0, out float b0);
            Assert.That(r0, Is.EqualTo(0f)); Assert.That(b0, Is.EqualTo(0f));
            HelmTransition.Phase(false, 1f, s.RootShare, s.BodyStart, out float r1, out float b1);
            Assert.That(r1, Is.EqualTo(1f)); Assert.That(b1, Is.EqualTo(1f));
            HelmTransition.Phase(false, 1f - s.BodyStart, s.RootShare, s.BodyStart, out _, out float bUp);
            Assert.That(bUp, Is.EqualTo(1f), "up by the mirrored body share");
            HelmTransition.Phase(false, (1f - s.RootShare) * 0.99f, s.RootShare, s.BodyStart, out float rEarly, out _);
            Assert.That(rEarly, Is.EqualTo(0f), "still at the station until the root's share before the end");
            HelmTransition.Phase(false, 0.5f, s.RootShare, s.BodyStart, out float rMid, out float bMid);
            Assert.That(bMid, Is.GreaterThan(rMid), "standing up leads the step off");
        }

        [Test]
        public void TheWeights_NeverJump_AtSixtyFrames([Values(true, false)] bool taking)
        {
            var s = Settings;
            float seconds = s.SecondsFor(taking, true);
            int frames = Mathf.CeilToInt(seconds / Frame);
            HelmTransition.Phase(taking, 0f, s.RootShare, s.BodyStart, out float lr, out float lb);
            float worst = 0f;
            for (int f = 1; f <= frames; f++)
            {
                HelmTransition.Phase(taking, f * Frame / seconds, s.RootShare, s.BodyStart, out float r, out float b);
                worst = Mathf.Max(worst, Mathf.Abs(r - lr), Mathf.Abs(b - lb));
                Assert.That(r, Is.GreaterThanOrEqualTo(lr - 1e-6f)); Assert.That(b, Is.GreaterThanOrEqualTo(lb - 1e-6f));
                lr = r; lb = b;
            }
            // The fastest weight is the shorter share: smoothstep peaks at 1.5x its average speed.
            float shortest = Mathf.Min(s.RootShare, 1f - s.BodyStart) * seconds;
            Assert.That(worst, Is.LessThanOrEqualTo(1.5f * Frame / shortest + 1e-4f));
        }

        [Test]
        public void Durations_AreAboutHalfASecond_AndReadByEnd()
        {
            var s = Settings;
            Assert.That(s.Enabled, Is.True);
            foreach (bool taking in new[] { true, false })
            foreach (bool seated in new[] { true, false })
                Assert.That(s.SecondsFor(taking, seated), Is.InRange(0.35f, 0.75f), $"taking {taking} seated {seated}");
            Assert.That(s.SecondsFor(true, true), Is.EqualTo(s.SeatTakeSeconds));
            Assert.That(s.SecondsFor(false, true), Is.EqualTo(s.SeatLeaveSeconds));
            Assert.That(s.SecondsFor(true, false), Is.EqualTo(s.WheelTakeSeconds));
            Assert.That(s.SecondsFor(false, false), Is.EqualTo(s.WheelLeaveSeconds));
            s.Enabled = false;
            Assert.That(s.SecondsFor(true, true), Is.EqualTo(0f), "off is the snap");
            Assert.That(new GameConfigProbe().Value.SeatTakeSeconds, Is.EqualTo(Settings.SeatTakeSeconds),
                "a fresh GameConfig carries the shipped move");
        }

        private sealed class GameConfigProbe
        {
            public readonly HelmTransitionSettings Value;
            public GameConfigProbe()
            {
                var config = ScriptableObject.CreateInstance<GameConfig>();
                Value = config.HelmSitStand;
                Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void OnlyDeckToHelmAndBack_IsAHop()
        {
            Assert.That(HelmTransition.HopBetween(ControlMode.OnDeck, ControlMode.Aboard), Is.EqualTo(1));
            Assert.That(HelmTransition.HopBetween(ControlMode.Aboard, ControlMode.OnDeck), Is.EqualTo(-1));
            Assert.That(HelmTransition.HopBetween(ControlMode.OnFoot, ControlMode.Aboard), Is.EqualTo(0), "a load or arrival is a re-seat");
            Assert.That(HelmTransition.HopBetween(ControlMode.OnFoot, ControlMode.OnDeck), Is.EqualTo(0), "boarding has its own move");
            Assert.That(HelmTransition.HopBetween(ControlMode.Aboard, ControlMode.Aboard), Is.EqualTo(0));
            Assert.That(HelmTransition.HopBetween(ControlMode.OnDeck, ControlMode.OnFoot), Is.EqualTo(0));
        }

        // ---------------------------------------------------------------- the move

        [Test]
        public void AMove_StartsWhereHeWasDrawn_AndEndsAtTheStation()
        {
            var m = new HelmTransition();
            Vector3 from = new Vector3(0.3f, -0.5f, 0.1f), to = new Vector3(0f, -1.6f, 0.2f);
            Assert.That(m.Begin(true, 10d, Settings, true, from, 350f, to), Is.True);
            Assert.That(m.Sample(10d), Is.True);
            m.Root(to, 20f, out Vector3 p0, out float y0);
            Assert.That((p0 - from).magnitude, Is.LessThan(1e-6f)); Assert.That(y0, Is.EqualTo(350f).Within(1e-4f));
            Assert.That(m.BodyWeight, Is.EqualTo(0f));

            // Mid-way the yaw has gone the short way round, through 0, never back through 180.
            float last = 350f;
            for (int f = 1; f < 36; f++)
            {
                if (!m.Sample(10d + f * Frame)) break;
                m.Root(to, 20f, out _, out float y);
                Assert.That(Mathf.DeltaAngle(last, y), Is.InRange(-1e-3f, 5f), $"frame {f}: turns the short way");
                last = y;
            }
            Assert.That(m.Sample(10d + Settings.SeatTakeSeconds + 1e-3), Is.False, "done on time");
            Assert.That(m.Active, Is.False);
            m.Root(to, 20f, out Vector3 p1, out float y1);
            Assert.That((p1 - to).magnitude, Is.LessThan(1e-6f)); Assert.That(Mathf.DeltaAngle(y1, 20f), Is.EqualTo(0f).Within(1e-4f));
            Assert.That(m.RootWeight, Is.EqualTo(1f)); Assert.That(m.BodyWeight, Is.EqualTo(1f));
        }

        [Test]
        public void ASnap_IsKeptWhereAMoveWouldBeWrong()
        {
            var m = new HelmTransition();
            var off = Settings; off.Enabled = false;
            Assert.That(m.Begin(true, 0d, off, true, Vector3.zero, 0f, Vector3.up), Is.False, "switched off");
            var zero = Settings; zero.SeatTakeSeconds = 0f;
            Assert.That(m.Begin(true, 0d, zero, true, Vector3.zero, 0f, Vector3.up), Is.False, "no time given");
            Assert.That(m.Begin(true, 0d, Settings, true, Vector3.zero, 0f, new Vector3(Settings.MaxTravelMeters + 0.1f, 0f, 0f)),
                Is.False, "too far to be a step: a re-seat");
            Assert.That(m.Active, Is.False);
            Assert.That(m.Begin(true, 0d, Settings, true, Vector3.zero, 0f, new Vector3(0f, 0f, 5f)), Is.True,
                "a seat's height is not a step across the deck");
        }

        [Test]
        public void AClockRunBackwards_HoldsTheStart_AndNeverOvershoots()
        {
            var m = new HelmTransition();
            m.Begin(false, 5d, Settings, true, Vector3.zero, 0f, Vector3.right);
            Assert.That(m.Sample(4d), Is.True);
            Assert.That(m.BodyWeight, Is.EqualTo(0f)); Assert.That(m.RootWeight, Is.EqualTo(0f));
        }

        // ---------------------------------------------------------------- the blend, toy chain

        private static CharacterSkinDef.Bone[] Chain => new[]
        {
            new CharacterSkinDef.Bone { Id = "root", Parent = -1 },
            new CharacterSkinDef.Bone { Id = "upper", Parent = 0 },
            new CharacterSkinDef.Bone { Id = "lower", Parent = 1 },
        };

        private static Matrix4x4[] Compose(CharacterSkinDef.Bone[] bones, params (Vector3 p, Quaternion r)[] locals)
        {
            var w = new Matrix4x4[bones.Length];
            for (int b = 0; b < bones.Length; b++)
            {
                var l = Matrix4x4.TRS(locals[b].p, locals[b].r, Vector3.one);
                w[b] = bones[b].Parent < 0 ? l : w[bones[b].Parent] * l;
            }
            return w;
        }

        private static Vector3 P(Matrix4x4 m) => new Vector3(m.m03, m.m13, m.m23);

        [Test]
        public void TheBlend_IsEachEnd_AtItsEnd_AndKeepsEveryBonesLength()
        {
            var bones = Chain;
            var from = Compose(bones, (new Vector3(0f, 0f, 1f), Quaternion.identity),
                                      (new Vector3(0f, 0f, 0.4f), Quaternion.identity),
                                      (new Vector3(0f, 0f, 0.35f), Quaternion.identity));
            var to = Compose(bones, (new Vector3(0.2f, 0.1f, 0.5f), Quaternion.Euler(0f, 0f, 90f)),
                                    (new Vector3(0f, 0f, 0.4f), Quaternion.Euler(80f, 0f, 0f)),
                                    (new Vector3(0f, 0f, 0.35f), Quaternion.Euler(-100f, 0f, 0f)));
            var result = new Matrix4x4[3];
            FigurePoseBlend.BlendLocal(from, to, bones, 0f, result);
            for (int b = 0; b < 3; b++) Assert.That((P(result[b]) - P(from[b])).magnitude, Is.LessThan(Rigid), $"w0 bone {b}");
            FigurePoseBlend.BlendLocal(from, to, bones, 1f, result);
            for (int b = 0; b < 3; b++) Assert.That((P(result[b]) - P(to[b])).magnitude, Is.LessThan(Rigid), $"w1 bone {b}");

            Vector3[] last = { P(from[0]), P(from[1]), P(from[2]) };
            for (int i = 1; i <= 60; i++)
            {
                FigurePoseBlend.BlendLocal(from, to, bones, i / 60f, result);
                Assert.That((P(result[1]) - P(result[0])).magnitude, Is.EqualTo(0.4f).Within(Rigid), $"step {i}: upper keeps its length");
                Assert.That((P(result[2]) - P(result[1])).magnitude, Is.EqualTo(0.35f).Within(Rigid), $"step {i}: lower keeps its length");
                for (int b = 0; b < 3; b++)
                {
                    Assert.That((P(result[b]) - last[b]).magnitude, Is.LessThan(0.08f), $"step {i} bone {b}: no jump");
                    last[b] = P(result[b]);
                }
            }
        }

        // ---------------------------------------------------------------- the blend, every skin

        private static IEnumerable<CharacterSkinDef> Skins(string seated)
        {
            foreach (string file in Directory.GetFiles(SkinFolder, "*.asset"))
            {
                var skin = AssetDatabase.LoadAssetAtPath<CharacterSkinDef>(file.Replace('\\', '/'));
                if (skin == null || !skin.TryGetClip(seated, out var c) || c.FrameCount <= 0) continue;
                if (!skin.TryGetClip(StandingState, out var s) || s.FrameCount <= 0) continue;
                yield return skin;
            }
        }

        private static RowingTargets Station(BoatVisualDef d) =>
            OutboardTillerStation.Targets(in d.Tiller, d.MotorMesh.PivotLocalMeters,
                                          HullPropFitment.Mount(0f, d.MotorMeshFitmentOffsetMeters),
                                          OutboardMotorMeshPose.Rotation(0f, 0f), 0f, 0f, d.MotorMaxSteerDegrees);

        private static Matrix4x4 Root(Vector3 local, float yaw) =>
            Matrix4x4.TRS(local, Quaternion.AngleAxis(yaw, Vector3.forward), Vector3.one);

        /// <summary>
        /// The presenter's own sequence, frame by frame: the hop keeps the drawn pose and root, then every
        /// frame the live target is solved at the station's root and drawn blended. Answers the drawn bones
        /// in the HULL's frame, per frame.
        /// </summary>
        private sealed class Rig
        {
            public CharacterSkinDef Skin;
            public RowingPoseSolver Solver;
            public CharacterSkinDef.SkinClip Standing, Seated;
            public BoatVisualDef Dory;
            public float SeatYaw, DeckYaw;
            public readonly HelmTransition Move = new HelmTransition();
            public Matrix4x4[] Drawn, Source, Target, Out;
            public Vector3 DrawnRoot;
            public float DrawnYaw;

            public void PoseTarget(bool seated)
            {
                if (seated)
                {
                    CharacterSkinPose.ComposeWorld(Seated, 0, Skin.Bones, Target);
                    Solver.Solve(Target, RowingPoseSolver.ToFigure(Station(Dory), Dory.Tiller.FloorOriginLocalMeters, SeatYaw, 0f));
                }
                else CharacterSkinPose.ComposeWorld(Standing, 0, Skin.Bones, Target);
            }

            /// <summary>Draw one frame; answers the hull-frame bones.</summary>
            public void Draw(bool seated, double now, Matrix4x4[] hull)
            {
                PoseTarget(seated);
                Vector3 toRoot = seated ? Dory.Tiller.FloorOriginLocalMeters : DeckStand;
                float toYaw = seated ? SeatYaw : DeckYaw;
                Vector3 root = toRoot; float yaw = toYaw;
                if (Move.Sample(now))
                {
                    Move.Root(toRoot, toYaw, out root, out yaw);
                    FigurePoseBlend.BlendLocal(Source, Target, Skin.Bones, Move.BodyWeight, Out);
                    System.Array.Copy(Out, Drawn, Drawn.Length);
                }
                else System.Array.Copy(Target, Drawn, Drawn.Length);
                DrawnRoot = root; DrawnYaw = yaw;
                Matrix4x4 r = Root(root, yaw);
                for (int b = 0; b < Drawn.Length; b++) hull[b] = r * Drawn[b];
            }

            public void Hop(bool taking, double now, bool seatedEnd)
            {
                System.Array.Copy(Drawn, Source, Drawn.Length);
                Vector3 to = taking ? Dory.Tiller.FloorOriginLocalMeters : DeckStand;
                Assert.That(Move.Begin(taking, now, Settings, seatedEnd, DrawnRoot, DrawnYaw, to), Is.True);
            }
        }

        private static Rig Build(CharacterSkinDef skin, BoatVisualDef d)
        {
            var rig = new Rig { Skin = skin, Dory = d, Solver = new RowingPoseSolver(skin.Bones, RowingBodyVolume.Of(skin)) };
            skin.TryGetClip(StandingState, out rig.Standing);
            skin.TryGetClip(d.Tiller.SeatedState, out rig.Seated);
            float bearing = d.Tiller.FacingDeckBearingDegrees;
            rig.SeatYaw = skin.AzimuthCounterClockwise ? -bearing : bearing;
            rig.DeckYaw = rig.SeatYaw + 120f;   // he walked up facing well away from the station's bearing
            int n = skin.Bones.Length;
            rig.Drawn = new Matrix4x4[n]; rig.Source = new Matrix4x4[n]; rig.Target = new Matrix4x4[n]; rig.Out = new Matrix4x4[n];
            return rig;
        }

        /// <summary>The body's bones: the held-prop sockets (<c>tool_*</c>) carry no skin, and a rod's tip
        /// metres out swings fast in any turn without anything of him moving fast.</summary>
        private static bool IsBody(CharacterSkinDef skin, int b) =>
            !skin.Bones[b].Id.StartsWith("tool_", System.StringComparison.Ordinal);

        private static string _worstBone;

        private static float WorstStep(CharacterSkinDef skin, Matrix4x4[] a, Matrix4x4[] b)
        {
            float worst = 0f;
            for (int i = 0; i < a.Length; i++)
            {
                if (!IsBody(skin, i)) continue;
                float d = (P(a[i]) - P(b[i])).magnitude;
                if (d > worst) { worst = d; _worstBone = skin.Bones[i].Id; }
            }
            return worst;
        }

                private static float WorstOff(Matrix4x4[] a, Matrix4x4[] b)
        {
            float worst = 0f;
            for (int i = 0; i < a.Length; i++) worst = Mathf.Max(worst, (P(a[i]) - P(b[i])).magnitude);
            return worst;
        }

        private static IEnumerable<(CharacterSkinDef skin, BoatVisualDef dory)> Cases()
        {
            var d = AssetDatabase.LoadAssetAtPath<BoatVisualDef>(DoryVisual);
            Assert.That(d, Is.Not.Null, DoryVisual);
            Assert.That(d.Tiller.Enabled, Is.True);
            int n = 0;
            foreach (var skin in Skins(d.Tiller.SeatedState)) { n++; yield return (skin, d); }
            Assert.That(n, Is.GreaterThan(0), "no skin carries both the standing and the seated clip");
        }

        /// <summary>A bone may travel at most this far in one 60 fps frame of a move: 7 m/s, a hand swung
        /// round while he turns 120 degrees to face the seat. A pop is the whole way (a metre and more) in
        /// one frame.</summary>
        private const float MaxStepPerFrame = 0.12f;

        /// <summary>The first and last frames of a move are eased: under 2 px of travel.</summary>
        private const float EaseEdgeMeters = 2f / 32f;

        [Test]
        public void SittingDownAtTheTiller_LeavesFromHisStand_AndLandsOnTheGrip_WithNoJump()
        {
            foreach (var (skin, d) in Cases())
            {
                var rig = Build(skin, d);
                int n = skin.Bones.Length;
                var prev = new Matrix4x4[n]; var now = new Matrix4x4[n];
                var standing = new Matrix4x4[n];
                rig.Draw(false, 0d, standing);                     // on deck, standing, before the press
                System.Array.Copy(standing, prev, n);
                rig.Hop(true, 0d, true);
                int frames = Mathf.CeilToInt(Settings.SeatTakeSeconds / Frame);
                float worst = 0f, first = 0f, lastStep = 0f;
                for (int f = 0; f <= frames + 1; f++)
                {
                    rig.Draw(true, f * Frame, now);
                    float step = WorstStep(skin, prev, now);
                    if (f == 0) Assert.That(step, Is.LessThan(Rigid), $"{skin.Id}: the hop's frame is the frame before");
                    if (f == 1) first = step;
                    if (f == frames) lastStep = step;
                    worst = Mathf.Max(worst, step);
                    System.Array.Copy(now, prev, n);
                }
                Assert.That(worst, Is.LessThan(MaxStepPerFrame), $"{skin.Id}: no bone jumps (worst {_worstBone})");
                Assert.That(first, Is.LessThan(EaseEdgeMeters), $"{skin.Id}: leaves at rest");
                Assert.That(lastStep, Is.LessThan(EaseEdgeMeters), $"{skin.Id}: arrives at rest");

                // Done: exactly the seated tiller pose, the hand on the grip.
                var seated = new Matrix4x4[n];
                rig.PoseTarget(true);
                Matrix4x4 r = Root(d.Tiller.FloorOriginLocalMeters, rig.SeatYaw);
                for (int b = 0; b < n; b++) seated[b] = r * rig.Target[b];
                Assert.That(WorstOff(now, seated), Is.LessThan(Rigid), $"{skin.Id}: ends exactly in the seated pose");
                Assert.That(rig.Move.Active, Is.False);
            }
        }

        [Test]
        public void GettingUpFromTheTiller_StandsFirst_ThenStepsOff_WithNoJump()
        {
            foreach (var (skin, d) in Cases())
            {
                var rig = Build(skin, d);
                int n = skin.Bones.Length;
                var prev = new Matrix4x4[n]; var now = new Matrix4x4[n];
                rig.Draw(true, 0d, prev);                          // seated, before the press
                rig.Hop(false, 0d, true);
                int frames = Mathf.CeilToInt(Settings.SeatLeaveSeconds / Frame);
                float worst = 0f;
                int pelvis = skin.IndexOfBone(RowingPoseSolver.PelvisId);
                float pelvisStart = P(prev[pelvis]).z;
                bool roseBeforeTheStep = false;
                for (int f = 0; f <= frames + 1; f++)
                {
                    rig.Draw(false, f * Frame, now);
                    worst = Mathf.Max(worst, WorstStep(skin, prev, now));
                    if (rig.Move.Active && rig.Move.RootWeight == 0f && P(now[pelvis]).z > pelvisStart + 0.1f)
                        roseBeforeTheStep = true;
                    System.Array.Copy(now, prev, n);
                }
                Assert.That(worst, Is.LessThan(MaxStepPerFrame), $"{skin.Id}: no bone jumps (worst {_worstBone})");
                Assert.That(roseBeforeTheStep, Is.True, $"{skin.Id}: he is up off the bench before he steps away");
                var standing = new Matrix4x4[n];
                CharacterSkinPose.ComposeWorld(rig.Standing, 0, skin.Bones, rig.Target);
                Matrix4x4 r = Root(DeckStand, rig.DeckYaw);
                for (int b = 0; b < n; b++) standing[b] = r * rig.Target[b];
                Assert.That(WorstOff(now, standing), Is.LessThan(Rigid), $"{skin.Id}: ends exactly standing on deck");
            }
        }

        [Test]
        public void ChangingHisMind_MidMove_StartsFromWhereHeIsDrawn()
        {
            foreach (var (skin, d) in Cases())
            {
                var rig = Build(skin, d);
                int n = skin.Bones.Length;
                var prev = new Matrix4x4[n]; var now = new Matrix4x4[n];
                rig.Draw(false, 0d, prev);
                rig.Hop(true, 0d, true);
                int half = Mathf.CeilToInt(0.45f * Settings.SeatTakeSeconds / Frame);
                float worst = 0f;
                double t = 0d;
                for (int f = 1; f <= half; f++)
                {
                    t = f * Frame;
                    rig.Draw(true, t, now);
                    worst = Mathf.Max(worst, WorstStep(skin, prev, now));
                    System.Array.Copy(now, prev, n);
                }
                Assert.That(rig.Move.Active, Is.True, "still mid-move");
                // He presses again: leave, from the half-sat pose and the half-way root.
                rig.Hop(false, t, true);
                rig.Draw(false, t, now);
                Assert.That(WorstStep(skin, prev, now), Is.LessThan(Rigid), $"{skin.Id}: the change of mind does not jump");
                int frames = Mathf.CeilToInt(Settings.SeatLeaveSeconds / Frame);
                for (int f = 1; f <= frames + 1; f++)
                {
                    rig.Draw(false, t + f * Frame, now);
                    worst = Mathf.Max(worst, WorstStep(skin, prev, now));
                    System.Array.Copy(now, prev, n);
                }
                Assert.That(worst, Is.LessThan(MaxStepPerFrame), $"{skin.Id}: no bone jumps either way (worst {_worstBone})");
                Assert.That(rig.Move.Active, Is.False);
            }
        }
    }
}
