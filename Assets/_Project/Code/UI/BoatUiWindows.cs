using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.UI
{
    /// <summary>Which boat-UI WINDOW a piece of state or chrome belongs to (the owner's 2026-08-07
    /// windowing ruling). One value per window the player can move, size, collapse or hide:
    /// the helm card (tiller / lever / composed dash — whichever the hull shows, it is ONE window),
    /// each instrument's EXPANDED card, and the HUD band's watch face (hide-only).</summary>
    public enum BoatUiWindowId
    {
        Helm = 0,           // the piloting card (S1/S2a) — dash, lone lever, or tiller
        Sounder = 1,        // the depth sounder's expanded card (S2)
        FishFinder = 2,     // the colour sonar's expanded card (S3b)
        Radar = 3,          // the PPI scope's expanded card (S5)
        Chartplotter = 4,   // the GPS chart's expanded card (S6)
        Watch = 5,          // the band's watch face (#447) — hide-only, never moved or sized
    }

    /// <summary>The three collapse tiers (the owner's "collapsible to certain degrees"):
    /// FULL is the card as dialled; COMPACT is the same card at a reduced glance scale
    /// (<see cref="BoatUiWindowSettings.CompactScale"/>); BAR is the title bar alone — the
    /// instrument's picture is not drawn at all, so a bar-collapsed window costs zero rasters.</summary>
    public enum BoatUiCollapse
    {
        Full = 0,
        Compact = 1,
        Bar = 2,
    }

    /// <summary>What the drag session currently owns: moving the window by its bar, or scaling it
    /// by its corner grip.</summary>
    public enum BoatUiDragKind
    {
        None = 0,
        Move = 1,
        Resize = 2,
    }

    /// <summary>
    /// One window's presentation state. The DEFAULT (all zero) means "exactly where the shipped
    /// layout puts it, at the dialled size, fully shown" — <see cref="BoatUiWindowLayout.WindowRect"/>
    /// returns the base rect BIT-EXACT for it, so windowing changes nothing until the player reaches
    /// for it.
    /// </summary>
    public struct BoatUiWindowState
    {
        /// <summary>True once the player has dragged the window somewhere; false = the shipped anchor.</summary>
        public bool HasCustomCenter;

        /// <summary>Where the window's CENTRE sits, as a 0..1 fraction of the screen — resolution-proof,
        /// so a window parked over the port quarter stays there when the game window is resized.</summary>
        public Vector2 Center01;

        /// <summary>The player's resize multiplier on the window's dialled base size. 0 = unset (1×).
        /// Clamped into <see cref="BoatUiWindowSettings.MinScale"/>..<c>MaxScale</c> on use.</summary>
        public float Scale;

        /// <summary>Which collapse tier the window is in.</summary>
        public BoatUiCollapse Collapse;

        /// <summary>Individually hidden (the bar's × button, or the watch's). Restored by the
        /// hide-all toggle's RESTORE half, which clears every individual hide.</summary>
        public bool Hidden;
    }

    /// <summary>
    /// The boat-UI windows' shared, transient state (the owner's 2026-08-07 windowing ruling): where
    /// each window sits, how big it is, which collapse tier it is in, whether it is hidden — plus the
    /// one global HIDE-ALL flag and the single live drag session.
    ///
    /// <para><b>Why one static registry and not a field per host.</b> The hosts are independent
    /// self-installing singletons that cannot see each other (the
    /// <see cref="HelmInstrumentExpansion"/> reasoning), and hide-all is a fact about the SET of
    /// them. The registry also publishes each window's live CHROME rect, so a press on one window's
    /// title bar is never read by another host as its own click-away — the same
    /// publish-then-consult discipline as <see cref="HelmOverlayHost.TryDashCard"/>.</para>
    ///
    /// <para><b>Transient, never persisted</b> (rule 5, and the S3b Ruling A / S4.5 expansion
    /// precedent): where a player parked a window this session is where their eyes are, not save
    /// data. A reload comes back at the shipped layout. Persisting the layout would be NEW save data
    /// (version bump + lead-architect co-sign) and is deliberately left as a flagged follow-up.</para>
    /// </summary>
    public static class BoatUiWindows
    {
        private const int Count = 6;

        private static readonly BoatUiWindowState[] _states = new BoatUiWindowState[Count];

        // The published chrome strips (title bar + grip bounds), one per window, refreshed each frame
        // a window draws chrome and cleared the moment it does not — so a stale strip can never
        // swallow clicks over a window that is no longer on screen.
        private static readonly Rect[] _chrome = new Rect[Count];
        private static readonly bool[] _chromeLive = new bool[Count];

        // The one live drag session (move or resize). One, because there is one pointer.
        private static int _sessionOwner = -1;
        private static BoatUiDragKind _sessionKind = BoatUiDragKind.None;

        /// <summary>The global hide-all flag (the owner's one-input toggle).</summary>
        public static bool AllHidden { get; private set; }

        /// <summary>This window's state. Default = the shipped layout.</summary>
        public static BoatUiWindowState State(BoatUiWindowId id) => _states[(int)id];

        /// <summary>Replace this window's state (drag/resize/collapse writes land here).</summary>
        public static void SetState(BoatUiWindowId id, in BoatUiWindowState state)
            => _states[(int)id] = state;

        /// <summary>Is this window on screen? False while hide-all is on OR the window is
        /// individually hidden.</summary>
        public static bool IsShown(BoatUiWindowId id)
            => !AllHidden && !_states[(int)id].Hidden;

        /// <summary>
        /// Do the dash's FLUSH brow mounts exist right now? The flush faces are bolted into the dash
        /// picture, so they follow the HELM window: a hidden or bar-collapsed dash draws no brow, and
        /// the instruments stand down with it rather than popping out to standalone cards. An
        /// EXPANDED instrument is its own window and is not gated here.
        /// </summary>
        public static bool FlushMountsAvailable
            => IsShown(BoatUiWindowId.Helm)
               && _states[(int)BoatUiWindowId.Helm].Collapse != BoatUiCollapse.Bar;

        /// <summary>Individually hide (or show) one window — the bar's × button.</summary>
        public static void SetHidden(BoatUiWindowId id, bool hidden)
            => _states[(int)id].Hidden = hidden;

        /// <summary>
        /// The hide-all toggle (one input, every window at once). The RESTORE half also clears every
        /// INDIVIDUAL hide — deliberately: it is the one guaranteed recovery path back to a window
        /// (the watch included) whose own chrome is no longer on screen to be clicked.
        /// </summary>
        public static void ToggleHideAll()
        {
            if (!AllHidden)
            {
                AllHidden = true;
                return;
            }
            AllHidden = false;
            for (int i = 0; i < Count; i++) _states[i].Hidden = false;
        }

        /// <summary>The collapse cycle (pure, EditMode-pinned): Full → Compact → Bar → Full.</summary>
        public static BoatUiCollapse NextCollapse(BoatUiCollapse c) => c switch
        {
            BoatUiCollapse.Full => BoatUiCollapse.Compact,
            BoatUiCollapse.Compact => BoatUiCollapse.Bar,
            _ => BoatUiCollapse.Full,
        };

        // ---- the chrome publication (press routing between hosts) ------------------------------

        /// <summary>Publish this window's live chrome bounds (the strip a press belongs to this
        /// window's chrome, not to whatever card sits under or beside it).</summary>
        public static void PublishChrome(BoatUiWindowId id, Rect bounds)
        {
            _chrome[(int)id] = bounds;
            _chromeLive[(int)id] = bounds.width > 0f && bounds.height > 0f;
        }

        /// <summary>Withdraw the publication (window hidden / host stood down).</summary>
        public static void ClearChrome(BoatUiWindowId id) => _chromeLive[(int)id] = false;

        /// <summary>Is this screen point over ANY window's published chrome? Hosts consult this
        /// before treating a press as click-away/focus, so grabbing one window's bar never collapses
        /// another window's expansion.</summary>
        public static bool OverAnyChrome(Vector2 screenPos)
        {
            for (int i = 0; i < Count; i++)
                if (_chromeLive[i] && _chrome[i].Contains(screenPos))
                    return true;
            return false;
        }

        // ---- the one drag session ---------------------------------------------------------------

        /// <summary>True while any window drag/resize is live — every host's cue to leave the
        /// pointer alone (the <see cref="HelmInstrumentExpansion.AnyExpanded"/> shape).</summary>
        public static bool AnySession => _sessionOwner >= 0;

        /// <summary>The drag this window owns right now (<see cref="BoatUiDragKind.None"/> if it
        /// owns nothing).</summary>
        public static BoatUiDragKind SessionKind(BoatUiWindowId id)
            => _sessionOwner == (int)id ? _sessionKind : BoatUiDragKind.None;

        /// <summary>Claim the pointer for a move/resize. First claim wins; a second window cannot
        /// steal a live session.</summary>
        public static bool BeginSession(BoatUiWindowId id, BoatUiDragKind kind)
        {
            if (_sessionOwner >= 0 || kind == BoatUiDragKind.None) return false;
            _sessionOwner = (int)id;
            _sessionKind = kind;
            return true;
        }

        /// <summary>Release the pointer (button up, window hidden mid-drag, host stood down).</summary>
        public static void EndSession(BoatUiWindowId id)
        {
            if (_sessionOwner != (int)id) return;
            _sessionOwner = -1;
            _sessionKind = BoatUiDragKind.None;
        }

        /// <summary>Back to the shipped layout — every state, the hide-all flag, the session and the
        /// chrome publications (scene teardown / tests).</summary>
        public static void Reset()
        {
            for (int i = 0; i < Count; i++)
            {
                _states[i] = default;
                _chromeLive[i] = false;
            }
            AllHidden = false;
            _sessionOwner = -1;
            _sessionKind = BoatUiDragKind.None;
        }

        // Statics survive a play session when the editor's domain reload is disabled (the
        // HelmKeyCapture precedent) — re-seed before any scene loads, or a session that ended with
        // everything hidden would start the next one blind.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Reset();
    }
}
