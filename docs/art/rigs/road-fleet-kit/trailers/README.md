# Trailer Set — `trailerIsoRig.js` → `TrailerIso`

The coupling MATES of the two semi tractors: **four towed bodies in one rig** — `flatbed28`,
`flatbed53`, `reefer28`, `reefer53` — in the fleet bake: 45° steps, elev 40°, fixed upper-LEFT key,
z-buffered flat facets, ordered dither, no AA, **32 px = 1 m**, ringless (ADR-0031).

## Two cells

Pups bake in the **384 × 320 road cell @ 192,214**; the 53-footers take **640 × 480 @ 320,300**
(16.15 m projects both up- and down-screen). `render()` returns the body's cell — read
`cellFor(body)`, and pack from THIS kit's per-body `painted_bbox`, never a sibling's.

## The handshake — locked, and asserted three places

Width **2.44 m** and kingpin set **0.90 m** are load-bearing: they ARE the tractors' published 4 mm
jackknife margin (nose swing √(1.22² + 0.90²) = **1.516 m** inside the 1.52 m kingpin→cab-back gap on
both tractors). Coupling plane **z 1.18** — the fifth-wheel plate top; the trailers bake at coupled
ride height so pairing needs no z shift. The game hinges `anchors().kingpin = [0, L/2−0.90, 1.18]`
to a tractor's `anchors().fifthWheel` and owns the articulation; the sidecar publishes the
off-tracking inputs (kingpin→axle-centre 6.265 / 13.275 m, tail sweep 7.63 / 15.25 m). The reefer
nose unit swings at 1.30 m — inside the corners, it never leads.

## Poses

`gear` 0 (legs up, COUPLED) → 1 (shoes grounded, PARKED — the bake default; couple, then crank it
up) · `barnL barnR` reefer doors 0→**255°** — out, through full outboard (|x| 2.37 m at 180°), flat
against the sides for dock work; flatbeds clamp them · `roll` + `wL wR` revolutions (one rev =
3.142 m, seam 0–1 px; 10-lug hubs with FIVE hand-holes so half a rev reads) · `sus` ±1, 0.10 m,
**pivoting at the kingpin** — the coupling plane holds 1.18 while the tail drops · `yaw` ±45°
rebaked headings · `night` (the reefer unit grille glows), `weather`, `outline`. Parts: `mudflaps`,
`headboard` (flatbeds). **No steering — towed bodies.**

## Files

```
trailerIsoRig.js                        the rig (no deps) → globalThis.TrailerIso
trailers.contract.json                  bake contract, all four bodies, sha-stamped
trailerIsoRig.trailers.gameplay.json    gameplay sidecar: KINGPIN handshake, GEAR, DOORS, cargo
harness.html                            standalone assert page (HANDSHAKE + per-body groups)
Flatbed28_white_8dir.png   3072×320     Flatbed53_white_8dir.png   5120×480
Reefer28_white_8dir.png    3072×320     Reefer53_white_8dir.png    5120×480
<body>_paints_SE.png       ten harbour paints each (5×2)
Flatbed*_gear_W.png        landing-gear cue    Reefer*_doors_N.png   255° door fan
```

## Class notes

- **Flatbeds**: planked lumber deck at 1.18 m (the WOOD ramp is the set's one new material), stake
  pockets, street-side winches, headboard part. The deck is EMPTY — straps and loads are the game's.
- **Reefers**: 2.32 × 8.43 / 16.05 m bay, T-floor at **1.30 m** (a 0.20 m step over the box trucks'
  dock height), 2.68 m headroom, nose unit with fuel pack, three amber markers.
- **Trailers bring their own tail lights and ICC bar** — exactly what both tractor sidecars promise
  for bobtail frames.
- Single axle on the pups, tandem on the 53s; duals everywhere; suspension formula in the sidecar.
