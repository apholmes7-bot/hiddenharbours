namespace HiddenHarbours.Core
{
    /// <summary>
    /// Forward-only schema migration for <see cref="SaveData"/> (tech-architecture.md §6: "versioned +
    /// migratable"). Every loaded save is run through <see cref="Migrate"/> before the game touches it,
    /// so an older blob is upgraded in memory to the current shape rather than crashing the load.
    ///
    /// <para>The contract: a migration step only *adds* — it fills fields that didn't exist in the older
    /// version and bumps the version. It never reinterprets existing values. v0→v1 is therefore a
    /// no-op upgrade: a pre-v1 save had no owned-boats / flags lists, so we just give it empty ones and
    /// stamp it v1; its money/seed/time carry through untouched.</para>
    /// </summary>
    public static class SaveMigration
    {
        /// <summary>The schema version this build writes. Bump when you add a field + a migration step.</summary>
        public const int CurrentVersion = 2;

        /// <summary>A fresh save for a brand-new game — current version, empty collections.</summary>
        public static SaveData NewGame() => new SaveData { SchemaVersion = CurrentVersion };

        /// <summary>
        /// Upgrade <paramref name="data"/> in place to <see cref="CurrentVersion"/> and return it.
        /// Null-safe field repair runs unconditionally so even a current-version blob that arrived with
        /// a missing JSON field (JsonUtility leaves absent reference fields null) is left usable.
        /// Returns null only if given null (the caller treats "no data" as "start a new game").
        /// </summary>
        public static SaveData Migrate(SaveData data)
        {
            if (data == null) return null;

            // ---- v0 → v1: the owned-fleet + flags lists are new in v1. Older saves simply lacked
            // them; give them empty containers and stamp v1. Scalar state (seed/time/money) is kept.
            if (data.SchemaVersion < 1)
                data.SchemaVersion = 1;

            // ---- v1 → v2: the license wallet, per-boat repair state, and owned-gear wallet are new in
            // v2 (St Peters opening). An older save simply had no licenses/gear and no damaged boats to
            // repair; the null-repair below gives them empty lists. A pre-v2 owned boat was always a
            // usable boat, so we mark every already-owned boat repaired — it stays usable after upgrade.
            if (data.SchemaVersion < 2)
            {
                data.RepairedBoats ??= new System.Collections.Generic.List<string>();
                if (data.OwnedBoats != null)
                    foreach (var id in data.OwnedBoats)
                        if (!string.IsNullOrEmpty(id) && !data.RepairedBoats.Contains(id))
                            data.RepairedBoats.Add(id);
                data.SchemaVersion = 2;
            }

            // ---- future steps go here, each guarded by `if (data.SchemaVersion < N)` and bumping to N.

            // Defensive null-repair (a hand-edited or partial JSON can omit reference-typed fields).
            data.OwnedBoats ??= new System.Collections.Generic.List<string>();
            data.OnboardingFlags ??= new System.Collections.Generic.List<SaveFlag>();
            data.OwnedLicenses ??= new System.Collections.Generic.List<string>();
            data.RepairedBoats ??= new System.Collections.Generic.List<string>();
            data.OwnedGear ??= new System.Collections.Generic.List<string>();
            data.ActiveHullId ??= "";

            // Clamp to the version we actually understand (never claim to be newer than this build).
            if (data.SchemaVersion > CurrentVersion)
                data.SchemaVersion = CurrentVersion;

            return data;
        }
    }
}
