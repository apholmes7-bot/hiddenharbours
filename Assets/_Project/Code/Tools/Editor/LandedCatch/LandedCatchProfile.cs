using System;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tools.LandedCatch
{
    /// <summary>Unshipped research inputs. An adapter must read real values from Defs/config;
    /// no species, mortality duration or shipping balance is supplied by this model.</summary>
    public sealed class LandedCatchProfile
    {
        public string Id { get; }
        public string SpeciesId { get; }
        public bool LandsAlive { get; }
        public bool SupportsLiveStorage { get; }
        public double? AmbientLiveSeconds { get; }
        public float SpoilPerDay { get; }

        public LandedCatchProfile(string id, string speciesId, bool landsAlive,
            bool supportsLiveStorage, double? ambientLiveSeconds, float spoilPerDay)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("A stable profile id is required.", nameof(id));
            if (string.IsNullOrWhiteSpace(speciesId)) throw new ArgumentException("A stable species id is required.", nameof(speciesId));
            if (ambientLiveSeconds.HasValue &&
                (!LandedCatchInputs.Finite(ambientLiveSeconds.Value) || ambientLiveSeconds.Value <= 0d))
                throw new ArgumentOutOfRangeException(nameof(ambientLiveSeconds));
            LandedCatchInputs.Nonnegative(spoilPerDay, nameof(spoilPerDay));
            Id = id;
            SpeciesId = speciesId;
            LandsAlive = landsAlive;
            SupportsLiveStorage = supportsLiveStorage;
            AmbientLiveSeconds = ambientLiveSeconds;
            SpoilPerDay = spoilPerDay;
        }
    }

    public sealed class LandedCatchSettings
    {
        public float SecondsPerDay { get; }
        public SpoilPolicy SpoilPolicy { get; }

        public LandedCatchSettings(float secondsPerDay, in SpoilPolicy spoilPolicy)
        {
            LandedCatchInputs.Nonnegative(secondsPerDay, nameof(secondsPerDay));
            if (secondsPerDay == 0f) throw new ArgumentOutOfRangeException(nameof(secondsPerDay));
            LandedCatchInputs.Nonnegative(spoilPolicy.AmbientRateMultiplier, nameof(spoilPolicy));
            LandedCatchInputs.Nonnegative(spoilPolicy.LiveRateMultiplier, nameof(spoilPolicy));
            LandedCatchInputs.Nonnegative(spoilPolicy.FrozenRateMultiplier, nameof(spoilPolicy));
            LandedCatchInputs.Nonnegative(spoilPolicy.IcedRateMultiplier, nameof(spoilPolicy));
            if (!LandedCatchInputs.Finite(spoilPolicy.UnsellableSpoil) ||
                spoilPolicy.UnsellableSpoil <= 0f || spoilPolicy.UnsellableSpoil > 1f)
                throw new ArgumentOutOfRangeException(nameof(spoilPolicy));
            SecondsPerDay = secondsPerDay;
            SpoilPolicy = spoilPolicy;
        }
    }

    internal static class LandedCatchInputs
    {
        internal static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

        internal static void Nonnegative(double value, string name)
        {
            if (!Finite(value) || value < 0d) throw new ArgumentOutOfRangeException(name);
        }
    }
}
