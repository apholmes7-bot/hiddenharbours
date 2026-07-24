# Console Helm — `ConsoleRig`

Hidden Harbours · centre-console skiff · diegetic control rig. *(the "centre console helm")*

A helm earns a full dash: a wheel that turns, one binnacle lever for throttle & shift, a tach and a
fuel gauge that read what the boat is doing, switches for the lights and the motor, and a fitted brow
sounder. The base centre-console dash — the Sport Skiff Helm is its polished sister.

## Quick start

Open **`Console Helm.dc.html`**. Drag the wheel to steer, swing the lever (up AHEAD / centre NEUTRAL /
down ASTERN), turn the key, flip the switches, tap the brow screen to swap depth/sonar.

```
console-helm/
├─ README.md
├─ Console Helm.dc.html   ← interactive preview + PNG exports
├─ support.js             ← preview runtime (do not edit)
└─ Art/
   ├─ leverRig.js  depthRig.js  fishRig.js  compassRig.js   ← shared instruments (load first)
   └─ consoleRig.js                                          ← this helm (load last)
```

## Rig API — `window.ConsoleRig`

```js
ConsoleRig.render({ running, drive, steer, fuel, rpm, deck, spot, night,
                    blink, finder, compass, heading, phase }) // → HTMLCanvasElement (600×510)
```

Signals follow the shared glossary (`../README.md`): `drive` −1…+1 (astern↔ahead), `steer` −1…+1,
`rpm`/`fuel` 0…1, `heading` 0…359, `finder` = `depth`\|`fish`, `compass` = `none`\|`dome`,
`night`/`deck`/`spot` bools, `blink` 0/1, `phase` seconds. The rig draws the **whole dash including the
fitted sounder and compass** — but **not** the moving lever.

### Composite the lever on top

```js
ctx.drawImage(ConsoleRig.render(opts), 0, 0);
const lv = LeverRig.render(drive, 'graphite');   // console = graphite finish
ctx.drawImage(lv.c, Math.round(ConsoleRig.DRIVE.px - lv.px),
                    Math.round(ConsoleRig.DRIVE.pivotY + ConsoleRig.TOPPAD - lv.py));
```

### Hit-geometry (rig-local; subtract `TOPPAD` from pointer Y)

| Target | Test |
|---|---|
| ignition key | `dist(p, SW.start) < SW.start.r + 9` |
| deck / spot switch | point-in-box `SW.deck` / `SW.spot` |
| wheel | `dist(p, WHEEL) < WHEEL.r + 8` → then steer by drag angle ÷ `wheelTurn` |
| lever | `dist(p, driveHandle(drive)) < DRIVE.hitR` → `driveFromPoint(x,y)` |
| brow sounder | the brow box (shifts when `compass='dome'` slides the sounder to port) → toggle `finder` |

`maxSteer = 45°`, `DEG = π/180`, `wheelTurn` = wheel turns per full lock.

## Layout & behaviour

- **Compass:** `none` or `dome`. The dome sits on the crown and slides the sounder to port to make room.
- **Night** backlights the gauges amber and follows the NIGHT PANEL switch.
- **Finish:** the lever is **graphite** (matte console housing).

## Export & integration

- **↓ STEER SET** (5 frames port→stbd) and **↓ STATE SHEET** (stop/run × astern/neutral/ahead).
- Preview state persists to `localStorage['hh.consoleHelm']` — preview-only; drive from game state.
- Offline-safe; only the preview heading fonts need the network.
