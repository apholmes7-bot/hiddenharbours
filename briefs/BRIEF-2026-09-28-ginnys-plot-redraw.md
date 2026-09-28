# Brief 2026-09-28: Ginny's plot, redrawn (St Peters key scenes)

From the Art desk, for the owner. First read [STATE.md](https://github.com/apholmes7-bot/hiddenharbours/blob/design-desk/STATE.md) on this branch.

This follows your return 3 (Ginny's cottage, then the camper). It covers:
- what happened to return 3;
- what the owner ruled;
- the one thing to draw next: Ginny's plot again, with the new cottage and the camper at the right facing.

## 1. Return 3, checked

- **Both zips arrived whole.** The cottage's SHA256SUMS-cottage.txt matches 50 of 50, and the camper's SHA256SUMS-camper.txt 45 of 45.
- **Real Node agrees with your runs.** `check-cottage.cjs` passed 43 of 43, and `check-camper.cjs` 49 of 49.
- **Checked here against the game:**
  - each zip's `game-45a09dd6/` is the game's bytes;
  - the re-stamped files are the game's with only your two stamps moved;
  - both sidecars are the game's apart from `kind` and the rig's stamp.
- **Your facing note holds for the houses too.** The game bakes a house the way it bakes the camper. Both rigs turn counter-clockwise, so cell k is drawn at the rig's dir (8 − k) mod 8: `DirForCell` in [RigBaker.cs](https://github.com/apholmes7-bot/hiddenharbours/blob/91ff97f2fad501e4470ca3faa54f2e2aa73d97b6/Assets/_Project/Code/Tools/Editor/RigBaking/RigBaker.cs), at line 99. The plot finds the cottage's facing from its door, through `FacingToward` in [StPetersGinnyPlot.cs](https://github.com/apholmes7-bot/hiddenharbours/blob/91ff97f2fad501e4470ca3faa54f2e2aa73d97b6/Assets/_Project/Code/App/Editor/StPetersGinnyPlot.cs). So your `d2`, door west, is the picture the game shows, in its cell 6.

## 2. The owner's rulings (28 Sept)

1. **The yellow cottage.** `ginnyCottage` stands as you dialled it: yellow, weather 0.45, green shutters, a red door and the five details.
2. **Yes, redraw Ginny's plot** (section 3).
3. **Both land in the game** as two pieces of work, the cottage first and then the camper. None of that needs anything from you.

## 3. Ginny's plot, redrawn

**Why.** Return 2's plot drew the camper at the rig's dir 2 (`ginnysPlot.js`, the `home.ginny_lot_camper` entry: `dir: 2`), with its door to the north, away from the cottage. The game parks it at its facing 2, which is the rig's dir 6: hitch east, door and step to the camera and the cottage. See `DoorFacing` in [StPetersCamperLot.cs](https://github.com/apholmes7-bot/hiddenharbours/blob/91ff97f2fad501e4470ca3faa54f2e2aa73d97b6/Assets/_Project/Code/App/Editor/StPetersCamperLot.cs), at line 464.

**What changes.**
- **The camper** is return 3's pass 2, lit through `coastalPass.js`, at the rig's dir 6. It is parked as your return 3 masters park it: `{variant:'clipper', awning:false, swing:0, weather:0.32, paint:null}`. If the plot's own weather (0.36 in return 2) is the one the game uses, say which, and why.
- **The cottage** is `ginnyCottage`, drawn with return 3's `houseIsoRig.js` at the rig's dir 2 (door west), as `sageCottage_then_ginnyCottage_d2.png` shows it.
- **The turned cottage is retired,** because the owner kept the door west. Drop the `plot_turned` master, the `wide_plot_1842_turned` board and the `building.stp_ginny_cottage_turned` entry. That leaves nine masters and fourteen boards.
- **At the camper's door.** The worn walk from the cottage, the chair and the boot rack already sit on the south side, where the door now is. Move anything that no longer fits, and let the camper's shade follow the camper.
- **`scene.json`:**
  - the camper's entry names both numbers, the rig's dir 6 and the game's facing 2;
  - the cottage's entry names `ginnyCottage`.
- **Recount the budgets and lit pools** (return 2's NOTES, part 2, section 3.6). At night the camper now lights its door lamp and its windows by room.

**What stays.**
- Everything else return 2 drew, including the frozen places, the two grounds, and the frames with their clocks and tides.
- Every ruling in section 2 of [the return 3 brief](https://github.com/apholmes7-bot/hiddenharbours/blob/design-desk/briefs/BRIEF-2026-09-28-key-scenes-return-3.md). The run and the Fen Pool stay dry ground. The sign is blank in the pictures, and the game letters "CODDLE".

**The rigs.** The bake page loads return 3's two rigs, and neither is in the game yet. Ship both in the zip, byte for byte as return 3 sent them. The checker holds their sha256 (LF):
- `houseIsoRig.js`: `62c45dc71aed70a629049a0e26414563454fbe834cf4b78d7de00fcdefee79ac`;
- `camperIsoRig.js`: `1b6ac817b37d8f3047b5273a2ec65310d9ed91b6ab05a29757d0a32aef5f3cc9`.

**Checks,** as before, plus:
- `facing`: the plot's camper meets your return 3 numbers: the hitch at (158.1, −10.8) px and the step at (2.6, 23.7) px from the pivot, as `Camper.json` quotes them.
- `pieces`: the camper and the cottage in the plot are drawn by those two rigs, with the options above.
- `changed`: each master against return 2's, showing where its pixels moved. Nothing should move outside the cottage, the camper, their shadows and their light.

## 4. Form

- **One zip,** with a new name and a sums file named for it (STATE.md, section 5).
- **If the masters will not fit in one upload,** send the scene first and the masters second, as return 2 did, each with its own sums file.
- **The masters as before:** four PNG tiles each, `tools/stitchMasters.cjs`, and `boards/PIXELS.txt`.
- **Not in this brief:** the village square (still held) and your two extra places.
