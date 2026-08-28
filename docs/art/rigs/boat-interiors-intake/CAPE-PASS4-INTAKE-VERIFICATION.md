# Cape pass 4 — her house learns to open, and one number in the drop was already right

**The flagship joins the cutaway kit; the skybridge's id collision ends upstream; and the drop's one
recomputed polygon is refused, because the rig disagrees with it by 50 mm.**

Everything below was measured, not argued. **No Unity ran in this lane**: the rigs were run in the
repo's own ClearScript V8 (`Assets/_Project/Plugins/Editor/JsEngine/`) from standalone `net8.0`
console apps that execute the rig `.js` **unmodified**, and the C# fixtures were compiled and run
headless against a control at the base commit.

Base: `origin/main` `6e7c43f2`.
Drop: `.claude/uploads/0f0ce03e-…/30b3c687-Pixel_art_capabilitiescapepass4.zip` — a **changeset**
(`CHANGES.md` + two payload files), which is the standing request working. It is much easier to
adjudicate than the whole-tree export that preceded it, and §5 of
[`CUTAWAY-LID-FIELD-VERIFICATION.md`](CUTAWAY-LID-FIELD-VERIFICATION.md) explains why.

---

## 1. The cape rig — every hunk declared, and the pixels prove it

Their base claim checks out. Our committed `docs/art/rigs/capeIslanderIsoRig.js` hashes
`60d127c3e77817ea…` LF, which is exactly the base `CHANGES.md` names. Sixteen hunks, 3 lines removed
and 64 added, and **every hunk is one of the declared additions**: the header/export comment, the
`lv()` authoring cursor and its `F.push` override, ten `lv('…')` cursor moves, the leaf's
`out.forEach(f => f.lv = 'house')`, `render()`'s `cullLevels` branch, and the
`LEVEL_IDS`/`geometry()`/`faces()` block.

Two of those are **behavioural**, not additive, so neither was taken on the diff's word:

### 1.1 The `F.push` override

`F.push` is replaced by a function that stamps `LV` on every argument before delegating to
`Array.prototype.push`. Every route a face takes into `F` goes through it — `face()`, `boxF`/`tubeF`
via `F.push.apply(F, …)`, and the four direct `F.push(winRR(…)/backPanel(…))` calls — so no face
escapes tagging, and none gains anything else:

```
FACES count=517
FACES keysets   {"b+db+lv+mat+v":517}        <- ONE record shape, and it is the old one plus lv
FACES lv hist   {"hull":397,"cockpit":52,"foredeck":15,"house":35,"rigging":18}   (= 517)
LEAF {doorOpen:0}   n=10   lv={"house":10}
```

The fingerprint over faces is a **projection** on `(v, mat, b, db)` — a tag riding a record may
legitimately change the record's shape while the geometry stands still, so the geometry is asserted
and the shape is reported separately.

### 1.2 `render()`, and the claim that an absent `cullLevels` changes nothing

Measured rather than read. Both rigs were rendered over **8 directions × 4 option sets** — default,
`elev 0.35`, `doorOpen 1`, and the non-default scheme `banks-white` — at 766 080 bytes each:

| | result |
|---|---|
| **32 renders, base vs pass 4** | **byte-identical, every one** (combined sha `1ba32c91115bb00e…`) |
| export surface | gained exactly `geometry`, `faces`, `doorFaces`, `LEVEL_IDS`; **lost nothing** |
| every pre-existing export | byte-identical by JSON (data) or by source text (functions) — **except `render` itself** |

That last row is what holds the **OKLCH paint kit** intact without arguing about it: `SCHEMES`,
`palette`, `rampFrom`, `chipWall`, `C_CAP` and the nine colour ramps all hash the same before and
after. It is the failure #660 had to hand-repair on the lobster, and it did not recur here.

⚠️ `doorFaces` was not exported by the base rig, so the leaf could not be compared function to
function. It is covered by the pixel statement instead — `render()` composes `F.concat(doorFaces(opts))`
and the `doorOpen:0` and `doorOpen:1` renders are byte-identical — which is the stronger claim anyway.

### 1.3 The lid law, on her own declarations

```
LID house     deck=house_sole   soleZ=0.72   ceilingZ=2.98    kind=hard    lid=<null: veto>
LID cuddy     deck=cuddy_sole   soleZ=0.3    ceilingZ=1.854   kind=raked   lid=foredeck
LID cockpit   deck=cockpit      soleZ=0.72   ceilingZ=null    kind=open    lid=<null: veto>
LID foredeck  deck=foredeck     soleZ=2.074  ceilingZ=null    kind=open    lid=<null: veto>
```

Four walkable levels, **four published lids, no `<ABSENT>`** — the #678 rule (absent is a refusal,
`null` is a veto) is satisfied without exception. `cuddy → foredeck` is one hop onto a leaf level.

### 1.4 The declared numbers are DERIVED, not typed

`CHANGES.md` says the geometry is declared "from the same constants the mesh is built from (never
re-measured off it)". Checked against the rig's own exported `loft`:

| declared | law | measured from the rig |
|---|---|---|
| `house.soleZ` 0.72 | `loft.DECK` | `=== loft.DECK` ✓ |
| `cockpit.soleZ` 0.72 | `loft.DECK` | `=== loft.DECK` ✓ |
| `cuddy.soleZ` 0.30 | `HOUSE.cuddy.soleZ` | `=== HOUSE.cuddy.soleZ` ✓ |
| `cuddy.ceiling.zAft` 1.854 | `sheerZ(y0) − 0.16` | `sheerZ(2.54) = 2.013875` → 1.854 ✓ |
| `cuddy.ceiling.zFwd` 2.828 | `sheerZ(y1) − 0.16` | `sheerZ(5.55) = 2.988125` → 2.828 ✓ |
| `foredeck.soleZ` 2.074 | `sheerZ(station(0.74).y) − 0.05` | `sheerZ(3.072) = 2.123600` → 2.074 ✓ |
| `foredeck.sole.zFwd` 3.226 | `sheerZ(station(0.985).y) − 0.05` | `sheerZ(6.208) = 3.276` → 3.226 ✓ |

⚠️ Her FACING LABELS ARE INVERTED (`cape-rig-merge-landed`), so every new field was checked for a
facing or direction reference. There is none: `lv` tags are level ids, `LEVEL_IDS` is a bake table,
and `geometry().frame` describes the MODEL axes — itself verified rather than believed, since
`station(t).y` rises toward the bow (0.74 → 3.072, 0.985 → 6.208) and the sole heights rise with z.
The `cullLevels` filter is direction-independent.

### 1.5 The cut, with its controls

A cull that removes nothing would pass a "render still works" check and fail the feature, so both
arms were run at dir 2:

| `cullLevels` | faces kept (of 527) | lit px | vs uncut |
|---|---|---|---|
| absent | 527 | 44 827 | — |
| `[]` | 527 | 44 827 | **byte-identical** |
| `['house']` | 482 | 43 271 | CUT |
| `['house','cuddy']` | 482 | 43 271 | CUT (the cuddy owns no faces — see below) |
| `['foredeck']` | 512 | 44 827 | CUT (occluded at this heading; the image still moves) |
| `['rigging']` | 509 | 44 799 | CUT |
| `['hull']` | 130 | 29 374 | CUT |
| `['no_such_level']` | 527 | 44 827 | **byte-identical** — no crash, no accidental cut |

And the dedicated class does its job: culling `house` takes 45 faces (35 static + the 10-face posed
leaf) and leaves **rigging untouched at 18** — the mast, spreader, boom and masthead do not come off
with the room.

`cuddy` has **no faces of its own**: it is a void under the whaleback, dressed by the interior rig.
That is precisely why its declared lid matters — without `lid: foredeck` its cut would remove
nothing and the foredeck would stay over the berth.

---

## 2. The skybridge sidecar — adopted, with three corrections

### 2.1 ⛔ `_supersedes` names a file we never had

The drop declares `_supersedes: 20aa9bc7cc43090f…`. That is **not** our committed file. It is the
sha256 of **upstream's own 2026-08-27 export** — the file #678 refused as internally inconsistent.

| file | sha256 |
|---|---|
| our committed mirror, LF/blob | `e15e0dce2ebc8b58…` |
| our committed mirror, working tree (CRLF) | `c246b2058def025e…` |
| **the drop's `_supersedes`** | `20aa9bc7cc43090f…` = **their refused export** |

So they fixed their branch, not ours, and the field named a predecessor this repository never held.
Re-stamped to `e15e0dce…` with a `_supersedes_convention` key saying which convention to answer in,
because two are live in this kit and #678 nearly lost a day to that.

**This matters beyond bookkeeping**: everything in the file arrived via the refused export, so
nothing in it could be taken on the changeset's description. It was resolved field by field against
our committed bytes.

### 2.2 The id split — adopted, and it is the right way round

`CHANGES.md` says "DECK `bridge_sole` (7.30, the skylounge) keeps its id". **That is true on their
branch and false on ours.** In our tree `bridge_sole` was the 9.74 COAMING. On our tree the change
is a double rename:

| walkable | ours (before) | adopted |
|---|---|---|
| enclosed skylounge sole, z 7.30, 4-vertex | `sky_sole` | **`bridge_sole`** |
| open control coaming, z 9.74, 42-vertex | `bridge_sole` | **`helm_coaming`** |

A shipped deck id keeping its name and changing which deck it means is the silent-repoint hazard, so
it was not waved through. It is adopted because our vocabulary was the outlier, and our own file said
so:

- The **rig** calls the 7.30 skylounge `bridge_sole`.
- Her **interior sidecar** has always called the 7.30 skylounge `bridge_sole`
  (`STAIRS: house_sole → bridge_sole`).
- Our **gameplay** mirror alone called 9.74 `bridge_sole` and invented `sky_sole` — and then carried
  the contradiction into its own STAIRS block, where `house_to_bridge` went `to: sky_sole` through an
  `opening.in: bridge_sole` **2.44 m above the deck it arrives at**. That has been in the file since
  #589.

`sky_sole` was ours, not upstream's; retiring it removes a permanent fork instead of creating one.

Adopted with it: `LADDER.bridge_ladder.connects → [aft_deck, helm_coaming]` (the 2.23-m-to-below-its-
own-foot defect dead), `STAIRS.house_to_bridge.opening.in → bridge_sole`, the reworded `_excluded` /
`_confirm`, and `derivedFromRigSha256 → 205a93c9…`, which **also fixes a stale pin on our side**: our
mirror pinned `ebc77bac…`, the #589-era rig, exactly as the convertible's did before #678 fixed it.

⚠️ The **rig is unchanged** (`205a93c9…` verified). The #660 expectation that "the sport sha will
move" is **VOIDED** — the fix was entirely sidecar-side. Nobody should wait for a rig move that is
not coming.

### 2.3 ⛔ The coaming polygon was recomputed, and recomputed WRONG

`CHANGES.md`: *"polygon computed by the rig's own volume xAt law (hw table + nose/aft rounding + 0.06
inset) minus the 0.16 rim."* It was not.

The rig builds this sole in `volume()`'s `o.open` branch as flat quads at z 9.74:

```js
for j: t0=TS[j], t1=TS[j+1]; if(t1>1) break;
       A=P(t0,z1), B=P(t1,z1);  ax=max(0.03, A.x-rim), bx=max(0.03, B.x-rim)
       face([[-ax,A.y,sole],[ax,A.y,sole],[bx,B.y,sole],[-bx,B.y,sole]])
```

Run in V8 (an instrumented copy — `volume` is closure-local — which is the one place in this lane a
rig was modified, and it is modified only to publish the builder it already ran):

| comparison | result |
|---|---|
| rig's ring vs **OUR committed polygon** | **identical, all 42 vertices** |
| rig's ring vs **the drop's polygon** | **38 of 42 differ**, x only, max **0.050 m** |
| `docs/art/rigs/` copy vs `hull-rigs/` copy | agree exactly — the root/kit split is not the cause |

The y values match exactly, so they used the same 21 stations; only x moved. The deviation is **zero
at the four knots of the volume's `hw` half-breadth table** (y −1.1, −2.5, −4.5, −8.75) and peaks
mid-segment — the signature of a linear interpolant. Reproducing the same law with `hw` interpolated
**linearly** instead of through the rig's `mono()` (a Fritsch–Carlson monotone cubic) matched the
drop's ring on **all 42 vertices to zero error**.

**Our polygon is kept.** It is the rig's own output; theirs is a reimplementation with the wrong
interpolant. This is the one number in the file the drop changed that was already right.

### 2.4 The new `helm_seats` note — kept, corrected

The drop adds a `helm_seats` obstruction with footprint ±0.25 and `top: 0.75`, flagged `_confirm`
for "seat halfwidth is nominal". Measured out of the rig's own face stream instead of transcribed
from the spec:

| | drop | **rig, measured** |
|---|---|---|
| x half-extent | ±0.25 | **±0.28** (AABB −0.900…−0.340 and 0.340…0.900) |
| y half-extent | ±0.25 | **±0.24** (AABB −6.140…−5.660) |
| top above the 9.74 sole | 0.75 | **0.60** (AABB z 9.740…10.340) |

Each seat is exactly six faces — `boxF([st, helmPod.z+0.30], [0.28,0.24,0.30])` over the spec literal
`helmPod.seats [[-0.62,-5.9],[0.62,-5.9]]`. The `_confirm` flag covered the halfwidth only; the top
was wrong and unflagged, and **the value the drop replaced already carried 0.60 correctly**, in prose,
inside `helm_pod`'s note. Corrected and the `_confirm` dropped, because the numbers are now measured
rather than nominal.

### 2.5 `helm_pod.height_above_floor_m.top` 0.78 → 0.83 — accepted

The rig builds the pod with `z0 = helmPod.z (9.74)`, `z1 = z0 + 0.78` and `cam: 0.05`. Their 0.83 is
the declared crown apex; our 0.78 was the box shoulder and ignored the camber. Accepted. For the
record the tessellated mesh in that region reaches **z 10.552 (+0.812)** — the crown's sampled points
sit just under its declared apex — and the screen brow above it tops out at +0.90.

⚠️ **This file is no longer byte-identical to what was sent.** A `_corrections_on_intake` key names
all three corrections inside the file itself, because its own `authoring` string says "do not
hand-edit — re-run the extractor", and a re-run that has not read that key will reintroduce every one.

---

## 3. The join — the test that had been waiting

#673 shipped the join rule (a rig's enclosed level must name a `BoatInteriorDef` level that exists)
and it was **vacuous on the cape**, because her rig published no levels to join. It is not any more:
her `capeIslander` row joins `Pass3Keys`, which is the single selector every fixture in
`HullLevelTagBakeTests` uses, so all twelve now cover the flagship.

The mapping is the rig's own: level vocabulary `house`/`cuddy`, deck ids `house_sole`/`cuddy_sole`,
and the two namespaces stay separate on purpose — conflating them is how a cutaway silently no-ops.

Three fixtures were added:

- **`TheCapesEnclosedLevelsNameRoomsHerInteriorSidecarActuallyHas`** — the join, asserted between the
  two files on disk (the rig in V8 against the interior sidecar's `WALKABLE` keys) rather than
  against a loaded asset, so it runs headless. #673's fleet-wide version loads a
  `BoatInteriorDef` and is editor-only.
- **`TheBakesLevelTable_AnswersForEveryEnclosedLevelAndRefusesTheOpenOnes`** — the row the gate reads,
  built by `RigMeshAssetBaker.LevelTableFor`. **Both arms**: an enclosed level must join to a deck id
  with a non-zero tag, and an open level must stay `Enclosed = false`, which is the field
  `CutawayForDeck` refuses on. "Every enclosed level opens" alone would pass on a table that opened
  everything.
- **`TheCapeIslandersHouseOpens_AndHerCuddyTakesTheForedeckWithIt`** — the end-to-end call:
  `CutawayForDeck("house_sole")` opens with **no** lid (her hard eave is an explicit veto), and
  `CutawayForDeck("cuddy_sole")` opens **taking the foredeck**, without which the cut removes nothing.
  Her two open decks refuse. ⚠️ Needs a live `ScriptableObject`, so it skips in the no-editor harness
  the way seven fixtures in that folder already do; the data under it is covered headless by the sweep
  above.

`RigMeshAssetBaker.LevelTableFor` was **factored out of `BakeOne`** for this — the fixture asks
through the bake's own mapping rather than transcribing its eight assignments into a test, where a
field dropped in the baker would go green.

### The skybridge guard, rewritten

`TheSkybridgeKeepsBothOfHerDecks…` asserted `bridge_sole` at 9.74 and the presence of `sky_sole` — a
**name test standing in for a geometry claim**, and it would have gone red on a split that fixes the
very thing it guarded. It now names neither id:

- exactly one walkable at z 7.30 with a 4-vertex polygon, exactly one at z 9.74 with 42;
- no id names two DECK entries (the collision law, stated as the law);
- **a ladder lands on the deck it names** (unchanged, and the general law);
- a companionway's opening is cut through the sole of the **UPPER** deck it connects — not "the deck
  it arrives at", which is only true climbing. This one was learned from a red: our own
  `house_to_below` correctly declares its opening in `house_sole`, and the first draft of the rule
  called that a contradiction.

`TheCoamingsOutlineIsBuiltFromVerticesTheRigActuallyEmits` is new, and is §2.3 made permanent: every
vertex of the coaming's polygon must be a point the rig actually emits on the 9.74 plane. A subset
test on purpose — the pod and the seats have bottoms on that plane too, and their vertices are no
business of the deck's outline.

---

## 4. Results

Fixtures compiled and run headless (`HullLevelTagBakeTests` + `BoatInteriorDefShapeTests`), branch
against a detached control at the base commit `6e7c43f2`:

| | pass | fail | skip |
|---|---|---|---|
| control (`6e7c43f2`) | 22 | 0 | 7 |
| this branch | **25** | **0** | **8** |

**28 shared tests, ZERO status changes.** Four of the five new fixtures pass; the fifth is the
`ScriptableObject` one and is a harness limit, not a result — the skip set is otherwise identical
name by name. One name left the list because it was renamed
(`…KeepsBothOfHerDecks…` → `…KeepsBothOfHerWalkables…`).

### Negative controls — five arms, each on its own run

| arm | expected | result |
|---|---|---|
| strip the cape's `house` lid | the bake REFUSES, does not default | **11 fixtures red** on `"…publishes no 'lid' … There is no stand-in table any more"` ✓ |
| cape `cuddy` lid → `cockpit` | the ledgers catch a wrong lid | 2 red (the provenance ledger and the lid-only-opens list) ✓ |
| reinstate the drop's coaming polygon | the new outline fixture catches the real artefact | **exactly 1 red**, naming the 38 stranger vertices ✓ |
| swap in the 2026-08-27 refused export | the rewrite has not weakened the HOLD | 2 red: `"no single walkable at z 9.74 — … The 2026-08-27 export deleted it and stranded them both"` ✓ |
| drop `cuddy_sole` from her `WALKABLE` | the join breaks visibly | exactly 1 red ✓ |

The third is the strongest: the fixture written about the drop's polygon demonstrably fails on it,
rather than merely passing today. The fourth caught a defect in the guard itself on its first run —
`Single()` answered the deleted coaming with `"Sequence contains no matching element"` instead of the
sentence written for it. A guard that cannot say what is wrong on its own artefact is half a guard;
both sites now count and report.

---

## 5. The pins — three moved, one of them stale

The cape's fourth sha. **Two conventions are live and both are correct**, each read from the file's
own bytes, exactly as #678 warned:

| file | field | was | now | convention |
|---|---|---|---|---|
| `docs/art/rigs/gameplay/capeIslanderIsoRig.gameplay.json` | `derivedFromRigSha256` | `e1004316…` | `1f2dd351…` | **working tree (CRLF)** |
| `docs/art/rigs/boat-interiors-kit/gameplay/capeIslanderIsoRig.gameplay.json` | `derivedFromRigSha256` | `a3be1d61…` ⚠ | `a1304e1b…` | **LF/blob** |
| `docs/art/rigs/boat-interiors-kit/capeIslanderIsoRig.interior.json` | `hullRigSha256` | `60d127c3…` | `a1304e1b…` | **LF/blob** |

`*.js` is stored LF in git and checked out CRLF under `core.autocrlf=true`, so the rig has two honest
digests: blob `a1304e1b34d3f347…` and working tree `1f2dd351bbb07327…`. Do not cross them.

⚠️ The middle row was **STALE**, and not by this drop: it pinned `a3be1d61…`, the LF hash of
UPSTREAM's pre-merge cape, which this kit has not shipped since #667 (`1286480b`) replaced
`hull-rigs/capeIslanderIsoRig.js` with the merged rig. #667 moved the rig and left the mirror behind —
the same omission #660 made with the two sport-fisher gameplay mirrors, one of which #678 fixed.
Corrected here since the file was being re-stamped anyway.

Both cape rig copies (root and kit) were already byte-identical and stay so. The kit copy moves too,
because the interior def's `HullRigSha256` pins it and
`EveryStampedHullPinResolvesToTheBundledRigItNames` would go red otherwise — it is green.

---

## 6. ⚠️ CI WILL BE RED, and correctly

`HullCutawayAssetTests.NoHullOutsideBatch1_HasARigThatOutranHerBake` asks whether a hull whose rig has
gained a vocabulary has been re-baked. **The cape now has one and has not been.** The test's own
message names the remedy:

> Cutaway batch 2 has arrived upstream and these hulls have not been re-baked, so their houses will
> never open … Re-bake them through the whole-fleet entry point.

That is this fixture doing its job, and it is the agreed two-part landing (the `814337f6` pattern).
**Owed to the coordinator's last-mile:**

1. Re-bake her hull mesh — she gains TexCoord1 level tags, a `LevelTags` table, and the tagged door leaf.
2. Re-import the decks — re-pins the cape and the skybridge, and **RENAMES** the skybridge's 9.74
   area `bridge_sole` → `helm_coaming`. ⚠️ *Nothing is gained and nothing moves to 7.30* — the
   original phrasing here (inherited from the handoff) was wrong: the deck def is built from the
   WIRED sidecar, which carries the five EXTERIOR decks only, and the 7.30 skylounge has never been
   a row in it. It is a level of the INTERIOR def and a deck of the merged kit mirror. Anything
   downstream that pinned that id by name should be read, not assumed.
3. Interior def builder — her `HullRigSha256` moves to the fourth sha `a1304e1b…`.
4. Patch-list commit (⚠️ `unity-runs-rewrite-boat-assets`: revert the churn from `git status`, never
   from a memorised list).

**The acceptance that matters:** after that lands, the owner's next launch opens below her decks with
her house CUT AWAY over the room — the #645 overdraw dies on the flagship, in the first minute of the
game. Screenshot it in the last-mile.

**Not in scope here:** sheet re-bakes, the intro's code (nothing changes there — the gate simply
starts answering), and the convertible.

---

## 7. Upstream asks

1. **Answer `_supersedes` against OUR bytes, not your branch's.** The drop's value named the export we
   refused on 2026-08-27. `_supersedes_convention` in the file says which digest to use.
2. **The coaming polygon: use `mono()`.** The `hw` half-breadth table is interpolated by the rig with
   a Fritsch–Carlson monotone cubic; a linear reimplementation is off by up to 50 mm mid-segment and
   exact at the knots, which is why it looks right at a glance. Better still, read the ring out of the
   sole faces the rig already emits rather than recomputing the law beside it.
3. **`helm_seats` is exactable, not nominal.** The rig's own `boxF` gives ±0.28 / ±0.24 and a 0.60 m
   top; the `_confirm` flag was covering a value that was simply available.
4. Receipts acknowledged: the lobster with the paint kit and our cape third sha are adopted upstream
   and the stale fork is deleted — the hand-restore #660 needed should not recur.

---

## 8. ⚠️ ADDENDUM 2026-08-28 — the split first landed in the WRONG FILE, and why the merge is the real story

The coordinator's last-mile ran green on the cape (`BakeFleetCli` 34 hulls, deck import 34/0, interior
defs 27/0) and then found this, correctly, before merging:

> the skybridge deck def came out of the import BYTE-IDENTICAL — no `helm_coaming`, no `bridge_sole`
> move.

**They were right.** The shipped deck def is imported by `DeckSidecarImporter` from
`docs/art/rigs/gameplay/` (`SidecarFolder`), and that folder names its files **by HULL** —
`sportFisherSkybridgeIso.gameplay.json` — so a search for `sportFisherIsoRig2*` never sees it. The
split had landed only in the kit's `boat-interiors-kit/gameplay/` mirror, and the PR body promised a
deck-def change that could not happen from there.

### The mechanism, and it is worse than a missed file

The kit mirror is not a sibling of the wired sidecar — it is **derived from it**:

> `BoatInteriorGameplayMerge`: "sections are additive against the hull's existing gameplay file:
> **DECK entries replace by `id`**, THRESHOLD/STAIRS/LADDER/INTERACT replace wholesale, `_excluded`
> merges."

Run over the two committed sources **as they stood**, the repo's own merge reports:

```
DECK 'bridge_sole': REPLACED the base entry (same id).
… 7 decks: cockpit · mezzanine · foredeck · bridge_sole@7.30 · aft_deck · house_sole · below_sole
```

The base named the OPEN coaming (9.74) `bridge_sole`; the interior sidecar names the ENCLOSED
skylounge (7.30) `bridge_sole`. **The merge deletes the coaming — which is upstream's refused
2026-08-27 export, reproduced exactly by our own code.** It was never carelessness; it was the merge
contract meeting a colliding id. Our committed mirror escaped it only by the hand-rename to
`sky_sole`, which no merge would reproduce and the next regeneration would have reverted.

### What the port actually changes

| file | change |
|---|---|
| `docs/art/rigs/gameplay/sportFisherSkybridgeIso.gameplay.json` (**the wired base**) | `DECK bridge_sole`@9.74 → **`helm_coaming`**; the reworded prose, `_excluded.bridge_access_interior`, `_confirm.upper_decks`, and the `_notes` (`helm_seats` in, the `bridge_ladder` annotation out — it is a `LADDER` record). **Polygon, z and winding untouched** — the base's ring was already the rig's own, vertex for vertex. |
| `docs/art/rigs/boat-interiors-kit/sportFisherIsoRig2.skybridge.interior.json` | `LADDER.bridge_ladder.connects → [aft_deck, helm_coaming]` and `INTERACT.helm_bridge.at → helm_coaming`. These four sections are taken **wholesale** from this file, so this is the only place the fix can live. `WALKABLE.bridge_sole` (7.30) and `STAIRS.house_to_bridge` are unchanged and were always right. |

Re-run over the ported sources:

```
DECK 'bridge_sole': appended (no base entry with that id).
… 8 decks, including helm_coaming@9.74 (42 verts) AND bridge_sole@7.30 (4 verts)
LADDER bridge_ladder connects = aft_deck, helm_coaming
INTERACT helm_bridge at = helm_coaming
```

Merged against the kit's pre-merged mirror: **zero DECK differences of any kind** — every id, z and
coordinate agrees. The 86 residual differences are all in `INTERACT` annotation fields (`anchor`,
`footprint`, `label`, `prompt`, `provenance`) that the drop's generator writes and the interior
sidecar does not carry, plus the lineage keys. That divergence is pre-existing and is the one
`BoatInteriorGameplayMerge`'s own doc says it cannot reproduce.

### The fixture that should have caught it

`TheSkybridgeKeepsBothOfHerWalkables…` reads the kit's **pre-merged mirror** — a *derived* file. It
stayed green while the collision lived on in the two sources it is merged from. New:
**`MergingHerTwoSources_AppendsBothWalkables_AndReplacesNothing`** runs
`BoatInteriorGameplayMerge` over the two committed sources and refuses **any** DECK `REPLACED` in the
report — stated as the id-collision law, not as "`helm_coaming` exists", so it covers every hull whose
interior contributes a deck. Negative control: reverting the id in the wired base alone reddens
**exactly** that fixture, with the merge report in the message. Headless: **26 pass / 0 fail / 8 skip.**

### ⚠️ One MORE red, newly owed

`DeckSidecarImportParityTests.EveryCommittedDeckDefMatchesItsSidecarVertexForVertex` asserts
`src.Id == baked.Id` per area. The committed `Data/Boats/Decks/SportFisherSkybridgeIso.asset` still
says `bridge_sole`; the sidecar now says `helm_coaming`. **Red until the deck import is re-run** — and
now it genuinely will change the def, which is what the PR body originally promised and could not
deliver.

### The pin question, answered rather than churned

The coordinator's import log notes both sport-fisher sidecars' `derivedFromRigSha256` match only
line-ending-normalised. That is **by design, not drift**: `DeckSidecarReader.MatchRigHash` tries the
exact digest first and then the same bytes with line endings flipped — "the one difference that
provably cannot move a vertex" — and its own doc says the normalised arm exists so "an older contract
that recorded the CRLF digest stays valid; this only changes what a NEW bake writes". **The rig has
not moved (`152eb5f3…` is still correct), so there is nothing to re-stamp.** Changing the convention
would force a deck-def re-bake for no gain, and the cape's own `_repin2` note records that switching
root-gameplay pins to LF was "explicitly overruled" as a separate standing ask. Left alone, deliberately.

---

## 9. CLOSED 2026-08-28 — the import, and one phrase corrected

`9379437d`: deck import 34/0, and the skybridge def moved by **exactly one line**, verified here
against the commit rather than taken on report:

```
-  - Id: bridge_sole
+  - Id: helm_coaming
```

Polygon, winding and area count untouched; still five areas
(`cockpit · mezzanine · foredeck · helm_coaming · aft_deck`). The def carries **no `z` per area at
all** — outlines only.

⚠️ **A phrase this record repeated was wrong, and the coordinator caught it.** "The deck def GAINS
`helm_coaming` and its `bridge_sole` row moves 9.74 → 7.30" came from the handoff and I carried it
into §6, the PR body and the hand-off summary without checking it against the file. The deck def is
built from the WIRED sidecar, whose DECK list is the five exterior decks; **the 7.30 skylounge was
never a row in it** — it is a level of the interior def and a deck of the merged kit mirror. The
change is a RENAME of one id, not a gain and not a move. §8's own port table said as much
("polygon, z and winding untouched"), which is exactly the tell: **the detail was right while the
summary was inherited.** A summary that restates a prediction instead of the measurement is worth no
more than the prediction was.

Cape half `dceaf997`, skybridge half `9379437d`. Merge on green.
