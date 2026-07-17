using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.Core;
using HiddenHarbours.Boats;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// THE PILOTABLE FLEET, on real physics.
    ///
    /// <para><b>Why this can't be an EditMode test.</b> Two of the three things under test only exist once
    /// the engine is actually running. (1) Terminal speed is the fixed point of BoatController's force
    /// assembly PLUS the rigidbody's own <c>linearDamping</c> — and that damping is applied by Unity's
    /// integrator, not by any code we could call. Algebra predicts it; only a real physics step proves the
    /// prediction. (2) <see cref="OutboardMotorLayer"/> draws in <c>LateUpdate</c>, so its swivel and its
    /// unmanned-helm centring simply never happen outside play mode.</para>
    ///
    /// <para>The hulls are the REAL committed assets (loaded off disk in the editor), so these tests fail
    /// when a stat is tuned out of band — which is the point. There is no environment service, so wind,
    /// current and tide are all zero: this is the hull, the engine and the water, and nothing else.</para>
    /// </summary>
    public class PilotableFleetPlayTests
    {
        const string DataBoats = "Assets/_Project/Data/Boats";

        private readonly List<Object> _spawned = new();

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();          // null environment → zero wind / current / tide
            EventBus.Clear<ActiveBoatChanged>();
            EventBus.Clear<DevNotice>();
        }

        [TearDown]
        public void TearDown()
        {
            GameServices.Reset();
            EventBus.Clear<ActiveBoatChanged>();
            EventBus.Clear<DevNotice>();
            foreach (var o in _spawned)
                if (o != null) Object.Destroy(o);
            _spawned.Clear();
        }

        private BoatHullDef LoadHull(string file)
        {
#if UNITY_EDITOR
            var asset = AssetDatabase.LoadAssetAtPath<BoatHullDef>($"{DataBoats}/{file}.asset");
            Assert.IsNotNull(asset, $"{DataBoats}/{file}.asset is missing — run the cove builder.");
            return asset;
#else
            Assert.Ignore("Needs the AssetDatabase: these assert the REAL committed hulls, not a mirror.");
            return null;
#endif
        }

        /// <summary>
        /// A hull that may legitimately not be on disk. Only <c>Punt.asset</c> qualifies: it is
        /// BUILDER-GENERATED AND NEVER COMMITTED, so it exists only in a checkout where someone has run the
        /// cove builder. Returns null rather than failing, so a clean clone skips rather than going red on a
        /// missing generated asset — see PilotableFleetContentTests.OptionalHull for the full note.
        /// </summary>
        private BoatHullDef LoadOptionalHull(string file)
        {
#if UNITY_EDITOR
            return AssetDatabase.LoadAssetAtPath<BoatHullDef>($"{DataBoats}/{file}.asset");
#else
            return null;
#endif
        }

        /// <summary>A boat rigged as the builder leaves it. The hull COLLIDER is mirrored deliberately: it
        /// is what Unity derives rotational inertia from, so a unit-default capsule would quietly change
        /// how the helm bites. (The established convention in the sibling PlayMode boat tests.)</summary>
        private (GameObject go, BoatController boat, Rigidbody2D rb) NewBoat(BoatHullDef hull, Vector3 pos)
        {
            var go = new GameObject(hull.Id);
            go.transform.position = pos;
            var boat = go.AddComponent<BoatController>();   // RequireComponent → Rigidbody2D + CapsuleCollider2D
            var col = go.GetComponent<CapsuleCollider2D>();
            col.direction = CapsuleDirection2D.Vertical;
            col.size = new Vector2(1.7f, Mathf.Max(1f, hull.LengthMeters));
            col.offset = Vector2.zero;
            _spawned.Add(go);
            boat.SetHull(hull);
            return (go, boat, go.GetComponent<Rigidbody2D>());
        }

        /// <summary>
        /// Spin the MAIN loop (Update/LateUpdate — not physics) until <paramref name="settled"/> holds, giving up
        /// after <paramref name="budgetSeconds"/> of <see cref="Time.time"/>. Returns either way: the caller's
        /// assertion is the verdict, so a timeout fails with the caller's own message rather than a generic one.
        ///
        /// <para><b>Why this is not a frame count.</b> The sibling loops in this file wait on
        /// <c>WaitForFixedUpdate</c>, where N steps really is N×<see cref="Time.fixedDeltaTime"/> of sim time on
        /// any machine. <c>yield return null</c> has no such guarantee: a main-loop frame is worth whatever wall
        /// clock it took, so a frame count is only a duration if you already know the frame rate. In headless
        /// batchmode there is nothing to render and frames cost ~0.7 ms, so the 60-frame wait this replaced
        /// bought ~0.045 s — where the author's editor, at ~60 fps, bought ~1 s. Anything paced off
        /// <see cref="Time.deltaTime"/> (which is the engine's whole job) then passes locally and fails in CI.
        ///
        /// <para>Waiting on <see cref="Time.time"/> is frame-rate independent <b>by construction</b>, not by
        /// tuning: <see cref="Time.time"/> is the running sum of the very <see cref="Time.deltaTime"/> the layer
        /// under test integrates. One 0.5 s frame and five hundred 1 ms frames advance the engine by exactly the
        /// same swivel — so this waits for the same amount of ENGINE, whatever the machine.</para></para>
        /// </summary>
        private IEnumerator SpinUntil(System.Func<bool> settled, float budgetSeconds)
        {
            float deadline = Time.time + budgetSeconds;
            while (!settled() && Time.time < deadline)
                yield return null;
        }

        /// <summary>
        /// The layer's OWN swivel cadence, read off the component rather than mirrored as a const here. The rate
        /// is a serialized owner tunable (rule 6); a test that re-declared it would still be green the day the
        /// owner halved it — and green for a reason that no longer matches the product. Read it, derive the wait
        /// from it, and a re-tune re-times the test for free.
        /// </summary>
        private static float SteerColumnsPerSecond(OutboardMotorLayer motor)
        {
#if UNITY_EDITOR
            var field = new SerializedObject(motor).FindProperty("_steerColumnsPerSecond");
            Assert.IsNotNull(field,
                "OutboardMotorLayer._steerColumnsPerSecond was renamed or removed. This test DERIVES its waits from " +
                "that cadence on purpose — re-point this read; do not paper over it with a magic sleep.");
            Assert.Greater(field.floatValue, 0f,
                "a zero/negative cadence snaps the engine instantly (OutboardMotorMath.StepTowardColumn), which " +
                "would make the swivel this test exists to prove unobservable");
            return field.floatValue;
#else
            return 0f;
#endif
        }

        /// <summary>Run full ahead until the hull stops accelerating, and report the speed it settles at.
        /// Bails out at <paramref name="maxSeconds"/> of sim time rather than looping forever.</summary>
        private IEnumerator RunToTerminal(BoatController boat, Rigidbody2D rb, float maxSeconds = 60f)
        {
            float elapsed = 0f, previous = -1f;
            while (elapsed < maxSeconds)
            {
                boat.SetControl(1f, 0f);   // full ahead, helm amidships — the throttle is a HELD slider
                yield return new WaitForFixedUpdate();
                elapsed += Time.fixedDeltaTime;

                // Settled = the speed stopped climbing measurably. Checked on a coarse cadence so a single
                // noisy step can't call it early.
                if (elapsed > 5f && Mathf.Abs(rb.linearVelocity.magnitude - previous) < 0.0005f) break;
                previous = rb.linearVelocity.magnitude;
            }
        }

        /// <summary>
        /// Row flat out (both oars) until the hull stops accelerating. The oars' twin of
        /// <see cref="RunToTerminal"/> — an Oars hull ignores <c>SetControl</c> entirely
        /// (<see cref="BoatController.UsesEngineHelm"/> branches the whole drive), so running the dory through
        /// the engine harness would have measured a boat with no propulsion at all.
        /// </summary>
        private IEnumerator RowToTerminal(BoatController boat, Rigidbody2D rb, float maxSeconds = 60f)
        {
            float elapsed = 0f, previous = -1f;
            while (elapsed < maxSeconds)
            {
                boat.SetOarInput(1f, 1f, false);   // both oars, hard ahead, no brace
                yield return new WaitForFixedUpdate();
                elapsed += Time.fixedDeltaTime;

                if (elapsed > 5f && Mathf.Abs(rb.linearVelocity.magnitude - previous) < 0.0005f) break;
                previous = rb.linearVelocity.magnitude;
            }
        }

        /// <summary>Drive a hull to terminal on whichever helm it actually has, and report the speed.</summary>
        private IEnumerator DriveToTerminal(BoatHullDef hull, System.Action<float> report)
        {
            var (go, boat, rb) = NewBoat(hull, Vector3.zero);
            if (BoatController.UsesEngineHelm(hull.Propulsion)) yield return RunToTerminal(boat, rb);
            else                                                yield return RowToTerminal(boat, rb);
            report(rb.linearVelocity.magnitude);
            Object.Destroy(go);
            yield return null;
        }

        // ---- (1) the speed ladder, measured -------------------------------------------------------

        [UnityTest]
        public IEnumerator TheDory_RowsToHerMeasuredTerminal()
        {
            // 2.0 m/s is MEASURED here, not solved for. Nobody had ever run her: the number everyone believed,
            // 2.5, came from "OarPower 300 / ForwardDrag 120" — a ratio that drops BOTH the second oar
            // (OarThrust sums them) and the rigidbody's own linearDamping (~40-50% of her resistance). She
            // really did 2.95. ForwardDrag 120 -> 215 is what brought her here.
            var hull = LoadHull("Dory");
            Assert.AreEqual(PropulsionType.Oars, hull.Propulsion,
                "precondition: the dory is hand-rowed — if she ever takes an engine, this harness measures the " +
                "wrong helm and every assertion below is meaningless");

            float measured = 0f;
            yield return DriveToTerminal(hull, v => measured = v);

            Assert.AreEqual(2.0f, measured, 0.15f,
                $"the dory settles at ≈2.0 m/s (measured {measured:0.00}). If this moved, someone retuned the " +
                "starter boat the owner rows every session — and 'the dory is the slowest boat' is his call.");
        }

        [UnityTest]
        public IEnumerator TheDory_IsTheSlowestBoatAfloat()
        {
            // The OWNER'S RULE, measured end to end rather than trusted to a table: "yes the dory should be the
            // slowest boat". She was not — at ForwardDrag 120 she made 2.95 and beat three hulls. This is the
            // test that would have caught that, and the one that catches the next person who re-tunes her (or
            // any other hull) back over the line. Every boat runs the identical harness on its own helm, so the
            // ordering is decided by the assets and by nothing else.
            var others = new List<(string name, float speed)>();
            foreach (var file in new[] { "FishingSkiff", "PuntUpgraded", "ConsoleSkiff", "SportSkiff", "SportSkiffTwin" })
            {
                float v = 0f;
                yield return DriveToTerminal(LoadHull(file), s => v = s);
                others.Add((file, v));
            }

            // The basic punt is builder-generated and never committed, so she is only measured where someone has
            // run the cove builder (see LoadOptionalHull). She is also the dory's NEAREST rival at ≈2.26 m/s —
            // the margin that matters most — so when she is on disk she is absolutely included.
            var punt = LoadOptionalHull("Punt");
            if (punt != null)
            {
                float v = 0f;
                yield return DriveToTerminal(punt, s => v = s);
                others.Add(("Punt", v));
            }

            float dory = 0f;
            yield return DriveToTerminal(LoadHull("Dory"), s => dory = s);

            foreach (var (name, speed) in others)
                Assert.Less(dory, speed,
                    $"the dory ({dory:0.00} m/s) must be slower than {name} ({speed:0.00} m/s). The whole " +
                    $"ladder, measured: {string.Join(", ", others.ConvertAll(o => $"{o.name} {o.speed:0.00}"))}.");
        }

        [UnityTest]
        public IEnumerator ConsoleSkiff_SettlesAtAWorkboatsSpeed()
        {
            var hull = LoadHull("ConsoleSkiff");
            var (_, boat, rb) = NewBoat(hull, Vector3.zero);

            yield return RunToTerminal(boat, rb);

            Assert.AreEqual(3.9f, rb.linearVelocity.magnitude, 0.15f,
                "the console is the WORKBOAT: the owner's target is 3.8–4.0 m/s. If this drifts, the " +
                "EnginePower/ForwardDrag/MassKg on ConsoleSkiff.asset moved — or BoatController's force " +
                "model changed under it (see the derivation in GreyboxBuilder's hull-ladder note).");
        }

        [UnityTest]
        public IEnumerator SportSkiff_IsTheFasterGlassSister()
        {
            var hull = LoadHull("SportSkiff");
            var (_, boat, rb) = NewBoat(hull, Vector3.zero);

            yield return RunToTerminal(boat, rb);

            Assert.AreEqual(4.64f, rb.linearVelocity.magnitude, 0.15f,
                "the sport skiff's target is ≈4.6 m/s — the SAME engine as the console on a lighter, " +
                "slipperier hull");
        }

        [UnityTest]
        public IEnumerator SportSkiffTwin_IsTheFastestHullAfloat()
        {
            var hull = LoadHull("SportSkiffTwin");
            var (_, boat, rb) = NewBoat(hull, Vector3.zero);

            yield return RunToTerminal(boat, rb);

            Assert.AreEqual(5.63f, rb.linearVelocity.magnitude, 0.15f,
                "the twin's target is ≈5.6 m/s. NOT 2× the single: drag is linear in v here, so doubling " +
                "thrust would exactly double terminal speed to ~9 m/s. EnginePower is a design-unit " +
                "tunable calibrated to a designed speed, not a Newton count.");
        }

        [UnityTest]
        public IEnumerator TheMeasuredLadder_MatchesTheDerivation()
        {
            // The guard on the ALGEBRA itself. GreyboxBuilder's hull-ladder note derives every stat on the
            // fleet from this formula; if Unity's damping semantics ever change, or someone edits
            // BoatController's feel scale, this fails and points straight at the note rather than leaving
            // three separate speed tests to be re-tuned by hand.
            const float forceFeelScale = 0.01f;   // BoatController.ForceFeelScale
            const float linearDamping = 0.2f;     // the damping BoatController.Awake sets

            foreach (var file in new[] { "ConsoleSkiff", "SportSkiff", "SportSkiffTwin", "FishingSkiff", "PuntUpgraded" })
            {
                var hull = LoadHull(file);
                var (go, boat, rb) = NewBoat(hull, Vector3.zero);

                yield return RunToTerminal(boat, rb);

                float predicted = (hull.EnginePower * forceFeelScale)
                                / (hull.ForwardDrag * forceFeelScale + (hull.MassKg / 100f) * linearDamping);
                Assert.AreEqual(predicted, rb.linearVelocity.magnitude, 0.1f,
                    $"{file}: measured {rb.linearVelocity.magnitude:0.00} m/s vs the derivation's " +
                    $"{predicted:0.00}. The rigidbody's own linearDamping is the term a naive " +
                    "EnginePower/ForwardDrag ratio drops — and it is over half the console's resistance.");

                Object.Destroy(go);
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator TheHeavierHull_AcceleratesSlower_EvenBeforeTerminal()
        {
            // Terminal speed is only half of feel. The console must also take longer to GET there, or
            // "heavier" is a number on an asset that the player never experiences.
            var console = LoadHull("ConsoleSkiff");
            var sport = LoadHull("SportSkiff");
            var (_, cBoat, cRb) = NewBoat(console, Vector3.zero);
            var (_, sBoat, sRb) = NewBoat(sport, new Vector3(50f, 0f, 0f));

            for (float t = 0f; t < 2f; t += Time.fixedDeltaTime)
            {
                cBoat.SetControl(1f, 0f);
                sBoat.SetControl(1f, 0f);
                yield return new WaitForFixedUpdate();
            }

            Assert.Less(cRb.linearVelocity.magnitude, sRb.linearVelocity.magnitude,
                "two seconds in, the 1200 kg console must still be behind the 950 kg sport — the weight is " +
                "felt in the pickup, not just at the top end");
        }

        [UnityTest]
        public IEnumerator ThePunt_StillSettlesWhereSheAlwaysHas()
        {
            // The regression guard on "we gave her a picture and nothing else". She is a REAL purchasable M1
            // boat (PuntOffer, ₲1800) whose feel the owner already knows, so #211 left EnginePower, the drags
            // and the mass strictly alone — and the 5.2 m length change touches the WAKE, never the physics
            // (BoatController reads mass and drag; LengthMeters only reaches WakeGrading).
            var hull = LoadOptionalHull("Punt");
            if (hull == null) Assert.Ignore("Data/Boats/Punt.asset is builder-generated and not committed.");

            var (_, boat, rb) = NewBoat(hull, Vector3.zero);
            yield return RunToTerminal(boat, rb);

            Assert.AreEqual(2.32f, rb.linearVelocity.magnitude, 0.15f,
                "the punt settles at ≈2.3 m/s, exactly as she did before she had a skin. If this moved, " +
                "someone retuned a boat the owner has already bought and learned.");
        }

        [UnityTest]
        public IEnumerator TheUpgradedPunt_IsAWorkboatWithABetterEngine_NotASportsCar()
        {
            // 825 EnginePower was MEASURED here, not solved for. A naive EnginePower/ForwardDrag ratio would
            // have said 5.89 m/s; the rigidbody's own linearDamping — which the ratio drops — is half her
            // resistance, and the truth is 2.89.
            var hull = LoadHull("PuntUpgraded");
            var (_, boat, rb) = NewBoat(hull, Vector3.zero);

            yield return RunToTerminal(boat, rb);
            float upgraded = rb.linearVelocity.magnitude;

            Assert.AreEqual(2.89f, upgraded, 0.15f,
                "the upgraded punt's target is ≈2.9 m/s — the owner asked for 20–30% over the basic punt");
            Assert.Less(upgraded, 4.64f,
                "…and she must stay under the sport skiff: a bigger outboard on a workboat is not a sports car");
        }

        [UnityTest]
        public IEnumerator ThePuntsUpgrade_IsWorthBetween20And30Percent_Measured()
        {
            // The design contract, measured end to end on real physics rather than restated from the assets.
            // Both punts run the identical harness, so the ratio is decided by the boats and by nothing else.
            var basicHull = LoadOptionalHull("Punt");
            if (basicHull == null) Assert.Ignore("Data/Boats/Punt.asset is builder-generated and not committed.");

            var (basicGo, basicBoat, basicRb) = NewBoat(basicHull, Vector3.zero);
            yield return RunToTerminal(basicBoat, basicRb);
            float basic = basicRb.linearVelocity.magnitude;
            Object.Destroy(basicGo);
            yield return null;

            var (_, upBoat, upRb) = NewBoat(LoadHull("PuntUpgraded"), Vector3.zero);
            yield return RunToTerminal(upBoat, upRb);
            float upgraded = upRb.linearVelocity.magnitude;

            float gain = upgraded / basic - 1f;
            Assert.GreaterOrEqual(gain, 0.20f,
                $"measured {basic:0.00} → {upgraded:0.00} m/s = {gain:P0}: under the 20% the owner asked for");
            Assert.LessOrEqual(gain, 0.30f,
                $"measured {basic:0.00} → {upgraded:0.00} m/s = {gain:P0}: over the 30% the owner asked for");
        }

        // ---- (2) the picker, in place, on a live boat ---------------------------------------------

        private (DevBoatPicker picker, BoatController boat, ShipHold hold, SpriteRenderer sr, GameObject go)
            NewPickedBoat(BoatHullDef[] roster)
        {
            var (go, boat, _) = NewBoat(roster[0], new Vector3(3f, -4f, 0f));
            var sr = go.AddComponent<SpriteRenderer>();
            var hold = go.AddComponent<ShipHold>();
            var picker = go.AddComponent<DevBoatPicker>();
            hold.SetHull(roster[0]);
            picker.Configure(roster, boat, hold, sr);
            BoatHullSkinner.ApplyHull(go, sr, roster[0], boat);
            return (picker, boat, hold, sr, go);
        }

        [UnityTest]
        public IEnumerator Picker_CyclesTheWholeFleet_OnALiveBoat_WithoutMovingIt()
        {
            // The affordance end to end, on the real assets and a real physics body: every committed hull, in
            // place.
            // EditMode covers the swap's LOGIC; this covers it surviving actual Awake/LateUpdate lifecycles
            // — where the skin's components really get added, destroyed and re-added under a running boat.
            // The builder's real cycle order, minus the basic punt: Punt.asset is builder-generated and never
            // committed, so a clean clone has no such file. Her rung is proven in EditMode
            // (DevBoatPickerTests) against an in-memory mirror; what THIS test exists for is the swap
            // surviving real Awake/LateUpdate lifecycles, which her sister hull exercises identically.
            var roster = new[]
            {
                LoadHull("Dory"), LoadHull("FishingSkiff"), LoadHull("PuntUpgraded"),
                LoadHull("ConsoleSkiff"), LoadHull("SportSkiff"), LoadHull("SportSkiffTwin"),
            };
            var (picker, boat, hold, _, go) = NewPickedBoat(roster);
            var start = go.transform.position;

            yield return new WaitForFixedUpdate();

            for (int step = 1; step <= roster.Length; step++)
            {
                picker.Next();
                yield return null;             // let LateUpdate draw the new rig
                yield return new WaitForFixedUpdate();

                var expected = roster[step % roster.Length];
                Assert.AreSame(expected, boat.Hull, $"step {step}: the feel is on {expected.Id}");
                Assert.AreEqual(expected.HoldUnits, hold.CapacityUnits, $"step {step}: the hold followed");
                Assert.AreEqual(expected.MassKg / 100f, go.GetComponent<Rigidbody2D>().mass, 0.001f,
                    $"step {step}: the rigidbody really is the new boat's weight");

                var child = go.transform.Find(BoatHullSkinner.VisualChildName);
                Assert.IsNotNull(child, $"step {step}: {expected.Id} wears its compass");
                Assert.AreEqual(1, go.GetComponents<DirectionalBoatSprite>().Length,
                    $"step {step}: one compass, not a pile — a cycle must swap, never stack");
            }

            Assert.AreSame(roster[0], boat.Hull, "a full lap wraps back to the dory");
            Assert.AreEqual(start, go.transform.position,
                "…and the boat never moved: same spot, same water, every hull");
        }

        [UnityTest]
        public IEnumerator Picker_SwappingDoryToSkiff_TradesTheOarsForAnOutboard()
        {
            // The transition that exercises BOTH overlays' install/teardown against each other — the pair
            // whose sorting bands overlap. The dory's oars must be gone, and the skiff's engine present.
            var roster = new[] { LoadHull("Dory"), LoadHull("SportSkiffTwin") };
            var (picker, _, _, _, go) = NewPickedBoat(roster);
            yield return null;

            Assert.IsNotNull(go.GetComponent<DoryOarLayer>(), "precondition: the dory has her oars");
            Assert.IsNull(go.GetComponent<OutboardMotorLayer>(), "precondition: …and no engine");

            picker.Next();
            yield return null;

            Assert.IsNull(go.GetComponent<DoryOarLayer>(),
                "the dory's oars must not row a skiff — and they'd z-fight the engine leg if they stayed");
            var motor = go.GetComponent<OutboardMotorLayer>();
            Assert.IsNotNull(motor, "the twin's outboards are bolted on");
            Assert.IsTrue(motor.IsWired);
            Assert.AreEqual(OutboardMotorLayer.MotorFit.Twin, motor.Fit);

            picker.Next();   // wrap back to the dory
            yield return null;

            Assert.IsNotNull(go.GetComponent<DoryOarLayer>(), "…and back: the oars return");
            Assert.IsNull(go.GetComponent<OutboardMotorLayer>(), "the engine comes off the rowboat");
        }

        [UnityTest]
        public IEnumerator Picker_IsInert_WhenNobodyIsAtTheHelm()
        {
            var roster = new[] { LoadHull("Dory"), LoadHull("ConsoleSkiff") };
            var (picker, boat, _, _, _) = NewPickedBoat(roster);
            yield return null;

            boat.enabled = false;                        // moored / the player is ashore
            Assert.IsFalse(picker.IsAtHelm);

            yield return null;                           // Update runs; the key is not pressed either way
            Assert.AreSame(roster[0], boat.Hull,
                "the picker must not re-skin a boat nobody is driving");
        }

        // ---- (3) the outboard, drawn from a live helm ---------------------------------------------

        [UnityTest]
        public IEnumerator ThePuntsOutboard_SwivelsAtHerOwnAuthority_AndCentresWhenTheHelmIsDropped()
        {
            // The punt's engine through a real LateUpdate — the same contract as the skiff's below, but on
            // HER sheets and HER ±32°, so the authority really does survive the trip from the asset.
            var hull = LoadHull("PuntUpgraded");
            var (go, boat, _) = NewBoat(hull, Vector3.zero);
            var sr = go.AddComponent<SpriteRenderer>();
            var rig = BoatHullSkinner.ApplyHull(go, sr, hull, boat);
            Assert.IsNotNull(rig.Motor, "precondition: the punt wears her tiller outboard");
            Assert.AreEqual(32f, rig.Motor.MaxSteerDegrees, 0.001f,
                "…at the authority her own sheets bake, not the skiffs' 30");
            Assert.AreEqual(OutboardMotorLayer.MotorFit.Single, rig.Motor.Fit, "…and only one of them");

            int centre = OutboardMotorMath.CenterColumn(OutboardMotorMath.SteerColumns);
            int hardPort = 0;

            // A DURATION, never a frame count — budgeted from the layer's own cadence (see SpinUntil).
            float cadence = SteerColumnsPerSecond(rig.Motor);
            float budget = (Mathf.Abs(centre - hardPort) / cadence) * 4f;

            yield return null;
            Assert.AreEqual(centre, rig.Motor.SteerColumn, "she wakes dead ahead");

            boat.SetControl(1f, -1f);                    // hard a-port
            yield return SpinUntil(() => rig.Motor.SteerColumn == hardPort, budget);
            Assert.AreEqual(hardPort, rig.Motor.SteerColumn,
                "the tiller goes hard over to port off the helm alone");

            boat.enabled = false;                        // the player steps off, mid-turn
            yield return SpinUntil(() => rig.Motor.SteerColumn == centre, budget);

            // Note the helm is STILL hard-over: Stop() clears _steer, disabling does not. So she can only
            // come back by the layer refusing to read an unmanned helm — the #205 gate.
            Assert.AreEqual(centre, rig.Motor.SteerColumn,
                "a dropped helm centres the punt's engine too — she must never sit frozen hard-over (#205)");
        }

        [UnityTest]
        public IEnumerator Outboard_SwivelsFromTheLiveHelm_AndCentresWhenTheHelmIsDropped()
        {
            // The PULL, end to end, through a real LateUpdate — the thing an EditMode test structurally
            // cannot reach. Nobody writes the motor's helm: it sources BoatController.Steer itself.
            var hull = LoadHull("SportSkiff");
            var (go, boat, _) = NewBoat(hull, Vector3.zero);
            var sr = go.AddComponent<SpriteRenderer>();
            var rig = BoatHullSkinner.ApplyHull(go, sr, hull, boat);
            Assert.IsNotNull(rig.Motor, "precondition: the sport skiff wears her outboard");

            int centre = OutboardMotorMath.CenterColumn(OutboardMotorMath.SteerColumns);
            // Hard a-starboard is the sheet's LAST column, derived through the same public mapping the layer
            // uses rather than spelled "8" — the sheets' column count is an art fact that may yet grow.
            int hardStarboard = OutboardMotorMath.ColumnForSteerDegrees(
                OutboardMotorMath.MaxSteerDegrees, OutboardMotorMath.SteerColumns, OutboardMotorMath.MaxSteerDegrees);

            // The swivel is rate-limited, so the wait must be a DURATION, never a frame count. Budget the full
            // centre→hard-over travel at the layer's own cadence, times a slack factor so this is decided by the
            // engine's behaviour and never by a knife-edge on the clock. SpinUntil returns as soon as she
            // arrives, so the slack costs nothing when the product is healthy — it is only ever spent failing.
            float cadence = SteerColumnsPerSecond(rig.Motor);
            float fullSwivel = Mathf.Abs(hardStarboard - centre) / cadence;
            float budget = fullSwivel * 4f;

            yield return null;
            Assert.AreEqual(centre, rig.Motor.SteerColumn, "she wakes dead ahead, never on a stale hard-over");

            boat.SetControl(1f, 1f);                     // hard a-starboard
            yield return SpinUntil(() => rig.Motor.SteerColumn == hardStarboard, budget);

            // DECISIVE, not one column past centre. The old assertion (Greater than centre) only needed half a
            // column of travel and was therefore a coin-flip on frame rate; requiring the FULL hard-over means a
            // broken cadence cannot squeak past, and it earns the precondition the drop test needs below.
            Assert.AreEqual(hardStarboard, rig.Motor.SteerColumn,
                $"{fullSwivel:0.00}s of swivel ({cadence:0.#} columns/sec) must walk the engine from dead ahead " +
                "to hard a-starboard");
            Assert.Greater(rig.Motor.SteerColumn, centre,
                "the engine swung to starboard off the WHEEL alone — no push, no driving system writing it");

            // …and THAT is what makes the next assertion mean something. Before this test waited on time it
            // never got off centre in CI, so "a dropped helm centres the engine" passed by never having left —
            // the #205 guard was vacuous. It can only be reached now from a genuinely hard-over engine.
            boat.enabled = false;                        // the player steps off, mid-turn
            yield return SpinUntil(() => rig.Motor.SteerColumn == centre, budget);

            // Note the helm is STILL hard-over: BoatController.Stop() clears _steer, disabling it does not. So
            // the engine can only come back by the layer refusing to read an unmanned helm (IsHelmManned) —
            // delete that gate and this fails, which is exactly the regression #205 fixed.
            Assert.AreEqual(centre, rig.Motor.SteerColumn,
                "a dropped helm centres the engine — an empty skiff must never sit frozen hard-over (#205)");
        }
    }
}
