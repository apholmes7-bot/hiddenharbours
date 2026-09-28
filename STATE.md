# Hidden Harbours: the design desk

**As of 2026-09-28 16:10Z.** The Art desk (the owner's steady art seat, an AI) keeps this file. The owner rules; the Art desk writes this branch and checks every return you send.

Read this file at the start of every session, then the brief the owner names. A ruling the owner gives you in a message wins over this file and over any brief. Some older docs still describe sprites where the owner has since ruled meshes (section 4); where they disagree, the ruling wins.

## 1. How this channel works

- This branch, `design-desk`, holds text only: this file and the `briefs/` folder. It is public, and it is never merged into the game.
- The owner pastes you one line: "Read <link> and follow it." The link names one brief.
- Links into the game point at one commit of its `main` branch (`45a09dd6` today), so they cannot move while you work. Open them with your GitHub file reader. `/raw/` links are refused on your side (the channel check, 09-27), so briefs give `/blob/` links only.
- You only read here. Your work comes back as files the owner downloads (section 5).
- Pictures do not reach you through GitHub. The game keeps PNG, JPG, PSD and ZIP files in Git LFS, and the channel check of 09-27 found no way through: the GitHub page showed no image, the raw link was refused, the file server could not be reached, and your file reader returned only the pointer text. Every picture you need comes to you in a zip the owner uploads; the brief names it.

## 2. The game

- The canon: [docs/vision-and-pillars.md](https://github.com/apholmes7-bot/hiddenharbours/blob/45a09dd6c9412841f1ed7dd171e2013a2ce026f0/docs/vision-and-pillars.md). Its logline: "Start with two hands and a tide table on a hard, beautiful stretch of the Atlantic Canadian coast. Dig clams at low water, earn your first boat and put it right, read the tides, the wind, and a market that never sits still, and work your way up from hauling handlines by hand to commanding a cargo fleet."
- The five pillars: P1 The Sea Has Moods · P2 From Dory to Dynasty · P3 A Living Working Coast · P4 Earn It, Then Automate It · P5 Cozy, but with Teeth. Every piece of art serves at least one.
- PC first (landscape; keyboard, mouse and gamepad). A phone port stays possible, so keep texture memory and draw counts modest.

## 3. The look

- The bible's north star: a working North Atlantic coast, cozy, weathered and quietly dangerous; Stardew's readable three-quarter top-down clarity wearing Kingdom Two Crowns' painterly light and limited palette.
- Its three non-negotiables: one perspective everywhere (three-quarter top-down; land, town and water share it); one scale, always (32 pixels per metre, 1 tile = 1 m); a limited palette that shifts with season, weather, time, fog and region but never loses its salt-stained North Atlantic identity.
- The art and audio bible: [docs/design/art-and-audio-bible.md](https://github.com/apholmes7-bot/hiddenharbours/blob/45a09dd6c9412841f1ed7dd171e2013a2ce026f0/docs/design/art-and-audio-bible.md). Section 2 perspective (locked), 3 scale, 4 palette and mood, 6 light, day-night and fog, 7 UI art.
- The decisions that shape your work (ADRs):
  - [0021](https://github.com/apholmes7-bot/hiddenharbours/blob/45a09dd6c9412841f1ed7dd171e2013a2ce026f0/docs/adr/0021-in-engine-js-rig-baking.md): the game runs your rig `.js` unmodified in its editor and bakes from it.
  - [0022](https://github.com/apholmes7-bot/hiddenharbours/blob/45a09dd6c9412841f1ed7dd171e2013a2ce026f0/docs/adr/0022-3d-boat-hulls.md): large hulls are real-time 3D meshes, baked from the same rigs.
  - [0026](https://github.com/apholmes7-bot/hiddenharbours/blob/45a09dd6c9412841f1ed7dd171e2013a2ce026f0/docs/adr/0026-rig-pivot-conventions.md): a rig pivot is a continuous cell-corner coordinate, not a pixel index.
  - [0029](https://github.com/apholmes7-bot/hiddenharbours/blob/45a09dd6c9412841f1ed7dd171e2013a2ce026f0/docs/adr/0029-character-colour-runtime-structure-baked.md): the character: colour is runtime, structure is baked.
  - [0036](https://github.com/apholmes7-bot/hiddenharbours/blob/45a09dd6c9412841f1ed7dd171e2013a2ce026f0/docs/adr/0036-interior-levels-as-layers.md): a second storey is a second interior layer on one footprint.
  - [0038](https://github.com/apholmes7-bot/hiddenharbours/blob/45a09dd6c9412841f1ed7dd171e2013a2ce026f0/docs/adr/0038-boat-interiors.md): a boat cabin is a level that rides.
  - [0041](https://github.com/apholmes7-bot/hiddenharbours/blob/45a09dd6c9412841f1ed7dd171e2013a2ce026f0/docs/adr/0041-full-mesh-interiors.md): full mesh interiors: the room becomes geometry, with a palette of its own.
  - [0042](https://github.com/apholmes7-bot/hiddenharbours/blob/45a09dd6c9412841f1ed7dd171e2013a2ce026f0/docs/adr/0042-squash-is-an-art-fact.md): the squash is an art fact (the world plane against the bake projection).
  - [0044](https://github.com/apholmes7-bot/hiddenharbours/blob/45a09dd6c9412841f1ed7dd171e2013a2ce026f0/docs/adr/0044-characters-are-meshes.md): characters are meshes in every state. Its text still says Proposed; the owner ruled its direction on 09-17 (section 4).
- Rig sources and kits: [docs/art/rigs](https://github.com/apholmes7-bot/hiddenharbours/tree/45a09dd6c9412841f1ed7dd171e2013a2ce026f0/docs/art/rigs). The [recipe ledger](https://github.com/apholmes7-bot/hiddenharbours/blob/45a09dd6c9412841f1ed7dd171e2013a2ce026f0/docs/art/rig-recipe-ledger.md) and the [asset manifest](https://github.com/apholmes7-bot/hiddenharbours/blob/45a09dd6c9412841f1ed7dd171e2013a2ce026f0/docs/art/asset-manifest.md). Briefs that became canon: [docs/art/briefs](https://github.com/apholmes7-bot/hiddenharbours/tree/45a09dd6c9412841f1ed7dd171e2013a2ce026f0/docs/art/briefs).

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

## 5. How work comes back

- One zip per job (or per boat), 500 entries or fewer, counting folders as well as files. A re-issue gets a new name.
- In every zip: a README, SHA256SUMS over every file, renders, and a checker that runs on Node 18 with built-ins only and ends with one line starting `RESULT:`.
- Every changed number goes in the README with its old and new value.
- Keep each rig's file name and the global it installs: the game's bake finds rigs by file name. If a change touches what the bake reads (the `const RIG = {` / `id:spec.id,` anchor, or `matsFor('gelcoat').MATS`), list it in the README; never change it silently.
- Your Node is emulated, so PNG bytes and the last digits of a float can differ from real Node. The Art desk re-runs every checker on real Node, compares pictures by pixels and regenerates your generated files there, so ship the scripts that write them.
- Ship every rig a scene or bake page loads inside the zip, or name the file in the game it matches byte for byte, as a blob link. A load from your project that is not in the zip cannot run here.
- A PNG master for every board or render; WebP only as an extra. If a full-size PNG will not upload, ship the rigs and the bake page, so the masters render here, and say so in the README.
- Positions in ground metres (x east, y north), with the reading stated in the file's `units`.

## 6. Open threads

Each thread has, or will get, its own paste in its own conversation. This list is here so every conversation knows the others exist. If one is new to you, say so, and the owner will send its paste.

- The boats switch (09-26). Asked of you: the batch plan; one versioned spec for the new layout (every field with its units and frame, the scripts that write it, and a checker in each zip); then the batches, the six new boats and the two sport fishers first, with the 53 at 0.67 m. On our side: your sport-fishers PROPOSAL zip of 09-26 (mesh interiors, ghost cutaway) arrived on 09-27, and all 14 sums match. An ADR for the new layout comes next; what it needs from you will follow when it lands.
- The village v3 send-back (09-26). Asked of you: Plan B drawn, two or three manor sites, the manor on the light engine, room heights and the side-entry rooms, the school's classroom, and the outbuilding rig's params sidecar with the kit designs.
- The St Peters key scenes (09-27). Asked of you: seven scenes (the Landing, the cannery, the north-east lighthouse and cliff, the village square, Ginny's plot, the waterfall, the tidal crossing). Return 1 (passes 1 to 5) arrived the same day and passed on real Node; the owner's rulings on it are in section 4. Pass 6 arrived on 09-28, then a re-issue of passes 1 to 7. Return 2's part 1 (the north-east light, the sixteen rigs and the Alder Fall's scene.json) arrived on 09-28. On real Node it passed 15 of 17 checks.
  - The two that failed, `package.sums` and `package.entries`, count every file under the folder. So they fail once the zip is unpacked over the earlier return, as its README says. In later parts, count only the files the zip carries.
  - The owner ruled the north-east light (section 4).
  - The Alder Fall's masters will be rendered here from `pass6/afBakePage.html`.
  - Next, in the brief's order: pass 2's notes, Ginny's plot, then the crossing: [briefs/BRIEF-2026-09-27-key-scenes-return-2.md](https://github.com/apholmes7-bot/hiddenharbours/blob/design-desk/briefs/BRIEF-2026-09-27-key-scenes-return-2.md).
- Nine Mile Creek's wharf, an art pass (09-28). Asked of you: the wharf rearranged and redrawn with your newest kits, light and flora. Art comes first and the layout is free. It also takes your pass 6 fuel stop, re-sited on Wharf Road: [briefs/BRIEF-2026-09-28-nmc-wharf-pass.md](https://github.com/apholmes7-bot/hiddenharbours/blob/design-desk/briefs/BRIEF-2026-09-28-nmc-wharf-pass.md). Your quick look (revision 2) arrived on 09-28 and passed, and the owner ruled on it (section 4). The scenes were asked for in [briefs/BRIEF-2026-09-28-nmc-wharf-scene.md](https://github.com/apholmes7-bot/hiddenharbours/blob/design-desk/briefs/BRIEF-2026-09-28-nmc-wharf-scene.md).
  - Your scene return, in three parts, arrived on 09-28. It passed on real Node, 42 of 42, over the three parts together.
  - The gas bar's name stays open.
  - The build is being planned for the game.
- The channel check (09-27, on this branch): DONE the same day. Text reaches you; pictures do not (section 1). [briefs/BRIEF-2026-09-27-channel-check.md](https://github.com/apholmes7-bot/hiddenharbours/blob/design-desk/briefs/BRIEF-2026-09-27-channel-check.md).

## 7. Landed in the game from you

- Character rig v9.2 (09-24): [docs/art/rigs/character/rig9](https://github.com/apholmes7-bot/hiddenharbours/tree/45a09dd6c9412841f1ed7dd171e2013a2ce026f0/docs/art/rigs/character/rig9). Everything earlier is under [docs/art/rigs](https://github.com/apholmes7-bot/hiddenharbours/tree/45a09dd6c9412841f1ed7dd171e2013a2ce026f0/docs/art/rigs) and in the recipe ledger.
