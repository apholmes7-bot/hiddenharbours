# Fleet art review: oldest hulls first

**Status:** working list, 12 Nov 2026. Owner request: give every boat the review the trawlers
got, oldest art first, one boat per PR.

## What a pass is
Each hull is rebuilt in place, under her existing ids, to the hull layout
(`docs/art/hull-layout-spec.md`): one rig that publishes `hull-geometry@1` and a written
`hull-walk@1`, rooms as geometry, hinged doors, a paint axis, named working points, and a bake by
the one-hull intake route. Each PR carries a before and after plate (same spot, light, camera and
two headings) and a table of every moved test pin.

**Fixed for every hull:** length, draught, speed, price, stats and save ids. The boat Def is not
in the diff. No scene roster is patched. The zodiacs' and the go-fast's speeds stay frozen until
the owner has seen the fleet speed table.

## Done
| Hull | State |
|---|---|
| Stern trawler | rebuilt (merged) |
| Stern trawler Mk2 | rebuilt (draft PR open) |
| Go-fast, bowrider, express cruiser | on the hull layout since 23 Oct |
| Motor yacht 84, expedition yacht 120, yacht tender | on the hull layout since 2 Nov |
| Sport fisher convertible, sport fisher skybridge | on the hull layout |

## The order
Age is the last change to the rig source on main. "Paint" is whether the rig publishes schemes.

| # | Hull | Rig last changed | Paint | What she needs |
|---|---|---|---|---|
| 1 | Stern trawler: wrecked scheme | 12 Nov | 4 schemes | A fifth scheme, `wrecked` (rust streaks, bare plate, weed and tide line, dulled glass), for the beached wreck. The four trawl working points the Mk2 has (`warps`, `codEnd`, `sorting`, `crew`). |
| 2 | Side dragger | 3 Sep | none | Full pass. Mesh-only today: no rooms, no doors, no paint. Side-trawl points: fore and aft gallows, the rail where the cod end comes aboard, sorting ground, hold hatch. |
| 3 | Sport skiff | 19 Jul | none | The oldest boat art in the game (366 lines). Full pass: hull form, thwarts, outboard well, paint axis, a walk. |
| 4 | Dory | 25 Jul | none | The first boat the player owns, seen more than any other. Full pass with care for her silhouette: sheer, tholes, oars, thwarts, the outboard variant, paint axis. |
| 5 | Punt (and the upgraded punt) | 25 Jul | yes | Layout and walk; paint carried over. Both Defs share the rig. |
| 6 | Console skiff | 25 Jul | yes | Layout and walk; console and helm as working points. |
| 7 | Zodiacs (FRC, Hurricane) | 14 Aug | none | Layout, walk, paint axis for tubes and console. Art and paint only; speeds frozen. |
| 8 | Sport skiff Mk2 (and the twin) | 14 Aug | yes | Layout and walk; paint carried over. |
| 9 | Coastal packet | 3 Sep | none | Full pass: house rooms, doors, cargo hatches and derrick as working points, paint axis. She now hosts the old-layout tie journey test; that test needs a new home or a retirement ruling when she moves. |
| 10 | Tanker | 3 Sep | none | Full pass: house rooms, catwalk, manifold and hose points, paint axis. |
| 11 | Cape Islander | 3 Sep | yes | Layout and walk; sheeted interior becomes rooms. |
| 12 | Lobster boat and her 18 variants | 3 Sep | yes | One rig family: three sizes, two tops, three regions. Layout and walk per size; hauler, pot table and bait points by name (the lobster routines read them). Likely several PRs, one per size. |
| 13 | Sloop 30, sloop 88 | 16 Sep | not checked | Layout and walk; rig and sails kept; cabin as rooms. |
| 14 | Go-fast | 23 Oct | yes | Already on the layout. One open render red on a GPU (`EveryFleetPackHull_RendersAsHerOwnRig`, 203 of 17730 px) to read and fix. |

After the boats: trawl fishing (the net up the ramp with individual pooled fish sprites, spilled,
sorted and stowed by hand).

## Why this order
- The two jobs in flight (wrecked scheme, side dragger) finish the trawler set first.
- Then strictly by age of the art. The sport skiff and dory are the oldest and have no paint;
  the dory is also the boat every player starts in.
- Hulls sharing a rig move together (punt pair, skiff Mk2 pair, zodiac pair, lobster family).
- The go-fast is last because she is already rebuilt; only her render red is owed.

## Known costs
- Every rebuild retires the old sidecar and interior-kit files of that hull and moves count pins
  in about a dozen test classes. None is deleted or loosened; each PR lists them.
- Each hull with schemes adds paint Def assets and append-only identity ledger rows.
- Small open boats have no rooms; their pass is hull form, a walk, paint and working points.
- Clips several hulls wait on: standing helm, stairs, ladder, sit.
