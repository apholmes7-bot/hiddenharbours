# Rider r1 — does family A (the advected foam sheet) reach the picture at all?

**Status: EVIDENCE ONLY. No code changed, no fix proposed, no look ruled.** This rider was shot from
the water-PR-F slot on a seat grant, alongside the wake-lift acceptance plates. The test code that
took it was reverted before the PR opened; this file and
[`MEASURED-r1-family-a.txt`](MEASURED-r1-family-a.txt) are what survive of it, which is the point —
the reading lands, the instrument does not.

It answers **candidate 1** of [this folder's README](README.md#for-the-owner--the-candidates-in-the-order-they-should-be-settled):

> *Confirm family A reaches the picture at all, on an unmodified camera with no render texture
> attached. Per-camera foam state keys on `(camera entity id, resolution)`, and both configurations
> that reported A as nothing had a render texture attached — a shared confound never ruled out.*

---

## The answer

🔴 **On this box, in NineMileCreek at hour 11.0, sweeping `_WakeFoamStrength` from its shipped
0.850 to 0.000 changed the plate by NOTHING on both legs — including the leg with no render texture
attached.** The confound the README named is ruled out: the render texture was never the reason.

| leg | camera | plate | on | off | on again | sweep (on vs off) | repeat floor (on vs on-again) |
|---|---|---|---|---|---|---|---|
| **RT** | the fixture's own camera, RenderTexture attached | 3662×1600 | `a390164d0136273b` | `a390164d0136273b` | `a390164d0136273b` | **0.00000 over 0 px** | 0.00000 over 0 px |
| **U** | **UNMODIFIED camera, back buffer, no RenderTexture** | 1307×571 | `32dd656e942a663f` | `1b92c18163451991` | `1b92c18163451991` | **0.32157 over 299 px** | **0.32157 over 299 px** |

**The U leg's sweep is not a signal — it is EQUAL to the repeat floor, pixel count included.** The
first shot of that leg differs from the second and third; the second and third are identical to each
other. That is a first-shot settle on the back buffer, and it is the same 299 pixels either way. A
sweep that exactly equals the floor it is read against measures the floor.

The RT leg is stronger still: three identical hashes across on / off / on-again. Per this lane's
standing law, **N identical hashes across a swept property means the property never reached the
drawing renderer** — so before believing anything else, the write was checked at the other end.

## The write DID land — the read-backs are the proof

Each arm recorded what it wrote and what the material read back immediately after, plus the
registry's own state:

```
GATE: 1 runtime material(s) [568105589188428916] carry _WakeFoamStrength; shipped value 0.850
REGISTRY before any write: LookStrength 0.850, ShouldRun True, 31 injector(s) alive
arm RT-A-on:        wrote 0.850, read back 0.850, registry LookStrength 0.850, ShouldRun True
arm RT-A-off:       wrote 0.000, read back 0.000, registry LookStrength 0.850, ShouldRun True
arm RT-A-on-again:  wrote 0.850, read back 0.850, registry LookStrength 0.850, ShouldRun True
```

So: the property exists, exactly one runtime material carries it, the value written is the value read
back, the registry says the look is on and 31 injectors are alive — and the picture does not move.

## What this does and does not establish

- **It does establish** that toggling `_WakeFoamStrength` between its shipped value and zero is
  invisible in NineMileCreek at hour 11.0 through both an RT camera and the plain back buffer. The
  README's leading suspect — the render texture — is eliminated.
- **It does NOT name the cause.** Two explanations remain open and this rider cannot separate them:
  either the advected sheet draws nothing for anybody in this scene, or the material carrying
  `_WakeFoamStrength` is not the material the *drawing* renderer uses. The second is live: the
  displaced water surface draws through **chunk clones**, and rider r2 measured in the same session
  that `DisplacedWaterSurface` re-copies the flat renderer's property block onto every chunk **every
  frame** (see [`shore-corner-diag/RIDER-r2-which-channel-draws-the-corner.md`](shore-corner-diag/RIDER-r2-which-channel-draws-the-corner.md)).
  A read-back proves a write survived; it does not prove the material was rendered.
- **It does NOT touch the look.** Nothing in this rider or the PR that carries it changes a shipped
  pixel of family A.

## The honest next step

The cheap one is to repeat this sweep through the channel r2 identified as live at a frozen frame —
**every runtime material carrying the key, chunk clones included** — rather than the one a shader-name
search finds first. That is a different rider, not a fix, and it is not this lane's to spend.

## Frame

NineMileCreek, hour 11.0, open water (260.0, 75.0) with 6.0 m under her. Taken on the water-PR-F
branch at `d2fbfcc9` with the rider test code present in the working tree and uncommitted; that code
was stripped before this PR opened and the stripped fixture re-run green (4/4). Numbers here are read
off the plate bytes, not computed.
