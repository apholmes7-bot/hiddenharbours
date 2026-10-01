# Hidden Harbours: the design desk

**As of 2026-10-01 00:36Z.** The Art desk (the owner's steady art seat, an AI) keeps this file. The owner rules; the Art desk writes this branch and checks every return you send.

Read this file at the start of every session, then the brief the owner names. A ruling the owner gives you in a message wins over this file and over any brief. Some older docs still describe sprites where the owner has since ruled meshes (section 4); where they disagree, the ruling wins.

## 1. How this channel works

- This branch, `design-desk`, holds text only: this file and the `briefs/` folder. It is public, and it is never merged into the game.
- The owner pastes you one line: "Read <link> and follow it." The link names one brief.
- Links into the game point at one commit of its `main` branch (`e8de9cd0` today), so they cannot move while you work. Open them with your GitHub file reader. `/raw/` links are refused on your side (the channel check, 09-27), so briefs give `/blob/` links only.
- You only read here. Your work comes back as files the owner downloads (section 5).
- Pictures do not reach you through GitHub. The game keeps PNG, JPG, PSD and ZIP files in Git LFS, and the channel check of 09-27 found no way through: the GitHub page showed no image, the raw link was refused, the file server could not be reached, and your file reader returned only the pointer text. Every picture you need comes to you in a zip the owner uploads; the brief names it.

## 2. The game

- The canon: [docs/vision-and-pillars.md](https://github.com/apholmes7-bot/hiddenharbours/blob/e8de9cd075f5c0b2daf4719e87a7ef751e648ae7/docs/vision-and-pillars.md). Its logline: "Start with two hands and a tide table on a hard, beautiful stretch of the Atlantic Canadian coast. Dig clams at low water, earn your first boat and put it right, read the tides, the wind, and a market that never sits still, and work your way up from hauling handlines by hand to commanding a cargo fleet."
- The five pillars: P1 The Sea Has Moods · P2 From Dory to Dynasty · P3 A Living Working Coast · P4 Earn It, Then Automate It · P5 Cozy, but with Teeth. Every piece of art serves at least one.
- PC first (landscape; keyboard, mouse and gamepad). A phone port stays possible, so keep texture memory and draw counts modest.

## 3. The look

- The bible's north star: a working North Atlantic coast, cozy, weathered and quietly dangerous; Stardew's readable three-quarter top-down clarity wearing Kingdom Two Crowns' painterly light and limited palette.
- Its three non-negotiables: one perspective everywhere (three-quarter top-down; land, town and water share it); one scale, always (32 pixels per metre, 1 tile = 1 m); a limited palette that shifts with season, weather, time, fog and region but never loses its salt-stained North Atlantic identity.
- The art and audio bible: [docs/design/art-and-audio-bible.md](https://github.com/apholmes7-bot/hiddenharbours/blob/e8de9cd075f5c0b2daf4719e87a7ef751e648ae7/docs/design/art-and-audio-bible.md). Section 2 perspective (locked), 3 scale, 4 palette and mood, 6 light, day-night and fog, 7 UI art.
- The decisions that shape your work (ADRs):
  - [0021](https://github.com/apholmes7-bot/hiddenharbours/blob/e8de9cd075f5c0b2daf4719e87a7ef751e648ae7/docs/adr/0021-in-engine-js-rig-baking.md): the game runs your rig `.js` unmodified in its editor and bakes from it.
  - [0022](https://github.com/apholmes7-bot/hiddenharbours/blob/e8de9cd075f5c0b2daf4719e87a7ef751e648ae7/docs/adr/0022-3d-boat-hulls.md): large hulls are real-time 3D meshes, baked from the same rigs.
  - [0026](https://github.com/apholmes7-bot/hiddenharbours/blob/e8de9cd075f5c0b2daf4719e87a7ef751e648ae7/docs/adr/0026-rig-pivot-conventions.md): a rig pivot is a continuous cell-corner coordinate, not a pixel index.
  - [0029](https://github.com/apholmes7-bot/hiddenharbours/blob/e8de9cd075f5c0b2daf4719e87a7ef751e648ae7/docs/adr/0029-character-colour-runtime-structure-baked.md): the character: colour is runtime, structure is baked.
  - [0036](https://github.com/apholmes7-bot/hiddenharbours/blob/e8de9cd075f5c0b2daf4719e87a7ef751e648ae7/docs/adr/0036-interior-levels-as-layers.md): a second storey is a second interior layer on one footprint.
  - [0038](https://github.com/apholmes7-bot/hiddenharbours/blob/e8de9cd075f5c0b2daf4719e87a7ef751e648ae7/docs/adr/0038-boat-interiors.md): a boat cabin is a level that rides.
  - [0041](https://github.com/apholmes7-bot/hiddenharbours/blob/e8de9cd075f5c0b2daf4719e87a7ef751e648ae7/docs/adr/0041-full-mesh-interiors.md): full mesh interiors: the room becomes geometry, with a palette of its own.
  - [0042](https://github.com/apholmes7-bot/hiddenharbours/blob/e8de9cd075f5c0b2daf4719e87a7ef751e648ae7/docs/adr/0042-squash-is-an-art-fact.md): the squash is an art fact (the world plane against the bake projection).
  - [0044](https://github.com/apholmes7-bot/hiddenharbours/blob/e8de9cd075f5c0b2daf4719e87a7ef751e648ae7/docs/adr/0044-characters-are-meshes.md): characters are meshes in every state. Its text still says Proposed; the owner ruled its direction on 09-17 (section 4).
- Rig sources and kits: [docs/art/rigs](https://github.com/apholmes7-bot/hiddenharbours/tree/e8de9cd075f5c0b2daf4719e87a7ef751e648ae7/docs/art/rigs). The [recipe ledger](https://github.com/apholmes7-bot/hiddenharbours/blob/e8de9cd075f5c0b2daf4719e87a7ef751e648ae7/docs/art/rig-recipe-ledger.md) and the [asset manifest](https://github.com/apholmes7-bot/hiddenharbours/blob/e8de9cd075f5c0b2daf4719e87a7ef751e648ae7/docs/art/asset-manifest.md). Briefs that became canon: [docs/art/briefs](https://github.com/apholmes7-bot/hiddenharbours/tree/e8de9cd075f5c0b2daf4719e87a7ef751e648ae7/docs/art/briefs).
- St Peters' ground is terrain pass 9: [part 1](https://github.com/apholmes7-bot/hiddenharbours/blob/e8de9cd075f5c0b2daf4719e87a7ef751e648ae7/docs/design/st-peters-terrain-pass-9.md) and [part 2](https://github.com/apholmes7-bot/hiddenharbours/blob/e8de9cd075f5c0b2daf4719e87a7ef751e648ae7/docs/design/st-peters-terrain-pass-9-part-2.md), both ruled. The game's height map shows it only once terrain PR 5 lands. Until then, stand St Peters on the Art desk's texture of it (section 4, 09-29).

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
  - The run and the Fen Pool stay dry ground.
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

## 5. How work comes back

- One zip per job (or per boat), 500 entries or fewer, counting folders as well as files. A re-issue gets a new name.
- In every zip: a README, SHA256SUMS over every file, renders, and a checker that runs on Node 18 with built-ins only and ends with one line starting `RESULT:`.
- Every changed number goes in the README with its old and new value.
- Keep each rig's file name and the global it installs: the game's bake finds rigs by file name. If a change touches what the bake reads (the `const RIG = {` / `id:spec.id,` anchor, or `matsFor('gelcoat').MATS`), list it in the README; never change it silently.
- Your Node is emulated, so PNG bytes and the last digits of a float can differ from real Node. The Art desk re-runs every checker on real Node, compares pictures by pixels and regenerates your generated files there, so ship the scripts that write them.
- Ship every rig a scene or bake page loads inside the zip, or name the file in the game it matches byte for byte, as a blob link. A load from your project that is not in the zip cannot run here.
- A PNG master for every board or render; WebP only as an extra. If a full-size PNG will not upload, ship the rigs and the bake page, so the masters render here, and say so in the README.
- World positions in the game's units, each plan's own numbers: x is metres east, and y is north, where a metre of ground northward is 0.643 of a unit (the camera every rig is baked through, [ADR 0034](https://github.com/apholmes7-bot/hiddenharbours/blob/e8de9cd075f5c0b2daf4719e87a7ef751e648ae7/docs/adr/0034-ground-bearings-not-world-xy.md)). Heights and sizes in ground metres. State the reading in the file's `units`.

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
  - St Peters as one scene (09-30), asked before Nine Mile Creek's part 2: [briefs/BRIEF-2026-09-30-st-peters-one-scene.md](https://github.com/apholmes7-bot/hiddenharbours/blob/design-desk/briefs/BRIEF-2026-09-30-st-peters-one-scene.md). It asks for the whole island on pass 9's ground, the south ring included, with every scene in it, redrawn where pass 9 moves it. The water is drawn once and every seam is finished. A quick look comes first, then the package. Updated 10-01: the village as ruled, a main beach on the south shore, and one reading of y.
- Nine Mile Creek's wharf, an art pass (09-28). Asked of you: the wharf rearranged and redrawn with your newest kits, light and flora. Art comes first and the layout is free. It also takes your pass 6 fuel stop, re-sited on Wharf Road: [briefs/BRIEF-2026-09-28-nmc-wharf-pass.md](https://github.com/apholmes7-bot/hiddenharbours/blob/design-desk/briefs/BRIEF-2026-09-28-nmc-wharf-pass.md). Your quick look (revision 2) arrived on 09-28 and passed, and the owner ruled on it (section 4). The scenes were asked for in [briefs/BRIEF-2026-09-28-nmc-wharf-scene.md](https://github.com/apholmes7-bot/hiddenharbours/blob/design-desk/briefs/BRIEF-2026-09-28-nmc-wharf-scene.md).
  - Your scene return, in three parts, arrived on 09-28. It passed on real Node, 42 of 42, over the three parts together.
  - The gas bar's name stays open.
  - The build is being planned for the game.
- Nine Mile Creek's key scenes (09-29). Asked of you:
  - part 1, the region's ground drawn whole (the coast, the south shore, the rivers, the roads and the lots, as numbers and as a texture) with your list of key scenes, then a stop for the owner's look;
  - part 2, the scenes the owner picks, on that ground;
  - part 3, road kit v4, which the owner put in on 09-29.

  The wharf, the gas bar, the bar and the crossing stay as drawn: [briefs/BRIEF-2026-09-29-nmc-key-scenes.md](https://github.com/apholmes7-bot/hiddenharbours/blob/design-desk/briefs/BRIEF-2026-09-29-nmc-key-scenes.md).

  Parts 1 and 3 arrived on 09-30 and passed on real Node, 20 of 20 and 12 of 12. Your texture decodes here to your numbers, today's map in it is the game's, and the bar and the seam are today's to the bit. The owner took your fourteen calls (section 4). Part 2 was asked on 09-30, with three points on part 1's record, in the owner's paste. It now waits for St Peters as one scene (above). Road kit v4 reaches the game once the game's road bake can read it (the first open item in your README).
- Character kit 9.2 (09-24). In the game since 09-26; a change in review now plays its face, blink, look-at and carry clips. Asked of you: the send-back, [briefs/BRIEF-2026-09-30-character-9-2-send-back.md](https://github.com/apholmes7-bot/hiddenharbours/blob/design-desk/briefs/BRIEF-2026-09-30-character-9-2-send-back.md): six fixes, the owner's question on the fisher's irises, and two things the game measured. Nothing in the game waits on it.
- The channel check (09-27, on this branch): DONE the same day. Text reaches you; pictures do not (section 1). [briefs/BRIEF-2026-09-27-channel-check.md](https://github.com/apholmes7-bot/hiddenharbours/blob/design-desk/briefs/BRIEF-2026-09-27-channel-check.md).

## 7. Landed in the game from you

- Character rig v9.2 (09-24): [docs/art/rigs/character/rig9](https://github.com/apholmes7-bot/hiddenharbours/tree/e8de9cd075f5c0b2daf4719e87a7ef751e648ae7/docs/art/rigs/character/rig9). Everything earlier is under [docs/art/rigs](https://github.com/apholmes7-bot/hiddenharbours/tree/e8de9cd075f5c0b2daf4719e87a7ef751e648ae7/docs/art/rigs) and in the recipe ledger.
