# Sport Skiff Helm — `SportRig`

Hidden Harbours · sport centre-console · chrome & white-gauge rig.

The console skiff's polished sister — same dash, upgraded. A **chrome destroyer wheel**, **white
gauges** under stainless bezels, one brushed binnacle lever for throttle & shift. No radar, no plotter;
just what the boat runs on. Mechanically identical to the Console Helm, re-skinned in polished stainless.

## Quick start

Open **`Sport Skiff Helm.dc.html`**. Drag the wheel, swing the lever, turn the key, flip the switches,
tap the brow screen to swap depth/sonar.

```
sport-skiff-helm/
├─ README.md
├─ Sport Skiff Helm.dc.html   ← interactive preview + PNG exports
├─ support.js                 ← preview runtime (do not edit)
└─ Art/
   ├─ leverRig.js  depthRig.js  fishRig.js  compassRig.js   ← shared instruments (load first)
   └─ sportRig.js                                            ← this helm (load last)
```

## Rig API — `window.SportRig`

```js
SportRig.render({ running, drive, steer, fuel, rpm, deck, spot, night,
                  blink, finder, compass, heading, phase }) // → HTMLCanvasElement (600×510)
```

Signals follow the shared glossary (`../README.md`) and match the Console Helm one-for-one:
`drive`/`steer` −1…+1, `rpm`/`fuel` 0…1, `heading` 0…359, `finder` = `depth`\|`fish`,
`compass` = `none`\|`dome`, `deck`/`spot`/`night` bools. The rig draws the entire dash except the lever.

### Composite the lever on top

```js
ctx.drawImage(SportRig.render(opts), 0, 0);
const lv = LeverRig.render(drive, 'chrome');   // sport = chrome finish
ctx.drawImage(lv.c, Math.round(SportRig.DRIVE.px - lv.px),
                    Math.round(SportRig.DRIVE.pivotY + SportRig.TOPPAD - lv.py));
```

### Hit-geometry (rig-local; subtract `TOPPAD` from pointer Y)

`SW.start` (ignition), `SW.deck` / `SW.spot` (switch boxes), `WHEEL {cx,cy,r}` (drag-to-steer ÷
`wheelTurn`), `driveHandle(drive)` within `DRIVE.hitR` (lever, → `driveFromPoint`), and the brow box
(→ toggle `finder`). `maxSteer = 45°`, `DEG = π/180`.

## Layout & behaviour

- **Compass:** `none` or `dome` (crown bracket; slides the sounder to port).
- **Finish:** the lever is **chrome**; gauges read **white** under stainless bezels.
- **Night** follows the NIGHT PANEL switch.

## Export & integration

- **↓ STEER SET** (5 frames port→stbd) and **↓ STATE SHEET** (stop/run × astern/neutral/ahead).
- Preview state persists to `localStorage['hh.sportHelm']` — preview-only.
- Offline-safe; only the preview heading fonts need the network.
