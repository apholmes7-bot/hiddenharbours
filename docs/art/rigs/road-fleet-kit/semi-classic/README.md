# Classic Long-Nose Semi — `classicSemiIsoRig.js` → `ClassicSemiIso`

The second tractor tier (W900/389 class) in the fleet bake: 45° steps, elev 40°, fixed upper-LEFT
key, z-buffered flat facets, ordered dither, no AA, **32 px = 1 m**, ringless (ADR-0031). Cell
**384 × 320 @ 192,214** — the road cell.

## Same handshake, different truck

The coupling is IDENTICAL to the aero tier by design — the pack's trailers couple to either tractor
with no re-derivation: `anchors().fifthWheel = [0, −2.30, 1.18]`, plate top **z 1.18**, slot aft,
kingpin→cab-back **1.52 m** against the 1.516 m nose swing. Full jackknife clears the cab by 4 mm and
the **stacks by 0.24 m** (they stand 1.75 m from the kingpin — closer-looking than they are, and the
harness asserts both). The coupling point rides the suspension; re-read it, never cache it.

What differs is the truck:

- **2.3 m level square hood** (`hood` 0→70°, front-clip tilt) — at full tilt the nose reaches 0.83 m
  past the bumper, the longest reach in the pack.
- **Twin chrome stacks** behind the cab, z 0.98→**3.55** with heat shields — the tallest point on the
  machine and the tier's silhouette cue. Smoke FX attach BOTH pipes (`anchors().stackL/stackR`).
- **Chrome everywhere the aero is body-colour**: bumper, grille surround, tanks, lamp pods.
- **Two-piece upright windshield** with a painted centre post, **drop visor** (`visor` part — zero
  bbox change, asserted), five amber roof markers, west-coast mirror bars.
- **No skirts, ever** — the bare frame is the look; the aero carries the fairings.

## Poses

`dL dR` doors 0→65° · `hood` 0→70° · `roll` + `wFL..wRR` revolutions (one rev = 3.142 m, closes
bit-identical; tandem sides share their corner's roll) · `susF susR` ±1 (0.09 / 0.11 m) · `steer` ±1
Ackermann **30°/26.2°** — the widest circle in the pack; the long hood pays for its presence ·
`yaw` ±45° rebaked headings · `night`, `weather`, `outline`. Parts: `mirrors`, `mudflaps`, `visor`.

## Files

```
classicSemiIsoRig.js                          the rig (no deps) → globalThis.ClassicSemiIso
classicSemi.contract.json                     machine-readable bake contract, sha-stamped
classicSemiIsoRig.classicSemi.gameplay.json   gameplay sidecar: coupling, stacks, cab, steering
harness.html                                  standalone assert page (COUPLING + STACKS groups)
ClassicSemi_white_8dir.png                    at rest, fleet white 0.45, bobtail   3072×320
ClassicSemi_night_8dir.png                    midnight preset — lamps aglow        3072×320
ClassicSemi_doors_W.png                       doors cue at W                       3072×320
ClassicSemi_hood_W.png                        long-clip cue at W                   3072×320
ClassicSemi_roll_W.png                        bounce cue, cyclic, 10 frames        3840×320
ClassicSemi_steer_S.png                       lock-to-lock at S                    3072×320
ClassicSemi_paints_SE.png                     the ten harbour paints               1920×640
```

Painted at rest: **290 × 214 px** union over the 8 facings (per-facing boxes in the contract).

## Class notes

- **10 wheels**, steered singles + tandem duals at −1.60/−2.80; flat-top 1.43 m sleeper.
- The **frame ends bare** — no rear bumper; the trailer brings the ICC bar and tail lights.
- Height is the STACKS (3.59 m over), not the roof (2.92) — clearance checks use the pipes.
