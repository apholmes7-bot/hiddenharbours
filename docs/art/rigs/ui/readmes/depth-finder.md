# Depth Sounder — `DepthRig`

Hidden Harbours · dash instrument · diegetic depth rig.

One small flush-mount sounder that fits both centre-console dashes. A **procedural 7-segment LCD** —
depth, water temp, a shallow-water flag, metric/imperial units, all drawn from parameters. Day is a
positive grey-green panel; night lights the amber backlight. Drop the depth below the shallow set-point
and the reading flashes SHALLOW. This is the **basic** brow instrument; the Fish Finder is the upgrade
that drops into the same cutout.

## Quick start

Open **`Depth Finder.dc.html`**. Scrub depth, toggle metres/feet, set the shallow alarm, tap the glass
for night mode.

```
depth-finder/
├─ README.md
├─ Depth Finder.dc.html   ← interactive preview + state-sheet export
├─ support.js             ← preview runtime (do not edit)
└─ Art/depthRig.js        ← the rig
```

## Rig API — `window.DepthRig`

```js
DepthRig.render({ depth, ft, night, armed, alarm, tempC, blink }) // → HTMLCanvasElement (W×H)
```

| Param | Type | Notes |
|---|---|---|
| `depth` | metres | the measured depth (transducer reading) |
| `ft` | bool | display in feet instead of metres |
| `night` | bool | amber backlight vs day panel |
| `armed` | bool | shallow alarm armed |
| `alarm` | metres | shallow set-point |
| `tempC` | °C | water temperature |
| `blink` | 0 / 1 | blink phase — flash the SHALLOW flag when `armed && depth <= alarm` |

Helpers:

- `DepthRig.layout(x, y, w, h) → { lcd, buttons[3] }` — hit-boxes inside a mount rect. `buttons[0]` =
  units (SET), `buttons[1]` = alarm **+0.5**, `buttons[2]` = alarm **−0.5**; tap `lcd` = toggle night.
- `DepthRig.fmtDepth(m, ft)` / `DepthRig.fmtSet(m, ft)` — the LCD number formatters.
- The preview's standalone screen inset is `X = 6%`, `Y = 11%`, `W = 88%`, `H = 78%` of the canvas —
  match that box (or pass your own to `layout`) when you make the dash instrument tappable.

## Export & integration

- **↓ STATE SHEET** = day/night × deep/at-set/shoal.
- On a helm this is rendered **inside** the helm rig (fitted to the brow); standalone it draws itself.
- Preview state persists to `localStorage['hh.depthSounder']` — preview-only.
- Offline-safe; only the preview heading fonts need the network.
