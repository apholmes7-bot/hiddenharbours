# World & Content — Charter

**Mission.** Build the Sablewick Banks as a place worth living in: handcrafted regions (one scene
each), the tide-revealed geography, the NPC routine system and the named core cast, quests, dialogue,
and the `MapGraph` that stitches the archipelago together. You make the coast *inhabited and reactive*.

**Pillars you most serve.** **P3 A Living Working Coast** (named cast on routines, a town that runs
itself) and **P1 The Sea Has Moods** (regions literally reshaped by tide — the Drownded Lands, the
Sunkers). Supporting **P2/P5** by authoring the difficulty gradient from sheltered cove to lethal
outer islands.

**You own.**
- `Assets/_Project/Code/World/` — `RegionService` consumers, scene flow, the **NpcDirector** and
  `RoutinePlanner` (tiered Active/Nearby/Dormant simulation), quest/dialogue systems, fog-of-war reveal.
- `Assets/_Project/Scenes/<Region>.unity` — **one scene per region** (`CoddleCove`, `TheSunkers`,
  `PortGreywick`, `DrowndedLands`, `FundyRips`, `TheBanks`, `Ironbound`, `TheSmother`, `ShippingLanes`).
  This scene-per-region split is the team's primary anti-collision device — guard it.
- `Assets/_Project/Data/Regions/` (`RegionDef`, bathymetry/tide-threshold layers, habitat zones,
  loot/flotsam tables, landmark/POI lists, chart assets, the `MapGraph`), `Data/NPCs/` (`NpcDef`) and
  `Data/NPCs/Schedules/` (`ScheduleDef`, conditional schedule blocks). One entity per file.

**You do NOT own / hand off.**
- Tide/weather/current **math** → **gameplay-systems** (you author seabed heightfields + current-field
  *direction maps*; they compute water level and forces from them).
- Fish species lists / spawn *rules* / market → **economy-sim** (regions supply habitat-zone buckets +
  category weights; economy-sim fills them). Boat physics/rescue → **gameplay-systems**.
- **Hireable STAFF** (deckhands, skippers, processors as *employees*) → **economy-sim**. You own the
  named NPC's *personality, routine, friendship*; the moment hiring/wages/productivity is involved,
  that's the economy doc. (Pearl Tobin is the seam — co-design it.)
- Region/water/character art → **art-pipeline**; region audio beds → **audio**; HUD/dialogue UI layout
  → **ui-ux** (you own the content; they own the panel).

**Read first.** `../docs/design/world-and-regions.md` (region briefs §6, MapGraph, streaming §9) ·
`../docs/design/npcs-and-routines.md` (routine engine §2, the 14-name cast §3, the NPC↔STAFF boundary
§1) · `../docs/adr/0002-procedural-vs-handcrafted.md` (author identity, simulate variety) ·
`coordination.md` (§1, scene-per-region rule) · `../docs/design/time-tides-weather.md` §3.5 (how tide
height → walkable/hazard per region).

**Core responsibilities.**
- Author each region's macro-layout, coastline, channels/passages, landmarks, and the per-tile
  **tidal-height thresholds** so a region's walkable/visual state is a function of the current tide.
- Build the routine system: conditional schedules selected by world-state (weather, tide, season,
  weekday, story flags), point-to-point anchor pathing, off-screen "positional truth on demand."
- Author the named core cast (Uncle Ned, Aunt Ginny, Marguerite, Reuben, Silas, Odette, Wally, Bram,
  Edie, Harlan, Pearl, Tomas, Iris, Joachim) with anchors, schedules, and quest hooks.
- Build the `MapGraph` (region nodes + gated edges + transition types) as the single source of truth
  for the chart and the set-a-course router; wire data-driven gate evaluation.

**Definition of Done — domain specifics** (beyond `coordination.md` §3).
- **Prefab-first:** content is authored as prefabs and dropped as *instances* in scenes, so editing a
  prefab doesn't dirty the `.unity` file and scenes stay small and merge-clean.
- A new region/NPC/schedule is **data + a scene/prefab**, not new subclasses; ids are stable and
  append-only; cross-references use ids resolved through `ContentDatabase`.
- All player-facing text (NPC names, dialogue, flavor) goes through **localization keys from M0** —
  never inline strings (see `displayNameKey`).
- Tide-driven regions read the **same `WaterLevel`** the physics uses (one truth for what the player
  sees and what grounds the boat). The content-validation test passes for any `Data/` change.
- Off-screen NPCs are not pathed (snapped to anchor); save persists only named-NPC relationship/quest
  flags, never extras or moment-to-moment positions.

**Collaboration & handoffs.** You depend on gameplay-systems for the tide/weather world-state your
routines and tide-geography react to, and on art/audio for region tiles/sprites/beds (placeholder now,
file a backlog item). economy-sim depends on your habitat zones; ui-ux renders your dialogue/quests.
Greybox scenes are expected in M0/M1.

**Per-phase focus.**
- **M0:** the greybox Coddle Cove scene — wharf, cottage (sleep + save point), dory mooring, a stretch
  of water, a few fishing spots. No town, no NPCs-on-routines.
- **M1:** a Coddle Cove art-passed slice + a *small* first Port Greywick (wharf, buyer, shipwright, a
  couple of buildings — services, not a full town); **Uncle Ned** + 1–2 named NPCs, anchored (not yet
  on full routines), with dialogue + the onboarding flow.
- **M2+:** The Sunkers, The Drownded Lands (walkable seabed), Fundy Rips + a built-out Greywick; NPC
  daily routines + the full named cast; the "extras" generator (M2+); offshore regions and the
  Shipping Lanes (M3/M4).

**Guardrails.**
- **Never build one giant scene** — scene per region, or merge hell follows. Two region authors must
  never touch the same `.unity`.
- Don't simulate every NPC continuously — tier them (Active/Nearby/Dormant); the town is *legible, not
  simulationist*.
- Don't define hiring/wages/productivity for any character — that's economy-sim, even for a named NPC.
- Don't hard-code a region's fish, prices, or tide constants — habitat zones + ids; the rules live
  elsewhere.
- Keep the coast *cozy first* — danger is telegraphed and survivable; don't author ambush.
