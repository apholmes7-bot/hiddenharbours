using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Audio
{
    /// <summary>One-shot cues fired by discrete beats of the rod-fishing interaction (design
    /// rod-fishing-v2-brainstorm.md §2–3). These are DIEGETIC — they live in the rod/line/water,
    /// not the UI (the design's "the rod is the instrument, no HUD" promise).</summary>
    public enum FishingCue
    {
        None = 0,
        CastWhoosh,     // the flick released — rod whip + line whistle (§2.2)
        SplashDown,     // the rig hits the water on the transition OUT of Cast (art has SplashBurst; this is its sound)
        BobberPlop,     // the CAST-path bite tell — the bobber dips (§2.1, surface/visual+plop)
        RodKnock,       // the DEPTH-path bite tell — the deep rod-tip knock (§2.1: audio/feel, no bobber)
        BottomSettle,   // the depth drop's "you felt bottom" — the line settles slack on the floor (§2.3)
        SlackRelease,   // the mid-fight slack window OPENS — the diegetic "PULL now" tell (§3)
        SnapSting,      // she threw the hook — a cozy sting, never a punishment sound (§7)
        LandedFlourish, // she's aboard — a warm little flourish + the wet slap on the boards
    }

    /// <summary>
    /// The continuous layer gains/pitches of the fight soundscape for one published
    /// <see cref="FishingState"/>. A plain value bag so <see cref="FishingAudioLogic.MixFor"/> is a
    /// pure, EditMode-testable function; <see cref="FishingAudio"/> smooths toward it and sets the
    /// AudioSources. Gains are 0..1 relative to their layer's serialized level; pitches are
    /// AudioSource pitch multipliers (1 = authored speed).
    /// </summary>
    public struct FishingAudioMix
    {
        public float CreakGain;    // the wind-back rod creak (WindBack only)
        public float PayoutGain;   // the sinking reel pay-out tick (Sinking only)
        public float PayoutPitch;  // pay-out tick speed — slows as the rig nears bottom (§2.3's "still working" cue)
        public float StrainGain;   // the line-strain groan — the continuous "ease off!" voice (any fight phase)
        public float StrainPitch;  // the groan tightens (lifts) slightly as tension climbs
        public float ReelGain;     // reel clicks while the player is GAINING (Landing01 rising)
        public float ThrashGain;   // the surface thrash/splash churn (FightSurface only)
        public float ThrashPan;    // -1..1 stereo pan of the thrash, following the fish's X offset
    }

    /// <summary>
    /// The PURE decision logic for the rod-fight sound layer (Rod Fishing v2, design §2–3, §7).
    /// Consumes ONLY the Core <see cref="FishingState"/> snapshot (rule 4: no Fishing-module
    /// reference) and returns cues + a continuous mix — no AudioSources, no events, no clock — so
    /// every mapping is EditMode-testable. <see cref="FishingAudio"/> is the thin player.
    ///
    /// <para>Design intent (§7 cozy-with-teeth): the everyday layer stays warm and readable — the
    /// strain groan is the "ease off!" voice, the slack release is the "PULL now" voice, and the
    /// snap is a cozy sting, not a punishment. The legacy <see cref="FishingPhase.Fighting"/> is
    /// serviced by the SAME tension/reel layers via <see cref="FishingState.IsFightPhase"/> — no
    /// special casing.</para>
    /// </summary>
    public static class FishingAudioLogic
    {
        // ---- one-shot cues ------------------------------------------------------------------

        /// <summary>
        /// The cue a PHASE TRANSITION fires (prev phase → the new snapshot). Same-phase ticks are
        /// silent (the continuous mix carries them). The bite tell branches on the tackle path the
        /// way §2.1 draws it: a bite with the rig down the column (<c>Depth01 &gt; 0</c>) is the
        /// deep rod-tip KNOCK; a surface bite is the bobber PLOP. Leaving <see cref="FishingPhase.Cast"/>
        /// is the splash-down (the line landed), unless the new phase carries its own beat.
        /// </summary>
        public static FishingCue PhaseCue(FishingPhase prevPhase, in FishingState next)
        {
            if (next.Phase == prevPhase) return FishingCue.None;
            switch (next.Phase)
            {
                case FishingPhase.Cast:    return FishingCue.CastWhoosh;
                case FishingPhase.Bite:    return next.Depth01 > 0f ? FishingCue.RodKnock : FishingCue.BobberPlop;
                case FishingPhase.Snapped: return FishingCue.SnapSting;
                case FishingPhase.Landed:  return FishingCue.LandedFlourish;
                default:
                    // The line only splashes down when it was flying: any exit from Cast that isn't
                    // one of the beats above (normally Cast → Waiting or Cast → Sinking).
                    return prevPhase == FishingPhase.Cast ? FishingCue.SplashDown : FishingCue.None;
            }
        }

        /// <summary>
        /// The cue a SLACK-WINDOW rising edge fires (false → true between consecutive snapshots).
        /// Pre-bite (<see cref="FishingPhase.Sinking"/>/<see cref="FishingPhase.Waiting"/>) the flag
        /// carries the depth drop's BOTTOM tell (§2.3) → the soft "line settles on the floor" note.
        /// In any fight phase it is the "PULL now" moment (§3) → the distinct release cue. No edge,
        /// or a closing edge → silence (the window closing is read by the strain layer instead).
        /// </summary>
        public static FishingCue SlackCue(in FishingState prev, in FishingState next)
        {
            if (!next.SlackWindowOpen || prev.SlackWindowOpen) return FishingCue.None;
            if (next.IsFightPhase) return FishingCue.SlackRelease;
            if (next.Phase == FishingPhase.Sinking || next.Phase == FishingPhase.Waiting)
                return FishingCue.BottomSettle;
            return FishingCue.None;
        }

        // ---- continuous mix -----------------------------------------------------------------

        /// <summary>The wind-back creak is audible from the first degree of draw (a floor gain), and
        /// deepens as the rod loads (<see cref="FishingState.RodBend01"/>).</summary>
        public const float CreakFloorGain = 0.35f;

        /// <summary>Pay-out tick pitch when the rig reaches the floor of the reachable band
        /// (<c>Depth01 = 1</c>) — the tick slows to less than half speed so "the drop is ending" is
        /// audible without a depth gauge (§2.3: fall-time + the slack tell are the whole read).</summary>
        public const float PayoutPitchFloor = 0.45f;

        /// <summary>Shape of the strain groan's rise: gain = Tension01^exponent. &gt;1 keeps the
        /// groan a whisper through the safe band and lets it swell URGENTLY toward the snap — the
        /// "ease off!" voice warns hardest exactly where it matters (§3, §7).
        /// <para>Eased 1.6 → 1.25 when the fight's HUD bars were deleted (owner's ruling 2026-07-23):
        /// sound is now one of only three things telling the player how loaded the line is, so the
        /// groan has to be audible through the middle of the fight, not just at the cliff edge. Still
        /// above 1, so it stays quieter than linear in the safe band.</para></summary>
        public const float StrainExponent = 1.25f;

        /// <summary>How far the groan's pitch lifts (tightens) at full tension — the line literally
        /// sounds tighter as it nears parting. Doubled with the bars' removal: pitch is the part of the
        /// strain voice you can read WITHOUT looking away from the water.</summary>
        public const float StrainPitchLift = 0.3f;

        /// <summary>The Landing01 gain rate (per second) that reads as "reeling flat out" — the reel
        /// click layer is at full voice here. Typical v2 fights gain the full bar over tens of
        /// seconds, so a burst of ~0.2/s is a strong, safe pull.</summary>
        public const float ReelFullLandingPerSec = 0.2f;

        /// <summary>The fish dart speed (world m/s of the line's far end) that reads as a full-power
        /// surface thrash.</summary>
        public const float DartFullSpeedMs = 3f;

        // The surface thrash churns at a floor while she's up at all, and swells with how hard she
        // is darting (offset motion) and how loaded the rod is (RodBend01) — §3's visible fight.
        public const float ThrashFloorGain = 0.25f;
        public const float ThrashBendWeight = 0.45f;
        public const float ThrashDartWeight = 0.5f;

        /// <summary>World metres of fish X offset that pans the thrash fully to one ear.</summary>
        public const float ThrashPanRangeMetres = 6f;

        /// <summary>How fast the played layers chase the target mix (gain/pitch units per second).
        /// High enough to track a dart, low enough that the groan is CONTINUOUS, never stepped.</summary>
        public const float MixSmoothingPerSec = 6f;

        /// <summary>
        /// The continuous layer mix for one snapshot. <paramref name="landingPerSec"/> is the rate
        /// Landing01 is rising (see <see cref="LandingPerSec"/>) and <paramref name="dartSpeed01"/>
        /// the normalized fish dart speed (see <see cref="DartSpeed01"/>) — both derived by the
        /// caller from consecutive snapshots so this stays pure. Legacy
        /// <see cref="FishingPhase.Fighting"/> gets the same strain/reel voice through
        /// <see cref="FishingState.IsFightPhase"/>.
        /// </summary>
        public static FishingAudioMix MixFor(in FishingState s, float landingPerSec, float dartSpeed01)
        {
            var m = new FishingAudioMix
            {
                // Pitches are always well-defined (their layers just sit at zero gain when idle),
                // so the player never has to smooth a pitch up from an uninitialised zero.
                PayoutPitch = Mathf.Lerp(1f, PayoutPitchFloor, Mathf.Clamp01(s.Depth01)),
                StrainPitch = 1f + StrainPitchLift * Mathf.Clamp01(s.Tension01),
            };

            if (s.Phase == FishingPhase.WindBack)
                m.CreakGain = Mathf.Lerp(CreakFloorGain, 1f, Mathf.Clamp01(s.RodBend01));

            if (s.Phase == FishingPhase.Sinking)
                m.PayoutGain = 1f;

            if (s.IsFightPhase)
            {
                m.StrainGain = Mathf.Pow(Mathf.Clamp01(s.Tension01), StrainExponent);
                m.ReelGain   = Mathf.Clamp01(landingPerSec / ReelFullLandingPerSec);
            }

            if (s.Phase == FishingPhase.FightSurface)
            {
                m.ThrashGain = Mathf.Clamp01(ThrashFloorGain
                                             + ThrashBendWeight * Mathf.Clamp01(s.RodBend01)
                                             + ThrashDartWeight * Mathf.Clamp01(dartSpeed01));
                m.ThrashPan = Mathf.Clamp(s.FishOffsetX / ThrashPanRangeMetres, -1f, 1f);
            }

            return m;
        }

        // ---- derivatives from consecutive snapshots (pure, guard-railed) --------------------

        /// <summary>Rate Landing01 is RISING (per second) between two snapshots — 0 when flat,
        /// falling, or the elapsed time is degenerate. Losing ground is voiced by the strain layer,
        /// not by reel clicks (you only hear the reel when you are actually gaining).</summary>
        public static float LandingPerSec(float prevLanding01, float nextLanding01, float dtSeconds)
        {
            if (dtSeconds <= 0f) return 0f;
            return Mathf.Max(0f, (nextLanding01 - prevLanding01) / dtSeconds);
        }

        /// <summary>Normalized 0..1 dart speed of the line's far end from the offset delta between
        /// two snapshots. 0 for a degenerate dt (a phase edge after an idle gap must not read as an
        /// infinite dart).</summary>
        public static float DartSpeed01(float deltaX, float deltaY, float dtSeconds)
        {
            if (dtSeconds <= 0f) return 0f;
            float speed = Mathf.Sqrt(deltaX * deltaX + deltaY * deltaY) / dtSeconds;
            return Mathf.Clamp01(speed / DartFullSpeedMs);
        }
    }
}
