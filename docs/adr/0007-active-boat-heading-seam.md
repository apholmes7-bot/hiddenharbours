# ADR 0007 — Active-Boat Heading Seam: a pull-style Core contract (`IActiveBoatService`)

- **Status:** **Accepted** — lead-architect. Implements the Core seam VS-19 (HUD compass +
  set-&-drift predictor + apparent wind) is built on. The downstream HUD widgets are a separate
  **ui-ux** change that consumes this contract; this ADR + change deliver only the seam.
- **Date:** 2026-06-21
- **Decision owner:** lead-architect (Core contract / cross-module integration, CLAUDE.md rule 4)
- **Flagged from:** the VS-19 HUD wind PR (#29) — the wind widget shipped with **absolute** wind
  only; `WindReadout.cs` documented that *wind relative to heading* needs "a Core boat-heading
  contract that does not exist yet."
- **Related:** `0004-perspective-and-scene-strategy.md` and `0006-boat-art-pipeline.md` (both turn
  on the **heading vs course-over-ground / crabbing** read this seam exposes), `project-structure.md`
  §5 (cross-module talk via Core), `Core/Events/GameSignals.cs` (`ActiveBoatChanged` — the existing
  Boats→App-via-Core handoff this mirrors), backlog VS-19.

## Context

The HUD (`HiddenHarbours.UI`) references **only** `HiddenHarbours.Core` by design, which structurally
prevents it from reading the active boat's heading/velocity — those live on `BoatController` in
`HiddenHarbours.Boats`. VS-19 needs three reads the HUD can't currently get:

1. a **heading compass** (where the bow points),
2. a **set-&-drift predictor** (a ghost-track of where the boat will actually go — heading vs
   course-over-ground), and
3. **apparent wind** (the existing wind vector expressed *relative to heading*).

All three need the same missing primitive: the active boat's **heading + course-over-ground**,
delivered through Core without the UI referencing the Boats module. The camera already solves an
analogous problem — `ActiveBoatChanged` carries a boat's framing from the Boats/Player lane to the
App camera through Core, so the camera never references Boats (`CameraFollow`). This is the same
shape of problem.

A timing caveat (caution from the brief): PR #26 had just landed **differential hand-rowing** for the
dory, so "heading" semantics for a rowed boat needed confirming before freezing a contract.

## Decision

Add a **pull-style** Core contract and a Boats-lane producer.

1. **`Core.BoatKinematics`** — a small immutable snapshot mirroring `EnvironmentSample`:
   `HasBoat`, `HeadingDegrees` (bow bearing), `Velocity` (course-over-ground, world m/s),
   `SpeedOverGround`. It carries the pure, deterministic bearing math (`BearingDegrees`,
   `RelativeBearingDegrees`, `CourseOverGroundDegrees`, `FromBow`) so the producer and the UI share
   **one** convention.
2. **`Core.IActiveBoatService`** — `bool HasActiveBoat` + `BoatKinematics Sample()`, exposed at
   `GameServices.ActiveBoat`. Optional and **not** part of `GameServices.Ready` (like `Wallet`): it
   is null on foot / before boarding, and consumers null-check it.
3. **`Boats.ActiveBoatProbe`** — a tiny `MonoBehaviour : IActiveBoatService` that reads the active
   `BoatController`'s bow (`transform.up`) and rigidbody velocity. It **self-registers** into
   `GameServices.ActiveBoat` on enable and clears it on disable.

The HUD **pulls** `GameServices.ActiveBoat.Sample()` on its existing ~4 Hz environment cadence.

### Pull, not push

The pushed alternative was a per-physics-tick `ActiveBoatKinematicsChanged` event (throttled). We
chose pull because:

- the HUD **already** samples `GameServices.Environment` at ~4 Hz — the boat read simply joins that
  loop, one cadence, no new subscription bookkeeping;
- it avoids **per-tick event spam** from `FixedUpdate` and the throttling logic a push would need;
- a `Sample()` returning a `struct` allocates nothing per call (the HUD's no-per-frame-GC rule).

### Scene-scoped, self-registering (not wired on GameRoot)

Unlike the clock/environment — persistent boot singletons wired once on the persistent `GameRoot` —
the **active boat is a per-scene, per-hull, runtime** thing (it changes hull on a purchase; it would
change object per region in M2). A serialized reference from the persistent `GameRoot` to a scene
boat is exactly the fragile cross-scene ref we avoid. So the probe self-registers, the same way the
Boats/Player lane already self-*publishes* `ActiveBoatChanged`. The probe only ever touches **its own
module + Core**, so it does **not** usurp `GameRoot`'s role as the cross-module composition root.

### Heading semantics for the rowed dory (the caution, resolved)

- **Heading = the bearing of the bow (`transform.up`).** This is the hull's facing, well-defined no
  matter whether the differential **oars** (PR #26) or a **rudder** turned it — rowing changed *how*
  the hull yaws, not what "which way am I pointing" means.
- **Velocity = the rigidbody's `linearVelocity` = course-over-ground**, wind- and current-set
  included. Heading ≠ course-over-ground when drifting — that difference **is** the set-&-drift /
  crabbing read VS-19 visualises and ADR 0004/0006 protect.

Both are already `BoatController`'s **public surface** (`public Vector2 Velocity`; the documented
`transform.up` = bow), so the seam adds **no new physics meaning** and nothing PR #26 destabilised.
The semantics are pinned by an EditMode test so a future gameplay change can't silently redefine
them. (gameplay-systems to confirm on review per rule 4.)

### Bearing convention

Compass bearing: **0 = North (+Y), 90 = East (+X), clockwise, [0, 360)** — identical to the wind
widget (`WindReadout`). A test cross-checks `BoatKinematics.BearingDegrees` against
`WindReadout.Cardinal` so heading, wind, and course can never disagree on which way is North.

## Consequences

- **Zero coupling added.** UI still references only Core; Boats still references only Core. The new
  contract is Core-owned; the producer is Boats-owned; the greybox wires the probe onto the dory in
  `GreyboxBuilder`.
- **VS-19 is unblocked** (ui-ux): build the compass, the ghost-track/set-&-drift predictor, and
  apparent wind by sampling `GameServices.ActiveBoat` and composing
  `BoatKinematics.RelativeBearingDegrees`. `WindReadout`/`HudController` may later **delegate** their
  duplicated atan2 bearing logic to the Core helper to dedupe (a UI-lane cleanup, not done here).
- **No save impact.** Kinematics are recomputed from the live boat, never serialized.
- **Tests:** EditMode pins the bearing/relative math, the convention cross-check, and probe
  gating/forwarding; PlayMode pins self-registration + the live rigidbody velocity path.

## Rejected alternatives

- **Push event each physics tick** (`ActiveBoatKinematicsChanged`, throttled) — per-tick spam +
  throttling complexity for no benefit over a 4 Hz pull that matches the HUD's existing loop.
- **Serialized `ActiveBoatProbe` ref on the persistent `GameRoot`** — fragile cross-scene reference;
  doesn't model the active boat's per-scene/per-hull runtime lifetime. Self-registration does.
- **UI reaches into Boats / `BoatController`** — breaks the UI→Core-only boundary (rule 4); the whole
  reason the seam exists.
- **Put heading on the existing `ActiveBoatChanged` event** — that event fires on a *hull swap*, not
  per motion update; heading/velocity change continuously and are a *pull*, not an edge.

## Open questions (later)

- **Multi-boat (M2):** which `BoatController` is "active" becomes a real choice; the probe (or a
  small manager) re-points at the active hull. One probe suffices for the single-dory slice.
- **World position:** intentionally **not** in the contract — the compass and a screen-space
  ghost-track need only heading + velocity. A *world-space* drift overlay would extend `BoatKinematics`
  with position; add it only when that widget is actually built.
