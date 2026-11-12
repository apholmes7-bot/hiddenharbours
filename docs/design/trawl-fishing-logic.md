# Trawl fishing: unwired logic foundation

Status: an isolated prototype for the authorized larger-boat work. No scene, prefab, input,
HUD, live content, economy or save path constructs these classes. All numbers in the tests
are synthetic proof fixtures, not fishing rates or accepted vessel tuning.

The intended job is to shoot the gear, keep the net working across a known ground, haul the
codend aboard, sort the catch by hand, and carry the retained fish into physical storage.
The implemented boundary ends at a sorting ledger with **kept catch on deck**. It grants no
inventory, money, ownership or depletion rights. Existing fishing stays playable as before.

## What is reused

| Existing model or contract | Reuse here |
|---|---|
| `SchoolField.TrySample` | `TrawlSchoolFieldBridge` calls this exact method with the same seed/time, species, certified patch, environment and reaction. |
| `SchoolFieldSample` | The solver consumes its species id, stable field key, centre, depth, footprint and individual count. No second population table is generated. |
| `FishSpeciesDef` | Copy its stable id, season mask and minimum/maximum weight into immutable `TrawlSpeciesFacts`; leave the original asset untouched. |
| `LivingFishBehaviour` and `SchoolHabitatPatch` | The existing field still decides whether any occupant exists. Disabled profiles and unknown certificates produce no trawl population. |
| `StableHash.CatchSeed`, `Fold`, `Finalize` | Independent count and size streams derived from world seed, stable tow id, exact tow-start game time, species and school key. |
| Core `Season` and game-clock units | Sample season at the canonical query instant. All durations and speeds use actual Core game seconds; there is no wall-clock input or assumed day length. |

The existing `FishSchoolModel`, `CatchResolver`, pot/longline controllers, all fish Defs and
their weights, prices and gear masks are unchanged. Main has no distinct `Trawl` gear bit.
`Gear.Net` is therefore **not** interpreted as trawl permission. The bridge requires an
explicit reviewed eligibility input; false is closed. The source must supply bycatch too,
rather than filtering the survey to the skipper's target list.

## Data and handling

`TrawlGearDef` and `TrawlHandlingDef` are serializable **data-only definition schemas**, copied
by value into the plain logic. They deliberately have no tuned defaults and no authored
entities. Asset wrappers and content authoring wait for identity-ledger registration: an
existing draft owns that shared file. This avoids editing it or weakening its test. These
schemas are not a replacement content registry, nor permission to instantiate hard-coded
live gear. No new ScriptableObject type or asset is introduced by this change.

| Definition | Explicit tuning, with units |
|---|---|
| Gear | Operating depth (m), warp/depth ratios, tow speed window (m/game-second), door spread window and mouth height (m), capture fraction, hard-bottom threshold, integrity loss per hard metre. |
| Gear budgets | Sampling interval and maximum tow duration (game seconds), codend capacity (individuals), maximum bins, schools per bin, distinct schools per tow and track knots. |
| Handling | Stern or side, required work station, shooting/hauling duration (game seconds), separate maximum working speeds, maximum sea state, manual sorting rate (individuals/game-second). |

Default, non-finite, inverted and missing tuning fails validation. Valid windows include
their endpoints. Zero capture or zero wear explicitly disables that effect. Zero working
speed means work must start stopped; it is allowed. Stern handling requires the astern
station; side handling requires the specified port or starboard station. The tests demonstrate
a slower, stopped side-handling profile beside a stern profile that can start work with way
on. Their numeric differences are fixtures, not a claim that every real side trawler stops.

`TrawlOperation` pins the sequence `Stowed → Shooting → Towing → Hauling → CodendOnDeck`.
Start commands validate station, speed and sea state; completion commands require elapsed
work time. An acknowledgement arriving late preserves the scheduled shooting completion
time. Backwards/non-finite clocks, wrong-phase commands and unrepresentable deadlines fail
without changing state. A new operation represents a new tow. There is no automatic fleet
work, crew hire or perpetual fishing loop. A future controller must certify safe conditions
throughout the job and explicitly pause interrupted deck work; the command model does not
observe the boat between commands.

## Track, ground and sampling

`TrawlTrack` owns a copy of chronological metre/time knots for the **net**, including warp and
door spread. Positions and controls interpolate linearly. Supplying a hull track as though
the net teleported behind it would be incorrect: boat-to-gear lag, turns and supported rope
geometry need their own later adapter. No renderer is implied here.

The complete tow is evaluated as a bounded batch. Bins are anchored at tow start, with
`SampleGameSeconds` spacing and a final partial bin. Their query time is the midpoint.
Rendering frames, slow-tick frequency and sleep do not drive this sampling schedule. Extra
collinear knots leave the same schedule and catch. Polyline arc length includes corners;
an encounter follows the actual polyline instead of the chord between a bin's endpoints.
Every constituent segment must satisfy the speed window, and warp/spread endpoints must
stay inside their windows throughout a fishing bin. A dash followed by a stop cannot be
hidden inside an acceptable average speed. Bins that cross a control gate conservatively
catch nothing; this is deliberate discretization, not an exact hydrodynamic solver.

`ITrawlSurvey.Fill` is an explicitly supplied, Fishing-local read seam, not a live World
dependency. It receives the whole bin, its half-width and exact query time. The source must
certify one ground's bottom depth and hardness over the whole bin, sample its season at that
time, and enumerate every potentially intersecting school including its full footprint.
A ground boundary needs separate certified bins/tows; an uncertified mixed bin is unknown.
It can enumerate certified cells through the bridge with caller-owned buffers. No scene
adapter or new cross-module interface is installed. A later World adapter must use Core.

Unknown ground/coverage or a truncated enumeration returns failure. Overflowing a sample,
track or encounter budget is an explicit `BudgetExceeded`, never a partial-looking success.
Duplicate school identity within one survey, mismatched species facts or changing individual
counts within an encounter fail closed. Failure returns no catch or committed wear; callers
must not treat it as a receipt. There is no table-roll fallback for an empty or unknown sea.

## Catch and damage arithmetic

For a certified bottom depth `d`, the mouth fishes `[max(0,d-mouthHeight), d]`. A species must
be explicitly eligible, in its authored catch season, physically sampled by SchoolField,
inside that depth layer and intersected by the swept track. Bite time/tide preferences are
not reused as physical-presence gates. Season is a capture gate here, not removal of an
off-season occupant from its habitat.

For footprint radius `r`, intersect the net polyline with a disc of radius `r + spread/2`.
Let the intersecting path length be `L`. Per-bin coverage is
`min(1, L*spread/(π*r²))`; a point occupant intersected by moving gear has coverage one.
Its per-tow uncaught fraction is multiplied by `1-coverage*captureFraction`. This is an
area-exposure approximation, not a spatially exact integration of individual swimmers.
The initial version has no mesh/length selection, escape panels, age or protected-species
content. Eligibility and release are not assertions of legal fishing permission.

For the sampled school count `N`, cumulative candidate count is
`floor(N*(1-uncaughtFraction) + stableCountDraw)`, capped at `N`. Only newly caught individuals
can enter the codend. Repeated passes never exceed that school's count in one tow. Capacity
fills in bin order; ties in one bin use ordinal species id then unsigned school key. Provider
enumeration and target selection cannot bias that priority, and late arrivals cannot displace
earlier catch. This tie-break is mechanical arbitration within a sampling bin, not a fish value
ranking. Individual counts, target catch and bycatch share the same capacity.

Each lot gets a reproducible representative unit weight inside the existing species weight
window on an independent hash stream. Total mass is count times that weight. It is a coarse
lot model, not an individual size distribution or price calculation. Nothing changes a
species' authored size range or the existing catch resolver's random stream.

Hard-bottom wear per bin is
`distance * max(0,hardness-threshold) * damagePerHardMetre`, while the sampled warp/depth
ratio reaches bottom contact. Warp/contact, hardness, species position and bottom depth are
midpoint samples; changing conditions below the bin scale need finer reviewed sampling.
Wear still applies with excess warp or speed that prevents fishing. Integrity clamps at zero.
If gear breaks inside a bin, collection ends at the corresponding **arc-length position**;
fish beyond the break position cannot enter the codend. Prior catch remains in the codend.
This models worn gear ceasing to fish, not a lost codend, wreck or hull damage system.

## Haul, bycatch and physical handling

`TrawlTowResult` exposes immutable lots and aggregate counts/mass. It is replayable and is
explicitly a **proposal**, not actual caught occupants. `TrawlDeckBatch` starts with every
individual unsorted. `SortByHand` needs chronological elapsed work at the handling Def's
rate and moves only the requested positive quantity to kept-on-deck or released counters.
Overdraw, duplicate sorting, backwards clocks and insufficient work fail without mutation.
Every individual remains accounted for: unsorted + kept + released = codend count.
Target selection only labels bycatch; it never makes unwanted fish disappear. Choosing
release is manual work too. Released counters do not spawn a fish into the world.

Kept fish must subsequently be picked up, carried and placed into supported physical storage.
That integration must reserve/deplete the same occupants transactionally, represent sprites
in codend/deck/storage, and account for release fate. This prototype deliberately cannot
grant those rights. Replaying it, or a second tow through the same school, is **not** stock
depletion proof. A 32-bit field key identifies a sampled group, not a durable occupant id.

## Proof, cost and activation gates

`TrawlOperationTests`, `TrawlTowSolverTests` and `TrawlDeckBatchTests` cover state sequencing,
side/stern handling, all fishing windows, net-path geometry, duration, seasonal capture,
bycatch, chronological capacity, repeated passes, damage/break positions, malformed data,
budgets, manual conservation and exact replay across 1,000 seeds at long game times.
`TrawlSchoolFieldBridgeTests` additionally checks the real native ScriptableObject-to-field
bridge, including disabled occupancy and unknown certificates. It is a distinct native suite;
compiling it does not mean it ran in a standalone managed harness.

This is batch logic, with no Update loop. Work is bounded by configured bins, track knots,
schools per sample and distinct encounters. The simple scans/sorts are intentionally for
small reviewed budgets: worst-case work includes `O(bins * (schoolsPerSample² +
schoolsPerSample*(encounters+knots) + encounters²))`. Scratch arrays are allocated once per
resolve at the configured caps; results and deck ledgers allocate once per batch. There is
no per-query array creation in the solver. This is not a zero-allocation or 60-fps claim.

Before activation: author and register one asset per gear/handling entity; review trawl
eligibility, local grounds, seasonal/size selectivity and balance; supply Core ecology and
track/gear adapters; certify complete surveys and set measured slow-tick budgets; connect
live occupant reservation/depletion and physical manual transfer; review save migration;
then add the scene/input/HUD/art as a separate tested slice. No later-phase fleet automation
or save schema has been smuggled into the current build.

## Reference basis

The operating vocabulary comes from the [FAO bottom-otter-trawl fact sheet](https://www.fao.org/fishery/docs/CDrom/ARTFIMED/ArtFiWeb/descript/Gear/geartype/gt306.htm):
doors spread a bottom net; winches handle the warps; substrate affects ground-gear protection;
non-target catch must be considered. The [NOAA bottom-trawl overview](https://www.fisheries.noaa.gov/national/bycatch/fishing-gear-bottom-trawls)
supports the codend and bycatch framing. The [FAO vessel classification, archived by a fisheries research institute](https://drs.cift.res.in/server/api/core/bitstreams/196099bd-3b29-4de3-817c-a5b367b10b1d/content)
describes side deployment with gallows and stern retrieval, commonly using a ramp. The
capture formula, working limits, sample schedule and fixture numbers above are gameplay
design choices; these references do not validate their tuning.
