# Hidden Harbours · character and clothing workbench

This is the versioned, offline authoring study for **CW01**, the first stage of the
13 September character creator/wardrobe request. It preserves the ten rig presets
and all 35 animations from the owner's cast viewer, with minor face polish and a
bounded Fisher clothing assembly proof. **It does not implement the in-game creator,
wardrobe, purchases, appearance save, or production expressive face renderer.**

## Build and inspect

From the repository root, with Node.js (standard modules only):

```powershell
node docs/art/character-workbench/build-viewer.cjs
node docs/art/character-workbench/wardrobe-validate.cjs
node docs/art/character-workbench/check-face-and-boots.cjs
node docs/art/character-workbench/review-faces.cjs
```

Open the generated `Hidden-Harbours-Cast-Viewer.html` in a browser; it works offline.
Open `review/face-comparison.html` for the before/after comparison. Generated HTML,
PNG and detailed JSON reports are ignored by Git; all required source is here.
The viewer build reads the individual authored fit/garment JSON files directly;
it does not depend on a generated catalogue from an earlier validation run.
The optional original browser runner, `check-viewer.cjs`, needs Playwright and Edge;
it is not required to build. It was **not rerun** for CW01 because the available
browser tool rejected local-file navigation. Static raster comparisons were viewed
directly with the image tool; browser interaction/layout acceptance remains pending.

- **Detail · 64 px/m** is the default. Gameplay · 32 px/m and Portrait · 96 px/m are
  inspection densities; the geometry stays in metres and the game's PPU stays 32.
- Play/pause, frame scrub and 360° drag/turntable remain available. Single actions
  stop at their endpoint; the viewer pauses when its tab is hidden.
- Inspect a character for the full body and enlarged head; choose one of eight
  expressions or speaking. Pupils move in fixed sockets, with staged blinks and
  occasional double blinks; four speech mouths retain the expression's brows.
- Fisher offers original clothing, the assembled starter set and the mixed set.
  This prototype supports **only the Fisher fit**. Other cast members retain their
  original clothing; they are not approved modular fits.

The comparison uses 0°, 35°, 90°, 145°, 180°, 270° and 325° turns at 64 and 32 px/m.
Each pair is **pass 03 before, pass 04 after**. PNG rows are Fisher, Ginny, Skipper,
Nan, Deck boss, Packer, Cutter, Deckhand, Wharf boy and Wharf girl. Expressions have
a separate before/after comparison. The camera is the inherited 40° orthographic
art preview, not a capture of the current in-world camera.
`review/face-expressions-enlarged.png` has rows neutral, smile, grin, frown, worry,
surprise, effort, weary; columns Fisher 64 before/after, Fisher 32 before/after,
Deck boss 64 before/after and Deck boss 32 before/after.

## What changed in the face

The eye centres move inward 2 mm per side; eye width/height shrink 2 mm. The dark
pupil core shrinks 2 mm and retains a distinct iris border and sclera. Neutral brows
move up 4 mm and expressive slopes remain explicit. The resting mouth widens 8 mm;
the nose tip recedes 5 mm, with a slightly narrower lower base. All remain surface
material landmarks attached to the head; there are no camera-facing eye cards.

The original expression/blink/gaze/speech functions and cast identity recipes are
unchanged. Jaw transitions, fringe, hair/hat contact, beard-mouth overlap, dark and
light skin were reviewed at both densities and retained; only four nose facets
change geometry. Fisher retains the earlier body study. The other nine bodies have
**not** been individually redesigned. At 32 px/m some pupil and mouth detail still
collapses into single pixels. Production camera/shader appearance is unverified.

## Source provenance and boot correction

`sources/` contains snapshots from the 13 September cast viewer:
`C:/Users/aphol/.codex/visualizations/2026/09/13/01a09bee-b46e-74c1-9638-96c10598fd2c/cast-viewer`.
The original 03 face is kept in `face-rig-pass03.js`; `face-rig.js` is the editable
04 study. `face-render.cjs` now uses a small standard-library PNG writer, removing
the earlier machine-specific Sharp dependency. `load-study.cjs` centralizes loading.

The frozen rig 7 snapshot is the original production source. The inherited viewer
clamped the boot cuff's x/y fallback but left z at the standing height. The corrected
preview interpolates **all three coordinates** on ankle-to-knee when the horizontal
boot-top plane does not intersect the shin. Valid standing intersections keep their
exact old output. The in-memory bone-only shortcut is compared with the full solver.

**Production rig 7 and committed Unity assets remain unchanged.**
`boot-segment-fix.cjs` applies the reviewed preview correction and reproduces
`production-boot-segment.patch`. Its exact contents are regression checked. The
production port is pending **CW02**, together with a real Unity rebake and freshness,
pose/contact and integration checks. Applying the patch alone makes committed bake
fingerprints stale; never update their hashes without performing the real bake.

```powershell
node docs/art/character-workbench/boot-segment-fix.cjs
git apply --check docs/art/character-workbench/production-boot-segment.patch
```

## Evidence and limits

CW01's no-Unity checks pass: 57,600 preserved face state samples; 8,750 pose samples
(10 cast × 35 animations × 25 times); 17,500 boot cuff segment checks. The old solver
reaches a 494.89985 m absolute cuff coordinate; corrected preview peaks at 1.14763 m
in that sample matrix. 3,089 sampled cuffs differ. Rest skeletons remain identical
for all ten presets, and only the four nose facets differ in the head geometry.

The viewer build checks 5,460 poses (ten original cast presets plus two Fisher
clothing variants × 35 animations × 13 times), finite geometry, a four-metre radial
envelope, and bone-only/full-solver parity at u=0.37 for each of the 420 cast/variant
and animation pairs. These are selected pose checks, not proof
of contact against tools, seats, vehicles or boats, nor a Unity performance result.
See `wardrobe-proof.md` for assembly measurements and the supported fit boundary.

No source here ships as a runtime JS dependency. The full create/buy/equip/reload
acceptance loop remains pending in the subsequent CW stages.
