using System.Collections;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// ⭐⭐ <b>THE SLOOP SAILS — the shipped hull, in the real physics loop, against her own polar
    /// (P1).</b>
    ///
    /// <para><b>Why this fixture has to exist, and what it caught.</b> Every EditMode guard on the sail
    /// drive checks a pure function against a file. None of them can see the one thing that decides
    /// whether the boat MOVES: what actually resists her once a <c>Rigidbody2D</c> is under her.
    /// <c>SailDrive.ThrustFor</c> originally balanced <c>BoatHullDef.ForwardDrag</c> alone, and its
    /// EditMode guard asserted <c>ThrustFor(t, d) / d == t</c> — which only inverts the function's own
    /// multiply and passes for ANY resistance, including the wrong one. The body also carries
    /// <see cref="BoatController.HullLinearDamping"/> against a mass of <c>MassKg/100</c>, which on a
    /// 4 875 kg sloop is ~5× the hull drag: she sailed at 17 % of her polar — 0.8 kn on a reach her own
    /// data puts at 6.2 — and read as becalmed in a working breeze rather than as an arithmetic slip.
    /// Only a fixture that runs the real body can adjudicate that, so the numbers below are asserted
    /// against the SHIPPED polar rather than against the drive's own opinion of itself.</para>
    ///
    /// <para><b>Time discipline.</b> Nothing here counts frames as seconds
    /// (<c>playmode-frame-count-is-not-time</c>). Physics steps are counted, and a step IS exactly
    /// <c>Time.fixedDeltaTime</c> of simulated time; the assertions are made on a SETTLED speed, proved
    /// settled by measuring that it has stopped changing, not by assuming a duration.</para>
    ///
    /// <para><b>Headless-safe:</b> no camera, nothing renders. The sea is glass (seaState01 = 0) so the
    /// seakeeping push cannot contribute to a speed this fixture attributes to the sails.</para>
    /// </summary>
    public class SailDrivePlayTests
    {
        const string Sloop30 = "Assets/_Project/Data/Boats/Sloop30.asset";

        /// <summary>12 kn of true wind — the charter's reference breeze.</summary>
        const float TwsKn = 12f;

        /// <summary>The shipped polar's own numbers for the sloop 30 at 12 kn true. Not tuned here: if
        /// one of these moves, the POLAR moved, and the drive is supposed to follow it.</summary>
        const float CloseHauledTwa = 45f, CloseHauledKn = 4.97f;
        const float BroadReachTwa = 135f, BroadReachKn = 6.18f;

        /// <summary>
        /// How far outside the no-go the close-hauled legs are actually sailed. The polar's 45° row is
        /// EXACTLY the hull's <c>NoGoTrueWindDeg</c> (45), and <see cref="SailDrive.TargetSpeedKn"/>
        /// gates on <c>twa &lt; noGo</c>. The true angle she reads is re-derived every tick from
        /// <c>transform.up</c> and the wind vector through two float atan2 calls, so sailing at a
        /// nominal 45° lands on 45 ± a few float ulps: on the + side she sails, on the − side the
        /// drive returns 0 kn and reports in irons, with the heading still printing 47.0 and the angle
        /// 45.0. Which side she lands on depends on the exact pose path the body took before the hold,
        /// so the leg was intermittent. A tenth of a degree is thousands of ulps clear of the gate and
        /// moves the polar's answer by about 0.006 kn (the 45→52 slope at 12 kn is 0.063 kn/°), far
        /// inside the 5 % the speed assertion allows. The expected speed stays the polar's 45° number.
        /// </summary>
        const float NoGoEdgeMarginDeg = 0.1f;
        const float CloseHauledSailedTwa = CloseHauledTwa + NoGoEdgeMarginDeg;

        GameObject _root;

        sealed class GlassSea : IEnvironmentService
        {
            public Vector2 Wind;
            public int WorldSeed => 0;
            public TideProfile ActiveTideProfile { get; set; }
            public EnvironmentSample Sample() => new EnvironmentSample(
                Wind, Vector2.zero, tideHeight: 0f, HiddenHarbours.Core.SeaState.Calm,
                visibility: 1f, seaState01: 0f);
            public float TideHeightAt(double totalSeconds) => 0f;
            public float WaterLevelAt(double totalSeconds) => 0f;
        }

        /// <summary>
        /// A settle read under this is not a speed the hull sailed to: she was HELD. Both this and
        /// <see cref="ReachedKn"/> sit far outside 5 % of every polar number this fixture asserts (the
        /// smallest is 4.97 kn), so the precondition that uses them can only re-word a failure the polar
        /// assertion would make anyway; it can never fail a case that assertion would pass.
        /// </summary>
        const float HeldKn = 0.1f;

        /// <summary>The fastest window must reach at least this, or she never got going at all.</summary>
        const float ReachedKn = 0.25f;

        /// <summary>
        /// Start every test from the same empty service state the legs after a <see cref="TearDown"/>
        /// already get. Without this the FIRST leg of a test inherits whatever the previous fixture in
        /// the run left in <see cref="GameServices"/> — a registered <c>TidalTerrain</c>, a <c>Clock</c>,
        /// a <c>Config</c> — none of which <see cref="BuildSloop"/> sets and all of which the controller
        /// reads every tick (a terrain alone puts land under a hull sailing out from the origin, and
        /// aground she takes the grounded-slowdown drag). The second leg of the polar test, run after
        /// this fixture's own reset, never saw those; the first one could.
        /// </summary>
        [SetUp]
        public void SetUp() => GameServices.Reset();

        [TearDown]
        public void TearDown()
        {
            GameServices.Reset();
            if (_root != null) Object.DestroyImmediate(_root);
            _root = null;
        }

        /// <summary>
        /// The wind vector that puts <paramref name="twaDeg"/> of true wind on the bow of a boat heading
        /// <paramref name="headingDeg"/> — in the SIM's convention, where <c>WindVector</c> points
        /// DOWNWIND. Same construction as the EditMode fixture's <c>FeelOf</c>, so the two cannot drift.
        /// </summary>
        static Vector2 WindFor(float twaDeg, float twsKn, float headingDeg)
        {
            float fromDeg = headingDeg + twaDeg;
            Vector2 from = new Vector2(Mathf.Sin(fromDeg * Mathf.Deg2Rad), Mathf.Cos(fromDeg * Mathf.Deg2Rad));
            return -from * SailDrive.ToMetresPerSecond(twsKn);
        }

        /// <summary>
        /// The real committed sloop, afloat, with the wind set to a stated angle off her bow. Heading is
        /// deliberately NOT zero: at heading 0 a sign error in the wind frame cancels against a sign
        /// error in the relative bearing and every angle below would pass on a boat that sails on the
        /// wrong tack (the same trap the EditMode fixture guards at 47°).
        /// </summary>
#if UNITY_EDITOR
        IEnumerator BuildSloop(float twaDeg, float headingDeg, float twsKn, System.Action<BoatController,
                               GlassSea> ready)
        {
            var hull = AssetDatabase.LoadAssetAtPath<BoatHullDef>(Sloop30);
            Assert.IsNotNull(hull, $"{Sloop30} is missing — this fixture asserts the REAL shipped sloop.");
            Assert.IsTrue(hull.HasSailPlan,
                $"{Sloop30} does not declare a sail plan (Propulsion = Sail with a usable SailPolar), so " +
                "the sail drive would never run and every number below would be measuring the auxiliary.");

            var sea = new GlassSea { Wind = WindFor(twaDeg, twsKn, headingDeg) };
            GameServices.Environment = sea;

            _root = new GameObject("Sloop30");
            _root.transform.rotation = Quaternion.Euler(0f, 0f, -headingDeg);
            var boat = _root.AddComponent<BoatController>();   // RequireComponent adds the Rigidbody2D
            yield return null;                                  // Awake caches the body
            boat.SetHull(hull);
            yield return null;

            ready(boat, sea);
        }
#endif

        /// <summary>
        /// A helmsman who holds her on the stated heading. With her sails drawing she carries weather
        /// helm (<c>BoatHullDef.SailWeatherHelm</c>, felt through <c>GameConfig.SeaYaw</c>): left alone
        /// she rounds up into the wind, which is the feel the game wants, and a sailor answers it with
        /// the helm. These fixtures measure the polar AT a stated true-wind angle, so the angle must
        /// survive the run: at 45° she sits exactly on the no-go edge, and an unheld bow luffs into it,
        /// loses drive, falls off and luffs again, settling well under the polar. Holding the heading
        /// removes only the turn: the sea yaw is a torque and nothing else, so it cannot add or remove
        /// a single newton along her course, and with the turn held this is the pre-yaw physics exactly.
        /// </summary>
        static void HoldHeading(BoatController boat, float headingDeg)
        {
            var body = boat.GetComponent<Rigidbody2D>();
            body.angularVelocity = 0f;
            body.rotation = -headingDeg;
            body.constraints |= RigidbodyConstraints2D.FreezeRotation;
        }

        /// <summary>
        /// Step the real physics until her speed stops changing, and answer the settled speed in knots.
        /// The settle is MEASURED (two windows that agree to 0.5 %), never assumed from a step count —
        /// so this cannot silently report a mid-acceleration number if the hull is ever made heavier.
        /// </summary>
        static IEnumerator SettleAndRead(Rigidbody2D rb, System.Action<float> settledKn, SettleLog log = null)
        {
            float previous = -1f;
            for (int window = 0; window < 40; window++)
            {
                for (int step = 0; step < 60; step++) yield return new WaitForFixedUpdate();

                float now = SailDrive.ToKnots(rb.linearVelocity.magnitude);
                log?.WindowKn.Add(now);
                if (previous >= 0f && Mathf.Abs(now - previous) <= Mathf.Max(0.002f, previous * 0.005f))
                {
                    settledKn(now);
                    yield break;
                }
                previous = now;
            }
            Assert.Fail($"she never settled — still changing after 2400 physics steps, last read " +
                        $"{previous:0.00} kn. Either the drive is oscillating or the hull's time " +
                        "constant has grown past what this fixture waits for.");
        }

        /// <summary>The speed at the end of each 60-step settle window, in order. Recording a float per
        /// window is the whole green-path cost; everything else is built only when a test fails.</summary>
        sealed class SettleLog
        {
            public readonly List<float> WindowKn = new List<float>(40);

            public float PeakKn
            {
                get
                {
                    float peak = 0f;
                    foreach (float kn in WindowKn) peak = Mathf.Max(peak, kn);
                    return peak;
                }
            }
        }

        /// <summary>
        /// "Settled" near zero, or never reached a real speed: the two windows that agreed were two windows
        /// in which something held her, not a balance of drive against resistance. Such a read says nothing
        /// about the drive's arithmetic, so it must not be reported as if it did.
        /// </summary>
        static bool WasHeld(SettleLog log, float settledKn) => settledKn < HeldKn || log.PeakKn < ReachedKn;

        /// <summary>
        /// What the scene shows when she was held: built ONLY on that failure. Names her own state, every
        /// contact on her colliders, every other body and boat in every loaded scene, and the services the
        /// controller reads — so a CI log alone can say who stopped her.
        /// </summary>
        static string DescribeHold(BoatController boat, string leg, float twa, float polarKn, float settledKn,
                                   SettleLog log)
        {
            const int MaxListed = 40;
            var sb = new StringBuilder(2048);
            var rb = boat.GetComponent<Rigidbody2D>();
            float stepsRun = log.WindowKn.Count * 60f;

            sb.Append($"{leg} at twa {twa}° in {TwsKn} kn her polar says {polarKn} kn, but she was HELD, not " +
                      $"sailed: the settle read {settledKn:0.000} kn (held under {HeldKn} kn) and her fastest " +
                      $"window was {log.PeakKn:0.000} kn (a real start reaches {ReachedKn} kn). This is not " +
                      "the damping balance; something in the scene stopped her. What the scene shows:\n");

            sb.Append($"  windows (kn per 60 steps, {stepsRun:0} steps = {stepsRun * Time.fixedDeltaTime:0.0} s " +
                      "simulated):");
            foreach (float kn in log.WindowKn) sb.Append($" {kn:0.000}");
            sb.Append('\n');

            sb.Append($"  her: '{boat.name}' scene '{boat.gameObject.scene.name}' pos {rb.position} heading " +
                      $"{-rb.rotation:0.0}° velocity {rb.linearVelocity} angularVelocity {rb.angularVelocity:0.00}\n");
            sb.Append($"  body: bodyType {rb.bodyType} simulated {rb.simulated} awake {rb.IsAwake()} " +
                      $"constraints {rb.constraints} linearDamping {rb.linearDamping} angularDamping " +
                      $"{rb.angularDamping} mass {rb.mass} gravityScale {rb.gravityScale}\n");
            var anchor = boat.GetComponent<BoatAnchor>();
            var mooring = boat.GetComponent<BoatMooring>();
            sb.Append($"  controller: enabled {boat.isActiveAndEnabled} aground {boat.IsAground} " +
                      $"moored {(mooring != null ? mooring.State.ToString() : "none")} anchor " +
                      $"{(anchor != null ? anchor.State.ToString() : "none")} autopilot {boat.AutopilotEngaged} " +
                      $"sailTarget {boat.LastSailTargetKn:0.00} kn trueAngle " +
                      $"{boat.LastSailWind.TrueAngleDeg:0.0}° inIrons {boat.DrawnInIrons} " +
                      $"hull '{(boat.Hull != null ? boat.Hull.name : "null")}'\n");

            var contacts = new List<ContactPoint2D>();
            rb.GetContacts(contacts);
            sb.Append($"  contacts on her body: {contacts.Count}\n");
            for (int i = 0; i < contacts.Count && i < MaxListed; i++)
            {
                Collider2D other = contacts[i].collider != null && contacts[i].collider.attachedRigidbody != rb
                    ? contacts[i].collider : contacts[i].otherCollider;
                sb.Append("    ").Append(DescribeCollider(other)).Append('\n');
            }
            var overlaps = new List<Collider2D>();
            foreach (var mine in boat.GetComponentsInChildren<Collider2D>())
            {
                overlaps.Clear();
                mine.Overlap(new ContactFilter2D().NoFilter(), overlaps);
                sb.Append($"  overlapping her {mine.GetType().Name} '{mine.name}' (trigger {mine.isTrigger}, " +
                          $"enabled {mine.enabled}): {overlaps.Count}\n");
                for (int i = 0; i < overlaps.Count && i < MaxListed; i++)
                    sb.Append("    ").Append(DescribeCollider(overlaps[i])).Append('\n');
            }

            var bodies = Object.FindObjectsByType<Rigidbody2D>(FindObjectsSortMode.None);
            sb.Append($"  other Rigidbody2D in loaded scenes: {bodies.Length - 1}\n");
            int listed = 0;
            foreach (var body in bodies)
            {
                if (body == rb) continue;
                if (listed++ >= MaxListed) { sb.Append("    ...\n"); break; }
                sb.Append($"    '{body.name}' root '{body.transform.root.name}' scene '{body.gameObject.scene.name}' " +
                          $"pos {body.position} bodyType {body.bodyType} simulated {body.simulated}\n");
            }
            var boats = Object.FindObjectsByType<BoatController>(FindObjectsSortMode.None);
            sb.Append($"  BoatControllers in loaded scenes: {boats.Length}\n");
            listed = 0;
            foreach (var b in boats)
            {
                if (listed++ >= MaxListed) { sb.Append("    ...\n"); break; }
                sb.Append($"    '{b.name}' scene '{b.gameObject.scene.name}' pos {b.transform.position} " +
                          $"active {b.isActiveAndEnabled}{(b == boat ? " (her)" : "")}\n");
            }

            sb.Append("  loaded scenes:");
            for (int i = 0; i < SceneManager.sceneCount; i++) sb.Append($" '{SceneManager.GetSceneAt(i).name}'");
            sb.Append('\n');

            IEnvironmentService env = GameServices.Environment;
            if (env != null)
            {
                EnvironmentSample s = env.Sample();
                sb.Append($"  environment: {env.GetType().Name} wind {s.WindVector} current {s.CurrentVector} " +
                          $"tide {s.TideHeight} seaState01 {s.SeaState01}\n");
            }
            else sb.Append("  environment: null\n");
            sb.Append($"  services: tidalTerrain {TypeOf(GameServices.TidalTerrain)} stillWater " +
                      $"{TypeOf(GameServices.StillWater)} clock {TypeOf(GameServices.Clock)}" +
                      $"{(GameServices.Clock != null ? $" at {GameServices.Clock.TotalSeconds:0.0} s" : "")} config " +
                      $"{(GameServices.Config != null ? GameServices.Config.name : "null")} region " +
                      $"'{GameServices.CurrentRegionId}'\n");
            sb.Append($"  time: timeScale {Time.timeScale} fixedDeltaTime {Time.fixedDeltaTime} physics2D " +
                      $"simulationMode {Physics2D.simulationMode}");
            return sb.ToString();
        }

        static string TypeOf(object o) => o != null ? o.GetType().Name : "null";

        static string DescribeCollider(Collider2D c)
        {
            if (c == null) return "(null collider)";
            Rigidbody2D owner = c.attachedRigidbody;
            return $"{c.GetType().Name} '{c.name}' root '{c.transform.root.name}' scene '{c.gameObject.scene.name}' " +
                   $"pos {(Vector2)c.transform.position} trigger {c.isTrigger} body " +
                   $"{(owner != null ? $"'{owner.name}' {owner.bodyType}" : "static")}";
        }

        // =============================================================================================

        /// <summary>
        /// ⭐⭐ <b>Close-hauled and on a broad reach, she makes the speed her own polar promises.</b>
        ///
        /// <para>Tolerance is 5 %, and it is not slack for its own sake: the hull also takes the direct
        /// wind shove (<c>WindExposure</c>) which the polar does not model, and that pushes with her on
        /// a reach and against her on a beat. 5 % is comfortably wider than that term (~0.1 %) and
        /// comfortably narrower than the 83 % error the missing damping term produced — which is the
        /// gap this test exists to keep closed.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator SheMakesHerPolarSpeed_CloseHauledAndOnABroadReach()
        {
#if !UNITY_EDITOR
            Assert.Ignore("Needs the AssetDatabase: this asserts the REAL committed sloop.");
            yield break;
#else
            foreach (var leg in new[] { (CloseHauledSailedTwa, CloseHauledKn, "close-hauled"),
                                        (BroadReachTwa, BroadReachKn, "on a broad reach") })
            {
                BoatController boat = null;
                yield return BuildSloop(leg.Item1, headingDeg: 47f, twsKn: TwsKn, ready: (b, _) => boat = b);
                // The polar is a speed AT an angle: hold her on it against her weather helm.
                HoldHeading(boat, 47f);

                float settled = 0f;
                var log = new SettleLog();
                yield return SettleAndRead(boat.GetComponent<Rigidbody2D>(), kn => settled = kn, log);

                // Precondition: she sailed and settled, rather than being held still for two windows
                // (which SettleAndRead's "two windows agree" would otherwise accept as settled). Only a
                // read that fails the polar assertion below anyway can trip this; it names who held her.
                if (WasHeld(log, settled))
                    Assert.Fail(DescribeHold(boat, leg.Item3, leg.Item1, leg.Item2, settled, log));

                Assert.AreEqual(leg.Item2, settled, leg.Item2 * 0.05f,
                    $"{leg.Item3} at twa {leg.Item1}° in {TwsKn} kn her polar says {leg.Item2} kn; the " +
                    $"hull settled at {settled:0.00} kn. If this is low by roughly the ratio " +
                    "ForwardDrag/(ForwardDrag + damping·MassKg), the drive has stopped balancing the " +
                    "body's own linearDamping — see SailDrive.LinearResistance.");

                Assert.Greater(boat.LastSailTargetKn, 0f, "the drive must have read a target from the polar.");
                // ...and the hold is not hiding a switched-off yaw: her weather helm was asking to bring
                // her up (negative = clockwise, her bow towards the wind on her starboard side) on the
                // very tick she was making her polar speed. A torque costs no speed along her course.
                Assert.Less(boat.LastSeaYawHelmShare, 0f,
                    $"{leg.Item3}: with her sails drawing in {TwsKn} kn her weather helm must be asking to " +
                    "round her up; zero here means the sea yaw never ran and the hold above proves nothing.");
                TearDown();
            }
#endif
        }

        /// <summary>
        /// <b>She makes ground TO WINDWARD</b> — the charter's actual ask, which a speed alone does not
        /// prove: a boat can be fast and still be sailing away from the wind she is supposed to be
        /// beating into. Measured as displacement projected onto the upwind direction.
        /// </summary>
        [UnityTest]
        public IEnumerator CloseHauled_SheMakesGroundToWindward()
        {
#if !UNITY_EDITOR
            Assert.Ignore("Needs the AssetDatabase: this asserts the REAL committed sloop.");
            yield break;
#else
            BoatController boat = null;
            GlassSea sea = null;
            yield return BuildSloop(CloseHauledSailedTwa, headingDeg: 47f, twsKn: TwsKn,
                                    ready: (b, s) => { boat = b; sea = s; });
            // The cos(45°) below is only her course if she is held on it against her weather helm.
            HoldHeading(boat, 47f);

            var rb = boat.GetComponent<Rigidbody2D>();
            float settled = 0f;
            yield return SettleAndRead(rb, kn => settled = kn);

            Vector2 upwind = -sea.Wind.normalized;   // WindVector points downwind
            Vector2 start = rb.position;
            for (int step = 0; step < 300; step++) yield return new WaitForFixedUpdate();
            float madeGood = Vector2.Dot(rb.position - start, upwind);

            Assert.Greater(madeGood, 0f,
                $"close-hauled at twa {CloseHauledTwa}° she must CLOSE with the wind, not fall away from " +
                $"it. She made {madeGood:0.0} m to windward over the run at {settled:0.00} kn — a " +
                "negative number here means the wind frame's sign is inverted and she is sailing the " +
                "opposite tack (invisible to any symmetric speed test).");

            // And the ground made good must be the speed's own upwind component, not a drift: at 45° off
            // the true wind that is cos(45°) of it, within the wind shove's contribution.
            float expected = SailDrive.ToMetresPerSecond(settled) * Mathf.Cos(CloseHauledSailedTwa * Mathf.Deg2Rad)
                             * (300f * Time.fixedDeltaTime);
            Assert.AreEqual(expected, madeGood, Mathf.Max(1f, expected * 0.15f),
                "the ground she makes to windward must be her own speed resolved onto the wind, so this " +
                "cannot pass on a boat that is merely being blown somewhere.");
#endif
        }

        /// <summary>
        /// <b>In the no-go she makes no way, and becalmed she makes none either</b> — the two zeros, in
        /// the real loop. She is allowed to be MOVED (the wind shove is a real force on a real hull);
        /// what she may not do is sail.
        /// </summary>
        [UnityTest]
        public IEnumerator InTheNoGoAndBecalmed_TheSailsGiveHerNothing()
        {
#if !UNITY_EDITOR
            Assert.Ignore("Needs the AssetDatabase: this asserts the REAL committed sloop.");
            yield break;
#else
            // Pinching inside the no-go: 20° off the true wind, well inside the def's 45°.
            BoatController boat = null;
            yield return BuildSloop(20f, headingDeg: 47f, twsKn: TwsKn, ready: (b, _) => boat = b);
            // This arm measures polar gating AT 20 degrees, not the time she takes to pay off.
            // The real sloop now escapes irons; hold the stated heading so the no-go premise survives.
            var body = boat.GetComponent<Rigidbody2D>();
            body.constraints |= RigidbodyConstraints2D.FreezeRotation;
            body.rotation = -47f;
            for (int step = 0; step < 600; step++) yield return new WaitForFixedUpdate();

            Assert.AreEqual(20f, Mathf.Abs(boat.LastSailWind.TrueAngleDeg), 0.01f,
                "the drive guard must still be measuring its stated 20-degree no-go premise");
            Assert.AreEqual(0f, boat.LastSailTargetKn, 1e-4f,
                "inside the no-go the polar hands out no speed at all.");
            float inIrons = SailDrive.ToKnots(boat.GetComponent<Rigidbody2D>().linearVelocity.magnitude);
            Assert.Less(inIrons, 0.5f,
                $"in irons she must lie there; she was making {inIrons:0.00} kn. Anything above the wind " +
                "shove's own drift means the no-go is not gating the thrust.");
            TearDown();

            // Becalmed: the angle is a fine one, there is simply no wind in it.
            yield return BuildSloop(BroadReachTwa, headingDeg: 47f, twsKn: 0f, ready: (b, _) => boat = b);
            for (int step = 0; step < 600; step++) yield return new WaitForFixedUpdate();

            Assert.AreEqual(0f, boat.LastSailTargetKn, 1e-4f, "no wind, no speed, at any angle.");
            Assert.Less(SailDrive.ToKnots(boat.GetComponent<Rigidbody2D>().linearVelocity.magnitude), 0.01f,
                "becalmed on a glass sea nothing moves her at all.");
#endif
        }

        /// <summary>
        /// ⭐ <b>The sabotage arm.</b> Rotate the wind 180° and leave everything else alone: a boat that
        /// was close-hauled is now dead downwind, and her speed MUST change. If it does not, the drive
        /// is not reading the wind direction at all and every green above is vacuous.
        /// </summary>
        [UnityTest]
        public IEnumerator TurnTheWindAround_AndTheSpeedGuardMustFail()
        {
#if !UNITY_EDITOR
            Assert.Ignore("Needs the AssetDatabase: this asserts the REAL committed sloop.");
            yield break;
#else
            BoatController boat = null;
            yield return BuildSloop(CloseHauledTwa + 180f, headingDeg: 47f, twsKn: TwsKn,
                                    ready: (b, _) => boat = b);

            float settled = 0f;
            yield return SettleAndRead(boat.GetComponent<Rigidbody2D>(), kn => settled = kn);

            Assert.That(Mathf.Abs(settled - CloseHauledKn), Is.GreaterThan(CloseHauledKn * 0.05f),
                $"with the wind reversed she settled at {settled:0.00} kn, inside the close-hauled " +
                $"tolerance of {CloseHauledKn} kn. The drive is not distinguishing 45° off the bow from " +
                "225°, so the wind frame is not being read and the speed guards prove nothing.");
#endif
        }
    }
}
