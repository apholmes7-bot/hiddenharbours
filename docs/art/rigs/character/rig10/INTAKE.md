# Rig 10 (character v10.2): intake record

Landed 2026-10-02 by the art-pipeline lane: the character rig 10 intake (charter
`HANDOFF-2026-10-01-character-rig-10-intake.md`). This folder is Claude Design's kit **as delivered**,
except for 3 generated files that real Node writes differently (below). Nothing in the rig was edited.
Rig 9's folder (`../rig9/`) is unchanged and stays as 9.2's record. Rig 10 is registered beside it as
catalog key `characterRig10`. Since the intake's Phase B (below), the editor bakes rig 10
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

## Phase B: the bake and the switch (2026-10-02)

All ten committed skins (`Assets/_Project/Data/Characters/Skin/<preset>.asset`, the player and the
nine cast) were re-baked from rig 10 in one headless run. Each keeps its GUID, id and switch states. Each
now pins `characterIsoRig10.js` and `characterIsoRig10.poses.js` at the hashes above and records revision
10.2 and tone rule V9. Each carries 28 bones, 53 clips and a bind mesh of 519 to 713 faces (2,119 to 2,852
corners), binds the 13 face groups, and paints 20 to 24 of the 32 colour slots that V9 allows. Two changes
came with the switch:

- **Smooth shading.** Where rig 10 gives a face a smooth normal, the bake stores it in UV2, every pose
  skins it, and the facet shader lights the face by it. Against the kit's own renders
  (`renders/1x/<preset>.idle8.png`: idle's first frame at 8 facings, 80 renders, 31,782 drawn pixels),
  the game differs in 52 pixels: 13 drawn by one side only and 39 in colour. 46 of the 80 renders are
  pixel-exact, and the worst differs in 4. Lit flat, as Phase A's port was, the same renders differ in
  4,778 pixels and none is exact.
- **The overlay covers the figure's reach.** Each skin records `ReachPx`: how far its drawing passes
  the 80 × 104 cell on each side, over every honest frame of every clip at every facing. The ashore
  overlay pads each side by it, and the cell is unchanged. All ten reach 10.1 to 11.1 px below the cell
  (the mount clips: `mountUp`, `mountDown`, `mountCab`, `mountCabDown`), at most 2.3 px past either
  side, and never above. Every clip, frame and facing was shot through an overlay opened 15 px all
  round, and no drawn pixel passed the measured reach. The old 1 px pad would have cut 707 to 1,092
  pixels per figure past the cell, nearly all in the mount clips; the measured pads cut none.

The look target height moved with the switch from 1.31 to 1.63 m (`CharacterLookTargetHeightMetres`):
rig 10's fisher's head at rest, 1.6332 m. The plates were shot in the same slot. They are evidence and
are not committed; the PR carries their numbers. Rig 9 stays in the catalog as `characterRig9`. To go
back, set `LiveRig` to `CharacterRigKit.Rig9.CatalogKey` and re-bake.

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

## The game's port (Phase A, no Unity)

The editor reads rig 10 with the same reader as 9.2, one kit per script host:
`CharacterRigKit` names each kit's files, global, revision, preset table and face rules, and
`CharacterSkinExtractor.Load9(host, CharacterRigKit.Rig10)` loads this folder. Phase A left the editor
baking rig 9; Phase B (above) moved `CharacterSkinAssetBaker.LiveRig` to rig 10 and re-baked the ten.

| What changes | 9.2 | 10.2 | Where the game takes it |
|---|---|---|---|
| Names | `characterIsoRig9`, `CharacterIso9`, `'9.2'`, `CAST` | `characterIsoRig10`, `CharacterIso10`, `'10.2'`, `CAST10` | `CharacterRigKit.Rig10`; catalog key `characterRig10` |
| The export | `_sidecarExport.js` and `fit.js` | the rig's own `exportBuild(key)` and `gameplay(key)` | `CharacterSkinExtractor` |
| The cell | 64 × 92, pivot (32, 82) | 80 × 104, pivot (40, 90) | read from the rig (`W`, `H`, `pivot`) into the def's `CellW`, `CellH`, `PivotPx` |
| The face marks | culled by role (`ROLE`, `minT`) | `minT` 0; a point mark culls by its own band (`pt`, `az`, `sn`, `oh`) | the band in the mesh's UV1.w, `sn` and flags in UV2; the def's `FaceMarkAzFloor` and `FaceMarkEdge`, read from the rig's source; the facet shader culls a mark by its band |
| The keyline | drawn | not drawn unless asked (`keylineDefault: false`) | the def's `KeylineDefault`; the figure is inked by its edge without the ring (owner ruling K4). Other rigs' keylines are untouched |
| Unknown preset | throws | throws | the bake takes the ten of `CAST10` and refuses any other key, the twenty NPCs included (presets only, owner 10-01) |

**The guards.** 9.2's bake guards became bodies that take the rig they read. Their `V9_` tests run them
on 9.2 as before, and the `V10_` tests in `CharacterSkinBakeGuardTests.V10.cs` run the same bodies on
rig 10: the composition, the material limit, the replay of every clip and frame, the clips, the face
and tool tracks, the blink, the look (four ways) and the ink against the rig's own render. Two 9.2
guards stay 9.2-only because they read 9.2's `ROLE`; rig 10 has its own pair, on the faces' own `az`
and on the rig's shading with no keyline. None retired. The strips check stays 9.2's: this kit's render
manifest lists no RGBA hashes, so the harness held its 60 renders to the rig by pixel at landing.

**Every number from the rig.** The cell, the bands, the two floors of the marks' cull (the rig's
`1e-6` and its pixel-edge `1e-4`) and the shading are read from the rig; the guards read them again
from the rig's source text, never from the code they test.

## Measured for the switch (no Unity)

**The body (the rig's own `gameplay(key)`, 9.2 against 10.2).** The work contracts (helm, oars, rail,
ladder, seat, bed, reach: 37 values) are the same in both; the body standing in them is not.

| Preset | Height 9.2 → 10.2 (m) | Change | Drawn at idle 9.2 → 10.2 (px) | Change | Collider radius 9.2 → 10.2 (m) | Change |
|---|---:|---:|---:|---:|---:|---:|
| fisher | 1.5057 → 1.7662 | +17.3% | 45.2 → 51.0 | +12.7% | 0.200 → 0.186 | −7.0% |
| ginny | 1.4739 → 1.6483 | +11.8% | 44.8 → 47.5 | +6.1% | 0.172 → 0.156 | −9.3% |
| skipper | 1.4881 → 1.6407 | +10.3% | 44.8 → 47.4 | +5.7% | 0.210 → 0.201 | −4.3% |
| nan | 1.3572 → 1.5138 | +11.5% | 41.1 → 43.5 | +5.8% | 0.179 → 0.168 | −6.1% |
| deckboss | 1.5981 → 1.8641 | +16.6% | 48.2 → 53.7 | +11.4% | 0.229 → 0.230 | +0.4% |
| packer | 1.4517 → 1.6233 | +11.8% | 44.0 → 47.0 | +6.6% | 0.178 → 0.161 | −9.6% |
| cutter | 1.3176 → 1.4916 | +13.2% | 39.9 → 43.0 | +7.7% | 0.157 → 0.142 | −9.6% |
| hand | 1.4477 → 1.6768 | +15.8% | 43.4 → 48.1 | +10.8% | 0.174 → 0.156 | −10.3% |
| boy | 1.1393 → 1.2570 | +10.3% | 35.8 → 37.5 | +4.9% | 0.158 → 0.147 | −7.0% |
| girl | 1.1367 → 1.2623 | +11.0% | 35.3 → 37.3 | +5.5% | 0.144 → 0.145 | +0.7% |

Height is `figure.height_m`, the crown in idle. "Drawn" is idle's first frame through the rig's own
`proj`, top to bottom of the picture, the tallest of the 8 facings. Both rigs draw 32 px a metre at
the same 40° camera, and the picture also holds the footprint's depth, which barely changes, so the
picture grows less than the standing height. What grows by about 17% is the Fisher's and the deck
boss's standing height (+0.26 and +0.27 m), most of it in the legs: the Fisher's hip goes from 0.6045
to 0.9338 m (+54%) and the hanging hands from 0.5738 to 0.7993 m (+39%). The footprint narrows a little
(0.40 × 0.36 to 0.372 × 0.36 m).

**What pins 9.2's body today.** What the switch moves, and whether it holds on rig 10:

- **The switch's own branches.** The bake (`CharacterSkinAssetBaker.cs` l.663) and four guard tests
  read `LiveRigIsV9` as "rig 9, else rig 7": `CharacterSkinBakeGuardTests`'
  `EveryCommittedSkinDef_PinsTheRigsAsTheyAreToday` and
  `TheCommittedBindMeshIsTheFaceTheChainComposesToday`, `CharacterSkinCastBakeTests`'
  `ThePlayersComposedTableIsTheCommittedDefsTable` and `EveryCastStateIsAClipTheRigBakes`. With
  `LiveRig` on rig 10 they would take rig 7's branch, so Phase B taught them rig 10 before it moved
  `LiveRig`.
- **The helm and the oars.** The contracts are unchanged (the wheel 0.655 m up and 0.315 m ahead, the
  seat 0.4 m). The intro's S7 (#916, not on main yet) holds Armand's drawn ankles (`foot_L`,
  `foot_R`) within 0.05 m of his helm station across the deck. Armand is the skipper preset. On 9.2
  his ankle midpoint sits 7.2 mm off his origin in `idle` (S7 measured 7 mm). On 10.2 it is 9.8 mm in
  `idle`, 0 in `helm_idle` and 32 mm in `helm_walk` (9.2: 24.3 mm). All inside the bar; the ankles
  stay 0.075 m up.
- **Doors.** `BoatInteriorDef.ClearHeightMeters` is read only by the editor spike
  `BoatInteriorExtruder`; the game's door threshold reads the width alone, so no door stops anyone.
  On 9.2 every door cleared every figure (lowest door 1.6 m, tallest figure 1.598 m). On 10.2 the
  Fisher (1.766 m) stands taller than the doors of `CapeIslanderIso` (1.6), `SportFisherSkybridgeIso`
  (1.65 and 1.75), the Fundy inshore lobster boats, hardtop and open (1.696), the Northumberland ones
  (1.74) and `SportFisherConvertibleIso` (1.75). The deck boss (1.864 m) also outgrows the
  Newfoundland inshore boats (1.784), `SternTrawlerIso` and `SternTrawlerMk2Iso` (1.8) and
  `LobsterBoatIso` (1.84). The rest (1.9 to 2.235 m) clear everyone.
- **Bunks.** No bunk length is recorded anywhere; sleep lies at the rig's bed height (0.3 m in both).
  Lying length in `sleep`'s first frame: the Fisher 1.533 → 1.791 m, the deck boss 1.608 → 1.866 m.
- **Where villagers look.** `GameConfig.DefaultCharacterLookTargetHeightMetres` is 1.31 m, rig 9's
  Fisher head point rounded (its comment says 1.3135 m; 9.2 measures 1.3151 m), and
  `CharacterFigureLife` aims looks at it. 10.2's Fisher head point is 1.6332 m. No test ties the
  number to the rig, so after the switch villagers would look about 0.32 m low until it is retuned
  (`GameConfig.asset`, the owner's to tune). Phase B retuned it to 1.63 m with the switch.
- **Walkers' lights.** `WalkerLights.HeadlampLiftMetres` (1.55 m, "her brow") and `LanternLiftMetres`
  (1.05 m, set for a 1.7 m figure) are fixed heights. The Fisher's crown goes from 1.506 to 1.766 m,
  so the headlamp, which rode just above 9.2's crown, sits about 0.22 m under 10.2's.
- **Carried things.** `CarryHands.Pose` hangs props from `FisherCarryAnchors`, sprite-era points by
  gait and frame. The rig's hands rise from 0.574 to 0.799 m, so props would hang low on rig 10 until
  the anchors follow the hands.
- **Plates.** `AshoreFigurePlatePlayTests` (`HerBox`, ±0.7 m across, −0.4 to +2.2 m up; its overlay
  check reads the def's own cell) and `VillagersAshorePlatePlayTests` (±0.9 m, −0.5 to +2.6 m) hold
  the taller figures as they stand.
- **Not tied to the rig's body:** the walkers' 0.35 m foot collider (sprite-era, not the rig's
  radius), the speech bubble at 2.1 m (above the deck boss's 1.864 m), the swim and wade waterlines
  (sprite-only), the synthetic test fixtures, the pre-rig sprite pins, and the guards that pin 9.2 on
  purpose.

**The lying clips against the 80 × 104 cell.** Every frame of `swim` (8), `tread` (6) and `sleep` (6)
at 8 facings, the posed mesh through the rig's own `proj`, unclipped. Reach past the cell's edge, worst
case (negative is room left):

| Preset | swim | tread | sleep | sleep's pixels past the cell | past the game's overlay (cell + 1 px) |
|---|---:|---:|---:|---:|---:|
| fisher | −4.39 | −6.82 | +0.37 (bottom, f0 N) | 0 | 0 |
| ginny | −5.49 | −7.26 | −1.14 | 0 | 0 |
| skipper | −5.49 | −6.52 | −1.43 | 0 | 0 |
| nan | −6.37 | −6.87 | −2.70 | 0 | 0 |
| deckboss | −3.32 | −6.47 | +1.69 (bottom, f0 N) | 8 | 1 |
| packer | −5.57 | −6.63 | −1.39 | 0 | 0 |
| cutter | −6.53 | −7.16 | −2.70 | 0 | 0 |
| hand | −5.13 | −6.97 | −0.51 | 0 | 0 |
| boy | −8.96 | −6.88 | −6.58 | 0 | 0 |
| girl | −9.04 | −7.17 | −6.62 | 0 | 0 |

On 9.2's 64 × 92 cell, every lying clip of the ten fits (the closest is the deck boss's sleep, 0.99 px
of room). Today only the player (the Fisher) plays the lying clips (`PlayerSleepPresenter`,
`PlayerSwimAnimator`). In Phase A the ashore figure drew inside its cell padded 1 px
(`IsoCharacterFigureRenderer.BuildAshoreOverlay`). Phase B pads each side by the figure's measured reach
over every clip as well (above), and the mount clips, not the lying ones, reach furthest.

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
   may be taken either way. (In the game, no code follows a socket's rotation yet: the tool track is
   data, and a clip plays frame by frame without blending, so such a turn would first show when a prop
   hangs on the socket.)
8. **The cell is tighter than the README says, and the lying clips run past it.** README l.122 says
   every build's cell fits with 4–5 px spare; the kit's own report gives 2 to 5 px. Measured unclipped,
   the sleeping figure facing N runs past the cell's bottom edge, and the render cuts it there: 8 px on
   the deck boss, 5 on the lobsterman, 1 on the painter; the Fisher and the monger touch the edge.
   Of the ten the game bakes, only the deck boss loses pixels (the table above). The `cell` check
   exempts swim, sleep and tread on any build over 1.545 m, which is 9.2's Fisher and covers 24 of the
   30 in 10.2. Ask: a cell that holds the lying clips, or a bar set for the 10.x body.
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
11. **The golden report's printed numbers depend on the engine.** The editor runs the rig in
    ClearScript's V8. On the ten presets it reproduces every gated row's pass and flags (189 / 190, the
    girl's face as the kit records), but prints 3 values differently from Node 24: residuals near
    1e-16 m. The game holds such rows to the rig's own 1e-6 gate. Ask: print residuals rounded, as the
    builds are (`toFixed(7)`), so any engine reprints the report byte for byte.

**Notes, not defects:**

- An unknown preset name throws (`no preset "…"`, l.1019). The desk reads this as new; 9.2 on main
  throws the same (`characterIsoRig9.js` l.748). An unknown field still falls back to the Fisher's.
- The body is about 17% taller: the Fisher's idle crown goes from 1.506 to 1.766 m and the collider
  radius from 0.20 to 0.186 m. That feeds the fixture resize, which Claude Design also carries as open
  ("fixture sizes for the 10.x body", README l.145).
- `SHADING.keylineDefault` is false: 10.x draws no keyline unless asked. The game follows the rig and
  draws no keyline on characters (owner ruling K4, 10-01).
- `brows.flat` binds no faces, on purpose (`FACE_EMPTY`: the fringe's edge is the flat brow).
