# Trawl content review and activation gates

Status: registered, **inactive prototype content**. The ledger-additions reader is now on main.
The assets below are outside Resources, have no production references, and set
`ReviewedForUse = false`. Gear capture is also zero. Registration does not activate fishing.
All operating numbers are provisional game tuning for review, not accepted real-vessel limits.

## Reused contracts and authored entities

Keep the existing `TrawlGearDef` and `TrawlHandlingDef` value schemas, validation and plain
operation/tow/deck logic. New `TrawlGearAssetDef` and `TrawlHandlingAssetDef` wrappers store
them in their public `Spec` field, the identity shape already supported by
`DefIdentityContractTests`. Each type and entity has its own ledger addition; the frozen
baseline is untouched. `TryGetReviewedSpec` fails with an empty result unless the data is
both valid and explicitly reviewed. A returned value is a copy. This is only the data-review
gate; it cannot certify a ground, reserve stock, move goods or enable a scene.

| Asset under `Assets/_Project/Data/Trawls/` | Stable id | Provisional behavior |
|---|---|---|
| `BottomOtterPrototype.asset` | `trawl_gear.bottom_otter_prototype` | 10–100 m operating depth; warp/depth 2–5; speed 0.5–2.5 m/game-second; spread 4–12 m; 3 m mouth; capture fraction **0**. |
| `SternPrototype.asset` | `trawl_handling.stern_prototype` | Astern station; shoot 30, haul 60 game seconds; work speed limits 0.5 / 0.25 m/game-second; sea-state limit 0.5. |
| `SideStarboardPrototype.asset` | `trawl_handling.side_starboard_prototype` | Starboard station; shoot 60, haul 90 game seconds; work starts stopped; sea-state limit 0.35. |

Both handling profiles sort one individual per game second. Gear uses hard-bottom threshold
0.5 and integrity loss 0.001 per hard metre above that threshold. These new values do not
change any existing number. No claim is made that all side trawlers stop or that either
profile is balanced. Existing operation validation and manual sorting are reused; no crew
controller, library, equipment grant or fleet routine consumes these assets.

## Local species and selectivity review

The scope is the five species named by the three South Water grounds. `Gear.Net` and
`FishingGroundDef.FishIds` are not trawl permission. The following are review recommendations,
not new live eligibility flags or changes to `FishSpeciesDef`.

| Species id | Current season mask / weight (kg) | Recommended bottom-trawl treatment |
|---|---|---|
| `fish.atlantic_cod` | 15: all four seasons / 2–12 | Target candidate on its named grounds, conditional on an actual sampled school in the net's bottom layer. |
| `fish.haddock` | 15 / 1–6 | Target candidate on Deep Mud. Do not add it to shallower grounds from a typical biology band. |
| `fish.pollock` | 15 / 1.5–9 | Target candidate on the Bank; actual school depth and hard-ground risk still decide. Rod-only legacy gear does not prohibit a separately reviewed future trawl method. |
| `fish.atlantic_herring` | 5: Early Spring and The Turn / 0.1–0.5 | Allow incidental capture only if a real school intersects the bottom mouth. Do not drag a pelagic school or its deposited eggs onto the bottom. |
| `fish.mackerel` | 15 / 0.3–1.5 | Same conditional incidental treatment; no fabricated deep school or forced bottom occupancy. |

The biology supports these candidates: NOAA describes trawl harvest of
[Atlantic cod](https://www.fisheries.noaa.gov/species/atlantic-cod) and
[haddock](https://www.fisheries.noaa.gov/species/haddock), and lists Atlantic pollock in its
[Northeast bottom-trawl fishery](https://www.fisheries.noaa.gov/national/marine-mammal-protection/northeast-bottom-trawl-fishery-mmpa-list-fisheries).
[Herring](https://www.fisheries.noaa.gov/species/atlantic-herring) can be taken with bottom and
midwater trawls; [mackerel](https://www.fisheries.noaa.gov/species/atlantic-mackerel) schools
near the surface. The proposed target/incidental split is a game-design inference, not a
biological exclusion or a regulatory claim. None of these sources validates local abundance,
capture rates, the provisional gear numbers or the game's four-season masks.

Preserve the current masks at the canonical per-bin query time, including herring's two
closed seasons. Preserve weight ranges as current content facts. The solver draws one
representative weight per school lot within those bounds; it has no mesh-size retention
curve, life-stage selection or independently measured seasonal size composition. Those
limits are explicit: no reviewed selective fishing rate is claimed. Capture remains zero
until that data is supplied and tested. Bycatch must enter the survey and deck accounting;
target choice may label it, never erase it or grant release survival.

The five current species assets do not author `LivingBehaviour`, so the default model is
disabled. Their ecology reference profiles describe typical bands, not certified occupancy
limits. Leave both untouched. An empty or disabled field produces no trawl population.

## Ground review: authored rectangles are not complete certificates

| Ground id suffix (`fishing_ground.`) | Authored depth / bottom / hardness | Species list | Certification result |
|---|---|---|---|
| `south_water_shelf` | 20–21 m / sand / 0.2 | cod, herring, mackerel | Candidate soft-ground trial; no full-bin survey certificate. |
| `south_water_bank` | 35 m / gravel + rock / 0.8 | cod, pollock | Hard-ground damage trial only; no safety or tow permission implied. |
| `south_water_deep_mud` | 70 m / mud / 0.05 | haddock, cod | Candidate deeper trial; no full-bin survey certificate. |

`TrawlSessionGround` already copies these rectangles and species facts. Reuse it without
changing the grounds. Their depth fields are authored at mean tide. A future Core adapter
must certify instantaneous bottom depth, habitat/vegetation (including explicit unknown),
the whole swept footprint, boundary crossing and complete school enumeration at each bin's
query time. A copied rectangle or a centre-point reading alone proves none of that.
Unknown, mixed or truncated coverage must fail closed through `ITrawlSurvey`; no table-roll
fallback. Do not edit a seabed or builder to make the prototype pass.

## Bounded batch measurements

The authored gear caps are 32 bins, 16 schools per bin, 32 distinct encounters and 64 net-track
knots, with 5 game seconds per bin and a 160-game-second maximum tow. Codend capacity is 512
individuals. Overflow remains an explicit solver failure; these are caps, not promises that
the live field fits. One completed tow is one batch job, never one job per rendering frame.

`TrawlSlowTickBudgetTests` loads that asset and stresses **all four caps together**, with 32
distinct one-individual schools and complete fixed surveys. It copies the spec, sets capture
to one and wear to zero to avoid a zero-yield or early-break shortcut, then warms four solves
and measures 32. It asserts 1,024 sampled bins and 1,024 caught individuals across those
measured solves, 32 final lots, and a mean solve below **10 ms**. Existing budgets and failure
tests are unchanged. The synthetic schools move with each query to exercise intersection;
they are not evidence of physical schools or local catches.

No-editor measurements of the actual compiled solver and same assertion helper were
0.283 ms/solve under the editor's standalone Mono and 0.922 ms under a standalone .NET host.
Both completed the exact workload. Managed allocation positive controls saw 4,112 / 4,120
bytes respectively; the whole helper allocated 146,648 / 162,136 bytes including setup,
warmup, 32 solves and assertions. These are observed diagnostic totals, **not** per-solve
byte bounds or a zero-allocation claim. No engine shim supplied the measurements.

This measures arithmetic with a synthetic survey. It excludes Core ecology acquisition,
real SchoolField enumeration, reservation, graphics and physical transfer. It is not a
worst-case input proof, a native editor result, a whole-slow-tick budget or a 60-fps claim.
The 10 ms regression gate is provisional. Live scheduling needs a native measurement on
the supported baseline, an aggregate limit across boats and a reviewed policy for spreading
completed tows over slow ticks before any activation.

## Remaining activation gates

Native CI must import all three assets and pass `TrawlContentValidationTests`,
`TrawlSlowTickBudgetTests`, `DefIdentityLedgerTests`, `DefIdentityContractTests` and
`NmcH7ContentValidationTests`. The six new native cases compile; they have not run locally.
The standalone harness passed all 69 existing operation/tow/deck cases plus the shared
stress assertion helper. Compilation and text-level registration checks do not prove native
type discovery or asset import. No editor lease remained and no editor was launched.

After content and performance review, the future live slice must supply the Core certificate
and net-track adapters, capture/selectivity data and living-occupant identities; reserve and
deplete those same occupants transactionally; sort and release with explicit fate; and load,
carry and unload codend/deck/storage contents by hand on physical supports. Review persistence
and save migration before granting ownership. The current prototype counters and replayable
catch proposals remain insufficient for every one of those permissions. Scene, input, HUD,
fleet automation and art activation remain separate authorized work.
