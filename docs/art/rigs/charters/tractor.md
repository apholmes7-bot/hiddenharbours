# Shore tractor and trailer — rig charter

Status: **Proposed, M3**. **art-director** owns source/sidecars; **art-pipeline** owns import and contact/load plates. gameplay-systems owns driving, towing and load access, lead-architect the Core/save seam, world-content the yard route. This is a compact farm/yard tractor, distinct from the existing Road Fleet semi-tractors. No source or asset changes here.

## The job and scale

Tow containers along a working shore on a real trailer. Proposed tractor envelope 3.2 × 1.6 × 2.3 m, wheelbase about 1.8 m, large rear and smaller front tyres. Proposed single-axle trailer clear bed 2.4 × 1.5 m at z=0.60, overall envelope about 3.5 × 1.7 × 1.2 m including drawbar and sideboards. Final dimensions must come from the delivered rig. Trailer payload starts as an economy/gameplay review around 1,000 kg; drawbar load and towing/braking limits are separate Def values. An industrial tub needs a hoist/forklift to load; the tractor does not grant a hand lift.

## Export and frame contract

Use the [Dually](../dually-iso-kit/vehicleIsoRig.js) / [ATV](../atv-pack/atvIsoRig.js) pattern: proposed `ShoreTractorIso` and separately identified `ShoreTrailerIso` exports, each self-contained or sharing an explicitly versioned helper. Each publishes `W,H,PX,DIRS,pivot,order`, `dims`, `resolve`, `render`, posed `anchors` and generated gameplay geometry. Agree names before consumers; then append-only. Existing face/material exporter requirements still apply. Any new coupling import tool belongs to tools-editor.

1 unit=1 m, PPU=32, +x curb/right, +y tractor nose, +z up; ground origin and eight facing convention measured and declared. Trailer has its **own** local origin/frame. Sidecars include `derivedFromRigSha256` and source-derived geometry/provenance. No copied Dually wheelbase, bed or fifth-wheel anchor.

## Required sections and nodes

| Section | Contract |
|---|---|
| `BODY` / operator | Tractor footprint, collision/swept bounds, clearance, seat, wheel, pedal/foot and mount/step anchors. Publish cab/roll-frame obstruction geometry; do not occupy those spaces with goods. |
| `WHEELS` / `STEERING` | Tractor FL/FR/RL/RR nodes with radius, roll axes and contacts; front kingpins/Ackermann bounds, rear drive roll, steering-wheel node. Trailer axle/wheels roll in its own frame. Differential roll is explicit, no single radius assumed for front and rear. |
| `TOW` | Tractor drawbar pin or ball **chosen explicitly**, hitch centre/height and approach, trailer drawbar eye/coupler, pivot limits, clearance sweep, parking support/stand, brake connection if present. This is not a semi kingpin/fifth wheel. Absence of loader/three-point attachments is declared. |
| `CARGO` | Trailer floor support polygon/height, board and wheel-well intrusions if any, open-gate access, latches/restraints and allowed geometric stack clearance. Tractor has **no cargo bed**. Load points ride trailer articulation, not tractor yaw. |
| `POSE` | Tractor steer/wheel roll, any suspension/axle oscillation, trailer hitch yaw/pitch limits, gate rotation and parking stand travel with units. All supports and interaction anchors follow posed geometry. |
| `INTERACTABLES` | Mount, couple/uncouple, park, gate, load/unload; reachable points off the machine, access sides and visual-facing facts. Uncoupling requires a supported parked trailer, not a load hanging from the hitch. |
| `SORT` / depth | Ground contact origins for both bodies; trailer bed/load plane; near boards, tractor rear tyres and operator occlusion groups. Publish load order per eight facings and through articulation so a turning trailer load does not draw through the tractor. |

## Delivery and acceptance

- art-director delivers tractor and trailer contracts separately, optional helper dependencies listed, all geometric numbers derived from the sources; no cargo fish baked into the vehicle body.
- tools-editor/qa-test verify anchor transforms at straight, both turn limits, gate open and stand down; wheel contacts and supported-load base remain on their declared surfaces. gameplay-systems validates the existing Core towing model or proposes an additive drawbar seam; never reinterpret a fifth-wheel field silently.
- art-pipeline proves empty and loaded trailer at all eight headings plus one articulated turn: wheels on shore, load on bed, no hover or outline/sort regression. world-content owns an accessible firm-shore route and clear loading bay.
- gameplay-systems proves manual load→secure→tow→park→unload, refusing over-envelope/gross load and uncoupling without support. lead-architect proves trailer contents, attachment and position survive reload exactly once.

Deferred: front loader, forks/bucket, powered winch, shore terraforming, staff driving and road registration detail. A loader is a new machine contract, not a pose smuggled into this one.
