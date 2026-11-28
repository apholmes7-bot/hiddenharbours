using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// ⭐ <b>The two gates every device-backed intent source applies, in ONE place</b> (ADR 0043 §"the
    /// gates"): the shell holding the world (<see cref="ShellFlow.WorldInputBlocked"/> — the title page,
    /// the pause menu) and a UI owning the move axis (<see cref="MoveActionClaim"/> — a picker steering on
    /// the walk keys).
    ///
    /// <para><b>Why here and not in each consumer.</b> Before the seam, the walk controller honoured the
    /// move claim and nothing else did; the deck walk and the arrival's cabin walk read the keys straight,
    /// so opening the notebook on deck both scrolled the page and walked the fisher, and the pause menu
    /// held the world but not a deck she was standing on. Gating inside the source means a mode cannot
    /// honour the claim in one reader and miss it in another — the flag has exactly the readers the seam
    /// has, and a new mode inherits both gates by construction.</para>
    ///
    /// <para><b>What each gate takes.</b> A stopped world takes EVERYTHING — no move, no press: the
    /// controls are parked, not merely deaf (the switcher's own words for it). A claimed move axis takes
    /// the MOVE ONLY: the picker that raised it steers on the axis and confirms on Interact, so the press
    /// must still arrive (<see cref="MoveActionClaim"/>'s own contract).</para>
    ///
    /// <para>Pure overloads first, so the truth table is a test and not a thing read off a screen; the
    /// parameterless overloads read the live gates and are what a source calls.</para>
    /// </summary>
    public static class ControlIntentGates
    {
        /// <summary>Is the shell holding the world? Everything reads as <c>None</c> while it is.</summary>
        /// <remarks>Lying down to sleep holds it too (owner ruling 2026-11-15): from the chooser to the
        /// wake, the fisher is in bed and the keys are the chooser's (<see cref="SleepFlow.HoldsTheWorld"/>).</remarks>
        public static bool WorldStopped => ShellFlow.WorldInputBlocked || SleepFlow.HoldsTheWorld;

        /// <summary>Does a UI own the move axis this frame? The move reads as zero while it does.</summary>
        public static bool MoveClaimed => MoveActionClaim.IsClaimed;

        public static WalkIntents Apply(in WalkIntents raw, bool worldStopped, bool moveClaimed)
        {
            if (worldStopped) return WalkIntents.None;
            if (moveClaimed) return new WalkIntents(Vector2.zero, raw.Sprint, raw.Interact, raw.Cancel);
            return raw;
        }

        public static DeckIntents Apply(in DeckIntents raw, bool worldStopped, bool moveClaimed)
        {
            if (worldStopped) return DeckIntents.None;
            if (moveClaimed) return new DeckIntents(Vector2.zero, raw.Interact);
            return raw;
        }

        public static WalkIntents Apply(in WalkIntents raw) => Apply(raw, WorldStopped, MoveClaimed);

        public static DeckIntents Apply(in DeckIntents raw) => Apply(raw, WorldStopped, MoveClaimed);

        /// <summary>
        /// The HUD's gates (H2). A stopped world takes the press (the pause menu is not the place to
        /// relayout the helm), and so does a text field that owns the keys (<see cref="HelmKeyCapture"/>:
        /// an "M" belongs in a waypoint name). The move claim does not apply: the HUD has no move.
        /// </summary>
        public static HudIntents Apply(in HudIntents raw, bool worldStopped, bool keysCaptured)
            => worldStopped || keysCaptured ? HudIntents.None : raw;

        public static HudIntents Apply(in HudIntents raw) => Apply(raw, WorldStopped, HelmKeyCapture.IsCapturing);

        /// <summary>
        /// The interaction chooser's gates (2026-11-15). A stopped world takes everything (a list left up
        /// under the pause menu waits, it is not driven through it), and so does a text field that owns
        /// the keys. The move claim does NOT apply: the chooser is the claimant, and steering on the axis
        /// is exactly what it raised the claim for.
        /// </summary>
        public static ChooserIntents Apply(in ChooserIntents raw, bool worldStopped, bool keysCaptured)
            => worldStopped || keysCaptured ? ChooserIntents.None : raw;

        public static ChooserIntents Apply(in ChooserIntents raw)
            => Apply(raw, WorldStopped, HelmKeyCapture.IsCapturing);

        /// <summary>
        /// The autopilot switch's gates (the HUD's two). A stopped world takes the press (the pause menu
        /// does not flick helm switches), and so does a text field that owns the keys: a "P" typed in a
        /// waypoint name is a letter, not the autopilot.
        /// </summary>
        public static AutopilotIntents Apply(in AutopilotIntents raw, bool worldStopped, bool keysCaptured)
            => worldStopped || keysCaptured ? AutopilotIntents.None : raw;

        public static AutopilotIntents Apply(in AutopilotIntents raw)
            => Apply(raw, WorldStopped, HelmKeyCapture.IsCapturing);

        /// <summary>
        /// The camera's gates (the HUD's two). A stopped world takes everything (no picture of the pause
        /// menu), and so does a text field that owns the keys: a "P" typed in a name is a letter. The move
        /// claim does not apply: the camera is the claimant while it is up, and aiming on the axis is what
        /// it raised the claim for.
        /// </summary>
        public static CameraIntents Apply(in CameraIntents raw, bool worldStopped, bool keysCaptured)
            => worldStopped || keysCaptured ? CameraIntents.None : raw;

        public static CameraIntents Apply(in CameraIntents raw)
            => Apply(raw, WorldStopped, HelmKeyCapture.IsCapturing);
    }
}
