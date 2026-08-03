# Chartplotter — `NavRig`

Hidden Harbours · dash instrument · diegetic GPS / chart navigator.

The fourth helm instrument. Same graphite case and `drawUnit` contract as the sounders and radar, but a
**landscape glass** (real plotters are wide) painting a live vector chart of the *same* water as the
paper *Chart of the Hidden Harbours* — Greywick, Coddle Cove, the Sunkers, the Drownded flats, the
Banks, the Rips. Graduated depth shading, land, rocks, lateral buoys, waypoints, the track breadcrumb,
a magenta planned route and AIS-style traffic. Wordmark is original (GN-12 / HARBOUR / CHARTPLOTTER).

**Two faces from one rig:** a compact **console** view (chart + slim data/route bars) and a **max** view
that reveals the advanced kit — layer/tool rail, waypoint & route manager, range/bearing measure line,
tide & current readout, split depth-profile strip.

## Quick start

Open **`Chartplotter.dc.html`**. Pan and range the chart, swap CONSOLE / MAX, north-up vs head-up, mark
waypoints, build a route, drag a measure line, toggle layers, run the tide from low to high water.

```
chartplotter/
├─ README.md
├─ Chartplotter.dc.html   ← interactive preview + state-sheet export
├─ support.js             ← preview runtime (do not edit)
└─ Art/navRig.js          ← the rig
```

## Rig API — `window.NavRig`

```js
NavRig.render(o)                       // → HTMLCanvasElement — 760×480 console · 980×648 max
NavRig.drawUnit(ctx, X, Y, WW, HH, o)  // draw fitted into any box (= paintInto)
```

| Param | Type | Notes |
|---|---|---|
| `view` | `console` | `max` | compact dash face, or the full plotter with rails and manager |
| `cam` | `{x, y}` | chart centre in world px (`WORLD_W`×`WORLD_H`, `PXNM` = 50 px/nm) |
| `rangeNM` | nm | chart range; step through `NavRig.RANGE_STEPS` |
| `orient` | `north` | `head` | north-up or course-up |
| `heading`, `sog` | 0…359, kn | own-ship vector — feeds COG/SOG, ETA and the route legs |
| `waypoints` | array | marked POI; `selWpt` = index selected in the manager |
| `route` | array | planned legs (magenta); length/ETA computed from `sog` |
| `track` | array | breadcrumb of where the boat has been |
| `traffic` | array | AIS-style contacts; `trafficSel` = index selected |
| `layers` | object | per-layer visibility flags (depths, rocks, buoys, track, route, traffic…) |
| `tool` | `pan`|`mark`|`route`|`measure` | active tool in the rail |
| `measure` | `{from, to}` | null | range/bearing line between two world points |
| `tide` | `tideState(phase)` | height, rising flag, set & drift for the tide/current readout |
| `night`, `phase` | bool, s | dusk chart palette · free-running time |

Every list param has a sample generator: `defaultWaypoints()`, `defaultRoute()`, `defaultTrack()`,
`defaultTraffic()`, `tideState(phase)`. Call `render({})` and you get a working chart immediately.

Geometry & math helpers: `makeView(o)`, `w2s` / `s2w` (world ↔ screen), `isLand(x,y)`,
`depthAt(x,y)`, `bearingWorld(a,b)`, `distWorldNM(a,b)`, `routeLenNM(route)`, `layout(X,Y,WW,HH,mode)`
(→ `lcd`, `col`, `keys[4]`, `knobBox`, `topbar`, `botbar`, `brand`), plus `fmtNM`, `fmtDeg`,
`fmtHM`, `fmtPos`, `cardinal8`. Chart data is exposed as `LAND`, `BUOYS`, `ROCKS`, `REGIONS`.

## Export & integration

- **↓ STATE SHEET** exports `Chartplotter_States.png`.
- **On a helm:** the Novi and Cape brows keep a **GPS** slot. Wire it once at boot —
  `NoviRig.paintGps = window.NavRig.paintInto` (or `CapeRig.paintGps`) — and the plotter paints into
  the exact glass rect. Slot is landscape, so it suits the `console` view.
- The chart's depth field is computed once and cached; everything else is per-frame.
- Preview state persists to `localStorage['hh.chartplotter']` — preview-only.
- Offline-safe; only the preview heading fonts need the network.
