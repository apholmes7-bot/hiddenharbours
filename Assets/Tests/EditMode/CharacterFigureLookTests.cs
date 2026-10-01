using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using Rot = HiddenHarbours.Core.CharacterFigureLook.Rot;
using Vec = HiddenHarbours.Core.CharacterFigureLook.Vec;
using Limits = HiddenHarbours.Core.CharacterFigureLook.Limits;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>THE LOOK'S ARITHMETIC</b> (<see cref="CharacterFigureLook"/>): the bearing measured in the
    /// chest's frame, wrapped through 180°, shared, clamped on both axes both ways, rounded to three
    /// decimals, and the eyes taking over past their threshold; and the turn's senses — + yaw toward
    /// the figure's right, + pitch tips the face down.
    ///
    /// <para>Synthetic limits of their own (yaw −40..45, pitch −15..20, eyes past 5°), so these hold
    /// the MECHANICS. That the port answers as the rig's own <c>lookAt</c> does over a grid, and lands
    /// where the rig's golden check does, is <c>CharacterSkinBakeGuardTests</c>'s, against the rig in
    /// V8.</para>
    /// </summary>
    public sealed class CharacterFigureLookTests
    {
        const double Deg = System.Math.PI / 180.0;
        static readonly Limits Lim = new Limits(-40, 45, -15, 20, 5, 0.6);
        static readonly Rot Identity = new Rot(1, 0, 0, 0, 1, 0, 0, 0, 1);
        static readonly Vec HeadAt = new Vec(0.1, -0.2, 1.4);
        static readonly Vec Mid = new Vec(0, 0, 0.12);

        /// <summary>The rig's Rz(θ), column-major: a turn about +z by θ degrees, right-handed.</summary>
        static Rot Rz(double deg)
        {
            double c = System.Math.Cos(deg * Deg), s = System.Math.Sin(deg * Deg);
            return new Rot(c, s, 0, -s, c, 0, 0, 0, 1);
        }

        /// <summary>A target <paramref name="range"/> away from the eye on a bearing (0 ahead on +y,
        /// + toward +x) and a rise.</summary>
        static Vec Target(double bearing, double range, double rise)
        {
            Vec eye = HeadAt + Mid;
            return eye + new Vec(range * System.Math.Sin(bearing * Deg), range * System.Math.Cos(bearing * Deg), rise);
        }

        static CharacterFigureLook.Result Look(Vec target, double share, Rot chest, Rot head) =>
            CharacterFigureLook.LookAt(chest, head, HeadAt, Mid, target, share, Lim);

        static CharacterFigureLook.Result Look(Vec target, double share) => Look(target, share, Identity, Identity);

        [Test]
        public void ATargetStraightAheadNeedsNoTurn()
        {
            CharacterFigureLook.Result r = Look(Target(0, 2, 0), 0.6);
            Assert.AreEqual(0.0, r.Yaw);
            Assert.AreEqual(0.0, r.Pitch);
            Assert.AreEqual(0.0, r.ResidualYaw);
            Assert.AreEqual(CharacterFigureLook.GazeOpen, r.Gaze);
        }

        [Test]
        public void TheHeadTakesItsShareAndTheEyesLeadPastTheirThreshold()
        {
            CharacterFigureLook.Result r = Look(Target(30, 2, 0), 0.7);
            Assert.AreEqual(30.0, r.NeedYaw, 1e-9);
            Assert.AreEqual(21.0, r.Yaw, 1e-9);
            Assert.AreEqual(9.0, r.ResidualYaw, 1e-9);
            Assert.AreEqual(CharacterFigureLook.GazeRight, r.Gaze, "Nine degrees past a five-degree threshold.");

            CharacterFigureLook.Result near = Look(Target(-12, 2, 0), 0.7);
            Assert.AreEqual(-3.6, near.ResidualYaw, 1e-9);
            Assert.AreEqual(CharacterFigureLook.GazeOpen, near.Gaze, "The eyes turned inside their threshold.");

            CharacterFigureLook.Result left = Look(Target(-30, 2, 0), 0.7);
            Assert.AreEqual(CharacterFigureLook.GazeLeft, left.Gaze);
        }

        [Test]
        public void TheTurnClampsOnBothAxesBothWays()
        {
            CharacterFigureLook.Result right = Look(Target(100, 2, 0), 1);
            Assert.AreEqual(45.0, right.Yaw, "Past the right limit.");
            Assert.AreEqual(55.0, right.ResidualYaw, 1e-9, "The eyes are left what the head could not take.");
            Assert.AreEqual(CharacterFigureLook.GazeRight, right.Gaze);

            CharacterFigureLook.Result left = Look(Target(-100, 2, 0), 1);
            Assert.AreEqual(-40.0, left.Yaw, "Past the left limit.");
            Assert.AreEqual(CharacterFigureLook.GazeLeft, left.Gaze);

            CharacterFigureLook.Result down = Look(Target(0, 2, -2), 1);
            Assert.AreEqual(45.0, down.NeedPitch, 1e-9, "A target below needs a + pitch: the face tips down.");
            Assert.AreEqual(20.0, down.Pitch, "Past the lower limit.");

            CharacterFigureLook.Result up = Look(Target(0, 2, 2), 1);
            Assert.AreEqual(-15.0, up.Pitch, "Past the upper limit.");
        }

        [Test]
        public void AShareOfNothingTurnsNothingAndLeavesTheEyesTheWhole()
        {
            CharacterFigureLook.Result r = Look(Target(20, 2, -0.5), 0);
            Assert.AreEqual(0.0, r.Yaw);
            Assert.AreEqual(0.0, r.Pitch);
            Assert.AreEqual(20.0, r.ResidualYaw, 1e-9);
            Assert.AreEqual(CharacterFigureLook.GazeRight, r.Gaze);
        }

        [Test]
        public void TheBearingIsTheChestsAndWrapsThrough180()
        {
            Rot east = Rz(-90);
            Vec eye = HeadAt + east.Apply(Mid);
            CharacterFigureLook.Result ahead = Look(eye + new Vec(2, 0, 0), 1, east, east);
            Assert.AreEqual(0.0, ahead.NeedYaw, 1e-9, "A chest facing +x measured a target on +x as off its bearing.");

            CharacterFigureLook.Result right = Look(eye + new Vec(0, -2, 0), 1, east, east);
            Assert.AreEqual(90.0, right.NeedYaw, 1e-9, "The figure's right, facing +x, is −y.");

            Rot turned = Rz(-170);
            CharacterFigureLook.Result wrapped = Look(Target(-170, 2, 0), 1, Identity, turned);
            Assert.AreEqual(20.0, wrapped.NeedYaw, 1e-9, "The need did not wrap through 180°.");
            Assert.AreEqual(20.0, wrapped.Yaw, 1e-9);
        }

        [Test]
        public void EveryAnswerIsRoundedToThreeDecimalsAsTheRigRoundsIt()
        {
            CharacterFigureLook.Result r = Look(Target(23.45678, 1.7, -0.31), 0.6);
            foreach (double v in new[] { r.Yaw, r.Pitch, r.NeedYaw, r.NeedPitch, r.ResidualYaw })
                Assert.AreEqual(System.Math.Round(v * 1000.0), v * 1000.0, 1e-7, $"{v} is not a whole thousandth.");
            Assert.AreNotEqual(0.0, r.Pitch);
        }

        [Test]
        public void TheTurnIsTheRigsLookEClampedThenSplit()
        {
            Quaternion q = CharacterFigureLook.TurnOf(30, 10, 0.6, Lim);
            Quaternion want = Quaternion.AngleAxis(-18f, Vector3.forward) * Quaternion.AngleAxis(-6f, Vector3.right);
            Assert.AreEqual(1f, Mathf.Abs(Quaternion.Dot(q, want)), 1e-6f);

            Quaternion clamped = CharacterFigureLook.TurnOf(100, -50, 0.4, Lim);
            Quaternion wantClamped = Quaternion.AngleAxis(-45f * 0.4f, Vector3.forward) *
                                     Quaternion.AngleAxis(15f * 0.4f, Vector3.right);
            Assert.AreEqual(1f, Mathf.Abs(Quaternion.Dot(clamped, wantClamped)), 1e-6f, "The turn was split before it was clamped.");
        }

        [Test]
        public void APlusYawTurnsTheFaceRightAndAPlusPitchTipsItDown()
        {
            Vector3 yawed = CharacterFigureLook.TurnOf(30, 0, 1, Lim) * Vector3.up;
            Assert.Greater(yawed.x, 0.4f, "+ yaw did not turn the face toward the figure's right (+x).");
            Vector3 pitched = CharacterFigureLook.TurnOf(0, 10, 1, Lim) * Vector3.up;
            Assert.Less(pitched.z, -0.1f, "+ pitch did not tip the face down (−z).");
        }

        [Test]
        public void TheRotationKeepsTheRigsColumnMajorLayout()
        {
            Quaternion q = Quaternion.Euler(20f, -35f, 50f);
            Rot r = Rot.Of(Matrix4x4.Rotate(q));
            var v = new Vector3(0.3f, -1.2f, 0.7f);
            Vector3 byQuat = q * v;
            Vec byRot = r.Apply(Vec.Of(v));
            Assert.AreEqual(byQuat.x, byRot.X, 1e-5);
            Assert.AreEqual(byQuat.y, byRot.Y, 1e-5);
            Assert.AreEqual(byQuat.z, byRot.Z, 1e-5);
            Vec back = r.ApplyInverse(byRot);
            Assert.AreEqual(v.x, back.X, 1e-5);
            Assert.AreEqual(v.y, back.Y, 1e-5);
            Assert.AreEqual(v.z, back.Z, 1e-5);
        }

        [Test]
        public void TheLimitsAreTheDefs()
        {
            var def = ScriptableObject.CreateInstance<CharacterSkinDef>();
            try
            {
                def.LookYawLimits = new Vector2(-33, 41);
                def.LookPitchLimits = new Vector2(-12, 17);
                def.LookEyesBeyondDeg = 4.5f;
                def.LookHeadShare = 0.55f;
                Limits l = Limits.Of(def);
                Assert.AreEqual(-33.0, l.YawMin);
                Assert.AreEqual(41.0, l.YawMax);
                Assert.AreEqual(-12.0, l.PitchMin);
                Assert.AreEqual(17.0, l.PitchMax);
                Assert.AreEqual((double)4.5f, l.EyesBeyond);
                Assert.AreEqual((double)0.55f, l.HeadShare);
            }
            finally
            {
                Object.DestroyImmediate(def);
            }
        }
    }
}
