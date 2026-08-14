# ART BRIEF — the diegetic devices: a calendar, a notebook, a phone, a computer

**To:** art-director (`agents/art-director.md` — your lane is `docs/art/rigs/**`)
**From:** ui-ux, on the owner's 2026-08-14 direction
**Status:** requested, not started. **Partly gated on owner rulings — read §5 before you draw a phone.**
**Design of record:** [`../../design/diegetic-devices.md`](../../design/diegetic-devices.md) (capture +
the twelve rulings) · sits under [`../../design/diegetic-ui-and-inventory.md`](../../design/diegetic-ui-and-inventory.md)
(the ratified *why*) and [`../../design/dialogue-and-knowledge.md`](../../design/dialogue-and-knowledge.md)
(knowledge lives in things and people)

The owner has directed that the in-game UI grows a **suite of diegetic devices** — a wall calendar you
read, a notebook that tracks what people have asked of you, a mobile phone carrying a few apps, and
computers. Your UI rig catalogue (`docs/art/rigs/ui/`) already holds thirteen rigs that obey the
diegetic rule perfectly — *"In-world UI is only ever a real object that carries it"*. **These four are
the same rule applied off the boat**, and none of them exists yet.

**Do them in the order in §2.** The calendar is unblocked and cheap; the phone is blocked on a taste
ruling you can help the owner make.

---

## 1. ⭐ The one requirement that is gameplay, not decoration

**Every one of these devices lives at two scales at once, and neither scale can borrow from the other.**

Run the canon arithmetic (§5.2: PPU 32, 1 unit = 1 m, humans ~1.8 m):

| Object | Real size | On screen, in the world |
|---|---|---|
| Phone in a character's hand | ~0.14 m | **≈ 4 px** |
| Notebook, open | ~0.3 m | **≈ 10 px** |
| Wall calendar | ~0.4 m | **≈ 13 px** |
| Computer monitor | ~0.4 m | **≈ 13 px** |

So each device needs **two pieces of art that are not the same drawing**:

1. **The world object** — an iso prop at 32 px/m, where the *entire* job is that the silhouette says
   *"that's a calendar"* / *"that's a phone"* at 4–13 px. No text survives here. Nothing on the face
   reads. Shape and ramp carry the whole identification, exactly as the fuel brief's §1 asks of gas vs
   diesel.
2. **The readable face** — a procedural screen/page rig at native canvas resolution, drawn when the
   player interacts and the object enlarges. This is where every glyph, ruled line and digit lives.

**This straddles your two rig families, and that is the unusual thing about this brief.** Your catalogue
splits cleanly today: iso props (`bucketRig`, `fishToteRig`, `shovelIsoRig` — 32 px/m, facings, pivots)
and UI screens (`watchRig`, `navRig`, `depthRig` — native canvas, stateless `render(opts)`,
`drawUnit`/`layout`). **Each device here needs one of each**, and they must agree about what the object
is. Treat them as a pair from the start rather than drawing a face and discovering later that its
silhouette doesn't read.

> **Precedent worth copying exactly:** the shipped instruments already solve the two-scale problem, and
> the engine-side rule is *"the same texture at two rects, so they cannot disagree"*
> (`HelmInstrumentExpansion`, ADR 0025 S4.5). Your side of that is: **one face rig, drawn into two
> different boxes** — which is what `drawUnit(ctx, X, Y, WW, HH, o)` + `layout(...)` already exist for.
> Don't author a "small version" of a face.

---

## 2. The pieces, in priority order

### 2.1 CALENDAR — do this one first. It is unblocked and it is the cheapest win in the doc.

A wall calendar — in the cottage, the general store, the harbourmaster's office. You walk up to it and
read it. Both halves of its *interaction* already ship in the engine, so art is the only thing missing.

**What the face has to carry** (all of it already exists as game state — no new maths anywhere):

| Read | Source | Why it's on the page |
|---|---|---|
| Weekday · day of season · season · year | `IGameClock.Weekday` / `DayOfSeason` / `Season` / `Year` | the basic read |
| **Market Day** | `IGameClock.IsMarketDay` | the town's week has a market day; it is a reason to plan (**P3**) |
| Rest day | the same weekday model | the town's rhythm, learned from the wall |
| ⭐ **Moon phase, and the spring/neap band** | `moonAge` / `springNeap01` (`time-tides-weather.md` §3.3) | **the hero read** — see below |
| Season boundaries | 4 × 28 days | when the water changes |
| *(later)* build/refit completion, contract deadlines, festivals | stored as `(nodeId, completeOnGameDate)` | turns it into a planning surface |

> **⭐ The moon strip is the hero, and it is the reason this object serves P1.** Canon: *"Spring tides
> are flagged by the moon phase"*, and range swells to springs at **new AND full** — twice a month, not
> once. A player who reads that off the wall has learned to plan a flats trip a week out, which is the
> single most valuable piece of tide literacy in the game.
>
> Draw it so the **doubling is visible**: two spring peaks per 28-day cycle, at new and full, with the
> neaps at the quarters. A naive moon strip that just waxes and wanes once will teach the player the
> wrong thing. **Shape and position must carry it, not colour** — the accessibility rule below is not
> optional on this element.

**Character.** A working-coast wall calendar: a printed sheet on a nail or a bulldog clip, curled at the
corner, pencil marks on it. Not a desk planner, not a Victorian almanac (the tide *almanac* is already a
separate shipped object — a warm-stock ruled page in aged ink — and the calendar should read as a
**different, cheaper, more everyday thing** than that page, or the two will muddle).

**Season names: use the canon ones in full.** `Early Spring · High Summer · The Turn · Hard Winter`
(canon §5.8, and `Core/Time/Calendar.cs`). A calendar has room a watch face doesn't. **⚠ Note the
existing `watchRig` README prints `Spring/Summer/Fall/Winter` — that is not canon; don't propagate it
from there** (see §5.5).

**Sketch of the contract** (yours to shape — this is the data that exists, not a demand):

```js
CalendarRig.render({ dow, date, season, year, market, rest, moonAge, springNeap, night })
```

**Blocked on:** nothing. R8 (is the calendar free furniture or bought?) changes how the player *gets*
it, not what it looks like.
**Serves:** P1 (moon → springs → planning) · P3 (market and rest day) · P5 (planning is what keeps the
teeth fair).

---

### 2.2 NOTEBOOK — the shell is unblocked; the tab set waits on a ruling

The player's own notebook: what people have asked of you, and what you've been taught. **It is a thing
the player writes into** — that is what makes it legal under the ratified law that knowledge lives in
things and people. **A pre-printed strategy guide would break the very doctrine it is meant to serve**,
so the art has to look *written*, not *published*: pencil and ink over ruled stock, uneven baselines,
things crossed out, a corner turned down.

**What to draw now (unblocked):**

- The **closed object** as an iso prop — pocket notebook, softcover, elastic or string, weathered.
- The **open spread** as the readable face: ruled or squared stock, a left/right page, a margin.
- **Tabs** as a physical affordance — stubs along the fore-edge, hand-lettered, some of them clearly
  added later. The tab *mechanism* is unblocked even though the tab *list* isn't.
- **Handwriting as a texture**, at a size that reads. This is the hardest pixel-art problem in the
  brief after the phone screen — a convincing "handwritten" look that survives no-anti-aliasing at a
  readable size. If it can't be done at the enlarged size, say so early and propose the fallback
  (a hand-set letterform that merely *feels* written).
- **A done state.** A closed task should look struck through, not deleted.

**What waits:** which tabs exist, and whether they arrive earned or pre-printed (R9). If pages are
earned — the recommendation — then **a mostly-empty notebook is the normal early-game state**, and it
must look intentional rather than broken. That is a real art requirement, and it is the interesting one.

**Blocked on:** R9 for the tab list only. Shell, spread, tabs-as-affordance, handwriting: go.
**Serves:** P3 (people give the tasks and teach the pages) · P4 (the record of what you earned) · P2.

---

### 2.3 PHONE — ⚠️ do NOT author a finished phone. Do a taste board.

**The blocker is real and it is upstream of every pixel: which era of phone is this?** A flip phone with
three apps and a 96×64 screen and a smartphone are not the same object, the same silhouette, the same
UI grammar, or the same fantasy. Nothing about the phone can be drawn until R6 is ruled.

**So the most useful thing you can deliver is the thing that lets the owner rule it.** This project
steers by taste surfaces, so:

> **Deliverable: 2–3 phone eras, side by side, each shown as (a) the ≈4 px in-hand silhouette and
> (b) one readable face — the same app on each, so the comparison is honest.**

Suggested candidates, but your call:

- **A candybar/flip with a small colour LCD** — cheap, rugged, salt-proof, reads as a working person's
  phone. The small screen is a *constraint that helps*: an app that has to fit 96×64 is forced to be a
  single honest read, which is very much this game's grammar.
- **An early touchscreen smartphone** — more app surface, more modern, more risk of reading as a menu.
- *(optional third)* **A rugged/industrial handset** — the marine-supply-catalogue object.

**The app to draw on all of them, for the comparison:** the **tide chart**. It is the app with the most
information density, it has a shipped page to be consistent with (the almanac), and if it works at an
era's screen size the others will.

**⚠️ Two hard constraints on the phone specifically:**

1. **Every wordmark and every icon must be original.** Your UI README already commits to this — *"every
   brand wordmark on them is original (not lifted from a real maker)"* — and a phone is the single most
   brand-associated object you will ever draw. It must not read as any real handset. Invent the maker.
2. **The screen must not become a menu with a bezel.** The design doc names this as the direction's main
   risk. Grammar that helps: few, large, physical-feeling elements; app faces that look like
   *instruments* (the sounder, the plotter, the watch), not like a settings list.

**What the phone eventually carries** (for context; do not design these until R6 lands): world map
(a position fix, *not* the full survey), tide chart, boats-for-sale, properties-for-sale.
**There is deliberately no weather app** — forecast stays with the barometer, the harbourmaster and the
radio, and adding one would delete a pillar mechanic. Don't draw one.

**Blocked on:** R6 for the finished object. The **taste board is the unblocking work** and needs no
ruling.
**Serves:** P3 (people who don't answer) · P2/P4 (the rung above you, priced) · P1 (coverage; earned apps).

---

### 2.4 COMPUTER — defer

M3 work, furthest out, and it carries the same unresolved era question as the phone (CRT on a desk vs a
flat panel). **Don't start it.** When it comes, it is *two* objects: **yours** (the management desk) and
**theirs** (the buyer's office, the harbourmaster's berth book) — and the second is much cheaper,
because a computer you don't own is a document with a screen.

---

## 3. Technical

Standard contract for the iso props — same as every prop kit you have shipped:

- **32 px = 1 m.** Fixed ¾ iso, elev 40°, 45° steps. Transparent background, no anti-aliasing,
  upper-left key light, ordered dither.
- **No keyline** (ADR 0031) — silhouette carried by the form's own dark side. These are small, pale-ish,
  rectangular objects against walls and hands, which is exactly the case that tempts an outline. It
  still doesn't get one; if a form can't hold its edge, the form needs work.
- Keep depth-edge darkening (>0.30 m apart in depth, far side darkened).
- **Ramps only** (ADR 0015 palette guard-rail). Pin by **pivot**, never by corners.
- **Directionality is your call** — a wall calendar hangs flat and may need very few facings; a phone in
  a hand may need none at all (`shellfishRig` is the precedent for a prop with no camera). **State
  which you chose in the README**, and if directional, azimuth gets measured from pixels at bake time,
  never read off a declaration (see `docs/art/rigs/README.md` § "THE AZIMUTH SPLIT").
- ⭐ **Export `F`, `MATS`, `GAIN`, `BIAS`, `LN`** — free on a new rig, and it keeps the mesh extractor
  shim-free.

For the readable faces, follow the **UI rig** conventions in `docs/art/rigs/ui/README.md` — they already
fit this job exactly:

- `render(opts) → HTMLCanvasElement`, **stateless**: same opts, same sprite. The game owns the state.
- Also expose **`drawUnit(ctx, X, Y, WW, HH, o)`** and **`layout(X, Y, WW, HH)`** so one face can be
  drawn into two rects (§1) and yield hit-boxes for anything pressable.
- **Night is a parameter, not a separate rig.**
- Procedural art only — no external image assets. `watchRig` is the closest precedent in the catalogue
  and the best one to read first.
- Preview state to `localStorage['hh.*']`, preview-only.
- Each folder standalone: its own `*.dc.html`, `support.js`, `Art/*.js`.

**Accessibility is not optional on these faces** (`ux-and-mobile-controls.md` §8, which the diegetic
doctrine confirms applies to *every* instrument readout): **redundant coding — shape + icon + text,
never colour alone.** The shipped almanac page is the standard to match: every tide turn carries a
glyph *and* the words *and* a time *and* a height, with colour as the fourth channel. **The calendar's
moon/spring strip is the element this bites hardest on.**

---

## 4. Deliverables

1. **The rigs** — your lane, `docs/art/rigs/`. Faces belong under `docs/art/rigs/ui/<name>/` in the
   standalone-folder shape the other thirteen use; iso props go in the usual place. One rig per device
   with the world-object and the face as a pair; your call whether that is one file or two.
2. **A README per rig**, house format — coordinates first, then the parts, then the param table (the
   `watch-face/README.md` shape is the model).
3. **A gameplay sidecar** if any of these need mount or carry anchors (a phone in a hand, a calendar on
   a wall) — schema in `docs/art/rigs/gameplay/`.
4. **Baked sheets** per the usual pipeline for the iso props. The faces are live-rendered, not baked.
5. For the phone: **the taste board** (§2.3), as proofs — `docs/art/proofs/` is where the lever strip
   went.

Branch `art/<short-desc>`, one concern per PR, `.github/pull_request_template.md`. Say in the PR body
which builders the owner must re-run after merge.

**Suggested PR split:** calendar first (unblocked, self-contained, immediately useful) · notebook shell
second · phone taste board third. Don't bundle them.

---

## 5. Open questions — what is actually gating you

The design doc's §10 carries twelve rulings. **Three of them gate art**, and the rest don't:

1. **R6 — phone era and price.** Flip phone or smartphone? **Hard blocker on the phone** (§2.3). Your
   taste board is what unblocks it.
2. **R9 — are the notebook's help pages earned or always present?** Gates the **tab list only**, not the
   notebook's shell. If earned (recommended), a mostly-empty early notebook is normal and must look
   deliberate.
3. **R8 — is the wall calendar free furniture or bought?** Affects how the player gets it, not how it
   looks. **Not a blocker.**

And two things worth knowing before you read the neighbouring rigs:

4. **⚠️ The `watch-face/README.md` time canon is stale.** It states `SecondsPerDay = 1200` (20 real
   minutes/day) and *"market day is tunable (preview = Sat)"*. Both were superseded: a day **ships at
   1800 s / 30 real minutes** (ruled 2026-08-01, canon §5.5) and **market day is Friday**. The rig
   itself takes these as parameters so nothing is broken — but **don't copy that README's canon block
   into a new one.** The current values are verified in `diegetic-instruments-and-consoles.md` §3.
5. **The same README's `SEASON_FULL = ['Spring','Summer','Fall','Winter']` is not canon** either. The
   canon names, which the code enum uses, are **Early Spring · High Summer · The Turn · Hard Winter**
   (§5.8, `Core/Time/Calendar.cs`). Abbreviating on a tiny watch face is a fair compromise; a calendar
   should print them properly.

Anything else — coverage, gating, what an app costs, who answers the phone — is design-side and does
not gate a single pixel.
