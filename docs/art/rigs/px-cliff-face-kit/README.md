# Hidden Harbours — Pixel Cliff Face Kit · gameplay export

**The pixel-language cliff set, exported for the engine: one stamped sidecar, the palette
table, and two relight paths so your own lighting owns the sun.**

3 rocks × 5 aspects × 4 batters × 3 wear steps = **180 face sets**. Four channels each as
standing bake, **five with the index channel on**. 32 px/m, 2 px texel, 384 × 288 = 12 × 9 m
of surface.

```
pxCliffFaceRig.gameplay.json   the sidecar. Schema hidden-harbours/terrain-cliff-gameplay@1.
CliffPx_palette.png            8 × 32 palette LUT — every colour the bake can emit
bake/                          the rig and the three files it loads — re-bake from the drop
shaders/relight_palette.glsl   THE ONE THAT KEEPS THE PIXEL LOOK. 2 samples + 1 LUT tap.
shaders/relight_continuous.glsl  the cheap live path. 3 samples, banding softens.
tex-sample/                    25 PNGs — Sandstone wall, all five aspects, all five channels
```

**This kit is parametric, so it ships as the rig, not as 900 PNGs** — same policy as the v10
photoreal kit. The standing bake is already in `Art/Textures/CliffPx/`: **360 PNGs**, 90 face sets
× 4 channels (3 rocks × 5 aspects × Lo/base/Hi at 90° + base at 76/62/48). The full 180-set cross
product would be 720 and has never been baked standing — the rest is a rig call away. The index
maps are not baked either (see §4). `tex-sample/` is there so you can wire the shader up first.

---

## 0. Geometry parity with the v10 kit — measured, 2026-09-13

The kit claimed "same field, same seed, same metres" as `Art/cliffRig.js` and the claim was
false. It is now true, and measured: across **3 rocks × 4 batters, 0 of 1,327,104 texels differ**
from v10's `profile()` — bit-identical floats, matching envelopes and toe depths.

Three things had drifted in the port. All three are in `form()`, and the third is the one that
mattered most:

| | was | now |
|---|---|---|
| third octave | 9 cells at 0.10 amplitude | **17 cells at 0.22** — cliffRig's `rib3 * 0.11` on a ±1 field |
| cleft width | fixed `0.10` | **`0.10 + cj.open * 0.12`** — the joint's own openness, and `joints()` publishes `open` again |
| **the hash** | PxLang's `hash2` | **cliffRig's `ih`**, local to the form field |

The hash is why a parameter-only fix would not have closed it. `hash2` mixes the seed with
`1442695041`, `ih` with `1274126177`, so a parameter-perfect port still lands every rib, cleft and
bench somewhere else — the same rules, a different wall, and each rock's seed pointing into
another rock's field. That is the scrambled rock identity in the report, and it is why the error
was everywhere rather than in the clefts alone.

So the form field — `joints()`, `form()`, and nothing else — now runs on cliffRig's own `ih` and
`pv`. Everything below the form (beds, sockets, columns, lichen, tiers, bands) stays on the pixel
language, which is the half that is meant to look different.

**What moved with it.** The faces are a different wall than the ones you measured, because the
wall is now v10's. The standing bake in `Art/Textures/CliffPx/` has been re-baked in full — all
360 PNGs, every face set it holds — and `tex-sample/` with it. Brow and toe decals are untouched:
they carry their own seeds and never read the form field.

**`_normal` and premultiplied alpha.** Not a re-export: the shipped PNGs are straight alpha
(`putImageData`, never `drawImage`). Decoding one *through a canvas* premultiplies it, and
`_normal`'s alpha is cavity AO rather than coverage, so the round trip loses precision wherever
AO < 255. Measured on `Sandstone_SE_normal`: 96,668 texels differ after a canvas read-back, **all
96,668 exactly predicted by the premultiply round trip, 0 of them at alpha = 255**, and no alpha
byte altered. Decode with `putImageData`/`ImageDecoder`, or compare only where A = 255, and it is
byte-exact.

---

## 1. Two hashes, because two renderers can drift

| Field | Bytes |
|-------|-------|
| `derivedFromRigSha256` | `Art/pxCliffFaceRig.js` — the form, the tiers, the channels |
| `languageDerivedFromRigSha256` | `Art/pixelLanguage.js` — **the key vector and the band ramps** |

After the parity fix the rig hash is `3a584110…` (was `80b92ef7…`); the language hash is
unchanged at `2d6fb820…` — the pixel language was not touched.

The pixel language owns half the lighting contract: `LIGHT.key` is the sun every albedo was
baked at, and `bands()` cuts the five-step ramp the palette LUT is made of. Change it and every
number in `LIGHTING` and every row in `PALETTES` moves, while the cliff rig's hash sits still.
Station and rowhouse precedent — stamp both, never one.

Nobody types either number. `Art/_pxCliffGameplay.js` generates the file and the hashes are
taken from the bytes on disk at the moment of writing. If a hash no longer matches, **re-run the
generator; never patch a value.**

---

## 2. The one thing to read before you write a shader

The bake moves a texel **one band** for detail light and **one tier** for form, shadow and
incidence. It never interpolates. Six tiers cut from the rock's own hue, five bands each — 30
colours per rock, and every pixel on the wall is one of them.

So the failure mode is specific and it is not subtle:

> `_unlit.rgb * saturate(dot(N, L))` produces colours that are **not in the palette**. At noon
> it looks nearly right. At 07:00 the two shadow tiers crush together, the buttress ribs stop
> reading as ribs, and the wall goes to mud.

The fix is to stop multiplying pixels and start **stepping indices**. That is what
`_index` + `CliffPx_palette.png` are for, and why the exact path is also the *cheaper* one.

| Path | Samples | Textures | Exact | Use |
|---|---|---|---|---|
| fixed sun | 1 | `<stem>.png` | ✓ | static time of day, distant LODs. It is the art. |
| **palette shift** | 2 + LUT | `_index`, `_normal` | **✓** | **live sun, banding intact** |
| continuous | 3 | `_unlit`, `_normal`, `_mask` | ✗ | live sun today, no index bake, banding softens |

Pick one per wall. Do not mix them on one wall.

---

## 3. The light maps

| Channel | Contents | sRGB |
|---|---|---|
| `<stem>.png` | **pre-lit** albedo at the fixed key, aspect incidence and cast shadow included | on |
| `<stem>_unlit.png` | authored bands + **non-directional** cavity only. No key, no incidence, no cast shadow. | on |
| `<stem>_normal.png` | tangent space. R = s · G = t **up** the face · B = out of the face · **A = cavity AO** | **off** |
| `<stem>_mask.png` | **R = key N·L with the cast shadow multiplied in** · G = sky occlusion · B = height · A = coverage | **off** |
| `<stem>_index.png` | **R = LUT row · G = band 0..4 · B = tier-shiftable · A = coverage.** Raw indices. | **off** |

`mask.R` is the one thing a normal map cannot reproduce — a **cast** shadow, off the buttress
ribs and the bench lips. `relight_continuous.glsl` divides the baked N·L back out to recover the
shadow term alone; where that N·L is near zero the result is a guess. If you displace the wall
with `PROFILE` you have the geometry to cast it live instead, which is the right answer for a
traversing sun.

**`_index` must not be touched by the import pipeline.** sRGB conversion, bilinear filtering,
mips or block compression on it all produce colours that are not in the palette. Point, raw,
uncompressed, no mips.

### The palette LUT

`CliffPx_palette.png`, 8 × 32, of which 5 × 25 is used. `x` = band 0..4 (columns 5–7 repeat band
4 to pad to a power of two), `y` = LUT row:

```
 0.. 5   sandstone tiers −3 −2 −1 0 +1 +2
 6..11   till
12..17   basalt
18..24   silt · moss · dust · peb · turf · grit · xanthoria
25..31   padding, alpha 0
```

Rows 18–24 take a **band** step but never a **tier** step — lichen does not go the colour of
shadowed rock. The `_index` B channel flags which rows may move. The same 25 rows are in the
sidecar's `PALETTES` section as hex, if you would rather build the table at load time.

**Night is a second LUT, not a grade.** A grade over a six-tier ramp crushes the two shadow tiers
together and the form stops reading. Re-bake the table with a night key and cross-fade between
two LUT taps across dusk — the index maps do not change, only the table they point into.

---

## 4. Baking the index maps

Not in `Art/Textures/CliffPx/` yet — the channel is implemented and opt-in at both ends so the
standing bake's file count did not move. 180 PNGs for the full cross product:

```js
(0,eval)(await readFile('Art/pixelLanguage.js'));
(0,eval)(await readFile('Art/pxCliffFaceRig.js'));
(0,eval)(await readFile('Art/_pxCliffBake.js'));
await PXCF_BAKE({ createCanvas, saveFile, log, index: true,
                  rocks: ['sandstone'], slopes: [90] });   // one group at a time
```

Or from your own tooling — the rig is a plain IIFE that needs only `Art/pixelLanguage.js` and a
canvas:

```js
PxCliffFace.face('sandstone', 'SE', 1, {slope: 'ramp'})       // -> b
PxCliffFace.channels(b, {index: true})                        // -> {'', _unlit, _mask, _normal, _index}
PxCliffFace.paletteLUT()                                      // -> the 25 rows, as hex
PxCliffFace.profile('sandstone', 'SE', {slope: 62})           // -> {disp, W, H, metres}
```

**Do not hand-edit the PNGs.** Change the rig and re-bake.

---

## 5. Aspect, and the mistake it invites

`ASPECTS[a].tier_shift` is **one tier of incidence** — the bake's stand-in for which way the face
points, because a fixed key cannot know. An engine that builds its tangent-space sun from each
segment's real outward normal **already has that**, so applying the shift on top darkens every E
face twice. Set `_AspectShift = 0`.

Apply it only if you light the whole coast with one shared sun vector, where the shift is the
only thing distinguishing a W face from an E one.

What you *cannot* relight away, because it is painted into the texels, is the weathering — and
that is why the five aspects still earn their keep once light is live:

| | W | SW | S | SE | E |
|---|---|---|---|---|---|
| `tier_shift` | +1 | +1 | 0 | −1 | −1 |
| weathering | scoured, dust crowns | dry | sun-bleached | damp, lichen | wet, moss in the joints |
| `read` gain | 1.30 | 1.08 | 1.00 | 1.10 | 1.32 |
| Xanthoria (basalt) | 0.85 | **1.00** | 0.70 | 0.22 | 0.06 |

You pick the aspect for the weathering. The sun does the rest.

---

## 6. The batter, and the tangent basis

`{slope: 'wall'|'steep'|'ramp'|'bank'}` = 90° · 76° · 62° · 48°.

| | wall 90° | steep 76° | ramp 62° | bank 48° |
|---|---|---|---|---|
| top setback on 8 m | 0.0 m | 2.0 m | 4.3 m | 7.2 m |
| beds per 9 m of face | 36 | 35 | 32 | 27 |
| sky gain | — | +9% | +19% | +28% |
| traverse | wall | wall | **scramble** | **walk** |

Build the tangent basis per wall segment, with the outward normal **tipped back by the batter**:

```
Ts = normalize(along-cliff direction, horizontal)
Nw = normalize(N_plan * sin(batter) + worldUp * cos(batter))
Tt = cross(Nw, Ts)                                    // up the face
L  = vec3(dot(S, Ts), dot(S, Tt), dot(S, Nw))         // world sun S, in face space
```

Skip the tip and a 48° bank is lit as though it were a wall — which is the one thing the batter
exists to fix. Feed `BATTERS[b].key_in_face_frame` instead of a live sun and you reproduce the
shipped albedo exactly, which is the test to run first.

**`t` is 32 px/m along the surface, not along the height.** An 8 m bank at 48° needs
8/sin(48°) = 10.8 m of `t`. Size the quad by surface length.

---

## 7. What gameplay gets that the art does not model

`COLLISION` is generated from `PROFILE`, not from the quad: the collider is the **displaced**
face, 1.15 m of plan depth included. `TRAVERSE` is per batter, and it is the one gameplay
decision this kit forces — 90° and 76° are walls, 62° is a scramble at 0.45 speed, 48° is a
walkable hillside at 0.7. **Declared, not read.** The owner has not ruled; it is in `_confirm`.

The brow edge is a fall, and it sits at `t = 0.42` of the brow strip — the walkable ground stops
there, not at the quad edge. Toe debris is a decal. `ToeCave` is a decal too: there is no hole to
walk into.

`_excluded` records the absences a reviewer will look for: no walkable polygon on a wall (the
absence is the data), no cleats, no thresholds, nothing interactable, no N aspect, and the iso
ledge tiles — fixed-sun pixel art by nature, 8 steps on a 32 px face — which are not in this
sidecar and want a palette swap, never a normal map.

---

## 8. Known limits

- The face tile repeats every **12 m** along the shore and carries no per-chunk offset by design.
  The decals, the aspect changes and the batter changes are what break it up; on a 200 m straight
  run you will see it.
- **Brow and toe decals are fixed-sun and stay that way.** Their darks are cast shadow and
  geometric occlusion — the sod lip's undercut, the notch recess — not N·L. Leave them out of the
  relight and palette-swap them if you need a night set.
- `relight_continuous` quantises per texel; the rig quantises the form on an **8 × 6 texel cell**
  (0.50 × 0.375 m). Tier boundaries may fizz. Untested in engine — `_confirm`.
- Sandstone and Till here are the same red beds as the terrain kit but a different bake. Not
  interchangeable; a face should use these.
