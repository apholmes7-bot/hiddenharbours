using System.Collections;
using System.Collections.Generic;
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
    /// ⭐ <b>THE TRIANGLE: a sloop sailed round a course on her rudder alone.</b> The shipped Sloop30,
    /// in the real physics loop, steered ONLY through <see cref="DevBoatInput.DriveSailHelm"/> — the one
    /// write every helm read goes through — by a plain proportional autopilot standing in for the
    /// player's hands. No thrust, trim or sheet is touched by the test: the wind drives her, the polar
    /// sets her speed, the rudder is the whole of the helm.
    ///
    /// <para><b>The course</b> (true wind 12 kn FROM north, a glass sea):
    /// a run on 170 → a GYBE to 290 through the stern → a reach on 290 → harden up to 305,
    /// close-hauled at 55° (ten degrees clear of the 45° no-go, so an autopilot overshoot on the luff is
    /// not mistaken for irons) → put her head to wind until she stops, IN IRONS. Each leg is 8 m made good.
    /// The order runs every turn the short way (310° of turning otherwise): the wall-clock is the
    /// budget, because CI paces PlayMode in real time.</para>
    ///
    /// <para><b>The hull capsule</b> is the player's (1.7 × 4.0 m, PersistentCoreBuilder; the arrival's
    /// greybox hull is the same): <c>SetHull</c> never resizes a collider, so this is the yaw inertia a
    /// sloop swapped onto the player's hull really turns with.</para>
    ///
    /// <para>What this does NOT claim: that she tacks (the no-go stops her, and paying off out of irons
    /// is the next PR's), that her sails are drawn, or that anything reads <c>DrawnInIrons</c> yet.</para>
    /// </summary>
    public class SailTrianglePlayTests
    {
        const string Sloop30 = "Assets/_Project/Data/Boats/Sloop30.asset";
        static readonly Vector2 PlayersHullCapsule = new Vector2(1.7f, 4.0f);

        const float TwsKn = 12f;
        const float LegMetres = 8f;

        /// <summary>The autopilot's proportional band: full helm at this many degrees off course.</summary>
        const float FullHelmAtErrorDeg = 6f;

        /// <summary>A turn is complete when she is this close to the new course…</summary>
        const float TurnSettledDeg = 3f;
        /// <summary>…and swinging slower than this.</summary>
        const float TurnSettledYawDegPerSec = 2f;

        /// <summary>Once settled, a leg must hold within this of its course.</summary>
        const float LegHoldDeg = 5f;

        /// <summary>A gybe passes the wind across the STERN: the true-wind side flips while the wind is
        /// at least this far aft. A flip nearer the bow would be a tack.</summary>
        const float GybeFlipAftOfDeg = 150f;

        const float StoppedKn = 0.5f;

        // Step caps: about twice what a model of this hull and capsule predicts for each phase (a leg
        // ≈ 150 steps, the 120° gybe ≈ 580–680, the 15° luff ≈ 130–150, head-to-wind-and-stop ≈ 470–490).
        const int LegStepCap = 400;
        const int GybeStepCap = 1400;
        const int LuffStepCap = 400;
        const int IronsStepCap = 1100;

        private readonly List<Object> _spawned = new();

        [SetUp]
        public void SetUp() => GameServices.Reset();

        [TearDown]
        public void TearDown()
        {
            GameServices.Reset();
            foreach (var o in _spawned)
                if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        sealed class GlassSea : IEnvironmentService
        {
            public Vector2 Wind;
            public int WorldSeed => 0;
            public TideProfile ActiveTideProfile { get; set; }
            public EnvironmentSample Sample() => new EnvironmentSample(Wind, Vector2.zero, tideHeight: 0f,
                HiddenHarbours.Core.SeaState.Calm, visibility: 1f, seaState01: 0f);
            public float TideHeightAt(double totalSeconds) => 0f;
            public float WaterLevelAt(double totalSeconds) => 0f;
        }

        static float HeadingOf(Transform t) => BoatKinematics.BearingDegrees(t.up);

        static Vector2 Along(float headingDeg)
        {
            float r = headingDeg * Mathf.Deg2Rad;
            return new Vector2(Mathf.Sin(r), Mathf.Cos(r));
        }

#if UNITY_EDITOR
        private BoatController _boat;
        private DevBoatInput _helm;
        private Rigidbody2D _rb;

        /// <summary>One autopilot step: helm toward <paramref name="courseDeg"/>, through the gate, then
        /// one physics step. Returns the heading error AFTER the step.</summary>
        IEnumerator Helm(float courseDeg)
        {
            float err = Mathf.DeltaAngle(HeadingOf(_boat.transform), courseDeg);
            _helm.DriveSailHelm(Mathf.Clamp(err / FullHelmAtErrorDeg, -1f, 1f));
            yield return new WaitForFixedUpdate();
        }

        float ErrorTo(float courseDeg) => Mathf.DeltaAngle(HeadingOf(_boat.transform), courseDeg);
        float SpeedKn => SailDrive.ToKnots(_rb.linearVelocity.magnitude);

        void AssertNotInIrons(string phase, int step)
        {
            Assert.IsFalse(_boat.DrawnInIrons,
                $"{phase}, step {step}: she must not be in irons off the wind (TWA " +
                $"{_boat.LastSailWind.TrueAngleDeg:0.0}°, AWA {_boat.LastSailWind.ApparentAngleDeg:0.0}°, {SpeedKn:0.00} kn)");
        }

        IEnumerator SailLeg(string phase, float courseDeg)
        {
            Vector2 start = _rb.position;
            Vector2 along = Along(courseDeg);
            int steps = 0;
            float worst = 0f;
            while (Vector2.Dot(_rb.position - start, along) < LegMetres)
            {
                Assert.Less(steps, LegStepCap,
                    $"{phase}: she must make good {LegMetres} m on {courseDeg}° inside {LegStepCap} physics steps; " +
                    $"she made {Vector2.Dot(_rb.position - start, along):0.00} m at {SpeedKn:0.00} kn");
                yield return Helm(courseDeg);
                steps++;
                worst = Mathf.Max(worst, Mathf.Abs(ErrorTo(courseDeg)));
                AssertNotInIrons(phase, steps);
            }
            Assert.LessOrEqual(worst, LegHoldDeg,
                $"{phase}: on her rudder alone she must hold {courseDeg}° within {LegHoldDeg}° (worst {worst:0.00}°)");
            Assert.Greater(_boat.LastSailTargetKn, 0f, $"{phase}: the polar drives her on this course");
            Debug.Log($"[SailTriangle] {phase}: {steps} steps, worst error {worst:0.00}°, {SpeedKn:0.00} kn");
        }

        IEnumerator TurnTo(string phase, float courseDeg, int cap, System.Action<int> eachStep = null)
        {
            int steps = 0;
            while (!(Mathf.Abs(ErrorTo(courseDeg)) < TurnSettledDeg
                     && Mathf.Abs(_rb.angularVelocity) < TurnSettledYawDegPerSec))
            {
                Assert.Less(steps, cap,
                    $"{phase}: she must come to {courseDeg}° and settle inside {cap} physics steps; she is " +
                    $"{ErrorTo(courseDeg):0.0}° off, swinging {_rb.angularVelocity:0.0}°/s, at {SpeedKn:0.00} kn");
                yield return Helm(courseDeg);
                steps++;
                eachStep?.Invoke(steps);
            }
            Debug.Log($"[SailTriangle] {phase}: {steps} steps, {SpeedKn:0.00} kn");
        }
#endif

        [UnityTest]
        public IEnumerator Sloop30_SailsTheTriangle_OnHerRudderAlone_Run_Gybe_Reach_Beat_InIrons()
        {
#if !UNITY_EDITOR
            Assert.Ignore("Needs the AssetDatabase: this sails the REAL committed sloop.");
            yield break;
#else
            var hull = AssetDatabase.LoadAssetAtPath<BoatHullDef>(Sloop30);
            Assert.IsNotNull(hull, $"{Sloop30} is missing — this sails the REAL shipped sloop.");
            Assert.IsTrue(hull.HasSailPlan, $"premise: {Sloop30} declares a sail plan");

            const float runCourse = 170f, reachCourse = 290f, beatCourse = 305f, headToWind = 0f;

            GameServices.Environment = new GlassSea { Wind = Vector2.down * SailDrive.ToMetresPerSecond(TwsKn) };
            var go = new GameObject("SailTriangleSloop");
            _spawned.Add(go);
            go.transform.rotation = Quaternion.Euler(0f, 0f, -runCourse);
            _boat = go.AddComponent<BoatController>();
            var bodies = go.GetComponents<Rigidbody2D>();
            Assert.AreEqual(1, bodies.Length, "premise: one body");
            _rb = bodies[0];
            var cols = go.GetComponents<CapsuleCollider2D>();
            Assert.AreEqual(1, cols.Length, "premise: one hull collider");
            cols[0].direction = CapsuleDirection2D.Vertical;
            cols[0].size = PlayersHullCapsule;
            _helm = go.AddComponent<DevBoatInput>();
            yield return null;
            _boat.SetHull(hull);
            yield return null;

            // The helm under test is the gate, driven directly: no device read runs beside it.
            _helm.enabled = false;
            GameServices.Helm.SetPilotedHull(_boat);
            Assert.IsTrue(_helm.RowingStationManned, "premise: she is at the helm");

            // Under way on the run at her polar speed, no swing on.
            float runTwa = BoatKinematics.RelativeBearingDegrees(runCourse, 0f);
            float runKn = SailDrive.TargetSpeedKn(hull.SailPolar, runTwa, TwsKn, hull.NoGoTrueWindDeg);
            Assert.Greater(runKn, 0f, "premise: the polar drives her on a run");
            _rb.linearVelocity = Along(runCourse) * SailDrive.ToMetresPerSecond(runKn);
            _rb.angularVelocity = 0f;

            var clock = System.Diagnostics.Stopwatch.StartNew();

            // 1 — the run.
            yield return SailLeg("the run", runCourse);

            // 2 — the gybe: through the stern, never through the eye of the wind.
            float lastTwa = _boat.LastSailWind.TrueAngleDeg;
            int flips = 0;
            string badFlip = null;
            yield return TurnTo("the gybe", reachCourse, GybeStepCap, step =>
            {
                float twa = _boat.LastSailWind.TrueAngleDeg;
                if ((twa < 0f) != (lastTwa < 0f))
                {
                    flips++;
                    if (Mathf.Abs(twa) < GybeFlipAftOfDeg || Mathf.Abs(lastTwa) < GybeFlipAftOfDeg)
                        badFlip ??= $"step {step}: {lastTwa:0.0}° → {twa:0.0}°";
                }
                lastTwa = twa;
                AssertNotInIrons("the gybe", step);
            });
            Assert.AreEqual(1, flips,
                "the gybe must bring the wind across her stern exactly once (the true-wind side flips once)");
            Assert.IsNull(badFlip,
                $"and it must cross the STERN, at least {GybeFlipAftOfDeg}° aft — a flip nearer the bow is a tack: {badFlip}");

            // 3 — the reach.
            yield return SailLeg("the reach", reachCourse);

            // 4 — harden up, and beat close-hauled.
            yield return TurnTo("hardening up", beatCourse, LuffStepCap, step => AssertNotInIrons("hardening up", step));
            yield return SailLeg("the beat", beatCourse);
            float beatTwa = Mathf.Abs(_boat.LastSailWind.TrueAngleDeg);
            Assert.That(beatTwa, Is.GreaterThan(hull.NoGoTrueWindDeg),
                "premise of the last phase: close-hauled she is OUTSIDE the no-go, still driven");

            // 5 — head to wind: the polar gives nothing inside the no-go, and she stops in irons.
            int ironsSteps = 0;
            while (!(Mathf.Abs(_boat.LastSailWind.TrueAngleDeg) < hull.NoGoTrueWindDeg && SpeedKn < StoppedKn))
            {
                Assert.Less(ironsSteps, IronsStepCap,
                    $"head to wind she must stop inside {IronsStepCap} physics steps; she is at TWA " +
                    $"{_boat.LastSailWind.TrueAngleDeg:0.0}°, {SpeedKn:0.00} kn");
                yield return Helm(headToWind);
                ironsSteps++;
            }
            clock.Stop();
            Debug.Log($"[SailTriangle] head to wind: {ironsSteps} steps; course sailed in {clock.Elapsed.TotalSeconds:0.0} s wall-clock");

            Assert.IsTrue(_boat.DrawnInIrons,
                $"stopped head to wind she is IN IRONS (TWA {_boat.LastSailWind.TrueAngleDeg:0.0}°, " +
                $"AWA {_boat.LastSailWind.ApparentAngleDeg:0.0}°)");
            Assert.AreEqual(0f, _boat.LastSailTargetKn, 0f, "and the polar gives her no speed inside the no-go");
            Assert.AreEqual(0f, _boat.Throttle, 0f, "the rudder was the whole helm: no throttle was ever written");
#endif
        }
    }
}
