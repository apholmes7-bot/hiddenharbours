# Brief 2026-10-03: character kit 10.2, the send-back

From the Art desk, for the owner. First read [STATE.md](https://github.com/apholmes7-bot/hiddenharbours/blob/design-desk/STATE.md) on this branch.

Your character kit 10.2, the first on rig 10, arrived on 10-01 and passed on real Node. The game has taken it in: a change now in review bakes the player and the other nine cast presets from it, shaded smooth as your renders are, with no keyline. Rig 10 is not on the game's main branch yet, so this brief names the files and lines of your own kit. It covers:
- what is still open from the 9.2 send-back, [briefs/BRIEF-2026-09-30-character-9-2-send-back.md](https://github.com/apholmes7-bot/hiddenharbours/blob/design-desk/briefs/BRIEF-2026-09-30-character-9-2-send-back.md) (section 2);
- five new fixes for the next kit (section 3);
- the look-at's aim bar, the one number the game needs from you (section 4);
- the owner's rulings on your README's open items (section 5);
- what the game measured (section 6);
- how the next kit comes back (section 7).

Nothing in the game waits on it. Keep your other conversations' work first, in the owner's order.

## 1. What the game reads from 10.2 (keep these as they are)

- **The rig runs only when the game bakes,** in V8. No JavaScript ships. Every check's oracle is your rig run in V8, and every number the bake uses is read from the rig: the cell, the face marks' bands and their two cull floors, and the shading.
- **The names:** `characterIsoRig10.js` and `characterIsoRig10.poses.js`, the global `CharacterIso10`, the revision `'10.2'`, and `CAST10`.
- **The export:** your own `exportBuild(key)` and `gameplay(key)`.
- **The cell:** 80 × 104, pivot (40, 90), read from `W`, `H` and `pivot`.
- **The face:** all 13 groups bound into each preset's one mesh, and each point mark culled by its own band (`pt`, `az`, `sn`, `oh`). `brows.flat` binds no faces, as you meant: the fringe's edge is the flat brow.
- **The shading:** where the rig gives a face a smooth normal, the game lights the face by it.
- **No keyline:** `SHADING.keylineDefault` is false, and the game follows it (section 5).
- **Presets only, for now:** the bake takes the ten of `CAST10` and refuses any other key, your twenty new builds included (section 5).

## 2. Still open from the 9.2 send-back

Settled since then: the stale revision labels (its section 2, item 2), since 10.2 ships neither file; and an unknown preset name, which throws (`characterIsoRig10.js` l.1019). The rest stand. Each item names the 9.2 send-back's section.

1. **The README's missing sections** (its 2.1). There is still no section on the options file's format, the shading or the gameplay sidecar. README l.145's "the outline ruling (§4.6)" points into 10.0's README, which the kit does not ship. Put the sections in the next README, and point every cross-reference at a file the kit ships.
2. **A stale file name** (its 2.3). `Art/characterIsoRig10.poses.js` l.4 still says the solver is in `characterIsoRig8.js`. It is in `characterIsoRig10.js`.
3. **Export the dither, the tolerance and the ink** (its 3.1). Rig 10 exports none of the three. `INK` is internal (`characterIsoRig10.js` l.278: `#12181b` and `#243036`), and the game's baker still carries the standard 4×4 Bayer and 1e-6 itself. Export all three beside `SHADING`, `BLINK` and `LOOK`, with the aim bar (section 4), so the game reads them instead of copying them.
4. **Show a blink and a look-at** (its 3.2). README l.146: the renders draw no tools, blink or look-at. The game plays both from your data and checks them against your rig in V8, but it can compare them with your pictures by pixel only once you render them. The ask is the same as before: a strip of each as PNG masters, at 1× and 4×, with the bake page that draws them; the fisher at least, and every preset if it is cheap.
5. **Files written under emulation** (its 3.3, in part). 10.2 ships no reports, so no DIFFER line can mislead. Three files still differ when the Art desk regenerates them on real Node, and none says it was written under your emulation:
   - `golden-report.json`: every build's `sizes.bindBytes` (all 30, by −36 to +208 bytes) and the printed numbers of 35 check rows, noise near 1e-16 m and the budget's KB counts. No gate, pass or failure changes.
   - `builds/boy.v10.json` and `builds/paperboy.v10.json`: 11 numbers each, all at one frame of the rod socket `tool_R_1` in `strike`. Each is the same rotation written the other way (a quaternion's sign, or a half-turn written as 180° on one engine and −180° on the other), or a rounding at the 7th decimal. Nothing is wrong on screen.

   Nothing in the rig needs to change for this, and the Art desk keeps regenerating on real Node. The ask: say in the README which files your emulation wrote, and keep shipping every writer. Section 3, item 11 asks for the one change that makes the golden report print the same on any engine.
6. **The fisher's irises** (its section 4). They are still teal, `#2ba39a` (`EYES.sea`, `characterIsoRig10.js` l.278). The owner's question stands: is that intended? If it is, say so, and it stays. If it is not, name the colour you meant and change it in the next kit. The game never edits the rig.
7. **The steps between frames** (its 5.2). There are fewer large steps than in 9.2, but the rod sockets still turn 180.0° between frames 1 and 2, and between frames 4 and 5, of `cast`, `strike` and `land`. `mountUp` swings the right hip 179.3° between frames 9 and 10 (9.2's worst body joint was 171.6°, in `mountCabDown`).
   - Say whether a 180° socket turn may be taken either way, and whether the hip is meant to swing that far in one frame.
   - In the game, nothing follows a socket's rotation yet. The tool track is data, and a clip plays frame by frame without blending, so such a turn would first show when a prop hangs on the socket. Props carried in the hands are among the game's next follow-ups for the taller body, so this is worth settling, still at low priority.

## 3. Five new fixes

8. **The cell is tighter than the README says, and the sleeping figure runs past it.**
   - README l.122 says every build's `cell` fits with 4–5 px spare; the kit's own report gives 2 to 5 px.
   - Measured unclipped, the sleeping figure facing N runs past the cell's bottom edge, and your render cuts it there: 8 px on the deck boss, 5 on the lobsterman and 1 on the painter (and on 3 of 60 seeded random builds). The Fisher and the monger touch the edge.
   - The `cell` check exempts swim, sleep and tread on any build over 1.545 m (`characterIsoRig10.checks.js` l.175). That bar is 9.2's Fisher, and it covers 24 of your 30 builds.
   - The game cuts nothing: it pads each figure's drawing box by its measured reach (section 6). This is about your cell and your README.
   - Ask: a cell that holds the lying clips, or the check's bar set for the 10.x body; and the README's spare as measured.
9. **Stale texts in the checks file and the README.**
   - The `cell` check's title says "the 64 × 92 cell" (`characterIsoRig10.checks.js` l.174).
   - The `heights` detail still says "the Fisher is 1.522, rig 7 1.523" (l.268).
   - The cell check's note "cloth (skirt, apron) reaches the cell edge" (l.187) fires at sleep N on 19 presets, the Fisher in overalls among them. There it is the sleeping body at the edge, not cloth.
   - The README's heights are the rig's plus 1 cm on all 30 rows, 6 to 15 mm over. The Fisher's "1.79 m" (l.26) is designed at 1.782 m and stands 1.766 m in idle.
   - README l.67 says the twenty new builds use every beard. They use every beard but the mutton chops, which only the core deck boss wears.
   - README l.115 says elders draw salt, grey or white hair 3 in 4. They draw it 83% of the time.
10. **Random builds fail the gates, and the README names two cases.**
    - 23 of 60 seeded random builds fail at least one gate.
      - Face, 15: at S a face state draws the same pixels as the default, and at SE and SW one state reads the same as another.
      - Look, 9: 1 to 3 px see-through at the neck, collar or hood, with the head at a limit.
      - Light, 1: E and W, mirrored, differ by 1 px.
    - README section 10 names the long head at SW and one crotch pixel.
    - The ten presets the game bakes pass every gate but the girl's face at SW, which the README names.
    - The owner's ruling (section 5) has the game bake presets only for now. Its creator will check each build against your gates when it bakes it, so a failing build is refused, not shown.
    - Ask: name every failing class in the README, and fix what you can in the rig, so the creator can offer every option.
11. **The golden report's printed numbers depend on the engine.**
    - The game's editor runs your rig in ClearScript's V8. On the ten presets it reproduces every gated row's pass and flags: 189 of 190, the girl's face failing as the kit records.
    - It prints 3 values differently from Node 24, all residuals near 1e-16 m. The game holds such rows to the rig's own 1e-6 gate.
    - Ask: print residuals rounded, with `toFixed(7)` as the builds are, so any engine prints the report byte for byte.
12. **The render manifest lists no RGBA hashes.** 9.2's [renders/manifest.json](https://github.com/apholmes7-bot/hiddenharbours/blob/fe3a734e66554730cbade63b78b41c38ded20e7c/docs/art/rigs/character/rig9/renders/manifest.json) gave each render's RGBA hash, and the game's strips check reads them. 10.2's `renders/manifest.json` gives none, so that check still runs on 9.2 only; the Art desk held your renders to the rig by pixel instead. Ask: list the hashes again.

## 4. The look-at's aim bar

- The 9.2 send-back asked which aim is the contract (its 5.1). No answer came, and 10.2's README names no aim.
- The `look` check reports the aim (share 1, targets 2 m away inside the limits) but gates nothing on it. In 10.2 the head lands within 2.82° to 2.98° on every build but the seven elders, which reach 3.65° to 3.73°. In 9.2 it was 1.70°, and 2.40° for the skipper and Nan.
- Until you set a bar, the game holds each figure to your rig's own aim, figure by figure: its port is checked against your rig in V8.
- Ask: name the bar. Either one bar for every build, with any build outside it brought inside; or a bar for each build, written in the README with its reason. Say what moved the aim from 9.2's 1.70°. Then export the bar with the tolerance (section 2, item 3), so the game's check reads it from the rig.

## 5. The owner's rulings on your README's open items

Your README's section 10 carries four items from 10.0 (l.145). The owner has ruled two of them:
- **The outline (your §4.6): no keyline.** The game follows your rig and draws no keyline on characters (10-01; STATE section 4). Other rigs' keylines are untouched.
- **Fixture sizes for the 10.x body: the game's own work.** The owner ruled on 09-24 that the game's fixtures are resized to fit every body (STATE section 4), so nothing is asked of you. The game has named its own follow-ups for the taller body: the props carried in the hands, which hang lower now, and 8 doors in 7 interiors that stand lower than the Fisher.
- **Feet on St Peters' slopes, and outfit ramps matched to the terrain's,** stay yours, in your order. Nothing is asked of them now.

And one more ruling of 10-01:
- **Presets only, for now.** The game bakes the ten cast presets now, and your twenty new builds later, with the wardrobe. Its creator will check each build against your gates when it bakes it (section 3, item 10).

## 6. What the game measured (nothing asked)

1. **Your smooth shading, matched.** Against your 80 renders of the ten, standing, at 8 facings (`renders/1x/<preset>.idle8.png`, 31,782 drawn pixels), the game differs in 52 pixels: 13 drawn by one side only and 39 in colour, single pixels at the edges of faces. 46 of the 80 renders are pixel-exact, and the worst differs in 4. Lit flat, as before, the same renders differed in 4,778 pixels.
2. **The taller cast.** The ten stand 10 to 17% taller than in 9.2: the Fisher 1.51 → 1.77 m, the deck boss 1.60 → 1.86 m, the boy and the girl 1.14 → 1.26 m. At idle the Fisher is 51 px tall on screen (45 in 9.2). Villagers now look up to 1.63 m, the Fisher's head at rest (1.6332 m in your rig).
3. **Nothing is cut at the cell's edge.** The game pads each figure's drawing box by its reach, measured over every frame of every clip at every facing: 10.1 to 11.1 px below the cell, where the mount clips reach furthest, at most 2.3 px past either side, and never above. The old 1 px pad would have cut 707 to 1,092 pixels per figure, nearly all in the mount clips.

## 7. How the next kit comes back

- **One zip with a new name,** for example `HH-character-kit-10-3-<date>.zip`, following STATE section 5: a README with every changed number, old and new; SHA256SUMS; the renders as PNG masters.
- **A checker that runs on Node.** 10.2 ships its checks (`characterIsoRig10.checks.js`) and their golden report, but no script that runs them on Node; the Art desk ran them through its own harness. Ship one, on Node 18 with built-ins only, ending in one `RESULT:` line (STATE section 5).
- **The whole kit folder, as 10.2 came.** Start the README with what changed from 10.2, file by file.
- **Keep every file name, every global and the bake's anchors:** `characterIsoRig10.js`, `characterIsoRig10.poses.js`, `characterIsoRig10.checks.js`, `CharacterIso10`, `CAST10`, `exportBuild`, `gameplay`, `W`, `H` and `pivot`. A new export (the Bayer, the tolerance, the ink, the aim bar) is an addition: list each one in the README.
- **If the rig file itself changes, say so first.** The game then re-bakes the ten presets from it and checks them against your rig in V8.
