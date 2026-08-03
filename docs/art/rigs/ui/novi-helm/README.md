# Novi Helm — `NoviRig`

Hidden Harbours · Novi · modern pilothouse rig.

The current downeast dash: a moulded white-gelcoat console, a stainless destroyer wheel with a black
hub, **black-face gauges with ice-blue arcs**, a carbon strip of backlit rockers, and one stainless
binnacle lever. The brow carries a **swappable depth / sonar sounder**, a **live radar
screen** (`RadarRig`, bundled) and a **gps / plotter mount** that takes `NavRig`. Rendered with Barlow (condensed) chrome in the preview.

## Quick start

Open **`Novi Helm.dc.html`**. Drag the wheel, swing the lever, turn the key, flip the rockers, tap the
sounder to swap depth/sonar, tap the radar/gps mounts to fit or blank, and reorder the three brow slots.

```
novi-helm/
├─ README.md
├─ Novi Helm.dc.html      ← interactive preview + PNG exports
├─ support.js             ← preview runtime (do not edit)
└─ Art/
   ├─ leverRig.js  depthRig.js  fishRig.js  compassRig.js  radarRig.js   ← shared instruments (load first)
   └─ noviRig.js                                                          ← this helm (load last)
```

## Rig API — `window.NoviRig`

```js
NoviRig.render({ running, drive, steer, fuel, rpm, deck, spot, night, blink,
                 finder, radar, gps, layout, compass, heading, phase }) // → HTMLCanvasElement (600×548)
```

Shared signals as in `../README.md`, plus the **three-slot brow**:

| Param | Type | Notes |
|---|---|---|
| `radar` | bool | radar mount fitted (live `RadarRig` PPI) vs blanked (flush black) |
| `gps` | bool | gps mount fitted vs blanked (hook `NavRig` in — see below) |
| `layout` | perm of `['sounder','radar','gps']` | brow slot order, LEFT → CENTRE → RIGHT |
| `compass` | `none`\|`dome`\|`flush` | Novi fits all three (dome takes the centre brow cluster) |

### Composite the lever on top

```js
ctx.drawImage(NoviRig.render(opts), 0, 0);
const lv = LeverRig.render(drive, 'chrome');   // novi = chrome finish
ctx.drawImage(lv.c, Math.round(NoviRig.DRIVE.px - lv.px),
                    Math.round(NoviRig.DRIVE.pivotY + NoviRig.TOPPAD - lv.py));
```

### Hit-geometry (rig-local; subtract `TOPPAD` from pointer Y)

`SW.start`, `SW.deck`, `SW.spot`; `WHEEL` (drag-to-steer ÷ `wheelTurn`); `driveHandle`/`DRIVE.hitR`
(lever); and per brow slot **`NoviRig.slotBox(i, portrait)`** — `portrait` is `true` when that slot
holds the sonar in fish mode. Resolve `layout[i]` to know whether a slot toggles `finder` (sounder),
`radar`, or `gps`.

### Radar & GPS screens

Both brow screens now have real rigs behind them:

- **Radar** — `radarRig.js` travels in this folder. Load it **before** `noviRig.js` and the radar slot
  picks up `RadarRig.paintInto` automatically; no wiring. Blanked shows a flush black screen.
- **GPS / plotter** — the chartplotter lives in `../chartplotter/`. Copy `Art/navRig.js` in, load it,
  and wire it once at boot:

  ```js
  NoviRig.paintGps = window.NavRig.paintInto;   // landscape slot → NavRig 'console' view
  ```

`NoviRig.paintRadar` / `paintGps` stay open as overrides if you want to paint the glass yourself.

## Export & integration

- **↓ STEER SET** (5 frames) and **↓ STATE SHEET** (stop/run × astern/neutral/ahead).
- Preview state (incl. brow `layout`) persists to `localStorage['hh.noviHelm']` — preview-only.
- Offline-safe; only the preview heading fonts (Barlow) need the network.
