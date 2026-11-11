# CI flake ledger — 2026-11-10

## Verdict

**Zero observed same-commit pass/fail flakes** in the frozen 30-completed-run cohort. There are 48 failed case observations, but none has a passing observation with the same head commit, platform and parsed full name in this cohort. Therefore there is no eligible top-five harness-fix list. Changing tests merely because they failed on a different commit would misclassify regressions and premise changes as flakes.

## Comparison rule and coverage limits

The exact cohort and result counts are in [the timing audit](ci-timing-audit.md#coverage). Runs are ordered by creation time, matching the Actions API order, after restricting to completed runs; an older run still active at snapshot time is not silently counted as completed. Every attempt and every available `test-results` artifact of these runs was inspected. The additional requested stall run 42757755999 is outside the completed cohort and is reported separately.

Outcomes are grouped by `(head_sha, platform, XML-decoded fullname)`. Passed plus Failed is required; skipped, cancelled, missing results, and an environment failure do not count as a failing case. A rerun of one run uses the frozen merge ref; comparing different run ids with an equal head would also require confirming the checkout merge commit before calling it a proven flake. There were no such different-run equal-head pairs here.

Run 42749360538 has two attempts: EditMode is available and has zero failures in both; the original PlayMode stalled without a results XML, while attempt 2 passed. That is an editor stall followed by a successful rerun, not a named test observed both passing and failing. Run 42765404391 attempt 1 failed before any test step ran and produced no artifact; attempt 2 passed. That is an environment failure, not a case flake.

Cancelled runs without artifacts provide no case evidence. No pass or fail is imputed to an unobserved test. This is an observational ledger, not a claim that the suite is flake-free or that the missing attempts passed.

## Failed observations retained for follow-up

| Run | Commit | Platform | Failed case records |
|---|---|---|---:|
| 42506975036 attempt 1 | `0757b18b3a41738e8ef2e1d2a805a39243508d9e` | EditMode | 4 |
| 42506975036 attempt 1 | `0757b18b3a41738e8ef2e1d2a805a39243508d9e` | PlayMode | 8 |
| 42528443985 attempt 1 | `73b2132aa93e54ac08f4ffc48a4137103aafc859` | EditMode | 4 |
| 42752647205 attempt 1 | `773292ca3f2dd98999a8f83a49876b8612aa1ce2` | EditMode | 2 |
| 42756907671 attempt 1 | `e31f3095ad02c272e9fe0efd004cfa4ba06f7d12` | EditMode | 21 |
| 42756907671 attempt 1 | `e31f3095ad02c272e9fe0efd004cfa4ba06f7d12` | PlayMode | 9 |

No existing assertion, tolerance, timing budget, case name or skip condition was changed for this ledger. The historical villager and bare-skipper fixture fixes already exist on pinned main; they are not duplicated or counted as new fixes.

## Repeat the audit

Freeze the run ids before collecting. Inspect all attempts, map each artifact creation time to its attempt job window, and stream every XML `test-case`. Preserve parameterized full names and decoded XML entities. Compare both name sets and outcomes on an identical checkout before editing a harness. Rank only proven same-commit pairs by recurrence, then retain the same assertions when fixing their harness causes. Missing or overwritten artifacts must remain visible as coverage gaps.
