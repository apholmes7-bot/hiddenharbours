# The clam hod FILLS — the heap bands, measured and baked

What was missing, what is baked to fix it, and what the four new sheets actually look like.

Lane: `feat/clam-hod-fill-bake`, off `main` `bc885842`. Charter:
`HANDOFF-2026-09-06-clam-hod-fill-bake.md`, from the finding the catch-pass-2 integration lane wrote
up in `docs/art/spikes/catch-pass-2-integration/README.md`.

---

## What was missing

Catch pass 2 baked exactly two hod sheets — `Hod2_back.png` and `Hod2_front.png`, the far and near
halves of the wire — plus `ClamHodAnchors.json`. Those draw an **empty** basket. The rig's own recipe
for a filling hod is a **runtime composition** over `CatchKit2.heap`, which is JavaScript; there is no
script host in the player, so nothing on disk could draw a full hod and `ClamDig` filled a number
nobody could see.

Two ways out, and only one of them is honest:

| | what it costs |
|---|---|
| reimplement the heap's layout in C# and place clam sprites in the rim quad | a second copy of a layout the rig owns, drifting from the day it is written — the family of guess that already makes `Crustacean2.render` draw a lobster for an unknown kind and `FishIso2.hold` weigh an unknown fish as a mackerel, both at zero pixels of difference |
| **bake the composition** | four sheets, and the runtime swaps three sprites and owns no layout at all |

The owner ruled on 2026-09-06; this is the second.

---

## What is baked

`Hod2_heap_<band>.png` for **few · half · full · brim**, 8 facing rows × 1 frame, on the hod's own
40×40 cell and (20,30) pivot — so back, heap and front draw on ONE transform and the existing `Hod2_`
slicer spec covers all three stems without an edit.

**The rig is not edited to do this, and does not need to be.** `CatchKit2.heap` already hands back the
composed surface *and its offset from the container pivot* (`x`, `y`), so the only thing the host adds
is placing that surface in the cell at `pivot + (x, y)`. That is the same division of labour as the
canvas shim (ADR 0021 §5: what the engine needs and the rig does not provide is the HOST's job, never a
patch to the art director's file). The layout, the mask, the dome shading, the lowering and the crown
all stay in the rig — `docs/art/rigs/` is untouched, and every sha256 in the kit's `SHA256SUMS.txt`
still stands.

The bands are not listed anywhere in C#: they are `ClamHod.FILLS` minus every band `CatchKit2.FRAC`
gives a zero fraction. `empty` heaps nothing and gets no sheet — an empty hod is back+front with
nothing between them.

---

## What was measured, before any of it was written

In the standalone V8 harness (`Assets/_Project/Plugins/Editor/JsEngine` referenced by a ~200-line
console app), running the rigs unmodified through the bake's own install chain.

**The control, which is not optional.** The harness reproduces both committed hod sheets
**byte-for-byte** — 40×320 RGBA, 0 pixels different, on both `Hod2_back` and `Hod2_front` — with the
whole `catchKit2` chain loaded first. That is the guard the charter asked for, and it also settles the
one risk in changing the bake's install order: nothing in the chain reaches the hod's own render.
`CatchPass2KitTests.LoadingTheHeapsChain_DoesNotMoveOneHodPixel` keeps it true.

**Every heap fits its cell.** All 32 (band × facing) heaps land inside the 40×40 cell at the pivot —
widest is 15×13 at the diagonals, landing at cell (13,18)…(27,30). The bake ASSERTS this rather than
cropping.

**The ladder is a LOWERING, not a redraw.** This is the thing that would otherwise read as a broken
bake:

| band | fraction | lowered by | opaque px (d0) | top row (d0) |
|---|---|---|---|---|
| few | 0.25 | 3 px | 72 | 25 |
| half | 0.55 | 1 px | 72 | 23 |
| full | 0.85 | 0 px | 72 | 22 |
| brim | 1.00 | 0 px + a 2 px crown | 88 | 20 |

`few → full` is **one plate translated three pixels** — the surface rising in a basket whose interior
is only 4 px deep — and `brim` is the only band that changes shape. Identical pixel counts across
three of the four bands are correct, and `TheBandLadderRises_AndOnlyBrimCrownsAboveTheRim` pins it so
nobody "fixes" it later.

**The heap cannot tell north from south.** Rows *d* and *d+4* are byte-identical, at every band: the
rim quad is symmetric about the pivot, so `opening(0)` and `opening(4)` are the same four points in
reverse winding. Four distinct pictures across eight rows. That is the art, not a stub — and the sheet
still carries eight rows so the runtime indexes it like every other container.

---

## The plates — before the bake

Rendered by the harness, from the same code the bake runs, before the bake ran. They are **not** game
screenshots — the editor plates the charter asks for (in hand, on the ground, daylight, at St Peters)
are owed once this lane gets an editor slot, and go beside these.

| plate | what to look at |
|---|---|
| `hod-bands-harness.png` | five bands × eight facings, composed back → heap → front on flat sand |
| `hod-band-ladder-harness.png` | the ladder at 12×, north and east — the clams show THROUGH the mesh, which is the whole reason the basket is baked hollow |

---

## The runtime

`HodFillPresenter` — three sprite renderers on one transform, fed by the **existing**
`HoldCatchFillSource` bridge, which already feeds every `ICatchFillTarget` on its object off the Core
event edges. No new plumbing, and no change to `ClamDig`: the dig stows into the hold, the hold raises
`FishCaught`, the bridge pushes contents, the hod picks its band.

**There is no new threshold table, and there must not be one.** The charter asked for the band ladder
to live in a Def; measured, `CatchFillMath.BandFor` already IS that one table — it is the rig's own
`FRAC` restated once, the pail and the deck tray read it, and a second opinion about what "half" means
is exactly how a drawn catch and a counted catch drift apart. The only per-hod number is capacity,
which the hold already owns (`IHold.CapacityUnits`, 20 clams). What this lane did add is
`CatchFillMath.FilledBands` — the four non-empty bands in the rigs' baked order — because the pail's
presenter had its own private copy and the hod would have been a second one.

Spoil tints the **heap layer alone**. The pail tints its whole sprite because its catch and its
container are one baked picture; the hod's are not, and galvanised wire does not rot.

---

## The editor step — what it produced

Run 2026-09-06 in a batch editor on this worktree, one process at a time.

**The bake.** `[rig-baker] clam hod: 6 sheet(s), 48 cells` — the two wire layers and the four bands,
all 40×320, plus the anchors JSON. The bands were not passed in: `HeapBands` read `ClamHod.FILLS` and
kept the four with a non-zero `CatchKit2.FRAC`.

**⭐ The two shipped layers did not move a byte.** `BakeClamHod` rewrites `Hod2_back.png` and
`Hod2_front.png` on every run, and a re-bake is not byte-deterministic in general — so the lane was
ready to decode both, confirm the pixels and restore the committed bytes. It had nothing to do: git
does not see either file as modified. The harness control held all the way through Unity's own
encoder.

**⭐ The sheets on disk ARE what the harness measured.** All four heap sheets are byte-identical to
the harness's own render of the same bands, so every number above describes the art that shipped.

**The slice.** `[CatchStorageSheetSlicer] (batch) Sliced 93 storage sheet(s) (0 failed)` — arrived at
from the other end, exactly the count the guarded set was updated to (89 + 4). Eight sprites per sheet,
pivot (0.5, 0.25) = the hod's ground centre, bottom-origin. No slicer edit was needed.

**The guards**, read from the XML rather than the exit code:

| suite | result |
|---|---|
| `CatchPass2KitTests` | 18/18 — including the four new measured ones and the sha256 pin that proves `docs/art/rigs/**` was not touched |
| `CatchStorageSheetSliceTests` | 375/375 — 93 stems × 4 per-sheet assertions + 3 |
| `HodFillTests` | 6/6 |
| `BucketFillTests` | 9/9 — the pail still reads its bands with `Bands` aliased to the shared array |

---

## In the game

`hod-bands-in-play.png` and `hod-bands-in-play-closeup.png` are captures of the running game at St
Peters, 13:00, one per band — `Camera.main` rendered at the camera's own aspect (the day/night overlay
fits itself to that and would otherwise sit inset as a bright rectangle), linear→gamma, at the game's
own framing. The hod stands a pace east of the pail in the starting tools group.

The bands were set by putting real `CatchItem` clams in the hod's own hold and letting the shipped
chain do the rest — `IHold.TryAdd` → `HoldCatchFillSource.Refresh` → the presenter — at 0 / 1 / 11 /
17 / 20 of its 20, which `CatchFillMath.BandFor` reads as empty / few / half / full / brim. The dig
itself is not in the picture because it is not what the picture is for: `HodFillTests` drives
`ClamDig.TryDig` through the same chain and asserts the band.

**The ladder reads at play scale**, which was the open question: three of the four bands are the same
plate lowered by a pixel or two, and on grass, through the wire, they are still four different amounts
of clam.

---

## What is still owed

1. **The hod in hand.** The charter asks for a plate of the hod carried as well as on the ground. It is
   a `CarriableBucket` and the carry verb is shipped, so this is a driving job, not a building one —
   but it was not done and should not be claimed.
2. **An owner call on where the hod belongs.** It is in the starting tools group, beside the pail, the
   rod and the shovel. The rig calls it “the wire roller basket a clam digger drags along the flat”,
   which argues for the bar rather than the lawn — and possibly for it following her. Placement is one
   line in `StPetersBuilder`.
3. **`StPeters.unity` is ~590 hunks behind the code.** Not this lane's doing and not fixed here: the
   committed scene still carries `SpriteShadow`'s six pre-profile fields on 438 components and four
   dead `ClamHoleVisual` fields on 66 holes, all of which the current code has replaced. The first
   editor to open and save that scene normalises every one of them. This lane's scene diff is +490/−0
   — sixteen new YAML documents and nothing else — because the churn hunks were separated out and
   dropped, and the hod was then re-verified as loading intact from the patched file. Whoever
   legitimately rebuilds St Peters next will carry that catch-up, and it should be its own commit.
