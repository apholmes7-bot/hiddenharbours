using HiddenHarbours.Core;

namespace HiddenHarbours.World
{
    /// <summary>
    /// The VS-08 flag store: backs <see cref="OnboardingFlags"/> with the versioned save file instead of
    /// PlayerPrefs (the consolidation FlagStore.cs anticipated — "a real save file replaces this at
    /// VS-08"). It delegates to <see cref="GameServices.Save"/>, so the opening flags now live in the
    /// same save slot as money/time/fleet and migrate with them.
    ///
    /// <para>When the save service isn't up yet (e.g. a scene opened without the bootstrap, or an EditMode
    /// context where <c>RuntimeInitializeOnLoadMethod</c> doesn't run), it falls back to an in-memory
    /// store so reads/writes are still safe (just not persisted) — same null-tolerant discipline the rest
    /// of the codebase uses for the optional Core seams.</para>
    /// </summary>
    public sealed class SaveFlagStore : IFlagStore
    {
        private readonly InMemoryFlagStore _fallback = new();

        public bool Get(string key)
        {
            var save = GameServices.Save;
            return save != null ? save.GetFlag(key) : _fallback.Get(key);
        }

        public void Set(string key, bool value)
        {
            var save = GameServices.Save;
            if (save != null) save.SetFlag(key, value);
            else _fallback.Set(key, value);
        }
    }
}
