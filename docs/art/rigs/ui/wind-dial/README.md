# Wind Dial — `WindRig`

Hidden Harbours · wind instrument · diegetic wind rig. Helm instruments plan, slice 4 (the art half).

The wind as the boat's own glass reads it. **Two grades from one dial layout**, each in two sizes:

| Grade | Face | Shows | Night |
|---|---|---|---|
| 1 · `dial` | black analog face in a graphite case | a boat outline, bow up; **one needle on the apparent wind angle**; the scale ring red to port and green to starboard, with the close-hauled sectors (25°–50°) printed bright; a two-drum window with **apparent wind speed** in whole knots | red bulb (like the compass) |
| 2 · `display` | grey-green LCD in the same case | the same dial with a segmented pointer; **apparent angle and speed, true angle and speed, and the Beaufort force**; an `APP` / `TRUE` tag says which angle the pointer follows | amber backlight (like the sounders) |

| Size | Canvas | Use |
|---|---|---|
| `card` | 340 × 366 (the compass card's canvas) | the raised card, when the player clicks the band piece |
| `piece` | 112 × 144 (`HelmBandGeometry.CompassW` × band `H`) | the band piece; the dial sits in the same 80 px circle as the flush compass |

The wordmark (TELLTALE) is original.

## Quick start

Open **`WindDial.dc.html`**. The sliders set the *true* wind and the boat's speed; the page works out
the apparent wind with `SailDrive.ResolveWind`'s own steps, so every state you can reach is one the
game can produce. Swap grade, card or piece, day or night, and (on grade 2) which angle the pointer
follows. Tap the glass for night.

```
wind-dial/
├─ README.md
├─ WindDial.dc.html        ← interactive preview + state-sheet export
├─ windRig.sidecar.json    ← needle pivot, angle range, text boxes, printing constants (checked copy of layout())
├─ support.js              ← preview runtime (do not edit; same file as compass/)
└─ Art/windRig.js          ← the rig
```

## Rig API — `window.WindRig`

```js
WindRig.render({ grade, size, night, awa, aws, twa, tws, hasTrue, follow }) // → HTMLCanvasElement
WindRig.paintAt(ctx, x, y, opts)        // native size, top-left at (x, y)
WindRig.paintInto(ctx, x, y, w, h, opts) // centred at the largest WHOLE scale that fits
WindRig.layout(grade, size)             // → { w, h, needle, face, boxes, hits } in native px
```

| Param | Type | Notes |
|---|---|---|
| `grade` | `dial` \| `display` | grade 1 analog, grade 2 LCD |
| `size` | `card` \| `piece` | raised card or band piece |
| `night` | bool | follows the helm NIGHT PANEL in game |
| `awa` | −180 … 180 | apparent wind angle off the bow, **+ = starboard** |
| `aws` | kn ≥ 0 | apparent wind speed |
| `twa`, `tws` | as above | true wind; drawn by grade 2 only |
| `hasTrue` | bool | grade 2 shows dashes for true and Beaufort when false |
| `follow` | `apparent` \| `true` | grade 2's one button: which angle the pointer (and the piece's speed) reads |

Helpers: `beaufort(kn)`, `fmtDrum`, `fmtKn`, `fmtAng`, `side`, `clampAng`, `dir`; constants `CLOSE_HAULED`,
`CALM_KN`, `KN_MAX`.

## Where the numbers come from

Every value is one `SailDrive.ResolveWind` already returns, through `HelmWind.TryRead`'s grade gate
(`Core/Boats/HelmWind.cs`). The rig computes no wind.

| Drawn | Source |
|---|---|
| needle (grade 1), pointer in `APP` (grade 2), drum | `HelmWindReading.ApparentAngleDeg`, `.ApparentKn` |
| TWA / TWS rows, pointer in `TRUE` | `.TrueAngleDeg`, `.TrueKn` (only when `.HasTrue`, so grade 2 and up) |
| Beaufort `F n` | `WindReadout.Beaufort(SailDrive.ToMetresPerSecond(TrueKn))`, the game's one table. The rig keeps a preview copy of that table; the port must call the game's |

**Angle convention:** signed, ±180, off the bow, **positive = wind over the starboard bow**, so the
needle's screen direction is `(sin a, −cos a)` from the pivot: 0 straight up at the bow, +90 to the
right, ±180 straight down. That is the sails' own convention, so the needle and the sails cannot
disagree (bug K1).

**Calm:** below `CALM_KN` (1.0 kn, about Beaufort 0) the LCD shows `---` for that angle and hides the
pointer. An analog needle has no off state, so the **host holds the last angle it drew above
`CALM_KN`**; the rig itself is stateless and draws whatever angle it is handed.

## Porting notes (for ui-ux)

- **The port is a C# renderer like `CompassRigRender`:** this file is the immutable source (ADR 0025,
  Option A). Not in this PR: no C# port, no band layout change, no Def change.
- **Geometry comes from `windRig.sidecar.json`**, a checked copy of `layout()` for all four grade ×
  size pairs: needle pivot and radii, the face box, and every text box with its format (`drum2`,
  `side+ang3`, `kn4`, `F+int`, `APP|TRUE`). `derivedFromRigSha256` is the sha256 of `Art/windRig.js`
  as LF bytes (the file is pinned `eol=lf`); a rig edit that does not re-stamp it fails the check.
- **Faces bake once, the needle draws per repaint.** The rig caches each static face (case, ring,
  graduations, boat, legends) per size × night; only the needle or pointer, the drum digits and the
  LCD numbers change. Repaint only when the angle moves 2° or the speed 0.5 kn (plan §3), with the
  digits from a pre-built string table (no allocation per frame, rule 7).
- **Everything is fillRect or a plain alpha fill.** The colour ring and the glass highlights are
  pixel scans over an annulus (`arcBand`), the 7-segment digits are rectangles, the LCD backing is one
  flat colour per row. No canvas path, gradient or composite mode, so nothing needs a coverage trick.
- **The band piece's housing** copies `HelmBandPlateRender.Blank` (inset 4/8, radius 6, rim 2, the
  same rim and face colours), so the port may call `Blank` and paint only the dial into it. The dial
  circle is the flush compass's box: centre (56, 72), the 80 × 80 at (16, 32).
- **Grade 2 has one button.** `layout('display','card').hits.follow` is the dial area of the glass:
  a press there swaps `follow`. Where the choice is kept is the host's (it is not saved by this rig).
- **Close-hauled marks are printing, not data.** Grades 1 and 2 print a fixed 25°–50° sector. The
  boat-specific no-go wedge from sail data is grade 3 (plan §3), not drawn here.

## Evidence

Plates in `Evidence~/wind-dial/` (written by `plates.html` there, from this rig):

- `wind-dial_card-states.png` — raised card: dial day, dial night, display day, display night, across
  five winds (a beat to starboard, a port reach, a starboard broad reach, motoring into a light head
  wind, and a calm).
- `wind-dial_piece-states.png` — the same at band-piece size, shown at 3×.
- `wind-dial_display-follow.png` — grade 2's pointer following apparent, then true.
- `wind-dial_needle-sweep.png` — the needle at every 45° from −180 to 180.
- `wind-dial_band-context-1x.png`, `-2x.png` — the flush compass piece beside both wind pieces, day
  over night, at k = 1 and k = 2.

`plates.html` also runs 21 self-checks (the needle lands at the angle on both sizes, port red and
starboard green, drum rounding, side letters, calm dashes, Beaufort against WindReadout's bounds, and
the transcribed resolve's at-rest and beating cases). `check-sidecar.js` there is the node-only check
that the sidecar equals `layout()` and carries the rig's sha.
