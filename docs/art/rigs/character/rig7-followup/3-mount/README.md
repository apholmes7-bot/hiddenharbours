# HH rig7 follow-up — part 3 of 4: the mount* boot excursion

Brief: BRIEF-2026-09-17-rig7-helm-oars-rock-mount-and-noses.md, Job 3.
Input: the accepted bytes of the rig chain (main bd5d28b2), verbatim in reference/rigs-as-delivered/.

## What was wrong

Rig 6 cuts the boot shaft at a fixed world HEIGHT, by intersecting the plane z = ankleZ + bootZ*hS
with the shin:

    const bz = P.ankleZ + G.bootZ*hS, t = (bz - a[2]) / Math.max(0.001, (knee[2] - a[2]));

`Math.max(0.001, dz)` guards a small POSITIVE denominator. It does not guard a NEGATIVE one: it
replaces it with +0.001. The four mount clips swing the leg over the saddle, which puts the knee
BELOW its own ankle — on mountUp f7 (fisher) the right knee is 0.236 m below the right ankle — so

    t = -0.516 / 0.001 = -516

and the boot top is extrapolated 516 shin-lengths away, ~300 m from the origin. The chain then
cancels back as the leg comes down, which is why the clip looks fine at both ends.

This is not a rounding problem and not an IK solve: the SKELETON is clean on every mount frame
(no joint of P is beyond 3 m). The excursion is created in facesOf(), downstream of the pose.

Rig 6 already knows about this failure. Its own comment above the code says a boarding foot
"tucks high enough that its knee sits BELOW its own ankle, and a fixed-height cut then
extrapolates backwards down the shin into a shaft several metres long", and routes board / water /
sleepP / reach to a distance-along-shin measure instead. The mount clips, added later, were never
joined to that guard — and there was no flag on P to join them with.

## The fix

Two tokens, at the cause, in the file's own idiom:

1. `characterIsoRig6.js` — pose() already computes the mount curve (`mnt`, line ~1376). It now
   publishes it next to its siblings: `P.mountP = mnt;`. `lift` carries the same defect, so
   `P.liftP = (anim==='lift') ? wk : null;` goes beside it (+2 lines).
2. `characterIsoRig6.js` and `characterIsoRig7.js` — the boot-shaft guard becomes
   `if(P.board || P.water || P.sleepP || P.reach || P.mountP || P.liftP){` (0 new lines).

`lift` is posed through the shared `workCurve`, so `P.work` is truthy for all six work clips;
guarding on it would have moved all six. The lift-specific flag keeps the other five bit-identical.

Rig 7 re-states rig 6's geometry, so the same cut is duplicated there. Patching rig 6 alone makes
the skinned export disagree with the pose by ~390 m — the boot bone stays at the old wild spot and
goldenDiff FAILS. Both files carry the change.

No new bone, no new material, no new clip, no re-timing. `P.mountP` is a new field on the pose
object, which is additive.

## The proof (check/check-mount-boot.cjs, 18 gates, all pass)

| what | result |
| --- | --- |
| every mount frame, 4 clips x 10 presets, max \|vertex\| | **1.994 m** worst (cutter mountCabDown f7), gate 3 m |
| lift, 10 presets, max \|vertex\| | **1.549 m** (fisher f6), was 15.00 m (boy f1) |
| the other 30 clips, facesOf drift vs as-delivered | **0.00e+0 m** — bit-identical, not merely inside 1e-4 |
| goldenDiff, the 5 fixed clips, 10 presets, every frame | **1.03e-15 m** (<= 1e-4) |
| goldenDiff, the other 30 clips, 10 presets, every frame | **1.04e-15 m** (<= 1e-4) |
| bones / anchors / cell / pivot / DIRS / GAIN / BIAS / LN / BAYER / CAST / BUILDS / ANIMS | unchanged |
| materials per preset | identical sets on all ten; this job moves no count |

Per-frame tables, before and after, and the exact list of (clip, frame) pairs that moved with
their magnitudes: **MOUNT-BOOT-TABLES.md**.

Every mount frame moves, not only the blown-up ones — the boot top is measured differently for the
whole clip. All 640 (clip, frame, preset) triples move, by 0.003 m to 389.10 m. The largest move on
a triple whose as-delivered reading was already under the 3 m gate is 3.48 m (mountCab f6, ginny,
which read 2.98 m before), because the old cut was wrong there too, just not catastrophically.

## Material counts, all ten presets (brief §3)

fisher    12
ginny     14
skipper   12
nan       13
deckboss  17
packer    17
cutter    14
hand      15
boy       13
girl      12

These are rig 6 BASE counts (this part ships no face layer). They match what the 2026-09-16 face
kit measured, including deckboss 17 and packer 17 at the base; the COMPOSED mesh the bake ships
stays at <= 16. This job changes neither number — it adds and repaints no material.

## `lift` — the same defect, fixed, as a named exception

§1.3 was measured on fisher, so it did not see that `lift` f1-f2 L put the boot top 15 m (boy) and
10 m (girl) from the origin — the identical degenerate cut, under the 3 m radar only because a
child's shin is short. It is fixed by the same guard.

All ten characters' lift frames move, because the boot top is measured differently for the whole
clip. The full (character, frame, metres) table is the named goldenDiff exception: checks.txt §6 and
MOUNT-BOOT-TABLES.md §3.6. The largest is boy f1 at 14.988 m; the adults move 0.007-1.394 m.

## The sprite re-bake

The flipbook's four mount sheets change on every frame (MOUNT-BOOT-TABLES.md §3.4), and the lift
sheet changes on every frame for every character (§3.6). The other 30 sheets are bit-identical and
need no re-bake. Per §4 the re-bake itself is the landing lane's call.

## Layout

    README.md                        this file
    checks.txt                       the checker's output, 18 gates
    MOUNT-BOOT-TABLES.md             §3.1-3.6: the measurement, before and after
    rigs/                            the chain to install: rig 6 and rig 7 patched, eye + head verbatim
    reference/rigs-as-delivered/     the accepted input bytes, untouched, for the diff gates
    check/load-kit.cjs               loads a chain the way the bake installs it
    check/check-mount-boot.cjs       the gates; `node check/check-mount-boot.cjs`
    SHA256SUMS.json                  sha256 of every file in this part

Run: `node check/check-mount-boot.cjs` from this folder. Exits 0 when all gates hold, 1 otherwise.
`CHECKS_OUT=checks.txt node check/check-mount-boot.cjs` rewrites checks.txt.
