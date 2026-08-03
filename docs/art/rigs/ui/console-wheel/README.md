# Console Wheel — `WheelRig`

Hidden Harbours · the wheel the **player grabs** · spin physics + pixels in one rig.

`consoleRig.js` draws a wheel as part of its dash. This is the wheel as its own control layer — one
destroyer wheel, own 256×256 cell, own hub pivot, nothing else in the frame — exactly the way
`tillerRig.js` is the dory's tiller. It owns the **spin model** as well as the art, so the game, the
helm view and any bake all agree on what a given wheel angle means.

**Drawn at angle, not rotated.** A baked sprite spun with a rotation matrix carries its highlights
around with it, so the light appears to orbit the boat. Here every pixel is solved for what it belongs
to — rim section, spoke, turned knob, hub — and shaded from a fixed upper-left key with a cylinder
cross-section term. Nothing is stroked, so there are no gaps and every pixel lands on a ramp step.

## Quick start

Open **`Console Wheel.dc.html`**. Drag the wheel and let go — it coasts and *holds*, like cable steer.
Change lock-to-lock, swap the rim finish, watch the rudder angle and the per-knob clicks, and see it
mounted on the console skiff's iso hull at all 8 headings.

```
console-wheel/
├─ README.md
├─ Console Wheel.dc.html   ← interactive preview + sprite/layer exports
├─ support.js              ← preview runtime (do not edit)
└─ Art/
   ├─ wheelRig.js          ← the wheel (256×256 cell, hub pivot at 128,128)
   └─ consoleIsoRig.js     ← the console skiff hull, for the mounted iso view
```

## Rig API — `window.WheelRig`

```js
WheelRig.render({ deg | steer, turns, rim })  // → HTMLCanvasElement (256×256)
WheelRig.paint(ctx, cx, cy, o)                // draw with the HUB on (cx, cy)
WheelRig.sheet(n, o)                          // n frames across the lock, one row
```

| Param | Type | Notes |
|---|---|---|
| `deg` | degrees | wheel angle. Wins over `steer` when both are given |
| `steer` | −1…+1 | port…stbd as a fraction of lock (`degFromSteer`) |
| `turns` | turns | lock-to-lock **each way** — default `1.5` (cable steer, 7 m skiff) |
| `rim` | `rubber`|`teak`|`steel` | finish — `RIMS` carries name + note for each; `rubber` is stock |

### Spin model — rig-owned, so it is the same everywhere

```js
let s = { deg: 0, vel: 0 };
s = WheelRig.step(s, dt, { turns: 1.5, friction: 2.4, selfCentre: 0 });
// → { deg, vel, hit, steer, clicks }
motorAngle = WheelRig.rudderDeg(s.steer);       // ±32° outboard swing
```

- coasts with friction and **holds** where released — a working cable helm has no return spring;
  `selfCentre > 0` opts into a springy arcade feel instead.
- **hard stop at lock** with a small bounce; `hit` = ±1 on the tick it hits, for the thunk sound.
- `clicks(deg)` = knobs passed since centre — the per-knob tick the audio hooks into.
- `steerFromDeg` / `degFromSteer` / `lockDeg(turns)` / `maxRudder` (32°) convert between spaces.

Constants: `W`, `H`, `pivot`, `R`, `RIMW`, `KNOBS` (8), `SPOKES` (8), `KNOBL`, and the palettes
`GOLD` (the cove-gold king spoke), `STEEL`, `GRAPH`, `RUBBER`, `TEAK`.

### Mounted on the hull (iso view)

`ConsoleIso.renderWheel(dir, { elev, steer, turns })` draws the wheel **in 3D on the console's aft
face** — the spokes turn about the column axis, so the gold king spoke stays readable at every heading.
Mount point is `ConsoleIso.wheelHub(dir, rock)`; `WHEEL_LOCK` matches this rig's default.

## Export & integration

- **↓** the 9-frame grab strip (`WheelGrab_<rim>.png`) and the 5 × 8 iso layer sheet
  (`ConsoleIsoWheel_layer.png`, steer × heading).
- Draw with the **hub on the mount point** — `paint(ctx, hubX, hubY, o)` handles the offset.
- Preview state persists to `localStorage['hh.consoleWheel']` — preview-only.
- Offline-safe; only the preview heading fonts need the network.
