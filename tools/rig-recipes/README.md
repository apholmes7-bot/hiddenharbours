# `tools/rig-recipes` — the ledger's Node half

The file shape, why it exists and what is in it: **[`docs/art/rig-recipe-ledger.md`](../../docs/art/rig-recipe-ledger.md)**.
Read that first; this file is just how to run these two scripts.

Node 22+, no dependencies, no Unity. The rigs run in node's own V8 — the same engine ClearScript
gives the in-editor baker (ADR 0021) — and their sources are read and evaluated **verbatim**.

```bash
# ⭐ THE PROOF. Reads the committed *.recipe.json files and nothing else, runs the rigs they name,
#    reassembles each sheet at the recorded crop, byte-compares against the committed PNG.
node tools/rig-recipes/verify-ledger.mjs
node tools/rig-recipes/verify-ledger.mjs --verbose Assets/_Project/Art/Sprites/Yard

# Derive the ledger from the bakers' own enumeration. Writes NOTHING without --write, and writes a
# kit only when every one of its sheets reproduces byte for byte.
node tools/rig-recipes/bake-ledger.mjs
node tools/rig-recipes/bake-ledger.mjs --kit camper --verbose --write
```

```bash
# A THIRD check, needing NEITHER the rigs nor the sheet pixels: hold each recipe to the slicer's own
# sprite rects in the committed <stem>.png.meta — cell size, sheet span, cell count, ADR 0026 pivot.
node tools/rig-recipes/check-slices.mjs

# Where each kit's facts were scraped from — path:line, plus the cited line itself.
node tools/rig-recipes/bake-ledger.mjs --provenance
```

All three exit non-zero on a refusal.

**None of these scripts rebakes.** No PNG is written, opened for writing, or touched: the prop-mesh bake is
not byte-deterministic, and a rebake would dirty sheets this lane never looked at.

## Git LFS

Sprite sheets are LFS-tracked. On a full checkout (`git lfs pull` has run) nothing special happens —
the file on disk *is* the PNG. On a container that has not, the file is a ~130-byte pointer, and
comparing a render against a pointer would "fail" every sheet for a reason that has nothing to do
with the recipe. `lib/lfs.mjs` resolves those through the repo's own LFS endpoint (needs `GH_TOKEN`)
into a cache **outside** the working tree — `$HH_LFS_CACHE`, or the system temp dir.

This does not weaken the pixel proof: an LFS pointer's `oid sha256` *is* the sha256 of the content,
the pointer is committed text, and a fetched blob is re-hashed against it before it is cached — a
mismatch throws rather than being compared. `check-slices.mjs` needs none of this.

## Layout

| file | what it is |
|---|---|
| `verify-ledger.mjs` | the proof: committed recipe → rigs → pixels → compare |
| `check-slices.mjs` | recipe vs the slicer's own rects in `<stem>.png.meta` — no rig, no LFS |
| `bake-ledger.mjs` | derive + verify + (with `--write`) commit the ledger, per kit |
| `lib/recipe.mjs` | the recipe shape: canonical serialisation, axis expansion, reassembly |
| `lib/csharp.mjs` | reads the FACTS out of the bakers' own C# — build tables, `const` tunables, `RigCatalog` — and cites them by `path:line` |
| `lib/rigHost.mjs` | loads a rig and its prerequisite closure; `dirForCell`; the LF-normalised rig hash |
| `lib/png.mjs` | PNG decode (compare) and encode (diff images only) |
| `lib/lfs.mjs` | resolves an LFS pointer to real bytes |
| `kits/*.mjs` | one per kit: the enumeration, scraped from that kit's own source |

### Adding a kit

1. Write `kits/<id>.mjs` returning one **plan** per sheet — `{ stem, rigKey, cellCall, call, axes,
   columns, packRule }`, plus `cropGroup` when several sheets share one measured crop and `cropAxes`
   when the crop was unioned over an axis the sheet does not carry. Scrape the numbers from the C#;
   do not retype them beside it.
2. Add the id to `KITS` in `bake-ledger.mjs`.
3. `node tools/rig-recipes/bake-ledger.mjs --kit <id> --verbose` until every sheet reproduces. **A
   sheet that will not reproduce is a recipe that is wrong** — the compare is the acceptance, not a
   formality.
4. Add a `provenance()` to the kit module citing every fact with `cite`/`citeLine`, and check
   `--provenance` prints the lines you meant.
5. Wire the matching C# baker to call `RigRecipe.Write` so the kit stays in the ledger after its next
   bake, then `--write`.

> ⚠️ The scrapers **throw** on anything they cannot parse. That is deliberate: a silently empty build
> table would write a short ledger and report success, which is this repo's most-repeated failure.
