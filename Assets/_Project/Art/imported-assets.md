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
- **Tide-aware moving shoreline** (the former headline P1 visual) — **two retired, one canonical.**
  `Code/Art/TideShoreline.cs` (a smooth transform-slide of a water plane, wired into no scene) was removed
  (ADR 0012 §5, follow-up (a)). `Code/Environment/TidalFlatVisual.cs` (a 2 m colour-cell tide-reveal grid)
  was also removed (ADR 0012 "converge the live shoreline on the shader path"): it double-drew the St Peters
  bar as blocky squares ON TOP of the shader. The **single** live tide-aware shoreline is now the height-map
  water shader (`Art/Shaders/HiddenHarboursWater.shader` + `Code/Art/WaterSurface.cs`) — its `clip(depth)`
  bares the authored ground when the tide recedes and renders depth-graded water when covered. Use it for any
  tide-gated coast (give the region a `TidalTerrain` + a Sea plane carrying `Water.mat` + `WaterSurface`,
  sorted ABOVE the authored ground it reveals).
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

---

## Skiff fleet — two 7 m centre-console skiffs + the shared remote-steer outboard (IMPORT-ONLY)

Two hulls off one keel — the **console skiff** (the workboat: wood sole, painted liner, gabled teal
canopy; single-engine only) and its **sport** glass sister (gelcoat white, twin teal stripes, stainless
rails + pulpit, domed bimini, raked bow; single **or** twin engine). Both share the same ~7.0 m
envelope, transom, pivot and mount anchors, so the outboard layers drop onto either one unchanged.
Sliced by `Editor/SpriteSheetSlicer.cs` (manifest-driven, pivot asserted per-sprite by `VerifyAll`).

| Sheet | File | px (W×H) | Grid | Cell | Index math |
|---|---|---|---|---|---|
| Console hull | `Boats/ConsoleIso.png` | 1952×216 | 8×1 | 244×216 | `index = heading` |
| Sport hull | `Boats/SportSkiffIso.png` | 1952×216 | 8×1 | 244×216 | `index = heading` |
| Console rock loop | `Boats/ConsoleIsoRock.png` | 1952×1728 | 8 cols × 8 rows | 244×216 | `index = heading×8 + frame` |
| Sport rock loop | `Boats/SportSkiffIsoRock.png` | 1952×1728 | 8 cols × 8 rows | 244×216 | `index = heading×8 + frame` |
| Outboard upper (work) | `Boats/SkiffMotorUpper-Work.png` | 2448×1728 | 9 cols × 8 rows | 272×216 | `index = heading×9 + steerCol` |
| Outboard lower (work) | `Boats/SkiffMotorLower-Work.png` | 2448×1728 | 9 cols × 8 rows | 272×216 | `index = heading×9 + steerCol` |
| Outboard upper (sport) | `Boats/SkiffMotorUpper-Sport.png` | 2448×1728 | 9 cols × 8 rows | 272×216 | `index = heading×9 + steerCol` |
| Outboard lower (sport) | `Boats/SkiffMotorLower-Sport.png` | 2448×1728 | 9 cols × 8 rows | 272×216 | `index = heading×9 + steerCol` |

- **Headings** (every row, every sheet — same CW order as the dory): `0 N · 1 NE · 2 E · 3 SE · 4 S ·
  5 SW · 6 W · 7 NW`. Rock cols = an 8-frame wave loop (roll+pitch+heave), ~7 fps to idle on the water;
  the sport rocks livelier (light glass hull), the console is stiffer.
- **Steer cols:** `0 = −30°` (full port) … `4 = dead ahead` … `8 = +30°` (full starboard), 7.5° steps.
  There is **no tiller** — steering is remote from the console wheel and the whole engine swivels on its
  clamp; tie the steer column to the wheel/rudder state and step at ~8 fps.
- **THE PIVOT (the load-bearing bit).** Every slice on every sheet shares one normalized pivot
  **(0.5, 0.4444…)** = the boat origin (amidships, keel bottom, centreline). The kit README fixes the
  anchor from each cell's **top-left**: hull `(122,120)` of 244×216, motor `(136,120)` of 272×216 — the
  motor cell is wider *on purpose* so hard-over/raised poses never clip. Flipped to Unity's bottom-left
  origin (`y = 216−120 = 96`), `122/244` and `136/272` **both** normalize to 0.5 — that identity is what
  pins the wider motor cell onto the transom. Composite by pinning pivots to one point, never by corner.
- **DRAW ORDER (per heading):** UPPER always composites **over** the hull. LOWER goes **under** the hull
  for the stern-away headings **SE/S/SW (3,4,5)** and over it everywhere else. So: `lower → hull → upper`
  for SE/S/SW; `hull → lower → upper` otherwise. Verified pixel-exact against the kit previews.
- **TWIN FIT (sport only):** reuse the *same* sport motor sheets — no extra art. Both engines steer off
  the one wheel; the bake is orthographic so a lateral clamp shift is an exact per-heading screen offset
  (`MOTOR.mountOffset(dir, mx)` for `mx ∈ [−0.34, +0.34]`). Draw the FAR engine first within each layer.
- **Not on the sheets:** the raised/tilt pose (prop clear, parked/beaching) — bake on demand from the rig.
- **Source rigs** (parametric, re-bakeable, JS/no-deps) + the art director's README live in
  [`docs/art/skiff-fleet-rigs/`](../../../docs/art/skiff-fleet-rigs/) — deliberately **not** under
  `Assets/` (Unity would try to treat `.js` as legacy script). They expose `render(dir, …)`, `rock(i)`,
  `motorMount(dir)`, `helmSeat(dir)` and `tubMounts(dir)` anchors if a pose ever needs re-baking.
- **Import note:** these are the first sheets to exceed Unity's default **2048** `maxTextureSize` — at
  2448 px wide the motor sheets imported *downscaled*, which silently poisons a grid slice (rects get
  refit + alpha-trimmed and the pivot is thrown away). `SpriteSheetSlicer` now raises the cap to the next
  power of two before slicing any oversized sheet.

**WIRE-IN (NOT done here — import + slice only):** no `BoatHullDef`, prefab, scene or spawner references
these yet. Wiring the fleet (hull defs, the rock loop, the steer column, twin-fit offsets) is
*gameplay-systems*' job; this lane provides the locked, correctly-pivoted, stable-GUID slices.

---

## Iso punt — the ~5.2 m tiller punt + her two-build outboard (IMPORT-ONLY)

The art director's punt kit: the flat-floored tiller punt (beamier and slightly longer than the dory,
wide low transom cut for an outboard; painted white topsides, teal sheer band + bottom, gold cove
pinstripe, bare-wood interior — the same fleet scheme as her buoy) and her outboard in **two paint
builds**. Same iso bake as the dory/skiff kits: 32 px = 1 m, fixed ¾ camera, elev 40°, 45° steps, no AA,
upper-left key.

| Sheet | Size | Grid | Slices | Index math |
|---|---|---|---|---|
| `Boats/PuntIso.png` | 1472×168 | 8 × 1 of **184×168** | 8 | `index = heading` |
| `Boats/PuntIsoRock.png` | 1472×1344 | 8 cols × 8 rows of **184×168** | 64 | `index = heading×8 + frame` |
| `Boats/PuntMotorUpper-Basic.png` | 1908×1344 | 9 cols × 8 rows of **212×168** | 72 | `index = heading×9 + steerCol` |
| `Boats/PuntMotorLower-Basic.png` | 1908×1344 | 9 × 8 of **212×168** | 72 | ″ |
| `Boats/PuntMotorUpper-Upgraded.png` | 1908×1344 | 9 × 8 of **212×168** | 72 | ″ |
| `Boats/PuntMotorLower-Upgraded.png` | 1908×1344 | 9 × 8 of **212×168** | 72 | ″ |

- **Headings** (all sheets, rows on the grid sheets): `0 N · 1 NE · 2 E · 3 SE · 4 S · 5 SW · 6 W · 7 NW`
  — clockwise, same order as the dory/skiffs. Rock cols = an 8-frame wave loop (roll+pitch+heave), ~7 fps
  to idle on the water; she is **beamier than the dory, so she rolls stiffer**.
- **Steer cols:** `0 = −32°` (full port) … `4 = dead ahead` … `8 = +32°` (full starboard), **8° steps**
  (rig: `angle(f) = −32 + 64f/8`). ⚠️ **Not the skiffs' ±30° / 7.5° steps** — check the sheet you're on.
  This is a **TILLER** outboard: steering swings the tiller across the transom and the operator's aft hand
  follows it. **No console, no wheel, no twin fit.** Tie the steer column to the helm/rudder state and step
  at ~8 fps, the same cadence as the oars.
- **THE PIVOT (the load-bearing bit).** Every slice on every punt sheet shares one normalized pivot
  **(0.5, 0.440476…)** = the boat origin (amidships, keel bottom, centreline). The kit README fixes the
  anchor from each cell's **top-left**: hull `(92,94)` of 184×168, motor `(106,94)` of 212×168 — the motor
  cell is wider *on purpose* so hard-over/raised poses never clip. Flipped to Unity's bottom-left origin
  (`y = 168−94 = 74`), `92/184` and `106/212` **both** normalize to 0.5 — that identity is what pins the
  wider motor cell onto the transom. Composite by pinning pivots to one point, never by corner.
  ⚠️ **This is NOT `SkiffOrigin`.** Same anchor *concept*, different cell: the skiffs derive
  `(0.5, 96/216 = 0.4444)` from a 244×216 cell, the punt `(0.5, 74/168 = 0.4405)` from her own. Reusing the
  skiffs' would sink the punt ~0.7 px at PPU 32 — `SpriteSheetSlicer` keeps a separate `PuntOrigin` const
  and `PuntSheetSliceTests` asserts the two stay distinct.
- **DRAW ORDER (per heading):** UPPER always composites **over** the hull (the tiller arcs inboard, above
  the deck). LOWER goes **under** the hull for the stern-away headings **SE/S/SW (3,4,5)** and over it
  everywhere else. So: `lower → hull → upper` for SE/S/SW; `hull → lower → upper` otherwise.
- **PAINT BUILDS:** `Basic` = weathered grey/black starter (paint scuffs, pan rust); `Upgraded` = ~15%
  larger domed cowl, gloss-black pan, white top, red wrap stripe + side flashes, brighter prop. Both builds
  share the **same cell, pivot, steer cols and grip JSON** — they are drop-in swaps (pick one per boat
  instance and swap the two PNGs).
- **Verified pixel-exact against the kit previews:** re-compositing all 8 headings of both builds from the
  source cells at this pivot in the draw order above gives **zero RGB diff** vs `_preview-basic.png` /
  `_preview-upgraded.png`. (The previews are reference-only and deliberately **not** imported.)
- **`Boats/PuntMotorGrips.json`** — the tiller-grip `x,y` per heading × steer frame, in **motor-cell**
  space, for seating an operator's aft hand on the tiller. Shared by both builds (tiller geometry is
  identical). Keys: `cell, pivot, hullPivot, cols, maxSteerDeg, order, grips{HEADING:[{x,y}×9]}`.
  **Nothing consumes it yet** — there is no punt operator sprite; committed unwired for later, exactly as
  `DoryOarHandles.json` was.
- **Not on the sheets:** the raised/tilt pose (prop clear, parked/beaching) — bake on demand from the rig
  with `tilt 0..40`.
- **Source rig** (parametric, re-bakeable, JS/no-deps) + the art director's README live in
  [`docs/art/punt-iso-rig/`](../../../docs/art/punt-iso-rig/) — deliberately **not** under `Assets/`
  (Unity would try to treat `.js` as legacy script). It exposes `render(dir, …)`, `rock(i)`,
  `renderMotor(dir, {steer, tilt, part, variant, …})`, `tillerGrip(dir)`, `motorMount(dir)` and
  `tubMounts(dir)` if a pose ever needs re-baking.
- **Import note:** unlike the 2448-wide skiff-motor sheets, every punt sheet fits under Unity's default
  **2048** `maxTextureSize`, so the downscale trap does not fire here. `PuntSheetSliceTests` asserts native
  res anyway — a downscaled sheet still yields the *right sprite count*, so only the res + pivot asserts
  would catch it.

**WIRE-IN (NOT done here — import + slice only):** no `BoatHullDef`, prefab, scene or spawner references
these yet, and `PuntMotorGrips.json` is unread. Wiring the punt (hull def, the rock loop, the tiller steer
column, the grip-seated operator) is *gameplay-systems*' job; this lane provides the locked,
correctly-pivoted, stable-GUID slices.

---

## Batch — Drift Weed kit (owner drop 2026-07-23)

The seaweed/flotsam **surface-drift decor** kit: four parametric species baked as variant-column ×
3-ramp-row sheets, plus the gameplay sidecar. Same 32 px = 1 m / no-AA standard (import lock
auto-applies); ¾ iso from the south with vertical foreshorten **0.72 baked into the shapes**;
1 px keyline `#1b2a22` (the decor keyline — matches the flowers/grass set). **NO heading** — these
are flat water-surface clumps, not turntable bakes.

| Sheet | Size | Grid | Slices | Rows (top→bottom) |
|---|---|---|---|---|
| `Sprites/Shore/Drift/Bladderwrack.png` | 192×108 | 4 cols × 3 rows of **48×36** | 12 | living · golden · bleached |
| `Sprites/Shore/Drift/SugarKelp.png` | 192×108 | 3 × 3 of **64×36** | 9 | ″ |
| `Sprites/Shore/Drift/Eelgrass.png` | 128×72 | 4 × 3 of **32×24** | 12 | ″ |
| `Sprites/Shore/Drift/TornMat.png` | 192×144 | 3 × 3 of **64×48** | 9 | ″ |

- **Columns = variants** (each its own seed-locked bake — no mirroring); **rows = ramps**: the SAME
  structure recoloured (living / golden / bleached; every ramp carries a `wet` sky-glint step).
  Slice names are `<Stem>_<index>`, row-major from the top-left (`index = rampRow×cols + variant`).
- **THE PIVOT (the load-bearing bit): per-VARIANT, not per-sheet.** Every slice pivots on its
  variant's **buoy** — the buoyancy centre from `Sprites/Shore/Drift/DriftWeed.json`, "register the
  sprite to the water surface here". The buoys genuinely differ column to column, so these sheets are
  sliced by their own `DriftWeedSheetSlicer` (sidecar-driven Custom pivots; menu *Hidden Harbours ▸
  Art ▸ Slice Drift Weed Sheets*), not by a `SpriteSheetSlicer` manifest entry. The 3 ramp rows of a
  column share one pivot (structure is seed-stable down a column) — `DriftWeedSheetSliceTests` holds
  the grid, the counts, and every buoy pivot to the kit contract restated as literals.
- **`Sprites/Shore/Drift/DriftWeed.json`** — the gameplay sidecar: per-cell `buoy` (px) + `snags`
  (2–3 outer tips ≥60° apart, px + metres) + `dragTail` (the trailing end, px + metres); metre frame
  `mx=(x−buoy.x)/32`, `my=(y−buoy.y)/(32·0.72)`. Owner's four `_confirm` judgments RULED 2026-07-23
  and recorded in place as `_ruled` (kept text — provenance): dragTail stays as baked · all four
  species ship golden rows · snag radius defaults **0.1 m** · no stranded set (the `Shore/Seaweed*`
  rockweed tiers own the wrack line). **Nothing consumes it yet** — committed as data for the future
  drift feature, exactly as `PuntMotorGrips.json` was.
- **Source rig:** [`docs/art/rigs/driftWeedRig.js`](../../../docs/art/rigs/README.md) → `DriftWeed`,
  landed byte-identical (sha256 = the sidecar's `derivedFromRigSha256`; the slice tests verify the
  tripwire). Parametric generators — any species can be re-baked at new seeds/params forever.
- **Import note:** the largest sheet is 192×144 — nowhere near the 2048 cap, so the downscale trap
  cannot fire here; the tests assert native res + pivots anyway (the Cape Islander discipline).

**WIRE-IN (NOT done here — import + slice only):** no prefab, spawner, scene or shader references
these yet. The runtime drift feature (drift/bob off the shared wave field, snag on buoys/rocks/rope,
clumping, ramp-row weathering) is the banked emitter-lane build (`seaweed-flotsam` vision, owner ask
2026-07-08); this lane provides the locked, correctly-pivoted, stable-GUID slices + the sidecar data.

---

## Batch — Terrain kits: Shoreline ISO (v7) + Road/Path blob-47 (owner drop 2026-07-23)

Two **terrain** kits, imported together because they are the two halves of paintable ground: the
coast, and everything you walk on once you're up it. Both are square **32×32 cells, 32 px = 1 m**,
no AA, muted North-Atlantic KTC ramps, hash-value noise phased on **global tile coords** (`gx,gy`)
so a painted run is seamless and never visibly repeats.

**The shoreline kit is baked to the boat camera** — ¾ from the **SOUTH at 40°**, the fleet's
turntable elevation (ADR 0006/0022). That is the point of the "v7 ISO" re-bake: the older near-plan
`Shore*`/`Grass`/`Sand`/`Rock` tiles still sitting loose in `Tilesets/` were drawn to a different
camera than the hulls, so land and boat never quite shared a space. **Those older tiles are left
exactly where they are** — nothing already painted breaks; the new kit is a parallel set.

### ⚠ The water contract — this kit bakes ZERO water, on purpose

The engine shader owns **all** of it (ADR 0010 / 0012 / 0023): it clips at the live depth-0 tide
contour, rides foam and swash on that line, and pins the displaced 3D surface to the same one
(`ShoreFadeMath`). So there is **no foam tile, no waterline tile, no shallows tile** here, and none
should be authored against these.

- **Every ground material is drawn to read right DRY *and* SUBMERGED**, because the tide sweeps whole
  flats over it — that is what makes a St Peters clam flat work as one painted surface across the
  whole swing rather than two sets of art.
- **Rule-tiles carry terrain-TYPE edges only** (grass↔sand↔rock) plus permanent landforms (cliff,
  dune). Butt any tile straight against shader water: there is nothing to line up.

### Sheets

| Sheet | Size | Grid | Slices | Rows (top→bottom) |
|---|---|---|---|---|
| `Tilesets/ShorelineIso/ShoreIsoGround.png` | 96×192 | 3 cols × 6 rows of **32×32** | 18 | grass · marram · sand · ripple · shingle · shelf |
| `Tilesets/ShorelineIso/ShoreIsoFringe.png` | 384×96 | 12 × 3 | 36 | grass · marram · sand |
| `Tilesets/ShorelineIso/ShoreIsoCliff.png` | 320×96 | 10 × 3 | 30 | cap · mid · toe |
| `Tilesets/ShorelineIso/ShoreIsoDune.png` | 288×32 | 9 × 1 | 9 | (single band) |
| `Tilesets/ShorelineIso/ShoreIsoSprites.png` | 186×44 | **packed, irregular** | 7 | sea stacks `reef/s/m/l` + slab boulders `bs/bm/bl` |
| `Tilesets/Roads/RoadIso_<surface>_new_blob47.png` × 7 | 384×128 | 12 × 4 | 48 | blob-47 by neighbour mask |

- **Ground COLUMNS are three adjacent world tiles, not three art variants.** The rig's noise is a
  pure function of `(gx,gy)`, so neighbours butt seamlessly — these three are a sample of that field.
- **Fringe** is a transparent overlay: stamp it **over the neighbour's ground tile** where two
  terrain types meet for a ragged tongue (grass/marram carry a 1 px soil under-shadow on
  camera-facing edges).
- **Cliffs stack**: `cap + mid×N + toe` for any height, ~1.3 m of drawn face per band at the 40°
  camera. Strata key on **global row Y**, so bands painted at the same world height align across a
  whole coast with no hand-matching. `caveToe` is the carved arch — it is how a sea cave gets a mouth,
  and it is cliff-only (the dune has the same nine landform pieces *minus* the cave).
- **Roads**: 12×4 = **48 cells holding 47 blob tiles + one spare padding cell**. Anything that walks
  the atlas by its rectangle would paint that 48th cell as road — `ShorelineIsoCatalog.RoadBlobCount`
  is the stop.

### Slicing + naming

- The five uniform sheets and all seven road atlases are **`SpriteSheetSlicer` manifest entries**
  (menu *Hidden Harbours ▸ Art ▸ Slice Environment + VFX Sheets*), **Center pivot** — a tilemap places
  by cell, so any other pivot shifts a painted tile off its own cell and a stacked cliff band off its
  neighbour.
- `ShoreIsoSprites.png` is **packed at seven different sizes with per-item base-centre pivots**, so it
  gets its own sidecar-driven `ShorelineIsoSpriteSlicer` (menu *Hidden Harbours ▸ Art ▸ Slice
  Shoreline Iso Rock Sprites*), reading rects and pivots from `ShoreIsoSprites.json`. Every pivot is
  horizontally centred and exactly **1 px above the item's base** — that contact point is what makes a
  sea stack and a boulder of different heights place by the same rule instead of floating.
- **Slice names state GEOMETRY, not semantics** (`ShoreIsoCliff_7` = the 8th cell, row-major from the
  top-left) — the same rule as `CharacterSheetSlicer`, and for the same reason: compass-labelled art
  has been mislabelled in five kits here. The meaning is resolved in one place,
  **`ShorelineIsoCatalog`**, against the kit's own `ShorelineIso.json` contract.
- **⚠ The compass letters (`cornSW`, `sideW`, `faceS`, `edN`…) are the kit's CLAIM, not a
  measurement.** There is no azimuth probe for a static tile, so nothing here has been checked against
  rendered pixels. The column ORDER is what the catalog guarantees; if a painted cliff faces the wrong
  way in-scene, correct the label→column map in `ShorelineIsoCatalog` (and the kit README) — never
  renumber the slices.

### Source rigs

- **`docs/art/rigs/shoreIsoKitRig.js`** → `ShoreIso` (new). Re-bakes any tile, any cliff height, any
  sprite: `ground(mat,{gx,gy,seed})` · `fringe(mat,piece)` · `cliff(piece,{band,gy,feature:'cave'})` ·
  `column(piece,rows)` · `dune(piece)` · `stack(size)` · `boulder(size)`.
- **`docs/art/rigs/roadPathRig.js`** → `RoadKit` (**already in the repo since #227, unchanged**). The
  kit zip's copy `md5`s differently, but that is **line endings only** — byte-identical once the CRs
  are stripped, so these atlases bake from exactly the rig already committed.
  `render(surface,{con,diag,wear,ground,markings,gx,gy,seed})` · `renderGround(ground,{gx,gy})` ·
  `BLOB47`. The PNGs are one bake at `new` wear over a grass verge with no markings; **`worn`/
  `cracked`, dirt/sand verges and lane markings all live in the rig** — re-bake, never hand-edit a
  sheet.
- Kit READMEs + the reference previews (`_preview-hero.png`, `road-scene.png` — reference only, NOT
  for import) are under `docs/art/shoreline-iso-kit/` and `docs/art/road-path-kit/`.

### Known limits (kit v7)

- N-facing cliff back-lips reuse the plateau grass tile (occluded at this camera); diagonals are 45°
  only.
- Overlay dressing (marram tufts, driftwood, fences, spruce) is **not** in this kit — it comes from
  the Wildflowers / Seaweed / Shoreline Finds sets already imported, which all composite fine on this
  ground.

**WIRE-IN (NOT done here — import + slice only):** no `Tile` asset, `RuleTile`, palette entry, paint
tool, prefab or scene references these yet. `TileAssetBuilder`/`TilePaletteBuilder` still build the
older loose tileset, untouched. Standing up the ISO ground/fringe rule-tiles and the road blob-47
autotiler is the next step and belongs with the world-scene work, not with the import.

---

## Batch — Shoreline ISO **v8** (`ShoreIso2`), two styles (owner drop 2026-07-28)

The same coast, re-cut, and shipped **twice**. v7 above is left in place and untouched, so nothing
already painted against it breaks. Same 32×32 cell, same 40°-from-the-south camera, and the **same
zero-water contract** — verified rather than assumed: all ten delivered sheets were scanned at import
and carry **zero blue-dominant pixels**.

### Two styles, one geometry

`nat` (naturalist — 8/7/8-step ramps, Bayer dither in the band-transition zone) and `gfx` (graphic —
6/5/6-step ramps, hard edges, unified keyline) are the kit's A/B. Geometry, grid, piece names and
pivots are **identical**; only the shading law differs, so a map authored against one drops straight
onto the other. `ShorelineIso2Catalog.ContractsAgreeOnGeometry` keeps that true if either style is
ever re-baked alone.

### Sheets — `Tilesets/ShorelineIso2/{nat,gfx}/`

| Sheet | Size | Grid | Slices | Rows (top→bottom) |
|---|---|---|---|---|
| `Ground.png` | 128×192 | **4** cols × 6 rows of **32×32** | 24 | grass · marram · sand · ripple · shingle · shelf |
| `Fringe.png` | 384×96 | 12 × 3 | 36 | grass · marram · sand |
| `Cliff.png` | 320×96 | 10 × 3 | 30 | cap · mid · toe |
| `Dune.png` | 288×64 | 9 × **2** | 18 | cap · toe |
| `Contact.png` | 160×32 | 5 × 1 | 5 | (single row: `n · ne · e · nw · w`) |

**⚠ Three grids changed from v7, so a v7 index is NOT a v8 index** — which is why v8 has its own
`ShorelineIso2Catalog` rather than more members on `ShorelineIsoCatalog`:

- **Ground 3 → 4 columns.** Still adjacent world tiles, not art variants.
- **Dune 1 → 2 rows.** `cap` alone is a 1 m bank; `cap + toe` is 2 m.
- **`Contact.png` is new** — the ambient-occlusion overlays v7 had none of. Stamp one on the GROUND
  tile a landform stands next to and the landform seats instead of floating. All five are of the
  NORTHERN half: at this camera a landform's south side is its own lit face.

### ⚠ `Cliff.png` is 10 columns but **not** 30 tiles

Column 9 is the cave, and a sea cave is carved at the waterline — it is filled on the **toe row
only**. Cells **9 and 19 are fully transparent padding** (measured: 100% alpha-zero in both styles,
pinned by `CliffPaddingCells_AreActuallyEmpty`). Exactly the road blob-47 trap one section up, where
the 48th cell is spare. The slice still cuts the full rectangle — a sheet's slice count must match its
grid — but `ShorelineIso2Catalog.CliffIndex` **throws** on `cap`/`mid` + cave rather than handing back
an index that resolves to nothing.

### Import settings — the soft alpha is load-bearing

`Fringe` and `Contact` carry **semi-transparent** pixels (measured alpha 92–235) for the soil
under-shadow and the AO overlays; every other sheet is binary. That makes `Compression = None` a
correctness setting, not a tidiness one — block compression quantises exactly that gradient and the
sheet still looks broadly right. This kit is all colour and ships **no** mask/normal/ID channel, so
sRGB stays **ON** (the inverse of the tree kit's data-channel trap). Pinned by
`EverySheet_CarriesTheLockedImportSettings`.

### Slicing + naming

Ten `SpriteSheetSlicer` manifest entries (menu *Hidden Harbours ▸ Art ▸ Slice Environment + VFX
Sheets*), **Center pivot**, sliced on import — the sheets ship ready to use. Slice names state
GEOMETRY (`Cliff_7` = the 8th cell, row-major from the top-left) and the meaning is resolved in
`ShorelineIso2Catalog` against each style's own `ShorelineIso.json`. The compass letters remain the
kit's **claim, not a measurement** — same standing warning as v7.

**WIRE-IN (NOT done here — import + slice only):** nothing places these. No `Tile` asset, rule-tile,
palette entry, paint tool, prefab or scene references them, and v7 is not retired (that is a separate,
owner-approved change).

---

## Batch — Wharf / dock tile kit (owner drop 2026-07-23)

The working waterfront's **deck**: near-plan 32×32 tiles that sit in the ground plane like `Grass.png`,
plus the mooring hardware and the shore-armour arms. Same 32 px = 1 m / no-AA / upper-left key standard
as the fleet, so buildings, boats and deck all register.

### ⚠️ The cell is 32×**56**, and that is the whole contract

The camera looks from the **south**, so a south-facing deck edge drops a visible **vertical face** over
the water. The top **32 rows are the deck** (the tile proper); the bottom **24 are face + waterline
foam**, and they **overhang downward over whatever is drawn in the cell below**.

Two consequences, both easy to get wrong and both loud once you do:

- **The atlas pivots TOP-LEFT**, not centre — the sidecar's "cell top-left aligns to tile screen origin".
  Centre is the right pivot for every other tile sheet in this repo and it is wrong here: it would sink
  every wharf tile 12 px into the water, consistently enough to look like an art bug rather than an
  import one.
- **Whatever draws these must paint BACK TO FRONT** (north rows first), or a face will overdraw the deck
  of its own southern neighbour.

N/E/W open edges get a raised curb only — the tall face is south + the SE/SW diagonals. That is a
single-camera convention, and it means a thin finger pier reads best running **N–S**.

### Sheets

| Sheet | Size | Grid | Slices | Rows (top→bottom) |
|---|---|---|---|---|
| `Tilesets/Wharf/WharfAtlas.png` | 544×392 | 17 cols × 7 rows of **32×56** | 119 | quay · lowpier · tallpier · **float f0–f3** |
| `Tilesets/Wharf/WharfBreakwaters.png` | 144×240 | 3 × 4 of **48×60** | 12 | riprap · crib · wall · sheet |
| `Tilesets/Wharf/WharfOverlays.png` | 520×41 | **packed, irregular** | 14 | rails · cleat · bollard · ring · dolphin · ladder · tyre · pile head · gangway |

- **Atlas columns** are the 17-piece auto-tile set: `ctr` · 4 edges · 4 outer corners · 4 end caps
  (three sides open) · 4 diagonal 45° cuts. An "open" side is one that drops to water.
- **`float` is ONE material occupying FOUR rows** — a 4-frame bob loop (f0–f3, ±1 px heave at ~6 fps,
  offsets 0, −1, 0, +1). The three fixed materials have no frames. `WharfKitCatalog.AtlasRow` throws if
  you ask a concrete quay for a bob frame, because a caller animating a quay has a bug.
- **Breakwaters pivot on the CREST** (top-centre), not the base — that is what lets consecutive pieces
  butt into a continuous run around a 45° turn, since the four armour types have different base heights
  (the gap below each is its foam fringe).
- **Overlays are sidecar-sliced**, because their pivots mean *different things per fitting*: standers
  (bollard/dolphin/pile head) sit 1 px above their base — the same contact rule as the shoreline kit's
  sea stacks; hangers (ladder/tyre/gangway) pivot at the **top** and fall away from where they attach;
  the low flat fittings (cleat, recessed ring) project their contact point **mid-sprite**, which is
  correct for a 7 px-tall object at a ¾ camera and not a bug; rails pivot on the **edge line** they run
  along, which is their bottom row for an N/S run and their top for an E/W one. Wood and galvanised-pipe
  rails are geometrically identical per run, so material is a paint decision and never a placement one.

### Source rig

**`docs/art/rigs/wharfKitRig.js`** → `WharfKit` — **already in the repo and unchanged** (byte-identical
once line endings are normalised; same check as `roadPathRig`). It re-bakes the **deck tiles** — any
material, any edge/diagonal combination, any bob frame. Overlays and breakwaters ship as baked sheets;
edit them via their PNGs or ask the art director for a parametric rig.

### ⚠️ The wharf BUILDING sheet is deliberately NOT in `Assets/`

`WharfBuildingIso_shack.png` ships in the building-kit zip at **9600 × 1160** — 8 facings × a
1200 × 1160 cell. It is reference only, and it stays under `docs/art/wharf-building-kit/`:

- Unity's default cap is 2048, so it would import **silently downscaled**. Lifting the cap to hold it
  means `NextPowerOfTwo(9600) = 16384`, i.e. a **16384 × 2048** texture ≈ **134 MB** at RGBA32 — for one
  preset of one building.
- The cell is oversized on purpose (it must hold the `cannery`/`fishPlant` presets), so a net shed is a
  small object in a 37 m × 36 m frame. For comparison the 12.9 m Cape Islander bakes at 456 × 420.
- **`wharfBuildingRig.js` is already in the repo** and is the source of truth. ✅ **The in-engine bake
  now exists** — see the next section.

### Also in this change: `MiniJson` moved down an assembly

`WharfOverlays.json` is dictionary-shaped (`"frames": { "cleat": {…} }`), which `JsonUtility` cannot
read. The repo already had a reader for exactly that case — but it lived in `HiddenHarbours.App.Editor`,
which sits at the **top** of the editor dependency graph (it references `Art.Editor`, not the reverse),
so nothing below it could reach it. It has moved to `HiddenHarbours.Art.Editor` with its `.meta`, and its
two callers updated. The alternative was a second, worse JSON parser thirty lines from a good one.

**WIRE-IN (NOT done here — import + slice only):** no `Tile` asset, `RuleTile`, palette entry, prefab or
scene references these yet.


---

## The BUILDING bake — houses + wharf buildings (`BuildingRigBaker`)

Both building rigs (`houseIsoRig` → the clapboard houses, `wharfBuildingRig` → net sheds, storage barns
and fish plants) are baked **in-engine** under ADR 0021, not hand-exported. Menu:
**Hidden Harbours ▸ Art ▸ Bake Buildings (houses + wharf)**. Twelve presets — five house, seven wharf.

### Why they needed their own baker rather than `RigBaker`

**The cell is sized for the largest possible build.** The house cell is 992×1060 and the wharf-building
cell is 1200×1160, because the latter must hold the `cannery`. A net shed drawn in that cell is a small
object in a 37 m × 36 m frame. Eight facings uncropped is 9600 px wide — which is exactly the reference
sheet the kit shipped, and exactly why that PNG was left in `docs/` and never imported.

So **the bake tight-crops**, and that is not an optimisation — it is what makes a bake possible:

| | uncropped | cropped |
|---|---|---|
| Widest legal grid | 3 cols × 3 rows | fits far wider |
| Sheet | 3600 × 3480 | a few hundred px per cell |
| Texture memory | **~50 MB per preset** | a fraction of it |

**One crop rect for all eight facings, not one per cell.** A grid slice needs a uniform cell — but the
reason that actually bites is the **pivot**: it must be identical across facings or the building shifts
as it turns (the same rule the boat kits state as "so a heading swap never shifts the boat"). The crop is
therefore the *union* of all eight silhouettes, and the pivot moves by exactly the crop origin.

**The pivot is DATA, not a constant.** Every other sheet in this repo pins its pivot with a named const
(`DoryWaterline`, `PuntOrigin`) because the kit fixes the cell. Here the cell depends on the preset — a
cannery crops differently from a shack — so each bake writes a **sidecar JSON** beside the PNG carrying
the cropped cell size, the cropped pivot, the crop origin, the measured convention, the footprint, and
the per-facing overlay anchors (door, ridge, chimney/stack tops) already in cropped-cell pixels.

### ⚠️ The preset trap — `{preset:'netShed'}` silently renders the wrong building

The obvious call looks right and is wrong: **neither rig's `resolve()` reads a `preset` key at all.** It
reads `type`/`era`, `body`, `siding`, `size`… so passing the name falls through to the *default* build
with no error and no warning — you would get seven identical sheds under seven different names, and the
only way to notice is to line all seven up. `PRESETS` is a data table meant to be **spread** into the
options, which is what the baker does (`Object.assign({}, Rig.PRESETS['netShed'])`).

`AssertPresetApplies` is the tripwire: it renders one facing with the preset and once with `{}` and
refuses if the bytes are identical. It cannot prove every field applied, but it catches the
whole-preset-ignored case, which is the one with teeth.

### The azimuth probe reads the DOOR, not a bow

`RigAzimuthProbe` works by PCA-ing a hull at a quarter turn and breaking the 180° ambiguity with a
bow-taper test — a boat is pointed at one end and blunt at the other. **A building has no bow.** Its
silhouette at a quarter turn is nearly mirror-symmetric, so that probe would return noise dressed as a
confident answer.

`BuildingRigAzimuthProbe` reads the **door** instead. Both rigs put the main door on the `+Y` gable and
expose it through `anchors(dir, opts).door`, already projected to screen pixels — and crucially
`anchors()` calls the *same* `camBasis`/`projVert` that `render()` draws with, so it is not a
declaration about where the door is, it is the arithmetic that puts it there. Cell 2 is labelled `'E'`;
if its door lands left of the pivot, the labels are lying by −90° and the rig is counter-clockwise.

Reading the rigs' projection by hand (`th = +dir·45°`, `xr = x·ct − y·stt`, door at `+Y`) predicts
**counter-clockwise for both**, matching the README's inference — but that is a prediction, not a
measurement. **Nothing is measured until the bake actually runs**, at which point it either agrees with
the catalog or refuses.

The probe additionally checks the rendered silhouette's width at two facings against the `Wd`/`Ln` the
rig reports, at 32 px = 1 m, and **refuses** if they disagree — because the shared-projection argument
only holds while `anchors()` and `render()` are resolving the same building.

> **Stated plainly:** the handedness is *not* independently re-derived from pixels the way the punt's
> byte-identical golden master was. There is no building feature as unambiguous as a bow taper. It rests
> on the shared-projection argument above, guarded by the width check.

### Output

`Art/Sprites/Buildings/HouseIso_<preset>.png` + `.json`, and `WharfBuildingIso_<preset>.png` + `.json`.
**Not committed until the owner runs the bake** — the sheets are generated, not authored.


### The Building Studio — dial a build, then bake it

**Hidden Harbours ▸ Art ▸ Building Studio.** The wharf rig has twenty axes and the house rig nineteen;
the twelve shipped presets are a thin sample of that space. The studio makes the whole surface reachable
without writing JS, and makes baking the last step rather than the only way to see anything.

- **Dropdowns are read from the rig**, not hard-coded — paint colours, sidings, roofs, doors, windows,
  cupolas, rooflines and types all come from the rig's own exported tables at load time, so a drop that
  adds a colour appears with no code change. Only four axes have no exported table (attic, porch,
  wainscot, loft); those are transcribed and **grepped by a test**.
- **Load a preset** to start from a known build, then dial from there. **Bake 8-facing sheet** runs the
  same `BuildingRigBaker` as the batch menu — same crop, same probe, same sidecar.
- **Elevation is a preview-only dial.** The bake always uses the rig's default 40°, the fleet's turntable
  elevation; a building baked at another camera would not sit in the same space as the boats.
- **Orientation is shown honestly.** The preview names the cell's rig LABEL *and* the bearing it actually
  DEPICTS. Both rigs turn counter-clockwise, so the cell the rig calls `'E'` draws a building facing
  **west**. The bake corrects this; the studio shows raw rig output, and labelling it `'E'` alone would
  repeat the exact mistake that has shipped five times here. The pivot (the building's ground point) is
  drawn as a crosshair.

#### ⚠️ Unknown option keys and values fail SILENTLY

Both rigs resolve options as `opts[k] != null ? opts[k] : fallback` and then look the value up in a
table. A **misspelled key** and an **unknown value** are both perfectly legal — the rig just renders
something else, with no error and no warning. That is why the studio only ever offers values it read
from the rig, and why every axis key is checked against the rig source by a test.

**A worked example of that trap, found while building this:** the wharf rig's `TYPES`/`PRESETS` tables
spell window density `winD`, so `winD` looks like the option key. It is not. The deciding line is

```
winD: opts.winDensity != null ? opts.winDensity : T.winD
```

— left of the colon is the internal build field, right is the option a caller passes. Dialling `winD`
would have been accepted in silence and done nothing. **The kit README was right and the first pass at
this table was wrong**; both rigs take `winDensity`.

---

## Batch — Rock Iso (`RockIso`), the rig and the contract (owner drop 2026-07-28)

Sibling of the Shoreline ISO v8 kit from the same drop: **ShoreIso2 owns the ground, RockIso owns
every stone standing on it.** They share a camera, a PPU and the red-sandstone ramp — `RockIso`'s
`sandstone` IS ShoreIso2's `redrock` verbatim, so a rock composites onto a cliff toe with no seam.

| Species | Cell | Variants | Sheet | What it is |
|---|---|---|---|---|
| `Erratic` | 52×44 | 4 | 208×132 | single shore boulder |
| `Outcrop` | 88×60 | 3 | 264×180 | 2–5 boulder cluster, shoreline edge dressing |
| `PoolLedge` | 80×48 | 3 | 240×144 | wave-cut plate with a tide-pool basin |
| `Skerry` | 104×52 | 4 | 416×156 | awash hazard rock, clipped at the sea plane |
| `Cloven` | 60×76 | 3 | 180×228 | split landmark, chart-mark scale |
| `Cobble` | 52×32 | 3 | 156×96 | beach cobbles & shingle, tiny filler |

Sheets are **variant COLS × dress ROWS**, named `<Species>_<stone>_<tide>.png`. Axes: stone
(`sandstone · granite · basalt · quartzite` — colour AND structure) × tide (`dry · wet · awash`) ×
dress (`bare · barnacled · weeded`, the sheet rows).

### ⭐ This kit ships NO pixels — it bakes to order

6 species × 4 stones × 3 tides = **72 sheets**, so the drop ships the rig and the sidecar and nothing
else. `RockIsoBaker` (menu *Hidden Harbours ▸ Art ▸ Bake Rock Iso Sheets*) writes them in-engine from
`docs/art/rigs/rockIsoRig.js`, running the rig UNMODIFIED in the V8 host (ADR 0021). Two menu items,
because the **atlas budget is a real decision**: the default bakes `sandstone` × 3 tides = 18 sheets;
an *ALL 4 stones* item behind a confirm dialog bakes all 72.

**The anchors are stone- and tide-independent** (the geometry is seed-stable), so one contract entry
serves all twelve bakes of a variant — which is what lets the engine be wired against `RockIso.json`
before a single PNG exists.

**⚠ The baker does NOT rewrite `RockIso.json`**, unlike the art director's `_rockBake.js`. The
committed sidecar is the ORACLE: `RockIsoBaker.AssertMatchesContract` compares the rig's live geometry
against it before writing a pixel and **refuses on any disagreement**. Every anchor is expressed in
cell pixels, so a cell that drifted by one row would silently invalidate all twenty variants at once —
this turns that into a loud stop pointing at the art director.

### ⚠ The pivot is the GROUND CONTACT, and it needs its own slicer

Under the 40° camera a rock's near flank is drawn **below** its ground contact — measured **7–22 px =
0.219–0.688 m**, positive on every one of the twenty variants. A bottom-centre pivot (what
`ArtImportPipeline` picks for standing art) floats every rock by exactly that.

`RockSheetSlicer` exists rather than a `SpriteSheetSlicer` manifest row because that manifest writes
**one pivot per sheet**, and a rock sheet's pivot is **per variant COLUMN** — each variant is a
different volume with its own contact point. One shared pivot would plant one column and misplace the
rest. Same reason `TreeSheetSlicer` exists.

Normalisation is ADR 0026's `(x/W, (H−y)/H)`. **Not** the tree kit's `pad/cellH`: the rock contract
publishes a continuous top-left coordinate and no `pad`. Read what the contract publishes.

### Anchors (all in `RockIso.json`, all measured off the built volume)

`footprint` collision ellipse (rx, ry in m) + ground contact px · `perch` highest standable point,
**`flat:false` ⇒ decorative, do not spawn on it** · `snags` outer silhouette catches for rope and pot
lines · `hazard` awash danger radius in m · `pool` tide-pool basin rect + depth (the **shader's** fill
target — the bake leaves the basin empty) · `weedLine` the tide mark where rockweed drapes.

Verified invariants: `pivot == footprint.ground`, horizontally centred, on all 20; `hazard` present
*exactly* when `waterline > 0` (7 variants); `pool` on PoolLedge alone and always inside its cell.

**⚠ `perch.flat` is a PER-VARIANT flag, not a species rule.** The kit README reads like a species rule
("Cobble and most Skerry builds"), but measured: all four Skerry are false, Cobble is 2 of 3, and
`PoolLedgeC` is flat. **Exactly two variants in the kit are standable — `PoolLedgeC` and `CobbleB`.**
Honour the flag, not the point.

**⚠ Snag separation is a GROUND-PLANE angle, not a screen angle.** The kit's rule is ≥60° apart;
measured naively in screen px, 9 of 19 variants appear to break it (worst 42.5°). They do not — the
camera squashes depth by 0.643. Un-squashed, the true minimum is **60.6°**. A sabotage test pins the
wrong measurement so nobody removes the correction.

### 🔴 Flagged to the art director, not patched

**`SkerryD` ships ONE snag where the README promises 2–3.** The other nineteen variants all carry
exactly three. Pinned by a canary test asserting the current state — if a re-bake fixes it, that test
goes red and should be deleted.

**WIRE-IN (NOT done here):** no sheets baked, nothing placed, no prefab/spawner/collision reads these
anchors yet, and the older hand-drawn `Sprites/Shore/RockCluster.png`, `RockMid.png`, `RockSmall.png`
and `TidePoolRock.png` are untouched.

---

## Batch — Shoreline Plants (`ShorePlants`), the rig and the contract (owner drop 2026-07-29)

The last habitat with no rig. Everything between the spruce line and the low-water mark was
hand-drawn per tide state, so the same weed had three unrelated silhouettes depending on which
artist drew which tide. **Sixteen species across the five tidal zones, one generator** — sibling of
ShoreIso2 (the ground) and RockIso (the stones standing on it), sharing their camera and PPU.

### ⭐ What this rig knows that the others don't: the tide is an AXIS, not a variant

Water height over a plant's **own** ground — `tideM − zone.baseM`, in metres — is resolved once and
drives four things, so they cannot disagree:

- **lay** — algae owe their height to the water. Drained, the same skeleton concertinas onto its
  substrate and falls downslope. It puddles; it does not stretch out flat.
- **wet** — soaked while any part is under, drying off over `DRY_M` = 0.85 m of further fall.
  Upland plants are never wet with no special case, because the tide never reaches their ground.
- **submergence** — everything below the waterline takes the cold water ramp and loses contrast with
  depth. **Colour only** (see below).
- **sway mode** — breeze above the waterline, phase-lagged surge below, and *nothing* when drained
  and limp. That stillness is most of why a low tide reads as low.

The zone staircase, metres above chart datum: subtidal fringe 0.15 · mid intertidal 1.55 · low marsh
2.55 · high marsh 3.35 · upland 4.65, against a nominal range `TIDE_M` = 4.0 m. It maps straight onto
the **painted seabed height plus the deterministic tide** (ADR 0014, rule 5) — no new simulation.

### ⭐ This kit ships NO pixels — it bakes to order

16 species × two sheet axes × 3 seasons × 3 growth stages is a matrix an import has no business
choosing from, so the drop ships the rig and the contract and nothing else — the same call RockIso
makes. `ShorePlantBaker` (menu *Hidden Harbours ▸ Art ▸ Bake Shore Plant Sheets*) writes them
in-engine from `docs/art/rigs/shorePlantRig.js`, running the rig UNMODIFIED in the V8 host
(ADR 0021). Two menu items, because the **atlas budget is a real decision**:

- default — the **tide axis** (5 tide states × 4 sway frames), summer, full growth, variant 0, all
  sixteen species = 16 sheets. That is the gameplay-minimum set: the tide moves continuously and
  every plant follows it, so the tide axis is what a placed plant actually samples.
- behind a confirm — **both axes** (adds 4 variants × 4 sway frames at half tide) = 32 sheets.

**Three PNGs per sheet.** The albedo, plus `<stem>_light.png` and `<stem>_tide.png` — the contract's
own filenames. Both mask channels are **DATA**: sRGB OFF, uncompressed, `alphaIsTransparency` off.

Sheet stems name the **fixed** dimension, never the axis: `<Species>_<season>_<stage>_v0` is a tide
axis sheet, `<Species>_<season>_<stage>_atLow` a variant one. That is deliberate — the state
channel's suffix *is* `_tide`, so a stem ending in a tide key would be indistinguishable from another
sheet's data channel and would get sliced in the wrong colour space. A test pins that no legal stem
can collide.

### 🔴 The strap material is a DECLARATION, and a shader must branch on it from data

A 2 px rim must leave 6 px of interior behind it, so no BODY mass is emitted under a 5 px radius. But
**a grass blade IS 2 px wide** — so blades, culms, fronds and sheets are declared **STRAP**: exempt
from the mass floor, and in exchange **forbidden a rim**. The exemption is a material, not a fudge,
which is what makes it auditable. Wood and fleck are linear too.

| bake | R | G | B | A |
|---|---|---|---|---|
| `_light` | key light · N·L · **ALL** materials, straps included | back rim · **BODY AND WOOD ONLY**, identically 0 on strap and fleck | surface depth | coverage |
| `_tide` | wetness (gloss authority) | 255 below the baked waterline | **255 on STRAP pixels — the no-rim flag** | coverage |

**`light.G` is not re-used as a spine channel — no channel changes meaning per material.** A strap's
lit spine is real N·L off its bowed cross-section normal, so it arrives in `light.R` with everything
else. **Gate every read of `light.G` on `state.B`; never infer strap-vs-mass from the sprite.** The
sprite-light include (`SpriteLightResponse.hlsl`, #314) is the consumer that will branch on it —
**not wired here, import only.**

Measured, not asserted: across the zone probes at low/half/high tide, **zero strap pixels carry
`light.G`**, and the rig's own `report.strapRimLeak` agrees. A sabotage raising ONE strap pixel to
`G = 1` is detected.

### 🔴 No baked water, and no baked moving light

Submergence bakes **colour only** — darker, cooler, contrast falling monotonically with depth. There
is no dapple, no spatial pattern and nothing per-frame, because **the live shader's caustics are
swell-driven** and a baked dapple would put two uncorrelated patterns on the same frond
(ADR 0010/0012/0023). `contract().water.bakesCaustics === false`, and a test scans submerged cells
for the actual signature of a dapple — high-frequency luminance direction changes along a scanline —
with an injected 5 px ripple as the measured sabotage.

`waterRow` is reported per render so a scene can line its water plane up with the bake; it is `null`
when the surface is outside the sprite in either direction (read `submerged` for which side).

### ⭐ Cells stay UNIONED over the tide states — one cell, one pivot per species

The runtime tide axis is continuous, so plants swap sprite state constantly as water rises over their
ground. With ONE cell and ONE ground-contact pivot those swaps are **anchored by construction**.
Per-tide cells would give every state its own pivot, and a 1 px disagreement becomes a visible hop
the moment the tide crosses a threshold. **Sheet waste is only memory; a state hop is an artifact.**

The cost is measured, not assumed — this is the table any per-species exception would be argued on:

| species | zone | h (m) | unit | cell | pivot | drape | variant sheet | tide sheet | worst ink | waste | headroom | KiB |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| `SugarKelp` | fringe | 2.75 | plant | 142×131 | 70,88 | 1.34 m | 568×524 | 710×524 | 16% (high) | 84% | 1338 | 2615 |
| `IrishMoss` | fringe | 0.53 | mat | 56×47 | 28,31 | 0.50 m | 224×188 | 280×188 | 33% (flood) | 67% | 1768 | 370 |
| `Eelgrass` | fringe | 1.44 | clump | 77×69 | 38,51 | 0.56 m | 308×276 | 385×276 | 23% (ebb) | 77% | 1663 | 747 |
| `KnottedWrack` | mid | 1.63 | plant | 98×94 | 48,64 | 0.94 m | 392×376 | 490×376 | 21% (flood) | 79% | 1558 | 1295 |
| `Bladderwrack` | mid | 1.06 | plant | 71×69 | 35,47 | 0.69 m | 284×276 | 355×276 | 20% (half) | 80% | 1693 | 688 |
| `SeaLettuce` | mid | 0.47 | mat | 61×55 | 30,31 | 0.75 m | 244×220 | 305×220 | 9% (high) | **91%** | 1743 | 471 |
| `Cordgrass` | low | 1.81 | clump | 70×83 | 34,70 | 0.41 m | 280×332 | 350×332 | 30% (low) | 70% | 1698 | 817 |
| `Glasswort` | low | 0.47 | mat | 41×40 | 20,24 | 0.50 m | 164×160 | 205×160 | 16% (half) | 84% | 1843 | 230 |
| `SaltmeadowHay` | high | 0.75 | mat | 82×55 | 41,29 | 0.81 m | 328×220 | 410×220 | 27% (high) | 73% | 1638 | 634 |
| `BlackRush` | high | 0.66 | clump | 40×41 | 19,29 | 0.38 m | 160×164 | 200×164 | 28% (flood) | 72% | 1848 | 230 |
| `Cattail` | high | 2.06 | clump | 90×73 | 44,60 | 0.41 m | 360×292 | 450×292 | 36% (half) | 64% | 1598 | 923 |
| `Threesquare` | high | 1.13 | clump | 38×52 | 18,40 | 0.38 m | 152×208 | 190×208 | 36% (high) | 64% | 1840 | 277 |
| `MarramGrass` | upland | 1.03 | clump | 64×66 | 31,44 | 0.69 m | 256×264 | 320×264 | 34% (high) | 66% | 1728 | 594 |
| `Bayberry` | upland | 1.81 | plant | 78×74 | 38,64 | 0.31 m | 312×296 | 390×296 | 55% (flood) | **45%** | 1658 | 811 |
| `SweetFern` | upland | 1.16 | plant | 60×60 | 29,49 | 0.34 m | 240×240 | 300×240 | 26% (ebb) | 74% | 1748 | 506 |
| `BeachPea` | upland | 0.50 | mat | 76×45 | 39,23 | 0.69 m | 304×180 | 380×180 | 32% (low) | 68% | 1668 | 480 |

**All 32 sheets fit the 2048 cap with 1338 px of headroom to spare** (SugarKelp, the largest).
Both axes, all sixteen species, at full growth: **11.41 MiB RGBA uncompressed** — and that is the
ALBEDO only; a full three-channel bake is 3×. Worst waste is **Sea Lettuce at 91%** — 471 KiB for
both its axes, which is not worth an exception. Bayberry is the tightest packed at 45%.

🔴 **If a species ever busts 2048, flip THAT species to per-tide cells as a targeted exception —
decided by the owner on these numbers, never by an importer.** `ShorePlantBaker.AssertFits` refuses
before writing a pixel, because over the cap Unity imports the sheet **silently downscaled with the
sprite count still matching**, and only a pivot assert much later would notice.

### ⚠ The pivot is the GROUND CONTACT (holdfast / root crown), and the drape hangs below it

Measured **10–43 px = 0.31–1.34 m** of art below the contact point across the sixteen species — a
drained kelp folds onto its rock and puddles downslope. A bottom-of-cell pivot floats every plant by
exactly that, and `ArtImportPipeline`'s default is wrong here for all sixteen.

Normalisation is ADR 0026's `(x/W, (H−y)/H)`. **Not** the tree kit's `pad/cellH` — the committed
contract publishes the `pivot` and no `pad`, and nothing here consumes a pad the way the tree's wind
shader consumes `_TrunkAnchor`. The rig *does* compute `pad = cellH − 1 − pivotY` internally; a test
pins that relation and a second one proves the two conventions differ by exactly one row on every
species, so an assert on the pivot has teeth rather than passing under either convention.

`ShorePlantSheetSlicer` exists rather than a `SpriteSheetSlicer` manifest row because the manifest
knows nothing about a three-channel sheet family with per-channel colour space. **Every rect on a
sheet carries the SAME pivot** — that is the whole purchase the union cell makes.

### ⚠ The baker does NOT rewrite the contract

`shorePlantRig.contract.json` is the ORACLE. `ShorePlantBaker.AssertMatchesContract` compares the
rig's live cell, pivot and both sheet dimensions against it before writing a pixel and **refuses on
any disagreement** — a rig change surfaces as a loud stop pointing at the art director, never as
sheets whose pivots quietly no longer match their own contract.

One deliberate divergence from a fresh `ShorePlants.contract()` call: the drop's committed JSON
carries `"generated": "baked from Art/shorePlantRig.js at full growth stage"` where the rig emits an
ISO timestamp. That makes the file diff-stable, and is why the tests compare **fields**, not bytes.

**WIRE-IN (NOT done here):** no sheets baked, nothing placed in a scene, no prefab, spawner, paint
tool or Def reads this contract yet, and `SpriteLightResponse.hlsl` is **not** branched on `state.B`.
The older hand-drawn `Sprites/Shore/SeaweedClump.png`, `SeaweedMat.png` and `SeaweedWisp.png` are
untouched.

---

## Batch — Acadian trees, **PASS 2** re-import (owner drop 2026-07-29)

`docs/art/rigs/treeIsoRig2.js` (`globalThis.TreeRig2`) supersedes `treeIsoRig.js`. Pass 1 built real
volume and lit it correctly, but every crown came out of one soft-ellipsoid cloud with per-pixel value
noise on top, so the family read as artichokes. Pass 2 rebuilds **what gets built and how the surface
is quantised**: crowns are 5–9 identified leaf MASSES with a hard edge where two meet (not one blended
green wall), the foliage surface is partitioned into per-species jittered Worley **leaf cells** shaded
flat from their own mean, `blob()` adds a triangular **tooth wave** over the low-order lobing with a
tooth-aware de-speckle, and broadleaves get primaries → secondaries → twigs with banded bark.

Both generations stay committed, the way `shoreIsoKitRig2.js` sits beside `shoreIsoKitRig.js`.
`_treeBake.js` is updated to the drop's version, which resolves `TreeRig2 || TreeRig` — so the art
director's harness still runs against either pass.

### ⭐ The swap was TWO CONSTANTS and a re-bake, and that is a claim with a test behind it

`TreeKitCatalog.RigScriptPath` and `.RigGlobalName` are the entire code change. Everything else —
cell, pivot, flare pad, trunk anchor, metres, the audit numbers — is read from the live rig at bake
time and was never restated in C#, which is exactly what made a whole-family art revision a two-line
diff.

`PassTwoRig_KeepsEveryContractConstant_SoTheSwapWasAReBakeAndNotAReDesign` loads **both** passes into
one V8 host (they install different globals, so they cannot collide) and asserts `PPU`, `RIM_PX`,
`MIN_BODY`, `MIN_R`, `SWAY`, `VARIANTS`, `ELEV`, `CE`, `SE`, both `LIGHT` vectors, `SEASONS`,
`STAGE_KEYS`, `STAGES` and the ten species keys **in order** are identical — then sabotages itself by
rendering Red Spruce from both and requiring the pixels to DIFFER (18,260 px → 20,034 px of coverage),
so the test cannot pass by `RigScriptPath` still pointing at pass 1.

That identity is why the sprite-light mask contract, `_TrunkAnchor` and the reflection wiring all
survived untouched. **The revised set comes through `AcadianTreeCatalog.Configure`**, so it inherits
sprite-light response (#314) and `ReflectiveObject` (#330, ADR 0027 #8) by construction — no
per-species wiring, and the prefab-shape and reflection pins stayed green through the swap.

### The geometry moved — every number below is measured off the bake, none is authored

| species | cell 1 -> 2 | trunk foot 1 -> 2 | pad | _TrunkAnchor | thinPct | m |
|---|---|---|---|---|---|---|
| `RedSpruce` | 110x166 -> **126x159** | (54,145) -> **(63,148)** | 20 -> **10** | 0.1205 -> **0.0629** | 0.6% -> **0.6%** | 5.7 |
| `BlackSpruce` | 84x156 -> **86x155** | (43,142) -> **(44,145)** | 13 -> **9** | 0.0833 -> **0.0581** | 0.4% -> **0.5%** | 5.6 |
| `BalsamFir` | 108x150 -> **125x142** | (54,128) -> **(62,132)** | 21 -> **9** | 0.1400 -> **0.0634** | 0.4% -> **0.4%** | 5.0 |
| `WhitePine` | 138x193 -> **153x191** | (69,175) -> **(76,179)** | 17 -> **11** | 0.0881 -> **0.0576** | 1.4% -> **0.2%** | 6.9 |
| `WhiteCedar` | 78x138 -> **79x141** | (41,124) -> **(41,127)** | 13 -> **13** | 0.0942 -> **0.0922** | 0.2% -> **0.4%** | 4.8 |
| `Tamarack` | 104x149 -> **91x144** | (51,132) -> **(46,134)** | 16 -> **9** | 0.1074 -> **0.0625** | 1.1% -> **5.4%** | 5.3 |
| `WhiteBirch` | 114x165 -> **117x161** | (57,146) -> **(58,151)** | 18 -> **9** | 0.1091 -> **0.0559** | 0.0% -> **0.0%** | 5.7 |
| `RedMaple` | 137x156 -> **149x150** | (68,134) -> **(74,137)** | 21 -> **12** | 0.1346 -> **0.0800** | 0.0% -> **0.1%** | 5.6 |
| `RedOak` | 169x159 -> **165x145** | (84,135) -> **(82,131)** | 23 -> **13** | 0.1447 -> **0.0897** | 0.0% -> **0.1%** | 5.3 |
| `TremblingAspen` | 88x156 -> **95x154** | (44,140) -> **(47,145)** | 15 -> **8** | 0.0962 -> **0.0519** | 0.0% -> **0.3%** | 5.6 |

30 sheets (10 species × mature/summer × albedo+mask+normal), **1544 KiB of PNG**, one sway row —
`TreeRigBaker.SwayRowsBaked` is still 1 because the shader owns the swaying off the shared
`_WindWorld`. Widest sheet is Red Oak at **660 px**, so **1388 px of headroom** under the 2048 cap;
the guard is still asserted twice (the rig's own `sheetSpec().fits` for the full 4-row sheet, and the
sheet we actually lay out).

### ⚠ The root flare HALVED, so two calibrated tests were re-derived

Pass 1 drew a broad root skirt; pass 2 draws **three splayed buttresses with dark splits**. That is a
narrower footprint by design, and it moved the pad from 13–23 px to **8–13 px** — so the trunk-foot
pivot band dropped from 0.0833–0.1447 to **0.0519–0.0922**.

Two tests carried pass-1 magnitudes and were re-measured against the new bake. **Neither assertion's
job changed**, and both are still falsifiable:

* `BottomCentre_WouldSinkEverySpecies_…` — renamed for the new range. Its own docstring anticipated
  this ("stated as a range so a re-bake that changes the flare cannot slip through"). The trap has
  NOT gone away: 8 px at PPU 32 is still a quarter-metre of visible sink, and the species that used
  to be worst (23 px) is now the best case (13 px).
* `TheDrawnHeight_…` — upper ratio bound 1.08 → 1.12. The measured span is **0.998 (Red Maple) to
  1.081 (White Birch)**, and the old cap clipped White Birch by 0.0007. A drawn height runs slightly
  above a flat height×0.766 because the cell's top row is set by the crown's SILHOUETTE, not the
  leader, and pass 2's serrated outline and hanging masses extend it further; the rig also rounds
  `metres` to 0.1 m, which is ±1% on its own. The bound still rejects the error it exists for —
  scaling to raw metres is +30.5%, refused with 18 points to spare.

⚠️ **`Tree.mat`'s single `_TrunkAnchor` 0.14 now sits ABOVE the whole band** (it was above eight of
the ten). One material-wide value over-anchors all ten species, freezing canopy that should move —
the case for the per-renderer anchor `TreeKitCatalog.TrunkAnchorFor` already supplies got stronger.

### Re-measured sabotage curves (the bake's own proof, all still green)

* **30 sheets / 120 cells are BIT-EXACT** against a fresh `TreeRig2` render. A 1-row shift breaks
  18.63% of the Red Spruce cell, so the exact comparison is not blind.
* Mask channel order **R = key · G = rim · B = depth · A = coverage** holds. An R↔G swap moves
  4,907 px = **24.49%** of a Red Spruce cell (pass 1: 5,405 px / 29.60%).
* Coverage: albedo/mask 6570 px, normal 5848 px, **keyline-only 722 px = 11.0%** (pass 1: 8.0%). The
  serrated outline has more perimeter per unit area, so the 1 px keyline ring is a larger share.
  Unchanged rule: light the keyline from the MASK, never from the normal.
* Sway frame 1 vs 0 differs by **5.18% (Balsam Fir) to 20.37% (Trembling Aspen)** — "we committed
  frame 0" stays falsifiable.

### 🔴 Flagged to the art director, not patched — **Tamarack fails the rig's own rule 1**

The pass-2 rig gates rule 1 on `audit.pass && thinPct <= 4%` — **its own threshold, read out of
`treeIsoRig2.js`**. Nine species clear it with room to spare (worst: Red Spruce 0.6%) and pass 2
IMPROVED most of them. **Tamarack alone regressed, 1.1% → 5.4%** — a 35% overshoot, not a rounding
miss. Its `bodyRatio` also fell 80 → 66.

Tamarack is the larch, the one deciduous conifer in the family, with the thinnest needle grain in
`GRAINS`. The plausible mechanism is that pass 2's Worley leaf-cell partition subdivides an
already-wispy tuft below the 5 px clump floor — but **which** is the art director's call, and the fix
belongs in the rig (`docs/art/rigs/**`, not this lane), never in the bake or in the assert.

Handled as the `SkerryD` flag from the Rock Iso import (#312): the species is named in
`TreeSheetImportTests.RigRuleFailures` and pinned by
`TamarackFailsTheRigsOwnRuleOne_AFlaggedRegressionNotAnExemption`, a **canary that goes red when the
rig is FIXED** (delete it then) and also red if Tamarack climbs past 6%. The main per-species
`audit.pass` assert still covers the other nine.

**OWNER RULING NEEDED:** ship Tamarack out of spec pending a rig fix (current state), or hold it out
of the kit until pass 3.

**WIRE-IN (NOT done here):** no scene placement (world-content's lane), no `Tree.mat`
`_LightResponse` dial-in (owner's pending verdict), no tree scale/style redesign, and the other three
stages (`sapling/young/pole`) and two seasons (`autumn/winter`) are still un-baked — the same
deliberate hundreds-of-MB decision as pass 1.
