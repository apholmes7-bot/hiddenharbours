# `hiddenharbours.scene/1` — the write-back half

**What this is.** The **return leg** of the scene-export contract: what the owner may change in his
scene editor, how those changes come home, and how we prove they arrived. The outbound half —
the envelope, the seventeen settled fields, the RLE rules — is
[`scene-export-contract.md`](scene-export-contract.md) (PR #588), and every field name used here is
that document's. Read it first; this file only ever adds a *direction*, never a second definition
of a field.

**Lane:** tools-editor. **Direction:** inbound. **Status:** contract only — this PR ships no
importer, no exporter change and no builder change. The build lane comes later and runs where
Unity lives.

> **The three documents this one links to sit on PR #588's branch, not on `main` yet**
> (`scene-export-contract.md`, `scene-editor-review.md`, `reference/sample-scene.json`). The links
> resolve once that PR lands; until then the citations name what they came from so nothing here
> depends on holding the file. Every claim quoted from the exported packages was checked against
> `tools/scene-export/packages/` on `claude/scene-export-regions-3vf5yw`.

**Why it exists now.** The owner stated his purpose for the tool on 2026-08-20: *inspect each scene
as the player sees it; rotate buildings easily ("some buildings read better at certain
orientations"); change design elements; choose colours and structure size; place foliage; adjust
wharf design; spot what's missing.* Read-only inspection is #588. Everything after the semicolon is
a write, and a write needs a contract before it needs code — because the honest answer to "may I
turn this house?" is different for a house, a tree and a breakwater block, and nobody should learn
which by watching an edit silently do nothing.

---

## 0. The four rulings that shape the return leg

**0.1 — The builders are the source of truth. The scene is their output. Edits land in BUILDER
TABLES, never in `.unity`.** This is not a preference; it is what the repo does. Both region
builders open with `EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single)`
— `NineMileCreekBuilder.cs` and `StPetersBuilder.cs` alike — and rebuild from zero. An edit written
into `NineMileCreek.unity` survives exactly until the next build and then is gone, with no error
and no trace — the failure mode ADR 0019 was written about, which has already eaten one
hand-added spotlight. So the importer's output is a **diff against builder source**, and the
`.unity` is regenerated from it.

> ⚠️ **This ruling has an expiry date, and it is not ours to set.** ADR 0019 (*Proposed — awaiting
> owner sign-off*) flips the default: an **adopted** region's committed `.unity` becomes the source
> of truth and the builder is confined to a tagged `--LOGIC--` subtree. Neither Nine Mile Creek nor
> St Peters is adopted today — both still `NewScene` — so §0.1 holds for both regions this contract
> covers. If either is adopted, the write-back target for its *visual* layer changes from the
> builder table to the scene, and this section must be re-decided rather than reinterpreted. The
> importer must therefore **refuse to run against a region whose builder does not `NewScene`**,
> rather than assume it still owns the bytes.

**0.2 — The package declares; the tool conforms.** The exported region dimensions are the region's
truth, read from the `RegionDef` asset: Nine Mile Creek is **760 × 560 m**, St Peters **760 × 520 m**,
at `terrain.cellMeters` 1. The editor's own region table carries 160 × 120 for the creek; that table
is stale on the tool's side and is being fixed there. No edit is accepted that assumes the smaller
frame, and no exporter change is made to match it. A `scene-edits.json` whose positions fall outside
the declared `worldSizeMeters` is refused as out-of-frame, not clamped. *(Coordinator ruling,
comment 5357286401 §4.)*

**0.3 — An edit naming a value outside the rig's own option table is REFUSED at import, with a
reason. It is never coerced.** The rigs themselves will not do this for us, and that is precisely
why the rule has to live here. `lobsterBoatVariantsIsoRig.js` resolves a variant with

```js
const byId = (arr,id,def)=>arr.find(o=>o.id===id) || arr.find(o=>o.id===def);
```

— so `{size:'medium'}` (not an id; the ids are `inshore`/`standard`/`offshore`) resolves silently to
`standard` and renders a perfectly good boat that is not the boat asked for. `RigCatalog.cs` names
this as the rigs' shared house style: *"resolve as `opts[k] ?? fallback`, never complain."* An
importer that forwards an edit to a rig inherits that silence, and the owner's experience of a typo
becomes "I chose oxblood and nothing happened."

**0.4 — The option table is read from the rig, at import time. Never transcribed, never quoted from
a doc.** Measured, not asserted: `lobster-paint-kit/README.md` describes the variants rig as
*"3 sizes × 3 styles × 3 regions × 12 paints, 324 legal combinations."* The rig on disk ships
`SIZES` 3, `STYLES` **2** (`open`, `hardtop`), `REGIONS` 3, `PAINTS` 12 — **216**. A prose table
drifted from its rig by a whole style axis, in the same repo, unnoticed. Any list of legal values
this document contains is therefore an **illustration with a source path**, not the authority; the
authority is the rig file, and the importer resolves against it.

---

## 1. The editable surface

Read this table as: *may the tool change it, who decides the legal values, where does the change
land, and what does honouring it cost.* **Every field of an exported entity that is not listed as
editable is READ-ONLY**, and §1.3 says so field by field so that no absence has to be interpreted.

### 1.1 Editable

| Field | Legal values decided by | Lands in | Cost to honour |
|---|---|---|---|
| **orientation / facing** | the sheet's own facing count — see §2 | the placement row's facing/heading argument | none for a baked-facing entity (a sub-sprite swap); none for a mesh hull (a yaw) |
| **colourway** | the piece's paint / body / ramp table in its rig (§0.4) | the placement row's paint or dial argument | **free** if it is a runtime ramp (ADR 0029), **free** if the sheet already carries the other value as a cell (§1.2 c), **re-bake** only if it is baked in with no other cell |
| **structure size** | the rig's own size axis, and only it | the dial table row (e.g. `VillageBuildingKit.M1Set`) | **re-bake** — size moves geometry |
| **structure style / variant** | the rig's own style/shape/era/region axes | the dial table row | **re-bake** |
| **position** | the declared region frame (§0.2), plus the builder's own clearance rules | the placement row's position constant | none — but see §1.4, clearance is law |
| **foliage add / remove** | the species tables of `treeIsoRig2` / `shrubIsoRig` / the flower and shore-plant kits | **nothing that exists today** — see §4.3 | an override list must be built first |
| **wharf pieces** | the kit's piece names | **generated, not authored** — see §4.3 | as above |

> **A colourway is a NAMED id in v1, and free-form colour is deliberately left out.** Several rigs
> will take one: `houseIsoRig`'s own header offers
> `body: 'greyShingle'|'white'|'cream'|'red'|'sage'|'blue' | custom ramp`, and `puntIsoRig` accepts
> `paint:{hull,trim,cove,bottom,interior}` mixed from any base colours. That is real capability and
> it is not being denied — but a hex triple has no option table to be checked against, so §0.3 has
> nothing to refuse and the KTC palette discipline (ADR 0015's water guard-rail is the same concern
> one layer down) has nothing to hold. Named ids first; free-form colour is a v2 question with the
> art lane, not an oversight here.

### 1.2 The three landing zones, and why the same edit is cheap or impossible depending on where it lands

This is the single most important thing in this document, and it is not visible from the exported
package at all. Three different mechanisms produce entities, and an edit's fate depends entirely on
which one produced the entity it names.

**(a) An authored table row.** A literal list of constants: `StPetersVillage.Sites` (four named
sites, each `new Site(key, position, reason)`), `VillageBuildingKit.M1Set` (each building a
`Build.FromDialled(key, label, "house", { era, shape, siding, body, roof, size, windows, winDensity,
attic, porch, dormers, chimneys, bay, weather })`), `NineMileCreekMainland.TownLots` / `ShantyRow`,
the road polylines. **An edit here is a one-value change to one row.** This is the zone the owner's
whole stated purpose lives in, and it is the zone this contract can actually deliver.

**(b) A generator with no row to edit.** `StPetersWoods.ScatterTrees` walks candidate cells, tests
`InStand(p, e)` against a noise field and a threshold, and picks a species by *a stable hash of
position* — deliberately, so a rebuild reproduces the wood. `ScatterFlowers`, `StPetersShrubs.Scatter`,
`NineMileCreekWharf.Fittings()`, `ArmourRun()` and `BreakwaterBlocks()` are the same shape: the
breakwater's block count is `floor(width / ArmourWidthMetres)` and each block's variant is a hash.
**There is no row for tree #23.** Moving or deleting one means either an explicit exception list the
generator consults, or a change to the field parameters — which moves every other tree with it.

> **Checked, not assumed: no builder in this repo has an override or exception hook today.** A grep
> for `Override` / `Exclusions` / `Excluded` / `SkipSites` / `Suppress` across
> `Assets/_Project/Code/App/Editor/*.cs` returns nothing. So "place foliage" and "adjust wharf
> design" are **not** contract gaps to be filled by writing an importer — they are missing builder
> capability, and §4.3 states what has to exist before an edits file can name them.

**(c) A baked axis.** Anything that changes drawn geometry — size, shape, siding, porch, dormers, and
every colour baked into a sheet rather than swapped at runtime — is pixels, and pixels come from a
V8 bake that ADR 0021 fences to the editor for licence reasons. **No JavaScript runs in a shipped
build.** So these edits are honourable, but the honouring is a bake, not an import: the importer's
job is to write the dial and *declare that a re-bake is owed*, never to pretend the change is live.

> **"Is this a baked axis?" and "does this edit owe a bake?" are different questions, and the
> recipe answers both per sheet.** Ruled 2026-08-21, after the recipe ledger (#629) made the
> distinction measurable. A `<stem>.recipe.json` records the axes a sheet was baked across, and
> they split two ways:
>
> - an **`opt:`-bound axis** has its other values *already baked as cells* — across all 298
>   committed recipes these are `fill` and `swing`. Honouring one is a **sub-sprite swap, exactly
>   like a facing**: live, no bake owed, and the odometer position (§2.3) gives the cell.
> - a **base `call.opts` key** was baked in as a constant, and no other cell exists — 36 distinct
>   keys, including `awning`, `bollards`, `hoses`, `keyline` and `len`. These genuinely owe one.
>
> Collapsing the two is wrong in one direction or the other: treat every baked opt as owing a bake
> and two of them become needlessly deferred; treat none as owing one and the other thirty-six are
> silently dropped. **The importer consults the recipe before marking an edit owed**, and a
> colourway swap that has a cell already reads as live rather than deferred.

### 1.3 Read-only, and why — field by field

| Field | Why it cannot be edited |
|---|---|
| `id` | the binding handle for the whole edits file (§3.3). Changing it is a delete plus an add. |
| `family` | the sprite sheet's name stem, an output of the bake (`x-familyIsSpriteStem: true`). Change the rig or the sheet, not the stem. |
| `rig`, `rigSource`, `x-rigSha256` | a pin on bytes. Editing it would assert a provenance that is not true. |
| `cell`, `unityPivot`, `x-pivotSource` | read from the sheet's `.png.meta` import settings, written by the baker. A "corrected" pivot puts a building metres into the dirt (`StPetersVillage` class remarks). |
| `sortBias` | a y-sort **tie-break delta** from `SortingBands.DecorBase`, computed from the C# constants. Sorting is the builder's law, not a design element. |
| `footprint`, `gameplaySidecar` | rig gameplay measurements. Gameplay reads these; changing one changes where a hauler mounts, not how a boat looks. |
| `flipX` | a mirror, not an orientation. For a baked-facing entity the facings already cover the circle, and a flip contradicts the fixed key light (upper-LEFT) that every rig bakes to. |
| `terrain.*` (all layers, legend, RLE, `pieces`) | derived — §6.3 and §6.4. |
| `paths[]`, `cliffLines[]`, `collision` | derived, never authored (`collision.note`, verbatim in both packages). |
| `region`, `frame`, `stats`, `schema`, `generatedBy`, `generatedAt`, all `x-provenance` | facts about the export, not about the harbour. §0.2. |

### 1.4 Two constraints an edit cannot argue with

- **Clearance is law, and it is the builder's, not the tool's.** `StPetersVillage` reserves a
  building's footprint as a **circle** of the half-diagonal, deliberately, so a site holds the same
  ground at every facing — `LaneGap` 4 m between footprints, `HearthClearanceRadius` 8 m
  around the green the village is arranged on, `PropClearance` 2.5 m from the small interactables. A position or size edit that violates these is refused with the measured
  overlap in the reason. A rotation never can (that is what the circle bought).
- **A door is a gameplay seam.** `BuildingFacing` derives the door's ground bearing from the baked
  per-facing anchors, and a room registers its doorway to that anchor. Rotating a building moves
  where the player walks in. The importer applies the rotation and **re-derives** the door bearing;
  it never carries the old one forward.

---

## 2. Orientation, per entity class — 8, 32, or continuous

The owner's first ask, and the one where a single global answer would be wrong. **The steps are the
sheet's, and the sheet says how many.** `IsoPropSheetBaker.cs` already fails a bake loudly when
the rig's measured `NativeDirs` disagrees with the contract's `Facings` — *"One of the two is
stale."* The importer inherits that posture: it reads the count, it does not assume one.

| Entity class | Steps | Where the count is declared | How a turn is applied |
|---|---|---|---|
| Baked buildings — `Village`, `Shopfront`, `ShopLevel` | **8**, at 45° | `shopBuilding.contract.json` → `projection.facings` (8) / `projection.step` `"45°"`; `VillageBuildingKit.Entry.facings` | **swap the sub-sprite** via `VillageBuildingCatalog.SetFacing`. **Never rotate the transform** — the art is baked at a fixed camera and a rotated sprite lights from the wrong side. |
| **Legacy single sprites** — `Cottage`, `ShipwrightShed`, `GreywickHouseRed`, `GreywickHouseTeal` | **1** | nothing declares one: no rig, no contract, and a sprite name ending `_0` rather than `_d<n>` | **not turnable.** A `facing` edit on one is refused `out-of-range` with `facings: 1`. Turning one means baking it from a rig first — a kit request, not an edit. |
| Baked iso props — wharf decor, utility, shore finds, interior props | **8** | each kit's `*.contract.json` `Facings`; cross-checked against the rig's `NativeDirs` | sub-sprite swap |
| Nav buoys | **8** | `NavBuoyKit.Facings = 8` — *"Facings come from HERE and from the contract, never from a `DIRS` field"* | sub-sprite swap |
| Fuel containers | **8** (`FuelContainerDef.Facings`, default 8, *"clockwise order from north"*) | the Def asset — data, per ADR 0003 | frame index `fillIndex × Facings + facing` |
| **Sprite hulls** — the dory, the punt, small craft | **8 today**, 32 coming | the rig: `doryIsoRig.js` and `puntIsoRig.js` both export `DIRS: 8`, and the creek's dory is on cell `DoryIso_2`. ADR 0006's *"32 facings, not 8 — the owner's decision"* is **Proposed, deferred to M2**, with sheets 8 columns × N rows and flat index `heading × rockFrames + frame` | heading index — **read the count, never assume it**, because this is the one class where it is scheduled to change under a hull that keeps its name |
| **Mesh hulls** — the large boats under ADR 0022 | **continuous** | there is no facing table; the hull is a real-time 3D mesh | a yaw in degrees. **None appears in either committed package today** — the fleet landed after the scenes were banked — so this row is the rule for when one does, not a description of the current bytes |
| Characters | **8** | `BuildingInterior`'s note is the shared recipe — *"8 at 45°, the ADR-0006 recipe"*; the sheets name it too (`Cutter_idle_d4_f0` — `_d<dir>_f<frame>`) | sub-sprite swap |
| Foliage, shore plants, seaweed, scattered props | **none** | these rigs publish variants, stages and seasons — not facings | **there is no orientation field.** An edits file that sets one is refused (§4.4). |

**The field.** An edits file names orientation as **`facing`, an integer step index** for every
baked class, and as **`headingDegrees`, a float** for a mesh hull only. Two names because they are
two different things, and one name would let a 45 slide between them meaning two different boats.
`facing` is refused if it is outside `[0, facings)` for that entity's own sheet — an 8-facing house
handed `facing: 12` is an error with the count in the reason, never `12 mod 8`.

**The current facing is already in the export, and the round trip should read it from there.** The
kit names each sliced sub-sprite `SpriteNameFor(buildKey, facing) => $"{stem}_d{facing}"`
(`VillageBuildingKit`), and the packages carry that name verbatim in `x-sprite.name` — St Peters
ships `Village_school_d4`, `Village_whiteFarmhouse_d5`, `Village_redSaltbox_d6`,
`Village_sageCottage_d0`; the creek's saltbox is `_d3` and its sage cottage `_d2`. So:

- **an edits file does not have to guess the current orientation** — it is `_d<n>`, and a turn is a
  delta the tool can compute against a fact already in the document it was handed;
- **§5's check 1 is testable today**, because "did the rotation land" is "did `x-sprite.name` come
  back with the new `_d<n>`" — no new export field required to prove the round trip;
- **the legacy sprites announce themselves**: `Cottage_0`, `ShipwrightShed_0`,
  `GreywickHouseRed_0`, `GreywickHouseTeal_0` have no `_d` at all, which is how the row above is
  decided per entity rather than per family name.

Promoting `_d<n>` to an explicit `facing` field is worth doing on the export side (§8), but the
return leg does not block on it.

**One measured warning, carried here so it is not re-learned.** `BuildingFacing`'s remarks record
that the arithmetic version of "which facing points the door at the green" had a sign error —
cell `i` is baked at `dir = (facings − i) mod facings`, so a door's bearing **decreases** as the cell
index rises — and it put the schoolhouse door ~92° off the green it faces, with a green test, because
the test was the algebraic inverse of the implementation. **The importer never computes a facing
from an angle.** It stores the index the tool chose, and any derived bearing is read back from the
baked anchors.

---

## 3. The diff format — `scene-edits.json`

### 3.1 What produces one

The editor has **no import path today** — no `FileReader`, no file input, no path from parsed JSON
into editor state (lead-architect's sweep of the 1.15 MB standalone HTML, PR #588). It does have
`doExport()`, which writes a full `hiddenharbours.scene/1` document out of its live state. So the
round trip does **not** wait on the tool learning to emit diffs:

> **The owner exports a whole package; we compute the diff.** He opens our package (once the tool's
> import lands — their side), turns the buildings, exports, and hands back a complete
> `<Region>.scene.json`. `scene-edits.json` is what **we** derive by diffing his document against
> the one we gave him.

That makes the edits file a repo-side artefact with a repo-side producer, and it means the format
below is a contract between two of our own programs — which is the only kind we can keep. A future
editor feature that writes one directly must produce the same document.

It also names the dependency the whole round trip rests on: **the tool's export is only as
expressive as its import was**, and its internal model is family + dir + opts. A package with no
`call`/`opts` can never seed it (export contract §0), which is why §6.2 is a precondition of this
document and not an optional enrichment.

### 3.2 The envelope

```jsonc
{
  "schema": "hiddenharbours.scene-edits/1",
  "basedOn": {
    "region":     "region.nine_mile_creek",   // region.id from the package
    "package":    "tools/scene-export/packages/NineMileCreek.scene.json",
    "sha256":     "…",                        // of the package the edits were computed against
    "generatedAt":"2026-08-20T13:32:46Z"      // the package's, copied — not a clock reading
  },
  "edits": {
    // "at" is the BINDING PIN — the entity's position as exported (§3.3 rule 2).
    // Required on every entry. "pos" is a CHANGE, and only present when the edit moves it.
    "e2c81a294ec7": { "at": [-148, 196], "facing": 6 },                       // CreekHouses/redSaltbox — turned
    "0250fd9b1c0f": { "at": [-208, 60],  "body": "blue", "size": 0.55 },      // CreekHouses/sageCottage — repainted + grown
    "554a7958e6d8": { "at": [81, 121],   "facing": 2, "pos": [80.5, 121.0] }  // CreekShops/fishMarket — turned + nudged
  },
  "adds":    [],
  "removes": [],
  "x-note":  "free-form; ignored by the importer"
}
```

- **`schema`** — its own name and version, distinct from `hiddenharbours.scene/1`. A reader that is
  handed the wrong one refuses rather than half-understanding it.
- **`basedOn.sha256`** — the binding. §3.3.
- **`edits`** — a flat map, **entity id → `at` plus changed fields only**. Unchanged fields are
  absent; an entry present with its exported value is a no-op, not an error.
- **`at`** — required on every entry, and **never a change**: it is the entity's `pos` as the
  package exported it, and it is what rule 2 below re-checks. It is a separate key from `pos`
  precisely because an edit may also *move* the entity, and one key doing both jobs could not tell
  "the shed I meant" from "where I want it".
- **`adds` / `removes`** — §4.3. They exist in the envelope from v1 so the shape does not change
  when the capability arrives, and they are **refused as unsupported** until it does.
- **Unknown `x-` keys are ignored**, per the export contract's §0 ruling, which this document
  inherits whole. **Unknown non-`x-` keys are refused** — inside a document whose entire purpose is
  to name fields, a key we do not recognise is far more likely a typo for one we do than a future
  extension, and silently ignoring it is the coercion §0.3 forbids wearing a different hat.

### 3.3 Binding an edit to a thing — and the identity trap

An id resolves to a builder table row through the package's `x-path`, which is the scene hierarchy
path. For authored rows the path leaf **is the table key**, and the binding is exact:

| `x-path` | resolves to |
|---|---|
| `IslandVillage/school` | `StPetersVillage.Sites[key == "school"]` → `VillageBuildingKit.M1Set["school"]` |
| `CreekShops/fishMarket` | the shop table row for trade `fishMarket` |
| `NineMileCreekDressing/Services/powerPole_0` | generated along a road polyline — **not a row** |
| `IslandWoods/RedMaple_1` | scattered — **not a row**; `_1` is a *variant* name the builder reuses across instances, so this path is not even unique |
| `NineMileCreekWharf/Breakwater/Crib_94.8` | generated — the leaf encodes the block's x, in metres |

So: **a generated entity's handle is an ordinal or a coordinate, not an identity** — and this is
worse than it looks, because the two available handles fail in different ways. Counted over the two
committed packages:

| | Nine Mile Creek (291 entities) | St Peters (1,028 entities) |
|---|---|---|
| unique `id` | **291 — every one** | **1,028 — every one** |
| `x-path` values shared by more than one entity | 3 | **83** |
| worst case | `NineMileCreekWharf/Fittings/Fitting_bollard` — **14 entities** | `ShorePlants/Eelgrass` — **94 entities** |
| unique `(x-path, pos)` pairs | **all of them** | **all of them** |

The `x-path` is stable but **not unique**: `IslandWoods/RedMaple_3` is a *species-variant* name the
builder reuses, and ninety-four eelgrass plants share one path outright. On its own it is a note
pinned to a crowd.

**The `id` closes that, because it is the pair.** It was once an export-local ordinal — `RedMaple_007`
was the seventh red maple the scene walk met, and became a different tree the moment the noise
field moved, with no error anywhere. Since the export's id rework it is
`sha256("{x-path}|{x}|{y}")[:12]`, twelve hex characters over the entity's path and its exported
position rounded to the millimetre. That is precisely the `(x-path, position)` pair this section
concludes is the only unique handle, so the two rules below now rest on the id itself rather than
on a convention the two programs have to keep agreeing about. It carries **no family name**: a
vocabulary ruling that renames a family must never re-key a row.

**`(x-path, position)` is unique in both packages, with zero collisions** — which is why the pin in
rule 2 is not merely a staleness guard. For authored rows it catches drift; for generated ones it is
the only thing that identifies the entity at all.

**An entity that moves re-keys, and that is a property rather than a defect.** Because position is
part of the digest, a builder that shifts a shed gives it a new `id` on the next export and the old
one resolves to nothing at all — so the failure reads *"no such id in this package"* rather than
*"the id is here but the thing moved"*. The contract needs no diagnostic for the second case,
because no consumer is ever in a position to need one:

- an edits document is valid **only** against its sha-pinned `basedOn` package (rule 1), so inside
  a single round trip every id it names resolves by construction; and
- a successful write-back is followed by re-export and re-import — the editor side requires package
  bytes and refuses to work from a restored autosave — so nothing tracks one entity *across* a move.

"Id not found against current state" is therefore a staleness signal that rule 1 already catches
earlier and more precisely, not an error class of its own.

Two rules follow, and they are the whole of the file's safety:

1. **`basedOn.sha256` must match the package on disk exactly**, or every edit in the file is refused.
   Not a warning. The exporter's `--check` already re-derives the package and compares, so the
   importer's first act is to run it: if the committed package is stale, the edits were computed
   against a harbour that no longer exists.
2. **Every edit carries `at`, the entity's exported position**, and the importer re-checks it after
   resolution. If the entity now at that id is not within **ε = 0.01 m** of `at`, the edit is refused
   as **unbound** — never applied to whatever now holds the id. This is what stops "rotate that
   shed" from turning a different shed after an unrelated commit. (ε is a binding tolerance, not a
   placement one: the exporter writes float metres and the check only has to survive round-tripping
   them, so it is deliberately far tighter than any real gap between two entities.)

Rule 2 is cheap for authored rows (they do not move unless the edit moves them) and is exactly the
guard the generated ones need.

---

## 4. The import path

### 4.1 Order of operations

1. **Verify.** `--check` the package; compare `basedOn.sha256`; confirm the region's builder still
   `NewScene`s (§0.1). Any failure stops the whole file — never a partial apply.
2. **Resolve.** Each id → its `x-path` → its landing zone (§1.2 a/b/c). Zone (b) and zone (c) edits
   are separated out here, not at the end.
3. **Validate.** Each field against §1's editable list, and each value against the **rig's own option
   table, read from the rig file** (§0.4) — plus the frame check (§0.2), the facing range (§2) and
   the clearance rules (§1.4).
4. **Refuse, in full, before applying anything.** §4.4. A file with one bad value applies none of its
   edits. The alternative — apply nine, refuse one — leaves the repo in a state no document
   describes, and leaves the owner to work out which nine.
5. **Apply.** Write the dial/placement changes into the builder source as a diff, one commit,
   reviewable as source. Declare every re-bake the edit owes (§1.2 c).
6. **Rebuild and re-export.** §5.

### 4.2 What the importer writes, and what it must never write

**Writes:** builder source — a value in a table row. Nothing else.

**Never writes:**
- **a `.unity` file.** §0.1. Not even "as well, to save a rebuild."
- **a rig file.** Rigs are art-director property (`agents/coordination.md`); an edit that would need a
  new option is a request to that lane, not a patch. This is also the ADR 0021 licence fence: the
  importer does not author what the baker executes.
- **a sheet, a `.meta`, or a pivot.** Those are bake output.
- **a value it invented.** Including a "nearest legal" one — §0.3.

### 4.3 Foliage and wharf pieces: what has to exist first

The owner asked for both by name, so this section says exactly what is missing rather than leaving
the capability implied by the envelope's `adds` / `removes` arrays.

Placement in these families is a deterministic function of position — a hash, no `System.Random`,
so a rebuild reproduces the wood exactly. That determinism is load-bearing and is not being traded
away. What is needed is a **seam**: an authored override list, per region, that the generator
consults and that survives a rebuild —

- **`removes`**: a list of suppressed positions the scatter skips, matched within a tolerance.
- **`adds`**: a list of authored sites the scatter appends after its own pass, each naming a species
  from the rig's own table and taking the same clearance tests.

That list is **builder work, not importer work**, and it does not exist in any builder today
(grep, §1.2 b). Until it does, `adds` and `removes` are refused with `unsupported: no override list
for <family> in <region>` — which is a true statement about the repo, and a better answer than an
edit that appears to work until the next build.

The same applies to wharf pieces: `Fittings()`, `ArmourRun()` and `BreakwaterBlocks()` compute their
own contents from the deck rect, `BerthPos(i)` and `ArmourWidthMetres`. "Adjust wharf design" in v1
means **editing those constants** — an authored-row edit, zone (a), fully supported — not adding or
deleting individual blocks.

### 4.4 Refusals

A refusal is a **document**, not a log line, and the tool is expected to show it:

```jsonc
{
  "schema": "hiddenharbours.scene-edits-result/1",
  "applied": [],
  "refused": [
    { "id": "0250fd9b1c0f", "field": "body", "value": "seafoam",
      "reason": "not in the rig's body table",
      "allowed": ["greyShingle","white","cream","red","sage","blue"],
      "source": "docs/art/rigs/houseIsoRig.js — const BODY" },
    { "id": "9f2c41ab77e0",
      "reason": "unbound — no entity in this package carries this id; the package has moved on from the one this file was computed against (§3.3)" }
  ]
}
```

Every refusal carries **`reason`**. A refusal about a *value* also carries **`field`**, **`allowed`**
and the **`source` path the list was read from**; a refusal about the *entry* — `unbound`,
`stale-package` — carries no `field`, because none of them is the thing that was wrong. The source path is what makes a refusal actionable: the
owner can see the six body colours *and* where they live, and "add a seventh" becomes a clear
request to the art lane instead of a mystery.

Refusal classes: `not-editable` (§1.3) · `not-in-option-table` (§0.3) · `out-of-range` (a facing
outside `[0, facings)`) · `out-of-frame` (§0.2) · `clearance` (§1.4, with the measured overlap) ·
`unbound` (§3.3) · `unsupported` (§4.3) · `stale-package` (§3.3 rule 1) · `unknown-field`.

---

## 5. Validation — proving the round trip

The claim to prove is narrow and total: **applying an edits file changes exactly the fields it
names, and nothing else.**

```
package₀ ──apply(edits)──► builder source ──rebuild──► scene ──re-export──► package₁
```

Then `package₁` must satisfy, in this order:

1. **The named fields changed, to the named values.** For every `edits[id][field]` other than `at`
   (the binding pin, §3.2), `package₁`'s entity `id` carries that value. A rotation that is silently
   a no-op fails here — which is the specific defect this whole document exists to make impossible.
2. **Everything else is byte-stable.** Not "equivalent": byte-identical, once the four provenance
   fields in §5.1 are normalised. The exporter is already a pure function of the repo with
   `DeterminismTests` on both halves, so byte-stability is a property it has, not one this test has
   to invent.
3. **The invariants still hold.** `sum(runs) == cols × rows` on every layer; row 0 north; `0` the
   reserved no-tile value; both pivot forms in agreement to under a hundredth of a pixel (ADR 0026);
   `sortBias` inside half the sorting band. These are #588's tests and they run unchanged — an
   importer that breaks one has broken the export, not the edit.
4. **A second apply is a no-op.** Re-run the same edits with `basedOn` re-pinned to `package₁` — the
   re-pin is required, because §3.3 rule 1 would otherwise refuse the file outright, and refusing is
   not the same as changing nothing. The apply must write an **empty diff to builder source**.
   Idempotence is what distinguishes "wrote the dial" from "nudged the dial", and the re-pin is what
   makes the check test the importer rather than its own staleness gate.

### 5.1 The four fields that move for honest reasons

A naïve byte-compare fails on a *correct* round trip, so name the exceptions rather than discovering
them as flakes:

| Field | Why it legitimately moves |
|---|---|
| `generatedAt` | the committer date of the newest **input** commit. Applying an edit *makes* a commit, so this moves — and must. |
| `x-provenance.sceneLastBuiltCommit` / `sceneLastBuiltDate` / `sceneLastBuiltSubject` | the rebuild banks a new scene. |
| `x-provenance.builderDrift` | measured from that new commit; the drift count resets. |
| `x-provenance.sceneFileSha256` | a different scene file. |

Everything else — every entity not named in `edits`, every unedited field of every entity that is,
the whole terrain block, `paths`, `collision`, `stats`, `x-rigs` — compares byte-for-byte.

### 5.2 Where the test runs

The apply-and-rebuild leg needs Unity, so the full loop is a lane that runs where Unity lives. Two
of the four checks do not: **(1)** and **(2)** can be pinned today as an EditMode-free fixture test —
a recorded `package₀`, a recorded edits file, a recorded `package₁` — which catches format drift in
either document without a build. The full loop is the build lane's acceptance criterion.

---

## 6. The four ruled points

From the editor maintainer's import diagnosis (PR #588, comment 5357286401), folded in here for
their bearing on the return leg. The **export-side** implementation of 2–4 belongs to the export
lane and is in flight there; what follows is what each one means for a write.

### 6.1 The package declares the region's dimensions; the tool conforms

Ruled in §0.2 because everything else depends on it. Restated for completeness: 760 × 560 (creek),
760 × 520 (St Peters), from the `RegionDef`. The tool's stale 160 × 120 table is being fixed on their
side. **No exporter change, and no edit accepted in the small frame** — an edits file whose positions
only make sense at 160 × 120 is refused `out-of-frame` rather than scaled, because a scale factor
that is wrong by a little produces a plausible harbour in the wrong place.

### 6.2 Entities carry `rig` + `call` blocks, via the RigCatalog stem→rig mapping

The editor renders **exclusively by procedural rig calls** and loads no images, so an entity exported
as a sheet stem with `rig: null` gives it nothing to draw. The export lane derives a stem → (rig,
opts) mapping from `RigCatalog`'s source and emits `rig` plus `call: {fn:'render', opts}` for every
entity the catalog resolves; the honest remainder stays declared under
`x-provenance.entityNotes.unresolvedSheets`.

**What this means for a write, and it is not a detail:** `call.opts` **is** the editable surface.
It is the tool's own internal model (family + dir + opts), it is what `doExport()` writes back out,
and it is therefore the vocabulary a diff is computed in. Two consequences:

- **An entity with `rig: null` has no editable surface at all.** We cannot honour a colourway on a
  sheet whose rig we do not know, and the tool cannot draw it to be turned. Unresolved entities are
  **inspect-only**, and an edits file naming one is refused `not-editable` with the
  `unresolvedSheets` entry as the reason. Measured on the two committed packages, that is
  **145 of 291** entities in the creek and **345 of 1,028** in St Peters — chiefly the hand-drawn
  wharf tilesets, plus the dory and the fisher sheet, which have no sidecar to trust.
- **`opts` keys are rig keys, and the refusal in §0.3 is a check against the same rig the call
  names.** No second vocabulary, no mapping table of ours to drift.

**The two gates are independent, and both must pass.** "Does this entity resolve to a rig?" (this
section) and "does this entity have a table row to edit?" (§1.2) are different questions with
different answers, and an entity can fail either alone. A scattered `IslandWoods/RedMaple_3`
resolves cleanly to `treeIsoRig2` and still has no row; a `CreekHouses/Cottage` sits in an authored
builder line and still resolves to no rig. An importer that checks one and assumes the other will accept edits it cannot honour.

### 6.3 Road layers are rasterized from the route polyline tables; full coverage required

The committed scenes carry no road tilemap because the builder paints roads. The route polylines are
in sources the exporter already reads — `NineMileCreekMainland.BarRoad`, `WharfRoad`, `ThroughRoad`,
`GullyPath`, with `RoadHalfWidth = 3f` — and the export lane rasterizes them into the road layer's
RLE at package cell resolution. `sum(runs) == cols × rows` is a requirement **on us**; the editor's
own reference sample under-covers its road layer (18,879 of 19,200) and **must not be matched**.

For a write: **the road raster is derived, and derived layers are read-only** (§1.3). Painting a road
cell in the tool is refused. The real edit is the **polyline** — a node moved in `WharfRoad` — and
polyline editing is out of scope for v1 (§7), because a road node moves the roadside furniture that
was placed along it (`powerPole_0` and its siblings are positioned from Wharf Road) and that is a
second-order rebuild this contract has not specified.

### 6.4 The ground layer is two-state, and consumers must handle both

The ground is an iso-contour of an R8 height texture that lives in Git LFS (ADR 0014/0028). One
exporter, two honesty levels, each **declaring which it is**:

- **LFS bytes absent** (the cloud container): the layer ships zero-filled at full coverage, flagged
  `x-readOnly` / `x-derived` / `x-authorable: false` with an **`x-unavailable`** note, and the map
  pinned by `textureSha256` — which is the same hash either way, since an LFS pointer's `oid sha256`
  **is** the sha256 of the content.
- **LFS bytes present** (a full checkout): the iso-contour ground layer is rasterized into the
  package.

**Every consumer of a package must handle both states**, and that includes the importer. Its rule is
simple because ground is read-only in both: **an edits file computed against an `x-unavailable`
package is fully valid.** Edits key on entity ids, not on the ground layer, and none of §1's editable
fields lives in terrain. The two states differ in what the owner can *see* while composing — which
is the whole argument for doing the LFS-present re-export — not in what he may *change*.

---

## 7. Out of scope for v1 — named, not implied

| Out | Why |
|---|---|
| **Terrain / height** | the owner already authors it, in Unity, through the painted height map (ADR 0014). A second authoring path for the same bytes is how they diverge. |
| **Water** | not in the package as an editable thing: `SeaTile` is one quad and the shader owns the surface (ADR 0010/0023/0027); `0` in the RLE means *the shader owns it*. |
| **Road polylines** | §6.3 — a node move relocates the furniture placed along it. Second-order rebuild, unspecified. |
| **Cliff lines** | an editor authoring artefact; the repo's cliffs are placed `CliffWallSurface` components and ship as entities. |
| **Collision** | derived, never authored — and the shapes carrying gameplay law (quay faces, dock zones, passages) are the builder's. |
| **Interiors, room layouts, fixtures** | the shop kit's `roomKinds` / `fixtures` / `doorPairs` are a real option surface, but interiors are a separate view the tool does not compose today. |
| **NPCs, routines, dialogue, quests** | world-content's, and not visual composition. |
| **New entity kinds** | v1 edits what the export contains. Adding a building the region does not have is a builder change with a backlog item. |
| **Anything not committed to an owner scene** | if it is not in the package, there is nothing to diff against. This is the addendum's *"anything owner-scene-uncommitted"*, and it is the same rule as §3.3 rule 1 seen from the other end. |
| **Regions other than Nine Mile Creek and St Peters** | those are the two the exporter ships, and §0.1's `NewScene` check has only been made for their builders. |

---

## 8. Owed, and honest about it

Nothing below is a hidden assumption; each is a thing that must exist before the round trip closes,
with whose it is.

1. ~~**An explicit `facing` field in the export.**~~ **Done.** The export now carries
   `facingIndex` — the baked step, read from `x-sprite.name`'s `_d<n>` — in the reference
   package's own slot between `pos` and `flipX`, with `x-facings` beside it giving the count
   **only where a sidecar declares one** (§2's "read the count, never assume it"), and
   `x-facingsSource` naming the file it was read from. `facing` is a compass *name* in the
   reference package, not an integer, and ships **null**: deriving a bearing from a step is the
   sign error `BuildingFacing` already records. §5's check 1 is now a field compare.
2. **A builder-table back-reference in the export** — and **not** under `x-origin`, ruled
   2026-08-20. `x-origin` ships as the builder's own **scene root**, whole and unmodified, which
   is what the editor's zone question actually needs: *may the owner move this?* Inferring that by
   parsing `x-path` misclassified any row whose key ends in a digit — 248 at Nine Mile Creek, 447
   at St Peters — which told the owner he could not move a building he can.

   File-and-row provenance is a **different fact** and must not share the key; one key carrying two
   meanings is how a reader ends up confidently wrong. Its home is `x-provenance`. Still owed as
   its own field. *Export lane; would let §3.3 rule 2 relax from a guard to a cross-check.*
3. **Override lists in the builders** for foliage and wharf pieces. §4.3. *world-content /
   tools-editor, with a backlog item — this is the gate on two of the owner's six asks.*
4. **The importer itself.** *tools-editor, in the build lane where Unity lives.*
5. **The tool's import path.** Their side, already diagnosed — without it the owner cannot open our
   package, and §3.1's "he exports, we diff" has no first step.
6. **A re-bake lane.** Zone (c) edits (§1.2) are honourable only through a bake. Until that is
   wired, size/style/colour-baked edits land in the dial and are reported as **owed**, which is
   honest but is not yet *done*. ⚠ **When it is built it must consult the recipe first** (§1.2 c):
   an `opt:`-bound axis already has its other cells baked and owes nothing, so a lane that marks
   every baked opt as owed would defer edits that are in fact live.

---

**Related:** [`scene-export-contract.md`](scene-export-contract.md) (the outbound half; §0's
unknown-key ruling is inherited whole) · [`scene-editor-review.md`](scene-editor-review.md) §9
(the import gate this contract answers) · [`reference/sample-scene.json`](reference/sample-scene.json)
(the editor's own bytes) · ADR 0019 (§0.1's expiry) · ADR 0021 (the V8 fence — why a bake is
editor-only) · ADR 0026 (pivot conventions) · ADR 0029 (colour runtime / structure baked — the seam
§1.1's cost column is measured against) · ADR 0006 (32 facings) · ADR 0022 (mesh hulls, continuous
yaw) · ADR 0003 (content is data).
