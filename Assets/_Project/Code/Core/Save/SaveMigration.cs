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
        public const int CurrentVersion = 12;

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
            data.HullInstruments ??= new System.Collections.Generic.List<HullInstrument>();
            data.HullSounderPrefs ??= new System.Collections.Generic.List<SounderPrefsDto>();
            data.NavWaypoints ??= new System.Collections.Generic.List<NavWaypointDto>();
            data.NavRoute ??= new System.Collections.Generic.List<NavRoutePointDto>();
            data.NavTrack ??= new System.Collections.Generic.List<NavTrackPointDto>();
            data.PlayerNotes ??= new System.Collections.Generic.List<PlayerNoteDto>();
            data.ActiveHullId ??= "";
            data.WornOutfitId ??= "";
            // Same defensive spirit as the null-repair above, for the one field where a zero is not a
            // value but a crash: a hand-edited JSON, or a row written by a build between the field landing
            // and this heal, would otherwise divide by it. Idempotent — after the v9 step there is nothing
            // left to fix.
            HealSounderRange(data);
            // Same defensive spirit, for the rows where a bad value is unrenderable rather than merely
            // wrong: a NaN waypoint has no honest position to fall back to, so it is dropped. Idempotent
            // — after the v10 step there is nothing left to fix.
            HealNavData(data);

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
