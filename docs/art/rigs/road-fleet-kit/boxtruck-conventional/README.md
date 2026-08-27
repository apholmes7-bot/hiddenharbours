# Conventional Box Truck — `convBoxIsoRig.js` → `ConvBoxIso`

The cabover's big sibling (F-650/MV class) in the fleet bake: 45° steps, elev 40°, fixed upper-LEFT
key, z-buffered flat facets, ordered dither, no AA, **32 px = 1 m**, ringless (ADR-0031).

## Its own cell — deliberately

**448 × 352 @ 224,214.** Not the 384 road cell: 9.60 m of LOA plus a liftgate that reaches 6.52 m aft
when grounded cannot fit 384 px at fleet scale. The **ground row (214) is kept**, so the truck still
parks on the same road plane as everything else. Pack from THIS kit's contract, never a sibling's.

## The class, in three mechanisms

- **Roll-up rear door** (`rollup` 0→1): 15 slats climb a 2.30 m track in the rear plane, bend forward
  and stack flat under the roof. **The door never leaves the body** — the painted bbox at N is
  identical closed and open, and the harness asserts it. Zero keep-clear behind the truck.
- **Tuck-under liftgate** (`gate` 0→1, `liftgate:true` part): swing out (0–0.45), unfold to the full
  2.20 × 1.26 m platform at dock height (0.45–0.70), lower to the ground (0.70–1). Dock **1.10 m** —
  a true dock-height floor, 0.18 over the cabover's.
- **Hood, not tilt cab** (`hood` 0→70°): the whole FRONT CLIP — hood, cheeks, fenders, arches,
  grille, headlamps — tilts forward over a hinge at the bumper line and bares the engine. The cab,
  its doors and their swing arcs never move. At full tilt the nose reaches 0.57 m past the bumper.

Plus the big tier's cue: a **cab roof fairing** (`fairing:true` part) carrying the five amber ID
lamps. `fairing:false` drops the lamps onto the bare roof; the painted top row does not change —
the 3.68 m box owns the skyline either way, and the harness asserts that too.

## Poses

`dL dR` cab doors 0→65° · `rollup` · `gate` · `hood` · `roll` + `wFL..wRR` revolutions (one rev =
2.827 m, closes to 1 stray px; half a rev lands mid-stripe so the cue visibly moves) · `susF susR`
±1 (0.10 / 0.14 m — the body moves, wheels stay down) · `steer` ±1 Ackermann **35°/30.3°** — the
biggest lock in the pack and still a wider circle than the cabover: 6.10 m of wheelbase taxes every
degree · `yaw` ±45° rebaked headings · `night`, `weather`, `outline`. Parts: `mirrors`, `mudflaps`,
`liftgate`, `fairing`.

## Files

```
convBoxIsoRig.js                        the rig (no deps) → globalThis.ConvBoxIso
convBox.contract.json                   machine-readable bake contract, sha-stamped
convBoxIsoRig.convBox.gameplay.json     gameplay sidecar: thresholds, liftgate, hood, steering
harness.html                            standalone assert page (open over http for the sha groups)
ConvBox_white_8dir.png                  at rest, workhorse white 0.50         3584×352
ConvBox_rollup_N.png                    rollup cue at N                       3584×352
ConvBox_gate_N.png                      liftgate cue at N                     3584×352
ConvBox_hood_W.png                      front-clip cue at W                   3584×352
ConvBox_roll_W.png                      bounce cue, cyclic, 10 frames         4480×352
ConvBox_steer_S.png                     lock-to-lock at S                     3584×352
ConvBox_paints_SE.png                   the ten harbour paints                2240×704
```

Painted at rest: **340 × 292 px** union over the 8 facings (per-facing boxes in the contract).

## Class notes

- **The box floor is a real dock sill**: 2.28 × 6.10 m at **1.10 m**, flat wall to wall above the
  duals. Sill = floor; bare, it is a climb, and the liftgate is the step.
- **6 wheels, 8-lug hubs**: steered singles up front (0.45 m radius), duals aft sharing per-corner roll.
- **The steps are frame parts** (0.38 / 0.70 m) — they ride the suspension, never the hood.
- **Nothing tows.** No ball, no pintle; the ICC bar is a safety bar. The chrome stack is the semi
  tier's cue, not this one's — under-frame exhaust here.
