# Hidden Harbours — Modern 3500 Iso Kit

*A second road vehicle, beside the [Dually 3500](../dually-iso-kit/README.md). The conventions the
road fleet shares — 32 px = 1 m, ¾ camera in 45° steps at elev 40°, `order` = `N NE E SE S SW W NW`,
facing names naming where the truck POINTS — are documented once, in the dually kit. Everything
below is what is different about this one.*

A modern crew-cab dually pickup: six wheels, long box, continuous curved flares, four doors on
hinges, a hood over a modelled engine bay, a drop tailgate, and per-corner wheel roll over
independent front and rear suspension travel.

## Provenance

**Authored by Codex (the GPT lane) and delivered as a drop on 2026-09-13.** It is a NEW asset, not a
revision: the delivery note asks for a separate asset entry and says not to overwrite the older
truck's rig or reuse its sidecar. `docs/art/rigs/dually-iso-kit/` is untouched by this kit, and the
two are independent bodies — the Modern 3500 is not a variant of the Dually.

The drop staged more files than this kit ships: `.obj`, `.mtl`, `.mesh.json`, a preview GIF, two
overview PNGs, and `bake.cjs`/`verify.cjs` (Node). They stay in the drop on purpose. **The JS rig is
the source of truth and the repo's own baker makes the repo's meshes** — a second, Node-shaped mesh
path under `docs/art/rigs/**` would be a fork of the pipeline that nothing reads.

**Re-issued on 2026-09-16 with her palette folded from 27 ramps to 16.** The first cut painted 27
material ramps against the facet shader's 16 slots, so the fleet bake had to skip her. The re-issued
rig adds an eleven-entry fold table inside `build()` and changes nothing else. No face was added, no
geometry moved, and no material name was added or renamed; the eleven folded names are still declared
in `makeMats`. She paints 16 ramps by day and 15 by night (the day-only `head` lens is the gap). One
fold departs from the route the art brief proposed: `reflect` → `glass` was taken instead of `bright`
→ `chrome`, because `bright` is the only consumer of `trim`, and folding it would have made
`trim:'black'` (and the `blackout` preset) a no-op. The owner kept that departure on 2026-09-16 and
ruled that she bakes as **a mesh only — no VehicleDef and no vehicle id until her gameplay exists**.

**One line of the rig is the repo's, not the return's: the exported literal gained
`KEY, GAIN, BIAS, LN, build`.** The baker reads `ModernTruck3500.KEY` off her global and parses it as
hex. She declares `const KEY` but never exported it, so the first editor bake of 2026-09-16 failed on
her with *"Could not find any recognizable digits."* `GAIN`, `BIAS`, `LN` and `build` were missing from
the same literal; the baker had been shimming them in memory, and its warning asks for exactly those
four. Every name added is one the rig already declared, so nothing she draws moved. That edit is what
moved the pin below. On `node`, only `MATS` is left for the baker's in-memory widening (it is
reconstructed); the editor has not baked her since.

### The pin

`modern3500.rig.js` LF-normalised sha256:

```
3eb1640029e51e28f8597e393418ab5492ad77e3bd33bd8ce0040cff53eddc86
```

The same 64 characters appear as `rigSha256` in `modern3500.contract.json` and as
`derivedFromRigSha256` in `../gameplay/vehicles/modern3500.rig.gameplay.json`. All three were verified
equal against the file's own bytes after the copy into the repo. **Compare the full digest, never a
prefix** — the hightop van's bad stamp shared its rig's first sixteen hex digits.

`.gitattributes` pins this kit's text files `text eol=lf` **by name**. That is load-bearing rather
than tidy: `core.autocrlf` is true on a Windows checkout, and `DeckSidecarReader.MatchRigHash` accepts
a CRLF working copy only as `RigHashMatch.LineEndingNormalized` — which files a standing
"art-director should re-stamp `derivedFromRigSha256`" note, forever, about a file that is correct.
With the pin the match is `Exact` on every platform and `sha256sum -c reference/SHA256SUMS.txt` runs
on any checkout. The rule names files one at a time and is **not** `modern3500-kit/**`: a blanket
would also match `reference/*.png`, where `text eol=lf` overrides the `-text` inside `[attr]lfs` and
corrupts every sheet. The sail kit's `**` is safe only because it ships no binaries.

## Files

| File | What it is |
| --- | --- |
| `modern3500.rig.js` → `globalThis.ModernTruck3500` | The rig: geometry, articulation, projection, render, mesh. Self-contained — one `<script>`, no dependency on another rig file. |
| `modern3500.contract.json` | Every number, machine-readable: `rigSha256`, bake + camera, dimensions, per-facing `painted_bbox`, paints, presets, the articulation table, anchors, and the sheet manifest below. |
| `preview.html` | Standalone turntable and pose bench. Opens off disk; no build, no server, no deps. |
| `reference/*.png` | The ten sprite sheets (below). **Reference only** — this body bakes as MESH (ADR 0035, the road fleet's path); no sprite path consumes them. |
| `reference/SHA256SUMS.txt` | sha256 of every file this kit ships — the four text files, the sidecar, and the ten sheets. The sheet digests are also their **LFS oids**, so they check against a pointer-only checkout as well as a pulled one. |

The gameplay sidecar does **not** live here: it is
`../gameplay/vehicles/modern3500.rig.gameplay.json`, with the rest of the fleet's.

## The sheets

Cell is **384 × 320**, pivot **192, 214** (`W`, `H`, `cx`, `groundY` in the rig). Every sheet was
re-measured off its own PNG header in this repo and agrees with the contract's manifest: ten sheets,
ten files, each width `columns × 384` and height `rows × 320`, each frame list the size of its grid.

| Sheet | Grid | What it shows |
| --- | --- | --- |
| `Modern3500_graphite_8dir.png` | 8 × 1 | The workhorse build — graphite, chrome trim, running boards, weather 0.08 — at rest, all eight facings. |
| `Modern3500_pearl_8dir.png` | 8 × 1 | Pearl, eight facings. |
| `Modern3500_night_8dir.png` | 8 × 1 | Night: glass ramps swap and the lamps light, eight facings. |
| `Modern3500_open_8dir.png` | 8 × 1 | Everything open at once — hood, gate and all four doors — eight facings. |
| `Modern3500_doors_W.png` | 8 × 1 | The doors cue at W, 0 → 68°, all four leaves in view. |
| `Modern3500_hood_SE.png` | 8 × 1 | The hood cue at SE, 0 → 48°. |
| `Modern3500_gate_NE.png` | 8 × 1 | The tailgate cue at NE, 0 → 92°. |
| `Modern3500_drive_W.png` | 12 × 1 | One wheel revolution at W with both axles working — roll and suspension together. Cyclic. |
| `Modern3500_steer_SE.png` | 12 × 1 | The steer cue at SE, lock to lock and back. Cyclic. |
| `Modern3500_paints_SE.png` | 7 × 2 | All fourteen paints at SE. |

## Load order

```html
<script src="modern3500.rig.js"></script>   <!-- that's all of it -->
```

## What moves

`render(dir, opts)` takes the whole pose; there are no baked states. The degrees below are the
literals in the rig source, cross-checked against `articulation` in the contract:

| Param | Range | What it does |
| --- | --- | --- |
| `dFL dFR dRL dRR` | 0..1 | Four doors, hinged on their forward edge, 0 → **68°**. |
| `hood` | 0..1 | Hinged at the cowl, 0 → **48°**, over a modelled engine bay. |
| `gate` | 0..1 | Tailgate, hinged at its bottom edge, 0 → **92°**. |
| `roll` | revolutions | Master wheel roll; 28 tread stripes to the revolution. |
| `wFL wFR wRL wRR` | revolutions | Per-corner offsets on top of `roll`. **Four keys drive six wheels** — each rear key drives its dual pair, inner and outer together. |
| `susF susR` | −1..1 | Suspension travel, **0.12 m front / 0.14 m rear**. The body and its attachment points move; the wheel centres stay on the road. |
| `steer` | −1..1 | Both front wheels yaw **28°** at full lock. |
| `steps` `mirrors` `hitch` | bool | Fitted parts. |
| `night` | bool | Glass ramps swap; the lamps light. |
| `paint` `trim` `weather` | — | Fourteen paints; `weather` greys and grimes. |

**There is no Ackermann split.** The contract says so in as many words — *"same visual angle on both
front wheels; no Ackermann solver"* — and it is why the fleet row carries `MaxInnerDeg` and
`MaxOuterDeg` both at 28 where the Dually carries 30 / 24.9.

## What was verified where

Verified **at the 2026-09-16 re-issue**, against these bytes:

- The rig's sha256 equals the pin and equals both stamps.
- `preview.html` is the returned bytes, pure LF, and re-embeds the folded rig. `modern3500.rig.js`
  is the returned bytes plus the export line above, also pure LF. The rig copy inside
  `preview.html` keeps the returned export line; the preview draws from it and never bakes.
- `modern3500.contract.json` and the ten sheets are **not** the returned files. They were re-baked
  on `node` by the kit's `bake.cjs` from the returned rig. The returned sheets match `node`'s pixel
  for pixel but not byte for byte: the return was rendered on a Node-18-compatible runtime whose PNG
  deflate differs. So the committed sheets are `node`'s, and each digest is also its LFS oid.
- After the export line was added, `bake.cjs` was run again on `node` from the edited rig. All ten
  sheets decode to the same pixels as the committed ones (0 differing pixels in every sheet, and the
  files are byte-identical too), and the contract differs from the committed one by `rigSha256`
  alone. That contract is the one committed.
- The contract differs from the previous one by `rigSha256` alone, and the sidecar by
  `derivedFromRigSha256` alone.
- On `node`, from the edited rig, `verify.cjs` passed 159 / 159 (faces 2802, triangles 6856, roll
  seam 0 px), and `count-ramps.cjs` read 16 ramps by day and 15 by night.
- The sheets' PNG dimensions match the grids above. The rig diff touches none of the four
  articulation degrees.
- `sha256sum -c reference/SHA256SUMS.txt` verifies all fifteen listed files.

At the first intake (2026-09-13), `preview.html` was delivered CRLF (1,194 line endings) and
converted to LF on the way in. The re-issue arrived LF. Nothing stamps a hash over this file, but a
mixed file would make its checksum line pass on Linux and fail on Windows, which is the failure the
`eol=lf` pin exists to prevent.

Proved by **the editor bake and CI**, not by `node`: everything that needs the engine. That covers
the sidecar registering, the fleet row's axes partitioning, the widened rig executing in the repo's
own script host, and the bake producing her mesh and fittings. The `node` numbers above are `node`'s
measurements of the rig. They are not the engine's.

## Known limits

- **The rig renders literal `RAM` badge lettering** — `lettering(…,'RAM',…)` twice, on the grille and
  on the tailgate — and the delivery note describes the truck as drawn from a Ram 3500 reference.
  That is a real-world trademark on a shipped art asset in a public repo. It is cheap to change in
  the rig now and expensive once sheets and meshes are baked from it. **Owner's call; flagged, not
  taken** — editing the rig would break the byte-for-byte pin this kit exists to carry.
- **The sidecar is written to its own schema**, not the fleet's. It has no `BODY.collider_bbox`, no
  top-level `SEATS[]` carrying `seat_ref` (its seats are under `CAB.seats`), and its `INTERACT` ids
  are `door_fl`…`tow` rather than `drive`/`ride`. `VehicleSidecarFacts` throws on none of these —
  they land as named `Absences` — so she registers and bakes, but arrives with **no collider and no
  declared way in**. The re-key belongs upstream in the art lane; nothing here invents the geometry.
- **Steering is a visual angle.** No solver, and no coupling to heading: a game that locks the wheels
  over without turning the machine will look wrong, and the rig will not stop it.
- **Dimensions are stylised art measurements**, not manufacturer figures.
- **Cargo walkability and character reach are unruled** — the geometry is published, the decisions
  are not.
- **No Unity or in-world test rode on the drop.** This is an art candidate that CI has accepted, not
  a body anyone has driven.
