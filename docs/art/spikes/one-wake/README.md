# Where the three families spring from — register row 29

**Owner, 2026-09-06, in play:** *"the foam seemed off-centred with three different sections leaving the
boat."*

`SHEET-row29-one-wake.png` — three rows (headings **0°, 45°, 90°** through a turn to starboard), two
columns (**before**, **after**). Per panel: the cape's own footprint projected the way her art is baked,
her **transom** as a white ring, the advected sheet's root in **blue**, the sprite families' root in
**orange**.

## What it shows

**Both roots sit on her transom at every heading.** That is the claim — the wake springs from where the
boat ends, not from her middle — and it holds through the turn in both columns.

| heading | before | after |
|---|---|---|
| 0° | 0.129 m (0.03 beam) | 0.096 m |
| 45° | 0.168 m (0.04 beam) | 0.126 m |
| 90° | **0.200 m** (0.04 beam) | 0.150 m |

The "after" separation is not zero and should not be: it is the deposits' own **named 0.15 m nudge from
the shared root** (`WakeTrailConfig.DepositAsternOffset`), which is config, not a second opinion about
where the boat ends. What went away is the 0.05 m of *disagreement* — the cape's rig lofts 12.8 m where
her `BoatHullDef` says 12.9.

## ⚠️ What this plate is, and is not

- **It is a diagram of the shipped arithmetic driven by the shipped data.** The cape's `HullMeshDef` is
  loaded from the asset; the roots are computed by the production functions (`FoamBuffer.SternWorld`,
  `WakeGrading.SternAnchorFromRoot`). **It is not a photograph of rendered foam.** A rendered plate needs
  a live boat with her emitter, her pools and the foam buffer's render feature in a PlayMode scene; that
  fixture is not built, and saying so is cheaper than a picture that overclaims.
- **The root correction is small and looks it.** 0.20 m on a 12.8 m boat is about 2.5 px at this scale.
  The plate is here to show the roots are *at the transom and together*, not to dramatise the number.
- The gap that leaves — *do the emitter's call sites actually use the one root?* — is closed by a source
  tripwire (`TheEmittersSternAnchors_AllGoThroughTheOneRoot`), not by the picture.

## ⚠️ The track is NOT in this plate, because there was nothing to draw

The charter's third ask was "one track". Measured, there was never a positional disagreement: deposits
land at `PointOnTrack(prevStern, stern, t)` — the transom's own swept path, the exact segment the buffer's
capsule lays on, **0.0000000 m apart at every swing fraction**. See `water-rendering.md` §40.3, including
the correction of a number this lane got wrong on the way.

## Re-running it

```
"C:/Program Files/Unity/Hub/Editor/6000.5.0f1/Editor/Unity.exe" -runTests -batchmode -projectPath <worktree> -testPlatform EditMode -testFilter HiddenHarbours.Tests.EditMode.OneWakeRootPlateTests -testResults <abs>/row29.xml -logFile <abs>/row29.log
```
