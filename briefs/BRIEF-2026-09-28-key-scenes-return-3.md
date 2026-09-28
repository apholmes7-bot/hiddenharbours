# Brief 2026-09-28: the St Peters key scenes, return 3 (Ginny's cottage and the camper)

From the Art desk, for the owner. First read [STATE.md](https://github.com/apholmes7-bot/hiddenharbours/blob/design-desk/STATE.md) on this branch.

This continues the St Peters key scenes. It covers:
- what happened to return 2's parts 2 and 3;
- what the owner ruled on your calls;
- the two things to draw next: Ginny's cottage, then the camper.

## 1. Return 2, checked

- **Parts 2a, 2b and 3 arrived whole.** Part 2a's SHA256SUMS.txt matches 68 of 68, part 2b's 22 of 22, and part 3's 30 of 30.
- **Real Node agrees with your runs.** The parts were unpacked over return 1 and part 1, in the order your READMEs give. Part 2a passed 16 of 16, part 2b 4 of 4, and part 3 13 of 13.
- **Ginny's ten masters were stitched here** with your `tools/stitchMasters.cjs`. The PIXELS.txt it wrote here matches yours byte for byte (25 pictures).
- **The wharf kit in part 2a:**
  - 13 of its 17 files match the game's [wharf-rig-kit-v2](https://github.com/apholmes7-bot/hiddenharbours/tree/45a09dd6c9412841f1ed7dd171e2013a2ce026f0/docs/art/rigs/wharf-rig-kit-v2) byte for byte, and 2 more differ only in line endings.
  - `wharfBuildingRig2.js` and `wharfBuildingRig2.geo.js` are not in the game yet. They match the copies in your Nine Mile Creek return, and they land with the wharf buildings' pass 2 (call 10 below).
- **One note on the checkers.** Part 3 replaces the root SHA256SUMS.txt, and part 2a's package checks read that file. So once part 3 is unpacked, those two checks fail, although nothing is wrong with the files. Part 2b got this right: in later returns, give each part a sums file named for it, as `SHA256SUMS-part2b.txt` is.

## 2. The owner's rulings (28 Sept)

Your calls are numbered here as the Art desk put them to the owner.

**Ginny's plot**
1. **The cottage keeps its door to the west**, as the game has it; the turned variant is not taken. The owner asks for more detail on its south wall or its roof (section 3).
2. **The camper gets a refresh,** because it is still the pass 1 model (section 4). The refresh settles the skin question too.
3. **The run and the Fen Pool** stay dry ground, as drawn, until St Peters can hold still water.
4. **Her sign reads "CODDLE".** The owner takes your name for her.

**The crossing: all as drawn**
5. The refuge on stilts, on the Nine Mile Creek side of the gut.
6. The marker poles, along the crest-top walk.
7. The tide gauge, on the flats at the bar head.
8. The board's two crossing windows, and "REFUGE" on the refuge.
9. The clam digger's gear, the sandpipers and the gulls, as scenery that changes with the tide.

**The east end**
10. The net loft, the net shed and the ice house wait for their building kit, the wharf buildings' pass 2, as drawn. The owner has asked for that kit to be brought into the game.
11. The four landmark trees stay left to the woods, as drawn.
12. The life-ring moves off the spring high-tide line. The lane that builds the east end picks where: the deck's north edge, or the top of the slip.

**The Alder Fall: all as drawn** (pass 6, section 9 of your NOTES.md)
13. The fall is 1.8 m high.
14. The flow follows the weather: it rises 6 hours after rain and falls back over 36 hours.
15. The ground round the fall is eased. The terrain lane builds that.
16. The alder beside it moves 1.6 m west.

Calls 3 to 16 need nothing redrawn.

## 3. First, Ginny's cottage: a detail pass

**How it stands in the game.**
- The plot builds her cottage as the houses kit's `sageCottage`. See `CottageKey` in [StPetersGinnyPlot.cs](https://github.com/apholmes7-bot/hiddenharbours/blob/45a09dd6c9412841f1ed7dd171e2013a2ce026f0/Assets/_Project/Code/App/Editor/StPetersGinnyPlot.cs), at line 110. Its comment says why, and asks for this work: a dialled `ginnyCottage` of her own.
  - The village has a `sageCottage` too, so today the island has two of the same cottage.
- `sageCottage` is dialled from your [houseIsoRig.js](https://github.com/apholmes7-bot/hiddenharbours/blob/45a09dd6c9412841f1ed7dd171e2013a2ce026f0/docs/art/rigs/village-return/houses-kit/Art/houseIsoRig.js), in [VillageBuildingKit.cs](https://github.com/apholmes7-bot/hiddenharbours/blob/45a09dd6c9412841f1ed7dd171e2013a2ce026f0/Assets/_Project/Art/Editor/VillageBuildingKit.cs) at line 426:

  `era: plain`, `shape: gable`, `siding: clapboard`, `body: sage`, `roof: asphaltBrown`, `size: 0.25`, `windows: twoOverTwo`, `winDensity: 0.50`, `attic: gable`, `porch: front`, `dormers: 0`, `chimneys: 1`, `bay: false`, `weather: 0.55`.
- The cottage:
  - is 6.60 by 8.05 m;
  - has one room, which the game has baked;
  - has its door on the west gable;
  - draws lit like every house since the village return landed: the rig's `frame`, `lights` and `relight`, through `CoastalPass.light`.

**What to send.**
- **A `ginnyCottage` dial,** a settings list in the same form as the one above.
- **Keep what the game depends on,** so the baked room still fits:
  - the footprint;
  - the door on the west gable (`porch: front`);
  - the one room;
  - the storey height.

  Its place on the plot is frozen: centred at (84, 28), with the door to the west.
- **More on the south wall, the roof, or both.** The south wall is the one the camera sees. Which details is your call; it is the home of an aunt who has lived there a long time.
  - Some possibilities: windows, shutters or window boxes, a lean-to or woodshed against the wall, a bench, a stovepipe, patched shingles.
  - If a detail stands out from the wall, give its size, and say whether it changes the footprint the player walks round.
- **If the house rig needs new options for this,** make each one default to off. Every build the game already bakes must bake exactly as before: the school, the general store, the white farmhouse, the red saltbox and `sageCottage`. The checker proves it: each of those five dials renders the same pixels with the new rig as with the one in the game.
- **PNG masters:**
  - the cottage at its facing in the game, by day and at night;
  - the eight turntable facings.

## 4. Then the camper: pass 2

**How it stands in the game.**
- The camper on Ginny's lot is a home in the game, `home.ginny_lot_camper`. It is the Clipper, parked at (84, 42) at facing 2, with its door to the camera.
  - Where it is placed: [StPetersCamperLot.cs](https://github.com/apholmes7-bot/hiddenharbours/blob/45a09dd6c9412841f1ed7dd171e2013a2ce026f0/Assets/_Project/Code/App/Editor/StPetersCamperLot.cs).
  - Its data: [GinnyLotCamper.asset](https://github.com/apholmes7-bot/hiddenharbours/blob/45a09dd6c9412841f1ed7dd171e2013a2ce026f0/Assets/_Project/Data/Homes/GinnyLotCamper.asset).
- The rig is your pass 1 kit, landed in August: [camperIsoRig.js](https://github.com/apholmes7-bot/hiddenharbours/blob/45a09dd6c9412841f1ed7dd171e2013a2ce026f0/docs/art/rigs/camper-iso-kit/camperIsoRig.js) (`globalThis.CamperIso`) and [its README](https://github.com/apholmes7-bot/hiddenharbours/blob/45a09dd6c9412841f1ed7dd171e2013a2ce026f0/docs/art/rigs/camper-iso-kit/README.md).
  - It builds two lengths from one loft, the Bantam and the Clipper.
  - It has 8 facings and 11 skins.
  - Its door is the one part that moves.
  - It shades from the fixed upper-left key with ordered dither, as the kits did before the village return.
- Its sidecars: [camperIsoRig.clipper.gameplay.json](https://github.com/apholmes7-bot/hiddenharbours/blob/45a09dd6c9412841f1ed7dd171e2013a2ce026f0/docs/art/rigs/gameplay/dwellings/camperIsoRig.clipper.gameplay.json) and [camperIsoRig.bantam.gameplay.json](https://github.com/apholmes7-bot/hiddenharbours/blob/45a09dd6c9412841f1ed7dd171e2013a2ce026f0/docs/art/rigs/gameplay/dwellings/camperIsoRig.bantam.gameplay.json).

**What to send.**
- **The houses' light.** Publish `frame(dir, opts)`, `lights(dir, opts)` and `relight(fr, sky, o)` through `CoastalPass.light`, as the houses do, so the game can bake the camper lit.
  - What the game reads from a rig: [BuildingLightFrame.cs](https://github.com/apholmes7-bot/hiddenharbours/blob/45a09dd6c9412841f1ed7dd171e2013a2ce026f0/Assets/_Project/Code/Tools/Editor/RigBaking/BuildingLightFrame.cs).
  - `CoastalPass` is the houses kit's [coastalPass.js](https://github.com/apholmes7-bot/hiddenharbours/blob/45a09dd6c9412841f1ed7dd171e2013a2ce026f0/docs/art/rigs/village-return/houses-kit/Art/coastalPass.js).
  - Its lights are the door lamp and the windows.
- **A skin that reads.** Bare aluminium read dark in Ginny's plot. Make it read in the new light, bare or painted; that is your call. List what changes.
- **A top-level `"kind": "dwelling"` in both sidecars.** The game asks for this so it can find campers the way it finds road vehicles, by the sidecar's own word: [DwellingRigFleet.cs](https://github.com/apholmes7-bot/hiddenharbours/blob/45a09dd6c9412841f1ed7dd171e2013a2ce026f0/Assets/_Project/Code/Tools/Editor/RigBaking/DwellingRigFleet.cs).
- **Keep what the game depends on:**
  - the file name `camperIsoRig.js` and the global `CamperIso`;
  - both lengths and the eight facings;
  - the 384 by 320 cell, with the pivot at 192, 214;
  - 32 px to the metre and the 40° camera;
  - `render`, `frames`, `anchors` and the sidecar generator.

  If a number changes (the loft, the pivot, the door, a sidecar value), list it in the README, old and new. The game measures its door against the sidecar.
- **PNG masters:**
  - the Clipper on Ginny's lot at facing 2, by day and at night;
  - both lengths on the turntable.

## 5. Order and form

- **Order:** first the cottage, then the camper, one zip each. Send each as soon as it is ready.
- **Form:** section 5 of STATE.md holds for both.
- **Not in this brief:**
  - the village square, which is still held;
  - the Alder Fall, whose masters are rendered here;
  - your two extra places, which have no rush.
