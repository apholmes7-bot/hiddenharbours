# Boat-interiors intake — the S0 adjudication and what it cleared

This folder belongs to the **intake**, not to the drop. The drop is at
`docs/art/rigs/boat-interiors-kit/` — landed verbatim at `d2950cef` and **superseded in place by the
re-export at `74be6b70`** — and nothing here modifies it.

> ## Current state, after the re-export (2026-08-19 evening)
>
> **24 of 27 sidecars CLEAR · 2 refused · 1 forked.** The re-export re-stamped every sidecar to the
> renderer that shipped, which cleared the 24 — and, in the same pass, shipped an unsubstituted
> generator template into the two hulls that were already clean.
>
> | verdict | hulls | what it needs |
> |---|---|---|
> | **CLEAN** (27 sidecars, 10 families) | capeIslander · coastalPacket · lobsterBoat · lobsterBoatVariants ×18 · sideDragger · sportFisher ×2 · sternTrawler · sternTrawlerMk2 · tanker | nothing |
> | ~~**REFUSED-PIN** (2)~~ | ~~both sport fishers~~ | **DISCHARGED 2026-08-26** — the cutaway kit shipped the substitution (PR #660) |
> | ~~**FORKED-RIG** (1)~~ | ~~capeIslander~~ | **DISCHARGED 2026-08-27** — the rig merge landed as the third sha `60d127c3…` |
>
> **Every hull in the drop is now cleared.** The two refusal rows are struck through rather than
> deleted: the ledger records history, and each discharge is a dated `_corrections` entry in
> `s0-verdicts.json`.
>
> Every claim below was verified against the bytes on this branch, never accepted from a report.

Read `../boat-interiors-kit/VERIFICATION.md` first — it is the coordinator's gate report and it is
what set this work. `s0-verdicts.json` beside this file is the answer, as data the builder reads.

## What S0 asked

> No interior def may be built for a hull until its baseline is adjudicated.

Two independent axes decide it, and a hull needs **both**:

| axis | question | refusal rule |
|---|---|---|
| **A — interior-rig pin** | Is `derivedFromRigSha256` the LF sha256 of the `boatInteriorRig.js` that actually shipped in the kit? | A pin naming a renderer that is not in the drop cannot be checked against anything. Unverifiable is refused, exactly as absent is. |
| **B — hull baseline** | Establish the exterior rig the rooms were measured against, diff main's current rig against it, and classify every hunk. | Geometry the interior actually READS (what the rig's published `loft` exposes) moving is fatal. Paint, materials, cues, comments — and exterior surface outside the loft — are not. |

## How the baselines were established

The drop's `_supersedes` stamps behave three ways (the gate report's table), so the stamp alone
could not settle six of the nine families. Every version of all nine hull rigs was therefore hashed
across the **entire** repository history — 588 commits, all refs — and matched by LF sha256.

Two facts fell out, and together they settle every hull:

1. **Seven of the nine rigs have exactly ONE version in the whole history.** coastalPacket,
   lobsterBoatVariants, sideDragger, sportFisher2, sternTrawler, sternTrawlerMk2 and tanker have
   never changed. Whatever their interiors were measured against, **main cannot have drifted out
   from under them** — which resolves the self-referential stamps (dragger, both trawlers) and the
   absent ones (packet, tanker) without needing the stamp at all.
2. **The two rigs that DID move are exactly the two whose stamps point backwards.** capeIslander and
   lobsterBoat both stamp the blob at `4f048395` (2026-07-19) — verified present and matching. So
   the drop's provenance discipline was, in fact, correct precisely where it mattered.

Hash in a POSIX shell, never PowerShell:

```sh
tr -d '\r' < path/to/rig.js | sha256sum
```

## The two hulls that moved

- **lobsterBoatIsoRig** — one commit since baseline, `4be92de9` (#497, the 12-scheme paint kit).
  +97/−8, entirely `PAINTS`/`paintRamps`/`matsFor`, one extra parameter on `_paint`, and a longer
  export list. Geometry, cell, pivot, camera, rock and every deck anchor untouched; the default
  `gelcoat` scheme reuses the original literal ramps. **Axis B clean.**

- **capeIslanderIsoRig** — two commits. `1b35453d` (#508) is paint. `be83274b` (#247, 2026-07-22)
  moved washboard geometry: side decks that stopped at the house front now run the full sheer to the
  foredeck, their inboard edge clamped against the **house** half-width. That was an owner ruling —
  *"capes washboards go all the way to foredeck"*.

  ### ⚠️ Correction (2026-08-19, same day): she is FORKED, not stale

  The observation above is right; the **verdict first drawn from it was wrong, twice**, and upstream
  caught both. Recorded rather than quietly rewritten — provenance is the point.

  1. **The washboards are not measured geometry for the interior.** The kit rig publishes
     `loft:{ station, skin, dfrac, halfAtZ, sheerZ, L, TH, DECK, SOLE_U, NSEG, house:HOUSE, shade,
     cell }`, and `WB` appears **only** inside the washboard face-emission loop (lines 191/197/198),
     never in that loft. #247 moved exterior deck surface the interior rig cannot read.
  2. **"Re-measure against main's rig" was not merely unnecessary — it was impossible.** Main's rig
     publishes no `loft`, no `HOUSE`, no `halfAtZ`, no `sheerZ` (grep count **0** for each), and
     `boatInteriorRig.js:268` reads `if(!E || !E.loft) return null;` with `list()` filtering on that.
     Adopting main's rig as the hull rig would **delete the cape from the kit**, not re-measure her.

  **What she actually is: two "pass 2" forks of different features that never merged.**

  | | publishes loft/HOUSE | aft DOOR | #508 OKLCH paint | #247 washboards |
  |---|:--:|:--:|:--:|:--:|
  | repo `92c3061b` (33,162 B) | ✗ 0 refs | ✗ | ✅ 9 refs | ✅ |
  | kit `a3be1d61` (28,482 B) | ✅ 3/6/2/2 refs | ✅ | ✗ 0 refs | ✗ |

  **Her rooms are SOUND.** Every input they are measured from is identical across both branches —
  `L=12.8`, `TH=0.05`, `DECK=0.72`, `NSEG=24`, `ROOFZ=3.02`, `SOLE_U=0.74`, `HX=1.32`, `HY0=0.5`,
  `HY1=2.9`, `HZ1=2.98`, `FYb=2.54`, `FYt=3.10` (same values; the kit hoists `FYb`/`FYt` to module
  scope so `HOUSE` can read them), plus `station()`, `skin()`, `dfrac()` and `dw()` verbatim. `HOUSE`
  is built only from those constants; `halfAtZ`/`sheerZ` only from `station()` and `L`.

  **So the remedy is a RIG MERGE, not a re-measure**: main as base, the aft door and the published
  loft re-applied, producing a **third sha that must land in `docs/art/rigs/capeIslanderIsoRig.js`** —
  not only in the kit. That is an owner call, because it rewrites a committed fleet rig. Until it
  lands she stays refused, under her own verdict word `FORKED-RIG`, and
  `hull-rigs/capeIslanderIsoRig.js` carries a **do-not-adopt** flag: adopting it reverts #247 and
  #508; adopting main's deletes her from the kit. Neither file can be taken whole.

  > **✅ DISCHARGED 2026-08-27.** The merge landed as the third sha `60d127c3…`, and the
  > **do-not-adopt flag is lifted**: `hull-rigs/capeIslanderIsoRig.js` and
  > `docs/art/rigs/capeIslanderIsoRig.js` are now the same bytes, so there is no longer a wrong
  > one to adopt. The paragraph above is kept as the record of why the merge was needed.

## The cape rig merge — ruled, and its acceptance bar

**Sequencing (owner, 2026-08-19): a SEPARATE PR, after the intake lands.** This intake is complete
and safe without it — she is refused, and the refusal arms hold.

**Acceptance bar (lead-architect, 2026-08-19), verbatim in substance:**

1. **Repo as base.** Diff merged-vs-repo-main classifies to EXACTLY: door addition + loft/HOUSE
   publication. Zero hunks touch #508's OKLCH paint tables or #247's washboard geometry — assert by
   classification, not by eye.
2. **The interior-measured inputs** (`T`, `L`/`TH`/`DECK`, `NSEG`, house envelope, `ROOFZ`,
   `SOLE_U`, `station()`, `skin()`, `dfrac()`, `FYb`/`FYt`) byte-identical to **both** parents. The
   parents already agree (proved above); the merge must not break that.
3. **The pixel proof:** exterior sheets from the merged rig, rendered in the V8 harness, byte-identical
   to main's for the shipped poses. ⚠️ **See the amendment below — as written this cannot hold.**
4. **The third sha lands as ONE repo PR:** rig + deck sidecar re-stamp + deck asset re-import, in a
   real Unity session, both suites green including `MatchRigHash` and the parity tests.
   ⚠️ **Re-stamp in the convention the deck import actually compares.** Her existing pin is a CRLF
   hash of an LF file (`425ff37d…`) and `DeckSidecarImportParityTests` asserts string equality —
   **match that convention, do not switch it to LF in this PR.** (The LF re-stamp is the gameplay
   README's separate standing ask; an intake suggestion to fold it in here was explicitly overruled.)
5. **The kit's cape interior sidecar then re-stamps `hullRigSha256`** to the third sha — no
   re-measure, per the byte-identity proof above.

### Point 3, as RULED (lead-architect, 2026-08-19)

The bar's original point 3 — "exterior sheets byte-identical to main's, any diff = refused" —
conflicted with its own point 1, which *requires* a door addition. The door is exterior-visible by
construction:

| | main `92c3061b` | kit `a3be1d61` (and any merge carrying its door) |
|---|---|---|
| aft doorway | ONE flat `'dark'` panel — an **open** doorway (`backPanel(AY,-0.34,0.40,HZ0+0.02,2.34)`) | cream jambs + header, `'wood'` sill, `'moto'` track tube, `'iron'` rail |
| the leaf | none | a sliding leaf posed by `doorOpen` (`doorFaces()`) |

**The ruled point 3 is therefore:** byte-identity on every exterior pixel **outside the aft
doorway's bounding box** across all shipped poses, **plus** full byte-identity on the **bow-on
facings (N/NE/NW)** where the house occludes the aft face entirely.

> ### ⚠️ The second clause was wrong, and could not have been met (merge lane, 2026-08-27)
>
> Measured in the V8 harness over all 72 shipped poses, **the facings are inverted**: the aft face
> is *visible* on dirs 0/1/7 — the ones labelled N/NE/NW — and *hidden* on dirs 3/4/5 (SE/S/SW).
>
> | dir | 0 N | 1 NE | 2 E | 3 SE | 4 S | 5 SW | 6 W | 7 NW |
> |---|---|---|---|---|---|---|---|---|
> | px changed by the door | 10,675 | 7,848 | 1,403 | **5** | **3** | **112** | 863 | 7,791 |
>
> The labels are nominal — `CapeIslanderSheetSliceTests` already warns that *"the art is baked
> counter-clockwise"*. So "full byte-identity on N/NE/NW" asked for zero change on the three
> facings that show the door most, which **no correct merge could deliver**. And even the genuinely
> stern-hidden facings are not perfectly zero: 120 px over 27 poses, all recolour, no silhouette
> change.
>
> **What the merge was held to instead**, and met: *zero differing pixels outside the **aft wall***
> — the face the doorway is cut into — across all 72 poses. That is the same guarantee the clause
> was reaching for (#508's paint touches every hull pixel, so a paint regression cannot hide behind
> it; #247's washboards are side-deck geometry well outside it), stated on a bound that is real.
> The tighter door-assembly box holds all but 38 px, and those 38 are attributable by staged
> measurement to the aft wall becoming three bands — the opening itself.

That is sufficient, and this is why: #508's paint touches every hull pixel, so a paint regression
cannot hide inside a doorway box; #247's washboards are side-deck geometry well outside it. Both
survivals stay proven, and the door is allowed to exist.

### Open, promoted to the owner: her resting look

Not a regression — a look, and therefore his call. **It cannot be settled by picking a `doorOpen`
for the resting pose**, which was the hope; measured against the rig's own numbers:

| resting pose | the opening (−0.34 … +0.40) | the leaf | the surround |
|---|---|---|---|
| main today | open, one flat dark panel | none | none |
| `doorOpen = 0` | **covered** — she rests closed | spans −0.40 … +0.46 | always drawn |
| `doorOpen = 1` | clear, as today | **parked, visible** at +0.40 … +1.26 (inside `HX`=1.32) | always drawn |

The jambs, header, sill, track tube and rail are `doorOpen`-independent — they are drawn at every
value. So the aft face changes whichever pose ships; `doorOpen = 1` changes it *less* (the opening
still reads open, as today) at the cost of a parked leaf that main does not have. The owner is
choosing **how** she changes, not whether.

## The re-export (commit `74be6b70`) — verified, not taken on trust

The coordinator's gate report is appended to the kit's own `VERIFICATION.md`. Everything it claims
was re-checked here against the files:

| claim | result |
|---|---|
| renderer LF sha `34bb7813…` | ✅ verified |
| all 27 interior sidecars pin it, zero strays | ✅ verified — the `560aa92e…` phantom is gone |
| companionway ruling applied | ✅ 0 stair ids remain in any `INTERACT` |
| all 9 bundled hull rigs byte-identical to drop 1 | ✅ verified via `git show d2950cef:…` |
| gameplay-vs-hull-rig 27/27 | ✅ true — **of the gameplay sidecars, which is what it checked** |
| committed kit == delivered zip | ✅ identical; only `VERIFICATION.md` is repo-side |

Because the nine hull rigs are byte-identical to drop 1, **every Axis B finding in this document
still stands unchanged** — which is what makes clearing the 24 a re-stamp question and not a
re-measure.

### ⚠️ What the gate missed: an unstamped stamp on the two hulls that were already clean

Both sport-fisher **interior** sidecars gained a *second* `hullRigSha256` entry whose value is the
literal generator template:

```json
"hullRigSha256": {
  "sportFisherIsoRig2.convertible": "STAMP_AT_EXPORT_LF_SHA256_OF_sportFisherIsoRig2.js",
  "sportFisherIsoRig2":             "ebc77bace833361b578f5315e175e10de61d1acf77b5037390217ceb09221bcb"
}
```

The gate's "27/27" is true of the **gameplay** sidecars; the **interior** sidecars' hull pins were
not checked on that axis. It is present in the delivered zip, so it is upstream, not the landing.

**An unstamped stamp is worse than an absent one** — it occupies a provenance field looking like a
value — so the reader now has an arm that refuses it *by name* and quotes the placeholder, because
the fix is one substitution and a vaguer message would send somebody hunting a geometry problem that
does not exist. Two tests pin the regression from both sides: it must not spread, and it must not
vanish silently either (when upstream substitutes it, the test fails and gets updated in the same
change that re-clears those hulls).

**Net: the re-export cleared 24 and broke the 2 that were already good.**

### Also found: the resolver spoke only one of two variant conventions

Not upstream's — mine, and it would have been invisible. The eighteen lobster variants have **no**
`variant.hull` field; they identify by a `{size, style, region}` triple, where the sport fishers use
a single `hull` string. The resolver knew only the latter, so all eighteen failed to resolve and
would have been **refused while adjudicated clean** — 6 defs built instead of 24, reported as a
refusal rather than as a bug. `BoatInteriorHullResolver.VariantKeyOf` now canonicalises both
(`paint` deliberately excluded — it does not move a bulkhead), and a test resolves all 27 against
the real committed catalogue and asserts 27 unique def ids.

## The phantom renderer

`README.md §1` of the kit claims the interior renderer's LF sha256 is
`560aa92e28d27719577ed07efec212ede9b33f58636fe5770836f8f729d260ae`. The shipped file hashes to
`3297674f4802c366d11d0813a75ca378bc21a698f2e2e4dda55dccd8b0ae28b1`. The `560aa92e` sha is **not**
that file, **not** the copy inside the delivered zip, and **not** any blob in the repository's
history.

**25 of the 27 sidecars pin `560aa92e`.** Only the two sport fishers pin what shipped. The kit
README's claim that tranche 4 regenerated all sidecars — *"earlier hashes are superseded, not
mixed"* — is exactly backwards.

This matters most for the **18 lobster variants**: the README says tranche 4 changed the rig's
"variant-aware envs + parametric variant layout", which is the very code that lays those 18 out,
yet all 18 pin the pre-tranche-4 renderer. There the stale pin is substantive, not clerical.

## Result

**2 of 27 sidecars cleared** — `sportFisherIsoRig2.convertible` and `sportFisherIsoRig2.skybridge`.
Seven families pass axis B and fail axis A. The cape is her own case: her rooms are sound on both
axes' *substance*, and she is refused because the rig she is measured against cannot land in the
repository as-is.

Every refusal carries an `upstream_ask`, and the three classes want **different** work:

| class | hulls | what upstream must do |
|---|---|---|
| `REFUSED-PIN` | 7 families (+18 variants) | Re-stamp `derivedFromRigSha256` from the renderer that shipped. Clerical. |
| ~~`FORKED-RIG`~~ | ~~capeIslander~~ | **DONE 2026-08-27.** The rig merge landed: repo main as base, the aft door and published loft re-applied, third sha `60d127c3…` in both `docs/art/rigs/` and the kit. |
| `CLEAN` | both sport fishers | Nothing. |

Naming these apart is the point of having three words instead of two: telling upstream to
"re-measure" a forked hull sends them to do work that would not help and, in the cape's case,
cannot be done at all.

## A separate warning: do not adopt the bundled hull rigs

The nine `hull-rigs/*.js` in the kit are cut from the 4f048395-era sources plus the interior
publishing — they are **not** rebased onto main. Verified by diff:

- Seven are cleanly additive: they remove a painted-on `frontPanel`/`backPanel` "deck door" and the
  wall face behind it, and publish real DOOR geometry in its place. `lobsterBoatVariantsIsoRig`
  keeps its paint kit intact and adds `doorFaces(V,t)`.
- **`lobsterBoatIsoRig` has no paint kit** — replacing main's would revert #497's 12 schemes.
- **`capeIslanderIsoRig` reverts BOTH #247's washboards and #508's paint** — and, unlike the
  others, the reverse swap is no better: main's copy publishes no loft, so adopting *it* drops
  her out of the kit entirely. She needs the merge described above, not a choice between two
  files. Her copy of main's rig is staged upstream at `scraps/capeIslanderIsoRig.repo.js`.

Nothing in this PR copies a bundled rig over `docs/art/rigs/`.

## The merge, checked against the drop's own

`BoatInteriorGameplayMerge` implements the manifest's sentence — DECK replaces by `id`,
THRESHOLD/STAIRS/LADDER/INTERACT replace wholesale, `_excluded` merges — and **writes nothing**;
`docs/art/rigs/gameplay/` is art-director's lane and is pinned by `DeckSidecarImportParityTests`.

Applying that rule to (main's committed sidecar + the interior sidecar) and comparing against the
kit's own pre-merged file, for both cleared hulls:

- **Every coordinate is identical** — DECK polygons and z, THRESHOLD, STAIRS, LADDER all match.
- **`_excluded` matches** on the convertible.
- Three differences, all in the drop's favour of extra annotation or against the documented rule:
  1. The shipped merge writes a prose `note` on each interior DECK entry. That prose is **not in the
     interior sidecar** — it is generator-side, so no merge computed from committed inputs can
     produce it.
  2. The skybridge's shipped `_excluded` carries a `skylounge` key present in **neither** committed
     input — same class as the prose above.
  3. The shipped merge **drops `companionway_up` and `companionway_down` from INTERACT**, where
     "replace wholesale" keeps them. Both hulls. Flagged for upstream: either the rule or the
     generator is wrong, and the STAIRS section arguably already carries those two.

Also noted, not refused: `cell.levels` lists the baked SHEETS (`["house","below"]`) while `WALKABLE`
lists the standable planes (three, including `helm_deck`). Different things — the helm deck is a
riser inside the house volume — but the names invite conflation.
