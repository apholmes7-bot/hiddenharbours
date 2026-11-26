using System;
using System.Collections.Generic;
using HiddenHarbours.Core;
using UnityEngine;

namespace HiddenHarbours.Fishing
{
    /// <summary>
    /// <b>What a villager angler lands</b>: the Fishing module's answer to Core's <see cref="IShoreCatchFacts"/>
    /// (ADR 0053 §11), over the pure catch function the player's rod already rolls through
    /// (<see cref="CatchResolver"/>). Per spot (<see cref="ShoreSpotDef"/>), at the slot's start in routine time:
    /// the season and hour from the calendar, the tide from the environment, then one seeded roll for the bite,
    /// the species and the weight.
    ///
    /// <para><b>Pure and presentation only.</b> The seed is a hash of <c>(worldSeed, angler key, slot, spot)</c>,
    /// never <c>UnityEngine.Random</c> or <c>string.GetHashCode</c>, so equal inputs land the equal fish on every
    /// run and machine. No fish is taken from a stock, no event is published, nothing is saved. The stand asks
    /// once per slot, so the one <see cref="System.Random"/> made per call is one allocation every session.</para>
    /// </summary>
    public sealed class ShoreCatchFacts : IShoreCatchFacts
    {
        // MurmurHash3's finalizer constants: hashing, not tunables.
        private const uint MixA = 0x85EBCA6Bu, MixB = 0xC2B2AE35u;

        private readonly Dictionary<string, ShoreSpotDef> _spots = new(StringComparer.Ordinal);
        private readonly Func<string, FishSpeciesDef> _species;
        private readonly List<FishSpeciesDef> _pool = new();

        /// <summary>
        /// Facts over <paramref name="spots"/> (first id wins), resolving species ids with
        /// <paramref name="species"/> (the runtime passes <see cref="FishSpeciesRegistry.Get"/>).
        /// </summary>
        public ShoreCatchFacts(IReadOnlyList<ShoreSpotDef> spots, Func<string, FishSpeciesDef> species)
        {
            _species = species ?? (_ => null);
            if (spots == null) return;
            for (int i = 0; i < spots.Count; i++)
            {
                ShoreSpotDef spot = spots[i];
                if (spot == null || string.IsNullOrEmpty(spot.Id) || _spots.ContainsKey(spot.Id)) continue;
                _spots[spot.Id] = spot;
            }
        }

        /// <summary>The spot with this id, or null.</summary>
        public ShoreSpotDef Spot(string spotId) =>
            spotId != null && _spots.TryGetValue(spotId, out ShoreSpotDef spot) ? spot : null;

        /// <inheritdoc/>
        public bool TryPick(string spotId, int worldSeed, uint anglerKeyHash, long slotIndex,
                            double slotStartTotalSeconds, out ShoreCatch shoreCatch)
        {
            shoreCatch = default;
            ShoreSpotDef spot = Spot(spotId);
            if (spot == null || spot.SpeciesIds == null) return false;

            _pool.Clear();
            for (int i = 0; i < spot.SpeciesIds.Length; i++)
            {
                FishSpeciesDef def = _species(spot.SpeciesIds[i]);
                if (def != null) _pool.Add(def);
            }
            if (_pool.Count == 0) return false;

            CatchContext ctx = ContextAt(spot, slotStartTotalSeconds);
            var rng = new System.Random(SeedFor(worldSeed, anglerKeyHash, slotIndex, spot.Id));
            if (rng.NextDouble() >= spot.BiteChance01) return false;

            FishSpeciesDef fish = CatchResolver.Resolve(_pool, in ctx, rng);
            if (fish == null) return false;
            float kg = CatchResolver.RollWeight(fish, rng);
            shoreCatch = new ShoreCatch(fish.Id, kg, FishFeelMath.Size01(kg, fish.MinWeightKg, fish.MaxWeightKg));
            return true;
        }

        /// <summary>The situation at <paramref name="spot"/> at <paramref name="totalSeconds"/>: the calendar's
        /// season and hour, the environment's tide, the spot's gear. Depth-less, bait-less, school-less: a
        /// villager's handline is the plain roll.</summary>
        public static CatchContext ContextAt(ShoreSpotDef spot, double totalSeconds)
        {
            GameConfig config = GameServices.Config;
            double secondsPerDay = Math.Max(1d, GameServices.SecondsPerDay);
            double t = Math.Max(0d, totalSeconds);
            long day = (long)Math.Floor(t / secondsPerDay);
            float hour = (float)((t - day * secondsPerDay) / secondsPerDay * RoutineHours);
            int daysPerSeason = config != null ? config.DaysPerSeason : GameConfig.DefaultDaysPerSeason;
            int startDay = config != null ? config.StartDayOfYear : 0;
            Season season = GameCalendar.AtDay(day, daysPerSeason, startDay).Season;
            IEnvironmentService env = GameServices.Environment;
            float tide = env != null ? env.TideHeightAt(t) : 0f;
            return new CatchContext(spot.RegionId, tide, hour, season, spot.Gear);
        }

        private const float RoutineHours = DaySchedule.HoursPerDay;

        /// <summary>The roll's seed: a hash of the world, the angler, the slot and the spot. Stable everywhere.</summary>
        public static int SeedFor(int worldSeed, uint anglerKeyHash, long slotIndex, string spotId)
        {
            unchecked
            {
                uint h = Mix((uint)worldSeed ^ anglerKeyHash);
                h = Mix(h ^ (uint)slotIndex);
                h = Mix(h ^ (uint)(slotIndex >> 32));
                h = Mix(h ^ FigureKeyHash.Of(spotId));
                return (int)(h & 0x7FFFFFFFu);
            }
        }

        private static uint Mix(uint h)
        {
            unchecked
            {
                h ^= h >> 16;
                h *= MixA;
                h ^= h >> 13;
                h *= MixB;
                h ^= h >> 16;
                return h;
            }
        }
    }

    /// <summary>
    /// <b>The shore spots, loaded at boot</b>: the authored <see cref="ShoreSpotLibrary"/> from Resources,
    /// published as <see cref="GameServices.ShoreCatch"/>, the <see cref="FishSpeciesRegistrar"/> pattern.
    /// Species ids are resolved through <see cref="FishSpeciesRegistry"/> at each pick, so the order the two
    /// boot methods run in does not matter. With no library the seam stays at its empty answer.
    /// </summary>
    public static class ShoreCatchRegistrar
    {
        /// <summary>Resources path (no extension) the shore spot library is loaded from.</summary>
        public const string ResourcesPath = "ShoreSpotLibrary";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            var lib = Resources.Load<ShoreSpotLibrary>(ResourcesPath);
            if (lib != null) GameServices.ShoreCatch = lib.BuildFacts();
        }
    }
}
