using UnityEngine;

namespace HiddenHarbours.UI
{
    /// <summary>Which boat-UI WINDOW a piece of chrome belongs to (the owner's 2026-08-07 windowing
    /// ruling). Since H2 the one window left is the HUD band's watch face (#447) — hide-only, never
    /// moved or sized. The helm is its band (or its small card) and an instrument is raised over it;
    /// neither carries chrome of its own.</summary>
    public enum BoatUiWindowId
    {
        Watch = 0,          // the band's watch face (#447) — hide-only, never moved or sized
    }

    /// <summary>
    /// The boat-UI windows' shared, transient state (the owner's 2026-08-07 windowing ruling, as H2
    /// left it): the one global HIDE-ALL flag, each window's individual hide, and each window's
    /// published CHROME rect.
    ///
    /// <para><b>What H2 retired.</b> The helm card and each instrument's expanded card were windows
    /// the player could move, size and collapse. The band replaced the dash card, and every raised
    /// card keeps the one shared placement (<see cref="HelmBandLayout.RaisedCardRect"/>), so the
    /// per-window layout, the collapse tiers and the drag session went with them. Hide-all stays: it
    /// is the owner's one-input toggle, and every helm host still stands down under it.</para>
    ///
    /// <para><b>Why one static registry and not a field per host.</b> The hosts are independent
    /// self-installing singletons that cannot see each other (the
    /// <see cref="HelmInstrumentExpansion"/> reasoning), and hide-all is a fact about the SET of
    /// them. The registry also publishes each window's live CHROME rect, so a press on the watch's ×
    /// is never read by a helm host as its own click-away.</para>
    ///
    /// <para><b>Transient, never persisted</b> (rule 5, and the S3b Ruling A / S4.5 expansion
    /// precedent): what a player hid this session is where their eyes are, not save data. A reload
    /// comes back with everything shown.</para>
    /// </summary>
    public static class BoatUiWindows
    {
        private const int Count = 1;

        private static readonly bool[] _hidden = new bool[Count];

        // The published chrome strips, one per window, refreshed each frame a window draws chrome and
        // cleared the moment it does not — so a stale strip can never swallow clicks over a window
        // that is no longer on screen.
        private static readonly Rect[] _chrome = new Rect[Count];
        private static readonly bool[] _chromeLive = new bool[Count];

        /// <summary>The global hide-all flag (the owner's one-input toggle).</summary>
        public static bool AllHidden { get; private set; }

        /// <summary>Is this window on screen? False while hide-all is on OR the window is
        /// individually hidden.</summary>
        public static bool IsShown(BoatUiWindowId id)
            => !AllHidden && !_hidden[(int)id];

        /// <summary>Individually hide (or show) one window — the watch's × button.</summary>
        public static void SetHidden(BoatUiWindowId id, bool hidden)
            => _hidden[(int)id] = hidden;

        /// <summary>
        /// The hide-all toggle (one input, every window at once). The RESTORE half also clears every
        /// INDIVIDUAL hide — deliberately: it is the one guaranteed recovery path back to a window
        /// (the watch) whose own chrome is no longer on screen to be clicked.
        /// </summary>
        public static void ToggleHideAll()
        {
            if (!AllHidden)
            {
                AllHidden = true;
                return;
            }
            AllHidden = false;
            for (int i = 0; i < Count; i++) _hidden[i] = false;
        }

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
        /// before treating a press as a click-away or a control, so pressing the watch's × never
        /// lowers a raised card or works the helm.</summary>
        public static bool OverAnyChrome(Vector2 screenPos)
        {
            for (int i = 0; i < Count; i++)
                if (_chromeLive[i] && _chrome[i].Contains(screenPos))
                    return true;
            return false;
        }

        /// <summary>Back to everything shown — every hide, the hide-all flag and the chrome
        /// publications (scene teardown / tests).</summary>
        public static void Reset()
        {
            for (int i = 0; i < Count; i++)
            {
                _hidden[i] = false;
                _chromeLive[i] = false;
            }
            AllHidden = false;
        }

        // Statics survive a play session when the editor's domain reload is disabled (the
        // HelmKeyCapture precedent) — re-seed before any scene loads, or a session that ended with
        // everything hidden would start the next one blind.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Reset();
    }
}
