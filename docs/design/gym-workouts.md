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
  in `CleanlinessService`, guarded against a missed stop. Cooking is currently a data seam:
  `Core/Food/RecipeDef.WorkSeconds` declares game-time work, while `RecipeBook` only selects recipes;
  no runner consuming that duration exists on this baseline. These are the timing precedents.

The pillars in `CLAUDE.md` section 1 and `docs/vision-and-pillars.md` guide this slice: P3 gives the
hall a useful daily activity; P4 makes training take the player's time and effort; P5 trades a
stronger body against meals, rest and washing without starvation locking him out. P2's later
strength benefits remain a design question, not new automatic transport or fish-catching power.

## One session

A `WorkoutDef` names exactly one equipment Def and one offered exercise. Its stable `workout.*`
id, normal duration, units, exertion rate, minimum pace and daily credited-unit limit are data.
There is one plan for each of the nine existing exercises. Initial tuning: ten game minutes per
session; thirty repetitions for lifts/grip work, ten minutes for cardio; three sessions' units
credited per exercise per game day; extra tiredness 0.12 per game hour; pace floor 0.6. These are
starting balance values, not accepted feel or visual decisions.

`WorkoutService.TryStart` takes the plan, a nonempty equipment-instance key, current absolute game
seconds and current body mass. The caller must establish proximity and exclusive use. One service
belongs to one player and permits one session at a time. An active session cannot be overwritten.
At start it copies its recipe, mass-scaled energy and needs pace, so editing a Def during a set
cannot alter that set. Pace is the existing needs walk multiplier clamped to [plan floor, 1].
Hunger and tiredness can lengthen the session; neither refuses it. A later meal affects the next
session, keeping this session independent of tick cadence.

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

- Hunger: existing `StrengthMath.ActivityKcal` and `BodyMath.KcalPerHungerUnit` convert the exercise's
  kcal and starting body mass into a meal fraction. Completion/cancellation calls `INeedsService.Spend`
  once. Hunger saturates at one. This slice does not turn overflow into body-mass loss: that remains
  with the saved body activity path and needs an explicit integration decision.
- Tiredness: passive clock rise still applies, plus the plan's exertion per actual game hour,
  through `INeedsService.Exert`. It clamps at one and announces stage changes like eating/sleeping.
- Cleanliness: actual workout hours, capped at the session end, are reported once through
  `ICleanlinessService.ReportActivity(DirtActivity.Gym, hours)`. It uses the existing gym sweat rate,
  without a per-action bump or a live stretch that might keep dirtying him after an overshot tick.
  Passive dirt remains the cleanliness service's responsibility. Never also begin a Gym stretch.

Costs land at the observing clock instant because existing needs services settle at their clock.
The receipt records the exact workout endpoint; runtime fitness is trained at that endpoint and
decays from there even after a long jump. A future integration must settle/cancel before another
activity, meal, wash or sleep; retrospective interleaving with those actions is not supported.

`WorkoutRuntimeStore` owns four independent unsaved fitness anchors. It uses the existing
`FitnessMath.Train` and `StrengthSettings` curves: diminishing exponential gains, cheap maintenance,
grace then linear decay, remembered peak and faster regain below that peak. It does not call
`BodyService.ReportActivity` or replace `GameServices.Strength`, so no saved fitness, carrying,
walking, rod force or hauling speed changes from these gains.

Credit is capped by exercise id, shared across equipment instances/plans. The settlement day
(`floor(endpoint / secondsPerDay)`) receives the units, including partial cancellations. The
four fitness kinds receive only credited units; costs still apply after the cap. Switching machines
or cancelling into many fractions cannot bypass it. Midnight opens a new allowance; a set crossing
midnight counts on its endpoint day. The allowance is bounded memory: only the most recent day per
exercise is retained. There is no global lockout; the player can train another exercise or continue
without further gains. Content validation requires one plan per exercise and reciprocal equipment
references so conflicting cap definitions cannot ship.

## Boundaries and future persistence

Built: validated plans for all nine exercises, session timing, costs, daily caps, runtime fitness,
cancellation and EditMode logic/content tests. Designed only: interaction, exercise poses and sound,
access hours/fees/NPCs, persistent progress and fitness effects on fishing/hauling.

No save fields or migrations are added, including to the pending v24 work. Persistence would need:

- A decision to merge workout gains into the existing four saved body fitness states, or save
  these separate anchors (level, peak, anchor time, maintained time, pending dose). Never both.
- Per-exercise credited day and units, otherwise loading resets the daily allowance.
- If mid-session resume is wanted: plan and equipment-instance ids, captured duration/pace,
  start/end time, unit and cost snapshots, and a settled marker. Alternatively cancel and settle
  before saving, and persist no session. Loading a receipt must never charge costs twice.

Open decisions for the owner: should grip alter rod authority, back alter hauling time, and legs
alter comfortable carried weight? What are their caps and failure boundaries? Should cardio make
those jobs cheaper or only movement? How should gym gains combine with work-earned fitness and
body-mass loss when hungry? This slice makes none of those gameplay changes. Goods still require
manual carrying/loading/unloading, regardless of the eventual strength benefit.

## Verification

`WorkoutSessionTests` pins repeat/overshoot timing, hunger pace, cancel fractions, midnight/caps,
runtime decay and memory, data snapshots, invalid input and reset boundaries. `WorkoutNeedsTests`
checks the real Core needs/cleanliness implementations. `WorkoutContentTests` checks every shipped
plan and its links. Identity additions accompany new Def identities; the baseline ledger is frozen.
No editor lease is granted for this work: Unity import and EditMode execution are CI proof, not a
claimed local editor run. Offline checks and their exact limits are recorded with the draft PR.
