# The wake fading — water fidelity PR 11c, register row 28

The owner, 2026-09-04 and again 2026-09-06:

> *"foam fades behind boat … and fade away"*
> *"the foam always stays to the original foam path, it doesnt widen over time **and fade away**."*

PR 11a rooted that trail at the transom and PR 11b made it widen. This folder is the evidence for the
last three words. **It was never a look problem — it was the render target.**

The advect pass computes at full float and then writes into the foam buffer, which shipped as
`RenderTextureFormat.RG16`: 8 bits a channel. The decay is a MULTIPLY, so the change it makes in one
frame is proportional to the value stored, and at the PC-first 60 fps baseline the largest change the
coverage channel can make — the one at full white — is **0.491 of a code**. Round-to-nearest sends it
straight back. **The coverage channel did not decay at any stored value.**

## `SHEET-foam-fade.png` — three rows, two columns

The cape at 8 kn on a straight run through the shipped 96 m window, at 60 fps, driven through the
SHIPPED advect pass one blit per frame. **Left column = the shipped 8-bit target. Right column = the
fixed 16-bit one** (`RG32` on this machine). The only difference between the two columns is the format
the ping-pong pair is stored in.

| row | what it is | what to look at |
|---|---|---|
| **1** | the run — 20 s of track | The left bar is **the same brightness for its whole length**: a trail with no gradient in it, because nothing was decaying. The right bar goes white at the hull, through grey, to nothing at the tail. |
| **2** | the tail — the same buffer 30 s after she has gone, nothing injected, nothing but decay | The left figure is **identical to row 1**. Eighteen hundred frames of pure decay changed nothing at all. The right figure is empty water. |
| **3** | the freshness clock of row 1, painted through #724's colour walk | The left bar is **flat white** — the clock stalls at stored 0.680, which is ramp 0.305, so the shallow-blue anchor at 0.5 was never reachable. The right bar walks white → shallow blue → mid blue and finishes. |

## `FOAM-FADE.txt` — the numbers the fixture wrote

```
figure            | arm     | drawn texels | peak coverage | drawn tail astern
1 the run 20 s    | shipped |        23004 |        1.0000 |          88.0 m
1 the run 20 s    | fixed   |        19070 |        1.0000 |          81.9 m
2 +30 s tail      | shipped |        23004 |        1.0000 |          88.0 m
2 +30 s tail      | fixed   |            0 |        0.0312 |           0.0 m
```

Row 2 is the acceptance, and it is worth reading twice. The shipped arm's three numbers are **byte for
byte the ones it had before the 30 s of decay**. The fixed arm comes to rest at **0.0312** — which is
2⁻⁵ to four decimals, exactly five coverage half-lives, so the GPU and the arithmetic agree.

## ⚠️ Why this fixture exists at all, and what it says about the last one

`FoamTrailRootPlateTests` — the harness this borrows its shape from — ping-pongs the advect pass through
`ARGBFloat`. That is a perfectly good rig for asking **where a trail is laid**, which is what 11a and 11b
needed, and it is **structurally unable to show this defect**: a float target has no 8-bit rounding in
it. The defect lives in the STORE, so the store had to be the thing under test and the arms here are two
real render-target formats.

The same is true one level up. `FoamBufferTests.DecayFactor_HalvesAtTheHalfLife_AndComposes` and
`HalfLife_SetsAVisibleLifetime_NotAnInstantPop` were green throughout: they measure the **factor**, in
float, and the factor was always right. Nothing asked what the buffer did with it.

## What this does NOT show

- **A hull in frame.** These are the buffer's own channels, read back — no boat, no sea, no compose.
  The hull-in-frame pair PR 11b deferred is still owed and is a PlayMode fixture.
- **The three-publisher question** (the owner's 2026-09-06 *"the foam seemed off-centred, with three
  different sections leaving the boat"*). That is register row 29, measured in #756's body and fenced
  from this PR — it needs its own plate with all three foam families drawn and their roots marked.
- **A look verdict.** The owner ruled the half-life dial stays at 6 s. At that value a wake now fades to
  nothing inside the window up to about 5 kn and still reaches the window edge at 8 kn; the half-life,
  not the format, is what decides that, and it is his.

## Re-running it

```
"C:/Program Files/Unity/Hub/Editor/6000.5.0f1/Editor/Unity.exe" -runTests -batchmode -projectPath <worktree> -testPlatform EditMode -testFilter HiddenHarbours.Tests.EditMode.FoamFadePlateTests -testResults <abs>/fade.xml -logFile <abs>/fade.log
```

No `-nographics`: the fixture self-skips on the Null device and says NOT VERIFIED rather than passing
quietly. It owns its whole time axis (its own dt and decay per step, never `_Time`), so two runs of one
arm are bit-identical and every difference between the columns is the format.
