using HiddenHarbours.Core;

namespace HiddenHarbours.App
{
    /// <summary>
    /// Which of the follow-cam's discrete, pixel-perfect framings should be on screen. The camera never
    /// zooms to an arbitrary orthographic size — each framing maps to a PPU-integer step (the ratified
    /// per-context discrete-zoom vision), so the picture stays crisp at every stop.
    /// </summary>
    public enum CameraFraming
    {
        /// <summary>At the helm — the active hull's data-driven framing
        /// (<c>BoatHullDef.CameraWorldHeightMeters</c>; bigger boat = more water).</summary>
        Boat,

        /// <summary>Walking the coast — the tighter on-foot framing (the fisher reads large).</summary>
        OnFoot,

        /// <summary>Standing ON DECK (boarded, not at the helm) — a step closer again so the boat fills
        /// the screen and deck work (pots, bait, the rail) reads in detail. Owner playtest 2026-07-08.</summary>
        Deck,

        /// <summary>On deck with a trap haul LIVE — one more step so the rope-and-buoy action is the
        /// star. Released the moment the pot surfaces or the haul goes idle. Optional (owner-tunable).</summary>
        DeckHaul,
    }

    /// <summary>
    /// The zoom brain of <see cref="CameraFollow"/> as a plain engine-light POCO (CLAUDE.md §5): it maps
    /// (control mode, live-haul flag) to the <see cref="CameraFraming"/> that should be on screen, and
    /// owns the commit HOLD (hysteresis) so rapid helm⇄deck hops collapse into a single re-zoom instead
    /// of thrashing the discrete pixel-perfect steps. Deterministic — a pure function of the inputs and
    /// the time it is fed (no hidden clock, no randomness), so EditMode tests drive it with plain numbers.
    /// </summary>
    public sealed class CameraZoomPolicy
    {
        private bool _hasCommitted;
        private CameraFraming _committed;
        private double _lastCommitTime;

        /// <summary>False until the first <see cref="TryCommit"/> succeeds.</summary>
        public bool HasCommitted => _hasCommitted;

        /// <summary>The framing last committed (undefined until <see cref="HasCommitted"/>).</summary>
        public CameraFraming Committed => _committed;

        /// <summary>
        /// The framing the current control state WANTS. Pure mapping: the helm gets the boat's framing,
        /// on foot gets the on-foot framing, the deck gets the closer deck step — tightened one more step
        /// while a trap haul is live (if <paramref name="haulTightensZoom"/>; the haul is deck work, so
        /// the flag can never tighten any other mode).
        /// </summary>
        public static CameraFraming DesiredFraming(ControlMode mode, bool haulLive, bool haulTightensZoom)
        {
            if (mode == ControlMode.Aboard) return CameraFraming.Boat;
            if (mode == ControlMode.OnDeck)
                return (haulLive && haulTightensZoom) ? CameraFraming.DeckHaul : CameraFraming.Deck;
            return CameraFraming.OnFoot;
        }

        /// <summary>
        /// Feed the desired framing every tick; returns true exactly when the camera should re-frame NOW.
        /// The first-ever desire commits immediately (a single clean switch feels instant). A change that
        /// lands within <paramref name="minHoldSeconds"/> of the previous commit is HELD: keep feeding it
        /// and it commits the moment the hold expires — unless the desire meanwhile returns to the
        /// committed framing, in which case the hop dissolves and the camera never moved (a rapid
        /// helm⇄deck there-and-back re-zooms ZERO times).
        /// </summary>
        public bool TryCommit(CameraFraming desired, double nowSeconds, double minHoldSeconds)
        {
            if (_hasCommitted && desired == _committed) return false;
            if (_hasCommitted && nowSeconds - _lastCommitTime < minHoldSeconds) return false;
            _committed = desired;
            _hasCommitted = true;
            _lastCommitTime = nowSeconds;
            return true;
        }
    }
}
