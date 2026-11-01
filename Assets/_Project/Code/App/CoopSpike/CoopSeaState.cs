using System;
using HiddenHarbours.Core;
using Unity.Netcode;

namespace HiddenHarbours.CoopSpike
{
    /// <summary>One atomic environment sample. No environmental simulation state travels or is saved.</summary>
    public struct CoopSeaState : INetworkSerializable, IEquatable<CoopSeaState>
    {
        public int Seed;
        public double Seconds;
        public float Rate;
        public bool Paused;
        public bool Ready;

        public static CoopSeaState Capture(IGameClock clock, int seed) => new CoopSeaState
        {
            Seed = seed, Seconds = clock.TotalSeconds, Rate = clock.TimeScale,
            Paused = clock.IsPaused, Ready = true
        };

        // This spike starts fresh on identical authored seeds. Refuse rather than pretend time fixes a seed.
        public bool Apply(IGameClock clock, int localSeed)
        {
            if (!Ready || clock == null || Seed != localSeed || double.IsNaN(Seconds)
                || double.IsInfinity(Seconds) || Seconds < 0 || float.IsNaN(Rate)
                || float.IsInfinity(Rate) || Rate < 0) return false;
            clock.SeekTo(Seconds);
            clock.TimeScale = Rate;
            clock.IsPaused = Paused;
            return true;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref Seed); s.SerializeValue(ref Seconds); s.SerializeValue(ref Rate);
            s.SerializeValue(ref Paused); s.SerializeValue(ref Ready);
        }

        public bool Equals(CoopSeaState other) => Seed == other.Seed && Seconds == other.Seconds
            && Rate == other.Rate && Paused == other.Paused && Ready == other.Ready;
        public override bool Equals(object obj) => obj is CoopSeaState other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Seed, Seconds, Rate, Paused, Ready);
    }
}
