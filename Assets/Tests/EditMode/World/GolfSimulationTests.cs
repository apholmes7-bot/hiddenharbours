using System;
using System.Collections.Generic;
using HiddenHarbours.World.Golf;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.World.EditMode
{
    public class GolfSimulationTests
    {
        readonly List<Object> _created = new List<Object>();
        T Def<T>() where T : ScriptableObject { var d = ScriptableObject.CreateInstance<T>(); _created.Add(d); return d; }
        [TearDown] public void Cleanup() { foreach (var o in _created) Object.DestroyImmediate(o); _created.Clear(); }

        GolfSurfaceDef Surface(GolfLie lie, float friction = 0.1f)
        {
            var d = Def<GolfSurfaceDef>(); d.Id = "golf_surface.test"; d.Lie = lie;
            d.RollingFriction = friction; d.StaticFriction = friction; d.Restitution = 0;
            d.LandingSpeedRetention = 1; d.LaunchSpeedMultiplier = 1; return d;
        }
        static Vector2[] Box(float x0, float y0, float x1, float y1) => new[] {
            new Vector2(x0, y0), new Vector2(x1, y0), new Vector2(x1, y1), new Vector2(x0, y1) };
        GolfArea Area(GolfLie lie, Vector2[] polygon, float friction = 0.1f) =>
            new GolfArea { Id = "area.test", Polygon = polygon, Surface = Surface(lie, friction) };
        GolfHoleDef Hole(float friction = 0.1f)
        {
            var d = Def<GolfHoleDef>(); d.Id = "hole.test";
            d.TeePosition = Vector2.zero; d.CupPosition = new Vector2(20, 0);
            d.Boundary = Box(-500, -500, 500, 500);
            d.Tee = Area(GolfLie.Tee, Box(-1, -1, 1, 1), friction);
            d.Fairway = Area(GolfLie.Fairway, Box(-300, -20, 300, 20), friction);
            d.Green = Area(GolfLie.Green, Box(10, -5, 30, 5), friction);
            d.Rough = Surface(GolfLie.Rough, friction); return d;
        }
        GolfClubDef Club(float speed = 8, float loft = 0)
        {
            var d = Def<GolfClubDef>(); d.Id = "golf_club.test";
            d.FullPowerSpeedMetresPerSecond = speed; d.LoftDegrees = loft; return d;
        }
        GolfRulesDef Rules()
        {
            var d = Def<GolfRulesDef>(); d.AirDragPerSecond = 0; return d;
        }
        static GolfShotContext Context(int seed = 1, double seconds = 100, int shot = 0, Vector2 wind = default) =>
            new GolfShotContext(seed, seconds, shot, wind);
        GolfShotResult Shoot(GolfHoleDef hole, GolfClubDef club, GolfRulesDef rules = null,
            float power = 1, Vector2? start = null, GolfShotContext? context = null, Vector2? aim = null) =>
            GolfSimulation.Shoot(hole.Build(), club.Build(), (rules ?? Rules()).Build(), start ?? hole.TeePosition,
                new GolfSwing(aim ?? Vector2.right, power), context ?? Context());

        [Test] public void FlatRoll_MatchesStoppingDistanceAndDoesNotReverse()
        {
            var result = Shoot(Hole(), Club(4));
            Assert.That(result.Status, Is.EqualTo(GolfShotStatus.Rest));
            Assert.That(result.Position.x, Is.EqualTo(16f / (2 * 0.1f * 9.81f)).Within(0.005));
            Assert.That(result.Position.y, Is.Zero); Assert.That(result.FlightSeconds, Is.Zero);
            Assert.That(result.Lie, Is.EqualTo(GolfLie.Fairway));
        }
        [Test] public void Flight_ZeroDragMatchesAnalyticCarryAndPeak()
        {
            var r = Shoot(Hole(), Club(20, 45));
            Assert.That(r.Status, Is.EqualTo(GolfShotStatus.Rest));
            Assert.That(r.FirstLanding.x, Is.EqualTo(400f / 9.81f).Within(0.02));
            Assert.That(r.FlightSeconds, Is.EqualTo(2 * 20 * Math.Sin(Math.PI / 4) / 9.81).Within(0.002));
            Assert.That(r.PeakHeightAboveStart, Is.EqualTo(400f / (4 * 9.81f)).Within(0.01));
        }
        [Test] public void HalfPowerHasQuarterCarry_ForLinearSpeedClub()
        {
            var hole = Hole(); var club = Club(20, 45);
            var full = Shoot(hole, club); var half = Shoot(hole, club, power: 0.5f);
            Assert.That(half.FirstLanding.x / full.FirstLanding.x, Is.EqualTo(0.25).Within(0.001));
        }
        [Test] public void SeedTimeAndShotIndexReplayExactly_RegardlessOfUnrelatedShots()
        {
            var hole = Hole(); var club = Club(20, 45); club.AimSpreadDegrees = 3;
            var context = Context(912, 87654321.125, 7, new Vector2(3, 2));
            var a = Shoot(hole, club, context: context);
            Shoot(hole, club, context: Context(4, 200));
            var b = Shoot(hole, club, context: context);
            Assert.That(b.Position, Is.EqualTo(a.Position)); Assert.That(b.FirstLanding, Is.EqualTo(a.FirstLanding));
            Assert.That(b.Seconds, Is.EqualTo(a.Seconds)); Assert.That(b.Steps, Is.EqualTo(a.Steps));
            Assert.That(Shoot(hole, club, context: Context(913, context.GameSeconds, 7, context.Wind)).Position, Is.Not.EqualTo(a.Position));
            Assert.That(Shoot(hole, club, context: Context(912, context.GameSeconds + 0.125, 7, context.Wind)).Position, Is.Not.EqualTo(a.Position));
            Assert.That(Shoot(hole, club, context: Context(912, context.GameSeconds, 8, context.Wind)).Position, Is.Not.EqualTo(a.Position));
        }
        [Test] public void SimulationWindChangesCarryAndCrosswind_WithoutNewWeatherGenerator()
        {
            var hole = Hole(); var club = Club(20, 45);
            var calm = Shoot(hole, club);
            var tail = Shoot(hole, club, context: Context(wind: new Vector2(8, 0)));
            var head = Shoot(hole, club, context: Context(wind: new Vector2(-8, 0)));
            var cross = Shoot(hole, club, context: Context(wind: new Vector2(0, 8)));
            Assert.That(tail.FirstLanding.x, Is.GreaterThan(calm.FirstLanding.x));
            Assert.That(head.FirstLanding.x, Is.LessThan(calm.FirstLanding.x));
            Assert.That(cross.FirstLanding.y, Is.GreaterThan(0));
        }
        [Test] public void SlopeChangesRoll_UphillShorterDownhillLonger()
        {
            var flat = Hole(); var uphill = Hole(); var downhill = Hole();
            uphill.Slope = new Vector2(0.04f, 0); downhill.Slope = new Vector2(-0.04f, 0);
            float middle = Shoot(flat, Club(4)).Position.x;
            Assert.That(Shoot(uphill, Club(4)).Position.x, Is.LessThan(middle));
            Assert.That(Shoot(downhill, Club(4)).Position.x, Is.GreaterThan(middle));
        }
        [Test] public void CrossSlopeDeflectsRoll_AndStaticFrictionHoldsRest()
        {
            var hole = Hole(); hole.Slope = new Vector2(0, 0.04f);
            var r = Shoot(hole, Club(4));
            Assert.That(r.Status, Is.EqualTo(GolfShotStatus.Rest)); Assert.That(r.Position.y, Is.LessThan(0));
        }
        [Test] public void RoughAndBunkerReduceLaunchAndRoll()
        {
            var hole = Hole(); hole.Rough.RollingFriction = 0.4f; hole.Rough.StaticFriction = 0.5f;
            hole.Rough.LaunchSpeedMultiplier = 0.5f;
            var club = Club(4);
            var fair = Shoot(hole, club, start: new Vector2(-20, 0));
            var rough = Shoot(hole, club, start: new Vector2(-20, 40));
            Assert.That(rough.Position.x + 20, Is.LessThan(fair.Position.x + 20));
            hole.Hazards = new[] { Area(GolfLie.Bunker, Box(2, -2, 9, 2), 0.5f) };
            hole.Hazards[0].Surface.StaticFriction = 0.5f;
            var sand = Shoot(hole, club);
            Assert.That(sand.Lie, Is.EqualTo(GolfLie.Bunker)); Assert.That(sand.Position.x, Is.LessThan(8));
        }
        [Test] public void ThinWaterHazardCannotBeSkippedByLargeRollStep()
        {
            var hole = Hole(); hole.Hazards = new[] { Area(GolfLie.Water, Box(2.13f, -2, 2.14f, 2)) };
            var rules = Rules(); rules.StepSeconds = 0.5f; rules.MaxTravelPerStepMetres = 20;
            var r = Shoot(hole, Club(8), rules);
            Assert.That(r.Status, Is.EqualTo(GolfShotStatus.Water));
            Assert.That(r.Position.x, Is.EqualTo(2.13f).Within(0.001));
        }
        [Test] public void AirborneBallCrossesWater_ButLandingInWaterIsPenalty()
        {
            var hole = Hole(); hole.Hazards = new[] { Area(GolfLie.Water, Box(2, -2, 8, 2)) };
            Assert.That(Shoot(hole, Club(20, 45)).Status, Is.EqualTo(GolfShotStatus.Rest));
            hole.Hazards[0].Polygon = Box(39, -2, 43, 2);
            Assert.That(Shoot(hole, Club(20, 45)).Status, Is.EqualTo(GolfShotStatus.Water));
        }
        [Test] public void OutOfBoundsStopsAtBoundary_InsteadOfRestingOutside()
        {
            var hole = Hole(); hole.Boundary = Box(-10, -10, 31, 10);
            var r = Shoot(hole, Club(10));
            Assert.That(r.Status, Is.EqualTo(GolfShotStatus.OutOfBounds));
            Assert.That(r.Position.x, Is.EqualTo(31).Within(0.001));
        }
        [Test] public void SlowPuttCapturesCupBetweenSteps_AndFastPuttPassesOver()
        {
            var hole = Hole(0.01f); hole.CupPosition = new Vector2(10.73f, 0);
            var rules = Rules(); rules.StepSeconds = 0.5f; rules.MaxTravelPerStepMetres = 3;
            var slow = Shoot(hole, Club(1), rules, start: new Vector2(10.1f, 0));
            Assert.That(slow.Status, Is.EqualTo(GolfShotStatus.Holed)); Assert.That(slow.Position, Is.EqualTo(hole.CupPosition));
            Assert.That(Shoot(hole, Club(4), rules, start: new Vector2(10.1f, 0)).Status, Is.EqualTo(GolfShotStatus.Rest));
        }
        [Test] public void BounceRetentionChangesRunout_WithoutChangingFirstContact()
        {
            var hole = Hole(); var club = Club(20, 45);
            var noBounce = Shoot(hole, club);
            hole.Rough.Restitution = hole.Fairway.Surface.Restitution = hole.Green.Surface.Restitution = 0.5f;
            var bounce = Shoot(hole, club);
            Assert.That(bounce.FirstLanding, Is.EqualTo(noBounce.FirstLanding));
            Assert.That(bounce.FlightSeconds, Is.GreaterThan(noBounce.FlightSeconds));
        }
        [Test] public void BoundedSimulationReturnsUnresolved_AndRoundDoesNotConsumeShot()
        {
            var rules = Rules(); rules.MaxSteps = 1;
            var round = new GolfRound(new[] { Hole().Build() }, rules.Build());
            Assert.That(round.Play(Club().Build(), new GolfSwing(Vector2.right, 1), Context()).Status, Is.EqualTo(GolfShotStatus.Unresolved));
            Assert.That(round.TotalStrokes, Is.Zero); Assert.That(round.ShotIndex, Is.Zero); Assert.That(round.BallPosition, Is.EqualTo(Vector2.zero));
        }
        [Test] public void WaterPenaltyCountsStrokeAndDistance_WithoutAdvancingHole()
        {
            var hole = Hole(); hole.Hazards = new[] { Area(GolfLie.Water, Box(2, -2, 3, 2)) };
            var rules = Rules(); rules.WaterPenaltyStrokes = 2;
            var round = new GolfRound(new[] { hole.Build() }, rules.Build());
            Assert.That(round.Play(Club().Build(), new GolfSwing(Vector2.right, 1), Context()).Status, Is.EqualTo(GolfShotStatus.Water));
            Assert.That(round.TotalStrokes, Is.EqualTo(3)); Assert.That(round.StrokesAt(0), Is.EqualTo(3));
            Assert.That(round.BallPosition, Is.EqualTo(hole.TeePosition)); Assert.That(round.HoleIndex, Is.Zero);
            Assert.That(round.CompletedPar, Is.Zero); Assert.That(round.ShotIndex, Is.EqualTo(1));
        }
        [Test] public void OutOfBoundsPenaltyUsesConfiguredCount_EvenWhenZero()
        {
            var hole = Hole(); hole.Boundary = Box(-2, -6, 31, 6); var rules = Rules(); rules.OutOfBoundsPenaltyStrokes = 0;
            var round = new GolfRound(new[] { hole.Build() }, rules.Build());
            Assert.That(round.Play(Club(10).Build(), new GolfSwing(Vector2.right, 1), Context()).Status, Is.EqualTo(GolfShotStatus.OutOfBounds));
            Assert.That(round.TotalStrokes, Is.EqualTo(1)); Assert.That(round.BallPosition, Is.EqualTo(Vector2.zero));
        }
        [Test] public void TwoHoleRoundScoresCompletedHolesOnly_RejectsFurtherPlay()
        {
            var first = Hole(0.01f); var second = Hole(0.01f);
            first.TeePosition = second.TeePosition = new Vector2(9.9f, 0);
            first.Tee.Polygon = second.Tee.Polygon = Box(9, -1, 9.99f, 1);
            first.CupPosition = second.CupPosition = new Vector2(10.3f, 0);
            second.Id = "hole.second"; second.Number = 2; second.Par = 3;
            var round = new GolfRound(new[] { first.Build(), second.Build() }, Rules().Build());
            var putter = Club(1).Build(); var swing = new GolfSwing(Vector2.right, 1);
            Assert.That(round.Play(putter, swing, Context()).Status, Is.EqualTo(GolfShotStatus.Holed));
            Assert.That(round.HoleIndex, Is.EqualTo(1)); Assert.That(round.ScoreToPar, Is.EqualTo(-3));
            Assert.That(round.Play(putter, swing, Context(shot: 1)).Status, Is.EqualTo(GolfShotStatus.Holed));
            Assert.That(round.Complete, Is.True); Assert.That(round.TotalStrokes, Is.EqualTo(2));
            Assert.That(round.CompletedPar, Is.EqualTo(7)); Assert.That(round.ScoreToPar, Is.EqualTo(-5));
            Assert.Throws<InvalidOperationException>(() => round.Play(putter, swing, Context(shot: 2)));
        }
        [Test] public void InvalidOrStaleShotLeavesRoundUntouched()
        {
            var round = new GolfRound(new[] { Hole().Build() }, Rules().Build()); var club = Club().Build();
            Assert.Throws<ArgumentException>(() => round.Play(club, new GolfSwing(Vector2.right, 1), Context(shot: 1)));
            Assert.Throws<ArgumentException>(() => round.Play(default, new GolfSwing(Vector2.right, 1), Context()));
            Assert.That(round.TotalStrokes, Is.Zero); Assert.That(round.BallPosition, Is.EqualTo(Vector2.zero));
        }
        [Test] public void BuiltRoundSnapshotsPolygonsAndTuning()
        {
            var hole = Hole(); var built = hole.Build(); var club = Club(4); var builtClub = club.Build();
            var rules = Rules(); var builtRules = rules.Build();
            hole.Boundary[0] = new Vector2(999, 999); hole.Tee.Surface.LaunchSpeedMultiplier = 0;
            club.FullPowerSpeedMetresPerSecond = 100; rules.MaxSteps = 1;
            var r = GolfSimulation.Shoot(built, builtClub, builtRules, Vector2.zero, new GolfSwing(Vector2.right, 1), Context());
            Assert.That(r.Status, Is.EqualTo(GolfShotStatus.Rest)); Assert.That(r.Position.x, Is.EqualTo(8.15494f).Within(0.005));
        }
        [Test] public void LiePrecedenceWaterThenBunkerThenGreenTeeFairwayRough()
        {
            var hole = Hole(); hole.Hazards = new[] {
                Area(GolfLie.Bunker, Box(15, -2, 16, 2)), Area(GolfLie.Water, Box(15.2f, -1, 15.8f, 1)) };
            hole.Hazards[1].Id = "pond.test"; var h = hole.Build();
            Assert.That(h.LieAt(Vector2.zero), Is.EqualTo(GolfLie.Tee));
            Assert.That(h.LieAt(new Vector2(5, 0)), Is.EqualTo(GolfLie.Fairway));
            Assert.That(h.LieAt(new Vector2(20, 0)), Is.EqualTo(GolfLie.Green));
            Assert.That(h.LieAt(new Vector2(15, 0)), Is.EqualTo(GolfLie.Bunker));
            Assert.That(h.LieAt(new Vector2(15.5f, 0)), Is.EqualTo(GolfLie.Water));
            Assert.That(h.LieAt(new Vector2(0, 50)), Is.EqualTo(GolfLie.Rough));
            Assert.That(h.LieAt(new Vector2(501, 0)), Is.EqualTo(GolfLie.OutOfBounds));
        }
        [Test] public void PolygonValidationRejectsSelfIntersectionAndDegenerateShapes()
        {
            var hole = Hole(); hole.Boundary = new[] { Vector2.zero, new Vector2(2, 2), new Vector2(0, 2), new Vector2(2, 0) };
            Assert.Throws<ArgumentException>(() => hole.Build());
            hole.Boundary = new[] { Vector2.zero, Vector2.right, Vector2.right * 2 };
            Assert.Throws<ArgumentException>(() => hole.Build());
        }
        [Test] public void PolygonWindingAndEdgesDoNotChangeLie()
        {
            var hole = Hole(); var a = hole.Build(); Array.Reverse(hole.Boundary); Array.Reverse(hole.Tee.Polygon);
            var b = hole.Build();
            Assert.That(b.LieAt(new Vector2(1, 0)), Is.EqualTo(GolfLie.Tee));
            Assert.That(b.LieAt(Vector2.zero), Is.EqualTo(a.LieAt(Vector2.zero)));
        }
        [Test] public void InvalidDefinitionsFailBeforeSimulation()
        {
            var hole = Hole(); hole.CupPosition = Vector2.zero; Assert.Throws<ArgumentException>(() => hole.Build());
            var club = Club(); club.LoftDegrees = 90; Assert.Throws<ArgumentException>(() => club.Build());
            var rules = Rules(); rules.StepSeconds = 0; Assert.Throws<ArgumentException>(() => rules.Build());
            var surface = Surface(GolfLie.Fairway); surface.RollingFriction = float.NaN;
            Assert.Throws<ArgumentException>(() => surface.Build());
            Assert.Throws<ArgumentException>(() => new GolfSwing(Vector2.zero, 1));
            Assert.Throws<ArgumentException>(() => new GolfSwing(Vector2.right, float.NaN));
            Assert.Throws<ArgumentException>(() => new GolfShotContext(1, double.NaN, 0, Vector2.zero));
            Assert.Throws<ArgumentException>(() => new GolfShotContext(1, 0, 0, new Vector2(float.PositiveInfinity, 0)));
        }
        [Test] public void RoundRejectsDuplicateHolesAndMixedCourses()
        {
            var a = Hole(); var b = Hole(); b.Id = "hole.second"; b.Number = 2; b.CourseId = "course.other";
            Assert.Throws<ArgumentException>(() => new GolfRound(new[] { a.Build(), a.Build() }, Rules().Build()));
            Assert.Throws<ArgumentException>(() => new GolfRound(new[] { a.Build(), b.Build() }, Rules().Build()));
        }
        [Test] public void AirborneOutOfBoundsIsEvaluatedAtLanding()
        {
            var hole = Hole(); hole.Boundary = Box(-10, -10, 31, 10);
            var r = Shoot(hole, Club(20, 45));
            Assert.That(r.Status, Is.EqualTo(GolfShotStatus.OutOfBounds));
            Assert.That(r.FirstLanding.x, Is.EqualTo(400f / 9.81f).Within(0.02));
            Assert.That(r.Position, Is.EqualTo(r.FirstLanding));
        }
        [Test] public void ShotTimeBudgetCannotInventAStoppedBall()
        {
            var rules = Rules(); rules.MaxShotSeconds = 0.01f;
            var r = Shoot(Hole(), Club(20, 45), rules);
            Assert.That(r.Status, Is.EqualTo(GolfShotStatus.Unresolved));
            Assert.That(r.Seconds, Is.EqualTo(0.01f)); Assert.That(r.Steps, Is.EqualTo(1));
        }
        [Test] public void SteepSlopeDoesNotFalselyHoldBallBelowStopSpeed()
        {
            var hole = Hole(); hole.Slope = new Vector2(-0.4f, 0);
            var rules = Rules(); rules.MaxShotSeconds = 1;
            var r = Shoot(hole, Club(0.001f), rules);
            Assert.That(r.Status, Is.EqualTo(GolfShotStatus.Unresolved));
            Assert.That(r.Position.x, Is.GreaterThan(0.1f));
        }
        [Test] public void FailedScoreOverflowIsAtomic()
        {
            var a = Hole(0.01f); var b = Hole(0.01f);
            a.TeePosition = b.TeePosition = new Vector2(9.9f, 0);
            a.Tee.Polygon = b.Tee.Polygon = Box(9, -1, 9.99f, 1);
            a.CupPosition = b.CupPosition = new Vector2(10.3f, 0);
            a.Par = int.MaxValue; b.Id = "hole.second"; b.Number = 2;
            var round = new GolfRound(new[] { a.Build(), b.Build() }, Rules().Build());
            var club = Club(1).Build(); var swing = new GolfSwing(Vector2.right, 1);
            round.Play(club, swing, Context());
            Assert.Throws<OverflowException>(() => round.Play(club, swing, Context(shot: 1)));
            Assert.That(round.TotalStrokes, Is.EqualTo(1)); Assert.That(round.StrokesAt(1), Is.Zero);
            Assert.That(round.ShotIndex, Is.EqualTo(1)); Assert.That(round.HoleIndex, Is.EqualTo(1));
            Assert.That(round.BallPosition, Is.EqualTo(b.TeePosition));
        }
        [Test] public void InvalidPenaltyAndSurfaceRulesAreRejected()
        {
            var rules = Rules(); rules.WaterPenaltyStrokes = -1; Assert.Throws<ArgumentException>(() => rules.Build());
            var surface = Surface(GolfLie.Green); surface.StaticFriction = 0;
            Assert.Throws<ArgumentException>(() => surface.Build());
            surface = Surface(GolfLie.Holed); Assert.Throws<ArgumentException>(() => surface.Build());
            var hole = Hole(); hole.Hazards = new[] { Area(GolfLie.Water, Box(2, -2, 3, 2)), Area(GolfLie.Bunker, Box(4, -2, 5, 2)) };
            Assert.Throws<ArgumentException>(() => hole.Build());
        }
    }
}
