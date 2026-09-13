# Hidden Harbours · character and clothing workbench

This is the versioned, offline authoring study for **CW01**, the first stage of the
13 September character creator/wardrobe request. It preserves the ten rig presets
and all 35 animations from the owner's cast viewer, with pass05 garment/jaw refinement,
pass06 eye revision and a bounded Fisher clothing assembly proof. **It does not implement the in-game creator,
wardrobe, purchases, appearance save, or production expressive face renderer.**

## Build and inspect

From the repository root, with Node.js (standard modules only):

```powershell
node docs/art/character-workbench/build-viewer.cjs
node docs/art/character-workbench/check-viewer-gestures.cjs
node docs/art/character-workbench/wardrobe-validate.cjs
node docs/art/character-workbench/check-face-and-boots.cjs
node docs/art/character-workbench/check-face-beauty.cjs
node docs/art/character-workbench/check-eye-refinement.cjs
node docs/art/character-workbench/review-eyes.cjs
node docs/art/character-workbench/check-character-finish.cjs
node docs/art/character-workbench/review-faces.cjs
node docs/art/character-workbench/compare-character-finish.cjs
```

Open the generated `Hidden-Harbours-Cast-Viewer.html` in a browser; it works offline.
Open `review/face-comparison.html` for the before/after comparison. Generated HTML,
PNG and detailed JSON reports are ignored by Git; all required source is here.
The viewer build reads the individual authored fit/garment JSON files directly;
it does not depend on a generated catalogue from an earlier validation run.
The complete all-cast finish comparison is `review/character-finish/comparison.html`.
The direct previous/revised eye comparison is `review/eyes/eye-comparison.html`;
`review/eyes/owner-eyes-64.png` is the compact labeled close-up sheet.
The optional original browser runner, `check-viewer.cjs`, needs Playwright and Edge;
it is not required to build. It was **not rerun** for CW01 because the available
browser tool rejected local-file navigation. Static raster comparisons were viewed
directly with the image tool; browser interaction/layout acceptance remains pending.

- **Detail · 64 px/m** is the default. Gameplay · 32 px/m and Portrait · 96 px/m are
  inspection densities; the geometry stays in metres and the game's PPU stays 32.
- Play/pause, frame scrub and 360° drag/turntable remain available. Single actions
  stop at their endpoint; the viewer pauses when its tab is hidden.
- **Art pass** switches between original pass04, previous eyes pass05 and revised eyes pass06, holding
  the current rotation, expression, animation time and density. The initial view is a
  relaxed idle at a 25° turn, making the character volumes easier to inspect.
- Inspect a character for the full body and enlarged head; choose one of eight
  expressions or speaking. Pupils move in fixed sockets, with staged blinks and
  occasional double blinks; four speech mouths retain the expression's brows.
- Fisher offers original clothing, the assembled starter set and the mixed set.
  This prototype supports **only the Fisher modular fit**. Every cast preset receives
  its own garment tailoring and finish; those edits do not grant interchangeable fits.

The comparison uses 0°, 35°, 90°, 145°, 180°, 270° and 325° turns at 64 and 32 px/m.
Each pair is **pass 04 before, pass 06 after**. PNG rows are Fisher, Ginny, Skipper,
Nan, Deck boss, Packer, Cutter, Deckhand, Wharf boy and Wharf girl. Expressions have
a separate before/after comparison. The camera is the inherited 40° orthographic
art preview, not a capture of the current in-world camera.
`review/face-expressions-enlarged.png` has rows neutral, smile, grin, frown, worry,
surprise, effort, weary; columns Fisher 64 before/after, Fisher 32 before/after,
Deck boss 64 before/after and Deck boss 32 before/after.

## Eye revision after owner review

The owner rejected the pass05 eyes. Its pixel-count checks caught isolated white marks,
but did not establish an appealing eye design: the large rectangular pupil and dark
outer halves could read as flat slots or an unfocused gaze.

Pass06 uses rounded openings with a gently capped upper lid, smaller round pupils,
balanced light corners, warmer eye ink and a slightly closer eye/brow placement.
The opening, iris and pupil share the same physical surface at every density. Head
geometry, mouth, identity choices and face animation timing are unchanged from pass05.
The previous face is preserved in `sources/face-rig-pass05.js` for direct comparison.
`review-eyes.cjs` and `EYE-REFINEMENT-REVIEW.md` cover this eye-specific revision;
the historical `check-face-beauty.cjs` still validates pass04/pass05 separately.

## What changed in pass05 (preceding finish)

Eyes now use a clear dark pupil/iris cluster and a restrained warm sclera. The old
subpixel pupil could disappear between raster samples, leaving a disconnected white
square. The new material remains fixed on the head surface at both densities; there
are no camera-facing cards or changes to world scale. Softer, wider lower-jaw planes
and a clearer resting mouth make the neutral face less pinched. Expression, blink,
gaze and speech timing remain; the pupil has its own append-only face-material index.

All ten presets receive explicit garment tailoring: chest, sleeves, leg volumes,
boot shafts and relevant skirt/apron shapes. Garment ramps and hat colours have a
coherent value hierarchy; selected skin/hair colours, body identities, skeletons,
physical heights and protected hand/foot/attachment geometry remain unchanged.
No mesh faces are added. `character-finish.json` contains the editable per-preset
parameters and material roles; `character-finish.js` applies them before skinning.

The preceding face is preserved in `sources/face-rig-pass04.js`. The before path skips
the finish module while using the same rig/clip inputs, so the comparison holds pose
and scale constant. At 32 px/m, occluded/profile eyes still naturally simplify.
See `CHARACTER-FINISH-REVIEW.md` for measured gains and the inherited motion limits.
Production camera/shader appearance remains unverified.

## iPhone review viewer

The [private character viewer](https://hidden-harbours-character-viewer.apholmes7.chatgpt.site)
is hosted for the owner. Open it in Safari and sign in with the same ChatGPT account
if prompted. Its source is this workbench; rebuilding the offline page remains supported.

At widths up to 700 px the viewer starts with an enlarged face, Face / Full body
tabs, previous/next character buttons, and folded Animation & appearance controls.
Horizontal drags turn the character; vertical swipes scroll, and pinch zoom remains
available. Controls have 48 px touch targets, form text stays at 16 px, and the page
accounts for screen safe areas. The cast gallery uses two columns on phones. Desktop
retains the full gallery and simultaneous body/face inspection. Preview density
remains 64 px/m; these layout changes do not alter the art or the game's PPU.

Before/previous-pass characters and wardrobe combinations are created on demand.
Only the selected face/body canvas renders on phones. Nine Node gesture checks cover
taps, direction intent, pointer ownership, cancellation, pinch and recovery. They do
not test browser layout or real touch hardware; physical iPhone and browser acceptance
remain pending. An optional feature-detected WebMCP `inspect_character` tool uses the
same selection path; no supporting WebMCP context was available to verify its contract.

The build compiles the final embedded JavaScript before writing the page. Template
substitutions use callbacks so literal JavaScript replacement tokens cannot corrupt
the generated script. This fixes a packaging failure that pose checks alone missed.

The private Site is project `appgprj_6aa7294cb19c81918e3906196133b53c`, with a separate
deployment checkout at `C:/hh-character-viewer-site`. Its committed static entrypoint
is `dist/index.html`, copied from the generated standalone viewer. Reuse this Site
for future viewer updates and retain owner-private access. Deployment succeeded on
13 September 2026. Hosting does not establish physical iPhone or production Unity
acceptance.

## Source provenance and boot correction

`sources/` contains snapshots from the 13 September cast viewer:
`C:/Users/aphol/.codex/visualizations/2026/09/13/01a09bee-b46e-74c1-9638-96c10598fd2c/cast-viewer`.
The original 03 face is kept in `face-rig-pass03.js`; `face-rig.js` is the editable
06 study; passes 04 and 05 are also preserved for comparison. `face-render.cjs` uses a small standard-library PNG writer, removing
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
for all ten presets. The pass05 face test separately checks its intended jaw edits,
unchanged upper head geometry, expression masks and sampled eye readability.

The viewer build checks both art passes for the ten cast presets and two Fisher
clothing variants across 35 animations and 13 times (10,920 pose samples), finite
geometry, a four-metre radial envelope, and bone-only/full-solver parity at u=0.37.
These are selected pose checks, not proof
of contact against tools, seats, vehicles or boats, nor a Unity performance result.
See `wardrobe-proof.md` for assembly measurements and the supported fit boundary.

No source here ships as a runtime JS dependency. The full create/buy/equip/reload
acceptance loop remains pending in the subsequent CW stages.

## Reproduce the full-cast finish comparison

```powershell
node docs/art/character-workbench/compare-character-finish.cjs
```

Open the generated `review/character-finish/comparison.html` to compare preserved
pass04 with pass06 across all ten characters, eight headings, selected work/vehicle
poses, and 64/32 px/m body and face plates. All inputs are checked in; no historical
checkout or image package is required. Generated PNGs and measurement JSON stay in
the ignored review folder.

The frozen run passed 4,550 pose pairs and 960 raster checks with no new degenerate
triangles or clipped/empty renders. It preserves 7,880,600 protected vertex samples
against the body-off control. See [the independent review](CHARACTER-FINISH-REVIEW.md)
for visual findings, the inherited zero-area source triangles, and the unresolved
dismount/contact limits.
