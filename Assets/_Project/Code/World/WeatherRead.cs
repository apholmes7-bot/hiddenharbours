using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.World
{
    /// <summary>
    /// <b>What the wind is doing today, sorted into a speaker's words.</b> One look at this day's wind
    /// at <see cref="WeatherReadDef.ReadHour"/> and one at <see cref="WeatherReadDef.OutlookHour"/>,
    /// both asked of Core's deterministic wind law (<see cref="IWaveWindSource"/>), give a
    /// <see cref="Reading"/>: where it comes from, how hard, and what the rest of the day does to it.
    ///
    /// <para><b>Always true, never saved.</b> The wind is a pure function of <c>(worldSeed, gameTime)</c>,
    /// so the reading is recomputed every time he is asked and comes out the same (rule 5). The line he
    /// picks from each pool is chosen by a hash of <c>(worldSeed, day)</c>: no new randomness, and the same
    /// day always gets the same words.</para>
    ///
    /// <para>The REAL sim wind, not the shaded look: a storm band at the island's edge is presentation
    /// (Core's <c>LocalWeather</c>), and a man reading the harbour reads the weather the HUD and the boats
    /// obey.</para>
    /// </summary>
    public static class WeatherRead
    {
        /// <summary>One day's read of the wind.</summary>
        public readonly struct Reading
        {
            /// <summary>Where the morning wind comes FROM: 0 = north, clockwise in eighths (1 = nor'east ... 7 = nor'west).</summary>
            public readonly int FromPoint;
            public readonly float NowStrength;
            public readonly float LaterStrength;
            public readonly WindBand NowBand;
            public readonly WindBand LaterBand;
            public readonly WeatherOutlook Outlook;

            public Reading(int fromPoint, float nowStrength, float laterStrength,
                           WindBand nowBand, WindBand laterBand, WeatherOutlook outlook)
            {
                FromPoint = fromPoint;
                NowStrength = nowStrength;
                LaterStrength = laterStrength;
                NowBand = nowBand;
                LaterBand = laterBand;
                Outlook = outlook;
            }
        }

        /// <summary>Salts that keep the now pick and the outlook pick independent of each other.</summary>
        private const uint NowSalt = 0x9E3779B9u;
        private const uint OutlookSalt = 0x85EBCA6Bu;

        /// <summary>
        /// Today's line for <paramref name="def"/>, from the live services: the environment's wind law,
        /// the world seed and the clock's day. Null when any of them is missing (pre-boot, a test scene
        /// without an environment): a missing read is better than an untrue one.
        /// </summary>
        public static string TodayLine(WeatherReadDef def)
        {
            if (def == null) return null;
            IEnvironmentService env = GameServices.Environment;
            IGameClock clock = GameServices.Clock;
            if (!(env is IWaveWindSource wind) || clock == null) return null;
            return LineFor(def, wind, env.WorldSeed, clock.DayIndex, GameServices.SecondsPerDay);
        }

        /// <summary>The line for a given day, from any wind source. Null when the asset has no words for
        /// the reading it got (the content tests forbid that).</summary>
        public static string LineFor(WeatherReadDef def, IWaveWindSource wind, int worldSeed, int day,
                                     float secondsPerDay)
        {
            if (def == null || wind == null) return null;
            Reading reading = Read(def, wind, day, secondsPerDay);
            return Compose(def, in reading, worldSeed, day);
        }

        /// <summary>Read the day's wind: the morning look, the later look, and the outlook between them.</summary>
        public static Reading Read(WeatherReadDef def, IWaveWindSource wind, int day, float secondsPerDay)
        {
            double dayStart = (double)day * secondsPerDay;
            double secondsPerHour = secondsPerDay / 24.0;
            Vector2 now = Look(wind, dayStart + def.ReadHour * secondsPerHour, def, secondsPerHour);
            Vector2 later = Look(wind, dayStart + def.OutlookHour * secondsPerHour, def, secondsPerHour);

            float nowStrength = now.magnitude;
            float laterStrength = later.magnitude;
            WindBand nowBand = def.BandOf(nowStrength);
            WindBand laterBand = def.BandOf(laterStrength);
            WeatherOutlook outlook = OutlookOf(nowBand, laterBand, laterStrength - nowStrength, def.ChangeAtLeast);
            return new Reading(FromPointOf(now), nowStrength, laterStrength, nowBand, laterBand, outlook);
        }

        /// <summary>The wind averaged (as a vector) over the look window centred on <paramref name="centre"/>.</summary>
        public static Vector2 Look(IWaveWindSource wind, double centre, WeatherReadDef def, double secondsPerHour)
        {
            int n = Mathf.Max(1, def.LookSamples);
            double window = System.Math.Max(0.0, def.LookWindowHours) * secondsPerHour;
            if (n == 1 || window <= 0.0) return wind.WaveWindAt(centre).Wind;

            double step = window / (n - 1);
            double start = centre - window * 0.5;
            Vector2 sum = Vector2.zero;
            for (int i = 0; i < n; i++)
                sum += wind.WaveWindAt(start + step * i).Wind;
            return sum / n;
        }

        /// <summary>
        /// What the rest of the day means for a dory. Too much now is a day in harbour (easing or not);
        /// fair now but too much later is go early, home early; otherwise the trend decides.
        /// </summary>
        public static WeatherOutlook OutlookOf(WindBand now, WindBand later, float change, float changeAtLeast)
        {
            bool easing = change <= -changeAtLeast;
            bool building = change >= changeAtLeast;
            if (now >= WindBand.TooMuch) return easing ? WeatherOutlook.StayInEasing : WeatherOutlook.StayIn;
            if (later >= WindBand.TooMuch) return WeatherOutlook.HomeEarly;
            if (building) return WeatherOutlook.Building;
            if (easing) return WeatherOutlook.Easing;
            return WeatherOutlook.Holding;
        }

        /// <summary>
        /// The eighth of the compass the wind comes FROM. Core's wind vector points the way the wind
        /// BLOWS (toward), +y north and +x east, so the source is the opposite bearing. A dead calm (zero
        /// vector) reads as north; it only happens below the light-air edge, where the words do not lean
        /// on the direction.
        /// </summary>
        public static int FromPointOf(Vector2 windToward)
        {
            Vector2 from = -windToward;
            float bearing = Mathf.Atan2(from.x, from.y) * Mathf.Rad2Deg;   // clockwise from north
            if (bearing < 0f) bearing += 360f;
            return Mathf.FloorToInt((bearing + 22.5f) / 45f) % 8;
        }

        /// <summary>The words for a reading: one now line and one outlook line, each picked by (seed, day).</summary>
        public static string Compose(WeatherReadDef def, in Reading reading, int worldSeed, int day)
        {
            string[] nowPool = def.NowLines(reading.NowBand);
            string[] outlookPool = def.OutlookLines(reading.Outlook);
            if (nowPool.Length == 0 || outlookPool.Length == 0) return null;

            string now = nowPool[Pick(worldSeed, day, NowSalt, nowPool.Length)];
            string outlook = outlookPool[Pick(worldSeed, day, OutlookSalt, outlookPool.Length)];
            string from = CompassWord(def, reading.FromPoint);
            return now.Replace("{from}", from) + " " + outlook.Replace("{from}", from);
        }

        /// <summary>The asset's word for a compass point; empty when the asset has fewer than eight.</summary>
        public static string CompassWord(WeatherReadDef def, int point)
        {
            string[] words = def.CompassWords;
            return words != null && point >= 0 && point < words.Length && words[point] != null ? words[point] : "";
        }

        /// <summary>Which line of a pool of <paramref name="count"/> a day gets: a pure integer hash of
        /// (seed, day, salt), so it is the same on every machine and every time it is asked.</summary>
        public static int Pick(int worldSeed, int day, uint salt, int count)
        {
            if (count <= 1) return 0;
            unchecked
            {
                uint h = (uint)worldSeed * 0x27D4EB2Fu ^ (uint)day * 0x165667B1u ^ salt;
                h ^= h >> 15; h *= 0x85EBCA77u;
                h ^= h >> 13; h *= 0xC2B2AE3Du;
                h ^= h >> 16;
                return (int)(h % (uint)count);
            }
        }
    }
}
