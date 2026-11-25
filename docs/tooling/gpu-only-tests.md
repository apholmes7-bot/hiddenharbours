# Proposal: inventory and schedule GPU-only tests

Status: proposal only. This document installs no task, changes no workflow, and accepts no new visual baseline.

CI runs the EditMode and PlayMode suites without a graphics device. A green result therefore leaves rendering cases unverified when their first action is `Assert.Ignore`. The inventory must come from actual skipped cases and their reasons, rather than a remembered skip count or a list of filenames. A partial class filename is not necessarily its NUnit class name.

## Build an inventory from CI evidence

Use the existing `gh` client and Python standard library, alongside the existing [CI workflow](../../.github/workflows/ci.yml) and [gauntlet tools](../../tools/gauntlet/pr-gauntlet.sh):

1. Select the latest completed **push to main** with complete EditMode and PlayMode results in the `test-results` artifact. Record run ID, attempt, head SHA and completion time. A failed run can supply the inventory if both suites completed; an infrastructure failure or truncated suite cannot. Falling back to an older complete run must report its age and the missing newer evidence.
2. Stream the artifact ZIP through `gh api` into memory. Open each XML member with `zipfile.ZipFile.open` and parse it with `xml.etree.ElementTree.iterparse`, clearing completed case elements. Keep only case records and counters. Do not download the expanded XML or CI logs to disk. The gauntlet's comparisons are useful precedent; its whole-artifact download is not the proposed ingestion path.
3. Enumerate **every** `<test-case result="Skipped">`, retaining the mode, `fullname`, `classname`, parameterized case name and complete `<reason><message>`. If the case carries no reason, retain its enclosing skipped fixture's reason too. Decode XML entities through the parser before comparing names. Never count `<test-suite>` entries as cases.
4. Classify graphics-device reasons, including the existing wording `no graphics device`, `Null Device`, `requires a GPU slot` and `needs a graphics device`. Check ambiguous reasons against the first graphics-device guard in the corresponding test source. Missing assets, scene premises and other skips stay separately visible. An unclassified skip requires review; it must not silently disappear into the GPU category.
5. Write a sorted manifest under ignored `Evidence~/gpu-only/`: source run/attempt/SHA, mode, class, full case name, reason and guard location. Include all other skips in a companion inventory. A daily name-set comparison reports additions, retirements, reason changes and cases that have changed classification. A source search for `GraphicsDeviceType.Null` is a cross-check for undiscovered guards, not evidence that a case was skipped.

This lists the GPU-skipped cases actually discovered in both complete suites at that SHA. A skipped fixture can hide cases before discovery, so unexplained class or population changes require investigation. Do not claim coverage of a newer SHA or an incomplete suite.

## Run the manifest through the existing editor keeper

Use the pinned editor from `ProjectSettings/ProjectVersion.txt`, the existing [slot keeper](../../tools/slot-keeper/slot.py), and an already provisioned test worktree. Keep the same checkout SHA as the manifest. If that commit or its required assets are unavailable locally, report blocked coverage and leave the checkout alone. Do not silently substitute another revision.

The proposed wrapper would perform these steps, with no new service or package dependency:

1. Queue a scheduled label and request a 30-minute lease through the machine's installed keeper and its shared configuration. A busy or refused slot produces a pending record, never a green result. Never launch Unity outside the keeper or beside another editor.
2. Before launch, set `HH_TEST_RUN_SAVE` to a file under the run's ignored evidence directory and verify the resolved destination. Retain the existing `TestRunSave` prebuild hook. Record the redirect before Play, plus the keeper's final shared-save check.
3. Touch a floor file immediately before each launch. Use `-batchmode -force-d3d11 -runTests`, the appropriate `-testPlatform`, and **fully qualified concrete class names** in `-testFilter`. Class filters avoid punctuation in parameterized NUnit names and also exercise the surrounding fixture. Do not launch the full PlayMode suite. Keep measured small groups under the remaining lease budget and include editor startup in that budget.
4. Verify the log names a real renderer. Stream the local results into per-class and per-case summaries. Every GPU case from the manifest must appear exactly once with a passed or failed result. A missing case, zero-case run, duplicate case or GPU skip on this machine is unverified coverage. Account separately for the additional non-GPU cases selected by a class filter, and compare their populations too.
5. Collect only plates named by this launch and newer than its floor. Copy them into its evidence directory with captions, results and provenance; remove only those identified shared spills. Never reuse an older plate as proof, change expected pixels, or re-baseline a failure. A failure in the picture goes to the art lane; a fixture defect goes to QA.
6. Finish the current bounded launch before the lease ends, close the editor, verify its PID is gone, and return the lease. Save counts, failure messages and pending cases even if the run cannot finish. A timeout is a failed execution, not a skip or a pass.

## Proposed schedule and coverage report

One Windows Task Scheduler task, at 02:00 machine-local time each day, would build the CI inventory and request one 30-minute keeper lease. This uses the machine's existing scheduler, Python, `gh`, Unity and the repository's keeper; it needs no hosted GPU runner, additional Unity version or new dependency. Registering that task and writing the wrapper are separate implementation work.

Run newly discovered cases and unresolved failures first, then the oldest unverified classes. Carry unrun classes forward explicitly. Keep historical timings so a class starts only when its measured duration plus editor startup fits the lease. A class that cannot fit needs a separately scheduled bounded selection; never weaken its assertions to meet the budget.

The daily report should contain:

- Manifest SHA and CI run/attempt, inventory age and graphics-skipped case count by mode.
- Each required case's latest GPU SHA, timestamp, result, class count and plate provenance.
- Newly skipped, missing, duplicated, failed, blocked and still-pending cases by full name.
- Lease time, launch count, renderer, editor PIDs closed and the shared-save verdict.
- Cases without GPU proof in the last seven days, including the runtime capacity needed to clear that backlog.

The weekly target is complete GPU coverage of the manifest. If seven nightly leases cannot cover it, report the capacity shortfall and overdue names. A green CI run alone never satisfies this target. Retiring a case requires naming the retired production rule or its replacement, as in the existing gauntlet discipline.

## First acceptance check for the implementation

Demonstrate the inventory against one complete CI run, then exercise a small filtered GPU group and show exact name-set accounting. Also demonstrate that a wrong filter, an old plate, an expired artifact, an unknown skip reason and a missing graphics device each produce an explicit unverified result. The first run should include the terrain, plant, figure, cliff, Cumberland, golf and surf debt cases; the inventory must still discover cases outside that seed list.
