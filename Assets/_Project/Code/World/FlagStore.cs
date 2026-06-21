using System.Collections.Generic;
using UnityEngine;

namespace HiddenHarbours.World
{
    /// <summary>
    /// A minimal persisted boolean store — the backing for onboarding flags (VS-21). Abstracted so
    /// the logic on top (<see cref="OnboardingFlags"/>) is unit-testable with an in-memory store,
    /// and so when the real save system lands (VS-08) it can supply its own implementation without
    /// touching call sites. (Same "seam, not a system" approach as HudStrings/WorldStrings.)
    /// </summary>
    public interface IFlagStore
    {
        bool Get(string key);
        void Set(string key, bool value);
    }

    /// <summary>An in-memory flag store — no persistence. Used by tests and as a safe default.</summary>
    public sealed class InMemoryFlagStore : IFlagStore
    {
        private readonly HashSet<string> _set = new();
        public bool Get(string key) => _set.Contains(key);
        public void Set(string key, bool value) { if (value) _set.Add(key); else _set.Remove(key); }
    }

    /// <summary>
    /// A <see cref="PlayerPrefs"/>-backed flag store: simple, survives quit/reload, and available in
    /// the editor + standalone today (no save system needed yet). Keys are namespaced so they don't
    /// collide with anything else in PlayerPrefs. A real save file replaces this at VS-08.
    /// </summary>
    public sealed class PlayerPrefsFlagStore : IFlagStore
    {
        private readonly string _prefix;

        public PlayerPrefsFlagStore(string prefix = "hh.flag.")
        {
            _prefix = prefix ?? "hh.flag.";
        }

        public bool Get(string key) => PlayerPrefs.GetInt(_prefix + key, 0) != 0;

        public void Set(string key, bool value)
        {
            PlayerPrefs.SetInt(_prefix + key, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }
}
