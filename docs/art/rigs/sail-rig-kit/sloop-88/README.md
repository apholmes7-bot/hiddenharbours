# HIDDEN HARBOURS — SLOOP 88 (sail rig kit)

The big one: an 88.5-foot performance cruiser on the fleet contract. One rig, two sidecars, no build step.

```
sloop88IsoRig.js              the rig — IIFE, attaches globalThis.Sloop88Iso, renders on demand
sloop88IsoRig.gameplay.json   the geometry sidecar — hidden-harbours/boat-gameplay-geometry@1, stamped (her first)
sloop88IsoRig.sailing.json    the sailing sidecar — hidden-harbours/boat-sailing@1, stamped (see ../README.md)
```

Builder page in the art workspace: `Sloop 88 Iso.dc.html` — turntable, wind + sheets, hoist / furl / staysail /
boom cover / platform, saloon door + cabin cut, paint shop, points-of-sail and state strips, sheet and sidecar
downloads (both sidecars).

## The contract (same as the rest of the fleet)

**32 px = 1 m.** Fixed ¾ turntable at **elev 40°** (30–50), 45° steps, 8 headings **N NE E SE S SW W NW**
(`dir` 0–7). Flat-facet shading, z-buffered, ordered dither, depth-edge darkening, **no AA**, binary alpha,
ringless (ADR 0031).

| | |
| --- | --- |
| Cell | **1072 × 1504** |
| Pivot | **(536, 1150)** = boat origin: amidships / **canoe-body bottom** / centreline, pinned every heading, pose, paint |
| LOA / beam | 27.0 m / 6.70 m (5.4 m transom) · masthead 37.0 m above the origin |
| Cell fit | `bounds()` swept clear over elev 30–50 × 8 headings × heel 20° × boom 86° × 8 wave frames (±519 / 1133 up / 339 down) |
| Colourways | the Sloop 30's 8 presets, hull for hull, + mixer (8 slots) — the two sail together |

**The waterline is not the pivot row.** The design waterline is **1.35 m above the origin** — the boot-top
bottom; the cabin cut (`ZLIP`) is 1.55. `WATERLINE.clip_rule` in the sidecar says where the water shader cuts.
The fin keel (4.60 m draft) and spade rudder bake only with `underbody:true`; afloat they are under the water the
shader owns. The swim platform sits 0.20 m above the cut.

## Rendering

```js
Sloop88Iso.render(dir, {
  elev:40,
  awa:45, aws:14,          // apparent wind: angle (deg, + = over the starboard bow), speed (kn)
  main:0.7, jib:0.7,       // sheets, 0 eased .. 1 hardened — the traveller car and the staysail car follow
  hoist:1, furl:0,         // main up the mast 0..1 · genoa rolled onto the forestay 0..1
  sfurl:1,                 // staysail rolled onto the inner forestay 0..1 (default 1 = furled; electric)
  cover:false,             // boom cover on — only at hoist 0 (the STORED state)
  platform:1,              // fold-down swim platform 1 = down · 0 = up, a flat transom door over the stair
  doorOpen:0,              // the sliding saloon door, 0..1 (8-frame cue, k/7)
  view:'exterior',         // 'cabin' cuts the boat open at the boot top (sails hidden unless sails:true)
  steer:0, grind:null,     // twin wheels -1..1 · 'jib' | 'main' puts a turning handle on the LEEWARD winch
  frame:0, rock:false,     // 8-frame loop: cloth flutter + handle; rock:true adds the wave (rock(i))
  underbody:false,
  scheme:'gelcoat-white'   // or paint:{ hull, stripe, bottom, deck, canvas, sail, teak, uph }
});                        // -> Uint8ClampedArray RGBA, W*H*4
```

`Sloop88Iso.sailPose(opts)` is the law — boom, genoa and staysail angles, angle of attack, fill, luff, stall,
heel and a `mode` string (`drawing · luffing · stalled · in irons · becalmed`). `poseOf(opts)` adds the wave.
`anchors(dir,opts)` projects the deck points (both helms and wheel hubs, threshold, mast base, bow roller, the six
winches, traveller car, staysail car, stair top, windlass, gooseneck, masthead) through the same pose;
`doorMount(dir,opts)` reports threshold, leading edge and `clear`; `bounds(dir,opts)` is the projected extent the
cell-fit sweep reads. `renderWheel(dir,opts)` is the masked wheel layer (both wheels) if you want it separate.

**Heel is a render parameter.** `pose.heel` rolls the whole model about the origin — capped at 20°, a 27 m yacht
is stiffer than the 30. Gameplay can pass `heelDeg` to override or `heel:false` to bake level.

## Sails and hardware the 30 does not have

- **Main** P 30.0 · E 10.2 · five full battens · loose in a **Park-Avenue boom** (10.6 m, 0.60 × 0.48 section)
  the sail flakes INTO — `hoist` shortens the luff and grows the pile in the trough; `cover` zips the boom cover.
  Rigid hydraulic vang, no topping lift. Boom underside 3.66 m over the cockpit sole.
- **Genoa** ~92% of J on a roller furler; sheets lead car → turning block → powered primary either side.
- **Self-tacking staysail** on the inner forestay, its own furler (`sfurl`), sheet on a curved track forward of
  the saloon; angle = min(genoa angle, 55°). Sits in the genoa's shadow (fill ×0.6) while the genoa is set.
- **Mainsheet traveller** across the aft deck: the car goes to leeward as the sheet eases
  (`x = side · 1.9 · 0.9 · (1−main)^0.7`); twin mainsheet winches.
- Three swept spreader sets (20°) with discontinuous diagonals, twin backstays, chainplates at ±2.95.
- Twin pedestal wheels with instrument pods and helm seats; two U-settees with tables; aft deck with two garage
  lids; transom stair to the fold-down platform; tender well forward of the mast; windlass + bow roller arm +
  anchor; eight cleats (bow, two spring pairs, stern); painted white spars.

## Colourways

The same eight schemes as the Sloop 30, value for value — `gelcoat-white` `atlantic-navy` `oyster-bone`
`squall-grey` `bottle-green` `graphite` `cranberry` `seafoam` — and the same slots (`hull` `stripe` `bottom` `deck`
`canvas` `sail` `teak` `uph`) through `rampFrom` / `chipWall` under the fleet chroma cap. **Paint never moves a
vertex.**

## Sidecars

Both generated, never typed, both stamped by `Art/_sidecarExport.js` from the same read of the rig
(`aed8100cb8ad…`).

**`sloop88IsoRig.gameplay.json`** — `Sloop88Iso.gameplayGeometry()`. `DECK` ×8 over four levels: cockpit_sole
(2.55) · aft_deck (3.29) · swim_platform (1.55, conditional on `opts.platform`) · coachroof · foredeck · tender_well (a 0.22 m
drop) · saloon_sole (2.05, `level: saloon`) · lower_sole (1.20, `level: lower`). `WASHBOARD` both sides (0.80 m
along the coaming, 0.85–1.05 along the saloon). `CLEATS` ×8. `ANCHORS` ×29 (both helms and wheel hubs, the six winches, traveller and staysail cars are read live
from `anchors()`). `THRESHOLD` — the **sliding** saloon
door (1.24 m leaf, 1.30 m travel to port, `keep_clear` = the pocket strip, not an arc; defaults CLOSED). `STAIRS`
×4: companionway (one tread), saloon_to_lower (two), helm_to_aft_deck (one step up), transom_stair (three treads
down onto the platform). `INTERACT` ×14: helm_port/stbd · mainsheet_port/stbd · primary_port/stbd · halyard_winch
· furler · windlass · platform · dining_table · chart_table · stove · bunk. `SAIL` and `WATERLINE` as the 30.
`_excluded`: no ladder (stairs everywhere), the owner's cabin not cut open in pass 1, no tender in the well, no
bimini, no downwind sail, no passerelle / davit / radar.

**`sloop88IsoRig.sailing.json`** — `SAIL_KIT.sailing()` off the live rig. LWL 26.95 m, waterline beam 6.15,
canoe-body displacement 89 190 kg (Cp 0.60), hull speed 12.6 kn, SA/D 17.8, D/L 127. Heel law
`min(20, 0.040·aws²…)`, saturating at 22.4 kn apparent. Eight states — the fleet five plus main_and_staysail,
heavy_weather (the builder's STAYSAIL ONLY: hoist 0.6 + staysail) and the orthogonal platform_up. Six transitions
(the staysail furls electrically — no deck station; the platform has one). Seven controls: both helms steer one
rudder; `grind` `main` lands on the leeward mainsheet winch, `jib` on the leeward primary. Reference polar: 9.15 kn
at twa 45 in 12 kn true (apparent 25.7° — one degree clear of the sprite's irons band), 11.4 on a beam reach,
7.2 dead downwind. **Read `_confirm.no_go_coupling`**: at twa 40 above ~10 kn true she reads "in irons" at the
polar's speed.

If the rig hash moves, regenerate from the builder page (both buttons) or `SAIL_KIT.write()` — never patch a number.
