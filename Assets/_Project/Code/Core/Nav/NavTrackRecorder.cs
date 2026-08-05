using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// Drives the track breadcrumb: gathers where the boat is from the seams that already publish it and
    /// hands the decision to <see cref="NavLocker.RecordTrack"/>.
    ///
    /// <para><b>The track is the PLAYER's, not the plotter's.</b> Nothing here asks whether a
    /// chartplotter is fitted, owned, drawn, or even implemented — if the glass were deleted tomorrow
    /// the breadcrumb would still accrue, because "where I have been" is a fact about the voyage. That
    /// is the honesty invariant this arc has applied to every instrument: the sounder reads a depth the
    /// hull already obeys, the finder draws schools that fish whether or not you can see them, and the
    /// plotter draws a track that was recorded whether or not you own one.</para>
    ///
    /// <para><b>In memory, not to disk.</b> A crumb every few metres is a save-file write every few
    /// seconds. The rule mutates the live <see cref="SaveData"/> only; the track reaches disk on the
    /// game's normal save cadence, like every other piece of accrued state. Deliberately NOT the
    /// <c>Save()</c>-on-write pattern that a deliberate act (fitting an instrument, moving a
    /// preference) uses — those are rare and worth an I/O; this is continuous.</para>
    ///
    /// <para><b>Where the driver lives.</b> This is a static tick, so the MonoBehaviour that calls it is
    /// a one-line host and can sit in any lane. FLAG lead-architect: it is driven from the UI lane today
    /// only because that was the slice's fence — the rule and its inputs are pure Core, so moving the
    /// call into an App/sim-lane tick is a file move and changes nothing here.</para>
    /// </summary>
    public static class NavTrackRecorder
    {
        // Transient early-out so the common frame costs a distance check instead of a walk back through
        // the ring. NEVER authoritative: NavLocker.RecordTrack re-checks against the save itself, so a
        // stale cache (a reloaded save, a travel) can only cost one redundant call, never a wrong crumb.
        private static bool _hasLast;
        private static string _lastRegion;
        private static Vector2 _lastPos;

        /// <summary>Forget the cached last crumb (a save load, a region change, a test). The next tick
        /// then asks the save rather than trusting a position from another voyage.</summary>
        public static void Reset()
        {
            _hasLast = false;
            _lastRegion = null;
            _lastPos = default;
        }

        /// <summary>
        /// Record a crumb if the boat has moved far enough. Safe to call every frame; safe to call with
        /// nothing wired (no save, no region, on foot) — it simply does nothing. Returns true iff a
        /// crumb was written, which is the seam the tests assert on.
        /// </summary>
        public static bool Tick()
        {
            ISaveService saveService = GameServices.Save;
            SaveData save = saveService?.Current;
            if (save == null) return false;

            string regionId = GameServices.CurrentRegionId;
            if (string.IsNullOrEmpty(regionId)) return false;

            IHelmInstruments instruments = GameServices.HelmInstruments;
            if (instruments == null || !instruments.TryReadPosition(out Vector2 pos)) return false;
            if (!NavMath.IsFinite(pos)) return false;

            ChartplotterSettings cfg = GameServices.Chartplotter;
            float spacing = cfg.TrackMinSpacingMetres > 0f
                ? cfg.TrackMinSpacingMetres
                : ChartplotterSettings.Default.TrackMinSpacingMetres;

            if (_hasLast && _lastRegion == regionId && (pos - _lastPos).sqrMagnitude < spacing * spacing)
                return false;

            if (!NavLocker.RecordTrack(save, regionId, pos, in cfg)) return false;

            _hasLast = true;
            _lastRegion = regionId;
            _lastPos = pos;
            return true;
        }
    }
}
