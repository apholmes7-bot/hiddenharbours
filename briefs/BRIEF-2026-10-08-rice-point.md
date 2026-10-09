# Brief 2026-10-08: Rice Point, ground first

Written by the owner's Planner for the owner; the Art desk checked it and publishes it here. First read [STATE.md](https://github.com/apholmes7-bot/hiddenharbours/blob/design-desk/STATE.md) on this branch. Rice Point follows the form of [Nine Mile Creek's key scenes brief](https://github.com/apholmes7-bot/hiddenharbours/blob/design-desk/briefs/BRIEF-2026-09-29-nmc-key-scenes.md), which stands wherever this one is silent. This brief reads main at b9e31bcf.

## The ask

The owner, on 10-08: "Rice Point next, write the brief for CD". And on its shape: "it should be wider than it is tall, it wont be accessed by any northern roads only the south road in NMC, so it will be mainly shoreline with the wharf and different fishing areas".

Rice Point is one of the areas the owner named on 09-26. It lies west of Nine Mile Creek, along the same shore as Nine Mile Creek's south shore. Nobody has drawn it yet. The game has no region, scene or ground there, so your plan is the first word on it.

This brief has two parts:
- **Part 1: the ground.** Design Rice Point's whole region the way you designed Nine Mile Creek's: the shore, the wharf as it stands today, the fishing areas, the houses and the road in from Nine Mile Creek. Send the heights as numbers and as a texture, with your list of the key scenes worth drawing. The owner looks at it and rules.
- **Part 2: Rice Point as one scene.** Draw the key scenes the owner picks on the ground the owner rules, and finish the seam to Nine Mile Creek, the way you drew your Nine Mile Creek one-scene package.

**The order:** part 1. **Then stop:** part 2 starts from the owner's rulings on part 1.

**Why the ground comes first.** St Peters taught it on 09-29 (STATE.md section 4): scenes drawn on ground that later changes look old.

**Which conversation.** If the conversation that drew Nine Mile Creek still has room, run this there. It already holds Nine Mile Creek's ground, plan and kits, and your SCHEMA.md. Otherwise the owner attaches `HH-nmc-ground-part-1-reissue-3-2026-10-07.zip`, your third issue of Nine Mile Creek's ground, which part 1 needs for the seam. If part 2 needs more from Nine Mile Creek's one-scene package, say so and the owner will attach it.

## 1. The region

**What it is.** The owner's words on 09-26:
- "3 is rice point an usued wharf which the player eventually comes to own and grow and has his personal fleet with staff stationed here";
- "There are several houses here, road connects to nine mile creek."

On 10-08 the owner added three things (above): Rice Point is wider than it is tall; only Nine Mile Creek's south road reaches it; and it is mainly shoreline, with the wharf and different fishing areas.

**Where it lies.** Nine Mile Creek's shore faces east. Its south shore runs from about the crossing to St Peters west to Rice Point, a region of its own to the south-west (the owner, 09-26; [Nine Mile Creek's brief](https://github.com/apholmes7-bot/hiddenharbours/blob/design-desk/briefs/BRIEF-2026-09-29-nmc-key-scenes.md), section 2). So along that shore, from west to east, come Rice Point, Nine Mile Creek's south shore and the landing, where the bar leaves for St Peters. In the owner's blueprint, the through-road runs on to Rice Point.

**The frame** is yours to propose (section 6), within four bounds:
- **Its shape.** It is wider, east to west, than it is tall (the owner, 10-08).
- **Its east edge** is Nine Mile Creek's west edge, x = −380 in Nine Mile Creek's region metres. [NineMileCreekSeabed.asset](https://github.com/apholmes7-bot/hiddenharbours/blob/b9e31bcf8c74472527dc2ca9f91155608a77f5f2/Assets/_Project/Data/Terrain/NineMileCreekSeabed.asset) sets that region at 760 × 560 m around the origin: x −380 to 380, y −280 to 280, north up. Rice Point shares the edge from the sea, across the strand and Route 91, to the land behind its houses (section 2).
- **Its sea edges.** Every edge in the sea runs through open, featureless water: never across a landfall, and never across a channel the player is steering through. The bay's water will meet those edges one day.
- **Its size.** The two built land regions are 760 × 520 m (St Peters) and 760 × 560 m (Nine Mile Creek).

**Positions.** Give every position in Rice Point's own region metres: x east, y north, from the region's centre. State the frame in `units`, with the offset that turns your metres into Nine Mile Creek's. Give every point on the seam in both frames. Heights are metres about mean sea level.

**The tide** is St Peters' own, as Nine Mile Creek's is: mean 0, amplitude 2.2 m. Regions that share ground share a tide. The flood maps use five levels: spring low −2.20, neap low −0.99, mean 0, neap high +0.99, spring high +2.20.

**The texture** uses Nine Mile Creek's format, so the seam can match code for code:
- 16-bit grey, 2 pixels a metre;
- height = −6 + 12 × code / 65535;
- pixel centres half a pixel in from the edges.

The still water is a second texture on the same scale, with code 0 for no still water ([ADR 0046](https://github.com/apholmes7-bot/hiddenharbours/blob/b9e31bcf8c74472527dc2ca9f91155608a77f5f2/docs/adr/0046-still-water-above-the-tide.md), section 5). Nine Mile Creek's ground stays within ±6 m (STATE.md section 4, 09-30). Here, ground beyond ±6 m is the owner's call.

## 2. The seam with Nine Mile Creek: held fixed

Nine Mile Creek's ground is ruled, every pixel of your third issue (STATE.md section 4, 10-07). Rice Point meets it, and Nine Mile Creek's side does not move. If Nine Mile Creek's ground is issued again, its west column stays as it is along your seam.

**The rule.** Along the seam, Rice Point's ground is Nine Mile Creek's. Your column of pixels beside the seam carries Nine Mile Creek's column 0, code for code, in both the height texture and the still texture. From there the ground runs on west without a step or a kink.

**What crosses the seam.** The Art desk re-read this table from your third issue's column 0 (x −379.75) and its plan.json. It runs south to north, in Nine Mile Creek's metres.

| y | What | Ground at column 0 |
|---|---|---|
| −280 to −245 | the bay's floor | −3.37 at the south edge, rising to −2.20 at the spring low line (y −245) |
| −245 to −184.5 | Rice Point Strand's beach (`coast.nmc_rice_point_strand`). `coast.nmc_south_shore`'s reference line starts at (−380, −203), and `path.nmc_south_strand` leaves for Rice Point's beach at (−380, −196) | −2.20 to +2.20: mean at y −203, the spring high line at y −184.5 |
| −184.5 to −167 | the red bank: a storm berm to +2.9 at y −176, then a face of about 2.3 m | +2.2 to +5.2 |
| −167 to −145 | the bank top: the tower house's lot (`lot.nmc_south_shore_estate_1`, x −380 to −330; call 59) | +5.2 at y −167, +5.4 by y −164, about +5.8 at y −145 |
| −145 to −139 | Route 91 (`road.nmc.route_91`): asphalt, 5.8 m wide, its centre line meeting the edge at (−380, −142), about 13° south of west; `hedge.nmc_route91_south` runs along its south side | +5.8 to +5.9 |
| −139 to −80 | the second row's west lot, the cupola Cape (`lot.nmc_south_shore_estate_4`, x −380 to −336; call 61) | +5.9 to +6.0: the ceiling from y −133 |
| −80 to 200 | the west farm (its south field from y −80 to −2, its north field from 2 to 88) and the river farm (100 to 196), with hedgerows reaching the edge at y −80 (call 61), 0 and 96 | +6.0, the texture's ceiling |

From the bay's floor to the bank top, column 0 is your strand's own profile (`coast.nmc_rice_point_strand`), laid on the reference line at y −203, to within a few centimetres: that profile is what Rice Point carries on west. Ten metres in from the edge, the estates' taller bluff rises from it (call 62); at the edge itself the bank is the strand's. Along the seam the ground barely changes from column 0 to column 1 (by 0.01 m at most, south of y 200). The still texture's column 0 is empty (code 0) south of y 218.

Further north, the town river (`stream.nmc_town_river`) leaves Nine Mile Creek at (−380, 222), "into the river’s upper valley in the next region west (not built)", as your plan.json has it; its valley crosses the edge from about y 202 to 222. Rice Point's frame need not reach it: north of your frame, Nine Mile Creek's west edge faces no region yet.

**Two things carry on across the seam:**
- **Route 91, the one road in.** It leaves Nine Mile Creek at (−380, −142), heading about 13° south of west, and "Rice Point's region takes the bend" (call 60, ruled 10-07). No road reaches Rice Point from the north (the owner, 10-08), so Route 91 ends in Rice Point. Show where it ends and where a vehicle turns there.
- **The beach.** The owner, 09-29: "the crossing should connect to a beach on NMC south shore, this beach runs the width of the scene and connects to the rice point region." Your `path.nmc_south_strand` walks that beach from the landing to Rice Point. Carry the beach, its bank and its path on.

**One thing to settle at the seam.**
- Call 61 moved the west farm's south edge to y −80, with a hedgerow on it, and gave the strip south of it to the second row.
- Your third issue's plan.json does not show that yet. It still runs `field.nmc_west_farm_south` from (−380, −130), and `hedge.nmc_y_minus96` along y −96 to the edge, through the cupola Cape's lot.
- Rice Point follows call 61, which the owner ruled: the lot's edge and the hedgerow at y −80. Fix the two plan lines in your next Nine Mile Creek issue.

**The neighbours.** Nine Mile Creek's westernmost houses stand near the seam: the tower house on the bluff at (−345, −148.5), south of Route 91, and the cupola Cape at (−356, −100), north of it (calls 59 and 61). Whatever Rice Point puts near the seam is seen beside them.

## 3. What the region must hold: the owner's words

**Mainly shoreline (10-08).** The region is mostly coast and water: the shore, the wharf and the fishing areas.

**The wharf (09-26):** "an usued wharf which the player eventually comes to own and grow and has his personal fleet with staff stationed here". So:
- **an unused wharf, standing,** drawn as the player first finds it, with nobody working it;
- **room to grow:** the water, the yard and the ground the grown wharf will need for the player's own fleet and for the staff stationed there;
- **growth comes later.** Owning the wharf, the fleet and the staff belong to the game's M3. The [roadmap](https://github.com/apholmes7-bot/hiddenharbours/blob/b9e31bcf8c74472527dc2ca9f91155608a77f5f2/docs/roadmap.md) gives M3 "the *first* automation (a staffed second boat)": pillar P4, Earn It, Then Automate It. So part 1 draws the growth as a plan, in stages, and part 2 draws only today's wharf.

**The berths.** Which boats a berth takes is decided by its depth, never by a rule (a standing ruling): a boat lies wherever the water over the bed is deeper than her draught.
- St Peters' dock (about 0.6 m) takes dories, punts and skiffs.
- Nine Mile Creek's berths (about 1.6 m) also take the lobster boat (draught 1.30 m) and the Cape Islander (1.40 m), and turn the side dragger (2.90 m) away.

For Rice Point, give the berths' beds and the least depth on the approach. Say which boats lie there at which tides, today and at each stage of the growth.

**The fishing areas (10-08):** "different fishing areas". Propose several, each different from the others. For each, give:
- its outline, its bottom (sand, mud, eelgrass, ledge, channel and so on) and its bed's height range;
- how the player fishes it: from the shore, the wharf, a dory or a boat, and with what gear;
- which of the game's ten species it suits ([Data/Fish](https://github.com/apholmes7-bot/hiddenharbours/tree/b9e31bcf8c74472527dc2ca9f91155608a77f5f2/Assets/_Project/Data/Fish)): American lobster, Atlantic cod, Atlantic herring, haddock, mackerel, pollock, rock crab, soft-shell clam, striped bass and winter flounder. Name any other species as a proposal.

**The houses (09-26):** "There are several houses here".

**The road (09-26):** "road connects to nine mile creek". It is Route 91, carried on from Nine Mile Creek's south shore, and it is the only road in (section 2).

**Its water** is drawn with Rice Point's scene, not as a scene of its own (09-26).

**Names (09-26):** "Keep them as written. We will change all names again later". Rice Point stays Rice Point. The wharf's own name stays open, as the gas bar's did.

## 4. Part 1: what to send

Send one zip, in a folder named `HH-rice-point-ground-part-1-<date>/`. Your Nine Mile Creek part 1 and its re-issues are the model.

1. **`ground/plan.json`: the ground as numbers,** in Rice Point's region metres. State the frame, the offset to Nine Mile Creek's metres and how to read it all in `units`. It holds:
   - the coast in sections: each section's kind, its line and a cross-shore profile, with the strand carried on from the seam;
   - the wharf as it stands: its deck and the deck's height, its piles or cribs, its ladders, slip and sheds, each berth's bed, and the approach with its least depth;
   - the growth in stages, each marked as later: what each stage adds (berths, the yard, sheds, the staff's quarters and anything else the fleet needs) and the depths it gives;
   - the fishing areas (section 3);
   - the houses' lots, each with its building;
   - Route 91, carried on to its end, and any lane;
   - each stream, pond or marsh, with its water level where it stands above the tide;
   - the fields, hedgerows and wood lots;
   - the water's depths and shoals;
   - the seam: each crossing in section 2's table, in both frames;
   - ids in your SCHEMA.md's style, with Rice Point's prefix listed there.
2. **`ground/RicePointSeabed_HeightTex_plan.png`:** the heights, in Nine Mile Creek's format (section 1), on your frame. **`ground/RicePointSeabed_StillTex_plan.png`:** the still water, on the same scale. State both sizes and the range in the README.
3. **`inputs/`:** Nine Mile Creek's third-issue height and still textures, unchanged, so the checker can read the seam.
4. **Pictures, as PNG masters:**
   - a north-up plan of the region, labelling every section, fishing area, stream, road, lot and berth, and the seam;
   - the seam: both regions side by side, 100 m each way, made from both textures, with Route 91 and the strand carried across;
   - flood maps at spring low, mean and spring high, each captioned with its dry ground;
   - the growth, one plan per stage;
   - a few views as the game frames them, in TerrainLight6: the wharf as the player first finds it, from the road and from the water; the fishing areas; the houses and the road; the strand where it crosses into Nine Mile Creek.
5. **`NOTES-part1.md`:**
   - the idea, in a paragraph;
   - the wharf today, and its growth stage by stage;
   - the fishing areas, side by side;
   - what the region asks of the game: a new region and its ground, the seam with Nine Mile Creek, and Route 91 carried on to its end;
   - the owner's calls, each with your lean (section 6);
   - **the key scenes you propose for part 2.** For each, give `keyscene.<prefix>_<name>`, its site, what makes it the signature place, and its clock, tide and weather.
6. **A checker and SHA256SUMS** (STATE.md section 5). The checker checks:
   - plan.json against the textures at named points: the berths and the approach, each fishing area, the seam's crossings, each lot, and each stream's mouth and head;
   - the seam: your column beside it against Nine Mile Creek's column 0 in `inputs/`, code for code, for both height and still water;
   - that the frame is wider than it is tall, and the offset.

**Then stop.** Part 2 starts from the owner's rulings on part 1.

## 5. Part 2: Rice Point as one scene

After the owner's rulings, draw Rice Point the way your Nine Mile Creek one-scene package draws Nine Mile Creek. Put the key scenes the owner picks in one scene with one index, on the ruled ground, and finish the seam. The package's form applies here:
- a `scene.json` per key scene, in your SCHEMA.md's KeySceneDef, in region metres with `units`;
- PNG boards at the clocks, tides and weather each scene names;
- NOTES: the idea, what each scene asks of the game, the owner's calls with your leans, and the budgets;
- new pieces as rigs in a Rice Point set-pieces kit, each piece with its own gameplay data;
- inside the zip, every rig each scene loads; a checker; SHA256SUMS.

Draw the wharf as it stands today, unused; its stages stay plans. Rice Point is the working coast, like Nine Mile Creek, so its materials follow [the wharf doc](https://github.com/apholmes7-bot/hiddenharbours/blob/b9e31bcf8c74472527dc2ca9f91155608a77f5f2/docs/design/nine-mile-creek-wharf.md) and your Nine Mile Creek wharf return. An unused wharf may stand dark, with its power off; say how you draw it.

## 6. Propose; don't decide

These are the owner's calls. Give each one with your lean:
- the frame: its size, and where it sits against Nine Mile Creek (wider than it is tall), so how much of Nine Mile Creek's west edge it meets;
- where the wharf stands and which way it faces; the depth at its berths and on its approach; which boats lie there today;
- the growth: its stages, what each adds, and the depth each gives;
- the fishing areas: how many, where, and each one's ground, gear and fish;
- the houses: how many, and where;
- Route 91's line through Rice Point, and where it ends;
- the coast's sections west of the seam;
- what lies past the region's west and north edges;
- ground above +6 m or below −6 m;
- Rice Point's id prefix;
- the key scenes for part 2.

A ruling in the owner's paste wins over this brief.
