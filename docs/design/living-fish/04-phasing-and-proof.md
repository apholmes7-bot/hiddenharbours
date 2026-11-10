# Living fish: phasing and proof

The milestone order comes from [the roadmap](../../roadmap.md), especially M1 at line 165, M2 at 228, M3 at 299 and the performance/save cross-cutting tracks at 397. The current M1 specification is the St Peters scope linked from the roadmap; the older Coddle Cove narrative is not authority to restore the retired inherited-boat opening.

| Piece | Phase and boundary |
|---|---|
| Baseline audit and behaviour/handling/water design | M1 planning now. Records existing gaps without changing the look or accepting a plate. |
| Disabled Def profile plus engine-light SchoolField | M1 foundation under this charter. Code and tests only; not a new population running beside the existing sonar/bite model. |
| Minimal certified habitat adapter and one controlled consumer switch | Proposed M1 follow-up only within the current slice, after lead-architect/world-content review. No new region, shader or animation work hidden inside activation. If unavailable, keep the feature disabled. |
| Broader ecological authoring, connected inshore daily/tidal routes and bounded hull/mate/lure reactions | M2 living coast. Existing per-cell patrols are not evidence of migration or a full animal AI. |
| Vitality, supported deck/hand/container placement, release, hooked reservation and save migration | M2 handling/logistics follow-up, respecting manual loading/carrying/unloading. Fixing a current catch-integrity bug remains a present-slice fix; PR 1033 owns its existing controller work. |
| Bed-following crawlers and sessile shell response | M2 regional ecology/art follow-up, only where actual habitat/support data exists. No free-swimming shellfish. |
| Shader transmission/refraction path, projected bed shadows and new feeding effects | Water/art owners' rendering roadmap. This charter requests seams; it does not move advanced rendering into M1 or authorize art changes. Existing approximate swimmers remain the baseline. |
| Offshore schools, nets/trawls and bulk/crew handling | M3 with offshore gear and staff systems. No nets, stock depletion, aquaculture production or automated transport added now. |
| Mussel/oyster leases and grown crop | Retain M3's existing lease ruling unless separately rephased. A wild sessile field is not a lease implementation. |

The proposed backlog is [proposed-living-fish](../../../backlog/proposed-living-fish.md). These are scoped proposals with owners and acceptance criteria, not edits to the main roadmap or approved commitments. The decision sheet in [09-owner-questions](09-owner-questions.md) records recommended defaults and does not block this run.

## Model proof

Code draft PR: #1041, `feat/living-fish-school-field`, corrected head `03afc0a653317aece36c080c8a8dbb5dfe5537d8`.

The new SchoolFieldTests has 24 cases and the new LivingFishContentValidationTests has one. Tests pin call-order independence, seed/species/cell/region identity, continuous centre paths including long saves, the species depth interval, complete footprint containment inside a certified disc, known/unknown vegetation preference, bounded scatter and exact re-form endpoints, stationary sessile occupants, bounded caller buffers, malformed inputs, single-precision coefficient budgets and zero steady-state managed allocations. No existing assertion or pixel expectation is changed. They do not prove actual habitat authoring, individual steering, crawler support, renderer sorting or a GPU budget.

Fishing and the broad EditMode assembly compiled offline against the pinned editor response files with zero errors. One stale source entry in the cached broad-test response file was absent from the baseline working tree and omitted from that evidence-only compile. The Unity test run will use fresh import/discovery, not that adjusted response file. No content asset is edited, and all current species remain disabled for this model.

A supplementary console harness linked the actual field/profile/hash source and Unity's managed Vector2, with a plain data stand-in for the native species asset. It passed 9,963 motion/depth/habitat/scatter assertions and measured zero managed bytes for 1,024 warmed queries. This is arithmetic evidence only: it does not execute the EditMode fixtures or prove native object, serialization or AssetDatabase behaviour.

The first authorized EditMode run executed all eight requested classes: 115 cases, 96 passed, 19 failed, none skipped/inconclusive. All 91 existing checks and the new content guard passed. The 19 positive model cases exposed a native Mono difference: the stored float coefficients 0.6 and 0.4 add to 1.0000000298023224 in a wider intermediate, so a direct float comparison incorrectly rejected a unit budget. The pinned Mono console reproduced the failure. The correction explicitly sums in double and accepts only half a single-precision ULP at one; a new regression still rejects genuinely excessive budgets. This is numeric precision, not a balance tolerance or a loosened habitat/depth assertion.

Corrected-head local Unity EditMode proof: **116 passed, zero failed, skipped or inconclusive**. All eight requested classes ran, proven by per-class counts from a temporary TestRunnerApi callback; only aggregate counts and failures were saved, with no full results XML.

| Class | Passed | Failed | Skipped/inconclusive |
|---|---:|---:|---:|
| SchoolFieldTests | 24 | 0 | 0 |
| LivingFishContentValidationTests | 1 | 0 | 0 |
| ContentValidationTests | 31 | 0 | 0 |
| FishSchoolDensityContentValidationTests | 4 | 0 | 0 |
| BiteContentValidationTests | 4 | 0 | 0 |
| FishSchoolModelTests | 21 | 0 | 0 |
| FishSchoolHonestyTests | 8 | 0 | 0 |
| VisibleFishTests | 23 | 0 | 0 |

Both authorized leases were returned with their editors closed; the keeper reported the save unchanged. Corrected-head CI run `42774501559` was in progress at publication; initial run `42771620626` was cancelled by the real numeric correction push. CI status is separate from local proof and is not claimed green here.

## Cost and remaining boundaries

The inactive addition introduces no current per-frame work, sprites, rendering passes or save data. An enabled query performs bounded arithmetic per requested species/cell and fills caller memory. The allocation guard is a CPU managed-memory check; it does not measure GPU time or prove 60 fps. Profile on the existing laptop target at the integration step and keep mobile possible.

No Unity Play session, save write, scene rebuild, shader edit, look judgment or plate rebaseline is needed for this model. The later migration must preserve freshness and distinguish owned individuals from recomputed background fish. A field query has no historical population memory: local stock depletion, persistent removals and connected migration are deliberately not promised.
