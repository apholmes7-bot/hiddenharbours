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

Three honest limits, all measured, all stated here rather than discovered later.

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

**3. A drawn driver is not occluded by the machine she is sitting in.** The mode shipped with the
player hidden in every machine, which was right while the only one was a hard cab. An OPEN machine
draws her driver now (rig 6.5's `drive` clip, placed on the seat her rig publishes — see
`VehicleMeshDef.DriverSeatLocal` and `PlayerDrivePresenter`), and the figure is Y-sorted in the decor
band while the vehicle composes at her own whole-object slot beneath it — so the driver draws over
**all** of the machine, gunwale and rack included, rather than being cut by the parts genuinely in
front of her. The hull fleet solved the same problem per pixel, against the mesh's own z-buffer
(`IsoFacetHullRenderer.SetDeckOccupant`, and the vehicle path uses that very renderer), but reaching
it needs the PLAYER's body renderer to carry the occludable material — a change to the character's
whole presentation stack, shared with the wardrobe swap and the submersion shader, and not something
to bolt on beside a pose.

We ship it anyway, and the choice of first machine is why it is affordable: the Otter's cockpit is an
open tub, her seat sits 0.76 m up in it and her rails top out at 0.86 m, so what should cover the
driver is a few pixels of near gunwale at some facings — against a driver who was not drawn at all
before. A hard-cab machine, where the error would be the whole figure, publishes no open seat and is
untouched. Wiring the occluder is the follow-up, and it is a character-presentation task rather than
a vehicle one.

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

## Amendment 2026-09-02 — the drive-input seam, and the coupling's frame (driveable charter, PR 0)

Two facts about the module the original text did not have to state, both surfaced by the road
fleet's laydown (#692) and settled in PR 0 of the driveable charter.

### The demand crosses ONE seam: `IDriveInputSource`

`ControlSwitcher` used to read `Keyboard.current` inline every frame a machine was driven. With no key
held — every frame of a headless run — that read landed a zero demand and overwrote anything a test had
asked for: a journey that set full throttle and stepped thirty seconds of physics measured 0.00 m, and
the failure pointed at the yard. The read now goes through `IDriveInputSource` (Core, beside
`IDriveSeat`). `KeyboardDriveInputSource` (Player) is the shipped source, byte for byte the mapping it
replaces — W/S throttle, A/D wheel, Space brake, LEFT is +1, the rig's own sense, pinned by
`DriveInputSourceTests`. `HeldDriveInput` (Core) holds a demand across frames for a scripted driver, a
replay, or an NPC at a wheel; `RoadFleetJourneyPlayTests` drives every machine in the built Nine Mile
Creek data through it, via the real switcher. A gamepad is another implementation of the same interface:
the socket exists, the device does not. ⚠️ This is not the intent layer `ux-and-mobile-controls.md` §9
describes — the walk and the helm still read raw keys, and unifying the three is a project-wide input
lane, not a vehicle change.

### The coupling lives in the transform frame

`BoatKinematics.BearingDegrees(transform.up)` — 0 north, clockwise — is the one heading convention, and
its inverse is `z = −bearing`. `VehicleCouplingMath.BodyOriginFromKingpin` and `TowedBody.KingpinWorld`
rotated the other way. They agreed with the drawn trailer only where `sin(heading) = 0`, which is why every
coupling fixture stood its tractor north and the laydown was sited due south; at 90° the arithmetic put a
pup's kingpin 6.73 m from where she was drawn, and a pair coupled off the north–south axis silently lost
the hitch. `VehicleCouplingMath.LocalOffsetToWorld` is now the one rotation (nose along
`NavMath.DirectionFromBearing`, curb side a quarter turn clockwise of it); both readers and the laydown's
own placement use it; `VehicleCouplingTests` sweeps capture, follow and release through four headings
against the TRANSFORM as the oracle, and `NineMileCreekLaydownTests` spins the pair through eight. The
yard's heading is layout now, not a constraint. Law, restated: a fixture at the one heading where a
mirror vanishes proves nothing about the mirror — the 90° fixture was written first and watched fail.

## Amendment 2026-09-04 — a driver who is not the player (road fleet PR 5)

Owner's ask: *"i want npcs to be able to enter and drive vehicles, lets set up some basic routes."*
Three additions, none of which change anything that was already true of the module.

### The route-following maths is Core's, and there is one copy of it

`RouteFollowMath` (Core, beside `IDriveSeat`) is the driver `RoadFleetJourneyPlayTests` measured in
PR 0: a waypoint is passed when she crosses its perpendicular, not when she touches it (a turning circle
wider than the reach otherwise orbits an overshot point for ever); a leg on a road ends only when she is
far enough along AND inside the carriageway's half-width; and she steers at a lookahead point re-derived
every step, so she converges onto a road rather than crossing it once. Its feel lives on `VehicleDef`
(rule 6) and is filled in from the measured driver for any def baked before the fields existed. The
journey fixture's private copy is deleted and it calls these functions, so the game and the fixture
cannot disagree about how a machine follows a road. `RouteDriver` (Vehicles) is the live
`IDriveInputSource` built on it — the socket the amendment above described, with a device in it at last.

### A scheduled trip is POSED from the clock, not integrated

`VehicleTripPlan.SampleAt(hour)` is pure, allocation-free and total: eight blocks (rest · board · drive ·
alight, at each end), of which **two hours are authored on a `VehicleTripDef` asset and six are derived**
from how long each leg takes at its own speed. Nothing is ticked or saved (rule 5): a save taken mid-trip
carries no trip state and a region loaded at 06:12 draws the truck where 06:12 puts her.

Chosen over a live `RouteDriver` on a real `VehicleController` because a truck is kinematic by design
(above), because the sea fleet already settled the half of the argument that matters
(`AmbientFleetSchedule` is pure and its presenter's join rule is *recompute, don't replay*), and
decisively because **a truck's route is a ROAD**: a body posed on the centre-line cannot wander off the
carriageway, and PR 0 measured live integrators ending 3–16 m off the line. The live driver still exists
for the fixture and for a future cruise control, and shares the maths.

⚠️ **A posed body cannot reverse**, and there is one road into a truck park — so the way she arrives is
the reverse of the way she leaves. She turns on the spot across the boarding block instead, while her
driver crosses the gravel to her. That is a pivot, not the three-point turn the park is sized for; an
astern flag on a leg is the honest fix and is a follow-up.

### A trip that TOWS is ten blocks, and it can only run on a pull-through (PR 6a, 2026-09-07)

A `VehicleTripDef` may name a `TowedBodyId`. The plan then grows two beats — `Coupling` and
`Uncoupling` — in which the driver walks to the **street-side release the player works**, the pin goes in
and the landing gear winds up on the **crank the player turns**. Still only two authored hours: a beat's
length is the walk it contains, so eight of the ten are derived.

**The trailer is not a second kinematic model.** `Core.TowedFollowTrack` walks the road once at build
time carrying her heading through `VehicleCouplingMath.FollowStep` — the function
`TowedBody.FollowKingpin` calls every `LateUpdate` when the PLAYER is towing. After the walk a sample is
two array reads and one rotation, so rule 5 survives intact: nothing ticked, nothing saved, a region
streamed in at 06:12 draws the pair where 06:12 puts them. `TowedFollowTrackTests` drives a live
`TowedBody` over the track's own stations and asserts **bit equality**, which is the only honest bar when
there is one transcription rather than two.

⭐⭐ **THE PULL-THROUGH LAW, and it is a constraint on the WORLD.** Three facts collide: a posed body
cannot reverse; a coupled pair cannot pivot in a bay (the eight-block turn would sweep a 53-footer
through the bays either side); and a trailer is re-derived from the clock rather than saved, so the day
must CLOSE — she has to end it on the plate she was picked up off or she teleports at midnight. Together
they force **both ends of a towing route to be pull-throughs**: she leaves each bay on the heading she
arrived on. `VehicleTripPlan.Build` refuses anything else by name, with the miss measured in metres off
the slot's centreline and degrees across its window, and the refusal is the shipped capture test
(`VehicleCouplingMath.WouldCapture`) rather than a tolerance invented in the planner.

**An ordinary there-and-back can never satisfy it**, which is why there is no towing run at Nine Mile
Creek yet: the region's four roads meet at two junctions — a tree — so every errand doubles back, and all
three shipped runs measure **180.0° apart at both bays against the slot's own 8.53°**.
`NineMileCreekTowingTests` records that, and proves it is the roads and not the pair by building the same
spec on the same road with a turning head sketched at each end. What is owed is ground.

**Where she rests is SOLVED, not authored.** "Drive out, then drive home" is a map from her resting
heading to itself, and a contraction (a trailer always swings toward her tractor), so it has one fixed
point — which is where a trailer on that road actually ends up. The plan walks the day a few times to
find it and builds the two tables from it, so the pose plan is exactly periodic and there is no snap at
midnight. The region's authored bay is the seed and the sanity check.

⚠️ **A trailer under tow is CLAIMED** (`TowedBody.HeldBy`), the same answer `DriveSeats` gives for a
wheel: `VehicleHitch.CapturedTrailer` will not offer a body somebody else is posing, because you cannot
pull a pin at 25 km/h and two clocks must not write one transform. Standing on her legs in her bay she is
anybody's — the narrowest reading, and the owner's to widen.
### A loaded pair turns wider and slower — half solved, half the owner's (PR 6b, 2026-09-07)

Nine of the fleet's ten `VehicleDef` assets carried the **identical shipped default**: a 200 cc trike, a
utility quad, a hightop van and a 53-ft-capable highway tractor all doing 11 m/s, 4.5 m/s² and two full
locks a second. Only the Otter had ever been authored. Every machine now carries her own envelope, and
the split between what is solved and what is tuned is the point:

⭐⭐ **The coupled pair's STEERING is solved, from published art alone.**
`VehicleCouplingMath.CoupledSteer` scales the TANGENT of the tractor's lock by
`wheelbase / (wheelbase + L)`, `L` being the trailer's own `KingpinToAxleCentreMeters`. That makes her
tightest circle **exactly** `(wheelbase + L) / tan(δ)` — the circle a rigid vehicle of the pair's whole
length would draw. Nobody tunes a pair: the Aero keeps 0.497 of her lock behind a 28-ft pup (8.40 m →
18.43 m circle) and 0.314 behind a 53 (→ 29.65 m), because those are the bodies' published lengths.
Scaled in tangent space rather than on the angle, because `tan` runs 15 % above its angle by the 30–35°
these machines lock to, and full lock is where the number matters.

Narrowing the lock is also what keeps `JackknifeCapDegrees` honest: its own note warns that a pair you
cannot fold further reads as a truck that stopped steering, and a driver with all her lock available
lives against that cap. With the lock narrowed, the cap is the emergency it was written as.

**Her SPEED, acceleration, braking and coast under load are FEEL, and they are the owner's** — four
fractions on `VehicleDef` (`TowingSpeedFraction` and friends), because the game models no load and there
is no honest arithmetic that turns a trailer into a stopping distance. `DriveEnvelope.With(load)` is a
narrowing stacked on whichever medium she is in, exactly as the skid model's is.

**The falloff half-speed is derived and then authored.** Speed-sensitive steering bites where lateral
grip runs out, `v_half = sqrt(a_lat · R_min)` with `R_min = wheelbase / tan(inner lock)` — both the art's.
One lateral constant serves the fleet, **anchored so the Dually keeps the 9 m/s she shipped with**, so
nothing the owner has already driven changes and every other machine moves by her own geometry. The
values live on the assets where he can move them; what `RoadFleetEnvelopeTests` holds is the ORDERING
(a tighter circle loses her steering sooner), never the numbers — a fixture that pinned a tuning gate's
values would go red the first time he used it.

### Occupancy is a registry, not a flag

`DriveSeats` (Core) answers *is somebody other than the player at this wheel?* — the shape `HelmSlot`
took when the intro skipper needed a boat's wheel without going through the player's switcher. A truck
on a trip is claimed from the moment her driver sets off for the door until he is back on his feet at the
far end; `VehicleDoor.IsAvailable` goes false (a refusal by silence, the same one scenery gets) and
`ControlSwitcher.TryEnterDriving` re-reads the same gate. The player is deliberately NOT a claimant — the
switcher already owns "the player is driving", and storing it twice is how two answers start disagreeing.
