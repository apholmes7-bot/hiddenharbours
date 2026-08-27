# Hidden Harbours — Road Fleet Kit Drop

Six complete kits, one camera: 45° steps, elev 40°, fixed upper-LEFT key, **32 px = 1 m**, ringless
(ADR-0031). Every kit contains its rig (one dependency-free `.js` → one global), a sha-stamped
machine-readable **contract**, a **gameplay sidecar** (thresholds, cargo, coupling, colliders, seats,
`_excluded` / `_confirm` blocks), a standalone **assert harness** (open over http), baked sheets in
all ten harbour paints, and its own README.

```
hightop-van/            vanIsoRig.js         → VanIso          384×320 @ 192,214
boxtruck-cabover/       boxIsoRig.js         → BoxIso          384×320 @ 192,214
boxtruck-conventional/  convBoxIsoRig.js     → ConvBoxIso      448×352 @ 224,214
semi-aero/              aeroSemiIsoRig.js    → AeroSemiIso     384×320 @ 192,214
semi-classic/           classicSemiIsoRig.js → ClassicSemiIso  384×320 @ 192,214
trailers/               trailerIsoRig.js     → TrailerIso      pups 384×320 · 53s 640×480 @ 320,300
                        (four towed bodies: flatbed28, flatbed53, reefer28, reefer53)
```

## The coupling loop (tractors ↔ trailers)

Both tractors publish the same handshake, and the trailers are built to it — asserted in all three
harnesses: fifth-wheel plate top **z 1.18** = trailer coupled deck plane; kingpin set **0.90 m**,
trailer width **2.44 m** → nose swing **1.516 m** inside both tractors' **1.52 m** kingpin→cab-back
gap (full jackknife clears by 4 mm; classic's stacks by 0.24 m; the reefer nose unit swings at
1.30 m, inside its own corners). The game hinges `anchors().kingpin` to `anchors().fifthWheel`;
off-tracking inputs (kingpin→axle-centre, tail sweep) are in the trailer sidecar. Couple → `gear 0`.

## Reading order per kit

`README.md` → `*.contract.json` (bake geometry: cells, pivots, per-facing painted bboxes, anchors)
→ `*.gameplay.json` (what the game may rely on) → `harness.html` (re-asserts all of it against a
fresh bake and re-hashes the rig).
