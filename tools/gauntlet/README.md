# PR gauntlet

The coordinator seat's intake battery. Every PR runs the same ordered checks before
`gh pr merge N --squash`; each prints `PASS` / `WARN` / `FAIL` with its evidence and the script exits
non-zero on any `FAIL`. A `WARN` is something the seat must **read**, not something the script decides.
The checks encode the merge laws recorded in the coordinator's memory and handoffs; when a law
changes, change the check.

Needs `gh` (authenticated as the bot), `python3`, and the usual coreutils. No Unity, no `jq`.

## Use

```bash
# once per stamp: pull a green MAIN run down as the base (refuses non-main, red, or pending runs)
tools/gauntlet/stamp-base.sh 34232815910 "$SCRATCH/base"

# per PR
tools/gauntlet/pr-gauntlet.sh 799 --base "$SCRATCH/base" --work "$SCRATCH/w799"
```

`stamp-base.sh` writes `STAMP` (sha, run id, createdAt, totals) beside the XML, and `pr-gauntlet.sh`
reads it for `--base-sha` and for the age check. `--work` keeps the PR's artifact and the per-class
tables; `record-<pr>.md` in it is the merge comment. Re-run with `--skip-download` to iterate without
pulling 250 MB again; `--merged-ok` allows a post-mortem on a merged PR; `--ack-removed` accepts
removed test names **after** you have read each one in the diff.

## The battery

| # | Check | Law it encodes |
|---|---|---|
| G1 | open · base is `main` · `mergeable` | a stacked PR does not re-target itself; a CONFLICTING PR fires no `pull_request` event |
| G2 | a `pull_request` run exists on the **exact head sha**, completed, success; both jobs pass | zero runs on a sha = check `mergeable`, not CI; `gh pr view` CLEAN is not "its tests pass" |
| G3 | the run's own results: total > 0, 0 failed, skip count unchanged | `total=0` is a FALSE GREEN; a new skip can hide a test |
| G4 | per-class count delta vs the stamped base; only the PR's **own** classes (declared in its test files, read at the head sha) may move | authenticate every PR by class; PR CI runs the MERGE ref (main drift shows positive), a run older than the stamp shows later merges negative (age, not damage) |
| G5 | test **name** sets inside every moved class | a renamed test reads as a deletion in the class diff |
| G6 | conflict markers in added lines · Library/Temp/csproj/sln · boat Def assets touched · ProjectSettings · binaries not under LFS · metas without assets | `resolver && git add && rebase --continue` commits markers; Unity runs rewrite boat assets; LFS discipline |
| G7 | scene / builder / prefab touched → export packages regenerated in the same PR (CI's scene-export `--check` is the arbiter) | a PR that touches a SCENE or BUILDER re-runs the exporter; St Peters cannot be rebuilt |
| G8 | lint of **added** lines: `GetInstanceID` (FAIL, obsolete-as-error on 6.5); in tests `Clear<`, `Register`, `LoadSceneMode.Single`, `StartNewGame`, `AddComponent` of a `[RequireComponent]`-added type, hard-coded clocks, savegame writes; in code unseeded randomness, float literals, cross-module `using`s | the fixture laws: a fixture consumes registration and never performs it; a duplicate component makes every negative assertion vacuous; rules 4, 5 and 6 |

## What it does not do

It does not open the PNGs, read the diff for you, or make a ruling. A plate is still opened by eye;
a `WARN` is still read; an owner call is still the owner's. It is the checklist made mechanical so
that the seat's attention goes to the parts that need a reader.

## Fixture runs (2026-09-09, base = main `c13d6506`)

- `#798 --merged-ok`: PASS, zero class delta, and G8 warned on the one real thing in it, an
  `AddComponent<BoatController>()` in a test.
- `#797 --merged-ok --skip-download`: its run predates the stamp, so #798's eight tests and #796's two
  show as negative non-own deltas, tagged "later merge (run predates stamp)" and reported as WARN.
