# Cape Islander Helm — `CapeRig`

Hidden Harbours · Cape Islander · old-school wheelhouse rig.

The 1982 workboat dash: a **mahogany-framed cork panel**, a chrome destroyer wheel with a wood spinner,
**cream gauges** under stainless bezels, banks of **red breakers**, and one brushed binnacle lever. The
brow carries a swappable depth / sonar sounder and **two reserved cutouts — radar & gps** — waiting on
their own rigs. Same working layout as the Novi, dressed as a boat that's had a life of work and add-ons.

## Quick start

Open **`Cape Islander Helm.dc.html`**. Drag the wheel, swing the lever, turn the key, flip the breakers,
tap the sounder to swap depth/sonar, tap the radar/gps mounts to fit or blank, reorder the brow slots.

```
cape-islander-helm/
├─ README.md
├─ Cape Islander Helm.dc.html  ← interactive preview + PNG exports
├─ support.js                  ← preview runtime (do not edit)
└─ Art/
   ├─ leverRig.js  depthRig.js  fishRig.js  compassRig.js   ← shared instruments (load first)
   └─ capeRig.js                                             ← this helm (load last)
```

## Rig API — `window.CapeRig`

```js
CapeRig.render({ running, drive, steer, fuel, rpm, deck, spot, night, blink,
                 finder, radar, gps, layout, compass, heading, phase }) // → HTMLCanvasElement (600×548)
```

Shared signals as in `../README.md`, plus the same **three-slot brow** as the Novi:

| Param | Type | Notes |
|---|---|---|
| `radar` | bool | radar cutout fitted (standby screen) vs blanked (cork cover) |
| `gps` | bool | gps cutout fitted vs blanked |
| `layout` | perm of `['sounder','radar','gps']` | brow slot order, LEFT → CENTRE → RIGHT |
| `compass` | `none`\|`dome`\|`flush` | dome takes the centre brow cluster; flush sinks in the dash |

### Composite the lever on top

```js
ctx.drawImage(CapeRig.render(opts), 0, 0);
const lv = LeverRig.render(drive, 'chrome');   // cape = chrome finish
ctx.drawImage(lv.c, Math.round(CapeRig.DRIVE.px - lv.px),
                    Math.round(CapeRig.DRIVE.pivotY + CapeRig.TOPPAD - lv.py));
```

### Hit-geometry (rig-local; subtract `TOPPAD` from pointer Y)

`SW.start`, `SW.deck`, `SW.spot`; `WHEEL` (drag-to-steer ÷ `wheelTurn`); `driveHandle`/`DRIVE.hitR`
(lever); and per brow slot **`CapeRig.slotBox(i, portrait)`**, resolving `layout[i]` to `finder`/
`radar`/`gps` as on the Novi. `maxSteer = 45°`, `DEG = π/180`.

### Radar / GPS placeholders

Placeholders until their own rigs ship — fitted shows a standby screen in the cutout, blanked shows the
cork cover. The mount boxes are exposed as **`CapeRig.RADAR` / `CapeRig.GPS`** with paint hooks so a
future rig can draw into the exact cutout.

## Export & integration

- **↓ STEER SET** (5 frames) and **↓ STATE SHEET** (stop/run × astern/neutral/ahead).
- Preview state (incl. brow `layout`) persists to `localStorage['hh.capeHelm']` — preview-only.
- Offline-safe; only the preview heading fonts need the network.
