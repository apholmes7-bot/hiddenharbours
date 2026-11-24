using System.Collections.Generic;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// One species on the player's catch record: how many caught, how many of those were protected and
    /// released, and the heaviest one. A row of <see cref="CatchRecordLog"/>; what the Notebook's catch
    /// page reads.
    /// </summary>
    public readonly struct CaughtSpeciesEntry
    {
        /// <summary>The species' stable Def id (<c>fish.*</c>), the key a row is counted under.</summary>
        public readonly string SpeciesId;

        /// <summary>The name the catch carried (<see cref="CatchItem.DisplayName"/>), so the page never
        /// has to ask the Fishing module what a species is called (rule 4).</summary>
        public readonly string DisplayName;

        /// <summary>How many have been caught, released ones included. At least 1 on a stored row.</summary>
        public readonly int Count;

        /// <summary>How many of <see cref="Count"/> were protected, documented and put back.</summary>
        public readonly int ReleasedCount;

        /// <summary>The heaviest catch of this species, in kg (<see cref="CatchItem.WeightKg"/>, the size
        /// every catch carries and the unit the landing line shows).</summary>
        public readonly float RecordWeightKg;

        public CaughtSpeciesEntry(string speciesId, string displayName, int count, int releasedCount,
                                  float recordWeightKg)
        {
            SpeciesId = speciesId ?? "";
            DisplayName = displayName ?? "";
            Count = count;
            ReleasedCount = releasedCount;
            RecordWeightKg = recordWeightKg;
        }
    }

    /// <summary>
    /// <b>THE CATCH RECORD</b>: one row per species caught, in the order each was first caught, with a
    /// count and the record (heaviest) weight. Pure and engine-free so it is provable headlessly;
    /// <see cref="CatchRecords"/> feeds it from the bus in a running game.
    ///
    /// <para><b>A catch is counted once, whichever way it came up.</b> Three events say a catch was made,
    /// and one of them also fires for a catch already counted:
    /// <list type="bullet">
    ///   <item><see cref="CatchLanded"/>: a fresh catch came into the player's hand (rod on foot, the
    ///   clam dig). Counted, and remembered as "in hand".</item>
    ///   <item><see cref="FishCaught"/>: a catch entered a hold. Straight from the water (rod at sea, a
    ///   trap or pot haul, a banded lobster, a clam into the pail) it is counted. When it is the hand
    ///   catch being stowed it is NOT counted again: see the handoff below.</item>
    ///   <item><see cref="CatchReleased"/>: a protected catch documented and put back. Counted as caught,
    ///   and as released.</item>
    /// </list></para>
    ///
    /// <para><b>The stow handoff.</b> Every way a catch leaves the hand (stowed, eaten, gifted) raises
    /// <see cref="CatchTakenFromHand"/>, and a stow then raises <see cref="FishCaught"/> for the same item
    /// in the same call (<c>CarryHands.TryGiveCatchTo</c>: hold add, hand cleared, then the event). So a
    /// take forgets the remembered hand catch at once and arms a one-shot handoff stamped with the
    /// current tick (the frame); only a <see cref="FishCaught"/> for the same species and weight on that
    /// SAME tick spends it. A catch eaten or given away therefore leaves nothing behind that could
    /// swallow a later, separate catch.</para>
    ///
    /// <para><b>Bound to one game.</b> Each record carries the session it was made in (the live
    /// <c>SaveData</c> object, which a load or a new game replaces). A record or a read for a different
    /// session starts the log afresh, so one save's catches never show in another's Notebook, whatever
    /// order <see cref="GameLoaded"/> subscribers run in.</para>
    ///
    /// <para><b>Saved with the game (v24).</b> When the session is the live <see cref="SaveData"/>, binding to
    /// it loads the record from <see cref="SaveData.CaughtSpecies"/> and every counted catch writes its row
    /// straight back there (<see cref="CatchRecordSave"/>), so the next save write carries it and a reload
    /// shows the same page. The hand catch and the stow handoff are moment-to-moment and are never saved.
    /// Any other session object (a test's) keeps the record in memory only.</para>
    /// </summary>
    public sealed class CatchRecordLog
    {
        /// <summary>How many hand catches are remembered while still in hand. Hands hold two at most;
        /// every take forgets its own, so this bound only matters if a take were ever missed. Oldest is
        /// forgotten first.</summary>
        public const int MaxPendingHandCatches = 8;

        private readonly List<CaughtSpeciesEntry> _entries = new List<CaughtSpeciesEntry>();
        private readonly List<CatchItem> _inHand = new List<CatchItem>(MaxPendingHandCatches);
        private object _session;

        private bool _handoffArmed;
        private CatchItem _handoff;
        private long _handoffTick;

        /// <summary>Rows in first-caught order. Live: read it, do not keep it.</summary>
        public IReadOnlyList<CaughtSpeciesEntry> Entries => _entries;

        /// <summary>The number of distinct species on the record.</summary>
        public int SpeciesCount => _entries.Count;

        /// <summary>A fresh catch came into the player's hand: count it and remember it until it is
        /// stowed. Returns true only when this call counted a new catch.</summary>
        public bool RecordLandedInHand(in CatchItem item, object session = null)
        {
            BindTo(session);
            if (!Count(in item, released: false)) return false;

            if (_inHand.Count >= MaxPendingHandCatches) _inHand.RemoveAt(0);
            _inHand.Add(item);
            return true;
        }

        /// <summary>A catch left the player's hand, any way (stowed, eaten, gifted). The remembered hand
        /// catch is forgotten; a one-shot handoff is armed for a stow's <see cref="FishCaught"/> on the same
        /// <paramref name="tick"/>. A take of something never counted in hand arms nothing.</summary>
        public void RecordTakenFromHand(in CatchItem item, long tick, object session = null)
        {
            BindTo(session);
            _handoffArmed = false;
            for (int i = 0; i < _inHand.Count; i++)
            {
                if (!SameCatch(_inHand[i], item)) continue;
                _inHand.RemoveAt(i);
                _handoff = item;
                _handoffTick = tick;
                _handoffArmed = true;
                return;
            }
        }

        /// <summary>A catch entered a hold on <paramref name="tick"/>. Counted unless it is the catch the
        /// hand gave up on that same tick (the stow). Returns true only for a new catch.</summary>
        public bool RecordEnteredHold(in CatchItem item, long tick, object session = null)
        {
            BindTo(session);
            bool stow = _handoffArmed && _handoffTick == tick && SameCatch(_handoff, item);
            _handoffArmed = false;
            if (stow) return false;
            return Count(in item, released: false);
        }

        /// <summary>A protected catch was documented and released: caught, and released.</summary>
        public void RecordReleased(in CatchItem item, object session = null)
        {
            BindTo(session);
            Count(in item, released: true);
        }

        /// <summary>The rows for <paramref name="session"/>: the record when it was made in that session,
        /// otherwise empty (and the stale record is dropped).</summary>
        public IReadOnlyList<CaughtSpeciesEntry> EntriesFor(object session)
        {
            BindTo(session);
            return _entries;
        }

        /// <summary>The row for <paramref name="speciesId"/>, if any.</summary>
        public bool TryGet(string speciesId, out CaughtSpeciesEntry entry)
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                if (!string.Equals(_entries[i].SpeciesId, speciesId, System.StringComparison.Ordinal)) continue;
                entry = _entries[i];
                return true;
            }
            entry = default;
            return false;
        }

        /// <summary>Forget everything and unbind.</summary>
        public void Clear()
        {
            _entries.Clear();
            _inHand.Clear();
            _handoffArmed = false;
            _session = null;
        }

        private bool Count(in CatchItem item, bool released)
        {
            if (string.IsNullOrEmpty(item.SpeciesId)) return false;
            int add = released ? 1 : 0;

            for (int i = 0; i < _entries.Count; i++)
            {
                CaughtSpeciesEntry e = _entries[i];
                if (!string.Equals(e.SpeciesId, item.SpeciesId, System.StringComparison.Ordinal)) continue;

                string name = string.IsNullOrEmpty(e.DisplayName) ? item.DisplayName : e.DisplayName;
                float best = item.WeightKg > e.RecordWeightKg ? item.WeightKg : e.RecordWeightKg;
                _entries[i] = new CaughtSpeciesEntry(e.SpeciesId, name, e.Count + 1, e.ReleasedCount + add, best);
                CatchRecordSave.Write(_session as SaveData, _entries[i]);
                return true;
            }

            float weight = item.WeightKg > 0f ? item.WeightKg : 0f;
            _entries.Add(new CaughtSpeciesEntry(item.SpeciesId, item.DisplayName, 1, add, weight));
            CatchRecordSave.Write(_session as SaveData, _entries[_entries.Count - 1]);
            return true;
        }

        /// <summary>The stowed catch is the hand catch when species and weight match exactly: the item is
        /// moved into the hold unchanged, and two separate catches sharing a species and an identical
        /// float weight is not a case worth a second key.</summary>
        private static bool SameCatch(in CatchItem a, in CatchItem b)
            => string.Equals(a.SpeciesId, b.SpeciesId, System.StringComparison.Ordinal)
               && a.WeightKg.Equals(b.WeightKg);

        private void BindTo(object session)
        {
            if (ReferenceEquals(session, _session)) return;
            _entries.Clear();
            _inHand.Clear();
            _handoffArmed = false;
            _session = session;
            CatchRecordSave.Load(session as SaveData, _entries);
        }
    }

    /// <summary>
    /// <b>The catch record's rows in the save</b> (v24, <see cref="SaveData.CaughtSpecies"/>): load them into
    /// a <see cref="CatchRecordLog"/> on bind, write one back each time a catch is counted. Null-safe: a null
    /// save reads as no rows and a write to it does nothing.
    /// </summary>
    public static class CatchRecordSave
    {
        /// <summary>Append the save's rows, in their saved (first-caught) order, to <paramref name="into"/>.
        /// A row with no species or no count is skipped (the migration's heal drops them anyway).</summary>
        public static void Load(SaveData save, List<CaughtSpeciesEntry> into)
        {
            if (save?.CaughtSpecies == null || into == null) return;
            for (int i = 0; i < save.CaughtSpecies.Count; i++)
            {
                CaughtSpeciesDto r = save.CaughtSpecies[i];
                if (r == null || string.IsNullOrEmpty(r.SpeciesId) || r.Count <= 0) continue;
                into.Add(new CaughtSpeciesEntry(r.SpeciesId, r.DisplayName, r.Count, r.ReleasedCount, r.RecordWeightKg));
            }
        }

        /// <summary>Write <paramref name="entry"/> as its species' row: the existing row updated in place, or
        /// a new one appended (so first-caught order is kept).</summary>
        public static void Write(SaveData save, in CaughtSpeciesEntry entry)
        {
            if (save == null || string.IsNullOrEmpty(entry.SpeciesId)) return;
            save.CaughtSpecies ??= new List<CaughtSpeciesDto>();
            CaughtSpeciesDto row = null;
            for (int i = 0; i < save.CaughtSpecies.Count; i++)
            {
                CaughtSpeciesDto r = save.CaughtSpecies[i];
                if (r == null || !string.Equals(r.SpeciesId, entry.SpeciesId, System.StringComparison.Ordinal)) continue;
                row = r;
                break;
            }
            if (row == null)
            {
                row = new CaughtSpeciesDto { SpeciesId = entry.SpeciesId };
                save.CaughtSpecies.Add(row);
            }
            row.DisplayName = entry.DisplayName ?? "";
            row.Count = entry.Count;
            row.ReleasedCount = entry.ReleasedCount;
            row.RecordWeightKg = entry.RecordWeightKg;
        }
    }

    /// <summary>
    /// <b>THE RUNNING GAME'S catch record</b>: one <see cref="CatchRecordLog"/>, fed by
    /// <see cref="CatchLanded"/>, <see cref="FishCaught"/>, <see cref="CatchReleased"/> and
    /// <see cref="CatchTakenFromHand"/> through the
    /// <see cref="EventBus"/>, so no catch path has to know it exists and it never references the Fishing
    /// module (rule 4). Each catch is bound to the live save object.
    ///
    /// <para>Installs itself before the first scene loads; EditMode tests call <see cref="Install"/> and
    /// <see cref="Uninstall"/> themselves.</para>
    /// FLAG lead-architect: new Core record (2026-11-16, the Notebook's catch page).
    /// </summary>
    public static class CatchRecords
    {
        /// <summary>The one record. Never null.</summary>
        public static CatchRecordLog Log { get; } = new CatchRecordLog();

        private static bool _installed;
        private static IslandWeekFactRecorder _islandWeek;

        /// <summary>True while the record is listening.</summary>
        public static bool IsInstalled => _installed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Uninstall();
            Log.Clear();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap() => Install();

        /// <summary>Start listening. Idempotent, and a re-arm: it drops any earlier subscription before
        /// adding one, so calling it again after a test suite's <c>EventBus.Clear</c> makes the record hear
        /// again without ever subscribing twice.</summary>
        public static void Install(IslandWeekRulesDef islandWeekRules = null)
        {
            _installed = true;
            Unsubscribe();
            var rules = islandWeekRules != null ? islandWeekRules : Resources.Load<IslandWeekRulesDef>("IslandWeek");
            _islandWeek = rules != null && rules.ValidationError() == null ? new IslandWeekFactRecorder(rules) : null;
            EventBus.Subscribe<CatchLanded>(OnLanded);
            EventBus.Subscribe<FishCaught>(OnCaught);
            EventBus.Subscribe<CatchReleased>(OnReleased);
            EventBus.Subscribe<CatchTakenFromHand>(OnTaken);
            EventBus.Subscribe<CatchSold>(OnSold);
        }

        /// <summary>Stop listening. Idempotent. The record itself is kept.</summary>
        public static void Uninstall()
        {
            _installed = false;
            Unsubscribe();
            _islandWeek = null;
        }

        /// <summary>The rows for the game being played now (the live save object's session).</summary>
        public static IReadOnlyList<CaughtSpeciesEntry> Current() => Log.EntriesFor(CurrentSession());

        private static void Unsubscribe()
        {
            EventBus.Unsubscribe<CatchLanded>(OnLanded);
            EventBus.Unsubscribe<FishCaught>(OnCaught);
            EventBus.Unsubscribe<CatchReleased>(OnReleased);
            EventBus.Unsubscribe<CatchTakenFromHand>(OnTaken);
            EventBus.Unsubscribe<CatchSold>(OnSold);
        }

        private static void OnLanded(CatchLanded e)
        {
            if (Log.RecordLandedInHand(e.Item, CurrentSession())) RecordIslandCatch(e.Item);
        }
        private static void OnCaught(FishCaught e)
        {
            if (Log.RecordEnteredHold(e.Item, Tick(), CurrentSession())) RecordIslandCatch(e.Item);
        }
        private static void RecordIslandCatch(in CatchItem item)
            => _islandWeek?.RecordKeptCatch(GameServices.Save, in item, GameServices.Helm.PlayersBoatId);
        private static void OnSold(CatchSold e) => _islandWeek?.RecordSale(GameServices.Save, in e);
        private static void OnReleased(CatchReleased e) => Log.RecordReleased(e.Item, CurrentSession());
        private static void OnTaken(CatchTakenFromHand e) => Log.RecordTakenFromHand(e.Item, Tick(), CurrentSession());

        /// <summary>The handoff's tick: the frame. A stow raises its take and its hold entry in one call,
        /// so both land on one frame; an eat or a gift is never followed by a separate identical catch
        /// entering a hold within that same frame.</summary>
        private static long Tick() => Time.frameCount;

        private static object CurrentSession() => GameServices.Save?.Current;
    }
}
