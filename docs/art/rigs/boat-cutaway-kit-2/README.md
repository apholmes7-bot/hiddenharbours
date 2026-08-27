# Boat cutaway kit — batch 2 (per-level CEILINGS + face→level TAGS), 2026-08-26

The batch your batch-1 README queued at line 5, formalized by HANDOFF 2: **dragger, trawler Mk II,
tanker, all 18 lobster variants** — same mechanism as batch 1, no new semantics (one new id:
`poop_deck:6`, the tanker's second exterior deck, extending the fleet table per ship as the lobster
family did). RIG SOURCES, not bakes: `hull-rigs/` ×4, revision-bumped. Extract in-engine as before:
`faces()` (or `faces(v)`) + `F.concat(doorFaces({doorOpen:0}))`. The committed adjudication lives at
`qa/Boat Cutaway QA 2.dc.html`, which re-instantiates the pristine base sources
(`qa/cutaway-baseline/`) beside the revisions and diffs — **ALL CHECKS PASS, failCount 0**
(2026-08-26).

## HULLS

| hull | rig | global | revision | door | notes |
|---|---|---|---|---|---|
| side dragger | sideDraggerIsoRig.js | SideDraggerIso | pass 3 | hinged, fwd wall | tie: main_deck+house z 2.05 |
| stern trawler Mk II | sternTrawlerMk2IsoRig.js | SternTrawlerMk2Iso | pass 3 | hinged, aft wall | tie: main_deck+house z 3.50 (same as Mk I; LAYOUT.trawler2 shares the arrangement) |
| gas tanker | tankerIsoRig.js | TankerIso | pass 3 | hinged, aft wall at poop | TWO exterior records (main_deck + poop_deck); tie: poop_deck+house z 11.60 |
| lobster variants ×18 | lobsterBoatVariantsIsoRig.js | LobsterBoatVariantsIso | pass 5 | sliding, aft wall (all 18) | ONE variantAware global — **no `byId` pick needed**: boatInteriorRig resolves via `interiorEnv(variant)`, unlike the sport fishers' byId route. `geometry(v)/faces(v)/doorFaces(opts)/render(dir,opts)` all take the EXTERIOR spelling: top-level `{size,style,region}`. The nested `variant:{…}` spelling belongs to the interior sidecars (boatInteriorRig's layer) — the wrong shape silently renders the default boat, so mind the two. |

## ASK A — `geometry()` beside `render()`

One record per WALKABLE level, `{ id, deck, soleZ, ceilingZ, ceiling }`, DECLARED from the build
constants — never re-measured off the mesh. Open sky is explicit (`ceilingZ:null` +
`ceiling:{kind:'open'}`); partial covers are declared as covers, not ceilings; shared-sole ties are
broken in-file via `geometry().tieBreak`.

| hull | level (deck id) | soleZ | ceiling |
|---|---|---|---|
| dragger | house (house_sole) | 2.05 | hard 4.50 ← ties with main_deck, broken |
| dragger | bridge (bridge_sole) | 4.56 | hard 6.60 |
| dragger | below (below_sole) | 0.35 | hard 1.95 (main-deck underside) |
| dragger | main_deck (main_deck) | 2.05 | OPEN — mast, derricks, gallows over it are rigging |
| trawler Mk II | house (house_sole) | 3.50 | hard 6.50 ← ties with main_deck, broken |
| trawler Mk II | bridge (bridge_sole) | 6.56 | hard 8.95 (the flared sides are walls, not lid) |
| trawler Mk II | below (below_sole) | 1.15 | hard 3.38 (main-deck underside) |
| trawler Mk II | main_deck (main_deck) | 3.50 | OPEN — the stern gantry is rigging, not a ceiling |
| tanker | house (house_sole) | 11.60 | hard 14.30 ← ties with poop_deck, broken |
| tanker | bridge (bridge_sole) | 19.70 | hard 22.50 |
| tanker | below (below_sole) | 9.00 | hard 11.35 (poop-deck underside) |
| tanker | main_deck (main_deck) | 8.60 | OPEN · partial catwalk-underside cover z 13.765, x −0.78..0.78, y −21.3..30.3 |
| tanker | poop_deck (poop_deck) | 11.60 | OPEN · partial bridge-wing cover z 19.7, y −35.6..−33.5, outboard of the house wall |
| variants ×18 | house (house_sole) | DECK (0.50·dK) | hard at the eave (houseOf(v).eaveZ) ← ties with cockpit, broken |
| variants ×18 | cuddy (cuddy_sole) | houseOf(v).cuddy.soleZ | raked sheerZ(y)−0.16 (the fleet liner law); honest minimum at the companionway |
| variants ×18 | cockpit (cockpit) | DECK | OPEN · hardtops declare the cantilever underside as a partial cover; open boats are pure sky |
| variants ×18 | foredeck (foredeck) | raked, sheer−0.05·dK | OPEN |

**The 59-count, closed.** Batch 1 carried 10 of your corrected 59; batch 2 carries the other 49:
dragger 4 + Mk II 4 + tanker 5 (its poop_deck included) = 13 ship records, plus the 18 variants'
house+cuddy = 36 interior records. 10 + 13 + 36 = 59 — every level on the count now publishes, every
exterior deck with an explicit open, never an absent field. The variant cockpit/foredeck records
(×18) publish beyond the 59, matching the lobster-family treatment from batch 1.

## ASK B — every face DECLARES its level

Per-face `lv` stamped by an authoring cursor riding `F.push` (every emission path — face/boxF/tubeF/
text bakers/direct push — carries it); `geometry().ids` is the shared int table for the
TexCoord1.x bake. Ships: hull 0 · main_deck 1 · house 2 · bridge 3 · below 4 · rigging 5
(+ poop_deck 6 on the tanker). Variants: hull 0 · cockpit 1 · foredeck 2 · house 3 · cuddy 4 ·
rigging 5 (the lobster family table).

Your two rules, applied unchanged:
- **Standing-on**: deck furniture goes with the deck it stands on (winches, hatches, net, drum,
  capstan, tank covers, compressor cabin, manifold, pipes → main_deck; mooring gear → poop_deck);
  boat decks, their rails, ladders, liferafts, lifeboats and funnels are the house's lid → `house`
  (the Newfoundland dry stack is the funnel — house, on all 6 stack variants); door leaves cut with
  the room (`lv:'house'`, all four rigs).
- **Rigging is the DEDICATED CLASS**: dragger foremast/derricks/gallows/otter boards/warps/mizzen;
  Mk II stern gantry (legs, cheeks and beam — plated in hull steel, still class-tagged so a cut can
  never take it with a room) + warps + mast + stays; tanker centre catwalk/vent masts/hose crane/
  radar mast/satdome/foremast/stays; variants arch/dome/pods/floods/whips/mast & boom/cabin-top
  rail/light poles.
- `hull` = the exterior silhouette, never culled: shells, bulwarks, washboards + their stanchion
  rails, rail caps, foc's'les and their furniture, the tanker's poop break + its ladders (the raised
  hull's own face, like a foc'sle break), baked hull lettering.

Dressed masses stay flagged: the tanker's L2 cabins + L3 officers are tagged `house` and declared in
`geometry().dressed` (the packet's pattern).

## Byte-discipline receipt (the reach-kit law)

`qa/Boat Cutaway QA 2.dc.html` — ALL CHECKS PASS, failCount 0 (2026-08-26): face streams byte-equal
outside `lv` (v/mat/b/db, count and order) on all 21 hulls · door leaves equal at doorOpen 0 and 1 ·
**0 differing pixels** across 5+3+2 facings × door 0/1 on the ships and 18 variants × facing 3 ×
door 0/1 + 4 more facings on the reference boat · anchors byte-equal (helm, door, gallows/gantry/
crane/manifold/funnel, tubs, nav, and the variants' `anchors(v)` table) · every face tagged · every
level ceilinged or explicitly open · all four ties broken.

LF sha256 (CRs stripped):

    sideDraggerIsoRig.js        pass 3  c4ad181666aca73874e2b8c9a2e2367567ebf528c5021925ae8199fca880855b
                                base    b52237484f241f200656c5480ca63f81d3a1a70d20f7b649c06ad25dfd97d9e0
    sternTrawlerMk2IsoRig.js    pass 3  7bf879c450e78dcdce74c10b2573cdd3dd9ccdaacb4d912cbe5f9f38ae0639dc
                                base    e7fa9ea69a310ab03be5640cf89c4c18c1fd92b5b8b074056b5208f1eeb64628
    tankerIsoRig.js             pass 3  abd17acb503fc08ce518ea57dd5f3cdee5bac9d02043c80a7c4594bd57b105db
                                base    c2faaa385e472e3c4c1fdc0a219b86d8889b71faafd02554a443e8bb20959f0c
    lobsterBoatVariantsIsoRig.js pass 5 ce7d45ff4fbcd3e14a608bbccb5c5fd0854f9e65bdf4323b664e3a379eb5019b
                                base    f5fa042997c21414bd9adcf558b0a2adde541849dce3f4b1ce399156940b0682

## The four lessons from batch 1's import, honoured

1. **Lineage declared per hull.** Each revision above was cut from the stated base sha, and each
   base sha EQUALED the `derivedFromRigSha256` pin every sidecar named at cut time — dragger and
   Mk II in `Art/gameplay/<stem>.gameplay.json`, the tanker in `tankerIsoRig.gameplay.json`, the
   variants in **all 18** `Art/gameplay/lobsterBoatVariants/*.gameplay.json`, unanimously. The QA
   page checks base-sha == cut-time pin as its first check per hull, so divergence would be visible
   at intake, not at the def build. No forks: every revision is cut from repo-main current. The
   pins were then re-stamped to the pass shas in this same drop — see **The re-stamp, executed**.
2. **Deck-defining core byte-identical.** No value of L/TH/DECK/POOP/NSEG, no offsets table, no
   station()/skin()/dfrac()/flareExp(), no house envelope changed — the revisions are purely
   additive (cursor calls, geometry()/faces()/LEVEL_IDS, an lv tag on the leaf, and a cullLevels
   branch in render that is byte-identical when absent). Nothing hoisted this pass; nothing
   re-measured. The proof is the pixel diff above.
3. **Stamping law.** This kit ships rig sources only — no new stamps invented. Sweep of the
   shipping surfaces (this kit, Art/gameplay, boat-interiors, gameplay-sidecar-kit, batch-1
   sidecars) finds **zero** unsubstituted `STAMP_AT_EXPORT_…` values in any sidecar JSON; the one
   textual hit is the generator's own template literal inside the boat-interiors kit's renderer
   source (`export/boat-interiors/boatInteriorRig.js`), which the export flow substitutes —
   generator code, not a shipped stamp. The sport-fisher multi-entry pins are untouched and remain
   unanimous; the batch-2 pins were flipped whole under the unanimity rule (below).
4. **Variant-opts spellings + the HULLS row** — see the HULLS table: exterior spelling is top-level
   `{size,style,region}` on every new API; the nested `variant` spelling stays boatInteriorRig's;
   no multi-hull `byId` global exists or is needed for this family.

## The re-stamp, executed in this drop (2026-08-26)

Run upstream as part of the drop itself, so the kit arrives self-consistent — and not under the
"provenance-only no-op" framing: the justification is the one the cape bar ratified — additive
pass, deck-defining cores byte-identical, 0 differing pixels — so the rooms' measurements still
describe the loft they were cut from. Four conditions, all met:

1. **Stamped shas are the LF sha256 of the exact bundled rig bytes** in
   `export/boat-cutaway-kit-2/hull-rigs/` — asserted byte-identical to `Art/` before hashing, so
   the stamped value is the hash of the form your reader hashes.
2. **Unanimity holds.** Every `hullRigSha256` map touched is single-entry, keyed by the bare rig
   stem; all 18 variant sidecars flipped to the identical value in the same commit — no half-updated
   multi-entry pin exists for your reader to refuse.
3. **Zero unsubstituted templates.** The `STAMP_AT_EXPORT_…` class appears in no sidecar JSON
   (re-swept post-flip across all touched directories).
4. **Old→new pairs, per hull** (verify against bytes, per your standing law — the pairs are the
   base→pass rows of the sha table above):

       sideDraggerIsoRig         b52237484f24… → c4ad181666ac…   3 files (gameplay, interior, kit-mirror gameplay)
       sternTrawlerMk2IsoRig     e7fa9ea69a31… → 7bf879c450e7…   3 files
       tankerIsoRig              c2faaa385e47… → abd17acb503f…   3 files
       lobsterBoatVariantsIsoRig f5fa042997c2… → ce7d45ff4fbc…   54 files (18 gameplay + 18 interior + 18 kit-mirror gameplay)

   63 files, 63 pin lines, across the three sidecar layers (`Art/gameplay/`,
   `export/boat-interiors/*.interior.json`, `export/boat-interiors/gameplay/`). `_supersedes`
   history lines were deliberately left at their historical values (the Mk II's and dragger's
   `_supersedes` still name the pre-interiors/base shas — history stays history);
   `interiorDerivedFromRigSha256` (boatInteriorRig's own layer) is untouched. Every flipped file
   re-parsed as valid JSON.

The QA page's lineage check still asserts base-sha == cut-time pin against the pristine baselines
in `qa/cutaway-baseline/` — that is the historical claim; the live sidecars now name the pass shas
above.

## Layout

    README.md                                   this receipt
    hull-rigs/sideDraggerIsoRig.js              pass 3
    hull-rigs/sternTrawlerMk2IsoRig.js          pass 3
    hull-rigs/tankerIsoRig.js                   pass 3
    hull-rigs/lobsterBoatVariantsIsoRig.js      pass 5 (18 hulls)
