# Binnacle Lever — `LeverRig`

Hidden Harbours · single-lever throttle + F/N/R shift · the moving part every helm reuses.

One control that does throttle **and** forward/neutral/reverse shift: push up for AHEAD (the grip
swings forward & away), centre for NEUTRAL, pull down for ASTERN (swings back toward you). It pivots
on a fixed hub. This is the piece each helm composites over its dash, so it ships once here and is
bundled into all four helm folders.

## Quick start

Open **`Lever Rig.dc.html`**. Drag the grip through its travel, switch the housing finish
(graphite / chrome), watch the baked 9-frame strip, download the sprite strip.

```
lever-rig/
├─ README.md
├─ Lever Rig.dc.html     ← interactive preview + strip exporter
├─ support.js            ← preview runtime (do not edit)
└─ Art/
   ├─ leverRig.js        ← the rig the helms composite (`LeverRig`)
   └─ leverRig2.js       ← astern-pose pass (`LeverRig2`) — variant study, same contract
```

## Rig API — `window.LeverRig`

```js
LeverRig.render(sig, spec)   // sig ∈ [-1,+1], spec ∈ 'graphite' | 'chrome'
                             // → { c: HTMLCanvasElement, px, py }
```

`sig` is the same signed value the helms call `drive`: **−1 astern · 0 neutral · +1 ahead**.
`(px, py)` is the **hub pivot inside the returned canvas** — draw so the pivot lands on the mount point:

```js
const lv = LeverRig.render(drive, 'graphite');
ctx.drawImage(lv.c, mountX - lv.px, mountY - lv.py);
```

Helpers:

- `LeverRig.bakeStrip(nFrames, spec) → HTMLCanvasElement` — an astern→neutral→ahead sprite strip.
- `LeverRig.handleOffset(sig) → {dx, dy}` — grip-tip offset from the hub along its arc (for drawing a
  travel guide or attaching a hand).
- `LeverRig.sigFromOffset(dx, dy) → sig` — inverse: turn a drag position into a signal (drag-to-set).

Two finishes: **`graphite`** (matte black console housing — the Console Helm) and **`chrome`**
(polished stainless — Sport, Novi & Cape). No `render` opts beyond `sig`/`spec`; no persistence.

## `LeverRig2` — the astern-pose pass

v1 centred the swing *toward* the operator, so full ASTERN sat 56° off vertical against a 17° camera and
the arm collapsed into a blob behind the grip. `leverRig2.js` re-centres the throw so neutral leans
away and no pose approaches the view axis (apparent length stays ~0.57–0.83 of full at both extremes),
ends the grip in a real domed cap, and bakes a static binnacle **boss + ground plane** into the rig.
Same call shape — `LeverRig2.render(sig, variantId, specOrId) → {c, px, py}` — plus `VARIANTS`/`VLIST`
to compare poses, `boss()`, `gripPt()`, `metrics()` and `bakeStrip(n, vid, spec)`. The preview shows
both side by side; the helms still composite `LeverRig`.

## Export & integration

- **↓ SPRITE STRIP · 9 FRAMES** exports `Lever_<spec>_9.png`.
- Stateless and light — call it every frame with the live `drive` value, layered over the helm sprite.
- Offline-safe; only the preview heading font needs the network.
