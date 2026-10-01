# Brief 2026-10-01: St Peters as one scene, the package (each key scene's detail back, every seam kept)

From the Art desk, for the owner. First read [STATE.md](https://github.com/apholmes7-bot/hiddenharbours/blob/design-desk/STATE.md) on this branch. This brief carries on from [the one-scene brief](https://github.com/apholmes7-bot/hiddenharbours/blob/design-desk/briefs/BRIEF-2026-09-30-st-peters-one-scene.md), which still stands wherever this one is silent.

Your one-scene return of 10-01 has reached the Art desk: the island's ground file, twenty quick-look boards, your tools and your working files. The island is whole, its ground is sound, and every piece is where it was. The owner, in their words: "some scenes are missing details or look off from the original key scenes, but its merged together now i guess. how should we proceeed to add the missing details from the orignal key scenes boards but maintain our seams between scenes and make it work in game?"

The answer: **the one scene is now the frame, and each key scene's last ruled board is its detail.**
- Keep the one scene's ground, water, seams and positions as they are.
- Bring each scene's detail back into it as data the game can take (section 4).
- Show it on a parity board: the one scene drawn through the frame, clock and tide of the scene's last ruled board, set beside that board (section 3).
- Then send the package (section 6).

## 1. What the Art desk found in the return

- **It is the quick look, not yet the package.** The package's scene.json files, island file, full boards, NOTES, checker and SHA256SUMS are still to come. Carry on to them.
- **The ground is sound.**
  - Pass 9 under it is the desk's texture: the file's sha256 starts `e3ad6b8b` and its pixels' `8c077ea1`, as your ground file says.
  - Every ask's lowest and highest points, raise and cut are as the file states.
  - Every ask's border meets the ground under it within 0.3 mm, but one. `ground.stp_alder_fall_cut` steps by up to 2.7 cm at its south-east corner, round (113.7, 52.6), where it meets `ground.stp_fen_pool_dry`. Close it to 1 cm or less.
- **Every piece is there.** The eight key scenes' last ruled scene.json files hold 300 pieces. 285 stand in the one scene at their own positions, and none has moved. The other 15 are variants that a mean-tide day does not show, so these boards rightly leave them out: the restored cannery's 7, the crossing's 6 gulls afloat at a high, the light's integral station (not taken) and the lookout's dusk dory. Keep all 15 in the package (section 4.7).
- **50 pieces stand at the wrong height.** 31 that stand on the ground kept their old heights where the ground under them moved (section 4.1). 19 that stand on something else do not name it (section 4.2).
- **18 village and beach pieces have no art yet** (section 4.3).
- **The ground carries the owner's own asks of 10-01,** as your tools say: the Head's neck, and the West and East Ledges broken into gullies and buttresses. They stay.
- **The beach:** West Gap Beach, about 65 m of sand from the West Gap to the Weather Cliff, with its dunes, the heath's path and steps down to it, and people on it. It opens cliff walls 045 to 054. The desk's lean is that it stands as drawn; the owner's paste says.
- **What looks off is mostly the one renderer.** Set beside the last ruled boards (the desk's comparison sheets come with the owner's paste):
  - the lookout's carved cliff faces are one flat face;
  - the Gap Brook and the Gap Pond, and the beach's Heath Brook, are a pale tint, not water;
  - the crossing's head is pale, dry and bare. The ruled board's wet, rippled sand, its wrack and finds, its cobbles, and the heath's edge with its path and sign do not show, and its hollows read as deep blue pits;
  - the Landing's wharf has lost its armour-stone edge;
  - the beach's cliff ends are stepped blocks;
  - Ginny's plot reads differently, through a frame that is not its ruled board's.

  The cannery and the north-east light read as their boards do.

## 2. The rule that keeps the seams

The seams are held by the data, not by the look. Three things stay as this return has them:
- **One ground,** the island's ground file. Change it only through an ask: its id, its heights in ground metres, and its border within 1 cm of the ground under it. A change that is only drawn never reaches the game.
- **One water,** drawn once from the ground and the tide, with the still water above the tide as data (section 4.4).
- **One set of positions:** every piece at its own number (section 5 of the one-scene brief), and every id in one scene only.

The detail comes back on top of these, without moving them:
- **Pieces, paint, planting and still water,** as data, each inside its scene's frame.
- **The look** (the light, the colour, the wet sand, the faces) from one renderer for the whole island. Bring that renderer up to the ruled boards, rather than giving each scene a look of its own. Where a scene's own dressing (its paint and scatter) meets the island's, thin it out over a few metres at the frame's edge, so no line shows.
- **The seam close boards,** drawn again from the package. Each matches this return's, except where a listed ask changed the ground.

## 3. Each scene's detail back: one parity board each

For each key scene, draw one parity board: the one scene through the frame, clock, tide and season of the scene's last ruled board, set beside that board. List every difference left, with its reason: pass 9 moved the ground, a ruling, or a piece without art. A difference without a reason is one to fix.

| Scene | Its last ruled board |
|---|---|
| the Landing | `landing.wide_approach.1431` |
| the cannery | `cannery.wide_landing.0600` |
| the north-east light | `ne_light.wide_station_0600` |
| the crossing | `crossing.head_open` |
| Ginny's plot | `ginnys_plot.plot_noon` |
| the Alder Fall | `alder_fall.noon` (the Art desk renders the ruled board to set beside yours) |
| the gap bridge | `gap_bridge.bridge_noon` |
| the lookout | `whelps_lookout.lookout_noon` |

The village, the beach, the ledges and the Head's neck are new in the one scene, with no ruled board. Draw one board of each at the key scenes' detail: the village from its plan, and the beach on a High Summer afternoon at mean tide.

What each scene needs back, from the desk's comparison sheets:
- **the lookout:** its carved faces, with the cliff kit v6 (`PxCliff3`), as its ruled board drew them (section 4.6);
- **the gap bridge:** the Gap Brook and the Gap Pond as water (section 4.4);
- **the crossing:** the wet, rippled sand the tide leaves, the wrack lines and finds along its tide marks (your crossing scatter has 29 wrack plants, none of them at the head, so add them there as scatter), the cobbles, the heath's edge with its path and sign, and the hollows as the shallow water they are (about 0.1 m deep);
- **the Landing:** the wharf's armour-stone edge, as its context draws it (`WharfRig2`'s pier, frozen);
- **Ginny's plot:** her planting and her dressing as `plot_noon` has them;
- **the Alder Fall:** its pieces on the ground pass 9 left it (section 4.1), with the pool at +2.05 as drawn;
- **the beach:** its cliff ends as finished walls, not steps (section 4.6).

## 4. What the game takes, as data

The game draws St Peters from data:
- its ground from the ground file;
- its cliffs from the walls, by number;
- its pieces from the scene.json files;
- its still water from the terrain plan.

So every detail that matters in the game comes back as data. A detail that is only paint on a board stays on the board.

### 4.1 Re-seat the 31 pieces the ground moved under

Each stood on its scene's ground, and the one ground under it has moved. Stand each on the ground under it, within 5 cm. Where its reason no longer holds there, move it to the nearest spot where it does, and list the move with both positions. Each number below is the piece's z less the ground under it now, in metres.
- **The Alder Fall (20).** Pass 9 lowered the shore round the fall, and the cut now blends into it.
  - Plants: `plant.BlueFlag@106.05,57.3` +0.23, `plant.BlueFlag@107.55,56.7` +0.13, `plant.MarshMarigold@104.25,63.25` −0.21, `plant.MarshMarigold@105.35,60.4` +0.33, `plant.MarshMarigold@107.8,63.7` −0.11, `plant.OstrichFern@103.5,60.7` +0.39, `plant.OstrichFern@104.3,65.3` +0.84, `plant.OstrichFern@107.2,65.1` +0.73, `plant.SoftRush@106.75,64.95` +0.26 and `plant.SpeckledAlder@107.8,66.95` +1.24.
  - Rocks: `rock.stp_af_chute_1` to `rock.stp_af_chute_4`, +0.21, +0.36, +0.50 and +0.58; `rock.stp_af_flat_stone` +0.38; and `rock.stp_af_stepping_stone_1`, `_2` and `_4`, −0.07, −0.11 and −0.07. The ground under the stepping stones now stands above the pool's +2.05, so say whether they still cross water.
  - `prop.stp_af_fallen_alder` +0.43, and `prop.stp_af_tin_cup` +0.60. The cup sits on the flat stone: name the stone as its mount, and it follows the stone.
- **Ginny's plot (4).**
  - `boat.stp_ginny_skiff` +2.37. It was hauled up the track above the spring high, and the ground under it is now +0.66, so move it up the track to dry ground above +2.20.
  - `prop.stp_gp_skiff_grapnel` +1.14 (its line runs down to the ford), `prop.stp_gp_track_staff` +0.30 and `plant.SpeckledAlder@104.6,57.2` +0.23.
- **The north-east light (4):** `prop.stp_ne_find_0`, `_1`, `_2` and `_5`, −1.54, −0.47, −2.78 and −1.05. What the tide leaves at the Head's foot is now under the Head's ask, `ground.stp_ne_head`: move each to the Head's new foot, in its own tide band.
- **The crossing (2):** `structure.stp_cx_wreck` +0.17, in its scour, and `gull.stp_cx_flats_a` +0.20.
- **The Landing (1):** `prop.stp_strand_bench` +0.14, down to +5.86 where it stands, as your `moves-pass9.json` has it. The game is placing the Landing, so the bench keeps its position.

### 4.2 Name a mount for the 19 pieces that stand on something else

The checker reads a piece's z against the ground under it, unless the piece names what it stands on. These stand where they should, but name nothing:
- the Landing's 8, on the wharf's deck (+5.35): `prop.stp_landing_bait_barrel`, `prop.stp_landing_cart`, `prop.stp_landing_gull`, `prop.stp_landing_rope`, `prop.stp_landing_totes`, `prop.stp_landing_traps`, `station.st_peters.head_lamp` and `station.st_peters.slip_lantern`;
- the cannery's 2 gulls: `prop.stp_cannery_gull_a` and `prop.stp_cannery_gull_b`;
- the light's 6 gulls: `prop.stp_ne_gull_bell`, `prop.stp_ne_gull_face_a`, `_b` and `_c`, and `prop.stp_ne_gull_stack_a` and `_b`;
- `aid.stp_ne_bar_east_cardinal`, afloat;
- `structure.stp_gap_bridge`, on its footings at +5.28;
- `fall.stp_alder_fall`, the fall's water, at the pool's +2.05.

A mount names the thing and its height, as `prop.stp_cannery_door_notice` and the lookout's gulls already do.

### 4.3 Art for the 18 placeholders

These stand in the one scene as placeholders (`kind: pending`), with no art:
- 4 gate leaves: `prop.stp_vl_gate_school`, `prop.stp_vl_gate_cream_saltbox`, `prop.stp_vl_gate_red_saltbox` and `prop.stp_vl_gate_harbour_gable`;
- the commons: `prop.stp_vl_common_logBench@-2.3,15.9`, `prop.stp_vl_common_logBench@-0.3,16.7` and `prop.stp_vl_common_toolShedLow@-22.4,25.6`;
- the 9 doorstep lanterns (`light.stp_vl_doorstepLantern_` and the home), and `light.stp_vl_batteryShedLight_sage_cottage`;
- `prop.stp_beach_steps`.

Give each its art from the kits you have, or list it as a kit ask with what it needs. Until its art comes, mark it on the boards, as the one-scene brief asks.

### 4.4 Still water, as data

The ponds and the brooks stand above the tide. The game draws them from its still-water map, which it derives from the terrain plan; nobody paints that map ([ADR 0046](https://github.com/apholmes7-bot/hiddenharbours/blob/e8de9cd075f5c0b2daf4719e87a7ef751e648ae7/docs/adr/0046-still-water-above-the-tide.md)). So:
- draw each as water on every board, at its surface level;
- carry each in the ground file with its id: a pond with its surface level and its outline, and a brook with its line, its widths and its bed.

The desk found these:
- `pond.stp_gap_pond` (surface +5.30, over its bed at +4.70) and `stream.stp_gap_brook`, which the gap bridge's scene.json already carries;
- `stream.stp_heath_brook`, which the beach's ask already carries;
- the Alder Fall's pool, at +2.05.

List any others.

### 4.5 Paths and paint

- **Every path a board draws goes in the data,** as a path with its id, its points and its width, as `path.stp_alder_fall_walk` does. The game draws paths from data.
- **Carry each scene's own paint into the one scene,** and keep it in that scene's scene.json: Ginny's path and her worn walks, the Alder Fall's walk and the crossing's paths.
- **The wet sand along the tide is the game's water's own.** Draw it on the boards as the ruled boards do. It needs no data.

### 4.6 The cliff faces, and the walls by number

- **Draw every cliff face with the cliff kit v6 (`PxCliff3`), as the game's walls will draw them.** The game's cliffs lane builds St Peters' cliffs on part 2's 79 walls with kit v6, so your ground's own faces are a look the game will not have. The lookout's ruled board drew its faces with `PxCliff3`, and that is the look to match across the island.
- **List by number every wall the one scene opens, moves or re-lines,** against part 2's 79: the beach's 045 to 054, opened, and each wall of the West and East Ledges' new lines. For a wall that moves, give its new line.
- **Finish each wall where it ends.** At the beach the cliff now ends in steps: end each wall into the slope beside it.

### 4.7 Keep every variant

The 15 variant pieces stay in the package's scene.json files, with their variants: the restored cannery, the crossing at a high, the light's integral station (`taken: false`) and the lookout's dusk dory (board only).

## 5. The checker, extended

The checker the one-scene brief asks for (real Node, built-in modules only) also checks:
- every id in each key scene's last ruled scene.json is in the package, or listed in the NOTES with why;
- every piece is within 5 cm of its last ruled position, or its move is listed;
- every piece's z is within 5 cm of the ground under it, or it names a mount;
- each ask's border is within 1 cm of the ground under it;
- the ground outside the asks matches pass 9, and the ground in each seam's band matches this return's, except under a listed ask;
- every still water is in the ground file, and the water is drawn once;
- every village piece is at the plan's position, within 5 cm.

It prints each failure by id, and it passes on the package.

## 6. The package

As in the one-scene brief's section 8: one zip, `HH-st-peters-one-scene-package-<date>.zip`, of 500 entries or fewer, with its SHA256SUMS. If it will not fit, split it into numbered parts, each with its own SHA256SUMS, and say so in the NOTES. Besides that section's list, it holds:
- the parity boards (section 3), each beside its ruled board, and the boards of the new places;
- the seam close boards, drawn from the package;
- in the NOTES: each parity board's differences with their reasons, every re-seat and move with both numbers, every mount, every kit ask, and every wall by number.

The quick look's boards and your working files need not come again.

## 7. After it arrives

The Art desk checks it on real Node, against pass 9 and against this return. Then the game takes it in, as the one-scene brief's section 9 says:
- the ground through its terrain work, with the south ring's terrain carrying the beach and the ledges;
- the walls through the cliffs work;
- the pieces through one placement for every scene;
- the village by its plan's own build.

As of 2026-10-01 12:20Z.
