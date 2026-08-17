# ADR 0035 — A `Vehicles` module, and how a road vehicle differs from a hull

**Status:** Accepted · **Date:** 2026-08-17 · **Deciders:** lead-architect (ruling given on #548),
gameplay-systems (this PR) · **Supersedes nothing** · **Related:** ADR 0022 (mesh hulls), ADR 0026
(rig pivot convention), ADR 0031 (keyline retirement)

## Context

The owner's 2026-08-16 ruling put **land vehicles** in scope for the first time. The art arrived as
`vehicleIsoRig.js` (the Dually 3500, a crew-cab one-tonne dually), landed hash-verified in #548, and
was revised in #549 to add a `steer` axis and a `yaw` axis. An amphibious 8×8 (the Otter) is in the
same drop and is next.

A vehicle is close enough to a mesh hull to be tempting to fold into `HullMeshDef`, and different
enough that folding would be wrong. Both are baked from a rig through the same 40° iso camera, both
pack the same per-face shader constants, both want continuous heading. But a hull carries
`RestingDraftMeters`, `WatertightDeckHeightMeters`, `WatertightHalfBeamMeters` and three rock
amplitudes — every one an answer to *"how does the sea move this?"* — and a vehicle carries a
wheelbase, a track, a lock angle and a suspension travel. Each set is meaningless on the other.

Two further facts, both measured rather than assumed, shaped the decision:

- **`yaw` moves zero geometry.** The rig folds it into `camBasis` (`th = dir·45° + yaw`), so it is a
  *camera* parameter — which is exactly what `IHullMeshRenderer.HeadingDirUnits` already is. A mesh
  vehicle therefore reads at any heading between the eight facings for free. Baking yaw into
  vertices would have turned her twice.
- **`steer` is an exactly rigid rotation.** All 666 vertices per front corner rotate by precisely the
  published lock about the published vertical axis (max ‖Δr‖ = 3.6e-16, ‖Δz‖ = 0). So the front
  wheels lift out as fittings on the existing `HullPropMeshDef` / `IHullPropRenderer` seam, with no
  new articulation machinery.

## Decision

### 1. A module, `HiddenHarbours.Vehicles`, Core-mediated

Own asmdef, referencing Core only. It never references Boats' concrete classes and Boats never
references it (rule 4). Shared contracts live in Core (`Core/Vehicles/`).

### 2. Per-domain defs over shared bake tooling

- `VehicleMeshDef` (Core) — the baked body mesh, the rig's shading payload, the measured azimuth
  convention, and the **chassis**: wheelbase, front track, wheel radius, axle positions, the two
  Ackermann lock angles, and the suspension travels. No drafts, no seakeeping.
- `VehicleDef` (Vehicles) — mass, power, drags, steering feel, camera. Mirrors `BoatHullDef`'s
  Engine branch so an owner who has tuned a boat recognises every field.
- Id family **`vehicle.*`** (`vehicle.dually_3500`), append-only once shipped.
- `RigMeshExtractor` / `RigMeshBuilder` / `RigMeshData` are reused **as-is**.

### 3. `HullPropMeshDef` is reused for wheels, not duplicated

That type is not really "a boat part" — it is *a rig-baked rigid body with a pivot and a local
rotation*, which is exactly what a wheel is. Reusing it means `IsoFacetPropRenderer` poses a wheel
and an outboard through one seam, and the vehicle path needs **no new Art renderer**. The name is a
historical accident; renaming it is a repo-wide churn that would buy nothing.

### 4. A separate presentation service, `IVehicleMeshPresentationService`

Not another method on `IHullMeshPresentationService`. Installing a *hull* also attaches a
`FoamInjector` (so she churns the wake-foam buffer) and a `ReflectiveObject` (so she appears in the
water), and hands the renderer a waterline clamp. A Dually parked on Wharf Road must do none of
those. A separate entry point cannot make that mistake; a shared one relies on remembering.

The **pose** seam is shared — `IHullMeshRenderer` is handed back — because its four channels
(heading in rig dir units, roll, pitch, heave) are exactly what a truck needs.

### 5. Steering and yaw are coupled in `VehicleSteeringMath`, because nothing else will

The rig's own sidecar states the gap plainly:

> STEERING moves the wheels only; YAW moves the machine. Nothing couples them — a game that turns
> the wheels without yawing (or the reverse) will look wrong, and the rig will not stop it.

The model is the **kinematic bicycle** referenced at the rear axle centre — no slip, no load
transfer, no tyre model — which is the same model the rig's own published turning radii are computed
under. Both the drawn wheel angles and the machine's yaw rate are derived from one steer number
through one set of published lock angles, so they cannot disagree.

Speed-sensitive steering reduces the **wheel**, not the yaw. Softening the yaw alone is precisely
the failure the sidecar warns about.

### 6. Numbers are derived from the rig, never transcribed

The full-lock turn radius to the rear axle centre is **8.348 m**, computed from the rig's own
Ackermann angles. Her sidecar publishes **8.29 m** (and 10.15 m for the outer front wheel path
against a derived 10.198 m) — a 0.7% rounding artefact in a hand-transcription. We take the circle
the drawn wheels are actually pointing at. Pinned by test so the choice is on the record rather than
a discrepancy someone later "fixes" toward the sidecar.

### 7. Azimuth is measured from the rig's ANCHORS, never from its silhouette

`RigAzimuthProbe` finds the bow **by taper** — it calls the narrower end of the silhouette the bow.
That is a fact about boats. A crew-cab dually is a box, blunt at both ends, so her taper carries no
signal at all; and the same heuristic has already been measured wrong on eighteen lobster hulls at a
taper ratio of 1.040. The vehicle baker therefore **refuses to consult the taper** and reads the
rig's own `anchors()`:

| Oracle | Reading (2026-08-17) | Verdict |
|---|---|---|
| Front-axle abeam pair (`wheelFL`/`wheelFR`), un-squashed ground bearing at dir 2 | exactly **−90.00°** | CCW |
| Centreline fore-and-aft pair (`hitch`→`hoodLatch`), screen dx at dir 2 | **−202.24 px** (nose west) | CCW |

The eight headings land on exact 45° steps, which is the self-check that the `sin(elevation)`
un-squash is right. A disagreement between the two oracles is a hard **error**, not a tie broken
quietly.

## What the mesh path cannot draw, and we are shipping anyway

Two honest limits, both measured, both stated here rather than discovered later.

**1. Procedural `tex` is dropped entirely.** The rig shades 144 of her 1153 faces through
*procedural texture closures* — 84 rubber faces carry the tyre tread (`(u+phase) % c`), 58 paint
faces carry the weathering speckle, plus two trim details. `RigMeshExtractor` never reads a face's
`tex` or `uv`, and `RigMeshBuilder` packs UV0 with `(materialId, b, db, 0)`. So the mesh path draws
her tyres and her paint **untextured**. This is the same family as the existing
`mesh-path-does-not-model-dith` limitation and applies to the whole mesh fleet; the truck is simply
the first rig where it is this visible a fraction.

**2. Wheel roll is an approximation; steer is exact.** The rig **re-tessellates** the hub each roll
phase rather than rigidly rotating a fixed vertex set — at a quarter turn the per-vertex angles
spread 76°–108° for a 90° step and the radius wanders by up to 7 cm. A baked mesh rotated about the
axle is therefore *close to* what the rig draws (the median tracks `rev × 360` within ~2°) but is
not a reproduction, and cannot be adjudicated against the rig's own sheets the way a hull is. The
most visible cue of a spinning wheel — the tread stripes — is a `tex` closure the mesh path cannot
carry at all, so what actually rotates on screen is the hub lugs and index notch.

We ship the rotation anyway: it reads as a turning wheel at 32 px/m, and the alternative is wheels
that never turn. The rate is `v / (2π·r)` **revolutions** — not `v/r`, which is 2π ≈ 6.3× too fast.

⚠️ A related measurement trap, now pinned: `roll:1` ties with `roll:0` **exactly**, so a probe that
tests the axis at 1 concludes it is dead. Probe at a quarter.

## The articulation split

A baked mesh is static geometry at one pose, so every part that articulates must become its own
mesh — otherwise the body draws a second, frozen copy of it. Which faces belong to which body is
**measured** (build the face list at two poses, keep what moved), and the partition is asserted
disjoint and covering rather than trusted:

| Group | Faces | How it moves |
|---|---|---|
| Body | 661 | static |
| `WheelFL`, `WheelFR` | 103 each | steer ∘ roll |
| `WheelRL`, `WheelRR` (each a dual pair) | 103 each | roll |
| `KnuckleFL`, `KnuckleFR` | 40 each | steer only |
| **total** | **1153** | = her whole face list, exactly |

Both rotations of a front wheel pass through the hub centre — the rig models no kingpin offset,
caster or scrub radius — so **one pivot serves both**, and a single `IHullPropRenderer.LocalRotation`
carries the corner.

⚠️ The order of the pose axes in `VehicleRigFleet` is load-bearing: each claims only what no earlier
axis took, so the per-wheel roll axes must take the tyres before the steer axes are asked what is
left. Listing steer first would swallow both front wheels into the knuckle fittings; the baker fails
loudly on that rather than shipping it.

## Consequences

- A second vehicle is an asset and a catalog row, not a class. The Otter's amphibious behaviour
  (ADR to follow) hangs off a kind discriminator, not off a new def type.
- `VehicleRigFleet` is now a working coverage law with a populated `Baked` list and an empty
  `NotBaked`. A drop that is in neither fails.
- The vehicle path takes the render the hull path is A/B-pinned against (both waterline clamps at 0,
  the documented "clamp off"), so nothing about the boats moves.
- **Not built here, and deliberately:** a player control mode for driving. `ControlSwitcher` owns
  the on-foot/at-helm state machine and lives in the Player lane; adding a Driving mode is its own
  change with its own camera handoff. The ruled path for the door interaction is an `IInteractable`
  registration (the dev-key ledger is exhausted A–Z and the pressure valve was shipped in #503), not
  a new key binding.
- **Not built here:** a pixel golden-master comparing the composed vehicle (body + six fittings)
  against the rig's own render. The hull path has one; a vehicle's would additionally have to model
  the two limits above. The structural guarantee that stands in the meantime is the exact partition.
