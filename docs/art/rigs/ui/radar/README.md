# Radar — `RadarRig`

Hidden Harbours · dash instrument · diegetic marine radar.

The third brow instrument, sharing the sounders' **graphite case idiom** and the fish finder's exact
**portrait footprint (480×660)** — so a console brow carries it at the sounder's height with no
re-cutting. The glass runs a live **PPI**: a rotating scan sweep with phosphor persistence, concentric
range rings, a bearing scale, own-ship at centre, and echo targets the *game* places. Day is green
phosphor; night is the fleet's warm amber. Wordmark is original (RD-4 / HARBOUR / RADAR).

## Quick start

Open **`Radar.dc.html`**. Change the range scale, swing HEAD-UP / NORTH-UP, ride the gain / sea / rain
clutter controls, drop an EBL bearing line and VRM ring, arc out a guard zone, click a blip to select it.

```
radar/
├─ README.md
├─ Radar.dc.html          ← interactive preview + state-sheet export
├─ support.js             ← preview runtime (do not edit)
└─ Art/radarRig.js        ← the rig
```

## Rig API — `window.RadarRig`

```js
RadarRig.render(o)                       // → HTMLCanvasElement (480×660), standalone unit
RadarRig.drawUnit(ctx, X, Y, WW, HH, o)  // draw fitted into any box (= paintInto)
```

| Param | Type | Notes |
|---|---|---|
| `targets` | array | the scene — see below. `RadarRig.defaultScene()` for a sample harbour |
| `scaleNM` | nm | range scale; step through `RadarRig.RANGE_STEPS` |
| `orient` | `head` | `north` | head-up (bearings relative) or north-up (true) |
| `heading` | 0…359 | own heading — rotates the picture in north-up, the scale in head-up |
| `sweep` / `phase` | deg / s | scan-line angle and free-running time (persistence + target drift) |
| `gain`, `sea`, `rain` | 0…1 | receiver gain and the two clutter suppressors |
| `rings` | int | range rings drawn (interval = `ringInterval(scaleNM, rings)`) |
| `trails` | bool | echo trails / persistence tails |
| `tx` | bool | transmitting vs STANDBY (standby dims the scope and holds the sweep) |
| `ebl` | deg | null | electronic bearing line |
| `vrm` | nm | null | variable range marker ring |
| `guard` | `{on, from, to, near, far}` | guard-zone arc — bearings in deg, near/far as 0…1 of scale |
| `sel` | index | selected target (its data goes to the readout panel) |
| `night`, `blink` | bool, 0/1 | amber night scope · alarm blink phase |

### Targets are gameplay, not art

```js
{ brgTrue: 118, rngNM: 3.6, size: 1.25, kind: 'vessel', crs: 300, spd: 1.1 }
```

`kind` ∈ `RadarRig.KINDS` = `vessel` · `land` · `buoy` · `rain` · `flock`; each paints its own echo
character (hard bright return, ragged coastline, small crisp pip, soft blotch, speckle). `size` scales
the blip; `crs`/`spd` drive drift and the trail direction. Place whatever the world holds.

Helpers: `layout(X,Y,WW,HH)` → `{lcd, col, buttons[3], brand, status, scope, data}` ·
`blipXY(scope, target, o)` → screen position of an echo · `pickAt(scope, px, py, o)` → `{brgTrue, rngNM}`
under the pointer · `inGuard(target, o)` → guard-zone alarm test · `screenAngOf`, `ringInterval`,
`fmtNM`, `fmtDeg`, `cardinal8`, `RANGE_STEPS`, `C8`, `defaultScene()`.

## Export & integration

- **↓ STATE SHEET** exports `Radar_States.png`.
- **On a helm:** Novi and Cape Islander auto-fit it — load `radarRig.js` before the helm rig and the
  radar brow slot picks up `RadarRig.paintInto` on its own. No wiring needed.
- Side pushers are MODE / RANGE ▲ / RANGE ▼ (`layout().buttons`).
- Preview state persists to `localStorage['hh.radar']` — preview-only.
- Offline-safe; only the preview heading fonts need the network.
