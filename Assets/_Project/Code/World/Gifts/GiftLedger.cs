using System;
using System.Collections.Generic;

namespace HiddenHarbours.World
{
    /// <summary>
    /// <b>Who has had their gift today</b> (gifting slice 1): a count per person for ONE game day, emptied
    /// the moment a different day is asked about (the next morning, or a day index that went backwards
    /// because a new game began).
    ///
    /// <para><b>⚠ Runtime only — not saved.</b> Persisting it needs a save field, and the save version is not
    /// this slice's to bump (v22 and v23 are in flight). So quitting and reloading on the same game day
    /// forgets today's gifts and a person would accept one more. Small, honest, and stated in the design
    /// doc; a later slice moves it into the save beside the relationship graph.</para>
    ///
    /// <para><see cref="Shared"/> is a static so the count survives walking from one region scene to the
    /// next (each region has its own <see cref="WorldInteractor"/>).</para>
    /// </summary>
    public sealed class GiftLedger
    {
        /// <summary>The game's one ledger.</summary>
        public static GiftLedger Shared { get; } = new GiftLedger();

        private readonly Dictionary<string, int> _counts = new Dictionary<string, int>(StringComparer.Ordinal);
        private int _day = int.MinValue;

        /// <summary>Gifts <paramref name="npcId"/> has accepted on <paramref name="dayIndex"/>.</summary>
        public int GiftsToday(string npcId, int dayIndex)
        {
            Roll(dayIndex);
            if (string.IsNullOrEmpty(npcId)) return 0;
            return _counts.TryGetValue(npcId, out int n) ? n : 0;
        }

        /// <summary>Count one accepted gift to <paramref name="npcId"/> on <paramref name="dayIndex"/>.</summary>
        public void Count(string npcId, int dayIndex)
        {
            if (string.IsNullOrEmpty(npcId)) return;
            int n = GiftsToday(npcId, dayIndex);
            _counts[npcId] = n + 1;
        }

        /// <summary>Forget everything (test isolation).</summary>
        public void Clear()
        {
            _counts.Clear();
            _day = int.MinValue;
        }

        private void Roll(int dayIndex)
        {
            if (dayIndex == _day) return;
            _counts.Clear();
            _day = dayIndex;
        }
    }
}
