# Missing routine destinations

No existing routine station is an equivalent destination for the mainland bar
landing, mainland fuel stop or post-office home. Their references remain unchanged
pending location authoring. Substituting a shop counter would send a resident to
the wrong activity or floor. No station identity is renamed and no position is added.

The static inventory at base `c626cea10d1cb496df41cca2f9ab2f580ad47b28`
finds 26 stations on St Peters, 4 in Nine Mile Creek and 43 in Cumberland.
All 108 ordinary/weekly routine entries were checked in their effective region;
three entries are missing, all in the Wednesday mainland route. The crossing
endpoint and the house's rehousing destination add two more references to the
same three absent destinations.

## Deferred location authoring

| Destination | Existing coordinate evidence | What the station still needs |
|---|---|---|
| `station.nine_mile_creek.bar_landing` | The crossing's `ToPath` runs from `(352, -200)` to `(338, -197.7)` in Nine Mile Creek world coordinates. The far station must join the final point `(338, -197.7)` unless the crossing is separately corrected. | A verified walkable landing at that endpoint, heading and lane connection. Neither coordinate is installed as a routine station. |
| `station.nine_mile_creek.fuel_stop` | The placed fuel-stop key-scene data puts the store piece `station.nmc_store` at ground `(−155, 139)`, with `Z: 6`. This is a set-piece id and ground location, not a routine station or an NPC stance. | A walkable service stance derived from the current fuel-stop geometry, transformed into the same scene-world coordinates as the routine table, plus heading and lane connection. No exact stance is authored; the store pivot cannot supply one. |
| `station.st_peters.home_post_office` | Existing ground-floor stations are the door `(12.539912, 35.914948)` and counter `(16.68, 41.039497)`. The home asset says the resident moves above the post office. | An upstairs home/floor anchor, its owning interior, safe stance, heading and approach. No upstairs station coordinate exists; neither ground-floor station is a substitute. |

The mainland table contains only `boat_gear_counter`, `boat_gear_keeper_home`,
`fishing_gear_counter` and `fishing_gear_keeper_home` under the
`station.nine_mile_creek.` prefix. They cannot supply aliases for the landing or
fuel errand. The old forecourt placement constant also cannot establish a station
at the current fuel stop.

The missing landing is referenced twice in `JuniorPoirierRoutine.asset` and once
by `StPetersNineMileCreekBar.asset`; the fuel stop once in that routine; the missing
home once by `StPetersRedSaltbox.asset`. These are authoring gaps, not permission
to open the mainland during the first week. The first-week weather policy remains
separate work.

## Regression coverage

`RoutineCrossRegionContentTests` already checks weekly schedules and crossing
regions. Its added test loads every routine, ordinary day and weekly override,
resolves `RoutineDef.RegionOf`, and reads only `RoutineStations` components from
that region's serialized scene. A set-piece id, station in another region or
declaration in a builder cannot satisfy it. Crossings and same-region rehousing
station references use the same lookup.

One `KnownStationExceptions` dictionary names the four consumer/region/station
combinations and their exact missing-reference counts: `2, 1, 1, 1`. The observed
set must equal that dictionary. Adding another broken reference fails; resolving
an existing one also fails until its obsolete exception is removed. Existing
schedule and crossing assertions are retained.

The static text inventory was run; Unity and EditMode tests were not run locally.
CI must compile and execute the extended suite. The parked station definitions
require a location-authoring change before the affected journeys can be verified.
