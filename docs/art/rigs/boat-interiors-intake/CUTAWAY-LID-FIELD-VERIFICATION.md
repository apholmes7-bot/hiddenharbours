# Cutaway kit — `ceiling.lid` on seven rigs, and the retirement of the stand-in table

**Seven rigs in, twenty-four pins moved, one table deleted, and a skip-list the next drop inherits.**
Everything below was measured, not argued. No Unity ran in this lane: the rigs were run in the repo's
own ClearScript V8 (`Assets/_Project/Plugins/Editor/JsEngine/`) from a standalone `net8.0` console
app, which executes the rig `.js` **unmodified**; the C# was compiled headless and the fixtures were
run headless too, against a control at the base commit.

Base: `origin/main` `8484ec03`.

Drops, all three from `.claude/uploads/13762e1c-…/`:

| zip | what it is | what this lane took |
|---|---|---|
| `…cutawaykitlidfield.zip` | batch-1 kit re-export (3 rigs + 2 sport sidecars) | the lid fields; the convertible pin |
| `…cutawaykitlidfield2.zip` | batch-2 kit re-export (4 rigs) | the lid fields |
| `…boatinterioersskybridgeconvertible.zip` | whole-tree `export/boat-interiors/` | **nothing** — see §5 |

⚠️ **Both lid zips carry the 2026-08-26 READMEs unchanged, and neither mentions `ceiling.lid`.** The
receipts describe the pass-3/pass-5 drops that already landed as #666 and #670, and their sha tables
name the OLD bytes. A receipt is not evidence of itself; every claim below comes from the files.

---

## 1. What the drop actually changed

Seven rigs, LF-normalised, diffed against our committed `docs/art/rigs/` copies:

| rig | changed lines | hunks | what moved |
|---|---|---|---|
| `sternTrawlerIsoRig` | 8 | 1 | 4 × `lid:` |
| `coastalPacketIsoRig` | 8 | 1 | 4 × `lid:` |
| `sideDraggerIsoRig` | 8 | 1 | 4 × `lid:` |
| `sternTrawlerMk2IsoRig` | 8 | 1 | 4 × `lid:` |
| `tankerIsoRig` | 10 | 1 | 5 × `lid:` |
| `lobsterBoatVariantsIsoRig` | 10 | 1 | 5 × `lid:` |
| `lobsterBoatIsoRig` | **128** | **7** | see §2 |

Six are a single hunk inside `geometry().levels[].ceiling` and nothing else. Those six take
upstream's bytes verbatim.

### The lids, as the rigs now declare them

| rig | level | lid |
|---|---|---|
| `lobsterBoatIsoRig` | `cuddy` | `foredeck` |
| `lobsterBoatVariantsIsoRig` (×18) | `cuddy` | `foredeck` |
| `sternTrawlerIsoRig` | `below` | `main_deck` |
| `coastalPacketIsoRig` | `below` | `main_deck` |
| `sideDraggerIsoRig` | `below` | `main_deck` |
| `sternTrawlerMk2IsoRig` | `below` | `main_deck` |
| `tankerIsoRig` | `below` | **`poop_deck`** |

Every other level publishes `lid: null` — the ruling's per-level veto. **All seven agree with the
retired `RigLevelLids` entry for entry**, including the tanker's, which is the one lid in the fleet
that is not the obvious deck and the one a z-rule could not have decided at all (her `poop_deck` and
`house` share a sole at 11.60 m).

---

## 2. ⚠️ THE LOBSTER IS NOT UPSTREAM'S FILE, AND THE DROP'S COPY WOULD REGRESS HER

Our root `lobsterBoatIsoRig.js` (`3d85bc36…`) is the three-way merge #660 landed: upstream's pass 3
plus **our #497 paint kit**, which upstream's tree has never held. The drop's lobster is cut from
their pass 3, so adopting it wholesale deletes:

`PAINTS` (12 schemes) · `paintRamps(id)` · `defaultPaint` · `oklchHex` · `mkRamp` · `matsFor` ·
`_rampCache` / `_matCache` · the `MAT` parameter on `_paint` · `matsFor(opts.paint)` in `render()` ·
`GRIP` from the exported ramp list · the merge-provenance header.

She therefore gets **the four lid insertions only**, applied as literal replacements asserted to
match exactly once each, with the paint kit's six symbols checked present afterwards.

---

## 3. Measured by RUNNING, not by reading

A cutaway pass has added geometry before — #670's posed door leaf, which the drop's README hid behind
"no new semantics" and which nothing composed. So the seven rigs were run before and after, through
the same expression `RigMeshExtractor` uses, on the default variant and on a non-default one.

- **Before:** every level on all seven reads `lid=?` — no property. That is the debt the table stood in for.
- **After:** every level publishes one. No `?` anywhere. The seven named lids are the seven above; the
  non-default variant (`offshore/hardtop/newfoundland`) declares the same `cuddy → foredeck`, so the
  family law is the family's and not the default boat's.
- **`faces()`, `doorFaces({doorOpen:0})` and `geometry().ids` are byte-identical across all eight
  probes** — same sha, same length. No face moved, none was retagged, no vocabulary changed.

---

## 4. The 24 pins

The root deck sidecars that name these rigs move with them:
`docs/art/rigs/gameplay/{lobsterBoatIsoRig, sternTrawlerIsoRig, coastalPacketIsoRig, sideDraggerIsoRig,
sternTrawlerMk2IsoRig, tankerIsoRig}.gameplay.json` and the eighteen `lobster*Iso.gameplay.json`.

⚠️ **TWO SHA CONVENTIONS ARE LIVE AND BOTH ARE CORRECT.** Each file's convention was read from its
OWN bytes — the old pin matched against the old rig's LF *and* CRLF digests — and re-stamped in
whichever form it already used. Nothing was assumed from a neighbour.

| files | convention | note |
|---|---|---|
| batch 1 ×3 | **CRLF** (working-tree form) | an LF-only grep finds none of these; it missed all six sites on the first pass here |
| batch 2 ×3 + variants ×18 | **LF** (blob form) | the new values are the LF sha of upstream's delivered bytes exactly |

Full 64-character digests throughout. A prefix check is not a hash check.

**Not touched, deliberately:** the kit's `hull-rigs/` mirrors and their interior sidecars. The cutaway
extractor reads `docs/art/rigs/`; the interior sheets and defs read the kit's copy — the split is by
design and documented at `BoatInteriorRigHost.Install`, and batch 1's kit copies are pass-2 for that
same reason. A lid field the interior layer never reads is no reason to move 63 more pins and rebuild
the interior defs. `EveryStampedHullPinResolvesToTheBundledRigItNames` and
`TheRepositorysCopyOfAHullRigIsNotWhatTheSidecarsPin` both stay green on that basis.

**Still owed to the coordinator's last-mile:** the seven `Data/Boats/Decks/*.asset` carry
`DerivedFromRigSha256` and must be regenerated from these sidecars. `Data/Boats/Interiors/*.asset`
pin the KIT rig and are unaffected.

---

## 5. ⛔ THE SKIP-LIST — what the whole-tree export would have regressed

`…boatinterioersskybridgeconvertible.zip` is a full `export/boat-interiors/` tree. It predates batch 2
and the cape merge, so most of it is a rewind. **Nine rigs compared to our committed kit copies:**

| rig | zip | ours | verdict |
|---|---|---|---|
| `capeIslanderIsoRig` | `a3be1d61…` | `60d127c3…` | ⛔ **SKIP** — rewinds the #667 cape merge |
| `lobsterBoatVariantsIsoRig` | `f5fa0429…` | `ce7d45ff…` | ⛔ **SKIP** — rewinds pass 5 (#670) |
| `sideDraggerIsoRig` | `b5223748…` | `c4ad1816…` | ⛔ **SKIP** — rewinds pass 3 (#670) |
| `sternTrawlerMk2IsoRig` | `e7fa9ea6…` | `7bf879c4…` | ⛔ **SKIP** — rewinds pass 3 (#670) |
| `tankerIsoRig` | `c2faaa38…` | `abd17acb…` | ⛔ **SKIP** — rewinds pass 3 (#670) |
| `coastalPacketIsoRig` | `ba4119da…` | `ba4119da…` | no-op |
| `lobsterBoatIsoRig` | `77a2e16f…` | `77a2e16f…` | no-op |
| `sportFisherIsoRig2` | `205a93c9…` | `205a93c9…` | no-op |
| `sternTrawlerIsoRig` | `3f306e41…` | `3f306e41…` | no-op |
| `boatInteriorRig` | `34bb7813…` | `34bb7813…` | no-op |

**Five would regress merged work.** Of its 54 sidecars, 51 are byte-identical to ours and three
differ; `capeIslanderIsoRig.interior.json` is a sixth rewind (it re-pins the cape's pre-merge sha).

**The rule the next drop inherits: resolve every file in a whole-tree export against our committed
bytes BEFORE taking any of it.** A tree export is not a changeset — it carries whatever the sender's
tree held at export time, including generations we have moved past. Take fields, not files.

---

## 6. The two sport fishers — one adopted, one held

**Convertible: ADOPTED.** Her kit `gameplay/` mirror pinned `ebc77bac…`, which is the LF sha of
`hull-rigs/sportFisherIsoRig2.js` **as committed at #589** — real, and stale since #660 moved that rig
to `205a93c9…`. #660 moved the two `interior.json` pins and left the two `gameplay/` mirrors behind.
The drop's replacement is that one line; our file is now byte-identical to it.

**Skybridge: HELD.** Her replacement carries the same pin fix and also deletes the z-9.74 /
42-vertex open coaming, renaming `sky_sole` onto `bridge_sole` at 7.30 — 218 lines shorter. That is
upstream resolving an id collision **in their own rig** (`sportFisherIsoRig2.js` uses `bridge_sole`
for the enclosed skylounge sole at 7.30 *and* the open coaming at 9.74, which carries `helms[0]` and
the bridge ladder) by keeping only one of the two.

⚠️ **Their file contradicts itself doing it.** It keeps `bridge_ladder` climbing z 7.32 → 9.55 and
connecting to a `bridge_sole` it has left at 7.30: the ladder rises 2.23 m to arrive 0.02 m **below
its own foot**. The HOLD is therefore not a preference between two valid exports — it is a refusal of
an internally inconsistent one. It stands until the rig disambiguates the id and re-exports
(upstream ask 6). Nothing is re-stamped on our side meanwhile.

⚠️ **A string-grep cleared this file once.** The staging pass counted `bridge_sole` occurrences —
eight, none of them `sky_sole` — and called the deck "fully present". A name's presence is not a
deck's presence. `TheSkybridgeKeepsBothOfHerDecks_AndHerLadderLandsOnTheUpperOne` reads the polygon
and the z, and its second half is the general law: **a ladder lands on the deck it names.**

---

## 7. The table, and what replaced it

`RigLevelLids` is deleted — `Declared`, `For`, `AllFor`, `FileNameOf`, `RigLevelTables.NoLids`, and
`RigLevelRecord.LidFromTable`. Its one surviving const is `RigLevelTags.LidProperty`.

**The absent case became a REFUSAL.** With no table there is nothing to fall back to, and the
tempting default — "no lid, takes nothing" — is the exact failure the ruling was made about: the
level engages the gate, its ceiling stays, and the occupant goes below to look at a whole boat. A rig
that publishes levels without a lid on every one of them now stops the bake. `lid: null` is how a
level says it takes nothing; absent still never looks like null.

`TheStandInLidTable_StaysRetired_AndEveryLidComesFromTheRig` asserts the type is gone **by name,
through reflection** — a test cannot reference a type that must not exist — and that every level on
all 24 cutaway hulls sourced its lid from `rig` or `veto`, never `none`.

---

## 8. Results

Headless fixture run (`HullLevelTagBakeTests` + `BoatInteriorDefShapeTests`), branch against a
detached control at the base commit `8484ec03`:

| | pass | fail | skip |
|---|---|---|---|
| control (`8484ec03`) | 21 | 0 | 7 |
| this branch | **22** | **0** | 7 |

**Identical skip set name-by-name, and zero status changes on any shared test.** The 7 skips are
harness limits (six `Application.dataPath`/`Mesh` ECalls, one parameterised fixture), not results.
The single extra pass is the new skybridge guard; the retirement test replaces the old ledger 1:1.

The one-hop lid law of #666 holds on rig-published lids exactly as it did on the table's, same
assertions and new source: `EveryDeclaredLid_IsALeafLevelThatIsNeitherHullNorRigging` (24 lids) and
`EveryEnclosedLevel_RemovesSomething_ItsOwnFacesOrItsDeclaredLids` both green.

### Negative controls — five arms, each on its own run

| arm | expected | result |
|---|---|---|
| reference `RigLevelLids` in the extractor | the type is gone | `CS0103` at that line ✓ |
| `ThisTypeDoesNotExist` in the edited test | stage 2 reports errors | `CS0246` at that line ✓ |
| strip the tanker's `lid` | the bake REFUSES, does not default | every extracting fixture red on the new message ✓ |
| lobster `cuddy` lid → `cockpit` | the provenance ledger catches a wrong lid | retirement test + one-hop fixture red ✓ |
| swap our skybridge mirror for the refused file | the HOLD is enforced | skybridge guard red on "the skylounge sole is gone" ✓ |

The last is the strongest: the guard demonstrably catches the artefact it was written about, rather
than merely passing today.
