# Hidden Harbours — Product Backlog

> **Status:** The structured seed backlog for the whole project, organized by **milestone (M0–M4)** and by
> **epic** within each. Canon: [`../docs/vision-and-pillars.md`](../docs/vision-and-pillars.md). Phasing:
> [`../docs/roadmap.md`](../docs/roadmap.md). The **full M0→M1 detail** (precise acceptance criteria, dependency
> order) lives in [`milestone-1-vertical-slice.md`](milestone-1-vertical-slice.md); this file gives the credible,
> well-organized superset an agent team pulls from — heaviest on M0/M1, lighter sketches for M2–M4.
>
> **Pillars:** **P1** The Sea Has Moods · **P2** From Dory to Dynasty · **P3** A Living Working Coast ·
> **P4** Earn It, Then Automate It · **P5** Cozy, but with Teeth.

---

## How to use this backlog (multi-agent workflow)

- **Pull the top unblocked item for your role.** Items are roughly ordered within a milestone; an item is
  workable once its noted dependencies are Done. Prefer earlier milestones — **honor milestone order** (the
  roadmap is the gate; later-phase work that jumps the queue gets redirected there).
- **Owner roles** (assign work to exactly these): `lead-architect`, `gameplay-systems`, `world-content`,
  `economy-sim`, `ui-ux`, `art-pipeline`, `audio`, `qa-test`, `tools-editor`.
- **Follow the Definition of Done.** For M0/M1, the DoD is in [`milestone-1-vertical-slice.md`](milestone-1-vertical-slice.md)
  §6 and each item carries explicit acceptance criteria there. For M2–M4, treat the **key AC** line here as the
  acceptance seed and write full criteria when the item is pulled into active work.
- **Coordinate via `../agents/coordination.md`** (claim/lock items, branch/PR conventions, review). Content
  assets (fish, commodities, recipes, regions, NPC data) are independent ScriptableObjects — author in parallel
  without code-merge conflicts; only the (rarely-touched, review-gated) enum/code files serialize.
- **ID scheme:** `M0-xx`, `M1-xx`, `M2-xx`, `M3-xx`, `M4-xx`. **M0/M1 items map to the `VS-xx` items** in the
  vertical-slice file (the cross-reference is noted); pull the detailed AC from there.
- **Cross-cutting tracks** (art, audio, tools, QA, performance, save-stability, localization) run through every
  milestone — see roadmap §3. They appear as epics here per milestone where they have concrete deliverables.

---

# M0 — Greybox Prototype

> **Goal:** prove the fishing→sell loop is fun with placeholder art. **Detail + AC:** the `VS-xx` items in
> [`milestone-1-vertical-slice.md`](milestone-1-vertical-slice.md) (M0-tagged). Internal only; the loop is
> human-playtested before proceeding.
>
> **PC-first note (ADR 0005): desktop baseline; mobile = later port.** VS items are left intact; where an AC below
> hard-codes a phone (e.g. M0-17 "mid-range phone", M0-01 "on device"), read it as the **desktop baseline** and
> KB/mouse/gamepad. Input intents (M0-02 / VS-02) are **unchanged** — only the bindings retarget. Full notes in
> [`milestone-1-vertical-slice.md`](milestone-1-vertical-slice.md).

### Epic M0-A — Project foundations (`lead-architect`)
| ID | Title | Owner | One-liner | Key AC | Maps to |
|---|---|---|---|---|---|
| M0-01 | Unity project bootstrap & modules | lead-architect | 2D URP mobile project, asmdef boundaries, LFS, persistent-core + additive scenes | Builds & launches empty scene on device; modules compile, no cycles; LFS tracks art/audio | VS-01 |
| M0-02 | Input intents (touch-first) | lead-architect | `InputService` exposing Move/SetThrottle/SetHeading/Interact/Haul | Gameplay reads only intents; touch drives all five; remaps without code change | VS-02 |
| M0-03 | Save/load scaffold (versioned) | lead-architect | Tiny versioned save `{seed, gameTime, money, dayIndex}` | Quit/relaunch restores state exactly (gameTime as double); version checked + migration hook | VS-03 |

### Epic M0-B — The sea v0 (`gameplay-systems`)
| ID | Title | Owner | One-liner | Key AC | Maps to |
|---|---|---|---|---|---|
| M0-04 | EnvironmentService v0 — clock + tide | gameplay-systems | `gameTime` clock + deterministic semidiurnal tide; sleep/skip; `OnDayRollover` | Tide reproducible from `(seed,gameTime)`; ~2 highs/2 lows/day walking ~50 min later; rollover fires once/midnight | VS-04 |

### Epic M0-C — The boat v0 (`gameplay-systems`)
| ID | Title | Owner | One-liner | Key AC | Maps to |
|---|---|---|---|---|---|
| M0-05 | Dory controller v0 (kinematic) + follow-cam | gameplay-systems | Simple top-down move + pixel-perfect follow-cam at intimate zoom (no forces yet) | Board, putter the cove, return; smooth camera, no shimmer; one-thumb controllable | VS-07 |

### Epic M0-D — Fishing & fish v0 (`economy-sim` / `world-content` / `gameplay-systems` / `ui-ux`)
| ID | Title | Owner | One-liner | Key AC | Maps to |
|---|---|---|---|---|---|
| M0-06 | FishSpecies schema (ScriptableObject) | economy-sim | The species data asset + sub-structs/enums, slice-scoped | Authorable in Inspector, no code; `id` unique; strings localized | VS-10 |
| M0-07 | Six Coddle Cove species (data) | world-content | cod, haddock, pollock, mackerel, rock-crab, blue-mussel | Pass validation; catchable by slice gear; ≥4/6 carry a real tide/time window | VS-11 |
| M0-08 | Catch resolver v0 | economy-sim | Context → weighted table (+miss/flotsam) → one fish + size roll | Diurnal fish absent at night; spot stable in a time bucket; sane catch mix | VS-12 |
| M0-09 | Fishing interaction v0 (one-thumb) | gameplay-systems + ui-ux | Spot→cast→bite→tension/landing/strain; cozy fail | Full catch one-thumb; lands to hold w/ juicy confirm; failure cozy | VS-13 |

### Epic M0-E — Economy v0 (`economy-sim` / `ui-ux`)
| ID | Title | Owner | One-liner | Key AC | Maps to |
|---|---|---|---|---|---|
| M0-10 | Fish Buyer + supply/demand price | economy-sim | One market; `priceMult=clamp(1/(1+e·S/D),floor,1)`; sliced sells; daily recovery | Big lot depresses own price then recovers; glut species crash faster; floor respected | VS-15 |
| M0-11 | Sell screen (marginal-price slider) | ui-ux | Quantity slider showing marginal price + total; sell-all | Live marginal price matches coin received; self-glut visible pre-confirm | VS-18 |

### Epic M0-F — World & HUD v0 (`world-content` / `ui-ux`)
| ID | Title | Owner | One-liner | Key AC | Maps to |
|---|---|---|---|---|---|
| M0-12 | Greybox Coddle Cove scene | world-content | Wharf, cottage (sleep/save), mooring, water, fishing spots; metric scale | Full M0 loop runnable end-to-end here; forgiving (no grounding) | VS-20 |
| M0-13 | HUD v0 — clock, tide gauge, money, hold | ui-ux | Glanceable top-band: time, tide (shape+number), money, hold fullness | Readable <1s without menus; redundant coding; no per-frame alloc | VS-17 |

### Epic M0-G — Pipeline, audio, tools, acceptance (`art-pipeline` / `audio` / `tools-editor` / `qa-test`)
| ID | Title | Owner | One-liner | Key AC | Maps to |
|---|---|---|---|---|---|
| M0-14 | Placeholder-art convention + import-settings lock | art-pipeline | Greybox convention; PPU=32/Point/no-compression preset; "add an asset" checklist | Preset applied to all slice sprites; placeholders at metric scale; Pixel Perfect on | VS-23 |
| M0-15 | Ambient audio bed v0 | audio | Calm-sea + gulls; hull slap aboard | Bed audible on land/sea; engine layer aboard; volume sliders; no load errors | VS-27 |
| M0-16 | Tide/clock + content authoring tools | tools-editor | Editor time/tide scrubber + friendly FishSpecies drawers | Scrub time → tide/clock respond; author a species via inspectors; editor-only | VS-29 |
| M0-17 | M0 acceptance + human playtest of the loop | qa-test | Acceptance pass + owner/tester hands-on; core-loop smoke test | M0 loop passes end-to-end on a mid-range phone; smoke test passes; **written fun verdict + GO/POLISH/PIVOT** | VS-30 |

---

# M1 — Vertical Slice ("Coddle Cove")

> **Goal:** make the loop *genuinely good* — the soft-launch candidate. **Detail + AC:** the M1-tagged `VS-xx`
> items + the Definition of Done in [`milestone-1-vertical-slice.md`](milestone-1-vertical-slice.md).
>
> **PC-first note (ADR 0005):** the M1 soft-launch is a **Steam / itch.io closed playtest** (not TestFlight), the
> perf/acceptance bars are the **desktop baseline**, and sailing comfort is validated on **KB/mouse + gamepad**.
> The touch/one-thumb validation moves to the later **mobile-port** pass — same intents, retargeted bindings.

### Epic M1-A — The sea v1 & boat force model (`gameplay-systems` / `lead-architect`)
| ID | Title | Owner | One-liner | Key AC | Maps to |
|---|---|---|---|---|---|
| M1-01 | EnvironmentService v1 — wind, sea-state, FORCES sample | gameplay-systems | Full `EnvironmentSample` (wind/current/sea-state/depth/visibility) @4Hz | Bit-stable sample; wind varies smoothly + reads on HUD; gentle cove current | VS-05 |
| M1-02 | Boat as composable components (Hull/Engine/Hold/Gear) | lead-architect + gameplay-systems | `Boat` aggregate from SOs; Dory + Punt chassis data | Boats are data; swap via data only; no name special-casing; persists | VS-08 |
| M1-03 | **Dory force model v1 (the #1 risk — prototype first)** | gameplay-systems | Thrust + anisotropic drag + windage + speed-scaled rudder; inshore-forgiving | Drifts/sets with engine off; carries way; can't pivot at zero; one-thumb-comfortable | VS-09 |
| M1-04 | Tide-table tool + generation | gameplay-systems + ui-ux | HW/LW extrema finder + readable Cove tide-table panel (pauses time) | Correct upcoming highs/lows; agrees with HUD gauge; forecast exact | VS-06 |

### Epic M1-B — Economy & the first boat (`economy-sim` / `gameplay-systems`)
| ID | Title | Owner | One-liner | Key AC | Maps to |
|---|---|---|---|---|---|
| M1-05 | Greywick market basics + Shipwright buy flow | economy-sim + gameplay-systems | Greywick spot price + buy the **Punt** (~1,800 ₲); switch active boat | Sell at a different price than the cove; buying Punt deducts coin, persists; low-funds blocked gracefully | VS-16 |
| M1-06 | Sell screen diegetic skin (chalkboard) | ui-ux | M1 polish of the sell flow (marginal price + total) | Diegetic skin; live total matches coin received; sell-all works | VS-18 |

### Epic M1-C — HUD v1 (read the sea) (`ui-ux`)
| ID | Title | Owner | One-liner | Key AC | Maps to |
|---|---|---|---|---|---|
| M1-07 | HUD v1 — wind widget + compass + set-&-drift predictor | ui-ux | Wind direction/strength (redundant coding), compass, ghost-track drift arrow | Wind reads at a glance + animates before change; drift predictor shows course-over-ground; colourblind-safe | VS-19 |

### Epic M1-D — World, Ned & onboarding (`world-content` / `ui-ux`)
| ID | Title | Owner | One-liner | Key AC | Maps to |
|---|---|---|---|---|---|
| M1-08 | Uncle Ned + onboarding flow | world-content + ui-ux | late Uncle Ned (brief prologue + his logbook); inherit the dory; teach-the-loop; 1–2 neighbours; dialogue panel | First-timer guided through one full loop; flags saved; warm/bittersweet Maritime tone | VS-21 |
| M1-09 | Minimal Port Greywick scene | world-content | Short hop to a wharf with Fish Buyer + Shipwright + flavour buildings | Sail/transition cove→Greywick; sell + buy Punt; harbour workable at any tide; clean load | VS-22 |

### Epic M1-E — Art pass (`art-pipeline`)
| ID | Title | Owner | One-liner | Key AC | Maps to |
|---|---|---|---|---|---|
| M1-10 | Coddle Cove art pass — terrain, shoreline, water | art-pipeline | Tiles, animated water, **tide-aware moving shoreline**, day-night grade, cottage/wharf | Shoreline advances/retreats with `WaterLevel` (matches sim); water reads; recolours dawn→night; within budget | VS-24 |
| M1-11 | Character + NPC sprites (player, Ned, neighbours) | art-pipeline | ¾ 4-facing sheets: idle/walk/fish-haul/row-board/celebrate | Correct metric scale; clean anim, no jitter; atlased | VS-25 |
| M1-12 | Boat & fish sprites (Dory, Punt, 6 fish) | art-pipeline | Metric hulls + wake/spray + rotation; 6 fish sprites/icons | Punt visibly larger; speed-scaled wake; no shimmer; placeholder→final = ref swap | VS-26 |

### Epic M1-F — Fishing polish & audio v1 (`gameplay-systems` / `ui-ux` / `audio`)
| ID | Title | Owner | One-liner | Key AC | Maps to |
|---|---|---|---|---|---|
| M1-13 | Fishing polish & technique feel | gameplay-systems + ui-ux | Real-art juicy fight; tuned tension per size; haptics; first-cod triumph | First cod *feels* like a triumph; bigger fish weightier not twitchy; one-thumb holds | VS-14 |
| M1-14 | Reactive ambient + adaptive music v1 | audio | Beds by sea-state/wind/time; warm Cove theme; **rising-wind tell**; catch sting; "made it home" | Rising wind audible before danger (+HUD mirror); catch sting; home warmth; smooth cross-fades | VS-28 |

### Epic M1-G — Save, perf & acceptance (`lead-architect` / `qa-test`)
| ID | Title | Owner | One-liner | Key AC | Maps to |
|---|---|---|---|---|---|
| M1-15 | Save schema v1 + migration test | lead-architect | Extend save (boat+components, flags); migrate an M0 save | New payload round-trips; an old save loads without loss (migration test) | VS-03/VS-31 |
| M1-16 | Mobile performance pass (slice) | qa-test + lead-architect | Profile water/lighting/HUD/physics on a mid-range phone | Hits frame budget; no per-frame GC in hot path; one active boat dominates cost | (DoD) |
| M1-17 | M1 acceptance + external playtest (soft-launch readiness) | qa-test | DoD pass + TestFlight/closed group; validate sailing feel vs stick alternate | DoD green; testers complete + return; sailing judged fun/one-thumb; **soft-launch verdict + GO/POLISH/PIVOT** | VS-31 |

---

# M2 — The Working Coast

> **Goal:** a living inshore-and-mid coast with teeth and the first business steps. Early-Access shape. Sketch-
> level items; write full AC when pulled. Most depend on the M1 slice being Done.

### Epic M2-A — Danger & rescue (P5) (`gameplay-systems`)
| ID | Title | Owner | One-liner | Key AC (seed) |
|---|---|---|---|---|
| M2-01 | Grounding (draught vs water depth) | gameplay-systems | Touch-bottom → aground; soft (mud) vs hard (rock/holing); falling-tide gut-punch | Aground when draught>depth; float off on rising tide; depth-sounder telegraph; never a wipe |
| M2-02 | Broach / capsize (stability vs sea-state vs load) | gameplay-systems | Escalating heel→knockdown→capsize with recovery agency | Capsize rare + earned; clear warnings + recovery (slow, bow-to-sea, shed load); partial-load loss only |
| M2-03 | Taking on water / bilge | gameplay-systems | Holing/swamp/leak → ingress → pump counterplay | Bilge gauge; manual pump beats minor leaks; bad holing forces a run for shelter |
| M2-04 | Breakdown / fuel | gameplay-systems | Engine health/foul/out-of-fuel → lose propulsion; row/sail home (small boats) | Telegraphed (engine note/temp/gauge); maintenance prevents; small craft can row home |
| M2-05 | Rescue / tow set-piece | gameplay-systems + economy-sim | Self-recover / radio tow / harbour rescue / drift; gentle penalties | Always a way home; cost = time/money/partial-load; **no permadeath of boat or skipper** |

### Epic M2-B — Weather & wind v2 (P1) (`gameplay-systems`)
| ID | Title | Owner | One-liner | Key AC (seed) |
|---|---|---|---|---|
| M2-06 | Weather fronts (moving systems) | gameplay-systems | Deterministic scheduled fronts marching across regions | Same front hits regions at different times; build→peak→clear; forecastable with falling confidence |
| M2-07 | Fog & visibility | gameplay-systems | `fogDensity` caps visibility (sets up The Smother later) | Dense fog shrinks visible sea; flavor + minor fish/flotsam effect |
| M2-08 | Storms & sea-state escalation | gameplay-systems | Storm = peak of a deteriorating front; telegraphed | Barometer falls + radio warning + building swell before it hits ("warned, not ambushed") |
| M2-09 | Forecast tools (barometer / harbourmaster / radio) | gameplay-systems + world-content | Three escalating instruments give foresight | Barometer trend reliable; harbourmaster ~24–48h; radio live at sea |

### Epic M2-C — New regions (`world-content` / `tools-editor`)
| ID | Title | Owner | One-liner | Key AC (seed) |
|---|---|---|---|---|
| M2-10 | The Sunkers (reef field, grounding teeth) | world-content | Submerged rocks; tide-gauge landmarks; channels | Reefs surface at low water (safe forage) / hide at high (holing risk); teaches grounding cheaply |
| M2-11 | The Drownded Lands (walkable seabed) | world-content | Flats drain to walkable seabed at low water; returning-tide clock | On-foot seabed at low water; spring lows expose the rarest; flood is a lethal-but-survivable clock |
| M2-12 | Fundy Rips (tide-as-a-wall) | world-content | Narrows; current gated; slack-water transit window | Peak current a wall; only slack is safe; spring tides far worse |
| M2-13 | Port Greywick — full town | world-content | Auction house, shops, shipwright, harbourmaster, named cast | Real town with services + named NPCs; the breathing-coast hub |
| M2-14 | Seabed heightfields + tide-revealed geography tooling | tools-editor + world-content | Per-region tidal-height thresholds drive walkable/visual state | Same `WaterLevel` drives sim + visuals; authoring tool for thresholds |

### Epic M2-D — Boats, gear & upgrades (P2) (`gameplay-systems` / `economy-sim`)
| ID | Title | Owner | One-liner | Key AC (seed) |
|---|---|---|---|---|
| M2-15 | Cape Islander (T2) + Lobster Boat (T3) branch | gameplay-systems | The hub workboat + the shellfish specialist branch | Both purchasable; branch reads as a choice; metric scale on screen |
| M2-16 | Gear methods: longline + traps/pots | gameplay-systems | Soak-and-haul gear beyond the handline | Set/soak/haul loop; gear gates which species/volume (catch-context contract) |
| M2-17 | Boat upgrades (engine/hull/hold/instruments/safety) | gameplay-systems + economy-sim | Component swaps at the shipwright; each mitigates a danger | Upgrades are data swaps; each maps to a danger (sounder→grounding, pump→ingress, etc.) |

### Epic M2-E — Market depth, storage & first refining (`economy-sim`)
| ID | Title | Owner | One-liner | Key AC (seed) |
|---|---|---|---|---|
| M2-18 | Full supply/demand market sim | economy-sim | Hourly+daily tick; demandMood walk; event shocks | Prices breathe over days; deterministic from `(seed, tick)`; cheap on mobile |
| M2-19 | NPC-fleet landings model | economy-sim | Aggregate supply curve lands fish without the player (P3) | Gluts/scarcity happen ashore; keyed to season/weather; not a full agent sim |
| M2-20 | Auction house + multiple buyers + first contracts | economy-sim | Consignment auction; specialty buyers; standing contracts | Different channels absorb gluts differently; a contract trades upside for stability |
| M2-21 | Storage (ice/well, cold storage) + perishability | economy-sim | Time-the-market + spoilage buffer | Freshness as timestamp survives save/time-skip; storage slows/arrests spoilage |
| M2-22 | First processing facilities (salt house, smokehouse) | economy-sim | Convert volatile raw → stable value-add (run by hand first) | Salt cod / smoked herring recipes; processing raises value + lowers elasticity + cuts spoilage |

### Epic M2-F — NPC routines & relationships (P3) (`world-content`)
| ID | Title | Owner | One-liner | Key AC (seed) |
|---|---|---|---|---|
| M2-23 | NPC daily routines | world-content | Named cast keep home/work schedules + a market/rest day | Town runs on its own rhythm; NPCs move on schedules keyed to the weekday |
| M2-24 | Dialogue system v2 + relationships | world-content + ui-ux | Richer dialogue; per-faction reputation hooks | Conversations advance relationships; reputation reads into prices/contracts |

### Epic M2-G — Progression currencies & housing (P2) (`world-content` / `ui-ux`)
| ID | Title | Owner | One-liner | Key AC (seed) |
|---|---|---|---|---|
| M2-25 | Licenses / proficiencies / reputation systems | world-content + ui-ux | The four proficiencies + license wallet + per-faction rep + unlock graph | Data-driven unlock graph; band-up coincides with a new capability; no global level |
| M2-26 | Housing: cottage refit + decor/Comfort + storage | world-content + ui-ux | Customize the cottage; light opt-in Comfort buff; sea-chest storage | Decor on a tile grid (metric); Comfort buff mild + never mandatory; uncle's cottage never taken |

### Epic M2-H — Map, art, audio, perf (cross-cutting) (`ui-ux` / `art-pipeline` / `audio` / `qa-test`)
| ID | Title | Owner | One-liner | Key AC (seed) |
|---|---|---|---|---|
| M2-27 | Map / chart UI + fog-of-war + tide-table tiers | ui-ux | Nautical chart, discovered-by-presence reveal, buyable charts, table tiers | Locked regions fogged; charts pre-reveal authored hazards; table horizon grows with tier |
| M2-28 | Region art passes (Sunkers / Drownded / Rips / Greywick) | art-pipeline | Per-region grade + tide-driven visuals + bigger boats | Each region reads as its own mood; tide visuals match sim; metric scale holds |
| M2-29 | Weather/danger audio + town hum | audio | Storm/fog/region beds; danger cues; inhabited town | Storm/fog beds; aground/lost cues; Greywick sounds populated |
| M2-30 | Streaming-at-passages + perf pass + save migration | qa-test + lead-architect | Stream clusters at passages; profile; migrate M1 saves | One/two regions resident; hits budget; old saves migrate |

### Epic M2-I — Owner-ratified 2026 additions (St Peters opening · lobster gear · weather/winter · wet tide)
> New design captured 2026 (the "St Peters batch"). All **M2-phased**; full design lives in the design docs.
> Honor milestone order — **none of this is M0/M1** (CLAUDE.md rule 8). The St Peters opening **prepends** the
> arc and **relocates** the existing onboarding; it does not delete the Coddle Cove opening — Coddle Cove is
> **reused** as the home base sailed to once the dory is bought + repaired.
> **Core seams already landed for this batch (ADR 0009):** the **tidal-exposure query**
> (`IEnvironmentService.WaterLevelAt` + `Core.TidalExposure.IsExposed/WaterDepth`) the world terrain and the
> on-foot walkability sim share for the falling-tide sandbar, and **`Core.RegionDisplayNames`** so the crossing
> fade card reads "Coddle Cove"/"Port Greywick". Build M2-31/31b/31c against these; do not re-invent them.

| ID | Title | Owner | One-liner | Key AC (seed) |
|---|---|---|---|---|
| M2-31 | St Peters Island opening (prologue region) | world-content | Tide-gated home island — 3 houses + school + general store; clam-dig at low water; **walk the tide-gated sandbar to Greywick** | New starter region; **tide-gated SANDBAR to Greywick** (low-water walking path; channels narrow as the tide falls) reads + works via the Core tidal-exposure seam; **clam-dig → walk sandbar → Greywick** arc playable; **start + onboarding relocate here, reusing the dialogue/onboarding system** (`vision` §5.8, `world-and-regions` §6.0) |
| M2-32 | St Peters clam-dig (shovel + "two squirting holes") | gameplay-systems + world-content | Read the tell on bared flats, dig with a shovel; clam-licence gate (licence system = economy) | Passive "tend" dig (no tension fight); **licence-gated** (licence system owned by economy); the **first catch, before any rod**; reconcile shovel vs `ClamFork` tag (new tag = review-gated) (`fish-and-content` §3.5a) |
| M2-31b | Greywick early-progression: cod licence + rod + **buy & repair a damaged dory** | economy-sim (+ gameplay-systems) | At Greywick: buy a **cod fishing licence** + a **rod**; sell clams; **buy a damaged dory at the shipwright and pay to repair it**, then sail it home to Coddle Cove | Minimal **licence system** (clam + cod licences) lands in economy; shipwright sells a **damaged** dory + a paid **repair** to usable; arc completes by **sailing the repaired dory to the Cove** (`vision` §5.8, `progression-and-housing` §2.2/§3) |
| M2-31c | Rework VS-21 onboarding: inherited dory → **earned + repaired** dory | world-content | The built M1 onboarding (`VS-21`: Ned's logbook + "inherit the dory" at the Cove) is **partly invalidated** by the dropped inherited-dory framing — rework the inherited-dory beat into a buy-and-repair beat | Cottage + Ned's memory stay inherited; the **dory beat becomes buy-and-repair**; dialogue/onboarding **reused, not rebuilt**; flagged in canon §5.8 + `npcs-and-routines` §3.1 (do **not** silently drop) |
| M2-33 | Lobster gear loop (trap+buoy, leave-helm gaff-haul, winch) | gameplay-systems | Set baited trap+buoy; lay alongside, **leave the helm to gaff & haul** (boat drifts); powered-winch upgrade | Hand-haul is a stamina action; winch automates it (P4); approach/drift reads as seamanship (P1/P5) (`boats-and-navigation` §6.3, `fish-and-content` §3.5b) |
| M2-34 | Weather v2: waves-push + traveling gusts | gameplay-systems | Wave-push force shoves the hull; moving gust cells you see coming | Wave-push adds to drift + broach (new FORCES field, save-compat bump); gust cells propagate + telegraph; deterministic (`time-tides-weather` §4.8) |
| M2-35 | Winter freezing (some regions) + lightning/heavy rain | gameplay-systems | Ice closes specific inshore water in Hard Winter (ice-strengthened hull pairs); rain/lightning as storm atmosphere/visibility | Regional + seasonal ice, **never save-stranding**; lightning atmosphere-first (strike mechanic stays an OQ) (`time-tides-weather` §4.8) |
| M2-36 | Wet-surface tide effects (~3–4 m range reveal) | art-pipeline | Wet glistening walls/pilings/flats revealed as the tide falls; drying over time | Extends the tide-aware shoreline; ~3–4 m `RegionTideProfile` range; the St Peters **tide-gated sandbar** is the gameplay case (`art-and-audio-bible` §6.1, `time-tides-weather` §3.5) |

---

# M3 — Offshore & Enterprise

> **Goal:** open The Banks and turn laborer into owner (the first automation). Sketch-level; depends on M2.

### Epic M3-A — Offshore world & boats (P2/P5) (`world-content` / `gameplay-systems`)
| ID | Title | Owner | One-liner | Key AC (seed) |
|---|---|---|---|---|
| M3-01 | The Banks (open offshore grounds) | world-content | Big water, banks/shoals, weather buoy, edge-of-soundings; overnight steaming | Exposure replaces terrain danger; productive marks; set-a-course attaches here |
| M3-02 | Set-a-course fast travel | world-content + gameplay-systems | Plot a course → stylised passage w/ weather/fuel/risk check | First journey hand-sailed; later compressed; never trivializes tide/weather |
| M3-03 | Side Dragger (T4) + Stern Trawler (T5) | gameplay-systems | Offshore-seaworthy hulls; heavy inertia; gate The Banks | Inertia reads (wide turns, long stops); draught locks them out of shallow regions |
| M3-04 | Net / trawl gear + offshore instruments | gameplay-systems | Bulk net hauls; radar/GPS/sounder | Trawl = bulk volume; instruments restore awareness (sets up Smother later) |

### Epic M3-B — Staff & automation (P4 — the heart) (`economy-sim` / `gameplay-systems`)
| ID | Title | Owner | One-liner | Key AC (seed) |
|---|---|---|---|---|
| M3-05 | Hiring, wages, morale, skill growth | economy-sim | Recruit crew with skills/traits/wage; payroll on the daily settle | Wages clear on settle; morale from pay/schedule; staff improve on the job |
| M3-06 | AI work routines + policy system | gameplay-systems + economy-sim | Roles run data-driven routines bounded by world rules; player sets policies | A staffed **skipper runs a 2nd boat without you**, obeying tide/weather; policies are standing orders |
| M3-07 | Deckhand / processor / hauler / seller / manager roles | economy-sim | The role ladder from "help" to "stop micromanaging" | Each role automates a former hand job; manager reduces decision load (operate by exception) |
| M3-08 | Automation guardrail tuning | economy-sim + qa-test | Delegated output good-but-not-better than skilled hand-play | Staff take wages + a small quality/efficiency tax; hand-play stays meaningful |

### Epic M3-C — Production chains & contracts (`economy-sim`)
| ID | Title | Owner | One-liner | Key AC (seed) |
|---|---|---|---|---|
| M3-09 | Multi-facility production chains | economy-sim | Cannery / reduction plant / pack house; parallel recipes | Gluts become opportunity; chains raise value + stability; throughput/capacity upgrades |
| M3-10 | Reputation & contracts depth + mainland hooks | economy-sim | Standing/freight contracts; distant-market price hooks | Contracts = stable automatable income; rep modulates offers/prices |

### Epic M3-D — Management UX, offline, art/audio, perf (cross-cutting)
| ID | Title | Owner | One-liner | Key AC (seed) |
|---|---|---|---|---|
| M3-11 | Management UIs (dashboard-first, manage-by-exception) | ui-ux | Summary cards + alerts feed; policy-over-clicks | Empire never means more tapping; depth ≤2 taps; thumb-glance management |
| M3-12 | Offline accrual (delegated ops, bounded) | lead-architect + economy-sim | Staffed sites/freight accrue while away, capped | Delegated income accrues to a cap; the *fun* verbs never auto-play; "while you were away" summary |
| M3-13 | Offshore art + crewed-deck sprites | art-pipeline | Big boats, banks, weather spectacle, busy decks | Crew visible on deck (visible progression); big hulls sectioned/atlased; budget held |
| M3-14 | Offshore/industry audio | audio | Engine voices scale with tier; busy decks; lonely offshore | Audible progression (outboard→diesel thrum); offshore loneliness reads |
| M3-15 | Fleet/abstraction perf + save scaling | lead-architect + qa-test | Abstract off-screen fleet; batched shift sim; bigger saves | A multi-boat empire stays cheap; save persists staff/facilities/freight |

### Epic M3-E — Owner-ratified 2026 additions (aquaculture · advanced rendering)
> New design captured 2026 (the "St Peters batch"). All **M3-phased** (advanced/late); design in the design docs.

| ID | Title | Owner | One-liner | Key AC (seed) |
|---|---|---|---|---|
| M3-16 | Mussel/oyster aquaculture leasing | economy-sim + world-content | Lease water, set buoys-in-series + grow-ropes, season-grow the crop, harvest at maturity | Lease cost + grow-timer + seasonal yield; **harvested from a lease** (not a wild catch roll); P4 owned-production (`fish-and-content` §3.5c + economy/progression docs) |
| M3-17 | Advanced rendering pass (3D-water→2D bake · dynamic + cloud shadows · parallax-underwater) | art-pipeline + lead-architect | Bake 3D water to the 2D surface; sun-driven dynamic shadows; drifting cloud shadows; shallow-water underwater preview | Holds one-perspective + PPU=32 + desktop budget; baked sculpt-light unchanged; shallow preview telegraphs hazards/forage (`art-and-audio-bible` §6.1) |

---

# M4 — Dynasty

> **Goal:** fleet command + freight empire + full content fill + multi-platform (1.0). Sketch-level; depends on M3.

### Epic M4-A — Commerce tier & fleet (P2/P4 capstone) (`gameplay-systems` / `economy-sim` / `world-content`)
| ID | Title | Owner | One-liner | Key AC (seed) |
|---|---|---|---|---|
| M4-01 | Coastal Packet (T6) + Coastal Tanker (T7) | gameplay-systems | The cargo/commerce hulls; tug-assisted docking; tide-window approaches | Ponderous, plan-every-turn; the scale fantasy fully paid off (a tanker dwarfs the dory) |
| M4-02 | The Shipping Lanes + mainland ports + freight | world-content + economy-sim | Lane network, off-map port nodes, freight runs/contracts | Bulk runs clear gluts at a premium; weather/risk along routes; one run can dwarf a week of local selling |
| M4-03 | Fleet command UX | ui-ux + gameplay-systems | Dispatch + direct a fleet (hand-steer optional) | Operate by exception; drop back into a dory for a quiet morning anytime |

### Epic M4-B — Apex frontiers (P1/P5 capstone) (`world-content` / `economy-sim`)
| ID | Title | Owner | One-liner | Key AC (seed) |
|---|---|---|---|---|
| M4-04 | Ironbound (storm coast) | world-content | Apex weather + rock-and-reef; rarest/legendary fish | The region that can sink you if you misjudge weather; the keeper NPC; runs parallel to commerce endgame |
| M4-05 | The Smother (fogbank) | world-content | Permanent fog; navigate by instrument/sound; cryptids | Unplayable on compass alone, navigable with radar+GPS; uncanny, optional, unforgettable |
| M4-06 | Legendary / cryptid catches | economy-sim + world-content | The 8 gated, named, memorable catches | Weight 0 until unlocked; long cooldown; one-off market footprint |

### Epic M4-C — Content fill & end-game economy (`world-content` / `economy-sim`)
| ID | Title | Owner | One-liner | Key AC (seed) |
|---|---|---|---|---|
| M4-07 | Full 100-species content fill | world-content + economy-sim | Author the remaining species via the parallel-authoring workflow | Hits the category×rarity allocation; balance dashboard stays healthy; no merge conflicts |
| M4-08 | End-game economy balance + money sinks + prestige property | economy-sim + world-content | Keep late coin meaningful; the Captain's House/estate; upkeep/insurance | Anti-inflation pass; prestige sinks; late-game money still matters |

### Epic M4-D — Multi-platform & 1.0 (cross-cutting)
| ID | Title | Owner | One-liner | Key AC (seed) |
|---|---|---|---|---|
| M4-09 | Desktop/console port (intents → KBM + gamepad) | lead-architect + ui-ux | Map existing intents to mouse/keyboard + gamepad; responsive UI reflow | No separate UI codebase; same info set; cards reflow multi-column; assists carry over |
| M4-10 | Full art/audio fill (outer world + biggest hulls) | art-pipeline + audio | Ironbound/Smother/Lanes grades + T6/T7 sprites + full music/ambience | Outer-world moods land; biggest hulls atlased within texture budget |
| M4-11 | 1.0 perf / save-migration / certification | lead-architect + qa-test | Full-content profiling; old-save migration; store certification | Hits budget at full content; saves migrate; passes platform cert |

---

## Post-1.0 / flex (not scheduled)
- Endless outer-grounds procedural zone (off The Banks/Ironbound).
- DLC / seasonal-event fish (the 8-species flex reserve in the fish allocation).
- Deeper piracy/insurance economy beyond the light version.
- Any design "open question" feature that earns its way in after launch.
