# Road / path / sidewalk kit v3 — import record

What was measured when this kit landed, and what a future reader must not have to re-derive.
The README beside this file is the art director's, verbatim. This file is the repo's side of it.

- **Rig:** `roadPathRig3.js`, in-repo verbatim, run unmodified (ADR 0021 §5).
  LF-normalised sha256 `d45e9ac657eb42daf5dc22f61068bf0ff1f0ee65152d6a4ca827a77ef0a5ee3c`
  (the file ships with LF endings, so the working-tree hash is the same number).
  Pinned in `RoadKitV3.RigSha256Lf` and asserted by `RoadKitV3Tests.Rig_IsTheCommittedFile_Unmodified`.
- **Baker:** `RoadKitSheetBaker` · **Slicer:** `RoadKitSlicer` · **Contract:** `road.contract.json`
- **Menus:** *Hidden Harbours ▸ Dev ▸ Bake Road Kit v3* (153), *Slice* (154), *adjacency proof sheets* (155)

---

## 1. v3 is a re-based family, not an extension of v1

v1 (`Art/Tilesets/Roads/RoadIso_<surface>_new_blob47.png`, 7 surfaces, 32×32 cells) drew its
height at **12 px/m**. v3 re-bases the family on **ZS = cos(40°)·32 ≈ 24.5 px/m** — the ruler every
building wall, utility prop and ShoreIso2 cliff band already uses. A sidewalk kerb goes from 1.5 px
to 4 px; the quay apron drops the 16 px face wharfKit's own quay cap shows; every face now seats on
a baked alpha contact shadow.

Two road families at different vertical scales in one village is a defect only the eye would ever
catch, so **v1 was retired by the same PR that landed v3** (owner's ruling). It was cheap because
v1 had **no consumer** — a grep of Data, Prefabs and Scenes found nothing referencing a road sprite.
Three code sites and one test changed: `ShorelineIsoCatalog` (road block removed),
`SpriteSheetSlicer` (7 `SheetSpec` rows removed), `ShorelineIsoKitSliceTests` (road dimension table
and the padding-cell test removed — the latter now lives in `RoadKitV3Tests`).

`docs/art/rigs/roadPathRig.js` stays in the repo as history.

---

## 2. The bake parameters were SOLVED, not read off the README

The README says the reference atlases are "`new` wear, lane profile, grass-soil spill, no markings".
That is a claim. The parameters below are what actually reproduces the eleven sheets, found by
sweeping the option space in the repo's own ClearScript V8 against the drop's PNGs:

| parameter | value | how it was pinned |
|---|---|---|
| `wear` | `new` | swept; `worn` moves 25 455 bytes |
| `piece` | `road` | swept; `apron` moves 72 512 bytes |
| `markings` | `[]` | README, and no marking pixels appear |
| `seed` | `7` | swept; 8 moves 92 889 bytes |
| `gx`, `gy` | **`2·col`, `2·row`** | solved per cell — see below |
| `axis` | **passed from `BLOB47[i].axis`** | deriving instead leaves **628 opaque px** wrong |
| `profile` | `lane` | **provably undetermined** — see below |
| `ground` | `grass` | **provably undetermined** — see below |

**The pattern step was the hard one.** A first sweep with every cell at `gx=gy=0` left ~106 k bytes
differing and only cell (0,0) close, which localised the fault to pattern space rather than to any
option. Solving `(gx, gy)` per cell then showed the step: cell 5 → (10,0), cell 24 → (0,4),
cell 27 → (6,4), cell 40 → (8,6), cell 42 → (12,6) — i.e. **two metres per cell, not one**, so a
reference sheet reads as 47 independent samples rather than one continuous road.

⚠️ The first per-cell solve searched `gx` only to 13 and therefore "failed" on every column ≥ 7,
which needs 14–22. A search range is part of the experiment; a null result inside too small a range
is not evidence.

**Two parameters cannot be determined by these sheets, and that is a fact about the rig rather than
a gap in the measurement** — both were checked in the rig source, not inferred from a tie:

- `PROFILE_Z` gives **`lane` and `road2` the same 0.02 m** lift, and the sheets carry no markings, so
  the two render identically. A future markings or wear variant *would* distinguish them.
- `SPILL` maps **`grass` and `dirt` to the same `{ramp:DIRT, i:2}`** entry.

The README's declared values (`lane`, `grass`) are what the bake uses.

⚠️ **`seed: 0` silently means 7.** The rig resolves it as `(opts.seed|0)||7`, so a caller who passes
zero — or omits it — gets seed 7. A placement pass cannot get an unseeded look by passing 0.

---

## 3. ⭐ The drop's reference PNGs are a LOSSY transcription of the rig

This is the finding that decided what ships.

Comparing the rig's raw buffer against the drop's `RoadIso3_dirt_new_blob47.png`:

- every **fully opaque** pixel matches **exactly**;
- **every one of the 381 partial-alpha pixels differs**, by 1–2 per channel;
- a **premultiply → unpremultiply round-trip explains 100 % of them** (381 / 381).

The reference sheets went out through a browser canvas, which stores premultiplied alpha and cannot
round-trip a partial-alpha pixel losslessly. This kit's contact shadows *are* partial alpha, by
design (ShoreIso2 v8 occlusion canon) — so importing the drop's PNGs would have shipped a quietly
degraded copy of the art. **The kit bakes from the rig instead**, which gives the lossless buffer.

The reference sheets remain the oracle, under a comparison that is **round-trip aware but tight**:
an opaque pixel must be exactly equal, and a partial-alpha pixel must round-trip to exactly the
reference byte. It is not a fuzz threshold and must not become one.

⚠️ The round-trip rounds **away from zero**, not banker's — `Mathf.RoundToInt` is the wrong helper
and would leave a handful of shadow pixels "differing" for reasons unrelated to the art.

**Verified end to end:** the PNGs Unity bakes decode **byte-identically** to the standalone V8
harness's buffers for all eleven surfaces (two independent hosts, one rig, same bytes), and all
eleven reproduce the drop with **0 opaque differences**.

### The sabotage check

An oracle that cannot fail proves nothing, and a round-trip comparison deserves the proof more than
an equality does. Each perturbation must **both** move pixels against the control **and** be caught —
the second half alone would pass for a parameter the rig quietly ignores:

| perturbation | bytes changed vs control | oracle |
|---|---|---|
| seed 7 → 8 | 92 889 | RED (29 189 px) |
| pattern step 2 → 1 | 107 145 | RED (34 064 px) |
| wear new → worn | 25 455 | RED (8 485 px) |
| piece road → apron | 72 512 | RED (20 224 px) |

Kept as `RoadKitBakeTests.Oracle_GoesRedForAPerturbedBake`, with `axis derived instead of passed`
substituted for the piece perturbation.

---

## 4. ⭐ Why every cell slices into TWO sprites

The kit's cell is **32 × 64**: headroom rows 0–9 (a boardwalk deck stands 0.35 m ≈ 9 px proud),
the ground square rows 10–41, and the **skirt** rows 42–63 carrying south drop faces, kerb faces and
contact shadows. Its composite rule is two-pass, painter N→S: *all* tops, then *all* skirts.

**No single `Tilemap` can express that at any `SortOrder`.** Painter N→S over whole 32×64 sprites
draws the southern cell *after* the northern one, and the southern cell's ground square lands
exactly on the northern cell's skirt: a cell's skirt occupies 16–38 px below its pivot, and the next
cell south occupies 16–48 px below that same pivot, so they overlap over the skirt's full 22 px.
Every kerb and drop face would be sliced off by its own neighbour — art that is perfect in isolation
and torn wherever height appears.

So a road is **two tilemaps sharing one cell grid** — a top layer and a skirt layer on adjacent
sorting orders — exactly as `StPetersShorePainter` already stacks ShoreGround/ShoreFringe/ShoreContact.
`RoadKitSlicer` emits 94 sprites per sheet (`…_<i>_top`, `…_<i>_skirt`), 1034 across the kit.

**Both sprites pin the same world point** — the ground-square centre, cell-local row 26 — which is
what keeps the two layers registered. The whole-cell pivot is therefore `(0.5, 0.59375)`
(ADR 0026's `(H − pivotY)/H`), and **the skirt's normalized pivot is 38/22 ≈ 1.727, above its own
rect**. That is legal, deliberate, and asserted by `RoadKitV3Tests.SkirtPivot_IsAboveItsOwnRect_OnPurpose`
so nobody clamps it back into range.

Note the pivot is the ground square's centre (row 26), **not** the cell's centre (row 32) — using the
cell centre would sink every road tile a quarter-metre into the terrain.

---

## 5. ⭐ Blob-47 correctness is combinatoric — hence the proof sheets

The 47 cases exist to **meet each other**. A tile drawn from the wrong atlas cell is a perfectly
good tile — right palette, right wear, right shadow — so it passes every check that looks at one
tile, and fails only where it touches its neighbours. Nothing per-tile can catch it.

The mapping from a raw 8-bit neighbourhood to an atlas index therefore **is never re-derived in C#**.
`road.contract.json` exports the 47 masks *from the rig* in atlas order, and `RoadKitContract` looks
the answer up. The single piece of the rig's maths that is restated — `canon()`, which clears each
diagonal bit whose two cardinals are not both set — is pinned against the rig over **all 256 inputs**
by `RoadKitBakeTests.Canon_AgreesWithTheRig_ForAll256Neighbourhoods`.

Measured while solving: `BLOB47[i].con` agrees with `BLOB47[i].mask` for all 47 entries on the bit
order `n=1 e=2 s=4 w=8`, and all 256 neighbourhoods resolve into the 47.

Two sheets are rendered, because they fail differently:

- **`maskgrid_<surface>.png`** — each of the 47 drawn with exactly the neighbours its contract entry
  declares. Exhaustive and rigorous: a drifted index shows as roadway pointing at bare ground.
- **`network_<surface>.png`** — a hand-drawn village layout. Catches what the grid cannot: whether
  the set reads as a road when laid continuously.

⚠️ **The network exercises 29 of the 47, not all of them.** `RoadKitProofSheet.CoverageReport()`
names the ones it misses rather than letting the sheet imply full coverage. Pushing it to 47/47 would
mean drawing shapes no village has, making it a worse copy of the mask grid — which already covers
all 47 by construction. The two sheets have different jobs.

---

## 6. Standing rules this kit inherits

- **ZERO water** (ADR 0010/0012/0023). The shader owns the waterline, foam and swash; there is no wet
  edge to line up and none is baked. Never author one against these tiles — butt land straight at
  shader water.
- **Slices are named by geometry**, `<stem>_<index>_<top|skirt>`. What index 23 *depicts* is resolved
  in `RoadKitV3`/the contract, so a wrong claim is a one-line fix and not a re-slice.
- **No `RigCatalog` entry.** That struct is built around `AzimuthConvention` for 8-direction
  turntables, and a road tile has no heading — its atlas axes are neighbour-mask × nothing. Same call
  and same reason as the shore-plant and grass-library kits.
- **Import cap is explicit** (`maxTextureSize = 512`) even though the sheet is 384×256: a silent
  downscale between the cap and 4096 keeps the sprite count correct while halving every pivot, and an
  explicit cap makes the intent reviewable.

---

## 7. Reproducing any of this

The standalone V8 harness (≈2 s per full sweep, versus a 20-minute Unity batch) is the tool that
solved the parameters; the recipe is in the `run-rigs-in-standalone-v8-harness` note. The shipped
sheets must still come from the Unity baker, because slicing, import settings and metas live there —
and the two were proven byte-identical, which is what makes the fast loop trustworthy.
