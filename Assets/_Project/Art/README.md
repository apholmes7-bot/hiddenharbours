# Art Pipeline — How to Add an Asset (VS-23)

> The one-page checklist for adding art to _Hidden Harbours_. **Canon:** `docs/design/art-and-audio-bible.md`
> (perspective §2, scale/specs §3, palette §4, tiles §5, lighting §6, UI art §7, pipeline §9). If this
> file and the bible disagree, the **bible wins**. Owner: `art-pipeline`.

The scale fantasy (a tanker dwarfing a dory) is **load-bearing** and dies the moment a sprite is scaled
to fake its size. So: **author at true metric footprint, one locked PPU (32), and never scale a sprite to
change its apparent size — only the camera zoom changes.**

---

## ✅ Add-an-asset checklist (do all five)

1. **Author at true metric scale & the master palette.** 1 metre = 32 px (PPU 32). A 4.5 m dory is
   ~144 px long; a ~1.8 m human is ~64 px tall (heroic). Paint against
   [`Palette/HiddenHarbours-Master.gpl`](Palette/HiddenHarbours-Master.gpl) (Aseprite ▸ Palette ▸ Load
   Palette). Reserve the saturated pops for human-made colour.
2. **Put the file in the right folder** (see the map below). Export the shipped **PNG** into `Art/…`;
   keep the editable **`.aseprite`** source alongside or under a `Source/` subfolder — both are LFS-tracked.
3. **Let the import lock do its job.** Any texture under `Assets/_Project/Art/` is auto-stamped on first
   import with the locked settings (PPU 32 · Point · Compression None · mips off · sRGB · pivot · wrap)
   by `Editor/ArtImportPipeline.cs`. You normally **don't touch the importer**. To (re)apply to an
   existing/hand-tuned asset: select it ▸ **Hidden Harbours ▸ Art ▸ Apply Locked Import Settings to Selection**.
4. **Confirm LFS.** New binary types must be covered by `.gitattributes` (png, psd, tga, aseprite, ase,
   wav, ogg… already are). `git check-attr filter <file>` should print `filter: lfs`. Prefer extending a
   packed atlas over a new loose texture once you have many sprites.
5. **Swap, don't rewire.** Replacing a placeholder with final art is a **reference swap** — no code change.
   Keep the same asset path/name where possible so prefabs/refs survive.

---

## 🔒 The import lock (bible §9.2) — what gets stamped

| Setting | Value | Why |
|---|---|---|
| Texture Type | **Sprite (2D and UI)** | it's a sprite |
| **Pixels Per Unit** | **32** | the scale standard (1 px-tile = 1 m) |
| **Filter Mode** | **Point (no filter)** | crisp pixels, no blur |
| **Compression** | **None (Uncompressed)** | exact pixels & colour |
| Mip Maps | **Off** | 2D, no minification blur |
| sRGB | **On** | colour art |
| Wrap | **Clamp** (Repeat for tiling) | tiling = path under `…/Water/` or name with `_tiling`/`_repeat`/`_tile.` |
| Pivot | **feet** (Characters) · **centre** (Boats & default) | stable placement / rotation |

Enforcement: `Editor/ArtImportPipeline.cs` stamps these on **first import only** (when the `.meta` is
created), then respects any deliberate per-asset tweak you make later. It is the single source of truth —
there is no separate `.preset` to remember to apply. (A `.preset` can still be created from a locked
importer if you want the Inspector "Preset" button; the postprocessor guarantees the lock regardless.)

> `.aseprite` and `.psd` use their **own** importers (the `com.unity.2d.aseprite` / `psdimporter`
> packages), which the postprocessor can't drive. For those, set **PPU 32 · Point · Compression None ·
> Mip Maps off** manually in their Inspector. The pipeline logs a one-time reminder on first import.

---

## 🧱 Placeholder (greybox) convention — M0

Greybox art is **flat-colour blocks at honest metric footprint, labeled** — enough to prove the loop and
read the scale fantasy before we spend on real sprites.

- **Honest footprint.** A placeholder is a unit sprite **scaled to its true metres** (e.g. the dory block
  is 4.5 m × 1.8 m). Do **not** invent sizes — a person must read correctly against the dory against the
  Punt. (The greybox builder uses a white unit sprite, `Sprites/Square.png`, scaled per object.)
- **Labeled.** Name the GameObject for what it represents (`Dory`, `Wharf`, `FishingSpot`) so the greybox
  is legible without art.
- **Colour-coded** from the master palette so categories read at a glance:

| Placeholder for | Palette colour | Hex |
|---|---|---|
| Sea / open water | Slate blue (mid) | `#2F6079` |
| Land / ground | Wet sand / mud | `#B49A74` |
| Wharf / decking | Weathered wood (brown) | `#5A4632` |
| Cottage / building | Tarred timber (dark) | `#241A14` |
| Player boat (hull) | Buoy orange | `#E07B27` |
| Player / NPC | Painted-hull teal | `#2BA39A` |
| Fishing spot | Sea-glass green | `#4F8A6B` |
| Interactable marker | Oilskin yellow | `#E8B23A` |
| Hazard (rocks / sunkers) | Storm grey + Sea-foam white | `#3A4248` / `#E8EEF0` |

---

## 📐 Quick scale reference (PPU 32)

| Thing | Metres | Pixels |
|---|---|---|
| Base tile | 1 × 1 m | 32 × 32 |
| Human (heroic) | ~1.8 m, ~1 m footprint | ~64 tall, ~32 wide |
| The Dory (T0) | 4.5 m | ~144 long |
| Punt / Skiff (T1) | 6 m | ~192 long |

Full boat ladder (T0–T7, dory → tanker) is in the bible §3.3. Big hulls (T5–T7) are sectioned/atlased —
don't author them yet (stay in phase; M0/M1 is Coddle Cove + Dory + Punt).

---

## 🎥 Pixel-Perfect camera convention (bible §3.7)

One PPU never changes; only camera distance does. Cameras run a URP **Pixel Perfect Camera** with the
locked spec so there's no sub-pixel shimmer as the follow-cam tracks the boat:

- Assets PPU **32**, ref resolution **288 × 512** (portrait mobile base → ~16 m of world height, the
  intimate default zoom), **Pixel Snapping** on, Crop Frame **None**.

Apply it to a camera with **Hidden Harbours ▸ Art ▸ Configure Pixel-Perfect Camera (active scene)** or
`ArtCameraSetup.ConfigurePixelPerfect(cameraGo)` (the greybox builder already does this). World-content's
region scenes should use the same helper.

---

## 📁 Folder map

```
Art/
├── Sprites/      general sprites + the greybox unit square
├── Tilesets/     32×32 modular terrain tiles, Rule Tiles (Water/ → tiling/Repeat)
├── Characters/   player + NPC sheets (pivot = feet)
├── Boats/        hull/wake/crew layers (pivot = centre)
├── UI/           UI art (look only; layout/behaviour is ui-ux)
├── VFX/          weather/tide/foam overlays
├── Palette/      HiddenHarbours-Master.gpl (the locked 22-colour ramp)
└── Editor/       ArtImportPipeline.cs (import lock) · ArtCameraSetup.cs (camera lock)
```

**LFS:** every binary (`.png .psd .tga .aseprite .ase .wav .ogg …`) is LFS-tracked via `.gitattributes`.
Never commit one that isn't. **Grow art region-by-region with the milestones — never front-load.**
