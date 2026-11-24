# PlayMode sprite-cleanup freeze audit

Source baseline: `832c6f78f9e607cfbdede4845a3d205551439c91`. Audited 2026-11-23.
This is a source audit, not a reproduced deadlock or a claim of a fix. No editor,
test suite, importer, scene builder or capture was run for this document.

## Finding

The reported main-thread stack is `CommitBatchDelete -> SpriteRenderer::MainThreadCleanup
-> AssetChangedNotifier::UnsubscribeFromAll -> GetGUIDFromEntityId ->
PersistentManager::GetPathName`, waiting on a lock. Two reported ten-minute hangs,
including `SunCastersEverywherePlayTests.Audit_Cumberland_VillagersGainCasters`, and
a passing retry of the same revision establish intermittency, not which thread
held the lock. In particular, a test-start marker does **not** establish that its
teardown had started.

The repository already documents an earlier two-thread deadlock: sprite destruction
on the main thread versus `Loading.Preload` reading a scene under the persistent
manager lock ([SceneSettle.cs:16-31](../../Assets/Tests/Support/SceneSettle.cs#L16)).
That historical diagnosis is the strongest lead, but the new `CommitBatchDelete`
stack must not be silently equated with the old `DelayedDestroyCallback` stack.
Single-scene replacement, async unload, immediate destruction and delayed destruction
are separate ways of arriving at renderer cleanup.

There is a specific harness gap worth addressing independently of the native cause:
`SceneSettle.DestroyAndSettle` destroys first and then waits for the victims to die
([98-110](../../Assets/Tests/Support/SceneSettle.cs#L98)); `Unload` waits three frames
before issuing an unload ([179-197](../../Assets/Tests/Support/SceneSettle.cs#L179)).
Neither waits for already pending scene operations. The existing shared counter is
`SceneLoadHold.InFlight` ([63-66](../../Assets/_Project/Code/Core/World/SceneLoadHold.cs#L63)),
but the immediate persistent-root cleanup in the implicated fixture does not consult
it either. Three frames and a completed *named* load do not establish that all loads
issued by live scene components have finished.

## Destruction and loading sites

Links and line numbers below refer to the baseline above. These are exposure sites,
not assertions that every row ran immediately before either reported hang. A whole
scene or persistent player hierarchy can contain many renderer descendants; exact
runtime counts were not measured. Existing caster floors are not renderer censuses.

| Fixture / shared path | Destruction and wait at baseline | Potential overlapping work / assessment |
| --- | --- | --- |
| `SunCastersEverywherePlayTests` | [117-151](../../Assets/Tests/PlayMode/SunCastersEverywherePlayTests.cs#L117): delayed destruction of owned objects, `DestroyImmediate` for every new persistent root, one frame, then scene unloads through `SceneSettle`. [190](../../Assets/Tests/PlayMode/SunCastersEverywherePlayTests.cs#L190) also replaces the previous scene with a Single load. | Direct load is awaited, but runtime neighbour loads are not explicitly drained before destruction. Highest-priority immediate-destroy site because the reported test belongs to this fixture. |
| `CumberlandPlayablePlayTests` base fixture | [224-239](../../Assets/Tests/PlayMode/CumberlandPlayablePlayTests.cs#L224): unload region and stand-in, then `DestroyAndSettle` on all persistent arrivals. Load at [124-127](../../Assets/Tests/PlayMode/CumberlandPlayablePlayTests.cs#L124) is awaited. | It exercises live regional travel. The shared helper drains *after* destroying and cannot exclude an outstanding runtime preload. Distinct fixture from the Cumberland caster audit. |
| `RicePointMemoryPlayTests` | [309-330](../../Assets/Tests/PlayMode/RicePointMemoryPlayTests.cs#L309): disables coordinators, unloads trip scenes, immediately destroys persistent arrivals. | Disabling stops future coordinator updates but does not cancel a load already issued. A still-loading scene is omitted by the `isLoaded` filter. Its explicit unused-asset collection is awaited ([193](../../Assets/Tests/PlayMode/RicePointMemoryPlayTests.cs#L193)). |
| `StPetersEastDoorPlayTests` | [81-106](../../Assets/Tests/PlayMode/StPetersEastDoorPlayTests.cs#L81): unload island, then immediate persistent-root loop; another cleanup path at [294-312](../../Assets/Tests/PlayMode/StPetersEastDoorPlayTests.cs#L294). | Persistent callbacks and scene-loaded callbacks can outlive a particular awaited operation. This fixture is also named in the historical failure described by the helper; that is not evidence it preceded the new hang. |
| `NmcWharfWaterSidePlayTests` / `WharfNightStage` | [187-196](../../Assets/Tests/PlayMode/NmcWharfWaterSidePlayTests.cs#L187) destroys spawned objects/assets via `DestroyAndSettle`, then stage cleanup; stage [253-275](../../Assets/Tests/PlayMode/WharfNightStage.cs#L253) destroys its objects and unloads the whole region. | Earlier destroy-then-unload exposure has already been mitigated by the shared settle. Outstanding loads remain a separate gap. Diagnostic `AssetDatabase` calls at [133-134](../../Assets/Tests/PlayMode/NmcWharfWaterSidePlayTests.cs#L133) are synchronous path/GUID lookups, not evidence of a background import. |
| `NineMileCreekFrameTimePlayTests` / `NineMileCreekWallPlatesPlayTests` | Frame-time cleanup [231-259](../../Assets/Tests/PlayMode/NineMileCreekFrameTimePlayTests.cs#L231), wall cleanup [403-434](../../Assets/Tests/PlayMode/NineMileCreekWallPlatesPlayTests.cs#L403): transient objects, then whole-scene unloads through `SceneSettle`. | Their `Resources.UnloadUnusedAssets` calls are yielded (frame-time [103,199](../../Assets/Tests/PlayMode/NineMileCreekFrameTimePlayTests.cs#L103); walls [232,372](../../Assets/Tests/PlayMode/NineMileCreekWallPlatesPlayTests.cs#L232)). No fire-and-forget unused-asset operation was found at these sites. Graphics-dependent cases are not proof of exposure on a headless run. |
| `CharacterCreatorOpeningPlayTests` | [48-68](../../Assets/Tests/PlayMode/CharacterCreatorOpeningPlayTests.cs#L48): destroys persistent core before unloading EastWater/StPeters; raw unload after `Frames()`, with a timeout. | Direct raw unload is not tracked by `SceneLoadHold`. Timeout permits cleanup to continue with the operation unfinished. It can affect a later fixture if this path times out; it is not shown to have done so in either new incident. |
| `RegionDialogueTravelPlayTests` | [194-210](../../Assets/Tests/PlayMode/RegionDialogueTravelPlayTests.cs#L194): immediate coordinator/player/boat/object cleanup before unloading source and destination. | Smaller synthetic hierarchies, same missing pending-operation barrier. Immediate destruction only changes when cleanup runs, not whether native cleanup needs a lock. |

Search scope: all C# under `Assets/Tests/PlayMode` and `Assets/Tests/Support` for
destruction, scene load/unload, `AsyncOperation`, activation holds, `AssetDatabase`,
unused-asset collection, and teardown attributes. No direct `AssetDatabase.Refresh`,
`ImportAsset`, `StartAssetEditing`, `StopAssetEditing` or `SaveAssets` call was found
in that scope. There are many synchronous `LoadAssetAtPath` calls; their presence
alone does not prove an operation remains active after the call returns. This
search is not a transitive proof that editor callbacks or engine import workers
are idle. For example, [ArtImportPipeline.cs:27](../../Assets/_Project/Art/Editor/ArtImportPipeline.cs#L27)
is an asset postprocessor, but nothing inspected connects it to the failure window.

## What the Cumberland audit actually does

1. [170-171](../../Assets/Tests/PlayMode/SunCastersEverywherePlayTests.cs#L170) calls
   `Audit("Cumberland", 8, 1, 0, 0)`. The eight-caster floor is not a population count.
2. [179-195](../../Assets/Tests/PlayMode/SunCastersEverywherePlayTests.cs#L179) disables
   the installer, hides casting, enables ignored diagnostics, restores time scale,
   yields `SceneSettle.Load(..., Single)`, and yields another twelve frames. That
   Single load can destroy the scene inherited from a previous test before the audit
   has installed a single caster of its own.
3. [201-217](../../Assets/Tests/PlayMode/SunCastersEverywherePlayTests.cs#L201) pins noon,
   requests a complete scan, waits up to ten real-time seconds, disables the installer,
   and restores casting. The scan visits loaded scenes **and** the persistent scene
   ([SunCasterInstaller.cs:85-105](../../Assets/_Project/Code/Art/SunCasterInstaller.cs#L85)).
4. [221-256](../../Assets/Tests/PlayMode/SunCastersEverywherePlayTests.cs#L221) counts
   classes, writes a census and asserts existing floors/exclusions. It does not itself
   launch an async load or call `AssetDatabase`. The `LoadAssetAtPath` calls at
   [349-350](../../Assets/Tests/PlayMode/SunCastersEverywherePlayTests.cs#L349) belong to
   a different setup path, not this audit.
5. Teardown re-enables the host, destroys owned objects and new persistent roots,
   asserts no persistent arrivals remain, changes to a cleanup scene, and unloads
   every other loaded scene. It never verifies `SceneLoadHold.InFlight == 0` first.
   A root promoted by a load that completes *after* the snapshot/cleanup is another
   possible source of inter-test contamination.

Casting toggles are not bulk deletion: [SunCasterInstaller.cs:108-129](../../Assets/_Project/Code/Art/SunCasterInstaller.cs#L108)
changes enabled state, and [SpriteShadow.cs:686-690](../../Assets/_Project/Code/Art/SpriteShadow.cs#L686)
disables pooled child renderers rather than destroying them. Renderer children are
created at [913-928](../../Assets/_Project/Code/Art/SpriteShadow.cs#L913), increasing the
cleanup population when their hierarchy eventually dies. No art change follows
from this audit.

## Why order could matter

`SceneLoadHold` is a per-session guard, not a fixture reset. It skips holding a new
load when another tracked operation is in flight or after three ten-second hold
timeouts ([45-50,74-86,130-158](../../Assets/_Project/Code/Core/World/SceneLoadHold.cs#L45)).
The latter state persists until subsystem registration. Thus identical test code can
enter with different protection depending on earlier operations. A passing isolated
case or retry does not disprove an order-sensitive overlap.

Runtime preloads are tracked at [RegionSceneLoader.cs:142-147](../../Assets/_Project/Code/World/RegionSceneLoader.cs#L142),
but their completion callback can still run after the requesting hierarchy is removed
([183-213](../../Assets/_Project/Code/World/RegionSceneLoader.cs#L183)). Coordinator
destruction calls `ReleaseWarmNeighbours` ([94-102](../../Assets/_Project/Code/App/RegionTravelCoordinator.cs#L94));
that deliberately leaves a still-loading neighbour alone
([737-751](../../Assets/_Project/Code/App/RegionTravelCoordinator.cs#L737)). Destroying
the requester is therefore not cancellation. It can also issue tracked unloads.

The CI command runs the full PlayMode suite without a test filter or explicit order
([ci.yml:310-316](../../.github/workflows/ci.yml#L310)). No `Order` or `Parallelizable`
attributes were found in the PlayMode C# search. Do not infer the actual predecessor
from alphabetical filenames: obtain the ordered START trail from each run. Compare
the same prefix through the failing case, isolated case, and passing retry; record
scenes, pending count and hold-timeout count at every boundary. Earlier scenes,
persistent roots and timeout budget are plausible order effects; causality and
failure probability have not been measured.

## What the watchdog already captures

* Test/suite full name and START trail, plus an overwritten current-event file;
  finished case/result overwrites that file ([123-135,370-377](../../Assets/Tests/PlayMode/PlayModeHangWatchdog.cs#L123)).
  It does not log a separate setup/body/teardown phase.
* A ten-second poll and a 600-second default test/gap deadline. Declared timeouts
  receive 120 seconds' grace, with the default as a floor
  ([TestHangWatch.cs:25-47](../../Assets/Tests/Support/TestHangWatch.cs#L25)).
* Last main-thread tick/frame/time scale, scene event and scene list (including
  not-yet-loaded entries), refreshed every two seconds when the main thread can
  run ([164-205](../../Assets/Tests/PlayMode/PlayModeHangWatchdog.cs#L164)). Sixty
  seconds without a tick classifies STOPPED, versus TURNING; it is not itself a
  separate sixty-second kill deadline ([MainThreadPulse.cs:61-83](../../Assets/Tests/Support/MainThreadPulse.cs#L61)).
* `playmode-hang.txt` before termination; on Linux, per-thread `/proc` state and,
  when stopped, an attempted all-thread native stack dump to
  `playmode-hang-threads.txt` ([234-256](../../Assets/Tests/PlayMode/PlayModeHangWatchdog.cs#L234)).
  The dump uses `sudo -n`, a 180-second bound and forty frames per thread
  ([318-346](../../Assets/Tests/PlayMode/PlayModeHangWatchdog.cs#L318)). Failure to attach
  is reported; absence of a stack is not evidence of absence of a worker.
* A main-thread-only SIGABRT on a stopped Linux editor, then thirty seconds for its
  crash handler, then fallback exit 124. Native crash termination may use a different
  exit code. It cannot capture another thread's managed stack.

CI prints the verdict/current event and uploads the artifacts
([321-352](../../.github/workflows/ci.yml#L321)). Missing today: exact cleanup phase,
victim hierarchy/count, pending operation identities, the in-flight/timeout counters
at that phase, and a guaranteed lock-holder stack. The recorded scene list may be
two seconds older than the freeze.

## Ranked hypotheses and distinguishing evidence

| Rank | Candidate | Evidence that would confirm or weaken it |
| --- | --- | --- |
| 1 | Scene preload versus bulk renderer cleanup, during Single replacement or teardown. Same family as the previously documented two-thread cycle. | Confirm with the other thread in `LoadFileCompletelyThreaded` / `CheckInstanceIDsLoaded`, the two locks, and a pending scene read at cleanup. A non-loading lock owner redirects this diagnosis. |
| 2 | Harness starts destruction without waiting for outstanding runtime loads; an earlier failed/short-circuited fixture can leave one pending. Concrete ordering gap in the source, not yet attributed to the reported incidents. | Trace pending count and phase before the first destroy and before/after each unload in the named fixtures. A nonzero count at destruction plus the native read-side stack supports it; zero count weakens it for tracked operations only. |
| 3 | Guard bypass due to already-queued operation or exhausted session timeout budget makes order matter. | Preserve all `[SceneLoadHold]` warnings and counter values from the failing prefix and retry. Three timeouts before the failure, or a load issued behind an unfinished operation, confirms loss of the hold; it does not alone establish the lock cycle. |
| 4 | Background import/asset-change processing holds the persistent-manager lock. | Need a worker/import stack and matching refresh/import activity overlapping cleanup. Synchronous asset lookup call sites and the notifier symbol alone are insufficient. No direct refresh/import trigger was found in the searched test code. |

## Narrow harness follow-up and limits

Extend the existing `SceneSettle` helper with a bounded, frame-yielding wait for
tracked scene operations **before** teardown destroys objects. Use it at the
implicated immediate-root cleanup and before the shared destruction/unload paths;
keep the existing post-destroy drain and assertions. Recheck after the drain before
issuing an unload, since callbacks may have started more work. A timeout must fail
visibly rather than continue into the known unsafe cleanup. Do not busy-wait the
main thread, reset the shared tracker, force activation of someone else's load, or
increase the watchdog deadline.

Existing coverage found before proposing tests: [SceneSettlePlayTests.cs](../../Assets/Tests/PlayMode/SceneSettlePlayTests.cs#L15)
checks immediate destruction, settled Unity-null victims, drain duration and the
hold bound. [SceneLoadHoldTests.cs](../../Assets/Tests/EditMode/World/SceneLoadHoldTests.cs#L1)
checks the hold policy. Extend the former for pending-operation wait and timeout
behavior; do not create a competing cleanup helper or weaken population assertions.

The wait covers only tracked operations and only the boundary where it is applied.
It cannot cancel a native deadlock already reached while yielding, stop independent
components from initiating future work, or prove engine import workers idle.
Successful completion of one run cannot prove an intermittent freeze fixed. The
required runtime follow-up is repeated full-suite/prefix comparison with per-class
executed counts, preserved watchdog evidence and unchanged assertions. No such
runtime validation was performed for this audit.
