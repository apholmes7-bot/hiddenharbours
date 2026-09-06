# Sail rig kit — the probe record (2026-09-06)

Every number below was measured in the repo's own ClearScript V8 (`Assets/_Project/Plugins/Editor/JsEngine`,
driven by a ~90-line `net8.0` console harness) against **the bytes committed in this PR**, not against the
drop's READMEs. Where a measurement contradicts the drop, both are given and the contradiction is named.

The guards that must keep holding are in `Assets/Tests/EditMode/RigBaking/SailRigKitTests.cs`. This file is
the transcript; the tests are the contract.

---

## 0 · Provenance — the strongest check available, and it passes

`SAIL_KIT.write()` produces each sidecar as `JSON.stringify(SidecarExport.stampRigSha(<section>, sha), null, 2)`.
So the sidecars are not merely *consistent* with the rigs — they are **reproducible from them**. Re-running the
kit's own writer over the committed rig bytes gives:

| | reproduced | shipped | verdict |
| --- | --- | --- | --- |
| `sloopIsoRig.gameplay.json` | 36,198 B | 36,198 B | **byte-for-byte identical** |
| `sloopIsoRig.sailing.json` | 75,774 B | 75,774 B | **byte-for-byte identical** |
| `sloop88IsoRig.gameplay.json` | 56,367 B | 56,367 B | **byte-for-byte identical** |
| `sloop88IsoRig.sailing.json` | 82,322 B | 82,322 B | **byte-for-byte identical** |

All six `SHA256SUMS.txt` entries hash EXACT on the landed bytes (full 64 characters compared — a prefix check
is not a hash check). Both sidecars of each hull stamp that hull's rig digest.

`.gitattributes` pins `docs/art/rigs/sail-rig-kit/**` and the two hulls' sidecars to LF. The stamps are over the
rig bytes as written, which are LF; with `core.autocrlf` on, an unpinned checkout hands `sha256sum` the CRLF
form and all six pins miss for line endings alone.

⚠️ `SHA256SUMS.txt` is **kit-relative**: it names the sidecars under `sloop-30/` and `sloop-88/`, where this repo
lands them in `docs/art/rigs/gameplay/sail/` (a subfolder while the mesh bake is blocked — see that folder’s README) beside the rest of the fleet's. The manifest is kept exactly as shipped
— rewriting a supplier's checksum file to suit our folders is how a checksum stops being evidence — and
`SailRigKitTests.ManifestToRepoPath` carries the mapping.

---

## 1 · Azimuth — COUNTER-CLOCKWISE, and one wrong way to measure it

| dir | 0 | 1 | 2 | 3 | 4 | 5 | 6 | 7 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| rig's `order` claims | N | NE | E | SE | S | SW | W | NW |
| **measured heading** | 0° | 315° | 270° | 225° | 180° | 135° | 90° | 45° |

Seven steps of **exactly −45.0000°** on both hulls. Method: the bearing of the rig's own **+X (starboard)** axis,
from the projected `winchPort`/`winchStbd` pair (30) and `helmPort`/`helmStbd` (88) — anchors that share a y
*and* a z, so their screen offset is pure +X and the anchor's height cannot enter it. Depth un-squashed by
sin(elev 40°) = 0.6428 before the atan2, because a raw screen angle is not a world bearing at this elevation.

Second, independent witness: `bowRoller` projects **147.84 px** (30) and **448.00 px** (88) **WEST** of the pivot
at cell 2 — the cell the rig's own `order` array labels `'E'`. A cell labelled east depicting west is the
counter-clockwise signature (`RigAzimuthProbe` step 4). Both hulls match the rest of the fleet.

### ⚠️ The reading that was incoherent, recorded because the failure is instructive

Taking the bearing from **`bowRoller` alone** gives steps of −29.806° / −32.393° / −42.071° / −75.730° / −75.730°
/ −42.071° / −32.393° — summing to 330° over seven steps instead of 315°. The bow roller sits **2.044 m above the
origin**, and at elev 40 that height moves the projected point up the screen by a term with nothing to do with
heading; un-squashing the whole offset by sin(elev) therefore answers a different question. **Only a pair at
equal z isolates the ground plane.** Do not re-derive a bearing from a single lifted anchor.

### ⚠️ The sailing sidecar's own glue note disagrees with the pixels

`WIND.from_true.heading_deg` says *"dir * 45 (N = 0, clockwise)"*. The pixels say −45°/dir. The note is
describing the rig's `order` array, which is the label that has been wrong on nineteen of twenty-one directional
rigs in this repo. **The maths in that block is frame-independent and correct** (twa, aws, awa); only the
dir→heading sentence is not. `SailingSidecarReader` carries `from_true` verbatim with the contradiction flagged,
so a reader sees it rather than inheriting it.

---

## 2 · The pose law, verbatim from the rig (line 760, `sloopIsoRig.js`)

```js
let awa = ((awa+180)%360+360)%360-180;                    // wrapped, ALWAYS
const a = Math.abs(awa), side = awa>=0 ? -1 : 1;          // wind over stbd -> sails to port
const irons = a < 25 && aws > 0.5;
let boom = Math.min(mainLimit, Math.max(0, a-6));
const fill = (aoa) => clamp01((aoa-5)/13) * clamp01(aws/6);
if (a > 150) fillJ *= 0.4;                                // blanketed by the main
const stallM = clamp01((aoaM-40)/40);
const heelDeg = Math.min(24, 0.067*aws*aws*(0.25+0.75*fillM)*k) * (irons?0.15:1);
const mode = aws<0.5 ? 'becalmed' : irons ? 'in irons'
           : (flogM>0.01||flogJ>0.01) ? 'luffing' : (stallM>0.3) ? 'stalled' : 'drawing';
```

Measured against the shipped tables: **0 mismatches** over TRIM_TABLE's 37 rows and all 7 POINTS_OF_SAIL, on
both hulls, comparing mode, boom, headsail angle and heel.

**Thresholds, swept at 0.5° resolution under AUTO_TRIM:**

| transition | measured |
| --- | --- |
| `in irons → drawing` | last in irons **awa 24.5**, first drawing **awa 25.0** — the 25° band, exactly |
| headsail blanketing | `fillJib` 1.0 → 0.4 between **awa 150.0 and 150.5** — strictly *above* 150 |
| `drawing → stalled` | **awa 138.0 → 138.5** |

The third is **not in the kit's headline pair (25/150)** and matters to PR 1: `stallM > 0.3` means stall begins at
aoa **52°**, not the 40° the threshold block names. Under AUTO_TRIM at aws 12 that lands at awa ≈ 138.5.

Also measured, and load-bearing for PR 1's default: **under AUTO_TRIM the sprite never luffs outside irons.**
The trim law holds angle-of-attack at 18°/16°, well clear of the 8° luff threshold, so a player who never
touches a sheet sees `drawing → stalled → in irons` and no flogging in between.

**Heel has two fields and they differ by sign.** `pose.heel` is SIGNED (`side * heelDeg`, negative = sails to
port); `pose.heelDeg` is the magnitude, and it is `heelDeg` that the sidecar's `heel_deg` column equals. A
consumer reading `heel` and comparing it to the sidecar sees 37 of 37 rows "disagree".

**`awa` has no out-of-range trap:** `sailPose(a)` is identical to `sailPose(wrap180(a))` over 155 samples from
−540° to +540°, on both hulls.

---

## 3 · Silent fallbacks — pinned by BUFFER IDENTITY, not by pixel count

A pixel count cannot tell two paints apart, so each was fingerprinted (FNV-1a over the whole RGBA return).

| probe | result |
| --- | --- |
| `render('N', …)` — the compass string both READMEs talk in | **0 opaque px of 28,827** (30) / **0 of 202,675** (88). No throw. `dir` is an integer 0..7. |
| `render(8, …)` | byte-identical to `render(0, …)` |
| `render(-1, …)` | byte-identical to `render(7, …)` |
| `scheme: 'no-such-scheme'` | byte-identical to the default, which **is** `gelcoat-white`. No warning. |
| `scheme: 'atlantic-navy'` (the control) | genuinely differs — the fingerprint can see paint |
| `awa` outside ±180 | **no trap** — wrapped at the law's first line |

The compass-string trap is the camper's, at a new scale: any baker must assert an opaque-pixel floor per cell
rather than trust its caller. `SailRigKitTests.TheCompassStringRendersAnEmptyCell` is that guard.

---

## 4 · THE SEAM — what a mesh bake gets, and what it does not

### The finding

`R.faces()` returns the rig's module-scope `F`. It is **pose-free**: same array identity and same fingerprint
after renders at opposite poses (`awa 45 / hoist 1 / exterior` vs `awa 170 / hoist 0 / cover / cabin`).

`render` composes `F.concat(dynamicFaces(o, pose, view))`. Everything that answers the wind — main, headsail,
staysail, boom, sheets, lazy jacks, stack pack, wheel, door — is in `dynamicFaces`, **not in `F`**.

> **So a mesh bake of `faces()` does not freeze one sail pose. It has no sails at all.**
>
> That is a better starting point than the handoff assumed, and it changes the question: the seam is not "which
> pose do we freeze", it is "how does a continuously-posed part get across at all".

| | sloop 30 | sloop 88 |
| --- | --- | --- |
| static body faces | 1,852 | 3,088 |
| static body vertices | 7,514 | 12,530 |
| triangles (fan) | 3,810 | 6,354 |
| polygon degrees | 1,833×quad, 4×8-gon, 15×10-gon | 3,054×quad, 6×7, 4×8, 24×10 |
| materials named by the body | 14 | 14 |
| **sails' share of the painted picture** | **27.5 %** on a close reach | **47.5 %** |

### The ramp arithmetic, which sizes the seam

| | full `palette({}).mats` | filtered to the body's used set | cap |
| --- | --- | --- | --- |
| sloop 30 | **18** | **14** | 16 |
| sloop 88 | **19** | **14** | 16 |

Dropped by the filter: `canvas`, `sail`, `batten`, `moto` (+ `mast` on the 88). Exactly the sail's palette —
unreferenced because the sails are not in `F`.

⚠️ The bare `palette({}).mats` overflows `HullMeshDef.HullRampSlots = 16`, and it fails **quietly**:
`IsUsable()` returns false above 16, so the bake writes a def the game refuses to present rather than throwing.
The filtered reconstruction (the zodiac's idiom, for the zodiac's reason) is in `RigMeshExtractor`.

**14 used + 3 sail ramps = 17 > 16.** A sail part cannot ride the body's ramp table. It needs its own.

### What the road fleet's AXES model can and cannot carry

The handoff proposes `VehicleMeshDef`-style axes with split assertions. Measured against that:

| part | rigid? | evidence |
| --- | --- | --- |
| **boom** | **YES** | gooseneck→boom-end ground length is **3.55 m** (30) / **10.6 m** (88) at every awa from 0 to 172 — **spread 0.0000 m**. A rotation about the gooseneck reproduces it exactly. |
| **hoist** | **NO** | 11 steps produce **11 distinct pictures** — the luff shortens along the mast *and* the stack pack grows in the trough. Not one transform. |
| **furl** | **NO** | 11 steps produce **11 distinct pictures** — the sail rolls onto the forestay and the rolled sausage wears the UV strip. |
| **the cloth** | **NO** | `fill`, `flog`, `stall` re-camber it per (awa, aws, sheet). A rigid transform cannot make a sail belly. |
| **flutter** | **NO** | 8 frames in irons draw 8 different silhouettes (52,879 → 52,599 px). |

So an axes model gets the boom and nothing else that matters. **Say that plainly in the seam decision**: the
fallback the handoff names — a STATE LADDER of baked parts (stored · hoisting · close-hauled P/S · reach P/S ·
run) — is not merely inelegant for a continuously-posed boat, it is *measurably* a 5-sample approximation of two
independent 11-step axes plus a continuous camber, on a part that is half the 88's picture.

### The three options

Written up as a decision request in `seam-proposal.md`, beside this file. Nothing is wired either way in
PR 0 — and note that **no** mesh of these hulls can bake at all until the level-stamp defect below is fixed
upstream, so the choice is not on the critical path yet.

### ⚠️⚠️ AND NO MESH OF EITHER HULL CAN BAKE TODAY — the bake was run, and REFUSED

Both rigs publish `geometry().ids`, which arms `RigMeshExtractor`'s level-tag contract: *"this rig publishes
geometry().ids, so every face it hands over must DECLARE its level."* Measured on the committed bytes:

| | sloop 30 | sloop 88 |
| --- | --- | --- |
| faces | 1,852 | 3,088 |
| **no `lv` at all** | **842 (45.5 %)** | **1,312 (42.5 %)** |
| stamped with | `cabin · lid · rig · under` | `cabin · lid · rig · under` |
| `geometry().ids` publishes | hull · cockpit · coachroof · foredeck · cabin · rig | hull · cockpit · aft_deck · coachroof · foredeck · saloon · lower · rig |
| declared levels never stamped | 4 of 6 | **7 of 8** |

It is not a handful of missed stamps: the faces carry a **cutaway** vocabulary while `ids` publishes a **level**
one. `lid` and `under` are in neither hull's `ids`; on the 88 the two vocabularies share exactly one member
(`rig`), and 449 of her faces are stamped `cabin` — a level she does not declare at all.

**Not defaulted to `hull`**, and the extractor's own refusal says why: *"The only defensible default is 'hull',
which means NEVER CULL — so a missed stamp would ship as a room that quietly stops opening, in one wall, on one
heading."* And `docs/art/rigs/**` is the art director's lane; a fix made here comes back wrong on the next
regeneration.

**The upstream ask:** stamp every emitted face with a member of `geometry().ids` (the cursor idiom the other
cutaway hulls use), **or** stop publishing `ids` in pass 1 and let these two bake as plain bodies. Either
answers it. The current files ask for the cutaway and do not pay for it.

Registered in `HullMeshFleet.BakeBlocked` and asserted in BOTH directions — the guard re-measures the break and
reddens when the rigs are fixed, saying "delist and bake".

---

## 5 · Facts PR 1 will need, measured

- `EnvironmentSample.WindVector` and `TelltaleMath.ApparentWind` already exist in Core — do not add a second wind.
- `WIND.from_true` (carried verbatim by the reader): `aws = √(tws² + v² + 2·tws·v·cos twa)`,
  `awa = sign(twa)·atan2(tws·sin|twa|, tws·cos|twa| + v)`.
- Reference polar: 10 winds × 15 angles = 150 cells per hull. Best VMG at 12 kn true: **twa 45, 4.97 kn** (30) and
  **twa 45, 9.15 kn** (88); downwind **twa 165, 5.22 kn** and **7.80 kn**.
- Hull speed 7.41 kn (30) / 12.6 kn (88); SA/D 13.1 / 17.8; D/L 168.5 / 127.
- ⚠️ `HULL_FORM.displacement_kg` (4,875 / 89,190) is **canoe body only** — keel and bulb not integrated. The
  hull-weight ride (#739) should read `HULL_FORM.waterline_half_breadths` (17 stations each), which is the
  honest waterplane input.

### ⚠️⚠️ The no-go coupling, and the drop's prose about it is BACKWARDS

The polar's model returns zero below **twa 35 (true)**. The rig flogs both sails below **awa 25 (apparent)**.
Different frames — no single number reconciles them. Cells where the polar hands out a speed and the sprite
shows a boat in irons:

| | cells | of 150 |
| --- | --- | --- |
| sloop 30 | **4** | 2.7 % — twa 35 at 4/6/8 kn, twa 40 at 4 kn |
| sloop 88 | **14** | 9.3 % — twa 35 at 4–18 kn, twa 40 at 4–14 kn |

**Sloop 88, awa at twa 40 across the wind range** (`*` = the sprite reads *in irons*):

```
4 kn 23.0*  6 kn 23.0*  8 kn 23.0*  10 kn 23.0*  12 kn 23.6*  14 kn 24.9*
16 kn 26.0   18 kn 27.0   20 kn 27.9   25 kn 29.7
```

Both the kit README and the sloop-88 README say she reads "in irons" at twa 40 **above ~10 kn true**. The rows
say the band is **4–14 kn and she clears at 16**. The mechanism is plain once seen: apparent wind draws forward
as boat speed grows *relative to* true wind, so the tightest apparent angles occur in the **lightest** air. The
numbers in the files are right; the prose about them is not.

**Why it matters for the ruling:** the defect bites when a player is beating in a light breeze — the most likely
moment to be pinching, and the least forgiving place for the picture to disagree with the speedo. The kit's own
`no_go` note already recommends the answer: *"Pinch to the sprite's 25 deg apparent, not the polar's 35 true."*
That is a recommendation, not a ruling. `SailPolarDef.IronsRowCount` carries the count onto the asset so the
size travels with the data.

---

## 6 · Surface asymmetries between the two hulls — do not write one loop over both

- `bounds(dir, opts)` exists on **Sloop88Iso only**. The 30 does not export it.
- The 88 takes `sfurl` (staysail furler) and `platform`; her `swim_platform` DECK polygon is **conditional** on
  `opts.platform >= 0.5`.
- Her threshold **slides** to port with a pocket-strip `keep_clear`; the 30's is hinged with a swept arc.
- `LEVEL_IDS` differ: 30 = hull/cockpit/coachroof/foredeck/cabin/rig; 88 adds aft_deck, saloon, lower.
- She paints her spars from `spar`; the 30 uses `mast`.
- Cell 400×552 vs **1072×1504** — the largest in the game, 6.4× the Cape Islander's area.
- Painted envelopes measured over the sailing states: 354×493 of 400×552, and 1008×1395 of 1072×1504 — inside
  the cell on all four edges, both hulls, `all_inside_cell: true`.
