using System;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEngine;
#if UNITY_5_3_OR_NEWER
using Unity.Profiling;
#endif

namespace HiddenHarbours.Tests.EditMode
{
    public class YachtRaceAllocationTests
    {
        // The Unity thread byte counter can be a zero-returning stub. Calibrate the current-thread
        // GC.Alloc recorder there; independent source-host runs use the working managed byte counter.
        private sealed class Probe : IDisposable
        {
#if UNITY_5_3_OR_NEWER
            private ProfilerRecorder _recorder = new ProfilerRecorder(ProfilerCategory.Memory, "GC.Alloc", 16384,
                ProfilerRecorderOptions.CollectOnlyOnCurrentThread);
            public Probe() { Assert.That(_recorder.Valid, Is.True); Begin(); End(); }
            public void Begin() { _recorder.Reset(); _recorder.Start(); }
            public long End() { _recorder.Stop(); return _recorder.Count; }
            public void Dispose() => _recorder.Dispose();
#else
            private long _before;
            public void Begin() => _before = GC.GetAllocatedBytesForCurrentThread();
            public long End() => GC.GetAllocatedBytesForCurrentThread() - _before;
            public void Dispose() { }
#endif
            public void Calibrate()
            {
                Begin(); var allocation = new byte[1024]; long positive = End(); GC.KeepAlive(allocation);
                Assert.That(positive, Is.GreaterThan(0), "The same allocation counter must see a known allocation.");
                Begin(); long empty = End(); Assert.That(empty, Is.EqualTo(0));
            }
        }

        [Test]
        public void ZeroAllocationsPerTick_IncludingFirstContactAndTurnCompletion()
        {
            // Warm methods on another instance; the measured session has never seen a contact.
            var warm = YachtRaceClubTests.Pair(); var fresh = YachtRaceClubTests.Pair();
            var motions = new[] { YachtRaceFixtures.Motion("racer.player", new Vector2(1, -5)), YachtRaceFixtures.Motion("racer.other", new Vector2(-1, -5)) };
            using var probe = new Probe(); probe.Calibrate();
            RunTurns(warm, motions);
            motions[0] = YachtRaceFixtures.Motion("racer.player", new Vector2(1, -5));
            motions[1] = YachtRaceFixtures.Motion("racer.other", new Vector2(-1, -5));
            probe.Begin(); RunTurns(fresh, motions); long allocated = probe.End();
            Assert.That(allocated, Is.EqualTo(0)); Assert.That(fresh.OwedTurns(0), Is.EqualTo(0));
        }

        private static void RunTurns(SailRaceSession race, RaceBoatMotion[] motions)
        {
            race.Tick(1, motions, Vector2.left);
            motions[1] = YachtRaceFixtures.Motion("racer.other", new Vector2(-30, -5));
            for (int i = 1; i <= 4; i++)
            {
                float angle = i * 90 * Mathf.Deg2Rad;
                motions[0] = YachtRaceFixtures.Motion("racer.player", new Vector2(1, -5), new Vector2(Mathf.Sin(angle), Mathf.Cos(angle)));
                race.Tick(1 + i * 2, motions, Vector2.left);
            }
        }

        [Test]
        public void ZeroAllocationsPerTick_IncludingFirstLapFinish()
        {
            var warm = YachtRaceFixtures.Session(); var fresh = YachtRaceFixtures.Session();
            var motions = new RaceBoatMotion[1];
            Vector2[] track = { new Vector2(5, 10), new Vector2(5, 19), new Vector2(5, 21), new Vector2(3, 24), new Vector2(-1, 25), new Vector2(-1, 0) };
            double[] times = { 10, 20, 21, 22, 23, 30 };
            void Run(SailRaceSession session)
            {
                for (int i = 0; i < track.Length; i++)
                { motions[0] = YachtRaceFixtures.Motion("racer.player", track[i]); session.Tick(times[i], motions, Vector2.left); }
            }
            using var probe = new Probe(); probe.Calibrate(); Run(warm);
            probe.Begin(); Run(fresh); long allocated = probe.End();
            Assert.That(allocated, Is.EqualTo(0)); Assert.That(fresh.Entrant(0).CompletedLaps, Is.EqualTo(1));
            Assert.That(fresh.Complete, Is.True);
        }

        [Test]
        public void ZeroAllocationsPerTick_IncrementalSkipperRoundingTacksAndFinish()
        {
            var polar = ScriptableObject.CreateInstance<SailPolarDef>();
            try
            {
                polar.TrueWindKn = new[] { 0f, 10f }; polar.TrueWindAngleDeg = new[] { 0f, 90f, 180f };
                polar.BoatSpeedKn = new[] { 0f, 2f, 0f, 2f, 0f, 2f };
                var warm = YachtRaceSkipperTests.Model(polar).CreateSkipper(new YachtRaceWind(), YachtRaceSkipperTests.Rules);
                var fresh = YachtRaceSkipperTests.Model(polar).CreateSkipper(new YachtRaceWind(), YachtRaceSkipperTests.Rules);
                using var probe = new Probe(); probe.Calibrate();
                for (int i = 0; i < 2000; i++) warm.Tick();
                probe.Begin(); for (int i = 0; i < 2000; i++) fresh.Tick(); long allocated = probe.End();
                Assert.That(allocated, Is.EqualTo(0)); Assert.That(fresh.Race.Finished, Is.True);
                Assert.That(fresh.TackCount, Is.GreaterThan(0));
            }
            finally { UnityEngine.Object.DestroyImmediate(polar); }
        }
    }
}
