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
   Harbours ▸ Build Light Test` (a dark ground plane + a boat-spotlight cone + a radial lantern; press Play,
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
