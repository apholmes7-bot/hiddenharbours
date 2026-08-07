# Hidden Harbours — Wharf Decor ISO Rig

The dressing of a working wharf: the gear, the handling kit, the lifting tackle, the safety board, the
drying flakes and the odds that pile up against a shed wall. **61 pieces in 7 categories**, each one a
parametric 3D model baked through the shared ¾ camera.

    wharfDecorRig.js   the rig (plain script, no imports, no build step)

Exposes `globalThis.WharfDecor`. Viewer page (in-project): `Wharf Decor Iso.dc.html`.

## Contract

| | |
|---|---|
| scale | 32 px = 1 m |
| camera | ADR-0006 shared turntable — 45° steps, elev 40°, orthographic |
| facings | `N NE E SE S SW W NW`, all 8 out of one model |
| light | fixed upper-LEFT key, flat-facet, z-buffered, ordered dither, per-face uv texture, depth-edge darkening, no AA |
| keyline | **retired** — `KEYLINE_DEFAULT = false` (ADR 0031); the 1 px ring `#1a1c22` is reachable with `{outline:true}` |
| sheet | fixed 420 × 520, pivot **(210, 420)** |
| pivot | ground-centre of the footprint — the same origin as the house, room and utility rigs |

Because the pivot is the footprint's ground centre, `render(name, dir)` drops straight onto a wharf
deck tile with no offset maths: blit at `screen(anchor) − pivot`.

At the default SE view +Y is the near side, so the wall-hung pieces — buoy wall, rope hanks, ring
station, fire cabinet, notice board, tide staff, harbour sign — carry their backboard toward −Y. They
are authored front-to-−Y and mirrored, which keeps the shed wall behind them and the face readable.

## Use the rig

    <script src="wharfDecorRig.js"></script>

    WharfDecor.render(name, dir, opts)   // -> Uint8ClampedArray, 420*520*4, binary alpha
    WharfDecor.footprint(name, opts)     // -> { w, d } in metres
    WharfDecor.height(name, opts)        // -> metres
    WharfDecor.mounts(name, opts)        // -> attach points in METRES
    WharfDecor.anchors(name, dir, opts)  // -> the same points baked to sheet pixels
    WharfDecor.project(dir, p, elev)     // -> { x, y } for any metres point
    WharfDecor.list()                    // -> the 61 keys
    WharfDecor.PROPS / CATS              // -> the catalogue itself

`opts` is the resolved spec every piece reads:

| option | values | effect |
|---|---|---|
| `paint` | a `BODY` palette key, or `null` | painted timber / steel colour; `null` leaves bare material |
| `metal` | `galv` · `alum` · `iron` | which metal ramp the fittings take |
| `variant` | int | the per-piece variants listed below |
| `len` | 0–1 | stack height / run length, on pieces marked · run |
| `loaded` | bool | totes with fish, barrels with bait, crates with rope, racks full |
| `tarp` | bool | canvas thrown over |
| `weather` | 0–1 | grime, rust into galv and iron, sun-silvered timber, greyed canvas |
| `night` | bool | cool moonlight |
| `elev` | degrees | default 40 |

Palette ramps ship on the object (`WOOD PLANK POLE IRON GALV ALUM CONCRETE COPPER BRASS ROPE POLYR
NET CANVAS FISH ICE RUBBER FOAM SLATE PAPER GRAVEL ASPHALT SALT GLASSD GLOW BODY`), so a compositor
can tint UI chips and overlays from the same ramps the bake used.

## Mounts

`mounts()` returns five kinds of attach point, in metres; `anchors()` projects the same points to cell
pixels and adds `foot` and `height`:

| kind | what it is |
|---|---|
| `deck` | ground contact — where the piece touches the planking |
| `rail` | rail-clamp points, for pieces that hang off the pipe or wood railing of the wharf kit |
| `wall` | the backboard plane that must sit against a shed wall |
| `post` | bolted to a pile head or a lamp post |
| `hook` | lift points — the block, hook or scale eye a load hangs from |

So a compositor can snap a ring station to a rail, hang a rope hank on a shed and sling a tote from a
davit with no per-instance nudging.

`wharfDecorRig.catalogue.json` carries the whole catalogue — sizes, variants and point counts —
generated straight out of the live rig.

## Catalogue

Sizes are **w × d, h** in metres, measured off the model at default `len`. · run = scales with `len`.

### gear (10)

| key | piece | size | variants |
|---|---|---|---|
| `trapStack` | Trap stack · run | 1.00 × 0.63, 1.44 | wire, wood |
| `trapSingle` | Single trap | 1.00 × 0.64, 0.36 | wire, wood |
| `buoyRack` | Buoy rack · run | 2.26 × 1.04, 1.62 | A-frame |
| `buoyWall` | Buoy wall · run | 2.42 × 0.31, 1.95 | two rails |
| `netReel` | Net reel | 1.74 × 1.51, 1.52 | timber drum |
| `netPile` | Net pile · run | 1.98 × 1.36, 0.55 | heaped |
| `ropeCoil` | Rope coils | 0.93 × 0.93, 0.10 | single, three coils |
| `ropeHanks` | Rope hanks · run | 1.71 × 0.24, 1.85 | pegged board |
| `floatBags` | Trawl floats · run | 1.44 × 0.96, 0.52 | loose pile |
| `trawlDoor` | Trawl door | 1.86 × 0.58, 1.02 | otter board |

### handling (11)

| key | piece | size | variants |
|---|---|---|---|
| `toteStack` | Tote stack · run | 0.80 × 0.57, 1.23 | stacked |
| `toteSingle` | Fish tote | 0.78 × 0.56, 0.36 | fish, iced |
| `crateStack` | Crate stack · run | 0.74 × 0.70, 0.86 | slat crate |
| `pallet` | Pallets · run | 1.18 × 0.91, 0.35 | stacked |
| `saltBin` | Salt bin | 1.71 × 1.10, 0.94 | lid shut, lid propped |
| `iceChest` | Ice chest | 1.50 × 0.96, 0.74 | shut, open |
| `weighScale` | Weigh scale | 1.30 × 0.86, 2.13 | dial head |
| `guttingTable` | Gutting table · run | 2.25 × 1.12, 1.04 | hosed |
| `baitBarrel` | Bait barrel | 0.68 × 0.68, 0.86 | single, pair |
| `bushelBaskets` | Baskets | 1.08 × 0.86, 0.67 | bushel |
| `sortingTrough` | Sorting trough · run | 2.65 × 0.88, 0.84 | graded, with tote |

### lifting (8)

| key | piece | size | variants |
|---|---|---|---|
| `davitHoist` | Davit hoist | 0.70 × 1.86, 3.00 | swing arm |
| `capstan` | Capstan | 0.96 × 0.96, 0.79 | powered, hand bar |
| `blockTackle` | Block & tackle | 0.54 × 1.40, 2.76 | two block |
| `wheelbarrow` | Wheelbarrow | 0.95 × 1.35, 0.72 | catch, net |
| `handTruck` | Hand truck | 0.62 × 0.62, 1.35 | upright |
| `dockCart` | Dock cart | 1.36 × 1.70, 0.50 | flat deck |
| `gantryFrame` | Gantry frame · run | 3.02 × 1.28, 3.08 | A-frame |
| `potHauler` | Pot hauler | 0.70 × 0.95, 1.10 | sheave head |

### safety (8)

| key | piece | size | variants |
|---|---|---|---|
| `ringStation` | Ring station | 0.98 × 0.30, 1.72 | board & line |
| `ringPost` | Ring post | 0.42 × 0.42, 1.21 | post & coil |
| `fireCabinet` | Fire cabinet | 0.70 × 0.26, 1.75 | glass front, with sand |
| `noticeBoard` | Notice board | 1.49 × 0.45, 1.88 | open, headed |
| `chalkSign` | Chalk board | 0.70 × 0.56, 0.94 | A-frame |
| `tideStaff` | Tide staff | 0.35 × 0.31, 3.20 | board, with float tube |
| `harbourSign` | Harbour sign · run | 2.60 × 0.30, 2.09 | plain, headed |
| `rescueLadder` | Rescue ladder | 0.62 × 0.52, 1.06 | rail hung |

### drying (5)

| key | piece | size | variants |
|---|---|---|---|
| `codFlake` | Cod flake · run | 2.69 × 1.86, 0.97 | drying |
| `fishRack` | Fish rack · run | 2.11 × 0.99, 2.16 | two rails |
| `netFrame` | Net frame · run | 2.73 × 1.24, 2.10 | hung net |
| `oilskinLine` | Oilskin line · run | 2.53 × 0.63, 1.86 | gear line |
| `herringSticks` | Herring sticks · run | 1.98 × 0.55, 1.65 | three rails |

### decor (10)

| key | piece | size | variants |
|---|---|---|---|
| `bench` | Bench · run | 1.95 × 0.56, 0.97 | plank back |
| `picnicTable` | Picnic table | 2.36 × 2.24, 0.74 | trestle |
| `deckChair` | Deck chair | 0.68 × 0.78, 1.08 | slat back |
| `drumPlanter` | Drum planter | 0.64 × 0.64, 0.72 | single, pair |
| `planterBox` | Planter box · run | 1.49 × 0.64, 0.60 | plank |
| `flagpole` | Flagpole · run | 0.62 × 0.62, 6.77 | halyard |
| `bunting` | Bunting line · run | 3.58 × 0.38, 2.41 | pennants |
| `lanternPost` | Lantern post | 0.46 × 0.73, 2.46 | hurricane |
| `anchorProp` | Old anchor | 1.06 × 0.84, 1.87 | leaning, on a skid |
| `ropeFence` | Rope fence · run | 3.38 × 0.31, 0.97 | one rail, two rail |

### odds (9)

| key | piece | size | variants |
|---|---|---|---|
| `trashBarrel` | Trash barrel | 0.66 × 0.66, 0.86 | open, lidded |
| `oilDrum` | Oil drum | 0.62 × 0.62, 0.91 | single, pair, stacked three |
| `saltBox` | Sand box | 1.34 × 0.90, 0.88 | closed, spilled |
| `bootRack` | Boot rack | 1.12 × 0.42, 0.52 | pegged |
| `bicycle` | Bicycle | 1.40 × 0.55, 0.98 | leaning, propped |
| `dogBowl` | Dog bowls | 0.34 × 0.30, 0.09 | single, pair |
| `tyrePile` | Tyre pile | 0.97 × 0.97, 0.95 | stacked, with leaner |
| `jerryCans` | Jerry cans | 0.86 × 0.53, 0.47 | row |
| `woodStack` | Wood stack · run | 2.18 × 0.77, 0.75 | cross-piled |

## What is not in here

The deck itself and everything structural — cleats, bollards, rails, ladders, fenders, gangways — is
the **wharf kit ISO rig** (`wharfIsoRig.js`). Poles, lamps, hydrants and tanks are the **utility rig**
(`utilityIsoRig.js`). This rig is only what a fisherman leaves lying about.

## Files

- `wharfDecorRig.js` — the rig. No dependencies.
- `wharfDecorRig.catalogue.json` — the catalogue, sizes, variants and mount-point counts, serialised
  out of the live rig so it cannot drift from the bake.
