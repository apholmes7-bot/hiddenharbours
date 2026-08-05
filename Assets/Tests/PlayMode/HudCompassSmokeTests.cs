using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using HiddenHarbours.Core;
using HiddenHarbours.UI;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// VS-19 — a PlayMode smoke test for the HUD's nav cluster (heading compass + set-&-drift). It
    /// proves the always-on HUD reads the Core heading seam (<see cref="GameServices.ActiveBoat"/>) at
    /// its ~4 Hz cadence: the cluster is HIDDEN ashore, SHOWN at sea with the right heading/cardinal,
    /// and the set-&-drift read reflects course-over-ground vs heading (crabbing). Reads only Core — a
    /// tiny in-file fake <see cref="IActiveBoatService"/> stands in for the Boats producer.
    /// </summary>
    public class HudCompassSmokeTests
    {
        private sealed class FakeClock : IGameClock
        {
            public double TotalSeconds { get; set; }
            public GameTime Now => new GameTime(TotalSeconds);
            public Season Season => Season.EarlySpring;
            public int Year => 1;
            public int DayIndex => 0;
            public int DayOfSeason => 1;
            public Weekday Weekday => Weekday.Monday;
            public bool IsMarketDay => false;
            public float HourOfDay => 6f;
            public float DayFraction => 0.25f;
            public bool IsPaused { get; set; }
            public float TimeScale { get; set; } = 1f;
        }

        private sealed class FakeEnv : IEnvironmentService
        {
            public int WorldSeed => 0;
            public TideProfile ActiveTideProfile { get; set; } = TideProfile.CoddleCove;
            public EnvironmentSample Sample()
                => new EnvironmentSample(new Vector2(0f, 3f), Vector2.zero, 1.2f, SeaState.Calm, 1f);
            public float TideHeightAt(double totalSeconds) => 0f;
        }

        private sealed class FakeActiveBoat : IActiveBoatService
        {
            public bool HasActiveBoat { get; set; }
            public BoatKinematics Next;
            public BoatKinematics Sample() => Next;
        }

        /// <summary>A helm that is only ever asked three things by the HUD (S4.5): am I manned, which
        /// control, what is fitted. Everything else on the seam is the overlay's business.</summary>
        private sealed class FakeHelm : IHelmControl
        {
            public bool HasHelm { get; set; }
            public HelmControlStyle Style { get; set; } = HelmControlStyle.None;
            public HelmFit Fit { get; set; } = HelmFit.None;
            public HelmLeverFinish LeverFinish => HelmLeverFinish.Graphite;
            public HelmWheelRim WheelRim => HelmWheelRim.Rubber;
            public float Drive => 0f;
            public float Steer => 0f;
            public bool SteerDragActive => false;
            public void StepAhead() { }
            public void StepAstern() { }
            public void SetNeutral() { }
            public void DragDrive(float sig) { }
            public void EndDrag() { }
            public void DragSteer(float steer) { }
            public void EndSteerDrag() { }
        }

        private GameObject _hudGo;

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();
            GameServices.Clock = new FakeClock();
            GameServices.Environment = new FakeEnv();
        }

        [TearDown]
        public void TearDown()
        {
            GameServices.Reset();
            if (_hudGo != null) Object.Destroy(_hudGo);
        }

        private static Text Label(HudController hud, string field)
        {
            var f = typeof(HudController).GetField(field, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(f, $"field '{field}' not found on HudController");
            return (Text)f.GetValue(hud);
        }

        private HudController MakeHud()
        {
            _hudGo = new GameObject("HUD");
            return _hudGo.AddComponent<HudController>(); // Awake builds the Canvas + labels
        }

        // One env-sample tick is throttled to ~4 Hz; cross it (unscaled) so UpdateNavReads runs.
        private static IEnumerator NextNavTick() => new WaitForSecondsRealtime(0.3f);

        [UnityTest]
        public IEnumerator NavCluster_IsHidden_Ashore()
        {
            GameServices.ActiveBoat = null; // on foot / no boat
            var hud = MakeHud();
            yield return NextNavTick();

            Assert.IsFalse(Label(hud, "_compassLabel").enabled, "no boat → compass hidden");
            Assert.IsFalse(Label(hud, "_setDriftLabel").enabled, "no boat → set-&-drift hidden");
        }

        [UnityTest]
        public IEnumerator NavCluster_ShowsHeading_Aboard()
        {
            var boat = new FakeActiveBoat
            {
                HasActiveBoat = true,
                Next = BoatKinematics.FromBow(Vector2.right, Vector2.zero), // bow East, at rest
            };
            GameServices.ActiveBoat = boat;

            var hud = MakeHud();
            yield return NextNavTick();

            Assert.IsTrue(Label(hud, "_compassLabel").enabled, "aboard → compass shows");
            Assert.AreEqual("→ 090°  E", Label(hud, "_compassLabel").text, "heading reads arrow + degrees + cardinal");
            Assert.AreEqual("COG —", Label(hud, "_setDriftLabel").text, "at rest there is no steady course");
        }

        [UnityTest]
        public IEnumerator SetDrift_ShowsCrabbing_WhenCourseDiffersFromHeading()
        {
            var boat = new FakeActiveBoat
            {
                HasActiveBoat = true,
                // Bow points North, but the hull is set to the NE by wind/current → crabbing to starboard.
                Next = BoatKinematics.FromBow(Vector2.up, new Vector2(1f, 1f)),
            };
            GameServices.ActiveBoat = boat;

            var hud = MakeHud();
            yield return NextNavTick();

            Assert.AreEqual("↑ 000°  N", Label(hud, "_compassLabel").text, "bow points North");
            Assert.AreEqual("COG 045°  → 45° stbd", Label(hud, "_setDriftLabel").text,
                "the player sees the true course-over-ground crab off the heading");
        }

        [UnityTest]
        public IEnumerator NavCluster_HidesAgain_WhenBoatLeftAshore()
        {
            var boat = new FakeActiveBoat
            {
                HasActiveBoat = true,
                Next = BoatKinematics.FromBow(Vector2.up, Vector2.zero),
            };
            GameServices.ActiveBoat = boat;

            var hud = MakeHud();
            yield return NextNavTick();
            Assert.IsTrue(Label(hud, "_compassLabel").enabled, "precondition: shown while aboard");

            boat.HasActiveBoat = false; // disembarked
            yield return NextNavTick();

            Assert.IsFalse(Label(hud, "_compassLabel").enabled, "disembarking hides the compass");
            Assert.IsFalse(Label(hud, "_setDriftLabel").enabled, "disembarking hides the set-&-drift read");
        }

        // ---- S4.5: the cluster yields to a helm dash ---------------------------------------------

        [UnityTest]
        public IEnumerator NavCluster_YieldsToAHelmDash_HidingOnlyWhereTheDashDuplicatesIt()
        {
            // The WIRING, not the rule (HelmHudSuppressionTests owns the rule exhaustively): that
            // HudController actually consults the live fit, and that the two answers reach the labels.
            var boat = new FakeActiveBoat
            {
                HasActiveBoat = true,
                Next = BoatKinematics.FromBow(Vector2.up, Vector2.zero),
            };
            GameServices.ActiveBoat = boat;
            var helm = new FakeHelm();
            GameServices.HelmControl = helm;

            var hud = MakeHud();
            yield return NextNavTick();
            Assert.IsTrue(Label(hud, "_compassLabel").enabled,
                          "precondition: aboard with no helm at all, the cluster is up");
            RectTransform home = RectOf(hud, "_compassLabel");

            // A dash WITH a compass: the dash says the heading better, so the HUD stops saying it.
            helm.HasHelm = true;
            helm.Style = HelmControlStyle.Lever;
            helm.Fit = new HelmFit(ConsoleRigKind.Console, SounderKind.Depth, CompassMount.Dome,
                                   false, false);
            yield return NextNavTick();
            Assert.IsFalse(Label(hud, "_compassLabel").enabled, "a dash compass hides the HUD one");
            Assert.IsFalse(Label(hud, "_setDriftLabel").enabled);

            // The SHIPPED wheelhouse fit — a dash and NO compass. The cluster must come back, moved:
            // this boat has no other heading read, and the dash card sits where the cluster was.
            helm.Fit = new HelmFit(ConsoleRigKind.Novi, SounderKind.Depth, CompassMount.None,
                                   false, false);
            yield return NextNavTick();
            Assert.IsTrue(Label(hud, "_compassLabel").enabled,
                          "no dash compass → the read survives");
            Assert.IsTrue(Label(hud, "_setDriftLabel").enabled);
            RectTransform moved = RectOf(hud, "_compassLabel");
            Assert.AreNotEqual(home.anchorMax.x, moved.anchorMax.x, "…and it moved off the dash");
            Assert.AreEqual(0f, moved.anchorMin.x, 1e-4f, "to the left edge");
            Assert.AreEqual(TextAnchor.LowerLeft, Label(hud, "_compassLabel").alignment);

            // Leaving the helm puts it back exactly where VS-19 authored it.
            helm.HasHelm = false;
            helm.Style = HelmControlStyle.None;
            helm.Fit = HelmFit.None;
            yield return NextNavTick();
            RectTransform back = RectOf(hud, "_compassLabel");
            Assert.AreEqual(home.anchorMin.x, back.anchorMin.x, 1e-4f, "the move is exactly reversible");
            Assert.AreEqual(home.anchorMax.x, back.anchorMax.x, 1e-4f);
            Assert.AreEqual(home.anchoredPosition.x, back.anchoredPosition.x, 1e-4f);
            Assert.AreEqual(TextAnchor.LowerCenter, Label(hud, "_compassLabel").alignment);
        }

        private static RectTransform RectOf(HudController hud, string field)
            => (RectTransform)Label(hud, field).transform;
    }
}
