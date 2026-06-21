# ADR 0005 — Platform Target: PC-First (Windows/Desktop), Mobile as a Later Port

- **Status:** Accepted
- **Date:** 2026-06-20
- **Decision owner:** lead-architect (canon-level pivot approved by the owner)
- **Supersedes:** the **mobile-first** platform assumption recorded in `0001-engine-choice.md`,
  `vision-and-pillars.md` §5.6, and the "mobile-first" framing in `CLAUDE.md`, `roadmap.md`, and
  `design/ux-and-mobile-controls.md`. Those docs are updated in the same change.

## Context

The M0 greybox fun-check returned a clear **GO** — the core fishing→sell loop is fun. But playing
it surfaced a platform mismatch: Hidden Harbours is an **information-dense** game (the always-on
glanceable tide/wind/clock HUD and conditions cluster, the market chalkboard with moving prices,
and the Big-Ambitions-style management dashboards) whose fantasy is *reading the sea at a glance*
and *running a growing operation*. On a phone screen those reads were cramped and "hard to see,"
and the systems plainly **read as a PC game** — they want screen real estate, pointer precision,
and keyboard shortcuts.

Two things make the pivot cheap and safe to make now rather than later:

1. **The architecture already anticipated it.** ADR 0001 chose Unity in part because it is
   "architected for later desktop/console," and the UX/tech design has consumed input through an
   **intent abstraction** (VS-02 — `Move/SetThrottle/SetHeading/Interact/Haul/…`) from day one.
   Re-targeting the primary platform is a **bindings + layout** change, not a gameplay rewrite.
2. **We are early.** M0 is done; M1 (the vertical slice) is the right moment to set the primary
   target, before the slice's art, HUD, and external playtest are built against the wrong shape.

This is explicitly **not** a design change. The five pillars, the boat ladder, the regions, the
fish, the market, the staff/automation layer, the data schemas, and the deterministic simulation
are all unchanged.

## Decision

**Target PC-first: Windows/desktop is the primary platform — landscape orientation, keyboard/mouse
+ gamepad as the primary input.** **Mobile (iOS/Android) is kept as a viable later port** and is
*not designed out*; console follows after, as before.

## Why — weighted to *this* game

1. **Legibility is a pillar.** P1 ("The Sea Has Moods") lives or dies on the tide/wind/clock being
   glanceable. A desktop screen, hover tooltips, and a wider top read-only band make that read
   *better*, not just bigger.
2. **The management depth wants a desktop.** The summary-first card dashboards (properties, staff,
   routes, contracts) reflow to multi-column and are far more pleasant with a pointer and keys —
   the design doc already specified this reflow (`ux-and-mobile-controls.md` §9).
3. **Sailing precision.** "Throttle + heading" steering against wind/current reads cleanly to
   mouse-point + keys / gamepad sticks; the skill fantasy is easier to land first on PC.
4. **It costs us little.** Because input is intents and the UI is responsive cards, mobile remains
   a reachable port rather than a fork.

## Consequences

- **Input (no gameplay change):** KB/mouse/gamepad become the **primary** bindings, emitted through
  the **same intent architecture (VS-02)**. Gameplay code still reads only intents; **only the
  device→intent bindings change.** Touch bindings are **retained** as the mobile-port path. The
  intent set itself does not change.
- **Orientation:** **portrait → landscape** as the primary layout. The "portrait-primary" stance in
  `ux-and-mobile-controls.md` §2 is demoted to *future-mobile-port* scope (kept, not deleted).
- **Camera/HUD:** retune to landscape/desktop — spread the top read-only band, exploit the wider
  sightlines (helpful for open water and big ships), add mouse-hover tooltips; the card screens use
  their multi-column reflow (`ux-and-mobile-controls.md` §9). Thumb-zone placement becomes a
  mobile-port concern.
- **Performance budget → desktop baseline:** target a comfortable **60fps on a typical
  desktop/laptop GPU**. **Mobile-portability stays a standing constraint**, not an afterthought:
  keep object pooling and draw-call discipline, throttle heavy sim to the slow tick, keep the HUD
  allocation-free, and mind texture memory — so we **do not paint mobile into a corner**. The
  cross-cutting "performance" track is re-baselined to desktop while keeping the mobile guardrails.
- **Distribution:** the M1 soft-launch shifts from **TestFlight / Play closed track** to a
  **Steam / itch.io closed playtest**. Acceptance bars that hard-coded "mid-range Android phone"
  become a **desktop baseline** (with a mobile-port note retained).
- **Validation:** the "one-thumb-comfortable" sailing check becomes **KB/mouse/gamepad comfort**
  (still validating the force model against the arcade/virtual-stick alternate); the touch/one-thumb
  validation moves to the future mobile-port pass.
- **Unchanged:** gameplay, simulation/determinism, economy, content, data schemas, the boat/fish/
  region/NPC canon, and the engine choice (Unity 6.3 LTS, 2D URP, C#).

## Mobile is kept viable (the standing constraint)

This pivot must **not** quietly delete the mobile option:

- Keep the **intent abstraction strict** — gameplay never reads raw input, so a touch binding set is
  always re-addable.
- Keep the **touch / one-thumb / portrait design** in `ux-and-mobile-controls.md` as the **port
  spec** (demoted in priority, not removed).
- Hold the **mobile perf guardrails** (pooling, draw calls, texture memory, slow-tick sim) even
  while the headline target is desktop.

## When we would revisit

If a mobile launch becomes the priority again, the port is a **bindings + layout** pass — re-promote
the touch/portrait design and re-baseline the perf target to a mid-range phone — **not** a rebuild.

## Sources

- M0 fun-check verdict (owner playtest): **GO**; the info-dense HUD/management reads as a PC game.
- `0001-engine-choice.md` (Unity chosen, "architected for later desktop/console").
- `design/ux-and-mobile-controls.md` §9 (input intents + responsive reflow that make this cheap).
