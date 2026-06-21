# ADR 0006 — Boat Art Pipeline: Pre-Rendered 3D → Sprite Sheets + Discrete Zoom Tiers

- **Status:** **Proposed — deferred to M2.** This records a *direction* and locks a few
  forward-compatible conventions now (cheap insurance). It is **not** a commitment to build anything
  in M0/M1. No bake happens until M2, gated by the go/no-go below.
- **Date:** 2026-06-21
- **Decision owner:** art-pipeline (proposed) — **pending `lead-architect` review** before Accepted.
- **Related:** `0004-perspective-and-scene-strategy.md` (¾ top-down, the source of the rotation
  problem), `design/art-and-audio-bible.md` §3.3 / §3.5 / §3.5.1 / §3.7, `vision-and-pillars.md`
  (P1 The Sea Has Moods, P2 Dory to Dynasty), backlog VS-19 (navigation), VS-26 (boat feel).

## Context

ADR 0004 locked a **¾ top-down** view. Boats, unlike characters, **turn continuously** through every
heading, so "how a boat faces its direction of travel" is an open production question the bible §3.5
explicitly left to decide per tier (pre-rendered heading frames vs a single sprite spun by the
engine). The answer drives the whole boat art pipeline — and because the fleet is the spine of P2
(*Dory to Dynasty*), getting it wrong is expensive to unwind once T2–T7 hulls are authored.

Three forces make a single top sprite **rotated live by the engine** the wrong default for this game:

1. **¾ is not plan view.** Hulls have a visible *front face* and elevated, often **asymmetric**
   upper works (wheelhouse offset to one side, masts, booms, A-frames, deck gear). Rigidly spinning
   that 2D image rotates the **box-tops and the implied sculpt-light with the hull** — the lit side
   and the "up" face point the wrong way at most headings. It reads as a paper cutout pirouetting,
   not a boat turning.
2. **Temporal stability is the look.** The Kingdoms-Two-Crowns / North-Atlantic style is **AA-free**.
   Continuously rotating crisp pixel art **crawls and shimmers** along every edge under motion and
   zoom; a *live-3D-with-pixel-filter* approach crawls for the same reason (the filter re-quantises
   different pixels each frame). **Baked** frames are temporally stable by construction.
3. **The variant explosion.** A boat is never one image: day vs **night/lit**, **wake/spray**,
   **row/idle**, **damage/laden**, crew states (bible §3.5). Hand-drawing each of those × each
   heading × each tier is unbounded.

## Decision

**Author boats as PRE-RENDERED 3D → sprite sheets**, and drive framing with **discrete,
pixel-perfect camera zoom tiers**.

1. **Pre-rendered 3D → sprites.** Model each hull once in 3D, light it with the project's fixed
   key-light (bible §3.5.1), and **bake** an orthographic ¾ render to a pixel-art sprite sheet
   (N headings × the needed state frames), through a locked post-recipe (palette-clamp, outline
   pass, explicit dither, **no AA** — see go/no-go). This is **not** hand-drawn per-direction art,
   and **not** a live-3D model rendered with a pixel-filter at runtime.
2. **Discrete pixel-perfect zoom tiers.** Framing steps through a small set of **data-driven,
   pixel-perfect zoom levels** bound to vessel size/context (a 4.5 m dory stays intimate; a 110 m
   tanker pulls back so it fits and its scale *reads*), each an integer/pixel-snapped step so nothing
   shimmers. This formalises bible §3.7 into named tiers rather than a free-floating ortho size.

## Why — weighted to *this* game

- **¾ + asymmetric upper hulls** ⇒ rigid sprite-spin breaks the form and the light (above). A relit
  3D bake per heading is correct *by construction*.
- **AA-free KTC look** ⇒ baked frames are **temporally stable** (no crawl/shimmer under motion or
  zoom); a runtime 3D-pixel-filter crawls.
- **Throughput, not bottleneck** ⇒ every variant axis (night/lit · wake · row · damage/laden ·
  per-heading) **collapses to a relight or a frame-range off one model**, instead of multiplying
  hand-drawn art. One model → a whole tier's states.

## Consequences

- **Zero architecture change.** Boats remain plain **`SpriteRenderer`s**; the baked PNGs/atlases
  **drop into the existing import lock** (VS-23: PPU 32 · Point · Compression None · mips off) exactly
  like today's hand-drawn hulls. Nothing in gameplay/Core needs to know the sprites came from a bake.
- **The current fleet is explicitly SLICE PLACEHOLDER.** All hand-drawn hulls — the T0 Dory, the
  T1 Punt, **and the imported-but-frozen T2+ hulls** (Cape Islander, Lobster Boat, Side Dragger,
  Stern Trawler, Coastal Packet, Tanker + roster icons) — are *spin-tolerant near-plan placeholders*
  good enough for the slice. **The M2 bake replaces them.** They are kept (stable GUIDs, usable now),
  not deleted; the swap is a sprite-ref change, not a rework (held true by bible §3.5.1's pinned
  pivots/footprints).
- **Pipeline/tooling at M2 (not now):** a repeatable bake+post recipe (modelling kit, render rig,
  palette-clamp/outline/dither post-pass) becomes an art-pipeline deliverable. Atlas/LFS budget
  (bible §3.3/§8) grows with heading count — choose N headings per tier deliberately.

## Rejected alternatives

- **Live-3D rendered with a runtime pixel-filter.** Crawls/shimmers under the AA-free style (re-
  quantises different pixels each frame); heavier at runtime. Baking front-loads the cost once.
- **Hand-drawn per-direction sheets.** Doesn't scale to N headings × state variants × 8 tiers;
  keeping the implied light and proportions consistent across all of that by hand is error-prone.
- **World-rotation ("keep the boat bow-up on screen, rotate the *world* around it").** **Rejected.**
  It throws away the distinction between **heading** (where the bow points) and **course-over-ground**
  (where you actually move) — the *set & drift / crabbing* read that is core to P1 navigation and the
  steering skill fantasy (VS-19). If the world spins to keep the bow up, the player can no longer see
  the boat crabbing across wind/current. We keep the **world fixed** and turn the boat within it.

## First M2 art task — the go/no-go

Before committing to bake the fleet, **bake exactly ONE boat** with the locked recipe — **palette-
clamp to the North-Atlantic master ramp (§4.1), post-pass outline, explicit dither, no AA** — and
**stand it beside Ned's cottage** next to the hand-drawn placeholders. **Go** if the baked hull holds
up against the hand-drawn art in-scene (style, light, scale, temporal stability while turning/zooming);
from there the rest of the fleet is **throughput**, not risk. **No-go** if it reads as off-style — then
we revisit the recipe (or the decision) *before* spending on the full fleet.

## Open questions (resolve at M2)

- **N headings per tier:** how many baked headings before interpolation/snapping reads smoothly —
  likely fewer for huge hulls, more for hero boats (bible §3.5). Atlas/LFS cost scales with it.
- **Metric reconciliation:** the placeholder hulls are not all at canon length (e.g. the placeholder
  Cape Islander is ~9 m vs the §3.3 canon 13 m). The M2 bake must hit the **§3.3 metric footprint**;
  confirm the canon lengths in `boats-and-navigation.md` when sizing the models.
- **Zoom-tier table:** the concrete ortho sizes / pixel ratios per vessel-size band (data-driven) —
  prototype against the dory→tanker range.
