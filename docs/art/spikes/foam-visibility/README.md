# Persistent foam: the drawing channel works, the moving hull loses its slot

Measured 2026-09-15 in the hydrated `C:/hh-water7` checkout. Baseline capture HEAD
`60f4167919e4b60aaa5aaa3ec53f089499d768ff`, including main's #842 shore repair.
This is diagnostic evidence, not a production water fix.

## Baseline result

The Cape offered a fresh injection on **519 observed camera renders** across the
three legs and was selected **zero times**. In the final packing recorded on each
leg, all 31 registered injectors were eligible; the Cape was index 30. Production
`CollectInjections` fills its eight slots from the start of that registration list.
The first eight offers came from already registered review/moored boats.

The existing comment describing the collection as newest-first is inaccurate: the
loop runs from index zero upward, and registration appends. The diagnostic follows
the executable order, not that comment.

| Leg | Subject eligible observations | Subject selected | Live foam off/on changed pixels | Known full/fresh foam changed pixels |
|---|---:|---:|---:|---:|
| North | 165 | 0 | 0 | 3,205,780 |
| East | 178 | 0 | 0 | 3,354,686 |
| Sustained turn | 176 | 0 | 0 | 3,364,847 |

All untouched A/A, live on/on, live off/off, known on/on, zero-strength input bypass,
and restored-original comparisons changed **zero pixels**. Each leg's shutters ran
inside one frame, with no yield between them. All 1,680 active displaced-water draw
slots were found. The on/off write therefore reached the drawing renderer, unlike
the old rider's ambiguous one-material sweep.

The known-input arm replaces the buffer/window on the renderer property blocks and
turns lace off. Its large pale sheet is a deliberate positive control, **not a proposed
water look**. It establishes that this draw path can visibly compose foam. The normal
wake visible in the live arm is still drawn by the other wake systems.

## What this establishes, and what it does not

Slot starvation is an observed producer defect for this hull and fixture. The normal
buffer has no measurable visual contribution in these views. Neither observation
alone proves that correcting allocation is sufficient to fix the whole path.

The CPU global readback reports `UnityBlack`, even while the feature's window is live.
The texture is published by a render-graph command; that CPU read is not independent
proof of the binding at the GPU draw. Do not declare a missing render-graph dependency
from that value alone.

## Causal follow-up: reserve the first slot for the subject

Run 02 gave only the subject first place in the registry at runtime, repeated the same
controls, and restored the registration order afterward. No production algorithm,
shader, texture, foam tuning, or render-graph dependency changed.

| Leg | Subject eligible / selected observations | Live foam off/on changed pixels | Known-input changed pixels |
|---|---:|---:|---:|
| North | 173 / 173 | 115,023 | 3,205,797 |
| East | 151 / 151 | 238,795 | 3,354,771 |
| Sustained turn | 192 / 192 | 182,671 | 3,364,898 |

Every same-frame repeat, zero-strength bypass, and original-state restoration again
changed zero pixels. The production sheet reaches the water in every view **in this
fresh-editor run**. Allocation is a demonstrated cause, and changing its order can
restore delivery. The combined runs below show why this is not a universal repair.

Do not compare image deltas across the two runs: their world frames differ. Every
reported pixel count compares on/off arms within its own frozen frame. Eligible
counts are camera-render observations, not a claim of a fixed frame-rate budget.

The CPU global still reports `UnityBlack` in the successful priority run. The GPU
result therefore also demonstrates why that CPU value must not be used to diagnose
the command-buffer texture as unbound.

## Sequence-dependent failure: a slot is not always sufficient

Run 03 (`ce3e04e4`) executed baseline then priority in one Editor. Baseline passed;
priority failed its new production-response assertion on the first leg. The subject
was selected in all 170 eligible observations, while production off/on changed zero
pixels. Known input changed 3,205,754 pixels; repeats/bypass/restoration remained zero.
This contradicts any claim that selection alone always restores delivery.

Run 05 repeated the sequence with an additional direct binding of the feature's
actual per-camera ping-pong target. Priority again received its slot, but both its
ordinary and directly bound production-target pairs changed zero pixels. The target
had the current frame and expected world origin; the known input still responded.
This narrows the investigation beyond merely trusting the CPU global texture value,
but does not establish whether the target lacks coverage or another composition gate
rejects it. No texture-content readback or definitive cause is claimed.

Run 04 was a compile failure while adding that probe (missing renderer assembly
references); it ran no tests and left the save unchanged. The probe now uses
reflection through the already referenced Art assembly without adding dependencies.

The final diagnostic retains all repeatability, positive-control, bypass, restoration,
and slot-allocation assertions. It **does not assert a desired production response**:
that is the unknown being measured. Removing that experimental expectation is not a
production fix and does not erase the failures above. The record and PR must carry
the unresolved sequence-dependent result even if the instrument tests pass or CI is
green. Both full combined failed runs' measurement records are preserved under
`verification/`.

Final run 06 (`ad6d4123`) executed **both diagnostic tests: 2 passed, 0 failed,
0 skipped; CLI exit 0**. Their durations were 36.860 and 31.984 seconds. All six
legs passed the same-frame repeats, positive control, bypass, and restoration.
Baseline selected 0/599 offers; priority selected 604/604. Both ordinary and directly
bound production-target pairs were zero pixels in all six legs. Priority known-input
responses were 3,205,802 / 3,354,382 / 3,365,142 pixels (north/east/turn).
These passing instrument tests **reproduce the unresolved missing sheet**, not fix it.
Final records and hashes are in `verification/run-06-instrument/`.

### The newly visible look is not accepted final art

Visual inspection shows a strong, opaque central ribbon with separated side ribbons;
the turn view exposes a disagreement with the other wake marks. Visibility is now
demonstrated, but organic gathering, breakup, density and agreement through turns
remain subsequent look work. Do not treat this diagnostic's bright wake as an approved
production preset or silently retune the established wake roots to hide it.

The repair lane should address camera-relevant injection allocation while retaining
the bounded shader loop, then repeat these controls on the unchanged production dial.
Consider capsule/dispersal footprint intersection, not just whether the hull centre
is inside the window: deposits can cross its edge. Cover oversubscription, stable
selection, multiple cameras, and ships entering/leaving the window in focused tests.
Permanently putting the player first, as this causal probe does temporarily, is not
the full production selection policy.

## Images and raw evidence

The baseline PNGs contain the three live-on views and their known-input controls.
The live-off PNG is byte-identical to live-on in each leg, so duplicates are retained
in the durable run directory rather than committed again. All original capture-file
SHA-256 hashes are in `SHA256-run-01.txt`; the three measured text records include the
packing, frame number, material ID, full-image diff counts, and pixel-buffer hashes.
The priority probe additionally includes all three live-off/live-on pairs and known-on
controls, with `SHA256-run-02-priority.txt` and its own three measured records.

![North: shipped foam input](foam-visibility-north-000-live-on.png)

![North: known-input positive control](foam-visibility-north-000-known-on.png)

![North: production foam when the subject receives a slot](foam-priority-probe-north-000-live-on.png)

![Turn: production foam when the subject receives a slot](foam-priority-probe-turn-000-thru-088-live-on.png)

Durable original run: `C:/Users/aphol/.codex/workspaces/hh-water-evidence-20260915/run-01`.
The fixture is documented in `docs/design/water-foam-visibility-diagnostic.md`.

## Test, process, and save outcomes

The filtered XML contains exactly **one executed test: passed, zero failed/skipped**,
duration 46.924 seconds. Unity logged test completion with code 0, then its process
exited with **-1073741819 (0xC0000005)** during shutdown. Consequently Unity CLI returned
6. These are separate facts: the capture's assertions passed, and the editor process
did not exit cleanly. No clean overall Unity-run claim is made. The shutdown log
contains Mono thread-abort failures; their cause has not been established.

Run 02 executed exactly one priority-probe test: **passed, zero failed/skipped** in
34.371 seconds; Unity CLI exit **0**, no editor remained. Recorded Unity processes
13916 and 32700 both closed. Its pre-run save was independently banked; the live file
was restored with the same guarded hash comparison and identical restoration hash
after this run too. Evidence: sibling directory `run-02-priority`.

Editor PID 13088 closed; no editor remained after run 01. The live save was banked
before launch (seed 12345, clock 593.8666823096573, 760 bytes). The test session rewrote
it to 869 bytes at the same seed/time. After verifying that the live file still
matched the exact captured post-run hash and no editor remained, the pre-run bytes
were restored. Restoration SHA-256:
`23D186BE0B89D2A497AFA200F4E24C1B2D1427F41BF1A447778DA0E0AA9EC572`.

The pre-existing `ProjectSettings/ProjectSettings.asset` run-in-background edit and
untracked `Assets/_Project/Data/Wind.meta` were preserved and are not part of this work.

Final Editor PID 20328 and worker 26892 are closed; process inspection found no
remaining Editor. The final pre-run save was restored with the same guarded hash
check and SHA-256 above. Runs 03 and 05 were also restored after exit. Generated
RockPx import metadata was banked outside the repository before cleanup; it is not
part of the diagnostic PR. Raw editor logs and shared save files are not committed.
