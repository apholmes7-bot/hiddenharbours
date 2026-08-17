# Hidden Harbours — Otter 8×8 Iso Kit

*One body of the [Vehicle Rig Pack](../README.md). Its sibling is the dually in `../dually-3500/`; the
conventions the two share — camera, ramps, facings, `yaw` — are documented once, in the pack README.*

The first **amphibian** in the harbour: a skid-steer 8×8 XTV — one sealed poly tub on eight
low-pressure tires, no suspension of any kind, optional rubber tracks over the lot, and a hull that
swims. **8 facings × 10 paints × a heading wheel between the facings.** Same turntable, same camera,
same shading recipe as the dually, the campers and the fleet, so it parks next to them without a reskin.

`amphibIsoRig.js` holds a single `BODIES` entry (`otter8x8`). It is a **separate rig from
`vehicleIsoRig.js` on purpose**: the dually lathes a body onto a frame, this one lofts a boat hull
through 19 stations and then hangs running gear off it. Nothing about a tub is a pickup with different
numbers.

Conventions (ADR-0006 bake): **32 px = 1 m**, ¾ camera in 45° steps at **elev 40°**, flat-facet shading
from the fixed upper-LEFT key, z-buffered, ordered dither, depth-edge darkening, **no AA**, binary alpha,
**ringless** per ADR-0031 (`{outline:true}` is kept as the live A/B).

## The bake is dry, and that is the point

**Nothing below the waterline is cut and no wake is painted.** `float` only lowers the whole machine
0.52 m onto its marks so the attitude is right — **the game's water shader owns the cut**, and the plane
to cut on is the **pivot row**, in every facing, because world z = 0 is horizontal in this projection.

Everything that shader needs is published: draft 0.28 m above the keel, freeboard 0.34 m (0.29 m at the
transom, her lowest point), a five-point displacement curve integrated off the same station profiles the
loft lathes (**866 kg** at rest, hull only), and a 14-point waterline polygon for the wake mask. See
`FLOAT` in the sidecar, and the "waterline contract" panel on the demo page, which draws a shader over
the dry sprite so the cut can be checked against the sheet.

## Facing 0 shows the transom

`order` is `N NE E SE S SW W NW` and it names **where she is pointed**, not what you are looking at. At
**N** the bow points away and you see the transom; at **S** you see the bow. The driver's side is `-x` —
**street side** — and it reads at `NE E SE`; the curb side at `SW W NW`. She is **centre-steer**, so the
sides are a convention here, not a helm position: read the handlebar, not the seats.

Cell is **256 × 192**, pivot **128,128**, identical in all eight facings: model `z = 0` at the hull's
ground-centre projects to the pivot row, so a tracked machine still sits on the road and an afloat one
still cuts at the surface. Measured painted union is **104 × 89 px** at rest, **104 × 105** once the
afloat attitude is included, and **104 × 90** over a ±22.5° yaw sweep — crop and pack from
`painted_bbox` in the contract, per facing, not from the cell.

## Files

| File | What it is |
| --- | --- |
| `amphibIsoRig.js` → `globalThis.AmphibIso` | The rig: hull loft, station profiles, running gear, tracks, bake. Self-contained — **no `isoSolid.js`**, it lofts its own hull. |
| `amphibIsoRig.otter8x8.gameplay.json` | The gameplay sidecar — hull form, `FLOAT`, cockpit, seats, the three ways in, decks, rack, wheels, tracks, yaw, interactables, and the absences. |
| `otter8x8.contract.json` | Every number in this kit, machine-readable: bake + camera, dims, per-facing painted bbox at rest / tracked / all-fitted-open / afloat, the articulation table, paints, presets, cues, 13 named anchors × 8 facings, and the sheet manifest. |
| `harness.html` | Standalone bake + assert harness. Open it in a browser: no build, no deps, no project. |
| `Otter8x8_sage_8dir.png` | 8 × (256 × 192) — the workhorse build (sage, weather 0.44) at rest, `N NE E SE S SW W NW`. |
| `Otter8x8_tracked_8dir.png` | 8 × (256 × 192) — the tracked build (greyShingle, weather 0.58), which stands **0.07 m taller**. |
| `Otter8x8_roll_NE.png` | 10 × (256 × 192) — one wheel revolution at NE, **cyclic** (frame 10 wraps to frame 0). |
| `Otter8x8_spin_NE.png` | 8 × (256 × 192) — the `spin` cue: the sides counter-rotate and she yaws through a **whole facing step**, 0 → 45°. Frame 8 *is* the E cell at rest. |
| `Otter8x8_launch_W.png` | 8 × (256 × 192) — `float` 0 → 1 at W. **Dry**: the sprites are whole, the shader cuts them. |
| `Otter8x8_hatch_SE.png` | 8 × (256 × 192) — the engine hatch 0 → 52° at SE. |
| `Otter8x8_paints_NE.png` | 5 × 2 grid — the ten harbour paints at NE, weather 0.22. |

Pivot is pinned identically in every cell of every sheet.

## Load order

```html
<script src="amphibIsoRig.js"></script>   <!-- that's all of it -->
```

## Everything on this machine moves

`render(dir, opts)` takes the pose. No baked animation states, no separate rigs per pose:

| Param | Range | What it does |
| --- | --- | --- |
| `roll` | revolutions | master wheel/track roll. 2.011 m per revolution. The loop closes on one revolution to within a lug phase — **0.20% of painted pixels, zero alpha change**. |
| `rollL` `rollR` | revolutions | per-**side** offsets. `rollL:+t, rollR:−t` is a spin on the spot: there is no steer axle and this is the whole drivetrain model. |
| `yaw` | −45..45° | heading **between** the facings, and her only yaw. The model turns about z under the fixed key, so the shading is **rebaked, not rotated**. A quarter-turn of side split (0.246 rev) is 45° — exactly one facing step, and that arithmetic is published. |
| `float` | 0..1 | settles her 0.52 m onto her waterline. **Nothing is clipped** — see above. |
| `hatch` | 0..1 | bow engine hatch, hinged at its aft edge, 0 → **52°**. There is a real bay underneath — block, intake, battery. |
| `tracks` | bool | rubber belts wrapping each side. **Defaults off.** The belt runs *under* the tires, so the whole machine — and every published z — stands **0.07 m** higher, and ground contact goes 0.36 → 1.63 m². |
| `rack` `winch` | bool | fitted parts, default **on**. The rack blocks boarding over the transom. |
| `screen` `bimini` | bool | fitted parts, default **off**. Neither folds: fitted or not. |
| `night` | bool | glass ramps swap, headlamps become glow, light spills one pixel onto its neighbours. |
| `weather` | 0..1 | greys and grimes the tub, rusts the running gear, speckles. Default 0.34; the sheets are 0.44. |

`frames(dir, n, opts, cue)` bakes a strip through one of five named cues — `roll` `spin` `launch` `hatch`
`bob`. `roll`, `spin` and `bob` are cyclic; `launch` and `hatch` run 0 → 1 inclusive. Five `PRESETS`
(`showroom` `bushOlive` `tracked` `afloat` `harbourHaul`) are whole builds, not just paint.

## The sidecar is a hull's and a vehicle's at once

Same frame convention as the hulls, the campers and the dually — metres, 32 px = 1 m,
heading-independent, `+x` curb, `+y` bow, `+z` up, origin at the **ground-centre of the hull
footprint** — but it is the first file in `Art/gameplay/` where `HULL` and `FLOAT` sit beside `WHEELS`
and `TRACKS`:

- **`HULL`** — the tub **is** the hull. Sixteen-station half-beam table, the keel flat and its bow
  rocker, the chine where the loft changes material (the black lower hull **follows the form**; it is
  not a decal), and the rub rail.
- **`FLOAT`** — the water-shader contract described above: clip rule, sink formula, draft, freeboard,
  displacement curve, waterline polygon, and the down-flooding depth she **cannot be posed at**
  (0.82 m of sink is `float` 1.58, outside the clamp).
- **`COCKPIT`** — an open tub, not a room: floor polygon 1.06 × 1.84 m at z 0.44, a 1.06 × 0.72 m
  footwell as the only clear floor, three obstructions with heights, and a **centred handlebar**. There
  is no driver's side on this machine.
- **`THRESHOLD`** — **no doors.** Three ways in: over either gunwale (0.86 m sill, and you step over a
  tire, not up a clear face) or over the transom (0.82 m, blocked by the rack, which is fitted by
  default).
- **`TRACKS`** — `present_when: {tracks:true}`, and the note that matters: **every z in the file gains
  0.07 m** when they are on. Nothing in the file is pre-shifted.
- **`YAW`** — the skid arithmetic tying a side split to a heading change, and the warning that the rig
  does not couple them.
- **`SEATS` `DECK` `CARGO` `WHEELS` `ATTACH` `INTERACT`** — two benches with refs; the crowned bow deck
  and its 0.68 × 0.44 m hatch; the tube rack as the only load surface; eight wheels on four **rigid**
  axles; winch, lamps, canopy, screen; seven interactables with reach points and `visible_facings`.

`visible_facings` is **computed, not eyeballed**: a feature with outward normal *n* is listed for facing
*d* when `nx·sin(45d) + ny·cos(45d) < −0.3` — the same near-side test the rasteriser uses, edge-on
excluded. `reach_point` is a **request, not a promise** (same warning as the dually's): a ground-level
standing spot, clear by measurement, but untested against terrain or the water's edge.

`derivedFromRigSha256` is the drift tripwire. Nobody types it; the harness re-hashes the file and fails
the `sha` group if the two disagree. The same sidecar sits byte-identical in the art workspace at
`Art/gameplay/amphibIsoRig.otter8x8.gameplay.json`.

## The harness asserts, it does not just draw

`harness.html` bakes all eight facings and checks: the origin projects to the published pivot in every
facing; nothing paints outside its cell or more than 30 px below the pivot; the union at rest is the
published 104 × 89; each of the five cues actually moves pixels; the one-revolution roll seam stays
under 0.6% of painted with zero alpha change; opposite skid splits differ; **tracks raise the body and
leave the ground row alone**; **`float:1` lowers every painted row by exactly 0.52 m and clips nothing**;
and all 13 anchors land inside the cell in all eight facings.

Served over http it also fetches the two JSON files and cross-checks them against the live rig — the
contract's per-facing bbox against a fresh bake, and the sidecar's rail, keel, floor, sink, lift, wheel
positions, hatch opening and waterline polygon against `AmphibIso.G` — then re-hashes the rig. Opened
straight off disk those fetches are blocked, and the harness reports them as **skipped**, not passed.

## Known limits

- **No suspension.** Four rigid axles. The tires are the suspension and no param flexes them — unlike
  the dually there is no travel, no `dz(y)`, and nothing for a rider to be corrected against.
- **No steer axle.** Correct for the class: `yaw` turns the whole machine, and that is the steering. The
  wheels never yaw individually.
- **Yaw and the side split are not coupled.** Driving one without the other reads wrong and the rig will
  not stop it.
- **No propeller, jet, rudder or kicker bracket.** She swims on her tires, which is how the class does
  it, but it means there is no thrust geometry to attach an FX to.
- **No bilge, drain or scupper.** The tub is published as sealed. If the game floods it, nothing in the
  rig empties it.
- **No wake, spray or waterline stain in the bake.** Deliberate — `FLOAT` gives the polygon to draw them
  from.
- **The canopy and screen do not fold.** Fitted or not.
- **No enclosed cab.** The hard-cab machines in this class would be a second `BODIES` entry.
- **The fuel tank is not modelled.** The aft box is a closed volume with nothing published inside it.
- **No plate, mirrors, aerial or decals** — 1–3 px each at this scale, left out of the bake.
- **The roll loop is seamless, not bit-exact.** 12.97 lug periods per revolution means no whole number
  of turns closes the tread pattern; a 10-frame loop is 0.20% of pixels off at the seam, which reads as
  nothing.
- **Weather is a single scalar** with a fixed speckle seed, so two identically-weathered machines wear
  identically.

## Demo page (in the main project, not this kit)

`Amphib 8x8 Iso.dc.html` — the live builder: turntable, the ten paints, all five presets, tracks and
fittings on toggles, the roll loop playing, `float` on a slider with the water shader drawn over the dry
sprite, the 8-dir strip and the cue strip baking live, and the four PNG downloads this kit's sheets came
out of.
