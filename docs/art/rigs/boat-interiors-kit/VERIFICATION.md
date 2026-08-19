# Coordinator gate report — boat-interiors drop (2026-08-19)

Drop: `Pixel art capabilitiesboatinteriors.zip`, received 2026-08-19, landed here VERBATIM
(all files LF as shipped; nothing normalised, nothing edited). This file is the §2 gate
result the intake handoff demanded — read it before building anything from this kit.

## What the drop is

RIG-RENDERED interiors: NO pre-rendered sheets. One renderer (`boatInteriorRig.js`,
101,528 B, LF sha256 `3297674f4802c366…`), 27 per-hull interior sidecars (9 families +
18 lobster variants), merged gameplay sidecars, and REVISED copies of nine exterior hull
rigs (they now publish HOUSE/loft/DOOR geometry the interiors are measured against).

## Gate finding 1 — every bundled hull rig DIFFERS from main, by design (claimed)

All nine `hull-rigs/*.js` hash differently from `docs/art/rigs/*.js` at main. The
MANIFEST claims these are additive revisions (publishing the house/loft/door). CLAIMED,
NOT VERIFIED — verifying it per-hull is the intake's S0 (below).

## Gate finding 2 — the drop's own provenance stamps are inconsistent THREE ways

`_supersedes` (the manifest's stale-merge detector, meant to carry the PREVIOUS hull-rig
sha) behaves differently across the nine families:

| hull | `_supersedes` | meaning |
|---|---|---|
| sportFisherIsoRig2 (both tops) | = main's CURRENT rig | ✅ cut against current main |
| sideDragger, sternTrawler, sternTrawlerMk2 | = the ZIP'S OWN new rig | self-referential; baseline UNKNOWN |
| capeIslanderIsoRig, lobsterBoatIsoRig | = the rigs at commit `4f048395` (2026-07-19) | ⚠️ measured against MONTH-OLD lofts — both were on #571's divergent-rig list and main has moved since |
| coastalPacket, tanker | ABSENT | no provenance at all |
| lobsterBoatVariants ×18 | NOT CHECKED | same family as lobsterBoat — assume suspect until measured |

## What this means (the rule for the intake)

A stale baseline is fatal ONLY if main's drift since that baseline touches the
loft/house/door geometry the interior was measured against. (Known example the other
way: `lobsterBoatIsoRig`'s drift includes the 12-scheme paint axis — colours, not loft.)
So: NO interior def may be built for a hull until its baseline is adjudicated —
diff main's rig against the claimed/discovered baseline, classify every changed line as
loft/house/door-touching or not, and REFUSE (flag upstream for re-measure) any hull
where geometry the sidecar measured has moved. The sportFisher2 pair is clean now.

## Upstream asks (§1) scorecard

Per-rig sha mechanism: PRESENT (two fields per sidecar) but discipline broken (above) ·
door as declaration: PRESENT (8-frame baked cue, ~70 ms/frame, reversed on exit) ·
interiors ride the hull's `rock(i)` on the same camera basis: STATED (answers the
architecture handoff's motion question in the direction the proposal favoured) ·
cell/pivot contract + anchors: in README §1–§7 — the intake verifies against the actual
JS, README-lies pattern is three-for-three · tanker at 16 px/m: a SECOND pixel grid
inside one kit — never conflate with the 32 px/m hulls.

## Re-export gate (2026-08-19 evening — the drop above is SUPERSEDED in place by this)

`Pixel art capabilitiesboatinteriorsreexport.zip`, verified before landing:

- **Renderer**: `boatInteriorRig.js` LF sha256 `34bb7813…` — the generator moved (the
  cuddy-branch `mechanism` fix was made at the source and everything regenerated).
- **Finding 1 CLOSED**: all 27 `.interior.json` pin `derivedFromRigSha256` = `34bb7813…`
  (27/27, zero strays — the `560aa92e…` mixed-hash pin is gone).
- **Gameplay provenance coherent**: all 27 gameplay sidecars pin their own bundled hull
  rig by exact LF sha (27/27 verified computationally, not by reading the README).
- **Finding 3 (companionway ruling) APPLIED**: no stair id remains in any INTERACT
  section (the one textual match is a `_note` pointing AT its STAIRS entry);
  `mechanism` now binds throughout.
- **Hull rigs BYTE-IDENTICAL to the first drop, all nine** — the re-export changed
  sidecars, renderer and docs only. The cape remains FORKED-RIG per the intake ledger;
  her merge is upstream work under the ruled acceptance bar (see PR #589's comments).

The S0 ledger's 24 `REFUSED-PIN` verdicts may now be re-adjudicated AGAINST THESE
FILES — which is the intake lane's job, on this branch, not a report-flip.
