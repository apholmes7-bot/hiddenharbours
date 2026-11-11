# Nonempty test results gate

Unity's command-line runner can return success when a filter matches no tests.
Its discovery `testcasecount` can still be positive. The workflow therefore gates
**both** suite result files with `tools/ci/results.py`, even after a failed step:

```sh
python3 tools/ci/results.py artifacts/editmode-results.xml artifacts/playmode-results.xml
```

Use the same command after a local filtered run, passing that run's results file.
The Unity exit code alone remains insufficient evidence. This gate does not alter
the Unity package or intercept unrelated editor invocations.

The command returns nonzero for a missing or truncated file, a wrong XML root,
zero case records, missing or inconsistent execution totals, failed or inconclusive
cases, a run failure outside a case, or an unknown case outcome. It reads every case and compares observed counts
with the root totals. Discovery counts and a root `result="Passed"` cannot override
zero matches. Parameterized names and XML entities retain their meaning.

Skipped cases count as matches and remain labelled skipped; they are not passes or
GPU verification. A filter selecting only GPU cases can therefore report zero passed
and positive skipped. The gate diagnoses that honestly, separately from zero matches.

CI supplies both filenames explicitly, so a missing file cannot disappear through
a glob. The summary pipeline preserves Python's failing exit code through `tee`.
Setup and licensing failures still take precedence in the final environment verdict.

The nine standalone unit tests exercise the false-green root with positive discovery
count, truncated and missing XML, inconsistent totals, unknown/inconclusive outcomes,
skips, bounded diagnostics, case and cleanup failures, and a passing control. Run them without Unity:

```sh
python3 -m unittest discover -s tools/ci -p 'test_*.py' -v
```

No existing Unity test assertion or selection is changed. The reporter releases captured
output and completed nodes while parsing, rather than retaining the full XML tree.
