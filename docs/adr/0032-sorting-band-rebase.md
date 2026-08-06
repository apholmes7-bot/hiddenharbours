# ADR 0032 — The Y-sort band is re-based to fit the region; real sorting layers deferred

- **Status: ACCEPTED (2026-08-05)** — lead-architect decision. This slice re-bases the decor band and
  makes the sorting-order partition explicit in code. The structural fix (real Unity sorting layers)
  is **recorded here and deliberately deferred** — see *Deferred*.
- **Date:** 2026-08-05
- **Decision owner:** lead-architect (the ladder + the re-base); art-pipeline (`YSortSprite`);
  world-content (the NPC standees that gain a `YSortSprite`).
- **Serves:** **P3 (A Living Working Coast)** — you cannot read a place as inhabited if you walk
  *through* the grass and *behind* the people instead of among them. Also **rule 7**: the fix had to
  keep `YSortSprite`'s static self-disable, because the island now carries ~35,000 tufts.
- **Related:** `0028-terrain-splat-ground.md` (the painted ground the band sits over),
  `0013-daynight-lighting.md` (the tint overlay that caps the ladder), `0004-scenes-and-prefabs.md`
  (scene-per-region, which is what makes "region extent" a checkable number).

## Context

Hidden Harbours draws its ¾ view by sorting order alone. `HiddenHarbours.Art.YSortSprite` maps a
sprite's world Y to a sorting order —
`order = clamp(round(base − worldY · perUnit), min, max)` — so a tuft lower on the screen draws in
front of one higher up, and the player interleaves with the scenery by position rather than by
hand-tuned layer.

Its shipped defaults were `base 10, perUnit 4, min 2, max 40`. **That resolves world Y only from
−7.5 m to +2.0 m — a 9.5 m window.** St Peters is 520 m tall (`StPetersBuilder.RegionWorldSize`).
Past either end of the window every sprite saturates on the same order and ties, and ties are not
broken by position: the project has one sorting layer and `Renderer2D.asset` uses the default
transparency sort, so tied sprites at z = 0 fall back to draw order.

Measured on the ground-cover retune (#434): **34,422 of 35,026 grass tufts — 98% — sat on a
saturated order.** The player carries the same defaults, so anywhere but a strip around Y = 0 the
walker and the scenery around them shared one order. The village stands at Y +8…+33; the sage
cottage at −20. Essentially the whole inhabited island was outside the window.

It survived this long because **`StartSpawnPos` is `(5, 0, 0)`** — dead centre of the only stretch
that still worked.

Three findings shaped the fix:

1. **The ceiling's stated rationale was false.** `YSortSprite`'s own tooltip said `max 40` "keeps a
   far-'down' sprite from rising above the HUD". The HUD is a `ScreenSpaceOverlay` Canvas, which
   draws over all world geometry at any sorting order. The ceiling protected nothing. (Every value
   in 94…1000 found near it is a *Canvas* order — a different axis.) An earlier ruling in this same
   session had rejected band-widening partly on the strength of that rationale, and was reversed
   once it was actually tested. **A component's stated rationale is a claim, not a fact.**
2. **Widening does not mean re-deriving the ladder.** Every load-bearing fixed order is *below* the
   band's floor — painted seabed −21…−18, submerged shore plants −8, Sea −5, both wharf decks −4…1,
   interior floors 1. Hold the floor at 2 and none of them move. Only the ceiling had to travel.
3. **The real root cause is structural.** `TagManager.asset` defines exactly one sorting layer, so
   seabed, water, deck, decor, characters, boats and rain all share a single integer axis,
   partitioned by *comment convention* restated across about ten files. The 2…40 clamp was doing the
   job a sorting **layer** should do. That is why it rotted, and why it can rot again.

## Decision

**Re-base the decor band to fit the region, hold its floor, and make the partition explicit.**

- **`HiddenHarbours.Core.SortingBands`** is the one place the order axis is partitioned. It lives in
  Core because Boats (mooring rope) and Fishing (trap-haul rope) need it and neither references Art
  (rule 4). Every tier that was previously a comment is now a named constant.
- **The band spans ±300 m** (`DecorHalfExtentMetres`), giving `base 1202, perUnit 4, min 2,
  max 2402`. `perUnit` is unchanged at 4 — a depth step every 0.25 m *was* the right feel; reach was
  the bug, never resolution. The ceiling is ~7% of the `short` that `sortingOrder` is stored in.
- **The floor stays at 2.** Nothing below the band moves, is re-derived, or needs re-testing.
- **The three sprites that must clear all decor** (rain, mooring rope, trap-haul rope) move from a
  literal `50` — which cleared the old ceiling of 40 — to `SortingBands.AboveDecor`, preserving their
  behaviour exactly while making them follow the band by name.
- **NPC standees gain a `YSortSprite`.** They held a fixed order 9, which read correctly only while
  the player's own sort resolved; in the village the player saturated to 2 and drew *behind every
  islander, always* — including face to face. Static, because routines are M2 and nobody walks yet.
- **Five greybox props gain one too** — the island cottage, Ginny's freezer, the wet-bucket spot, the
  general-store counter, and Nine Mile Creek's buildings. Each sat at a fixed order 2–3, i.e. *on the
  band's floor*, which only read correctly while the meadow around them saturated there as well.
  Widening the band without this would have turned "grass sometimes draws over the cottage" (a tie,
  broken arbitrarily) into "grass always draws over the cottage". These predate
  `VillageBuildingCatalog`, which already gives the kit's real houses the same component.
- **`ChimneySmoke`'s order becomes relative.** Its plume sat at a fixed 5 to clear a cottage fixed at
  2. With the cottage now Y-sorting to ~1146, a fixed 5 would put the smoke *behind the roof it comes
  out of*, so `_sortingOrder` is now an offset above the nearest renderer up the parent chain — the
  idiom `CatchFillRenderer` and `SpriteShadow` already use. Re-checked on the plume's existing tick
  rather than resolved once, because the building's order is written by a `YSortSprite` in its own
  `OnEnable` and nothing orders that against `ChimneySmoke.Awake`.
- **`SortingBandsTests` re-derives the requirement from each builder's declared extent** and fails
  with the number to raise `DecorHalfExtentMetres` to. Grow a region past the band and it goes red
  *before* the layering quietly stops.
- **Tests stop hard-coding the band.** `YSortSpriteTests`, `YSortSpriteDispatchPlayTests` and
  `AcadianTreePlacementTests` passed `10f, 4f, 2, 40` in as literals, so they kept passing while the
  thing they guarded drifted. They now ask `SortingBands`. `YSortSpriteTests` also moves its
  interleave check from Y = 0 up to the village, since Y = 0 is exactly where the bug did not show.

### Rejected alternatives

- **Custom Axis transparency sort** (`TransparencySortMode.CustomAxis (0,1,0)`, collapse the band to
  a single order, `spriteSortPoint = Pivot`). Elegant, and it removes the band entirely — but it
  sorts on the GPU-facing render path, so **it cannot be asserted headless**: CI has no graphics
  device, and a silent regression here is exactly the failure mode that produced this ADR. Integer
  orders stay checkable in EditMode. Ruled in, then reversed the same evening.
- **Camera-relative Y** (sort by `worldY − cameraY` so the window follows the player). The standard
  2D answer, and wrong here: it forces all ~35,000 static tufts to re-sort whenever the camera moves,
  destroying the sort-once-then-self-disable promise that makes the ground cover affordable (rule 7),
  and it makes an authored order meaningless for static decor that self-disables in play.

## Deferred — real sorting layers

The proper fix for the *class* of bug is to stop partitioning one integer axis by convention: define
sorting layers (Seabed / Water / Deck / World / Overlay) so the decor layer owns its whole order
range and a layer boundary makes crossing structurally impossible.

Not done here because it touches ~40 renderer assignment sites plus the mesh-hull `SortingGroup`
path (`IsoFacetHullFeature` enumerates `SortingLayer.layers` per camera), which is delicate enough to
want its own PR and its own eyeballs. `SortingBands` is a deliberate stepping stone: it names the
tiers that would *become* those layers, so the migration is a mechanical move rather than a
re-derivation.

## Consequences

- **The owner must re-run the region builders** for the fix to appear. No committed scene or prefab
  carries a `YSortSprite`; every one is added by a builder at author time, so the change rides the C#
  field initialisers and needs no scene migration — but nothing changes until the builders run.
- Sorting orders in the inspector are now four-digit numbers. That is the band being honest about the
  size of the world; `SortingBands` is where they are explained.
- A region larger than ±300 m in Y will fail `SortingBandsTests` with the number to use. Raising
  `DecorHalfExtentMetres` is the intended response, and it is cheap.
- Anything that must sit above all decor must use `SortingBands.AboveDecor`, not a literal. A bare
  number above the old 40 will now be buried mid-band.
- **A fixed sorting order on an upright world object is now a bug by default.** While the band was
  9.5 m wide, everything saturated together and a hand-picked 2, 3 or 9 read acceptably; it does not
  any more. Anything a character can walk around wants a `YSortSprite`, and anything that must ride
  *with* such an object (its smoke, its shadow, its glow) wants an order derived from that object's
  renderer, not a number of its own. `Nine Mile Creek`'s trees still run a private `-Y × 2` scheme
  and its dory a fixed order; both are older than the component and are left for their own pass.
- `StPetersGroundCoverBudgetTests.TheSortingBandsReach_…` (on #434) measures the defect using the old
  literals. It keeps passing, but its logged figure becomes untrue once this lands — it should be
  re-pointed at `SortingBands` when the two branches meet.
