# ADR 0016 — Additive 2D lights that cut through the dark night: an additive glow drawn ABOVE the day/night overlay, night-gated in-shader — and the first concrete one, a boat spotlight

- **Status:** **Accepted** — art-pipeline + lead-architect. This ADR ships **code** (a reusable additive-light
  shader + material, a drop-on `SceneLight` component, the first concrete light — a `BoatSpotlight` cone — an
  "Add Light" editor menu + a "Build Light Test" demo, and EditMode tests for the pure light maths). The other
  light TYPES (worklight / window glow / lightpost) are **follow-ups**: structured for (menu entries + a
  `LightPreset` enum + a generic radial stub) but **not** built as bespoke components here.
- **Date:** 2026-06-28
- **Decision owner:** lead-architect (a new cross-cutting render seam that composites ABOVE the day/night
  overlay and reads its published `_DayNightTint` global; the same "Water/fog/lighting" seam as ADR 0013).
  art-pipeline owns the *look* (the beam softness, the warm palette, the demo).
- **Serves:** **P1 "The Sea Has Moods"** (the genuinely-dark night of ADR 0013 becomes a thing you *navigate by
  your own light*) and **P5 "Cozy but with Teeth"** (night-sailing lit only by your beam is the foundation of
  night-sailing risk). This is the **payoff of the day/night system** — the owner's M2/M3 night-lighting vision
  (genuinely dark nights navigated by **boat lights**), started now with the boat spotlight.
- **Related:** `0013-dynamic-lighting.md` (the whole-frame MULTIPLY darkening overlay this draws ABOVE — its
  "migration path" note (1) named exactly this: *lights render above the overlay as additive sprites*),
  `0010-water-rendering.md` (the self-lit water this beam falls on; the unset-tint fallback idiom is borrowed
  from its shader), `0004-perspective-and-scene-strategy.md` (¾ top-down, scene-per-region — why a light must be
  a drop-on, not scene-wired), `0005-pc-first-target.md` (the 60fps budget, mobile-portable),
  `design/lighting-and-daynight.md` §6 (the "future night lights" note this realises), the owner's
  **night-lighting vision**.
- **Implementation (this PR):**
  `Assets/_Project/Code/Art/LightMath.cs` (the pure night-gate / cone-radial / flicker maths),
  `Assets/_Project/Code/Art/SceneLight.cs` (the reusable drop-on additive light),
  `Assets/_Project/Code/Art/BoatSpotlight.cs` (the first concrete light — a bow cone),
  `Assets/_Project/Art/Shaders/HiddenHarboursAdditiveLight.shader` (+ `Assets/_Project/Resources/AdditiveLight.mat`),
  `Assets/_Project/Art/Editor/LightMenu.cs` ("Add Light to Selection" + "Build Light Test"),
  `Assets/Tests/EditMode/Art/LightMathTests.cs`.
  **Follow-up fix 2 (the beam lit land but not the WATER — lit in-shader via globals, see the section below):**
  `Assets/_Project/Code/Art/BoatSpotlight.cs` (publishes the `_BoatLight*` globals),
  `Assets/_Project/Art/Shaders/HiddenHarboursWater.shader` (the `BoatLightTerm` frag term),
  `Assets/_Project/Code/Art/LightMath.cs` (the pure water-cone twins) + its tests.

## Context

ADR 0013 made the night **genuinely dark** by multiplying the whole composited frame by a dark `_DayNightTint`
(the only way to darken the project's **Sprite-UNLIT** sprites without migrating every material to Sprite-Lit).
It explicitly recorded the trade-off: *a multiply overlay is a global darkener — it cannot, by itself, let a
light source punch a bright hole in the dark.* It also recorded the chosen migration path for when boat-lights
arrive: **(1) lights render ABOVE the overlay as additive sprites** (cheap, stylized, pixel-art friendly), or
(2) migrate to URP 2D lights. This ADR builds path (1).

The hard question: **how does a 2D "light" brighten a frame whose darkness is a full-screen multiply overlay at
sortingOrder ~32760, over unlit sprites that sample no 2D light?**

| Approach | Brightens the darkened frame? | Works with unlit sprites? | Cost / risk |
|---|---|---|---|
| **(a) URP `Light2D` + Global Light 2D** | No — the sprites are Sprite-Unlit and sample no 2D light (the exact ADR-0013 finding) | No (needs a project-wide Sprite-Lit migration) | High; un-validatable headless |
| **(b) Additive glow drawn ABOVE the overlay (Blend One One)** | **Yes** — it ADDS brightness back on top of the multiply | **Yes** — renderer-agnostic; it draws over the composited frame | **Low — one quad, one shader; consistent by construction** |
| **(c) A second "un-darken" overlay** | Partially | Yes | Can't be localized to a cone/lantern; not a light |

## Decision

**A light here is an ADDITIVE glow quad drawn ABOVE the day/night MULTIPLY overlay (approach (b)).** It uses
`Blend One One` (premultiplied additive), so it ADDS its colour into the crushed-dark frame — a lantern/beam
**punching a bright hole in the dark**. It sorts at `sortingOrder ~32770` (> the overlay's ~32760, < the
screen-space HUD), so it brightens the darkened world but never the HUD. The component
(`SceneLight`) only positions/orients/colours/sizes the quad and pushes the tunables; the shader
(`HiddenHarbours/AdditiveLight`) does the soft cone/radial shape.

**The night-gate is IN-SHADER (zero per-light C# coupling to the cycle).** The shader reads the published
`_DayNightTint` (the same global ADR 0013 sets), computes the frame **darkness** `≈ 1 − luminance(tint)`, and
scales its additive output by a smooth ramp on that darkness — so a light is **~invisible at a bright noon** (it
can't wash daytime out) and **full in a dark night**. No light component ever reads the clock or the cycle: drop
a `SceneLight` anywhere and it auto-fades with the day. The ramp is the pure, unit-tested `LightMath.NightGate`
mirrored exactly in HLSL.

**Cycle-off / edit-mode fallback (mirrors the water shader's unset-tint handling).** When the cycle isn't
running (EditMode, a bare art scene, the demo before Play) the `_DayNightTint` global is unset/near-black. A
naive gate would read that as "deep night → full light" — which is actually what we WANT here (the beam shows
for tuning + the preview). The shader makes this explicit and **tunable**: when the tint is near-black
("no cycle") it returns `_GateFallback` (default 1 = show), exactly how the water shader defaults an unset tint.

**Visual-only (rule 5).** A light drives no simulation, is recomputed every frame, and **saves nothing**. The
optional flicker is a **deterministic hash** of `(seed, time)` — never `System.Random`. (Walkability,
boat-crossing, etc. continue to read the seabed/tide, not the light.)

Everything is **tunable** (rule 6 — colour, intensity, cone half-angle, range, edge/angular softness, the
night-gate curve, flicker amount/speed, all serialized on the component / material), **drop-on + self-contained**
(no scene wiring; mirrors `SpriteShadow` / `CottageDayNight`), **pooled with no per-frame allocation** (rule 7 —
one shared mesh, one shared material via a `MaterialPropertyBlock`, the heavy shape on the GPU), and
**Core/public-surface only** (rule 4 — see the boat spotlight below).

### The shape (cone + radial), concretely

The shader works in the quad's normalized space (`q ∈ [-1,1]²`). A `_LampPos` uniform places the lamp (the
component sets it: bottom-centre `(0,−1)` for a cone so the beam throws "up"/forward; centre `(0,0)` for a round
radial). The glow is `RadialFalloff(distance) × ConeFalloff(angle off the +y axis)`:

- a **cone** (small half-angle) is a directional **beam** (the boat spotlight, a torch);
- a **half-angle of 180°** disables the angular cut → a full **radial** round glow (a lantern, a worklight, a
  window spill, a lightpost);
- `edge softness` softens the radial fade (hard disc ↔ soft halo); `angular softness` feathers the cone edge.

All three falloffs are pure functions in `LightMath` (`RadialFalloff` / `ConeFalloff` / `ShapeIntensity`),
unit-tested headless and mirrored in the HLSL.

### The first concrete light — the BOAT SPOTLIGHT

`BoatSpotlight` is a drop-on that owns + configures a `SceneLight` **cone**: warm, soft, thrown forward off the
**bow** onto the dark water, that **follows + rotates with the hull** and **dims toward off when not making way**
(moored/aground/drifting reads as a working searchlight only under way; a small floor keeps a faint moored
glow).

- **It reads the boat through Transform only (rule 4).** The component lives in the **Art** lane, which does
  **not** reference the Boats module. It is attached to the boat GameObject, so the boat's HEADING is its own
  `transform.up` (the bow — the same convention `BoatController` and the wake use) and the bow ANCHOR is a local
  offset forward along it. **No cross-module reference at all.** The optional way-gate measures the carrier's OWN
  speed frame-to-frame (so it works on the player boat AND NPC boats), again dependency-free. (When the Core
  boat-kinematics seam — `IActiveBoatService` — is later wired into the active boat, the spotlight can opt into
  it; the transform-speed read is sufficient and zero-coupling today.)

## What this PR ships

1. **`HiddenHarbours/AdditiveLight` shader (+ `Resources/AdditiveLight.mat`).** A soft additive cone/radial
   glow, `Blend One One`, drawn above the overlay, night-gated in-shader off `_DayNightTint` with the
   cycle-off fallback. Pixel-art friendly (procedural falloff, no texture, crisp at any zoom). No `+`/operator
   char in any `[Header]`, no `[unroll]` over a runtime loop, every symbol defined before use. The shipped
   material means the existing **magenta shader-compile guard** (`WaterShaderCompileGuardTests`, which
   force-compiles every project material) covers it — a broken light shader fails CI **red**, not magenta-in-build.
2. **`SceneLight`** — the reusable drop-on additive light: shape (Cone/Radial), colour, intensity, range, cone
   half-angle, edge/angular softness, the night-gate (driven in-shader), optional deterministic flicker, all
   pooled (one child quad + one shared material via an MPB), no per-frame alloc.
3. **`BoatSpotlight`** — the first concrete light (above).
4. **`LightMenu`** — `Hidden Harbours ▸ Lighting ▸ Add Light to Selection ▸ {Spotlight | Worklight* | Window
   Glow* | Lightpost*}` (the starred three are radial **stubs** — structured for, not bespoke yet) and `Hidden
   Harbours ▸ Dev ▸ Build Light Test` (a dark ground plane + a boat-spotlight cone + a radial lantern; press Play,
   scrub to night, watch the beam cut through).
5. **`LightMathTests`** — the pure maths: the night-gate ramp (invisible day → full night, monotonic, the
   threshold/fallback), the cone/radial falloff + angle math, the flicker determinism + band, and the boat
   way-gate.

## Determinism, performance, seams (the invariants)

- **Deterministic (rule 5).** A light is a pure function of `(its tunables, the published tint, time)`; nothing
  is saved or randomised. The flicker is a `(seed, time)` hash. The pure model is `LightMath`, unit-tested headless.
- **Visual-only.** No light reads or writes the sim. It composites over the rendered frame; it changes no
  walkability, no tide, no crossing.
- **Core / public-surface only (rule 4).** `SceneLight` reads no game state at all. `BoatSpotlight` reads the
  boat through `transform` (heading + speed) — never a Boats concrete type.
- **No magic numbers (rule 6).** Every knob is a serialized field on the component / material.
- **Performance (rule 7).** One quad + one shared material per light (batched via MPB), the shape on the GPU, a
  throttled recompute (flicker/gate values), a per-frame pose-only follow. A handful of lights is cheap;
  mobile-portable.
- **Shader-compile guarded.** As (1): the shipped `AdditiveLight.mat` is force-compiled by the magenta guard.

## Consequences

- **The dark night of ADR 0013 is now navigable by light** — the first real night-lighting payoff, starting
  with the boat spotlight; the owner can SEE + tune a beam cutting the dark via "Build Light Test".
- **A reusable light primitive exists** for the follow-up types (worklight / window glow / lightpost) — they are
  the same `SceneLight` (radial) with different tunables; the menu + `LightPreset` enum already structure them.
- **The day/night model is untouched** — this only *reads* `_DayNightTint`; it does not modify the controller or
  its overlay shader.
- **The migration to true URP 2D lights remains open** (ADR 0013 path (2)) if a future need outgrows additive
  sprites; the durable part (the deterministic day/night model + the published globals) is unchanged.

## Follow-up fix — the beam lit LAND but not the WATER (the mesh-vs-sprite ordering quirk)

**Symptom.** After shipping, the boat spotlight visibly brightened land/sprites at night but had **no visible
effect over the WATER**. By sorting order this is impossible: the additive light quad sorts at ~32770 on the
Default layer, above both the day/night overlay (~32760) and the Sea (sortingOrder −5), all on one camera / one
sorting layer — additive-on-top should brighten the water exactly as it brightens land.

**Root cause (PROVEN).** It is **not** a sort-order/layer mismatch. The light quad is a **`MeshRenderer`**, the
Sea is a **`SpriteRenderer`** — and in the **URP 2D renderer a mesh does NOT reliably sort against sprites by
`sortingOrder` alone**: for a mesh-vs-sprite pair the renderer falls back to **world-space DEPTH** (Unity's own
2D sorting docs: a mesh needs a Sorting Group with "Sort as 2D" to sort like a sprite, otherwise it sorts by
world depth). The light quad sits at the boat's **world depth (z = 0)** — the **same depth as the big Sea
sprite** — so the full-screen Sea sprite **overdraws the light** despite the light's far-higher sorting order.
Land "works" only because those are **small** unlit sprites the cone happened to win the depth tie against; the
full-screen water sprite at the same depth does not. The day/night overlay dodges the same quirk by sitting **at
the camera near plane** (the closest depth), which is why night still darkens the water correctly — the asymmetry
that confirms the diagnosis.

**Fix (light-side only; additive / night-gated / P1-safe are all preserved).** Two complementary, version-robust
changes on the light quad in `SceneLight`, covering both code paths the 2D renderer might take:

1. **A `SortingGroup` with `sortAtRoot` ("Sort as 2D")** on the quad — the Unity-documented way to make a mesh
   participate in 2D sorting like a sprite, so its ~32770 order is honoured against **every** sprite (water
   included). It clears the quad's depth info, which is harmless here (the light is `ZTest Always`, writes no
   depth, and nothing depth-based reads it).
2. **Pin the quad's DEPTH (z) just in front of the active camera** each frame (a new `_cameraDepthOffset`, default
   0.1 m), mirroring how the overlay reliably draws over the water. Under the orthographic 2D camera, moving the
   quad along z **never changes its on-screen x/y or the look** — only the compositing order. The pure depth math
   is `LightMath.CameraDepthZ`, unit-tested headless alongside the rest of the light maths.

The water shader, the day/night controller + overlay, `Water.mat`/presets, the magenta guard, and the sim/depth/
clip (P1) are **untouched** — this only changes how the existing additive quad is composited.

## Follow-up fix 2 — the WATER is lit IN-SHADER via published globals (the quad-sort approach could not composite over the custom water shader)

**Symptom (persisted).** The two quad-sort fixes above (the original PR, and the `SortingGroup` + camera-depth
pin) **did not** make the beam read on the water — confirmed by owner screenshots: the additive light quad lights
**land**, but the **water surface stays dark under the beam**. The URP 2D renderer keeps drawing the water
`SpriteRenderer` **on top of** the additive `MeshRenderer` over water areas, regardless of `sortingOrder` /
`SortingGroup` / camera-depth pinning. A **third** quad-sorting fix was explicitly ruled out — the quad approach
cannot reliably composite an additive mesh over the project's **custom-shader** water in this renderer.

**The robust approach — light the water FROM WITHIN the water shader.** The beam-on-water is no longer a quad at
all: the boat spotlight **publishes its world-space cone as GLOBAL shader uniforms** (exactly like the day/night
`_DayNightTint` / `_SunDir` already are), and the **water fragment shader adds the spotlight's cone illumination
to its own `col.rgb`**. Because the light is computed **inside the water's own rendering**, there is **no sorting
dependency** — it *will* show on the water, and it **composes naturally** with the water's reflections / foam /
palette (it sits after the foam/reflection so the beam reads over them; it originally sat **before** the palette
guard-rail, but that — plus the day/night multiply overlay — crushed the beam at complete dark, so it now sits
**after** the grade, overlay-compensated: see Follow-up fix 3 below). The **existing additive QUAD is kept
unchanged for LAND** (it works there). So the **one** boat light lights **both** surfaces via **two** mechanisms: the quad on
land, the in-shader term on water — both driven by the **same** `BoatSpotlight` tunables.

**Why in-shader, not a third quad fix.** The quad-vs-sprite ordering in the URP 2D renderer is not something the
light side can force against a full-screen **custom-shader** water sprite (sorting order, Sort-as-2D, and
camera-depth pinning were all tried and all failed over water). Adding a colour term **inside** the water shader
sidesteps ordering entirely — the term is part of the very draw that was winning the depth/sort tie, so it
**cannot** be overdrawn by it. It is the same proven idiom the water already uses for the day/night sun
(`_SunDir`), sky reflection, and palette grade: read a published global, modify `col.rgb`.

**Implementation (light-side + the water frag; additive / night-gated / P1-safe all preserved).**

1. **`BoatSpotlight` publishes the beam as globals** (on the existing throttled tick, ~20 Hz, via
   `Shader.SetGlobal*` — no per-frame alloc, rule 7): `_BoatLightPos` (world lamp xy at the bow), `_BoatLightDir`
   (world beam axis = the boat heading `transform.up`), `_BoatLightColor`, `_BoatLightParams`
   (`x` = effective intensity = master × way-gate × **water-strength** × flicker, `y` = range, `z` =
   `cos(halfAngle)`, `w` = `cos(innerAngle)`), `_BoatLightParams2` (`x` = radial edge softness, `y/z/w` =
   night-gate threshold/softness/cycle-off fallback). The cone half-angle is published as a **cosine** so the
   water tests "inside the cone" with a single `dot`, no per-pixel trig. **When no light is active** (off / not
   lighting water / the component disabled) it publishes **intensity 0**, and the water term is skipped — no
   stuck beam. **ONE global light** is enough for now (the boat spotlight is THE night-nav light); the clean
   extension later is to publish **arrays** (`_BoatLightPos[]` … with a `_BoatLightCount`) and loop in the
   shader — the single-light path is a count-1 case of that.

2. **The water frag adds the cone** (`HiddenHarboursWater.shader`, `BoatLightTerm()`): for the pixel's `worldXY`
   (pixel-snapped, so the lit pool reads as **pixel art** like every other layer), compute the cone contribution
   from the globals (vector lamp→pixel, within range, within the cone via the published cosines, **radial ×
   angular** falloff — mirroring `LightMath` / the AdditiveLight shader), scale by the **same night-gate** the
   land cone uses (read from `_DayNightTint`: off by day, full at deep night, off-by-dawn; cycle-off → the
   tunable fallback, the same unset-tint convention the reflection/palette use), and **ADD** it to `col.rgb`.
   **P1: `col.rgb` ONLY** — it never touches `depth` / `clip()` / `_WaterLevel` / the height read / the sim
   (rule 5, deterministic, saves nothing). **Magenta-safe:** no `+`/operator char in any `[Header]`, no
   `[unroll]` over a runtime loop, every symbol defined before use (the day/night luma is **inlined** in
   `BoatLightTerm` because `PaletteLuma` is defined later in the file); the shipped `Water.mat` variant is
   force-compiled by the existing guard, so a broken term fails CI **red**, not magenta-in-build.

3. **Tunables (rule 6).** The water reuses the **same** `BoatSpotlight` tunables (colour / intensity / range /
   cone half-angle / softness), so tuning the spotlight tunes **both** land and water consistently. A water-side
   **strength multiplier** (`BoatSpotlight._waterStrength`, default **1.4**) lets the owner balance how strongly
   the beam reads on water vs land. The effect defaults **ON and strong** (water-strength 1.4 over the
   spotlight's 1.5 master) so a midnight beam is an **obvious raking pool of light** on the dark sea (the prior
   quad defaults read too soft). No new material property was added, so `Water.mat` (and its tuned values /
   presets) are **untouched**.

4. **Tests (EditMode).** The pure cone/gate maths the water term mirrors live in `LightMath`
   (`CosFromHalfAngleDeg`, `ConeFalloffCos`, `WaterConeTerm`) and are unit-tested headless in `LightMathTests`:
   within-cone vs behind/outside, range falloff (monotonic along the axis), dimmer off-axis than on-axis,
   at-the-lamp core, and the night-gate (off by day / full at night). The shader visual is what the owner
   verifies at deep night.

The day/night controller + overlay, the terrain tool, the wake, the weather palette, the `Water.mat` existing
values / presets, and the sim/depth/clip (P1) are **untouched** — this adds a published-global read + a `col.rgb`
term to the water frag, and a globals push on the boat spotlight; the **land quad is unchanged**.

**Owner verification note.** The effect is **night-gated** — it fades toward off near dawn (a daylight beam
would wash the bright water out, which is wrong). Verify at **DEEP night (~midnight)**, driving over open water.

## Follow-up fix 3 — the water beam is added AFTER the palette grade, PRE-COMPENSATED for the day/night multiply (it was crushed at complete dark)

**Symptom (owner report).** The beam-on-water read fine at dusk but all but **vanished at complete dark** —
exactly when it matters (P1/P5: dark nights navigated by boat lights). Root cause: the fix-2 term was added to
`col.rgb` **before** two downstream crushers. (1) The ADR 0013 overlay **multiplies** the whole frame by
`_DayNightTint`; at deepest night that is ≈ `(0.022, 0.029, 0.061)`, so the beam survived at ~3–6%,
blue-shifted. (2) At deep night the ADR 0015 grade's day/night value floor saturates and pulls **all**
pre-overlay water toward luma 1, flattening lit-vs-unlit contrast. The cone/gate curves themselves were correct.

**Fix.** The beam (plus the reflection's night-gated sky content — moon/glitter/stars, which had the same
pre-overlay crush) now composites **after `PaletteGrade()`**, divided by
`max(_DayNightTint.rgb, DN_COMP_MIN_CHANNEL = 0.02)` so the overlay's multiply **cancels** — the same
pre-compensation pattern ADR 0015's `PaletteValueFloorDayNight` established. The 0.02 floor bounds the boost at
≤ 50×; the shipped deepest-night channels all exceed it, so cancellation there is exact (no hue shift). Daylight
is pixel-identical (the beam is night-gated to 0 by day); cycle-off (edit mode / demo) adds the term raw (no
overlay to compensate for). **Depends on HDR being ON** (`UniversalRP.asset m_SupportsHDR: 1`) so the
compensated >1 values survive to the overlay — re-check if a later mobile port disables HDR. The rail no longer
bounds the lit pool (deliberate: it would clamp the compensated values); it still grades the sea under the beam.
Headless twin: `LightMath.CompensateForDayNightTint` (+ `DayNightCompensationMinChannel`), pinned in
`LightMathTests`. Full mechanism: `design/water-rendering.md` §11.6.

## Amendment — the beam lights the sea's SHAPE, and many lamps can do it (wave relief, 2026-08-29)

**The owner, 2026-08-28:** *"the spotlights and headlights need to put shadows, the spotlight over the water
is just one uniform shape with a gentle gradient. The light needs to affect the environment, create shadows.
it should highlight the water at crests and be shadowed at the valleies of waves unless the proper light angle
exposes them."*

He is describing follow-up fix 2 exactly. `BoatLightTerm` returned `radial × cone` — a shape in the ground
plane, blind to the sea underneath it. Lit water and unlit water differed only in brightness, so the beam read
as a decal laid over the waves rather than light falling on them. **The additive glow stays what it always
was — the lamp's own bloom. It is no longer the illumination model.**

### The decision: N·L against the shared wave field, normalized by flat water

The water fragment already evaluates the ADR 0018 wave field per pixel and already has its **analytic slope**
(`WaveFieldSample`'s `slopeXY`, which the swell FACE SHADING rides). A height field's surface normal is
`N ∝ (-∂h/∂x, -∂h/∂y, 1)`, so the relief is available for the cost of a dot product — no second field, no
re-derived phase, no new sampling. One quantity, one computation.

```
L      = normalize(lampWorldXYZ - pixelXYZ)         // lamp height above THIS pixel's own surface
lz     = max(L.z, _BeamReliefMinElevation)          // floored ONCE, reused in both places below
relief = (lz - dot(slope, L.xy)) / (lz · sqrt(1 + |slope|²))
weight = radial · cone · lerp(1, relief, strength)  // the ADR 0016 cone, now shaped by the sea
```

**Why a lamp is not the sun.** ADR 0013's sun is a direction at infinity: one world vector for the whole sea,
which is why the swell face shading can use a constant. A lamp is a **point at a height**, so `L` differs at
every pixel — steep underfoot, grazing at the far end of the throw. Every clause the owner asked for is that
one fact, with no special cases in the code:

| his words | what the maths does |
|---|---|
| "highlight the water at crests" | a facet turned into the beam has `dot(slope, L.xy) < 0` ⇒ relief > 1 |
| "shadowed at the valleys" | the back slope has it > 0 ⇒ relief < 1, clamped at 0 when turned away |
| "unless the proper light angle exposes them" | small `lz` (a low lamp) divides the term up; large `lz` divides it away |

**Measured** (`BeamWaveReliefTests.Measure_ReliefSpreadAgainstLampHeight`, a real sea at sea-state 0.55):

| lamp height | 0.5 m | 1 m | 2.5 m | 5 m | 10 m | 60 m | 1000 m |
|---|---|---|---|---|---|---|---|
| relief spread | 1.140 | 1.093 | 0.571 | 0.296 | 0.159 | 0.044 | 0.028 |

Two things in that table are load-bearing. A **1 m** lamp separates crest from trough **24×** harder than a
60 m one — the angle genuinely decides what the beam exposes. And at **1000 m** the spread converges on
**0.0279** against a computed geometric floor of **0.0273**: pushed to infinity the lamp becomes the sun, the
whole directional term vanishes, and only the area foreshortening a tilted facet always suffers is left. A
model that kept any angular dependence out there would not land on that number.

### The glass calm is sacred, by construction rather than by tuning

`N·L` is divided by the dot product a **flat** facet would have had **at the same pixel** (`lz`, floored once
and reused in both the numerator and the divisor). Zero slope therefore cancels to **exactly 1** for any lamp
position, height or range — so a searchlight sweeping a dead-calm sea leaves §11's mirror bit-identical. This
is asserted bit-exactly over a sweep of geometries, including lamps below the elevation floor, which is the
case where a naive implementation (clamping one side but not the other) silently stops cancelling.

Two independent **exact passthroughs** guard the shipped look, both bit-exact: `_BeamReliefStrength = 0`, and
a lamp that publishes no height (`pos.z == 0` — a legacy publisher, a bare material). Either one yields the
flat ADR 0016 cone unchanged.

### Many lamps: the array this ADR reserved

Follow-up fix 2 said *"the clean extension to many is to publish ARRAYS + a count and loop — the single-light
path is a count-1 case of that."* That is now built: `WaterLightBridge` (Art, self-installing on the
`WaveFieldBridge` pattern) collects registered `IWaterLightEmitter`s, keeps the **4 nearest the camera**, and
publishes `_WaterLight*[4]` + `_WaterLightCount`. Budget (rule 7): the beam term is bounded at four cone
evaluations per water pixel however many lamps a scene grows, the loop is `[unroll]`ed over the fixed bound
with the count masking inside (the shape `WaveFieldSample` uses for its eight trains), and each slot
early-outs on intensity — so a scene with one searchlight pays for one.

**The legacy `_BoatLight*` singleton is kept and still published**, because a **second lit path** reads it:
`SpriteLitDecor.hlsl` lights trees, shrubs and shore plants from that one lamp. Two lit paths are deliberate
architecture (ADR 0013 / the lit-sprite ruling), so this change publishes the array **alongside** the singleton
and alters neither the singleton's contract nor the decor path. The water sums the **array** when the count is
live and falls back to the singleton when it is 0 — never both, or the primary lamp would be counted twice.

### It reaches the screen, and it is measured there too

The pure tests cannot prove a lit pixel exists, so `BeamReliefRenderTests` stands the real Nine Mile Creek
coast at pre-dawn, publishes a real sea and a real searchlight through the shipped bridge, and photographs it.
The metric is the **relative** luminance change of the lit pool, measured on the **pre-overlay HDR** values,
against a control of **two identical shots** — same sea, same beam, same brightness, only the clock differing.
Three earlier metrics had to be thrown away first: an absolute delta measured after the night overlay lands
inside 8-bit quantization (it reported one least-significant bit); an out-of-cone control under-reads the clock
because lit water is brighter; and aiming at the deepest water put the sea rect edge in frame, so half the
control region was black void that could never change and silently flattered every ratio.

| | dial moves the lit pool | clock alone | |
|---|---|---|---|
| a working sea | 12.8% | 1.9% | **6.6x the clock** |
| a gale | 15.0% | 0.7% (out of cone) | confined to the cone |
| **no waves at all** | **1.5%** | **3.2%** | **0.49x — less than the clock, i.e. nothing** |

That last row is the glass calm proved in pixels as well as in algebra. And the relief **shapes** the pool
rather than merely brightening it: mean in-cone luminance moves 0.480 to 0.492, **+2.4%**.

### Known limitation, stated rather than hidden

Since #686 raised the additive lights above the sea, the lamp's **quad** also lays its flat amber cone over
the water the shader is now lighting with relief — two illuminations stacked, the flat one washing out the
shaped one. The quad **cannot** tell water from land: it works in quad space and has neither the seabed height
map nor the water level (both per-material on `Water.mat`). Making it spatially water-aware means publishing
the seabed as new globals and rewiring the LAND lighting path, which is its own slice. What ships here is the
lever, defaulted to today's look: `BoatSpotlight._quadGlowScale` (1 = the shipped full-length quad; lower pulls
it back to a source glow at the lamp and lets the water channel carry the throw). The eyeball pack shows the
pair; the value is the owner's call.

**Implementation:** `LightMath.WaveReliefFactor` / `.ApplyReliefStrength` (the pure twin) ·
`HiddenHarboursWater.shader` (`BeamRelief`, `BoatLightWeight`, the summing `BoatLightTerm`, `_WaterLight*[4]`) ·
`WaterLightBridge.cs` + `IWaterLightEmitter` · `BoatSpotlight` (lamp height, registration, quad glow scale) ·
`Water.mat` (the three dials) · `BeamWaveReliefTests`, `WaterLightBridgeTests`, `BeamReliefRenderTests`.
The HLSL is guarded against drifting from the C# reference by source assertions on the shader text.

## Rejected alternatives

- **URP `Light2D` now.** The sprites are Sprite-Unlit and sample no 2D light (the ADR-0013 finding); this needs
  the project-wide Sprite-Lit migration ADR 0013 rejected, and is un-validatable headless. Kept as the *future*
  path (2).
- **Per-light C# that reads the clock to fade the beam.** Couples every light to the cycle and risks drift; the
  in-shader gate off the published `_DayNightTint` is zero-coupling and consistent by construction.
- **`System.Random` flicker.** Violates rule 5 (hidden randomness, non-reproducible). We use a `(seed, time)` hash.
- **A bow light that references `BoatController`.** Violates rule 4 (Art → Boats coupling). Heading/speed come
  from `transform`; the Core `IActiveBoatService` is the seam if a richer read is needed later.
- **Building the worklight / window-glow / lightpost now.** The owner said "start with a boat spotlight"; the
  rest are structured-for follow-ups (CLAUDE.md §4 keep-PRs-small, rule 8 stay-in-phase).
- **A third quad-sorting fix for the beam-on-water.** Two failed already (the original PR; the `SortingGroup` +
  camera-depth pin). The URP 2D renderer will not reliably composite an additive `MeshRenderer` over the
  full-screen **custom-shader** water `SpriteRenderer`; the water is lit **in its own fragment** instead
  (follow-up fix 2), which sidesteps ordering entirely. The land quad is kept (it works on land).

## Amendment — lights PR B: the lamps cast SHADOWS (2026-09-01)

**The owner, 2026-08-28:** *"the spotlights and headlights need to put shadows ... The light needs to
affect the environment, create shadows."* PR A (#691) made the beam light the sea's shape. This is the
other half of the sentence: a caster standing in a lamp's light now throws its silhouette AWAY from
that lamp, by a length that grows with its distance from the lamp and shrinks with the lamp's height,
at a strength that is the lamp's own falloff at its feet — and the shadow moves as the beam sweeps.

**Implementation:** `Assets/_Project/Code/Art/LampShadowMath.cs` (the pure model), `LampShadowSystem.cs`
(the self-installing pool), `LampShadowProfile.cs` (the owner's tunables), `HullLampShadowCaster.cs` (a
mesh hull as a caster), `Assets/_Project/Art/Shaders/HiddenHarboursLampShadow.shader` +
`Resources/LampShadow.mat` / `LampShadowHull.mat`; `SceneLight` gains `CastsShadows` /
`LampHeightMeters` and registers with the system; every `SpriteShadow` (every sun caster) registers as
a lamp caster; the presentation service fits a hull caster where it fits her lamps; the St Peters wharf's
standing fittings (bollards, pileheads) gain a `SpriteShadow` so the pilings the searchlight rakes can
throw. Tests: `LampShadowMathTests`, `LampShadowSystemTests`, `LampShadowRenderTests` (GPU, self-skipping
on CI), `LampShadowPlayTests`.

### The model: a MULTIPLY drawn ABOVE the glow — and why not a dark sprite under the caster

The sun shadow (ADR 0013 §7, `SpriteShadow`) is a dark alpha-blended silhouette sorted one order UNDER its
caster, and for the sun that is right: the world is what the sun lights. **A lamp's light is not in the
world.** It is ADDED after the whole-frame multiply — this ADR's additive quad on land, the pre-compensated
in-shader beam on water (fixes 2–3) — so a dark sprite in the world sort is crushed to black by the night
along with everything else, and the glow is then added on top of it unchanged. At night such a shadow is
invisible by construction (and it was measured so before this design was settled).

So a lamp shadow is a THIRD thing: a quad drawn **above every glow** with `Blend Zero SrcColor`
(`dst *= lerp(1, tint, alpha)`), which removes a fraction of whatever light is at the pixel — quad glow,
water beam, lit decor — and leaves an unlit pixel exactly as it was. It is not a second illumination
model: the additive quad stays the glow, the water's relief stays the water's, and the water shader, the
foam buffer and the light bridge are untouched (the water lane's files).

### The silhouette, per pixel, through the inverse shear

The quad rasterised is the axis-aligned box of the caster's SHEARED image. Each fragment runs the shear
backwards — `LampShadowMath.Unshear`, twinned verbatim in the HLSL and pinned by a source guard — to find
which caster point it is the shadow of, and asks the caster's silhouette whether that point is opaque:

| caster | silhouette source | why |
|---|---|---|
| a sprite (every `SpriteShadow`: trees, shrubs, shore plants, the player, the wharf's standers) | its own sheet, world → cell → texture uv (`_SpriteRectWorld` / `_SpriteRectUV`, published per renderer) | the same alpha the sun shadow shears |
| a mesh hull (`HullLampShadowCaster`, every `IsoFacetHullRenderer`) | the feature's resolved screen texture `_HHHullScreenTex` at that point's screen pixel, filtered by her ID BLOCK — the same either-id test her overlay and reflection passes use | she has no sprite; whatever she is drawing this frame (heading, roll, an open house) is what casts, with no second silhouette pass and no bake |

The charter asked whether the object-reflection target (`_HHReflectTex`, ADR 0027 #8) could serve as the
hull's caster mask. Measured against the code: it holds every reflector MIRRORED about its own pivot, with
no per-pixel owner, and is rendered only when reflectors are near water — a shadow read from it would carry
a neighbouring boat's mirrored planking and vanish for a hull hauled ashore. `_HHHullScreenTex` is the
unmirrored image, id-tagged per hull, rendered for every camera before any sprite draws. It serves; the
mirror does not.

A shadow never darkens its own caster: a fragment lying on the caster's own opaque pixels discards first
(the sun shadow gets the same effect by sorting under its caster).

### Direction, length, fade (`LampShadowMath`)

- **Direction** — radially away from the lamp through the caster's feet, per (lamp, caster). A caster
  under the lamp falls back to the beam axis (a cone) or down the screen (a round lamp).
- **Length** — the sun's own elevation→length curve (`DayNightMath.ShadowLength`: a 0.35× stub at the
  zenith, a 5× rake at the horizon, capped at 7×) driven by the LAMP's elevation as seen from the feet,
  `h / sqrt(h² + d²)`. A low lamp rakes long behind a far caster; the height is floored at 0.5 m so a lamp
  that never declared one throws a bounded rake. A shadow thrown down the screen is capped so the shear
  stays invertible (`ClampShearFold`) — the sun never meets that case, a lamp can be anywhere.
- **Fade** — alpha = strength × the lamp's own radial × cone falloff at the feet (`LightMath.ShapeIntensity`,
  the additive quad's own curve) × the SAME night gate the glow uses × the lamp's intensity share (a
  searchlight dimmed at a standstill fades its shadows). A caster in the feathered edge throws a feathered
  shadow; outside the cone or beyond the range, none. `ShadowAlpha(0, …)` returns exactly `0f`.

### The sorting law, in numbers

There is no order above `short.MaxValue` (#686's clamp), so the shadow quads share the light quads' ceiling
order and win the tie by DEPTH — the 2D renderer breaks equal orders back-to-front along the view axis:

| element | order | depth pin (metres in front of the camera) |
|---|---|---|
| day/night overlay | 32760 | `DayNightController.OverlayNearOffset` = 0.02 |
| lamp shadows | 32767 | `LampShadowSystem.ShadowDepthOffset` = **0.06** |
| additive light quads | 32767 | `SceneLight.DefaultCameraDepthOffset` = 0.10 |

Nearer draws later, so a shadow lands over every glow. `LampShadowMathTests.TheDepthPins_AreOrdered…`
pins the three constants in that order.

### Budget (rule 7)

- **The pool:** `LampShadowProfile.MaxShadows` = **24** quads shipped, one shared unit mesh, two shared
  materials, one property block, no per-frame allocation. Past the pool the NEAREST lamp-to-caster pairs
  win (an insertion sort into the fixed slots, the `WaterLightBridge` shape).
- **The scan:** O(lamps × casters) at 10 Hz (`RefreshHz`), the caster states gathered once per tick.
  St Peters today: 6 lamps (the cape's five glows and her searchlight) against ~1,000 registered casters
  = ~6,000 squared distances ten times a second. The POSE of the chosen shadows follows every frame.
- **Idle cost:** a lamp gated off by day, or with no caster in range, pairs nothing and enables nothing.

### Passthroughs, proved

- **Strength 0 is today's frame, byte for byte** — `LampShadowRenderTests.APost_…` shoots strength 0 against
  the system absent and compares every byte (the scene is clock-free, so two identical shots ARE identical).
- **Sun shadows do not move** — the lamp system never writes a caster's own block; `LampShadowPlayTests.
  TheSunShadow_IsUntouchedByALampInRange` reads the sun shadow's direction, length, alpha and pivot map with
  and without a lamp in range.
- **Noon is the control** — the shadow gates with its lamp (`LightMath.NightGateWithFallback`, the shader's
  own ramp); at a bright tint nothing pairs and nothing is enabled.

### The approximation, stated

This is 2D iso. A lamp shadow is the caster's SKEWED SILHOUETTE — one direction per caster, parallel edges,
screen height standing in for world height (a hull's far rail shears as if it were tall) — not a raycast.
Known and accepted:

- where two lamps overlap, one lamp's shadow also dims the other's light (a fraction of ALL light present
  is removed, because the water and the land light by different models and the shadow cannot know how
  much of a pixel is which lamp);
- through twilight the multiply also dims the little ambient under the shadow, not only the lamp's share;
- nothing self-shadows (a wheelhouse does not darken its own deck), and a rotated sprite casts its unrotated cell;
- a hull's lamp heights are her rig's z above the KEEL (the def carries no waterline), so a sidelight's rake
  reads a little steeper than truth;
- shadows follow the additive quad's range; with `_quadGlowScale` pulled below 1 the water beam reaches
  past where shadows are cast.

The owner's eye is the judge; if the skew reads wrong, the lever is the profile's length curve, not a raytracer.

### Tunables (rule 6)

`Resources/LampShadowProfile.asset` (optional; code defaults otherwise): `Strength` 0.8 (THE dial, 0 = off),
`ShadowColor`, `MaxShadows` 24, `RefreshHz` 10, `LengthAtNoon` 0.35 / `LengthAtHorizon` 5 / `MaxLength` 7,
`MinLampHeightMeters` 0.5, `MinShearDenominator` 0.2, `PixelSnap` + `PixelsPerUnit` 32. Per lamp on
`SceneLight`: `CastsShadows` (default on), `LampHeightMeters` (2.5; `BoatSpotlight` publishes its own,
`BoatLamps` each lamp's rig z).

### Rejected alternatives

- **A dark sprite one order under the caster (the sun model).** Invisible at night by construction — see above.
- **`_HHReflectTex` as the hull mask.** Mirrored, un-owned per pixel, water-gated — see above.
- **A second silhouette render per hull (a CommandBuffer capture to a small RT).** Works, but duplicates a
  silhouette the feature already resolves every frame with ids attached; rejected for cost and duplication.
- **A subtractive blend removing the lamp's estimated contribution.** Exact for overlapping lamps in
  principle, but the water's beam and the land's quad are different models, so the estimate would be wrong
  on one surface or the other; the multiply is robust to both.
- **Shadows into the water shader.** The water lane's file (serial PRs, one shader); the multiply above the
  glow cuts the in-shader beam without touching it.

## Amendment — boat-lights PR 2a: the whole fleet wears her lamps, and a MOORED boat may not show them (2026-09-03)

**The owner, 2026-08-27:** *"it would be cool to arrive in the morning so long as the boats lights are
working ... cabin light on, navigation lights on, spotlight working."* PR 1 (#686) lit the one hull the
intro's arrival is run on. This is the second half: **every hull whose rig says where her lamps are now
declares them**, and — the design work of the PR — **a boat lying still is no longer allowed to burn the
lights that say she is under way**.

### The lamp tables: 27 hulls, measured, not eyeballed

Nine rigs publish `navMounts(dir)` and they dress twenty-seven hulls between them (the lobster generator
alone makes eighteen). `HullMeshDef.Lamps` gains a row set on each. The open boats — the dory, the punt,
the console skiff, both sport skiffs, both zodiacs — publish no mounts and carry no lamps: **absence is
data**, and it stays so.

**The measurement.** A rig publishes `navMounts` as SCREEN points, one answer per facing, because that is
what a sprite bake needs; a mesh hull needs the boat-local triple behind the projection. At rest (no roll,
no pitch, no heave) that projection is AFFINE in `(x, y, z)`, so eight facings are sixteen linear equations
in three unknowns: `BoatLampAnchorProbe` solves them in the least-squares sense against the RUNTIME's own
`IsoFacetMath.RigToWorld`, and the residual is not a fitting error but floating noise — **worst 2.3e-13 px
across all twenty-seven hulls**. Two independent checks say the inversion is right rather than merely
self-consistent:

- `sportFisherIsoRig2` is the one rig that has answered #686's upstream ask and publishes a boat-local
  `NAV` table directly. The inversion reproduces it to **3.6e-15 m**.
- The Cape Islander's six rows were measured by another lane, by another method, four days earlier. The
  inversion reproduces them to **1.8e-15 m** — and her def is not re-derived from it: PR 2 leaves her
  shipped numbers alone and pins them value by value.

The probe **prints; it does not write.** That is this ADR's own rule and it still holds: the mesh baker
must not author `Lamps` until the export contract grows a boat-local `NAV` table beside `navMounts` on
every rig. Until then the def is hand-authored and the probe is the instrument that says what it should
contain — `Hidden Harbours / Rig Baking / Probe: boat lamp anchors`.

**The two lamps no rig projects.** A cabin glow goes at the centre of the room the rig publishes, at the
centre of that room's own glass band — which reproduces the cape's shipped `(0, 1.52, 2.21)` exactly: her
house-box centre and her side-glass centre. A searchlight goes on the front of the roof of the room she is
conned from, 0.14 m aft of its face and 0.08 m above it — which reproduces her shipped `(0, 2.4, 3.1)`
exactly. Both constants are calibrated on her and on nothing else. The three published HOUSE shapes
(`wheelhouse`, `ship` with a `decks.bridge`, `sport` with an open flybridge) are read as they are written.

### Two new kinds (append-only)

| kind | why it is not one of the existing five |
|---|---|
| `AnchorLight = 6` | The other half of the rule of the road, and the reason a regime exists at all. Hoisted at the masthead — the highest point every one of these rigs names. Dimmer and shorter than the masthead it hangs in place of: a wharf of seven anchor lights each as bright as a steaming light reads as a fleet getting under way. |
| `RangeLight = 7` | The second masthead a vessel of fifty metres or more carries. Only the tanker has one, and her rig publishes it under its own name (`range`). Its LOOK is the masthead's verbatim; it is a separate KIND so that "one lamp of each kind" stays an exact guard rather than a hull with two mastheads collapsing into one duplicated row. |

### The regime — and what it is worth

`IVesselWay` (Core) on the boat root answers `UnderWay` or `Moored`; `MooredBoat` answers *moored*, and
**a hull that answers nothing is UNDER WAY**. That default is load-bearing: it is exactly what every
lamp-bearing hull did before the regime existed, the arrival's Cape Islander among them, so absence means
"no change" and never "dark". `BoatLamps.ShowsWhen(kind, way)` is the whole rule, pure and pinned headless.

- **Under way** — sidelights, stern, masthead, range. Not the anchor light: a boat making way is not at
  anchor, and showing both says both.
- **Moored** — the anchor light only. Showing sidelights while lying still is the one lie a navigation
  light can tell, and it is refused here rather than dimmed somewhere downstream.
- **The cabin glow is not a navigation light** and burns in both. Nobody takes a bearing off a lit window.

Two states, not three. A seaman separates "made fast alongside" (strictly, no lights at all) from "at
anchor" (one all-round white); both are collapsed into `Moored` and both show the anchor light. That is a
PICTURE decision made on purpose — a wharf of seven working boats showing nothing whatever is a black hole
in the middle of the harbour at two in the morning — and the lie it tells is the opposite of the dangerous
one.

**What it prevents, concretely.** Nine Mile Creek moors seven owned boats against the wharf wall and the
review anchorage holds every hull that has art. Without the regime, `MakeLit`/`MakeSearchlit` as written
would have lit every one of them with sidelights, mastheads and a burning searchlight, all night.

### The searchlight's owner — asked of the helm slot, answered by the BOAT

`MakeSearchlit`'s own documentation owed PR 2 this: *"when the player is given a hull that declares a
searchlight, 'is this the boat whose wheel the player is holding' becomes a real question."* It is now
asked properly, every frame, of `HelmSlot` — **and the honest predicate is `IsPlayersBoat`, not
`IsPlayerHelm`.** `IsPlayerHelm` carries `HasHelm`, which is the ENGINE question, and the boat the player
owns at the opening is a ROWED DORY that has carried a switchable searchlight since the builder first
bolted one on. Gating on the helm would have looked right, passed a fixture written against a powered
hull, and killed the L key on the starting boat. A searchlight is a boat's TACKLE, like her anchor: at the
wheel or on her deck, it is hers. (This is #642's own lesson, applied: an INSTRUMENT belongs to a helm; a
boat's tackle does not.)

Art holds a `Transform` and the slot arbitrates on an opaque `BoatController` token, so `HelmSlot` gains
an `IsPlayersBoat(GameObject)` overload that resolves both sides to the GameObject they live on — the one
comparison that can be made honestly across that seam.

Three consequences fall out:

- The old rule "a def-minted beam is deaf to the key" is retired. It was a blunt instrument for "not the
  player's", and it also meant a hull the player BOUGHT could never work her own searchlight. Every beam
  is now key-capable; ownership decides.
- `MakeSearchlit`'s destroy discriminator moves off `KeyTogglesBeam` (now true everywhere) onto
  `BoatSpotlight.MintedFromDef` — what it always actually meant.
- A def-minted beam nobody is aboard follows her way: lit under way, out at her berth. It acts **on the
  transition**, not every frame — re-asserting sixty times a second would silently stomp anything that set
  the beam by hand. A builder-bolted beam (the player's) is never driven by the regime at all, or walking
  up the wharf would light her dory behind her.

### Which hulls carry a searchlight — a measurement, not a size class

The shipped beam throws `BoatSpotlight.DefaultRangeMetres` = 9 m forward **from its mount**. A hull conned
from far aft would therefore rake her own foredeck rather than the sea. So the probe measures, per hull,
whether the beam clears her own stem, and declines to declare a mount she cannot use:

- **21 carry one** — the cape, the lobster boat, her eighteen variants, and the sport fisher convertible.
- **6 do not** — the side dragger, both stern trawlers, the coastal packet, the tanker, and the sport
  fisher skybridge.

The fleet separates cleanly: every hull that clears does so by **2.3 m or more**, every hull that does not
falls short by **0.9 m or more**, so nothing is balanced on the line. Unblocking the six is a per-hull
throw — a preset/data change — and is not this PR.

### What did NOT change

The sidelight preset's 0.28 m radius stands: the tightest pair in the whole fleet is still the cape's
0.6048 m, and the preset test now MEASURES that off the shipped defs instead of restating it, so the day a
narrower hull is imported the bound moves with her. Nav lamps and anchor lights stay off the four-slot
water bridge — only `BoatSpotlight` lights the sea, and a moored fleet must not evict the beam the player
is steering by. Nothing about the day/night curve, the water shader or the beam relief is touched.

**The cape's anchor light is appended LAST on her def, and that is not cosmetic.** `BoatLamps` builds one
child light per lamp in array order and `SceneLight`'s deterministic flicker is seeded from the child's
SIBLING INDEX — the trap that cost #702 five false reds. A row inserted before her cabin glow would re-seed
its flicker and move her shipped pixels; appended last, every earlier lamp keeps the index it had, and the
anchor light is disabled while she is under way anyway. She needs one at all because one of the seven boats
moored at Nine Mile Creek is a Cape Islander, and she would otherwise be the one dark hull on the wall.
