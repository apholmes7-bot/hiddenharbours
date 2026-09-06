# Catch pass 2 — integration findings

Where the 240 baked pass-2 sheets reach the game, what they replaced, and the one thing in the
charter that could not be built because the art it names was never baked.

Lane: `feat/catch-pass-2-integration`, off `main` `6c901274`.

---

## What was integrated

| # | sheets | consumer | was |
|---|---|---|---|
| 1 | `CatchItem2_<kind>` × 7 | `CatchItemLibrary` | `CatchItem_<kind>` × 4 |
| 2 | `Fish_<sp>[_sm\|_lg]_<state>` × 210 | `RodFightPresenter` via a size ladder | the middle rung only, for every weight |
| 3 | `Crust2Held_<kind>`, `Shell2Hand_<kind>` | `CarriableCatch` | a **UI icon** in the fisher's hand |

Three kinds — **oyster, periwinkle, scallop** — had no art in the game at all before this: pass 1
never baked them, and the library's kind lists were hand-kept arrays that still said four. They are
now discovered from the baked sheets, so the next drop needs no edit here.

---

## 🔴 The clam hod: the fill was never baked

**The charter asks for the hod to fill. Nothing on disk can draw a full one.**

The pass-2 storage bake produced exactly two hod sheets, `Hod2_back.png` and `Hod2_front.png`
(8 facings × 1 frame each), plus `ClamHodAnchors.json`. Those draw an **empty** basket from two
sides. What a fill needs is absent in three separate ways:

| a fill needs | the tote has | the hod has |
|---|---|---|
| item **slot** points per facing | `CatchStorageAnchors.json` → `tote.byDir[d].slots` | — `ClamHodAnchors.json` publishes `opening`, `depthPx`, `carryGrip` and nothing else |
| an **opening mask** sprite per facing | `ToteMask.png` | — the only mask sheet in `Art/Fishing/Storage` is the tote's |
| or baked **filled states** | the bucket's model (`Bucket_pail_<band>_<group>_d<dir>`) | — no `Hod2_<band>` sheet exists |

`ClamHod` *names* the five bands (`FILLS = ['empty','few','half','full','brim']`), but its `render()`
takes no fill at all. The rig's own header states the recipe, and it is a **runtime composition**:

> `render(dir,{layer:'back'})` the far half … blit the CatchKit2 heap between them, clipped to
> `opening(dir)` and lowered by `(1 - fill) × depthPx()`

`CatchKit2.heap` is JavaScript that returns a composed canvas. It was never baked, and the intake
lane knew — its own plate README lists `hod-filling.png` among the plates that
**"render through the rigs, because what they show is composed at runtime and no sheet holds it."**

### Why this lane did not build it anyway

The only way to fill a hod from what shipped is to reimplement `CatchKit2.heap`'s layout in C# and
place clam item sprites inside the rim quad. That is a second copy of a layout the rig already owns,
drifting from the day it is written, and this repo has paid for that shape of guess before
(`Crustacean2.render` silently draws a **lobster** for an unknown kind; `FishIso2.hold` silently
falls back to **mackerel**). A guessed heap would look plausible and be wrong, which is the worst
available outcome.

### What is owed, precisely

**Bake the heap, one sheet per band, eight facings:** `Hod2_heap_<band>` for
`few | half | full | brim`, each cell composed as `CatchKit2.heap('clam', ClamHod.opening(dir),
ClamHod.depthPx(), fill, seed)` and already clipped to the opening. The runtime then draws
back → heap → front: three sprite swaps, no mask, no runtime composition, no second copy of the
layout. `CatchPass2StorageBaker` already installs `CatchKit2` and the canvas shim for
`BakeCatchItems`, so it is the natural home; it needs a `CatchStorageSheetSlicer` spec per new stem
and an arithmetic update to the closed-set guard.

Until then the hod can be drawn and carried but not filled, and this lane deliberately added no
half-hod: an always-empty basket beside the dig is worse than the abstract bucket `ClamDig` uses now.

---

## Two charter items that turned out not to need work

**Buckets stay pass 1 — correctly.** Pass 2 re-baked no container. Its README lists the containers it
fills as "the existing tote / stack-nest tray / wire pot / pail rigs + the hod", and there is no
`Bucket2_*` on disk. `Bucket_pail_*` is not superseded by anything.

**The carry anchor table did not need rebuilding.** The charter asks for
`CarryAnchorTableBuilder` to be re-run sourcing `Crust2Held_` / `Shell2Hand_`. That builder does not
read item sheets at all — it imports the **fisher's** hand geometry (where her wrists are, per facing
per frame) from `FisherFightAnchors.json`, which is the CHARACTER rig's sidecar and which catch pass 2
did not touch. The shipped `FisherCarryAnchors.asset` already matches it exactly: pass 6.4,
`torsoHalfMetres` 0.115, all seven prop rows. Re-running it would churn a committed asset for nothing.

The held **sprites** the charter was reaching for belong in the catch art table instead, beside the
in-container lay variants, and that is where they went — on their own rows, because they are a
different PIVOT (the grip, not ground contact) and cannot be another variant of the same row.

---

## Plates owed

To be rendered in the editor step, daylight, under this directory:

| plate | what to look at |
|---|---|
| `fish-on-the-line.png` | one species at all three rungs on the line, with the mouth anchor drawn — the line must leave the mouth at every rung, not just the middle one |
| `fish-on-the-deck.png` | the deck lay, seven species |
| `held-lobster.png` | `Crust2Held_lobster` in the hand at eight headings — was a UI icon |
| `hand-of-clams.png` | `Shell2Hand_clam`, the one-facing handful |
| `hod-empty.png` | back + front, eight headings — and the honest caption that no baked sheet fills it |
