# Imported art — "Hidden Harbours Assets" sheet

> Final pixel-art for the Coddle Cove vertical slice, imported from the Claude Design project
> *Hidden Harbours Assets* (`f9a59e08-…`). The sheet is authored to our exact standard —
> **32 px = 1 m · no anti-aliasing · transparent PNG** — so it drops straight into the VS-23 lock.
> On first import, `Editor/ArtImportPipeline.cs` stamps PPU 32 · Point · Uncompressed · mips off ·
> pivots automatically; Unity generates the `.meta` files then (commit those after opening the project).

## What landed

| Sprite | File | px (W×H) | At PPU 32 | Wrap / pivot | In-game use |
|---|---|---|---|---|---|
| Dory hull | `Boats/Dory.png` | 64×144 | 2 m × **4.5 m** (bow-up) | Clamp · centre | The Dory (T0) hull — `VS-26` |
| Sea tile | `Tilesets/Water/SeaTile.png` | 64×64 | 2 m × 2 m, **seamless** | **Repeat** · centre | tiling water surface — `VS-24` |
| Fisher | `Characters/FisherSheet.png` | 96×256 | sheet of 32×64 cells | Clamp · feet | player/NPC sprite — `VS-25` |
| Cod | `Sprites/Fish/Cod.png` | 48×32 | 1.5 m × 1 m | Clamp · centre | catch icon — `VS-26` |
| Haddock | `Sprites/Fish/Haddock.png` | 48×32 | — | Clamp · centre | catch icon |
| Mackerel | `Sprites/Fish/Mackerel.png` | 48×32 | — | Clamp · centre | catch icon |
| Lobster | `Sprites/Fish/Lobster.png` | 48×32 | — | Clamp · centre | catch icon |

The sea tile lives under `Tilesets/Water/` so the import lock sets **Repeat** wrap (it's seamless);
everything else is Clamp. The dory's 144 px length is the canon 4.5 m — true metric scale holds.

## Fisher sheet — slicing spec (for whoever wires the player animation)

`FisherSheet.png` is a **3 × 4 grid of 32 × 64 cells** (Sprite Mode: **Multiple**, Grid By Cell Size
**32 × 64**). The design's own viewer defines the layout:

- **Rows = facing:** row 0 = **Down** (toward camera), row 1 = **Up**, row 2 = **Left**, row 3 = **Right**.
- **Columns = frames:** col 0 = idle/neutral, cols 1 & 2 = the two walk frames.
- **Walk cycle:** `[1, 0, 2, 0]` at ~230 ms/frame (step-left → neutral → step-right → neutral).

This matches the bible §3.4 four-facing character convention (right is its own art here, not mirrored).
Slice it in the Sprite Editor (or a follow-up Art tool) before building the Animator/Sprite Library.

## Next steps to make it visible in-game (cross-lane — not done here)

These need Unity to resolve the imported sprite references, and several sit in other lanes:
- **Greybox swap:** point the Dory's `SpriteRenderer` at `Boats/Dory.png` and the water at the sea
  tile instead of the colour blocks (the placeholder→final swap). *(art-pipeline / app)*
- **Fish data:** assign each `Sprites/Fish/*.png` to the matching `FishSpeciesDef` sprite ref. *(economy-sim / world-content)*
- **Player sprite:** slice `FisherSheet.png` and build the 4-direction Animator. *(gameplay-systems / world-content)*

Source design (read-only reference, not shipped): `Hidden Harbours Assets.dc.html`.

---

## Batch 2 — full Coddle Cove slice art set + wiring owners

Imported the rest of the slice art (same 32 px = 1 m / no-AA standard — the import lock auto-applies on
first Unity import). Existing canonical files were kept (`Boats/Dory.png`, `Characters/FisherSheet.png`,
`Tilesets/Water/SeaTile.png`); identical re-exports were skipped.

**art-pipeline imports the art and builds the tile/shoreline assets; everything below in *italics* is
another lane's job to wire into the game:**

| Asset | Files | Wire-in owner(s) | Work |
|---|---|---|---|
| Punt (T1) | `Boats/Punt.png` (64×192) | *gameplay-systems* + *economy-sim* | Punt `BoatHullDef` + hull sprite; Shipwright buy flow (VS-08/16/26) |
| Player sprite | `Characters/FisherSheet.png` | *gameplay-systems* | slice 32×64 → 4-dir Animator, drive from movement (VS-25) |
| NPCs | `Characters/{Ned,Ginny,Neighbour}.png` (96×256) | *world-content* | place in the cove, dialogue, Ned onboarding (VS-21/25) |
| Fish (7) | `Sprites/Fish/*.png` (48×32) | *economy-sim* / *world-content* + *ui-ux* | assign sprite to each `FishSpeciesDef`; show in catch + sell UI (VS-11/14/18) |
| Cottage day/night | `Sprites/Buildings/Cottage{,Night}.png` (160×192) | *world-content* + art-pipeline | place + sleep/save interactable; day↔night sprite swap (VS-20/21/24) |
| Terrain tiles | `Tilesets/{Sand,Rock,Grass,Dirt,WharfDeck}.png` (32²) | art-pipeline → *world-content* | make Rule/Tile assets; paint the cove tilemap (VS-24/20) |
| Shoreline + sea | `Tilesets/{ShoreEdge,ShoreCornerInner,ShoreCornerOuter,Foam}.png` + `Water/SeaTile.png` | art-pipeline (tide-aware autotile) + *gameplay-systems* (WaterLevel) | the moving shoreline — headline P1 visual (VS-24) |
| Fishing spot | `Sprites/FishingSpot.png` (32²) | *gameplay-systems* + *world-content* | the cast/fish interactable anchor (VS-13/20) |
| Decor props | `Sprites/{LobsterBuoy,LobsterTrap,Barrel,Crate,WharfPost}.png` | *world-content* | wharf/cove decor placement (VS-20/24) |

---

## VS-24 — tile assets + the moving-shoreline & day/night rendering (art-pipeline)

The rendering layer that drives the imported tiles/sprites. **art-pipeline owns these; world-content
paints + places.**

- **Tile assets** — run **Hidden Harbours ▸ Art ▸ Build Coddle Cove Tiles**
  (`Art/Editor/TileAssetBuilder.cs`) to generate a plain `Tile` per terrain sprite and an autotiling
  `Shoreline` `RuleTile` (edge/corner-by-neighbour) under `Tilesets/Tiles/`. ***world-content* then paints
  the Coddle Cove tilemap with these.** (The Shoreline rule orientations are a sensible start — refine in
  the Tile Palette if a sprite faces the wrong way.)
- **Tide-aware moving shoreline** (the former headline P1 visual) — **retired.** `Code/Art/TideShoreline.cs`
  was a smooth transform-slide of a water plane by tide height, wired into no builder or scene; it has been
  removed (ADR 0012 §5, follow-up (a)). The live tide-aware shoreline is the height-map water shader
  (`Art/Shaders/HiddenHarboursWater.shader` + `Code/Art/WaterSurface.cs`) plus the `TidalFlatVisual`
  tide-reveal (`Code/Environment/TidalFlatVisual.cs`) — use that for any tide-gated coast.
- **Cottage day↔night** — `Code/Art/CottageDayNight.cs`. Attach to the cottage `SpriteRenderer`, assign
  `Cottage.png` / `CottageNight.png`; it swaps on `GameServices.Clock.HourOfDay`. No new Core hook needed.

Both components read the sim through Core contracts only. EditMode tests for the pure mappings live in
`Assets/Tests/EditMode/Art/`.

---

## Batch 3 — HUD/UI, portraits & haul animation (for the tide/wind/time/HUD work)

art-pipeline owns the UI *look* (imported here); ***ui-ux* owns layout & behaviour** (`ux-and-mobile-controls.md`).
This is the set the HUD/tide/wind/time work needs.

| Group | Files (`Art/…`) | Wire-in owner(s) | Backlog |
|---|---|---|---|
| HUD instruments | `UI/TideGauge` (48×96), `UI/TideArrow{Up,Down}` (16²), `UI/WindCompass` (64²), `UI/Clock{Sun,Moon}` (24²), `UI/CoinIcon` (16²), `UI/HoldIcon` (24²) | *ui-ux* | VS-17 / VS-19 |
| Dialogue | `UI/DialoguePanel` (208×104), `UI/NamePlate` (92×28), `Portraits/{Ned,Ginny,Player}` (96²) | *ui-ux* + *world-content* | VS-21 |
| Fishing UI | `UI/TensionGauge` (64×40), `UI/LineHook` (16×28), `UI/FishOnSilhouette` (32×24) | *ui-ux* + *gameplay-systems* | VS-13 / VS-14 |
| Sell screen | `UI/SellChalkboard` (208×144), `UI/Button` (76×28) | *ui-ux* | VS-18 |
| Player haul anim | `Characters/PlayerHaul.png` (96×256, 3×4 of 32×64) | *gameplay-systems* | VS-14 / VS-25 |

UI sprites get the same PPU-32 / Point / no-compression lock on import; if a Canvas needs a different
reference PPU, *ui-ux* can override per-asset (the lock only stamps on first import). The haul sheet uses
the same 4-facing / 3-frame layout as `FisherSheet.png`.

---

## Batch 4 — minimal Greywick, the dory row anim & feel VFX

Closes the P2 (Greywick) and P3 (polish) art gaps. The Shipwright logic (VS-16) and HUD (VS-17) just
landed, so the Greywick buildings are timely.

| Group | Files (`Art/…`) | Wire-in owner(s) | Backlog |
|---|---|---|---|
| Greywick buildings | `Sprites/Buildings/{ShipwrightShed (256×224), FishBuyerStall (128×160), GreywickHouseRed (144×184), GreywickHouseTeal (160×176)}.png` | *world-content* (place in the Greywick scene) + *economy-sim* (Shipwright/buyer are built) | VS-22 / VS-16 |
| Dory row anim | `Boats/DoryRow.png` (384×144 = 6 frames of 64×144) | *gameplay-systems* | VS-26 / oars |
| Feel VFX | `VFX/{BoatWake (64×96), CatchSparkle (72×24, 3 frames), WindPennant (160×48, 4 frames)}.png` | *gameplay-systems* (boat wake) + art-pipeline (sparkle/pennant wiring) | VS-14 / VS-19 / VS-26 |

The three effect overlays were filed under `VFX/` (not `UI/`) — they're world/boat effects, not HUD
widgets. Excluded from the import: the design canvas's `gallery/ShoreDemo.png` preview and the `*.dc.html`
source files (not game assets).

---

## Import-meta status (stable GUIDs + slicing)

Every committed `.png` must have its `.meta` committed too — a meta-less PNG regenerates a new GUID + default
settings on a fresh clone/CI, breaking references and importing the sprite blurry/wrong-scale ([[commit-unity-metas-with-assets]]).

- **Batch-3 metas committed** — the 15 UI sprites, 3 portraits (+ `Portraits/`), and `PlayerHaul` carry the
  VS-23 lock (Sprite · PPU 32 · Point · Compression None · mips off).
- **Sheets are sliced** (ready for *gameplay-systems* — no slicing needed on their side):
  `Characters/FisherSheet.png` and `Characters/PlayerHaul.png` are `Multiple`, **12 frames** of 32×64
  (rows = facing down/up/left/right, cols = idle / walk-or-haul-1 / -2).
- **Batch-4 metas committed + sliced** — `Boats/DoryRow.png` and the three `VFX/` sheets carry the
  VS-23 lock (Sprite · PPU 32 · Point · Compression None · mips off) and are sliced to clean
  **full-cell grids** (rect = the whole cell, not trimmed — so the centre pivot is identical on every
  frame and the animation never jitters):
  - `DoryRow` — `Multiple`, **6×1 of 64×144** (the oar stroke; `DoryRow_0…5`).
  - `CatchSparkle` — `Multiple`, **3×1 of 24×24** (`CatchSparkle_0…2`).
  - `WindPennant` — `Multiple`, **4×1 of 40×48** — the strip *is* animated (4 evenly-spaced frames with
    transparent gaps), so it's sliced, not single (`WindPennant_0…3`).
  - `BoatWake` — single-frame, **1× 64×96** full-cell (`BoatWake_0`).
  Wiring the wake/oars into boat behaviour (ParticleSystem/Animator) is *gameplay-systems*' job — this
  lane just provides the sliced, correctly-imported assets.

---

## Batch 5 — the boat fleet (T2+ hulls + roster icons)

The bigger tiers beyond the Dory (T0) and Punt (T1) — the **P2 "Dory to Dynasty"** progression art —
plus a matching set of roster/fleet thumbnails. Imported headless (editor closed) so
`ArtImportPipeline` stamped the VS-23 lock on first import.

| Group | Files (`Art/…`) | Wire-in owner(s) | Backlog |
|---|---|---|---|
| Fleet hulls | `Boats/{CapeIslander (100×288), CoastalPacket (124×620), LobsterBoat (104×268), SideDragger (132×456), SternTrawler (144×576), Tanker (110×640)}.png` | *gameplay-systems* + *economy-sim* (one `BoatHullDef` per tier + hull sprite ref) | P2 fleet tiers |
| Roster icons | `UI/Roster/{CapeIslander, CoastalPacket, Dory, LobsterBoat, Punt, SideDragger, SternTrawler, Tanker}.png` | *ui-ux* (fleet/roster screen) | P2 fleet UI |

Each hull is a **single-frame** sprite (`Multiple` with one auto-trimmed sprite + centre pivot — the
same convention as `Boats/Dory.png` / `Boats/Punt.png`); they're individual hulls, not animation strips,
so no grid-slicing is needed. Roster icons get the standard PPU-32 / Point / no-compression lock; *ui-ux*
can override reference PPU per-asset if a Canvas needs it. Skipped as **identical re-exports**:
`Boats/{Dory, DoryRow, Punt}.png` (byte-for-byte matches of what's already committed). Building/Def
wiring (a `BoatHullDef` asset per hull, with stats + the sprite ref) is *gameplay-systems* / *economy-sim*'
job — this lane only provides the locked, imported sprites.

> **The whole fleet is SLICE PLACEHOLDER art** (spin-tolerant, near-plan hand-drawn hulls) — the Dory
> and Punt included, and these T2+ hulls + roster icons especially. Final boats are planned to come
> from an **M2 pre-rendered-3D → sprite-sheet bake** that replaces them via a sprite-ref swap (no
> rework, no placement shift — the pivots/footprints are pinned). See `docs/adr/0006-boat-art-pipeline.md`
> (*Proposed, deferred to M2*) and the boat art conventions in `docs/design/art-and-audio-bible.md` §3.5.1.
> Keep these assets (stable GUIDs, usable now); don't invest in hand-drawing per-heading boat art.

---

## Batch 6 — dory oar-rework rig (hull + oar + rower, layered)

The dory's rowing was a single baked 6-frame strip (`Boats/DoryRow.png`). To drive the **per-oar
differential hand-rowing feel** (the input→per-oar fwd/back/idle table — see the gameplay-systems
oar-rework), the oars need to move *independently of the hull*, so the art ships as three separate,
composited layers instead of one pre-baked strip. Imported headless (editor closed) so
`ArtImportPipeline` stamped the VS-23 lock (Sprite · PPU 32 · Point · Compression None · mips off) on
first import.

| Layer (back→front) | File | px (W×H) | At PPU 32 | Pivot | Role |
|---|---|---|---|---|---|
| 1 — base | `Boats/DoryHull.png` | 64×144 | 2 m × 4.5 m (bow-up) | **centre** | The oar-less dory hull. Same footprint/pivot as `Boats/Dory.png` — a drop-in hull base. |
| 2 — oars (×2) | `Boats/Oar.png` | 56×16 | 1.75 m × 0.5 m | **LeftCenter (handle/inboard end)** | One oar; the rig **mirrors it L/R** and **rotates each about its oarlock** to animate strokes. |
| 3 — rower | `Boats/DoryRower.png` | 26×28 | ~0.8 m × 0.9 m | **centre** | The rower figure that sits at the thwart, on top of the hull (hands meet the oar handles). |

**Intended composition (for the gameplay-systems rig — cross-lane, not wired here):**
- Stack the three as child `SpriteRenderer`s of the dory: hull (sorting back) → two oars → rower (front),
  so the rower covers the inboard handle ends and the looms/blades sweep out over the water.
- The hull's centre pivot is the boat's rotation/footprint anchor — identical to `Dory.png`, so the rig
  drops onto the existing dory placement with **no shift**.
- **Oars are one sprite, used twice:** instance it for port & starboard, `flipX` one of them, anchor each
  at its gunwale oarlock, and rotate each oar's transform to row. Because the two oars share the sprite,
  per-oar input (left stick vs right stick / A·D feathering) just drives two independent rotations.

**Oarlock / pivot note (important for whoever rigs the stroke):**
- `Oar.png` is drawn **handle-left → loom → blade-right** (trimmed content rect 55×13, handle knob at the
  left edge, wide blade at the right). Its sprite **pivot is set at the handle/inboard end** (`LeftCenter`,
  `{x:0, y:0.5}`) so rotating the SpriteRenderer directly swings the blade through an arc — the cheap path.
- The **true oarlock (fulcrum) is *not* the handle tip** — it sits roughly the **inboard third along the
  loom: ≈ x 18 of 55 px from the handle, i.e. normalized pivot ≈ {x: 0.33, y: 0.5}** of the trimmed
  sprite. A real stroke pivots here (handle swings inboard, blade swings outboard about this point).
- If the handle-tip swing looks off, **don't re-pivot the art** — parent `Oar.png` under an empty *oarlock
  pivot transform* placed at that ≈0.33 point and rotate the transform instead (more flexible: gameplay
  can tune the fulcrum and the inboard/outboard lever per boat without an art round-trip). The sprite's
  `LeftCenter` pivot and this documented oarlock offset support **either** approach.

`Boats/DoryRow.png` (the old 6-frame strip) is kept for now (stable GUID; usable as a fallback) but is
**superseded** by this layered rig for the oar-rework. Building the rowing rig/animation (oarlock
transforms, the per-oar rotation curves, sorting) is *gameplay-systems*' job — this lane provides the
locked, correctly-pivoted layers.

---

## Clamming kit — soft-shell clam catch + the dig loop art

The art for the soft-shell clam dig loop (clam-flat tide-pool digging). Seven sprites authored to the
locked standard (**32 px = 1 m · no anti-aliasing · transparent PNG**, palette/light-matched). Imported
**IMPORT-ONLY** — no wiring (downstream, pairs with the in-flight St Peters work; owners flagged below).

> **Metas are hand-authored, not Unity-generated.** Because we build headless (no Unity to auto-generate
> `.meta`s), each `.meta` here was written to clone the committed VS-23 import lock and adapt it — Sprite ·
> **PPU 32 · Point · Compression None · mips off · sRGB on · Clamp wrap**, with fresh, repo-collision-checked
> GUIDs / `internalID`s / `spriteID`s. `ArtImportPipeline` only stamps the lock when a `.meta` is *missing*,
> so these authored metas are authoritative and stable on a fresh clone/CI ([[commit-unity-metas-with-assets]]).
> Multi-sprite sheets are sliced to **clean full-cell rects** (rect = the whole cell, not alpha-trimmed) so
> every frame shares an identical pivot and the animation never jitters — same convention as `Boats/DoryRow.png`.

| Asset | File (`Art/…`) | px (W×H) | Sprite Mode / slice | Pivot · wrap | Wire-in owner(s) |
|---|---|---|---|---|---|
| Soft-shell clam | `Sprites/Fish/SoftShellClam.png` | 48×32 | Single (1 sub-sprite `SoftShellClam_0`) | centre · Clamp | *economy-sim* / *world-content* — assign to the clam `FishSpeciesDef.sprite` (catch + sell UI) |
| Clam hole | `Sprites/ClamHole.png` | 32×32 | Single (`ClamHole_0`) — the dig spot (two holes in sand) | centre · Clamp | *gameplay-systems* / *world-content* — the clam-spot visual (dig anchor) |
| Clam squirt | `Sprites/ClamSquirt.png` | 128×32 | **Multiple** — 4 horizontal 32×32 frames (`ClamSquirt_0…3`) | centre · Clamp | *gameplay-systems* / *world-content* — squirt anim on the clam-spot |
| Shovel | `Sprites/Gear/Shovel.png` | 32×32 | Single (`Shovel_0`) | centre · Clamp | *economy-sim* / *ui-ux* — gear icon (`GearOffer` / inventory UI) |
| Clam bucket | `Sprites/Gear/ClamBucket.png` | 32×32 | Single (`ClamBucket_0`) | centre · Clamp | *economy-sim* / *ui-ux* — gear icon |
| Rod | `Sprites/Gear/Rod.png` | 48×32 | Single (`Rod_0`) | centre · Clamp | *economy-sim* / *ui-ux* — gear icon |
| Fisher dig sheet | `Characters/FisherDig.png` | 128×256 | **Multiple** — 4 cols × 4 rows of 32×64 (16 sub-sprites) | **feet** (BottomCenter) · Clamp | *gameplay-systems* — the player dig Animator |

**`FisherDig.png` slicing (cloned from `Characters/FisherSheet.png`'s 32×64-cell layout, extended to 4 rows):**
- **Rows = facing**, in the **same order as `FisherSheet.png`**: row 0 = **Down**, row 1 = **Up**, row 2 = **Left**,
  row 3 = **Right** (row 0 = the top of the image; Unity's bottom-left rect origin puts Down at `y:192`, Right at `y:0`).
- **Columns = the dig beat:** col 0 = **Ready** → col 1 = **WindUp** → col 2 = **Plunge** → col 3 = **Scoop**.
- Sub-sprites are named `FisherDig_<Dir>_<Frame>` (e.g. `FisherDig_Down_Plunge`) so the Animator can address each
  frame by name. Feet pivot (`{0.5, 0}` per cell) matches `FisherSheet` so the dig sheet plants on the same ground
  grid with **no shift** — the dig animation can swap in over the walk sheet at the same transform.

**`ClamSquirt.png`** is a 4-frame horizontal flipbook (`ClamSquirt_0…3`, left→right, ~32×32 each) for the
spurt that marks a live clam under the sand — play it on the clam-spot when the player approaches/probes.

**WIRE-IN is downstream (NOT done here):** clam → `FishSpeciesDef.sprite` (*economy-sim* / *world-content*);
dig sheet → player dig Animator (*gameplay-systems*); gear icons → `GearOffer` / gear UI (*economy-sim* / *ui-ux*);
hole + squirt → the clam-spot visual (*gameplay-systems* / *world-content*). This lane only provides the locked,
correctly-sliced, correctly-pivoted sprites with stable GUIDs.

---

## Trees / environment decor — owner's free-to-use tree pack (IMPORT-ONLY)

A **seasonal tree sprite pack** for island/coast decor — banked here for **world-content to place as
scene decor** during scene-dressing / the art pass. Imported **IMPORT-ONLY: not wired into any
scene, builder, or prefab.** From the owner's drop `trees.zip` (37 individual 64×64 RGBA PNGs).

> **SOURCE / ATTRIBUTION (owner-provided).** Author **ranju**. Source-listing tags: **2D · 64×64 ·
> nature · Pixel Art · Top-Down · treeset**. **Status: Released.** The owner confirms the pack is
> **free-to-use** for the project. **AI Disclosure: AI-Assisted graphics** — flag this for surfacing
> in the game's own **credits / AI-disclosure at release** (we ship AI-assisted art and should say so).
> The **exact licence type and the required credit/attribution line are still per the source listing** —
> confirm the precise terms (and the exact credit string) against the listing before commercial release.
> (Earlier note, now resolved: the zip itself carried no LICENSE/README and the PNGs no embedded
> metadata; the author/tags/status/AI-disclosure above are the owner's out-of-band attribution.)

> **Metas are hand-authored, not Unity-generated** (we build headless — no Unity to auto-generate
> `.meta`s). Each `.meta` clones the committed VS-23 import lock and adapts it for discrete decor:
> Sprite · **PPU 32 · Point · Compression None · mips off · sRGB on · Clamp wrap** (trees are discrete
> decor, NOT seamless tiles → **Clamp**, unlike the `Tilesets/Water/` repeat tile) · **Pivot
> BottomCenter** (`{x: 0.5, y: 0}` — trees plant at the trunk base so they sort correctly in the ¾
> top-down view). Sprite Mode **Multiple** with **one full-cell sub-sprite** per file (`TreeNN_0`,
> rect = the whole 64×64 cell). Fresh, repo-collision-checked GUIDs / `spriteID`s / `internalID`s.
> `ArtImportPipeline` only stamps the lock when a `.meta` is *missing*, so these authored metas are
> authoritative and stable on a fresh clone / CI ([[commit-unity-metas-with-assets]]).

**Files:** `Sprites/Environment/Trees/Tree01.png` … `Tree37.png` — **37 sprites, each 64×64 px**
(2 m × 2 m at PPU 32) · Sprite (Multiple, 1 sub-sprite each) · **pivot BottomCenter** · **Clamp** wrap.
All confirmed **LFS-tracked** (the `*.png` rule) with their `.meta` committed alongside.

The pack is a four-season decor set (visual types below, from inspection — names are descriptive, not
botanically authoritative; files are numbered `TreeNN` so the GUID/ref is stable regardless of any
later re-classification):

| File | Visual type (approx.) |
|---|---|
| `Tree01`, `Tree05`, `Tree06`, `Tree08`, `Tree18`, `Tree21`, `Tree34`, `Tree35` | green summer broadleaf (oak / maple / round canopy) |
| `Tree02`, `Tree22` | green conifer / pine |
| `Tree03`, `Tree04`, `Tree17` | blossom / flowering (pink-white) |
| `Tree07`, `Tree26` | weeping willow (drooping fronds) |
| `Tree09`, `Tree10`, `Tree11`, `Tree12`, `Tree19`, `Tree23`, `Tree31`, `Tree32`, `Tree36` | autumn canopy (orange / red / yellow) |
| `Tree14`, `Tree15`, `Tree24` | snow-covered conifer (winter) |
| `Tree13`, `Tree16`, `Tree20`, `Tree28`, `Tree33`, `Tree37` | bare / dead winter branches |
| `Tree25` | slender pale-trunk birch / sapling |
| `Tree27` | dark / deep-purple foliage |
| `Tree29`, `Tree30` | fruit tree (red fruit on green) |

**WIRE-IN (world-content):** *world-content* places these as scene decor during scene-dressing /
the art pass. **PLACED so far:** the cold-coast subset (green broadleaf, pine, birch — no
blossom/autumn/snow) is scattered along the land/coast edges of **Coddle Cove** (`GreyboxBuilder`,
14 trees) and **Port Greywick** (`GreywickBuilder`, 11 trees) under a `Decor/Trees` parent, base-Y
`sortingOrder`, never in water / on docks / paths / over buildings. **PENDING:** **St Peters** trees
are a follow-up (its builder was contested at the time of this pass). The art lane only provides the
locked, correctly-pivoted sprites with stable GUIDs.


---

## UI icon wire-in — sell / catch / HUD (ui-ux)

*ui-ux* integrated the imported icons into the UI through a Core seam (no UI→Fishing/Economy
reference): **`Core.IconRegistry`** (id → sprite) is published at boot from an authored
**`Resources/IconLibrary.asset`** by the self-installing **`Core.IconRegistrar`** (see
`docs/architecture/tech-architecture.md` §4.3). The UI reads `IconRegistry.Get(id)` and falls back to
text-only when an icon isn't registered.

- **Done now (a UI surface shows the sprite):**
  - **Sell screen** (`SellScreen`): each hold/species row shows the fish/clam icon at its left, and the
    detail panel shows the larger icon beside the species name.
  - **Catch card** (`HudController`): the landed fish/clam icon shows above the "nice catch!" text
    (was TEXT-only; the noted follow-up is closed — resolved by id, not via a builder).
  - **HUD money**: a `ui.coin` glyph sits beside the cash read.
  - **Fish defs**: `FishSpeciesDef.Sprite` assigned on Cod / Haddock / Mackerel / Lobster / SoftShellClam
    (the data-driven home) — these match the `IconLibrary` fish rows.
- **Registered + ready, but no UI surface yet (so no icon visible until the screen exists):**
  `gear.rod` / `gear.shovel` / `gear.bucket`, `license.cod`, `boat.dory` / `boat.punt`, and `ui.hold`
  are in the `IconLibrary` so the future Shipwright / gear / licence **buy screens** (today only a dev
  keypress, no screen) and a HUD hold-fullness read pick them up by id with zero extra wiring.
- **Flagged (NOT done — needs another lane):** a glanceable **HUD hold-fullness** read needs a Core
  seam to read the active boat's `IHold` (no `GameServices` hold accessor exists) — a *lead-architect*
  seam, not built here. The `ui.hold` icon is registered and waiting.
