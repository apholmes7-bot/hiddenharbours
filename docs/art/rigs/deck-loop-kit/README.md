# Hidden Harbours — Deck Loop Kit

The three deck pages and everything they load: the working furniture, the crew clips that operate
it, and the twelve-beat shift the two hands run on a real hull.

Nothing here needs a build step. The rigs are plain browser JS that render `ImageData` you can blit,
bake to sheets, or run live; the sidecars are plain JSON. The three `.dc.html` pages are **viewers** —
they are how the art was reviewed, not the deliverable.

```
deck-loop-kit/
├─ README.md              ← this file
├─ Deck Gear.dc.html      ← the furniture, the deck plan, the seven-step loop
├─ Deck Work.dc.html      ← the six crew clips against the furniture (pass 6.3)
├─ Deck Loop.dc.html      ← the whole shift: twelve beats, two hands, two hulls, eight headings
├─ support.js             ← page runtime (needed only by the three pages)
└─ Art/
   ├─ isoSolid.js             → globalThis.IsoSolid       (the turntable — load 1st, always)
   ├─ eyeIsoRig.js            → EyeIso                    ┐ character chain,
   ├─ headIsoRig3.js          → HeadIso3 / HeadIso        │ in this order
   ├─ characterIsoRig6.js     → CharacterIso6             ┘
   ├─ deckGearRig.js          → DeckGear                  (the five pieces of furniture)
   ├─ trapIsoRig.js           → TrapIso
   ├─ trapFaunaRig.js         → TrapCatch
   ├─ trayIsoRig.js           → FishTray2
   ├─ fishToteRig.js          → FishTote
   ├─ buoyIsoRig.js           → BuoyIso
   ├─ lobsterRig.js / rockCrabRig.js / shellfishRig.js / fishIsoRig.js
   ├─ crustaceanRig.js → Crustacean · catchKit.js → CatchKit
   ├─ lobsterBoatIsoRig.js    → LobsterBoatIso
   ├─ capeIslanderIsoRig.js   → CapeIslanderIso
   └─ gameplay/
      ├─ README.md                            ← the sidecar contract (frame, fields, conventions)
      ├─ lobsterBoatIsoRig.gameplay.json
      └─ capeIslanderIsoRig.gameplay.json
```

**Load order matters** — `isoSolid.js` first (every rig lathes against it), then the character chain
(the body delegates the head, the head delegates the eye), then anything else in any order. Each page
already lists its own set in its `<helmet>`; copy that block if you are wiring a new page.

---

## The two cell contracts

| | furniture (`DeckGear`) | crew (`CharacterIso6`) |
|---|---|---|
| Cell | **80 × 76** | **64 × 92** |
| Pivot | **(40, 60)** — ground under the centre | **(32, 82)** — ground contact |
| Scale | 32 px = 1 m | 32 px = 1 m |
| Facings | 8, `N NE E SE S SW W NW` (`dir` 0…7, CW) | same |
| Camera | 3/4, elev **40°** | same |
| Edges | binary alpha, ordered dither at band edges, no AA | same |

Same turntable, camera and light as the fleet, so furniture, crew, pots, trays and hull composite
without adjustment. Furniture is **ringless** (ADR 0031); `{keyline:true}` is kept as the A/B.

---

## The furniture — `DeckGear`

```js
DeckGear.render(kind, dir, opts)   // -> Uint8ClampedArray RGBA
DeckGear.surface(kind, dir)        // -> where a THING lands on it (TOPS)
DeckGear.station(kind, dir, opts)  // -> where a PERSON stands to work it
```

| kind | label | footprint l × w × h (m) | top (m) | station clip | work z (m) | clear (m) |
|---|---|---|---|---|---|---|
| `baitbin` | salt-bait barrel | 0.64 × 0.64 × 0.88 | 0.88 | `bench` | 0.865 | 0.58 × 0.56 |
| `baitbox` | bait bin | 0.78 × 0.56 × 0.48 | 0.47 | `bench` | 0.470 | 0.58 × 0.56 |
| `chopboard` | chopping board | 0.72 × 0.50 × 0.82 | 0.775 | `chop` | 0.775 | 0.58 × 0.56 |
| `bandingtable` | banding bench | 1.00 × 0.58 × 0.90 | 0.885 | `bench` | 0.885 | 0.62 × 0.58 |
| `haulerstation` | hauler + washboard | 1.50 × 0.62 × 1.36 | 0.93 | `hauler` | 1.05 | 0.66 × 0.60 |

`station()` is the other half of the crew contract: it returns where to stand, which way to face,
what `workZ` to pass the clip, and how much deck a planner must reserve for the operator **before**
it packs pots. The hauler is the one station whose operator faces **outboard** — the warp comes in
over the rail and the drum is worked from inboard of it. Its `sheave` is at `[-0.40, 0.06, 1.31]`.

---

## The crew clips — `CharacterIso6`, pass 6.3

Append-only on 6.2: six clips and a fifth carry stance, nothing else in `pose()` moved.

| anim | frames | ms | one-shot | works |
|---|---:|---:|---|---|
| `hauler` | 8 | 115 | — | the drum, the davit, the warp coming aboard |
| `bench` | 10 | 130 | — | banding, gutting, emptying — any two-handed bench task |
| `chop` | 8 | 105 | — | bait off the chopping board |
| `lift` | 8 | 105 | ✓ | taking a load off a surface |
| `place` | 8 | 110 | ✓ | putting a load down on one |
| `toss` | 8 | 95 | ✓ | sending a pot back over the rail |

Carry stance `pot` joins `buckets`, `tray`, `helm`, `oars`.

**The work height is a world metre (`opts.workZ`), never a body fraction** — a bench belongs to the
deck, not to the person standing at it. The clip clamps `workZ` to the figure's own reach and reports
when it bit. Feed it from `DeckGear.station()`; never guess it, and never scale it by build.

---

## The shift — the twelve beats

`Deck Loop` runs one tick table at **115 ms**, 118 ticks ≈ 13.6 s a pot. Two hands: **skipper**
starboard at the hauler (nine of the twelve beats), **sternman** port at the bench and the board.

| # | beat | ticks | skipper | sternman | the pot |
|--:|---|--:|---|---|---|
| 1 | `GAFF` | 10 | hauler | walks stack → bench | in the water, warp on the drum |
| 2 | `HAUL` | 10 | hauler | bench | rising up the side |
| 3 | `LAND` | 8 | place @ cap | bench | onto the washboard cap |
| 4 | `EMPTY` | 10 | bench @ cap | walks bench → chop | on the cap |
| 5 | `CULL` | 8 | lift @ cap | chop | on the cap |
| 6 | `PASS` | 12 | walks, carrying tray | chop | on the cap |
| 7 | `TIP` | 8 | place @ bench | chop | on the cap |
| 8 | `BAIT` | 12 | walks back, carrying tray | walks chop → hauler | on the cap |
| 9 | `REBAIT` | 10 | bench @ cap | place @ cap | bait bag in, door closed |
| 10 | `LIFT` | 8 | lift @ cap | walks to wait | off the cap |
| 11 | `MOVE` | 12 | walks aft on the `pot` carry | idle | carried |
| 12 | `SET` | 10 | place @ stack | toss | stacked aft, one back over |

Every position in the table is **boat-local metres** (`+x` starboard, `+y` bow, `+z` off the keel), so
the swell moves crew and gear *with* the hull instead of sliding them across it. A second hull is a
row in `LAY`, not a second page — the lobster boat and the Cape Islander already differ only by sole
height, station y's and transom.

`Deck Gear`'s own seven-step summary (HAUL → LAND → BAND → GRADE → STOW → BAIT → SET) is the same
loop stated as furniture rather than as frames.

---

## The sidecars

`Art/gameplay/*.gameplay.json` carry the gameplay geometry the art rigs do not model — walkable deck
polygons, washboard strips, cleats — in hull-local metres. **`Art/gameplay/README.md` is the
contract**: read it before consuming them. Two rules that bite first:

- `derivedFromRigSha256` is the drift tripwire. If the rig's hash no longer matches, the hull was
  reshaped and the sidecar must be re-checked.
- An absent section is data, not a gap. A hull with no `WASHBOARD` block is a hull you cannot climb a
  washboard onto. Never invent one to fill it.

Keep the filenames identical on both sides (`Art/gameplay/` here, `docs/art/rigs/gameplay/` in the
game repo) so import stays a pure copy.

## Importing into the game

The rigs are the art director's verbatim source — copy, never edit. Tuned render constants
(`F`, `MATS`, `GAIN`, `BIAS`, `LN`) stay in the rig and reach the engine via the export. The sidecars
are the gameplay-owned layer and are the only files here meant to be authored against.

## Running the three pages

They want a **local web server** and a **live network connection** — the page runtime pulls React and
Babel from unpkg, the type comes from Google Fonts, and the deck plans are `fetch`ed from
`Art/gameplay/`, which `file://` will refuse.

```
cd deck-loop-kit && python3 -m http.server 8000
# then open http://localhost:8000/Deck%20Gear.dc.html
```

Opened straight off the disk the pages fall back gracefully — the deck-plan panels go empty — but the
sprite stages still need the runtime, so the server is the short path. Each page has download buttons
that bake its contact sheets to PNG: `Traps.png`, `Buoys.png`, `FishTrays.png`, `DeckGear.png`,
`DeckWork.png`, and `DeckLoop-<hull>.png` (12 beats × 8 headings).

## Known open

- `toss` is authored for a pot going over the rail; a light object thrown flat is not the same arc.
- The hauler clip is the drum only — the gaff itself is not authored, and beat 1 reads as a reach.
- Both hulls' beat table is one shift on flat-to-moderate swell. Nothing in it degrades for weather.
- Furniture is ringless per ADR 0031; if a keyline pass is ever wanted, `{keyline:true}` is still
  live on every kind and re-bakes the A/B.
