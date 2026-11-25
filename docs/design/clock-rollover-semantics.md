# Clock rollover semantics: ordinary advance and fast-forward

Status: design for implementation review; no runtime change. Audited at
`00b030cf8d286bc9d3b1464acd55c025e5a26a32`. Resolves the contract needed by
[determinism finding D02](simulation-determinism-audit.md), not the defect itself.
Serves P1 (one readable world clock) and P3 (consistent daily market recovery).

## Decision

Ordinary `GameClock.Advance` and `IGameClock.FastForwardTo` should traverse the same
ordered midnight boundaries. Every crossed midnight publishes one `DayStarted`;
each actual season transition publishes `SeasonChanged` immediately after that day's
notification. During both notifications, all calendar and time reads describe that
midnight. After the call, the clock stands at the requested endpoint and all daily
handlers have been attempted. `SeekTo` remains a silent restore, with no settlement.

This is a proposal for changing the clock's event semantics, including single-day
overshoot reads and fast-forward's intermediate flow reads. It does not authorize
changing prices, calendar tuning, save format, presentation, or the EventBus policy.

## Existing behavior and reused contracts

| Source on the audited base | Existing rule to reuse |
|---|---|
| [GameClock](../../Assets/_Project/Code/Environment/GameClock.cs), lines 105–128, 137–161 | `Update` supplies unscaled wall delta; `Advance` uses the clock's own rate. Fast-forward already traverses days and orders day before season. |
| [IGameClock](../../Assets/_Project/Code/Core/Services/IGameClock.cs), lines 21–68 | Hold separates hour from flow; pause stops frames; seek restores without replay; fast-forward represents passing time. |
| [GameCalendar](../../Assets/_Project/Code/Core/Config/GameConfig.cs), lines 1138–1174 | `AtDay` maps elapsed days through the authored season length and starting calendar offset. |
| [GameSignals](../../Assets/_Project/Code/Core/Events/GameSignals.cs), lines 4–21 | Existing day and season payloads are enough; no event or saved field is added. |
| [Market](../../Assets/_Project/Code/Economy/Market.cs), lines 124–155 | An enabled subscriber runs one `SettleDaily` per `DayStarted`; it does not infer missed days from the payload. |
| [MarketMath](../../Assets/_Project/Code/Economy/MarketMath.cs), lines 57–64 | Recovery is the existing clamped per-day supply multiplication. |
| [EventBus](../../Assets/_Project/Code/Core/Events/EventBus.cs), lines 6–10, 61–72 | Synchronous notifications, per-publish subscriber snapshot, exceptions logged with delivery continuing; no transaction. |
| [Time design](time-tides-weather.md#13-sleeping--skipping-time) | Sleep skips frame simulation but settles crossed days. Existing bed/bunk completion and save ordering remain intact. |

Clock seconds here use the **implemented** `SecondsPerDay` coordinate: at the default
1800 seconds/day, hour 22 is 1650 seconds into the day. Do not introduce the separate
48× conversion illustrated in the older time-design prose; `Advance` currently adds
`wallSeconds * max(0, TimeScale)` directly. This item changes no unit or pace.

### Reproduced D02 trace

Start at elapsed day 0, 22:00 (`T=1650`), with flow equal to time and supply 8 in one
market category. End at day 3, 06:00 (`T=5850`); recovery is the existing 0.5 fixture.
The supply column is read **after** the market handler in this controlled subscription
order. Values are an example, not new balance constants.

| Operation on current code | Day callbacks: `DayIndex / TotalSeconds / FlowSeconds / supply` | Final time / flow / supply |
|---|---|---|
| `Advance(4200)` | `3 / 5850 / 5850 / 4` | `5850 / 5850 / 4` |
| `Advance(150)`, `Advance(1800)`, `Advance(1800)`, `Advance(450)` | `1 / 1800 / 1800 / 4`; `2 / 3600 / 3600 / 2`; `3 / 5400 / 5400 / 1` | `5850 / 5850 / 1` |
| `FastForwardTo(5850)` | `1 / 1800 / 1650 / 4`; `2 / 3600 / 1650 / 2`; `3 / 5400 / 1650 / 1` | `5850 / 5850 / 1` |

There are three distinct discrepancies to address together:

1. A multi-day ordinary step omits daily settlement. Across a full four-season year
   it also omits all four season events when its endpoint season matches its start.
2. Ordinary day callbacks read the end of the step, even when it overshoots only one
   midnight. Fast-forward's calendar callbacks already read each midnight.
3. Fast-forward updates `_flow` only after the loop. Its callbacks can therefore read
   flow earlier than time, even with no prior hold. Its final flow value is correct.

## Proposed operation contract

### Entry gates and final values

Let `T0` and `F0` be the entry time and flow, `L` the configured seconds per day,
and `delta` the accepted forward span. Snapshot these and calendar configuration
before dispatch; no handler changes the target of an operation already in progress.

| Operation | Acceptance and completion |
|---|---|
| `Advance(wallSeconds)` | Missing config or pause: no change. Preserve the existing float product `wallSeconds * Mathf.Max(0f, TimeScale)` before widening to double; do not silently change integration precision. Zero span: no events. Unheld: `T1=T0+delta`, `F1=F0+delta`. |
| Held `Advance` | Flow increases by the accepted span; hour and calendar stay fixed; no day or season events, even across many days of flow. |
| `FastForwardTo(target)` | Missing config, held hour, or target no later than time: false and no change. Pause does not refuse it; `TimeScale` does not rescale its absolute target. For accepted input, `delta=target-T0`, `T1=target`, `F1=F0+delta`, return true after dispatch. |
| `SeekTo(target)` | Outside dispatch, preserve restore semantics: finite negative input clamps to zero, both readings become the target, rollover trackers rebaseline, no events or market recovery. |

Reject non-finite input/state, negative wall delta, non-finite rate, invalid day
length, or unrepresentable target/day/calendar before changing either reading.
For void methods rejection is a no-op; fast-forward returns false. A finite negative
rate still clamps to zero. Non-finite seek must also be a no-op, so it cannot poison
the next advance. These are **proposed guards**, not claims about today's validation.
Do not convert an invalid forward request into a seek or a partial advancement.
Days per week and season must be positive. Use the existing starting-season/day
normalization; no retuning or new gameplay cap is proposed.

### Boundaries and subscriber reads

For valid forward time, visit each integer elapsed day `d` whose canonical midnight
`B(d)=d*L` lies in `(T0,T1]`, in increasing order. The starting midnight is excluded;
the ending midnight is included. No day-zero announcement occurs merely because a
clock was configured. Initialize/rebaseline its trackers before accepting advances.

For each boundary:

1. Set time to `B(d)` and flow to `F0 + (B(d)-T0)`. Set the current day tracker to `d`.
2. Derive the date once with `GameCalendar.AtDay(d, daysPerSeason, startDayOfYear)`.
   During both notifications, `DayIndex`, `Now`, `TotalSeconds`, `DayOfSeason`,
   `Season`, `Year`, `Weekday`, and `IsMarketDay` must agree with this boundary;
   `HourOfDay` and `DayFraction` are zero. Weekday uses elapsed day modulo the
   configured week, as today, without the seasonal label offset.
3. Determine whether the season changed from the previous boundary and update the
   internal season tracker before callbacks can observe/reenter the clock.
4. Publish `DayStarted` with that date. Then, if the season changed, publish
   `SeasonChanged` with that same season/year. Keep the same boundary reads through
   the complete pair. Repeat for the next midnight.
5. After the last boundary, set time to `T1` and flow to `F1`, including any remainder.
   A following zero step or same-day step must not repeat the last notifications.

An existing flow-minus-hour gap from a hold survives both kinds of advancement.
The example trace under the proposed contract is the segmented row above for both
entry points. With an initial gap of 10, callback flow is `1810, 3610, 5410` and final
flow is `5860`. This intentionally changes fast-forward's callback flow readings.

Represent boundary identity using the integer day, not an unchecked division of
rounded `d*L` back by `L`. At an exact endpoint retain that same identity. The final
classifier and boundary enumerator must agree on `[B(d),B(d+1))`, including fractional
authored day lengths; use comparison against canonical boundary products to correct
a quotient if necessary. Do not add an epsilon that starts a day early. Use wider
loop arithmetic and validate the existing `int DayIndex` range before dispatch, so
the last increment cannot overflow. This adds no save field or wider public day type.

The guarantee is ordered event/settlement equivalence for the same representable
start, endpoint, configuration, subscribers and intervening inputs. Arbitrary
partitions of floating-point frame deltas need not yield bit-identical endpoints;
this proposal neither changes that arithmetic nor promises complete frame simulation.

### Callbacks, reentrancy and failures

The advancing call is synchronous and owns its chosen span. Reject nested
`Advance`/`FastForwardTo`/`SeekTo` on that clock until it returns; do not queue them
or recursively replay days. Nested void calls are no-ops; nested fast-forward returns
false. Clear the guard in `finally`. This is a proposed clock guard, not a change to
EventBus's support for nested publications of other events.

Changes to `IsPaused`, `IsHeld` or `TimeScale` made by a handler remain set for the
next operation; entry snapshots govern the current one. A handler must not mutate
calendar configuration during dispatch. Save/load, trading and other operations
that require the completed span must run after the clock call returns, not inside a
day notification. No new deferred job system or save format is needed.

Retain EventBus delivery policy. A throwing subscriber is logged and later handlers
and boundaries still run. "Once" means one publication attempt per boundary and one
invocation per registered handler entry, not guaranteed economic success. There is
no rollback, retry, durable exactly-once ledger, or deduplication of bad subscriptions.
Tests collect observations inside callbacks and assert afterward: swallowed callback
assertions must not make a test look green, and unexpected logs must fail Unity tests.

## Market settlement and consumer consequences

Every currently subscribed market receives one daily settlement per crossed midnight,
including non-market weekdays. `IsMarketDay` is not a recovery gate. Reuse
`Market.OnDayStarted` and `MarketMath.SettleSupplyDaily` unchanged:

```text
S(next) = max(0, S(current) * (1 - clamp01(configuredRecovery)))
repeat once for each crossed midnight, in event order
```

Do not replace repeated float multiplication with `pow`, consolidate days into one
event, or compensate inside `Market`: those can change rounding or double-settle
sleep. Per-category supply, demand, price levels, sale increments and recovery values
stay as authored. With the same sale sequence and fixed recovery, one large ordinary
advance, midnight-sized advances, and fast-forward end with the same supply bits and
the same resulting quotes. Different sale times are different histories.

The bus calls handlers in registration order. A day subscriber registered **before**
a market may still read its pre-settlement supply; one registered **after** it reads
post-settlement supply. Clock consistency does not create an economic transaction.
Only after the advance returns are all subscribed markets' handlers completed or
logged as failed. Do not add handler priorities or a second economy phase in this fix.

Disabled/unloaded markets miss notifications today. This proposal does not reconstruct
their history, persist market state, replay events on enable, or change sell/load
semantics. An enabling subscription during a publish affects later publishes under
the existing EventBus snapshot rules. Tests of equivalence keep subscriptions fixed.

The production subscriber scan found `Market` and
[FoliageSnowBridge](../../Assets/_Project/Code/Art/FoliageSnowBridge.cs), lines 78–106:
the latter reads the day payload and rereads the clock on a season change. The
boundary contract keeps those dates aligned. No bridge, shader, snow setting or
pixel expectation is changed by this document. Future runtime integration must
review its presentation consequences through the appropriate art lane; a headless
logic check does not establish visual equivalence.

## Implementation and acceptance handoff

Keep the traversal in Environment, with existing Core calendar/event contracts. Both
entry points should share a private forward-span path; do not implement `Advance`
by blindly calling public `FastForwardTo`, because their pause/hold/rate gates differ.
Core interface documentation must describe boundary flow and nested-call refusal.
Calendar classification, if factored into a pure Core helper, must be shared by the
clock's calendar reads rather than silently forking the calendar formula. No feature
module references Economy, and no market code change is needed.

Add these EditMode contracts when implementation is separately authorized. Assertions
must check exact counts and ordered payload/read traces, not merely event presence.

| Case | Required assertions |
|---|---|
| No boundary; zero step | No events; ordinary final time/flow arithmetic preserved. |
| One midnight with overshoot | One day callback at midnight, not endpoint; final remainder restored. |
| Three midnights, both entry points and partitioned advance | Exactly days 1, 2, 3; matching time/calendar/flow reads; example supply `4,2,1`; final supply bits and quotes match. |
| Exact endpoint and next frame | Endpoint midnight included once; zero or same-day follow-up adds zero events. |
| Start exactly at midnight | No replay of the starting day. |
| Season and year crossing; full-year span | Day before season at each turn; four season events across four seasons even if start/end season match; exact calendar years. |
| Authored calendar offset/week | Use configured start season/day and lengths; unchanged weekday/market-day definition. |
| Existing flow gap | Gap retained at every callback and completion for both operations. |
| Hold and pause | Held advance changes only flow; pause changes neither; held fast-forward refuses; paused unheld fast-forward succeeds. |
| Seek and first subsequent step | No replay/recovery, flow reset, no duplicate rollover; invalid seek does not corrupt state. |
| Fractional day length and neighboring doubles | Correct side of each canonical midnight, zero boundary hour, no epsilon or duplicate; payload equals getters. |
| Invalid inputs and representable limits | Negative/non-finite wall delta, rate/target/state/config errors and day overflow change nothing; negative finite rate remains clamped. |
| Nested time mutations and changed flags | Every listener sees the same boundary; nested moves refuse; flag/rate changes affect the next call only. |
| Throwing handler | Expected exception logged once; later subscriber and later boundaries still run; guard cleared after operation. |
| Market ordering and recovery edges | Before/after subscribers retain their order; recovery 0 retains supply, 1 clears it, empty/multiple categories remain isolated; no extra direct settlement. |

Retain the existing `BedBringsTheMorningTests`, `GameClockHoldTests`,
`GameClockReadsWallTimeTests`, `DayCalendarTests`, market and save/restore suites.
The bed's midnight assertion remains strong. If any test pins ordinary endpoint
reads or stale fast-forward flow, migrate it to the explicit new trace above and
name that contract change in the implementation PR. Do not loosen tolerances or
remove assertions to hide D02. Graphics-dependent checks need editor evidence;
the present document makes no claim about pixels.

Validation for this design: eight source-backed characterization cases passed in an
offline .NET harness using the unchanged clock, bus, market and market math, verbatim
calendar/signal extracts, and minimal Unity API/config stand-ins. They reproduced
the three trace rows, exact endpoint behavior, hold/pause gates, final held gap,
four missing season events, and silent seek. This is **not** a Unity EditMode run
and does not test the proposed implementation. Only this document is committed.

Cost of the eventual traversal is O(crossed days + dispatched handler work), with
no per-frame allocation needed in the clock itself. Market's existing per-day key
copy remains. Do not silently cap or drop days to avoid a long synchronous jump;
bounded UI requests or measured catch-up scheduling would be separate work. No
later-phase simulation, persistent catch-up service, asset edit or save write is
part of this item.
