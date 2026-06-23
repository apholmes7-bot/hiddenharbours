# Hidden Harbours — Art & Audio Bible

> Design module. Subordinate to `../vision-and-pillars.md` (CANON). Locks the perspective, the
> **PPU=32 / 1 world unit = 1 m / 32×32 base tile** scale standard, the palette, and the sprite/
> audio pipeline. Serves every pillar but especially **P1 (The Sea Has Moods)**, **P2 (From Dory
> to Dynasty — scale must read on screen)**, and **P3 (A Living Working Coast)**.
>
> Sibling docs: `../project-structure.md` (asset folders, Git LFS), `boats-and-navigation.md`
> (boat metres), `world-and-regions.md` (region identity), `time-tides-weather.md`
> (tide/weather/season state we render), `ux-and-mobile-controls.md` (UI behaviour/HUD; this doc
> sets UI *art direction*), `../adr/0001-engine-choice.md` (Unity 6.3, 2D URP).

---

## 1. North star

A working North Atlantic coast that is **cozy, weathered, and quietly dangerous** — Stardew's
readable ¾ top-down clarity wearing Kingdoms Two Crowns' painterly light and limited palette. The
player should feel salt on the wind. Every art and audio decision answers to one question: *does
this make the sea feel like a real, moody force you read and respect?* (P1).

Three non-negotiables:

1. **One perspective everywhere** (¾ top-down). Land, town, and on-water share it.
2. **One scale, always** (PPU=32, 1 tile = 1 m). The scale fantasy (a tanker dwarfing a dory) is
   *load-bearing* and must never be faked with inconsistent sprite scaling (P2).
3. **A limited, cohesive palette** that shifts with mood (season/weather/time/fog/region) but never
   loses its salt-stained North Atlantic identity.

---

## 2. Perspective (LOCKED)

**Canonical camera: ¾ top-down**, Stardew Valley as the geometric base — a high, slightly
forward-tilted orthographic view where the ground plane reads top-down but objects (people,
buildings, boats, masts, trees) are drawn with a visible *front face* and cast short forward
shadows. This gives readability of a top-down grid with the charm and verticality of a side view.

On top of that base we layer **Kingdoms Two Crowns mood**:

- **Painterly parallax.** Multiple scrolling depth layers (far sky/horizon haze → distant
  headlands/islands → mid water/coast → play plane → near foreground framing: rigging, a buoy, reeds
  at the screen edge). Parallax sells depth without breaking the ¾ read.
- **Atmospheric lighting.** A global light/colour grade per time-of-day and weather (warm low sun,
  flat grey overcast, blue dusk, lantern-lit night), plus local light sources (lighthouses,
  windows, deck lamps, the town at dusk).
- **Limited palette for mood** (see §4): scenes are colour-graded toward a small ramp so each
  region/weather/time has a unifying tonal signature.

### 2.1 Land, town, and on-water in one perspective

- **Land & town** are tile-based ¾ scenes (clapboard buildings, wharves, slipways, roads, gardens),
  with NPCs and the player walking the grid. Buildings drawn with front faces and rooflines.
- **On-water** uses the *same* camera and grid: the boat is a sprite on an animated water tilemap
  /shader, with parallax water depth layers beneath and weather/light above. Sailing is not a
  separate "mode camera" — you simply drive off the wharf and the same view follows you out (a
  smooth follow-cam, see §3.7). This continuity is core to "one world."
- **The waterline** (where land meets sea) is an autotiled transition that is **tide-aware**: the
  drawn shoreline literally moves as the tide height changes (more beach/rock/mud exposed at low
  water). This is the single most important art expression of P1.

### 2.2 Reading the seabed at low tide (The Drownded Lands)

In **The Drownded Lands** (and tide-pools in **The Sunkers**), low water exposes *walkable seabed*.
In the ¾ view this reads as a **tide-driven autotile/overlay swap on the same ground grid**:

- At **high water**: the tiles are water (animated surface, parallax depth, your boat floats over).
- As the tide **falls**: water tiles retreat outward from the high points; a wet-transition ring
  (glistening mud/sand/weed, tide-pools catching sky reflections, stranded kelp, exposed wreck
  timbers) is drawn at the moving edge. The player can step onto newly-exposed seabed and walk it
  (clams, wrecks, secrets).
- As the tide **rises**: the sequence reverses and *floods* the flats — the danger beat (get caught
  out, P5). A clear visual "the water is coming back" cue (advancing wet edge + audio) is essential.

The same `WaterLevel` value that the simulation uses for grounding drives this rendering, so what
the player *sees* and what the physics *does* are the same truth (P1 integrity).

---

## 3. The scale standard & sprite/tile specs

### 3.1 The locked numbers

| Constant | Value | Meaning |
|---|---|---|
| **PPU (Pixels Per Unit)** | **32** | Every sprite imports at 32 px = 1 Unity unit |
| **World unit** | **1 metre** | 1 Unity unit = 1 m in the fiction |
| **Base tile** | **32 × 32 px** | = 1 m² of ground |
| **Result** | constant scale | Real metres → consistent pixels everywhere. **Never** scale a sprite to fake size. |

> **The rule that makes the game's scale fantasy work (P2):** *art is authored at true metric
> footprint and a single PPU.* A 110 m tanker is ~3,520 px long; a 4.5 m dory is ~144 px long.
> The tanker is ~24× the dory because it *is* ~24× the dory. We never cheat this with per-object
> scaling — only the camera zoom changes (§3.6).

### 3.2 The human sprite (the scale anchor)

The player and NPCs are the reference the eye calibrates against.

| Property | Value | Notes |
|---|---|---|
| In-fiction height | ~1.8 m | Canon |
| True pixel height (1:1) | ~58 px | 1.8 m × 32 px/m |
| **Authored sprite height** | **~64 px** (heroic) | Slight **heroic proportion** — bumped to ~64 px and drawn with a marginally larger head/hands and chunkier silhouette for readability at phone size. The *footprint/collision* stays honest (~1 m wide). |
| Footprint (collision) | ~1 m (≈32 px) wide | So crowds/space read truthfully |
| Frame canvas | 32 × 64 px cell | Single-tile-wide, two-tiles-tall cell (see sheet layout §3.4) |

The heroic bump is a **readability allowance**, applied *only* to the human silhouette, not to
boats/buildings — those stay strictly metric so the scale ladder reads. The human at ~64 px next to
a 144 px dory next to a 3,520 px tanker is exactly the gut-punch of scale we want.

### 3.3 Boat tiers → pixel footprint

Lengths are **locked canon** (`../vision-and-pillars.md` §5.4 / detail in
`boats-and-navigation.md`). Beam (width) figures below are *art-direction estimates* for sprite
sizing — confirm against `boats-and-navigation.md` if it specifies beam. Pixel sizes = metres ×
32, rounded to convenient sprite dimensions (and to a sensible power-of-two-ish atlas cell). "Cell"
is the recommended sprite canvas (includes a little margin for wake/overhang).

| Tier | Boat | Length (m) | Beam est. (m) | Hull px (L×W) | Suggested sprite cell (px) |
|---|---|---|---|---|---|
| 0 | **The Dory** | 4.5 | 1.6 | ~144 × 51 | 160 × 64 |
| 1 | **Punt / Skiff** | 6 | 2.0 | ~192 × 64 | 224 × 96 |
| 2 | **Cape Islander** | 13 | 4.3 | ~416 × 138 | 448 × 160 |
| 3 | **Lobster Boat** | 12 | 4.0 | ~384 × 128 | 416 × 160 |
| 4 | **Side Dragger / Trawler** | 25 | 7.0 | ~800 × 224 | 832 × 256 |
| 5 | **Stern Trawler / Seiner** | 38 | 9.5 | ~1216 × 304 | 1280 × 352 |
| 6 | **Coastal Packet / Freighter** | 60 | 11 | ~1920 × 352 | 2048 × 384 |
| 7 | **Coastal Tanker / Cargo Ship** | 110 | 16 | ~3520 × 512 | 3584 × 576 |

Notes:
- **A human (~64 px) standing on the dory (~144 px) fills a big chunk of it; on the tanker
  (~3,520 px) they are a speck.** That contrast *is* the dynasty fantasy made visible (P2).
- Large hulls (T5–T7) exceed a single texture comfortably; author them in **sections / as a small
  set of stitched sub-sprites** or a modular hull (bow / mid / stern + deckhouse) on one atlas. See
  §3.5 boat approach.
- These cells are big — **respect the LFS / atlas guidance in §8** so the repo stays lean.

### 3.4 Character sprite sheet & animation (¾ view)

- **Cell:** 32 × 64 px (1 m wide footprint, ~2 m tall canvas with headroom).
- **Facing directions:** the ¾ view uses **4 cardinal facings** drawn as distinct art —
  **down (toward camera), up (away), left, right** (right is the mirror of left to save art where
  asymmetry allows; bias-critical items like a slung satchel may need true L/R). 4 directions is the
  Stardew standard and is plenty for ¾ readability on a phone.
- **Animation states per direction:**
  - **Idle** — 1–2 frames, subtle breathing/sway (2 fps feel).
  - **Walk** — 4–8 frame cycle (~8–10 fps).
  - **Work** verbs — short loops for: **fish/haul**, **lift/carry**, **use/interact**, **row**
    (dory). 3–6 frames each.
  - **Special** one-shots — board boat, catch celebration, stagger (rough sea), sleep.
- **Sheet layout:** horizontal strips per state, stacked by direction (row = direction, columns =
  frames), one PNG atlas per character archetype. Use Unity's sliced multiple-sprite import +
  Sprite Library / Animator. Modular cosmetic layers (oilskins, hats) as overlay sprites sharing the
  rig where feasible.
- **Pixel discipline:** integer-pixel movement on the play grid; no sub-pixel jitter (pixel-perfect
  camera, §7). Shadows are a separate soft blob sprite under the feet, scaled by sun state.

### 3.5 Boat sprite approach

A boat is composed, not a single flat sprite, so it can *animate and crew*:

1. **Hull** — the base sprite (sized per §3.3), one per facing. For small boats, 4 facings (or even
   free-rotated sprite for smooth turning, since boats turn continuously — see below). For very large
   ships, fewer discrete headings + smooth interpolation, or modular bow/mid/stern + deckhouse.
   - *Rotation:* boats turn continuously, unlike characters. Two options, decide in production:
     **(a)** pre-rendered rotation frames at e.g. 16–32 headings (crisper pixels, more art/atlas), or
     **(b)** a single top sprite rotated by the engine with pixel-perfect snapping (cheaper, slight
     pixel shimmer). Recommendation: **(b) for tiny boats** (forgivable), **(a) for hero/large boats**
     where shimmer would be ugly. Confirm per tier.
2. **Wake / spray** — a separate animated layer behind/around the hull, *scaled by speed and turn
   rate*, tinted to water/weather. Bigger boats throw bigger wakes (reinforces mass).
3. **Crew & deck activity** — crew sprites placed on deck anchor points, doing work animations
   (hauling, sorting). Visible crew = visible progression (a lone you in the dory vs a busy deck on
   the dragger; P2/P4). Deck gear (traps, nets, winches, containers) as overlay sprites.
4. **Damage / load state** (optional polish) — a fuller hull sits lower (load), and rough-sea
   pitch/roll is a subtle vertical bob + slight rotation on the whole assembly.

#### 3.5.1 Boat art conventions (forward-compatible with the M2 bake)

These three rules are **locked now** so the planned M2 boat pipeline (pre-rendered 3D → sprite
sheets — `adr/0006-boat-art-pipeline.md`, *Proposed*) can drop in **without re-placing or re-lighting
the world**. They are cheap to honour now and expensive to retrofit, so **all boat art from here on
obeys them** — including the current slice placeholders.

1. **One implied light direction, across all art.** The fixed *sculpt/key* light that shades forms in
   the pixels is **high, from the top of the frame and slightly behind/above the camera** — so objects
   show a lit **front face** and cast **short shadows forward / down-screen** (this is the §2 LOCKED
   read). Boat hulls are authored **bow-up**, so on the sprite this key falls from the **bow/top of
   the canvas**. This is **distinct from the dynamic day-night colour grade** (§6), which is a global
   light applied in-engine on top — *that* one moves; the **baked sculpt-light must not vary** between
   sprites. The **M2 bake's virtual key light must match this direction and elevation** exactly, or
   baked hulls will read as lit differently from everything around them.
2. **Per-hull pivot/origin + metric footprint are pinned.** Each hull's **pivot = hull centre**
   (the VS-23 import lock already stamps `Center` for `Art/Boats/`) and its **metric footprint** (the
   §3.3 length × beam → pixels) are the **placement contract**. A placeholder-sprite → baked-sheet
   swap **must preserve both**, so the boat never shifts on the water, at the wharf, or in a docking
   slot when the art is replaced. Treat the pivot + footprint as fixed even if the *art inside the
   canvas* changes. (If a placeholder isn't yet at canon length — see §3.3 — the **M2 bake** is what
   reconciles it to the pinned footprint, not a quiet rescale of the placeholder.)
3. **FX follows direction.** Wakes, spray, and nav/deck lights are currently baked **bow-up** (the art
   only ever faces up). The moment boats carry **real headings** and turn within the fixed world, these
   must **rotate with the hull** (a wake trails astern of the *actual* stern; a starboard light stays
   starboard) — or be **baked per-heading** alongside the hull. Do **not** leave bow-up FX pinned to a
   rotating boat. **Flag for VS-26 (boat-feel):** wire wake/lights to heading when continuous turning
   lands.

Water is a **first-class P1 system**, not a backdrop:

- **Surface:** animated, tiled water on the ground grid — scrolling normal/specular highlights,
  gentle swell, and **sea-state-driven** amplitude (glassy calm → white-capped gale). Implemented as
  a **URP shader** on the water tilemap/quad (preferred) with an overlay fallback.
- **Parallax depth:** beneath the surface layer, 1–2 darker parallax layers (depth gradient,
  shadows of the deep) so open water reads as *deep*, not flat.
- **Tide-driven coverage:** the water *extent* is driven by `WaterLevel` (§2.1/§2.2) — the shoreline
  and flats are redrawn as the tide moves. This is the headline P1 visual.
- **Reflections & light:** sky/sun colour and lighthouse beams glint on the surface; night water is
  darker with sparse highlights; fog flattens and greys it.
- **Foam & edges:** autotiled foam where water meets land/hull/rocks; sunker rocks show breaking
  white water as a hazard tell (P5).

### 3.7 Camera & zoom strategy

The hard problem: a 144 px dory and a 3,584 px tanker must *both* feel right with **one constant
PPU**. Solution = **camera distance (orthographic size) adapts; PPU never does.**

- **Pixel-perfect, integer-zoom camera.** Use Unity's Pixel Perfect Camera (URP) at a base
  resolution. Zoom changes happen in **integer pixel-perfect steps** (1×, 2×, …) or as smoothly
  interpolated orthographic-size changes that still snap rendering to whole pixels, to avoid
  shimmer.
- **Default play zoom (intimacy):** tuned so a person and a small boat feel personal — roughly
  **~12–16 m of world height visible** on a phone in the default fishing/walking view. The dory and
  the wharf fill the frame; cozy and close (the small-craft romance of Kingdoms Two Crowns).
- **Auto-zoom-out for big hulls / open water:** when piloting a large vessel or out on open grounds,
  the follow-cam **eases to a wider orthographic size** so the whole ship fits and you can see
  weather/hazards coming. A 110 m tanker simply *cannot* fit at intimate zoom — so the camera pulls
  back, and the very act of zooming out *communicates* the ship's enormity (the world shrinks around
  it). Bind zoom band to current vessel size + context (docked/inshore/offshore).
- **Player zoom control:** allow a pinch-zoom within sane clamps so players can choose intimacy vs
  overview, but clamp per context so they can't break readability or the scale read.
- **Follow & lookahead:** smooth follow with slight lookahead in the direction of travel (and in
  wind/current direction at sea, to help the player read set & drift — supports P1 navigation).

---

## 4. Palette & mood

### 4.1 Master palette (North Atlantic)

A cohesive, limited master ramp — **slate blues, fog greys, weathered-wood browns, sea-glass
greens**, with a few **saturated pops** held in reserve for human-made colour (buoys, oilskins,
painted hulls) so the eye goes where the *life* is (P3). The pops are precious — used sparingly so
they read as warmth against the cold sea.

Example **22-colour master ramp** (hex; tune in Aseprite, but this is the locked *intent*):

| Role | Name | Hex |
|---|---|---|
| **Sea / sky cool ramp** | Deepwater navy | `#16242E` |
| | Slate blue (deep) | `#1E3A4C` |
| | Slate blue (mid) | `#2F6079` |
| | Atlantic blue | `#4A8AA6` |
| | Pale sky / haze | `#A9C7D4` |
| **Fog / neutral greys** | Storm grey (dark) | `#3A4248` |
| | Fog grey (mid) | `#6E7A80` |
| | Mist grey (light) | `#A7B0B3` |
| | Sea-foam white | `#E8EEF0` |
| **Wood / earth warm ramp** | Tarred timber (dark) | `#241A14` |
| | Weathered wood (brown) | `#5A4632` |
| | Driftwood tan | `#8A6F4E` |
| | Wet sand / mud | `#B49A74` |
| | Dry rope / canvas | `#D8C39A` |
| **Greens (sea-glass / weed)** | Kelp green (dark) | `#243A2E` |
| | Sea-glass green | `#4F8A6B` |
| | Lichen / moss | `#7FA87E` |
| **Saturated pops (life)** | Lobster-buoy red | `#C0392B` |
| | Buoy orange | `#E07B27` |
| | Oilskin yellow | `#E8B23A` |
| | Painted-hull teal | `#2BA39A` |
| | Lighthouse warm light | `#F4D58D` |

Discipline: **scenes use the cool/neutral/warm ramps for environment** and reserve the **pops** for
small, intentional accents. A foggy grey morning with one red buoy glowing is the signature image.

### 4.2 How the palette shifts (the moods of the sea — P1)

The master ramp is graded/tinted per condition (a global colour-grade + light, see §6), never
swapped wholesale — identity persists, mood changes:

| Axis | Shift |
|---|---|
| **Time of day** | Dawn: warm low rim-light, long cool shadows. Day: fuller saturation, neutral. Dusk: amber→violet grade, windows glow. Night: deep blues, low value, point lights (windows, lighthouses, deck lamps) carry the scene. |
| **Weather** | Clear: cleaner highlights, more saturation. Overcast: compress to greys, flatten contrast. Rain: darker, desaturated, wet-sheen speculars, drips. Storm: low value, high contrast, whitecaps, lightning rim-flashes (P5). |
| **Season** | Spring: cool but brightening, fresh greens. Summer: warmest, fullest. Autumn: amber/ochre push, lower sun. Winter: bleached, blue-shifted, low light, ice/snow accents, breath fog. |
| **Fog** | Lift the black point, crush saturation toward grey, reduce parallax-layer contrast with distance (aerial perspective), shorten visible range. Fog is both art and *mechanic* (you navigate by instrument/sound — P1). |
| **Region** | Each region biases the grade (see §4.3) so travel *feels* like crossing into a new mood. |

### 4.3 Per-region grade

| Region | Grade direction |
|---|---|
| **Coddle Cove** | Balanced, warmest, "home" — full master ramp, cozy. |
| **The Sunkers** | Cool with green tide-pool pops; white breaking water on rocks as hazard tells. |
| **Port Greywick** | Warm human colour (painted clapboard, signage, lit windows) — the most *colourful* place (P3 life). |
| **The Drownded Lands** | Wide, low, big-sky; wet-mud browns and reflective tide-pools at low water; eerie expanse. |
| **Fundy Rips** | High-energy water, churned surface, strong directional light on the current race. |
| **The Banks** | Open, deep, fewer landmarks; deepwater navy dominant; weather is the spectacle. |
| **Ironbound** | **Colder & darker** — blue-shifted, lower value, stormier, bleak grandeur (end-game teeth, P5). |
| **The Smother** | **Desaturated** — near-monochrome fog grey, range collapsed, shapes loom; instruments & sound carry it (eerie). |
| **The Shipping Lanes** | Industrial maritime — bigger structures, channel markers, harder edges, the scale of commerce (P2). |

---

## 5. Tile & environment specs

- **Tile set approach:** modular 32×32 terrain/ground tiles per biome (wharf decking, cobble/dirt
  roads, grass/heath, rock, sand, mud, kelp, interior floors). Authored as atlases per environment.
- **Autotiling:** use Unity **Rule Tiles** (2D Tilemap) for terrain transitions (grass↔rock,
  land↔water shoreline, road edges, wharf edges) so designers paint regions fast and coastline reads
  cleanly. The **shoreline/flats rule tile is tide-aware** (its drawn state keys off `WaterLevel`).
- **Buildings & props:** ¾ buildings drawn with front faces + rooflines, sized to metric footprint
  (a cottage ~ several tiles). Props (traps, nets, barrels, crates, dories on the hard, lighthouses)
  as individual sprites on a sorting layer with Y-sorting for correct overlap.
- **Sorting / depth:** Y-sort the play plane so characters/props/boats overlap correctly in ¾ view;
  parallax layers live on separate sorting layers behind/in front.
- **Lighting / day-night / fog:** see §6.

---

## 6. Lighting, day-night & fog (shader/overlay strategy)

Recommended: **2D URP Lights + a global colour-grade**, with **fog as a layered overlay/shader**:

- **Global day-night:** a `Global Light 2D` whose colour & intensity are driven by the time-of-day
  curve (from `time-tides-weather.md`), plus a post-process **colour-grade/LUT** per
  weather/season/region (§4). This is the cheap, mobile-friendly backbone.
- **Local lights:** `Point/Spot Light 2D` for lighthouses (sweeping beam), windows, deck lamps,
  shop interiors — these *pop* at dusk/night and in fog (a lighthouse in The Smother is a gameplay
  beacon, not just decor — P1).
- **Fog:** a combination of (a) a **fog overlay** (animated, parallax, density-graded with distance)
  and (b) **reduced view range / aerial-perspective tint** on parallax layers. Keep it shader-light
  for mobile; fog density is a *gameplay value* (drives The Smother's instrument navigation).
- **Weather VFX:** rain/snow as lightweight particle overlays + the wet/blue grade; lightning as
  brief global rim-flash. Wind expressed visually via flag/sail/foliage sway and wave amplitude (so
  the player *sees* wind, supporting the HUD's wind read — `ux-and-mobile-controls.md`).
- **Mobile budget:** prefer one global grade + a few local lights + overlay particles over many
  realtime 2D lights. Profile on a mid-range phone; provide a **reduce-motion** path (calmer water,
  fewer particles — see accessibility in the UX doc).

### 6.1 Advanced rendering roadmap (owner-ratified vision — phased M2/M3)

> **Future work, captured for consistency — not in the M0/M1 slice** (CLAUDE.md rule 8). The M1 slice
> ships the **§3.6 water + §6 global-grade backbone**. These owner-ratified ambitions deepen the look
> later. **Phase: the wet-surface tide effects ≈ M2; the heavier rendering (3D-water→2D bake, dynamic
> & cloud shadows, parallax-underwater preview) ≈ M3** ([`../roadmap.md`](../roadmap.md)). Each must
> hold the LOCKED rules (§2 one perspective, §3 one scale / PPU) and the desktop perf budget
> (mobile-portable).

- **3D water baked to a 2D surface (M3).** Author the hero water look by **rendering a 3D water sim and
  baking it down to the 2D surface** sprite/shader — richer swell, refraction and foam than
  hand-pixelled tiles, while staying a flat ¾ surface on the grid. Mirrors the **M2 boat bake**
  philosophy ([`../adr/0006-boat-art-pipeline.md`](../adr/0006-boat-art-pipeline.md)): author in 3D,
  ship as 2D, with **one implied light direction** (§3.5.1) so baked water lights consistently with
  everything around it. A profiled spike decides bake-to-frames vs a runtime shader (extends **OQ2**).
- **Dynamic shadows on the 24-h clock (M3).** Shadows that **lengthen, shorten and swing** with the
  sun's altitude/azimuth from the time-of-day curve ([`time-tides-weather.md`](time-tides-weather.md)
  §2.3) — dawn rake → short noon → long dusk — instead of the static blob shadows of the slice (§3.4).
  A major mood/P1 upgrade; must stay cheap (projected/skewed sprite shadows, not realtime shadow maps)
  and respect the **fixed baked sculpt-light** (§3.5.1), which does *not* move.
- **Cloud shadows (M3).** Soft **cloud-shadow patches drifting across land and water** (tied to
  `cloudCover` + wind), so the light *breathes* — a Kingdoms-Two-Crowns staple that makes the open
  coast feel vast and alive (P1). A cheap scrolling overlay; density follows the weather state.
- **Wet-surface tide effects (M2).** As the tide **falls (~3–4 m range)**, harbour walls, pilings,
  slipways, rock and mud are **revealed wet and glistening**, drying over time as the water leaves —
  the visual half of the tide-tell value set in [`time-tides-weather.md`](time-tides-weather.md) §3.5.
  Extends the already-locked **tide-aware shoreline** (§2.1/§2.2) from "the waterline moves" to "the
  bared surfaces read *wet*." Lands with the **M2 region art passes**; the **St Peters tide-gated
  sandbar** (the low-water walking path to Greywick) is the gameplay-critical case
  ([`world-and-regions.md`](world-and-regions.md) §6.0, §7).
- **Parallax underwater / shallow-water preview (M3).** A **parallax peek beneath the surface** in
  shallow water — you can *see* the bar, the sunker, the clam bed, the seabed shelving away — turning
  the §3.6 sub-surface parallax layers into a **readable shallow-water preview** that telegraphs
  grounding hazards and forage (P1/P5: see the rock before you strike it, see the bed before you dig).
  Pairs with the depth sounder ([`boats-and-navigation.md`](boats-and-navigation.md) §4.3) as the
  *visual* counterpart to the instrument.

---

## 7. UI art direction

**Diegetic, weathered, nautical — but ruthlessly readable on a phone.** The fiction is a working
skipper's tools; the constraint is a thumb and a 6-inch screen. *Readability wins every tie.*

- **Material language:** aged brass instruments, varnished wood panels, painted enamel signage,
  knotted rope frames, canvas/oilcloth backings, hand-drawn nautical charts, chalk-on-slate prices
  at the market. Think a tactile captain's desk, not a sci-fi HUD.
- **Key diegetic objects** become UI: the **tide table** (a printed almanac page), the **compass /
  binnacle**, the **barometer** (forecast), the **chart** (map), the **manifest/ledger** (inventory
  & business), **chalkboard** (market prices), **brass throttle** (sailing control art).
- **Readability rules:** high text contrast over busy textures (drop a subtle scrim/parchment panel
  behind text); generous touch targets (min ~44×44 pt); a clean pixel/near-pixel UI font with a
  legible fallback; icons that read at 24–32 px. The *frame* is weathered; the *information* is
  crisp.
- **Pixel consistency:** UI art is pixel-art-styled to match the world, authored at integer scale
  and snapped, but UI may render at a higher effective resolution than the world for text crispness
  (a common pixel-game approach — confirm with the UX doc's UI Toolkit/uGUI decision).
- **This doc owns UI *look*; `ux-and-mobile-controls.md` owns UI *layout/behaviour* and the HUD
  spec.** Keep them in sync (the HUD's tide/wind/compass widgets must be both *legible*, their job,
  and *diegetic-weathered*, our job).

---

## 8. Audio direction

Audio is half of P1: the sea should be *heard* changing before it is dangerous. Warm, sparse,
maritime — never wallpaper.

### 8.1 Ambient beds (responsive — P1)

- **Layered ambience** mixed live from weather/region/time:
  - **Sea state:** lapping calm → rhythmic swell → crashing surf/whitecaps, cross-faded by sea-state
    value.
  - **Wind:** a wind bed whose **level/pitch rises with wind strength** — the single most important
    audio tell. **Rising wind precedes a storm** (an audible forecast; the player learns to trust it
    — P5 danger telegraph).
  - **Wildlife:** gulls and shorebirds inshore/at the wharf; fewer, lonelier calls offshore; eerie
    near-silence + foghorn in The Smother.
  - **Weather:** rain on deck/roof, sleet, distant thunder; muffled, close-pressing quiet in fog.
- **Region recolours the bed:** Coddle Cove = gentle/home; Ironbound = harsh wind & surf; The
  Smother = muffled, sparse, a slow foghorn and your own creaking hull; Greywick = town hum.

### 8.2 Diegetic harbour & deck sounds (P3 life)

Creaking timber & rigging, hull slap, mooring lines, winches, the outboard's putter vs the
dragger's diesel thrum (engine voice scales with boat tier — another *audible* progression cue),
trap/net handling, fish on deck, the auctioneer's chant and market chatter, footsteps on decking
vs cobble, lighthouse bell, ferry horn. The town and wharves should sound *inhabited*.

### 8.3 Music tone

- **Folk / maritime, sparse, warm.** Acoustic and intimate — guitar, fiddle, concertina, a low
  drone, occasional wordless voice; Maritime/Newfoundland folk DNA without pastiche. Often the score
  steps *back* to let the sea speak.
- **Adaptive by context:**
  - **Region** — each has a tonal palette (Coddle Cove warm & hopeful; Greywick livelier & social;
    Ironbound stark & vast; The Smother eerie/minimal; The Banks open & lonely).
  - **Weather/tension** — calm days drift; as wind/sea/storm rises, music thins, darkens, adds
    low-end tension (or drops to near-silence so the *wind* is the score), then resolves to warmth
    when you reach safe harbour (the "made it home" exhale).
  - **Beats** — gentle stings for a good catch, a landed legendary, a big purchase (the dynasty
    swell), and a low, lonely cue for danger (aground, lost in fog, load lost).
- **Implementation:** vertical layering (stems faded by intensity) + horizontal cues per
  region/state, ducked under important diegetic sounds (rising wind, the foghorn).

### 8.4 Audio reinforces danger (P5)

The contract with the player: **you can hear trouble coming.** Wind rises, gulls scatter, the music
thins, the barometer-tied cue sours — *before* the storm hits. Getting caught out should feel like
"the sea warned me," not "the game ambushed me." Audio is the primary early-warning channel, backed
by the HUD (UX doc).

---

## 9. Pixel-art pipeline (practical)

### 9.1 Authoring

- **Aseprite** is the recommended pixel-art tool (sprites, sheets, animation, palette files).
  - Keep the **master palette** (§4.1) as a shared `.gpl`/`.aseprite` palette; author against it.
  - Use Aseprite **tags** for animation states; export **sprite sheets** (PNG + JSON) with
    consistent cell sizes per §3.
  - Author boats at true metric pixel footprint (§3.3); large hulls as sections/modules on an atlas.

### 9.2 Unity import settings (LOCK these per `../adr/0001-engine-choice.md`, 2D URP)

| Setting | Value | Why |
|---|---|---|
| Texture Type | **Sprite (2D and UI)** | — |
| Sprite Mode | Single / Multiple (sheets) | — |
| **Pixels Per Unit** | **32** | The scale standard |
| **Filter Mode** | **Point (no filter)** | Crisp pixels, no blur |
| **Compression** | **None** | Preserve exact pixels/colour |
| Mip Maps | **Off** | 2D, no minification blur |
| Wrap Mode | Clamp (Repeat for tiling water) | — |
| Max Size | per-asset, ≥ sprite | Don't downscale hero sprites |
| Pivot | consistent (feet for chars, hull center/stern for boats) | Stable placement |
| **Pixel Perfect Camera** (URP) | **On**, base res set, snap on | No sub-pixel shimmer (§3.7) |

Also: set the project **Sprite Atlas** to pack per-environment/character atlases (Point filter, no
compression) to cut draw calls without sacrificing crispness.

### 9.3 Repo hygiene — adding assets without bloat (Git LFS)

Per `../project-structure.md`:

- **All binary art/audio go through Git LFS** — `*.png`, `*.aseprite`, `*.wav`, `*.ogg`, `*.mp3`,
  plus other large binaries. Confirm/extend the `.gitattributes` LFS patterns there before adding a
  new asset type.
- **Source vs imported:** keep editable sources (`.aseprite`, audio project files) in the
  designated source area (per `../project-structure.md`); export game-ready `.png`/`.ogg` into the
  Unity `Assets/` tree. Don't commit both giant uncompressed WAVs *and* shipped OGGs to the runtime
  folders unless intended.
- **Atlas, don't sprawl:** prefer packed atlases over thousands of loose sprites; reuse modular
  parts (tiles, crew, decor overlays) instead of bespoke megasprites.
- **Big-hull caution:** T5–T7 sprites are huge (§3.3) — section them, atlas them, and keep an eye on
  texture memory on mobile. Profile.
- **Art agents:** when adding assets, (1) author to the master palette + PPU=32, (2) use correct
  Unity import settings (§9.2), (3) place files per `../project-structure.md`, (4) ensure the type
  is LFS-tracked, (5) prefer extending an atlas over a new loose texture.

---

## 10. Open questions

1. **Boat rotation method per tier.** Pre-rendered rotation frames (crisp, heavy) vs engine-rotated
   single sprite (cheap, shimmer)? Recommendation in §3.5 is split by size — confirm and lock per
   tier, since it drives atlas budgets.
2. **Water: shader vs overlay, and how far to push it.** How much of the water look is a custom URP
   shader vs tilemap animation + overlays, given the mobile GPU budget? Needs a prototype + profile.
   The **M3 3D-water→2D bake** (§6.1) is the far end of this spectrum — decide bake-to-frames vs a
   runtime shader with the same profiled spike.
3. **UI rendering resolution.** Render UI at world-pixel scale (maximally consistent) or at a higher
   effective res for text crispness on dense screens? Tie to the UI Toolkit vs uGUI decision in
   `ux-and-mobile-controls.md`.
4. **Exact master palette tuning.** §4.1 hex values are the locked *intent* but want a pass in
   Aseprite for ramp smoothness and colourblind separation (coordinate with the UX accessibility
   section — tide/wind colour coding must stay distinguishable).
5. **Heroic human proportion — how heroic?** ~64 px is the proposal; validate against a 32-px-wide
   footprint and against the smallest target phone so faces/poses still read. Lock the final body
   ratio.
6. **Night/fog readability vs mood.** How dark/foggy can we go before mobile readability suffers? Set
   minimum value/contrast floors (and the reduce-motion / high-contrast accessibility options).
7. **Animation budget.** How many work-verb animations per character archetype for the first
   shippable slice vs full game? Sequence with `../roadmap.md`.
8. **Diegetic UI vs speed.** Where does the weathered-diegetic frame slow down a quick mobile
   interaction (e.g. fast market trading)? Define which screens lean fully diegetic vs which go
   cleaner-functional, with the UX doc.
