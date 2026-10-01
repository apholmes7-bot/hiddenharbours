# Brief 2026-09-30: St Peters as one scene (every key scene on its planned ground, every seam finished)

From the Art desk, for the owner. First read [STATE.md](https://github.com/apholmes7-bot/hiddenharbours/blob/design-desk/STATE.md) on this branch.

The owner, on 09-30, in their words: "i want to start by finising up st peters as one new consolidated scene with all key scenes and seams completed so i can see it in game as soon as possible". Then, on your region view of St Peters: "south shore looks like its missing stuff from the terrain pass that added prorposed terrain changes on the southshore".

On 10-01 the owner ruled the village plan, so the village is drawn now (section 6), and then asked, in their words: "I want there to a be a popular main beach on the south shore of st peters" (section 7). If you began from this brief before 10-01, read it again: the reading of y (section 5), the village (section 6) and the beach (section 7) are new.

So St Peters comes before Nine Mile Creek's part 2. If you have begun part 2, finish the scene in hand, keep it, and say in the NOTES where you stopped. Part 2 carries on after this.

## 1. What to make

St Peters as one scene: the whole island on terrain pass 9's ground, with every key scene in it, the village as the owner ruled it, a main beach on the south shore, and every seam finished. Send it as one package, so the game can place the island in one go.

## 2. The ground: all of pass 9, the south ring included

- **Stand the whole island on `StPetersSeabed_HeightTex_pass9.png`,** the Art desk's texture of pass 9. It came to you on 09-29 in `HH-st-peters-pass9-ground-2026-09-29.zip` ([its brief](https://github.com/apholmes7-bot/hiddenharbours/blob/design-desk/briefs/BRIEF-2026-09-29-st-peters-pass9-ground.md)). Its file sha256 starts `e3ad6b8b` and its pixels' sha256 starts `8c077ea1`. If this conversation does not have it, the owner uploads the zip again.
- **All of it:** [part 1](https://github.com/apholmes7-bot/hiddenharbours/blob/e8de9cd075f5c0b2daf4719e87a7ef751e648ae7/docs/design/st-peters-terrain-pass-9.md), and [part 2](https://github.com/apholmes7-bot/hiddenharbours/blob/e8de9cd075f5c0b2daf4719e87a7ef751e648ae7/docs/design/st-peters-terrain-pass-9-part-2.md) with the crossing's layout B.
- **The owner is right about the south shore.** Your region view draws the south as one cliff line with a narrow strip of shallows below it, so part 2's ring is missing. Over part 1, part 2 changes 28,014 m², by up to +3.72 m (the reef fill). The ring is:
  - the South Flats, 12,382 m², from the harbour mouth round the south to the bar root, with their drains, pools, hollows, ribs and guts. The south's ground between the tides grows from 2,363 m² to 13,988 m²;
  - its sections, from the Harbour Strand round to the bar root: the Landing, the South Arm, the East Gap, the East Ledges, the Weather Cliff, the West Gap, the West Ledges, the South-West Bluff and the Storm Beach;
  - shingle spits and pocket beaches in the gaps, a reef with gullies and rock pools under the ledges, and a storm beach with its berm in the south-west corner;
  - the cliff walk round the south, 6 m inside the brows, from the slip road to the bar-head road, with a path down each gap.

  Say in the NOTES which ground your region view stood on, in the south and anywhere else.
- **Each scene's own ground asks go on top of pass 9,** and they win where the two overlap:
  - the Head: the `terrain.json` beside `keyscene.stp_ne_light` (the headland, the neck, the platform, the lantern stack and the NE bar), by its own rule, the higher of the two grounds with nothing lowered;
  - the cut round the Alder Fall, over x 99 to 114 and y 55 to 69.4, with its 1.6 m blend;
  - the cannery's site and 12 m round it, as they stand;
  - the bridge's two footings (level at +5.28) and the dipping pool (floor +4.70).
- **Where pass 9 moves a ruled scene's ground, redraw the scene on it.** Your own measure, `moves-pass9.json`, lists the moves:
  - the track down to Ginny's cove, by up to −3.17 m;
  - beside the Alder Fall, −2.66 m;
  - the crossing's head, −3.72 m;
  - the cannery's shingle, by up to +1.00 m;
  - 135 m² north of the light station;
  - the Landing's strand bench, from +6.00 to +5.86.

  Every string, lean and call the owner ruled stands (section 4 of STATE.md).
- **Write one ground file for the island,** with every ask above, each with its id, its heights in ground metres and its positions in the game's units (section 5). As your SCHEMA has it, no scene.json carries a height.

## 3. The water, drawn once

Draw the island's water once, across every scene, from the ground's depth and St Peters' own tide (mean 0, springs ±2.20 m, neaps ±0.99 m). No scene draws water of its own. In your region view each scene's water shows as a lighter, finer rectangle: round the north-east light, off the east end past the harbour, and along the bar.

## 4. The seams

1. **Every scene's frame.** The ground, the paint, the flora and the water run across each frame without a line.
2. **The Head joins the island by its neck,** the ramp from +6 up to +11 in its `terrain.json`. Your region view shows the Head on a block cut straight along its west side, with water between it and the island.
3. **The Alder Fall's cut** meets part 1's north shore all round it.
4. **The crossing meets the bar, and the bar meets Nine Mile Creek** at the seam, the bar's midpoint. The crest there stays as it is, so the two halves meet (Nine Mile Creek's part 1 keeps its half to the bit).
5. **The east end** runs as one shore: the Landing, the cannery and the Harbour Strand.
6. **The two extra places** sit on pass 9's South Arm and Gap Brook. At the lookout, two stones at the board's west edge changed between your returns with no line in the NOTES: list them.
7. **Where a seam needs it, you may change the ground.** To finish a seam, you may raise or lower pass 9's ground, and widen a scene's frame, where the seam needs it.
   - Every such change goes in the island's ground file as a new ask, with its id and its heights in ground metres, and in the NOTES with its numbers and why. A change that is only drawn never reaches the game, because the game takes its ground from that file.
   - These still stand: the bar's crest at the Nine Mile Creek seam, nothing lowered at the Head, and the Landing's and the cannery's pieces at their ids and positions.

## 5. One reading of y, for the whole island

Your 09-29 NOTES found the plans reading y two ways. Part 2, and your passes 1 to 3 with it, read y as ground metres. The village plan reads it as the screen's, ground north × 0.643, and your pass 4 square followed the plan. One scene needs one reading, and it is the game's. The game's world plane is the ground as its camera squashes it: a metre of ground northward is 0.643 of a unit, and a metre eastward is a full unit ([ADR 0034](https://github.com/apholmes7-bot/hiddenharbours/blob/e8de9cd075f5c0b2daf4719e87a7ef751e648ae7/docs/adr/0034-ground-bearings-not-world-xy.md)). Every rig is baked through that camera. So:
- **Every position is its plan's own number, unconverted.** The game places each piece at the number in its file, and every plan quotes the game's own positions: the village's hearth at (0, 14) is the game's. Your passes 1 to 3 already work this way. In the village, Green Row stays at y 28.3, not pass 4's 44.0. Pass 9's ground is laid in the same units, so a piece stands on the ground under its number.
- **A piece that spans between positions takes its ground size from the units it covers.** A unit northward is 1.556 m of ground. So the wharf deck's 6 units north to south are 9.33 m of planks, and the school's lot, 9.0 units deep on the plan, is 14.0 m of ground, as the plan says. A piece that stands free, such as a bench, a stack of traps or a rock, keeps its ground size.
- **A board draws a position at its number north to south.** The 0.643 is already in the number, so a board does not apply it again.
- **Keep the Landing's and the cannery's pieces as the game is placing them.** List every size this reading changes, there and elsewhere, with both numbers. Your 09-29 table has six.

## 6. The scenes, all of them

- the Landing (`keyscene.stp_landing`) and the cannery (`keyscene.stp_cannery`). The game is placing their pieces now, so keep every id and position, and list anything pass 9 changes;
- the north-east light and the Head (`keyscene.stp_ne_light`), with the light station kit;
- Ginny's plot (`keyscene.stp_ginnys_plot`), as redrawn on 09-29, with the cottage yellow and the camper's pass 2;
- the Alder Fall (`keyscene.stp_alder_fall`);
- the crossing (`keyscene.stp_crossing`);
- the two extra places (`keyscene.stp_whelps_lookout` and `keyscene.stp_gap_bridge`);
- the east end, your pass 2 look pass (`pass2/eastEndPieces.json`).

**The village, as the owner ruled it on 10-01:** [the village plan](https://github.com/apholmes7-bot/hiddenharbours/blob/e8de9cd075f5c0b2daf4719e87a7ef751e648ae7/docs/design/st-peters-village-plan.md), its plan B, with its 11 recommendations. Draw it from `plan-map.png` and `st-peters-village-plan.json`, which come with this brief in `HH-st-peters-village-plan-2026-10-01.zip`. The plan's section 10.1 has the asks for you. In this scene they are:
- plan B only. Plan A is not drawn;
- no manor (the owner's ruling: not now);
- six homes, three of them new from the houses kit, all clapboard: `cream_saltbox`, `white_gable` and `harbour_gable` (a home above the harbour shore, not a harbourmaster's). Their options are in `buildings[].optsJs`, and each needs a camera-first shell and room. The cream saltbox and the harbour gable house the cannery's four hands, and the white gable a family;
- every door on its porch front, seven of the nine facing south. The red saltbox and the white farmhouse enter from the west (entry 'left'), each down a walk of its own, with a signpost where the walk turns the corner. Each fits the pivot and footprint in its `buildings[].jobOneAsk`;
- a gate leaf for post and rail, wire and stone (today only the picket has one), the path from each gate to its door, and a doorstep lantern on a bracket by each door;
- the commons from the kits you have. If you draw a commons set, draw these first: a gravel hearth ring with log benches round the fire pit, and a low potting bench;
- the lights within the island's ruling: nine doorstep lanterns, five hurricane lanterns on posts that villagers light at dusk, the fire pit and the shed light. No poles, no wires and no street lamps;
- the store's and the post office's front paths stay 1.5 m footpaths, with no shell walks now.

Keep the plan's ids, and place every piece at the plan's number (section 5). If a piece's art is not ready, place it by the plan, mark it on the boards, and send its art in a package of its own.

The main beach is new: section 7.

Ids stay as they are, because they are append-only. A new piece gets a new id.

## 7. A main beach on the south shore

The owner, 10-01: "I want there to a be a popular main beach on the south shore of st peters".

Pass 9 has no such beach. From the South Arm round to the South-West Bluff the south is cliff, and at mean tide the sea stands at the cliffs' feet. Its only beaches are the pocket beaches in the two gaps and the Storm Beach's cobbles.
- **Where.** The Art desk's lean is the West Gap, opened into a sand bay. It lies about 85 units south of the village's hearth (some 130 m of ground), where the heath's path already comes down. The Heath Brook runs out across it, the Heath Pond lies behind, and the Weather Cliff and the West Ledges frame it. The Storm Beach, the crossing and the clam flats stay as they are. If another place on the south shore makes a better main beach, show it in the quick look with its numbers.
- **What it is:** the island's main beach, and its busiest shore in summer.
  - Dry sand above the spring-high line (+2.20 m), so there is somewhere to sit at every tide.
  - A long face of sand that shelves gently into the sea, so the waves break along it. At a low it runs out onto the South Flats.
  - Marram dunes behind it, and the brook's mouth across it.
  - Long enough to be the island's main beach. The desk's lean is 60 m or more of sand along the water.
- **Its way down.** The heath's path from the village becomes the beach's main way down, with steps where the cut is steep and a bench at the top. The cliff walk keeps its id, and goes round the head of the bay or down across it.
- **Popular.** Show it in use on a High Summer afternoon at mean tide, with people on the sand and in the water. The game's beach-goers come later, with the village's people.
- **Its ground and its walls.** The beach is a new ask in the island's ground file (say `ground.stp_main_beach`), with its heights in ground metres. List every cliff wall it opens or moves, by number, in the NOTES. Section 4's limits stand, and the beach's sand meets the South Flats without a line. The scene gets a new id, `keyscene.stp_main_beach`.

## 8. What to send

**First, a quick look,** because the owner wants this in the game soon: the whole island on its ground, with its water, its seams, the village and the beach, at mean tide and at a spring low, and the beach on a High Summer afternoon. Then carry on to the package without waiting. The owner will stop you if the quick look needs it.

**Then the package:** one zip, named with its date, of 500 entries or fewer (section 5 of STATE.md). It holds:
- every scene's own `scene.json`, redrawn where its ground moved;
- one island file that lists every scene and its frame, and every seam with what meets what;
- the island's ground file (section 2);
- boards of the whole island at a spring low, at mean tide and at a spring high, and a close board of each seam in section 4. Every label shows its text (in your Nine Mile Creek view about a dozen draw as empty boxes);
- NOTES: for each scene, what changed from its last ruled return, with numbers; every size the reading of y changes (section 5), with both numbers; the beach's site and every wall it opens; and any call for the owner, each with your lean;
- a checker for real Node, with built-in modules only. It checks every piece's z within 5 cm of the ground under it, each seam, that the water is drawn once, and every village piece at the plan's position within 5 cm. Add a SHA256SUMS.

## 9. After it arrives

The Art desk checks the zip on real Node and reads it against pass 9. Then the game takes it in: the ground through its terrain work, on the same plan with the scenes' asks, and the pieces through the path the Landing and the cannery are on now. The village comes in by its plan's own build, and the beach's ground with the south ring's terrain.

As of 2026-10-01 00:36Z.
