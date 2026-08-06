# Hidden Harbours — Utility ISO Rig

The services that hang off a village: **power, light, water, sewer, fuel and telecom**. 42 pieces in
6 categories, each one a parametric 3D model baked through the shared ¾ camera, so a pole line rotates
in lockstep with the wharf, the houses and the villagers walking under it.

    utilityIsoRig.js   the rig (plain script, no imports, no build step)

Exposes `globalThis.UtilityIso`. Viewer page (in-project): `Utility Iso.dc.html`.

## Contract

| | |
|---|---|
| scale | 32 px = 1 m |
| camera | ADR-0006 shared turntable — 45° steps, elev 40°, orthographic |
| facings | `N NE E SE S SW W NW`, all 8 out of one model |
| light | fixed upper-LEFT key, flat-facet, z-buffered, ordered dither, per-face uv texture, depth-edge darkening, 1 px keyline `#1a1c22`, no AA |
| sheet | fixed 440 × 620, pivot **(220, 520)** |
| pivot | ground-centre of the footprint — the same origin as the house and room rigs |

Blit at `screen(anchor) − pivot` and the piece lands on its terrain point with no offset maths. Front
face is −Y, so the wall-mounted pieces (service mast, fill pipes) sit with their backboard toward +Y
and the wall behind them.

Poles and masts are tall: a power pole is 9.08 m, a radio mast 10.10 m. The sheet is sized for them,
so most small pieces sit in a lot of transparent space — crop to the ink bbox on pack, keep the pivot.

## Use the rig

    <script src="utilityIsoRig.js"></script>

    UtilityIso.render(name, dir, opts)   // -> Uint8ClampedArray, 440*620*4, binary alpha
    UtilityIso.footprint(name, opts)     // -> { w, d } in metres
    UtilityIso.height(name, opts)        // -> metres
    UtilityIso.ties(name, opts)          // -> wire / lamp / drop attach points in METRES
    UtilityIso.anchors(name, dir, opts)  // -> the same points baked to sheet pixels
    UtilityIso.project(dir, p, elev)     // -> { x, y } for any metres point
    UtilityIso.list()                    // -> the 42 keys
    UtilityIso.PROPS / CATS              // -> the catalogue itself

| option | values | effect |
|---|---|---|
| `paint` | a `BODY` palette key, or `null` | painted cabinet / tank colour; `null` leaves bare material |
| `metal` | `galv` · `alum` · `iron` | which metal ramp the hardware takes |
| `variant` | int | the per-piece variants listed below |
| `len` | 0–1 | height or length run, on pieces marked · run |
| `weather` | 0–1 | grime, and rust into the metals |
| `gravel` | bool | gravel apron / pad skirt at the base |
| `night` | bool | cool moonlight, plus the lamp glow overlay |
| `elev` | degrees | default 40 |

Palette ramps ship on the object (`POLE WOOD IRON GALV ALUM CONCRETE COPPER BRASS GLASSINS PORC
GRAVEL ASPHALT BODY GLOW`).

## Spans are never baked

`ties()` returns four sets of attach points in metres — `wires` (primary), `secondary`, `lamp`,
`drop` — and `anchors()` projects them to cell pixels alongside `foot` and `height`. The catenary
between two poles, and the service drop to a house, are **drawn at runtime between ties**; no span is
ever baked into a cell. That is what lets a pole line span arbitrary distances, cross corners and sag
correctly without a sprite per gap.

`utilityIsoRig.catalogue.json` carries the catalogue — sizes, variants and tie-point counts —
generated out of the live rig.

## Catalogue

Sizes are **w × d, h** in metres at default `len`. · run = scales with `len`.

### power (9)

| key | piece | size | variants |
|---|---|---|---|
| `powerPole` | Power pole · run | 0.54 × 0.54, 9.08 | crossarm, transformer, lamp arm |
| `hFrame` | H-frame · run | 3.13 × 0.65, 8.59 | twin circuit |
| `padTransformer` | Pad transformer | 2.05 × 1.67, 1.30 | single |
| `switchgear` | Switch cabinet | 2.38 × 1.84, 1.70 | four-way |
| `pedestal` | Junction pedestal | 0.78 × 0.62, 1.00 | hooded |
| `genset` | Standby set | 1.94 × 1.13, 1.34 | enclosed |
| `serviceMast` | Service mast · run | 0.97 × 0.54, 4.56 | meter + head |
| `meterBank` | Meter bank | 1.64 × 0.50, 1.94 | three-gang, five-gang |
| `guyAnchor` | Guy anchor · run | 1.68 × 1.56, 2.84 | rod & guard |

### light (4)

| key | piece | size | variants |
|---|---|---|---|
| `yardLight` | Yard light · run | 0.54 × 0.54, 7.26 | cobra head |
| `streetLamp` | Street lamp · run | 1.08 × 0.54, 4.48 | pendant lantern, acorn globe |
| `floodMast` | Flood mast · run | 2.05 × 0.76, 7.80 | three-head |
| `beacon` | Harbour beacon · run | 1.51 × 1.51, 4.34 | tripod pile |

### water (9)

| key | piece | size | variants |
|---|---|---|---|
| `hydrant` | Fire hydrant | 0.62 × 0.62, 0.96 | two-way |
| `yardHydrant` | Yard hydrant · run | 0.45 × 0.45, 1.39 | frost-free |
| `standpipe` | Standpipe | 1.20 × 1.70, 1.24 | tap & trough |
| `handPump` | Hand pump | 0.88 × 0.80, 1.28 | pitcher pump |
| `wellCap` | Well head | 0.86 × 0.86, 0.72 | drilled casing, dug well lid |
| `curbStop` | Curb stop | 0.46 × 0.46, 0.12 | road box |
| `valveVault` | Valve vault | 1.30 × 1.10, 0.90 | twin lid |
| `waterTank` | Water tank · run | 2.29 × 2.29, 4.28 | timber stand |
| `cistern` | Cistern | 2.59 × 2.05, 1.94 | buried box |

### sewer (9)

| key | piece | size | variants |
|---|---|---|---|
| `manhole` | Manhole | 1.44 × 1.44, 0.14 | sanitary, road patch |
| `cleanout` | Cleanout | 0.44 × 0.50, 0.50 | capped |
| `ventStack` | Vent stack · run | 0.65 × 0.37, 2.72 | candy cane |
| `septicLids` | Septic lids · run | 3.04 × 1.45, 0.47 | twin riser |
| `catchBasin` | Catch basin | 1.24 × 1.24, 0.34 | curb inlet, with apron |
| `trenchDrain` | Trench drain · run | 0.87 × 4.71, 0.16 | grated channel, with apron |
| `liftStation` | Lift station | 2.70 × 1.70, 1.62 | well + panel |
| `outfall` | Outfall | 1.90 × 1.70, 1.00 | headwall & flap |
| `culvert` | Culvert end · run | 3.66 × 5.25, 0.75 | corrugated |

### fuel (7)

| key | piece | size | variants |
|---|---|---|---|
| `oilTank` | Oil tank | 1.12 × 0.66, 2.00 | 275 gal, twin |
| `fillPipes` | Fill & vent | 0.70 × 0.42, 0.98 | pair |
| `propaneTank` | Propane tank · run | 2.93 × 1.02, 1.20 | 500 gal, bottle pair |
| `bulkTank` | Bulk tank · run | 3.47 × 3.25, 3.64 | vertical shell |
| `fuelPump` | Fuel pump | 0.68 × 0.58, 1.94 | dockside |
| `drumRack` | Drum rack | 1.90 × 1.50, 1.24 | two drum, three drum |
| `coalBin` | Coal bin · run | 2.35 × 2.24, 1.20 | plank bin |

### telecom (4)

| key | piece | size | variants |
|---|---|---|---|
| `telecomPed` | Telecom pedestal | 0.56 × 0.44, 1.00 | hooded |
| `crossBox` | Cross-connect | 1.40 × 0.86, 1.38 | cross-connect |
| `alarmBox` | Call box | 0.45 × 0.45, 1.69 | pull box |
| `radioMast` | Radio mast · run | 5.92 × 5.92, 10.10 | guyed lattice |

## Files

- `utilityIsoRig.js` — the rig. No dependencies.
- `utilityIsoRig.catalogue.json` — the catalogue, sizes, variants and tie-point counts, serialised out
  of the live rig so it cannot drift from the bake.
