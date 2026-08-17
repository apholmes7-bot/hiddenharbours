# The Speech Bubble Kit — `BubbleKit` + `TalkCue`

Hidden Harbours · the talking UI, as objects in the scene · **the bubble comes from the character**

Animal Crossing's shape (paper bubble, name chip, a second bubble of selectable rows) with Eastward's
move (the bubble is **anchored at the speaker** by a tail whose tip is a measured pixel), in harbour
colours. **No portraits** — the in-world character is the portrait, and nothing here covers her. The
world stays visible behind the exchange, so every piece is drawn at the world's own grid.

The sound is the bubble populating, so the text area is a **monospace cell grid with a caret cell**:
the fill cadence is the characterisation channel, and the art has to hold it exactly.

## Quick start

Open **`harness.html`** (double-click). Scrub the facing, pick a tail — down or **up** — watch the line
fill at the speaker's head, step the option rows, fire an emote. Three taste calls sit on boards under
the scene; the type metrics, the occupancy arithmetic and the mouth-channel probe are below them; the
reference sheet at the bottom is drawn by the kit itself.

```
docs/art/rigs/dialogue-bubble-kit/
├─ README.md
├─ dialogueBubble.contract.json     ← MEASURED values, per-value provenance (generated, never hand-edited)
├─ harness.html                     ← live scene + taste boards + metrics + probe + reference sheet + PNG exports
├─ support.js                       ← harness runtime (do not edit)
└─ Art/
   ├─ dialogueBubbleRig.js          ← the kit: panel · tails · chip · options · cursor · marker · caret
   ├─ talkCueRig.js                 ← the character-side talk cues + the mouth-channel probe
   ├─ characterIsoRig6.js           ← harness only — the real speaker at all 8 facings
   ├─ headIsoRig3.js                ← harness only (characterIsoRig6 needs it)
   └─ eyeIsoRig.js                  ← harness only
```

## The grid — and why this one

| | |
|---|---|
| Scale | **32 px = 1 m** |
| Why | The bubble is an object IN the scene, so it is authored on the same grid as the props it stands among — the assets grid every shipped rig in this repo bakes at (fleet, shore, trees, wall calendar). A bubble pixel is a prop pixel. |
| The other grid | 24 px/m is the **camera-side** number (`two-pixel-grids-ppu-vs-assetsppu`). Authoring there would put UI pixels on a different pitch from world pixels, and the bubble would read as an overlay — the one thing this slice must not do. |
| Light | top-of-frame key (art bible §1): light row under the top edge, shade row above the bottom edge. No upper-left bias. |
| Keyline | **none** (ADR 0031). The panel's 1 px ink ring is the object's edge, not an outline pass. |
| Lit states | **one**. The day–night grade is the in-engine layer (bible §6). |
| Colour | no new colours (ADR 0015) — every hex is lifted from a shipped rig, with per-value provenance in the contract. |

## Type — the ruling, the face, and the arithmetic

**Speech is set in sentence case** (owner ruling, 2026-08-17). Caps stay on the **name chip** and the
**option rows**, which are labels rather than voice.

**Provenance: the face is newly drawn for this kit.** Nothing borrowed, no licence to carry. The wall
calendar's 3 × 5 caps stay where they are — that face is print, this one is speech. **Lowercase exists**:
a full a–z with a 4-row x-height, ascenders to cap height, and 2 descender rows (`g j p q y`), plus
repositioned points and figures.

**The two costs of mixed case, separately** — because only the second one moves any other number:

1. **Drawing the lowercase set** — done in this pass. 26 glyphs and the repositioned points. No layout
   cost of its own.
2. **The taller line box** — cell **4 × 8 → 5 × 10** (+1 px advance, +2 px line height). This is what
   re-derives everything: a 22-col 4-line panel goes 97 × 39 → **119 × 48**, and the option ladder goes
   30 / 41 / 52 → **34 / 47 / 60 px**. Taken **once, before the freeze**, so every number below is already
   the mixed-case number.

### The cell

| | |
|---|---|
| Text cell | **5 × 10 px** (advance 5, line height 10, leading 2) |
| Glyph box | 4 × 8 px |
| Cap height | 6 · x-height 4 · ascender 6 · descender 2 |
| Baseline | row 5 of the glyph box |
| Monospace | by construction — one cell per character, so the per-character fill never reflows and a caret can own a cell |
| Wrap | `charsPerLine = cols = floor((textBoxWidth + 1) / 5)  ·  BubbleKit.colsFor(px)` |
| Fill | one cell per character, left to right, line by line; layout.rows[] gives each line its cell origin, and layout.chars the total the cadence divides |

### The panel ladder — what the gameplay fill needs

```
panel w = insetL + (cols × 5 − 1) + insetR
panel h = insetT + (lines − 1) × 10 + 8 + insetB
```

| Cells (cols × lines) | Panel px | Metres | Text box px | Chars/line | Cells |
|---|---|---|---|---|---|
| 8 × 1 | 49 × 18 | 1.53 × 0.56 | 39 × 8 | 8 | 8 |
| 8 × 2 | 49 × 28 | 1.53 × 0.88 | 39 × 18 | 8 | 16 |
| 8 × 4 | 49 × 48 | 1.53 × 1.5 | 39 × 38 | 8 | 32 |
| 14 × 1 | 79 × 18 | 2.47 × 0.56 | 69 × 8 | 14 | 14 |
| 14 × 2 | 79 × 28 | 2.47 × 0.88 | 69 × 18 | 14 | 28 |
| 14 × 4 | 79 × 48 | 2.47 × 1.5 | 69 × 38 | 14 | 56 |
| 18 × 1 | 99 × 18 | 3.09 × 0.56 | 89 × 8 | 18 | 18 |
| 18 × 2 | 99 × 28 | 3.09 × 0.88 | 89 × 18 | 18 | 36 |
| 18 × 4 | 99 × 48 | 3.09 × 1.5 | 89 × 38 | 18 | 72 |
| 22 × 1 | 119 × 18 | 3.72 × 0.56 | 109 × 8 | 22 | 22 |
| 22 × 2 | 119 × 28 | 3.72 × 0.88 | 109 × 18 | 22 | 44 |
| 22 × 4 | 119 × 48 | 3.72 × 1.5 | 109 × 38 | 22 | 88 |
| 26 × 1 | 139 × 18 | 4.34 × 0.56 | 129 × 8 | 26 | 26 |
| 26 × 2 | 139 × 28 | 4.34 × 0.88 | 129 × 18 | 26 | 52 |
| 26 × 4 | 139 × 48 | 4.34 × 1.5 | 129 × 38 | 26 | 104 |
| 34 × 1 | 179 × 18 | 5.59 × 0.56 | 169 × 8 | 34 | 34 |
| 34 × 2 | 179 × 28 | 5.59 × 0.88 | 169 × 18 | 34 | 68 |
| 34 × 4 | 179 × 48 | 5.59 × 1.5 | 169 × 38 | 34 | 136 |

Min cell **8 × 1**, max **34 × 4**. Past that the line wants a second bubble, not a taller one.
`layout.rows[]` returns each drawn line with its cell origin and `layout.chars` the total the cadence
divides, so a consumer never re-measures anything. `metricsTable()` returns this whole table as data.

### The option ladder

| Rows | px | Row height | Text origin (x, y) |
|---|---|---|---|
| 2 | 111 × 34 | 13 | 16, 8 |
| 3 | 111 × 47 | 13 | 16, 8 |
| 4 | 111 × 60 | 13 | 16, 8 |

+13 px per row (34 / 47 / 60 px for 2 / 3 / 4 rows).

## The tails — six, anchored, measured both ways

The tail's **tip pixel is the anchor**. The presenter aims it at the speaker's own anchor;
`BubbleKit.mount(anchor, {clear, gap})` returns the bubble x/y that puts it there.

| key | dir | speaker is | px | tip (x, y) |
|---|---|---|---|---|
| `left` | down | below-left | 7 × 7 | **1, 6** |
| `centre` | down | below-centre | 7 × 6 | **3, 5** |
| `right` | down | below-right | 7 × 7 | **5, 6** |
| `leftUp` | up | above-left | 7 × 7 | **1, 0** |
| `centreUp` | up | above-centre | 7 × 6 | **3, 0** |
| `rightUp` | up | above-right | 7 × 7 | **5, 0** |

**Up tails ship, they are not flipped at import.** A tail is two colours with **no shading**, so a y-flip
fights nothing in the panel's light — but the kit performs the flip so the import never has to reason
about it, and so the tip pixel is *measured* in both directions rather than derived.

**`clear` is directional, and it matters.** The head anchor is the head *centre*, so a tail aimed 3 px
off the anchor buries its tip in the crown. Pass `clear` = the anchor-to-ink distance in the tail's own
direction and the tip stops `gap` px clear of the **sprite**, not of a point inside it. Measured off the
rig's own alpha (adult, idle, dir 4): **down 7 px** (to the crown), **up 36 px** (to the feet — below the head
is her body, which is the whole reason the up set exists). The harness measures this live.

The chip rides the edge **away** from the tail — top-left for down tails, bottom-left for up — so a name
and a tail never fight for a corner.

## The invariant — and the arithmetic that proves it

> **The speech panel, the name chip and the option bubble never occlude the speaker's sprite rect.**

That is the rule, because the character IS the portrait. `speakerClear` = **20 px** is *one
implementation* of it, not the rule. The **tail** is the deliberate exception: it is the pointer, it
reaches towards the speaker, and it stops `gap` = **3 px** clear of the sprite ink.

`screenBudget()` builds the **worst case** — widest panel, 4 lines, 4 rows — mounts it the way the
presenter will, at every tail, and measures:

| tail | list side | union box | panels occlude | tail tip clears ink | verdict |
|---|---|---|---|---|---|
| `left` | right | 183 × 125 | **0 px** | 3 px | holds |
| `centre` | right | 217 × 124 | **0 px** | 3 px | holds |
| `right` | left | 183 × 125 | **0 px** | 3 px | holds |
| `leftUp` | right | 183 × 125 | **0 px** | 3 px | holds |
| `centreUp` | right | 217 × 124 | **0 px** | 3 px | holds |
| `rightUp` | left | 183 × 125 | **0 px** | 3 px | holds |

Then it prices that worst case against the screen. Assumption: **output 1920×1080, per-tier pixel-perfect zoom (ruling #327). If the shipped tier ladder differs, pass tiers[] — the arithmetic does not change.**

| tier | world px | worst union | of screen | area | verdict |
|---|---|---|---|---|---|
| **2×** | 960 × 540 | 217 × 124 | 22.6% × 23% | 5.2% | fits |
| **3×** | 640 × 360 | 217 × 124 | 33.9% × 34.4% | 11.7% | fits |
| **4×** · closest | 480 × 270 | 217 × 124 | 45.2% × 45.9% | 20.8% | fits |

**The invariant holds at every tail**, and the worst case eats 20.8% of the screen at the closest tier. That is
the answer to "is 20 px enough on a narrow screen": at the tier with the biggest pixels and the least
screen, the whole exchange still fits with the speaker uncovered. Pass `tiers[]` if the shipped ladder
differs — the arithmetic does not change.

The list's side is a **preference, not a guarantee**: it hangs away from the tail and below the bubble, but
a below-right tail near the left edge has no room there, and a tall exchange near the bottom has none
underneath. Pass `bounds` {x0, x1, y0, y1} — the drawable rect — and `optionsMount` takes the side that
fits in **both** axes, returning `side` / `away` / `vSide` / `clearFromTail` / `clamped`.
`clamped: true` means nothing fits: shorten the rows, narrow the cols, or move the camera.

**Never clamp a returned x or y yourself.** Pass the bound in and let the mount answer, or `clamped`
becomes a lie about what is on screen — which is exactly how a proof stops proving anything. The harness
follows its own rule here: one placement path feeds both the drawn scene and the verdict printed under it,
and its scene is sized to the rig's worst case so nothing is ever clamped.

## Stock — four, one ships

| key | label | paper / light / shade | ink | provenance |
|---|---|---|---|---|
| `cream` **·ships** | WARM CREAM PAPER | #e8ebe5 / #f2f4ee / #d6dbd7 | #232a32 | paper/hi/lo = KTC_HOUS steps 4/5/3 (lobsterBoatVariantsIsoRig.js) · edge = KTC_BOOT[2] · ink = HAIRS.black[2] (headIsoRig3.js) |
| `newsprint` | GREY NEWSPRINT | #cdc8b9 / #dcd7c8 / #b8b2a1 | #2c2b29 | calendarRig.js DAY.paper / paperHi / paperLo / ink / inkSoft |
| `sailcloth` | SAILCLOTH | #e9e6df / #f2f4ee / #c9c4b8 | #231d14 | paper = ShoreFinds BONE · hi = KTC_HOUS[5] · lo/inkSoft = HATCOLS.oat[3]/[1] · ink/edge = ShoreFinds KEYLINE |
| `enamel` | ENAMEL BOARD | #d6dbd7 / #e8ebe5 / #a2aaae | #10141b | paper/hi/lo = KTC_HOUS steps 3/4/1 · ink/edge = KTC_BOOT[1]/[0] · inkSoft = BOOT[3] (headIsoRig3.js) |

## The selection cursor

| key | label | px | point px | note |
|---|---|---|---|---|
| `hand` **·ships** | POINTING HAND | 10 × 9 | 9, 3 | Redrawn 2026-08-17 (pass 1 read as a hand/pointer hybrid): a closed fist with a thumb knuckle, and an index finger that leaves it as a THREE-row form — ink line, lit paper row, ink line — so the finger is a cylinder against the fist mass instead of a spur off a blob. Owner’s pick. |
| `glove` | WOOL WORK GLOVE | 10 × 10 | 9, 3 | The same hand with a knitted cuff instead of a wrist. The owner’s "most ours" — worth a live trial; it costs one row and a dark band the row highlight also wants. |
| `tack` | BRASS TACK | 6 × 6 | 5, 2 | An object pressed into the row instead of a hand — no anatomy to get wrong at this size. Reads as a rivet before it reads as a pointer. |
| `hook` | FISH HOOK | 7 × 10 | 6, 2 | Charming at 6x, ambiguous at 1x (owner, 2026-08-17 — check the 1x row on the board before keeping it). It also points with its barb, which is a threat, not an invitation. |

## The selected row — four treatments

| key | label | note |
|---|---|---|
| `pill` **·ships** | GOLD PILL | Cove gold behind the row, dark ink on it. The AC read in fleet colours; the one place a saturated colour is allowed, because selection is the one thing that must never be missed. |
| `invert` | INVERTED ROW | Ink and paper swap. No new colour at all, and it survives any grade — but it reads as a redaction on paper stock. |
| `rule` | GOLD RULE | Paper stays, a gold rule under the row. Lightest touch, leans hardest on the cursor to carry the state. |
| `press` | PRESSED ROW | The row is inset a pixel with its own shade above and light below, so selection is a physical state of the object. Quietest, and the only one that costs no colour. |

## The continue marker · the caret

- **Marker** — 7 × 4, ink ring + paper fill, at **x + w − 11, y + h − 2**: on the bottom edge, right of centre,
  clear of every tail. 2-frame idle bob, **+1 px, 420 ms** a frame. It appears only when the fill completes.
- **Caret** — 1 × 6 ink bar in the cell the next character will land in, 260 ms blink if a consumer wants one.
  Shown **while filling only**; it hands off to the continue marker at the end of the line.

## The talk cues — `TalkCue`

A **cue layer**, not a new clip set: whole-pixel screen offsets applied to the rig's existing idle at
draw time. No re-bake, no sheet, nothing authored per facing.

- **Bounce** — 1 px lift, 2.6 Hz beat, every 4th beat held longer. At 32 px/m a 2 px talk bounce reads as a nod, not as speech.
- **Settle** — 210 ms at the line's end: one last lift, then still. The line ending has to be visible without reading the text.
- **Eight facings, nothing per row.** The bounce is a screen-**vertical** offset, and screen-vertical is the one axis that does not rotate with a ground bearing (ADR 0034) — so one cue is correct at all eight rows and no screen-bearing row is invented. The marker anchors to `anchors(dir).head`, which the body rig already resolves per facing.

| key | emote | marker px | length | head expr | note |
|---|---|---|---|---|---|
| `surprise` | SURPRISE | 9 × 11 | 620 ms | `oh` | The hop is the read; the marker only confirms it. |
| `laugh` | LAUGH | 11 × 11 | 900 ms | `grin` | Four bobs on the beat — the only cue that repeats. |
| `think` | THINK | 13 × 11 | 1100 ms | `worry` | Stillness IS the cue. The body stops bouncing and the dots do the work. |

### The mouth channel is already shipping — and the claim is now a test

`CharacterIso6.render(dir, {t, talk:true, expr})` reaches `HeadIso.look()` (pose forwards its opts as
the `rock` argument) and flips the mouth on the head rig's ~7.2 Hz jittered cadence. Drive it with
`talk = (fill < 1)` and the mouth stops the instant the bubble finishes populating.

```js
TalkCue.probeMouth(CharacterIso6)   // → {pass, px, t, bbox, rowsBelowHeadAnchor}
```

It renders one frame **twice at the same t**, talk on and talk off, so any changed pixel is the talk
channel — then asserts the diff is mouth-sized and sits in the lower face. The harness runs it and shows
the result. **Run it in the import PR and fail on it.**

The rename that follows must keep the **stable ids**: `rock` is the sixth positional argument of
`pose()`, read as `arguments[5]`; `render()` / `pose()` / `anchors()` signatures and the opts keys
(`t`, `talk`, `expr`) must stay exactly as they are, or every consumer page and this kit break at once.
Alias first, deprecate later.

## Reference sheet — every piece, 1×, with its crop box

`BubbleKit.sheet({stock})` draws it with the same functions the game calls, so a piece cannot look one
way on the sheet and another way in the scene. `sheetSpec({stock})` returns this table as data.

| piece | px | sheet x, y | note |
|---|---|---|---|
| `panel.slice` | 18 × 18 | 4, 14 | 9-slice source tile — corner 6, edge 1 px repeat |
| `panel.min` | 49 × 18 | 68, 14 | 8 cols, 1 line |
| `panel.wide` | 119 × 28 | 127, 14 | 22 cols, 2 lines |
| `tail.left` | 7 × 7 | 256, 14 | down · tip 1,6 |
| `tail.centre` | 7 × 6 | 310, 14 | down · tip 3,5 |
| `tail.right` | 7 × 7 | 374, 14 | down · tip 5,6 |
| `tail.leftUp` | 7 × 7 | 433, 14 | up · tip 1,0 |
| `tail.centreUp` | 7 × 6 | 4, 68 | up · tip 3,0 |
| `tail.rightUp` | 7 × 7 | 78, 68 | up · tip 5,0 |
| `chip` | 32 × 12 | 147, 68 | mount x+4, overlap 4 |
| `marker.f0` | 7 × 4 | 189, 68 | bob frame 0 |
| `marker.f1` | 7 × 5 | 243, 68 | bob frame 1 (+1 px) |
| `caret` | 1 × 6 | 297, 68 | cell 5x10 |
| `cursor.hand` | 10 × 9 | 331, 68 | point 9,3 |
| `cursor.glove` | 10 × 10 | 395, 68 | point 9,3 |
| `cursor.tack` | 6 × 6 | 464, 68 | point 5,2 |
| `cursor.hook` | 7 × 10 | 4, 106 | point 6,2 |
| `type.caps` | 64 × 8 | 68, 106 | cap 6, cell 5x10 |
| `type.lower` | 64 × 8 | 142, 106 | x-height 4, descender 2 |
| `type.rest` | 64 × 8 | 216, 106 | newly drawn for this kit |
| `type.figs` | 84 × 8 | 290, 106 | figures + points |
| `options.2` | 111 × 34 | 384, 106 | 2 rows · +13 px each |
| `options.3` | 111 × 47 | 4, 166 | 3 rows · +13 px each |
| `options.4` | 111 × 60 | 125, 166 | 4 rows · +13 px each |
| `highlight.pill` | 91 × 34 | 246, 166 | GOLD PILL |
| `highlight.invert` | 91 × 34 | 347, 166 | INVERTED ROW |
| `highlight.rule` | 91 × 34 | 448, 166 | GOLD RULE |
| `highlight.press` | 91 × 34 | 4, 252 | PRESSED ROW |
| `say.left` | 119 × 52 | 105, 252 | panel + tail + chip + text + marker |
| `say.centreUp` | 119 × 51 | 234, 252 | panel + tail + chip + text + marker |

## API

```js
// assembly
BubbleKit.say(ctx, x, y, { text, cols, name, tail, fill, frame, stock, marker, caret })  // → layout
BubbleKit.layoutSay(x, y, o)                     // same maths, nothing drawn (top/bottom include chip + tail)
BubbleKit.mount(anchor, { tail, clear, gap })    // → {x, y} so the tail tip lands clear of the sprite
BubbleKit.options(ctx, x, y, { rows, sel, cols, highlight, cursor, stock })
BubbleKit.optionsMount(sayLayout, { rows, cols, bounds })   // → {x, y, w, h, side, away, vSide, clearFromTail, clamped}

// pieces
BubbleKit.panel(ctx, x, y, w, h, o)  ·  slices(w, h)  ·  tail(ctx, x, y, kind, o)
BubbleKit.chip(ctx, x, y, label, o)  ·  cursor(ctx, x, y, kind, o)
BubbleKit.marker(ctx, x, y, frame, o)  ·  caret(ctx, x, y, o)
BubbleKit.text(ctx, str, x, y, col, limit)  ·  wrap(str, cols)  ·  textW(str)  ·  colsFor(px)

// the numbers, generated
BubbleKit.metricsTable({ cols, lines, optCols })          // the line arithmetic
BubbleKit.screenBudget({ out, tiers, speaker, clear })    // the invariant as arithmetic
BubbleKit.sheet(o)  ·  sheetSpec(o)  ·  contract()

// talk cues
TalkCue.body({ t, talking, startedAt, stoppedAt, emote, emoteAt })   // → {dy, beat, phase, state}
TalkCue.charOpts(o)                                                  // → opts for CharacterIso*.render
TalkCue.marker(ctx, x, y, kind, { t, stock })  ·  markerAt(anchor, { emote, side, place })
TalkCue.probeMouth(CharacterIso6, { dir, samples, maxPx })
```

## Flagged for the owner — drawn or costed, not decided

1. **Per-speaker chip tint** — held, per the handoff. One ramp reference is the only thing that moves.
2. **The pointing hand was redrawn** (2026-08-17): pass 1 read as a hand/pointer hybrid. The fix is a
   closed fist with a thumb knuckle plus an index finger built as a **three-row form** — ink line, lit
   paper row, ink line — so the finger reads as a cylinder against the fist instead of a spur off a blob.
   The glove carries the same hand with a knitted cuff.
3. **Cursor alternates** — the wool glove is the owner's "most ours" and is drawn, measured and live on
   the board; the brass tack and the fish hook are there too. The hook is charming at 6× and **ambiguous
   at 1×** (owner) — the board shows every cursor at 1× beside its 6× tile, so that judgement is made
   against pixels rather than against a blow-up.
4. **Row highlight** — the gold pill ships. The pressed row is the only alternative that costs no colour,
   if a grade ever fights the gold.
5. **Sailcloth** is the strongest alternate stock if cream ever feels too AC (owner) — one call away.
6. **Emphasis beats** are uniform every 4th beat. Punctuation-driven emphasis needs the line parsed —
   that is the dialogue system's job, not art's.

## Gameplay sidecar

`gameplay/bubbleKit.gameplay.json` is the gameplay-facing half of the contract, generated from the two
rigs: load order and globals, the calls a presenter makes, the type arithmetic for wrapping and for the
per-character fill, the tail anchors with their directional `clear`, the option ladder and its bounds
contract, the cursor point pixels, the stock hexes, the invariant **with its proof at all six tails**, and
the talk-cue cadence and emote table — plus **hit-boxes**, including one fully worked layout (speaker
anchor in, every rect out) so an implementation can diff its own numbers against a known-good answer
instead of reading pixels.

```
gameplay/bubbleKit.gameplay.json     ← load order · metrics · hit-boxes · invariant proof · cue table
```

Nothing in it is hand-written. Regenerate it from the rigs rather than editing it.

## Import notes for the wiring PR

- This kit lands under `docs/`, where `.meta` files do not apply. The import into
  `Assets/_Project/Art/UI/` happens in the gameplay lane's PR — check `.gitattributes` covers the PNG
  paths before that PR's first commit. The presenter already carries a tinted-rect fallback, so nothing
  blocks on this kit.
- Nothing here bakes: every piece is **live-drawn** from the rig at native resolution, exactly like the
  wall calendar's face. The PNG exports on the harness exist for review and for slice cutting, not as the
  shipping art path.
- Run `TalkCue.probeMouth()` in the import PR; it is the test form of this kit's one behavioural claim.
- `harness.html` persists its own state to `localStorage['hh.bubble']` — harness only.
- **The harness scene runs on the wall clock, not on frame deltas.** `requestAnimationFrame` does not
  fire in a hidden document, and a preview pane can be frozen outright, so scene time is derived from
  `performance.now()` and a paint pump lives in the page itself: whenever a paint does happen — a thaw,
  a visibility change, a control click, a real frame — it shows the correct time rather than a clock that
  crawled at a fraction of real speed. Worth stealing for any harness that has to be screenshotted.
- Offline-safe; only the harness's heading fonts touch the network.
