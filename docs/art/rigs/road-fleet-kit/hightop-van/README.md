# Hightop Van — `vanIsoRig.js` → `VanIso`

A Euro-style forward-cab cargo van in the fleet bake: 45° steps, elev 40°, fixed upper-LEFT key,
z-buffered flat facets, ordered dither, no AA, **32 px = 1 m**, ringless (ADR-0031). Cell **384 × 320**,
pivot **192,214** — the same road cell as the Dually 3500, so they park in one atlas convention.

## The one body, four vans

Two **build variants** multiply out of the single `hightopVan` body — geometry, not palette swaps:

| | `windows:false` (default) | `windows:true` |
| --- | --- | --- |
| `roof:'high'` (default) | panel van, full bulkhead | crew shuttle, glass + 2 benches, no bulkhead |
| `roof:'low'` | compact panel van | the `beachBus` preset |

Roof changes every published top-of-van z (2.72 → 2.42 m); windows swaps blind sides for set-in glass
and deletes the bulkhead. Pivot and cell never move.

## Poses

`dFL dFR` front doors 0→62° · `slide` curb sliding door (pops 0.085 m, runs **1.16 m aft** on a real
track) · `barnL barnR` rear barn doors 0→96°, hinged at their **outer** edges · `hood` stub clamshell
0→42° over a modelled bay · `roll` + `wFL..wRR` revolutions · `susF susR` ±1 (0.08 / 0.10 m — the body
moves, wheels stay down) · `steer` ±1 Ackermann 24°/20.6° (capped so the tire corner stays inside the
1.09 m arch bulges) · `yaw` ±45° rebaked headings · `night`, `weather`, `outline`.

## Files

```
vanIsoRig.js                          the rig (no deps) → globalThis.VanIso
hightopVan.contract.json              machine-readable bake contract, sha-stamped
vanIsoRig.hightopVan.gameplay.json    gameplay sidecar: thresholds, cargo, steering, variants
harness.html                          standalone assert page (open over http for the sha groups)
HightopVan_white_8dir.png             at rest, courier white 0.45          3072×320
HightopVan_lowroof_8dir.png           beachBus — low roof + windows        3072×320
HightopVan_slide_W.png                slide cue at W                       3072×320
HightopVan_barn_N.png                 barn cue at N                        3072×320
HightopVan_roll_W.png                 bounce cue, cyclic, 10 frames        3840×320
HightopVan_steer_S.png                lock-to-lock at S                    3072×320
HightopVan_paints_SE.png              the ten harbour paints               1920×640
```

Painted at rest: **200 × 184 px** union over the 8 facings (per-facing boxes in the contract).

## Class notes

- The **sliding door is curb side** (+x) and reads at `SW W NW` — kerb loading, correctly.
- The **cargo floor is the sill**: 0.55 m, one step up from the street, flat nose to tail.
- **No running boards, no front tow hooks, no fifth wheel.** One ball hitch aft (`hitch:true` default).
- The **widest point is the front arch bulges** (2.18 m), not the mirrors (2.52 m, glass) — the
  steering lock is capped at 24° so a tire corner never gets proud of them, and the harness asserts it.
