# HH rig7 follow-up — part 4 of 4: the boy and girl nose at 32 px/m

Brief: BRIEF-2026-09-17-rig7-helm-oars-rock-mount-and-noses.md, Job 4.

## READ THIS FIRST — which file this part patches, and why it may not be the one you expected

§0 lists the input as `headIsoRig3.js` (the head, **with the 09-16 accepted face**). That file was
not in what I could see. Every copy of headIsoRig3.js available to me is byte-identical to the
09-16 job's AS-DELIVERED pin:

    headIsoRig3.js  sha256 88c2acf89df46c09e1c04d752b3ba2e703c1c37c710b0f7fbeefae1e27ceca9c
                    == the 'as-delivered' pin in the 09-16 kit's check-face-bake.cjs

i.e. the head BEFORE the 09-16 face was folded into it. In that file the nose is not geometry at
all — it is a single stamped pixel (`put(np[0], np[1]+q, 'skinD', nv.d)`). There is nothing there
to push forward by 3 mm.

The nose GEOMETRY the 09-16 job delivered — the four triangles, painted `skin`, with the -0.5
shade bias on the away-side plane — lives in **characterFaceStudy.js**, which §0 does not list.
So this part patches characterFaceStudy.js and proves itself against the 09-16 composition chain.

**If main really has folded that face into headIsoRig3.js, the same change applies there**: it is
one guarded line against the same five nose points, at whatever coordinates the fold gave them.
Send me the folded file and I will re-land it there and re-run these gates against it.

## What was wrong

The nose points are **head-local** and get multiplied by `scale = C.headScale * headSize`. A child
head is 0.873 of an adult's, so the nose shrinks with the head. At 32 px/m one pixel is 31.25 mm
and the child nose spans about 0.9 px, so no pixel centre lands inside the triangles and it
rasterises into the cheek — exactly what §1.4 reports.

Measured, as delivered, at 32 px/m under the game's own rule (nose pixels per heading):

    heading      0    45    90   270   315
    adults       4     1     2   2-4   0-1
    boy/girl     0     1     0     0     1

## The fix

A child nose is specified in **world metres**, not head units: +3 mm forward and +3 mm wider,
applied to any head whose `age` is `'child'` — which is exactly boy and girl, and stays correct
for any child preset added later. One guarded line in `createHead`:

    if(b.age==='child'){const k=.003/scale;for(const q of p)q[0]+=Math.sign(q[0])*k;p[4][1]+=k;}

`/scale` is what makes it a world-metre correction rather than another head-relative one.

### Why 3 mm and not the ~1 mm the brief estimates

Swept at 32 px/m. Forward alone resolves **nothing**; width alone resolves 0 deg and 45 deg but
never the profile; both are needed, and 1 mm of each leaves two of the five resolving headings
blank:

    forward  wider   boy / girl at [0, 45, 90, 270]
      0 mm    0 mm   [0,1,0,0]   as delivered
      3 mm    0 mm   [0,1,0,0]   forward alone does nothing
      0 mm    3 mm   [2,2,0,0]   the profile stays blank
      1 mm    3 mm   [2,2,0,0]
      2 mm    3 mm   [2,2,1,2]   passes, 1 px on the weakest heading
      3 mm    1 mm   [2,1,1,2]   passes, 1 px on two headings
      3 mm    3 mm   [2,2,2,4]   passes with 2 px on the weakest

3 mm on both is the first combination with 2 px of margin on its weakest heading, so a downstream
dither or ramp change cannot silently drop it back to nothing.

## The proof (check/check-child-nose.cjs, 11 gates, all pass)

| what | result |
| --- | --- |
| boy, five resolving headings | **2, 2, 2, 4, 2 px** (was 0, 1, 0, 0, 1) |
| girl, five resolving headings | **2, 2, 2, 4, 2 px** (was 0, 1, 0, 0, 1) |
| head vertices, the eight non-child presets | **0.00e+0 m** — bit-identical (gate 1e-4) |
| nose pixels, the eight non-child presets, all 8 headings | identical before and after |
| head face count, all ten presets | 312, unchanged |
| materials | identical set on all ten; the child nose is still `skin` |
| head drift on boy and girl | 3.0e-3 m, exactly the push |

The raster test is a with/without diff: rasterise the composed head at 32 px/m under the game's
rule, then again with the four nose faces removed. A nose that changes no pixel is not there. That
is the literal reading of "the nose's pixels differ from the cheek's".

### "all four DIRS"

Measured, a nose resolves at **five** headings, not four: 0, 45, 90, 270, 315 deg. 135/180/225
show nothing for any preset — they look at the back of the head. The gate uses all five, which is
stricter than the brief asks. Both children pass all five.

## FINDING — deckboss resolves no nose at any heading, before or after

    deckboss, all eight headings, as delivered:  0,0,0,0,0,0,0,0
    deckboss, all eight headings, after:         0,0,0,0,0,0,0,0

Every other adult lands 4 px at 0 deg. deckboss lands none, anywhere. It is an adult head at full
scale, so this is NOT the child sub-pixel problem and this push does not address it. §1.4 says the
09-16 face landed 25 of 25 probes for the adult presets; at 32 px/m under the game rule this one
paints no nose. It needs its own look — most likely its hat or beard geometry is winning the depth
test over the nose, but I have not diagnosed it and did not touch it.

## Layout

    README.md                        this file
    checks.txt                       the checker's output, 11 gates
    rigs/                            the composition chain to install; only characterFaceStudy.js differs
    reference/rigs-as-delivered/     the accepted 09-16 chain, untouched, for the diff gates
    check/load-kit.cjs               the 09-16 kit's loader, verbatim
    check/face-render.cjs            the 09-16 kit's rasteriser, verbatim
    check/cast-engine.js             the 09-16 kit's cast engine, verbatim
    check/check-child-nose.cjs       the gates; `node check/check-child-nose.cjs`
    SHA256SUMS.json                  sha256 of every file in this part

Run: `node check/check-child-nose.cjs` from this folder. Exits 0 when all gates hold.
`CHECKS_OUT=checks.txt node check/check-child-nose.cjs` rewrites checks.txt.
