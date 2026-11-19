using System;
using System.Collections.Generic;
using HiddenHarbours.Core;

namespace HiddenHarbours.World
{
    /// <summary>
    /// <b>Who has had their gift today</b> (gifting): a count per person for ONE game day, emptied the moment
    /// a different day is asked about (the next morning, or a day index that went backwards).
    ///
    /// <para><b>Saved with the game (v24, <see cref="SaveData.NpcGiftDays"/>).</b> The ledger is bound to one
    /// session, in the catch record's shape: when the session is the live <see cref="SaveData"/>, binding
    /// loads that save's rows (<see cref="GiftDaySave.Load"/>) and every counted gift is written back
    /// (<see cref="GiftDaySave.Write"/>). Only the current day's rows are kept in the save: a new day drops
    /// the old ones, so the list never grows past the cast. A different session (another save loaded, a new
    /// game) starts the ledger from that session's rows. A null or non-save session keeps the count in
    /// memory only (tests).</para>
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
        private object _session;

        /// <summary>Gifts <paramref name="npcId"/> has accepted on <paramref name="dayIndex"/> in
        /// <paramref name="session"/> (the live save object, or null for memory only).</summary>
        public int GiftsToday(string npcId, int dayIndex, object session = null)
        {
            BindTo(session);
            Roll(dayIndex);
            if (string.IsNullOrEmpty(npcId)) return 0;
            return _counts.TryGetValue(npcId, out int n) ? n : 0;
        }

        /// <summary>Count one accepted gift to <paramref name="npcId"/> on <paramref name="dayIndex"/>, and
        /// write it to <paramref name="session"/> when that is a save.</summary>
        public void Count(string npcId, int dayIndex, object session = null)
        {
            if (string.IsNullOrEmpty(npcId)) return;
            int n = GiftsToday(npcId, dayIndex, session) + 1;
            _counts[npcId] = n;
            GiftDaySave.Write(_session as SaveData, npcId, dayIndex, n);
        }

        /// <summary>Forget everything and unbind (test isolation). The save is not touched.</summary>
        public void Clear()
        {
            _counts.Clear();
            _day = int.MinValue;
            _session = null;
        }

        private void BindTo(object session)
        {
            if (ReferenceEquals(session, _session)) return;
            _counts.Clear();
            _session = session;
            _day = GiftDaySave.Load(session as SaveData, _counts);
        }

        private void Roll(int dayIndex)
        {
            if (dayIndex == _day) return;
            _counts.Clear();
            _day = dayIndex;
            GiftDaySave.KeepOnly(_session as SaveData, dayIndex);
        }
    }

    /// <summary>
    /// <b>The gift ledger's rows in the save</b> (v24, <see cref="SaveData.NpcGiftDays"/>): one row per person
    /// given a gift on the current game day. Null-safe: a null save reads as no rows and a write to it does
    /// nothing. Deterministic: rows are read in saved order and nothing is invented.
    /// </summary>
    public static class GiftDaySave
    {
        /// <summary>
        /// Fill <paramref name="into"/> with the counts of the LATEST day the save holds rows for, and return
        /// that day (<see cref="int.MinValue"/> when there are none). A row with no person or no count is
        /// skipped (the migration's repair drops them anyway); two rows for one person on that day keep the
        /// larger count, so a damaged save never lets a villager take more than she was given.
        /// </summary>
        public static int Load(SaveData save, Dictionary<string, int> into)
        {
            if (save?.NpcGiftDays == null || into == null) return int.MinValue;
            int day = int.MinValue;
            List<NpcGiftDayDto> rows = save.NpcGiftDays;
            for (int i = 0; i < rows.Count; i++)
                if (Usable(rows[i]) && rows[i].DayIndex > day) day = rows[i].DayIndex;
            if (day == int.MinValue) return day;

            for (int i = 0; i < rows.Count; i++)
            {
                NpcGiftDayDto r = rows[i];
                if (!Usable(r) || r.DayIndex != day) continue;
                if (!into.TryGetValue(r.NpcId, out int n) || r.Count > n) into[r.NpcId] = r.Count;
            }
            return day;
        }

        /// <summary>Write <paramref name="npcId"/>'s count for <paramref name="dayIndex"/>: rows for any other
        /// day are dropped first, then the person's row is updated in place or appended.</summary>
        public static void Write(SaveData save, string npcId, int dayIndex, int count)
        {
            if (save == null || string.IsNullOrEmpty(npcId) || count <= 0) return;
            save.NpcGiftDays ??= new List<NpcGiftDayDto>();
            KeepOnly(save, dayIndex);

            List<NpcGiftDayDto> rows = save.NpcGiftDays;
            for (int i = 0; i < rows.Count; i++)
            {
                if (!string.Equals(rows[i].NpcId, npcId, StringComparison.Ordinal)) continue;
                rows[i].Count = count;
                for (int k = rows.Count - 1; k > i; k--)          // one row per person
                    if (string.Equals(rows[k].NpcId, npcId, StringComparison.Ordinal)) rows.RemoveAt(k);
                return;
            }
            rows.Add(new NpcGiftDayDto { NpcId = npcId, DayIndex = dayIndex, Count = count });
        }

        /// <summary>Drop every row that is unusable or not for <paramref name="dayIndex"/>.</summary>
        public static void KeepOnly(SaveData save, int dayIndex)
        {
            List<NpcGiftDayDto> rows = save?.NpcGiftDays;
            if (rows == null) return;
            for (int i = rows.Count - 1; i >= 0; i--)
                if (!Usable(rows[i]) || rows[i].DayIndex != dayIndex) rows.RemoveAt(i);
        }

        private static bool Usable(NpcGiftDayDto r) => r != null && !string.IsNullOrEmpty(r.NpcId) && r.Count > 0;
    }
}
