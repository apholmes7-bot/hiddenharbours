# The art director's parametric rigs — the SOURCE the sheets are baked from

These are the art director's own `.js` rigs, imported **verbatim**. They are pure procedural renderers
with no external asset dependencies: each exposes `render(dir, opts)` returning a `Uint8ClampedArray` of
RGBA, plus metadata (`W, H, order, ANIMS, BUILDS`, anchors, …).

**Why they live in the repo.** Under [ADR 0021](../../adr/0021-in-engine-js-rig-baking.md) sprite sheets
stop being hand-exported from a browser and become an **editor operation** — the baker runs these files
directly. Versioning the rig alongside the code means any hull can be re-baked at any facing count,
forever, without another art hand-off.

⚠️ **Do not edit these files.** They are the art director's source. Fixes belong upstream, or the next drop
silently reverts them. Anything the engine needs that the rig doesn't provide belongs in *our* host code.

---

## ⚠️ THE AZIMUTH SPLIT — read this before baking anything

The rigs do **not** share one facing convention.

> ⚠️ **CORRECTION (2026-07-19, from the first real bake).** An earlier version of this file said the split
> was determined by inspecting the sign of the `th = ±dir*Math.PI/4` term. **That method is NOT reliable and
> must not be used.** `puntIsoRig` and `lobsterBoatIsoRig` both carry a **positive** sign and both render
> **counter-clockwise** — handedness comes from the iso camera basis, not from that sign alone. The sign
> merely *correlated* with the answer on the rigs that had been pixel-checked.
>
> **Only measurement is authoritative.** The groups below are trustworthy where the art was measured
> (characters via face-skin centroid; punt/dory/Cape Islander via PCA bearing + bow-taper; punt again via a
> byte-identical golden-master bake). **Every other rig here is UNVERIFIED** — the baker must measure it at
> bake time, not read this list. Treat the list as a prior, never as a fact.

**CLOCKWISE-CORRECT (2)** — the art director fixed these at source; character sheets pixel-verified:
`characterIsoRig.js` · `rodIsoRig.js`

> ⚠️ **That verification is pass 1's, and it does not transfer to `characterIsoRig6.js`** (the
> 2026-08-02 drop — see the character kit section at the foot of this file). Pass 6 is a different
> renderer with a separate head rig in the projection path. Its clockwise claim is a **prior** until
> `CharacterRigAzimuthProbe` measures it at bake time; the bake refuses on a mismatch.

**CLAIMED CLOCKWISE, UNVERIFIED (fishing kit, 2026-07-22)** — `fishIsoRig.js` · `fishToteRig.js`.
Both carry `th = -dir*Math.PI/4` and the kit's contract declares 8 headings at 45° **CW** (fleet order
N NE E SE S SW W NW). Per the correction above, the sign term is *not* proof — the baker must verify
each with `CharacterRigAzimuthProbe` (which refuses on mismatch) before trusting the labels.
(`fishIsoRig` was measured CW by `FishingRigAzimuthProbe` in the #265 bake; `fishToteRig` and the
CCW-inferred `bucketRig` are measured at bake time by `StorageRigAzimuthProbe` — the tote by where
its leaning lid lands at the E/W rows, the bucket by the fish tray's diagonal chirality — and the
storage bake refuses on a catalog mismatch like every sibling.)

**COUNTER-CLOCKWISE (19)** — cell `i` depicts heading **−45°·i** while labelled `+45°·i`.
Pixel-verified: `puntIsoRig` (golden master — byte-identical until the v2 rig revised her, see below),
`doryIsoRig`, `capeIslanderIsoRig`,
`lobsterBoatIsoRig`. The rest are inferred and must be measured before use:
`bucketRig` · `capeIslanderIsoRig` · `coastalPacketIsoRig` · `consoleIsoRig` · `doryIsoRig` · `fishTubRig`
· `houseIsoRig` · `interiorIsoRig` · `interiorPropRig` · `lobsterBoatIsoRig` · `puntIsoRig` · `shovelIsoRig`
· `sideDraggerIsoRig` · `skiffMotorRig` · `sportSkiffIsoRig` · `sternTrawlerIsoRig` ·
`sternTrawlerMk2IsoRig` · `tankerIsoRig` · `wharfBuildingRig`

**No azimuth term (18 + 4 + 1 + 2)** — kits, props and creatures that aren't 8-way directional; they need no
convention. (`sceneKit`, `shorelineRig`, `potRig`, `foxRig`, …) The fishing kit adds `bobberRig` ·
`crustaceanRig` · `shellfishRig` · `catchKit` to this group; the drift-weed kit adds `driftWeedRig`
(flat water-surface clumps — the kit bakes NO heading by design); the terrain kits add
`shoreIsoKitRig` · `roadPathRig` (see the caveat below — they are *static tiles*, which is not the same
thing as being safe); the tree kit adds **`treeIsoRig`** (+ its batch harness `_treeBake.js`) — a tree
has no heading, so its sheet axes are **variant × sway frame**, not direction.

> ⚠️ **`treeIsoRig`'s pivot is the TRUNK FOOT, not the cell's bottom row.** The near root flare
> projects *below* it under the 40° camera, so the cell carries `pad` rows underneath the pivot
> (`sheetSpec().pivot` and `nearFlarePad` in `Trees.json`). Assuming bottom-centre — the convention
> every other tree sprite in this repo uses — **sinks every tree into the ground.** Precedent: the dory
> bakes at pivot (80,88) in a 160×156 cell for the same reason. See
> [`../tree-rig-kit/README.md`](../tree-rig-kit/README.md).
>
> This rig is also the **first to ship a complete public API** (`render` · `packMask` · `normalView` ·
> `sheetSpec` · `cellOf` + its constants, all on `root.TreeRig`). It needs **no symbol shim** — the
> thing ADR 0022 open question 4 has been asking of every other rig.

> **⚠️ "No azimuth term" ≠ "no compass risk" for the terrain kits.** `shoreIsoKitRig` bakes no
> turntable, so there is no heading to mirror and the probe machinery does not apply — but its cliff and
> fringe *pieces* are named by bearing (`faceS`, `cornSW`, `sideW`, `edN`…), and nothing has checked
> those names against rendered pixels. The slices are therefore named by GRID POSITION and the labels
> live in one place, `ShorelineIsoCatalog`, precisely so a wrong label is a one-line fix and not a
> re-slice. Treat those bearings as a prior, exactly like every list on this page.

**The BUILDING rigs.** `BuildingRigAzimuthProbe` reads the **door** anchor rather than a bow taper,
because a building has no bow for PCA to bite on and that probe would return noise dressed as an answer.
It runs at bake time and refuses on a mismatch, exactly like every sibling. Note the honest limit,
recorded in that file too: the door anchor is the same `projVert` arithmetic that draws the pixels (so it
is measurement, not a declaration), but it is **not** the independent pixel re-derivation the punt's
byte-identical golden master was.

- **`houseIsoRig` — MEASURED counter-clockwise (2026-07-30).** The first real building bake ran (*Art ▸
  Bake Village Buildings (M1 set)*, the M1 village kit) and the probe **agreed with the prior** on all
  five builds: the door lands screen-**west** at the cell labelled `'E'`, and the silhouette-vs-`Wd`/`Ln`
  cross-check passed, so the reading is evidence rather than a coin flip. Its listing above is no longer
  a prior. ⚠️ The correction is applied **in the bake**, so the shipped sheets' cell `i` genuinely depicts
  +45°·i — do not re-mirror them.
- **`wharfBuildingRig` — still UNMEASURED.** Its listing above stays a prior until *Dev ▸ Bake Buildings
  (houses + wharf)* runs and the bake either passes or refuses. Nothing consumes it yet (the shed/barn/
  cannery family is M2).
- **🔴 `interiorIsoRig` / `interiorPropRig` — counter-clockwise, but `BuildingRigAzimuthProbe` MUST NOT
  be used on them (measured 2026-08-05).** The probe above reads which side the **door** lands on at a
  quarter turn, and the step it does not state is that it assumes the door is on the **`+Y`** gable —
  true of both exterior rigs. **`interiorIsoRig` puts its doorway on `−Y`** (`anchors → pj(0,−Ln/2,fZ)`;
  the hearth takes `+Y`), because a room is entered over the near wall the open-dollhouse cutaway drops.
  Fed to that probe the room measures **Clockwise** — a wrong answer with a confident report — and the
  bake would then apply the opposite correction to all eight cells, silently.
  `InteriorRigAzimuthProbe` measures instead in three layers, none of them a declaration: both rigs'
  `project()` output compared per facing (one camera, one turntable), then each rig's door **gable**
  from `door.y` at dir 0 vs dir 4 (the door's height above the floor cancels out of that difference,
  which is the only reason one measurement works on a rig with a 0.55 m foundation and one whose floor
  is `z = 0`), then the single facing offset that lines the two door anchors up at **all eight**
  facings — which is the handedness proof and the registration number at once.
- **⭐ Consequence for anyone placing a room under a shell: `interior facing = exterior facing + 4`.**
  Same model, opposite gable, so the same cell index shows the two 180° apart. The offset is measured at
  bake time and written into `Interiors.json` as `exteriorFacingOffset`; `InteriorKit.InteriorFacingFor`
  is the one place it is applied. At any other offset the doorway lands against the back wall — you walk
  in the front door and appear at the back of the room, and it reads as an art bug.

⇒ **The baker MUST carry a per-rig convention flag. A blanket correction is wrong** — it would re-mirror
the two already-correct rigs. And the flag must be *machine-verified against the rendered pixels*, not
maintained by hand: this mislabel has now caused defects in five separate kits, every time because someone
trusted a declared order instead of measuring the art. Once a rig is baked in-engine with its convention
applied, `FacingsAreCounterClockwise` goes **false** for that artwork — the flag survives only for legacy
hand-exported sheets until they are re-baked.

---

## ⚠️ These differ from the previously-imported copies

`puntIsoRig.js`, `consoleIsoRig.js`, `sportSkiffIsoRig.js` and `skiffMotorRig.js` were already in the repo
under `docs/art/punt-iso-rig/` and `docs/art/skiff-fleet-rigs/`. **The versions here differ** (md5 mismatch
on all four) — these came from the art director's live project folder and are newer.

**`roadPathRig.js` is NOT one of them (checked 2026-07-23).** The road/path kit zip's copy `md5`s
differently from the one imported in #227, but that difference is **line endings only** — strip the CRs and
the two files are byte-identical. The committed `RoadIso_*_new_blob47.png` atlases therefore bake from
exactly the rig already in the repo. Worth recording, because an `md5` mismatch on a text file in a repo
with `eol` normalization is not evidence of anything: **diff it before believing it.**

**`shoreIsoKitRig.js` is new** and does *not* replace `shorelineKitRig.js`/`shorelineRig.js` — those bake
the older **near-plan** shoreline still sitting loose in `Art/Tilesets/`, this one bakes the **ISO** re-cut
that matches the boat camera. Both are kept: nothing already painted from the near-plan tiles should break.

The older per-kit copies have been removed so there is ONE canonical location; their `README.txt` files are
kept, since they document the shipped kits' cell sizes and pivots.

**Consequence for the golden-master test:** the first baker acceptance test bakes a hull and diffs it
against the sheet already shipped in `Assets/_Project/Art/Boats/`. If the punt does not match, the likely
cause is that the shipped art was baked from the *older* rig, not that the baker is wrong. Establish which
before chasing a phantom bug.

**And as of 2026-07-25 it no longer matches, for that exact reason.** The small-craft rig kit v2 revised
the punt, so `PuntIso.png` is one revision behind her rig: all eight paired cells fell to **98.08–98.42%**
(spread 0.34 pp) while the off-axis check held at 84.62%, i.e. uniform art drift, not a broken correction.
`PuntGoldenMasterTests` now asserts the **spread** rather than the absolute match, and says so in place.
To restore the strong form, the art director re-exports `PuntIso.png` from the v2 rig **by hand from his
browser** — re-baking it in engine would make the test compare the baker against itself.

---

## The small craft rig kit v2 (imported 2026-07-25)

Updated `doryIsoRig.js`, `puntIsoRig.js`, `consoleIsoRig.js`, plus two rigs that are **versioned but
wired to nothing**:

- `doryMotorRig.js` → `globalThis.DoryMotor` — the dory's little tiller two-stroke (cell 196×164,
  pivot 98,92 onto her 80,88; `maxSteer` 32°, `tiltMax` 40°, one `stock` variant). Not in
  `HullPropFleet`; landing it is its own piece of work. Note its export publishes neither
  `motorFaces` nor `swivelPt`, so it will need the same shim the other two motors take.
- `wheelRig.js` → the helm wheel at control scale. No `dir`, returns a canvas, needs a DOM — it sits
  outside the shared rig contract and has no consumer.

`skiffMotorRig.js` was **not** re-imported: the drop's copy is byte-identical modulo CRLF.

⚠️ **Paint became data on the punt and the console skiff.** Neither rig has a `MATS` constant any more —
`palette(opts)` derives every ramp from a named scheme — so both hulls stopped baking outright until
`RigMeshSymbols.Reconstructions` learned to read `palette({}).mats`. That pins their bake to each rig's
`DEFAULT_SCHEME` (**`harbour-white`** on both). Choosing a colourway at runtime, and the console's new
per-material `dith` weight, are **not** modelled — see the note on that reconstruction.

⚠️ **The dory's `'oars'` build is NOT "unchanged pixel-for-pixel"** as the drop's README claims. Measured
across 192 cells / 4.79 M px: 96 of 96 hull cells differ, 34,703 px, because four additions landed in the
shared face list and the two rowing thwarts were resized 131–145 mm narrower per side. Her oar layer and
every anchor ARE identical.

## Boats that exist only as a rig

No baked sheets exist for these — they can only ship once the baker does:
`lobsterBoatIsoRig` (Tier 3, ~12.0 m) · `coastalPacketIsoRig` · `sideDraggerIsoRig` ·
`sternTrawlerIsoRig` · `sternTrawlerMk2IsoRig` · `tankerIsoRig`

Most are M2/M3 fleet content — importing the source is **not** a licence to wire them (CLAUDE.md rule 8).

---

## The fishing rig kit (imported 2026-07-22)

One drop of every rig behind the fishing loop: the character that casts, the rod and its runtime FX,
the catch itself (fish / crustaceans / shellfish), and the storage it fills. Everything follows the M2
bake recipe (ADR-0006): fixed ¾ turntable, 8 headings at 45° **CW** (fleet order N NE E SE S SW W NW),
elev 40°, 32 px = 1 m, upper-left key light, ordered dither, 1 px keyline, no AA. All files are plain
browser scripts — each exposes ONE global and depends only on the globals it names.

New files in this folder (the kit's other nine were already here and arrived byte-identical):

### Character + rod (the cast)
> ⚠️ **`characterIsoRig.js` is PASS 1 and is superseded for every SHEET BAKE** by
> `characterIsoRig6.js` + `headIsoRig3.js` + `eyeIsoRig.js` (imported 2026-08-02, **section at the
> foot of this file**). The catalog's `character` entry points at pass 6; the cell is 64 × 92 and
> the pivot (32,82). **Do not bake a character sheet from this file** — a 64 × 88 sheet no longer
> slices.
>
> **It stays in the folder for exactly one reason**, and it is not sentiment: the ratified
> deck-character-MESH arc (ADR 0024) pins to it. `DeckCharacterMeshSpikeDef.SourceRigPath` names
> it and `CharacterPoseMeshSpikeGoldenTests` includes a **byte-identical source check**, so
> deleting it reds five tests in a live arc. Porting that spike to pass 6 re-baselines its golden
> numbers and is its own change, with its own eyeball — not a side effect of an art import.
>
> The API below is unchanged in pass 6 and still describes it: `anchors(dir,opts)` →
> handL/handR/head/hip cell px (the motor-mount pattern: every held thing pins to these),
> `tool(dir,opts)` → rod grip px + pitch/yaw/bend per frame, `carry(dir,opts)` → bucket and tray
> pins + swing.

- **characterIsoRig.js** → `CharacterIso` (pass 1, mesh-spike input only) /
  **characterIsoRig6.js** → `CharacterIso6` (pass 6, what bakes) — fishing anims: hold 6f, cast
  10f @70 ms (windup f0–3, snap f4–5 — the bobber launches at f5, settle f6–9), power-scaled
  short/long via `CAST_W1`/`CAST_S1` sub-ranges (`castBack`/`castRelease`).
- **rodIsoRig.js** → `RodIso` — 3 tiers (cane / coaster / deepwater), 112×112 bake, pivot = grip,
  pinned to handR. `tip()`/`tipLocal()` anchor the line; `project()` maps character-local 3D points to
  screen px for line/bobber/splash FX. CAST distances × `castMul` per tier. Rest poses: ground ×8 dirs
  + stored upright.
- **bobberRig.js** → `RodBobber` — the purpose-made float, 16×22, pivot (8,12) = the waterline.
  States: float 4f / nibble 4f / strike 4f / fly 2f. Underwater pixels bake with tint + alpha — never
  clip against water at runtime. Line attaches at the stem top.
- **splashRig.js** → `Splash` — the splash/ring burst FX used on entry, strike and land.

### The catch (every item is its own rig — no icons, this world is diegetic)
- **fishIsoRig.js** → `FishIso` — parametric 3D fish loft. `SPECIES` = one data block (len, girth,
  flatness, stripes + 5 hexes); `scale` sizes any catch on one skeleton; `hold(species,scale)` →
  `{mass, hands}` (<2.2 kg = one per hand, else two-arm cradle). Water anims: swim 4f / dart 2f /
  thrash 4f (surface break) / shadow 2f — pose z vs waterZ bakes a depth-graded underwater tint. Dry
  RESTS: deck 4 lays (fills + loose item), gill / tail (held, pivot = THE GRIP → pin to hand anchors),
  cradle (two-arm). `mouth(dir,opts)` = line attach in the surface fight. `spoil` 0..1 = the rot
  (green shift + dither mottle); rot motes are runtime FX in `FishIso.SPOIL` green.
- **crustaceanRig.js** → `Crustacean` — lobster + rock crab, SCALABLE rebuild: geometry in metres,
  replotted per render (never resampled). walk 4f / rear / defend / held 2f (dangled by the back,
  pivot = hpivot). `hold(kind,scale)` like the fish.
- **shellfishRig.js** → `Shellfish` — mussel + soft-shell clam: item (14×12, 4 lays, fills) and
  handful (22×16, pivot = grip, one per hand).
- **lobsterRig.js / rockCrabRig.js** → `Lobster` / `RockCrab` — the original fixed 48×48 deck/icon
  rigs. Kept for existing pages; **new work should use crustaceanRig**.

### Storage (containers fill with the catch's own rigs)
- **catchKit.js** → `CatchKit` — THE glue. `item(kind,{variant,scale,spoil})` → ready canvas + ground
  anchor for any catch (fish species / lobster / crab / mussel / clam). `fillItems(catch, fill, seed,
  capacity)` → seeded MONOTONIC item lists (growing a fill never moves earlier items; pass the
  container's slot count so full/brim genuinely heap). `tintSpoil` rots any rgba; `particles()` specs
  the motes.
- **fishToteRig.js** → `FishTote` — the ~1 m³ insulated deck tote (Cape Islander up): 5 colours, lid
  on/off/lean, pallet feet, genuinely hollow shell. `slots(dir)` → 4 stacked layers × 8 projected
  points rising from the floor (draw CatchKit items onto them, clipped to `opening(dir)`,
  back-to-front — layers visibly stack).
- **bucketRig.js** → `BucketIso` — steel pail / plastic pail / fish tray, carry + rest pivots,
  abstract fills (retrofit onto CatchKit planned).
- **fishTrayRig.js** → `FishTray` — the 32×24 deck tray with baked keepers (reshape to the grey
  stack-nest tote + CatchKit fills planned).
- **fishTubRig.js** → `FishTubIso` — the older on-deck tub prop.
- **buoyRig.js** → `LobsterBuoys` — per-fleet pot-marker buoys (spar shape, 8 schemes).

### The wiring cheat-sheet (the whole loop)
1. **CAST** — play character `cast`; at f5 read `tool()` wrist + `RodIso.tipLocal()`, launch the
   bobber (fly state) along the CAST arc; splash rings on entry.
2. **WAIT** — bobber float; bites: nibble dips; hook window: strike (pulled under).
3. **FIGHT** — FishIso shadow → thrash/dart at the surface; line attaches at `FishIso.mouth()`; the
   bobber rides just above it (strike while hooked, float while it tires, fly on the lift).
4. **HANDLE** — the landed fish is a rest bake: held by gill/tail (one per hand if light), cradled if
   heavy; crustaceans dangle by the back; shellfish by the handful.
5. **STORE** — drop into bucket / tray / tote: CatchKit items on the container's slots. Left too long
   → `spoil` climbs, everything greens, motes rise.

### Layering rules
Held/rod layers draw UNDER the character sprite for the away facings (NW / N / NE —
`RodIso.behind` = [7,0,1]); over it otherwise. Containers on boats: the boat's mount anchor carries
all translation; the container bakes only roll/pitch.

### Engine handoff
`gameplay/FisherRodMount.json` — frame-by-frame rod mount data (grip px per dir/frame, behind dirs,
pose sub-ranges) for engine-side integration without running the JS rigs. See `gameplay/README.md`.

The kit's demo pages (Fishing Rods.dc.html · Rod Bobber.dc.html · Fish Iso.dc.html ·
Catch Handling.dc.html) live in the art director's design workspace, **not** in this repo.

---

## The drift weed kit (imported 2026-07-23)

One drop for the seaweed/flotsam surface-drift decor: the parametric rig source, four baked variant
sheets, and the gameplay sidecar. Follows the fishing-kit pattern (parametric JS rig → bake → ramps +
sidecar) but is **NOT a turntable rig**: these are flat water-surface clumps seen in the ¾ iso view
(camera from the south, elev 40°), with **NO heading**. 32 px = 1 m, vertical foreshorten **0.72**
baked into the shapes. KTC pixel conventions: no AA, binary alpha, upper-left key light, banded colour
with ordered-dither band edges, 1 px keyline `#1b2a22` (the decor keyline — matches the flowers/grass
set). Silhouette first — reads at gameplay zoom.

### driftWeedRig.js → `DriftWeed`
The single rig source (plain browser script, one global, no dependencies). Landed **byte-identical**
to the drop — sha256 `bcada722…b0110c`, the exact hash the sidecar's `derivedFromRigSha256` tripwire
pins (`DriftWeedSheetSliceTests` verifies it, CRLF-normalized, on every run).

- `render(species, opts, rampKey)` → `{ w, h, rgba, anchors, params }` — `opts` is
  `{variant: 0..n-1}` for a shipped seed-locked cell, or raw `{seed, sizeM, fronds, sprawl, bladders}`
  for a live custom build.
- `SPECIES` — cell sizes, per-species param reads, the shipped variant seeds. `RAMPS` — per-species
  ramp sets. `PPU` 32 · `Q` 0.72 · `KEYLINE` `'#1b2a22'`.

### The species (each a parametric GENERATOR, not fixed drawings)
- **Bladderwrack** (*Fucus vesiculosus*) — knobbly forking fronds, paired air bladders on the outer
  half. The signature one. **48×36** cells, **4** variants (seeds 4101–4104).
- **Sugar Kelp** (*Saccharina latissima*) — one long torn ribbon: puckered blade, ruffled asymmetric
  edges, dark stipe stub, split torn tail. **64×36**, **3** variants (4201–4203).
- **Eelgrass** (*Zostera marina*) — fine tuft of thin blades combed downstream. **32×24**, **4**
  variants (4301–4304; `bladders` ignored).
- **Torn Mat** — mixed ragged raft: wrack bodies, a kelp scrap, loose strands over the edge, holed
  through. **64×48**, **3** variants (4401–4403).

Params are one uniform space, interpreted per species: `sizeM` clump span 0.4–2 m · `fronds`
blades/pieces/tail-strips · `sprawl` raggedness 0–1 · `bladders` bladder/pucker/fleck density 0–1.
Each shipped variant is its own seed-locked baked cell — **no mirroring assumptions**.

### The baked sheets — `Assets/_Project/Art/Sprites/Shore/Drift/*.png`
One sheet per species: variant columns × **3 ramp rows** (living / golden / bleached, top to bottom).
Structure is seed-stable — every row is the SAME build recoloured. Sliced by
`DriftWeedSheetSlicer` (menu: *Hidden Harbours ▸ Art ▸ Import (after a new drop) ▸ Slice Drift Weed
Sheets*) with each column's
**buoy as a per-variant Custom pivot** — the sidecar's "register the sprite to the water surface
here" — which is why these sheets are not `SpriteSheetSlicer` manifest entries (that table is one
pivot per sheet).

### Colour — ramps only (owner palette guard-rail)
- **living** — per-species olive/brown-green/grass-green derived from KTC master hexes (rockweed
  FLOAT ramp, dune `#7f8a54`, iris stem `#3f7a52`, ochre `#7d6a3a`/`#968049` — nothing invented;
  provenance per ramp in the sidecar).
- **golden** — sun-golden kelp set, shared; wet step = fleet gold `#e0b13a` verbatim.
- **bleached** — storm-bleached pale set, shared; wet step = bone `#e9e6df` verbatim.

Every ramp carries a **wet-surface glint step** (`ramp.wet`) — sky-glint specular on the upper-left rim.

### The gameplay sidecar — `Assets/_Project/Art/Sprites/Shore/Drift/DriftWeed.json`
Per shipped cell:
- **buoy** — buoyancy centre (area centroid). THE PIVOT. px, cell-local (origin top-left, +y down).
- **snags** — 2–3 outer-frond tips ≥60° apart (catching on buoys/rocks/rope). px + metres from buoy.
- **dragTail** — the end that trails when drifting (kelp: the torn blade tip; else the farthest tip
  from the buoy). px + metres.

Metre frame: `mx=(x−buoy.x)/32`, `my=(y−buoy.y)/(32·0.72)` (+y toward camera/south).
`derivedFromRigSha256` is the drift tripwire — re-bake if the rig source changes. Anchor provenance
recorded per variant.

### Owner rulings (2026-07-23) — the kit's four `_confirm` judgments, RULED
Recorded in place in the sidecar as `_ruled` (the PR #247 append-only style — original judgment text
kept):
- **TornMat.dragTail** — KEEP THE BAKED TIP: the longest-scrap tip stands; no runtime down-drift picking.
- **golden_rows** — ALL FOUR species ship their golden rows (runtime may weight kelp toward golden).
- **snag_radius** — **0.1 m** is the engine default catch radius (tunable data in the runtime feature).
- **stranded_set** — exclusion CONFIRMED: the existing `Shore/Seaweed*` rockweed tiers own the wrack
  line; no stranded recolour set.

### What is NOT in this kit (by design)
No animation frames (drift, bob, clumping, sway are runtime, driven by the shared wave field — the
wake work landed the reusable pieces: pooled deposit-anywhere emitters + the ride-the-displaced-sea
read), no heading bakes, no mirrored cells.

### The wiring cheat-sheet (for the future runtime drift feature — NOT built in the import PR)
1. **SPAWN** — pick species/variant cell; draw registered at the buoy on the water surface.
2. **DRIFT** — translate the buoy along the current; yaw slowly so dragTail trails down-drift; bob
   from the shared wave field read at the buoy.
3. **SNAG** — test snag points (ruled 0.1 m radius) against buoys/rocks/rope; a caught snag becomes
   the pivot, the clump weathervanes about it.
4. **CLUMP** — rafts: near buoys attract; snag-to-snag contact links clumps loosely.
5. **WEATHER** — swap ramp ROW, not structure: living day-to-day, golden sun-struck kelp, bleached
   after storms.

The kit's demo page (Seaweed Drift Kit.dc.html) lives in the art director's design workspace, **not**
in this repo.

---

## The character rig kit, pass 6 (imported 2026-08-02)

One procedural person: eight facings, **fourteen** animations, four carry stances, and the axes that
make her somebody in particular. This drop replaces the pass-1 body (`characterIsoRig.js`) and splits
the head and the eyes into rigs of their own.

### ⚠️ LOAD ORDER IS A HARD REQUIREMENT

The body delegates skull / hair / beard / hats to the head rig, and the head delegates the eye socket
to the eye rig. Load them **eye → head → body**, in that order, into the same host:

```
docs/art/rigs/eyeIsoRig.js         → globalThis.EyeIso                     (1st)
docs/art/rigs/headIsoRig3.js       → globalThis.HeadIso3 / HeadIso2 / HeadIso   (2nd)
docs/art/rigs/characterIsoRig6.js  → globalThis.CharacterIso6              (3rd — the body)
```

Loaded out of order the body still *runs* — `hatList()` quietly falls back to its local `HATS_LOCAL`
table and the face never stamps — so this fails as **wrong art**, not as an exception.
`RigCatalog.Install` loads exactly one file, so a character bake must install the two prerequisites
first (the `catchKit` canvas-shim precedent: whatever the rig needs and does not provide is the
host's job, never a patch to his file).

The body registers `CharacterIso5` and `CharacterIso` **only if those names are free**, so a page
that still loads pass 1 keeps pass 1. In-engine the catalog names `CharacterIso6` explicitly rather
than relying on that fallback.

### The cell contract — and the port that comes with it

| | pass 1 | **pass 6** |
|---|---|---|
| Cell | 64 × 88 | **64 × 92** |
| Pivot (top-left origin) | (32, 80) | **(32, 82)** |
| Ground inset (`H − pivotY`) | 8 px | **10 px** |
| Unity pivot (ADR 0026, `(H−pivotY)/H`) | 8/88 ≈ 0.0909 | **10/92 ≈ 0.1087** |
| Scale | 32 px = 1 m | 32 px = 1 m |
| Facings | 8, `N NE E SE S SW W NW` | 8, same order *claimed* |
| Camera / light | 3⁄4, elev 40°, upper-left key | unchanged |

Mounts computed from `anchors()` / `tool()` / `carry()` absorb the two-row shift for free. Anything
that hard-codes 88 or 80 does not — and the sheet slicer's ground inset is exactly such a constant.

### ⚠️ The azimuth claim is a PRIOR again, not a fact

The kit README states facing order `N NE E SE S SW W NW`, azimuth **clockwise**. The pass-1 body was
pixel-verified clockwise and is listed as such at the top of this file — **that measurement does not
transfer**: pass 6 is a different renderer with a new head rig in the projection path, and this lane
has been CCW-mislabelled twice. `CharacterRigAzimuthProbe` measures it from rendered pixels at bake
time and the bake refuses on a mismatch, exactly like every sibling. Until that bake has run, treat
the clockwise claim as a prior.

### Animations — fourteen, and the mount contract is in the rig

Frame counts and `ms` come from `C.ANIMS`; which layer each one drives comes from `C.ANIM_MOUNT`.
Never restate either in engine code.

| anim | f | ms | mount | | anim | f | ms | mount |
|---|--:|--:|---|---|---|--:|--:|---|
| `idle` | 6 | 170 | free | | `bite` | 6 | 150 | rod |
| `walk` | 8 | 110 | free | | `strike` | 6 | 80 | rod |
| `run` | 6 | 80 | free | | `reel` | 12 | 90 | rod |
| `balance` | 8 | 150 | free | | `land` | 12 | 100 | rod |
| `stagger` | 10 | 90 | free | | `castBack` | 6 | 90 | rod |
| `hold` | 6 | 170 | rod | | `castRelease` | 8 | 70 | rod |
| `cast` | 10 | 70 | rod (`power:'short'\|'long'`) | | `dig` | 10 | 90 | **shovel** |

`C.GROUPS` bundles them: `base` = idle/walk/run · `balance` = balance/stagger · `fishing` = the eight
rod states. The four carry stances (`C.CARRIES`: `buckets` · `tray` · `helm` · `oars`) ride the
**free** anims only — a tool anim always wins and `carry` is ignored.

### Customization — colour is data, structure is geometry

`character/options.json` carries every axis and every ramp, dark → light, exactly as the rig ships
them; `character/presets.json` resolves the ten cast builds. The split that matters downstream:

- **Colour (7 axes)** — `skin` 9 · `hair` 9 · `outfit` 6 · `shirt` 7 · `hatCol` 6 · `apronCol` 3 ·
  `eyes` 5. Ramps, so a colour change is a ramp swap and never a re-bake.
- **Structure** — `sex` (a skeleton switch, not a costume), `age` (child/youth/adult/elder),
  `garment` (7), `hat` (6 + bare), `hairStyle` (8), `beard` (8), `height`/`weight` (≈0.85–1.15),
  `headSize` (0.9–1.1). These move geometry, so they are baked.

The ten presets, in `C.CAST` order — no two share a sex/age/garment triple, which is the cast's
whole job:

| key | who | sex | age | garment | hat | height |
|---|---|---|---|---|---|--:|
| `fisher` | Fisher | m | adult | bib overalls | — | 1.51 m |
| `ginny` | Ginny | f | adult | bib overalls | oilskin hood | 1.44 m |
| `skipper` | Skipper | m | elder | oilskins | sou'wester | 1.42 m |
| `nan` | Nan | f | elder | skirt + shawl | kerchief | 1.35 m |
| `deckboss` | Deck boss | m | adult | quilted vest | flat cap | 1.51 m |
| `packer` | Packer | f | adult | gutting apron | kerchief | 1.44 m |
| `cutter` | Cutter | f | youth | gutting apron | — | 1.28 m |
| `hand` | Deckhand | m | youth | work shirt | ball cap | 1.34 m |
| `boy` | Wharf boy | m | child | knit jumper | watch cap | 1.09 m |
| `girl` | Wharf girl | f | child | work shirt | — | 1.04 m |

`build:{preset:'nan'}` loads one; overrides layer on top of it. Omitted keys fall back to
`C.DEFAULT_BUILD` (adult man, bib overalls), so a pass-1 build still resolves.

### Afloat, and the other API

`render(dir,opts)` · `renderAt(px,dir,opts)` · `anchors` · `tool` · `carry` · `metrics(build)` ·
`projectLocal` · `counter(roll,pitch,gain)`. A character rides a hull's swell through four extra
opts — `roll` / `pitch` / `heave` / `counter` — fed straight from a hull rig's `rock(i)`, with the
sign flipped for aft stations and `counter` 0 (passenger) … 1 (working crew). The rig still owns no
`ROCK` block of its own, so `RigCatalog.Install` reporting `rockFrames 0` remains correct.

### The three prop rigs arrived unchanged

`bucketRig.js`, `rodIsoRig.js` and `shovelIsoRig.js` ship in the kit and are **content-identical to
the copies already here** — the only difference is LF against the repo's checked-out CRLF, which
`core.autocrlf` invents and git never stored. They were left untouched rather than rewritten. Same
lesson as `roadPathRig.js` above: an `md5` mismatch on a text file in this repo is not evidence of
anything. Diff it before believing it.

### Known open (flagged by the kit itself)

- The oilskin hood reads as a cowl at the rear facings.
- The kerchief's knot is a ball rather than a tie: fine at 32 px/m, not at 48.
- Resolution above 32 px/m needs the hat bands **re-solved**, not rescaled — they are solved in rows
  above the eye, not in metres.
- `headIsoRig3.js`'s header comment says "pass 3" while its API reports `pass: 7`. The filename and
  the global are what the engine binds to, so this is cosmetic — noted so the next hand does not
  read it as two different files.

The kit's `harness.html` (standalone viewer + sheet baker) is **not** imported: previews live in the
art director's design workspace, and in this repo the baker is an editor operation under ADR 0021.
