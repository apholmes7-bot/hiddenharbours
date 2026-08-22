# `hiddenharbours.scene/1` — the contract

**What this is.** The scene editor's package format. It began as a reconstruction from
[`scene-editor-review.md`](scene-editor-review.md) (#571, corrected by #576) with seventeen
fields the review named but never specified; **lead-architect settled all seventeen on PR #588**
from the editor's own reference package. That package's `sample-scene.json` is now committed at
[`reference/sample-scene.json`](reference/sample-scene.json), so every row of §2 is checkable
against bytes in this repo rather than quoted second-hand — and the exporter's tests compare
their output to it block for block. The exporter at `tools/scene-export/` implements exactly
this.

**Lane:** tools-editor. **Direction:** outbound only. Import stays gated (review §9).

---

## 0. The two rulings that shape everything else

**Unknown keys MUST be ignored, and `x-` is the reserved extension prefix.** Nothing can refuse
them today because nothing *reads* a package: the standalone editor ships `doExport()` and
`copyExport()` and has no import branch at all — no `FileReader`, no file input, no path from
parsed JSON into editor state. The format is outbound-only on both ends. The eventual repo-side
importer is ours to write, and this is its first rule.

**`call` / `opts` are write-only.** The editor renders from its own live state
(`RigKit.rigNow(...)`) and `buildExport()` writes the `call` record *out of* that state; no
renderer reads one back. An export without them is valid. The cost, and it is a real one: a
document with no `call`/`opts` can never seed a round-trip **into** the editor, whose internal
model is family + dir + opts.

## 1. The envelope

```
schema        'hiddenharbours.scene/1'          ← the key is `schema`, not `format`
generatedBy   string
generatedAt   ISO-8601
region        { id, sceneName, worldCenter, worldSizeMeters }   ← id in def form
frame         { ppu, cellMeters, originNW, axes, camera, sort }
terrain       { cols, rows, note, legend, layers }
entities      [ … ]
cliffLines    [ … ]
paths         [ … ]
collision     { note, sidecars }                ← derived, never authored
stats         { entities, tiles: { ground, road, cliff } }
```

## 2. The seventeen, settled

| # | Question | Ruling, from the reference package |
|---|---|---|
| 1 | Are unknown keys tolerated? | **Yes — nothing reads a package at all.** Keep `x-`; it is the reserved extension prefix. See §0. |
| 2 | Does an entity render without `call`/`opts`? | **Yes.** `call` is written out of live state, never read back. See §0. |
| 3 | Where does an entity say it is? | **`pos: [x, y]`, metres, origin = region centre.** `frame.axes` verbatim: `'+x east, +y north, origin = region centre'`. |
| 4 | RLE encoding | **Pairs**: `[[0, 5492], ["g1", 56], [0, 104], …]`. |
| 5 | Legend | **One top-level `terrain.legend`**, prefixed string keys (`g1`–`g6`, `r1`–`r11`, `c1`–`c3`), each value `{layer, rig, rigSource, material}` — and those keys **are** the RLE values. Layer objects carry **no** legend. The numeric convention exists only in `cliff.pieces.legend` (`"1"`–`"12"` → piece names), whose RLE values are numbers. *(The review's per-layer-legend claim was wrong.)* |
| 6 | Row order | **Row 0 = north edge**, per `terrain.note`. |
| 7 | Termination, and what `0` means | `0` is a real RLE value meaning **no tile (water — never baked, the shader owns it)**, and it counts toward full coverage. The sample's first ground run is `[0, 5492]`. |
| 8 | `layers` container | **Map keyed by layer name.** |
| 9 | `paths[]` shape | `{id, layer, material, widthMeters, closed, curve: {kind: 'catmull-rom', uniform, tangentScale}, nodes: [[x,y]… metres], polyline: [[x,y]… derived]}`. Sibling top-level `cliffLines[]` is the same shape plus `landSide`, `corners`, `tiles`. |
| 10 | `family` vocabulary | The editor's `RigKit.byId` ids — `dory`, `punt`, `lobsterboat`, `capeislander`, `sportskiff`, `console`, `camper`, `character`, `pot`, `rock`, … No reader exists to refuse an unknown one. |
| 11 | Is `cell` required, and does it need `pivot`? | **Nullable**, by the editor's own hand: `cell: b && b.ok ? {w, h, pivot: [px, py], pxPerM, unityPivot} : null`. Both pivot forms ship together; nothing reads them back. |
| 12 | `sortBias` units | A per-entity **tie-break delta** (`sortBias: e.bias \|\| 0`). `frame.sort`: *"painter, descending world y (north draws first); sortBias breaks ties"*. **It is not `YSortSprite._baseOrder`'s absolute order** — the review's mapping row overstated it. |
| 13 | `footprint` | **Optional**, present only when the family has a footprint fn; the value is the rig's gameplay measurement object (the rock: `{footprint: {rx, ry, ground}, perch, snags, hazard, pool, weedLine, pivot}`). |
| 14 | Top-level envelope | The key is **`schema`**. Full top level in §1. |
| 15 | Container | A bare **`<sceneName>.scene.json`** download. The zip was the review package's wrapper, not the format. No open dialog exists. |
| 16 | `gameplaySidecar` | Per-entity string, from a hardcoded 7-family map (six boats + camper); non-vehicle entities omit it. Plus the top-level `collision.sidecars` array. |
| 17 | `stats` shape | `{entities: N, tiles: {ground, road, cliff}}` — tiles keyed by layer, **stamp counts**, not a checksum. |

## 3. What this export ships, and what it cannot

Honoured exactly: the envelope and every key name above · region frame from the `RegionDef`
(never a hardcoded size) · `sum(runs) == cols × rows` on every layer · row 0 north · `0` as the
reserved no-tile value · one top-level legend · both pivot forms · `sortBias` as a delta from
`SortingBands.DecorBase`, read from the C# rather than hardcoded · rig pinning by LF sha256.

Deliberately absent, each for a stated reason:

- **`call` / `opts`** — §0. Reconstructing an invocation from a baked sheet would be a second
  definition of the bake.
- **`cliffLines`** — empty. Cliff lines are an editor authoring artefact; the repo's cliffs are
  placed `CliffWallSurface` components and ship as entities.
- **`footprint`, `gameplaySidecar`** — both are rig gameplay measurements, and no rig is
  executed here (rigs are hashed, not evaluated).
- **`opts` inside `call`** — see §7.
- **A guessed rig** — a sheet with no sidecar the exporter will trust resolves to `null` and is
  listed by name under `x-provenance`.

See §7 for what the terrain layers now carry. One field is honest-but-mismatched, and every
entity says so: **`family` is the sprite name's
stem, not a `RigKit` id** (§2 #10), because a baked sheet does not record which palette family
drew it. Each entity carries `x-familyIsSpriteStem: true`.

`generatedAt` is the committer date of the newest **input** commit, never the wall clock — a
run timestamp would make the output non-reproducible and the `--check` gate meaningless.

## 4. Ours, and marked as ours

Any key beginning `x-` is this exporter's, not the contract's — permitted by §0 and used for
what the repo can state and the format does not name: `x-provenance` (source commit, the commit
the scene was last banked at, builder drift since, what was read from where, the
unresolved-sheet list), `x-rigs` and `x-rigSha256` (the pin table the review asks for but names
no key for), `x-cellAt` / `x-inBounds`, `x-name` / `x-path` (the scene hierarchy path),
`x-pivotSource`, `x-declaredBy` (which sidecar linked a sheet to its rig), `x-readOnly` /
`x-derived` / `x-authorable`, `x-heightMap`, `x-familyIsSpriteStem`.

## 5. What reading the bytes added

The reference landed in `66f03140` and reading it directly corrected one thing the relay had not
covered and confirmed everything else:

- **`cellMeters` and `originNW` belong to `terrain`, not `frame`.** The ruling gave the top-level
  envelope and `frame.axes`, so the two were plausible in either block and the exporter had them
  in the wrong one. `frame` is `{units, scale_px_per_m, axes, camera, sort, pivots}` — note
  `scale_px_per_m`, not `ppu`.
- **Entities carry `group` and `flipX`**, and the cliff layer alone carries a `pieces` block
  (`{note, legend, rle}`, numeric keys, covering the grid). `paths[]` ends with `tiles`, a stamp
  count.
- **The reference itself violates the coverage rule.** Its road RLE sums to 18,879 of 19,200 —
  exactly the defect review §8.2 reported. So `sum(runs) == cols × rows` is a requirement *on
  us*, not a description of the sample; this exporter satisfies it and a test enforces it.

Four entity fields the format names were absent here for stated reasons: `call` and `opts` (§0),
`facing`/`facingIndex` (the editor's own view state), and `gameplaySidecar` (a rig gameplay
measurement — no rig is executed by this exporter). **Three of the four have since landed** and
the reasons above are kept as the record of why each waited: `facingIndex` with the write-back
contract's §8.1 ask, `call`/`opts` with #629's ledger and the kit contracts (§6.4, §6.4.1), and
`facing` on 2026-08-22 (§6.2.1). Only `gameplaySidecar` is still absent, and for the reason
given: it is a measurement no baked sprite records.

## 6. Portability

The output is a pure function of the repo, and that has to hold on any machine, not just the one
that wrote it. A second run on a Windows full-LFS checkout found three ways it did not:

- **The height-map hash.** A Git LFS pointer's `oid sha256` **is** the sha256 of the content, so
  a pointer checkout and a full checkout agree — but only if both report it under one key. The
  document carries `textureSha256` from whichever is on disk, and no longer says which; that was
  a fact about the machine, not about the harbour.
- **Subprocess decoding.** `text=True` alone decodes in the platform locale, and every em-dash in
  a commit subject came back mojibake'd through cp1252. The git calls pass `encoding="utf-8"`.
- **Line endings.** A checkout with autocrlf rewrites the committed packages, and `--check` was
  comparing raw bytes. It now reads with universal newlines — what it means is "does this commit
  still produce this document", not "is your working tree LF" — and `.gitattributes` pins the
  packages to LF as well.

A fourth is not a bug but a boundary, and it needs stating plainly: **determinism is per-commit
*and* per-LFS-state.** The seabed textures are Git LFS objects, so the same commit exports a
contoured ground where their bytes are present and an empty one where they are pointers (§7.3).
Both are correct for what they could read. Two consequences:

- Every package stamps `x-provenance.heightMap.textureBytesRead` (and the same flag under
  `terrain.x-heightMap`), so which of the two you are holding is a fact in the file rather than
  something to infer from whether the ground looks empty.
- `--check` names that cause instead of printing a bare `STALE`, which would otherwise send a
  reader hunting for a scene re-bank that never happened.

This is not hypothetical for CI: `.github/workflows/ci.yml` runs an unscoped `git lfs pull`, so
**a `--check` gate wired into that job would fail against packages generated without the bytes.**
Closing it properly means regenerating the packages in an LFS-present checkout — which would also
ship the real coast rather than an empty layer, and is the better fix of the two.


### 6.1 A pointer-only re-export must not delete a coastline

Once a package has been exported on a machine holding the Git LFS objects, it carries a ground
contour and a height field that **no pointer-only checkout can rebuild**. The standing routine on
this lane is "a builder commit lands → merge, re-export, push", and run unguarded in a
pointer-only container that routine quietly empties the layer and reports success.

The pointer itself is the fix. An LFS pointer's `oid sha256` **is** the sha256 of the object it
stands for, so a checkout with no bytes can still prove *which* bytes the committed contour was
built from. Three outcomes, in order:

| Committed package | This checkout | Result |
|---|---|---|
| holds a contour, same `textureSha256` | cannot read the bytes | **carried forward**, stamped `heightCarriedForward: true` |
| holds a contour, **different** `textureSha256` | cannot read the bytes | **refused** — the contour is genuinely stale and only an LFS-present run can fix it |
| anything | can read the bytes | recomputed normally, `textureBytesRead: true` |

Carrying is never silently equated with reading: the flags are separate, and a carried package
says so. A package that was itself carried forward is a valid source for the next carry — it
holds the same contour pinned to the same hash. (Requiring `textureBytesRead` on the *source*
was a real bug: the guard fired exactly once, and the next pointer-only run read its own output,
judged it no richer, and emptied the coast.)

Refusal is all-or-nothing. A `MANIFEST.json` naming sha256s of packages we declined to write
would be a third state, worse than either honest one.

⚠ **The third row had no test, and its absence cost three.** Two rows of that table were pinned;
*"anything | can read the bytes | recomputed normally"* was not. The three carry-forward tests
each banked a committed package and asked `_carry_forward_height` to carry it, without ever
building the precondition that **this run could not read the bytes** — so they passed on a
pointer-only checkout, and on a full-LFS one the same correct code returned early, exactly as
this table says it must, and failed them. Neither run was evidence about the rule: one was a
statement about the tester's `git lfs` state. The fixture builds the pointer-only state itself
now (bytes unread, ground emptied, same `textureSha256` — the pointer's `oid` being that hash is
the whole proof), so all three pin the contract on either machine, and the third row is asserted
in its own test so the next reader cannot make them green by deleting the early return.


### 6.2 Orientation: the index is a fact, the bearing is not

`scene-writeback-contract.md` §8.1 asked for an explicit facing field, since the index was only
encoded in `x-sprite.name`'s `_d<n>` and *"a parsed suffix is a convention two programs have to
keep agreeing about, and a field is not"*. Three things decided the shape, and two came from the
reference bytes rather than from the ask:

- **`facing` and `facingIndex` are different types.** The reference package carries both:
  `facing` is a compass name (`"S"`, `"SE"`) and `facingIndex` is the baked step (`3`, `4`).
  Putting the integer in `facing` would be a confidently wrong value in a field a reader expects
  to hold a string, so the index goes only where the index belongs, in the reference's own slot
  between `pos` and `flipX`.
- **`facing` shipped null for two rounds, and is derived as of 2026-08-22.** The reason for the
  delay stands as written: deriving a bearing from a step is the one piece of arithmetic this
  repo has already got wrong, and the inverted form put the schoolhouse door ~92° off the green
  it faces *with a green test*, because the test was the algebraic inverse of the implementation.
  Shipping `null` was the right answer while the only thing on offer was another go at the same
  sum. What closed it was not a better derivation but **four independent measurements that agree**
  — and a test whose oracle is none of them. See §6.2.1.
- **The count is read, never assumed.** §2 is emphatic, and `IsoPropSheetBaker` already fails a
  bake when a rig's measured `NativeDirs` disagrees with its contract's `Facings`. So `x-facings`
  appears only where a sidecar declares one, with `x-facingsSource` naming the file. The
  contract's classes then fall out of the data instead of a hardcoded list: baked buildings
  declare 8; foliage and shore plants declare none, because those rigs publish variants and
  seasons rather than directions; and the legacy single sprites declare none either — which is
  exactly the per-entity test §2 describes, rather than a family-name list living in code.

Nine character entities carry an index with no machine-readable count: §2 declares characters as
8, but in prose (*"8 at 45°, the ADR-0006 recipe"*) rather than in a field this reader can follow.
They are exported with the index and no count rather than a plausible one — **and therefore with
no `facing` either**, since a name derived from a count nobody declared is exactly the assumption
the rule above forbids. They are the only entities that still ship `facing: null` with an index.

### 6.2.1 The rule, the trap, and why the test does not touch either

**The rule.** Cell `i` depicts a ground bearing of `(360 / facings) · i`, clockwise from north.
`facing` is the compass name of that bearing and `x-facingBearingDeg` is the bearing itself, kept
beside it so the claim is checkable and so a count that does not land on a named point still says
which way the cell looks. A bearing between two names gets **no name** rather than the nearer one;
rounding it on would be the aliasing §6.6 refuses everywhere else.

Four sources say so, and they were checked against each other rather than stacked:

| Source | What it says | Kind of evidence |
|---|---|---|
| `RigBaker.DirForCell` | bakes CCW rigs as `render((N−k) % N)` and CW rigs as `render(k)`, summarising itself as *"⇒ Anything baked here is genuinely clockwise"* | the invariant the code exists to create |
| `Buildings.json` (+ every per-sheet village sidecar, and `Interiors.json`) | *"The correction is already applied to the sheet, so cell i genuinely depicts +45\*i"* | measured at bake time by `BuildingRigAzimuthProbe`, and committed |
| **ADR 0034** | all eight rows at `45°·d` of ground azimuth, row 1 = NE at **exactly 45.00°** | measured off the shipped pixels, against a byte-identical re-render |
| the reference package | `facingIndex 3 → "SE"`, `4 → "S"` | the editor's own worked pairs |

**The trap, stated because it is the one a reader will reach for.** `dir = (facings − i) mod
facings` is real — it is in `BuildingFacing`'s remarks and it is what the baker hands the rig —
but it describes **the argument, not the sheet**. Applying it to the exported index and naming a
compass point from the result gives `SW` where the reference says `SE`. Note where it *agrees*:
index 4 is a fixed point of that map, so `S` survives it, and `S` is exactly what a spot-check
reaches for. That is the same shape as the original defect — *"the store and the saltbox, whose
targets are near due south, happened to land right"*.

**The oracle is a fact about the world, not about the arithmetic.** `FacingTests` does not
re-derive a bearing. The village builder turns every door toward the village green, so the test
reads `VillageHearthPos` and `StartSpawnPos` out of `StPetersBuilder.cs`, takes their midpoint,
un-squashes the difference by `sin 40°` (ADR 0034's whole subject) and asks whether each exported
bearing is within a half-cell of it. Under this rule the four village buildings land at 4–20°.
Under the inverted reading the red saltbox lands **176°** away — facing out of the village it
stands in — and a companion test asserts that failure, because a check that cannot fail is not
evidence for either reading. Two further witnesses come from outside `hhexport` as well: the
reference package's `SE`, and `Interiors.json`'s `exteriorFacingOffset: 4`, which only stays a
half-turn if the step direction is right.

### 6.3 A road layer names every surface it will not solve

`read_ways` had always built a list of the surfaces it skips — the computed truck-park spur, the
per-lot town walks — and `_road_grid` **discarded it**, so no package ever carried one. That was
a claim made in this PR before it was true in the bytes; it is true now, as `layers.road.x-omitted`.

The gap it hid got larger with #626: the paved **rectangles** (`new Pad(…)` — the winch apron,
the buyers' gravel, the truck park and the new Route 91 forecourt) were not read at all. Every one
takes a computed area rather than a declared one, because each is derived from geometry authored
elsewhere, so none can be rasterised here without re-deriving somebody else's rectangle. They are
named instead. A road layer that silently omits four paved areas reads as a region that has none.


### 6.4 `call.opts` comes from the ledger, or says it does not

The recipe ledger (#629) puts a `<stem>.recipe.json` beside 298 baked sheets, recording the rig,
the call template, and the exact opts per variant axis. Reading it is **a lookup, not a
derivation** — the ledger's own §6 word — so nothing here re-implements a baker.

Three rules, each the same house style applied to a new file:

- **The sheet hash is a refusal, not a warning.** A recipe describes one bake; if the sheet on
  disk hashes differently, its axes describe cells that are not there, so no opts are taken from
  it and the reason is reported in `entityNotes.recipeRefusals`. The PNG is a Git LFS object, but
  a pointer's `oid sha256` **is** the content sha256, so the check holds in a pointer-only
  checkout exactly as in a full one — the same proof §6.1 uses for the seabed.
- **An unknown key is refused, not ignored.** The ledger's C# reader is strict because silently
  dropping a key is how a consumer draws the wrong variant while believing it read the whole
  file. A reader that is strict on one side of the fence and lax on the other gives that
  guarantee away.
- **The odometer is read, not guessed.** Axes run first-fastest (§2.3), so an axis's index is
  `(i // stride) % len(values)`. The cell index comes from the sliced sprite's own rect: Unity's
  rect origin is the texture's bottom-left while the bakers pack row 0 at the top, so the row is
  flipped — measured on `camper_clipper_rest_d0…_d7`, which occupy top-rows 0…7 in that order,
  not assumed.

**What it is worth today, stated plainly.** 44 Nine Mile Creek entities are covered and 0 at
St Peters. Every one is the `iso-prop` kit, whose recipes declare a single `dir` axis and an empty
base `opts` — so their opts are still `{}`, but now as a **recorded fact** rather than an
admission of ignorance, and the call finally carries the piece literal and the resolved direction
(`render("trapStack", 4, {})`) where before it carried neither. The 171 recipes that hold real
option content belong to kits — fuel storage, gas station, nav buoy, camper, yard — that landed
*after* these scenes were banked, so none is reachable from the committed packages yet. This
grows on the next re-bank without a line of tool change.

Entities the ledger does not cover keep the empty-opts form, and its note now says *why* for that
entity — no recipe beside that sheet — rather than the older blanket claim that the axes are
recorded nowhere, which the ledger has made false for six kits.


### 6.4.1 The kit contracts are the second lookup — and the reason the number above moved

The paragraph above ends *"this grows on the next re-bank without a line of tool change"*. It
grew without one, and not the way that sentence expected: the re-bank landed the yard's opts as
predicted, but the larger gap turned out to be a **discovery** bug rather than a coverage one.

Every baker already commits a kit contract beside its sheets — `Buildings.json`,
`Interiors.json`, `yardIso.contract.json`, `shops.contract.json`,
`shopFixtures.contract.json` — and each carries the verbatim `optionsJs` of the bake, per entry.
That is the same class of committed declaration §6.4 reads the ledger as, one level coarser: a
recipe knows which **cell**, a contract knows the **bake**. Where both exist the recipe wins, and
`ContractOptsTests` pins it on the one sheet that has both — the yard's postRail, whose recipe
says `kept: 0.72` for this cell where the contract says `0.88` for the sheet.

Every entity carrying contract opts says what they describe, in `call.x-optsScope`: *"the whole
SHEET, not this cell"*. A per-sheet value read as a per-cell one is a wrong variant drawn with
confidence, which is the failure mode §6.4 exists to prevent, so it is stated in band rather than
left for a reader to infer from which key the provenance is under.

**Two refusals carry over unchanged, and one is new.** Matching is on the **sheet an entry
names**, never on a key resembling a sprite stem — `Buildings.json` holds nine entries keyed
`school`, `generalStore`, `redSaltbox`, and a key match would hand one building another's siding
on a near miss. `optionsJs` is JavaScript, so it is parsed by a deliberately small literal
grammar and **refused whole** when that grammar does not consume all of it: the rigs resolve an
unknown key as a *silent fallback* (§6.4), so a half-read opts dict draws a confidently wrong
object with nothing to flag it. The new one is what to do with a refusal that is not a defect —
`shops.contract.json` and the four `wharfBuilding` outbuildings declare
`Object.assign({}, Shopfront.PRESETS['harbourStore'])`, a **call into the rig's own preset
table**. It cannot be resolved without running the rig, so `opts` stays empty and the expression
travels verbatim as `call.x-optsExpression` for a reader that can. Taking `harbourStore` as an
opt would invent an option no rig reads.

**The discovery bug, which is worth more than the count.** `sidecars_for_sheet` had four rules
for which committed file speaks for a sheet, and the last — *a folder's lone sidecar that is not
itself a per-sheet file* — counted the folder's **files** rather than its **index candidates**.
That is true of `Trees/`, which publishes nothing else, and false of `Buildings/Village/`, where
`Buildings.json` sits beside eight per-sheet sidecars. So the village kit's contract was
invisible: its eight buildings resolved no rig at all (their per-sheet sidecars name
`"rig": "house"`, a kit KEY that is not a path), carried no `call`, and stood in the package under
the sprite-stem family `Village`, which the editor cannot draw — while the contract two lines
away declared `rigScript`, `rigGlobal` and the exact `optionsJs` of every one.

The rule now counts JSONs with no PNG of their own name; exactly one of those is the folder's
index. The guard it must not cost is intact and tested: `Art/Boats/` holds four anchor files and
no index, four is not one, and a dory still resolves to nothing there rather than to whichever
hull sorts first. **Measured across both packages: eight sheets change, all of them in
`Village/`.**

**What the entry buys that the folder cannot.** `rig_for_sheet` stays honestly ambiguous over
`Village/` — `Buildings.json` declares `houseIsoRig` and `wharfBuildingRig` side by side and the
folder cannot choose between them, which is §6.6's rule and is not loosened here. But an *entry*
names one rig for one sheet, which is a narrower declaration than the folder-wide scan, so the
rig comes from there with `x-rigFrom` naming the entry. Seven buildings move from the stem
`Village` onto `house` and four onto `wharfbuilding`, both listed wire names. **No entity id
moves**: ids are minted from path and position and carry no vocabulary (§8.2), which is the
ruling that makes a family correction free.

### 6.5 The landing zone travels in the package

Ruled 2026-08-21: the write-back contract's landing zone (its §1.2 — whether an edit is a
one-value change to a row, or a thing the builder has no row for) ships **in band** rather than as
a root list the editor keeps in step by hand. Three choices inside that ruling were mine:

- **A sibling `x-zone`, not a second meaning inside `x-origin`.** §8.2 ruled that one key must not
  carry two facts; the zone is a *classification of* the origin rather than part of its identity,
  so folding it in would repeat the mistake that ruling had just corrected.
- **Keyed on a path prefix, longest match first — not on the root.** Roots mix: `StPetersWharf`
  holds a computed `Deck` and its rule-derived `Fittings` under one name. A root-level table would
  have given both the same answer.
- **Only (a) and (b) are per-entity.** Zone **(c)** classifies an *edit* — anything that changes
  baked pixels — not an entity, and the same building is (a) for its position and (c) for its
  siding. Emitting it per entity would say something false about every one of them.

The table lives at `reference/root-zones.json`, one row per prefix with the evidence for its class,
and the exporter reads it: `IslandVillage` is (a) because `StPetersVillage.Sites` is a literal list
of `new Site(...)`; `ClamHoles` is (b) because the builder's own note says the scatter jitter is a
stable hash of the grid cell. **A prefix the table does not carry exports no zone at all** — an
unknown zone is not zone (a), and telling the owner he may move something he cannot is the failure
this field exists to prevent.

**One new entry for the request list, arriving with the contracts.** Reading `shops.contract.json`
resolves the shop *levels* to `shopBuildingRig.js`, which normalises to `shopbuilding` — a name
the wire list does not carry. Four placements (two per region, the shop interiors, sprite stem
`ShopLevel`) therefore keep the stem, flag `x-familyIsSpriteStem`, and appear under
`unlistedFamilies` with the candidate named, exactly as §6.6 requires. **A rig resolving is not a
licence to name its family**: the two halves are reported separately because they are separate
facts, and the alternative — an entity holding a rig the editor cannot name, with nothing asked
for on its behalf — is the worse half of both states.

### 6.6 A ruling is not a loosened match rule

Two entries here are decisions rather than derivations, and both are one-line tables so the next
one has to be a decision too:

- **`wharf` → `wharfmodule`.** `wharfIsoRig.js` normalises to `wharf`, which the wire list never
  carried — it carried `wharfbuilding` *and* `wharfmodule`, so for two rounds this export declared
  the remainder rather than toss a coin. The editor published `wharfmodule` (its 44th: WharfIso,
  8 facings, drawing quay/pier/crib/float/gangway/slipway/riprap) and the mapping was ruled, so 24
  Nine Mile Creek placements resolve. A candidate with **no** ruling behind it still refuses; the
  guard test changed shape rather than being deleted.
- **The wharf fittings resolve to nothing, on purpose.** `NineMileCreekWharf.Fittings()` derives
  them by rule — a bollard per `BerthPos(i)`, tyres midway between every `TyreEveryNthBerth` pair,
  the ladder in a computed gap — so a family would hand the owner a gesture the next regenerate
  throws away, and the editor's own module auto-places its fittings, which would double-draw.
  They are marked `x-resolutionExcluded` and, importantly, **left out of the unresolved-sheet
  tally**: a decision and a missing sidecar must not read the same, because one is somebody's
  work item and the other is closed. 25 at Nine Mile Creek, 10 at St Peters — the same derived
  class on both wharves.


## 7. Making the package renderable

The editor draws **only** by calling rigs — it loads no images — so an entity with no rig gives
it nothing, and an empty terrain layer draws nothing. Three enrichments, each derived from a
source of truth and each declaring how far it goes.

### 7.1 Entities call a rig

Every entity whose sheet resolves to a rig now carries that rig's **installed global** and a
`call` block. The global comes from `RigCatalog`'s registrations where the catalog knows the rig,
and otherwise from the rig's own `root.X = …` publication — both are declarations, and the
second matters because the catalog registers only the rigs the Unity bakers bake, which is a
fraction of the palette.

**`opts` is deliberately `{}`.** A baked sheet does not record which option axes produced a given
cell, and the rigs resolve an unknown or wrong key as a *silent fallback* rather than an error —
the #571 review measured a mistyped species key rendering a different object at a different cell
size. An empty opts draws the rig's default build, which is true; a guessed one draws something
confidently wrong. Every `call` carries `x-synthesised: true`: it was reconstructed by the
exporter, never recorded from a bake.

### 7.2 Roads are stroked from the declared route table

`NineMileCreekMainland` declares each way's vertices and `NineMileCreekRoads` its surface,
half-width and rank; the exporter strokes them into one-metre cells, lowest rank first. That is a
**surface-material** raster, which is exactly what the format's road RLE carries — the review is
explicit that the export ships no mask index and that blob-47 stays derived in one place.

Ways whose route is *computed* rather than declared are **omitted and named**: the truck-park
spur solves for the nearest point on any road, and the town walks solve from each lot to the
nearest carriageway. St Peters declares no road table at all, and the package says so rather
than shipping an empty layer that reads as an oversight.

### 7.3 Ground is an iso-contour, at two honesty levels

The R8 height texture is a Git LFS object. On a pointer-only checkout the ground layer stays
unpainted with `x-unavailable` naming the reason; on a checkout with the bytes, the exporter
decodes the PNG and bands each cell by the shore map's **declared floor elevations**.

⚠ **That contour is not the ground Unity paints, and the package says so in
`layers.ground.x-derived`.** `ShoreMaterialAt` also wiggles the elevation so the rings meander,
tests a sandbar segment with its own spine rule, and chooses between two band tables by weather
sector. Those are logic, not declarations. Reimplementing them here would be a second definition
of the coastline — the review's §7(c) trap, whose failure mode is a shoreline that looks
approximately right and disagrees with the sim. The contour is true to the height map and
coarser than the paint, and that is the whole of what it claims.

### 7.4 Families are the editor's own wire vocabulary

The editor's `family` list is **closed** — 43 prop families and 5 tile layers, transcribed at
[`reference/family-names.json`](reference/family-names.json). An entity's family is resolved by
normalising its rig's filename (lowercase, drop a trailing pass digit, then the `rig`/`iso`
suffixes) and matching a listed name **exactly**.

**A near miss is never aliased.** `wharfIsoRig` normalises to `wharf`, and the list holds both
`wharfbuilding` and `wharfmodule`; choosing between them is guessing, so those placements keep
the sprite stem, flag `x-familyIsSpriteStem`, and appear under
`x-provenance.entityNotes.unlistedFamilies` with the candidate name and a placement count. That
block is the **request list for the editor side**, not a defect in the export.

Currently unresolved: `wharf` (24 placements, Nine Mile Creek) and `interior` / `interiorprop`
(4 + 26, St Peters — the gap the editor maintainer already named).

### 7.5 Region dimensions stand as exported

760 × 560 for Nine Mile Creek is the `RegionDef`'s truth. The editor's own region table is stale
(review §6.2 measured it at the C# field default). Ruled: **the package declares, the tool
conforms.** Nothing here compensates for a stale table on the other side.


## 8. The editor's second round (relayed 2026-08-20 evening)

Five additions, and one question that had to be measured before it could be answered.

### 8.1 `x-rigVersions` — one hash per family

Top-level and **flat**: `family -> {rigSource, sha256}`, for the families the scene actually
uses, with the family names as the keys and nothing else in there. The editor hashes its own copy
and badges a mismatch pink rather than refusing to draw.

⚠ It shipped one level down, as `{x-shaRule, families}` — so an editor doing what this section
says, one entry per family, read `x-shaRule` and `families` as two families and went looking
for rigs by those names. Flattened 2026-08-22; the hash rule moved to the sibling
`x-rigVersionsShaRule`, which is also where every `x-rigs` row already carried its own copy, so
nothing was lost by moving it out of the iterable. **A note that has to be stepped over to read
the data belongs beside the data, not inside it** — the same shape as §6.5's ruling that one key
must not carry two facts. A family that
resolved through **more than one rig** in a scene gets no single hash — it is reported under
`x-ambiguous` with each rig named, because one number there would be a lie. Neither region hits
that case today; the guard is for when one does.

### 8.2 Entity ids are minted from row identity — and carry no vocabulary

Formerly `family_001` ordinals — the defect the #571 review warns about in §8.3 and the editor
now depends on not having, since its write-back matches our rows by `id`. An id is now
`sha256(path|x|y)[:12]`: the builder names each object and computes where it stands, and that
pair is the row's identity. Measured on both regions — path alone repeats (94 objects are called
`ShorePlants/Eelgrass`), path + position does not collide once.

**No family prefix**, ruled 2026-08-20. A first draft read `{family}_{hash}`, matching the
reference package's `character_001` shape — but that re-keyed a row whenever a *vocabulary*
ruling renamed its family: 30 rows when the editor published `interior`/`interiorprop`, 24 more
waiting on `wharf`. Stability is the whole point of the field, and the entity already carries
`family` separately, so the id carries content identity alone.

Twelve hex characters rather than ten. Width can only ever be widened by re-keying, and this
ruling was the one moment re-keying was free; 48 bits leaves a far larger scene than either of
these clear of the birthday bound instead of parking a forced re-key in a future region.

### 8.3 `terrain.waterLevelMeters` — and why one number needs three

`RegionDef.TideMeanLevel`, in metres relative to **chart datum** — the same datum the height
map's elevations use, so elevation and water level compare directly. Both regions declare `0`.

One number is what was asked for and one number ships, but a wash drawn at it is drawn at *mean*
water, not the water now: `TideModel.Height` is `MeanLevel + amplitude * carrier`, and both
regions declare an amplitude of 2.2 m — a 4.4 m swing the mean says nothing about. So
`terrain.x-tide` carries the amplitude and phase alongside. The exporter states the model's
declared terms and never evaluates it: the tide is recomputed from `(worldSeed, gameTime)` and
never stored (CLAUDE.md rule 5), and this document has no clock.

### 8.4 `terrain.x-heightField` — a wash, not a survey

The referenced height file is unreadable from the editor's sandbox, so a **downsampled** copy
ships inline: `strideMeters` 8, nearest texel, no interpolation, row 0 north, metres on the datum
above. A 760 x 560 region becomes 95 x 70 samples instead of 425,600. `null` marks a sample
outside the painted map. Full resolution stays in `terrain.x-heightMap`, pinned by `textureSha256`.

Two-state on the LFS bytes, exactly like the ground layer (§6): present, it carries values;
absent, it carries `x-unavailable` naming why. `x-provenance.heightMap.textureBytesRead` says
which you are holding.

### 8.5 `x-interiorOf` — hierarchy, not geometry

The editor was drawing interior props on roofs. The builders already answer it: an interior
stands at `IslandVillage/school/Interior` and its furniture under
`IslandVillage/school/Furniture/`, so the container is the **nearest ancestor path that is itself
an exported entity**. All 30 St Peters interiors resolve, to 4 buildings. A point-in-footprint
test was available and rejected: at a shared wall it would put a prop in the wrong house, and the
scene already declares the answer.

### 8.6 Is per-instance variety enumerated or continuous? — **both, and the split is the answer**

Asked because the editor's bake cache keys on `family|facing|opts`, and a free-float per-instance
seed would make that cache 1:1 and useless. Measured against the builder tables rather than
assumed:

**The bake axes are enumerated.** Every sprite selector in the repo takes integers or enums —
`SpriteFor(int state)`, `SpriteFor(string kind, int variant)`,
`SpriteFor(stance, gait, int facingRow, int frame)`. Variant rolls are cast to `int`
(`GrassFieldScatter.VariantRoll` is `(int)(Hash01(...) * 1024f)`), mirroring is a `bool`, tide
state is an `int`. **Nothing anywhere selects a sprite by a continuous value.**

**But two genuinely continuous per-instance axes exist**, and neither is a bake key:

| Axis | Where | Reaches the package as |
|---|---|---|
| Uniform scale | `Mathf.Lerp(ScaleMin, ScaleMax, Hash01(...))` at five scatter sites, folded into `transform.localScale` | `x-scale` — 384 St Peters shore plants, 387 distinct values in 387 entities |
| Tint brightness | `Mathf.Lerp(0.9f, 1.1f, shapeRoll)`, a shader multiply over the sprite | **not exported** — applied at runtime; 1,029 of 1,032 banked renderers are pure white |

`NineMileCreekShorePainter` says it outright: *"the species' own PlantedScale x this site's
jitter, folded into the TRANSFORM"*. So the cache is safe **only if the scale is applied as a
draw-time transform on the baked sprite and never folded into `opts`**. It ships verbatim and
unquantised: enumerating it here would be this exporter inventing an axis the pipeline does not
have, which is what the ask explicitly forbade.

