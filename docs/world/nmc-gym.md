# NMC gym (plan)

Status: **plan only.** No scene, prefab or tilemap is edited by this document. The equipment and the
exercises exist as data (body slice 2); a world-content lane builds the room from this plan.

Owner ask (2026-11-15): "add workout equipment and a gym to the NMC region".

Pillars: P5 Cozy but with Teeth (a stronger body lifts more and walks a load faster; a sedentary skipper
loses it), P3 Living Working Coast (the gym is a place villagers use, not a menu).

## Where

Nine Mile Creek, in an existing village building rather than a new one, so the region's street plan does
not change. Proposal: the back room of the community hall (or the loft over the fish plant's net store,
if the hall is taken), entered through a normal seamless-interior door (ADR 0036). It is indoors: no
weather, lit by the room's lamps.

## Footprint

About **8 x 6 tiles** of floor. Machines, by their `GymEquipmentDef.FootprintTiles` (width x depth):

| Equipment (id) | Tiles | Exercises it offers |
|---|---|---|
| `gym_equipment.squat_rack` | 2 x 2 | `activity.barbell_squat`, `activity.deadlift` |
| `gym_equipment.bench_press` | 1 x 2 | `activity.bench_press` |
| `gym_equipment.pull_up_bar` | 1 x 1 | `activity.pull_up` |
| `gym_equipment.grip_trainer` | 1 x 1 | `activity.grip_work` |
| `gym_equipment.treadmill` | 1 x 2 | `activity.treadmill_run` |
| `gym_equipment.rowing_machine` | 1 x 3 | `activity.rowing_machine`, `activity.seated_row` |
| `gym_equipment.skipping_rope` | 1 x 1 | `activity.skipping` |

Total machine area is 15 tiles; the rest is walking room and a bench by the door. Layout: the rack and
bench along the back wall (heavy, away from the door), treadmill and rower under the windows, pull-up
bar and grip trainer by the door, the skipping spot in the open middle.

## What training does

Each set or minute is reported through `IBodyService.ReportActivity(def, units)`: it raises hunger by
its kcal above rest (a workout makes the player hungry, never a number on screen) and trains the fitness
kinds the `ActivityDef` names. Strength grows slowly, holds cheaply, fades after a few days without, and
comes back several times faster up to the old peak (muscle memory). Tunables: `GameConfig.Strength`.

Interaction (later slice): walk up to a machine, press use, the character does a short looping set (the
rig's lift clip) and each repetition or minute is reported. No bar or meter: the player reads progress
from breathing, posture and how fast the character lifts.

## Membership, hours, staff

- **Membership:** free for the first visit, then a small weekly fee paid in person at the hall (in-person
  prices are one of the three places needs may act, per the 2026-11-14 rulings). Price is data on the
  economy side; until that lane adds it, the gym is free.
- **Hours:** 06:00 to 21:00 every day, closed on market day mornings (opening at 12:00). Hours are an
  `f(clock)` routine on the door, like every NPC routine.
- **Staff:** one villager runs it as part of their routine (opens, closes, takes the fee, says a cranky line
  if the player comes in starving). Two or three villagers train there on their own routines, so the room
  is lived in.

## Not in this plan

No scene edits, no art, no fee number, no NPC def. Each is its own small PR for its own lane.
