using System.Collections.Generic;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// One protected species the player documented and put back: how many, and when first and last.
    /// A row of <see cref="ReleasedCatchLog"/>; what the Notebook's released page reads.
    /// </summary>
    public readonly struct ReleasedCatchEntry
    {
        /// <summary>The species' stable Def id (<c>fish.*</c>), the key a row is counted under.</summary>
        public readonly string SpeciesId;

        /// <summary>The name the catch carried when it was released (<see cref="CatchItem.DisplayName"/>),
        /// so the page never has to ask the Fishing module what a species is called (rule 4).</summary>
        public readonly string DisplayName;

        /// <summary>How many of this species have been released. Always at least 1 on a stored row.</summary>
        public readonly int Count;

        /// <summary>Master-clock seconds of the first release (<see cref="IGameClock.TotalSeconds"/>).</summary>
        public readonly double FirstSeconds;

        /// <summary>Master-clock seconds of the latest release.</summary>
        public readonly double LastSeconds;

        public ReleasedCatchEntry(string speciesId, string displayName, int count,
                                  double firstSeconds, double lastSeconds)
        {
            SpeciesId = speciesId ?? "";
            DisplayName = displayName ?? "";
            Count = count;
            FirstSeconds = firstSeconds;
            LastSeconds = lastSeconds;
        }
    }

    /// <summary>
    /// <b>THE RECORD OF WHAT WAS LET GO</b>: one row per protected species released, in the order each
    /// was first released. Pure and engine-free so it is provable headlessly; <see cref="ReleasedCatches"/>
    /// is what feeds it from <see cref="CatchReleased"/> in a running game.
    ///
    /// <para><b>Bound to one game.</b> Each record carries the session it was made in (the live
    /// <c>SaveData</c> object, which a load or a new game replaces). A record or a read for a different
    /// session starts the log afresh, so one save's releases never show in another's Notebook, and
    /// the answer does not depend on the order <see cref="GameLoaded"/> subscribers run in (a hold's
    /// restore can itself release a protected fish inside that dispatch).</para>
    ///
    /// <para><b>In-session only.</b> Nothing here is written to the save yet; persistence needs a
    /// SaveData field and a schema step, which is owed separately.</para>
    /// </summary>
    public sealed class ReleasedCatchLog
    {
        private readonly List<ReleasedCatchEntry> _entries = new List<ReleasedCatchEntry>();
        private object _session;

        /// <summary>Rows in first-release order. Live: read it, do not keep it.</summary>
        public IReadOnlyList<ReleasedCatchEntry> Entries => _entries;

        /// <summary>The number of distinct species on the record.</summary>
        public int SpeciesCount => _entries.Count;

        /// <summary>
        /// Count one released catch at <paramref name="gameSeconds"/>. A catch with no species id is
        /// ignored (nothing to file it under). <paramref name="session"/> different from the one the log
        /// holds clears it first (see the type summary); null is a session like any other.
        /// </summary>
        public void Record(in CatchItem item, double gameSeconds, object session = null)
        {
            BindTo(session);
            if (string.IsNullOrEmpty(item.SpeciesId)) return;

            for (int i = 0; i < _entries.Count; i++)
            {
                ReleasedCatchEntry e = _entries[i];
                if (!string.Equals(e.SpeciesId, item.SpeciesId, System.StringComparison.Ordinal)) continue;

                string name = string.IsNullOrEmpty(e.DisplayName) ? item.DisplayName : e.DisplayName;
                double first = gameSeconds < e.FirstSeconds ? gameSeconds : e.FirstSeconds;
                double last = gameSeconds > e.LastSeconds ? gameSeconds : e.LastSeconds;
                _entries[i] = new ReleasedCatchEntry(e.SpeciesId, name, e.Count + 1, first, last);
                return;
            }

            _entries.Add(new ReleasedCatchEntry(item.SpeciesId, item.DisplayName, 1, gameSeconds, gameSeconds));
        }

        /// <summary>The rows for <paramref name="session"/>: the record when it was made in that session,
        /// otherwise empty (and the stale record is dropped).</summary>
        public IReadOnlyList<ReleasedCatchEntry> EntriesFor(object session)
        {
            BindTo(session);
            return _entries;
        }

        /// <summary>The row for <paramref name="speciesId"/>, if any.</summary>
        public bool TryGet(string speciesId, out ReleasedCatchEntry entry)
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
            _session = null;
        }

        private void BindTo(object session)
        {
            if (ReferenceEquals(session, _session)) return;
            _entries.Clear();
            _session = session;
        }
    }

    /// <summary>
    /// <b>THE RUNNING GAME'S released record</b>: one <see cref="ReleasedCatchLog"/>, fed by the
    /// <see cref="CatchReleased"/> event through the <see cref="EventBus"/>, so no landing path has to know
    /// it exists and it never references the Fishing module (rule 4).
    ///
    /// <para>Installs itself before the first scene loads; EditMode tests call <see cref="Install"/> and
    /// <see cref="Uninstall"/> themselves. Each release is stamped with the master clock
    /// (<see cref="GameServices.Clock"/>, 0 when there is none) and bound to the live save object.</para>
    /// FLAG lead-architect: new Core record (2026-11-16, the Notebook lists protected fish released).
    /// </summary>
    public static class ReleasedCatches
    {
        /// <summary>The one record. Never null.</summary>
        public static ReleasedCatchLog Log { get; } = new ReleasedCatchLog();

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
            EventBus.Unsubscribe<CatchReleased>(OnReleased);
            EventBus.Subscribe<CatchReleased>(OnReleased);
        }

        /// <summary>Stop listening. Idempotent. The record itself is kept.</summary>
        public static void Uninstall()
        {
            _installed = false;
            EventBus.Unsubscribe<CatchReleased>(OnReleased);
        }

        /// <summary>The rows for the game being played now (the live save object's session).</summary>
        public static IReadOnlyList<ReleasedCatchEntry> Current() => Log.EntriesFor(CurrentSession());

        private static void OnReleased(CatchReleased e)
        {
            IGameClock clock = GameServices.Clock;
            // A clock is often a MonoBehaviour: a destroyed one is Unity's fake-null, which `?.` would
            // sail past, so ask Unity's own equality first.
            if (clock is UnityEngine.Object producer && producer == null) clock = null;
            double seconds = clock != null ? clock.TotalSeconds : 0d;
            Log.Record(e.Item, seconds, CurrentSession());
        }

        private static object CurrentSession() => GameServices.Save?.Current;
    }
}
