using System;
using HiddenHarbours.Boats;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    public class SailRaceTrackTests
    {
        [Test]
        public void Start_InterpolatesCrossingAndNeverRoundsBeforeStarting()
        {
            var race = SailRaceFixtures.Entrant();
            race.Advance(new RaceTrackSample(10, new Vector2(5, 10)));
            Assert.That(race.Started, Is.True);
            Assert.That(race.StartTime, Is.EqualTo(0).Within(0.00001));
            Assert.That(race.NextMarkIndex, Is.EqualTo(0));
        }

        [Test]
        public void OverEarly_RequiresReturnThenLegalRecrossing()
        {
            var race = SailRaceFixtures.Entrant();
            race.Advance(new RaceTrackSample(-2, new Vector2(5, 5)));
            race.Advance(new RaceTrackSample(2, new Vector2(5, 10)));
            Assert.That(race.OverEarly, Is.True);
            SailRaceFixtures.Round(race, 10);
            Assert.That(race.Started, Is.False);
            Assert.That(race.NextMarkIndex, Is.EqualTo(0));
            race.Advance(new RaceTrackSample(20, new Vector2(15, -5)));
            Assert.That(race.OverEarly, Is.False, "Returning around an end is allowed.");
            race.Advance(new RaceTrackSample(22, new Vector2(5, -5)));
            race.Advance(new RaceTrackSample(24, new Vector2(5, 5)));
            Assert.That(race.StartTime, Is.EqualTo(23).Within(0.00001));
        }

        [Test]
        public void EarlyExcursionBackBeforeGun_DoesNotRecall()
        {
            var race = SailRaceFixtures.Entrant();
            race.Advance(new RaceTrackSample(-8, new Vector2(5, 5)));
            race.Advance(new RaceTrackSample(-6, new Vector2(5, -15)));
            race.Advance(new RaceTrackSample(2, new Vector2(5, 5)));
            Assert.That(race.OverEarly, Is.False);
            Assert.That(race.Started, Is.True);
            Assert.That(race.StartTime, Is.EqualTo(0).Within(0.00001));
        }

        [TestCase(10, true)]
        [TestCase(11, false)]
        public void Start_RequiresFiniteLineIncludingItsEndpoints(float x, bool expected)
        {
            var race = new SailRaceEntrant("racer.player", SailRaceFixtures.Course(), 0,
                new RaceTrackSample(-1, new Vector2(x, -1)));
            race.Advance(new RaceTrackSample(1, new Vector2(x, 1)));
            Assert.That(race.Started, Is.EqualTo(expected));
        }

        [Test]
        public void Start_PointOnLineAtGunCountsOnceAfterPositiveEvidence()
        {
            var race = SailRaceFixtures.Entrant();
            race.Advance(new RaceTrackSample(0, new Vector2(5, 0)));
            Assert.That(race.Started, Is.False);
            race.Advance(new RaceTrackSample(1, new Vector2(5, 1)));
            Assert.That(race.StartTime, Is.EqualTo(0));
            Assert.That(race.OverEarly, Is.False);
        }

        [Test]
        public void MissedMarkAndWrongWayFinish_DoNotFinish()
        {
            var race = SailRaceFixtures.Entrant();
            race.Advance(new RaceTrackSample(10, new Vector2(5, 10)));
            race.Advance(new RaceTrackSample(20, new Vector2(5, 0)));
            Assert.That(race.Finished, Is.False);
            Assert.That(race.CompletedLaps, Is.EqualTo(0));
            SailRaceFixtures.Round(race, 30);
            Assert.That(race.NextMarkIndex, Is.EqualTo(1));
            race.Advance(new RaceTrackSample(40, new Vector2(15, 0)));
            race.Advance(new RaceTrackSample(41, new Vector2(5, 0)));
            race.Advance(new RaceTrackSample(42, new Vector2(5, 10)));
            Assert.That(race.Finished, Is.False);
            race.Advance(new RaceTrackSample(44, new Vector2(5, 0)));
            Assert.That(race.FinishTime, Is.EqualTo(43).Within(0.00001));
        }

        [Test]
        public void TwoLaps_RequireAllMarksAgainAndUseGunElapsedTime()
        {
            var race = SailRaceFixtures.Entrant(2);
            race.Advance(new RaceTrackSample(10, new Vector2(5, 10)));
            SailRaceFixtures.Round(race, 20);
            race.Advance(new RaceTrackSample(30, new Vector2(-1, 0)));
            Assert.That(race.CompletedLaps, Is.EqualTo(1));
            Assert.That(race.Finished, Is.False);
            Assert.That(race.NextMarkIndex, Is.EqualTo(0));
            race.Advance(new RaceTrackSample(31, new Vector2(-1, 10)));
            race.Advance(new RaceTrackSample(32, new Vector2(-1, 0)));
            Assert.That(race.CompletedLaps, Is.EqualTo(1), "Repeated finishes cannot skip the next lap's marks.");
            SailRaceFixtures.Round(race, 40);
            race.Advance(new RaceTrackSample(50, new Vector2(-1, 0)));
            Assert.That(race.CompletedLaps, Is.EqualTo(2));
            Assert.That(race.FinishTime, Is.EqualTo(48.6).Within(0.00001));
            Assert.That(race.LapElapsedSeconds(0), Is.EqualTo(28.6).Within(0.00001));
            Assert.That(race.LapElapsedSeconds(1), Is.EqualTo(20).Within(0.00001));
            Assert.That(race.CorrectedSeconds, Is.EqualTo(48.6).Within(0.00001));
            race.Advance(new RaceTrackSample(60, new Vector2(-1, -10)));
            Assert.That(race.FinishTime, Is.EqualTo(48.6).Within(0.00001));
        }

        [Test]
        public void Track_RejectedDiscontinuitiesLeaveProgressUntouched()
        {
            var rules = SailRaceFixtures.Rules; rules.MaximumTrackGapSeconds = 20; rules.MaximumTrackSegmentMetres = 20;
            var race = new SailRaceEntrant("racer.player", SailRaceFixtures.Course(rules: rules), 0,
                new RaceTrackSample(-1, new Vector2(5, -5)));
            Assert.Throws<ArgumentException>(() => race.Advance(new RaceTrackSample(-1, Vector2.zero)));
            Assert.Throws<ArgumentException>(() => race.Advance(new RaceTrackSample(30, Vector2.zero)));
            Assert.Throws<ArgumentException>(() => race.Advance(new RaceTrackSample(1, new Vector2(100, 0))));
            Assert.That(race.LastSample.Time, Is.EqualTo(-1));
            Assert.That(race.Started, Is.False);
            Assert.Throws<ArgumentOutOfRangeException>(() => new RaceTrackSample(double.NaN, Vector2.zero));
            Assert.Throws<ArgumentException>(() => new SailRaceEntrant("racer.late", SailRaceFixtures.Course(), 0,
                new RaceTrackSample(1, Vector2.zero)));
        }

        [Test]
        public void OrderedMarks_CannotBeCollectedInReverseOrder()
        {
            var first = SailRaceFixtures.Mark; first.Id = "racemark.first"; first.Position = new Vector2(0, 50);
            var race = new SailRaceEntrant("racer.player", SailRaceFixtures.Course(marks: new[] { first, SailRaceFixtures.Mark }),
                0, new RaceTrackSample(-1, new Vector2(5, -1)));
            race.Advance(new RaceTrackSample(1, new Vector2(5, 1)));
            SailRaceFixtures.Round(race, 10);
            Assert.That(race.NextMarkIndex, Is.EqualTo(0));
        }

        [Test]
        public void SubdividingTheSameTrack_PreservesStartMarkAndFinishTimes()
        {
            var coarse = SailRaceFixtures.Entrant(); var fine = SailRaceFixtures.Entrant();
            var samples = new[] { new RaceTrackSample(10, new Vector2(5, 10)), new RaceTrackSample(20, new Vector2(5, 19)),
                new RaceTrackSample(21, new Vector2(5, 21)), new RaceTrackSample(22, new Vector2(3, 24)),
                new RaceTrackSample(23, new Vector2(-1, 25)), new RaceTrackSample(30, new Vector2(-1, 0)) };
            foreach (var sample in samples)
            {
                var previous = fine.LastSample;
                fine.Advance(new RaceTrackSample((previous.Time + sample.Time) / 2, (previous.Position + sample.Position) / 2));
                fine.Advance(sample); coarse.Advance(sample);
            }
            Assert.That(fine.StartTime, Is.EqualTo(coarse.StartTime).Within(0.00001));
            Assert.That(fine.NextMarkIndex, Is.EqualTo(1));
            Assert.That(fine.FinishTime, Is.EqualTo(coarse.FinishTime).Within(0.00001));
        }
    }
}
