# Boat-interiors intake — the S0 adjudication and what it cleared

This folder belongs to the **intake**, not to the drop. The drop itself is at
`docs/art/rigs/boat-interiors-kit/`, landed verbatim at `d2950cef`, and nothing here modifies it.

Read `../boat-interiors-kit/VERIFICATION.md` first — it is the coordinator's gate report and it is
what set this work. `s0-verdicts.json` beside this file is the answer, as data the builder reads.

## What S0 asked

> No interior def may be built for a hull until its baseline is adjudicated.

Two independent axes decide it, and a hull needs **both**:

| axis | question | refusal rule |
|---|---|---|
| **A — interior-rig pin** | Is `derivedFromRigSha256` the LF sha256 of the `boatInteriorRig.js` that actually shipped in the kit? | A pin naming a renderer that is not in the drop cannot be checked against anything. Unverifiable is refused, exactly as absent is. |
| **B — hull baseline** | Establish the exterior rig the rooms were measured against, diff main's current rig against it, and classify every hunk. | Loft/house/door geometry that MOVED is fatal. Paint, materials, cues and comments are not. |

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
  **moved washboard geometry**: side decks that stopped at the house front now run the full sheer to
  the foredeck, their inboard edge clamped against the **house** half-width. That was an owner
  ruling — *"capes washboards go all the way to foredeck"*. **Axis B fatal.**

  Worse than stale: the kit's own bundled `capeIslanderIsoRig.js` still carries the pre-#247 line
  verbatim (`if(station(u0).y > HY0-0.05) continue;   // stop at the house front`), so adopting the
  bundled rig would silently revert the ruling.

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
Seven families pass axis B and fail axis A; the cape fails both. Every refusal carries an
`upstream_ask` naming the fix. For seven families that fix is a re-stamp; for the cape it is a real
re-measure against main's current loft.

## A separate warning: do not adopt the bundled hull rigs

The nine `hull-rigs/*.js` in the kit are cut from the 4f048395-era sources plus the interior
publishing — they are **not** rebased onto main. Verified by diff:

- Seven are cleanly additive: they remove a painted-on `frontPanel`/`backPanel` "deck door" and the
  wall face behind it, and publish real DOOR geometry in its place. `lobsterBoatVariantsIsoRig`
  keeps its paint kit intact and adds `doorFaces(V,t)`.
- **`lobsterBoatIsoRig` has no paint kit** — replacing main's would revert #497's 12 schemes.
- **`capeIslanderIsoRig` reverts the washboard ruling**, as above.

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
