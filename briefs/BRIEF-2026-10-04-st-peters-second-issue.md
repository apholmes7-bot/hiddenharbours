# Brief 2026-10-04: St Peters, the package's second issue (your file on the game's ground; the still water, the pieces and the Alder Fall)

From the Art desk, for the owner. First read [STATE.md](https://github.com/apholmes7-bot/hiddenharbours/blob/design-desk/STATE.md) on this branch. This brief carries on from [the package brief](https://github.com/apholmes7-bot/hiddenharbours/blob/design-desk/briefs/BRIEF-2026-10-01-st-peters-one-scene-package.md), which still stands wherever this one is silent.

Your one-scene package of 10-01 is now the game's St Peters. On 10-04 the game's terrain lane merged your island ground file with three small asks of its own (main 883f27b5). The owner has played it there and calls it good progress. This brief reads main at b9e31bcf, which holds the same ground and the same still water.

This issue does five things, in one package:
- it puts the game's three asks into your file, so your file is the game's ground (section 1);
- it brings back the still water the ground lost or never named: the fen, the Head's 14 rock pools, the ledges' ten, the dipping pool and two brooks (section 2);
- it names the bay's sand bands in the beach's file (section 3);
- it names the pieces the package left loose or misnamed, and takes the game's ids for the 70 it has renamed (section 4);
- it renders the Alder Fall's nine masters on the one ground (section 5).

Section 6 extends your checker, section 7 is the package and section 8 says what happens when it arrives. Section 9 is what the owner sees against the boards, with one question on how your boards are lit.

The owner sends `HH-st-peters-game-ground-2026-10-04.zip` with this brief. Its README gives every encoding.

## 1. Your file on the game's ground

The game took `ground.stp_island` whole and laid it out by its own rule. Its height map is in the zip, `ground/StPetersSeabed_HeightTex.png`. Away from three small boxes it is your file within 2.5 mm.

Those three are the game's own asks. They are in `ground/game-asks.json`, in your island-ground format: each is a patch of the game's own pixels, and each patch's border ring is your file's ground exactly. `pictures/where-the-game-differs.png` circles them, and a close-up of each is beside it.

| # | Id | Kind | Its rule, in the game's words | Pixels moved over 5 mm | Most |
|---|---|---|---|---:|---:|
| 1 | `ground.stp_main_beach_west_blend` | fill | the beach's toe held at the flats' level across its west end, raised only | 68 (26.45 m²) | +0.615 m |
| 2 | `ground.stp_bluff_channel_hold` | cut | the channel's bed along wall 068's toe held below the spring low, lowered only | 5 (1.94 m²) | −1.147 m |
| 3 | `ground.stp_ne_neck_hollow_fill` | fill | the closed hollow by the Head's neck filled to its spill (+3.755), raised only, outside the cannery's circle | 12 (4.67 m²) | +0.302 m |

The third fills a hollow nobody meant. The Head's neck rises out of the cannery's kept ground over the 6 units past the cannery's reach, from 21.06 units off (170, 16) (`stpOneShape.js` line 36). The hollow lies at 21.22, where that rise begins. There it dams pass 9's slope and closes a bowl of 21 pixels (8.2 m²), 0.302 m deep.

- **Append the three** to your file's asks after `ground.stp_main_beach`, in this order. The Art desk has laid that out: it is the game's map within 0.12 mm.
- **Build every ask of this issue on that ground.** Where a later ask covers one of the three, keep its rule there.
- **Restate `base.fileSha256`.** It names e3ad6b8b, the 561,064-byte file of the 09-29 send. The package ships, and the game took, its re-encoding: `23131b615bb3d2983017ef65b21947af2d328402715a2ccee9f947efcb697f30`, 566,834 bytes, by your own `SHA256SUMS-part1.txt`. The pixels are the same (8c077ea1).

## 2. Still water

The game draws the water that stands above the tide from a still-water map ([ADR 0046](https://github.com/apholmes7-bot/hiddenharbours/blob/b9e31bcf8c74472527dc2ca9f91155608a77f5f2/docs/adr/0046-still-water-above-the-tide.md)). It derives that map from your `stillWater` and its own plan, and St Peters carries it at main b9e31bcf. So the water your file lists stands where you list it. Water it does not list stands only where your hollows rule keeps it.

### 2.1 The fen, back as still water

The owner ruled on 10-02 that the Fen Pool comes back (STATE.md section 4). Its id stays `pond.stp_fen_pool`, as pass 9 named it.
- **Drop `ground.stp_fen_pool_dry`'s fill.** It raises 326 pixels over 5 mm (126.8 m²), by up to +0.547 m.
- **Re-cut the bowl's north side too.** `ground.stp_alder_fall_cut` comes after the fill in your file, and its box overlaps the bowl. Across x 102.25 to 113.25, y 52.75 to 54.75, its patch carries the fill: with the fill dropped, those 90 pixels (35.0 m²) still stand over pass 9 by up to +0.451 m. Re-seat the cut there on the ground without the fill, so the bowl is pass 9's all round.
- **List `pond.stp_fen_pool`** as the other four ponds are listed (surface, bed, centre, radii, rotation, outline), without `dry`, and with the 10-02 ruling in its `ruled`. Set its surface where the Alder Run takes its water: a step of 0, as theirs.
- **Its surface needs its outlet.**
  - Pass 9's doc ([section 4.2](https://github.com/apholmes7-bot/hiddenharbours/blob/b9e31bcf8c74472527dc2ca9f91155608a77f5f2/docs/design/st-peters-terrain-pass-9.md)) gives +5.25 over a +4.70 bed: 0.55 m deep, 138.25 m², with its outlet the run, at (106.93, 55.50).
  - Pass 9's texture is lower there. Flooded from the sea at a spring low, the bowl closes at +5.013, over 77.0 m², and its water leaves at (106.75, 55.25), the run's head. Your run's levels start at +5.29, at (107, 55).
  - So +5.25 stands only with a sill at the run's head. Otherwise the pond is listed lower. The ruled boards and the masters draw the fen full, so keep it full if the ground lets you.
  - Make the run's first level and the pond's surface one level, and say which you chose.

### 2.2 The Head's 14 rock pools

The game lays the Head's wave-cut platform, `ground.stp_ne_platform`, with no pools of its own, since your Head holds none. On the game's ground, seven of the fourteen sites below have no basin within their radius. The rest are under 2.4 m² and 0.12 m deep.
- **Cut the fourteen into `ground.stp_ne_head`** by the rule your part 2's pools follow, as the game's platform Def carries it:
  - 0.2 to 0.3 m deep below the spill;
  - an outline 1.25 × 0.85 of a radius from 0.8 to 1.5 m, wobbling by 0.14;
  - the floor zone `irishmoss`.
- **Make each a bowl on the one ground**, and list each in `stillWater.pools` with its id, centre, radii, rotation, spill, bed and floor zone.
- **Keep the ids `pool.stp_ne_platform_01` to `_14`.** Say by id if one cannot hold water.

The game's terrain lane derived the fourteen on an earlier ground. On the Head's ground their spills will differ:

| Pool | Centre | Radii (m) | Rot | Depth | Spill | Bed | m² |
|---|---|---|---:|---:|---:|---:|---:|
| 01 | (191.64, 40.43) | 1.71 × 1.16 | 38 | 0.25 | −0.743 | −0.993 | 3.2 |
| 02 | (196.19, 43.84) | 1.86 × 1.26 | −31 | 0.28 | −0.519 | −0.799 | 3.5 |
| 03 | (200.36, 42.65) | 1.25 × 0.85 | 38 | 0.28 | −1.192 | −1.472 | 1.8 |
| 04 | (205.57, 45.66) | 1.01 × 0.69 | 57 | 0.22 | −0.645 | −0.865 | 1.2 |
| 05 | (210.47, 48.89) | 1.41 × 0.96 | 35 | 0.22 | −0.209 | −0.429 | 2.5 |
| 06 | (217.77, 53.56) | 1.16 × 0.79 | −4 | 0.29 | −0.391 | −0.681 | 1.5 |
| 07 | (221.96, 62.91) | 1.70 × 1.16 | −56 | 0.29 | +0.507 | +0.217 | 0 |
| 08 | (218.36, 65.68) | 1.10 × 0.75 | 2 | 0.23 | −0.178 | −0.408 | 0 |
| 09 | (209.05, 68.53) | 1.38 × 0.94 | 42 | 0.24 | −0.272 | −0.512 | 2.0 |
| 10 | (201.11, 69.13) | 1.27 × 0.87 | 8 | 0.29 | +0.084 | −0.206 | 2.8 |
| 11 | (194.78, 69.09) | 1.77 × 1.20 | 11 | 0.28 | −0.785 | −1.065 | 3.0 |
| 12 | (189.98, 69.28) | 1.58 × 1.07 | 55 | 0.25 | −1.388 | −1.638 | 2.8 |
| 13 | (184.15, 66.58) | 1.81 × 1.23 | 45 | 0.27 | −1.267 | −1.537 | 3.8 |
| 14 | (177.62, 62.43) | 1.12 × 0.76 | −24 | 0.26 | −1.247 | −1.507 | 1.2 |

07 and 08 held no water there.

### 2.3 The ledges' ten rock pools

Your [part 2](https://github.com/apholmes7-bot/hiddenharbours/blob/b9e31bcf8c74472527dc2ca9f91155608a77f5f2/docs/design/st-peters-terrain-pass-9-part-2.md), section 7.5, puts ten pools in the ledges' benches: `pool.stp_east_ledges_01` to `_05` and `pool.stp_west_ledges_01` to `_05`. All ten, it says, hold water at every low.

On the one ground each of the ten still has a basin at or beside its centre, spilling above the spring low. But your file names none of them, so the game keeps their water only by your hollows rule: 0.1 m deep and 4 m² or more. Four of the ten basins are smaller than that:

| Pool | Part 2's m² | Its basin on the one ground (m²) | Spill | Deepest (m) | Kept by the hollows rule |
|---|---:|---:|---:|---:|---|
| `pool.stp_east_ledges_01` | 5.8 | 19.06 | −0.752 | 0.41 | yes |
| `pool.stp_east_ledges_02` | 4.0 | 4.28 | −1.304 | 0.34 | yes |
| `pool.stp_east_ledges_03` | 2.8 | 4.28 | −0.550 | 0.30 | yes |
| `pool.stp_east_ledges_04` | 2.0 | 3.11 | −0.670 | 0.37 | **no** |
| `pool.stp_east_ledges_05` | 2.5 | 3.89 | −1.311 | 0.34 | **no** |
| `pool.stp_west_ledges_01` | 4.2 | 1.17 | −0.832 | 0.32 | **no** |
| `pool.stp_west_ledges_02` | 3.8 | 6.61 | −0.896 | 0.42 | yes |
| `pool.stp_west_ledges_03` | 4.0 | 5.45 | −1.124 | 0.50 | yes |
| `pool.stp_west_ledges_04` | 3.0 | 10.11 | −0.603 | 0.43 | yes |
| `pool.stp_west_ledges_05` | 3.0 | 3.50 | −0.521 | 0.24 | **no** |

- **List the ten in `stillWater.pools`** by these ids, as the Head's fourteen, so the game keeps all ten whatever their size. Say by id if one cannot hold water.
- **Your `hollows` list has 64.** On its map the game finds 77 by the same rule, 5 of them a pond's or pool's own, and keeps 72. It floods by each pixel's four sides. A flood that also steps corner to corner finds 69 on your file, or 64 without the ponds' and pools' own: your count. Flood by the four sides, as the game does, and list the hollows as found on the one ground.

### 2.4 The dipping pool at its spill

- `pool.stp_gap_dipping_pool` spills at +4.960, at (157.75, −42.75): 6 pixels, 2.33 m², 0.219 m deep at most. The game already holds it there.
- Your 5.144 is the brook's surface at the pool, not the pool's own: the Gap Brook's designed bed there (5.024, between its 5.05 and 4.98) plus the brook's 0.12 m of water.
- Set the pool's `surface` to +4.960 and give its spill point. The floor (+4.70) and the pads (+5.28) stand.

### 2.5 The Alder Run's two levels in the fall's water

Your run's note says that from y 55 to 68.8 it is the Alder Fall's own water. Two of its levels lie there and read as the run's:
- 1.359 at (106, 62), inside the plunge pool, whose surface is +2.05;
- 1.865 at (105.5, 65.5), past the pool's spill at (105.8, 64.44), so the run's level rises downstream there.

Drop the two, or mark them as the fall's. The game skips them by name today.

### 2.6 The Heath Brook to the shore

Your file draws the brook twice: `stream.stp_heath_brook`'s line (13 points) and `ground.stp_main_beach`'s `brook.bed` (27 stations). Every station lies on the line, but the line strays from the stations' course by up to 0.25 units, at (12.90, −76.20). And at two stations the ground stands more than 1 cm over the bed:
- by 0.012 m at (14.14, −62.34);
- by 0.065 m at (9.64, −86.85), where the bed is −0.837. That is seaward of where the bed crosses 0 m, near (11.76, −78.15).

The asks:
- make the stations the line's own points, so the two cannot disagree;
- end the brook at the shore near (11.76, −78.15), so the sea owns its mouth. The game's brook already ends there.

## 3. The bay's sand bands, in the beach's file

Today only your tool knows the bands. `tools/stpOnePaint.js` (lines 28 and 83) paints the bay by height. Inside the bay, where `StpOneBeach.surface` weighs over 0.45, it takes the ground's height plus ±0.06 m of noise:
- below −1.75: eelgrass;
- −1.75 to −0.95: ripple;
- −0.95 to +3.45: sand;
- above +3.45: marram;
- grass at the back, where the distance across the shore passes D − 0.4 and the ground is over +5.6.

No scene's `ground.paint` holds an area: in all ten scenes it holds paths only. For now the game lays the bands from that rule itself, as `coast_recipe.stp_main_beach`.

Your `BOX` in `tools/stpOneBeach.js` (line 21) ends the bay's surface (line 61). The game found that at x −8 the surface still weighs in full, so its paint ran a straight line down the beach. It now fades the bay out over 5 m inside the box's west, east and south edges, along a line that wanders 4 m (its [Bay_MainBeach](https://github.com/apholmes7-bot/hiddenharbours/blob/b9e31bcf8c74472527dc2ca9f91155608a77f5f2/Assets/_Project/Data/Terrain/StPetersPlan/Bay_MainBeach.asset)).

The ask: write the bands into the main beach scene's file, each with an id, its floor zone and its heights, with the noise and the bay's mask, and say how the mask ends at the box. Then the file says what your tool paints.

## 4. The pieces

### 4.1 Every mount's host, by id

Your files name 43 mounts. The README's "22 mounts named" (line 23) is a slip, and NOTES' table has 23 rows. By scene they are:
- the Alder Fall 2, the cannery 3, the crossing 14, the gap bridge 1, Ginny's plot 3;
- the Landing 8, the main beach 1, the north-east light 7, the village 0, the lookout 4.

Restate the README, and give all 43 in NOTES. Name these hosts right. Until your files name every host by id, the game reads these eight from its own [hosts' table](https://github.com/apholmes7-bot/hiddenharbours/blob/b9e31bcf8c74472527dc2ca9f91155608a77f5f2/docs/design/st-peters-key-scenes/key-scene-hosts.json):
- **The cannery's two gulls stand on `structure.stp_cannery`.** "building.stp_cannery" in their mount words is a slip. The cannery's rig has no anchor for them, so they keep their own numbers:
  - `prop.stp_cannery_gull_a` at (174.38, 11.62), +15.30, on the ridge;
  - `prop.stp_cannery_gull_b` at (167.17, 20.53), +14.60, on the boiler house's ridge.
- **The bell gull stands on `structure.stp_ne_fog_bell`.** That is the scene's own piece at (215.4, 61.8), +11. The gull, `prop.stp_ne_gull_bell`, stands at +14.12 on its frame. Its mount and NOTES line 131 call the bell a "prop".
- **The stack's two gulls stand on `form.stp_lantern_stack`.** `prop.stp_ne_gull_stack_a` and `_b` stand at +7.65 on its +7.6 top. Their mount words name `form.stp_harbour_stack`, the Whelps' stack.
- **Ginny's two perches.**
  - `gull.stp_gp_mud` (+1.02) sits on `rock.stp_gp_erratic_02` (your `rock.erratic@110.2,77.9`, section 4.3), which stands at its point.
  - `gull.stp_gp_reef` (+0.11) has no piece within 3 m, and it stands 0.12 m over the reef ledge's own ground. So its mount is null, as the north-east bar's three gulls have theirs.
- **The Landing's gull stands on the pier's north pilehead.** `prop.stp_landing_gull` is at (213.5, 2.5), +5.72, on the game's own pier. "Bollard" is your word for the pilehead.

### 4.2 The east end's 21 pieces and the spur

Pass 2's `eastEndPieces.json` names the east end's three buildings. Each is its take "owner" (ruled 09-28, call 10), on the game's wharfBuilding2 and wharfDecor kits:

| Id | Preset | At | Faces | Weather |
|---|---|---|---|---:|
| `structure.stp_net_loft` | redShed | (183.2, 22.8) | E | 0.55 |
| `structure.stp_net_shed` | netShed | (187.7, 9.3) | SE | 0.70 |
| `structure.stp_ice_house` | iceHouse | (158.6, 7.6) | S | 0.60 |

With the buildings come:
- 14 dressing pieces: `prop.stp_loft_traps`, `_traps_b`, `_buoys`, `_nets`, `_wood`, `_oilskins` and `_barrow`; `prop.stp_flake_a` and `_b`; `prop.stp_shed_reel`, `_totes` and `_baskets`; `prop.stp_ice_wood` and `_barrow`;
- the bait store's 4: `prop.stp_bait_ice`, `_salt`, `_bike` and `_flag`;
- `path.stp_loft_spur`, 0.9 m wide.

The package carries none of them: of the east end, it has only the bait store and its walk.
- **Name all 21 and the spur** in the package's files, seated on the one ground.
- **Seat the six round the loft.** 15 of the 21 stand within 4 cm at their points. The six round the loft do not, because the ground there has moved since pass 2 drew it. Pass 2's z less the ground:
  - the loft −0.651, the nets −0.580, the wood −0.354;
  - the traps +0.321, traps_b +0.411, the barrow +0.760.

### 4.3 The ids that held positions

106 of your ids held positions (`kind.name@x,y`): the crossing's 34 sandpipers, 36 other pieces in the key scenes and 36 in the village. Each matched where its piece stands, within 5 mm. But an id that holds a position goes false the first time its piece moves, and the game never changes an id once it has one.

The game has now taken the key scenes' data in, unplaced, and renamed their 70 by its [id map](https://github.com/apholmes7-bot/hiddenharbours/blob/b9e31bcf8c74472527dc2ca9f91155608a77f5f2/docs/design/st-peters-key-scenes/key-scene-id-map.json):
- the 34 sandpipers, `prop.stp_cx_sandpiper_01` to `_34`, in the crossing scene file's order, as the withies run from `prop.stp_cx_withy_01`;
- the Alder Fall's 19 plants, as `plant.stp_af_speckled_alder_01`;
- Ginny's plot's 7 plants, 7 trees and 3 stones, as `tree.stp_gp_red_maple_01` and `rock.stp_gp_erratic_02`.

- **Take the game's ids,** exactly as the map gives them, in the scene files and in `ruled-ids.json`, and put the map in NOTES.
- **Leave the village's 36** (34 `prop.stp_vl_*` and 2 `tree.stp_vl_square_maple`) as they are for now. Whichever side takes them first renames them by the same rule.

### 4.4 Restated figures

- **The main beach's area.** `ground.stp_main_beach` says 4,027 m².
  - The Art desk counts 4,190 m² by pixels. By samples it counts 4,205, 4,185 and 4,166 m², moving more than 1, 1.5 and 2 cm.
  - The game's terrain lane counted 4,161.

  It is a summary figure, not a bar, so restate it with how you count.
- **The neck's fade.** `ground.stp_cannery_hold` says the neck "fades out over the 3 beyond it". The fade is 6 units (`stpOneShape.js`, lines 27 and 36).
- **`base.fileSha256`** (section 1) and **the hollows** (section 2.3).

### 4.5 The lookout's two stones

Your full return for the lookout (09-30) redrew two stones by the Gap Brook, at the west edge of its low and dusk boards (the boards' pixels x 6 to 105, inside the `wide_bench` box). They are squarer, and the lower one is recoloured from brown to grey. Your notes do not list them. Say whether they were meant, and if they were, name them in the lookout's file. Nothing waits on it.

## 5. The Alder Fall's nine masters, on the one ground

On 10-02 the Art desk rendered the nine with your own bake, `pass6/afBake.js`, run as `pass6/afBakePage.html` runs it. The nine are noon, golden, dusk, spring, autumn, lowflow, highflow, low and high. Each has:
- a 960 × 640 frame, `boards/fall_<id>.png`;
- an 8-frame loop, `fall_<id>_anim.png`, 224 × 1408, cut from the box [380, 172, 224, 176].

Their ground is pass 9, the bake's default. Inside their frame the one ground differs from pass 9 by two asks only: the fen's fill and `ground.stp_alder_fall_cut`, the shingle bar that holds the plunge pool at +2.05. So with the fen back, only the fall's foot changes.
- **Render the nine on the one ground**, with the fen back (2.1), under the same names, so the game swaps them in by name.
- **Say what changed on the north shore.** Against your ruled view of 09-28, the pass 9 masters match everywhere but the north shore, which has:
  - more sea;
  - a deeper patch at the top;
  - a plank across the brink;
  - a tall birch to the north-east.

  The zip's `pictures/compare-alder-fall.jpg` sets the three side by side: your ruled view, your bake on pass 9 and the package's parity board. Say in NOTES which of the four differences come from the ground and which from the bake.

## 6. The checker, extended

Besides the package brief's section 5, it checks on real Node that:
- the ground file, laid out by its rule, is the zip's map within 1 mm outside the boxes of the asks this issue adds or changes;
- every pond and pool in `stillWater` holds its water: its surface is its spill within 1 cm, or its outlet's level, and its bed lies under it;
- the Head's fourteen pools and the ledges' ten are listed, by id;
- each brook's stations are its line's own points, and the ground stands no more than 1 cm over any station's bed;
- every mount names a piece in the package, the pier's pilehead, or null, and the README's count is the files' count;
- the east end's 21 pieces stand within 4 cm of the one ground at their points;
- no id in the key scenes names a position, and each of the game's 70 ids stands as its map gives it.

It prints each failure by id, and it passes on the package.

## 7. The package

One zip, `HH-st-peters-one-scene-package-<date>.zip`, of 500 entries or fewer, with its SHA256SUMS, as before.
- **It is the package whole,** with this issue in it, so the game takes one package and not a patch.
- **Every data file comes whole.** A board that has not changed need not come again.

Besides the 10-01 list, it holds:
- the nine Alder Fall masters (section 5);
- in the NOTES, each section of this brief by its number, with what changed and why, the id map (the game's 70), and any pool that cannot hold water, by id.

## 8. After it arrives

The Art desk checks it on real Node, against the game's map and this brief. Then the game takes it in:
- the ground and the still water through its terrain work;
- the pieces through its placement;
- the masters by name.

## 9. What the owner sees against the boards

The owner played the game's St Peters on 10-04 (main 883f27b5). What still differs from your boards is mainly the light and the shading.

That is the game's to close, and nothing in this issue changes for it:
- the game's ground already relights by a line-for-line port of your `terrainLight6.js` ([TerrainLight6.cs](https://github.com/apholmes7-bot/hiddenharbours/blob/b9e31bcf8c74472527dc2ca9f91155608a77f5f2/Assets/_Project/Code/Art/TerrainLight6.cs));
- its sea and the shore's wetness are the game's own water plane and wet band, not TerrainLight6's tide;
- its cliffs, its pieces and its hours are lit by the game's own shaders and its day-night grade.

The game's look pass will set its camera, clock and tide to each parity board's (`tools/boards.json` gives each one's frame, `t` and `tide`), and close the gap layer by layer against your boards.

One question only you can answer, in NOTES: does StpPano do anything to a board after the light that the libs in your package do not carry, such as a grade, a palette quantise, an outline, a haze or a dither? If it does, name each step and the file it lives in.

As of 2026-10-07 13:35Z.
