using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.App;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// THE WHEEL IS THE PLAYER'S EYE (owner ruling 2026-08-19): the mouse wheel walks the WALKING view
    /// up and down the camera's integer pixel-perfect ladder — closer to read an interior, wider to see
    /// where you are going outdoors.
    ///
    /// <para>The claims that matter, and the reason each is here rather than left to the playtest:</para>
    /// <list type="bullet">
    /// <item><b>Every stop is a crisp integer step.</b> A fractional camera scale shimmers on pixel art,
    /// so a zoom that can stop anywhere is not a zoom this game can ship.</item>
    /// <item><b>The wheel never takes a framing away from its authority.</b> Since the owner ruling of
    /// 2026-08-22 it works aboard and on deck as well, but there it moves an OFFSET inside a band
    /// centred on the ruled framing, and the offset is released on every tier change — so the helm's
    /// per-hull derivation and the deck step are still what she is handed each time she arrives. A
    /// second zoom system fighting them for the same orthographic size is the failure mode this design
    /// exists to avoid, and a band that resets is how it stays avoided.</item>
    /// <item><b>The wheel is live from the first frame.</b> The on-foot dead path of the 2026-08-22
    /// playtest was a liveness gate that waited for a commit the boot sequence never produced.</item>
    /// <item><b>A modal refuses it.</b> One wheel must not scroll a list and zoom the world at once.</item>
    /// <item><b>It is presentation, never simulation</b> (CLAUDE.md rule 5) — nothing sim-side reads it
    /// and nothing saves it.</item>
    /// </list>
    /// </summary>
    public class PlayerZoomTierTests
    {
        private const int Ppu = 32;         // VS-23 locked assets PPU
        private const int ScreenH = 1080;   // the design screen the ladder is tuned at

        // The shipped range as ladder steps. Steps ASCEND inward, so "closest" is the larger number.
        private const int ClosestStep = 6;  // 5.625 m — the live-haul step, the tightest the game ships
        private const int FarthestStep = 3; // 11.25 m — one stop wider than standing on foot

        // The aboard band (owner ruling 2026-08-22): a test hull, the stop it lands on, and the deck's.
        private const float PuntHeight = 17f;   // an authored hull framing between two stops, on purpose
        private const int PuntStep = 2;         // 16.875 m — the stop 17 m quantises to
        private const int DeckStep = 5;         // 6.75 m — the authored deck step
        private const int ShippedStopsCloser = 2;
        private const int ShippedStopsWider = 2;

        [SetUp]
        public void SetUp()
        {
            GameServices.Config = null;
            InteractionGate.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            GameServices.Config = null;
            InteractionGate.Reset();
        }

        // ===== the rules, pure =====================================================================

        [Test]
        public void TheWheelMovesThreeFramings_AndTwoStayRuledOutright()
        {
            foreach (CameraFraming hers in new[]
                     { CameraFraming.OnFoot, CameraFraming.Boat, CameraFraming.Deck })
                Assert.IsTrue(CameraZoomPolicy.PlayerOwnsFraming(hers),
                    $"{hers} is hers to move (owner ruling 2026-08-22 — aboard and on deck too)");

            foreach (CameraFraming ruled in new[] { CameraFraming.DeckHaul, CameraFraming.Vehicle })
                Assert.IsFalse(CameraZoomPolicy.PlayerOwnsFraming(ruled),
                    $"{ruled} is RULED outright — a haul releases itself in seconds and a truck at " +
                    "11 m/s needs every metre of the view her def asks for");
        }

        [Test]
        public void BeforeTheFirstCommit_TheWheelIsTheWalkers()
        {
            // 🔴 THE ON-FOOT DEAD PATH (owner playtest 2026-08-22). This assertion used to read
            // IsFalse, on the reasoning that an un-committed camera has no tier to step from. But the
            // camera only commits from TickZoom, TickZoom waits for a ControlModeChanged, and NOTHING
            // publishes one at boot — the switcher speaks on a transition and on the region-arrival
            // re-assert, and the arrival opening is skipped for any save that has already arrived. So
            // a returning player walked down the wharf with a wheel that was switched off and no error
            // anywhere; boarding once and stepping back ashore "fixed" it, which is how it shipped.
            //
            // Un-committed does not mean "no framing on screen". It means the BUILDER-authored one is,
            // and the builders author the walker's. She is a walker until the game says otherwise.
            Assert.IsTrue(CameraZoomPolicy.WheelIsLive(CameraFraming.OnFoot, hasCommitted: false,
                                                      modalBlocked: false),
                "a camera that has committed nothing is a WALKING camera, and its wheel is hers");

            Assert.AreEqual(CameraFraming.OnFoot,
                CameraZoomPolicy.FramingOnScreen(CameraFraming.Boat, hasCommitted: false),
                "…and what is on screen before any commit is the walker's framing, whatever the " +
                "policy's uninitialised Committed happens to read");
        }

        [Test]
        public void BeforeTheFirstCommit_AModalStillRefusesTheWheel()
        {
            // The dead-path repair must not have unlocked the wheel through a modal on the way past.
            Assert.IsFalse(CameraZoomPolicy.WheelIsLive(CameraFraming.OnFoot, hasCommitted: false,
                                                       modalBlocked: true));
        }

        [Test]
        public void AModalHoldingTheGate_RefusesTheWheel()
        {
            Assert.IsFalse(CameraZoomPolicy.WheelIsLive(CameraFraming.OnFoot, hasCommitted: true,
                                                       modalBlocked: true),
                "a wheel turned over an open notebook or dialogue must not also zoom the world");
        }

        [Test]
        public void OnFootWithNoModal_TheWheelIsLive()
        {
            Assert.IsTrue(CameraZoomPolicy.WheelIsLive(CameraFraming.OnFoot, hasCommitted: true,
                                                      modalBlocked: false));
        }

        [Test]
        public void AtTheHelmAndOnDeck_TheWheelIsLive()
        {
            foreach (CameraFraming banded in new[] { CameraFraming.Boat, CameraFraming.Deck })
                Assert.IsTrue(CameraZoomPolicy.WheelIsLive(banded, hasCommitted: true, modalBlocked: false),
                    $"{banded} takes the wheel inside its band (owner ruling 2026-08-22)");

            foreach (CameraFraming ruled in new[] { CameraFraming.DeckHaul, CameraFraming.Vehicle })
                Assert.IsFalse(CameraZoomPolicy.WheelIsLive(ruled, hasCommitted: true, modalBlocked: false),
                    $"{ruled} is ruled outright — the ruling reached the helm and the deck, not these");
        }

        [Test]
        public void ANotchStepsExactlyOneTier_PositiveIsCloser()
        {
            Assert.AreEqual(5, CameraZoomPolicy.StepPlayerZoom(4, +1, ClosestStep, FarthestStep),
                "+1 notch = one step CLOSER (scroll up zooms in), which is +1 on an inward-ascending ladder");
            Assert.AreEqual(3, CameraZoomPolicy.StepPlayerZoom(4, -1, ClosestStep, FarthestStep));
        }

        [Test]
        public void TheRangeSaturates_ItNeverWraps()
        {
            Assert.AreEqual(ClosestStep,
                CameraZoomPolicy.StepPlayerZoom(ClosestStep, +5, ClosestStep, FarthestStep),
                "a wheel spun hard at the closest tier stays there — it must not wrap to the widest");
            Assert.AreEqual(FarthestStep,
                CameraZoomPolicy.StepPlayerZoom(FarthestStep, -5, ClosestStep, FarthestStep));
        }

        [Test]
        public void ClampsTypedTheWrongWayRound_StillGiveAUsableRange()
        {
            // The owner tunes two METRE values on an asset; nothing stops them being swapped. A range
            // that collapsed to nothing would freeze the wheel with no error anywhere.
            Assert.AreEqual(5, CameraZoomPolicy.ClampPlayerStep(5, FarthestStep, ClosestStep),
                "the bounds normalise — an inverted pair is still the same range");
            Assert.AreEqual(ClosestStep, CameraZoomPolicy.ClampPlayerStep(99, FarthestStep, ClosestStep));
        }

        [Test]
        public void ADegenerateRange_PinsToOneTier_NeverToNone()
        {
            Assert.AreEqual(4, CameraZoomPolicy.ClampPlayerStep(9, 4, 4));
            Assert.AreEqual(4, CameraZoomPolicy.ClampPlayerStep(-9, 4, 4));
        }

        [Test]
        public void TheRangeNeverEscapesTheLadder()
        {
            Assert.AreEqual(CameraZoomPolicy.MaxStep,
                CameraZoomPolicy.ClampPlayerStep(999, 999, 999),
                "a nonsense config is still held inside the ladder's own ends");
            Assert.AreEqual(CameraZoomPolicy.MinStep,
                CameraZoomPolicy.ClampPlayerStep(-999, -999, -999));
        }

        // ===== every reachable stop is crisp ========================================================

        [Test]
        public void EveryReachableStop_IsAnIntegerPixelPerfectStep()
        {
            // THE WHOLE REASON THIS IS TIERED. At PPU 32 on a 1080p screen a stop is only crisp when the
            // screen height divides by (step x ppu) as a whole-number magnification; anything between
            // two stops resamples the art and shimmers as the camera drifts.
            for (int step = FarthestStep; step <= ClosestStep; step++)
            {
                float height = CameraZoomPolicy.WorldHeightForStep(step, Ppu, ScreenH);
                Assert.AreEqual(ScreenH / (float)(step * Ppu), height, 1e-5f,
                    $"step {step} must be the exact x{step} magnification, not a nearby ortho size");
                Assert.IsTrue(CameraZoomPolicy.StepIsPixelPerfectUpscale(step),
                    $"step {step} must be expressible by PixelPerfectCamera — the player's range never " +
                    "crosses the 1:1 pivot into the downscale half of the ladder");
            }
        }

        [Test]
        public void TheShippedRange_IsFourStopsWideAndBracketsTheStandingView()
        {
            int standing = CameraZoomPolicy.StepForWorldHeight(
                CameraFollow.OnFootWorldHeightMeters, Ppu, ScreenH);

            Assert.AreEqual(4, standing, "today's on-foot framing quantises to the x4 stop");
            Assert.That(standing, Is.InRange(FarthestStep, ClosestStep),
                "the walker's home rung must be inside the range, or the wheel starts out of bounds");
            Assert.AreEqual(4, ClosestStep - FarthestStep + 1,
                "four stops: one out from standing, and three in toward the interior close-up");
        }

        // ===== the shipped clamps vs the ladder (the anti-drift tripwire) ===========================

        [Test]
        public void TheShippedClamps_AreTheLaddersOwnStops()
        {
            // ⚠️ GameConfig lives in Core and the ladder lives in the App camera, so the two shipped
            // heights are LITERALS in Core. This is the tripwire that fires if the ladder ever moves
            // (a PPU change, a design-screen change) and leaves those literals behind.
            PlayerZoomSettings shipped = PlayerZoomSettings.Default;

            Assert.AreEqual(CameraZoomPolicy.WorldHeightForStep(ClosestStep, Ppu, ScreenH),
                shipped.ClosestWorldHeightMeters, 1e-5f,
                "the closest clamp must BE the x6 stop, not a number that merely rounds to it");
            Assert.AreEqual(CameraZoomPolicy.WorldHeightForStep(FarthestStep, Ppu, ScreenH),
                shipped.FarthestWorldHeightMeters, 1e-5f,
                "the farthest clamp must BE the x3 stop");
            Assert.IsTrue(shipped.WheelEnabled, "the wheel ships on — the ruling is a feature, not a flag");
        }

        [Test]
        public void TheShippedClosestClamp_IsTheFramingTheGameAlreadyUsesAtItsTightest()
        {
            // Not a new number: the interior close-up reuses the live-haul step, so the closest the
            // player can get is a framing the game has already been played at.
            Assert.AreEqual(CameraFollow.HaulWorldHeightMeters,
                PlayerZoomSettings.Default.ClosestWorldHeightMeters, 1e-5f);
        }

        // ===== the notch accumulator ================================================================

        [Test]
        public void AMouseDetent_IsExactlyOneTier()
        {
            float carry = 0f;
            Assert.AreEqual(1, CameraZoomPolicy.NotchesFromScroll(ref carry, 120f, 120f));
            Assert.AreEqual(0f, carry, 1e-4f, "a whole detent leaves nothing banked");
        }

        [Test]
        public void ATrackpadDribble_AccumulatesToOneTier_InsteadOfFiringEveryFrame()
        {
            // ⚠️ THE BUG THIS PREVENTS: with Mathf.Sign instead of an accumulator, a two-finger scroll
            // reporting a small value every frame would step a whole tier per FRAME and blow through
            // the entire range in a flick.
            float carry = 0f;
            int fired = 0;
            for (int frame = 0; frame < 11; frame++)
                fired += CameraZoomPolicy.NotchesFromScroll(ref carry, 11f, 120f);

            Assert.AreEqual(1, fired, "121 units of dribble is one tier, not eleven");
        }

        [Test]
        public void AReversal_DoesNotHaveToSpendTheBankedCarryFirst()
        {
            float carry = 0f;
            CameraZoomPolicy.NotchesFromScroll(ref carry, 90f, 120f);   // banked 90, no tier yet
            Assert.AreEqual(-1, CameraZoomPolicy.NotchesFromScroll(ref carry, -120f, 120f),
                "flicking back the other way steps back immediately, not after undoing the bank");
        }

        [Test]
        public void ABigSpin_EarnsEveryTierItPaidFor()
        {
            float carry = 0f;
            Assert.AreEqual(3, CameraZoomPolicy.NotchesFromScroll(ref carry, 360f, 120f));
        }

        [Test]
        public void ANonPositiveNotchSize_DegradesToOneTierPerReading_NeverDividesByZero()
        {
            float carry = 5f;
            Assert.AreEqual(1, CameraZoomPolicy.NotchesFromScroll(ref carry, 0.001f, 0f));
            Assert.AreEqual(0f, carry, 1e-5f);
            Assert.AreEqual(0, CameraZoomPolicy.NotchesFromScroll(ref carry, 0f, 0f));
        }

        [Test]
        public void TheAccumulator_IsDeterministic_ReadsNoClock()
        {
            // Rule 5's shape, applied to the input half: the same readings in the same order always
            // produce the same tiers. Nothing here may depend on frame time or a hidden RNG.
            float[] readings = { 40f, -15f, 95f, 120f, 3f, -200f };

            int First()
            {
                float carry = 0f;
                int total = 0;
                foreach (float r in readings) total += CameraZoomPolicy.NotchesFromScroll(ref carry, r, 120f);
                return total;
            }

            Assert.AreEqual(First(), First());
        }

        // ===== the camera applying the rules ========================================================

        private static float Ortho(float worldHeightMeters)
            => CameraFollow.OrthoSizeForWorldHeight(worldHeightMeters);

        private static float StepHeight(int step)
            => CameraZoomPolicy.WorldHeightForStep(step, Ppu, ScreenH);

        /// <summary>A camera rig standing on foot with the zoom policy already committed — the state
        /// every wheel test starts from.</summary>
        private static CameraFollow WalkingRig(GameObject go)
        {
            Camera cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            CameraFollow follow = go.AddComponent<CameraFollow>();
            follow.OnControlModeChanged(new ControlModeChanged(ControlMode.OnFoot));
            follow.TickZoom(10.0);
            return follow;
        }

        /// <summary>The same rig, taken to the helm of a hull framed at
        /// <paramref name="hullHeightMeters"/> with the Boat framing committed.</summary>
        private static CameraFollow AtTheHelm(GameObject go, float hullHeightMeters)
        {
            CameraFollow follow = WalkingRig(go);
            follow.OnActiveBoatChanged(new ActiveBoatChanged("boat.punt", hullHeightMeters));
            follow.OnControlModeChanged(new ControlModeChanged(ControlMode.Aboard));
            follow.TickZoom(100.0);
            return follow;
        }

        [Test]
        public void WithTheWheelUntouched_TheStandingViewIsExactlyWhatItWas()
        {
            // THE PASSTHROUGH CLAIM. A player who never scrolls must get byte-for-byte the camera that
            // shipped — including the RAW authored 9 m request, which ApplyFramingHard drives the
            // orthographic size from (its own comment records two tests catching a snap here before).
            var go = new GameObject("Cam");
            try
            {
                CameraFollow follow = WalkingRig(go);
                Assert.AreEqual(Ortho(CameraFollow.OnFootWorldHeightMeters),
                    go.GetComponent<Camera>().orthographicSize, 1e-4f);
                Assert.AreEqual(CameraFollow.OnFootWorldHeightMeters,
                    follow.PlayerZoomWorldHeightMeters, 1e-4f);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test]
        public void AWheelNotchOnFoot_MovesTheViewOneCrispStepCloser()
        {
            var go = new GameObject("Cam");
            try
            {
                CameraFollow follow = WalkingRig(go);

                Assert.IsTrue(follow.NudgePlayerZoom(+1), "a notch inside the range moves the tier");

                Assert.AreEqual(5, follow.PlayerZoomStep);
                Assert.AreEqual(Ortho(StepHeight(5)), go.GetComponent<Camera>().orthographicSize, 1e-4f,
                    "the view lands on the x5 stop — the same framing deck work uses");
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test]
        public void ScrollingBackToTheHomeRung_RestoresTheAuthoredFramingExactly()
        {
            var go = new GameObject("Cam");
            try
            {
                CameraFollow follow = WalkingRig(go);
                follow.NudgePlayerZoom(+2);
                follow.NudgePlayerZoom(-2);

                Assert.AreEqual(4, follow.PlayerZoomStep);
                Assert.AreEqual(Ortho(CameraFollow.OnFootWorldHeightMeters),
                    go.GetComponent<Camera>().orthographicSize, 1e-4f,
                    "back home is the authored framing, not a rung that merely quantises to it");
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test]
        public void AtTheClamp_TheNudgeIsRefusedAndNothingReFrames()
        {
            var go = new GameObject("Cam");
            try
            {
                CameraFollow follow = WalkingRig(go);
                follow.NudgePlayerZoom(+9);            // saturate at the closest tier
                float atClamp = go.GetComponent<Camera>().orthographicSize;

                Assert.AreEqual(ClosestStep, follow.PlayerZoomStep);
                Assert.IsFalse(follow.NudgePlayerZoom(+1), "a notch past the clamp changes nothing");
                Assert.AreEqual(atClamp, go.GetComponent<Camera>().orthographicSize, 1e-6f,
                    "…and must not re-frame, or the camera would twitch on every blocked notch");
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test]
        public void AtTheHelm_TheWheelStepsInsideTheHullsOwnBand()
        {
            var go = new GameObject("Cam");
            try
            {
                CameraFollow follow = AtTheHelm(go, PuntHeight);

                Assert.AreEqual(Ortho(PuntHeight), go.GetComponent<Camera>().orthographicSize, 1e-4f,
                    "arriving at the helm hands her the hull's ruled framing, untouched");
                Assert.IsTrue(follow.WheelIsLive, "…and the wheel is live there now (ruling 2026-08-22)");

                Assert.IsTrue(follow.NudgePlayerZoom(+1));
                Assert.AreEqual(1, follow.PlayerBandOffset);
                Assert.AreEqual(Ortho(StepHeight(PuntStep + 1)),
                    go.GetComponent<Camera>().orthographicSize, 1e-4f,
                    "one stop in from the hull's own stop — a stop on the same ladder, not a new size");

                Assert.IsTrue(follow.NudgePlayerZoom(-1));
                Assert.AreEqual(0, follow.PlayerBandOffset);
                Assert.AreEqual(Ortho(PuntHeight), go.GetComponent<Camera>().orthographicSize, 1e-4f,
                    "…and back at offset zero she is on the AUTHORED hull height byte-for-byte, not " +
                    "on the step it merely quantises to");
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test]
        public void TheAboardOffsetClampsInsideTheBand_AndABlockedNotchNeverReFrames()
        {
            var go = new GameObject("Cam");
            try
            {
                CameraFollow follow = AtTheHelm(go, PuntHeight);

                follow.NudgePlayerZoom(+9);                       // spin it hard
                Assert.AreEqual(ShippedStopsCloser, follow.PlayerBandOffset,
                    "the band saturates at the owner's allowance — it never runs away up the ladder");
                float atClamp = go.GetComponent<Camera>().orthographicSize;
                Assert.AreEqual(Ortho(StepHeight(PuntStep + ShippedStopsCloser)), atClamp, 1e-4f);

                Assert.IsFalse(follow.NudgePlayerZoom(+1), "a notch past the band does nothing");
                Assert.AreEqual(atClamp, go.GetComponent<Camera>().orthographicSize, 1e-6f,
                    "…and must not re-frame, or the camera twitches on every blocked notch");

                follow.NudgePlayerZoom(-99);                      // and hard the other way
                Assert.AreEqual(-ShippedStopsWider, follow.PlayerBandOffset);
                Assert.IsFalse(follow.NudgePlayerZoom(-1));
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test]
        public void OnDeck_TheWheelStepsTheDeckBand_ButALiveHaulStaysRuledOutright()
        {
            var go = new GameObject("Cam");
            try
            {
                CameraFollow follow = WalkingRig(go);

                follow.OnControlModeChanged(new ControlModeChanged(ControlMode.OnDeck));
                follow.TickZoom(20.0);
                Assert.IsTrue(follow.NudgePlayerZoom(-1), "the deck takes the wheel inside its band");
                Assert.AreEqual(Ortho(StepHeight(DeckStep - 1)),
                    go.GetComponent<Camera>().orthographicSize, 1e-4f);

                follow.OnTrapHaulStateChanged(new TrapHaulStateChanged(
                    new TrapHaulState(TrapHaulPhase.Hauling, 0.5f, 0.1f, false)));
                follow.TickZoom(30.0);

                Assert.AreEqual(Ortho(CameraFollow.HaulWorldHeightMeters),
                    go.GetComponent<Camera>().orthographicSize, 1e-4f,
                    "a haul starting is a tier change: her band is released and the rope is the star");
                Assert.AreEqual(0, follow.PlayerBandOffset);
                Assert.IsFalse(follow.WheelIsLive, "and the haul framing is ruled outright while it lives");
                Assert.IsFalse(follow.NudgePlayerZoom(-2));
                Assert.AreEqual(Ortho(CameraFollow.HaulWorldHeightMeters),
                    go.GetComponent<Camera>().orthographicSize, 1e-4f);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test]
        public void ATierChangeReleasesTheBand_HelmToDeckAndBack()
        {
            var go = new GameObject("Cam");
            try
            {
                CameraFollow follow = AtTheHelm(go, PuntHeight);
                follow.NudgePlayerZoom(+2);
                Assert.AreEqual(2, follow.PlayerBandOffset, "she is looking in close at the helm");

                follow.OnControlModeChanged(new ControlModeChanged(ControlMode.OnDeck));
                follow.TickZoom(200.0);
                Assert.AreEqual(0, follow.PlayerBandOffset, "stepping onto the deck releases the band");
                Assert.AreEqual(Ortho(CameraFollow.DeckWorldHeightMeters),
                    go.GetComponent<Camera>().orthographicSize, 1e-4f,
                    "…and the deck hands her the deck step, not the helm's leftover zoom");

                follow.NudgePlayerZoom(-1);
                follow.OnControlModeChanged(new ControlModeChanged(ControlMode.Aboard));
                follow.TickZoom(300.0);
                Assert.AreEqual(0, follow.PlayerBandOffset, "and back the other way, the same");
                Assert.AreEqual(Ortho(PuntHeight), go.GetComponent<Camera>().orthographicSize, 1e-4f);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test]
        public void ANewHullReleasesTheBand_ButAMereRegionHopDoesNot()
        {
            // ⚠️ THE DEFECT THIS PREVENTS is §9.8's, arriving through the back door: an upgrade that
            // framed the NEW boat at the OLD boat's zoom. And the reason the reset is guarded on the
            // framing actually changing is the other half — ActiveBoatChanged is re-published verbatim
            // on every region arrival (ControlSwitcher.ReassertControlMode), and a hop across a
            // passage is not a new boat.
            var go = new GameObject("Cam");
            try
            {
                CameraFollow follow = AtTheHelm(go, PuntHeight);
                follow.NudgePlayerZoom(+2);
                Assert.AreEqual(2, follow.PlayerBandOffset);

                follow.OnActiveBoatChanged(new ActiveBoatChanged("boat.punt", PuntHeight));
                Assert.AreEqual(2, follow.PlayerBandOffset,
                    "the same hull restated is a landfall, not a new boat — her look survives it");

                follow.OnActiveBoatChanged(new ActiveBoatChanged("boat.dragger", 40f));
                Assert.AreEqual(0, follow.PlayerBandOffset, "a DIFFERENT hull is a tier change");
                Assert.AreEqual(Ortho(40f), go.GetComponent<Camera>().orthographicSize, 1e-4f,
                    "…and the new hull is framed as IT was ruled, not as the last one was looked at");
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test]
        public void TheWalkersRungIsNotTheAboardBand_TheyAreDifferentStateAndBothSurvive()
        {
            // The two pieces of player state must not be the same variable: her RUNG is absolute and
            // survives the whole voyage; her aboard OFFSET is relative and dies at every tier change.
            var go = new GameObject("Cam");
            try
            {
                CameraFollow follow = WalkingRig(go);
                follow.NudgePlayerZoom(+2);
                Assert.AreEqual(ClosestStep, follow.PlayerZoomStep);

                follow.OnActiveBoatChanged(new ActiveBoatChanged("boat.punt", PuntHeight));
                follow.OnControlModeChanged(new ControlModeChanged(ControlMode.Aboard));
                follow.TickZoom(200.0);
                follow.NudgePlayerZoom(-2);                       // a wide look around at the helm
                Assert.AreEqual(-2, follow.PlayerBandOffset);
                Assert.AreEqual(ClosestStep, follow.PlayerZoomStep,
                    "…which must not have touched the rung she is going to step back onto");

                follow.OnControlModeChanged(new ControlModeChanged(ControlMode.OnFoot));
                follow.TickZoom(300.0);
                Assert.AreEqual(0, follow.PlayerBandOffset);
                Assert.AreEqual(ClosestStep, follow.PlayerZoomStep, "her tier survived the trip");
                Assert.AreEqual(Ortho(StepHeight(ClosestStep)),
                    go.GetComponent<Camera>().orthographicSize, 1e-4f);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test]
        public void TheWheelWorksOnTheVeryFirstFrame_BeforeControlHasSaidAnything()
        {
            // 🔴 THE OWNER'S BUG, END TO END, THROUGH THE REAL CAMERA. No ControlModeChanged has ever
            // arrived — the exact state a returning save boots into — and one detent must still move
            // the walking view one crisp stop.
            var go = new GameObject("Cam");
            try
            {
                Camera cam = go.AddComponent<Camera>();
                cam.orthographic = true;
                CameraFollow follow = go.AddComponent<CameraFollow>();

                Assert.IsFalse(follow.FramingKnown, "the state under test: nothing has committed");
                Assert.IsTrue(follow.WheelIsLive, "the wheel must be hers from the first frame");

                Assert.IsTrue(follow.NudgePlayerZoom(+1));
                Assert.AreEqual(5, follow.PlayerZoomStep);
                Assert.AreEqual(Ortho(StepHeight(5)), cam.orthographicSize, 1e-4f);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test]
        public void BoardingThenSteppingAshore_RestoresTheWalkersOwnTier()
        {
            // The whole handoff, end to end: the walker picks a rung, the boat overrides it with its
            // ruled framing for the whole trip, and stepping ashore puts her back where she was.
            var go = new GameObject("Cam");
            try
            {
                CameraFollow follow = WalkingRig(go);
                follow.NudgePlayerZoom(+2);                          // she likes it close
                Assert.AreEqual(6, follow.PlayerZoomStep);

                follow.OnActiveBoatChanged(new ActiveBoatChanged("boat.punt", 17f));
                follow.OnControlModeChanged(new ControlModeChanged(ControlMode.Aboard));
                follow.TickZoom(20.0);
                Assert.AreEqual(Ortho(17f), go.GetComponent<Camera>().orthographicSize, 1e-4f,
                    "the hull's ruled framing wins outright while aboard");

                follow.OnControlModeChanged(new ControlModeChanged(ControlMode.OnFoot));
                follow.TickZoom(30.0);

                Assert.AreEqual(6, follow.PlayerZoomStep, "her tier survived the trip");
                Assert.AreEqual(Ortho(StepHeight(6)), go.GetComponent<Camera>().orthographicSize, 1e-4f,
                    "ashore she gets her own rung back — not the standing default");
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test]
        public void AModalHoldingTheGate_RefusesTheNudge()
        {
            var go = new GameObject("Cam");
            try
            {
                CameraFollow follow = WalkingRig(go);
                float before = go.GetComponent<Camera>().orthographicSize;

                InteractionGate.IsBlocked = true;
                Assert.IsFalse(follow.WheelIsLive);
                Assert.IsFalse(follow.NudgePlayerZoom(+1),
                    "one wheel must not scroll a modal's list and zoom the world at the same time");
                Assert.AreEqual(before, go.GetComponent<Camera>().orthographicSize, 1e-6f);

                InteractionGate.IsBlocked = false;
                Assert.IsTrue(follow.WheelIsLive, "closing the modal gives the wheel straight back");
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test]
        public void WithTheWheelDisabledOnTheConfig_TheStandingViewNeverMoves()
        {
            var config = ScriptableObject.CreateInstance<GameConfig>();
            var go = new GameObject("Cam");
            try
            {
                PlayerZoomSettings off = PlayerZoomSettings.Default;
                off.WheelEnabled = false;
                config.PlayerZoom = off;
                GameServices.Config = config;

                CameraFollow follow = WalkingRig(go);
                Assert.IsFalse(follow.WheelIsLive);
                Assert.IsFalse(follow.NudgePlayerZoom(+1));
                Assert.AreEqual(Ortho(CameraFollow.OnFootWorldHeightMeters),
                    go.GetComponent<Camera>().orthographicSize, 1e-4f);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
                UnityEngine.Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void TheOwnersRange_IsHonouredLive_WithNoSceneRebuild()
        {
            // Rule 6's promise: the owner drags two numbers on the GameConfig asset and the wheel's
            // reach changes on the next notch — no code, no rebuild.
            var config = ScriptableObject.CreateInstance<GameConfig>();
            var go = new GameObject("Cam");
            try
            {
                PlayerZoomSettings narrow = PlayerZoomSettings.Default;
                narrow.ClosestWorldHeightMeters = CameraZoomPolicy.WorldHeightForStep(5, Ppu, ScreenH);
                config.PlayerZoom = narrow;
                GameServices.Config = config;

                CameraFollow follow = WalkingRig(go);
                follow.NudgePlayerZoom(+9);

                Assert.AreEqual(5, follow.PlayerZoomStep,
                    "the owner's tightened range holds the wheel one stop short of the shipped one");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
                UnityEngine.Object.DestroyImmediate(config);
            }
        }

        // ===== the three dead-path suspects, each nailed to a rule ==================================
        //
        // The owner's 2026-08-22 playtest said only "on foot the wheel did nothing". Reading found the
        // culprit — the liveness gate waiting on a commit the boot sequence never produces, covered by
        // BeforeTheFirstCommit_TheWheelIsTheWalkers above. These three are the other suspects that were
        // ruled out by reading: each was a plausible silent way for a wheel to do nothing, and each is
        // pinned here so it can never become the answer to the same report a second time.

        [Test]
        public void SUSPECT_AWindowsDetentIsOneRung_AndTheCarryNeitherEatsItNorBanksIt()
        {
            // SUSPECT 1: the carry accumulator vs Windows' ±120 per detent. A threshold the device can
            // never reach — or one it overshoots into a banked remainder that fires later — both read
            // to a player as "the wheel does nothing" / "the wheel does it twice".
            float carry = 0f;
            for (int detent = 1; detent <= 5; detent++)
            {
                Assert.AreEqual(1, CameraZoomPolicy.NotchesFromScroll(ref carry, 120f, 120f),
                    $"detent {detent}: one detent is one rung, every time");
                Assert.AreEqual(0f, carry, 1e-4f, $"detent {detent}: nothing left banked to fire later");
            }
        }

        [Test]
        public void SUSPECT_OneDetentOnFoot_IsExactlyOneRung_ThroughTheWholeChain()
        {
            // …and the same claim through the real camera, because the accumulator being right is only
            // half of it: the notch has to survive the liveness gate and the clamps to reach a framing.
            var go = new GameObject("Cam");
            try
            {
                CameraFollow follow = WalkingRig(go);
                float carry = 0f;
                // 1f = one detent as the Input System actually reports it (owner-measured
                // 2026-08-26). Feeding Win32's raw 120 here is how a dead wheel stayed green for
                // three days: the device never sends that scale, so a test that does is testing a
                // machine that does not exist.
                int notches = CameraZoomPolicy.NotchesFromScroll(ref carry, 1f, follow.WheelUnitsPerNotch);

                Assert.AreEqual(1, notches, "the shipped notch size must be the shipped device's detent");
                Assert.IsTrue(follow.NudgePlayerZoom(notches));
                Assert.AreEqual(5, follow.PlayerZoomStep, "one detent, one rung — no more, no less");
                Assert.AreEqual(Ortho(StepHeight(5)), go.GetComponent<Camera>().orthographicSize, 1e-4f);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test]
        public void SUSPECT_TheWalkersHomeRungIsStrictlyInsideTheShippedBand_NotSittingOnABound()
        {
            // SUSPECT 2: the clamp collapsing because the on-foot height sits ON a bound. A range that
            // pins the walker to her own starting rung is a wheel that does nothing, with no error
            // anywhere — and one edited metre on the config asset is all it takes.
            int home = CameraZoomPolicy.StepForWorldHeight(
                CameraFollow.OnFootWorldHeightMeters, Ppu, ScreenH);

            Assert.Greater(home, FarthestStep, "she must have somewhere to go OUT to");
            Assert.Less(home, ClosestStep, "…and somewhere to go IN to");
            Assert.AreNotEqual(home, CameraZoomPolicy.StepPlayerZoom(home, +1, ClosestStep, FarthestStep),
                "a notch in must move her");
            Assert.AreNotEqual(home, CameraZoomPolicy.StepPlayerZoom(home, -1, ClosestStep, FarthestStep),
                "a notch out must move her");
        }

        [Test]
        public void SUSPECT_ARangePinnedToTheHomeRung_RefusesTheNudgeRatherThanFakingIt()
        {
            // The same collapse, deliberately configured: the honest behaviour is a refusal the caller
            // can see (false), never a silent no-op that reports success.
            var config = ScriptableObject.CreateInstance<GameConfig>();
            var go = new GameObject("Cam");
            try
            {
                float home = CameraZoomPolicy.WorldHeightForStep(4, Ppu, ScreenH);
                PlayerZoomSettings pinned = PlayerZoomSettings.Default;
                pinned.ClosestWorldHeightMeters = home;
                pinned.FarthestWorldHeightMeters = home;
                config.PlayerZoom = pinned;
                GameServices.Config = config;

                CameraFollow follow = WalkingRig(go);
                Assert.AreEqual(4, follow.PlayerZoomStep);
                Assert.IsFalse(follow.NudgePlayerZoom(+1));
                Assert.IsFalse(follow.NudgePlayerZoom(-1));
                Assert.AreEqual(4, follow.PlayerZoomStep, "pinned to one rung, and it says so");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
                UnityEngine.Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void SUSPECT_AConfigOlderThanTheFeature_DoesNotSilentlySwitchTheWheelOff()
        {
            // SUSPECT 3: WheelEnabled read off the wrong config instance. The shape that actually bites
            // is a GameConfig asset serialized BEFORE PlayerZoom existed: Unity deserializes the absent
            // block as default(T), which reads as a wheel that is off with a range of 0 m to 0 m. Every
            // one of those is silent, and [Min] polices the Inspector rather than the deserializer.
            PlayerZoomSettings unauthored = default;
            Assert.IsTrue(unauthored.IsUnauthored);

            PlayerZoomSettings healed = unauthored.Sanitized();
            Assert.IsTrue(healed.WheelEnabled, "an unwritten block is not an owner switching the wheel off");
            Assert.AreEqual(PlayerZoomSettings.Default.ClosestWorldHeightMeters,
                healed.ClosestWorldHeightMeters, 1e-5f);
            Assert.AreEqual(PlayerZoomSettings.Default.FarthestWorldHeightMeters,
                healed.FarthestWorldHeightMeters, 1e-5f);
            Assert.AreEqual(PlayerZoomSettings.Default.WheelUnitsPerNotch, healed.WheelUnitsPerNotch, 1e-5f);
        }

        [Test]
        public void SUSPECT_TheOwnersOwnOffSwitch_IsNeverHealedAway()
        {
            // The other half of the same rule, and the reason IsUnauthored asks about the WHOLE struct:
            // an owner who turns the wheel off keeps the rest of their tuning, so the healing must not
            // reach them. This is the assertion that stops the fix above becoming a new bug.
            PlayerZoomSettings off = PlayerZoomSettings.Default;
            off.WheelEnabled = false;

            Assert.IsFalse(off.IsUnauthored);
            Assert.IsFalse(off.Sanitized().WheelEnabled, "off on purpose stays off");
        }

        [Test]
        public void SUSPECT_AHalfWrittenRange_HealsOnlyTheValuesThatCannotMeanAnything()
        {
            PlayerZoomSettings half = PlayerZoomSettings.Default;
            half.FarthestWorldHeightMeters = 0f;      // a clamp of 0 m is not a framing
            half.ClosestWorldHeightMeters = 6.75f;    // …but this one is, and it is the owner's

            PlayerZoomSettings healed = half.Sanitized();
            Assert.AreEqual(PlayerZoomSettings.Default.FarthestWorldHeightMeters,
                healed.FarthestWorldHeightMeters, 1e-5f);
            Assert.AreEqual(6.75f, healed.ClosestWorldHeightMeters, 1e-5f,
                "healing an unset value must not overwrite an authored neighbour");
        }

        // ===== the ladder holds, at every framing the wheel can reach ================================

        [Test]
        public void TheLadderWalkSkipsTheTwoNonSteps_AtThePixelPerfectPivot()
        {
            // Steps 0 and -1 are not steps, so the sequence runs … -3, -2, 1, 2 … A band centred on a
            // BIG hull sits at or past that pivot, and plain step arithmetic would walk into the hole.
            Assert.AreEqual(-2, CameraZoomPolicy.StepClosestBy(1, -1), "one stop wider than 1:1 is 2:1");
            Assert.AreEqual(1, CameraZoomPolicy.StepClosestBy(-2, +1), "…and one stop back in is 1:1");
            Assert.AreEqual(-3, CameraZoomPolicy.StepClosestBy(1, -2));
            Assert.AreEqual(2, CameraZoomPolicy.StepClosestBy(-2, +2));

            foreach (int stops in new[] { -99, -8, -1, 0, 1, 8, 99 })
                foreach (int from in new[] { CameraZoomPolicy.MinStep, -2, 1, CameraZoomPolicy.MaxStep })
                {
                    int landed = CameraZoomPolicy.StepClosestBy(from, stops);
                    Assert.AreNotEqual(0, landed, $"{from} + {stops} stops landed on the 0 non-step");
                    Assert.AreNotEqual(-1, landed, $"{from} + {stops} stops landed on the -1 non-step");
                    Assert.That(landed, Is.InRange(CameraZoomPolicy.MinStep, CameraZoomPolicy.MaxStep));
                }
        }

        [Test]
        public void NoFramingTheWheelCanReach_IsEverANonIntegerPPU()
        {
            // ⭐ THE STANDING CONSTRAINT, swept across everything the 2026-08-22 ruling opened up: the
            // walker's whole range, and every band offset around every framing a hull or a deck can
            // ask for. A fractional camera scale shimmers on pixel art, so a stop that is not a whole
            // number of asset pixels to a screen pixel is not a stop this game may reach.
            foreach (float ruled in new[] { 6.75f, 9f, 14f, PuntHeight, 40f, 60f, 90f, 160f })
                for (int offset = -ShippedStopsWider; offset <= ShippedStopsCloser; offset++)
                {
                    int step = CameraZoomPolicy.BandStep(ruled, offset,
                        ShippedStopsCloser, ShippedStopsWider, Ppu, ScreenH);
                    AssertIsACrispStop(step, $"{ruled} m at band offset {offset}");
                }

            for (int rung = FarthestStep; rung <= ClosestStep; rung++)
                AssertIsACrispStop(rung, $"the walker's rung {rung}");
        }

        /// <summary>A ladder step is crisp when it is a REAL step and its world height is a whole-number
        /// magnification of the asset grid — an integer upscale, or an integer downscale of it.</summary>
        private static void AssertIsACrispStop(int step, string what)
        {
            Assert.AreNotEqual(0, step, $"{what}: 0 is not a step");
            Assert.AreNotEqual(-1, step, $"{what}: -1 is not a step");
            Assert.That(step, Is.InRange(CameraZoomPolicy.MinStep, CameraZoomPolicy.MaxStep), what);

            float height = CameraZoomPolicy.WorldHeightForStep(step, Ppu, ScreenH);
            float expected = step >= 1 ? ScreenH / (float)(step * Ppu) : ScreenH * -step / (float)Ppu;
            Assert.AreEqual(expected, height, 1e-5f,
                $"{what}: step {step} must be the exact x{step} ratio, not a nearby ortho size");

            float magnification = step >= 1 ? ScreenH / (height * Ppu) : height * Ppu / ScreenH;
            Assert.AreEqual(Mathf.Round(magnification), magnification, 1e-4f,
                $"{what}: {height} m is a fractional asset-pixel scale — it would shimmer");
        }

        [Test]
        public void TheBandIsAnOffset_NotARung_AndThatIsWhatKeepsTheFramingTheHulls()
        {
            // Two hulls, the same look. If the band stored a RUNG instead of an offset, the second hull
            // would be framed at the first hull's zoom — §9.8's defect, wearing a different hat.
            int fromPunt = CameraZoomPolicy.BandStep(PuntHeight, +1,
                ShippedStopsCloser, ShippedStopsWider, Ppu, ScreenH);
            int fromDragger = CameraZoomPolicy.BandStep(40f, +1,
                ShippedStopsCloser, ShippedStopsWider, Ppu, ScreenH);

            Assert.AreEqual(PuntStep + 1, fromPunt);
            Assert.AreNotEqual(fromPunt, fromDragger,
                "the same offset off two different hulls is two different stops — that is the point");
        }

        [Test]
        public void AZeroBand_TurnsTheAboardWheelOffWithoutTouchingTheWalkersRange()
        {
            // The owner's off switch for just this half of the ruling.
            Assert.AreEqual(0, CameraZoomPolicy.ClampBandOffset(+3, 0, 0));
            Assert.AreEqual(0, CameraZoomPolicy.ClampBandOffset(-3, 0, 0));

            CameraZoomPolicy.StepBandOffset(0, +1, PuntHeight, 0, 0, Ppu, ScreenH, out bool moved);
            Assert.IsFalse(moved, "with no allowance a notch aboard moves nothing and says so");
        }

        [Test]
        public void ANegativeAllowance_ReadsAsNoStopsThatWay_NeverAsAnInvertedBand()
        {
            Assert.AreEqual(2, CameraZoomPolicy.ClampBandOffset(+9, -2, 2),
                "a negative typed into an allowance is a magnitude, not a flipped band");
            Assert.AreEqual(-2, CameraZoomPolicy.ClampBandOffset(-9, 2, -2));
        }

        [Test]
        public void TheOwnersBand_IsHonouredLive_WithNoSceneRebuild()
        {
            // Rule 6 again, for the band: the owner drags a number and the next notch obeys it.
            var config = ScriptableObject.CreateInstance<GameConfig>();
            var go = new GameObject("Cam");
            try
            {
                PlayerZoomSettings narrow = PlayerZoomSettings.Default;
                narrow.AboardStopsCloser = 1;
                narrow.AboardStopsWider = 0;
                config.PlayerZoom = narrow;
                GameServices.Config = config;

                CameraFollow follow = AtTheHelm(go, PuntHeight);
                follow.NudgePlayerZoom(+9);
                Assert.AreEqual(1, follow.PlayerBandOffset, "one stop in is all this owner allows");

                Assert.IsTrue(follow.NudgePlayerZoom(-1), "coming back to the ruled framing is always on");
                Assert.AreEqual(0, follow.PlayerBandOffset);
                Assert.IsFalse(follow.NudgePlayerZoom(-1),
                    "…but with no WIDER allowance she cannot go out past it at all");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
                UnityEngine.Object.DestroyImmediate(config);
            }
        }

        // ===== determinism: the zoom is presentation (rule 5) =======================================

        [Test]
        public void TheSameWheelInput_AlwaysLandsOnTheSameTier()
        {
            int Run()
            {
                var go = new GameObject("Cam");
                try
                {
                    CameraFollow follow = WalkingRig(go);
                    foreach (int n in new[] { +1, +1, -1, +3, -2 }) follow.NudgePlayerZoom(n);
                    return follow.PlayerZoomStep;
                }
                finally { UnityEngine.Object.DestroyImmediate(go); }
            }

            Assert.AreEqual(Run(), Run(), "the tier is a pure function of the notches — no hidden state");
        }

        [Test]
        public void TheZoomIsNeverSaved()
        {
            // ⚠️ THE TRIPWIRE. Tide, wind and weather are recomputed rather than saved (rule 5) and the
            // camera's zoom is the same kind of thing: derived presentation. If anybody ever adds it to
            // the save this fires, and the save-format review that should accompany it happens.
            var offenders = SaveFieldNames(typeof(SaveData), depth: 2)
                .Where(n => n.IndexOf("zoom", StringComparison.OrdinalIgnoreCase) >= 0
                            || n.IndexOf("framing", StringComparison.OrdinalIgnoreCase) >= 0
                            || n.IndexOf("camera", StringComparison.OrdinalIgnoreCase) >= 0)
                .ToArray();

            CollectionAssert.IsEmpty(offenders,
                "the camera's zoom is presentation and must not enter the save: " + string.Join(", ", offenders));
        }

        private static System.Collections.Generic.IEnumerable<string> SaveFieldNames(Type t, int depth)
        {
            foreach (FieldInfo f in t.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                yield return $"{t.Name}.{f.Name}";
                if (depth <= 0) continue;

                Type inner = f.FieldType;
                if (inner.IsArray) inner = inner.GetElementType();
                else if (inner.IsGenericType && inner.GetGenericArguments().Length == 1)
                    inner = inner.GetGenericArguments()[0];   // List<PlacedTrapDto> → PlacedTrapDto
                if (inner == null || inner.IsPrimitive || inner == typeof(string) || inner.IsEnum) continue;
                if (inner.Namespace == null || !inner.Namespace.StartsWith("HiddenHarbours")) continue;

                foreach (string n in SaveFieldNames(inner, depth - 1)) yield return n;
            }
        }
    }
}
