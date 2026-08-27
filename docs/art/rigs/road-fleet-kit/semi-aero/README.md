# Aero Sleeper Semi — `aeroSemiIsoRig.js` → `AeroSemiIso`

The first tractor tier (Cascadia/VNL class) in the fleet bake: 45° steps, elev 40°, fixed upper-LEFT
key, z-buffered flat facets, ordered dither, no AA, **32 px = 1 m**, ringless (ADR-0031). Cell
**384 × 320 @ 192,214** — a bare tractor fits the road cell.

## It couples — that is the tier's point

The tractor bakes **bobtail**. Trailers are separate bodies with kingpin anchors; the **game**
articulates the pair about `anchors().fifthWheel = [0, −2.40, 1.18]`:

- **Fifth wheel**: plate top z 1.18, slot opens AFT, approach ramps angled down — back under a nose
  and the kingpin rides into the slot. Release handle on the street side.
- **Swing clearance**: kingpin→cab-back **1.52 m** against a **1.516 m** nose swing (2.44 m trailer,
  0.90 m kingpin set) — **full jackknife clears by 4 mm**. The harness asserts the arithmetic; keep
  the pack's trailers at the 0.90 m set.
- **The coupling point rides the suspension** — re-read the anchor under load, never cache 1.18.
- Glad hands on the back wall, grid-plate catwalk between cab and plate, frame-end bobtail lamps
  (the trailer brings its own ICC bar and tail lights — this frame has **no rear bumper**).

## Poses

`dL dR` cab doors 0→65° · `hood` front-clip tilt 0→72° (fenders, grille, lamps ride it; the CAB and
its doors stay) · `roll` + `wFL..wRR` revolutions (one rev = 3.142 m, seam 0.15%; tandem sides share
their corner's roll) · `susF susR` ±1 (0.09 / 0.11 m) · `steer` ±1 Ackermann **32°/27.5°**, envelope
= the 2.54 m painted fenders · `yaw` ±45° rebaked headings · `night`, `weather`, `outline`.
Parts: `mirrors`, `mudflaps`, `skirts` (aero fairings; `false` bares frame + tanks — **identical
painted bbox**, asserted).

## Files

```
aeroSemiIsoRig.js                       the rig (no deps) → globalThis.AeroSemiIso
aeroSemi.contract.json                  machine-readable bake contract, sha-stamped
aeroSemiIsoRig.aeroSemi.gameplay.json   gameplay sidecar: coupling, cab, steering, thresholds
harness.html                            standalone assert page (incl. the COUPLING group)
AeroSemi_white_8dir.png                 at rest, fleet white 0.45, bobtail    3072×320
AeroSemi_skirtless_8dir.png             skirts:false — frame and tanks bared  3072×320
AeroSemi_doors_W.png                    doors cue at W                        3072×320
AeroSemi_hood_W.png                     front-clip cue at W                   3072×320
AeroSemi_roll_W.png                     bounce cue, cyclic, 10 frames         3840×320
AeroSemi_steer_S.png                    lock-to-lock at S                     3072×320
AeroSemi_paints_SE.png                  the ten harbour paints                1920×640
```

Painted at rest: **264 × 197 px** union over the 8 facings (per-facing boxes in the contract).

## Class notes

- **10 wheels**: steered singles (0.50 m radius, 10-lug), tandem duals on two axles at −1.70/−2.90.
- **Sleeper**: 1.63 m berth behind the buckets, windows both sides, one shell — no partition.
- **The tallest climb in the pack**: 1.15 m cab floor, two tank-top treads (0.44 / 0.80).
- **No chrome stacks** — under-frame after-treatment. The stacks are the CLASSIC tier's cue.
- Integrated roof fairing to **3.85 m** (matches a 4.1 m trailer's face); cab side extenders at the
  back edge sit 1.83 m from the kingpin — clear of the swing despite looking close.
