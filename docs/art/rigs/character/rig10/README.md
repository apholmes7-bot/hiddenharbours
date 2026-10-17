# Hidden Harbours — Character Iso · kit 10.3

**2026-10-03 · rev 10.3** · rig `Art/characterIsoRig10.js` `ad563075…` · pose library `Art/characterIsoRig10.poses.js` `a28e5d92…` · checks `Art/characterIsoRig10.checks.js` `9649a0f0…` (full hashes in `SHA256SUMS.txt` and in every generated file)

The send-back on kit 10.2 (brief of 10-03), answered item by item in section 3.

**The rig file changed.** So did the pose library and the checks. What the bake reads and what changed in it:
* Kept: the file names, `CharacterIso10`, `CAST10`, `exportBuild`, `gameplay`, `W` 80, `H` 104, `pivot` (40, 90), the 13 face groups and their bands (`pt`, `az`, `sn`, `oh`), `SHADING` and `keylineDefault: false`.
* Changed: `revision` is `'10.3'`. The rod's bend sockets (`tool_R_1`, `tool_R_2`, `tool_L_1`, `tool_L_2`) turn differently in every bent frame of hold, cast, castBack, castRelease, bite, strike, reel and land. The sleep clip lies in a new place, so its pins move. The saddle mounts' swing leg turns its knee another way. Five presets' face marks moved at SE and SW (the girl, the postmistress, the painter, the tourist, the paper boy).
* Added: `DITHER`, `TOL`, `INK`, `INK_ROLES`, `EYE_WHITE`, `AIM`, `R7`, and `aimBar` in `lookContract()` (so in every build's `look` and every sidecar's `overlays.look`).

Contents: 1. What changed, file by file · 2. Changed numbers · 3. The send-back, item by item · 4. The cast · 5. Gates and random builds · 6. The options file · 7. The shading · 8. The gameplay sidecar · 9. Renders and the bake page · 10. Checking and regenerating · 11. Using it · 12. Open · 13. Packaging

## 1. What changed from 10.2, file by file

| File | Change |
|---|---|
| `Art/characterIsoRig10.js` | rev 10.3. New exports (section 11). `paint()` reads its tolerances from `TOL` (same values, same pixels). Bend chords keep the rod's roll. Face marks: a profile eye the hair covers moves a column forward; the diagonal eye pair keeps 0.08 px inside the cheek and counts the body round the head as cover; a knit brow wins over a lidded eye's lid; the mouth's centre goes a row up where the chin is too short or the collar covers it. `sizes().bindBytes` counts at 7 decimals. |
| `Art/characterIsoRig10.poses.js` | The sleeper lies centred on the pivot. The saddle mounts turn the swing knee about the leg. Line 4 names `characterIsoRig10.js` as the solver's file. |
| `Art/characterIsoRig10.checks.js` | Residuals print with `toFixed(7)`. The 1e-6 gates read `TOL.gate_m`. `cell` gates swim, sleep and tread on every build, names the 80 × 104 cell, and reports cloth from the cloth's own pixels. `look` gates the aim at `AIM.bar_deg`. `budget` counts clip bytes at 7 decimals. `heights` quotes the 10.x Fisher. |
| `Art/characterIsoRig10_2.js`, `_2.poses.js` | New: 10.2 frozen (`CharacterIso10_2`), for the review page's comparisons. |
| `Art/characterIsoRig10_1.js`, `_1.poses.js` | Unchanged (10.1 frozen, for `Character v10.2.dc.html`). |
| `builds/<key>.v10.json`, `gameplay/…`, `builds/presets.json` | Regenerated from 10.3. `presets.json` changes only in its stamps and revision. |
| `data/options.v10.json` | Regenerated; the randomBuild rule for elders' hair says 5 in 6. |
| `data/shading.v10.json` | Schema `character-shading@3`: adds `dither`, `tolerance`, `ink` and `aim`. |
| `golden-report.json` | Schema `character-golden@3`, run by `tools/check.cjs`: 570 / 570. |
| `renders/` | New strips `<key>.blink.png` and `<key>.look.png` for all thirty, 1× and 4×. `manifest.json` (schema `character-renders@3`) lists every image with its `rgbaSha256` and `pngSha256`. `cast.png` is redrawn transparent (layout in the manifest). |
| `tools/kit.js`, `tools/check.cjs` | New: every writer, and the checker (section 10). |
| `Character v10.3.dc.html` | New: the bake page for the blink and look-at masters, and five fixes drawn against 10.2. |
| `Character v10.2.dc.html`, `support.js` | Unchanged. The 10.2 page keeps the working wardrobe for the creator; it now draws rig 10.3 under its 10.2 labels. |

## 2. Changed numbers, old and new

| What | 10.2 | 10.3 |
|---|---|---|
| `revision` | 10.2 | 10.3 |
| Sleep: pelvis y | −0.18 m | `pelvisZ − heightM / 2`: the Fisher +0.053, the deck boss +0.064, the boy −0.024 |
| Sleep: every pin's y | as 10.2 | +0.233 m on the Fisher, +0.244 the deck boss, +0.156 the boy (the same shift as the pelvis) |
| Sleep at N, rows to spare below the feet (unclipped) | deck boss −2, lobsterman −1, Fisher 0 | deck boss 3, lobsterman 4, Fisher 5 |
| `cell`: tightest spare, keyline included | 2 to 5 px; lying clips not gated on 24 builds | 2 to 5 px on every clip but the mounts, lying ones included (the deck boss 2 px, swim f0 E) |
| Rod bend sockets, largest step between frames | 180.0° (cast f1→2, f4→5; strike f3→4, f4→5; land f5→6, f10→11) | 14.3° (the Fisher, cast f4→5, `tool_L_2`) |
| The mounts, largest joint step | 111.8° to 179.3° (nan and the mender 179.3°, mountUp f9→10 `hip_R`) | 96.8° to 141.6°: the saddle mounts 120.7° at most (nan and the mender, mountDown f7→8 `hip_R`); the children's bench mount 141.6°, as before |
| The Fisher's largest joint step | 131.0° (mountUp f9→10 `foot_R`) | 103.6° (mountDown f7→8 `foot_R`) |
| Aim bar | none | 4.0° (`AIM.bar_deg`) |
| `sizes().bindBytes` | 196,475 to 289,899 (the Fisher 210,260), raw numbers | 138,560 to 208,377 (the Fisher 155,304), at 7 decimals |
| `budget`: KB of clip JSON | 1,783 to 1,845 (the Fisher 1,783), raw numbers | 1,176 to 1,211 (the Fisher 1,176), at 7 decimals |
| Residuals in the golden report | e.g. `3.33e-16 m` | `0.0000000 m` |
| Gated checks on the thirty | 569 / 570 (the girl's face at SW) | 570 / 570 |
| Tris: girl, postie, painter, tourist, paper boy | 1,126 · 1,122 · 1,346 · 1,062 · 1,140 | 1,146 · 1,142 · 1,362 · 1,082 · 1,156 (the mouth's marks at the diagonals) |
| Random builds failing a face or light gate (seeds 1 to 300) | 50 | 7 |
| Random builds failing a face, look or light gate (seeds 1 to 60) | 8 | 7 (every gate run: 7) |
| README heights | the designed crown + 1 cm (`heightM`) | the designed crown (`headZ + crown`) and the crown in idle |
| The Fisher's height | 1.79 m | 1.782 m designed, 1.766 m in idle |
| Elders' grey hair (README, options file) | 3 in 4 | 5 in 6 (83 %) |

## 3. The send-back, item by item

Numbered as the brief numbers them.

1. **The README's missing sections.** Sections 6, 7 and 8. Every cross-reference here points at a file in this kit.
2. **The stale file name.** `characterIsoRig10.poses.js` line 4 names `characterIsoRig10.js`.
3. **The dither, the tolerance and the ink** are exported beside `SHADING`, `BLINK` and `LOOK`:
   * `DITHER`: `{ used: false, bayer4: [[0,8,2,10],[12,4,14,6],[3,11,1,9],[15,7,13,5]], index, threshold }`. Rig 10 never dithers: every tone is `clamp(round(…))` (`SHADING.step`). The matrix is the fleet rigs' standard, for a baker that shares one rasteriser.
   * `TOL`: `gate_m` 1e-6 (the round trip, loops, hand-offs, rocked skin), `inside` 1e-6 (a pixel centre is inside a triangle when every barycentric weight is ≥ −inside), `area` 1e-9, `cull` 1e-4, `markEdge` 1e-4, `depthScale` 1e7, `shadeScale` 1e9, `tieDepth` 1e-9, each with its use in `TOL.use`. `paint()` reads them from `TOL`.
   * `INK` `['#12181b', '#243036']`, with `INK_ROLES` and `EYE_WHITE` `#e6e9e2`. `INK[0]` is the open eye and the gaze pupil, and the brow is `mix(hair ramp[0], INK[0], 0.34)`. `INK[1]` is the half-shut eye. The keyline `#101a19` is a colour of its own, the A/B keyline's tint; neither is meant to be the other.
   * The aim bar is `AIM` (item 4 below).
4. **A blink and a look-at, shown.** `renders/1x` and `renders/4x` hold `<key>.blink.png` and `<key>.look.png` for all thirty builds; the bake page is `Character v10.3.dc.html`, and `node tools/check.cjs --write` renders the same masters. The layouts are in `renders/manifest.json` (`strips`), and each look image lists the turns it was drawn with.
5. **Files written under emulation.** My Node is a browser's V8. Written there: everything under `builds/`, `gameplay/`, `data/` and `renders/`, `golden-report.json` and `SHA256SUMS.txt`. Every writer ships in `tools/kit.js`, and `node tools/check.cjs --write` regenerates them all on real Node. With item 11 and the socket fix, the two kinds of difference the Art desk found should be gone: residuals print the same, and no socket turns by exactly 180°.
6. **The Fisher's irises.** Intended. `sea` is the Fisher's eye colour, a sea green, and it stays. Since 10.0 it draws nowhere: the eye is one pixel of `INK[0]`, and no face of any build is drawn in `iris` (`INK_ROLES.iris`), so no render shows a teal eye.
7. **The steps between frames.**
   * The rod sockets: a 180° turn may not be taken either way, and it was not the rod swung through. 10.2's bend chords took `aimTo`'s world-up frame, which turned 180° about the rod each time the rod passed vertical. 10.3 turns each chord the shortest way from the frame before it, so the chords keep the rod's roll; their largest step is 14.3°. The palm socket still turns 100.3° in cast f4→5: that is the rod swung forward (pitch 113° to 10° in one frame, rig 6's attitude table), and it is meant.
   * The hip: not meant. 179.3° was the swing knee's pole flipping as the foot crossed beside the hip. On the saddle the swing knee now turns about the leg from the standing pole to the seated pole; the largest joint step in the saddle mounts is 120.7°, the leg going back over the seat at the clip's pace. The children's bench mount keeps 141.6° (mountCab f6→7 `hip_R`), the leg lifted over the bench in one frame of 18; that is meant.
8. **The cell.** The cell stays 80 × 104, and the lying clips now fit it: the sleeper lies centred on the pivot, and `cell` gates swim, sleep and tread on every build. Measured spare, keyline included: 2 to 5 px on the thirty (section 4), 2 px or more on the 60 random builds.
9. **Stale texts.** The cell check names `W × H`. The heights detail quotes the 10.x Fisher (1.782 designed, 1.766 in idle). The cloth note reads the cloth's own pixels: on the thirty it fires once, the mender's skirt in reach f3 at N. Heights are in section 4. The twenty new builds use every beard but the mutton chops, which only the deck boss wears. Elders draw salt, grey or white 5 in 6 (3 in 4 from those three, else from all nine colours).
10. **Random builds.** Section 5 names every failing class, with what 10.3 fixed.
11. **Printed numbers.** Residuals print with `toFixed(7)`; `bindBytes` and the clip KB count numbers at 7 decimals.
12. **RGBA hashes.** `renders/manifest.json` lists `rgbaSha256` (straight RGBA, row-major, empty pixels 0,0,0,0) and `pngSha256` for all 241 images.

**The aim bar (the brief's section 4).** One bar for every build: **4.0°**, exported as `AIM.bar_deg` with its targets and measure, and gated by `look`.
* Measured on the thirty: 2.82° to 2.98°; the seven elders 3.65° to 3.73°. On the 60 random builds: 3.71° at most.
* What moved it from 9.2's 1.70°: the head point the aim is taken from. In 9.2 `headMid` sat 2 cm ahead of the head bone; since 10.0 it sits 10.4 cm ahead, where the eyes of the scaled head are. Turned 50°, it swings 9.2 cm instead of 3.2 cm, and for a target 2 m away that parallax grew from 0.28° to 2.20° (the skipper 0.58° to 2.49°). The other part, the turn split over two bones in their own frames, is unchanged: 1.58°, and 2.23° on an elder, whose stoop tilts the neck.
* `lookAt()` is unchanged, so the game's port holds. Three fixed-point passes on the turned head (measure the miss, add it, re-solve) land within 0.003° on every build. That is a change to `lookAt()` and to the port, so it is offered, not made.

**The owner's rulings (the brief's section 5).** No keyline: `SHADING.keylineDefault` is false, and `keyline: true` stays only as `render()`'s A/B. The fixtures for the 10.x body are the game's. Feet on St Peters' slopes and outfit ramps matched to the terrain's stay with me (section 12). Presets only, for now: nothing here changes which builds the game bakes.

## 4. The cast

Designed is the build's crown, `headZ + crown`; idle is the crown in idle f0. 10.2's README gave `heightM`, which adds 1 cm above the crown for the hair.

| Key | Label | Body | Wearing | Hair | Beard | Hat | Tris | Designed m | Idle m | Gated |
|---|---|---|---|---|---|---|---:|---:|---:|---:|
| `fisher` | Fisher | man, average, adult | overalls | mop | stubble | none | 1,165 | 1.782 | 1.766 | 19 / 19 |
| `ginny` | Ginny | woman, average, adult | overalls | bob | none | hood | 1,154 | 1.664 | 1.648 | 19 / 19 |
| `skipper` | Skipper | man, stocky, elder | oilskins | crop | full | souwester | 1,426 | 1.660 | 1.641 | 19 / 19 |
| `nan` | Nan | woman, full, elder | skirt | bun | none | kerchief | 1,248 | 1.533 | 1.514 | 19 / 19 |
| `deckboss` | Deck boss | man, broad, adult | vest + trousers | buzz | mutton | flatcap | 1,081 | 1.880 | 1.864 | 19 / 19 |
| `packer` | Packer | woman, pear, adult | apron + trousers | bun | none | kerchief | 1,145 | 1.639 | 1.623 | 19 / 19 |
| `cutter` | Cutter | woman, slender, youth | apron + trousers | ponytail | none | none | 1,145 | 1.508 | 1.492 | 19 / 19 |
| `hand` | Deckhand | man, lean, youth | workshirt + trousers | crop | none | ballcap | 1,107 | 1.693 | 1.677 | 19 / 19 |
| `boy` | Wharf boy | man, average, child | sweater + trousers | mop | none | watchcap | 1,148 | 1.273 | 1.257 | 19 / 19 |
| `girl` | Wharf girl | woman, average, child | workshirt + trousers | long | none | none | 1,146 | 1.278 | 1.262 | 19 / 19 |
| `harbourmaster` | Harbourmaster | man, broad, elder | peacoat + trousers | crop | vandyke | captain | 1,294 | 1.729 | 1.709 | 19 / 19 |
| `lightkeeper` | Lightkeeper | woman, slender, elder | greatcoat | bun | none | watchcap | 1,200 | 1.646 | 1.626 | 19 / 19 |
| `ferry` | Ferry captain | woman, average, adult | vest + trousers | ponytail | none | captain | 1,124 | 1.668 | 1.652 | 19 / 19 |
| `monger` | Fishmonger | man, heavy, adult | apron + trousers | crop | full | flatcap | 1,254 | 1.782 | 1.766 | 19 / 19 |
| `mender` | Net mender | woman, full, elder | cardigan + longskirt | bun | none | kerchief | 1,236 | 1.533 | 1.514 | 19 / 19 |
| `boatwright` | Boatwright | man, stocky, adult | singlet + trousers | mop | chinstrap | none | 1,143 | 1.687 | 1.671 | 19 / 19 |
| `postie` | Postmistress | woman, hourglass, adult | pinafore | bob | none | beret | 1,142 | 1.664 | 1.648 | 19 / 19 |
| `grocer` | Grocer | man, heavy, elder | cardigan + trousers | bald | moustache | none | 1,115 | 1.647 | 1.627 | 19 / 19 |
| `diver` | Diver | woman, average, youth | wetsuit | buzz | none | none | 974 | 1.577 | 1.561 | 19 / 19 |
| `lobsterman` | Lobsterman | man, lean, adult | waders | crop | stubble | ballcap | 1,126 | 1.857 | 1.841 | 19 / 19 |
| `cafe` | Café owner | woman, pear, adult | breton + skirt | long | none | none | 1,279 | 1.620 | 1.604 | 19 / 19 |
| `swimmer` | Swimmer | man, average, youth | trunks | crop | none | none | 1,048 | 1.680 | 1.664 | 19 / 19 |
| `oldsalt` | Old salt | man, average, elder | peacoat + trousers | bald | long | watchcap | 1,332 | 1.706 | 1.686 | 19 / 19 |
| `lifeguard` | Lifeguard | woman, slender, adult | swimsuit | ponytail | none | bucket | 1,095 | 1.726 | 1.710 | 19 / 19 |
| `painter` | Painter | man, lean, adult | breton + cropped | mop | goatee | beret | 1,362 | 1.802 | 1.786 | 19 / 19 |
| `coastguard` | Coastguard | woman, hourglass, adult | lifevest + trousers | ponytail | none | ballcap | 1,251 | 1.668 | 1.652 | 19 / 19 |
| `mechanic` | Mechanic | man, stocky, adult | coveralls | buzz | sideburns | none | 1,005 | 1.645 | 1.629 | 19 / 19 |
| `baker` | Baker | woman, full, adult | apron + skirt | bun | none | kerchief | 1,145 | 1.662 | 1.646 | 19 / 19 |
| `tourist` | Tourist | woman, average, adult | sundress | bob | none | sunhat | 1,082 | 1.664 | 1.648 | 19 / 19 |
| `paperboy` | Paper boy | man, average, child | hoodie + shorts | mop | none | watchcap | 1,156 | 1.279 | 1.263 | 19 / 19 |

## 5. Gates and random builds

`golden-report.json`: the 20 checks on all 30 builds, **570 / 570 gated checks pass** (`budget` reports and never gates).

Random builds are `CharacterIso10.randomBuild(mulberry32(seed))`, with `mulberry32` as in `tools/kit.js`; `node tools/check.cjs --random 60` runs them. Every gate on seeds 1 to 60: **7 of 60 fail**, all on `look`. The face and light gates on seeds 1 to 300: 7 of 300 fail (10.2: 50).

Fixed in 10.3, with the 10.2 count on seeds 1 to 300:
* the profile eye under the hair on the square and wide heads (E and W showing no eye): about 25;
* `brows.knit` drawing nothing over a lidded eye: about 17;
* the far eye at a diagonal on a narrow face in idle (the Wharf girl's class): 3, all fixed, and the girl with them; 2 others remain (below);
* 10.2's crotch pixel on a full-frame girl at weight −2 no longer differs (0 px on the work shirt, tee, overalls and dress).

Still failing, every class:
* **look, 1 px of see-through** between the hair, the neck and the collar with the head at a limit (pitch +20, or yaw ±60): 7 of 60 (seeds 5, 14, 17, 18, 21, 33, 59), mostly pear and wide heads. No preset.
* **face, `mouth.open` draws as `mouth.flat` at S**: a heart head with a ballcap, or a child's heart or oval head, whose chin the collar covers in idle: 5 of 300 (seeds 112, 116, 121, 157, 184). Moving the mouth up wherever the chin is tight moved eighteen presets' mouths, so it waits.
* **face, one eye at SW** on a heart head on a child or youth: 2 of 300 (seeds 121, 145).
* **light, 1 px** between E and W mirrored on a square head with mutton chops and a bun: 1 of 300 (seed 189).

The game's creator checks each build against these gates, so a failing build is refused.

## 6. The options file

`data/options.v10.json` (schema `character-options@2`, one-space indent) is written from the rig's tables by `tools/kit.js` `optionsFile()`:
* `options`: `OPTIONS`, every field's values in the creator's order. `fields`: `FIELDS`, a build's 20 fields.
* `garments`: `GARMENTS` with `group` (`top`, `outfit`, `swimwear`). A top takes a bottom; `fem` marks what randomBuild never gives a man; `look` gives the colours the creator switches to; `wear` names what is worn.
* `bottoms`, `beards` (parts `r0`, `r1`, `marks`, `mass`, `mat`), `hats` (the hairline each hat's band sits at), `sex` and `frames` (the body factors), `ages`, `heads` (ring widths, chin, z, corner), `eyeShapes`.
* `palettes`: every ramp by name (`SKINS`, hair, outfit, shirt, hat and trim colours).
* `randomBuild`: the call and its rules, in words.

## 7. The shading

`data/shading.v10.json` (schema `character-shading@3`) is `shadingContract('fisher')` plus `dither`, `tolerance`, `ink` and `aim`.
* A face is culled on its own normal (toward-camera component ≤ `TOL.cull`) and lit on its smooth normal `sn` where it has one.
* Its tone is `clamp(round(s · gain + bias + b), lo, hi) + off`, clamped to the ramp, with `s = n · key + form · (n.toward − formMid)` and `s` rounded at `TOL.shadeScale`. `key` is the fleet key without its left part, so E and W mirror.
* `lo..hi` spans four tones (shadow, base, light, highlight). Across a depth break wider than `edge` (0.12 m) the far pixel drops one step.
* Point marks (the face) draw one pixel under their centre, over head, nose, beard or another mark, nearer by their depth bias, within `az` of their design camera; a centre within `TOL.markEdge` of a pixel edge draws nothing.
* The head bone moves in screen space so the head point sits on a pixel centre (≤ 0.5 px). No dither, no keyline (`keylineDefault` false).
* `materials` gives every material's ramp, gain, bias, lo, hi and off; fixed colours (ink, white, iris, lid, brow, lip, mouth) have one colour and no tone.

## 8. The gameplay sidecar

`gameplay/characterIsoRig10.<key>.gameplay.json` (schema `character-gameplay@1`) is `gameplay(key)`, figure-frame metres, unrocked:
* `frame`: origin at the cell pivot on the ground, +x right, +y forward, +z up; 32 px/m, the cell and pivot, 40°.
* `figure`: the crown in idle, hip and hand heights, the collider (`collision_radius_m`, `footprint_m`, and the bind mesh's measured half-widths).
* `contracts`: the world contracts (`CONTRACT`: rail, rung, work height, bed, wheel, saddle and its fitted size); `sockets`; `overlays`: the blink clip and the look contract, with its `aimBar`.
* `clips`: per clip its frames, ms, loop, mount, carry, `speed_mps` (walk and run: play at moveSpeed / speed_mps) and hand-off, and `pins` per frame: hands, feet, hip, head, mid, contact, `ikShort` where a limb fell short, the tool, and the clip's mount section (`tool`, `ladder`, `water`, `bed`, `wheel`, `saddle`, `rest`, `board`, `carry`). An absent section means the clip does not use it.

## 9. Renders and the bake page

Every image is `render({ build, clip, frame, dir, face, look })` per cell, laid in a grid of 80 × 104 cells, transparent, at 32 px/m and 40°, contour and head snap on, no keyline, no sky; 4× is the 1× grid scaled by 4, nearest neighbour.
* `idle8`: idle f0 at the eight facings, N first. `walkS`: the eight walk frames at S.
* `blink`: rows E, SE, S, SW, W; columns the four blink frames (half, shut, shut, half), idle f0.
* `look`: at S. Row 0, the head turned to each limit. Row 1, `lookAt()` with the default share (0.7) for targets 2 m away at bearings −50, −30, −15, 0, 15, 30, 50 level, and 0° 0.35 m down, with lookAt's eyes.
* `cast.png`: the thirty at S then SE, ten to a row, ×2.

`Character v10.3.dc.html` draws the blink and look strips for any of the thirty, saves them as PNG at 1× and 4×, and holds each against the manifest's `rgbaSha256` when it can read the manifest (open it from a web server).

## 10. Checking and regenerating

`node tools/check.cjs` (Node 18 or later, built-ins only) loads the rig as a page does, regenerates every data file and compares it byte for byte, re-renders all 241 images and compares them pixel for pixel and with the manifest, runs the golden suite on the thirty and compares it with `golden-report.json`, and checks `SHA256SUMS.txt`. It ends with one `RESULT:` line. About three minutes; `--quick` skips the golden suite, `--random 60` adds the random sweep (reported), `--write` regenerates everything first, `SHA256SUMS.txt` last.

It has not run on real Node. Under my emulation I ran it with stand-ins for fs, crypto and zlib on part of the kit (the stamps, five data files, two renders and their PNG decoding, the sums), and it ran clean there; the golden report and the renders were written by the same `tools/kit.js` functions it calls. Each `pngSha256` is the file's as shipped; another PNG encoder writes other bytes for the same pixels, so the checker holds pixels and only notes a different `pngSha256`.

## 11. Using it

```
<script src="Art/characterIsoRig10.js"></script>
<script src="Art/characterIsoRig10.poses.js"></script>
<script src="Art/characterIsoRig10.checks.js"></script>   <!-- optional -->

const C = CharacterIso10;                                   // C.revision === '10.3'
C.render({ build:'girl', clip:'idle', frame:0, dir:5, face:{ eyes:'shut', brows:'flat', mouth:'flat' } });
C.render({ build:'fisher', clip:'idle', frame:0, dir:4, look:{ yaw:30, pitch:5 } });
C.exportBuild('fisher'); C.gameplay('fisher');
C.AIM.bar_deg; C.TOL.inside; C.DITHER.used; C.INK[0];
```

New exports (additions only): `DITHER`, `TOL`, `INK`, `INK_ROLES`, `EYE_WHITE`, `AIM`, `R7` (the 7-decimal JSON replacer every export prints with), and `lookContract().aimBar`.

## 12. Open

* The random classes still failing (section 5): the neck at a turned limit, the mouth on a short chin under a collar, the far eye on a child's heart head, the one light pixel.
* The look-at's three-pass aim (section 3), if the game wants a bar near 0°.
* The Character Creator still runs rig 9; moving it to 10.3 means the 80 × 104 cell, the sex and frame controls, the wardrobe and randomBuild (`Character v10.2.dc.html` has the working wardrobe).
* Feet on St Peters' slopes, and outfit ramps matched to the terrain's: mine, nothing asked now.

## 13. Packaging

`HH-character-kit-10-3-2026-10-03.zip`: 320 files in 8 folders (328 entries). `SHA256SUMS.txt` covers every other file. The 30 build exports are about 1.4 MB each.
