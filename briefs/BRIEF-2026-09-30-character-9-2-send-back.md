# Brief 2026-09-30: character kit 9.2, the send-back

From the Art desk, for the owner. First read [STATE.md](https://github.com/apholmes7-bot/hiddenharbours/blob/design-desk/STATE.md) on this branch.

Your character kit 9.2 is in the game: [docs/art/rigs/character/rig9](https://github.com/apholmes7-bot/hiddenharbours/tree/266d35d2a7a479ad696f5dfa55bbc96f3d558cd2/docs/art/rigs/character/rig9), with its intake record, [INTAKE.md](https://github.com/apholmes7-bot/hiddenharbours/blob/266d35d2a7a479ad696f5dfa55bbc96f3d558cd2/docs/art/rigs/character/rig9/INTAKE.md). The game builds the player and the ten cast presets from it. A change now in review plays its face, blink and look-at, and the wheel, oar and carry clips. This brief sends back what the intake and that work found in the kit. It covers:
- six fixes for the next kit (sections 2 and 3);
- the owner's question on the fisher's irises (section 4);
- two things the game measured: the look-at's aim bar, and the steps between frames (section 5);
- how the next kit comes back (section 6).

Nothing in the game waits on it. Keep your other conversations' work first, in the owner's order.

## 1. What the game reads from 9.2 (keep these as they are)

- **The rig runs only when the game bakes,** in V8. No JavaScript ships. Every check's oracle is your rig run in V8, not the kit's JSON.
- **The face:** all 13 groups of `FACE_SLOTS` are bound into each preset's one mesh (eyes open, half, shut, wide, left and right; brows flat, up and knit; mouth flat, open, grit and smile), and each frame shows its own face.
- **Blink,** from `BLINK`: half 40 ms, shut 80 ms, half 40 ms; waits of 2.4 to 6 s; a double blink at chance 0.15, 0.12 s apart. Each figure is seeded on its own.
- **Look-at,** from `LOOK` and `lookAt`. The game's port matches your rig in V8 over 24,960 results, the worst 0.001° apart. Villagers and skippers look at the player within 5 m; the player looks at nothing (the owner's ruling of 09-27).
- **The wheel, the oars and the pail:** `idle_helm`, `walk_helm`, `idle_oars`, `walk_oars`, and the `buckets` clips.
- **Your shading,** from `SHADING` and `ROLE`: edge 0.12, the keyline `#101a19` mixed 22%, the face thresholds, and the head snap.

## 2. Three fixes to the text

1. **The README's missing sections.** [README.md](https://github.com/apholmes7-bot/hiddenharbours/blob/266d35d2a7a479ad696f5dfa55bbc96f3d558cd2/docs/art/rigs/character/rig9/README.md) l.9 says the 9.1 README's sections on the rig 7 comparison, the options file format, shading and the sidecar format still hold. The kit does not ship the 9.1 README. Put those four sections in the next README, or ship the 9.1 README beside it.
2. **Stale revision labels.** [tools/compare-rig7.cjs](https://github.com/apholmes7-bot/hiddenharbours/blob/266d35d2a7a479ad696f5dfa55bbc96f3d558cd2/docs/art/rigs/character/rig9/tools/compare-rig7.cjs) l.140 and [reports/rig7.txt](https://github.com/apholmes7-bot/hiddenharbours/blob/266d35d2a7a479ad696f5dfa55bbc96f3d558cd2/docs/art/rigs/character/rig9/reports/rig7.txt) l.1 call the rig "characterIsoRig9 9.1". The rig reports 9.2.
3. **A stale file name.** [Art/characterIsoRig9.poses.js](https://github.com/apholmes7-bot/hiddenharbours/blob/266d35d2a7a479ad696f5dfa55bbc96f3d558cd2/docs/art/rigs/character/rig9/Art/characterIsoRig9.poses.js) l.4 says the solver is in `characterIsoRig8.js`. It is in `characterIsoRig9.js`.

## 3. Three fixes to what the kit exports and shows

1. **Export the dither, the tolerance and the ink.**
   - The rig exports no Bayer matrix and no tolerance, so the game's baker carries the standard 4×4 Bayer and 1e-6 itself.
   - It does not export its ink either (`INK`, [characterIsoRig9.js](https://github.com/apholmes7-bot/hiddenharbours/blob/266d35d2a7a479ad696f5dfa55bbc96f3d558cd2/docs/art/rigs/character/rig9/Art/characterIsoRig9.js) l.227), though the options file makes the brow from it: `brow = mix(hair ramp [0], ink, 0.34)`. The intake had to search for the ink that gives all nine hair options their brow, and found one, `#12181b`, which is `INK[0]`.
   - Export all three beside `SHADING`, `BLINK` and `LOOK`, so the game reads them instead of copying or searching for them.
   - The keyline and the ink are two colours: `SHADING`'s keyline is `#101a19`, and `INK[0]` is `#12181b`. If one is meant to be the other, say so.
2. **Show a blink and a look-at.** Blink and look-at ship as data, and `check-look` passes, but none of the 120 renders shows either; they cover idle, walk and dig (your README section 12 says so).
   - Render a strip of each as PNG masters, at 1× and 4×, with the bake page that draws them: the blink's frames in order, and the head turned to each limit and to a few targets inside them.
   - The fisher at least; every preset if it is cheap.
   - The game shoots the same frames and compares them with yours by pixel. That is how the owner's ruling of 09-24 is checked: blink and gaze come from you, and the engine plays them.
3. **Files written under emulation.** The builds carry last-bit noise against real Node, the numbers are not printed the shortest way that round-trips, and `reports/shading.txt` and `reports/worldfit.txt` say the data files differ from their regeneration. On real Node both regenerate byte for byte.
   - This is your emulated Node (STATE section 5), so nothing in the rig needs to change. The Art desk regenerates the files on real Node; keep shipping every writer.
   - One ask: where a report compares a file with a regeneration made under your emulation, say so in the report, so its DIFFER line is not read as real.

Seen, nothing asked: an unknown preset name still becomes the Fisher, labelled "Custom", with no error (`normBuild`, l.340). The game's baker checks the name first, so this is handled here. A strict mode or an error in the rig would be welcome, but it is low priority.

## 4. The owner's question: the fisher's irises

The fisher's irises are teal: `#2ba39a`, `EYES.sea`. Is that intended?
- If it is, say so, and it stays.
- If it is not, name the colour you meant, and change it in the next kit. The game never edits the rig.

## 5. Two things the game measured

1. **The look-at's aim bar.**
   - [README.md](https://github.com/apholmes7-bot/hiddenharbours/blob/266d35d2a7a479ad696f5dfa55bbc96f3d558cd2/docs/art/rigs/character/rig9/README.md) section 5 (l.114) says that with share 1 the head's +y lands within 1.7° of targets 2 m away inside the limits, and [reports/look.txt](https://github.com/apholmes7-bot/hiddenharbours/blob/266d35d2a7a479ad696f5dfa55bbc96f3d558cd2/docs/art/rigs/character/rig9/reports/look.txt) l.6 says 1.70°.
   - [golden-report.json](https://github.com/apholmes7-bot/hiddenharbours/blob/266d35d2a7a479ad696f5dfa55bbc96f3d558cd2/docs/art/rigs/character/rig9/golden-report.json) gives 1.70° for eight presets but 2.40° for the skipper and nan. Their worst target is at bearing −50°, +0.25 m; the other eight's is at bearing 50°, −0.35 m.
   - The game's port lands within 0.0031° of your rig and finds the same: 1.695° to 1.701° for the eight, and 2.397° and 2.396° for the skipper and nan. So the skipper and nan miss the README's 1.7°, and the eight sit right at it.
   - Say which is the contract. Either 1.7° for every preset, with the skipper and nan brought inside it; or a bar for each preset, written in the README with its reason. Then export the bar with the tolerance (section 3, item 1), so the game's check reads it from the rig.
2. **The steps between frames.**
   - The game plays your clips frame by frame and never blends them.
   - Its bake check measured 13,365 steps of a bone between adjacent frames. 403 of them turn more than 90°, and the worst is 180.0°: `tool_R_1` in `cast`, from frame 1 to frame 2 (`tool_L_1` turns 180.0° there too).
   - Nothing is wrong on screen, because nothing is blended. Say whether the clips are meant to step like this. For the cast, say whether the 180° is the rod swung through or the socket turned the long way. If the socket turned the long way, take the short way in the next kit, so a tool that follows the socket does not spin. This is low priority.

## 6. How the next kit comes back

- **One zip with a new name,** for example `HH-character-kit-9-3-<date>.zip`, following STATE section 5: a README with every changed number, old and new; SHA256SUMS; the kit's checkers, each ending in one `RESULT:` line; and the renders as PNG masters.
- **The whole kit folder, as 9.2 came.** Start the README with what changed from 9.2, file by file.
- **Keep every file name, every global and the bake's anchors.** The game's bake finds the rig by its file name. A new export (the Bayer, the tolerance, the ink, the aim bar) is an addition: list each one in the README.
- **If the rig file itself changes, say so first.** The game then re-bakes the ten presets from it and checks them against your rig in V8.
