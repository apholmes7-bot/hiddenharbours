using System;
using System.Collections.Generic;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    public class SailRaceNpcTests
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

        private SailRaceNpc Npc(int seed = 17, float min = 1, float max = 1, SailPolarDef polar = null, float noGo = 0,
            Vector2? start = null, int laps = 1, double gun = 0)
            => new SailRaceNpc("racer.fixture", SailRaceFixtures.Course(laps), seed, gun, polar ?? _polar, noGo,
                start ?? new Vector2(5, -5), 0, 0.25f, 1000, 72, min, max, 2, 0.5f, 15, 10);

        [Test]
        public void StartOutsideLineSpan_ApproachesTheLineBeforeRacing()
        {
            var sample = Npc(start: new Vector2(40, -1)).At(200, Wind);
            Assert.That(sample.Race.Started, Is.True);
            Assert.That(sample.Race.Finished, Is.True);
        }

        private static Vector2 Wind(int seed, double time) => Vector2.left * SailDrive.ToMetresPerSecond(10);

        [Test]
        public void ConstantPolar_RacerCrossesStartRoundsMarkAndFinishes()
        {
            var sample = Npc().At(200, Wind);
            Assert.That(sample.Race.Started, Is.True);
            Assert.That(sample.Race.NextMarkIndex, Is.EqualTo(1));
            Assert.That(sample.Race.CompletedLaps, Is.EqualTo(1));
            Assert.That(sample.Race.Finished, Is.True);
            Assert.That(sample.Race.FinishTime, Is.GreaterThan(sample.Race.StartTime));
        }

        [Test]
        public void TwoLapNpc_RepeatsMarksAndFinishWithoutReplayingStart()
        {
            var sample = Npc(laps: 2).At(300, Wind);
            Assert.That(sample.Race.CompletedLaps, Is.EqualTo(2));
            Assert.That(sample.Race.Finished, Is.True);
            Assert.That(sample.Race.LapElapsedSeconds(0) + sample.Race.LapElapsedSeconds(1),
                Is.EqualTo(sample.Race.ElapsedSeconds).Within(0.00001));
        }

        [Test]
        public void WindQueries_ReceiveWorldSeedAndAbsoluteFixedTickTimes()
        {
            var times = new List<double>(); var seeds = new List<int>();
            Npc(seed: 81, gun: 100).At(101.125, (seed, time) => { seeds.Add(seed); times.Add(time); return Wind(seed, time); });
            CollectionAssert.AreEqual(new[] { 100d, 100.25, 100.5, 100.75, 101 }, times);
            CollectionAssert.AreEqual(new[] { 81, 81, 81, 81, 81 }, seeds);
        }

        [Test]
        public void Calm_DoesNotTeleportOrAdvanceCourse()
        {
            var sample = Npc().At(200, (seed, time) => Vector2.zero);
            Assert.That(sample.Motion.Position, Is.EqualTo(new Vector2(5, -5)));
            Assert.That(sample.Motion.Velocity, Is.EqualTo(Vector2.zero));
            Assert.That(sample.Race.Started, Is.False);
            Assert.That(sample.Race.CompletedLaps, Is.EqualTo(0));
        }

        [Test]
        public void BeforeGun_HoldsWithoutReadingWind()
        {
            var sample = Npc().At(-1, (seed, time) => throw new Exception("Pre-race wind must not be read."));
            Assert.That(sample.Race.Started, Is.False);
            Assert.That(sample.Motion.Position, Is.EqualTo(new Vector2(5, -5)));
        }

        [Test]
        public void AfterFinish_FreezesAtInterpolatedLineForEveryLaterQuery()
        {
            var npc = Npc(); var complete = npc.At(200, Wind);
            var justAfter = npc.At(complete.Race.FinishTime + 0.001, Wind);
            Assert.That(Vector2.Distance(justAfter.Motion.Position, complete.Motion.Position), Is.LessThan(0.00001));
            Assert.That(complete.Motion.Position.y, Is.EqualTo(5).Within(0.00001));
            Assert.That(complete.Motion.Velocity, Is.EqualTo(Vector2.zero));
            Assert.That(justAfter.Motion.Velocity, Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void QueryOrderAndPartialTicks_AreDeterministicWithClockVaryingWind()
        {
            var npc = Npc();
            Vector2 VariableWind(int seed, double time) => Wind(seed, time) * (0.7f + (float)Math.Sin(time * 0.1) * 0.2f);
            var first = npc.At(37.125, VariableWind);
            npc.At(150, VariableWind); npc.At(2, VariableWind);
            var repeated = npc.At(37.125, VariableWind);
            Assert.That(repeated.Motion.Position, Is.EqualTo(first.Motion.Position));
            Assert.That(repeated.Motion.Velocity, Is.EqualTo(first.Motion.Velocity));
            Assert.That(repeated.Race.StartTime, Is.EqualTo(first.Race.StartTime));
            Assert.That(repeated.Race.NextMarkIndex, Is.EqualTo(first.Race.NextMarkIndex));
            var lo = npc.At(1, Wind); var hi = npc.At(1.25, Wind); var middle = npc.At(1.125, Wind);
            Assert.That(Vector2.Distance(middle.Motion.Position, (lo.Motion.Position + hi.Motion.Position) / 2), Is.LessThan(0.00001f));
        }

        [Test]
        public void SeedAndIdentitySkill_ChangePaceWithoutExceedingPolar()
        {
            var a = Npc(1, 0.5f, 1).At(1, Wind); var b = Npc(2, 0.5f, 1).At(1, Wind);
            Assert.That(a.Motion.Position, Is.Not.EqualTo(b.Motion.Position));
            Assert.That(a.Motion.Velocity.magnitude, Is.LessThanOrEqualTo(SailDrive.ToMetresPerSecond(2)));
            Assert.That(b.Motion.Velocity.magnitude, Is.LessThanOrEqualTo(SailDrive.ToMetresPerSecond(2)));
        }

        [Test]
        public void Speed_EqualsMainSailDriveAndTrueWindNoGoIsRespected()
        {
            var sample = Npc(noGo: 45).At(1, Wind);
            var resolved = SailDrive.ResolveWind(Wind(17, 0), sample.Motion.Forward, Vector2.zero);
            float expected = SailDrive.ToMetresPerSecond(SailDrive.TargetSpeedKn(_polar, resolved.TrueAngleDeg, resolved.TrueKn, 45));
            Assert.That(sample.Motion.Velocity.magnitude, Is.EqualTo(expected).Within(0.00001));
            var upwind = Npc(noGo: 45).At(1, (seed, time) => Vector2.down * SailDrive.ToMetresPerSecond(10));
            var tack = SailDrive.ResolveWind(Vector2.down, upwind.Motion.Forward, Vector2.zero);
            Assert.That(Mathf.Abs(tack.TrueAngleDeg), Is.GreaterThanOrEqualTo(45));
            Assert.That(upwind.Motion.Velocity.y, Is.GreaterThan(0));
        }

        [Test]
        public void InvalidProfilesAndWind_AreRefused()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Npc().At(1001, Wind));
            Assert.Throws<ArgumentOutOfRangeException>(() => Npc().At(1, (seed, time) => new Vector2(float.NaN, 0)));
            Assert.Throws<ArgumentException>(() => Npc(min: 1, max: 0.5f));
            Assert.Throws<ArgumentOutOfRangeException>(() => Npc(noGo: 180));
            _polar.TrueWindKn[1] = 0;
            Assert.Throws<ArgumentException>(() => Npc());
        }

        [Test]
        public void NpcDef_ReusesHullPolarAndNoGoWithoutChangingThem()
        {
            var hull = ScriptableObject.CreateInstance<BoatHullDef>();
            var def = ScriptableObject.CreateInstance<SailRaceNpcDef>();
            try
            {
                hull.Propulsion = PropulsionType.Sail; hull.SailPolar = _polar; hull.NoGoTrueWindDeg = 45;
                def.Id = "racenpc.fixture"; def.Hull = hull; def.StartPosition = new Vector2(5, -5);
                def.FixedStepSeconds = 0.25f; def.MaximumReplaySeconds = 1000; def.HeadingSamples = 72;
                def.MinimumSpeedFraction = 1; def.MaximumSpeedFraction = 1; def.GateClearanceMetres = 2;
                def.RoundingLaneFraction = 0.5f; def.RoundingStepDeg = 15; def.GateOvershootDeg = 10;
                var built = def.Build("racer.fixture", SailRaceFixtures.Course(), 17, 0);
                Assert.That(built.At(1, Wind).Motion.Velocity.magnitude, Is.EqualTo(SailDrive.ToMetresPerSecond(2)).Within(0.00001));
                Assert.That(hull.NoGoTrueWindDeg, Is.EqualTo(45));
                Assert.That(hull.SailPolar, Is.SameAs(_polar));
                hull.Propulsion = PropulsionType.Engine;
                Assert.Throws<ArgumentException>(() => def.Build("racer.fixture", SailRaceFixtures.Course(), 17, 0));
            }
            finally { UnityEngine.Object.DestroyImmediate(def); UnityEngine.Object.DestroyImmediate(hull); }
        }

        [TestCase("Assets/_Project/Data/Boats/SailPolars/SloopIsoRig.asset")]
        [TestCase("Assets/_Project/Data/Boats/SailPolars/Sloop88IsoRig.asset")]
        public void ShippedPolar_IsAcceptedAndDrivesNpcWithoutAssetEdits(string path)
        {
            var polar = AssetDatabase.LoadAssetAtPath<SailPolarDef>(path);
            Assert.That(polar, Is.Not.Null);
            var sample = Npc(polar: polar, noGo: 45).At(1, Wind);
            var resolved = SailDrive.ResolveWind(Wind(17, 0), sample.Motion.Forward, Vector2.zero);
            Assert.That(sample.Motion.Velocity.magnitude, Is.EqualTo(SailDrive.ToMetresPerSecond(
                SailDrive.TargetSpeedKn(polar, resolved.TrueAngleDeg, resolved.TrueKn, 45))).Within(0.00001));
            for (int bearing = 0; bearing < 360; bearing += 45)
            {
                Vector2 wind = new Vector2(Mathf.Sin(bearing * Mathf.Deg2Rad), Mathf.Cos(bearing * Mathf.Deg2Rad))
                    * SailDrive.ToMetresPerSecond(10);
                var finish = Npc(polar: polar, noGo: 45).At(1000, (seed, time) => wind);
                Assert.That(finish.Race.NextMarkIndex, Is.EqualTo(1), "wind bearing=" + bearing);
                Assert.That(finish.Race.CompletedLaps, Is.EqualTo(1), "wind bearing=" + bearing);
                Assert.That(finish.Race.Finished, Is.True, "Shipped polar must finish, wind bearing=" + bearing);
            }
        }
    }
}
