import contextlib
import io
from pathlib import Path
import tempfile
import unittest

import results


def xml(cases, **attributes):
    attrs = dict(total=len(cases), passed=sum('result="Passed"' in c for c in cases),
                 failed=sum('result="Failed"' in c for c in cases),
                 skipped=sum('result="Skipped"' in c for c in cases),
                 inconclusive=sum('result="Inconclusive"' in c for c in cases))
    attrs.update(attributes)
    text = " ".join(f'{k}="{v}"' for k, v in attrs.items())
    return io.BytesIO((f'<test-run {text}><test-suite testcasecount="900">'
                       + "".join(cases) + '</test-suite></test-run>').encode())


class ResultsTests(unittest.TestCase):
    def test_zero_filter_cannot_use_discovered_count_or_passed_root(self):
        summary = results.read_results(xml([], testcasecount=900, result="Passed"))
        self.assertEqual(summary.total, 0)
        self.assertIn("ZERO TESTS MATCHED", " ".join(summary.problems()))

    def test_counts_names_and_failure_diagnostics_survive_output_discard(self):
        summary = results.read_results(xml([
            '<test-case fullname="A&amp;B&quot;C" result="Passed"><output>noise</output></test-case>',
            '<test-case fullname="Broken" result="Failed"><failure><message>actual &lt; expected</message>'
            '<stack-trace>a\nb\nc\nd\ne</stack-trace></failure><output>more noise</output></test-case>',
            '<test-case fullname="Gpu" result="Skipped"><reason><message>needs GPU</message></reason></test-case>',
        ]))
        self.assertEqual(summary.total, 3)
        self.assertEqual(dict(summary.counts), {"Passed": 1, "Failed": 1, "Skipped": 1})
        self.assertEqual(summary.failures, [("Broken", [("message", "actual < expected"),
                                                       ("stack-trace", "a\nb\nc\nd")])])
        self.assertEqual(summary.problems(), ["tests failed or were inconclusive"])

    def test_all_skipped_matches_are_reported_as_skipped(self):
        summary = results.read_results(xml(['<test-case result="Skipped"/>']))
        self.assertEqual(summary.problems(), [])
        self.assertEqual(summary.counts["Passed"], 0)

    def test_run_failure_outside_a_case_cannot_read_green(self):
        summary = results.read_results(xml(['<test-case result="Passed"/>'], result="Failed"))
        self.assertEqual(summary.counts["Failed"], 0)
        self.assertEqual(summary.problems(), ["test run failed outside a case (setup or cleanup)"])

    def test_total_and_outcome_mismatches_are_rejected(self):
        for attrs in ({"total": 2}, {"passed": 0}, {"failed": "invalid"}):
            with self.subTest(attrs=attrs):
                self.assertTrue(results.read_results(xml(['<test-case result="Passed"/>'], **attrs)).problems())

    def test_inconclusive_and_unknown_outcomes_fail(self):
        for outcome in ("Inconclusive", "Unexpected"):
            with self.subTest(outcome=outcome):
                self.assertTrue(results.read_results(xml([f'<test-case result="{outcome}"/>'])).problems())

    def test_diagnostics_are_bounded_but_every_failure_is_counted(self):
        case = '<test-case fullname="Broken" result="Failed"><failure><message>' + 'x' * 1000 + '</message></failure></test-case>'
        summary = results.read_results(xml([case] * 45))
        self.assertEqual(summary.counts["Failed"], 45)
        self.assertEqual(len(summary.failures), 40)
        self.assertEqual(summary.failures[0][1][0][1], 'x' * 800 + ' …[truncated]')

    def test_missing_truncated_empty_and_failing_files_exit_nonzero(self):
        with tempfile.TemporaryDirectory() as folder:
            path = Path(folder) / "results.xml"
            for payload in (None, b'<test-run>', b'<wrong-root/>', xml([]).getvalue(),
                            xml(['<test-case result="Failed"/>']).getvalue()):
                with self.subTest(payload=payload), contextlib.redirect_stdout(io.StringIO()):
                    if payload is not None:
                        path.write_bytes(payload)
                    self.assertEqual(results.main([str(path)]), 1)

    def test_valid_file_exits_zero(self):
        with tempfile.TemporaryDirectory() as folder:
            path = Path(folder) / "results.xml"
            path.write_bytes(xml(['<test-case result="Passed"/>']).getvalue())
            with contextlib.redirect_stdout(io.StringIO()):
                self.assertEqual(results.main([str(path)]), 0)


if __name__ == "__main__":
    unittest.main()
