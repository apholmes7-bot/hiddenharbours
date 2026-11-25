# Proposal: split the Unity CI critical path

Status: proposal for lead-architect and qa-test. No workflow is changed here.
Baseline: `3e7188a426c00dde0b18f94ab9512ea1a2bc0c21`.

## Evidence and recommendation

Start with two isolated jobs, one complete EditMode suite and one complete PlayMode
suite. This removes their serial dependency without changing test membership. Consider
three jobs only after a two-runner license/concurrency trial and an exact coverage
comparison pass. The third job splits PlayMode at whole fixture-class boundaries;
never split a fixture's setup, cases or cleanup among editors.

The model below uses successful pinned-main Actions run **42743400071**, attempt 1,
on 2026-11-10. It is an estimate from one run, not a measured parallel speedup.
The preserved timing audit associated with draft PR 1065 contains the wider cohort.
That cohort predates this proposal; it is not a fresh sample of all current runs.

| Configuration | Jobs | Estimated critical path, minutes | Estimated runner minutes |
|---|---:|---:|---:|
| Current serial job, observed | 1 | 108.02 | 108.02 |
| Whole suites in parallel, model | 2 | 78.70 | 112.92 |
| EditMode plus two PlayMode shards, model | 3 | 42.77 | 119.76 |

The observed EditMode step was 29.32 minutes, PlayMode was
73.80, and all other job work was 4.90. For the two-job
model each job pays that full overhead: `max(EditMode, PlayMode) + overhead`.
For three jobs, PlayMode's fixture durations sum to 71.86
minutes. The remaining 1.94 minutes are conservatively charged to
**each** PlayMode shard, then the full job overhead is charged to every job.
These costs cover observed setup/reporting, not an upper bound on cold imports,
runner contention, license waits, or cache misses. Parallel jobs use more runner
minutes and can increase simultaneous memory and network demand.

## Two-job implementation contract

Use a matrix with explicit `EditMode` and `PlayMode` values, `fail-fast: false` and
`max-parallel: 2`. Each job owns its checkout, Library, artifacts and editor process.
Both suites must run even when the other fails; do not put PlayMode behind
`needs: editmode`. Keep the existing workflow concurrency group and cancellation
policy, pinned action revisions, Hub/editor version, setup timeout and 150-minute
job ceiling during the trial. Measure before lowering a timeout.

Copy the existing checkout, LFS restore, editor restore, setup and activation steps
into the matrix job without changing their pins or authentication. Preserve the
existing license action's cleanup behavior on failures and cancellation. Each job
starts its own editor through the existing action. Keep the current test assertions,
scene loads, real-time route waits, graphics skips and platform arguments intact.

Use unique artifact names (`test-results-editmode`, `test-results-playmode`) and
mode-specific result/log filenames. Always upload results and editor/import-worker
diagnostics, including on failure. Apply the streaming reporter and empty-suite
guard from PR 1065 if accepted; on pinned main they are not present yet. Missing,
malformed or zero-executed results must fail the future job, never read as green.

An `always()` aggregate check, named **EditMode + PlayMode tests** to preserve the
existing reviewer signal, requires all matrix jobs to succeed and both expected
reports to exist with nonzero executed counts. It must reject failure, cancellation,
skipped jobs and missing reports. Check the actual existing check names and any
repository rules before introducing this job; this document changes no rules.

## Three-job variant and proposed manifest

Keep EditMode whole. The attached [PlayMode class manifest](ci-playmode-shards-proposal.csv)
assigns all 226 recorded PlayMode classes exactly once, using descending
recorded fixture duration, then ordinal full class name, assigning each to the
currently lighter shard (ties go to play-1). Parameterized fixture variants sharing
a class stay together. It proposes:

| Shard | Whole classes | Recorded fixture minutes |
|---|---:|---:|
| play-1 | 113 | 35.928 |
| play-2 | 113 | 35.928 |

The manifest is a frozen proposal, not an executable test filter or a source of truth
for discovery. A future workflow PR must discover tests from the checkout being run,
including all test assemblies, before partitioning. Unknown classes must cause a
clear manifest-update failure or be included by a reviewed deterministic fallback;
they must never disappear because they are absent from this snapshot. Remove stale
entries only after discovery proves those fixtures were actually retired.

Implement assembly-qualified, exact full-class membership using the pinned Test
Framework's discovered test tree or a verified anchored filter. Do not pass short
class fragments: `-testFilter` substring matches can run unintended fixtures, and a
wrong filter can run zero tests successfully. Verify the pinned runner's filter
semantics in the implementation PR rather than assuming a join character is safe.
Keep inherited and parameterized cases, explicit/ignored states and assembly
identity. No assembly-definition or product-code changes are needed for this design.

## Coverage and isolation gates

1. On an identical checkout and runner environment, capture an unsharded discovery
   inventory and results, then each proposed shard's inventory and results. Stream
   XML and discard output nodes; never materialize a full results tree or archive
   complete logs for this comparison.
2. Compare multisets of `(assembly, platform, decoded full test name)` for **all**
   leaves, including skipped cases. Shard union must equal the baseline multiset;
   shard intersections must be empty. Test counts alone are insufficient. The
   observed reference had 15,936 EditMode cases (15,644 passed, 292 skipped) and
   1,291 PlayMode cases (1,065 passed, 226 skipped). These are reference observations,
   not permanent expected counts for a later checkout.
3. Compare outcomes by identity. Investigate changed skips, order dependence and
   failures; never loosen a rule, delete an assertion or introduce a skip to fit
   the new schedule. Each shard needs a nonzero executed count and proof its
   requested classes ran. GPU cases skipped in CI remain owed to an editor lease.
4. Keep each job's temporary save and plate output isolated. Redirect any real-play
   harness at launch and verify the destination before entering Play. Never copy a
   live save into shared cache. Do not run two editors in one workspace or on the
   local machine to trial this proposal.
5. Run the old serial schedule and proposed schedule against the same head. Trial
   at least three complete paired runs, covering a cold import and warm restores.
   Report queue time, setup, import, per-shard wall time, total runner minutes,
   failures, skips and any missing coverage. Two cloud runners must prove licensing
   and cleanup before the design is enabled for all PRs; three need another trial.

## Cache ownership, failure reporting and rollout

Keep PR caches restore-only. Do not let parallel jobs race to save the same immutable
Library or LFS key. For a main push, nominate **one** matrix entry as cache writer;
save its Library/LFS only when the aggregate test result is green. This needs an
explicit implementation: for example a separate final writer job retaining the
designated job's snapshot, with transfer/storage cost measured. Alternatively leave
Library/LFS saving in the existing serial main job during the PR-only trial. Never
add an unpriced Library upload merely to claim the split is cheap. Keep editor-cache
saving single-writer as well, retaining its current ability to save after setup.

The current Red X verdict must report per shard: environment/setup/license failure
versus tests that ran and failed. Aggregate failures must link the responsible job
and result summary. A native import stall without case records remains an environment
diagnostic gap; splitting jobs is not a demonstrated fix for the stalls on PRs 1034
and 1040. Case-start journals and a watchdog are separately proposed in the backlog.

Land no workflow change as part of this document. A separately reviewed **draft**
workflow PR should start with the two-job PR-only trial while main remains serial,
carry exact coverage proof and licensing evidence, and state the measured cost.
Lead-architect decides whether to enable it; qa-test verifies the union of cases.
Keep rollback to the existing serial workflow a one-commit revert, retaining every
test and reporter guard. Only then evaluate the three-job variant. No new assertions,
thresholds, accepted plates, runtime assets, tunables or save formats are proposed.
