# ADR 0015 — Water palette guard-rail: a tunable final-stage soft colour grade that bounds the sea to an art-directed palette (day/night-aware)

- **Status:** **Accepted** — lead-architect + art-pipeline. Ships the **final colour-grade stage** on the
  water shader plus a **palette-preset set** integrated with the §12 / ADR 0010 water preset library. The
  grade is **visual-only** (`col.rgb`), opt-in via a master strength, and defaulted to a SOFT North-Atlantic
  guard-rail on the live `Water.mat`.
- **Date:** 2026-06-28
- **Decision owner:** lead-architect (the URP shader/rendering plumbing is the cross-cutting/architectural
  seam — `agents/coordination.md` §1.1 "Water/fog/lighting"; CLAUDE.md rule 4). art-pipeline owns the
  *palette look* (the anchor colours + per-mood bounds); the two tune together.
- **Flagged from:** the owner asking for a **guard-rail** on the water's final output so the increasingly
  rich, sea-state-driven look (depth tint / fbm / rolling swell / caustics / specular / **reflection** / foam
  — ADR 0010 §5.6–§11) can never **wash out** (too bright) or go **muddy** (too dark), while keeping the
  dynamic diversity. The owner chose **SOFT guard-rails** — bound the extremes and gently PULL toward the
  palette, **NOT a hard lock** — and asked to ship it as **palette presets** (North Atlantic / Stirred Brown /
  Deep Blue / Tropical) layered onto the just-merged preset library (PR #129).
- **Related:** `0010-water-rendering.md` (the layered shader this grade is the LAST stage of; §12 the preset
  library), `0012-shoreline-rendering.md`, `0013-dynamic-lighting.md` (the full-screen MULTIPLY day/night
  overlay this grade pre-compensates for), `0009-tidal-exposure-…` (the height-map / depth truth the grade
  must NOT touch), `0006-boat-art-pipeline.md` (one implied sun), `0005-pc-first-target.md` (the 60fps
  budget). Design recipe + tunables: `design/water-rendering.md` §13.

## Context

The water shader composites a colour `col.rgb` from many independent, sim-driven layers (ADR 0010
addenda). Each layer is individually bounded, but their **sum** is not: a bright reflection on a glassy
noon plus strong specular plus a pale fbm patch can push the sea toward a washed-out white; a storm-grey
deep tint under heavy weather can sink it toward mud. Nothing keeps the **final** colour inside an
art-directed band. The owner wants a **single, tunable rail** at the end of the frag that:

1. bounds the **value (luminance)** between a floor (no mud) and a ceiling (no blowout);
2. **caps saturation** so no layer can scream past the palette;
3. **gently pulls** the colour toward the palette's anchor colours (deep / mid / shallow / foam) — a soft
   nudge, **not** a hard remap that would flatten the diversity;
4. is **opt-in + revertible** (a master strength where 0 = exactly today's look);
5. ships as **palette presets** so a Water material variant carries its whole palette.

Two hard constraints shape the design:

- **P1 / determinism (rule 5).** The grade is a pure `col.rgb` operation. It must **never** touch
  `depth` / `clip()` / `_WaterLevel` / the height read / the sim — the visible waterline stays the physical
  waterline. It saves nothing and feeds no simulation, exactly like every §5.6–§11 cosmetic layer.

- **The day/night interaction (the subtle part).** The day/night system (ADR 0013) draws a **full-screen
  MULTIPLY overlay ABOVE the water** (`Blend DstColor Zero`, sortingOrder 32760, Overlay queue) that
  multiplies the WHOLE composited frame by the global `_DayNightTint`. So whatever the water shader emits is
  **multiplied downstream** by the day/night tint's luminance. A naive constant value floor in the water
  shader would therefore be **darkened away** by the overlay — and worse, would force a choice between
  *daylight muddy* (floor too low) and *killing the owner's "genuinely dark nights"* (floor too high). The
  grade must keep **daylight/overcast out of the mud while letting TRUE NIGHT still go genuinely dark.**

## Decision

**(1) Add a final palette-grade stage to the water frag (`col.rgb` only), right before `return col`.**
After every layer composites, the frag applies a soft guard-rail in order — value clamp → saturation cap →
anchor pull — the whole thing lerped back toward the raw colour by a master strength. It reads the global
`_DayNightTint` (already published by ADR 0013) but writes nothing global and touches **no** depth/sim term.

**(2) Value FLOOR + CEILING, with a DAY/NIGHT-AWARE, PRE-COMPENSATED floor.** The grade clamps the colour's
luminance into `[floor, ceil]` by a hue-preserving multiplicative re-scale (not a desaturating lerp to grey).
The **ceiling** is a plain luminance cap (no blowout). The **floor is day/night-aware**: because the overlay
multiplies the frame by `dayNightLuma` downstream, the water floors its PRE-overlay luminance at

```
floorPre = min(1, paletteFloor / max(dayNightLuma, eps))
```

so that **after** the overlay's `× dayNightLuma`:

- **Daylight / overcast** (`dayNightLuma ≈ 1`): `floorPre ≈ paletteFloor`, so the ON-SCREEN water lands at
  ~`paletteFloor` — **never muddy**.
- **True night** (`dayNightLuma` small): the quotient **saturates at 1** (water full-bright PRE-overlay),
  and the overlay still multiplies it down to **genuine dark** — the owner's dark-nights vision is preserved.

A tunable **`_PaletteNightFloor`** (an on-screen luminance, default **0**) lets the owner optionally keep a
faint readable sea at night: the effective pre-overlay floor is also held at
`min(1, nightFloor / max(dayNightLuma, eps))`, which is inert in daylight (`nightFloor ≤ paletteFloor`) and
only lifts the deep-night floor a touch. **`_PaletteNightFloor = 0` lets night go as dark as the overlay
takes it** (the default). This is the cleanest mechanism that achieves "never muddy in daylight, still dark
at true night" given the verified overlay-multiplies-after pipeline.

> **Why pre-compensation (not a post-overlay floor or a separate night shader).** The water cannot see the
> framebuffer after the overlay multiplies it (the overlay is a *later* draw), so it must compensate *before*.
> Dividing the floor by `dayNightLuma` is the exact inverse of the downstream multiply, capped at full bright
> so night is never un-darkened. No second pass, no reading back the framebuffer, no new global.

**(3) Saturation CAP.** HSV-style saturation `(max−min)/max` capped at `_PaletteSatCap`: above the cap the
colour is pulled toward its own grey (its luminance) just enough to hit the cap; luminance is preserved (the
cap only desaturates, never darkens). `_PaletteSatCap = 1` is a no-op.

**(4) Anchor PULL (soft — a rail, not a cage).** The colour is gently `lerp`ed toward the **nearest palette
anchor by luminance** (a piecewise-linear blend across four anchors `_PaletteDeep` / `_PaletteMid` /
`_PaletteShallow` / `_PaletteFoam`, breakpoints = the anchors' own luminances, forced strictly increasing for
stable lerps) at `_PalettePullStrength` (default **0.35** — soft). This nudges every tone toward the family
without flattening the dynamic variance.

**(5) Master strength = opt-in, revertible.** `_PaletteGradeStrength` lerps the whole graded result back
toward the raw colour. **`_PaletteGradeStrength = 0` is an EXACT passthrough — today's look, byte-for-byte.**
The live `Water.mat` ships it at a SOFT **0.35** so St Peters gets the North-Atlantic guard-rail by default.

**(6) Palette presets.** Each palette = its four anchor colours + its bounds (floor / ceil / sat-cap /
pull-strength / night-floor), stored as **material properties** so a Water material variant carries its
palette. The live `Water.mat` is set to **North Atlantic** at the soft default. Three NEW palette variants
join the §12 library (`Assets/_Project/Art/Materials/WaterPresets/`): **Water_StirredBrown** (turbid
brown-green, low sat, mid value — stirred/estuary), **Water_DeepBlue** (saturated deep open-ocean blue,
higher contrast), **Water_Tropical** (turquoise/cyan, brighter higher-sat shallows — the deliberate
warm/bright outlier; everything else is cold North-Atlantic-canon). The existing 5 mood variants
(NorthAtlantic / GlassyCalm / StormGrey / FoggySmother / WarmShelter) gain the **same** palette property key
set with per-mood-appropriate bounds, so **every variant stays a complete material with one property key
set** (the magenta guard force-compiles them all). They appear in `WaterPresetMenu.cs` (the menu hardcodes
the variant list, so the three new ones were added to both the Apply submenu and the Generate list).

**(7) A pure C# twin + headless tests (the determinism-guard pattern).** The grade math — the day/night
floor pre-compensation, the luminance re-scale, the saturation cap, the anchor pull — lives in a pure
`Assets/_Project/Code/Art/WaterPaletteGrade.cs` (`HiddenHarbours.Art`), mirrored exactly by the shader, and
unit-tested headless (`Assets/Tests/EditMode/Art/WaterPaletteGradeTests.cs`): strength 0 = identity at every
input + day/night state; the floor lifts mud to the palette floor in daylight and the pre-comp lands on-screen
at the palette floor after the multiply; true night still reaches genuinely dark; the night floor keeps a
faint sea only when asked; the ceiling caps blowout; the saturation cap desaturates while preserving
luminance; the anchor pull is soft (moves toward, never snaps to, the anchor) and continuous in luminance.
The GPU result can't be tested headless, but the math that decides the bounds can — the same precedent as
`WaterReflection` / `DayNightMath`.

## Determinism & save (the invariant guarded)

The grade is a **pure function** of the composited `col.rgb` and the read-only global `_DayNightTint`. It
**never** reads or writes `depth` / `clip()` / `_WaterLevel` / the height map / any sim state, drives no
simulation, and enters no save (ADR 0008). Nothing about the water render — including this final stage —
becomes save state; the tide is still recomputed from `(worldSeed, gameTime)` (rule 5).

## Performance posture (rule 7 — 60fps desktop, mobile-portable)

A handful of ALU ops per pixel at the very end of the frag — one luminance dot, a clamp + multiply, an
HSV-style sat compute + lerp, and a four-anchor luminance-keyed lerp. **No extra texture taps, no extra
passes, no new C# uniform push** (the grade reads the already-published `_DayNightTint` global and the
per-material `_Palette*` properties). At `_PaletteGradeStrength = 0` the stage early-outs to a passthrough.

## Consequences

- **The sea cannot wash out or go muddy** — it stays in the art-directed palette band on every preset, in
  every weather, at every hour, while keeping the sea-state diversity (a soft rail, not a lock).
- **Daylight never goes muddy; true night still goes genuinely dark** — the day/night pre-compensation
  reconciles the floor with the ADR 0013 overlay multiply, the subtle part this ADR exists to get right.
- **Opt-in + revertible** — `_PaletteGradeStrength = 0` returns the exact pre-feature look; the live
  `Water.mat` ships a soft North-Atlantic rail by default.
- **A palette is a material property set** — switching palettes is a preset swap (the §12 menu), no shader
  edit. Every variant carries one property key set (magenta-guarded).
- **Zero new Core surface, zero new coupling** — no new interface/signal/save field; the shader reads an
  already-shipped global; `WaterSurface.cs` is untouched.

## Rejected alternatives

- **A HARD palette lock** (snap every pixel to the nearest palette colour / a fixed remap). Kills the
  dynamic diversity the owner values; he explicitly chose SOFT rails.
- **A constant (non-day/night-aware) value floor.** Either leaves daylight muddy or un-darkens the owner's
  dark nights once the ADR 0013 overlay multiplies the frame — the exact failure this ADR's pre-compensation
  avoids.
- **A post-process / second render pass to grade the frame.** Outside the rule-7 budget and the pixel-art
  in-shader discipline, and it would grade the *whole* frame (sprites/HUD), not just the water. The grade
  belongs in the water frag, on `col.rgb`, before return.
- **Grading in HSV/Lab fully.** More faithful but heavier; a luminance re-scale + HSV-style sat cap +
  RGB anchor lerp reads correctly for stylized pixel water at a fraction of the cost.
- **Saving the graded colour / driving it from sim state.** Violates rule 5 — the grade is a pure visual
  function of the composite + the day/night global.

## Open questions (later / for the art pass)

- **Per-region palette by mood/weather.** A region or weather state could pick a palette (e.g. the Smother
  → FoggySmother) automatically; today it is an explicit owner choice via the preset menu. A future
  Core-driven palette selection would be its own additive change.
- **Posterizing the graded output to the master palette ramp** (tie the anchor pull to the §4 discrete water
  ramp) — an art-pipeline call once the final palette is locked.
- **Exposing a few bounds in `GameConfig`** if the owner wants a global "how hard the rail bites" knob across
  all water — currently per-material (rule 6 satisfied on the material).

## Addendum — the LIGHT CONTENT now composites AFTER the grade (complete-dark fix)

The grade is no longer literally the last write to `col.rgb`: the boat-spotlight water beam (ADR 0016) and
the reflection's night-gated sky content (moon/glitter/stars) now add **after** `PaletteGrade()`,
pre-compensated for the ADR 0013 overlay multiply (divide by `max(_DayNightTint.rgb, 0.02)` — the same
pre-compensation idea this ADR's day/night floor established). Deliberate: at deep night the grade's floor
saturates (`floorPre = 1`) and was flattening lit-vs-unlit contrast, and its value ceiling would clamp the >1
compensated values the overlay needs (HDR). The rail still bounds the SEA those lights sit on; the exemption
is only the additive light content. Mechanism + tests: `design/water-rendering.md` §11.6, ADR 0016 follow-up
fix 3, `LightMath.CompensateForDayNightTint` / `LightMathTests`.
