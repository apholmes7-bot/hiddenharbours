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
└─ Art/leverRig.js       ← the rig
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

## Export & integration

- **↓ SPRITE STRIP · 9 FRAMES** exports `Lever_<spec>_9.png`.
- Stateless and light — call it every frame with the live `drive` value, layered over the helm sprite.
- Offline-safe; only the preview heading font needs the network.
