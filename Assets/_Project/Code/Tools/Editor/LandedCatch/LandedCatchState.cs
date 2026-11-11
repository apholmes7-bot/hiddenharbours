using System;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tools.LandedCatch
{
    public enum CatchLifeState { Alive, Dead }

    /// <summary>Immutable, editor-only landed-catch experiment. Life and freshness are independent.
    /// Time is absolute game seconds supplied by the caller; no clock, random source, rendering,
    /// save format, container resource allocator or gameplay registration lives here.</summary>
    public sealed class LandedCatchState
    {
        private readonly FreshnessState _freshnessAnchor;
        private readonly double _exposureAtAnchor;
        private readonly double _anchorAt;
        private readonly double? _iceEndsAtAnchor;
        public LandedCatchProfile Profile { get; }
        public LandedCatchSettings Settings { get; }
        public CatchLifeState Life { get; }
        public bool IsAlive => Life == CatchLifeState.Alive;
        /// <summary>Dead/still handling fact, not an animation or a claim about movement at rest.</summary>
        public bool IsStill => Life == CatchLifeState.Dead;
        public FreshnessState FreshnessState { get; }
        public double EvaluatedAtGameSeconds { get; }
        public double AmbientLiveSecondsAccrued { get; }
        public double? IceEndsAtGameSeconds { get; }
        public float Quality => Freshness.Remaining(FreshnessState.SpoilAccrued);
        public bool IsSellable => Freshness.IsSellable(FreshnessState.SpoilAccrued, Settings.SpoilPolicy);

        private LandedCatchState(LandedCatchProfile profile, LandedCatchSettings settings,
            CatchLifeState life, in FreshnessState freshness, double evaluatedAt,
            double ambientLiveSecondsAccrued, double? iceEndsAt, LandedCatchState readAnchor = null)
        {
            Profile = profile;
            Settings = settings;
            Life = life;
            FreshnessState = freshness;
            EvaluatedAtGameSeconds = evaluatedAt;
            AmbientLiveSecondsAccrued = ambientLiveSecondsAccrued;
            IceEndsAtGameSeconds = iceEndsAt;
            // Reads retain the last transition's anchors. Polling frequency must not compound
            // floating-point spoil rounding or change the life deadline.
            _freshnessAnchor = readAnchor == null ? freshness : readAnchor._freshnessAnchor;
            _exposureAtAnchor = readAnchor == null ? ambientLiveSecondsAccrued : readAnchor._exposureAtAnchor;
            _anchorAt = readAnchor == null ? evaluatedAt : readAnchor._anchorAt;
            _iceEndsAtAnchor = readAnchor == null ? iceEndsAt : readAnchor._iceEndsAtAnchor;
        }

        public static LandedCatchState Land(LandedCatchProfile profile, LandedCatchSettings settings,
            double nowGameSeconds, StorageMode mode, double? iceEndsAtGameSeconds = null)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            LandedCatchInputs.Nonnegative(nowGameSeconds, nameof(nowGameSeconds));
            var life = profile.LandsAlive ? CatchLifeState.Alive : CatchLifeState.Dead;
            ValidateStorage(profile, life, nowGameSeconds, mode, iceEndsAtGameSeconds);
            if (mode == StorageMode.Frozen) life = CatchLifeState.Dead;
            return new LandedCatchState(profile, settings, life, Freshness.Landed(nowGameSeconds, mode),
                nowGameSeconds, 0d, iceEndsAtGameSeconds).Advance(nowGameSeconds);
        }

        public LandedCatchState Advance(double nowGameSeconds)
        {
            LandedCatchInputs.Nonnegative(nowGameSeconds, nameof(nowGameSeconds));
            if (nowGameSeconds < EvaluatedAtGameSeconds)
                throw new ArgumentOutOfRangeException(nameof(nowGameSeconds), "Cannot rewind a settled catch.");

            var life = Life;
            double exposed = AmbientLiveSecondsAccrued;
            if (IsAlive && _freshnessAnchor.Mode != StorageMode.Live)
            {
                // Ice is preservation, not a species-compatible live habitat. Wet storage pauses
                // this accumulated exposure; returning to it never resets the previous exposure.
                exposed = _exposureAtAnchor + (nowGameSeconds - _anchorAt);
                if (exposed >= Profile.AmbientLiveSeconds.Value)
                {
                    exposed = Profile.AmbientLiveSeconds.Value;
                    life = CatchLifeState.Dead;
                }
            }

            FreshnessState freshness;
            double? iceEnds = _iceEndsAtAnchor;
            if (_freshnessAnchor.Mode == StorageMode.Iced)
            {
                freshness = Freshness.SettleThroughProtection(_freshnessAnchor, nowGameSeconds,
                    iceEnds.Value, Profile.SpoilPerDay, Settings.SecondsPerDay, Settings.SpoilPolicy);
                if (nowGameSeconds >= iceEnds.Value)
                {
                    freshness = Freshness.WithMode(freshness, nowGameSeconds, Profile.SpoilPerDay,
                        Settings.SecondsPerDay, Settings.SpoilPolicy, StorageMode.Ambient);
                    iceEnds = null;
                }
            }
            else
            {
                freshness = Freshness.Settle(_freshnessAnchor, nowGameSeconds,
                    Profile.SpoilPerDay, Settings.SecondsPerDay, Settings.SpoilPolicy);
            }

            return new LandedCatchState(Profile, Settings, life, freshness, nowGameSeconds, exposed, iceEnds, this);
        }

        /// <summary>Caller supplies the container's validated absolute ice depletion instant.
        /// This experiment does not create ice or authorize a physical transfer.</summary>
        public LandedCatchState WithStorage(double nowGameSeconds, StorageMode mode,
            double? iceEndsAtGameSeconds = null)
        {
            LandedCatchState settled = Advance(nowGameSeconds);
            ValidateStorage(Profile, settled.Life, nowGameSeconds, mode, iceEndsAtGameSeconds);
            var life = mode == StorageMode.Frozen ? CatchLifeState.Dead : settled.Life;
            FreshnessState freshness = Freshness.WithMode(settled.FreshnessState, nowGameSeconds,
                Profile.SpoilPerDay, Settings.SecondsPerDay, Settings.SpoilPolicy, mode);
            return new LandedCatchState(Profile, Settings, life, freshness, nowGameSeconds,
                settled.AmbientLiveSecondsAccrued, iceEndsAtGameSeconds).Advance(nowGameSeconds);
        }

        /// <summary>An explicit non-graphic life transition. Live storage stops protecting a dead
        /// catch; iced/frozen protection, if present, keeps its existing expiry and spoil history.</summary>
        public LandedCatchState StopLife(double nowGameSeconds)
        {
            LandedCatchState settled = Advance(nowGameSeconds);
            FreshnessState freshness = settled.FreshnessState;
            if (freshness.Mode == StorageMode.Live)
                freshness = Freshness.WithMode(freshness, nowGameSeconds, Profile.SpoilPerDay,
                    Settings.SecondsPerDay, Settings.SpoilPolicy, StorageMode.Ambient);
            return new LandedCatchState(Profile, Settings, CatchLifeState.Dead, freshness,
                nowGameSeconds, settled.AmbientLiveSecondsAccrued, settled.IceEndsAtGameSeconds);
        }

        private static void ValidateStorage(LandedCatchProfile profile, CatchLifeState life,
            double now, StorageMode mode, double? iceEnds)
        {
            if (mode < StorageMode.Ambient || mode > StorageMode.Iced)
                throw new ArgumentOutOfRangeException(nameof(mode));
            if (mode == StorageMode.Iced)
            {
                if (!iceEnds.HasValue || !LandedCatchInputs.Finite(iceEnds.Value) || iceEnds.Value < now)
                    throw new ArgumentOutOfRangeException(nameof(iceEnds));
            }
            else if (iceEnds.HasValue)
                throw new ArgumentException("Only iced storage has an ice expiry.", nameof(iceEnds));

            if (mode == StorageMode.Live && (life != CatchLifeState.Alive || !profile.SupportsLiveStorage))
                throw new InvalidOperationException("Live storage requires a living, compatible species.");
            if (life == CatchLifeState.Alive && (mode == StorageMode.Ambient || mode == StorageMode.Iced) &&
                !profile.AmbientLiveSeconds.HasValue)
                throw new InvalidOperationException("Live exposure requires an authored species duration.");
        }
    }
}
