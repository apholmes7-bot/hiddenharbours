using System;

namespace HiddenHarbours.Environment
{
    /// <summary>Where the schedule puts a seal at a moment: away, out on its rock, or in the water by it.</summary>
    public enum SealPlace : byte
    {
        /// <summary>Not at this haul-out: off feeding, or the rock is long covered.</summary>
        Away = 0,

        /// <summary>Out on its rock.</summary>
        Hauled = 1,

        /// <summary>In the water near its rock: waiting for the ebb to bare it, or just pushed off it by the flood.</summary>
        InWater = 2,
    }

    /// <summary>
    /// A seal's hashes: a deterministic number in [0, 1) from the world seed, the haul-out, the spot, a
    /// bucket (the tide, a surfacing count) and a salt. No <see cref="System.Random"/>, no hidden state:
    /// the same arguments give the same number on every machine, forever (rule 5).
    /// </summary>
    public static class SealHash
    {
        /// <summary>FNV-1a over the id's chars: an id's stable 32-bit hash.</summary>
        public static uint Id(string id)
        {
            unchecked
            {
                uint h = 2166136261u;
                if (id != null)
                    for (int i = 0; i < id.Length; i++) { h ^= id[i]; h *= 16777619u; }
                return h;
            }
        }

        /// <summary>The murmur3 finaliser: avalanches every input bit over the output.</summary>
        public static uint Mix(uint h)
        {
            unchecked
            {
                h ^= h >> 16; h *= 0x85ebca6bu;
                h ^= h >> 13; h *= 0xc2b2ae35u;
                h ^= h >> 16;
                return h;
            }
        }

        /// <summary>[0, 1) from the seed, the place, the spot, a bucket and a salt.</summary>
        public static double Unit(int seed, uint placeHash, uint spotHash, long bucket, uint salt)
        {
            unchecked
            {
                uint h = Mix((uint)seed ^ 0x9E3779B9u);
                h = Mix(h ^ placeHash);
                h = Mix(h ^ spotHash);
                h = Mix(h ^ (uint)bucket);
                h = Mix(h ^ (uint)(bucket >> 32));
                h = Mix(h ^ salt);
                return h / 4294967296.0;
            }
        }
    }

    /// <summary>The tide as far as a seal cares: the level, whether it is making, and how low this tide went.</summary>
    public readonly struct SealTideReading
    {
        /// <summary>The water level now, metres above chart datum.</summary>
        public readonly float Level;

        /// <summary>True on the flood.</summary>
        public readonly bool Rising;

        /// <summary>The lowest the water stood since the last high water (on the flood: this tide's low
        /// water; on the ebb: the level now, since it is still falling).</summary>
        public readonly float CycleLow;

        /// <summary>Which tide this is (<c>IEnvironmentService.TideCycleIndexAt</c>): the bucket each
        /// seal's choices are hashed in, so a seal decides once per tide whether to come.</summary>
        public readonly long Cycle;

        public SealTideReading(float level, bool rising, float cycleLow, long cycle)
        {
            Level = level;
            Rising = rising;
            CycleLow = cycleLow;
            Cycle = cycle;
        }
    }

    /// <summary>What the schedule reads the tide through: the environment service, or a test's fake.</summary>
    public interface ISealTideSource
    {
        /// <summary>The water level (m above chart datum) at a game time.</summary>
        float LevelAt(double gameSeconds);

        /// <summary>The tide's index at a game time (steps by one at each high water).</summary>
        long CycleAt(double gameSeconds);
    }

    /// <summary>
    /// ⭐ <b>WHICH SEALS ARE OUT</b> — a pure function of <c>(worldSeed, gameTime, tide, sea state)</c>
    /// (rule 5). Nothing here reads a clock, the engine or the save; nothing is remembered between calls.
    /// The haul-out host evaluates it on its slow tick and a test evaluates it a thousand times in a row
    /// and gets the same answer.
    ///
    /// <para><b>The tide rule (a seal never lies on water).</b> A spot's rock stands at
    /// <see cref="SealSpot.LieMetres"/>. On the ebb a seal hauls out once the water is
    /// <see cref="SealTuning.EbbHaulOutBelowMetres"/> (plus its own hashed spread) under the rock; on the
    /// flood it stays until the water is <see cref="SealTuning.FloodSlipBelowMetres"/> (plus spread)
    /// under it, and only if this tide's low water went far enough to bring it out at all. Both
    /// thresholds are held at or under <c>lie − FloodSlipBelowMetres</c>, which is above zero, so
    /// <see cref="SealPlace.Hauled"/> implies <see cref="Core.TidalExposure.IsExposed(float,float)"/>
    /// with margin. These are the key scene's <c>SealIso.HAULOUT</c> rows' own numbers (ebb 0.30, flood 0.15).</para>
    ///
    /// <para><b>How many.</b> Each seal draws one number per tide; it comes if that number is under the
    /// occupancy now: <see cref="SealTuning.BaseOccupancy"/> x the time of day (fewer at night) x the sea
    /// (fewer in a rough sea). So the most are out at a calm, sunny low tide.</para>
    /// </summary>
    public static class SealSchedule
    {
        /// <summary>How many steps the flood looks back over half a tide for its low water. A sampling
        /// resolution, not a balance number: at 24 the sampled low is within about 2 cm of the true one
        /// on the widest spring, an order under the 15 cm flood margin.</summary>
        public const int LowWaterSamples = 24;

        /// <summary>The look-ahead that tells the flood from the ebb, as a share of a tide period.</summary>
        public const double RisingProbeShare = 1.0 / 240.0;

        // Salts: one per independent choice, so no two choices share a number.
        public const uint SaltOccupancy = 0x51u;
        public const uint SaltEbb = 0x52u;
        public const uint SaltFlood = 0x53u;
        public const uint SaltWater = 0x54u;

        /// <summary>The tide at <paramref name="gameSeconds"/> as a seal reads it.
        /// <paramref name="tidalPeriodSeconds"/> is one high water to the next, in game seconds.</summary>
        public static SealTideReading ReadTide(ISealTideSource tide, double gameSeconds, double tidalPeriodSeconds)
        {
            if (tide == null) return new SealTideReading(0f, false, 0f, 0L);
            float level = tide.LevelAt(gameSeconds);
            long cycle = tide.CycleAt(gameSeconds);
            if (!(tidalPeriodSeconds > 0.0)) return new SealTideReading(level, false, level, cycle);

            double probe = tidalPeriodSeconds * RisingProbeShare;
            bool rising = tide.LevelAt(gameSeconds + probe) > level;
            float low = level;
            if (rising)
            {
                // Low water was at most half a tide ago: walk back over it and keep the lowest.
                double step = tidalPeriodSeconds * 0.5 / LowWaterSamples;
                for (int i = 1; i <= LowWaterSamples; i++)
                {
                    float l = tide.LevelAt(gameSeconds - i * step);
                    if (l < low) low = l;
                }
            }
            return new SealTideReading(level, rising, low, cycle);
        }

        /// <summary>The hour of the day (0..24) at a game time.</summary>
        public static float HourOf(double gameSeconds, double secondsPerDay)
        {
            if (!(secondsPerDay > 0.0)) return 12f;
            double day = gameSeconds / secondsPerDay;
            return (float)((day - Math.Floor(day)) * 24.0);
        }

        /// <summary>The time-of-day factor: 1 by day, the night scale by night, linear ramps at dawn and dusk.</summary>
        public static float DayFactor(in SealTuning k, float hour)
        {
            float night = k.NightOccupancyScale;
            if (hour <= k.DawnFromHour || hour >= k.DuskToHour) return night;
            if (hour < k.DawnToHour) return Lerp(night, 1f, Ramp(k.DawnFromHour, k.DawnToHour, hour));
            if (hour <= k.DuskFromHour) return 1f;
            return Lerp(1f, night, Ramp(k.DuskFromHour, k.DuskToHour, hour));
        }

        /// <summary>The weather factor: 1 in a calm sea, the rough scale in a rough one, linear between.</summary>
        public static float WeatherFactor(in SealTuning k, float seaState01)
            => Lerp(1f, k.RoughOccupancyScale, Ramp(k.CalmSeaState01, k.RoughSeaState01, seaState01));

        /// <summary>The share of spots used now.</summary>
        public static float Occupancy(in SealTuning k, float hour, float seaState01)
            => Clamp01(k.BaseOccupancy) * DayFactor(k, hour) * WeatherFactor(k, seaState01);

        /// <summary>The highest the water may stand for a seal still to be out on a rock at
        /// <paramref name="lieMetres"/>: under it by the flood margin. Every threshold below is held here.</summary>
        public static float DryCeiling(in SealTuning k, float lieMetres) => lieMetres - k.FloodSlipBelowMetres;

        /// <summary>
        /// ONE spot's place, from its rock, the tide, the occupancy and its four hashed numbers. Pure.
        /// </summary>
        public static SealPlace PlaceOf(in SealTuning k, in SealTideReading tide, float lieMetres, float occupancy,
                                        double uOccupancy, double uEbb, double uFlood, double uWater)
        {
            if (uOccupancy >= occupancy) return SealPlace.Away;

            float ceiling = DryCeiling(k, lieMetres);
            float ebb = Math.Min(lieMetres - k.EbbHaulOutBelowMetres - (float)uEbb * k.ArrivalSpreadMetres, ceiling);
            float flood = Math.Min(lieMetres - k.FloodSlipBelowMetres - (float)uFlood * k.ArrivalSpreadMetres, ceiling);
            if (flood < ebb) flood = ebb; // the flood never sends a seal off sooner than the ebb would have let it out

            bool hauled = tide.Rising
                ? tide.CycleLow <= ebb && tide.Level <= flood
                : tide.Level <= ebb;
            if (hauled) return SealPlace.Hauled;

            if (tide.Level <= lieMetres + k.WaterLingerAboveMetres && uWater < k.WaterShare) return SealPlace.InWater;
            return SealPlace.Away;
        }

        /// <summary>
        /// Every spot of a haul-out at a moment, into <paramref name="into"/> (one per spot, in the def's
        /// order). <paramref name="lieMetres"/> and <paramref name="spotHashes"/> are the spots' rock heights
        /// and id hashes, in the same order. Pure; allocates nothing.
        /// </summary>
        public static void Evaluate(in SealTuning k, int worldSeed, uint placeHash, float[] lieMetres, uint[] spotHashes,
                                    int count, in SealTideReading tide, float hour, float seaState01, SealPlace[] into)
        {
            float occupancy = Occupancy(k, hour, seaState01);
            for (int i = 0; i < count; i++)
            {
                uint s = spotHashes[i];
                into[i] = PlaceOf(k, tide, lieMetres[i], occupancy,
                    SealHash.Unit(worldSeed, placeHash, s, tide.Cycle, SaltOccupancy),
                    SealHash.Unit(worldSeed, placeHash, s, tide.Cycle, SaltEbb),
                    SealHash.Unit(worldSeed, placeHash, s, tide.Cycle, SaltFlood),
                    SealHash.Unit(worldSeed, placeHash, s, tide.Cycle, SaltWater));
            }
        }

        static float Ramp(float from, float to, float x)
        {
            if (to <= from) return x >= to ? 1f : 0f;
            return Clamp01((x - from) / (to - from));
        }

        static float Lerp(float a, float b, float t) => a + (b - a) * t;
        static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }
}
