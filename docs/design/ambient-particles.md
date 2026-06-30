# Living-coast ambient particles

> Cheap, pooled, atmospheric VFX that make St Peters feel **alive and inhabited** — sea mist drifting
> over the water, hearth smoke off the cottage chimney, gulls wheeling over the harbour, and dust motes
> shimmering in the daylight air. All ride the **same deterministic wind** the grass and water read (so a
> gust moves the whole coast together), and all **dim/warm with the day/night cycle**. Authored by
> `art-pipeline`. Pillars served: **P3 A Living Working Coast** (a lived-in, breathing coast) and
> **P5 Cozy but with Teeth** (the cosy hearth-smoke read), with **P1 The Sea Has Moods** (mist thickens
> with fog and sea-state; everything leans on the shared wind).

## The look in one breath
A few dozen soft pixel-art sprites, total, drifting on the breeze. Mist creeps low over the sea and
thickens when it's foggy or rough. A thin column of smoke rises off the cottage flue and bends downwind.
A couple of gulls wheel lazy loops, on and off. By day, faint motes hang in the air and shimmer; after
dark they fade away. Nothing here is loud — it's the ambient life under the gameplay.

## Design constraints (CLAUDE.md)
- **Visual-only (rule 5).** These drive **no** simulation and save **nothing**. Cosmetic motion reads
  `Time`/the clock and a **deterministic hash** for variety — never `System.Random` for anything that
  could touch the sim. They only **read** the shared globals; they never write the sim.
- **Core/global reads only (rule 4).** The sea mood is read **only** through the Core
  `GameServices.Environment` accessor; the wind + day/night are read from the existing global shader
  uniforms (`_WindWorld`, `_DayNightTint`) that the grass/water/day-night systems already publish. No
  feature module reaches into another's concrete classes.
- **Performance is a feature (rule 7).** Every effect uses a **fixed, recycled pool** of
  `SpriteRenderer`s (zero per-frame heap allocation), **one shared sprite + material** per effect
  (batched), and a **throttled tick** (the sim is slow). A few dozen particles, total, across all
  effects. The motes early-out entirely when it's fully dark, so they cost nothing at night. Mobile-
  portable.
- **No magic numbers (rule 6).** Every density/rate/drift/size/lifetime/colour/fade knob lives in a
  per-effect serialized config struct, editable by the owner on the component.

## Cohesion — one wind, one light
The whole point is that the coast moves **together**:

- **Wind.** `GrassWindBridge` already publishes the deterministic sim wind to the global `_WindWorld`
  (direction · normalized 0..1 strength) — the same global the grass shader and water read. The ambient
  effects read that **same** global (`AmbientGlobals.Wind`) and scale it per-effect (`WindResponse` /
  `WindDrift`): a gust drifts the mist, bends the smoke plume, and skews the gull loops the same way it
  leans the grass and ruffles the sea. With no sim present (a bare scene), `_WindWorld` is whatever a dev
  test-wind sets (or zero → the effects creep on their own base-drift).
- **Light.** `DayNightController` publishes the global `_DayNightTint` (the whole-frame multiply tint).
  The ambient effects read it (`AmbientGlobals.DayNightTint`) and **multiply their colour by it** so they
  dim and warm exactly with the scene, plus a per-effect `NightFade` (how strongly night kills the
  effect) and, for mist, a `MoonlightCatch` floor so the haze faintly catches moonlight instead of
  blacking out. When no cycle is installed yet, a near-black/unset read is treated as plain daylight.

## The four effects

### 1. Sea mist / shore spray — `SeaMistEmitter` (self-installing)
Pooled soft wisps drifting low over the water, kept centred on the camera so a small fixed pool always
covers what the player sees. **Density** is `AmbientParticleMath.MistIntensity(visibility, seaState, …)`:
a small `BaselineIntensity` always-on shimmer, **more** when visibility is low (fog) and **more** when
sea-state is up (spray off the whitecaps) — the same linear sea-state scale the boat wake uses, so mist
thickens exactly when the sea does. Wisps fade in/out over a soft life envelope and drift on the shared
wind. Subtle by default (low `MaxAlpha`).

### 2. Chimney smoke — `ChimneySmoke` (drop-on + builder hook)
The one **positioned** effect. A thin pooled column whose puffs **rise** (`RiseSpeed`) and **bend
downwind**: the lateral drift grows with the *square* of a puff's age (`AmbientParticleMath.SmokePosition`),
so the higher/older a puff the further downwind it's been carried — the column bends over rather than
shearing. A faint per-puff sway keeps it breathing. Dropped on the cottage chimney and wired into
`StPetersBuilder` at the cottage (so a **Build St Peters** re-run shows hearth smoke). `NightFade` is low
— the hearth burns at night too.

### 3. Gulls — `GullFlock` (self-installing)
A **few** gull-silhouette sprites wheeling on smooth, looping, varied paths
(`AmbientParticleMath.GullPosition` — a wandering Lissajous figure, different harmonics on each axis so
it's a wheel, not a circle), each bird with a deterministic radius/period/phase/variant so a small flock
spreads out. Each gull is only on-screen for `ActiveFraction` of its loop (fading in/out at the window
edges) so birds appear **occasionally**, not constantly. The loops skew downwind; the sprite flips to
face its travel direction (`GullHeading`). Gulls roost at night (high `NightFade`).

### 4. Dust motes / pollen — `DustMotes` (self-installing, optional)
The lightest touch: tiny pooled specks that drift on the wind and **bob** (a slow sinusoid,
`MoteBob`) so they hang and shimmer rather than fall. `NightFade` = 1 by default → motes are a daylight
effect and vanish after dark (and the whole tick early-outs when it's dark, so they're free at night).

## Where the math lives (testable headless)
All the feel-math is pure and static in **`Assets/_Project/Code/Art/AmbientParticleMath.cs`** — the
hash, life envelope, drift integration, mist-intensity response, day/night brightness→opacity +
moonlight floor, the smoke plume shape, and the gull flight path/heading. Like `DayNightMath` and
`WaterSurface`, this lets the spawn/drift/lifecycle curves be **EditMode-tested headless** (the
determinism guard, rule 5) without opening Unity — see
`Assets/Tests/EditMode/Art/AmbientParticleMathTests.cs`. The MonoBehaviour shells
(`SeaMistEmitter` / `ChimneySmoke` / `GullFlock` / `DustMotes`) are thin: read the shared globals + the
Core sim, call the math, write the pooled sprites.

The sprites are generated in code (`AmbientGlobals.BuildSoftPuff` / `BuildDot` / `BuildGull`) — soft,
point-filtered, crisp pixel-art — so the effect is self-contained (one shared sprite + material per
effect, batched) and depends on no imported PNG that might slice to a single sub-sprite.

## How the owner sees them
- **Hidden Harbours ▸ Build Atmosphere Test** (mirrors *Build Grass Test*) drops a small sea backdrop +
  a cottage-with-chimney into the current scene. Press **Play**: the mist, gulls and motes self-install
  and appear; the chimney carries live smoke; a gentle dev test-wind drifts them all even with no sim.
  Delete the spawned `AtmosphereTest` object to fully revert.
- In **St Peters**, the chimney smoke is wired at the cottage by **Build St Peters**; the mist, gulls and
  motes self-install in every scene with no wiring. Tune each effect live on its component
  (`SeaMistEmitter` / `ChimneySmoke` / `GullFlock` / `DustMotes`).
