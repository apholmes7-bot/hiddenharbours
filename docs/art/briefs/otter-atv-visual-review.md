# Otter and ATV visual review — 2026-09-13

**Owner:** art-director. **Request:** owner-directed review, aesthetic improvements and missing details.
**Pillars:** P3 Living Working Coast and P4 Earn It Then Automate It: readable, cared-for working machines.
**Scope:** Otter 8x8, Enduro 250, Trike 200 and Utility Quad source rigs and their reference packs.

The review found hard rectangular upholstery, blank tanks and controls, texture-only Otter hatch
vents that disappear in mesh extraction, and an understated Otter yaw bound. Both HTML harnesses
also requested sidecars beside the rigs, although the repository stores them in `gameplay/vehicles`.

The revised rigs soften seat edges while keeping the rider anchor envelopes, add restrained
mechanical details in the existing palette, and carry the hatch vents as geometry. The optional
parts, gameplay dimensions and exported interfaces retain their existing meanings.

## Acceptance and evidence

- Both existing HTML harness assertion suites pass under Node/V8 with a Canvas/DOM adapter and
  file-backed fetches resolving the repository paths: Otter 42/42; ATV 77/77; no skipped JSON checks.
- Comparison with the base revision covers 296 facing/pose cases: anchors and dimensions unchanged,
  deterministic RGBA, binary alpha, no clipped sprites, and unchanged per-case bounding boxes.
- A 1.125° yaw sweep measures Otter bounds `[75,65,180,155]` on both versions. The former contract
  understated this as `[76,65,179,154]`; the corrected measurement is 106 × 91 pixels.
- All 29 manifest reference sheets are regenerated from the revised sources. The sidecars retain
  their gameplay values and receive matching source SHA-256 stamps. Source line endings are pinned.
- [Before/after plate](../rigs/atv-pack/reference/OtterAtv_review.png) compares identical options and
  integer-scale crops under the same lighting, with front and rear quarter views of each machine.

Default-build geometry cost (source faces, before triangulation):

| Machine | Before | After | Distinct material ramps |
| --- | ---: | ---: | ---: |
| Otter | 1,296 | 1,399 | 16, unchanged |
| Enduro | 502 | 573 | 9, unchanged |
| Trike | 574 | 643 | 9, unchanged |
| Quad | 663 | 762 | 9, unchanged |

## Integration

This is a source-art PR. The owner/art-pipeline must rebuild the Otter and three ATV vehicle meshes
after merge, then inspect a parked and ridden machine in Unity. No Unity editor slot was used;
the source checks do not claim an in-game mesh bake or player playtest. The existing optional Otter
canopy ramp-budget exception and separately authored rider poses are unchanged.
