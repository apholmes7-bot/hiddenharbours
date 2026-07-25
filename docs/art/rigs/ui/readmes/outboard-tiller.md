# Outboard Tiller — `TillerRig`

Hidden Harbours · motorised dory · diegetic control rig.

The one control a plain dory earns once it has a motor: a real tiller in the fist. The local sprite
points **straight up at centre** and pivots about the clamp, so steering is a **1:1 rotation about the
pivot** — the rig itself never bakes the angle in. The grip carries start/stop, a red FWD/REV shift
rocker, an integrated throttle band, telltales, and a kill-cord clipped to its switch.

## Quick start

Open **`Outboard Tiller.dc.html`**. Drag the handle to steer, tap the grip button to start/stop,
flip the FWD/REV rocker, run the throttle up. Two download buttons bake PNG sheets.

```
outboard-tiller/
├─ README.md
├─ Outboard Tiller.dc.html   ← interactive preview + PNG exporter
├─ support.js                ← preview runtime (do not edit)
└─ Art/tillerRig.js          ← the rig
```

## Rig API — `window.TillerRig`

```js
TillerRig.render({ throttle, running, press, warn, gear, blink }) // → HTMLCanvasElement (W×H)
```

| Param | Type | Notes |
|---|---|---|
| `throttle` | 0…1 | idle → wide-open; drives the throttle band + engine jitter |
| `running` | bool | engine state (start/stop button glyph) |
| `press` | bool | button-pressed frame |
| `gear` | `F` \| `R` | forward / reverse shift rocker |
| `warn` | bool | telltale warning lamp |
| `blink` | 0 / 1 | blink phase for `warn` |

**Steering is not a render parameter.** The sprite is drawn straight and the caller rotates it about
the pivot:

```js
const sp = TillerRig.render({ throttle, running, gear });
const angle = steer * TillerRig.maxSteer * Math.PI/180;   // maxSteer = 45°
ctx.translate(clampX, clampY); ctx.rotate(angle);
ctx.drawImage(sp, -TillerRig.pivot.x, -TillerRig.pivot.y);
```

- `TillerRig.pivot = {x, y}` — the clamp point (rotation origin) in sprite space.
- `TillerRig.maxSteer = 45` — degrees at full lock.
- `TillerRig.hit.button {x,y,r}` and `TillerRig.hit.shift {x,y,w,h}` — rig-local hit targets to make
  the grip tappable (transform the pointer into sprite space first, as the preview does).

## Exports & integration

- **↓ STEER ARC · 9 FRAMES** (−45°→+45°) and **↓ STATE SHEET** (stop/run × throttle 0/.25/.5/.75/1).
- Preview state persists to `localStorage['hh.tiller']` — preview-only.
- Offline-safe; only the preview heading fonts need the network.
