# Bite sequence timing across real-time ticks

## Decision

Do **not** carry phase overshoot into the next phase of `BiteSequenceSim`.
Keep the current one-transition-per-call rule, including quiet approach and return phases.
This is the lane's design decision for the F6 follow-up, not a new owner ruling.
The implementation and species tuning remain unchanged.

The bite is a real-time reaction to a tell. The controller reads one strike edge, calls
`Tick`, then publishes the resulting phase. A catch-up loop could enter and exhaust the
true take within one call, before the controller could publish it. That would turn a hitch
before the take into a missed opportunity the player never received. Keeping each new
phase's full clock supports the [false-nibble design](rod-fishing-v2-brainstorm.md#102-the-bite--hook--his-picture)
and the canon's readable, forgiving hand work (P4/P5). This does not promise that the
renderer displays every update; it bounds what this simulator consumes in one call.

## Exact contract

For finite `dt`, judge the strike against the phase at call entry, before advancing time.
A strike in `TakeOpen` hooks even if this call would expire the window. A strike in
`Approach` or `NibbleDip` ends the pass as early. A strike while `Away` is ignored; time
still advances. A strike is never retried against a phase entered later in the same call.

On the quiet path, add `max(0, dt)` to the current phase clock. If the duration has been
reached, advance once. Every newly entered phase starts at zero. Discard the excess;
there is no deferred time debt. This includes `Away -> Approach`, `NibbleDip -> Approach`
and a late take's return delay. An exact endpoint transitions too. Terminal outcomes
remain terminal. The terminal `Gone` clock can retain the expired take's elapsed time;
it is not a new timed phase.

The profile's authored phase durations are unchanged. They are minimum simulated dwell
times for a newly entered phase across subsequent calls, not a promise of an exact total
wall-clock sequence duration. Negative time remains clamped to zero. Nonfinite input
policy is outside this decision; no new clamp or acceptance rule is introduced.

## F6 failing-case comparison

Use seed 42, zero false nibbles, a fixed 0.25-second approach, a 0.5-second take, no return,
and one pass. All values below are exactly representable as binary floats.

| Calls, no strikes | Result after 1 second supplied | Phase seconds | Pass index | Last miss |
|---|---|---:|---:|---|
| `Tick(1)` | `TakeOpen` | 0 | 0 | None |
| Four calls of `Tick(0.25)` | `Gone` | 0.5 | 1 | Late |

The partitioned trace is take at 0.25, take age 0.25 at 0.5, gone at 0.75, still gone at
1.0. A partition-invariance assertion comparing the two final phases **fails**. A further
`Tick(0, true)` hooks the large-step sequence while the partitioned sequence remains gone.
This is a player-relevant difference, not merely a private timer difference.

`BiteSequenceTickContractTests` records both exact outcomes and the failed-equality
counterexample as passing characterization tests. It also pins discarded fractional and
huge overshoot, no banked debt, nibble/gap/take visibility to the caller, strike ordering
and return ordering. Existing strike, retry and replay assertions remain intact.

## Consequences and limits

Total elapsed time alone does not determine this interaction. Frame partitioning can
lengthen a pass, and a hitch can make the encounter more forgiving. With finite calls
there is at most one timed transition per call, so the quantization delay accumulates at
phase boundaries. There is no catch-up for sleep or unloaded regions. Do not feed this
sequence through a world slow-tick scheduler or promise tick-partition determinism.
Replay requires the same profile, seed **and ordered `(dt, strike)` calls**.

Do not reinterpret F6 as a request for an overshoot loop or silently reduce the authored
take window to compensate. If exact elapsed schedules become a requirement, first design
timestamped input and a way to expose every actionable take to presentation, then migrate
this rule and its tests together. That would be a separate gameplay change.

## Validation scope

The seven new NUnit cases exercise the actual plain simulator and profile without scenes
or native object creation. They specify current behavior, not frame-rate fairness or a
visual acceptance result. No runtime, asset, price, save or presentation change is included.
