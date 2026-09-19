using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.App;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// BOAT FEEL PR 1 — the camera leads the boat and pulls back at cruise (charter
    /// <c>HANDOFF-2026-09-18-boat-feel-turn-trim-camera.md</c>, owner ruling 2026-09-19 item 6:
    /// "lead yes, zoom-out yes, intro gets it too, no shake").
    ///
    /// <para>The Phase A measure this answers: the follow filter (Smooth 6) trails a moving target by
    /// 0.1585 s of her travel at 60 fps, and the scene's look-ahead is only 0.35 s capped at 2.5 m, so
    /// the lead a player SAW was 0.38 m at 2 m/s and under 1 m at every speed. The ruled dials pay the
    /// lag back and lead by 0.6 s, capped at 0.35 of half the view; the pull-back starts at 1 m/s and
    /// is full (15 %) at 4.5 m/s, where the hulls actually cruise; and a passenger's deck (the intro)
    /// gets the pull-back too.</para>
    ///
    /// <para>Laws this leans on. Every bar below is a STATED constant (the ruling, or the old scene
    /// numbers), never read back from the code under test. The driven cases run the REAL follow on a
    /// clock the fixture owns (<see cref="CameraFollow.TickFollow"/>), at 60 and 144 fps. And they fly
    /// the SHIPPED config asset, not a patched <see cref="JuiceSettings.Default"/>: the code default
    /// is the old picture on purpose, so a Default-built fixture would measure a camera nobody plays.
    /// The pixel snap is output-only (<c>PixelGridSnapTests</c>), so the follow is measured before it.</para>
    /// </summary>
    public class BoatCameraLeadTests
    {
        private const string ConfigAssetPath = "Assets/_Project/Data/Config/GameConfig.asset";

        // ---- the owner's ruling (2026-09-19), as shipped in the asset -----------------------------
        private const float RuledLeadSeconds = 0.6f;
        private const float RuledLagCompensation = 1f;
        private const float RuledMaxViewFraction = 0.35f;
        private const float RuledPullBackFraction = 0.15f;
        private const float RuledPullBackStartMps = 1f;
        private const float RuledPullBackFullMps = 4.5f;

        // ---- the old follow, as the scenes serialize it (CameraFollow's field defaults) ----------
        private const float SceneLookaheadSeconds = 0.35f;
        private const float SceneLookaheadMaxMeters = 2.5f;
        private const float SceneSmooth = 6f;

        /// <summary>The follow's steady lag at 60 fps, Smooth 6: e^-0.1 / (1 - e^-0.1) / 60 (Phase A).</summary>
        private const float LagSecondsAt60 = 0.158472f;
        /// <summary>…and at 144 fps: e^-(6/144) / (1 - e^-(6/144)) / 144.</summary>
        private const float LagSecondsAt144 = 0.163217f;

        /// <summary>A hull authored at 17 m with no length floor frames on rung 2 at 1080p:
        /// 1080 / (2 × 32) = 16.875 m, so half the view is 8.4375 m.</summary>
        private const float PuntHeight = 17f;
        private const float PuntRungHeight = 16.875f;

        private const float Dt60 = 1f / 60f;
        private const float Dt144 = 1f / 144f;
        private const float FeelDt = 1f / 120f;
        private const float LeadTol = 2e-3f;

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

        // ===== the shipped asset ===================================================================

        [Test]
        public void TheShippedConfig_CarriesTheOwnersBoatCamera_AndTheCodeDefaultIsTheOldPicture()
        {
            GameConfig config = Shipped();

            JuiceSettings d = JuiceSettings.Default;
            Assert.AreEqual(0f, d.BoatLeadSeconds, "harness: the CODE default must stay 0 (the scene's look-ahead)");
            Assert.AreEqual(0f, d.BoatLeadLagCompensation, "harness: the CODE default must stay 0");
            Assert.AreEqual(0f, d.BoatLeadMaxViewFraction, "harness: the CODE default must stay 0 (the scene's metres cap)");
            Assert.IsFalse(d.SpeedPullBackWhenCarriedAboard, "harness: the CODE default must stay OFF — " +
                "the owner's values live in the ASSET, or this guard becomes a mirror of the constructor");

            const string retune = " — if the owner retuned it on purpose, move the stated constant in " +
                                  "BoatCameraLeadTests with it; if not, a Unity save has rewritten the asset";
            JuiceSettings j = config.Juice;
            Assert.AreEqual(RuledLeadSeconds, j.BoatLeadSeconds, 1e-6f, "BoatLeadSeconds" + retune);
            Assert.AreEqual(RuledLagCompensation, j.BoatLeadLagCompensation, 1e-6f, "BoatLeadLagCompensation" + retune);
            Assert.AreEqual(RuledMaxViewFraction, j.BoatLeadMaxViewFraction, 1e-6f, "BoatLeadMaxViewFraction" + retune);
            Assert.AreEqual(RuledPullBackFraction, j.SpeedPullBackFraction, 1e-6f, "SpeedPullBackFraction" + retune);
            Assert.AreEqual(RuledPullBackStartMps, j.SpeedPullBackStartMps, 1e-6f, "SpeedPullBackStartMps" + retune);
            Assert.AreEqual(RuledPullBackFullMps, j.SpeedPullBackFullMps, 1e-6f, "SpeedPullBackFullMps" + retune);
            Assert.IsTrue(j.SpeedPullBackWhenCarriedAboard, "SpeedPullBackWhenCarriedAboard" + retune);

            Assert.IsTrue(j.FeelEnabled && j.FeelOnDeckEnabled,
                "…and the intro's pull-back rides two switches in series: FeelEnabled, then FeelOnDeckEnabled. " +
                "Either off makes SpeedPullBackWhenCarriedAboard dead data");
        }

        [Test]
        public void TheShippedPullBack_StartsWhereTheHullsCruise()
        {
            JuiceSettings s = Shipped().Juice;
            Assert.AreEqual(0f, CameraFeel.PullBackTarget(RuledPullBackStartMps, 0f, s), "at 1 m/s: nothing yet");
            Assert.AreEqual(0.075f, CameraFeel.PullBackTarget(2.75f, 0f, s), 1e-6f,
                "half way between 1 and 4.5 m/s: half of 15 %");
            Assert.AreEqual(0.15f, CameraFeel.PullBackTarget(4.5f, 0f, s), 1e-6f,
                "at 4.5 m/s the whole 15 % — the cape and the lobster boat cruise at 4.2-4.35");
            Assert.Greater(CameraFeel.PullBackTarget(2.03f, 0f, s), 0.03f,
                "the dory's 2 m/s is a visible pull-back now (it was 0 under the old 2.5 m/s start)");
        }

        // ===== the lead, pure ======================================================================

        [Test]
        public void FollowLag_IsThePhaseAMeasure()
        {
            Assert.AreEqual(LagSecondsAt60, CameraFollow.FollowLagSeconds(SceneSmooth, Dt60), 1e-5f);
            Assert.AreEqual(LagSecondsAt144, CameraFollow.FollowLagSeconds(SceneSmooth, Dt144), 1e-5f);
            Assert.AreEqual(1f / 6f, CameraFollow.FollowLagSeconds(SceneSmooth, 1e-4f), 1e-3f, "1/S in the limit");
            Assert.AreEqual(0f, CameraFollow.FollowLagSeconds(SceneSmooth, 0f), "no time, no lag");
            Assert.AreEqual(0f, CameraFollow.FollowLagSeconds(0f, Dt60), "no stiffness, no steady state to pay back");
        }

        [Test]
        public void WithNoCompensation_TheLeadIsTheOldLookaheadExactly()
        {
            var speeds = new[]
            {
                Vector2.zero, new Vector2(2f, 0f), new Vector2(0f, -5f), new Vector2(6f, 8f),
                new Vector2(-20f, 5f), new Vector2(0.3f, 0.1f),
            };
            foreach (Vector2 v in speeds)
                Assert.AreEqual(Vector2.ClampMagnitude(v * SceneLookaheadSeconds, SceneLookaheadMaxMeters),
                    CameraFollow.LeadOffset(v, SceneLookaheadSeconds, SceneLookaheadMaxMeters, 0f, SceneSmooth, Dt60),
                    $"v = {v}");
        }

        [Test]
        public void TheCap_HoldsHerHeading_AndStandingStillHasNoLead()
        {
            float cap = RuledMaxViewFraction * PuntRungHeight * 0.5f;   // 2.953125 m
            Vector2 lead = CameraFollow.LeadOffset(new Vector2(12f, -16f), RuledLeadSeconds, cap, 0f, SceneSmooth, Dt60);
            Assert.AreEqual(cap, lead.magnitude, 1e-5f, "20 m/s × 0.6 s = 12 m, held to the cap");
            Assert.AreEqual(0.6f, lead.x / lead.magnitude, 1e-5f, "along her heading");
            Assert.AreEqual(-0.8f, lead.y / lead.magnitude, 1e-5f, "along her heading");

            Assert.AreEqual(Vector2.zero,
                CameraFollow.LeadOffset(Vector2.zero, RuledLeadSeconds, cap, 1f, SceneSmooth, Dt60),
                "lying still: no lead, and no lag to pay back");
        }

        // ===== the lead, driven through the real follow ============================================

        [Test]
        public void AtTheHelm_TheLeadOnScreenIsTheRuledSeconds_At60And144Fps()
        {
            foreach (float dt in new[] { Dt60, Dt144 })
            foreach (float speed in new[] { 2.03f, 4.2f })   // the dory's and the cape's way at full ahead
            {
                var go = new GameObject("Cam");
                var boat = new GameObject("Boat");
                try
                {
                    GameServices.Config = Shipped();
                    CameraFollow follow = AtTheHelm(go, boat);
                    Assert.AreEqual(RuledLeadSeconds * speed, SteadyLeadAlongX(follow, boat.transform, speed, dt), LeadTol,
                        $"{speed} m/s at {1f / dt:F0} fps: the view leads her by 0.6 s of her way, the follow's lag paid back");
                }
                finally { Object.DestroyImmediate(go); Object.DestroyImmediate(boat); }
            }
        }

        [Test]
        public void AtTheHelm_AFastBoatIsLedToTheCap_NotOffTheScreen()
        {
            var go = new GameObject("Cam");
            var boat = new GameObject("Boat");
            try
            {
                GameServices.Config = Shipped();
                CameraFollow follow = AtTheHelm(go, boat);
                Assert.AreEqual(PuntRungHeight, follow.WorldUnitsPerRenderedPixel * 1080f, 1e-3f,
                    "premise: a 17 m helm framing sits on rung 2 at 1080p");
                Assert.AreEqual(RuledMaxViewFraction * PuntRungHeight * 0.5f,
                    SteadyLeadAlongX(follow, boat.transform, 10f, Dt60), LeadTol,
                    "10 m/s would be 6 m of lead; 0.35 of the half view (2.95 m) is all she gets");
            }
            finally { Object.DestroyImmediate(go); Object.DestroyImmediate(boat); }
        }

        [Test]
        public void AZeroConfig_IsTheOldFollow_AtTheHelm()
        {
            var go = new GameObject("Cam");
            var boat = new GameObject("Boat");
            try
            {
                // No config wired: the code default, every boat dial at 0.
                CameraFollow follow = AtTheHelm(go, boat);
                Assert.AreEqual((SceneLookaheadSeconds - LagSecondsAt60) * 5f,
                    SteadyLeadAlongX(follow, boat.transform, 5f, Dt60), LeadTol,
                    "the Phase A picture: 0.35 s of look-ahead less the follow's 0.1585 s = 0.96 m at 5 m/s");
            }
            finally { Object.DestroyImmediate(go); Object.DestroyImmediate(boat); }
        }

        [Test]
        public void Walking_KeepsTheScenesLookahead_EvenWithTheBoatDialsShipped()
        {
            var go = new GameObject("Cam");
            var walker = new GameObject("Walker");
            try
            {
                GameServices.Config = Shipped();
                CameraFollow follow = WalkingRig(go);
                follow.Target = walker.transform;
                Assert.IsFalse(follow.BoatLeadIsLive);
                Assert.AreEqual((SceneLookaheadSeconds - LagSecondsAt60) * 2f,
                    SteadyLeadAlongX(follow, walker.transform, 2f, Dt60), LeadTol,
                    "on foot the follow is the old one: 0.38 m at 2 m/s");
            }
            finally { Object.DestroyImmediate(go); Object.DestroyImmediate(walker); }
        }

        [Test]
        public void APassenger_GetsTheBoatLead_HerOwnDeckDoesNot()
        {
            var go = new GameObject("Cam");
            var boat = new GameObject("Boat");
            try
            {
                GameServices.Config = Shipped();
                CameraFollow follow = OnTheDeck(go, carried: false);
                follow.Target = boat.transform;
                Assert.IsFalse(follow.BoatLeadIsLive, "her own deck is walking, not a boat's way");
                Assert.AreEqual((SceneLookaheadSeconds - LagSecondsAt60) * 2f,
                    SteadyLeadAlongX(follow, boat.transform, 2f, Dt60), LeadTol);

                follow.OnCarriedAboardChanged(new CarriedAboardChanged(true));
                follow.TickZoom(200.0);
                Assert.AreEqual(PuntRungHeight, follow.WorldUnitsPerRenderedPixel * 1080f, 1e-3f,
                    "premise: a passenger is framed for the hull she rides (the Boat framing, rung 2), so " +
                    "the cap is 2.95 m and not the deck rung's 1.18 m");
                Assert.IsTrue(follow.BoatLeadIsLive, "riding someone else's deck (the intro) follows the boat");
                Assert.AreEqual(RuledLeadSeconds * 2f, SteadyLeadAlongX(follow, boat.transform, 2f, Dt60), LeadTol,
                    "the intro gets the lead too (ruling item 6)");

                follow.OnCabinEntered(new CabinEntered(default, 0));
                Assert.IsFalse(follow.BoatLeadIsLive, "a cabin frames the interior, not the way ahead");
            }
            finally { Object.DestroyImmediate(go); Object.DestroyImmediate(boat); }
        }

        // ===== the passenger's pull-back ===========================================================

        [Test]
        public void APassengersDeck_PullsBackAtSpeed_WithTheOwnersFlag()
        {
            var go = new GameObject("Cam");
            try
            {
                GameServices.Config = Shipped();
                CameraFollow follow = OnTheDeck(go, carried: true);
                Camera cam = go.GetComponent<Camera>();
                float baseOrtho = cam.orthographicSize;

                SettleFeel(follow, 480, speed: 6f);
                Assert.AreEqual(baseOrtho * (1f + RuledPullBackFraction), cam.orthographicSize, 1e-4f,
                    "4 s past 4.5 m/s as a passenger in a flat sea: framing × 1.15");

                SettleFeel(follow, 480, speed: 0f);
                Assert.AreEqual(baseOrtho, cam.orthographicSize, "stopped: the framing's number, exactly");
                Assert.IsFalse(follow.FeelApplied);
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void APassengersDeck_WithTheFlagOff_OrHerOwnDeck_NeverPullsBack()
        {
            foreach (bool flag in new[] { false, true })
            foreach (bool carried in new[] { false, true })
            {
                if (flag && carried) continue;   // the case above
                var go = new GameObject("Cam");
                var cfg = ScriptableObject.CreateInstance<GameConfig>();
                try
                {
                    cfg.Juice = Shipped().Juice;   // the shipped sea, one switch turned
                    cfg.Juice.SpeedPullBackWhenCarriedAboard = flag;
                    GameServices.Config = cfg;
                    CameraFollow follow = OnTheDeck(go, carried);
                    Camera cam = go.GetComponent<Camera>();
                    float baseOrtho = cam.orthographicSize;
                    SettleFeel(follow, 480, speed: 6f);
                    Assert.AreEqual(baseOrtho, cam.orthographicSize, $"flag {flag}, carried {carried}: the deck holds still");
                    Assert.IsFalse(follow.FeelApplied);
                }
                finally { Object.DestroyImmediate(go); Object.DestroyImmediate(cfg); }
            }
        }

        // ===== rigs ================================================================================

        private static GameConfig Shipped()
        {
            var config = UnityEditor.AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigAssetPath);
            Assert.IsNotNull(config, $"the shipped config {ConfigAssetPath} must exist — every scene wires it");
            return config;
        }

        private static CameraFollow WalkingRig(GameObject go)
        {
            Camera cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            CameraFollow follow = go.AddComponent<CameraFollow>();
            follow.OnControlModeChanged(new ControlModeChanged(ControlMode.OnFoot));
            follow.TickZoom(10.0);
            return follow;
        }

        private static CameraFollow AtTheHelm(GameObject go, GameObject boat)
        {
            CameraFollow follow = WalkingRig(go);
            follow.OnActiveBoatChanged(new ActiveBoatChanged("boat.punt", PuntHeight));
            follow.OnControlModeChanged(new ControlModeChanged(ControlMode.Aboard));
            follow.TickZoom(100.0);
            follow.Target = boat.transform;
            Assert.IsTrue(follow.BoatLeadIsLive, "premise: the helm follows the boat");
            return follow;
        }

        /// <summary>On a deck; <paramref name="carried"/> = a passenger on someone else's (the intro),
        /// declared before the framing settles so the rig sits on the Boat framing the intro gets.</summary>
        private static CameraFollow OnTheDeck(GameObject go, bool carried)
        {
            CameraFollow follow = WalkingRig(go);
            follow.OnActiveBoatChanged(new ActiveBoatChanged("boat.punt", PuntHeight));
            follow.OnControlModeChanged(new ControlModeChanged(ControlMode.OnDeck));
            if (carried) follow.OnCarriedAboardChanged(new CarriedAboardChanged(true));
            follow.TickZoom(100.0);
            return follow;
        }

        /// <summary>Runs the follow on the fixture's clock with the target making <paramref name="speed"/>
        /// along +x, long enough for both lerps to settle (the look-ahead's rate 3 is the slow one), and
        /// returns how far ahead of her the camera sits.</summary>
        private static float SteadyLeadAlongX(CameraFollow follow, Transform target, float speed, float dt)
        {
            int frames = Mathf.RoundToInt(12f / dt);
            Vector3 start = target.position;
            for (int i = 1; i <= frames; i++)
            {
                target.position = start + new Vector3(speed * dt * i, 0f, 0f);
                follow.TickFollow(dt);
            }
            Vector3 d = follow.transform.position - target.position;
            Assert.AreEqual(0f, d.y, 1e-4f, "a boat running along x is led along x only");
            return d.x;
        }

        private static void SettleFeel(CameraFollow follow, int ticks, float speed)
        {
            for (int i = 0; i < ticks; i++) follow.TickFeel(FeelDt, speed);
        }
    }
}
