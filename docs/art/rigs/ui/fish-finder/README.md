# Fish Finder — `FishRig`

Hidden Harbours · sonar instrument · diegetic fish-finder rig.

The upgrade sounder — same flush-mount cutout as the Depth Sounder, but the glass runs a live colour
scan: depth & water temp up top, a **scrolling bottom contour** sitting at the measured depth, and
**fish marks the game can drop anywhere on the water column at any size**. A boat carries the plain
depth read *or* the sonar, never both and never a floating HUD.

## Quick start

Open **`Fish Finder.dc.html`**. Click open water to drop a fish, drag a mark to move it, scrub depth /
temp / range, toggle fish-ID and night.

```
fish-finder/
├─ README.md
├─ Fish Finder.dc.html    ← interactive preview + state-sheet export
├─ support.js             ← preview runtime (do not edit)
└─ Art/fishRig.js         ← the rig
```

## Rig API — `window.FishRig`

```js
FishRig.render({ depth, ft, night, tempC, range, fish, sel, fishID, phase }) // → HTMLCanvasElement (W×H)
```

| Param | Type | Notes |
|---|---|---|
| `depth` | metres | current depth; the bottom contour sits here |
| `ft` | bool | feet vs metres |
| `night` | bool | amber backlight vs day (colour) |
| `tempC` | °C | water temperature |
| `range` | metres | vertical scale — snap to `FishRig.RANGE_STEPS` |
| `fish` | `[{x, y, size}]` | marks; `x,y` normalised **0…1** in the sonar area, `size` ≈ 0.5–2.5 |
| `sel` | int | index of a highlighted mark (or −1) |
| `fishID` | bool | draw fish-ID arches/icons |
| `phase` | seconds | free-running time — scrolls the scan |

Helpers:

- `FishRig.layout(x, y, w, h) → { sonar, status, buttons[3] }`. `buttons[0]` = fish-ID, `buttons[1]` =
  range **+**, `buttons[2]` = range **−**; tap `status` = night; `sonar` is the click/drag zone.
- `FishRig.fishGeom(layout, fish) → { cx, cy, half }` — screen geometry of a mark (for hit-testing).
- `FishRig.RANGE_STEPS` (allowed scales), `FishRig.M2FT`, `FishRig.defaultSchool()` (a starter set),
  `FishRig.fmtDepth(m, ft)`.
- Fish live in **normalised column space**, so a mark keeps its depth ratio when you change `range` —
  the game places marks as `(x, y, size)` and the rig turns `y·range` into a depth.

## Export & integration

- **↓ STATE SHEET** = day/night × shoal/mid/deep.
- Fitted inside a helm's brow (same cutout as the Depth Sounder); standalone it draws itself.
- Preview state (incl. placed fish) persists to `localStorage['hh.fishFinder']` — preview-only.
- Offline-safe; only the preview heading fonts need the network.
