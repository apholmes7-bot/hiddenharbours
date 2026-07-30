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
        public const int CurrentVersion = 7;

        /// <summary>
        /// The region id Port Greywick was saved under before it was renamed Nine Mile Creek, and the id
        /// it is rewritten to. Public so the test can state the pair it is guarding rather than
        /// re-typing two magic strings that could drift apart from the migration silently.
        /// </summary>
        public const string RenamedRegionFrom = "region.port_greywick";
        public const string RenamedRegionTo = "region.nine_mile_creek";

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

            // ---- v2 → v3: world-placed persistent objects (the placed-traps list) and counted bait stock
            // are new in v3 (trap-fishing arc Build 0 — the save groundwork so a dropped trap survives
            // save/load once Build 3's runtime lands). An older save had no placed traps and no bait; the
            // null-repair below gives them empty lists. Only PLACEMENT facts are ever stored — soak/contents
            // recompute from seed+time (rule 5), so there is nothing else to fill. (ADR 0020.)
            if (data.SchemaVersion < 3)
            {
                data.PlacedTraps ??= new System.Collections.Generic.List<PlacedTrapDto>();
                data.BaitStock ??= new System.Collections.Generic.List<BaitStock>();
                data.SchemaVersion = 3;
            }

            // ---- v3 → v4: pots become OWNED, counted stock (the shipwright sells them; T spends them —
            // ADR 0020 addendum). The new list starts empty, then ADOPTS the pots already in the water:
            // pre-v4 pots were conjured free, but the ones a player has deployed are physically theirs —
            // counting each PlacedTraps record into owned means the availability derivation
            // (owned − deployed − aboard, PotLocker) starts at exactly zero spare instead of negative,
            // and hauling a pre-v4 pot returns it to the locker like any bought pot. Nobody's set gear is
            // confiscated by the update. (The cozy STARTER KIT — a couple of fresh pots — is data, not
            // schema: granted once, flag-guarded, by Economy's StartingPots off GameConfig.StarterPotKit,
            // the StartingGear/StartingBait pattern. The migration stays pure and asset-free.)
            if (data.SchemaVersion < 4)
            {
                data.PotStock ??= new System.Collections.Generic.List<PotStock>();
                if (data.PlacedTraps != null)
                {
                    for (int i = 0; i < data.PlacedTraps.Count; i++)
                    {
                        string kind = data.PlacedTraps[i].TrapDefId;
                        if (!string.IsNullOrEmpty(kind)) PotLocker.AddOwned(data, kind, 1);
                    }
                }
                data.SchemaVersion = 4;
            }

            // ---- v4 → v5: Port Greywick was RENAMED Nine Mile Creek (plan-to-m1 D1/§7.10), and its
            // stable region id went with it. Placed traps are the only save field that stores a region
            // id as a STRING (PlacedTrapDto.Region — scene-per-region, ADR 0004), so a trap dropped
            // before the rename would name a region no def claims: it would load, sit in an unloadable
            // region, and never be haulable again. Rewriting the id here is the whole migration.
            //
            // ⚠ THIS IS THE FIRST STEP THAT REINTERPRETS AN EXISTING VALUE rather than only adding, so
            // it is worth saying why that is legitimate: the value's REFERENT was renamed, not its
            // meaning. The trap is in the same water it was always in. The step is narrow by
            // construction — it touches exactly one field, matches exactly one id, and leaves every
            // other region string alone.
            if (data.SchemaVersion < 5)
            {
                if (data.PlacedTraps != null)
                {
                    for (int i = 0; i < data.PlacedTraps.Count; i++)
                    {
                        if (!string.Equals(data.PlacedTraps[i].Region, RenamedRegionFrom,
                                           System.StringComparison.Ordinal))
                            continue;
                        PlacedTrapDto dto = data.PlacedTraps[i];
                        dto.Region = RenamedRegionTo;
                        data.PlacedTraps[i] = dto;   // PlacedTrapDto is a struct — write it back
                    }
                }
                data.SchemaVersion = 5;
            }

            // ---- v5 → v6: held catch persists (M1 §7.3 — freshness must survive a save). Three lists,
            // one per container (boat hold / clam pail / Ginny's freezer), each record = species id +
            // quantity + the FreshnessState triple. A pre-v6 save simply carried no hold contents — it
            // gets empty lists (an empty hold, never a crash) and everything else carries through.
            // (Authored as v4→v5 in the same hours #324's rename step CLAIMED v5 on main; renumbered
            // to v6 at merge — two lanes, one version counter.)
            if (data.SchemaVersion < 6)
            {
                data.BoatHoldCatch ??= new System.Collections.Generic.List<HeldCatchDto>();
                data.BucketCatch ??= new System.Collections.Generic.List<HeldCatchDto>();
                data.FreezerCatch ??= new System.Collections.Generic.List<HeldCatchDto>();
                data.SchemaVersion = 6;
            }

            // ---- v6 → v7: consumable SUPPLIES become counted stock (plan-to-m1 §7.5 — the island general
            // store stocks ice, and §7.3's cold chain adds lids after it). One generic list keyed by stable
            // SupplyDef id, so every further consumable is a Def asset rather than another schema bump. A
            // pre-v7 save simply owned no supplies: it gets an empty list and everything else carries
            // through. Nothing is reinterpreted. (The Ginny fronted-fee beat that ships alongside needed no
            // field at all — it rides the existing OnboardingFlags store, the StartingPots precedent.)
            if (data.SchemaVersion < 7)
            {
                data.SupplyStock ??= new System.Collections.Generic.List<SupplyStock>();
                data.SchemaVersion = 7;
            }

            // ---- future steps go here, each guarded by `if (data.SchemaVersion < N)` and bumping to N.

            // Defensive null-repair (a hand-edited or partial JSON can omit reference-typed fields).
            data.OwnedBoats ??= new System.Collections.Generic.List<string>();
            data.OnboardingFlags ??= new System.Collections.Generic.List<SaveFlag>();
            data.OwnedLicenses ??= new System.Collections.Generic.List<string>();
            data.RepairedBoats ??= new System.Collections.Generic.List<string>();
            data.OwnedGear ??= new System.Collections.Generic.List<string>();
            data.PlacedTraps ??= new System.Collections.Generic.List<PlacedTrapDto>();
            data.BaitStock ??= new System.Collections.Generic.List<BaitStock>();
            data.PotStock ??= new System.Collections.Generic.List<PotStock>();
            data.BoatHoldCatch ??= new System.Collections.Generic.List<HeldCatchDto>();
            data.BucketCatch ??= new System.Collections.Generic.List<HeldCatchDto>();
            data.FreezerCatch ??= new System.Collections.Generic.List<HeldCatchDto>();
            data.SupplyStock ??= new System.Collections.Generic.List<SupplyStock>();
            data.ActiveHullId ??= "";

            // Clamp to the version we actually understand (never claim to be newer than this build).
            if (data.SchemaVersion > CurrentVersion)
                data.SchemaVersion = CurrentVersion;

            return data;
        }
    }
}
