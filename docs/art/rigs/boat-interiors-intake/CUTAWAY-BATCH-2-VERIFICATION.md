# Cutaway kit, batch 2 — verification

**Four rigs in, sixty-three pins moved, no verdict re-earned.** Everything below was measured, not
argued. No Unity ran in this lane; the whole proof is the repo's own ClearScript V8
(`Assets/_Project/Plugins/Editor/JsEngine/`) driven from a standalone `net8.0` console app, which
runs the rig `.js` **unmodified**, plus a headless csc chain for the C# side.

Drop: `Pixel art capabilitiesboatcutawaykitbatch2.zip` (rig sources + receipt, staged verbatim at
`docs/art/rigs/boat-cutaway-kit-2/`) and `Pixel art capabilitiesboatcutawayrefresh.zip` (the
re-stamped sidecars).

---

## 1. Lineage — no fork anywhere in this batch

Every number below was recomputed here from the bytes on disk.

| rig | base (LF) | = our committed kit copy? | pass (LF) | = the delivered bytes? |
|---|---|---|---|---|
| `sideDraggerIsoRig` | `b5223748…` | ✅ | `c4ad1816…` (pass 3) | ✅ |
| `sternTrawlerMk2IsoRig` | `e7fa9ea6…` | ✅ | `7bf879c4…` (pass 3) | ✅ |
| `tankerIsoRig` | `c2faaa38…` | ✅ | `abd17acb…` (pass 3) | ✅ |
| `lobsterBoatVariantsIsoRig` | `f5fa0429…` | ✅ | `ce7d45ff…` (pass 5) | ✅ |

**4/4 base shas equal our committed `boat-interiors-kit/hull-rigs/*.js`** — batch 2 is cut from
exactly the lofts our interior sidecars pinned at cut time. There is no repo-side fork, so nothing
here needed the cape's three-way merge; each hull fast-forwards the way #660 did trawler and packet.

⚠️ **TWO baselines per hull, and they are not the same file.** Root canon held the PRE-interiors
generation of each rig; the interiors were cut from the KIT copy, which root never held. The S0
ledger's axis B resolved each baseline against ROOT because root was all main had. Both numbers are
recorded in `s0-verdicts.json` so the pair can never be read as one. After this import they are the
same bytes for the first time:

| rig | root was | kit was | both now |
|---|---|---|---|
| `sideDraggerIsoRig` | `3e82a11e…` | `b5223748…` | `c4ad1816…` |
| `sternTrawlerMk2IsoRig` | `4656fd3d…` | `e7fa9ea6…` | `7bf879c4…` |
| `tankerIsoRig` | `deec4594…` | `c2faaa38…` | `abd17acb…` |
| `lobsterBoatVariantsIsoRig` | `ff8414f8…` | `f5fa0429…` | `ce7d45ff…` |

---

## 2. The 63 pins

The receipt declares 63 files and 63 pin lines across three sidecar layers. All 63 landed, and each
diff is **exactly +1/−1**:

| layer | files | source | check |
|---|---|---|---|
| `docs/art/rigs/gameplay/` | 21 | **not in the drop** — stamped here | LF sha of the bytes landed in the same commit |
| `boat-interiors-kit/**/*.interior.json` | 21 | upstream's refresh | verified vs the receipt's old→new pairs |
| `boat-interiors-kit/gameplay/` | 21 | upstream's refresh | verified vs the receipt's old→new pairs |

- **Before landing:** the refresh's 42 were compared line-by-line against the receipt's declared
  old→new pairs. **42/42 were single-line moves, base → pass, with nothing else in the diff.**
- **After landing:** all 63 pins were re-checked as resolving to the bytes they name — layer 1
  against `docs/art/rigs/<stem>.js`, layers 2 and 3 against `boat-interiors-kit/hull-rigs/<stem>.js`.
  **63/63.** The whole-kit invariant `EveryStampedHullPinResolvesToTheBundledRigItNames` also holds
  at its exact expected numbers: **stamped = 29, variantKeyed = 2, unresolved = 0.**
- All 63 files re-parse as valid JSON.

**Why the kit's `hull-rigs/` moved too.** The interior pins resolve against the rig shipped *beside*
them, not against root. Landing the re-stamped sidecars while leaving the kit rigs at their base
bytes would ship a kit whose sidecars name a sha no file in that kit has.

**Not re-stamped, deliberately:** the `_supersedes` history lines (history stays history) and
`interiorDerivedFromRigSha256`, which is `boatInteriorRig`'s own layer and untouched by this batch.

**One convention change, stated rather than smuggled.** The three ships' root sidecars carried the
CRLF-of-working-tree hash and now carry LF, like the eighteen variants already did. LF is what git
stores, it is what `ShipyardIsoBakeTests` requires, and it is platform-independent;
`LineEndingNormalized` is an accepted pass either way, so nothing is loosened. The upshot is that all
three layers now name **one value per hull** instead of three conventions.

---

## 3. What licenses moving a pin: the additive proof

Moving a pin is only honest if the derivation's inputs are unchanged, and that is measured here
rather than read off the receipt. Each **base** rig (our committed kit copy — the sha every batch-2
sidecar pinned at cut time) was run against its **pass** rig in two V8 engines.

**Ships — `sideDragger`, `sternTrawlerMk2`, `tanker`:**

- `loft` and `HOUSE` byte-identical.
- Every published anchor byte-identical (`HELM`, `HAULER`, `GALLOWS`, `GANTRY`, `DRUM`, `CRANE`,
  `MANIF`, `FUNL`, `TUBS`, `DOOR`, and every `…Mount`/`…Mounts`/`…Seat` function).
- **16 renders each — 8 facings × door 0/1 — byte-identical.**
- Keys **added**: `geometry`, `faces`, `doorFaces`, `LEVEL_IDS`. Keys **removed**: none.

**Variants — all 18:**

- `loftOf`, `houseOf`, `anchors`, `gameplayGeometry`, `hullMeta`, `resolve`, `windowPlan` identical
  on all eighteen.
- **43/43 renders byte-identical** (18 hulls × door 0/1 at the reference facing, plus the reference
  boat at the other 7 facings).
- ⚠️ **`interiorEnv(v)` DID differ on all eighteen** — the one finding in this pass, and it matters
  because `interiorEnv` is precisely the route the variants' interiors resolve through. Chased down:
  the difference is entirely (a) the four added keys and (b) the **source text** of `render()`'s new
  `cullLevels` branch, which the receipt declares is byte-identical when absent and which the 43
  pixel probes prove inert. Compared **data-only** — functions and the four new keys dropped —
  `interiorEnv` is **identical 18/18**.

**The harness was controlled first.** It reproduces #660's already-adjudicated batch-1 trawler as
additive, and a 1 cm move of `DECK` on that same rig is caught in `loft`, in `HOUSE`, in
`loft.DECK` and in **every** pixel probe. A third control caught a harness bug worth recording:
functions are compared by `.toString()`, which returns source **including line endings**, so an
LF copy of a CRLF file reported a false DIFF on every function-valued probe until the harness
LF-normalised on read — the same normalisation `DeckSidecarReader.MatchRigHash` applies.

---

## 4. The lid — the table grew, and one entry is not the obvious deck

Batch 2's rigs were swept for `ceiling.lid`: **none publishes it.** The string `lid` occurs in them
only in prose and in the authoring cursor's own comments. So `RigMeshLevels.RigLevelLids` grows and
the upstream ask grows with it — from three levels on three rig files to **seven on seven**.

| rig | level | lid | the rig's own words |
|---|---|---|---|
| `sideDraggerIsoRig` | `below` | `main_deck` | `'main-deck underside (DECK-0.10)'` |
| `sternTrawlerMk2IsoRig` | `below` | `main_deck` | `'main-deck underside (DECK-0.12)'` |
| **`tankerIsoRig`** | `below` | **`poop_deck`** | `'poop-deck underside (POOP-0.25)'` |
| `lobsterBoatVariantsIsoRig` | `cuddy` | `foredeck` | `lv('foredeck'); // the cuddy's lid — a walkable level of its own` |

Every other enclosed level folds its own lid into its own tag, exactly as batch 1's do — all three
ships' `house` carries its boat deck (`lv('house'); // the funnel stands on the boat deck — the
house's lid`) and each `bridge` its deckhead. The Mk II says it outright in the ceiling record:
*"the flared sides (hxAt) are the walls, not the lid"*.

**⚠️ The tanker settles the argument against inferring lids.** Measured off her own `geometry()`:
`below`'s ceiling is 11.35 m, and the levels whose sole sits 0.25 m above it are `poop_deck` **and**
`house` — both at 11.60. They share a sole; that is the tie her own `tieBreak` field exists to
break. A rule matching ceiling z to sole z would not merely get her wrong, it could not decide her
at all. Prose-matching fares no better: `poop-deck` is hyphenated where the id is `poop_deck`.

**Proved against the rigs' own published data**, with the table parsed out of the shipped C# source
rather than re-typed: every declared level is one the rig publishes; every declared lid is in that
rig's `geometry().ids`; no level is its own lid, `hull`, or `rigging`; no lid has a lid (ONE HOP);
and every lid is an **open** level — the asymmetry #666 named out loud: an open level may never be
cut *into*, but makes a fine lid.

One table entry covers all **eighteen** variants: `RigLevelLids` is keyed by rig **file**, and one
generator makes them all, so no variant can drift from its family.

---

## 5. C# verification — compiled, then RUN

All 14 assemblies on the path compile from worktree sources.

⚠️ Main's `Library/ScriptAssemblies` carry a **fresh mtime and pre-#666 content** (probed: no
`LevelTags`, no `CarriesLevelTags`), so every assembly was rebuilt and fed forward rather than
referenced stale — the trap that makes a real branch look broken. Each was compiled against its
asmdef's **direct** references only, never the transitive closure, which is more permissive than
Unity where `overrideReferences` is set.

Both negative-control arms fired, on **separate runs** (a combined control hides its second arm):
`CS0246` on an unresolvable type, and `CS0234` on a reach into `HiddenHarbours.Player` — so the
harness both reports errors and enforces asmdef direction.

**Then the tests were RUN**, in a `net8` reflection runner over the freshly-built assemblies. Two
harness seams were needed and neither touches measurement: `Application.dataPath` is a native ECall,
so harness-only copies of the two RigBaking assemblies carry a literal in its place (29 sites, and
the patcher fails loudly on zero); and `Debug.Log*` bottoms out in the same kind of ECall, which was
turning **every** extraction test into a skip — Unity's log handler is a managed seam, so swapping it
is enough and no source is touched for it at all.

| | result |
|---|---|
| `HullLevelTagBakeTests` | **7 pass / 0 fail / 3 skip** (the skips are `AssetDatabase` fixtures) |
| whole RigBaking assembly, this branch | 534 pass / 149 fail / 225 skip |
| whole RigBaking assembly, **control at `origin/main`** | 534 pass / 149 fail / 225 skip |

**Name-by-name diff of the two runs: ZERO status changes**, across 908 tests each side. The only
difference is the one renamed fixture (`Batch1_IsThreeHullsAndTheyAreAllInTheFleetTable` →
`TheCutawayKitsHulls_AreAllInTheFleetTable`), passing on both. The 149 failures are harness limits —
no `AssetDatabase`, no texture loading — reproduced identically on a detached worktree at
`origin/main`. **That is not a claim that main is red on them**; it is the claim that they are not
this branch's.

Worth naming because it is the running proof of §2:
`DeckSidecarImportParityTests.EverySidecarStillDescribesTheRigItNames` **passes** — every re-stamped
root pin resolves against the rig bytes this branch lands.

---

## 6. What is NOT done here — the Unity last-mile

This lane lands rig sources, sidecars, the lid table and its fixtures. It bakes nothing. The
following go red on arrival **by design**, and each is a bake instruction rather than a defect:

Every fixture in this table is one the headless runner could only **skip** (it needs `AssetDatabase`),
so none of them is covered by the green above and each is listed here rather than assumed.

| family | why | what clears it |
|---|---|---|
| `NoHullOutsideBatch1_HasARigThatOutranHerBake` | 21 hulls now publish a vocabulary their committed mesh has not | whole-fleet mesh re-bake + retire the batch-1-only entry point |
| `HullCutawayAssetTests` (batch-1 selection) | the batch-1-only selection is now stale | same re-bake |
| `HullMeshFleetTests.EveryCommittedHullMesh_MatchesAFreshExtractionFromItsRig` | **the door leaf** (§4a below) changes the extracted face list on 21 hulls: +16 on each ship, +9 per variant. The committed meshes have no door | same re-bake |
| `IsoFacetSideDraggerAcceptanceTests` and the variants' acceptance fixtures | they compare the committed mesh against the rig's own renderer, and the rig draws a door the mesh has not | same re-bake — the leaf should IMPROVE agreement, not worsen it |
| deck defs / `EveryCommittedDeckDefMatchesItsSidecarVertexForVertex` | `Data/Boats/Decks/*.asset` carry their own `DerivedFromRigSha256` — e.g. `SideDraggerIso.asset` still holds `163f0cdf…`, the old root CRLF hash | re-import deck sidecars, then `git checkout --` the untouched hulls (they come back with ULP drift) |
| `BoatInteriorDef` interiors | `Data/Boats/Interiors/*.asset` carry `HullRigSha256`; 21 of them still name a base sha (`SideDraggerIso.asset` → `b5223748…`) | re-run the interior def builder |
| lobster sheet (`IsoFacetLobsterEndToEndTests`) | **pre-existing** — control-proved at `68cead28` by #666, owned by the sheet re-bake lane | not this lane |

### 6a. The door leaf — a defect this lane found, and fixed

Batch 2's rigs gained a posed door leaf that nothing on this side composed. Extraction took bare `F`
(and bare `facesFor(V).F`), so all 21 hulls would have baked a mesh missing geometry their own
picture draws. Four `RigMeshSymbols.Widenings` entries fix it — the three ships take the ships'
composition, the variants take the generator's, whose private `doorFaces` takes `(V, t)` rather than
an opts bag.

**It was found by RUNNING, not by reading.** A compile cannot see it, and the drop's README says
"same mechanism, no new semantics". `TheDoorLeaf_IsTaggedWithTheRoomItCloses` went red on the dragger
the moment batch 2's hulls joined `Pass3Keys` — that fixture asserts `leaf > 0` *before* checking any
tag, precisely so a missing leaf cannot pass by having nothing to check.

The same run forced two fixture corrections, both worth reading as method notes:

- the door fixture asked a **generator** for `faces()` with no argument, i.e. its default variant, and
  so compared one hull's extraction against another hull's static list — reporting a *negative* leaf
  (−50) that read as "the door is missing";
- and the enclosed-level ledger was first written here as **five** entries with a comment explaining
  why the tanker and the variants were absent. They were not absent: the runner was truncating the
  failure message at six lines. It is 24, and the comment now states the measured fact.

---

## 7. Found but deliberately NOT landed

The refresh zip is a whole-tree export and carries files outside this batch. Four differ from ours;
all four were examined and skipped, because ours is canon:

| file | ours | the zip's | why skipped |
|---|---|---|---|
| `hull-rigs/capeIslanderIsoRig.js` | `60d127c3…` (38,718 B) | `a3be1d61…` (28,482 B) | ours is the merged rig from #667; the zip predates it |
| `capeIslanderIsoRig.interior.json` | pins `60d127c3…` | pins `a3be1d61…` | ours points at the rig we actually ship |
| `sportFisherIsoRig2.convertible.interior.json` | pins `205a93c9…` | pins `ebc77bac…` | ours is #660's coordinator fix; the zip's is stale |
| `gameplay/sportFisherIsoRig2.skybridge.gameplay.json` | pins `ebc77bac…`, carries both `sky_sole` and `bridge_sole` | pins `205a93c9…`, carries `bridge_sole` but **not** `sky_sole` | see below |

**⚠️ Two sport-fisher items for a follow-up lane, out of scope here.**

1. `boat-interiors-kit/gameplay/sportFisherIsoRig2.skybridge.gameplay.json` still pins `ebc77bac…`
   while the rig beside it is `205a93c9…` — **stale**. #660 fixed the `.interior.json` layer's
   convertible pin but this `gameplay/` mirror was last touched by #589. The zip's replacement
   carries the right pin but **drops the `sky_sole` deck**, so it is not a straight adoption and
   neither file is clean. Nothing hashes this layer today (`BoatInteriorDefBuilder` reads
   `docs/art/rigs/gameplay`, not the kit's mirror), which is why it has gone unnoticed.
2. `boat-interiors-kit/README.md` documents the skylounge walkable as `DECK sky_sole`, but our own
   bundled rig renamed it `bridge_sole`. Upstream's refresh README carries the corrected paragraph.
   Our README is stale about our own rig.
