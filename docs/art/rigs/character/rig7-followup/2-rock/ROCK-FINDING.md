# Job 2 — FINDING: the rock delta is not a three-bone table

Measured, not argued. Every number here comes from check/check-rock-additive.cjs.

## What 09-09 §2.5 expected

`additive(rock, build)` returning bone-space deltas for the counter-lean (spine / chest / head) plus
the head look (neck / head) — about three bones, keyed on the rock and the build alone.

## What rig 6 actually does

`rock` reaches `pose()` as `arguments[5]` and is read in exactly one place:

    if(rock && rock.counter){ const c = counterLean(rock.roll||0, rock.pitch||0, rock.counter);
      list += c.list*DEG; lean += c.lean*DEG; }

`counterLean` returns POSE PARAMETERS — `{list, lean}` in degrees, not rotations — and they are folded
into the torso's list and lean. Everything downstream is then re-derived from the new torso.

Two consequences, both measured at rock `{roll:10, pitch:5, counter:1}` over idle / walk / balance /
astride, every frame, fisher:

### 1. The legs are safe. The arms are not.

21 of the 45 bones move. 24 are untouched — and those 24 include every hip, knee, ankle, foot, the
pelvis and the inseam. The brief's worry that "counterLean moves IK targets so the legs move" does
not happen: the feet stay planted, which is the whole point of a counter-lean.

But **both arm chains move**, and not by a little:

| bone | max local rotation | spread across clips and frames, for ONE rock |
| --- | --- | --- |
| shoulder_L | 178.9 deg | 176.5 deg |
| elbow_L | 170.7 deg | 173.9 deg |
| wrist_L / hand_L | 83.2 deg | 152.5 deg |
| shoulder_R | 176.1 deg | 175.4 deg |
| elbow_R | 170.9 deg | 172.6 deg |
| wrist_R / hand_R | 86.6 deg | 152.6 deg |

The cause is that rig 6 specifies the hand targets in the FIGURE frame
(characterIsoRig.js:321-328): `tx`, `ty`, `tz` are absolute, not relative to the chest. Listing the
torso moves the shoulder while the target stays where it was, so the arm is re-solved to reach it.
A spread of 176 deg for one and the same rock is not a delta — it is a different pose.

### 2. Even torso, neck and head are clip-dependent

| bone | max local rotation | spread across clips and frames |
| --- | --- | --- |
| torso | 10.964 deg | 1.831 deg |
| neck | 21.643 deg | 10.564 deg |
| head | 10.909 deg | 4.699 deg |
| carry_mid | 10.964 deg | 5.368 deg |

The torso is close to constant (1.8 deg of wobble) but not constant. The neck swings 10.6 deg
depending on which clip and frame it is applied to.

Nine of the 21 touched bones DO have a constant delta — `neck_tip`, the four limb `_tip` bones, and
`tool_L/R` + `carry_L/R`. All nine are pure position with zero rotation: they are tips and pins that
simply inherit their parent's move.

## So the table cannot be keyed on (rock, build)

A three-bone table would leave both arms unmodelled by up to 178.9 deg. A 21-bone table keyed on
the rock alone would still be wrong by the spreads above — 1.8 deg at the torso, 10.6 at the neck,
176 at the shoulder. Neither holds 1e-4.

Per brief §2, that is reported here rather than widened silently.

## What this part ships instead

`additive(rock, build, anim, u)` — the same table, keyed on the frame as well, which is the honest
signature for what rig 6 computes:

    additive({roll,pitch,counter}, build, anim, u)
      -> { rock, anim, u, build, touched, of,
           bones: [{ bone, rot:[x,y,z,w], rotEuler:[x,y,z] deg, pos:[dx,dy,dz] m, deg, off }] }

`rot` is the local rotation delta as a unit quaternion, `pos` the local position delta. Compose onto
the plain clip's local transform for that frame and the rocked pose comes back exactly. Bones whose
delta is below 1e-3 deg and 1e-6 m are omitted, so the table carries only what moves.

`additiveGrid(build, grid, anims)` walks a whole grid. `goldenDiffRocked(anim, u, build, rock)` and
`goldenDiffRockedDetail` are the checkers.

Over the brief's grid — roll and pitch in {-15, -10, -5, 0, 5, 10, 15}, counter in {0, 0.5, 1}, four
animations, two clip phases, 1176 rows — `clip(anim,u,build) + additive(rock)` matches rig 6's
`facesOf(pose(anim,u,build,rock))` to a worst of **6.80e-16 m**.

Being exact is not the achievement: the table is the measured difference, so of course it closes.
The finding is its SHAPE — 21 bones, frame-keyed, both arms re-solved.

## What the landing lane should decide

Three options, in increasing order of work and of quality:

1. **Ship the frame-keyed table.** Exact today. Costs one delta set per (rock sample, clip, frame),
   so the extractor stores a rocked variant per sea state per clip — which is close to what a clip
   per sea state would have cost, and is what §1.2 wanted to avoid.
2. **Counter-lean the arms in the chest frame.** Change rig 6 so the hand targets are specified
   relative to the chest rather than the figure. Then a torso list carries the arms with it, the IK
   does not re-solve, and the delta really does collapse to about three bones. This changes the look
   of every existing rocked frame, so it is a design decision, not a refactor.
3. **Accept a three-bone approximation** with a stated error. At rock {10, 5, 1} that error is up to
   178.9 deg of arm rotation, so this is only viable if the arms are re-solved at runtime from the
   rocked torso — i.e. the engine runs the IK, not the clip.

Option 2 is the one that makes 09-09 §2.5 true. It is out of scope here and not attempted.
