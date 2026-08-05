# Hidden Harbours — Cliff Face & Terrain Ledge Kit v10

**Near-vertical — and now not-so-vertical — sections the player never traverses.
Their job is to say which way they face and what shape they are.**

3 rocks × 5 aspects × **4 batters** × 3 wear steps = **180 face sets**, each shipping
**4 co-registered channels**. Plus brow and toe decals, 32 px iso ledge tiles, and a
plan-displacement profile per rock and batter. 32 px/m throughout.

```
bake/bake.html          open this — bakes the whole set into a folder you pick
bake/cliffRig.js        the rig. The source of truth. No dependencies.
bake/_cliffScene.js     axonometric harness: renders cliffs as geometry for review
cliff.json              manifest — names, sizes, wrap rules, channel meanings
sheets/                 contact sheets: aspects, ladder, batters, form, coast, strips, ledges
tex-sample/             49 PNGs — one worked example of every asset type
```

**This kit is parametric, so it ships as the rig, not as 800 PNGs.** Baking the full
set is one click and about three minutes; keeping it in the repo is 90 MB that goes
stale the moment a coefficient changes. `tex-sample/` is there so you can wire the
shader up before you bake.

---

## 1. Bake it

Open `bake/bake.html` in Chrome or Edge, press **Bake everything to a folder**, pick a
destination. It writes:

```
tex/      <Rock>_<Aspect>{|_S76|_S62|_S48}{_Lo|""|_Hi}{|_unlit|_normal|_mask}.png   720 files, 384×288
brow/     <Aspect>{_Lo|""|_Hi}.png                                                   15 files, RGBA 384×128
toe/      <Aspect>{_Lo|""|_Hi}{|_cave|_slump}.png                                    45 files, RGBA 384×128
ledges/   <Rock>{_Lo|""|_Hi}.png  +  Contact.png                                     10 files
profile/  <Rock>{|_S76|_S62|_S48}.png                                                12 files
```

The vertical bake carries no batter suffix; the base wear step carries no step suffix.
Everything is exactly periodic in `s` — sample with **Repeat + Point**, no filtering.
The preview panel on the same page bakes any single set so you can eyeball a
combination before committing to the full run.

To bake from your own tooling instead, `cliffRig.js` is a plain IIFE that needs
nothing but a canvas:

```js
CliffRig.face('sandstone', 'SE', 1, {slope: 'ramp'})   // -> {data, unlit, normal, mask, W, H}
CliffRig.profile('sandstone', 'SE', {slope: 62})       // -> {disp, W, H, metres}
CliffRig.brow('E', 2)  ·  CliffRig.toe('S', 1, {feature: 'slump'})
CliffRig.ledge('till', 'cornSW', {band: 'mid', gx, gy, step: 1})  ·  CliffRig.contact('cornSE')
```

**Do not hand-edit the PNGs.** Change the rig and re-bake.

---

## 2. The contract, and the one place it differs from the terrain kit

The terrain kit bakes **no directional light**, deliberately: a plan-view material must
work under any sun. **This kit bakes it** — a cliff face never rotates relative to the
camera and the player cannot walk round it, so incident level, shadow temperature and
aspect-dependent weathering all belong in the albedo.

But a fixed key is wrong by 07:00 on a 24 h cycle, so every face also ships the
channels that let the shader light it live. Pick a path per project; do not mix them on
one wall.

| Channel | Contents | sRGB |
|---|---|---|
| `<name>.png` | **pre-lit** albedo at the aspect's key, including the form pass cast shadow. The cheap path. | on |
| `<name>_unlit.png` | albedo + the **non-directional** cavity and macro AO only. No directional light in it at all — survives any grade and any sun. | on |
| `<name>_normal.png` | **tangent space.** R = s, G = t-up, B = out of the face, **A = cavity + macro AO**. | **off** |
| `<name>_mask.png` | **R = key light** (N·L at this aspect's key, *including the form cast shadow*), **G = sky occlusion**, **B = depth** (the height field), **A = coverage**. Packed exactly like `TreeRig2.packMask`. | **off** |

Luminance in the lit channels sits inside **16–244** so a grade has headroom. A night
set is a re-bake with a different key, not a grade.

### Lighting one live

```hlsl
// wall basis from its plan azimuth: Ts along the cliff, Nw outward, world up
float3 S   = float3(sin(sunAz)*cos(sunEl), cos(sunAz)*cos(sunEl), sin(sunEl));
float3 L   = float3(dot(S, Ts), S.z, dot(S, Nw));      // sun in tangent space
float4 n   = tex2D(_Normal, uv); float3 N = n.xyz*2-1; float ao = n.w;
float4 m   = tex2D(_Mask, uv);
float  ndl = saturate(dot(N, L));
float  cast= lerp(1, m.r / max(saturate(dot(N, Lbake)), 0.02), castStrength);  // borrow the baked cast shadow
float  sky = (0.34 + ao*0.66) * ambient;
float  bnc = saturate(-N.y*0.85+0.15) * (0.35+ao*0.5) * bounce;
return tex2D(_Unlit, uv).rgb * (sky*skyColour + bnc*bounceColour + ndl*cast*sunColour);
```

`mask.R` is the one thing a normal map cannot reproduce — a **cast** shadow, from the
buttress ribs and the bench lips. It is baked at the aspect's key. If you displace the
wall with `profile/` you have the geometry to cast it live instead, which is the right
answer for a traversing sun; the baked one is for the cheap path.

---

## 3. The aspect law — W · SW · S · SE · E

`sheets/aspects-*.png`. N faces the camera's back and is not authored. The composer
walks the coastline polygon, takes each segment's outward normal and snaps it to the
nearest of five.

| | W | SW | S | SE | E |
|---|---|---|---|---|---|
| incident | 1.00 | 0.90 | 0.72 | 0.44 | 0.34 |
| light | frontal | frontal | raking | sky dome | sky dome |
| relief | 0.90 | 1.00 | 1.06 | 1.34 | 1.48 |
| weathering | scoured | dry | sun-bleached | damp, lichen | wet, moss |
| `read` | 1.30 | 1.08 | 1.00 | 1.10 | 1.32 |

Incident is the obvious half. The other three are what make it hold up:

- **Where the light comes from.** A lit face is lit frontally, so its relief is crisp,
  local and small. A shadowed face is lit by the **sky dome** — broad, top-down — so its
  relief goes soft. That swap is most of the read.
- **Weathering is permanent** and does not change at 6 pm, which is why the five aspects
  still earn their keep once light is live. Windward W/SW is scoured and bare; lee SE/E
  carries seep, crustose lichen and moss in the joints.
- **`read`** is baked geometry. W and E present a fraction of their true width on screen,
  so their joints and bed lips are drawn thicker and harder or they alias into mush.

---

## 4. The batter — 90° · 76° · 62° · 48°

`sheets/batters.png`. `{slope: 'wall'|'steep'|'ramp'|'bank'}`, or any angle 30–90 for
geometry. Four things move with it and they have to move together, or the bake lies
about its own shape:

| | wall 90° | steep 76° | ramp 62° | bank 48° |
|---|---|---|---|---|
| top set back on 8 m | 0.0 m | 2.0 m | 4.3 m | 7.2 m |
| beds per 9 m of face | 36 | 35 | 32 | 27 |
| sky seen | half dome | +9% | +19% | +28% |
| colluvium + plants | none | in hollows | on benches | general |
| toe to pair with | `notch` | `notch` | `slump` | `slump` |

1. **Light.** The plane tips back, so **L is rotated into the tipped frame** and N·L
   answers honestly. That is why a shaded E bank still catches a high sun while a
   vertical E wall does not.
2. **Sky.** A tipped face sees more of the dome.
3. **Bedding spacing.** Beds are horizontal in the **world**. Measured down a sloped
   surface they are 1/sin further apart, so the bake carries **fewer of them per texture
   metre**. That is the foreshortening tell, and it is what a naive tilt of a vertical
   texture always gets wrong.
4. **Weathering.** A wall shrugs debris off; a slope keeps it. Colluvium in the hollows
   and on the benches, plants with a foothold, seep running **on** the face rather than
   dripping clear of it, and no undercut bed lip to speak of — the lip term scales with
   sin. Ribs weaken and benches deepen: a bank terraces, a wall does not.

**`t` is still 32 px/m along the surface, not along the height.** A battered face at
384×288 covers 12 × 9 m *of surface*, so an 8 m bank needs 8/sin(48°) = 10.8 m of `t`,
i.e. 1.2 tiles. Size the quad by surface length, not by height.

---

## 5. The form pass — the wall is not a plane

`sheets/form-vs-plane.png` is the same 14 m of sandstone, same seed, same aspect, same
camera, with the form pass off and on.

- **Ribs.** Buttress and re-entrant at 4 m and 0.9 m, on an **alternating** lattice —
  sign by cell parity, amplitude by hash — then stretched and clipped. The clipped
  plateaus are the rib crowns and the gully floors; the fast run between them is the
  flank, and the flank is where all the form shading lives.
- **Clefts.** Narrow chimneys on the joint lattice, gated to appear **only in a
  re-entrant**. Clefts are what make ribs read as ribs.
- **Benches.** Three step-backs — the band below the line sits back, its tread lip
  stands proud. Periodic in `t`, so faces still tile vertically, and each one dies out
  along its length: a step of even depth running the whole 12 m is a moulding.
- **Relief.** Detail and form are shaded at **different reliefs**. Detail keeps an
  artistic 8.4× multiplier; form gets the real one — 1.15 m of plan depth across texels
  of 1/32 m. Shading a buttress at the detail relief is how v9 shipped a plane.
- **Occlusion.** A wide-radius AO on the form field (non-directional, so it rides in
  `_unlit` and `_normal.A`) plus a **horizon march along L** for the cast shadow.

### profile/ — the part that needs engine work

`profile/<Rock>{_S76…}.png` is the same form field as **plan displacement in metres**:
grey, 128 is zero, the full range is ±1.15 m. It depends only on the rock seed and the
batter — **not on aspect or wear** — so one profile serves all fifteen bakes of a group.

```
subdivide the wall along s at 0.25 m
for each vertex:  p += Nw * (profile.sample(u, v) - 0.5) * 2 * 1.15
```

Do that and the silhouette, the baked cast shadow and the normal map are all the same
shape; the brow line and the toe stop being smooth curves. Skip it and the depth stops
at the texture — which is exactly the complaint v10 exists to answer.

---

## 6. Import settings

| | Faces | Brow / Toe | Ledges | Profile |
|---|---|---|---|---|
| Wrap | Repeat | **Repeat S · Clamp T** | Clamp | Repeat S · Clamp T |
| Filter | Point | Point | Point | **Bilinear** (it is geometry, not pixels) |
| Alpha Is Transparency | — | on | on | — |
| Compression | None | None | None | None |
| sRGB | on (**off** on `_normal`, `_mask`) | on | on | **off** |
| Mip Maps | off at 1:1 | off | off | off |

## 7. Sampling the wear ladder

Same code as the terrain kit — bracket and lerp between two steps of the same variant:

```hlsl
float f = wear * 2.0;  int a = (int)floor(f);  int b = min(a+1, 2);
half4 c = lerp(SampleStep(a, uv), SampleStep(b, uv), f - a);
```

Wear is a painted channel, so a collapsing headland is a brush stroke.

---

## 8. Known limits

- The face tile repeats every 12 m along the shore and carries **no chunk offset** by
  design; the brow and toe decals, the aspect changes and the batter changes are what
  break it up. On a 200 m straight run you will see it.
- **Brow and toe are still fixed-sun.** Their darks are cast shadow and geometric
  occlusion — the sod lip's undercut, the notch recess — not `N·L`, so a normal map does
  not relight them. They read correctly from mid-morning to late afternoon and go
  slightly wrong at a low sun.
- **Ledge tiles are quantised pixel art** and fixed-sun by nature — 8 steps on a 32 px
  face. Relight with a palette swap per time of day, not a normal map. They take no
  batter: a sloped 32 px riser is a different silhouette, not a different shade.
- Diagonal ledge pieces are 45° only. There is no overhang or arch geometry — `ToeCave`
  is a decal, not a hole.
- Sandstone and Till in the terrain kit are the same red beds but a different bake. They
  are not interchangeable with these; a face should use these.

## 9. Rules this rig paid for

1. **A cliff is block ends, not beds** — but the blocks must be sparse. Move them all and
   you have masonry.
2. **Bed thickness has to vary.** Identical beds read as decking.
3. **A joint that runs edge to edge is a ruled line.** Gate it along its length. The same
   goes for a bed lip and for a bench.
4. **Thresholding ridged fbm does not draw a gully** — it gives smooth swells. A line
   wants a 1D lattice of wandering traces.
5. **Never scale a UV by a non-integer inside a periodic field.**
6. **A grey rock cannot take a red rock's shadow temperature.** The violet that makes
   sandstone read in shadow turns basalt navy. Scale it per material.
7. **Colour identity belongs to the bed, not the block.** Give every block its own value
   and the wall comes out as confetti.
8. **A rounded iso corner is an extra facet, not a curve.** Curves in `t` stack into slits.
9. **Value noise on a handful of cells does not span its own range.** Six cells came out
   entirely above the mean, so the buttress field was a constant offset — a flat wall.
   Alternate the sign per cell, then stretch and clip.
10. **Form and detail want different reliefs.** Shading a buttress at the fine-detail
    relief is how a rig ships a plane and wonders why everything reads flat.
11. **A batter is not a shading multiplier.** Rotate L, widen the sky, respace the
    bedding, keep the debris. Any one of those alone reads as a lighting bug.
