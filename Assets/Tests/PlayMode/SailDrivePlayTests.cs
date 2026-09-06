using System.Collections;
using NUnit.Framework;
using UnityEngine;
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
        /// Step the real physics until her speed stops changing, and answer the settled speed in knots.
        /// The settle is MEASURED (two windows that agree to 0.5 %), never assumed from a step count —
        /// so this cannot silently report a mid-acceleration number if the hull is ever made heavier.
        /// </summary>
        static IEnumerator SettleAndRead(Rigidbody2D rb, System.Action<float> settledKn)
        {
            float previous = -1f;
            for (int window = 0; window < 40; window++)
            {
                for (int step = 0; step < 60; step++) yield return new WaitForFixedUpdate();

                float now = SailDrive.ToKnots(rb.linearVelocity.magnitude);
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
            foreach (var leg in new[] { (CloseHauledTwa, CloseHauledKn, "close-hauled"),
                                        (BroadReachTwa, BroadReachKn, "on a broad reach") })
            {
                BoatController boat = null;
                yield return BuildSloop(leg.Item1, headingDeg: 47f, twsKn: TwsKn, ready: (b, _) => boat = b);

                float settled = 0f;
                yield return SettleAndRead(boat.GetComponent<Rigidbody2D>(), kn => settled = kn);

                Assert.AreEqual(leg.Item2, settled, leg.Item2 * 0.05f,
                    $"{leg.Item3} at twa {leg.Item1}° in {TwsKn} kn her polar says {leg.Item2} kn; the " +
                    $"hull settled at {settled:0.00} kn. If this is low by roughly the ratio " +
                    "ForwardDrag/(ForwardDrag + damping·MassKg), the drive has stopped balancing the " +
                    "body's own linearDamping — see SailDrive.LinearResistance.");

                Assert.Greater(boat.LastSailTargetKn, 0f, "the drive must have read a target from the polar.");
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
            yield return BuildSloop(CloseHauledTwa, headingDeg: 47f, twsKn: TwsKn,
                                    ready: (b, s) => { boat = b; sea = s; });

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
            float expected = SailDrive.ToMetresPerSecond(settled) * Mathf.Cos(CloseHauledTwa * Mathf.Deg2Rad)
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
            for (int step = 0; step < 600; step++) yield return new WaitForFixedUpdate();

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
