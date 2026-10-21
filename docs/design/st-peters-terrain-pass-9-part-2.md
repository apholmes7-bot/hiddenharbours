# St Peters, terrain pass 9, part 2: the plan

> - **Status:** PLAN, for the owner's decisions (§13). Session 1 of part 2. Docs only: no code, no asset, no scene and no rig bytes.
> - **Lane:** art-pipeline, wearing world-content's hat for the island plan. Charter: `seat/HANDOFF-2026-09-25-terrain-pass-9-part-2.md`.
> - **Base:** part 1, `docs/design/st-peters-terrain-pass-9.md` (#881, squashed to main as `c25d952d`), all twelve of its decisions ruled on 2026-09-25. Where part 1 already answers something, this doc cites it and does not redo it.
> - **Pillars:**
>   - **P1 Sea Has Moods:** a spring low now bares a ring of low tidal ground round the whole island, and the tide opens and shuts the crossing, the flats' walks and the guts.
>   - **P3 Living Working Coast:** the cliff coast gets named stacks, gaps, reefs and a storm beach; three brooks run to the sea; the landing reads as a harbour held between two arms.
>   - **P5 Cozy but with Teeth:** the flats under the cliffs can be walked for under two hours of a spring low and never at neaps, the guts turn boat-only as the flood makes, and the crossing's pools make the walk wind.
> - **Siblings:** `docs/design/seabed-and-terrain-look.md` · `docs/architecture/grass-field.md` · `docs/design/time-tides-weather.md` · ADRs 0003, 0004, 0010, 0011, 0012, 0014, 0019, 0021, 0028.
> - **The kit:** `Pixel art capabilitiescliffrockkitv6.zip`: 3,681,139 B, sha256 `aa632e3cfc899eef274963ee2a70fbe399f06c010e3224a2a53ae9dbb232bb20`.
> - **Pictures:** 37 PNGs, 2.64 MB in all, in LFS under `docs/design/st-peters-terrain-pass-9-part-2/` (listed in Appendix B).

---

## 0. In plain words

🔴 **The stop comes first.** The charter (§3.10) says to report it now.

The new shores stand under **five St Peters layers that change only through `StPetersBuilder.Build()`**, which is never run on St Peters (ADR 0019, `RegionBuildGuard`):
- the shore plants, the shrubs and the flowers;
- the shoreline rocks (`Shoreline`);
- the clam holes (`ClamHoles`).

Part 1 hit the same wall, and the owner ruled its way through (part 1's decision 10 (a)). That ruling covers part 2 without a new kind of step:
- **The three plant roots retire at St Peters** with the grass field (part 1's decision 3 (b)), so they need nothing.
- **Part 1's PR 4b already refreshes the shore rocks and the clam holes in place.** Part 2 widens those two steps to the whole island and adds one sub-step: nine of today's shore rocks keep their places and take the kit's rock forms (§4.4).
- The third PR 4b step, the nav marks' records, gains nothing: no mark's depth changes beyond part 1's.

Two more things would add Build()-only work, and the plan avoids both:
- **The new stacks, skerries and rock scatters** are new objects. Placed by a builder, they would be one more Build()-only root. The plan spawns them at load from their Defs instead, as the plant field is, so they never go stale (§10).
- **Cliff option (a)** would replace the `CliffWalls` root, which is Build()-only and is the CLIFFS lane's. PR 4b cannot take a step for that, because it is not a refresh. That is one reason the recommendation is option (b) (§3).

This PR does not work around the stop. It runs no `Build()`, edits no scene and adds no code. **Building waits for the owner's word on decision 11 (§13).**

**What the plan builds:**
- **A ring of low tidal ground round the island (the owner's ask of 2026-09-25).** The South Flats run from the harbour mouth round the south to the bar root, where they meet part 1's Bar-Head Flats.
  - With part 1's Lagoon Flats, North Cove terrace, Ginny's Cove and Cannery terrace, they ring the island, broken only at the harbour mouth, where the arrival route and the dredged approach are frozen.
  - They cover 12,382 m². The south's intertidal grows from 2,363 m² to 13,988 m².
  - They lie between −0.45 and −1.85 m, close to the Lagoon Flats' band: a spring low bares them, every high floods them, and a neap low bares only their inner part.
  - **The cliffs stand behind them and do not move.** The flat keeps out of a 3.5 m moat at every wall's toe (0 pixels inside it), so it never ramps up a face. The channel at each wall's foot is exactly today's: where it holds water at a spring low today (South Arm, Weather Cliff, South-West Bluff) it still does, and the ledges' benches bare as they do today.
  - The flats have their own detail: drains with tributaries, 12 pools, 16 hollows that hold the ebb, rock ribs under the Weather Cliff, mussel beds, and two guts that let the flood in first.
- **Eleven named sections from the cannery round to the bar head:** Harbour Strand, The Landing, South Arm, East Gap, East Ledges, Weather Cliff, West Gap, West Ledges, South-West Bluff, Storm Beach and the Bar root.
  - The gaps get shingle spits and pocket beaches. The ledges get a reef with gullies and rock pools. The south-west corner gets a storm beach with a berm.
- **The cliff & rock kit v6 joins the walls (option (b), recommended):**
  - four stacks: the Harbour Stack, the Old Man and the two Sisters;
  - reef benches at the ledges' toes;
  - 103 rock sprites from #840's sheets, already in the repo: 87 in scatters, 9 of today's shore rocks re-drawn in place, and 7 skerries (the Whelps and the two gaps' skerries);
  - the kit's floor light (`occ` and `skyv`).
  - The 79 walls stay the cliffs, and CLIFFS' Phases C and D go ahead as chartered.
- **Water:**
  - three brooks (Gap, Heath and Storm), each out of a pond;
  - three tidal creeks across the flats;
  - the guts and the pools.
  - All of it is real water in PR 4's still-water seam (part 1's decision 6).
- **The crossing to Nine Mile Creek** gets the north shore's low-tide detailing and 23 small pools. With the crest pools (layout B), the walk winds: 2 swings on the quickest line and 12 along the crest top.
  - **The tide window does not change.** It is set by the gut, which is frozen.
- **The landing is kept and framed** at the intro's own clock (06:00 on day 1, tide −0.85 m and falling). A 6 m dock shift is drawn as a second picture only, with its costs.
- **Paths:** a cliff walk round the south, from the slip road to the bar-head road, 6 m inside the brows; and a path down each gap.
- **Plants:** the whole island comes to 23,180 plants in a field of 72,879 B. The worst screen is still on the north shore, inside the budget test's limits.
- **Nothing that must not move moves.** This covers:
  - the landing, the wharf, the berths, the dredged approach and the arrival route;
  - the buildings, roads, yards, the woods and the 79 walls;
  - the bar's crest outside the pools, the gut and the passages.

  All of it lies inside the plan's frozen mask (338,011 cells of 0.5 m, under layout B), where the ground changes by 0.0 m against part 1's plan. The walls, the trees, the landing's points and part 1's paths are also checked one by one (§2.3).

**Findings the charter did not list:**
1. **Nothing stops a walker at a cliff today.** `TidalWalkability` is depth-only, with no slope rule, and a wall is "pure look" (`CliffWallSurface`), with no collider. So the flats would let a player walk straight through a stack at a low. Each form therefore carries a footprint collider from its bake sidecar (the player is a `Rigidbody2D`, so a static collider stops it). §2.4, §4.4.
2. **`seaDir` only flips the swell along screen Y** (`terrainLight6.js:236`). The east and west shores have no correct value (§1.5).
3. **The kit's face compose clamps a face's foot to the nearest form's top line.** Behind a stack or bench, the face reads as one bed. The previews work round it from outside the kit (§1.7).
4. **The fleet.** The flats cut the ambient fleet rect's 13 deep cells off from the harbour at a spring low (part 1: all 13 joined). This is for gameplay-systems, alongside part 1's decision 12 (§12; decision 12 in §13).

**What I would like the owner to look at first:**
- `plan-island.png` and `plan-south.png` for the ring and the south;
- `preview-flats-weather.png` for the Old Man on the flats at a spring low;
- `option-a.png`, `option-b.png` and `option-c.png` for the cliffs;
- `crossing.png` and `crossing-window.png` for the crossing;
- `preview-landing.png` for the intro.

Then the decisions in §13.

---

## 1. Phase 0: the kit

### 1.1 Identity
- **The zip:** `Pixel art capabilitiescliffrockkitv6.zip`, as the owner downloaded it: 3,681,139 B, sha256 `aa632e3c…bb20`. It matches the charter.
  - 31 files under `export/cliff-rock-kit-v6/`: 16 `.js`, 13 `.png`, `README.md` and `viewer.dc.html`. Uncompressed, 4,244,924 B.
  - Unzipped to `Evidence~/kit-v6/`, never committed. Every file's sha256 is in `Evidence~/kit-v6.sha256`.
- **No SHA256SUMS and no checkers** in the zip (§1.6).
- **Load order** (the README's), each rig and its global:

  | File | Global |
  |---|---|
  | `lib/pixelLanguage.js` | `PxLang` |
  | `lib/pxKit.js` | `PxKit` |
  | `lib/pxKit2.js` | `PxKit2` |
  | `lib/weatherSky.js` | `WeatherSky` |
  | `lib/terrainLight4.js` | `TerrainLight4` |
  | `lib/pxKit8.js` | `PxKit8` |
  | `lib/terrainLight5.js` | `TerrainLight5` |
  | `terrainLight6.js` | `TerrainLight6` |
  | `lib/plantIsoRig4.js` | `PlantRig4` |
  | `lib/plantIsoRig5.js` | `PlantRig5` |
  | `lib/treeIsoRig4.js` | `TreeRig4` |
  | `lib/pxRockIso2.js` | `PxRockIso2` |
  | `pxCliff3.js` | `PxCliff3` |
  | `pxRockIso3.js` | `PxRockIso3` |
  | `pxCliffScenes.js` | `PxCliffScenes` |

- **New bytes:** `pxCliff3.js` (43,471 B), `pxRockIso3.js` (11,322 B), `pxCliffScenes.js` (35,365 B) and `terrainLight6.js` (25,056 B). The rest equal copies main or part 1's drop already has, as the seat checked (charter §0.3).
- **The harness** is `Evidence~/harness/v6.js`. It loads the kit in that order into one V8 context and drives it only through its public API. It never edits a kit file.

### 1.2 Node checks
All on Node 24.19.0.

**The re-render.** Each of the four scenes (headland, benches, bluff and tor), at each of the three looks in `renders/`, at 520 × 450:
- **All 12 stills are pixel-exact:** 0 pixels differ from the kit's PNGs, compared pixel by pixel in the harness. Non-blank controls confirm the comparison is real: every still is fully opaque (234,000 pixels), with the same colour count as the kit's to within one.
- The settings that reproduce them: wind 0.20, gust 0.30, direction 1, frame 0; tide 0.30 (1.20 m of the kit's range) for the afternoon and snow looks, 0.55 for the golden one; snow drawn as winter.
- The snow preset's own gust (0.20) leaves 35 to 88 pixels of falling snow different in each snow still; at 0.30 all four are exact. So the viewer drew them at gust 0.30.
- **Not reproduced: `options_sheet.png`.** The best sky found (time 16.45, cloud 0.05, fog 0.1) still differs in 40% of its pixels. It is outside the required set (the four scenes at three looks), so it is a finding, not a stop. Its settings go in the Claude Design paste (§13, decision 9).

**TerrainLight6 against TerrainLight5, with `occ`, `skyv` and `seaDir` unset: identical in 110 of 110 cases.**
- The cases: part 1's five `PxKit9` scenes × 11 presets × frames 0 and 7.
- The controls prove the inputs do something:
  - `occ` over half the frame moves about 108,000 to 114,000 pixels;
  - `skyv` = 0.5 moves 2,400 to 5,600 pixels;
  - `seaDir` = −1 moves 0 pixels at frame 0, and 1,535 to 1,549 pixels (coast) and 660 to 668 pixels (marsh) at frames 4 and 7, because it only shifts the swell's phase.
- So PR 2's own proof should agree (§11).

**Measured (`PxCliff3.field`, and one full relight):**

| Scene | Highest field Z (kit m) | Highest ground (kit m) | Relight | Frame | Build |
|---|---:|---:|---:|---:|---:|
| headland | 5.381 | 8.885 | 118 ms | 62 ms | 804 ms |
| benches | 3.198 | 5.488 | 109 ms | — | — |
| bluff | 4.709 | 8.306 | 120 ms | — | — |
| tor | 3.200 | 4.810 | 124 ms | — | — |

- **The kit's datum is not St Peters'.** Kit heights are measured from a spring low: the kit's tide runs 0 to 4 m for St Peters' −2.2 to +2.2. Part 1's rule converts: kit m = (E + 2.2) × 4.0 / 4.4. So the headland's 8.885 kit m is +7.57 m at St Peters.
  - That is above part 1's decision 11 (+7 m), **but only in the kit's own demo scene.** This plan's ground tops out at +6.65 m (part 1's barren outcrops). Its stacks are sprites, not ground (§3.1, the height range).
- The option vignettes, at the 7 m slider, reach about 10.6 kit m.
- A cold relight takes 134 to 401 ms.
- Buffers per scene: field about 8.2 MB, stage 13.4 to 17.9 MB, G-buffer 13.05 MB.
- Face pixels per scene: headland 86,080, benches 44,976, bluff 101,696, tor 29,725.

### 1.3 Table 1: the rocks

| Rock | v6 (`pxCliff3`) | Main's px cliff face kit | #840 (Rock Px) |
|---|---|---|---|
| **sandstone** | `#a6583b`, contrast .95, bedded, porosity .36, joints at azimuths 14° and 101°, spacing [2.1, 1.55] m, jitter .38 | `Sandstone_*`: `#a6583b`, contrast 1.0 | contrast .95 |
| **till** | `#8a5a3e`, contrast .85, smooth | the same (the Overburden band) | — |
| **basalt** | `#5a6169`, contrast 1.0, a columnar lattice | the same | — |
| **granite** | `#6f7169`, contrast .9, massive (sheeting) | none | `#6f7169`, contrast .90 |
| **quartzite** | — | — | `#8d8272`, contrast .90, rock only |

- `pxRockIso3`'s porosity table (sandstone .34, till .30, basalt .10, granite .08, quartzite .06) is its own, not `pxCliff3`'s.
- **Joints differ in kind.** Main's are pixel spacings (`jsp` [17, 13, 9, 6]); v6's are a 3-D lattice in metres. A v6 face and a px wall of the same rock will not show the same joint rhythm.
- **v6's `lib/pxRockIso2.js` is #840's rig, byte for byte** (sha256 `3b6ec36d…` equals #840's `derivedFromRigSha256`). So "the new rock art" and #840 are one art, and v6's rocks need no new sheets.
- Every main wall shows till (the Overburden) above sandstone, as v6's bluff does.

### 1.4 Table 2: the faces
v6 computes a face buffer per pixel (it bakes nothing yet). Here is each field against today's cliff maps and the inputs of `HiddenHarboursCliffFace.shader`:

| v6 face-buffer field | Main's channel |
|---|---|
| `pal` (palette index) | `_index`.R |
| `band` (the stratum) | `_index`.G |
| `ao` | `_normal`.A |
| `sn` (snow) | `_mask`.G |
| `nx`, `ny`, `nz` | `_normal` (tangent space, per wall) |
| `z`, `eA` (height, arris) | none: a wall takes its height from its quad and `_HHSeaLevelWorld` |
| `fl` (flags) | none, except `_index`.B |
| `por` (porosity, how wet it reads) | none |
| `ar`, `an` | none |
| `fx`, `fy` (the face direction) | the quad's normal |
| `below` (under the water) | none (`_SubmergedTint` tints the whole wall) |
| cast shadow | v6: a live `sunAt` ray march. Main: baked into `_mask`.R at `_PxKey` (−0.5477, −0.6572, 0.5178), one sun only |

- `_mask`.B (depth) has no v6 counterpart.
- **Main's px kit** (`docs/art/rigs/px-cliff-face-kit`, #866): 180 face sets (3 rocks × 5 aspects × 4 batters × 3 wear steps), of which its README says the standing bake holds 90, as 360 PNGs. Faces are 384 × 288 px (12 × 9 m); the palette LUT is 8 × 32; batters 90°, 76°, 62° and 48°. The px look sits behind #866's bake switch (`CliffCatalog.PxKeyword`, `_HH_CLIFF_PX`), and v10 stays the shipped default. Baked faces go to `Art/Terrain/Cliffs/`, which is gitignored (`.gitignore:127`), so CI has none.

### 1.5 Table 3: TerrainLight6's inputs

| Input | What it is | What the engine must feed it, and from which data |
|---|---|---|
| **`occ`** | the cliffs' and stacks' sun shadow on the floor: a mask, 0 or 1 per floor texel (`Uint8`), 1 where the sun is blocked. In the kit it is `PxCliff3.occlusion` = 1 − `sunAt` | **A map recomputed on the slow tick** (rule 7), because the sun moves. Its data: the painted height (R16, PR 4), the walls' brow lines and the forms' heights (their Defs and sidecars). Deterministic from the game's time, never saved (rule 5). Only the floor that a wall or form can shade needs it. |
| **`skyv`** | the floor's sky visibility, 0.55 to 1 | **A static map, baked once per region** by the plan's deriver (PR 8), with the kit's `floorSky` rule (a 16 px max grid, 7 m radius) over the same heights. |
| **`seaDir`** | ±1: flips the swell's phase along screen Y only (`terrainLight6.js:236`) | **A sign per shore section** from the plan (sea to the south = one sign, to the north = the other). **Finding: the east and west shores have no correct value,** because their sea lies along screen X. Leave them unset (TerrainLight5's swell) until the kit takes a 2-D sea direction (the Claude Design paste). |

Unset, each input is exactly TerrainLight5 (§1.2), so PR 2 can land TerrainLight6 now and the feeds can come later.

### 1.6 Not stops, and the PR that needs each

| Missing | Needed by | Who makes it |
|---|---|---|
| SHA256SUMS | PR 6 (the kit lands, §11) | Claude Design (the paste), or PR 6 computes it and says so |
| Checkers (the kit's own self-tests) | PR 6 | Claude Design |
| Baked face sheets | Only option (a) (79 walls' faces), in a cliff bake PR in CLIFFS' lane | Claude Design's bake spec, then CLIFFS |
| Baked form sheets (stacks, skerries) | PR 6 | PR 6's bake driver, from the kit's public API |
| Gameplay sidecars (footprint, pivot, sort line, plinth) | PR 6: the forms' colliders need them (finding 1) | Claude Design's spec, PR 6's driver |
| North faces | Nothing in this plan: every wall faces E to SW, and a stack is seen from the south | — |
| The viewer's `lib/lib` path bug (8 files fail to load, so it shows "A RIG DID NOT LOAD") | Nobody here: the harness loads the rigs directly | Claude Design |

### 1.7 Kit findings
1. **The face compose clamps a face pixel's foot.** A face pixel's height is clamp((py − sy) / KZ, foot, ztop), where `foot` is the Z of the last non-face cell drawn in front of that column.
   - That is right when the cell touches the face (the ground or a terrace at its toe).
   - It is wrong when the cell stands clear of it. Behind a sea stack or a reef bench, every face pixel above the nearer form's top line is lifted to that top's height, so the face reads as one bed there (vertical streaks).
   - The previews work round it from outside the kit: the walls come first in the tier list, a field of the walls alone draws them cell by cell, and a face pixel the full compose lifted takes that field's values. §9's table gives each window's face pixels and how many still read lifted after the workaround.
2. **No ground risers.** The kit draws a step in its ground by stretching the step's top cell down the screen; only a tier's step becomes a lit face. So in the previews, land outside the walls that rises northward faster than 0.7 m per m (a gap's side, a bank) is eased on screen. The plan's heights are not changed by this; only the picture is.
3. **A C2PA provenance chunk (`caBX`)** sits in the kit's PNGs. It is harmless to the harness. PR 6's slicer must ignore it.

---

## 2. Today's shores

**How the pictures were made** (as part 1's):
- They are drawn from the committed `Data/Terrain/StPetersSeabed_HeightTex.png` and `StPetersSplatA–E.png`.
- Each carries part 1's five tide lines: spring low −2.2, neap low −0.99, mean 0, neap high +0.99 and spring high +2.2 m.
- Magenta marks what must not move (part 1's `features.json`).
- The 79 walls are drawn as their brow and toe lines, coloured by class and labelled by id. They were parsed from `StPeters.unity` by a streaming script, never read whole.

**The pictures:**
- [`today-east.png`](st-peters-terrain-pass-9-part-2/today-east.png): x 140 to 300, y −60 to 70, at 6 px per m. The harbour, the landing and the South Arm.
- `today-south.png` (below): x −60 to 200, y −100 to −5, at 6 px per m. Walls 000 to 078, the two access coves and the fleet's grounds.
- [`today-west.png`](st-peters-terrain-pass-9-part-2/today-west.png): x −375 to −25, y −70 to 70, at 4 px per m. The bar root, the crossing and the pass to Nine Mile Creek.

![Today's south coast: the 79 walls by id, the tide lines and the two access coves](st-peters-terrain-pass-9-part-2/today-south.png)

**What today's east, south and west are:**
- **One cliff coast** runs from the harbour's south arm (bearing 96.6°) round to the Storm Beach (252°): 79 walls, 256.4 m of them, every brow at +6.0 m.
- **Two access coves** break it: a ramp at 128° to 136° between walls 022 and 023, and one at 205° to 213° between walls 054 and 055. They are the only ways down.
- **The toes** stand from −4.0 to +3.15 m. At a spring low the South Arm, the Weather Cliff and the South-West Bluff stand in water, and the ledges' benches (about −1.65) bare.
- **The south's intertidal** is only 2,363 m² (part 1 did not change it). This is what the owner saw missing: the north has flats, terraces and coves between the tides, and the south has a wall with water at its foot.
- **North of the landing,** part 1's Cannery Shingle ends at the cannery (189.0, 37.5). Between it and the slip the shore is today's.
- **The west** is the bar root and part 1's Bar-Head Flats (paint only), then the bar itself: one sand texture from the root to the pass, with no variance.

### 2.1 The 79 walls, by id

Every wall stays in every option (§3). By run of class, aspect and batter:

| Walls | Class | Aspect | Batter | Drop (m) | Toe (m) | Length (m) | Section (§4) |
|---|---|---|---:|---|---|---:|---|
| 000–002 | DeepShoreCliff | E | 62° | 4.48–7.29 | −1.29 to +1.52 | 2.6 | South Arm |
| 003–008 | DeepShoreCliff | E | 76° | 7.29–10.00 | −4.00 to −1.29 | 7.7 | South Arm |
| 009–021 | DeepShoreCliff | SE | 76° | 6.08–10.00 | −4.00 to −0.08 | 32.8 | South Arm |
| 022 | DeepShoreCliff | SE | 62° | 3.52–6.08 | −0.08 to +2.48 | 3.3 | South Arm |
| 023–024 | LedgeCliff | SE | 62° | 3.13–5.27 | +0.73 to +2.87 | 3.8 | East Ledges |
| 025–029 | LedgeCliff | SE | 76° | 5.27–7.65 | −1.65 to +0.73 | 14.6 | East Ledges |
| 030–039 | LedgeCliff | S | 76° | 7.65–8.75 | −2.75 to −1.65 | 57.5 | East Ledges |
| 040–052 | Cliff | S | 76° | 4.89–9.19 | −3.19 to +1.11 | 65.8 | Weather Cliff |
| 053–054 | Cliff | S | 62° | 2.85–4.89 | +1.11 to +3.15 | 3.2 | Weather Cliff |
| 055 | LedgeCliff | S | 62° | 2.95–5.00 | +1.00 to +3.05 | 4.0 | West Ledges |
| 056 | LedgeCliff | S | 76° | 5.00–6.40 | −0.40 to +1.00 | 1.5 | West Ledges |
| 057–067 | LedgeCliff | SW | 76° | 6.40–8.76 | −2.76 to −0.40 | 41.5 | West Ledges |
| 068–075 | DeepShoreCliff | SW | 76° | 7.19–10.00 | −4.00 to −1.19 | 15.2 | South-West Bluff |
| 076–078 | DeepShoreCliff | SW | 62° | 4.00–7.19 | −1.19 to +2.00 | 2.9 | South-West Bluff |

- **Names:** `CliffWall_<class>_<aspect>_<steep|ramp>_<nnn>`, for example `CliffWall_Cliff_S_steep_042`.
- **The source:** each wall's `CliffWallSurface` carries its brow and toe polylines, its drops and its toe elevations (`_browPlan`, `_toePlan`, `_dropMetres`, `_toeElevations`), 1,102 stations in all. The table is in `Evidence~/p2/walls_table.json`.
- **Most walls on one screen** (`Code/App/CameraFollow.cs`: 32 px per m, 1080 px high, 16:9). A wall counts when any of its brow or toe stations is on screen; the window slides on a 0.5 m grid (`p2_extra_costs.py`):

  | Framing | View (m) | Walls | Centred at | Which |
  |---|---|---:|---|---|
  | The Dory's default (`DefaultWorldHeightMeters` 14) | 24.9 × 14 | 13 | (180.0, −15.2) | 000–012 |
  | On foot (`OnFootWorldHeightMeters` 9) | 16 × 9 | 10 | (−46.1, −26.2) | 069–078 |

### 2.2 What must not move

Part 1's list (its §2.1) holds, with its sources. In short:
- **The landing:** the beach slip, the dredged approach, the berth pocket, the dock, the disembark point, the arrival point, the dory's berth and the #677 mooring, the arrival route and the wharf (§6 has each by coordinate).
- **The village:** every building (8 m; the cannery 12 m), every yard (its hull + 3 m), every built root's objects (3 m) and the roads (3 m either side).
- **The water's frame:** the harbour entrance (its route + 20 m), the bar's gut (+5 m), the three passages (10 m), the map's edge (20 m) and the fleet's grounds rect.
- **The woods:** each of the 259 trees (3 m).
- **Part 2 adds:**
  - **every wall**, by its footprint + 3 m (2 m feather). The channel at its foot is kept by the toe rule (§4.1);
  - **the bar's crest band** (|y| ≤ 5 m, ground above +0.5) and **part 1's Bar-Head Flats**, except inside the crest pools of layout B (§5);
  - **the clam holes are NOT frozen.** A hole whose ground the bar's flank lobes would carry out of the scatter's band pulls its ground back to part 1's (within 1.5 m, 4 m feather). §2.3 checks every hole.

### 2.3 The checks

All from `Evidence~/harness/p2_check.py`, against part 1's derived plan, under layouts B and A (§5):

| Check | B | A |
|---|---|---|
| The frozen mask: cells / largest change | 338,011 / **0.0 m** | 341,617 / **0.0 m** |
| Placed things (the landing, the marks, the passages) | 0.0 m | 0.0 m |
| The walls: moved | **0 of 79** | 0 of 79 |
| The toe rule: changed cells in front of a toe / breaking the rule | 21,883 / **0** | the same |
| The trees: on moved ground (within 3 m) | **0 of 259** | 0 of 259 |
| The clam holes: ground changed by more than 0.02 m / largest change | 33 / 0.543 m | 31 / 0.543 m |
| The clam holes: outside the ±1.8 m band / in a pool | **0 / 0** | 0 / 0 |
| The clam holes: all bare at a spring low and flood at a spring high | yes | yes |
| The nav marks (13): their ground | unchanged; the shallowest, the reef's north cardinal, keeps 0.83 m at a spring low | the same |
| The fleet's grounds rect: largest change / share on land | 0.0 m / 0.976 | the same |
| The landing's points (dock, disembark, arrival, dory) | today −4.0, −4.0, −4.0, −3.97; plan the same | the same |
| The slip, the approach cut, the arrival route, the wharf | 0.0 m | 0.0 m |
| The bar: the crest outside the pools / inside them / the gut | 0.0 / 1.165 m over 1,769 cells / 0.0 | 0.0 / — / 0.0 |
| Part 1's seven paths: largest change | 0.0 m | 0.0 m |

### 2.4 Walkability today (a finding)

The charter asks what each cliff option does to walkability, because "a cliff built as terrain is ground the player could walk up". **Today's cliffs are already walkable:**
- `Code/Player/TidalWalkability.cs` decides by depth only, in four bands: dry, wading (to `GameConfig.WadeDepth`, 0.5 m), slow-swimming (to `GameConfig.SwimLimit`, 2.0 m), and deep, which is boat-only: a soft wall stops the player stepping in. It has no slope rule.
- A wall only draws: `CliffWallSurface` (`Code/Art/CliffWallSurface.cs`) has no collider.
- The only colliders on St Peters' terrain are the three passage triggers (`BoxCollider2D` at `StPetersBuilder.cs:2165`, `:2188` and `:2217`).
- So today a player can walk off a brow, down the ground under the face, to the toe.

This matters to part 2 in one place. **A stack's mass is not terrain** (§4.4): its plinth is ground, but the stack is a sprite. At a low, a player on the flats would walk through the Old Man.
- `PlayerWalkController` drives a `Rigidbody2D` (gravity 0), so a static collider stops it.
- **The plan gives every form a footprint collider** from its bake sidecar. That is the gameplay sidecar the kit's "Not done" list names.
- Whether walls should also block (a slope rule, or colliders at the brows) is a gameplay question outside this pass. It is logged in §12.

### 2.5 The cliffs' faces and the rocks today
- **The faces.** Each wall is drawn from baked faces in the **v10** look that ships. The px look sits behind a bake switch (#866, "v10 stays the shipped default"): the menus `Hidden Harbours/Dev/Bake Cliff Kit — v10` and `— px` (`CliffBakeMenu.cs`), and the shader keyword `_HH_CLIFF_PX` (`CliffCatalog.PxKeyword`).
  - The faces are gitignored, so they live only in the owner's checkout. CI and fresh boxes have none.
  - The cliffs backup of 2026-09-24 (outside the repo) holds `Cliffs/` (331 files, 35.5 MB) and `Faces/` (135 PNGs, 33.6 MB).
  - The px kit (`docs/art/rigs/px-cliff-face-kit`): 180 face sets (3 rocks × 5 aspects × 4 batters × 3 wear steps), of which the standing bake holds 90 as 360 PNGs; faces of 384 × 288 px (12 × 9 m); a palette LUT of 8 × 32. The batters are 90°, 76°, 62° and 48°.
- **The rocks.**
  - Rock Px (#840) is in the repo: 5 stones, 16 forms, 225 sheets and 900 PNGs (956 files with the sidecars, 36.96 MB). Its `RockPx.json` records `derivedFromRigSha256` `3b6ec36d…`, v6's `lib/pxRockIso2.js`.
  - Today's shore rocks are the `Shoreline` root: 21 objects, placed by `StPetersShorePainter.Paint` (`StPetersBuilder.cs:1639`), Build()-only.

### 2.6 Nine Mile Creek today (read only)
- **Its terrain is analytic,** not painted: its scene holds a `MainlandTidalTerrain` (`Code/World/MainlandTidalTerrain.cs:138`). It is rectangular CARVE and FILL features plus polyline CHANNELs.
  - It has no `TidalTerrain` and no `PaintedTidalTerrain`. By the scene's GUIDs: `TidalTerrain` `74d8faff…` is in St Peters only, `PaintedTidalTerrain` `a1655af8…` is in neither, and `MainlandTidalTerrain` `98303a6e…` is in Nine Mile Creek only.
- **Its walls:** 86, built by `NineMileCreekCliffWalls`, all east-facing, drop 4.31 to 12.0 m, toe −6.0 to +1.69 m.
- **Its half of the crossing** (`NineMileCreekMainland`, in its own frame): from `BarFrom` (45, −150) to `BarTo` (346, −199), 305 m long, crest +0.88, the gut at 0.45 of the way, its bed −0.6. `NineMileCreekMainlandTerrainTests.TheCrossingIsOneBarEitherSideOfTheSeam` guards the seam.

---

## 3. The cliff options

**The charter's three options:**
- **(a) v6 replaces the walls.** The weather coast's cliffs become v6 terrain: every St Peters wall is drawn from `pxCliff3` tiers on the same brow and toe lines, jointed, baked per wall.
- **(b) v6 joins the walls.** The px walls stay the cliffs. v6 adds the forms (the four stacks), the reef benches under the ledges, and the floor's `occ` and `skyv`.
- **(c) A mix.** v6 builds only the new forms on the reshaped shores (the stacks). The walls stay where they stand. There are no benches and no floor light.

In all three, the rocks are #840's sprites (§2.5), already in the repo, and nothing moves a wall.

**The pictures.** One window for all three, as the charter asks: x 64.25 to 80.50, y −80.84 to −59.00 (walls 039–044 and the Old Man), tide −1.2 m, afternoon 14:00, the same seed.

| (a) replaces | (b) joins | (c) mix |
|---|---|---|
| ![(a)](st-peters-terrain-pass-9-part-2/option-a.png) | ![(b)](st-peters-terrain-pass-9-part-2/option-b.png) | ![(c)](st-peters-terrain-pass-9-part-2/option-c.png) |

- **(a) against (b):** 144,878 of 234,000 pixels differ (mean 109.6). That is the walls themselves: v6's joints cut the face into buttresses and notches and break the brow's line.
- **(b) against (c) in this window:** 2,064 pixels differ (mean 64.8). The weather window has no reef, so only the floor's `occ` and `skyv` differ.
- **Where (b) and (c) part:** the East Ledges, where the reef lies under LedgeCliff walls. [`preview-east-ledges.png`](st-peters-terrain-pass-9-part-2/preview-east-ledges.png) (b) against [`preview-east-ledges-c.png`](st-peters-terrain-pass-9-part-2/preview-east-ledges-c.png) (c): 99,648 pixels differ (mean 86.0), the bench and the floor's light.

### 3.1 What each option does

|  | (a) replaces | (b) joins | (c) mix |
|---|---|---|---|
| **CLIFFS Phase C at St Peters** (the plates, v10 beside px) | Moot: (a) redraws every St Peters wall the plates would judge | As chartered | As chartered |
| **CLIFFS Phase C and D at Nine Mile Creek** (D: the flip) | As chartered, but the flip then reaches only Nine Mile Creek's 86 walls | As chartered | As chartered |
| **The walls, by id** | all 79 (000–078) redrawn from v6 on their own brow and toe lines | all 79 as they are | all 79 as they are |
| **Walkability** | unchanged: depth-only, no collider (§2.4) | unchanged for walls; the four stacks get footprint colliders | as (b) |
| **The height range** (part 1's decision 11: −4 to +7 m) | inside it: the tiers draw the same height field | inside it: the stacks are sprites, their plinths −1.0 to −1.3 | inside it |
| **The `CliffWalls` root** (Build()-only) | **replaced: CLIFFS' builder must change, which is a stop** | untouched | untouched |
| **Bake or author first** (the kit's "Not done") | face sheets for 79 walls and their bake driver; the relight baked; north faces not needed (every wall faces E to SW) | form sheets for the 4 stacks and 2 bench strips; gameplay sidecars (footprint, pivot, sort line, plinth) | form sheets for the 4 stacks; their sidecars |
| **Texture MB** | +21.1 MB: 1.38 M unique face texels as 4 RGBA32 maps, on top of NMC's px faces | +2.7 MB for the stacks (one state) or +8.2 MB (three tide rows); about +1.1 MB for the benches; 2 R8 maps of 1.5 MB (`occ`, `skyv`) | +2.7 MB or +8.2 MB |
| **Draws** | a face texture of its own per wall: up to 13 walls on the Dory's screen (§2.1), none sharing texels | walls as today; +1 per stack and per bench strip in view | walls as today; +1 per stack in view |
| **The rocks** (all options) | 103 sprites from 12 of #840's sheets (13.8 MB in memory, 0 MB new). The worst 16 × 9 m screen, by the Sisters, holds 17 rock and stack sprites (centred at (−52.7, −38.5)); the most sheets on one screen is 5 (at (−58.7, −38.5)) | the same | the same |
| **Claude Design** | the face bake spec | the form bake and sidecar spec | the same as (b) |

**How the costs were measured** (`Evidence~/harness/p2_cliff_cost.py`):
- **(a):** the face pixels each wall shows on screen from the section's own brow, drop and batter. By section: Weather Cliff 0.47 M, East Ledges 0.42 M, West Ledges 0.23 M, South Arm 0.18 M, South-West Bluff 0.08 M.
- **The stacks:** each stack's sprite at 32 px per m: the Harbour Stack 204 × 288 px (0.90 MB), the Old Man 179 × 280 (0.77 MB), the Sisters 166 × 242 and 140 × 201 (0.62 and 0.43 MB). "Three tide rows" is a dry, a wet and an awash state, like #840's rows.
- **The benches:** 136 m of south-facing reef edge under the ledges. Its risers, from the crest's top to the foot within 5 m, are mostly 0.34 to 0.92 m (median 0.69, the tallest 1.57): 0.070 M face pixels, 1.06 MB as 4 RGBA32 maps (`p2_extra_costs.py`).
- **The floor maps:** the plan's grid is 1,520 × 1,040 cells, so one byte a cell is 1.5 MB.

### 3.2 The recommendation: (b), in two steps

1. **First (c)'s forms:** the four stacks, their colliders and the rocks, with PR 6 and PR 5b (§11).
2. **Then (b)'s benches and the floor's light,** in PR 8: after PR 2 lands TerrainLight6 with its inputs unset, and after PR 5b spawns the forms (§11).

**Why:**
- **It keeps CLIFFS as chartered.** (b) and (c) touch no wall and no builder, so Phases C and D go ahead as planned at both regions. (a) makes St Peters' Phase C moot and needs `StPetersCliffWalls.Build` changed, which is a stop.
- **v6's value on this coast is the forms and the light, not the walls.** The px walls are what CLIFFS is finishing now. v6's stacks, benches and floor light are what the new flats need to read as a coast.
- **(a) costs the most:** +21.1 MB of unique face texels, a Build()-only root replaced, and 79 bakes. Its jointed faces do look richer (option-a.png).
- **Walkability does not choose between them.** No option makes a wall stop a walker (§2.4); every option's stacks need colliders.

**The seat does not decide this, and neither does this lane: the owner does** (decision 2, §13).

---

## 4. The new coast

### 4.1 How a section is made
- **The reference line** is today's 0 m line, measured on rays from the island's centre. It runs clockwise, land on the right, from the cannery (189.0, 37.5) at 61.6° to the bar head (−60.6, 20.4) at 285°: 405.7 m through 60 stations (`SHORE2`).
- **Each section has a mode:**
  - **cut** or **fill:** part 1's cross-shore profile, a list of (distance from the line, height) knots, inland positive;
  - **toe:** the walls stay, and the section shapes only the sea floor in front of their toe lines;
  - **paint:** today's ground, with the section's recipe;
  - **keep:** nothing changes.
- **Edges:** 3 m of feather between sections, widening by 0.22 m for each metre past 8 m seaward; 6 m at the ends of the scope; and a 1.2 m warp over 7 m, so no edge runs straight.
- **The toe rule.** In front of a wall, new ground may not rise above the line of sight from the toe: e ≤ toeZ + s × 0.84, where s is how far south of a toe station the cell lies, in that station's own screen column (out to 30 m), and 0.84 = PY / KZ = 20.6 / 24.5. On screen, ground in front of a wall never climbs over its face. Of the 21,883 changed cells in front of a toe, none breaks it (§2.3).
- **The walls' keep:** each wall's footprint + 3 m (2 m feather) keeps its ground.
- **Materials:** each section's recipe is a list of (top height, material), part 1's pass-9 materials. The paint follows the ground's height and the section's features (reef, spit, flats, runnels).

### 4.2 The sections

![The plan: the south shore and the South Flats at a spring low](st-peters-terrain-pass-9-part-2/plan-south.png)

The east and west in the same style: [`plan-east.png`](st-peters-terrain-pass-9-part-2/plan-east.png) and [`plan-west.png`](st-peters-terrain-pass-9-part-2/plan-west.png).

| Section | Id | Type, mode | From | Length (m) | Walls | The intertidal profile |
|---|---|---|---:|---:|---|---|
| Harbour Strand | `coast.stp_harbour_strand` | harbour_strand, cut | 61.6° | 26.5 | — | +6.0 at 16 m inland, +3.4 at 9 m, 0 at the line, −0.8 at 8 m out, −1.3 at 18 m, −2.3 at 36 m |
| The Landing | `coast.stp_the_landing` | landing, paint | 79.0° | 27.6 | — | today's ground |
| South Arm | `coast.stp_south_arm` | deep_cliff, toe | 96.6° | 46.0 | 000–022 | the walls into deep water; the channel stays deep |
| East Gap | `coast.stp_east_gap` | gap_cove, cut | 127.4° | 22.4 | between 022 and 023 | +2.2 at 6 m inland, 0 at the line, −1.3 at 14 m out, −2.4 at 30 m; a spit (below) |
| East Ledges | `coast.stp_east_ledges` | toe_reef, toe | 136.9° | 73.9 | 023–039 | a reef in front of the bench (below) |
| Weather Cliff | `coast.stp_weather_cliff` | deep_cliff, toe | 172.0° | 71.6 | 040–054 | the highest faces into deep water; nothing laid against them |
| West Gap | `coast.stp_west_gap` | gap_cove, cut | 205.0° | 19.5 | between 054 and 055 | as the East Gap; a spit |
| West Ledges | `coast.stp_west_ledges` | toe_reef, toe (broken) | 213.0° | 46.3 | 055–067 | the reef, 1.5 times more broken |
| South-West Bluff | `coast.stp_sw_bluff` | deep_cliff, toe | 240.0° | 20.3 | 068–078 | the walls into deep water |
| Storm Beach | `coast.stp_storm_beach` | storm_beach, cut | 252.0° | 19.6 | — | +6.0 at 16 m inland, a swale at +2.7, a berm at +3.1 (7.5 m), 0 at the line, −1.6 at 14 m out, −2.4 at 24 m |
| Bar root | `coast.stp_bar_root` | sandy_flats, keep | 262.0° to 285.0° | 32.0 | — | part 1's Bar-Head Flats, as part 1 planned them |

**Materials,** each a top height and the material below it (m):

| Recipe | Its materials, from the lowest |
|---|---|
| harbour_strand | eelgrass to −2.0 · ripple to −1.1 · foreshore to −0.2 · sand to +0.9 · shingle to +2.9 · marram to +3.6 · grass |
| landing | eelgrass to −2.0 · ripple to −1.2 · rockweed to −0.2 · foreshore to +1.2 · shingle to +2.9 · marram to +3.6 · grass |
| deep_cliff, toe_reef | eelgrass to −2.3 · Irish moss to −1.43 · rockweed to −0.05 · ledge to +2.4 · shelf to +4.2 · grass |
| gap_cove | eelgrass to −2.0 · ripple to −1.0 · foreshore to +0.6 · shingle to +1.1 · sand to +2.4 · marram to +3.8 · grass |
| storm_beach | eelgrass to −2.0 · ripple to −1.2 · foreshore to −0.4 · shingle to +3.3 · marram to +3.9 · grass |
| flats, sand | eelgrass to −1.75 · ripple to −0.95 · sand to +2.4 · grass |
| flats, mud | eelgrass to −1.75 · ripple to −1.3 · mud to +0.6 · grass |

**Why each sits where it does:**
- **Harbour Strand:** the harbour's north arm. Part 1's Cannery Shingle is carried round the point and softened to a sand-and-shingle strand where the entrance channel's shelter begins. It is shaped only outside the entrance route's 30 m capsule (north of about 72°); inside it the ground is today's and only the paint is new.
- **The Landing:** kept (§6). It is framed by paint (a weed line on the slip's flanks, a wrack line at the spring-high mark, shingle above) and by the forms either side: the strand to the north, the Harbour Stack to the south.
- **South Arm:** walls 000–022 are the harbour's south arm. The deep water at their foot stays deep (the arrival comes from the north-east). A stack and three skerries stand off the arm, so the harbour reads as held between two arms.
- **East Gap:** the access ramp between walls 022 and 023, the cliff coast's one way down on the east. A shingle spit runs out along the gap's axis (bearing 132°, 18 m, crest +1.2 to −1.4, 6 m half-width) with a pocket beach either side. The Gap Brook comes down the ramp to the sea.
- **East Ledges:** walls 023–039 stand on a bench at about −1.65. In front of it lies a reef: a toe channel the walls' foot keeps, then a weed-covered crest that bares at every low, cut by gullies along the rock's joints and pocked with rock pools.
- **Weather Cliff:** walls 040–054, the highest faces, into deep water. Nothing is laid against them. The Old Man stands off the middle, his plinth heaped with the cliff's fallen blocks.
- **West Gap:** the access ramp between walls 054 and 055, the heath's way down. A spit (bearing 209°) and a pocket beach; the Heath Brook reaches the sea here.
- **West Ledges:** walls 055–067, the same reef as the east, more broken where the weather comes round the island's shoulder.
- **South-West Bluff:** walls 068–078, the island's corner. The Sisters, two stacks, stand off it.
- **Storm Beach:** where the bar's south flank meets the bluff, open to the south-west: a cobble storm beach with a berm at +3.1 m and a swale behind it that the Storm Brook follows to the sea.
- **Bar root:** part 1's Bar-Head Flats, the clam flats, kept.

**The reef** (East and West Ledges), in metres seaward of the toe line:
- a toe channel of 5 m, which the walls' keep holds;
- a rise from 6 to 9 m, a crest from 9 to 17 m at −0.6 ± 0.3, a fall from 17 to 21 m;
- gullies every 15 to 25 m, 1.5 to 2.5 m wide, their beds at −1.6;
- rock pools, 7 per 100 m of toe, 0.8 to 1.5 m in radius (drawn as ellipses), 0.3 to 0.5 m deep: 10 in all, 35.1 m², each holding water (spill −1.245 to −0.548, up to 0.49 m deep);
- the reef's width wanders 0.7 to 1.3 times along the toe (a 30 m noise). On the West Ledges the gullies and pools are 1.5 times as many, and the reef's edge 1.5 times as ragged.

**What each section changes** (m² of ground in the section; changed by more than 0.02 m; between the tides; the largest raise and cut):

| Section | m² | Changed | Intertidal | Raise | Cut |
|---|---:|---:|---:|---:|---:|
| Harbour Strand | 2,089 | 1,032 | 1,326 | +1.18 | −1.72 |
| The Landing | 3,496 | 0 | 606 | 0 | 0 |
| South Arm | 7,821 | 974 | 479 | +3.07 | −1.30 |
| East Gap | 910 | 778 | 776 | +2.30 | −1.39 |
| East Ledges | 12,516 | 5,036 | 4,618 | +3.72 | −0.03 |
| Weather Cliff | 9,429 | 5,559 | 4,137 | +3.52 | −0.46 |
| West Gap | 846 | 745 | 724 | +2.57 | −2.03 |
| West Ledges | 10,246 | 2,799 | 2,238 | +3.65 | −0.31 |
| South-West Bluff | 2,832 | 876 | 548 | +3.13 | −0.86 |
| Storm Beach | 872 | 522 | 558 | +1.60 | −3.74 |
| Bar root | 5,158 | 551 | 2,712 | +0.26 | −1.07 |

- In all, 28,014 m² of ground changes against part 1, from +3.72 to −3.74 m. The plan's ground spans −4.0 to +6.65 m, inside part 1's decision 11.
- The raises of +3 m and more are the reefs and the South Flats laid over deep water in front of the walls (§4.3); the largest (+3.72 m) is the East Ledges' reef. No raise touches a wall's keep, and the toe rule holds everywhere.
- [`change.png`](st-peters-terrain-pass-9-part-2/change.png) shows what moves: raised, cut and frozen.

### 4.3 The South Flats (the owner's ask)

> "make sure the southshore has the same low level areas as the north, i want the low tidal areas simialr to the barhead flats to encircle the island … i know there will be visible cliffs on southshore but lets combine the two please"

`flats.stp_south_flats` is one low-tide flat laid seaward of the south's cliffs, reefs and coves, from the South Arm (bearing 100°) round to the bar root (266°).
- **The ring.** At the bar root it meets part 1's Bar-Head Flats on the bar's south flank. With part 1's Lagoon Flats, North Cove terrace, Ginny's Cove and Cannery terrace, it rings the island. The ring breaks only at the harbour mouth, where the arrival route and the dredged approach are frozen. See [`plan-island.png`](st-peters-terrain-pass-9-part-2/plan-island.png).
- **Its band.** It lies between −0.45 and −1.85 m, close to the Lagoon Flats' band (part 1: 0 to −1.80 m over their first 60 m). A spring low bares it, every high floods it, and a neap low bares only its inner part. At mean tide it is sea to the cliffs' feet; at a low the stacks stand on it.
- **Its shape:**
  - its width seaward of the 0 m line runs from 0 at both ends to 52 m under the Weather Cliff (bearing 177°), and its front lobes in and out by up to 9 m (a 46 m noise);
  - its level falls evenly from −0.45 at the 0 m line to −1.85 at its front, and the front's level wanders by 0.30 m over 45 m, so eelgrass reaches in and sand runs out;
  - the front falls to today's sea floor over 12 m;
  - it is a FILL only: it raises the sea floor and never lowers today's ground.
- **The cliffs and the flats combined:**
  - no flat inside 3.5 m of a wall's toe, the full flat beyond 8 m. The check finds 0 cells of flat inside that moat;
  - under the ledges, the flat begins 10 to 16 m out from a LedgeCliff toe, behind the reef's crest. It is a fill, so the reef stands 0.5 to 0.9 m proud of it and no trough is left between the reef's fall and the flat;
  - the channel at each wall's foot is exactly as part 1 left it, which is today's (0.0 m change). Where it holds water at a spring low today it still does: South Arm 84% of its 179 m², Weather Cliff 88% of 240 m², South-West Bluff 82% of 72 m². Under the ledges it is dry at a spring low today (6% and 2% wet) and stays so;
  - **two guts** drain the walls' channels out through the flats (§7.3). Without them the channel at a cliff's foot would be shut in at a low.
- **Its detail:**
  - low swells (0.22 m over 30 m), ridge-and-runnel (0.14 m, 16 m apart, broken up by a noise) and the sand's own ripple (0.04 m over 3.5 m);
  - **drains:** a stem every 13 to 21 m from the inner flat to the front, 0.9 to 1.7 m wide and 0.16 to 0.30 m deep, widening and deepening seaward, and meandering 1.5 to 4.5 m either side over 17 m. 0 to 3 tributaries join each from the landward side at 25° to 55°, a dendritic net. A drain keeps 0.04 m of the ebb down its bed at a low;
  - **12 pools and 16 hollows** (§7.4);
  - **rock ribs** under the Weather Cliff, a wave-cut platform: ribs of bare rock 9 m apart along the strike, 0.28 m proud;
  - **mussel beds** in clumps on the mid-flat, from −1.35 to −0.45 m.
- **Its ground by section:** boulders under the South Arm; sand at the East Gap, the East Ledges, the West Gap, the Storm Beach and the bar root; ribs under the Weather Cliff; mud in the lee of the West Ledges and the South-West Bluff.
- **What it adds:** 12,382 m². The south's intertidal grows from 2,363 m² (today and part 1) to 13,988 m².

The flats at a spring low:
- [`preview-flats-weather.png`](st-peters-terrain-pass-9-part-2/preview-flats-weather.png): the Old Man and his gut;
- [`preview-flats-weather-neap.png`](st-peters-terrain-pass-9-part-2/preview-flats-weather-neap.png): the same at a neap low (−0.99), when only the flat's inner part bares;
- [`preview-flats-east.png`](st-peters-terrain-pass-9-part-2/preview-flats-east.png): a pool, a hollow and the drains;
- [`preview-flats-ribs.png`](st-peters-terrain-pass-9-part-2/preview-flats-ribs.png): the rock ribs;
- [`preview-flats-sisters-gut.png`](st-peters-terrain-pass-9-part-2/preview-flats-sisters-gut.png): the Sisters' Gut.

![The Weather Flats at a spring low: the Old Man on the flat, his gut, the drains](st-peters-terrain-pass-9-part-2/preview-flats-weather.png)

### 4.4 The forms and the rocks

**The forms** (`FormDef`, one file each). A form's mass is not terrain: the game's floor is drawn flat and lit, so a stack is a v6 tier (`pxCliff3`) baked like a wall face and y-sorted with the walls. The terrain carries only its plinth, the rock's foot, bared at a low.

| Form | Id | Where | Radii (m) | Top | Plinth | Why |
|---|---|---|---|---:|---|---|
| Harbour Stack | `form.stp_harbour_stack` | (192.0, −38.0) | 3.2 × 2.6, turned 20°, batter 80° | +6.2 | radius 5.5 m at −1.2 | the harbour's south gatepost: 37.6 m from the dock, 30.5 m off the entrance's port mark p3 |
| The Old Man | `form.stp_old_man` | (70.0, −79.5) | 2.8 × 2.4, turned −10°, batter 84° | +6.4 | radius 3.5 m at −1.0 | above the Weather Cliff's brow (+6.0), 7.8 m off wall 042's toe with a channel between |
| The Sisters | `form.stp_the_sisters` | (−52.0, −40.0) and (−47.0, −46.0) | 2.6 and 2.2, batter 78° | +5.2, +4.1 | radius 4.0 m at −1.0 | two stacks off the south-west corner, outside the fleet grounds' rect + 6 m and the bar's capsule |
| The Whelps | `form.stp_the_whelps` | (186, −46), (192, −50), (181, −52) | 2.8, 2.2, 2.0 | — | at −1.2 | three skerries trailing south of the stack |
| Gap skerries (east) | `form.stp_east_gap_skerries` | (189.5, −63.5), (192.5, −61.0) | 1.8, 1.5 | — | at −1.3 | off the East Gap spit's tip, where its shingle came from |
| Gap skerries (west) | `form.stp_west_gap_skerries` | (−10.5, −85.0), (−7.0, −87.5) | 1.8, 1.5 | — | at −1.3 | off the West Gap spit's tip |

- The stacks are bedded sandstone (the kit's `strata` 0.8 to 1.0).
- The skerries are #840's `skerry` rock on raised bases, not v6 tiers.
- **Every form blocks walking** (`blocks_walk: true`) with a footprint collider from its sidecar (§2.4).
- **They are spawned at load** from their Defs by a `CoastFormField`, as the plant field is (PR 5b), so no new Build()-only root appears (§10).

**Today's shore rocks in scope keep their places** and take a v6 form (`ShoreRockDef`). The swap is PR 4b's step, because `Shoreline` is Build()-only.

| Id | Today | At | Form | Stone | State |
|---|---|---|---|---|---|
| `rock.stp_strand_block` | `Rock_bs` | (189.82, 35.82) | block | sandstone | dry |
| `rock.stp_strand_knuckle` | `Rock_bm` | (199.60, 21.98) | knuckle | sandstone | wet |
| `rock.stp_landing_knuckle` | `Rock_m` | (217.52, 15.40) | knuckle | sandstone | wet |
| `rock.stp_landing_skerry` | `Rock_reef` | (219.75, 16.60) | skerry | sandstone | wet |
| `rock.stp_east_gap_skerry` | `Rock_reef` | (176.35, −55.98) | skerry | sandstone | wet |
| `rock.stp_ledge_block` | `Rock_bs` | (114.98, −69.63) | block | sandstone | wet |
| `rock.stp_field_erratic_1` | `FieldRock_bs` | (104.34, −56.54) | erratic | granite | dry |
| `rock.stp_field_erratic_2` | `FieldRock_bs` | (122.01, −47.86) | erratic | granite | dry |
| `rock.stp_field_erratic_3` | `FieldRock_bs` | (4.58, −45.33) | erratic | granite | dry |

**New rocks are scatters** (`RockScatterDef`): a rule, deterministic from the plan's seed, 87 rocks in all.

| Scatter | Where | Rocks | Drawn | Why |
|---|---|---:|---|---|
| `scatter.stp_stack_aprons` | the Harbour Stack's and the Sisters' plinths, 0.3 to 2.6 m out | 33 | 13 block, 10 wedge, 6 cloven, 4 slab | blocks fallen from the stacks |
| `scatter.stp_spit_cobbles` | the two spits, −1.4 to +1.6 m | 27 | 22 cobbles, 5 knuckle | the spits' big stones along their crests |
| `scatter.stp_ledge_boulders` | the East Ledges' bench and channel, 1 to 8 m off the toes of walls 023–039 | 9 | 4 block, 4 knuckle, 1 slab | off the reef crest |
| `scatter.stp_storm_cobbles` | the Storm Beach, +0.5 to +3.4 m | 8 | 3 knuckle, 3 block, 2 cobbles, all granite | the berm's big cobbles, granite from the glacial till |
| `scatter.stp_weather_apron` | the Old Man's plinth, 0.3 to 3.0 m out | 5 | 3 cloven, 1 wedge, 1 block | the Weather Cliff's fallen blocks. The cliff's own toe stays in deep water (−3.19), where a boulder would sit 1 m under a spring low, in the boats' way |
| `scatter.stp_ledge_boulders_w` | the West Ledges, 1 to 8 m off the toes of walls 055–067 | 5 | 3 knuckle, 1 cobbles, 1 scree | the same on the West Ledges |

- By form and stone: 23 sandstone cobbles, 18 sandstone blocks, 12 sandstone knuckles, 11 wedges, 9 cloven, 5 slabs, 3 granite knuckles, 3 granite blocks, 2 granite cobbles, 1 scree. A rule's forms are weights: the Weather apron allows an erratic and drew none.
- With the 9 swaps and the 7 skerries, the 103 rocks draw from 12 of #840's sheets, all in the repo: block (granite, sandstone), cloven, cobbles (granite, sandstone), erratic, knuckle (granite, sandstone), scree, skerry, slab and wedge.

### 4.5 Walking the new shores

**Across the flats at a low** (the quickest walk: Dijkstra on the 0.5 m grid, 8 neighbours, at the game's walk, wade and swim speeds):

| Walk | Straight | Its sill | Part 1, spring low | Part 2, spring low | Part 2, neap low |
|---|---:|---|---|---|---|
| East Gap → West Gap | 166.3 m | −2.449 at (63.0, −101.4) | 175.6 m, 82.1 s: 69.5 m wet, 1.04 m at the deepest | **179.4 m, 60.2 s:** 7.0 m wet, 0.25 m at the deepest, 2 swings | 180.6 m, 64.5 s: swims 1.46 m |
| West Gap → Storm Beach | 85.3 m | −2.449 at (−56.3, −50.7) | 89.5 m, 40.3 s: 1.8 m at the deepest | **89.8 m, 30.6 s:** 0.57 m at the deepest | 94.2 m, 34.9 s: swims 1.46 m |

- **The window** (both walks, dry or wading): the sill is −2.449, so the walk is cut above −1.949.
  - At springs it is open **1.91 h** of each 12.42 h tide (cut 10.51 h). At neaps it never opens (the low is −0.99).
  - Over a fortnight: 12.2 game hours open, about 15 real minutes.
- **The guts are the flats' teeth.** Their beds stay at −2.4 at the highest, so at a spring low 0.2 m of water stands in them (a wade). As the flood makes they turn to a swim, and above a tide of −0.4 they are deeper than 2.0 m: boat-only, behind the soft wall.
- **The stacks are solid** (§2.4): the walk goes round them.
- **P5:** the flats are generous at a spring low and shut at neaps. A walker who lingers as the flood makes finds each gut turn boat-only (above −0.4) well before the flat around it does (above +0.15 at its front).

The cliff walk and the gap paths are in §8.

---

## 5. The crossing

**Today.** The bar is analytic: from (−45, 0) to (−350, 0), 30 m half-width, crest +0.88. Its gut at x −234.1 (15 m half-width) has its bed at −0.6. It is one sand texture from the root to the pass, with no variance.
- **The window:** the gut's bed plus the wade depth, −0.6 + 0.5, so the crossing is cut above −0.10.
  - At springs it is cut 6.39 h of each tide and open 6.03 h; at neaps, cut 6.61 h and open 5.81 h. Over a fortnight: 160.4 game hours open.
  - On day 1 it opens at 05:19 and shuts at 11:20, then opens at 17:44 and shuts at 23:45.
- **The walk:** 311.0 m of St Peters' half, straight down the crest: 103.7 s at a low.

**The plan** (`coast.stp_the_bar`, type the_bar, x −356 to −70, 45 m half-width) carries the north shore's low-tide detailing out along the bar:
- ripple and sand, eelgrass below −2.0;
- runnels on the flanks every 9 to 14 m, 0.9 m wide and 0.10 m deep, painted silt;
- sand-wave lobes on the flanks only (0.45 m over 26 m, in bands 5.5 to 8 m and 24 to 30 m off the axis);
- a mussel bed either side of the gut (within 26 m of it, −1.3 to −0.3 m);
- materials: eelgrass to −2.0 · ripple to −0.6 · sand to +2.4 · marram to +3.8 · grass;
- **pools, in two layouts.** A pool's water stands at its lowest rim (part 1's pan rule): the spill is derived from the ground, never typed. A pool's dish is flat to half its radius, then rises to its lip.

![The crossing, layouts A and B, at a spring low](st-peters-terrain-pass-9-part-2/crossing.png)

- **Layout A: 12 flank pools.** The crest stays frozen, so the walk stays straight (311.0 m, 0 swings).
- **Layout B: A + 11 crest pools.** Each crest pool covers the spine (|y| ≤ 5 m) from one side and leaves a 3 m dry lane on the other, and the sides alternate. A walker keeping to the dry spine swings round them.
  - **B needs the owner to unfreeze the crest inside the pools' footprints:** 1,769 cells (about 442 m²), where the ground changes by up to 1.165 m. The crest outside the pools does not change (0.0 m), nor does the gut.

**The window does not change in either layout.** Before and after: the sill is the gut's bed (−0.6 at x −234.1); cut above −0.10; springs cut 6.39 h and open 6.03 h; neaps cut 6.61 h and open 5.81 h; 160.4 game hours a fortnight.

![The crossing's window before and after, spring and neap, and the walk's time inside it](st-peters-terrain-pass-9-part-2/crossing-window.png)

**The walk** (St Peters' half, 311.0 m straight):

| Layout, tide | Length | Time | Swings (over 1 m) | Wet | Deepest |
|---|---:|---:|---:|---:|---:|
| Today, any low | 311.0 m | 103.7 s | 0 | 0 | 0 |
| Today, at the opening (−0.10) | 311.0 m | 105.1 s | 0 | 11.5 m | 0.5 m |
| A, spring or neap low | 311.0 m | 103.7 s | 0 | 0 | 0 |
| A, at the opening | 311.0 m | 105.1 s | 0 | 11.5 m | 0.5 m |
| **B, spring or neap low, the quickest line** | **314.7 m** | **104.9 s** | **2** | 0 | 0 |
| **B, along the crest top** | **321.8 m** | **107.3 s** | **12** | 0 | 0 |
| B, at the opening | 314.7 m | 106.6 s | 2 | 13.5 m | 0.5 m |

- At the opening the gut is wet, so a crest-top walk does not exist until the tide falls further.
- **Time inside the window:** St Peters' half takes 104.9 to 107.3 real seconds under B, 1.4 game hours (a game hour is 75 real seconds). Nine Mile Creek's half is 305 m more (§2.6), about 102 s at the walk. The whole crossing, 616 m straight, takes about 2.8 game hours of a window open 6.03 h at springs and 5.81 h at neaps.

**The pools** (spill = the lowest rim; bed; depth; area; whether a walker wades (under 0.5 m) or swims; hours bared in a 12.42 h spring tide):

| Pool | Spill | Bed | Depth | m² | | Bared |
|---|---:|---:|---:|---:|---|---:|
| `pool.stp_bar_a01` | −0.574 | −0.924 | 0.35 | 11.5 | wade | 5.17 h |
| `pool.stp_bar_a02` | −0.436 | −0.836 | 0.40 | 9.8 | wade | 5.42 h |
| `pool.stp_bar_a03` | −0.301 | −0.751 | 0.45 | 15.5 | wade | 5.67 h |
| `pool.stp_bar_a04` | −0.501 | −0.801 | 0.30 | 8.2 | wade | 5.30 h |
| `pool.stp_bar_a05` | −0.647 | −1.047 | 0.40 | 13.0 | wade | 5.03 h |
| `pool.stp_bar_a06` | −0.689 | −1.039 | 0.35 | 9.2 | wade | 4.95 h |
| `pool.stp_bar_a07` | −0.844 | −1.294 | 0.45 | 15.2 | wade | 4.65 h |
| `pool.stp_bar_a08` | −0.519 | −0.819 | 0.30 | 7.8 | wade | 5.27 h |
| `pool.stp_bar_a09` | −0.722 | −1.122 | 0.40 | 12.0 | wade | 4.89 h |
| `pool.stp_bar_a10` | −0.667 | −1.017 | 0.35 | 9.5 | wade | 4.99 h |
| `pool.stp_bar_a11` | −0.468 | −0.918 | 0.45 | 15.8 | wade | 5.36 h |
| `pool.stp_bar_a12` | −0.307 | −0.607 | 0.30 | 8.0 | wade | 5.66 h |
| `pool.stp_bar_b01` | +0.464 | +0.114 | 0.35 | 25.0 | wade | 7.05 h |
| `pool.stp_bar_b02` | +0.459 | −0.191 | 0.65 | 27.0 | **swim** | 7.04 h |
| `pool.stp_bar_b03` | +0.464 | +0.064 | 0.40 | 27.5 | wade | 7.05 h |
| `pool.stp_bar_b04` | +0.464 | +0.164 | 0.30 | 19.5 | wade | 7.05 h |
| `pool.stp_bar_b05` | +0.464 | −0.236 | 0.70 | 28.5 | **swim** | 7.05 h |
| `pool.stp_bar_b06` | +0.532 | +0.132 | 0.40 | 25.2 | wade | 7.18 h |
| `pool.stp_bar_b07` | +0.464 | +0.114 | 0.35 | 25.0 | wade | 7.05 h |
| `pool.stp_bar_b08` | +0.532 | −0.068 | 0.60 | 25.0 | **swim** | 7.18 h |
| `pool.stp_bar_b09` | +0.532 | +0.082 | 0.45 | 24.2 | wade | 7.18 h |
| `pool.stp_bar_b10` | +0.464 | −0.286 | 0.75 | 29.5 | **swim** | 7.05 h |
| `pool.stp_bar_b11` | +0.532 | +0.232 | 0.30 | 22.8 | wade | 7.18 h |

- Four crest pools are swum (b02, b05, b08, b10). The dry lane beside each keeps the walk dry; a walker who cuts through swims.
- The crest pools stand above mean tide (spill +0.46 to +0.53), so they hold rain and spray between tides as still water, bared 7 h of each spring tide.

**Nine Mile Creek's role.** Its half is analytic (§2.6), and this plan does not touch it. Carrying the pools and runnels onto its half is PR 7, only if the owner says so (decision 8). The window would not move there either: its gut's bed is −0.6, the same as ours.

The crossing at a spring low and at a spring high: [`preview-crossing-low.png`](st-peters-terrain-pass-9-part-2/preview-crossing-low.png) and [`preview-crossing-high.png`](st-peters-terrain-pass-9-part-2/preview-crossing-high.png).

---

## 6. The landing

The charter asks for the landing kept and framed, rendered at the intro's clock, and for a second picture only: a slight dock shift, with everything it moves.

**The recommendation is to keep it** (decision 7). Nothing at the landing moves in this plan. Its points, the beach slip, the dredged approach, the berth pocket, the arrival route and the wharf all keep their ground to 0.0 m (§2.3).

### 6.1 The intro's clock

A new game starts on day 1 at 06:00 (`_startHour = 6f`, `Environment/GameClock.cs:15`).

**St Peters' tide at that moment:**
- **The inputs:**
  - amplitude 2.2 m and phase 1 h (`StPetersBuilder.cs:201-202`);
  - a tidal period of 12.4206 h, a 28-day lunar month and a neap fraction of 0.45 (`Data/Config/GameConfig.asset`);
  - the carrier sin(2π(t + phase)/period) (`Environment/TideModel.cs:22`).
- **So:** tide(t) = 2.2 · (0.45 + 0.55 · env) · sin(2π(t + 1)/12.4206), with env = 0.5 + 0.5 · cos(2πt/336) and t in game hours from the start.
- **Day 1 is a spring day.** At 06:00 the tide stands at **−0.854 m and falls 1.02 m an hour.**
  - Low water is at 08:19 (−2.19 m), high water at 14:31 (+2.18 m), and low water again at 20:44 (−2.16 m).
  - The crossing opens at 05:19 and shuts at 11:20, then opens at 17:44 and shuts at 23:45 (§5).
- **The light:** the kit's dawn preset at 06:00, sun intensity 0.81.

So the arrival comes in on a falling tide with the foreshore baring. The weed line, the wrack line and the shingle all show, and the strand's lower sand is wet.

![The Landing at the intro's clock: 06:00 on day 1, the tide at −0.854 m and falling](st-peters-terrain-pass-9-part-2/preview-landing.png)

`preview-landing.png` is a 2 × 2 mosaic of 520 × 450 windows at the kit's dawn preset: x 186.00 to 218.50, y −21.69 to 22.00.

**The points** (part 1 §2.1: `StPetersBuilder.cs:1051-1080` for the points, `:986-997` for the dory's berth). "Largest change" is the largest ground change within 8 m of the point:

| Point | At | Ground today | Ground in the plan | Largest change |
|---|---|---:|---:|---:|
| Dock | (211.5, −5.8) | −4.00 | −4.00 | 0.0 m |
| Disembark | (211.5, −1.9) | −4.00 | −4.00 | 0.0 m |
| Arrival | (213.5, −5.8) | −4.00 | −4.00 | 0.0 m |
| The dory's berth | (213.5, 4.2) | −3.97 | −3.97 | 0.0 m |

The slip, the dredged approach, the arrival route and the wharf each change by 0.0 m as well.

### 6.2 The frame

**The Landing section** (`coast.stp_the_landing`, 79.0° to 96.6° round the island, 27.6 m of shore) **is paint only**, so its ground is today's.
- Its recipe: eelgrass to −2.0 · ripple to −1.2 · rockweed to −0.2 · foreshore to +1.2 · shingle to +2.9 · marram to +3.6 · grass.
- A weed line runs along the slip's flanks, and a wrack line lies at the spring-high mark.

**The harbour is read from the forms either side** (§4.2, §4.4):
- **North: the Harbour Strand** (`coast.stp_harbour_strand`, 61.6° to 79.0°, 26.5 m). Part 1's Cannery Shingle is carried round the point as a sand-and-shingle strand, cut only outside the arrival route's 30 m capsule.
- **South, three pieces:**
  - the South Arm, walls 000 to 022, standing in deep water;
  - **the Harbour Stack** (`form.stp_harbour_stack`) at (192.0, −38.0), top +6.2 m, 37.6 m from the dock;
  - **the Whelps**, three skerries trailing south of the stack at (186, −46), (192, −50) and (181, −52).
- **The arrival comes in from the north-east** between the strand and the stack, so the boat passes between the harbour's two arms before it reaches the slip.
- **Today's four landing rocks keep their places** and take v6 rock forms (§4.4): `rock.stp_strand_block`, `rock.stp_strand_knuckle`, `rock.stp_landing_knuckle` and `rock.stp_landing_skerry`.

![The Landing, kept and framed: the sea at the intro's tide, the strand to the north, the South Arm, the Harbour Stack and the Whelps to the south](st-peters-terrain-pass-9-part-2/landing.png)

### 6.3 The second picture: a 6 m dock shift (drawn, not planned)

The charter asks for this as a picture only. It is not in the plan's data, and nothing in Appendix A moves the dock.

![The second picture, drawn not planned: the dock, the wharf head and every landing point 6 m east](st-peters-terrain-pass-9-part-2/landing-dock-shift.png)

**What it moves, 6 m east:**
- **the wharf's head,** from x 213.5 to 219.5. Its deck grows from 30.5 m to 36.5 m (`StPetersWharf.cs:44-60`: today x 183 to 213.5, y −3 to 3, 201 placed objects);
- **the dock,** from (211.5, −5.8) to (217.5, −5.8);
- **the disembark point, the arrival point and the dory's berth;**
- **the end of the arrival route's last leg** (`StPetersNavMarks.cs:94-110`, `StPetersArrivalOpening.cs:51-64`).

**What stays:** the beach slip, the dredged approach and the route's first legs.

**The ground does not stop it.** Every shifted point stands at −4.0 m in part 1's plan and in this one. So does the whole strip the wharf would grow over (x 213.5 to 219.5, y −3 to 3). No dredging is needed.

**What it costs:**
- **The Build()-only stop, again.** `Build()` places all five of the roots it moves: `StPetersWharf`, `StPetersDockZone`, `StPetersArrival`, `StPetersDisembark` and `Dory`.
  - Moving them needs either a `Build()`, which is ruled out at St Peters (ADR 0019), or a hand-written YAML patch.
  - PR 4b cannot take a step for it, because it is a move, not a refresh.
- **The builder's constants:** the points, the berth, the route's last leg and the wharf's length.
- **Every guard whose premise moves** (the table below).
- **#846**, the wharf draft ("art(wharf): add modular sections, end caps and structural detail"; draft, 72 files, branch `art/wharf-asset-review`).
  - It dresses today's 30.5 m wharf, so a shift means re-laying its sections and moving its east end cap to the 36.5 m head.
  - Keeping the landing leaves #846 untouched.

**The guards whose premise moves:** 12 classes. A full sweep would be the shift PR's own job.

| Class | Mode | Tests | What it pins |
|---|---|---:|---|
| `StPetersDoryBerthTests` | EditMode | 8 | the dory's berth pose and heading |
| `StPetersAlongsideBerthTests` | EditMode | 11 | the berth pocket digs the berth and nothing else |
| `StPetersEastBerthTests` | EditMode | 10 | the builder's centre line of the channel that feeds the berth |
| `StPetersWharfWalkabilityTests` | EditMode | 10 | the wharf's walkability over the real authored terrain |
| `ArrivalOverRealTerrainPlayTests` | PlayMode | 13 | reaching the berth over the real terrain and tide, in the promised time |
| `BerthingPilotTests` | EditMode | 19 | literals: the berth (211.5, −5.8) and the planks (211.5, −1.9) |
| `BerthPilotTests` | EditMode | 18 | the same literals |
| `FishDutyCycleAtTheLandingTests` | EditMode | 3 | "berth pocket" fishing spots at (211.5, −5.8) |
| `BoardingSeatHeadingTests` | EditMode | 5 | the dory at (213.5, 4.25) |
| `HelmStationHeadingTests` | EditMode | 5 | the dory at (213.5, 4.25) |
| `HullPresencesTests` | EditMode | 10 | the dory at (213.5, 4.25) |
| `BoardingWhileCarriedPlayTests` | PlayMode | 6 | the disembark position (213.5, −1.9) and the dory at (213.5, 4.25) |

### 6.4 Why keep it

- **What the shift buys:** 6 m more wharf, and a berth 6 m further out from the slip.
- **The frame does the intro's work without it.** The strand, the stack and the Whelps make the arrival read as coming into a harbour, and the frame moves nothing at the landing.
- **What the shift costs:** a Build()-only move of five roots, 12 guard classes and a re-lay of #846.
- **Recommendation: keep and frame** (decision 7).

---

## 7. Water

Every piece of water in this plan is real water in PR 4's still-water seam (part 1's decision 6). Wherever the still level stands above the ground, the water level is the higher of the tide and the still level (part 1 §4.3).
- **Still water in all: 2,597 m².** Part 1's 392.75 m² all stands unchanged, and part 2 adds 2,204.25 m².
- **The channels:** 1,266.5 m² of part 2's water lies in the brooks' and creeks' channels and in the flats' drains.
  - It is mostly a trickle: 0.04 m at the median and 0.16 m at the 95th percentile.
  - Its deepest is 0.54 m, where a pool sits on a drain.
- **The ponds and pools** off those channels are the other 937.75 m².
- **The bar's runnels** (§5) are painted silt and hold almost none (4.25 m²).

### 7.1 Ponds

Three ponds on the cliff-top heath, each at the head of a brook.
- A pond is flat at its surface. It fills from its centre over every cell lower than its surface plus 0.05 m, leaving out the brook's channel.
- A pond would leak if that fill reached the window's edge or ran past twice its radius. None does.
- Radii and turn (Appendix A): the Gap Pond 4.6 × 3.0 m turned 35°, the Heath Pond 4.5 × 3.0 m turned −15°, and the Storm Pond 4.2 × 2.8 m turned 8°.

| Pond | Id | Kind | Centre | Surface | Bed | Water (m²) | Leaks | Outlet |
|---|---|---|---|---:|---:|---:|---|---|
| Gap Pond | `pond.stp_gap_pond` | bog pond | (151.0, −33.0) | +5.30 | +4.70 | 41.75 | no | `stream.stp_gap_brook` |
| Heath Pond | `pond.stp_heath_pond` | bog pond | (20.0, −53.0) | +5.30 | +4.70 | 43.5 | no | `stream.stp_heath_brook` |
| Storm Pond | `pond.stp_storm_pond` | fen pool | (−24.0, −10.0) | +5.30 | +4.75 | 38 | no | `stream.stp_storm_brook` |

### 7.2 Brooks

Each brook runs from its pond through a gap or a swale to the shore. It then goes on across the flats as a creek (§7.3).
- **The fresh reach** is the length whose bed stands above +2.2 m (the spring high). Below that the brook is tidal.
- **The water:** part 1's rule steps it down the channel, the bed plus 0.10 to 0.14 m (part 1 §4.3).
- **The channel:** every brook is 0.9 m wide at its head and 1.6 m at its mouth, 0.12 m deep, with banks of 0.35 to 0.55 m.

| Brook | Id | Length (m) | Fresh reach (m) | Tidal reach (m) | Bed, top → mouth | Steepest climb downstream | Deepest water (m) | Perched |
|---|---|---:|---:|---:|---|---:|---:|---|
| Gap Brook | `stream.stp_gap_brook` | 25.7 | 18.9 | 6.7 | +5.15 → −0.82 | 0.001 m | 0.31 | no |
| Heath Brook | `stream.stp_heath_brook` | 20.8 | 13.3 | 7.5 | +5.13 → −0.82 | 0.012 m | 0.34 | no |
| Storm Brook | `stream.stp_storm_brook` | 38.0 | 21.5 | 16.5 | +5.07 → −0.89 | none | 0.30 | no |

- **"Steepest climb downstream"** is the largest rise of the bed along the flow. At most it is 12 mm, under one 8-bit step (3.9 cm, part 1 §4.1), so nothing holds the water back.
- **No brook is perched:** no water stands above its banks.

**Where the paths cross them.** All four crossings are in fresh reaches, where part 1's rule puts stepping stones on `talus` (part 1 §5). The values are read at the crossing's 0.5 m cell:

| Path | Brook | At | Water | Ground |
|---|---|---|---:|---:|
| Cliff walk | Gap Brook | (156.4, −40.9) | 0.17 m | +5.02 |
| Cliff walk | Heath Brook | (15.1, −56.1) | 0.16 m | +5.14 |
| Cliff walk | Storm Brook | (−40.9, −12.9) | 0.17 m | +3.71 |
| West Gap path | Heath Brook | (12.1, −58.7) | 0.17 m | +5.00 |

### 7.3 Creeks and guts

Across the flats each brook goes on as a creek to the eelgrass. The two guts are the walls' own channels out to sea (§4.3, §4.5).

| Name | Id | Joins | Length (m) | Bed, head → lowest | Width (m) | Water over its highest bed at a spring low |
|---|---|---|---:|---|---|---|
| Gap Creek | `creek.stp_gap_creek` | `stream.stp_gap_brook` | 59.6 | −0.71 → −3.90 | 1.6 to 3.0 | none: the brook's trickle only |
| Heath Creek | `creek.stp_heath_creek` | `stream.stp_heath_brook` | 47.9 | −0.70 → −2.75 | 1.6 to 3.0 | none: the brook's trickle only |
| Storm Creek | `creek.stp_storm_creek` | `stream.stp_storm_brook` | 32.5 | −0.79 → −4.06 | 1.6 to 2.6 | none: the brook's trickle only |
| The Old Man's Gut | `gut.stp_old_mans_gut` | the walls' channel | 53.2 | −2.40 → −3.40 | 3.2 to 3.6 | 0.2 m |
| The Sisters' Gut | `gut.stp_sisters_gut` | the walls' channel | 43.5 | −2.40 → −3.67 | 3.0 to 3.4 | 0.2 m |

- **The creeks' beds** are carved from −0.70 m (the Storm Creek's from −0.60) down to −2.50 m by design.
  - They meet deeper ground where they cross a drain or reach the front; the lowest figures in the table are those places.
  - The carve fades out over 1 to 2 m either side.
  - At a spring low only the brook's trickle (0.06 m) runs in them.
- **The guts:**
  - at a spring low, 0.2 m of water stands in each (a wade);
  - from a tide of −1.9 to −0.4, a walker swims them;
  - above −0.4 they are deeper than 2.0 m, so they are boat-only (§4.5).

### 7.4 The flats' pools and hollows

**The pools** (12, `pool.stp_flats_01` to `_12`) are dished into the flat.
- Each holds water to its lowest rim. This is part 1's pan rule: the spill is derived from the ground, never typed.
- **"Stands alone"** is how long in one tide the sea stays below the pool's spill, so that the pool holds its own water:
  - hours = 12.4206/π · acos(−spill/A);
  - A = 2.2 m at springs and 0.99 m at neaps.

| Pool | Centre | Spill | Bed | Deepest water (m) | Area (m²) | Stands alone per spring cycle (h) | Per neap cycle (h) |
|---|---|---:|---:|---:|---:|---:|---:|
| `pool.stp_flats_01` | (150.4, −75.5) | −1.199 | −1.499 | 0.30 | 10.2 | 3.93 | — |
| `pool.stp_flats_02` | (146.7, −85.5) | −1.119 | −1.469 | 0.35 | 15.2 | 4.10 | — |
| `pool.stp_flats_03` | (132.4, −90.6) | −1.183 | −1.493 | 0.31 | 5.0 | 3.97 | — |
| `pool.stp_flats_04` | (114.3, −88.5) | −0.990 | −1.380 | 0.39 | 10.8 | 4.36 | — |
| `pool.stp_flats_05` | (96.5, −108.5) | −1.527 | −1.877 | 0.35 | 25.8 | 3.18 | — |
| `pool.stp_flats_06` | (76.6, −95.2) | −1.061 | −1.391 | 0.33 | 18.2 | 4.22 | — |
| `pool.stp_flats_07` | (46.6, −83.8) | −0.849 | −1.119 | 0.27 | 20.8 | 4.64 | 2.14 |
| `pool.stp_flats_08` | (27.2, −93.8) | −1.331 | −1.571 | 0.24 | 13.8 | 3.64 | — |
| `pool.stp_flats_09` | (−17.9, −90.7) | −1.694 | −2.054 | 0.36 | 18.2 | 2.74 | — |
| `pool.stp_flats_10` | (−25.1, −80.8) | −1.639 | −1.969 | 0.33 | 10.5 | 2.89 | — |
| `pool.stp_flats_11` | (−40.4, −74.3) | −1.787 | −2.087 | 0.30 | 6.5 | 2.46 | — |
| `pool.stp_flats_12` | (−53.7, −53.9) | −1.394 | −1.684 | 0.29 | 16.0 | 3.50 | — |

- All twelve are wades, 0.24 to 0.39 m deep.
- Only pool 07 stands alone at neaps, for 2.14 h. The rest are under the sea for the whole of a neap tide.

**The hollows** (16) are places where the flat's own shape holds the ebb. They are found, not placed:
- by a priority flood from the open sea at a spring low;
- keeping every basin at least 0.1 m deep and 4 m² in area.

| Hollow | Centre | Spill | Bed | Deepest water (m) | Area (m²) | On foot | Stands alone per spring cycle (h) |
|---|---|---:|---:|---:|---:|---|---:|
| `pool.stp_hollow_01` | (186.9, −42.2) | −1.216 | −1.331 | 0.11 | 11.8 | wade | 3.89 |
| `pool.stp_hollow_02` | (−70.1, −42.8) | −1.688 | −1.931 | 0.24 | 22.8 | wade | 2.75 |
| `pool.stp_hollow_03` | (−27.3, −52.3) | −1.891 | −2.373 | 0.48 | 71.5 | wade | 2.12 |
| `pool.stp_hollow_04` | (142.1, −64.3) | −1.831 | −2.141 | 0.31 | 36.0 | wade | 2.32 |
| `pool.stp_hollow_05` | (−30.6, −70.5) | −1.538 | −1.765 | 0.23 | 6.0 | wade | 3.15 |
| `pool.stp_hollow_06` | (110.7, −74.3) | −2.015 | −2.572 | 0.56 | 64.5 | **swim** | 1.63 |
| `pool.stp_hollow_07` | (90.3, −81.0) | −1.699 | −2.053 | 0.35 | 4.0 | wade | 2.72 |
| `pool.stp_hollow_08` | (140.3, −82.8) | −1.093 | −1.264 | 0.17 | 28.0 | wade | 4.15 |
| `pool.stp_hollow_09` | (21.2, −83.7) | −0.923 | −1.116 | 0.19 | 13.2 | wade | 4.50 |
| `pool.stp_hollow_10` | (14.4, −85.7) | −0.939 | −1.048 | 0.11 | 4.5 | wade | 4.47 |
| `pool.stp_hollow_11` | (118.5, −87.2) | −0.965 | −1.183 | 0.22 | 5.0 | wade | 4.42 |
| `pool.stp_hollow_12` | (172.5, −93.8) | −1.908 | −2.150 | 0.24 | 16.8 | wade | 2.06 |
| `pool.stp_hollow_13` | (175.5, −100.1) | −2.060 | −2.321 | 0.26 | 5.2 | wade | 1.42 |
| `pool.stp_hollow_14` | (85.2, −104.6) | −1.335 | −1.549 | 0.21 | 10.0 | wade | 3.63 |
| `pool.stp_hollow_15` | (46.3, −105.0) | −1.690 | −1.830 | 0.14 | 6.5 | wade | 2.75 |
| `pool.stp_hollow_16` | (48.9, −113.3) | −2.024 | −2.189 | 0.17 | 4.5 | wade | 1.59 |

- They cover 310.3 m² in all.
- Fifteen are wades. Hollow 06 (0.56 m deep) is a swim.

### 7.5 Rock pools

Ten pools lie in the reefs' benches, five on each ledge, each in a gully's hollow (§4.2).

| Pool | Centre | Spill | Bed | Deepest water (m) | Area (m²) | Stands alone per spring cycle (h) |
|---|---|---:|---:|---:|---:|---:|
| `pool.stp_east_ledges_01` | (148.29, −67.04) | −0.803 | −1.163 | 0.36 | 5.8 | 4.73 |
| `pool.stp_east_ledges_02` | (139.19, −73.82) | −1.245 | −1.645 | 0.40 | 4.0 | 3.83 |
| `pool.stp_east_ledges_03` | (128.35, −76.77) | −0.548 | −0.918 | 0.37 | 2.8 | 5.22 |
| `pool.stp_east_ledges_04` | (114.14, −78.95) | −0.572 | −1.062 | 0.49 | 2.0 | 5.17 |
| `pool.stp_east_ledges_05` | (95.00, −83.21) | −1.221 | −1.651 | 0.43 | 2.5 | 3.88 |
| `pool.stp_west_ledges_01` | (−7.32, −72.69) | −0.841 | −1.321 | 0.48 | 4.2 | 4.66 |
| `pool.stp_west_ledges_02` | (−16.61, −65.44) | −0.894 | −1.314 | 0.42 | 3.8 | 4.56 |
| `pool.stp_west_ledges_03` | (−23.87, −63.40) | −1.140 | −1.620 | 0.48 | 4.0 | 4.06 |
| `pool.stp_west_ledges_04` | (−30.10, −59.76) | −0.694 | −1.034 | 0.34 | 3.0 | 4.94 |
| `pool.stp_west_ledges_05` | (−38.48, −52.84) | −0.565 | −0.895 | 0.33 | 3.0 | 5.18 |

- All ten hold water at every low.
- Each stands alone for 3.8 to 5.2 h of a spring tide.

### 7.6 The flood, tide by tide

![The flood at the five tide levels: today, part 1 and part 2](st-peters-terrain-pass-9-part-2/flood.png)

**Dry ground at each level,** for the whole map and for the island alone (east of x −70):

| Tide | Level | Dry today | Part 1 | Part 2 | Change from part 1 | The island alone (x > −70): today / part 1 / part 2 |
|---|---:|---:|---:|---:|---:|---|
| spring low | −2.20 m | 53,042 m² | 62,471 m² | 74,254 m² | +11,783 m² | 41,778 / 48,756 / 60,306 |
| neap low | −0.99 m | 39,621 m² | 45,768 m² | 49,124 m² | +3,356 m² | 32,371 / 38,499 / 41,878 |
| mean | +0.00 m | 35,365 m² | 36,638 m² | 36,655 m² | +17 m² | 31,036 / 32,310 / 32,450 |
| neap high | +0.99 m | 30,182 m² | 30,511 m² | 30,524 m² | +13 m² | 30,182 / 30,511 / 30,524 |
| spring high | +2.20 m | 29,394 m² | 29,033 m² | 29,007 m² | −26 m² | 29,394 / 29,033 / 29,007 |

- **The ground between the tides** grows from 23,648 m² today to 33,438 m² under part 1 and 45,247 m² under part 2. For the island alone: 12,384 → 19,723 → 31,299 m².
- **Part 2's gain is almost all below the neap low.**
  - A spring low bares 11,783 m² more than part 1, and a neap low 3,356 m² more.
  - Mean and high water barely move (+17, +13 and −26 m²).
- **Water left behind** is sea water cut off from the open sea at a low; still water is not counted.
  - **At a spring low:** 77.5 m² at (−104.5, 21.0), 0.9 m deep, the same as part 1's.
  - **At a neap low:** 1,138.2 m². That is part 1's 1,137.8 m² at (−89.2, 26.2), 2.11 m deep, plus two one-cell dips of 0.2 m² each, holding 0.00 and 0.01 m, at (184.2, −58.2) and (70.8, −78.8).

**Where the flood cuts a path off.** The rule is the game's own (part 1 §4.5): a path is cut while the tide stands more than 0.5 m (`WadeDepth`) over its lowest ground.

| Walk | Lowest ground | Cut above | Cut per spring tide | Per neap tide | Over a fortnight |
|---|---|---:|---:|---:|---|
| Bar walk (the crossing; part 1's) | −0.60 at (−234.2, 0.0) | −0.10 | 6.39 h | 6.61 h | cut 175.6 game h |
| Reef walk (part 1's) | +0.03 at (70.0, 88.0) | +0.53 | 5.24 h | 3.96 h | cut 128.5 game h |
| Ginny's track (part 1's) | +0.11 at (101.0, 69.5) | +0.61 | 5.09 h | 3.57 h | cut 122.0 game h |
| **The flats' walks** (East Gap → West Gap, West Gap → Storm Beach; §4.5) | the sill, −2.449 | −1.949 | 10.51 h (open 1.91 h) | the whole tide | open 12.2 game h |

- **No part 2 path is ever cut.** The cliff walk and the two gap paths stay above +2.49 m (§8.1).
- The three cut paths are part 1's, with part 1's times.
- The flats' walks are the quickest lines across the flats (§4.5), not path Defs. At springs they are open for 1.91 h of each tide; at neaps they never open. Over a fortnight that is 12.2 game hours, about 15 real minutes.

**The fleet** (`flats.fleet_spring_low`):
- The ambient fleet's grounds rect holds 13 deep cells (0.4 m of water or more at a spring low).
- Under part 1 all 13 join the harbour mouth. Under part 2 none does: the flats cut them off at a spring low.
- The fleet joins on station and plans its legs inside its rect (`AmbientFleetPresenter.PlanFleetDay`), so this is a note only.
- It goes to gameplay-systems, alongside part 1's decision 12 (decision 12 here).

---

## 8. Paths, plants and fish

### 8.1 Paths

Part 1's paths go on round the island (`path.stp_*`, Appendix A). Kinds and widths follow part 1's (part 1 §5): a footpath is 0.9 to 1.0 m, a cart track 1.5 to 1.6 m, and the tidal crossing 3.0 m.

![Every path on the island, and where the tide cuts them](st-peters-terrain-pass-9-part-2/paths.png)

| Path | Id | Part | Kind | Width | Length (m) | Lowest ground | Cut when the tide stands | Cut per spring cycle | Per neap cycle | Steepest 2 m, m per m (part 1's ground) |
|---|---|---:|---|---:|---:|---|---|---:|---:|---|
| Slip road | `path.stp_slip_road` | 1 | cart track | 1.5 m | 188.7 | +6.00 at (11.9, 5.5) | never | — | — | 1.501 (1.501) |
| Bar-head road | `path.stp_bar_head_road` | 1 | cart track | 1.5 m | 48.6 | +6.00 at (−6.7, 3.7) | never | — | — | 0.000 (0.000) |
| Bar walk | `path.stp_bar_walk` | 1 | tidal crossing | 3.0 m | 321.2 | −0.60 at (−234.2, −0.0) | above −0.10 m | 6.39 h | 6.61 h | 0.521 (0.521) |
| Shore path | `path.stp_shore_path` | 1 | footpath | 1.0 m | 291.1 | +2.68 at (149.8, 57.2) | never | — | — | 0.854 (0.854) |
| Barren path | `path.stp_barren_path` | 1 | footpath | 0.9 m | 64.0 | +5.65 at (45.4, 43.3) | never | — | — | 0.053 (0.053) |
| Reef walk | `path.stp_reef_walk` | 1 | footpath | 0.9 m | 24.1 | +0.03 at (70.0, 88.0) | above +0.53 m | 5.24 h | 3.96 h | 0.706 (0.706) |
| Ginny's track | `path.stp_ginnys_track` | 1 | cart track | 1.6 m | 29.6 | +0.11 at (101.0, 69.5) | above +0.61 m | 5.09 h | 3.57 h | 0.985 (0.985) |
| **Cliff walk** | `path.stp_cliff_walk` | 2 | footpath | 1.0 m | 280.6 | +2.81 at (−40.1, −15.8) | never | — | — | 0.965 (0.000) |
| **East Gap path** | `path.stp_east_gap_path` | 2 | footpath | 0.9 m | 12.8 | +3.30 at (165.5, −49.5) | never | — | — | 0.760 (0.596) |
| **West Gap path** | `path.stp_west_gap_path` | 2 | footpath | 0.9 m | 11.0 | +2.49 at (10.0, −66.0) | never | — | — | 0.614 (0.730) |

- **The cliff walk** (`path.stp_cliff_walk`, 280.6 m) runs 6 m inside the brows from the slip road to the bar-head road. It crosses the Gap and Heath Brooks on stepping stones and the Storm Brook in its swale.
  - Its lowest ground is +2.81 m at (−40.1, −15.8), where it drops into the Storm Beach swale. It is never cut.
  - Its steepest 2 m (0.965 m per m) is on that drop, near (−39, −18).
  - Its largest change from part 1's ground is 3.19 m, at (−40.3, −15.3), where the swale is new (+6.00 → +2.76 m).
- **The gap paths** go down each gap's ramp to its pocket beach. Neither is ever cut.
  - The East Gap path runs beside the Gap Brook, 2.3 m north-east of it, to the beach and the spit.
  - The West Gap path runs down to its own beach.
- **The bar walk** is part 1's line, carried on to the Nine Mile Creek pass (−356, 0): 321.2 m, against part 1's 255.0 m to x −300.
  - Under layout B it is re-laid 1.0 m clear of the crest pools, on their dry side.
  - Its lowest ground (−0.60, at the gut) and its cut times do not change.
- **There is no slope rule for walkers** (§2.4), so a steep stretch is a look, not a block. Part 1's slip road already reaches 1.501 m per m.
- **Part 1's other paths keep their ground:** each one's steepest 2 m equals part 1's.

### 8.2 Plants

Part 1's plant field (one Def per biome, computed at load: part 1 §6.1 to §6.4) is carried round the whole island. The new shores take part 1's biomes by part 1's rules:
- **Harbour shore** (`biome.stp_harbour_shore`): every new intertidal and berm metre between −3.0 and +4.0 m (the strand, the gaps, the storm beach, the reefs and the bar).
- **Blueberry barren:** the cliff-top heath, above +5.4 m and within 10 m of a wall. This is part 1's rule, now round the whole south.
- **Roadside meadow:** the rest of the open plateau.
- **The salt marsh and the alder swale** keep part 1's ground.

![The biomes round the island](st-peters-terrain-pass-9-part-2/biomes.png)

**The whole island** (every biome, both parts):

| Biome | Id | Area (m²) | Plants | Per m² | Lighter option: plants |
|---|---|---:|---:|---:|---:|
| Harbour shore | `biome.stp_harbour_shore` | 49,774 | 10,478 | 0.211 | 10,448 |
| Salt marsh | `biome.stp_salt_marsh` | 6,935 | 3,986 | 0.575 | 3,986 |
| Blueberry barren | `biome.stp_blueberry_barren` | 4,272 | 2,550 | 0.597 | 2,535 |
| Roadside meadow | `biome.stp_roadside_meadow` | 13,101 | 5,966 | 0.455 | 5,646 |
| Alder swale | `biome.stp_alder_swale` | 364 | 200 | 0.549 | 200 |

- **23,180 plants** (6,948 of them young), in a field of **72,879 B** (97,172 characters of base64). Part 1 alone came to 18,182 plants (5,460 young) and 56,044 B (74,728).
- **The lighter option** (part 1's, lighter along the village roads): 22,815 plants (6,846 young) and 71,668 B (95,560). That is 365 plants fewer (1.6%).
- **The rest of the map:**
  - the woods floor, 9,557 m², is the woods' own understorey;
  - the ground below −3.0 m, 311,197 m², has no plants. It is exactly the inshore fish band (§8.3).

**Against the budget** (`StPetersGroundCoverBudgetTests`), by part 1 §6.4's method:
- every 16 × 9 m window at gameplay framing;
- renderers are the plants inside the window;
- batches are the distinct (species, sorting order) pairs, at `SortingBands.OrdersPerMetre` 4.

| Case | Renderers, the worst window | Batches, the worst window |
|---|---:|---:|
| Part 2, either option | 251, at (120.0, 76.5) | 130, at (56.0, 49.5) |
| Part 1 | 319, at (120.0, −67.5) | 130 |
| Today (the test's record) | 395 | 114 or fewer |
| An upper bound: part 2 plus everything today draws | 646 | 244 |
| The test's budget | 900 | 260 |

- **Both of part 2's worst windows are on the north shore.** The new southern shores are lighter than them.
- **Part 1's worst renderer window,** at (120.0, −67.5) on the East Ledges, holds 191 plants under part 2, where the ledges are re-zoned as a reef (§4.2).
- **Today's old families** stand 7 plants in part 2's worst window. So even if nothing retired, that screen would hold 258.
- **The upper bound** adds today's whole count to part 2's worst window, as if nothing retired and both worst screens fell on one spot. It is still under the budget.

### 8.3 Fish

The fish bands, recomputed for the whole island at mean tide, with part 1's bands: tidepool where the water is 0.6 m deep or less, shallows from 0.6 to 3.0 m, and inshore deeper.

| Band at mean tide | Today | Part 1 | Part 2 | Change from part 1 |
|---|---:|---:|---:|---:|
| Tidepool (wet, 0.6 m of water or less) | 2,630 m² | 5,506 m² | 6,170 m² | +664 m² |
| Shallows (0.6 to 3.0 m) | 19,924 m² | 29,542 m² | 41,178 m² | +11,636 m² |
| Inshore (deeper than 3.0 m) | 337,280 m² | 323,514 m² | 311,197 m² | −12,317 m² |

- Part 2 adds 664 m² of tidepool and 11,636 m² of shallows, taken from the inshore band.
- gameplay-systems checks the bands, as for part 1 (part 1's decision 9).

---

## 9. The previews

**How they were made:**
- **The renderer:** every preview is the kit's own renderer, run on Node through its public API from this plan's harness (`p2_window.js`, Appendix B), with the plan's ground, water, forms and plants fed in. No kit file is edited.
- **The camera** is the kit's (§1.2): 32 px per m across, 20.569 px per m up the screen for ground y, and 24.513 px per m of height. So a 520 × 450 window spans 16.25 m of x and about 21.8 m of y at sea level.
  - Height lifts the ground up the screen, so a window sees higher ground from further south. "Sees +6 m ground to y" is how far back the brows show.
- **The light:** the kit's afternoon preset at 14:00, except the landing, which is at the intro's clock (§6.1).
- **The still water** is added at its own level, as the game will (part 1 §7).

**The columns:**
- **Sprites:** the plant and rock sprites drawn. In brackets, the plants hidden behind a face or a nearer form.
- **Cliffs and forms in it:** the walls in the window, and each form with its top. A form's top is its height above the window's sea plus the tide.
- **Still water:** the pixels of still water drawn.
- **Face px / still lifted:** the face pixels drawn, and how many still read lifted above their own column after the walls-alone workaround (§1.7, finding 1).

| Picture | What it shows | Id | World window (m) | Tide | Preset | Sprites: plants / rocks (plants hidden) | Cliffs and forms in it: tops (m) | Still water (px) | Face px / still lifted | Size |
|---|---|---|---|---:|---|---|---|---:|---|---:|
| `preview-strand.png` | Harbour Strand | `coast.stp_harbour_strand` | x 186.00 to 202.25, y 16.16 to 38.00 | +0.00 | afternoon, 14:00 | 183 / 2 (0) | — | — | — | 74,056 B |
| `preview-landing.png` | The Landing, 2 × 2 windows | `coast.stp_the_landing` | x 186.00 to 218.50, y −21.69 to 22.00 | −0.854 | dawn, 06:00 (the intro) | — | — | — | — | 228,046 B |
| `preview-south-arm.png` | South Arm and the Harbour Stack | `coast.stp_south_arm` | x 180.00 to 196.25, y −41.84 to −20.00 (sees +6 m ground to y −12.86) | +0.00 | afternoon, 14:00 | 334 / 5 (75) | walls 006–019, brows +6.0 · Harbour Stack +6.20 | — | 58,169 / 204 | 50,818 B |
| `preview-east-gap.png` | East Gap: the throat and Gap Brook | `coast.stp_east_gap` | x 152.00 to 168.25, y −48.84 to −27.00 (sees +6 m ground to y −18.44) | −1.20 | afternoon, 14:00 | 470 / 4 (21) | walls 019–027, brows +6.0 · reef bench −0.33 | 34,180 | 40,874 / 4,486 | 98,750 B |
| `preview-east-gap-spit.png` | East Gap: the spit and its pools | `coast.stp_east_gap` | x 166.00 to 182.25, y −63.84 to −42.00 (sees +6 m ground to y −33.44) | −1.20 | afternoon, 14:00 | 240 / 14 (25) | walls 018–022, brows +6.0 · reef bench −0.46 | 40,298 | 529 / 159 | 92,952 B |
| `preview-east-ledges.png` | East Ledges reef | `coast.stp_east_ledges` | x 120.00 to 136.25, y −78.84 to −57.00 (sees +6 m ground to y −48.44) | −1.20 | afternoon, 14:00 | 504 / 2 (27) | walls 028–034, brows +6.0 · reef bench −0.32 | 990 | 65,014 / 59 | 84,937 B |
| `preview-west-gap.png` | West Gap and its spit | `coast.stp_west_gap` | x −6.00 to 10.25, y −81.84 to −60.00 (sees +6 m ground to y −51.44) | −1.20 | afternoon, 14:00 | 244 / 10 (11) | walls 053–059, brows +6.0 · reef bench −0.27 | 43,467 | 1,445 / 18 | 92,243 B |
| `preview-west-ledges.png` | West Ledges reef | `coast.stp_west_ledges` | x −25.00 to −8.75, y −61.84 to −40.00 (sees +6 m ground to y −31.44) | −1.20 | afternoon, 14:00 | 609 / 2 (17) | walls 057–066, brows +6.0 · reef bench −0.52 | 0 | 98,652 / 178 | 81,168 B |
| `preview-sisters.png` | The Sisters | `coast.stp_sw_bluff` | x −58.00 to −41.75, y −46.84 to −25.00 (sees +6 m ground to y −17.86) | +0.00 | afternoon, 14:00 | 390 / 27 (82) | walls 067–078, brows +6.0 · The Sisters 1 +5.20 · The Sisters 2 +4.10 | — | 38,990 / 192 | 51,280 B |
| `preview-storm-beach.png` | Storm Beach | `coast.stp_storm_beach` | x −66.00 to −49.75, y −27.84 to −6.00 | +0.00 | afternoon, 14:00 | 274 / 12 (0) | — | 10,374 | — | 76,421 B |
| `preview-heath-pond.png` | Heath Pond and Brook | `pond.stp_heath_pond` | x 8.00 to 24.25, y −65.84 to −44.00 (sees +6 m ground to y −36.86) | +0.00 | afternoon, 14:00 | 545 / 1 (16) | walls 049–055, brows +6.0 | 44,881 | 46,624 / 0 | 89,095 B |
| `preview-crossing-low.png` | The crossing, spring low | `coast.stp_the_bar` | x −170.00 to −153.75, y −11.84 to 10.00 | −2.20 | afternoon, 14:00 | 30 / 0 (0) | — | 16,177 | — | 27,961 B |
| `preview-crossing-high.png` | The crossing, spring high | `coast.stp_the_bar` | x −170.00 to −153.75, y −11.84 to 10.00 | +2.20 | afternoon, 14:00 | 30 / 0 (0) | — | — | — | 5,357 B |
| `option-a.png` | (a) v6 replaces the walls — walls 041–043 | `coast.stp_weather_cliff` | x 64.25 to 80.50, y −80.84 to −59.00 (sees +6 m ground to y −50.44) | −1.20 | afternoon, 14:00 | 198 / 5 (62) | walls 039–044, brows +6.0, jointed · The Old Man +6.40 | 550 | 149,599 / 2,614 | 60,100 B |
| `option-b.png` | (b) v6 joins the walls — walls 041–043 | `coast.stp_weather_cliff` | x 64.25 to 80.50, y −80.84 to −59.00 (sees +6 m ground to y −50.44) | −1.20 | afternoon, 14:00 | 204 / 5 (56) | walls 039–044, brows +6.0 · The Old Man +6.40 | 550 | 136,474 / 585 | 50,352 B |
| `option-c.png` | (c) a mix — walls 041–043 | `coast.stp_weather_cliff` | x 64.25 to 80.50, y −80.84 to −59.00 (sees +6 m ground to y −50.44) | −1.20 | afternoon, 14:00 | 204 / 5 (56) | walls 039–044, brows +6.0 · The Old Man +6.40 | 550 | 136,474 / 585 | 50,369 B |
| `preview-east-ledges-c.png` | East Ledges reef as (c): no bench, the floor without occ and skyv | `coast.stp_east_ledges` | x 120.00 to 136.25, y −78.84 to −57.00 (sees +6 m ground to y −48.44) | −1.20 | afternoon, 14:00 | 505 / 2 (26) | walls 028–034, brows +6.0 | 3,669 | 64,777 / 0 | 85,028 B |
| `preview-flats-weather.png` | The Weather Flats at a spring low: the Old Man and his gut | `flats.stp_south_flats` | x 58.00 to 74.25, y −85.84 to −64.00 (sees +6 m ground to y −54.25) | −2.20 | afternoon, 14:00 | 192 / 3 (45) | walls 040–045, brows +6.0 · The Old Man +6.40 | 4,450 | 110,942 / 611 | 48,706 B |
| `preview-flats-weather-neap.png` | The Weather Flats at a neap low | `flats.stp_south_flats` | x 58.00 to 74.25, y −85.84 to −64.00 (sees +6 m ground to y −55.69) | −0.99 | afternoon, 14:00 | 194 / 3 (43) | walls 040–045, brows +6.0 · The Old Man +6.40 | 2,713 | 112,963 / 593 | 42,636 B |
| `preview-flats-east.png` | The East Flats at a spring low: a pool, a hollow and the drains | `flats.stp_south_flats` | x 106.00 to 122.25, y −105.84 to −84.00 (sees +6 m ground to y −74.25) | −2.20 | afternoon, 14:00 | 51 / 0 (0) | reef bench −0.55 | 51,073 | 3,402 / 0 | 53,603 B |
| `preview-flats-ribs.png` | The Weather Flats' rock ribs, a pool and the drains at a spring low | `flats.stp_south_flats` | x 68.00 to 84.25, y −113.84 to −92.00 (sees +6 m ground to y −82.25) | −2.20 | afternoon, 14:00 | 148 / 0 (1) | — | 56,809 | — | 64,093 B |
| `preview-flats-sisters-gut.png` | The Sisters' Gut at a spring low | `flats.stp_south_flats` | x −66.00 to −49.75, y −57.84 to −36.00 (sees +6 m ground to y −26.25) | −2.20 | afternoon, 14:00 | 158 / 10 (35) | The Sisters 1 +5.20 · The Sisters 2 +4.10 · reef bench −0.84 | 37,737 | 13,694 / 44 | 56,171 B |

**Two windows keep a visible share of lifted face pixels:**
- the East Gap's throat: 4,486 of 40,874 (11%);
- option (a): 2,614 of 149,599 (1.7%).

Both come from the kit's foot clamp (§1.7, finding 1). The fix is the kit's, so it is in the Claude Design paste (decision 9).
- Every other window with a wall in it is at 1.3% or under.
- The one exception is the East Gap spit, which shows only 529 face pixels in all; 159 of them read lifted.

**The previews that are not shown elsewhere in this doc:**

| | |
|---|---|
| ![Harbour Strand](st-peters-terrain-pass-9-part-2/preview-strand.png) | ![South Arm and the Harbour Stack](st-peters-terrain-pass-9-part-2/preview-south-arm.png) |
| **Harbour Strand**, x 186.00 to 202.25, y 16.16 to 38.00, mean tide | **South Arm and the Harbour Stack**, x 180.00 to 196.25, y −41.84 to −20.00, mean tide |
| ![East Gap: the throat and the Gap Brook](st-peters-terrain-pass-9-part-2/preview-east-gap.png) | ![East Gap: the spit and its pools](st-peters-terrain-pass-9-part-2/preview-east-gap-spit.png) |
| **East Gap: the throat and the Gap Brook**, x 152.00 to 168.25, y −48.84 to −27.00, tide −1.2 | **East Gap: the spit and its pools**, x 166.00 to 182.25, y −63.84 to −42.00, tide −1.2 |
| ![East Ledges reef](st-peters-terrain-pass-9-part-2/preview-east-ledges.png) | ![East Ledges as option (c)](st-peters-terrain-pass-9-part-2/preview-east-ledges-c.png) |
| **East Ledges reef**, x 120.00 to 136.25, y −78.84 to −57.00, tide −1.2 | **The same, as option (c):** no bench, and the floor without `occ` and `skyv` |
| ![West Gap and its spit](st-peters-terrain-pass-9-part-2/preview-west-gap.png) | ![West Ledges reef](st-peters-terrain-pass-9-part-2/preview-west-ledges.png) |
| **West Gap and its spit**, x −6.00 to 10.25, y −81.84 to −60.00, tide −1.2 | **West Ledges reef**, x −25.00 to −8.75, y −61.84 to −40.00, tide −1.2 |
| ![The Sisters](st-peters-terrain-pass-9-part-2/preview-sisters.png) | ![Storm Beach](st-peters-terrain-pass-9-part-2/preview-storm-beach.png) |
| **The Sisters**, x −58.00 to −41.75, y −46.84 to −25.00, mean tide | **Storm Beach**, x −66.00 to −49.75, y −27.84 to −6.00, mean tide |
| ![Heath Pond and the Heath Brook](st-peters-terrain-pass-9-part-2/preview-heath-pond.png) | ![The Weather Flats at a neap low](st-peters-terrain-pass-9-part-2/preview-flats-weather-neap.png) |
| **Heath Pond and the Heath Brook**, x 8.00 to 24.25, y −65.84 to −44.00, mean tide | **The Weather Flats at a neap low**, x 58.00 to 74.25, y −85.84 to −64.00, tide −0.99 |
| ![The East Flats at a spring low](st-peters-terrain-pass-9-part-2/preview-flats-east.png) | ![The Weather Flats' rock ribs](st-peters-terrain-pass-9-part-2/preview-flats-ribs.png) |
| **The East Flats at a spring low:** a pool, a hollow and the drains; x 106.00 to 122.25, y −105.84 to −84.00 | **The Weather Flats' rock ribs,** a pool and the drains at a spring low; x 68.00 to 84.25, y −113.84 to −92.00 |
| ![The Sisters' Gut at a spring low](st-peters-terrain-pass-9-part-2/preview-flats-sisters-gut.png) | ![The crossing at a spring low](st-peters-terrain-pass-9-part-2/preview-crossing-low.png) |
| **The Sisters' Gut at a spring low**, x −66.00 to −49.75, y −57.84 to −36.00 | **The crossing at a spring low**, x −170.00 to −153.75, y −11.84 to 10.00 |
| ![The crossing at a spring high](st-peters-terrain-pass-9-part-2/preview-crossing-high.png) | |
| **The crossing at a spring high**, the same window | |

---

## 10. The scene's layers, and the stop

Part 1 §10 lists every St Peters layer with its builder, and says whether it refreshes in place or only through `Build()`.
- **Part 2 adds no new kind of layer.** It changes which items stand on moved ground.
- **Its new objects** (the forms, the skerries and the scatters) spawn at load, as the plant field does.

**Layers that refresh in place, or by themselves** (part 1's table, with part 2's changes):

| Layer | Builder | Refreshes | What part 2 does to it |
|---|---|---|---|
| Height map | `TerrainPaintTool`'s bake (part 1 §10) | in place | the derived R16 map covers the whole island (PR 5) |
| The painted terrain's adoption | `TerrainPaintTool.AdoptOnOpenScene` | in place; hand-written YAML at St Peters | needed, as in part 1 |
| Splat A to F | the deriver (part 1 §8.2) | in place | adds the new sections' recipes; `_SplatF` (PR 2) |
| The water | `WaterSurface`, `HiddenHarboursWater`, `WaterOverlay`, `TidalFace` | at runtime | the new still water (PR 4, §7) |
| The plant field | computed at load (part 1 §6) | at load | carried round the island (§8.2) |
| **The forms, their footprint colliders, the skerries and the scatters** (new) | `CoastFormField`, spawned at load from the Defs (PR 5b) | at load | new: 4 stacks, 7 skerries and 87 scatter rocks (§4.4); under option (b), the reef benches join them in PR 8 |
| Walkability, fleet lanes, traps and fish | `TidalWalkability`, `AmbientFleetPresenter`, `TrapPlacement`, `FishSchools` | at runtime | they read the new terrain; for the fleet, see §7.6 |

**Layers that change only through `Build()`,** and what part 2 does to them:

| Layer | Builder | Part 2 |
|---|---|---|
| IslandGrass, ShorePlants, IslandShrubs, IslandFlowers | `StPetersWoodsPlanter.Plant` | they retire at St Peters (part 1's decision 3 (b)) |
| 🔴 Shoreline | `StPetersShorePainter.Paint` | PR 4b's refresh, widened to the whole island, plus the nine swaps (§4.4) |
| 🔴 ClamHoles | `ScatterClamHoles` | PR 4b's refresh, widened to the whole island |
| StPetersNavMarks | `StPetersNavMarks.Place` | nothing new: the reef north mark stays at part 1's 0.83 m |
| IslandWoods, IslandUnderstorey | `StPetersWoodsPlanter.Plant` | untouched: no ground moves under them |
| CliffWalls | `StPetersCliffWalls.Build`; CLIFFS' lane | untouched under option (b). Option (a) replaces it, which PR 4b cannot do (§3). |
| The landing's roots, the inhabitants, the routines and the passages | `Build()` | untouched: the ground changes by 0.0 m under each of the 26 placed roots |

**The Build()-only items on ground that part 2 moves.** This is measured against part 1's plan; part 1's own table in its §10 is against today.

| Root | Items | On ground that moved more than 0.2 m from part 1's plan | Newly between the tides | Standing in still water |
|---|---:|---:|---:|---:|
| ShorePlants | 384 | 59 | 0 | 7 |
| IslandShrubs | 116 | 4 | 0 | 3 |
| IslandFlowers | 33 | 1 | 0 | 2 |
| IslandUnderstorey | 32 | 0 | 0 | 0 |
| Shoreline | 21 | 2 | 0 | 0 |
| IslandWoods | 259 | 0 | 0 | 0 |
| ClamHoles | 66 | 8 | 0 | 0 |

- **The clam holes:** 66 in the band ±1.8 m, now between −1.74 and +1.66 m.
  - 33 change their height by more than 0.02 m, by at most 0.543 m.
  - None leaves the band, and none stands in a pool.
- **The nav marks:** all 13 hold their depths. The reef north mark's spring-low depth stays at part 1's 0.83 m.
- **The placed roots:** 26 of them, including the inhabitants, the landing's roots, the passages and the cliff walls. The ground changes by 0.0 m under every one.

🔴 **The stop.** Five Build()-only layers stand under the new shores: ShorePlants, IslandShrubs, IslandFlowers, Shoreline and ClamHoles. The reef north mark's record gains nothing. The charter's rule is that a layer that changes only through `Build()` stops the plan's building, so it is reported here and in §0.

**Can PR 4b take a step for it? Yes, for all of it:**
- **The three plant roots** retire (part 1's ruling 3 (b)), so they need no step.
- **Shoreline:** PR 4b's step, widened from part 1's shores to the whole island.
  - It recomputes the root's rocks with the shore painter's own placement on the new terrain.
  - It writes one YAML patch to that root, gated by named deletions.
  - The nine rocks that take v6 forms keep their places and change only their sprite (§4.4).
- **ClamHoles:** PR 4b's step, widened. It uses the same function (`ScatterClamHoles`) over the whole island.
- **The new forms, skerries and scatters** are spawned at load by `CoastFormField` from their Defs (PR 5b), as the plant field is. They have no root, so nothing goes stale.

**PR 4b cannot do two things, and the plan asks neither of it:**
- replace `CliffWalls` (option (a));
- move the landing (§6.3).

Both are moves, not refreshes.

**Building waits for the owner's word on decision 11** (§13): PR 4b's widened steps, and the forms spawned at load.

---

## 11. The amended PR plan

Part 1's §11 is the base. Its five PRs keep their numbers, lanes and order. Part 2 amends three of them and adds four.

**What changes:**
- **PR 2 lands TerrainLight6 in place of TerrainLight5,** with its three inputs unset. TerrainLight6 is TerrainLight5 verbatim plus three optional inputs, and unset it is TerrainLight5 exactly (110 of 110 cases, §1.2). So nothing PR 2 promised changes, and PR 8 can bind the inputs later without landing the rig again.
- **PR 4b and PR 5 widen** from part 1's shores to the whole island.
- **Four PRs are new:**
  - 6: the kit lands;
  - 5b: the coast forms;
  - 8: option (b)'s second step, the benches and the floor's light;
  - 7: Nine Mile Creek's half, only if decision 8 says so.
- **PR 3 and PR 4 do not change.** Part 2 adds no species and no biome (§8.2). Its water is data in PR 5's still map, read through PR 4's seam as planned (§7).

**A correction to part 1: `docs/art/rigs/**` is art-director's.** `agents/coordination.md:40` gives that folder to art-director alone ("no other role edits here"). So:
- v6's rig bytes, its SHA256SUMS, its bake driver and its sidecars are art-director's, in PR 6;
- part 1's PR 2 (`docs/art/rigs/terrain/pass9/`) and PR 3 (`docs/art/rigs/plants/`) need art-director for those folders too. Part 1 gave them to art-pipeline alone.

**Every PR,** as in part 1: the MB figures are estimates, and tests are named with their production subject, as *TestClass (subject)*.

### Summary

| PR | Lane | Slot | Adds (estimate) | After |
|---|---|---|---|---|
| **2: the ground** (amended) | art-pipeline; art-director for `docs/art/rigs/terrain/pass9/` | one (the bake and the plate) | as part 1: +11 to +12 MB. TerrainLight6's rig bytes replace TerrainLight5's (25 KB). | — (beside PRs 4 and 6) |
| **3: the plants** (unchanged) | art-pipeline, with tools-editor; art-director for `docs/art/rigs/plants/` | one (the bake) | as part 1: +8 to +15 MB | the tree PR |
| **4: still water and one height source** (unchanged) | lead-architect, with gameplay-systems | CI, plus one slot for a plate | about 0.1 MB | — |
| **4b: the layer refresh** (widened) | world-content, with tools-editor | CI only (the refresh runs in PR 5's slot) | about 0 | — |
| **5: St Peters** (widened) | world-content | one | part 1's +0.3 to +0.5 MB of maps (the same maps on the same grid); the plant field +16.8 KB; about −2.2 MB when the plant roots retire (part 1) | PRs 2, 3, 4 and 4b, and CLIFFS' Phase C (as part 1) |
| **6: the kit lands** (new) | art-director; art-pipeline for the sheets' import | one (the bake and the import) | 4.24 MB of rig bytes, the PNGs in LFS; the stack sheets +2.7 MB of texture memory (one state) or +8.2 MB (three tide rows) | — (beside PRs 2 and 4) |
| **5b: the coast forms** (new) | world-content, with gameplay-systems for the colliders | one (a plate of the stacks against the walls) | about 0.1 MB | PRs 5 and 6 |
| **8: the benches and the floor's light** (new; option (b)'s second step) | art-pipeline, with gameplay-systems for the slow tick | one (the bake and the plate) | the benches about +1.1 MB; `skyv` 1.5 MB (R8); `occ` 1.5 MB of memory, never saved | PRs 2 and 5b |
| **7: Nine Mile Creek's half** (new; only if decision 8) | world-content | one | small: Nine Mile Creek's own maps | PR 5 |
| **CLIFFS' Phases C and D** | the CLIFFS lane | as chartered | as chartered | as chartered |

**The order:** PR 6 beside PRs 2 and 4; PR 4b, widened, before PR 5; PR 5b after PRs 5 and 6; PR 8 after PRs 2 and 5b; PR 7 last, if ruled (decision 10). Under option (c), PR 8 is dropped.

### PR 2: the ground (amended)
**What changes from part 1's PR 2:**
- `docs/art/rigs/terrain/pass9/` takes v6's `terrainLight6.js` (25,056 B, sha256 `f1fd93c0eeb85385…`) byte for byte, in place of the drop's `terrainLight5.js`. The folder's SHA256SUMS covers it. This part is art-director's.
  - PR 6 carries the same bytes in the kit's own folder. PR 2 keeps its own copy, so that the two can run side by side. Both SHA256SUMS name the same hash.
- `Code/Art/TerrainLight5.cs` becomes **`Code/Art/TerrainLight6.cs`**: the line-for-line C# twin, with the three inputs.
- `Art/Shaders/HiddenHarboursTerrainSplat.shader`: TerrainLight6's relight in HLSL, with `occ` off, `skyv` = 1 and `seaDir` = 1 until PR 8 binds them. That is TerrainLight5's relight, so PR 2's plate against the drop's PNGs is unchanged.
- Everything else in part 1's PR 2 stands.

**Tests added:**
- `TerrainLight6TwinTests` (TerrainLight6), in place of part 1's `TerrainLight5TwinTests`: the twin equals the rig at sampled G-buffer texels, **with the inputs unset and with each one set.** The set cases are §1.2's controls: `occ` over half the frame, `skyv` = 0.5, and `seaDir` = −1 at frame 7.
- `TerrainSplatPathSlotTests` and `TerrainKitPass9BytesTests`: as part 1.

**Tests changed:** as part 1. **Tests retired:** none. **Slot and MB:** as part 1.

### PR 3: the plants (unchanged)
Part 2 adds no species, no biome and no sheet: the new shores take part 1's five biomes (§8.2). PR 5 carries the field round the island. The only change is who owns `docs/art/rigs/plants/` (art-director, above).

### PR 4: still water and one height source (unchanged)
- **The seam is unchanged.** `IStillWater` answers max(tide, still) at a point.
  - Part 2's water is 2,597 m² of still water in all: part 1's 392.75 m², and 2,204.25 m² of new brooks, creeks, drains, ponds, pools and hollows (§7).
  - It is all data in PR 5's still map, so PR 4 needs nothing new.
- **R16 over −4 to +7 m stands** (decision 5). The plan tops out at +6.65 m, and the stacks are sprites, not ground.

### PR 4b: the layer refresh (widened)
**Files:** part 1's `Code/App/Editor/StPetersLayerRefresh.cs`, with its steps widened:
- **Shoreline:** the whole island, not part 1's shores only. The step recomputes the root's rocks with `StPetersShorePainter`'s own placement on the new terrain, and writes one YAML patch to that root, gated by named deletions.
  - **A new sub-step, the nine swaps** (§4.4): each of the nine rocks keeps its transform and takes its `ShoreRockDef`'s Rock Px sprite (#840's sheets, already in the repo).
  - It adds the `ShoreRockDef` class, which the step reads. The nine Def files come with PR 5.
- **ClamHoles:** the whole island, with the same function (`ScatterClamHoles`).
- **The nav marks' records:** gain nothing. No mark's depth changes beyond part 1's (§10).

It adds nothing to the scene until PR 5 runs it, as in part 1.

**Tests added:**
- `StPetersLayerRefreshTests` (ScatterClamHoles, StPetersShorePainter, StPetersNavMarks): as part 1, over the whole island. Each refresh equals the builder's own function on the same terrain.
- `ShoreRockSwapTests` (StPetersLayerRefresh, ShoreRockDef): the nine rocks keep their positions, rotations and scales, and only their sprites change. No other `Shoreline` rock is touched by the swap.

**Tests changed and retired:** none. **Slot and MB:** CI only; about 0.

### PR 5: St Peters (widened)
**Files:** part 1's, widened:
- **Part 2's 80 Def files** under `Data/Terrain/StPetersPlan/` (Appendix A), one entity per file.
  - With them come the classes of the four new Def types the deriver reads: `FlatsDef`, `TidalPoolDef`, `FormDef` (its plinth is ground) and `RockScatterDef`. `ShoreRockDef` came with PR 4b.
- **`TerrainPlanDerivation`** learns part 2's kinds:
  - the new section types and their recipes;
  - the South Flats, with their drains, pools, hollows, ribs and guts;
  - the forms' plinths;
  - the bar's pools under the chosen layout;
  - the ponds, brooks and creeks.
  - It resolves each `RockScatterDef`'s rule to its rocks (87 in all), deterministic from the plan's seed, and writes them to the plan's manifest for PR 5b.
- **The maps, regenerated for the whole island:** the height (R16), `StPetersSplatA–F.png`, `StPetersStillWater.png` and the manifest.
- **`Scenes/StPeters.unity`: hand-written YAML only,** gated by named deletions, as in part 1. It also runs PR 4b's widened refresh.
- The scene exporter is re-run.

**Tests added** (part 1's, widened):
- `StPetersTerrainPlanDeterminismTests` (TerrainPlanDerivation): two derivations are identical, the committed maps equal a fresh one, and so do the manifest's 87 scatter rocks.
- `StPetersTerrainPlanGuardTests` (the derived maps). Part 1's cases, plus:
  - **the toe moat:** no ground changes within 3.5 m in front of any wall's toe. This session's plan changes 21,883 cells in front of the walls, and none inside the moat;
  - **the walls' channels:** the channel at each wall's foot equals today's;
  - **the new water:** the three ponds do not leak, and the three brooks never climb;
  - **the crossing's window:** the sill stays the gut's bed (−0.6 at x −234.1), so the crossing is cut above −0.10, as today;
  - **the landing:** 0.0 m of change under each landing point;
  - **the frozen mask:** 0.0 m against part 1's plan (338,011 cells of 0.5 m under layout B);
  - **the flats' window:** the flats' walks open at a spring low and never at a neap low (decision 12).
- `StPetersTerrainPlanDefValidationTests` (the plan's Defs, part 1's and part 2's): the ids are unique, stable, `type.snake_case` and one per file, and every reference resolves (a creek's `joins`, a pond's `outlet`, a scatter's `around`).

**Tests changed:** part 1's list stands. Part 2 moves these premises further, and each needs the owner's word:
- `StPetersTerrainTests` (TidalExposure, TidalTerrain): under layout B, the bar's crest changes inside the 11 crest pools (1,769 cells, about 442 m², by up to 1.165 m). The gut and the rest of the crest hold (decision 6).
- `ClamScatterTests` (ScatterClamHoles): against part 1's plan, 33 of the 66 holes change height by more than 0.02 m, by at most 0.543 m. All stay inside ±1.8 m.
- `FishDutyCycleAtTheLandingTests` (FishSchoolModel, FishSchoolMath): the bands shift further: +664 m² of tidepool and +11,636 m² of shallows at mean tide (§8.3).
- `StPetersCoastTests` (StPetersBuilder, CoastClass, CoastPlan) and `StPetersShoreMapTests` (StPetersShoreMap, ShoreMaterial): the south's coast and materials now come from the plan too.
- `SceneWeightGuardTests` (GrassField, the committed scenes): the plant field is 16,835 B larger (72,879 B against part 1's 56,044).

**Re-run, with their premise held:** part 1's list, and:
- `NineMileCreekMainlandTerrainTests` (MainlandTidalTerrain, StPetersBuilder): the passage at (−356, 0) lies inside the frozen mask, so the ground at the seam changes by 0.0 m;
- `AmbientFleetPlanTests` (AmbientFleetPlan): it reads only `StPetersBuilder`'s tide constants, so the fleet cut-off moves no guard (§7.6).

**Tests retired:** as part 1.

**Slot and MB:** one slot. The maps are part 1's +0.3 to +0.5 MB, though the south's new detail may compress a little worse. The plant field grows by 16.8 KB.

### PR 6: the kit lands (new)
**Lane:** art-director, with art-pipeline for the sheets' import. **Slot:** one, for the bake and the import.

**Files:**
- `docs/art/rigs/cliff-rock-kit-v6/`: the zip's `export/cliff-rock-kit-v6/`, **all 31 files byte for byte** (4.24 MB; the 13 PNGs in LFS), with:
  - **SHA256SUMS:** Claude Design's, if the paste brings it (decision 9). If not, PR 6 computes it from the zip and says so;
  - **the kit's checkers,** if Claude Design sends them.
- **The form bake driver:** `docs/art/rigs/cliff-rock-kit-v6/bake/_pxFormExport.js`, after `rock-px-kit/bake/_pxRock2Export.js`.
  - It drives the kit's public API only (`PxCliff3`), and edits no kit file.
  - It bakes the four stacks' sheets. The bench strips come in PR 8.
- **The gameplay sidecars:** `docs/art/rigs/gameplay/pxCliff3Forms.gameplay.json`. For each of the six forms (the stacks and the skerries) it gives the footprint, the pivot, the sort line and the plinth.
- **The sheets:** `Assets/_Project/Art/Sprites/Shore/CoastForms/`, with their metas and a catalog JSON, as `RockPx/RockPx.json` is for the rocks.
  - The import is `Art/Editor/CoastFormCatalog.cs` and `CoastFormSheetSlicer.cs`, after `RockPxCatalog.cs` and `RockPxSheetSlicer.cs`.
  - The slicer ignores the PNGs' `caBX` chunk (§1.7).

**Tests added:**
- `CliffRockKitV6ContractTests` (the kit's folder, against SHA256SUMS): every file matches its sum, and no file is missing or extra.
- `CoastFormSheetTests` (CoastFormSheetSlicer, the sidecars): there is one sheet and one sidecar per form; each footprint lies inside its sprite; each pivot sits on its plinth; and the rows are dry, wet and awash, if the three-row sheet is chosen.

**Tests changed and retired:** none.

**MB:** 4.24 MB of rig bytes. The stack sheets add +2.7 MB of texture memory (one state) or +8.2 MB (three tide rows), §3.1.

### PR 5b: the coast forms (new)
**Lane:** world-content, with gameplay-systems for the colliders. **Slot:** one, for a plate of the stacks against the walls at a spring low, mean tide and a spring high.

**Files:**
- **`Code/World/CoastFormField.cs`.** At load, it spawns the 4 stacks and the 7 skerries from their `FormDef`s, and the 87 scatter rocks from the manifest (PR 5).
  - The forms get **footprint colliders** from the sidecars (finding 1): static colliders, which stop the player's `Rigidbody2D`.
  - The scatter rocks and the nine swapped rocks do not block walking, as today's rocks do not.
  - Everything is y-sorted with the walls, and pooled (rule 7).
  - Whether the stacks cast `SpriteShadow` is PR 5b's call. If they do, `LitDecorCasterBudgetTests` counts them.
- **`Scenes/StPeters.unity`:** the component, in hand-written YAML.

**Tests added:**
- `CoastFormFieldTests` (CoastFormField): the field is deterministic. It spawns 4 stacks, 7 skerries and 87 scatter rocks, which with the nine swaps are the 103 rock sprites of §4.4, each at its Def's or manifest's position.
- `CoastFormBlocksWalkPlayTests` (CoastFormField, PlayerWalkController): at a spring low, the player cannot walk through the Old Man's footprint, and can walk round it.

**Tests changed and retired:** none. **MB:** about 0.1.

### PR 8: the benches and the floor's light (new; option (b)'s second step)
**Lane:** art-pipeline, with gameplay-systems for the slow tick. **Slot:** one.

**Files:**
- **The bench strips:** baked by PR 6's driver (136 m of reef edge under the ledges, §3.1) and placed by `CoastFormField`.
- **An `occ` field on the slow tick** (rule 7). It is TerrainLight6's shadow mask (1 where the sun is blocked), recomputed as the sun moves, deterministic from the game's time and never saved (rule 5). It covers only the floor a wall or form can shade.
- **A `skyv` bake:** the deriver writes one R8 map per region (1,520 × 1,040 at St Peters, 1.5 MB), with the kit's `floorSky` rule.
- **TerrainLight6's inputs bound** in the shader. `seaDir` is set per section **on the south only**. The east and west stay unset until the kit takes a 2-D sea direction (decision 9).

**Tests added:**
- `FloorOcclusionFieldTests` (the `occ` field): deterministic from the time; it matches the kit's `PxCliff3.occlusion` on sampled cells; it stays inside the slow tick's budget.
- `SkyVisBakeTests` (the deriver's `skyv` bake): it matches the kit's `floorSky` on sampled cells.

**Tests changed:** `TerrainLight6TwinTests` (TerrainLight6) gains the bound inputs' cases.

**MB:** the benches about +1.1 MB; `skyv` 1.5 MB; `occ` 1.5 MB of memory.

### PR 7: Nine Mile Creek's half (new; only if decision 8)
- **What:** the bar's flank pools and runnels, and its materials, on Nine Mile Creek's half.
- **First, a survey of Nine Mile Creek's layers,** as part 1 §10 made for St Peters. Its terrain is the analytic `MainlandTidalTerrain` (§2.6), and this plan has not read which of its layers change only through its own builder.
- **The window does not move:** Nine Mile Creek's gut bed is −0.6, the same as ours.
- **Tests:** re-run `NineMileCreekMainlandTerrainTests` (MainlandTidalTerrain), with its seam premise held. The rest are named by the survey.

### CLIFFS' Phases C and D
As chartered, under (b) or (c). Option (a) would make St Peters' Phase C moot and change `StPetersCliffWalls.Build` (§3.1). This plan does not ask for that.

---

## 12. Risks

1. **The Build()-only layers** (§10). Five of them stand under the new shores.
   - Part 1's ruling covers them: the three plant roots retire, and PR 4b's widened steps refresh Shoreline, with the nine swaps, and ClamHoles.
   - The new forms spawn at load.
   - Building waits for decision 11.
2. **A wall does not stop a walker** (§2.4). The flats bring players to the walls' toes at a spring low.
   - The forms get footprint colliders (PR 5b). A wall is still "pure look", so a player can walk from the flats up a cliff's face.
   - A slope rule, or colliders along the brows, is gameplay-systems' question, outside this plan.
3. **The foot clamp is a preview artifact.** In the game, a wall draws its own baked face, and the forms are separate sprites y-sorted with it, so the clamp cannot happen there.
   - The fix is still asked of the kit (decision 9), because PR 8's bench bake composes faces behind forms.
   - The worst preview window, the East Gap's throat, still reads 11% of its face pixels lifted after the workaround (§9).
4. **The cliff walk's steepest 2 m** rise 0.965 m per m, near (−39, −18) (§8.1). There is no slope rule, so the game will not stop a walker there. A switchback is data, if a playtest asks for one.
5. **The flats' flood must be felt in a playtest,** as part 1's risk 5.
   - The walks open for 1.91 h of a spring tide, and never at neaps.
   - The guts turn boat-only above a tide of −0.4.
6. **`seaDir` on the east and west** stays unset until the kit takes a 2-D sea direction (decision 9). Their swell keeps TerrainLight5's phase.
7. **The fleet** (§7.6). At a spring low, the flats cut the fleet rect's 13 deep cells off from the harbour mouth. This is a note for gameplay-systems (decision 12). No guard moves.
8. **Texture memory:**
   - the stacks +2.7 MB (one state) or +8.2 MB (three rows);
   - the benches about +1.1 MB;
   - `occ` and `skyv`, 1.5 MB each.
   - The rocks add 0 MB to the repo. But St Peters would hold up to 13.8 MB of #840's sheets in memory, with at most 5 sheets on one screen.
   - That is well inside a PC's budget. The mobile port should count it (rule 7).
9. **The cost of `occ`.** The kit's full relight takes 109 to 124 ms per scene in JavaScript (§1.2). PR 8 must compute `occ` on the slow tick, only for the floor a wall or form can shade, and measure it there (rule 7).
10. **Layout B moves `StPetersTerrainTests`' crest premise** inside the crest pools: 1,769 cells, by up to 1.165 m (decision 6).
11. **The dock shift's costs, if it is chosen** (§6.3): a Build()-only move of five roots, 12 guard classes, and a re-lay of #846.
12. **If Nine Mile Creek is not touched,** the bar's detail stops at the seam. St Peters' half gets pools and runnels; Nine Mile Creek's half stays one sand texture (decision 8).
13. **The previews are the kit's renderer, not the engine,** as part 1's risk 12. PR 5's plate must match them.
14. **Two one-cell dips hold water at a neap low** (0.00 and 0.01 m deep, §7.6). This is trivial, and PR 5's deriver can fill them.

---

## 13. Decisions for the owner

Each decision has a *Recommendation*. The seat does not decide these, and neither does this lane: the owner does.

1. **The plan.** The sections, the South Flats, the forms, the water, the paths and the biomes. Judge them on `plan-island.png`, `plan-south.png`, `plan-east.png`, `plan-west.png`, `flood.png` and the previews.
   - *Recommendation:* approve. Every shape is data (Appendix A), so any of them can be moved, renamed or re-typed later without code.
2. **The cliffs: (a), (b) or (c)** (§3), and with them CLIFFS' Phases C and D.
   - *Recommendation:* **(b), in two steps.** First the forms (PRs 6 and 5b); then the benches and the floor's light (PR 8). CLIFFS' Phases C and D go ahead as chartered.
   - (c) is (b) without PR 8.
   - (a) is a stop: it replaces the `CliffWalls` root and changes CLIFFS' builder.
3. **The rocks.**
   - *Recommendation:* as planned.
     - Nine of today's `Shoreline` rocks take Rock Px forms in place (PR 4b).
     - New rocks are scatters (87).
     - Nine Mile Creek's rocks are not touched.
4. **PR 2's light.**
   - *Recommendation:* PR 2 lands TerrainLight6 with `occ`, `skyv` and `seaDir` unset, which is TerrainLight5 exactly.
     - PR 8 binds `occ` and `skyv`, and `seaDir` on the south only.
     - The east and west wait for a 2-D sea direction (decision 9).
5. **The height range.**
   - *Recommendation:* keep part 1's −4 to +7 m. The plan tops out at +6.65 m, and the stacks are sprites.
6. **The crossing: layout A or B** (§5). The window is the same in both.
   - **A:** 12 flank pools. The crest stays frozen, so the walk stays straight.
   - **B:** A plus 11 crest pools, so the walk winds: 2 swings on the quickest line, 12 along the crest top. Four of the crest pools are swum.
     - B unfreezes 1,769 cells of the crest (about 442 m², by up to 1.165 m).
     - It moves `StPetersTerrainTests`' crest premise.
   - *Recommendation:* **B.** It is what the owner asked for: pools "so the player doesnt navigate in a purely straight line".
7. **The landing: keep and frame, or shift the dock 6 m** (§6).
   - *Recommendation:* **keep and frame.** The frame gives the intro its harbour, and it moves nothing. The shift costs five Build()-only roots, 12 guard classes and a re-lay of #846.
8. **Does Nine Mile Creek join?**
   - *Recommendation:* yes, as PR 7 after PR 5: the flank pools and runnels on its half, starting with a survey of its layers. The window does not move.
9. **Claude Design.**
   - *Recommendation:* send the paste below now. Sending it is the owner's.
10. **The order in part 1's arc.**
    - *Recommendation:*
      - PR 6 beside PRs 2 and 4;
      - PR 4b, widened, before PR 5;
      - PR 5b after PRs 5 and 6;
      - PR 8 after PRs 2 and 5b;
      - PR 7 last, if decision 8 says yes.
11. **The Build()-only stop** (§10).
    - *Recommendation:* accept this way through it:
      - PR 4b's widened steps: Shoreline with the nine swaps, and ClamHoles;
      - the forms spawned at load by `CoastFormField` (PR 5b).
    - Building waits for this word.
12. **The flats' teeth and the fleet.**
    - *Recommendation:*
      - accept the walking window (1.91 h of a spring tide, never at neaps);
      - accept the guts' depths (boat-only above −0.4);
      - send the fleet's cut-off to gameplay-systems, with part 1's decision 12.

**The Claude Design paste (decision 9).** The owner sends it; this lane does not.

```text
Hidden Harbours: cliff & rock kit v6, eight asks (from terrain pass 9, part 2)

We imported the v6 kit (zip sha256 aa632e3cfc899eef274963ee2a70fbe399f06c010e3224a2a53ae9dbb232bb20).
All 12 stills in renders/ re-render pixel-exact on Node, and TerrainLight6 with occ, skyv and seaDir
unset equals TerrainLight5 in 110 of 110 cases. Thank you. To land it, we would like:

1. SHA256SUMS for the 31 files under export/cliff-rock-kit-v6/, and the kit's checkers (self-tests),
   as with the earlier kits.
2. A form bake and sidecar spec. How to bake a PxCliff3 form (a sea stack) and a reef-bench strip to a
   sprite sheet at 32 px per m, with dry, wet and awash rows like Rock Px. And, per form, a gameplay
   sidecar: its footprint (the ground-contact outline), pivot, sort line, and plinth radius and height.
   Our player is a Rigidbody2D and our cliff walls have no colliders, so the footprint is what stops
   a walker.
3. A 2-D sea direction for TerrainLight6. seaDir only flips the swell along screen Y
   (terrainLight6.js:236). Our east and west shores face their sea along screen X.
4. The face compose's foot clamp. A face pixel's foot is the Z of the last non-face cell drawn in front
   of its column, so behind a stack or a bench the face above the form's top line is lifted to that top
   (vertical streaks). Could the foot come only from a cell that touches the face?
5. Ground risers. A step in the ground is drawn by stretching its top cell down the screen; only a
   tier's step becomes a lit face. Could ground steeper than about 0.7 m per m draw a riser?
6. viewer.dc.html loads 8 files from lib/lib/..., so it shows "A RIG DID NOT LOAD".
7. The settings behind options_sheet.png. Our closest match (time 16.45, cloud 0.05, fog 0.1) still
   differs in 40% of its pixels.
8. For your notes only: the PNGs carry a C2PA chunk (caBX). It is harmless, and our slicer will
   ignore it.
```

---

## Appendix A. The plan's data (part 2)

This is every value part 2's prototype derives from: the proposed Defs, in YAML. The conventions are part 1's:
- coordinates are world metres (x east, y north);
- elevations are metres about mean sea level (springs ±2.2, neaps ±0.99).

Part 1's values are unchanged. Part 2 adds **80 Def files:**

| Def | Type | Files | What |
|---|---|---:|---|
| `CoastSectionDef` | part 1's | 12 | the 11 sections, and `coast.stp_the_bar` (the crossing) |
| `FlatsDef` | **new** | 1 | `flats.stp_south_flats` |
| `CoastRecipeDef` | part 1's | 9 | the recipes of part 2's new section types |
| `FormDef` | **new** | 6 | the four stacks (the Sisters are one Def with two) and the three skerry groups |
| `TidalPoolDef` | **new** | 23 | the crossing's pools, `pool.stp_bar_a01`–`a12` and `b01`–`b11` |
| `PondDef` | part 1's | 3 | the Gap, Heath and Storm ponds |
| `StreamDef` | part 1's | 3 | the Gap, Heath and Storm brooks |
| `TidalCreekDef` | part 1's | 5 | three creeks across the flats, and the two guts (`kind: gut`) |
| `PathDef` | part 1's | 3 | the cliff walk and the two gap paths |
| `ShoreRockDef` | **new** | 9 | the nine of today's shore rocks that take Rock Px forms |
| `RockScatterDef` | **new** | 6 | the scatters' rules (87 rocks) |

**Derived and recorded, not Defs,** as part 1's pans are:
- the flats' 12 pools and 16 hollows, and the reefs' 10 rock pools;
- the 87 rocks the scatters place. The deriver writes them to the plan's manifest (PR 5), and `CoastFormField` spawns them from it (PR 5b).

**How the YAML was made.** It is the harness's data (`Evidence~/harness/p2_data.py`), written out by script. Four `why:` lines were then corrected here, where the harness's wording was out of date: the South Flats, the East Gap, the Weather Cliff and the West Ledges. They are prose, and no value changed.

```yaml
# terrain_plan.st_peters: part 2 extends part 1's RegionTerrainPlanDef (part 1's values are unchanged)
part2_seed: 9209
scope_bearing_deg: {east: [61.6, 96.6], south: [96.6, 252.0], west: [252.0, 285.0]}
scope_crossing: {frm: [-60.0, 0.0], to: [-356.0, 0.0], half_width: 45.0}
# the reference line: today's 0 m line, clockwise from the cannery to the bar head, land on the right;
# a third value starts that section
shore_reference_line_2:
  - [189.0, 37.5, "coast.stp_harbour_strand"]
  - [193.5, 32.1]
  - [197.0, 27.0]
  - [200.0, 21.7]
  - [202.7, 15.0, "coast.stp_the_landing"]
  - [204.2, 9.6]
  - [204.5, 4.0]
  - [204.3, -1.0]
  - [201.0, -6.0]
  - [196.2, -8.5, "coast.stp_south_arm"]
  - [189.9, -12.3]
  - [188.1, -17.2]
  - [185.7, -21.9]
  - [182.8, -26.6]
  - [179.4, -31.1]
  - [175.4, -35.5]
  - [170.9, -39.7]
  - [169.2, -44.3, "coast.stp_east_gap"]
  - [172.5, -50.2]
  - [170.5, -52.8]
  - [166.6, -54.4]
  - [158.5, -55.2, "coast.stp_east_ledges"]
  - [148.5, -54.6]
  - [141.8, -57.6]
  - [134.7, -60.4]
  - [127.3, -62.9]
  - [119.7, -65.1]
  - [111.8, -66.9]
  - [103.7, -68.5]
  - [95.4, -69.7]
  - [87.0, -70.4, "coast.stp_weather_cliff"]
  - [78.5, -70.9]
  - [70.0, -71.0]
  - [61.5, -70.9]
  - [53.0, -70.4]
  - [44.7, -69.5]
  - [36.4, -68.3]
  - [28.3, -66.8]
  - [15.8, -67.9, "coast.stp_west_gap"]
  - [9.2, -69.6]
  - [4.5, -69.0]
  - [0.8, -67.2]
  - [-1.4, -64.1, "coast.stp_west_ledges"]
  - [-3.5, -56.9]
  - [-10.1, -53.8]
  - [-16.3, -50.4]
  - [-22.2, -46.7]
  - [-27.5, -42.9]
  - [-32.4, -38.8]
  - [-35.5, -35.5, "coast.stp_sw_bluff"]
  - [-39.4, -31.1]
  - [-42.8, -26.6]
  - [-50.8, -22.9, "coast.stp_storm_beach"]
  - [-61.2, -19.1]
  - [-62.7, -15.0]
  - [-63.9, -11.0, "coast.stp_bar_root"]
  - [-64.5, 0.0]
  - [-63.9, 11.0]
  - [-62.25, 16.4]
  - [-60.6, 20.4]
section_feather_deg: 3.0
feather_growth_deg_per_m: 0.22
end_feather_deg: 6.0
warp_amp_wavelength_m: [1.2, 7.0]
wall_keep: {buf: 3.0, feather: 2.0}   # buf m, feather m: every wall's brow and toe keep their ground
toe_rule: {slope: 0.8408, reach_m: 30.0, rule: "where the ground changes, e <= toeZ + s * 20.6 / 24.5 (s = m seaward of the toe)"}
reef: {channel: 5.0, rise: [6.0, 9.0], crest: [9.0, 17.0], fall: [17.0, 21.0], crest_z: -0.6, crest_amp: 0.3, gully_every: [15.0, 25.0], gully_w: [1.5, 2.5], gully_z: -1.6, pools_per_100m: 7, pool_r: [0.8, 1.5], pool_depth: [0.3, 0.5], broken_scale: 1.5, width_wander: 0.3, width_lam: 30.0}
hollows: {min_depth: 0.1, min_m2: 4.0}   # a hollow shallower or smaller is wet ground
creek_trickle_m: 0.06
creek_bank_m: [1.0, 2.0]
pool_lip: 0.5
bar_walk_clear_m: 1.0
clam_pull: {r: 1.5, feather: 4.0, margin: 0.03}

# coast sections (CoastSectionDef, one file each)
- id: "coast.stp_harbour_strand"
  name: "Harbour Strand"
  type: "harbour_strand"
  mode: "cut"
  edge: [1.5, 10.0]
  knots: [[16, 6.0], [12, 4.8], [9, 3.4], [7, 3.0], [5, 2.6], [0, 0.0],
    [-8, -0.8], [-18, -1.3], [-28, -1.7], [-36, -2.3]]
  sea_tail: 12
  why: "The harbour's north arm: part 1's Cannery Shingle carried round the point and softened to a sand-and-shingle strand where the entrance channel's shelter begins. It is shaped only outside the entrance route's 30 m capsule (north of about 72 deg); inside it the ground is today's and only the paint is new."
- id: "coast.stp_the_landing"
  name: "The Landing"
  type: "landing"
  mode: "paint"
  why: "The intro's landing is kept: slip, wharf, dock, dory berth, dredged approach and arrival route do not move. It is framed by paint (a weed line on the slip's flanks, a wrack line at the spring-high mark, shingle above) and by the forms either side: the strand to the north, the Harbour Stack to the south."
- id: "coast.stp_south_arm"
  name: "South Arm"
  type: "deep_cliff"
  mode: "toe"
  walls: ["000", "022"]
  why: "Walls 000-022 (DeepShoreCliff, E/SE) are the harbour's south arm. The deep water at their foot stays deep (the arrival is from the north-east); a stack and three skerries stand off the arm so the harbour reads as held between two arms."
- id: "coast.stp_east_gap"
  name: "East Gap"
  type: "gap_cove"
  mode: "cut"
  edge: [1.0, 8.0]
  keep_above: 1.0
  knots: [[6, 2.2], [3, 1.0], [0, 0.0], [-6, -0.7], [-14, -1.3], [-22, -1.8],
    [-30, -2.4]]
  sea_tail: 10
  spit: {bearing: 132.0, length: 18.0, crest: [1.2, -1.4], half_width: 6.0, max_cut: 1.6}
  why: "The Access ramp between walls 022 and 023: the cliff coast's one way down on the east. A shingle spit runs out along the gap's axis with a pocket beach either side; the Gap Brook comes down the ramp to the sea."
- id: "coast.stp_east_ledges"
  name: "East Ledges"
  type: "toe_reef"
  mode: "toe"
  walls: ["023", "039"]
  why: "Walls 023-039 (LedgeCliff) stand on a bench at -1.65. In front of it the plan lays a reef: a toe channel the walls' foot keeps, then a weed-covered crest that bares at every low, cut by gullies along the rock's joints and pocked with rock pools."
- id: "coast.stp_weather_cliff"
  name: "Weather Cliff"
  type: "deep_cliff"
  mode: "toe"
  walls: ["040", "054"]
  why: "Walls 040-054 (Cliff, S): the highest faces, into deep water. Nothing is laid against them; the Old Man stands off the middle, his plinth heaped with the cliff's fallen blocks."
- id: "coast.stp_west_gap"
  name: "West Gap"
  type: "gap_cove"
  mode: "cut"
  edge: [1.0, 8.0]
  keep_above: 1.0
  knots: [[6, 2.2], [3, 1.0], [0, 0.0], [-6, -0.7], [-14, -1.3], [-22, -1.8],
    [-30, -2.4]]
  sea_tail: 10
  spit: {bearing: 209.0, length: 18.0, crest: [1.2, -1.4], half_width: 6.0, max_cut: 1.6}
  why: "The Access ramp between walls 054 and 055, the heath's way down; the Heath Brook reaches the sea here."
- id: "coast.stp_west_ledges"
  name: "West Ledges"
  type: "toe_reef"
  mode: "toe"
  walls: ["055", "067"]
  broken: true
  why: "Walls 055-067 (LedgeCliff, S/SW): the same reef as the east, more broken (more gullies and pools, a more ragged edge) where the weather comes round the island's shoulder."
- id: "coast.stp_sw_bluff"
  name: "South-West Bluff"
  type: "deep_cliff"
  mode: "toe"
  walls: ["068", "078"]
  why: "Walls 068-078 (DeepShoreCliff, SW): the island's corner. The Sisters, two stacks, stand off it."
- id: "coast.stp_storm_beach"
  name: "Storm Beach"
  type: "storm_beach"
  mode: "cut"
  edge: [1.5, 9.0]
  knots: [[16, 6.0], [12, 4.2], [9.5, 2.7], [7.5, 3.1], [5, 2.2], [0, 0.0],
    [-6, -0.9], [-14, -1.6], [-24, -2.4]]
  sea_tail: 10
  why: "Where the bar's south flank meets the bluff, open to the south-west: a cobble storm beach with a berm at +3.1 and a swale behind it that the Storm Brook follows to the sea."
- id: "coast.stp_bar_root"
  name: "Bar root"
  type: "sandy_flats"
  mode: "keep"
  why: "Part 1's Bar-Head Flats (paint-only, the clam flats): kept as part 1 planned them."

# the crossing (CoastSectionDef, type the_bar; along its own axis)
- id: "coast.stp_the_bar"
  name: "The Bar"
  type: "the_bar"
  x: [-356.0, -70.0]
  half_width: 45.0
  gut_x: -234.1
  gut_keep: 20.0
  runnel_every: [9.0, 14.0]
  runnel_w: 0.9
  runnel_depth: 0.1
  musselbed: {near_gut: 26.0, band: [-1.3, -0.3]}
  lobe_amp: 0.45
  lobe_lam: 26.0
  lobe_band: [5.5, 8.0, 24.0, 30.0]
  why: "The north shore's low-tide detailing carried out along the bar: ripple and sand, runnels on the flanks (silt), eelgrass below -2.0, a mussel bed either side of the gut, and pools."

# the South Flats (FlatsDef)
- id: "flats.stp_south_flats"
  name: "The South Flats"
  width: [[100.0, 0.0], [106.0, 14.0], [116.0, 24.0], [126.0, 32.0], [137.0, 40.0], [150.0, 48.0],
    [163.0, 44.0], [177.0, 52.0], [192.0, 50.0], [205.0, 42.0], [214.0, 38.0], [227.0, 42.0],
    [240.0, 30.0], [252.0, 26.0], [259.0, 20.0], [266.0, 0.0]]
  lobes: [9.0, 46.0]
  inner: -0.45
  outer: -1.85
  outer_var: [0.3, 45.0]
  front: 12.0
  moat: [3.5, 8.0]
  reef: [10.0, 16.0]
  swell: [0.22, 30.0]
  ridges: [0.14, 16.0]
  ripple: [0.04, 3.5]
  runnels: {every: [13.0, 21.0], w: [0.9, 1.7], depth: [0.16, 0.3], meander: [3.0, 17.0], start: [0.06, 0.4], branches: [0, 3], branch_at: [0.3, 0.9], branch_len: [0.15, 0.45], branch_angle: [25.0, 55.0], branch_w: 0.55, branch_depth: 0.6, trickle: 0.04}
  pools: {per_100m: 6.0, r: [1.8, 4.2], depth: [0.22, 0.42], band: [0.25, 0.85]}
  ribs: {spacing: 9.0, width: 0.13, lift: 0.28, break_up: 0.45, wander: 0.55}
  mussels: {band: [-1.35, -0.45], lam: 6.0, cover: 0.62}
  character: {coast.stp_south_arm: "boulder", coast.stp_east_gap: "sand", coast.stp_east_ledges: "sand", coast.stp_weather_cliff: "ribs", coast.stp_west_gap: "sand", coast.stp_west_ledges: "mud", coast.stp_sw_bluff: "mud", coast.stp_storm_beach: "sand", coast.stp_bar_root: "sand"}
  why: "It lies between -0.45 and -1.85, close to the Lagoon Flats' band: a spring low bares it, every high floods it, and a neap low bares only its inner part. At mean tide it is sea to the cliffs' feet; at a low the stacks stand on it."

# coast recipes, part 2's new types (CoastRecipeDef coast_recipe.<type>): [top elevation m, material], first match from below
coast_recipe.harbour_strand: [[-2.0, "eelgrass"], [-1.1, "ripple"], [-0.2, "foreshore"], [0.9, "sand"], [2.9, "shingle"], [3.6, "marram"], [99, "grass"]]
coast_recipe.landing: [[-2.0, "eelgrass"], [-1.2, "ripple"], [-0.2, "rockweed"], [1.2, "foreshore"], [2.9, "shingle"], [3.6, "marram"], [99, "grass"]]
coast_recipe.deep_cliff: [[-2.3, "eelgrass"], [-1.43, "irishmoss"], [-0.05, "rockweed"], [2.4, "ledge"], [4.2, "shelf"], [99, "grass"]]
coast_recipe.toe_reef: [[-2.3, "eelgrass"], [-1.43, "irishmoss"], [-0.05, "rockweed"], [2.4, "ledge"], [4.2, "shelf"], [99, "grass"]]
coast_recipe.gap_cove: [[-2.0, "eelgrass"], [-1.0, "ripple"], [0.6, "foreshore"], [1.1, "shingle"], [2.4, "sand"], [3.8, "marram"], [99, "grass"]]
coast_recipe.storm_beach: [[-2.0, "eelgrass"], [-1.2, "ripple"], [-0.4, "foreshore"], [3.3, "shingle"], [3.9, "marram"], [99, "grass"]]
coast_recipe.the_bar: [[-2.0, "eelgrass"], [-0.6, "ripple"], [2.4, "sand"], [3.8, "marram"], [99, "grass"]]
coast_recipe.flats_sand: [[-1.75, "eelgrass"], [-0.95, "ripple"], [2.4, "sand"], [99, "grass"]]
coast_recipe.flats_mud: [[-1.75, "eelgrass"], [-1.3, "ripple"], [0.6, "mud"], [99, "grass"]]

# the forms (FormDef, one file each): a v6 tier baked like a wall face and y-sorted with the walls; the terrain carries
# only the plinth. Each carries a footprint blocker from its bake sidecar (the player is a Rigidbody2D; walkability is
# depth-only, so without it a walker at a low would pass through the rock).
- id: "form.stp_harbour_stack"
  name: "Harbour Stack"
  kind: "stack"
  c: [192.0, -38.0]
  r: [3.2, 2.6]
  rot: 20
  top: 6.2
  plinth: {r: 5.5, z: -1.2, skirt: 0.7}
  stone: "sandstone"
  batter: 80
  strata: 0.9
  why: "The harbour's south gatepost, 37.6 m from the dock and 30.5 m off the entrance's port mark p3."
  blocks_walk: true
- id: "form.stp_the_whelps"
  name: "The Whelps"
  kind: "skerries"
  pts: [[186.0, -46.0, 2.8], [192.0, -50.0, 2.2], [181.0, -52.0, 2.0]]
  plinth: {z: -1.2, skirt: 0.7}
  rock: "skerry"
  stone: "sandstone"
  why: "Three skerries trailing south of the stack: kit 'skerry' rocks on raised bases, awash at half tide."
  blocks_walk: true
- id: "form.stp_old_man"
  name: "The Old Man"
  kind: "stack"
  c: [70.0, -79.5]
  r: [2.8, 2.4]
  rot: -10
  top: 6.4
  plinth: {r: 3.5, z: -1.0, skirt: 0.8}
  stone: "sandstone"
  batter: 84
  strata: 1.0
  why: "A stack as tall as the Weather Cliff, 7.8 m off wall 042's toe with a channel between."
  blocks_walk: true
- id: "form.stp_the_sisters"
  name: "The Sisters"
  kind: "stacks"
  pts: [[-52.0, -40.0, 2.6, 5.2], [-47.0, -46.0, 2.2, 4.1]]
  plinth: {r: 4.0, z: -1.0, skirt: 0.7}
  stone: "sandstone"
  batter: 78
  strata: 0.8
  why: "Two stacks off the island's south-west corner, outside the fleet grounds' rect + 6 m and the bar's capsule."
  blocks_walk: true
- id: "form.stp_east_gap_skerries"
  name: "Gap skerries (east)"
  kind: "skerries"
  pts: [[189.5, -63.5, 1.8], [192.5, -61.0, 1.5]]
  plinth: {z: -1.3, skirt: 0.8}
  rock: "skerry"
  stone: "sandstone"
  why: "Off the East Gap spit's tip: where the spit's shingle came from."
  blocks_walk: true
- id: "form.stp_west_gap_skerries"
  name: "Gap skerries (west)"
  kind: "skerries"
  pts: [[-10.5, -85.0, 1.8], [-7.0, -87.5, 1.5]]
  plinth: {z: -1.3, skirt: 0.8}
  rock: "skerry"
  stone: "sandstone"
  why: "Off the West Gap spit's tip."
  blocks_walk: true

# the crossing's pools (TidalPoolDef): water stands at the LOWEST RIM (spill derived from the ground, never typed)
# layout A: the flanks only; layout B: A + the crest pools b01-b11 (the owner unfreezes the crest inside them)
- id: "pool.stp_bar_a01"
  c: [-78.0, 9.0]
  r: [3.2, 2.2]
  rot: 4
  depth: 0.35
  layout: "A and B"
- id: "pool.stp_bar_a02"
  c: [-96.5, -8.0]
  r: [2.8, 2.0]
  rot: -8
  depth: 0.4
  layout: "A and B"
- id: "pool.stp_bar_a03"
  c: [-105.0, 8.0]
  r: [3.6, 2.4]
  rot: 10
  depth: 0.45
  layout: "A and B"
- id: "pool.stp_bar_a04"
  c: [-130.0, -8.0]
  r: [2.6, 1.8]
  rot: -4
  depth: 0.3
  layout: "A and B"
- id: "pool.stp_bar_a05"
  c: [-150.5, 8.0]
  r: [3.2, 2.2]
  rot: 6
  depth: 0.4
  layout: "A and B"
- id: "pool.stp_bar_a06"
  c: [-173.0, -9.0]
  r: [2.8, 2.0]
  rot: -10
  depth: 0.35
  layout: "A and B"
- id: "pool.stp_bar_a07"
  c: [-192.0, 9.0]
  r: [3.6, 2.4]
  rot: 8
  depth: 0.45
  layout: "A and B"
- id: "pool.stp_bar_a08"
  c: [-211.0, -9.0]
  r: [2.6, 1.8]
  rot: -6
  depth: 0.3
  layout: "A and B"
- id: "pool.stp_bar_a09"
  c: [-264.0, 9.0]
  r: [3.2, 2.2]
  rot: 4
  depth: 0.4
  layout: "A and B"
- id: "pool.stp_bar_a10"
  c: [-278.0, -8.0]
  r: [2.8, 2.0]
  rot: -8
  depth: 0.35
  layout: "A and B"
- id: "pool.stp_bar_a11"
  c: [-301.0, 8.0]
  r: [3.6, 2.4]
  rot: 6
  depth: 0.45
  layout: "A and B"
- id: "pool.stp_bar_a12"
  c: [-321.0, -9.0]
  r: [2.6, 1.8]
  rot: -4
  depth: 0.3
  layout: "A and B"
- id: "pool.stp_bar_b01"
  c: [-78.0, 1.6]
  r: [4.0, 3.3]
  rot: 6
  depth: 0.35
  layout: "B"
- id: "pool.stp_bar_b02"
  c: [-97.0, -1.6]
  r: [3.8, 3.2]
  rot: -8
  depth: 0.65
  layout: "B"
- id: "pool.stp_bar_b03"
  c: [-117.0, 1.6]
  r: [4.4, 3.4]
  rot: 8
  depth: 0.4
  layout: "B"
- id: "pool.stp_bar_b04"
  c: [-136.0, -1.6]
  r: [3.6, 3.2]
  rot: -5
  depth: 0.3
  layout: "B"
- id: "pool.stp_bar_b05"
  c: [-155.0, 1.6]
  r: [4.2, 3.3]
  rot: 9
  depth: 0.7
  layout: "B"
- id: "pool.stp_bar_b06"
  c: [-172.0, -1.6]
  r: [3.9, 3.2]
  rot: -8
  depth: 0.4
  layout: "B"
- id: "pool.stp_bar_b07"
  c: [-190.0, 1.6]
  r: [4.1, 3.3]
  rot: 5
  depth: 0.35
  layout: "B"
- id: "pool.stp_bar_b08"
  c: [-205.0, -1.6]
  r: [3.6, 3.2]
  rot: -9
  depth: 0.6
  layout: "B"
- id: "pool.stp_bar_b09"
  c: [-278.0, 1.6]
  r: [3.6, 3.2]
  rot: 8
  depth: 0.45
  layout: "B"
- id: "pool.stp_bar_b10"
  c: [-296.0, -1.6]
  r: [4.2, 3.3]
  rot: -6
  depth: 0.75
  layout: "B"
- id: "pool.stp_bar_b11"
  c: [-314.0, 1.6]
  r: [3.8, 3.2]
  rot: 8
  depth: 0.3
  layout: "B"

# ponds (PondDef)
- id: "pond.stp_gap_pond"
  name: "Gap Pond"
  kind: "bog_pond"
  c: [151.0, -33.0]
  r: [4.6, 3.0]
  rot: 35
  surface: 5.3
  bed: 4.7
  basin: [10.0, 0.35]
  outlet: "stream.stp_gap_brook"
  why: "Behind the East Gap, where the plateau's water gathers before the ramp: a small peat pool."
- id: "pond.stp_heath_pond"
  name: "Heath Pond"
  kind: "bog_pond"
  c: [20.0, -53.0]
  r: [4.5, 3.0]
  rot: -15
  surface: 5.3
  bed: 4.7
  basin: [10.0, 0.35]
  outlet: "stream.stp_heath_brook"
  why: "On the cliff-top heath above the West Gap, 7 m clear of the fleet grounds' rect."
- id: "pond.stp_storm_pond"
  name: "Storm Pond"
  kind: "fen_pool"
  c: [-24.0, -10.0]
  r: [4.2, 2.8]
  rot: 8
  surface: 5.3
  bed: 4.75
  basin: [10.0, 0.35]
  outlet: "stream.stp_storm_brook"
  why: "South of the bar-head road, in the hollow the Storm Brook drains to the Storm Beach."

# streams (StreamDef): z = designed bed at each point
- id: "stream.stp_gap_brook"
  name: "Gap Brook"
  source: "pond.stp_gap_pond"
  pts: [[154.0, -36.0], [155.5, -39.0], [156.6, -41.2], [158.1, -43.5], [159.9, -46.2], [161.1, -48.1],
    [163.5, -50.5], [167.0, -53.5], [170.0, -55.5]]
  z: [5.18, 5.12, 5.05, 4.98, 4.85, 4.3, 3.1, 0.3, -0.7]
  w: [0.9, 0.9, 0.9, 1.0, 1.0, 1.1, 1.2, 1.4, 1.6]
  depth: 0.12
  bank: [0.35, 0.55]
  why: "Out of the Gap Pond, down the ramp on the throat's bisector between walls 022 and 023 (4.9 m from each at the throat: the walls' keep of 3 m + 2 m never touches its bed), across the pocket beach."
- id: "stream.stp_heath_brook"
  name: "Heath Brook"
  source: "pond.stp_heath_pond"
  pts: [[16.5, -55.0], [13.5, -57.5], [11.0, -60.0], [9.5, -63.0], [8.5, -66.0], [7.5, -69.0],
    [6.5, -72.5]]
  z: [5.55, 5.4, 4.7, 3.4, 1.9, 0.5, -0.7]
  w: [0.9, 0.9, 1.0, 1.1, 1.2, 1.4, 1.6]
  depth: 0.12
  bank: [0.35, 0.55]
  why: "Off the heath, down the West Gap ramp between walls 054 and 055, out over the pocket beach."
- id: "stream.stp_storm_brook"
  name: "Storm Brook"
  source: "pond.stp_storm_pond"
  pts: [[-27.5, -11.0], [-33.0, -12.0], [-40.0, -12.8], [-47.0, -13.8], [-53.0, -17.0], [-58.0, -19.5],
    [-63.5, -21.5]]
  z: [5.18, 5.0, 3.9, 2.5, 1.5, 0.2, -0.6]
  w: [0.9, 1.0, 1.0, 1.1, 1.2, 1.4, 1.6]
  depth: 0.12
  bank: [0.35, 0.55]
  why: "From the Storm Pond west, south of the bar-head road and 6 m clear of wall 078, through the berm's swale to the Storm Beach."

# tidal creeks and guts (TidalCreekDef): z = bed at [head, mouth], w = width at [head, mouth]
- id: "creek.stp_gap_creek"
  name: "Gap Creek"
  joins: "stream.stp_gap_brook"
  kind: "creek"
  pts: [[170.0, -55.5], [172.0, -58.0], [174.4, -60.4], [173.6, -63.8], [172.1, -67.4], [172.0, -70.6],
    [174.0, -73.1], [177.3, -75.2], [179.9, -77.5], [180.5, -80.5], [179.3, -84.0], [178.0, -87.6],
    [178.6, -90.6], [181.2, -92.9], [184.5, -94.9], [186.5, -97.4], [186.4, -100.7], [184.9, -104.3]]
  z: [-0.7, -2.5]
  w: [1.6, 3.0]
  why: "The Gap Brook out of the pocket beach, wandering across the flats east of the Whelps to the eelgrass."
- id: "creek.stp_heath_creek"
  name: "Heath Creek"
  joins: "stream.stp_heath_brook"
  kind: "creek"
  pts: [[6.5, -72.5], [7.0, -75.7], [8.0, -78.9], [5.6, -81.5], [2.6, -84.0], [0.9, -86.7],
    [1.4, -89.9], [3.3, -93.3], [4.5, -96.6], [3.6, -99.4], [0.8, -101.9], [-2.0, -104.5],
    [-3.0, -107.3], [-1.8, -110.6], [-0.5, -112.9]]
  z: [-0.7, -2.5]
  w: [1.6, 3.0]
  why: "The Heath Brook out of the West Gap, past the gap's skerries to the front."
- id: "creek.stp_storm_creek"
  name: "Storm Creek"
  joins: "stream.stp_storm_brook"
  kind: "creek"
  pts: [[-63.5, -21.5], [-65.5, -23.9], [-67.7, -26.0], [-71.4, -25.8], [-74.6, -26.4], [-76.5, -29.1],
    [-77.8, -32.5], [-80.1, -34.6], [-83.6, -34.7], [-87.1, -34.7], [-88.8, -35.8]]
  z: [-0.6, -2.5]
  w: [1.6, 2.6]
  why: "The Storm Brook out through the berm onto the bar's south flank, where the flats meet the Bar-Head Flats."
- id: "gut.stp_old_mans_gut"
  name: "The Old Man's Gut"
  joins: null
  kind: "gut"
  pts: [[61.0, -75.0], [61.4, -78.0], [62.3, -81.1], [61.7, -84.0], [60.5, -87.0], [59.2, -89.9],
    [58.6, -92.9], [58.9, -95.9], [59.9, -99.0], [60.9, -102.0], [61.2, -105.0], [60.6, -108.0],
    [59.4, -111.0], [58.1, -113.9], [57.5, -116.9], [57.8, -119.9], [58.8, -123.0], [59.8, -126.0]]
  z: [-2.4, -2.4]
  w: [3.2, 3.6]
  why: "The Weather Cliff's channel out to sea, 4.4 m west of the Old Man's foot: the flood's first way in."
- id: "gut.stp_sisters_gut"
  name: "The Sisters' Gut"
  joins: null
  kind: "gut"
  pts: [[-49.5, -27.5], [-52.5, -29.5], [-55.5, -31.5], [-58.5, -34.0], [-61.0, -37.0], [-63.0, -40.5],
    [-64.5, -44.5], [-65.5, -48.5], [-66.5, -52.5], [-68.0, -56.5], [-69.0, -60.5], [-69.5, -64.0]]
  z: [-2.4, -2.4]
  w: [3.0, 3.4]
  why: "The South-West Bluff's channel out to sea, north-west of the Sisters."

# paths (PathDef)
- id: "path.stp_cliff_walk"
  name: "Cliff walk"
  kind: "footpath"
  width: 1.0
  pts: [[178.0, -2.5], [183.0, -6.5], [181.7, -12.5], [178.8, -18.9], [174.8, -24.8], [170.1, -30.3],
    [164.7, -35.4], [160.4, -38.7], [156.5, -41.0], [152.8, -43.8], [146.1, -47.4], [139.2, -50.7],
    [132.1, -53.5], [124.8, -56.0], [117.4, -58.1], [109.9, -59.9], [102.3, -61.3], [94.6, -62.5],
    [86.9, -63.3], [79.1, -63.8], [71.4, -64.0], [63.6, -63.9], [55.9, -63.5], [48.1, -62.8],
    [40.4, -61.8], [32.8, -60.4], [25.3, -58.8], [17.8, -56.8], [12.5, -55.3], [7.7, -53.5],
    [0.6, -50.6], [-6.3, -47.3], [-12.9, -43.7], [-19.2, -39.6], [-25.1, -35.0], [-30.5, -29.9],
    [-35.2, -24.4], [-39.1, -18.4], [-41.5, -11.0], [-43.5, -4.0]]
  why: "The owner's paths carried round the island: 6 m inside the brows from the slip road to the bar-head road, over the Gap Brook and the Heath Brook on stepping stones."
- id: "path.stp_east_gap_path"
  name: "East Gap path"
  kind: "footpath"
  width: 0.9
  pts: [[158.5, -39.0], [160.6, -43.2], [162.4, -46.6], [165.5, -49.5]]
  why: "Down the East Gap ramp beside the brook (2.3 m north-east of it) to the pocket beach and the spit."
- id: "path.stp_west_gap_path"
  name: "West Gap path"
  kind: "footpath"
  width: 0.9
  pts: [[12.5, -55.3], [12.0, -59.5], [11.0, -63.0], [10.0, -66.0]]
  why: "Down the West Gap ramp to its pocket beach."

# shore rocks (ShoreRockDef): today's Shoreline rock keeps its place and takes a Rock Px form (#840 sheets)
- id: "rock.stp_strand_block"
  today: "Rock_bs"
  at: [189.82, 35.82]
  form: "block"
  stone: "sandstone"
  op: "dry"
- id: "rock.stp_strand_knuckle"
  today: "Rock_bm"
  at: [199.6, 21.98]
  form: "knuckle"
  stone: "sandstone"
  op: "wet"
- id: "rock.stp_landing_knuckle"
  today: "Rock_m"
  at: [217.52, 15.4]
  form: "knuckle"
  stone: "sandstone"
  op: "wet"
- id: "rock.stp_landing_skerry"
  today: "Rock_reef"
  at: [219.75, 16.6]
  form: "skerry"
  stone: "sandstone"
  op: "wet"
- id: "rock.stp_east_gap_skerry"
  today: "Rock_reef"
  at: [176.35, -55.98]
  form: "skerry"
  stone: "sandstone"
  op: "wet"
- id: "rock.stp_ledge_block"
  today: "Rock_bs"
  at: [114.98, -69.63]
  form: "block"
  stone: "sandstone"
  op: "wet"
- id: "rock.stp_field_erratic_1"
  today: "FieldRock_bs"
  at: [104.34, -56.54]
  form: "erratic"
  stone: "granite"
  op: "dry"
- id: "rock.stp_field_erratic_2"
  today: "FieldRock_bs"
  at: [122.01, -47.86]
  form: "erratic"
  stone: "granite"
  op: "dry"
- id: "rock.stp_field_erratic_3"
  today: "FieldRock_bs"
  at: [4.58, -45.33]
  form: "erratic"
  stone: "granite"
  op: "dry"

# rock scatters (RockScatterDef): a rule, deterministic from the plan's seed; forms = relative weights
- id: "scatter.stp_weather_apron"
  around: ["form.stp_old_man"]
  ring: [0.3, 3.0]
  every: [2.2, 3.4]
  forms: {cloven: 3, wedge: 3, block: 2, erratic: 1}
  stone: "sandstone"
  op: "wet"
  why: "The Weather Cliff's fallen blocks, heaped on the Old Man's plinth: the cliff's own toe stays in deep water (-3.19, the walls kept), where a boulder would sit 1 m under a spring low and in the boats' way."
- id: "scatter.stp_stack_aprons"
  around: ["form.stp_harbour_stack", "form.stp_the_sisters"]
  ring: [0.3, 2.6]
  every: [2.4, 3.6]
  forms: {cloven: 2, wedge: 2, block: 2, slab: 1}
  stone: "sandstone"
  op: "wet"
  why: "Blocks fallen from the Harbour Stack and the Sisters, on their plinths (-1.2 and -1.0)."
- id: "scatter.stp_ledge_boulders"
  walls: ["023", "039"]
  band: [1.0, 8.0]
  every: [5.0, 9.0]
  forms: {block: 3, knuckle: 2, slab: 2, cobbles: 2, scree: 1}
  stone: "sandstone"
  op: "wet"
  why: "Blocks on the East Ledges' bench and channel, off the reef crest."
- id: "scatter.stp_ledge_boulders_w"
  walls: ["055", "067"]
  band: [1.0, 8.0]
  every: [4.0, 7.0]
  forms: {block: 3, knuckle: 2, slab: 2, cobbles: 2, scree: 1}
  stone: "sandstone"
  op: "wet"
  why: "The same on the West Ledges, denser where the reef is broken."
- id: "scatter.stp_storm_cobbles"
  section: "coast.stp_storm_beach"
  elev: [0.5, 3.4]
  every: [3.0, 5.0]
  forms: {cobbles: 4, knuckle: 1, block: 1}
  stone: "granite"
  op: "dry"
  why: "The berm's big cobbles, granite from the glacial till (the 'erratic' stone)."
- id: "scatter.stp_spit_cobbles"
  section: ["coast.stp_east_gap", "coast.stp_west_gap"]
  elev: [-1.4, 1.6]
  every: [3.0, 5.0]
  forms: {cobbles: 3, knuckle: 1}
  stone: "sandstone"
  op: "wet"
  why: "The spits' shingle, big stones along their crests."

# biomes on the new shores (part 1's BiomeDefs; no new biome)
harbour_shore: "every new intertidal and berm metre between -3.0 and +4.0 (strand, gaps, storm beach, reefs, bar)"
blueberry_barren: "the cliff-top heath: +5.4 m and within 10 m of a wall (part 1's rule, round the whole south)"
roadside_meadow: "the rest of the open plateau"
salt_marsh: "none new: no marsh is planned on the exposed shores"
alder_swale: "none new"

# the node previews (not Defs: the harness's windows; 520 x 450 at 32 px/m across, 20.6 px/m down the plan)
# x0 = the west edge, y_top = the north edge (plan); tide in m; sky "intro" = 06:00 on day 1
- {key: "strand", section: "coast.stp_harbour_strand", x0: 186.0, y_top: 38.0, tide: 0.0, sky: "afternoon", title: "Harbour Strand"}
- {key: "landing", section: "coast.stp_the_landing", x0: 205.0, y_top: 18.0, tide: -0.854, sky: "intro", title: "The Landing at 06:00"}
- {key: "south_arm", section: "coast.stp_south_arm", x0: 180.0, y_top: -20.0, tide: 0.0, sky: "afternoon", title: "South Arm and the Harbour Stack"}
- {key: "east_gap", section: "coast.stp_east_gap", x0: 152.0, y_top: -27.0, tide: -1.2, sky: "afternoon", title: "East Gap: the throat and Gap Brook"}
- {key: "east_gap_spit", section: "coast.stp_east_gap", x0: 166.0, y_top: -42.0, tide: -1.2, sky: "afternoon", title: "East Gap: the spit and its pools"}
- {key: "east_ledges", section: "coast.stp_east_ledges", x0: 120.0, y_top: -57.0, tide: -1.2, sky: "afternoon", title: "East Ledges reef"}
- {key: "weather_cliff", section: "coast.stp_weather_cliff", x0: 64.25, y_top: -59.0, tide: -1.2, sky: "afternoon", title: "Weather Cliff and the Old Man"}
- {key: "west_gap", section: "coast.stp_west_gap", x0: -6.0, y_top: -60.0, tide: -1.2, sky: "afternoon", title: "West Gap and its spit"}
- {key: "west_ledges", section: "coast.stp_west_ledges", x0: -25.0, y_top: -40.0, tide: -1.2, sky: "afternoon", title: "West Ledges reef"}
- {key: "sw_bluff", section: "coast.stp_sw_bluff", x0: -58.0, y_top: -25.0, tide: 0.0, sky: "afternoon", title: "The Sisters"}
- {key: "storm_beach", section: "coast.stp_storm_beach", x0: -66.0, y_top: -6.0, tide: 0.0, sky: "afternoon", title: "Storm Beach"}
- {key: "heath_pond", section: "pond.stp_heath_pond", x0: 8.0, y_top: -44.0, tide: 0.0, sky: "afternoon", title: "Heath Pond and Brook"}
- {key: "crossing_low", section: "coast.stp_the_bar", x0: -170.0, y_top: 10.0, tide: -2.2, sky: "afternoon", title: "The crossing, spring low"}
- {key: "crossing_high", section: "coast.stp_the_bar", x0: -170.0, y_top: 10.0, tide: 2.2, sky: "afternoon", title: "The crossing, spring high"}
- {key: "landing_00", section: "coast.stp_the_landing", x0: 186.0, y_top: 22.0, tide: -0.854, sky: "intro", title: "The Landing at 06:00, tile 00"}
- {key: "landing_01", section: "coast.stp_the_landing", x0: 202.25, y_top: 22.0, tide: -0.854, sky: "intro", title: "The Landing at 06:00, tile 01"}
- {key: "landing_10", section: "coast.stp_the_landing", x0: 186.0, y_top: 0.1553, tide: -0.854, sky: "intro", title: "The Landing at 06:00, tile 10"}
- {key: "landing_11", section: "coast.stp_the_landing", x0: 202.25, y_top: 0.1553, tide: -0.854, sky: "intro", title: "The Landing at 06:00, tile 11"}
- {key: "flats_weather", section: "flats.stp_south_flats", x0: 58.0, y_top: -64.0, tide: -2.2, sky: "afternoon", title: "The Weather Flats at a spring low: the Old Man and his gut"}
- {key: "flats_weather_neap", section: "flats.stp_south_flats", x0: 58.0, y_top: -64.0, tide: -0.99, sky: "afternoon", title: "The Weather Flats at a neap low", seed_of: "flats_weather"}
- {key: "flats_east", section: "flats.stp_south_flats", x0: 106.0, y_top: -84.0, tide: -2.2, sky: "afternoon", title: "The East Flats at a spring low: a pool, a hollow and the drains"}
- {key: "flats_west", section: "flats.stp_south_flats", x0: -66.0, y_top: -36.0, tide: -2.2, sky: "afternoon", title: "The Sisters' Gut at a spring low"}
- {key: "flats_ribs", section: "flats.stp_south_flats", x0: 68.0, y_top: -92.0, tide: -2.2, sky: "afternoon", title: "The Weather Flats' rock ribs, a pool and the drains at a spring low"}
# the cliff options' shared window (the harness's): walls 041-043 of the Weather Cliff, with the Old Man
option_window: {x0: 64.25, y_top: -59.0, tide: -1.2, sky: "afternoon", walls: ["041", "043"]}
```

---

## Appendix B. The prototype (part 2)

**What it is.** It lives under `Evidence~/harness/`, and is not committed. It extends part 1's prototype (part 1's Appendix B), and imports part 1's modules unchanged.
- It is a Python and Node port of the deriver's part 2 steps (§4 to §8), used to make every number and picture in this document.
- It reads the committed maps, part 1's derived maps, and the two scenes' cliff walls, with a line scanner. It never reads a scene whole into memory.
- It drives the v6 kit and the px cliff-face kit only through their public APIs, from its own scripts. It edits no kit file.

**Determinism.**
- A fresh derivation of both layouts, made for this appendix, **equals the saved one on all 33 maps of each**.
- A fresh check run reproduces `checks2_B.json`, `checks2_A.json`, `stats2.json` and the plant field **byte for byte**.
- The maps' hashes use part 1's method: the sha256 of a map's bytes, with NaN written as −9999.

| Output | Layout B (recommended) | Layout A |
|---|---|---|
| `maps2.E` (the ground) | `34d306bf924342ea` | `68e6c1aa52e1e366` |
| `maps2.zone` | `5a5e3a1af7dfd13e` | `fff6b6bab7b0edf6` |
| `maps2.still` | `3271a39e13b4d6ae` | `912824068fcc1c34` |
| `maps2.biome` | `7cc860be455d89fa` | the same |
| `maps2.chan` | `d98bcbd4ce677419` | the same |
| `maps2.tidal` | `776586820ad25b36` | the same |
| `maps2.flw` (the flats' weight) | `baaac7aaab01ecae` | the same |
| `maps2.flrun` (the flats' drains) | `ce58d2eaaeed2bb7` | the same |
| `field2` (the plant field: 23,180 plants) | `5e78372a00472b7a` | — |
| `records_*.json` (file) | `05c5659a0b5b2748` | `9671ec82dfe5b0e9` |
| `checks2_*.json` (file) | `a2c35d95a5ea3f7a` | `39e4aee46435fdd9` |
| `stats2.json` (file) | `907e02863b778f7b` | — |

**How to run it** (from `Evidence~/harness/`, on Python 3 with numpy and Pillow, and Node 24):
- `python walls_parse.py StPeters` writes `p2/walls_StPeters.json`; the same with `NineMileCreek`.
- `python p2_run.py derive` writes `p2/maps2_{B,A}.npz` and `p2/records_{B,A}.json`, about 64 s per layout.
- `python p2_check.py` writes `p2/checks2_{B,A}.json`, `p2/stats2.json` and `p2/field2.npz`, in about 19 s.
- `python p2_scatter.py B`, then `python p2_preview.py`, then `node p2_window.js` make the previews.
- `python p2_pics.py` and `python p2_small.py` make the pictures.
- The `v6_*.js` scripts are §1's checks.

**Inputs:**

| Input | sha256 (first 16) |
|---|---|
| `Assets/_Project/Data/Terrain/StPetersSeabed_HeightTex.png` | `dd4780eeeb0b81a9` |
| `Assets/_Project/Data/Terrain/StPetersSplatA.png` | `218a09b02a0898c1` |
| `Assets/_Project/Data/Terrain/StPetersSplatB.png` | `738e85fe1c8cf79c` |
| `Assets/_Project/Data/Terrain/StPetersSplatC.png` | `3e0c5aab043c6cd3` |
| `Assets/_Project/Data/Terrain/StPetersSplatD.png` | `940829e1390a6012` |
| `Assets/_Project/Data/Terrain/StPetersSplatE.png` | `24e51ee3377a25cd` |
| `Assets/_Project/Scenes/StPeters.unity` (the walls, by line scanner) | `2183607fafea055b` |
| `Assets/_Project/Scenes/NineMileCreek.unity` (the walls, by line scanner) | `a3a125418a8043a7` |
| `docs/art/rigs/px-cliff-face-kit/bake/pixelLanguage.js` | `1efe4c249a2c82a1` |
| `docs/art/rigs/px-cliff-face-kit/bake/pxCliffFaceRig.js` | `15b0ae3b4cdeb847` |
| `Evidence~/scene/StPeters.json` (part 1's) | `f2409d83e2cea332` |
| `Evidence~/plan/features.json` (part 1's) | `13da0bc40bdc2b59` |
| `Evidence~/plan/kit_density.json` (part 1's) | `3d0c6301bd93ebbc` |
| `Evidence~/plan/species.json` (part 1's) | `4e1480e0b3da985f` |
| `Evidence~/plan9/maps.npz` (part 1's derived maps) | `8184428271e1bb3a` |
| `Evidence~/plan9/field.npz` (part 1's plant field) | `be38a0a0cbb85dbb` |
| `Evidence~/p2/walls_StPeters.json` | `a2017aff126d0f05` |
| `Evidence~/p2/walls_NineMileCreek.json` | `fcf38ab492e00190` |
| `Evidence~/drop/Export/pixel-terrain-pass-9/kit/plantIsoRig5.js` (part 1's drop) | `a80c485993a1a059` |
| The kit's zip, `Pixel art capabilitiescliffrockkitv6.zip` | `aa632e3cfc899eef` |
| `…/cliff-rock-kit-v6/pxCliff3.js` | `bb5ddba39c1f692a` |
| `…/cliff-rock-kit-v6/pxRockIso3.js` | `c5f124e495d460d6` |
| `…/cliff-rock-kit-v6/pxCliffScenes.js` | `60003b4a321928b2` |
| `…/cliff-rock-kit-v6/terrainLight6.js` | `f1fd93c0eeb85385` |
| `…/cliff-rock-kit-v6/support.js` | `8fe7df74405f3c55` |

| Harness file (`Evidence~/harness/`, not committed) | Lines | sha256 (first 16) |
|---|---:|---|
| `p2_check.py` | 468 | `408bf0f588999acf` |
| `p2_cliff_cost.py` | 73 | `d9a70aedc0502bad` |
| `p2_data.py` | 412 | `15750c116d111c47` |
| `p2_extra_costs.py` | 59 | `27782522d9304d85` |
| `p2_lib.py` | 840 | `88aca280aba7e74f` |
| `p2_pics.py` | 496 | `9b30575f70084b74` |
| `p2_preview.py` | 378 | `845caf4f8c74ecbf` |
| `p2_run.py` | 68 | `433755fbdef64cec` |
| `p2_scatter.py` | 13 | `5ffde51d79143bd6` |
| `p2_small.py` | 40 | `51c71adf78bcadc9` |
| `p2_summary.py` | 28 | `c30f2af519ec4d3a` |
| `p2_today.py` | 123 | `34118917bd6c2eda` |
| `p2_window.js` | 242 | `cae2fe619176679e` |
| `v6.js` | 95 | `8357742df20c7fa7` |
| `v6_explain.js` | 76 | `a6da52aa881af9d5` |
| `v6_measure.js` | 26 | `8ef80b10dc249c6b` |
| `v6_options.js` | 23 | `c1ad8d3f2a9a7bea` |
| `v6_options_sky.js` | 14 | `1bcc6ad46c49cc58` |
| `v6_options_sky2.js` | 19 | `f3529fba88869f4b` |
| `v6_options_time.js` | 17 | `a4e4c3c4923334aa` |
| `v6_rerender.js` | 57 | `b90ae7dfe847850c` |
| `v6_snowgust.js` | 21 | `b2f21ae73d543858` |
| `v6_sprites.js` | 24 | `bc6947df64209173` |
| `v6_tide.js` | 41 | `d9355e901e656968` |
| `v6_tl.js` | 46 | `7236d17763e51ac6` |
| `v6_tl_seadir.js` | 12 | `0438f706f9a4e6ac` |
| `v6_ulp.js` | 58 | `61589ffd76e3f8cd` |
| `v6_verify.js` | 27 | `4b0d07cdce608880` |
| `v6_wind.js` | 25 | `316d08319c24702b` |
| `walls_parse.py` | 48 | `7aac2237604d7be6` |

Part 1's modules, imported unchanged: `analytic.py`, `stp.py`, `plan9_data.py`, `plan9_lib.py`, `plan9_run.py`, `plan9_check.py`, `plan9_preview.py` and `hh9.js`. Their hashes equal those in part 1's Appendix B.

**Pictures:**

| Picture | Pixels | Size |
|---|---|---:|
| `biomes.png` | 784 × 548 | 92,287 B |
| `change.png` | 1248 × 576 | 122,359 B |
| `crossing-window.png` | 980 × 430 | 16,698 B |
| `crossing.png` | 1288 × 686 | 78,541 B |
| `flood.png` | 624 × 1411 | 188,431 B |
| `landing-dock-shift.png` | 516 × 664 | 22,230 B |
| `landing.png` | 516 × 681 | 26,714 B |
| `option-a.png` | 520 × 450 | 60,100 B |
| `option-b.png` | 520 × 450 | 50,352 B |
| `option-c.png` | 520 × 450 | 50,369 B |
| `paths.png` | 1248 × 590 | 103,588 B |
| `plan-east.png` | 960 × 892 | 45,037 B |
| `plan-island.png` | 1248 × 574 | 108,013 B |
| `plan-south.png` | 1890 × 922 | 118,644 B |
| `plan-west.png` | 1400 × 672 | 56,698 B |
| `preview-crossing-high.png` | 520 × 450 | 5,357 B |
| `preview-crossing-low.png` | 520 × 450 | 27,961 B |
| `preview-east-gap-spit.png` | 520 × 450 | 92,952 B |
| `preview-east-gap.png` | 520 × 450 | 98,750 B |
| `preview-east-ledges-c.png` | 520 × 450 | 85,028 B |
| `preview-east-ledges.png` | 520 × 450 | 84,937 B |
| `preview-flats-east.png` | 520 × 450 | 53,603 B |
| `preview-flats-ribs.png` | 520 × 450 | 64,093 B |
| `preview-flats-sisters-gut.png` | 520 × 450 | 56,171 B |
| `preview-flats-weather-neap.png` | 520 × 450 | 42,636 B |
| `preview-flats-weather.png` | 520 × 450 | 48,706 B |
| `preview-heath-pond.png` | 520 × 450 | 89,095 B |
| `preview-landing.png` | 1040 × 974 | 228,046 B |
| `preview-sisters.png` | 520 × 450 | 51,280 B |
| `preview-south-arm.png` | 520 × 450 | 50,818 B |
| `preview-storm-beach.png` | 520 × 450 | 76,421 B |
| `preview-strand.png` | 520 × 450 | 74,056 B |
| `preview-west-gap.png` | 520 × 450 | 92,243 B |
| `preview-west-ledges.png` | 520 × 450 | 81,168 B |
| `today-east.png` | 960 × 780 | 30,011 B |
| `today-south.png` | 1560 × 570 | 39,743 B |
| `today-west.png` | 1400 × 560 | 31,086 B |
| **All 37** | | **2,644,222 B** |
