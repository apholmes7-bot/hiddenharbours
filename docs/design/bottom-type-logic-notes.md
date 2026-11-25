# Bottom-type logic proposal

This slice supplies inactive, callable Core/Fishing logic. It installs no scene service,
terrain provider, live content, save data or fleet behavior. The companion design is
draft PR 1129; this branch is independently based on the same pinned main.

`IBottomMap` is the only bottom interface. `DeterministicBottomMap` snapshots validated
`BottomMapDef.Settings` at setup. `TrapHabitat.DensityAt` is the shared evaluator for
future spawn weighting and current callable trap/rotation logic. It samples this interface;
it never samples art, changes `FishingGroundDef.Bottom`, or creates a second material table.

The three new Def classes are plain serializable proposal records, like the existing
plain trawl Defs. These [example records](trap-habitat-proposals/bottom-map.json) are
one entity per file outside Unity Data, with stable new ids and explicit tunables. They
are not auto-loaded ScriptableObjects. This keeps the shared identity ledger and existing
Def numbers unchanged. Asset authoring, ledger additions and validation belong to adoption.
An adapter must deserialize/copy records explicitly; this change adds no runtime loader.

[Lobster habitat](trap-habitat-proposals/lobster-habitat.json) favours rock and
[proposed crab habitat](trap-habitat-proposals/crab-habitat.json) favours mud. The
[crab species record](trap-habitat-proposals/crab-species.json) reserves a new proposed id;
it does not rename or retune `fish.rock_crab`. Taxonomy and remaining production species
fields are deliberately unresolved, not fabricated biological data. The example period
is four 28-day seasons at the current 1800-game-second day; adoption must derive the value
from the actual calendar configuration. Coordinates and rates are unaccepted proposal tuning.

`HabitatTrapCatch.Resolve` gates readiness and a matching single-species pool, calls
`TrapFill.Fill01`, scales additional-slot fill by placement-time density, then calls
`TrapFill.ResolveCount` and `PlacedTrapCatch.ResolveMany`. The ready floor of one stays.
The same placement-time density applies throughout the soak, preserving monotonic slots;
new placements follow migration. `DeckPot` receives the ordinary indexed animal outcomes.
The existing resolver's bait and eligibility checks and seeded weights remain intact.
The slow-tick allocation guarantee applies to map, density, count and planner calls;
haul-time resolution retains the existing resolver's allocations.

`TrapRotationPlanner.Plan` takes observations with catch per game hour and explicit history
validity, surveyed candidates, region bounds, terrain, a water level, habitat/time/seed,
[rotation tuning](trap-habitat-proposals/trap-rotation.json) and an output array. It returns
the written prefix length; old tail entries are not orders. Equal scores retain candidate
order. A known-good or unknown-history trap stays. Dry, excessively deep, unknown, occupied,
crowded, distant and out-of-bounds candidates are rejected. The caller supplies stable trap
order and recent-history snapshots, and consumes orders through physical boat/deck work.
Only endpoints are checked; safe transit is still the navigation layer's responsibility.

Adoption seams remain `NpcPotDayCatch.Build`, the placed-pot haul caller, `NpcPotDay`
scheduling and region composition. This change does not edit their classes or connect a
parallel catch system. Persistent individual identity, depletion and occupant transactions
remain unresolved activation gates; indexed per-animal catches preserve the current boundary.
PR 1123 is not on pinned main, so no sounder or helm file is changed.

The pure `BottomTypeAndTrapRotationTests` can execute against the real compiled runtime
assemblies with a managed no-editor runner. `HabitatTrapCatchIntegrationTests` additionally
cover actual ScriptableObject species, unchanged catch/deck composition and eligibility;
these require native Unity EditMode execution. Report them separately when only compiled.
