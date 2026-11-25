using System;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using HiddenHarbours.Environment;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    internal sealed class YachtRaceWind : IRaceWindSource
    {
        public Vector2 Value = Vector2.down * SailDrive.ToMetresPerSecond(10);
        public int Calls, LastSeed;
        public double LastTime;
        public bool Variable;
        public Vector2 WindAt(int worldSeed, double gameTime)
        {
            Calls++; LastSeed = worldSeed; LastTime = gameTime;
            if (!Variable) return Value;
            // The actual main weather model, through the same Core contract as a future App adapter.
            return WeatherModel.SampleWind(gameTime, worldSeed, 75, WindProfile.CoddleCove);
        }
    }

    public class YachtRaceSkipperTests
    {
        private SailPolarDef _polar;
        [SetUp]
        public void SetUp()
        {
            _polar = ScriptableObject.CreateInstance<SailPolarDef>();
            _polar.TrueWindKn = new[] { 0f, 10f };
            _polar.TrueWindAngleDeg = new[] { 0f, 90f, 180f };
            _polar.BoatSpeedKn = new[] { 0f, 2f, 0f, 2f, 0f, 2f };
        }
        [TearDown]
        public void TearDown() => UnityEngine.Object.DestroyImmediate(_polar);

        internal static SailRaceNpc Model(SailPolarDef polar, int seed = 17, double gun = 100, float min = 1, float max = 1,
            Vector2? start = null, int laps = 1)
            => new SailRaceNpc("racer.fixture", SailRaceFixtures.Course(laps), seed, gun, polar, 45,
                start ?? new Vector2(0, -10), 0, 0.25f, 4000, 72, min, max, 2, 0.5f, 15, 10);
        internal static SailRaceSkipperRules Rules => new SailRaceSkipperRules(10, 0.1f, 1);

        [Test]
        public void SkipperTacksUpwindAndNeverSailsTheDeadZone()
        {
            var wind = new YachtRaceWind(); var skipper = Model(_polar).CreateSkipper(wind, Rules);
            float highestY = skipper.Motion.Position.y;
            bool portBeforeStart = false, starboardBeforeStart = false;
            for (int i = 0; i < 1000; i++)
            {
                skipper.Tick(); var motion = skipper.Motion;
                if (motion.Velocity.sqrMagnitude > 0f)
                {
                    Assert.That(Mathf.Abs(SailDrive.ResolveWind(wind.Value, motion.Forward, motion.Velocity).TrueAngleDeg),
                        Is.GreaterThanOrEqualTo(45), "Moving heading must remain outside true-wind no-go.");
                    if (!skipper.Race.Started)
                    {
                        float tack = SailDrive.ResolveWind(wind.Value, motion.Forward, motion.Velocity).TrueAngleDeg;
                        portBeforeStart |= tack < 0; starboardBeforeStart |= tack > 0;
                    }
                }
                highestY = Mathf.Max(highestY, motion.Position.y);
            }
            Assert.That(skipper.TackCount, Is.GreaterThan(0));
            Assert.That(portBeforeStart && starboardBeforeStart, Is.True, "Both tacks are needed on the first upwind approach.");
            Assert.That(highestY, Is.GreaterThan(20));
            Assert.That(skipper.Race.Finished, Is.True);
        }

        [Test]
        public void HeaderChoosesOtherTackAndReadsWindAtAbsoluteTickTime()
        {
            var wind = new YachtRaceWind(); var skipper = Model(_polar, seed: 81).CreateSkipper(wind, Rules);
            for (int i = 0; i < 8; i++) skipper.Tick();
            float heading = BoatKinematics.BearingDegrees(skipper.Motion.Forward);
            // Shift the wind FROM toward the established bow by twenty degrees, a header.
            float shift = heading > 180 ? -20 : 20;
            wind.Value = -new Vector2(Mathf.Sin(shift * Mathf.Deg2Rad), Mathf.Cos(shift * Mathf.Deg2Rad)) * SailDrive.ToMetresPerSecond(10);
            skipper.Tick();
            Assert.That(skipper.HeaderTackCount, Is.EqualTo(1));
            Assert.That(wind.LastSeed, Is.EqualTo(81)); Assert.That(wind.LastTime, Is.EqualTo(102));
            Assert.That(wind.Calls, Is.EqualTo(9));
            Assert.That(Mathf.Abs(SailDrive.ResolveWind(wind.Value, skipper.Motion.Forward, Vector2.zero).TrueAngleDeg), Is.GreaterThanOrEqualTo(45));
        }

        [Test]
        public void GradualHeaderAccumulatesSinceLastTackRatherThanResettingEveryTick()
        {
            var wind = new YachtRaceWind(); var skipper = Model(_polar).CreateSkipper(wind, Rules);
            for (int i = 0; i < 8; i++) skipper.Tick();
            float sign = BoatKinematics.BearingDegrees(skipper.Motion.Forward) > 180 ? -1 : 1;
            for (int i = 1; i <= 3; i++)
            {
                float shift = sign * 4 * i * Mathf.Deg2Rad;
                wind.Value = -new Vector2(Mathf.Sin(shift), Mathf.Cos(shift)) * SailDrive.ToMetresPerSecond(10);
                skipper.Tick();
                Assert.That(skipper.HeaderTackCount, Is.EqualTo(i == 3 ? 1 : 0));
            }
        }

        [Test]
        public void ExistingNpcDefBuildsSkipperWithAuthoredRulesAndHullPolar()
        {
            var hull = ScriptableObject.CreateInstance<BoatHullDef>(); var def = ScriptableObject.CreateInstance<SailRaceNpcDef>();
            try
            {
                hull.Propulsion = PropulsionType.Sail; hull.SailPolar = _polar; hull.NoGoTrueWindDeg = 45;
                def.Hull = hull; def.StartPosition = new Vector2(0, -10); def.FixedStepSeconds = 0.25f;
                def.MaximumReplaySeconds = 4000; def.HeadingSamples = 72; def.MinimumSpeedFraction = def.MaximumSpeedFraction = 1;
                def.GateClearanceMetres = 2; def.RoundingLaneFraction = 0.5f; def.RoundingStepDeg = 15; def.GateOvershootDeg = 10;
                def.ClubSkipper = Rules;
                var skipper = def.BuildSkipper("racer.fixture", SailRaceFixtures.Course(), 17, 100, new YachtRaceWind());
                for (int i = 0; i < 2000; i++) skipper.Tick();
                Assert.That(skipper.Race.Finished, Is.True); Assert.That(skipper.TackCount, Is.GreaterThan(0));
                Assert.That(hull.NoGoTrueWindDeg, Is.EqualTo(45)); Assert.That(hull.SailPolar, Is.SameAs(_polar));
                def.ClubSkipper.HeaderDegrees = 0;
                Assert.Throws<ArgumentOutOfRangeException>(() => def.BuildSkipper("racer.fixture", SailRaceFixtures.Course(), 17, 100, new YachtRaceWind()));
            }
            finally { UnityEngine.Object.DestroyImmediate(hull); UnityEngine.Object.DestroyImmediate(def); }
        }

        [Test]
        public void CalmMakesNoProgressAndInvalidWindDoesNotMutateTick()
        {
            var wind = new YachtRaceWind { Value = Vector2.zero }; var skipper = Model(_polar).CreateSkipper(wind, Rules);
            for (int i = 0; i < 40; i++) skipper.Tick();
            Assert.That(skipper.Motion.Position, Is.EqualTo(new Vector2(0, -10)));
            Assert.That(skipper.Race.Started, Is.False); Assert.That(skipper.Motion.Velocity, Is.EqualTo(Vector2.zero));
            double before = skipper.Time;
            wind.Value = new Vector2(float.NaN, 0);
            Assert.Throws<ArgumentOutOfRangeException>(() => skipper.Tick());
            Assert.That(skipper.Time, Is.EqualTo(before)); Assert.That(skipper.Motion.Position, Is.EqualTo(new Vector2(0, -10)));
        }

        [Test]
        public void DeterminismFromSeedAndTime_UsesMainWeatherThroughCoreInterface()
        {
            var a = Model(_polar, min: 0.6f, max: 1).CreateSkipper(new YachtRaceWind { Variable = true }, Rules);
            var b = Model(_polar, min: 0.6f, max: 1).CreateSkipper(new YachtRaceWind { Variable = true }, Rules);
            for (int i = 0; i < 400; i++)
            {
                a.Tick(); b.Tick();
                Assert.That(a.Time, Is.EqualTo(b.Time)); Assert.That(a.Motion.Position, Is.EqualTo(b.Motion.Position));
                Assert.That(a.Motion.Velocity, Is.EqualTo(b.Motion.Velocity));
                Assert.That(a.Race.NextMarkIndex, Is.EqualTo(b.Race.NextMarkIndex));
                Assert.That(a.TackCount, Is.EqualTo(b.TackCount));
            }
            var different = Model(_polar, seed: 18, min: 0.6f, max: 1).CreateSkipper(new YachtRaceWind { Variable = true }, Rules);
            for (int i = 0; i < 400; i++) different.Tick();
            Assert.That(different.Motion.Position, Is.Not.EqualTo(a.Motion.Position));
        }

        [Test]
        public void FinishedSkipperFreezesWithoutReadingMoreWind()
        {
            var wind = new YachtRaceWind { Value = Vector2.left * SailDrive.ToMetresPerSecond(10) };
            var skipper = Model(_polar).CreateSkipper(wind, Rules);
            for (int i = 0; i < 4000 && !skipper.Race.Finished; i++) skipper.Tick();
            Assert.That(skipper.Race.Finished, Is.True);
            var last = skipper.Motion; int calls = wind.Calls; double time = skipper.Time;
            for (int i = 0; i < 10; i++) skipper.Tick();
            Assert.That(skipper.Motion.Position, Is.EqualTo(last.Position)); Assert.That(skipper.Time, Is.EqualTo(time));
            Assert.That(wind.Calls, Is.EqualTo(calls)); Assert.That(skipper.Motion.Velocity, Is.EqualTo(Vector2.zero));
        }

        [TestCase("Assets/_Project/Data/Boats/SailPolars/SloopIsoRig.asset")]
        [TestCase("Assets/_Project/Data/Boats/SailPolars/Sloop88IsoRig.asset")]
        public void BothShippedSloopPolarsFinishUnderEightWindBearingsWithoutRetuning(string path)
        {
            var polar = AssetDatabase.LoadAssetAtPath<SailPolarDef>(path); Assert.That(polar, Is.Not.Null);
            for (int bearing = 0; bearing < 360; bearing += 45)
            {
                var wind = new YachtRaceWind { Value = new Vector2(Mathf.Sin(bearing * Mathf.Deg2Rad), Mathf.Cos(bearing * Mathf.Deg2Rad)) * SailDrive.ToMetresPerSecond(10) };
                var skipper = Model(polar).CreateSkipper(wind, Rules);
                for (int i = 0; i < 16000 && !skipper.Race.Finished; i++)
                {
                    skipper.Tick();
                    if (skipper.Motion.Velocity.sqrMagnitude > 0)
                        Assert.That(Mathf.Abs(SailDrive.ResolveWind(wind.Value, skipper.Motion.Forward, Vector2.zero).TrueAngleDeg),
                            Is.GreaterThanOrEqualTo(45), "wind bearing=" + bearing);
                }
                Assert.That(skipper.Race.CompletedLaps, Is.EqualTo(1), "wind bearing=" + bearing);
                Assert.That(skipper.Race.Finished, Is.True, "wind bearing=" + bearing);
            }
        }

        [Test]
        public void SkipperRepeatsCourseForTwoLaps()
        {
            var skipper = Model(_polar, laps: 2).CreateSkipper(new YachtRaceWind(), Rules);
            for (int i = 0; i < 8000 && !skipper.Race.Finished; i++) skipper.Tick();
            Assert.That(skipper.Race.CompletedLaps, Is.EqualTo(2));
            Assert.That(skipper.Race.LapElapsedSeconds(0) + skipper.Race.LapElapsedSeconds(1), Is.EqualTo(skipper.Race.ElapsedSeconds).Within(0.00001));
        }
    }
}
