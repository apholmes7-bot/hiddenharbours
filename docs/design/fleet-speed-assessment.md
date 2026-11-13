# Fleet speed assessment

**Status: a proposal for the owner. No hull Def is changed by this document.**

The owner asked which boats should leave the water off a wave, then widened the question: assess the true speeds of the whole fleet. This is that assessment: what every hull makes today, what a real boat of her kind makes, a proposed game top speed, and what the proposed speed does to flight, crossing times, fuel, handling and NPC boats.

## Summary

- **Slow boats are already close to life; fast boats are not.** Rowed and displacement hulls sit at roughly half to all of a real boat's speed. Planing hulls sit at about a quarter. The whole fleet fits between 1.7 and 5.9 m/s, a spread of 3.5 times, where real boats of these kinds spread about 20 times.
- **The fastest boat today barely outruns a sprinting person** (5.89 m/s against the 5.5 m/s sprint).
- **No hull on main leaves the water at her true top speed in any sea.** Only the jetski, as intended at 5.5 m/s, hops.
- **Proposal: displacement and semi-displacement hulls keep today's speeds. Planing hulls rise to about 0.45 of a real boat's speed**, which is 1.5 to 1.8 times today for most of them. At those speeds the RIBs, the sport skiffs and the bowrider hop in a gale and a storm, rarely in a rough sea, and never in a lively one.
- **One scale factor does not fit both ends.** The jetski cannot go faster than 5.5 m/s without hopping in a lively sea, which the flight laws forbid. The go-fast needs about 16 m/s (31 knots) before she hops at all. Both need a ruling of their own (see "Decisions for the owner").

## How the numbers were made

- **Top speed today** is the fleet tests' own formula: `EnginePower x 0.01 / (ForwardDrag x 0.01 + MassKg / 100 x 0.2)`.
- **Real-boat speeds** are my judgement of typical top speeds for a boat of that kind and size, given as a range. None is a measured figure for a named model.
- **Hops** come from the flight law as pushed on PR #1104, on the shipped sea, heading into the waves for five minutes per cell, with each hull's own length, beam and seakeeping. The law here reads the **drawn waterline** (the hull mesh's resting draught), the one input change agreed for that PR. A cell is hops per minute; "none" is zero in five minutes. This is a calculation outside the editor, not play.
- 1 m/s is 1.94 knots.

## The fleet

Lobster boats come in 18 style and region variants; speed differs only by size, so there is one row per size. Sailboats are under auxiliary power. Proposed speeds marked "keep" are today's.

| Hull | Length (m) | Type | Today, m/s (knots) | Real boat (knots) | Proposed, m/s (knots) | Hops per minute at the proposed speed: lively / rough / gale / storm |
|---|---|---|---|---|---|---|
| Dory, rowed | 4.5 | displacement | 1.69 (3.3) | 2.5 to 3.5 | keep | none |
| Dory with outboard | 4.5 | displacement | 2.17 (4.2) | 5 to 6 | keep | none |
| Punt | 5.2 | displacement | 2.32 (4.5) | 8 to 15 | keep | none |
| Punt, upgraded | 5.2 | semi-planing | 2.89 (5.6) | 10 to 18 | keep | none |
| Console skiff | 7.0 | planing | 3.90 (7.6) | 25 to 35 | 7.0 (13.6) | 0 / 0 / 0 / 0.2 |
| Sport skiff | 7.0 | planing | 4.64 (9.0) | 30 to 40 | 8.0 (15.6) | 0 / 0 / 0.6 / 1.6 |
| Sport skiff Mk2 | 7.0 | planing | 5.19 (10.1) | 33 to 42 | 8.5 (16.5) | 0 / 0 / 1.2 / 2.2 |
| Sport skiff, twin | 7.0 | planing | 5.63 (10.9) | 40 to 50 | 9.0 (17.5) | 0 / 0.2 / 1.8 / 2.8 |
| Bowrider | 6.0 | planing | 5.08 (9.9) | 35 to 45 | 8.0 (15.6) | 0 / 0.2 / 1.2 / 2.4 |
| Yacht tender | 5.8 | planing | 4.75 (9.2) | 25 to 35 | 7.0 (13.6) | 0 / 0 / 0.2 / 1.4 |
| Jetski (as intended) | 3.3 | planing | 5.50 (10.7) | 45 to 60 | keep 5.5 | 0 / 0.2 / 0.4 / 1.0 |
| RIB, 6.7 m (FRC) | 6.7 | planing | 5.70 (11.1) | 30 to 40 | 8.5 (16.5) | 0 / 0.2 / 2.0 / 2.2 |
| RIB, 7.3 m (Hurricane) | 7.3 | planing | 5.89 (11.4) | 40 to 45 | 9.5 (18.5) | 0 / 0 / 1.4 / 3.0 |
| Go-fast | 12.4 | planing | 5.65 (11.0) | 60 to 80 | 16.0 (31.1), or 10.0 with no flight | 0 / 0 / 0.2 / 1.2 at 16.0 |
| Express cruiser | 10.2 | planing | 4.60 (8.9) | 25 to 32 | 6.5 (12.6) | none |
| Sport fisher, convertible | 16.2 | planing | 5.39 (10.5) | 28 to 35 | 7.0 (13.6) | none |
| Sport fisher, skybridge | 27.4 | planing | 4.98 (9.7) | 25 to 32 | 6.5 (12.6) | none |
| Lobster boat, inshore | 8.6 | semi-displacement | 3.89 (7.6) | 12 to 18 | keep | none |
| Lobster boat, standard | 12.0 | semi-displacement | 4.35 (8.5) | 12 to 20 | keep | none |
| Lobster boat, offshore | 14.6 | semi-displacement | 4.60 (8.9) | 12 to 20 | keep | none |
| Cape Islander | 12.9 | semi-displacement | 4.20 (8.2) | 8 to 14 | keep | none |
| Motor yacht 84 | 25.5 | semi-displacement | 4.98 (9.7) | 18 to 24 | keep | none |
| Expedition yacht 120 | 36.8 | displacement | 3.45 (6.7) | 12 to 14 | keep | none |
| Sloop, 30 ft, under power | 9.4 | displacement | 2.83 (5.5) | 5.5 to 6.5 | keep | none |
| Sloop, 88 ft, under power | 27.0 | displacement | 5.14 (10.0) | 9 to 11 | keep | none |
| Side dragger | 25.0 | displacement | 3.48 (6.8) | 9 to 11 | keep | none |
| Stern trawler (and Mk2) | 38.0 | displacement | 3.45 (6.7) | 11 to 13 | keep | none |
| Coastal packet (cargo) | 60.0 | displacement | 3.18 (6.2) | 11 to 13 | keep | none |
| Tanker | 110.0 | displacement | 2.61 (5.1) | 12 to 15 | keep | none |

Every hull at today's speed: no hops in any sea, except the jetski row as shown.

### The three named hulls in more detail

Hops per minute in lively / rough / gale / storm, with the highest keel clearance in a storm.

**Jetski (3.3 m)**

| Speed, m/s | Law reads the drawn waterline (0.30 m) | Law reads the gameplay draught (0.80 m) |
|---|---|---|
| 5.5 | 0 / 0.2 / 0.4 / 1.0 (0.35 m) | 0 / 0 / 0.2 / 0.2 (0.26 m) |
| 6.0 | 0.2 / 0.2 / 1.4 / 2.2 (0.84 m) | not measured |
| 6.5 | 0.2 / 0.6 / 2.0 / 3.8 (0.42 m) | 0 / 0 / 0.8 / 1.6 (0.32 m) |
| 7.5 | 0 / 1.4 / 4.4 / 6.4 (0.59 m) | 0 / 0.2 / 1.6 / 2.4 (0.51 m) |
| 8.5 | 1.8 / 4.6 / 8.8 / 11.0 (0.92 m) | 0.2 / 1.8 / 4.6 / 6.6 (0.83 m) |

On the drawn waterline she hops in a lively sea from 6.0 m/s up, so 5.5 m/s is her ceiling under the law "nothing flies in a lively sea or calmer".

**RIB, 6.7 m (FRC)**

| Speed, m/s | Drawn waterline (0.30 m) | Gameplay draught (0.80 m) |
|---|---|---|
| 5.7 (today) | none | none |
| 8.0 | 0 / 0 / 0.6 / 1.6 (0.27 m) | 0 / 0 / 0.2 / 0.6 (0.19 m) |
| 8.5 | 0 / 0.2 / 2.0 / 2.2 (0.33 m) | not measured |
| 9.0 | 0 / 0.2 / 2.2 / 2.6 (0.43 m) | 0 / 0 / 0.4 / 1.0 (0.36 m) |
| 10.0 | 0 / 0.8 / 3.4 / 6.4 (0.66 m) | 0 / 0.4 / 1.4 / 3.0 (0.56 m) |

**RIB, 7.3 m (Hurricane)**

| Speed, m/s | Drawn waterline (0.32 m) | Gameplay draught (0.85 m) |
|---|---|---|
| 5.9 (today) | none | none |
| 9.0 | 0 / 0 / 0.6 / 1.8 (0.30 m) | not measured |
| 9.5 | 0 / 0 / 1.4 / 3.0 (0.27 m) | 0 / 0 / 0.2 / 1.2 (0.18 m) |
| 10.0 | 0 / 0.4 / 2.6 / 4.2 (0.51 m) | 0 / 0 / 0.8 / 1.6 (0.40 m) |
| 11.0 | 0 / 0.4 / 4.6 / 7.8 (0.46 m) | 0 / 0 / 1.8 / 3.2 (0.37 m) |

**Go-fast (12.4 m)**

| Speed, m/s | Drawn waterline (0.52 m) | Gameplay draught (1.45 m) |
|---|---|---|
| 5.65 (today), 10, 12, 14 | none | none |
| 16.0 | 0 / 0 / 0.2 / 1.2 (0.12 m) | none |
| 18.0 | 0 / 0 / 1.8 / 6.2 (0.16 m) | not measured |

She is long enough to bridge the shipped waves, so she needs about three times her present speed before the sea drops away under her.

## What the proposed speeds do

### Crossing times

The world-scale design sizes a region by the time it takes to cross, not by metres. The two large regions are about 760 m across.

| Boat | Speed, m/s | Time to cross 760 m |
|---|---|---|
| Dory, rowed | 1.69 | 7 min 30 s |
| Cape Islander | 4.20 | 3 min 1 s |
| Fastest boat today (7.3 m RIB) | 5.89 | 2 min 9 s |
| Proposed sport skiff, bowrider | 8.0 | 1 min 35 s |
| Proposed 7.3 m RIB | 9.5 | 1 min 20 s |
| Go-fast at 16.0 | 16.0 | 48 s |

- At the proposed speeds a fast boat crosses a region in about a minute and a half instead of two. That is a real change but the region still reads as a place.
- **Real speeds would make the regions too small.** A real 40 knot RIB is 20 m/s and would cross a region in 37 seconds. The compressed distances need compressed speeds.
- **One scale factor for all planing hulls is the better law than hull-by-hull picks**, because it keeps the order of the fleet the same as in life and gives one dial to turn. About 0.45 of the real figure is the lowest factor at which the RIBs hop in a gale. It has two exceptions, both below.

### Fuel

- Each hull has a full-throttle burn in litres per hour and a tank size. Burn does not depend on speed, so a hull that goes 1.6 times faster on the same burn goes 1.6 times farther on a tank.
- To keep each boat's range in metres as it is today, her full-throttle burn should rise in step with her speed. To keep her endurance in minutes instead, leave it alone. That is a choice for the owner; it is one number per hull, in data.

### Handling and what else reads speed

- **Steering and trim** are untested above 6 m/s. Rudder authority, lateral drag and the planing trim numbers were tuned at today's speeds, and each sped-up hull needs a pass at the helm.
- **Bow spray art** is graded for 1.7 to 6 m/s. Above 6 m/s a hull gets no more spray than at 6, so the fast boats would look under-dressed until that range is widened.
- **The wake** lays deposits by distance, so a faster hull spends her particle budget faster. The pool sizes need checking at 9 to 16 m/s.
- **Fleet tests** hold today's numbers on purpose: the family speeds, the rule that the RIBs are the fastest things afloat and stay under 6 m/s, and the order of the hull swap list by speed. All of these change with the proposal. If the go-fast goes to 16 m/s she outruns both RIBs, as in life.
- **Grounding** is unchanged: gameplay draught is not touched by any of this.

### NPC boats

- The working boats seen around the regions do not use the hull speeds at all. The ambient fleet has its own speed band in data (1.6 to 2.4 m/s), so none of their routines or timings change.
- A future NPC jetski or RIB that should look fast would need its own band; nothing exists today that would break.

## Decisions for the owner

1. **Planing hulls at about 0.45 of real speed, displacement hulls unchanged: yes or no.** A lower factor (0.33) keeps every boat under 8 m/s but then only the jetski hops.
2. **The jetski.** She stays at 5.5 m/s under the law that nothing flies in a lively sea. If she should be faster, either that law gives way for her alone, or the flight law reads the gameplay draught for her (6.5 m/s then gives gale 0.8, storm 1.6, nothing lively or rough).
3. **The go-fast.** 16 m/s so she hops in a storm, or 10 m/s as the fastest boat afloat with no flight.
4. **Fuel:** keep range in metres (raise burn with speed) or keep endurance in minutes (leave burn alone).

## What this assessment does not show

- Nothing here was played. The hop rates are calculations on the shipped sea model, heading straight into the waves; other headings hop less.
- Real-boat speeds are judgement, not measurement.
- Handling at the proposed speeds has not been tried.
