# PlayMode stall audit — 2026-11-10

## Finding

The original attempts of runs **42749360538** (PR 1034) and **42757755999** (PR 1040)
both stopped making progress near the start of PlayMode, then logged an **Asset Import
Worker connection timeout**. There is no PlayMode results XML for either original
attempt. This is evidence of an editor/import-worker stall, not evidence that a
particular water assertion hung. The exact blocking test and native call remain
unproven. **The item is parked pending case-start and worker diagnostics.**

| Evidence | Run 42749360538, attempt 1 | Run 42757755999, attempt 1 |
|---|---|---|
| Last named test log stack | `PrivateSoundPackPlayTests.MusicBus_FollowsTheSongCycle`, 04:16:11Z | Same method, 05:36:56Z |
| Last audio-listener warning before the worker diagnostic | 04:16:33Z | 05:37:17Z |
| Worker connection timeout | 04:21:00Z | 05:41:45Z |
| Silence after worker diagnostic | 5,677 seconds | 5,566 seconds |
| PlayMode XML | Absent | Absent |

All timestamps are UTC on 2026-11-10. Both worker messages identify worker 0,
`WorkerState(Connecting)`, `ImporterState(Initializing)`, a zero import-address GUID,
`Importer(-2,...)`, and an empty asset path. They do not identify a water asset or
shader. The last named audio stack is an informational log in the no-songs branch,
after that branch's assertions; it does **not** establish the active case at the
stall. Audio-listener warnings likewise do not identify a native cause.

Run 42749360538 attempt 2 completed on the same head commit
`d6c9bb3c18fe862abbc5f855821e4e348da9c84b`: PlayMode took 72.73 minutes and
reported 1,065 passed, 226 skipped, zero failed. Run 42757755999 attempt 2 was
still in progress when checked; it is not claimed as a pass. The four adjacent
successful runs 42748990660, 42751146911, 42754823984 and 42755606918 took
72.57–75.20 minutes in PlayMode. The stalled attempts are a different pattern
from those completed suites.

## What the existing evidence cannot answer

The historical logs have no complete, flushed per-case start/finish journal.
The worker log named by the editor diagnostic was not included in these
`test-results` artifacts. Neither a test full name nor a blocked native stack can
be reconstructed from an absent results file. A timeout annotation on a coroutine
also cannot interrupt an editor main thread blocked inside a synchronous native
import. Adding or raising test budgets would not establish a cause.

Exact parked question: **which test was started but not finished when the editor
waited for worker 0, and what editor/worker stack or resource limit caused that
wait?** It needs another occurrence with those diagnostics, or an independently
reproduced editor/import-worker failure. No shader, import setting, scene, wait,
assertion, pixel baseline or product behavior is changed by this audit.

## Next diagnostic change

Keep this separate from a speculative product fix. On a CI-only diagnostic branch:

1. Register one test-run callback before either suite. Append and flush platform,
   full case name, UTC start, finish status and elapsed time to an artifact journal.
   Preserve the last unpaired start even when the editor cannot finish its XML.
2. Upload the journal, editor log, and import-worker log tails with `always()`.
   Stream and bound the tails; retain no private paths or credentials in public reports.
3. Use an external watchdog around the editor process it starts. Bound a case
   without a finish, retain diagnostics, and fail the run; never turn a timeout into
   a skip or a pass. Pick its budget from the measured slowest cases plus explicit
   cold-import allowance. Record editor startup separately from case execution.
4. Repeat on the exact frozen merge commit. Confirm the named case and worker
   state before changing a harness or assigning a product fix.

No such reproduction, watchdog, callback or new CI rerun was performed in this audit.
The original job timeout remains 150 minutes.

## Fixture law: no deferred destroy pending when a scene load or unload starts (2026-11-16)

**The law.** A PlayMode fixture never starts `SceneManager.LoadSceneAsync`, `UnloadSceneAsync` or
`EditorSceneManager.LoadSceneAsyncInPlayMode` while an `Object.Destroy` it (or an earlier test) queued
has not run yet. Every such call in `Assets/Tests/PlayMode` goes through
`HiddenHarbours.Tests.Support.SceneSettle` (`Load`, `Unload`, `LoadInPlayMode`, or `Frames()` before a
call whose `AsyncOperation` the fixture keeps), and teardown destroys use `SceneSettle.DestroyAndSettle`
(or `DestroyNow` for test-spawned objects, never from a physics callback or `OnDestroy`).
`SceneSettlePlayTests` pins the contract.

**Why.** A silent CI hang, seen four times, is an editor-only lock-order deadlock inside the engine
(native stacks in the hang log of CI job 133069600016):

- main: `PostLateUpdate DelayedCallManager::Update > DelayedDestroyCallback > SpriteRenderer::MainThreadCleanup > AssetChangedNotifier::UnsubscribeFromAll > PersistentManager::GetPathName` (waits for the PersistentManager lock).
- `Loading.Preload`: `LoadSceneOperation::Perform > PersistentManager::LoadFileCompletelyThreaded > CheckInstanceIDsLoaded` (holds that lock, waits for the object lock).

A destroy queued in frame N runs in frame N's PostLateUpdate pass, or N+1's if it was queued after the
pass or by an `OnDestroy` cascade; so two `yield return null` is the floor and `SceneSettle.DrainFrames`
is 3. One yield is not enough.

**Not covered.** A scene the game itself streams in (the region loader's additive load) is invisible to
the helper; destroying sprites while it runs can still deadlock in the editor. Players are not affected:
the asset-change notifier exists only in the editor.

### Second slice: the settle is necessary, not sufficient (2026-11-16)

The first main run with the settle (run 43834512753, attempt 1, job 133187728920) hung in
`StPetersEastDoorPlayTests.TheIslandAnswersTheKeyABoatComesHomeWith` with the same two stacks. That
test does no region travel. It loads St Peters through `SceneSettle.Load` after the settle; the main
thread stopped in the first second of the test, in the frames of that load. The previous teardown
destroyed its roots with `DestroyImmediate`, so the test left nothing queued. The pending destroy came
from **game code**: something on the persistent `DontDestroyOnLoad` hosts (presenters installed by
`RuntimeInitializeOnLoadMethod`, which live for the whole run) destroyed a sprite hierarchy while the
preload thread was still reading the scene file. That read takes a few hundred milliseconds of frames.
The native stack cannot name the script.

**The law, extended.** A test-issued load also holds the main thread, inside the frame that issues it,
until the threaded read is done. `SceneSettle.StartLoad` / `StartLoadInPlayMode` issue the load with
`allowSceneActivation = false`, sleep until `progress` reaches 0.9 (the read is done), then allow
activation and hand the operation back. While the main thread is held, no PostLateUpdate can run, so no
delayed destroy can meet `LoadSceneOperation::Perform`. `Load` / `LoadInPlayMode` use it, and the four
fixtures that keep their own `AsyncOperation` call `StartLoad` directly. The hold is bounded at
`HoldSeconds` (10 s). After `MaxHoldTimeouts` (3) timeouts the run stops holding, so a hold that does not
work on some editor costs at most 30 s per run, not a hang.

**Still not covered.** Loads the game starts itself: `RegionSceneLoader.Travel` (a passage, an arrival,
a crossing), which about 20 PlayMode files drive. The proposed fix is the same hold inside
`RegionSceneLoader.Travel`, under `#if UNITY_EDITOR` only. The risks: an editor-only hitch of one read
(about 200 ms) per crossing; a bounded freeze (up to `HoldSeconds`) if the load is queued behind another
scene operation; and a gameplay file changes. It is not in this change.
