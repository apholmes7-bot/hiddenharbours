# Persistent foam: draw-channel diagnostic

Prepared 2026-09-13 after the owner's instruction to proceed with water fidelity.
Original base: `70969b684e6bf4d0581c8e7ea5c7e35e5de31a8c`.
Updated 2026-09-14 to include settled main `5f691b43420cb5a52dbd292b8cc59fa75693d489`
(#842), via merge `a7097ecd`. Diagnostic preparation commit: `fa8d5e27`.
Owner role: gameplay-systems / rendering plumbing. Pillar P1.

## Status

Prepared and compiled without launching Unity. **Not rendered or visually verified.**
No production code, shader, scene, material, boat data, or savegame changed.
The new fixture was prepared independently while PR #842 held the water shader and
its editor slot. #842 has now merged. This diagnostic still has no editor-slot grant
and does not claim that any foam effect has been fixed.

Worktree: `C:/hh-codex-water-foam`, branch `codex/water-foam-visibility`.
LFS hydration was deliberately skipped in this source-only checkout. Do not open it
in Unity or mistake pointer files for usable art. Integration and GPU capture should
use the hydrated water checkout only after its existing lane releases it.

## Question and evidence

The previous family-A rider compared `_WakeFoamStrength` on/off without proving that
the written material was the displaced chunks' drawing material. Its null result
leaves both a missed write and missing production foam possible.

Source inspection also found a separate producer risk:

- `FoamBuffer.MaxInjectors` is eight.
- `FoamInjectionRegistry.CollectInjections` takes the first eight eligible members,
  in registration order, with no camera-window or strength selection.
- `FoamInjector.TryTakeInjection` excludes inactive/stale deposits; registered count
  alone does **not** establish overflow. A moored hull can nevertheless be eligible
  through relative current or heave.
- The previous rider reported 31 registered injectors. It did not establish the
  subject's eligible rank or which eight were uploaded.

These facts do not prove the cause of the invisible sheet. The diagnostic observes
eligible packing just before the game's camera renders, after the injectors' normal
LateUpdate, and records the subject's accepted/eligible frame counts over each leg.
It does not change membership or call the warning-producing collection method.

## Instrument

One added UnityTest in the existing photograph fixture:

`WakeCentrePhotographPlayTests.TheCape_FoamSheet_ReachesTheDrawingChannelOrNamesTheMissingInput`

It reuses the Cape, three headings, camera framing, real-region loading, drive loop,
capture path and teardown from #835. Its diagnostic body is a separate partial-class
file. Existing photograph tests keep their existing path.

For each leg:

1. Record injector eligibility and slot allocation during the drive.
2. Find every active scene renderer carrying the foam property, including displaced
   chunk clones. Write indexed property blocks, preserving original indexed blocks
   or inheriting the renderer-wide block when there was no indexed override.
   No material asset is written and no Update can overwrite a shutter in progress.
3. Warm the camera, then photograph untouched A/A, real buffer off/on/on/off, and
   known-input off/on/on, all synchronously inside one frame. Record hashes and
   full-frame changed-pixel counts.
4. The known input is a full, fresh foam texture covering the camera, bound through
   those same renderer blocks, with lace disabled. This is a positive control of
   the consumption path, **not** evidence that production foam was generated.
5. Assert same-frame repeats are identical, the known input changes the picture,
   strength zero ignores that input, and the subject offered an injection during
   its drive. Zero difference from the real buffer is recorded rather than hidden
   by asserting the desired outcome.
6. Restore all indexed property blocks in `finally`; unsubscribe the render callback
   in `finally`. Existing teardown restores the camera, region and clock. Emit partial
   measurements before any control assertion so a failed run preserves evidence.

The log also reports the CPU-visible global foam texture/window and reads the texture
when it is a render target. A `Shader.GetGlobalTexture` readback is not independent
proof of the GPU binding at the water draw; command-buffer bindings may differ. Use
the responding per-renderer positive control and the recorded feature/input path
together, rather than declaring a missing texture from that CPU read alone.

## Validation performed

- Compiled the PlayMode test sources, including the new diagnostic, with Unity
  6000.5.0f1's bundled Roslyn compiler and the already-generated references/defines
  from `C:/hh-water7/Library/Bee/artifacts/1900b0aE.dag/HiddenHarbours.Tests.PlayMode.rsp`.
  Sources came from this worktree; outputs went only to this worktree's ignored
  `artifacts/foam-visibility-validation/`. Exit 0. No editor launched.
- Existing deprecated-API and serialization warnings remain. Compilation is not a
  Unity test run, shader validation, image result, or proof of correct draw behavior.
- `git diff --check` passed.
- Expected suite change: one additional PlayMode test in the existing class, no
  renamed/removed tests. The existing null-graphics guard will explicitly skip it
  on CI; that skip is not visual acceptance.

## Next permitted execution

PR #842 merged on 2026-09-14 at 16:21:50Z as `5f691b43`. GitHub verification found both
checks successful on `5db6f792f12783396256364995e9799ee9d1ef52`, run `34783222968`.
Alex's relay reports its slot returned and temporary riders verified stripped.
Alex owns the machine-wide editor slot: the truck is next, then the foam diagnostic
after its editor is closed, its PID is named/verified, and Alex grants the next run.
An empty process list is not a slot grant. Do not switch another lane's checkout.

The requested sequence has been relayed to Codex v18: run this diagnostic first,
then have the existing Claude family-A wire PR consume the evidence instead of
repeating its first sweep. No concurrent shader work and no replacement lane was
launched. Await v18's sequencing confirmation before transferring the diagnostic.

Settled main has been incorporated without conflicts; the existing photograph,
injector, injection registry, and displaced-surface source files did not change
between the original base and #842. After release and sequencing confirmation,
transfer these named test/doc changes to the agreed hydrated test checkout. Before opening Unity, record
its clean shader and exact HEAD, confirm no conflicting editor, and bank/compare
the live owner save. Do not restore a different lane's fixture residue as the owner
save. Filter to the exact method above, with a 30-minute hard timeout. Confirm XML
matches **one test**, not a false green with zero cases. The method runs three legs.

Outputs use the existing photograph fixture's temporary-cache output directory,
under `wake-photograph`, and are named `foam-visibility-*`. Copy the fresh PNGs and
measurement records to a durable evidence directory before any scratch cleanup;
inspect the plates, report controls, then close the actual editor and return its PID.

## What the result permits

| Observation | Next action |
|---|---|
| Known input does not affect the water | The probe failed to demonstrate the drawing channel. Fix the instrument; do not claim missing foam. |
| Known input responds; production buffer responds | The old null sweep was not evidence of an invisible sheet. Verify shipped strength and move on to foam behavior. |
| Known input responds; production does not; subject never gets a slot | Test a camera-relevant injection selection repair, preserving the bounded buffer. Do not raise the cap blindly. |
| Known input responds; subject gets slots; production does not respond | Trace live buffer coverage, sampling coordinates, graph binding, and composition gates at the actual draw. |
| Any same-frame repeat moves | No effect verdict. Repair the capture's repeatability first. |

Once the sheet is visible and measured, the next behavior pass is local foam
transport: patches stretch, collect and separate under a spatially varying flow,
while coverage and freshness remain distinct. That is subsequent production work,
not implemented by this diagnostic. Preserve the shared gameplay-wave field and
the existing drawn-only wake contract throughout.
