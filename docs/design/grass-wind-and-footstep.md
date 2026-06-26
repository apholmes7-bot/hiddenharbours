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
- **Footstep** = bend away from `_PlayerWorld` by `(1 - smoothstep(0, _FootRadius, dist))`. Recovery is
  automatic — it tracks the *live* player position every frame, so nothing is stored per tuft.
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
- **`GrassFootstep`** (`Assets/_Project/Code/Art/`) — a tiny component on the player (or any mover). Each
  frame it sets `Shader.SetGlobalVector("_PlayerWorld", transform.position)`. That's the whole footstep
  system — recovery is the shader tracking the live position.

Both are **visual-only**: they drive no simulation and save nothing (rule 5). Determinism-sensitive math
(`WindToShaderVector`, `FootstepFalloff`) lives as pure static methods mirrored from the HLSL and is unit
tested headless.

### Performance (rule 7)
One material, GPU-instanced / dynamic-batched; all sway + bend in-shader; two global vectors set on a
throttled tick (wind) / per frame (player) regardless of tuft count; no per-frame allocation. Hundreds
of tufts stay cheap and the later mobile port stays viable.

## The demo — **Hidden Harbours ▸ Build Grass Test**
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
grass reads the shared wind automatically. A lingering "trail you stomped down" is a deliberate later
upgrade (today's footstep bend is a soft radius that follows the player and springs back).
