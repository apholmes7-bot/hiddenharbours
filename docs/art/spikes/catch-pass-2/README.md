# Catch pass 2 — review plates

Five plates for the owner, from the bake on `feat/catch-pass-2-intake`. Three read the **committed
sheets**, so what you judge is what actually shipped; two render through the rigs, because what they
show is composed at runtime and no sheet holds it.

| plate | source | what to look at |
|---|---|---|
| [`size-ladder.png`](size-ladder.png) | committed sheets | 7 species × 3 rungs, the swim cycle |
| [`crustacean-motions.png`](crustacean-motions.png) | committed sheets | the lobster's flip, the crab's sidle and burrow |
| [`clam-flat-spurt.png`](clam-flat-spurt.png) | rig-composed | the dig's TELL, twice: the curve, then the flat as played |
| [`hod-filling.png`](hod-filling.png) | rig-composed | the hod filling, shells inside the wire |
| [`pass1-vs-pass2-reloft.png`](pass1-vs-pass2-reloft.png) | rig-rendered comparison | **needs a ruling — see below** |

---

## ⚠️ `pass1-vs-pass2-reloft.png` — the one that needs your decision

The kit README says pass 2 *"moves no pixel"* on the four species we already ship. **It does.**
Measured across cod, haddock, pollock and mackerel at scale 1: **33,547 pixels differ, in every one
of 280 cells.**

It is not the keyline — pass 1 ignores `{outline:true}` entirely, so plain and outlined renders are
zero pixels apart. It is not the strict world scale either — `SPECIES.len` is unchanged for all four.
The bodies are simply **drawn differently**, and much sparser: cod 132 → 96 opaque pixels, haddock
100 → 63, pollock 112 → 71, mackerel 66 → 38. `swim` also grew 4f → 6f and `dart` 2f → 3f.

So replacing the shipped sheets is **a visible art change, not a no-op**, and that is a call for you
rather than for this lane. The plate puts pass 1, pass 2 and a difference mask side by side on the
`thrash` frame — a state both passes have at the same frame count, so the comparison is like for
like rather than an artefact of `swim` growing.

The handoff's acceptance line asked for a *parity* plate showing no pixel moved. That plate cannot
exist; this is the honest version of it.

---

## `size-ladder.png` — the ladder you ruled on

The runtime cannot scale a fish. `RodFightPresenter` swaps frames and nothing else, and pixel art on
the 32 px/m grid cannot be resized at draw time without breaking the grid — so a heavier catch has
to be **a different sheet**.

Three candidate bands disagreed, so you ruled (2026-09-05) that the ladder spans **what the game
actually rolls**:

| source | cod's band |
|---|---|
| the rig's `SPECIES.range` | 0.6 – 1.5 |
| `CatchKit2.fillItems` rolls | 0.85 – 1.15 only |
| **`CatchResolver`, from the shipped def** | **2 – 12 kg** = scale 0.873 – 1.586 |

Each row shows the weight and body length that rung actually draws, read from the bake's own
`FishIsoAnchors.json`. Rungs are spaced evenly in **rendered length**, not mass, because length is
what the eye reads and mass goes as its cube.

Four species step **outside** the art director's declared range at the top — that is the ruling
working as intended, and every rung records its `bandSource` so no later reader has to guess why a
cod is baked at 1.586. **bass, flounder and herring have no `FishSpeciesDef` yet**, so their ladders
fall back to `SPECIES.range`; the plate marks those rows.

The middle rung is unsuffixed, so `Fish_cod_swim.png` is still the file every existing consumer
loads and the ladder is purely additive on disk.

---

## `crustacean-motions.png`

Only the poses each animal genuinely owns. A lobster cannot sidle or burrow and a crab cannot
tail-flip; the rig quietly renders those as `walk`, so the baker **measures** which poses differ from
walk and bakes only those. Nothing here is a listed table that can drift.

Watch the crab's sidle: it faces **across** its travel, which is the whole point of a crab.

---

## `clam-flat-spurt.png`

Two blocks, and the difference between them matters.

**THE CURVE** drives every hole from its own window opening, so `rise = sin(π·u)` is visible at a
glance — nothing, rising, full at the midpoint, gone by 420 ms. It is a diagram, **not** the in-game
look.

**AS PLAYED** is one wall clock with every hole on its own independently rolled phase and period:
1–3 of 26 holes up at any moment, scattered and irregular. That is the ~9% duty cycle the rig's
2.6–7.8 s period implies, and it is why the phases are rolled independently rather than shared out
evenly — an evenly spread flat reads as a sprinkler system.

This replaced a greybox tell that flipped a boolean every 10–20 s for 1.5 s: roughly **three times
too slow and four times too long**, and a bare on/off, so the art could only ever be a flip-book
rather than a jet that rises and falls.

---

## `hod-filling.png`

The basket is hollow: back layer, then the clam heap clipped to `opening(dir)`, then the front over
it. The shells sit **inside** the wire, not as a lid on top of it. Five fills × three facings.

---

## Regenerating

The plates are built by `plates.py` with `plate_flat.js` / `plate_hod.js` / `plate_reloft.js`, which
run the rigs unmodified in the repo's own ClearScript V8 (no Unity needed). Sheet-derived plates just
read the committed PNGs, so re-running after a re-bake picks the new art up automatically.
