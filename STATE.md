# Hidden Harbours: the design desk

**As of 2026-10-03 02:38Z.** The Art desk (the owner's steady art seat, an AI) keeps this file. The owner rules; the Art desk writes this branch and checks every return you send.

Read this file at the start of every session, then the brief the owner names. A ruling the owner gives you in a message wins over this file and over any brief. Some older docs still describe sprites where the owner has since ruled meshes (section 4); where they disagree, the ruling wins.

## 1. How this channel works

- This branch, `design-desk`, holds text only: this file and the `briefs/` folder. It is public, and it is never merged into the game.
- The owner pastes you one line: "Read <link> and follow it." The link names one brief.
- Links into the game point at one commit of its `main` branch (`fe3a734e` today), so they cannot move while you work. Open them with your GitHub file reader. `/raw/` links are refused on your side (the channel check, 09-27), so briefs give `/blob/` links only.
- You only read here. Your work comes back as files the owner downloads (section 5).
- Pictures do not reach you through GitHub. The game keeps PNG, JPG, PSD and ZIP files in Git LFS, and the channel check of 09-27 found no way through: the GitHub page showed no image, the raw link was refused, the file server could not be reached, and your file reader returned only the pointer text. Every picture you need comes to you in a zip the owner uploads; the brief names it.

## 2. The game

- The canon: [docs/vision-and-pillars.md](https://github.com/apholmes7-bot/hiddenharbours/blob/fe3a734e66554730cbade63b78b41c38ded20e7c/docs/vision-and-pillars.md). Its logline: "Start with two hands and a tide table on a hard, beautiful stretch of the Atlantic Canadian coast. Dig clams at low water, earn your first boat and put it right, read the tides, the wind, and a market that never sits still, and work your way up from hauling handlines by hand to commanding a cargo fleet."
- The five pillars: P1 The Sea Has Moods · P2 From Dory to Dynasty · P3 A Living Working Coast · P4 Earn It, Then Automate It · P5 Cozy, but with Teeth. Every piece of art serves at least one.
- PC first (landscape; keyboard, mouse and gamepad). A phone port stays possible, so keep texture memory and draw counts modest.

## 3. The look

- The bible's north star: a working North Atlantic coast, cozy, weathered and quietly dangerous; Stardew's readable three-quarter top-down clarity wearing Kingdom Two Crowns' painterly light and limited palette.
- Its three non-negotiables: one perspective everywhere (three-quarter top-down; land, town and water share it); one scale, always (32 pixels per metre, 1 tile = 1 m); a limited palette that shifts with season, weather, time, fog and region but never loses its salt-stained North Atlantic identity.
- The art and audio bible: [docs/design/art-and-audio-bible.md](https://github.com/apholmes7-bot/hiddenharbours/blob/fe3a734e66554730cbade63b78b41c38ded20e7c/docs/design/art-and-audio-bible.md). Section 2 perspective (locked), 3 scale, 4 palette and mood, 6 light, day-night and fog, 7 UI art.
- The decisions that shape your work (ADRs):
  - [0021](https://github.com/apholmes7-bot/hiddenharbours/blob/fe3a734e66554730cbade63b78b41c38ded20e7c/docs/adr/0021-in-engine-js-rig-baking.md): the game runs your rig `.js` unmodified in its editor and bakes from it.
  - [0022](https://github.com/apholmes7-bot/hiddenharbours/blob/fe3a734e66554730cbade63b78b41c38ded20e7c/docs/adr/0022-3d-boat-hulls.md): large hulls are real-time 3D meshes, baked from the same rigs.
  - [0026](https://github.com/apholmes7-bot/hiddenharbours/blob/fe3a734e66554730cbade63b78b41c38ded20e7c/docs/adr/0026-rig-pivot-conventions.md): a rig pivot is a continuous cell-corner coordinate, not a pixel index.
  - [0029](https://github.com/apholmes7-bot/hiddenharbours/blob/fe3a734e66554730cbade63b78b41c38ded20e7c/docs/adr/0029-character-colour-runtime-structure-baked.md): the character: colour is runtime, structure is baked.
  - [0036](https://github.com/apholmes7-bot/hiddenharbours/blob/fe3a734e66554730cbade63b78b41c38ded20e7c/docs/adr/0036-interior-levels-as-layers.md): a second storey is a second interior layer on one footprint.
  - [0038](https://github.com/apholmes7-bot/hiddenharbours/blob/fe3a734e66554730cbade63b78b41c38ded20e7c/docs/adr/0038-boat-interiors.md): a boat cabin is a level that rides.
  - [0041](https://github.com/apholmes7-bot/hiddenharbours/blob/fe3a734e66554730cbade63b78b41c38ded20e7c/docs/adr/0041-full-mesh-interiors.md): full mesh interiors: the room becomes geometry, with a palette of its own.
  - [0042](https://github.com/apholmes7-bot/hiddenharbours/blob/fe3a734e66554730cbade63b78b41c38ded20e7c/docs/adr/0042-squash-is-an-art-fact.md): the squash is an art fact (the world plane against the bake projection).
  - [0044](https://github.com/apholmes7-bot/hiddenharbours/blob/fe3a734e66554730cbade63b78b41c38ded20e7c/docs/adr/0044-characters-are-meshes.md): characters are meshes in every state. Its text still says Proposed; the owner ruled its direction on 09-17 (section 4).
- Rig sources and kits: [docs/art/rigs](https://github.com/apholmes7-bot/hiddenharbours/tree/fe3a734e66554730cbade63b78b41c38ded20e7c/docs/art/rigs). The [recipe ledger](https://github.com/apholmes7-bot/hiddenharbours/blob/fe3a734e66554730cbade63b78b41c38ded20e7c/docs/art/rig-recipe-ledger.md) and the [asset manifest](https://github.com/apholmes7-bot/hiddenharbours/blob/fe3a734e66554730cbade63b78b41c38ded20e7c/docs/art/asset-manifest.md). Briefs that became canon: [docs/art/briefs](https://github.com/apholmes7-bot/hiddenharbours/tree/fe3a734e66554730cbade63b78b41c38ded20e7c/docs/art/briefs).
- St Peters' ground is terrain pass 9: [part 1](https://github.com/apholmes7-bot/hiddenharbours/blob/fe3a734e66554730cbade63b78b41c38ded20e7c/docs/design/st-peters-terrain-pass-9.md) and [part 2](https://github.com/apholmes7-bot/hiddenharbours/blob/fe3a734e66554730cbade63b78b41c38ded20e7c/docs/design/st-peters-terrain-pass-9-part-2.md), both ruled. The game's height map shows it only once terrain PR 5 lands. Until then, stand St Peters on the Art desk's texture of it (section 4, 09-29).

## 4. Standing rulings (the owner's, 2026)

- 09-17, everyone is a mesh. The player and the whole cast draw as skinned meshes; sprite sheets stay only as a fallback.
- 09-23, more colours. A character may paint more than 16 materials: count painted materials per figure (fixed face colours and shade variants included), not ramps. The engine raises its limit to carry character rig v9 as it is, with its shading rule; v9 keeps its colours.
- 09-24, character rig v9 (rulings 1 to 8). v9 for the player and the whole cast. The full range of options, so nothing is baked ahead: the game gets a builder that runs no JavaScript. Base clothes in the creator, and every clothing option wearable from the cottage wardrobe until clothing stores exist. The character creator comes after New Game, before the intro. The game's fixtures are resized to fit every body. Over 1,000 triangles a figure is allowed.
- 09-26, the boats switch. Every hull, not only the sport fishers, moves to your new sidecar layout, your mesh interiors and the gameplay you designed, in batches you propose; the six new boats and the two sport fishers go first.
- 09-27, the 53's draft. The fleet rule stands: every boat floats at about 4% of her length, and a test in the game enforces it. The 53 (16.2 m) floats at 0.67 m, so her painted waterline and boot stripe move from 1.00 m to 0.67 m above the keel bottom, in her rig and in the batch. List it in the README as a changed number, old and new.
- 09-27, the key scenes, return 1. The pier head's lamp stays, lit at night like both wharf lanterns in the game (`when: 'dusk'`). The tide board goes below the south lip, as drawn. Your strings stand ("BAIT & ICE", "ST PETERS PACKING CO." and the for-sale notice), with the boards blank in pictures. The bait store waits. The cannery's silhouette is your set pieces as drawn, with no `signature` option. The restored look is a reference for M3 only.
- 09-28, Nine Mile Creek's wharf, the quick look. The following stand:
  - your berths, with the finger boats as the ambient fleet;
  - the shore path, and the road into the turning pad;
  - granite armour;
  - a boat ramp for small craft beside the working float;
  - the restaurant and the fish market to the north, with the ground able to grow for them.

  The breakwaters reach further south, so boats can sail round the fingers. The crane replaces the winch and needs gameplay. The harbourmaster moves to the wharf office, and the General Store folds into the gas station, whose name stays open. Ladders go down the quay face.
- 09-28, St Peters' north-east light (return 2, part 1). Your leans stand:
  - site A, the Head;
  - the square, tapered tower beside the keeper's house, as on PEI;
  - a keeper, who is one of St Peters' residents;
  - an oil lamp, with the kerosene kept in the oil house.

  The tunables stay as you drew them: `Fl W 6s`; lit at 06:00 and put out after sunrise; the fog bell rung by the keeper. The Head stands above the height map's +6.0 top. Your lean for that, a 16-bit texture, goes to the terrain lane, which decides.
- 09-28, St Peters' key scenes, return 2 (your calls 1 to 16, as the Art desk numbered them). The detail is in [briefs/BRIEF-2026-09-28-key-scenes-return-3.md](https://github.com/apholmes7-bot/hiddenharbours/blob/design-desk/briefs/BRIEF-2026-09-28-key-scenes-return-3.md).
  - Ginny's cottage keeps its door to the west, with more detail on its south wall or roof.
  - The camper gets a refresh.
  - The run and the Fen Pool hold water, drawn on their floors until there is a still-water map. *Corrected 10-02: this line said they stay dry ground. That was the Art desk's slip; see the 10-02 ruling below.*
  - Her sign reads "CODDLE".
  - The crossing stands as drawn.
  - The east end's three buildings wait for the wharf buildings' pass 2.
  - The four trees stay in the woods.
  - The life-ring moves off the spring high-tide line, to wherever the building lane picks.
  - The Alder Fall stands as drawn.
- 09-28, St Peters' key scenes, return 3. The detail is in [briefs/BRIEF-2026-09-28-ginnys-plot-redraw.md](https://github.com/apholmes7-bot/hiddenharbours/blob/design-desk/briefs/BRIEF-2026-09-28-ginnys-plot-redraw.md).
  - Ginny's cottage is your yellow `ginnyCottage`, as dialled.
  - The camper's pass 2 lands as sent.
  - The two go into the game as two pieces of work, the cottage first.
  - Ginny's plot is redrawn with both, the camper at the game's facing.
- 09-29, Ginny's plot, redrawn. Accepted: "the new plot looks good".
- 09-29, St Peters' ground. On your extra places quick look the owner said "the terrain looks like the old shape", and asked: "wouldnt it be easier that these scenes are built over the exisiting terrain modifications?" So every St Peters scene now stands on terrain pass 9's ground. The Art desk sends it to you as a 16-bit texture until terrain PR 5 bakes it into the game: [briefs/BRIEF-2026-09-29-st-peters-pass9-ground.md](https://github.com/apholmes7-bot/hiddenharbours/blob/design-desk/briefs/BRIEF-2026-09-29-st-peters-pass9-ground.md).
- 09-29, Nine Mile Creek's layout, on its key scenes brief ([briefs/BRIEF-2026-09-29-nmc-key-scenes.md](https://github.com/apholmes7-bot/hiddenharbours/blob/design-desk/briefs/BRIEF-2026-09-29-nmc-key-scenes.md), section 2):
  - the town stands a bit north of the gas bar's junction, with a medium-sized hardware store, a restaurant and bar, a grocery store and two or three apartment buildings, built from your new multi-unit buildings, manors and shopping plazas;
  - a new campground north of the crossing fills most of the land from the crossing to the town and the wharf;
  - the crossing lands at Nine Mile Creek's south-east corner, on the south shore's beach, which runs the width of the region into Rice Point's;
  - Wharf Road is paved;
  - road kit v4 is the brief's part 3.
- 09-29, St Peters' two extra places, on terrain pass 9's ground (your eight calls, in section 4 of your notes). The owner: "Go with leans". So:
  - the vista hold is 18.93 units tall, eased in and out;
  - the lookout's bench stays at (166.9, -39.3);
  - the seals keep your rule, so on pass 9 they haul out on W3 at every low, and on all three Whelps at springs;
  - the plank bridge replaces the stepping stones, on its footings at +5.28, with its deck at +5.66;
  - the dipping pool stands, its floor at +4.70;
  - the brook's sound point stays at (156.45, -40.95, +5.4);
  - the South Arm's faces go to the cliffs lane as a look;
  - next is your full return for both places, on pass 9's ground, the lookout first.

  The bridge's footings and the pool's cut are the terrain lane's to build, in terrain PR 5.
- 09-30, character kit 9.2 (the owner, on the character work's open items): the ten cast presets stay as baked until the wardrobe, and the fisher's teal irises go to you as a question, in [briefs/BRIEF-2026-09-30-character-9-2-send-back.md](https://github.com/apholmes7-bot/hiddenharbours/blob/design-desk/briefs/BRIEF-2026-09-30-character-9-2-send-back.md).
- 09-30, Nine Mile Creek's ground, part 1 (your fourteen calls, in section 5 of your NOTES-part1). The owner: "Go with leans". So:
  - the south shore is one beach, with five houses on the bank top along Route 91 and three on Shore Lane;
  - there are three rivers: Nine Mile Creek (the town river and the northern waterway as one), the north creek and the camp brook;
  - each river's still water runs down its tidal reach;
  - Route 91 leaves north as today and west to Rice Point;
  - the campground has its gate by the office and three loops: 29 serviced sites, 17 tent sites and 13 cabins;
  - the town stands as you placed it, with the lot moves in your call 6;
  - the gas bar's board reads `COFFEE  ICE  BAIT`, and its name stays open;
  - the truck park and the laydown move south of Wharf Road;
  - no ground goes beyond ±6 m;
  - the gas bar's top edge meets the river valley's rim when it is next drawn;
  - the barachois and the marsh pool are shapes;
  - the river wood stands;
  - there is no shore path north of the wharf for now;
  - your key scenes come in your order: the town bridge, the Falls, the landing and the campground first, then the north creek and the weather face.
- 09-30, St Peters first. The owner: "i want to start by finising up st peters as one new consolidated scene with all key scenes and seams completed so i can see it in game as soon as possible". So St Peters, as one scene on pass 9's ground, comes before Nine Mile Creek's part 2: [briefs/BRIEF-2026-09-30-st-peters-one-scene.md](https://github.com/apholmes7-bot/hiddenharbours/blob/design-desk/briefs/BRIEF-2026-09-30-st-peters-one-scene.md).
- 10-01, the village and a main beach. The owner ruled the village plan with its 11 recommendations, in their words "go with leans to make village square happen": plan B, six homes, no manor now, and the lights within the island's ruling. Pass 9 wins under the scenes, and each scene's own asks still win on its own spot. Then: "I want there to a be a popular main beach on the south shore of st peters". Both are in the one-scene brief, with one reading of y for every position (section 5).
- 10-01, St Peters' package taken. The owner, on your package's calls and the Art desk's leans on them: "go with leans". So West Gap Beach stands as drawn; the crossing's crest pools stay as pass 9 cuts them, 0.38 to 0.80 m; the Alder Fall keeps the quick look's planting; and the seven kit asks go to the next kit pass. The wall numbers were checked here against the walls table: yours were placed by length, so the game takes the Art desk's list by the real walls, and by those the beach opens walls 043 to 054, not 045 to 054. Three small fixes go to the game's own lanes, not to you: cliff walls on the Head's neck's south face, the beach's west end smoothed where it meets pass 9, and wall 068's foot kept under the spring low. Nine Mile Creek's part 2 resumes.
- 10-01, Nine Mile Creek's part 2 taken. The owner, on your calls 15 to 30, the two points on part 1's record, and the Art desk's leans on them: "I go with leans". So:
  - the river wall is laid with its fill; the boardwalks stand on piles on the plan's lines; the bridge stands as drawn, its name and year the owner's to give;
  - the restaurant and bar is ManorIso `harbourmaster`, and the game's building lane lights its glass at night;
  - the landing keeps its flood board; the Falls stand on the texture's ramp, with the steps the cascade piece's rock and the white water the game's foam; the Falls deck gets its 1.8 m landing at +3.2;
  - the campground keeps part 1's two roads, and the crossing's stakes stand every 8 m along the bar walk;
  - the waterfront houses stand as drawn, and each of the seven waterfront pads is at no less than +3.6;
  - the gully steps go down the cove's north wall, with a spur of the shore path to their head;
  - the weather face shows the bluff, the cove and the ledges in the squall, and the landing's two boards carry the bar;
  - the Art desk bakes the gas bar's three boards from your `nfsBakePage2c.html`;
  - Wharf Road runs straight at y 92 along the gas bar (call 30). At the wharf it takes lean A: onto the ramp and into the turning pad, with the ramp regraded at about 14%;
  - the groove at x 28 is filled;
  - all of the ground's changes, the river wall's fill and the pads included, go into one re-issue of part 1's ground (section 6), so the game bakes one texture.

  The town bridge's restaurant was drawn with an older ManorIso than the game's. The bridge's boards stand as baked, and from here on you use the game's: [manorIsoRig.js](https://github.com/apholmes7-bot/hiddenharbours/blob/fe3a734e66554730cbade63b78b41c38ded20e7c/docs/art/rigs/village-return/houses-kit/Art/manorIsoRig.js).
- 10-01, character kit 10.2 (rig 10). The owner took the Art desk's leans on it. The game follows your rig and draws no keyline on characters, so the outline ruling your README carries is made: no keyline. For now the game bakes only presets: the ten cast presets, then your twenty new builds with the wardrobe; and the game's creator checks each build against your gates when it bakes it. The send-back on the kit waited for the game's intake of it, and is now [briefs/BRIEF-2026-10-03-character-10-2-send-back.md](https://github.com/apholmes7-bot/hiddenharbours/blob/design-desk/briefs/BRIEF-2026-10-03-character-10-2-send-back.md).
- 10-02, St Peters' Fen Pool. The owner: "Also i take the leans on the fen pool". So the Fen Pool comes back as still water.
  - Your one-scene package filled its bowl (`ground.stp_fen_pool_dry`) because this file said the pool stays dry. That line was the Art desk's slip, and it is corrected in the 09-28 entry above.
  - In your next St Peters issue, take that fill out, re-cut the bowl, and list the pond in `stillWater` at its spill, as `pond.stp_fen_pool`.
  - Until then, the terrain lane ships your ground as it is, without the fen's water.
- 10-02, Nine Mile Creek as one scene taken. The owner, on your re-issue of part 1's ground (10-01), your one-scene package, its re-issue with the north creek redrawn, your calls 31 to 47 and the Art desk's leans on them: "i take your leans on all". So:
  - part 1's ground as you re-issued it on 10-01 is the ground every Nine Mile Creek scene stands on;
  - the plaza faces south (call 31), and the second three-decker is left out (call 32);
  - the plaza's site stands as you drew it (call 34), and the house on Mill Road, `lot.nmc_house_mill`, moves 2.5 m west to (-264.5, 152), 3 m off the truck apron;
  - the storefronts carry trade words only, and the stores' and the plaza's names stay open (call 35);
  - the plaza replaces main street's plaza master, and Water Street stands (call 37);
  - Shore Lane stands as you leaned it: its houses on their own rig, the lane gravel at 3.1 m with its turn, the larger houses and the three households (calls 38 to 41);
  - the north creek stands as redrawn: rears to the creek and doors to the lanes, each house its own recipe, walkouts on lots 1, 3 and 4 with lot 2's terrace, both Creek Lanes asphalt at 4.2 m, the seven households and the trees (calls 42 to 47). Lots 1, 3 and 4 stand 2.67 to 2.97 m off the asphalt, inside the plan's 3 m, and stay as drawn;
  - the truck wells (call 36) and the north creek's five laid boxes go into one more small re-issue of part 1's ground (section 6), so the game bakes one texture;
  - four small fixes go to the game's own lanes, not to you: the dories tied at their docks' heads, two bed ids renamed, the pickups seated on their drives, and the north bank's doors shown through their houses.

  From here on: no coordinates in ids (two beds were `_bed_35` and `_bed_29`; the game names them `_bed_w` and `_bed_e`), and a boat drawn afloat on the flood sits where a tide floats it (dory 5's spot needed +2.33, above the spring high).
- 10-03, Nine Mile Creek's campground and its last ground issue taken. The owner, on your second re-issue of the one-scene package (the campground redrawn, as the owner asked you), part 1's ground re-issued with the truck wells and the five laid boxes, your calls 48 to 58 and the Art desk's leans on them: "I take the leans". So:
  - the campground stands as redrawn. Three of Loop A's units stay St Peters campers, drawn from each preset's fields (call 48); the wharf office's trailer joins them as `boxCamper`, in four bodies and six paints (call 49); and so do the fifth-wheel and the pop-up (call 50);
  - tents stand on all 17 of Loop B's pitches, each on its pad, and b13 to b17 stay where you drew them (call 51);
  - the tennis court and the pool stand as drawn (calls 52 and 53), with the planting: 23 trees, 79 shrubs and the lawns (call 54);
  - the afternoon board is the campground's main board, and the dusk board stays (call 55);
  - the comfort station's lamps light its doors, on the south side (call 56);
  - in `plan.json`, lots 1 to 4 are the 3 m rule's exception, at their nearest corners: 2.72, 2.78, 2.02 and 2.12 m off the asphalt (call 57). The Art desk's earlier 2.67 to 2.97 m were measured from each lot box's lane side at its centre, and understated how close the north bank comes; nothing is redrawn;
  - no more ground: the houses stand as drawn, on their footings and walkouts (call 58);
  - five small fixes go to the game's own lanes, not to you: b17's cooler set beside its pad on dry ground, one id renamed, `moved.why` corrected for b10 and b15, the brook bridge built from `plan.json`, and the playground fitted clear of the roads.

  From here on: ids are `type.snake_case`, all lower case (a19's cabin tent was `prop.nmc_camp_tent_a19_cabinTent`; the game names it `prop.nmc_camp_tent_a19_cabin`). In `plan.json`, a house's extent is its `footprint`, and `at` is where its rig stands.

## 5. How work comes back

- One zip per job (or per boat), 500 entries or fewer, counting folders as well as files. A re-issue gets a new name.
- In every zip: a README, SHA256SUMS over every file, renders, and a checker that runs on Node 18 with built-ins only and ends with one line starting `RESULT:`.
- Every changed number goes in the README with its old and new value.
- Keep each rig's file name and the global it installs: the game's bake finds rigs by file name. If a change touches what the bake reads (the `const RIG = {` / `id:spec.id,` anchor, or `matsFor('gelcoat').MATS`), list it in the README; never change it silently.
- Your Node is emulated, so PNG bytes and the last digits of a float can differ from real Node. The Art desk re-runs every checker on real Node, compares pictures by pixels and regenerates your generated files there, so ship the scripts that write them.
- Ship every rig a scene or bake page loads inside the zip, or name the file in the game it matches byte for byte, as a blob link. A load from your project that is not in the zip cannot run here.
- A PNG master for every board or render; WebP only as an extra. If a full-size PNG will not upload, ship the rigs and the bake page, so the masters render here, and say so in the README.
- World positions in the game's units, each plan's own numbers: x is metres east, and y is north, where a metre of ground northward is 0.643 of a unit (the camera every rig is baked through, [ADR 0034](https://github.com/apholmes7-bot/hiddenharbours/blob/fe3a734e66554730cbade63b78b41c38ded20e7c/docs/adr/0034-ground-bearings-not-world-xy.md)). Heights and sizes in ground metres. State the reading in the file's `units`.

## 6. Open threads

Each thread has, or will get, its own paste in its own conversation. This list is here so every conversation knows the others exist. If one is new to you, say so, and the owner will send its paste.

- The boats switch (09-26). Asked of you: the batch plan; one versioned spec for the new layout (every field with its units and frame, the scripts that write it, and a checker in each zip); then the batches, the six new boats and the two sport fishers first, with the 53 at 0.67 m. On our side: your sport-fishers PROPOSAL zip of 09-26 (mesh interiors, ghost cutaway) arrived on 09-27, and all 14 sums match. An ADR for the new layout comes next; what it needs from you will follow when it lands.
- The village v3 send-back (09-26). Asked of you: Plan B drawn, two or three manor sites, the manor on the light engine, room heights and the side-entry rooms, the school's classroom, and the outbuilding rig's params sidecar with the kit designs. The owner ruled the village plan on 10-01: its plan B is drawn in St Peters as one scene (below).
- The St Peters key scenes (09-27). Asked of you: seven scenes (the Landing, the cannery, the north-east lighthouse and cliff, the village square, Ginny's plot, the waterfall, the tidal crossing). Return 1 (passes 1 to 5) arrived the same day and passed on real Node; the owner's rulings on it are in section 4. Pass 6 arrived on 09-28, then a re-issue of passes 1 to 7. Return 2 then came in parts, all on 09-28:
  - Part 1 (the north-east light, the sixteen rigs and the Alder Fall's scene.json) passed 15 of 17 checks on real Node.
  - Parts 2a, 2b and 3 (pass 2's notes, Ginny's plot, the crossing) passed 16 of 16, 4 of 4 and 13 of 13. Ginny's masters stitch here pixel for pixel.
  - The owner ruled the north-east light and your calls 1 to 16 (section 4).
  - The Alder Fall's masters will be rendered here from `pass6/afBakePage.html`.
  - The village square: the owner ruled the village plan on 10-01, so it is drawn in the one scene.
  - Return 3 (Ginny's cottage, then the camper's pass 2) arrived on 09-28 and passed on real Node, 43 of 43 and 49 of 49. The owner took both, with the cottage yellow (section 4).
  - Ginny's plot, redrawn ([briefs/BRIEF-2026-09-28-ginnys-plot-redraw.md](https://github.com/apholmes7-bot/hiddenharbours/blob/design-desk/briefs/BRIEF-2026-09-28-ginnys-plot-redraw.md)), arrived on 09-29, and the owner accepted it (section 4).
  - Your quick look at two extra places (the lookout at the South Arm's south end, and the plank bridge over the Gap Brook) arrived on 09-29 and passed on real Node, 43 of 43. It stood on the old ground, so you redrew it on St Peters' planned ground ([briefs/BRIEF-2026-09-29-st-peters-pass9-ground.md](https://github.com/apholmes7-bot/hiddenharbours/blob/design-desk/briefs/BRIEF-2026-09-29-st-peters-pass9-ground.md)). That arrived on 09-29 too and passed on real Node, 43 of 43: its texture is the Art desk's, pixel for pixel, and your spot heights match here. The owner took your eight calls on it (section 4). Next: your full return for both places, on pass 9's ground, the lookout first.
  - Your measure of what pass 9 moves under the six ruled scenes went to the terrain lane. As your notes say, each of those scenes redraws on pass 9, for what it moves, with its full return. They are asked for now, all together, in the one scene below.
  - St Peters as one scene (09-30), asked before Nine Mile Creek's part 2: [briefs/BRIEF-2026-09-30-st-peters-one-scene.md](https://github.com/apholmes7-bot/hiddenharbours/blob/design-desk/briefs/BRIEF-2026-09-30-st-peters-one-scene.md). It asks for the whole island on pass 9's ground, the south ring included, with every scene in it, redrawn where pass 9 moves it. The water is drawn once and every seam is finished. A quick look comes first, then the package. Updated 10-01: the village as ruled, a main beach on the south shore, and one reading of y. Your quick look arrived on 10-01, so the package now comes with each key scene's detail back inside the one scene, by a parity board each: [briefs/BRIEF-2026-10-01-st-peters-one-scene-package.md](https://github.com/apholmes7-bot/hiddenharbours/blob/design-desk/briefs/BRIEF-2026-10-01-st-peters-one-scene-package.md). Your package arrived on 10-01 in three parts and passed on real Node, 9 of 9, by default and with `--ruled`, every sum matching; the owner took it (section 4). Nothing more is asked of St Peters now.
- Nine Mile Creek's wharf, an art pass (09-28). Asked of you: the wharf rearranged and redrawn with your newest kits, light and flora. Art comes first and the layout is free. It also takes your pass 6 fuel stop, re-sited on Wharf Road: [briefs/BRIEF-2026-09-28-nmc-wharf-pass.md](https://github.com/apholmes7-bot/hiddenharbours/blob/design-desk/briefs/BRIEF-2026-09-28-nmc-wharf-pass.md). Your quick look (revision 2) arrived on 09-28 and passed, and the owner ruled on it (section 4). The scenes were asked for in [briefs/BRIEF-2026-09-28-nmc-wharf-scene.md](https://github.com/apholmes7-bot/hiddenharbours/blob/design-desk/briefs/BRIEF-2026-09-28-nmc-wharf-scene.md).
  - Your scene return, in three parts, arrived on 09-28. It passed on real Node, 42 of 42, over the three parts together.
  - The gas bar's name stays open.
  - The build is being planned for the game.
- Nine Mile Creek's key scenes (09-29). Asked of you:
  - part 1, the region's ground drawn whole (the coast, the south shore, the rivers, the roads and the lots, as numbers and as a texture) with your list of key scenes, then a stop for the owner's look;
  - part 2, the scenes the owner picks, on that ground;
  - part 3, road kit v4, which the owner put in on 09-29.

  The wharf, the gas bar, the bar and the crossing stay as drawn: [briefs/BRIEF-2026-09-29-nmc-key-scenes.md](https://github.com/apholmes7-bot/hiddenharbours/blob/design-desk/briefs/BRIEF-2026-09-29-nmc-key-scenes.md).

  Parts 1 and 3 arrived on 09-30 and passed on real Node, 20 of 20 and 12 of 12. Your texture decodes here to your numbers, today's map in it is the game's, and the bar and the seam are today's to the bit. The owner took your fourteen calls (section 4). Road kit v4 reaches the game once the game's road bake can read it (the first open item in your README).

  Part 2 arrived on 10-01 in three zips. It passed on real Node: 25 of 25, 24 of 24, and 30 of 30 with 2c's package group. Your scene tools regenerate here, and your answers on part 1's record check. The owner took it (section 4).

  Your re-issue of part 1's ground arrived on 10-01 and passed on real Node, 33 of 33. Your one-scene package (parts 2d, 2e and 2f, and all twelve scenes on one ground with one index) arrived on 10-02 and passed 24 of 24. Its re-issue the same day, with the north creek redrawn, passed 27 of 27. The owner took all three (section 4).

  Your re-issue of part 1's ground, with the truck wells and the five laid boxes, came back with your second re-issue of the package, the campground redrawn as the owner asked you. They passed on real Node, 35 of 35 and 32 of 32, and the owner took both (section 4). The Art desk baked the gas bar's three boards. Nothing more is asked of Nine Mile Creek's key scenes.
- Character kit 9.2 (09-24), then kit 10.2, the first on rig 10 (10-01). 9.2 has been in the game since 09-26, and since 09-30 it plays its face, blink, look-at and carry clips. Kit 10.2 arrived on 10-01 and passed on real Node; the owner's rulings on it are in section 4, and a change in review now bakes the ten cast presets from it. Asked of you: the send-back on 10.2, [briefs/BRIEF-2026-10-03-character-10-2-send-back.md](https://github.com/apholmes7-bot/hiddenharbours/blob/design-desk/briefs/BRIEF-2026-10-03-character-10-2-send-back.md). It carries what is still open from the 9.2 send-back ([briefs/BRIEF-2026-09-30-character-9-2-send-back.md](https://github.com/apholmes7-bot/hiddenharbours/blob/design-desk/briefs/BRIEF-2026-09-30-character-9-2-send-back.md)) and asks for the look-at's aim bar. Nothing in the game waits on it.
- The channel check (09-27, on this branch): DONE the same day. Text reaches you; pictures do not (section 1). [briefs/BRIEF-2026-09-27-channel-check.md](https://github.com/apholmes7-bot/hiddenharbours/blob/design-desk/briefs/BRIEF-2026-09-27-channel-check.md).

## 7. Landed in the game from you

- Character rig v9.2 (09-24): [docs/art/rigs/character/rig9](https://github.com/apholmes7-bot/hiddenharbours/tree/fe3a734e66554730cbade63b78b41c38ded20e7c/docs/art/rigs/character/rig9). Everything earlier is under [docs/art/rigs](https://github.com/apholmes7-bot/hiddenharbours/tree/fe3a734e66554730cbade63b78b41c38ded20e7c/docs/art/rigs) and in the recipe ledger.
