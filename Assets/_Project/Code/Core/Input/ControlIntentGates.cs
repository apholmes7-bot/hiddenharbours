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
        public static bool WorldStopped => ShellFlow.WorldInputBlocked;

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
    }
}
