# A week of casts at the wharf head

`WharfVisitCatches.Build` answers one visit without changing a scene, inventory, licence or save.
Its input is the world seed, arrival clock second, spot id, explicit rod numbers and bait identity/preferences.
Its setup supplies the player's existing species pool, clock pace, time-addressed water and existing fishing settings.
There is no separate villager stock table. Each cast returns its start, wait, species id, weight in kg,
fight duration, strength class and landed/lost result. A blank has a wait but no bite, weight or fight.
Eligibility is checked at the prospective bite, so crossing an hour window during the wait matters.

## Controlled comparison week

Seed **4242**, spot `station.st_peters.wharf_head`, High Summer, visits at **07:00 and 19:00**
on days 1–7. The clock uses the committed 1,800 seconds per day. The rod snapshot is `gear.rod`,
reel multiplier 1, fight help 0; bait is the committed `bait.capelin` and its own preferences.
Three casts per visit use the new `wharf_visit.st_peters` Def. Wait/fight values below are clock seconds.
The next cast starts four seconds after the previous wait and fight finish.

This is a controlled example, **not a forecast of that seed's weather**. Water stays eight metres deep
for each visit; its sea state, tide height and tide rate are the explicit columns below. The date stays
High Summer and the hour advances during each cast. School boosts are disabled for both anglers in this
comparison to expose the common stock and bait weights; normal callers pass the same school settings and
water model as the player's rod. No committed settings are changed. Moving-water fish cannot bite on day
4's slack water. Source-level calculation produced these rows; runtime parity remains pending CI in
`WharfVisitCatchesTests.ControlledExampleWeekMatchesTheDocument`.

Each entry reads **species, kg; wait; fight; strength; result**, in cast order.

| Day | Arrival | Sea (0–1) | Tide m / rate m per hour | Casts |
|---|---|---|---|---|
| 1 | 07:00 | 0.0 | -1 / 1 | fish.mackerel 0.71kg; wait 23.02s; fight 6.40s; Light; landed <br> fish.atlantic_cod 6.75kg; wait 14.56s; fight 9.60s; Strong; landed <br> fish.mackerel 0.37kg; wait 13.25s; fight 6.40s; Light; landed |
| 1 | 19:00 | 0.0 | -1 / 1 | fish.mackerel 0.32kg; wait 20.09s; fight 6.40s; Light; landed <br> fish.mackerel 1.21kg; wait 20.18s; fight 6.40s; Light; landed <br> fish.mackerel 0.33kg; wait 11.04s; fight 6.40s; Light; landed |
| 2 | 07:00 | 0.5 | 0 / 1 | fish.atlantic_cod 9.04kg; wait 17.82s; fight 9.60s; Strong; landed <br> fish.mackerel 1.39kg; wait 7.15s; fight 6.40s; Light; landed <br> fish.atlantic_cod 5.52kg; wait 11.48s; fight 9.60s; Strong; landed |
| 2 | 19:00 | 0.5 | 0 / 1 | fish.mackerel 1.07kg; wait 16.20s; fight 6.40s; Light; landed <br> fish.atlantic_cod 8.34kg; wait 12.33s; fight 9.60s; Strong; landed <br> fish.mackerel 0.61kg; wait 9.52s; fight 6.40s; Light; landed |
| 3 | 07:00 | 1.0 | 1 / 1 | fish.mackerel 1.00kg; wait 10.03s; fight 6.40s; Light; landed <br> fish.mackerel 1.48kg; wait 12.43s; fight 6.40s; Light; landed <br> fish.mackerel 1.22kg; wait 12.50s; fight 6.40s; Light; landed |
| 3 | 19:00 | 1.0 | 1 / 1 | fish.atlantic_cod 7.43kg; wait 5.98s; fight 9.60s; Strong; landed <br> fish.mackerel 0.97kg; wait 7.05s; fight 6.40s; Light; landed <br> fish.atlantic_cod 10.37kg; wait 8.41s; fight 9.60s; Strong; lost |
| 4 | 07:00 | 0.0 | -1 / 0 | fish.atlantic_cod 11.98kg; wait 11.04s; fight 9.60s; Strong; landed <br> fish.atlantic_cod 3.87kg; wait 9.65s; fight 9.60s; Strong; landed <br> fish.atlantic_cod 7.75kg; wait 11.10s; fight 9.60s; Strong; landed |
| 4 | 19:00 | 0.0 | -1 / 0 | fish.atlantic_cod 5.72kg; wait 15.60s; fight 9.60s; Strong; lost <br> fish.mackerel 1.14kg; wait 17.96s; fight 6.40s; Light; landed <br> fish.atlantic_cod 9.13kg; wait 17.53s; fight 9.60s; Strong; landed |
| 5 | 07:00 | 0.5 | 0 / 1 | fish.atlantic_cod 6.89kg; wait 14.84s; fight 9.60s; Strong; landed <br> fish.atlantic_cod 6.00kg; wait 15.76s; fight 9.60s; Strong; lost <br> fish.mackerel 0.78kg; wait 9.30s; fight 6.40s; Light; lost |
| 5 | 19:00 | 0.5 | 0 / 1 | fish.atlantic_cod 10.53kg; wait 8.10s; fight 9.60s; Strong; landed <br> fish.atlantic_cod 6.83kg; wait 16.36s; fight 9.60s; Strong; landed <br> fish.atlantic_cod 7.23kg; wait 19.13s; fight 9.60s; Strong; lost |
| 6 | 07:00 | 1.0 | 1 / 1 | fish.atlantic_cod 9.41kg; wait 13.15s; fight 9.60s; Strong; landed <br> fish.atlantic_cod 10.32kg; wait 14.10s; fight 9.60s; Strong; landed <br> fish.atlantic_cod 10.04kg; wait 14.88s; fight 9.60s; Strong; landed |
| 6 | 19:00 | 1.0 | 1 / 1 | fish.atlantic_cod 9.22kg; wait 13.73s; fight 9.60s; Strong; landed <br> fish.mackerel 1.48kg; wait 12.60s; fight 6.40s; Light; landed <br> fish.atlantic_cod 10.27kg; wait 10.78s; fight 9.60s; Strong; landed |
| 7 | 07:00 | 0.0 | -1 / 1 | fish.mackerel 0.94kg; wait 23.78s; fight 6.40s; Light; landed <br> fish.mackerel 0.90kg; wait 12.72s; fight 6.40s; Light; landed <br> fish.atlantic_cod 9.81kg; wait 18.88s; fight 9.60s; Strong; lost |
| 7 | 19:00 | 0.0 | -1 / 1 | fish.atlantic_cod 9.33kg; wait 23.02s; fight 9.60s; Strong; lost <br> fish.atlantic_cod 11.28kg; wait 8.96s; fight 9.60s; Strong; landed <br> fish.mackerel 0.43kg; wait 18.74s; fight 6.40s; Light; landed |

There are **42 bites: 24 cod and 18 mackerel; 35 landed and seven lost**. No blank was forced into this
week: cod and mackerel remain eligible throughout these High Summer visits. Flounder is out of season;
bass remains possible in these hours when the tide moves, but capelin favours cod and mackerel and this
seed draws no bass. A dead-hour fixture uses a genuinely closed species window and must return three
blanks, rather than substituting another fish.

A player with the same pool, bait, hour, tide and school context gets exactly the same **eligible species
and preference weights** through `CatchResolver.Matches` and `EffectiveWeight`. His actual cast sequence
and skill-driven fight are his own: this model uses a separately keyed draw and the visit Def's risk,
not the player's interactive fight outcome. Weather uses the player's `SeaFightMath` wait and size bias.
Strength comes from the species' `RodFightDef`, with the existing neutral strength for an unassigned fish.
Size stays inside `FishSpeciesDef.MinWeightKg` and `MaxWeightKg`; no length range is invented.

## Integration and proof boundary

Read the player's serialized `_regionFish` pool at setup; `RegionDef.SpawnFishIds` is not that pool.
The content test reads the committed scene's eight references and sweeps all four seasons, every hour,
three tide heights, three tide rates and three sea states: **7,776 cast records**. Other cases pin the
hash stream, independent peer replay, changing seed/kit, empty water, bare hooks, timing, risk and rod help.
Allocation tests use the existing calibrated `ManagedAllocationProbe`: only the returned list and its
backing array may allocate after setup, including the shipped pool with schools enabled. School tuning
above the existing model's four-species cell buffer is rejected at setup when the pool could exceed it.

The routine lane supplies the actual hook position and deterministic water adapter, passes each angler's
own rod/skill snapshot and handles cast, hold-up and release. Water reads must address the requested
seed and time, never a current-player service. Content is fixed while the calculator is in use; one
calculator per concurrent caller keeps school scratch private. This code does not attach to a routine,
choose a new station, consume bait or grant ownership of the result. `Landed` means brought to hand;
licence and conservation rules still decide whether it may be kept.

There are **no saved fields**. The returned structs are session snapshots. Replaying a visit needs its
arrival time, spot, seed and original kit; persisting a partly completed routine would need its visit
anchor and original kit per participant, and any transferred catch would need a separately authorised
ownership/save step. Neither is added here. Unity compilation, test execution, identity validation and
visual checks were not run locally; CI is pending. No visible assets or existing rules were changed.
