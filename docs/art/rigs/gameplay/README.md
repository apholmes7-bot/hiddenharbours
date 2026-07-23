# Gameplay sidecars — `DECK` / `WASHBOARD` / `CLEATS` as data

`<rigBasename>.gameplay.json` — one per pilotable hull, named mechanically from its rig
(`lobsterBoatIsoRig.js` → `lobsterBoatIsoRig.gameplay.json`). Authored by **art-director**
(this folder is that role's lane, see `agents/art-director.md`); consumed — eventually — by
the rig extractor and the deck-boarding/mooring work (M2-37..39). As of this PR the
extractor does **not** read these files yet; this folder establishes the contract and the
content.

## Extractor resolution (per section)

rig export → sidecar → **absent section = the hull does not support that feature** (not an
error). Never invent a section to fill a gap.

## Coordinate frame

- **Units: metres.** Scale 32 px = 1 m (`frame.scale_px_per_m`; the tanker bakes at 16 px/m
  — its sidecar says so, the metres don't change).
- **Hull-local**, the rig's own object space: origin at **amidships, keel bottom,
  centreline**; axes **+x starboard, +y bow, +z up**. This matches the rig math directly:
  `y = -L/2 + u·L` (station parameter u: 0 = stern, 1 = bow), `x = side·halfwidth`
  (`side = +1` starboard), `z` up from the keel.
- **Heading-independent.** The CW/CCW facing-order convention of the bakes (boats ccw=TRUE)
  is about sprite-cell labelling and never touches these numbers.
- Each file carries a `frame` block stating all of the above.

## Fields

- `derivedFromRigSha256` — SHA-256 of the exact rig source file the values were derived
  from. **Hash mismatch = the hull was reshaped and the sidecar must be re-checked** before
  anything trusts it. Update the SHA in the same PR as any rig geometry change
  (art-director charter rule 2).
- `DECK` — array of walkable polygons, each with an `id` and
  `winding: "ccw_from_above"` (counter-clockwise when viewed from +z).
  - `polygon` + `z` for FLAT areas: `[x, y]` pairs, height in the single `z` field.
  - `polygon3d` for areas whose height varies (sheer-following foredecks, bilge floors):
    `[x, y, z]` triples, no separate `z`.
- `WASHBOARD` — one entry per side. The authored side carries `width_m` (walkable width
  measured inboard from the outer edge) and `outer_edge` (a polyline of `[x, y, z]` along
  the sheer/cap). The opposite side is written as
  `{ "side": "starboard", "mirror": "port across x=0" }`. **Omitted entirely on open
  boats** — the omission is recorded in `_excluded` so it reads as a decision.
- `CLEATS` — array of `{ id, type, pos: [x, y, z], provenance }`. Ids follow
  `bow_1`, `stern_port`, `stern_star`, `mid_port`, `mid_star`, `quarter_port`… — port/star
  suffixes for pairs, numbered ids on the centreline. `type` is descriptive and honest for
  the hull (`cleat`, `samson_post`, `bow_bitt`, `bollard`, `bitt`, `painter`). A samson
  post or bitt counts as a tie-off; **hauling gear does not**. `pos` is the modelled
  fitting's **box centre** as built by the rig. Omit the section if the hull has no
  modelled tie-off (none of the current eleven needed that).
- `provenance` / `_derivation` — cites the rig line(s) the value came from; `exact` means
  read verbatim, otherwise the formula used is written out. Every derived value names the
  rig constants (`station()`, `dfrac`, `dw`, `WB`, `DROP`, …) it was computed from.
- `_confirm` — the value stands, but it rests on a design judgment not yet ruled; one line
  of context so the owner can rule from the PR page.
- `_ruled` / `_ruled_items` — a former `_confirm` the owner has ruled on. The annotation keeps
  the original judgment text verbatim ("was _confirm: …") and appends the ruling, `(owner,
  <date>)`, and one line of rationale. The judgment record is never deleted — provenance is
  the point. (`_confirm_items` arrays become `_ruled_items` the same way.)
- `_excluded` — deliberate omissions, recorded so reviewers see decisions, not silent gaps.
- `_notes` — non-normative context (obstruction footprints on the deck polygons, etc.).
  Deck polygons do **not** carry holes for deck furniture; obstructions are listed per
  polygon in `_note`/`_notes` and their game-side treatment is **ruled** (owner, 2026-07-22):
  **game-side colliders**, sourced from these obstruction notes, for M2-37..39 — never
  authored holes.

## Derivation discipline

Every coordinate in these files is **computed from the rig's own math** (station tables,
`lerp`, `dfrac`, offset constants), never eyeballed from a bake. The known traps, so they
stay known:

- lobsterBoat & capeIslander foredeck `DROP = 0.05` (capeIslanderIsoRig.js:198 — verified,
  previously flagged); sportSkiff & console foredeck `DROP = 0.07`
  (sportSkiffIsoRig.js:147, consoleIsoRig.js:154) — **not** 0.05. The `DROP = 0.045` at
  sportSkiffIsoRig.js:222 is the bimini canvas skirt, a canvas detail — ignore it.
- The tanker's bow-rake weighting is `0.25 + 0.75·frac` (tankerIsoRig.js:85); the rest of
  the fleet uses `0.30 + 0.70·frac`.
- coastalPacket, sternTrawlerMk2 and tanker apply `flareExp` inside their deck-width
  functions; sternTrawler (mk1) and sideDragger do not.

## Current set (11 hulls)

| hull | LOA | DECK | WASHBOARD | CLEATS |
|---|---|---|---|---|
| dory | 4.5 m | bilge floor (3d) | — open boat | bow painter (data-only, ruled) |
| punt | 5.2 m | bilge floor (3d) | — open boat | bow painter (data-only, ruled) |
| console | 7.0 m | cockpit sole | — open skiff | bow cleat |
| sportSkiff | 7.0 m | cockpit sole | — open skiff | bow cleat |
| capeIslander | 12.8 m | cockpit + whaleback foredeck (3d) | both sides, 0.42 m (full run to the foredeck; narrows past the house) | bow bitt + 2 stern |
| lobsterBoat | 12.0 m | cockpit + foredeck (3d) | both sides, 0.44 m (full length) | bow samson + 2 stern |
| sideDragger | 25 m | aft deck, house alleys, working deck, foc'sle (3d) | — bulwarks | samson + 2 bow + 2 quarter bollards |
| sternTrawler | 38 m | ramp strips, trawl deck, house alleys, foc'sle (3d) | — bulwarks | samson + 2 bow + 2 quarter bollards |
| sternTrawlerMk2 | 38 m | ramp strips, trawl deck, house alleys, foc'sle (3d) | — bulwarks | samson + 2 bow + 2 quarter bollards |
| coastalPacket | 60 m | aft deck, house alleys, hold walkways, gaps, foc'sle (3d) | — merchant rails | 2 bow + 2 quarter bollards |
| tanker | 110 m | poop, alleys, tank lanes, fore deck, catwalk, foc'sle (3d) | — bulwarks | centre bitt + 2 bow + 2 quarter + 2 stern bollards |

## Other handoff sidecars

`FisherRodMount.json` (fishing rig kit, 2026-07-22) — a second *kind* of sidecar: frame-by-frame rod
mount data (grip px per dir/frame in the character's 64×88 body cell, `behindDirs` = [7,0,1] for the
rod-under-body facings, pose sub-ranges for hold/cast/reel) so the engine can pin the rod overlay
without running the JS rigs. Units are **cell pixels**, not metres — it describes bake cells, not hull
geometry, so the coordinate-frame section above does not apply to it. It carries
`derivedFromRigSha256` as a map over its two source rigs (`characterIsoRig.js`, `rodIsoRig.js`);
the same staleness rule applies — hash mismatch means the rig moved and the mount table must be
re-exported before anything trusts it.

Foredeck boardability on lobsterBoat/capeIslander is owner-ruled (boardable). **All ten
`_confirm` items from the original drop were ruled by the owner on 2026-07-22** and are
recorded in place as `_ruled`/`_ruled_items` — no open `_confirm` remains. The one rig
change ordered by those rulings (cape washboards run to the foredeck) is applied and the
cape sidecar re-derived. Two art additions were **banked, not ordered**: packet/tanker
midship breast bollards, and tanker catwalk end-ladders.
