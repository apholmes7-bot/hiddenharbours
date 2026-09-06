# The foam trail's root — water fidelity PR 11a

The owner, 2026-09-04, the cape under way and turning at St Peters:

> *"foam fades behind boat but doesnt disperse in width, the foam seem to come from the cetnre of a boat
> when turning and not accuratly from the stern."*

This folder is the evidence for the **second half** of that sentence — where the trail is laid. The first
half (it does not widen) is **not fixed here**: see "What this does NOT show" below.

## `SHEET-foam-root.png` — four quadrants

|  | **left column** — as shipped | **right column** — this PR |
|---|---|---|
| **top row** | 90° turn, trail laid at the hull's **origin** | 90° turn, trail laid at her **transom** |
| **bottom row** | 8 kn straight, laid at the **origin** | 8 kn straight, laid at the **transom** |

The **top row is the one the complaint is about.** On the left the trail springs from where her middle
was; on the right it follows from where she actually sheds it. The bottom row is the control: a straight
run displaces the whole trail astern uniformly, which is a bigger centroid move but not the defect he saw.

⚠️ **The hull is NOT in frame.** This is the advected foam buffer's own picture — the coverage channel,
painted as greyscale — read straight back off the GPU. There is no boat, no sea, no hull art: only the
mark left on the water. So the plate shows *where the trail is relative to the track*, not how it sits
against a drawn hull. A hull-in-frame pair is a PlayMode job (the injector runs in `LateUpdate` on a live
boat) and is **deferred to PR 11b**, where the dispersal will need the same fixture; building it twice for
one column would be the more expensive way round.

## The numbers (`FOAM-ROOT.txt`)

```
figure | centroid shift, origin-laid -> transom-laid (m)
90 deg turn    |  1.19
straight 8 kn  |  2.11
```

The whole drawn trail's centroid, weighted by coverage, moves that far between arms. It is guarded with a
floor, so two views of one sea would fail the test rather than pass it quietly.

The headline number is not on the plate but in the tests: the trail was being laid **6.45 m** from the
cape's transom — over half her length — and 2.25 m / 3.50 m / 55 m on the dory, console skiff and tanker.

## How it was shot

`Assets/Tests/EditMode/FoamTrailRootPlateTests.cs`, through the **shipped** advect pass
(`Hidden/HiddenHarbours/FoamBufferAdvect`), one blit per step over ~8.7 s of track at 30 Hz, exactly as
`IsoFacetHullFeature` drives it — so the buffer photographed here is the buffer the sea draws from.

**The clocks are stopped by construction, not by tuning:** the fixture owns the whole time axis (it passes
its own `dt` and its own decay factors per step and never reads `_Time`), so two runs of one arm are
identical and every difference between the arms is the change under test.

Regenerate with the fixture; the plate lands in `artifacts/foam-trail/` (gitignored) and is copied here.

## What this does NOT show

The trail's **width**. It is the hull's beam in every quadrant, at every age, in both columns — because
that half of the defect is untouched by 11a. It is register row **26(b)** and PR **11b**, and it is not a
knob somebody declined to turn: two measurements say so.

- A **compose-only reveal has nothing to reveal.** The drawn band already sits at **78 % of the stamp
  radius** (1.86 m of 2.4 m on the cape), where the stamp's falloff is 0.425 and heading to zero. Dropping
  the draw threshold with age moved the band **1.86 → 1.91 m**. You cannot reveal width that was never
  stamped.
- A **naive stamped skirt widens but multiplies the foam** — drawn integral **9356 → 13052** — because
  coverage *accumulates over passes*, so "faint enough to stay hidden while young" is not one constant but
  a function of speed, deposit rate and frame step.

11b's term instead: normalise the injected profile so widening the footprint scales the amount by the
ratio of the profiles' integrals — a skirt then changes the *distribution* of the foam laid, never its
total, and conservation holds at **injection** rather than being patched at compose. Guarded per age bin
(integral at age *t* ≤ integral at birth × the decay term), never on a whole-trail total: the dispersing
arm legitimately draws old foam the shipped arm had dropped below threshold, so a total would always
favour the shipped one.

## A note on the file, for whoever regenerates it

`*.png` is LFS by `.gitattributes`, so this sheet is stored as a pointer. There are **no `.meta` files**
here — `docs/` sits outside `Assets/`, so Unity never generates them, which is the convention every other
folder under `docs/art/spikes/` already follows.
