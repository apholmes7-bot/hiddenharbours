# HIDDEN HARBOURS — SAIL RIG KIT

The two sailing hulls, source-side, each with a **full gameplay sidecar** on the fleet schema and a **sailing
sidecar** — the new kind, carrying what a sailing game needs that the geometry file deliberately leaves out.
Rig source only: no baked sheets, no dependencies, no build step.

```
sloop-30/  sloopIsoRig.js              9.4 m fractional sloop — IIFE, attaches globalThis.SloopIso
           sloopIsoRig.gameplay.json   boat-gameplay-geometry@1 — DECK · WASHBOARD · CLEATS · ANCHORS · THRESHOLD · STAIRS · INTERACT · SAIL · WATERLINE
           sloopIsoRig.sailing.json    boat-sailing@1 (NEW) — HULL_FORM · SAIL_PLAN · AUTO_TRIM · POINTS_OF_SAIL · TRIM_TABLE · HEEL · STATES · TRANSITIONS · MANOEUVRES · CONTROLS · WIND · POLAR_REFERENCE · ANIMATION · SPRITE_ENVELOPE
           README.md
sloop-88/  sloop88IsoRig.js            27.0 m masthead sloop with inner forestay — globalThis.Sloop88Iso
           sloop88IsoRig.gameplay.json same sections; 8 DECK polygons over four levels, 14 INTERACT, 8 cleats — her first sidecar
           sloop88IsoRig.sailing.json  same sections, plus the staysail, the heavy-weather set and the platform
           README.md
_sailKit.js                            the writer that produced every JSON here (SAIL_KIT) — re-run it, never edit a number
_sidecarExport.js                      the stamp (derivedFromRigSha256 from the rig bytes as read)
SHA256SUMS.txt                         every rig and sidecar hash
```

Lands in `docs/art/rigs/` (rigs) and `docs/art/rigs/gameplay/` (both sidecars) — filenames already match the art
workspace (`Art/`, `Art/gameplay/`), so import is a pure copy. **The two rigs are byte-identical to the copies in
the art workspace; the builder pages (`Sloop 30 Iso.dc.html`, `Sloop 88 Iso.dc.html`) check both committed
sidecars against the served rig on load.**

## Stamps

| | rig sha256 | gameplay | sailing |
| --- | --- | --- | --- |
| Sloop 30 | `f3b545c2e526…` (unchanged since the sloop-30 kit) | `c0ac60c81cdf…` — content identical to the sloop-30 kit's file, re-stamped from the same bytes | `8b4c627fd826…` |
| Sloop 88 | `aed8100cb8ad…` | `0258d84d4b03…` | `c1c6a5ec3438…` |

Both sidecars of a hull carry the **same** `derivedFromRigSha256`, taken from the rig source read in the same
run that generated them. If either disagrees with `sha256 <rig>.js`, the hull was reshaped — regenerate, do not
patch.

## The contract the hulls share (with the rest of the fleet)

**32 px = 1 m.** Fixed ¾ turntable, **elev 40°** (30–50), 45° steps, 8 headings **N NE E SE S SW W NW** (`dir`
0–7). Flat-facet, z-buffered, ordered dither, depth-edge darkening, no AA, binary alpha, ringless (ADR 0031).
Origin **amidships / canoe-body bottom / centreline**, pinned every heading, pose and paint. **The waterline is not
the pivot row** — `WATERLINE.clip_rule` in each gameplay sidecar says where the shader cuts.

| | Cell | Pivot | LOA / beam | Masthead | DWL above origin | Colourways |
| --- | --- | --- | --- | --- | --- | --- |
| Sloop 30 | 400 × 552 | (200, 432) | 9.4 / 2.98 m | 13.0 m | 0.55 m | 8 presets + mixer |
| Sloop 88 | 1072 × 1504 | (536, 1150) | 27.0 / 6.70 m | 37.0 m | 1.35 m | the same 8, hull for hull |

**The sails are pose, not pixels.** Both rigs take `{awa, aws, main, jib, hoist, furl, cover}` (the 88 adds
`sfurl`, `platform`) and the picture follows one law — `sailPose()`. The sailing sidecar is that law, sampled.

## What the sailing sidecar gives gameplay

Everything in it is computed, not typed: `sailPose()` sampled for every pose number, the loft integrated for the
hull form, the bake measured for the envelope, `INTERACT` read from the geometry sidecar generated in the same run.

| Section | What it carries |
| --- | --- |
| `HULL_FORM` | LWL, waterline beam, canoe-body displacement, Cp, midship area, hull speed, D/L, a displacement curve over draft, the waterline half-breadths (the wake/foam mask) — trapezoidal integration of the rig loft below the DWL. **Canoe body only**; keel and bulb are not integrated. |
| `SAIL_PLAN` | I J P E, areas, upwind area, SA/D, boom length and its clearance over the sole. |
| `AUTO_TRIM` | The builder pages' law verbatim: 18° of attack on the main, 16° on the headsail, until the sheet limits cap them. Every table below is sampled at this trim. |
| `POINTS_OF_SAIL` | in irons · close hauled · close reach · beam reach · broad reach · run · becalmed — the awa bands, the builder presets, and what the rig reports for each (mode, boom, headsail, fill, luff, heel). Only 25° (irons) and 150° (blanketing) are pose-law thresholds; the rest are labels. |
| `TRIM_TABLE` | awa 0–180 in 5° steps at the reference wind — boom, headsail (and staysail), angles of attack, fill, luff, stall, heel, mode. |
| `HEEL` | The law and its constants, the wind that saturates it, and heel over awa × aws (13 × 12). |
| `STATES` | sailing · main_only · headsail_only · motoring · stored, each as render opts; the 88 adds main_and_staysail, heavy_weather (hoist 0.6 + staysail) and the orthogonal platform_up. |
| `TRANSITIONS` | hoist, furl, cover, (staysail furl, platform), door — the opt that moves, its range, the INTERACT that drives it, which winch takes the grind handle. |
| `MANOEUVRES` | The boom / clew path through a tack (40 → −40) and a gybe (170 → −170), sampled: the 30's boom sweeps 44° through irons and 172° through a gybe, 1.815 m over the sole. |
| `CONTROLS` | INTERACT action → rig opt: enter_helm→steer, trim_main→main, trim_jib→jib, hoist_main→hoist, furl_jib→furl, (anchor, fold_platform), with the pose each produces. |
| `WIND` | The awa/aws frame and the true→apparent glue (`aws = √(tws² + v² + 2·tws·v·cos twa)`, `awa = atan2(…)`), plus every pose-law threshold in one place. |
| `POLAR_REFERENCE` | A **reference** polar — true wind in; boat speed, apparent wind, heel and the sprite's mode out — over 10 winds × 15 angles, with best VMG per wind. Model: `min(v_hull·(1−e^(−tws/tws_h))·G(twa), R(twa)·tws)`, inputs from the loft and the sail plan. **Gameplay owns the polar**; this one closes the loop so a first physics pass and the sprite agree. |
| `ANIMATION` | The 8-frame loop (flutter, irons wander, winch handle), the wave (`rock`), the door cue. |
| `SPRITE_ENVELOPE` | Painted bbox per facing for close-hauled at max heel, the run with the boom at 86°, and stored — all inside the cell on both hulls (30: 354 × 493 of 400 × 552; 88: 1008 × 1395 of 1072 × 1504). |

Two numbers to read before wiring anything: the **88 at twa 40 above ~10 kn true reads "in irons"** on the
sprite at the polar's speed (apparent 23–24°, inside the 25° band) — `_confirm.no_go_coupling`; and the
**displacements are canoe body only** (4 875 kg / 89 190 kg) — `_confirm.displacement`.

## Rendering

```js
SloopIso.render(dir, { elev:40, awa:45, aws:12, main:0.7, jib:0.7, hoist:1, furl:0, cover:false,
  doorOpen:0, view:'exterior', steer:0, grind:null, frame:0, rock:false, underbody:false, scheme:'gelcoat-white' });
Sloop88Iso.render(dir, { …the same, plus sfurl:1, platform:1 });      // -> Uint8ClampedArray RGBA, W*H*4
```

`sailPose(opts)` is the law; `poseOf(opts)` adds the wave; `anchors(dir,opts)` projects the deck points through
the pose; `doorMount(dir,opts)` reports the companionway; `renderWheel(dir,opts)` is the wheel layer on its own.
Heel is a render parameter (`heelDeg` overrides, `heel:false` bakes level). Each hull's README has the full
surface.

## Regenerating

From the art workspace's script sandbox, one call rewrites every JSON in this kit AND the copies in
`Art/gameplay/`, stamped from the rig bytes it just read:

```js
(0,eval)(await readFile('Art/_sidecarExport.js'));
(0,eval)(await readFile('Art/_sailKit.js'));
await SAIL_KIT.write({ readFile, saveFile, log, dir:'export/sail-rig-kit/' });
```

Or per hull from the builder page: **GAMEPLAY SIDECAR** and **SAILING SIDECAR** buttons, both stamped through
`_sidecarExport.js`. The two routes produce byte-identical files.

## Not in this kit

- **No baked sheets** — the builder pages bake 8-dir sheets and frame strips of any state.
- **No polar of record.** `POLAR_REFERENCE` is flagged as such in-file; replace the model, keep `WIND.from_true`.
- **No reef points, engine, spinnaker, leeway, anchoring state** — `_excluded` in each sailing sidecar.
- **No interior sidecar** — both sloops cut their own cabins (`view:'cabin'`); the rooms are in `DECK` (`level: cabin` / `saloon` / `lower`) of the gameplay file, single-stamped.
