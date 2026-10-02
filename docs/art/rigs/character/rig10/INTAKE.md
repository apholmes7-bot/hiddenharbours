# Rig 10 (character v10.2): intake record

Landed 2026-10-02 by the art-pipeline lane: the character rig 10 intake (charter
`HANDOFF-2026-10-01-character-rig-10-intake.md`). This folder is Claude Design's kit **as delivered**,
except for 3 generated files that real Node writes differently (below). Nothing in the rig was edited.
Rig 9's folder (`../rig9/`) is unchanged and stays as 9.2's record. Rig 10 is registered beside it as
catalog key `characterRig10`. Until the intake's Phase B, the editor still bakes rig 9
(`CharacterSkinAssetBaker.LiveRig`).

| | |
|---|---|
| Drop | `C:/hh-drops/character-rig-kit-v10.2.zip`, 8,416,055 bytes, sha256 `79dcf1ab92db84ad8433c046bb82597944167e1be3b67395739ed06cdd0aaae5` |
| Kit folder in the zip | `export/character-v10.2-kit/`, 195 files |
| On arrival | `SHA256SUMS.txt` lists 194 files: 194 ok, 0 differ, 0 missing, nothing unlisted but itself |
| Rig | `Art/characterIsoRig10.js`, rev 10.2, pass 10, global `CharacterIso10`, sha256 (LF) `cbd50b54001d504d711a6a2e96ea01b1843a12e8b30665c9002e6ce45a7ed814` |
| Pose library | `Art/characterIsoRig10.poses.js`, sha256 (LF) `89c7c902d3575e9ea905aade6c43a26c91bc29653467f62fdd80fb9e2713f820` |
| Checks | `Art/characterIsoRig10.checks.js`, sha256 (LF) `9b25859906baf75f076eae6fde1b5f2210330e00e1306f3640854043b577ad55`. Optional; loaded after the poses |
| Frozen 10.1 | `Art/characterIsoRig10_1.js` and `_1.poses.js` define `CharacterIso10_1`, for the review page only. The game loads neither |
| Added by the intake | `INTAKE.md` (this file), `INTAKE.SHA256SUMS.txt`, and the Art desk's harness `node_check_char10_2.cjs` (sha256 `1e33d7879c2822f10e5c6a736959c84da84a0eb2ac8e9394d7e00ecb76a68dea`, as in `C:/hh-drops/character-rig-kit-v10.2-desk/`) |

## The 3 files Node regenerated

As for 9.2 (#889), the committed copy of a generated file is Node's, not the drop's. The Art desk's
harness named these three; on real Node (v24.19.0) the regeneration changes exactly them:

| File | Why the drop's copy differs | How big the difference is |
|---|---|---|
| `builds/boy.v10.json` | Not written by Node. `exportBuild('boy')` on Node writes 11 numbers differently, all at one frame of the rod socket `tool_R_1` in `strike` (`clips[9]`): the same rotation written the other way (a quaternion's sign, and 180° for −180°), plus rounding at the 7th decimal | 11 of 140,812 numbers. 1,411,142 → 1,411,145 bytes. sha256 `608b2fbed247907266ac9b299268407b45ec5749ec78e9f9f67f1ce4bae931ae` → `322d52033af0f9e6b139cbd749ee49013596d35dcd7a111737fdf1d2ef29fb25` |
| `builds/paperboy.v10.json` | The same, at the same frame | 11 of 140,668 numbers. 1,409,419 → 1,409,422 bytes. sha256 `5e8d4f3198d40c07994309d9c1e85d0c0c43e9b506fc43ec9933e12789c77bcc` → `32d154e8d85851b3fae1012b400f184d7ab84d4d7b378e463ed32453c3e11390` |
| `golden-report.json` | Written under Claude Design's emulation, unlabelled. On Node, every build's `sizes.bindBytes` differs (all 30, by −36 to +208 B), and so do the printed numbers of 35 check rows: noise near 1e-16 m and the budget's KB counts (budget on 9 builds, handoffs 15, rock 9, loops 1; the monger's `roundtrip` worst falls on another frame). No gate, pass or failure changes: 569 / 570 gated, the one failure the girl's `face` at 7 / 8 facings | 72 of 5,330 lines. 318,234 → 318,314 bytes. sha256 `6a7e42df3bdd1d19b648346851271051cad79f59e95753ad14add6456aaef976` → `f1841dccfae7a34a2c94e6f2890d100b749c520eedd9d5c8a4197b7fd325f9b5` |

The regeneration wrote each build as the kit writes the other 28 (`exportBuild(key)` under the kit's
header, every number `toFixed(7)`), and the golden report as the kit lays it out (`runChecks` and
`sizes` per build, no row tables, `JSON.stringify(report, null, 1)`). Node writes the other 28 builds
byte for byte as delivered.

`sha256sum -c SHA256SUMS.txt` in this folder gives 191 OK and 3 FAILED, and the 3 are exactly the
files above. `sha256sum -c INTAKE.SHA256SUMS.txt` gives 3 OK. Between them, the two lists pin every
file of the kit.

## What was checked (no Unity)

| Check | Engine | Result |
|---|---|---|
| Zip hash and `SHA256SUMS.txt` | sha256sum | 194 / 194 on arrival |
| The Art desk's harness on the unzipped drop | Node v24.19.0 | `RESULT: PASS`, every line as the desk's record but the timings |
| The same harness on this folder as committed | Node v24.19.0 | `RESULT: PASS`. Stamps 65 / 65. Golden 569 / 570 gated, as the kit; rows equal to this folder's report 600 / 600, sizes 0 differ. Builds 30 / 30 byte for byte with `toFixed(7)`. Gameplay 30 / 30 byte for byte. Data and presets equal. Renders 60 / 60 by pixel. Sections G to L as the desk's record |

## Line endings

`.gitattributes` pins this folder to LF by extension (`js cjs json txt md html`), and the 121 PNGs stay
in Git LFS. The kit's `SHA256SUMS.txt` is taken over LF bytes (the kit's 75 text files carry no CR), and
the baker hashes the rig the same way (`CharacterSkinExtractor.LfSha256`), so a Windows checkout does
not move a pin.

## Re-running the checker

The harness is read-only on the kit and writes only into its output folder. From the repository root:

```bash
R9=docs/art/rigs/character/rig9/Art RC=60 node docs/art/rigs/character/rig10/node_check_char10_2.cjs docs/art/rigs/character/rig10 "$TMP/rig10-check"
```

Pass both folders and `R9`: the defaults are the Art desk's own paths, and where `R9` holds no
`characterIsoRig9.js` the harness skips section K (against 9.2) without a word. It should end
`RESULT: PASS (stamps, golden, exports, gameplay, data, renders, random, claims, steps, frozen, vs92,
cellcut)`. It took about 3 minutes here; the desk measured about 12. Section B compares
the golden checks with this folder's `golden-report.json`, so on this folder it reports 600 / 600 rows
equal and no size differences (on the drop's copy: 565 / 600 and 30).

## Findings for Claude Design (through the owner)

The rig is not edited here. The send-back waits until the intake lane reports (owner, 10-01); this list
is its first draft. Each item goes back with the evidence named. Items 1 to 7 are the 09-30 send-back's,
checked by the Art desk against 10.2.

1. **Missing README sections** (09-30 §2.1, not done). There is still no section on the options file's
   format, the shading or the gameplay sidecar. README l.145's "the outline ruling (§4.6)" points into
   10.0's README, which the kit does not ship.
2. **Stale file name** (09-30 §2.3, not done). `Art/characterIsoRig10.poses.js` l.4 still says the
   solver is in `characterIsoRig8.js`.
3. **No dither, tolerance or ink export** (09-30 §3.1, not done). The API has none of the three, and
   `INK` is internal (`characterIsoRig10.js` l.278, `#12181b`, `#243036`). The baker carries the
   standard 4×4 Bayer and 1e-6 itself.
4. **No blink or look-at in the renders** (09-30 §3.2, not done). README l.146: the renders draw no
   tools, blink or look-at.
5. **Files written under emulation** (09-30 §3.3, in part). No reports ship, but the golden report's
   bind bytes and its 1e-16 values are emulation numbers, unlabelled, and the boy's and the paperboy's
   builds differ from Node's at one frame of `strike` (above). Ask: write the kit's files on real Node.
6. **The fisher's irises** (09-30 §4, no answer). They are still teal, `#2ba39a` (`EYES.sea`, l.278).
7. **The aim bar and the steps** (09-30 §5.1 and §5.2, no answer). The look-at now lands within 2.82° to
   2.98° on every build but the seven elders, which reach 3.65° to 3.73° (9.2: 1.70°, and 2.40° for the
   skipper and Nan). The rod sockets still turn 180.0° between frames 1 and 2 and 4 and 5 of `cast`,
   `strike` and `land`, and `mountUp` swings the right hip 179.3° between frames 9 and 10 (9.2's worst
   body joint was 171.6°, `mountCabDown`). Ask: name an aim bar, and say whether a 180° socket turn
   may be taken either way.
8. **The cell is tighter than the README says, and the lying clips run past it.** README l.122 says
   every build's cell fits with 4–5 px spare; the kit's own report gives 2 to 5 px. Measured unclipped,
   the sleeping figure facing N runs past the cell's bottom edge, and the render cuts it there: 8 px on
   the deck boss, 5 on the lobsterman, 1 on the painter; the Fisher and the monger touch the edge.
   The `cell` check exempts swim, sleep and tread on any build over 1.545 m, which is 9.2's Fisher and
   covers 24 of the 30 in 10.2. Ask: a cell that holds the lying clips, or a bar set for the 10.x body.
9. **Stale texts in the checks file and the README.** The `cell` check's title says "the 64 × 92 cell"
   (`characterIsoRig10.checks.js` l.174). The `heights` detail still says "the Fisher is 1.522, rig 7
   1.523" (l.268). The cell check's note "cloth (skirt, apron) reaches the cell edge" fires at sleep N
   on 19 presets, the Fisher in overalls among them: there it is the sleeping body at the edge. The
   README's heights are the rig's plus 1 cm on all 30 rows, 6 to 15 mm over (the Fisher "1.79 m" is
   designed at 1.782 m and stands 1.766 m in idle). README l.67: the twenty new builds use every beard
   but the mutton chops (only the core deck boss wears those). README l.115: elders draw salt, grey or
   white hair 83% of the time, not 3 in 4.
10. **Random builds fail the gates; the README names two cases.** 23 of 60 seeded random builds fail at
    least one gate (face 15, look 9, light 1). README §10 names the long head at SW and one crotch
    pixel. The ten presets the game bakes pass every gate but the girl's face at SW, which the README
    names. The owner's ruling K3 has the game's creator gate each build at bake.

**Notes, not defects:**

- An unknown preset name now throws (`no preset "…"`), as the 09-30 send-back asked. An unknown field
  still falls back to the Fisher's.
- The body is about 17% taller: the Fisher's idle crown goes from 1.506 to 1.766 m and his collider
  radius from 0.20 to 0.186 m. That feeds the fixture resize, which Claude Design also carries as open
  ("fixture sizes for the 10.x body", README l.145).
- `SHADING.keylineDefault` is false: 10.x draws no keyline unless asked. The game follows the rig and
  draws no keyline on characters (owner ruling K4, 10-01).
- `brows.flat` binds no faces, on purpose (`FACE_EMPTY`: the fringe's edge is the flat brow).
