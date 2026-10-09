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
| `modern350.rig.gameplay.json` | The gameplay sidecar, `"kind": "road_vehicle"`. Written to the fleet schema (`BODY.collider_bbox`, `SEATS[]` with `seat_ref`, `drive`/`ride` in `INTERACT`) and it also carries the Modern 3500's blocks. |
| `preview.html` | Standalone turntable and pose bench with this rig inline. Opens off disk. |
| ten `Modern350_*.png` sheets | Reference renders; the game bakes the mesh from `build()`. |
| `validation.json` | 133 / 133 checks. |

## The pin

`modern350.rig.js` sha256 (LF, exact bytes):

```
b483514e4247d70cdd6c9b7d62573f423ffd76db7c0cc9c79c9770285c394118
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
