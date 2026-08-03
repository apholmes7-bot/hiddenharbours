# Hidden Harbours — Boat UI Rigs

Every piece of **in-world UI the boats carry**: the start-of-game watch, the outboard tiller, the
steering wheel, the single-lever binnacle control, the depth & fish sounders, the radar, the
chartplotter, the compass, and the four helm dashes that assemble them. **Thirteen parametric rigs**,
each with its own interactive preview and README, one folder apiece.

Nothing here is hand-pixeled per frame. Every rig is a **procedural renderer** — you hand it a few
parameters (heading, depth, throttle, RPM, range scale, a list of radar targets…) and it draws the
sprite. No bakes to keep in sync; a new state is a new argument, and PNG sheets fall out of the
previews' download buttons when the engine wants flats.

---

## The diegetic rule

There is no floating HUD in Hidden Harbours. **In-world UI is only ever a real object that carries
it** — a clock, a gauge, a dial, a screen bolted to a dash, a wheel you grab. A bare dory shows
nothing; a motor earns one tiller; a rigged console earns the whole dash. Depth is not a number on the
screen edge — it lives on the glass of a sounder screwed to the console. Every rig here obeys that
rule, and every brand wordmark on them is original (not lifted from a real maker).

---

## Two kinds of rig

**Instruments & controls** — one real object each, usable on their own or dropped into a helm:

| Rig | Global | Folder | Role |
|---|---|---|---|
| Watch Face | `WatchRig` | `watch-face/` | resin digital watch — game clock, start-of-game screen |
| Outboard Tiller | `TillerRig` | `outboard-tiller/` | the one control a motorised dory shows |
| Console Wheel | `WheelRig` | `console-wheel/` | the wheel the player grabs — owns the spin physics |
| Binnacle Lever | `LeverRig` | `lever-rig/` | single-lever throttle + F/N/R shift; every helm reuses it |
| Depth Sounder | `DepthRig` | `depth-finder/` | flush-mount depth read + shallow alarm |
| Fish Finder | `FishRig` | `fish-finder/` | upgrade colour sonar; same cutout as the sounder |
| Radar | `RadarRig` | `radar/` | live PPI scope, game-placed echo targets, guard zone |
| Chartplotter | `NavRig` | `chartplotter/` | vector chart, waypoints, routes, tide, AIS traffic |
| Compass | `CompassRig` | `compass/` | heading; dome bracket or flush Ritchie |

**Helms** — a full dash that composes the instruments above plus its own hull-specific console art:

| Rig | Global | Folder | Boat |
|---|---|---|---|
| Console Helm | `ConsoleRig` | `console-helm/` | centre-console skiff (the "centre console helm") |
| Sport Skiff Helm | `SportRig` | `sport-skiff-helm/` | polished sister of the console skiff |
| Novi Helm | `NoviRig` | `novi-helm/` | modern downeast pilothouse |
| Cape Islander Helm | `CapeRig` | `cape-islander-helm/` | 1982 old-school wheelhouse |

---

## Shared conventions

- **Load** `<script src="Art/<name>Rig.js">` — it registers `window.<Name>Rig`. No modules, no build step.
- **`render(opts) → HTMLCanvasElement`** on every rig. The rig is **stateless**: the same opts always
  draw the same sprite. The game owns the state; the rig only draws. (`WheelRig` is the one rig that
  also owns *physics* — `step(state, dt)` returns the next state; it still never stores it.)
- **Screen instruments** additionally expose **`drawUnit(ctx, X, Y, WW, HH, o)`** (aliased `paintInto`)
  so a helm can fit them into an arbitrary glass rect, and **`layout(X, Y, WW, HH)`** so the same box
  yields hit-boxes for the pushers and the glass.
- **Pixel art:** no anti-aliasing (`ctx.imageSmoothingEnabled = false`, `image-rendering: pixelated`),
  KTC master palette, procedural 7-segment/needle/card/phosphor art — no external image assets.
- **Night** is a parameter, not a separate rig. On a helm it follows the **NIGHT PANEL** switch and
  floods the instruments' backlights (amber sounders, amber radar scope, dusk chart, red compass bulb,
  ice-blue Novi gauges).
- **Preview state** persists to `localStorage` under `hh.*` keys (listed per rig). That is a
  convenience of the preview only — **ignore/strip it in production**; drive the rig from game state.
- **Offline:** the canvas render is fully self-contained. Only the previews' heading fonts (IM Fell /
  Barlow, from Google Fonts) touch the network, and they style the preview chrome, never the sprite.

---

## How a helm composes the instruments

Every helm folder bundles the instrument rigs it needs. **Load order matters** — the shared
instruments first, the helm rig last (the previews already do this):

```html
<script src="Art/leverRig.js"></script>
<script src="Art/depthRig.js"></script>
<script src="Art/fishRig.js"></script>
<script src="Art/compassRig.js"></script>
<script src="Art/radarRig.js"></script>     <!-- novi / cape brow -->
<script src="Art/consoleRig.js"></script>   <!-- or sportRig / noviRig / capeRig -->
```

Then draw in two passes:

```js
const R = window.ConsoleRig;
// 1. the whole static dash — console, wheel, swept gauges, fitted sounder/radar/compass screens
ctx.drawImage(R.render({ running, drive, steer, fuel, rpm, deck, spot, night,
                         blink, finder, compass, heading, phase }), 0, 0);
// 2. the ONE moving part the helm rig leaves out — the binnacle lever — composited on top
const lv = window.LeverRig.render(drive, 'graphite');       // 'chrome' on sport/novi/cape
ctx.drawImage(lv.c, Math.round(R.DRIVE.px - lv.px),
                    Math.round(R.DRIVE.pivotY + R.TOPPAD - lv.py));
```

The helm rig renders the sounder, the radar and the compass **internally** (they are fitted to the
dash) and exposes hit-geometry so you can make the panel interactive — see each helm's README.

**Brow screens on the Novi & Cape.** The radar slot auto-detects `RadarRig.paintInto` — load
`radarRig.js` before the helm and it just appears. The GPS slot takes one line at boot:

```js
NoviRig.paintGps = window.NavRig.paintInto;   // or CapeRig.paintGps
```

**The wheel, twice.** Each helm draws its own dash wheel (correct for that console's art). `WheelRig`
is the *grabbable* wheel — spin physics, lock-to-lock, per-knob clicks, rudder angle — used for the
player's control layer and for the iso hull mount. Both agree on `steer` −1…+1.

---

## Signal glossary

| Signal | Range | Meaning |
|---|---|---|
| `drive` | −1 … +1 | single lever: −1 astern · ~0 neutral · +1 ahead (throttle **and** F/N/R shift in one) |
| `steer` | −1 … +1 | wheel / tiller: −1 port · 0 amidships · +1 stbd (`maxSteer` = 45° at full lock) |
| `deg` / `turns` | degrees / turns | `WheelRig` wheel angle and lock-to-lock each way (default 1.5) |
| `rpm` | 0 … 1 | tachometer sweep (game derives it; previews use `0.11 + 0.89·|drive|` when running) |
| `fuel` | 0 … 1 | fuel gauge; < 0.13 blinks the low-fuel telltale |
| `heading` | 0 … 359 | compass card, radar bearing scale, chart orientation |
| `finder` | `depth` \| `fish` | which sounder is fitted in the brow |
| `compass` | `none` \| `dome` \| `flush` | compass unit fitted (flush only on Novi & Cape) |
| `radar`, `gps` | bool | Novi/Cape brow screens fitted vs blanked |
| `layout` | perm of `['sounder','radar','gps']` | Novi/Cape brow slot order (LEFT/CENTRE/RIGHT) |
| `scaleNM` / `rangeNM` | nm | radar range scale · chart range (`RANGE_STEPS` on each rig) |
| `orient` | `head` \| `north` | head-up or north-up, on radar and chartplotter alike |
| `targets` / `traffic` | array | radar echoes · AIS contacts — **gameplay places these** |
| `night` | bool | night panel — backlights the instruments |
| `blink` | 0 / 1 | shared blink phase for alarms/telltales (game toggles ~2–3 Hz) |
| `phase` | seconds | free-running time for sonar scroll, radar persistence, idle animation |

---

## Folders

```
ui-rigs/
├─ README.md               ← this file
├─ watch-face/             ├─ depth-finder/       ├─ console-helm/
├─ outboard-tiller/        ├─ fish-finder/        ├─ sport-skiff-helm/
├─ console-wheel/          ├─ radar/              ├─ novi-helm/
├─ lever-rig/              ├─ chartplotter/       └─ cape-islander-helm/
│                          └─ compass/
```

Each folder is **standalone**: open its `*.dc.html` in any browser (double-click — no server) and it
runs, because `support.js` and every `Art/*.js` it needs travel with it. The instrument previews
cross-link to the helms in prose; those links resolve inside the full project, not across these
isolated folders.

*Not in this package:* the screen-space HUD mockups (`Hidden Harbours UI`) — that is 2D interface art
on PNG assets, not a diegetic boat rig.
