# Boat wake — a foam-particle trail that lives its own life

> A realistic wake behind a moving boat. The owner's brief, verbatim: *"the wake animation [should]
> follow the boat, it needs to travel with the current as the waves distort it, once it loses force a
> distance from the boat [it dissipates]."* Visual-only, deterministic, pooled. Owned by
> `gameplay-systems` (Boats lane). Pillars served: **P1 The Sea Has Moods** (the wake reads the *same*
> tidal current + sea-state the water shader reads, so wake and water move together — the set carries the
> foam, a rough sea breaks it up) and **P2 From Dory to Dynasty** (every boat that makes way leaves one).

## The look in one breath
Foam puffs are **shed astern** as the boat makes way, placed **directly on two diverging Kelvin-V arms**
(plus a turbulent stern-fill churn between them) so the wake reads as a **crisp V**, not a soft trail. Each
puff, once shed, is **on its own**: it drifts on its **own momentum + the live tidal current**, its own push
**fades** so far astern it just sets with the tide, the **waves wobble it** (scaled by sea-state — glassy
leaves the V crisp, a gale breaks it up), and over a **lifetime** it **fades to nothing and spreads/softens**.
A distance astern = faint + spread = dissolved. No two puffs are identical (deterministic per-puff jitter),
and nothing is saved.

### How the V is built (the crisp-V fix)
The original wake shed every puff at a single stern point and pushed the two streams outward with velocity.
Because that outward push *decays* (the wake must "lose force"), the arms collapsed back inward within a
second and the V read as a soft cone/trail. The fix places each wing puff **directly on the arm geometry**:
from the stern apex it walks **astern + outward at the Kelvin half-angle** by a deterministic distance up to
`ArmLength`. Two consequences: (1) the arms are **straight diverging lines that widen with distance by
construction** — a true V — and (2) the spread no longer depends on velocity, so the puffs can still *lose
force and advect with the current* without the V folding up. A tunable `SternFillFraction` of puffs fill the
churn **between** the arms, bounded to stay **inside** the V so they never blur the crisp edges. Glassy water
keeps the edges razor-sharp; sea-state wobble (brief point 3) breaks them up as it roughens.

## How the four brief points are realised

| Brief point | How |
|---|---|
| **follow the boat** | Puffs are emitted astern of the **stern** (`pos − transform.up · sternOffset`) at a rate **proportional to boat speed** (`Velocity.magnitude`). **None** below a small speed threshold, **none** when `IsAground`. Each wing puff is placed **directly on the arm** at a tunable **V half-angle** (~19°) and a deterministic distance up to `ArmLength` (`ArmEmitPoint`), so the two arms are crisp diverging lines that **widen with distance** and the V trails the hull from its apex at the stern. A `SternFillFraction` of puffs fill the churn between the arms (`SternFillPoint`, bounded inside the V). |
| **travel with the current** | Every live puff integrates `pos += (ownVel + CurrentVector) · dt` each tick — it **drifts with the live tidal set** (read once per throttled tick through the Core `EnvironmentSample`) on top of its own momentum. `ownVel *= velocityDecay^dt` each tick, so the wake's **own push fades** ("loses force"); far from the boat **only the current's drift remains**. |
| **waves distort it** | A deterministic **value-noise** displacement of `(worldPos, time, per-puff seed)`, **scaled by sea-state roughness** (`SeaState → 0..1`, the same linear scale the water surface uses for choppiness), wobbles each puff at render time. Glassy water (roughness 0) → **no** distortion; a rough sea → the wake **wobbles and breaks up**. The wobble is a display-only offset — it never accumulates into the integrated position, so it can't drift the wake. |
| **dissipate** | Each puff has a **lifetime**. Over its life its opacity **fades** to 0 (monotonic — a puff never re-brightens) and its size **spreads/softens** (monotonic growth to `base · spreadFactor`). So a time/distance astern reads as faded + spread = dissolved. |

## How it's built

- **`Assets/_Project/Code/Boats/WakeParticleSystem.cs`** — the **pure, engine-light** simulation. A fixed
  array of foam-puff structs with all the feel-math as **side-effect-free static functions** (emission
  rate vs speed, stern apex, **V-arm placement (`ArmEmitPoint`) + bounded stern fill (`SternFillPoint`)**,
  V-wing emit velocity, advection + decay, life fade/spread, sea-state-scaled wave distortion). No
  `MonoBehaviour`, no `System.Random` — a deterministic integer **hash** drives the per-puff stream choice,
  along-arm distance and jitter, and the wave noise. This is what the EditMode tests hammer headless.
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
  per-speed, speed threshold, stern offset, V half-angle, **arm length, stern-fill fraction, stern-fill
  width**, wash-speed scale, velocity decay, lifetime, start alpha, fade power, spread factor, foam size,
  lifetime/size jitter, and the wave-distort amount/frequency/speed — plus pool size, max boats, foam colour
  and sorting on the emitter.
- **Rule 7 (perf):** a **fixed pool per boat**, dead puffs recycled in place → **zero per-frame
  allocation**; one shared sprite/material (batched); the sim sampled once per throttled tick. Mobile-
  portable. One active player boat dominates cost.

## Tunables & defaults (`WakeConfig.Default`)

| Knob | Default | Meaning |
|---|---|---|
| `ShedPerSpeed` | 10 | Foam puffs/sec per (m/s) of speed over threshold (denser = a sharper-reading arm) |
| `SpeedThreshold` | 0.4 | m/s below which no wake forms |
| `SternOffset` | 0.5 | m astern of the boat origin the V apex sits |
| `VHalfAngleDeg` | 19 | Kelvin-V half-angle of the diverging arms (smaller = a narrower, sharper V) |
| `ArmLength` | 3.0 | m, how far astern each crisp V arm reaches before it hands off to dissipation |
| `SternFillFraction` | 0.3 | fraction of puffs that fill the turbulent churn between the arms (0 = clean arms only) |
| `SternFillWidth` | 0.7 | how wide the fill spreads, as a fraction of the V width at each distance (≤1 = inside the arms) |
| `WashSpeedScale` | 0.2 | Boat-speed → initial along-arm flow speed (kept low so velocity doesn't blow the crisp arm apart) |
| `VelocityDecay` | 0.5 | Per-second retention of a puff's own momentum (<1 = fades to current-only drift) |
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

## The graded plume & bow spray (size + weight + speed)

On top of the particle wake, each boat carries two **authored, graded** sprites driven by a blend of hull
size (`BoatHullDef.LengthMeters`) + weight (`MassKg`) + live speed (`WakeGrading` — tunable reference
ranges, weights and tier thresholds; four authored tiers Small/Medium/Large/Huge each, loaded as
**Texture2D refs** through `Resources/WakeSpriteLibrary` and built into full-image sprites in code, because
the PNGs import `spriteMode: Multiple`):

- **The stern PLUME** (`Art/VFX/Wake/*`) — art is authored **narrow-apex-at-the-TOP, widening downward**
  (pixel-verified by `WakeArtOrientationTests`). The apex is anchored at the boat's **actual stern**
  (`WakeGrading.SternAnchor`: half the hull length back from the boat origin + a tunable nudge — anchoring
  at the origin was the "wake reads backwards" playtest bug: the plume hid under the hull with only its
  wide faint tail showing). Local +Y points at the bow so the plume widens + fades astern; `PlumeFlip`
  (serialized) turns it 180° and mirrors the pivot if the art is ever re-authored the other way up.
- **The BOW SPRAY** (`Art/VFX/BowSpray/*`) — art is authored **impact-churn-at-the-BOTTOM, fan spreading
  up** (same pixel test). Pinned at the cutwater (`BowSprayGrading.BowAnchor`, the stern anchor's mirror),
  fan ahead of the bow, with its **own speed-forward config** (`BowSprayGradeConfig`): weights ≈
  0.20 size / 0.15 weight / **0.65 speed**, and a speed onset (1.7 m/s) just under the dory's real rowed top
  speed — **2.0 m/s, MEASURED** on real physics (`PilotableFleetPlayTests`) — ramping to full only far
  **beyond** it, so the dory (the slowest boat in the game per the owner, and now actually the slowest)
  shows at most a gradual subtle wisp at a flat-out row, and the prominent sheet belongs to the faster hulls.
  **Do not re-derive that top speed from the stats.** This line used to read "≈2.5 m/s from
  `OarPower/ForwardDrag`", and that ratio is wrong twice over: **both oars pull** (`BoatController.OarThrust`
  sums them — a flat-out row is 600 N, not 300), and the rigidbody's own `linearDamping` — ~40–50% of the
  dory's resistance — appears in no stat on the asset. She really did **2.95 m/s**, so she had been crossing
  well into a spray she was never meant to throw. Slowing her (`ForwardDrag` 120 → 215) is what fixed that;
  the onset stays at 1.7 deliberately, because it gates **every** hull and lowering it would retune the
  skiffs' spray too. `SprayFlip`/`SprayPivotY` mirror the plume's escape hatch. Both
  sprites are one renderer per boat, built once, tier-swapped + continuously scaled + onset-faded,
  hidden at rest/aground — zero per-tick allocation, visual-only.

## Tests
`Assets/Tests/EditMode/WakeParticleSystemTests.cs` — pure-logic guards for all four brief points
(emission gating vs speed / aground; **the crisp-V geometry: apex at the stern, arms widening at the Kelvin
half-angle, mirrored wings, and the stern fill staying inside the arms**; fresh puffs at-or-astern of the
apex; advection by velocity+current and momentum decay toward current-only drift; monotonic fade-to-0 and
monotonic spread; lifetime death; sea-state-scaled + bounded + deterministic wave distortion) plus whole-run
determinism (same inputs → bit-stable particle field, no RNG).
`WakeGradingTests` / `BowSprayGradingTests` — the graded selection math (monotone in every input, bounded,
defensively-sorted thresholds, and the dory's spray pinned gentle against its real hull data).
`WakeArtOrientationTests` — the **pixel-verified orientation contract**: reads the raw wake/spray PNGs and
asserts the narrow apex is at the image top (wake) and the impact churn at the image bottom (spray), so a
flipped art re-export fails the suite instead of shipping a backwards wake again. PlayMode
`WakePlumePlayTests` proves both texture sets load through the spriteMode-Multiple trap at runtime.

## The deposited trail + the dynamic bow wave (owner ask 2026-07-23)

> The owner, verbatim: *"the boats wakes are currently static lines, they should be dynamic small waves
> or at least a representation that leaves a trail behind the boat, same with bow waves when they crash
> against the bow."*

**What was wrong.** Everything above emitted along a Kelvin-V *template hung off the boat's current
pose* — fresh (brightest) puffs and crest streaks kept appearing at fixed offsets relative to the hull
(up to `ArmLength` astern of wherever she is NOW), and the graded plume/spray sprites were rigid decals
glued to the stern/bow. Turn hard and the whole pattern swings with you: the "static lines" read.

**The fix — deposition (`WakeTrailMath`, pure + EditMode-tested).** The wake is now laid **at the
stern's swept track, per metre of travel** (distance-based carry, so trail density is uniform along the
track at any speed): each deposit is two **shoulder wavelets** (into the crest-line pool) plus, for a
tunable fraction, a **centre churn puff** (into the foam pool). Deposits persist and decay **where they
were laid** — the trail traces the boat's actual path and **curves through turns**. The Kelvin V is not
drawn; it **emerges**: shoulder deposits spread laterally at `boatSpeed·tan(KelvinHalfAngleDeg)`, which
is exactly the stationary V pattern behind a straight run and a curved, still-opening trail behind a
turn. Birth strength/length are **baked at emit** (`Particle.BirthStrength`), so a trail laid at speed
keeps reading after the boat slows or stops instead of dimming with the live speed. Pool safety is
hard: `DepositCount` clamps per tick (`MaxDepositsPerTick`), `EmitAt` recycles the fixed pool, and a
teleport-sized stern jump **resets** rather than striping foam across the map. The legacy boat-locked
stamp survives behind `WakeTrailConfig.Enabled = false` as the A/B escape hatch.

**The live plume + bow wave.** The boat-attached plume/spray sprites are *allowed* to be attached (the
transom wash and the cutwater sheet really do travel with the hull) but must be **alive**: both wear a
bounded, deterministic **churn pulse** (`ChurnPulse` — two incommensurate sine bands, per-boat phase;
amount 0 restores the decal), and the rigid straight-V plume **fades with turn rate**
(`TurnFade01`) — it cannot bend, so a hard turn hands the wake read to the deposited trail, which can.
The bow additionally sheds pooled **droplets** off the cutwater (`DropletVelocity`: a forward fan at a
fraction of boat speed, which she then drives past — the crash read), rate-gated by the **same
dory-gentle speed-onset ramp** as the spray sheet (`BowSprayGrading.SpeedOnset`), hard-clamped per tick,
sub-second lifetime, its own small pool (`_dropletPoolPerBoat`, default 24).

**The displaced sea (ADR 0023).** While the displaced surface is active, **every** wake element — laid
foam, shoulder wavelets, droplets, plume, spray — lifts by
`ShoreFadeMath.DisplacedHeight(height, depth, band, exaggeration)` of the shared swell **under its own
world position** (the emitter owns one `WaveFieldAnimator` at settings parity, the `BuoyWaveVisual`
pattern; depth via `BoatCrossing.DepthAt`), with the exaggeration + shore band read **live from the
Core `DisplacedSea` seam** each tick — never a config copy (the overlay-pose lesson: this very wake
once breathed a 1.6 m gap off a cached scale). Foam laid on a swell **heaves with the wave passing
under it**; the ride is display-only (never accumulated into the integrated position). Displaced OFF ⇒
the inactive context returns 0 everywhere and the wake sits on the flat plane exactly as before (the
A/B contract). The resting draft is deliberately **not** applied: foam rides the water surface, and the
draft exists to sink the *hull* to its waterline — the surface (and everything on it) doesn't move.

**The rendered read (owner playtest 2026-07-23, same day).** The first deposition build read as *"small
horizontal lines … it should bubble close to the boat, be foamy close to the boat, and then the wake
should be a long wake pattern."* The deposition was right; the **render** of it was wrong, three ways at
once, and each fix is pure math the tests pin headless:

1. **Orientation (the "horizontal dashes").** Shoulder streaks were rotated along their **live
   velocity** — for a deposit that is mostly-lateral spread + astern drift, decaying into the current,
   which painted near-perpendicular dashes on most headings. Now the orientation is **baked at emit**
   (`Particle.OrientDeg`) along the emergent arm's **analytic locus**: deposits fall astern at `speed`
   and spread at `speed·tanθ`, so one shoulder's deposits lie on a line **exactly θ off dead-astern** —
   `WakeTrailMath.ArmDir = normalize(−track + lateral·tanθ)`. World-locked for the streak's whole life;
   overlapping streaks fuse into one long coherent arm.
2. **Continuity (the "dotted rows").** Streak length (≤1.1 m, shrinking with age) vs 0.55 m spacing left
   gaps. Now the **overlap law** (`WakeTrailMath.ArmStreakLength = spacing/cosθ · overlapFactor`, factor
   clamped ≥ 1) guarantees a streak at least spans the along-arm gap to its neighbour — **continuous by
   construction at any tuning** — and trail streaks keep full length for life (the alpha fade dissolves
   the arm; shrinking re-opened the gaps).
3. **The near-stern churn band ("bubble/foamy close to the boat").** Each deposit now also lays
   `ChurnPuffsPerDeposit` (default 2, hard-clamped ≤ 4) **big overlapping foam puffs** (`ChurnSizeScale`
   1.9×) jittered across a hull-fraction strip (`ChurnHalfWidthFraction` 0.10) behind the transom,
   deliberately **short-lived** (`ChurnLifetimeScale` 0.4× → ~0.9 s): they die before the boat gets far,
   so the dense white band **clings to the transom and fades with distance** — its astern reach is
   `speed · churnLifetime`, speed-proportional for free. All laid foam **bubbles while young**:
   `AgedPulse` boils size + alpha at the full amount at birth and is *exactly* calm by end of life
   (render-only, bounded, deterministic), so the near band churns and the far trail lies quiet. The
   centre-lane fraction rises to 0.85 for a continuous fading middle lane. The code-built foam/crest
   sprites are alpha-**banded + 2×2 Bayer-dithered** (the KTC pixel-foam law — no airbrush falloff).

The read is therefore three zones: **bubbling white churn at the transom → a fading centre lane → two
long continuous arms** peeling out at the Kelvin angle, all still laid in world space (curving through
turns, persisting astern, graded by hull size + weight + speed). The per-tick emission budget is
explicit — `WakeTrailMath.MaxParticlesPerTick = MaxDepositsPerTick · (2 streaks + 1 centre + churn)` =
30 with defaults, well inside the 96-foam + 48-line pools — and `WakeTrailConfig.Enabled = false` still
restores the legacy boat-locked stamp exactly (the pulse and the trail length law are gated on it).

**Tunables** — `WakeTrailConfig` (deposition spacing/astern nudge/per-tick cap/teleport reset, Kelvin
half-angle + spread clamps + astern drift, shoulder half-width fraction + magnitude boost, graded
lifetime/size scales, centre-churn fraction, **arm overlap factor, churn puffs-per-deposit /
lifetime-scale / size-scale / band half-width fraction, foam pulse Hz/amount**, plume pulse
Hz/scale/alpha amounts, plume turn-fade onset/range) and `BowWaveConfig` (droplet
rate/cap/fan/speed-scale/lifetime/size/decay, spray pulse Hz/scale/alpha), both serialized on
`BoatWakeEmitter` beside the existing configs. Tests:
`Assets/Tests/EditMode/WakeTrailMathTests.cs` (spacing/carry conservation, the hard pool clamps at both
the math and the system level, track/shoulder geometry, the emergent-V spread law, grading monotonicity,
turn fade, pulse bounds/determinism, droplet fan/rate gating, **the arm-locus orientation, the overlap
law, the churn-band density/lifetime/clamps, the explicit per-tick budget, the aged bubbling pulse, and
the baked-orientation emit contract**).

## Future work (not built)
The procedurally-generated round foam puff is a **greybox** placeholder. The authored V sprite
`Assets/_Project/Art/VFX/BoatWake.png` (a 64×96 point-filtered white Kelvin triangle, apex at the
bottom-centre) is **kept ready** for an art pass: it can be dropped onto a single stern-anchored renderer
(oriented down the boat's heading, scaled + faded by speed, advected with the current) to draw razor-sharp
leading edges, with the foam particles supplying the churn — a clean hybrid on top of today's pure-particle
V. (It is `spriteMode: Multiple`, so a runtime load needs `LoadAllAssetsAtPath`/`LoadSpriteAny`, not
`LoadAssetAtPath<Sprite>`.) A bow-wave / quarter-wave layer and rudder turbulence are natural additions on
the same particle system. Larger hulls up the ladder could scale `WakeConfig` by `BoatHullDef` (a wider,
heavier, longer-armed wake for the dragger) — data-driven, no new mechanic.
