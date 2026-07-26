# Hidden Harbours — Sprite colour-light response (captured target)

> **Status:** Design module — a **captured look-target + build-later recipe**. **Docs only: no code,
> shader, scene, material, or Core change ships here.** One of three captured
> [advanced-rendering targets](advanced-rendering-targets.md); read that index first for the shared rules
> (technique-not-palette, pixel-art discipline, determinism, perf budget, phase). Subordinate to
> [`../vision-and-pillars.md`](../vision-and-pillars.md) (CANON) and
> [`art-and-audio-bible.md`](art-and-audio-bible.md).
>
> **Pillars served.** **P1 The Sea Has Moods** (light is a force — dawn, dusk, a lighthouse in fog),
> **P5 Cozy-with-Teeth** (a genuinely dark night navigated by your own light), **P3** (a town glowing
> warm at dusk).
>
> **Siblings.** [`lighting-and-daynight.md`](lighting-and-daynight.md) (the shipped day/night + lights
> this extends) · [`../adr/0013-dynamic-lighting.md`](../adr/0013-dynamic-lighting.md) (the Sprite-Unlit
> finding + the recorded "path 2" migration) · [`../adr/0016-additive-2d-lights.md`](../adr/0016-additive-2d-lights.md)
> (the custom additive lights + light-into-water-shader).

---

## 1. The reference (credited)

**Owner-shared:** a Unity developer's pixel scene (r/Unity3D; author later ported to URP). A single flat
**tree sprite** shown under different **coloured point lights** — moody blue, neutral, **neon magenta**,
**fiery orange** — where the tree's own surface **glows and rims with the light's colour** on its lit
side. From the author's replies, the trick is **two hand-painted masks** (a *front-light* and a
*back-light* zone) that a light-sprite shader tints by which coloured light is near — *not* normal maps —
plus projected shadow sprites and a dark base. *(The author: it's a 3D scene styled 2.5D, flat billboards,
a fixed low-FOV camera.)* **Not our art** — captured as a *technique target*.

---

## 2. What we take vs what we leave

- **Take — the mechanism:** flat **hero sprites that catch and rim with the colour of nearby local
  lights**, giving directional, coloured mood lighting.
- **Leave — the intensity/palette:** the reference is a **neon fantasy** (magenta/electric). Ours is the
  restrained coast, where **one warm light in the cold** is the image (art bible §4.2). Same machinery,
  dialled to the master ramp.

---

## 3. We already have ~80% of this

The moody-coloured-light *half* of the reference is **shipped** (`lighting-and-daynight.md`):

| Reference piece | Hidden Harbours today |
|---|---|
| A dark, moody base | **Deterministic day/night** — genuinely dark nights (ADR 0013). |
| Coloured point lights | **`SceneLight`** (cone/radial, **any colour**, **night-gated in-shader**) + the **`BoatSpotlight`** (ADR 0016). |
| Shadows grounding the objects | **Projected sprite shadows** that swing/lengthen with the sun (ADR 0013 PR2). |
| Light bleeding onto surfaces | The boat light **already bleeds its colour into the water shader** (ADR 0016 follow-up 2). |

So *"coloured light + shadows + navigable dark night"* — the whole mood — is already in the game; the
owner can see it via **`Hidden Harbours ▸ Build Light Test`** (scrub to night).

---

## 4. The gap = the actual "wow" (and the two ways to close it)

Our lights brighten a **region** of the frame (a glow/halo/pool). What the reference does that we do
**not** yet is make **the sprite's own surface glow with the light** — the magenta **rimming the tree's
form**. That is missing because — by a deliberate ADR 0013 decision — our sprites are **Sprite-Unlit**
(they sample no light, so we never had to migrate the whole sprite library). Two **documented URP paths**
close the gap; both are proven possible (the reference author's own URP port is the existence proof):

| Path | What it is | Pros | Cons |
|---|---|---|---|
| **(a) Hand-painted light masks + a sprite-lit shader** *(the reference's approach)* | Author a **front/back light mask** per hero sprite; a small shader tints those zones by nearby coloured lights | **Pixel-art-friendly** (hand masks avoid the "too-smooth/rounded" look normal maps give pixel art); art keeps full control; **no library-wide migration** — opt in per hero sprite | A **new shader** + a **mask-authoring step** per sprite that opts in |
| **(b) Migrate hero sprites to Sprite-Lit + normal maps + `Light2D`** *(ADR 0013's recorded "path 2")* | Convert the sprites we want lit to URP **Sprite-Lit**, add **normal maps**, drive a **`Light2D`** from the same `DayNightProfile` | **Standard, built-in** URP path; real 2D lighting; the durable day/night model already carries over | Heavier; normal maps can read **too soft** on pixel art; more perf per lit sprite; the migration ADR 0013 deliberately deferred |

**Recommendation to carry into the build:** lean **(a) masks** for the pixel-art fidelity and the
opt-in-per-sprite cost profile, on **hero elements only**; keep **(b)** on the table if a scene needs true
many-light interplay. Neither is built yet — both are the deferred **M2/M3 night-lighting vision**.

### 4.1 ⭐ The mechanism, from the author (owner-relayed, 2026-07-25)

The author described the real implementation, which **closes the "how does a flat sprite know front from
back" gap** the table above left open. Recorded because it is the load-bearing trick:

> **Every light carries a technical sprite: a radial gradient split into two colours — green in the top
> half, blue in the bottom.** An object samples that gradient **at its own position**. Land in the green
> zone → the light is **in front** → the shader lifts the **green channel** of that object's lighting
> texture. Land in the blue zone → the light is **behind** → it lifts the **blue channel**.

So "in front or behind" is a **2D screen-space lookup against the light's own sprite** — no depth buffer,
no normals, no per-object geometry. That is exactly why it survives on flat billboards. The object's
lighting texture is correspondingly a **two-channel mask**: green = the pixels that catch a light in
front of it, blue = the pixels that rim when the light is behind. *(The owner's first reference image
shows both artefacts side by side: the tree's own green/blue lighting texture, and the light's
green-over-blue radial gradient.)*

Three further notes from the author, each a design constraint rather than a detail:

- **Every object gets a fake normal pointing straight up.** Purpose: equalise object colour against
  terrain colour regardless of the directional light's angle.
- **Standard point lights were abandoned deliberately.** There was no way to remove lighting influence
  from specific pixels of a flat sprite while still controlling brightness per pixel for directional,
  point and ambient *separately* — "without rewriting the engine."
- **Water and light both live in the terrain shader.** The motive was puddles: paint small water directly
  on the terrain with no geometry, then *"why not make all water paintable directly on the terrain."*
  **Stated cost: the main object shader must duplicate the terrain shader's calculations** to stay
  visually matched. **Stated limitation: multiple light sources are approximate** — accepted, on the
  grounds that the average player will not notice.

### 4.2 What this means for us — three findings

**1. We already converged on that architecture, independently.** ADR 0016's follow-up already bleeds the
boat light's colour **into the water shader**, and the water shader already carries the painted terrain
height map (ADR 0010/0012/0023) with the shader owning the moving waterline. *"Light and water are part
of the terrain shader"* is not a new direction for this project — it is a description of where we already
are. That materially de-risks adoption.

**2. The author's stated downside is a cost we have already paid — and already solved.** Duplicated maths
across two shaders is precisely the `ShoreFade01` ↔ `Fade01` situation, and the answer here is the
**HLSL-twin discipline**: a line-for-line transcription plus a **test that pins the shader's exact text
and fails red on drift** (`WhitecapSalienceMath` does the same job for the C# twin). Any adoption must
carry that discipline **from the first commit**, not retrofit it — it is the difference between the
author's honest caveat and a maintainable system.

**3. ⭐ Our masks can be DERIVED, not painted — which retires path (a)'s only real cost.** §4 lists "a
mask-authoring step per sprite" as the con, and OQ 3 asks whether the rig pipeline could generate them.
**ADR 0022 already answers it:** the rigs *are* flat-facet 3D renderers — they compute per-face normals
and shade from a fixed key direction, which is the entire reason boat hulls could become meshes. A
front-catch / back-catch mask is therefore a **bake output of information the rig already holds**, not a
hand-paint job. The trees are rig-generated; so is most of the prop library. That turns "author two masks
per hero sprite" into "extend the baker with two more output channels once."

⚠️ Note also that the fake-upward-normal trick solves a problem **we do not have in that shape**: ADR 0013
put us on **Sprite-Unlit** with a multiply-overlay tint, so our sprites sample no scene light at all. We
start from a simpler position than the reference did, not a harder one.

### 4.3 ⚠️ Sequencing — read before anyone starts

This technique lands **in the terrain/water shader and the object shader**, which is the **same lane as
the in-flight hull-water work**. It must not start until that lands, or two sessions will be editing the
same fragment. The natural order is: hull-water work merges → the mask-bake spike (cheap, art-lane, no
shader edits) → then the shader itself, with the HLSL-twin test written first.

---

## 5. Where it earns its keep (on-brand uses)

Not neon trees — the restrained, canon-serving cases:

- A **lighthouse beam** raking a wharf and **catching the edge** of buildings/boats as it sweeps (a
  gameplay beacon in The Smother — P1).
- **Warm window-light** spilling onto a character at dusk in Greywick ("the most colourful place" — P3),
  coordinating with the existing `CottageDayNight` pane swap.
- **Deck lamps / running lights** picking out a hull on dark water; a **red buoy** glowing in fog.
- **Storm lightning** as a brief coloured **rim-flash** (already atmosphere-only in the art bible).

---

## 6. Determinism · performance · seams

- **Deterministic (rule 5).** Lighting is visual-only — a pure function of `(tunables, published tint/sun,
  time)`; the existing lights already gate off the deterministic `_DayNightTint`, drive no sim, save
  nothing. Any flicker is a `(seed, time)` hash (ADR 0016).
- **Perf (rule 7) — the real constraint.** Sprite-Lit + `Light2D` (path b) adds cost **per lit sprite**;
  the art bible is explicit: *"prefer one global grade + a few local lights… profile."* So this is
  **hero-sprite-only**, never the whole world. Masks (path a) keep the cost opt-in. A profiled spike sets
  the budget (extends the lighting OQ).
- **Seams (rule 4).** Stays in the **Art** lane, reading the published day/night + light globals; no
  feature-module coupling (the `BoatSpotlight` already reads the boat through `transform` only).

---

## 7. Phase

**M2/M3 night-lighting vision**, not M1. M1 already ships the foundation (day/night grade, boat spotlight,
projected shadows). The per-sprite rim response is the polish that lands with the M2 regions / M3 advanced
rendering — applied to a **handful of hero sprites**, owner-steered.

---

## 8. Open questions (for the build)

1. **Masks (a) vs Sprite-Lit (b)** — lock the approach with a one-scene spike comparing pixel fidelity +
   cost on a hero sprite (a tree, a cottage, a hull).
2. **Which sprites opt in** — the hero set (player, active boat, lighthouse, key buildings/props) vs the
   long tail (which stays unlit under the global grade). Keep the list small for perf.
3. ~~**Mask authoring cost**~~ — ✅ **largely answered (§4.2 finding 3): the rig baker can DERIVE both
   masks**, because the rigs already carry per-face normals and a fixed key direction (ADR 0022). The
   residual question is narrower and worth a spike: *does a derived front/back split read as well as a
   hand-painted one on a 5.5 m tree*, and what happens to the sprites that have **no** rig (imported
   sheets, hand-drawn one-offs) — those still need hand masks or an opt-out.
4. **Palette discipline** — the reserved-pop rule for glowing light so night reads *warm-light-in-cold*,
   not a rave (art bible §4.2).
</content>
