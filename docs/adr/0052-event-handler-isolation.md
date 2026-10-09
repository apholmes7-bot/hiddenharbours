# ADR 0052 — A failing event handler cannot cut its publisher short

- **Status: PROPOSED** (2026-10-08). Option A was RULED by the owner; this ADR still
  needs the owner's approval in its PR under `CLAUDE.md` §7.
- **Date:** 2026-10-08
- **Decision owner:** owner; authored by `lead-architect`. Review: gameplay-systems,
  ui-ux and qa-test for changed failure behavior across Core consumers.
- **Serves:** P5 Cozy but with Teeth: a storage failure must not strand control and
  camera in different modes. P2: a purchase notification failure must not suppress
  later fleet delivery and the publisher's continuation.
- **Amends:** [technical architecture §3](../architecture/tech-architecture.md).

## 1. Why

`ControlSwitcher.TakeHelm` commits Aboard, then publishes `ActiveBoatChanged`, then
`ControlModeChanged`. `SaveService` saves on the first notification. An exception at
`SaveStore`'s file replacement currently escapes the bare multicast delegate call,
skipping later boat listeners and the entire mode notification. Changing the event
order only relocates that failure. The same interruption can cut short purchases,
catch transfers, arrival and restore.

## 2. Decision

Events are **notifications, not transactions**. Publish synchronously in registration
order on the main thread. Catch each leaf handler's `Exception`, call
`Debug.LogException(exception)` once for that failure occurrence, then continue.
Never rethrow afterward, in editor or player builds. Do not remove failed handlers.
Duplicates remain separate invocations and separate diagnostics.

Keep ordinary delegate combine/remove semantics, including null no-ops, duplicates,
last-occurrence removal and last matching multicast-subsequence removal. Each channel
retains its multicast delegate and an immutable flattened `Action<T>[]` snapshot.
Subscribe/Unsubscribe rebuild the cache; Clear releases both channel roots.
Publish reads the array reference once. Mutation during a callback leaves that outer
snapshot intact; an immediate nested publish sees the newly installed array and runs
depth-first. This is reentrancy, not thread safety; concurrent use is unsupported.

Keep `ControlSwitcher`'s state/event order unchanged. No save schema, simulation,
retry, player feedback or feature-module dependency changes follow from this decision.

## 3. Cost and failure contracts

- After initialization, successful stable-subscriber Publish has **zero bus heap
  allocation**, one snapshot-reference read and O(n) invocation/try boundaries.
  Subscriber work and payload construction are outside this budget. Never call
  `GetInvocationList` inside Publish. Subscription changes may allocate the invocation
  list and typed cache; channel storage is O(n), with old arrays retained only while
  active dispatches need them. Exception construction and logging may allocate.
- Log exceptions in every build. Unexpected exception logs must fail Unity tests,
  including assertions thrown inside subscribers. Fault-injection fixtures use an
  exact `LogAssert.Expect` for each injected exception and assert subsequent effects
  outside callbacks. Never use `LogAssert.ignoreFailingMessages` to hide failures.
- A returned Publish does not establish that a handler finished or that a save landed.
  Failed handlers can leave partial state. A request still needs its own **explicit
  outcome**; the bus cannot synthesize success, roll back, retry or complete a response.
  Save result/retry/feedback and quit policy remain the separately chartered option B.
- Logging uses Unity's logger directly, never a recursive EventBus diagnostic event.

## 4. Validation and test seam

New dispatch guards cover failure continuation, combined delegates, nested dispatch,
mutation during failure, nulls, duplicates, remove order, Clear and HasSubscribers.
The warmed zero-byte allocation guard preserves an already-green baseline; an
allocating dispatch variant in ignored scratch must fail it.

New companion fixtures exercise the real public helm interaction and purchase/fleet
path. The save/helm PlayMode fixture uses the runner's redirect before SaveService's
Awake, test-owned old bytes, and an internal editor-only `SaveStore.BeforeReplaceForTests`
hook immediately before File.Replace, after temp serialization. The owner authorized
this narrow exception to the EventBus-only production scope. The hook is restored after
the test and filtered to its destination. It is excluded from player builds; it does
not replace the successful writer. This exercises a deterministic persistence-boundary
failure on Linux too; it is not a claim to reproduce a Windows OS share-mode failure.

Phase A forbids Unity. Compile/headless results and native-lifecycle cases awaiting
Unity are recorded separately in the lane's ignored Phase A report. Before acceptance,
CI must run the new guards and the existing EditMode/PlayMode suites, including the
seam-only original-Publish negative control. No existing test is retired or weakened.

## 5. Alternatives

Wrapping the whole multicast call loses later handlers. Catching per submitted
delegate loses leaves of a combined subscription. Rethrowing after delivery still
cuts off the publisher. Allocating invocation lists on every Publish breaks the hot
path budget. Reordering helm notifications leaves other publishers vulnerable.
Option B is useful but changes persistence outcomes and is separate from this ADR.
