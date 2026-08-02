# Water Rendering — the layered URP water shader (recipe + the shipped first pass)

> **Status: FIRST PASS SHIPPED (greybox-real) + PAINTED-TEXTURE SLOTS.** The layered shader now exists
> as a **text URP 2D HLSL/ShaderLab shader** (NOT a Shader Graph — authored as text so it builds
> headless), wired to the deterministic sim. The §0 "Applying the shader" note below covers what shipped
> and how to use it; §2–§5 remain the layer-by-layer recipe (now describing the built layers). Colours /
> speeds / foam / thresholds are all Inspector tunables on the material — the owner art-directs the LOOK
> next; this is a solid first pass, not final polish. **The shader also accepts optional owner-painted
> TEXTURES (§10)** that blend with / override the matching procedural layer when assigned, and fall back
> to the procedural look when empty — so art-pipeline can hand-paint foam, caustics, ripple, sparkle, the
> depth-colour ramp, and whitecaps without touching the shader. Decision of record:
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
>    width/softness, specular amount/sharpness/light-dir, caustic amount/scale/depth, the pixel grid
>    (`Pixels Per Unit`, default 32), the **anti-tiling** lever (`Untile Strength`, default 0.6 — raise
>    it if the painted surface grid reads at CALM) and the **always-on beach swash** (`Swash Amplitude`
>    0.3 m, `Swash Speed` 0.5, `Swash Wavelength` 1.2, `Swash Along-Shore Vary` 0.35 — the fast in/out
>    shoreline wash that now rolls **in** from the sea, §5.6). No graph
>    editing, no code.
> 5. **Art-direct beyond procedural (optional):** drop owner-painted textures into the **Painted
>    textures** slots to override or blend with the matching procedural layer — foam shape, caustics,
>    surface ripple, sparkle, a hand-painted depth-colour ramp, whitecaps. **Every slot is empty by
>    default, so the shipped look is 100% procedural until you assign one.** Full per-slot spec
>    (suggested dims, seamless, no-AA import, what each drives, the fallback): **§10** below.
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
  swim together. Two octaves at different scroll speeds reads richer than one. **Shipped: now three
  syncopated octaves on distinct (direction, rate) — current swell along `_FlowDir`, a wind-driven chop
  along `_WindDir`, and a slow perpendicular cross-swell — so the surface follows the wind and stops
  reading as one marching grid (§5.7).**
- **Pixelize:** pixelize the noise sample coords (§3) so swell snaps to the grid.
- **Tunables:** noise scale, scroll direction + speed (**wind direction now wired — §5.7**), amplitude vs
  sea-state, octave mix (the per-octave syncopation weights, §5.7).

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

### 5.6 Anti-tiling + always-on beach swash (shipped upgrades)

Two fixes the owner asked for after seeing the painted-texture first pass live. Both are in
`HiddenHarboursWater.shader`; both expose every value as a material property (rule 6); both are
**visual-only** (no sim, no save — rule 5).

**(A) Anti-tiling of the painted slots — `_UntileStrength` (0..1, default 0.6, ON).** At a CALM
sea-state the painted **surface** tile's repeat grid reads as an obvious small square (it's hidden at
"Light"+ only because chop/flow motion masks it). The `UntileSampleW` helper breaks the grid two ways,
both dialed by `_UntileStrength`:
- **Domain warp** — the sample world-coord is nudged by the low-freq surface `ValueNoise` so straight
  tile seams bend before they're sampled (cheap, smooth).
- **IQ-style hash-untile** — per repeat-cell, the lookup is offset by a per-cell hash (`Hash22`) and two
  neighbouring offset variants are cross-faded by a smooth weight, so adjacent cells differ yet never
  show a seam (the [Inigo-Quilez "untile"](https://iquilezles.org/articles/texturerepetition/) trick,
  adapted to our point-sampled pixel grid).

It is applied to the four **scrolling** painted slots — `_SurfaceTex` (the primary fix), `_FoamTex`,
`_CausticTex`, `_SparkleTex` — and stays **pixel-art faithful**: the per-tile offset is added to the
**world** coord *before* `PaintUV` pixelizes, so the untiled lookup still snaps to the PPU grid and
remains point-sampled. `0` = the raw repeating grid; `1` = fully broken up. Cost: one extra noise eval
plus two extra texture taps per untiled slot only when `_UntileStrength > 0` — within the rule-7 budget.

**(B) Always-on beach swash — `_SwashAmplitude` (m, default 0.3), `_SwashSpeed` (default 0.5),
`_SwashWavelength` (default 1.2), `_SwashAlongShoreVary` (0..1, default 0.35).** Before this, the
**only** in/out shoreline motion was the slow deterministic tide. The swash adds a **fast, continuous,
cosmetic** waterline wash — "waves crashing in and out" — driven off `_Time` in the shader
(`BeachSwash`): a two-beat sine produces a signed **depth offset** (`±_SwashAmplitude` m) that advances
(run-up) and recedes (backwash) the wet edge.

- **The crest rolls SHOREWARD, not around the island (the rotation fix).** The original phase advanced
  along a **fixed world diagonal** (`(worldX+worldY)·_SwashScale`). On the round island's ring-shaped
  foam band a crest travelling in one compass direction sweeps *around* the ring's circumference — the
  owner saw the foam **rotate** around the island. Real run-up rolls **shoreward**, perpendicular to the
  local coast, everywhere at once. So the phase is now driven by the **shoreward coordinate**: the local
  visual `depth` (which decreases toward shore). A crest sits at constant total phase
  `θ = t·speed·2π + depth·_SwashWavelength`; holding `θ` as `t` grows forces `depth` to **shrink**, so
  each crest marches to ever-shallower water — **in** toward the beach — the same radial run-up at every
  point on the ring. The shore-normal comes from `ShoreDir()` (the baked-seabed height gradient, §11), so
  no fixed compass direction is involved. `_SwashWavelength` sets the shoreward wave spacing (crests per
  metre of depth); larger = tighter-packed run-up lines.
- **Along-shore desync, not a travelling wave.** To keep the wash from pulsing as one flat ring, a
  **small** value-noise offset (`_SwashAlongShoreVary`) sampled along the shore **tangent** (perpendicular
  to `ShoreDir`) breaks neighbouring stretches of coast slightly out of sync — organic, but it carries
  **no** single world direction, so it can never re-form a coherent wave circling the island. The
  dominant motion stays shoreward (in/out); this term is only a subtle desync. `0` = a perfectly
  in-phase ring; the default `0.35` reads natural.
- **Flat-seabed fallback.** Where `ShoreDir()` returns zero (open deep water / no height map) there is no
  shoreward axis, so the swash falls back to a **gentle time-only pulse** (no travelling term) — the wet
  edge still animates, and there is no fixed-direction sweep to circle anything.
- **Confined to the foam band (the P1 integrity rule).** The swash offset is multiplied by a band gate
  (`1 − smoothstep(0, foamWidth·2 + |amp|, depth)` — full at the wet edge, **zero** by the band reach)
  and applied **only** to a *local foam-only depth* (`foamDepth`). The real `depth` that drives
  `clip()`, the deep-water tint (`dt`), and the caustic gate is **never touched** — so deep water does
  not move and **the cosmetic wash cannot move the gameplay waterline** (it's foam *dressing* on top of
  the real depth read: saves nothing, drives no sim — rule 5). Set `_SwashAmplitude = 0` to disable.
- **SLOPE-TRUE since 2026-07-23 (the "swirly shoreline" fix).** The swash (and the ADR 0012 fringe
  wiggle `_ShoreNoise`) offsets a DEPTH, so its **visible** contour excursion used to be
  `amplitude ÷ the local beach slope` — the 0.3 m default painted a **±1.7 m swinging worm tongue** on
  the gently painted 0.18 m/m bar. Both offsets are now scaled by the LOCAL painted slope
  (`SeabedSlopeMag` — the same ±`_ShoreSampleStep` central difference `ShoreDir` reads — saturated at
  the 1 m/m authoring reference), so **the authored amplitudes read as CONTOUR metres on any coast**:
  `_SwashAmplitude`/`_ShoreNoise` now mean metres of visible wet-edge excursion; a steep (≥ 1 m/m)
  edge keeps its previous look; uniform-deep materials (no height map) have no shore and are
  untouched. Pinned in `WaterWhiteoutShoreSwirlAcceptanceTests.ShoreSwirl_CosmeticContourExcursion_IsSlopeTrue`.

The swash math has a pure-C# twin in `WaterSurface.cs` (`SwashOffset` + `SwashBandGate`) so the
oscillation, the amplitude bound, and **the band-confinement invariant** are unit-tested headless
(`Assets/Tests/EditMode/Art/ArtRenderingTests.cs`) without opening Unity — the twin feeds no sim and is
not pushed to the material; the shader owns the live wash.

> **The twin mirrors the shoreward phase term-for-term** — synced in #172 when the shader's rework
> landed. (This section used to flag the sync as an open gameplay-systems follow-up; that flag was
> stale and is retired.) `SwashOffset` carries the same base `θ = t·speed·2π + max(depth,0)·wavelength`,
> the same two beats (0.7 at full rate + 0.3 at half), the same `(sample − 0.5)·vary·2π` desync and the
> same flat-seabed fallback as `BeachSwash`; `SwashBandGate` matches the call site's
> `reach = max(foamWidth,1e-3)·2 + max(|amp|,1e-3)`. The only inputs it takes rather than computes are
> the two **GPU-only** ones — the value-noise sample along the shore tangent, and `haveShore` from
> `ShoreDir()` — so those are the only places it can drift. The headless tests pin the shoreward march's
> **direction and rate** (follow a crest's constant-phase characteristic and the same wash reappears at
> shallower depth; a sign flip rolling crests out to sea fails), the half-rate second beat (one beat does
> not repeat, two do), and the desync mapping (the mid sample shifts nothing; `vary = 0` is an in-phase
> ring). The bounded-oscillation contract is unchanged. **Not twinned:** the `shoreSlope` contour scaling
> the call site multiplies the offset by — that one is guarded only by the slope-true acceptance test
> named in the bullet above, which needs a GPU (it `Assert.Ignore`s on a Null device).

> **Retired dial:** `_SwashScale` (the old fixed-diagonal along-shore scale) is replaced by
> `_SwashWavelength` + `_SwashAlongShoreVary`. Any `_SwashScale` value serialized in `Water.mat` / the
> Water Presets is now **inert** (Unity ignores a serialized property the shader no longer declares); the
> new dials pick up their Properties-block defaults until the owner tunes them.

### 5.7 Wind direction + syncopation + FBM variance (shipped upgrade)

The owner saw the surface "stay organized in a pattern" and "march one direction." The cause was a
shader/sim split: the sim's wind **already varies direction over time** (`WeatherModel.SampleWind` —
prevailing-wander + gust veer), but the shader **discarded** wind direction — it scrolled *every*
animated layer along `_FlowDir` (the tidal **current**, a fixed axis) and used wind only as the scalar
`_Roughness`. So no matter the weather the whole sea slid down one diagonal. This upgrade makes the
surface follow the **wind** (intensity **and** direction), adds multi-rate/multi-direction wave octaves
(syncopation), and adds organic low-frequency variance (FBM) that also scatters the specular sparkles.
All of it is **visual-only** — like the beach swash, it touches only `col.rgb` / the foam dressing and
**never** `depth`, `clip()`, the deep-tint, the caustic gate, or `_WaterLevel`; it drives no sim and
saves nothing (P1 integrity, CLAUDE.md rule 5). Every constant is a material property (rule 6), and
every new octave/field is **pixelized** like the rest (pixel-art faithful, §3). The new layers default
**ON at a modest strength** so the change is visible immediately yet fully dial-able on `Water.mat`.

**(A) Wind direction is now pushed to the shader — `_WindDir`.** `WaterSurface.cs` adds
`IdWindDir = Shader.PropertyToID("_WindDir")` and, in `PushUniforms` (right after the `_FlowDir`
set-vector), pushes `WindDirection(EnvironmentSample.WindVector)` — a new pure static helper mirroring
`FlowDirection`: it normalizes the wind vector (strength is dropped here — it still drives `_Roughness`
separately) and falls back to `Vector2.up` on near-zero wind (`sqrMagnitude < 1e-6`, NaN-safe), matching
the shader's `_WindDir` default `(0,1,0,0)`. A headless EditMode test
(`WindDirection_FollowsTheWind_NormalizesAndFallsBackOnSlackWind`) covers the normalization,
strength-independence, and slack-wind fallback (alongside the existing `FlowDirection`/`SwashOffset`
tests). The runtime push is throttled like every other uniform — no per-frame cost.

**(B) Wind-driven chop octave — `WindChop`.** A new 1–2-octave value-noise field scrolled along
`normalize(_WindDir.xy)` at its **own** rate `_WindChopSpeed` and scale `_WindChopScale` — a **separate
scroll from `_FlowDir`**. This is the layer that *follows the wind*. Folded into the surface mix weighted
by `_WindChop` (0..1). Pixelized like `SurfaceNoise`.

**(C) Syncopation — `SurfaceNoise` is now 3 octaves with distinct (direction, rate).** The old two-octave
`SurfaceNoise` (both along `_FlowDir`) becomes:
- **A** = the **current swell** along `_FlowDir` @ `_Flow` (the original look — the foundation);
- **B** = the **wind chop** along `_WindDir` @ `_WindChopSpeed` (weighted `_WindChop`);
- **C** = a **slow cross-swell** along a derived **perpendicular** axis (the 90°-rotation of the average
  of flow & wind) @ `_CrossSwellSpeed` with a big `_CrossSwellScale` — or an explicit `_CrossSwellDir`
  when set (its default `(0,0)` means "auto-perpendicular").

B and C are mixed by single, clear per-octave weights (no double-counting): octave B's effective weight
is `_WindChop × _Octave2Weight` (the headline wind knob × an octave-2 fine-tune), octave C's is
`_Octave3Weight`. The blend is normalized so the result stays ~0..1 regardless of the weights. Different
directions + rates break the single-direction read at **~no extra cost** — still pure value-noise, no
textures.

**(D) FBM low-frequency variance — `Fbm` + a tint and a sparkle gate.** A new `Fbm(p, octaves)` helper
(4 octaves of `ValueNoise`, lacunarity 2, gain 0.5, each pixelized) is sampled once per pixel at a **big**
scale `_FbmScale`, slowly drifting at `_FbmDriftSpeed`, giving broad slow patches. Its 0..1 value does two
things, **both `col.rgb`-only**:
- **(i) Tint patchwork** — near the base-colour step it lerps `col.rgb` toward `_FbmTint` (strength
  `_FbmStrength`) plus a gentle brightness wobble, so the sea breaks into broad slow patches instead of an
  even sheet.
- **(ii) Specular scatter** — the specular glint is multiplied by `smoothstep(_FbmGateLo, _FbmGateHi, fbm)`
  **before** it's added, so sparkles **cluster** in organic patches instead of an even posterized lattice.
  The hard `floor(glint*4+0.5)/4` posterize is replaced by a tunable band count `_SpecBands`.

**(E) A second domain-warp octave in `UntileSampleW`.** The anti-tiling domain warp now sums a low-freq
bend **and** a finer ripple octave (still dialed by the existing `_UntileStrength`, no new knob) so the
untiled painted slots read more organic. `_UntileStrength = 0` is unchanged (raw grid).

> **Property summary (all additive — none of the owner's existing tuned values changed):**
> *wind chop* — `_WindDir` (vec, sim-driven; default `(0,1)`), `_WindChop` (0.4), `_WindChopScale` (0.7),
> `_WindChopSpeed` (0.09). *syncopation* — `_CrossSwellDir` (vec, `(0,0)`=auto-perp), `_CrossSwellSpeed`
> (0.025), `_CrossSwellScale` (0.16), `_Octave2Weight` (0.35), `_Octave3Weight` (0.3). *FBM* — `_FbmScale`
> (0.05), `_FbmDriftSpeed` (0.012), `_FbmStrength` (0.18), `_FbmTint` (pale teal), `_FbmGateLo` (0.35),
> `_FbmGateHi` (0.7), `_SpecBands` (4). To calm the look back toward the old single-direction surface, set
> `_WindChop` / `_Octave2Weight` / `_Octave3Weight` / `_FbmStrength` to 0.

### 5.8 Cohesion pass — rolling ocean swell + wind-streaked foam + flow-with-body (shipped upgrade)

The §5.7 upgrade gave the surface organic small-scale variance, but the owner noted it read as a **field
of separate specks**, not **one large body** of water — and that the foam/whitecap layers were scrolling
on a diagonal **opposite** to the surface (`float2(-t*_Flow, t*_Flow)`). This **cohesion pass** adds three
coupled layers, all **visual-only** (col.rgb / foam dressing — never `depth`, `clip()`, the deep-tint
`dt`, the caustic gate, or `_WaterLevel`; drives no sim, saves nothing — P1 integrity, CLAUDE.md rule 5),
every constant a material property (rule 6), every new field **pixelized** (decision (2)), modest defaults
**ON** so the cohesion is visible yet fully dial-able. **Everything keys off the LIVE, time-wandering sim
directions** (`_WindDir` from `WeatherModel`, `_FlowDir` from PR #95's drifting current bearing — both
already pushed by `WaterSurface.cs`), so the whole body visibly **reorients as the weather shifts** — no
hardcoded angle (the P1 "sea has moods" integrity).

**(A) Rolling ocean swell — the keystone (`SwellField`).** ONE big, **long-wavelength** swell field over
worldXY: a low-frequency directional wave (a sine **along** the swell axis, broken up by a slow value-noise
so the bands aren't ruler-straight), scrolling **slowly** along that axis. Its 0..1 crest factor modulates
the **base-colour brightness** (crests lighter, troughs darker) so broad light/dark **bands roll across the
WHOLE surface** — the §5.7 small variance rides on top, and the sea reads as **one connected body**. The
swell **direction defaults to the (wandering) wind** (`SwellDir()` — wind generates swell), with an optional
`_OceanSwellDir` override (`(0,0)` = auto-from-`_WindDir`), so the bands reorient as the wind veers. The
same field is **reused** below (crest-gate the whitecaps, bias the specular) so foam, glint and brightness
all ride the **same** swell.

**(B) Wind-streaked foam (wind rows).** The open-water whitecap speckle is now **anisotropic** — sampled on
a coordinate **compressed perpendicular to `_WindDir`** (a wind-aligned basis: along-wind axis kept,
cross-wind axis multiplied by `_FoamStreakStretch`) so a round noise cell **elongates into a long thin
streak ALONG the wind** instead of isotropic speckle. The existing wind/roughness gating (the `_Roughness`
threshold + the deep-water `dt` gate) is unchanged.

**(C) Couple everything to the swell + flow together.**
> - **Whitecaps ride the crests.** The cap mask is gated by the swell field's high values
>   (`_FoamCrestGate`: 0 = even, 1 = crest-only) so foam preferentially appears on swell **crests**.
> - **Specular leans to the lit swell faces.** The glint is multiplied by a swell-crest term
>   (`_SpecSwellBias`) before it's added, so sparkles ride the same bands the cohesion brightness does
>   (one body catching one sun — still the §3.5.1 single implied light).
> - **Foam now flows WITH the body (the opposite-motion fix).** The foam churn + whitecap scroll's old fixed
>   counter-diagonal `float2(-t*_Flow, t*_Flow)` is **replaced** by a drift along `FoamDriftDir()` — a
>   **blend of the wind (`_WindDir`) and the tidal current (`_FlowDir`)**, dialed by `_FoamDriftWindVsCurrent`
>   (0 = current-led, 1 = wind-led). Both axes are sim-driven and wander over time, so the foam flows with
>   the one connected surface and reorients with the weather, instead of scrolling against it.

The swell-direction and foam-drift **direction logic** has pure C# twins in `WaterSurface.cs`
(`SwellDirection`, `FoamDriftDirection`) — **not pushed** to the material (the shader derives the live
versions from the already-pushed `_WindDir`/`_FlowDir`; **no new uniform**), unit-tested headless
(`ArtRenderingTests.cs`) for the auto-from-wind default, the override-wins rule, the wind/current blend, and
the NaN-safe fallbacks — the determinism guard for the cohesion reorientation.

> **Property summary (all additive — none of the owner's existing tuned values changed):**
> *ocean swell* — `_OceanSwellDir` (vec, `(0,0)` = auto-from-`_WindDir`), `_OceanSwellScale` (0.025, SMALL =
> long wavelength), `_OceanSwellSpeed` (0.018), `_OceanSwellStrength` (0.16), `_OceanSwellSharpness` (2.2 —
> raised from 1.4 so the crest brightness reads as a defined ridge, matching the wave field's own sharpening).
> *foam streaks* — `_FoamStreakStretch` (3.5; 1 = round, higher = longer streaks). *coupling* —
> `_FoamCrestGate` (0.6), `_SpecSwellBias` (0.35), `_FoamDriftWindVsCurrent` (0.6). To dissolve the cohesion
> back toward the §5.7 look, set `_OceanSwellStrength` / `_FoamCrestGate` / `_SpecSwellBias` to 0 and
> `_FoamStreakStretch` to 1.

### 5.9 Living foam — an evolving field + a soft (metaball) threshold (shipped upgrade)

The owner saw the open-water whitecaps (and the foam-fringe churn) read as a **repeating pattern** whose
shapes **never change**: the foam was a **fixed-shape noise stamp that only TRANSLATED** across the surface
(one `ValueNoise` sample scrolled by `capDrift`/`foamDrift`), masked by a **hard `step()`**. A sliding stamp
+ a hard cut is a sliding repeat by construction. This pass makes the foam **EVOLVE, not just translate**:
patches **MERGE**, **SEPARATE**, and **CHANGE SHAPE** over time, and the residual painted-tile repeat is
killed. Like every prior addendum it is **visual-only** — it touches only `col.rgb` / `col.a` (the foam
blend) and **never** `depth`, `clip()`, the deep-tint, the caustic gate, or `_WaterLevel`; it drives no sim
and saves nothing (P1 integrity, CLAUDE.md rule 5). Every constant is a material property (rule 6) and every
new field is **pixelized** (decision (2)), defaults **ON at a modest strength**, fully dial-able on `Water.mat`.

**(A) The evolving FIELD (`EvolvingField`) — the field morphs in place.** A new pseudo-3D value-noise helper
replaces the single translating `ValueNoise` for both the whitecaps and the fringe churn. It is built by
**blending two time-offset `ValueNoise` samples of the SAME coord, where the mix itself animates** — as the
mix sweeps, a local maximum from one sample fades while a (differently-placed) maximum from the other rises,
so bright spots **appear, grow, drift, shrink and vanish**: the field MORPHS instead of sliding rigidly. Two
such "boil" pairs run half a step out of phase (a smoothed crossfade) so the morph is **continuous and
seamless** (no popping when a pair re-randomizes at a step boundary). The existing **wind+current drift**
(`FoamDriftDir()`, blended by `_FoamDriftWindVsCurrent`) is layered ON TOP — the foam still **travels with
the weather**; the in-place evolution is *added* to that drift, not a replacement. `_FoamEvolveSpeed` sets
the boil rate (0 = frozen shapes, just drift); `_FoamBlobScale` sets the blob size (smaller = bigger blobs).
Pure value-noise + pixelize, a few extra taps — within the rule-7 budget.

**(B) MERGE / SEPARATE via a SOFT THRESHOLD.** The foam mask is now
`smoothstep(_FoamThreshold − _FoamThresholdSoft, _FoamThreshold + _FoamThresholdSoft, field)` — **not** a hard
`step`. This soft band is the metaball mechanism: when two field maxima grow toward each other the **valley
between them rises** above `thr − soft` and the blobs **MERGE**; when the field **dips** below between them
they **SEPARATE**; and a maximum rising through / falling back across the band **fades a blob IN / OUT** — so
the foam reads as organic, connected, living patches rather than a binary speckle. The wind-roughness still
lowers the cap threshold (rougher ⇒ more sea above the threshold ⇒ more caps), the **swell-crest gate**
(`_FoamCrestGate`) still lifts caps onto crests, and the **wind-streak stretch** (`_FoamStreakStretch`) still
compresses the field coord perpendicular to the wind so the morphing blobs **elongate into streaks ALONG the
wind**. All three keep working *on top of* the evolving field + soft threshold.

**(C) Kill the residual REPEAT (painted whitecap tile).** The procedural `ValueNoise` is hash-based
(effectively non-tiling), so the procedural foam never tiled — but the painted **`_WhitecapTex`** slot
(`_UseWhitecapTex` ON) was sampled through a **plain `PaintUV`** (the only scrolling painted slot that
*skipped* the anti-tiling path), so its small seamless tile's **repeat grid** could read as the periodic
culprit. It is now routed through the existing **`UntileSampleW`** (IQ-style hash-untile + domain warp,
dialed by `_UntileStrength`), exactly like `_SurfaceTex`/`_FoamTex`/`_CausticTex`/`_SparkleTex` — kept
pixel-snapped. If a repeat still reads, raise `_UntileStrength` or lower `_WhitecapTexStrength`.

> **Why no C# uniform.** The evolving field and the soft threshold are derived **in-shader** off `_Time` and
> the already-pushed `_WindDir`/`_FlowDir` — **no new uniform**, `WaterSurface.cs` pushes nothing new. The
> GPU value-noise can't be unit-tested headless, but the **soft-threshold math** — the part that produces the
> merge/separate behaviour — has pure C# twins (`WaterSurface.FoamSoftThreshold` + a general `Smoothstep`),
> unit-tested in `ArtRenderingTests.cs` (the soft band is partial coverage not a 0/1 step; monotonic in the
> field; a risen valley between two maxima fills in = MERGE, a low valley reads bare = SEPARATE). The CI
> shader-compile guard (`WaterShaderCompileGuardTests.cs`) continues to force-compile the shipped `Water.mat`
> variant: no `+` in any `[Header]`/property string, no `[unroll]` over a runtime bound (the magenta class
> stays guarded).

> **Property summary (all additive — none of the owner's existing tuned values changed):**
> *living foam* — `_FoamEvolveSpeed` (0.25, boil/morph rate; 0 = frozen shapes), `_FoamBlobScale` (2.2, blob
> size; smaller = bigger blobs), `_FoamThreshold` (0.55, soft-threshold level; higher = less foam),
> `_FoamThresholdSoft` (0.18, the merge/separate softness band). The painted whitecap de-tile reuses the
> existing `_UntileStrength` (no new knob). To revert toward the old translating-stamp look, set
> `_FoamEvolveSpeed` to 0 (shapes stop morphing, foam only drifts) and `_FoamThresholdSoft` small (toward a
> hard edge).

### 5.10 Flow momentum — the water has MASS (shipped upgrade)

PR #95/#96 made the sim's **wind and tidal-current directions WANDER over time** (a deterministic drift,
P1 "sea has moods"). `WaterSurface.cs` pushes those live directions to the shader (`_FlowDir`/`_WindDir`,
§5.7), and the shader scrolls **every** wind/current-driven layer along them — so the moment the sim's
heading shifted, the surface motion **SNAPPED** to the new direction. The owner's note: *"when the water
changes direction of movement it shouldn't be instantaneous — it needs time to slow and change direction
from the newly applied force"* (water has mass).

This upgrade gives the pushed flow a **damped response** so the VISUAL surface motion **eases** toward the
live sim instead of snapping — decelerating through a heading change and accelerating out of it (momentum).
It lives **entirely in `WaterSurface.cs`** — **no shader change, no material property** (it's how the
uniforms are *fed*, not a new layer):

- **Smoothed vectors (the mechanism).** `WaterSurface` keeps persistent `Vector2` **smoothed twins** of the
  live `EnvironmentSample.CurrentVector` / `WindVector`. Each throttled push eases them toward the real sim
  vectors via frame-rate-independent **exponential smoothing**
  (`smoothed += (target − smoothed)·(1 − exp(−dt/τ))`, the pure static `SmoothVectorToward`), and **ALL**
  pushed uniforms are derived from the SMOOTHED vectors (`_Flow`/`_FlowDir` from smoothed current;
  `_WindDir`/`_Roughness` from smoothed wind — reusing the existing `FlowSpeed`/`FlowDirection`/
  `WindDirection`/`Roughness` helpers). So **every** wind/current-driven layer — current scroll, wind chop,
  rolling swell, foam streaks, foam drift — inherits the **same** momentum: the whole body eases round
  together (cohesive), not layer-by-layer.
- **Why smooth the VECTOR (not heading + magnitude apart).** When the flow reverses heading, the smoothed
  vector travels THROUGH a low-magnitude region as it rotates, so the surface **speed dips** mid-turn and
  recovers — *"slows, turns, then speeds back up"* for free. Smoothing heading and magnitude separately
  would hold the speed flat through the turn (the very snap we're removing).
- **One tunable (rule 6) — `Flow Response Time`** (`_flowResponseTime`, seconds, **default 3**). The time
  constant τ: heavier (larger) = more sluggish inertia; lighter (smaller) = livelier/snappier; **0 = no
  smoothing** (instant snap, the old behaviour). It is a **`WaterSurface` serialized field, NOT a material
  property** — the knob is on the component, tuned in the Inspector with no builder re-run. Frame-rate AND
  refresh-rate independent (the smoothing law composes, so the look doesn't change with `_refreshHz`).
- **Presentation only (rule 5).** This smooths the **visual** uniforms; it does **not** change the
  deterministic sim. The boat physics still read the **real** `EnvironmentSample` directly — only what the
  player SEES lags the sim slightly, and **that lag IS the momentum**. It saves nothing and feeds no
  simulation, exactly like the §5.6–§5.9 cosmetic layers.

The smoothing law has a pure-C# twin tested headless (`SmoothVectorToward` in `ArtRenderingTests.cs`): it
eases toward a steady target, the magnitude **dips below both endpoints on a reversal** (the slows-through-
the-turn property), it is **frame-rate independent** (sub-stepping reaches the same end state), and it is
deterministic — the guards for the momentum feel without opening Unity.

### 5.11 Foam density + whitecap lifecycle — dense solid core, milky edge, born-on-the-crest (shipped upgrade)

The §5.9 living-foam pass gave the foam a **soft (metaball) threshold** so blobs merge/separate — but the
owner saw it read **MILKY EVERYWHERE**, losing the **dense, solid-white** whitecaps the painted `_FoamTex`
(`_UseFoamTex` ON) used to give. The milky look is right for **calm / dissipating** foam, but a
**building / rough** sea needs solid density. The owner also wanted a natural **wave lifecycle**: foam
**forms** as waves build → peaks into dense **whitecaps** → **collapses** / dissipates. This pass is
**additive on #100/#101** — it keeps their merge/separate + the milky soft fade as the **LIGHT/dissipating
end**, and adds (1) a **dual-zone density** (solid-white core + milky edge), (2) **condition-driven density**
(sea-state widens/solidifies the foam), and (3) the **form→whitecap→collapse lifecycle**. Like every prior
addendum it is **visual-only** — it touches only `col.rgb` / `col.a` and **never** `depth`, `clip()`, the
deep-tint, the caustic gate, or `_WaterLevel`; it drives no sim and saves nothing (P1 integrity, CLAUDE.md
rule 5). Every constant is a material property (rule 6), every field stays **pixelized** (decision (2)), and
the new levers default **ON at a modest strength** so the change reads immediately yet dials fully back.

**(A) Dual-zone density — a SOLID-WHITE CORE + a milky soft edge (`SolidCore`, `FoamDensity`).** The #101
mask was a single `smoothstep(thr − soft, thr + soft, field)` — a smooth ramp, so even a field maximum only
reached partial coverage = milky. This pass keeps that smoothstep as the **milky band near the threshold
boundary**, but adds a **solid core**: where the evolving field is **WELL above** the threshold (above a new
`_FoamSolidThreshold`, which sits **above** `_FoamThreshold`), the foam coverage is lifted to **full
opacity** — `coverage = lerp(milky, 1, SolidCore(field))` — so the painted solid-white `_FoamTex` shows
through at the **dense heart**, with the milky smoothstep surviving only at the **soft edge**. Result: a
dense solid heart with soft milky edges, not milky-everywhere. Applied to **both** the shoreline foam fringe
and the open-water whitecaps.

**(B) Condition-driven density — calm sparse/milky, rough dense/solid/widespread (`FoamDensity`).** A master
`_FoamDensity` is **raised by wind** via `_FoamDensityWind` (× the existing `_Roughness`, which `WaterSurface`
already drives from the sim wind): `density = saturate(_FoamDensity + _Roughness · _FoamDensityWind)`. Density
both **lifts** the solid-core opacity and **widens** the solid zone (it slides the effective solid level
**down toward** the threshold as the sea roughens, so more of the field reads solid). So **CALM → sparse +
milky** (the #101 end) and **ROUGH → dense, solid, widespread whitecaps**, automatically, as the weather
shifts — the owner's "milky for some conditions, dense for others" with **no manual retuning**.

**(C) Wave lifecycle — form → peak → collapse, keyed off the swell crest (`WhitecapLifecycle`).** The
open-water whitecaps are tied to the **rolling-swell crest factor** (`SwellField`, §5.8 — reused, no new
field). A whitecap is **BORN dense & solid on the breaking crest** (a sharp break band at the crest top,
narrowed by `_WhitecapFormSharpness`, at `_WhitecapPeakDensity` opacity — which also **replaces the old hard
`0.6` cap-opacity ceiling**), then **AGES into milky residual** as the crest passes
(`crest^_WhitecapCollapseRate` decays the solid lift; faster rate = more milky residual off-crest), the
residual **spreading downwind** through the existing wind-streaked aniso coord (`_FoamStreakStretch`). Off the
crest / in the trough the solid lift fades to nothing and **only the milky soft mask remains** — exactly the
dissipating look #101 nailed. This is a **separate axis** from the existing `_FoamCrestGate` (which gates
*where* caps appear): the lifecycle shapes *how dense/solid* they are across their life stage.

> **Why no C# uniform.** The dual-zone core, the density coupling, and the lifecycle are all derived
> **in-shader** off the already-pushed `_Roughness`/`_WindDir`/`_FlowDir` + `_Time` — **no new uniform**,
> `WaterSurface.cs` pushes nothing new and is untouched. The evolving foam FIELD is GPU value-noise (not
> unit-testable headless), but the three shaping functions — `FoamDensity` / `SolidCore` /
> `WhitecapLifecycle` — are pure functions of the uniforms + the crest factor, mirrored as C# twins and
> unit-tested headless (`Assets/Tests/EditMode/Art/FoamDensityLifecycleTests.cs`): wind raises density and
> saturates; the solid core is 0 near the threshold and 1 well above it yet **always keeps a milky band**
> (the dual zone, never all-milky nor all-solid); density **widens** the solid zone; the lifecycle **peaks on
> the breaking crest and collapses in the trough**, ages monotonically off-crest, and **density gates the
> whole solid look** (calm = milky everywhere, rough = dense crests). The CI shader-compile guard
> (`WaterShaderCompileGuardTests.cs`) continues to force-compile the shipped `Water.mat` variant: no `+` in
> any `[Header]`/property string, no `[unroll]` over a runtime bound (the magenta class stays guarded).

> **Property summary (all additive — none of the owner's existing tuned values changed):**
> *density* — `_FoamSolidThreshold` (0.78, the field level above the soft band that reads SOLID; sits above
> `_FoamThreshold`), `_FoamDensity` (0.6, master), `_FoamDensityWind` (0.5, wind→density coupling).
> *lifecycle* — `_WhitecapFormSharpness` (0.5, how abruptly foam breaks at the crest), `_WhitecapPeakDensity`
> (0.95, newborn-crest opacity, replaces the old hard 0.6), `_WhitecapCollapseRate` (1.5, how fast it ages to
> milky off-crest). To revert toward the #101 milky-everywhere look, set `_FoamDensity` to 0 and
> `_WhitecapPeakDensity` to ~0.6 (the old ceiling); the merge/separate soft mask is then the whole look again.

### 5.12 Shoreward swell + foam bias — waves roll IN near the coast (shipped upgrade)

The owner saw the sea's surface artifacts / movement / foam appear to **ORIGINATE AT THE SHORELINE and travel
OUTWARD to sea** — "foam blowing out of the sand." It reads unnatural: a real ocean's swell and foam roll
**INWARD** toward the shore. **Root cause (a shader/sim direction split):** the cohesion pass (§5.8) keyed the
rolling **swell** axis (`SwellDir()`) and the **foam-drift** axis (`FoamDriftDir()`) off the **wind** (and the
tidal current) — both **wander over time** (the P1 "sea has moods" sim), and the wind blows **offshore part of
the time**. When the wind pointed land→sea, the swell crest bands and the near-shore foam streamed **OUT** from
the beach. Real swell is generated far offshore and propagates **shoreward regardless of the local wind**; foam
at the wet edge runs **up** the beach and recedes, it does not stream seaward.

The fix derives a per-pixel **shoreward direction from the seabed height map** the shader already samples, and
**biases** the swell + foam direction toward it **near the coast**, fading back to the wind/current direction in
deep water (the open sea keeps its §5.8 wind-driven cohesion). Like every prior addendum it is **visual-only** —
it steers only the swell-brightness bands + the foam/whitecap **dressing**, and **never** touches `depth`,
`clip()`, the deep tint, the caustic gate, or `_WaterLevel`; it drives no sim and saves nothing (P1 integrity,
CLAUDE.md rule 5). Every constant is a material property (rule 6), the gradient sampling stays **pixelized**
(decision (2)), and the bias defaults **ON at a modest strength** so the roll-in reads immediately yet dials
fully back. **Crucially the open sea is unchanged** — the bias fades to nothing past the falloff depth, so out at
sea the swell/foam still follow the wandering wind/current (the §5.8 cohesion). The wind may still scatter
chop/spray on top (§5.7) — this only stops it dragging the **wave trains + foam offshore near the beach**.

- **Shore direction from the height gradient (`ShoreDir`)** — the seabed elevation **rises toward land**, so the
  **gradient** of the elevation points toward shallower water = **toward the shore**. `ShoreDir(worldXY)` samples
  the baked `_HeightTex` (via `SeabedElevation`) at `± _ShoreSampleStep` metres on each axis (a central
  difference) and normalizes. It returns `(0,0)` on a flat seabed / when no height map is baked, so a region with
  no `TidalTerrain` (the open-water fallback) keeps the pure wind/current direction — **no behaviour change
  there**. Reads the **same** height map the depth/foam already use (one source of truth); it is a **visual
  direction only** — the gradient never feeds `depth`/`clip` (P1).
- **Near-shore weight (`ShorewardWeight`)** — **full** (= `_ShorewardBias`) at the wet edge (`depth ≈ 0`), fading
  smoothly to **0** by `_ShorewardFalloff` metres deep. So waves/foam roll in near the coast and the open sea is
  untouched. `_ShorewardBias = 0` disables it everywhere (the old wind-led behaviour).
- **Bias the swell + foam axes (`BiasTowardShore`)** — `SwellDir()` and `FoamDriftDir()` now `lerp` their existing
  wind/current axis toward `ShoreDir` by the near-shore weight, re-normalized (NaN-safe; a zero shore direction or
  zero weight returns the base axis unchanged). `SwellField` and both foam-drift call sites pass the per-pixel
  `depth`, so the crest **bands advance toward the beach** and the foam **runs up the shore** near the coast.

> **Why a C# twin (but not the gradient).** The height-gradient sampling is **GPU-side** (it reads `_HeightTex`)
> and can't be evaluated headless — no C# mirror, as expected. But the **direction-blend + the near-shore
> weight** — the part that decides whether waves roll IN — are pure functions with C# twins
> (`WaterSurface.ShorewardWeight` + `WaterSurface.BiasTowardShore`), unit-tested headless
> (`ArtRenderingTests.cs`): the weight is full at the edge, zero past the falloff, monotonic non-increasing, and
> bias-0/zero-falloff safe; the blend steers toward the shore by the weight, keeps the base axis when there is no
> shore direction (open water), and is NaN-safe on opposed directions. The CI shader-compile guard
> (`WaterShaderCompileGuardTests.cs`) continues to force-compile the shipped `Water.mat` variant: no `+` in any
> `[Header]`/property string, no `[unroll]` over a runtime bound (the magenta class stays guarded).

> **Property summary (all additive — none of the owner's existing tuned values changed):**
> *shoreward bias* — `_ShorewardBias` (0.7, master strength; 0 = old wind-led behaviour), `_ShorewardFalloff`
> (2.5 m, the depth over which the bias fades from full at the wet edge to none in deep water), `_ShoreSampleStep`
> (0.4 m, the world step the height gradient is sampled over; larger = a smoother/broader shore direction). To
> turn the roll-in OFF (back to the §5.8 pure wind/current cohesion everywhere), set `_ShorewardBias` to 0.

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

---

## 10. Owner-painted texture slots (art-direct beyond procedural)

> **Status: the owner's six hand-painted tiles are now IMPORTED + ASSIGNED.** They live at
> `Assets/_Project/Art/Textures/Water/` (`Foam.png`, `Caustics.png`, `SurfaceRipple.png`,
> `Whitecaps.png`, `Sparkle.png` 32×32, `DepthRamp.png` 256×8) and are wired into every matching slot
> on `Water.mat` with their `_Use…` toggles **ON** and each strength at the visible default `1`
> (`_PaintScale` 0.25 / `_SparkleTexScale` 0.5 left at defaults). They import as **Default** textures
> (not Sprite): **Point** filter, **no compression**, **mipmaps off** — **Repeat** wrap for the five
> seamless tiles, **Clamp** for the 1-D `DepthRamp`; **sRGB on** for the colour ramp, **off** for the
> five grayscale/mask tiles (per the import table below). So the default `Water.mat` now renders the
> owner's painted look; every strength remains a tunable to dial back toward the procedural fallback.

The shader's first pass draws every layer **procedurally** (value-noise + math) so it ships with no art
dependency. To let the owner/art-pipeline **art-direct the exact look**, the shader exposes **six
optional texture slots** on `Water.mat`. Each one **blends with or overrides the matching procedural
layer when assigned**, and **falls back to the shipped procedural look when the slot is empty** — so the
default material (every slot empty, every toggle off) renders *exactly* the first pass, unchanged.

**How the fallback works.** Each slot is paired with a `Use…` toggle (a shader keyword). The material
ships with all toggles **off** and all slots **empty**, so the procedural branch runs. To use a slot:
**assign the texture *and* tick its `Use…` toggle** in the Inspector. (Assigning a texture without
ticking the toggle does nothing — the toggle is the on-switch; this keeps the procedural path the
guaranteed default.) A per-slot **strength/blend** `[0..1]` then dials procedural ↔ painted
(`0` = pure procedural, `1` = fully painted), except `_DepthRamp`, which is a hard replace when on.

**Universal import settings for every slot** (so painted detail stays on-look):

| Setting | Value | Why |
|---|---|---|
| **Filter Mode** | **Point (no filter)** | no bilinear AA — keeps the pixel-art read (LOCKED §3) |
| **Wrap Mode** | **Repeat** | a small seamless tile covers the whole sea plane |
| **Compression** | None (or high-quality) | avoid block-artefacts on tiny tiles |
| **sRGB** | **on** for `_DepthRamp` (it's colour); **off** for the grayscale/mask tiles | masks are data, not colour |
| **Alpha** | keep for the white-on-*transparent* tiles (foam, whitecap) | coverage comes from alpha |

All slots are sampled on the **pixelized world grid** (PPU-snapped, like every procedural layer) and
**tiled by `_PaintScale`** (tiles/unit; sparkle has its own finer `_SparkleTexScale`). Time-animated
layers (surface, caustics, sparkle, foam, whitecap) **scroll the painted tile with the current**, so a
single static tile still "swims" — no flip-book frames needed.

### The six slots

| Slot (material property) | Drives / blends into | On-switch + strength | Suggested authoring | Procedural fallback (slot empty) |
|---|---|---|---|---|
| **`_SurfaceTex`** | the layer-2 surface ripple/wave detail — augments **or replaces** the procedural scrolling value-noise that produces swell + the surface tint + the foam/spec coords | `_UseSurfaceTex` · `_SurfaceTexStrength` | **~64×64**, **seamless**, **grayscale** (mid-grey ≈ flat; light/dark = crest/trough) | the two-octave scrolling value-noise (`SurfaceNoise`) |
| **`_FoamTex`** | the layer-3 foam fringe pattern, **masked to the waterline/shallows** (the depth≈0 band) — the painted shape breaks the foam line in place of the procedural churn | `_UseFoamTex` · `_FoamTexStrength` | **~64×64**, **seamless**, **white-on-transparent** (alpha = foam coverage; opaque tiles fall back to luminance) | the value-noise `churn` term inside the foam band |
| **`_CausticTex`** | the layer-5 caustics, **distorted by time** (two counter-scrolling samples) and **depth-gated to the shallows** — painted light-veins over the visible seabed | `_UseCausticTex` · `_CausticTexStrength` | **~64×64**, **seamless**, **grayscale** (bright = caustic vein) | the ridged dual-value-noise caustic, same shallow gate |
| **`_SparkleTex`** | the layer-4 specular glint pattern — replaces/blends the procedural posterized glint, still **gated by the implied-sun facing** (one-sun discipline, ADR 0006) | `_UseSparkleTex` · `_SparkleTexStrength` (+ `_SparkleTexScale`) | **~32×32**, **seamless**, **white-on-black** (white = a glint dot) | the noise-gradient facing glint, posterized to pixel sparkles |
| **`_DepthRamp`** | the layer-1 depth **colour** — a **1-D shallow→deep ramp** sampled by depth (`u=0` shallow → `u=1` deep). When assigned it **drives the depth colour instead of** the `_ShallowColor`/`_DeepColor` lerp (a hard replace; the depth-band posterization still applies *before* the lookup) | `_UseDepthRamp` (no strength — hard replace) | **64×1** or **256×1** (1px tall), **sRGB colour**, shallow at the **left** (`u=0`); alpha in the ramp drives water opacity too | the `lerp(_ShallowColor, _DeepColor, dt)` two-colour gradient |
| **`_WhitecapTex`** | the open-water, wind-driven whitecap pattern — coverage **scaled by the `_Roughness` (wind) uniform** and gated to deeper water, blended over the procedural speckle | `_UseWhitecapTex` · `_WhitecapTexStrength` | **~64×64**, **seamless**, **white-on-transparent** (alpha = cap coverage) | the wind-thresholded value-noise speckle |

> **Notes.**
> - `_PaintScale` (default `0.25` tiles/unit) sets how large the painted tiles read on the sea for all
>   slots except sparkle, which uses `_SparkleTexScale` (default `0.5`, finer). Both are tunables, not
>   hard-coded (rule 6).
> - **`_UntileStrength` (0..1, default `0.6`, ON)** breaks up the painted tiles' repeat grid (visible at
>   CALM) for the four scrolling slots — `_SurfaceTex`, `_FoamTex`, `_CausticTex`, `_SparkleTex` — via an
>   IQ-style hash-untile + domain warp, kept pixel-snapped. `0` = the raw grid; raise it until the tile
>   square stops reading. See §5.6(A).
> - Slots blend **in their own layer only** — e.g. a painted foam tile still appears *only* in the
>   depth≈0 band, painted caustics still fade out into deep water. The owner paints the *texture*; the
>   shader keeps the *placement* tied to the tide-truth (the P1 integrity rule — render and sim still
>   read one height map). A painted tile cannot move the waterline.
> - **Determinism & save (rule 5) are unaffected:** these are read-only authored textures sampled for
>   *visuals only*; they feed no simulation, influence no walkability/grounding, and enter no save —
>   exactly like the procedural look they replace.

### Ownership

Per [`../../agents/coordination.md`](../../agents/coordination.md) §1.1 ("Water/fog/lighting"):
**lead-architect** owns the **slot plumbing** (the properties, keywords, sampling, blend math — this
section); **art-pipeline** owns the **textures** (painting the seamless tiles + ramp to the §4 palette
and tuning the strengths). The slots are the seam where the two lanes meet — author the tiles to the
import table above and tune together.

---

## 11. Painted seabed-height authoring (ADR 0014) — hand-paint the §4 height map

The §4 height map (the *single source of truth* for render + walkability + boat-cross) can be authored
**two ways**, and they feed the **exact same** `depth = waterLevel − terrainHeight` equation:

1. **Analytic zones** (`World.TidalTerrain`) — elevation composed in code from a few blended zones
   (island / sandbar / channel / deep). The shipped St Peters default.
2. **A hand-painted height map** (`World.PaintedHeightMap`, ADR 0014) — the owner paints elevation with
   the **Terrain Paint Tool (height + look)** (`Hidden Harbours ▸ Tools ▸ Terrain Paint Tool (height +
   look)` — renamed from "Seabed Paint Tool"). The painted texture's R channel encodes elevation over a
   world rect + min/max range — **the same `_HeightTex` / `_HeightWorldMin` / `_HeightWorldSize` /
   `_HeightMin` / `_HeightMax` this shader already samples** (§5.1 `SeabedElevation`).

**Paint a terrain TYPE — look + height in ONE stroke.** The tool's headline brush paints a tunable terrain
*type* (Deep / Channel / Beach / Sandbar / Grass / Cliff): one stroke (a) sets the height-map cells to the
type's elevation AND (b) stamps the type's ground **tile** on the scene's ground tilemap (underwater types —
Deep / Channel — paint no tile and CLEAR any there, so the water shows). The **height side stays the single
source of truth** for water + tide (this section is unchanged by the type brush); the tile is authored
*visual* content, like normal Tile-Palette painting, never sim. A toggleable **edit-mode height colour
overlay** (deep blue → cyan → sand → green → rock, with a legend + the preview waterline) lets the owner SEE
the elevation he's shaping — a designer aid drawn ONLY in the Scene view that never serializes and never
renders in Play or a build (`World.TerrainHeightPalette` owns the pure ramp).

**One map, both consumers, no drift.** The painted texture is **CPU-readable**, so the sim decodes it once
into a cached `float[]` (`PaintedHeightField`, sampled by `PaintedTidalTerrain : ITidalTerrain`) using the
**identical** world→uv bilinear mapping this shader uses; the render feeds the **same texture** straight to
`_HeightTex` (`WaterSurface.DepthSource.PaintedHeightMap` — no re-bake). So the visible depth and the
gameplay depth come from the same bytes — the one-height-map / three-consumers rule (§4) holds by
construction. Painting forks neither a "visual seabed" nor a "physics seabed" — the exact drift §4 / ADR
0009 forbid.

**See the coast WHILE editing (the headline UX).** `WaterSurface` is `[ExecuteAlways]` with a serialized
**preview tide level**: in the Scene view (not playing) it drives `_WaterLevel` so the painted coast is
visible — land dry, the bar baring, the channel flooded — and a slider scrubs any tide WITHOUT pressing
Play. Presentation only (feeds no sim, saves nothing — rule 5); at runtime the live `WaterLevelAt(t)`
overrides it.

**Seed from today's coast.** "Export analytic St Peters → painted map" samples the shipped `TidalTerrain`
zones into a painted map (committed seed: `Assets/_Project/Data/Terrain/StPetersSeabed.asset`), so the owner
paints **from** the existing coast. Adopting the painted map (swap the scene's `TidalTerrain` for a
`PaintedTidalTerrain` + point `WaterSurface` at it) is an **explicit** step — the shipped St Peters look is
not silently changed.

**Determinism & save:** the painted map is **authored DATA committed like a tilemap** — read at runtime,
never written at runtime, no RNG; the tide is still recomputed from `(worldSeed, gameTime)` and nothing new
is saved (rule 5; ADR 0014).

---

## 11. Sky reflections — strong + sharp on CALM water, gone in a storm (shipped upgrade)

A reflection layer shipped on `HiddenHarboursWater.shader`: the sea now reflects the **sky**. On
**CALM / glassy** water it adds a clean, mirror-like sheen — the **current sky colour** smeared down the
surface plus a **brighter sun streak/glitter** sitting toward the sun. As the **sea-state** rises the
reflection **breaks up** (smears/scatters across the chop) and **fades**, reaching **~0 by a tunable
sea-state** (a storm doesn't mirror). So **calm → strong + sharp**, **lively → broken + dim**, **gale →
gone**. It serves **P1 ("The Sea Has Moods")**: the reflection *is* a sea-state tell.

It is a **faked, single-pass, in-shader** reflection — **NO reflection camera, NO extra render pass**
(those need wiring we can't verify and would blow the rule-7 perf budget). The "reflection" is the sky
colour stamped down the surface as a stylized **vertical-ish band** (the pixel-art cue for a mirror) plus a
sun-aligned glitter streak — pixelized on the PPU grid like every other layer (§3).

Like every prior addendum it is **visual-only** — it adds to `col.rgb` like every other water layer and
**never** touches `depth`, `clip()`, the deep tint, the caustic gate, or `_WaterLevel`; it drives no sim
and saves nothing (P1 integrity, CLAUDE.md rule 5). Every constant is a material property (rule 6). It is
composited **after** the caustics + specular (the mirror sits over them) but **before** the foam (so
whitecaps/fringe read on top of the reflection). **It defaults ON at a modest strength** so it reads
immediately, yet `_ReflectionStrength = 0` returns the exact pre-feature look.

### 11.1 Sea-state drives everything (NO new C# uniform)

The calm↔stormy behaviour is read **entirely from the sea-state uniforms `WaterSurface.cs` already
pushes** — `_Chop` (0 = glass .. 1 = storm; set from `Choppiness(SeaState)`) and `_Roughness` (the wind
whitecap scalar). **No new uniform push; `WaterSurface.cs` is untouched.** Two in-shader curves shape it,
each a pure function of those uniforms:

- **Strength** (`ReflectionStrength()`): `1` on glass, faded by `1 − smoothstep(0, _ReflectionFadeChop,
  _Chop)` to `0` by the fade-out sea-state, **further dimmed** by wind (`1 − _Roughness·_ReflectionWindFade`),
  scaled by the master `_ReflectionStrength`. So a storm (or master 0) yields no reflection.
- **Sharpness** (`ReflectionSharpness()`): `1` (a clean mirror) at calm, falling toward `0` (smeared)
  against a combined agitation `_Chop·_ReflectionChopScatter + _Roughness·_ReflectionWindScatter`. The
  shader uses it to **widen the vertical smear** (a sharp mirror is a tight band; a smeared one is broad)
  and to **broaden the sun streak**.

### 11.2 The reflection reflects the CURRENT sky (day/night)

The reflected sky colour is the **day/night `_DayNightTint` global** (the same sky/scene colour the
DayNightController multiplies the frame by, ADR 0013) so the mirror reads **warm at dusk, dark at night,
bright at noon** — dialed by `_ReflectionSkyTint` against the material's `_ReflectionColor` base. The
**sun streak** sits toward `_SunDir` (the same global the specular uses) and **fades out as the sun sets**
(`_SunElevation`). When the day/night cycle is **not running** (the global defaults to near-black /
`_SunDir == 0` — e.g. a bare art scene or editor preview) the layer falls back to the authored
`_ReflectionColor` and treats the sun as up — mirroring the specular's existing `_SunDir == 0` fallback,
so it never paints a black sky from an unset global.

### 11.3 Tunables (all additive — none of the owner's existing tuned values changed)

| Property | Default | What it does |
|---|---|---|
| `_ReflectionStrength` | 0.6 | **Master** opacity. **0 = off / today's look.** |
| `_ReflectionFadeChop` | 0.6 | the `_Chop` sea-state at which the reflection has fully faded to nothing. |
| `_ReflectionWindFade` | 0.5 | how much wind/`_Roughness` **additionally dims** it (0 = wind ignored). |
| `_ReflectionChopScatter` | 1.5 | how much chop **smears** (softens) the reflection. |
| `_ReflectionWindScatter` | 0.8 | how much wind **smears** the reflection. |
| `_ReflectionSkyTint` | 0.85 | weight of the live day/night sky vs `_ReflectionColor`. |
| `_ReflectionColor` | (0.62,0.74,0.86,1) | base reflected-sky colour (used when the cycle is off). |
| `_ReflectionSmear` | 1.6 m | vertical smear length of a SHARP (calm) reflection. |
| `_ReflectionSunStreak` | 0.9 | intensity of the brighter sun glitter/streak. |
| `_ReflectionSunSharp` | 6.0 | tightness of the sun streak at calm (higher = narrower/hotter). |

To turn reflections OFF entirely (the pre-feature look), set `_ReflectionStrength = 0`.

### 11.4 Determinism guard (headless C# twin)

The reflection FIELD (the smear band + glitter) is GPU value-noise (not unit-testable headless), but the
**sea-state response curves** — the part that decides *how strong* and *how sharp* the reflection reads as
the sea changes mood — are pure functions mirrored as C# twins in
`Assets/_Project/Code/Art/WaterReflection.cs` (`WaterReflection.ReflectionStrength` /
`ReflectionSharpness`, reusing `WaterSurface.Smoothstep`), unit-tested headless in
`Assets/Tests/EditMode/Art/WaterReflectionTests.cs`: strength is full on glass, monotonically fades to 0 by
the fade-chop and stays gone past it, wind dims it further, and the master 0 turns it off at every
sea-state; sharpness is a mirror at calm and smears monotonically toward 0 with chop + wind, clamped (no
negative smear). The twins are **not pushed** to the material and **not** in `WaterSurface.cs` — they read
the existing sea-state uniforms, so there is no new C# uniform. The CI shader-compile guard
(`WaterShaderCompileGuardTests.cs`) continues to force-compile the shipped `Water.mat` variant: no `+` in
any `[Header]`/property string, no `[unroll]` over a runtime bound (the magenta class stays guarded).

### 11.5 Sky CONTENT — drifting clouds, a LIVING moon glitter path, faint stars (shipped upgrade)

§11 reflected the sky *colour* + a sun glint. **Because this is a ¾ top-down game the player never sees the
sky directly — the water's reflection is the ONLY place the sky appears** — so the owner asked for the sky's
*content* to reflect too. This layer adds three things ON TOP of the §11 mirror, all in
`SkyContentReflection()` (the DAY share is composited after `SkyReflection()`, before the foam; the NIGHT-gated
share is composited after the palette grade, overlay-compensated — see §11.6):

1. **Drifting CLOUDS (day + night).** Soft, elongated pale bands built from an FBM field on a coord
   **compressed across the wind** (so the cloud cells elongate into wisps ALONG it) and **scrolled along the
   shared sim wind** global `_WindWorld` (the SAME wind the grass + water already read — declared here as a
   read-only global; **no new push**, falling back to a gentle +X creep when nothing publishes it). A soft
   threshold (`_CloudSoftness`) shapes pale clumps with clear sky between; the clouds tint toward the current
   sky (warm at dusk). `_CloudStrength` / `_CloudScale` / `_CloudDriftSpeed` / `_CloudColor` tune them.
   The cloud FBM coord is **camera-anchored** (`worldXY − _WorldSpaceCameraPos.xy`) exactly like the moon
   disc below, so distant clouds — a reflection of the sky at infinity — **stay put as the follow-cam tracks
   the sailing boat** and drift **only** with the wind at `_CloudDriftSpeed` (owner playtest fix, 2026-07-05:
   sampling the raw `worldXY` scrolled them past at BOAT speed, which is why lowering `_CloudDriftSpeed` alone
   never fixed it — that dial only rode ON TOP of the boat-motion scroll). `_WorldSpaceCameraPos` is the URP
   built-in the moon/sun anchors already read — no new uniform. (Stars remain world-anchored, unchanged.)
2. **The LIVING MOON** — a reflected disc + a shimmering **vertical GLITTER PATH** (the classic moonlight
   column: broken, wavy, animated highlights descending toward the viewer; pixelized so it reads as pixel
   art). The money shot on **calm night water**. It is **alive**:
   - **It MOVES** — the moon rises east, arcs overhead, and sets west across the night, so the reflected disc +
     glitter **travel** over the water. The current arc direction comes from the **`_MoonDir` global**
     published by the new self-installing **`MoonCycle`** service (mirrors `GrassWindBridge` /
     `DayNightController`; reads `GameServices.Clock`; **`DayNightController` is NOT touched**).
   - **It is ANCHORED AT THE CAMERA** — the reflected disc sits offset along the arc direction from the
     **camera's ground position** (`_WorldSpaceCameraPos.xy`), so it travels **with the viewer** like a real
     reflection of a body at infinity (the classic "the moon follows you along the shore") and always lands on
     water near the play area. (It was anchored at the height-map world centre — on St Peters that is the
     middle of the bared **sandbar**, ~40 m from the play area, so the owner never actually saw it.)
   - **It has PHASES** — `_MoonPhaseState` carries a signed **terminator** the shader carves the disc with
     (new → crescent → quarter → gibbous → full → waning), and a **brightness** that dims a thin crescent.
   - **It is TIED TO THE TIDES** — `MoonMath.Phase01` derives the phase from the **same lunar period** that
     drives `TideModel`'s spring/neap envelope, so **full moon ↔ spring tide** (proved in a headless test;
     vision-and-pillars §5.5). A tunable links per-night presence to phase: a **new moon** is a genuinely dark
     night you need the boat spotlight for (P1/P5). Tunables: `_MoonStrength` / `_MoonSize` / `_MoonGlitter` /
     `_MoonGlitterLength` / `_MoonColor` on the material; the lunar period + moonrise/set + phase→presence on
     `MoonCycle`.
3. **Faint STAR sparkle (night).** Tiny, sparse, per-cell-phased twinkling glints from a high-frequency hash
   field, pixelized to single pixels, very subtle. `_StarStrength` / `_StarDensity` / `_StarTwinkleSpeed`.
4. **The SUN GLITTER PATH (golden hour)** — the moon column's daytime/dusk twin: a **warm golden glitter
   column toward the LOW sun** at dawn and dusk (the classic "path of light to the sun" across calm water).
   Same camera-anchored column structure as the moon's glitter path (decorrelated noise so the two never read
   as copies), but gated by **`SunGlitterGate` over `_SunElevation`** instead of night: it rises just above
   the horizon (full by elevation 0.02), holds through the low-sun band, and is **gone by ~0.5 elevation** (a
   high sun glints via the specular, not a column) and **at/below the horizon** (the moon takes over; the
   unset cycle-off elevation of 0 also gates it to 0 — no phantom glitter in a bare scene). It **reuses the
   moon's geometry knobs** (`_MoonGlitterLength` = reach, `_MoonSize` = width basis; rule 6) and adds only
   two tunables: **`_SunGlitterStrength`** (default 0.6; 0 = off) and **`_SunGlitterColor`** (warm gold
   `(1.0, 0.82, 0.55)`). It is routed into the **compensated post-grade share** (§11.6, alongside the
   moon/stars/boat beam) so the dusk tint's downstream multiply can't mute its authored warm gold — at midday
   the tint is ~1 so the compensation is a natural no-op, and the gate is ~0 there anyway (midday water is
   effectively unchanged). Inherits the sea-state fade + sharpness smear like all sky content.

**Invariants (all hold):** everything **inherits the §11 sea-state fade** (reuses `ReflectionStrength()` /
`ReflectionSharpness()` — strong on CALM, gone in chop/storm); the moon + stars additionally **gate by night**
(`NightFactor()`, the darkness of the global `_DayNightTint`, the same convention the boat-light night-gate
uses), clouds read day + night. It is **col.rgb-only** — the DAY share is added before the foam and graded by
the **palette guard-rail** as before; the NIGHT-gated share is added **after** the grade, pre-compensated for
the day/night multiply overlay so complete dark can't crush it (§11.6) — it composes with the day/night
overlay (multiplies on top) and the weather palette. **`_SkyReflectionStrength = 0` returns the §11
look.** `WaterSurface.cs` is **untouched** (no new water uniform).

**Determinism guard.** The cloud/moon/star FIELDS are GPU value-noise (not unit-testable headless), but the
moon's deterministic state is pure: `Assets/_Project/Code/Art/MoonMath.cs` (`Phase01`, `IlluminatedFraction`,
`TerminatorSigned`, `MoonArc`, `NightProgress`) + `MoonCycle.ComputeState` are unit-tested in
`Assets/Tests/EditMode/Art/MoonMathTests.cs` (phase cycles 0..1, full-moon-on-spring-tide /
quarter-on-neap, arc rises→peaks→sets, down by day, new dimmer than full). The reflection-curve twins gain
`WaterReflection.MoonDirection` / `NightFactor` / `SkyElementStrength`, tested in `WaterReflectionTests.cs`.
The sun glitter's golden-hour window is the pure twin `WaterReflection.SunGlitterGate` (window constants
`SunGlitterRiseEnd` / `SunGlitterFallStart` / `SunGlitterFallEnd`), pinned there too (zero at/below the
horizon, peak through the low-sun band, gone by high sun, monotonic dawn rise / noon fall).

### 11.6 Complete-dark fix — light content is PRE-COMPENSATED for the day/night multiply (post-grade)

The owner reported two night-visual bugs with the same root cause: at **complete dark** the boat spotlight's
water beam and the reflected moon/glitter/stars all but vanish. The day/night system (ADR 0013) draws a
whole-frame **MULTIPLY** overlay after the water renders; at deepest night the tint is
`skyTint(0.12, 0.16, 0.34) × intensity floor 0.18 ≈ (0.022, 0.029, 0.061)`, so any light the water added to
itself survived on screen at **~3–6%, blue-shifted**. A secondary crusher: at deep night the §13 palette
grade's day/night value floor saturates (`floorPre = 1`) and pulls **all** pre-overlay water toward luma 1 at
`_PaletteGradeStrength`, flattening lit-vs-unlit contrast.

**The fix (in `HiddenHarboursWater.shader`'s `frag()`):** the light content — `BoatLightTerm()` plus the
NIGHT-gated share of `SkyContentReflection()` (moon disc + glitter + stars + the clouds' night portion) — is
now added **after `PaletteGrade()`**, divided by `max(_DayNightTint.rgb, DN_COMP_MIN_CHANNEL)` so the
overlay's downstream multiply **cancels** and the light reads at its authored brightness however dark the
night is. This is the same pre-compensation pattern the guard-rail's `PaletteValueFloorDayNight` already uses
(ADR 0015). Key properties:

- **`DN_COMP_MIN_CHANNEL = 0.02`** bounds the boost at ≤ 50× so a near-zero tint channel can't explode the
  divide; the shipped deepest-night channels all exceed it, so cancellation there is **exact** (no hue shift).
- **Daylight is pixel-identical**: the beam is night-gated to 0 by day, the night share is 0 by day, and the
  clouds' day share still composes pre-grade exactly where the whole layer used to sit.
- **The clouds' night share is MOONLIT, not fully compensated (owner playtest 2026-07-23 — the "whole sea
  becomes white" fix).** The exact-cancel compensation meant the night clouds read at FULL authored
  strength over a sea the overlay had dimmed to a few percent — a milky whole-frame veil that smothered
  every water detail from dusk on (the night factor saturates by a dusk tint of luma ~0.35). Clouds are a
  REFLECTION of the sky, not a light source: at night they read only by moonlight. The night share's
  weight is now `night × saturate(moonPresence × moonBrightness) × _CloudMoonlitVis` (default 0.35 —
  faint moonlit bands under a full high moon; a moonless/new-moon night shows none; the no-MoonCycle
  fallback keeps a bare-scene preview sane). `_CloudMoonlitVis = 1` restores the pre-fix full-strength
  night share exactly. The moon disc/glitter/stars/beam/rain rings are genuine LIGHT content and keep
  the compensated bucket ungated. Twin: `WaterReflection.MoonlitCloudVisibility`
  (+ `DefaultCloudMoonlitVisibility`), pinned in `WaterReflectionTests`; the rendered-frame pin is
  `WaterWhiteoutShoreSwirlAcceptanceTests.WhiteOut_DuskClouds_AreMoonlit_NotAVeil` (moonless dusk ⇒ no
  veil, full moon ⇒ faint share survives, visibility-1 sabotage ⇒ the veil magnitude is visible to the
  assert).
- **Cycle off (edit mode / bare art scene / demo)**: the tint global is near-black (unset) → the content is
  added **raw** (there is no overlay to compensate for) — the tuning/preview look is preserved.
- **HDR dependency**: this works because the URP asset has **HDR ON** (`UniversalRP.asset m_SupportsHDR: 1`) —
  the compensated values are far above 1 and must survive the framebuffer to reach the overlay's multiply. A
  later mobile port that disables HDR silently regresses this; re-check there.
- **Post-grade on purpose**: the guard-rail still bounds the SEA the light sits on, but no longer clamps the
  compensated (>1) light values or floor-flattens the lit pool. Known side-effect: once the water-beam's
  night gate saturates (~mid-dusk) the lit pool reads at full authored brightness — brighter than the crushed
  look the owner saw at dusk; it stays tunable via the existing beam strength.
- **Determinism guard**: the divide is mirrored headless in `LightMath.CompensateForDayNightTint`
  (+ `LightMath.DayNightCompensationMinChannel`) and pinned in `LightMathTests` (on-screen constancy across
  tint luminances, exact deepest-night cancellation, cycle-off untouched, the 50× bound).

---

## 12. Water presets — saved sea-mood material variants + the apply/generate/save menu

> The owner asked to *"save the current ocean tune as a material preset, along with several variations."*
> This section is the result: a small **library of complete `HiddenHarbours/Water` material variants**, each a
> distinct sea MOOD, plus an editor menu to **apply** one onto the live water, **generate** native Unity
> `.preset` assets, and **save** the owner's own tune as a new variant. It is **art-direction only** — no
> shader, code-sim, or save change (rule 5). Tunable: every value lives on the material assets (rule 6).

### 12.1 The sim-override caveat (read this first)

At runtime the `WaterSurface` component **overrides** the sim-driven knobs — `_Chop`, `_Roughness`, `_Flow`,
`_FlowDir`, `_WindDir` — from the deterministic sea-state every tick (§0, ADR 0010/0013). So **calm vs storm
happens automatically with the weather**, on *any* preset: a preset cannot make the sea permanently flat or
permanently raging. A preset instead expresses mood through the **non-sim-overridden VISUAL knobs**:

- **Palette** — `_DeepColor` / `_ShallowColor` / `_FoamColor` / `_SpecColor` / `_CausticColor` /
  `_ReflectionColor` / `_FbmTint`, plus `_SurfaceTint` and `_DepthBands`.
- **Foam character** — `_FoamDensity` / `_FoamDensityWind` / `_FoamThreshold(Soft)` / `_FoamSolidThreshold` /
  `_FoamStreakStretch` / the `_Whitecap*` lifecycle (form sharpness / peak density / collapse rate).
- **Swell** — `_OceanSwellStrength` / `_OceanSwellScale` / `_OceanSwellSharpness` (the rolling cohesion bands).
- **Specular** — `_SpecAmount` / `_SpecSharpness` / `_SpecSwellBias`.
- **Caustics** — `_CausticAmount` / `_CausticScale` / `_CausticDepth`.
- **Reflection** — `_ReflectionStrength` / `_ReflectionColor` / `_ReflectionSkyTint` / `_ReflectionSmear` /
  `_ReflectionSunStreak(Sharp)` / the chop+wind scatter/fade knobs.

The **structural** knobs are **identical** across every variant (so applying one never moves the gameplay
waterline): the height map (`_HeightMin/Max/WorldMin/WorldSize`), `_WaterLevel`, every `_Use*` keyword toggle,
the painted texture references, `_PixelsPerUnit`, `_PaintScale`, and the shoreward bias
(`_ShorewardBias/Falloff`, `_ShoreSampleStep`). The sim-driven knobs above are also left at the base value
(they're overwritten at runtime anyway). Each variant is therefore a **complete, valid `HiddenHarbours/Water`
material** — assigning it to the Sea "just works", and the CI magenta guard (`WaterShaderCompileGuardTests`)
force-compiles every one.

### 12.2 The variant library (`Assets/_Project/Art/Materials/WaterPresets/`)

| Variant | Mood (one line) |
|---|---|
| **Water_NorthAtlantic** | The current shipped tune **verbatim** — the cold teal-navy "home" / default. |
| **Water_GlassyCalm** | The mirror showcase: reflections up + sharp, restrained milky foam, gentle round swell, soft cool spec, clear cold caustics. Serene. |
| **Water_StormGrey** | Cold grey gloom (P5 teeth): desaturated grey-blue palette, dense whiter whitecaps, stronger broader swell, reflection near-off (storms don't mirror), dark brooding deeps. |
| **Water_FoggySmother** | Pale, desaturated, low-contrast, eerie (The Smother): washed cold-grey colours, minimal spec + caustics, a soft diffuse pale reflection, low-contrast foam. |
| **Water_WarmShelter** | A gentler, slightly **warmer** sheltered-harbour mood: warmer shallow + spec + reflection tint (tasteful, not tropical), calmer foam, a touch more caustic clarity. |

All five are cold North-Atlantic-family except **WarmShelter**, which leans a careful step warmer for the
sheltered-harbour feel — still in-palette.

### 12.3 The menu (`Hidden Harbours ▸ Art ▸ Water Presets`)

The editor menu lives in `Assets/_Project/Art/Editor/WaterPresetMenu.cs`:

1. **Apply to live Water ▸ &lt;variant&gt;** — the recommended non-dev path. Copies the chosen variant's shader
   properties onto the shipped `Assets/_Project/Art/Materials/Water.mat` (via `CopyPropertiesFromMaterial`),
   then dirties + saves it. Because the St Peters Sea plane uses `Water.mat` (`StPetersBuilder` hard-sets
   `sharedMaterial = Water.mat`), this swaps the in-game look **immediately** AND **survives a "Build St Peters
   Scene" re-run**. It asks before overwriting and is **Undo-able** (Edit ▸ Undo). One item per variant.
2. **Generate native .preset assets** — creates a real Unity `UnityEditor.Presets.Preset` (`new Preset(mat)`)
   next to each variant `.mat` in the WaterPresets folder. These are genuine Unity "material presets" the owner
   can drag onto any material's Inspector. They are **generated by this menu** (Unity authors them at runtime),
   never hand-written `.preset` YAML (fragile).
3. **Save current Water as new variant...** — duplicates the live, tuned `Water.mat` into the WaterPresets
   folder under a name the owner picks (a save dialog), so the owner can bank his own tweaks as a reusable
   preset variant.

The live `Water.mat` is **only ever changed by the explicit "Apply" command** (that is the intent) — the
variants are read-only sources the menu copies *from*.

---

## 13. Palette guard-rail — a tunable final-stage soft grade that bounds the sea to a palette (ADR 0015)

> The owner asked for a **guard-rail** on the water's FINAL output so the increasingly rich, sea-state-driven
> look (§5.6–§11) can never **wash out** (too bright) or go **muddy** (too dark), while keeping the dynamic
> diversity. He chose **SOFT** rails — bound the extremes and gently PULL toward the palette, **NOT a hard
> lock**. This section is the result: a **final colour-grade stage** in the water frag (`col.rgb` only, the
> LAST thing before `return col`) plus **palette presets** integrated with the §12 library. Decision of
> record: [`../adr/0015-water-palette-guard-rail.md`](../adr/0015-water-palette-guard-rail.md).

### 13.1 What the grade does (three soft ops, scaled by a master)

After every layer composites, `PaletteGrade(col.rgb, dayNightLuma)` applies, in order:

1. **VALUE (luminance) FLOOR + CEILING** — no mud, no blowout. A hue-preserving multiplicative re-scale
   clamps the colour's luminance into `[floor, ceil]`. The **floor is DAY/NIGHT-AWARE** (see §13.2); the
   ceiling is a plain luminance cap.
2. **SATURATION CAP** — HSV-style `(max−min)/max` capped at `_PaletteSatCap`; above the cap the colour is
   pulled toward its own grey just enough to hit the cap (luminance preserved — it only desaturates).
3. **ANCHOR PULL** — a soft `lerp` toward the **nearest palette anchor by luminance** (a continuous
   piecewise blend across `_PaletteDeep` / `_PaletteMid` / `_PaletteShallow` / `_PaletteFoam`) at
   `_PalettePullStrength` (~0.3–0.4 — a rail, not a cage).

The whole graded result is lerped back toward the raw colour by the master `_PaletteGradeStrength`, so
**`_PaletteGradeStrength = 0` is an EXACT passthrough (today's look, byte-for-byte)** — opt-in + revertible.
It is composited **after** the foam/whitecaps (it bounds the *finished* colour). `col.rgb` ONLY: it never
touches `depth` / `clip()` / the deep tint / the caustic gate / `_WaterLevel` / the height read / the sim
(P1 integrity, CLAUDE.md rule 5); it drives no sim and saves nothing.

### 13.2 The day/night floor — never muddy in daylight, still dark at true night (the subtle part)

The day/night system (ADR 0013) draws a **full-screen MULTIPLY overlay ABOVE the water** that multiplies the
WHOLE composited frame by the global `_DayNightTint`. So whatever the water shader emits is **multiplied
downstream** by the day/night tint's luminance. A naive constant floor in the water shader would be
**darkened away** by that multiply — forcing a bad choice between *daylight muddy* (floor too low) or
*killing the genuinely-dark nights* (floor too high).

The fix is **pre-compensation**: the water floors its PRE-overlay luminance at

```
floorPre = min(1, paletteFloor / max(dayNightLuma, eps))
```

so that AFTER the overlay's `× dayNightLuma`:

- **Daylight / overcast** (`dayNightLuma ≈ 1`): `floorPre ≈ paletteFloor` → the on-screen water lands at
  ~`paletteFloor` — **never muddy**.
- **True night** (`dayNightLuma` small): the quotient **saturates at 1** (water full-bright pre-overlay), so
  the overlay still multiplies it down to **genuine dark** — the owner's dark-nights vision is preserved.

`_PaletteNightFloor` (an on-screen luminance, default **0**) optionally keeps a faint readable sea at night
(it raises the deep-night floor a touch, inert in daylight). **`_PaletteNightFloor = 0` lets night go as dark
as the overlay takes it** (the default). When the day/night cycle is NOT running the global `_DayNightTint`
is near-black (the same "unset" convention the reflection/specular use) — the grade then treats it as full
daylight (`dayNightLuma = 1`, the daylight rail) so a bare art scene / editor preview never paints a
phantom-dark floor.

**The DAY KNEE (`_PaletteFloorKnee`, default 0.45 — owner playtest 2026-07-23, the "whole sea becomes
white" fix).** The raw quotient above SATURATES through dusk: at a dusk tint (dnLuma ~0.17–0.34) it held
the on-screen floor at the full daylight `paletteFloor` while the whole scene dimmed around the sea, and —
worse — the pre-overlay clamp level rose into the middle of the sea's value distribution, flattening most
of the frame to ONE value (the dusk-storm repro measured 99.7% of on-screen pixels within ±0.05 of the
median). The knee bounds the divisor:

```
floorPre = min(1, paletteFloor / max(dayNightLuma, _PaletteFloorKnee))
```

- **At/above the knee** (daylight incl. storm-overcast): byte-identical to the shipped curve — the
  on-screen floor lands at `paletteFloor`, never muddy.
- **Below the knee** (dusk → night): the pre-overlay floor stops growing (it holds at `floor/knee`), so
  the ON-SCREEN floor rides down with the scene (`× dnLuma/knee`) and the clamp stays at the BOTTOM of the
  value distribution — dusk keeps its crest/trough/foam structure and genuinely darkens.
- `_PaletteFloorKnee = 0` restores the pre-fix saturating curve EXACTLY (the legacy passthrough).
- The NIGHT floor (`_PaletteNightFloor`) keeps its saturating divide untouched — its whole job is to
  survive deep night.

Twin: `WaterPaletteGrade.ValueFloorDayNight(paletteFloor, dnLuma, nightFloor, floorDayKnee)` +
`WaterPaletteGrade.DefaultFloorDayKnee`, pinned by `WaterPaletteGradeTests` (identity at/above the knee,
the dusk ride-down, knee-0 legacy passthrough); the rendered-frame pin is
`WaterWhiteoutShoreSwirlAcceptanceTests.WhiteOut_DuskStorm_KeepsValueStructure_OnScreen` (with a knee-0
sabotage arm proving the assert sees the defect).

### 13.3 Palette presets (the palette IS a material property set)

A palette = its four **anchor colours** + its **bounds** (floor / ceil / sat-cap / pull-strength /
night-floor), all material properties, so a Water variant **carries its palette**. The live `Water.mat`
ships **North Atlantic** at the soft default (`_PaletteGradeStrength = 0.35`). Three NEW palette variants
join `WaterPresets/` alongside the §12 moods:

| Variant | Palette (one line) |
|---|---|
| **Water_StirredBrown** | Turbid brown-green estuary: low saturation, mid value, muddy olive-tan anchors. |
| **Water_DeepBlue** | Saturated deep open-ocean blue: higher contrast, vivid navy→blue-teal anchors. |
| **Water_Tropical** | Turquoise / cyan, brighter higher-sat shallows — the deliberate WARM/BRIGHT outlier (everything else is cold North-Atlantic-canon). |

The existing 5 mood variants gained the **same palette property key set** with per-mood-appropriate bounds
(e.g. StormGrey: low floor + low ceiling + tight sat-cap + stronger pull for cold gloom; FoggySmother:
high floor + tight sat-cap for pale low-contrast eerie), so **every variant is a complete material with one
property key set** (the CI magenta guard force-compiles them all). All eight appear in the
`Hidden Harbours ▸ Art ▸ Water Presets ▸ Apply to live Water` submenu and the "Generate native .preset
assets" list (`WaterPresetMenu.cs`).

### 13.4 Tunables (all additive; `_PaletteGradeStrength = 0` = the pre-feature look)

| Property | Default (Water.mat) | What it does |
|---|---|---|
| `_PaletteGradeStrength` | 0.35 | **Master** — lerps the whole grade back toward raw. **0 = today's look.** |
| `_PaletteValueFloor` | 0.10 | Daylight on-screen luminance FLOOR (no mud). |
| `_PaletteValueCeil` | 0.85 | Luminance CEILING (no blowout). |
| `_PaletteSatCap` | 0.55 | HSV-style saturation CAP. |
| `_PalettePullStrength` | 0.35 | Anchor PULL strength (soft; a rail). |
| `_PaletteNightFloor` | 0.0 | On-screen luminance floor permitted at NIGHT (0 = night goes dark). |
| `_PaletteDeep/Mid/Shallow/Foam` | (palette) | The four anchor colours the grade pulls toward, by luminance. |

To turn the guard-rail fully off (the pre-feature look), set `_PaletteGradeStrength = 0`. The floor + ceiling
live in `_PaletteValueFloor` / `_PaletteValueCeil`; switch palettes via the preset menu (§12.3).

### 13.5 Determinism guard (headless C# twin)

The grade math is mirrored exactly in a pure `Assets/_Project/Code/Art/WaterPaletteGrade.cs`
(`WaterPaletteGrade.Grade` + `ValueFloorDayNight` / `CapSaturation` / `AnchorForLuma` / `ScaleToLuminance`)
and locked headless in `Assets/Tests/EditMode/Art/WaterPaletteGradeTests.cs`: strength 0 = identity at every
input + day/night state; the floor lifts mud to the palette floor in daylight and the pre-comp lands
on-screen at the palette floor after the multiply; **true night still reaches genuinely dark**; the night
floor keeps a faint sea only when asked; the ceiling caps blowout; the sat cap desaturates while preserving
luminance; the anchor pull is soft (moves toward, never snaps to, the anchor) and continuous in luminance.
The CI shader-compile guard (`WaterShaderCompileGuardTests.cs`) continues to force-compile the shipped
`Water.mat` AND every WaterPresets variant: no `+` in any `[Header]`/property string, no `[unroll]` over a
runtime bound (the magenta class stays guarded), and every variant carries the same `_Palette*` key set.

---

## 14. Weather-driven water palette — the deterministic weather EASES the sea's mood through the presets (ADR 0017)

> The owner asked the weather to *"cycle through the water presets, in a realistic fashion."* This section is
> the result: a runtime **weather → water-mood** blend on `WaterSurface` that, when enabled, EASES the sea's
> MOOD/COLOUR through the §12 preset library as the **deterministic** `EnvironmentSample` shifts (calm ↔ storm
> by sea-state, pulled toward fog by low visibility — P1 "the sea has moods"). It is **opt-in** (off = today's
> static look), **MPB-only** (the `Water.mat` asset is never written), and it drives **only** the mood props —
> **never** the physics props `WaterSurface` already feeds from the sim. Decision of record:
> [`../adr/0017-weather-driven-water-palette.md`](../adr/0017-weather-driven-water-palette.md). This **answers
> the §13 / ADR 0015 open question** ("per-region palette by mood/weather").

### 14.1 The realistic model (a pure 2-axis blend across four anchor moods)

`WeatherWaterPalette` (a pure C# static class, `Assets/_Project/Code/Art/WeatherWaterPalette.cs`) turns the
deterministic sample into 0..1 weights over **four anchor preset MOODS** — a region **BASE**, a **CALM** mood,
a **STORM** mood, and a **FOG** mood — that sum to 1:

1. **Sea-state axis** — `SeaStateAxis01(SeaState)` (Glass=0 .. Storm=1), shaped by a tunable threshold + curve
   (`ShapeAxis`), drives a **CALM ↔ STORM** lerp. Low sea-state = serene; rising = the greyer/choppier/
   desaturated **Storm** mood.
2. **Fog axis** — `(1 − Visibility)`, shaped by its own threshold + curve, pulls the whole mood toward the
   **FOG** mood (pale, desaturated, low-contrast, soft).
3. **Combine** — the sea-state lerp makes a calm↔storm base mood; the fog amount then pulls THAT toward fog:
   `storm = (1−fog)·seaAmt`, `calm = (1−fog)·(1−seaAmt)·calmReach`, `fog = fogAmt`, the **base** backfilling
   the rest. So a **foggy storm reads mostly fog** (the smother dominates), a **foggy calm reads pale-serene**,
   and a **clear gale reads storm** — the realistic ordering.

The default anchors (St Peters) — the calm/storm/fog presets are from `Art/Materials/WaterPresets/`; the
**BASE / calm anchor is left UNWIRED so it resolves to the Sea's own LIVE `Water.mat`** (the calm baseline
then tracks the owner's `Water.mat` tuning; weather-off / strength-0 = exactly `Water.mat` — ADR 0017):

| Axis end | Anchor preset | Mood |
|---|---|---|
| **Base** (fair/clear/calm-ish) | _unwired_ → the live `Water.mat` | the renderer's own tuned look (the cold teal-navy "home"); assign an explicit preset only to *pin* the calm look |
| **Calm** (lowest sea-state) | `Water_GlassyCalm` | serene mirror, gentle swell, restrained foam |
| **Storm** (highest sea-state) | `Water_StormGrey` | grey gloom, dense whitecaps, reflection near-off |
| **Fog** (lowest visibility) | `Water_FoggySmother` | pale, low-contrast, soft, eerie |

### 14.2 Integration — `WaterSurface`'s opt-in mode, pushed via the EXISTING MPB

`WaterSurface` already reads the `EnvironmentSample` each throttled tick and owns the per-renderer
`MaterialPropertyBlock`. The weather blend is an **opt-in mode** on it (master enable + strength, four
assignable anchor materials, the axis tunables). Each tick — the same tick that pushes the physics props — it:

1. reads the `EnvironmentSample`;
2. computes the target weights (`WeatherWaterPalette.BlendWeightsNonAlloc`, no alloc);
3. **EASES** the visible weights toward the target (`EaseWeights` — a frame-rate-independent exponential ease,
   the same form as the flow-momentum `SmoothVectorToward`, so the mood never POPS; first push snaps);
4. applies the master **strength** (`ApplyStrengthInPlace` — lerp the weights back toward the BASE anchor;
   **0 = base only = today's look**);
5. blends the MOOD props by reading each anchor material's value **per key** and writing the weighted result
   onto the **same** MPB — alongside (never replacing) the physics props.

Because it rides the MPB it **never mutates `Water.mat`** (rule 5) and is **cleared on disable** like every
other `WaterSurface` override.

### 14.3 What it blends — only the mood/colour props (DISJOINT from the physics props)

The blend writes exactly the §12.1 **non-sim-overridden** keys (palette grade `_Palette*`; colours
`_DeepColor`/`_ShallowColor`/`_FoamColor`/`_SpecColor`/`_FbmTint`/`_CausticColor`/`_ReflectionColor`; swell
`_OceanSwellStrength`/`Sharpness`/`Scale`; foam character `_FoamDensity`/`_FoamThreshold*`/`_FoamStreakStretch`/
`_FoamSolidThreshold`/`_FoamCrestGate`/`_FoamSoftness`/`_FoamWidth`/`_FoamNoise`/`_FoamTexStrength`/
`_WhitecapTexStrength`/`_Whitecap*`; `_SurfaceTint`/`_SurfaceTexStrength`/`_FbmStrength`/`_SparkleTexStrength`;
specular `_SpecAmount`/`_SpecSharpness`/`_SpecSwellBias`; caustics `_CausticAmount`/`_CausticScale`/
`_CausticDepth`/`_CausticTexStrength`; reflection `_Reflection*`). The key set is **read from the anchor
materials at runtime** (per-key, `HasProperty`-guarded), so it **can't drift** from what the presets carry.

It **deliberately EXCLUDES** every PHYSICS prop `WaterSurface` already drives — `_Chop`, `_Roughness`, `_Flow`,
`_FlowDir`, `_WindDir`, `_WaterLevel`, `_HeightTex`/`_Height*`. The two sets are **disjoint and compose**: the
sim drives the motion (chop/foam roughen physically with the sea-state), the weather blend sets the look. **No
double-drive.**

### 14.4 Composition (guard-rail + day/night)

The blend sets the material's mood VALUES; everything downstream still applies on top:

- the **§13 / ADR 0015 palette guard-rail** still bounds the FINAL `col.rgb` (and its `_Palette*` bounds are
  themselves part of the blended set, so a stormier sea gets stormier guard-rail bounds);
- the **ADR 0013 day/night overlay** still MULTIPLIES the whole frame on top.

Both are downstream of the values blended here, so they compose by construction (verified by the disjoint
key-set + the headless tests).

### 14.5 Tunables (rule 6; off = today's exact look)

All on the Sea's `WaterSurface` component (St Peters defaults):

| Tunable | Default | What it does |
|---|---|---|
| **Weather Palette Enabled** | **off** | Master enable. **Off = the Sea reads its `Water.mat` preset exactly (today's look).** |
| **Weather Palette Strength** | 1.0 | 0 = base anchor only (inert / today's look); 1 = the full weather-driven blend. |
| **Base / Calm / Storm / Fog Mood Material** | NA / Glassy / StormGrey / FoggySmother | the four anchor presets the blend mixes. |
| **Sea State Threshold** | 0.15 | sea-state axis (0..1 over Glass..Storm) below which no storm pull. |
| **Sea State Curve** | 1.4 | shaping exponent (1 = linear; >1 = the storm bites LATE — only a real blow goes grey). |
| **Fog Threshold** | 0.25 | fog axis (0..1 over 1−visibility) below which no fog pull (light haze leaves it alone). |
| **Fog Curve** | 1.2 | shaping exponent (>1 = only a thick smother goes pale). |
| **Calm Reach** | 0.8 | how far the lowest sea-state pulls toward the pure CALM preset vs the BASE (0 = base is calm). |
| **Weather Palette Response Time** | 8 s | the ease time constant — how slowly the mood slides between presets (0 = snap). |

To turn it fully off, untick **Weather Palette Enabled** (or set **Strength = 0**) — the Sea is then exactly
its authored preset.

### 14.6 Determinism guard (headless C# twin)

The model is a **pure function** of the deterministic sample + tunables, so it's fully unit-testable headless
(`Assets/Tests/EditMode/Art/WeatherWaterPaletteTests.cs`): the axes normalise/shape monotonically; the weights
always sum to 1 and stay non-negative across the whole weather space; CALM clear water reads serene (no storm,
no fog); a rising sea-state grows the storm mood monotonically; low visibility grows the fog mood monotonically
and **fog dominates** on top of any sea-state (a foggy storm reads mostly fog; a foggy calm reads pale-serene);
a clear gale reads storm-led; `calmReach`/the thresholds behave (calmReach 0 leaves glassy water on the base, a
higher sea threshold delays the storm); the ease is **frame-rate independent** (one step over `dt` == N steps
of `dt/N`); and **STRENGTH 0 / disabled == identity == today's static look** (the base anchor only, at every
weather). The GPU blend of the actual props can't be tested headless, but the WEIGHTS that decide the mood can
— the same precedent as `WaterReflection` / `WaterPaletteGrade` / `DayNightMath`.

## 15. Boat spotlight on the water — the beam lights the sea FROM WITHIN the water shader (ADR 0016)

The boat spotlight (ADR 0016) is an additive glow **quad** that lights **land** at night. It did **not** read on
the **water**: the URP 2D renderer draws the custom-shader water `SpriteRenderer` over the additive `MeshRenderer`
regardless of sorting order / Sort-as-2D / camera-depth pinning (two quad-sort fixes failed). The fix is to light
the water **inside the water's own fragment** — the same idiom the water already uses for the day/night sun
(`_SunDir`), the sky reflection, and the palette grade: read a **published global** and modify `col.rgb`.

- **The beam is published as GLOBAL shader uniforms** by `HiddenHarbours.Art.BoatSpotlight` (on its existing
  throttled tick, ~20 Hz, via `Shader.SetGlobal*` — no per-frame alloc): `_BoatLightPos` (world lamp xy at the
  bow), `_BoatLightDir` (world beam axis = the boat heading `transform.up`), `_BoatLightColor`, `_BoatLightParams`
  (`x` = intensity, `y` = range m, `z` = `cos(halfAngle)`, `w` = `cos(innerAngle)`), `_BoatLightParams2`
  (`x` = radial edge softness, `y/z/w` = night-gate threshold / softness / cycle-off fallback). The half-angle is
  a **cosine** so the water tests the cone with one `dot`, no per-pixel trig. **No boat / off** → intensity 0 →
  the water term is skipped (no stuck beam). **One light** for now (the boat spotlight is THE night-nav light);
  arrays + a count extend it cleanly later.
- **The water frag adds the cone** (`HiddenHarboursWater.shader`, `BoatLightTerm()`, **after** the palette
  guard-rail, pre-compensated for the day/night multiply overlay — the complete-dark fix, §11.6; the rail
  bounds the sea the beam sits on, but no longer clamps/flattens the lit pool): for the pixel's `worldXY`
  (pixel-snapped → the pool reads as **pixel art**) it computes the cone (lamp→pixel within range + within the
  cone, **radial × angular** falloff), scales by the **same night-gate** the land cone uses (off by day, full at
  deep night, off-by-dawn, read from `_DayNightTint`; cycle-off → the tunable fallback), and **ADDs** to
  `col.rgb` divided by `max(_DayNightTint.rgb, 0.02)` so the beam survives complete dark at authored brightness. **Sorting-INDEPENDENT** — it is part of the very draw that was winning the order tie, so it cannot be
  overdrawn like the quad. **`col.rgb` ONLY** — never `depth` / `clip()` / `_WaterLevel` / the height read / the
  sim (the P1-integrity / determinism invariant of every prior addendum holds; the beam is purely cosmetic and
  saves nothing).
- **One beam, two surfaces.** The **same** `BoatSpotlight` tunables (colour / intensity / range / cone / softness)
  drive **both** the land quad and the water term — tuning the spotlight tunes both. A water-side strength
  multiplier (`BoatSpotlight._waterStrength`, default **1.4**) balances how strongly the beam reads on water vs
  land. The effect defaults **ON and strong** so a midnight beam is an obvious raking pool of light on the dark
  sea. **No new material property** → `Water.mat` (and its presets) are untouched.
- **Magenta-safe:** no `+`/operator char in any `[Header]`, no `[unroll]` over a runtime loop, define-before-use
  (the day/night luma is inlined in `BoatLightTerm` since `PaletteLuma` is defined later); the shipped `Water.mat`
  variant is force-compiled by `WaterShaderCompileGuardTests` so a broken term fails CI red, not magenta-in-build.
- **Determinism guard (headless C# twin).** The pure cone/gate maths the water term mirrors live in `LightMath`
  (`CosFromHalfAngleDeg`, `ConeFalloffCos`, `WaterConeTerm`) and are unit-tested in `LightMathTests` (within-cone
  vs behind/outside, range falloff, off-axis dimming, at-the-lamp core, night-gate off-by-day). The GPU term
  itself is verified by the owner at **deep night** driving over open water (the beam is **night-gated**, so it
  fades toward off near dawn — verify ~midnight).

## 16. The shared wave field — whitecaps ride REAL crests (ADR 0018, Arc B1)

The swell layer's next life. §5.8's `SwellField` was **paint** — value-noise brightness bands that existed
only in HLSL, so the whitecap lifecycle (§5.11) gated on noise: the owner's verdict, *"unconvincing… a foggy
white soup."* ADR 0018 replaced the truth: **one deterministic directional wave field** (up to
`WaveTrains.MaxTrains` = **8** wave trains since the ADR 0027 P2 widening; `TrainsFrom` still derives 4 until
the spectrum re-weights them — `Core/Environment/WaveMath.cs`) that BOTH the boat (B2 rocking,
`BoatWaveMotion`) and the water shader sample.
B1 is the shader side: the trains become the water's **primary swell brightness source**, and the whitecaps are
re-keyed to **form → break → streak → fade on real, advancing crests** — foam that visibly **travels with the
wave**, which is what kills the static-soup read.

### 16.1 The bridge (`Art/WaveFieldBridge.cs`) — the same eased sea the hull rides

A self-installing `[RuntimeInitializeOnLoadMethod]` host (the `GrassWindBridge`/`MoonCycle` pattern). Every
frame it ticks the **same `WaveFieldAnimator`** (ADR 0018 addendum) `BoatWaveMotion` ticks — eased train
parameters chasing the weather-derived `WaveMath.TrainsFrom` targets, dispersion speed re-derived from the
**eased** wavelength (speed is never free), phase accumulated **incrementally in double** and baked into each
train's `PhaseOffset` — then publishes the trains as **global vectors** (outside every CBUFFER; `Water.mat`
untouched):

> `_WaveTrain0..7` — `xy` = unit travel direction, `z` = wave number `k = 2π/λ` (precomputed; the shader never
> divides by a wavelength), `w` = amplitude (m). Dead slots publish zero.
> `_WavePhases` / `_WavePhases2` — per-train phase for trains 0–3 and 4–7 (radians, wrapped to `[0, 2π)` in C#
> **double** before the float cast).
> `_WaveFieldParams` — `x` = live train count (**0 = nothing published → the LEGACY §5.8 path holds**),
> `y` = crest sharpening p, `z` = total amplitude (the crest normalizer), `w` = the **dominant (spectral-peak)
> slot index**.

⚠️ **The width is a seam (ADR 0018 amendment, 2026-07-29).** `WaveTrains.MaxTrains`,
`PackedWaveField.MaxTrains`, the bridge's uniform push, the shader's `WAVE_MAX_TRAINS` loop bound and the
shore-seam proof shader must all agree. Widening some of them is **not a compile error** — it is a reader
quietly sampling a narrower sea than the shader draws. The whole payload therefore travels as one
`PackedWaveField` (so no signature knows the width), publishing goes through the single
`WaveFieldBridge.PublishGlobals` (so the uniform names live in one place), and `WaveFieldSeamWidthTests`
asserts the width structurally — including by reading both shader sources as text, the only way a C# test can
hold the HLSL half of a twin.

⚠️ **`_WaveFieldParams.w` was `reserved` and always 0.** It now carries the spectral-peak slot, which is what
the whitecap lifecycle keys its face sign on and what the rocking consumers read the phase of. Under the flat
weighting the peak IS slot 0, so the published bytes are unchanged — but "the dominant train" stopped being
"whichever one is first" the moment a spectrum could re-weight it.

**No time uniform exists**: the shader evaluates `θ = k·(dir·worldPos) + φ` — the advancing time lives entirely
in the phase the animator accumulates, so the unbounded game time never touches float trig on the GPU, and the
water pixels and the hull provably ride the **identical eased sea** (both consumers tick the same animator code
with the same inputs; `WaveFieldBridgeTests` pins the parity). Cycle-off (EditMode / a bare art scene / no sim)
publishes count 0 → the pre-B1 look, the `_DayNightTint`/`_MoonDir` "unset" convention. The bridge's
`WaveFieldSettings`/`WaveFieldAnimatorSettings` start at the same `Default`s `BoatWaveMotion` uses — keep them
identical until a later Arc B PR unifies them on `GameConfig`.

### 16.2 The HLSL twin (`WaveFieldSample()`) and the §(6) transition mapping

A line-by-line transcription of `WaveMath.Sample` (mirrored headless by `WaveFieldBridge.ShaderTwinSample`;
change one, change all **in the same PR**): sharpened sine height, analytic slope, crest factor — plus
`primaryCos`, the primary train's face sign (negative = the wave's front face, the crest is arriving; positive
= behind, it just passed) that drives the foam lifecycle's fore/aft asymmetry. Fixed `[unroll]` bound of 4 with
the live count masked **inside** (never `[unroll]` a runtime count — the #96 trap); pow bases floored at 1e-6
(HLSL `pow(0,0)` NaN guard).

When trains are live, `swellCrest` — the 0..1 crest driver every downstream layer already reads (spec bias
`_SpecSwellBias`, whitecap gate `_FoamCrestGate`, sky-reflection lit faces) — comes from the real field, and
the owner's tuned `_OceanSwell*` values **map on instead of resetting** (ADR 0018 §(6)):

> `_OceanSwellStrength` → the brightness amplitude (`swellSigned × strength × 0.30`). **The brightness now
> reads the SHARPENED crest, not raw height** (owner playtest fix, 2026-07-05): `swellSigned` derives from
> `swellCrest` — `swellSigned = (swellCrest × 2 − 1) × swellLive` — so a **narrow bright ridge sits over a
> broad dark trough (a DEFINED crest)** instead of four summed trains smearing into a wide soft "white
> cloud". Raw un-sharpened height (the old `height/totalAmp`) never reached the eye's brightness; only the
> whitecap gate saw the sharpening. The gain nudged `0.25 → 0.30` because a pinched crest covers less area.
> `swellLive = saturate(_WaveFieldParams.z × 40)` gates the band by the field's un-clamped total amplitude so
> **glass = zero bands = the untouched mirror** stays true even though the remap alone would floor at −1.
> `_OceanSwellSharpness` → the crest-shaping exponent on the 0..1 crest signal (its exact legacy role);
> **default raised 1.4 → 2.2** so the brightness sharpening agrees with the wave field's own crest geometry.
> (The owner's Water.mat override, if any, still wins — his tuned swell dials now read as more DEFINED, so he
> may want to re-tune `_OceanSwellStrength`/`_OceanSwellSharpness`.)
> `_OceanSwellScale` → a **visual wavelength scale**, normalized to the shipped default **0.025**: at 0.025 the
> water renders the field's TRUE wavelengths (pixel == hull); the current tuned 0.07 renders ~2.8× shorter
> waves — retune toward 0.025 when the B2 rocking should visibly match the crests on screen.

**Not carried over** (out of Arc B scope — shore breakers are a later arc, ADR §(5)): the legacy path's
*shoreward crest-bias* — live trains run downwind everywhere; the foam **drift** shoreward bias (§5.12) is
untouched. The legacy `SwellField` path itself stays byte-for-byte behind the count-0 fallback until the owner
signs off the reworked look.

### 16.3 The whitecap rework — form, BREAK, streak, fade (the soup fix)

Open-water caps only (`_Roughness > 0.01` branch); the §5.3 fringe foam is untouched. With live trains the
**lifecycle places the foam on the advancing wave** (`WhitecapLifecycleWave()`, C#-twinned in
`WaveFieldBridgeTests`) and the §5.9 evolving wind-streaked cap field only **textures** it — nothing is a
field-wide veil any more:

- **FORM** — on the wave's **front face** (`primaryCos < 0`) the foam whitens in as the crest builds toward the
  break band.
- **BREAK** — a **tight band at the crest tip**: the `SolidCore` dense heart over the pixelized cap field →
  bright, **crisp pixel-art edges**, not soft alpha fog. `_WhitecapFormSharpness` narrows the band (its legacy
  role); wind lowers it (`− _Roughness × 0.35`, the cap-threshold discipline) so **a gale breaks more crests —
  marching whitecaps**.
- **STREAK** — the residual spreads **downwind** through the existing wind-aniso coord (`_FoamStreakStretch`,
  reused as-is).
- **FADE** — behind the crest (`primaryCos > 0`) the milky remnant decays at `_WhitecapCollapseRate` (its
  legacy role). `_WhitecapPeakDensity` still caps the newborn opacity; `_FoamCrestGate` still dials how tightly
  foam hugs the crest — the same knobs, a truer crest.
- **Sea-state coupling through the trains' amplitudes**: the one new knob, **`_WhitecapOnsetAmp`** (default
  0.5 m) — full caps by that much total train amplitude, first foam from ~10% of it. Glass = zero amplitude =
  **zero foam, automatically** (and the crest factor is already exactly 0 on dead glass — the mirror keeps the
  §11 reflections at full strength).

Composition unchanged: everything is `col.rgb`-only (P1, rule 5 — never depth/`clip()`/`_WaterLevel`/the sim),
sits **below** the palette guard-rail (§13) and **below** the post-grade compensated light block (§11.6 / §15);
the sky reflection's sea-state fade keeps working (it keys off `_Chop`/`_Roughness`, untouched).

### 16.4 Determinism guard (headless C# twins)

`WaveFieldBridgeTests` pins: the **packing layout** (k = 2π/λ, dead slots zero, empty field all-zero); **twin
parity** — `ShaderTwinSample` (the C# mirror of the HLSL) vs the reference `WaveMath.Sample` across the
WaveMathTests sweep, AND through the full runtime path (5 000 uneven animator ticks → `Pack` → reconstruct ==
`animator.Sample`, phases still wrapped — the hull/water same-sea contract); **glass silence**; and the
**lifecycle gates** (forms only on the front face, breaks at the tip, residual dies behind at the collapse
rate, wind widens the breaking population, zero density/troughs = nothing). The shipped `Water.mat` variant is
force-compiled by `WaterShaderCompileGuardTests`, so a broken twin fails CI red, not magenta-in-build.

### 16.1b Where the settings live — `GameConfig.WaveField`, and nowhere else (ADR 0018 §(5))

The derivation constants and the smoothing are on **`GameConfig`**, read through
`GameServices.WaveField` / `.WaveFieldAnimator`, wired once by `GameRoot`. Unwired (EditMode, a bare
art scene, any test rig) falls back to `Default`, so nothing breaks.

**There used to be eight copies** — the bridge, `BoatWaveMotion`, `BoatController`'s sim path,
`BoatWakeEmitter`, `BuoyWaveVisual`, `TrapHaulController` and `SeaweedPresenter` — each annotated
*"keep identical to the others"*, which is a comment, not a mechanism. They agreed only for as long
as nobody tuned anything, and ADR 0027's `SpectrumBlend` made divergence a one-slider mistake.

⚠️ **The sharpest edge this closed:** `WaveFieldBridge` is a runtime-created `HideAndDontSave` host
with no inspector, so **the water's copy was the one the owner could never reach**. A tuned config
would have moved the hull, the wake and the buoys while the drawn sea stayed at `Default` — the
see/feel split ADR 0018 exists to prevent, arriving through the tuning surface itself.

⚠️ **`Config != null`, never `?.`/`??`** — `GameConfig` is a `UnityEngine.Object` and the
null-propagating operators bypass its overloaded `==`, so a destroyed asset reads as alive and throws.
Resolved per read rather than cached, so dragging a slider during play moves the sea live.
`WaveFieldSettingsUnificationTests` scans the assemblies and fails if any type outside `GameConfig`
declares one of these structs as a field.

### 16.4b The JONSWAP spectrum — variance, a fan, and GROUPS (ADR 0027 #5, P2)

The owner's P0 verdict was that the sea reads as *"a rigid pattern"* at every non-storm weather. The
hand-authored field is a primary plus three shorter cross-chop trains at fixed fractions (1 / 0.55 / 0.38 /
0.22) — four sizes, three discrete axes, forever. `WaveSpectrum` replaces the weighting with a spectrum:

| Symptom | Mechanism | Where |
|---|---|---|
| no variance in sizes | JONSWAP amplitudes `√S(ω)`, `S = r⁻⁵e^(−1.25r⁻⁴)γ^…` | `WaveSpectrum.JonswapShape` |
| three discrete directions | `cos^2s(θ−θ_wind)` over a stratified, hash-jittered fan | `DirectionalWeight` + `AngleOffsetRadians` |
| nothing builds or dies | neighbouring frequencies **beat** — that IS a wave group | `FrequencyRatio` (spacing = the group dial) |

**Grouping is not code.** There is no group oscillator; it falls out of the frequency spacing, and
`BeatPeriodSeconds` = `2π/(ω_p·spacing)` exists so a test can prove the period is readable (≈25 s calm to
≈60 s gale at the shipped 0.08 — tens of seconds, not minutes).

**`SpectrumBlend` is a continuous morph, not a switch.** Every slot lerps from its legacy self toward its
spectral self; slots 4–7 have no legacy counterpart so they fade in from **exactly zero amplitude**, which is
why the field is continuous even though the live count jumps 4 → 8 the instant the dial leaves 0. It ships at
**0** (the ADR 0027 passthrough discipline), so the sea is byte-identical until the owner dials it in.

⚠️ **The amplitude ENVELOPE (Σ A) is preserved, deliberately.** Preserving *energy* (Σ A²) is the more
physical normalization, but Σ A is the crest-factor normalizer the whitecap lifecycle divides by AND the bound
the watertight hull clamp scans against — growing it would quietly reduce foam and raise every hull. Preserving
the envelope instead means the spectrum's peaks reach the height the hand-authored sea reached, and what it
adds is **the lulls between them**: exactly *"building and collapsing"*, at no risk to two calibrated systems.

⚠️ **Measuring grouping: run length, not variance.** The obvious metric — coefficient of variation of crest
heights — separates the two fields by only 17 % (0.53 → 0.63) and reads as "the spectrum barely helps". It
does not: the hand-authored field's four trains sit at frequency ratios 1 / 1.35 / 1.62 / 2.13, so they beat
FAST and irregularly. Big and small alternate almost every wave — high variance, and still a rigid pattern.
What the spectrum changes is the **timescale**, so the measure is the mean **run length** of consecutive
above-average crests ("three big ones then a lull" is literally a run of three). `WaveSpectrumTests` pins it.

### 16.5 Swell READ legibility — the passing swell you can SEE (`_SwellReadStrength`)

**Owner playtest (2026-07-08):** working the trap-haul minigame — which times a heave against the passing
swell — the owner reported *"it's hard to see the swells and to know when to time the heave,"* and localized it
to **the water itself**, not the cue: he could not see the wave rise and pass under the boat. The stock
crest/trough brightness (`_OceanSwellStrength × 0.30`) is tuned **subtle** — the shipped `Water.mat` sits at
`_OceanSwellStrength 0.09` × `_OceanSwellSharpness 6`, a swing of only **~±0.027** and a razor-thin pinched
ridge. See must equal feel (P1): the crest the haul samples has to be legible on screen.

The fix is a dedicated, **ON-by-default** legibility knob that amplifies the crest→trough **VALUE contrast** of
the **same shared wave field** (§16) the hull rocks on and the haul times against — value contrast is the
single biggest readability win and works on calm water too. It reads the **BROAD normalized crest**
(`waveHN`, pre-sharpen) rather than the pinched `swellCrest`, so the swell reads as the water **rising/falling**
in a wide moving band instead of a thin line, and adds `readBand × _SwellReadStrength × 0.25` to `col.rgb`
right after the stock swell add. It carries **its own gate, independent of `_OceanSwellStrength`** (so it reads
even where the owner dialed the stock swell down), and **inherits the field's `swellLive` amplitude gate — so
glass stays glass** (a dead-flat sea shows no band; the §11 mirror is untouched).

- `_SwellReadStrength` (default **0.35**) — master contrast amount. `0` = exact passthrough (the pre-feature
  look). At 0.35 the swing is **±0.0875** (~3× the owner's tuned stock swell) — a clearly legible band; the
  §13 palette guard-rail's value floor/ceiling bounds the extremes so troughs never go muddy nor crests blow
  out.
- `_SwellReadBands` (default **0** = smooth) — optional pixel-art posterize of the moving band into N discrete
  value steps for a crisp marching-contour read, mirroring `_DepthBands` / `_SpecBands`.

`col.rgb`-only, additive like every water layer — **never** `depth` / `clip()` / the deep tint / `_WaterLevel` /
the sim wave field (P1 integrity, CLAUDE.md rule 5): the waterline the player wades and the crest the haul
samples are byte-identical, so the sim is provably unchanged. No new C# uniform and no twin — it reads only the
already-sampled `waveHN`; `WaveFieldSample` / `WaveFieldBridge` / `WaveMath` are untouched. Legacy count-0
path (edit mode / cycle off) reuses `swellSigned` so the knob still reads there.

### 16.6 Convergence (Jacobian) foam gate (ADR 0027 #3)

Foam was tall-wave only — `_FoamCrestGate` + the §16.3 lifecycle key everything to the crest factor —
so **crossing trains never foam at their intersections**. Real foam also spawns where the surface
**pinches**: surface water drifts toward crests (the Gerstner horizontal displacement), and where that
drift field compresses — its Jacobian determinant dropping below 1, negative when the surface folds —
the sea whitens. `_FoamConvergenceStrength` (default **0** = today's foam EXACTLY, bit-identical
composite) adds that term as an **additional placement driver alongside the crest factor, never
replacing it**: four taps of the same `WaveFieldSample()` (same `waveFreqScale`, each on the pixelized
world grid) central-difference the field's **analytic slope** into the three second derivatives, and
`ConvergenceGate` — C#-twinned by `WaterFoam.Convergence`, change one change BOTH in the same PR —
computes `saturate(1 − J)` with `J = (1 + q·h_xx)(1 + q·h_yy) − (q·h_xy)²`, `q =
_FoamConvergencePinch` (metres, ≈ the Gerstner `Q/k`). Curvature is negative at a crest so a crest
converges; `h_xy` is the cross term two crossing trains write. The output is **textured by the same
thresholded cap field** (`capMilkyT` — already banded/dithered), so it feeds the existing foam
threshold and inherits the existing quantization — no new one. Still inside `waveGate` (glass = zero
foam, automatically) and the cap shore fade; trains-live path only (the legacy count-0 path has no
field to difference and is untouched). `col.rgb`-only dressing — never `depth` / `clip()` /
`_WaterLevel` / the height read / the sim (P1 integrity, rule 5). Twin tests
(`WaterFoamTests`): flat sea ⇒ 0, zero pinch ⇒ 0, crest converges / trough does not, crossing crests
out-converge either train alone and follow the determinant (not a plain sum), the cross term adds
exactly `q²·h_xy²`, and a golden value pins the arithmetic. Deliberately NOT added to
`WaterSurface.MoodFloatNames`; whether the strength should be mood-eased is an open question for the
owner.

| Property | Default | Effect |
|---|---|---|
| `_FoamConvergenceStrength` | `0` (**OFF**) | Master; 0 = today's foam exactly. The owner's dial. |
| `_FoamConvergencePinch` | `4` m | How far surface water is drawn toward a crest (≈ Gerstner Q/k); higher = more of the sea pinches past the gate. |
| `_FoamConvergenceStep` | `0.5` m | Finite-difference step of the four slope taps. |

## 17. Shallow-water reads: the bottom through the column + day-gated caustics (Arc C, superseded in part by ADR 0027 #7)

Owner-opt-in shallow-water effects, all shipping **OFF** (their strength = 0), so the shipped `Water.mat`
look is byte-identical until the owner dials them in — exactly like `_ReflectionStrength` / `_SkyReflectionStrength`
(rule 6). They live entirely in `HiddenHarboursWater.shader`, touching **only `col.rgb`** — never
`depth` / `clip()` / `_WaterLevel` / the height read / the sim (P1 integrity, CLAUDE.md rule 5). All key off the
read-only `depth` (`_WaterLevel - seabedElevation`, metres), so they naturally hug the moving shoreline.

### 17.1 See-through shallows — RETIRED by §17.7 (kept as the record of what it was)

Arc C showed the bottom by making the water slightly **translucent** right at the shore, so a **seabed sprite
drawn behind the Sea plane** (lower sorting) bled through the shader's `Blend SrcAlpha OneMinusSrcAlpha`:

```hlsl
// RETIRED (ADR 0027 #7). Kept here so the reasoning is legible, NOT as a live path.
float shallowT = 1 - saturate(depth / max(_ShallowSeeThroughDepth, 1e-3));   // 1 at the waterline -> 0 deep
col.a *= lerp(1, _ShallowMinAlpha, shallowT * saturate(_ShallowTranslucency));
```

**Why it went.** Three things were wrong with it, and none was tunable:

1. **The water shader never saw the bottom's colour**, so it could not absorb it — the seabed arrived
   **ungraded**, which is why `_ShallowMinAlpha` carried a "keep it above 0.5" warning: a hole in the sea.
2. **A scalar alpha cannot express per-channel transmission.** Real water eats red first; one alpha eats
   everything equally, so the shallows could only get *fainter*, never *bluer*.
3. It fought the caustics it sat on top of (§17.3), so both had to be tuned around each other.

`_ShallowTranslucency` was **0 in every material** from the day it shipped — `Water_FoggySmother` set it to 0
explicitly and no other preset overrode it (ADR 0027 finding 2). §17.7 supersedes it; it was **not revived**.
`_ShallowTranslucency`, `_ShallowSeeThroughDepth` and `_ShallowMinAlpha` are gone from the shader, from
`Water.mat`, from `Water_FoggySmother.mat`, and `_ShallowTranslucency` is gone from
`WaterSurface.MoodFloatNames` (nothing else read any of them).

### 17.2 Day-gated caustics (`col.rgb` only)

Folds a **day gate** into the existing shallow caustic add so the sun-dappled light nets only show when the sun
is up. The driver is **`saturate(_SunElevation)`** — 1 at noon, naturally 0 below the horizon at night (this is the
right curve; it is deliberately **not** `SunGlitterGate`, which peaks at *golden hour* and falls to 0 by high sun —
backwards for caustics). When the day/night cycle is **not running** (`_DayNightTint` sum ≈ 0: editor / bare art
scene) it treats the world as **full day**, the same "unset" convention `NightFactor` and the palette grade use —
**not** `_SunElevation == 0`, which is a legitimate value at real sunrise/sunset. An optional `_CausticShallowBias`
pushes the caustic band a little deeper off the very edge (see below).

### 17.3 The interaction — DISSOLVED, not tuned around (ADR 0027 #7)

This section used to say: see-through lowers `col.a` in the **same shallow band** where caustics live in
`col.rgb`, so under the `SrcAlpha` blend the lowered alpha **fades** the caustic-lit water and the two effects
partly cancel — mitigate with a conservative `_ShallowMinAlpha` and/or a `_CausticShallowBias` that pushes the
dapple just inside the see-through fringe.

**That cancellation no longer exists.** §17.7 composites the bottom **inside the shader**, so `col.a` stays
opaque and there is no alpha for the caustic add to be faded by. The interaction is gone **by construction**
rather than by tuning — which was the point of doing it in the shader at all. `_CausticShallowBias` survives as
an independent art dial (push the dapple off the very edge if you want to), no longer a mitigation for anything.

### 17.4 Tunables (rule 6; all default to today's look)

| Property | Default | Effect |
|---|---|---|
| `_CausticDayGate` | `0` (**OFF**) | 0 = caustics always on (today); 1 = day-only (fades out at night). |
| `_CausticShallowBias` | `0` m | Push the caustic band deeper off the very edge (0 = today's band). |

`_CausticDayGate` is in `WaterSurface.MoodFloatNames`, so the weather-driven palette (§14) and the preset
library (§12) **ease** it per mood — e.g. a `FoggySmother` preset can kill the sun-dapple. This is art-lane
dressing, not a sim change. (`_ShallowTranslucency` used to sit in that list too; §17.7's `_Turbidity`
replaces it there.)

### 17.5 Composition + guard

The caustic day gate rides the pre-existing `col.rgb` caustic add, which sits **before** the palette guard-rail
grade (§13, `col.rgb`-only) and the post-grade compensated light content (§11.6), both left untouched, so they
compose cleanly. The shipped `Water.mat` variant is force-compiled by `WaterShaderCompileGuardTests`, so any
HLSL slip fails CI red (not magenta-in-build).

### 17.6 Field-driven caustics (ADR 0027 #2)

The `_Caustic*` layer scrolled an independent noise, so the seabed shimmer had no relationship to the
swell visibly rolling over it. `_CausticCurvatureBlend` (default **0** = today's independent noise
EXACTLY) re-derives the caustic brightness from the local **curvature** of the SAME `WaveFieldSample()`
the swell bands / whitecaps / hull ride (§16): a finite-difference Laplacian over 4 axis taps at
`_CausticCurvatureStep` metres around the already-sampled centre height, on the same pixelized world
grid (the crawl law, §3) — brightest where the surface is locally **convex toward the sun** (a dome
focuses light; `−lap × _CausticCurvatureGain`, saturated). Composition unchanged: the curvature signal
replaces only the vein VALUE inside the existing `_CausticDepth` gate, the `_CausticDayGate` sun gate
(§17.2), `_CausticAmount` and `_CausticColor` — every downstream multiplier intact, and the painted
`_CausticTex` blend still applies before it at its own strength. Gated by the field's amplitude (the
`swellLive` idiom, `saturate(_WaveFieldParams.z × 40)`): with no live trains (edit mode / a bare art
scene) or on dead glass the blend eases back to the independent noise, so a bare scene never loses its
dapple — and physically a flat surface focuses nothing. No new C# uniform (the curvature needs no new
sim-pushed data — the ADR 0027 "no new uniform" ruling; the three knobs are material properties,
rule 6). `col.rgb` only — never `depth` / `clip()` / `_WaterLevel` / the height read / the sim
(P1 integrity, CLAUDE.md rule 5). Cost: 4 extra `WaveFieldSample` calls inside
`if (_CausticCurvatureBlend > 0.001)` — zero at the shipped default. Deliberately NOT added to
`WaterSurface.MoodFloatNames` (no double-drive); whether the blend should be mood-eased is an open
question for the owner.

| Property | Default | Effect |
|---|---|---|
| `_CausticCurvatureBlend` | `0` (**OFF**) | 0 = today's independent noise exactly; 1 = fully field-driven. The owner's dial. |
| `_CausticCurvatureStep` | `0.5` m | Finite-difference step of the curvature taps (bigger = broader, softer light nets). |
| `_CausticCurvatureGain` | `12` | Contrast of the field-driven veins (scales the raw Laplacian into 0..1). |

### 17.7 Seabed absorption — the bottom seen THROUGH the column (ADR 0027 #7)

The one place the physics does work no existing knob does. **Absorption applies to the transmitted seabed,
never to the water's own colour** — that distinction is the whole decision, and it is what makes this
compatible with the hand-painted ramp instead of a replacement for it.

**Why not drive the water body with `e^(−σd)`.** `_USE_DEPTHRAMP` is ON with a painted texture assigned, so
the base colour is a lookup into a **hand-painted 1D LUT** over a linear depth axis. A LUT is **strictly more
general** than any closed-form absorption curve: anything `e^(−σd)` can compute, the owner can already paint,
per channel, including non-physical shapes he prefers. `_DeepBlueStrength: 0.45` is standing evidence that the
physical answer was **already overridden by hand** (ADR 0027 finding 1). Beer-Lambert on the base colour would
remove owner control and add no expressiveness. It is **rejected**, permanently.

**Why the bottom needs it.** The LUT cannot describe the bottom at all — see §17.1 for the three ways the old
alpha-blend approach failed. Handing the shader the bottom's **albedo** is what makes per-channel transmission
possible in the first place.

#### The three pieces

**(1) `_SeabedTex`, baked over the height map's rect.** The bottom's albedo, baked over the **same world
rect** as `_HeightTex` — `_HeightWorldMin` / `_HeightWorldSize`, ADR 0014's established pattern — so the
shader needs **no new uniform** to place it and the bottom is registered to the elevation that decides how deep
it is. Its **alpha is COVERAGE, not opacity**: where the terrain painted no ground tile (the Deep / Channel
types deliberately CLEAR theirs) coverage is 0, nothing is composited, and open water with no baked bed is
**unchanged by construction**. Off the baked rect the shader zeroes coverage rather than smearing the Clamp
edge texel across the sea.

**(2) Per-channel Beer-Lambert.**

```hlsl
float3 sigma = max(_Turbidity, 0) * max(_AbsorptionRatio.rgb, 0);   // 1/m, per channel
float3 T     = exp(-sigma * (2.0 * max(depthC, 0)));                // 2d: light descends AND returns
T            = AbsorptionBand(T, _AbsorptionBands);                 // posterize (ON by default)
col.rgb      = lerp(col.rgb, bed.rgb, saturate(T) * saturate(bed.a) * inRect);
```

The path is **2d**, not d — light descends the column, reflects off the bottom and comes back. σ is factored as
**one** turbidity scalar × a fixed per-channel ratio, which is what lets ADR 0017 ease turbidity per weather
through a *float* while the per-channel character stays authored art. Red extinguishes first at the default
ratio `(1, 0.18, 0.08)`, so the characteristic depth-colour shift comes free — a sandy bottom goes warm →
green → gone rather than merely dimming. **One turbidity parameter replaces the two independently-tuned depth
constants** the old path needed (`_ShallowSeeThroughDepth` = 0.6 m against the ramp's own 0.15/4.0 m axis).

It reads `depthC` — the **cosmetic** organic-fringe depth (`== depth` when `_ShoreNoise = 0`) — so the bottom
fades with the *visible* shore rather than a clean iso-contour. Read-only, as always.

**(3) Pixelized + posterized.** The seabed sample coordinate is snapped on the **world** PPU grid (the
`Pixelize` helper — the crawl law, §3), so a bottom cell belongs to a place on the seabed and stays there while
the camera pans; the texture is imported **Point + Clamp** so nothing smears between cells. `_AbsorptionBands`
then quantizes **transmission** into discrete steps, **default ON** — the concrete form of ADR 0027's
"every layer carries its own quantization control", which matters here precisely because `_DepthBands: 0` means
the base ramp contributes no pixel character of its own. Quantizing T (not depth) makes the steps crowd where
the bottom is actually fading.

#### Passthrough — twice over

`[Toggle(_USE_SEABEDTEX)]` is **off** on the shipped material, so the whole block **compiles out**; and inside
it, `_Turbidity = 0` skips it anyway (`ABSORPTION_EPS`). Either alone is exact.

> ⚠️ **σ = 0 means "no absorption model", NOT "perfectly clear water".** Perfectly clear water would show the
> bottom at **full** strength at every depth, so the transition from 0 to 0.001 is a deliberate discontinuity,
> not a fade-in. This is safe in practice — clear water is not a sea state, useful σ starts near 0.05, coverage
> confines the effect to the painted shallow band, and the shipped presets carry real values — but drag the
> slider knowing it is a switch at the bottom of its range.

#### Tunables (rule 6; all default to today's look)

| Property | Default | Effect |
|---|---|---|
| `_UseSeabedTex` | `0` (**OFF**) | The keyword. Off = the block compiles out entirely. |
| `_SeabedTex` | none | The bake. RGB = the bottom's albedo, **A = coverage**. Point + Clamp, no mips. |
| `_Turbidity` | `0` (**OFF**) | σ in **1/m**. **Mood-eased** (§14) — see below. |
| `_AbsorptionRatio` | `(1, 0.18, 0.08)` | Per-channel extinction ratio, red = 1. A `Vector`, not a `Color`, so it is passed through verbatim (no gamma conversion on a physical quantity). |
| `_AbsorptionBands` | `6` (**ON**) | Transmission posterize steps; 0 = smooth. |

#### Turbidity is mood-eased, which makes a murky sea a DERIVED state

`_Turbidity` joins `WaterSurface.MoodFloatNames`, so ADR 0017 eases it per weather **from the eight preset
materials in `Art/Materials/WaterPresets/`, not from `Water.mat`** (§14.3 — tuning a mood-eased prop in
`Water.mat` does nothing at runtime). The shipped spread:

| Preset | σ (1/m) | Preset | σ (1/m) |
|---|---|---|---|
| `Water_Tropical` | 0.12 | `Water_NorthAtlantic` | 0.6 |
| `Water_GlassyCalm` | 0.25 | `Water_FoggySmother` | 1.2 |
| `Water_DeepBlue` | 0.3 | `Water_StormGrey` | 1.6 |
| `Water_WarmShelter` | 0.5 | `Water_StirredBrown` | **3.0** |

`Water_StirredBrown` stops being a hand-picked colour and becomes **high σ over the same painted ramp** — the
sea goes murky because the water is murky. These values are inert until the owner ticks `_UseSeabedTex` and
assigns a bake; the `Water.mat` baseline stays 0.

#### The bake tool

`Hidden Harbours ▸ Dev ▸ Bake Seabed Texture (_SeabedTex)` — an explicit region rect (auto-filled from the open
scene's `WaterSurface.HeightWorldRect`), a resolution, and the scene's ground Tilemap as the source. It writes
an external PNG **next to the painted height map** (`Data/Terrain/<base>_SeabedTex.png` — the `_HeightTex`
convention) and configures the importer: sRGB **on** (colour, unlike the height map's linear metres), Point,
Clamp, no mips, uncompressed, alpha from input. Tile pixels are read via a **GPU readback** (blit → RT →
`ReadPixels`) so no importer is mutated behind the owner's back — which also means the tool needs a graphics
device and is **editor-only**; nothing in it is reachable from a test.

**Budget (rule 7), measured:** 512² RGBA32, point-filtered, no mips, uncompressed = **1.0 MB** of texture
memory for a whole region, against `_HeightTex`'s 192² R8 = **36 KB**. Over St Peters' 160 × 120 m rect that is
0.31 × 0.23 m per texel (≈ 10 × 7.5 screen pixels at PPU 32) — which the world-grid pixelize and
`_AbsorptionBands` posterize further downstream. A bottom, not a photograph. 256 → 0.26 MB, 1024 → 4.2 MB; the
field is exposed so the owner can trade.

#### Composition + guard

The composite sits **after** the depth block settles the base colour and after the deep-blue enrichment, and
**before** every additive layer — so swell tint, FBM, specular, caustics and foam all ride **on top** of the
composited bottom, which is where they physically belong. It is upstream of the palette guard-rail (§13) and
the post-grade compensated light content (§11.6), both untouched. `col.rgb` only: never `depth` / `clip()` /
`_WaterLevel` / the height read / the sim (P1 integrity, rule 5). Twins: `WaterAbsorption`
(`Sigma` / `Transmission` / `BandTransmission` / `Composite`) and `SeabedBake` (the world↔texel mapping the
bake and the shader must agree on) — **change one, change both in the same PR.**


### 17.8 Activation — the cove's committed bake, and why it is baked from ELEVATION

§17.7 shipped the capability dormant. This is what turned it on for the current playable region.

**The bake source had to change, because the cove has nothing painted.** §17.7's bake tool reads the
ground TILEMAP — the honest source where a bottom has been painted. The committed `Greybox.unity` has
**no tilemap at all** (it carries markers, a GameRoot and a Wharf; the whole `--LOGIC--` tree is
builder output, created on a builder run and never committed). A tilemap bake there is a fully
transparent texture and absorption stays invisible. So the tool gained a second source: **terrain
ELEVATION through `SeabedPalette`**, which needs no painting because every region already authors its
depth.

> ⚠️ **`SeabedPalette` is deliberately NOT `World.TerrainHeightPalette`.** That one is the editor's
> hypsometric DESIGNER OVERLAY and its underwater stops are navy and blue, because it is showing you
> how deep something is. Feed it to `_SeabedTex` and you paint the water's own colour onto the bottom:
> the blue doubles up and absorption — whose whole job is taking the red out of a warm bottom — has
> nothing left to take. A seabed is sand, silt and rock; the blue is the water's job, and the water
> already does it. Pinned by a test that asserts the bottom is never bluer than it is red.

**The bake is reproducible, not hand-made.** `Hidden Harbours ▸ Dev ▸ Bake Cove Seabed` (and
`CoveSeabedBakeEntry.Run` for `-executeMethod`) builds the cove's terrain **from the builder's own
constants** in a throwaway object — no scene opened, so it works from a clean checkout and there is
nothing that could dirty a committed scene. A committed texture nobody can regenerate is a texture
nobody can correct.

Committed: `Data/Terrain/CoveSeabed_SeabedTex.png`, 512² over the 80 × 50 m cove =
0.16 × 0.10 m per texel (≈ 5 × 3 screen px at PPU 32), **1.0 MB RGBA32**, LFS-tracked like every PNG.

### 17.9 `_SeabedTex` is per-REGION, and the trap that makes that load-bearing

**`Water.mat` is shared by every region**, and a bake belongs to ONE region's world rect — assigning it
on the material would stretch the cove's bottom across St Peters' coast. So the texture rides the same
per-surface `MaterialPropertyBlock` push the world rect already uses: `WaterSurface.ConfigureSeabedTexture`,
wired by each region's builder.

> ⚠️ **And a region with no bake must PUBLISH a transparent 1×1, not leave the slot unbound.**
> `_USE_SEABEDTEX` is a MATERIAL keyword, so turning it on for the region that has a bake turns the
> shader block on for **every** region sharing the material. A surface that pushed nothing would then
> sample the material's default `"black"` texture — which is **opaque** black, alpha 1 — and composite
> a black bottom across its whole shallows. `WaterSurface` therefore pushes an explicit clear 1×1 on
> enable, unconditionally, so "no bake" means coverage 0 and composites nothing. Same lesson, same
> shape, as the interior guard's black 1×1 and the reflection target's clear one. Pinned by a test.

## 18. Current drift lines — the tide's SET reads on the surface (Arc C water visuals)

Faint foam **streaks aligned with the tidal current** so the player can **read which way the sea is setting**
(P1 *The Sea Has Moods*) — the same way real drift lines, foam windrows, and slicks betray a tide rip. It ships
**OFF** (`_DriftLineStrength = 0`), so the shipped `Water.mat` look is byte-identical until the owner dials it in
(rule 6, like `_ReflectionStrength`). It lives entirely in `HiddenHarboursWater.shader` and touches **only
`col.rgb`** — never `depth` / `clip()` / `_WaterLevel` / the height read / the sim (P1 integrity, CLAUDE.md rule 5).

### 18.1 It reads the CURRENT for free (NO new C# uniform)

The lines are built from the **same `_FlowDir` / `_Flow`** the surface scroll already uses. Those are pushed by
`WaterSurface.cs` from **`EnvironmentSample.CurrentVector`** — the tide's **smoothed set** (direction + speed) via
the `CurrentModel`. So the streaks orient with, and drift downstream along, the live current with **no new uniform
push**. This is the same "reuse an already-published uniform" trick the sky reflection (§11.1) uses with the
sea-state — the cheapest correct wiring.

Note the **correction baked into the design**: the aniso streak basis is keyed to **`_FlowDir` (the current)**, not
`_WindDir` (the wind) — the wind drives *roughness / whitecaps* (§5.8), the current drives *where the water is
going*, which is what a drift line shows.

### 18.2 The streak build (`col.rgb` only)

A small HLSL helper `DriftLines(worldXY, dt, t)` added in the **same pre-grade dressing zone the foam + whitecaps
occupy** (after the whitecap block, **before** the palette guard-rail §13), so the guard-rail bounds it like all the
other dressing:

- **Flow-aligned anisotropic basis** — the wind-streak idiom (§5.8), keyed to the current:
  `flowdir = normalize(_FlowDir.xy)`, `flowperp = (-flowdir.y, flowdir.x)`.
- **Advance downstream over time** — `along = dot(pp, flowdir) / _DriftLineStretch − t·_Flow·_DriftLineSpeed`, so
  the streaks **travel with the current**. The along-axis is **stretched** by `_DriftLineStretch` so a round noise
  cell reads as a long thin lane running *with* the flow.
- **Thin ridged-noise lanes across the flow** — the shader's own `pow(saturate(1 − |g1−g2|·k), n)` ridge idiom
  (the same one the caustics/moon glitter use) over two `ValueNoise` samples of the stretched coord → bright thin
  veins = the streaks.
- **Wander** — a slow low-freq `ValueNoise` nudge on the along-coord so the lanes **bend and drift** instead of
  reading as a marching ruler grid.
- **Pixelized** coords throughout (pixel-art faithful, ADR 0010), and the noise is the shader's existing
  `ValueNoise` / `Hash21` + `_Time.y` — **deterministic, no new RNG** (rule 5).
- Tinted faintly toward `_FoamColor` (or the optional `_DriftLineColor`), added — *streaks, not a paint layer*.

### 18.3 The sea-state WINDOW — a BELL, not a fade

The lines **peak on calm-to-moderate water** and are **zero on dead glass** *and* **zero in a storm's chaos**:

- **Zero on dead glass** so the glassy mirror (§11) stays a mirror — a drift line on perfectly still water would
  read as noise, not information.
- **Zero in a storm** because whitecaps + chop (§5.11) already scream the sea-state; drift lines there would just
  add mud to the "foggy white soup" the whitecap rework fought.

So it is a **band over `_Chop`**, not a monotone fade: rises from `_DriftLineSeaStateLo`, holds through the middle,
falls back to 0 by `_DriftLineSeaStateHi` (`rise·fall` of two `smoothstep`s). It **also** eases **down as wind
roughness `_Roughness` rises** (the foam-dodge) so the streaks don't fight the whitecaps, and fades out at the very
**shore** via the read-only depth key `dt` so they live on **open, navigable water**, not the wet foam edge.

> Implementation note: the foam-dodge gates on **`_Roughness`** (a CBUFFER uniform, always in scope), **not** the
> block-local `foamCoverage` (which is computed *inside* the foam branch and is out of scope where the lines are
> added). Simpler and correct.

### 18.4 Tunables (rule 6; all default to today's look)

| Property | Default | Effect |
|---|---|---|
| `_DriftLineStrength` | `0` (**OFF**) | Master; 0 = `col.rgb` untouched (today). The owner's main dial. |
| `_DriftLineSpeed` | `0.5` | How fast the streaks drift downstream, as a multiple of `_Flow`. |
| `_DriftLineStretch` | `5` | Along-flow stretch — higher = longer, thinner lanes. |
| `_DriftLineScale` | `0.3` | Lane density (lanes per world unit). |
| `_DriftLineSeaStateLo` | `0.05` | `_Chop` where the lines start rising (below = glass, none). |
| `_DriftLineSeaStateHi` | `0.6` | `_Chop` where the lines are gone (above = storm, none). |
| `_DriftLineColor` | `(…, a=0)` | Optional streak colour; **`a = 0` reuses `_FoamColor`**. |

**How the owner steers it:** raise `_DriftLineStrength` on **calm-to-moderate** water and watch faint streaks trace
the current across the surface — they vanish on dead glass and vanish in a storm. `_DriftLineStretch` /
`_DriftLineScale` tune how ropy vs fine the lines read; `_DriftLineSpeed` how briskly they run with the set.

### 18.5 Composition + guard

`DriftLines` returns an additive `col.rgb` term placed **after** the whitecap block and **before** the palette
guard-rail grade (§13, `col.rgb`-only) and the post-grade compensated light content (§11.6) — the same slot the
foam + whitecaps occupy, so the guard-rail bounds it and it composes cleanly with everything downstream. No
`WaterSurface.cs` change is needed (it reuses `_FlowDir` / `_Flow`). The shipped `Water.mat` variant is
force-compiled by `WaterShaderCompileGuardTests`, so any HLSL slip fails CI **red** (not magenta-in-build).


### 18.6 The Arc C upgrade — the lines join the SHARED drift and the SHARED field

§18 shipped drift lines that read the current on their own and knew nothing about the wave field
underneath them. Three knobs join it, each defaulting to the shipped behaviour **bit-for-bit**. Nothing
here duplicates the streak build, the bell, the wander or the tint — **this is an upgrade of §18, not a
second drift-line layer.** A parallel copy is exactly the mistake #323 spent a PR undoing.

#### (1) The basis follows the SHARED foam drift (`_DriftLineFoamDrift`)

§18.1 keyed the streak basis to `_FlowDir` and recorded that as a deliberate correction against using
`_WindDir` — *"the wind drives roughness / whitecaps, the current drives where the water is going."*
That reasoning was right about wind-vs-current and **still incomplete**: the foam on this very surface
already drifts along `FoamDriftDir()`, a wind/current blend that also carries the shoreward bias, so the
lines and the foam they are *made of* were reading two different directions. A real windrow follows the
blend, not either force alone.

So the choice becomes a dial: `0` = today's raw current, `1` = the shared `FoamDriftDir()`. It reuses
the shared function rather than re-deriving a blend, so it cannot drift out of step with the foam.

#### (2) Scum GATHERS where the surface converges (`_DriftLineConvergence`)

A drift line is not a texture that happens to be long — it is floating material **collected on a
convergence line**, which is why real ones sit in bands with clean water between them. The shared field
already exposes that term: ADR 0027 #3's `ConvergenceGate` (twinned by `WaterFoam.Convergence`). The
lanes multiply by it, so the drift lines and the convergence **foam** agree about where the surface is
folding instead of holding two opinions about one piece of physics. Four `WaveFieldSample` taps, inside
`if (_DriftLineConvergence > 0.001)` — unreachable at the default.

**Measured:** at full weight the gate retains **11.6%** of the lane energy — the lanes really are
confined to bands rather than merely dimmed.

#### (3) The grid is chosen, not inherited (`_DriftLineGrid`)

ADR 0027's Pixelation section asks for deliberately **different** grids per layer rather than every
feature edge snapping to one shared lattice. `PixelizeGrid(p, divisor)` gives this layer its own cell as
a multiple of the PPU cell; divisor 1 is bit-identical to `Pixelize`.

> ⚠️ **The finding worth recording: the drift lines were ALREADY coarser than the caustics, by
> accident.** `DriftLines` pixelizes the *scaled* coordinate (`Pixelize(worldXY · _DriftLineScale)`), so
> its quantum is `1/ppu` in scaled space and `1/(ppu·scale)` in world metres — at PPU 32 and the shipped
> `_DriftLineScale` 0.3 that is **10.4 cm**, against the caustics' **3.1 cm** on the raw world grid. The
> hierarchy the ADR asks for partly existed before anyone chose it. The divisor makes it deliberate and
> tunable; `WaterDriftLines.WorldCellMetres` is where the arithmetic lives, and a test pins it.

**Recommended: `_DriftLineGrid` = 3** (≈ 31 cm cells) when the layer is dialled in. A drift lane is
*metres* long, so a cell that size reads as lane texture; the 10.4 cm default reads closer to pixel
noise at lane scale. Left at 1 so the default is the passthrough — this is a recommendation for the
preset, not a new default.

#### Passthrough — what proves what

| Claim | Proved by |
|---|---|
| The **shipped** look is untouched | Inspection: `_DriftLineStrength` is 0, so `DriftLines()` returns before it reaches any new code. |
| The three defaults are **bit-exact** identities | `WaterDriftLinesTests`, with **exact** equality (no tolerance) — blend 0 short-circuits before the lerp, convergence 0 returns literally 1, divisor 1 lands on `Pixelize`'s lattice. Plus the two unreachable `if` guards. |
| No **visible** change at the defaults, on a GPU, through the real material | `DriftLineProbeTests`, against a **measured temporal floor** (see below). |

> ⚠️ **Why the probe measures a floor instead of asserting byte-identity.** This water is time-driven and
> `_Time` advances between `Camera.Render()` calls, so two frames of the *same* material differ —
> **9366 px** on that test's first draft, which a naive byte-comparison reported as a broken passthrough.
> The probe now measures the floor first (**6505 px**) and states each claim against it: the knobs at
> their defaults move **4993 px** (inside the floor), while swinging the basis onto the shared drift
> moves **26 230 px** (4× the floor).

#### Tunables (rule 6; all default to today's look)

| Property | Default | Recommended when dialled in | Effect |
|---|---|---|---|
| `_DriftLineFoamDrift` | `0` (today's current) | `1` | Swing the streak basis onto the shared `FoamDriftDir()`. |
| `_DriftLineConvergence` | `0` (**OFF**) | `0.6` – `1` | Confine lanes to convergence lines (bands with clean water between). |
| `_DriftLineGrid` | `1` (today's grid) | `3` | This layer's pixel cell, in multiples of the PPU cell. |

`col.rgb` only — never `depth` / `clip()` / `_WaterLevel` / the height read / the sim (P1 integrity,
rule 5; the interior-mask clamp stack never notices). Twin: `WaterDriftLines`.

## 19. Surface rain rings (night-visible) + storm foam lanes (Arc C water visuals — final piece)

The closing Arc C shader pass adds two opt-in `col.rgb`-only dressings that read the live sea mood: **surface
rain rings** (dimple rings where rain strikes the water) and **storm foam lanes** (long downwind foam streaks
that come up in a blow). Both default **OFF** (strength `0` = today's look byte-identical) and, like every
water dressing, never touch depth / `clip()` / `_WaterLevel` / the height read / the sim (P1 integrity, rule 5).
They sit in **opposite** day/night buckets on purpose — see below.

### 19.1 The shared `_RainIntensity` derivation (derived ONCE in C#, never in HLSL)

Rain has no signal in the sim, so its strength is **derived** from two mood axes exactly like the falling-rain
particles (§`RainEmitter`, PR #156): `AmbientParticleMath.RainIntensity(visibility, seaState01, baseline,
seaStateWeight, visOnset, visFull, seaOnset)` — rain is an **occasional squall** that needs BOTH real murk AND
real chop, via **two onsets, not a leaky linear gate**: a **murk gate** (`smoothstep(visOnset, visFull,
visibility)`, `0` while the air is clear at/above `visOnset`, ramping to `1` as visibility falls to `visFull`)
times a **sea-state onset** (`smoothstep(seaOnset, 1, seaState01)`, `0` on near-glass). So a clear or
lightly-choppy night stays **dry** — the fix for the owner playtest where the old `(1-g)+g·fog` gate leaked
~40% of the sea-state drive through even in perfectly clear air (constant rain on any Moderate sea).
`WaterSurface.PushUniforms` computes it **once** (reading the deterministic `EnvironmentSample`) and pushes it
to the cached `_RainIntensity` uniform right next to the `_Chop` push. The shader **never re-derives** rain from
`_Chop`: `_Chop == SeaState01` today but is a distinct retunable knob, so the C# passes `s.SeaState01` directly.
`_RainIntensity` is a **physics-style derived push, NOT a per-mood colour**, so it is deliberately kept **out**
of `MoodFloatNames` (putting it there would double-drive it via the weather-palette blend).

The shape floats are serialized on `WaterSurface` (`_rainBaselineIntensity` `0`, `_rainSeaStateWeight` `1.0`,
`_rainVisOnset` `0.65`, `_rainVisFull` `0.40`, `_rainSeaOnset` `0.30`) with defaults **matching
`RainConfig.Default`** so the surface **rings** and the falling **rain particles** agree out of the box. **If
the owner retunes rain feel, match BOTH** this and `RainEmitter`'s `RainConfig` — a future refactor can unify
them into one shared rain config (flagged, not built here).

### 19.2 Surface rain rings — `RainRings()` (`col.rgb` only; **night-visible, post-grade compensated**)

Expanding concentric **dimple rings** stippled over the water where rain strikes (P1). A pixelized value-noise
grid (`_RainRingScale`) seeds ring **centres**: each cell that passes the `_RainRingDensity` lottery (a stable
per-cell `Hash21`) hosts one raindrop strike, its centre jittered inside the cell and its phase offset per-cell
so the rings do not pulse in lockstep. `RAINRING_TAPS` concentric rings expand from each centre — radius =
`frac(strike phase)` so a ring is born at the centre, grows, and recycles; a thin bright edge (a narrow band
around the growing radius) is the ring line, fading as it expands (a dying ripple). The whole term is gated by
the derived `_RainIntensity` and masked to **open water** via the **read-only** depth key (`dt`) so rings never
stipple the dry shore. The tap count is a **compile-time `#define`** driving a bare `[unroll]` (the `FBM_OCTAVES`
idiom) — **never an `[unroll]` over a runtime count** (the #96 magenta trap).

**OWNER RULING (2026-07-05): the rings must STAY VISIBLE THROUGH THE DARK — a night squall still shows rain on
the water.** So `RainRings()` is added in the **post-grade, overlay-compensated** light block (§11.6), folded
into the same `lightContent` bucket as the boat beam + moon/sun glitter: `float3 lightContent =
BoatLightTerm(...) + skyNightRGB + RainRings(...)`. That bucket is divided by `max(_DayNightTint.rgb,
DN_COMP_MIN_CHANNEL)` when the day/night cycle runs, so the downstream night **MULTIPLY** (ADR 0013) cancels
and the rings read on **black water day AND night**; when the cycle is off (edit mode / bare art / demo) the
same branch adds the content **raw**. This is the deliberate opposite of the storm foam lanes below.

### 19.3 Storm foam lanes — `StormFoamLanes()` (`col.rgb` only; **dims with the night** like the foam)

Long **downwind** foam streaks that come up in a building sea (P1) — the storm sibling of the drift lines (§18),
but keyed to the **wind** (the `_WindDir` aniso basis reused from the whitecaps) not the current, and gated by
`_Roughness` as a **monotone** rise (`blow = _Roughness²`): **gone on calm, strong in a gale** (not a bell).
It reuses the living-whitecap `EvolvingField` + the `pow(saturate(1 - |g1-g2|·k))` ridged-lane streak idiom,
the coord **stretched along the wind** by `_StormFoamLaneStretch` so a round cell reads as a long thin lane,
streamed downwind over time (`t · _Flow`). Depth is read **only** via `dt` (fade at the wet shore edge). Its
locals are named `laneAlong` / `laneAcross` to avoid shadowing the `cross` intrinsic / other helpers' locals.

**Tightened to crisp streaks (owner playtest, 2026-07-05):** the ridge exponent was raised `3.0 → 5.0`
(`pow(saturate(1 − |g1−g2|·2.2), 5.0)` — thinner, more defined veins) and the output multiplier dropped
`0.4 → 0.25`, so the lanes stay **tight streaks even at max `_StormFoamLaneStrength`** instead of blooming
into a broad white wash. `_StormFoamLaneStrength`'s default is unchanged (`0` / off).

`StormFoamLanes()` returns an additive `col.rgb` term placed **pre-grade**, right after the whitecap block and
before the drift-line call — the **same** foam dressing zone the whitecaps occupy — so the palette guard-rail
(§13) bounds it **and** so it **dims with the night** overlay like the rest of the foam. That is the opposite of
the rain rings, which sit post-grade in the compensated bucket to survive the dark.

### 19.4 Tunables (rule 6; all default to today's exact look)

| Property | Default | Meaning |
|---|---|---|
| `_RainIntensity` | `0.0` | **C#-driven** (derived), not hand-tuned; `0` = no rings. |
| `_RainRingStrength` | `0.6` | Master rain-ring strength; `0` = an exact passthrough (off). **Raised 0 → 0.6 on `Water.mat` by the 2026-07-31 activation pass**, once the owner ratified the weather look-target — the rings had shipped dark since Arc C. Useful band ≈ `0.35` (a whisper) … `0.9` (a hard squall); past ~`1` a heavy squall starts to read as white confetti, because the derived `_RainIntensity` already multiplies in. Deliberately **not** in `MoodFloatNames`: `_RainIntensity` scales the rings by {sea-state, visibility} and the weather blend runs on those *same* two axes, so mood-blending the strength would multiply one signal by itself. Every `WaterPresets/*.mat` therefore carries the key with a per-mood value (fog `0.45`, storm `0.75`, else `0.6`) — not for blending, but because the editor's "Apply water preset" is a wholesale `CopyPropertiesFromMaterial` and a preset missing the key would stamp the shader default `0` onto `Water.mat` and switch the rain off. |
| `_RainRingScale` | `6.0` | Ring-centre cell scale (**cells/unit — BIGGER = smaller rings**; the label misled: it is cells-per-unit, so a larger value shrinks each ripple). Raised 0.4 → 6.0 (owner playtest, 2026-07-05): at 0.4 one cell was 2.5 world units, so a ripple spanned ~2.5 tiles (a dinner plate); at 6.0 a cell ≈ 0.17 units → fine sub-tile dimples. Pure default change, no math (radius/band are already in cell-units and shrink with the scale). |
| `_RainRingDensity` | `0.35` | Fraction of cells that host a strike. Dropped 1.0 → 0.35 (owner playtest) so drops **scatter sparsely** instead of striking every cell. |
| `_RainRingSpeed` | `1.5` | Ring expansion speed (rings/sec). |
| `_RainRingColor` | pale cool white | Ring line colour. |
| `_StormFoamLaneStrength` | `0.0` | Master storm-lane strength; `0` = off / today. |
| `_StormFoamLaneStretch` | `6.0` | Along-wind stretch (thin lanes). |
| `_StormFoamLaneScale` | `0.3` | Lane scale (lanes/unit). |

Plus the C#-side shape floats on `WaterSurface`: `_rainBaselineIntensity` `0`, `_rainSeaStateWeight` `1.0`,
`_rainVisOnset` `0.65`, `_rainVisFull` `0.40`, `_rainSeaOnset` `0.30` (mirror `RainConfig.Default`).

**How the owner steers it:** the rain rings are **on** (`_RainRingStrength` `0.6`); raise or lower that one
number on `Water.mat` to taste, and raise `_StormFoamLaneStrength` (still `0`/off) if you want the storm lanes
too. Then sail into a building blow. Rain rings dimple the surface and **read even at night** (per the owner
ruling); the storm lanes streak **downwind** and **dim with the dark** like the foam they belong to. The
surface rings and the falling-rain particles share the one derived `_RainIntensity`, so they thicken together —
and both stay **dry** until the weather brings **real murk AND real chop**.

### 19.5 Composition + guard

`StormFoamLanes` is `col.rgb`-only, added **pre-grade** with the whitecaps (bounded by the §13 guard-rail).
`RainRings` is `col.rgb`-only, added **post-grade** inside the §11.6 overlay-compensated `lightContent` bucket
(so it survives the night multiply). `WaterSurface.cs` gains the derived `_RainIntensity` push (reusing the
shared `AmbientParticleMath.RainIntensity`). Arc C shipped `Water.mat` **byte-identical OFF**; the
**2026-07-31 activation** turned the rings on there (`_RainRingStrength` `0.6`) and wrote the ring key set into
the whole preset library, changing **no HLSL** — the shader block was already opt-in by design. The shipped
`Water.mat` variant is force-compiled by `WaterShaderCompileGuardTests`, so any HLSL slip fails CI **red** (not
magenta-in-build); `RainActivationTests` pins that the rings stay on and that no preset can silently zero them.

## 20. Aesthetic pass — clumping foam, deep blues, crest-face shading (owner mandate, 2026-07-08)

> The owner delegated taste (verbatim): *"feel free to tune the water in whatever fashion you think will lead
> to better looking waves, better clumping of foam, deep blues."* This pass builds ON his committed baseline
> (#183 — his own `Water.mat` tuning is the canon this refines, never bulldozes; his locked `_Flow`/`_WindChop`
> stay locked and untouched) and COMPOSES with the #182 swell-read. Three additive levers, one per ask, each a
> named tunable defaulting **ON at a modest strength** (this pass IS the mandate) and each an **exact
> passthrough at 0**. All three are `col.rgb`/`col.a` dressing only — never `depth`/`clip()`/`dt`/
> `_WaterLevel`/the height read/the wave-field sample/the sim (P1 integrity, CLAUDE.md rule 5) — and all three
> sit **pre-grade**, so the §13 palette guard-rail remains the single final colour owner and bounds them like
> every other layer (they are water colour, not light content — they correctly dim with the night overlay;
> the §11.6 post-grade compensated bucket is untouched).

### 20.1 Deep-blue enrichment (the `_USE_DEPTHRAMP` trap, resolved)

The shipped material's base colour comes from the owner's **hand-painted `_DepthRamp`** (`_USE_DEPTHRAMP` ON) —
the `lerp(_ShallowColor, _DeepColor, dt)` path does not run, so `_DeepColor` alone is inert, and repainting his
ramp would bulldoze his art. The lever chosen instead: a **bounded pull of the settled base colour** toward a
rich navy, keyed to the read-only deep fraction `dt`, applied immediately after the base block — **before every
additive layer** (the #182 swell-read, swell bands, spec and foam ride on top at full amplitude; nothing is
washed out) and **before the guard-rail**. `smoothstep(_DeepBlueStart, 1, dt)` leaves the shallows and mid ramp
untouched. The default target `(0.02, 0.09, 0.30)` is the deeper-saturated cousin of the owner's own
`_PaletteDeep` anchor `(0.02, 0.08, 0.26)`, so the grade's anchor pull agrees with it; the enriched deep sits
below his `_PaletteSatCap 0.78` and above his value floor `0.08`, so the rail neither greys it nor lifts it.

| Property | Default (Water.mat) | Meaning |
|---|---|---|
| `_DeepBlueStrength` | `0.45` | Master pull toward the navy; `0` = the painted ramp exactly. |
| `_DeepBlueColor` | `(0.02, 0.09, 0.30)` | The navy target (per-mood: FoggySmother pins strength `0`, StormGrey `0.1`, Water_DeepBlue `0.7`). |
| `_DeepBlueStart` | `0.25` | The `dt` fraction where the pull begins (shallower water untouched). |

### 20.2 Foam clumping — windrows + crest-shed rafts (`_FoamClump*`)

The open-water whitecaps read as an **even sprinkle** — organic per-fleck (§5.9) but statistically uniform.
Real foam **gathers**: wind rows (lanes of foam down the wind) and rafts shed by breaking crests, with bare
water between. A second, much **broader and slower** `EvolvingField` (reused helper; pixel-snapped; evolving at
0.35× the foam boil rate — rafts morph slower than the flecks riding them), **stretched along the wind** like
the caps and sampled on the same drifted coord, **REDISTRIBUTES** the cap coverage: a soft patch mask
(`smoothstep(0.35, 0.65, field)` around the field's midline) lifts in-patch coverage ×1.25 (saturated) and
thins between-patch coverage toward bare water. The same foam, gathered instead of thinned. Applied to **both**
whitecap paths (trains-live and legacy) via one gate on `capOpacity`; the §5.3 shoreline fringe is deliberately
untouched (the sprinkle complaint is the open water; the fringe already has the swash/churn character).

| Property | Default (Water.mat) | Meaning |
|---|---|---|
| `_FoamClumpStrength` | `0.55` | Master gathering; `0` = today's even sprinkle. |
| `_FoamClumpScale` | `0.10` | Patch frequency (patches/unit) — smaller = broader rafts, wider clear lanes. |
| `_FoamClumpStretch` | `2.5` | Wind anisotropy — `1` = round rafts, higher = long thin windrows. |

### 20.3 Swell face shading — the modelled wave (`_SwellFaceShade`)

`WaveFieldSample()` already computes the field's **analytic slope** (`waveSlope`) for twin parity — previously
unused in the composite. This shades each swell face against the **one implied sun** (`_SunDir`, falling back
to `_LightDir` — the ADR 0006 single-light discipline, the specular's exact fallback): the surface normal's
ground component is minus the height gradient, so `-dot(waveSlope, lightDir)` is positive on the **lit face**
and negative behind the crest. Where the #182 swell-read is **symmetric** (crest bright / trough dark), this is
**antisymmetric** (lit face vs shaded back) — they compose into a directional, modelled wave instead of
doubling one band into glare (combined worst-case crest add ≈ 0.16 pre-grade, inside the rail's ceiling). The
×2 slope normalizer and the 0.15 add ceiling follow the swell-read's documented-constant idiom (at the 0.22
default the swing is ±0.033 — shading, not glare). **Self-gating:** glass publishes zero amplitude ⇒ zero slope
⇒ zero term (the §11 mirror is untouched); the legacy count-0 path leaves `waveSlope` at 0 (pre-B1 look
unchanged there). No new uniform and **no C# twin needed** — it consumes `WaveFieldSample`'s existing outputs
(the #182 precedent); `WaveMath`/`ShaderTwinSample` are untouched.

| Property | Default (Water.mat) | Meaning |
|---|---|---|
| `_SwellFaceShade` | `0.22` | Lit-face/shaded-back contrast; `0` = flat bands (FoggySmother pins `0` — flat fog light). |

### 20.4 Composition, registration + guard

Perf (rule 7): +1 `EvolvingField` (4 value-noise taps) per **whitecap-branch** pixel when clumping is on, one
`smoothstep` chain for the deep pull, one dot/clamp for the face shade — no new texture fetches. The new float
keys join `WaterSurface.MoodFloatNames` (+ `_DeepBlueColor` in `MoodColorNames`) so the §12 preset library and
the §14 weather blend ease them per mood — look props only, disjoint from the physics set (no double-drive).
`Water.mat` serializes the new keys explicitly at their defaults (and pins the previously-unserialized
`_SwellReadStrength 0.35` / `_SwellReadBands 0`); the owner's tuned values are otherwise byte-identical. The
shipped `Water.mat` + every `WaterPresets/` variant stay force-compiled by `WaterShaderCompileGuardTests`
(no `+` in any `[Header]`, no `[unroll]` over a runtime bound — the magenta class stays guarded).

## 21. The shore seam — displacement dies at the walkable waterline (ADR 0023, displaced-water arc step 1)

The displaced-water arc (owner greenlit 2026-07-22; decision + full derivation in
`docs/adr/0023-displaced-water-surface.md`) will lift the water as a real mesh surface — the same
deterministic field, vertically displaced through the ADR 0022 off-screen pattern. Before any surface ships,
the arc's step 1 solves and proves its one hard problem: **displacement must reach exactly zero at the
walkable waterline or the coast tears** (water drawn over dry sand at a crest; a bared strip at a trough).

**The mechanism — `Core.ShoreFadeMath` (pure, additive):**

```
fade = smoothstep(0, band, depth)        depth = WaterLevelAt(t) − ElevationAt(pos)
lift = waveHeight × exaggeration × fade  (ShoreFadeMath.DisplacedHeight — EVERY consumer reads this)
```

The fade's zero set is the depth-0 iso-contour of the painted seabed **itself** — the same
`WaterLevelAt − ElevationAt` read the water shader, walkability and boat-cross already share (the one
height map gains its **fourth consumer**). As the tide moves the waterline, the seam moves with it; there is
no second contour to drift. The falloff band is **derived, not tuned** (rule 6):
`band = 2 × envelope × exaggeration × shoreGradient` (`RecommendedBandMeters` — overlap bound 1.125,
in-band fold bound 1.5, coefficient 2 holds both with margin), giving a steepness-independent ground
footprint of `2 × envelope × exaggeration` ≈ 3.1 m of shallows at the reference sea × 1.5.

**Proof shipped with step 1** (the numbers live in ADR 0023 and
`Assets/_Project/Code/Tools/Editor/ShoreSeamProof/Evidence~/proof-log.txt`):

- `ShoreFadeMathTests` (headless, CI-safe): displacement exactly 0 on the contour at three tides; shore
  transects over four profiles × three tides never cross the waterline and never fold — including the
  100%-envelope event (t = 1513.5 s, pinned as a regression guard) parked adversarially at mid-band; past
  the band the seam is bit-invisible.
- The `ShoreSeamProof` editor harness (GPU, evidence committed): rendered water/land boundary vs the
  analytic contour = **0 px** deviation on every north shore at every tide (±1 px south, sub-pixel
  rasterization); the seam-OFF control tears 31 px / gaps 50 px; open-sea render at the event moment is
  pixel-identical with the seam active (0 of 518,400).

**Contracts binding on the later phases** (the production surface, hull heave, glitter/whitecap retunes —
ADR 0023 §Phases): one sea (displace the existing field only), ONE shared exaggeration constant read through
`DisplacedHeight` by surface and hulls alike, the style law (solid bands, dithered edges, world-locked
cells, owner palette anchors), and the HLSL twin discipline — the production vertex shader's fade must be a
line-for-line twin of `Fade01`, changed only in lockstep.


## 22. The displaced surface in production — the water joins the off-screen pass (ADR 0023, arc step 2·1)

Step 1 of phase 2 puts the displaced surface IN the game, behind a dev A/B toggle (the owner's
readability verdict instrument). The wiring, for anyone touching it:

- **Two passes, one program.** `HiddenHarboursWater.shader` now holds its whole program in a
  SubShader-scope `HLSLINCLUDE`; the flat `Universal2D` pass and the new off-screen
  `HHWaterDisplaced` pass (LightMode `HHWater`) share every declaration, helper and the FULL
  fragment — the displaced sea cannot drift from the flat sea because it IS the flat sea's
  fragment on lifted geometry. The A side of the A/B is byte-identical to today: same pass, same
  vertex, same pragmas.
- **The vertex twin.** `vertDisplaced` lifts each vertex by
  `height × _WaveExaggeration × ShoreFade01(stillDepth, _ShoreFadeBand)` — the same
  `WaveFieldSample` the fragment paints with (one field, two sampling densities, same visual
  frequency scale), the same painted-seabed depth read (`SeabedElevationLod` — the LOD-0 vertex
  twin of `SeabedElevation`; the height map has no mips, so the reads are byte-identical), and the
  line-for-line HLSL twin of `Core.ShoreFadeMath.Fade01` (§21). The fragment receives the
  UNDISPLACED ground position, so `clip()`, bands, foam and every layer are painted at the ground
  coordinate and ride the lift — the walkable waterline and clip contour are untouched by
  construction.
- **The ADR 0022 route, with one refinement.** The displaced mesh joins `IsoFacetHullFeature`'s
  off-screen recording and shares the facet passes' PRIVATE depth buffer (never the scene depth —
  a depth-writing mesh there punches holes in every later sprite). It writes its OWN colour target
  (`_HHWaterScreenTex`, ARGBHalf so the night light content's pre-compensated >1 values survive;
  alpha = the water's own translucency), NOT a fifth MRT attachment: the facet buffers' alpha is
  the hull-id contract, and water pixels inside them would starve the keyline flood of the empty
  neighbours it floods into. The keyline resolve is byte-identical with or without water; the
  shared z-buffer is the part phase 3's waterline-on-the-hull needs. Membership in the off-screen
  renderer list is an EXPLICIT rendering-layer bit (`DisplacedWaterRegistry.RenderingLayer`), so
  the flat Sea sprite and the owner's preset materials — which carry the same shader — can never
  ride into the pass by accident.
- **The in-scene face** is `WaterOverlay` (`HiddenHarboursWaterOverlay.shader` + the committed
  `WaterOverlay.mat`): a quad sampling `_HHWaterScreenTex` at its own SV_Position, sorted through
  a SortingGroup at the flat sprite's exact layer/order — boats, characters and props stack
  against the displaced sea exactly as against the flat one.
- **Plumbing** (`DisplacedWaterSurface`, beside `WaterSurface` on the Sea): chunked vertex grid
  (default one vertex per 8 px — the ADR perf envelope; chunk math pinned by
  `DisplacedWaterMathTests`), the flat renderer's MaterialPropertyBlock copied each throttled tick
  (one sea, two representations), the displaced material a runtime instance of the LIVE Water.mat
  with the `Universal2D` pass disabled, and the fade band DERIVED each tick:
  `band = coefficient × live envelope × exaggeration × shoreGradient`
  (`DisplacedWaterMath.BandMeters`, pinned bit-equal to `ShoreFadeMath.RecommendedBandMeters`).
  Exaggeration (×1.5) and coefficient (2) are **owner data** (arc step 3, shipped):
  `GameConfig.DisplacedWater` on `Assets/_Project/Data/Config/GameConfig.asset` is the live source
  — the component re-reads it every tick, so tuning the asset in Play moves the sea within a
  second; the component's serialized fields are the unwired fallback and mirror the config
  defaults. Phase 3's hull heave reads the SAME value through `GameConfig.WaveExaggeration` (the
  shared-constant accessor — never a per-consumer copy). `DisplacedWaterConfigTests` pins
  config == shader defaults == twin constants, so a wired config is a visual no-op until the owner
  actually tunes it. The per-coast shore gradient stays on the component (scene data, not world
  policy).
- **The A/B**: `O` at runtime (rebindable, the DevBoatPicker pattern) flips flat ↔ displaced in
  place. OFF is a contract: nothing registers, the feature records nothing, the flat water renders
  exactly as today.

Still ahead in the arc (ADR 0023 §Phases): the screen-anchored-layer reviews (phase 4 — moon
glitter, rain rings, drift lines, flotsam riding the surface). The envelope-relative
band/whitecap retune (step 2) shipped — §23 below; the GameConfig exposure (step 3) shipped —
the paragraphs above and §23's threshold table; the hull waterline (phase 3 step 1) shipped —
§24 below; the shared heave + resting draft (phase 3 step 2) shipped — §24's heave-honesty
paragraph; the watertight hull clamp (the owner's 2026-07-23 flooding defect) shipped — §24's
watertight paragraph.


## 23. Envelope-relative salience — the big wave wears the solid foam core (ADR 0023, arc step 2·2)

Step 2 of phase 2 retunes the SHARED fragment (both passes inherit it — flat and displaced are one
program, §22) so the rare big wave is *marked*, by foam **and** by shade. The flat water's caps used
to mark every local crest with equal salience — the spike's control image showed the 100%-envelope
event (t = 1513.5 s, h = 1.045 m of a 1.047 m envelope) sitting invisible in uniform speckle. Now
salience keys on **height relative to the field's envelope** (`height / _WaveFieldParams.z`, the
crest factor the shared wave field already publishes — ADR 0023 §(4)):

- **Whitecap solid cores are RESERVED for near-envelope crests.** In the trains-live cap path the
  dense breaking core (`solidPart`) is gated by `CapEnvelopeGate(waveCrest, …)`: zero at and below
  the envelope threshold, a Bayer-dithered binary fringe just above it, hard 1 past the solid
  margin. Ordinary chop keeps only the thin milky residual streaks (which were already
  envelope-keyed through `crestF`). Result: the everyday tallest crest of the reference sea
  (crestF ≈ 0.61) wears no core; the envelope event (crestF ≈ 0.996) wears a solid one on every
  dither cell — both pinned by `WhitecapSalienceMathTests`.
- **Envelope value bands** (a new pre-grade layer after the swell face shading): the
  envelope-relative height (`vN = h/A × 0.5 + 0.5`) is posterized into SOLID value steps shaded
  from the owner's palette anchors (`_PaletteDeep/Mid/Shallow` — ADR 0015 keeps applying), and
  blended over the composited base at `_EnvelopeBandStrength`. Because the axis is
  envelope-relative, the TOP band is reachable only by a near-envelope crest — the big wave is
  marked by shade even before its foam. Gated by trains-live, the glass gate (`envelope × 40`) and
  the shared modelled-swell calm gate (`_SwellReadSeaStateLo/Hi`) — glass stays glass, calm stays
  serene, and the bands melt with the swell they mark.
- **The style law, mechanised (ADR 0023 §(3)):** `BandValue01` dithers ONLY inside a window around
  each rounding boundary — outside it the step is hard (solid bands, dithered edges; full-range
  Bayer reconstructs the smooth gradient — the spike's measured airbrush trap). The dither reads
  `BayerWorld` — the rigs' 4×4 matrix indexed by the PPU-quantised WORLD cell, zero crawl by
  construction.
- **Near shore the caps die with the SEAM** (ADR 0023 §Whitecap salience): `capOpacity` is faded by
  `ShoreFade01(depth, _ShoreFadeBand)` — the same twin, the same band the displaced vertex stage
  reads (pushed derived per tick on the displaced pass; the material default 0.5 m gives the flat
  pass a thin graceful band, and a zeroed band degrades to "no fade", never a divide). The dying
  displaced edge cannot wear open-sea caps; shore foam/swash stays the separate dressing layer,
  untouched.
- **And so do the VALUE BANDS (owner playtest 2026-07-23, "shoreline looks a bit swirly").** The
  envelope bands originally did NOT fade with the seam, so their band-edge dither drew worm
  contours crowding along the shore over the bright shallow ramp — envelope-relative shade marking
  waves that visibly are not there on the dying displaced edge. The band blend weight is now
  multiplied by `bandSeam = ShoreFade01(depth, _ShoreFadeBand)` — **the same curve, the same band
  as `capShoreFade`** (one contour, never a second). Exactly 0 at the walkable waterline, exactly
  1 past the band, so the open sea's marked-wave read is untouched and `_EnvelopeBandStrength = 0`
  stays an exact passthrough. Twin: `WhitecapSalienceMath.BandShoreSalience` (pinned in
  `WhitecapSalienceMathTests`); the rendered-frame pin (in-seam band imprint ≪ open-water imprint,
  with a degenerate-band sabotage) is
  `WaterWhiteoutShoreSwirlAcceptanceTests.ShoreSwirl_EnvelopeBands_FadeWithTheSeam`.
- **The swirl's other half — slope-blind shore cosmetics — is fixed beside it (§5.6):** the beach
  swash and the ADR 0012 fringe wiggle offset a cosmetic DEPTH, so their visible contour excursion
  was `amplitude ÷ beach slope` — metres-wide swinging worm tongues on the gently painted bar.
  Both offsets are now scaled by the LOCAL painted slope (`SeabedSlopeMag`, saturated at the
  1 m/m authoring reference), making the authored amplitudes read as CONTOUR metres on any coast
  (steep shores keep today's look; `_SwashAmplitude`/`_ShoreNoise` now mean metres of visible
  wet-edge excursion). Pinned by
  `WaterWhiteoutShoreSwirlAcceptanceTests.ShoreSwirl_CosmeticContourExcursion_IsSlopeTrue`.

**Thresholds and where they live** (rule 6 — named material properties on Water.mat, spike-tuned
defaults from `spike/3d-water` VERDICT.md / IsoWaterSpike.shader / SpikeWaterRenderer; the three
OWNER knobs moved to GameConfig with arc step 3 — `GameConfig.DisplacedWater` on
`Assets/_Project/Data/Config/GameConfig.asset`, pushed by `WaterSurface` onto the flat renderer's
MPB each tick, which the displaced pass copies, so one push covers both passes; the four style
constants stay material-level):

| property | default | owner source (step 3) | provenance |
|---|---:|---|---|
| `_CapSalienceStrength` | 1.0 | `GameConfig.DisplacedWater.CapSalienceStrength` | master; 0 = the legacy even salience, exactly |
| `_CapEnvelopeThreshold` | 0.62 | `GameConfig.DisplacedWater.CapEnvelopeThreshold` | spike `_CapThreshold` |
| `_CapSolidMargin` | 0.3 | material (style constant) | spike `_CapSolid` |
| `_CapDitherBand` | 0.25 | material (style constant) | spike `_CapDither` |
| `_EnvelopeBandStrength` | 0.35 | `GameConfig.DisplacedWater.EnvelopeBandStrength` | production blend (spike rendered full-replacement) |
| `_EnvelopeBands` | 7 | material (style constant) | spike run value (`SpikeWaterRenderer`) |
| `_EnvelopeBandDitherWin` | 0.4 | material (style constant) | spike `_DitherWin` |

The C# twin is `HiddenHarbours.Art.WhitecapSalienceMath` (`CapEnvelopeGate` / `BandValue01` /
`BayerThreshold` / `CapShoreSalience`) — line-for-line with the HLSL, changed only in lockstep;
`WhitecapSalienceMathTests` pins the twin to the reference sea's numbers AND scrapes the shader
source so a drifted property default fails red, and `DisplacedWaterConfigTests` closes the
triangle: the `DisplacedWaterSettings.Default` config values must equal the same shader defaults
and twin constants, so config, shader and twin can never disagree silently. The owner's presets
predate these properties, so a preset apply leaves them at the material/shader defaults — sane by
construction (and the config push, being per-tick on the MPB, rides OVER whatever a preset left on
the material — the config wins wherever it is wired). The three knob keys are deliberately NOT in
`WaterSurface.MoodFloatNames`: they are owner policy, not weather mood — adding them there would
double-drive the push.


## 24. The waterline on the hull — one calibrated iso z-buffer (ADR 0023, phase 3 step 1)

The owner's explicit ask ("water changes height on the hull"), the spike's probe productionised:
with the displaced sea ON, the surface truthfully covers and uncovers a mesh hull's lower planking
as swell passes — the waterline climbs the planking (~1 m per dominant period in the reference
sea, spike-measured). Two mechanisms, both riding ONLY while a `DisplacedWaterSurface` is active;
displaced OFF renders byte-identically to before this step (pinned in pixels by
`HullWaterlineAcceptanceTests`).

**The calibrated cross-object z convention.** The shared private depth buffer (`_HHHullZ`) now has
ONE meaning for every object in it:

```
z(point) = waterPlaneZ + (groundAnchorY − _HeightWorldMin.y) · cos(elev) − heightAboveStillWater · sin(elev)
```

- **The water already computes exactly this per vertex** (`vertDisplaced`:
  `ws.z += (ground.y − _HeightWorldMin.y)·_WaterIsoDepth.x − lift·_WaterIsoDepth.y`) — the water
  is the REFERENCE side of the convention and is unchanged by this step.
- **A hull joins it as ONE per-hull constant translation.** `DisplacedWaterSurface` publishes the
  frame (`DisplacedWaterRegistry.WaterIsoDepthFrame`: `_HeightWorldMin.y`, `_WaterIsoDepth.xy`,
  the sea plane's world z — read from the SAME live material the shader samples, each throttled
  tick); `IsoFacetHullRenderer.ApplyPose` translates the FacetMesh child to
  `DisplacedWaterMath.HullDepthBias(rootY, heaveMetres, frame)` =
  `baseZ + (rootY − refY)·cos − heave·sin`. Constant per hull, so every intra-hull depth relation
  — the rig's own `ry·cos − rz·sin` self-occlusion (the golden-master truth), the deck-pass
  contract, the keyline's depth-difference darkening (the resolve is id-gated and reads only
  differences between solid pixels) — is preserved exactly. Never correct the hull per vertex: the
  rig's intra-hull convention IS the golden master, and re-scaling its ground term would change
  facet self-occlusion.
- **Why the compare is truthful at the contact line:** hull planking and the water lapping against
  it share a ground anchor, so the ground terms cancel and the z-test reduces to
  `heightAboveStillWater vs surfaceLift` — water covers exactly the planking below the lifted
  surface. The residual is the baked-iso ground-term mismatch away from the contact line (rig
  ground metres vs world metres, `ry·cos(1−sin)` ≈ 0.27 m of depth per rig-metre of `ry`): a small
  static offset of the resting waterline toward the hull's near rail, NOT a motion error — the
  climb itself moves at `(cos+sin)/(cos²+sin)` ≈ 1.15 rig-metres per metre of lift at the fleet's
  40°, effectively 1:1. (In height terms the far-rail penalty is ≈ 0.17 m per rig-metre of
  half-beam; the watertight line below absorbs it as data.)
- **The hull is WATERTIGHT — the waterline climbs the planking but never boards the boat
  (owner playtest 2026-07-23: "water enters hull on the mesh models").** The truthful z-test has
  no concept of a shell: a hull's LOW interior surfaces (cockpit sole, hold floor, inner
  bulwarks, working deck) are just geometry below a big enough lift, and in a storm the
  differential between the hull's single-point ride and the local surface — wave slope across a
  hull-sized footprint, plus the beam residual above — exceeded the interior's freeboard and
  painted the sea over the boat's inside. Early phase-3 called that flooded cockpit "the known
  intermediate state"; the owner has judged it: it is a defect, and it is now impossible by
  construction. The fix stays inside the per-hull-constant discipline (never a per-vertex touch
  of the rig's own convention) and is a PER-POINT law, measured into shape in pixels — the
  lineage matters: a 1:1 differential clamp flooded the cockpit (the projections are not 1:1); a
  blanket footprint-max bound dry-docked the dragger (distant crests that cannot touch a hull
  still inflated it); a root-line-only per-point law re-flooded the far rail (the beam residual
  is per-ground-line). Each cut was adjudicated by the acceptance suite before the complete law
  below replaced it:

  - ⚠️ **TWO TERMS WERE MISSING FROM THIS LAW UNTIL 2026-07-25, and both are the same story:
    a quantity that was worth nothing when the law was written became dominant later.** The law
    below is stated for a hull whose own screen heave `H` is ZERO, and for a sea sampled at the
    published trains' true wave numbers. Neither held in production:
    (1) **The hull's own screen heave.** Its image is translated in world/screen Y by `H` while
    `HullDepthBias` anchors its depth at the root line, so the pixel-share is really
    `r(ry) = (Δ + L − H)/cos − tan·ry` and the win test carries a further `− H·cos`. Drop them and
    the demand is `H·cot(elev)` ≈ 1.19·|H| too LOW — and `H` is NEGATIVE most of the time (the ride
    always subtracts the resting draft, and the sharpened field sits below still water for most of
    its period), i.e. exactly when the boat is down in a trough with the sea standing over her. It
    was written when a mesh hull's heave was the rig's ~0.04 m rock bob; phase 3 step 2 made the
    channel metre-scale. The 0.4 m ramped safety only ever covered |H| ≤ 0.34 m.
    (2) **The drawn sea's frequency scale** — see the `_OceanSwellScale` note under "The data".
    Both fixes are inert at their old values (`H = 0`, scale 1), which is *precisely* why nothing
    went red: `HullWaterlineAcceptanceTests.SetPose` pins `_hull.HeavePixels = 0f` inside every GPU
    render, and the EditMode pin re-implements the same H-free formula, so **the suite and the code
    agreed with each other and disagreed with the game.** A test that shares an assumption with the
    thing it guards cannot see that assumption break.
  - **Who fights whom.** The hull's height projects at cos(elev) px/m and its ground at
    sin(elev); the water's lift moves its pixels at 1 px/m (`ws.y += lift`); both depths obey
    the calibrated convention above. Solving the pixel-share: a water sample at ground offset
    `Δ` from the hull's ROOT line with lift `L` fights, on EACH hull ground line `ry`, exactly
    the height `r(ry) = r_f − tan(elev)·ry` where `r_f = (Δ + L)/cos`, and wins iff
    `r(ry)·(cos²+sin) < L·(cos+sin) − zHeave·sin + ry·cos·(1−sin)` — the climb rate ≈1.146
    rig-m per metre of lift at 40°, a z-heave counterweight of only ≈0.523, and the far-rail
    beam residual as an exact per-line term.
  - **The clamp** (`DisplacedWaterMath.WatertightZHeaveMeters`, per pose push): scan the
    footprint (x ±half the rig cell's width, step 2 m; y ±6 m — all the water that can share a
    pixel with the hull — step 0.5 m, the axis that bounds the blind spot), evaluate the
    shader-twin field (`WaveFieldBridge.ShaderTwinSample` over the SAME published `_WaveTrain*`
    globals the water's vertex stage lifts with — the ONE-SEA rule closed at the globals —
    times the frame's published effective exaggeration, shore fade taken as 1: an offshore
    bound that can only over-dry, never flood). Every sample whose root-line fight lands ON OR
    ABOVE the deck line (`r_f ≥ WatertightDeckHeightMeters`) demands protection of the WORST
    ground line it threatens — `ry* = min(WatertightHalfBeamMeters, (r_f − deckHeight)/tan)` —
    i.e. `zh ≥ (L·(cos+sin) − (r_f − tan·ry*)·(cos²+sin) + ry*·cos·(1−sin))/sin`; the Z-BIAS
    heave (and ONLY the z bias — the on-screen ride stays the honest shared heave) is raised to
    the maximum demand plus an engagement-ramped safety
    (`WatertightDemandSafetyMeters` = 0.4 zh-m: full where protection binds hard, exactly zero
    at the no-clamp boundary — daily seas stay bit-untouched; it buys out the discrete scan's
    between-station residue, measured as 16–53 px single-instant leaks without it). Samples
    fighting the open planking BELOW the deck line demand NOTHING — the exterior waterline
    keeps every centimetre of truthful climb the interior allows, storms included.
  - **The data.** `HullMeshDef.WatertightDeckHeightMeters` + `WatertightHalfBeamMeters` are
    GAME-SIDE per-hull data like `RestingDraftMeters` (the baker never writes them; deck height
    0 = clamp off, byte-identical pre-fix render): the rig sources' own constants — lobster
    sole DECK 0.50 / half-beam 2.5 (station 2.20 committed generous: the washboards ride the
    sheer outside the station line, and only this value answers in the capped protection
    branch), side dragger working deck DECK 2.05 / half-beam 3.50 ("max beam 7 m"). All
    measured green 2026-07-23.

    ⚠️ **And it is data, which means it can be MISSING — it was, for nine hulls (owner playtest
    2026-07-25: "water shader is leaking onto deck of iso hulls making boats look semi
    submerged").** The clamp shipped 2026-07-23 against the only two mesh hulls that existed;
    ADR 0022 phase 6 landed the other nine a day later with all three flotation fields at 0, so
    every boat the player actually sails had the clamp switched off and the sea drew inside her.
    Nothing went red, because the watertight asserts reach two hulls by literal asset path and
    the fleet-wide fixture's oracle is a fresh rig extraction — and these fields have no rig
    counterpart by design. The enumeration guard now lives with the fleet, CPU-only so CI
    adjudicates it: `HullMeshFleetTests.EveryCommittedHullMesh_CarriesTheWatertightClampAndAWaterline`.
    The committed fleet, deck height / half-beam / resting draft in rig metres:

    | hull | deck | half-beam | draft | deck height read from |
    |---|---|---|---|---|
    | dory | 0.06 | 0.85 | 0.11 | no `DECK`; open sole `floorPt` = `kz + FLOOR`, min keel z 0 |
    | punt | 0.06 | 0.90 | 0.19 | no `DECK`; open bilge sole, battens sit higher |
    | console skiff | 0.28 | 1.25 | 0.21 | `DECK = 0.28` (sole hatch is a lid, not a hole) |
    | sport skiff | 0.28 | 1.25 | 0.19 | `DECK = 0.28` |
    | lobster boat | 0.50 | 2.50 | 0.50 | `DECK = 0.50` |
    | cape islander | 0.72 | 2.40 | 0.53 | `DECK = 0.72` (flat sole, does not follow the rocker) |
    | side dragger | 2.05 | 3.50 | 1.10 | `DECK = 2.05` |
    | stern trawler | 1.75 | 4.70 | 1.60 | ⚠ the open STERN RAMP floor, not her `DECK = 3.5` |
    | stern trawler mk2 | 1.75 | 4.70 | 1.63 | same envelope as her sister |
    | coastal packet | 5.00 | 5.50 | 1.90 | `DECK = 5.0` (flush-decked; hold is closed) |
    | tanker | 8.60 | 9.00 | 2.47 | `DECK = 8.6`, the lowest of her three deck levels |

    Two of those are judgement calls, not measurements, and are the first knobs if a storm run
    disagrees: the **trawlers' 1.75** (their stern ramp is open to the sea, so the lowest open
    interior surface is 3.4 m below the working deck — "lower = drier = safer" says 1.75, but if a
    wet ramp mouth is correct art, 3.5 is a one-field revert) and the **tanker's 0.38-ratio draft
    2.47**, the one hull where the ratio and the rig disagree — her painted boot line sits at
    z 3.25–3.95, so 3.60 is the rig's own answer.

    **The regime split this data creates, in plain terms.** Reducing the clamp at 40° elevation,
    a hull's zero-lift demand is `0.4257·halfBeam − 1.913·deck`; when that exceeds `−draft` the
    clamp binds even in a dead calm. The dragger (`−2.43`), packet and tanker keep an honest
    living waterline; the lobster (`+0.108`) is pinned at her marks, and the **dory (`+0.247`) and
    punt (`+0.268`) are the most-clamped hulls in the fleet** — their soles sit 6 cm above the
    keel, so they trade "semi-submerged" for "sitting ON the sea". That is the safe direction and
    the shipped lobster's accepted precedent, but there is no data cure: the real one is the
    per-face interior mask named above.

  Result: water still climbs the exterior planking with every wave, but the moment a crest would
  put water inside the bulwarks, the calibrated frame rides up exactly enough that it cannot.
  Per-hull looks follow the geometry honestly: the DRAGGER (real freeboard — 2.05 m deck over
  1.1 m draft) keeps her daily-sea waterline essentially clamp-free (her reference-sea demands
  measure ≈ 0) and wears a bounded band even in the gale; the LOBSTER — whose sole sits AT her
  design waterline — is pinned at her marks whenever the local sea could top them, so her share
  of the living waterline is the trough swing (the sea drops away, bares her planking, and
  returns to her marks). ⭐ **The per-face interior mask named here as "the known upgrade" is now
  BUILT and SUPERSEDES this whole clamp — see §24.1.** Displaced OFF there is no frame and no clamp
  (the A/B byte-identity contract holds); a silent wave field demands nothing and the clamp is
  inert.
  Proof: `HullWaterlineAcceptanceTests` — the per-point law, its worst-line term and the ramped
  safety pinned HEADLESS (CI-adjudicated) against an independent reconstruction over the packed
  reference field, plus the OFF states (deck height 0 / silent field) bit-exact; the GPU storm
  suite (reference wind ×2.2, sea state 1.0 — a full gale) pins BOARDED water (covered hull
  pixels disconnected from the bottom-contiguous waterline run) at SPECK LEVEL — ≤ 64 px, a
  1–2 px thin-rigging residue tolerance sitting 30–150× under the measured defect class
  (1,800–9,900 px of solid interior water) — for BOTH committed hulls at the storm's most
  dangerous instants (crests scanned at the root and four footprint offsets; a gale deliberately
  demands DRYNESS only — interior protection may occupy the whole freeboard there); the
  reference-sea production tests own the climb contract — the dragger's living daily band, the
  lobster's trough swing, speck-level boarding in daily seas (lobster measures exactly 0); and
  the unclamped control (deck height 0, the pre-fix state) must flood loudly or the metric
  proved nothing.
- **Deck corollary (phase-4 note):** while the sea is displaced, a deck occupant that wants to
  interleave with ITS hull must ride the hull's frame (parent under the hull renderer or apply the
  same registry frame) — a raw world-z≈0 deck renderer sits far NEARER than a calibrated hull.

### 24.1 The per-face interior mask — the sea is told what it may not draw on

**The clamp above hit its ceiling, in two directions at once** (owner playtest 2026-07-25, after the
H-term and frequency-scale fixes landed): *"when the bow faces south you see water at the stern,
also a lot of the boats ride very high in the water now with props not submerged."* Both are the
same limit. The clamp guards ground-lines only out to `WatertightHalfBeamMeters`, but a bow-on hull's
ground-lines span her half-LENGTH, so the stern lies outside the guarded band; and closing that gap
costs **+0.42 m (dory) to +2.50 m (side dragger)** of extra shove on every boat — which is the second
complaint, and is the same wall that "dry-docked the dragger" in the clamp's own lineage. A blunt
whole-hull z shove cannot keep a sole dry without also lifting the planking, the transom and the
propeller out of the water with it.

**So stop guessing the hull's reach and ask the GPU.** Each face of a baked hull now carries an
INTERIOR flag in `UV0.w`. A guard pass (`HHHullGuard`, a second pass on the facet shader riding the
SAME `vert`) rasterises the fleet into a one-channel `_HHHullGuardTex` with `ZWrite On / ZTest LEqual`
against **its own** depth buffer, so the surviving value at each pixel is the flag of the NEAREST hull
surface there. The displaced water's fragment then `discard`s where that reads interior — killing the
colour *and* the depth write, so the pixel keeps the hull's depth and the hull composes normally.

Per-pixel exact, orientation-free, and **no wave maths is duplicated anywhere** — which matters,
because a duplicated copy of the lift is precisely what caused the frequency-scale defect. The
exterior is left completely alone: the waterline climbs the planking truthfully at every heading, an
outboard's leg and prop stay wettable, and hulls float at their tuned resting draft because nothing
is shoving them nearer the camera. `IsoFacetHullRenderer` bypasses the clamp when the mask is on AND
that hull's mesh actually carries flags, so an un-rebaked hull keeps her clamp rather than silently
losing all protection.

**Why not a stencil** (the first design, rejected): `Stencil Ref` is per-PASS fixed state in ShaderLab
while "interior" is per-FACE; `SV_StencilRef` is SM5.1 and would burn the mobile port; and it was
never established that the Depth32 buffer even has stencil bits. **Why not `ZTest Equal`** on the
facet pass: after the water writes, `LEqual` already selects the identical fragment set — `Equal`
adds only a hard dependency on two separately-compiled vertex programs agreeing bit-for-bit, a
silent GPU-only failure (the boat vanishing in patches) that CI can never see.

**The classifier — "can the sea see it?"** A face is EXTERIOR iff it is frontmost at even one pixel
from even one direction at or below the horizon, all round. It reads no normal, no material and no
rig source, and each of those was measured false first: **normals do not survive mirroring** (on 7 of
11 hulls the majority of paired faces carry the shared winding, so half the planking would be
misclassified); material names do not separate (the dory's whole palette is `{wood, iron}`); `b <= -1`
includes the boot stripe and excludes every sole; an inset threshold is a continuum, not a split.
Two structural details are load-bearing: the raster is **two-phase** (a winner-takes-all pass loses
every coplanar decoration band to the plank beneath — 184 wrongly-interior faces on the dory alone),
and it **samples strictly below elevation 0** (see the ring's two fences immediately below).

⚠️ **The sampling ring is fenced on BOTH sides, for two different reasons.**

*Above 0* — at +1° an upward-facing deck becomes visible and leaks into the wettable set (18 dory /
18 lobster / 44 packet faces flipping), i.e. decks becoming floodable fleet-wide.

*At exactly 0* — the ray is **level**, and a level ray clears a low gunwale and lands on the **inner
planking of the far side**. Sheer curve makes that routine rather than a graze, since freeboard is
lowest amidships; one frontmost pixel then condemns the whole face. This shipped in the first mask
bake and the owner found it by eye (playtest 2026-07-26): *"it doesnt seem to affect the lower floor
deck though, now its just the interior walls of the boat"* — with the diagnosis attached, *"the waves
need to read on the exterior walls to give the submerged effect but the walls considered interior
need to not show any water shader."* It was wave-independent (unchanged at `WaveExaggeration = 0`),
never touched the sole (a level ray cannot hit an upward-facing floor — edge-on at 0, backfacing
below), and hit only the vertical interior surfaces, which is exactly the set a level ray can reach.

**Why every existing check passed it.** The cross-check below measures the *lowest* interior face,
which the sole satisfies on every hull — the metric is structurally blind to the walls above it. Same
shape as the clamp's post-mortem in §24: the test and the code agreed with each other and disagreed
with the game. Removing the ring is surgical, because every remaining elevation looks **upward** at
the hull and can only ever reach her outside; −8° is what actually decides the topsides. The
classifier asserts the fence rather than commenting it.

⚠️ **It is a GOLDEN MASTER, not a formula** — 5–30% of sea-band faces move under a doubling of the
azimuth count, so the sampling constants are committed data and a change to them is a deliberate
re-bake with re-measured pins.

**The evidence it is right.** The classifier's lowest interior face, from geometry alone, against the
deck heights measured independently by hand off the rigs' own `DECK` constants:

| hull | interior faces (0° ring → without) | lowest interior | deck line | Δ |
|---|---|---|---|---|
| dory | 116 → **134** /472 | 0.060 | 0.06 | **0.000** |
| punt | 138 → **156** /575 | 0.060 | 0.06 | **0.000** |
| console skiff | 126 → **150** /663 | 0.271 | 0.28 | −0.009 |
| sport skiff | 107 → **136** /621 | 0.271 | 0.28 | −0.009 |
| lobster boat | 135 → **167** /676 | 0.491 | 0.50 | −0.009 |
| cape islander | 95 → **122** /509 | 0.710 | 0.72 | −0.010 |
| side dragger | 163 → **194** /792 | 2.034 | 2.05 | −0.016 |
| stern trawler | 117 → **158** /793 | 1.750 | 1.75 | **0.000** |
| stern trawler mk2 | 121 → **188** /1210 | 1.750 | 1.75 | **0.000** |
| coastal packet | 178 → **197** /1254 | 4.731 | 5.00 | −0.269 ⚠ |
| tanker | 204 → **250** /1760 | 8.582 | 8.60 | −0.018 |

Ten of eleven within 18 mm, by two methods sharing no code and no input. The coastal packet is the
one outlier and is also the least stable hull under sampling perturbation — she is named in the guard
rather than hidden by a loose tolerance. Pinned by
`HullMeshFleetTests.EveryCommittedHullMesh_CarriesTheInteriorMaskAndAgreesWithItsDeckLine` and
`InteriorClassifier_SamplesStrictlyBelowTheHorizon`.

**Reading the walls re-bake (2026-07-26).** Every hull gained 18–67 protected faces and **every
lowest-interior height is bit-identical** across the change. That pairing is the whole proof: the
faces that flipped all sit *above* the sole (the interior walls), and none dropped toward the keel —
so no outer planking became interior and the waterline still climbs her exactly as before. The
failure mode this rules out is the opposite one named above: coarser sampling over-classifies, which
would have shown as a dry patch on the planking where the sea should be washing.

**The stern ramp stays wet** (owner ruling 2026-07-26: *"you can keep the ramp wet. its a cool
effect"*), and the same column proves it rather than promising it. A stern ramp cuts *down* from the
deck toward the water, so its floor spans heights below the deck line; if any of it had flipped to
interior, the trawlers' lowest-interior would have fallen below 1.750. Both are still pinned exactly
at 1.750, so the ramp mouth is still exterior. This closes the open §24.1 question — no `DECK = 3.5`
move, no rig change.

### 24.2 The rail — the sea has a LEVEL

Removing the level ray fixed the walls and nothing else, and the next playtest found the next
instance: *"stern washboards are still being covered"*, then *"some in the bow still show, some of
the elevated decks on bow show it, some of the larger vessels show it on what looks like deck."* The
owner named the real problem rather than the instances: **"fixing one surface at a time feels
tedious, there must be a better solution, like defining the exterior walls of a boat or something."**
He was right, and visibility was the wrong primitive all along.

**Light reaches places water cannot.** It enters through the gap over a gunwale. It falls on the
UNDERSIDE of an overhanging washboard — which rides the sheer outside the station line — and the
classifier then wets its *top*, because `Raster` takes `Mathf.Abs(area)` and has no idea which side
of a face it is looking at. It cannot be given one either: winding does not survive mirroring on 7 of
the 11 hulls, which is why normals were rejected in the first place. Every one of these is a separate
patch under a visibility rule.

**Water is not light: it arrives from outside and it has a level it cannot climb above.** So the rule
gains a second half — a face is EXTERIOR iff the sea can *see* it **and** the sea can *rise* to it.
`RigMeshInteriorClassifier.DeriveRailHeight` traces the sheer station by station (the highest vertex
still out at that station's own half-beam, so cabins and consoles are excluded by geometry rather
than by name) and takes a low percentile, because water enters where the sheer is lowest. Anything
lying wholly above that is out of reach whichever way it faces. **No sidedness required** — the level
does the work that winding could not.

| hull | rail | deck | interior (visibility → +rail) | lowest interior |
|---|---|---|---|---|
| dory | 0.460 | 0.06 | 134 → **203** /472 | 0.060 |
| punt | 0.347 | 0.06 | 156 → **335** /575 | 0.060 |
| console skiff | 0.607 | 0.28 | 150 → **336** /663 | 0.271 |
| sport skiff | 0.622 | 0.28 | 136 → **323** /621 | 0.271 |
| cape islander | 1.403 | 0.72 | 122 → **277** /509 | 0.710 |
| lobster boat | 1.173 | 0.50 | 167 → **410** /676 | 0.491 |
| side dragger | 2.600 | 2.05 | 194 → **533** /792 | 2.034 |
| stern trawler | 3.500 | 1.75 | 158 → **610** /793 | 1.750 |
| stern trawler mk2 | 4.550 | 1.75 | 188 → **670** /1210 | 1.750 |
| coastal packet | 5.007 | 5.00 | 197 → **1011** /1254 | 4.731 |
| tanker | 8.600 | 8.60 | 250 → **1475** /1760 | 8.582 |

**Every rail clears its deck line** (pinned by `HullRailHeightTests.EveryHullsRail_SitsAboveHerDeck-
Line`); a rail at or below the deck would mean the derivation had lost the hull side. **Every
lowest-interior is unchanged**, so the independent deck-height cross-check survives the rewrite
untouched. And the derivation recovered real structure nobody fed it: the stern trawler's rail landed
at exactly **3.500**, the shelter-deck height §24.1 had previously guessed at by hand.

The change is **monotone** — it only ever removes wettable faces — so it cannot introduce flooding
anywhere. The only way it can be wrong is by drying something that should be wet, which is the
failure direction this document has always preferred.

⚠️ **The accepted cost (owner-accepted in advance).** A level cuts both ways: the sea also stops
drawing on her OUTSIDE above the rail, so she no longer takes green water over the topsides in a
heavy sea. That was judged the right trade, because a boat that read as swamped was the defect being
removed. If it ever reads as too tame, the knob is `RigMeshInteriorClassifier.RailPercentile` — and
raising it is a deliberate re-bake, not a tweak.

**Accepted artefact:** the mask is binary and depth-blind, so a crest genuinely between camera and
boat that overlaps her cockpit leaves an interior-shaped island of dry boat. No existing metric can
see it — the storm suite counts water-over-hull and this is hull-over-water, so it reads as MORE hull
and every assert passes. Owner verdict pending.

**Rollback:** one serialized bool, `IsoFacetHullFeature._interiorMask`. Off ⇒ no guard pass, no
discard, and the whole-hull clamp resumes — exactly the shipped behaviour, with no data edits.

**The waterline composition — draw order IS the waterline.** The water pass now records BEFORE the
facet/deck passes (`IsoFacetHullFeature`). Hull fragments below the lifted surface fail the shared
z-test and never enter the facet MRT; the resolved hull texture holds only the EMERGENT hull, so
the hull overlay (sorted above the sea, as boats always were) composes planking only where it is
truly above water, and the WaterOverlay (at the flat sprite's slot, under every boat) shows the
sea where the submerged planking used to be. Water pixels behind a hull stay in the water target
and are simply covered in-scene by the hull overlay's sort. The keyline resolve is untouched: it
floods the emergent silhouette, so **the hull outline follows the waterline and reads OVER the
water** — the sprite fleet's ink-over-water convention at the flat waterline, kept.

⚠️ **The composing WINDOW has to travel with the boat — the second half of the 2026-07-25
"semi submerged" defect, and it is not a z-test bug at all.** A mesh hull's only in-scene face is
the `HullOverlay` quad, and its shader is a 1:1 screen-space `Load()` that exists ONLY where that
quad rasterises: a hull pixel outside the quad is never composed, and what shows there is whatever
sorts beneath — the WaterOverlay, which covers the whole sea rect under every boat. The quad is the
rig CELL rect (+1 px for the keyline), baked once and parented at the un-heaved root. That was
sound while `HeavePixels` carried only the rig's own rock, which is an animation INSIDE the cell
(the rig subtracts it from screen y after projecting, so it clips at the cell edge too — matching
that is the golden master) and runs 1.0–1.6 px across the fleet, well inside the authored margin.
Phase 3 step 2 then began pushing the displaced ride through the same channel in metres ×
PxPerMetre — **20–100× that budget**. On a crest the hull's image slid up out of a window that
stayed behind, her top band was dropped, and the sea drew through the gap: a hard horizontal cut
that reads exactly like a swamped boat. On the dory (cell 156 px, pivot 88 from the top) the
headroom is ~21 px ≈ 0.65 m against a reference-sea crest ride of ~1.4 m.

The fix separates the two, because they are physically different things wearing the same units:
the Core seam gains `IHullMeshRenderer.RidePixels` — *how much of `HeavePixels` is world ride
rather than rig rock* — which `MeshHullDriver` reports alongside the unchanged total, and
`IsoFacetHullRenderer.ApplyPose` translates the overlay quad by. The rock keeps clipping at the
cell as the rig does; the boat moving through the world carries her window with her. Y only, never
the calibrated z (meaningless to an in-scene quad under the ortho camera, and large enough to throw
it out of frame). Ride 0 ⇒ window at localPosition 0 ⇒ byte-identical to before, so the flat-sea
A/B contract holds unchanged. Note the interaction the two defects had: both worsen with sea state,
so they presented as one symptom, and the acceptance suite could see neither — it pins
`HeavePixels = 0` and builds its own screen-sized water overlay.

**Heave honesty (phase 3 step 2 — SHIPPED): boats ride the sea they are drawn on.** While the
displaced sea is active, every hull's vertical ride is the same displaced-height rule the surface
lifts with: `ShoreFadeMath.DisplacedHeight(h, stillDepth, band, exaggeration)`, where `h` is the
one wave sample `BoatWaveMotion` already rocks on (the ONE-SEA rule), `stillDepth` is the game's
one depth rule (`BoatCrossing.DepthAt` — open water reads +∞ ⇒ fade 1), and
exaggeration + band are the ACTIVE surface's own per-tick effective values, published through the
Core seam `DisplacedSea` (`Core/Environment/DisplacedSea.cs`) — never a per-consumer config read,
so boat and sea cannot disagree even mid-tune (the surface re-reads `GameConfig.DisplacedWater`
and re-publishes every ~8 Hz tick). Delivery differs by hull kind, agreement doesn't:

- **Mesh hulls** take the ride through the presenter seam
  (`IBoatHullPresenter.SetDisplacedHeaveMeters` → `MeshHullDriver`), which folds
  `(ride − restingDraft)` into the renderer's heave-pixels channel — so the screen lift and the
  calibrated waterline z (`HullDepthBias`'s heave term, above) move together by construction and
  the waterline stays truthful for free. **The resting draft** sinks the keel-origin rig
  (rig z = 0 is the KEEL BOTTOM) to a design waterline: per-hull data,
  `HullMeshDef.RestingDraftMeters` — a GAME-SIDE field the baker never writes (it survives
  re-bakes; the natural long-term home is a waterline symbol in the rig's gameplay sidecar —
  migrate when the export contract grows one). Lobster boat 0.5 m (the spike's own probe framing:
  `spike/3d-water` `Spike3dWaterMenu.cs`, "sunk half a metre of draft"); side dragger 1.1 m (the
  same ≈0.38 visual-to-gameplay-draught ratio applied to her 2.9 m `DraughtMeters`). The draft
  applies only while the displaced sea is active.
- **Sprite hulls** (the dory's rock-grid path and the legacy transform path alike) ride the same
  displaced height as a plain screen-vertical lift of the visual — no waterline clipping to keep
  honest, just the same sea under the whole fleet. The legacy path's bob cap deliberately does
  NOT apply to the ride (never re-scale one consumer alone).
- **The A/B contract extends to boats:** displaced OFF clears the seam — no ride, no draft, and
  every hull's pose is byte-identical to the pre-phase-3 flat-water render
  (`SharedHeaveTests` / `SharedHeavePlayTests` pin the whole law).

**RULED and shipped — seakeeping FORCES read the displaced height (SEE==FEEL).** The owner
closed the ADR's open question on 2026-07-23, verbatim: **"Yes seas push should match"** —
overriding the keep-sim-true recommendation previously on file (his call, deliberate). While the
displaced sea is active, every height-scaled seakeeping force term (the wave push + the wave yaw
torque, both linear in the field's amplitude via its slope) is multiplied by
`SeakeepingForcesMath.DisplacedForceScale` = the surface's **published** exaggeration ×
`ShoreFadeMath.Fade01(depth, band)` — the same wave sample, the same factor the vertex stage and
the visual ride use, read from the Core `DisplacedSea` seam (never a per-consumer config read).
Boats now *feel* calm water inside the shore-fade band and *feel* the ×exaggeration drama
offshore — the sea's push matches what the player sees, and `GameConfig.WaveExaggeration`
deliberately becomes a handling dial as well as a readability one. Displaced OFF the scale is
exactly 1: forces read the raw sim height byte-identically — the A/B contract extends to
physics. Design consequence accepted with the ruling: while the dev A/B toggle exists, handling
depends on a presentation toggle; once the displaced sea ships as the default that distinction
collapses. Laws pinned by `SeeEqualsFeelForcesTests` (EditMode: OFF byte-identity, open-water
×exaggeration, shore-fade parity, linearity in the published exaggeration, and the
output-scaling ≡ displaced-field-read equivalence) and `SeeEqualsFeelForcesPlayTests` (PlayMode:
an adrift hull's push stills at published exaggeration 0, hardens at 2, and returns exactly when
the seam clears — deterministic scripted clock).

**Proof** (`HullWaterlineAcceptanceTests`, the IsoFacetUrpPassTests pattern — production path via
`Camera.Render()`, Null-Device-gated for CI; measured RTX 4060 / D3D12 2026-07-23 and pinned): a
CI-safe headless pin that `HullDepthBias` is the water's vertex depth and reduces to heights at
the contact line; the GPU acceptance rendering the lobster hull beam-on in the reference sea at
its deterministic trough/crest instants (found by scan, not authored: h −1.046 m / +0.950 m) —
the trough leaves the planking bone dry (0 covered px), the crest puts a bottom-contiguous
covered run up the planking in EVERY measured column (median 10 px, p90 13 px, 8,719 submerged
px, keyline riding the cut; bars 6/10 px), nothing is ever covered in the silhouette's top 40 %
(wheelhouse/mast country), and turning the sea OFF restores today's render with 0 differing
pixels; and the sabotage — flip the sign of the water's `_WaterIsoDepth` height term (a lifted
crest steps farther instead of nearer) and the crest goes bone dry (median run 0). Harness traps
honoured: fresh material (never Water.mat's baked height map), `_USE_HEIGHTTEX` off AND a black
height texture, plain `LEqual` through the render-graph camera path (no hand-rolled reversed-Z),
shader warm-up before measuring. The test can dump its three adjudicated frames as PNGs
(`HH_WATERLINE_DUMP=<dir>`) for a human eye on a red run.

### 24.3 Sidedness — the inner strake, and why a face's two sides are two different surfaces

The rail retired the over-the-lip family, and the next playtest found the survivor: *"its still on
the console walls, in the cape islander its at the wall intersection with the floor"* — mainly the
interior wall boards (console vertical wall sections), the upper bow interior, the rear washboards.
The rigs model a separate **inner skin** (`skin(side,u,frac,inset)`; dory `TH = 0.035`, console
`0.045`, cape `0.05`), inset by `TH` at the sheer and narrowing toward the floor. From in under the
covering board, a below-horizon ray **genuinely reaches its BACK** — this is not a sampling
artefact: raising the raster from 16 to 96 px/m (62.5 → 10.4 mm) changed the console and cape by
exactly **0 faces**, at 836 s of classify time. But the surface the player looks at is its FRONT.
One face, two sides, two different relationships to the sea — and a face-level flag cannot say both.

**So the classification is per SIDE** (`RigMeshInteriorClassifier.ClassifySides`): each side of a
face is exterior iff the sea can see THAT side and can rise to the face (the rail caps both sides).
`UV0.w` becomes a side code — `0` exterior both sides (and every pre-mask bake / every fitting),
`1` interior both sides, `2` interior on the FRONT only, `3` interior on the BACK only — and the
guard pass decodes which side the camera is rendering before writing the mask.

⚠️ **This is NOT the sidedness that was rejected when normals were tried as a classifier.** That
attempt needed the normal to point OUTWARD — an orientation claim mirroring breaks on 7 of the 11
hulls (mirror twins share winding, so one twin's normal points into the boat). Here the first-three-
vertex normal is only a **LABEL** telling a face's two sides apart, and it does not matter which
side it lands on: the classifier sorts a view into front/back by `sign(dot(faceNormal, eye))`, and
the guard vertex stage recomputes the same quantity in world space
(`sign(dot(worldNormal, towardCamera))`, third row of the view matrix) — the object matrix is
orthogonal (rotation × the deliberate det −1 mirror), and **orthogonal maps preserve dot products**,
so bake and render agree face by face however the mirroring fell. `SV_IsFrontFace` was deliberately
NOT used: its winding convention would have to survive the reflection and the shared-winding twins,
which is exactly the parity minefield the label sidesteps.

| hull | fully interior (= old interior) | front-dry (code 2) | back-dry (code 3) |
|---|---|---|---|
| dory | 203/472 | 83 | 117 |
| punt | 335/575 | 62 | 93 |
| console skiff | 336/663 | **90** | 134 |
| sport skiff | 323/621 | 93 | 129 |
| cape islander | 277/509 | **49** | 104 |
| lobster boat | 410/676 | 66 | 117 |
| side dragger | 533/792 | 54 | 110 |
| stern trawler | 610/793 | 13 | 66 |
| stern trawler mk2 | 670/1210 | 196 | 170 |
| coastal packet | 1011/1254 | 49 | 89 |
| tanker | 1475/1760 | 9 | 107 |

(⚠️ Superseded one revision later: §24.4 moves the rail test from the face to the HIT and re-bakes
every count in this table. The fully-interior *heights* are unchanged there too.)

**The golden master survives untouched by construction.** "Fully interior" (code 1 — the sea reaches
neither side) is exactly the set the side-blind classifier produced, so every fully-interior count
and every lowest-fully-interior height above is **identical to the §24.2 table**, and the deck-line
cross-check re-pins nothing. The change is again **monotone**: codes 2/3 only ever dry a side that
was previously wettable (the sea's own reachable side stays wet), so it cannot introduce flooding.
The bolded numbers are the cure for the reported defect: the console's and cape's inner strakes are
front-dry, so the wall the player looks at no longer takes the sea, while its outboard back — where
the water genuinely laps under the covering board — stays honest. Guards:
`HullMeshFleetTests.EveryCommittedHullMesh_CarriesPerSideInteriorCodes` (every hull has one-sided
faces; console + cape specifically carry code-2 faces), and the fully-interior cross-check now
filters to code 1. Rollback unchanged: `IsoFacetHullFeature._interiorMask` off ⇒ no guard pass at
all; a pre-sidedness mesh (0/1 only) renders exactly as before through the new shader.

### 24.4 The rail belongs to the HIT, not the face — the wall that straddles it

Sidedness cured the inner strake and the owner's next look found what it had not (2026-07-27,
screenshots): *"on dory it seems to be just the lowest hull interior boards, not the deck, same
situation for the punt, centre console boats it seems to be the centre console walls themselves,
then the cape islander/lobster boat it seems to be the stern interior wall."*

The console wall and the stern bulkhead are the same shape of thing: **a surface standing up inside
the boat, straddling the rail.** Its base sits on the deck — below the rail — so §24.2's cap, which
only retires faces lying *wholly* above the rail, never fired on it. Yet the only pixels of it the
sea can see are the ones a shallow ray reaches by clearing a low stretch of gunwale, and geometry
forces those to lie at or above that sheer: the ray descends as it travels outward, so its crossing
of the hull side is always *lower* than what it lands on inside. Water at its level cannot make that
entry. The face was condemned by a hit the sea could never have made.

**So the level applies per HIT, not per face.** A visible pixel counts as sea-reachable only if the
hit point's own height is below the rail — evaluated inside the raster, on the interpolated model
height, in the same test that already checks frontmost-ness. This **subsumes** the whole-face cap (a
face wholly above the rail has no below-rail pixel to be seen at, so it still classifies interior)
and retires the over-the-gunwale-entry family with it. A face is now exterior iff **some point of it
that the sea can see is also a point the sea can reach.**

⚠️ **The exact gate in front of the interpolated one is load-bearing, not defensive.** A barycentric
sum of three corner heights can land a hair *below* the minimum corner, and the tanker's and
trawler's decks sit bit-exactly AT their rails (8.600 / 3.500). Without a corner-exact
`min(z) < railZ` pre-test, FP jitter marked **13 tanker and 2 trawler** exactly-at-rail faces
wettable — a measured regression against the previous bake, caught by the counts, not by a test.
Corner comparison is exact; interpolation is not.

| hull | fully interior (§24.3 → §24.4) | front-dry | back-dry | lowest fully-interior |
|---|---|---|---|---|
| dory | 203 → **211** /472 | 81 | 115 | 0.060 |
| punt | 335 → **337** /575 | 65 | 96 | 0.060 |
| console skiff | 336 → **372** /663 | 87 | 126 | 0.271 |
| sport skiff | 323 → **357** /621 | 88 | 124 | 0.271 |
| cape islander | 277 → **298** /509 | 52 | 95 | 0.710 |
| lobster boat | 410 → **426** /676 | 68 | 111 | 0.491 |
| side dragger | 533 → **583** /792 | 49 | 94 | 2.034 |
| stern trawler | 610 → **626** /793 | 14 | 67 | 1.750 |
| stern trawler mk2 | 670 → **719** /1210 | 186 | 169 | 1.750 |
| coastal packet | 1011 → **1029** /1254 | 41 | 82 | 4.731 |
| tanker | 1475 → **1481** /1760 | 6 | 104 | 8.582 |

Every hull gains interior faces (+2 to +50 — the standing walls joining), and **every
lowest-fully-interior is unchanged from §24.2 and §24.3**, so the hand-measured deck-line
cross-check survives its third rewrite untouched. Monotone again: moving the level test from the
face to the hit can only *withdraw* wettable evidence, never add it, so no bake this rule produces
can flood something a previous one kept dry.

**Still open, and NOT this mask's to fix:** the dory's and punt's lowest interior boards. Those two
have no standing wall — geometry says an over-gunwale ray can only land *above* the entry sheer —
and the owner reported in the same breath that *"a lot of the boats seem to be sitting very low …
nearly submerged."* A hull riding too deep loses the shared z-test to the drawn sea, so her lower
interior fragments never enter the facet MRT and the flat sea sprite shows through from underneath.
No interior mask can intercept that; it is a DRAFT/ride problem (`HullMeshDef.RestingDraftMeters`
and the §24 ride law), banked by the owner for its own session. Triage rule: a classifier miss is a
whole face wet regardless of sea state and unchanged at `WaveExaggeration = 0`; a draft flood rides
up and down with the swell and hugs the lowest surfaces.

## 25. Sea-state band scaling + dispersion — the octaves stop sliding over each other (ADR 0027 #4 + #9)

The two items land together — the ADR is explicit that building #4 without #9 leaves waves that grow
longer but do not speed up. Both default **OFF = today's fixed wavelengths and hand-set speeds
EXACTLY** (bit-for-bit passthrough: the frequency factor divides by exactly 1.0 at response 0, the
dispersion blend is a lerp whose 0 endpoint is the legacy value, and a zero shoal shift adds
exactly 0). C# twin: `WaterDispersion` — `BandFrequencyFactor` / `DeepPhaseSpeed` / `PhaseSpeed` /
`BandSpeed` / `SwellPhaseRate` / `ShoalShift`, mirrored one-for-one by the shader's `BandFreq` /
`Dispersion*` helpers — **change one, change BOTH in the same PR** (`WaterDispersionTests` pins the
laws headless, including the bit-exact passthrough).

### 25.1 #4 — band wavelengths scale with sea state

Real seas grow in **wavelength** as they build, not only in amplitude; the visual octaves' spatial
frequencies were fixed, so a storm read as the same-sized water moving harder. Every band read of
`_NoiseScale` / `_WindChopScale` / `_CrossSwellScale` (and the legacy `_OceanSwellScale` band) now
goes through `BandFreq(scale) = scale / (1 + _BandScaleResponse · _Chop)` — wavelength grows
linearly in the already-pushed sea state (`_Chop` is sim-pushed; read, never authored — §12.1).
**One uniform, one meaning:** every visual consumer of each scaled uniform applies the same law —
the untile warp fields, the spec normal-tilt read and the whitecap blob scale all coarsen with the
same growing sea. Two reads are deliberately excluded, with reasons stated where they live:

- **the depth-read warp** (`worldXY * _NoiseScale + 7.3` feeding `SeabedElevation` → `depth` →
  `clip()`): scaling it would move where the gameplay waterline shimmers — Tier A must never touch
  the height read/clip (rule 5);
- **the wave-field `freqScale` mapping** (`_OceanSwellScale / 0.025` in the fragment, the displaced
  vertex stage, and `DisplacedWaterSurface.PublishIsoDepthFrame`): the trains already carry the
  wavelength-growth law at the sim level (`WaveMath.TrainsFrom`, `DominantWavelengthPerWindSpeed`),
  so scaling the mapping would double-apply it — and it moves drawn geometry the interior-mask /
  watertight-clamp stack guards (the `_OceanSwellScale` incident, 2026-07-25). All three freqScale
  computations remain textually identical to each other, so hull and water cannot disagree.

### 25.2 #9 — band speeds derived from wavelength (dispersion)

Each octave carried an independent hand-tuned speed with no relation to its wavelength — and the
legacy set is **anti-dispersive** (the 40 m swell band's 0.72 m/s world speed is a fraction of what
dispersion gives it relative to the 1.4 m chop's 0.09 m/s) — a large part of why the multi-scale sea
read as stacked layers sliding over each other. Each band's wavelength is `λ = 1/BandFreq(scale)`
(the noise cell size in metres; for the legacy swell band it is the literal sine wavelength). Speeds
blend from the legacy value toward `bandMult × √(gλ/2π)` by the master `_DispersionScale`; the
per-band multipliers keep the owner's art direction (rule 6), and their 0.06 default **anchors the
wind-chop band on its legacy 0.09 m/s at full dispersion** so the master re-ties the slower bands to
physics around an unchanged fastest band. Octave A's scroll is `_Flow` — the tidal current, not a
wave phase speed — so #9 leaves it alone. The trains need nothing: `WaveTrain.PhaseSpeed` has been
the dispersion relation since ADR 0018 ("speed is never free").

**Shallow water — why the scroll rate stays depth-uniform and the shoal enters as a bounded static
drift.** A per-pixel depth-dependent scroll RATE on an absolute-world-coordinate band accumulates
unbounded domain shear (the pattern offset between two depths differs by `Δc·t`; on the legacy swell
band that is ~26× the base wavenumber of spurious frequency after 600 s over a typical shoal — a
shimmer/spiral generator at every shoreline). The stable equivalent ships instead:
`DispersionShoalShift = saturate(s) · bunch · λ · (1 − c(λ,d)/c_deep)` — a **bounded** (≤ bunch·λ),
**static** drift along the band's travel direction, keyed to the full finite-depth relation
`c = √(gλ/2π · tanh(2πd/λ))` off the **same read-only depth** every other layer consumes. Where the
drift's along-travel gradient compresses the domain by a factor m, the band's local wavelength AND
its apparent phase speed both drop by m — waves genuinely **slow and bunch approaching shore**, with
zero time-accumulating artefacts. The octaves read one guarded, still (unwarped) seabed sample for
it (free at the defaults). `_ShorewardBias` (§5.12) is untouched — whether #9 subsumes part of it is
an open question to be **measured later**, per the ADR.

### 25.3 Tunables (rule 6; all default to today's exact look)

| Property | Default | Effect |
|---|---|---|
| `_BandScaleResponse` | `0` (**OFF**) | Wavelength growth per unit sea state (λ_eff = λ·(1 + r·_Chop)). |
| `_DispersionScale` | `0` (**OFF**) | 0 = today's hand-set speeds exactly; 1 = fully wavelength-derived. The master. |
| `_DispersionChopMult` | `0.06` | Feel multiplier, wind-chop band (default anchors it on its legacy 0.09 m/s). |
| `_DispersionCrossMult` | `0.06` | Feel multiplier, cross-swell band. |
| `_DispersionSwellMult` | `0.06` | Feel multiplier, legacy ocean-swell band (native-rate blend — scale 0 keeps 0.018 bit-for-bit). |
| `_DispersionShoalBunch` | `1` | Shoal drift bound, × wavelength at zero depth (bunches + slows wavefronts near shore). |

None of the six is mood-eased (no `MoodFloatNames` entry — no double-drive); whether
`_BandScaleResponse` / `_DispersionScale` should be is an open question for the owner. The see/feel
gap this opens (drawn octaves speed-scaled, the ride unchanged) is accepted and explicit for P1 —
the promotion into the field is P2, gated on an ADR 0018 amendment.

## 26. Object reflections — a filtered renderer list into an RT, wave-warped (ADR 0027 #8)

Boats, wharf structures and bankside trees **reflect in the water**. This is the project's first render
target in the water path, and it reopens a settled rejection — so both halves are recorded here.

### 26.1 Why ADR 0010 addendum 8 was reopened

That addendum rejected reflections on a **fact about the codebase**, not a matter of taste: a reflection
pass "would need a second camera + render target wired into the 2D URP renderer (**unverifiable here**)
and a second draw of the scene."

Both clauses stopped being true. **ADR 0022 phase 3 shipped `IsoFacetHullFeature`** — a working
RenderGraph injection into the 2D renderer, registered in `Renderer2D.asset`, with per-camera
`RTHandle`s, LightMode-filtered renderer lists and an explicit zero-cost-when-idle contract, under test.
The wiring is in the repo. And the cost is not a second scene draw: it is **one filtered list of
near-water objects**. A new fact is the only thing that justifies reopening an ADR; this is one.

What is **still** rejected, unchanged: a planar reflection camera re-drawing the scene (the cost verdict
was right), screen-space reflections (the 2D renderer has no depth/normals to march, and a boat's
reflection would pop at the screen edge), and per-object mirrored duplicates (no single place to warp by
the wave field, and it doubles renderer count and sorting complexity).

### 26.2 The pass — a fourth renderer list

`HHReflect` joins the existing recording in `IsoFacetHullFeature`, beside the facet MRT, the deck list
and the displaced water:

- **One `ARGBHalf` target** per camera (`_HHReflectTex`), at camera render resolution, **point**-filtered.
  Half float for the same reason `_HHWaterScreenTex` is: a night-lit source writes its colour
  **pre-compensated** for the day/night multiply (far above 1), and an 8-bit target would clamp it.
- **No depth buffer, no interior mask, no self-occlusion** — the flat mirrored silhouette §26.6 found
  sufficient. Premultiplied blending (`One OneMinusSrcAlpha`) inside the pass means overlapping
  reflectors composite in one target with no sorting contract.
- **Zero cost when idle**, and that is also this feature's *passthrough proof*: with no live
  `ReflectiveObject` the registry count is 0, `AddRenderPasses` enqueues nothing, and there is no
  "reflections off" branch to keep byte-identical — only an absent pass.

### 26.3 The mirror axis is the ground-contact pivot — and it must be PUBLISHED

A reflection is the source reflected in the plane it stands on. ADR 0026 already settled that a rig's
pivot **is** its ground contact, so the axis needs no new convention: `y' = 2·pivotY − y`, in the vertex
stage. For a mesh hull the same formula runs against the ADR 0023 calibrated iso-depth waterplane.

> 🔴 **`unity_ObjectToWorld` is IDENTITY for a SpriteRenderer.** Unity submits sprite meshes with their
> vertices **already in world space**, so the object origin reads `(0,0)` for every sprite in the scene.
> A shader that mirrors about "the object origin" reflects the whole scene about the **world origin** —
> not a subtle error, a total one. This repo measured the same trap in the tree lane on 2026-07-29 (a
> 3 m-range lamp lit three trees equally), and the facet renderer learned it earlier with `_HullOrigin`.
> **It is not an edge case; it is every sprite.**

So `ReflectiveObject` publishes the pivot per renderer into the MaterialPropertyBlock as
`_HHReflectOrigin` (`xy` = the pivot, `w` = 1 meaning "published"). With `w = 0` the HLSL **refuses to
mirror** and collapses the geometry to a clipped point: a missing reflection is a bug you go and find, a
reflection pinned to the world origin is a bug that looks like a haunted sea.

> ⚠️ **World Y is the mirror axis but NOT "height".** In a top-down game world Y is a ground-plane
> coordinate (north); the art fakes height by drawing up the screen, which is exactly why mirroring
> about the pivot's world Y is the right *visual* mirror. The distance gate below is a different
> quantity and reads terrain **elevation** off the one height map (§4). Conflating them cost a red test
> in this PR and would have excluded every reflector at the north end of the map.

### 26.4 The lookup — warped by the wave field, snapped in WORLD space

The water shader samples `_HHReflectTex` with the lookup displaced by the **same `WaveFieldSample()`
the hull rides** (ADR 0018, consumed as a black box — never forked), so a reflection wobbles on the very
crests the boat is riding. That is the payoff, it needs **no new uniform**, and it is the reason this
beats per-object mirrored duplicates: **one place to do the warp.** The displacement widens as
`ReflectionSharpness()` falls, so a breaking sea scatters the mirror.

> ⚠️ **The snap is in WORLD space, never screen/RT space.** A render target is screen-space by nature,
> and with `CameraFollow` panning continuously behind the boat a lookup quantized on the RT's own grid
> **crawls** on every pan — the one artefact that would make this read as a screen filter instead of a
> reflection (ADR 0027's Pixelation section names this exact case). Snapping the *sample position* on the
> world PPU grid means a reflection cell belongs to a place on the water and stays there. Measured:
> across a sub-pixel camera pan the screen-snapped lookup travels ~0.97 px and changes cell repeatedly;
> the world-snapped one does not move at all.

The read uses `Load()` at integer pixel coordinates rather than a uv sample: the target is exactly
camera-render-resolution so the map is 1:1, and `Load` shares `SV_POSITION`'s coordinate convention —
which removes the render-target Y-flip ambiguity a uv fetch would smuggle in.

### 26.5 Composition — over the sky, under the foam, pre-grade except the lit share

Placed **after** the §11 sky mirror and sky content (a boat reads on top of reflected cloud — hence a
premultiplied **over**-operator rather than an add) and **before** the foam (whitecaps read on top of
the boat). **Pre-grade**, so it dims with the night like the rest of the sea.

**Except night-lit sources.** A lit wheelhouse reflected in black water is *light content*; left
pre-grade the day/night multiply crushes it to ~3% and the boat appears to douse her lamps in her own
reflection. Those ride the §11.6 post-grade compensated bucket, exactly as the moon glitter does.

The split needs **no flag channel, no second target and no extra uniform**. The pass writes
*premultiplied* colour, so an ordinary reflection's rgb can never exceed its coverage; a night-lit
source writes its colour already divided by the day/night tint, which at night puts it far **above**
coverage. The excess over coverage *is* the light content:

```hlsl
float3 ordinary = min(refl.rgb, cov.xxx);          // pre-grade
float3 lit      = max(refl.rgb - cov.xxx, 0.0);    // post-grade, compensated
```

By day the compensation factor is ≈ 1, so a lit source stays under coverage and the whole sample lands
pre-grade — daylight unchanged, which is correct rather than a special case. Twin:
`WaterReflectionWarp.SplitLitShare`.

### 26.6 The fidelity probe — a flat mirrored silhouette IS enough at PPU 32

ADR 0027 left this open: *is a flat mirrored silhouette enough, or must reflections honour the interior
mask and hull self-occlusion? Cheapest correct answer wins; probe before building.*

**Probed, and the cheap answer wins.** `ObjectReflectionProbeTests` renders the cheapest version
end-to-end — a sea quad on the real water material, one reflective sprite, the real feature pass, at
PPU 32 through `Camera.Render()` on the project's own 2D renderer — and it reads: 256 source pixels
above the waterline, **255 reflected below**, tapering *away* from the waterline (so it is a mirror, not
a translated copy), and not bleeding into open water. No interior mask and no self-occlusion were
built, and none is needed at this pixel scale.

> The probe's own first draft had **no water in it** and measured 0 reflected pixels — correctly.
> `_HHReflectTex` is a private target, not the camera's colour buffer: without a sea to composite it,
> the pass draws into a texture nobody reads. Worth knowing about the architecture.

### 26.7 Bounding the reflective set (the ADR's open question)

An unbounded reflective set is this item's perf failure mode. The rule, all three parts data (rule 6):

| Half | Rule |
|---|---|
| **Layer** | The renderer must carry `ReflectionRegistry.RenderingLayer` (`1 << 29`), which only `ReflectiveObject` sets. ⚠️ The `HHReflect` **tag alone is not enough** — the tree shader carries that pass, so tag-only filtering would sweep every tree in the scene into the list. Same trap ADR 0023 hit with the flat Sea sprite sharing the water shader. |
| **Distance** | The reflector's **ground elevation** must be within its own `maxHeightAboveWater` (default 3 m) of the water level. A clifftop tree does not reflect in the harbour below it. Throttled, never per frame. |
| **Frustum** | Unity's own culling drops off-screen reflectors for free — the list is built from `cullResults` like every other. |

**Worst case: 64 renderers** (`ReflectionRegistry.MaxReflectors`), one extra filtered renderer list and
one `ARGBHalf` target at camera resolution. Exceeding the cap **logs once** and still draws — a silent
truncation reads as "everything is covered" when it is not.

### 26.8 Tunables (rule 6; all default to today's look)

| Property | Default | Effect |
|---|---|---|
| `_ObjectReflectStrength` | `0` (**OFF**) | Master; 0 skips the whole block. Multiplies the §11 sea-state curve. |
| `_ObjectReflectWarp` | `0.35` m | How far a unit of wave slope displaces the lookup. 0 = a flat mirror. |
| `_ObjectReflectSink` | `0.35` | How much the reflection fades as the water deepens under it. 0 = no fade. |
| `ReflectiveObject.pivotOffset` | `(0,0)` | Offset to the ground-contact pivot when the art's own pivot is not the contact point. |
| `ReflectiveObject.useWaterLevel` | `false` | Take the axis from the live tide (the ADR 0023 waterplane) — for anything that floats. |
| `ReflectiveObject.maxHeightAboveWater` | `3` m | The distance gate. |
| `ReflectiveObject.nightLitSource` | `false` | Route this source's reflection to the post-grade compensated bucket (§26.5). |

### 26.9 Determinism, guards + what ships enabled

`col.rgb` and one render target only — never `depth` / `clip()` / `_WaterLevel` / the height read /
`_WaveFieldParams` / anything the hulls ride (P1 integrity, rule 5). Nothing enters the save.
Twins: `WaterReflectionWarp` (mirror, world-snapped warp, and the pre/post split) with measured
sabotages; `ReflectiveObjectTests` pins the published-pivot contract against the **renderer**, because
"the pivot reached the block" is exactly what silently does not happen when a shader tries to derive it.
The shipped `Water.mat` variant is force-compiled by `WaterShaderCompileGuardTests` and the tree shader
by `TreeWindShaderCompileGuardTests`, so an HLSL slip in either `HHReflect` pass fails CI red rather
than magenta-in-build.

**Nothing ships reflective** — ⚠️ **the PRE-ACTIVATION state, as §26 first shipped it; superseded by
§26.10 below, which opted the fleet and the trees in and raised the strength.** The `HHReflect` pass
exists in `HiddenHarboursTreeWind`; no prefab carries a `ReflectiveObject`, so no pass is enqueued and
`_ObjectReflectStrength` is 0. Opting a boat, a wharf or a treeline in is: add the component, then
raise the strength.

### 26.10 Activation — what actually reflects, and where each wiring lives

§26 shipped the capability with nothing carrying a `ReflectiveObject`. This is what opted the first
renderers in — **in the builders**, never by editing a committed scene. A committed scene here is
builder OUTPUT, so a feature activated by hand survives exactly until the next builder run erases it,
and nothing fails: the effect just quietly stops being there.

| What reflects | Where the wiring lives | Why there |
|---|---|---|
| **The Acadian rig trees** | `AcadianTreeCatalog.Configure` | The ONE place such a tree is configured — the Tree Paint Tool comes through it too, so a *painted* treeline reflects without the owner touching a component. |
| **The hand-drawn trees** (`Tree01..43`) | `DecorPrefabBuilder` | Their prefabs are built there, with the TreeWind material that carries the `HHReflect` pass. |
| **The fleet** (every mesh hull) | `IsoFacetHullPresentationService.Install` | Hulls are constructed at RUNTIME from Defs, never authored into a scene, so "wire it where the thing is made" lands in the service that installs them. |

**The fleet needed the reflect pass on the OVERLAY shader, not the facet one.** A mesh hull's visible
image is not what the facet MRT holds — it is what the keyline resolve made of it, which the overlay
quad re-composes from `_HHHullScreenTex`. Mirroring the facet pass would reflect raw facet colour with
no keyline, no darkening and no hull-id filter: recognisably the wrong boat. So the reflection mirrors
the **overlay quad** and re-composes from the same resolved texture — which means the quad rasterises
**mirrored** while sampling **unmirrored**, flipping the fetch back about the pivot's screen row
(computed once in the vertex stage through `ComputeScreenPos`, which carries the render target's Y-flip
convention).

**What is NOT wired, and why.** Wharf structures and props sit on the **default sprite material**,
which has no `HHReflect` pass — a `ReflectiveObject` on one of those would join the filtered list and
draw nothing. Giving them a pass means a project sprite shader for decor, which is its own piece of
work. The wharf TILE kit is a tilemap, so it cannot carry a per-renderer component at all; when it is
wired it wants one reflector on the tilemap renderer with a pivot override, because that kit pivots
TOP-LEFT and its breakwaters on the CREST.

> ⚠️ **`useWaterLevel` was removed from `ReflectiveObject`, unused.** It set the mirror axis (a world-Y
> POSITION) from the tide (an ELEVATION in metres above datum) — different quantities, the same
> conflation the distance gate had. A floating hull needs no such option anyway: her pivot already
> rides the swell with her.

---

## 27. The capillary ripple band — the finest octave, gated by wind, face and framing (ADR 0027 #10)

The finest thing this shader drew was `_WindChopScale` at 0.7 — a **1.4 m** band, which is *chop*, not
ripples. §27 adds the ~0.12 m octave riding **on** the larger waves: the thing that makes water read as
*water* close up. It ships **OFF** (`_RippleStrength` 0 in `Water.mat`), so the owner's tuned sea is
byte-identical until he dials it in. C# twin: `WaterRipple` — `WindGate01` / `WindwardGate01` /
`FramingFade01` / `Band01` / `Amplitude01` / `SignedAdd`, mirrored one-for-one by the shader's
`RippleWindGate` / `RippleWindwardGate` / `RippleFramingFade` / `RippleField` — **change one, change BOTH
in the same PR** (`WaterRippleTests` pins the laws headless; `RippleBandProbeTests` proves the pixel
claim on a GPU and skips loudly without one).

**Tier A permanently.** `col.rgb` brightness only — never `depth`, `clip()`, `_WaterLevel`, the height
read, `_WaveFieldParams`, or anything the hulls ride. The ADR says it in as many words: *a ripple is not
a force*. Nothing enters the save.

### 27.1 Where it sits in the composite

Drawn **after** the §23 envelope value bands and **before** the caustics/foam/specular. Both sides are
deliberate: the envelope bands `lerp` `col.rgb` toward the palette anchors, so a ripple added before them
would be partly washed away — texture belongs on top of shade — while breaking water and glints
physically belong on top of the ripple. The swing is capped at **±0.10** (`RIPPLE_ADD_CEIL`), below the
swell-read band's 0.25 and the face shading's 0.15: the finest layer on the sea is the one most able to
turn into glare. The ADR 0015 palette guard-rail still owns the final colour downstream.

### 27.2 The three gates

| Gate | Reads | Off end | Why |
|---|---|---|---|
| **Wind** | `_Roughness` (sim-pushed) between `_RippleWindOnset` and `_RippleWindFull` | exactly 0 at/below the onset | No wind, no ripples — glass stays glass. Monotone and **saturating**: by a gale the whitecaps own the surface. |
| **Windward face** | `dot(waveSlope, windDir)` — the *shared* field's slope, already sampled for the §20 face shading | `_RippleWindwardGate` 0 returns exactly 1 (ripples everywhere) | Going **downwind** you climb the windward face, so a positive projection *is* that face. Ripples sit there and thin to `_RippleLeeFloor` in the lee — sheltered, never glass. **No new uniform.** |
| **Framing** | `_SeaFramingHeight` (metres of sea on screen) between `_RippleFadeNear` and `_RippleFadeFar` | `_SeaFramingHeight` ≤ 0 (unset) returns exactly 1 | The anti-**density** guard — see §27.3. |

> ⚠️ **The windward gate is skipped when the trains are not live.** A dead field publishes zero slope,
> which the gate would read as "not a windward face" and would erase the band on the legacy /
> edit-mode / bare-art-scene path. The fragment tests `trainsLive` and passes 1 there.

`Amplitude01` composes them as a **product**, so any single gate at zero kills the layer, and
`_RippleStrength = 0` is the exact passthrough the shipped material sits at.

### 27.3 ⚠️ The fade is keyed to the FRAMING, not to the zoom tier — and that is an ADR amendment

ADR 0027 made #10 conditional on a **per-discrete-zoom-tier amplitude fade**, on the premise that *"at a
wider zoom tier a ripple falls below one pixel"*, and told the build to drop the item if that fade could
not be made stable. **That fade does not exist and must not be built.** A report-only spike measured the
camera instead of assuming it:

- `PixelPerfectCamera.assetsPPU` is **locked at 32** (`ArtCameraSetup`, mirrored by
  `CameraFollow.AssetsPPU`), and `CameraFollow` frames a tier by changing the **reference resolution**,
  never world-metres-per-pixel (`upscaleRT` is never set; `gridSnapping = PixelSnapping`).
- So one pixel is 1/32 m = 3.125 cm at **every framing the game has**, and a 0.12 m ripple is **3.8 px**
  from the 5.625 m live-haul framing to the 90 m Coastal Packet. It never goes sub-pixel.
- Pinned by `RipplePixelFootprintTests`, which is a **tripwire**: unlock `assetsPPU`, or let a framing
  path drive the orthographic size independently of the reference resolution, and #10's original kill
  condition is live again with the band already shipped.

What **does** vary is the **cycle count**: the widest framing shows 33.75 m of sea against the tightest's
5.625 m, so ~**6× more ripple cycles** down-screen at the same pixel size. Close in, the band reads as
texture on the swell; wide open it reads as a dense field competing with the swell bands. Hence a fade
over *how much sea is on screen*.

`_SeaFramingHeight` is published by `WaterSurface.PushCameraFraming` via `Shader.SetGlobalFloat` — the
`WaveFieldBridge` / `GrassWindBridge` pattern — **every frame, not on the throttled tick**, because the
framing eases over the `CameraFollow` zoom tween and a few-Hz push would visibly step the density
through the ease. It is a **global**, not an MPB property, because it belongs to the *camera* rather than
to the water renderer.

> ⚠️ **It is a derived-physics PUSH, never a mood float.** `_SeaFramingHeight` must not enter
> `WaterSurface.MoodFloatNames` — anything in that list is eased from the eight preset materials and
> would double-drive it, the same trap `_Chop` / `_Roughness` / `_Flow` are kept out of (§12.1).

> **Unset means NO fade, deliberately.** Edit mode publishes 0 (rather than leaving a stale value from a
> previous Play session) and the shader's `<= 0` branch turns that into full strength — so the owner
> tuning in the Scene view sees the band he is tuning, instead of a silently blank sea. Same convention
> as `_WaveFieldParams`' "count 0 = not published".

### 27.4 Pixelation — the crawl law and the layer's own quantization

Both halves of the ADR's pixelation demand for #10 ship, and only the zoom half was retired:

- **World-grid quantization.** `RippleField` snaps the sample position on the **world** PPU grid
  (`Pixelize(worldXY)`, *then* scales) so a ripple cell belongs to a place on the water and stays there
  while the camera pans. Never screen space — that is the crawl law §3 states and the shader's Bayer
  dither, the §26 reflection lookup and the ADR 0022 facet pass all hold.
- **The layer's own quantization, DEFAULT ON.** `_RippleBands` (3) posterizes the band into solid steps
  through the shared `BandValue01`, Bayer-dithered only inside `_RippleDitherWin` around each boundary,
  indexed by the world-locked cell (`bay`, already read for the envelope bands — one dither read). This
  matters most here because `_DepthBands` is 0, so the base ramp lends no pixel character of its own and
  a smooth ripple would read as airbrushed shimmer. **Dither at the EDGE only**: full-range dither
  dissolves the steps back into the gradient this game is not (spike-measured, §3).

The wavefronts are broken out of a ruled grating by a slow pixelized value-noise wander (the §5 swell
idiom at this band's scale) — `RIPPLE_WANDER_*`, bounded under a half cycle so wavefronts stay
wavefronts.

### 27.5 Speed, and why it rides the §25 dispersion blend

`_RippleSpeed` is a world speed **along the wind**, defaulting to **0.09 m/s** — the wind-chop band's own
speed, so the ripple sits in the sea's established (deliberately slow) feel family at ~0.75 wavelengths
per second. The *physical* deep-water speed for 0.12 m is 0.43 m/s; at 3.8 px per cycle that reads as
temporal shimmer rather than ripple, which is why the shipped default is the feel value and not the
physical one.

It still goes through `DispersionBandSpeed` with its own `_DispersionRippleMult` (0.06, the same
convention as the chop / cross-swell / swell bands), so dialling `_DispersionScale` up does not leave the
ripple as the one band ignoring the relation. At the shipped `_DispersionScale = 0` this is bit-exact
`_RippleSpeed`.

### 27.6 Recommended starting values (the owner's dial-in)

`Water.mat` ships every one of these at its property default with **`_RippleStrength` at 0**. To see the
band, raise the strength; the rest are already at the recommended settings.

| Property | Shipped | Recommended | Note |
|---|---|---|---|
| `_RippleStrength` | **0** | **0.45** | The only knob that must move. 0.45 gives a ±0.045 brightness swing. |
| `_RippleWavelength` | 0.12 | 0.10–0.15 | 3.2–4.8 px at PPU 32. Below ~4 px it starts to moiré against the grid; the ADR's band is 0.08–0.15. |
| `_RippleSpeed` | 0.09 | 0.06–0.12 | Faster reads as shimmer, slower as a static texture. |
| `_RippleWindOnset` / `_RippleWindFull` | 0.05 / 0.45 | as shipped | Glass at a whisper, full band by a good breeze. |
| `_RippleWindwardGate` / `_RippleLeeFloor` | 0.7 / 0.15 | as shipped | 0.7 keeps the face read legible without emptying the lee. |
| `_RippleBands` / `_RippleDitherWin` | 3 / 0.5 | as shipped | Three solid steps is the pixel-art read; drop to 0 to compare against smooth. |
| `_RippleFadeNear` / `_RippleFadeFar` / `_RippleFadeFloor` | 16 / 30 / 0.2 | as shipped | Full through every small-boat framing, thinning across the trawler/packet tiers. |

---

## 28. The advected foam buffer — a wake as a mark on the sea (ADR 0027 #6)

**Status: SHIPPED 2026-08-01, OFF by default.** The ADR deferred this item to the fleet era on
2026-07-29; the owner overturned that on 2026-08-01 and, in the same sentence, gave it a look-target
the original spec never had:

> *"I want foam to move to now as well. The hull doesn't create realistic foam when bobbing etc."*

That second half is the reason this is built now. Everything the ADR's decision text says about #6 is a
*multi-boat* payoff — trails that merge, unbounded persistence at fixed cost — which is exactly why
deferring it was defensible. **Bobbing churn is a one-boat defect**, it is visible on a single moored
dory, and nothing in the project serves it: `BoatWakeEmitter` keys on **speed** (`SpeedOnset` is 0 at
rest, correctly, because it draws a *wake*), so a boat at her mooring is silent.

### 28.1 What it is

One persistent single-channel render target per camera, ping-ponged each frame, holding *"how much
churned foam is on this patch of sea"*. Each frame `IsoFacetHullFeature` runs one blit
(`HiddenHarboursFoamBufferAdvect.shader`) that:

1. **scrolls** the previous buffer by a **whole number of world cells** (the camera window's move plus
   the wind/current drift);
2. **decays** it by an exponential half-life factor;
3. **injects** a capsule of foam along each churning hull's swept segment.

The water shader samples the result as a coverage mask that **ADDS** to the foam already composed
(`WakeFoamCoverage`, in the same pre-grade dressing zone as the fringe foam and whitecaps, so the ADR
0015 palette guard-rail bounds it and it dims with the night like every other foam layer).

**`BoatWakeEmitter` stays.** The emitter is the young, bright churn at the stern; the buffer is the
mark left on the sea that persists and drifts after the boat has gone, plus the bobbing case. They add.
Whether the emitter's trail should later be *thinned* in favour of the buffer is an **owner look call**
and is deliberately not pre-empted.

### 28.2 🔴 The cell law — the one thing this can get catastrophically wrong

A render target is screen-space by nature. If the buffer's cells are camera-relative, the whole trail
**crawls** under every pan — the one artefact that would make this read as a screen filter rather than
foam on water. So:

- the window's origin is **snapped onto a world cell lattice** (`FoamBuffer.WorldCellOrigin`). It does
  not move *at all* for a camera pan smaller than one cell, and moves by whole cells otherwise;
- **the wind drift is quantized the same way** (`FoamBuffer.AdvectCells`), with the sub-cell remainder
  **banked** into the next frame. This goes further than the ADR's warning asks, and for a second
  reason beyond crawl: a fractional scroll has to be **resampled**, and resampling a buffer into itself
  every frame is a blur filter — the wake would smear into a smudge within seconds. Whole-cell moves
  are exact integer copies. Banking the remainder is what stops the foam travelling slower than the
  wind that is carrying it;
- the advect pass reads with `Load()` at integer texels, not a uv sample — no filtering, and no
  render-target Y-flip ambiguity (the `ObjectReflection` precedent, same reason);
- the water shader's read is a **world** position mapped through the cell-snapped window
  (`_HHFoamBufferWorld`) into a point-filtered target. There is no screen-space step anywhere in it.

`FoamBuffer.CameraRelativeOrigin` exists **only** as the wrong answer the tests measure the crawl
against. Nothing ships calling it. `FoamBufferTests` reports the sabotage as a number: across a camera
pan of **0.123 m — less than one 0.125 m cell** — camera-relative addressing slides a fixed patch of
sea **0.969 of a cell** inside its own cell (the boundary sweeps clean across the mark), while
world-anchored addressing slides **0.000**.

> ⚠️ **The buffer owns its grid constant, and this is not a style preference.** `FOAM_CELLS_PER_UNIT`
> is **8 cells/m** (0.125 m = **4 screen px** at PPU 32), mirrored by `FoamBuffer.CellsPerUnit` and
> pinned by a tripwire that reads the shader source. It is deliberately **not** the material's
> `_PixelsPerUnit`: that is an art knob the owner drags — **`Water.mat` ships it at 24, not 32** — and
> the C# side cannot read a material, so quantizing through it would let the two halves of the seam
> disagree silently. Same ruling, same reason, as `FETCH_MARCH_PPU`. It is also deliberately *coarser*
> than the pixel grid, which is the ADR's own "foam coarser than caustics" scale hierarchy.

### 28.3 Injection — both components of hull-vs-water relative motion

`FoamInjector` (drop it on any hull) reads two channels and adds them:

| Channel | Signal | Why |
|---|---|---|
| **Horizontal** | speed **through the water** — world velocity minus the tidal current | A boat carried along by the stream is stationary relative to the water she floats on and leaves no wake in it. |
| **Vertical** | \|relative heave rate\| — how fast the gap between hull and wave surface is opening or closing | **The owner's ask.** Zero speed, and she still churns. |

**How the bob signal is obtained.** A hull that tracked the surface perfectly would displace nothing and
churn nothing — **the churn is the hull's inertia losing the race with the wave face**. So the injector
samples the same displaced sea the ride already samples (`WaveFieldAnimator` + `ShoreFadeMath.DisplacedHeight`
through the `DisplacedSea` Core seam, at the surface's own `FreqScale` so it reads the sea *as drawn* —
the documented `_OceanSwellScale` defect class) and models the hull's response as a first-order lag
(`FoamBuffer.FollowSurface`). `hullResponseSeconds = 0` returns the surface exactly, so the bob channel
falls identically silent — a real off switch, not an approximation of one, and pinned as a test.

⚠️ **It reads the ride and never writes it.** No force, no pose, no heave goes back to Boats, and the
component holds no reference to any boat class — so a buoy, a raft or a swimmer can carry one later
without touching Boats or breaking rule 4 (Art references only Core).

**The slap shaping is super-linear.** `FoamBuffer.Shape01` normalises a rate against a knee and raises
it to an exponent (default **2.5** for the bob channel, **1.0** for the wake). Above 1 the response is
super-linear: the gentle end is pushed down toward nothing while a hard slap still reaches full white.
That is *"a hull slapping at the waterline churns, a hull breathing on a swell does not"* as a curve,
rather than as a steeper straight line. Every knee, exponent and weight is a tunable (rule 6).

Deposits are **capsules** along the segment swept since the last tick, not points: a boat at 3 m/s
crosses about a cell and a half per frame at 60 fps and several on a frame hitch, so a point deposit
would lay a trail that gets dashier the worse the frame rate.

### 28.4 Zero cost when idle — both gates

Nothing is recorded unless **both** are true (the `IsoFacetUrpPassTests` contract, CLAUDE.md rule 7):

- **a hull is churning water** — `FoamInjectionRegistry.Count > 0`. Every `FoamInjector` unregisters the
  moment it is off the water (hauled out, or over ground that has bared on the ebb), so a boat ashore
  costs nothing;
- **the owner has dialled the look in** — `_WakeFoamStrength > 0`, mirrored out of the live material by
  `WaterSurface` each push. At 0 the water shader draws nothing from the buffer, so filling one is pure
  waste.

The shipped state has **both gates shut**: `_WakeFoamStrength` is 0 in `Water.mat` and in all eight
presets, and nothing in any scene carries a `FoamInjector`. So there is no "buffer off" branch to keep
byte-identical — only an **absent pass**, which *is* the passthrough proof (the #8 shape exactly).

When the pass stops running, `FoamInjectionRegistry.BindIdle()` rebinds a **black 1×1** and zeroes the
window. Without that, a buffer that stops being filled would leave its last frame bound and a **frozen
wake** would hang on the sea for the life of the scene. The fallback is black, not grey: the mask is
read as coverage, and Unity's grey unbound placeholder would wash the entire sea with half-strength
foam on the first frame of every scene (the interior guard's lesson, verbatim).

### 28.5 Budget (rule 7 — and the later mobile port)

| | |
|---|---|
| Format | `R8` — a coverage mask needs one channel, and 256 levels are more than a posterized foam edge can show |
| Resolution | **Derived**: `extent × FOAM_CELLS_PER_UNIT`. At the 96 m default that is **768²**, so "one texel = one world cell" holds by construction and cannot be broken by typing a different number |
| Memory | 576 KiB × 2 (ping-pong) = **1.1 MB per camera** — against the `_SeabedTex` bake's 1.0 MB per region |
| Per frame | **one** fullscreen blit, over a **fixed 8-slot** injection loop (a compile-time bound — `[unroll]` over a runtime bound is a known magenta trap) |
| Window | 96 m square, camera-centred. Must comfortably exceed the widest framing (33.75 m down-screen, ~60 m across) or wake scrolls out of the window while still on screen |

### 28.6 Determinism boundary — the one knowing exception, and it is bounded

The buffer is **accumulated visual state**. It is **not** a deterministic function of
`(worldSeed, gameTime)` and **is allowed to differ run-to-run** with frame pacing, exactly as particles
are. It therefore:

- feeds **no simulation** — nothing in Boats, Fishing, Economy or the wave field may read it;
- enters **no save** (rule 5 / ADR 0008), and needs none: it is regenerated by sailing;
- is read by **the water shader's foam compose and nothing else**. That last clause is what contains the
  exception, and it is the invariant to defend in review.

Every function in the `FoamBuffer` twin is nonetheless pure and deterministic in its own arguments,
which is what keeps the maths testable headless even though the accumulation is not.

### 28.7 Recommended starting values (the owner's dial-in)

`Water.mat` ships every one of these at its property default with **`_WakeFoamStrength` at 0**, and no
scene carries a `FoamInjector`. Two steps to see it:

1. add a **Foam Injector** component to the dory (Add Component → Hidden Harbours → Art);
2. raise `_WakeFoamStrength` on `Water.mat`.

| Property | Shipped | Recommended | Note |
|---|---|---|---|
| `_WakeFoamStrength` | **0** | **0.8** | The only material knob that must move. Above ~1.2 the churn goes solid white and stops reading as foam. |
| `_WakeFoamThreshold` | 0.12 | 0.10–0.20 | Lower shows more of the trail's faint fringe; too low reads as a grey wash rather than churn. |
| `_WakeFoamSoftness` | 0.18 | as shipped | The soft edge of the band. |
| `_WakeFoamBands` | 3 | as shipped | Three solid tones is the pixel-art read; drop to 0 to compare against smooth. |
| Feature: foam window | 96 m | as shipped | Raise only if wake visibly stops at the screen edge at the widest framing. Cost is quadratic. |
| Feature: foam half-life | 6 s | 4–10 s | Visible lifetime is roughly 4× this. At 6 s a wake is half gone at 6 s and essentially gone by 30 s. |
| Injector: `strength` | 1 | as shipped | Per-hull scale. |
| Injector: `radiusMeters` | 0.9 | ≈ the hull's beam | A dory wants ~0.9; a coastal packet several times that. |
| Injector: `hullResponseSeconds` | 0.2 | as shipped | The hull's vertical inertia (matches `BoatWaveMotion`'s own smoothing). **0 disables the bob channel entirely.** |
| Injector: `slapExponent` | 2.5 | 2.0–3.5 | Higher = only genuinely hard slaps churn. |
| Injector: `slapRateKnee` | 0.5 m/s | as shipped | Measured: a 0.2 s hull in a 0.6 m / 0.48 Hz chop peaks at ~0.89 m/s relative heave, so 0.5 saturates on the hard hits and stays quiet on a lazy swell. |

**What to look for.** Moor the dory in chop with the strength up: foam should churn around the
waterline while she bobs, streak downwind, and fade over seconds. Sail a circle: the wake should stay
painted on the sea *where the boat went*, drifting with the wind, not glued to the stern. Pan the
camera with foam on screen: **nothing may crawl.**

> ℹ️ **One stated divergence.** The buffer advects along the **global** wind/current blend
> (`WaterSurface.FoamDriftDirection` × `_Flow`), without `FoamDriftDir()`'s per-position shoreward bias:
> a rigid buffer scroll is uniform by construction and cannot carry a position-dependent term. So near a
> beach, buffered foam drifts on the wind/current axis while the shader's own fringe foam also leans
> shoreward. Small, and stated rather than discovered.
