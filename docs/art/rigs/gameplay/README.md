# Gameplay sidecars — `DECK` / `WASHBOARD` / `CLEATS` as data

One file per **hull**. Authored by **art-director** (this folder is that role's lane, see
`agents/art-director.md`); consumed by the deck sidecar importer and the deck-boarding/mooring
work (M2-37..39).

**Naming, and where the rig name comes from.** The original eleven are named mechanically from
their rig (`lobsterBoatIsoRig.js` → `lobsterBoatIsoRig.gameplay.json`), which works while a rig
makes exactly one boat. `lobsterBoatVariantsIsoRig.js` makes **eighteen**, and eighteen sidecars
cannot share one file name — so a sidecar is named for its HULL
(`lobsterStandardHardtopFundyIso.gameplay.json`) and **the rig is read from the file's own `rig`
field**, which every sidecar has always carried. For the eleven the two agree, so nothing about
them changed. Reading the field is also the safer form: before this, a sidecar could name rig A
in its JSON while being hashed against rig B off its file name, and nothing checked that they
agreed (`DeckSidecarReader.ResolveRigFileName`).

The file stem is not cosmetic — it is the input to the importer's naming: strip a trailing
`Rig`, capitalise, and that is the `BoatDeckDef` asset file; snake_case it and that is the def's
id. `LobsterVariantFleetTests.EveryDerivedName_RoundTripsThroughTheImportersOwnConventions`
pins the round-trip.

**Generated, not authored, where the rig ships a generator.** The eighteen lobster variants are
extracted from the rig's own `gameplayGeometry(variant)` by *Hidden Harbours ▸ Dev ▸ Generate
the 18 lobster-variant deck sidecars* — every number derived from the same `resolve(v)` the mesh
bake reads, so a reshape cannot leave them stale. That tool also stamps `derivedFromRigSha256`,
which this rig's generator omits; see that field below for why an absent hash is a refusal and
not a warning. **Do not hand-edit those eighteen — re-generate.**

**These files are read by the game now** (M2-37's data half). `Hidden Harbours ▸ Dev ▸
Import Deck Sidecars` turns each one into a `BoatDeckDef` asset under
`Assets/_Project/Data/Boats/Decks/`, wired onto that hull's `BoatVisualDef.Deck`; the
on-deck player is clamped to those polygons instead of the one-size greybox rectangle
(`Assets/_Project/Code/Tools/Editor/RigBaking/DeckSidecarImporter.cs`). Nothing is
transcribed by hand, and `DeckSidecarImportParityTests` re-reads every file here in CI and
fails if a committed Def has drifted from it. **Edit a sidecar → re-run the importer.**
What the runtime uses today: `DECK` (walked), `CLEATS` (carried as data, no rope gameplay
yet), `WASHBOARD` (imported and tagged, but NOT walkable until the Space climb of M2-37
lands — a washboard is somewhere you climb onto).

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
  (art-director charter rule 2). The importer enforces this: a mismatch is a loud refusal
  and that hull keeps the greybox rectangle — it never silently imports polygons cut from a
  different boat.
  - ⚠️ **Eight of the eleven SHAs below are CRLF hashes** (cape, packet, lobster, sideDragger,
    sportSkiff, both sternTrawlers, tanker) — they were derived from working copies with
    Windows line endings, and the repo stores the rigs with LF. The importer accepts a match
    on either convention and says so on every run, because a line ending cannot move a
    vertex; every cited line number and constant in those files still reads exactly as
    written. **art-director: please re-stamp those eight from the committed files** so the
    check goes back to exact. dory / punt / console already hash exactly.
  - ⚠️ **That re-stamp is blocked on the IMPORTER, not on the art — measured 2026-08-14, and the
    ask above stands.** It is not a one-file edit: the SHA is recorded in **two** places. Each
    committed deck asset carries its own `DerivedFromRigSha256`
    (`Assets/_Project/Data/Boats/Decks/*.asset`), written at import from the sidecar's value
    (`DeckSidecarImporter.cs:154`), and `DeckSidecarImportParityTests` asserts the two are
    **string-equal** (`DeckSidecarImportParityTests.cs:93`). All eleven sidecars currently agree
    with their assets, so re-stamping the eight reds exactly those eight until *Hidden Harbours ▸
    Dev ▸ Import Deck Sidecars* is re-run and the eight assets re-committed **in the same PR**.
    Note which assert fires: the staleness check (`EverySidecarStillDescribesTheRigItNames`) would
    still pass, because it accepts the line-ending match — the failure lands on the *provenance*
    assert instead. **So the re-stamp needs an editor session; a session without Unity must not
    attempt it.** Geometry is unaffected either way — a line ending cannot move a vertex.
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
  previously flagged); sportSkiff foredeck `DROP = 0.07` (sportSkiffIsoRig.js:147) — **not**
  0.05. The `DROP = 0.045` at sportSkiffIsoRig.js:222 is the bimini canvas skirt, a canvas
  detail — ignore it. **The console skiff's `DROP = 0.07` trap is RETIRED** (2026-07-25):
  her pass-2 rig has no `DROP` at all — the foredeck is flush at the sheer,
  `fz(u) = station(u).kz + station(u).dep − 0.012` (consoleIsoRig.js:272). Her bow cleat `z`
  moved 0.989 → 1.047 in the same pass.
- The tanker's bow-rake weighting is `0.25 + 0.75·frac` (tankerIsoRig.js:85); the rest of
  the fleet uses `0.30 + 0.70·frac`.
- coastalPacket, sternTrawlerMk2 and tanker apply `flareExp` inside their deck-width
  functions; sternTrawler (mk1) and sideDragger do not.

## Current set (11 single-hull rigs + the lobster generator's 18)

| hull | LOA | DECK | WASHBOARD | CLEATS |
|---|---|---|---|---|
| dory | 4.5 m | bilge floor (3d) | — open boat | stem-head ring bolt + port-quarter ring (modelled, 2026-07-25) |
| punt | 5.2 m | bilge floor (3d) | — open boat | stem-head ring bolt + port-quarter ring (modelled, 2026-07-25) |
| console | 7.0 m | cockpit sole | — open skiff | bow cleat |
| sportSkiff | 7.0 m | cockpit sole | — open skiff | bow cleat |
| capeIslander | 12.8 m | cockpit + whaleback foredeck (3d) | both sides, 0.42 m (full run to the foredeck; narrows past the house) | bow bitt + 2 stern |
| lobsterBoat | 12.0 m | cockpit + foredeck (3d) | both sides, 0.44 m (full length) | bow samson + 2 stern |
| sideDragger | 25 m | aft deck, house alleys, working deck, foc'sle (3d) | — bulwarks | samson + 2 bow + 2 quarter bollards |
| sternTrawler | 38 m | ramp strips, trawl deck, house alleys, foc'sle (3d) | — bulwarks | samson + 2 bow + 2 quarter bollards |
| sternTrawlerMk2 | 38 m | ramp strips, trawl deck, house alleys, foc'sle (3d) | — bulwarks | samson + 2 bow + 2 quarter bollards |
| coastalPacket | 60 m | aft deck, house alleys, hold walkways, gaps, foc'sle (3d) | — merchant rails | 2 bow + 2 quarter bollards |
| tanker | 110 m | poop, alleys, tank lanes, fore deck, catwalk, foc'sle (3d) | — bulwarks | centre bitt + 2 bow + 2 quarter + 2 stern bollards |

### The lobster generator's eighteen (2026-08-15, ADR 0022 phase 8)

`lobsterBoatVariantsIsoRig.js` → **3 sizes × 2 styles × 3 regions**, one sidecar each, all
generated. Every cell has the same section shape — cockpit (flat, 24 pts) + foredeck (3d, 14
pts), both washboards, 3 cleats (bow samson + 2 stern) — and differs in the numbers:

| size | LOA | sole (`DECK`) | washboard width, nor / fundy / nfld |
|---|---|---|---|
| inshore | 8.6 m | 0.44 m | 0.352 / 0.376 / 0.424 m |
| standard | 12.0 m | 0.50 m | 0.44 / 0.47 / 0.53 m |
| offshore | 14.6 m | 0.55 m | 0.502 / 0.536 / 0.604 m |

Style changes the roof and the arch, never the planking — measured: `open` and `hardtop` of one
size+region report identical hull metrics, which is why the flotation table gives them one row
(9 hull rows, 18 hulls). Their meshes are still distinct, because deck and anchors differ.

⚠ `standard/*/northumberland` **is** the shipped `lobsterBoatIsoRig.js` hull: offsets table
byte-identical, same `L` / `DECK` / `RAKE`. Her flotation is the shipped boat's 0.50/0.50/2.50,
and `FleetFlotationTableTests.TheAnchor_MatchesTheCommittedLobsterBoat` holds the two together.

## Other handoff sidecars

`FisherRodMount.json` (fishing rig kit, 2026-07-22; regenerated by the rod-continuity fix) — a second
*kind* of sidecar: frame-by-frame rod mount data (grip px per dir/frame in the character's 64×88 body
cell, `behindDirs` = [7,0,1] for the rod-under-body facings, a pose curve per rod state) so the engine
can pin the rod overlay without running the JS rigs. Units are **cell pixels**, not metres — it
describes bake cells, not hull geometry, so the coordinate-frame section above does not apply to it.
It carries `derivedFromRigSha256` as a map over its two source rigs (`characterIsoRig.js`,
`rodIsoRig.js`); the same staleness rule applies — hash mismatch means the rig moved and the mount
table must be re-exported before anything trusts it.

**Generated, not authored** — `node tools/rig-recipes/fisher-rod-mount.mjs` writes it, `--check`
fails if the committed file has drifted. Do not hand-edit it.

⚠️ **What it used to say, and why that mattered.** Until the rod-continuity fix this file described
`hold`, `cast_short` and `cast_long` as three overlays with three different cells (71×68, 107×91,
109×95) and three different rod pivots ((36,56), (54,56), (55,56)). Three cells and three pivots is
three rods, and a reader following it would pin the cast rod somewhere the held rod never was — the
document half of the defect the rig fix ended. It now states **one** `rodCell`, **one** `rodPivot`
and one `blankLenM` per tier, with every state as a pose curve on that one rod: the seven the
character rig drives, then the three rests. `cast_short`/`cast_long` are gone; they described the
retired `cast` power variants, and the flick-cast ships on `castBack`/`castRelease`.

Foredeck boardability on lobsterBoat/capeIslander is owner-ruled (boardable). **All ten
`_confirm` items from the original drop were ruled by the owner on 2026-07-22** and are
recorded in place as `_ruled`/`_ruled_items` — no open `_confirm` remains. The one rig
change ordered by those rulings (cape washboards run to the foredeck) is applied and the
cape sidecar re-derived. Two art additions were **banked, not ordered**: packet/tanker
midship breast bollards, and tanker catwalk end-ladders.

⚠ **One of those rulings has been reopened by art and needs the owner again.** The 2026-07-22
ruling on the dory's and punt's bow painter was "the data-only painter point stands, **NO
visible ring/fitting is added to the art**". The small-craft rig kit v2 (2026-07-25) models a
visible iron ring bolt at that point on **both** hulls, plus a second port-quarter ring, and
moves the tie-point itself (dory `station(1.0)` → `station(0.965)`, punt `station(1.0)` →
`station(0.962)`). The original judgment text is kept verbatim in both sidecars' `_ruled`
with the reversal appended — provenance, not deletion. **Owner: re-rule.**
