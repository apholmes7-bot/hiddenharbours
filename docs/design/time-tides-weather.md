# Hidden Harbours — Time, Tides & Weather

> **Status:** Design module (production-grade, implementable).
> **Canon parent:** [`../vision-and-pillars.md`](../vision-and-pillars.md) — when in doubt, that file wins.
> **Sibling docs:** [`boats-and-navigation.md`](boats-and-navigation.md) (consumes the FORCES interface; owns draught/grounding physics), [`fish-and-content.md`](fish-and-content.md) (consumes season/tide/weather modifiers for spawn tables), [`world-and-regions.md`](world-and-regions.md) (region water-level profiles), [`economy-and-business.md`](economy-and-business.md) (weather affects auction supply).
>
> **Pillars served:** **P1 The Sea Has Moods** is the spine of this entire doc — tide, wind, weather, season and the clock are *real forces the player reads and respects*. Also **P2 From Dory to Dynasty** (forecasting/access reward scale and capability) and **P5 Cozy, but with Teeth** (storms, fog, falling tides are the teeth).

---

## 0. Design intent (read first)

The world must feel like a place with **moods that you learn to read**, never a place that punishes you with dice. Three principles flow from that:

1. **Everything is deterministic from `(worldSeed, gameTime)`.** Tide, sun, moon, wind, fog and storms are all computed from the clock and a fixed seed — never from live RNG that can't be reproduced. This is what makes the sea *readable* (you can forecast it) and *save-friendly* (no extra state to serialize; a save is just a timestamp + seed). See [§9 Implementation](#9-implementation-notes).
2. **The signs come before the event.** A storm is preceded by a falling barometer, a greasy swell, and a radio warning. Fog rolls in with a dewpoint cue. Spring tides are flagged by the moon phase. **Reading the signs is the skill** (P1); the forecast tools (tide table, barometer, harbourmaster, radio) are how the player *earns* foresight.
3. **The same cove is a different place** at high vs low tide and calm vs gale. This is the anti-pillar made concrete: no static water.

> **Tuning philosophy:** every number below lives in a `ScriptableObject` config (`EnvironmentConfig`, `SeasonConfig`, `RegionTideProfile`, `WeatherConfig`). The values here are *defaults that feel right*; expect to tune them in playtest. Nothing in this doc should be hard-coded in C#.

---

## 1. The 24-hour clock

### 1.1 Day length & time scale

- One in-game **day = 24 in-game hours**, mapped to a tunable **real-time day length** `D_real`.
- **Default `D_real` = 20 real minutes** (canon range 18–24 min). At 20 min/day, **1 in-game hour ≈ 50 real seconds** and **1 in-game minute ≈ 0.83 real seconds**.
- Define the master scale factor:

  ```
  timeScale = (24 * 3600) / (D_real_seconds)      // in-game seconds per real second
  // at D_real = 20 min = 1200 s  ->  timeScale = 72
  ```

- **`gameTime`** is the single source of truth: a `double` count of **in-game seconds since world epoch** (day 0, 00:00, Year 1, first day of Early Spring). All derived systems (tide, sun, moon, weather phase) are pure functions of `gameTime`. Store as `double`, not `float` — across a multi-year save, `float` (≈7 sig digits) loses sub-minute precision; `double` does not.

```
gameTime += Time.deltaTime * timeScale * timeFlowMultiplier;
```

- **`timeFlowMultiplier`** lets us pause (0), run normally (1), or fast-forward (e.g. 60×) for sleeping/skipping/cutscenes. It is **not** the same as `timeScale` (which is fixed by `D_real`).

### 1.2 How time advances

- Time advances continuously while the player is in the world (on land or on water). There is **no separate "paused while in menus by default"** rule except where a menu explicitly sets `timeFlowMultiplier = 0` (e.g. the pause menu, the tide-table reader — see §3.6).
- **Fixed-step driver:** `EnvironmentService` advances on a fixed cadence (see §8 FORCES — environment ticks at **4 Hz** for physics consumers) but `gameTime` itself accumulates every frame for smooth visuals (sun angle, water shimmer). The two are reconciled by always deriving from `gameTime`, never by integrating drift.

### 1.3 Sleeping & skipping time

The player ends a day (or skips ahead) in three sanctioned ways. All three are just **a fast-forward of `gameTime` to a target**, with a fade:

| Action | Where | Effect |
|---|---|---|
| **Sleep** (bed at cottage / any owned home / bunk on a crewed boat) | Home / boat with a bunk | Fast-forward to **06:00 next day** (configurable wake time). Restores stamina (see `progression-and-housing.md`). Triggers daily economy tick (market resupply — see `economy-and-business.md`). |
| **Nap / wait** (e.g. waiting out a tide on a beach or wharf) | A "wait" prompt at safe spots | Choose a target hour (1–12 h ahead). Partial stamina, no daily economy tick. Useful for **timing a tide window** — wait for low water on The Drownded Lands. |
| **Pass out** (stamina hits 0 while awake) | Anywhere safe-ish | Forced fade to a safe location (nearest wharf/home), small penalty (see P5 framing — never brutal). |

> **P1 hook:** "Wait for the tide" is a *first-class verb*. The wait UI shows the **next low/high water time** so the player learns to plan around tides rather than be ambushed by them.

**Implementation:** sleeping never simulates intervening ticks step-by-step. Because the whole environment is deterministic from `gameTime`, we just set `gameTime = targetTime` and re-sample. (Economy/NPC systems that *do* need discrete daily ticks subscribe to an `OnDayRollover(dayIndex)` event and run their resupply once per crossed midnight — see §9.4.)

### 1.4 Reading the clock

- HUD shows a **24h clock** (canon) with an analog sun/moon dial and digital `HH:MM`. Skipper vernacular for time-of-day flavor is fine in ambient text ("first light", "the turn of the tide") but the HUD is literal.
- **Daylight** is driven by §2.3 (sunrise/sunset shift by season). Lighting/colour grading is owned by the art bible (`art-and-audio-bible.md`); this doc supplies the **sun altitude** value it reads.

---

## 2. The four seasons

North Atlantic / Sablewick Banks flavor: cold, fog-prone, big weather. Seasons change **daylight length, water temperature, fish availability, and weather odds** — never just a palette swap (P1).

### 2.1 Calendar structure

- **Year = 4 seasons. Each season = 28 in-game days** (matches the ~28-day moon cycle in §3.3 — one lunar cycle per season, clean for spring/neap math). **Year = 112 days.**
- Season names use Maritime feel:

| Index | Season | Real-world analog | Flavor |
|---|---|---|---|
| 0 | **Early Spring** ("the Breakup") | late Mar–Apr | Ice going out, raw, lengthening days, capelin/herring stirring. |
| 1 | **High Summer** ("the Open Water") | Jun–Aug | Long days, warm-ish water, settled spells but afternoon fog, peak inshore fishing. |
| 2 | **The Turn** ("the Fall Run") | Sep–Oct | Shortening days, the big fall pelagic run, first gales, gorgeous low light. |
| 3 | **Hard Winter** ("the Lock") | Nov–Feb | Short brutal days, storm season, some grounds freeze/close, end-game Ironbound at its most dangerous and rewarding. |

> These are *display* names; code uses the `Season` enum (`EarlySpring, HighSummer, TheTurn, HardWinter`).

### 2.2 Day-of-year & season helpers

```
dayIndex      = floor(gameTime / SECONDS_PER_DAY)            // 0-based, since epoch
dayOfYear     = dayIndex mod 112                             // 0..111
seasonIndex   = floor(dayOfYear / 28)                        // 0..3
dayOfSeason   = dayOfYear mod 28                             // 0..27
yearIndex     = floor(dayIndex / 112)                        // Year 1 = 0
hourOfDay     = (gameTime / 3600) mod 24                     // 0..24 float
```

### 2.3 Daylight changes (sun model)

We don't need real astronomy — we need a **believable, smoothly-varying daylight curve** that lengthens toward High Summer and shortens toward Hard Winter, with a North-Atlantic-ish swing.

- Define per-season **sunrise/sunset anchors** in `SeasonConfig` (interpolated *across* the season so transitions are gradual, not stepped at season boundaries):

| Season midpoint | Sunrise | Sunset | Daylight |
|---|---|---|---|
| Early Spring | 06:10 | 19:00 | ~12h50 |
| High Summer | 04:50 | 21:10 | ~16h20 |
| The Turn | 06:40 | 18:20 | ~11h40 |
| Hard Winter | 08:00 | 16:30 | ~8h30 |

- Compute a continuous **`yearPhase` ∈ [0,1)** = `dayOfYear / 112`, and interpolate sunrise/sunset with a smooth (Catmull-Rom or cosine) curve through the four anchors so the longest day sits mid-High-Summer and the shortest mid-Hard-Winter.
- **Sun altitude** for lighting:

  ```
  // 0 at horizon, 1 at local noon, negative at night (for dusk/dawn blending)
  dayLen   = sunset - sunrise                     // hours
  noon     = (sunrise + sunset) / 2
  // smooth bump peaking at noon, zero at sunrise/sunset:
  sunAltitude01 = clamp( sin( PI * (hourOfDay - sunrise) / dayLen ), 0, 1 )
  isDaytime     = (hourOfDay > sunrise) && (hourOfDay < sunset)
  ```

- Art reads `sunAltitude01` + `isDaytime` + current weather (§4) to drive ambient colour/intensity. **Twilight bands** (civil dusk/dawn) are a fixed ±0.5h around sunrise/sunset for the colour ramp.

### 2.4 Seasonal effects (gameplay)

| Vector | How season changes it |
|---|---|
| **Water temperature** | `waterTemp` curve per region, coldest mid-Hard-Winter, warmest mid-High-Summer. Feeds fish spawn tables (`fish-and-content.md`) and a few "cold shock" flavor events. |
| **Fish availability** | Each species' `SpeciesData` declares seasonal weighting (e.g. the **fall run** in The Turn, capelin in Early Spring). This doc only *provides* `seasonIndex`, `dayOfSeason`, `waterTemp`; the tables live in `fish-and-content.md`. |
| **Weather odds** | `WeatherConfig` has a per-season profile: Hard Winter biases storm frequency & severity up and fog down (cold dry gales); High Summer biases settled weather but **afternoon advection fog** up; The Turn brings the first big gales. See §4.6. |
| **Daylight / work window** | Short Hard-Winter days compress the safe working window — you can do less per day, raising the stakes of each trip (P5). |
| **Region access** | Some outer grounds (Ironbound, parts of The Banks) are *more often weathered-out* in Hard Winter — not hard-locked, but the forecast will tell you to stay in. Ice is *flavor/atmosphere only* at launch (no ice-physics); revisit in Open Questions. |

---

## 3. TIDES — the signature system (P1)

> This is the system that most makes Hidden Harbours *itself*. The sea breathes twice a day; the same reef is a deathtrap or a clam garden depending on the hour and the moon. Get this right and P1 is delivered.

### 3.1 Model overview

- **Semidiurnal** (canon): **two highs and two lows per tidal day**. A tidal day is **~24h 50m** (the moon's daily lag), so the tide **walks later by ~50 min each calendar day** — this drift is itself a thing the player learns ("the tide's later today").
- **Spring/neap cycle** on the **~28-day moon** (§3.3): tidal *range* swells to **spring tides** (very high highs, very low lows) near new & full moon, and shrinks to **neap tides** (modest range) near the quarters. Sablewick has **enormous tides** (Bay-of-Fundy flavor), so spring range is dramatic.
- Tide is expressed as a single scalar **`tideHeight`** in **metres above chart datum** (datum = lowest astronomical tide ≈ 0). Every tide-sensitive region maps this scalar to a local water level (§3.5).

### 3.2 The tidal-day constant & two constituents

A clean, cheap, believable tide uses **two cosine constituents** — a principal lunar semidiurnal term plus the modulation that produces springs/neaps. We model it as a **base semidiurnal cosine whose amplitude is itself modulated by the spring/neap envelope**, which is both accurate enough and trivial to compute.

```
// --- constants (EnvironmentConfig) ---
TIDAL_DAY_HOURS   = 24.8412        // lunar day
SEMIDIURNAL_PERIOD = TIDAL_DAY_HOURS / 2.0    // ≈ 12.4206 h between successive highs
SYNODIC_MONTH_DAYS = 28.0          // game moon (canon ~28); springs twice per month
SPRINGNEAP_PERIOD  = SYNODIC_MONTH_DAYS / 2.0 // ≈ 14 days high->high range (springs at new AND full)

MEAN_SEA_LEVEL_m   = regionProfile.meanLevel      // e.g. 4.0 m above datum
MEAN_AMPLITUDE_m   = regionProfile.meanAmplitude  // half the mean range, e.g. 3.5 m
SPRING_NEAP_RATIO  = regionProfile.springNeapRatio// e.g. 0.45  -> neaps are 55% as big as springs
```

### 3.3 Moon phase (drives springs/neaps AND night light)

```
// moonAge in days since a reference new moon at epoch
moonAge      = (gameTime/SECONDS_PER_DAY - MOON_EPOCH_DAY) mod SYNODIC_MONTH_DAYS   // 0..28
moonPhase01  = moonAge / SYNODIC_MONTH_DAYS                                          // 0=new,0.5=full
illumination = (1 - cos(2*PI * moonPhase01)) / 2                                     // 0 new .. 1 full
```

- **Spring/neap envelope.** Tidal range is greatest at new **and** full moon (twice per synodic month). So the envelope uses **double the lunar frequency**:

  ```
  // springNeap01: 1.0 = peak spring (new/full), 0.0 = peak neap (quarters)
  springNeap01 = (1 + cos(2*PI * moonAge / SPRINGNEAP_PERIOD)) / 2
              // equivalently: (1 + cos(4*PI * moonPhase01)) / 2
  ```

- **Amplitude after spring/neap modulation:**

  ```
  amplitude = MEAN_AMPLITUDE_m * lerp(SPRING_NEAP_RATIO, 1.0, springNeap01)
  //  springNeap01 = 1 -> full MEAN_AMPLITUDE_m  (spring)
  //  springNeap01 = 0 -> SPRING_NEAP_RATIO * MEAN_AMPLITUDE_m (neap)
  ```

### 3.4 Tide height formula (the concrete equation)

```
// hoursSinceEpoch (in-game hours)
t = gameTime / 3600.0

// principal semidiurnal phase (two highs/two lows per tidal day):
phase = 2*PI * t / SEMIDIURNAL_PERIOD            // SEMIDIURNAL_PERIOD ≈ 12.4206 h

tideHeight(t) = MEAN_SEA_LEVEL_m
              + amplitude(t) * cos(phase - PHASE_OFFSET)

// PHASE_OFFSET (regionProfile.phaseOffset) sets when high water falls at epoch,
// and lets different regions have slightly offset tides (a rip leads a cove, etc.)
```

That's the whole tide in one line plus the amplitude envelope from §3.3. Properties this gives us for free:

- Two highs / two lows per ~24h50m. ✔
- Highs walk ~50 min later each day. ✔ (period is the lunar 12.42 h, not 12.00 h)
- Range swells to springs near new/full moon, shrinks to neaps near quarters. ✔
- Fully deterministic & save-friendly (pure function of `gameTime`). ✔

**Optional realism polish (cheap, do later):** add a small **diurnal inequality** (one of the day's two highs higher than the other) by superposing a weaker term at the full tidal-day period:

```
tideHeight += DIURNAL_AMP_m * cos(2*PI * t / TIDAL_DAY_HOURS - DIURNAL_OFFSET)
//  DIURNAL_AMP_m small (e.g. 0.4 m). Makes "the morning low is the big one today" emerge.
```

We also expose the **rate of change** (the *set* of the tide — drives tidal current strength in §3.7 and §8):

```
dTide/dt ≈ -amplitude * (2*PI/SEMIDIURNAL_PERIOD) * sin(phase - PHASE_OFFSET)
// max |rate| at mid-tide (half-tide), ~zero at slack (HW/LW). Current follows this.
```

> **"Slack water"** = when `|dTide/dt|` ≈ 0 (near HW and LW). **"Mid-tide"** = max rate = strongest current. This is the timing knowledge Fundy Rips demands (§3.9).

### 3.5 Mapping tide height → water level per region

Each tide-sensitive region carries a **`RegionTideProfile`** (ScriptableObject) and a **bathymetry/seabed-elevation field** (per-tile or per-zone "ground height above datum", authored in the region tilemap or a heightfield). The local water surface is global tide; **a tile is underwater iff `tideHeight > seabedElevation(tile)`**, and local **water depth = `tideHeight − seabedElevation`** (≤0 means dry land/exposed).

```
waterDepth(tile, t) = tideHeight(t) - seabedElevation(tile)     // metres; <=0 means dry
isSubmerged(tile,t) = waterDepth(tile,t) > 0
```

This single rule produces *every* tidal gameplay consequence:

| Region | Profile feel | What tide does (P1/P5) |
|---|---|---|
| **Coddle Cove** (start) | Small amplitude, gentle slope, deep enough channel that you **never ground the dory in the main channel** — a forgiving tutorial. | Teaches the *concept* of tide safely: the wharf float rises/falls, mudflat edges show at low water, but you can't get badly stuck. |
| **The Sunkers** | Reef field. Many tiles have `seabedElevation` just below mean level → submerged rocks (**sunkers**) that **break the surface near low water**. | At **low tide** the sunkers are exposed hazards you can see and avoid (and tide-pool/shellfish forage); at **high tide** they're *hidden just under the surface* → grounding/holing risk (P5). Reading the tide turns a deathtrap into a larder. |
| **The Drownded Lands** | Vast flats with `seabedElevation` near mid-range. | At **low water** huge areas go dry → **walkable seabed** (clams, wrecks, secrets — canon). At **high water** they flood (*drownded*). Spring lows expose the most; the *biggest* secrets only surface at **spring low tide**. Get caught out there on a fast flood and you're in trouble (P5) — but never lethal; you wade/swim back or lose forage, not your life. |
| **Fundy Rips** | Deep narrows; tide drives **current**, not exposure. | Transit only safe near **slack water**; at mid-tide the **rips** run ferociously (§3.7/§3.9). |
| **The Banks / Ironbound / Shipping Lanes** | Deep, open. | Tide height matters little for grounding; tidal *current* still applies a gentle set offshore. Weather (§4) dominates here, not tide. |

> **Hard rule for tide-sensitive regions:** the region's authored seabed heightfield + the global tide is the **single source of truth** for "is this passable / walkable / a hazard right now." Boats query `waterDepth` for grounding (next doc); the player-on-foot query the same field for walkability on the flats.

> **Tide range & the wet-reveal tell (owner-ratified vision; art lands M2/M3).** Sablewick's working
> harbours run a **big tide** — author **marina/wharf tide ranges of ~3–4 m** so the water visibly
> *walks up and down the walls*. As the tide **falls**, wet pilings, harbour walls, slip ramps,
> weed-lines and bared mud/rock are **revealed glistening** — a primary, always-on **tide tell** the
> player reads at a glance (P1), and the gentle everywhere-version of the Drownded Lands
> transformation. The **value** (a ~3–4 m range per `RegionTideProfile`) can be set whenever; the
> **wet-surface rendering** is owned by [`art-and-audio-bible.md`](art-and-audio-bible.md) and lands
> with the art passes (**M2/M3**). Ties to **OQ1** (tide → visual-cue mapping). St Peters' **causeway**
> is the gameplay-critical case: the same 3–4 m fall both **bares the flats** and **opens the
> tide-gate** (`world-and-regions.md` §6.0).

### 3.6 The tide table (the player's core tool)

Canon: *"a readable tide table is a core tool"* and is a **gate** for The Drownded Lands. It's the in-fiction forecasting instrument.

- **What it is in fiction:** a printed almanac/booklet (and later a wall chart at home, and a glanceable HUD widget once mastered). The skipper buys/inherits it; upgraded versions extend its range/accuracy.
- **What it shows:**
  - For **today + next N days** (N grows with tool tier — see below), the **times and heights of each HW and LW**, per selectable region.
  - A **tide curve graph** (height vs time) for the selected day with "now" marker.
  - **Moon phase** icon (so the player links springs/neaps to the moon — P1 literacy).
  - **Annotations:** "SPRING LOW 03:40 — flats fully exposed", "rips slack ~09:10 & ~15:30".
- **How forecasting works (mechanically):** because tide is a deterministic function of `gameTime`, the table just **samples the formula forward** and finds extrema. No simulation, no RNG — *the forecast is exact*, which is the point: tide is the one force you can fully trust if you learn to read it (contrast weather in §4, which is probabilistic).

  ```
  // Build a day's tide events for the table:
  for each region R the player can read:
    sample tideHeight over [dayStart, dayStart + horizonDays*24h] at fine step (e.g. 2 in-game min)
    detect local maxima (HW) and minima (LW); record (time, height, type)
    flag "spring"/"neap" by springNeap01 at that time (> 0.8 spring, < 0.2 neap)
  ```

- **Tool tiers (progression — P2):**

| Tier | Item | Horizon | Regions | Notes |
|---|---|---|---|---|
| 0 | Uncle's dog-eared **Tide Booklet** | Today + 1 day | Coddle Cove, The Sunkers | Inherited. Times rounded to 10 min. |
| 1 | **Greywick Almanac** (buy) | 7 days | + The Drownded Lands, Fundy Rips | Unlocks Drownded Lands access (canon gate). Exact times. |
| 2 | **Harbourmaster's Charts** | 28 days (full moon cycle) | All known regions | Shows spring/neap calendar; best for planning expeditions. |
| 3 | **HUD Tide Widget** (skill/upgrade) | Live glance | Current region | Always-on next-HW/LW + countdown on the HUD. The "mastery" reward. |

- **Reading the table can be done at the cottage wall chart with `timeFlowMultiplier = 0`** (time pauses while you plan) or via the always-on HUD widget once unlocked. On a small boat with no chart table, you rely on memory/HUD — a subtle pressure that rewards planning before you leave the wharf.

> **P1 payoff:** the *gate* on The Drownded Lands isn't an arbitrary key — it's literally **owning and reading a tide table**, i.e. the player demonstrating tide literacy. That is pillar-perfect.

### 3.7 Tidal current (set & drift) — feeds the FORCES interface

Tide doesn't just raise water; it **moves** it. Tidal current is what makes Fundy Rips and the flats dangerous, and is a primary input to boat drift (§8, consumed by `boats-and-navigation.md`).

- Each region carries a **`currentField`**: an authored direction map (flow axis per zone, e.g. "flood runs NE up the narrows, ebb runs SW out") + a **strength scalar** that scales with the **rate of tide change**:

  ```
  // global driver: normalized tide rate, -1 (max ebb) .. +1 (max flood)
  tideRateNorm = clamp( (dTide/dt) / maxTideRate , -1, +1 )

  // per-zone current vector consumed by physics:
  currentVector(zone, t) = currentField.flowDir(zone)         // unit vector, flood-positive
                         * currentField.maxSpeed(zone)        // m/s at peak (e.g. Fundy Rips 3.0; Cove 0.2)
                         * tideRateNorm                        // sign flips flood<->ebb, magnitude follows the tide
  ```

- **Slack water** (`tideRateNorm≈0`, near HW/LW) → near-zero current → safe transit window. **Mid-tide** → peak current → the rips run hard. This *emergent timing puzzle* is the heart of Fundy Rips and a P1 set-piece.
- Strength tuning (peak `maxSpeed`): Coddle Cove ~0.2 m/s (gentle), The Drownded Lands flood ~0.8 m/s (you can get swept), **Fundy Rips ~3.0 m/s** (ferocious — a small boat at full throttle barely makes headway against a mid-tide rip), open Banks ~0.3 m/s drift.

### 3.8 Access windows (the planning game)

An **access window** is "the span of time during which region/feature X is doable." The tide table is how you find it.

- **Drownded Lands forage window:** the flats are walkable while `tideHeight < walkableThreshold(zone)`. Window opens as the tide falls past the threshold and closes as it rises back — typically a couple of hours around low water, **widest at spring low**. The HUD warns when the window is closing ("flood making — head in").
- **Sunkers safe-passage window:** safest near low water (rocks visible). At high water you *can* cross but it's a draught/holing gamble (P5).
- **Fundy Rips transit window:** ride **slack water** (around HW or LW). Mis-time it and you fight a 3 m/s rip.
- **General:** `EnvironmentService` exposes `GetNextWindow(feature, fromTime)` so UI/quests can say "next safe crossing of the Rips: 14:55."

### 3.9 Worked example (sanity check for implementers)

> Region: Fundy Rips. `meanLevel=5.0`, `meanAmplitude=4.0`, `springNeapRatio=0.4`, `phaseOffset=0`. Suppose `gameTime` puts us 3 days after new moon (near spring), `t = 50.0` in-game hours.
> - `moonAge≈3` → `springNeap01 = (1+cos(2π·3/14))/2 ≈ 0.90` → near-spring.
> - `amplitude = 4.0 · lerp(0.4,1.0,0.90) = 4.0 · 0.94 = 3.76 m`.
> - `phase = 2π·50/12.4206 ≈ 25.29 rad`; `cos(phase) ≈ cos(25.29 − 8π=25.13) = cos(0.16) ≈ 0.987`.
> - `tideHeight ≈ 5.0 + 3.76·0.987 ≈ 8.71 m` → **near a spring high**. Current ≈ slack (we're near HW). A boat with 2.0 m draught over a 6.5 m-datum sill has `waterDepth = 8.71 − 6.5 = 2.21 m` → just clears. Two hours later at mid-ebb, current peaks and water drops ~2–3 m → very different crossing. ✔ The model produces exactly the "time the slack" tension we want.

---

## 4. Weather & wind

> Weather is the *probabilistic* counterpart to the *deterministic* tide. You can forecast it (barometer, harbourmaster, radio) but never be 100% certain — that uncertainty is the source of at-sea tension (P5) while still rewarding the player who reads the signs (P1).

### 4.1 What weather is made of (the state)

A region's weather at time `t` is a small struct, all derived from `(worldSeed, t, region, season)`:

| Field | Type | Meaning |
|---|---|---|
| `windVector` | Vector2 (m/s) | Direction + strength of wind. Magnitude `windSpeed = |windVector|`. |
| `seaState` | enum + float | Discrete tier (§4.3) + continuous 0..1 "roughness" used by physics/visuals. |
| `fogDensity` | float 0..1 | 0 clear → 1 the-Smother-thick. Caps visibility. |
| `precip` | enum {None, Drizzle, Rain, Sleet, Snow} + intensity | Cosmetic + minor visibility/handling effect; snow only Hard Winter. |
| `cloudCover` | float 0..1 | Dims daylight; feeds art. |
| `barometer_hPa` | float | Pressure. **Leads** storms (falling = deteriorating). The forecasting cue. |
| `visibility_m` | float | Derived: `min(fogVis, precipVis, nightVis)` → how far you can see (gates radar value in The Smother). |
| `frontId` / `frontPhase` | id + 0..1 | Which weather **front** is overhead and how far through it we are (§4.5). |

### 4.2 Wind model

- Wind is a **slowly-varying vector field**, generated by layered value/Perlin noise sampled at `(t, regionAnchor)` so it's smooth in time and space and **deterministic**:

  ```
  // base wind: smooth noise -> direction + speed, biased by season & region prevailing wind
  dirNoise   = fbm( seed_dir,   t * windDirDriftRate )          // slow rotation
  spdNoise   = fbm( seed_speed, t * windSpeedDriftRate )        // slow swell/lull
  baseDir    = lerp(region.prevailingDir, dirNoise*360, 0.6)    // prevailing wind + wander
  baseSpeed  = region.baseWind + spdNoise * region.windVariance + frontWindBoost(frontPhase)
  windVector = polar(baseDir, baseSpeed)
  ```

- **Gusts:** a higher-frequency, lower-amplitude noise term adds short-lived gusts on top of `baseSpeed` (matters for sail craft and broaching — see `boats-and-navigation.md`).
- **Prevailing wind** per region gives each place character (e.g. Ironbound's prevailing nor'wester; sou'westers in High Summer). Fronts (§4.5) temporarily override/boost it (e.g. a nor'easter ahead of a storm).

### 4.3 Sea state scale (named tiers — canon "calm→gale")

We use a Beaufort-flavored, **Maritime-named** scale. `seaState` is driven mostly by sustained `windSpeed` (with fetch/region modifiers; offshore builds bigger seas than a sheltered cove for the same wind):

| Tier | Name | Wind (m/s) | Sea feel | Gameplay |
|---|---|---|---|---|
| 0 | **Glass / Flat Calm** | 0–1.5 | Mirror, oily | Easiest handling; great for the dory. |
| 1 | **Ripple / Light Air** | 1.5–3.3 | Cat's-paws | Pleasant. |
| 2 | **Lop / Light Breeze** | 3.3–5.5 | Small wavelets | Inshore comfortable. |
| 3 | **Chop / Moderate** | 5.5–8.0 | Whitecaps begin | Dory starts shipping spray; mild handling cost. |
| 4 | **Popple / Fresh** | 8.0–10.8 | Frequent whitecaps, spray | Small craft uncomfortable; **dory at its safe limit**. |
| 5 | **Tumble / Strong** | 10.8–13.9 | Big whitecaps, building swell | Inshore boats labor; broaching risk rises. |
| 6 | **Knockabout / Near-Gale** | 13.9–17.2 | Heaped seas, foam streaks | Only seaworthy hulls (dragger+) belong out here. |
| 7 | **Gale** | 17.2–20.8 | Moderately high seas, blown spray | Dangerous; Ironbound/Banks weather-capable boats only. |
| 8 | **Storm / Living Gale** | 20.8+ | High seas, reduced visibility | End-game peril; even big boats want to be home (P5). Storm warnings precede it. |

- Each boat declares **`maxSafeSeaState`** (seaworthiness — next doc). Exceeding it ramps capsize/swamping risk. This is the **direct bridge** between this doc and the danger model in `boats-and-navigation.md`.
- **Continuous `roughness`** (0..1) interpolates within/around tiers for smooth wave visuals and a continuous physics input (no jarring step at a tier boundary).

### 4.4 Fog, rain, storms

- **Fog** (`fogDensity`): the Sablewick signature. Two flavors: **advection fog** (warm air over cold water — High Summer afternoons; rolls in fast) and **sea fog / mist** (calm, damp). Fog **caps `visibility_m`** (e.g. dense fog → 40–80 m), which:
  - Drastically shrinks how much sea you can see → you navigate by instrument/sound. **This is the entire premise of The Smother** (canon: permanent fog bank; navigate by instrument and sound). Radar/GPS upgrades (next doc) restore effective awareness — a clean upgrade payoff (P2/P5).
  - Slightly reduces fish-spotting/flotsam visibility (cosmetic + minor).
- **Rain/precip:** mostly atmosphere + small visibility/handling nudge. **Sleet/snow** only in Hard Winter (and The Turn shoulder). Heavy rain modestly reduces visibility.
- **Storms:** a storm is the **peak of a deteriorating front** (§4.5): high `seaState` (7–8), strong gusty `windVector`, low `barometer`, often rain, sometimes reduced visibility. Storms are **telegraphed**: barometer falls hours ahead, radio issues a **gale/storm warning**, swell builds, sky darkens. A skipper who reads the signs gets home; one who ignores them gets caught (P1+P5). **Lightning is atmosphere only** (no strike mechanic at launch — see Open Questions).

### 4.5 Weather fronts (moving, evolving systems)

Rather than rolling weather purely per-tick, we model **fronts**: discrete weather systems that **march across the archipelago over hours**, so weather *moves and evolves* (canon) and the *same front* hits different regions at different times (you can watch a storm coming from the west).

- A **`WeatherFront`** is spawned deterministically from a **schedule** seeded by `(worldSeed, season, dayIndex)`: a Poisson-ish sequence of fronts whose **frequency/severity is biased by season** (§4.6). Spawning from a seeded schedule (not live RNG) keeps determinism.
- Each front has: a **type** (Settled High, Warm Front (fog/drizzle), Cold Front (line squall), Gale, Named Storm), a **track** (entry edge, heading, speed across the map), a **timeline** (build → peak → clear), and **intensity**.
- At sample time, a region's weather = **blend of the base field (§4.2) + whatever front(s) overlap that region right now**, weighted by `frontPhase` (how central the front is over the region). As the front's track moves, regions transition in/out smoothly.
- **Forecast = look ahead on the schedule.** Because fronts are scheduled deterministically, the harbourmaster/radio can *describe the next 1–2 fronts* with increasing vagueness the further out they are (we add forecast "noise" so far-out forecasts are fuzzy — *uncertainty is intentional*, unlike the exact tide table).

  ```
  // Forecast confidence falls with lead time:
  forecastValue(field, leadHours) = trueFuture(field, leadHours)
                                   + noise(seed, field) * forecastError(leadHours, toolTier)
  // forecastError small near-term, grows with leadHours, shrinks with better tools/NPCs.
  ```

### 4.6 Weather ↔ season ↔ region correlation

`WeatherConfig` holds a **season × region** bias table. Defaults that hit the canon notes:

| Region | Character | Seasonal lean |
|---|---|---|
| **Coddle Cove** | Sheltered; sea state runs ~1 tier below open water for same wind. | Mild year-round (good tutorial). |
| **The Sunkers / Drownded Lands** | Inshore; moderate. | Summer fog notable on the flats. |
| **Fundy Rips** | Wind-against-tide kicks up steep dangerous chop even in moderate wind (overfalls). | Worse in storm seasons. |
| **The Banks** | Open ocean; biggest seas for given wind; long swells. | Rough in The Turn/Hard Winter. |
| **Ironbound** | **Stormy** (canon): highest storm frequency & severity, strong prevailing nor'westers, cold gales. | Hard Winter = brutal & most rewarding (P5). |
| **The Smother** | **Foggy** (canon): persistent high `fogDensity`, low visibility nearly always; relatively *low* wind (fog likes calm). | Fog all year; thickest in High Summer. |
| **Shipping Lanes** | Open-water exposure; weather windows matter for freight timing. | Storms delay/threaten cargo runs. |

- **Season biases** (in `WeatherConfig.seasonProfile`):
  - **Early Spring:** unsettled, raw, variable winds, lingering fog/mist.
  - **High Summer:** most settled spells, but **afternoon advection fog** common; occasional thunder/heat-driven squalls.
  - **The Turn:** the first real **gales**; great visibility on clear days, dramatic light.
  - **Hard Winter:** **storm season** — frequent, severe gales; cold, dry, often *clear-but-vicious*; fog less common than summer.

### 4.7 Forecasting weather (the player's tools — P1)

Three escalating instruments. The reward for using them is *foresight*; the cost of ignoring them is getting caught out (P5):

| Tool | Where | Gives | Confidence |
|---|---|---|---|
| **Barometer** (wall instrument; portable upgrade for the boat) | Cottage; later boat-mounted | Current `barometer_hPa` + trend arrow (rising/steady/falling). **Falling = weather coming.** The earliest, most reliable cue. | Trend is trustworthy; you infer the rest. |
| **Harbourmaster** (NPC at Port Greywick) | Port Greywick wharf | A spoken **daily forecast**: today + tomorrow sea state, wind, fog, any **gale/storm warning**. Friendlier/more detailed as relationship grows (P3 tie-in). | Good for ~24–48 h. |
| **Marine radio** (boat upgrade) | On the boat | **Live updates while at sea**: warnings as fronts develop, "small craft advisory", storm warnings. Critical for offshore (Banks/Ironbound) safety. | Best at-sea early-warning; updates as fronts move. |

- **The sky itself is a free, learnable forecast.** Telegraph cues — greasy swell, mares' tails cloud, a sudden lull, the glass dropping, fog bank on the horizon, a sickly light before a squall — are rendered/audible so an attentive player can read weather *without any tool*, then confirm with instruments. **Reading the signs is the mastery fantasy** (P1). (Optional later: a "weather lore" skill that surfaces these cues as subtle UI hints.)

### 4.8 Phased weather & sea enhancements (owner-ratified vision — M2+)

> **Future work, captured for consistency — not in the M0/M1 slice** (CLAUDE.md rule 8). These
> owner-ratified additions deepen the sea's moods beyond the M1 slice's wind+tide. **Phase: M2** (the
> weather / winter / fog wave — [`../roadmap.md`](../roadmap.md)). Each reconciles with an existing
> Open Question (§10). All stay **deterministic from `(worldSeed, gameTime)`** — no new save state.

- **Waves with magnitude that push boats.** Beyond drift (current) and stability stress (sea state),
  big seas apply a **physical push force** to the hull — a wave set that shoves you to leeward / onto
  a lee shore, strongest at high sea state and in the overfalls. This becomes an **explicit term in
  the FORCES sample** (a wave-push vector alongside `CurrentVector`/`WindVector`, §5.1) that
  [`boats-and-navigation.md`](boats-and-navigation.md) adds to drift and to the broach check (§3.2
  there). Reconciles **OQ6** (wind-against-tide overfalls become a larger wave-push). **M2.**
- **Wind gusts that travel across the water.** §4.2's gusts become **moving gust cells** — a
  cat's-paw / darkened patch of ruffled water that **propagates across the surface** and strikes the
  boat as a timed spike (heel/broach check, sail trim), so an attentive skipper **sees the gust
  coming** and eases for it (P1). A moving noise lobe keyed to `(seed, t)`: deterministic, cheap, and
  a readable tell. **M2.**
- **Lightning & heavy rain.** §4.4 keeps **lightning atmosphere-first** (rim-flash + thunder) and
  **heavy rain** as a **visibility + handling** factor (wetter, lower-vis, slightly worse traction).
  The owner wants both present as real storm weather; whether lightning ever becomes a **rare,
  telegraphed strike hazard** stays **OQ4** (leaning no — it risks tipping P5 from tense to
  punishing). Capture: rain/lightning are part of the M2 storm package; a *strike mechanic* is a
  later, optional, carefully-telegraphed call. **M2** (atmosphere) / OQ (mechanic).
- **Winter freezing in some areas.** Canon has Hard Winter as storm season; the owner ratifies that
  **some grounds actually freeze** in winter (not merely weather-gated). This **upgrades OQ3 from
  "flavor-only at launch" to a real M2 mechanic in *specific* regions**: ice that closes/blocks
  certain inshore water in Hard Winter, paired with the **ice-strengthened hull** upgrade
  ([`boats-and-navigation.md`](boats-and-navigation.md) §4.2). Kept **regional and seasonal** — not
  world-freezing — and **never save-stranding** (you can always get home / wait out the season).
  **M2** for the first frozen region(s); broader ice later.

---

## 5. The FORCES interface (consumed by `boats-and-navigation.md`)

> This section is the **contract** between this doc and boat physics. `EnvironmentService` produces per-tick **environment samples**; `boats-and-navigation.md` consumes them to compute drift, handling, grounding and danger. Neither side reaches into the other's internals — they meet here.

### 5.1 The environment sample (data shape)

`EnvironmentService.Sample(worldPosition, gameTime)` returns an immutable struct. This is the **one object** boat physics reads each physics tick:

```csharp
public readonly struct EnvironmentSample
{
    // --- Tide ---
    public readonly float  TideHeight;      // m above chart datum at this position's region
    public readonly float  TideRateNorm;    // -1 (max ebb) .. +1 (max flood); 0 = slack
    public readonly float  WaterDepth;      // m of water under this position (TideHeight - seabedElevation); <=0 = aground/dry

    // --- Currents & wind as forces ---
    public readonly Vector2 CurrentVector;  // m/s, world-space tidal current at this position (§3.7)
    public readonly Vector2 WindVector;     // m/s, world-space wind at this position (§4.2)
    public readonly float   GustFactor;     // 1.0 = steady; transient >1 spikes for broaching checks

    // --- Sea & visibility ---
    public readonly SeaState SeaStateTier;  // enum 0..8 (§4.3)
    public readonly float    Roughness;     // 0..1 continuous sea-state for physics/visuals
    public readonly float    Visibility;    // metres (fog/precip/night-capped)
    public readonly float    FogDensity;    // 0..1

    // --- Context ---
    public readonly RegionId Region;
    public readonly float    SeabedElevation; // m above datum at this position (for grounding math)
}
```

> **Forward note (M2, do not add yet):** the **wave-push** force in §4.8 will add **one field** to
> this struct (e.g. `Vector2 WavePush; // m/s² leeward shove from sea state/overfalls`) when the M2
> weather wave lands. It is called out here so the M1 contract above stays stable and the M2 addition
> is a *known, additive* change (a new field + a save-compatible bump), not a surprise reshape.

### 5.2 How wind & current produce force/drift (the spec boats implement)

This doc **defines the environmental quantities**; `boats-and-navigation.md` owns the boat's mass, drag, sail/hull coefficients and the actual `Rigidbody2D` integration. The agreed model:

- **Tidal current = the water the boat floats in is itself moving.** The boat's velocity *through the water* is `boatVelocity − currentVector`. Drag/thrust act on that relative velocity; in still throttle the boat is **set** (drifts) with `currentVector`. This is why timing the rips matters — at mid-tide the very medium you're in is sweeping you sideways.

  ```
  v_relWater   = boatVelocity - sample.CurrentVector
  dragForce    = -0.5 * rho_water * Cd_hull * area * |v_relWater| * v_relWater   // (boat doc owns coeffs)
  // with zero throttle, equilibrium drags boat toward currentVector -> it "sets" with the tide
  ```

- **Wind = a force on the boat's exposed area** (windage; much larger for sailing/high-sided craft, smaller for a low dory). Wind also acts on the *relative* air velocity:

  ```
  v_relAir   = boatVelocity - sample.WindVector
  windForce  = -0.5 * rho_air * Cd_windage * exposedArea * |v_relAir| * v_relAir
             // plus, for sail craft, a sail-lift term the boat doc computes from sail trim vs WindVector
  windForce *= sample.GustFactor          // gusts spike the force -> broaching/heeling checks
  ```

- **Net environmental force** handed to the boat each tick = `dragForce(currentMedium) + windForce` (+ sail thrust for sail craft). The boat adds engine thrust, rudder torque, hull stability, etc. **Larger/heavier boats → more inertia → slower to respond to all of the above** (canon handling spec lives in the boat doc).
- **Sea state → stability stress.** `Roughness` + `SeaStateTier` feed the boat's **broaching/swamping/capsize** checks (boat doc) — high sea state + bad heading/overload + sharp helm input = danger (P5).
- **Tide height/depth → grounding.** `WaterDepth` and `SeabedElevation` let the boat compare against its **draught**: `aground when draught > WaterDepth` (boat doc owns the consequence; this doc owns the water level).

### 5.3 Sampling contract & guarantees

- **Determinism:** for identical `(worldSeed, gameTime, worldPosition)`, `Sample(...)` is **bit-stable**. No internal RNG with hidden state — all noise is hash/Perlin keyed by seed+time+position. (Save = seed + `gameTime`; everything reconstructs.)
- **Spatial coherence:** samples vary smoothly in space (no per-tile popping) so a moving boat feels a continuous field. Region boundaries blend over a margin.
- **Cost:** `Sample` is cheap (a handful of trig/noise evals). Physics consumers call it at **4 Hz per active boat** (§8) and interpolate between; visuals can sample more often but read cached values.
- **Authority:** `EnvironmentService` is the *only* writer of environmental truth. UI (tide table, forecasts), audio, art lighting, fish spawns and boat physics are all **readers**. No system computes its own tide/weather.

---

## 6. Cross-system data flow (summary)

```
gameTime (double) ──┐
worldSeed ──────────┤
                    ▼
            EnvironmentService  (the one source of truth; §9)
                    │
   ┌────────────────┼─────────────────┬───────────────┬───────────────┐
   ▼                ▼                  ▼               ▼               ▼
 Tide model      Weather/front      Sun/Moon        Sea state     EnvironmentSample
 (§3)            scheduler (§4)      (§2.3/§3.3)     (§4.3)        (§5.1)  ──► boat physics
   │                │                  │               │             (boats-and-navigation.md)
   ▼                ▼                  ▼               ▼
 Tide table     Forecast tools     Daylight/art    Fish spawn weighting
 (§3.6)         (barometer/HM/      lighting        (fish-and-content.md)
                 radio §4.7)        (art bible)
   │
   ▼
 Region water levels / walkable flats / hazards (§3.5)  ──► on-foot walkability + grounding
```

---

## 7. Worked "a day on the water" (designer feel check, ties to pillars)

> *Hard Winter, day 9 of the season, two days past full moon (near-spring tides). Skipper plans an Ironbound run.*
> **Morning (plan, P1):** reads the **Harbourmaster's Charts** at the cottage (`timeFlowMultiplier=0`): spring **low water 07:50** (good — the Sunkers will be visible on the way out), but the **barometer is falling** and the harbourmaster warns of a **gale building from the nor'west by late afternoon**. Decision: go early, be home by 15:00.
> **Outbound (P1+P2):** times the **Fundy Rips slack at 08:10** to slip through (mid-tide later would be a 3 m/s wall). Sea state 4 (Popple) inshore — fine for the stern trawler (maxSafeSeaState 7), a death sentence for the dory.
> **At the grounds (P5 looming):** marine radio upgrades the warning to a **storm warning**; sea state climbing 5→6. Hold's half full of a rare Ironbound fish.
> **The gut-check (P5):** push for a full hold and risk getting caught in a gale offshore in worsening seas, or run for home now with a good (not great) trip? The skipper reads the swell building, the glass still dropping — and **turns for home**. Catches the next Rips slack at 14:30. Ties up at Greywick as the first storm gusts hit. **That decision — earned by reading tide + weather — is the game.**

---

## 8. The environment tick (cadence & perf)

- **`gameTime`**: advances every frame (smooth visuals).
- **Environment sample for physics**: **4 Hz** per *active* boat (the player's boat always; AI/crew boats only when on-screen or simulated — most are abstracted, see `economy-and-business.md`/`npcs-and-routines.md`). Physics interpolates between samples; this keeps trig/noise cost flat regardless of frame rate. Mobile target: a single active boat → ~4 samples/sec → negligible.
- **Weather/front re-evaluation**: **~1 Hz** (fronts move over minutes/hours; no need for high rate). Wind noise is smooth and can be sampled at sample-time cheaply.
- **Tide events / table build**: **on demand** (when the player opens the table, or once per day for the HUD widget) — never per frame.
- **Tide/sun/moon scalars for art**: cheap enough to evaluate per frame, but cache per-tick and lerp.

---

## 9. Implementation notes

### 9.1 The central service

- **`EnvironmentService`** (a single, save-light MonoBehaviour or plain C# service owned by a bootstrapper; **not** a scattered set of statics) holds: `worldSeed`, current `gameTime`, region profiles, weather config, the front scheduler, and the public API:
  - `EnvironmentSample Sample(Vector2 worldPos, double gameTime)`
  - `float TideHeight(RegionId, double t)`, `float WaterDepth(Vector2 worldPos, double t)`
  - `IReadOnlyList<TideEvent> GetTideTable(RegionId, double fromT, int horizonDays)`
  - `WeatherForecast GetForecast(RegionId, double fromT, int leadHours, ToolTier tier)`
  - `AccessWindow GetNextWindow(FeatureId, double fromT)`
  - events: `OnDayRollover(int dayIndex)`, `OnSeasonChange(Season)`, `OnStormWarning(WeatherFront)`, `OnTideSlack(RegionId)`.

### 9.2 Determinism & saves (the big win)

- **A save is essentially `{ worldSeed, gameTime, playerState, worldMutations }`.** Tide, weather, sun, moon, fronts are **recomputed** from `(worldSeed, gameTime)` on load — *we never serialize a weather snapshot.* This is robust, tiny, and forecast-consistent (the same future you were promised is the future you load into).
- All stochastic elements (wind noise, front schedule, gust timing) use **hash-based / Perlin noise keyed by `seed + quantizedTime + positionCell`** — never `System.Random` with hidden mutable state. Same inputs → same outputs forever.
- **Caveat to handle:** *player-driven* world changes that depend on weather (e.g. flotsam that washed up in a storm the player saw) — if those need to persist exactly, record the *outcome* in `worldMutations`, not the weather. Keep environment pure; record only player-observable consequences that must be sticky.

### 9.3 Config as data (canon: data-driven, ADR-0003)

- `EnvironmentConfig` (global constants: tidal-day, synodic month, time scale defaults, air/water density).
- `SeasonConfig[4]` (sun anchors, water-temp curve, weather season-profile).
- `RegionTideProfile` per region (meanLevel, meanAmplitude, springNeapRatio, phaseOffset, diurnalAmp, currentField, seabed heightfield ref, prevailing wind, base wind, fog bias).
- `WeatherConfig` (sea-state thresholds, front-type definitions, front spawn schedule params, forecast-error curves).
- All ScriptableObjects so designers tune in-editor without code (mirrors fish/economy data-driven approach).

### 9.4 Day rollover & discrete ticks

- Continuous systems read `gameTime` directly. Systems needing **once-per-day** ticks (market resupply, NPC schedule reset, crop/aging if any) subscribe to `OnDayRollover`. On a sleep/skip that crosses **multiple** midnights, fire `OnDayRollover` **once per crossed day** in order (so a 3-day skip resupplies the market 3 times) — important for economy correctness.

### 9.5 Mobile performance

- Everything here is **O(1) math per query** — no simulation grids ticking. Wind/current "fields" are *functions*, not stored arrays (optionally cache a coarse grid per region for visuals).
- Sample at the cadences in §8; interpolate. One active boat dominates cost.
- Heightfields for tidal regions are authored data (textures/tilemaps), read-only at runtime — no per-frame allocation. Use struct samples (no GC churn in the hot path).
- Forecast/table generation is on-demand and can run on a worker or be spread across frames if a 28-day build ever feels heavy (it won't at 2-min sampling, but the option exists).

---

## 10. Open questions

1. **Tide datum vs. art sea level.** We define tide as metres above chart datum, but the ¾ top-down camera shows water as a *plane*, not a side elevation. Need an agreed mapping from `tideHeight`/`waterDepth` → **visual cues** (shoreline waterline position, exposed-rock sprites toggling, flat tiles switching wet→dry, float heights). Coordinate with `art-and-audio-bible.md`. (Likely: per-region a small set of waterline states + tile wet/dry swaps keyed off `waterDepth` thresholds, rather than true vertical displacement.)
2. **Diurnal inequality on/off at launch?** The optional second term (§3.4) adds realism ("the morning low is the big one") but also planning complexity. Ship semidiurnal-only for the tutorial regions, enable inequality for advanced regions? Or globally? Decide with playtest.
3. **Ice / freezing in Hard Winter.** Canon mentions some grounds may "freeze/close." Is winter ice **flavor only** (atmosphere + soft weather-gating) or a **mechanic** (literal ice tiles blocking water, ice-strengthened hulls)? Recommend flavor-only at launch; revisit as an Ironbound/late feature. **Owner-ratified (2026):** winter freezing becomes a **real M2 mechanic in *some* regions** (ice closes specific inshore water; pairs with ice-strengthened hulls) — see §4.8. Launch / M1 stays flavor-only.
4. **Lightning / squall strikes.** Currently atmosphere-only. Do we ever want a rare, telegraphed lightning hazard, or does that tip P5 from "tense" toward "punishing"? Leaning no. **Owner lists lightning + heavy rain as M2 storm weather** (§4.8): rain/lightning ship as *atmosphere/visibility*; a strike *mechanic* remains this OQ's open call (still leaning no).
5. **Forecast-error curve shape.** How fuzzy should a 2-day-out forecast be, and how much should relationship-with-harbourmaster (P3) and tool tier (P2) sharpen it? Needs tuning so forecasts feel *useful but not omniscient*.
6. **Wind-against-tide overfalls.** Fundy Rips should kick up dangerous steep seas when strong wind opposes a strong tide (`windVector` vs `currentVector` anti-aligned at mid-tide). Is this an explicit `seaState` bump in the sample, or emergent in the boat's stability check? Recommend an explicit local sea-state modifier in `RegionTideProfile` so it's tunable and readable. Now also folds into the **wave-push force** in §4.8 (M2) — overfall steepness becomes a stronger push.
7. **Multiple simultaneous fronts.** Blending two overlapping fronts (§4.5) — cap at one dominant front per region for clarity, or allow genuine blends? Start with one-dominant for readability; allow blends only if it doesn't confuse forecasting.
8. **Per-region vs per-position tide phase.** We currently compute tide per *region* (with a `phaseOffset`). Do we ever need *within-region* tide phase variation (a long narrows where one end floods before the other)? Probably not at launch; flag for Fundy Rips if it ever feels wrong.
```