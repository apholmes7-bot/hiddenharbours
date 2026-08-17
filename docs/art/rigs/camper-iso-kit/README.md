# Hidden Harbours — Camper Iso Kit

A riveted-aluminium monocoque travel trailer, **parked as a dwelling**: one lofted body, two lengths,
**8 facings × 11 skins × one animated part**. Same turntable, same camera, same shading recipe as the
houses, the dually and the fleet, so it parks next to them without a reskin.

`camperIsoRig.js` holds two `VARIANTS` — `bantam` (16 ft) and `clipper` (26 ft) — and they are **the
same loft re-run, not two models**. Every station is a super-ellipse ring, rounder over the crown and
flatter under the belly; the plan half-width and crown height roll off over `noseRun`/`tailRun` into a
rounded dome at each end. Panel seams and their rivet lines are a **UV texture on the skin, not
geometry**, so the seams converge into the nose the way real panels do. Windows, the door and the wheel
arches are classified *out* of that ring — a window is the same lofted quads re-materialled and pushed
3.5% toward the section centre, so glass curves with the body and cannot z-fight it.

Conventions (ADR-0006 bake): **32 px = 1 m**, ¾ camera in 45° steps at **elev 40°**, flat-facet shading
from the fixed upper-LEFT key, z-buffered, ordered dither, depth-edge darkening, **no AA**, binary alpha,
**ringless** per ADR-0031 (`{outline:true}` is kept as the live A/B, never the bake).

## The pivot is the body, not the trailer

Cell is **384 × 320**, pivot **192,214**, identical in all eight facings — model origin projects to it
with zero error. The origin is the **ground-centre of the BODY footprint, tongue excluded**, so the
camper drops onto a terrain point like the house and the room rigs do. The tongue hangs off `+y` beyond
the pivot, which is why ground-level pixels paint **below the pivot row** when the nose swings toward
the camera: 61 px on the Bantam, 94 px on the Clipper. That is the drawbar sitting on the ground, not
the body sinking into it.

`order` is `N NE E SE S SW W NW` and names **where the camper points** — the hitch, not the view. At
**S** the tongue comes at you; at **N** you see the tail. `+x` is the **curb side**, which is the door
side, and it reads at `SW W NW`.

| | Bantam 16 | Clipper 26 |
| --- | --- | --- |
| body L × W × H | 4.34 × 1.98 × 2.58 m | 7.16 × 2.20 × 2.80 m |
| over the coupler | 5.58 m | 8.58 m |
| sole / headroom | 0.68 m / 1.80 m | 0.70 m / 2.00 m |
| axle | single, y −0.34 | single, y −0.62 |
| door clear | 0.68 × 1.70 m | 0.72 × 1.72 m |
| fitted by default | 2 vents | 3 vents, AC pod, awning |
| painted union, at rest | 220 × 159 px | 322 × 231 px |
| fit-out | 5 obstructions | 8 obstructions |
| sole polygon | 50 points | 82 points |

Crop and pack from a per-facing bbox, never from the cell. At rest, per facing (`x0,y0,x1,y1`):

```
bantam   N 150,119,226,253   NE 111,124,258,255   E  82,135,266,233   SE 111,122,258,256
         S 157,117,233,275   SW 125,122,272,262   W 117,141,301,233   NW 125,124,272,255
clipper  N 145, 87,282,282   NE  75, 99,291,277   E  31,109,311,235   SE  75, 96,291,279
         S 101, 78,238,308   SW  92, 96,308,285   W  72,133,352,271   NW  92, 99,308,277
```

Clipper numbers include the awning, which is fitted by default on that variant. Everything stays inside
the cell at every facing, with the door open and every deployable fitted.

## Files

| File | What it is |
| --- | --- |
| `camperIsoRig.js` → `globalThis.CamperIso` | The rig: loft, stations, fit-out, bake, anchors, and the sidecar generator. Self-contained — no deps, no `isoSolid.js`. |
| `camperIsoRig.bantam.gameplay.json` | Gameplay sidecar for the 16 ft. |
| `camperIsoRig.clipper.gameplay.json` | Gameplay sidecar for the 26 ft. |

Both sidecars are stamped `derivedFromRigSha256: b7a48e2f…7549`, hashed from **the exact
`camperIsoRig.js` in this folder**. Re-hash it before you trust either file; a mismatch means one of the
three moved without the others.

## Load order

```html
<script src="camperIsoRig.js"></script>   <!-- that's all of it -->
```

## One part moves, and it is the door

`render(dir, opts)` takes the whole build. There are no baked pose states and no second rig per length:

| Param | Range | What it does |
| --- | --- | --- |
| `variant` | `bantam` `clipper` | Which length. Same loft, different numbers. |
| `swing` | 0..1 | **The only animation.** The leaf, hinged on its **forward** edge, rotates 0 → **104°** while the two-tread step unfolds under the threshold over the **back 58%** of the run. This is the enter cue. |
| `paint` | `null` + 10 ramps | `null` is the **bare polished skin**: a 9-step alum ramp plus a polish term (+z normals take the sky, −z the ground) — what makes a curved tube read as metal instead of a grey pipe. Any `BODY` ramp repaints it and drops the polish to a matte 0.22, which is how a camper becomes a canteen, a shop or a bait van. |
| `weather` | 0..1 | Greys the paint, dulls the polish, pits the panels. Default 0.32. |
| `awning` `winAwn` | bool | Main awning (default **on** for the Clipper, off for the Bantam) and the window awnings (default off). |
| `vents` `acPod` `rack` | bool | Roof fit-out. `acPod` and `rack` default per variant. |
| `propane` `jacks` `chocks` `hookups` `step` | bool | A-frame bottles, stabiliser pads, wheel chocks, service inlets, entry step. All default **on**. |
| `night` | bool | Glass ramps swap and the door lamp becomes a glow. |
| `outline` | bool | The ADR-0031 A/B. Off in the bake. |

`frames(dir, n, opts)` bakes the enter cue as an *n*-frame strip, `swing` 0 → 1 inclusive; the engine
plays it forward on enter and **reversed on exit**. Five `PRESETS` (`bantamBare` `bantamPaint`
`clipperBare` `clipperCamp` `canteen`) are whole builds, not just paint. `anchors(dir, opts)` returns
nine screen-space anchors — `floor door threshold hinge step hitch lamp awning vents[]`.

## What the sidecar publishes

Same frame convention as the hulls, the dually and the Otter — metres, 32 px = 1 m, heading-independent,
`+x` curb, `+y` bow/hitch, `+z` up, origin at the ground-centre of the body footprint. Every number is
**generated from the same `resolve`/`prof` math the bake uses**, never hand-transcribed:

- **`SOLE`** — one continuous walkable polygon from tail to nose, CCW from above, at sole height, inset
  **0.055 m off the skin**. It is measured through the loft's own interior half-width, so the
  **tumblehome makes the walkable width narrower than the body width everywhere** — do not derive it
  from `width_m`.
- **Obstructions** (`SOLE[0]._notes`) — fit-out travels as **footprints with heights above the sole,
  never as holes in the floor**, each classified: `step_over` (≤ 0.50 m), `waist_block` (≤ 0.95 m),
  `wall` (above). The Bantam's wardrobe and the Clipper's wardrobe and head are walls; the berths and
  benches are not.
- **`THRESHOLD`** — the door, its clear width and height, the hinge axis, and the collider that matters:
  the **swept arc, not the leaf**, given as a three-point keep-clear at 104° open. The leaf sweeps
  outward over the step.
- **`STEP`** — two treads with top heights and reach, ground to first tread (0.22 m Bantam, 0.24 m
  Clipper), and the rule: **stowed, it is inside the silhouette and not walkable.**
- **`HOOKUPS`** — shore power, water fill and sewer outlet on the street side; propane on the A-frame.
- **`_excluded` / `_confirm`** — the absences, on the record: no side decks, no walkable roof, the
  coupler is a tow point and the jacks are level pads (neither is a tie-off), the head is currently a
  1.90 m partition rather than a room, and the awning's shade footprint belongs to the runtime because
  it moves with the toggle.
- **`_sections_elsewhere`** — there is **no `INTERACT` section here**, deliberately. The berth, wardrobe,
  shelf and wall calendar are placed by `camperInteriorRig.js`; merge
  `CamperInterior.interactables({variant})` in rather than re-typing it, and check its reach points
  against `SOLE` above.

## Known limits

- **Only the door animates.** No slide-out, no folding step independent of the door, no jack travel, no
  vent lids, no awning furl — the awning is fitted or not.
- **Single axle only.** Tandem-axle and fifth-wheel bodies would be new `VARIANTS` entries, not
  parameters on these two.
- **No tow coupling.** The hitch anchor is published, but nothing in this rig articulates against a
  vehicle: pair it with the dually by hand, and mind that neither rig knows about the other.
- **No interior.** This is the shell. What you see through the open door is the dark seen-in volume of
  the fit-out, not a room; `camperInteriorRig.js` owns the room and measures against this rig's
  published `loft`.
- **The roof is not walkable** and no sidecar section pretends otherwise, even though vents, AC pod and
  rack are all modelled.
- **Weather is a single scalar** with a fixed speckle seed, so two identically-weathered campers wear
  identically.
- **No plate, decals, aerial or mirrors** — 1–3 px each at this scale, left out of the bake.
- **No harness or baked sheets in this kit.** The rig and its two sidecars are the whole export; bake
  sheets from the demo page below, which is where these numbers were measured.

## Demo page (in the main project, not this kit)

`Camper Iso.dc.html` — the live builder: turntable, both lengths, the eleven skins, all five presets,
every deployable on a toggle, the door cue looping, the 8-dir and swing strips baking live, the walkable
sole drawn in plan with its obstructions and the door arc, and the PNG + sidecar downloads.
