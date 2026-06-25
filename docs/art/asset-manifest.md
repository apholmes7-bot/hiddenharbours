# Hidden Harbours — Art Asset Manifest

> **The owner's prioritized "draw these" checklist.** A content-authoring aid (tools-editor lane):
> a single scannable inventory of every sprite the game needs, at its locked pixel dimensions, so
> the art scope is **bounded and clear**. The owner draws his own pixel art — this tells him exactly
> *what*, at *what size*, with *what pivot/layout*, and in *what order*.
>
> **Synthesized from** (read those for the *why*): the scale standard + boat-size table in
> [`../design/art-and-audio-bible.md`](../design/art-and-audio-bible.md) §3, the already-imported set
> in [`../../Assets/_Project/Art/imported-assets.md`](../../Assets/_Project/Art/imported-assets.md),
> and the content lists in [`../design/boats-and-navigation.md`](../design/boats-and-navigation.md),
> [`../design/fish-and-content.md`](../design/fish-and-content.md),
> [`../design/world-and-regions.md`](../design/world-and-regions.md),
> [`../design/npcs-and-routines.md`](../design/npcs-and-routines.md).
>
> **This is a checklist, not a spec.** Dimensions are the *locked intent* from the bible's
> 32 px = 1 m standard; where an imported asset already exists its **actual** size is recorded (and
> wins). When in doubt, the bible (§3) is canon for scale, this doc for *status & priority*.

---

## 0. The standing spec (the locked import convention — read once)

Every sprite obeys this, no exceptions. It is the **VS-23 import lock**
(`Editor/ArtImportPipeline.cs` stamps it on first import; hand-authored `.meta`s clone it for the
headless batches).

| Setting | Value |
|---|---|
| **Scale** | **PPU 32** — 32 px = **1 metre**. Author at true metric footprint; never scale a sprite to fake size. |
| Texture type | **Sprite (2D and UI)** |
| **Filter mode** | **Point (no filter)** — crisp pixels |
| **Compression** | **None** — exact pixels/colour |
| Mip maps | **Off** |
| **Anti-aliasing** | **None** — hard pixel edges only |
| Format | **Transparent PNG** (RGBA) |
| Wrap mode | **Clamp** everywhere — **except** seamless tiles under `Tilesets/Water/` → **Repeat** |
| Palette | The 22-colour North Atlantic master ramp (bible §4.1) — cool/neutral/warm for environment, the saturated "pops" reserved for human-made colour |

**Pivots (the placement contract — pinned per category):**

| Category | Pivot | Why |
|---|---|---|
| Characters / NPCs / trees / buildings on the ground | **feet / BottomCenter** `{0.5, 0}` | Plant on the ground grid; sort correctly in ¾ view |
| Boats (hulls) | **centre** `{0.5, 0.5}` | Rotation + footprint anchor; pinned so the M2 art swap never shifts placement |
| Fish / catch icons, props, FX, UI icons | **centre** `{0.5, 0.5}` | Stable icon centring |
| Oar (special) | **LeftCenter (handle end)** `{0, 0.5}` | Rotate about the oarlock |

**Sheet rule:** multi-frame sheets slice to **clean full-cell rects** (rect = the whole cell, *not*
alpha-trimmed) so every frame shares an identical pivot and the animation never jitters.

**Boat heading is UNDECIDED (open question — bible §10.1).** Boats turn continuously, unlike
4-facing characters. Two options, to lock per tier in production:
- **(a) Pre-rendered rotation frames** at e.g. 16 or 32 headings — crisp pixels, large atlas/art cost. Recommended for hero/large hulls where engine-rotation shimmer would be ugly.
- **(b) Single top-down sprite, engine-rotated** with pixel-perfect snapping — cheap, slight shimmer. Fine for tiny boats.

All current boat art is **option (b)** single bow-up sprites (slice placeholders). The Boats section
below gives the **single-sprite size** *and* the **per-heading atlas estimate** for option (a) so the
owner can scope either. **Do not hand-draw per-heading boat art yet** — final boats come from the
**M2 pre-rendered-3D → sprite bake** (ADR-0006), swapped in by sprite-ref with pivots/footprints
preserved.

**Priority key:**
- **P1** = needed for the **current playable slice** (St Peters opening + the tide-gated sandbar walk + Port Greywick + Coddle Cove; dory/punt; the cove fish + clam dig; the sell/HUD/dialogue UI). Draw these first.
- **P2** = **M2 fleet & later regions** (T2+ hulls, offshore/outer regions, the full 100-fish catalog, instrument-region content). Banked or deferred — do not draw ahead of phase.

---

## 1. Characters

The scale anchor. **Cell = 32 × 64 px** (1 m footprint, ~2 m tall canvas, heroic bump). **4 facings**
drawn as distinct art — row 0 Down, row 1 Up, row 2 Left, row 3 Right (right is its own art, not
mirrored). Pivot **feet**. Sheets are `Multiple`, sliced by 32 × 64 cell.

| Asset | px (W×H) | Pivot | Sprite mode / sheet layout | Status | Priority |
|---|---|---|---|---|---|
| Player — walk sheet (`FisherSheet`) | 96×256 | feet | 3×4 of 32×64 (rows = facing D/U/L/R; cols = idle / walk-1 / walk-2). Walk cycle `[1,0,2,0]` @ ~230 ms | **IMPORTED** | P1 |
| Player — haul anim (`PlayerHaul`) | 96×256 | feet | 3×4 of 32×64 (same 4-facing layout, haul frames) | **IMPORTED** | P1 |
| Player — dig anim (`FisherDig`) | 128×256 | feet | 4×4 of 32×64 (rows = facing; cols = Ready / WindUp / Plunge / Scoop) — the clam dig | **IMPORTED** | P1 |
| NPC — Ned (Uncle) | 96×256 | feet | 3×4 of 32×64 (walk sheet) | **IMPORTED** | P1 |
| NPC — Ginny (Aunt / teaching figure) | 96×256 | feet | 3×4 of 32×64 | **IMPORTED** | P1 |
| NPC — Neighbour (generic islander) | 96×256 | feet | 3×4 of 32×64 | **IMPORTED** | P1 |
| NPC — Storekeeper (St Peters general store) | 96×256 | feet | 3×4 of 32×64 walk sheet | **NEEDED** | P1 |
| NPC — Silas Boyne (shipwright, Greywick) | 96×256 | feet | 3×4 of 32×64 | **NEEDED** | P1 |
| NPC — fish buyer / auctioneer (Greywick stall) | 96×256 | feet | 3×4 of 32×64 | **NEEDED** | P1 |
| Portrait — Ned | 96×96 | centre | Single — dialogue bust | **IMPORTED** | P1 |
| Portrait — Ginny | 96×96 | centre | Single | **IMPORTED** | P1 |
| Portrait — Player | 96×96 | centre | Single | **IMPORTED** | P1 |
| Portrait — Storekeeper / Silas / fish buyer | 96×96 | centre | Single — one per speaking NPC in the slice | **NEEDED** | P1 |
| NPC — the rest of the core cast (Marguerite, Reuben, Odette, Wally, Bram, Edie, Harlan, Pearl, Tomas, Iris, Joachim) | 96×256 walk + 96×96 portrait each | feet / centre | 3×4 of 32×64; portrait Single | **NEEDED** | P2 |
| Extras — layered paper-doll parts (body base + hair/hat/oilskins/boots/beard overlays) | 32×64 cells | feet | Overlay layers sharing the rig (mobile-cheap crowd variety, NPC doc §4.2) | **NEEDED** | P2 |

> **Slice need:** the St Peters + sandbar + Greywick + Cove loop needs the **storekeeper**, the
> **shipwright**, and the **fish buyer** as walk sheets + portraits (the three buy/sell faces). The
> player, Ned, Ginny, Neighbour are done.

---

## 2. Boats

Hull lengths are **locked canon** (bible §3.3 / boats doc §1.1). Hull px = metres × 32. "Single
sprite" = the current option-(b) bow-up size (what's imported). "16-heading atlas est." = a rough
option-(a) budget (single-frame area × 16) if pre-rendered rotation is chosen — for *scoping only*.
Pivot **centre**; pinned so the M2 bake swaps in without shifting placement. Each hull is a
single-frame `Multiple` sprite (no grid slice).

| Tier | Boat | Canon length | Single sprite px (imported/intended) | 16-heading atlas est. | Pivot | Status | Priority |
|---|---|---|---|---|---|---|---|
| 0 | **The Dory** (hull) | 4.5 m | **64×144** (`Dory.png`) | ~144 px cell × 16 | centre | **IMPORTED** | P1 |
| 0 | Dory — oar-rework rig: hull base (`DoryHull`) | 4.5 m | 64×144 | — | centre | **IMPORTED** | P1 |
| 0 | Dory — oar (`Oar`, used ×2, mirrored) | 1.75 m | 56×16 | — | **LeftCenter (handle)** | **IMPORTED** | P1 |
| 0 | Dory — rower figure (`DoryRower`) | ~0.8 m | 26×28 | — | centre | **IMPORTED** | P1 |
| 0 | Dory — legacy row strip (`DoryRow`, superseded fallback) | 4.5 m | 384×144 (6×1 of 64×144) | — | centre | **IMPORTED** | P1 |
| 1 | **Punt / Skiff** (hull) | 6 m | **64×192** (`Punt.png`) | ~192 px cell × 16 | centre | **IMPORTED** | P1 |
| 2 | **Cape Islander** | 13 m | 100×288 (`CapeIslander.png`) | — | centre | **IMPORTED** (slice placeholder, frozen) | P2 |
| 3 | **Lobster Boat** | 12 m | 104×268 (`LobsterBoat.png`) | — | centre | **IMPORTED** (frozen) | P2 |
| 4 | **Side Dragger / Trawler** | 25 m | 132×456 (`SideDragger.png`) | — | centre | **IMPORTED** (frozen) | P2 |
| 5 | **Stern Trawler / Seiner** | 38 m | 144×576 (`SternTrawler.png`) | — | centre | **IMPORTED** (frozen) | P2 |
| 6 | **Coastal Packet / Freighter** | 60 m | 124×620 (`CoastalPacket.png`) | — | centre | **IMPORTED** (frozen) | P2 |
| 7 | **Coastal Tanker / Cargo Ship** | 110 m | 110×640 (`Tanker.png`) | — | centre | **IMPORTED** (frozen) | P2 |

> **Note on T2+ sizes:** the imported placeholder hulls are drawn *near* canon proportion but **not
> at full metric px** (a true 110 m tanker is ~3,520 px long — see bible §3.3). The imported set is
> deliberately small, spin-tolerant placeholder art; the **M2 bake** reconciles each hull to its
> pinned metric footprint (not a quiet rescale). For the slice, only the **Dory (P1)** and **Punt
> (P1)** matter — both done.

**Boat overlays / deck (bible §3.5) — NEEDED, mostly P2:**

| Asset | px (W×H) | Pivot | Layout | Status | Priority |
|---|---|---|---|---|---|
| Wake / spray (`BoatWake`) | 64×96 | centre | Single full-cell; scaled by speed/turn (filed under `VFX/`) | **IMPORTED** | P1 |
| Crew-on-deck sprites (working anims: haul/sort) on deck anchors | 32×64 cells | feet | Per-boat deck activity; visible progression (P2 fleet) | **NEEDED** | P2 |
| Deck gear overlays (traps / nets / winch / containers) | varies | centre | Overlay sprites on hull anchors | **NEEDED** | P2 |
| Per-heading wake/spray + nav/deck lights (when boats carry real headings) | varies | follows hull | Rotate with hull or bake per-heading (bible §3.5.1 rule 3) | **NEEDED** | P2 |

**Roster / fleet thumbnails (UI):**

| Asset | px | Pivot | Status | Priority |
|---|---|---|---|---|
| Roster icons — Dory, Punt | (per file) | centre | **IMPORTED** | P1 |
| Roster icons — CapeIslander, LobsterBoat, SideDragger, SternTrawler, CoastalPacket, Tanker | (per file) | centre | **IMPORTED** (frozen, fleet UI is M2) | P2 |

---

## 3. Fish & catch

Catch icons. The cove/slice species are **48×32** (1.5 m × 1 m), centre pivot, Clamp, `Single`.
The full catalog targets **100 species** (fish doc §5) — only a handful are in the slice. Assigned
to a `FishSpeciesDef.spriteRef`; shown in the catch card + sell screen.

| Asset (species) | id | px (W×H) | Pivot | Mode | Status | Priority |
|---|---|---|---|---|---|---|
| Atlantic Cod | `fish.atlantic_cod` | 48×32 | centre | Single | **IMPORTED** (sprite assigned) | P1 |
| Haddock | `fish.haddock` | 48×32 | centre | Single | **IMPORTED** (assigned) | P1 |
| Atlantic Mackerel | `fish.mackerel` | 48×32 | centre | Single | **IMPORTED** (assigned) | P1 |
| American Lobster | `fish.lobster` | 48×32 | centre | Single | **IMPORTED** (assigned) | P1 |
| Soft-shell Clam | `fish.soft_shell_clam` | 48×32 | centre | Single | **IMPORTED** (assigned) | P1 |
| Pollock | (fish.pollock) | 48×32 | centre | Single | **IMPORTED** (sprite on disk, not yet in IconLibrary) | P1 |
| Rock Crab | (fish.rock_crab) | 48×32 | centre | Single | **IMPORTED** (on disk) | P1 |
| Blue Mussel | (fish.blue_mussel) | 48×32 | centre | Single | **IMPORTED** (on disk) | P1 |
| Cusk, Herring, Capelin, Bluefin Tuna, Smelt, Eel, Halibut, Redfish, Monkfish, Striped Bass, Lumpfish, Snow Crab, Sea Scallop, … (the rest of the 24 seed species) | per id | 48×32 (finfish) / sized for shellfish | centre | Single | **NEEDED** | P2 |
| The remaining ~76 species to reach 100 (incl. 8 legendary/cryptid: Sunker King, Fundy's Grey Mare, Drownded Bride, Smother Lantern, +4) | per id | 48×32+ | centre | Single | **NEEDED** | P2 |

> **Slice note:** the cove + St Peters clam economy reads from Cod/Haddock/Mackerel/Lobster + the
> clam — all imported and assigned. Pollock/RockCrab/BlueMussel sprites already exist on disk (a head
> start) but aren't in `IconLibrary.asset` yet (a wiring task, not an art task). Everything else is
> P2 and **must not be drawn ahead of the catalog phase** (M3/M4 for the full 100).

---

## 4. Terrain tiles

Modular **32×32** ground tiles (= 1 m²), centre pivot, Clamp wrap, made into `Tile`/`RuleTile`
assets by `Art/Editor/TileAssetBuilder.cs`. One biome atlas per environment.

| Tile | px | Pivot | Mode | Status | Priority |
|---|---|---|---|---|---|
| Sand | 32×32 | centre | Single → Tile | **IMPORTED** | P1 |
| Grass | 32×32 | centre | Single → Tile | **IMPORTED** | P1 |
| Rock | 32×32 | centre | Single → Tile | **IMPORTED** | P1 |
| Dirt | 32×32 | centre | Single → Tile | **IMPORTED** | P1 |
| Wharf decking | 32×32 | centre | Single → Tile | **IMPORTED** | P1 |
| **Wet sand / mud flats** (St Peters + Drownded Lands bared seabed) | 32×32 | centre | Single → Tile; tide-exposed walkable | **NEEDED** | P1 |
| **Cobble / dirt road** (Greywick town, St Peters paths) | 32×32 | centre | Single → Tile | **NEEDED** | P1 |
| Kelp / weed (Sunkers, tide-pool shelves) | 32×32 | centre | Single → Tile | **NEEDED** | P2 |
| Heath / moor, interior floors, breakwater stone (region biomes) | 32×32 | centre | Single → Tile per biome | **NEEDED** | P2 |

> **Slice gap:** St Peters' bared **sandbar/flats** and Greywick's **town road/cobble** are the two
> P1 terrain tiles not yet imported. The five cove tiles (sand/grass/rock/dirt/wharf) are done.

---

## 5. Shoreline & water

The headline P1 visual — the **tide-aware moving shoreline**. Sea tile is **seamless / Repeat**;
shoreline pieces autotile via a `RuleTile`. Pivot centre.

| Asset | px | Pivot · wrap | Mode / layout | Status | Priority |
|---|---|---|---|---|---|
| Sea tile (`SeaTile`) | 64×64 | centre · **Repeat** | Seamless tiling water surface | **IMPORTED** | P1 |
| Shoreline edge (`ShoreEdge`) | 32×32 | centre · Clamp | Autotile edge | **IMPORTED** | P1 |
| Shoreline inner corner (`ShoreCornerInner`) | 32×32 | centre · Clamp | Autotile corner | **IMPORTED** | P1 |
| Shoreline outer corner (`ShoreCornerOuter`) | 32×32 | centre · Clamp | Autotile corner | **IMPORTED** | P1 |
| Foam (`Foam`) | 32×32 | centre · Clamp | Autotiled foam where water meets land/hull/rock | **IMPORTED** | P1 |
| **Wet-transition ring** (glistening mud/weed/tide-pool at the moving tide edge) | 32×32 | centre · Clamp | Autotile band drawn at the falling-tide edge (bible §2.2) | **NEEDED** | P2 |
| **Breaking white water** on sunker rocks (hazard tell) | 32×32 | centre · Clamp | Animated overlay on reef rocks (P5 tell) | **NEEDED** | P2 |
| Sub-surface parallax depth layers (1–2 darker layers under the surface) | tileable | centre · Repeat | Depth gradient beneath the sea tile | **NEEDED** | P2 |

> The shader-driven hero water (caustics/specular/foam layers, pixelized) is an **art-pass / M2–M3**
> effort with its own plan ([`../design/water-rendering.md`](../design/water-rendering.md)) — **not a
> hand-drawn sprite deliverable** and out of scope for this manifest. The slice ships the imported
> sea tile + the autotiled shoreline/foam above.

---

## 6. Buildings

Drawn with front faces + rooflines (¾ view), sized to metric footprint, pivot **feet**
(BottomCenter), placed on a Y-sorted layer. Day/night swaps are two sprites.

| Building | px (W×H) | Pivot | Mode | Status | Priority |
|---|---|---|---|---|---|
| Cottage — day (`Cottage`) | 160×192 | feet | Single (day↔night swap) | **IMPORTED** | P1 |
| Cottage — night (`CottageNight`) | 160×192 | feet | Single | **IMPORTED** | P1 |
| Shipwright shed (`ShipwrightShed`, Greywick) | 256×224 | feet | Single | **IMPORTED** | P1 |
| Fish-buyer stall (`FishBuyerStall`, Greywick) | 128×160 | feet | Single | **IMPORTED** | P1 |
| Greywick house — red (`GreywickHouseRed`) | 144×184 | feet | Single | **IMPORTED** | P1 |
| Greywick house — teal (`GreywickHouseTeal`) | 160×176 | feet | Single | **IMPORTED** | P1 |
| **St Peters — school** (one-room, the opening's teaching anchor) | ~160×192 | feet | Single | **NEEDED** | P1 |
| **St Peters — general store** (basic gear / licence point) | ~160×192 | feet | Single | **NEEDED** | P1 |
| **St Peters — 3 clapboard houses** (village) | ~128–160 wide | feet | Single (can reuse/recolour cottage) | **NEEDED** | P1 |
| Greywick — auction house / fish market | ~256 wide | feet | Single | **NEEDED** | P2 |
| Greywick — chandlery, tavern, harbourmaster office, chart shop, processing plant, church, lighthouse | varies | feet | Single each | **NEEDED** | P2 |
| Outer-region structures (Ironbound lighthouse, wrecks, Smother foghorn structure, freight terminal) | varies | feet | Single each | **NEEDED** | P2 |

> **Slice gap:** St Peters' **school**, **general store**, and **3 houses** are the P1 buildings not
> yet drawn (the houses can recolour the existing cottage to save art). Greywick's two houses +
> shipwright shed + buyer stall are done; the cove cottage is done. Greywick's *fuller* service
> buildings (tavern, auction house, etc.) are P2.

---

## 7. Props / decor

Individual sprites on a Y-sorted layer, pivot **centre** (props) unless noted, Clamp.

| Prop | px (W×H) | Pivot | Mode | Status | Priority |
|---|---|---|---|---|---|
| Lobster buoy (`LobsterBuoy`) | (per file) | centre | Single | **IMPORTED** | P1 |
| Lobster trap (`LobsterTrap`) | (per file) | centre | Single | **IMPORTED** | P1 |
| Barrel (`Barrel`) | (per file) | centre | Single | **IMPORTED** | P1 |
| Crate (`Crate`) | (per file) | centre | Single | **IMPORTED** | P1 |
| Wharf post (`WharfPost`) | (per file) | centre | Single | **IMPORTED** | P1 |
| Fishing spot anchor (`FishingSpot`) | 32×32 | centre | Single — cast/fish interactable | **IMPORTED** | P1 |
| Clam hole (`ClamHole`) | 32×32 | centre | Single — dig spot (two holes in sand) | **IMPORTED** | P1 |
| Trees (`Tree01`–`Tree37`, four-season decor pack) | 64×64 each | **BottomCenter** | Single each (37 sprites) | **IMPORTED** (free-to-use, owner's pack; AI-disclosure flagged for credits) | P1 |
| **Mooring cleat / post / tie-item** (rope anchor prop) | ~32×32 | centre | Single (future mooring §9.6) | **NEEDED** | P2 |
| Nets / drying racks / dories-on-the-hard / slipway clutter | varies | centre | Single each — working-coast dressing (P3) | **NEEDED** | P2 |
| Region landmarks (navigation spindle, range markers, bell/whistle buoys, wreck timbers, refuge marker) | varies | centre/feet | Single each | **NEEDED** | P2 |

---

## 8. UI icons

HUD instruments + glyphs. Authored at integer scale, Point, snapped; `ui-ux` may override reference
PPU per-asset for a Canvas. The **`IconLibrary` ids** are the registered seam (id → sprite via
`Core.IconRegistry`). Pivot centre.

**HUD instruments & frames (imported):**

| Asset | px (W×H) | Status | Priority |
|---|---|---|---|
| Tide gauge (`TideGauge`) | 48×96 | **IMPORTED** | P1 |
| Tide arrows up/down (`TideArrowUp`/`Down`) | 16×16 | **IMPORTED** | P1 |
| Wind compass (`WindCompass`) | 64×64 | **IMPORTED** | P1 |
| Clock sun / moon (`ClockSun`/`ClockMoon`) | 24×24 | **IMPORTED** | P1 |
| Coin icon (`CoinIcon`) | 16×16 | **IMPORTED** | P1 |
| Hold icon (`HoldIcon`) | 24×24 | **IMPORTED** | P1 |
| Dialogue panel (`DialoguePanel`) | 208×104 | **IMPORTED** | P1 |
| Name plate (`NamePlate`) | 92×28 | **IMPORTED** | P1 |
| Tension gauge (`TensionGauge`) | 64×40 | **IMPORTED** | P1 |
| Line hook (`LineHook`) | 16×28 | **IMPORTED** | P1 |
| Fish-on silhouette (`FishOnSilhouette`) | 32×24 | **IMPORTED** | P1 |
| Sell chalkboard (`SellChalkboard`) | 208×144 | **IMPORTED** | P1 |
| Button (`Button`) | 76×28 | **IMPORTED** | P1 |

**`IconLibrary` registered ids (the catalog the UI reads by id — all IMPORTED & registered):**

| id | Source sprite | Drawn @ | Status | Priority |
|---|---|---|---|---|
| `fish.atlantic_cod` | Cod | 48×32 | **IMPORTED** | P1 |
| `fish.haddock` | Haddock | 48×32 | **IMPORTED** | P1 |
| `fish.mackerel` | Mackerel | 48×32 | **IMPORTED** | P1 |
| `fish.lobster` | Lobster | 48×32 | **IMPORTED** | P1 |
| `fish.soft_shell_clam` | SoftShellClam | 48×32 | **IMPORTED** | P1 |
| `gear.rod` | Rod | 48×32 | **IMPORTED** | P1 |
| `gear.shovel` | Shovel | 32×32 | **IMPORTED** | P1 |
| `gear.bucket` | ClamBucket | 32×32 | **IMPORTED** | P1 |
| `license.cod` | (reuses Cod) | 48×32 | **IMPORTED** | P1 |
| `boat.dory` | Dory roster icon | (roster px) | **IMPORTED** | P1 |
| `boat.punt` | Punt roster icon | (roster px) | **IMPORTED** | P1 |
| `ui.coin` | CoinIcon | 16×16 | **IMPORTED** | P1 |
| `ui.hold` | HoldIcon | 24×24 | **IMPORTED** | P1 |

**UI icons NEEDED (not yet drawn / not yet an id):**

| Asset | Suggested px | Status | Priority |
|---|---|---|---|
| Clam-licence icon (distinct from `license.cod`) | 48×32 / 24×24 | **NEEDED** | P1 |
| Stamina / energy glyph | 16–24 px | **NEEDED** | P1 |
| Barometer (forecast) HUD | ~48×48 | **NEEDED** | P2 |
| Chart / map, compass binnacle, ledger/manifest, brass throttle (diegetic screens) | varies | **NEEDED** | P2 |
| Instrument icons (depth sounder, radar, GPS, marine radio) | 24–32 px | **NEEDED** | P2 |
| Roster/boat-buy & gear-shop screen frames | varies | **NEEDED** | P2 |
| Almanac entries, trophy/record glyphs, contract/commodity icons | varies | **NEEDED** | P2 |

> UI icons must read at **24–32 px** (bible §7). The slice's HUD/sell/dialogue/fishing UI is fully
> imported. The only **P1** UI gaps are a distinct **clam-licence** glyph and a **stamina** glyph.

---

## 9. FX

World/boat effect overlays (filed under `VFX/`, not `UI/`). Pivot centre; sheets sliced full-cell.

| Effect | px (W×H) | Pivot | Mode / layout | Status | Priority |
|---|---|---|---|---|---|
| Boat wake (`BoatWake`) | 64×96 | centre | Single — scaled by speed/turn | **IMPORTED** | P1 |
| Catch sparkle (`CatchSparkle`) | 72×24 | centre | 3×1 of 24×24 (good-catch celebration) | **IMPORTED** | P1 |
| Wind pennant (`WindPennant`) | 160×48 | centre | 4×1 of 40×48 (wind-direction tell) | **IMPORTED** | P1 |
| Clam squirt (`ClamSquirt`) | 128×32 | centre | 4×1 of 32×32 (the "live clam" tell on the flats) | **IMPORTED** | P1 |
| **Splash / ripple** (cast hit, boat nudge) | ~32–64 | centre | Small flipbook | **NEEDED** | P1 |
| Rain / snow particle overlays | tileable | — | Lightweight particle sheets (weather) | **NEEDED** | P2 |
| Fog overlay (animated, parallax, density-graded) | tileable | — | Overlay layer (Smother / weather) | **NEEDED** | P2 |
| Cloud-shadow patches (drift with wind) | tileable | — | Scrolling soft overlay (M3 ambition) | **NEEDED** | P2 |
| Lightning rim-flash, spray-aboard / swamping splash, lighthouse beam glint | varies | centre | Per-effect | **NEEDED** | P2 |

> The slice's feel-FX (wake, sparkle, pennant, clam squirt) are imported. A small **splash/ripple**
> is the one P1 FX nicety worth adding; the rest (weather/fog/cloud FX) are P2 and several lean on
> shader/particle work rather than hand-drawn frames.

---

## 10. Summary — what's done, what to draw

### Counts (distinct asset rows above; the 37-tree pack counts as one entry)

| Category | DONE (imported) | NEEDED |
|---|---:|---:|
| Characters | 9 | 6 |
| Boats (hulls + rig + overlays + roster) | 14 | 6 |
| Fish & catch | 8 | 2 (groups: ~16 seed + ~76 catalog) |
| Terrain tiles | 5 | 4 |
| Shoreline & water | 5 | 3 |
| Buildings | 6 | 8 |
| Props / decor | 8 | 3 |
| UI icons (instruments + ids) | 26 | 9 |
| FX | 4 | 5 |
| **Totals** | **~85** | **~46 (rows; many P2 are large catalogs)** |

The slice is **art-rich already** — the entire cove loop, the dory/punt, the HUD/sell/dialogue/
fishing UI, the clamming kit, and the tree pack are imported and (mostly) wired.

### P1 — the owner's "draw these first" list (for the current playable slice)

Everything needed to finish **St Peters → sandbar → Greywick → Cove**. Short and concrete:

1. **St Peters buildings** — the one-room **school**, the **general store**, and **3 clapboard houses** (houses can recolour the existing cottage). *(§6)*
2. **St Peters NPCs** — the **storekeeper** walk sheet + portrait. *(§1)*
3. **Greywick buy/sell faces** — the **shipwright (Silas)** and the **fish buyer/auctioneer** walk sheets + portraits. *(§1)*
4. **Two terrain tiles** — **wet sand / mud-flat** (the bared sandbar + clam flats) and a **town road / cobble** tile. *(§4)*
5. **Two small UI glyphs** — a distinct **clam-licence** icon and a **stamina** glyph. *(§8)*
6. **One FX** — a small **splash / ripple** flipbook. *(§9, optional polish)*

That is roughly **a dozen sprites** to complete the slice's art. Everything else is **P2** — M2 fleet
hulls (already banked as frozen placeholders), the rest of the 14-NPC cast, the full Greywick service
buildings, the outer regions, and the long tail of the 100-fish catalog and instrument-region content.
**Stay in phase: do not draw P2 art ahead of the roadmap.**
