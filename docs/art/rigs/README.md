# The art director's parametric rigs — the SOURCE the sheets are baked from

These are the art director's own `.js` rigs, imported **verbatim**. They are pure procedural renderers
with no external asset dependencies: each exposes `render(dir, opts)` returning a `Uint8ClampedArray` of
RGBA, plus metadata (`W, H, order, ANIMS, BUILDS`, anchors, …).

**Why they live in the repo.** Under [ADR 0021](../../adr/0021-in-engine-js-rig-baking.md) sprite sheets
stop being hand-exported from a browser and become an **editor operation** — the baker runs these files
directly. Versioning the rig alongside the code means any hull can be re-baked at any facing count,
forever, without another art hand-off.

⚠️ **Do not edit these files.** They are the art director's source. Fixes belong upstream, or the next drop
silently reverts them. Anything the engine needs that the rig doesn't provide belongs in *our* host code.

---

## ⚠️ THE AZIMUTH SPLIT — read this before baking anything

The rigs do **not** share one facing convention.

> ⚠️ **CORRECTION (2026-07-19, from the first real bake).** An earlier version of this file said the split
> was determined by inspecting the sign of the `th = ±dir*Math.PI/4` term. **That method is NOT reliable and
> must not be used.** `puntIsoRig` and `lobsterBoatIsoRig` both carry a **positive** sign and both render
> **counter-clockwise** — handedness comes from the iso camera basis, not from that sign alone. The sign
> merely *correlated* with the answer on the rigs that had been pixel-checked.
>
> **Only measurement is authoritative.** The groups below are trustworthy where the art was measured
> (characters via face-skin centroid; punt/dory/Cape Islander via PCA bearing + bow-taper; punt again via a
> byte-identical golden-master bake). **Every other rig here is UNVERIFIED** — the baker must measure it at
> bake time, not read this list. Treat the list as a prior, never as a fact.

**CLOCKWISE-CORRECT (2)** — the art director fixed these at source; character sheets pixel-verified:
`characterIsoRig.js` · `rodIsoRig.js`

**CLAIMED CLOCKWISE, UNVERIFIED (fishing kit, 2026-07-22)** — `fishIsoRig.js` · `fishToteRig.js`.
Both carry `th = -dir*Math.PI/4` and the kit's contract declares 8 headings at 45° **CW** (fleet order
N NE E SE S SW W NW). Per the correction above, the sign term is *not* proof — the baker must verify
each with `CharacterRigAzimuthProbe` (which refuses on mismatch) before trusting the labels.
(`fishIsoRig` was measured CW by `FishingRigAzimuthProbe` in the #265 bake; `fishToteRig` and the
CCW-inferred `bucketRig` are measured at bake time by `StorageRigAzimuthProbe` — the tote by where
its leaning lid lands at the E/W rows, the bucket by the fish tray's diagonal chirality — and the
storage bake refuses on a catalog mismatch like every sibling.)

**COUNTER-CLOCKWISE (19)** — cell `i` depicts heading **−45°·i** while labelled `+45°·i`.
Pixel-verified: `puntIsoRig` (golden master, byte-identical), `doryIsoRig`, `capeIslanderIsoRig`,
`lobsterBoatIsoRig`. The rest are inferred and must be measured before use:
`bucketRig` · `capeIslanderIsoRig` · `coastalPacketIsoRig` · `consoleIsoRig` · `doryIsoRig` · `fishTubRig`
· `houseIsoRig` · `interiorIsoRig` · `interiorPropRig` · `lobsterBoatIsoRig` · `puntIsoRig` · `shovelIsoRig`
· `sideDraggerIsoRig` · `skiffMotorRig` · `sportSkiffIsoRig` · `sternTrawlerIsoRig` ·
`sternTrawlerMk2IsoRig` · `tankerIsoRig` · `wharfBuildingRig`

**No azimuth term (18 + 4)** — kits, props and creatures that aren't 8-way directional; they need no
convention. (`sceneKit`, `shorelineRig`, `potRig`, `foxRig`, …) The fishing kit adds `bobberRig` ·
`crustaceanRig` · `shellfishRig` · `catchKit` to this group.

⇒ **The baker MUST carry a per-rig convention flag. A blanket correction is wrong** — it would re-mirror
the two already-correct rigs. And the flag must be *machine-verified against the rendered pixels*, not
maintained by hand: this mislabel has now caused defects in five separate kits, every time because someone
trusted a declared order instead of measuring the art. Once a rig is baked in-engine with its convention
applied, `FacingsAreCounterClockwise` goes **false** for that artwork — the flag survives only for legacy
hand-exported sheets until they are re-baked.

---

## ⚠️ These differ from the previously-imported copies

`puntIsoRig.js`, `consoleIsoRig.js`, `sportSkiffIsoRig.js` and `skiffMotorRig.js` were already in the repo
under `docs/art/punt-iso-rig/` and `docs/art/skiff-fleet-rigs/`. **The versions here differ** (md5 mismatch
on all four) — these came from the art director's live project folder and are newer.

The older per-kit copies have been removed so there is ONE canonical location; their `README.txt` files are
kept, since they document the shipped kits' cell sizes and pivots.

**Consequence for the golden-master test:** the first baker acceptance test bakes a hull and diffs it
against the sheet already shipped in `Assets/_Project/Art/Boats/`. If the punt does not match, the likely
cause is that the shipped art was baked from the *older* rig, not that the baker is wrong. Establish which
before chasing a phantom bug.

## Boats that exist only as a rig

No baked sheets exist for these — they can only ship once the baker does:
`lobsterBoatIsoRig` (Tier 3, ~12.0 m) · `coastalPacketIsoRig` · `sideDraggerIsoRig` ·
`sternTrawlerIsoRig` · `sternTrawlerMk2IsoRig` · `tankerIsoRig`

Most are M2/M3 fleet content — importing the source is **not** a licence to wire them (CLAUDE.md rule 8).

---

## The fishing rig kit (imported 2026-07-22)

One drop of every rig behind the fishing loop: the character that casts, the rod and its runtime FX,
the catch itself (fish / crustaceans / shellfish), and the storage it fills. Everything follows the M2
bake recipe (ADR-0006): fixed ¾ turntable, 8 headings at 45° **CW** (fleet order N NE E SE S SW W NW),
elev 40°, 32 px = 1 m, upper-left key light, ordered dither, 1 px keyline, no AA. All files are plain
browser scripts — each exposes ONE global and depends only on the globals it names.

New files in this folder (the kit's other nine were already here and arrived byte-identical):

### Character + rod (the cast)
- **characterIsoRig.js** → `CharacterIso` — fishing anims: hold 6f, cast 10f @70 ms (windup f0–3,
  snap f4–5 — the bobber launches at f5, settle f6–9), power-scaled short/long via `CAST_W1`/`CAST_S1`
  sub-ranges (`castBack`/`castRelease`). `anchors(dir,opts)` → handL/handR/head/hip cell px (the
  motor-mount pattern: every held thing pins to these). `tool(dir,opts)` → rod grip px + pitch/yaw/bend
  per frame. `carry(dir,opts)` → bucket/tray pins + swing.
- **rodIsoRig.js** → `RodIso` — 3 tiers (cane / coaster / deepwater), 112×112 bake, pivot = grip,
  pinned to handR. `tip()`/`tipLocal()` anchor the line; `project()` maps character-local 3D points to
  screen px for line/bobber/splash FX. CAST distances × `castMul` per tier. Rest poses: ground ×8 dirs
  + stored upright.
- **bobberRig.js** → `RodBobber` — the purpose-made float, 16×22, pivot (8,12) = the waterline.
  States: float 4f / nibble 4f / strike 4f / fly 2f. Underwater pixels bake with tint + alpha — never
  clip against water at runtime. Line attaches at the stem top.
- **splashRig.js** → `Splash` — the splash/ring burst FX used on entry, strike and land.

### The catch (every item is its own rig — no icons, this world is diegetic)
- **fishIsoRig.js** → `FishIso` — parametric 3D fish loft. `SPECIES` = one data block (len, girth,
  flatness, stripes + 5 hexes); `scale` sizes any catch on one skeleton; `hold(species,scale)` →
  `{mass, hands}` (<2.2 kg = one per hand, else two-arm cradle). Water anims: swim 4f / dart 2f /
  thrash 4f (surface break) / shadow 2f — pose z vs waterZ bakes a depth-graded underwater tint. Dry
  RESTS: deck 4 lays (fills + loose item), gill / tail (held, pivot = THE GRIP → pin to hand anchors),
  cradle (two-arm). `mouth(dir,opts)` = line attach in the surface fight. `spoil` 0..1 = the rot
  (green shift + dither mottle); rot motes are runtime FX in `FishIso.SPOIL` green.
- **crustaceanRig.js** → `Crustacean` — lobster + rock crab, SCALABLE rebuild: geometry in metres,
  replotted per render (never resampled). walk 4f / rear / defend / held 2f (dangled by the back,
  pivot = hpivot). `hold(kind,scale)` like the fish.
- **shellfishRig.js** → `Shellfish` — mussel + soft-shell clam: item (14×12, 4 lays, fills) and
  handful (22×16, pivot = grip, one per hand).
- **lobsterRig.js / rockCrabRig.js** → `Lobster` / `RockCrab` — the original fixed 48×48 deck/icon
  rigs. Kept for existing pages; **new work should use crustaceanRig**.

### Storage (containers fill with the catch's own rigs)
- **catchKit.js** → `CatchKit` — THE glue. `item(kind,{variant,scale,spoil})` → ready canvas + ground
  anchor for any catch (fish species / lobster / crab / mussel / clam). `fillItems(catch, fill, seed,
  capacity)` → seeded MONOTONIC item lists (growing a fill never moves earlier items; pass the
  container's slot count so full/brim genuinely heap). `tintSpoil` rots any rgba; `particles()` specs
  the motes.
- **fishToteRig.js** → `FishTote` — the ~1 m³ insulated deck tote (Cape Islander up): 5 colours, lid
  on/off/lean, pallet feet, genuinely hollow shell. `slots(dir)` → 4 stacked layers × 8 projected
  points rising from the floor (draw CatchKit items onto them, clipped to `opening(dir)`,
  back-to-front — layers visibly stack).
- **bucketRig.js** → `BucketIso` — steel pail / plastic pail / fish tray, carry + rest pivots,
  abstract fills (retrofit onto CatchKit planned).
- **fishTrayRig.js** → `FishTray` — the 32×24 deck tray with baked keepers (reshape to the grey
  stack-nest tote + CatchKit fills planned).
- **fishTubRig.js** → `FishTubIso` — the older on-deck tub prop.
- **buoyRig.js** → `LobsterBuoys` — per-fleet pot-marker buoys (spar shape, 8 schemes).

### The wiring cheat-sheet (the whole loop)
1. **CAST** — play character `cast`; at f5 read `tool()` wrist + `RodIso.tipLocal()`, launch the
   bobber (fly state) along the CAST arc; splash rings on entry.
2. **WAIT** — bobber float; bites: nibble dips; hook window: strike (pulled under).
3. **FIGHT** — FishIso shadow → thrash/dart at the surface; line attaches at `FishIso.mouth()`; the
   bobber rides just above it (strike while hooked, float while it tires, fly on the lift).
4. **HANDLE** — the landed fish is a rest bake: held by gill/tail (one per hand if light), cradled if
   heavy; crustaceans dangle by the back; shellfish by the handful.
5. **STORE** — drop into bucket / tray / tote: CatchKit items on the container's slots. Left too long
   → `spoil` climbs, everything greens, motes rise.

### Layering rules
Held/rod layers draw UNDER the character sprite for the away facings (NW / N / NE —
`RodIso.behind` = [7,0,1]); over it otherwise. Containers on boats: the boat's mount anchor carries
all translation; the container bakes only roll/pitch.

### Engine handoff
`gameplay/FisherRodMount.json` — frame-by-frame rod mount data (grip px per dir/frame, behind dirs,
pose sub-ranges) for engine-side integration without running the JS rigs. See `gameplay/README.md`.

The kit's demo pages (Fishing Rods.dc.html · Rod Bobber.dc.html · Fish Iso.dc.html ·
Catch Handling.dc.html) live in the art director's design workspace, **not** in this repo.
