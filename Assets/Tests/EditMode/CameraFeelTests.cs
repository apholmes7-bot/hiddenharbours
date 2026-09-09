using System.IO;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.App;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// THE CAMERA SPEAKS (juice PR 2 — <c>HANDOFF-2026-09-09-juice-lane.md</c> §3, owner ruling
    /// 2026-09-09). The charter's acceptance, verbatim: "an EditMode test drives the layer with
    /// synthetic events and asserts the zoom/offset curves hit their numbers and return to rest; the
    /// clamp law still holds at the rect edge."
    ///
    /// <para>Two halves. The <b>curves</b> are pinned as pure functions (a number at a point, a
    /// monotone run, a bound), then the <b>driven</b> <see cref="CameraFeel"/> is fed synthetic catches,
    /// groundings and speed and asserted back to exactly rest. Then the ONE follower is rigged the way
    /// <c>PlayerZoomTierTests</c> rigs it and the same events are pushed through
    /// <see cref="CameraFollow"/>'s public handlers: the orthographic size moves by the shipped
    /// fraction and comes back byte-for-byte, the shake stays inside its amplitude and comes back
    /// exactly, every gate of charter §3 (deck, cabin, road, shore) holds, and a shake against the
    /// region rect never carries the view over the edge.</para>
    ///
    /// <para>Laws this leans on: EditMode has no OnEnable, so everything goes through the public
    /// handlers, never the bus; a serialized field absent from the asset reads ZERO, so the shipped
    /// numbers are <see cref="JuiceSettings.Default"/> here and <c>GameConfigAssetCoverageTests</c>
    /// guards that the asset carries every key; the camera clamp IS the region rect.</para>
    /// </summary>
    public class CameraFeelTests
    {
        private const float Dt = 1f / 120f;
        private const float Tol = 1e-4f;
        private const float PuntHeight = 17f;

        private static JuiceSettings S => JuiceSettings.Default;

        [SetUp]
        public void SetUp()
        {
            GameServices.Config = null;
            GameServices.Environment = null;
            GameServices.CurrentRegionBounds = default;
            InteractionGate.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            GameServices.Config = null;
            GameServices.Environment = null;
            GameServices.CurrentRegionBounds = default;
            InteractionGate.Reset();
        }

        // ===== the curves, pure ====================================================================

        [Test]
        public void PushInEnvelope_IsZeroAtRest_OneAtThePeak_ZeroAfterTheRelease()
        {
            float inS = S.CatchPushInSeconds, outS = S.CatchPushOutSeconds;
            Assert.AreEqual(0f, CameraFeel.PushInEnvelope(-1f, inS, outS));
            Assert.AreEqual(0f, CameraFeel.PushInEnvelope(0f, inS, outS));
            Assert.AreEqual(1f, CameraFeel.PushInEnvelope(inS, inS, outS), 1e-6f, "exactly 1 at t = in");
            Assert.AreEqual(0.5f, CameraFeel.PushInEnvelope(inS + outS * 0.5f, inS, outS), 1e-6f,
                "the release is a smooth-step: half way down at half the out-time");
            Assert.AreEqual(0f, CameraFeel.PushInEnvelope(inS + outS, inS, outS), "exactly 0 at t = in + out");
            Assert.AreEqual(0f, CameraFeel.PushInEnvelope(inS + outS + 5f, inS, outS));
        }

        [Test]
        public void PushInEnvelope_RisesMonotoneThenFallsMonotone()
        {
            float inS = S.CatchPushInSeconds, outS = S.CatchPushOutSeconds;
            float prev = 0f;
            for (int i = 1; i <= 50; i++)
            {
                float v = CameraFeel.PushInEnvelope(inS * i / 50f, inS, outS);
                Assert.GreaterOrEqual(v, prev, $"rise step {i}");
                Assert.LessOrEqual(v, 1f);
                prev = v;
            }
            for (int i = 1; i <= 50; i++)
            {
                float v = CameraFeel.PushInEnvelope(inS + outS * i / 50f, inS, outS);
                Assert.LessOrEqual(v, prev, $"fall step {i}");
                Assert.GreaterOrEqual(v, 0f);
                prev = v;
            }
        }

        [Test]
        public void PushInEnvelope_ZeroTimes_AreInstant()
        {
            Assert.AreEqual(0.75f, CameraFeel.PushInEnvelope(0.06f, 0.12f, 0f), 1e-6f,
                "ease-out rise: 1 - (1 - u)^2 at u = 0.5");
            Assert.AreEqual(0f, CameraFeel.PushInEnvelope(0.12f, 0.12f, 0f), "out-time 0 = instant release");
            Assert.Greater(CameraFeel.PushInEnvelope(0.001f, 0f, 0.45f), 0.99f, "in-time 0 = instant peak");
        }

        [Test]
        public void WeightScale_HeavyIsFull_LightIsTheFloor_LinearBetween()
        {
            Assert.AreEqual(1f, CameraFeel.WeightScale(8f, 6f, 0.35f), "a cod above the heavy mark");
            Assert.AreEqual(1f, CameraFeel.WeightScale(6f, 6f, 0.35f), "at the mark");
            Assert.AreEqual(0.5f, CameraFeel.WeightScale(3f, 6f, 0.35f), 1e-6f, "half the mark");
            Assert.AreEqual(0.35f, CameraFeel.WeightScale(0.1f, 6f, 0.35f), 1e-6f, "a smelt gets the floor");
            Assert.AreEqual(1f, CameraFeel.WeightScale(0.1f, 0f, 0.35f), "heavy mark 0 = every catch is heavy");
        }

        [Test]
        public void ShakeEnvelope_OneAtTheHit_SquaredFall_ZeroAtTheEnd()
        {
            Assert.AreEqual(1f, CameraFeel.ShakeEnvelope(0f, 0.3f));
            Assert.AreEqual(0.25f, CameraFeel.ShakeEnvelope(0.15f, 0.3f), 1e-6f, "(1 - 1/2)^2");
            Assert.AreEqual(0f, CameraFeel.ShakeEnvelope(0.3f, 0.3f));
            Assert.AreEqual(0f, CameraFeel.ShakeEnvelope(0.4f, 0.3f));
            Assert.AreEqual(0f, CameraFeel.ShakeEnvelope(0.1f, 0f), "no duration = no shake");
        }

        [Test]
        public void ShakeAt_StaysInsideItsAmplitude_AndIsZeroAfterTheDuration()
        {
            float amp = S.ImpactShakeMeters, sec = S.ImpactShakeSeconds, hz = S.ImpactShakeHz;
            float bound = amp * Mathf.Sqrt(2f);
            bool moved = false;
            for (int i = 0; i <= 300; i++)
            {
                float t = sec * i / 300f;
                Vector2 o = CameraFeel.ShakeAt(t, amp, sec, hz);
                Assert.LessOrEqual(o.magnitude, bound * CameraFeel.ShakeEnvelope(t, sec) + 1e-6f, $"t = {t}");
                if (o.sqrMagnitude > 0f) moved = true;
            }
            Assert.IsTrue(moved, "a shake that never moves is no shake");
            Assert.AreEqual(Vector2.zero, CameraFeel.ShakeAt(sec, amp, sec, hz));
            Assert.AreEqual(Vector2.zero, CameraFeel.ShakeAt(0.05f, 0f, sec, hz), "amplitude 0 = nothing");
        }

        [Test]
        public void PullBackTarget_NothingBelowStart_FullAboveFull_SeaScaledBySpeed()
        {
            float f = S.SpeedPullBackFraction, sf = S.SpeedPullBackSeaStateFraction;
            float start = S.SpeedPullBackStartMps, full = S.SpeedPullBackFullMps;
            Assert.AreEqual(0f, CameraFeel.PullBackTarget(0f, 0f, S));
            Assert.AreEqual(0f, CameraFeel.PullBackTarget(start, 0f, S), "at the start threshold: nothing");
            Assert.AreEqual(f, CameraFeel.PullBackTarget(full, 0f, S), 1e-6f, "at full speed: the fraction");
            Assert.AreEqual(f, CameraFeel.PullBackTarget(full + 20f, 0f, S), 1e-6f, "and no more above it");
            Assert.AreEqual(0.5f * f, CameraFeel.PullBackTarget((start + full) * 0.5f, 0f, S), 1e-6f,
                "smooth-step: half way at the mid speed");
            Assert.AreEqual(f + sf, CameraFeel.PullBackTarget(full, 1f, S), 1e-6f, "a full sea adds its fraction");
            Assert.AreEqual(0f, CameraFeel.PullBackTarget(0f, 1f, S),
                "a boat lying still in a swell is NOT pulled back — the sea term rides the speed term");
        }

        [Test]
        public void SmoothStep01_BackwardsThresholds_AreAHardStep_NeverADivideByZero()
        {
            Assert.AreEqual(0f, CameraFeel.SmoothStep01(7f, 2.5f, 2f));
            Assert.AreEqual(1f, CameraFeel.SmoothStep01(7f, 2.5f, 3f));
            Assert.AreEqual(0f, CameraFeel.SmoothStep01(5f, 5f, 4.99f));
            Assert.AreEqual(1f, CameraFeel.SmoothStep01(5f, 5f, 5f));
        }

        [Test]
        public void Slew_MovesAtMostTheRangeOverTheSlewTime_AndZeroSlewIsInstant()
        {
            Assert.AreEqual(0.0075f, CameraFeel.Slew(0f, 0.15f, 0.15f, 2f, 0.1f), 1e-7f,
                "range 0.15 over 2 s = 0.075/s, so 0.0075 in 0.1 s");
            Assert.AreEqual(0.15f - 0.0075f, CameraFeel.Slew(0.15f, 0f, 0.15f, 2f, 0.1f), 1e-7f, "and the same rate back");
            Assert.AreEqual(0.15f, CameraFeel.Slew(0f, 0.15f, 0.15f, 0f, 0.1f), "slew 0 = no limit");
            Assert.AreEqual(0.15f, CameraFeel.Slew(0f, 0.15f, 0f, 2f, 0.1f), "an empty range = no limit");
            Assert.AreEqual(0.01f, CameraFeel.Slew(0f, 0.01f, 0.15f, 2f, 1f), "inside the limit it lands exactly");
        }

        // ===== the driven layer, synthetic events =================================================

        [Test]
        public void ADrivenCatch_HitsTheFractionAtTheInTime_AndIsExactlyAtRestAfterInPlusOut()
        {
            var feel = new CameraFeel();
            Assert.IsTrue(feel.AtRest);
            Assert.AreEqual(1f, feel.ZoomMultiplier);

            feel.OnCatch(8f, S);                       // a cod above the heavy mark: the full push
            for (int i = 0; i < 12; i++) feel.Tick(0.01f, 0f, 0f, false, S);   // t = 0.12 = CatchPushInSeconds
            Assert.AreEqual(1f - S.CatchPushInFraction, feel.ZoomMultiplier, Tol,
                "at the in-time the framing is multiplied by exactly 1 - CatchPushInFraction");
            Assert.IsFalse(feel.AtRest);

            for (int i = 0; i < 60; i++) feel.Tick(0.01f, 0f, 0f, false, S);   // t = 0.72 > in + out = 0.57
            Assert.AreEqual(1f, feel.ZoomMultiplier, "exactly 1 — not 'close to'");
            Assert.AreEqual(0f, feel.PushIn);
            Assert.IsTrue(feel.AtRest);
        }

        [Test]
        public void ADrivenLightCatch_PushesInByTheLightFloor()
        {
            var feel = new CameraFeel();
            feel.OnCatch(0.1f, S);
            for (int i = 0; i < 12; i++) feel.Tick(0.01f, 0f, 0f, false, S);
            Assert.AreEqual(1f - S.CatchPushInFraction * S.CatchPushInLightScale, feel.ZoomMultiplier, Tol);
        }

        [Test]
        public void ADrivenImpact_ShakesInsideItsAmplitude_ThenReturnsExactlyToZero()
        {
            var feel = new CameraFeel();
            feel.OnImpact(1f, S);
            float bound = S.ImpactShakeMeters * Mathf.Sqrt(2f) + 1e-6f;
            bool moved = false;
            for (int i = 0; i < 20; i++)
            {
                feel.Tick(Dt, 0f, 0f, false, S);
                Assert.LessOrEqual(feel.ShakeOffset.magnitude, bound);
                if (feel.ShakeOffset.sqrMagnitude > 0f) moved = true;
            }
            Assert.IsTrue(moved);
            for (int i = 0; i < 40; i++) feel.Tick(Dt, 0f, 0f, false, S);   // 60 × 1/120 = 0.5 s > 0.3 s
            Assert.AreEqual(Vector2.zero, feel.ShakeOffset);
            Assert.IsTrue(feel.AtRest);
        }

        [Test]
        public void ADrivenImpact_ScalesBySeverity_AndASecondHitTakesTheLargerAmplitude()
        {
            var expected = CameraFeel.ShakeAt(Dt, S.ImpactShakeMeters, S.ImpactShakeSeconds, S.ImpactShakeHz);
            var half = CameraFeel.ShakeAt(Dt, 0.5f * S.ImpactShakeMeters, S.ImpactShakeSeconds, S.ImpactShakeHz);

            var feel = new CameraFeel();
            feel.OnImpact(0.5f, S);
            feel.Tick(Dt, 0f, 0f, false, S);
            Assert.AreEqual(half.x, feel.ShakeOffset.x, 1e-6f, "severity 0.5 = half the metres");
            Assert.AreEqual(half.y, feel.ShakeOffset.y, 1e-6f);

            feel = new CameraFeel();
            feel.OnImpact(0.3f, S);
            feel.OnImpact(1f, S);                        // a harder hit on top of a live soft one
            feel.Tick(Dt, 0f, 0f, false, S);
            Assert.AreEqual(expected.x, feel.ShakeOffset.x, 1e-6f, "the harder hit wins");
            Assert.AreEqual(expected.y, feel.ShakeOffset.y, 1e-6f);

            feel = new CameraFeel();
            feel.OnImpact(1f, S);
            feel.OnImpact(0.3f, S);                      // a softer hit does not shrink a live hard one
            feel.Tick(Dt, 0f, 0f, false, S);
            Assert.AreEqual(expected.x, feel.ShakeOffset.x, 1e-6f, "two bumps never add up to less than the harder one");
        }

        [Test]
        public void ADrivenPullBack_IsSlewLimited_ReachesTheFraction_AndComesBackToExactlyNothing()
        {
            var feel = new CameraFeel();
            float range = S.SpeedPullBackFraction + S.SpeedPullBackSeaStateFraction;
            float perTick = range / S.SpeedPullBackSlewSeconds * Dt;

            feel.Tick(Dt, 20f, 0f, true, S);
            Assert.AreEqual(perTick, feel.PullBack, 1e-7f, "the first tick moves exactly one slew step, not to the target");
            Assert.AreEqual(1f + perTick, feel.ZoomMultiplier, 1e-6f);

            for (int i = 0; i < 360; i++) feel.Tick(Dt, 20f, 0f, true, S);   // 3 s at full speed
            Assert.AreEqual(S.SpeedPullBackFraction, feel.PullBack, 1e-6f, "at full speed the base fraction, no sea");
            Assert.AreEqual(1f + S.SpeedPullBackFraction, feel.ZoomMultiplier, 1e-6f);

            for (int i = 0; i < 360; i++) feel.Tick(Dt, 20f, 0f, false, S);  // off open water: back at the same rate
            Assert.AreEqual(0f, feel.PullBack, "exactly 0");
            Assert.AreEqual(1f, feel.ZoomMultiplier);
            Assert.IsTrue(feel.AtRest);
        }

        [Test]
        public void ADrivenPullBack_InASwell_NeverBreathes()
        {
            // The sea state flips every tick — the worst 'breathing' input there is. The on-screen
            // pull-back may move at most one slew step per tick regardless.
            var feel = new CameraFeel();
            float range = S.SpeedPullBackFraction + S.SpeedPullBackSeaStateFraction;
            float perTick = range / S.SpeedPullBackSlewSeconds * Dt;
            float prev = 0f;
            for (int i = 0; i < 600; i++)
            {
                feel.Tick(Dt, 20f, (i & 1) == 0 ? 1f : 0f, true, S);
                Assert.LessOrEqual(Mathf.Abs(feel.PullBack - prev), perTick + 1e-7f, $"tick {i}");
                prev = feel.PullBack;
            }
            Assert.GreaterOrEqual(feel.PullBack, S.SpeedPullBackFraction - 1e-6f);
            Assert.LessOrEqual(feel.PullBack, range + 1e-6f);
        }

        [Test]
        public void ADrivenPullBack_WithAFullSea_ReachesBothFractions()
        {
            var feel = new CameraFeel();
            for (int i = 0; i < 480; i++) feel.Tick(Dt, 20f, 1f, true, S);
            Assert.AreEqual(S.SpeedPullBackFraction + S.SpeedPullBackSeaStateFraction, feel.PullBack, 1e-6f);
        }

        [Test]
        public void Reset_DropsEverythingToRestAtOnce()
        {
            var feel = new CameraFeel();
            feel.OnCatch(8f, S); feel.OnImpact(1f, S);
            for (int i = 0; i < 5; i++) feel.Tick(Dt, 20f, 1f, true, S);
            Assert.IsFalse(feel.AtRest);
            feel.Reset();
            Assert.IsTrue(feel.AtRest);
            Assert.AreEqual(1f, feel.ZoomMultiplier);
            Assert.AreEqual(Vector2.zero, feel.ShakeOffset);
        }

        // ===== the one follower, rigged ===========================================================

        private static CatchItem Fish(float kg)
            => new CatchItem("fish.atlantic_cod", "Atlantic cod", FishCategory.InshoreGroundfish, kg, 10, 0.5f);

        private static CameraFollow WalkingRig(GameObject go)
        {
            Camera cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            CameraFollow follow = go.AddComponent<CameraFollow>();
            follow.OnControlModeChanged(new ControlModeChanged(ControlMode.OnFoot));
            follow.TickZoom(10.0);
            return follow;
        }

        private static CameraFollow AtTheHelm(GameObject go, float hullHeightMeters)
        {
            CameraFollow follow = WalkingRig(go);
            follow.OnActiveBoatChanged(new ActiveBoatChanged("boat.punt", hullHeightMeters));
            follow.OnControlModeChanged(new ControlModeChanged(ControlMode.Aboard));
            follow.TickZoom(100.0);
            return follow;
        }

        private static CameraFollow OnTheDeck(GameObject go, float hullHeightMeters)
        {
            CameraFollow follow = WalkingRig(go);
            follow.OnActiveBoatChanged(new ActiveBoatChanged("boat.punt", hullHeightMeters));
            follow.OnControlModeChanged(new ControlModeChanged(ControlMode.OnDeck));
            follow.TickZoom(100.0);
            return follow;
        }

        /// <summary>A config asset with the shipped juice numbers and one dial turned, wired as the live
        /// config for the test (the caller restores it — SetUp/TearDown null it anyway).</summary>
        private static GameConfig ConfigWith(System.Action<GameConfig> turn)
        {
            var cfg = ScriptableObject.CreateInstance<GameConfig>();
            cfg.Juice = JuiceSettings.Default;
            turn(cfg);
            GameServices.Config = cfg;
            return cfg;
        }

        private static void Settle(CameraFollow follow, int ticks, float speed = 0f)
        {
            for (int i = 0; i < ticks; i++) follow.TickFeel(Dt, speed);
        }

        [Test]
        public void ALandedCatchOnFoot_PushesTheViewInByTheFraction_AndHandsItBackByteForByte()
        {
            var go = new GameObject("Cam");
            try
            {
                CameraFollow follow = WalkingRig(go);
                Camera cam = go.GetComponent<Camera>();
                float baseOrtho = cam.orthographicSize;
                Assert.AreEqual(CameraFollow.OrthoSizeForWorldHeight(CameraFollow.OnFootWorldHeightMeters), baseOrtho, Tol);

                follow.OnCatchLanded(new CatchLanded(Fish(8f)));
                for (int i = 0; i < 12; i++) follow.TickFeel(0.01f, 0f);
                Assert.IsTrue(follow.FeelApplied);
                Assert.AreEqual(baseOrtho * (1f - S.CatchPushInFraction), cam.orthographicSize, Tol,
                    "at the in-time the camera shows the framing × (1 - CatchPushInFraction)");

                for (int i = 0; i < 80; i++) follow.TickFeel(0.01f, 0f);
                Assert.IsFalse(follow.FeelApplied);
                Assert.AreEqual(baseOrtho, cam.orthographicSize, "back to the framing's own number, exactly");
                Assert.IsTrue(follow.Feel.AtRest);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void ALightCatch_PushesInByTheLightFloor()
        {
            var go = new GameObject("Cam");
            try
            {
                CameraFollow follow = WalkingRig(go);
                Camera cam = go.GetComponent<Camera>();
                float baseOrtho = cam.orthographicSize;
                follow.OnCatchLanded(new CatchLanded(Fish(0.1f)));
                for (int i = 0; i < 12; i++) follow.TickFeel(0.01f, 0f);
                Assert.AreEqual(baseOrtho * (1f - S.CatchPushInFraction * S.CatchPushInLightScale), cam.orthographicSize, Tol);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void AGroundingAtTheHelm_ShakesInsideTheAmplitude_AndReturnsExactly()
        {
            var go = new GameObject("Cam");
            try
            {
                CameraFollow follow = AtTheHelm(go, PuntHeight);
                var origin = new Vector3(3f, 4f, -10f);
                go.transform.position = origin;
                float bound = S.ImpactShakeMeters * Mathf.Sqrt(2f) + 1e-5f;

                follow.OnBoatGrounded(new BoatGrounded(null, 1f));
                bool moved = false;
                for (int i = 0; i < 20; i++)
                {
                    follow.TickFeel(Dt, 0f);
                    Vector3 d = go.transform.position - origin;
                    Assert.AreEqual(0f, d.z, "depth is never touched");
                    Assert.LessOrEqual(d.magnitude, bound, $"tick {i}");
                    if (d.sqrMagnitude > 0f) moved = true;
                }
                Assert.IsTrue(moved, "a grounding at severity 1 moves the camera");
                Assert.IsTrue(follow.FeelApplied);

                Settle(follow, 60);
                Assert.AreEqual(origin, go.transform.position, "exactly where it was — a shake never accumulates");
                Assert.IsFalse(follow.FeelApplied);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void AGroundingAtSeverityZero_IsNothing()
        {
            var go = new GameObject("Cam");
            try
            {
                CameraFollow follow = AtTheHelm(go, PuntHeight);
                var origin = new Vector3(3f, 4f, -10f);
                go.transform.position = origin;
                follow.OnBoatGrounded(new BoatGrounded(null, 0f));
                Settle(follow, 10);
                Assert.AreEqual(origin, go.transform.position);
                Assert.IsFalse(follow.FeelApplied);
                Assert.IsTrue(follow.Feel.AtRest);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void AGroundingHeardAshore_DoesNotShakeTheWalker()
        {
            var go = new GameObject("Cam");
            try
            {
                CameraFollow follow = WalkingRig(go);
                var origin = new Vector3(3f, 4f, -10f);
                go.transform.position = origin;
                follow.OnBoatGrounded(new BoatGrounded(null, 1f));
                Settle(follow, 10);
                Assert.AreEqual(origin, go.transform.position, "the boat that grounded may not even be hers");
                Assert.IsFalse(follow.FeelApplied);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void OnDeck_TheLayerIsOffByDefault_AndOnByTheOwnersDial()
        {
            var go = new GameObject("Cam");
            try
            {
                CameraFollow follow = OnTheDeck(go, PuntHeight);
                Camera cam = go.GetComponent<Camera>();
                float baseOrtho = cam.orthographicSize;

                follow.OnCatchLanded(new CatchLanded(Fish(8f)));
                for (int i = 0; i < 12; i++) follow.TickFeel(0.01f, 0f);
                Assert.AreEqual(baseOrtho, cam.orthographicSize, "charter §3: deck mode untouched — FeelOnDeckEnabled ships 0");
                Assert.IsFalse(follow.FeelApplied);

                ConfigWith(c => c.Juice.FeelOnDeckEnabled = true);
                follow.OnCatchLanded(new CatchLanded(Fish(8f)));
                for (int i = 0; i < 12; i++) follow.TickFeel(0.01f, 0f);
                Assert.AreEqual(baseOrtho * (1f - S.CatchPushInFraction), cam.orthographicSize, Tol,
                    "the owner can turn the deck on (the intro fishes from the deck — ruling 2026-09-06)");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void OnDeck_EvenWhenEnabled_ThereIsNoPullBack()
        {
            var go = new GameObject("Cam");
            try
            {
                ConfigWith(c => c.Juice.FeelOnDeckEnabled = true);
                CameraFollow follow = OnTheDeck(go, PuntHeight);
                Camera cam = go.GetComponent<Camera>();
                float baseOrtho = cam.orthographicSize;
                Settle(follow, 240, speed: 20f);
                Assert.AreEqual(baseOrtho, cam.orthographicSize, "open water is the helm's alone");
                Assert.IsFalse(follow.FeelApplied);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void InACabin_TheLayerStandsOff_AndComesBackOnLeaving()
        {
            var go = new GameObject("Cam");
            try
            {
                CameraFollow follow = AtTheHelm(go, PuntHeight);
                Camera cam = go.GetComponent<Camera>();
                float baseOrtho = cam.orthographicSize;

                follow.OnCabinEntered(new CabinEntered(default, 0));
                follow.OnCatchLanded(new CatchLanded(Fish(8f)));
                follow.OnBoatGrounded(new BoatGrounded(null, 1f));
                for (int i = 0; i < 12; i++) follow.TickFeel(0.01f, 0f);
                Assert.AreEqual(baseOrtho, cam.orthographicSize, "interior: untouched");
                Assert.IsFalse(follow.FeelApplied);

                follow.OnCabinLeft(new CabinLeft(default));
                follow.OnCatchLanded(new CatchLanded(Fish(8f)));
                for (int i = 0; i < 12; i++) follow.TickFeel(0.01f, 0f);
                Assert.AreEqual(baseOrtho * (1f - S.CatchPushInFraction), cam.orthographicSize, Tol);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void AtARoadWheel_TheLayerIsOff()
        {
            var go = new GameObject("Cam");
            try
            {
                CameraFollow follow = WalkingRig(go);
                Camera cam = go.GetComponent<Camera>();
                follow.OnControlModeChanged(new ControlModeChanged(ControlMode.Driving));
                float baseOrtho = cam.orthographicSize;
                follow.OnCatchLanded(new CatchLanded(Fish(8f)));
                follow.OnBoatGrounded(new BoatGrounded(null, 1f));
                Settle(follow, 12, speed: 20f);
                Assert.AreEqual(baseOrtho, cam.orthographicSize);
                Assert.IsFalse(follow.FeelApplied);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void TheMasterSwitchOff_HandsTheCameraBackUntouched()
        {
            var go = new GameObject("Cam");
            try
            {
                ConfigWith(c => c.Juice.FeelEnabled = false);
                CameraFollow follow = AtTheHelm(go, PuntHeight);
                Camera cam = go.GetComponent<Camera>();
                var origin = new Vector3(3f, 4f, -10f);
                go.transform.position = origin;
                float baseOrtho = cam.orthographicSize;
                follow.OnCatchLanded(new CatchLanded(Fish(8f)));
                follow.OnBoatGrounded(new BoatGrounded(null, 1f));
                Settle(follow, 60, speed: 20f);
                Assert.AreEqual(baseOrtho, cam.orthographicSize);
                Assert.AreEqual(origin, go.transform.position);
                Assert.IsFalse(follow.FeelApplied);
                Assert.IsTrue(follow.Feel.AtRest);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void AtSpeedAtTheHelm_TheViewPullsBackUnderTheSlew_AndReturnsExactly()
        {
            var go = new GameObject("Cam");
            try
            {
                CameraFollow follow = AtTheHelm(go, PuntHeight);
                Camera cam = go.GetComponent<Camera>();
                float baseOrtho = cam.orthographicSize;
                float range = S.SpeedPullBackFraction + S.SpeedPullBackSeaStateFraction;
                float perTick = range / S.SpeedPullBackSlewSeconds * Dt;

                follow.TickFeel(Dt, 20f);
                Assert.IsTrue(follow.FeelApplied);
                Assert.AreEqual(baseOrtho * (1f + perTick), cam.orthographicSize, 1e-5f,
                    "the first tick at speed is one slew step, not the whole pull-back");

                Settle(follow, 360, speed: 20f);
                Assert.AreEqual(baseOrtho * (1f + S.SpeedPullBackFraction), cam.orthographicSize, Tol,
                    "3 s at full speed in a flat sea: framing × (1 + SpeedPullBackFraction)");

                Settle(follow, 360, speed: 0f);
                Assert.AreEqual(baseOrtho, cam.orthographicSize, "stopped: the framing's number, exactly");
                Assert.IsFalse(follow.FeelApplied);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void BelowTheStartSpeed_ThereIsNoPullBack()
        {
            var go = new GameObject("Cam");
            try
            {
                CameraFollow follow = AtTheHelm(go, PuntHeight);
                Camera cam = go.GetComponent<Camera>();
                float baseOrtho = cam.orthographicSize;
                Settle(follow, 240, speed: S.SpeedPullBackStartMps * 0.9f);
                Assert.AreEqual(baseOrtho, cam.orthographicSize, "a slow boat is not pulled back");
                Assert.IsFalse(follow.FeelApplied);
            }
            finally { Object.DestroyImmediate(go); }
        }

        private sealed class SeaEnv : IEnvironmentService
        {
            public float Sea;
            public int WorldSeed => 0;
            public TideProfile ActiveTideProfile { get; set; }
            public EnvironmentSample Sample() => new EnvironmentSample(Vector2.zero, Vector2.zero, 0f, default, 1f, Sea);
            public float TideHeightAt(double totalSeconds) => 0f;
        }

        [Test]
        public void AtSpeedInAFullSea_TheSeaStateAddsItsFraction()
        {
            var go = new GameObject("Cam");
            try
            {
                GameServices.Environment = new SeaEnv { Sea = 1f };
                CameraFollow follow = AtTheHelm(go, PuntHeight);
                Camera cam = go.GetComponent<Camera>();
                float baseOrtho = cam.orthographicSize;
                Settle(follow, 480, speed: 20f);
                Assert.AreEqual(baseOrtho * (1f + S.SpeedPullBackFraction + S.SpeedPullBackSeaStateFraction),
                    cam.orthographicSize, Tol);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void WalkingFast_IsNotOpenWater()
        {
            var go = new GameObject("Cam");
            try
            {
                CameraFollow follow = WalkingRig(go);
                Camera cam = go.GetComponent<Camera>();
                float baseOrtho = cam.orthographicSize;
                Settle(follow, 240, speed: 20f);
                Assert.AreEqual(baseOrtho, cam.orthographicSize, "an ATV-fast walker gets no pull-back");
                Assert.IsFalse(follow.FeelApplied);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void ATierChangeDuringAPushIn_LandsOnTheNewTier_NotOnTheTierTimesThePush()
        {
            // The framing under a live effect is the framing's own, never the multiplied camera value:
            // a helm framing committed mid push-in must end exactly where a helm framing always ends.
            var go = new GameObject("Cam");
            try
            {
                CameraFollow follow = WalkingRig(go);
                Camera cam = go.GetComponent<Camera>();
                follow.OnCatchLanded(new CatchLanded(Fish(8f)));
                for (int i = 0; i < 12; i++) follow.TickFeel(0.01f, 0f);
                Assert.IsTrue(follow.FeelApplied);

                follow.OnActiveBoatChanged(new ActiveBoatChanged("boat.punt", PuntHeight));
                follow.OnControlModeChanged(new ControlModeChanged(ControlMode.Aboard));
                follow.TickZoom(100.0);
                for (int i = 0; i < 80; i++) follow.TickFeel(0.01f, 0f);
                Assert.IsFalse(follow.FeelApplied);

                var reference = new GameObject("Ref");
                try
                {
                    CameraFollow plain = AtTheHelm(reference, PuntHeight);
                    Assert.AreEqual(reference.GetComponent<Camera>().orthographicSize, cam.orthographicSize, 1e-6f,
                        "the helm framing after a push-in is the helm framing");
                }
                finally { Object.DestroyImmediate(reference); }
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void WithNothingFired_AWholeFrameLeavesTheCameraByteForByte()
        {
            // THE PASSTHROUGH CLAIM for the frame: a camera that never hears a catch, a grounding or a
            // speed goes through TickFrame — zoom, tween, feel, snap, clamp — and comes out identical.
            var go = new GameObject("Cam");
            try
            {
                CameraFollow follow = WalkingRig(go);
                Camera cam = go.GetComponent<Camera>();
                go.transform.position = new Vector3(3.123f, 4.567f, -10f);
                follow.TickFrame(10.0, Dt, 0f);          // one frame so the snap (pre-existing) has landed
                float ortho = cam.orthographicSize;
                Vector3 pos = go.transform.position;
                for (int i = 0; i < 30; i++) follow.TickFrame(10.0 + i * Dt, Dt, 0f);
                Assert.AreEqual(ortho, cam.orthographicSize);
                Assert.AreEqual(pos, go.transform.position);
                Assert.IsFalse(follow.FeelApplied);
                Assert.IsTrue(follow.Feel.AtRest);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void AShakeAgainstTheRegionRect_NeverCarriesTheViewOverTheEdge()
        {
            // THE CLAMP LAW (charter §3 acceptance; memory: the camera clamp IS the region rect). The
            // camera is parked with its right edge exactly on the rect's, a full-severity grounding is
            // fired, and every frame of the shake goes through the whole TickFrame — feel, then snap,
            // then clamp. The view's right edge may never pass the rect's.
            var go = new GameObject("Cam");
            try
            {
                GameServices.CurrentRegionBounds = new Rect(-20f, -20f, 40f, 40f);   // right edge x = 20
                CameraFollow follow = AtTheHelm(go, PuntHeight);
                Camera cam = go.GetComponent<Camera>();
                Assert.IsTrue(follow.Bounds.IsBounded);

                float aspect = cam.aspect > 0f ? cam.aspect
                    : CameraFollow.ReferenceWidthPx / (float)CameraFollow.ReferenceHeightPx;
                float halfWidth = cam.orthographicSize * aspect;
                go.transform.position = new Vector3(20f - halfWidth, 0f, -10f);

                follow.OnBoatGrounded(new BoatGrounded(null, 1f));
                bool pushedRight = false;
                for (int i = 0; i < 60; i++)
                {
                    follow.TickFrame(100.0 + i * Dt, Dt, 0f);
                    if (follow.Feel.ShakeOffset.x > 0f) pushedRight = true;
                    float liveAspect = cam.aspect > 0f ? cam.aspect
                        : CameraFollow.ReferenceWidthPx / (float)CameraFollow.ReferenceHeightPx;
                    float rightEdge = go.transform.position.x + cam.orthographicSize * liveAspect;
                    Assert.LessOrEqual(rightEdge, 20f + Tol, $"tick {i}: the view's right edge passed the rect's");
                }
                Assert.IsTrue(pushedRight, "the shake did push toward the wall at least once — otherwise nothing was clamped");
                Assert.IsTrue(follow.Feel.AtRest, "and it decayed out inside the window");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void TheFollowerTicksTheFeelOnUnscaledTime_AndThereIsStillOneFollower()
        {
            // Charter §3: "All timers on unscaled time so PR 3's hit-stop cannot freeze a shake
            // mid-frame." A source guard, because a scaled-time regression is invisible to every test
            // above (they hand the layer its dt) and only shows the first time a hit-stop lands on a
            // grounding. And the memory law — two CameraFollows fight — as a count.
            string appDir = Path.Combine(Application.dataPath, "_Project", "Code", "App");
            string follow = File.ReadAllText(Path.Combine(appDir, "CameraFollow.cs"));
            StringAssert.Contains("TickFrame(Time.timeAsDouble, Time.unscaledDeltaTime", follow,
                "LateUpdate must hand the feel Time.unscaledDeltaTime, never Time.deltaTime");

            int followers = 0;
            foreach (string f in Directory.GetFiles(appDir, "*.cs", SearchOption.AllDirectories))
                if (File.ReadAllText(f).Contains("class CameraFollow ")) followers++;
            Assert.AreEqual(1, followers, "there is ONE follower — extend it, never add a second");
        }
    }
}
