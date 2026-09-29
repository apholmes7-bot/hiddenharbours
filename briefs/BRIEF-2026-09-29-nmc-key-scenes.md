# Brief 2026-09-29: Nine Mile Creek's key scenes, ground first

From the Art desk, for the owner. First read [STATE.md](https://github.com/apholmes7-bot/hiddenharbours/blob/design-desk/STATE.md) on this branch.

## The ask

The owner, on 09-29: "we created key scenes for St. Peters, now i want key scenes for NMC".

The owner also asked whether Nine Mile Creek's ground comes first: "does NMC need this to happen first before we create more key scenes? also should the current key scenes be integrated into a NMC terrain pass?" The Art desk's answer was yes to both: the ground first, with the scenes you have drawn kept as fixed points. The owner said: "yes lets go on nmc brief". So this brief has two parts, with a stop between them:
- **Part 1: the ground.** Design Nine Mile Creek's whole region the way terrain pass 9 redesigned St Peters: the coast, the south shore, the rivers, the roads and the lots. Send the heights as numbers and as a texture. Add your list of the key scenes worth drawing. **Then stop.** The owner looks at it and rules.
- **Part 2: the key scenes,** drawn on the ground the owner rules, the St Peters way.
- **Part 3: road kit v4,** only if the owner's paste says "Part 3 is on." (section 6).

**Why the ground comes first.** St Peters taught us this on 09-29 (STATE.md section 4): scenes drawn on today's ground look old once the ground changes. Most of Nine Mile Creek's best scenes will sit where its ground changes most.

**The two scenes you have drawn stay as they are.** The wharf and the gas bar are fixed points the new ground is designed round, and so are the bar and the crossing (section 3).

The owner attaches two zips:
- **`HH-nine-mile-creek-ground-2026-09-28.zip`:** Nine Mile Creek's ground as the game plays it today. It is the zip from the wharf pass, unchanged, because the game's map has not moved since.
- **`HH-st-peters-pass9-ground-2026-09-29.zip`:** St Peters' planned ground ([briefs/BRIEF-2026-09-29-st-peters-pass9-ground.md](https://github.com/apholmes7-bot/hiddenharbours/blob/design-desk/briefs/BRIEF-2026-09-29-st-peters-pass9-ground.md)), for the bar's far half and every view toward St Peters.

## 1. The region

**The frame.** The region is 760 × 560 m around the origin, set in [NineMileCreekSeabed.asset](https://github.com/apholmes7-bot/hiddenharbours/blob/404e3911dff3cadd8e4f2511aa6b04e3a61d8575/Assets/_Project/Data/Terrain/NineMileCreekSeabed.asset). x runs east from −380 to +380 and y runs north from −280 to +280. North is up. Every position in this brief is in these region metres.

**Reading today's ground** (the first zip):
- `NineMileCreekSeabed_HeightTex.png` is 1520 × 1120 pixels, 8-bit grey: 2 pixels per metre.
- Height in metres = −6 + 12 × grey / 255.
- Pixel (col, row), counted from the top-left, has its centre at x = −380 + (col + 0.5) / 2 and y = 280 − (row + 0.5) / 2.
- The game samples it bilinearly between pixel centres.

**The tide** is St Peters' own: mean 0, amplitude 2.2 m. Terrain pass 9's flood maps use five levels: spring low −2.20, neap low −0.99, mean 0, neap high +0.99, spring high +2.20.

**Today's ground, in words.** Read [the mainland doc](https://github.com/apholmes7-bot/hiddenharbours/blob/404e3911dff3cadd8e4f2511aa6b04e3a61d8575/docs/design/nine-mile-creek-mainland.md): section 1 for the geography, the roads and the town, 5 for the coast plan and 9 for the land behind it. In short:
- **The coast.** The mainland is a side, not a shape. One coast runs the region's height, 596 m of it from the south edge to the north edge, near x = 25 to 60. The water lies to the east and the fields to the west.
- **The bar** lands at (60, −150) and runs out east-south-east, on bearing 99.3°, for 305 m to the seam with St Peters.
- **North of the landing** come a ledge bank, then the gully, which is the only way down onto the ledges at low water, then the tall red bluff. The bluff is the weather face: from it you look back south-east across the bar to St Peters.
- **The wharf** stands on a low made spit at the creek's mouth, about 260 m north of the landing. The barachois, a lagoon behind a bar, lies behind it, and a marsh pool lies south of the road at (−26, 58).
- **The coast plan, from south to north:** beach 176 m, dune 34, ledge cliff 58, the gully's access 26, cliff 76, deep-shore cliff 29, beach 122, dune 30, beach 45.
- **The fields** stand at +6 m, the top of the map, with hedgerows and marsh. The land behind the shore is fields, not forest: three wood lots stand about 110 m back from the shore, and the lots cover at most 8% of it.
- **The roads:**
  - the through-road, Route 91 (`ThroughRoad` in the builder), gravel, 5 m wide, runs the region's height a couple of hundred metres inland, from (−230, −280) to (−186, 280);
  - Wharf Road, gravel, 5 m, runs from the through-road at (−178, 92) east to the wharf front at (140, 122);
  - the bar road, dirt, 4 m, runs from the landing north to Wharf Road at (−16, 92);
  - the gully path is 19 m of dirt footpath.
- **The town** is strung along the through-road, about 230 m inland.

**How the game builds it.** The `NineMileCreek*.cs` builders hold every position: start with [NineMileCreekMainland.cs](https://github.com/apholmes7-bot/hiddenharbours/blob/404e3911dff3cadd8e4f2511aa6b04e3a61d8575/Assets/_Project/Code/App/Editor/NineMileCreekMainland.cs), [NineMileCreekRoads.cs](https://github.com/apholmes7-bot/hiddenharbours/blob/404e3911dff3cadd8e4f2511aa6b04e3a61d8575/Assets/_Project/Code/App/Editor/NineMileCreekRoads.cs), [NineMileCreekFields.cs](https://github.com/apholmes7-bot/hiddenharbours/blob/404e3911dff3cadd8e4f2511aa6b04e3a61d8575/Assets/_Project/Code/App/Editor/NineMileCreekFields.cs) and [NineMileCreekWoodLots.cs](https://github.com/apholmes7-bot/hiddenharbours/blob/404e3911dff3cadd8e4f2511aa6b04e3a61d8575/Assets/_Project/Code/App/Editor/NineMileCreekWoodLots.cs). The game bakes the height map from them (`BakeNineMileCreekSeabed` in [TerrainPaintTool.cs](https://github.com/apholmes7-bot/hiddenharbours/blob/404e3911dff3cadd8e4f2511aa6b04e3a61d8575/Assets/_Project/Code/App/Editor/TerrainPaintTool.cs)). So the new ground comes back as numbers, and your texture is how everyone checks them (section 4).

## 2. What the ground must hold: the owner's rulings

**The overhaul (09-26):** "NMC gets the same overhaul terrain pass St. Peter’s got. With several rivers for fishing river fish."

**The south shore (09-26):** "we need to add more south shore to it", and "Yes with houses on south shore".
- Where it runs, as the owner answered it: Nine Mile Creek's shore faces east. Its south shore runs from about the crossing to St Peters west to Rice Point, which becomes a region of its own to the south-west. The south shore's houses stand along it.
- Today the region has no south shore: the east coast runs straight on to the south edge. So this is the largest change.

**The roads out (09-26):**
- **To Rice Point:** "There are several houses here, road connects to nine mile creek." In the owner's blueprint, the through-road runs on to Rice Point.
- **North:** the road runs north from Nine Mile Creek to the centre of the peninsula, then turns right down Rocky Point. The Cumberland road branches off it. The West River runs along the north side of the peninsula and Rocky Point.

**Three waterways, each ruled.** They may be one river, two or three. The 09-26 overhaul asks for several rivers.
1. **The town river (08-20):** "A river will run off the ocean and up through Nine Mile Creek town. It divides the town and creates small picturesque boardwalks."
   - The owner ruled the same day that "the town can move to accommodate this", and that the bridge where the through-road crosses it gets its own artwork.
   - Its seaward half is already there: the creek mouth in the coast at about (24 to 44, 128 to 168), and the barachois, centred at (−10, 132), 108 × 52 m, with its bed at −0.8 m ([harbour-geography.md](https://github.com/apholmes7-bot/hiddenharbours/blob/404e3911dff3cadd8e4f2511aa6b04e3a61d8575/docs/design/harbour-geography.md), section 5).
   - A boardwalk at +3.0 m, the wharf deck's height, is dry at every tide.
2. **The creek north of the wharf (09-26):** "perhaps one small creek that runs west inland from the water north of nine mile creek wharf. This will have waterfront properties on it".
3. **The northern waterway (09-28, with the gas bar):** "the norther waterway can be the stream/river running east to towards the northern part of the NMC Wharf".
   - Your `stream.nmc_junction_brook` drew it as a proposal (your wharf return's NOTES, section 7): from the gas bar's north side at (−196, 178) east to the barachois's west end at (−58, 146).
   - The game's plan for the wharf build treats your brook as one candidate course for the creek north of the wharf. It waits for this ground.

**Keep the coast's variety,** which Nine Mile Creek's earlier coastal passes gave it (the coast plan in section 1), unless the new shore reshapes it. Where it does, say what goes and why.

## 3. Held fixed

Draw these as they stand, and design the ground round them.
1. **The wharf, as you drew it, with your ground round it.** Your wharf return (`hh-nmc-wharf-return/`) has it:
   - NOTES section 3: the arms and the mouth, the dredging, the yard and the terrace, the boat ramp, the ladders, the shore path, the restaurant and the fish market;
   - NOTES section 6: the harbourmaster's office.

   The game is building this wharf as drawn.
2. **The gas bar** (`keyscene.nmc_fuel_stop`). It stands at the junction of Wharf Road and the through-road, (−178, 92): on the north side of Wharf Road, facing south, with the through-road down its west side. Its store takes the General Store's lot. Its name stays open.
3. **The bar and the crossing.**
   - The landing stays at (60, −150), and the bar keeps its line out to the seam, at the bar's midpoint.
   - The bar's crest stays as it is at the seam: the two halves are pinned to meet there.
   - St Peters' crossing scene draws the far side, on the second zip's ground.

**Open for part 1:**
- your brook and your lane (`road.nmc.stream_lane`);
- the town's lots and lanes, which this ground may move (the town river asks it to);
- the harbourmaster's and the General Store's old town lots, which both businesses have left, for the wharf office and the gas bar.

## 4. Part 1: what to send

One zip, in a folder named `HH-nmc-ground-part-1-<date>/`.

**The model.** Use terrain pass 9's plan ([st-peters-terrain-pass-9.md](https://github.com/apholmes7-bot/hiddenharbours/blob/404e3911dff3cadd8e4f2511aa6b04e3a61d8575/docs/design/st-peters-terrain-pass-9.md)) for what a ground design covers:
- its section 3: the coast in sections, with a height profile across each intertidal;
- its section 4: the streams and ponds, where each drains, and flood maps at the five tide levels;
- its section 5: the paths.

There, the terrain lane wrote the plan from your kit. Here you draw the ground yourself, so it comes back as data.

1. **`ground/plan.json`: the ground as numbers,** in region metres, with the reading stated in `units`.
   - **The coast, in sections:** each section's kind (beach, dune, ledge, cliff, marsh and so on), its line, and a cross-shore profile (distance to height).
   - **The south shore,** the same way.
   - **Each river or stream:** its line, width, bed height along it, bank slopes, its mouth, how far the tide runs up it, and its water level along it where it runs above the tide.
   - **Each pond or lagoon** (the barachois, the marsh pool and any new one): its outline, its bed, and its water level if it holds water above the tide.
   - **Each road:** its line, width and surface (paved, gravel, dirt or lane), and where it leaves the region. The boardwalks, bridges and paths too.
   - **The lots:** the town's, moved or new; the south shore's houses; the waterfront lots; the fields, hedgerows, wood lots and marsh.
   - **For everything the game has today,** its old and new place, under the builder's name for it (such as `HarbourmasterPos`).
   - Ids in your SCHEMA.md's style, such as `stream.nmc_junction_brook`. List any new prefix there.
2. **`ground/NineMileCreekSeabed_HeightTex_plan.png`: the heights as a texture.**
   - 16-bit grey, 1520 × 1120, on the same rectangle and pixel centres as today's.
   - State its range in the README. Today's is −6 to +6 m; going beyond it is the owner's call (section 7). The game reads a 16-bit map with a range of its own ([ADR 0046](https://github.com/apholmes7-bot/hiddenharbours/blob/404e3911dff3cadd8e4f2511aa6b04e3a61d8575/docs/adr/0046-still-water-above-the-tide.md), section 8).
   - If you can, send the water levels as a second texture on the same scale, where code 0 means no still water (ADR 0046, section 5).
   - Why both: the game bakes from the numbers, and the texture lets the Art desk and the terrain lane check the numbers. It is also what your part 2 boards stand on.
3. **Pictures, as PNG masters:**
   - a north-up plan of the whole region, with every section, river, road, lot and fixed point labelled, like pass 9's `plan-island.png` in the second zip;
   - today against the plan, as `pictures/change.png` in the second zip shows St Peters;
   - flood maps at spring low, mean and spring high, with the dry ground in each caption;
   - a few views as the game frames them, in TerrainLight6: the south shore and its houses; the town river and its boardwalks; the creek north of the wharf; the weather face looking across the bar to St Peters; and the edges where the roads leave.
4. **`NOTES-part1.md`:**
   - the idea, in a paragraph;
   - what moves from today and why, old and new, in metres;
   - what each move asks of the game: the builders, the roads, the town's lots, the seams, the wharf;
   - the owner's calls, each with your lean (section 7);
   - **the key scenes you propose for part 2.** For each: `keyscene.nmc_<name>`, its site, what makes it the signature place, and its clock, tide and weather. The wharf and the gas bar count as drawn.
5. **A checker and SHA256SUMS** (STATE.md section 5). The checker checks:
   - plan.json against the texture, at named points: every fixed point in section 3, each river's mouth and head, each lot;
   - that the region stays 760 × 560 m around the origin.

**Then stop.** Part 2 starts from the owner's rulings on part 1.

## 5. Part 2: the key scenes

Draw the scenes the owner picks from your list, on the ground the owner rules, the St Peters way. Follow the shape of section 5 of `README-FIRST.md` in the St Peters key scenes package (`HH-st-peters-key-scenes-2026-09-27.zip`). If this conversation cannot see that package, say so, and the owner will attach it.
- a `scene.json` per scene, in your SCHEMA.md's KeySceneDef, in region metres with `units`;
- PNG boards at the clocks, tides and weather each scene names;
- NOTES: the idea, what each scene asks of the game, the owner's calls with your leans, and the budgets;
- new pieces as rigs in a Nine Mile Creek set pieces kit, each piece with its own gameplay data;
- every rig each scene loads, inside the zip; a checker; SHA256SUMS.

**The package's rules** (its section 4) stand here too, with one exception: rule 10, materials and power, is St Peters' own. Nine Mile Creek is the working coast, so its materials follow [the wharf doc](https://github.com/apholmes7-bot/hiddenharbours/blob/404e3911dff3cadd8e4f2511aa6b04e3a61d8575/docs/design/nine-mile-creek-wharf.md) and your wharf return, with the gas bar and the wharf's lamps.

## 6. Part 3: road kit v4 (only if the owner's paste says "Part 3 is on.")

**Why.**
- The game's road work for Nine Mile Creek waits on it. The owner ruled on 09-28: "1. wait for road rig 4".
- Your gas bar loads `export/road-path-kit-v4/roadPathRig4.js` from your project, but the game has only [road kit v3](https://github.com/apholmes7-bot/hiddenharbours/tree/404e3911dff3cadd8e4f2511aa6b04e3a61d8575/docs/art/rigs/road-path-kit-v3).
- Your village return's notes name what v4 adds: the classes walk, footpath and lane (ruts and a grass crown).

**What to send:** its own zip, before part 2 if you can, holding:
- the kit's source and every rig, each with its gameplay sidecar;
- a README that lists every change from v3, old and new;
- a checker and SHA256SUMS.

Keep v3's file names and the globals they install. If a change touches what the game's bake reads, list it (STATE.md section 5).

## 7. Propose; don't decide

These are the owner's calls. Give each one with your lean:
- the south shore's line, and where its houses stand;
- the rivers: how many, where each runs, where the player fishes them, and how the town river, the creek north of the wharf and the northern waterway fit together;
- where each road leaves the region, and whether the road to Rice Point is the through-road carried on;
- each road's surface where your scenes and the game differ: your gas bar draws Wharf Road paved, and the game's is gravel;
- any move of the town's lots and lanes;
- ground above +6 m or below −6 m;
- the key scenes for part 2;
- in part 3, each change from road kit v3.

A ruling in the owner's paste wins over this brief.
