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

        /// <summary>Every persistent object the player has DROPPED INTO THE WORLD — currently just placed
        /// traps (the first world-placed object; trap-fishing arc Build 0). Each entry is the irreducible
        /// PLACEMENT record only: the trap's soak progress and contents are recomputed from
        /// <see cref="WorldSeed"/> + <see cref="GameTimeSeconds"/> + the placement facts, never stored
        /// (rule 5 / determinism — the same discipline that keeps tide/wind/weather out of the save).
        /// UNUSED until the trap runtime lands (arc Build 3); this field is the durable groundwork, inert
        /// like <c>Fishing.Gear.Trap</c> until a consumer exists. Append-only; order is placement order.
        /// Added in v3. (ADR 0020, extending ADR 0008; diegetic-ui-and-inventory §4.3.)</summary>
        public List<PlacedTrapDto> PlacedTraps = new();

        /// <summary>Bait the player owns, as COUNTED stock (unlike <see cref="OwnedGear"/>, which is a
        /// presence-only wallet — bait is consumable, so it needs a quantity). One record per bait kind.
        /// UNUSED until the trap runtime lands (arc Build 3). Added in v3. (ADR 0020.)</summary>
        public List<BaitStock> BaitStock = new();

        /// <summary>Pots/traps the player OWNS, as counted stock keyed by stable TrapDef id (e.g.
        /// "trap.lobster") — the physical gear inventory behind the trap loop's P2 money wheel: pots are
        /// BOUGHT at the shipwright, not conjured. This is the OWNED total; how many are free to set is
        /// DERIVED (owned − deployed-in-<see cref="PlacedTraps"/> − aboard-the-deck) by
        /// <see cref="PotLocker"/>, never stored — the same recompute-don't-store discipline as the soak
        /// (rule 5). One record per trap kind. Added in v4. (ADR 0020 addendum.)</summary>
        public List<PotStock> PotStock = new();
    }

    /// <summary>
    /// The persisted PLACEMENT record for one world-placed trap — the irreducible facts that cannot be
    /// recomputed (a player's placement choice), and nothing else. The trap's soak progress and catch are
    /// a deterministic function of <see cref="SaveData.WorldSeed"/> + <see cref="PlacementGameTimeSeconds"/>
    /// → now + the trap/bait Defs, so they are RECOMPUTED, never stored (rule 5), exactly as tide/wind are.
    ///
    /// <para>Shaped as a special case of the future world-placed-CONTAINER record
    /// (diegetic-ui-and-inventory §4.3): a stable instance id, a Def id, a position, a region, and a
    /// placement time are the same skeleton a placed bucket/rack would carry — a later container ADR can
    /// generalize this, but we keep it concrete and minimal now (ADR 0020, no generic system yet).</para>
    /// </summary>
    [Serializable]
    public struct PlacedTrapDto
    {
        /// <summary>Stable id UNIQUE PER PLACED INSTANCE (many traps of the same kind can be down at once).
        /// The sim keys this trap's deterministic soak stream on this id, so it must survive save/load.</summary>
        public string InstanceId;

        /// <summary>Stable Def id of the trap KIND (e.g. "trap.lobster_pot"), resolved against the
        /// ContentDatabase at load. The save carries the reference, never the trap's stats (rules 2/6).</summary>
        public string TrapDefId;

        /// <summary>World X position. Stored as two flat floats (not a Vector2) to keep the JSON clean and
        /// human-readable; <see cref="SaveData"/> stores no vectors, so this sets the flat-scalar precedent.</summary>
        public float PosX;

        /// <summary>World Y position (see <see cref="PosX"/>).</summary>
        public float PosY;

        /// <summary>Stable Def id of the bait loaded (e.g. "bait.herring"), or empty for an unbaited trap.
        /// What's baited drives what soaks — an irreducible placement fact, not derivable.</summary>
        public string BaitId;

        /// <summary>The game-clock instant the trap was placed, at full precision (matching
        /// <see cref="SaveData.GameTimeSeconds"/>). The anchor the deterministic soak is computed FROM;
        /// storing the anchor, not the result, is what keeps the catch recomputable (rule 5).</summary>
        public double PlacementGameTimeSeconds;

        /// <summary>Region id the trap lives in (scene-per-region, ADR 0004), so a trap in an unloaded
        /// region is still recorded and restored when that region loads.</summary>
        public string Region;
    }

    /// <summary>
    /// One bait kind the player owns, with a quantity. Bait is consumable (spent to arm a trap), so unlike
    /// the presence-only <see cref="SaveData.OwnedGear"/> wallet it carries a <see cref="Count"/>. A list of
    /// these is JsonUtility-friendly where a Dictionary is not (same reason <see cref="SaveFlag"/> is).
    /// </summary>
    [Serializable]
    public struct BaitStock
    {
        /// <summary>Stable bait Def id (e.g. "bait.herring").</summary>
        public string BaitId;

        /// <summary>How many of this bait the player holds.</summary>
        public int Count;

        public BaitStock(string baitId, int count)
        {
            BaitId = baitId;
            Count = count;
        }
    }

    /// <summary>
    /// One pot/trap kind the player owns, with a quantity — the counted-gear twin of
    /// <see cref="BaitStock"/> (pots are finite physical stock, so like bait they carry a
    /// <see cref="Count"/>, not just presence). Keyed by the stable TrapDef id, never the offer id —
    /// the save records WHAT you own; where you bought it is the economy's business. A list of these is
    /// JsonUtility-friendly where a Dictionary is not (the <see cref="SaveFlag"/> reason).
    /// </summary>
    [Serializable]
    public struct PotStock
    {
        /// <summary>Stable trap Def id (e.g. "trap.lobster").</summary>
        public string TrapDefId;

        /// <summary>How many of this pot kind the player OWNS (deployed + aboard + spare — the physical
        /// total; the free-to-set number is derived by <see cref="PotLocker"/>).</summary>
        public int Count;

        public PotStock(string trapDefId, int count)
        {
            TrapDefId = trapDefId;
            Count = count;
        }
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
