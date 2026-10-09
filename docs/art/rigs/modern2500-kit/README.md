# Modern 2500 — Hidden Harbours road-vehicle kit

A new modern crew-cab pickup for the road fleet, built the same way as the Modern 3500: one self-contained
procedural rig, the fleet camera (32 px = 1 m, elevation 40°, eight facings, N shows the tail) and the shared
harbour palettes. Tall square nose · huge dark grille · C-blade lamps · scoop hood · black cladding · standard box.

It is a NEW asset, not a variant of the Modern 3500 or the Dually. Give it its own asset entry.

## Files

| File | What it is |
| --- | --- |
| `modern2500.rig.js` → `globalThis.ModernTruck2500` | The rig. One `<script>`, no dependencies. |
| `modern2500.contract.json` | rigSha256, bake + camera, dimensions, painted_bbox per facing, paints, presets, articulation, ramps, anchors, sheet manifest. |
| `modern2500.rig.gameplay.json` | The gameplay sidecar, `"kind": "road_vehicle"`. Written to the fleet schema (`BODY.collider_bbox`, `SEATS[]` with `seat_ref`, `drive`/`ride` in `INTERACT`) and it also carries the Modern 3500's blocks. |
| `preview.html` | Standalone turntable and pose bench with this rig inline. Opens off disk. |
| ten `Modern2500_*.png` sheets | Reference renders; the game bakes the mesh from `build()`. |
| `validation.json` | 133 / 133 checks. |

## The pin

`modern2500.rig.js` sha256 (LF, exact bytes):

```
c2a1217d484268e346eb3d9b91b77ccc68b901e03a875462fc4771b67ac15ca7
```

The same 64 characters are `rigSha256` in the contract and `derivedFromRigSha256` in the sidecar.

## Ramps

15 / 16 by day, 15 at night — the headlamp lenses paint `head` by day and `glow` at night, so night is a swap, not an addition.

    day:   paint bright iron galv rubber clad shade glass cloth bedliner head led amber red hook
    night: paint bright iron galv rubber clad shade glass cloth bedliner glow led amber red hook

## What moves

| Param | Range | What it does |
| --- | --- | --- |
| `dFL dFR dRL dRR` | 0..1 | Four doors, hinged on their forward edge, 0 → 68°. |
| `hood` | 0..1 | Hinged at the cowl (y 1.44, z 1.665), 0 → 50°, over a modelled engine bay. |
| `gate` | 0..1 | Tailgate, hinged at its foot (y -2.98, z 1.03), 0 → 92°. |
| `roll`, `wFL wFR wRL wRR` | revolutions | Master wheel roll plus per-corner offsets. |
| `susF susR` | −1..1 | 0.12 m front / 0.14 m rear. The body moves; the wheel centres stay on the road. |
| `steer` | −1..1 | Both front wheels yaw 30° at full lock, +1 = left. No Ackermann split. |
| `steps` `mirrors` `hitch` | bool | Rock rails, mirrors, receiver hitch. |
| `night` `brake` | bool | Glass and lamps swap; brake lamps light. |
| `paint` `trim` `weather` | — | 18 paints, chrome or black trim, weathering. |

## Known limits

- Badges are plain shapes. No lettering, logos or model names anywhere on the body.
- Dimensions are stylised art measurements, not manufacturer figures.
- Steering is a visual angle; nothing couples it to heading.
- No Unity or in-world test has been run. This is an art candidate.
