# `tools/rig-recipes` — the ledger's Node half

The file shape, why it exists and what is in it: **[`docs/art/rig-recipe-ledger.md`](../../docs/art/rig-recipe-ledger.md)**.
Read that first; this file is just how to run these scripts.

**Requires Node ≥ 16** — `node:`-prefixed imports and top-level await are the newest things used;
there are no dependencies, no package.json, and nothing to install. Verified on 22. `curl` is needed
only on a checkout *without* the LFS objects (see below); a full checkout never shells out.

No Unity. The rigs run in node's own V8 — the same engine ClearScript gives the in-editor baker
(ADR 0021) — and their sources are read and evaluated **verbatim**.

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

```bash
# ⭐ THE ROD IS ONE ROD. Renders every transition's frame-pair (hold→cast, cast→hold, hold→ground,
#   hold→stow-V, hold→stow-H) at all 8 facings × 3 tiers and measures the rod's pivot, blank length,
#   rendered extent, on-screen angle, yaw and which hand holds it. Fails if any seam drifts past
#   tolerance — the owner's law for every tool: no teleport, no hand change without an animated
#   hand-over, no size change, no orientation change across any transition.
node tools/rig-recipes/rod-continuity.mjs
node tools/rig-recipes/rod-continuity.mjs --tier deep      # one tier
node tools/rig-recipes/rod-continuity.mjs --json           # the measurements, machine-readable

# Regenerate the rod-mount handoff sidecar from the rigs (--check proves the committed file matches).
node tools/rig-recipes/fisher-rod-mount.mjs
node tools/rig-recipes/fisher-rod-mount.mjs --check
```

```bash
# ⭐ THE CUTAWAY IMPORT, RE-PROVEN IN OUR TREE. For the batch-1 hulls (lobster, trawler, packet):
#   geometry() publishes every walkable level with a ceiling or an EXPLICIT open · both shared-sole
#   ties broken in data · every face carries lv · nothing moved off the kit-bundled pass 2 (face
#   stream field by field, every published anchor and table, 0 differing pixels) · and the lobster,
#   which is a three-way MERGE, still carries BOTH the 12-scheme paint kit and every pass-3 export.
node tools/rig-recipes/cutaway-intake.mjs
node tools/rig-recipes/cutaway-intake.mjs --facings 8      # the full turntable, not four
node tools/rig-recipes/cutaway-intake.mjs --hull lobster   # one hull
node tools/rig-recipes/cutaway-intake.mjs --json           # the measurements, machine-readable
```

```bash
# ⭐ THE REACH IS THE RIG'S. Re-renders every committed reach sheet (and the two runs the 6.6 drop
#   completed) from the rig sources and byte-compares against the committed PNG — the character
#   sheets' half of the #654 standard, which the recipe ledger does not cover. Also pins the two
#   halves of the hand-over to each other: REST_FRAMES, RELEASE_AT, how many frames the tool is
#   still in hand, and the grip rise are stated by BOTH the rod rig and the character rig and must
#   agree. Fails if any sheet drifts a pixel or either rig moves a number.
node tools/rig-recipes/reach-continuity.mjs
node tools/rig-recipes/reach-continuity.mjs --all        # every iso character sheet, both cells
node tools/rig-recipes/reach-continuity.mjs --json       # the measurements, machine-readable
```

> `restLift()` is REPORTED beside the reach clip's lifts and never asserted against them. The rod's
> number is how far a settled rod holds its GRIP above whatever it rests on; the character's is that
> surface's own HEIGHT. They meet at the ground, where the surface is the floor and both rigs say
> 0.095 m — that one IS asserted.

The in-editor twin of the continuity check is `RodContinuityTests`; both measure the same things off
the same V8, and neither restates a number the rigs own.

All of these exit non-zero on a refusal.

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
| `cutaway-intake.mjs` | the batch-1 cutaway hulls: geometry contract, byte-discipline vs pass 2, and the lobster's paint × pass-3 merge proof |
| `bake-ledger.mjs` | derive + verify + (with `--write`) commit the ledger, per kit |
| `rod-continuity.mjs` | the rod is ONE rod: every hold ↔ cast ↔ rest seam, measured off the render |
| `reach-continuity.mjs` | the reach sheets re-render byte for byte, and the two rigs agree about the hand-over |
| `fisher-rod-mount.mjs` | regenerates (or `--check`s) the rod-mount handoff sidecar from the rigs |
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
