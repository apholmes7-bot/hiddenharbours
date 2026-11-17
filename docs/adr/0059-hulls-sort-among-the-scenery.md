# ADR 0059: Hulls sort among the scenery by where they lie

- **Status: Proposed** (2026-11-16). The owner accepts or rejects it in its PR (decision 1 below). The
  index row in `README.md` is added when it is accepted, so that this draft does not collide with the
  open renumbering of 0058.
- **Decision owner:** lead-architect (the rule and the Core constants); gameplay-systems (the boat's
  sorting group); art-pipeline (the re-anchored effects and the plant species data).
- **Serves:** P1 (a boat run up on the foreshore lies *on* it), P3 (boats, people and scenery
  interleave by position), rule 7.
- **Related:** ADR 0032 (the Y-sort band; "a fixed sorting order on an upright world object is a bug by
  default"), ADR 0022/0023 (mesh hulls; water and hull share one depth buffer), ADR 0056 (the deck-occupant
  split). The analysis, the map of every order involved and the rejected options are in
  `docs/architecture/hull-vs-decor-sorting.md`.

## Context

Every hull picture sorts at a fixed order 1 (`BoatVisualDef.SortingOrder`). Every Y-sorted renderer,
including rocks, grass, plant and grass-field chunks, cliffs and figures, sorts at 2 or above. With one
sorting layer, any decor that overlaps a hull's picture draws over her, wherever it stands. The owner saw
it at St Peters: "it seem that sprites appear above the boat when they should be below/behind". PR
#1281 fixes the plant field's share of it with a special band under hulls. Everything else in the band
still draws over a hull.

The beached wreck and the mesh vehicles each closed the same bug for themselves by Y-sorting in the band.
Hulls did not, because several things are parked at fixed orders just above 1 (deck containers, the
telltale, the interior room, spray, splashes, buoys, the rod line). They would sink under a Y-sorted
hull, and the player aboard would sort under the deck he stands on.

## Decision

1. **A boat is an upright world object.** Its root carries a `SortingGroup` whose order is the decor
   band's Y-sort at her waterline pivot, one rung behind: the vehicle rule, held once in Core
   (`HullSorting.OrderAt`, `SortingBands.HullYieldRungs`). Sprite and mesh hulls, the ambient fleet, NPC
   boats, moored boats and the beached wreck all use it.
2. **Everything aboard rides with the boat.** Renderers under the boat root keep their existing local
   orders inside the group, and that includes the player while he is parented aboard. Wheelhouse
   occlusion stays with the facet-id split and the waterline stays with the shared depth buffer. Neither
   is an ordering question. Renderers that must clear all decor (mooring rope, anchor rode) escape
   the group.
3. **No fixed order in the band's low floor.** Free effects (spray, splashes, bursts, buoys, emergent
   shore-plant sprites, the rod line and catch) Y-sort at their emission point, or derive from their
   carrier. A source-scan test forbids new literals between `DecorFloor` and `DecorFloor + 100`.
4. **One writer per boat order.** Situational rules, such as the St Peters pier, are inputs to the boat's
   sorting rule, not second writers to its group.
5. **Flat things lie under every hull by data.** #1281's `UnderHulls` band and `LiesFlat` rule stay.
   Its `BehindAHull` test is retired once hulls Y-sort. Whether a covered plant draws under the sea or on
   its surface is a per-species choice on `PlantSpeciesDef` (`WhenCovered`, default under). The surface
   case sorts at a new `SortingBands.FloatingWeed = -3`, the drifting seaweed's existing order.

## Consequences

- Rocks, tufts, figures and other hulls interleave with boats by position: beached, at a slip, alongside
  and at sea. Two hulls no longer tie.
- One sort point per hull is a compromise. On a long hull lying up the screen, standing decor inside her
  footprint south of the pivot still draws over her stern (the owner's decision 3 can refine it for a
  grounded hull).
- The cost is one integer per boat per frame, written on change, and one extra `SortingGroup` per boat.
  A hull inside the decor run can split decor batching by one or two batches. The static decor keeps
  sort-once-then-disable.
- Rollout is six small PRs, in the order given in the architecture document. The parked effects are
  re-anchored *before* hulls move, so nothing vanishes in between. No save change.

## Rejected

- **A per-hull footprint rule that pushes decor under**, on the CPU (a registry of 35,000 static sprites
  and chunk rebuilds as hulls move) or on the GPU (a hull mask read by every decor shader, which cannot
  be asserted headless). Kept only where #1281 already applies it to lying growth.
- **Moving tide-line decor under hulls wholesale.** It is right for flat things and wrong for a standing
  boulder. Kept as the flat-decor flag.
- **Real sorting layers** (ADR 0032's deferred item). A hull must interleave with decor, and a layer
  boundary cannot express that.

## Decisions for the owner

1. Accept this ADR: hulls sort among the scenery by where they lie.
2. Covered weed under the sea plane (recommended) or over it. And which species, if any, float at the
   surface.
3. The hull's sort point: waterline pivot (recommended) or, when aground, her nearest waterline point.
