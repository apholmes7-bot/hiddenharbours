# HH rig7 follow-up — part 1 of 4: helm and oars as named clips

Brief: BRIEF-2026-09-17-rig7-helm-oars-rock-mount-and-noses.md, Job 1.
Input: the accepted bytes of the rig chain (main bd5d28b2), verbatim in reference/rigs-as-delivered/.

## What was missing

The posing was already there. `carry: 'helm'` and `carry: 'oars'` have been rig 6 modifiers since
characterIsoRig.js:38-39 — braced over the wheel with the feet apart, leaned into the pull at the
oars — and `CARRIES` (characterIsoRig6.js:375) already declares which animations each may ride:
`helm.anims = ['idle','walk']`, `oars.anims = ['idle','walk']`.

What was missing was a NAME. `clips(build, opts)` maps over ANIMS with a single `opts`, so it can
emit `idle` or `idle+helm`, never both, and an extractor had nothing to enumerate. Rig 7's own
golden harness already walked every carry (`goldenRows()`, :512) — so the stances were tested but
never exported. The mesh player stood idle at the wheel because nobody asked for the stance, not
because the stance did not exist.

## What this part adds

`CARRY_CLIPS` in characterIsoRig7.js: a table of (anim, carry) pairs with a stance name, plus
`carryClip(name, build)`, `carryClips(build)` and `carryClipNames()`. `exportBuild()` gains a
`carryClips` array beside the untouched `clips`.

    clip        rides  carry  frames    ms  loop  pin
    helm_idle   idle   helm        6   170  true  carry_mid + carry_L + carry_R
    helm_walk   walk   helm        8   110  true  carry_mid + carry_L + carry_R
    oars_idle   idle   oars        6   170  true  carry_L + carry_R
    oars_row    walk   oars        8   110  true  carry_L + carry_R

Every one runs `clip() -> solveAt() -> C6.pose(anim, u, b, power, carry, o)` — the same path as all
35 existing clips. No new posing, `rock` null throughout.

### Why a table and not an ANIMS extension

ANIMS is append-only, so adding `helm_idle` to it is legal on paper and wrong in fact: every ANIMS
key is an animation `pose()` knows how to build, and `pose()` has no `helm_idle`. A stance is
(existing animation) + (carry modifier). Putting these in ANIMS would demand new posing, which Job
1 forbids. The table names pairs instead, and ANIMS stays byte-identical at 35.

### Frames and ms

Taken from the ridden animation, unchanged: idle 6f/170 ms, walk 8f/110 ms. Those are exactly what
HelmStance and OarsStance bake in FisherIso.asset at main, so nothing is re-timed and no sheet
needs a new cadence. `BalanceStance` (idle 8f/150 ms, walk 8f/100 ms) is untouched — `balance` is
already an ANIMS animation and Job 1 says leave it.

### oars_row rides walk

That is what rig 6 sanctions, and it is what makes the stroke: under a non-idle animation the oars
arm target runs at 4pi (characterIsoRig.js:326) — the pull cycle — instead of idle's gentle 2pi.

### The boat owns the wheel and the oars

No wheel or oar geometry is exported here. Each clip names the bones the boat's own rig pins its
gear to, in `pin` — the tool-root chain of 09-09 §2.1: `carry_L` hangs off `hand_L`, `carry_R` off
`hand_R`, `carry_mid` off the torso. helm pins all three (the wheel has a centre); oars pin the two
hands. Only the hands' chain travels.

## The proof (check/check-carry-clips.cjs, 15 gates, all pass)

| what | result |
| --- | --- |
| goldenDiff vs `facesOf(pose(anim, u, build, {carry}))` | **5.56e-16 m** over 280 frames (4 clips x 10 presets), gate 1e-4 |
| frames / ms vs the sprite's sheets | all four match; nothing re-timed |
| bones | 45, unchanged — a carry adds no bone |
| ANIMS | byte-identical, 35 animations |
| the existing 35 clips | byte-identical on all ten presets |
| cell, pivot, DIRS, GAIN, BIAS, LN, BAYER, CAST, BUILDS | byte-identical |

## LANDING ORDER — this part and part 3 both touch characterIsoRig7.js

Different functions, so they merge cleanly, but do not let one zip overwrite the other's file:

* part 1 (this one) adds the CARRY_CLIPS block after `clips()`, three names to the API object, and
  one key to `exportBuild()`.
* part 3 changes one token in the boot-shaft guard inside `solve()`.

Land both edits, or land part 3 first and re-apply this part's block on top.

## Layout

    README.md                        this file
    checks.txt                       the checker's output, 15 gates
    rigs/                            the chain to install; only characterIsoRig7.js differs
    reference/rigs-as-delivered/     the accepted input bytes, untouched, for the diff gates
    check/load-kit.cjs               loads a chain the way the bake installs it
    check/check-carry-clips.cjs      the gates; `node check/check-carry-clips.cjs`
    SHA256SUMS.json                  sha256 of every file in this part
