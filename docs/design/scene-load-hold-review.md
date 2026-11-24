# Scene-load hold on slow machines

The hold prevents frames from running during one editor scene-file read only when
it is granted and that read reaches the activation threshold before the hold gives
up. It is not a guarantee against an editor freeze. A queued load, an exhausted
timeout budget or a read slower than the cap resumes the ordinary asynchronous
path while reading may still be in progress. Built player loads do not use this
hold at all.

**PROPOSAL, nobody has accepted this.** This review changes no production code,
cap, configuration, scene or appearance. Baseline:
`c626cea10d1cb496df41cca2f9ab2f580ad47b28`. No local Unity session, compiler,
PlayMode run, slow-machine timing or native-deadlock reproduction was performed.

## What it actually guarantees

[`SceneLoadHold`](../../Assets/_Project/Code/Core/World/SceneLoadHold.cs#L129)
receives a newly issued `AsyncOperation`. If eligible, it sets
`allowSceneActivation = false` and waits on the calling main thread. A stopwatch
supplies elapsed real seconds and each unsuccessful check requests a 1 ms sleep.
No frame, ordinary Update or PostLateUpdate can advance on that thread during the
wait. The read worker can continue.

`WaitForRead` stops when progress reaches **0.9** or elapsed time reaches the
**10 s** cap. It checks progress again for the result, so a read reaching 0.9 on
the cap observation is `ReadDone`. A stalled scheduler can overshoot the cap before
the next observation; 10 s is not a hard real-time return deadline. This wait does
not cover scene activation, Awake/OnEnable work, first-frame installation or
rendering. Read completion is not operation completion.

After a normal return, activation is permitted and the operation is tracked until
its completion callback. A successful read increments `HeldLoads`; a timeout
increments `Timeouts` and logs a warning. Only timeouts consume the three-timeout
session budget. Successful holds do not spend it, so the session can contain many
successful stalls. Three nominal ten-second timeouts bound the intentional failed
waits, not total scene-loading time or all successful holds.

The counters reset at subsystem registration, not per scene, fixture or region
crossing. A completed queue does not restore spent timeout budget. Tracking covers
only operations explicitly passed to `Track`; it is not a census of native import
or asset-loading activity. The wrapper assumes ordinary nonthrowing engine
progress/clock access; it has no `finally` restoring activation after an unexpected
exception in the waiting path.

## Every refusal and escape from the hold

| Situation | Result and bookkeeping | What is observable |
|---|---|---|
| Null operation | `NotHeld`; no track or counter change | Nothing to load. `Travel` reports failure and its caller cancels the expected crossing; the hold itself supplies no player message. |
| Operation already done | `NotHeld`; `Track` immediately returns | No intentional stall; normal completion has already happened. |
| Another tracked load or unload is in flight | `NotHeld`; the new unfinished operation is tracked | Frames continue, allowing the predecessor to activate/unload. The new read has no hold protection. There is no refusal warning in the game wrapper. |
| `Timeouts >= 3` | `NotHeld`; new unfinished operation is tracked | Every subsequent game load in this editor session remains ordinary async, even after the queue drains. No retry or renewed budget is supplied. |
| Granted and progress reaches 0.9 | `ReadDone`; increment `HeldLoads`, permit activation, track to actual completion | The last rendered frame remains still during the read; activation may cost more time afterward. |
| Granted but read is still below 0.9 at the time limit | `TimedOut`; increment `Timeouts`, warn, permit activation, track | The visible hold ends even though reading continues. This restores the overlap with renderer cleanup that the guard was intended to avoid. It does not cancel the load. |
| Built player | The game call is excluded by `UNITY_EDITOR`; `Track` calls and their argument expressions are conditional too | Ordinary async loading; no 10 s hold or three-timeout budget is imposed by this class. |

There is no dedicated progress indicator, failure panel or retry choice in this
class. During an editor neighbour warm-up the player can see the current world
stop at its last frame before crossing. During an ordinary edge crossing the
coordinator requests black cover before beginning travel, so he can instead see a
stationary black screen. A refused/timed-out hold lets frames turn; whether the
world or cover is drawn then belongs to the caller's transition state.

`SeamCrossingFade.Tick` keeps the **Arriving** phase black until arrival and does
not apply its max-black limit to that phase. Its configured 1.5 s max-black value
therefore does not cap a slow pending scene load. A background warm-up is not a
crossing and need not be covered. These are source-derived consequences, not a
measured playthrough or a proposal to change a fade.

## Complete direct caller inventory

| Caller at this base | Purpose and consequences |
|---|---|
| `World/RegionSceneLoader.cs:142-148`, `StartAdditiveLoad` | The single production load start calls `SceneManager.LoadSceneAsync` first, then holds only inside `#if UNITY_EDITOR`. Both `Travel` (line 108) and `Preload` (196) use it. Neither consumes the outcome; they wait for engine completion to activate the destination or park the warmed scene. Revisited scenes and a travel request joining an existing preload do not start another hold. |
| `App/RegionTravelCoordinator.cs:737-751`, `ReleaseWarmNeighbours` | Issues an unload separately, then calls conditional `Track`. Unloads need frames and are never held. A still-loading neighbour is left to complete; destroying its requester is not cancellation. |
| `Tests/Support/SceneSettle.cs:161-174`, `StartLoad` / `StartLoadInPlayMode` | Both test load starts enter the helper's local wrapper, which calls the shared hold at this base. Test loads therefore share its timeout budget with game-initiated region loads. |
| `Tests/Support/SceneSettle.cs:179-197`, both `Unload` overloads | Yield the existing drain frames, issue unload separately, track it, then yield its completion. They do not hold the main thread. |
| `SceneLoadHold.cs:123-125,134-158` | The convenience overload forwards to the outcome overload; both refusal and post-wait paths track unfinished operations. `ShouldHold` and `WaitForRead` are the existing plain policy/wait seams. |
| `Tests/EditMode/World/SceneLoadHoldTests.cs` | Direct pure calls cover grant, queue/budget refusal and bounded waiting; reflection/source checks pin the player-build exclusion and prevent issuing a scene operation inside conditional `Track`. |

The caller search covered all C# under `Assets/`. `SceneSettle` also exposes the
shared cap, threshold and counters; those are read-only forwarding properties,
not additional load starts. Paths in this table are relative to
`Assets/_Project/Code/`, except paths beginning `Tests/`, which are under `Assets/`.

## Relation to the existing freeze work

The [sprite-cleanup freeze audit](../testing/playmode-sprite-cleanup-freeze.md)
distinguishes the earlier two-thread lock cycle from newer single-stack reports.
Losing this hold creates an exposure; it does not prove that a particular overlap
caused a particular hang, nor that the cap itself froze the editor. The identified
asset-notifier cycle is editor-specific; this review does not infer a matching
standalone-player deadlock.

PR 1513 owns `SceneSettle`, `SceneSettlePlayTests` and
`SunCastersEverywherePlayTests`. Its diff was read and none of those files is
changed here. It adds pending-operation waits before test loads/unloads, a separate
**40 s** test read hold with **three** test timeouts, and named completion failures
at **60 s** for pending operations / **120 s** for an issued operation. It also
drains tracked work before the implicated caster teardown. These are proposed
test-side limits, not changes to the game's 10 s hold. Its direct StartLoad paths
can still refuse a hold, and its own timeout path still permits frames; it does not
establish a universal native deadlock guarantee.

## Constants versus policy data

| Number | Today | Proposed ownership |
|---|---|---|
| `CapSeconds = 10f` | Core code constant | An editor-loading setting in `GameConfig`, retaining 10 as its initial default; validate finite/nonnegative values. This is a policy tradeoff between stall duration and coverage. No value changes in this proposal. |
| `MaxTimeouts = 3` | Core code constant | A companion editor session-timeout-budget setting in `GameConfig`, retaining 3 initially; nonnegative, with zero explicitly disabling holding. It is policy rather than engine protocol. |
| `ReadDoneProgress = 0.9f` | Core code constant | Keep fixed in code: this is the engine activation/read threshold, not a balance dial. |
| `Thread.Sleep(1)` | Literal milliseconds | Name as a scheduling constant if implementation is revisited; do not expose an owner-facing gameplay setting without evidence that polling cadence needs tuning. It cannot guarantee a 1 ms wake-up. |
| Fade out 0.15 s, fade in 0.2 s, max-black 1.5 s | Already `GameConfig` defaults and asset values | Keep existing settings; they are separate presentation policy and do not bound Arriving. |
| PR 1513 test limits | Test-harness constants | Keep in the test harness and under its watchdog contract, not in shipped balance data. |

`GameConfig` is also owned by open work. This review proposes configuration
ownership only; it introduces no field and changes no current value.

## Options and their costs

1. **Keep the bounded hold and expose its losses.** Retain 10/3 defaults; record
   granted/refused/timed-out outcome, scene, pending count and elapsed read time.
   Later move policy values into editor settings in `GameConfig`. Lowest code and
   integration cost; no additional geometry or frame work. It makes failures
   diagnosable but does not close the slow-read or queued-load exposure. Increasing
   the cap trades more visible stalls for probabilistic coverage, not correctness.

2. **Serialize editor scene operations and gate owned renderer teardown.** Use a
   shared operation coordinator: wait with frames before issuing a load behind
   unfinished work, register every owned operation, and defer known destructive
   cleanup until tracked reads have drained. On a diagnostic deadline, fail or
   stop the requested transition before proceeding with unsafe cleanup. This is
   the direction to investigate alongside PR 1513, while retaining its test-side
   ownership. Cost: queue/lifetime plumbing across travel and teardown, reduced
   overlap, explicit pending/failure handling and integration tests. It still
   needs native evidence: uncontrolled engine/import work and already-queued
   destruction may lie outside that coordinator. An issued AsyncOperation has no
   cancellation implemented by this proposal; reporting failure must not pretend
   the operation disappeared.

3. **Use a synchronous editor load at a controlled transition.** After draining
   owned earlier operations, load at an explicit safe transition boundary; keep
   built-player loading unchanged. This removes the intentional frame-turning
   interval for that load but can block arbitrarily long and sacrifices seamless
   neighbour warming. Cost: visible editor stalls, changed callback/activation
   timing, watchdog implications and broad transition validation. It is not a
   bounded-time replacement and cannot be accepted without native and lifecycle
   tests.

Investigate option 2 with option 1's measurements first. No option is accepted or
implemented here. None should be described as a freeze fix merely because an
isolated retry passes.

## Tests and remaining proof

The existing `SceneLoadHoldTests` was found before adding coverage; no competing
hold class, fake operation or new production clock seam is needed. The existing
`elapsedSeconds`, `progress` and `pause` delegates provide the fake-clock seam.
The suite already pins grant, in-flight refusal, read completion, timeout,
negative-cap handling, budget exhaustion and editor-only call placement.

Added cases pin threshold completion exactly at the cap, scheduler overshoot,
zero-cap progress boundaries, no clock access for a completed read, no budget
refund after queue drain, and null-operation bookkeeping. They perform no sleep
and launch no load. CI must compile and execute the suite and report its executed
count; the additions are not local execution evidence.

Actual operation completion/counter lifetimes, activation, sustained slow reads,
the complete suite prefix and the native lock-holder stacks still need the owned
PlayMode/native investigation. No test here claims that a slow editor cannot
freeze. There is no runtime, save, configuration or visual change to validate in
this proposal.
