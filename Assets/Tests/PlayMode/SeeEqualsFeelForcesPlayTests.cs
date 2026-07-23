using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// <b>SEE==FEEL in the real physics loop</b> (ADR 0023 phase 3; owner ruling 2026-07-23 "Yes
    /// seas push should match"): a live <see cref="BoatController"/> rigidbody, adrift with no input
    /// in an exposed moderate sea, is measurably pushed by the seakeeping force — and that push
    /// CHANGES when the displaced-sea seam activates. Published exaggeration 0 (an active but
    /// visually flat displaced sea) stills the push; a raised exaggeration pushes harder than the
    /// raw sim. The displaced-OFF phase doubles as the byte-identity baseline.
    ///
    /// <para><b>Time discipline</b> (headless PlayMode frames are NOT wall time): every phase drives
    /// the DETERMINISTIC scripted clock, advanced explicitly per fixed step, and each phase replays
    /// the identical clock schedule from the identical start pose — so the ONLY difference between
    /// phases is the published displaced state, and path-length comparisons are exact-cause.</para>
    /// </summary>
    public class SeeEqualsFeelForcesPlayTests
    {
        const float Dt = 1f / 50f;
        const int Steps = 120;

        readonly object _seaOwner = new object();
        readonly List<Object> _spawned = new();

        sealed class ScriptedClock : IGameClock
        {
            public double TotalSeconds { get; private set; }
            public void Advance(double dt) => TotalSeconds += dt;
            public GameTime Now => new GameTime(TotalSeconds);
            public bool IsPaused { get; set; }
            public float TimeScale { get; set; } = 1f;
            public int DayIndex => 0;
            public Season Season => Season.EarlySpring;
            public int Year => 1;
            public int DayOfSeason => 1;
            public Weekday Weekday => Weekday.Monday;
            public bool IsMarketDay => false;
            public float DayFraction => 0f;
            public float HourOfDay => 12f;
            public void SeekTo(double totalSeconds) => TotalSeconds = totalSeconds;
        }

        sealed class ScriptedSea : IEnvironmentService
        {
            public int WorldSeed => 0;
            public TideProfile ActiveTideProfile { get; set; }
            public EnvironmentSample Sample() => new EnvironmentSample(
                new Vector2(6f, 3f), Vector2.zero, tideHeight: 0f,
                HiddenHarbours.Core.SeaState.Moderate, visibility: 1f, seaState01: 0.75f);
            public float TideHeightAt(double totalSeconds) => 0f;
            public float WaterLevelAt(double totalSeconds) => 0f;
        }

        [SetUp]
        public void SetUp() => GameServices.Reset();

        [TearDown]
        public void TearDown()
        {
            DisplacedSea.Clear(_seaOwner);
            GameServices.Reset();
            foreach (var o in _spawned)
                if (o != null) Object.Destroy(o);
            _spawned.Clear();
        }

        /// <summary>A light, lively, undamped hull adrift: no wind shove (WindExposure 0), no input —
        /// the ONLY driver of motion is the seakeeping push under test.</summary>
        BoatHullDef NewAdriftHull(string id)
        {
            var h = ScriptableObject.CreateInstance<BoatHullDef>();
            h.Id = id; h.DisplayName = "SeeFeel Test Hull";
            h.Propulsion = PropulsionType.Oars;
            h.MassKg = 100f;                                   // rb mass 1 — brisk, readable motion
            h.OarPower = 0f; h.OarLateralOffset = 0.6f; h.OarBraceDrag = 0f;
            h.ForwardDrag = 40f; h.LateralDrag = 200f; h.WindExposure = 0f;
            h.DraughtMeters = 0.3f; h.HoldUnits = 1; h.CrewSlots = 1;
            h.CameraWorldHeightMeters = 14f;
            h.SeakeepingMassFactor = 1f; h.SeakeepingLiveliness = 1f; h.SeakeepingDamping = 0f;
            _spawned.Add(h);
            return h;
        }

        /// <summary>Run one adrift phase from the identical start pose over the identical clock
        /// schedule; returns the swept path length (robust against the push's oscillation).</summary>
        IEnumerator RunPhase(BoatHullDef hull, System.Action<float> pathLengthOut)
        {
            var clock = new ScriptedClock();
            GameServices.Clock = clock;
            GameServices.Environment = new ScriptedSea();

            var go = new GameObject("AdriftBoat");
            go.transform.position = new Vector3(37f, -12f, 0f);   // same field spot every phase
            var boat = go.AddComponent<BoatController>();          // RequireComponent → rb + collider
            _spawned.Add(go);
            yield return null;                                     // Awake caches the rigidbody
            boat.SetHull(hull);

            var rb = go.GetComponent<Rigidbody2D>();
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;

            float path = 0f;
            Vector2 prev = rb.position;
            for (int i = 0; i < Steps; i++)
            {
                clock.Advance(Dt);
                yield return new WaitForFixedUpdate();
                path += (rb.position - prev).magnitude;
                prev = rb.position;
            }

            Object.Destroy(go);
            yield return null;                                     // let the body leave the 2D world
            pathLengthOut(path);
        }

        [UnityTest]
        public IEnumerator SeakeepingPush_ChangesWhenTheDisplacedSeamActivates()
        {
            var hull = NewAdriftHull("boat.see_feel");

            // --- Phase 1: displaced OFF — the raw sim push (the baseline, today's handling). ---
            float pathOff = -1f;
            yield return RunPhase(hull, p => pathOff = p);
            Assert.Greater(pathOff, 0.05f,
                "displaced OFF: an exposed moderate sea must measurably push the adrift hull — " +
                "if this is ~0 the seakeeping force never reached the rigidbody and every " +
                "comparison below is vacuous");

            // --- Phase 2: displaced ON, exaggeration 0 — a visually flat sea must FEEL flat. ---
            DisplacedSea.Publish(_seaOwner, new DisplacedSeaState(0f, 0.6f));
            float pathFlat = -1f;
            yield return RunPhase(hull, p => pathFlat = p);
            Assert.Less(pathFlat, pathOff * 0.05f,
                "displaced ON at exaggeration 0: the player SEES a flat sea, so the hull must " +
                "FEEL ~no push (see==feel — the seam's state gates the physics push)");

            // --- Phase 3: displaced ON, exaggeration 2 — the drama you see is the drama you fight. ---
            DisplacedSea.Publish(_seaOwner, new DisplacedSeaState(2f, 0.6f));
            float pathBig = -1f;
            yield return RunPhase(hull, p => pathBig = p);
            Assert.Greater(pathBig, pathOff * 1.2f,
                "displaced ON at exaggeration 2: the exaggerated sea the player sees must shove " +
                "the hull measurably harder than the raw sim did");

            // --- Phase 4: cleared again — the OFF push returns (the A/B contract, physics side). ---
            DisplacedSea.Clear(_seaOwner);
            float pathOffAgain = -1f;
            yield return RunPhase(hull, p => pathOffAgain = p);
            Assert.AreEqual(pathOff, pathOffAgain, pathOff * 0.05f,
                "clearing the seam must restore the raw sim push (identical clock schedule + " +
                "start pose ⇒ near-identical path; a real drift here means hidden state leaked)");
        }
    }
}
