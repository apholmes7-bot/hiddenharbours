# The bake recipe ledger — every baked sheet says what baked it

**Status:** in effect for the kits listed in §5. **Owner:** tools-editor. **Related:**
[ADR 0021](../adr/0021-in-engine-js-rig-baking.md) (the rigs run in the editor) ·
[ADR 0026](../adr/0026-rig-pivot-conventions.md) (pivots) ·
[`tools/scene-writeback-contract.md`](../tools/scene-writeback-contract.md) §6.2 (the consumer) ·
[`tools/scene-editor-review.md`](../tools/scene-editor-review.md) §6.1 (the finding this answers).

---

## 1. The problem, in one paragraph

Every sheet under `Assets/_Project/Art` is the output of a rig call — a rig, a function, a set of
`dir` values, and an `opts` dict per variant axis. **None of that survived the bake.** The
scene-export packages emit a `rig` + `call` block for every placement they resolve and have to leave
`call.opts` **empty**, with an honest `x-optsNote`, because the option axes that produced each baked
cell were never recorded. The rigs resolve an unknown or missing key as a *silent fallback* — no
throw, no warning, a plausible different picture — so guessing is strictly worse than defaulting.
The owner's scene editor therefore draws every structure in its default variant.

The axes were never lost. They are **in the baker source**: every baker under
`Assets/_Project/Code/Tools/Editor/RigBaking` enumerates its variants in code as it bakes. This
ledger writes them down.

---

## 2. The file

A recipe is a **new sibling file**, `<stem>.recipe.json`, beside `<stem>.png`.

> ⚠️ **Never an addition to an existing sidecar.** Several shipped sidecars are byte-pinned by tests
> (the sidecar-hash law), and adding a key to one is how a pin breaks three suites away. The
> separation is not tidiness — it is the reason this could land at all.

```jsonc
{
  "schema": "hiddenharbours.rig-recipe/1",
  "sheet": "bulk_s10k_gas.png",          // always the recipe's own sibling
  "kit": "fuel-storage",
  "baker": "FuelSheetBaker",

  "rig": {
    "key": "fuel",                       // RigCatalog key
    "global": "FuelIso",                 // the global the rig installs
    "source": "docs/art/rigs/fuel-storage-kit/fuelRig.js",
    "sha256": "811361ff…",               // LF-NORMALISED. See §2.1.
    "convention": "Clockwise",           // what DirForCell was handed
    "prerequisites": [                   // the catalog's TRANSITIVE closure, in load order
      { "key": "deckIsoSolid", "global": "IsoSolid",
        "source": "docs/art/rigs/deck-loop-kit/Art/isoSolid.js", "sha256": "f7fc9db5…" }
    ]
  },

  "call": {
    "fn": "render",
    "args": ["bulk", "$dir", "$opts"],   // a TEMPLATE — see §2.2
    "opts": { "size": "s10k", "fuel": "gas", "fill": 0, "wear": "working" }
  },

  "grid": {
    "columns": 4, "rows": 5, "order": "rowMajor",
    "axes": [                            // ODOMETER, first axis FASTEST — see §2.3
      { "name": "facing", "bind": "dir",      "values": [0, 2, 4, 6] },
      { "name": "fill",   "bind": "opt:fill", "values": [0, 0.25, 0.5, 0.75, 1] }
    ]
  },

  "pack": {                              // the MEASUREMENTS the bake made — see §2.4
    "rule": "pivotUnionCrop",
    "nativeW": 139, "nativeH": 133, "nativePivotX": 69, "nativePivotY": 87,
    "cropX": 2, "cropY": 2,
    "cellW": 134, "cellH": 129, "pivotX": 67, "pivotY": 85,
    "sheetW": 536, "sheetH": 645
  },

  "sheetSha256": "f3accd76…"             // what this recipe claims to produce
}
```

The serialisation is **`JSON.stringify(recipe, null, 2)` plus a trailing newline**, with the key
order above, and the file is pinned to LF in `.gitattributes`. That is not cosmetic: there are two
writers — `RigRecipe` in the editor and `tools/rig-recipes/lib/recipe.mjs` in node — and
`RigRecipeTests.EveryCommittedRecipe_ReSerialisesByteIdentically` re-serialises every committed file
through the C# writer and compares bytes. Two writers that agreed on content but differed on
whitespace would churn the whole ledger on alternate runs and make a byte-compare meaningless.

### 2.1 `sha256` — which bytes drew this

The scene-editor review's headline finding was that **nothing in an export recorded which rig bytes
produced it**: 35 of the 51 rigs inlined in the owner's editor were byte-identical to this repo's,
15 were not, and none of those 15 matched any commit in the repo's history. A recipe that cannot be
traced to a rig source is a guess, so the source is named *and* hashed.

⚠️ **LF-normalised**, per the repo's existing convention (`deckIsoSolid` is `f7fc9db5…`). `core.autocrlf`
is on in some checkouts, and a raw hash of the working tree reports a drift that does not exist.

`sheetSha256` is the sheet's own bytes. Sprite sheets are Git-LFS tracked, and an LFS pointer's
`oid sha256` **is** the sha256 of the content — so a checkout without the objects can still tell
whether a sheet has moved under its recipe.

### 2.2 `call.args` — a template, not a call

`"$dir"` stands where the facing argument goes and `"$opts"` where the options object goes. That is
what carries the three call shapes this repo's rigs actually have, without a second table mapping
family names to argument orders:

| shape | who | example |
|---|---|---|
| `render(key, dir, opts)` | most families | `["bulk", "$dir", "$opts"]` |
| `render(dir, opts)` | a family that draws exactly one thing (camper, `fishTray2`) | `["$dir", "$opts"]` |
| `render(key, opts)` | a family with no facing axis (`trapFauna`) | `["urchin", "$opts"]` |

⚠️ Getting this wrong does **not** throw. The rigs compute `dir·π/4`, so a key string arriving where
`dir` belongs makes every projected vertex `NaN` and `render` returns a fully transparent cell in
silence.

**`call.opts` is the editable surface** the writeback contract (§6.2) is computed in. Its keys are
**rig keys** — there is no second vocabulary to drift.

### 2.3 `grid` — one placement rule

The axes are an **odometer and the first axis is the fastest**. Cell index `i = Σ (index_a × stride_a)`;
cell `i` lands at `col = i % columns`, `row = ⌊i / columns⌋`. Every baker in the repo packs that way:

- fuel storage — `[facing, fill]`, `columns = facings` → columns are facings, rows are fills;
- camper — `[swing, facing]`, `columns = frames` → columns are the swing axis, rows are facings;
- gas station — `[facing]`, `columns` solved per sheet → one facing axis wrapping into rows;
- iso-prop / yard / nav-buoy — `[facing]`, one row (or the contract's own plan).

`bind` is one of `dir`, `opt:<key>` or `arg:<n>`.

### 2.4 `pack` — recorded, not re-derived

⚠️ **The crop rect is recorded on purpose.** It makes a verifier a *reassembler* rather than a second
implementation of the crop rule, so a recipe whose crop is wrong **fails the pixel compare** instead
of quietly agreeing with itself.

`rule` names how the crop was arrived at; it is documentation, not behaviour.

| rule | meaning |
|---|---|
| `pivotUnionCrop` | `IsoPropSheetBaker.MeasureCell` — the pivot-**inclusive** union of the ink bbox across every cell, **seeded at the pivot**. The seeding is the rule, not a detail: a wall-hung piece whose ink stops above its own pivot would otherwise crop its ground contact outside its own cell, which does not fail loudly — it just stands in the wrong place. |
| `pivotUnionCropOverWear` | the nav-buoy kit's, which unions over all three wear states and ships only `working`. A verifier that re-derived the crop from the shipped sheet's own ink would get a tighter rect than the one on disk. |

Two invariants a reader may rely on, both asserted in `RigRecipeTests`:
`sheetW = columns × cellW` (likewise H), and `cropX = nativePivotX − pivotX` (likewise Y).

---

## 3. The two halves

**Going forward — the bakers write it.** Each baker calls `RigRecipe.Write(assetPath, …)` right after
it writes its PNG. The options live in **one dict** per baker and the JS literal handed to the rig is
*derived* from it (`RigRecipe.Js`), so a baker cannot render one thing and record another. Where an
options table sits in an assembly that cannot see `RigRecipe` — `YardKit` is in `HiddenHarbours.Art.Editor`,
and the reference runs the other way — the two spellings are compared on every sheet and the bake
**refuses** on a difference (`YardSheetBaker.AssertOptionsAgree`).

**Backfill — re-derived from the baker's own code.** `tools/rig-recipes/` reads the enumeration out of
the C# source (the build tables, the `const` tunables, `RigCatalog`, `IsoPackContract.Registry`,
`IsoPropSheetBaker.Shapes`), runs the rigs, and writes the ledger for sheets already shipped —
**without rebaking anything**. No PNG is written, opened for writing, or touched: the prop-mesh bake
is not byte-deterministic and a rebake would dirty sheets this lane never looked at.

---

## 4. ⭐ What makes a recipe true rather than plausible

```
node tools/rig-recipes/verify-ledger.mjs            # every committed recipe
node tools/rig-recipes/verify-ledger.mjs --verbose Assets/_Project/Art/Sprites/Yard
```

It reads the **committed** `.recipe.json` files and nothing else, runs the rigs they name in a
standalone V8 (node — no Unity, no ClearScript), reassembles each sheet at the recorded crop, and
byte-compares against the committed PNG. **A recipe that reproduces its sheet IS the recipe.** One
that does not is a lie about how the art was made, and the verifier exits non-zero.

It also audits what a JSON file can be wrong about on its own: the canonical serialisation, the rig
hashes against the working tree, the prerequisite closure against `RigCatalog`, and the sheet hash.

> **What is compared is RGBA pixel bytes, not PNG container bytes.** Unity's `EncodeToPNG` produced
> the committed containers, and nothing outside Unity reproduces its zlib window and filter choices —
> nor should it. What a recipe has to reproduce is the **image**.

**Writing the ledger** goes through the same compare, per kit:

```
node tools/rig-recipes/bake-ledger.mjs                        # verify all, write nothing
node tools/rig-recipes/bake-ledger.mjs --kit camper --write   # write iff every sheet reproduces
```

A kit where one sheet disagrees is **refused whole** and named in the report. A ledger that is right
about 83 of 84 sheets is worse than none, because nothing downstream can tell which one is the lie.

On a checkout without the LFS objects, the sheets are ~130-byte pointers and every compare would
"fail" for a reason that has nothing to do with the recipe; the tools resolve those through the
repo's own LFS endpoint into a cache outside the working tree (`git lfs pull` makes it a no-op).

---

## 5. What is in the ledger — and what is not

**In (298 sheets, 6 kits), each proved sheet-by-sheet by §4:**

| kit | sheets | baker | what the axes are |
|---|---:|---|---|
| `fuel-storage` | 84 | `FuelSheetBaker` | facings × fills, over 8 vessels × 21 sizes × 4 grades |
| `gas-station` | 23 | `GasStationSheetBaker` | facings (folded to 4 on the 180°-symmetric pieces), 14 options |
| `iso-prop` | 127 | `IsoPropSheetBaker` | facings; `wharfDecor` 61 · `utilityIso` 42 · `deckGear` 5 · `trap` 4 · `buoyIso` 8 · `fishTray2` 1 · `trapFauna` 6 |
| `nav-buoy` | 50 | `NavBuoySheetBaker` | facings, cell measured wear-invariant |
| `yard` | 10 | `YardSheetBaker` | facings, per-piece dressing |
| `camper` | 4 | `CamperSheetBaker` | swing × facings, one crop shared by both roles of a variant |

**Rig-baked, recipe-able, NOT YET written.** Nothing here is blocked — each is the same work as a kit
above, plus whatever its own row names:

| baker | sheets | what it needs beyond today's schema |
|---|---:|---|
| `WharfIsoSheetBaker` (`wharfIso`, `shipyardIso`) | 42 | a second pack rule: these rigs size their own buffer per bake and return `{data,w,h,px,py}`, so the crop is a **buffer** union with a fractional pivot, not an ink union |
| `ShoreFindsSheetBaker` | 108 | an analytic cell rule and non-facing axes (lie angles × variants × weathering state) |
| `ShorePlantBaker` · `TreeRigBaker` · `ShrubBaker` · `GrassLibraryBaker` · flowers | ~200 | **multi-channel sheets** — one `render()` produces albedo + `_light` + `_calendar`, so one call maps to 2–3 files and `sheet` is singular today |
| `BuildingRigBaker` · `ShopLevelBaker` · `ShopFixtureSheetBaker` · `InteriorRigBaker` | 23 | nothing structural; the building kit's opts are `Object.assign` over a rig PRESET, which the `opts` dict has to flatten rather than name |
| `CharacterRigBaker` · `BoatInteriorSheetBaker` · `FishingKitBaker` · `CatchStorageBaker` · `RigBaker` (hull turntables) · `RoadKitSheetBaker` · `RockIsoBaker` · `NotebookKitBaker` · `BubbleKitBaker` · `HarbourTypeBaker` · `DriftWeedKitBuilder` | the rest | per-baker work; several have clip/animation axes (`anim`, `frame`) the schema carries fine as `opt:` binds |

**Out of scope — no rig at all.** These are hand-drawn or painted imports; there is no call to record,
and inventing one would be exactly the guess this ledger exists to replace:

- `Assets/_Project/Art/Tilesets/Wharf` — **`WharfAtlas.png` / `WharfOverlays.png`**, the hand-drawn
  wharf tile kit ([`docs/art/wharf-tile-kit/README.md`](wharf-tile-kit/README.md));
- `Assets/_Project/Art/Tilesets/ShorelineIso`, `ShorelineIso2`, `Tiles`, `Palettes`, `Water` and the
  loose ground tiles (`Dirt`, `Grass`, `Rock`, `Sand`, `Foam`, `Shore*`) — imported tilesets;
- `Assets/_Project/Art/Terrain/**` — painted height and splat maps, authored in Unity (ADR 0014);
- `Assets/_Project/Art/Textures/Water/**` — water textures the shader owns (ADR 0010/0023);
- `Assets/_Project/Art/Portraits/**`, `Assets/_Project/Art/UI/Roster/**` — hand art;
- `Assets/_Project/Art/VFX/**` — wake and bow-spray strips;
- the pre-rig placeholder sheets still sitting at the top of `Sprites/`, `Sprites/Fish`,
  `Sprites/Gear`, `Sprites/Shore`, `Sprites/Shore/Finds`, `Sprites/Buildings`, `Art/Boats`,
  `Art/Characters`, `Art/Fishing` and `Art/Foliage` — greybox art from before the rigs, which a
  re-bake replaces rather than a recipe explains.

---

## 6. For the scene-export lane (#588)

The exporter's follow-up is a **lookup, not a derivation**. For an entity resolved to sheet stem
`S` in folder `F`:

1. read `F/S.recipe.json` — absent means the sheet is not in the ledger (§5), and the honest
   `x-optsNote` stands for that entity;
2. `rig` → the `rig` block, which now also names the **source and its sha256**, so a package can
   state which rig bytes its editor needs (§6.1 of the review) instead of hoping;
3. `call` → `{ fn, opts }` directly. The facing index already encoded in `x-sprite.name`'s `_d<n>`
   suffix is the index into the `bind: "dir"` axis's `values`, so `opts` for one drawn cell is the
   base `opts` with each axis's value at that cell's odometer position applied (§2.3);
4. `pack.pivotX/pivotY` and `pack.cellW/cellH` are the same numbers the slicer used, if the
   exporter wants to cross-check its own.

`RigRecipe.Read(sheetAssetPath)` returns a parsed recipe (or `null`) on the C# side; the parser is
**strict** — an unknown key throws rather than being ignored, because silently dropping one is how a
consumer draws the wrong variant while believing it read the whole file.

Nothing in `tools/scene-export` is touched by this lane.
