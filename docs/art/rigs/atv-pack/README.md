# Hidden Harbours — ATV Pack Iso Kit

*One folder of the [Vehicle Rig Pack](../README.md) — three bodies in one rig. The conventions it shares with
the trucks, the Otter and the trailers (camera, ramps, facings, `yaw`) are documented once, in the pack README.*

Everything in the harbour you sit **astride**: an **Enduro 250** dirtbike, a **Trike 200** three-wheeler on
balloon tires, and a **Utility Quad 4×4** with racks, a hitch and a winch. No cab, no doors, no bed — a saddle,
a bar, pegs or boards, and racks where the class carries them. **8 facings × 10 paints × a heading wheel
between the facings**, one 256 × 192 cell for all three, same turntable and shading recipe as the rest of the
pack, so they park beside the dually and the Otter without a reskin.

`atvIsoRig.js` holds a `SPECS` table with three entries and builds each body from it: `body:'dirtbike' |
'trike' | 'quad'` on every call. It is a **separate rig from `vehicleIsoRig.js` on purpose**: a truck is a body
lathed onto a frame; these are a frame with a wheel at each end and the rider as the missing half of the
silhouette. Nothing about a saddle is a pickup with different numbers.

Conventions (ADR-0006 bake): **32 px = 1 m**, ¾ camera in 45° steps at **elev 40°**, flat-facet shading from
the fixed upper-LEFT key, z-buffered, ordered dither, depth-edge darkening, **no AA**, binary alpha,
**ringless** per ADR-0031 (`{outline:true}` is kept as the live A/B).

## The bake carries no rider, and that is the point

Nobody is on any of these machines in any sheet. The rider is the **character rig**, mounted at `anchors(dir,
opts)`: `seat`, `bars`, `gripL` / `gripR`, `pegL` / `pegR` (the quad's are its footboards), returned **already
posed** — grips turn with the bars, every point rides the suspension, and on the bike everything rolls with the
lean. Place the character on those and it sits right at any pose; `dims().leanDeg` is the angle to roll the
rider by. The sidecar's `SADDLE` section is the same contract in metres.

Two consequences worth knowing before anything is wired:

- **The dirtbike defaults to `stand:1` — she bakes PARKED**, leaning 12° onto her side stand on the street
  side. A dirtbike does not stand upright unridden, so the default is the honest one. Every rolling cue sets
  `stand:0`; the `ridden` sheet is the upright state a rider mounts into, and it is the one to pool for a moving
  bike. A game that shows her upright with nobody aboard is showing a bug the rig will not catch.
- **Two riders' worth of geometry share one cell.** The bike, the trike and the quad are 1.85–2.23 m long and
  paint 68–74 px wide; the cell is the Otter's, and it holds every pose of all three — see `union_everything`
  per body in the contract. Crop from the contract, not from the cell.

## Facing 0 shows the tail

`order` is `N NE E SE S SW W NW` and it names **where the machine is pointed**. At **N** the nose points away
and you see the tail lamp; at **S** the headlamp. The street side is `-x` and reads at `NE E SE`; the curb side
`+x` at `SW W NW`. The exhaust is on the curb side, the side stand on the street side: a bike is mounted from
the street side, over the stand, which is why the sidecar's `mount.preferred` says so.

Cell **256 × 192**, pivot **128,128**, identical in all eight facings for all three bodies: model `z = 0` at the
ground-centre of the wheelbase projects to the pivot row, so a bike on full bump, leaned 28°, or down on her
stand still sits on the road. Painted unions at rest, measured off the alpha: **Enduro 74 × 62** (parked; the
ridden union is the same box), **Trike 68 × 54**, **Quad 74 × 60**. Over every pose, fitting, yaw step and
lean: **74 × 66 · 68 × 55 · 74 × 64**. Per-facing boxes are in `per_body.<body>.painted_bbox`.

## Files

| File | What it is |
| --- | --- |
| `atvIsoRig.js` → `globalThis.AtvIso` | The rig: three bodies off one `SPECS` table, raked-head steering, Ackermann kingpins, suspension, the bike's lean and stand, the bake, `anchors()`, and the sidecar generator. Self-contained — **no `isoSolid.js`**. |
| `atvIsoRig.atvPack.gameplay.json` | The gameplay sidecar — **one file, three bodies** under `bodies.dirtbike / trike / quad`. Byte-identical to the copy in the art workspace. |
| `atvPack.contract.json` | Every number in this kit, machine-readable: bake + camera, the shared articulation table, and per body — dims, per-facing painted bbox in every state, pose-sweep unions, the roll seam with its cause, cue motion, suspension rows, steering geometry, the bike's stand and lean, distinct colours, every anchor × 8 facings (the bike twice: parked and ridden), and the sheet manifest. |
| `harness.html` | Standalone bake + assert harness, all three bodies. Open it in a browser: no build, no deps, no project. |
| `_atvKit.js` | The script that baked the sheets and wrote the contract off the live rig. Re-run it (instructions in its header); never hand-edit a number it wrote. |
| `Enduro250_teal_8dir.png` | 8 × (256 × 192) — the `enduro` build (teal, weather 0.30, headlamp) **PARKED**: stand 1, 12° onto the stand. `N NE E SE S SW W NW`. |
| `Enduro250_teal_ridden_8dir.png` | 8 × (256 × 192) — the same build **RIDDEN**: stand 0, upright. Pool this for a moving bike. |
| `Enduro250_teal_roll_W.png` | 10 × (256 × 192) — one rear-wheel revolution at W, stand 0, cyclic (frame 10 wraps to 0). See the seam note below. |
| `Enduro250_teal_turn_SE.png` | 8 × (256 × 192) — the `turn` cue at SE, cyclic: steer, 14° of yaw and a 0.55 lean **into** the turn, rolling. |
| `Enduro250_teal_bounce_W.png` | 8 × (256 × 192) — the `bounce` cue at W, cyclic: front and rear suspension out of phase, rolling. |
| `Enduro250_teal_steer_S.png` | 8 × (256 × 192) — full right lock → full left lock at S, inclusive, baked at stand 0. |
| `Enduro250_teal_park_SE.png` | 8 × (256 × 192) — the `park` cue at SE: stand 0 → 1, upright → down on the stand. Run it reversed to kick the stand up. |
| `Enduro250_rustOrange_plate_8dir.png` | 8 × (256 × 192) — the `mxPlate` preset: `lamp:false` swaps the headlamp for a white MX number plate at the same station. Parked. |
| `Enduro250_paints_SE.png` | 5 × 2 grid — the ten harbour paints at SE, weather 0.22, parked. |
| `Trike200_gold_8dir.png` | 8 × (256 × 192) — the `beachTrike` build (gold, weather 0.38, rear rack) at rest. |
| `Trike200_gold_roll_W.png` · `_turn_SE.png` · `_bounce_W.png` · `_steer_S.png` | The four cues the trike has: 10 / 8 / 8 / 8 cells. The bounce moves the **front only** — the rear axle is rigid. |
| `Trike200_paints_SE.png` | 5 × 2 grid — the ten paints at SE, weather 0.22. |
| `UtilityQuad_sage_8dir.png` | 8 × (256 × 192) — the `wharfQuad` build (sage, weather 0.42) with **both racks, the hitch and the winch**. |
| `UtilityQuad_sage_bare_8dir.png` | 8 × (256 × 192) — the same paint with nothing fitted. |
| `UtilityQuad_sage_roll_W.png` · `_turn_SE.png` · `_bounce_W.png` · `_steer_S.png` | The four cues: 10 / 8 / 8 / 8 cells. The roll loop closes **bit-identical**. |
| `UtilityQuad_paints_SE.png` | 5 × 2 grid — the ten paints at SE, weather 0.22, racks + hitch fitted. |

Pivot is pinned identically in every cell of every sheet. Each cue was baked at the facing where its motion
faces the camera — roll and bounce side-on at W, steer nose-on at S, turn and park on the SE quarter where the
street side (and the stand) read. Recipes are in the contract's sheet manifest.

## Load order

```html
<script src="atvIsoRig.js"></script>   <!-- that's all of it -->
```

## Everything on these machines moves

`render(dir, opts)` takes the body and the pose. No baked animation states, no separate rigs per pose:

| Param | Range | What it does |
| --- | --- | --- |
| `body` | `dirtbike` `trike` `quad` | which of the three. Default `quad`. |
| `roll` | revolutions | master wheel roll, **revolutions of the rear wheel**: 2.073 m per rev on the bike, 1.759 on the trike, 1.979 on the quad. The front wheel on the bike and trike is a different radius and turns rR/rF times as fast, so both tread the same road. |
| `rollF` `rollR` | revolutions | per-axle offsets. |
| `steer` | −1..1 | handlebar; **+1 is full LEFT** (the nose swings toward `-x`), the dually's convention. Bike and trike: the **whole front assembly** — wheel, fork, fender, lamp, bars — turns about the **raked steering axis** through the head (27° / 25° rake, ±35° / ±30°). Quad: the bars turn ±28° about the stem and the front pair yaw about their own kingpins, **Ackermann 30° inner / 21.9° outer**. |
| `susF` `susR` | −1..1 | suspension per axle. **The body moves, the wheels stay on the ground**: `dz(y)` runs linearly between the axles. Travel bike 0.14 / 0.12 m, quad 0.10 / 0.09, trike 0.08 / **0** — the trike's rear is a rigid axle on balloon tires and `susR` is accepted and ignored. |
| `lean` | −1..1 | **dirtbike only.** Roll of the whole machine about the tyre contact line (`x=0, z=0`); +1 is 28° toward the curb. The contact patch never leaves the ground. Cornering leans INTO the turn: `steer:+1` pairs with `lean:-1`. |
| `stand` | 0..1 | **dirtbike only.** Side stand on the street side, 0 up → 1 down, and she settles 12° onto it. **Default 1.** Stacks with lean: `leanDeg = lean·28 − stand·12`, published by `dims()`. |
| `yaw` | −45..45° | heading **between** the facings. The model turns about z under the fixed key, so the shading is **rebaked, not rotated**; 45° at NE is bit-identical to E, and the harness asserts it for all three bodies. |
| `lamp` | bool | bike: `true` enduro headlamp (default), `false` MX number plate at the same station — no light at night. |
| `rackF` `rackR` `hitch` `winch` | bool | fitted parts. Quad: racks and hitch default **on**, winch **off**. Trike: `rackR` on. The bike carries nothing. |
| `night` | bool | headlamps swap to the glow ramp and spill one pixel; tail lamps do not glow. |
| `weather` | 0..1 | greys and grimes the plastics, rusts the running gear, speckles. Default 0.32. |

`frames(dir, n, opts, cue)` bakes a strip through one of five named cues — `roll` `turn` `bounce` (cyclic) ·
`steer` `park` (0 → 1 inclusive); `cuesFor(body)` says which apply (`park` is the bike's alone). `roll`, `turn`
and `bounce` set `stand:0` themselves; `steer` does not, so the steer sheet was baked with `stand:0` passed in.
Six `PRESETS` (`showroom` `trailWorn` `enduro` `mxPlate` `beachTrike` `wharfQuad`) are whole builds; the last
four name a body. `fittingsFor(body)` lists the parts a body takes; `steer.quad` / `.dirtbike` / `.trike`
publish the lock and rake numbers; `steer.angles(v, SPECS.quad)` is the Ackermann split itself.

## The roll loop — exact on four wheels, a front-wheel seam on two

One rear revolution is the loop, and the lug pitch on every tyre is fitted to a whole number of lugs per
revolution (13 on the bike and quad, 11 on the trike), so the **rear** always closes. The **quad's four wheels
share one radius and the loop closes bit-identical** — 0 pixels differ between frame 10 and frame 0.

The bike and the trike have a smaller rear wheel than front, so the front turns **0.943** (bike) and **0.966**
(trike) of a revolution per rear revolution and stops **20.6° / 12.4° short**. On the bike's six-spoke wheel
that leaves a seam of **27 colour + 28 alpha pixels, 2.0% of painted**; on the trike's spokeless balloon tyre
**8 pixels, 0.56%**. Neither reads in motion. The contract proves the cause: `roll:1` with `rollF: 1 − rR/rF`
added is **bit-identical** to `roll:0` on both — the seam is the front wheel and nothing else. A game that wants
a bit-exact bike loop can drive `rollF` to that value at the wrap; the sheets ship the honest physics.

The trike's roll barely reads at all — a lugged balloon tyre with no spokes moves **4 px** between frames at
W. That is the class, not a defect; its `roll` sheet is there for the wheels-on-road check, not for the eye.

## The sidecar is one file with three saddles in it

`atvIsoRig.atvPack.gameplay.json` — the first **generated** vehicle sidecar in the workspace: the rig carries
`gameplayGeometry({body})` and `gameplayAll()`, and every number comes off the same `SPECS` table the bake
reads. Same frame as the trucks and the Otter (metres, 32 px = 1 m, heading-independent, `+x` curb, `+y` nose,
`+z` up, origin at the **ground-centre of the wheelbase**), `frame` once at the top, then `bodies.dirtbike`,
`bodies.trike`, `bodies.quad`, each with:

- **`SADDLE`** — the mount contract: `seat_ref`, the seat's extent, the two grips (they turn with the bars —
  read them from `anchors()`, not from the rest values, when the bars are turned), pegs or footboards, the
  astride posture, mount sides with reach points and computed `visible_facings`.
- **`STEERING`** — bike/trike: the raked axis through the head as a point and a direction, lock, and the
  geometric turning radius (2.11 m / 2.08 m). Quad: stem, kingpins, Ackermann inner/outer, and the radii at
  full lock (inner wheel 2.22 m, centreline 2.70 m).
- **`SUSPENSION`** — `dz(y)` per axle with the unsprung list. The trike's rear travel is **zero**, recorded as
  the class's fact and not an omission.
- **`STAND`** and **`LEAN`** (bike only) — hinge, tip, the 12° parked attitude with `world_at_rest` giving the
  leaned seat ref and tip on z = 0 to 1 mm; ±28° about the contact line, how it stacks with the stand, and that
  the rider leans **with** the machine.
- **`CARGO`, `TOW`** — racks as platform polygons with load heights (quad front and rear, trike rear); the
  quad's 50 mm hitch ball and its winch drum. The bike's `CARGO` says why there is none.
- **`WHEELS`, `ATTACH`, `INTERACT`, `YAW`** — the wheel list with roles and spoked flags, lamps and exhaust
  tips, `ride / kick_stand / cargo / couple / recover` with reach points, and `yaw` as a render param.

`_excluded` records the absences a reviewer will look for: **no `THRESHOLD`** (the way in is a leg over the
saddle), no cab, roof or bed, no levers or pedals at 1–2 px, no chain on the bike (the sprocket is there), no
float. `_confirm` carries the open ones: reach points untested, every seat one undivided cushion, the bike's
**parked default** as a sprite-pooling trap, standing on the quad's footboards.

To pull one body: `sidecar.bodies.quad` plus `sidecar.frame` is a complete single-body file. `derivedFromRigSha256`
is the drift tripwire — nobody types it; the harness re-hashes the shipped rig and fails the group if the two
disagree.

## The harness asserts, it does not just draw

`harness.html` bakes all three bodies in all eight facings and checks: the origin projects to the published
pivot in every facing; nothing paints outside the cell or more than 30 px below the pivot; each body's union at
rest is the published box; **every anchor lands in the cell in all facings**, the grips move under steer and the
seat does not, the seat rides the suspension and the wheels do not, and the bike's saddle leans both ways; every
cue moves pixels at the facing it was baked at and the rolling cues kick the stand up; the quad's roll closes
bit-identical while the bike's and trike's seams stay under 2.5% / 1% and close bit-identical once the front is
given its `rollF`; full left lock differs from full right on all three, the quad's outer angle equals the
Ackermann formula and mirrors under `steer:-1`; the ground row holds on full bump and droop while the body drops
and rises, and the trike's `susR` changes nothing; the bike alone has a stand, her tip sits on the road at
`stand:1`, `leanDeg` sums as published, lean pivots on the contact line and goes the right way; and yaw 45° at
NE is bit-identical to E for every body, with a ±22.5° sweep inside the cell.

Served over http it also fetches the contract and the sidecar, cross-checks the contract's at-rest bbox per
facing against a fresh bake for each body, reads ten sidecar values per body back against `SPECS`, and
re-hashes the rig against both files' stamps. Opened straight off disk those fetches are blocked and the group
reports **skipped**, not passed. Current state: **77 passed, 0 failed, 0 skipped**.

## Known limits

- **No rider in the bake.** Deliberate — see above. Until the character rig's saddle pass lands, a placed bike
  is a parked bike.
- **The bike's parked default** is a pooling trap: the rest pose of `render(d, {body:'dirtbike'})` is not the
  pose of a bike being ridden. Bake the ridden state from the roll cue or `stand:0`.
- **Steer, yaw and lean are not coupled.** The `turn` cue couples them for the sheet only; the rig will not stop
  a game leaning the wrong way into a corner.
- **The roll loop is not bit-exact on the bike or the trike** — the front wheel is a different radius (above).
  The quad's is.
- **The trike's roll barely reads** — 4 px per frame on a spokeless balloon tyre.
- **No rear suspension on the trike**, correctly for the class. `susR` is a no-op there.
- **No levers, pedals, chain, mirrors, plate or decals** — 1–2 px each at this scale. The bars carry grips only.
- **Racks are tube frames** — small items fall through in the fiction; the sidecar publishes the platform
  polygon and leaves that to the game.
- **The winch has no rope**, the hitch has no trailer. Both are anchors for the game to draw from.
- **Weather is a single scalar** with a fixed speckle seed, so two identically-weathered machines wear
  identically.
- **11 ramps at most** on any build of any body (31–41 distinct colours in a cell at rest), well inside the
  fleet's 16.

## Demo page (in the main project, not this kit)

`ATV Pack Iso.dc.html` — the live builder: body selector, turntable, the ten paints, six presets, fittings on
toggles, every pose param on a slider, the roll loop playing, the 8-dir strip and the cue strip baking live,
**SADDLE MARKS** drawing the mount anchors on the stage, the PNG downloads, and the GAMEPLAY SIDECAR button that
stamps `derivedFromRigSha256` through `Art/_sidecarExport.js`.
