# Deck Loop Kit — import record (2026-08-09)

What arrived in the owner's drop, what was taken, what was **refused**, and why. Everything below
is measured, not read off the drop's README. Reproduce with:

```
node docs/art/rigs/deck-loop-kit/_verify.js
```

## The drop

`Pixel_art_capabilities_deckloopkit.zip` → `export/deck-loop-kit/`: 19 rig `.js`, a page runtime
(`support.js`), three `.dc.html` viewer pages, a kit README, and two `Art/gameplay/*.gameplay.json`
sidecars. No pre-rendered PNGs, no catalogue JSON.

## Byte-diff against the repo — the first question, before any measurement

| file | verdict | action |
|---|---|---|
| `isoSolid.js` | **NEW** — the shared turntable every rig lathes against | imported |
| `deckGearRig.js` | **NEW** — `DeckGear`, five pieces of working furniture | imported |
| `trapIsoRig.js` | **NEW** — `TrapIso`, four builds | imported |
| `trapFaunaRig.js` | **NEW** — `TrapFauna` **and** `TrapCatch` (README names only `TrapCatch`) | imported |
| `trayIsoRig.js` | **NEW** — `FishTray2` | imported |
| `buoyIsoRig.js` | **NEW** — `BuoyIso`, 8 fleet schemes | imported |
| `support.js` | **NEW** — page runtime, needed only by the three viewer pages | imported |
| `characterIsoRig6.js` | **REVISED** 6.2 → 6.3 | imported **in place** at `docs/art/rigs/` |
| `capeIslanderIsoRig.js` | **REVISED — but backwards.** See below. | **REFUSED** |
| `catchKit.js` `crustaceanRig.js` `eyeIsoRig.js` `fishIsoRig.js` `fishToteRig.js` `headIsoRig3.js` `lobsterRig.js` `rockCrabRig.js` `shellfishRig.js` `lobsterBoatIsoRig.js` | **byte-identical** to the repo copies | not re-imported (no-op) |
| `Art/gameplay/capeIslanderIsoRig.gameplay.json` | differs — **older** than the repo's | **REFUSED** |
| `Art/gameplay/lobsterBoatIsoRig.gameplay.json` | differs — **older** than the repo's | **REFUSED** |

Ten of nineteen rigs are byte-identical, so this drop is much smaller than its file count suggests.

## Why the Cape Islander rig and both sidecars were refused

**`capeIslanderIsoRig.js` reverts an owner ruling.** The repo's copy draws washboards along the
**full sheer**, transom → foredeck bulkhead, with the inner edge clamped clear of the house wall:

```js
// ... along the FULL sheer, transom -> foredeck bulkhead (owner 2026-07-22:
// "capes washboards go all the way to foredeck").
const xin=(st)=>side*(st.y>HY0-0.05 ? Math.max(st.ws-TH-WB, HX+0.02) : st.ws-TH-WB);
```

The drop's copy stops them at the house front (`if(station(u0).y > HY0-0.05) continue;`) — the
pre-ruling behaviour. The drop's README does not mention revising this hull at all; it lists the
file only as a page dependency. This is the hazard `docs/art/rigs/README.md` names in its own
words: *"Fixes belong upstream, or the next drop silently reverts them."*

The refusal costs nothing, because the revision touches **only** that washboard block:
`haulerMount`, `helmSeat`, `tubMounts`, `navMounts`, `HAULER`, `TUBS` and `HELM` are all **verified
identical** between the two copies. There is no anchor in the drop that the repo does not already
have.

**Both sidecars are older than the repo's.** They carry open `_confirm` entries that the repo's
copies have already closed as `_ruled` (owner, 2026-07-22), and they drop the cockpit-obstruction
`_notes` (engine box, exhaust stack) and the `_excluded` reasoning entirely. Importing them would
re-open settled questions and lose data.

## The sha stamps are fine — and the convention differs between the two sides

Worth recording because it looks like drift and is not. The repo's sidecars stamp
`derivedFromRigSha256` as the **CRLF** hash of the rig; the files on disk are LF, so a naive
`sha256sum` disagrees and looks stale:

| rig | LF sha (disk) | CRLF sha = the committed stamp |
|---|---|---|
| `capeIslanderIsoRig.js` | `47cedd4b…` | `fe9130fe…` ✔ matches sidecar |
| `lobsterBoatIsoRig.js` | `d8bb0caa…` | `fd3ab95c…` ✔ matches sidecar |

`DeckSidecarImportParityTests` accepts either ("exactly, or once line endings are normalised"), so
the tripwire is armed and currently **green**. The drop's sidecars stamp the **LF** hash instead —
a convention difference, not a drift signal. **No re-stamping was required or done.**

## `characterIsoRig6.js` 6.3 — the append-only claim, checked

The drop calls 6.3 "append-only on 6.2: six clips and a fifth carry stance, nothing else in
`pose()` moved." #469 drives the `haul` clip through `CharacterClipPlayer`, so a silent change here
would move shipped animation. Verified rather than trusted:

- 18 pre-6.3 clips × 8 facings × every frame = **1184 frames, all byte-identical** between 6.2 and 6.3.
- Shared-clip metadata (`frames`, `ms`, `oneShot`) drift: **0**.
- Clips removed: **none**. Added: `hauler bench chop lift place toss`. Carry stance added: `pot`.

The claim holds. #469's haul clip is provably untouched.

*(One internal inconsistency, surfaced as data: the kit README's table lists **six** new clips, while
the rig's own inline comment calls them "Five clips" and then names six. The count is six.)*

## Handedness — measured, and the README is right

The drop declares "8 facings, `N NE E SE S SW W NW`, `dir` 0…7, **CW**". The repo's boats are
CounterClockwise and `docs/art/rigs/README.md` records that a declared order has shipped wrong five
times, so this was measured against the repo's **own** registered-CCW reference, in one harness
with one sign convention (+X ground-plane bearing, depth un-squashed by `/sin 40°`):

| family | +X ground step | +Y at `dir` 2 | verdict |
|---|---:|---|---|
| `iso-rig-pack/utility-iso` (registered CounterClockwise) | **+45°** | screen-WEST | CounterClockwise |
| `deck-loop-kit` (IsoSolid turntable) | **−45°** | screen-EAST | **Clockwise** |

Opposite signs. **The kit is Clockwise and the drop README is correct** — the outcome nobody should
have assumed. A baker that applies the fleet's CCW correction here mirrors all eight cells of every
piece.

⚠️ The kit's **screen** mean is −46.75°/step, numerically identical to the figure the iso-rig-pack
contracts record for their *CounterClockwise* rigs. That is because the screen mean is an
alternating, foreshortened quantity (that pack's `VERIFICATION.md` §6) and **is not a handedness
test**. Only the un-squashed ground-plane bearing is. Two families can share a screen mean and turn
opposite ways; these do.

This kit is therefore the **first Clockwise-measured family in the repo** other than the two the art
director fixed at source. It rides its own turntable (`isoSolid.js`) rather than the fleet's.

## Measurements a README cannot give you

- **Cell rule** (recovered, then key-by-key verified): pivot-**inclusive** union of the **ink** bbox
  across all facings on the fixed `nativeW × nativeH` buffer, seeded at the pivot. Same rule as
  `wharfDecorRig` / `utilityIsoRig`. 24/24 committed cells reproduce exactly.
- **Nothing is a mirror pair.** Best horizontal-mirror agreement across `d` vs `8−d` is 97.1%
  (`chopboard`); the hauler station is 81.5%. All eight facings are real art — mirror is recorded
  per artwork, never as a family property.
- **What barely turns**, so a packer may collapse it: `baitbin` 98.6%, and all eight buoys
  98.8–99.0% agreement with `dir` 0. `haulerstation` is the most directional piece at 71.9%.
  `crabCone` reads rotationally symmetric in silhouette but measures 92.7% — not collapsible.
- **`TrapCatch` is DOM-bound** (`document.createElement`/`ImageData`): bake through
  `TrapFauna.render`, not the façade. It also **delegates unknown kinds to `CatchKit` and returns
  `null` when CatchKit is absent** — so a bake that forgets to load `catchKit.js` +
  `crustaceanRig.js` produces empty mixes silently rather than failing.
- **`TrapFauna.render(kind, opts)` takes no `dir`.** The catch inside a pot is not directional; the
  contract records `facings: 1` rather than omitting the axis, because the absence is the data.
- **Determinism**: 150 renders across two cold V8 hosts, byte-identical.

## Per-hull data the kit already carries (Phase B consumes this)

`TrapIso.CAPS` — stack limits, **in the rig**, not a game constant:

| hull | deck | washboard |
|---|---:|---:|
| `lobsterBoat` | 5 | 2 |
| `capeIslander` | 3 | 2 |
| `wharf` | 6 | 0 |

`DeckGear.station(kind, dir)` is the crew contract: where to stand, which `dir` to face (`turn`),
the **world-metre** `workZ` to hand the clip, and the deck a planner must reserve (`clear`). The
hauler is the one station whose operator faces **outboard** (`turn: 4`), sheave at
`[-0.40, 0.06, 1.31]` m.

Hull-local hauler mounts, read from the repo rigs (metres, +x starboard, +y bow, +z up):

| hull | `HAULER` |
|---|---|
| `lobsterBoatIsoRig` | `x 1.34, y −1.50, z 1.44` |
| `capeIslanderIsoRig` | `x 1.30, y −2.56, z 1.592` |

## The three `.dc.html` pages

Imported as the catalogue, per kit convention. They are **viewers** (the drop README says so), and
they expect ten sibling rigs at `Art/` that already live at `docs/art/rigs/*.js`. Those are
**deliberately not duplicated** — a second copy of a byte-identical rig is exactly the drift the
repo's no-edit rule exists to prevent. To run a page locally, materialise the siblings first:

```
cd docs/art/rigs/deck-loop-kit
for f in catchKit crustaceanRig eyeIsoRig fishIsoRig fishToteRig headIsoRig3 \
         lobsterRig rockCrabRig shellfishRig lobsterBoatIsoRig capeIslanderIsoRig; do
  cp ../$f.js Art/; done
python3 -m http.server 8000        # pages also need network: React/Babel from unpkg
```

`Art/` carries a `.gitignore` for exactly those filenames so a materialised copy cannot be
committed by accident.

## Notes and TODOs found inside the drop — surfaced, not acted on

From the kit README's own "Known open" (these are the art director's words, recorded as data):

- `toss` is authored for a pot going over the rail; a light object thrown flat is not that arc.
- The hauler clip is **the drum only** — the gaff is not authored, and beat 1 reads as a reach.
- Both hulls' twelve-beat table is one shift on flat-to-moderate swell; **nothing in it degrades
  for weather**. (P1 "the sea has moods" will eventually want a say here.)
- Furniture is ringless per ADR 0031; `{keyline:true}` is still live on every kind as the A/B.

## Not in this PR

No baked sheets — the coordinator bakes after merge-review. No `nativeDirs` was trusted anywhere:
facings come from the contract.
