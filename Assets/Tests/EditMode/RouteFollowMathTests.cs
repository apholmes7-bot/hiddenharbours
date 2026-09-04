using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>The driving maths, headless</b> — the three rules the PlayMode journey paid six minutes a piece
    /// to find (#701), pinned where they cost nothing to check.
    ///
    /// <para>Every one of these is a SABOTAGE-shaped test: it fails if the rule is removed, not merely if
    /// the arithmetic is wrong. The perpendicular clause, the converged-and-in-lane clause and the
    /// re-derived lookahead are the whole content of <see cref="RouteFollowMath"/>, and each has a case
    /// below that the naive implementation gets wrong.</para>
    /// </summary>
    public class RouteFollowMathTests
    {
        private static RouteFollowMath.RouteFollowTuning Driver => RouteFollowMath.RouteFollowTuning.Measured;

        // =============================================================================================
        //  RULE 1 — the perpendicular switch
        // =============================================================================================

        [Test]
        public void AWaypointInsideTheReachIsReached()
        {
            Assert.That(RouteFollowMath.HasReached(Vector2.zero, new Vector2(10f, 0f),
                                                   new Vector2(8.5f, 0f), 3f), Is.True);
        }

        [Test]
        public void AWaypointStillAheadIsNotReached()
        {
            Assert.That(RouteFollowMath.HasReached(Vector2.zero, new Vector2(10f, 0f),
                                                   new Vector2(4f, 0f), 3f), Is.False);
        }

        /// <summary>⭐ The measured defect, as a test. She overshot the waypoint by 9 m at an angle, so
        /// she is far outside the reach — and a driver that only asked "am I close?" would turn back for
        /// it and orbit, which is exactly what five machines did in the laydown for 3000 steps each.</summary>
        [Test]
        public void AWaypointOvershotWideOfTheLegIsReachedBecauseShePassedItsPerpendicular()
        {
            var from = new Vector2(0f, 0f);
            var target = new Vector2(10f, 0f);
            var overshot = new Vector2(19f, 9f);   // 12.7 m from the target: four times the reach

            Assert.That(Vector2.Distance(overshot, target), Is.GreaterThan(3f),
                "the fixture must put her OUTSIDE the reach or it is not testing the perpendicular.");
            Assert.That(RouteFollowMath.HasReached(from, target, overshot, 3f), Is.True);
        }

        [Test]
        public void AWaypointPassedOnTheOtherSideOfTheLegIsAlsoReached()
        {
            Assert.That(RouteFollowMath.HasReached(Vector2.zero, new Vector2(10f, 0f),
                                                   new Vector2(14f, -20f), 3f), Is.True);
        }

        [Test]
        public void ADegenerateLegFallsBackToTheReachAlone()
        {
            // from == target: there is no leg, so there is no perpendicular to cross and only distance
            // can answer. Far away is NOT reached — the alternative (a zero leg reading as "passed")
            // would retire every waypoint of a route authored with a repeated point, silently.
            Assert.That(RouteFollowMath.HasReached(Vector2.zero, Vector2.zero, new Vector2(50f, 0f), 3f),
                        Is.False);
            Assert.That(RouteFollowMath.HasReached(Vector2.zero, Vector2.zero, new Vector2(1f, 0f), 3f),
                        Is.True);
        }

        // =============================================================================================
        //  RULE 2 — converged AND in the lane
        // =============================================================================================

        private static readonly Vector2[] StraightRoad =
        {
            new Vector2(0f, 0f), new Vector2(100f, 0f),
        };

        [Test]
        public void FarEnoughAlongButOffTheCentreLineIsNotAnEndedLeg()
        {
            // The measured defect: an along-road projection of 30 m with the machine 8 m off the line.
            bool ended = RouteFollowMath.LegEnded(StraightRoad, 0, 2, new Vector2(30f, 8f), Vector2.zero,
                                                  Vector2.right, requiredAlongMetres: 20f,
                                                  laneHalfWidthMetres: 2.5f);
            Assert.That(ended, Is.False, "8 m off a 2.5 m half-width lane is not 'parked on the road'.");
        }

        [Test]
        public void OnTheCentreLineButNotYetFarEnoughAlongIsNotAnEndedLeg()
        {
            Assert.That(RouteFollowMath.LegEnded(StraightRoad, 0, 2, new Vector2(11f, 0.2f), Vector2.zero,
                                                 Vector2.right, 20f, 2.5f), Is.False);
        }

        [Test]
        public void FarEnoughAlongAndInsideTheLaneEndsTheLeg()
        {
            Assert.That(RouteFollowMath.LegEnded(StraightRoad, 0, 2, new Vector2(30f, 1.9f), Vector2.zero,
                                                 Vector2.right, 20f, 2.5f), Is.True);
        }

        [Test]
        public void OffCentreLineMeasuresToTheNearestPointNotToAnEnd()
        {
            Assert.That(RouteFollowMath.OffCentreLineMetres(StraightRoad, 0, 2, new Vector2(50f, 4f)),
                        Is.EqualTo(4f).Within(1e-3f));
        }

        // =============================================================================================
        //  RULE 3 — a lookahead re-derived from where she actually is
        // =============================================================================================

        [Test]
        public void TheLookaheadTargetIsAheadOfHerOwnProjectionOntoTheLine()
        {
            // She is 6 m off the road at x = 20. Her projection is (20, 0); the target is 12 m further
            // ALONG the road, not 12 m from her — which is what makes her converge rather than cross.
            Vector2 target = RouteFollowMath.LookaheadTarget(StraightRoad, 0, 2, new Vector2(20f, 6f), 12f);
            Assert.That(target.x, Is.EqualTo(32f).Within(1e-3f));
            Assert.That(target.y, Is.EqualTo(0f).Within(1e-3f));
        }

        [Test]
        public void TheLookaheadTargetMovesWithHerSoTheAimIsNeverAFixedPoint()
        {
            Vector2 a = RouteFollowMath.LookaheadTarget(StraightRoad, 0, 2, new Vector2(10f, 3f), 12f);
            Vector2 b = RouteFollowMath.LookaheadTarget(StraightRoad, 0, 2, new Vector2(40f, 3f), 12f);
            Assert.That(b.x - a.x, Is.EqualTo(30f).Within(1e-3f));
        }

        [Test]
        public void TheDirectionAtAPointIsTheSegmentSheIsBesideNotTheWholeRoutesChord()
        {
            var dogleg = new[] { new Vector2(0f, 0f), new Vector2(50f, 0f), new Vector2(50f, 50f) };
            Vector2 onTheFirst = RouteFollowMath.DirectionAt(dogleg, 0, 3, new Vector2(20f, 4f));
            Vector2 onTheSecond = RouteFollowMath.DirectionAt(dogleg, 0, 3, new Vector2(54f, 30f));

            Assert.That(onTheFirst.x, Is.EqualTo(1f).Within(1e-3f));
            Assert.That(onTheSecond.y, Is.EqualTo(1f).Within(1e-3f));
        }

        // =============================================================================================
        //  THE DEMAND
        // =============================================================================================

        [Test]
        public void ATargetToStarboardTurnsTheWheelRight()
        {
            // Heading north; the target lies east. Right is NEGATIVE on the wheel — the rig's own sense.
            DriveDemand d = RouteFollowMath.Toward(0f, Vector2.zero, new Vector2(30f, 30f), false, Driver);
            Assert.That(d.Steer, Is.LessThan(0f));
        }

        [Test]
        public void ATargetToPortTurnsTheWheelLeft()
        {
            DriveDemand d = RouteFollowMath.Toward(0f, Vector2.zero, new Vector2(-30f, 30f), false, Driver);
            Assert.That(d.Steer, Is.GreaterThan(0f));
        }

        [Test]
        public void DeadAheadIsFullCruiseAndNoWheel()
        {
            DriveDemand d = RouteFollowMath.Toward(0f, Vector2.zero, new Vector2(0f, 30f), false, Driver);
            Assert.That(d.Steer, Is.EqualTo(0f).Within(1e-4f));
            Assert.That(d.Throttle, Is.EqualTo(Driver.CruiseThrottle).Within(1e-4f));
            Assert.That(d.Brake, Is.False, "a route driver never touches the brake — she eases the "
                                           + "throttle, which is what the fleet's coast rate is for.");
        }

        [Test]
        public void SheComesOffCruiseForATurnAhead()
        {
            // 45° off: well past the 12° the tuning slows at.
            DriveDemand d = RouteFollowMath.Toward(0f, Vector2.zero, new Vector2(30f, 30f), false, Driver);
            Assert.That(d.Throttle, Is.EqualTo(Driver.TurnThrottle).Within(1e-4f));
        }

        [Test]
        public void TheWheelSaturatesAtFullLockRatherThanRunningPastIt()
        {
            DriveDemand d = RouteFollowMath.Toward(0f, Vector2.zero, new Vector2(0f, -30f), false, Driver);
            Assert.That(Mathf.Abs(d.Steer), Is.EqualTo(1f).Within(1e-4f));
        }

        [Test]
        public void SlowIsHonouredEvenDeadAhead()
        {
            DriveDemand d = RouteFollowMath.Toward(0f, Vector2.zero, new Vector2(0f, 30f), true, Driver);
            Assert.That(d.Throttle, Is.EqualTo(Driver.TurnThrottle).Within(1e-4f));
        }

        // =============================================================================================
        //  THE TUNING — an old def must still be driveable
        // =============================================================================================

        [Test]
        public void AllZeroTuningFillsInFromTheMeasuredDriver()
        {
            RouteFollowMath.RouteFollowTuning zeroed = default;
            RouteFollowMath.RouteFollowTuning sane = zeroed.Sane();
            RouteFollowMath.RouteFollowTuning measured = RouteFollowMath.RouteFollowTuning.Measured;

            Assert.That(sane.LookaheadMetres, Is.EqualTo(measured.LookaheadMetres),
                "a zero lookahead is a driver aiming at her own bumper — an asset baked before the "
                + "fields existed must still be driveable.");
            Assert.That(sane.WaypointReachMetres, Is.EqualTo(measured.WaypointReachMetres));
            Assert.That(sane.CruiseThrottle, Is.EqualTo(measured.CruiseThrottle));
            Assert.That(sane.SteerGainDegrees, Is.EqualTo(measured.SteerGainDegrees));
        }

        [Test]
        public void AnAuthoredTuningIsLeftAlone()
        {
            var authored = new RouteFollowMath.RouteFollowTuning(5f, 20f, 0.4f, 0.1f, 8f, 30f);
            RouteFollowMath.RouteFollowTuning sane = authored.Sane();

            Assert.That(sane.LookaheadMetres, Is.EqualTo(20f));
            Assert.That(sane.CruiseThrottle, Is.EqualTo(0.4f));
            Assert.That(sane.SlowForTurnDegrees, Is.EqualTo(8f));
        }

        [Test]
        public void AThrottleOverOneIsClampedRatherThanTakenAtFaceValue()
        {
            var silly = new RouteFollowMath.RouteFollowTuning(3f, 12f, 4f, 2f, 12f, 20f);
            Assert.That(silly.Sane().CruiseThrottle, Is.EqualTo(1f));
            Assert.That(silly.Sane().TurnThrottle, Is.EqualTo(1f));
        }
    }
}
