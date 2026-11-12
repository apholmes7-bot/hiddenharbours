using HiddenHarbours.Boats;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    public class SailRaceRoundingTests
    {
        private static RaceMark Local(RaceRoundingSide side)
        { var m = SailRaceFixtures.Mark; m.Position = Vector2.zero; m.Side = side; m.ExitBearingDeg = side == RaceRoundingSide.Port ? 0 : 180; return m; }

        [TestCase(RaceRoundingSide.Port, 1)]
        [TestCase(RaceRoundingSide.Starboard, -1)]
        public void BothSides_RequireAnEntryThenTheAuthoredExit(RaceRoundingSide side, float sign)
        {
            var detector = new SailRaceRounding(); var mark = Local(side);
            Assert.That(detector.Advance(mark, new Vector2(5, -sign), new Vector2(5, sign), out _), Is.False);
            Assert.That(detector.Advance(mark, new Vector2(5, sign), new Vector2(3, 4 * sign), out _), Is.False);
            Assert.That(detector.Advance(mark, new Vector2(3, 4 * sign), new Vector2(-1, 5 * sign), out float t), Is.True);
            Assert.That(t, Is.EqualTo(0.75).Within(0.00001));
        }

        [Test]
        public void ShortcutThroughBuoy_InvalidatesAttemptAndNeedsNewEntry()
        {
            var detector = new SailRaceRounding(); var mark = Local(RaceRoundingSide.Port);
            detector.Advance(mark, new Vector2(5, -1), new Vector2(5, 1), out _);
            Assert.That(detector.Advance(mark, new Vector2(5, 1), new Vector2(-1, 0.1f), out _), Is.False);
            Assert.That(detector.Advance(mark, new Vector2(3, 4), new Vector2(-1, 5), out _), Is.False);
            detector.Advance(mark, new Vector2(5, -1), new Vector2(5, 1), out _);
            Assert.That(detector.Advance(mark, new Vector2(5, 1), new Vector2(-1, 5), out _), Is.True);
        }

        [Test]
        public void WrongDirectionOrLeavingZone_CannotCollectMark()
        {
            var detector = new SailRaceRounding(); var mark = Local(RaceRoundingSide.Port);
            Assert.That(detector.Advance(mark, new Vector2(-1, 5), new Vector2(5, 1), out _), Is.False);
            Assert.That(detector.Advance(mark, new Vector2(5, 1), new Vector2(5, -1), out _), Is.False);
            detector.Advance(mark, new Vector2(5, -1), new Vector2(5, 1), out _);
            detector.Advance(mark, new Vector2(5, 1), new Vector2(20, 3), out _);
            Assert.That(detector.Advance(mark, new Vector2(3, 4), new Vector2(-1, 5), out _), Is.False);
        }

        [Test]
        public void WrapAroundNorth_UsesSmallSignedSweep()
        {
            var mark = Local(RaceRoundingSide.Port); mark.EntryBearingDeg = 10; mark.ExitBearingDeg = 350;
            var detector = new SailRaceRounding();
            Assert.That(mark.SweepDeg, Is.EqualTo(20));
            Assert.That(detector.Advance(mark, new Vector2(2, 5), new Vector2(-2, 5), out float fraction), Is.True);
            Assert.That(fraction, Is.EqualTo(0.7204087).Within(0.0001));
        }

        [Test]
        public void HalfTurn_SupportsWideRoundingWithoutChordThroughBuoy()
        {
            var mark = Local(RaceRoundingSide.Port); mark.ExitBearingDeg = 270;
            var detector = new SailRaceRounding();
            detector.Advance(mark, new Vector2(5, -1), new Vector2(5, 1), out _);
            detector.Advance(mark, new Vector2(5, 1), new Vector2(0, 5), out _);
            Assert.That(detector.Advance(mark, new Vector2(0, 5), new Vector2(-5, -1), out _), Is.True);
        }

        [Test]
        public void TackWithinTheZone_PreservesNetRoundingWithoutCuttingBuoy()
        {
            var mark = Local(RaceRoundingSide.Port); var detector = new SailRaceRounding();
            detector.Advance(mark, new Vector2(5, -1), new Vector2(5, 1), out _);
            detector.Advance(mark, new Vector2(5, 1), new Vector2(3, 4), out _);
            detector.Advance(mark, new Vector2(3, 4), new Vector2(4, 3), out _);
            Assert.That(detector.Advance(mark, new Vector2(4, 3), new Vector2(-1, 5), out _), Is.True);
        }

        [TestCase(RaceRoundingSide.Port)]
        [TestCase(RaceRoundingSide.Starboard)]
        public void TranslatedAndRotatedCourses_PreserveRoundingSideAndCrossingFraction(RaceRoundingSide side)
        {
            float sign = side == RaceRoundingSide.Port ? 1 : -1;
            foreach (float bearing in new[] { 0f, 30f, 90f, 180f, 270f, 330f })
            {
                Vector2 Transform(float x, float y)
                {
                    float cos = Mathf.Cos(bearing * Mathf.Deg2Rad), sin = Mathf.Sin(bearing * Mathf.Deg2Rad);
                    return new Vector2(x * cos + y * sin, -x * sin + y * cos) + new Vector2(123.25f, -76.5f);
                }
                var mark = Local(side); mark.Position = Transform(0, 0);
                mark.EntryBearingDeg += bearing; mark.ExitBearingDeg += bearing;
                var detector = new SailRaceRounding();
                detector.Advance(mark, Transform(5, -sign), Transform(5, sign), out _);
                detector.Advance(mark, Transform(5, sign), Transform(3, 4 * sign), out _);
                Assert.That(detector.Advance(mark, Transform(3, 4 * sign), Transform(-1, 5 * sign), out float fraction),
                    Is.True, "rotation=" + bearing);
                Assert.That(fraction, Is.EqualTo(0.75).Within(0.0001));
            }
        }
    }
}
