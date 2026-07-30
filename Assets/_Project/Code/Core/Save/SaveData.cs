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

        /// <summary>The BOAT hold's catch (M1 §7.3 — freshness must survive a save). One record per run
        /// of identical units: species id, quantity, and the three <c>FreshnessState</c> fields — the
        /// save carries the species REFERENCE and the clock, never the Def's stats (rule 2); the item
        /// is rebuilt from the Def at load. Spoil stays a pure function of <c>(state, now)</c>, so the
        /// restored numbers land exactly (the #289 settle-on-read guarantee). Added in v5.</summary>
        public List<HeldCatchDto> BoatHoldCatch = new();

        /// <summary>The hand-carried clam PAIL's catch — same record shape as
        /// <see cref="BoatHoldCatch"/>, its own list because it is its own container (a clam dug on
        /// the flats must reload into your hand, not the boat). Added in v5.</summary>
        public List<HeldCatchDto> BucketCatch = new();

        /// <summary>Ginny's FREEZER's catch (the third hold the §7.3 slice created — banked catch
        /// must not evaporate on reload). Same record shape. Added in v5.</summary>
        public List<HeldCatchDto> FreezerCatch = new();

        /// <summary>Consumable SUPPLIES the player owns, as counted stock keyed by a stable SupplyDef id
        /// (e.g. "supply.ice") — the third counted wallet beside <see cref="BaitStock"/> and
        /// <see cref="PotStock"/>, read and written only through <see cref="SupplyLocker"/>.
        ///
        /// <para><b>Why its own list and not more gear.</b> <see cref="OwnedGear"/> is presence-only ("you
        /// have a rod or you don't"); a bag of ice is spent. Bait's list is species-facing and keyed by
        /// BaitId, so ice does not belong in it either. This one list is deliberately GENERIC so the rest of
        /// the §7.3 cold chain (lids, and later fuel cans) lands as new Def assets rather than new save
        /// fields — one schema bump for the shape, none for the content. Added in v7 (plan-to-m1 §7.5).</para></summary>
        public List<SupplyStock> SupplyStock = new();
    }

    /// <summary>
    /// One run of identical held catch: a species (by stable id — the Def is resolved at load, rule 2),
    /// how many, and the freshness clock all of them share (<c>FreshnessState</c>'s three fields, flat
    /// scalars for clean JSON — the <see cref="PlacedTrapDto"/> precedent). Units landed at different
    /// instants carry different clocks and therefore occupy different records; <see cref="Quantity"/>
    /// compresses the common same-haul case without ever merging distinct freshness.
    /// </summary>
    [Serializable]
    public struct HeldCatchDto
    {
        /// <summary>Stable species Def id (e.g. "fish.atlantic_cod"), resolved at load.</summary>
        public string SpeciesId;

        /// <summary>How many units share this exact freshness state (≥ 1).</summary>
        public int Quantity;

        /// <summary>Spoil banked as of <see cref="LastSettleGameSeconds"/> (FreshnessState.SpoilAccrued).</summary>
        public float SpoilAccrued;

        /// <summary>The clock instant the banked spoil is correct at (FreshnessState.LastSettleGameSeconds),
        /// full precision like <see cref="SaveData.GameTimeSeconds"/>.</summary>
        public double LastSettleGameSeconds;

        /// <summary>The <c>StorageMode</c> held since, as its stable int value (append-only enum).</summary>
        public int Mode;

        public HeldCatchDto(string speciesId, int quantity, float spoilAccrued,
                            double lastSettleGameSeconds, int mode)
        {
            SpeciesId = speciesId;
            Quantity = quantity;
            SpoilAccrued = spoilAccrued;
            LastSettleGameSeconds = lastSettleGameSeconds;
            Mode = mode;
        }

        /// <summary>This record's clock as the runtime <see cref="FreshnessState"/>.</summary>
        public FreshnessState Freshness => new FreshnessState(SpoilAccrued, LastSettleGameSeconds,
                                                              (StorageMode)Mode);
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
    /// One consumable supply the player owns, with a quantity — the generic counted-stock sibling of
    /// <see cref="BaitStock"/>/<see cref="PotStock"/>, keyed by the stable SupplyDef id (e.g. "supply.ice").
    /// Generic on purpose: ice is the first, lids are next (§7.3's cold chain), and each is a Def asset, not
    /// another save field. A list of these is JsonUtility-friendly where a Dictionary is not (the
    /// <see cref="SaveFlag"/> reason).
    /// </summary>
    [Serializable]
    public struct SupplyStock
    {
        /// <summary>Stable supply Def id (e.g. "supply.ice").</summary>
        public string SupplyId;

        /// <summary>How many of this supply the player holds.</summary>
        public int Count;

        public SupplyStock(string supplyId, int count)
        {
            SupplyId = supplyId;
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
