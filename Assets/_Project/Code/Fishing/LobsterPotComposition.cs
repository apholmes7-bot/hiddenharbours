using System;
using HiddenHarbours.Core;

namespace HiddenHarbours.Fishing
{
    /// <summary>Haul-season catch mix. Shares describe all lobsters present in a pot, not landings.
    /// Short and berried are independent, overlapping attributes; berried implies female.</summary>
    public readonly struct LobsterPotSeason
    {
        public readonly double FullSoakOccupancy01, ShortShare01, BerriedShare01;

        public LobsterPotSeason(double fullSoakOccupancy01, double shortShare01, double berriedShare01)
        {
            FullSoakOccupancy01 = fullSoakOccupancy01;
            ShortShare01 = shortShare01;
            BerriedShare01 = berriedShare01;
        }

        internal bool IsValid => LobsterPotComposition.Probability(FullSoakOccupancy01) &&
            LobsterPotComposition.Probability(ShortShare01) &&
            LobsterPotComposition.Probability(BerriedShare01);
    }

    /// <summary>Caller-authored ground snapshot. No habitat lookup, regional defaults or live tuning.
    /// Berried animals below the retention gauge must still meet MinBerriedCarapaceMm.</summary>
    public readonly struct LobsterPotGround
    {
        public readonly string Id;
        public readonly int Capacity, MinCarapaceMm, RetentionGaugeMm, MaxCarapaceMm, MinBerriedCarapaceMm;
        public readonly LobsterPotSeason EarlySpring, HighSummer, TheTurn, HardWinter;

        public LobsterPotGround(string id, int capacity, int minCarapaceMm, int retentionGaugeMm,
            int maxCarapaceMm, int minBerriedCarapaceMm, LobsterPotSeason earlySpring,
            LobsterPotSeason highSummer, LobsterPotSeason theTurn, LobsterPotSeason hardWinter)
        {
            Id = id;
            Capacity = capacity;
            MinCarapaceMm = minCarapaceMm;
            RetentionGaugeMm = retentionGaugeMm;
            MaxCarapaceMm = maxCarapaceMm;
            MinBerriedCarapaceMm = minBerriedCarapaceMm;
            EarlySpring = earlySpring;
            HighSummer = highSummer;
            TheTurn = theTurn;
            HardWinter = hardWinter;
        }

        internal bool IsValid => !string.IsNullOrWhiteSpace(Id) && Capacity > 0 &&
            MinCarapaceMm > 0 && MinCarapaceMm < RetentionGaugeMm &&
            RetentionGaugeMm <= MaxCarapaceMm && MinBerriedCarapaceMm >= MinCarapaceMm &&
            MinBerriedCarapaceMm < RetentionGaugeMm &&
            EarlySpring.IsValid && HighSummer.IsValid && TheTurn.IsValid && HardWinter.IsValid;

        internal LobsterPotSeason At(Season season)
        {
            switch (season)
            {
                case Season.EarlySpring: return EarlySpring;
                case Season.HighSummer: return HighSummer;
                case Season.TheTurn: return TheTurn;
                default: return HardWinter;
            }
        }
    }

    /// <summary>Explicit snapshot of Def/config inputs; no defaults. Seconds use the Core clock's
    /// scale. A future adapter supplies SecondsPerDay, DaysPerSeason and StartDayOfYear from config.
    /// FullSoakGameSeconds is the saturation point of the prototype's linear occupancy curve.</summary>
    public readonly struct LobsterPotSettings
    {
        public readonly double SecondsPerDay, FullSoakGameSeconds;
        public readonly int DaysPerSeason, StartDayOfYear;

        public LobsterPotSettings(double secondsPerDay, int daysPerSeason, int startDayOfYear,
            double fullSoakGameSeconds)
        {
            SecondsPerDay = secondsPerDay;
            DaysPerSeason = daysPerSeason;
            StartDayOfYear = startDayOfYear;
            FullSoakGameSeconds = fullSoakGameSeconds;
        }

        internal bool IsValid => LobsterPotComposition.Finite(SecondsPerDay) && SecondsPerDay > 0 &&
            DaysPerSeason > 0 && StartDayOfYear >= 0 && StartDayOfYear < 4L * DaysPerSeason &&
            LobsterPotComposition.Finite(SecondsPerDay * (4d * DaysPerSeason)) &&
            LobsterPotComposition.Finite(FullSoakGameSeconds) && FullSoakGameSeconds > 0;
    }

    public readonly struct LobsterPotAnimal
    {
        public readonly int CarapaceMm;
        public readonly bool IsShort, IsBerried;

        internal LobsterPotAnimal(int carapaceMm, bool isShort, bool isBerried)
        {
            CarapaceMm = carapaceMm;
            IsShort = isShort;
            IsBerried = isBerried;
        }
    }

    public readonly struct LobsterPotSummary
    {
        public readonly Season Season;
        public readonly int Count, Shorts, Berried, ShortsAndBerried;
        /// <summary>Size and egg exclusions only; this is not a licence or retention decision.</summary>
        public int SizeAndEggEligible => Count - Shorts - Berried + ShortsAndBerried;

        internal LobsterPotSummary(Season season, int count, int shorts, int berried, int overlap)
        {
            Season = season;
            Count = count;
            Shorts = shorts;
            Berried = berried;
            ShortsAndBerried = overlap;
        }
    }

    /// <summary>
    /// Inactive statistical prototype: pure (seed, absolute haul time, ground, soak, settings).
    /// O(capacity + id length), no managed allocations, no randomness or state outside these inputs.
    /// Not a SchoolField encounter/depletion solver, retention rule, or player/NPC controller.
    /// </summary>
    public static class LobsterPotComposition
    {
        internal static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        internal static bool Probability(double value) => Finite(value) && value >= 0 && value <= 1;

        /// <summary>
        /// Writes exactly summary.Count animals into caller storage; the suffix is untouched.
        /// False means invalid input or storage smaller than ground.Capacity: no writes, zero summary.
        /// Time/soak must be finite, nonnegative, with soak no longer than the elapsed world time.
        /// Haul time chooses the seasonal row and a fixed random stream. At fixed haul time,
        /// increasing soak only adds occupied slots, up to FullSoakGameSeconds; it never grows animals.
        /// </summary>
        public static bool TryCompose(int seed, double gameTimeSeconds, in LobsterPotGround ground,
            double soakGameSeconds, in LobsterPotSettings settings, LobsterPotAnimal[] buffer,
            out LobsterPotSummary summary)
        {
            summary = default;
            if (!ground.IsValid || !settings.IsValid || !Finite(gameTimeSeconds) || gameTimeSeconds < 0 ||
                !Finite(soakGameSeconds) || soakGameSeconds < 0 || soakGameSeconds > gameTimeSeconds ||
                buffer == null || buffer.Length < ground.Capacity)
                return false;

            // Reduce before adding the calendar offset, preserving long-time/subsecond precision.
            double seasonSeconds = settings.SecondsPerDay * settings.DaysPerSeason;
            double yearSeconds = seasonSeconds * 4;
            double phase = gameTimeSeconds % yearSeconds;
            double offset = settings.StartDayOfYear * settings.SecondsPerDay;
            double untilWrap = yearSeconds - offset;
            phase = phase >= untilWrap ? phase - untilWrap : phase + offset;
            if (phase >= yearSeconds) phase = 0; // Possible rounding at the last representable instant.
            var season = (Season)(int)(phase / seasonSeconds);
            var mix = ground.At(season);
            double occupancy = mix.FullSoakOccupancy01 *
                Math.Min(1d, soakGameSeconds / settings.FullSoakGameSeconds);
            uint key = unchecked((uint)StableHash.CatchSeed(seed, ground.Id, gameTimeSeconds));
            int count = 0, shorts = 0, berried = 0, overlap = 0;
            for (int slot = 0; slot < ground.Capacity; slot++)
            {
                uint slotKey = StableHash.Fold(key, slot);
                if (Unit(slotKey, 0) >= occupancy) continue;
                bool isShort = Unit(slotKey, 1) < mix.ShortShare01;
                bool isBerried = Unit(slotKey, 2) < mix.BerriedShare01;
                int min = isShort ? ground.MinCarapaceMm : ground.RetentionGaugeMm;
                int max = isShort ? ground.RetentionGaugeMm - 1 : ground.MaxCarapaceMm;
                if (isBerried) min = Math.Max(min, ground.MinBerriedCarapaceMm);
                // Multiply-high maps all 32 hash bits into the authored inclusive integer range.
                uint draw = StableHash.Finalize(StableHash.Fold(slotKey, 3));
                int size = min + (int)(((ulong)draw * (uint)(max - min + 1)) >> 32);
                buffer[count++] = new LobsterPotAnimal(size, isShort, isBerried);
                if (isShort) shorts++;
                if (isBerried) berried++;
                if (isShort && isBerried) overlap++;
            }
            summary = new LobsterPotSummary(season, count, shorts, berried, overlap);
            return true;
        }

        // Domain tags distinguish occupancy, short, berried and size draws; these are not tuning.
        private static double Unit(uint key, int domain)
            => StableHash.Finalize(StableHash.Fold(key, domain)) / 4294967296d;
    }
}
