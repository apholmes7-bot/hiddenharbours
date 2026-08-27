# Cabover Box Truck — `boxIsoRig.js` → `BoxIso`

A low-cab-forward city box truck (NPR class) in the fleet bake: 45° steps, elev 40°, fixed upper-LEFT
key, z-buffered flat facets, ordered dither, no AA, **32 px = 1 m**, ringless (ADR-0031). Cell
**384 × 320**, pivot **192,214** — the same road cell as the Dually 3500 and the Hightop Van.

## The class, in three mechanisms

- **Roll-up rear door** (`rollup` 0→1): 12 slats climb a 1.88 m track in the rear plane, bend forward
  and stack flat under the roof. **The door never leaves the body** — the painted bbox at N is
  identical closed and open, and the harness asserts it. Zero keep-clear behind the truck.
- **Tuck-under liftgate** (`gate` 0→1, `liftgate:true` part): one param, three phases — swing out
  from the stow (0–0.45), unfold to the full 1.90 × 1.20 m platform at dock height (0.45–0.70), lower
  to the ground (0.70–1). Dock 0.92 m, ground 0.03 m. The only thing on this truck that wants
  clearance behind it.
- **Cab tilt** (`tilt` 0→38°): this class has no hood — the whole cab (doors, mirrors, steps,
  interior) noses over the front hinge and bares the engine, radiator and air cleaner on the chassis.

## Poses

`dL dR` cab doors 0→65° · `rollup` · `gate` · `tilt` · `roll` + `wFL..wRR` revolutions (one rev =
2.099 m and closes **bit-identical**) · `susF susR` ±1 (0.09 / 0.12 m — the body moves, wheels stay
down) · `steer` ±1 Ackermann **33°/27.5°** (cabovers steer tight; capped so the tire corner stays
8 mm inside the 1.07 m black arch flares) · `yaw` ±45° rebaked headings · `night`, `weather`,
`outline`. Parts: `mirrors`, `mudflaps`, `liftgate`.

## Files

```
boxIsoRig.js                            the rig (no deps) → globalThis.BoxIso
caboverBox.contract.json                machine-readable bake contract, sha-stamped
boxIsoRig.caboverBox.gameplay.json      gameplay sidecar: thresholds, liftgate, tilt, steering
harness.html                            standalone assert page (open over http for the sha groups)
CaboverBox_white_8dir.png               at rest, rental white 0.50            3072×320
CaboverBox_rollup_N.png                 rollup cue at N                       3072×320
CaboverBox_gate_N.png                   liftgate cue at N                     3072×320
CaboverBox_tilt_W.png                   cab-tilt cue at W                     3072×320
CaboverBox_roll_W.png                   bounce cue, cyclic, 10 frames         3840×320
CaboverBox_steer_S.png                  lock-to-lock at S                     3072×320
CaboverBox_paints_SE.png                the ten harbour paints                1920×640
```

Painted at rest: **218 × 201 px** union over the 8 facings (per-facing boxes in the contract).

## Class notes

- **The box floor is flat**: 2.00 × 4.50 m at 0.92 m, riding ABOVE the duals — no wheel tubs, unlike
  the van. The roll-up sill IS the floor.
- **6 wheels**: steered singles up front, duals aft. Each dual pair shares its corner's roll param.
- **Steps exist** on this body (two treads per side, 0.30 / 0.58 m), and they ride the tilt.
- **No build variants** — no roof or glass options. `liftgate:false` is a part toggle: plain tail,
  same everything else.
- **Nothing tows**: no ball, no pintle. The ICC bar is a safety bar, not a coupling.
- The open roll-up caps the aft 1.9 m of bay at **1.92 m headroom** — the sidecar publishes the stack.
