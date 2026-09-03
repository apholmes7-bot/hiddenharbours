# Boat cutaway kit — batch 5 (complete set), 2026-09-02

The full SECTION-composite kit in one drop: batch 3 (the rule set + composite, eight tagged hulls) and
batch 4 (sport fisher tagging, rule refinements 1b/2b/3b/5b, raked-front rooms, the dragger's boat-deck
bulwark) merged. Every file is byte-identical to its primary in `Art/` (sportFisherIsoRig2 to
`export/sport-fisher-rig-kit/`). Nothing here depends on batches 1–4 — replace, don't overlay.

## Contents

    README.md
    boatCutawayRig.js                 pass 2 — rules 1–5 + refinements 1b / 2b / 3b / 5b
    boatInteriorRig.js                v18 — rgba.dep out; raked-front side walls + sole; seam-free corners
    hull-rigs/lobsterBoatIsoRig.js
    hull-rigs/lobsterBoatVariantsIsoRig.js
    hull-rigs/capeIslanderIsoRig.js
    hull-rigs/sideDraggerIsoRig.js    boat-deck bulwark plated (sea showed through the pipe rail on S/SW/E/NE)
    hull-rigs/sternTrawlerIsoRig.js
    hull-rigs/sternTrawlerMk2IsoRig.js
    hull-rigs/coastalPacketIsoRig.js
    hull-rigs/tankerIsoRig.js
    hull-rigs/sportFisherIsoRig2.js   pass 3 — lv tags, geometry(), dep, cutaway hook (both hulls, 53 + 90)

All nine hulls: per-face level tags (`lv`), `geometry()` publishing soleZ + ceilingZ per level,
`rgba.dep` out of render(), the `cullLevels` reference cut, and the guarded `cutaway` hook.

## The rule set (what the engine mirrors, in this order)

1. **LEVEL** — the level's faces, its lid clipped to the level's footprint (±0.05 m: the foredeck
   forward of the cuddy and the trawl deck aft of the below-deck flat stay), and every level STACKED
   OVER it (cullAbove — a higher level clear of the footprint in plan stays, e.g. the 53's flybridge
   while the below flat is cut).
2. **RIGGING** — rigging whose lowest vertex stood on a vanished lid (z ≥ ceiling − 0.35 within the
   footprint) goes with it; every level lifted by cullAbove contributes its roof to the lid list, so the
   90's tower and the dragger's radar drop too. `rigging:'keep'` is the old floating behaviour.
   Gantries, gallows and masts on open decks are untouched.
3. **BITE** — the hull's near side(s) (outward normal toward the camera in plan) is sectioned alongside
   the footprint down to soleZ + sill (0.60 m): the shell becomes the knee-high stub wall of the
   dollhouse cut. The stub's top and camera-facing ends get a light section CAP (house paint, dark
   rim) — emitted only where the shell actually reached the cut plane, so ship bridges and the
   skylounge grow no floating cap strip.
4. **DEPTH** — exterior and room merge per PIXEL by depth (both carry `rgba.dep`). Across a depth step
   > 0.30 m the far pixel takes the hull's key colour, so the outline continues along the cut.
5. **THROUGH** — camera square on an end of the room (N/S; `through:'all'` admits the diagonals) and an
   enclosed room (ceilingZ set) on that end: that room's enclosure + lid go too and its sheet is
   composited as well — one sectioned model, not a hole.

## API

    E.render(dir, { cutaway:{ level, sill, bite, rigging, cullAbove, cap, through } })   // sectioned exterior; rgba.dep
    BoatInterior.render(dir, opts)                                                      // rgba.dep added; otherwise unchanged
    BoatCutaway.composite(dir, { hull, level, doorOpen, night, focus, roll, pitch, heave, variant, cutaway })
      → Uint8ClampedArray (hull cell) with .dep, .src (0 sky · 1 hull · 2 room), .levels (rooms painted)
    BoatCutaway.plan(E, opts)            // the resolved cut: set, footprint, near sides, through, lids, zCut
    BoatCutaway.filter(faces, E, opts)   // the face-list transform each hull's render() calls
    BoatCutaway.DEFAULTS

Defaults: `{ sill:0.60, bite:true, rigging:'cull', cullAbove:true, cap:true, edge:'key', capW:0.12, through:'end' }`.

Two of these are taste calls still open on the QA board (`Boat Visual QA.dc.html` and
`Boat Interiors & Doors.dc.html` both expose them as toggles): the **sill** height and whether **rigging**
drops with the roof (`'cull'`) or stays as deck gear (`'keep'`). The kit ships the current defaults;
pass the override in `opts.cutaway` or change `DEFAULTS` when the call is made.

## Byte discipline

No `opts.cutaway` → every hull renders byte-identical to its pre-kit version. `rgba.dep` is an expando
property on the returned array, invisible to every existing consumer. Interior sidecars keep their pins:
no loft / HOUSE input moved.

## Adjudicated, not changed

- Lobster hardtop aft corner (91 px see-through): the open side of the shelter aft of the wheelhouse — correct.
- Sport skiff bow (1–7 px): the bow rail's own see-through at the stem, not the T-top legs — correct.
  (`sportSkiffIsoRig.js` is an open boat with no rooms and is not part of this kit.)
- Dragger / trawler / packet SECTION "leaks" of 30–300 px: sky through gallows frames and mast stays whose
  backdrop was the culled wheelhouse — open rigging, detector semantics.
- Sport 53 / 90 room DRIFT (300–1100 px): the interior's straight walls vs the exterior's rounded plan and
  raked nose — authored abstraction, cosmetic, flagged POLISH for boatInteriorRig.

## QA

`Boat Visual QA.dc.html` (rows EXTERIOR / SECTION V2 / PROXY V1 / CULL ONLY / ROOM per facing, the five
rules as toggles, leak / drift / sky detectors, findings list) and `Boat Interiors & Doors.dc.html`
both load exactly these versions.

LF sha256 (CRs stripped):

    boatCutawayRig.js               44911b397b76d7244defa27f13ae22b0ffd2e4f8e7a91936cbd6560613c928db
    boatInteriorRig.js              c28c147cb0b8ce3ceb1f8f40ab25bbf1e990188f309deb5bbc0020bc42f3b74c
    lobsterBoatIsoRig.js            98f5cff508232371d2d2cb1ec71d3c93cdfbf2a54e8279d7ced87d5006a75606
    lobsterBoatVariantsIsoRig.js    ed8d0e7a43c9e8ee68f4dfc0d48919e1e162b749252a86d193d86cdaa4772795
    capeIslanderIsoRig.js           009775b481758f9802515fb205af2716c7f12ba87d46f392d50804d7cd3257db
    sideDraggerIsoRig.js            4ca48880a8427fdbc72439d36766cc2f205d9edf6b9b6297a082b12b254650f9
    sternTrawlerIsoRig.js           a1deec112f97675a6a669f2c8a6fdfbc7aca3eadd6f918ca784f3b5c4ee0989b
    sternTrawlerMk2IsoRig.js        0b6db2293ef96ddb007f4d786512f22bf22c8e715de14cdfce477a044bc96929
    coastalPacketIsoRig.js          0bcb68c4e26fbdc4a9e4f5b6fb3b5abb6c462436bf2c40cf2cb51b88ce1c11d9
    tankerIsoRig.js                 f31207abfd71de080d4ece0c9a4cee423ed6ac82f5fb533c15211fc1c6a97217
    sportFisherIsoRig2.js           521eb6c29153117dc15e8b8bc160cc3f00822010d33168ddee9d2c64780bf9a6
