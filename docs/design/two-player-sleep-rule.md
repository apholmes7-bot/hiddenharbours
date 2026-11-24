# Deciding when the night may pass

The night may pass only when every participating player is lying in a separate bunk.
A wheelhouse bench counts as a bunk. The bunks may be aboard and ashore, in different
regions. Sleep still lasts at most eight game hours and keeps the completed rest.
This change extracts the decision and tests it; it does not create a second character
or make the current save capable of resuming two players.

The existing implementation was found in `Core/Save/RestQuorum.cs`, `SleepFlow.cs` and
`RestSaveResponder.cs`; the cap and duration arithmetic already live in `Core/Time/SleepPlan.cs`.
The existing `Assets/Tests/EditMode/Save/SleepFlowTests.cs` fixture is extended, with a
partial file for quorum cases. Its instant-versus-chosen sleep, day rollover, needs,
early wake, occupancy and save assertions are retained. Three characterization cases
were written before the extraction for named, empty and null legacy bed ids: each must
reach the same morning, write once and preserve the original rest anchor. No local Unity
or compiler run was performed; these assertions await CI execution.

## Core contract

`IRestQuorum.Evaluate(IReadOnlyList<RestParticipant>)` is implemented by the plain
`RestQuorumRule` class in the existing `RestQuorum.cs`. It reads only its input, changes
nothing and publishes nothing. Each `RestParticipant` is a session-only snapshot of
`PlayerId`, `BunkId` and `RegionId`. A missing/blank bunk means standing. Bunk ids must
identify physical instances globally, for example a particular boat's wheelhouse bench,
not a reusable boat type or a room-local name. Equal ids mean the same bunk even if a
caller supplies inconsistent region labels.

The caller supplies the complete roster, including players who are standing. An empty
or null roster refuses sleep. Repeating a player does not count twice. Refusal keys have
stable precedence independent of list order:

| Condition | Text key |
|---|---|
| No roster/participants | `sleep.no_players` |
| A player id appears twice | `sleep.repeated_player` |
| At least one player is not in a bunk | `sleep.player_not_in_bunk` |
| Two players name the same bunk | `sleep.shared_bunk` |
| Every player has a distinct bunk | Empty key; `MayPassNight` is true |

These are plain result keys. There is no new banner, localisation asset or pose.
Eligibility assumes the caller has validated the authored rest surface; an arbitrary
chair is not made a bunk by this rule. Boat/bed presentation and art are unchanged.

## Session adapter and revalidation

`RestQuorum.ParticipantSource` is an optional callback returning a current complete
occupancy snapshot. It does not allocate actors, track network presence or decide who
counts as a participant after a disconnect. A future session layer must update this
input when someone lies down, gets up or leaves. `SnapshotFor` copies the supplied
snapshot; changing the source afterwards does not change a previously captured array.
An installed source returning null fails closed; it never falls back to solo sleep.

With no source installed, the old local player remains the sole participant. Existing
named bed claims are rechecked at confirmation and before time advances. Anonymous
legacy rest requests are represented by a private compatibility bunk id in memory;
the original request, including its empty saved bed id, remains intact. This exception
does not apply to a supplied roster: a participant without a bunk always refuses.

`SleepFlow.Confirm` now uses this decision instead of `RestQuorum.IsMet(1)`.
`SleepFlow.LastReadiness` exposes the latest occupancy result without a new event.
The decision is taken again before each fast-forward step. If a player leaves during
the transition into sleep, the first tick returns to awake with no clock advance and
no save. If some time has already passed, the existing early-wake path keeps that
completed interval once. The old count helpers remain for compatibility and tests;
they no longer authorize the night.

The instant fallback in `RestSaveResponder.Fulfil` checks the same rule before the cover
and again after cover listeners have run. A refusal returns `GameSaved(false, textKey)`
without publishing it. Both paths retain `SleepPlan`'s existing cap and defaults.
`SleepFlow.Reset` clears the optional source through `RestQuorum.Reset`.

## Persistence boundary

The current flow still has one local request, one local needs service and one v25 rest
anchor. This is a readiness seam for later integration, not a completed two-player
sleep/save system. Session snapshots are not saved; no GameConfig value or saved field
changes. To persist two sleepers, a separate approved migration would need player ids
and per-player `RestRegion`, `RestPosX/Y`, `RestLevel`, `RestBedId`, `RestWokeAtSeconds`,
aboard/support identity and personal needs/body state. The current `RestBoat*` pose
should become one world pose per physical hull, referenced by each sleeper, rather
than two competing copies. One checkpoint must include both players and the shared
clock atomically. It must not infer a second player's anchor from the first player's
record. No migration or technical save trigger is added here.
