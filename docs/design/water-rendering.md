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
