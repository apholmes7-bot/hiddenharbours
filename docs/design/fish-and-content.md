# Hidden Harbours — Fish & Content (DESIGN)

> Sibling docs: [`../vision-and-pillars.md`](../vision-and-pillars.md) (CANON — wins on conflict),
> [`time-tides-weather.md`](time-tides-weather.md), [`world-and-regions.md`](world-and-regions.md),
> [`boats-and-navigation.md`](boats-and-navigation.md), [`economy-and-business.md`](economy-and-business.md),
> [`../adr/0003-data-driven-content.md`](../adr/0003-data-driven-content.md).
>
> **Scope of this doc:** the **100 fish species** as *data*, the catch-resolution model, and the
> framework content agents use to author species safely in parallel. It does **not** define the
> market math (that lives in [`economy-and-business.md`](economy-and-business.md)) or the tide/weather
> simulation (that lives in [`time-tides-weather.md`](time-tides-weather.md)); it *consumes* both.

---

## 0. Design intent (which pillars this serves)

The fish system is the engine of **P1 The Sea Has Moods** and **P3 A Living Working Coast**, and the
fuel for **P2 From Dory to Dynasty**.

- **P1:** *What bites is a function of the sea's state.* The same cove at high tide vs. low tide, at
  dawn vs. dusk, in fog vs. sun, in spring vs. autumn, produces different catches. The player learns
  to *read* conditions and *choose* when and where to fish. Fishing is never "always the same water."
- **P2:** Species are gated up the ladder — beginner inshore fish in Coddle Cove, legendary cryptids
  out at Ironbound and The Smother. New boats unlock new grounds, which unlock new fish, which unlock
  new value. The catch ladder *is* the progression ladder, expressed as content.
- **P3:** A simulated NPC fleet also lands fish (see [`economy-and-business.md`](economy-and-business.md)
  §1), so supply on the market moves whether or not the player fishes. Species carry **supply
  elasticity** so the coast's economy breathes.
- **P5 (mild):** The catch mini-interaction is a light, cozy timing/tension action — *seasoning, not
  the meal*. Danger lives in weather and grounding (P1/P5), not in twitch-fighting fish.

**Hard constraint (from canon §5.7 & ADR-0003):** species are **data assets (ScriptableObjects)**,
never hand-coded. This doc defines the schema, the seed set, and an allocation framework so later
content agents can fill the remaining species without touching code or colliding with each other.

---

## 1. The `FishSpecies` data schema (ScriptableObject)

`FishSpecies` is an authored ScriptableObject asset. One asset = one species. The catch resolver and
the economy both read these assets; nothing about a species is special-cased in code.

### 1.1 Field reference

| Field | Type | Notes / authoring rules |
|---|---|---|
| `id` | string (stable) | Immutable, unique, lowercase-kebab, e.g. `atlantic-cod`. **Never reused or renamed** (save data & market state key off it). Display name can change; `id` cannot. |
| `displayName` | localized string | Player-facing, e.g. "Atlantic Cod". |
| `category` | enum `FishCategory` | One of the 8 categories in §2.1. Drives default elasticity, UI grouping, and which buyers want it. |
| `rarityTier` | enum `Rarity` | Common / Uncommon / Rare / Prize / Legendary (§2.2). Drives spawn weight band and value band. |
| `regions` | list<`RegionId`> | Which of the 7 core regions + commerce/late zones it appears in. Empty = appears nowhere (invalid; validator flags). Uses canon region ids (§ canon 5.3). |
| `depthBand` | flags `DepthBand` | `Tidepool, Shallows, Inshore, Midwater, Deep, Abyssal`. A species may span several. Must be reachable by gear the species also requires. |
| `tideWindow` | `TideWindow` struct | When (in the tidal cycle) it bites. See §1.2. The hook into **P1**. |
| `timeOfDayWindow` | `TimeWindow` struct | Hour ranges (0–24) with a weight curve; supports dawn/dusk/night/diel-vertical patterns. See §1.2. |
| `seasonModifiers` | `SeasonModifier[4]` | Per-season spawn-weight multiplier (Spring/Summer/Autumn/Winter) + optional size/value shift. Models runs & migrations (capelin roll, mackerel run, winter cod). |
| `weatherModifiers` | `WeatherModifier[]` | Multipliers keyed by weather/sea-state tags (`Calm, Wind, Rough, Fog, Storm, PostStorm, Rain`). The hook for "only in fog" / "best after a blow." |
| `requiredGear` | list<`GearTag`> | Gear that *can* take it (`Handline, Rod, Jigging, Gillnet, Trap, Dredge, Trawl, Longline, Pots, ClamFork, DipNet`). OR-set: any one listed gear qualifies. Empty = invalid. |
| `requiredBait` | list<`BaitTag>` (optional) | If non-empty, at least one listed bait must be loaded or the species is excluded (or heavily down-weighted — see `baitMode`). |
| `baitMode` | enum | `Required` (no bite without it) or `Preferred` (bite weight × `baitBonus` when present). Default `Preferred`. |
| `baitBonus` | float | Weight multiplier when a preferred bait is present (e.g. 1.5–3.0). |
| `sizeRange` | `SizeRange` struct | `minLengthCm, maxLengthCm, minWeightKg, maxWeightKg`, plus a roll curve (see §1.3). Drives value, "trophy" flags, and processing yields. |
| `baseValue` | int (coin) | Reference price **per kg** (most fish) or **per unit** (shellfish counted by piece — set `valuedBy`). The economy applies market modifiers on top (see [`economy-and-business.md`](economy-and-business.md) §1). |
| `valuedBy` | enum | `PerKg` or `PerUnit`. Lobster/crab/scallop typically `PerUnit`; finfish `PerKg`. |
| `supplyElasticity` | float (0–1) | How fast its price crashes when over-supplied / recovers when scarce. Higher = crashes faster. Defaults inherited from category (§2.1) but overridable per species. Consumed by [`economy-and-business.md`](economy-and-business.md) §1. |
| `perishability` | enum | `Hardy, Standard, Perishable, HighlyPerishable`. Raw-catch spoil rate (economy §3). Shellfish-alive and oily pelagics skew perishable. |
| `spriteRef` | AssetRef (Addressable) | Pixel-art sprite + icon set. Art per [`art-and-audio-bible.md`](art-and-audio-bible.md). May be a placeholder until art lands (validator allows a flagged placeholder). |
| `flavorText` | localized string | Short, warm, Maritime-voiced. Almanac entry copy. |
| `behaviorFlags` | flags `FishFlags` | `Legendary, FightsHard, FogOnly, StormOnly, NightOnly, SchoolingGlut, Migratory, Bottom, Cryptid, RequiresInstruments, TrophyEligible, QuestLocked`. Drives mini-interaction tuning and special spawn rules. |
| `minSkill` | int (optional) | Gate behind an angling/fishing skill level if desired (default 0). |
| `unlockCondition` | ref (optional) | Quest/flag gate for `QuestLocked`/legendary species (a `GameFlag` or quest id). Empty = ungated. |

> **Why per-kg + size-range, not fixed value:** a 12 kg cod should pay more than a 3 kg cod, and a
> trophy halibut should feel like a windfall. Value = `effectivePrice × weight (or count)`, where
> `effectivePrice` is the market-modified per-kg/per-unit price. This makes size genuinely matter and
> gives shellfish (counted) and finfish (weighed) sensible, different economics.

### 1.2 `TideWindow` & `TimeWindow` (the P1 hooks)

These reference the tide/clock model in [`time-tides-weather.md`](time-tides-weather.md); this doc only
*consumes* their outputs (current tide height %, tide state, hour).

**`TideWindow`** — any of three authoring modes (pick one per species, mix across species for variety):
- **By state:** weights for `{ Flood, HighSlack, Ebb, LowSlack }`. (e.g. striped bass favor moving
  water — high `Flood`/`Ebb`.)
- **By height band:** min/max tide height % (0 = lowest astronomical, 100 = highest). (e.g. clams on
  the flats only accessible when height < 20%.)
- **By spring/neap:** weight on big-range spring tides vs. small neaps (e.g. some Fundy Rips pelagics
  feed hard on spring-tide currents).

A species with an empty `TideWindow` is tide-agnostic (uniform). **Authoring guidance:** ~60–70% of
species should carry a non-trivial tide window so the tide table stays a meaningful tool (P1).

**`TimeWindow`** — list of `{ startHour, endHour, weight }` segments forming a 24h weight curve.
Convenience presets: `Dawn (04–07)`, `Day (07–18)`, `Dusk (18–21)`, `Night (21–04)`, `Crepuscular`
(dawn+dusk peaks), `Diel` (deep by day, shallow by night — pairs with `depthBand`).

### 1.3 Size roll & trophies

On a successful catch, length is rolled on a right-skewed curve (most fish near the lower-mid of the
range, big ones rare); weight is derived from length via a per-category length–weight relation
(`weight ≈ a · length^b`, `a`/`b` set by `bodyType` so eels, flatfish, and tuna scale differently).
A catch in the top `trophyPercentile` (default top 5%) and above `trophyEligible` length flags as a
**trophy** (almanac record, optional mount in the cottage — see
[`progression-and-housing.md`](progression-and-housing.md)).

---

## 2. Categories & rarity

### 2.1 Categories (`FishCategory`)

Eight categories. Category sets **default supply elasticity** and **default perishability** (a species
may override). Category also tells the economy which buyers/markets want it (economy §1.4) and groups
the almanac UI.

| Category | Identity | Typical regions | Gear that takes it | Default elasticity | Default perish |
|---|---|---|---|---|---|
| **Inshore Groundfish** | Bread-and-butter bottom fish near home | Coddle Cove, Sunkers, Greywick approaches | Handline, Jigging, Longline, Gillnet | 0.45 (steady demand, floods if overfished) | Standard |
| **Shellfish & Crustaceans** | Traps, dredges, hand-digging; counted by piece | Sunkers, Drownded Lands, Coddle Cove | Trap, Pots, Dredge, ClamFork | 0.35 (premium, sticky demand) | Perishable (often sold/kept alive) |
| **Pelagic** | Fast, schooling, mid/surface; runs & migrations | Fundy Rips, Banks, Coddle Cove (seasonal) | Jigging, Gillnet, Rod, DipNet | 0.60 (gluts hard during runs) | HighlyPerishable (oily) |
| **Tidepool & Flats** | Small, tide-gated, hand-gathered; bait & curios | Sunkers (pools), Drownded Lands (flats) | DipNet, ClamFork, Handline | 0.50 | Perishable |
| **Deepwater / Banks** | Big offshore groundfish & deep species | The Banks, Ironbound | Trawl, Longline, Jigging | 0.40 | Standard |
| **Storm-grounds / Ironbound** | Cold-water, rough-weather, high-value rarities | Ironbound, The Banks (edges) | Trawl, Longline, Jigging | 0.30 (rare, holds value) | Standard |
| **Estuary / Brackish** | River-mouth & migratory species | Drownded Lands, Coddle Cove rivers, Greywick estuary | Rod, Gillnet, Trap, DipNet | 0.55 | Perishable |
| **Legendary / Cryptid** | Named, gated, story/ambience catches | Ironbound, The Smother, special spots | Varies (often special gear/instruments) | 0.10 (one-off; minimal market effect) | Hardy (kept as trophy/quest item) |

### 2.2 Rarity tiers (`Rarity`)

Rarity sets the **base spawn-weight band** (relative likelihood in a roll) and a **value band**
(multiplier guidance on `baseValue` within its category). These are *bands*, not fixed numbers — a
species' final weight is `rarityBaseWeight × tide × time × season × weather × gear × bait` (§3).

| Tier | Spawn-weight band | Value band (× category typical) | Implication |
|---|---|---|---|
| **Common** | 60–100 | 0.6–1.0× | The daily catch. You will see these constantly; they anchor the economy and early income. |
| **Uncommon** | 25–55 | 1.0–1.8× | A pleasant mix-in; needs reasonable conditions/gear. |
| **Rare** | 8–22 | 1.8–4× | A good day. Often tied to a specific tide/time/weather/season window. |
| **Prize** | 2–7 | 4–12× | A trophy-feeling catch; tight windows, better grounds, good gear. A genuine payday. |
| **Legendary** | 0.2–1.5 (or quest-gated, weight 0 until unlocked) | 15–60× or fixed quest value | Named, rare, often one-per-save or long-cooldown. Memorable, not farmable. |

---

## 3. Catch resolution model

The resolver answers one question every time the player commits a cast/haul: **given the full sea
state and the player's setup, what (if anything) bites, and how big?** It is a **weighted spawn
table** assembled on demand from the `FishSpecies` assets that match the context — no per-spot
hand-authored loot tables.

### 3.1 Inputs (the "fishing context")

```
FishingContext {
  RegionId       region          // where the boat/player is
  DepthBand      depthHere        // from local bathymetry at this spot
  TideState      tideState; float tideHeightPct; bool isSpringTide   // from tide sim
  float          hour             // 0..24 from clock
  Season         season
  WeatherTags    weather          // Calm/Wind/Rough/Fog/Storm/PostStorm/Rain...
  GearTag        gearEquipped
  BaitTag        baitLoaded        // may be None
  int            anglerSkill
  Set<GameFlag>  unlockedFlags     // for quest/legendary gating
  Vector2        spotJitter        // local micro-spot seed (tide pools, hotspots)
}
```

### 3.2 Candidate filter → weighting

1. **Hard filter** — drop any species that fails a binary gate:
   - region not in `regions`; `depthHere` not in `depthBand`;
   - `requiredGear` doesn't include `gearEquipped`;
   - `baitMode == Required` and `baitLoaded` not in `requiredBait`;
   - `anglerSkill < minSkill`; `QuestLocked`/legendary `unlockCondition` not satisfied;
   - `FogOnly`/`StormOnly`/`NightOnly` flags whose condition isn't met.

2. **Compute weight** for each survivor:
   ```
   weight = rarityBaseWeight
          × tideFactor(tideState, tideHeightPct, isSpringTide)   // from TideWindow
          × timeFactor(hour)                                      // from TimeWindow
          × seasonModifier[season]
          × weatherModifier(weather)                              // 1.0 if no tag matches
          × gearAffinity(gearEquipped)                            // optional per-gear bonus
          × (baitMode==Preferred && baitLoaded∈requiredBait ? baitBonus : 1.0)
          × skillSoftBonus(anglerSkill)                           // gentle, not gatekeeping
   ```
   Any factor that drives weight to ~0 effectively removes the species for this cast (e.g. a
   strictly diurnal fish at 3 a.m.).

3. **Add the "nothing/junk" entries.** The table always includes a `MissOrFlotsam` pseudo-entry
   (empty hook, seaweed, an old boot, flotsam — ties to flotsam in canon §5.7) with a weight that
   *rises* in bad conditions and poor spots, so fishing the wrong place/time has a real (cozy) cost.

4. **Normalize & roll.** Pick one entry proportional to weight. On a fish, roll **size** (§1.3), then
   hand off to the **mini-interaction** (§3.4). On a miss/flotsam, resolve that instead.

> **Determinism & feel:** the roll uses a seeded RNG keyed off (spot, in-game time bucket,
> save-seed) so a given hotspot at a given moment is consistent within a short window (rewards
> reading the spot), but rerolls as conditions/time advance (P1). `spotJitter` lets tide pools and
> flats have stable micro-spots ("the good pool by the third sunker").

### 3.3 Hotspots & schools (optional surface layer)

To make grounds *legible* (P3 "living coast") and reward observation, the world may render **visible
cues** — bird activity, surface boils, ripples, bubbles over a clam bed — which are just a temporary
**weight multiplier** applied to matching species/categories in a small area, plus an icon. A
`SchoolingGlut` pelagic during its seasonal run can spawn a moving school cue worth chasing. These are
data-light: a hotspot is `{ area, categoryOrSpeciesFilter, weightMult, lifespan, vfxRef }`, spawned by
a lightweight ambient system. Hotspots are flavor + nudge, never required to catch anything.

### 3.4 The catch mini-interaction (cozy, P5-mild)

One simple, **touch-first** action covers all rod/handline/jig catches; passive gear (traps, dredges,
nets, clam-digging) uses a lighter variant.

- **Active (line/rod/jig):** a **tension meter**. After a bite, a marker drifts within a "keep-tension"
  band; the player **holds to reel / releases to give line**, keeping the marker in the band as it
  wanders. A short timer fills a **landing gauge**; fill it before the line's **strain bar** maxes
  (too much tension too long → the fish throws the hook / line parts). One thumb, no precision twitch.
  - `FightsHard` widens the wander and speeds drift (legendaries, big tuna/halibut): longer, tenser,
    but still readable and forgiving — *teeth, not a reflex test*.
  - Higher angler skill / better gear **narrows** the required band's difficulty and widens the keep
    zone. This is the P4 payoff: hand-fishing gets easier as you master it, *then* you delegate it.
- **Passive (traps/pots/dredge/gillnet/clam fork):** no tension fight. You **set/haul** and resolve a
  small "tend" beat — a quick tap-to-haul with a light quality bump for good timing/full soak. This is
  the gear the automation layer (economy §5) eventually runs for you.
- **Fail states are cozy:** a lost fish = "it threw the hook," you keep your gear and bait-or-not; no
  damage, no death. Real danger stays in weather/tide/grounding (P1/P5), per anti-pillars.

> **Mobile/UX:** exact control mapping, haptics, and one-handed layout live in
> [`ux-and-mobile-controls.md`](ux-and-mobile-controls.md); this doc fixes only the *model* (tension band + landing
> gauge + strain bar) and its feel.

### 3.5 Phased gear & content — St Peters clam-dig · lobster gaffing · aquaculture leases

> **All future work, captured here for consistency — none of it is in the M0/M1 slice** (CLAUDE.md
> rule 8). The **St Peters clam-dig** ships with the St Peters opening (**M2**); the **lobster**
> gaffing loop ships with the lobster gear / specialist branch (**M2**); **aquaculture leasing** is
> advanced/late (**M3**). Phasing: [`../roadmap.md`](../roadmap.md). On-water interactions:
> [`boats-and-navigation.md`](boats-and-navigation.md).

**(a) St Peters clam-dig — shovel + the "two squirting holes" tell (M2). The FIRST catch, before any
rod.** The opening's by-hand income ([`world-and-regions.md`](world-and-regions.md) §6.0, canon
[`../vision-and-pillars.md`](../vision-and-pillars.md) §5.8). Clams are the player's **very first
"catch" — earned by hand on the bared sandbar/flats before they own a rod or a boat**. On the bared
low-water flats, buried clams betray themselves with **two little squirting holes** in the wet sand;
the player reads the tell, **digs with a shovel**, and pulls the clam. Mechanically this is the
**passive "tend" beat** of §3.4 (read-the-spot + dig, no tension fight), tuned cozy. It is
**licence-gated** by a **clam licence** bought at the St Peters general store. *Ownership note (this
wave):* the licence system itself — including the clam licence and the **cod fishing licence** bought
later at Greywick (the gate on rod-fishing) — is a **real, minimal licence system owned by
[`economy-and-business.md`](economy-and-business.md)** (not authored here, and not the
`progression-and-housing.md` currency table's job to implement); this doc only declares that clams are
**gated** behind it. *Data note:* clam content already exists (`soft-shell-clam`, `blue-mussel`; gear
tag `ClamFork`). The **shovel** is the St Peters flavour of the `ClamFork` / hand-dig method —
**reconcile at M2** whether to add a distinct `Shovel`/`ClamRake` `GearTag` or treat the shovel as the
`ClamFork` tag's presentation (a new tag touches the enum and is review-gated — §6.1). The **rod** (and
the cod licence that gates rod finfishing) is bought at **Greywick**, after the sandbar walk — so St
Peters' economy is clams-by-hand only. The "two squirting holes" is a **spot tell** (a hotspot cue,
§3.3), not a new system.

**(b) Lobster — trap + buoy + bait, hauled by gaffing the buoy (M2).** Lobster already exists
(`american-lobster`: `Pots`/`Trap`, **Required** bait herring/mackerel, `/unit`, kept alive). The
**loop** the owner specifies: **set a baited trap** marked by a **surface buoy**; later **return,
bring the boat alongside the buoy, leave the helm (step to port or starboard), and gaff the buoy to
haul the trap**. This **dive-beside-the-trap + leave-the-helm-to-haul** interaction and the
**electric-winch upgrade** (powered hauling on boats that mount it) are **on-water boat interactions
owned by [`boats-and-navigation.md`](boats-and-navigation.md)** (the trap-hauler gear mount, §4.2 /
§6.1 there); this doc owns only the **species + bait + soak** side. Without the winch the haul is a
**stamina action** (P4, "by hand first"); the winch automates the tedium later (P4). Trap soak /
bycatch follows the passive-gear model (§3.4 and the multi-catch open question §7.2).

> **Trap runtime status (arc builds).** The trap arc ships in slices: **Build 1** the wave-driven buoy
> (visual), **Build 2** the trap/bait/crab **content** (`TrapDef`/`BaitDef` + the lobster/crab pot and
> herring/mackerel/fish-scrap assets), **Build 3** the **logical `PlacedTrap` runtime** — a deterministic
> **soak** (ready when `now − placedAt ≥ SoakHours·3600`, recomputed not saved, rule 5) and a **seeded
> catch** resolved on-demand from a *stable* hash of `(worldSeed, InstanceId, placementTime)` so a
> save→load→haul lands the **identical** catch. The catch reuses the existing `CatchResolver` over the
> trap's `AllowedCatchFishIds` (resolved id→Def at runtime via a Resources-loaded `FishSpeciesLibrary`);
> the loaded **bait soft-weights** the roll toward its `FavorsSpeciesIds` (a nudge, both catches stay
> possible — owner's call), it does **not** hard-gate. Build 3 wires this to save/restore
> (`SaveData.PlacedTraps`) and a **dev key** (drop / check-haul) only. The **depth-gated placement** (the
> `Min/MaxSoakDepthMeters` band) and the real **gaff-the-buoy haul interaction** are **Build 4**.
>
> **Build 7 — the post-haul DECK WORK (owner ask 2026-07-12).** A surfacing haul no longer lands the
> catch instantly: the **pot lands on the deck** with the Build-3 catch still inside (WHAT was caught is
> untouched — only when/how it lands changed), and the player works her by hand on deck (`OnDeck`):
> **pick** each animal out (a HOLD, released — a grab can get **nipped**: recoil, animal stays, try
> again; a fuller hold is a safer grab), **sort** it by a deterministic per-animal **size** (and
> **berried** flag) — shorts and berried hens splash back over the side, value zero (the honest-fishery
> read) — **band** each keeper's claws (a second short hold; only a banded keeper lands, through the
> unchanged `FishCaught` path), and **re-bait** the emptied pot by hand (consumes one `BaitStock` — the
> physical replacement for the abstract at-placement charge; the T-set of a worked pot charges nothing
> extra). All rules and feel numbers are data: the per-species gauge/size-window/berried rules live in a
> **`DeckWorkDef`** (`deckwork.pot`, Data/Traps) a `TrapDef` opts into (no Def → the old instant land).
> Per-animal size/berried/nip streams hash off the SAME seed lineage as the catch roll
> (`worldSeed + instanceId + placementTime` + species + index + channel — rule 5, EditMode-pinned).
> **Save (ADR 0020, greybox compromise):** a pot on deck is transient like `ControlMode` — its DTO
> leaves the save at pot-aboard (no re-haul dupes) and a load/region change **auto-resolves** it cozily
> (keepers land per the deterministic sort, one toast). Persisting a mid-sort pot needs an ADR — deferred.

**(c) Aquaculture — mussel/oyster leases, buoys-in-series, season-grown (M3, advanced/late).** A new
**farmed-shellfish** path distinct from wild hand-gathering: you **lease a patch of water**, set
**buoys in series** with grow-ropes/socks beneath, seed them, and the crop **grows over a season**
(mussels, oysters) before harvest. This is **slow, owned, passive production** — pure **P4** ("earn it,
then automate it") and **P2** scale — and leans on the **leasing / property** mechanics in
[`progression-and-housing.md`](progression-and-housing.md) §4.4 and the **value-add / contracts** in
[`economy-and-business.md`](economy-and-business.md). *Content shape:* farmed mussel/oyster are **not**
new wild `FishSpecies` rolls — they are **harvested from a lease at maturity** (a grow-timer + yield),
so they live closer to the production/economy layer than the catch resolver. Capture only — **design
the full lease → grow → harvest loop at M3** with the economy doc (where the grow-timer, water-lease
cost, and seasonal yield live is an M3 joint pass). Natural home grounds: sheltered inshore (Coddle
Cove, the Sunkers, the Drownded Lands edges).

---

## 4. Seed species (concrete examples against the schema)

These ~24 fully-specified entries are **canonical exemplars** — they ship, *and* they're the reference
for content agents authoring the rest. Real Atlantic-Canada species are tuned to plausible local
behavior; the four legendaries are invented Maritime cryptids. Values are reference coin (the economy
re-prices live). "Tide/Time" abbreviates the windows; full structs live in the assets.

> Notation — Rarity: C/U/R/P/L. Region ids: CC=Coddle Cove, SK=The Sunkers, PG=Port Greywick (approaches/estuary),
> DL=The Drownded Lands, FR=Fundy Rips, BK=The Banks, IB=Ironbound, SM=The Smother. `PerKg` unless noted `/unit`.

### 4.1 Inshore Groundfish

| id / Name | Rarity | Regions | Depth | Tide / Time | Season | Weather | Gear | Bait | Size (len cm / wt kg) | Base | Elas. | Perish | Flags / flavor |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| `atlantic-cod` / Atlantic Cod | U | CC, SK, BK | Inshore, Deep | Ebb+Flood / Day, better Dawn | Autumn↑↑, Winter↑ | Calm/Wind ok | Handline, Jigging, Longline | Capelin, Squid (Pref ×1.8) | 40–120 / 1–25 | 9 | 0.45 | Standard | The hold-filler. "The fish that built every wharf on the Banks." |
| `haddock` / Haddock | U | CC, BK | Inshore, Deep | Flood / Day | Spring↑, Summer | Calm pref | Longline, Jigging, Trawl | Clam, Squid (Pref ×1.6) | 35–70 / 0.5–4 | 11 | 0.45 | Standard | Cleaner cousin of cod; the chip-shop favourite. |
| `pollock` / Pollock | C | CC, SK, BK | Inshore, Midwater | Flood+Ebb / Day | Summer, Autumn↑ | Wind ok | Jigging, Handline, Gillnet | Mackerel strip (Pref ×1.5) | 40–90 / 1–10 | 6 | Standard | 0.50 | Hard-pulling, plentiful. A good day's wage if cod won't bite. |
| `cusk` / Cusk | R | SK, BK, IB | Deep | LowSlack / Night | Winter↑ | Rough ok | Longline, Jigging | Squid (Pref ×1.7) | 45–95 / 1–12 | 14 | 0.40 | Standard | Eel-shouldered bottom-hugger from the rough ground. |

### 4.2 Shellfish & Crustaceans (`/unit` where counted)

| id / Name | Rarity | Regions | Depth | Tide / Time | Season | Weather | Gear | Bait | Size | Base | Elas. | Perish | Flags / flavor |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| `american-lobster` / American Lobster | U | SK, CC, IB | Shallows, Inshore | Any / Night-set | Summer↑, Autumn↑↑ | Calm pref | Pots, Trap | Herring, Mackerel (Required) | 20–55 / 0.4–4 `/unit` | 28 /unit | 0.35 | Perishable(alive) | The prize of the inshore. Kept alive in the well. |
| `snow-crab` / Snow Crab | R | BK, IB | Deep | Any / — | Winter↑, Spring↑↑ | Rough ok | Pots, Trap | Herring (Required) | 8–16 span / 0.5–1.4 `/unit` | 22 /unit | 0.35 | Perishable | Cold-water crab off the Banks; spring is the season. |
| `rock-crab` / Rock Crab | C | CC, SK | Tidepool, Shallows | Low half / Day | Summer | Any | Trap, ClamFork(by hand) | Fish scrap (Pref ×1.4) | 7–13 span / 0.1–0.4 `/unit` | 4 /unit | 0.45 | Perishable | Underfoot in every pool; good bait, modest sale. |
| `sea-scallop` / Sea Scallop | R | BK, SK | Inshore, Deep | Any / Day | Spring, Autumn | Calm pref | Dredge | — | 9–17 shell / — `/unit` | 18 /unit | 0.35 | Perishable | Dredged from gravel beds; the meat sells, packaged sells better. |
| `blue-mussel` / Blue Mussel | C | SK, DL, CC | Tidepool, Shallows | Low half (height<35%) / — | All | Any | ClamFork(hand), DipNet | — | 4–8 / — `/unit` | 1.2 /unit | 0.50 | Perishable | Hand-gathered off the rocks at low water. Cheap, reliable. |
| `soft-shell-clam` / Soft-shell Clam | C | DL, SK | Tidepool | Height<20% (low only) / Day | Spring↑, Summer↑ | Any | ClamFork | — | 5–10 / — `/unit` | 2 /unit | 0.45 | Perishable | Dug from the flats when the tide bares them. The Drownded Lands' staple. |

### 4.3 Pelagic

| id / Name | Rarity | Regions | Depth | Tide / Time | Season | Weather | Gear | Bait | Size | Base | Elas. | Perish | Flags / flavor |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| `atlantic-mackerel` / Atlantic Mackerel | C | CC, FR, BK | Midwater, Shallows | Flood / Dawn+Dusk | Summer↑↑, Autumn↑ | Calm/Wind | Jigging, Rod, Gillnet | Shiny lure (Pref ×2) | 25–40 / 0.2–0.7 | 3 | 0.60 | HighlyPerish | `SchoolingGlut, Migratory`. The summer run: easy buckets, crashes the price. |
| `atlantic-herring` / Atlantic Herring | C | CC, FR, BK | Midwater | Ebb / Dusk+Night | Spring↑, Autumn↑ | Calm | Gillnet, DipNet | — | 20–35 / 0.1–0.5 | 2 | 0.60 | HighlyPerish | `SchoolingGlut, Migratory`. Bait, food, fishmeal — the base of the chain. |
| `capelin` / Capelin | U | CC, IB, DL | Shallows | High half / Dusk+Night | **Summer (the roll)** ↑↑↑ | Calm | DipNet, Gillnet | — | 13–20 / 0.02–0.05 | 2 | 0.60 | `SchoolingGlut, Migratory`. The capelin roll: they wash ashore in summer. Prime cod bait. |
| `bluefin-tuna` / Bluefin Tuna | P | FR, BK, IB | Midwater, Deep | Flood (moving water) / Dawn | Summer↑, Autumn↑↑ | Wind ok | Rod, Longline | Mackerel, Herring (Required) | 150–300 / 100–500 | 30 | 0.50 | `FightsHard, Migratory, TrophyEligible`. A single fish is a payday. The fight of your life. |

### 4.4 Tidepool & Flats

| id / Name | Rarity | Regions | Depth | Tide / Time | Season | Weather | Gear | Bait | Size | Base | Elas. | Perish | Flags / flavor |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| `rainbow-smelt` / Rainbow Smelt | U | DL, CC, PG | Shallows, Tidepool | High half / Night | Winter↑↑, Spring↑ | Cold/Calm | DipNet, Rod | — | 13–22 / 0.02–0.1 | 4 | 0.50 | Perishable | `NightOnly`-ish. Dipped through winter ice-edges and estuaries; a delicacy fried whole. |
| `american-eel` / American Eel | U | DL, PG, CC | Tidepool, Shallows | Ebb / Night | Autumn↑↑ | Rain↑ | Trap, Handline | Worm, Clam (Pref ×1.8) | 40–90 / 0.3–2 | 8 | 0.50 | Standard | `NightOnly`. Slips the flats and river-mouths after dark; smokes beautifully. |

### 4.5 Deepwater / Banks

| id / Name | Rarity | Regions | Depth | Tide / Time | Season | Weather | Gear | Bait | Size | Base | Elas. | Perish | Flags / flavor |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| `atlantic-halibut` / Atlantic Halibut | P | BK, IB | Deep, Abyssal | Slack / Day | All, Summer↑ | Rough ok | Longline, Trawl | Squid, Herring (Required) | 90–250 / 10–200 | 24 | 0.40 | Standard | `FightsHard, Bottom, TrophyEligible`. The flat giant of the Banks. Hauls like a barn door. |
| `acadian-redfish` / Acadian Redfish | U | BK, IB | Deep | Any / Day | All | Rough ok | Trawl, Longline | — | 25–45 / 0.4–1.5 | 10 | 0.40 | Standard | `Bottom`. Slow-growing deep rockfish; comes up in numbers in the trawl. |
| `monkfish` / Monkfish | R | BK, SK | Deep | LowSlack / Night | Winter↑ | Rough | Trawl, Longline | — | 50–120 / 3–25 | 16 | 0.40 | Standard | `Bottom`. Ugly as sin, sells as "poor man's lobster." The tail is the prize. |

### 4.6 Estuary / Brackish

| id / Name | Rarity | Regions | Depth | Tide / Time | Season | Weather | Gear | Bait | Size | Base | Elas. | Perish | Flags / flavor |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| `striped-bass` / Striped Bass | R | PG, DL, CC | Shallows, Inshore | **Flood+Ebb (moving water)** / Dawn+Dusk | Summer↑↑, Autumn↑ | Wind↑, PostStorm↑↑ | Rod, Handline, Gillnet | Mackerel, Eel, Herring (Pref ×2) | 45–120 / 1–25 | 17 | 0.55 | Perishable | `FightsHard, TrophyEligible`. Hunts the rips and river-mouths on the moving tide. The inshore sport prize. |

### 4.7 Storm-grounds / Ironbound

| id / Name | Rarity | Regions | Depth | Tide / Time | Season | Weather | Gear | Bait | Size | Base | Elas. | Perish | Flags / flavor |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| `lumpfish` / Lumpfish | R | IB, SK | Shallows, Inshore | High half / Day | **Spring (roe season)** ↑↑↑ | Rough ok | Gillnet, Handline | — | 25–50 / 1–5 | 13 (roe ↑↑) | 0.30 | Perishable | `Bottom`. Knobbly cling-fish of cold rock; the spring roe ("poor caviar") is the real money. |

> Storm-grounds also draws its rarest catches from the **Legendary/Cryptid** set below.

### 4.8 Legendary / Cryptid (invented — Maritime-voiced)

These are gated, named, memorable. Weight 0 until `unlockCondition` met; long cooldown or one-per-save.

| id / Name | Regions | When it appears | Gear / Bait | Size | Base | Flags / flavor |
|---|---|---|---|---|---|---|
| `the-sunker-king` / The Sunker King | SK, IB | **Spring tide + lowest water + fog**, over the deepest sunker. Dawn slack only. | Longline; bait = aged `lumpfish` or `american-eel` (Required) | 180–260 / 60–140 | 1,400 | `Legendary, Cryptid, FightsHard, FogOnly, Bottom`. A cod the size of a man, white-eyed and barnacled. They say he was old when your uncle was young. |
| `fundys-grey-mare` / Fundy's Grey Mare | FR, BK | **Peak spring-tide rip, post-storm**, slack at the top of the flood. | Rod/Longline; bait = live `atlantic-mackerel` (Required) | 220–340 / 120–400 | 1,900 | `Legendary, Cryptid, FightsHard, StormOnly, Migratory, TrophyEligible`. A tuna-shaped shadow that runs the rips when the sea's still angry. Surfaces grey as weathered rope. |
| `the-drownded-bride` / The Drownded Bride | DL, SM | **Lowest spring low, deep fog**, out where the flats meet the channel. Night. | Handline/DipNet; bait = none — comes to a lantern (`RequiresInstruments`) | 60–110 / 1–4 | 1,200 | `Legendary, Cryptid, FogOnly, NightOnly, RequiresInstruments`. A pale, trailing eel-thing that follows a light across the bared seabed. Don't follow it back. |
| `the-smother-lantern` / The Smother Lantern | SM | **Inside the permanent fog bank only**, navigating by instrument. Any deep slack. | Jigging; `RequiresInstruments` (sounder + compass), bait = `capelin` (Pref) | 30–70 / 0.5–3 | 1,600 | `Legendary, Cryptid, FogOnly, RequiresInstruments, Abyssal`. A deep fish that glows faintly, like a drowned lamp. The Smother's only reliable landmark — and it moves. |

---

## 5. Allocation table (all 100 species)

This distributes the **100 species** across **category × rarity**, with a **region spread** column so
later content agents fill a consistent framework. The seed species above are counted in their buckets.
**Variety comes from gating (tide/time/season/weather/gear), not from headcount** — these counts are
deliberately modest because each species multiplies into many "appearances" via its windows.

### 5.1 Category × rarity counts (target = 100)

| Category | C | U | R | P | L | **Total** |
|---|---:|---:|---:|---:|---:|---:|
| Inshore Groundfish | 5 | 5 | 3 | 1 | 0 | **14** |
| Shellfish & Crustaceans | 5 | 4 | 3 | 1 | 0 | **13** |
| Pelagic | 4 | 4 | 2 | 2 | 0 | **12** |
| Tidepool & Flats | 6 | 4 | 2 | 0 | 0 | **12** |
| Deepwater / Banks | 3 | 4 | 3 | 2 | 0 | **12** |
| Storm-grounds / Ironbound | 2 | 3 | 4 | 2 | 0 | **11** |
| Estuary / Brackish | 4 | 3 | 2 | 1 | 0 | **10** |
| Legendary / Cryptid | 0 | 0 | 0 | 0 | 8 | **8** |
| **Subtotal** | **29** | **27** | **19** | **9** | **8** | **92** |
| *Flex reserve* (any category, for tuning gaps / DLC / events) | 3 | 3 | 1 | 1 | 0 | **8** |
| **TOTAL** | **32** | **30** | **20** | **10** | **8** | **100** |

> The **flex reserve** lets balance/content agents add seasonal-event fish or fill a thin region
> without renumbering. Keep the grand total at 100 (canon §5.7).

### 5.2 Region spread (where each category's species should appear)

Each species lists 1–3 regions in `regions`; this is the *intended center of gravity* so the world
fills evenly and gating up the boat ladder (P2) stays clean. (Counts below are "species whose primary
home is here," and overlap is expected.)

| Region (canon §5.3) | Primary categories | Approx. primary species | Notes (the source of variety) |
|---|---|---|---|
| **Coddle Cove** | Inshore Groundfish, Shellfish, Tidepool, some Pelagic | ~16 | Beginner-safe. Heavy tide/time gating teaches P1. Seasonal pelagic runs visit. |
| **The Sunkers** | Tidepool & Flats, Shellfish, Inshore | ~16 | Low-water access gating (height bands). Grounding hazard pairs with shellfish reward. |
| **Port Greywick** (estuary/approaches) | Estuary/Brackish, some Inshore | ~8 | River-mouth & brackish species; gentle, social hub waters. |
| **The Drownded Lands** | Tidepool & Flats, Estuary, Shellfish | ~14 | Strong low-tide gating (walkable seabed). Clams/smelt/eel; wreck-tied rarities. |
| **Fundy Rips** | Pelagic, some Deepwater | ~10 | Spring/neap and moving-water gating. Fast pelagics; bluefin; Grey Mare legendary. |
| **The Banks** | Deepwater/Banks, Pelagic, offshore Shellfish | ~18 | Dragger-class gate. Big groundfish, snow crab, scallop, tuna, halibut. |
| **Ironbound** | Storm-grounds, Deepwater, rare Shellfish | ~14 | Weather-gated. Highest-value rarities; Sunker King; cold-water specialists. |
| **The Smother** (late) | Legendary/Cryptid only | ~4 | Instrument-gated. Cryptids: Drownded Bride, Smother Lantern, plus 1–2 reserved. |

### 5.3 Seasonal/tide gating as the variety engine (authoring rule for content agents)

To keep 100 species feeling like *hundreds of distinct fishing situations*, every authored species
**must** declare at least **two** non-trivial conditional windows from this set, and the *set as a
whole* must stay balanced:

- A **tide window** (state, height band, or spring/neap) — target ~65% of species non-trivial.
- A **time-of-day window** — target ~70% non-trivial (avoid everything being "Day").
- A **season modifier** with a real peak — target ~60% with a clear best season.
- A **weather modifier** — target ~40% (reserve `Fog/Storm` peaks mostly for R/P/L).

Balance dashboards (§6.2) report these coverage percentages so no single condition dominates and so
**every season, every tide state, and every weather** has worthwhile fish to chase (P1, P3).

---

## 6. Implementation notes

### 6.1 ScriptableObject authoring

- One `FishSpecies` asset per species under `Assets/Content/Fish/<category>/<id>.asset`. The `id` is the
  filename stem and is validated unique on import.
- Enums (`FishCategory`, `Rarity`, `DepthBand`, `GearTag`, `BaitTag`, `FishFlags`, `RegionId`,
  `Season`, weather tags) are code; **species are data**. Adding a species requires **no code change**.
  Adding a *new gear/bait tag or category* is the only thing that touches code (and is rare/reviewed).
- Region ids and `Season` come from shared canon enums so [`world-and-regions.md`](world-and-regions.md)
  and [`time-tides-weather.md`](time-tides-weather.md) stay the single source of truth.
- Sub-structs (`TideWindow`, `TimeWindow`, `SeasonModifier`, `WeatherModifier`, `SizeRange`) are
  `[Serializable]` with custom property drawers for friendly inline editing (curve fields, tide-band
  sliders, a 24h time-curve widget).
- Localized fields (`displayName`, `flavorText`) reference the localization tables, not raw strings.
- Art (`spriteRef`) is an **Addressable** ref; a placeholder sprite is allowed but flagged so art can
  be authored asynchronously (see [`art-and-audio-bible.md`](art-and-audio-bible.md)).

### 6.2 Spawn-table tooling & validation

- The resolver builds candidate tables **at runtime** from assets (§3); there is no separate
  per-region loot-table asset to maintain. An **indexer** pre-buckets species by `region`/`depthBand`
  at load so the per-cast filter is cheap on mobile.
- **Validator (CI + editor):** flags species with empty `regions`/`requiredGear`; gear/bait/depth
  that can't actually take the species (e.g., `Trap` gear but `Abyssal`-only depth with no deep trap);
  `Required` bait with no obtainable bait in those regions; duplicate `id`; missing `spriteRef`
  (warning); legendary without `unlockCondition`.
- **Balance dashboard (editor window):** rolls the resolver thousands of times across a grid of
  (region × tide × time × season × weather × gear) and reports: catch-mix per context, value/hour
  estimates, % of contexts with no worthwhile catch (should be low but non-zero — bad spots exist),
  and the §5.3 coverage percentages. This is how designers keep the economy and P1 honest.
- A small **"day-in-the-life" sim** can fast-forward a simulated angler over a season to sanity-check
  income against the economic progression curve in [`economy-and-business.md`](economy-and-business.md) §7.

### 6.3 How content agents add species safely in parallel

1. **Claim a bucket** from §5.1/§5.2 (category × rarity × primary region) so two agents don't double up.
2. **Copy a seed exemplar** of the same category as a template; change `id` first (unique).
3. Fill all required fields; declare **≥2 conditional windows** per §5.3; set elasticity/perishability
   (inherit category default unless there's a reason).
4. Run the **validator** (must pass) and check the **balance dashboard** delta (your species shouldn't
   dominate any context or leave one empty).
5. Because each species is an **independent asset**, parallel authoring causes **no merge conflicts**
   in code; only the (rarely-touched) enum files would, and those are gated behind review.
6. Keep `displayName`/`flavorText` in the **localization tables**, not inline, so translation scales.

### 6.4 Cross-doc contracts (don't duplicate; consume)

- **Tide/time/weather/season** values are produced by [`time-tides-weather.md`](time-tides-weather.md);
  this doc only reads them in `FishingContext`.
- **Pricing** (turning a catch's `baseValue`/weight/elasticity into coin) is owned by
  [`economy-and-business.md`](economy-and-business.md) §1; this doc supplies the species data it needs.
- **Gear availability** (which gear the player owns, and which boat can deploy trawl/dredge) is gated
  by [`boats-and-navigation.md`](boats-and-navigation.md); `requiredGear` here is the *species-side*
  half of that contract.
- **Trophies/mounts** display per [`progression-and-housing.md`](progression-and-housing.md).

---

## 7. Open questions

1. **Length–weight model granularity:** one `a/b` pair per `bodyType`, or per species? (Leaning
   per-`bodyType` with optional per-species override to keep authoring light.)
2. **Bycatch/multi-catch:** should passive gear (trawl/gillnet/pots) resolve *several* species per haul
   (a mixed net), not one? Feels right for P2 scale and processing chains — propose: passive gear rolls
   N entries scaled by hold/soak, active gear rolls 1. Needs an economy check (gluts get easier).
2. **Conservation / over-fishing pressure:** should a region's species `localStock` deplete with
   sustained pressure (player + NPC fleet) and need a fallow season to recover, beyond the *market*
   glut already modeled in economy §1? Strong P1/P3 flavor, but risks feeling punishing — gate behind
   late game or make it a soft, recoverable dip.
3. **Catch-and-release & minimum sizes:** do regulations/seasons forbid keeping undersized or
   out-of-season fish (Maritime realism, a soft P5 "rules of the sea")? Could tie into reputation.
4. **Almanac as progression:** is "discover all 100 + trophy records" a first-class collection
   meta-goal with rewards, and where does its UI live (likely [`progression-and-housing.md`](progression-and-housing.md))?
5. **Legendary cadence:** one-per-save, long real-cooldown, or re-summonable via a costly ritual/bait?
   (Leaning long in-game cooldown so they stay events but aren't permanently missable.)
6. **Live-vs-dead shellfish economics:** do alive lobster/crab carry a separate "keep alive in the
   well" state with its own spoilage, sold at a premium vs. dead? (Pairs with economy §3 perishability;
   nice P2 texture if not over-fiddly.)
7. **The Smother gear gate:** which exact instruments (`RequiresInstruments`) and how they map to the
   boat/equipment tiers in [`boats-and-navigation.md`](boats-and-navigation.md). Needs a joint pass.
