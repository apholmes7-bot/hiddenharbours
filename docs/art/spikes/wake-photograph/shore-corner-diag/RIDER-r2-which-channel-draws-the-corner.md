# Rider r2 — which renderer actually draws the corner's water, and which channel reaches it

**Status: EVIDENCE ONLY. No code changed, no fix proposed, no cause named.** Shot from the
water-PR-F slot on a seat grant. The test code that took it was reverted before the PR opened; this
file and [`MEASURED-r2-shore-corner.txt`](MEASURED-r2-shore-corner.txt) are what survive of it.

This is the **retry** of the first shore-corner sweep in [this folder's README](README.md). That one
swept four material keys and reported almost nothing, and the retry was chartered on a specific
suspicion: *the sweep never reached the renderer that draws*. The suspicion was correct, and the
order below is the order the seat demanded — **identify the renderer first, prove it by hashed A/B,
and only then sweep.**

---

## LEG 1 — census: who carries the key, and who is switched off

```
RUNTIME 'Water (Displaced)' (shader 'HiddenHarbours/Water', instance 568105589188012454)
        on Sea/DisplacedWaterMesh/Chunk_47_34 — renderer enabled True, active True
        reads amp 1.0000  speed 0.1600  edge 0.6000  foamWidth 1.7400
ASSET   'Water' on Sea — NOT written.
FLAT RENDERER (WaterSurface) on Sea — enabled False, active True.
DisplacedWaterSurface on Sea — Displaced True.
TOTAL: 38352 renderer(s) in the loaded scenes, 1681 carrying _SwashAmplitude,
       of which 1 distinct runtime material and 1 asset material.
```

Three facts that together explain the first sweep's silence:

1. **The flat `Sea` renderer is DISABLED.** Under ADR 0023 the sea is drawn by chunk meshes when
   `DisplacedWaterSurface.Displaced` is true. Anything written to the flat renderer is written to a
   renderer that is not drawing.
2. **The chunks draw through a runtime material clone**, not the `Water` asset. Writing the asset
   material — which is what a shader-name search reaches first — writes something nothing renders.
3. ⚠️ **`DisplacedWaterSurface` copies the flat renderer's MaterialPropertyBlock onto every chunk
   EVERY FRAME.** This is the trap that makes a property-block write *look* like it worked: it
   survives its own read-back and is then overwritten before the next draw.

## LEG 2 — prove by A/B that the identified renderers actually put pixels in this frame

63 renderers overlap the frame; the 24 nearest the corner were each disabled, shot, hashed, and
re-enabled, every delta read against a **shutter repeat of 0.00000 over 0 px** (`r2-baseline` and
`r2-baseline-repeat` both hash `6ac5a48122dcd120`).

**24 of 24 tested renderers put pixels in the frame.** The two that matter:

| renderer | material | delta | px moved |
|---|---|---|---|
| `Sea/DisplacedWaterMesh/Chunk_26_18` | `Water (Displaced)` | 0.58824 | **1 385 271** |
| `Sea/DisplacedWaterMesh/Chunk_25_18` | `Water (Displaced)` | 0.57255 | **70 506** |

The rest are the land the corner sits in — grass chunks (0.30–0.43 over 19k–37k px), `ShoreRocks/Rock_bs`
(0.47843 / 2278 px), two `Glasswort` plants, `Shoreline/ShoreContact` (0.39608 / 18 837 px), gull
shadows, and `DayNightOverlay` across the whole frame (0.17647 / 5 859 200 px).

**So the corner's water is drawn by chunk renderers of the displaced mesh through runtime material
instance `568105589188012454` — not by the flat `Sea` renderer, and not through the `Water` asset.**

## LEG 3 — which channel reaches that renderer at a frozen frame

Four channels, same held frame, each read against the 0.00000 shutter repeat:

| channel | delta | px | note |
|---|---|---|---|
| **RUNTIME MATERIALS (frozen)** | 0.58824 | 37 004 | writes **every** material carrying the key, chunk clones included |
| RUNTIME MATERIALS (clock running, 0.60 s/arm) | 0.70980 | 1 626 821 | against a control-to-control floor of **0.75686 over 1 673 984 px** — i.e. below its own floor, uninformative |
| PROPERTY BLOCK (frozen) | 0.75686 | 1 597 463 | ⚠️ **wrote 4.000, read back 4.000 — the write survived to the shutter and still is not the channel**, because the block is re-copied each frame |
| GLOBAL (frozen) | 0.75686 | 1 563 697 | the global read **0.000** before the arm; these four keys are per-material CBUFFER members, never globals |

**Verdict: at a frozen frame the live channel is the RUNTIME MATERIALS, swept across every material
carrying the key.** The first attempt wrote only the one a shader-name search finds first, which is
why it measured nothing. Note what the property-block row costs to learn: a **successful read-back is
not proof a channel is live**.

Note also why the running-clock arm is useless here: consecutive shots land ~6.0 s apart against a
~6.25 s swash period, so three serial plates are three readings of one moment. The frozen frame plus
a dial is the only way to walk the pulse.

## LEG 4 — the split, at a held frame, walked by dial

At held `t = 3.3683 s`, four phases produced by dialling `_SwashSpeed` (1.000 / 1.250 / 1.500 / 1.750
cycles), each shot three ways — shipped, foam collapsed, wet edge pinned:

| phase | cycles | foam collapsed moves | wet edge pinned moves |
|---|---|---|---|
| 0 | 1.000 | **0.00000 over 0 px** | 0.69412 over **129 420 px** |
| 1 | 1.250 | **0.00000 over 0 px** | 0.70588 over **78 980 px** |
| 2 | 1.500 | **0.00000 over 0 px** | 0.70588 over **27 240 px** |
| 3 | 1.750 | **0.00000 over 0 px** | 0.70588 over **636 px** |

At every phase the shipped plate and the foam-collapsed plate are **bit-identical** (same hash), while
pinning the wet edge moves tens of thousands of pixels.

**⇒ The corner's pulsing edge is a WET-EDGE phenomenon, not a foam-channel one.**

**Phase check** (so this is not four readings of one still frame): the widest difference between any
two of the four shipped phases is **0.74902**, against a shutter repeat of **0.00000**. The dial
genuinely walked a moving swash.

## What this does and does not establish

- **It does establish** the renderer, the material instance, the live channel, and the channel split:
  the hairline the owner described lives on the wet edge, and collapsing foam does not touch it.
- **It does NOT name a cause** for the hairline itself, and proposes no fix. The shore-corner hairline
  fix is not this lane's — it is queued behind, per the series law on the water shader.
- **It does NOT touch the look.** Every dial written was restored.

## Frame

NineMileCreek, hour 11.0. Camera (34.70, 11.50), half-height 4.500 m, plate 3662×1600,
**0.00563 m per pixel**. Clock frozen at `Time.timeSinceLevelLoad = 0.5834 s` (`timeScale = 0`) for
legs 1–3; held `t = 3.3683 s` for leg 4. Taken on the water-PR-F branch at `d2fbfcc9` with the rider
test code present in the working tree and uncommitted; that code was stripped before this PR opened
and the stripped fixture re-run green (4/4). Every number is read off plate bytes.
