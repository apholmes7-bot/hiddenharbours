# Rock Iso, pass three — export

Baked from `Art/pxRockIso2.js` by `Art/_pxRock2Export.js`. Regenerate, never hand-edit.
Rig SHA-256 `3b6ec36d7891893c1b970859b5e9de4c744c6404540d3b888ab3a0119615d58c` is stamped into every JSON here; if it does not match the rig you
are building against, the sheets and the contract have come apart — rebake.

## The rig

`bake/` carries the rig that made everything in this drop, and the export that wrote it:

```
bake/pxRockIso2.js      the rig. Its bytes ARE the digest stamped above.
bake/_pxRock2Export.js  the export — sheets, seats, sidecars, manifest.
bake/pixelLanguage.js   \  the two it loads. Same bytes as the repo's.
bake/pxKit.js            > Load in this order, then the rig, then the export.
bake/pxKit2.js          /
```

The rig is shipped **unchanged** from the bake that produced these sheets, so the digest in every
sidecar still resolves against it. Re-exporting the sidecars (below) does not touch the rig and
does not move the digest.

```js
for (const f of ['pixelLanguage.js','pxKit.js','pxKit2.js','pxRockIso2.js','_pxRock2Export.js'])
  (0,eval)(await readFile('bake/' + f));
await PXROCK2_EXPORT({ createCanvas, saveFile, readFile, log, phase:'sidecars' });
```

`phase` is `jobs` · `sheets` (`{from,to}`, about five per call) · `seats` · `sidecars` · `manifest`.
**`sidecars` writes all sixteen files in one pass** and is the phase to run: a sheet slice only sees
the stones in its own range, so a sidecar written alongside the sheets carries only that slice's
stones. All 75 declared (form, stone) pairs now carry a ground-contact anchor block.

## Layout

`tex/<Form>_<stone>[_<dress>].png` — 8 cols (4 variants, then the same 4 mirrored)
× 3 rows (tide: dry / wet / awash). Cell size per form is in `RockPx.json`.

## Channels

Four co-registered PNGs per sheet:

| file | contents |
| --- | --- |
| `<stem>.png` | albedo, key light baked in |
| `<stem>_unlit.png` | base colour to relight |
| `<stem>_mask.png` | R key · G rim · B depth · A coverage |
| `<stem>_normal.png` | view-space normal (R x, G y-up, B toward camera) |

## Shading

- Sort and occlude on mask.B, not on the sprite's y — a skerry's far head is behind its near one inside one sheet cell.
- Relight = unlit x a THREE-step band (multipliers 0.76 / 1.00 / 1.24), not a smooth ramp. Smooth ramps stop it being pixel art.
- The dynamic key NUDGES the baked band, it does not replace it: take the band from mask.R (>200 lit, <90 shade), take a wanted band from saturate(N.L) (>0.84 lit, <0.30 shade), and move the baked one at most ONE step toward it. Recomputing the band from N.L alone bands the mark relief too and the rock dissolves into speckle.
- mask.G is a back rim, already keyline-correct at 2 px. Add dusk/lantern rim to it, do not replace it.
- Wet and awash rows are ALBEDO changes, not shader ones. Crossfade rows on the tide clock; do not tint the dry row.
- Water, foam, spray and tide-pool fill are NOT baked. The pool rect in the sidecar is where to put them.

## Seats

A rock does not blend itself into the ground — `seat/` does. Each decal is painted by PxKit in
the terrain's own material, so it IS grass or shingle rather than a colour resembling it.
Draw it over the rock on the same pivot. Two-seat decals carry a frontier: place that frontier
on the level's real material boundary, and the rock hides the seam for you.

## Not baked

Sea, foam, spray, tide-pool fill, and cast shadow. The sidecars give you the pool rect, the
hazard radius, the weed line and the footprint to drive all five.
