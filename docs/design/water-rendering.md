# Water Rendering — the layered URP water shader (recipe + the shipped first pass)

> **Status: FIRST PASS SHIPPED (greybox-real).** The layered shader now exists as a **text URP 2D
> HLSL/ShaderLab shader** (NOT a Shader Graph — authored as text so it builds headless), wired to the
> deterministic sim. The §0 "Applying the shader" note below covers what shipped and how to use it;
> §2–§5 remain the layer-by-layer recipe (now describing the built layers). Colours / speeds / foam /
> thresholds are all Inspector tunables on the material — the owner art-directs the LOOK next; this is a
> solid first pass, not final polish. Decision of record:
> [`../adr/0010-water-rendering.md`](../adr/0010-water-rendering.md).
>
> ---
>
> ## 0. Applying the shader (what shipped + how to use it in ANY scene)
>
> The first pass ships three reusable pieces:
>
> | Asset | Path | What it is |
> |---|---|---|
> | **Shader** | `Assets/_Project/Art/Shaders/HiddenHarboursWater.shader` | the custom URP 2D unlit `HiddenHarbours/Water` shader — all five layers, every colour/speed/threshold a material property |
> | **Material** | `Assets/_Project/Art/Materials/Water.mat` | the tunable instance the owner art-directs (the single place to change the look) |
> | **Runtime** | `Assets/_Project/Code/Art/WaterSurface.cs` (`HiddenHarbours.Art`) | the SIM→shader bridge MonoBehaviour |
>
> **To put live water in ANY scene (including the hand-painted cove):**
> 1. On the scene's **water plane** SpriteRenderer (or a quad), set its **Material** to `Water.mat`.
>    The shader ignores the sprite texture — it draws everything procedurally from world position — so
>    any sea sprite/quad works as the canvas.
> 2. Add the **`WaterSurface`** component to the same GameObject. Set **Height world center / size** to
>    the world rectangle the water covers, so the baked seabed depth map lines up with that region's
>    `TidalTerrain`. Leave the rest at defaults.
> 3. Press Play. `WaterSurface` reads `GameServices.Environment` + `GameServices.TidalTerrain` and feeds
>    the surface every throttled tick — water flows with the current, roughens in wind, and its
>    shoreline/foam track the tide. With no `TidalTerrain` wired the plane reads as uniform deep water
>    (no false shoreline) — safe in any region.
> 4. **Tune the look** on `Water.mat` in the Inspector: depth colours/bands, surface noise/flow, foam
>    width/softness, specular amount/sharpness/light-dir, caustic amount/scale/depth, and the pixel grid
>    (`Pixels Per Unit`, default 32). No graph editing, no code.
>
> The St Peters builder applies this automatically to the `Sea` plane (the free demo touch). To see it
> move: **Hidden Harbours ▸ Build St Peters Scene**, open `StPeters.unity`, press Play, and tick the
> `DevFastTide` object (or use the Tide Scrubber) to sweep the tide and watch the shoreline + foam move.
>
> **Phase note.** This is the M1 **VS-24** first pass (the §3.6 water backbone), deepening into **M2/M3**
> advanced rendering. Greybox-real: a solid, tunable first pass the owner colours next; the per-pixel
> authored height-map texture (vs the current coarse bake) and the runtime-vs-bake fork remain §9 open
> questions for the deeper passes.
>
> **Ownership** ([`../../agents/coordination.md`](../../agents/coordination.md) §1.1 "Water/fog/lighting"):
> **lead-architect** owns the URP Shader Graph *plumbing* (layer/subgraph structure, height-map
> sampling, the pixelize pattern, the `WaterLevelAt` hookup); **art-pipeline** owns the *look* (palette,
> foam/caustic/specular textures, tuning). Tune together.
>
> **Reference target.** A Unity URP Shader Graph water tutorial the owner picked: a **main water
> shader** assembled from **caustic, specular, and sea-foam subgraphs** over a depth-driven base. We
> reproduce its *technique*, adapted to our **pixel-art** look and our **height-map / tide** truth.

---

## 1. North star (what this shader is for)

Water is a **first-class P1 system** ("The Sea Has Moods"), not a backdrop
([`art-and-audio-bible.md`](art-and-audio-bible.md) §3.5). This shader delivers the hero water look:
shallow→deep colour, living surface, foam that hugs the **moving** tide waterline, sun glint, and
caustic shimmer in the shallows — all while reading as **PPU=32 pixel art** (§3.1), and all driven by
the **same height-map + tide data the gameplay reads**, so what the player *sees* and what the physics
*does* are one truth (the P1 integrity rule, §2.2).

Two rules dominate every layer below:

1. **Pixelize world coords in every layer** (§3). The surface must read as pixel art, not smooth 3D.
2. **Depth comes from the shared height map** (§4): `depth = waterLevel − terrainHeight`, the same
   arithmetic as `Core.TidalExposure.WaterDepth` and the same `IEnvironmentService.WaterLevelAt` the
   walkability sim uses (ADR 0009 / #59).

---

## 2. The layer stack (foundation → polish)

Build in this order; each layer is a Shader Graph **subgraph** (mirroring the tutorial's structure),
composited into a **main water shader**. Earlier layers are the foundation later ones mask against.

| # | Layer / subgraph | Input | Produces | Pillar |
|---|---|---|---|---|
| 1 | **Depth gradient** (base) | height map → `depth` | shallow→deep base colour ramp | P1 (reads as water) |
| 2 | **Surface distortion** | scrolling perlin/value-noise × time | swell, living surface; UV warp for later layers | P1 (moods) |
| 3 | **Sea-foam fringe** | foam texture, masked by depth≈0 band | foam hugging the moving waterline | P1/P5 (tide tell, hazard edge) |
| 4 | **Specular** | sun/sky dir + surface | glint highlights on the surface | P1 (light/mood) |
| 5 | **Caustics** | perlin × time, depth-gated to shallows | rippling light over the visible seabed | P1/P5 (read the shallows) |

> Composite order is bottom-up: **depth gradient → (distort surface) → specular over surface → foam at
> the edge band → caustics gated to the shallow depth range.** The distortion subgraph's output UV/offset
> is reused to animate foam, specular and caustics so they all "swim" with the same surface.

---

## 3. Pixel-art fidelity (MANDATORY in every layer)

Every layer/subgraph **pixelizes world coordinates** before sampling noise/textures, so the result
snaps to the PPU=32 grid and reads as pixel art rather than smooth 3D water. The node pattern:

```
World Position  ──► Multiply (× PPU, e.g. 32)  ──► Floor  ──► Divide (÷ PPU)  ──► pixelized coord
```

- Apply this to the coords feeding **noise** (surface, caustics), **foam UV/mask**, and **specular** —
  not just the base colour. A layer that samples smooth coords will betray the pixel look.
- `PPU` is a shader property (default 32, matching §3.1), **not** a hard-coded literal (CLAUDE.md
  rule 6) — so art-pipeline can experiment without editing the graph, and a future zoom band can pass
  an effective PPU.
- The depth ramp itself can be **posterized** (quantized to N colour bands) to reinforce the pixel
  feel and read as the master palette's discrete water ramp (§4).

This rule is the difference between "URP water that happens to be in our game" and "our water." It is
non-negotiable and holds the LOCKED §2 (one perspective) / §3 (one scale, PPU) rules.

---

## 4. The height-map unification (the key architecture)

A single **height map** — per-region **terrain elevation in metres above chart datum** — is the shared
source of truth for **three** consumers, all reading the *same* number:

1. **This shader** — depth gradient (layer 1) and foam band (layer 3).
2. **Tide walkability** — `Core.TidalExposure.IsExposed(WaterLevelAt(t), terrainElevation)` and
   `IEnvironmentService.WaterLevelAt(t)` (ADR 0009 / #59); the on-foot walkability sim.
3. **Boat-cross** — "deep enough = passable": boat draught vs `WaterDepth` (boats doc owns the
   consequence).

The one equation, everywhere:

```
depth = waterLevel − terrainHeight        // metres; <= 0 means dry / exposed
        └─ WaterLevelAt(t) ─┘  └─ height map sample ─┘
```

- `waterLevel` = `IEnvironmentService.WaterLevelAt(gameTime)` — deterministic, recomputed from
  `(worldSeed, gameTime)`, **never saved** (CLAUDE.md rule 5; ADR 0009). The shader receives it as a
  **material float**, set on tide change / the slow tick — **not** recomputed per frame.
- `terrainHeight` = the authored height-map value at the position (read-only authored content).
- This generalizes the canon **seabed-elevation / bathymetry heightfield** — already named the *single
  source of truth* for "passable / walkable / hazard" in
  [`time-tides-weather.md`](time-tides-weather.md) §3.5 — to **all** terrain (land above datum
  included). It resolves the **rendering half** of that doc's **OQ1** (mapping tide→visual cues).

**The St Peters sandbar is just a low ridge in the height map.** As the deterministic tide falls, the
ridge's `depth` crosses zero; the shader's foam band (layer 3) sweeps across it *and* the same
zero-crossing makes it walkable (`IsExposed`). Render and sim cannot disagree — they read one map. The
Drownded Lands flats and Sunkers tide-pools work the same way.

> **Why this matters:** decoupling a "visual seabed" from a "physics seabed" is exactly the drift ADR
> 0009 exists to prevent. One height map, three consumers, one equation.

---

## 5. Subgraph breakdown (the build recipe)

Each subgraph below lists *intent → inputs → method → pixelize point → tunables*. Tunables are
shader/material properties or Def values (rule 6), owned by art-pipeline.

### 5.1 Depth gradient (base) — layer 1

- **Intent:** the water reads shallow→deep; the base every other layer sits on.
- **Inputs:** height map sample (`terrainHeight`), `WaterLevelAt(t)` (material float) → `depth`.
- **Method:** `depth = waterLevel − terrainHeight`; remap `depth` over a shallow→deep **colour ramp**
  (the §4 master palette water ramp: pale shallow teal → deepwater navy `#16242E`). Optionally
  **posterize** to N bands for the pixel read. `depth <= 0` → fully transparent / hands off to terrain
  (the tile/Rule-Tile ground shows through; the shader does not draw land).
- **Pixelize:** posterize the ramp; pixelize the position used for any depth-edge softening.
- **Tunables:** ramp gradient + stops, band count, shallow/deep depth thresholds.

### 5.2 Surface distortion — layer 2

- **Intent:** a living surface — gentle swell, sea-state-driven amplitude (glassy → white-capped).
- **Inputs:** pixelized world coords, `time`, sea-state amplitude (from
  [`time-tides-weather.md`](time-tides-weather.md) weather/sea-state; a material float).
- **Method:** scrolling **perlin / value-noise** sampled at `(pixelizedCoord + time·scrollDir)`;
  output a small UV offset / normal-ish perturbation reused by foam, specular and caustics so they all
  swim together. Two octaves at different scroll speeds reads richer than one.
- **Pixelize:** pixelize the noise sample coords (§3) so swell snaps to the grid.
- **Tunables:** noise scale, scroll direction + speed (wire to wind dir later — §6), amplitude vs
  sea-state, octave mix.

### 5.3 Sea-foam fringe — layer 3

- **Intent:** foam that **hugs the moving waterline**, the headline tide tell (P1) and hazard edge
  around rocks/hulls (P5) — not a fixed painted edge.
- **Inputs:** `depth` (from 5.1), a **foam texture**, the layer-2 distortion offset.
- **Method:** build an **edge mask** from a **blurred-edge / depth≈0 band** — `smoothstep` of `depth`
  across a thin band around zero (`0 → foamWidth` metres), so the band sits exactly where the tide
  meets the seabed *now*. Multiply the foam texture (scrolled by the layer-2 offset so it churns) by
  the mask. Because the mask is a function of `depth`, the foam **follows `WaterLevel`** every tide
  with zero re-authoring — the live waterline lives **here, in the shader**, not in tiles (ADR 0010
  decision (4)).
- **Pixelize:** pixelize the foam-texture UV and the band so the fringe reads as crisp pixel foam.
- **Tunables:** foam band width (metres of depth), edge softness, foam texture + tint, churn speed.

### 5.4 Specular — layer 4

- **Intent:** sun/sky glint on the surface; night water dark with sparse highlights; fog flattens it
  (art-bible §3.5 "Reflections & light").
- **Inputs:** an **implied light direction** (the §3.5.1 single baked light dir, consistent with boats
  and shadows — *do not* invent a second sun), the layer-2 surface perturbation, sky/sun colour from
  the day-night grade (§6 lighting).
- **Method:** a cheap highlight where the perturbed surface faces the implied light; modulate
  intensity/colour by time-of-day + weather (calm glassy = sharp glint; gale/fog = scattered/greyed).
- **Pixelize:** pixelize/posterize the highlight so glints read as pixel sparkles, not smooth specular.
- **Tunables:** highlight sharpness, intensity vs sea-state/weather, sparkle density.

### 5.5 Caustics — layer 5

- **Intent:** rippling light over the **visible seabed** in the shallows — the shimmer that says "you
  can see the bottom here" (pairs with the depth sounder; P1/P5 "read the shallows").
- **Inputs:** pixelized coords, `time`, `depth` (for the shallow gate).
- **Method:** **perlin × time** distortion forming caustic ripple, **depth-gated** so it only appears
  where `depth` is within a shallow range (fades out into deep water). Composited additively over the
  depth gradient where the bottom would be visible.
- **Pixelize:** pixelize the perlin coords (§3).
- **Tunables:** caustic scale + speed, shallow depth range (fade in/out), intensity.

---

## 6. Edges: tiles vs shader (the division of labour)

- **Static terrain-type boundaries** (grass↔sand↔rock; road/wharf edges) → **Rule Tiles** (the
  existing autotile approach, art-bible §5 / §2.1). These don't move, so they're authored once.
- **The live, moving waterline + foam** → **the shader** (the depth≈0 band, §5.3). The waterline moves
  with `WaterLevel` every tide; **do not re-stamp tiles per frame** — that's per-frame authoring churn
  and forks the shoreline truth away from the height map (ADR 0010 decision (4)).

Rule of thumb: *if it moves with the tide, it's in the shader; if it's a fixed material boundary, it's
a tile.*

---

## 7. Phasing & what lands when

| When | What | Owner | Notes |
|---|---|---|---|
| **Now (St Peters greybox)** | **Height map** + a **flat depth-tint** (shallow→deep colour, no animation) | world / gameplay | Gameplay-relevant: the height map *is* the walkability data (ADR 0009 seam). A readable depth tint aids the fun-check. **Not** the shader. |
| **M1 VS-24** | The water + global-grade **backbone** (art-bible §3.6/§6) | lead-architect + art-pipeline | The first real shader pass once mechanics prove fun & placeholder art is dropped. |
| **M2** | Wet-surface tide effects; foam fringe maturing on real region art | art-pipeline | art-bible §6.1. |
| **M3** | The heavy pass: runtime-shader **vs 3D-water→2D bake** decided by a profiled spike; parallax-underwater preview | lead-architect + art-pipeline | art-bible §6.1, OQ2. The layer recipe here applies either way. |

The full shader slots onto the **same height-map data** the greybox already authored — it is a new
*consumer* of an existing field, **no data migration**.

---

## 8. Determinism, save & performance (the guardrails)

- **Determinism (rule 5).** The render is a **pure function** of the deterministic `WaterLevelAt(t)`
  (recomputed from `(worldSeed, gameTime)`, never saved) + the authored read-only height map. Surface
  & caustic **animation** is driven by `time` for *visual motion only* — it feeds **no** simulation and
  influences **no** walkability, grounding, or saved state.
- **Save (ADR 0008).** Nothing about water rendering is saved; the height map is authored content, not
  save state.
- **Performance (ADR 0005 — 60fps desktop, mobile-portable).** A small fixed set of texture samples +
  noise per pixel; **no per-frame CPU allocation** (`WaterLevelAt` is a material float set on tide
  change / slow tick, not rebuilt per frame); pooled/static materials; mind texture memory for the
  foam/caustic/specular textures. The runtime-shader-vs-bake fork is a profiled call (§7, M3).

---

## 9. Open questions (for the art pass / owning lanes)

- **Height-map authoring source + a possible Core sampler.** ADR 0009 takes `terrainElevation` as a
  caller-supplied parameter; how the world authors it per position (tile heightfield texture vs
  per-feature zones — world-and-regions §9.4, time-tides-weather §3.5) and whether shader + sim should
  share a Core-owned **per-position sampler** is a build-time call (its own additive ADR if needed).
- **Runtime shader vs 3D-water→2D bake** (art-bible §6.1, OQ2) — profiled spike in the art pass.
- **Per-region water-plane offset** — a region that offsets its water plane from raw tide overrides
  `IEnvironmentService.WaterLevelAt`; the shader reads whatever that returns (no shader change).
- **Foam band width / depth thresholds / palette ramps / caustic intensity** — art-pipeline tunables,
  exposed as material/Def values (rule 6), not hard-coded in the graph.
- **Tide→visual-cue mapping (time-tides-weather OQ1).** This doc resolves the *rendering* side
  (continuous depth gradient + depth≈0 foam band); whether discrete waterline states / wet-dry tile
  swaps are *also* wanted for non-shader fallbacks is an art-pipeline call coordinated with that doc.
