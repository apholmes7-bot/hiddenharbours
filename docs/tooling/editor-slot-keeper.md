# The editor slot keeper: taking the one Unity slot

The machine runs **one Unity editor at a time**. Instead of the owner pasting a grant for every
run, `tools/slot-keeper/slot.py` hands the slot out in queue order and enforces the slot rules:
one editor, headroom and disk checks before a launch, a watchdog during it, the shared save checked
and restored after it, and nothing closed but the keeper's own editor, by PID tree. The owner keeps
the queue order, a pause switch, an emergency stop and every merge. Detail (rules R1-R9, state,
return record): `tools/slot-keeper/README.md`.

Below, `slot` means `python <live home>/slot.py`; the live home and its `slot.json` sit outside
every box and outside git (`--config` or `HH_SLOT_CONFIG` point elsewhere). Every command prints one
line first. Exit codes: 0 done or GRANTED, 2 REFUSED, 3 QUEUED or still RUNNING, 1 an error.

## For a lane

The seat puts your label in the queue when it writes your phase paste
(`slot queue add --label <label> --box <your box> --minutes <budget> --by seat`). Then:

1. **Take the slot.** `slot take --label <label>` answers at once. If it says `QUEUED`, call
   `slot wait --label <label> --max-minutes 9` (stay under your tool's own timeout) and call it
   again while it says `STILL QUEUED`. Calling `take` or `wait` is what marks you as waiting: an
   entry that stops calling is skipped, not waited for.
2. **Launch.** `slot launch --lease <lease-id> --max-minutes 9 -- -batchmode -runTests ... -logFile ...`
   with your args exactly as you would pass them to Unity. `-projectPath` is your box (added if you
   leave it out), `-force-d3d11` is added, `-nographics` is refused. The editor runs under a
   detached watchdog, so your tool call may end without ending the run.
3. **If it says `RUNNING`,** call `slot wait-run --lease <lease-id> --max-minutes 9` until you get
   the outcome line: exit code, minutes, lowest headroom, the save unchanged or restored.
4. **Between launches,** run `slot keep --lease <lease-id>` at least every 30 minutes, or the lease
   ends as idle.
5. **Give it back** with `slot give --lease <lease-id>` and copy its `RETURN ...` line into your
   report. It says whether every PID you launched is gone.

Your lease ends by itself when its budget plus 15 minutes of grace runs out: the watchdog closes a
run still going (`stopped: budget`). It also closes it if commit headroom falls under 4 GiB
(`stopped: headroom`). A launch after the budget is refused: grace only finishes a run already
going. Restoring `ProjectSettings.asset` and any other file in your box stays your job: the keeper
never writes in a box.

## For the owner

- **See it:** `slot status` shows the holder, the running launch, the queue, paused or not,
  headroom, free disk, the `unity.exe` processes by kind, and the last three returns.
- **Pause:** `slot pause --reason "..."` grants nothing new; a running lease goes on to its end.
  `slot resume` lifts it. Making a file named `paused` in the live home pauses too; deleting it
  resumes. The keeper pauses itself when the shared save changed and it would not restore it
  (a foreign editor ran, or a launch went unwatched): read the reason, then decide.
- **Reorder:** `slot queue list`, `slot queue move <label> <position>`, `slot queue remove <label>`.
- **Stop now:** `slot stop` ends the current lease, closes its editor by PID tree, and writes the
  return.
- **Your own editor:** open the owner's project whenever you like. The keeper never launches there
  and never touches an editor it did not start; while yours runs, it grants nothing.
- **Tune:** the numbers live in `slot.json` (headroom 16 / 4 GiB, disk 15 GB, grace 15 min,
  idle 30 min and the rest). Edit, save, and the next call uses them.
