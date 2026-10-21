# Coastal heritage pass — houses, manors, and stairs that reach the floor above

Bundle `manor-house-coastal-heritage-pass-03`, generated 2026-09-16. Seven rig sources, seven
sidecars, five `.dc.html` designs and five layout proposals. **The rig is the asset** — no PNGs ship
here; sheets are baked in Unity from these sources.

The seven rigs live at `docs/art/rigs/`, not in this folder. This folder holds the designs, the
sidecars, the layout proposals, `support.js` and the manifest that pins them all together. The
`Art/` copies beside this file are the bundle's own, kept so the `.dc.html` designs open standalone.

## The seven sources and what they install

| rig | global | presets | pinned sha-256 |
|---|---|---|---|
| `manorIsoRig.js` | `ManorIso` | 6 | `eb59dcbf…dd1241` |
| `manorUnitIsoRig.js` | `ManorUnitIso` | 7 | `d8bfad68…cecaf7b` |
| `houseIsoRig.js` | `HouseIso` | 5 | `4b28a464…8ba6ea66` |
| `interiorIsoRig.js` | `InteriorIso` | 8 | `ef06a219…e80bafc1` |
| `interiorPropRig.js` | `PropIso` | — | `a6c880e1…183f14a6` |
| `wharfBuildingRig.js` | `WharfBuilding` | 7 | `76dbecb0…3a08ed8163` |
| `coastalPass.js` | `CoastalPass` | — | `cb029383…2bade780` |

Render signature over all seven: `57e59da887ab14e1ced3627ab75c2d07c64a4e27bf3903cf4adfbbb2058b0a08`.
Each sidecar stamps `derivedFromRigSha256`; rehash the rig and compare. **The seven are one unit** —
a partial intake breaks the signature and there is no error, only different art.

`wharfBuildingRig.js` carries no diff in this pass: its bytes are identical to the copy already on
`main`. It is listed and hashed anyway because the signature covers all seven.

## Load order — this one is load-bearing

```
1. interiorPropRig.js      PropIso
2. coastalPass.js          CoastalPass    (needs PropIso present)
3. the host rigs           ManorIso BEFORE ManorUnitIso
```

`ManorUnitIso` reads its shell geometry off `ManorIso`. Install it first and `ManorUnitIso.dims()`
answers `{Wd:0, Ln:0, topZ:0}` — **silently**, no throw — instead of 13.25 × 17.50. The repo asserts
this by consequence in `CoastalHeritageStairTests`, not by inspecting an install list.

`RigCatalog.CoastalHeritage.cs` wires this for the in-editor baker. `coastalPass` deliberately does
**not** declare a `"house"` prerequisite: that would close the cycle `house -> coastalPass -> house`,
and `InstallPrerequisites` has no cycle guard.

## The switch

```js
CoastalPass.enabled = false;         // globally
InteriorIso.render(dir, {coastalPass:false});   // per render
```

**The look is revertible. The stairs are not.** The structural stair corrections live in the host
rigs, not in the companion. Measured with the companion on and off: the floor-to-floor rise, the
`floorRise` and the riser count are identical to the last digit on all four rooms; only the plate
moves.

## Reading a storey height

⚠️ `anchors(0, opts).storeyZ` is **not** the storey height in these rigs. It is `fH`, the ground
plate — a flat **0.55** on every domestic ground room. Under the old rigs the same field was
`ceilZ + joistZ`, the height to the floor above. Same name, same type, different meaning, no throw.

Ask for the rise as the difference between storeys:

```js
ManorIso.dims({...opts, storey:'upper'}).storeyZ - ManorIso.dims(opts).storeyZ
```

| room | rise (m) |
|---|---|
| sageCottage | 2.65 |
| school | 2.59 |
| redSaltbox | 2.74 |
| whiteFarmhouse | 2.92 |

Four independent expressions agree on each number: the floor-to-floor difference, `stair.floorRise`,
`stair.top.z`, and `steps × riser`.

## ⚠️ The double-rise trap

The upper-storey sprite's own floor z is **zero**. Place the sprite at `dims(options).storeyZ`. The
stair's `top.z` is already measured **from the ground floor** — do not add the rise to it a second
time. A doubled rise looks almost right and puts the landing through the roof.

`groundZ + topZ == upperZ` is asserted in the repo's tests for exactly this reason.

## Stairs

- Manor flights derive riser count and height from the **actual** next floor. A flight rises by
  `floorRise / steps` — never by the rounded `riser` the rig reports. Using the reported riser
  accumulates error across fourteen treads.
- Openings cover every tread needing the 2 m headroom envelope under the 0.30 m floor slab.
- Both ends reserve 1 m landings and the flight endpoints sit on them.
- Cottage flights climb all the way to the upper floor; ground and loft share one opening/landing
  contract. The keeper cottage rises **2.62 m in 14 equal risers**, 0.26 m treads, 1.05 m flight
  width.
- Every manor void names the flight beneath it, across all seven `ManorUnitIso.PRESETS`.

`layout-proposals/stair-contracts.json` is the full table.

## Pairing two storeys

Ground and upper must match in **type, size, shape and pitch**. Upstairs takes
`storey:'upper', dividers:0, hearth:false`. Domestic interiors assume two storeys; `coastalStoreys:1`
removes the companion stair.

## Layouts

`InteriorIso.furnishings(options)` returns a proposed layout in interior-local metres: blockers,
routes, target and approach positions, stair data, landing reachability. The five
`layout-proposals/*.layout.json` are those proposals, hashed.

It returns **`null` for the three unchanged wharf presets.** That is correct, not a failure.

Optional chairs and storage are **omitted** where they lack a clear slot or a reachable standing
point, and the layout files record each omission. An absent chair is a recorded decision. Do not add
it back.

## Known, and not fixed here

- **`ManorUnitIso.anchors(dir, opts)` returns `{anchors, openings, dims}`** and carries none of
  `door`, `floor`, `Wd`, `Ln`, `storeyZ` — the five fields `InteriorRigBaker` reads. The manor
  interior cannot bake through the existing baker; it needs an adapter or a baker of its own.
- **`anchors()` no longer returns `roomH`.** `EvaluateNumber` on a JS `undefined` throws
  `InvalidCastException` inside `System.Convert.ToDouble` — it does not return NaN.
- **The `floor` option is inert while the companion is on.** `FLOORS` still declares
  `plank, wideBoard, checker, stoneFlag, painted`, but with `CoastalPass` enabled all five render
  byte-identically; with it disabled they differ. `coastalPass.js` `cottage()` drops every face on
  the floor layer (line 241) and lays its own fixed tile-hall / oak-parlour bands (line 246). The
  host still honours the option — the companion discards the result. Reported, not patched.
- **The door anchor moved.** `HouseIso.anchors().door` now follows where the door is drawn (the +X
  eave on `(default)`, `redSaltbox` and `dormerCape`) rather than the +Y gable it always claimed.
  This is a fix to a shipped inconsistency, but it moves interior registration and is awaiting an
  owner ruling.
- Door animation, decorative exterior collisions, and the earlier review's exterior/interior
  opening-registration issues are all still open.
- Manor interaction anchors are **object targets**, not certified walk-to positions.

## Verifying the bundle

`node package-pass.cjs`, `node verify-stairs.cjs` from a **copy**, never from the read-only drop.
`verify-dramatic.cjs` expects a sibling review folder this bundle does not ship and crashes; a
cell-by-cell render reconcile is the substitute.

The designs open directly from this folder — relative paths to `Art/` and `support.js` are preserved.
Typefaces come from Google Fonts, so text falls back to a serif offline.
