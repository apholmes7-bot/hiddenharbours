# Living grass — wind sway + footstep bend

> Cozy, performant grass for the on-foot areas (St Peters clearings / forest) that **sways with the
> wind** and **bends under the player's footsteps**, springing back as they pass. Pixel-art, all motion
> in-shader. Authored by `art-pipeline`; world-content integrates it into the St Peters scene as a
> follow-up. Pillars served: **P1 The Sea Has Moods** (grass reads the *same* wind as the water, so a
> gust moves the whole world together) and **P3 A Living Working Coast** / **P5 Cozy but with Teeth**
> (the coast feels alive and reactive underfoot).

## The look in one breath
Grass-tuft sprites are *planted at their base* and bend at the *top*. A steady wind leans them over; a
gust ripple travels **downwind** across the whole patch (decorrelated per-tuft so it never sways in
lockstep). Walk through and the blades part **away** from you and spring back once you leave. Every
blade of motion happens on the GPU — there is no CPU per-blade animation and no saved per-blade state.

## How it's built

### Shader — `HiddenHarbours/GrassWind`
`Assets/_Project/Art/Shaders/HiddenHarboursGrass.shader` — a URP 2D **unlit sprite** shader (text HLSL,
not Shader Graph, so it builds headless like the water shader). It bends the sprite in the **vertex**
stage:

- **Bend weight** = `saturate(uv.y)` squared — `0` at the root, `1` at the tip — so the base stays
  rooted and the tip moves most. (Tufts import with a **bottom-centre pivot**, so the transform origin
  is the planted root.)
- **Wind** = a steady lean along the wind direction **plus** a travelling gust ripple. The ripple
  projects world position along the wind and advances with time, so one gust rolls across the entire
  field — the same cohesion the water swell has. A per-`_PhaseGrid`-cell phase offset decorrelates
  neighbouring tufts. Amplitude scales with wind strength, plus a small wind-independent `_IdleSway`
  baseline so grass always has a little life.
- **Footstep trail** = bend away from the player's recent **path**, not a single point. `GrassFootstep`
  publishes the last N world positions (`_GrassTrail`); each disturbs only a **footprint-sized** radius
  (`_FootRadius`, ~0.5 m) and fades by recency, so the grass parts **along the trail the player walked**
  and springs back behind them — a trodden path, not a halo circling the player. The shader takes the
  strongest nearby point (max, not sum) so overlapping footprints never stack into a bulge.
- **Pixel-art faithful**: the bend offset is **snapped to the PPU grid** (PPU 32), point-sampled, like
  the water shader. The blade also dips slightly in Y as it bends (`_BendY`) so a hard bend reads as
  folding over, not stretching.

> **Shader cautions honoured** (this project lost hours to a magenta shader): no `+`/operator
> characters in any `[Header(...)]` label or property string (ShaderLab parse error → magenta); no
> `[unroll]` over a runtime loop bound (this shader has no loops). The grass material's shipped variant
> is force-compiled headless by a CI guard (below), so a broken grass shader fails CI **red**.

### The two bridges (cross-module via Core only — rule 4)
Both just publish a **global** shader vector; every grass instance reads it with no per-object wiring.

- **`GrassWindBridge`** (`Assets/_Project/Code/Art/`) — **self-installing** via
  `RuntimeInitializeOnLoadMethod` (a hidden `DontDestroyOnLoad` host, mirroring the water plumbing but a
  **separate** component — it does not touch `WaterSurface`/the water material). On a throttled tick it
  reads `GameServices.Environment.Sample().WindVector` — the **same deterministic wind the water reads**
  — normalizes the strength to `0..1` against `_windForFullSway`, preserves the (time-wandering)
  direction, and sets `Shader.SetGlobalVector("_WindWorld", dir * strength)`. So **grass + water move
  together**. When there is no sim yet (EditMode / pre-boot / the bare demo) it publishes nothing,
  leaving the grass on its idle baseline.
- **`GrassFootstep`** (`Assets/_Project/Code/Art/`) — a tiny component on the player (or any mover). It
  keeps a ring buffer of the walker's recent positions (a new footprint every `_pointSpacing` m, each
  fading over `_trailLifetime` s) and uploads it once per frame via
  `Shader.SetGlobalVectorArray("_GrassTrail", …)` (no per-frame allocation). The spring-back is the
  recency fade — still no per-tuft state, nothing saved.

Both are **visual-only**: they drive no simulation and save nothing (rule 5). Determinism-sensitive math
(`WindToShaderVector`, `FootstepFalloff`) lives as pure static methods mirrored from the HLSL and is unit
tested headless.

### Performance (rule 7)
One material, GPU-instanced / dynamic-batched; all sway + bend in-shader; two global vectors set on a
throttled tick (wind) / per frame (player) regardless of tuft count; no per-frame allocation. Hundreds
of tufts stay cheap and the later mobile port stays viable.

## The demo — **Hidden Harbours ▸ Dev ▸ Build Grass Test**
`Assets/_Project/Code/Tools/Editor/GrassTestBuilder.cs` (a separate dev builder, like *Build
Boat-Rotation Test* — it does **not** touch the St Peters scene builder). It drops a patch of tufts (one
shared material) + a movable red avatar (`GrassDevWalker` WASD/arrows + `GrassFootstep`) into the current
scene. A `GrassDevWind` on the root feeds a gentle **veering test wind** *only while there is no sim*, so
the demo sways out of the box; the moment the real environment sim is present, `GrassWindBridge` takes
over the same global off the deterministic wind. Reversible: delete the `GrassTest` object.

For density and a painterly read (matching the owner's evergreen-clearing reference), the builder
scatters a **mix of three greybox tuft variants** — `GrassTuft` (medium), `GrassTuft_Short`,
`GrassTuft_Tall` — with per-tuft **scale and tint jitter** (the shader multiplies vertex colour, so each
tuft shades within the palette). These are **placeholders**: drop the owner's final tuft art into
`Assets/_Project/Art/Sprites/` (bottom-centre pivot, Point, PPU 32) and the system drives it unchanged.

## Tunable knobs (no magic numbers — rule 6)
On the **Grass** material (`Assets/_Project/Art/Materials/Grass.mat`): `_SwayAmount`, `_IdleSway`,
`_WindLean`, `_SwaySpeed`, `_GustScale`, `_GustStrength`, `_PhaseGrid`, `_BendY`, `_FootRadius`,
`_FootStrength`, `_PixelsPerUnit`, `_Color`, `_AlphaClip`. On `GrassWindBridge`: `_windForFullSway`
(wind speed that maps to full sway — mirrors the water's wind-for-full-roughness) and `_refreshHz`.

## Integration handoff (world-content)
Place grass-tuft `SpriteRenderer`s (sharing `Grass.mat`) in the St Peters clearings, and put a
`GrassFootstep` on the on-foot player. No wind wiring is needed — `GrassWindBridge` self-installs and the
grass reads the shared wind automatically. The footstep bend is a **fading trail** along the path the
player walks (a footprint-sized disturbance per recent position), so the grass reads as trodden-down
rather than a halo orbiting the player.

## ⚠️ The bend curve requires a tessellated sprite (measured 2026-07-25)

Both wind shaders shape their sway in the **vertex stage** — grass as `bendW = uv.y²`, trees as
`bendW = smoothstep(_TrunkAnchor, 1, uv.y)²`. A sprite imported as **FullRect is a four-vertex
quad**, so that expression is only ever evaluated at `uv.y = 0` and `uv.y = 1`, and the rasteriser
interpolates **linearly** between them. Every shaping term then does nothing: the squaring collapses
(0² = 0, 1² = 1) and `_TrunkAnchor` cannot change `smoothstep(a,1,0) = 0` or `smoothstep(a,1,1) = 1`
for **any** value of `a`.

**The grass tufts were always `Tight`, so grass was never affected.** All 43 trees shipped as
`FullRect`, so the "trunk stays planted" promise in that shader's own header was not being kept —
the whole sprite sheared from its bottom row. Measured on `Tree38` at the shipped material values:
peak sway 0.133 m = 4.3 px, **worst deviation 0.362 of full sway (~1.5 px) near mid-canopy**, and
3.5× the intended motion at half height. Small in absolute terms, which is why nobody reported it;
inert knobs are worse than wrong ones, because the next person tunes them and sees nothing.

It matters more from here on: the [Acadian tree rig](../art/tree-rig-kit/README.md) pivots at the
**trunk foot** with a near-root flare *below* it (20 px on Red Spruce), and a sliding root flare
reads as broken in a way a sliding trunk did not. Note the rig's own geometry puts that trunk foot
at `uv.y = 20/166 = 0.120` — so the shipped `_TrunkAnchor` of 0.14 is already about right for the
new art, and wants to come from `Trees.json` per species rather than staying one material constant.

**Pinned by** `Assets/Tests/EditMode/Art/WindBendTessellationTests.cs`, which asserts the
*capability* rather than the import flag: at least three distinct vertex heights, and at least one
vertex **below** the trunk anchor, or there is no row the shader can hold still while the crown moves.

### ✅ `_TrunkAnchor` is now per species (2026-07-26)

The in-engine tree bake (`TreeRigBaker` → `Assets/_Project/Art/Foliage/Trees/Trees.json`) writes
`trunkAnchor = nearFlarePad / cellH` for each species, read from the rig's own `sheetSpec()`.
Measured across the ten:

| | anchor | | anchor |
|---|---|---|---|
| Black Spruce | **0.0833** | White Birch | 0.1091 |
| White Pine | 0.0881 | Red Spruce | 0.1205 |
| White Cedar | 0.0942 | Red Maple | 0.1346 |
| Trembling Aspen | 0.0962 | Balsam Fir | 0.1400 |
| Tamarack | 0.1074 | Red Oak | **0.1447** |

So the single shipped `0.14` on `Art/Materials/Tree.mat` sits at the **top** of the range and
over-anchors eight of the ten — it freezes canopy that should move rather than letting a flare
slide, which is the safer of the two failures but still not what the art says. Read the value with
`TreeKitCatalog.TrunkAnchorFor(contract, species, stage)`; the consumer that *applies* it (a
per-renderer `MaterialPropertyBlock`, or a material per species) belongs with tree placement, which
has not been built yet. `Tree.mat`'s constant is deliberately untouched so the 43 old hand-drawn
trees keep working.

⚠️ **The equivalent assert on the old set is vacuous.** `WindBendTessellationTests
.TreeSprites_HaveAVertexBelowTheTrunkAnchor_SoTheBaseCanBeHeldStill` normalises `uv.y` against each
sprite's own min/max and then asks for a vertex below the anchor — but the minimum vertex maps to
exactly `0`, and `0 < anchor` for every positive anchor, so it cannot fail. The rig kit's equivalent
(`TreeSheetImportTests.TheAnchorLine_ActuallyCutsEverySpritesMesh_NotJustTouchesItsBottom`) uses
**absolute** atlas `uv.y` — valid because those sheets are one sway row, so `uv.y` and cell-relative
height are the same number the shader's `saturate(IN.uv.y)` reads — and requires the anchor to
genuinely *split* the mesh. Verified by sabotage: on `FullRect` it fails with "only 2 distinct
vertex heights".
