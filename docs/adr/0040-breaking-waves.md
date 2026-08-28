# ADR 0040 — Waves that BREAK: lip, barrel, pocket and whitewater, computed from the wave field × the painted depth × the tide

- **Status:** **Proposed — awaiting owner sign-off.** This PR ships the ADR **and** the Core maths
  (`BreakerMath`), pinned headless; it draws nothing and pushes nothing yet. Merging it = the go-ahead
  for the two consumer PRs below, each gated on the owner's own eye.
- **Date:** 2026-08-27
- **Decision owner:** `lead-architect` (a new Core contract read by three lanes — `agents/coordination.md`
  §1.1, CLAUDE.md rule 4). `gameplay-systems` owns the maths and the forces; `art-pipeline` owns the look
  (what a lip is drawn as, how a barrel shades, the foam language).
- **Serves:** **P1 "The Sea Has Moods"** — the surf line walks in and out with the tide, so the sea's state
  and the hour of the day are readable from the water itself; and **P5 "Cozy but with Teeth"** — a bar that
  boils on the ebb is a warning you can learn to read, and later a force you can be caught by.
- **Flagged from:** the owner, 2026-08-27, on the arrival tour: *"our waves are missing something. i want
  them to be even more physics based"* — then a glossary as the quality bar. **Lip** (the top edge thrown
  forward), **Barrel/Tube** (the hollow cylinder of a full curl), **Pocket** (the powerful peeling zone
  beside the curl), **Whitewater** (the foam left after the break). Those four words are the acceptance
  criteria of this ADR, and §"What the owner asked for" maps each one to the mechanism that produces it.
- **Related:** `0018-shared-wave-field.md` (the field this reads — **extended, never reworked**),
  `0014-painted-seabed-height-authoring.md` (the depth it reads — one height map, three consumers),
  `0009-tidal-exposure-and-region-display-name-seams.md` (the tide that moves it; the walkability
  waterline is untouched), `0027-water-realness-pass.md` (`WaveFetch`, the spatial-envelope precedent
  this follows line for line), `0023-displaced-water-surface.md`, `0015-water-palette-guard-rail.md`
  (the water grade the drawn surf must live inside), `0010-water-rendering.md`,
  `design/water-rendering.md`.

---

## Context

The wave field (ADR 0018) is a good deep-water sea and stops there. A train crosses onto a two-metre bar
at exactly the amplitude, wavelength and speed it had a hundred metres out; it runs up a beach and
vanishes into the shore fade. Nothing in the shipped water knows the bottom is there.

That is the "something missing". Everything a breaking wave needs is **already owned and already
deterministic from `(worldSeed, gameTime)`**:

| input | where it already lives | what it gives breaking |
|---|---|---|
| the wave field | `WaveMath` / `SharedWaveField` (ADR 0018) | wavelength, amplitude, direction, celerity, phase |
| the painted seabed | `ITidalTerrain` / `PaintedHeightMap` (ADR 0014) | the bed elevation, and its **slope** |
| the tide | `IEnvironmentService.WaterLevelAt` (ADR 0009) | the water level, hour by hour |

`depth = waterLevel − seabed` is the same single number walkability and boat-cross already compare
against. Breaking is not new content and needs no new authored data: it is what those three **already
imply**, read out. There is no RNG and nothing is saved — like tide and wind, it is recomputed (rule 5).

**Why it is worth building at all, in one line:** it makes the bathymetry and the hour of the day
*visible*. A bar that boils at half-ebb and sleeps at high water is P1 you can see from the helm and P5
you can be caught by, and it costs no authoring — the owner already painted it.

## Decision

Add **`BreakerMath`** to `Core/Environment`, beside `WaveMath` and `WaveFetch`: a pure, deterministic,
allocation-free model of shoaling, breaking, breaker type and whitewater, plus a serialized
`BreakerSettings` on `GameConfig`. Ship it in three sequential PRs, each stopping at PR-open, the middle
one gated twice on the owner's eye.

### 1. Shoaling — the wave feels the bottom

A train's **period is conserved** as it crosses onto a shoal. Everything follows from that:

- **Wavelength** shortens: `L = L₀ · tanh((k₀d)^¾)^⅔` (Fenton & McKee 1990). The exact relation
  `ω² = g·k·tanh(kd)` has no closed form for `k`, and a Newton iteration is precisely what an HLSL twin
  must not carry. This explicit form is within ~1.7% everywhere, monotone, and **exact in both limits
  that matter** — `L → L₀` offshore, `c → √(g·d)` in the shallows.
- **Celerity** falls with it: `c = c₀·(L/L₀)`. `c₀` is taken from `WaveTrain.PhaseSpeed`, which already
  *is* the dispersion relation (the owner's canon ruling) — it is never re-derived here, so the relation
  cannot fork.
- **Height** grows: `Ks = √(cg₀/cg)`, energy flux conserved — **Green's law**. Exactly 1 in deep water, so
  **the open sea is untouched and the shipped field's tuning stays valid**; growing as `d^(−¼)` in the
  shallows, which is the swell that is knee-high offshore standing head-high on the bar.

`Ks` dips to ~0.913 in intermediate depth before Green's law takes over. That is textbook and deliberate,
and it is pinned as a test so nobody later "fixes" real physics away.

### 2. Breaking — and the tide that moves it

A wave breaks where its height reaches **γ·d**, γ ≈ 0.78 (the solitary-wave breaker index, tunable).
After it breaks its height is **held at γ·d**, which is why surf gets smaller as it runs up a beach and
why a big day and a small day look alike in the last few metres.

**The criterion moves with the tide for free**, because `d` is `waterLevel − seabed` and nothing else. No
animation, no schedule, no authored contour: the same bar under the same swell breaks on one tide and
not on the other.

The gate is a **smoothstep, not a cutoff**, and that is a physics decision rather than a polish one: a
hard `H ≥ γ·d` test would step the entire surf line on and off as the tide crossed a bar — a
discontinuity in the water the hull rides, arriving on the tide's schedule. Same reasoning that made
`WaveFetch`'s shore gate smooth.

### 3. Breaker TYPE — the bathymetry decides, nobody paints a barrel in

The **Iribarren number** (surf similarity) `ξ₀ = tanβ / √(H₀/L₀)`, with `tanβ` read as the painted bed's
slope **along the wave's travel**, classifies the break (Battjes 1974):

| ξ₀ | class | what the player sees | where |
|---|---|---|---|
| < 0.5 | **spilling** | the crest crumbles down its own face into whitewater | every sandy shoal |
| 0.5 – 3.3 | **plunging** | the **lip** thrown forward, the **barrel**, the **pocket** peeling beside it | a shingle bank, a reef edge |
| 3.3 – 5.0 | collapsing | the face gives way low down, short and foam-poor | a steep bank |
| > 5.0 | surging | it surges up and back with barely a break | a quay wall |

This is the load-bearing decision of the whole ADR: **barrels appear only where the seabed earns them.**
The owner painted that seabed; the model only reads it. It also means a place has a *season* rather than
a fixed type — the same bed reads plunging under a long swell and spilling under short chop, because ξ₀
carries the wavelength.

### 4. Whitewater — and the one way this could have gone wrong

Post-break energy advecting shoreward and decaying: `E = exp(−t/τ)`, with the age
`t = metersSinceBreak / √(g·d)` — distance past the break line over the bore speed.

**The age is derived from geometry and never accumulated.** This is the one place this lane could have
repeated the living-wake defect, twice over, and the ADR states the rule rather than trusting the author
to remember it:

- **#665:** a wake texel's age was derived from its coverage, after that coverage had been through
  `saturate` → `smoothstep` → 3-level posterize. The proxy could take three values, **72–81 % of the
  visible band drew at age exactly 0**, and the owner saw it in one playthrough — *"the big foam band
  stays white, never disperses."* Saturation destroys ordering; thresholding destroys range; quantization
  destroys resolution.
- **the round-2 repeat:** the replacement freshness mark was then *scaled by the hull's vigour*, so a
  dory's brand-new churn was **born half-aged**. A clock scaled by intensity conflates *how hard* with
  *how long ago*.

So, here: `MetersSinceBreak` marches upwave in `MarchSteps` (16) fixed steps and accumulates a **running
product of the break gate** — the `WaveFetch` land-shadow idiom, so once the march steps out of breaking
water nothing beyond it counts, and the shorebreak never inherits an outer bar's dead foam across a
lagoon. The result is **linear in position with no clamp, threshold or posterize before the exponential
consumes it**, and the exponential has no plateau. `Breaking01` is a **GATE** — 1 where the sea is
breaking, 0 elsewhere — and is **never** used as a scale on the age.

And the ADR does not ask to be believed about any of that. `BreakerWhitewaterAgeMeasurementTests`
**measures the shipped chain end to end** and holds the numbers, so a retune re-runs the measurement
instead of quietly measuring a world that no longer exists:

| measured 2026-08-27, default tuning | value |
|---|---|
| samples across the surf zone | 128 |
| **distinct ages** | **128** (not 16 — the smooth gate supplies the sub-step fraction) |
| energy range | 0.000 – 0.995 |
| share of the band at the most common energy | **2 %** (the #665 defect read 72–81 %) |
| with the gate narrowed toward a hard cutoff (sabotage arm) | distinct ages collapse to **29** |

The sabotage arm is the part that matters: it proves *what the smooth gate buys*, so a later
"simplification" to a hard test fails loudly instead of quietly flattening the foam.

### 5. What this does NOT touch

- **`WaveMath` and `WaveFieldAnimator` are unchanged.** Breaking is a **read layered over** the field,
  exactly as `WaveFetch` is: it consumes trains and never rewrites them. The living wake (#669) reads
  their published phase and their contracts stay frozen. No lead-architect sign-off is needed because
  nothing of theirs moves.
- **Any phase used past the break is the field's PUBLISHED phase**, read forward via
  `WaveMath.TrainPhaseDegrees` off a train taken from `SharedWaveField`. Nothing reconstructs a phase
  from a sampled surface: `atan2(height, slope·d/k)` is exact for one pure sine and is not a phase at all
  when fed the real four-train sharpened field — measured, it reverses on 1.7 % of frames. And a stateful
  smoother can only be relied on to agree with **itself**, so consumers read the published instance
  rather than ticking a lookalike.
- **The walkability waterline is untouched.** Surf rides *on* the tide level; `TidalExposure` never sees
  it, exactly as waves never move it (ADR 0018).
- **Glass calm stays sacred.** A zero-amplitude field breaks nowhere, on any seabed, at any tide.
- **No scene edits.** Breakers derive from shipped data. If a consumer PR believes it needs a scene
  change, that is a signal to stop and report, not to edit.

### 6. Where the tunables live (rule 6)

`GameConfig.Breakers` — γ, the gate band, the three Iribarren thresholds, the slope-probe span, the
whitewater decay and march step. They ship at **the textbook physics** (γ = 0.78, Battjes' 0.5 / 3.3 /
5.0), because these are constants of the sea rather than art direction: *where* and *what kind* is the
bathymetry's answer, and the owner's dials for how the surf **reads** belong in the consumer PRs.

⚠️ A `GameConfig` asset serialized before today deserializes these as zero, and **zero is inert, not
wrong**: γ = 0 breaks nothing anywhere. Same safe-stale property `WaveFetchSettings` ships under.

⚠️ The one dial to be careful with is `PlungingLimit`. Widening it puts barrels on shoals that have not
earned them, which is precisely the claim this ADR is making.

## What the owner asked for, and what produces it

| his word | the mechanism |
|---|---|
| **Lip** — the top edge thrown forward | only where ξ₀ says **plunging**; the throw scales with the depth-limited height `γ·d` and the phase past the break. PR 2. |
| **Barrel / Tube** — the hollow curl | the same plunging band; the hollow is the face between the thrown lip and the water it lands on. PR 2. |
| **Pocket** — the peeling zone beside the curl | high `Breaking01` **and** small `MetersSinceBreak` **and** plunging: young, violent water next to a face that has not broken yet. Falls out of the primitives; PR 2 reads it, it is not a fifth model. |
| **Whitewater** — the foam after the break | `WhitewaterEnergy01`, advecting shoreward and decaying on a real clock, feeding the existing foam language inside the ADR 0015 water grade. PR 2. |
| *"even more physics based"* | every number above is a textbook relation over data the game already owns; nothing is painted in, nothing is faked, nothing is saved. |

## Staging — three PRs, and the gates on each

1. **PR 1 (this one): the ADR + `BreakerMath`.** Core, pure, no pixels and no forces. Pinned by
   `BreakerMathTests` (determinism, shoaling, the tide sweep, the classification table, the guards) and
   `BreakerWhitewaterAgeMeasurementTests` (the measurement above). *Accepted when the maths is pinned and
   the ADR merges.*
2. **PR 2: the look.** Breaker bands along the moving contours. **Two drops, each with a screenshot
   check-in — his eye is the gate, not a test.** (a) spilling + whitewater, the common case on every
   gentle shoal; (b) plunging — the lip, the barrel's hollow shadow, the pocket peeling beside it, only
   where slope earns it. HLSL twin of anything per-pixel with **this side as the pinned reference**;
   parity by visual epsilon and ULP, never bit equality. World-pixelized coordinates; no `frac()` on a
   varying in a vertex shader; `multi_compile` on any runtime-made material. Sorting stays in the water
   layers **under** hulls. Glass calm and the dead-calm mirror survive untouched. Contour work on the
   slow tick or in the shader — no per-frame CPU sweeps of the height field, no per-frame allocations
   (rule 7). *Accepted when he nods at both check-ins.*
3. **PR 3: the teeth.** Whitewater shove and pocket broach torque **through the existing B3 seakeeping
   channel** — no second force path — scaled by `SeaState01 × exposure`, `GameConfig` toggle default ON,
   calm and sheltered water unchanged, gentle-to-medium never-capsize (the M1 law). Mind the ~20 s hull
   time constant: the sea pushes, it does not teleport. *Accepted when a hull held in whitewater
   measurably drifts shoreward, the same seed and time reproduce the drift, two hulls of different mass
   order correctly, and calm water is untouched.*

**Out of scope, deliberately:** surfing as a mechanic (the anatomy is the sea's, not a sport);
grounding / keel-over (its own vision); capsize; nervous water over fish (M2); buoy physics; and **any
retune of the #669 wake dials** — the owner's verdict on those is still pending and a mid-lane change
would stale his judgment.

## Consequences

**Good.** The painted seabed becomes legible from the helm, and the tide becomes legible in the water
rather than only in the HUD. It is authored content the owner already made, shown for the first time.
Nothing new is saved, nothing new is authored, and the open sea is byte-untouched offshore where `Ks = 1`.

**Costs.** `SampleAt` is three terrain samples; the whitewater march is sixteen more, which is why it is
a separate call a consumer opts into. Both are the `WaveFetch` cost shape, and both are subject to rule
7 at the consumer: slow tick or shader, never a per-frame CPU sweep of the height field.

**Risks, named.**
- *The march reach is a cap* — 32 m at the default tuning. A surf zone wider than that reads as uniformly
  old at its inshore end. The energy is below 1 % long before then, so it is not reachable in a visible
  quantity, but it is a cap and it is stated (and tested) rather than left silent.
- *Fenton & McKee is an approximation* (~1.7 %). Chosen over an exact iterative solve precisely so the
  HLSL twin can exist at all.
- *Plunging is the showpiece and the most abusable dial.* The physical thresholds ship unchanged for that
  reason.
- *A twin is a twin.* Two transcriptions of one formula cannot be made bit-identical; parity is held at a
  visual epsilon, and `MarchSteps` is the fixed `[unroll]` bound both sides move together.
