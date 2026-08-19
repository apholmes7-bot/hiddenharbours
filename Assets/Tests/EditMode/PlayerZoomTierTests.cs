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
    /// <item><b>The wheel owns ONE framing.</b> The helm's ruled per-hull framing, the deck step and the
    /// haul tighten are decided by their own authority; a second zoom system fighting them for the same
    /// orthographic size is the failure mode this design exists to avoid.</item>
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
        public void TheWheelOwnsTheWalkingViewAndNothingElse()
        {
            Assert.IsTrue(CameraZoomPolicy.PlayerOwnsFraming(CameraFraming.OnFoot),
                "the walking view is the player's to zoom");

            foreach (CameraFraming ruled in new[]
                     {
                         CameraFraming.Boat, CameraFraming.Deck,
                         CameraFraming.DeckHaul, CameraFraming.Vehicle,
                     })
                Assert.IsFalse(CameraZoomPolicy.PlayerOwnsFraming(ruled),
                    $"{ruled} is RULED by its own authority — the wheel must never move it");
        }

        [Test]
        public void BeforeTheFirstCommit_TheWheelIsDead()
        {
            // Until control declares itself the builder-authored framing rules and there is no rung to
            // step from. A wheel that worked here would fight the opening shot of the game.
            Assert.IsFalse(CameraZoomPolicy.WheelIsLive(CameraFraming.OnFoot, hasCommitted: false,
                                                       modalBlocked: false));
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
        public void AtTheHelmAndOnDeck_TheWheelIsRefused()
        {
            foreach (CameraFraming ruled in new[]
                     {
                         CameraFraming.Boat, CameraFraming.Deck,
                         CameraFraming.DeckHaul, CameraFraming.Vehicle,
                     })
                Assert.IsFalse(CameraZoomPolicy.WheelIsLive(ruled, hasCommitted: true, modalBlocked: false),
                    $"{ruled} is ruled — boarding hands authority back, it is not shared");
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
        public void TheWheelCannotMoveTheHelmFraming()
        {
            var go = new GameObject("Cam");
            try
            {
                CameraFollow follow = WalkingRig(go);
                follow.OnActiveBoatChanged(new ActiveBoatChanged("boat.punt", 17f));
                follow.OnControlModeChanged(new ControlModeChanged(ControlMode.Aboard));
                follow.TickZoom(20.0);
                float atHelm = go.GetComponent<Camera>().orthographicSize;

                Assert.IsFalse(follow.WheelIsLive, "the helm's framing is the hull's, not the player's");
                Assert.IsFalse(follow.NudgePlayerZoom(+3));
                Assert.AreEqual(atHelm, go.GetComponent<Camera>().orthographicSize, 1e-6f,
                    "spinning the wheel at the helm must leave the ruled per-hull framing alone");
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        [Test]
        public void TheWheelCannotMoveTheDeckStepOrAHaulTighten()
        {
            var go = new GameObject("Cam");
            try
            {
                CameraFollow follow = WalkingRig(go);

                follow.OnControlModeChanged(new ControlModeChanged(ControlMode.OnDeck));
                follow.TickZoom(20.0);
                Assert.IsFalse(follow.NudgePlayerZoom(-2), "the deck step is deck work's, not the wheel's");
                Assert.AreEqual(Ortho(CameraFollow.DeckWorldHeightMeters),
                    go.GetComponent<Camera>().orthographicSize, 1e-4f);

                follow.OnTrapHaulStateChanged(new TrapHaulStateChanged(
                    new TrapHaulState(TrapHaulPhase.Hauling, 0.5f, 0.1f, false)));
                follow.TickZoom(30.0);
                Assert.IsFalse(follow.NudgePlayerZoom(-2));
                Assert.AreEqual(Ortho(CameraFollow.HaulWorldHeightMeters),
                    go.GetComponent<Camera>().orthographicSize, 1e-4f);
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
