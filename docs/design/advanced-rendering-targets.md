# Hidden Harbours — Advanced rendering targets (the captured look-set)

> **Status:** Design **index** — a coherent set of owner-shared **visual references**, captured as
> *technique targets* for the **M2/M3** advanced-rendering pass and mapped onto systems the project
> already has. **Docs only: no code, shader, scene, material, or Core change ships from this set.** It
> exists so a future art pass can build these without re-deriving, and so the owner can steer the look in
> one place. Subordinate to [`../vision-and-pillars.md`](../vision-and-pillars.md) (CANON), then
> [`art-and-audio-bible.md`](art-and-audio-bible.md) — whose **§6 (Lighting, day-night & fog)** and
> **§6.1 (advanced-rendering roadmap)** are the canon home this set elaborates.
>
> **Why this exists.** The owner has been collecting striking rendering references (external Unity work)
> and asking *"can we do this, and should we?"* The answer is nuanced and repeats across all three, so it
> is written **once** here as shared rules (§2) and applied per-target (§4). Each target has its own
> detail doc with the full mapping + build recipe.

---

## 1. The three targets

| Target | Reference (external, credited) | In one line | Detail doc |
|---|---|---|---|
| **A. Organic seabed / terrain** | A URP-ported "HDRP terrain shader" — sand shoals + scattered boulders under blue water | Make the seabed read **organic and procedural**, not gridded | [`seabed-and-terrain-look.md`](seabed-and-terrain-look.md) |
| **B. Sprite colour-lighting** | A pixel tree that **glows with the colour of nearby lights** (hand-painted front/back light masks) | Make hero sprites **catch and rim with local coloured light** | [`sprite-light-response.md`](sprite-light-response.md) |
| **C. Weather fog / sky / rain** | A screen-space fog that takes its colour from the sky, clouds, and lights | A unifying **fog layer** that reads the sun, the clouds, and nearby lights | [`weather-rendering.md`](weather-rendering.md) |

All three come from the **same developer's ecosystem** (a fixed-camera, custom-shader-light,
pixel-styled Unity look) — which is *why they map onto us so well* (see §3).

---

## 2. The shared rules every target obeys (the throughline)

These are the same five constraints, repeated so no target has to re-argue them:

1. **Technique, not palette.** Every reference is more **saturated / neon** than Hidden Harbours will
   ever be. We take the *mechanism* and dial it to the **locked 22-colour master ramp** (art bible §4.1),
   where **saturated pops are precious** — *"a foggy grey morning with one red buoy glowing is the
   signature image."* A glowing magenta willow is a different game; a warm lit window in grey fog is ours.
2. **Pixel-art discipline (LOCKED).** PPU=32, Point filter, one perspective (¾ top-down), one scale.
   Anything shader-driven must **pixel-snap** so it reads as pixel art, not a soft gradient — exactly how
   the shipped water surface noise already behaves (ADR 0012 note). No sub-pixel shimmer, no off-ramp
   colour.
3. **Deterministic (rule 5).** Everything is a pure function of `(worldSeed, gameTime)` + authored data;
   any smoothing is presentation-only; **nothing is saved**. Randomness is a `(seed, time/cell)` hash,
   never `System.Random` (the pattern `RainEmitter` and the clam-hole scatter already use).
4. **Perf is a feature (rule 7).** 60fps desktop baseline, mobile-portable (ADR 0005). The reference
   author openly *"only recently started optimising."* Each target's cost is a **profiled spike** against
   the budget, cheapest-path-first — not a blank cheque.
5. **In milestone order (rule 8).** **None of this is M1.** M1 ships the day/night grade + water mood +
   rain/mist. These land in the **M2 weather/danger wave** and the **M3 advanced-rendering pass**. This
   set is the *capture*; the build is owner-steered, later.

---

## 3. Why the whole set ports cleanly (the shared reason)

The reference look depends on **a fixed, non-rotatable camera** and **custom shader-based lights (not
Unity's built-in 2D lights)**. Most projects treat those as constraints to fight. **Hidden Harbours
already made both choices:**

- Our camera is a **fixed ¾ top-down orthographic** (art bible §2, LOCKED).
- We deliberately use **custom additive-glow lights** (`SceneLight`/`BoatSpotlight`), *not* URP `Light2D`,
  and the water shader already reads light as **published globals** (ADR 0016).

So the fiddly foundation is free, and — crucially — **most of each target is already shipped or
roadmapped.** The genuinely *new* work is small and named per target (§4).

---

## 4. The targets, each in brief (detail in the linked docs)

### A. Organic seabed / terrain → [`seabed-and-terrain-look.md`](seabed-and-terrain-look.md)
- **Already have:** the **depth-tinted water** (bluer deeper) is the reference's "atmosphere"; the
  **Sunkers / Drownded Lands** are canon seabed regions; the **shallow-water preview** ("see the bar, the
  sunker, the clam bed") is a roadmapped M3 item (art bible §6.1). The seabed **height map** is authored
  and read by both render and sim (ADR 0014).
- **The new piece:** organic sand/rock distribution via **noise-driven scatter of pixel-art rock/kelp/
  pebble sprites + varied autotiles** (mostly art + a light procedural-placement pass), optionally a
  **pixelized** procedural detail shader for mottled sand — *not* the soft HDRP shader.
- **Phase:** M2 (Sunkers/flats art passes) → M3 (shallow-water preview).

### B. Sprite colour-lighting → [`sprite-light-response.md`](sprite-light-response.md)
- **Already have:** deterministic **day/night**, **coloured additive lights** (cone/radial, night-gated),
  the **boat spotlight**, **projected shadows**, and **light bleeding into the water shader**
  (ADR 0013/0016). The whole *moody-coloured-light* half of the reference is built.
- **The gap = the actual "wow":** our sprites are **Sprite-Unlit**, so lights brighten a *region* but
  don't **rim the sprite's own form**. Two documented URP paths close it: **(a)** hand-painted light
  masks + a sprite-lit shader (the reference's approach; pixel-art-friendly), or **(b)** migrate hero
  sprites to **Sprite-Lit + normal maps + `Light2D`** (ADR 0013's recorded "path 2").
- **Phase:** M2/M3 night-lighting vision; applied to **hero elements only** (lighthouse rake, window
  spill, buoy glow, storm flash), never every sprite (perf).

### C. Weather fog / sky / rain → [`weather-rendering.md`](weather-rendering.md)
- **Already have:** **rain + sea-mist** emitters, **weather-driven water mood** (ADR 0017), the **sun
  globals**, and **deterministic `fogDensity`/`visibility`** state. ~5 of 7 pieces exist or are
  roadmapped.
- **The new piece:** one unifying **screen-space, parallax-graded "SkyFog"** layer that takes its colour
  from the sun (sky), the clouds, and nearby lights — restrained to the palette. **The Smother** (canon
  permanent fog) is its home.
- **Phase:** M2 (fog-as-hazard) → M3 (clouds, many-lights-in-fog).

---

## 5. How the three compose (they are not independent)

A quiet strength of doing all three *our* way: they **share the same seams**, so they reinforce instead
of fighting —

- **B and C share the lights.** The coloured lights that rim a sprite (B) are the same published light
  globals that bleed into the fog (C). One lighthouse beam rakes the wharf **and** glows in the fog **and**
  catches the boat's edge — one light, three payoffs.
- **A and C share the depth truth.** The seabed height map that shades the shallow-water preview (A) is
  the same height the water depth-tint and the fog's aerial-perspective grade (C) read — one height, many
  consumers (the ADR 0009/0010/0014 invariant).
- **All three share the day/night sun.** `_SunDir`/`_SunElevation` position the fog's sky disc (C), swing
  the projected shadows that ground the sprites (B), and warm the seabed shallows (A).

So the *coherent-set* framing is not just organisational — building them on the shared globals is what
keeps the game reading as **one world** (art bible §2.1), not three bolted-on effects.

---

## 6. Not in scope for this set

Per CLAUDE.md rule 8 and the "docs only" status: **no shader, material, scene, component, Core surface,
or save change ships from these docs.** They are the map and the recipes; each names its own perf/
determinism invariants and open questions for the build. When the owner greenlights a target's build, the
cross-cutting render decision gets its own **ADR** (as the water/lighting seams did — 0010/0013/0016/0017),
owned by lead-architect (the "Water/fog/lighting" seam) with art-pipeline on the look. Built in milestone
order, each only when its milestone is reached.

---

## 7. Cross-links

- Canon: [`../vision-and-pillars.md`](../vision-and-pillars.md) §5.2 (perspective/scale, LOCKED),
  §5.7 (procedural vs handcrafted).
- Art canon: [`art-and-audio-bible.md`](art-and-audio-bible.md) §2 (perspective), §4 (palette), §6/§6.1
  (lighting/fog + the advanced-rendering roadmap).
- Sibling detail docs: [`seabed-and-terrain-look.md`](seabed-and-terrain-look.md) ·
  [`sprite-light-response.md`](sprite-light-response.md) · [`weather-rendering.md`](weather-rendering.md).
- Adjacent shipped-system docs: [`water-rendering.md`](water-rendering.md),
  [`lighting-and-daynight.md`](lighting-and-daynight.md), [`ambient-particles.md`](ambient-particles.md),
  [`time-tides-weather.md`](time-tides-weather.md).
- Decisions: ADRs [`../adr/0009-tidal-exposure-and-region-display-name-seams.md`](../adr/0009-tidal-exposure-and-region-display-name-seams.md),
  [`../adr/0010-water-rendering.md`](../adr/0010-water-rendering.md),
  [`../adr/0013-dynamic-lighting.md`](../adr/0013-dynamic-lighting.md),
  [`../adr/0014-painted-seabed-height-authoring.md`](../adr/0014-painted-seabed-height-authoring.md),
  [`../adr/0016-additive-2d-lights.md`](../adr/0016-additive-2d-lights.md),
  [`../adr/0017-weather-driven-water-palette.md`](../adr/0017-weather-driven-water-palette.md).
</content>
