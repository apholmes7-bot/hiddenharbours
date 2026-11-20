using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using HiddenHarbours.Player;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>WHICH SIDE OF HIM A PAIL DRAWS ON, AT EVERY HEADING</b> (<see cref="CarryMath.SideOrdersFor"/>
    /// over <see cref="CarryMath.TowardCameraMetres"/>): a pail in his near hand draws in front of the
    /// mesh figure, one in his far hand behind it, and a hand beside him (facing the camera or away)
    /// falls back to the facing rule.
    ///
    /// <para>The per-heading cases are measured on the SHIPPED fisher skin's own carry clips, through
    /// the same frame chain the ashore figure builds (ashore frame at the def's elevation and mirror,
    /// the facet child turned to the heading by the def's azimuth sign), so a rig re-bake that moves
    /// the hands, or a projection change that flips depth, fails here rather than in a picture.</para>
    /// </summary>
    public sealed class CarrySideByHeadingTests
    {
        const string FisherSkinPath = "Assets/_Project/Data/Characters/Skin/fisher.asset";
        const string Pail = "container.bucket";

        readonly List<Object> _made = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object o in _made)
                if (o != null) Object.DestroyImmediate(o);
            _made.Clear();
        }

        // ---- the rule on its own ------------------------------------------------------------------

        [Test]
        public void NearHandDrawsAhead_FarHandBehind_BesideFallsBackToTheFacing()
        {
            Assert.AreEqual(2, CarryMath.SideOrdersFor(0.2f, 0.03f, 270f, 2), "A near hand drew behind.");
            Assert.AreEqual(-2, CarryMath.SideOrdersFor(-0.2f, 0.03f, 90f, 2), "A far hand drew in front.");

            // Beside him: the facing decides, exactly as for a thing with no hand to measure.
            Assert.AreEqual(CarryMath.AheadOrdersFor(180f, 1), CarryMath.SideOrdersFor(0.01f, 0.03f, 180f, 1));
            Assert.AreEqual(CarryMath.AheadOrdersFor(0f, 1), CarryMath.SideOrdersFor(-0.01f, 0.03f, 0f, 1));
            Assert.AreEqual(1, CarryMath.SideOrdersFor(0.01f, 0.03f, 180f, 1), "Beside him facing the camera is in front.");
            Assert.AreEqual(-1, CarryMath.SideOrdersFor(-0.01f, 0.03f, 0f, 1), "Beside him facing away is behind.");

            // A negative band reads as no band at all, never as an inverted one.
            Assert.AreEqual(1, CarryMath.SideOrdersFor(0.001f, -0.5f, 0f, 1));
            Assert.AreEqual(-1, CarryMath.SideOrdersFor(-0.001f, -0.5f, 180f, 1));
        }

        [Test]
        public void DepthIgnoresHowHighTheHandHangs()
        {
            // Same spot around him, different heights: the answer must not move.
            Transform facet = FacetFrame(40f, 90f, counterClockwise: false);
            Matrix4x4 m = facet.localToWorldMatrix;
            var body = new Vector3(0f, 0f, 0.93f);
            float high = CarryMath.TowardCameraMetres(m, new Vector3(0.25f, 0f, 0.93f), body);
            float low = CarryMath.TowardCameraMetres(m, new Vector3(0.25f, 0f, 0.40f), body);
            Assert.AreEqual(high, low, 1e-5f);
            Assert.AreEqual(0f, CarryMath.TowardCameraMetres(m, body, body), 1e-5f);
        }

        // ---- per heading, on the shipped rig ------------------------------------------------------

        // Expected side for a pail in each hand at the eight compass headings (0 = N, clockwise).
        // Facing east his right hand is on the south side, toward the camera; facing west it is the far
        // side. Due north and due south both hands are beside him and the facing decides.
        static readonly (float heading, int right, int left)[] Expected =
        {
            (180f, +1, +1),   // S: facing the camera, both beside him: in front
            (135f, +1, -1),   // SE
            (90f, +1, -1),    // E
            (45f, +1, -1),    // NE
            (0f, -1, -1),     // N: facing away, both beside him: behind
            (315f, -1, +1),   // NW
            (270f, -1, +1),   // W
            (225f, -1, +1),   // SW
        };

        [Test]
        public void StandingWithAPail_EveryHeading_EveryFrame_FarHandBehindNearHandAhead()
            => AssertClip("idle_buckets", includeFacingCameraOrAway: true);

        [Test]
        public void WalkingWithAPail_EveryHeading_EveryFrame_FarHandBehindNearHandAhead()
            => AssertClip("walk_buckets", includeFacingCameraOrAway: false);

        [Test]
        public void RunningWithAPail_EveryHeading_EveryFrame_FarHandBehindNearHandAhead()
            => AssertClip("run_buckets", includeFacingCameraOrAway: false);

        /// <summary>
        /// Every frame of one carry clip at every heading, both hands. Due north and due south a moving
        /// fisher's hanging hand swings through the hip line, so it may honestly be a hair in front of
        /// or behind his legs on a given frame; only the standing clip pins those two headings.
        /// </summary>
        void AssertClip(string state, bool includeFacingCameraOrAway)
        {
            CharacterSkinDef skin = Fisher();
            Assert.IsTrue(skin.TryGetClip(state, out CharacterSkinDef.SkinClip clip),
                          $"The shipped fisher skin has no '{state}' clip.");
            CharacterCarryPoseDef.Row row = ShippedPailRow();

            int right = skin.IndexOfBone(row.SocketRight);
            int left = skin.IndexOfBone(row.SocketLeft);
            int body = skin.IndexOfBone(row.BodyBone);
            Assert.That(right, Is.Not.EqualTo(CharacterSkinDef.NoBone), $"No '{row.SocketRight}' bone.");
            Assert.That(left, Is.Not.EqualTo(CharacterSkinDef.NoBone), $"No '{row.SocketLeft}' bone.");
            Assert.That(body, Is.Not.EqualTo(CharacterSkinDef.NoBone), $"No '{row.BodyBone}' bone.");

            var world = new Matrix4x4[skin.BoneCount];
            var failures = new List<string>();
            foreach ((float heading, int wantRight, int wantLeft) in Expected)
            {
                bool besideHim = Mathf.Approximately(heading, 0f) || Mathf.Approximately(heading, 180f);
                if (besideHim && !includeFacingCameraOrAway) continue;

                Matrix4x4 m = FacetFrame(skin.ElevationDeg, heading, skin.AzimuthCounterClockwise).localToWorldMatrix;
                for (int f = 0; f < clip.FrameCount; f++)
                {
                    CharacterSkinPose.ComposeWorld(clip, f, skin.Bones, world);
                    Vector3 b = world[body].MultiplyPoint3x4(Vector3.zero);
                    int gotRight = Side(m, world[right].MultiplyPoint3x4(Vector3.zero), b, row, heading);
                    int gotLeft = Side(m, world[left].MultiplyPoint3x4(Vector3.zero), b, row, heading);
                    if (gotRight != wantRight) failures.Add($"{state} heading {heading} frame {f}: right hand {Word(gotRight)}, want {Word(wantRight)}");
                    if (gotLeft != wantLeft) failures.Add($"{state} heading {heading} frame {f}: left hand {Word(gotLeft)}, want {Word(wantLeft)}");
                }
            }
            Assert.IsEmpty(failures, string.Join("\n", failures));
        }

        static int Side(in Matrix4x4 m, Vector3 socket, Vector3 body, in CharacterCarryPoseDef.Row row, float heading)
            => CarryMath.SideOrdersFor(CarryMath.TowardCameraMetres(m, socket, body), row.DepthDeadbandMeters, heading, 1);

        static string Word(int side) => side > 0 ? "in front" : "behind";

        // ---- the shipped data ---------------------------------------------------------------------

        [Test]
        public void TheShippedPailRowHangsFromBothHandsOfTheShippedRig()
        {
            CharacterCarryPoseDef.Row row = ShippedPailRow();
            Assert.AreEqual("carry_L", row.SocketFor(CarryHandSide.Left));
            Assert.AreEqual("carry_R", row.SocketFor(CarryHandSide.Right));
            Assert.AreEqual("carry_R", row.SocketFor(CarryHandSide.None), "No particular hand reads as the right.");
            Assert.Greater(row.GripAbovePivotMeters, 0f, "The pail's grip must sit above its base pivot.");
            Assert.Greater(row.DepthDeadbandMeters, 0f, "With no beside-him band the order flickers at N and S.");

            CharacterSkinDef skin = Fisher();
            Assert.That(skin.IndexOfBone(row.SocketLeft), Is.Not.EqualTo(CharacterSkinDef.NoBone));
            Assert.That(skin.IndexOfBone(row.SocketRight), Is.Not.EqualTo(CharacterSkinDef.NoBone));
            Assert.That(skin.IndexOfBone(row.BodyBone), Is.Not.EqualTo(CharacterSkinDef.NoBone));
        }

        // ---- helpers ------------------------------------------------------------------------------

        static CharacterCarryPoseDef.Row ShippedPailRow()
        {
            var shipped = Resources.Load<CharacterCarryPoseDef>(CharacterCarryPoseDef.ResourcesPath);
            Assert.IsNotNull(shipped, $"No CharacterCarryPoseDef at Resources/{CharacterCarryPoseDef.ResourcesPath}.");
            Assert.IsTrue(shipped.TryRow(Pail, out CharacterCarryPoseDef.Row row), "The shipped table has no pail row.");
            return row;
        }

        static CharacterSkinDef Fisher()
        {
            var skin = AssetDatabase.LoadAssetAtPath<CharacterSkinDef>(FisherSkinPath);
            Assert.IsNotNull(skin, $"No fisher skin at {FisherSkinPath}.");
            return skin;
        }

        /// <summary>The ashore figure's frame chain, built the way <c>IsoCharacterFigureRenderer</c>
        /// builds it: an ashore frame at heading 0 and the def's elevation with the hull mirror, and the
        /// facet child turned by the presenter's yaw (the heading through the def's azimuth sign).</summary>
        Transform FacetFrame(float elevationDeg, float headingDeg, bool counterClockwise)
        {
            var root = new GameObject("CarrySideTest_Root");
            _made.Add(root);
            var frame = new GameObject("AshoreFrame").transform;
            frame.SetParent(root.transform, false);
            frame.localRotation = IsoFacetMath.HullRotation(0d, elevationDeg);
            frame.localScale = IsoFacetMath.HullScale;
            var facet = new GameObject("Facet").transform;
            facet.SetParent(frame, false);
            float yaw = counterClockwise ? -headingDeg : headingDeg;
            facet.localRotation = Quaternion.AngleAxis(yaw, Vector3.forward);
            return facet;
        }
    }
}
