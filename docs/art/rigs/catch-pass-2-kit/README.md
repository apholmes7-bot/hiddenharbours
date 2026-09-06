# Hidden Harbours — Catch Pass 2 Kit
Fish · lobster + rock crab · shellfish · the clam hod · the catch kit that fills containers with them.

The second pass on the catch as one drop: the five pass-2 rigs, the one file they depend on, a data
sidecar per rig, and hashes for all of it. This carries the files that the "Catch pass 2 (2026-09-04)"
section of `fishing-rig-kit/README.md` describes; the pass-1 files in that kit are untouched and still
load — pass 2 is a superset on the same cells and pivots. Review page in the main project:
**Catch Pass 2.dc.html**.

```
Art/isoSolid.js         IsoSolid      the shared turntable + painter — load FIRST
Art/fishIsoRig2.js      FishIso2      7 species · swim / dart / thrash / shadow / roll / jump · 4 rests · shoal()
Art/crustaceanRig2.js   Crustacean2   lobster + rock crab lofted as solids · walk / rear / defend / held · flip · sidle · burrow
Art/shellfishRig2.js    Shellfish2    mussel / clam / scallop / oyster / periwinkle at true size · beds · the flat · heaps
Art/clamHodRig.js       ClamHod       the wire roller basket — hollow, back / front layers
Art/catchKit2.js        CatchKit2     item / fillItems / heap / hold across all 14 kinds
sidecars/*.rig.json     one per rig: cells, pivots, frame tables, motion, contracts — see sidecars/README.md
_catchSidecars.js       the harness that wrote the sidecars; re-run it, never hand-edit them
SHA256SUMS.txt          every file above
```

Plain browser scripts, one global each, no build step. Load order: `isoSolid` → `fishIsoRig2`,
`shellfishRig2` (no deps) → `crustaceanRig2`, `clamHodRig` (need IsoSolid) → `catchKit2` (needs
whichever catch rigs the fill uses). The clam-dig demo on the review page also loads the character
chain (eye → head → characterIsoRig6 → shovelIsoRig); those live in `character-rig-kit`, not here —
nothing in this kit needs them.

## The recipe (unchanged)
M2 bake recipe (ADR-0006): fixed 3/4 turntable, 8 headings at 45° CW — N NE E SE S SW W NW, `dir`
0..7 — elev 40°, 32 px = 1 m, upper-left key, ordered 4×4 dither, no AA. **Ringless** (ADR 0031):
`KEYLINE_DEFAULT = false` in every rig; `{outline:true}` (fish, crustaceans) / `{keyline:true}` (hod)
is the live A/B, never the shipping bake. Underwater pixels bake the depth-graded tint + alpha
(`waterZ`), `spoil` 0..1 bakes the rot — nothing is clipped or tinted at runtime.

## What pass 2 changed
**Strict world scale.** scale 1 = the real animal: `SPECIES.len` / `SIZES` in metres × 32 px. A cod
is 22 px, a herring 8, a mussel 2×1, a periwinkle one pixel. No readability floor; the beds carry the
shellfish. `scale` is the specimen — `SPECIES.range` is the spread the game rolls (a 0.6× cod is a
42 cm market fish, a 1.5× cod a metre). `hold(kind, scale)` turns that into mass and hands.

### fishIsoRig2.js → `FishIso2`
Cell 64×64, pivot (32,38) = the water-surface point under the body centre. Seven species: the four
pass-1 blocks verbatim (cod, haddock, pollock, mackerel) + striped bass (lateral stripe band, two
dorsals), flounder (a true flatfish — `zflat`, fringe fins down both sides, eyes on top, undulating
swim, lies FLAT on deck) and herring (`shoal:true`, silver). Fins that read at 20 px: paired
pectorals, anal fin, second dorsal, gill edge, dithered spots on the flounder.
- Water anims: **swim** 6f @120 ms (body-wave lag behind the tail beat) · **dart** 3f @80 ·
  **thrash** 4f @110 (head through the surface) · **shadow** 2f @280 · **roll** 4f @130 (belly flash
  at the surface) · **jump** 6f @95 (z arc + pitch baked; the page moves it `MOTION.jump.travel` =
  0.55 m forward over the 6 frames).
- `MOTION` m/s along the heading: swim .35 · dart 1.4 · thrash 0 · shadow .18 · roll .12 · jump .9.
- Rests (pass-1 contract): **deck** 4 lays · **gill** 2f · **tail** 2f (held; pivot = THE GRIP, pin
  to a CharacterIso hand anchor) · **cradle** 2f (two-arm; pivot = the midpoint of both hands).
- `mouth(dir, opts)` → line-attach px in the surface fight · `hold(species, scale)` → {mass, hands}
  (< 2.2 kg one per hand, else cradle) · `sizeOf` · `shoal(species, n, t, opts)` → runtime singles
  (positions + heading + 8-dir bake + swim frame; nothing is baked as a group) · `dirOf(heading)` ·
  `sheetOrder()` (the 35-column sheet layout).

### crustaceanRig2.js → `Crustacean2`
Cell 64×64, pivot (32,40) = ground centre; **held** uses `hpivot` (32,12) = the grip on the back.
Needs IsoSolid. Both animals are lofted as real solids (carapace / abdomen lathes, claws with a
movable dactyl, tail-fan plates); legs and antennae are depth-tested 1 px plots.
- Poses: **walk** 4f @140 · **rear** · **defend** (claws up, gape) · **held** 2f @420 ·
  lobster **flip** 4f @70 (tail-flip escape: `MOTION.lobster.flip` travel −0.32 m per cycle
  BACKWARD, hop [0, .06, .10, .04] m) · crab **sidle** 4f @120 (0.16 m/s along body +x — a crab faces
  ACROSS its travel) · crab **burrow** 4f @160 (sink [0, .014, .030, .046] m; pixels under `sandZ`
  are cut with a dithered edge, the page draws the mound). walk v: lobster .07, crab .05.
- `ang` (rad, 0 = N, CW, continuous) turns the animal on the spot; `dir` is the camera; they compose.
  A kind's invalid poses render as walk (verified by rendering — see the sidecar).
- Sizes at scale 1: lobster 0.40 m overall / 0.7 kg; rock crab 0.13 m body, 0.30 m span / 0.4 kg.

### shellfishRig2.js → `Shellfish2`
Item 8×8, pivot (4,6) = ground (the odd loose shell) · handful 8×8, pivot (4,4) = the grip (one clutch
per hand) · 4 item variants, 2 handful variants. Tones: the pass-1 mussel/clam ramps, shorelineRig's
SCALLOP and PERI, yardIsoRig's crushed-oyster SHELL — nothing invented.
- `bed(kind, {w, h, seed, cover})` → alpha-masked texture to lay over rock / sand / mud (mussel,
  periwinkle, oyster, scallop). A clam flat shows NO clams.
- The flat: `holes(seed, n, w, h)` → keyholes; `spurt(hole, t)` → null | {rise 0..1, u} every
  2.6–7.8 s for 420 ms. Draw a 1 px jet, rise × 4 px tall — the gameplay TELL for the dig.
- `heap(kind, w, h, seed, mask, {dome})` → heaped shells clipped to a container opening.

### clamHodRig.js → `ClamHod`
Cell 40×40, pivot (20,30) = ground centre; `cpivot(dir)` = the roller grip. 0.42 × 0.30 × 0.20 m,
galvanised wire on a bent-wire frame, ash bail. Hollow like the tote: `render(dir, {layer:'back'})`
→ the heap → `{layer:'front'}`; `opening(dir)` = the rim quad, `depthPx()` = 4. Needs IsoSolid.

### catchKit2.js → `CatchKit2`
The glue over all 14 kinds (+ `mixed`). `item(kind, {variant, scale, spoil})` → ready canvas +
ground anchor · `fillItems(catch, fill, seed, slotCount)` → seeded MONOTONIC list (growing a fill
never moves earlier items; small fish pack DENSE — herring ×3, mackerel / flounder / crab ×1.5) ·
`isHeap(kind)` — shellfish fill as `heap(kind, rimPoly, depthPx, fill, seed, {spoil})`, lowered for
partial fills, crowned at the brim · `hold(kind, scale)` across fish / crustacean / handful · `SIZES` ·
`FRAC` empty 0 / few .25 / half .55 / full .85 / brim 1 · `tintSpoil` · `particles`. Containers: the
existing tote / stack-nest tray / wire pot / pail rigs + the hod.

## Wiring — what changed in the loop
Rod cast and trap haul are unchanged from pass 1. New: **the clam dig** — CharacterIso6 `dig` (10f)
with ShovelIso pinned by `tool()`, at a spurting keyhole; the clam turns up in the spoil
(`CatchKit2.item('clam')`) and goes into the hod: `ClamHod.render(dir, {layer:'back'})` →
`CatchKit2.heap('clam', ClamHod.opening(dir), ClamHod.depthPx(), fill, seed)` clipped to the opening →
`{layer:'front'}`.

Storage: fish and crustaceans blit as items on the container's `slots()`; shellfish are a heap
clipped to `opening()`. Spoil greens items and heaps alike; the motes are runtime FX in
`CatchKit2.SPOIL`.

Swapping a pass-1 page: FishIso → FishIso2, Crustacean → Crustacean2, Shellfish → Shellfish2,
CatchKit → CatchKit2. Cells, pivots and `hold()` contracts are kept, so nothing moves a pixel; pass 2
adds species, anims and the strict scale.

## Layering
Held / pinned layers draw UNDER the character sprite for NW / N / NE (dirs 7, 0, 1), over it
otherwise. In the water: bottom, then a dithered shadow ellipse under each swimmer (fading with depth),
then sprites sorted by world y, then surface glints and splash rings. Containers on boats: the boat's
mount anchor carries all translation; the container bakes only roll / pitch.

## Verify before trusting any of this
```
sha256 Art/fishIsoRig2.js     -> c97f5f76…   must equal derivedFromRigSha256 in sidecars/fishIsoRig2.rig.json
sha256 Art/crustaceanRig2.js  -> 40975234…   … in sidecars/crustaceanRig2.rig.json
sha256 Art/shellfishRig2.js   -> 3bcd4cef…   … in sidecars/shellfishRig2.rig.json
sha256 Art/clamHodRig.js      -> 142c63db…   … in sidecars/clamHodRig.rig.json
sha256 Art/catchKit2.js       -> 4afe6d0d…   … in sidecars/catchKit2.rig.json
sha256 Art/isoSolid.js        -> f7fc9db5…   generated.isoSolidSha256 in every sidecar
```
Full hashes in `SHA256SUMS.txt`. If a rig hash has moved, the animal was reshaped: re-run the harness
rather than editing a sidecar.

## Regenerate the sidecars
```
for (const f of ['isoSolid','fishIsoRig2','crustaceanRig2','shellfishRig2','clamHodRig','catchKit2'])
  (0,eval)(await readFile('Art/' + f + '.js'));
(0,eval)(await readFile('Art/_catchSidecars.js'));
await CATCH_SIDECARS({ readFile, saveFile, log, dir: 'export/catch-pass-2-kit/sidecars/' });
```
The harness reads every number from the rigs' own tables and functions, refuses to write a file it
cannot stamp (D1), and hashes the bytes at the moment of writing (D2).

## Not in this drop
- The pass-1 catch files (fishIsoRig, crustaceanRig, shellfishRig, catchKit, lobsterRig, rockCrabRig)
  — untouched, still in `fishing-rig-kit`.
- The character chain + shovel the dig demo uses — `character-rig-kit`.
- The containers the kit fills (fishToteRig, trayIsoRig, trapIsoRig, bucketRig) — `fishing-rig-kit` /
  `deck-loop-kit`.
- Baked PNG sheets. The rig is the asset; the review page bakes a sheet per heading on demand, and
  `sheetOrder` in the fish sidecar is the column map for a headless bake.
