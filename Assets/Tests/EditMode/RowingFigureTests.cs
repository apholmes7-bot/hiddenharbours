using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using Is = UnityEngine.TestTools.Constraints.Is;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The rower's body follows the oars: the Core stroke seam (<see cref="IOarStroke"/>,
    /// <see cref="OarStrokeMath"/>), the dory's oars publishing it, and <see cref="RowingFigure"/>
    /// turning it into a rowing clip and frame. Pure arithmetic and a hand-built skin def; no scene,
    /// no clock.
    /// </summary>
    public class RowingFigureTests
    {
        private const float Eps = 1e-5f;
        private const int RowFrames = 16;   // the row-stroke contract's frame count
        private const int OarsRowFrames = 8; // the rig's oars_row: 8 frames, two strokes

        private CharacterSkinDef _def;
        private GameObject _root;
        private BoatVisualDef _visual;

        [TearDown]
        public void TearDown()
        {
            if (_def != null) Object.DestroyImmediate(_def);
            if (_root != null) Object.DestroyImmediate(_root);
            if (_visual != null) Object.DestroyImmediate(_visual);
            _def = null;
            _root = null;
            _visual = null;
        }

        // ---------------------------------------------------------------- which way the pilot faces (data)

        [Test]
        public void ARowedHullsPilot_FacesTheStern_AndASteeredOne_FacesTheBow()
        {
            _visual = ScriptableObject.CreateInstance<BoatVisualDef>();
            Assert.AreEqual(BoatVisualDef.DefaultOarsPilotDeckBearingDegrees, _visual.OarsPilotDeckBearingDegrees,
                            "a fresh visual needs no edit for its rower to face aft");
            Assert.AreEqual(180f, BoatVisualDef.DefaultOarsPilotDeckBearingDegrees, "a rower sits facing the stern");
            Assert.AreEqual(0f, BoatVisualDef.HelmPilotDeckBearingDegrees, "a helm faces the bow");

            _visual.PilotStance = PilotStanceChoice.Oars;
            Assert.AreEqual(180f, _visual.PilotDeckBearingDegrees());
            Assert.IsFalse(RowingFigure.FacesBow(_visual.PilotDeckBearingDegrees()));

            _visual.PilotStance = PilotStanceChoice.Helm;
            Assert.AreEqual(0f, _visual.PilotDeckBearingDegrees(), "the helm facing is unchanged");
            _visual.PilotStance = PilotStanceChoice.None;
            Assert.AreEqual(0f, _visual.PilotDeckBearingDegrees());

            // The value is the asset's: an owner who wants his rower facing forward sets it there.
            _visual.PilotStance = PilotStanceChoice.Oars;
            _visual.OarsPilotDeckBearingDegrees = 0f;
            Assert.AreEqual(0f, _visual.PilotDeckBearingDegrees());
            Assert.IsTrue(RowingFigure.FacesBow(_visual.PilotDeckBearingDegrees()));
        }

        [Test]
        public void TheDorysRower_FacesAft_AndHerTholesAreAbaftHisThwart()
        {
            var dory = UnityEditor.AssetDatabase.LoadAssetAtPath<BoatVisualDef>("Assets/_Project/Data/Boats/Visuals/DoryIso.asset");
            Assert.IsNotNull(dory, "harness: the dory's visual asset");
            Assert.AreEqual(CharacterStance.Oars, dory.PilotStanceFor(), "the dory is rowed");
            Assert.AreEqual(180f, dory.PilotDeckBearingDegrees(), "and her rower faces the stern");

            // Facing aft is what makes the hands meet the handles: the oarlock is ABAFT the rowing thwart,
            // so the handles work aft of him, where an aft-facing figure reaches. Facing the bow, every
            // handle position is behind his back.
            Assert.IsNotNull(dory.OarPortMesh, "harness: the dory's mesh oar");
            var walk = JsonUtility.FromJson<DoryWalk>(File.ReadAllText(Path.Combine(
                Application.dataPath, "..", "docs/art/rigs/gameplay/doryIsoRig.dory_iso.walk.json")));
            Assert.Less(dory.OarPortMesh.PivotLocalMeters.y, walk.anchors.helm.y,
                        "the tholes must sit abaft the rower's seat (the hull's +y is forward)");
        }

        [Serializable] private class DoryWalk { public DoryAnchors anchors; }
        [Serializable] private class DoryAnchors { public Vector3 helm; }

        // ---------------------------------------------------------------- the oar's own geometry

        [Test]
        public void TheOarRigsCatch_IsWhereTheSweepIsFurthestForward_AndTheBladeIsGoingIn()
        {
            const int steps = 3600;
            float minSweep = float.MaxValue, minAt = -1f;
            for (int i = 0; i < steps; i++)
            {
                float t = i / (float)steps;
                float sweep = DoryOarMeshPose.Row(t).SweepDegrees;
                if (sweep < minSweep) { minSweep = sweep; minAt = t; }
            }
            Assert.AreEqual(DoryOarMeshPose.RowCatchTurns, minAt, 1f / steps,
                            "the catch constant must sit where Row()'s sweep is at its minimum");

            // Sweep negative = blade FORWARD (rig y is forward, Direction.y = -sin(sweep)·cos(dip)).
            foreach (int side in new[] { DoryOarMeshLayer.PortSide, DoryOarMeshLayer.StarboardSide })
            {
                DoryOarMeshPose.Pose catchPose = DoryOarMeshPose.Row(DoryOarMeshPose.RowCatchTurns);
                DoryOarMeshPose.Pose releasePose = DoryOarMeshPose.Row(DoryOarMeshPose.RowCatchTurns + 0.5f);
                float catchY = DoryOarMeshPose.Direction(side, catchPose.SweepDegrees, catchPose.DipDegrees).y;
                float releaseY = DoryOarMeshPose.Direction(side, releasePose.SweepDegrees, releasePose.DipDegrees).y;
                Assert.Greater(catchY, 0f, "the blade is forward of the oarlock at the catch");
                Assert.Less(releaseY, 0f, "the blade is aft of the oarlock at the release");
            }

            // Dip rising through the catch = blade going DOWN (Direction.z = -sin(dip)): going in.
            float before = DoryOarMeshPose.Row(DoryOarMeshPose.RowCatchTurns - 0.02f).DipDegrees;
            float after = DoryOarMeshPose.Row(DoryOarMeshPose.RowCatchTurns + 0.02f).DipDegrees;
            Assert.Greater(after, before, "the blade must be going in at the catch, not coming out");
            // Through the drive the blade is in the water (dip above its centre); through the recovery, out.
            Assert.Greater(DoryOarMeshPose.Row(DoryOarMeshPose.RowCatchTurns + 0.25f).DipDegrees,
                           DoryOarMeshPose.RowDipCentreDegrees, "mid-drive the blade is down");
            Assert.Less(DoryOarMeshPose.Row(DoryOarMeshPose.RowCatchTurns + 0.75f).DipDegrees,
                        DoryOarMeshPose.RowDipCentreDegrees, "mid-recovery the blade is up");
        }

        // ---------------------------------------------------------------- stroke phase mapping

        [TestCase(0.75f, 0f)]
        [TestCase(1.00f, 0.25f)]
        [TestCase(1.25f, 0.5f)]
        [TestCase(0.25f, 0.5f)]
        [TestCase(0.50f, 0.75f)]
        [TestCase(-0.25f, 0f)]
        [TestCase(7.75f, 0f)]
        public void OarTurns_MapToStrokePhase_CatchAtZeroReleaseAtHalf(float oarTurns, float expected)
        {
            Assert.AreEqual(expected, OarStrokeMath.PhaseFromOarTurns(oarTurns, DoryOarMeshPose.RowCatchTurns), Eps);
        }

        [Test]
        public void TheDorysColumns_MapToStrokePhase_ThroughTheSameCatch()
        {
            // The layer's accumulator runs in columns [0, StrokeColumns); the pose reads phase/columns turns.
            float catchColumns = DoryOarMeshPose.RowCatchTurns * DoryOarMath.StrokeColumns;
            Assert.AreEqual(0f, DoryOarMeshLayer.StrokePhaseOf(catchColumns), Eps);
            Assert.AreEqual(0.5f, DoryOarMeshLayer.StrokePhaseOf(catchColumns + DoryOarMath.StrokeColumns * 0.5f), Eps);
            Assert.AreEqual(0.25f, DoryOarMeshLayer.StrokePhaseOf(0f), Eps);
        }

        [TestCase(-3.25f, 0.75f)]
        [TestCase(1f, 0f)]
        [TestCase(0.999999f, 0.999999f)]
        [TestCase(float.NaN, 0f)]
        [TestCase(float.PositiveInfinity, 0f)]
        public void Wrap01_StaysInsideTheUnitInterval(float x, float expected)
        {
            float w = OarStrokeMath.Wrap01(x);
            Assert.AreEqual(expected, w, 1e-4f);
            Assert.That(w >= 0f && w < 1f);
        }

        [TestCase(0.00f, OarStrokePart.Catch)]
        [TestCase(0.95f, OarStrokePart.Catch)]
        [TestCase(0.12f, OarStrokePart.Catch)]
        [TestCase(0.20f, OarStrokePart.Drive)]
        [TestCase(0.40f, OarStrokePart.Release)]
        [TestCase(0.37f, OarStrokePart.Drive)]
        [TestCase(0.50f, OarStrokePart.Release)]
        [TestCase(0.70f, OarStrokePart.Recovery)]
        public void StrokePhase_FallsInTheRightPart(float phase, OarStrokePart part)
        {
            Assert.AreEqual(part, OarStrokeMath.PartOf(true, phase));
        }

        [Test]
        public void ANotStrokingOar_IsAtRest_WhateverItsPhase()
        {
            for (int i = 0; i < 8; i++)
                Assert.AreEqual(OarStrokePart.Rest, OarStrokeMath.PartOf(false, i / 8f));
        }

        // ---------------------------------------------------------------- one body, two oars

        [Test]
        public void TheBodyFollowsTheOarPullingHarder_PortOnATie_AndNoneWhenBothRest()
        {
            Assert.IsTrue(DoryOarMeshLayer.LeadingOarPhase(true, 1f, 0.5f, true, 2f, 0.9f, out float p));
            Assert.AreEqual(2f, p, "starboard pulls harder");
            Assert.IsTrue(DoryOarMeshLayer.LeadingOarPhase(true, 1f, -0.9f, true, 2f, 0.5f, out p));
            Assert.AreEqual(1f, p, "a hard backing port oar leads");
            Assert.IsTrue(DoryOarMeshLayer.LeadingOarPhase(true, 1f, 0.7f, true, 2f, 0.7f, out p));
            Assert.AreEqual(1f, p, "port on a tie");
            Assert.IsTrue(DoryOarMeshLayer.LeadingOarPhase(false, 1f, 0f, true, 3f, 0.4f, out p));
            Assert.AreEqual(3f, p, "only starboard rows");
            Assert.IsFalse(DoryOarMeshLayer.LeadingOarPhase(false, 1f, 0f, false, 3f, 0f, out p));
            Assert.AreEqual(0f, p);
        }

        [Test]
        public void AnUnwiredOarLayer_IsNotStroking()
        {
            _root = new GameObject("dory");
            DoryOarMeshLayer layer = _root.AddComponent<DoryOarMeshLayer>();
            IOarStroke stroke = layer;
            Assert.IsFalse(stroke.IsStroking);
            Assert.AreEqual(0f, stroke.StrokePhase);
            Assert.AreEqual(OarStrokePart.Rest, stroke.StrokePart);
        }

        // ---------------------------------------------------------------- body phase and facing

        [Test]
        public void FacingTheBow_TheHandsAreInAtTheCatch_AndOutAtTheRelease()
        {
            Assert.IsTrue(RowingFigure.FacesBow(0f));
            Assert.AreEqual(RowingFigure.RigHandsInShare, RowingFigure.BodyPhase(0f, facesBow: true), Eps,
                            "the handle is aft (in at his chest) when the blade is forward");
            Assert.AreEqual(0f, RowingFigure.BodyPhase(0.5f, facesBow: true), Eps,
                            "the handle is forward (hands out) when the blade is aft");
        }

        [Test]
        public void FacingAft_TheHandsAreOutAtTheCatch_AndInAtTheRelease()
        {
            Assert.IsFalse(RowingFigure.FacesBow(180f));
            Assert.AreEqual(0f, RowingFigure.BodyPhase(0f, facesBow: false), Eps,
                            "an aft-facing rower reaches out at the catch and leans forward");
            Assert.AreEqual(RowingFigure.RigHandsInShare, RowingFigure.BodyPhase(0.5f, facesBow: false), Eps,
                            "and has them in at the release, leaning back");
        }

        [TestCase(true)]
        [TestCase(false)]
        public void TheBodyPhase_RunsOnceRoundPerStroke_Monotonically(bool facesBow)
        {
            // Starting from the hands-out moment, the body phase climbs 0 -> 1 exactly once.
            float start = RowingFigure.HandsOutStrokePhase(facesBow);
            float last = -1f;
            const int steps = 200;
            for (int i = 0; i < steps; i++)
            {
                float body = RowingFigure.BodyPhase(start + i / (float)steps, facesBow);
                Assert.Greater(body, last, $"step {i}");
                last = body;
            }
            Assert.Less(last, 1f);
        }

        [Test]
        public void FacingIsDecidedByTheBearing()
        {
            Assert.IsTrue(RowingFigure.FacesBow(45f));
            Assert.IsTrue(RowingFigure.FacesBow(-80f));
            Assert.IsFalse(RowingFigure.FacesBow(100f));
            Assert.IsFalse(RowingFigure.FacesBow(-170f));
            Assert.AreEqual(0.5f, RowingFigure.HandsOutStrokePhase(true));
            Assert.AreEqual(0f, RowingFigure.HandsOutStrokePhase(false));
        }

        // ---------------------------------------------------------------- frame selection across a stroke

        [Test]
        public void AOneStrokeClip_StepsThroughEveryFrameOnce_InOrder_AcrossAStroke()
        {
            var seen = new bool[RowFrames];
            int last = 0;
            int advances = 0;
            const int steps = RowFrames * 20;
            for (int i = 0; i < steps; i++)
            {
                int frame = RowingFigure.FrameFor(i / (float)steps, RowFrames, RowingFigure.RowStrokeStrokesPerLoop);
                Assert.That(frame >= 0 && frame < RowFrames);
                seen[frame] = true;
                if (frame != last)
                {
                    Assert.AreEqual((last + 1) % RowFrames, frame, "frames advance one at a time, never skip");
                    advances++;
                    last = frame;
                }
            }
            Assert.That(Array.TrueForAll(seen, s => s), "every frame of the stroke is shown");
            Assert.AreEqual(RowFrames, advances, "one full loop of frames per stroke (last advance wraps to 0)");
        }

        [Test]
        public void TheRigsTwoStrokeClip_UsesOnlyItsFirstStrokesFrames()
        {
            int perStroke = OarsRowFrames / RowingFigure.OarsRowStrokesPerLoop;
            for (int i = 0; i < 100; i++)
            {
                int frame = RowingFigure.FrameFor(i / 100f, OarsRowFrames, RowingFigure.OarsRowStrokesPerLoop);
                Assert.That(frame >= 0 && frame < perStroke, $"phase {i / 100f} chose frame {frame}");
            }
            Assert.AreEqual(0, RowingFigure.FrameFor(0f, OarsRowFrames, 2));
            Assert.AreEqual(2, RowingFigure.FrameFor(0.5f, OarsRowFrames, 2));
        }

        [Test]
        public void FrameFor_PicksTheNearestSample()
        {
            Assert.AreEqual(0, RowingFigure.FrameFor(0.02f, RowFrames, 1));
            Assert.AreEqual(1, RowingFigure.FrameFor(1f / RowFrames - 0.01f, RowFrames, 1));
            Assert.AreEqual(0, RowingFigure.FrameFor(0.99f, RowFrames, 1), "the end of the stroke is its start");
            Assert.AreEqual(0, RowingFigure.FrameFor(0.5f, 0, 1), "no frames, frame 0");
        }

        // ---------------------------------------------------------------- the resolver: row, or idle

        [Test]
        public void AtTheOarsWhileStroking_TheFigureRowsTheOneStrokeClip_LockedToTheOar()
        {
            _def = Skin(meshed: new[] { RowingFigure.RowStrokeKey, RowingFigure.OarsRowKey, "idle_oars" });
            var stroke = new ScriptedStroke { Stroking = true };

            // Facing the bow, the release is hands-out: frame 0.
            stroke.Phase = 0.5f;
            Assert.IsTrue(RowingFigure.TryResolve(_def, CharacterStance.Oars, stroke, 0f, out string key, out int frame));
            Assert.AreEqual(RowingFigure.RowStrokeKey, key);
            Assert.AreEqual(0, frame);

            // The catch is hands-in: the frame nearest the rig's hands-in share.
            stroke.Phase = 0f;
            Assert.IsTrue(RowingFigure.TryResolve(_def, CharacterStance.Oars, stroke, 0f, out key, out frame));
            Assert.AreEqual(Mathf.RoundToInt(RowingFigure.RigHandsInShare * RowFrames), frame);
        }

        [Test]
        public void AtTheOarsFacingAft_TheDefault_HeReachesAtTheCatch_AndPullsInToTheRelease()
        {
            _def = Skin(meshed: new[] { RowingFigure.RowStrokeKey, RowingFigure.OarsRowKey, "idle_oars" });
            var stroke = new ScriptedStroke { Stroking = true };
            float aft = BoatVisualDef.DefaultOarsPilotDeckBearingDegrees;

            // The catch is hands-out, leaning forward toward the stern: frame 0.
            stroke.Phase = 0f;
            Assert.IsTrue(RowingFigure.TryResolve(_def, CharacterStance.Oars, stroke, aft, out string key, out int frame));
            Assert.AreEqual(RowingFigure.RowStrokeKey, key);
            Assert.AreEqual(0, frame);

            // The drive pulls the hands in and lays him back: at the release, the hands-in frame.
            stroke.Phase = OarStrokeMath.ReleasePhase;
            Assert.IsTrue(RowingFigure.TryResolve(_def, CharacterStance.Oars, stroke, aft, out key, out frame));
            Assert.AreEqual(Mathf.RoundToInt(RowingFigure.RigHandsInShare * RowFrames), frame);
        }

        [Test]
        public void WithoutTheOneStrokeClip_TheRigsOwnRowingClipPlays()
        {
            _def = Skin(meshed: new[] { RowingFigure.OarsRowKey, "idle_oars" });
            var stroke = new ScriptedStroke { Stroking = true, Phase = 0.5f };
            Assert.IsTrue(RowingFigure.TryResolve(_def, CharacterStance.Oars, stroke, 0f, out string key, out int frame));
            Assert.AreEqual(RowingFigure.OarsRowKey, key);
            Assert.AreEqual(0, frame);
        }

        [Test]
        public void AOneStrokeClipThatIsNotMeshed_IsSkippedForTheRigsClip()
        {
            _def = Skin(meshed: new[] { RowingFigure.OarsRowKey }, extraClips: new[] { RowingFigure.RowStrokeKey });
            var stroke = new ScriptedStroke { Stroking = true, Phase = 0.25f };
            Assert.IsTrue(RowingFigure.TryResolve(_def, CharacterStance.Oars, stroke, 0f, out string key, out _));
            Assert.AreEqual(RowingFigure.OarsRowKey, key, "ADR 0041: an unlisted state keeps its sheet");
        }

        [Test]
        public void RestingOars_GiveTheStateMapBack_SoTheFigureIdles()
        {
            _def = Skin(meshed: new[] { RowingFigure.RowStrokeKey, RowingFigure.OarsRowKey, "idle_oars" });
            var stroke = new ScriptedStroke { Stroking = false, Phase = 0.3f };
            Assert.IsFalse(RowingFigure.TryResolve(_def, CharacterStance.Oars, stroke, 0f, out string key, out int frame));
            Assert.IsNull(key);
            Assert.AreEqual(0, frame);

            Assert.IsFalse(RowingFigure.TryResolve(_def, CharacterStance.Oars, null, 0f, out _, out _),
                           "no oars under the figure");
            stroke.Stroking = true;
            Assert.IsFalse(RowingFigure.TryResolve(_def, CharacterStance.Free, stroke, 0f, out _, out _),
                           "not at the oars: a passenger does not row");
            Assert.IsFalse(RowingFigure.TryResolve(_def, CharacterStance.Helm, stroke, 0f, out _, out _));
        }

        [Test]
        public void ASkinWithNoRowingClip_IsLeftToTheStateMap()
        {
            _def = Skin(meshed: new[] { "idle_oars" });
            var stroke = new ScriptedStroke { Stroking = true, Phase = 0.1f };
            Assert.IsFalse(RowingFigure.TryResolve(_def, CharacterStance.Oars, stroke, 0f, out _, out _));
            Assert.IsFalse(RowingFigure.TryResolve(null, CharacterStance.Oars, stroke, 0f, out _, out _));
        }

        [Test]
        public void TheStateKeys_AreTheCarryKeysTheBakeWrites()
        {
            Assert.AreEqual("row_oars", RowingFigure.RowStrokeKey);
            Assert.AreEqual("walk_oars", RowingFigure.OarsRowKey);
        }

        // ---------------------------------------------------------------- the reader: Core only

        [Test]
        public void TheReader_FindsTheStrokeOnTheBoatAboveTheHull_AndFollowsAHullChange()
        {
            _root = new GameObject("boat");
            FakeOarStroke onBoat = _root.AddComponent<FakeOarStroke>();
            var hull = new GameObject("hull").transform;
            hull.SetParent(_root.transform, false);
            var other = new GameObject("other boat");

            var reader = new RowingFigureReader();
            Assert.AreSame(onBoat, reader.StrokeUnder(hull), "found through the hull's parent");
            Assert.AreSame(onBoat, reader.StrokeUnder(hull), "and cached");
            try
            {
                Assert.IsNull(reader.StrokeUnder(other.transform), "a hull with no oars has no stroke");
                Assert.IsNull(reader.StrokeUnder(null));
                Assert.AreSame(onBoat, reader.StrokeUnder(hull), "back on the dory");

                Object.DestroyImmediate(onBoat);
                Assert.IsNull(reader.StrokeUnder(hull), "a destroyed source is dropped, not read");
            }
            finally
            {
                Object.DestroyImmediate(other);
            }
        }

        [Test]
        public void TheReader_RowsOnlyAtTheOars()
        {
            _root = new GameObject("boat");
            FakeOarStroke onBoat = _root.AddComponent<FakeOarStroke>();
            onBoat.Stroking = true;
            onBoat.Phase = 0.5f;
            _def = Skin(meshed: new[] { RowingFigure.RowStrokeKey });
            var stand = new ScriptedStand { Hull = _root.transform, Bearing = 0f };

            var reader = new RowingFigureReader();
            Assert.IsTrue(reader.TryPose(_def, stand, CharacterStance.Oars, out string key, out int frame));
            Assert.AreEqual(RowingFigure.RowStrokeKey, key);
            Assert.AreEqual(0, frame);
            Assert.IsFalse(reader.TryPose(_def, stand, CharacterStance.Free, out _, out _));

            onBoat.Stroking = false;
            Assert.IsFalse(reader.TryPose(_def, stand, CharacterStance.Oars, out _, out _), "oars rest: idle");
        }

        // ---------------------------------------------------------------- rule 7: nothing per frame

        [Test]
        public void ASteadyRowingPose_AllocatesNothing()
        {
            _root = new GameObject("boat");
            FakeOarStroke onBoat = _root.AddComponent<FakeOarStroke>();
            onBoat.Stroking = true;
            _def = Skin(meshed: new[] { RowingFigure.RowStrokeKey, RowingFigure.OarsRowKey });
            var stand = new ScriptedStand { Hull = _root.transform, Bearing = 0f };
            var reader = new RowingFigureReader();
            reader.TryPose(_def, stand, CharacterStance.Oars, out _, out _);   // warm: the one search

            Assert.That(() =>
            {
                for (int i = 0; i < 64; i++)
                {
                    onBoat.Phase = i / 64f;
                    reader.TryPose(_def, stand, CharacterStance.Oars, out _, out _);
                    reader.TryPose(_def, stand, CharacterStance.Free, out _, out _);
                }
            }, Is.Not.AllocatingGCMemory(), "a rowing frame allocated (rule 7)");
        }

        // ---------------------------------------------------------------- the rig facts this leans on

        [Test]
        public void TheRigsRowingClip_HoldsTwoStrokes_WithTheHandsInAtFiftyFivePercent()
        {
            string poses = File.ReadAllText(Path.Combine(
                Application.dataPath, "..", "docs/art/rigs/character/rig10/Art/characterIsoRig10.poses.js"));
            StringAssert.Contains("ph=frac(2*u)", poses,
                "oars_row no longer holds two strokes per loop: update RowingFigure.OarsRowStrokesPerLoop");
            StringAssert.Contains("drive=ph<0.55?", poses,
                "oars_row's hands-in share moved: update RowingFigure.RigHandsInShare");
            Assert.AreEqual(2, RowingFigure.OarsRowStrokesPerLoop);
            Assert.AreEqual(0.55f, RowingFigure.RigHandsInShare, Eps);
        }

        // ---------------------------------------------------------------- helpers

        private static CharacterSkinDef Skin(string[] meshed, string[] extraClips = null)
        {
            var def = ScriptableObject.CreateInstance<CharacterSkinDef>();
            int extra = extraClips != null ? extraClips.Length : 0;
            var clips = new CharacterSkinDef.SkinClip[meshed.Length + extra];
            for (int i = 0; i < clips.Length; i++)
            {
                string key = i < meshed.Length ? meshed[i] : extraClips[i - meshed.Length];
                int frames = key == RowingFigure.OarsRowKey ? OarsRowFrames : RowFrames;
                clips[i] = new CharacterSkinDef.SkinClip { Anim = key, State = key, FrameCount = frames, Loop = true };
            }
            def.Clips = clips;
            def.MeshStates = meshed;
            return def;
        }

        private sealed class ScriptedStroke : IOarStroke
        {
            public bool Stroking;
            public float Phase;
            public bool IsStroking => Stroking;
            public float StrokePhase => Stroking ? Phase : 0f;
            public OarStrokePart StrokePart => OarStrokeMath.PartOf(Stroking, Phase);
        }

        private sealed class ScriptedStand : ICharacterFigureStand
        {
            public Transform Hull;
            public float Bearing;
            public IsoCharacterSprite FigureCharacter => null;
            public Transform FigureHull => Hull;
            public Vector3 FigureStandRigMetres => Vector3.zero;
            public float FigureDeckBearingDegrees => Bearing;
        }
    }

    /// <summary>A stand-in for a boat's oars, as a component on the boat: what the reader searches for.</summary>
    public sealed class FakeOarStroke : MonoBehaviour, IOarStroke
    {
        public bool Stroking;
        public float Phase;
        public bool IsStroking => Stroking;
        public float StrokePhase => Stroking ? Phase : 0f;
        public OarStrokePart StrokePart => OarStrokeMath.PartOf(Stroking, Phase);
    }
}
