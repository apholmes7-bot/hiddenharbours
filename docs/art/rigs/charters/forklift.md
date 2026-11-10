# Wharf forklift — rig charter

Status: **Proposed, M3 industrial handling**. **art-director** owns source and gameplay sidecar; **art-pipeline** owns import, load sprites and depth/contact proof. gameplay-systems owns rear steering, lifting/retention and payload limits; economy-sim owns equipment progression; lead-architect owns Core/save integration; world-content owns the hard-standing work area. This file changes no rig or asset.

## The job and scale

A small counterbalance forklift on firm wharf/store-yard ground, moving a compatible pallet or fork-pocket tub. Candidate body 2.5 × 1.2 × 2.2 m, plus 1.1 m forks; about 1.6 m wheelbase. Proposed fork width/thickness 0.10/0.04 m, adjustable centre spacing 0.35–0.80 m, heel-top height about 0.08 m lowered to 2.5 m raised. These are starting geometry requests, not a selected machine's specifications. Payload is a Def curve by lift height and load centre; begin review in a 1.5 tonne class, never assume that capacity at every pose. Fork pockets must fit the actual blade and spacing.

## Export and frame contract

Read the [Dually](../dually-iso-kit/vehicleIsoRig.js) and [ATV](../atv-pack/atvIsoRig.js) sources for the house pattern. Proposed `WharfForkliftIso` export: `W,H,PX,DIRS,pivot,order`, `dims(opts)`, `resolve(opts)`, `render(dir,opts)`, `anchors(dir,opts)` and generated gameplay geometry. Ratify names before implementation; exports then append-only. Existing extractor face/material exports must be declared, not supplied by a hidden shim. Tool extensions belong to tools-editor.

1 unit=1 m, PPU=32; +x curb/right, +y forward toward the forks, +z up. Origin ground centre of the chassis, tyre contacts z=0; publish measured eight-facing azimuth/pivot. Sidecar includes `derivedFromRigSha256`, units, source expressions/provenance and the **posed** mast/fork transform. Existing look/palette/lighting remain the art roles' decision, not a new direction in this brief.

## Required sections and nodes

| Section | Contract |
|---|---|
| `BODY` / operator | Chassis and overhead-guard bounds, counterweight, clearance, seat, wheel/levers/feet, mount points and rear swing envelope. No cargo slot on the counterweight. |
| `WHEELS` / `STEERING` | Four named contact/roll nodes; front drive wheels fixed in yaw, **rear** kingpins/steering links and their signs/limits. Wheelbase and turning geometry are measured. A pickup's front-steer animation is not reused as the visual truth. |
| `MAST` | Base pivot, rail axes, carriage node, stages and maximum travel, mast tilt axis/limits. Expose lift in metres (or normalized value with exact metric conversion), tilt in degrees, and which nodes move together. |
| `FORKS` / load | Left/right blade nodes, heel/tip/support planes, spacing axes/range, insertion and pocket-clearance envelopes, load-centre reference, backrest plane. Expose a supported load frame derived from the actual top surfaces at every lift/tilt. No cargo “snaps up” before blades are under it. |
| `INTERACTABLES` | Mount, lift/lower, tilt, fork spacing if implemented, engage/release, park; reach/access facts and required stopped state. No driver-seat sale or remote tub selection. |
| `POSE` | Independent wheel roll, rear steer, lift, tilt, spacing and continuous heading. Absences such as side-shift or telescopic boom are explicit; do not add phantom controls. |
| `SORT` / support | Ground sort origin, mast rails/backrest, near fork/body occluders and elevated load frame for eight facings. Load depth follows its physical height/tilt and position: above forks, in front of backrest, correctly occluded by near mast/guard. Do not globally increment sort order to fake elevation. |

Art supplies contact geometry; gameplay-systems supplies capacity/load-centre/tip-prevention data and valid operating envelope. A load can be supported by forks or a receiving bed, never neither. Transfer support only after overlap, clearance, height and payload checks. Lifting loose animals directly on forks is forbidden; use compatible tub/pallet. A sealed/lidded crate's contents remain owned by that crate while lifted.

## Delivery and acceptance

- art-director delivers source, generated sidecar, exporter contract and poses empty/engaged/raised/tilted; no full tub baked into the forklift body. art-pipeline imports through the existing vehicle/fitting path, tools-editor owns any new mast extraction support.
- qa-test/tools-editor assert wheel contacts and fork support at min/max lift, both tilt bounds and both rear steering locks; posed anchors agree with the rig. Verify a pallet/tub's pockets clear the actual blades.
- gameplay-systems proves approach→insert→engage→lift→carry low→lower→release, with rejected over-capacity/over-height/load-centre moves changing no ownership. Front wheels and rear wheels do not exchange their steering roles.
- art-pipeline supplies all-eight-facing empty/loaded contact plates plus elevated and tilted cases; bottom of load rests on forks/bed, no hovering, and mast/rim occlusion preserves existing appearance. world-content supplies a usable loading bay with enough rear-swing/overhead clearance.
- lead-architect proves save/reload with an engaged raised tub retains one support parent, mast pose and all contents; an unsupported restore retains the record for explicit recovery rather than dropping or teleporting cargo.

Deferred: workplace injury, load fall physics, staff operation, warehouse routing, side-shift, rough-terrain forklift and reach stacker. They require separate scope and data.
