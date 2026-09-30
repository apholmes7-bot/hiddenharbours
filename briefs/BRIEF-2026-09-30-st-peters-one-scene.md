# Brief 2026-09-30: St Peters as one scene (every key scene on its planned ground, every seam finished)

From the Art desk, for the owner. First read [STATE.md](https://github.com/apholmes7-bot/hiddenharbours/blob/design-desk/STATE.md) on this branch.

The owner, on 09-30, in their words: "i want to start by finising up st peters as one new consolidated scene with all key scenes and seams completed so i can see it in game as soon as possible". Then, on your region view of St Peters: "south shore looks like its missing stuff from the terrain pass that added prorposed terrain changes on the southshore".

So St Peters comes before Nine Mile Creek's part 2. If you have begun part 2, finish the scene in hand, keep it, and say in the NOTES where you stopped. Part 2 carries on after this.

## 1. What to make

St Peters as one scene: the whole island on terrain pass 9's ground, with every key scene in it and every seam finished. Send it as one package, so the game can place the island in one go.

## 2. The ground: all of pass 9, the south ring included

- **Stand the whole island on `StPetersSeabed_HeightTex_pass9.png`,** the Art desk's texture of pass 9. It came to you on 09-29 in `HH-st-peters-pass9-ground-2026-09-29.zip` ([its brief](https://github.com/apholmes7-bot/hiddenharbours/blob/design-desk/briefs/BRIEF-2026-09-29-st-peters-pass9-ground.md)). Its file sha256 starts `e3ad6b8b` and its pixels' sha256 starts `8c077ea1`. If this conversation does not have it, the owner uploads the zip again.
- **All of it:** [part 1](https://github.com/apholmes7-bot/hiddenharbours/blob/16cee51a7c574817cae6002b37ff61f36029ead6/docs/design/st-peters-terrain-pass-9.md), and [part 2](https://github.com/apholmes7-bot/hiddenharbours/blob/16cee51a7c574817cae6002b37ff61f36029ead6/docs/design/st-peters-terrain-pass-9-part-2.md) with the crossing's layout B.
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
- **Write one ground file for the island,** with every ask above, each with its id, in ground metres. As your SCHEMA has it, no scene.json carries a height.

## 3. The water, drawn once

Draw the island's water once, across every scene, from the ground's depth and St Peters' own tide (mean 0, springs ±2.20 m, neaps ±0.99 m). No scene draws water of its own. In your region view each scene's water shows as a lighter, finer rectangle: round the north-east light, off the east end past the harbour, and along the bar.

## 4. The seams

1. **Every scene's frame.** The ground, the paint, the flora and the water run across each frame without a line.
2. **The Head joins the island by its neck,** the ramp from +6 up to +11 in its `terrain.json`. Your region view shows the Head on a block cut straight along its west side, with water between it and the island.
3. **The Alder Fall's cut** meets part 1's north shore all round it.
4. **The crossing meets the bar, and the bar meets Nine Mile Creek** at the seam, the bar's midpoint. The crest there stays as it is, so the two halves meet (Nine Mile Creek's part 1 keeps its half to the bit).
5. **The east end** runs as one shore: the Landing, the cannery and the Harbour Strand.
6. **The two extra places** sit on pass 9's South Arm and Gap Brook. At the lookout, two stones at the board's west edge changed between your returns with no line in the NOTES: list them.

## 5. The scenes, all of them

- the Landing (`keyscene.stp_landing`) and the cannery (`keyscene.stp_cannery`). The game is placing their pieces now, so keep every id and position, and list anything pass 9 changes;
- the north-east light and the Head (`keyscene.stp_ne_light`), with the light station kit;
- Ginny's plot (`keyscene.stp_ginnys_plot`), as redrawn on 09-29, with the cottage yellow and the camper's pass 2;
- the Alder Fall (`keyscene.stp_alder_fall`);
- the crossing (`keyscene.stp_crossing`);
- the two extra places (`keyscene.stp_whelps_lookout` and `keyscene.stp_gap_bridge`);
- the east end, your pass 2 look pass (`pass2/eastEndPieces.json`).

The village square stays held: it waits on the owner's ruling of the village plan. Draw the village as your region view has it now, and mark it as held on the boards.

Ids stay as they are, because they are append-only. A new piece gets a new id.

## 6. What to send

**First, a quick look,** because the owner wants this in the game soon: the whole island on its ground, with its water and its seams, at mean tide and at a spring low. Then carry on to the package without waiting. The owner will stop you if the quick look needs it.

**Then the package:** one zip, named with its date, of 500 entries or fewer (section 5 of STATE.md). It holds:
- every scene's own `scene.json`, redrawn where its ground moved;
- one island file that lists every scene and its frame, and every seam with what meets what;
- the island's ground file (section 2);
- boards of the whole island at a spring low, at mean tide and at a spring high, and a close board of each seam in section 4. Every label shows its text (in your Nine Mile Creek view about a dozen draw as empty boxes);
- NOTES: for each scene, what changed from its last ruled return, with numbers; and any call for the owner, each with your lean;
- a checker for real Node, with built-in modules only. It checks every piece's z within 5 cm of the ground under it, each seam, and that the water is drawn once. Add a SHA256SUMS.

## 7. After it arrives

The Art desk checks the zip on real Node and reads it against pass 9. Then the game takes it in: the ground through its terrain work, on the same plan with the scenes' asks, and the pieces through the path the Landing and the cannery are on now.

As of 2026-09-30 23:35Z.
