# Idle fidgets

Pillars: P3 (a living, working coast), P1 (the sea has moods: a glance at the sky).

A person standing still does small things on their own. The player and every villager and skipper
use the same rules, through one shared piece (`CharacterFigureLife`).

## What ships

| Id | Channel | Weight | Length | What it is |
|----|---------|--------|--------|------------|
| `idle.look_around` | Look | 3 | 4.2 s | looks along the wharf to the left, then to the right |
| `idle.sky_glance` | Look | 2 | 3.4 s | looks up at the weather |
| `idle.look_down` | Look | 2 | 2.8 s | checks the water or the deck at his feet |
| `idle.weight_shift` | Clip `idleShift` | 3 | 2.4 s | shifts his weight, resets his feet |
| `idle.scratch_head` | Clip `idleScratch` | 1 | 2.6 s | scratches under his cap |
| `idle.hands_on_hips` | Clip `idleHips` | 1 | 4.0 s | hands to his hips, looks out, hands back down |

- **Look** fidgets play a gaze track through the rig's own look: the neck, head and eyes, in pixel-safe
  head snaps. They work on every facing with the current rig.
- **Clip** fidgets need rig clips that do not exist yet. A skin that lacks the clip never picks the
  fidget, so these Defs ship now and switch on when the export lands
  (see "Rig export needed" below).

## Data

- One `IdleFidgetDef` per fidget under `Assets/_Project/Data/IdleFidgets/`.
- One `IdleFidgetLibraryDef` at `Assets/_Project/Resources/IdleFidgetLibrary.asset`. It holds:
  - the switch;
  - the least and the most gap between fidgets (4 s and 11 s);
  - the settle, how long a person must stand still before a fidget may start (2 s);
  - the gaze distance (6 m).

All of these are tunable without code.

## The schedule

Which fidget plays and when is a pure function of (world seed, game time, the figure's key):
`IdleFidgetSchedule`. Nothing is saved and nothing draws a random number.

- Each figure's time is cut into slots of `MinGap + J + longest + stretch`, where:
  - `J = (MaxGap - MinGap) / 2`;
  - the stretch is the figure's own, in `[0, J/2]`.
- Each slot starts at a jitter in `[0, J/2]`, which keeps every gap inside the bounds. A per-figure
  phase and slot length mean two people side by side drift apart instead of locking in step.
- The choice is by weight. A slot re-draws when it would repeat the slot before, judged four slots
  back. With three fidgets about one slot in twenty repeats; with six, under one in a hundred.

## What stops a fidget

A fidget only plays over the plain standing `idle` clip, so walking, carrying, work, beats and every
performance clip suppress it by being themselves. On top of that, these all end a fidget the same
frame:
- the oars, rowing, or the helm (seated or standing);
- an NPC on a driven move or at work;
- the player touching any control;
- a villager who has the player inside her look radius. She attends to him rather than looking about.

A person who stops mid-slot waits for the next slot. He is never dropped into the middle of a head
turn.

## Tests

- **EditMode:**
  - `IdleFidgetScheduleTests`: determinism, never in step, gap bounds, weights, settle and cancel,
    channels, zero allocation.
  - `IdleFidgetContentValidationTests`: the shipped assets.
  - `IdleFidgetLifeTests`: the look on a synthetic rig; cancel, attending to the player, the clip
    hand-over, zero allocation.
- **PlayMode:** `IdleFidgetPlatePlayTests` photographs the player in St Peters at each key of each
  Look fidget. It is opt-in (`HH_IDLE_FIDGET_PLATES=1`).

## Rig export needed

Three new ANIMS entries in the character rig, baked into every skin and added to `MeshStates`:
- `idleShift`: 12 frames at 200 ms, one-shot.
- `idleScratch`: 13 frames at 200 ms, one-shot.
- `idleHips`: 16 frames at 250 ms, one-shot.

Each clip's first and last frame must equal idle frame 0, so it can swap in and out of the idle loop
with no pop. If the frame counts change, set each Def's `DurationSeconds` to frames x ms. The full
request is kept with the fidget work's hand-off notes.
