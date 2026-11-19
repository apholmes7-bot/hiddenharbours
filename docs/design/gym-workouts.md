# Gym workouts: logic slice

Status: implemented as Core logic and data; no scene interaction is connected. The player sees no
change yet. This is the workout seam for the community hall, not a new fishing or hauling system.

## Grounding

- `docs/world/nmc-gym.md` records the equipment request and proposed hall room. Draft PR 1276
  supplies seven equipment markers; its scene, builders, prefabs and tests are outside this change.
- `Assets/_Project/Code/Core/Body/GymEquipmentDef.cs` and `ActivityDef.cs`, with the seven assets
  in `Data/Resources/GymEquipment` and nine exercise assets in `Data/Resources/Activities`, already
  specify equipment, repetitions/minutes, kcal and grip/back/legs/cardio doses.
- `BodyService.ReportActivity` already spends hunger and trains saved fitness. `BodyMath`,
  `StrengthMath`, `FitnessMath` and `StrengthSettings` define mass, energy, strength, diminishing
  returns, maintenance and muscle memory. There is no separate stamina pool in these contracts.
  `docs/architecture/data-model.md` describes the v21 body and fitness anchors.
- `Core/Needs/INeedsService.cs` exposes hunger spending and rest; `NeedsService` derives passive
  hunger/tiredness from game time. `Core/Cleanliness/ICleanlinessService.cs` exposes washes and work;
  `DirtActivity.Gym` already has a sweat rate in `CleanlinessSettings.Sources`.
- `Core/Time/SleepPlan.cs` and `Core/Save/SleepFlow.cs` plan/advance sleep using absolute game time.
  `Core/Kitchen/SeatSession.cs` banks sitting through the needs interface; kitchen storage ticks
  run beside needs in `Environment/GameClock.TickNeeds`. Washing integrates a clock-stamped stretch
  in `CleanlinessService`, guarded against a missed stop. `Core/Food/RecipeRunner` now consumes
  recipe work against game time and the v24 cook-job rows. These are the timing precedents.

The pillars in `docs/vision-and-pillars.md` guide this slice: P3 gives the
hall a useful daily activity; P4 makes training take the player's time and effort; P5 trades a
stronger body against meals, rest and washing without starvation locking him out. Training uses
the body's existing rod, pull, carry and walk benefits; goods still move by hand.

## One session

A `WorkoutDef` names exactly one equipment Def and one offered exercise. Its stable `workout.*`
id, normal duration, units, exertion rate, minimum pace and daily credited-unit limit are data.
There is one plan for each of the nine existing exercises. Initial tuning: ten game minutes per
session; thirty repetitions for lifts/grip work, ten minutes for cardio; three sessions' units
credited per exercise per game day; extra tiredness 0.12 per game hour; pace floor 0.6. These are
starting balance values, not accepted feel or visual decisions.

`WorkoutService.TryStart` takes the plan, a nonempty equipment-instance key and current absolute game
seconds. It reads mass from its injected `IBodyService` for input/overflow validation. The caller
must establish proximity and exclusive use. One service belongs to one player and permits one
session at a time. An active session cannot be overwritten.
At start it captures the plan's duration, units, exertion rate, daily limit and needs pace, and keeps
the authoritative exercise Def for body reporting. Energy and training are not copied into a second
fitness model: the body reads the activity and current saved body at settlement. Pace is the existing
needs walk multiplier clamped to [plan floor, 1].
Hunger and tiredness can lengthen the session; neither refuses it. A later meal cannot change the
captured duration, but the body still owns its current energy cost.

The future adapter calls `Tick(clock.TotalSeconds)` before needs on the existing game-time slow tick and
`Cancel(clock.TotalSeconds)` before leaving, sleeping, washing, switching activity or unloading.
No adapter, subscription, input, pose, animation, UI or MonoBehaviour is installed here. Paused or
held game time makes no progress. Duplicate ticks do nothing. Time must be finite, nonnegative
and monotonic; a rewind is rejected before mutation. Discard/recreate this runtime service on a
save load or new game. Never replay a restored clock through an old session.

The first tick reaching the planned end settles exactly one session. Overshoot never adds extra
work. Cancel settles the fraction actually completed, including costs and proportional training;
zero elapsed time earns/costs nothing. At or beyond the end, cancel is a normal completion.

## Costs and gains

- Effort: completion/cancellation reports credited units through `IBodyService.ReportActivity`.
  That one path computes kcal from the current body mass and cardio, spends hunger, burns overflow
  from mass down to the frame's floor, trains saved fitness and publishes `ActivityReported`.
  Current `StrengthMath` balance applies cardio savings to movement only; gym Exercise costs retain
  the existing rule. The workout does not spend hunger separately.
- Units past the daily cap report through the same path with `train: false`. This optional Core
  parameter defaults to `true`, preserving existing callers. It skips all fitness changes, including
  maintenance, but retains energy, hunger, overflow and the activity event. A set straddling the cap
  reports excess units first, then credited units; its receipt sums the energy-derived hunger.
- Tiredness: passive clock rise still applies, plus the plan's exertion per actual game hour,
  through `INeedsService.Exert`. It clamps at one and announces stage changes like eating/sleeping.
- Cleanliness: actual workout hours, capped at the session end, are reported once through
  `ICleanlinessService.ReportActivity(DirtActivity.Gym, hours)`. It uses the existing gym sweat rate,
  without a per-action bump or a live stretch that might keep dirtying him after an overshot tick.
  Passive dirt remains the cleanliness service's responsibility. Never also begin a Gym stretch.

Costs land at the observing clock instant because existing needs services settle at their clock.
The receipt and daily allowance record the exact workout endpoint; saved fitness is anchored at
the body's observing clock, like other activities. A long jump bounds units to the planned set but
does not backdate training. A future integration must settle/cancel before another
activity, meal, wash or sleep; retrospective interleaving with those actions is not supported.

`PlayerBodyDto.Grip/Back/Legs/Cardio` are the only fitness anchors. They already persist in the
body save (present before v24), and the existing strength reader supplies rod strength, pull,
carry capacity and walking benefits. `BodyService` uses `FitnessMath.Train` and `StrengthSettings`:
diminishing exponential gains, cheap maintenance, grace then linear decay, remembered peak and
faster regain below that peak. `WorkoutRuntimeStore` keeps only daily credit; it has no fitness
anchors, training logic or independent decay clock.

Credit is capped by exercise id, shared across equipment instances/plans. The settlement day
(`floor(endpoint / secondsPerDay)`) receives the units, including partial cancellations. The
four fitness kinds receive only credited units; costs still apply after the cap. Switching machines
or cancelling into many fractions cannot bypass it. Midnight opens a new allowance; a set crossing
midnight counts on its endpoint day. The allowance is bounded memory: only the most recent day per
exercise is retained. There is no global lockout; the player can train another exercise or continue
without further gains. Content validation requires one plan per exercise and reciprocal equipment
references so conflicting cap definitions cannot ship.

## Boundaries and future persistence

Built: validated plans for the existing exercises, session timing, body-reported effort and saved
fitness, costs, daily caps, cancellation and EditMode logic/content tests. Designed only: interaction, exercise poses and sound,
access hours/fees/NPCs and persistent daily allowances or interrupted sessions.

No save fields or migrations are added. Main already has v24; it has no workout-credit row.
Fitness gains persist through the existing saved body and need no new persistence policy.
Only daily credit, the active session and its last receipt remain session-only:

- A save after v24 would need one row per exercise: `(exercise id, day, units)`. Until then,
  recreating the workout service on load resets the daily allowance; saved fitness remains.
- If mid-session resume is wanted: plan and equipment-instance ids, captured duration/pace,
  start/end time, captured units/limit/exertion, exercise identity and a settled marker. Alternatively
  cancel and settle before saving, and persist no session. Loading a receipt must never charge costs twice.

Open integration decisions: persist daily allowances in a later save version, and either settle
before saving or define interrupted-session resume. Workout and work-earned training already
combine through the same body anchors and balance. Goods still require manual carrying, loading
and unloading regardless of strength.

## Verification

`WorkoutSessionTests` pins repeat/overshoot timing, hunger pace, cancel fractions, midnight/caps,
saved-body decay and memory, captured plan timing, current body costs, activity events, invalid input
and reset boundaries. `WorkoutNeedsTests` checks real body/needs/cleanliness behavior, saved fitness
and player benefits after service recreation, cardio effort, and overflow-to-mass even past the cap.
`WorkoutContentTests` checks nonempty content, every asset's validity, unique ids and reciprocal links,
with catalog coverage instead of fixed plan/equipment counts. Identity additions accompany new Def
identities; the baseline ledger is frozen.
No editor lease is granted for this work: Unity import and EditMode execution are CI proof, not a
claimed local editor run. Offline checks and their exact limits are recorded with the draft PR.
