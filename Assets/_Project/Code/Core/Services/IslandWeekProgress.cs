using System;
using System.Collections.Generic;

namespace HiddenHarbours.Core
{
    [Flags]
    public enum IslandWeekFact
    {
        None = 0,
        DoryMended = 1,
        CodLicence = 2,
        FishFromDory = 4,
        ShellfishSold = 8,
        DayReached = 16,
        All = DoryMended | CodLicence | FishFromDory | ShellfishSold | DayReached
    }

    /// <summary>A reading, never saved. Missing keys remain in the authored requirement order.</summary>
    public readonly struct IslandWeekSnapshot
    {
        public readonly IslandWeekFact Held;
        private readonly IReadOnlyList<string> _missingTextKeys;
        public IReadOnlyList<string> MissingTextKeys => _missingTextKeys ?? Array.Empty<string>();
        public bool AllHold => Held == IslandWeekFact.All;
        public bool Holds(IslandWeekFact fact) => fact != IslandWeekFact.None && (Held & fact) == fact;

        internal IslandWeekSnapshot(IslandWeekFact held, List<string> missing)
        {
            Held = held;
            _missingTextKeys = missing.AsReadOnly();
        }
    }

    /// <summary>Reads the five facts. It cannot grant the weather flag, a conversation or an exit.</summary>
    public interface IIslandWeekProgress
    {
        /// <summary>Pass the Core clock's zero-based <see cref="IGameClock.DayIndex"/>.</summary>
        IslandWeekSnapshot Read(SaveData save, int dayIndex);
    }

    /// <summary>On-demand, deterministic evaluation over explicit saved facts and the game clock.</summary>
    public sealed class IslandWeekProgress : IIslandWeekProgress
    {
        private readonly string _doryId;
        private readonly string _licenceId;
        private readonly int _earliestDay;
        private readonly string[] _textKeys;

        public IslandWeekProgress(IslandWeekRulesDef rules)
        {
            if (rules == null) throw new ArgumentNullException(nameof(rules));
            string error = rules.ValidationError();
            if (error != null) throw new ArgumentException(error, nameof(rules));
            _doryId = rules.DoryId;
            _licenceId = rules.LicenceId;
            _earliestDay = rules.EarliestDay;
            _textKeys = new[] { rules.DoryMendedTextKey, rules.CodLicenceTextKey, rules.FishFromDoryTextKey,
                                rules.ShellfishSoldTextKey, rules.DayReachedTextKey };
        }

        public IslandWeekSnapshot Read(SaveData save, int dayIndex)
        {
            IslandWeekFact held = IslandWeekFact.None;
            if (save?.RepairedBoats != null && save.RepairedBoats.Contains(_doryId)) held |= IslandWeekFact.DoryMended;
            if (save?.OwnedLicenses != null && save.OwnedLicenses.Contains(_licenceId)) held |= IslandWeekFact.CodLicence;
            if (HasFlag(save, IslandWeekFactRecorder.FishFromDoryKey)) held |= IslandWeekFact.FishFromDory;
            if (HasFlag(save, IslandWeekFactRecorder.ShellfishSoldKey)) held |= IslandWeekFact.ShellfishSold;
            if (dayIndex >= _earliestDay - 1)
                held |= IslandWeekFact.DayReached;

            var missing = new List<string>(_textKeys.Length);
            for (int i = 0; i < _textKeys.Length; i++)
                if ((held & (IslandWeekFact)(1 << i)) == 0) missing.Add(_textKeys[i]);
            return new IslandWeekSnapshot(held, missing);
        }

        internal static bool HasFlag(SaveData save, string key)
        {
            if (save?.OnboardingFlags == null) return false;
            foreach (SaveFlag flag in save.OnboardingFlags)
                if (flag.Key == key) return flag.Value;
            return false;
        }
    }
}
