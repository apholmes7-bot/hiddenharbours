# Hidden Harbours — Organic seabed / terrain look (captured target)

> **Status:** Design module — a **captured look-target + build-later recipe**. **Docs only: no code,
> shader, scene, or Core change ships here.** One of three captured
> [advanced-rendering targets](advanced-rendering-targets.md); read that index first for the shared rules
> (technique-not-palette, pixel-art discipline, determinism, perf budget, phase). Subordinate to
> [`../vision-and-pillars.md`](../vision-and-pillars.md) (CANON) and
> [`art-and-audio-bible.md`](art-and-audio-bible.md).
>
> **Pillars served.** **P1 The Sea Has Moods** (a seabed you can *read* through shallow water),
> **P5 Cozy-with-Teeth** (see the sunker before you strike it), **P3** (a coast that feels like a real
> place, not a grid).
>
> **Siblings.** [`water-rendering.md`](water-rendering.md) (the depth-tint + the M3 shallow-water preview
> this leans on) · [`../adr/0014-painted-seabed-height-authoring.md`](../adr/0014-painted-seabed-height-authoring.md)
> (the authored height map render + sim share) · [`../adr/0002-procedural-vs-handcrafted.md`](../adr/0002-procedural-vs-handcrafted.md)
> (authored identity vs procedural fill) · art bible §2.2 (reading the seabed at low tide), §5 (tiles).

---

## 1. The reference (credited)

**Owner-shared:** a Unity developer's terrain demo (r/Unity3D, *"HDRP custom terrain shader"*; author
notes it was later ported to URP). *"It's all texture-based… no geometry, no normal maps,"* plus
volumetric fog and post-effects. Visually: a **tan sand shoal** surrounded by **drifts of rounded
boulders**, all under a **blue water tint that deepens outward**, with soft light dappling the sand — the
rocks clustering in **organic rings and drifts**, the sand with **soft irregular edges**. **Not our art**
— captured as a *technique target*.

The striking quality the owner named: *"very organic textures and the procedural effect is very cool."*

---

## 2. What we take vs what we leave

- **Take — the spirit:** (a) an **organic, procedural distribution** of sand vs rock that reads natural,
  not gridded; (b) **depth atmosphere** (bluer/darker deeper); (c) the **subject** — a seabed shelving
  under shallow water, which is *exactly* the Sunkers / Drownded Lands / clam-bed content the game already
  wants.
- **Leave — the render style:** it is **HDRP** (we are 2D URP — a different, locked pipeline; not a shader
  we can lift), and its **soft, painterly, volumetric-fog** finish is the *opposite* of our crisp,
  limited-palette pixel art. Reproduced literally it would look alien beside the pixel dory and wharf. So
  we chase the *feeling*, rendered our way.

---

## 3. The mapping — reference → our stack (HAVE vs NEW)

| Reference technique | Hidden Harbours today | Gap / new work |
|---|---|---|
| Depth atmosphere (bluer/darker deeper) | The **water shader already depth-tints** off `depth = WaterLevel − seabedHeight` (ADR 0010/0012) | **Have.** The "underwater blue" is already ours. |
| Subject: seabed under shallow water | **Sunkers** (submerged rocks) + **Drownded Lands** (walkable flats) are **canon regions**; the **shallow-water preview** (see the bed shelving away) is a roadmapped **M3** item (art bible §6.1) | **Have the plan.** The view is already wanted. |
| Organic sand/rock distribution (clusters, rings) | Tilemaps + Rule-Tiles today read *placed*, not *scattered* | **New:** noise-driven **procedural scatter** of pixel-art rock/kelp/pebble sprites + varied autotiles over authored sand — the "organic" win. Mostly **art + a light placement pass**, not a fancy shader. |
| Procedural surface texture (mottled sand, speckle) | Flat tile fills | **New (optional):** a **pixelized** URP detail shader adding palette-clamped mottle/speckle to sand — snapped to the pixel grid so it stays pixel art (like the water noise). |
| "All texture-based, no geometry" | Our sprites/tiles are already flat (2D) | **Have (differently).** We get the "no geometry" benefit natively; we do **not** want the soft lighting that came with it. |

**Net: the depth + subject are ours already; the new work is "organic procedural scatter," which is
largely an art job with a small procedural-placement helper.**

---

## 4. Build-later approach (the recipe)

The organic look, our way, in three layers over the **authored** seabed height map (ADR 0014 — the owner
paints the coast; render and sim read the same map):

1. **Authored macro-layout (unchanged).** Where the shoal, the channel, the reef sit is **hand-authored**
   (ADR 0002: identity is authored, variety is procedural). The Sunkers' hazard rocks that gate a crossing
   are *placed*, not random — the tide-gameplay reads them.
2. **Procedural scatter fill (the new bit).** Over the authored ground, scatter **pixel-art rock / kelp /
   pebble / shell** sprites with a **deterministic `(seed, cell)` hash** (the exact pattern the clam-hole
   scatter already uses, `time-tides-weather.md` §3.5) — density and clustering driven by value-noise so
   they gather in natural drifts and rings, thinning into the sand. Keep only positions on appropriate
   ground (rock on reef, kelp on shallow rock, shell on sand). Pixel-snapped, on-palette, pooled.
3. **Optional pixelized detail shader.** If flat sand still reads too plain, add a **pixel-snapped,
   palette-clamped** mottle/speckle in a URP shader over the sand band — the disciplined cousin of the
   reference's procedural texture, never a soft gradient.

The **depth-tinted water** (already shipped) supplies the "underwater" atmosphere on top; the **M3
shallow-water preview** (`water-rendering.md`) is what lets the player *see* this bed through the surface —
the reference's actual subject.

---

## 5. Determinism · performance · seams

- **Deterministic (rule 5).** The scatter is a pure `(seed, cell)` hash — **no `System.Random`**, nothing
  saved; a rebuild reproduces the same bed (like the clam holes). The authored height map is read-only
  data.
- **One height, many consumers.** The bed the player *sees* and the bed the boat *grounds on* / the player
  *walks* are the **same authored height map** (ADR 0009/0010/0014) — a decorative scatter never changes
  `depth`/walkability. P1 integrity holds by construction.
- **Perf (rule 7).** Scatter sprites are **pooled, batched** (shared atlas), built once per region — no
  per-frame cost; the optional detail shader is a few pixel-snapped taps. Mobile-portable. **Author where
  identity lives, scatter where variety lives** keeps the count sane (ADR 0002).

---

## 6. Palette & phase

- **Palette:** the master ramp's **wet-sand/mud, kelp-green, driftwood, slate** families (art bible §4.1)
  — cold North Atlantic seabed, not tropical blue-green. Pops (a lone orange starfish, a red-weed patch)
  stay precious.
- **Phase:** **M2** with the Sunkers / Drownded Lands art passes (the scatter + tiles); the **shallow-water
  preview** that shows the bed through the surface is **M3** (art bible §6.1). Not M1.

---

## 7. Open questions (for the M2/M3 art pass)

1. **Scatter density/clustering tuning** — value-noise thresholds per region (dense reef vs sparse flat);
   an editor preview like the terrain paint tool (ADR 0014).
2. **Sprite vs shader for the rock field** — pooled scatter sprites (crisp, art-controlled) vs a procedural
   pebble shader (cheaper at scale, harder to keep on-palette). Likely sprites for hero rocks + a shader
   speckle for fine detail; profile.
3. **How much shows through the surface** — the shallow-water preview's readability vs the water's own
   reflections/foam; a `water-rendering.md` OQ.
4. **Reuse across regions** — one modular rock/kelp/shell atlas scattered with per-region palette grades,
   vs bespoke per region (art-volume call; grow region-by-region, never front-loaded — art bible §8).
</content>
