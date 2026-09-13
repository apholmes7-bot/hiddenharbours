# Eye refinement 06 — independent evidence

Run from the repository root:

```powershell
node docs/art/character-workbench/check-eye-refinement.cjs
node docs/art/character-workbench/review-eyes.cjs
```

The scripts compare checked-in `sources/face-rig-pass05.js` with the current
`sources/face-rig.js`, using the same rig inputs and body finish. They need Node's
standard library only. Generated output stays under ignored `review/eyes/`:

- `owner-eyes-64.png`: Fisher, Ginny, Deck boss and Wharf girl, before/after at
  0°, 25° and 335°, sampled at 64 px/m and enlarged three times without smoothing.
- `eye-comparison.html`: labeled all-cast turns at 64/32 px/m, two horizontal pixel
  phases, expression plates, staged blink plates and the vertical-phase diagnostic.
- `eye-checks.json`: exact invariant results and both eyes' material counts for
  every sampled cast, expression, heading and pixel phase.
- `eye-plates.json`: plate parameters and contributing source hashes.

## What is protected

The check compares all ten characters' identity/build fields, bind data, complete
weighted character faces and material maps exactly. Head geometry, topology and UVs
are also compared directly: **3,192 faces and 13,188 vertices are identical to
pass05**. This catches an incidental hair or jaw edit even if the eyes look correct.
Non-eye face palette entries remain exact. The eye spacing adjustment also moves
the brow centres because they share the eye anchor; unchanged brow masks are not
claimed.

**19,200 face-state pairs and 350 clip-state pairs match pass05**, covering blink,
gaze, expression selection and speech timing. All eight expression masks and four
speech mouth masks remain distinct. Forty pupil-direction checks cover both eyes
and five authored eye shapes; gaze moves the pupil inside a fixed socket. Fully
closed eyes expose no iris, pupil or sclera. Seven staged closures for each eye and
shape retain eye marks, and the last open rim overlaps the closed dash rather than
jumping to a disjoint position. These mask checks use a dense surface grid; game
pixels can still hide a feature between samples.

The raster matrix has **2,800 renders per pass**: ten characters, two densities,
seven headings, four horizontal/vertical pixel phases and five expressions
(neutral, smile, grit, weary, surprise). No head is empty or clipped. Diagnostic
counts separate each eye, so a visible pupil in one eye cannot conceal its absence
in the other. Pixel counts are observations, not attractiveness targets or art
approval.

## Visual review

The chosen round socket with a gently capped upper edge reads less like the flat
rectangles of pass05. At 64 px/m the smaller pupil, visible iris and balanced pale
corners give Fisher and Ginny a more focused gaze. Warmer eye ink fits the face
palette. Deck boss retains more separation between eyes and surrounding dark skin;
Wharf girl's fringe still naturally hides much of the eyes at oblique headings.
The staged blink plates show the opening narrowing toward the same closed-lid
region. No geometry or identity improvement is inferred from an eye-only change.

The initial selected candidate showed a concrete low-density weakness: at
**25°/335°, neutral, pixel phase [0.5, 0.5]**, an exposed eye corner on eight adults
lost its iris/pupil samples and retained one or two sclera pixels with the lash.
The result looked pale and hollow as the sampling phase shifted. This observation
prompted a bounded iris-coverage correction and a focused regression check.

## Final revalidation — 13 September 2026

The final source uses iris width radius `0.0255` m, height radius `0.033` m and a
`0.012` m lower colour arc. The pupil, socket and lid shapes remain the selected
capped design. **All 240 neutral eye samples at 32 px/m, 0°/25°/335° and four pixel
phases now retain iris/pupil colour whenever sclera is visible.** The dedicated
regression assertion passes. `eye-phase-risk-32.png` now displays the corrected
phase comparison; the compact owner plate retains the chosen 64 px/m appearance.
The final owner, phase, turn and staged blink plates were inspected again. No new
blocking finding remains in this offline eye review.

The broader matrix still records 242 sclera-without-iris/pupil eye samples among
5,600 eye observations in pass06: 170 at 32 px/m and 72 at 64 px/m. These remain
descriptive: most are partially exposed eyes at 45°/315° at 32 px/m or strict
90°/270° profiles at 64 px/m, with a few weary-expression samples. They do not
invalidate the targeted neutral correction, nor establish perfect eyes at every
heading. Small corner highlights still change with pixel phase, and Wharf girl's
eyes remain subdued under the fringe, especially on dark skin. The reports retain
every case instead of declaring that all white-only material samples are gone.

The final normalized face-source SHA-256 is
`31a9e0eda3539aecc6637ed4b5bd9d6590f94446034bfaa5ba95d8c2ada37540`.
All invariant checks above pass, including 70 staged eye-closure masks and the
5,600 head rasters with no empty or clipped output. Both generated manifests record
the contributing sources for reproduction.

No browser interaction, Unity shader/camera, in-world contact or performance was
tested in this lane. The owner still judges whether the eyes look good; passing
invariants and avoiding a particular pixel failure do not supply that judgment.
