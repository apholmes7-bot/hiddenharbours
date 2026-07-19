# The art director's parametric rigs — the SOURCE the sheets are baked from

These are the art director's own `.js` rigs, imported **verbatim**. They are pure procedural renderers
with no external asset dependencies: each exposes `render(dir, opts)` returning a `Uint8ClampedArray` of
RGBA, plus metadata (`W, H, order, ANIMS, BUILDS`, anchors, …).

**Why they live in the repo.** Under [ADR 0021](../../adr/0021-in-engine-js-rig-baking.md) sprite sheets
stop being hand-exported from a browser and become an **editor operation** — the baker runs these files
directly. Versioning the rig alongside the code means any hull can be re-baked at any facing count,
forever, without another art hand-off.

⚠️ **Do not edit these files.** They are the art director's source. Fixes belong upstream, or the next drop
silently reverts them. Anything the engine needs that the rig doesn't provide belongs in *our* host code.

---

## ⚠️ THE AZIMUTH SPLIT — read this before baking anything

The rigs do **not** share one facing convention.

> ⚠️ **CORRECTION (2026-07-19, from the first real bake).** An earlier version of this file said the split
> was determined by inspecting the sign of the `th = ±dir*Math.PI/4` term. **That method is NOT reliable and
> must not be used.** `puntIsoRig` and `lobsterBoatIsoRig` both carry a **positive** sign and both render
> **counter-clockwise** — handedness comes from the iso camera basis, not from that sign alone. The sign
> merely *correlated* with the answer on the rigs that had been pixel-checked.
>
> **Only measurement is authoritative.** The groups below are trustworthy where the art was measured
> (characters via face-skin centroid; punt/dory/Cape Islander via PCA bearing + bow-taper; punt again via a
> byte-identical golden-master bake). **Every other rig here is UNVERIFIED** — the baker must measure it at
> bake time, not read this list. Treat the list as a prior, never as a fact.

**CLOCKWISE-CORRECT (2)** — the art director fixed these at source; character sheets pixel-verified:
`characterIsoRig.js` · `rodIsoRig.js`

**COUNTER-CLOCKWISE (19)** — cell `i` depicts heading **−45°·i** while labelled `+45°·i`.
Pixel-verified: `puntIsoRig` (golden master, byte-identical), `doryIsoRig`, `capeIslanderIsoRig`,
`lobsterBoatIsoRig`. The rest are inferred and must be measured before use:
`bucketRig` · `capeIslanderIsoRig` · `coastalPacketIsoRig` · `consoleIsoRig` · `doryIsoRig` · `fishTubRig`
· `houseIsoRig` · `interiorIsoRig` · `interiorPropRig` · `lobsterBoatIsoRig` · `puntIsoRig` · `shovelIsoRig`
· `sideDraggerIsoRig` · `skiffMotorRig` · `sportSkiffIsoRig` · `sternTrawlerIsoRig` ·
`sternTrawlerMk2IsoRig` · `tankerIsoRig` · `wharfBuildingRig`

**No azimuth term (18)** — kits, props and creatures that aren't 8-way directional; they need no
convention. (`sceneKit`, `shorelineRig`, `potRig`, `foxRig`, …)

⇒ **The baker MUST carry a per-rig convention flag. A blanket correction is wrong** — it would re-mirror
the two already-correct rigs. And the flag must be *machine-verified against the rendered pixels*, not
maintained by hand: this mislabel has now caused defects in five separate kits, every time because someone
trusted a declared order instead of measuring the art. Once a rig is baked in-engine with its convention
applied, `FacingsAreCounterClockwise` goes **false** for that artwork — the flag survives only for legacy
hand-exported sheets until they are re-baked.

---

## ⚠️ These differ from the previously-imported copies

`puntIsoRig.js`, `consoleIsoRig.js`, `sportSkiffIsoRig.js` and `skiffMotorRig.js` were already in the repo
under `docs/art/punt-iso-rig/` and `docs/art/skiff-fleet-rigs/`. **The versions here differ** (md5 mismatch
on all four) — these came from the art director's live project folder and are newer.

The older per-kit copies have been removed so there is ONE canonical location; their `README.txt` files are
kept, since they document the shipped kits' cell sizes and pivots.

**Consequence for the golden-master test:** the first baker acceptance test bakes a hull and diffs it
against the sheet already shipped in `Assets/_Project/Art/Boats/`. If the punt does not match, the likely
cause is that the shipped art was baked from the *older* rig, not that the baker is wrong. Establish which
before chasing a phantom bug.

## Boats that exist only as a rig

No baked sheets exist for these — they can only ship once the baker does:
`lobsterBoatIsoRig` (Tier 3, ~12.0 m) · `coastalPacketIsoRig` · `sideDraggerIsoRig` ·
`sternTrawlerIsoRig` · `sternTrawlerMk2IsoRig` · `tankerIsoRig`

Most are M2/M3 fleet content — importing the source is **not** a licence to wire them (CLAUDE.md rule 8).
