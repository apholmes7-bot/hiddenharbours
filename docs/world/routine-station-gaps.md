# Routine destination references

The mainland bar landing and fuel stop are installed on pinned main
`d46bde303e6aa256d23d47a05ede5d7d6f38e51e`. The only remaining exception in
this audit is the upstairs post-office home destination. No reference is renamed
and this change authors no station position, scene, crossing or schedule.

## Mainland destinations now resolved

[PR 1537](https://github.com/apholmes7-bot/hiddenharbours/pull/1537), merged as
`f1089fe61462a7d1cc2a1366c6f918ef940cb66b`, installs both stations in the
Nine Mile Creek scene's `RoutineStations` table:

| Station | Installed scene-world position | Consumers now resolved |
|---|---|---|
| `station.nine_mile_creek.bar_landing` | `(59.6192322, -152.379868)` | Two stops in `routine.junior_poirier` and the mainland endpoint of `crossing.st_peters_nine_mile_creek_bar` |
| `station.nine_mile_creek.fuel_stop` | `(-155.33, 109.42)` | One stop in `routine.junior_poirier` |

That merge also extends the crossing to the landing and adjusts Wednesday's
schedule to allow for the journey. The earlier endpoint `(338, -197.7)` is not
the installed destination. The fuel-store pivot is not used as a substitute for
the authored service stance.

The three resolved consumer/region/station exceptions have been removed from
`KnownStationExceptions`. Their previous occurrence counts were `2, 1, 1`.
The exact set-equivalence and occurrence-count assertions remain: deleting a
station, adding a broken reference or leaving a stale exception still fails.

## Remaining location authoring

| Consumer | Region | Missing station | Exact count |
|---|---|---|---|
| `home.st_peters_red_saltbox` | `region.st_peters` | `station.st_peters.home_post_office` | 1 |

The home asset rehouses its resident above the post office. Existing ground-floor
stations are the door `(12.539912, 35.914948)` and counter `(16.68, 41.039497)`.
Neither substitutes for an upstairs home/floor anchor, its owning interior,
safe stance, heading and approach. No upstairs station exists on this base.
The remaining exception must be removed when that destination is authored.

Routine destinations do not authorize the player's departure during the first
week. The island weather and story policy remain separate from NPC routing.

## Regression coverage

The existing `RoutineCrossRegionContentTests` fixture already checks weekly
schedules and crossing regions. Its station-reference case loads every routine,
ordinary day and weekly override, resolves `RoutineDef.RegionOf`, and reads
`RoutineStations` components only from that region's serialized scene. A
set-piece id, a station in another region or a builder declaration cannot satisfy
it. Crossings and same-region rehousing references use the same lookup.

The existing `NpcContentValidationTests` covers NPC definition relationships;
this case extends the cross-region fixture for routine and crossing references.
The separately proposed broader station census in #1460 remains independent;
none of its files is changed here.

The previous head passed automated run 45345101147. The refreshed source was
checked against the installed station rows and crossing/routine data; compilation
and EditMode execution of the updated head remain pending. No local editor or
player journey ran. The remaining upstairs location requires separate authoring
before that rehousing journey can be verified.
