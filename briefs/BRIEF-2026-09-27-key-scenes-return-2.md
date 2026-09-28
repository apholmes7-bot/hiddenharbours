# Brief 2026-09-27: the St Peters key scenes, return 2

From the Art desk, for the owner. First read [STATE.md](https://github.com/apholmes7-bot/hiddenharbours/blob/design-desk/STATE.md) on this branch.

This continues the St Peters key scenes. It covers:
- what happened to your return 1 (passes 1 to 5);
- what the owner ruled on it;
- the ground you asked for;
- what to send next, in order.

The owner attaches one zip to this message: `HH-st-peters-height-map-2026-09-27.zip` (section 3).

## 1. Return 1, checked

- **All 110 files arrived.** SHA256SUMS.txt matches 109 of 109.
- **Real Node agrees with your run.** It ran `checks/all.cjs`: ground 30, kit 71, scenes 40, return 6, RESULT OK. Your `checks.txt` agrees line for line, apart from separators and one signed zero.
- **Your y reading stands.** Passes 1, 2, 3 and 5 read y as ground metres, as the game's code does. Keep it.
- **Pass 1 will go into the game in two steps:**
  1. The set-pieces kit: its eight pieces are baked, and each gets a data file of its own.
  2. The Landing's and the cannery's "today" pieces are placed in St Peters from data, without rebuilding the scene.

  Nothing restored is built now.

## 2. The owner's rulings on return 1 (27 Sept)

Your NOTES section 3, answered:

1. **The pier head's lamp: keep it**, lit at night as the game has it today. It lights the ladder at low water.
   - In the Landing's scene.json, `station.st_peters.head_lamp` goes from `when: 'never'` to `when: 'dusk'`.
   - The infrastructure doc's "nothing standing" row, which you followed, is being corrected to match.
   - Both wharf lanterns light at night in the game: `LampPostSites()` in [StPetersWharf.cs](https://github.com/apholmes7-bot/hiddenharbours/blob/8ae9c5afa3c364c82f311773e209fbbe98ccedbd/Assets/_Project/Code/App/Editor/StPetersWharf.cs), lit by [LampPosts.cs](https://github.com/apholmes7-bot/hiddenharbours/blob/8ae9c5afa3c364c82f311773e209fbbe98ccedbd/Assets/_Project/Code/App/Editor/LampPosts.cs). Draw them so from now on.
2. **The tide board:** below the south lip, as you drew it.
3. **The strings: yours.** "BAIT & ICE", "ST PETERS PACKING CO.", and the notice that the cannery is for sale. Keep the boards blank in pictures; the game letters them.
4. **The bait store** waits for the wharf buildings' pass 2 (the Art desk's drop 13) to land in the game. There is no work in it for you.
5. **The cannery's silhouette:** your set pieces as drawn. No `signature` option on the kit's cannery preset.
6. **The restored look:** the reference for M3 only.

**Not ruled yet.** Draw these as the game has them today until the owner rules:
- the root lantern's dusk rule ("the last villager to tie up lights it"). Today the lantern lights itself at night;
- your pass 5 calls (NOTES section 8): the cottage turned to the camera, the camper's skin, and water in the run and the Fen Pool;
- your two extra places: 11, a lookout over the Whelps; 12, a plank bridge over the Gap Brook.

## 3. The ground you asked for

The zip holds `StPetersSeabed_HeightTex.png`, a README and a SHA256SUMS.txt. This is the ground the game plays today.

**The settings** are in [StPetersSeabed.asset](https://github.com/apholmes7-bot/hiddenharbours/blob/8ae9c5afa3c364c82f311773e209fbbe98ccedbd/Assets/_Project/Data/Terrain/StPetersSeabed.asset):
- world centre (0, 0);
- world size 760 × 520 m;
- minimum −4 m, maximum +6 m.

**Reading the picture:**
- The PNG is 1520 × 1040 pixels, 8-bit grey: 2 pixels per metre.
- Height in metres = −4 + 10 × grey / 255.
- Count pixel (col, row) from the top-left. Its centre is at x = −380 + (col + 0.5) / 2 and y = 260 − (row + 0.5) / 2, in metres, with x east and y north. North is up.
- The game samples bilinearly between pixel centres: `ElevationAt` and `DecodeElevation` in [PaintedHeightField.cs](https://github.com/apholmes7-bot/hiddenharbours/blob/8ae9c5afa3c364c82f311773e209fbbe98ccedbd/Assets/_Project/Code/World/PaintedHeightField.cs).

**Checking it.**
- Its sha256 is `dd4780eeeb0b81a986e75c75e3b06acd3724405166295fa545f620e30717c71c`, 28,150 bytes.
- Your file store adds bytes to the images it saves (your NOTES 0.1), so check the pixels, not the file's bytes.

**What it cannot show:**
- The ground tops out at +6.0 m. Anything higher, like the north-east Head at +11.0, is new ground. Give it as numbers the terrain lane can build from, not as paint.
- Where [part 2 of the terrain plan](https://github.com/apholmes7-bot/hiddenharbours/blob/8ae9c5afa3c364c82f311773e209fbbe98ccedbd/docs/design/st-peters-terrain-pass-9-part-2.md) reshapes ground that this picture does not show yet, keep part 2's numbers. Say so, piece by piece.
- The derived maps (`maps2`) are not in the repo. If you need one, say which, and why.

**Re-measure** every height in return 1, and in what you send next, against it. In NOTES, list every height that moves by more than 5 cm, with the old value, the new value, and what it changes.

## 4. What to send next, in order

One scene per zip is fine. Send each one as soon as it is ready.

**First, the rigs return 1 left out.** Passes 2, 3 and 5 load sixteen rigs from your project that the zip did not carry, so they cannot run here yet. Ship them with item 1, under the paths your load lists use:
- your lighthouse rig kit v2 (`export/lighthouse-rig-kit-v2/`): `stationLight2.js`, `lighthouseRig2.js`, `buoyRig2.js`. The game does not have this kit; its only lighthouse is [lighthouseIso.js](https://github.com/apholmes7-bot/hiddenharbours/blob/8ae9c5afa3c364c82f311773e209fbbe98ccedbd/docs/art/rigs/lighthouseIso.js).
- your nav buoy kit (`export/nav-buoy-rig-kit/`): `isoSolid.js`, `navBuoyRig.js`.
- your cliff and rock kit v6 (`export/cliff-rock-kit-v6/`): `lib/plantIsoRig4.js`, `lib/plantIsoRig5.js`, `lib/treeIsoRig4.js`, `lib/pxRockIso2.js`, `pxCliff3.js`, `pxRockIso3.js`.
- `Art/doryIsoRig.js` and `Art/lobsterBoatIsoRig.js`.
- your village return (`hh-village-return/`): `houses-kit/Art/houseIsoRig.js`, `shop-building-kit/shopfrontRig.js`, `yard-landscaping-kit/yardIsoRig.js`.

The game has its own copies of some of these. Ship yours regardless: the Art desk compares them byte for byte. The camper rig is the one from our package, unchanged, so there is nothing to send for it.

**1. The north-east light and its cliff (`keyscene.stp_ne_light`). This goes first: the terrain lane waits on it.**
- A `scene.json` in your SCHEMA.md's KeySceneDef, like pass 1's.
- Its own NOTES section:
  - what it is for;
  - what is frozen;
  - the budgets;
  - the owner's calls, with your leans: the site, the tower (octagonal, or square as on PEI), keeper or automated, and the light (oil or battery).
- The Head, the plateau, the neck and the bar as numbers the terrain lane can build from, not as paint: outlines, heights, slopes, and the bar's line and width against the tide.
- How far the bar stands from the arrival route's 30 m capsule.
- PNG masters for its boards.

**2. Pass 2 (the east end): NOTES.** What it is, and what the game should take from it.

**3. Ginny's plot (`keyscene.stp_ginnys_plot`).**
- A `scene.json`.
- PNG masters. If full-size PNGs will not upload, say so in the README: with the rigs above, the bake page runs here and we render the masters ourselves.
- Still water: the game can now hold still water above the tide ([ADR 0046](https://github.com/apholmes7-bot/hiddenharbours/blob/8ae9c5afa3c364c82f311773e209fbbe98ccedbd/docs/adr/0046-still-water-above-the-tide.md)), but St Peters has no still-water map yet. Keep your frames as they are until the owner rules that call.

**4. The village square (pass 4): hold it.**
- Its y follows the village plan's stated reading: screen-north, ground × 0.643 (the Units line of [the village plan](https://github.com/apholmes7-bot/hiddenharbours/blob/8ae9c5afa3c364c82f311773e209fbbe98ccedbd/docs/design/st-peters-village-plan.md)).
- But the plan also quotes the game's own positions, which are in ground metres.
- The plan's authors will settle which reading holds. Do not redraw pass 4 until this branch says which. Its autumn master can wait with it.

**5. Pass 6, then the tidal crossing.** Pass 6 reached the owner on 28 Sept: the alder fall and the Bar Road Stop.
- The Bar Road Stop moves to Nine Mile Creek. Another conversation takes it on, working on copies, so leave its pass 6 files as they are and do no more on it here.
- The alder fall came as WebP views and code only. Send its scene.json, NOTES and PNG masters with item 1.
- Then the tidal crossing, after items 1 to 3.

## 5. Every return

Section 5 of STATE.md holds. It now also asks for three things return 1 did not always have:
- every rig a scene loads, inside the zip, or the file in the game it matches byte for byte, as a blob link;
- a PNG master for every board, with WebP only as an extra;
- positions in ground metres, with the reading stated in the file's `units`.

And NOTES for every pass you send: return 1 had none for passes 2 and 3.
