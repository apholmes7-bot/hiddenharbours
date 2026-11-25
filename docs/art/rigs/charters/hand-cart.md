# Hand cart — rig charter

Status: **Proposed, M2**. For **art-director** (source and gameplay sidecar) and **art-pipeline** (import, load sprites and contact/occlusion proof). gameplay-systems owns push/carry mechanics; lead-architect reviews Core; world-content owns route clearances. No rig or asset is changed by this brief.

## The job and scale

A plain four-wheel wharf/platform cart, not a two-wheel sack truck: low bed, upright handle, two fixed wheels and two swivel casters. Candidate overall 1.20 m long × 0.70 m wide × 0.90 m high; clear bed 1.00 × 0.60 m, top at z=0.30 m; wheel diameter around 0.20 m. These are proposed source dimensions to confirm before authoring. A small 0.70 × 0.50 tote can sit lengthwise; two 0.65 × 0.45 pans need an expressly measured arrangement/stack, not a guessed second slot. Gross payload is a gameplay Def dial (start review at 100 kg), never an inferred art fact. Cannot carry a full industrial tub.

## Export and frame contract

Follow the [Dually source](../dually-iso-kit/vehicleIsoRig.js) and [ATV source](../atv-pack/atvIsoRig.js) house style: a self-contained source with proposed export `HandCartIso`; `W,H,PX,DIRS,pivot,order`, `dims(opts)`, `resolve(opts)`, `render(dir,opts)`, `anchors(dir,opts)` and a gameplay-sidecar generator. Art-director ratifies names before any consumer depends on them. Exported symbols then become append-only. Use the existing extractor contract and declare required face/material exports for the chosen pipeline; tool changes are tools-editor's work, not hidden rig shims.

Metric axes: +x right/curb, +y cart nose, +z up; origin ground centre, wheel contacts at z=0. True scale 1 unit=1 m, PPU=32, existing ¾ projection. Publish pivot, eight facing labels, measured azimuth convention and provenance; do not infer turn direction from a sign. No new palette or rendering style is specified.

## What the rig must expose

| Section | Required facts |
|---|---|
| `BODY` | Outside footprint, collider outline, bed rim and handle obstructions; clear loading opening and operator swept space. |
| `CARGO` | Bed support polygon/top height, local load origin, optional two pan slots derived from actual fit, lip bounds, maximum geometric stack clearance, and proposed four strap/latch anchors. Cargo is separate content, absent from the empty rig. |
| `PUSH` | Left/right handle grips, operator feet/rest reach point, push direction; grips follow handle geometry. Both player hands are occupied while pushing. |
| `WHEELS` / `STEERING` | Four named wheel nodes, centres, radii, roll axes and contact points; caster kingpin axes/offsets and swivel bounds. Fixed wheels roll only. No fake pickup Ackermann steer on all four wheels. |
| `POSE` | Independent wheel roll in revolutions, caster swivel in degrees, continuous heading/yaw convention. State explicitly whether any suspension exists; absence is valid. Every posed load/grip anchor follows the same transform as its support. |
| `INTERACTABLES` | Push/release handle, load/unload bed access points, approach sides and reach envelopes. A person stands beside a load while loading, not inside it. |
| `SORT` / support | Ground sort origin, metric bed plane, back/near rim occluder groups and handle depth; publish load sorting/occlusion guidance for all eight facings. The load is above the bed, behind near rim and correctly behind/in front of the operator by depth, never always-front. |

The sidecar carries `derivedFromRigSha256`, units, origin, source expressions/provenance and posed-anchor rules. No hand-transcribed second set of geometry. gameplay-systems puts stack/payload/reach tuning in Defs; an art sidecar says where it fits and rests.

## Delivery and acceptance

- art-director delivers source, generated sidecar and contract with empty, loaded-clearance and push poses; art-pipeline integrates using established bake/mesh paths, preserving measured orientation and scale.
- qa-test/tools-editor compare wheel contacts and grip/load anchors at rest, a turn and nonzero wheel roll; each datum remains finite and consistent with geometry. gameplay-systems proves one push/release per Interact press.
- art-pipeline supplies all-eight-facing contact/occlusion plates: empty, one tote, a legal pan stack and hands on handle. Load base meets the bed at z=0.30, wheels meet ground and the player does not intersect the load. No rebaselining an existing plate to hide a look regression.
- world-content proves the cart can traverse its first real loading route without stairs/unsupported ground; a load outside the footprint refuses. Parked cart, loaded container ids, position/support and grip release survive save/load through lead-architect's schema work.

Out of scope: self-propulsion, tractor hitch, sand wheels, accidental spill physics and staff pushing. Separate work if requested.
