# Compass — `CompassRig`

Hidden Harbours · heading instrument · diegetic compass rig.

Its own rig with **one parameter: the boat's heading**. Two real units off the same card — a **black
bracket dome** that sits on the crown, and a **white flush Ritchie** that sinks into the dash. The card
stays true to north; the case and its **red lubber line** turn with the hull, and you read the heading
at the lubber line (12 o'clock, the bow). Night floods the bulb red.

## Quick start

Open **`Compass.dc.html`**. Swing the heading (or hit SWING to come about), swap DOME/FLUSH, tap the
glass for night.

```
compass/
├─ README.md
├─ Compass.dc.html        ← interactive preview + rose/state export
├─ support.js             ← preview runtime (do not edit)
└─ Art/compassRig.js      ← the rig
```

## Rig API — `window.CompassRig`

```js
CompassRig.render({ form, heading, night }) // → HTMLCanvasElement (W×H)
```

| Param | Type | Notes |
|---|---|---|
| `form` | `dome` \| `flush` | crown bracket dome, or flush-mount Ritchie |
| `heading` | 0 … 359 | boat heading in degrees (card holds north; case rotates) |
| `night` | bool | red bulb lit (follows the helm NIGHT PANEL in game) |

Helpers:

- `CompassRig.C8` — the 8 cardinal labels `['N','NE','E','SE','S','SW','W','NW']`.
- `CompassRig.cardinal8(heading)` → nearest label; `CompassRig.fmtDeg(heading)` → 3-digit string (`045`).
- `CompassRig.norm(heading)` → wrap into `[0, 360)`.
- The nearest cardinal index is `Math.round(norm(h)/45) % 8`.

## Mounting notes

- **Dome** sits on the crown — it takes the centre brow cluster's place, so the sounder slides aside.
- **Flush** sinks in the dash, leaving the brow's glass mounts free up top.
- In game the **night** state is not a separate control — it follows the helm's NIGHT PANEL switch.

## Export & integration

- **↓ ROSE SET** = the 8 canon headings; **↓ STATE SHEET** = dome/flush × day/night.
- On a helm this is rendered inside the helm rig (`compass` = `none`/`dome`/`flush`); standalone it
  eases toward the target heading itself for the preview.
- Preview state persists to `localStorage['hh.compass']` — preview-only.
- Offline-safe; only the preview heading fonts need the network.
