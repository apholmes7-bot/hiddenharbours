# HIDDEN HARBOURS — SLOOP 30 (sail rig kit)

The first sail in the fleet. One rig, two sidecars, no build step.

```
sloopIsoRig.js              the rig — IIFE, attaches globalThis.SloopIso, renders on demand
sloopIsoRig.gameplay.json   the geometry sidecar — hidden-harbours/boat-gameplay-geometry@1, stamped
sloopIsoRig.sailing.json    the sailing sidecar — hidden-harbours/boat-sailing@1, stamped (see ../README.md)
```

Builder page in the art workspace: `Sloop 30 Iso.dc.html` — turntable, wind + sheets, hoist / furl / stow,
door + cabin cut, paint shop, points-of-sail and state strips, sheet and sidecar downloads (both sidecars).

## The contract (same as the rest of the fleet)

**32 px = 1 m.** Fixed ¾ turntable at **elev 40°** (30–50), 45° steps, 8 headings **N NE E SE S SW W NW**
(`dir` 0–7). Flat-facet shading, z-buffered, ordered dither, depth-edge darkening, **no AA**, binary alpha,
ringless (ADR 0031).

| | |
| --- | --- |
| Cell | **400 × 552** |
| Pivot | **(200, 432)** = boat origin: amidships / **canoe-body bottom** / centreline, pinned every heading, pose, paint |
| LOA / beam | 9.4 m / 2.98 m · masthead 13.0 m above the origin |
| Cell fit | clear of all four edges over elev 30–50 × 8 headings × heel 24° × boom 86° × 8 wave frames |
| Colourways | 8 presets + mixer (8 slots) |

**The waterline is not the pivot row.** The design waterline is **0.55 m above the origin** — the boot-top
bottom. `WATERLINE.clip_rule` in the sidecar says where the water shader cuts. The fin keel and spade
rudder bake only with `underbody:true`; afloat they are under the water the shader owns.

## Rendering

```js
SloopIso.render(dir, {
  elev:40,
  awa:45, aws:12,          // apparent wind: angle (deg, + = over the starboard bow), speed (kn)
  main:0.7, jib:0.7,       // sheets, 0 eased .. 1 hardened
  hoist:1, furl:0,         // main up the mast 0..1 · jib rolled onto the forestay 0..1
  cover:false,             // stack pack zipped — only at hoist 0 (the STORED state)
  doorOpen:0, hatchOpen:undefined,   // companionway; the hatch follows the door unless set
  view:'exterior',         // 'cabin' cuts the boat open at the boot-top lip (sails hidden unless sails:true)
  steer:0, grind:null,     // wheel -1..1 · 'jib' | 'main' puts a turning handle on that winch
  frame:0, rock:false,     // 8-frame loop: cloth flutter + handle; rock:true adds the wave (rock(i))
  underbody:false,
  scheme:'gelcoat-white'   // or paint:{ hull, stripe, bottom, deck, canvas, sail, teak, uph }
});                        // -> Uint8ClampedArray RGBA, W*H*4
```

`SloopIso.sailPose(opts)` is the law the picture obeys — boom and jib angles, angle of attack, fill, luff,
stall, heel and a `mode` string (`drawing · luffing · stalled · in irons · becalmed`). `poseOf(opts)`
adds the wave. `anchors(dir,opts)` projects the deck points (helm, wheel hub, threshold, winches, sheet
block, cleats, boom end) through the same pose; `doorMount(dir,opts)` reports threshold, leading edge and
`clear`. `renderWheel(dir,opts)` is the masked wheel layer if you want it separate; by default the wheel
is in the main render.

**Heel is a render parameter.** `pose.heel` rolls the whole model about the origin. Gameplay can pass
`heelDeg` to override or `heel:false` to bake level.

## Sails

- **Main** P 9.5 · E 3.4 · four full battens · square-ish head · loose-footed on a 3.55 m boom. `hoist`
  shortens the luff along the mast and grows the stack pack on the boom; `cover` zips it.
- **Jib** 105% on a roller furler. `furl` rolls it onto the forestay; the roll wears the **canvas** slot
  (the UV strip). The working sheet runs taut through the leeward car to the leeward coaming primary;
  the lazy sheet sags across the foredeck forward of the mast.
- Running rigging that follows the pose: mainsheet (boom end → sole block → pedestal), both jib sheets,
  lazy jacks, topping lift. Static: halyard tails to the cabin-top winches, the furling line down the
  port deck, the standing rigging.

## Colourways

Slots: `hull` `stripe` (cove + boot) `bottom` `deck` `canvas` `sail` `teak` `uph`. Ramps derive in OKLCH
under the fleet chroma cap (`rampFrom`, `chipWall`) — identical envelope to the skiffs and the punt, and
six hull colours are shared with them value for value: `atlantic-navy` `oyster-bone` `squall-grey`
`bottle-green` `graphite` `cranberry` `seafoam`, plus the yard's `gelcoat-white`. **Paint never moves a
vertex.**

## Sidecars

Both generated, never typed, both stamped by `Art/_sidecarExport.js` (`derivedFromRigSha256` = the rig bytes as
served) from the same read of the rig.

**`sloopIsoRig.gameplay.json`** — `SloopIso.gameplayGeometry()`. Sections: `DECK` (cockpit_sole · both cockpit
seats · coachroof · foredeck · cabin_sole), `WASHBOARD` (both side decks), `CLEATS` (6), `ANCHORS`, `THRESHOLD`
(hinged companionway, camper schema, swept `keep_clear` + the sliding hatch), `STAIRS` (three treads down),
`INTERACT` (helm · mainsheet · two primaries · halyard winch · furler · stove · locker · bunk), **`SAIL`** (the rig
plan and the pose law, so gameplay can make its polar agree with the sprite), **`WATERLINE`** (the shader cut + drafts).
`_excluded` and `_confirm` carry the absences and the open rulings.

**`sloopIsoRig.sailing.json`** — `SAIL_KIT.sailing()` off the live rig. LWL 9.31 m, waterline beam 2.65, canoe-body
displacement 4 875 kg (Cp 0.61), hull speed 7.41 kn, SA/D 13.1, D/L 168. Heel law `min(24, 0.067·aws²…)`,
saturating at 18.9 kn apparent. Five states (sailing · main_only · headsail_only · motoring · stored), four
transitions, the tack (44° of boom) and gybe (172°) paths, five controls, the reference polar (4.97 kn at twa 45 in
12 kn true, 6.56 on a broad reach, 4.84 dead downwind). Grind handle: `jib` on the leeward primary, `main` on the
starboard cabin-top halyard winch — the 30 has no mainsheet winch.

If the rig hash moves, regenerate from the builder page (both buttons) or `SAIL_KIT.write()` — never patch a number.
