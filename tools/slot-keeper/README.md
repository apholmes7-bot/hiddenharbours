# Editor slot keeper

The machine runs one Unity editor at a time. `slot.py` hands that one slot to the lanes in queue
order, under the slot rules the owner used to enforce by hand (R1-R9 below). The owner keeps the
queue order, a pause switch, an emergency stop and every merge. For lanes and the owner, the short
version is `docs/tooling/editor-slot-keeper.md`.

- **Stdlib only** (Python 3.14), **Windows only**: headroom comes from kernel32 and processes from CIM.
- **No path lives in this folder.** The repo is public, so every path is in the live `slot.json`,
  which sits in the live home, outside every box and outside git. `slot.example.json` holds
  placeholders only; `slot.py` refuses a path that still starts with `<`.
- The config is `--config PATH`, else `HH_SLOT_CONFIG`, else `slot.json` beside `slot.py`.

## Commands

Every command prints **one summary line first**; `--json` adds the full state after it.
Exit codes: **0** done or GRANTED, **1** an error, **2** REFUSED, **3** QUEUED, STILL QUEUED or still RUNNING.

| Command | Who | What it does |
|---------|-----|--------------|
| `status [--json]` | anyone | Holder, lease age and budget left, the running launch, the queue, paused or not, headroom, disk, `unity.exe` counts by class, the last three returns. Read-only when the state folder is missing. |
| `queue add --label L --box PATH --minutes N --by seat\|owner` | seat, owner | Adds an entry. The box must exist and must not be the owner's project or under it. N is at most `max_lease_minutes`. |
| `queue remove L` · `queue move L POS` · `queue list` | owner | Reorders or drops entries. |
| `pause [--reason TEXT]` · `resume` | owner | While paused nothing new is granted; a running lease goes on to its end. |
| `stop` | owner | Ends the current lease now: closes its editor by PID tree, waits for the watchdog, writes the return. |
| `take --label L` | lane | Answers at once: `GRANTED <lease-id>`, `QUEUED (position n of m): why`, or `REFUSED: why`. |
| `wait --label L --max-minutes M` | lane | `take` every `wait_check_seconds` until granted or M runs out (`STILL QUEUED ...`). |
| `launch --lease ID [--max-minutes 30] -- <unity args>` | lane | R2 and R3, then a detached watchdog starts the editor and enforces R4-R6. Prints the outcome line, or `RUNNING ...` when M runs out first. |
| `wait-run --lease ID [--max-minutes M]` | lane | Waits again on a launch the watchdog still has. |
| `keep --lease ID` | lane | Heartbeat between launches: resets the idle clock. |
| `give --lease ID [--stop]` | lane | Ends the lease and writes the return. Refused while a launch runs, unless `--stop`. |

An outcome line looks like
`EXITED 0 | 0.4 min | lowest headroom 37.6 GiB at 18:13:46Z | save restored (4D3B11D5243A 8193 B seed 12345) | launch <lease-id> #1`
or `STOPPED: BUDGET (exit 1) | ...`.

**The grant rule.** When the slot is free, nothing is paused and no editor runs, the slot goes to
the highest queue entry among the lanes *waiting now*: those that called `take` or `wait` within
`wait_heartbeat_seconds`. An entry whose lane is not waiting never blocks the entries below it.
A label that is not in the queue is refused.

**A lease ends** on `give`; when budget plus grace runs out; after `idle_minutes` with no launch
running and no `keep`; and on `stop`. While a run is live the watchdog enforces these; otherwise
any keeper call does (a waiting lane's `wait` expires an idle lease). `launch` is refused once the
budget has ended: grace only finishes a run already going.

## The rules

| Rule | What the keeper does |
|------|----------------------|
| **R1** one editor | The slot is busy while any editor runs, whether the keeper launched it or not. Workers, bridges and the Hub do not count. A foreign editor blocks every grant and every launch. |
| **R2** before a launch | No editor running; headroom at least `launch_headroom_gib`; `disk_path` at least `min_disk_gb` free; the box exists and is not the owner's project or under it (case-insensitive, either slash, links resolved). |
| **R3** the command line | Refuses `-nographics`, other `-force-<gfx>` flags, a `-projectPath` that is not the box, two `-projectPath`s, and a second launch while one runs. Adds `-projectPath <box>` when missing and `-force-d3d11` when missing. |
| **R4** the watchdog | Samples every `sample_seconds`. Headroom under `stop_headroom_gib`: closes the editor (`stopped: headroom`). Budget plus grace over: the same (`stopped: budget`). Records the lowest headroom and when. |
| **R5** the save | Before each launch: a copy in `saves/`, checked by sha256. After the whole tree exits: if the save changed, put the copy back and check it. Not restored (the queue pauses, the seat decides) when a foreign editor was seen, when the launch went unwatched, or when the copy no longer checks. |
| **R6** after the exit | Every editor or import worker naming the box that is in our tree is closed by PID tree and recorded. One not in our tree is recorded (`foreign_naming_box`), never closed: R7 wins. |
| **R7** what may close | Only `taskkill /PID n /T /F`, only on the editor the keeper launched and the descendants it saw, each checked by PID **and** creation time. Never a bridge, the Hub, a foreign editor, or anything by name. |
| **R8** never write in a box | No git writes. After each launch it reads `git status --porcelain` (with `GIT_NO_LAZY_FETCH=1`, `GIT_OPTIONAL_LOCKS=0`) into the record. |
| **R9** delete nothing | The keeper deletes nothing outside its own temp files in the state folder; save copies and returns are kept. |

The editor's own environment gets `ALLUSERSPROFILE=<allusersprofile>` when the shell lacks it
(Package Manager fails at startup without it).

## How a launch runs

1. `launch` checks R2 and R3 under the lock, copies the save, records the launch, then starts the
   watchdog through a short `_spawn` hop (detached, its own process group, out of the caller's
   job), so the watchdog's parent is gone and it outlives `launch` and the lane's tool call.
2. The watchdog starts the editor, records its PID and creation time, and samples: headroom, the
   editor's descendants (recorded as they appear), foreign editors, the lease's stop and budget.
3. When the editor exits (or is closed), the watchdog waits for the whole tree, runs the R6 sweep,
   checks the save (R5), reads the porcelain (R8) and commits the launch. A lease past its budget
   or stopped ends there and writes its return.
4. `launch`, `wait-run`, `status` and every other call only read what the watchdog wrote.

**Locking.** Every state change happens under one lock file made by exclusive create, holding the
PID, its creation time and what took it. A lock whose holder is dead is stale: it is renamed aside,
checked and removed, and the break goes in `log.jsonl`. State files are written to a temp file and
`os.replace`d.

**Recovery.** Every call first reconciles state with the process list. A launch whose watchdog and
editor are both dead is closed as `watchdog lost`. An editor alive with no watchdog is adopted by a
new watchdog, or closed if its lease is past budget plus grace or stopped. Either way the launch is
marked `unwatched_gap`, and a save that changed in it is not restored (R5).

## State (the live home)

| File | What |
|------|------|
| `slot.json` | The config (from `slot.example.json`). |
| `queue.json` | `{"entries": [{label, box, minutes, by, added, seen}]}` in grant order. |
| `lease.json` | `{"lease": null}` or the live lease, with its running launch. |
| `paused` | Present while paused: `{reason, by, at}`. The owner may also make or remove it by hand. |
| `log.jsonl` | Append-only: grants, launches, kills, restores, lock breaks. |
| `returns/<lease-id>.json` | The return record and its one-line `summary`. |
| `saves/<launch-id>.json` | The save copy taken before each launch (`.after.json`: a changed save left in place). |
| `runs/` | Each launch's watchdog and editor output. |

Lease ids are `YYYYMMDDTHHMMSSZ-<label>-<4 hex>`; a label is 1-48 of `A-Z a-z 0-9 . _ -`.

**The return record** carries the lease id, label, box, who added the entry, grant, end and end
reason; per launch the args, editor PID, every child PID seen (with names), watchdog PIDs, start,
end, minutes, exit code, how it ended, lowest headroom and its time, the save before and after
(sha256 prefix, size, WorldSeed), restored or not and why, foreign editors seen, unwatched gap,
porcelain, leftovers closed, kills; and at the end every launched PID confirmed gone and the disk's
free space. Its summary line:

```text
RETURN <lease-id> | <label> on <box>, added by seat | 18:13:36Z to 18:15:06Z, give | #1 exited 0, 0.4 min, low 37.6 GiB, save restored (4D3B11D5243A 8193 B seed 12345) | launched PIDs gone: yes | 184.8 GB free
```

## Tests

```bash
python -m unittest discover -s tools/slot-keeper -p "test_slot.py" -v
```

31 cases against a fake machine (clock, memory, disk, processes, kills); nothing real is started
or closed. Each case gets its own state folder under the box's `Temp/slot-tests/` (git-ignored,
never deleted). The fake clock moves only through `sleep` and fails a test that runs a whole fake
day, so a broken stop rule shows up red instead of hanging.

## Tuning

The owner tunes the numbers in the live `slot.json`: `launch_headroom_gib` 16, `stop_headroom_gib`
4, `min_disk_gb` 15, `sample_seconds` 5, `grace_minutes` 15, `idle_minutes` 30,
`wait_heartbeat_seconds` 120, `max_lease_minutes` 240, `wait_check_seconds` 15. `editor_rule`
tells editors, import workers, bridges and the Hub apart; a trial points it at a stand-in.
