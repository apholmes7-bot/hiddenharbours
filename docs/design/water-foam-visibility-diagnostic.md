# Persistent foam: draw-channel diagnostic

Updated 2026-09-15. Owner-authorized water fidelity investigation, pillar P1.
Prepared in `C:/hh-codex-water-foam`; captured after #842 merged and the owner
released the editor slot, in hydrated `C:/hh-water7`, branch
`codex/water-foam-capture`. No production code, shader, scene, material, or boat data
is changed by this diagnostic.

## Findings and status

The real-region baseline offers 31 eligible injectors to eight slots. The test Cape
is last and receives none. The positive control reaches all 1,680 displaced-water
slots and changes the sea; production foam contributes zero measured baseline pixels.

A fresh-editor counterfactual temporarily gives the Cape the first slot. Its sheet
then appears in north, east, and turning views, with every repeat/restoration control
remaining identical. This proves starvation and demonstrates a functioning delivery
path in that run. It does **not** establish that allocation is the only defect:
running baseline and priority tests consecutively exposed a second failure (the
priority subject receives slots but its sheet does not appear).

The measured records, captures, run qualifications, and downstream repair boundaries
are in [the evidence record](../art/spikes/foam-visibility/README.md).
The newly visible dense ribbons are experimental output, not accepted final art.

## Instrument

Two UnityTests in the existing photograph fixture:

- `TheCape_FoamSheet_ReachesTheDrawingChannelOrNamesTheMissingInput`
- `TheCape_FoamSheet_WithSubjectFirst_SeparatesSlotStarvationFromComposition`

Their separate partial-class source reuses the Cape, real Nine Mile Creek region,
hour 11, north/east/turn drive, production camera, capture, and teardown. The second
test temporarily promotes the subject in the registry, then restores its position.
This is a causal experiment, not the proposed production allocation policy.

Each leg now observes the production collector's read-only selection event for its
camera, after normal injector LateUpdate and actual ranking. It does not call collection
to measure selection or infer slots from registration order. Active water
renderers receive indexed property blocks preserving their existing values (or
inheriting their renderer-wide block if no indexed override existed).

All shutters execute synchronously within one frame: untouched A/A, production
off/on/on/off, a directly bound feature-target probe, known-input off/on/on, and
original-state restoration. Known input is a full fresh foam texture covering the
camera, with lace disabled; it proves consumption only. No cross-frame subtraction
is valid evidence of the foam effect.

Assertions require unchanged same-frame repeats, responding known input, zero-strength
bypass, exact visual restoration, and eligible subject injections. The priority test
also requires every eligible offer to fit. Production pixel response is measured,
not assumed: a zero response with valid controls is a diagnostic finding.
Failed assertions preserve measured records. Property blocks and registry ordering
are restored in `finally`; the render observer is unsubscribed in `finally`.

CPU `Shader.GetGlobalTexture` reports `UnityBlack` even in a successful capture:
render-graph GPU binding is not established by this CPU value. A diagnostic reflection
probe locates the feature's actual per-camera ping-pong target and tests a direct
renderer binding to separate generation from delivery.

## Validation and execution contract

Both new tests explicitly skip without a graphics device. Expected suite delta:
**+2 PlayMode tests, +2 explicit skips on null-device CI**, no removed/renamed tests.
CI green cannot validate these visual claims. See the evidence record for actual
local outcomes, including failures; compilation alone is not visual acceptance.

The exact filter prefix is
`HiddenHarbours.Tests.PlayMode.WakeCentrePhotographPlayTests.TheCape_FoamSheet_`.
It must match two tests. Individual methods match one each. Use a granted editor
slot, the hydrated checkout, a 30-minute timeout, and freshly banked owner save.
Copy fresh `wake-photograph` outputs into durable storage before scratch cleanup.
The region fixture rewrites the shared save: restore its pre-run bytes only after
the editor closes and the live hash still equals this run's recorded post-run hash.

The family-A production lane should consume these measurements. Preserve the fixed
eight-injector budget; selection needs camera-relevant swept/dispersion footprints
and stable ties, with overflow/multiple-camera coverage. Do not permanently promote
the player as a substitute for that policy. Delivery after scene/test transitions
must also be checked before declaring the path fixed.

Once delivery is reliable, subsequent work is foam transport and appearance: patches
stretch, collect, tear apart, and fade with separate coverage/freshness. Keep the
shared gameplay-wave field and drawn-only wake contract. These behavior changes are
outside this diagnostic PR.

## F1 boundary (2026-09-27)

F1 replaces registration-order packing with independent selection for each camera's
cell-snapped foam window. Candidates overlap that window with their swept capsule or
one of the five dispersal-track capsules widened to the existing `MaxHalfWidth`.
This is a conservative reach envelope: the shader's astern gate and moving ring can
still leave individual enclosed texels untouched. `EdgeWidth` cannot expand reach
beyond `MaxHalfWidth`, where the shader envelope is zero.

Rank is ascending distance from the window centre to the nearest capsule surface,
then ordinal scene-path/hierarchy key (names and sibling indices, cached on registration).
There are no selection weights or new reach dials. The existing injector radius and
dispersal data remain authoritative. A single candidate scan maintains at most eight
sorted slots; gathering capacity and keys are allocated on registration, not rendering.
The overflow warning counts only eligible, omitted footprints for that camera.

`InjectionSelectionObserved` reports the camera, injector, overlap, actual selected rank
(-1 when omitted), eligible count and selected count without exposing writable slot
arrays. The two #850 test names and the temporary registration reorder remain. With
F1 the reorder no longer grants priority, so the historical subject-first control no
longer isolates starvation; its retained all-eligible-offers assertion may fail under
genuine camera-local overflow. That control's future interpretation belongs to the owner.

F1 preserves #875's initialization/reset protocol, repeated-render guard, decay,
dispersal and eight-slot shader budget. Four pure EditMode cases cover footprints,
stable ties, predictable overflow/no allocations, and independent cameras. No local
Unity or GPU run is part of Phase A. The second #850 failure (selected subject with
no visible sheet) is **not claimed fixed**: fresh and sequential photograph runs,
each camera and domain reload, await the owner's editor slot after S1 and C1.
Known-input consumption and actual production foam response remain separate evidence.
