# Modern 350 — Hidden Harbours road-vehicle kit

A new modern crew-cab pickup for the road fleet, built the same way as the Modern 3500: one self-contained
procedural rig, the fleet camera (32 px = 1 m, elevation 40°, eight facings, N shows the tail) and the shared
harbour palettes. Tall blunt nose · three-bar chrome grille · C-clamp lamps · long box · single rear wheels.

It is a NEW asset, not a variant of the Modern 3500 or the Dually. Give it its own asset entry.

## Files

| File | What it is |
| --- | --- |
| `modern350.rig.js` → `globalThis.ModernTruck350` | The rig. One `<script>`, no dependencies. |
| `modern350.contract.json` | rigSha256, bake + camera, dimensions, painted_bbox per facing, paints, presets, articulation, ramps, anchors, sheet manifest. |
| `../gameplay/vehicles/modern350.rig.gameplay.json` | Schema v2: `road_vehicle`, derived collider bounds and door reach points, enclosed seats under `CAB.seats`. |
| `preview.html` | Standalone turntable and pose bench with this rig inline. Opens off disk. |
| ten `Modern350_*.png` sheets | Reference renders; the game bakes the mesh from `build()`. |

## Provenance and intake

Source: Claude Design, its modern-trucks kit of 2026-09-26.
A privacy cut by the Art desk before staging (rig lines 2 and 15, contract line 7).

The staged rig gained `KEY, GAIN, BIAS, LN, build` on its exported literal, exactly as the Modern
3500 did. The same one-line edit was applied to the inline rig in `preview.html`. The contract,
gameplay sidecar and checksum file were re-pinned together. Node v24.19.0 proved unchanged data,
geometry, materials and anchors across 74 poses, then identical renders for all 98 sheet frames.
All ten PNGs match those renders pixel for pixel; no sheet needed changing.

Claude Design reported **133/133 in-browser checks on the pre-cut rig**. These are the source's
checks, not Unity checks. The Art desk proved the staged cut output-identical; the intake proof
then proved the export edit output-identical. The source validation record stays outside this kit.

## The pin

`modern350.rig.js` sha256 (LF, exact bytes):

```
9457a399d041e2b2590fb702305fdccf658ee7f1291bfe48664ee48ca361a8fb
```

The same 64 characters are `rigSha256` in the contract and `derivedFromRigSha256` in the sidecar.

## Ramps

14 / 16 by day, 14 at night — the headlamp lenses paint `head` by day and `glow` at night, so night is a swap, not an addition.

    day:   paint bright chrome iron galv rubber shade glass cloth bedliner head led amber red
    night: paint bright chrome iron galv rubber shade glass cloth bedliner glow led amber red

## What moves

| Param | Range | What it does |
| --- | --- | --- |
| `dFL dFR dRL dRR` | 0..1 | Four doors, hinged on their forward edge, 0 → 68°. |
| `hood` | 0..1 | Hinged at the cowl (y 1.72, z 1.585), 0 → 48°, over a modelled engine bay. |
| `gate` | 0..1 | Tailgate, hinged at its foot (y -3.16, z 0.97), 0 → 92°. |
| `roll`, `wFL wFR wRL wRR` | revolutions | Master wheel roll plus per-corner offsets. |
| `susF susR` | −1..1 | 0.11 m front / 0.13 m rear. The body moves; the wheel centres stay on the road. |
| `steer` | −1..1 | Both front wheels yaw 30° at full lock, +1 = left. No Ackermann split. |
| `steps` `mirrors` `hitch` | bool | Running boards, towing mirrors, receiver hitch. |
| `night` `brake` | bool | Glass and lamps swap; brake lamps light. |
| `paint` `trim` `weather` | — | 18 paints, chrome or black trim, weathering. |

## Known limits

- Badges are plain shapes. No lettering, logos or model names anywhere on the body.
- Dimensions are stylised art measurements, not manufacturer figures.
- Steering is a visual angle; nothing couples it to heading.
- No Unity or in-world test has been run. This is an art candidate.

## Sheets and anchors

The ten PNGs and SHA256SUMS.txt share the sheet folder. The checksum file also covers the rig,
contract, preview, this README and the gameplay sidecar. The PNGs use Git LFS; all kit text uses LF.

| Sheet | Grid | Frames |
| --- | --- | --- |
| `Modern350_deepBlue_8dir.png` | 8 × 1 | 8 |
| `Modern350_white_8dir.png` | 8 × 1 | 8 |
| `Modern350_night_8dir.png` | 8 × 1 | 8 |
| `Modern350_open_8dir.png` | 8 × 1 | 8 |
| `Modern350_doors_W.png` | 8 × 1 | 8 |
| `Modern350_hood_SE.png` | 8 × 1 | 8 |
| `Modern350_gate_NE.png` | 8 × 1 | 8 |
| `Modern350_drive_W.png` | 12 × 1 | 12 |
| `Modern350_steer_SE.png` | 12 × 1 | 12 |
| `Modern350_paints_SE.png` | 9 × 2 | 18 |

The 23 anchors are `hitch`, `bed`, `driverSeat`, `passengerSeat`, `rearSeatL`, `rearSeatC`,
`rearSeatR`, `fuel`, `exhaust`, `headlightL`, `headlightR`, `tailLightL`, `tailLightR`, `hoodLatch`,
`gate`, `doorFL`, `doorFR`, `doorRL`, `doorRR`, `wheelFL`, `wheelRL`, `wheelFR`, `wheelRR`.
Use `anchorPoints(pose)` for their articulated rig-space positions.

## Gameplay intake

The fleet row reserves `vehicle.modern_350` and `vehiclemesh.modern_350`, with bake outputs
`Modern350.asset` and `Modern350VehicleMesh.asset`. The defs and meshes are created by the Unity
baker in the next phase. No scene placement is part of this intake.

The v2 sidecar has collider ranges x [-1.047, 1.047], y [-3.389, 3.399], z [0, 2] metres, derived on Node
from the mesh with optional hitch, steps and mirrors disabled. The x range is the body group's;
y and z use all faces, rounded to millimetres. The drive point is [-1.557, 0.4, 0] metres.
Door points use the measured body half-width plus 0.51 m, and the rig's front/rear latch stations.

`VehicleSidecarFacts.Read` reports zero errors, a drive door, a collider and a hidden driver.
The duplicate top-level seat list was removed; enclosed seating stays in `CAB.seats`. The four
absences are the open driver seat, FLOAT, fifth wheel and KINGPIN. No towing is enabled.
Collision and door clearance in the world await the runtime plates. Cargo, reach and doorGroups
remain unsettled. No journey or StandTheWorld coverage is claimed.

`ModernTruckKitContractTests` guards the pins, text, sheets, fleet identities and sidecar shape.
`ModernTruckKitProbeTests` checks the bake shim, ramp counts, collider bounds and six rigid hinges.
The hood sweep is the rig's own literal: **48° on the 350 and 50° on the 2500**.
