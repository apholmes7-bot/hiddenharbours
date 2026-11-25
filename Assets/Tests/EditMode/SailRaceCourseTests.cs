using System;
using HiddenHarbours.Boats;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    internal static class SailRaceFixtures
    {
        public static SailRaceRules Rules => new SailRaceRules
        {
            WarningSeconds = 300, PreparatorySeconds = 60, MaximumTrackGapSeconds = 400,
            MaximumTrackSegmentMetres = 1000, ContactDistanceMetres = 3, ContactReleaseMetres = 5,
            OvertakingConeHalfDeg = 45, CalmWindMetresPerSecond = 0.1f, RightOfWayPenaltySeconds = 15
        };

        // East radial entry -> north radial exit, anticlockwise, with buoy kept to port.
        public static RaceMark Mark => new RaceMark
        {
            Id = "racemark.windward", Position = new Vector2(0, 20), Side = RaceRoundingSide.Port,
            EntryBearingDeg = 90, ExitBearingDeg = 0, ClearanceMetres = 1, ZoneRadiusMetres = 10
        };

        public static SailRaceCourse Course(int laps = 1, RaceMark[] marks = null, SailRaceRules? rules = null)
            => new SailRaceCourse("racecourse.test", new RaceLine(new Vector2(-10, 0), new Vector2(10, 0)),
                marks ?? new[] { Mark }, new RaceLine(new Vector2(10, 5), new Vector2(-10, 5)), laps, rules ?? Rules);

        public static SailRaceEntrant Entrant(int laps = 1)
            => new SailRaceEntrant("racer.player", Course(laps), 0, new RaceTrackSample(-10, new Vector2(5, -10)));

        public static void Round(SailRaceEntrant race, double time)
        {
            race.Advance(new RaceTrackSample(time, new Vector2(5, 19)));
            race.Advance(new RaceTrackSample(time + 1, new Vector2(5, 21)));
            race.Advance(new RaceTrackSample(time + 2, new Vector2(3, 24)));
            race.Advance(new RaceTrackSample(time + 3, new Vector2(-1, 25)));
        }
    }

    public class SailRaceCourseTests
    {
        [TestCase(-301, SailRacePhase.BeforeWarning)]
        [TestCase(-300, SailRacePhase.Warning)]
        [TestCase(-60, SailRacePhase.Preparatory)]
        [TestCase(0, SailRacePhase.Racing)]
        public void Sequence_UsesAuthoredBoundaries(double time, SailRacePhase expected)
            => Assert.That(SailRaceFixtures.Course().PhaseAt(time, 0), Is.EqualTo(expected));

        [Test]
        public void Course_SnapshotsItsMarks()
        {
            var marks = new[] { SailRaceFixtures.Mark };
            var course = SailRaceFixtures.Course(marks: marks);
            marks[0].Position = Vector2.zero;
            Assert.That(course.Mark(0).Position, Is.EqualTo(new Vector2(0, 20)));
        }

        [Test]
        public void Course_RejectsDuplicateIdentityAndEmptyOrDegenerateGeometry()
        {
            var mark = SailRaceFixtures.Mark;
            Assert.Throws<ArgumentException>(() => SailRaceFixtures.Course(marks: Array.Empty<RaceMark>()));
            Assert.Throws<ArgumentException>(() => SailRaceFixtures.Course(marks: new[] { mark, mark }));
            mark.ExitBearingDeg = mark.EntryBearingDeg;
            Assert.Throws<ArgumentException>(() => SailRaceFixtures.Course(marks: new[] { mark }));
            mark = SailRaceFixtures.Mark; mark.ClearanceMetres = mark.ZoneRadiusMetres;
            Assert.Throws<ArgumentException>(() => SailRaceFixtures.Course(marks: new[] { mark }));
            Assert.Throws<ArgumentException>(() => new SailRaceCourse("racecourse.test", default,
                new[] { SailRaceFixtures.Mark }, default, 1, SailRaceFixtures.Rules));
        }

        [Test]
        public void Rules_RejectNonFiniteNegativeAndReversedThresholds()
        {
            var rules = SailRaceFixtures.Rules; rules.ContactReleaseMetres = 2;
            Assert.Throws<ArgumentException>(() => rules.Validate());
            rules = SailRaceFixtures.Rules; rules.WarningSeconds = float.NaN;
            Assert.Throws<ArgumentOutOfRangeException>(() => rules.Validate());
            rules = SailRaceFixtures.Rules; rules.RightOfWayPenaltySeconds = -1;
            Assert.Throws<ArgumentOutOfRangeException>(() => rules.Validate());
            rules = SailRaceFixtures.Rules; rules.PreparatorySeconds = 300;
            Assert.Throws<ArgumentException>(() => rules.Validate());
        }

        [Test]
        public void CourseDef_BuildsValidatedDataWithoutAnyScene()
        {
            var def = ScriptableObject.CreateInstance<SailRaceCourseDef>();
            try
            {
                def.Id = "racecourse.fixture"; def.Start = SailRaceFixtures.Course().Start;
                def.Finish = SailRaceFixtures.Course().Finish; def.Marks = new[] { SailRaceFixtures.Mark };
                def.Rules = SailRaceFixtures.Rules; def.Laps = 2;
                Assert.That(def.Build().Laps, Is.EqualTo(2));
                def.Marks[0].Side = (RaceRoundingSide)50;
                Assert.Throws<ArgumentException>(() => def.Build());
            }
            finally { UnityEngine.Object.DestroyImmediate(def); }
        }
    }
}
