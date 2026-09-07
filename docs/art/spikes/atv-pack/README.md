# ATV pack — intake plates (2026-09-06)

Written by `AtvPackMeshParityTests` (EditMode). Everything here is a measurement with a picture
attached, not a picture with a claim attached — the numbers are in the fixture and in
[the PR](../../../../pull/PRNUM), and each plate says which number it shows.

## `<body>-rig-over-mesh-8dir.png`

Top row: the **rig's own `render()`** at rest, all eight facings, `N NE E SE S SW W NW`.
Bottom row: the **extracted mesh**, rasterised by the repo's CPU oracle at the same eight views.

This is the parity the whole hull fleet is held to (`RigMeshMenu.Verify`'s 0.5% bar), and on this
drop it is doing a second job: the ATV rig exports no `MATS`, so its ramp table is **reconstructed**
(`RigMeshSymbols.Reconstructions["atvIsoRig.js"]` — the union of what all three bodies' faces name,
in the rig's own key order). The truth row picks its colours the rig's inline way; the mesh row goes
through the reconstruction. A dropped ramp, or the wrong key first — which the face packer resolves
to index 0 — would re-paint every lit pixel, and it would read in whole percent rather than in
hundredths.

⚠️ Both rows name the body. `AtvIso.resolve` falls back to the **quad** for an unknown or absent
body, at **zero pixels** difference from a real quad, so a truth render without the pick would
compare all three machines against a quad: two would read enormous and the third would pass for
entirely the wrong reason.

⚠️⚠️ **Both rows carry a 1-px keyline, and the shipped machine does not.** The CPU oracle draws its
outline **unconditionally** — it predates ADR 0031 — while these rigs are RINGLESS by default
(`KEYLINE_DEFAULT = false`, as the Dually's is). Against a bare rig render the mesh therefore paints
a perfect one-pixel border the rig does not: measured **351 surplus pixels at E on the bike, and
ZERO missing ones**, which is what identified it. The truth row is rendered with the rig's own
`outline:true` so both sides are in one style; the runtime draws no ring either way
(`GameConfig.DefaultHullKeylineFlood = false`). The hull rigs draw theirs unconditionally, which is
why the hull fleet has never seen this — **no road-fleet body has ever been parity-checked**, and
anyone who runs this oracle over the trucks will meet the same ~20% and should read it here first.

## `enduro-parked-vs-ridden.png`

Left: `render(SE, {body:'dirtbike'})` — the rig's **default**, which is `stand:1`: parked, leaned
12° onto her side stand, 502 faces. Right: the same build at `stand:0` — upright, 497 faces, and the
state a rider mounts into.

**1204 pixels differ.** This is why the fleet entry's rest pose carries `stand:0` in all four places
that select a body: a mesh taken from the default is a bike nobody can ride, and it looks entirely
correct in every still. The kit's own README calls the default a sprite-pooling trap; the same trap
reaches a mesh bake, one layer further in.

The five faces that appear at `stand:1` are the deployed leg. They are **deferred, not faked** — the
stand is a whole-machine lean *plus* a topology change, so it is neither a rotation nor a translation
nor a pose of one build. It belongs with PR 1's mount, where the lean is a transform of the whole
machine and the leg is a state swap.

## `quad-fitted-vs-bare.png`

Left: racks front and rear, hitch and winch. Right: nothing fitted.

The fittings are **BUILD** variants, not poses — the face list changes length — so the bake takes one
of them, and this plate is which: the rig's own defaults (`rackF`, `rackR` and `hitch` on, `winch`
off), which is also what her `wharfQuad` preset dresses. A game that wants a bare quad needs a second
mesh, not a pose.

## `astride-gap.png` — PR 1

The plate the section below promised, and it is not the one that was expected: **the rider goes on,
and her hands do not reach the bars.**

Left: the enduro pointing east, with the three points her sidecar publishes — `seat_ref` at 0.94 m
(amber) and the two `grips` at 1.04 m, 0.42 m ahead of the seat (green). Right: the same machine with
the `drive` clip placed on her by the shipped arithmetic — `DriveSeatMath.SeatPivotWorld`, no
adjustment of any kind — and a line from where the pose puts her hands to where the bars actually are.

**13.5 px on screen, 0.433 m in the machine's own frame.** The seat is exact: that is what
`DriveSeatMath` guarantees, and it is visibly right in the picture. What is wrong is the REACH. The
`drive` clip was baked to a helm **0.315 m forward and 0.255 m above** the seat — a wheel you reach
out and down to, sitting up — and a bike's bars are **0.72 m** ahead of hers.

The control is in the fixture rather than the plate, and it is the sentence that makes this a finding
rather than an opinion: the same arithmetic, on the Otter it shipped for, lands **0.68 px** out. And
the **trike's seat is 0.76 m, the Otter's height to the millimetre**, so her lift is identical while
her hands are still 12.7 px short. `AtvAstrideRiderTests` pins all of it, per machine, exactly — so
the day the astride stance lands, the numbers go red and somebody has to come and say so.

⚠️ **Two azimuths meet in this plate and they run opposite ways.** The pack's cell order is
counter-clockwise — at the cell *labelled* `E` the nose points **west** — while the character rows run
clockwise. The machine here is therefore cell **6** (`W` by the sheet's own label), which is the one
whose nose points east, matching the rider's `d2`. Neither convention is wrong and neither is a bug:
each side is measured by its own probe at bake time. It only bites when a human composites the two by
hand, as this plate does.

### What PR 1 did NOT do, and why

The section above expected PR 1 to take the stand — *"the lean is a transform of the whole machine and
the leg is a state swap"*. It did not, and the reason is in the bake: the enduro's mesh has **three**
moving parts, and they are `WheelF`, `WheelR` and `ForkF`. There is no lean axis and no stand in it to
raise. A rider rolled by `dims().leanDeg` today would lean while the machine underneath her stayed
bolt upright — inventing a fact the machine does not have, which `PlayerDrivePresenter`'s own doc
forbids. The absence is pinned at both ends instead (the sidecar publishes `LEAN` and `STAND`; the
mesh carries neither), so the day a lean is baked, the rider is known to be owed one.

Nothing in the world parks an ATV yet either — that is PR 2.

---

## What these plates deliberately do NOT show

- **A rider.** Nobody is on any of these machines in any *sheet*, by design: the rider is the
  CHARACTER rig, mounted at `anchors()` / the sidecar's `SADDLE`. PR 1 put her on
  (`astride-gap.png` above) and measured what the missing astride stance costs — **11.6–13.9 px at
  the hands**, against 0.68 px on the Otter. She is drawn, she is on the saddle, and her hands are
  in the air until that stance lands upstream.
- **A machine in the world.** Nothing is placed in a scene by this PR. St Peters gets her riders in
  PR 2, after the owner's rulings on who owns what and where it is parked.

---

## PR 2a — the player rides one, and three stand outside the shop (2026-09-07)

The owner ruled (d) of the pack: **the PLAYER may ride one**, and the same evening asked for the bike
and the trike to be parked too, not only the quad. All three now stand on the open ground at the
general store's frontage on St Peters, and any of them can be got on.

### What was already there, and what was not

Almost the whole verb was standing: PR 1's two mount sides, the tie-broken dismount, the rider on the
saddle. Three things were not.

1. **The three `VehicleDef`s existed and every number in them was the DUALLY'S.** PR 0 created them
   "at the class's tuning defaults", which is the same sentence as *a 112 kg dirtbike weighing three
   and a half tonnes and pulling away exactly as hard as a one-tonne pickup*. Nothing was wrong with
   the assets; nobody had ever tuned them, and it looked finished from every direction. The envelopes
   are now per machine — the mass off each sidecar's own `mass_kg_estimate`, the rest stated in the
   PR body as a table for the owner to move. `AtvRideVerbTests` refuses to let any of the three go on
   sharing five envelope fields with the Dually.

2. **The verb said "Climb in".** You climb into a cab and you get ON something you sit astride, and
   until this pack every machine in the game was a cab. The fact was already in the sidecar and was
   being thrown away: `VehicleSidecarFacts.ReadWayIn` has to pick between an `INTERACT id: "drive"`
   arm and an `id: "ride"` arm to find the door at all, and it discarded which it took. It now
   records it, the bake writes it to `VehicleMeshDef.WayInInteractId`, and the door reads it. Nothing
   in the code names a vehicle, and an absent field reads as a cab, so every truck in the fleet is
   the machine she was.

3. **Nothing in any scene was one.** Three `ParkedVehicle`s now are.

### The raked steer needs no controller field — asked, measured, answered NO

The charter asked for "the RAKED steer's own field if the generic controller needs one". It does not.
The rake (27° on the bike, 25° on the trike) lives on the mesh as `VehicleFitment.SteerAxisLocal` and
is spent POSING the fork; the yaw model is the kinematic bicycle and takes wheelbase, front track and
lock angle and nothing else. The proof is that each def's own full-lock radius — solved from those
three fields, with no rake term anywhere — comes out at the radius the rig publishes for the same
machine (**2.114 / 2.078 / 2.697 m** against the sidecar's **2.11 / 2.08 / 2.70**). Two derivations,
one answer, so there is nothing left for a fourth field to carry. Pinned in `AtvRideVerbTests`.

### ⚠️⚠️ THE ENDURO'S PARKED PICTURE STILL DOES NOT EXIST, AND SHE IS PARKED ANYWAY

Her rig publishes a side stand — `STAND`, `default: 1` — and says in capitals what it is for:

> she BAKES PARKED. A dirtbike does not stand upright without a rider: the roll/turn/bounce cues set
> `stand 0`, and a game that shows her upright with nobody aboard is showing a bug the rig will not
> catch.

The mesh this repo holds is `stand:0` — the RIDDEN state, 497 faces. That was the right call at
intake and it is still not fakeable: the parked build is **502 faces and a 12° lean**, a topology
change rather than a rotation, so it cannot be posed out of what is baked (`enduro-parked-vs-ridden.png`
above is the 1204-px difference). **So she stands upright on her wheels outside the shop until a
second bake lands**, and that is stated here rather than hidden.

**What is owed:** one more `VehicleMeshDef` off `build(AtvIso.resolve({body:'dirtbike',stand:1}))`,
and a drawer that shows the parked mesh when nobody is aboard and the ridden one when somebody is.
`AtvRideVerbTests.TheEndurosBakedMeshIsStillTheRiddenStateAndHerStandIsOwed` pins the gap **exactly**
— it turns red the day the parked build lands, so somebody has to come and wire it rather than the
guard quietly going on passing.

The quad and the trike owe nothing: neither publishes a `STAND` at all — three wheels and four stand
up by themselves — so parked and ridden are the same picture for them, pinned in both directions.

### And the hands are still in the air

Unchanged and unhidden: the `drive` clip's reach is **11.6–13.9 px** short of every one of these
machines' bars (`astride-gap.png` above), against 0.68 px on the Otter the clip was fitted to. The
astride stance is an upstream art ask, `AtvAstrideRiderTests` pins each residual exactly, and a rider
sitting on a saddle with her hands short of the grips is what the player sees until it lands.

### Where they stand, and what is still the owner's

The row is at the general store's frontage — derived from `StPetersBuilder.GeneralStorePos` and
`VillageGreen` through the shop yard's own gate, spaced by each machine's own published beam, noses
out toward the green (the enduro has no reverse worth the name). Nothing is typed.

They are the owner's **TEST** machines. Ruling **(a)** — which resident owns which and where each is
parked — is still owed and this does not pre-empt it: when it lands, each moves to its owner's yard
in a one-line change, because every position is derived. **(b)** the trike's recreation loop and the
transport runs, **(c)** two-up, and **(e)** a visible track on St Peters are likewise untouched.
