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
That ruling was replaced on 2026-09-18, once her gameplay existed: she parks, drivable, at Nine Mile
Creek beside the Dually. She now wears her own def, `Assets/_Project/Data/Vehicles/Modern3500.asset`,
which the fleet bake makes and no hand writes, and her own id, `vehicle.modern_3500`.

**One line of the rig is the repo's, not the return's: the exported literal gained
`KEY, GAIN, BIAS, LN, build`.** The baker reads `ModernTruck3500.KEY` off her global and parses it as
hex. She declares `const KEY` but never exported it, so the first editor bake of 2026-09-16 failed on
her with *"Could not find any recognizable digits."* `GAIN`, `BIAS`, `LN` and `build` were missing from
the same literal; the baker had been shimming them in memory, and its warning asks for exactly those
four. Every name added is one the rig already declared, so nothing she draws moved. That edit is what
moved the pin below. On `node`, only `MATS` is left for the baker's in-memory widening (it is
reconstructed); the editor has not baked her since.

**Cut on 2026-09-18, by the owner's ruling D1: no badge lettering.** The rig's two
`lettering(…,'RAM',…)` draws were deleted, the grille badge and the tailgate badge, and nothing was
put in their place. No other line of the rig changed, and `preview.html` lost the same two lines. On
`node` she went from 2802 faces to 2738 and from 6856 triangles to 6728. The 64 faces removed are the
grille lettering's 32 (`frontClip`) and the tailgate lettering's 32 (`gate`). The same 64 went in each
of 69 poses checked, and every other face is identical and in the same order. Every door, the hood and
the gate is still one rigid leaf about its declared pin to 1e-6 m, and the per-facing `painted_bbox`
did not move. She still paints 16 ramps by day and 15 by night, because twelve faces of her body still
paint with the badge ramp. The sidecar was then re-derived from the cut rig (see *Known limits*), and
the contract's `rigSha256`, `faces` and `triangles` were re-stamped from it. **The ten sheets were not
re-rendered.** They still show the lettering, 899 px across the ten against the cut rig. That is debt.

### The pin

`modern3500.rig.js` LF-normalised sha256:

```
0601bc98435533aa024804532725c491d3cdc31b86b506706cd4cff79d3ced27
```

The same 64 characters appear as `rigSha256` in `modern3500.contract.json` and as
`derivedFromRigSha256` in `../gameplay/vehicles/modern3500.rig.gameplay.json`. All three were verified
equal against the file's own bytes after the copy into the repo. **Compare the full digest, never a
prefix** — the hightop van's bad stamp shared its rig's first sixteen hex digits.

Until the cut of 2026-09-18 the pin was
`3eb1640029e51e28f8597e393418ab5492ad77e3bd33bd8ce0040cff53eddc86`. All three places moved together,
and each was re-derived from the cut rig rather than re-typed.

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

Verified **at the 2026-09-18 cut and re-key**, on `node`, against the edited bytes:

- The rig's LF sha256 equals the pin, the contract's `rigSha256` and the sidecar's
  `derivedFromRigSha256`.
- The face census in the cut paragraph above, every hinge rigid to 1e-6 m, `painted_bbox` unchanged,
  and 16 / 15 ramps.
- The sidecar's `BODY.collider_bbox` was measured off the cut rig at rest. It is x ±1.326 m, the
  flares (the mirrors reach ±1.418 and are not solid). It is y −3.545 to 3.456 m, rear plate to front
  plate (the receiver and ball reach −3.63 and are not solid). It is z 0 to 2.19 m, tyres to marker
  lamps.
- The `drive` and `ride` reach points stand 0.51 m outside the flare line, at the latch seam
  (y 0.35). Over each door's full swing, sampled in 69 steps, they clear the front door by 0.284 m
  and the rear door by 0.320 m, on both sides.
- `sha256sum -c reference/SHA256SUMS.txt` verifies all fifteen listed files.

The bullets below are the re-issue's and stay as history. Their face and triangle counts are the uncut
rig's.

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

- **The `RAM` badge lettering is gone from the rig and `preview.html`** (owner's ruling D1,
  2026-09-18; see the cut paragraph above). Three debts remain. The ten reference sheets still show
  it, because they were not re-rendered. The words "Ram 3500 reference" / "Ram-inspired" survive as
  provenance in the contract's `source` and in the rig's and the preview's header comments. And the
  Codex drop upstream still draws the lettering, so a re-issue from there has to be cut again.
- **The sidecar was re-keyed on 2026-09-18** onto the fleet's schema, modelled on the Dually's. It now
  has a fitted `BODY.collider_bbox`, plus an `INTERACT` `drive` at `door_fl` and `ride` at `door_fr`,
  each with a `reach_point` and `visible_facings`. Her own door, hood, cargo, fuel and tow entries
  stay. Like the Dually, she lists no top-level `SEATS[]` (her seats stay under `CAB.seats`), so her
  driver is hidden inside the cab. `VehicleSidecarFacts` reads her with no error and four absences:
  the seat inside the cab, no FLOAT, no fifth wheel and no kingpin. Every number carries its
  provenance, and what could not be measured sits under `_confirm`.
- **No towing** (owner's ruling D4, 2026-09-18). Her `TOW` block is art only, and nothing reads a
  fifth wheel or a hitch from her. That is debt, and it needs its own ruling.
- **Steering is a visual angle.** No solver, and no coupling to heading: a game that locks the wheels
  over without turning the machine will look wrong, and the rig will not stop it.
- **Dimensions are stylised art measurements**, not manufacturer figures.
- **Cargo walkability and character reach are unruled** — the geometry is published, the decisions
  are not.
- **No Unity or in-world test rode on the drop.** This is an art candidate that CI has accepted, not
  a body anyone has driven.
