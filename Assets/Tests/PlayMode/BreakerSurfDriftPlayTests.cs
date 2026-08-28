using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.Core;
using HiddenHarbours.Boats;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// ⭐ <b>THE TEETH ON THE LIVE PHYSICS LOOP (ADR 0040, PR 3): a hull left in broken water is CARRIED
    /// SHOREWARD.</b>
    ///
    /// <para>The EditMode file next door pins the force's shape — where it points, how it scales, what
    /// silences it. None of that can prove the force ever reaches the rigidbody, and the wiring is
    /// exactly where this feature was most likely to die: the swell's own exposure is 0 in the surf zone,
    /// so an early-out placed one line too high would have returned before the surf was ever asked for,
    /// with every unit test still green. These tests are the half that would have caught it.</para>
    ///
    /// <para><b>Time, not frames.</b> Headless frames run as fast as the machine allows, so every wait
    /// here is on elapsed <see cref="Time.time"/> — never on a frame count.</para>
    ///
    /// <para><b>The sea pushes, it does not teleport.</b> A hull's time constant is seconds, so the drift
    /// is given seconds to accumulate and is judged as a displacement, not as a velocity on one
    /// step.</para>
    /// </summary>
    public class BreakerSurfDriftPlayTests
    {
        /// <summary>A 1:25 sandy shoal rising toward +X — shore-normal is +X, so that is the way a bore
        /// must carry her. Deep water is to the west.</summary>
        private sealed class SandyShoal : ITidalTerrain
        {
            public float ElevationAt(Vector2 worldPos) => 0.04f * worldPos.x - 3f;
        }

        /// <summary>A working sea: an onshore breeze at a middling sea state, and a still tide so the
        /// only thing moving the boat is the water.</summary>
        private sealed class SurfEnv : IEnvironmentService
        {
            public float Level;
            public int WorldSeed => 7;
            public TideProfile ActiveTideProfile { get; set; }
            public EnvironmentSample Sample()
                => new EnvironmentSample(new Vector2(6f, 0f), Vector2.zero, 0f, SeaState.Moderate, 1f, 0.55f);
            public float TideHeightAt(double totalSeconds) => 0f;   // keep the flat-seabed grounding read away
            public float WaterLevelAt(double totalSeconds) => Level;
        }

        private sealed class TestClock : IGameClock
        {
            public double LiveOrigin;
            public double TotalSeconds => Time.timeAsDouble - LiveOrigin;
            public GameTime Now => new GameTime(TotalSeconds);
            public Season Season => Season.EarlySpring;
            public int Year => 1;
            public int DayIndex => 0;
            public int DayOfSeason => 1;
            public Weekday Weekday => Weekday.Monday;
            public bool IsMarketDay => false;
            public float HourOfDay => 0f;
            public float DayFraction => 0f;
            public bool IsPaused { get; set; }
            public float TimeScale { get; set; } = 1f;
        }

        private readonly List<Object> _spawned = new();
        private SurfEnv _env;
        private GameConfig _config;

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();
            InteractionGate.Reset();
            HelmKeyCapture.Reset();
            EventBus.Clear<ControlModeChanged>();

            _env = new SurfEnv { Level = 0f };
            _config = ScriptableObject.CreateInstance<GameConfig>();
            _spawned.Add(_config);

            GameServices.TidalTerrain = new SandyShoal();
            GameServices.Environment = _env;
            GameServices.Clock = new TestClock { LiveOrigin = Time.timeAsDouble };
            GameServices.Config = _config;
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Clear<ControlModeChanged>();
            InteractionGate.Reset();
            HelmKeyCapture.Reset();
            GameServices.Reset();
            foreach (var o in _spawned) if (o != null) Object.Destroy(o);
            _spawned.Clear();
        }

        private BoatHullDef Hull(float massKg, string id)
        {
            var h = ScriptableObject.CreateInstance<BoatHullDef>();
            h.Id = id;
            h.DisplayName = id;
            h.MassKg = massKg;
            h.ForwardDrag = 60f;
            h.LateralDrag = 200f;
            h.WindExposure = 0f;             // ⚠ no windage: the ONLY horizontal driver is the water
            h.DraughtMeters = 0.3f;
            _spawned.Add(h);
            return h;
        }

        private BoatController NewBoat(BoatHullDef hull, Vector2 at, out Rigidbody2D rb)
        {
            var go = new GameObject("Boat") { transform = { position = new Vector3(at.x, at.y, 0f) } };
            _spawned.Add(go);
            var boat = go.AddComponent<BoatController>();
            rb = go.GetComponent<Rigidbody2D>();
            boat.SetHull(hull);
            GameServices.Helm.SetPlayersBoat(boat);
            return boat;
        }

        /// <summary>Walk the shoal for the strongest surf and put the boat there — the boil, where a hull
        /// would actually be in trouble. Found from the model rather than hand-picked, so a retune of the
        /// break depth re-aims the test instead of stranding it in flat water.</summary>
        private static float StrongestSurfX()
        {
            var terrain = new SandyShoal();
            var breakers = BreakerSettings.Default;
            WaveTrains trains = WaveMath.TrainsFrom(new Vector2(6f, 0f), 0.55f, GameServices.WaveField);
            WaveTrain dominant = trains.Dominant;
            BreakerContour contour = BreakerMath.ContourFor(in dominant, 1f, in breakers);
            Assert.IsTrue(contour.Breaks, "the test sea must break on this shoal");

            float bestX = float.NaN, best = 0f;
            for (float x = 0f; x < 120f; x += 0.25f)
            {
                SurfState s = BreakerMath.SurfAt(new Vector2(x, 0f), 0f, terrain, in contour, 1f,
                                                 2f * dominant.Amplitude, dominant.Wavelength, in breakers);
                if (!s.IsWorking) continue;
                float bite = s.Breaking01 * s.Whitewater01 * s.StandingHeightMeters;
                if (bite > best) { best = bite; bestX = x; }
            }
            Assert.IsFalse(float.IsNaN(bestX), "the shoal must carry working surf somewhere");
            return bestX;
        }

        private static IEnumerator WaitSeconds(float seconds)
        {
            float deadline = Time.time + seconds;
            while (Time.time < deadline) yield return new WaitForFixedUpdate();
        }

        // =========================================================================================

        [UnityTest]
        public IEnumerator AHullLeftInTheBrokenWater_IsCarriedShoreward()
        {
            // ⭐⭐ THE ACCEPTANCE, and the one that would have caught the wiring trap. The surf force is
            // added BEFORE the swell's early-out precisely because the swell's exposure is 0 here; if it
            // were ever moved below that return, this test fails and every EditMode test still passes.
            float x = StrongestSurfX();
            NewBoat(Hull(400f, "boat.surf_dory"), new Vector2(x, 0f), out Rigidbody2D rb);
            yield return new WaitForFixedUpdate();

            float startX = rb.position.x;
            yield return WaitSeconds(6f);
            float drift = rb.position.x - startX;

            Debug.Log($"[surf-drift] a 400 kg hull in the boil drifted {drift:F3} m shoreward in 6 s " +
                      $"(from x = {startX:F2}).");

            Assert.Greater(drift, 0.15f,
                $"the broken water must carry her shoreward — she moved {drift:F3} m. If this is ~0, " +
                "check that the surf force is added BEFORE the swell's early-out in ApplySeakeeping: " +
                "the swell's exposure is 0 in the surf zone, so `sea` is None here and an early return " +
                "above the surf term kills the whole feature silently.");
        }

        [UnityTest]
        public IEnumerator WithTheSurfDialledOff_SheStaysPut()
        {
            // The A/B, on the live loop: same shoal, same sea, same hull, one bool.
            _config.Seakeeping = SeakeepingSettings.Default;
            var off = _config.Seakeeping;
            off.SurfEnabled = false;
            _config.Seakeeping = off;

            float x = StrongestSurfX();
            NewBoat(Hull(400f, "boat.surf_off"), new Vector2(x, 0f), out Rigidbody2D rb);
            yield return new WaitForFixedUpdate();

            float startX = rb.position.x;
            yield return WaitSeconds(6f);
            float drift = rb.position.x - startX;

            Debug.Log($"[surf-drift] with the surf switch OFF she drifted {drift:F3} m.");
            Assert.Less(Mathf.Abs(drift), 0.05f,
                $"with the surf off she must stay where she was put — she moved {drift:F3} m, so " +
                "something other than the surf is pushing her and the test above proves less than it looks");
        }

        [UnityTest]
        public IEnumerator TwoHullsOfDifferentMass_DriftInTheRightOrder()
        {
            // A light boat is carried further than a heavy one in the same water. Both are pushed; the
            // dory just has less to hold her.
            float x = StrongestSurfX();
            NewBoat(Hull(400f, "boat.surf_light"), new Vector2(x, 0f), out Rigidbody2D light);
            NewBoat(Hull(3200f, "boat.surf_heavy"), new Vector2(x, 40f), out Rigidbody2D heavy);
            yield return new WaitForFixedUpdate();

            float lightStart = light.position.x, heavyStart = heavy.position.x;
            yield return WaitSeconds(6f);
            float lightDrift = light.position.x - lightStart;
            float heavyDrift = heavy.position.x - heavyStart;

            Debug.Log($"[surf-drift] light {lightDrift:F3} m vs heavy {heavyDrift:F3} m in the same water.");

            Assert.Greater(lightDrift, 0.1f, "the light hull must be carried");
            Assert.Greater(heavyDrift, 0f, "the heavy one is not immune either");
            Assert.Greater(lightDrift, heavyDrift * 1.2f,
                $"the light hull must go further than the heavy one ({lightDrift:F3} vs {heavyDrift:F3})");
        }

        [UnityTest]
        public IEnumerator TheSameSeaAndTimeReproduceTheSameDrift()
        {
            // Rule 5 on the live loop: the drift is a function of (seed, game time, seabed, tide), so two
            // hulls dropped at the same spot at the same moment must be carried identically. Run them
            // SIDE BY SIDE rather than in sequence — a second sequential run starts at a different game
            // time, and the sea genuinely is different then, which would be testing the wrong thing.
            float x = StrongestSurfX();
            NewBoat(Hull(400f, "boat.surf_a"), new Vector2(x, 0f), out Rigidbody2D a);
            NewBoat(Hull(400f, "boat.surf_b"), new Vector2(x, 25f), out Rigidbody2D b);
            yield return new WaitForFixedUpdate();

            float aStart = a.position.x, bStart = b.position.x;
            yield return WaitSeconds(5f);
            float aDrift = a.position.x - aStart, bDrift = b.position.x - bStart;

            Debug.Log($"[surf-drift] two identical hulls in the same water: {aDrift:F4} m and {bDrift:F4} m.");

            Assert.Greater(aDrift, 0.1f, "they must actually have been carried for this to mean anything");
            Assert.AreEqual(aDrift, bDrift, 0.02f,
                $"the same sea at the same moment must carry identical hulls identically ({aDrift:F4} vs " +
                $"{bDrift:F4}) — a difference here is hidden state, not physics");
        }

        [UnityTest]
        public IEnumerator SheIsShovedNotTeleported()
        {
            // The ~20 s hull time constant: broken water leans on a boat, it does not snatch her. The
            // drift must ACCELERATE from rest rather than appear in the first step, which is what tells
            // a force apart from a position write.
            float x = StrongestSurfX();
            NewBoat(Hull(400f, "boat.surf_ramp"), new Vector2(x, 0f), out Rigidbody2D rb);
            yield return new WaitForFixedUpdate();

            float startX = rb.position.x;
            yield return WaitSeconds(1f);
            float firstSecond = rb.position.x - startX;
            yield return WaitSeconds(1f);
            float twoSeconds = rb.position.x - startX;

            Debug.Log($"[surf-drift] {firstSecond:F4} m in the first second, {twoSeconds:F4} m by the second.");

            Assert.Greater(twoSeconds, firstSecond,
                "she must keep going — a one-step jump and then nothing is a teleport, not a shove");
            Assert.Less(firstSecond, 0.5f,
                $"she must not be snatched: {firstSecond:F3} m in the first second is a position write, " +
                "not broken water leaning on a hull");
        }

        [UnityTest]
        public IEnumerator ARuntimeSpawnedHull_ResolvesTheSharedGameConfig()
        {
            // ⭐⭐ THE DRIVE-BY FIX, PINNED AS A FIX rather than as today's neutrality.
            //
            // BoatController's `_config` is a per-COMPONENT serialized reference. A boat placed by a
            // builder gets one; a boat created at RUNTIME — spawned fleet, a dev rig, this fixture — got
            // NOTHING, and silently ran the code defaults while ignoring GameConfig.asset entirely. ADR
            // 0040's surf toggle is a GameConfig field, so a spawned hull could not be switched off by
            // the very dial meant to switch it off.
            //
            // ⚠ This asserts the RESOLUTION, not that the resolved values happen to match the code
            // defaults today. Neutrality is not the property worth pinning: the owner tuning the asset
            // away from the defaults is the system working as designed, and after this fix spawned hulls
            // FOLLOWING that tuning is the entire point. So the config here is deliberately set to
            // something no code default would produce.
            var tuned = SeakeepingSettings.Default;
            tuned.Strength = 137.5f;              // a value nothing else in the codebase would hand back
            tuned.SurfShoveStrength = 42.25f;
            tuned.SurfEnabled = false;
            _config.Seakeeping = tuned;

            BoatController boat = NewBoat(Hull(400f, "boat.config_probe"), new Vector2(-400f, 0f), out _);
            yield return new WaitForFixedUpdate();   // Awake has run by now

            Assert.AreEqual(137.5f, boat.SeakeepingPolicy.Strength, 1e-4f,
                "a runtime-spawned hull must read the shared GameConfig — if this reads 220 it has " +
                "fallen back to the code default and the owner's tuning is being ignored on every " +
                "spawned boat");
            Assert.AreEqual(42.25f, boat.SeakeepingPolicy.SurfShoveStrength, 1e-4f,
                "…including the surf dials ADR 0040 added");
            Assert.IsFalse(boat.SeakeepingPolicy.SurfEnabled,
                "…and the toggle, which is the whole reason this mattered");
        }

        [UnityTest]
        public IEnumerator InDeepWaterOffTheShoal_TheSurfDoesNothing()
        {
            // Calm and sheltered water unchanged (the charter, and the M1 law). Well offshore nothing
            // breaks, so the surf term is silent — whatever else the swell may be doing out there.
            NewBoat(Hull(400f, "boat.surf_deep"), new Vector2(-400f, 0f), out Rigidbody2D rb);
            yield return new WaitForFixedUpdate();

            float startX = rb.position.x;
            yield return WaitSeconds(4f);
            float drift = rb.position.x - startX;

            Debug.Log($"[surf-drift] in {(0f - (0.04f * -400f - 3f)):F1} m of water she drifted {drift:F4} m.");
            Assert.Less(Mathf.Abs(drift), 0.05f,
                $"deep water must break nothing and so shove nothing — she moved {drift:F4} m");
        }
    }
}
