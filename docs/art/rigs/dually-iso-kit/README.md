# Hidden Harbours — Dually 3500 Iso Kit

The first **road vehicle** in the harbour: a crew-cab one-tonne dually pickup, six wheels, long box,
flared rear fenders — **8 facings × 10 paints × every panel on a hinge**. Same turntable, same camera,
same shading recipe as the campers, the houses and the fleet, so it parks next to them without a reskin.

`vehicleIsoRig.js` is a **catalogue rig, not one truck**. Its `BODIES` table holds a single entry today
(`dually3500`); cars, SUVs, cube vans and semis are meant to land as further entries rendered through
this same bake. Everything in this kit that says *Dually* is one row of that table.

Conventions (ADR-0006 bake): **32 px = 1 m**, ¾ camera in 45° steps at **elev 40°**, flat-facet shading
from the fixed upper-LEFT key, z-buffered, ordered dither, depth-edge darkening, **no AA**, binary alpha,
**ringless** per ADR-0031 (`{outline:true}` is kept as the live A/B).

## Facing 0 shows the tailgate

`order` is `N NE E SE S SW W NW` and it names **where the truck is pointed**, not what you are looking at.
At **N** the nose points away and you see the tail; at **S** you see the grille. The driver's side is
`-x` — **street side**, doors `FL`/`RL` — and it reads at **NE E SE**; the curb side (`FR`/`RR`) reads at
**SW W NW**. Get this backwards and every truck in the scene drives in reverse.

Cell is **384 × 320**, pivot **192,214**, identical in all eight facings: model `z = 0` at the body's
ground-centre projects to the pivot row, so a truck at full suspension travel still sits on the road.
Measured painted union is **218 × 162 px** at rest and **224 × 174 px** with the hood up, the gate down
and all four doors open — crop and pack from `painted_bbox` in the contract, per facing, not from the cell.

## Files

| File | What it is |
| --- | --- |
| `vehicleIsoRig.js` → `globalThis.VehicleIso` | The rig: geometry, ramps, articulation, bake. Self-contained — **no `isoSolid.js`**, this one lathes its own faces. |
| `vehicleIsoRig.dually3500.gameplay.json` | The gameplay sidecar — cab, cargo bed, four door thresholds, running boards, tow points, suspension correction, wheels, interactables, and the absences. |
| `dually3500.contract.json` | Every number in this kit, machine-readable: bake + camera, dims, per-facing measured painted bbox (at rest and wide open), the articulation table with its degree ranges, paints, presets, cues, 13 named anchors × 8 facings, and the sheet manifest. |
| `harness.html` | Standalone bake + assert harness. Open it in a browser: no build, no deps, no project. |
| `Dually3500_white_8dir.png` | 8 × (384 × 320) — the workhorse build (white, weather 0.45) at rest, `N NE E SE S SW W NW`. |
| `Dually3500_doors_W.png` | 8 × (384 × 320) — the doors cue 0 → 62° at facing W, all four leaves in view. |
| `Dually3500_gate_NE.png` | 8 × (384 × 320) — the tailgate cue 0 → 92° at facing NE. |
| `Dually3500_roll_W.png` | 10 × (384 × 320) — the `bounce` cue at W: one wheel revolution plus suspension, **cyclic** (frame 10 wraps to frame 0). |
| `Dually3500_paints_SE.png` | 5 × 2 grid — the ten harbour paints at SE, weather 0.22. |

Pivot is pinned identically in every cell of every sheet.

## Load order

```html
<script src="vehicleIsoRig.js"></script>   <!-- that's all of it -->
```

## Everything on this truck moves

`render(dir, opts)` takes the pose. No baked animation states, no separate rigs per pose:

| Param | Range | What it does |
| --- | --- | --- |
| `dFL dFR dRL dRR` | 0..1 | four doors, hinged on their **forward** edge, 0 → **62°**. Tow mirrors ride the front pair and swing with them. |
| `hood` | 0..1 | hinged at the cowl, 0 → **46°**. There is a real engine bay underneath — block, intake, battery. |
| `gate` | 0..1 | tailgate, hinged at its bottom edge, 0 → **92°**: it drops a whisker past level. |
| `roll` | revolutions | master wheel roll. The loop closes on **one revolution** to within a tread phase — the hub lugs and the index notch return exactly, but the tread advances 2.639 m over a 0.105 m stripe period (25.13 stripes), so `roll:1` differs from `roll:0` by 43 px of 14,800 painted (**0.29%**, no alpha change). Seamless in motion; not bit-identical. |
| `wFL wFR wRL wRR` | revolutions | per-wheel offsets. The wheels turn **independently**; an index notch on each hub gives the loop a full-revolution period rather than a 45° one. |
| `susF susR` | −1..1 | suspension travel, 0.09 m front / 0.11 m rear. **The body moves, the wheels do not** — and the drop extrapolates past both axles, so the bumpers pitch. That is what sells it. |
| `steps` | bool | running boards. **Defaults off** — see the sidecar's `_confirm`. |
| `mirrors` `mudflaps` `hitch` | bool | fitted parts, default on. |
| `night` | bool | glass ramps swap, headlamps become glow, light spills one pixel onto its neighbours. |
| `weather` | 0..1 | greys and grimes the paint, rusts the running gear, speckles. Default 0.32; the workhorse sheet is 0.45. |

`frames(dir, n, opts, cue)` bakes a strip through one of five named cues — `doors` `hood` `gate` `roll`
`bounce`. `roll` and `bounce` are cyclic; the other three run 0 → 1 inclusive. Five `PRESETS`
(`showroom` `workhorse` `farmGate` `serviceBay` `crewCall`) are whole builds, not just paint.

## The sidecar is the gameplay layer, and it is not a boat's

Same frame convention as the hulls and the campers — metres, 32 px = 1 m, heading-independent, `+x` curb,
`+y` nose, `+z` up, origin at the **ground-centre of the body footprint** — but the sections are a
vehicle's, and two of them exist nowhere else in `Art/gameplay/`:

- **`CAB`** — a **seated interior, not a walkable room**. Floor polygon at z 0.80, liner walls, the helm
  on the street side (left-hand drive, read off the wheel's own tube), two buckets and a rear bench with
  seat reference points, and the console and dash as obstructions with heights above the floor.
- **`CARGO`** — the bed: a 1.76 × 1.90 m rubber sole **0.95 m off the road**, 0.51 m walls, and the two
  wheel tubs as 0.29 m step-overs that pinch it to a **1.12 m aisle** for 1.10 m of its length. The
  tailgate open is published as what it actually is — a **0.46 m level ledge at z ≈ 0.95**, 0.94 m up
  from the ground, not a ramp.
- **`THRESHOLD`** — four doors, each with clear width (1.20 m front, 1.22 m rear), a 1.20 m clear height
  over a **0.70 m step-in**, the **vertical hinge axis on the forward edge**, and the swept `keep_clear`
  arc sampled at the sill. The collider is the arc, not the leaf: a front door reaches **|x| 2.07 m**,
  a foot outboard of the flares.
- **`STEP`** — the running boards, marked `present_when: {steps:true}` because the rig defaults them off.
  With them the step-in breaks into 0.55 + 0.15.
- **`TOW`** — receiver and ball (top at z 0.60) aft, two recovery hooks forward. Not cleats: nothing here
  is a mooring, and the section says so.
- **`SUSPENSION`** — the exact `dz(y)` the bake applies, verbatim, because **every polygon in the file
  rides it**. A rider in a seat or a crate in the bed needs the same correction.
- **`WHEELS`** — six, with roles (`steer`, `dual_inner`, `dual_outer`), tracks and radius.
- **`ATTACH`** / **`INTERACT`** — exhaust tip, fuel tank, engine bay, clearance lamps; six interactables
  with reach points and `visible_facings`.

`visible_facings` is **computed, not eyeballed**: a feature with outward normal *n* is listed for facing
*d* when `nx·sin(45d) + ny·cos(45d) < −0.3` — the same near-side test the rasteriser uses, edge-on
excluded. So the tailgate reads at `N NE NW`, the grille at `SE S SW`, and top faces at all eight.
`reach_point` is a **request, not a promise** (same warning as the camper's): it is a ground-level
standing spot, clear of the door arcs by measurement, but untested against terrain.

`derivedFromRigSha256` is `130580238ab2c6d4…` — the SHA-256 of `vehicleIsoRig.js` **as shipped in this
kit**, and the drift tripwire. Nobody types it; the harness re-hashes the file and fails the `sha` group
if the two disagree. The same sidecar sits byte-identical in the art workspace at
`Art/gameplay/vehicleIsoRig.dually3500.gameplay.json`.

## The harness asserts, it does not just draw

`harness.html` bakes all eight facings twice — at rest and wide open — and checks: the origin projects to
the published pivot in every facing; nothing paints outside its cell or more than 62 px below the pivot;
each of the five cues actually moves pixels; the one-revolution roll seam stays under 0.6% of painted
pixels; compressing both axles lowers the roof and **leaves the bottom row alone**; all 13 anchors land
inside the cell.

Served over http it also fetches the two JSON files and cross-checks them against the live rig — the
contract's per-facing bbox against a fresh bake, and the sidecar's floor heights, door spans, hinge
edges, wheel positions and travel against `VehicleIso.G` — then re-hashes the rig. Opened straight off
disk those fetches are blocked, and the harness reports them as **skipped**, not passed.

## Known limits

- **No steering.** The front wheels roll but never yaw. A turning truck is a yaw on the sprite.
- **No walkable roof**, no rack, no grab rail, no route up — the cab roof is omitted from the sidecar
  rather than published as a surface.
- **No fuel filler.** The tank is modelled (street side, under the bed); the neck, cap and door are not.
- **Bed walkability is unruled.** The geometry is published; whether a player stands in the box or the
  box is a container is a gameplay decision, recorded in `_confirm`, not decided here.
- **The rear bench is one undivided 1.64 m cushion.** Rider slots are gameplay's to cut.
- **Mirrors are outside the collider.** `collider_bbox` stops at the flares (2.48 m); the mirrors reach
  **2.72 m** over. Decide before one clips a doorframe.
- **No plate, wipers, aerial or decals** — 1–3 px each at this scale, left out of the bake.
- **One body, one wheelbase.** No regular cab, no long box, no chassis-cab. Those are `BODIES` entries
  someone still has to write.
- **The roll loop is seamless, not bit-exact.** 25.13 tread stripes per revolution means no whole number
  of turns ever closes the tread pattern; a 10-frame loop is 0.29% of pixels off at the seam, which reads
  as nothing. If you need a bit-exact cycle, bake the strip once and play the same frames.
- **Weather is a single scalar** with a fixed speckle seed, so two identically-weathered trucks wear
  identically.

## Demo page (in the main project, not this kit)

`Dually Pickup Iso.dc.html` — the live builder: turntable, the ten paints, all five presets, every hinge
on a slider, the roll loop playing, the 8-dir strip and the cue strip baking live, and the four PNG
downloads this kit's sheets came out of.
