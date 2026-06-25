namespace HiddenHarbours.Core
{
    /// <summary>
    /// The Core contract for the running save system. Feature modules talk to it through
    /// <see cref="GameServices.Save"/> rather than the concrete <c>SaveService</c>, so (per
    /// tech-architecture.md §10) a cloud or binary backend can slot in behind the same interface.
    ///
    /// <para>It owns the in-memory <see cref="Current"/> blob, persists flags on behalf of the world
    /// (the VS-08 consolidation of the VS-21 onboarding flags off PlayerPrefs), and writes to disk on
    /// demand and on app suspend/quit.</para>
    /// </summary>
    public interface ISaveService
    {
        /// <summary>The live save blob — loaded on launch, kept up to date in memory, written on save.
        /// Never null once the service is running. Consumers may read it to restore their own state.</summary>
        SaveData Current { get; }

        /// <summary>True iff <see cref="Current"/> was read from an existing save file on disk (a resumed
        /// game), rather than freshly minted for a new game. The load-restore path (<see cref="SaveRestore"/>)
        /// uses this to decide whether to seek the clock to the saved time: a NEW game must keep its authored
        /// start hour (its blob's gameTime is 0), so only a resumed game seeks. False until the service has
        /// loaded (e.g. EditMode before bootstrap).
        /// <para><b>Additive &amp; non-breaking:</b> a default interface property (defaults to <c>false</c>)
        /// so existing test fakes implementing <see cref="ISaveService"/> compile unchanged; the real
        /// <c>SaveService</c> overrides it.</para></summary>
        bool LoadedExistingSave => false;

        /// <summary>Read a persisted boolean flag by stable key (backs the world's onboarding flags).</summary>
        bool GetFlag(string key);

        /// <summary>Set a persisted boolean flag by stable key and persist it. No-op on a null/empty key.</summary>
        void SetFlag(string key, bool value);

        /// <summary>Snapshot the live services into <see cref="Current"/> and write it to disk now.</summary>
        void Save();
    }
}
