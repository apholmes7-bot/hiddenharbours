using System;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Boats;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The ambient fleet's local STEERING (canon M2-33): separation (two converging boats give way
    /// and pass, never deadlock bow-to-bow), and the tide-aware shoal look-ahead (a bow pointed at
    /// shallow water swings toward the deeper side and eases down). Pure vector maths, headless.
    /// </summary>
    public class AmbientFleetSteeringTests
    {
        // ---- repulsion ---------------------------------------------------------------------------

        [Test]
        public void Repulsion_PushesAwayFromANearObstacle_AndIgnoresFarOnes()
        {
            var obstacles = new[] { new Vector2(1f, 0f) };
            Vector2 push = AmbientFleetSteering.Repulsion(Vector2.zero, obstacles, 1, 5f);
            Assert.Less(push.x, 0f, "must push away (−x) from an obstacle at +x");
            Assert.AreEqual(0.8f, push.magnitude, 1e-4f, "linear falloff: 1 − d/R = 1 − 1/5");

            obstacles[0] = new Vector2(10f, 0f);
            Assert.AreEqual(Vector2.zero, AmbientFleetSteering.Repulsion(Vector2.zero, obstacles, 1, 5f),
                            "outside the radius there is nothing to shoulder away from");
        }

        [Test]
        public void Repulsion_SkipsACoLocatedEntry_SoASharedListCanIncludeTheBoatItself()
        {
            var obstacles = new[] { new Vector2(0f, 0f), new Vector2(2f, 0f) };
            Vector2 push = AmbientFleetSteering.Repulsion(Vector2.zero, obstacles, 2, 5f);
            Assert.Less(push.x, 0f, "only the real neighbour contributes");
            Assert.AreEqual(0f, push.y, 1e-5f);
        }

        // ---- head-on composition -------------------------------------------------------------------

        [Test]
        public void ComposeHeading_BreaksAHeadOnStandoff_Deterministically()
        {
            // Seek dead ahead, avoidance dead astern (the exact cancellation of a bow-to-bow meet):
            // the starboard bias must yield a real, unit direction — never a zero deadlock.
            Vector2 desired = AmbientFleetSteering.ComposeHeading(new Vector2(1f, 0f), new Vector2(-1f, 0f));
            Assert.AreEqual(1f, desired.magnitude, 1e-4f);
            Assert.AreNotEqual(0f, desired.y, "the tiebreak must veer off the line of approach");

            Vector2 again = AmbientFleetSteering.ComposeHeading(new Vector2(1f, 0f), new Vector2(-1f, 0f));
            Assert.AreEqual(desired, again, "the tiebreak is deterministic");
        }

        [Test]
        public void ComposeHeading_WithNoAvoidance_IsJustTheSeek()
        {
            Vector2 desired = AmbientFleetSteering.ComposeHeading(new Vector2(0f, 2f), Vector2.zero);
            Assert.AreEqual(0f, desired.x, 1e-5f);
            Assert.AreEqual(1f, desired.y, 1e-5f);
        }

        // ---- two converging boats diverge ----------------------------------------------------------

        [Test]
        public void TwoConvergingBoats_GiveWay_PassClear_AndStillReachTheirMarks()
        {
            // Boat A sails +x for (10,0); boat B sails −x for (−10,0): a dead head-on meet. Step the
            // exact update the presenter runs (repulsion → compose → rate-limited turn → move) and the
            // pair must keep clear and still make their marks — competent small-boat handling.
            const float dt = 0.05f, speed = 2f, turnRate = 90f, avoidRadius = 6f;
            var pos = new[] { new Vector2(-10f, 0f), new Vector2(10f, 0f) };
            var heading = new[] { new Vector2(1f, 0f), new Vector2(-1f, 0f) };
            var target = new[] { new Vector2(10f, 0f), new Vector2(-10f, 0f) };

            float minSeparation = float.MaxValue;
            var reached = new[] { false, false };
            var buffer = new Vector2[2];

            for (int step = 0; step < 600; step++)
            {
                buffer[0] = pos[0]; buffer[1] = pos[1];
                for (int i = 0; i < 2; i++)
                {
                    Vector2 toTarget = target[i] - pos[i];
                    if (toTarget.magnitude < 1.2f) { reached[i] = true; continue; }
                    Vector2 seek = toTarget.normalized;
                    Vector2 avoid = AmbientFleetSteering.Repulsion(pos[i], buffer, 2, avoidRadius);
                    Vector2 desired = AmbientFleetSteering.ComposeHeading(seek, avoid);
                    heading[i] = AmbientFleetSteering.RotateToward(heading[i], desired, turnRate * dt);
                    pos[i] += heading[i] * (speed * dt);
                }
                minSeparation = Mathf.Min(minSeparation, Vector2.Distance(pos[0], pos[1]));
                if (reached[0] && reached[1]) break;
            }

            Assert.Greater(minSeparation, 1.5f, "the boats must give way, not drive through each other");
            Assert.IsTrue(reached[0] && reached[1], "giving way must not stop either boat making her mark");
        }

        // ---- the shoal look-ahead -------------------------------------------------------------------

        [Test]
        public void DepthAvoid_IsSilent_InClearWater()
        {
            static float Deep(Vector2 p) => 10f;
            Vector2 steer = AmbientFleetSteering.DepthAvoid(Vector2.zero, Vector2.right, 6f, 40f,
                                                            Deep, 1f, out float speedScale);
            Assert.AreEqual(Vector2.zero, steer);
            Assert.AreEqual(1f, speedScale);
        }

        [Test]
        public void DepthAvoid_SwingsTowardTheDeeperBow_AndEasesDown_WhenShoalAhead()
        {
            // Shallow east of x = 5; the bow probe (at x = 6) shoals, both 40° probes (x ≈ 4.6) are
            // still deep — she must swing off the line and slow, not plow on at full speed.
            static float Shelf(Vector2 p) => p.x < 5f ? 10f : 0.1f;
            Vector2 steer = AmbientFleetSteering.DepthAvoid(Vector2.zero, Vector2.right, 6f, 40f,
                                                            Shelf, 1f, out float speedScale);
            Assert.Greater(steer.magnitude, 0.1f, "a shoal ahead must produce a correction");
            Assert.Less(Vector2.Dot(steer.normalized, Vector2.right), 0.9f, "the correction turns her OFF the shoal line");
            Assert.Less(speedScale, 1f, "she eases down feeling her way");
        }

        [Test]
        public void DepthAvoid_BacksOff_WhenShoalAllRoundTheBow()
        {
            static float Wall(Vector2 p) => p.x > -1f ? 0.1f : 10f;
            Vector2 steer = AmbientFleetSteering.DepthAvoid(Vector2.zero, Vector2.right, 6f, 40f,
                                                            Wall, 1f, out float speedScale);
            Assert.Less(steer.x, 0f, "with both bows shoal she comes astern-ward");
            Assert.LessOrEqual(speedScale, 0.2f, "and all but stops");
        }

        // ---- the rate-limited turn ------------------------------------------------------------------

        [Test]
        public void RotateToward_SwingsAtMostTheGivenRate()
        {
            Vector2 swung = AmbientFleetSteering.RotateToward(Vector2.right, Vector2.up, 30f);
            Assert.AreEqual(30f, Vector2.SignedAngle(Vector2.right, swung), 1e-3f, "a 90° demand meets the 30° cap");

            Vector2 snapped = AmbientFleetSteering.RotateToward(Vector2.right, Vector2.up, 180f);
            Assert.AreEqual(90f, Vector2.SignedAngle(Vector2.right, snapped), 1e-3f, "inside the cap it reaches the demand");
        }
    }
}
