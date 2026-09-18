# Job 3 — the mount* boot excursion: measurement

All numbers are max |vertex| from the rig origin, in metres, frame 0 first, produced by
check/check-mount-boot.cjs. "as-delivered" is reference/rigs-as-delivered/, "fixed" is rigs/.

## 3.1 fisher, as delivered — reproduces the brief's §1.3 table

mountUp      (16f)  1.58 1.58 1.58 1.60 1.63 147.34 233.68 292.56 275.82 197.46 1.81 1.88 1.92 1.93 1.93 1.93
mountDown    (14f)  1.93 1.93 1.93 1.91 1.84 23.47 271.44 290.72 213.96 111.37 1.60 1.59 1.58 1.58
mountCab     (18f)  1.58 1.58 1.58 1.58 1.59 1.59 2.66 232.67 355.91 305.86 257.51 5.58 1.80 1.87 1.92 1.93 1.93 1.93
mountCabDown (16f)  1.93 1.93 1.93 1.90 1.84 3.08 242.39 305.01 352.10 214.86 1.61 1.59 1.58 1.58 1.58 1.58

## 3.2 fisher, fixed — every frame under 3 m

mountUp      (16f)  1.58 1.58 1.58 1.60 1.63 1.67 1.71 1.74 1.75 1.76 1.81 1.88 1.92 1.93 1.93 1.93
mountDown    (14f)  1.93 1.93 1.93 1.91 1.84 1.76 1.75 1.73 1.70 1.65 1.60 1.59 1.58 1.58
mountCab     (18f)  1.58 1.58 1.58 1.58 1.59 1.59 1.62 1.65 1.67 1.70 1.73 1.75 1.80 1.87 1.92 1.93 1.93 1.93
mountCabDown (16f)  1.93 1.93 1.93 1.90 1.84 1.75 1.73 1.70 1.67 1.64 1.61 1.59 1.58 1.58 1.58 1.58

## 3.3 the gate across all ten presets, fixed

clip           worst max |vertex|   at
mountUp        1.946 m              ginny f13
mountDown      1.946 m              ginny f0
mountCab       1.946 m              ginny f15
mountCabDown   1.994 m              cutter f7

The largest is 1.994 m, against a 3 m gate. Every frame of every mount clip on every
preset passes.

## 3.4 exactly which (clip, frame) pairs moved, and by how much

Max vertex displacement between as-delivered and fixed, over the ten presets, in metres.
All 640 (clip, frame, preset) triples move, by 0.003 m to 389.10 m: the boot top is measured
differently for the whole clip, not only on the blown-up frames. The largest move on a triple that
already read under the 3 m gate is 3.48 m (mountCab f6, ginny, which read 2.98 m before) — the old
cut was wrong there too, just not catastrophically.

mountUp      (16f)  0.01 0.01 0.04 0.29 59.76 166.09 272.94 344.54 312.99 230.51 1.61 1.37 1.41 0.98 0.69 0.65
mountDown    (14f)  0.65 0.71 1.13 1.46 1.15 208.41 306.85 342.78 244.80 129.00 1.16 0.12 0.01 0.01
mountCab     (18f)  0.01 0.01 0.01 0.12 0.37 1.19 3.76 256.71 389.10 359.81 297.56 218.13 1.44 0.95 1.02 0.83 0.67 0.65
mountCabDown (16f)  0.65 0.68 0.90 1.02 0.89 24.92 282.25 357.93 385.02 237.90 2.77 0.83 0.16 0.08 0.01 0.01

## 3.5 the other 31 clips do not move at all

facesOf vertex drift, as-delivered vs fixed, 31 clips x 10 presets x every frame: 0.00e+0 m.
Not "inside 1e-4" — bit-identical. The guard the fix adds is false on every one of those frames,
so the old code path runs unchanged.

## 3.6 lift — the same defect, fixed by the same guard, as a NAMED EXCEPTION

lift carries the identical degenerate cut: on boy and girl the left knee sits 3-11 mm BELOW its own
ankle (dz = -0.0035 boy, -0.0046 girl), the clamp substitutes +0.001, and the shaft extrapolates
~95x the shin. It stayed under the brief's 3 m radar only because a child's shin is short, which is
why §1.3 — measured on fisher — did not see it.

    boy   lift  before  1.16 15.00 14.97 1.16 1.16 1.16 1.16 1.16
    boy   lift  after   1.16  1.16  1.16 1.16 1.16 1.16 1.16 1.16
    girl  lift  before  1.07 10.09 10.06 1.07 1.07 1.07 1.07 1.07
    girl  lift  after   1.07  1.07  1.07 1.07 1.07 1.07 1.07 1.07

lift is posed through the shared workCurve, so `P.work` is truthy for all six work clips (hauler,
bench, chop, lift, place, toss). Guarding on it would have moved all six. pose() therefore
publishes a lift-specific `P.liftP`, and the other five work clips stay bit-identical.

Every character's lift frames move, not only boy's and girl's, because the boot top is measured
differently for the whole clip. This is the named goldenDiff exception, in metres:

    character        f0      f1      f2      f3      f4      f5      f6      f7
      fisher      0.085   0.586   0.732   0.186   0.106   0.029   0.012   0.012
      ginny       0.082   0.599   0.761   0.181   0.102   0.027   0.012   0.012
      skipper     0.124   1.046   1.394   0.282   0.156   0.040   0.018   0.018
      nan         0.043   0.383   0.525   0.098   0.054   0.014   0.007   0.007
      deckboss    0.060   0.411   0.513   0.131   0.074   0.020   0.008   0.008
      packer      0.093   0.679   0.862   0.205   0.116   0.031   0.014   0.014
      cutter      0.086   0.720   0.957   0.195   0.108   0.027   0.015   0.015
      hand        0.053   0.412   0.534   0.119   0.066   0.017   0.008   0.008
      boy         0.077  14.988  14.961   0.225   0.102   0.018   0.015   0.015
      girl        0.056  10.103  10.081   0.168   0.075   0.015   0.013   0.013

After the fix, lift's worst frame across all ten characters is 1.549 m (fisher f6).
