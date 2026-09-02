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
            DisplacedSea.Clear(this);   // the lift arms publish one; never leak it into the next fixture
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

            Debug.Log($"[surf-drift] with the surf switch OFF she drifted {drift:F6} m.");
            // ⭐ EXACT, not "small" (the PR 3 charter: OFF = 0.000 m). It can be exact by construction:
            // in the surf zone the swell's own exposure is 0, so Resolve returns None; with the surf
            // switch off the surf term returns None too; the assembled force is then Vector2.zero and
            // ApplySeakeeping returns BEFORE the AddForce and before the damping. Nothing touches her.
            // A tolerance here would hide the day one of those three stops being true.
            Assert.AreEqual(0f, drift, 1e-5f,
                $"with the surf off she must stay exactly where she was put — she moved {drift:F6} m, so " +
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
            // Rule 5 on the live loop: the drift is a function of (seed, game time, seabed, tide), so the
            // same hull dropped at the same spot at the same game time must be carried identically twice.
            //
            // ⚠️ THIS TEST USED TO RUN THE TWO ARMS SIDE BY SIDE — one hull at (x, 0), one at (x, 25) —
            // because the steady-state surf was a function of DEPTH alone and the shoal is flat along y,
            // so two hulls 25 m apart stood in provably identical water. Revision 3 ends that, and the
            // reason is the feature: a bore has a PHASE, read off the train at the break line it was born
            // on, and a crest arrives along a LINE rather than everywhere at once. Unless the swell runs
            // exactly along +x, the crest reaches (x, 0) and (x, 25) at different moments — measured at
            // 0.7284 m against 0.7707 m, and that spread is the sea being modelled, not hidden state.
            //
            // So the arms are now SEQUENTIAL over the same game time: the clock's origin is reset between
            // them (see RunArm), which is a stricter test than the old one — same place, same instant of
            // the same sea, same seed — rather than a weaker one.
            float period = BorePeriodSeconds();
            var first = new List<float>();
            var second = new List<float>();
            float firstDrift = 0f, secondDrift = 0f;

            yield return RunArm(1f, period, 2, 4, first, d => firstDrift = d);
            yield return RunArm(1f, period, 2, 4, second, d => secondDrift = d);

            Debug.Log($"[surf-drift] the same water twice: {firstDrift:F4} m and {secondDrift:F4} m.");

            Assert.Greater(firstDrift, 0.05f, "she must actually have been carried for this to mean anything");
            Assert.AreEqual(firstDrift, secondDrift, 0.02f,
                $"the same sea at the same moment must carry the same hull identically ({firstDrift:F4} vs " +
                $"{secondDrift:F4}) — a difference here is hidden state, not physics");
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

        // =========================================================================================
        //  ⭐⭐ THE BEAT (ADR 0040 revision 3): the drift arrives in STEPS, one bore per wave period
        // =========================================================================================

        /// <summary>The dominant train's period on this test sea — the beat a bore keeps. Read from the
        /// model rather than typed in, so a retune of the field re-aims the window instead of stranding
        /// the measurement against a number that used to be true.</summary>
        private static float BorePeriodSeconds()
        {
            WaveTrains trains = WaveMath.TrainsFrom(new Vector2(6f, 0f), 0.55f, GameServices.WaveField);
            float period = BreakerMath.PeriodSeconds(trains.Dominant);
            Assert.Greater(period, 0.2f, "the test sea must have a period to beat at");
            Assert.Less(period, 30f, "…and a sane one");
            return period;
        }

        /// <summary>Walk her for <paramref name="periods"/> wave periods, sampling every
        /// <c>period/binsPerPeriod</c>, and collect her mean shoreward VELOCITY over each slice.
        ///
        /// <para>Velocity, not displacement, and the difference is the whole reliability of the
        /// measurement: a wait on <see cref="Time.time"/> lands on a fixed step, so a slice is
        /// <c>T/bins ± 0.02 s</c> — a ~5 % wobble in slice WIDTH that a raw displacement would report as
        /// a ~5 % wobble in the drift, right on top of the beat we are trying to see. Dividing by the
        /// elapsed time the slice actually took removes it exactly.</para></summary>
        private IEnumerator SampleVelocities(Rigidbody2D rb, float period, int periods, int binsPerPeriod,
                                             List<float> velocities)
        {
            float step = period / binsPerPeriod;
            float lastX = rb.position.x;
            float lastT = Time.time;
            for (int i = 0; i < periods * binsPerPeriod; i++)
            {
                float deadline = Time.time + step;
                while (Time.time < deadline) yield return new WaitForFixedUpdate();
                float nowX = rb.position.x, nowT = Time.time;
                float dt = Mathf.Max(1e-4f, nowT - lastT);
                velocities.Add((nowX - lastX) / dt);
                lastX = nowX; lastT = nowT;
            }
        }

        /// <summary>
        /// <b>How much of the drift arrives in BEATS.</b> The per-slice speeds are DETRENDED first — a
        /// hull accelerating from rest is faster every slice, and that ramp is not a beat; a raw variance
        /// would score it as one — then folded onto the wave period and reported as the peak-to-trough
        /// swing of the folded profile, as a fraction of her mean speed.
        ///
        /// <para>0 = she is leaned on evenly. Large = the water arrives, shoves and lets go.</para>
        /// </summary>
        /// <summary>
        /// <b>How much of the drift arrives in BEATS</b>, folded at a GIVEN period rather than at the
        /// sampling period — and folding at a period the sea does not have is what turns the number into
        /// evidence.
        ///
        /// <para>⚠️ A hull accelerating from rest through a surf zone whose profile changes under her has
        /// a per-slice speed that wanders for reasons that have nothing to do with a bore, and a linear
        /// detrend does not remove all of it: the steady arm scores a perfectly real 16 % this way. The
        /// control is not a smaller number, it is a DIFFERENT PERIOD. Fold the same samples at an
        /// incommensurate decoy period and a wandering trend scores about the same, because a trend has
        /// no favourite period — while a genuine beat collapses. The ratio of the two folds is the
        /// scale-free statistic the test compares between arms.</para>
        /// </summary>
        private static float BeatDepthFoldedAt(List<float> perSliceSpeeds, float sampleStepSeconds,
                                               float foldPeriodSeconds, int binsPerPeriod, out float meanSpeed)
        {
            List<float> increments = perSliceSpeeds;
            int n = increments.Count;
            double sx = 0, sy = 0, sxx = 0, sxy = 0;
            for (int i = 0; i < n; i++)
            {
                sx += i; sy += increments[i]; sxx += (double)i * i; sxy += (double)i * increments[i];
            }
            double denom = n * sxx - sx * sx;
            double slope = denom != 0d ? (n * sxy - sx * sy) / denom : 0d;
            double intercept = (sy - slope * sx) / n;
            meanSpeed = (float)(sy / n);
            if (meanSpeed <= 0f) return 0f;

            var sum = new double[binsPerPeriod];
            var count = new int[binsPerPeriod];
            double perBin = Mathf.Max(1e-6f, foldPeriodSeconds) / binsPerPeriod;
            for (int i = 0; i < n; i++)
            {
                int bin = (int)(System.Math.Floor(i * (double)sampleStepSeconds / perBin) % binsPerPeriod);
                sum[bin] += increments[i] - (intercept + slope * i);
                count[bin]++;
            }
            double lo = double.MaxValue, hi = double.MinValue;
            for (int b = 0; b < binsPerPeriod; b++)
            {
                if (count[b] == 0) continue;
                double v = sum[b] / count[b];
                if (v < lo) lo = v;
                if (v > hi) hi = v;
            }
            return (float)((hi - lo) / meanSpeed);
        }

        /// <summary>One arm of the A/B: the SAME sea, the same shoal, the same hull, at the same GAME
        /// TIME (the clock origin is reset, so arm B sails the water arm A sailed — the sea genuinely is
        /// different a minute later, and comparing across that would be measuring the wrong thing), with
        /// one dial moved.</summary>
        private IEnumerator RunArm(float borePulse01, float period, int periods, int bins,
                                   List<float> increments, System.Action<float> totalDrift)
        {
            var tuned = SeakeepingSettings.Default;
            tuned.SurfBorePulse01 = borePulse01;
            _config.Seakeeping = tuned;                       // read at the boat's Awake, below
            GameServices.Clock = new TestClock { LiveOrigin = Time.timeAsDouble };

            float x = StrongestSurfX();
            BoatController boat = NewBoat(Hull(400f, $"boat.beat_{borePulse01:F0}"),
                                          new Vector2(x, 0f), out Rigidbody2D rb);
            yield return new WaitForFixedUpdate();
            Assert.AreEqual(borePulse01, boat.SeakeepingPolicy.SurfBorePulse01, 1e-4f,
                "the arm must actually be running the dial it claims — a shared config read at Awake");

            float start = rb.position.x;
            yield return SampleVelocities(rb, period, periods, bins, increments);
            totalDrift(rb.position.x - start);

            Object.Destroy(boat.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator TheDriftArrivesInBEATS_NotAsASteadyLean()
        {
            // ⭐⭐ THE ACCEPTANCE for PR 3's feel, and the A/B that makes it mean something. Both arms
            // are carried up the beach; only one of them does it in STEPS. Two arms returning the same
            // number would be a dead control, not a pass — that is how #682 found the fleet-wide config
            // bug — so the assertions below compare the arms rather than judging one in isolation.
            float period = BorePeriodSeconds();
            const int Bins = 8, Periods = 6;

            float step = period / Bins;
            float decoy = period * 1.4142f;      // incommensurate: no harmonic of the wave's own period

            var pulsedInc = new List<float>();   // per-slice mean speed, not displacement — see the sampler
            float pulsedTotal = 0f;
            yield return RunArm(1f, period, Periods, Bins, pulsedInc, d => pulsedTotal = d);
            float pulsedBeat = BeatDepthFoldedAt(pulsedInc, step, period, Bins, out float pulsedMean);
            float pulsedDecoy = BeatDepthFoldedAt(pulsedInc, step, decoy, Bins, out _);

            var steadyInc = new List<float>();
            float steadyTotal = 0f;
            yield return RunArm(0f, period, Periods, Bins, steadyInc, d => steadyTotal = d);
            float steadyBeat = BeatDepthFoldedAt(steadyInc, step, period, Bins, out float steadyMean);
            float steadyDecoy = BeatDepthFoldedAt(steadyInc, step, decoy, Bins, out _);

            float pulsedPreference = pulsedBeat / Mathf.Max(1e-4f, pulsedDecoy);
            float steadyPreference = steadyBeat / Mathf.Max(1e-4f, steadyDecoy);

            Debug.Log($"[surf-beat] T = {period:F2} s, {Periods} periods x {Bins} bins, decoy fold {decoy:F2} s.\n" +
                      $"  pulsed: total {pulsedTotal:F3} m, mean speed {pulsedMean:F4} m/s, " +
                      $"swing at T {pulsedBeat:P1} vs decoy {pulsedDecoy:P1} = x{pulsedPreference:F2}\n" +
                      $"  steady: total {steadyTotal:F3} m, mean speed {steadyMean:F4} m/s, " +
                      $"swing at T {steadyBeat:P1} vs decoy {steadyDecoy:P1} = x{steadyPreference:F2}");

            // Anti-vacuous first: both arms must actually have been carried, or every ratio below is a
            // ratio of two nothings.
            Assert.Greater(pulsedTotal, 0.05f, $"the pulsed arm must be carried at all ({pulsedTotal:F3} m)");
            Assert.Greater(steadyTotal, 0.05f, $"the steady arm must be carried at all ({steadyTotal:F3} m)");

            // ⭐ The claim, and it is about a PERIOD, not an amplitude: with the dial up her drift prefers
            // the WAVE's period over a decoy that is no period of anything. With it down it has no
            // preference, because what wanders in the steady arm is her acceleration through a changing
            // surf zone, and a trend has no favourite period.
            Assert.Greater(pulsedPreference, 1.5f,
                $"the drift must prefer the wave's own period — it swung {pulsedBeat:P1} folded at T " +
                $"against {pulsedDecoy:P1} folded at a decoy ({pulsedPreference:F2}x). If this is ~1 the " +
                "bore's clock is not reaching the force and what is left is her acceleration ramp.");
            Assert.Greater(pulsedPreference, steadyPreference * 1.5f,
                $"…and prefer it MORE than the steady arm does ({pulsedPreference:F2}x against " +
                $"{steadyPreference:F2}x) — two arms with the same preference are a dead control");
            Assert.Greater(pulsedBeat, steadyBeat,
                $"…and swing wider at that period than the steady arm ({pulsedBeat:P1} vs {steadyBeat:P1})");
            Assert.AreNotEqual(steadyTotal, pulsedTotal,
                "identical arms are a dead control: the switch is not switching anything");
        }

        [UnityTest]
        public IEnumerator TheBoresLift_ReachesTheHullsRide()
        {
            // The other half of the feel, and the half no force test can see: the wash PICKS HER UP. The
            // lift rides the displaced sea's own ride channel, so the fixture publishes a displaced state
            // exactly as DisplacedWaterSurface does, wires the motion component to a bare visual child,
            // and watches that child.
            //
            // ⚠ Judged on MEANS across the run, never sample against paired sample. The ride the child
            // carries is the swell's heave PLUS the lift, the swell's heave is a large zero-mean
            // oscillation, and the two arms' sample instants differ by the few milliseconds it takes to
            // build a boat — at ~2.8 m/s of heave rate that is tens of millimetres of pure timing noise,
            // the same size as the thing being measured. Over three periods the swell averages itself
            // away and the lift, which is never negative, does not.
            float period = BorePeriodSeconds();
            var withLift = new List<float>();
            var noLift = new List<float>();
            var modelSaid = new List<float>();

            yield return RunLiftArm(1f, period, withLift, modelSaid);
            yield return RunLiftArm(0f, period, noLift, null);

            float rideOn = Mean(withLift), rideOff = Mean(noLift), expected = Mean(modelSaid);
            float carried = rideOn - rideOff;

            Debug.Log($"[surf-lift] mean ride {rideOn:F4} m with the dial at 1 against {rideOff:F4} m at 0 " +
                      $"= {carried:F4} m carried; the model said {expected:F4} m " +
                      $"(peak {Max(modelSaid):F4} m) over {withLift.Count} samples.");

            // Anti-vacuous: the bore must actually be lifting her in this water, or the comparison below
            // is two numbers that were always going to match.
            Assert.Greater(Max(modelSaid), 0.02f,
                $"the model must claim a real lift here first — it peaked at {Max(modelSaid):F4} m");
            Assert.Greater(expected, 0.005f, "…and a non-trivial mean one");

            Assert.Greater(carried, expected * 0.5f,
                $"the ride must actually carry the bore's lift — the model said a mean {expected:F4} m and " +
                $"the ride moved {carried:F4} m. If this is ~0, the lift is not reaching BoatWaveMotion " +
                "(check BoatController.SurfUnderHull is published, and that the displaced sea is active: " +
                "the lift rides that channel and is 0 without it).");
            Assert.Less(carried, expected * 2f + 0.02f,
                $"…and carry THAT and not something else — {carried:F4} m against the model's {expected:F4} m");
        }

        private static float Mean(List<float> values)
        {
            if (values == null || values.Count == 0) return 0f;
            double sum = 0;
            for (int i = 0; i < values.Count; i++) sum += values[i];
            return (float)(sum / values.Count);
        }

        private static float Max(List<float> values)
        {
            float hi = 0f;
            if (values != null) for (int i = 0; i < values.Count; i++) hi = Mathf.Max(hi, values[i]);
            return hi;
        }

        /// <summary>One arm of the lift A/B: a boat with the wave-motion component wired to a bare visual
        /// child, a displaced sea published under her, and the ride sampled off that child's world Y —
        /// alongside, when asked, what the MODEL says the lift should be at that same instant, read from
        /// the very surf state the physics tick solved.</summary>
        private IEnumerator RunLiftArm(float liftScale, float period, List<float> rideSamples,
                                       List<float> modelLift)
        {
            var tuned = SeakeepingSettings.Default;
            tuned.SurfLiftScale = liftScale;
            _config.Seakeeping = tuned;
            GameServices.Clock = new TestClock { LiveOrigin = Time.timeAsDouble };

            float x = StrongestSurfX();
            BoatController boat = NewBoat(Hull(400f, $"boat.lift_{liftScale:F0}"), new Vector2(x, 0f), out _);

            var visual = new GameObject("visual").transform;
            visual.SetParent(boat.transform, false);
            var motion = boat.gameObject.AddComponent<BoatWaveMotion>();
            motion.Configure(visual, (IBoatHullPresenter)null);

            // The displaced sea, published the way DisplacedWaterSurface publishes it: without it the
            // ride is 0 by the A/B contract and this test would be measuring nothing.
            DisplacedSea.Publish(this, new DisplacedSeaState(1.5f, 0.6f));

            yield return new WaitForFixedUpdate();
            yield return null;

            const int Samples = 32;
            float step = period / 8f;
            for (int i = 0; i < Samples; i++)
            {
                float deadline = Time.time + step;
                while (Time.time < deadline) yield return new WaitForFixedUpdate();
                rideSamples.Add(visual.position.y - boat.transform.position.y);
                modelLift?.Add(SeakeepingForcesMath.SurfLiftMeters(boat.SurfUnderHull, boat.SeakeepingPolicy));
            }

            DisplacedSea.Clear(this);
            Object.Destroy(boat.gameObject);
            yield return null;
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
