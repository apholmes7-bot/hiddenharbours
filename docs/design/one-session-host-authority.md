# One session host authority

Status: design only. Apply after the three drafts below are on main, in one
reviewed migration. This page implements no host service and changes no draft.
It complements the [actor-service seam](first-week-actor-services.md): player
identity selects hands; host authority decides whether this process may commit
shared state. A player's key, position or region never establishes host status.

## Inspected heads

The complete three Core classes and relevant caller/test excerpts were read at
these immutable heads. This describes those sources, not a claim about future
versions of the drafts.

| Draft | Head | Current host check |
|---|---|---|
| PR 1572, `Core/Config/DemoWeek.cs` | `28ecb02f4b13321612d3cc54555aa69ecb08b01f` | Public `Func<bool> IsHost`; private `HostHere` means callback absent or true; `Install` resets the callback and subscribes. `OnWord` writes the ending only on the host, but publishes `DemoEnded` on guests too. |
| PR 1581, `Core/Config/IslandWeekStart.cs` | `0a930920485f1b8e18008078a39ad0c4ada27a3b` | Public `Func<bool> IsHost`; `BeginForNewGame` refuses only when an installed callback returns false; its own subsystem reset clears the callback. |
| PR 1587, `Core/Boats/SlipDoryStory.cs` | `bbd06280846bfdd77563ca864765a60df7e29d36` | Public `System.Func<bool> IsHost` and public `HostHere`; its own subsystem reset clears the callback. `DoryMendingStation` reads `HostHere` for availability and passes it into `TryLaunch(save, boatId, bool host)`. |

All three default to solo authority, but installing only two callbacks leaves
the third permitting writes on a guest. Reset order can also restore one
feature's authority independently. Main has no `ISessionAuthority` or
`SessionAuthority` contract to extend; the existing service location is
`Assets/_Project/Code/Core/Services/`.

Two details matter to the migration: `SlipDoryStory.BeginForNewGame` currently
sets its flag without consulting a host check, and `TryLaunch` trusts the caller's
boolean. Also, a guest's `DemoWeek.OnWord` currently returns true and publishes
on every qualifying received word; host flag persistence alone does not deduplicate
guest presentation. Do not claim the consolidation fixes that separate event issue.

## Exact Core surface

Place the contract in `Assets/_Project/Code/Core/Services/ISessionAuthority.cs`
and its one process-wide access point in
`Assets/_Project/Code/Core/Services/SessionAuthority.cs`, both in the existing
Core namespace and assembly. The declared public surface will be:

```csharp
public interface ISessionAuthority
{
    bool IsHost { get; }
}

// Public signatures for SessionAuthority, a static class:
public static bool IsHost { get; }
public static void Set(ISessionAuthority authority);
public static void ResetToSolo();
```

Every runtime reader calls **`SessionAuthority.IsHost`**. The interface is the
session composition boundary behind that property, not a second selectable
authority. There are no feature-owned callbacks and no public writable host
boolean. No feature names a transport's concrete class. This global answers
one process/session question; it holds no per-player inventory or identity.

Contract semantics:

- Initially and after `ResetToSolo`, the internal solo provider reports true.
  This preserves current single-player behavior, including bare test setups.
- `Set` takes a non-null provider. Null throws `ArgumentNullException` without
  replacing the current provider; null cannot accidentally turn a guest into
  a host. Only session composition and test setup call `Set` or `ResetToSolo`.
- `IsHost` reads the current provider each time. It does not cache a frame's
  authority, infer it from an actor key, or use a clock or random value. A
  destroyed component provider reports false. A provider failure must never
  become a true result; an exception aborts the operation before mutation.
- A co-op provider reports false while connecting, when a guest, and after
  loss of authority. It reports true only for the active host. Install this
  provider **before** starting/loading world services or delivering story events.
  Joining never enters the solo New Game path, even temporarily.
- One subsystem-registration reset belongs to `SessionAuthority`. Individual
  feature listeners must not reset it. Region changes, a guest leaving and a
  listener reinstall do not change authority. The composition root cancels
  pending work and ends the session before calling `ResetToSolo` at title or
  before starting a fresh solo session; never reset a live guest to solo.
- Fixture setup/teardown explicitly resets this seam. Do not attach this reset
  to `GameServices.Reset` until its call sites are audited: a region/service
  teardown must not promote a connected guest. No new saved value is needed.

This property is a local guard, not authorization of arbitrary network input.
Guests send actor-addressed requests; the host validates sender, reach, goods and
request ordering, then commits once. Only the host saves or pauses. A guest may
work in another place and receive the shared ending. No host migration, transport,
pause rewrite or save-schema work is part of this document.

## Mechanical edits after merge

| Source | Exact replacement | Preserve |
|---|---|---|
| `DemoWeek` | Remove its `IsHost` property and private `HostHere`; replace `if (HostHere)` with `if (SessionAuthority.IsHost)`; remove `IsHost = null` from `Install` while keeping `Listen()`. Remove unused `using System` if nothing else needs it. | Host alone sets `demo.island_week_ended`; guests still receive/publish `DemoEnded` and write nothing; full-game behavior, word filtering and the existing host idempotence checks stay unchanged. Do not put the entire event handler behind the host guard. |
| `IslandWeekStart` | Remove its `IsHost` property and `ResetStatics`; replace `if (IsHost != null && !IsHost()) return false;` with `if (!SessionAuthority.IsHost) return false;`. Remove now-unused imports. | Null/current-save checks and the already-begun check; only a newly started host game writes `st_peters.first_week`, once. Continue and joining do not begin the week. |
| `SlipDoryStory` and `DoryMendingStation` | Remove `IsHost`, `HostHere` and `ResetHost`; replace station availability's `SlipDoryStory.HostHere` with `SessionAuthority.IsHost`. Change the mutating signature to `TryLaunch(ISaveService save, string boatId)` and have it pass the live `SessionAuthority.IsHost` into `PeekLaunch`. Update the station and all test/caller sites to the two-argument signature. Guard `BeginForNewGame` with `!SessionAuthority.IsHost` before its first flag write. | Keep the pure `PeekLaunch(ISaveService save, string boatId, bool host)` evaluator and its refusal order, the launch write batch, one launched signal, and the unchanged old-save behavior. A public mutation overload accepting `true` must not survive as an authority bypass. |

For the dory this is a rule correction as well as consolidation: a new-game
flag write and direct launch mutation must obey the same host-only rule already
used at the station. The future PR must say so, migrate tests to configure the
shared authority, and retain every existing write-count and refusal assertion.
Do not weaken the rule or remove the direct-mutation tests to make them pass.

Search all references to `DemoWeek.IsHost`, `IslandWeekStart.IsHost`,
`SlipDoryStory.IsHost`, `SlipDoryStory.HostHere` and `TryLaunch` after the merges.
The inspected tests include `DemoWeekTests`, `IslandWeekBeginsTests` and
`DoryMendAndLaunchTests`; caller inventories may have grown. Replace `() => false`
and `() => true` assignments with one mutable fake `ISessionAuthority` supplied
to `SessionAuthority.Set`. Replace per-feature null resets with one explicit
fixture reset. Keep pure `PeekLaunch` tests free to supply their boolean input.

## Acceptance for that later migration

1. With default solo authority, run the existing ending, beginning and dory tests
   unchanged in their expected gameplay outcomes. Assert exact write and event counts.
2. Install **one** guest fake and drive all three features in the same test:
   ending gives one closing signal and zero writes; beginning returns false with
   zero writes; a fully eligible dory launch returns `NotHost` with zero writes,
   no placement and zero launched signals; dory New Game writes zero flags.
3. With one host fake, first-week beginning and ending each write their existing
   flag once; eligible launch commits once. Repeat each action and keep the
   existing idempotence/refusal expectations, without broadening count assertions.
4. Change that same provider from host to guest between availability and commit:
   the mutation must read the new answer. No stored UI availability authorizes it.
5. Verify null-provider rejection, destroyed-provider refusal, listener reinstall,
   scene change and guest disconnect cannot promote a guest. End the session and
   reset explicitly; the next solo game is permitted again. Fixture teardown leaves
   no provider or listener from the previous test.
6. Extend `ModuleBoundaryTests` for the new Core surface; check there are no remaining
   per-feature host callbacks or public mutating launch overloads taking a host boolean.

No compiler, Unity, EditMode, PlayMode or network test was run for this design.
The three draft heads were inspected, not executed. No saved field is proposed:
host authority is reconstructed from the active session, never restored as a
trusted boolean from disk. The separate player-key and held-goods save work is
described in the actor-service page.
