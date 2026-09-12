# The shore-foam corner — one diagnostic plate

**Slot-rider, no code.** The lane charter for the wake photograph asked for one diagnostic plate of
the **shore-foam corner** while the editor was already open, as evidence for a *later* charter:

> owner, 2026-09-11: *fine hairlines at corners when the wave pulses*

**No fix, no register row, no cause is proposed here.** This is a plate and a measurement, parked
for whoever the seat charters next. It is deliberately a sibling of the wake photograph rather than
part of it — nothing in this folder bears on the wake being off centre.

## What was shot

A concave bend of the beach at NineMileCreek — where the shoreline turns from running south-south-east
to running east — at **four moments** of the swash, through the game's own camera in play mode.

| | |
|---|---|
| region | NineMileCreek, hour 11.0 (daylight) |
| camera | (34.70, 11.50, −10), orthographic, half-height 4.5 m |
| plate | 1280 × 960 px → **0.0094 m per pixel** |
| shipped material values, read live | `_FoamWidth` **1.74** · `_SwashAmplitude` **1** · `_SwashEdgeShift` **0.6** · `_SwashSpeed` **0.16** (swash period 6.25 s) |

`PLATE-shore-corner.png` — the four moments, whole frame.
`PLATE-shore-corner-hairline.png` — the bend itself, 2× nearest, the same crop at each moment.

**Registration.** The dry sand 12–52 px landward of each column's own waterline is the same in all
four moments to within **0.0078 luma** across 85 columns. The camera did not move between them, so
the differences below are the sea, not the shutter.

## What it shows

Scanning down 85 columns across the bend (x 900–1240), taking the lowest pixel above 0.60 luma as the
waterline and classifying what lies in the 25 px landward of it:

| moment | waterline | landward of it | hairline width | prominence |
|---|---|---|---|---|
| **shipped-1** | y 613 | **a bright line standing on dark sand — 85 of 85 columns** | **6.0 px = 0.056 m** | **0.295 luma** |
| shipped-2 | y 613 | the lip of a broad bright sheet — 85 of 85 | none | — |
| shipped-3 | y 613 | the lip of a broad bright sheet — 85 of 85 | none | — |
| shipped-4 | y 613 | the lip of a broad bright sheet — 85 of 85 | none | — |

Three things are worth carrying forward, and all three are readings of the pixels, not explanations:

**1 — The waterline does not move between the four moments; what changes is what lies behind it.**
The edge sits at the same y in all four. In three of them the ground landward of the edge is a bright
sheet; in the fourth it is dark, and all that remains of the sheet is a **6-pixel line, 5.6 cm wide at
this scale, standing 0.295 luma above the sand it is drawn on**. The classification is unanimous — 85
columns of 85, in every moment.

**2 — In `shipped-1` the line continues across sand that carries no sheet at all.** Visible in the 2×
crop: the hairline traces the bend and then runs on down-right over ground that is otherwise dry-
coloured, clear of the water body. A line that outlives the sheet it belongs to is the thing the
owner is pointing at.

**3 — A confound for anyone who compares two frames of this sea.** 38.1 % of the whole frame changes
across the four moments, and **74 % of that change lies above y = 520 — the grass and the upper beach,
where there is no water.** That is drifting cloud shadow over the land. Differencing whole frames of
this scene measures the weather; the shore terms have to be isolated by where they are.

## ⚠️ What was asked for and NOT delivered: the wet-edge / foam channel split

The charter asked for the plate with **wet-edge and foam channels split**. That split is **not in this
folder**, and the reason is worth more than the split would have been.

The instrument was a frozen-instant sweep: at a held frame, swash phase is `_Time × _SwashSpeed`, so
setting `_SwashSpeed = wantedCycles / Time.time` walks the run-up through a full cycle with light,
water and geometry otherwise identical — and then the same four phases again with `_FoamWidth` driven
to 0.001 to collapse the foam channel. Eight plates: `phase-0..3`, `wetedge-0..3`.

**All eight came back bit-identical** — one md5, `1a53bbb0af4ba5029ff00aae73e7ce90`, across every arm.
Neither the phase sweep nor the foam collapse reached a single pixel. The property was set on the
first renderer whose `sharedMaterial.shader.name` contains "Water", and that is evidently not the
renderer drawing this water; `Shader.GetGlobalFloat("_SwashAmplitude")` reads **0** while the material
carries **1**, which says the same thing from the other side.

**This is the same trap as family A in the wake photograph** — an isolation that is inert by
construction, whose zero rows look exactly like a measurement. The eight plates were hashed before any
result was claimed, which is the only reason it is recorded as a failure rather than published as a
finding. They are not committed.

The four `shipped-*.png` frames are untouched captures of the shipped material and do differ from one
another, so the plate above stands on its own. **The split needs a slot of its own, and it needs the
renderer identified first.**

## Re-shooting it

Needs a GPU and an open editor on a granted slot. The camera and region are in the table above; the
four frames were taken over consecutive round trips. ⚠️ Consecutive MCP calls land ~6.0 s apart against
a **6.25 s** swash period, so naïvely shooting four in a row samples nearly the same phase four times —
`shipped-2/3/4` are three readings of one moment, which is why they agree so exactly. Only
`shipped-1` caught the other half of the pulse. Shoot phases deliberately, not serially.
