# Boat wake — a foam-particle trail that lives its own life

> A realistic wake behind a moving boat. The owner's brief, verbatim: *"the wake animation [should]
> follow the boat, it needs to travel with the current as the waves distort it, once it loses force a
> distance from the boat [it dissipates]."* Visual-only, deterministic, pooled. Owned by
> `gameplay-systems` (Boats lane). Pillars served: **P1 The Sea Has Moods** (the wake reads the *same*
> tidal current + sea-state the water shader reads, so wake and water move together — the set carries the
> foam, a rough sea breaks it up) and **P2 From Dory to Dynasty** (every boat that makes way leaves one).

## The look in one breath
Foam puffs are **shed from the stern** as the boat makes way, in two diverging streams that form the
classic **Kelvin V** (plus an optional central prop-wash stream). Each puff, once shed, is **on its own**:
it drifts on its **own momentum + the live tidal current**, its own push **fades** so far astern it just
sets with the tide, the **waves wobble it** (scaled by sea-state — glassy leaves it alone, a gale breaks
it up), and over a **lifetime** it **fades to nothing and spreads/softens**. A distance astern = faint +
spread = dissolved. No two puffs are identical (deterministic per-puff jitter), and nothing is saved.

## How the four brief points are realised

| Brief point | How |
|---|---|
| **follow the boat** | Puffs are emitted at the **stern** (`pos − transform.up · sternOffset`) at a rate **proportional to boat speed** (`Velocity.magnitude`). **None** below a small speed threshold, **none** when `IsAground`. Two streams at a tunable **V half-angle** (~19°) diverge to form the wake V; the boat's speed seeds each puff's initial outward+astern wash, so the freshest, brightest foam sits right under the stern and the V trails the hull. |
| **travel with the current** | Every live puff integrates `pos += (ownVel + CurrentVector) · dt` each tick — it **drifts with the live tidal set** (read once per throttled tick through the Core `EnvironmentSample`) on top of its own momentum. `ownVel *= velocityDecay^dt` each tick, so the wake's **own push fades** ("loses force"); far from the boat **only the current's drift remains**. |
| **waves distort it** | A deterministic **value-noise** displacement of `(worldPos, time, per-puff seed)`, **scaled by sea-state roughness** (`SeaState → 0..1`, the same linear scale the water surface uses for choppiness), wobbles each puff at render time. Glassy water (roughness 0) → **no** distortion; a rough sea → the wake **wobbles and breaks up**. The wobble is a display-only offset — it never accumulates into the integrated position, so it can't drift the wake. |
| **dissipate** | Each puff has a **lifetime**. Over its life its opacity **fades** to 0 (monotonic — a puff never re-brightens) and its size **spreads/softens** (monotonic growth to `base · spreadFactor`). So a time/distance astern reads as faded + spread = dissolved. |

## How it's built

- **`Assets/_Project/Code/Boats/WakeParticleSystem.cs`** — the **pure, engine-light** simulation. A fixed
  array of foam-puff structs with all the feel-math as **side-effect-free static functions** (emission
  rate vs speed, stern point, V-wing emit velocity, advection + decay, life fade/spread, sea-state-scaled
  wave distortion). No `MonoBehaviour`, no `System.Random` — a deterministic integer **hash** drives the
  per-puff jitter and the wave noise. This is what the EditMode tests hammer headless.
- **`Assets/_Project/Code/Boats/BoatWakeEmitter.cs`** — the **self-installing** Unity driver. A
  `RuntimeInitializeOnLoadMethod` spawns **one** hidden `[DontDestroyOnLoad]` host before the first scene
  (mirroring `GrassWindBridge` / the audio director), so there is **no builder change and no builder
  re-run** for the owner — sail any boat and it leaves a wake. The host discovers `BoatController`s on a
  cheap throttled scan and gives each a per-boat rig: its own `WakeParticleSystem` + a fixed pool of
  `SpriteRenderer`s. Each tick it reads the boat's public surface (`Velocity`, `IsAground`, bow =
  `transform.up`) and the sea **only through Core** (`GameServices.Environment.Sample()` → `CurrentVector`
  + `SeaState`; `GameServices.Clock.TotalSeconds` for the wobble phase), then emits, advects and renders.
- **Foam sprite** is **generated in code** (a small soft round point-filtered puff at PPU 32) — so the
  effect is self-contained and avoids depending on the multiple-sprite-mode `BoatWake.png` (which
  `LoadAssetAtPath<Sprite>` can't return). One shared sprite + material → the puffs batch.

## Discipline honoured (CLAUDE.md rules)
- **Rule 4 (seams):** reads the sea through the Core `IEnvironmentService` / `IGameClock` accessors only —
  never the Environment concrete classes; reads the boat through its public API. Drives no other module.
- **Rule 5 (deterministic, save-nothing):** the wake **reads** the sim and **drives no sim, saves
  nothing**. The per-puff jitter and wave wobble are a deterministic hash keyed by the emit index /
  world-pos / time — **no `System.Random`**, no hidden global randomness in any sim path (it's scoped to
  the VFX). Identical inputs reproduce identical foam (EditMode-guarded).
- **Rule 6 (no magic numbers):** every knob lives on `WakeConfig` (serialized on the emitter): shed-rate-
  per-speed, speed threshold, stern offset, V half-angle, central-stream toggle, wash-speed scale,
  velocity decay, lifetime, start alpha, fade power, spread factor, foam size, lifetime/size jitter, and
  the wave-distort amount/frequency/speed — plus pool size, max boats, foam colour and sorting on the
  emitter.
- **Rule 7 (perf):** a **fixed pool per boat**, dead puffs recycled in place → **zero per-frame
  allocation**; one shared sprite/material (batched); the sim sampled once per throttled tick. Mobile-
  portable. One active player boat dominates cost.

## Tunables & defaults (`WakeConfig.Default`)

| Knob | Default | Meaning |
|---|---|---|
| `ShedPerSpeed` | 6 | Foam puffs/sec per (m/s) of speed over threshold |
| `SpeedThreshold` | 0.4 | m/s below which no wake forms |
| `SternOffset` | 0.5 | m astern of the boat origin the foam sheds |
| `VHalfAngleDeg` | 19 | Kelvin-V half-angle of the diverging wings |
| `CentralStream` | true | Add a central turbulent prop-wash stream |
| `WashSpeedScale` | 0.35 | Boat-speed → initial wash speed |
| `VelocityDecay` | 0.35 | Per-second retention of a puff's own momentum (<1 = fades) |
| `Lifetime` | 2.2 | s a puff lives before fully dissolved |
| `StartAlpha` | 0.7 | Opacity at birth |
| `FadePower` | 1.4 | Fade shaping (1 linear, >1 lingers then drops) |
| `SpreadFactor` | 2.2 | How much a puff grows over its life |
| `FoamSize` | 0.35 | m, puff size at birth |
| `LifetimeJitter` / `SizeJitter` | 0.25 / 0.3 | ± deterministic per-puff variation |
| `WaveDistortAmount` | 0.18 | m, max wobble at full sea-state roughness |
| `WaveDistortFrequency` | 0.6 | 1/m, spatial frequency of the wobble |
| `WaveDistortSpeed` | 0.5 | how fast the wobble swirls |
| `_poolPerBoat` | 96 | max live puffs per boat (fixed pool) |
| `_maxBoats` | 4 | boats driven at once (player dominates) |
| `_tickHz` / `_rescanHz` | 30 / 1 | sim tick / boat-rescan cadence |

## Tests
`Assets/Tests/EditMode/WakeParticleSystemTests.cs` — pure-logic guards for all four brief points
(emission gating vs speed / aground; stern + diverging V geometry; advection by velocity+current and
momentum decay toward current-only drift; monotonic fade-to-0 and monotonic spread; lifetime death;
sea-state-scaled + bounded + deterministic wave distortion) plus whole-run determinism (same inputs →
bit-stable particle field, no RNG).

## Future work (not built)
The procedurally-generated round foam puff is a **greybox** placeholder — an art pass can swap in
authored foam sprites (e.g. a sliced `BoatWake.png`) on the same pooled renderers without touching the
sim. A bow-wave / quarter-wave layer and turbulence at the rudder are natural additions on the same
particle system. Larger hulls up the ladder could scale `WakeConfig` by `BoatHullDef` (a wider, heavier
wake for the dragger) — data-driven, no new mechanic.
