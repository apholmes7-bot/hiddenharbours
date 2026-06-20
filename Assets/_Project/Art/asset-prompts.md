# Asset-generation prompt kit (pixel-art AI)

> Copy-paste prompts for generating Hidden Harbours pixel art that drops straight into the locked
> pipeline (VS-23). Everything here matches the original *Hidden Harbours Assets* design sheet, so new
> art stays consistent. **Stay in phase** — these cover the **Coddle Cove slice + the Punt** only; the
> M2 section at the bottom is reference, not a cue to generate now (`docs/design/art-and-audio-bible.md`,
> `agents/art-pipeline.md`).

## 1. Preamble — paste at the top of EVERY request

> **Hidden Harbours pixel art — house style (match the existing "Hidden Harbours Assets" sheet exactly).**
> ¾ top-down (Stardew-style), cozy weathered North Atlantic fishing village.
> **Scale: 32 px = 1 metre.** Draw each asset at its TRUE size in metres × 32 — never resize to "look nicer."
> **Transparent PNG, hard pixel edges, NO anti-aliasing** (no soft/blurry/semi-transparent edge pixels).
> **Palette (stay on it):** slate blues `#1E3A4C #2F6079 #4A8AA6`, fog greys `#3A4248 #6E7A80 #A7B0B3`,
> sea-foam `#E8EEF0`, weathered wood `#241A14 #5A4632 #8A6F4E`, wet sand `#B49A74`, rope/canvas `#D8C39A`,
> greens `#243A2E #4F8A6B #7FA87E`. **Saturated "pops" — use sparingly, only for human-made things:**
> lobster-buoy red `#C0392B`, buoy orange `#E07B27`, oilskin yellow `#E8B23A`, painted-hull teal `#2BA39A`,
> lighthouse warm `#F4D58D`.
> Boats are drawn **bow-up** (pointing up); characters stand with **feet at the bottom**; ground tiles are
> **seamless/tileable**. One transparent PNG per asset at the exact pixel size stated.

(Full 22-colour master palette: `Art/Palette/HiddenHarbours-Master.gpl`.)

## 2. Priority assets — Coddle Cove slice

### 🐟 Finish the six fish — `48 × 32 px` each, side profile → `Art/Sprites/Fish/`
- **Pollock** — slim greenish-grey groundfish, paler belly.
- **RockCrab** — top-down reddish-brown crab, claws spread (reads top-down, not side).
- **BlueMussel** — a small clump of 2–3 dark blue-black shells (hand-gather item).
- *(Already done: cod, haddock, mackerel, lobster.)*

### ⛵ The Punt — `64 × 192 px`, bow-up → `Art/Boats/`
- A 6 m open skiff, beamier/sturdier than the dory, painted hull (a teal `#2BA39A` or red `#C0392B` stripe
  is a good pop), small outboard at the stern.

### 🏠 Home-base buildings (¾ with a visible front wall + roof) → `Art/Sprites/Buildings/`
- **Cottage** — `~160 × 192 px`. Uncle Ned's weathered clapboard cottage; one door, two windows (can glow
  warm `#F4D58D` at night), shingled roof, small chimney.
- **WharfPost** — `16 × 40 px`, a single mooring piling/bollard.

### 🟫 Coddle Cove terrain — `32 × 32 px`, seamless → `Art/Tilesets/`
- **Sand**, **Rock**, **Grass** (heath), **Dirt** (path) — muted, low-contrast (background).
- **WharfDeck** — weathered planks.
- **Shoreline set** — `ShoreEdge` (straight water→sand edge), `ShoreCornerInner`, `ShoreCornerOuter`, and a
  `Foam` strip (sea-foam white `#E8EEF0`). Tell the generator: "tiles that line up so water meets land along
  any edge." (Seamless sea tile already done at `Tilesets/Water/SeaTile.png`.)

### 🧑 NPCs — `96 × 256 px` sheet (3 cols × 4 rows of 32 × 64 cells) → `Art/Characters/`
- **Ned** — older weathered fisherman, kind face, flat cap, oilskin *(idle-only is fine — he doesn't walk in
  the slice)*.
- **Ginny** — older woman, shawl/apron, warm.
- **Neighbour** — one extra, for variety.
- Sheet layout: **rows = facing (down / up / left / right), columns = idle / walk-1 / walk-2.**

### 🦞 Props (small life) → `Art/Sprites/Props/`
- **LobsterBuoy** — `16 × 32`, the precious **red `#C0392B`** pop (one red buoy in the grey = the signature
  image).
- **LobsterTrap** — `32 × 32`, wooden slatted trap.
- **Barrel** / **Crate** — `32 × 32` each.
- **FishingSpot** — `32 × 32`, a subtle ripple/swirl marker on water.

## 3. What to send back
Transparent PNGs at the **exact pixel sizes above** (a zip/sheet like the first one is ideal). Drop them in
the folders noted — the import lock auto-applies PPU 32 / Point / Uncompressed / mips-off on first import.

## 4. Later — M2 (Greywick & beyond) — reference only, don't generate yet
When the slice expands to Port Greywick, the same preamble plus:
- **Shipwright** — `~256 × 224 px` boat-shed with an open bay and a hull on the slip.
- **Auction/Fish-buyer stall** — `~128 × 160 px`, chalkboard prices, awning (a colour pop — Greywick is the
  *most colourful* place).
- **Greywick clapboard houses** — `~128–192 px` wide, painted fronts (teal/red/yellow), lit windows.
- **Cape Islander (T2)** — `448 × 160 px` inshore longliner. *(Bigger boats grow with the milestones.)*
