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
>    0.3 m, `Swash Speed` 0.5, `Swash Scale` 0.25 — the fast in/out shoreline wash, §5.6). No graph
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
`_SwashScale` (default 0.25).** Before this, the **only** in/out shoreline motion was the slow
deterministic tide. The swash adds a **fast, continuous, cosmetic** waterline wash — "waves crashing in
and out" — driven off `_Time` in the shader (`BeachSwash`): a two-beat sine (`_SwashSpeed` sets the
rate; `_SwashScale` varies the phase along-shore so it doesn't pulse as one flat line) produces a signed
**depth offset** (`±_SwashAmplitude` m) that advances (run-up) and recedes (backwash) the wet edge.

- **Confined to the foam band (the P1 integrity rule).** The swash offset is multiplied by a band gate
  (`1 − smoothstep(0, foamWidth·2 + |amp|, depth)` — full at the wet edge, **zero** by the band reach)
  and applied **only** to a *local foam-only depth* (`foamDepth`). The real `depth` that drives
  `clip()`, the deep-water tint (`dt`), and the caustic gate is **never touched** — so deep water does
  not move and **the cosmetic wash cannot move the gameplay waterline** (it's foam *dressing* on top of
  the real depth read: saves nothing, drives no sim — rule 5). Set `_SwashAmplitude = 0` to disable.

The swash math has a pure-C# twin in `WaterSurface.cs` (`SwashOffset` + `SwashBandGate`) so the
oscillation, the amplitude bound, and **the band-confinement invariant** are unit-tested headless
(`Assets/Tests/EditMode/Art/ArtRenderingTests.cs`) without opening Unity — the twin feeds no sim and is
not pushed to the material; the shader owns the live wash.

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
> long wavelength), `_OceanSwellSpeed` (0.018), `_OceanSwellStrength` (0.16), `_OceanSwellSharpness` (1.4).
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

The default anchors (St Peters) are all from `Art/Materials/WaterPresets/`:

| Axis end | Anchor preset | Mood |
|---|---|---|
| **Base** (fair/clear/calm-ish) | `Water_NorthAtlantic` | the cold teal-navy "home" / region default |
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
