# Boat cutaway kit — batch 1 (per-level CEILINGS + face→level TAGS), 2026-08-26

Answers the lead-architect ask for the owner-ruled CUTAWAY COMPOSITE. Batch 1 per the invited
order: the **lobster** (your render-verified reference) plus the **trawler + packet** (the
shared-sole tie pair). Batch 2 — dragger, trawler Mk II, tanker, all 18 lobster variants — is
queued behind this import lane; same mechanism, no new semantics.

RIG SOURCES, not bakes: `hull-rigs/` ×3, revision-bumped to **PASS 3**. Extract in-engine as
before. The committed adjudication lives in this repo at `qa/Boat Cutaway QA.dc.html`, which
re-instantiates the pristine pass-2 sources (`qa/cutaway-baseline/`) beside pass 3 and diffs.

## ASK A — `geometry()` beside `render()`

One record per WALKABLE level: `{ id, deck, soleZ, ceilingZ, ceiling }`, DECLARED from the same
constants the mesh is built from — never re-measured off the mesh (your two derivations failed
for exactly that reason; the cuddy's law, `sheerZ(y)-0.16`, was only ever knowable by declaration).

- `ceiling.kind: 'hard' | 'raked' | 'open'`. Raked ceilings publish `zAft/zFwd` and put the
  honest MINIMUM in `ceilingZ` (the lobster cuddy: 2.014 at the companionway → 2.484 at the bow).
- **Open sky is explicit** — `ceilingZ: null` + `ceiling:{kind:'open'}`. An absent field and an
  open sky never look the same in this data (your relay-contract rider, honoured).
- **Partial covers are declared as covers, not ceilings**: the lobster's extended hardtop over the
  forward cockpit only (`z 2.955, y -1.55..0.55`), the packet's bridge-wing underside over the
  side-deck walkways (`z 10.0, y -21.6..-19.8`).
- **THE TIE, broken by data**: trawler `main_deck`+`house_sole` share z 3.50 — house publishes
  ceiling 6.50, main_deck publishes OPEN. Packet: shared z 5.00 — house 7.60 vs OPEN. Each ship's
  `geometry().tieBreak` states it in-file.

| hull | level (deck id) | soleZ | ceiling |
|---|---|---|---|
| lobster | house (house_sole) | 0.50 | hard 2.90 — the eave the interior dresses |
| lobster | cuddy (cuddy_sole) | 0.24 | raked 2.014 → 2.484 (foredeck underside) |
| lobster | cockpit (cockpit) | 0.50 | OPEN · partial hardtop 2.955 over y −1.55..0.55 |
| lobster | foredeck (foredeck) | raked 2.124→2.740 | OPEN |
| trawler | house (house_sole) | 3.50 | hard 6.50 ← ties with main_deck, broken |
| trawler | bridge (bridge_sole) | 6.56 | hard 8.95 |
| trawler | below (below_sole) | 1.15 | hard 3.38 (main-deck underside) |
| trawler | main_deck (main_deck) | 3.50 | OPEN — the gantry is rigging, not a ceiling |
| packet | house (house_sole) | 5.00 | hard 7.60 ← ties with main_deck, broken |
| packet | bridge (bridge_sole) | 10.00 | hard 12.50 |
| packet | below (below_sole) | 2.20 | hard 4.80 (main-deck underside) |
| packet | main_deck (main_deck) | 5.00 | OPEN · partial bridge-wing cover declared |

**The 59-vs-53 count, reconciled.** 53 was interior levels only across the 24 in-engine hulls;
your corrected 59 also counts the exterior decks the sheets never bake — the five ships'
`main_deck` + the tanker's `poop_deck`. `geometry()` publishes those exterior records too (with
explicit opens), and on the lobster family we additionally publish `cockpit`/`foredeck` records
beyond the 59 because the ceilings ask bites there (hardtop partial cover; the foredeck IS the
cuddy's lid). Batch 1 carries 10 of the 59: lobster 2 (+2 extra records), trawler 4, packet 4.

## ASK B — every face DECLARES its level

Per-face `lv` on the face objects (beside your `db`), stamped by an **authoring cursor**: each
build section states its level before emitting — declaration in the source, zero derivation.
`geometry().ids` is the shared int table for the TexCoord1.x bake (identical vocabulary across
the ships: hull 0 · main_deck 1 · house 2 · bridge 3 · below 4 · rigging 5; lobster: hull 0 ·
cockpit 1 · foredeck 2 · house 3 · cuddy 4 · rigging 5).

Semantics, per your two rules:
- **Standing-on**: a deckhouse wall belongs to the room it encloses, not the deck it stands on.
  Boat decks, their rails, ladders, liferafts and funnels are the house's lid and go with `house`;
  the door leaf (`doorFaces(opts)`) is house enclosure and cuts with the room; the packet's
  dressed L2 block is tagged `house` and flagged in `geometry().dressed`.
- **Rigging is a DEDICATED CLASS** (your option B, as agreed): lobster arch/dome/aerials; trawler
  gantry, warps, radar mast, stays; packet foremast, derrick, deck crane, shrouds, roof gear.
  A cut can never take a spar with a room — presentation stays yours.
- `hull` = the exterior silhouette (shell, bulwarks, washboards, rail caps, foc's'les, the
  trawler's stern-ramp cut) — never culled: the room shows inside the hull's own silhouette.

Access: `faces()` returns the static tagged mesh; `doorFaces(opts)` the posed leaf;
`render(dir,{cullLevels:['house']})` is a REFERENCE cut for adjudication only.

## Byte-discipline receipt (the reach-kit law)

`qa/Boat Cutaway QA.dc.html` — ALL CHECKS PASS, failCount 0 (2026-08-26):
face streams byte-equal outside `lv` (v/mat/b/db, count and order) · door leaf equal at
doorOpen 0 and 1 · **0 differing pixels** across 5+3+2 facings × door 0/1 per hull · anchors
byte-equal (helm, door, nav, tubs, hauler/gantry/crane) · every face tagged · every level
ceilinged or explicitly open · both ties broken.

LF sha256 (CRs stripped):

    lobsterBoatIsoRig.js    pass 3  5bac264142fe7006c57f0431c6077f708bbfa196d21d6dc3697b0e402e87636d
                            pass 2  77a2e16f1c2f18ebc5e8ac784fce258e6ef838d3a05a781b602cf8958f2c4729
    sternTrawlerIsoRig.js   pass 3  8e1adb556be751d41fea196fd795298a99ea7af17e9568c74e0b260888b1578f
                            pass 2  3f306e419d071a41644ec91133efd067fca6db3fa37fe4af8032ca1a006a5e9e
    coastalPacketIsoRig.js  pass 3  145c3cc3c974e7ad6cc298b203ef860c048f5597fe6c6031c6b9b5e1ec68e11c
                            pass 2  ba4119da06269e967096b0e3853c0cd79a31017f9a4ef435d2ef5d39faf445b0

The lobster pass-2 sha equals the `hullRigSha256` pin in her interior sidecar — the baselines are
the canonical parents, not reconstructions. **Interior sidecars keep their pass-2 pins for now**:
every loft/HOUSE input the rooms were measured from is byte-identical in pass 3 (proven above), so
the pins truthfully name the measured parent. Re-stamping the 3 affected sidecars + gameplay
merges to the pass-3 shas is a provenance-only no-op we'll run on your call — same argument as
the tranche-4 Correction 1 re-stamp.

## Riders settled in this drop

1. **Sport-fisher template stamp — DONE.** Both sport sidecars' placeholder
   `STAMP_AT_EXPORT_LF_SHA256_OF_sportFisherIsoRig2.js` substituted with the real LF sha256 of the
   kit's `sportFisherIsoRig2.js`: `ebc77bace833361b578f5315e175e10de61d1acf77b5037390217ceb09221bcb`
   — which equals the bare-stem pin already in those files (internally consistent). No geometry
   touched. Your fails-by-design suite should now clear REFUSED-PIN on both hulls. The two
   corrected sidecars SHIP IN THIS KIT under `sidecars/` (byte-identical to the boat-interiors
   kit copies, which carry the same fix).
2. **`scene/rigs.all.js` — REGENERATED** from current `Art/` sources. 9 of 51 sections were
   stale and were replaced (incl. the flagged 6.2-era `characterIsoRig6.js` body → current 6.6:
   houseIsoRig, wharfBuildingRig, shopfrontRig, lobsterBoatIsoRig, capeIslanderIsoRig,
   sideDraggerIsoRig, sternTrawlerIsoRig, lobsterBoatVariantsIsoRig, characterIsoRig6); the other
   42 verified byte-identical and untouched. Note the bundle's capeIslander section carries OUR
   pass-2 branch, as `Art/` does — the cape merge is unaffected.
3. **Cape rig merge — deliberately NOT folded in.** It stays its own drop under the agreed bar
   (byte-identity outside the aft-doorway bbox, full identity bow-on); mixing it into this drop
   would muddy both "nothing else moved" checks.

## Layout

    README.md                       this receipt
    hull-rigs/lobsterBoatIsoRig.js      pass 3 (reference hull)
    hull-rigs/sternTrawlerIsoRig.js     pass 3 (tie half 1)
    hull-rigs/coastalPacketIsoRig.js    pass 3 (tie half 2)
    sidecars/sportFisherIsoRig2.convertible.interior.json   rider 1 — re-stamped, no geometry
    sidecars/sportFisherIsoRig2.skybridge.interior.json      rider 1 — re-stamped, no geometry
