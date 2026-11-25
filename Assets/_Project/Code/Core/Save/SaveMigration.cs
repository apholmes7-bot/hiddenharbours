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
        public const int CurrentVersion = 27;

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

            // ---- v7 → v8: helm instruments become PURCHASABLE, PER HULL (ADR 0025 S2 / ADR 0030 — the
            // depth sounder is the first). Two new sparse lists: which instruments the player bought for
            // which hull, and that hull's sounder preferences (set-point / armed / units / night). A pre-v8
            // save simply owned no bought instruments and had touched no glass: it gets two empty lists, and
            // EVERY hull therefore resolves to its console's AUTHORED default fit — which is exactly the
            // pre-v8 behaviour, so nothing a player already had changes. Nothing is reinterpreted.
            //
            // ⚠ Deliberately NOT here: the depth itself. A sounding is recomputed from the one height map
            // (water level − seabed elevation) every tick; a saved depth is the precise bug rule 5 exists to
            // prevent, and it would go stale the moment the tide moved.
            if (data.SchemaVersion < 8)
            {
                data.HullInstruments ??= new System.Collections.Generic.List<HullInstrument>();
                data.HullSounderPrefs ??= new System.Collections.Generic.List<SounderPrefsDto>();
                data.SchemaVersion = 8;
            }

            // ---- v8 → v9: the fish finder's vertical RANGE joins the per-hull sounder preferences
            // (ADR 0025 S3 — the sonar supersedes the plain sounder in the same cutout and keeps its
            // shallow alarm, so it shares that record rather than growing a parallel one). ONE new field:
            // SounderPrefsDto.RangeMetres.
            //
            // ⚠ THIS STEP HEALS, IT DOES NOT MERELY DEFAULT — and that distinction is the whole reason it
            // exists. A v8 row deserialized under the v9 struct arrives with RangeMetres = 0 (JsonUtility
            // zero-fills an absent value field), and 0 is not "a small range": every fish mark and the
            // bottom contour are drawn at `depth / range`, so a zero produces Inf/NaN and a garbage
            // picture on the first hull the player ever touched the glass on. Defaulting the field on NEW
            // rows would leave exactly those existing rows broken. So we walk the list and repair.
            //
            // ⚠ ADR 0030 said the per-hull instrument shape needs "no further schema bump" for S3–S5.
            // That sentence is about OWNERSHIP ROWS — fitting a finder, a radar or a GPS is one more
            // (hullId, instrumentId) pair in HullInstruments, and that promise holds exactly as written:
            // nothing here touches that list or its shape. What is bumping is a PREFERENCE, which ADR 0030
            // §"Consequences" already anticipated as a separate matter ("the chartplotter's
            // waypoints/routes are a genuinely different shape and still need their own step"). A new
            // preference field is the smallest possible version of that, and the schema contract in ADR
            // 0008 is that a new persisted field ships with a bump and a forward migration — not that
            // preferences are exempt. Recorded as an amendment on ADR 0030.
            //
            // Deliberately NOT here (Ruling E): a second alarm. The finder reuses DepthSounder.ShallowAlarm
            // and the Armed/AlarmMetres fields that already persist at v8, so this bump stays ONE field.
            if (data.SchemaVersion < 9)
            {
                HealSounderRange(data);
                data.SchemaVersion = 9;
            }

            // ---- v9 → v10: the chartplotter's NAVIGATION — marked waypoints, the planned route and the
            // track breadcrumb (ADR 0025 S6). This is the step ADR 0030 named in advance when it said the
            // per-hull instrument shape needed no further bump "for S3–S5" but that "the chartplotter's
            // waypoints/routes are a genuinely different shape and still need their own step". Three new
            // lists; nothing existing is reinterpreted.
            //
            // A v9 save simply had no navigation, so it gets three empty lists — a loaded v9 game shows an
            // unmarked chart, which is exactly what that player had. Nothing is invented for them.
            //
            // ⚠ PER SAVE, NOT PER HULL, and region-stamped — see SaveData.NavWaypoints for why the
            // skipper's knowledge is not the boat's equipment.
            //
            // ⚠ Deliberately NOT here: the chart itself. Depth shading is recomputed from the one height
            // map (seabed elevation vs water level) exactly as the sounder's reading is; a saved chart is
            // the precise bug rule 5 exists to prevent, and it would go stale the moment the seabed was
            // re-painted.
            if (data.SchemaVersion < 10)
            {
                data.NavWaypoints ??= new System.Collections.Generic.List<NavWaypointDto>();
                data.NavRoute ??= new System.Collections.Generic.List<NavRoutePointDto>();
                data.NavTrack ??= new System.Collections.Generic.List<NavTrackPointDto>();
                HealNavData(data);
                data.SchemaVersion = 10;
            }

            // v10 -> v11: the player's own notebook lines (the My Notes tab). One new list; nothing
            // existing is reinterpreted. A v10 save simply had no notebook, so it gets an empty list —
            // a loaded v10 game shows a blank My Notes page, which is exactly what that player had.
            // Nothing is invented for them.
            //
            // ⚠ DELIBERATELY NOT A HealNotesData(). Every sibling step above heals what it adds —
            // nav trims over-cap lists and rewrites unrenderable rows — and copying that here would be
            // the natural thing to do and would be WRONG. PlayerNoteDto.Text is the player's own prose,
            // the only such field in the file, and the only correct transformation of it is none: no
            // trim, no truncation, no re-encoding, no whitespace collapsing. A cap belongs at the point
            // of entry where she can see it, never at rest. If a later lane adds a heal here in good
            // faith, SaveMigrationV11Tests is what should stop it.
            //
            // ⚠ Deliberately NOT here: quests or knowledge pages. Those are a VIEW over flags that
            // already exist (QuestBoard), so they have no save surface to migrate — which is the whole
            // reason a quest added later is retroactively correct.
            if (data.SchemaVersion < 11)
            {
                data.PlayerNotes ??= new System.Collections.Generic.List<PlayerNoteDto>();
                data.SchemaVersion = 11;
            }

            // v11 -> v12: what the player is WEARING. One new string; nothing existing is reinterpreted.
            // A v11 save was written before there was a wardrobe, so it gets the empty id — which reads
            // as "the outfit the sheets were baked in", i.e. exactly how that player looked. Nothing is
            // invented for them, and nobody is retroactively dressed in something they never chose.
            //
            // ⚠ NOT healed against the shipped wardrobe, deliberately. It is tempting to check the id
            // resolves here and reset it if not — and that would be wrong twice over: a loader has no
            // business deciding a retired outfit means "wear the default", and an id that fails to
            // resolve because an asset failed to LOAD would be permanently erased from the save on the
            // next write. Resolution belongs at the point of use, where the fisher can fall back to the
            // baked look for one session and say so, and the player's choice survives to be honoured
            // when the content is back. SaveMigrationV12Tests is what should stop a heal landing here.
            if (data.SchemaVersion < 12)
            {
                data.WornOutfitId ??= "";
                data.SchemaVersion = 12;
            }

            // v12 -> v13: WHERE THE PLAYER WENT TO SLEEP (ADR 0037) — the region, the storey and the
            // position of the spot they stood on to turn in. Four appended fields; nothing existing is
            // reinterpreted. A v12 save was written when the only thing a bed did was write the file, so
            // it gets the empty region — which reads as "never rested", under which the player wakes at
            // the authored spawn exactly as they did before this field existed. Nothing is invented for
            // them, and in particular nobody is retroactively put to bed somewhere they never slept.
            //
            // ⚠ NOT healed against the world, deliberately, and for the reason the v11→v12 note gives.
            // It is tempting to check here that the region still exists, or that the position is inside
            // it — and both would be wrong: a region id that fails to resolve because a scene did not
            // load is not a corrupt save, and erasing the anchor on the next write would throw away the
            // player's bed for good. The REGION MISMATCH is checked at the point of USE
            // (`RestWakeRestorer`), where it can decline out loud for one session and leave the save
            // alone. What IS repaired is a position that cannot be a position at all — see
            // `HealRestAnchor`, which drops a NaN anchor on the `HealNavData` precedent rather than
            // inventing an origin to put the player at.
            if (data.SchemaVersion < 13)
            {
                data.RestRegion ??= "";
                data.SchemaVersion = 13;
            }

            // v13 -> v14: WHAT IS IN EACH HULL'S TANK (fuel-and-refuelling.md §9.3). One new list;
            // nothing existing is reinterpreted.
            //
            // ⭐ THE EMPTY LIST IS THE WHOLE MIGRATION, AND IT IS NOT LAZINESS. The list is SPARSE and a
            // hull with no row reads as BRIM-FULL, so an empty list means "every boat you own is full" —
            // which is exactly right for a v13 save. That player's boat had no tank, burned nothing and
            // could not run dry; deciding on load that she is empty would INVENT A CONSEQUENCE for them,
            // and stranding somebody on the strength of a schema bump is the spiral canon forbids (P5).
            // Full is the only reading that hands them back what they had.
            //
            // ⚠ NOTHING IS BACK-FILLED, and that is the contract rather than an omission. This step must
            // not walk OwnedBoats writing a "full" row for each: those rows would say exactly what the
            // absence already says, and a step that writes DERIVED state is one re-tune away from being
            // wrong — a hull whose capacity the owner later changes would carry a stale litre count that
            // outranks her own Def. The absence is the truth; a row is only ever a deviation from it.
            // Same reasoning the v12->v13 note gives for not healing the rest anchor against the world.
            if (data.SchemaVersion < 14)
            {
                data.HullFuel ??= new System.Collections.Generic.List<HullFuelDto>();
                data.SchemaVersion = 14;
            }

            // v14 -> v15: THE PLAYER'S OWN BUILD (character PR 2b, ADR 0051). One new field, the recipe;
            // nothing existing is reinterpreted.
            //
            // ⭐ THE EMPTY RECIPE IS THE WHOLE MIGRATION. An empty recipe draws the body as baked, which is
            // exactly what a v14 save's player looked like: the mesh figure never read a recipe because
            // there was none.
            //
            // ⚠ WornOutfitId IS RESOLVED, NOT MOVED. It names a wardrobe preset that recolours the SPRITE
            // layer (PlayerOutfitVisual); the mesh never drew it, so copying it into the recipe's outfit
            // and shirt keys would CHANGE how an old save looks rather than keep it. It stays where it is,
            // read by the same code as before. And no recipe is healed against shipped content here: the
            // builder refuses a stale key out loud at the point of use (the v12->v13 reasoning), which is
            // where a refusal can be logged without rewriting the player's file.
            if (data.SchemaVersion < 15)
            {
                data.PlayerRecipe ??= new CharacterRecipe();
                data.SchemaVersion = 15;
            }

            // v15 -> v16: fishing longline placements and owned tubs. Existing pots are untouched.
            if (data.SchemaVersion < 16)
            {
                data.PlacedLonglines ??= new System.Collections.Generic.List<PlacedLonglineDto>();
                data.TubStock ??= new System.Collections.Generic.List<TubStock>();
                data.SchemaVersion = 16;
            }

            // v16 -> v17: older games have no chosen name. Their appearance is untouched.
            if (data.SchemaVersion < 17)
            {
                data.PlayerName = "";
                data.SchemaVersion = 17;
            }

            // v17 -> v18: character traits. Older games picked none; nothing else is touched.
            if (data.SchemaVersion < 18)
            {
                data.PlayerTraits ??= new System.Collections.Generic.List<string>();
                data.SchemaVersion = 18;
            }

            // v18 -> v19: hunger and tiredness. Older games start fed and rested at their own saved time.
            if (data.SchemaVersion < 19)
            {
                data.PlayerNeeds = PlayerNeedsDto.FedAndRestedAt(data.GameTimeSeconds);
                data.SchemaVersion = 19;
            }

            // v19 -> v20: THE REST OF THE SLEEP PLACE (owner ruling 2026-11-15: "a sleep is a save"). Which
            // bed, when the sleep ended, and for a bunk aboard the boat's own pose. Nothing existing is
            // reinterpreted: a v13..v19 bed anchor stays exactly where it was and still wakes the player
            // there, and a save with no anchor still wakes at the authored spawn. The new fields take their
            // honest empties: no bed id (unknown), woke-at 0 (unknown, never invented from the autosave's
            // clock, which may be hours after the sleep), and not aboard (a pre-v20 bunk recorded nothing).
            if (data.SchemaVersion < 20)
            {
                data.RestBedId ??= "";
                data.RestBoatHullId ??= "";
                data.RestBoatRegion ??= "";
                data.RestAboard = false;
                data.SchemaVersion = 20;
            }

            // v20 -> v21: THE BODY (owner ask 2026-11-15: frame, weight, metabolism, overeating). An older
            // game gets the unset body, which reads as the default frame and height at its lean mass: the
            // reference body, so the player keeps exactly today's hunger rate and walk speed. No messes yet.
            if (data.SchemaVersion < 21)
            {
                data.PlayerBody = new PlayerBodyDto();
                data.Messes ??= new System.Collections.Generic.List<MessRecordDto>();
                data.NextMessSerial = 0;
                data.SchemaVersion = 21;
            }

            // v21 -> v22: CLEANLINESS, the third need (owner ask 2026-11-15). An older game starts clean,
            // idle and anchored at its own saved time, so nothing changes until the player gets dirty.
            if (data.SchemaVersion < 22)
            {
                data.PlayerCleanliness = PlayerCleanlinessDto.CleanAt(data.GameTimeSeconds);
                data.SchemaVersion = 22;
            }

            // v22 -> v23: THE FOOD STORES (kitchens slice 1, owner ask 2026-11-15). An older game had no
            // pantry, cooler or fridge, so it starts with every store empty: no food invented, no ice packed.
            if (data.SchemaVersion < 23)
            {
                data.KitchenStores = new System.Collections.Generic.List<KitchenStorageStateDto>();
                data.SchemaVersion = 23;
            }

            // v23 -> v24: THE CATCH RECORD, GIFT DAYS, COOK JOBS AND LORAN NUMBERS (owner: "Save v24 yes").
            // An older game never recorded any of them, so each starts empty: no catch invented, every person
            // takes a gift today, nothing on the stove, no numbers in any set.
            if (data.SchemaVersion < 24)
            {
                data.CaughtSpecies = new System.Collections.Generic.List<CaughtSpeciesDto>();
                data.NpcGiftDays = new System.Collections.Generic.List<NpcGiftDayDto>();
                data.CookJobs = new System.Collections.Generic.List<CookJobDto>();
                data.HullLoranWaypoints = new System.Collections.Generic.List<LoranWaypointDto>();
                data.SchemaVersion = 24;
            }

            // v24 -> v25: STORAGE WITH MEMORY (owner 2026-11-17: "give them storage with memory at ginnys").
            // No container remembered anything before v25, so every one starts empty. Nothing is invented: the
            // first rod and the shovel lie where the world places them, which is where an older game left them
            // on every load anyway.
            if (data.SchemaVersion < 25)
            {
                data.StoredContainers = new System.Collections.Generic.List<StoredContainerDto>();
                data.SchemaVersion = 25;
            }

            // v25 -> v26: STORE PURCHASES, PAID SPECIAL ORDERS AND THE SHOPPING BAG (one bump for the three).
            // An older game recorded none of them, so each starts empty: its shelves are what the calendar
            // says, there is no order and no bag, and whatever he bought is simply in his supplies. Nothing
            // is transformed and nothing else in the save moves.
            if (data.SchemaVersion < 26)
            {
                data.StorePurchases = new System.Collections.Generic.List<StorePurchaseRowDto>();
                data.SpecialOrders = new System.Collections.Generic.List<SpecialOrderDto>();
                data.NextSpecialOrderNumber = 1;
                data.ShoppingBags = new System.Collections.Generic.List<ShoppingBagDto>();
                data.SchemaVersion = 26;
            }

            // v26 -> v27: CLOTHES, THE CLOTHING RACK'S SALES AND SKILLS (owner 2026-11-25: "Yes, plan v27";
            // ADR 0064). An older game recorded none of them, so each starts empty: he owns no garment and
            // wears only his own outfit colours, every rack is full, every skill is at zero. Nothing is
            // transformed and nothing else in the save moves. Until this step has shipped, a further v27 list
            // (fitted rod parts are to come) is set empty here too; after it ships, anything new is v28.
            if (data.SchemaVersion < 27)
            {
                data.PlayerClothing = new System.Collections.Generic.List<PlayerClothingDto>();
                data.ClothingRackSales = new System.Collections.Generic.List<ClothingRackSaleDto>();
                data.PlayerSkills = new System.Collections.Generic.List<PlayerSkillDto>();
                data.SchemaVersion = 27;
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
            data.PlacedLonglines ??= new System.Collections.Generic.List<PlacedLonglineDto>();
            data.TubStock ??= new System.Collections.Generic.List<TubStock>();
            data.BoatHoldCatch ??= new System.Collections.Generic.List<HeldCatchDto>();
            data.BucketCatch ??= new System.Collections.Generic.List<HeldCatchDto>();
            data.FreezerCatch ??= new System.Collections.Generic.List<HeldCatchDto>();
            data.SupplyStock ??= new System.Collections.Generic.List<SupplyStock>();
            data.HullInstruments ??= new System.Collections.Generic.List<HullInstrument>();
            data.HullSounderPrefs ??= new System.Collections.Generic.List<SounderPrefsDto>();
            data.HullFuel ??= new System.Collections.Generic.List<HullFuelDto>();
            data.NavWaypoints ??= new System.Collections.Generic.List<NavWaypointDto>();
            data.NavRoute ??= new System.Collections.Generic.List<NavRoutePointDto>();
            data.NavTrack ??= new System.Collections.Generic.List<NavTrackPointDto>();
            data.PlayerNotes ??= new System.Collections.Generic.List<PlayerNoteDto>();
            data.ActiveHullId ??= "";
            data.WornOutfitId ??= "";
            data.RestRegion ??= "";
            data.RestBedId ??= "";
            data.RestBoatHullId ??= "";
            data.RestBoatRegion ??= "";
            data.PlayerRecipe ??= new CharacterRecipe();
            data.PlayerRecipe.Heal();    // a field JsonUtility left null reads as "the preset's own"
            data.PlayerName ??= "";
            data.SpentClamHoles ??= new System.Collections.Generic.List<SpentClamHoleDto>();
            data.PlayerTraits ??= new System.Collections.Generic.List<string>();
            data.PlayerNeeds ??= new PlayerNeedsDto();
            data.PlayerBody ??= new PlayerBodyDto();
            data.Messes ??= new System.Collections.Generic.List<MessRecordDto>();
            data.PlayerCleanliness ??= new PlayerCleanlinessDto();
            data.PlayerCleanliness.WashStationId ??= "";
            data.KitchenStores ??= new System.Collections.Generic.List<KitchenStorageStateDto>();
            HealKitchenStores(data.KitchenStores);
            data.CaughtSpecies ??= new System.Collections.Generic.List<CaughtSpeciesDto>();
            data.NpcGiftDays ??= new System.Collections.Generic.List<NpcGiftDayDto>();
            data.CookJobs ??= new System.Collections.Generic.List<CookJobDto>();
            data.HullLoranWaypoints ??= new System.Collections.Generic.List<LoranWaypointDto>();
            HealV24Rows(data);
            data.StoredContainers ??= new System.Collections.Generic.List<StoredContainerDto>();
            HealStoredContainers(data.StoredContainers);
            data.StorePurchases ??= new System.Collections.Generic.List<StorePurchaseRowDto>();
            data.SpecialOrders ??= new System.Collections.Generic.List<SpecialOrderDto>();
            data.ShoppingBags ??= new System.Collections.Generic.List<ShoppingBagDto>();
            HealV26Rows(data);   // after SupplyStock's null-repair: a bag is held to what he owns
            data.PlayerClothing ??= new System.Collections.Generic.List<PlayerClothingDto>();
            data.ClothingRackSales ??= new System.Collections.Generic.List<ClothingRackSaleDto>();
            data.PlayerSkills ??= new System.Collections.Generic.List<PlayerSkillDto>();
            HealV27Rows(data);
            // Same defensive spirit as the null-repair above, for the one field where a zero is not a
            // value but a crash: a hand-edited JSON, or a row written by a build between the field landing
            // and this heal, would otherwise divide by it. Idempotent — after the v9 step there is nothing
            // left to fix.
            HealSounderRange(data);
            // Same defensive spirit, for the rows where a bad value is unrenderable rather than merely
            // wrong: a NaN waypoint has no honest position to fall back to, so it is dropped. Idempotent
            // — after the v10 step there is nothing left to fix.
            HealNavData(data);
            // Same defensive spirit again, for the one anchor in the file: a rest position that is not a
            // finite number cannot be walked to, and there is no honest place to put the player instead.
            // Idempotent — a save whose anchor is good is left exactly as it is.
            HealRestAnchor(data);

            // Clamp to the version we actually understand (never claim to be newer than this build).
            if (data.SchemaVersion > CurrentVersion)
                data.SchemaVersion = CurrentVersion;

            return data;
        }

        /// <summary>
        /// Repair every sounder-preference row whose fish-finder RANGE is not a positive number of metres,
        /// setting it to the shipped default (<see cref="FishFinderSettings.Default"/>). The ONE place that
        /// rule lives, called from the v8→v9 step and once more unconditionally.
        ///
        /// <para><b>Asset-free on purpose</b> (the v3→v4 precedent): the migration reads the code default
        /// rather than <c>GameServices.Config</c>, so loading a save can never depend on a
        /// <c>ScriptableObject</c> having been wired first — and so this is testable with no scene.</para>
        /// </summary>
        private static void HealSounderRange(SaveData data)
        {
            if (data?.HullSounderPrefs == null) return;
            float defaultRange = FishFinderSettings.Default.DefaultRangeMetres;
            for (int i = 0; i < data.HullSounderPrefs.Count; i++)
            {
                SounderPrefsDto dto = data.HullSounderPrefs[i];
                if (dto.RangeMetres > 0f) continue;         // a dialled range is never overwritten
                dto.RangeMetres = defaultRange;
                data.HullSounderPrefs[i] = dto;             // SounderPrefsDto is a struct — write it back
            }
        }

        /// <summary>
        /// DROP every unusable navigation row and trim each list back inside its cap. Called from the
        /// v9→v10 step and once more unconditionally, the <see cref="HealSounderRange"/> shape.
        ///
        /// <para><b>Drop, never repair.</b> A sounder range has an obviously right replacement (the
        /// shipped default), so that heal substitutes. A waypoint at NaN does not — there is no correct
        /// position to invent for it, and putting it at the origin would silently plant a mark on water
        /// the player never marked. So a row that cannot be drawn honestly is removed. That is also why
        /// this never throws: a hand-edited save loses the bad rows and still loads.</para>
        ///
        /// <para><b>Asset-free on purpose</b> (the v3→v4 precedent): the caps come from
        /// <see cref="ChartplotterSettings.Default"/>, not from <c>GameServices.Config</c>, so loading a
        /// save never depends on a <c>ScriptableObject</c> being wired first — and so this is testable
        /// with no scene.</para>
        /// </summary>
        /// <summary>
        /// Null-repair the food stores (kitchens slice 1): drop a null row or one with no key (it can
        /// never be found again), give a missing stack list or def id their empties, and drop a stack with
        /// no item or no count. A stack whose count is real is never touched: food is not invented or
        /// thrown away by a load. Idempotent.
        /// </summary>
        private static void HealKitchenStores(System.Collections.Generic.List<KitchenStorageStateDto> stores)
        {
            for (int i = stores.Count - 1; i >= 0; i--)
            {
                KitchenStorageStateDto s = stores[i];
                if (s == null || string.IsNullOrEmpty(s.Key)) { stores.RemoveAt(i); continue; }
                s.StorageDefId ??= "";
                s.Stacks ??= new System.Collections.Generic.List<KitchenStackDto>();
                for (int k = s.Stacks.Count - 1; k >= 0; k--)
                    if (string.IsNullOrEmpty(s.Stacks[k].ItemId) || s.Stacks[k].Count <= 0) s.Stacks.RemoveAt(k);
            }
        }

        /// <summary>
        /// Null-repair the v25 storage rows. A container with no key is dropped (nothing could find it again);
        /// a second row for a key already kept is folded into the first, so no item is lost and the key stays
        /// one row. An item with no kind, or a counted lot of zero or fewer, is dropped, and so is a catch record
        /// with no species or no quantity; a container left with nothing in it is dropped. One object, one
        /// place: an instance id that repeats (in two containers, or twice in one) keeps its FIRST occurrence in
        /// list order and every later one is dropped. Kept rows are never changed. Idempotent.
        /// </summary>
        private static void HealStoredContainers(System.Collections.Generic.List<StoredContainerDto> containers)
        {
            var kept = new System.Collections.Generic.Dictionary<string, StoredContainerDto>(System.StringComparer.Ordinal);
            var instances = new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal);
            for (int i = 0; i < containers.Count; i++)
            {
                StoredContainerDto c = containers[i];
                if (c == null || string.IsNullOrEmpty(c.Key))
                {
                    containers.RemoveAt(i--);
                    continue;
                }
                c.ContainerDefId ??= "";
                c.Items ??= new System.Collections.Generic.List<StoredItemDto>();
                for (int k = 0; k < c.Items.Count; k++)   // forward: the first occurrence is the one kept
                {
                    StoredItemDto item = c.Items[k];
                    if (item == null || string.IsNullOrEmpty(item.ItemId) || (!item.IsInstance && item.Count <= 0)
                        || (item.IsInstance && !instances.Add(item.InstanceId)))
                    {
                        c.Items.RemoveAt(k--);
                        continue;
                    }
                    item.InstanceId ??= "";
                }
                c.Catch ??= new System.Collections.Generic.List<HeldCatchDto>();
                c.Catch.RemoveAll(r => string.IsNullOrEmpty(r.SpeciesId) || r.Quantity <= 0);
                if (kept.TryGetValue(c.Key, out StoredContainerDto first))
                {
                    first.Items.AddRange(c.Items);
                    first.Catch.AddRange(c.Catch);
                    containers.RemoveAt(i--);
                    continue;
                }
                kept.Add(c.Key, c);
            }
            for (int i = containers.Count - 1; i >= 0; i--)
                if (StoredContainers.IsEmpty(containers[i])) containers.RemoveAt(i);
        }

        /// <summary>
        /// Null-repair the v24 rows. A row that can never be found or used again is dropped: a catch row with
        /// no species or no count (or a second row for a species already kept, so the record stays one row
        /// per species), a gift row with no person or no count, a cook job with no station, no recipe or no
        /// finite ready time, a Loran row with no hull or one that is not a usable waypoint. A kept row's
        /// counts and numbers are never changed, except that a catch row's released count is held inside
        /// 0..count and an unreadable record weight reads as unknown (0). Idempotent.
        /// </summary>
        private static void HealV24Rows(SaveData data)
        {
            var seen = new System.Collections.Generic.HashSet<string>(System.StringComparer.Ordinal);
            var caught = data.CaughtSpecies;
            for (int i = 0; i < caught.Count; i++)
            {
                CaughtSpeciesDto c = caught[i];
                if (c == null || string.IsNullOrEmpty(c.SpeciesId) || c.Count <= 0 || !seen.Add(c.SpeciesId))
                {
                    caught.RemoveAt(i--);
                    continue;
                }
                c.DisplayName ??= "";
                if (c.ReleasedCount < 0) c.ReleasedCount = 0;
                if (c.ReleasedCount > c.Count) c.ReleasedCount = c.Count;
                if (float.IsNaN(c.RecordWeightKg) || float.IsInfinity(c.RecordWeightKg) || c.RecordWeightKg < 0f)
                    c.RecordWeightKg = 0f;
            }

            var gifts = data.NpcGiftDays;
            for (int i = gifts.Count - 1; i >= 0; i--)
                if (gifts[i] == null || string.IsNullOrEmpty(gifts[i].NpcId) || gifts[i].Count <= 0) gifts.RemoveAt(i);

            var jobs = data.CookJobs;
            for (int i = jobs.Count - 1; i >= 0; i--)
            {
                CookJobDto j = jobs[i];
                if (j == null || string.IsNullOrEmpty(j.StationKey) || string.IsNullOrEmpty(j.RecipeId)
                    || double.IsNaN(j.ReadyAtGameSeconds) || double.IsInfinity(j.ReadyAtGameSeconds))
                {
                    jobs.RemoveAt(i);
                    continue;
                }
                j.StationId ??= "";
            }

            var loran = data.HullLoranWaypoints;
            for (int i = loran.Count - 1; i >= 0; i--)
            {
                LoranWaypointDto w = loran[i];
                if (w == null || string.IsNullOrEmpty(w.HullId) || !w.ToWaypoint().IsValid) loran.RemoveAt(i);
            }
        }

        /// <summary>
        /// Repair the v26 rows: store purchases, special orders and shopping bags. Asset-free (no Def, no
        /// config, no service) and idempotent. The one rule over all three: <b>a load never destroys
        /// something the player paid for or owns</b>. A purchase row is only a note of what left a shelf, so a
        /// bad one can go (the shelf reads a little fuller). A paid order is money, so it is kept. A bag row
        /// only sets goods aside that are already counted in his supplies, so dropping one loses nothing.
        /// </summary>
        private static void HealV26Rows(SaveData data)
        {
            HealStorePurchases(data.StorePurchases);
            HealSpecialOrders(data);
            HealShoppingBags(data);
        }

        private static readonly System.Comparison<StorePurchaseRowDto> PurchaseOrder = ComparePurchases;

        /// <summary>The one order purchase rows are kept and replayed in: day, seconds, shop, line.</summary>
        private static int ComparePurchases(StorePurchaseRowDto a, StorePurchaseRowDto b)
        {
            int c = a.Day.CompareTo(b.Day);
            if (c != 0) return c;
            c = a.Seconds.CompareTo(b.Seconds);
            if (c != 0) return c;
            c = string.CompareOrdinal(a.ShopId, b.ShopId);
            if (c != 0) return c;
            return string.CompareOrdinal(a.ShelfLineId, b.ShelfLineId);
        }

        /// <summary>
        /// Store purchases: drop a row with no shop or no line, a negative day or time, a time that is not a
        /// finite number, or units that are zero, negative or not finite. Sort into the replay order, then
        /// fold rows equal in shop, line, day and seconds into one (their units added, so nothing bought is
        /// forgotten). Kept rows are not otherwise changed.
        /// </summary>
        private static void HealStorePurchases(System.Collections.Generic.List<StorePurchaseRowDto> rows)
        {
            for (int i = rows.Count - 1; i >= 0; i--)
            {
                StorePurchaseRowDto r = rows[i];
                if (r == null || string.IsNullOrEmpty(r.ShopId) || string.IsNullOrEmpty(r.ShelfLineId)
                    || r.Day < 0 || !IsFiniteNumber(r.Seconds) || r.Seconds < 0d
                    || !IsFiniteNumber(r.Units) || r.Units <= 0d)
                    rows.RemoveAt(i);
            }
            rows.Sort(PurchaseOrder);
            for (int i = rows.Count - 1; i > 0; i--)
            {
                if (ComparePurchases(rows[i - 1], rows[i]) != 0) continue;
                double sum = rows[i - 1].Units + rows[i].Units;
                if (IsFiniteNumber(sum)) rows[i - 1].Units = sum;
                rows.RemoveAt(i);
            }
        }

        /// <summary>
        /// Special orders (reserved at v26). <b>A row is dropped only when it carries nothing</b>: no money
        /// paid on the order and no line left. Every other row is kept, whatever else is wrong with it,
        /// because the repair cannot refund: an order with a missing id, an unknown state or no lines left
        /// still holds what he paid, and the feature's own start-up decides what it becomes.
        ///
        /// <para><b>Money on a line counts as something too.</b> A line is dropped only when it is unusable
        /// (no listing, or a count under 1) AND has no price above zero; a line he paid for stays, so it
        /// keeps its order alive even when the order's own total reads 0. Lines for the same listing fold
        /// into the first, counts added and prices added, so folding never changes what the lines sum to.
        /// <b>The repair never rewrites money</b>: when the lines' prices do not add up to the order's total,
        /// both are left exactly as they are (the total is the figure that stands; reconciling them is the
        /// feature's work). The one exception is a negative price, on the order or on a line, which reads 0:
        /// it would otherwise be a refund that takes money.</para>
        ///
        /// <para>Also inside a kept row: an unreadable or negative order time, a negative ordered day and a
        /// negative due day read 0 (due at once, never later; the due day is never changed to fit the
        /// ordered day); a negative count on a kept line reads 0; a state outside the
        /// known ones reads as waiting, the one reading that can never cost him the goods. Ids are never
        /// rewritten. The counter is held at 1 or more and above every number on any row, dropped rows
        /// included, so no number is used twice.</para>
        /// </summary>
        private static void HealSpecialOrders(SaveData data)
        {
            System.Collections.Generic.List<SpecialOrderDto> orders = data.SpecialOrders;
            int next = data.NextSpecialOrderNumber < 1 ? 1 : data.NextSpecialOrderNumber;
            for (int i = 0; i < orders.Count; i++)
            {
                SpecialOrderDto o = orders[i];
                if (o == null)
                {
                    orders.RemoveAt(i--);
                    continue;
                }
                o.OrderId ??= "";
                o.SellerId ??= "";
                o.DeliverToId ??= "";
                o.Lines ??= new System.Collections.Generic.List<SpecialOrderLineDto>();
                for (int k = 0; k < o.Lines.Count; k++)
                {
                    SpecialOrderLineDto line = o.Lines[k];
                    if (line != null && line.PaidPrice < 0) line.PaidPrice = 0;
                    // A line goes only when it is unusable AND holds no money. A paid line stays, whatever
                    // else is wrong with it: its price is the exact refund for that line.
                    if (line == null
                        || ((string.IsNullOrEmpty(line.ListingId) || line.Count < 1) && line.PaidPrice <= 0))
                    {
                        o.Lines.RemoveAt(k--);
                        continue;
                    }
                    line.ListingId ??= "";
                    if (line.Count < 0) line.Count = 0;
                    for (int first = 0; first < k; first++)
                    {
                        if (!string.Equals(o.Lines[first].ListingId, line.ListingId, System.StringComparison.Ordinal))
                            continue;
                        o.Lines[first].Count = AddClamped(o.Lines[first].Count, line.Count);
                        o.Lines[first].PaidPrice = AddClamped(o.Lines[first].PaidPrice, line.PaidPrice);
                        o.Lines.RemoveAt(k--);
                        break;
                    }
                }
                if (o.PaidPrice < 0) o.PaidPrice = 0;
                if (!IsFiniteNumber(o.OrderedAtSeconds) || o.OrderedAtSeconds < 0d) o.OrderedAtSeconds = 0d;
                if (o.OrderedDay < 0) o.OrderedDay = 0;
                if (o.DueDay < 0) o.DueDay = 0;
                if (o.State < SpecialOrderDto.StateWaiting || o.State > SpecialOrderDto.StateCollected)
                    o.State = SpecialOrderDto.StateWaiting;

                int used = OrderNumberOf(o.OrderId);
                if (used >= next && used < int.MaxValue) next = used + 1;

                if (o.PaidPrice <= 0 && o.Lines.Count == 0) orders.RemoveAt(i--);
            }
            data.NextSpecialOrderNumber = next;
        }

        /// <summary>The n of an <c>order.&lt;seller&gt;.&lt;n&gt;</c> id: the digits after the last dot.
        /// 0 when the id has no such number.</summary>
        private static int OrderNumberOf(string orderId)
        {
            if (string.IsNullOrEmpty(orderId)) return 0;
            int dot = orderId.LastIndexOf('.');
            if (dot < 0 || dot == orderId.Length - 1) return 0;
            long n = 0;
            for (int i = dot + 1; i < orderId.Length; i++)
            {
                char c = orderId[i];
                if (c < '0' || c > '9') return 0;
                n = n * 10 + (c - '0');
                if (n > int.MaxValue) return 0;
            }
            return (int)n;
        }

        /// <summary>
        /// Shopping bags. The goods in a bag are counted in <see cref="SaveData.SupplyStock"/>, which this
        /// never touches; a row only sets some of them aside. So every repair here errs toward "found put
        /// away" and none can lose goods.
        ///
        /// <para>A bag is dropped when it has no kind, or when it was set down somewhere that is not a place
        /// (no region, a position that is not a finite number, a storey below the ground): it could never be
        /// found again and its goods would stay set aside for good. A bag in his hand needs no place, so its
        /// unreadable place fields just read 0. A region no scene claims is kept: that is a content question,
        /// and it may load later. Inside a kept bag: an item with no id or a count under 1 is dropped, items
        /// of one kind fold into the first, and each is held to what he owns of it, less what earlier bags in
        /// the list already hold; a bag left with nothing in it is dropped.</para>
        /// </summary>
        private static void HealShoppingBags(SaveData data)
        {
            System.Collections.Generic.List<ShoppingBagDto> bags = data.ShoppingBags;
            var claimed = new System.Collections.Generic.Dictionary<string, int>(System.StringComparer.Ordinal);
            for (int i = 0; i < bags.Count; i++)
            {
                ShoppingBagDto b = bags[i];
                if (b == null)
                {
                    bags.RemoveAt(i--);
                    continue;
                }
                b.BagDefId ??= "";
                b.Region ??= "";
                b.HullId ??= "";
                b.Items ??= new System.Collections.Generic.List<BagItemDto>();

                bool finite = IsFiniteNumber(b.PosX) && IsFiniteNumber(b.PosY);
                if (b.Held)
                {
                    if (!finite) { b.PosX = 0f; b.PosY = 0f; }
                    if (b.Level < 0) b.Level = 0;
                }
                bool isSomewhere = b.Held || (finite && b.Level >= 0 && b.Region.Length > 0);
                if (b.BagDefId.Length == 0 || !isSomewhere)
                {
                    bags.RemoveAt(i--);
                    continue;
                }

                for (int k = 0; k < b.Items.Count; k++)
                {
                    BagItemDto item = b.Items[k];
                    if (item == null || string.IsNullOrEmpty(item.SupplyId) || item.Count < 1)
                    {
                        b.Items.RemoveAt(k--);
                        continue;
                    }
                    for (int first = 0; first < k; first++)
                    {
                        if (!string.Equals(b.Items[first].SupplyId, item.SupplyId, System.StringComparison.Ordinal))
                            continue;
                        b.Items[first].Count = AddClamped(b.Items[first].Count, item.Count);
                        b.Items.RemoveAt(k--);
                        break;
                    }
                }
                for (int k = 0; k < b.Items.Count; k++)
                {
                    BagItemDto item = b.Items[k];
                    claimed.TryGetValue(item.SupplyId, out int taken);
                    int free = OwnedSupply(data, item.SupplyId) - taken;
                    if (item.Count > free) item.Count = free;
                    if (item.Count < 1)
                    {
                        b.Items.RemoveAt(k--);
                        continue;
                    }
                    claimed[item.SupplyId] = taken + item.Count;
                }
                if (b.Items.Count == 0) bags.RemoveAt(i--);
            }
        }

        /// <summary>How many of a supply the save holds (the first row for the id, the way the supply locker
        /// reads it). Read straight from the list on purpose: the repair must see what he OWNS, whatever a
        /// bag sets aside.</summary>
        private static int OwnedSupply(SaveData data, string supplyId)
        {
            System.Collections.Generic.List<SupplyStock> stock = data.SupplyStock;
            for (int i = 0; i < stock.Count; i++)
                if (stock[i].SupplyId == supplyId) return stock[i].Count > 0 ? stock[i].Count : 0;
            return 0;
        }

        /// <summary>
        /// The v27 rows' load repair: asset-free (it has no garment or skill content to ask) and idempotent.
        /// It never takes away something he owns or has earned.
        /// </summary>
        private static void HealV27Rows(SaveData data)
        {
            HealPlayerClothing(data.PlayerClothing);
            HealClothingRackSales(data.ClothingRackSales);
            HealPlayerSkills(data.PlayerSkills);
        }

        /// <summary>
        /// Clothes: drop a null row; an empty player key means the local player; drop empty ids and repeats
        /// in both lists; a worn garment missing from what he owns is added to it (he was wearing it); rows
        /// for one player fold into the first, their lists joined in order. A row with both lists empty is
        /// kept as it is. Ids are never rewritten and no garment is dropped for being unknown: the save has
        /// no content to know it by, and the wardrobe skips an id it cannot find.
        /// </summary>
        private static void HealPlayerClothing(System.Collections.Generic.List<PlayerClothingDto> rows)
        {
            for (int i = rows.Count - 1; i >= 0; i--)
                if (rows[i] == null) rows.RemoveAt(i);
            for (int i = 0; i < rows.Count; i++)
            {
                PlayerClothingDto row = rows[i];
                row.PlayerKey = SavePlayerKeys.Normalise(row.PlayerKey);
                row.OwnedClothing = Distinct(row.OwnedClothing);
                row.WornClothing = Distinct(row.WornClothing);
                for (int w = 0; w < row.WornClothing.Count; w++)
                    PlayerClothingRows.AddOnce(row.OwnedClothing, row.WornClothing[w]);
            }
            for (int i = rows.Count - 1; i > 0; i--)
            {
                int first = -1;
                for (int j = 0; j < i; j++)
                    if (string.Equals(rows[j].PlayerKey, rows[i].PlayerKey, System.StringComparison.Ordinal)) { first = j; break; }
                if (first < 0) continue;
                for (int k = 0; k < rows[i].OwnedClothing.Count; k++)
                    PlayerClothingRows.AddOnce(rows[first].OwnedClothing, rows[i].OwnedClothing[k]);
                for (int k = 0; k < rows[i].WornClothing.Count; k++)
                    PlayerClothingRows.AddOnce(rows[first].WornClothing, rows[i].WornClothing[k]);
                rows.RemoveAt(i);
            }
        }

        /// <summary>The ids of <paramref name="ids"/> in order, each once, none empty (a new list for null).</summary>
        private static System.Collections.Generic.List<string> Distinct(System.Collections.Generic.List<string> ids)
        {
            var kept = new System.Collections.Generic.List<string>(ids?.Count ?? 0);
            if (ids != null)
                for (int i = 0; i < ids.Count; i++) PlayerClothingRows.AddOnce(kept, ids[i]);
            return kept;
        }

        private static readonly System.Comparison<ClothingRackSaleDto> RackSaleOrder = ClothingRackSaleRows.Compare;

        /// <summary>
        /// Rack sales: drop a row with no rack or garment, units under 1 or a negative day; sort into (day,
        /// rack, garment) order; fold rows equal in all three into one with their units added (so nothing sold
        /// is forgotten and the rack never reads fuller than it is).
        /// </summary>
        private static void HealClothingRackSales(System.Collections.Generic.List<ClothingRackSaleDto> rows)
        {
            for (int i = rows.Count - 1; i >= 0; i--)
            {
                ClothingRackSaleDto r = rows[i];
                if (r == null || string.IsNullOrEmpty(r.RackId) || string.IsNullOrEmpty(r.ClothingId)
                    || r.Units <= 0 || r.Day < 0)
                    rows.RemoveAt(i);
            }
            // List.Sort is not stable, but rows that compare equal are folded next, so the result is the same.
            rows.Sort(RackSaleOrder);
            for (int i = rows.Count - 1; i > 0; i--)
            {
                if (ClothingRackSaleRows.Compare(rows[i - 1], rows[i]) != 0) continue;
                rows[i - 1].Units = AddClamped(rows[i - 1].Units, rows[i].Units);
                rows.RemoveAt(i);
            }
        }

        /// <summary>
        /// Skills: drop a null row or one with no skill id; an empty player key means the local player; a
        /// negative experience reads 0; two rows for one player and skill fold into the first keeping the
        /// LARGER experience (two copies of one record are the same experience, not twice as much). Order is
        /// otherwise kept.
        /// </summary>
        private static void HealPlayerSkills(System.Collections.Generic.List<PlayerSkillDto> rows)
        {
            for (int i = rows.Count - 1; i >= 0; i--)
            {
                PlayerSkillDto r = rows[i];
                if (r == null || string.IsNullOrEmpty(r.SkillId)) { rows.RemoveAt(i); continue; }
                r.PlayerKey = SavePlayerKeys.Normalise(r.PlayerKey);
                if (r.Experience < 0) r.Experience = 0;
            }
            for (int i = rows.Count - 1; i > 0; i--)
            {
                for (int j = 0; j < i; j++)
                {
                    if (!string.Equals(rows[j].PlayerKey, rows[i].PlayerKey, System.StringComparison.Ordinal)
                        || !string.Equals(rows[j].SkillId, rows[i].SkillId, System.StringComparison.Ordinal))
                        continue;
                    if (rows[i].Experience > rows[j].Experience) rows[j].Experience = rows[i].Experience;
                    rows.RemoveAt(i);
                    break;
                }
            }
        }

        private static int AddClamped(int a, int b)
        {
            long sum = (long)a + b;
            return sum > int.MaxValue ? int.MaxValue : (int)sum;
        }

        private static bool IsFiniteNumber(double v) => !double.IsNaN(v) && !double.IsInfinity(v);

        private static bool IsFiniteNumber(float v) => !float.IsNaN(v) && !float.IsInfinity(v);

        /// <summary>
        /// DROP a rest anchor that cannot be a position — a NaN or infinite coordinate, or a storey below
        /// the ground — by clearing the region, which is the field that means "there is an anchor at
        /// all". Called from the v12→v13 step and once more unconditionally, the
        /// <see cref="HealNavData"/> shape.
        ///
        /// <para><b>Drop, never repair</b>, for exactly the reason the nav heal gives: a sounder range has
        /// an obviously right replacement and a bed does not. Putting an unreadable anchor at the origin
        /// would wake the player in the corner of the map with no indication anything had gone wrong,
        /// whereas clearing it wakes them at the authored spawn — the same well-understood place a player
        /// who has never slept wakes.</para>
        ///
        /// <para><b>A wrong-looking anchor is NOT unreadable and is left alone.</b> A region no scene
        /// claims, a position outside the region's rectangle, a storey in a building that has since lost
        /// its upstairs: every one of those is a content question a loader cannot answer, and every one
        /// of them can be true for one build and false for the next. They are declined at the point of
        /// use, which leaves the save intact for the build that can honour it.</para>
        /// </summary>
        private static void HealRestAnchor(SaveData data)
        {
            if (data == null || string.IsNullOrEmpty(data.RestRegion)) return;

            bool finite = !float.IsNaN(data.RestPosX) && !float.IsInfinity(data.RestPosX)
                       && !float.IsNaN(data.RestPosY) && !float.IsInfinity(data.RestPosY);
            if (finite && data.RestLevel >= 0) return;

            data.RestRegion = "";
            data.RestPosX = 0f;
            data.RestPosY = 0f;
            data.RestLevel = 0;
        }

        private static void HealNavData(SaveData data)
        {
            if (data == null) return;
            ChartplotterSettings cfg = ChartplotterSettings.Default;

            if (data.NavWaypoints != null)
            {
                for (int i = data.NavWaypoints.Count - 1; i >= 0; i--)
                {
                    NavWaypointDto d = data.NavWaypoints[i];
                    if (!d.ToWaypoint().IsValid) { data.NavWaypoints.RemoveAt(i); continue; }
                    // An out-of-range kind is a symbol we cannot draw; the row's POSITION is still good,
                    // so it survives as a plain mark rather than being thrown away.
                    if (d.Kind < 0 || d.Kind > (int)NavWaypointKind.Dest)
                    {
                        d.Kind = (int)NavWaypointKind.Mark;
                        data.NavWaypoints[i] = d;
                    }
                }
                // Over the cap (a cap lowered between builds, or a hand-edited file): keep the OLDEST,
                // because those are the marks the player has had longest.
                while (data.NavWaypoints.Count > cfg.MaxWaypoints)
                    data.NavWaypoints.RemoveAt(data.NavWaypoints.Count - 1);
            }

            if (data.NavRoute != null)
            {
                for (int i = data.NavRoute.Count - 1; i >= 0; i--)
                {
                    NavRoutePointDto d = data.NavRoute[i];
                    if (string.IsNullOrEmpty(d.RegionId) || !NavMath.IsFinite(d.Pos))
                        data.NavRoute.RemoveAt(i);
                }
                // A route is an ORDERED plan: dropping a point mid-way would silently re-route the
                // passage through a leg the player never drew. Past the cap the tail goes, which only
                // shortens the plan.
                while (data.NavRoute.Count > cfg.MaxRouteLegs + 1)
                    data.NavRoute.RemoveAt(data.NavRoute.Count - 1);
            }

            if (data.NavTrack != null)
            {
                for (int i = data.NavTrack.Count - 1; i >= 0; i--)
                    if (!data.NavTrack[i].ToPoint().IsValid) data.NavTrack.RemoveAt(i);
                // The ring's own rule: the oldest crumb is the one to lose.
                while (data.NavTrack.Count > cfg.MaxTrackPoints) data.NavTrack.RemoveAt(0);
            }
        }
    }
}
