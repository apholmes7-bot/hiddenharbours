using System;
using HiddenHarbours.Boats;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    internal static class YachtRaceFixtures
    {
        public static SailRaceClubRules Club => new SailRaceClubRules(360, 8, 100);
        public static SailRaceHandicap Rating(string hull = "boat.fixture", float multiplier = 1) => new SailRaceHandicap(hull, multiplier);
        public static RaceBoatMotion Motion(string id, Vector2 p, Vector2? heading = null)
            => new RaceBoatMotion(id, p, heading ?? Vector2.up, Vector2.zero);
        public static SailRaceEntry Entry(string id = "racer.player", Vector2? p = null)
            => new SailRaceEntry(id, Rating(), new RaceTrackSample(0, p ?? new Vector2(5, -10)), Vector2.up, 4);
        public static SailRaceSession Session() => new SailRaceSession(SailRaceFixtures.Course(), Club, 0, new[] { Entry() });
        public static void RoundAndFinish(SailRaceSession race, double offset = 0)
        {
            race.Tick(offset + 10, new[] { Motion("racer.player", new Vector2(5, 10)) }, Vector2.left);
            race.Tick(offset + 20, new[] { Motion("racer.player", new Vector2(5, 19)) }, Vector2.left);
            race.Tick(offset + 21, new[] { Motion("racer.player", new Vector2(5, 21)) }, Vector2.left);
            race.Tick(offset + 22, new[] { Motion("racer.player", new Vector2(3, 24)) }, Vector2.left);
            race.Tick(offset + 23, new[] { Motion("racer.player", new Vector2(-1, 25)) }, Vector2.left);
            race.Tick(offset + 30, new[] { Motion("racer.player", new Vector2(-1, 0)) }, Vector2.left);
        }
        public static SailRaceResult Finish(string id, double seconds, float multiplier = 1)
            => new SailRaceResult(id, Rating(multiplier: multiplier), SailRaceStatus.Finished, seconds);
    }

    public class YachtRaceClubTests
    {
        [Test]
        public void BoatThatMissesAMark_DoesNotFinish()
        {
            var race = YachtRaceFixtures.Session();
            race.Tick(10, new[] { YachtRaceFixtures.Motion("racer.player", new Vector2(5, 10)) }, Vector2.left);
            race.Tick(20, new[] { YachtRaceFixtures.Motion("racer.player", new Vector2(5, 0)) }, Vector2.left);
            Assert.That(race.Entrant(0).Started, Is.True);
            Assert.That(race.Entrant(0).NextMarkIndex, Is.EqualTo(0));
            Assert.That(race.Status(0), Is.EqualTo(SailRaceStatus.Racing));
            Assert.That(race.Complete, Is.False);
            Assert.That(double.IsNaN(race.Results()[0].CorrectedSeconds), Is.True);
        }

        [Test]
        public void RaceStateMachine_StartsRoundsAndFinishesAtInterpolatedTime()
        {
            var race = YachtRaceFixtures.Session();
            Assert.That(race.Status(0), Is.EqualTo(SailRaceStatus.Waiting));
            Assert.That(race.Phase, Is.EqualTo(SailRacePhase.Racing));
            YachtRaceFixtures.RoundAndFinish(race);
            Assert.That(race.Status(0), Is.EqualTo(SailRaceStatus.Finished));
            Assert.That(race.Complete, Is.True);
            Assert.That(race.Results()[0].ElapsedSeconds, Is.EqualTo(28.6).Within(0.00001));
            Assert.Throws<InvalidOperationException>(() => race.Retire(0));
        }

        [Test]
        public void RetirementCompletesUnfinishedRosterWithoutNumericFinish()
        {
            var race = YachtRaceFixtures.Session(); race.Retire(0);
            YachtRaceFixtures.RoundAndFinish(race);
            Assert.That(race.Complete, Is.True);
            Assert.That(race.Status(0), Is.EqualTo(SailRaceStatus.Retired));
            Assert.That(double.IsNaN(race.Results()[0].ElapsedSeconds), Is.True);
        }

        private static SailRaceClubBoat Boat(string id, Vector2 p, Vector2? heading = null, int lap = 0, int mark = 0)
            => new SailRaceClubBoat(YachtRaceFixtures.Motion(id, p, heading), lap, mark, 4);
        private static void BothOrders(SailRaceClubBoat a, SailRaceClubBoat b, Vector2 wind, string offender, SailRaceClubRule rule)
        {
            var ab = SailRaceClubJudge.Judge(a, b, wind, SailRaceFixtures.Course());
            var ba = SailRaceClubJudge.Judge(b, a, wind, SailRaceFixtures.Course());
            Assert.That(ab.GiveWayId, Is.EqualTo(offender)); Assert.That(ba.GiveWayId, Is.EqualTo(offender));
            Assert.That(ab.Rule, Is.EqualTo(rule)); Assert.That(ba.Rule, Is.EqualTo(rule));
        }

        [TestCase(1)]
        [TestCase(-1)]
        public void PortGivesWayToStarboard_BothWindDirections(int direction)
        {
            BothOrders(Boat("racer.a", Vector2.right, Vector2.up), Boat("racer.b", Vector2.left, Vector2.down),
                Vector2.left * direction, direction > 0 ? "racer.b" : "racer.a", SailRaceClubRule.PortStarboard);
        }

        [TestCase(1)]
        [TestCase(-1)]
        public void WindwardGivesWayToLeeward_BothWindDirections(int direction)
        {
            BothOrders(Boat("racer.a", Vector2.right), Boat("racer.b", Vector2.left), Vector2.left * direction,
                direction > 0 ? "racer.a" : "racer.b", SailRaceClubRule.WindwardLeeward);
        }

        [TestCase(1)]
        [TestCase(-1)]
        public void RoomAtMark_OutsideBoatGivesRoomOnBothSides(int side)
        {
            // Inner boat is windward: mark room must override windward/leeward, in both directions.
            BothOrders(Boat("racer.inner", new Vector2(2 * side, 20)), Boat("racer.outer", new Vector2(4 * side, 20)),
                Vector2.right * side, "racer.outer", SailRaceClubRule.MarkRoom);
        }

        [Test]
        public void MarkRoomRequiresOverlapSameMarkLapAndEstablishedTack()
        {
            var a = Boat("racer.a", new Vector2(2, 20));
            BothOrders(a, Boat("racer.b", new Vector2(4, 20), lap: 1), Vector2.right, "racer.a", SailRaceClubRule.WindwardLeeward);
            BothOrders(a, Boat("racer.b", new Vector2(4, 20), mark: 1), Vector2.right, "racer.a", SailRaceClubRule.WindwardLeeward);
            BothOrders(a, Boat("racer.b", new Vector2(4, 27)), Vector2.right, "racer.a", SailRaceClubRule.WindwardLeeward);
            BothOrders(a, Boat("racer.b", new Vector2(4, 20), Vector2.down), Vector2.right, "racer.a", SailRaceClubRule.PortStarboard);
            BothOrders(a, Boat("racer.b", new Vector2(4, 20)), Vector2.zero, null, SailRaceClubRule.None);
            BothOrders(a, Boat("racer.b", new Vector2(4, 20)), Vector2.down, null, SailRaceClubRule.None);
        }

        [Test]
        public void OutsideZoneOrEqualRadius_DoesNotInventMarkRoom()
        {
            BothOrders(Boat("racer.a", new Vector2(12, 20)), Boat("racer.b", new Vector2(14, 20)),
                Vector2.right, "racer.a", SailRaceClubRule.WindwardLeeward);
            BothOrders(Boat("racer.a", new Vector2(2, 20)), Boat("racer.b", new Vector2(-2, 20)),
                Vector2.right, "racer.b", SailRaceClubRule.WindwardLeeward);
        }

        [TestCase(1)]
        [TestCase(-1)]
        public void PenaltyTurnRequiresFullRotationAndMinimumTime_BothDirections(int sign)
        {
            var turn = new SailRaceTurn(YachtRaceFixtures.Club); turn.Queue(0, 0);
            for (int i = 1; i <= 4; i++) turn.Advance(i, sign * 90 * i);
            Assert.That(turn.Owed, Is.EqualTo(1), "A fast spin still owes minimum turn time.");
            Assert.That(turn.Advance(8, sign * 360), Is.True);
            Assert.That(turn.Owed, Is.EqualTo(0)); Assert.That(turn.Completed, Is.EqualTo(1));
        }

        [Test]
        public void OscillationTimeAloneAndAmbiguousHeading_DoNotClearPenalty()
        {
            var turn = new SailRaceTurn(YachtRaceFixtures.Club); turn.Queue(0, 0);
            for (int i = 1; i <= 20; i++) turn.Advance(i, i % 2 == 0 ? 0 : 90);
            Assert.That(turn.Owed, Is.EqualTo(1));
            turn.Advance(100, 0); Assert.That(turn.Owed, Is.EqualTo(1));
            Assert.Throws<ArgumentException>(() => turn.Advance(101, 170));
            Assert.Throws<ArgumentException>(() => turn.Advance(99, 0));
            Assert.That(turn.SweepDegrees, Is.EqualTo(-90));
        }

        [Test]
        public void MultiplePenaltiesRequireSeparateCompleteTurns()
        {
            var turn = new SailRaceTurn(YachtRaceFixtures.Club); turn.Queue(0, 0); turn.Queue(0, 0);
            for (int i = 1; i <= 4; i++) turn.Advance(i * 2, i * 90);
            Assert.That(turn.Owed, Is.EqualTo(1));
            for (int i = 5; i <= 8; i++) turn.Advance(i * 2, i * 90);
            Assert.That(turn.Owed, Is.EqualTo(0)); Assert.That(turn.Completed, Is.EqualTo(2));
        }

        [Test]
        public void ContactQueuesOneTurnUntilSeparationAndBlocksCourseProgress()
        {
            var race = Pair(); var motions = new[] { YachtRaceFixtures.Motion("racer.player", new Vector2(1, -5)),
                YachtRaceFixtures.Motion("racer.other", new Vector2(-1, -5)) };
            race.Tick(1, motions, Vector2.left);
            Assert.That(race.OwedTurns(0), Is.EqualTo(1));
            race.Tick(2, motions, Vector2.left); Assert.That(race.OwedTurns(0), Is.EqualTo(1));
            motions[1] = YachtRaceFixtures.Motion("racer.other", new Vector2(-5, -5));
            race.Tick(3, motions, Vector2.left);
            motions[1] = YachtRaceFixtures.Motion("racer.other", new Vector2(-1, -5));
            race.Tick(4, motions, Vector2.left); Assert.That(race.OwedTurns(0), Is.EqualTo(2));
            motions[0] = YachtRaceFixtures.Motion("racer.player", new Vector2(5, 10));
            race.Tick(5, motions, Vector2.left);
            Assert.That(race.Entrant(0).Started, Is.False);
        }

        internal static SailRaceSession Pair() => new SailRaceSession(SailRaceFixtures.Course(), YachtRaceFixtures.Club, 0,
            new[] { YachtRaceFixtures.Entry(p: new Vector2(1, -10)), YachtRaceFixtures.Entry("racer.other", new Vector2(-1, -10)) });

        [Test]
        public void PenaltyCostsElapsedTimeAndCompletionSegmentCannotCollectStart()
        {
            var race = Pair();
            var motions = new[] { YachtRaceFixtures.Motion("racer.player", new Vector2(1, -5)),
                YachtRaceFixtures.Motion("racer.other", new Vector2(-1, -5)) };
            race.Tick(1, motions, Vector2.left);
            motions[1] = YachtRaceFixtures.Motion("racer.other", new Vector2(-30, -5));
            for (int i = 1; i <= 4; i++)
            {
                float angle = i * 90 * Mathf.Deg2Rad;
                motions[0] = YachtRaceFixtures.Motion("racer.player", new Vector2(5, i == 4 ? 10 : -5),
                    new Vector2(Mathf.Sin(angle), Mathf.Cos(angle)));
                race.Tick(1 + i * 2, motions, Vector2.left);
            }
            Assert.That(race.OwedTurns(0), Is.EqualTo(0)); Assert.That(race.Entrant(0).Started, Is.False);
            motions[0] = YachtRaceFixtures.Motion("racer.player", new Vector2(5, -10)); race.Tick(10, motions, Vector2.left);
            // Finish the same geometric track, ten seconds later than the unpenalized entrant.
            var reference = YachtRaceFixtures.Session(); YachtRaceFixtures.RoundAndFinish(reference);
            void Tick(double time, Vector2 p) { motions[0] = YachtRaceFixtures.Motion("racer.player", p); race.Tick(time, motions, Vector2.left); }
            Tick(20, new Vector2(5, 10)); Tick(30, new Vector2(5, 19)); Tick(31, new Vector2(5, 21));
            Tick(32, new Vector2(3, 24)); Tick(33, new Vector2(-1, 25)); Tick(40, new Vector2(-1, 0));
            Assert.That(race.Entrant(0).ElapsedSeconds - reference.Entrant(0).ElapsedSeconds, Is.EqualTo(10).Within(0.00001));
            Assert.That(race.Entrant(0).PenaltySeconds, Is.EqualTo(0), "A sailed turn adds no synthetic seconds.");
        }

        [Test]
        public void InvalidRosterOrSampleRejectsWholeTickWithoutMutation()
        {
            var race = Pair();
            Assert.Throws<ArgumentException>(() => race.Tick(1, new[] { YachtRaceFixtures.Motion("racer.player", Vector2.zero), default(RaceBoatMotion) }, Vector2.left));
            Assert.That(race.Entrant(0).LastSample.Time, Is.EqualTo(0)); Assert.That(race.OwedTurns(0), Is.EqualTo(0));
            Assert.Throws<ArgumentException>(() => new SailRaceSession(SailRaceFixtures.Course(), YachtRaceFixtures.Club, 0,
                new[] { YachtRaceFixtures.Entry(), YachtRaceFixtures.Entry() }));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SailRaceTurn(default));
        }

        [Test]
        public void OwedTurnBlocksFinishUntilTurnAndNewLegalCrossingAreComplete()
        {
            var race = Pair();
            var motions = new[] { YachtRaceFixtures.Motion("racer.player", new Vector2(5, 10)),
                YachtRaceFixtures.Motion("racer.other", new Vector2(50, -10)) };
            void Tick(double time, Vector2 position, float heading = 0)
            {
                motions[0] = YachtRaceFixtures.Motion("racer.player", position,
                    new Vector2(Mathf.Sin(heading * Mathf.Deg2Rad), Mathf.Cos(heading * Mathf.Deg2Rad)));
                race.Tick(time, motions, Vector2.left);
            }
            Tick(10, new Vector2(5, 10)); Tick(20, new Vector2(5, 19)); Tick(21, new Vector2(5, 21));
            Tick(22, new Vector2(3, 24)); Tick(23, new Vector2(-1, 25));
            Assert.That(race.Entrant(0).NextMarkIndex, Is.EqualTo(1));
            motions[1] = YachtRaceFixtures.Motion("racer.other", new Vector2(-3, 10));
            Tick(24, new Vector2(-1, 10)); Assert.That(race.OwedTurns(0), Is.EqualTo(1));
            motions[1] = YachtRaceFixtures.Motion("racer.other", new Vector2(-30, 10));
            Tick(25, new Vector2(-1, 0)); Assert.That(race.Entrant(0).Finished, Is.False);
            Tick(26, new Vector2(-1, 0), 90); Tick(28, new Vector2(-1, 0), 180);
            Tick(30, new Vector2(-1, 0), 270); Tick(32, new Vector2(-1, 0), 360);
            Assert.That(race.OwedTurns(0), Is.EqualTo(0)); Assert.That(race.Entrant(0).Finished, Is.False);
            Tick(33, new Vector2(-1, 10)); Tick(34, new Vector2(-1, 0));
            Assert.That(race.Entrant(0).FinishTime, Is.EqualTo(33.5).Within(0.00001));
        }

        [Test]
        public void BeforeGunContactIsNotChargedAndInvalidTurnSampleIsAtomic()
        {
            var entries = new[] { new SailRaceEntry("racer.player", YachtRaceFixtures.Rating(), new RaceTrackSample(-10, Vector2.right), Vector2.up, 4),
                new SailRaceEntry("racer.other", YachtRaceFixtures.Rating(), new RaceTrackSample(-10, Vector2.left), Vector2.up, 4) };
            var race = new SailRaceSession(SailRaceFixtures.Course(), YachtRaceFixtures.Club, 0, entries);
            var motions = new[] { YachtRaceFixtures.Motion("racer.player", Vector2.right), YachtRaceFixtures.Motion("racer.other", Vector2.left) };
            race.Tick(-1, motions, Vector2.left); Assert.That(race.OwedTurns(0), Is.EqualTo(0));
            race.Tick(0, motions, Vector2.left); Assert.That(race.OwedTurns(0), Is.EqualTo(1));
            motions[0] = YachtRaceFixtures.Motion("racer.player", Vector2.right, Vector2.down);
            Assert.Throws<ArgumentException>(() => race.Tick(1, motions, Vector2.left));
            Assert.That(race.Entrant(0).LastSample.Time, Is.EqualTo(0)); Assert.That(race.Entrant(1).LastSample.Time, Is.EqualTo(0));
            motions[0] = YachtRaceFixtures.Motion("racer.player", Vector2.right, Vector2.right);
            race.Tick(1, motions, Vector2.left); Assert.That(race.OwedTurns(0), Is.EqualTo(1));
        }

        [Test]
        public void HandicapReordersAKnownFinishAndUsesOrdinalTieBreak()
        {
            var results = SailRaceResults.Order(new[] { YachtRaceFixtures.Finish("racer.fast", 100, 1.2f), YachtRaceFixtures.Finish("racer.slow", 110) });
            Assert.That(results[0].RacerId, Is.EqualTo("racer.slow"));
            Assert.That(results[1].CorrectedSeconds, Is.EqualTo(120).Within(0.00001));
            Assert.That(results[1].ElapsedSeconds, Is.EqualTo(100));
            var ties = SailRaceResults.Order(new[] { YachtRaceFixtures.Finish("racer.b", 100), YachtRaceFixtures.Finish("racer.a", 100),
                new SailRaceResult("racer.retired", YachtRaceFixtures.Rating(), SailRaceStatus.Retired, double.NaN) });
            Assert.That(ties[0].RacerId, Is.EqualTo("racer.a")); Assert.That(ties[2].RacerId, Is.EqualTo("racer.retired"));
        }

        [Test]
        public void HandicapAndResultValidationRejectBadRatingsAndUnfinishedTimes()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => YachtRaceFixtures.Rating(multiplier: 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => YachtRaceFixtures.Rating(multiplier: float.NaN));
            Assert.Throws<ArgumentException>(() => new SailRaceResult("racer.a", YachtRaceFixtures.Rating(), SailRaceStatus.Retired, 0));
            Assert.Throws<ArgumentException>(() => SailRaceResults.Order(new[] { YachtRaceFixtures.Finish("racer.a", 100), YachtRaceFixtures.Finish("racer.a", 110) }));
            var legacy = new SailRaceResult("racer.a", YachtRaceFixtures.Rating(multiplier: 2), SailRaceStatus.Finished, 100, 15);
            Assert.That(legacy.CorrectedSeconds, Is.EqualTo(230));
        }

        [Test]
        public void SeasonUsesCompleteRacesLowPointsAndStableTies()
        {
            var races = new[] { "race.one", "race.two" }; var points = new[] { 1, 2 };
            var season = new SailRaceSeason(races, new[] { "racer.b", "racer.a" }, points, 3);
            races[0] = "race.changed"; points[0] = 99;
            season.Record("race.one", new[] { YachtRaceFixtures.Finish("racer.a", 100), YachtRaceFixtures.Finish("racer.b", 110) });
            season.Record("race.two", new[] { YachtRaceFixtures.Finish("racer.b", 100), YachtRaceFixtures.Finish("racer.a", 110) });
            var table = season.Table(); Assert.That(table[0].RacerId, Is.EqualTo("racer.a"));
            Assert.That(table[0].Points, Is.EqualTo(3)); Assert.That(table[1].Points, Is.EqualTo(3));
            Assert.That(table[0].CompletedRaces, Is.EqualTo(2));
        }

        [Test]
        public void SeasonRejectsDuplicateIncompleteForeignOrUnfinishedResultsWithoutMutation()
        {
            var season = new SailRaceSeason(new[] { "race.one", "race.two" }, new[] { "racer.a", "racer.b" }, new[] { 1, 2 }, 3);
            var a = YachtRaceFixtures.Finish("racer.a", 100); var b = YachtRaceFixtures.Finish("racer.b", 110);
            Assert.Throws<ArgumentException>(() => season.Record("race.two", new[] { a, b }));
            Assert.Throws<ArgumentException>(() => season.Record("race.one", new[] { a }));
            Assert.Throws<ArgumentException>(() => season.Record("race.one", new[] { a, a }));
            Assert.Throws<ArgumentException>(() => season.Record("race.one", new[] { a, YachtRaceFixtures.Finish("racer.c", 110) }));
            Assert.Throws<ArgumentException>(() => season.Record("race.one", new[] { a,
                new SailRaceResult("racer.b", YachtRaceFixtures.Rating(), SailRaceStatus.Waiting, double.NaN) }));
            Assert.That(season.CompletedRaces, Is.EqualTo(0)); Assert.That(season.Table()[0].Points, Is.EqualTo(0));
            season.Record("race.one", new[] { a, new SailRaceResult("racer.b", YachtRaceFixtures.Rating(), SailRaceStatus.Retired, double.NaN) });
            Assert.That(season.Table()[1].Points, Is.EqualTo(3));
            Assert.Throws<ArgumentException>(() => season.Record("race.one", new[] { a, b }));
        }

        [Test]
        public void ExistingCourseDefBuildsClubDataSnapshotsWithoutSceneOrHullEdits()
        {
            var course = ScriptableObject.CreateInstance<SailRaceCourseDef>();
            try
            {
                var source = SailRaceFixtures.Course(); course.Start = source.Start; course.Finish = source.Finish;
                course.Marks = new[] { SailRaceFixtures.Mark }; course.Laps = 1; course.Rules = source.Rules;
                course.ClubRules = YachtRaceFixtures.Club; course.ClubHandicaps = new[] { YachtRaceFixtures.Rating() };
                var session = course.BuildClubSession(0, new[] { YachtRaceFixtures.Entry() });
                course.ClubRules.TurnDegrees = 720; course.ClubHandicaps[0].TimeMultiplier = 2;
                YachtRaceFixtures.RoundAndFinish(session);
                Assert.That(session.Results()[0].TimeMultiplier, Is.EqualTo(1));
                course.ClubPlacePoints = new[] { 1 }; course.ClubNonFinisherPoints = 2;
                var season = course.BuildClubSeason(new[] { "race.one" }, new[] { "racer.player" });
                course.ClubPlacePoints[0] = 99; season.Record("race.one", session.Results());
                Assert.That(season.Table()[0].Points, Is.EqualTo(1));
                Assert.Throws<ArgumentException>(() => course.BuildClubSession(0, new[] { YachtRaceFixtures.Entry() }));
                Assert.Throws<ArgumentException>(() => course.HandicapFor("boat.missing"));
                course.ClubHandicaps = new[] { YachtRaceFixtures.Rating(), YachtRaceFixtures.Rating() };
                Assert.Throws<ArgumentException>(() => course.HandicapFor("boat.fixture"));
                Assert.Throws<ArgumentException>(() => new SailRaceClubRules(180, 8, 100));
                Assert.Throws<ArgumentOutOfRangeException>(() => new SailRaceSkipperRules(float.NaN, 1, 2));
            }
            finally { UnityEngine.Object.DestroyImmediate(course); }
        }
    }
}
