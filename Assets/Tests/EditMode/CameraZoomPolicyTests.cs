using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.App;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The on-deck camera-zoom brain (owner playtest 2026-07-08): stepping onto the DECK steps the
    /// camera IN one discrete pixel-perfect step so deck work reads in detail; a LIVE trap haul may
    /// tighten one step more. <see cref="CameraZoomPolicy"/> is the pure POCO — mode→framing mapping
    /// plus the commit hold (hysteresis) that stops rapid helm⇄deck hops from thrashing the zoom.
    /// </summary>
    public class CameraZoomPolicyTests
    {
        private const double Hold = 0.35; // a representative hold; the live value is owner-tunable

        // ---- mode → framing mapping (pure) ---------------------------------------------------

        [Test]
        public void OnFoot_WantsTheOnFootFraming()
        {
            Assert.AreEqual(CameraFraming.OnFoot,
                CameraZoomPolicy.DesiredFraming(ControlMode.OnFoot, haulLive: false, haulTightensZoom: true));
        }

        [Test]
        public void TheHelm_WantsTheBoatFraming()
        {
            Assert.AreEqual(CameraFraming.Boat,
                CameraZoomPolicy.DesiredFraming(ControlMode.Aboard, haulLive: false, haulTightensZoom: true));
        }

        [Test]
        public void TheDeck_WantsTheCloserDeckStep()
        {
            Assert.AreEqual(CameraFraming.Deck,
                CameraZoomPolicy.DesiredFraming(ControlMode.OnDeck, haulLive: false, haulTightensZoom: true));
        }

        [Test]
        public void ALiveHaulOnDeck_TightensOneStepMore()
        {
            Assert.AreEqual(CameraFraming.DeckHaul,
                CameraZoomPolicy.DesiredFraming(ControlMode.OnDeck, haulLive: true, haulTightensZoom: true));
        }

        [Test]
        public void TheHaulTighten_CanBeDisabled_DeckStepStays()
        {
            Assert.AreEqual(CameraFraming.Deck,
                CameraZoomPolicy.DesiredFraming(ControlMode.OnDeck, haulLive: true, haulTightensZoom: false));
        }

        [Test]
        public void ALiveHaulFlag_NeverTightens_OffTheDeck()
        {
            // The haul is deck work: a stale/rogue haulLive flag must not distort other modes.
            Assert.AreEqual(CameraFraming.OnFoot,
                CameraZoomPolicy.DesiredFraming(ControlMode.OnFoot, haulLive: true, haulTightensZoom: true));
            Assert.AreEqual(CameraFraming.Boat,
                CameraZoomPolicy.DesiredFraming(ControlMode.Aboard, haulLive: true, haulTightensZoom: true));
        }

        // ---- the commit hold (hysteresis) ------------------------------------------------------

        [Test]
        public void TheFirstDesire_CommitsImmediately()
        {
            var policy = new CameraZoomPolicy();
            Assert.IsFalse(policy.HasCommitted);
            Assert.IsTrue(policy.TryCommit(CameraFraming.OnFoot, nowSeconds: 100.0, Hold),
                "a fresh policy commits its first desire at once — a single clean switch feels instant");
            Assert.AreEqual(CameraFraming.OnFoot, policy.Committed);
        }

        [Test]
        public void ASteadyDesire_CommitsExactlyOnce()
        {
            var policy = new CameraZoomPolicy();
            Assert.IsTrue(policy.TryCommit(CameraFraming.Deck, 100.0, Hold));
            for (int i = 1; i <= 50; i++)
                Assert.IsFalse(policy.TryCommit(CameraFraming.Deck, 100.0 + i * 0.016, Hold),
                    "feeding the same desire every frame must never re-commit (no zoom churn)");
        }

        [Test]
        public void AChangeAfterTheHold_CommitsImmediately()
        {
            var policy = new CameraZoomPolicy();
            policy.TryCommit(CameraFraming.Deck, 100.0, Hold);
            Assert.IsTrue(policy.TryCommit(CameraFraming.Boat, 100.0 + Hold + 0.01, Hold),
                "once the hold has expired a new desire lands at once");
            Assert.AreEqual(CameraFraming.Boat, policy.Committed);
        }

        [Test]
        public void AChangeInsideTheHold_IsHeld_ThenLandsWhenTheHoldExpires()
        {
            var policy = new CameraZoomPolicy();
            policy.TryCommit(CameraFraming.Deck, 100.0, Hold);

            // The helm is taken 0.1 s after boarding — inside the hold: no zoom yet.
            Assert.IsFalse(policy.TryCommit(CameraFraming.Boat, 100.1, Hold));
            Assert.AreEqual(CameraFraming.Deck, policy.Committed, "the committed framing holds");

            // Kept fed (the camera ticks every frame) — it lands the moment the hold expires.
            Assert.IsFalse(policy.TryCommit(CameraFraming.Boat, 100.2, Hold));
            Assert.IsTrue(policy.TryCommit(CameraFraming.Boat, 100.0 + Hold, Hold));
            Assert.AreEqual(CameraFraming.Boat, policy.Committed);
        }

        [Test]
        public void AHopThereAndBack_InsideTheHold_NeverReZooms()
        {
            var policy = new CameraZoomPolicy();
            policy.TryCommit(CameraFraming.Deck, 100.0, Hold);

            // deck → helm → deck inside one hold window: the hop dissolves — zero re-zooms.
            Assert.IsFalse(policy.TryCommit(CameraFraming.Boat, 100.1, Hold));
            Assert.IsFalse(policy.TryCommit(CameraFraming.Deck, 100.2, Hold), "back to the committed framing — nothing to do");
            Assert.IsFalse(policy.TryCommit(CameraFraming.Deck, 101.0, Hold), "and nothing later either");
            Assert.AreEqual(CameraFraming.Deck, policy.Committed);
        }

        [Test]
        public void AZeroHold_CommitsEveryChangeImmediately()
        {
            var policy = new CameraZoomPolicy();
            Assert.IsTrue(policy.TryCommit(CameraFraming.Deck, 100.00, 0.0));
            Assert.IsTrue(policy.TryCommit(CameraFraming.Boat, 100.01, 0.0), "hold 0 disables the hysteresis entirely");
            Assert.IsTrue(policy.TryCommit(CameraFraming.Deck, 100.02, 0.0));
        }

        [Test]
        public void RapidToggling_CommitsAtMostOncePerHoldWindow()
        {
            var policy = new CameraZoomPolicy();
            policy.TryCommit(CameraFraming.Deck, 100.0, Hold);

            int commits = 0;
            // A player mashing helm⇄deck at 10 Hz for two seconds: desired alternates every tick.
            for (int i = 1; i <= 20; i++)
            {
                CameraFraming desired = (i % 2 == 0) ? CameraFraming.Deck : CameraFraming.Boat;
                if (policy.TryCommit(desired, 100.0 + i * 0.1, Hold)) commits++;
            }
            Assert.LessOrEqual(commits, 6, "commits are rate-limited by the hold — no per-toggle thrash");
        }

        // ---- the full signal → decision → framing flow on the component -----------------------

        [Test]
        public void BoardingTheDeck_StepsTheCameraIn_ThenTheLiveHaulTightens_AndTheSurfaceReleases()
        {
            var go = new GameObject("Cam");
            try
            {
                var cam = go.AddComponent<Camera>();
                cam.orthographic = true;
                var follow = go.AddComponent<CameraFollow>();

                // Board the deck (Build 5): the camera steps IN to the deck framing.
                follow.OnControlModeChanged(new ControlModeChanged(ControlMode.OnDeck));
                follow.TickZoom(10.0);
                Assert.AreEqual(CameraFollow.OrthoSizeForWorldHeight(CameraFollow.DeckWorldHeightMeters),
                    cam.orthographicSize, 1e-4f, "on deck the camera frames the (closer) deck step");

                // The haul goes live: one more step — the rope is the star.
                follow.OnTrapHaulStateChanged(new TrapHaulStateChanged(
                    new TrapHaulState(TrapHaulPhase.Hauling, 0.5f, 0.1f, false)));
                follow.TickZoom(20.0);
                Assert.AreEqual(CameraFollow.OrthoSizeForWorldHeight(CameraFollow.HaulWorldHeightMeters),
                    cam.orthographicSize, 1e-4f, "a live haul tightens one step more");

                // The pot surfaces: the tighten releases back to the deck step.
                follow.OnTrapHaulStateChanged(new TrapHaulStateChanged(
                    new TrapHaulState(TrapHaulPhase.Surfaced, 0f, 1f, false)));
                follow.TickZoom(30.0);
                Assert.AreEqual(CameraFollow.OrthoSizeForWorldHeight(CameraFollow.DeckWorldHeightMeters),
                    cam.orthographicSize, 1e-4f, "surfacing releases the haul tighten");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void TakingTheHelm_RestoresTheBoatFraming_FromTheActiveBoatSignal()
        {
            var go = new GameObject("Cam");
            try
            {
                var cam = go.AddComponent<Camera>();
                cam.orthographic = true;
                var follow = go.AddComponent<CameraFollow>();

                // Board, then take the helm — the switcher publishes ActiveBoatChanged BEFORE the mode.
                follow.OnControlModeChanged(new ControlModeChanged(ControlMode.OnDeck));
                follow.TickZoom(10.0);
                follow.OnActiveBoatChanged(new ActiveBoatChanged("boat.punt", 17f));
                follow.OnControlModeChanged(new ControlModeChanged(ControlMode.Aboard));
                follow.TickZoom(20.0);

                Assert.AreEqual(CameraFollow.OrthoSizeForWorldHeight(17f), cam.orthographicSize, 1e-4f,
                    "at the helm the camera frames the hull's data-driven height — deck zoom released");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void SteppingAshore_ReturnsToTheOnFootFraming()
        {
            var go = new GameObject("Cam");
            try
            {
                var cam = go.AddComponent<Camera>();
                cam.orthographic = true;
                var follow = go.AddComponent<CameraFollow>();

                follow.OnControlModeChanged(new ControlModeChanged(ControlMode.OnDeck));
                follow.TickZoom(10.0);
                follow.OnControlModeChanged(new ControlModeChanged(ControlMode.OnFoot));
                follow.TickZoom(20.0);

                Assert.AreEqual(CameraFollow.OrthoSizeForWorldHeight(CameraFollow.OnFootWorldHeightMeters),
                    cam.orthographicSize, 1e-4f, "ashore keeps today's on-foot framing — unchanged");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void LeavingTheDeck_ReleasesAStaleHaulTighten()
        {
            var go = new GameObject("Cam");
            try
            {
                var cam = go.AddComponent<Camera>();
                cam.orthographic = true;
                var follow = go.AddComponent<CameraFollow>();

                // A live haul on deck, then ashore WITHOUT an explicit idle beat: the mode change
                // releases the tighten (the haul is deck work; a stale flag must not leak ashore).
                follow.OnControlModeChanged(new ControlModeChanged(ControlMode.OnDeck));
                follow.OnTrapHaulStateChanged(new TrapHaulStateChanged(
                    new TrapHaulState(TrapHaulPhase.Hauling, 0.5f, 0.1f, false)));
                follow.TickZoom(10.0);
                follow.OnControlModeChanged(new ControlModeChanged(ControlMode.OnFoot));
                follow.TickZoom(20.0);
                follow.OnControlModeChanged(new ControlModeChanged(ControlMode.OnDeck));
                follow.TickZoom(30.0);

                Assert.AreEqual(CameraFollow.OrthoSizeForWorldHeight(CameraFollow.DeckWorldHeightMeters),
                    cam.orthographicSize, 1e-4f,
                    "re-boarding after stepping ashore frames the plain deck step, not a stale haul tighten");
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
