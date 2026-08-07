using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// The end-to-end guard on the owner's 2026-08-07 read — <i>"the wake originates at the CENTRE of the
    /// hull, obvious when turning"</i> — through the REAL deposit path: the self-installing
    /// <see cref="BoatWakeEmitter"/>, a real <see cref="BoatController"/>, real ticks, real pooled
    /// renderers.
    ///
    /// <para><b>Why this cannot be an EditMode test.</b> The geometry is pinned headlessly in
    /// <c>WakeDispersalTests</c>, but the thing that actually went wrong lives in a private method of a
    /// private nested class inside a component that installs itself from
    /// <c>RuntimeInitializeOnLoadMethod</c> and only lays foam on its own throttled tick. None of that
    /// exists in EditMode (<c>editmode-has-no-onenable</c>). What is observable from outside — and what
    /// the owner was looking at — is HOW MUCH FOAM IS ON SCREEN, so that is what this measures: the count
    /// of live foam renderers under each boat's own wake root.</para>
    ///
    /// <para><b>The comparison is between two boats in one run</b>, at the same speed for the same time,
    /// differing only in that one is turning. That controls for pool size, tick timing, recycle order and
    /// lifetime — none of which a bare absolute count could be trusted against
    /// (<c>rendering-guards-rot-two-ways</c>: no absolute bars).</para>
    /// </summary>
    public class WakeDepositPlayTests
    {
        private GameObject _straight;
        private GameObject _turning;
        private GameObject _ownEmitter;

        /// <summary>
        /// The emitter installs itself once per PLAY SESSION from <c>RuntimeInitializeOnLoadMethod</c> and
        /// guards that with a static flag — so by the time a late test class runs, an earlier test that
        /// tore down DontDestroyOnLoad objects can have taken the host away with no chance of it coming
        /// back. Stand one up ourselves when it is missing rather than depending on suite order (and take
        /// it down again, so the next class inherits nothing from us).
        /// </summary>
        private void EnsureEmitter()
        {
            var existing = Object.FindObjectsByType<BoatWakeEmitter>(FindObjectsInactive.Include,
                                                                     FindObjectsSortMode.None);
            if (existing != null && existing.Length > 0) return;
            _ownEmitter = new GameObject("BoatWakeEmitter[test]");
            _ownEmitter.AddComponent<BoatWakeEmitter>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_straight != null) Object.Destroy(_straight);
            if (_turning != null) Object.Destroy(_turning);
            if (_ownEmitter != null) Object.Destroy(_ownEmitter);
            GameServices.Reset();
        }

        /// <summary>
        /// A hull the wake can grade: 7 m — the work skiff the owner was driving when he reported this.
        /// Built in memory rather than loaded, so the test does not depend on the owner's local assets
        /// (the untracked-asset class of red).
        /// </summary>
        private static BoatHullDef Skiff()
        {
            var hull = ScriptableObject.CreateInstance<BoatHullDef>();
            hull.Id = "boat.test_skiff";
            hull.LengthMeters = 7f;
            hull.MassKg = 950f;
            // Afloat, deliberately. With no environment service the controller sees water depth
            // (seabed + tide) = 0, so ANY draught reads AGROUND — and an aground boat lays no wake at
            // all, which would make every count below zero for a reason that has nothing to do with the
            // trail. Grounding has its own tests; this one is about where the foam goes.
            hull.DraughtMeters = 0f;
            return hull;
        }

        private static GameObject MakeBoat(string name)
        {
            var go = new GameObject(name);
            var boat = go.AddComponent<BoatController>();      // RequireComponent adds the Rigidbody2D
            boat.SetHull(Skiff());
            var rb = go.GetComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.linearDamping = 0f;
            rb.angularDamping = 0f;
            return go;
        }

        /// <summary>Live foam renderers under this boat's wake root — what the player actually sees.
        /// Found by the rig's own naming rather than by reaching into its privates.</summary>
        private static int LiveFoam(string boatName)
        {
            var root = GameObject.Find($"Wake[{boatName}]");
            if (root == null) return -1;
            int live = 0;
            foreach (Transform child in root.transform)
                if (child.name == "foam" && child.gameObject.activeSelf) live++;
            return live;
        }

        /// <summary>Total pooled foam slots the rig built — separates "no rig / no pool" from "a pool that
        /// was never fed", which are very different failures.</summary>
        private static int CountSlots(string boatName)
        {
            var root = GameObject.Find($"Wake[{boatName}]");
            if (root == null) return -1;
            int slots = 0;
            foreach (Transform child in root.transform) if (child.name == "foam") slots++;
            return slots;
        }

        /// <summary>
        /// Drive both boats for <paramref name="seconds"/> of game time at the SAME speed, re-asserting pose and
        /// velocity every frame so the controller's own drag cannot slew one of them relative to the
        /// other. The turning boat holds her speed and adds heading rate — which is precisely the case
        /// the owner screenshotted.
        /// </summary>
        private IEnumerator DriveBoth(float seconds, float speed, float turnDegPerSec)
        {
            var straightRb = _straight.GetComponent<Rigidbody2D>();
            var turningRb = _turning.GetComponent<Rigidbody2D>();
            float heading = 0f;

            // ⚠️ SECONDS, not frames. A headless batchmode frame here is well under a millisecond, so the
            // obvious "run 90 frames" drove the boats 0.3 m and the 30 Hz emitter barely ticked once —
            // the trail had nothing to lay and the whole comparison read zero
            // (playmode-frame-count-is-not-time). The emitter's clock is real time; so is this loop's.
            for (float elapsed = 0f; elapsed < seconds; )
            {
                float dt = Time.deltaTime;
                elapsed += dt;

                // The POSE is advanced by hand rather than left to the physics step. The controller's own
                // drag would slew the two hulls differently, and a frame in which FixedUpdate does not run
                // would move them not at all — either would make the comparison a measurement of the
                // integrator rather than of the wake (playmode-frame-count-is-not-time). The rigidbody
                // velocity is set alongside because that is what the emitter's speed gate reads.
                _straight.transform.rotation = Quaternion.identity;              // bow = +Y
                _straight.transform.position += new Vector3(0f, speed * dt, 0f);
                straightRb.linearVelocity = new Vector2(0f, speed);

                heading += turnDegPerSec * dt;
                _turning.transform.rotation = Quaternion.Euler(0f, 0f, heading); // bow swings
                Vector2 course = _turning.transform.up * speed;
                _turning.transform.position += (Vector3)(course * dt);
                turningRb.linearVelocity = course;

                yield return null;
            }
        }

        /// <summary>
        /// Turning at speed must lay the COURSE's worth of foam, not the STERN SWING's worth.
        ///
        /// <para>The trail's deposit rate came from the stern anchor's swept segment, and the anchor sits
        /// half a hull aft of the origin, so a turn added <c>ω·r</c> of pure swing to the distance the
        /// emitter thought she had covered. On this hull at this rate that segment is over TWICE her
        /// actual travel — twice the deposits, strung along an arc about amidships, which is the wake the
        /// owner saw coming out of the middle of his boat.</para>
        ///
        /// <para><b>The bound is computed from the shipped config, not typed in.</b> The default keeps a
        /// quarter of the swing on purpose (a transom kicking out does throw water), so "no more foam at
        /// all" would be the wrong claim and a hand-picked ratio would expire the moment the owner retunes
        /// <c>SternSwingFraction</c> (<c>rendering-guards-rot-two-ways</c>: no absolute bars). Instead both
        /// candidate ratios are derived here from the same pure math the emitter uses, and the measured
        /// one simply has to land nearer the course than the swing.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator TurningAtSpeed_LaysTheCoursesWorthOfFoam_NotTheSternSwingsWorth()
        {
            EnsureEmitter();
            _straight = MakeBoat("WakeStraight");
            _turning = MakeBoat("WakeTurning");

            // The emitter rescans at 1 Hz and only then builds a rig per boat; give it the scan.
            float waited = 0f;
            while (LiveFoam("WakeStraight") < 0 && waited < 3f) { waited += Time.deltaTime; yield return null; }
            Assert.GreaterOrEqual(LiveFoam("WakeStraight"), 0, "the straight boat never got a wake rig.");
            Assert.GreaterOrEqual(LiveFoam("WakeTurning"), 0, "the turning boat never got a wake rig.");

            const float speed = 6f;
            const float turnDegPerSec = 180f;
            yield return DriveBoth(seconds: 2.5f, speed: speed, turnDegPerSec: turnDegPerSec);

            int straightFoam = LiveFoam("WakeStraight");
            int turningFoam = LiveFoam("WakeTurning");

            var boat = _straight.GetComponent<BoatController>();
            Assert.Greater(straightFoam, 0,
                "a boat making 6 knots-ish must be laying foam at all, or this comparison is vacuous. " +
                $"(speed={boat.Velocity.magnitude:0.##} aground={boat.IsAground} " +
                $"hull={(boat.Hull != null ? boat.Hull.Id : "null")} " +
                $"movedY={_straight.transform.position.y:0.##} slots={CountSlots("WakeStraight")})");

            // The two candidate rates, per unit tick, from the emitter's own geometry. The boats are
            // unskinned, so the anchor is un-foreshortened and its arm is exactly half the hull plus the
            // nudge — the same number SternAnchor walks back.
            var trail = WakeTrailConfig.Default;
            float arm = 7f * 0.5f + trail.DepositAsternOffset;
            var travel = new Vector2(0f, speed);                                   // course made good
            var swept = travel + new Vector2(arm * turnDegPerSec * Mathf.Deg2Rad, 0f); // + the stern's swing
            float sweptRatio = swept.magnitude / travel.magnitude;                 // what the defect laid
            float trackRatio = WakeTrailMath.TrackVector(travel, swept, trail.SternSwingFraction).magnitude
                               / travel.magnitude;                                 // what the fix lays
            float measured = turningFoam / (float)straightFoam;

            Assert.Less(trackRatio, sweptRatio,
                "the blend must actually pull the laid rate back toward the course, or there is nothing " +
                "for this test to detect. (Set SternSwingFraction to 1 and this fails first, by design.)");
            Assert.Less(Mathf.Abs(measured - trackRatio), Mathf.Abs(measured - sweptRatio),
                $"turning laid {turningFoam} against {straightFoam} straight (ratio {measured:0.###}), which " +
                $"is nearer the STERN SWING's {sweptRatio:0.###} than the course's {trackRatio:0.###}. The " +
                "swing is being counted as distance travelled again — the defect the owner reported as the " +
                "wake coming from the centre of the hull.");
        }

        /// <summary>
        /// The trail is still LAID AT THE TRANSOM, not at the boat's middle: every live foam puff sits
        /// abaft the hull's midpoint. This is the other half of the owner's item — the fix moved the
        /// deposit RATE and GEOMETRY onto the course, and this pins that it did not also drag the
        /// deposit POSITION forward onto the hull while doing so.
        /// </summary>
        [UnityTest]
        public IEnumerator EveryLaidPuff_SitsAbaftTheHullsMidpoint()
        {
            EnsureEmitter();
            _straight = MakeBoat("WakeAftCheck");
            _turning = MakeBoat("WakeAftIdle");

            float waited = 0f;
            while (LiveFoam("WakeAftCheck") < 0 && waited < 3f) { waited += Time.deltaTime; yield return null; }
            Assert.GreaterOrEqual(LiveFoam("WakeAftCheck"), 0, "no wake rig was built for the boat.");

            // Run her due north at a steady clip so "abaft" is simply "below her on screen".
            yield return DriveBoth(seconds: 2.0f, speed: 6f, turnDegPerSec: 0f);

            var root = GameObject.Find("Wake[WakeAftCheck]");
            Assert.IsNotNull(root);
            float boatY = _straight.transform.position.y;
            int checkedPuffs = 0;

            foreach (Transform child in root.transform)
            {
                if (child.name != "foam" || !child.gameObject.activeSelf) continue;
                checkedPuffs++;
                Assert.Less(child.position.y, boatY,
                    "a foam puff was laid at or FORWARD of the boat's origin (amidships). The trail must " +
                    "be born at the transom and left behind her.");
            }

            Assert.Greater(checkedPuffs, 0, "no live foam to check — the run laid nothing.");
        }
    }
}
