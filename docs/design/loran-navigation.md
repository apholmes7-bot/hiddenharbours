# Loran-C — position before GPS

> Owner ruling: **"Loran before GPS: YES."** Helm rulings that bind it: instruments are diegetic
> objects on the dash, read like real instruments; **no helm text overlay**. A later, separate ask is
> an **autopilot** sold as a tool on recommended hulls; this slice does not build it but is shaped
> for it (§5).

## 1. What the player gets

A grey box in the wheelhouse with two rows of numbers — the **time differences (TDs)** between the
master station's pulse and two secondaries' — and a lamp. Lit steady: a good fix. **Blinking**: the
numbers are there and not to be trusted (fringe of range, lines crossing badly). Dark: no chain heard.

A skipper does not read a position off it. He reads **two numbers**, writes them in the back of the
logbook at a ground that fished, and later steers until the box shows the same two numbers again.
The set remembers ten sets of numbers ("WPT 01" .. "WPT 10") and gives bearing and distance from
the current fix to any of them. Numbers can also be keyed in by hand — another skipper's, an old
logbook's.

## 2. What is built (this slice)

| Piece | Where | Notes |
|---|---|---|
| Chain data | `LoranChainDef` (Core/Nav), assets in `Data/Boats/Loran/` | master + secondaries in world metres, coding delays, range, the error numbers |
| Receiver data | `LoranReceiverDef`, `StandardLoranReceiver.asset` | the chains it tracks, readout step, blink/no-fix limits, memory size, solver settings |
| Maths | `LoranMath` | position to TD, TD pair to position (Newton), geometry, deterministic error |
| Read model | `LoranReceiver.Read` → `LoranReading`; `LoranReceiver.Steer` → `LoranSteer` | pure, allocation-free |
| The numbers | `LoranWaypointStore`, `LoranWaypoint` | runtime only (§6) |
| Purchase | `LoranOffer.asset` (`instrument.loran`, an `InstrumentOffer`) | bolted into one hull, bought through `InstrumentShop` like the sounder; **listed at the St Peters general store (`seller.leblancs`), on the Gear shelf beside the sounder**, with its own `InstrumentShop` on `GeneralStoreCounter` (written by `StoreWiringPatch`). The fitment is recorded in the save's `InstrumentLocker` (`SaveData.HullInstruments`), which already persists, so owning it survives a reload with no save version step |
| Helm slot | `HelmConsoleDef.SupportsLoran` / `DefaultLoran`, `HelmFit.Loran` | resolved by `BoatEquipment.EffectiveFit`; **only `helm.cape_islander` (the 1982 wheelhouse) has room** |

Not built: the instrument view (§7), saving the numbers (§6), the world-frame mapping
from a region's scene frame (§3).

## 3. Chain layout

All positions are **world metres, +X east, +Y north, origin at the home bay** (St Peters' water).
The shared world frame (a per-region offset) is in flight; until it lands every region's centre is
(0, 0), so a home-water scene position and a world position coincide. The instrument host must map
the hull's region position through the world frame before calling `LoranReceiver.Read`; the Loran
never reads a region itself.

| Chain | GRI | Master | Secondaries (coding delay µs) | Range | Covers |
|---|---|---|---|---|---|
| `loran_chain.bay` | 5880 | (-6 000, 4 000) mainland west | X (7 000, 6 000) north shore, 11 000 · Y (2 000, -9 000) across the bay to the south, 26 000 | 30 km | the home bay, both shores, the run to East Point |
| `loran_chain.strait` | 7120 | (14 000, -12 000) toward New Scotland | W (-2 000, -16 000), 13 000 · X (22 000, 4 000), 29 000 | 40 km | the strait, the south water, the long sail south-east |
| `loran_chain.offshore` | 9360 | (28 000, -2 000) | Y (10 000, -34 000), 15 000 · Z (48 000, -30 000), 31 000 | 50 km | the planned deep-water grounds to the **south and east** (the Banks), roughly centred near (28 000, -23 000) inside the triangle |

Measured geometry (metres of position error per µs of TD error; best possible ≈ 212):
home bay (0, 0) on the bay chain ≈ 290; the deep grounds (30 000, -25 000) on the offshore chain
≈ 275; east (40 000, -10 000) ≈ 500. West of the bay master (-12 000, 2 000) the bay chain sits on
a baseline extension (≈ 6 250, refused) and the set falls back to the strait chain, blinking.
Far west and far offshore there is no signal.

The station spacing is game-scaled (real baselines were hundreds of km). The numbers still read
like the real thing because of the coding delays: a home-water TD is about **11 0xx.x** and
**26 0xx.x**.

## 4. The error model, and why

Every TD carries an error that is a **pure function of (worldSeed, position cell, game time
bucket)** — value noise hashed from those, smooth in space and time (rule 5; no `System.Random`):

- **Repeatable** part (bay 0.10 µs, strait 0.12, offshore 0.15): fixed at a spot for a given
  world. This is the point of Loran in a fishing game: the fix is a little off the chart, but **the
  same numbers bring you back to the same water**, so a skipper's numbers are worth more than the
  chart. Cell size 500 / 600 / 800 m.
- **Drifting** part (0.03 / 0.04 / 0.05 µs) that blends from one 6-game-hour bucket to the next: the
  numbers wander a little through a day, so a mark is "about there", never pixel-exact.
- **Range gain** `1 + 3 × f²` (f = farthest station / range): up to four times worse at the edge.
- The **geometry** carries TD error into position honestly: errors are applied to the TDs, and the
  hyperbolas spread far from the chain and blow up on a baseline extension. Nothing is painted on.
- The readout rounds to **0.1 µs** (the classic one-decimal display); a stored waypoint keeps
  exactly what was shown.

Typical result: tens of metres in the home bay, worse at the fringe; the lamp blinks past 85 % of
range or beyond 1 200 m/µs geometry, and a pair is refused past 5 000 m/µs. All of these numbers
are on the Def assets and owner-tunable.

## 5. How the autopilot will consume it

`LoranReading.FixPosition` (world metres, carrying the chain's error) is the autopilot's idea of
where the boat is; `LoranReceiver.TryResolve(receiver, waypoint, currentFix, out target)` gives the
point it steers at, and `LoranReceiver.Steer` already returns bearing and distance. An autopilot
that steers the bearing until the distance closes will arrive where the **numbers** say — on the
ground, as a real one did — not on the chart's idea of it. It should refuse to engage on
`LoranSignal.Blink` / `NoSignal`. Nothing here blocks a GPS autopilot either: it would read the
plotter's position through the same shape.

## 6. Saving the numbers — owed

**Waypoints do not survive a reload yet.** `LoranWaypointStore` is runtime-only by instruction
(save v22/v23 are in flight). When they persist they need one append-only per-hull list on
`SaveData`, e.g. `HullLoranWaypoints: List<LoranWaypointDto>` with plain fields `HullId`,
`Number` (int), `ChainId` (string), `SecondaryA`, `SecondaryB` (int), `TdA`, `TdB` (double), read
and written through a locker the way `InstrumentLocker` keeps per-hull instrument rows, and a
version step that starts it empty. Store TDs, never positions: a retune of the chain data must
not move a skipper's ground.

## 7. Art and rig work owed

There is no generic readout widget on the dash, so nothing is drawn yet. Owed:

1. **A Loran rig** (art director): the box — two rows of seven-segment or Nixie-style digits
   (`5 digits . 1 decimal`), the GRI and secondary letters, the signal lamp with a blink state, a
   waypoint number window, and a small keypad (SAVE / RCL / keyed entry). Night lighting like the
   other dash rigs.
2. **A mount on the Cape wheelhouse dash** (`CapeRig`): where the box sits, its footprint in the
   band layout, and a helm-band entry so it can be expanded.
3. **The renderer** (ui-ux), reading `LoranReading` + `LoranSteer` only; it never computes a fix.
4. **The instrument host** (Boats/App): read `Fit.Loran`, map the hull position through the world
   frame, call `LoranReceiver.Read` on the instrument tick, own one `LoranWaypointStore` per hull.
5. ~~A seller~~ done: Marguerite's counter at St Peters sells it (the only counter with an instrument
   seam today). A Nine Mile Creek chandlery or a coast-tech counter is the more natural home later;
   moving it there is a listing change plus an `InstrumentShop` on that counter (a scene step).
