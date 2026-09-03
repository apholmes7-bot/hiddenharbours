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

**CLOCKWISE, MEASURED (deck-loop kit, 2026-08-09)** — `deck-loop-kit/Art/isoSolid.js` and every rig
that lathes against it: `deckGearRig` · `trapIsoRig` · `trayIsoRig` · `buoyIsoRig` (and
`trapFaunaRig`, which has no facing axis at all). This kit rides **its own turntable**, not the
fleet's, and it turns the **opposite way to the boats**. Measured against this repo's own
registered-CounterClockwise reference (`iso-rig-pack/utility-iso`) in one harness with one sign
convention — +X ground-plane bearing, depth un-squashed by `/sin 40°`:

| family | +X ground step | +Y at `dir` 2 |
|---|---:|---|
| `iso-rig-pack/utility-iso` (registered CCW) | **+45°** | screen-WEST |
| `deck-loop-kit` | **−45°** | screen-EAST |

The kit's drop README declared CW and — unusually — **is correct**. Applying the fleet's CCW
correction to this kit mirrors all eight cells of every piece.

> ⚠️ **The −46.75°/step screen figure does not distinguish the two.** This kit's screen mean is
> −46.75, the same number the iso-rig-pack contracts record for their *CounterClockwise* rigs,
> because that mean is an alternating foreshortened quantity (see `iso-rig-pack/VERIFICATION.md`
> §6). Only the **un-squashed ground-plane bearing** is a handedness test. Two families can share a
> screen mean and turn opposite ways; these do.

Re-measure any time with `node docs/art/rigs/deck-loop-kit/_verify.js`; details and the rest of the
kit's measurements are in [`deck-loop-kit/IMPORT.md`](deck-loop-kit/IMPORT.md).

> ✅ **The keyline gate has landed on all five bakeable rigs, so this kit bakes.** Its gap was the
> *opposite shape* to the shipyard's, and the difference is worth knowing before auditing the next
> kit. The shipyard arrived drawing the ring; **this kit's default render was already ringless** —
> every rig passed an explicit `keyline:false` into the shared `isoSolid.paint`. What was missing was
> the **exported declaration** `AssertKeylineGated` probes, and a missing declaration is mechanically
> indistinguishable from ringed art, so the gate refused five families of already-compliant work.
> **Audit the export, not the pixels.** The gate binds `KEYLINE_DEFAULT` to the turntable's own
> constant rather than re-declaring `false` per rig — `isoSolid.paint` owns the only ring pass this
> kit has, and a second copy could drift from the pass it claims to gate. `trapFaunaRig` is the
> exception: a flat 2D plotter with no ring pass at all, so its `false` is local and means "ringless
> by construction". Nothing moved — 24/24 cells reproduce, 0 painted pixels changed.
>
> ⚠️ **This kit spells the A/B arm `{keyline:true}`; the rest of the repo spells it `{outline:true}`**
> (#463, #477). Both now work on every rig here. Before they did, an A/B driven with the
> repo-standard name was **silently ignored** and came back ringless — indistinguishable from a
> successful gate-off render, which is the failure the positive-control arm exists to catch.

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

**COUNTER-CLOCKWISE, MEASURED (iso rig pack, 2026-08-06)** — `iso-rig-pack/wharf-kit-iso/wharfIsoRig`
· `iso-rig-pack/wharf-decor-iso/wharfDecorRig` · `iso-rig-pack/utility-iso/utilityIsoRig`. Not
inferred from the `th = ±dir·π/4` sign (which the correction above forbids) and not taken from the
pack README: each was measured to turn its +X axis **−46.75° of screen rotation per dir step** — the
same figure, to the digit, as `houseIsoRig` / `wharfBuildingRig` / `interiorIsoRig`. Their `project()`
outputs also agree to **0.000000000 px** relative to each rig's own origin, so the three ride ONE
turntable rather than three that resemble each other. The bake still probes from pixels.

**COUNTER-CLOCKWISE, MEASURED (shipyard iso kit, 2026-08-09)** — `shipyard-iso-kit/shipyardIsoRig`
(20 yard parts + 5 whole sites, `ShipyardIso`). Measured the same way and for the same reason: its
`project()` returns figures **identical to `wharfIsoRig`'s at all 8 dirs to 4 decimal places**, so it
rides that same turntable and inherits its convention.

> ⚠️ **The calibration is the evidence, not the sign the probe prints — and this rig is the case that
> proves it.** A probe reading the screen angle of the +X axis reports **+46.75°/step** for the
> shipyard, the same magnitude as the −46.75 recorded above but the *opposite* sign. That is the
> probe's own convention, not a mirrored rig: run the identical probe against `wharfIsoRig` and it
> returns **+46.75 too**. The two rigs' `camBasis`/`projRaw` are the same formula character-for-
> character (as are `doryIsoRig`'s and `houseIsoRig`'s). **Always calibrate a new family's probe
> against a rig already measured before believing its sign** — reading the positive `th = +dir·π/4`
> term as clockwise would have registered this family mirrored and flipped all 25 keys at once.
> Details: [`shipyard-iso-kit/VERIFICATION.md`](shipyard-iso-kit/VERIFICATION.md) §2.
>
> ✅ **The keyline gate has landed, so this kit bakes.** It arrived after the 2026-08-06 ruling with
> the 1 px ring hard-on, and `IsoPackContract.AssertKeylineGated` refused the whole kit; it now
> carries `KEYLINE_DEFAULT = false` in the same shape #463 gave the four pack rigs above. The cells
> did not move — this family measures the buffer, which is sized before the ring pass runs.

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

**NOT A RIG AT ALL — the building lifecycle PASS (2026-08-19)** —
`building-lifecycle-kit/buildingLifecycleRig.js` (`BuildingLifecycle`). The only entry in the catalog that
draws nothing of its own: it runs **between** a host rig's `build(b)` and its `paint()` and hands back the
same building at an earlier or a later point in its life — 7 construction phases
(`site foundation frame rafters sheathed cladding finished`), 4 states of dereliction
(`sound neglected abandoned collapsing ruin`), and a `burnt` modifier that composes with any of them.
Because it reads the host's REAL faces, every preset and every dialled config is covered with no
per-config authoring. It has no azimuth — it inherits whatever facing the host is rendering — so its entry
carries the `Clockwise` placeholder every non-directional entry uses and nothing probes it.

Bound into **`wharfBuildingRig` · `houseIsoRig` · `shopfrontRig`** by a two-line hook after `build(b)`, and
declared a **prerequisite** of all three in `RigCatalog`. Adopting the hook was measured to change
nothing: all three hosts render **byte-identical** to their pre-hook selves on every finished build, and a
full re-bake returned the five committed M1 village sheets byte-for-byte (`BuildingLifecyclePassTests`).

> ⚠️ **An unrecognised state id is SILENTLY IGNORED.** `active()` returns true for any non-default value,
> then `normPhase`/`normDecay` fall back to the default and hand the faces back untouched — so
> `decay:'collapsed'` (the natural misspelling of `collapsing`) renders byte-for-byte as `finished`, with
> no throw, the right cell size and the right sprite count. `BuildingLifecycleStates.AssertKnown` refuses
> one, and a lifecycle bake must also render *differently from the same build with the state taken off*.
>
> ⚠️ **The azimuth probe must be pointed at the SOUND building, not the derelict.** Its cross-check
> compares the drawn silhouette against the rig's own `Wd`/`Ln`, and a ruin's planks lie on the ground
> **outside** its footprint — measured at 2.2–2.4× (netShed@ruin 281 px against 124 expected;
> cannery@collapsing 681 against 305). `BuildingBakeRequest.UnderlyingOptsJs` is where a build names the
> one underneath it. The pass does not touch `anchors()`, so the convention and the footprint belong to
> the sound building either way.
>
> **Not covered:** `shop-building-kit/shopBuildingRig` (cutaway interior) and `shipyard-iso-kit` take a
> different face signature — the kit README's own stated limit, not an omission.

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

⚠️ **…and on the Cape Islander (2026-08-12), authored in-repo rather than imported.** She takes the same
API — `SCHEMES` / `schemeIds` / `defaultScheme` / `palette(opts)`, the same OKLCH envelope, no shim — so
she needed one `Reconstructions` line (`palette({}).mats`, pinned to `DEFAULT_SCHEME` **`sage-green`**)
and one row in `HullPaintSchemeBaker.Fleet`. **Eight schemes over four painted roles** (hull 7 steps /
boot 5 / house 7 / cove 4); sole, washboards, glass, iron and mast metal stay shared.
The pass-1 KTC ramps ride as literals on the default, so an unset colourway is the shipped boat byte for
byte — measured pre-vs-post in the V8 harness: table identical entry for entry and in key order,
509-face list byte-identical, all 8 facings 0 bytes apart, committed hull mesh a no-op.
Her `dith` weight is deliberately **not** ported either: adding it would have moved those pixels.

✅ **`Art/Boats/CapeIslanderIso.png` was RE-BAKED through RigBaker (full-mesh rollout PR 2b, 2026-09-02)** —
it had been STALE since #224 (baked before #247 ran her washboards out to the foredeck: 1,767 px / 0.56% of
opaque in a band forward of amidships) and, more to the point, it was a **pre-rig hand export**: 8 cells,
counter-clockwise, pivot recovered from pixels at (228, 263) ±4. It is now the lobster's shape exactly —
32 facings on an 8×4 page, 4 rock frames on `CapeIslanderIsoRock0/1.png`, the rig's declared pivot (228,
258), genuinely clockwise (`FacingsAreCounterClockwise = false`), with `CapeIslanderIsoAnchors.json` beside
it — and `IsoFacetCapeEndToEndTests` compares her committed mesh against it at eight headings, the same
detector the lobster has. The `PuntIso`/`ConsoleIso` sheets remain the stale hand exports in this family.

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
  screen px for line/bobber/splash FX. CAST distances × `castMul` per tier.
  **ONE ROD, EVERY STATE** (rod-continuity fix): held, cast, set down or stowed, the grip centre is
  the cell pivot and the yaw is `HELD_YAW` — every state resolves through the one `poseOf()`, so no
  transition can teleport, resize or re-point the rod. Rests are `REST_FRAMES`-frame **animated
  hand-overs**, not props: `ground` (laid underfoot), `stowV` (upright, butt down — `stored` is the
  shipped alias) and `stowH` (across the pegs), each starting at the `STANCE` the hand handed it over
  from and releasing at `RELEASE_AT`, mid-animation. How high a rest holds the rod is `restLift()`
  data, never a pixel offset. Held by `node tools/rig-recipes/rod-continuity.mjs` and, in-editor, by
  `RodContinuityTests`.
  ⚠️ **`shovelIsoRig.js` still has the pre-fix shape** (`rest:'ground'`/`'stored'` as single cells
  with their own yaw and their own `zOff`) — the owner's law is all tools, and the shovel has not had
  this pass yet.
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
per-state pose curves) for engine-side integration without running the JS rigs. **Generated, not
authored**: `node tools/rig-recipes/fisher-rod-mount.mjs` (and `--check` to prove the committed file
still describes the rigs it names). See `gameplay/README.md`.

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

### The wiring cheat-sheet (the runtime drift feature — built: #195 the system, #301 the art at native size, round 2 the anchors)
Round 2 wires the anchors exactly as listed below: a drifting clump yaws so `dragTail` **trails behind** the transport (`SeaweedDef.DragAlignDegreesPerSecond`); a snag nails the **leading `snags` tip** to the line, swings the body down-transport and sways it about the tip (`SnagByFrondTip`, `SnagSwayDegrees`); the engine reach is `BuoySnagRadiusMeters` (data, per bed). The runtime reads the anchors as sprite-frame metres — cell px over `scale_px_per_m`, y flipped — not the plane-metre `m` (which a test reconciles through `y_foreshorten`). NPC lines and lying-to hulls reach the drift through Core `SnagTargets`.
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

## The character rig kit, pass 6 (imported 2026-08-02 · **rev 6.2 imported 2026-08-06** · **rev 6.6 imported 2026-08-26** · **rev 6.9 imported 2026-09-02**)

One procedural person: eight facings, **eighteen** animations, four carry stances, and the axes that
make her somebody in particular. This drop replaces the pass-1 body (`characterIsoRig.js`) and splits
the head and the eyes into rigs of their own.

> **Rev 6.2 (2026-08-06) is append-only over the 6.0 body this repo already carried.** Four clips
> arrived — `board`, `boardDown`, `haul` (rev 6.1) and `ladderDown` (rev 6.2) — plus `boardMount()`,
> `haulGrip()`, `ladderMount()` and the new `'ladder'` mount kind. **The cell, the pivot, the camera,
> the ten presets and all fourteen earlier clips are untouched and re-bake byte-identical**, which is
> the drop's own claim and the reason this was a rig swap and not a re-derivation. `presets.json` is
> unchanged; `options.json` grew only the four new `anims` entries. See *The three clip families*
> below.

> **Rev 6.9 (2026-09-02) is a FACE pass, and it moves every sheet in the family.** Three files changed
> — `characterIsoRig6.js` 6.6 → 6.9, `headIsoRig3.js` (head 3.1 → 3.3, +254/−63 lines) and
> `eyeIsoRig.js` (eye 5.1 → 5.2). The five prop rigs and `characterIsoRig6.hands.js` in the drop are
> **byte-identical** to ours, and `presets.json` / `options.json` are semantically identical (they
> differ only in whitespace), so nothing else landed. The contract the baker reads is untouched:
> cell 64 × 92, pivot, `DIRS`, all 29 `ANIMS`, `CAST`, `CARRIES` and `REACH_LIFT` compare IDENTICAL.
>
> **Measured, and it is why the whole family was re-baked rather than the drop's sheets imported:**
> every one of 100 (preset × anim) cells differs between 6.6 and 6.9. The delta is confined to the
> HEAD — hairline, skull raster and eyes — at 9–21 % of a cell's opaque pixels; the body, the clothes
> and the ground shadow are untouched. Importing part of the family would have given a character one
> face standing and another face hauling.
>
> **THE DROP'S SHEETS ARE AT THE WRONG CELL FOR EVERY LOCOMOTION STATE.** He bakes all 73 at the
> OFF-DECK 64 × 88; the engine's locomotion and deck-work lane is 64 × 92 and only swim / tread /
> sleep / drive ship at 88 (`CharacterRigBakeMenu.PlayerAnimsBakedElsewhere` explains why the baker
> cannot make those four). So the intake split: the **forty off-deck sheets were imported from him
> verbatim**, and everything the baker owns was **re-baked in-engine**. The two were then checked
> against each other — our 92-row bake cropped 2 rows top and 2 bottom per cell against his 88 —
> and **33 of 33 comparable sheets are byte-identical**, which is what says our load order, preset
> resolution and camera are his.

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

### Animations — eighteen, and the mount contract is in the rig

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
| **`board`** | 10 | 90 | free · one-shot · reads `railZ` | | **`haul`** | 8 | 120 | free · **loops** |
| **`boardDown`** | 6 | 95 | free · one-shot · reads `railZ` | | **`ladderDown`** | 10 | 110 | **`ladder`** · loops |

`C.GROUPS` bundles them: `base` = idle/walk/run · `balance` = balance/stagger · `fishing` = the eight
rod states · `boarding` = board/boardDown/ladderDown · `work` = dig/haul. The four carry stances
(`C.CARRIES`: `buckets` · `tray` · `helm` · `oars`) ride the **free** anims only — a tool anim always
wins and `carry` is ignored. Rev 6.1 added `board` / `boardDown` to the `buckets` and `tray` lists.

**`'ladder'` is a third mount kind**, neither free nor a tool: both hands are committed to the rungs,
so no carry stance rides `ladderDown` and no prop layer mounts on it. The baker needs no special case
— everything that asks "is this free?" already refuses carry on it.

### The three clip families (rev 6.1 / 6.2)

These are **events**, not gaits: a gait is picked from measured speed and a stance from context, but a
clip is *started*, runs on its own clock and ends. In engine that is `CharacterClipPlayer`
(`HiddenHarbours.Core`), which takes the renderer through the same counted `Suspend`/`Release` claim
`PlayerHaulAnimator` uses and reads every fact off `CharacterVisualDef` — no sheet paths in code.

Each family exposes a **pin call** the baker writes into the sidecar as `clipPins`, tagged with
`clipPinSource`. Read them; never re-derive a contact point.

| family | clips | pin call | what the pins carry |
|---|---|---|---|
| **boarding** | `board`, `boardDown` | `boardMount()` | `rail` (where the plant hand meets the rail), `landing` (the cell-px vector the LAST frame re-seats the sprite by), `phase`, `rise`, `clamped` |
| **haul** | `haul` | `haulGrip()` | `handL`/`handR` (the two rope grips), `mid` (where a rope FX starts), `out` (unit direction), `tension` (0–1 heave envelope) |
| **ladder** | `ladderDown` | `ladderMount()` | `rungL`/`rungR` (soles projected to rung level), `standoff` (0.275 m off the ladder plane), `descend`, `stepZ` (0.60 m per loop), `ladderBehind` (draw order) |

Three things about them that are easy to get wrong:

- **`board` re-solves per `railZ`, but a sheet does not.** The shipped bake uses the rig's default
  **0.55 m** (a dory sheer). A hull with a different sheer wants its own sheet — the kit's own rule is
  "bake one sheet per rail height you ship". Above the clamp (drop > 1.2 m) the answer is
  `ladderDown`, not a taller `board`.
- **`ladderDown` is locomotion, not a transition.** The ground does not change under the figure; the
  cell plays in place like `walk` and the engine translates the sprite down the ladder. Drive that
  translation off `ladderMount().descend` — a *stair*, not a ramp — and the soles sit still on real
  rungs. A constant 0.55 m/s creeps up to a third of a rung, three visible pixels at 32 px/m.
- **`haul` replaces the legacy `PlayerHaul.png`** (`fisherRig.js` → `FisherHaul`, c0–c5 + strain +
  ease): hand-pixelled side profile, ONE facing, 32 × 64 at pivot (16, 64) — a different cell and a
  different contract, unusable at seven of the eight headings. The old sheet is still what
  `PlayerHaulAnimator` draws; swapping that presenter over to this clip is its own change.

**Known open, from the kit:** there is no `ladderUp` and it is not this clip reversed (going up the
arms pull and the hips stay in). The turn-around at the top of a ladder and the step off at the bottom
onto a moving gunwale are not authored — an engine covers both with `board`/`boardDown` or a hard cut,
and the turn-around is the one players will notice.

### The reach family (rev 6.6, imported 2026-08-26)

**One clip, `reach`, baked at three rest heights** — `ground` · `stowV` · `stowH`. Not three
animations: the rig re-solves the same descent per height, which is why a floor set-down crouches and
a rack one is all arm. `ANIM_MOUNT.reach` is a fourth mount kind, **`'rest'`**: a tool mount that does
NOT own the tool. The set-down bake owns the prop's pose; `reachMount(dir,opts)` says where the HAND
is on it each frame, and `tool()`'s pitch/yaw are advisory on this clip alone.

| | | |
|---|--:|---|
| frames · ms | 6 · 100 | one-shot |
| frame → `u` | **`u = f/(frames-1)`** | ⚠️ every other clip is cyclic `f/frames` — see below |
| tool is HOME | `REACH_ARRIVE` **0.62** | |
| hand OPENS | `RELEASE_AT` **0.72** | gripped on frames 0–3, empty on 4–5 |
| grip rise | `GRIP_RISE` **0.095 m** | the grip centre above the rest surface |
| rest surfaces | `REACH_LIFT` = 0 / 0.95 / 1.05 m | **world metres, not scaled by build** (the `workZ` precedent) |

Four things about it that are easy to get wrong:

- **`settle` is a frame→`u` mapping, and `reach` is the only clip that carries it.** `u = f/(frames-1)`
  puts the LAST frame on `u = 1` — the settled rest — instead of one step short of it. A consumer that
  reads it cyclically never draws the settled frame, so the character finishes reaching for something
  they never put down. In engine that is `CharacterVisualDef.ClipSettles`.
- **The seam is not the release.** The tool arrives at 0.62 and the hand opens at 0.72, so the release
  is something you can watch. Releasing at the seam is exactly the defect the rod kit's continuity law
  was written for — the old rests were single cells with their own yaw and pivot meaning, and the rod
  jumped 2.3–3.9 px the instant it left the hand.
- **A pick-up is these frames REVERSED.** A 0.72 release mirrors to a 0.28 grip-close: the hand
  arrives empty, closes, and lifts. There is no `reachUp` and there should not be — a second family
  would double the art and let the two drift.
- **⚠️ `stowV` 0.95 m and `stowH` 1.05 m are DECLARED PLACEHOLDERS.** They are the art lane's reading
  of "rack height, roughly standing reach", not a measurement of any furniture in the game, and
  **`RodIso.restLift()` is not their oracle**: the rod rig's number is how far a settled rod holds its
  GRIP above whatever it rests on (0.16 / 0.62 m), and the rod rig has no way to know how high a rack
  is. Whoever builds the rack owns these two. Where the kits genuinely meet is the ground —
  `restLift('ground')` for a reeled rod is 0.095 m, the same number as `GRIP_RISE` — and
  `tools/rig-recipes/reach-continuity.mjs` asserts that one and reports the rest.

Small builds cannot reach the high rack: the rig **clamps** the lift to what the figure can touch and
reports `clamped:true` rather than stretching the arm. Both children clamp at both racks and one small
adult at the high one; a consumer that places a tool at the requested height for a clamped build hangs
it above an empty hand. The engine side is `CharacterReachDef`, imported from `Reach_sidecar.json`.

The drop also completed **`Ginny_run` and `Skipper_run`** — the two cast presets the harbour actually
sends anywhere. The rest of the cast still bake idle + walk only.

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

---

## The grass library (2026-08-05) — one drop, one authored rig, one manifest

Two files, and they are **not the same kind of file**:

| file | who owns it | edit? |
| --- | --- | --- |
| `grassSpeciesRig.js` | the art director — imported **verbatim** | ❌ never |
| `grassRig.js` | **art-pipeline, authored in-repo by PR** | ✅ yes |

`grassRig.js` is the exception this README's opening warning does not cover: the tree / shrub /
flower precedent, an in-repo rig that **composes on** a drop instead of copying it. It requires
`grassSpeciesRig.js` to be loaded FIRST and throws if it isn't, so the load order is enforced at the
far end rather than trusted. Everything it draws goes through the drop's renderer on the drop's
`grass` species — hue +0°, saturation ×1.00, i.e. the base ramp verbatim — which is what makes
"every variant stays on the ramp" true by construction instead of by review.

    grassSpeciesRig.js   10 sprites, 5 species   cattail · soft rush · tussock sedge ·
                                                 saltmeadow hay · timothy
    grassRig.js          16 sprites, 6 habitats  meadow short/mid/tall · wide clump · fringe ·
                                                 dune marram · dry headland

**Neither rig has an azimuth term.** Grass has no heading, no facings and no sway frames — the
sprites are STATIC and `HiddenHarboursGrass.shader` does all the animation in the vertex stage. So
none of the compass machinery above applies, and there is no probe to run.

### ⚠️ The contract these sprites live under is the SHADER's, and it is unusual

The grass shader bends blade tips weighted by the sprite's own **`UV.y²`** — 0 at the canvas bottom
edge, 1 at the top. Four consequences, all asserted at bake time (`GrassLibraryBaker`) and again on
the committed pixels (`GrassLibraryContractTests`):

1. **Root on the bottom edge.** A tuft that starts a row up shears off the ground in wind.
2. **`climb` is the sway dial.** How far a variant's blades rise up its canvas *is* how much bend it
   takes — so a "tall" sprite that only climbs 12 px would stand dead still in a gale. Climb is
   therefore **measured off the bake**, never declared, and it is what files a variant into the
   paint tool's Short / Medium / Tall mix.
3. **Nothing detached.** Every lit pixel 8-connects down to the bottom edge, or it flies away alone.
4. **The exact ramp** — `#283a22 #3a542a #567834 #7ca248 #aac660`, and each species ramp is that one
   with the hue rotated and the saturation scaled at *identical lightness*. The runtime lush→straw
   knob MULTIPLIES a tint over it, so an off-ramp pixel reads fine until the owner tints a field and
   then becomes a colour island. This is the failure a human reviewer cannot catch, which is why it
   is a test.

Hard alpha, PPU 32, bottom-centre pivot (alignment 7 — a centre pivot buries every tuft half its own
height), widths in multiples of 32.

### One PNG per variant — deliberately not a sheet

Every sibling kit here bakes an atlas and then slices it, and every one has been bitten by the same
trap: a fresh Multiple-mode import has EMPTY rects, so a sheet committed before its slicer ran looks
imported and yields no sprites. Grass has **no axis to put on a sheet** — no facings, no sway frames,
no tide states — so an atlas buys nothing here except that trap. Single-mode PNGs also match what the
three shipped #102 tufts already are. Batching is not lost: every tuft draws on the one shared
`Grass.mat`, and a Sprite Atlas can be laid over the folder later.

### The manifest is the product

`Assets/_Project/Art/Sprites/Grass/GrassLibrary.json` is emitted by the **same rig call** that
renders the pixels (`GrassRig.manifest()`), so a variant's size, climb, height class and habitat tags
cannot drift from its own art. It covers all three sources in one list — the three shipped tufts
(declared, never re-baked, still at their original paths), the habitat set, and the species drop —
which is what let `GrassPaintTool` drop its three hard-coded sprite paths.

Bake: **Hidden Harbours ▸ Dev ▸ Bake Grass Library** (or `GrassLibraryBakeMenu.BakeFromCommandLine`).
Kit README: [`../grass-species-kit/README.md`](../grass-species-kit/README.md).

---

## The ISO rig pack (imported 2026-08-06) — wharf kit · wharf decor · utility · shoreline finds

An owner drop of **four independent rigs** under [`iso-rig-pack/`](iso-rig-pack/), landed verbatim
with the pack README, four per-rig READMEs, three `*.catalogue.json` sidecars and the wharf kit's
`gameplay/wharfIsoRig.gameplay.json` + `harness.html`.

| folder | global | what it is |
|---|---|---|
| `wharf-kit-iso/` | `WharfIso` | 7 wharf structure families, 17 presets, tide model, gameplay contract |
| `wharf-decor-iso/` | `WharfDecor` | 61 pieces of wharf gear and dressing, 7 categories |
| `utility-iso/` | `UtilityIso` | 42 village services (power/light/water/sewer/fuel/telecom), 6 categories |
| `shoreline-finds-iso/` | `ShoreFinds` | 36 beachcombing finds from 19 forms, 3 states |

**Conventions are PER FAMILY and every number below is measured, not read.** The measured contract
for each family is committed beside where its sheets will bake:
`Art/Sprites/Wharf/Iso/`, `Art/Sprites/Wharf/Decor/`, `Art/Sprites/Utility/`,
`Art/Sprites/Shore/FindsIso/` — the same "the committed contract is the oracle, and the baker
refuses rather than rewrites" arrangement the shore-plant and shrub kits use.

| | wharf kit | wharf decor | utility | shoreline finds |
|---|---|---|---|---|
| cell | tight, `px,py` per bake (fractional) | fixed 420×520 | fixed 440×620 | tight, `cellOf(key)` |
| pivot | model origin = footprint centre at chart datum | ground centre (210,420) | ground centre (220,520) | `sit`, ground-contact centre |
| load | `InstallModule` (no `W/H/pivot`) | `Install` | `Install` | `InstallModule` (no pivot) |
| azimuth | CCW, measured | CCW, measured | CCW, measured | none — flat lie angle |
| ground squash | 0.6428 | 0.6428 | 0.6428 | **0.72** |
| keyline | `#1a1c22` | `#1a1c22` | `#1a1c22` | `#231d14` (warm) |
| import cap | **4096** | 2048 | 2048 | 2048 |

### The five things that will bite the bake

1. **`ShoreFinds.DIRS` is a string array, not a number**, and the rig declares no `pivot`. Fed to
   `RigCatalog.Install` it throws on the pivot, and the `typeof DIRS === 'number'` probe reports 0
   facings rather than 8. Use `InstallModule` and `cellOf(key)`.
2. **Its ground foreshorten is 0.72, not 0.6428.** Carrying the structure rigs' constant across is
   the un-squash error this repo keeps repeating. Measured per family, always.
3. **The union cell is much larger than the biggest per-facing cell** wherever the model origin's
   projection swings with the facing. `floatSet` / `plasticSet` (a float *plus* its gangway) measure
   **757×592** unioned against **478×423** as a naive per-facing max — a 1.6× miss that is exactly
   what a cap computed from the wrong number would hide. This is why the wharf kit's cap is 4096 and
   everything else's is 2048.
4. **`wharfDecor`'s `fireCabinet` pivot lands 5 px BELOW its own ink.** It is wall-hung, so nothing
   is drawn down at deck level, and a crop that is merely "tight to ink" puts the piece's ground
   contact outside its own cell. The committed cells are therefore pivot-**inclusive** unions, and
   the contract carries `pivotInsideInk` so a baker can assert it rather than discover it.
5. **The wharf cell is parametric, so a fixed cap is not a fixed guarantee.** At the rig's defaults
   every preset fits; at `bays: 8`, or at a Fundy `tideRange: 14`, the same preset packs past 2048.
   Assert native resolution from the *rendered* cells at bake time — never from a table.

### What was verified, and what the pack claims

The pack README's **"No order dependency, no shared state, no globals beyond the four rig objects"
is TRUE, and was measured rather than believed** — 83 probe keys, including the full RGBA buffers of
24 representative renders across all four rigs, are byte-identical whether each rig loads alone, in
the README's order, reversed, or shuffled. That claim is checked here because the shop kit's README
made the same one and was wrong three ways (#437).

**All four now carry a `KEYLINE_DEFAULT` gate, default FALSE — the ring is retired (ADR 0031).**
They *arrived* in the pre-ADR-0031 style, which ADR 0031 §4 explicitly permits ("sheets migrate
naturally … a mixed period is expected and accepted"); this is those four families being redone, in
the shape `shorePlantRig` / `shrubIsoRig` already use. Four touch points per rig: the
`const KEYLINE_DEFAULT = false` beside the keyline colour · the ring pass wrapped in
`o.outline === undefined ? KEYLINE_DEFAULT : o.outline !== false` · `KEYLINE_DEFAULT` in the rig's
exports · `keylineDefault: false` in the rig's `*.contract.json`. `wharfDecorRig` and
`utilityIsoRig` needed one extra line each — their `resolve()` returns an explicit whitelist, so
`outline` is threaded through it unnormalised; `shoreFindsRig`'s ring is **two-tone** (the lit side
is `mix(KEYLINE,'#6b6045',0.30)`, not `KEYLINE` flat), so the gate wraps the whole no-material
branch and the `KEYLINE` constant stays live and exported.

**The colours are NOT deleted, and the A/B arm is proven.** The pack states its two keyline colours
are deliberate; they stay exported and stay in each contract's `keyline`, so the archived sheets
remain describable. Measured in a standalone V8 host over **49 subjects** (7 wharf families, 3 decor
props, 3 utility props, all 36 finds): `{outline:true}` reproduces the pre-gate render **byte for
byte** on every one, `{outline:false}` is byte-identical to the new default, and switching the ring
off is a **pure ring deletion** — 0 painted pixels changed on any subject, because every pixel the
pass writes is an empty neighbour of the silhouette. The ring was not free: **25.6 %** of every ink
pixel on a `powerPole` and 15.2 % on a clam, the perimeter-cost law from
`../outline-interaction-language.md` landing on filamentary subjects.

The gate changes what the *rigs* draw; the shipped sheets still carry the ring until the owner
re-runs the bake. That is ADR 0031 §4's mixed period, working as intended.
