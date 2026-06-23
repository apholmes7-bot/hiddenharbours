using System;
using System.Collections.Generic;

namespace HiddenHarbours.Core
{
    /// <summary>
    /// The save schema v1 DTO (VS-08). A plain, JsonUtility-serializable container for the slice of
    /// world state that can't be recomputed — everything else (tide/wind/weather, authored geometry,
    /// dormant NPCs) is regenerated from <see cref="WorldSeed"/> + <see cref="GameTimeSeconds"/> at
    /// load, per data-model.md §4 and tech-architecture.md §6. Keep this a dumb data bag: no behaviour,
    /// public fields only (JsonUtility serializes public fields), one canonical version per release.
    ///
    /// <para>Schema evolution is append-only and goes through <see cref="SaveMigration"/>: add a field,
    /// bump <see cref="SaveMigration.CurrentVersion"/>, and teach the migrator to fill it for older
    /// saves. Never rename or repurpose a shipped field (that breaks existing saves) — add a new one and
    /// migrate, the same append-only rule Def ids follow (data-model.md §5).</para>
    /// </summary>
    [Serializable]
    public sealed class SaveData
    {
        /// <summary>Schema version this blob was written at. Drives <see cref="SaveMigration"/>.</summary>
        public int SchemaVersion;

        /// <summary>The world's deterministic seed — the sim recomputes tide/wind/weather from this.</summary>
        public int WorldSeed;

        /// <summary>The master clock, in in-game seconds (a <c>double</c>, matching <see cref="GameTime"/>).
        /// Stored at full precision so a mid-minute save reloads to the same instant.</summary>
        public double GameTimeSeconds;

        /// <summary>The player's purse (₲).</summary>
        public int Money;

        /// <summary>Absolute day counter since a new game began (derived from the clock; see
        /// <see cref="IGameClock.DayIndex"/>). Saved for convenience/UI; the clock is the source of truth.</summary>
        public int DayIndex;

        /// <summary>Stable hull ids of every boat the player owns (e.g. "boat.dory", "boat.punt").
        /// Order is the order they were acquired. Added in v1.</summary>
        public List<string> OwnedBoats = new();

        /// <summary>Stable hull id of the active boat (e.g. "boat.punt"); empty when none is aboard yet.
        /// Always also present in <see cref="OwnedBoats"/>. Added in v1.</summary>
        public string ActiveHullId = "";

        /// <summary>The opening-sequence (and other story) flags, consolidated here from PlayerPrefs
        /// (VS-08 absorbs the VS-21 onboarding flags). Added in v1.</summary>
        public List<SaveFlag> OnboardingFlags = new();

        /// <summary>Stable ids of every license the player holds (e.g. "license.cod") — the license
        /// wallet for the St Peters opening (progression-and-housing §2.2). Append-only; order is
        /// acquisition order. Backs <c>ILicenseService</c>. Added in v2.</summary>
        public List<string> OwnedLicenses = new();

        /// <summary>Per-boat repair state: the stable hull ids the player has PAID THE SHIPWRIGHT TO
        /// REPAIR. A boat bought damaged sits in <see cref="OwnedBoats"/> but is unusable until its id
        /// also appears here. (A boat bought already-usable is repaired on grant, so it appears in
        /// both.) Added in v2 for the damaged-dory buy+repair flow.</summary>
        public List<string> RepairedBoats = new();

        /// <summary>Stable ids of every gear/equipment item the player owns (e.g. "gear.rod",
        /// "gear.shovel"). gameplay-systems maps an owned id to its Gear capability; Economy only records
        /// the purchase. Append-only; acquisition order. Added in v2.</summary>
        public List<string> OwnedGear = new();
    }

    /// <summary>
    /// One persisted boolean flag, keyed by a stable string id (e.g. "met_ginny"). A list of these is
    /// JsonUtility-friendly where a Dictionary is not, and stays readable in the on-disk JSON.
    /// </summary>
    [Serializable]
    public struct SaveFlag
    {
        public string Key;
        public bool Value;

        public SaveFlag(string key, bool value)
        {
            Key = key;
            Value = value;
        }
    }
}
