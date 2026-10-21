# hh-village-return · phase 1 · houses

2026-09-25. The owner asked for the pass in phases, houses first. This return covers HouseIso (exteriors), the cottage-room cutaway, the 09-16 `floor` job, and a village plan draft for the houses (option A). Shops, yards, manors and plan B are later phases.

## This return, as packaged (2026-09-26, after phase 5)

Phases 1 to 5 are in `NOTES.md` (this file) and `NOTES-phase2.md` … `NOTES-phase5.md`. The folder follows the brief's §7:

- `houses-kit/`: the whole `kit/` folder of 1-houses, restamped. The restamp is `tools/restamp-kit.cjs`'s own logic, run in this workspace. It rewrote 10 of 14 stamped files: the houseIso, interiorIso, manorIso, manorUnitIso and coastalPass sidecars, the five layout proposals, `stair-contracts.json` and `manifest.json`. The interiorProp and wharfBuilding sidecars were already current. The new render signature is `d5d1c69503756a6c3adc1ee2353de33df22e42e5e827e8260935d4f7b9327a4c`. A `--check` pass afterwards reported all 14 current. `restamp-kit` only refreshes the presets a sidecar already lists, so `PRESETS.gambrelColonial` (phase 2) is in the rig but not in `houseIsoRig.params.json`.
- `shop-building-kit/`: the whole kit folder. `shopfrontRig.js` and `shopInteriorRig.js` are this return's. `shopBuildingRig.js`, the contract, the harness and the reference sheets are main's, unchanged. The contract (read out on 2026-08-06) was not regenerated, because the kit has no generator for it.
- `yard-landscaping-kit/`: the whole kit folder. `yardIsoRig.js` is this return's. `yardLots.js`, `outbuildingIsoRig.js` and `lots-phase5.json` are new. The sidecar was regenerated with `harness.html`'s own `buildSidecar()`. Only `derivedFromRigSha256` changed: the 62 pieces are identical. `SHA256SUMS.txt` lists the five files that changed.
- `checks/`, `plan/`, `boards*/`, `CAMERA-FIRST.md`, `checks.txt`. `phase1-rigs/` is the phase 1 rigs, kept for the phase 2 comparison only.
- `SHA256SUMS.json`: written last, in `tools/write-sha256sums.cjs`'s format.

**Load order across kits.** Load `houses-kit/Art/interiorPropRig.js`, then `coastalPass.js`, then the hosts. The shop, yard and outbuilding rigs use `CoastalPass.light` when it is loaded, and draw the classic look without it. Nothing is copied between kits (rule 12).

**Still to do on node.**
- Re-run every checker, `restamp-kit.cjs --check` and `write-sha256sums.cjs --check`. Here they ran through the workspace's evaluator.
- `outbuildingIsoRig.js` has no params sidecar yet.
- The yard sidecar's facing labels (Job 4, item 4) are as main had them.
- The kit designs and harnesses do not have the new options yet.
- Plan B is not drawn.

## The owner's rulings used here

- **The new look replaces today's.** Every house re-bakes; the 09-16 "today" bar becomes a classic pin: `render(dir, {...opts, classic:true})` is byte-identical to the cut (checked for HouseIso {}, the 5 presets and the 4 village shells at facings 4 and 6, and 30 cases at 1/4/6 while building).
- **Refine the coastal pass** (keep slate, green joinery). No cladding or roofing material changed.
- **The `floor` option (09-16 Job 2): a named floor wins.**
- **Cutaway: the first option** (walls cut at sill height with the house's outside). I read the form's `option_1` as the first choice; `cutH: 0.3` gives the knee-high curb if that was the pick.
- **Scope: cottage exteriors and a village plan draft.** Room heights (Job 3) and the manor (Job 4) were not picked, so `room-anchors` and `manor-surface` still fail exactly as at the cut. Their guards all pass.
- In chat: the newest tree rig's shading, night lighting by occupancy and time, glowing windows and lamps inside, all working with character v9.2's verbs.

## HouseIso (`houses-kit/Art/houseIsoRig.js`)

**The live look is now the default** (`render()` with the companion pass loaded). The light follows TreeRig4 / WharfRig2: a G-buffer relit from any `WeatherSky.at()` sky, sky visibility from nine occlusion maps, a sun map so eaves, porch roofs, hoods and chimneys shade the house, `castShadow()` onto the ground (levels 1 to 3), rain, fog, snow and a backlit rim. Faces take one band each, with no dither, and there is no keyline (ADR 0031; `outline:true` gives the A/B). The canvas and pivot are unchanged (992 × 1060, pivot 496, 676).

**Art fixes** (live only; `classic:true` skips them):
- The slate roof is a value lighter (`CoastalPass.PALETTE.slateLit`), its courses at 0.9 texture strength instead of 0.62, with a ridge cap and a corbel course on the chimney.
- A water-table board over a coursed stone plinth.
- A door paint picked to stand out from the walls (`DOORPAINT`: sage → red, white → green, red → blue, grey → blue). Doors are half-glazed, four lights in the upper leaf, and the hall lamp shows through them at night.
- Porch: 1.6 m deep instead of 1.9, and its roof is higher at the front (`eaveZ − 0.25`) with a 0.08 overhang past the posts. Posts stand only at the corners and either side of the steps (±0.85), the rail opens at the steps, the steps are solid to the ground, the deck has boards, and a work bench stands at the +X end. These numbers make the door ≥ 60 % visible at the diagonal facings (the checker measured 29 % at the cut).
- An eave door or a side door gets a **portico**: a deck, steps along the door's normal, a gabled hood on knee brackets (no posts, so a diagonal view reaches the door), and a lamp post at the foot of the steps on the show side.
- The woodpile and rain barrel moved from the back to the side the camera sees. The woodpile is stacked on a rack, with its top at 0.80 m.
- Bargeboards go on both gables.

**Camera-first surface** (every name is new; nothing was renamed):
- `entry: 'front' | 'left' | 'right'`. Left and right are as you stand looking at the show face. A side door sits 1.35 m back from the show corner. The show face keeps a window where its door was.
- `layout(opts)` gives `{show, door, ...}`. The show face is `+Y` when the door is under a front porch or on an ell wing, `+X` for an eave-door house (`HouseIso {}`, `redSaltbox`, `dormerCape`).
- `placement(opts)` gives `{show, entry, entryKind, facings:[{dir, doorFaces, showFaces, via}]}`, with directions measured by projection. A front entry lists 3/4/5 (or 5/6/7 for an eave door). A side entry lists its diagonal as `door` and the square-on facing as `signpost`. One deviation from the brief: an **ell or a cross-gabled house drops the facings where its +X side faces north**, because its wing, porch or cross gable is on that side. So `PRESETS.whiteFarmhouse` and `PRESETS.gothicRevival` list 4 and 5 only.
- `anchors(dir, opts)` gains `show`, `doorWall`, `doorFaces`, `approach` and `entryPath`, each point given as `{x, y}` screen px, `m` model metres and `w` world `[east, north]` metres from the pivot.
  - `approach` is at least 1.5 m out, past every deck and step.
  - `entryPath` runs straight out for a front entry. For a side entry it goes along the wall and round the corner.
  - Every existing field is unchanged.
- `footprint(opts)` gives rectangles in model metres (`walk: true` for decks and steps), and `dooryard(opts)` gives where the goods stand.
- Every face now carries a **tag**: `door.leaf`, `entry.frame|steps|porch|hood|lantern`, `show.porch|bay|dormers|crossGable|wing|bargeboard`, `yard.woodpile|barrel|bench`. Glass carries an emitter id.
- `stations(opts)`: the v9.2 verbs outside, each with its clip, fixture height, the range every creator body fits (`reports/worldfit.txt`), a stand point and a reachability test run on a 0.1 m grid from the approach.
  - door: `walk`, doorway ≥ 0.458 + 0.3 m
  - doorstep: `reach` at 0.95 (the rest `stowV`)
  - woodpile: `lift` / `place` at 0.80 (fit 0.718–0.978)
  - rain barrel: `lift` at its rim, 0.93
  - porch bench: `bench` at 0.80 above the deck (fit 0.643–0.853)
  - `requests`: v9.2 has no seated clip, so chairs stay decor.
- **Night:** `SCHEDULES` (family, fisher, elder, school, empty), `lightsOn(opts, sky)` and `lights(dir, opts)`.
  - Each window is assigned a room by where it is: kitchen (behind the midline), parlour, or upper (above the ground-floor heads).
  - Rooms light only when someone is home and awake after dark. `lampNeed` is 0 in daylight and 1 under the moon.
  - The fisher's kitchen lights at 04:15 before the boats. A house goes up to bed at `upstairs`. The lantern burns while anyone is up or out.
  - `occupancy: 'home' | 'away' | 'asleep' | {rooms, lantern}` lets the game's own routine override the schedule.
  - `lights()` returns every lamp with its level, model position, screen anchor and a ground pool, for the game's glow sprites.
- `frame(dir, opts, {only, noSky})`, `relight(fr, sky, o)`, `castShadow(fr, sky)`, `view(fr, channel)`, `renderLive`.

## InteriorIso (`interiorIsoRig.js`)

- **`cutaway: 'section'`** with `shell: <the house's options>` and `cutH` (default 0.9):
  - the dropped walls stand as stubs with the house's siding outside, wainscot inside and a cap on the cut;
  - corner boards, the stone plinth from grade, and the porch with its posts cut to match;
  - on the door's wall, a sill board across the gap, two jamb stubs and a mat inside, all tagged `entry.threshold` and `entry.mat`;
  - standing walls get an exterior edge on their cap.
- It draws with the live light. Indoors the sun is cut to 0.35 and the sky fill raised to 1.55 (`indoor` box). Night lamps (on the table, desk, dresser or chest) and the hearth burn by `HouseIso.lightsOn`.
- `live: true` gives the live light without the section. `frame`, `relight` and `renderLive` are exported.
- **Default render unchanged**, so every 09-16 byte pin holds.
- Not done: the doorway moving to a side door's wall (09-16 Job 1 part B, brief Job 2.4). The sage cottage's room still has its doorway on the porch wall.

## CoastalPass 3.2.0 (`coastalPass.js`)

- `CoastalPass.light` is the shared light engine that HouseIso and InteriorIso use. ManorIso can use it next. It does not check `enabled`.
- The house look: `slateLit`, door paint, texture strength, lanterns tagged and emissive, the dooryard goods, and downpipes at the goods corner and the opposite back corner. The manor path is untouched.
- **Job 2 (floor):** `cottage(faces, M, b, opts)`. With a floor named, the host's floor is kept, clipped round the upper storey's stairwell, and the pass's bands are skipped. With none named, it is byte-identical. The four shipped rooms name `plank` / `wideBoard`, so they re-bake with those floors (warm plank and wide board in place of the tile-hall / oak-parlour bands).

## Plan A (`plan/village-plan-A.json`, `boards/`)

- The pads are unmoved. The school and the farmhouse face S onto the lanes. The school lane is now drawn from the green past the hearth to the store and on to the school. Its east arm is `route.stpeters.farm_lane` (new id), and the post office edge is a footpath, `route.stpeters.post_lane` (new id).
- **Red saltbox: needs a ruling.** On its pad (25, 8), 4.6 m from the slip road, every camera-first facing with a gable porch stands on the road (measured clearance ≤ 0.37 m). The plan gives it the eave door of HouseIso's own `redSaltbox` preset: `porch: 'none'`, a portico, facing 5, door SW onto the road toward the green. The alternative is moving the pad about 3.5 m north in plan B.
- **Sage cottage:** facing 4 with a side entry on the left. The porch faces the shore, and the door faces W with a signpost. The path runs north to the slip road, with a lantern post at the turn.
- **Ginny's cottage:** facing 3, door SW. It is drawn with `shingleCottage`, because the game's own options for it were not read here.
- The store and the post office stay as today (door S) until the shop phase.
- Street furniture (a fingerpost at the green, lantern posts at the hearth and the store corner) still has to be checked against `municipal-infrastructure.md` §3.3.
- `StPetersInhabitants.Dooryard()` should read `approach` now: doors no longer face the green.

## Things that disagree with the brief

- The coordinator's rule 11 (today's pictures byte-identical) is superseded by the owner's ruling. The classic pins keep 09-16 reproducible.
- The show bar at the first listed facing fails for an ell or cross-gabled preset at the facing where its wing or cross gable is on the far side. Those facings are dropped from `placement()` (see above) rather than kept and failed.

## Checks

See `checks.txt`:
- 09-16: rigs-intact 24/24, door-registration 18/18, floor-option 4/4. room-anchors and manor-surface fail as at the cut, with every guard passing.
- `restamp-kit --check` is OK.
- `checks/camera-shells.cjs`: 308/308.

Everything ran through a Node shim in this workspace, because no node binary was available. Please re-run on node.

## Next phases

- Room heights (Job 3) and side-entry rooms.
- Shops (ShopBuilding / Shopfront / ShopInterior: `entry`, `view`, stock slots, the general store's tools / bait / materials).
- Yard paths (YardIso), manors (ManorIso and ManorUnitIso on the light engine; Job 4), and plan B.
- The kit designs (`House Iso.dc.html` and the rest) are not yet updated with the new options. The review board is `Village Camera-First Phase 1.dc.html` at the project root.
