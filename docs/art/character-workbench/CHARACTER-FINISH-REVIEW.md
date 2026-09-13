# Character finish 05: independent comparison

**Superseded eye design:** the owner subsequently rejected pass05's eyes. Its eye
pixel counts below are historical technical evidence, not art approval. See
`EYE-REFINEMENT-REVIEW.md` for the pass05/pass06 eye revision. The full-cast script
now regenerates pass04/pass06 plates; the garment and jaw finish remains pass05.

Run `node docs/art/character-workbench/compare-character-finish.cjs` from the repository
root. It writes labeled PNG plates, a comparison HTML page and measurement JSON under
ignored `review/character-finish/`. All inputs are source-controlled; the before side
uses the preserved `sources/face-rig-pass04.js` and disables the body finish. It does not
need an archive, an earlier checkout, a browser or an image package.

Each pair is **A: pass04, B: pass05**. All ten cast members appear at eight headings,
at **64 px/m** and **32 px/m**, with walk, haul, drive and mount-down pose samples.
The camera remains the same 40-degree orthographic preview. All casts and both passes
share one metre-based crop per plate. The 32 px/m images are enlarged twice using
nearest-neighbour display; neither the model nor the sampling density changes.

## Baseline observations

The archived 64/32 px/m lineups were inspected before editing. Bright isolated eye
whites dominated several faces at 32 px/m, especially Fisher, Skipper and Deckhand;
the neutral gaze read sideways or tired. Cutter's chin appeared pinched. Large dark
boot wedges met narrow calves, and Fisher's many strap/pocket blocks made the torso
busy. Blond hair and the oilskin collar merged on Skipper. These are art-read findings,
not numerical attractiveness scores.

The starting strengths are distinct heights and proportions across the cast, clear
apron/skirt silhouettes, and the teal, red, ochre and navy work-clothes palette. The
pass should retain those identities while simplifying competing details.

## What the checks establish

The geometry check samples all 35 clips thirteen times for all ten cast members.
It compares the finished body against a control using the **same pass05 face with
body finishing disabled**, so intended eye/jaw changes cannot mask a body regression.
It checks unchanged identity fields, bind skeleton, face topology and weights,
protected head/neck/hand/thumb/foot vertices, and skin/hair/beard/eye material metadata.
Posed bone anchors and protected geometry must remain identical. Finite coordinates,
model-space triangle area and measured metre bounds are recorded.

The published turn/action raster plates are checked for nonempty output and clipping.
These selected renders do not cover every continuous pose or camera phase. Bone and
hand/foot equality do not establish contact against an actual boat, chair, tool or
terrain. Existing degenerate triangle samples are counted separately; the finish must
not introduce new ones. Results are offline CPU evidence, not Unity performance,
in-world camera acceptance or a claim that all garment self-intersections are absent.

The generated measurement report records contributing source hashes. The checked-in
script and preserved pass04 head make the visual comparison reproducible without
duplicating the complete historical workbench.

## Frozen pass05 results — 13 September 2026

The comparison was regenerated after the final face, garment and hat palette edits.
All eight recorded source hashes matched the files inspected for this review.

| Check | Result |
| --- | --- |
| Cast / clips / samples per clip | 10 / 35 / 13 |
| Before/after pose pairs | 4,550 |
| Body-control bone pose pairs | 4,550 identical |
| Protected posed vertex samples | 7,880,600 identical |
| Published turn/action rasters | 960; none empty or clipped |
| Nonfinite coordinates / new degenerate triangles | 0 / 0 |
| Inherited degenerate triangle samples | 18,200 in both pass04 and pass05 |

The inherited degeneracies are repeated zero-area head/hat fan endpoints on seven
presets, not 18,200 distinct bad faces. The check rejects newly degenerate triangles
against both pass04 and the pass05 face/body-off control. It does not assert that
every source triangle was valid before this pass.

The JSON includes minimum/maximum metre coordinates for every character across the
sampled clips. For example, Fisher's all-pose x range changes from
`[-1.25886, 0.61419]` to `[-1.26246, 0.61419]` m. These are posed envelopes, not
standing heights or a ground-contact test. The separate `check-character-finish.cjs`
run records a maximum bind-vertex adjustment of 0.0322 m, unchanged standing height,
and unchanged protected geometry and attachment bones. No faces are added.

Final front/back turn plates, face plates at both densities, and action plates were
visually inspected. The dark eyes read as connected features rather than isolated
white squares; Fisher and Deckhand look less weary, and Cutter's lower jaw is less
pinched. Darker trouser values distinguish Skipper's lower body from the ochre
oilskin. The hat and garment colours now belong together, especially on Deck boss,
Packer and the children. Ginny and Nan retain their red family through terracotta
hats, while their apron/skirt silhouettes remain distinct. These are visible
improvements at the preview scale; they are not an overall production-quality score.

The separate `check-face-beauty.cjs` matrix records white-only eyes falling from
5 to 0 across 240 renders per pass, with sclera pixels falling from 683 to 104 and
dark eye pixels increasing from 1,362 to 1,624. Pass04's dark count conservatively
includes lashes. The final pupil core uses the `0.48 × eye height` threshold so
vertical gaze still moves inside the fixed socket. These are sampled readability
checks, not guarantees for every expression or pixel phase.

## Remaining visual limits and viewer review

At 32 px/m some profile eyes disappear naturally behind the hat or hair, particularly
Wharf girl's fringe. Fisher's straps/pockets still produce several small torso
clusters; simplifying them further is an art decision for an in-world review.
The low-density profile boot silhouette remains angular even though the calf/shaft
transition is more coherent.

`body-actions-32.png` and `body-actions-64.png`, **mountDown at u=0.5, 45°/225°**,
show a particularly awkward extreme pose on Nan and Wharf boy: the skirt/torso and
extended limbs read poorly without the vehicle. The same pose is present on the A
and B sides. This comparison found no new triangle degeneracy or protected-geometry
change that would establish a newly detached mesh. A vehicle/seat contact review is
still required before accepting that animation in production.

Code review of `viewer-controller.js` confirms that the pass selector changes only
`state.pass` and redraws. Both cached casts use the same clip, phase, face time, camera
angle, density and expression controls; each uses its own head sampler and colours.
Wardrobe variants retain the selected recipe and their base character's head study.
The full-body crop comes from shared bounds covering both art passes. While playback
is running, time continues normally; pause or scrub for a fixed-pose comparison.

A focused independent check compared the viewer's pass04 alias with a separate
`loadStudy({finishPass:'before'})` context: all ten builds, heads, faces, bind bones,
material maps and face colours were identical, as were 40 representative posed
outputs (idle, haul, drive and mountDown). The production rig has no diff. Live
browser layout and input interaction were not exercised in this lane, and no Unity
editor, bake, shader or gameplay acceptance is claimed.
