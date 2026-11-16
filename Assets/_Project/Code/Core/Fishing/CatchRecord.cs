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
    ///   <item><see cref="CatchLanded"/>: a fresh catch came into her hand (rod on foot, the clam dig).
    ///   Counted, and remembered as "in hand".</item>
    ///   <item><see cref="FishCaught"/>: a catch entered a hold. Straight from the water (rod at sea, a
    ///   trap or pot haul, a banded lobster, a clam into the pail) it is counted; when it is the hand
    ///   catch being stowed (the same item: <c>CarryHands</c> moves it, never re-stamps it) it matches the
    ///   remembered hand catch and is NOT counted again.</item>
    ///   <item><see cref="CatchReleased"/>: a protected catch documented and put back. Counted as caught,
    ///   and as released.</item>
    /// </list></para>
    ///
    /// <para><b>Bound to one game.</b> Each record carries the session it was made in (the live
    /// <c>SaveData</c> object, which a load or a new game replaces). A record or a read for a different
    /// session starts the log afresh, so one save's catches never show in another's Notebook, whatever
    /// order <see cref="GameLoaded"/> subscribers run in.</para>
    ///
    /// <para><b>In-session only.</b> Nothing here is written to the save yet; persistence needs a
    /// SaveData list and a schema step, which is owed separately.</para>
    /// </summary>
    public sealed class CatchRecordLog
    {
        /// <summary>How many hand catches are remembered while waiting to be stowed. Hands hold two at
        /// most; the slack covers a catch that left the hand some other way (sold, given, dropped) and so
        /// will never be stowed. Oldest is forgotten first.</summary>
        public const int MaxPendingHandCatches = 8;

        private readonly List<CaughtSpeciesEntry> _entries = new List<CaughtSpeciesEntry>();
        private readonly List<CatchItem> _inHand = new List<CatchItem>(MaxPendingHandCatches);
        private object _session;

        /// <summary>Rows in first-caught order. Live: read it, do not keep it.</summary>
        public IReadOnlyList<CaughtSpeciesEntry> Entries => _entries;

        /// <summary>The number of distinct species on the record.</summary>
        public int SpeciesCount => _entries.Count;

        /// <summary>A fresh catch came into her hand: count it and remember it until it is stowed.</summary>
        public void RecordLandedInHand(in CatchItem item, object session = null)
        {
            BindTo(session);
            if (!Count(in item, released: false)) return;

            if (_inHand.Count >= MaxPendingHandCatches) _inHand.RemoveAt(0);
            _inHand.Add(item);
        }

        /// <summary>A catch entered a hold. Counted unless it is a remembered hand catch being stowed.</summary>
        public void RecordEnteredHold(in CatchItem item, object session = null)
        {
            BindTo(session);
            for (int i = 0; i < _inHand.Count; i++)
            {
                if (!SameCatch(_inHand[i], item)) continue;
                _inHand.RemoveAt(i);
                return;
            }
            Count(in item, released: false);
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
                return true;
            }

            float weight = item.WeightKg > 0f ? item.WeightKg : 0f;
            _entries.Add(new CaughtSpeciesEntry(item.SpeciesId, item.DisplayName, 1, add, weight));
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
            _session = session;
        }
    }

    /// <summary>
    /// <b>THE RUNNING GAME'S catch record</b>: one <see cref="CatchRecordLog"/>, fed by
    /// <see cref="CatchLanded"/>, <see cref="FishCaught"/> and <see cref="CatchReleased"/> through the
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
        public static void Install()
        {
            _installed = true;
            Unsubscribe();
            EventBus.Subscribe<CatchLanded>(OnLanded);
            EventBus.Subscribe<FishCaught>(OnCaught);
            EventBus.Subscribe<CatchReleased>(OnReleased);
        }

        /// <summary>Stop listening. Idempotent. The record itself is kept.</summary>
        public static void Uninstall()
        {
            _installed = false;
            Unsubscribe();
        }

        /// <summary>The rows for the game being played now (the live save object's session).</summary>
        public static IReadOnlyList<CaughtSpeciesEntry> Current() => Log.EntriesFor(CurrentSession());

        private static void Unsubscribe()
        {
            EventBus.Unsubscribe<CatchLanded>(OnLanded);
            EventBus.Unsubscribe<FishCaught>(OnCaught);
            EventBus.Unsubscribe<CatchReleased>(OnReleased);
        }

        private static void OnLanded(CatchLanded e) => Log.RecordLandedInHand(e.Item, CurrentSession());
        private static void OnCaught(FishCaught e) => Log.RecordEnteredHold(e.Item, CurrentSession());
        private static void OnReleased(CatchReleased e) => Log.RecordReleased(e.Item, CurrentSession());

        private static object CurrentSession() => GameServices.Save?.Current;
    }
}
