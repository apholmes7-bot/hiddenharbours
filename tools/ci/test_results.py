import contextlib
import io
import json
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


def assembly_case(assembly, classname="Project.Tests", outcome="Passed"):
    return (f'<test-suite type="Assembly" name="{assembly}.dll">'
            f'<test-suite type="TestFixture"><test-case classname="{classname}" result="{outcome}"/>'
            '</test-suite></test-suite>')


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


class ProjectCoverageTests(unittest.TestCase):
    def setUp(self):
        self.coverage = {"minimum_project_cases": 1405, "required_classes": {
            "HiddenHarbours.Tests.CoopSpike.CoopSpikeDevBootTests": 2,
            "HiddenHarbours.Tests.CoopSpike.CoopSpikeNetworkTests": 10,
        }}
        self.cases = [assembly_case("HiddenHarbours.Tests.PlayMode") for _ in range(1393)]
        for name, count in self.coverage["required_classes"].items():
            self.cases.extend([assembly_case("HiddenHarbours.Tests.CoopSpike.PlayMode", name)] * count)

    def problems(self, cases):
        return results.read_results(xml(cases)).coverage_problems(self.coverage)

    def test_main_plus_all_coop_cases_pass_the_boundary(self):
        self.assertEqual(self.problems(self.cases), [])

    def test_one_missing_project_case_fails_even_with_all_coop_classes(self):
        self.assertIn("project cases 1404 below required 1405", self.problems(self.cases[1:]))

    def test_package_cases_cannot_fill_the_project_floor(self):
        cases = self.cases[1:] + [assembly_case("Unity.Netcode.Runtime.Tests")]
        self.assertIn("project cases 1404 below required 1405", self.problems(cases))

    def test_every_netcode_assembly_is_rejected_including_skipped_cases(self):
        for assembly in ("Unity.Netcode.Editor.Tests", "Unity.Netcode.Runtime.Tests", "Unity.Netcode.Future.Tests"):
            for outcome in ("Passed", "Skipped"):
                with self.subTest(assembly=assembly, outcome=outcome):
                    problems = self.problems(self.cases + [assembly_case(assembly, outcome=outcome)])
                    self.assertEqual(problems, [f"excluded package assembly {assembly} contains 1 cases"])

    def test_no_missing_class_can_hide_behind_a_large_total(self):
        for name in self.coverage["required_classes"]:
            cases = [c.replace(name, "Other.Class") for c in self.cases]
            with self.subTest(name=name):
                self.assertTrue(any(f"{name}: 0 executed" in p for p in self.problems(cases)))

    def test_skipped_required_cases_do_not_count_as_execution(self):
        cases = self.cases[:-1] + [self.cases[-1].replace('result="Passed"', 'result="Skipped"')]
        self.assertTrue(any("9 executed cases below required 10" in p for p in self.problems(cases)))

    def test_class_identity_comes_from_project_assembly_not_case_namespace(self):
        cases = [c.replace('name="HiddenHarbours.Tests.CoopSpike.PlayMode.dll"',
                           'name="ThirdParty.Tests.dll"') for c in self.cases]
        problems = self.problems(cases)
        self.assertIn("project cases 1393 below required 1405", problems)
        self.assertEqual(sum("0 executed cases" in p for p in problems), 2)

    def test_missing_assembly_metadata_cannot_claim_project_coverage(self):
        cases = [c.replace('type="Assembly"', 'type="TestFixture"') for c in self.cases]
        self.assertIn("project cases 0 below required 1405", self.problems(cases))

    def test_global_setup_fixture_keeps_its_assembly_identity(self):
        cases = [c.replace('type="Assembly"', 'type="SetUpFixture" fullname="GlobalSetup"')
                 for c in self.cases]
        self.assertEqual(self.problems(cases), [])
        cases.append(assembly_case("Unity.Netcode.Runtime.Tests").replace(
            'type="Assembly"', 'type="SetUpFixture" fullname="PackageSetup"'))
        self.assertEqual(self.problems(cases), ["excluded package assembly Unity.Netcode.Runtime.Tests contains 1 cases"])

    def test_inherited_case_counts_towards_the_executing_fixture(self):
        cls = "HiddenHarbours.Tests.CoopSpike.CoopSpikeNetworkTests"
        inherited = self.cases[-1].replace('<test-suite type="TestFixture">',
                    f'<test-suite type="TestFixture" classname="{cls}" fullname="{cls}">')
        inherited = inherited.replace(f'<test-case classname="{cls}"',
                    '<test-case classname="HiddenHarbours.Tests.CoopSpike.CoopNetworkTest"')
        self.assertEqual(self.problems(self.cases[:-1] + [inherited]), [])

    def test_skipped_ordinary_cases_remain_in_the_project_total(self):
        cases = [c.replace('result="Passed"', 'result="Skipped"') for c in self.cases[:1393]] + self.cases[1393:]
        summary = results.read_results(xml(cases))
        self.assertEqual(summary.counts["Skipped"], 1393)
        self.assertEqual(summary.coverage_problems(self.coverage), [])

    def test_cli_enforces_coverage_and_requires_every_configured_file(self):
        with tempfile.TemporaryDirectory() as folder:
            root = Path(folder)
            path = root / "playmode-results.xml"
            contract = root / "coverage.json"
            contract.write_text(json.dumps({path.name: self.coverage}), encoding="utf-8")
            args = ["--project-coverage", str(contract), str(path)]
            for cases, expected in ((self.cases, 0), (self.cases[1:], 1)):
                path.write_bytes(xml(cases).getvalue())
                with contextlib.redirect_stdout(io.StringIO()):
                    self.assertEqual(results.main(args), expected)
            contract.write_text(json.dumps({path.name: self.coverage, "editmode-results.xml": self.coverage}), encoding="utf-8")
            with contextlib.redirect_stdout(io.StringIO()):
                self.assertEqual(results.main(args), 1)


if __name__ == "__main__":
    unittest.main()
