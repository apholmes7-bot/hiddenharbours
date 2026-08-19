# The Notebook Kit — `NotebookKit` + `NotebookIsoKit`

Hidden Harbours · **the player's main UI surface** · no menus, a physical book

Active quests with their steps, finished quests, every how-to page, and the lists the player writes
herself — all of it in one object she holds up. The diegetic answer to a quest log and a help menu.
Overflow is **more pages**: there is no scroll state anywhere in this kit.

> **This kit has no face of its own.** `CELL` and `FONT` are imported from the
> [dialogue bubble kit](../dialogue-bubble-kit/README.md) by reference, and the page is set in that face
> **clean** — same glyphs, same cell, same weight as a speech bubble, enforced by `probeType()`. The
> roughened hand from passes 1–2 is off (owner, 2026-08-17: unreadable at page sizes) and stays behind
> `hand(..., {rough:1})` on the taste board. What makes the book read as HERS is the furniture — her
> pencil-ruled leaves, her wobbling checkboxes, her strikes, pencil weight for her own lines.

## Quick start

Open **`harness.html`** (double-click). Pick a family, then push the layout: the longest step list, a
title that wraps three lines, a knowledge page at full text, notes mixing prose and checkboxes, and
seven quests into two leaves so overflow is *demonstrated* rather than asserted. Seven taste boards sit
under the spread — click a tile and the whole book redraws in it. The metrics, the read budget, the four
probes and the reference sheet are below.

```
docs/art/rigs/notebook-kit/
├─ README.md
├─ notebook.contract.json            ← MEASURED values, per-value provenance (generated, never hand-edited)
├─ gameplay/notebookKit.gameplay.json ← the presenter-facing subset: rects, states, flow rules, probes
├─ harness.html                      ← live spread + boards + metrics + probes + reference sheet + exports
├─ support.js                        ← harness runtime (do not edit)
└─ Art/
   ├─ notebookRig3.js                ← the kit: book · tabs · quest anatomy · knowledge · notes · turn · boards
   ├─ dialogueBubbleRig.js           ← REQUIRED — the imported face, cursors and selection gold
   └─ isoSolid.js                    ← required by the closed object only
```

## The grid — declared

| | |
|---|---|
| Scale | **32 px = 1 m** (the assets grid) |
| Why | THIS KIT IS AUTHORED AT 32 px/m — the assets grid, same as every shipped rig and the bubble kit. 24 px/m is the camera-side number and authoring there would put UI pixels on a different pitch from world pixels. |
| Lit states | one. The lamplit palette in the harness is a preview of the in-engine day/night grade (bible §6). |
| Keyline | none (ADR 0031) |
| Colour | no new colours (ADR 0015) — every hex is carried from pass 1/2 or lifted from a shipped rig, with provenance in the contract |

## Type — one system with the bubble kit

| | |
|---|---|
| Text cell | **5 × 10 px** (advance 5, line box 8, leading 2) |
| Glyph box | 4 × 8 px · cap 6 · x-height 4 · descender 2 · baseline row 5 |
| Source | `BubbleKit.CELL` / `BubbleKit.FONT` — **imported**, never redeclared |
| Printed rule | row **7** of the line box, two under the baseline, so descenders cross it as they do on real ruled stock |
| Case | sentence case — knowledge pages are the longest running text in the game and mixed case is what makes them readable. CAPS on tab chips, running heads and the DONE stamp — those are labels, not voice |
| Slack | 1 px inside the cell — where the crowding that reads as an uneven hand lives |
| Wrap | `charsPerLine = cols = floor((textLaneWidth + s) / (5·s)) · NotebookKit.colsFor(px, s) · wrap() is BubbleKit.wrap, shared` |

**The cost of the shared cell, stated once.** Pass 2 set the book in a 4 × 8 (notebookRig2) caps-only cell of
its own. Moving to the bubble kit's 5 × 10 re-derives every pixel number in the kit:
a 22-col 16-line spread: 209 × 143 px → 320 × 192 px. Taken once, before the freeze, so every number here is already the mixed-case number.

### The ladder

| Cells a leaf | Title lane | Spread @1x | Metres | @2x | @3x |
|---|---|---|---|---|---|
| `probeType()` | **PASS** | the notebook’s cell and glyph set ARE the bubble kit’s, by reference — one voice across the talking UI and the book UI, enforced rather than promised. |
| `probeHand()` | **PASS** | the ROUGHENED mode (flagged OFF) differs from the clean face pixel by pixel — wobble, crowding, dropped strokes — while every cell origin stays exactly x + i·5·s. The page ships clean, so what this proves is that switching roughness back on cannot break the arithmetic. |
| `probeWrite()` | **PASS** | ink at fill k is a subset of ink at fill k+1; a part-written block carries exactly ONE caret and no strike or tick on text that has not been written yet; a finished one carries its marks and no caret. |
| `probeFlow()` | **PASS** | blocks are kept whole across the gutter when they could fit a leaf, nothing is drawn past the last ruled line, and placed + overflow = given — so the pencil "n more" is a fact and the page turn is the only answer to a long list. |
| 16 × 6 | 18 | 260 × 92 | 8.13 × 2.88 m | 520 × 184 | 780 × 276 |
| 16 × 16 | 18 | 260 × 192 | 8.13 × 6 m | 520 × 384 | 780 × 576 |
| 16 × 22 | 18 | 260 × 252 | 8.13 × 7.88 m | 520 × 504 | 780 × 756 |
| 24 × 6 | 26 | 340 × 92 | 10.63 × 2.88 m | 680 × 184 | 1020 × 276 |
| 24 × 16 | 26 | 340 × 192 | 10.63 × 6 m | 680 × 384 | 1020 × 576 |
| 24 × 22 | 26 | 340 × 252 | 10.63 × 7.88 m | 680 × 504 | 1020 × 756 |
| 40 × 6 | 42 | 500 × 92 | 15.63 × 2.88 m | 1000 × 184 | 1500 × 276 |
| 40 × 16 | 42 | 500 × 192 | 15.63 × 6 m | 1000 × 384 | 1500 × 576 |
| 40 × 22 | 42 | 500 × 252 | 15.63 × 7.88 m | 1000 × 504 | 1500 × 756 |

Formulae: `2 × pageW + gutter + 2 × (coverPad + edge) + tabCol = 10c + 100` · `pageH + 2 × (coverPad + edge) = 10L + 32` · one more line = +10 px of height; one more column = +10 px of width (both leaves).
**INTEGER scale only — fit() picks it. A fractional scale puts the hand on a half cell and the wobble stops reading as a hand and starts reading as a bug.**

## The book as an object

| Part | What ships |
|---|---|
| Closed cover | `NotebookIsoKit` — the world prop **and** the icon the open verb mounts. ~3 × 5 px, 8 facings, cover ramp from the cover board. |
| Open spread | `drawUnit()` — two leaves, gutter, fore-edge, tabs off the edge. `layout().leaves[i]` publishes each leaf's inset rect and row grid. |
| Page turn | **3 frames** — `pageTurn(ctx, layout, frame)`. a flip, not a scroll: lift off the right leaf, stand at the gutter, land on the left. ~70 ms a frame at 3 frames is a page turn you can read; slower reads as a cutscene. |
| Bookmark ribbon | `ribbon()` — 3 px, cover height plus a tail. The only thing that reads on a dark cover. |
| Wear | `wear(ctx, layout, 1..2)` — nibbled fore-edge, then a stain. Cheap and flagged, not gold-plated. |
| Page numbers | `pageNumber()` — pencil, outside corner. `leaves[i].pageNo` is the rect. |
| Corner marks | `cornerMark()` — `layout().cornerNext` / `cornerPrev`. The dog-ear is decoration; these are the affordance. |

## The tab system

FOUR families ship as furniture: Active · Done · Knowledge · My Notes. Knowledge takes N SUB-TABS and N is DATA — the kit ships the chip and the label rect, never a label.

| | |
|---|---|
| Mount line | `layout().tabMountX` (the fore-edge) · chips at `layout().tabs[i]` |
| Chip | 37 × 12 px · pad 3 · gap 3 · up to **8 stubs** |
| Max label | colsFor(tabW − pad − 1) — 5 characters at scale 1, clipped not shrunk. Sub-tabs lose one to the narrower stub. |
| States | on — pulled out, inked edge, lit top row · off — flush, unlit · cursor — the pointing hand rests on the stub at layout().tabs[i].cursor |
| Styles | HAND-LETTERED CARD **(ships)** · PENCIL ON TAPE · STITCHED CLOTH |

## Quest anatomy — the load-bearing art

`block = { kind:"quest", title, context?, steps:[{text, state}], done? }. blockRows() turns it into ruled lines; flow() places it.`

| Part | Rect | Treatment |
|---|---|---|
| Title row | `rows[i].title` (titleCols wide) | ink, the hand at press 0.9 |
| Context line | same x, one row down | pencil at press 0.68 — the asker or the deadline, never a second title |
| Step box | `rows[i].box` · 6 × 6 px | the checkbox family, drawn once, reused by her own note lines |
| Step text | `rows[i].text` (cols wide) | ink; struck through when done |
| Checkbox states | unchecked · checked · **current** | INK ARROW **(ships)** · GOLD PILL · INKED BOX |
| Finished quest | block-level | STRUCK + TICKED **(ships)** · HARBOUR STAMP · INK FADE |

Hit-boxes: flow().placed[k].hit is the whole block (a quest that wraps is one thing to click); .rows[j].r.hit is the ruled line; .rows[j].r.box is the box a mouse actually aims at

## Knowledge pages · My Notes

- **Knowledge** — block = { kind:"section", head, body, slot? } — head in ink with a ruled underline, body in the hand at titleCols. the printed ruled stock carries the body; the facing leaf is squared so a diagram lands on the same lattice as the type
- **Illustration slot** — ART RESERVES THE ROOM AND SAYS WHERE. The frame, the corner ticks and the caption ship now; what goes inside is a later content drop (knots, a fish plate, a rigged line). Rect: the rows the section RESERVED (slotLines, default 5) — the frame flows with the text, so knowledge copy can never run through it. layout().plate is the default full-leaf rect for a plate that is not part of a section.
- **My Notes** — the leaves are stock "mine" — the same paper, ruled by HER in pencil, wobbling, with no printed margin rule. Distinct by drawing, not by a new colour.
- **Note lines** — block = { kind:"note", text, box?, state? } — prose lines run titleCols wide with no box; task lines take the shared checkbox family at cols wide
- **New note** — block = { kind:"newnote" } — newNoteMark() draws a fresh box and a pencil cross in the margin lane; its rect is the affordance’s hit-box

## Flow — overflow is more pages

`flow(blocks, layout) → { placed[], overflow, used[], capacity, filled }`

blocks are kept whole. A quest that fits a leaf but not the remaining rows moves to the facing leaf; a quest that fits neither is split, because the alternative is not drawing it. what is left over is NOT drawn and NOT shrunk: overflowMark() writes a pencil "n more" with an arrow, and the answer is pageTurn(). There is no scroll state in this kit.

## Selection

the pointing hand and the cove gold are BubbleKit.CURSORS / BubbleKit.GOLD, imported — one selection vocabulary across the talking UI and the book UI. a row cursor rests at rows[i].cursor (the margin lane, 12 px, sized for the bubble kit’s 10 px hand); a tab cursor rests at tabs[i].cursor.

**GOLD MEANS SELECTED. If the CURRENT step also takes gold (currents.pill), one colour carries two meanings on one page — flagged, not decided.**

## The taste boards

| Board | Asking | Tiles | Ships |
|---|---|---|---|
| THE COVER | the closed book is also the icon gameplay mounts, so this colour has to read at 3 × 5 px AND against water | WAXED DARK TEAL · OXBLOOD LEATHER · OILED CANVAS | **WAXED DARK TEAL** |
| PAPER STOCK | the printed leaves and her own leaves must not be the same drawing, or My Notes reads as another printed tab | PRINTED RULED · PRINTED SQUARED · HER OWN RULES · BLANK | **PRINTED RULED** |
| TAB STYLE | the four families are permanent furniture; knowledge sub-tabs arrive as the player learns, so they may want to look added-later | HAND-LETTERED CARD · PENCIL ON TAPE · STITCHED CLOTH | **HAND-LETTERED CARD** |
| THE CHECKBOX | drawn once and reused by quest steps AND her own lists, so it has to survive both voices | PENCIL SQUARE · PRINTED SQUARE · PENCIL CIRCLE | **PENCIL SQUARE** |
| THE CURRENT STEP | the one row the eye must find first — and the one place the bubble kit’s gold might be spent twice | INK ARROW · GOLD PILL · INKED BOX | **INK ARROW** |
| A FINISHED QUEST | the Done tab is a page of these, so whatever this is, it is what that tab looks like | STRUCK + TICKED · HARBOUR STAMP · INK FADE | **STRUCK + TICKED** |
| THE FACE ON THE PAGE | the shipped answer is the bubble kit’s face, clean — the roughened hand stays drawn and measured so the call can be re-made against real pages rather than a screenshot | THE BUBBLE KIT’S FACE · THE ROUGHENED HAND | **THE BUBBLE KIT’S FACE** |

Every tile is drawn by the same functions the page calls, so a tile cannot look one way on the board and
another way on the page. The owner's calls are listed under *Flagged* below — the boards are how they get
made, not where they get made.

## The read budget

the book is READ, not glanced at: the hand never draws below one cell (scale 1), the spread is placed at an INTEGER cell scale, and it is a held object rather than a full-screen menu.

| Tier | Room | Scale | Cells a leaf | Spread | Screen | World left | Holds |
|---|---|---|---|---|---|---|---|
| 2× | 825 × 464 | s3 | 17 × 12 | 810 × 456 | 71.3% | 28.7% | yes |
| 3× | 550 × 309 | s2 | 17 × 12 | 540 × 304 | 71.3% | 28.7% | yes |
| 4× **closest** | 412 × 232 | s1 | 31 × 20 | 410 × 232 | 73.4% | 26.6% | yes |

**at the CLOSEST tier (4×) the open book occupies 73.4% of the screen (410 × 232 px of 480 × 270), leaving 26.6% of the world visible. It DOMINATES, by design — it is held up in front of her — and the presenter now chooses that knowing the fraction.**

⚠️ **THE PLAYER CAN NOW STAND CLOSER THAN 4×.** The mouse wheel steps the walking view through the camera's tiers (owner ruling 2026-08-19 — `docs/design/boats-and-navigation.md` §9.8), and its shipped range reaches **6×** (a 320 × 180 screen); the deck and live-haul framings have sat at 5× and 6× since 2026-07-08. A 410 × 232 spread does not fit either, and `fit()` tables nothing past 4×. This is a note for the **presenter lane**, not a change to the kit: when the book gets an open binding it must either extend `fit()` past 4× or refuse to open below its floor and say so. The camera refuses a *zoom* while a modal is open, which does not help a book *opened* at a tier it cannot fit.

**THE NUMBER THE WRITING LANE NEEDS: at the closest tier a step line holds 31 characters and a title line 33, with 20 ruled lines a leaf. A step longer than that is not clipped — it wraps and eats a line, and a quest whose block outgrows a leaf moves whole to the facing leaf. Write to 31 and nothing surprises anybody.**

## The probes — run these in the import PR and fail on them

| Probe | Verdict | Claim |
|---|---|---|

## Reference sheet — drawn by the kit itself

`NotebookKit.sheet()` calls the same piece functions the page calls. Crop boxes are in
`notebook.contract.json` under `sheet`.

| Piece | px @1x | Crop (x, y, w, h) | Note |
|---|---|---|---|
| `stock.ruled` | 70 × 34 | 4, 30, 70, 34 | PRINTED RULED · pitch 10, rule at row 7 |
| `stock.squared` | 70 × 34 | 84, 30, 70, 34 | PRINTED SQUARED · pitch 10, rule at row 7 |
| `stock.mine` | 70 × 34 | 164, 30, 70, 34 | HER OWN RULES · pitch 10, rule at row 7 |
| `box.pencil` | 6 × 6 | 244, 30, 6, 6 | PENCIL SQUARE · SHIPS |
| `box.printed` | 6 × 6 | 303, 30, 6, 6 | PRINTED SQUARE |
| `box.round` | 6 × 6 | 367, 30, 6, 6 | PENCIL CIRCLE |
| `box.ticked` | 7 × 7 | 421, 30, 7, 7 | done · ticked, never emptied |
| `current.arrow` | 8 × 8 | 480, 30, 8, 8 | INK ARROW · SHIPS |
| `current.pill` | 8 × 8 | 4, 90, 8, 8 | GOLD PILL |
| `current.dot` | 8 × 8 | 73, 90, 8, 8 | INKED BOX |
| `caret` | 1 × 6 | 137, 90, 1, 6 | the cell the next character lands in |
| `stamp.done` | 25 × 10 | 171, 90, 25, 10 | harbour stamp · on the board, not shipped |
| `cursor.hand` | 10 × 9 | 230, 90, 10, 9 | BubbleKit · point 9,3 · SHIPS |
| `cursor.cuff` | 10 × 9 | 294, 90, 10, 9 | BubbleKit · point 9,3 |
| `cursor.tack` | 6 × 6 | 358, 90, 6, 6 | BubbleKit · point 5,2 |
| `tab.card` | 37 × 12 | 422, 90, 37, 12 | HAND-LETTERED CARD · SHIPS |
| `tab.tape` | 37 × 12 | 471, 90, 37, 12 | PENCIL ON TAPE |
| `tab.cloth` | 37 × 12 | 520, 90, 37, 12 | STITCHED CLOTH |
| `mark.new` | 22 × 10 | 4, 128, 22, 10 | a new line on her page |
| `mark.next` | 5 × 7 | 53, 128, 5, 7 | page corner · next |
| `mark.prev` | 5 × 7 | 107, 128, 5, 7 | page corner · prev |
| `dogear` | 9 × 9 | 161, 128, 9, 9 | the corner she turned down |
| `ribbon` | 5 × 26 | 200, 128, 5, 26 | bookmark · where she left off |
| `select.pill` | 40 × 10 | 239, 128, 40, 10 | GOLD PILL · SHIPS |
| `select.bracket` | 40 × 10 | 303, 128, 40, 10 | PENCIL BRACKET |
| `select.rule` | 40 × 10 | 382, 128, 40, 10 | GOLD RULE |
| `select.press` | 40 × 10 | 446, 128, 40, 10 | PRESSED ROW |
| `type.clean` | 104 × 8 | 4, 180, 104, 8 | THE PAGE FACE · cell 5x10 · BubbleKit glyphs, pixel for pixel |
| `type.pencil` | 94 × 8 | 118, 180, 94, 8 | her own lines · the SAME face in pencil weight — ink and pencil differ by COLOUR, never by holes |
| `type.rough` | 89 × 8 | 222, 180, 89, 8 | the alternate · DOES NOT SHIP · hand(.., {rough:1}), on the board and measured by probeHand() |
| `page.quests` | 260 × 92 | 321, 180, 260, 92 | ACTIVE QUESTS · title + context + step list, flowing across both leaves |
| `page.done` | 260 × 92 | 4, 298, 260, 92 | FINISHED · the same anatomy under the done treatment |
| `page.knowledge` | 260 × 92 | 274, 298, 260, 92 | HOW IT IS DONE · section header, rule lines, illustration slot on the squared leaf |
| `page.notes` | 260 × 92 | 4, 416, 260, 92 | MY OWN LISTS · her paper, her rules, her checkboxes, and a place to start a new one |
| `turn.1` | 260 × 92 | 274, 416, 260, 92 | page turn frame 1 of 3 |
| `turn.2` | 260 × 92 | 4, 534, 260, 92 | page turn frame 2 of 3 |
| `turn.3` | 260 × 92 | 274, 534, 260, 92 | page turn frame 3 of 3 |

## Flagged — the owner's calls, surfaced not resolved

1. THE DONE TREATMENT. Struck + ticked ships by default (cheapest, carried, keeps the tick). The harbour stamp is the best read on a Done tab and the most characterful, but it is the harbourmaster’s mark in HER book — that is a fiction call, not an art call. Ink fade is quietest and loses the tick. All three are drawn and on the board.
2. THE CURRENT STEP. The ink arrow ships because gold already means SELECTED, and spending it twice on one page is how a colour stops meaning anything. If the owner wants the bubble kit’s gold-pill emphasis on the current step, the swap is one string — and then selection needs its own mark (the pencil bracket is drawn and measured).
3. THE FACE ON THE PAGE — RESOLVED 2026-08-17, recorded here because it reverses pass 1 and 2. The page is set in the bubble kit’s face CLEAN: the roughened hand (wobble, dropped strokes, crowding, drift) was unreadable at page sizes, and knowledge pages are the longest running text in the game. The roughening is still drawn, still measured by probeHand(), and one flag away (`rough`) if the owner wants it back for short pages. What carries "she wrote this" now is the furniture: her pencil-ruled leaves, her wobbling boxes, her strikes, pencil weight for her own lines.
4. THE COVER. Waxed dark teal ships (pass 1). Oxblood reads instantly against water and is the best closed-icon at 3 × 5 px; oiled canvas reads as a ledger rather than a diary. Whichever wins, the ribbon stays — it is the only thing that reads on a dark cover.
5. HOW MUCH SCREEN THE OPEN BOOK TAKES. readBudget().occupancyAnswer states the fraction at the closest tier instead of asserting a screenshot. The kit’s position: it is a held object and it may dominate; the number is published so the presenter chooses knowingly.
6. KNOWLEDGE SUB-TAB COUNT. N is data. The chip clips at 5 characters and 8 stubs fit the fore-edge at any legal size — past that the tab column needs a second bank, which is a layout change, not a label change.
7. WEAR. Two levels ship cheap. Per-page wear, ring stains, a cracked spine and a swollen block are a separate ask with real art in them.
8. THE ILLUSTRATION SLOT’S CONTENTS. The rect, the frame and the caption ship. Nobody has said what draws inside it.

## Handoff to gameplay

`gameplay/notebookKit.gameplay.json` is the presenter-facing subset: the data model, every rect and
hit-box, the flow rules, the tab mount, the copy budget and the probes to run. The gameplay side (quest
data model, knowledge registry, note entry, and the open binding — the dev-key ledger is exhausted, so
opening rides an existing verb) is a separate lane written **against this contract**; nothing in this kit
contains a word of game text.
