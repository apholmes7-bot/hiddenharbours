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
- **Tide-aware moving shoreline** (the headline P1 visual) — `Code/Art/TideShoreline.cs` (assembly
  `HiddenHarbours.Art`, Core-only). Attach to the water object; it reads the live tide via
  `GameServices.Environment.Sample().TideHeight` and slides the waterline (low tide exposes shore, high
  tide floods). Visual only — it never touches the tide sim (gameplay-systems').
- **Cottage day↔night** — `Code/Art/CottageDayNight.cs`. Attach to the cottage `SpriteRenderer`, assign
  `Cottage.png` / `CottageNight.png`; it swaps on `GameServices.Clock.HourOfDay`. No new Core hook needed.

Both components read the sim through Core contracts only. EditMode tests for the pure mappings live in
`Assets/Tests/EditMode/Art/`.

