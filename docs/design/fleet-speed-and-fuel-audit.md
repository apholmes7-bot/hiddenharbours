# Fleet speed and fuel audit

Snapshot: main `c9c9ef73dee59ee9b6e8c072ea98487992f82c2e`, 2026-11-12. **45 BoatHullDef assets, every stable id below, including the 18 lobster variants.** The ambient-fleet configuration is not a hull; proposed hulls on unmerged branches are outside this snapshot. No hull, existing Def number, scene, prefab or art changes are proposed here. P2 progression and P5 range decisions motivate the audit.

## What is and is not authored

**Main has no top-speed, measured-top-speed, cruise-speed, cruise-throttle, hull hydrodynamic-class or home-region field on BoatHullDef.** It carries engine thrust in design units, not horsepower or kW, plus linear drag and mass. Every row's authored top speed and cruise speed is therefore **MISSING**. The numeric speed column below is an **analytic still-water estimate**, not an editor measurement and not a proposed replacement speed. No Unity editor was run. Keep the four pending speed decisions with the owner.

Engine steady-speed estimate from `BoatController`: `v_top = EnginePower / (ForwardDrag + 0.2 * MassKg)` m/s. This includes `ForceFeelScale = 0.01`, body mass `MassKg/100`, and `HullLinearDamping = 0.2`; omitting body damping grossly overstates large-ship speed. The continuous balance omits fixed-step integration error, steering, waves, load, grounding, wind and current. Actual trial speeds must replace these estimates before a live speed/slip feature uses them. One knot = 1852/3600 m/s. Cruise for this audit alone is **0.7 throttle**, following the existing fuel design's example duty; because drive/drag are linear, estimated `v_cruise = 0.7 v_top`. It is not a newly ratified cruise setting.

Fuel numbers come directly from hull YAML and `Data/Config/GameConfig.asset`: `idle = 0.08`, `exponent = 2`, `BurnScale = 1`. Light/glass burn at the reference cruise is `R * (0.08 + 0.92 * 0.7^2) = 0.5308 R`; full throttle is `R`. `hours = capacity / burn`; `km = hours * v_mps * 3.6`. Hours are **real engine-running hours**, not game hours: `FuelBurn.SecondsPerHour = 3600`. No reserve is deducted; a navigable detour, full catch, bad sea or return journey reduces practical reach. Do not call the one-way distances a safe working radius.

The rowed dory's engine field is dormant; its speed estimate uses simultaneous maximum oar force `2 * OarPower` in the same resistance balance, an ideal continuous-pull envelope, **not** a human's stroke duty. Its cruise is undefined. The sloops' numeric speeds/ranges in the table refer only to **hypothetical auxiliary motoring** using their authored engine and burn data. Their actual sail performance reads reference polars: 30 ft hull-speed ceiling 7.41 kn, sail area 36.98 mÂ²; 88 ft ceiling 12.6 kn, sail area 349.29 mÂ². Both have `Status = Reference`, and neither ceiling is a measured sailing top speed. `BoatFuelTank.EngineIsTurning` currently accepts only `PropulsionType.Engine`; both `Sail` hulls currently spend **zero litres**, including the present auxiliary path. Thus the auxiliary hours below are authored-number budgets, not observed live depletion. Sailing with the engine stopped has no fuel-limited range; weather, supplies and stamina still matter.

No hull specifies engine make, horsepower, usable tank reserve, measured load condition, measured burn by RPM/speed, or a valid home-to-home voyage length. The exact real vessel represented is also not an authored link. Real classes below are **inferences from hull identity/dimensions/role**, with explicitly named comparators rather than an assertion that the game's engine matches a production installation. Real cruise/top figures vary by engine, payload and water conditions. A blank real figure means the cited public source does not publish it; no number has been invented to fill it.

## Every hull

`C/T` means cruise/top. Speeds are knots; dimensions are length/draught in metres; mass kg; `E` is authored EnginePower (design thrust units), `O` OarPower per oar. Tank is litres with authored grade. Burn C/T is L/h. Each `H?` means **home crossing cannot be classified because the hull has no home region**. `Wg` means its computed endurance exceeds a seven-day **game** week (7 Ã— 1800 s = 3.5 real hours); `Wr` would mean over seven real days (168 h). `AUX` budgets do not currently drain. Source letters link to the public-source register below. Ratios are analytic-game/real C/T; `?` denotes unavailable, `â‰¤` a real-top lower-bound denominator. There are no measured game/real ratios on main.

| Hull id | L / draught; kg | Authored drive | Est. speed C/T (authored: missing) | Tank L; grade | Authored burn R; computed C/T | Inferred real class; source | Real C/T kn | Est. game/real C/T | Cruise hours / km | Top hours / km | Flags |
|---|---:|---|---:|---|---:|---|---:|---:|---:|---:|---|
| `boat.bowrider` | 5.98 / 0.95; 800 | engine E 1600 | 6.91 / 9.87 | 60; gas | 105; 55.734 / 105 | Small bowrider [B](#source-b) | 27.2 / 42.1 | 0.25 / 0.23 | 1.077 / 13.78 | 0.571 / 10.45 | H? |
| `boat.cape_islander` | 12.9 / 1.4; 6000 | engine E 6300 | 5.71 / 8.16 | 90; diesel | 115; 61.042 / 115 | Cape Islander [C](#source-c) | 8 / 10 | 0.71 / 0.82 | 1.474 / 15.60 | 0.783 / 11.83 | H? |
| `boat.coastal_packet` | 60 / 5; 1244000 | engine E 871000 | 4.32 / 6.18 | 2000; diesel | 1250; 663.500 / 1250 | Coastal packet/freighter [F](#source-f) | 11 / 13 | 0.39 / 0.48 | 3.014 / 24.14 | 1.600 / 18.30 | H? |
| `boat.console_skiff` | 7 / 0.55; 1200 | engine E 1600 | 5.31 / 7.59 | 45; gas | 78; 41.402 / 78 | Planing fishing skiff [K](#source-k) | 18.6 / 36.9 | 0.29 / 0.21 | 1.087 / 10.69 | 0.577 / 8.11 | H? |
| `boat.dory` | 4.5 / 0.3; 400 | O 300; E 500 dormant | ? / 3.95 | 0; none | 0; 0.000 / 0 | Rowing dory [R](#source-r) | 3.3 / 4.6 | ? / 0.86 | unbounded by fuel | unbounded by fuel | H?; no fuel limit |
| `boat.dory_outboard` | 4.5 / 0.3; 400 | engine E 640 | 2.95 / 4.22 | 10; gas | 23; 12.208 / 23 | Small outboard dory [D](#source-d) | ? / 6.5 | ? / 0.65 | 0.819 / 4.48 | 0.435 / 3.40 | H? |
| `boat.expedition_yacht_120` | 36.75 / 2.75; 316000 | engine E 253000 | 4.69 / 6.70 | 700; diesel | 535; 283.978 / 535 | Displacement expedition yacht [X](#source-x) | 10 / 13 | 0.47 / 0.52 | 2.465 / 21.41 | 1.308 / 16.24 | H? |
| `boat.express_cruiser` | 10.2 / 1.6; 5000 | engine E 5980 | 6.26 / 8.94 | 90; gas | 155; 82.274 / 155 | Express cruiser [E](#source-e) | 30 / 45 | 0.21 / 0.20 | 1.094 / 12.68 | 0.581 / 9.62 | H? |
| `boat.go_fast` | 12.36 / 1.45; 4500 | engine E 6780 | 7.69 / 10.98 | 90; gas | 155; 82.274 / 155 | Offshore go-fast [G](#source-g) | 55.6 / 75.1 | 0.14 / 0.15 | 1.094 / 15.58 | 0.581 / 11.81 | H? |
| `boat.lobster_boat` | 12 / 1.3; 6800 | engine E 7300 | 5.91 / 8.45 | 85; diesel | 105; 55.734 / 105 | Standard lobster workboat [L](#source-l) | 8 / 10 | 0.74 / 0.84 | 1.525 / 16.70 | 0.810 / 12.66 | H? |
| `boat.lobster_inshore_hardtop_fundy` | 8.6 / 1.15; 2460 | engine E 2560 | 5.30 / 7.57 | 45; diesel | 69; 36.625 / 69 | Inshore lobster workboat [I](#source-i) | 15 / 20 | 0.35 / 0.38 | 1.229 / 12.06 | 0.652 / 9.15 | H? |
| `boat.lobster_inshore_hardtop_newfoundland` | 8.6 / 1.15; 2550 | engine E 2630 | 5.30 / 7.57 | 45; diesel | 69; 36.625 / 69 | Inshore lobster workboat [I](#source-i) | 15 / 20 | 0.35 / 0.38 | 1.229 / 12.06 | 0.652 / 9.15 | H? |
| `boat.lobster_inshore_hardtop_northumberland` | 8.6 / 1.15; 2500 | engine E 2590 | 5.30 / 7.57 | 45; diesel | 69; 36.625 / 69 | Inshore lobster workboat [I](#source-i) | 15 / 20 | 0.35 / 0.38 | 1.229 / 12.06 | 0.652 / 9.14 | H? |
| `boat.lobster_inshore_open_fundy` | 8.6 / 1.15; 2460 | engine E 2560 | 5.30 / 7.57 | 45; diesel | 69; 36.625 / 69 | Inshore lobster workboat [I](#source-i) | 15 / 20 | 0.35 / 0.38 | 1.229 / 12.06 | 0.652 / 9.15 | H? |
| `boat.lobster_inshore_open_newfoundland` | 8.6 / 1.15; 2550 | engine E 2630 | 5.30 / 7.57 | 45; diesel | 69; 36.625 / 69 | Inshore lobster workboat [I](#source-i) | 15 / 20 | 0.35 / 0.38 | 1.229 / 12.06 | 0.652 / 9.15 | H? |
| `boat.lobster_inshore_open_northumberland` | 8.6 / 1.15; 2500 | engine E 2590 | 5.30 / 7.57 | 45; diesel | 69; 36.625 / 69 | Inshore lobster workboat [I](#source-i) | 15 / 20 | 0.35 / 0.38 | 1.229 / 12.06 | 0.652 / 9.14 | H? |
| `boat.lobster_offshore_hardtop_fundy` | 14.6 / 1.45; 12000 | engine E 13220 | 6.26 / 8.94 | 160; diesel | 170; 90.236 / 170 | Offshore lobster workboat [O](#source-o) | 20 / 23 | 0.31 / 0.39 | 1.773 / 20.55 | 0.941 / 15.58 | H? |
| `boat.lobster_offshore_hardtop_newfoundland` | 14.6 / 1.45; 12470 | engine E 13660 | 6.26 / 8.94 | 160; diesel | 170; 90.236 / 170 | Offshore lobster workboat [O](#source-o) | 20 / 23 | 0.31 / 0.39 | 1.773 / 20.56 | 0.941 / 15.59 | H? |
| `boat.lobster_offshore_hardtop_northumberland` | 14.6 / 1.45; 12250 | engine E 13450 | 6.26 / 8.94 | 160; diesel | 170; 90.236 / 170 | Offshore lobster workboat [O](#source-o) | 20 / 23 | 0.31 / 0.39 | 1.773 / 20.55 | 0.941 / 15.58 | H? |
| `boat.lobster_offshore_open_fundy` | 14.6 / 1.45; 12000 | engine E 13220 | 6.26 / 8.94 | 160; diesel | 170; 90.236 / 170 | Offshore lobster workboat [O](#source-o) | 20 / 23 | 0.31 / 0.39 | 1.773 / 20.55 | 0.941 / 15.58 | H? |
| `boat.lobster_offshore_open_newfoundland` | 14.6 / 1.45; 12470 | engine E 13660 | 6.26 / 8.94 | 160; diesel | 170; 90.236 / 170 | Offshore lobster workboat [O](#source-o) | 20 / 23 | 0.31 / 0.39 | 1.773 / 20.56 | 0.941 / 15.59 | H? |
| `boat.lobster_offshore_open_northumberland` | 14.6 / 1.45; 12250 | engine E 13450 | 6.26 / 8.94 | 160; diesel | 170; 90.236 / 170 | Offshore lobster workboat [O](#source-o) | 20 / 23 | 0.31 / 0.39 | 1.773 / 20.55 | 0.941 / 15.58 | H? |
| `boat.lobster_standard_hardtop_fundy` | 12 / 1.3; 6680 | engine E 7200 | 5.92 / 8.45 | 85; diesel | 105; 55.734 / 105 | Standard lobster workboat [L](#source-l) | 8 / 10 | 0.74 / 0.85 | 1.525 / 16.71 | 0.810 / 12.67 | H? |
| `boat.lobster_standard_hardtop_newfoundland` | 12 / 1.3; 6920 | engine E 7400 | 5.91 / 8.44 | 85; diesel | 105; 55.734 / 105 | Standard lobster workboat [L](#source-l) | 8 / 10 | 0.74 / 0.84 | 1.525 / 16.69 | 0.810 / 12.66 | H? |
| `boat.lobster_standard_hardtop_northumberland` | 12 / 1.3; 6800 | engine E 7300 | 5.91 / 8.45 | 85; diesel | 105; 55.734 / 105 | Standard lobster workboat [L](#source-l) | 8 / 10 | 0.74 / 0.84 | 1.525 / 16.70 | 0.810 / 12.66 | H? |
| `boat.lobster_standard_open_fundy` | 12 / 1.3; 6680 | engine E 7200 | 5.92 / 8.45 | 85; diesel | 105; 55.734 / 105 | Standard lobster workboat [L](#source-l) | 8 / 10 | 0.74 / 0.85 | 1.525 / 16.71 | 0.810 / 12.67 | H? |
| `boat.lobster_standard_open_newfoundland` | 12 / 1.3; 6920 | engine E 7400 | 5.91 / 8.44 | 85; diesel | 105; 55.734 / 105 | Standard lobster workboat [L](#source-l) | 8 / 10 | 0.74 / 0.84 | 1.525 / 16.69 | 0.810 / 12.66 | H? |
| `boat.lobster_standard_open_northumberland` | 12 / 1.3; 6800 | engine E 7300 | 5.91 / 8.45 | 85; diesel | 105; 55.734 / 105 | Standard lobster workboat [L](#source-l) | 8 / 10 | 0.74 / 0.84 | 1.525 / 16.70 | 0.810 / 12.66 | H? |
| `boat.motor_yacht_84` | 25.5 / 1.8; 72000 | engine E 79930 | 6.78 / 9.68 | 900; diesel | 885; 469.758 / 885 | Planing motor yacht [M](#source-m) | 22 / 27 | 0.31 / 0.36 | 1.916 / 24.04 | 1.017 / 18.23 | H? |
| `boat.punt` | 5.2 / 0.5; 700 | engine E 650 | 3.16 / 4.51 | 25; gas | 49; 26.009 / 49 | Open outboard punt [P](#source-p) | 18.3 / 29.5 | 0.17 / 0.15 | 0.961 / 5.62 | 0.510 / 4.26 | H? |
| `boat.punt_upgraded` | 5.2 / 0.55; 725 | engine E 825 | 3.94 / 5.63 | 25; gas | 49; 26.009 / 49 | Open outboard punt [P](#source-p) | 18.3 / 29.5 | 0.22 / 0.19 | 0.961 / 7.01 | 0.510 / 5.32 | H? |
| `boat.side_dragger` | 25 / 2.9; 90000 | engine E 78000 | 4.74 / 6.77 | 320; diesel | 275; 145.970 / 275 | Displacement fishing trawler [T](#source-t) | 9 / 9.3 | 0.53 / 0.73 | 2.192 / 19.24 | 1.164 / 14.59 | H? |
| `boat.sloop_30` | 9.4 / 1.9; 4875 | sail + aux E 3325 | 3.85 / 5.50 | 60; diesel | 3.5; 1.858 / 3.5 | 30 ft cruising sloop (auxiliary) [A](#source-a) | 5.5 / 6 | 0.70 / â‰¤0.92 | 32.296 / 230.31 | 17.143 / 174.64 | H?; AUX; Wg(C/T) |
| `boat.sloop_88` | 27 / 4.6; 89190 | sail + aux E 96300 | 6.99 / 9.99 | 1200; diesel | 45; 23.886 / 45 | 88 ft cruising sloop (auxiliary) [Y](#source-y) | 8.5 / 10 | 0.82 / 1.00 | 50.239 / 650.64 | 26.667 / 493.37 | H?; AUX; Wg(C/T) |
| `boat.sport_fisher_convertible` | 16.2 / 1.75; 15000 | engine E 19300 | 7.34 / 10.48 | 320; diesel | 340; 180.472 / 340 | Convertible sportfisher [V](#source-v) | 35 / 40 | 0.21 / â‰¤0.26 | 1.773 / 24.09 | 0.941 / 18.27 | H? |
| `boat.sport_fisher_skybridge` | 27.4 / 2.95; 72000 | engine E 79930 | 6.78 / 9.68 | 900; diesel | 885; 469.758 / 885 | Skybridge sportfisher [S](#source-s) | 32.5 / 38 | 0.21 / â‰¤0.25 | 1.916 / 24.04 | 1.017 / 18.23 | H? |
| `boat.sport_skiff` | 7 / 0.5; 950 | engine E 1600 | 6.31 / 9.01 | 60; gas | 105; 55.734 / 105 | Planing fishing skiff [K](#source-k) | 18.6 / 36.9 | 0.34 / 0.24 | 1.077 / 12.58 | 0.571 / 9.54 | H? |
| `boat.sport_skiff_mk2` | 7 / 0.95; 1150 | engine E 2050 | 7.06 / 10.09 | 70; gas | 120; 63.696 / 120 | Planing fishing skiff [K](#source-k) | 18.6 / 36.9 | 0.38 / 0.27 | 1.099 / 14.37 | 0.583 / 10.90 | H? |
| `boat.sport_skiff_twin` | 7 / 0.55; 1000 | engine E 2000 | 7.67 / 10.95 | 90; gas | 155; 82.274 / 155 | Planing fishing skiff [K](#source-k) | 18.6 / 36.9 | 0.41 / 0.30 | 1.094 / 15.53 | 0.581 / 11.78 | H? |
| `boat.stern_trawler` | 38 / 4.2; 316000 | engine E 253000 | 4.69 / 6.70 | 700; diesel | 535; 283.978 / 535 | Displacement fishing trawler [T](#source-t) | 9 / 9.3 | 0.52 / 0.72 | 2.465 / 21.41 | 1.308 / 16.24 | H? |
| `boat.stern_trawler_mk2` | 38 / 4.3; 330000 | engine E 264000 | 4.71 / 6.73 | 750; diesel | 575; 305.210 / 575 | Displacement fishing trawler [T](#source-t) | 9 / 9.3 | 0.52 / 0.72 | 2.457 / 21.45 | 1.304 / 16.27 | H? |
| `boat.tanker` | 110 / 6.5; 7668000 | engine E 4217000 | 3.54 / 5.06 | 9000; diesel | 4425; 2348.790 / 4425 | Coastal product tanker [N](#source-n) | 12 / ? | 0.30 / ? | 3.832 / 25.15 | 2.034 / 19.07 | H?; Wg(C) |
| `boat.yacht_tender` | 5.82 / 0.8; 1100 | engine E 1710 | 6.46 / 9.23 | 55; gas | 95; 50.426 / 95 | Planing RIB/tender [Z](#source-z) | 17.7 / 43.8 | 0.37 / 0.21 | 1.091 / 13.06 | 0.579 / 9.90 | H? |
| `boat.zodiac_frc` | 6.66 / 0.8; 800 | engine E 1710 | 7.76 / 11.08 | 55; gas | 95; 50.426 / 95 | Planing RIB/tender [Z](#source-z) | 17.7 / 43.8 | 0.44 / 0.25 | 1.091 / 15.67 | 0.579 / 11.88 | H? |
| `boat.zodiac_hurricane` | 7.28 / 0.85; 1050 | engine E 2210 | 8.02 / 11.46 | 75; gas | 130; 69.004 / 130 | Planing RIB/tender [Z](#source-z) | 17.7 / 43.8 | 0.45 / 0.26 | 1.087 / 16.14 | 0.577 / 12.24 | H? |

## Crossing and week flags

No hull has an authored home assignment; **all 45 carry H?**, so a definitive per-home crossing claim is unavailable. Regional names in lobster ids are boat-building traditions (Fundy/Newfoundland/Northumberland), not RegionDef references. Do not silently treat them as home ports.

As an explicit **straight-water span check only**, the serialized region rectangles have these diagonals: St Peters, East Water and West Water 0.921 km; Nine Mile Creek and Cumberland 0.944 km; Rice Point 0.841 km; South Water 2.683 km. Coddle Cove has no serialized `WorldSizeMeters` key in its asset and cannot supply an authored span. These diagonals bound the straight distance between any two points in a rectangle; they do not bound a navigable detour or region-to-region voyage. **None of the 44 fuel-budgeted hulls falls below even the largest 2.683 km span at either audit duty.** This is not a claim that every offshore boat can complete its home crossing. A home port, sea route, return leg and fuel reserve must be authored to answer that gameplay question.

No finite fueled endurance exceeds **168 real hours** at either duty. The game-week flags are: `boat.tanker` at cruise (3.5 h threshold); both sloops at cruise and top on their auxiliary budgets. The rowed dory and both actual sailing hulls have no live fuel-limited endurance, so they are separately flagged rather than presented as a finite week-long voyage. Idle loitering is outside these cruise/top flags.

## Findings and next decisions

- The fleet's speed ladder is compressed relative to the cited planing-boat trials; the largest engine hulls are particularly governed by mass-scaled body damping. A force/drag calculation cannot settle the owner's four pending speed decisions. A headless lane has no editor lease to take a still-water trial.
- Existing fuel rates are game balance numbers. Their very short real endurance is consistent with a 30-minute game day and is not a claim about real marine fuel efficiency. Changing the day length does not alter those rates. Directly copying real tankage/burn would change gameplay reach by orders of magnitude.
- The current curve rises with **throttle**, not measured speed. A prospective speed curve can preserve the existing 0.7-throttle budget at a supplied reference cruise speed without editing any hull speed, any existing fuel number or the live meter. It must distinguish through-water engine-driven speed from current-driven ground speed, engine-off sailing and temporary acceleration.
- Measure each hull's full and cruise still-water speeds, name its load/sea state, and resolve its home route before integrating a speed curve. The auxiliary metering gap and matched 38 m trawler/ocean tanker reference trials need separate owner/module review. No integration, speed adjustment or missing-reference guess is approved here.

## Public-source register

Sources were consulted on 2026-11-12. All numbers are paraphrased; MPH conversions use 1609.344/1852 knots per mph and round to 0.1 kn. A selected efficient cruise row in a trial table is an audit choice, not a manufacturer mandate. The source conditions and comparator limitations below are part of each fleet row.

<a id="source-r"></a>**R â€” Rowing dory.** [Rowing dory public specification or firsthand report](https://forum.clcboats.com/t/boat-speed-easy-pace-and-sprint-pace/47163). Firsthand GPS report: easy 3.7â€“3.9 mph, sprint 5.3 mph; converted to knots. Lightweight 5.2 m recreational dory, not the laden game hull.

<a id="source-d"></a>**D â€” Small outboard dory.** [Small outboard dory public specification or firsthand report](https://www.fyneboatkits.co.uk/kits/rowing/southwester-dory/). Builder: a 2 hp outboard reaches about 6.5 kn hull speed. Cruise not published. A longer 5.7 m wooden dory; this is a ceiling comparator, not a speed claim for the 4.5 m game hull.

<a id="source-p"></a>**P â€” Open outboard punt.** [Open outboard punt public specification or firsthand report](https://yamahaoutboards.com/getmedia/98515365-2e9e-4238-b980-6d988277511c/pb_g3b_anglerv16f_f70la_2016-07-15_alm_1). Engine-maker trial of a 16 ft 4 in G3 Angler V16 F with F70: selected efficient cruise at 4000 rpm, 21.1 mph; top 33.9 mph. More power than a traditional work punt; benchmark only.

<a id="source-k"></a>**K â€” Planing fishing skiff.** [Planing fishing skiff public specification or firsthand report](https://yamahaoutboards.com/getmedia/22da366e-e87e-44ae-82f1-befcb0562250/pb_csk_21seaskiff_f115xa_2013-11-12_bay). Engine-maker Sea Skiff 21/F115 trial: selected planing cruise 3500 rpm, 21.4 mph, and top 42.5 mph. A 6.4 m comparator, not proof that every skiff has this engine.

<a id="source-b"></a>**B â€” Small bowrider.** [Small bowrider public specification or firsthand report](https://boattest.com/boats/yamaha/195s-2020). Firsthand sea trial, 5.92 m Yamaha 195S: planing cruise row at 6000 rpm 27.2 kn and top 42.1 kn. Jet drive differs from the game engine abstraction.

<a id="source-i"></a>**I â€” Inshore lobster workboat.** [Inshore lobster workboat public specification or firsthand report](https://uk.boats.com/power-boats/2009-custom-alley-boats-28-downeast-9386316/). Broker-authored Alley 28 Downeast listing reports 15/20 kn. It is one 8.84 m diesel installation, not a regional universal. Structured fields say 15/20 kn; prose says 16.6/22 mph (14.4/19.1 kn), so the source is internally inconsistent and this anchor is provisional.

<a id="source-l"></a>**L â€” Standard lobster workboat.** [Standard lobster workboat public specification or firsthand report](https://trinav.com/boats/boat-details/106330/?file_number=11211NS). Marine broker listing of a 38 ft 6 in working fiberglass lobster boat reports 8 kn cruise and 10 kn top. Deliberately compares working boats, not lobster-styled speed yachts.

<a id="source-o"></a>**O â€” Offshore lobster workboat.** [Offshore lobster workboat public specification or firsthand report](https://oceansupportvessels.com/46-ft-wesmac/). Operator specification of a 46 ft Wesmac, 800 hp diesel, reports 20/23 speed entries. Interpreted as knots in this vessel-specification context; units are not explicitly printed beside the two entries. Treat this benchmark as provisional.

<a id="source-c"></a>**C â€” Cape Islander.** [Cape Islander public specification or firsthand report](https://www.atlanticcanadaboats.com/boats/capesable). Listing broker of the 38 ft Cape Sable, based on Cape Islander workboats, reports 8 kn cruise and 10 kn top with Yanmar 110. Builder prose elsewhere calls 10 kn cruising, so this installation is not a universal class speed. Pleasure conversion and length differ from the 12.9 m workboat.

<a id="source-t"></a>**T â€” Displacement fishing trawler.** [Displacement fishing trawler public specification or firsthand report](https://media.damen.com/image/upload/v1680772503/catalogue/fishing/shrimp-trawler/2607/shrimp-trawler-2607-specification.pdf). Builder specification of the 26.45 m Shrimp Trawler 2607: 9 kn service, 9.3 kn trial at stated draught/power. Close size for side dragger; smaller than the two 38 m stern trawlers. Stern comparison is a broad family anchor only, pending a matched 38 m trial.

<a id="source-f"></a>**F â€” Coastal packet/freighter.** [Coastal packet/freighter public specification or firsthand report](https://www.history.uscg.mil/Browse-by-Topic/Assets/Water/All/Article/2521718/north-star-1941-wpg-59/). Operator historical record of 68.6 m North Star: 11 kn cruise, 13 kn max. A diesel coastal supply vessel/cutter rather than an exact 60 m hull; useful packet-duty comparator.

<a id="source-n"></a>**N â€” Coastal product tanker.** [Coastal product tanker public specification or firsthand report](https://thuntankers.com/wp-content/uploads/2013/03/erikthunlng.pdf). Operator concept specification: 115 m product tanker, speed 12 kn. Used as a service-speed anchor; document does not distinguish service from maximum or supply a second speed. No maximum ratio claimed.

<a id="source-e"></a>**E â€” Express cruiser.** [Express cruiser public specification or firsthand report](https://coastalcraft.com/wp-content/uploads/2021/01/33_ExpressFish_Specs_Jan2021_v3.pdf). Builder 33 ExpressFish specification: 30 kn cruise at 5000 rpm, 45 kn maximum. The same sheet also advertises 35 kn cruise; use the explicitly RPM-qualified 30 kn. Outboard fishing express, not exact leisure cruiser.

<a id="source-g"></a>**G â€” Offshore go-fast.** [Offshore go-fast public specification or firsthand report](https://www.boats.com/reviews/powerboat-performance-report-cigarette-38-top-gun/). Firsthand radar trial of Cigarette 38 Top Gun: cruise 64 mph at 4000 rpm, top 86.4 mph, converted to knots.

<a id="source-m"></a>**M â€” Planing motor yacht.** [Planing motor yacht public specification or firsthand report](https://iyc.com/yachts/princess85/). Listing broker specification for a 25.91 m Princess 85: cruise 22 kn, maximum 27 kn. Engine option and loading matter.

<a id="source-x"></a>**X â€” Displacement expedition yacht.** [Displacement expedition yacht public specification or firsthand report](https://yachtsinternational.com/yachts/nordhavn-120-aurora/). Firsthand delivery review of Nordhavn 120: 10 kn cruising, 13 kn maximum. Builder-hosted Circumnavigator also reports 10â€“12 kn cruise: https://nordhavn.com/wp-content/uploads/2021/02/CircumnavigatorV.pdf.

<a id="source-v"></a>**V â€” Convertible sportfisher.** [Convertible sportfisher public specification or firsthand report](https://www.vikingyachts.com/54C.asp). Builder Viking 54: mid-30 kn cruise, over 40 kn top with optional engines. Table uses 35 and 40 as rounded comparison anchors, not exact trial endpoints; top ratio is an upper bound.

<a id="source-s"></a>**S â€” Skybridge sportfisher.** [Skybridge sportfisher public specification or firsthand report](https://www.vikingyachts.com/90C.asp). Builder Viking 90: 32â€“33 kn cruising, over 38 kn maximum for hull 1 optional engines. Table uses cruise midpoint 32.5 and top lower bound 38; top ratio is an upper bound.

<a id="source-a"></a>**A â€” 30 ft cruising sloop (auxiliary).** [30 ft cruising sloop (auxiliary) public specification or firsthand report](https://forums.sailboatowners.com/threads/yanmar-diesel-what-should-i-expect.19793/). Firsthand Catalina 30 owner report: clean-bottom auxiliary cruise 5.5 kn at two-thirds throttle and over 6 kn maximum. Lower bound 6 used for the ratio; not a sailing maximum.

<a id="source-y"></a>**Y â€” 88 ft cruising sloop (auxiliary).** [88 ft cruising sloop (auxiliary) public specification or firsthand report](https://www.simpsonsuperyachts.com/storage/pdfs/Karibu-334371.pdf). Broker specification of Oyster 885: cruise 8.5 kn, maximum 10 kn. Engine performance comparator. Under sail the designer separately observed 9.5â€“10 kn upwind in 12â€“14 kn true wind: https://www.humphreysdesign.com/news/oyster-885-sea-trials/. Sailing has no single unconditional top speed.

<a id="source-z"></a>**Z â€” Planing RIB/tender.** [Planing RIB/tender public specification or firsthand report](https://yamahaoutboards.com/getmedia/ca04e94f-0c9f-4419-b957-463ed9a7af4b/pb_zod_recpro_650_f150xb_11-13-2018_inf). Engine-maker trial: Zodiac Rec Pro 650/F150, selected efficient planing cruise at 3000 rpm 20.4 mph, top 50.4 mph. Same broad RIB family only; not a Hurricane military load trial.

## Applied 2026-11-13: the ruled speed table

The owner ruled on the four speed decisions on 2026-11-12: planing hulls run at **0.45 x real-world top speed**; the jetski stays **5.5 m/s** and does not fly; the go-fast is **10 m/s**, no flight; fuel burn rises with speed (the PR 1134 model, unchanged). The table is applied as data: each planing hull's `EnginePower` was solved for her ruled speed as `speed x (ForwardDrag + 0.2 x MassKg)`, with mass, drag, rudder and every fuel number left alone. "Planing" means the 14 hulls bound to `fuelcurve.planing`. The other 32 hulls keep the speeds in the table above. `FleetSpeedTableTests` holds every hull Def to her row.

| Hull id | Real top kn | Old top m/s (kn) | Ruled top m/s (kn) | EnginePower old -> new |
|---|---:|---:|---:|---:|
| `boat.bowrider` | 42.1 | 5.08 (9.87) | 9.75 (18.95) | 1600 -> 3070 |
| `boat.console_skiff` | 36.9 | 3.90 (7.59) | 8.54 (16.61) | 1600 -> 3502 |
| `boat.sport_skiff` | 36.9 | 4.64 (9.01) | 8.54 (16.61) | 1600 -> 2947 |
| `boat.sport_skiff_mk2` | 36.9 | 5.19 (10.09) | 8.54 (16.61) | 2050 -> 3374 |
| `boat.sport_skiff_twin` | 36.9 | 5.63 (10.95) | 8.54 (16.61) | 2000 -> 3033 |
| `boat.express_cruiser` | 45 | 4.60 (8.94) | 10.42 (20.25) | 5980 -> 13543 |
| `boat.motor_yacht_84` | 27 | 4.98 (9.68) | 6.25 (12.15) | 79930 -> 100321 |
| `boat.sport_fisher_convertible` | 40 | 5.39 (10.48) | 9.26 (18.00) | 19300 -> 33151 |
| `boat.sport_fisher_skybridge` | 38 | 4.98 (9.68) | 8.80 (17.10) | 79930 -> 141192 |
| `boat.yacht_tender` | 43.8 | 4.75 (9.23) | 10.14 (19.71) | 1710 -> 3650 |
| `boat.zodiac_frc` | 43.8 | 5.70 (11.08) | 10.14 (19.71) | 1710 -> 3042 |
| `boat.zodiac_hurricane` | 43.8 | 5.89 (11.46) | 10.14 (19.71) | 2210 -> 3802 |
| `boat.go_fast` | 75.1 | 5.65 (10.98) | 10.00 (19.44), ruled outright | 6780 -> 12000 |
| `boat.jet_ski` | not in the audit | 5.50 (10.69) | 5.50 (10.69), ruled outright | 715, unchanged |

What strict application leaves open, for the owner:

- The audit gives one real figure per boat family, so the four 7 m skiffs now share one top speed, and so do the two Zodiacs and the yacht tender. The ladder between them is now mass, hold, seakeeping and pickup only.
- At 0.45 x real, the express cruiser (10.42) and the three RIBs (10.14) are a shade quicker than the go-fast's ruled 10 m/s, and the jetski's ruled 5.5 m/s is the slowest planing hull.
- The bow spray art is framed for 1.7 to 6 m/s. Above 6 m/s it reads flat out; re-framing it is an art follow-up.
- The live fuel meter still charges by throttle. A planing hull burns the same litres per hour at full throttle as before and now covers more water on them, until the PR 1134 speed model is wired in with measured cruise speeds.
- These are still-water speeds off the force model. No editor trial was run.
