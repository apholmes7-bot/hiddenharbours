# HH rig7 follow-up — part 2 of 4: the additive rock table

Brief: BRIEF-2026-09-17-rig7-helm-oars-rock-mount-and-noses.md, Job 2.
Input: the accepted bytes of the rig chain (main bd5d28b2), verbatim in reference/rigs-as-delivered/.

## Read ROCK-FINDING.md first

Job 2 asked for `additive(rock, build)` and said that if the delta is not confined to a handful of
bones, that is a finding to report rather than a table to widen silently. It is not confined, so
the finding is the main deliverable:

* the delta touches **21 of 45 bones**;
* the legs, pelvis, feet and inseam are **clean** (24 bones) — the IK feet are not disturbed;
* but **both arm chains move**, by up to **178.9 deg**, with up to **176.5 deg of spread** across
  clips and frames for one and the same rock, because rig 6 specifies the hand targets in the
  figure frame and a torso list re-solves the arms;
* and torso / neck / head — the three bones 09-09 §2.5 expected — are themselves clip-dependent
  (1.8 / 10.6 / 4.7 deg of spread).

So no table keyed on (rock, build) alone can hold 1e-4. Details, the mechanism and three options
for the landing lane: **ROCK-FINDING.md**.

## What this part adds

In characterIsoRig7.js, all new surface — nothing existing changes:

| function | what it does |
| --- | --- |
| `additive(rock, build, anim, u)` | the bone-space delta table for one frame: per bone `{bone, rot, rotEuler, pos, deg, off}` |
| `additiveGrid(build, grid, anims)` | the same across a whole (roll, pitch, counter) grid |
| `goldenDiffRocked(anim, u, build, rock)` | plain clip + additive vs rig 6's own rocked pose, in metres |
| `goldenDiffRockedDetail(...)` | the full record: `{max, at, faces, touched, err, ok}` |

It is keyed on `anim` and `u` as well as the rock — that is the finding, not an oversight.

## The proof (check/check-rock-additive.cjs, 10 gates, all pass)

| what | result |
| --- | --- |
| goldenDiffRocked over the brief's grid | **6.80e-16 m** worst of **1176 rows**, gate 1e-4 |
| grid | roll x pitch in {-15,-10,-5,0,5,10,15}, counter in {0,0.5,1}, 4 anims, 2 clip phases |
| legs / pelvis / feet / inseam | untouched by any rock |
| bones | 45, unchanged |
| ANIMS | byte-identical, 35 animations |
| the existing 35 clips | byte-identical on all ten presets |
| the unrocked golden rule | unmoved on every clip and preset |

checks.txt carries the per-grid-point maxima for all 147 points and the worst 20 rows.

### On "2 dirs"

The brief asks for the grid x 4 anims x 2 dirs. `facesOf(pose(...))` is camera-independent — a
facing cannot enter a pose-level comparison, only `render(dir, ...)` takes one — so a literal "dir"
axis would have compared each row against itself. The two samples per (grid point, anim) are two
CLIP PHASES instead, frame 0 and the mid frame, which keeps the brief's 1176 rows and actually
varies the thing under test. Say the word if you want the facing axis run through `diffPixels`
instead; that is a pixel check, not a golden one.

### counter: 0 rows are the identity

`rock` reaches `pose()` only through `counterLean`, and only when `counter` is set, so all 392
`counter: 0` rows are exact no-ops (768 of 1176 rows carry a non-empty delta; roll 0 with pitch 0
is also a no-op). They are checked anyway, and they are exactly the identity.

## LANDING ORDER — parts 1, 2 and 3 all touch characterIsoRig7.js

Different functions, so they merge, but do not let one zip overwrite another's file:

* part 1 adds the CARRY_CLIPS block after `clips()`, plus API names and one `exportBuild` key.
* part 2 (this one) adds the additive block before the golden-rule section, two quaternion helpers
  next to `rnd`/`rndR`, plus API names.
* part 3 changes one token in the boot-shaft guard inside `solve()`.

## Layout

    README.md                        this file
    ROCK-FINDING.md                  the finding: the bone list, the mechanism, the options
    checks.txt                       the checker's output, 10 gates, the full grid tables
    rigs/                            the chain to install; only characterIsoRig7.js differs
    reference/rigs-as-delivered/     the accepted input bytes, untouched, for the diff gates
    check/load-kit.cjs               loads a chain the way the bake installs it
    check/check-rock-additive.cjs    the gates; `node check/check-rock-additive.cjs`
    SHA256SUMS.json                  sha256 of every file in this part
