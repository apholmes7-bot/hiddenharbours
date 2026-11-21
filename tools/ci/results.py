"""Stream NUnit results without retaining captured Unity output in memory.

The command is also a gate: missing, truncated, inconsistent, empty or failing
results return nonzero. A skipped case is a match, but is never called a pass.
"""

import argparse
from collections import Counter
from dataclasses import dataclass, field
import json
from pathlib import Path
import sys
import xml.etree.ElementTree as ET


MAX_FAILURES = 40
MAX_MESSAGE_CHARS = 800


@dataclass
class Summary:
    attributes: dict = field(default_factory=dict)
    counts: Counter = field(default_factory=Counter)
    failures: list = field(default_factory=list)
    assemblies: dict = field(default_factory=dict)
    project_classes: dict = field(default_factory=dict)

    @property
    def total(self):
        return sum(self.counts.values())

    def problems(self):
        errors = []
        if not self.attributes:
            errors.append("missing test-run root")
        if self.total == 0:
            errors.append("ZERO TESTS MATCHED: check the test filter and assembly names")
        for name, observed in (("total", self.total), ("passed", self.counts["Passed"]),
                               ("failed", self.counts["Failed"]), ("skipped", self.counts["Skipped"]),
                               ("inconclusive", self.counts["Inconclusive"])):
            value = self.attributes.get(name)
            # testcasecount can describe discovery before filtering; it is not execution evidence.
            if value is None and name != "total":
                continue
            try:
                if int(value) != observed:
                    errors.append(f"{name}={value} disagrees with {observed} case records")
            except (TypeError, ValueError):
                errors.append(f"missing or invalid {name}")
        unknown = self.counts.keys() - {"Passed", "Failed", "Skipped", "Inconclusive"}
        if unknown:
            errors.append("unknown case outcomes: " + ", ".join(sorted(unknown)))
        if self.counts["Failed"] or self.counts["Inconclusive"]:
            errors.append("tests failed or were inconclusive")
        elif self.attributes.get("result", "").split(":")[0] in ("Failed", "Inconclusive"):
            errors.append("test run failed outside a case (setup or cleanup)")
        return errors

    def coverage_problems(self, coverage):
        errors = []
        project_total = sum(sum(counts.values()) for name, counts in self.assemblies.items()
                            if name.startswith("HiddenHarbours."))
        minimum = coverage["minimum_project_cases"]
        if project_total < minimum:
            errors.append(f"project cases {project_total} below required {minimum}")
        for name, minimum in coverage["required_classes"].items():
            counts = self.project_classes.get(name, Counter())
            executed = counts["Passed"] + counts["Failed"] + counts["Inconclusive"]
            if executed < minimum:
                errors.append(f"{name}: {executed} executed cases below required {minimum}")
        for name, counts in self.assemblies.items():
            if name.startswith("Unity.Netcode."):
                errors.append(f"excluded package assembly {name} contains {sum(counts.values())} cases")
        return errors


def read_results(stream):
    """Read a path or binary stream, releasing each case, suite and output node.

    Output is discarded at its end event, before the containing case completes.
    Memory is bounded by XML depth, the largest individual text node, assembly
    and class counters, and MAX_FAILURES diagnostics, not the captured log.
    """
    summary = Summary()
    stack = []
    for event, node in ET.iterparse(stream, events=("start", "end")):
        if event == "start":
            if not stack:
                if node.tag != "test-run":
                    raise ValueError("expected a test-run root")
                summary.attributes = dict(node.attrib)
            stack.append(node)
            continue
        if node.tag == "test-case":
            outcome = node.get("result", "UNKNOWN")
            summary.counts[outcome] += 1
            # A namespace-wide NUnit SetUpFixture can replace the Assembly node's
            # type/fullname; Unity retains its assembly filename in name.
            assembly = next((parent.get("name", "") for parent in reversed(stack)
                             if parent.tag == "test-suite" and
                             (parent.get("type") == "Assembly" or
                              (parent.get("type") == "SetUpFixture" and
                               parent.get("name", "").endswith(".dll")))), "")
            assembly = assembly.replace("\\", "/").rsplit("/", 1)[-1].removesuffix(".dll")
            summary.assemblies.setdefault(assembly, Counter())[outcome] += 1
            if assembly.startswith("HiddenHarbours."):
                # Inherited methods name their declaring base class on the case;
                # the enclosing fixture identifies the class that actually ran.
                classname = next((parent.get("classname") or parent.get("fullname", "")
                                  for parent in reversed(stack) if parent.tag == "test-suite" and
                                  parent.get("type") == "TestFixture" and
                                  (parent.get("classname") or parent.get("fullname"))),
                                 node.get("classname", ""))
                summary.project_classes.setdefault(classname, Counter())[outcome] += 1
            if outcome == "Failed" and len(summary.failures) < MAX_FAILURES:
                failure = node.find("failure")
                details = []
                if failure is not None:
                    for tag in ("message", "stack-trace"):
                        text = (failure.findtext(tag) or "").strip()
                        if tag == "stack-trace":
                            text = "\n".join(text.splitlines()[:4])
                        elif len(text) > MAX_MESSAGE_CHARS:
                            text = text[:MAX_MESSAGE_CHARS] + " …[truncated]"
                        if text:
                            details.append((tag, text))
                summary.failures.append((node.get("fullname") or node.get("name") or "?", details))
        if node.tag in ("test-case", "test-suite", "output", "system-out", "attachments"):
            node.clear()
            if len(stack) > 1:
                stack[-2].remove(node)
        stack.pop()
    return summary


def report(path, summary, coverage=None):
    print(f"\n### {path} — {summary.counts['Passed']}/{summary.total} passed, "
          f"{summary.counts['Failed']} failed, {summary.counts['Skipped']} skipped")
    for name, details in summary.failures:
        print(f"\nFAILED: {name}")
        for tag, text in details:
            print(f"  {tag}: {text}")
    omitted = summary.counts["Failed"] - len(summary.failures)
    if omitted:
        print(f"\n…and {omitted} more failures (showing the first {MAX_FAILURES}).")
    problems = summary.problems()
    if coverage is not None:
        for name, counts in sorted(summary.assemblies.items()):
            print(f"ASSEMBLY {name or '(missing)'}: {dict(counts)}")
        for name in coverage["required_classes"]:
            print(f"REQUIRED {name}: {dict(summary.project_classes.get(name, {}))}")
        problems.extend(summary.coverage_problems(coverage))
    for problem in problems:
        print(f"ERROR: {problem}")
    return bool(problems)


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--project-coverage", type=Path,
                        help="Per-results-file project count floors and required co-op classes")
    parser.add_argument("results", nargs="+", type=Path)
    args = parser.parse_args(argv)
    coverage = None
    if args.project_coverage is not None:
        try:
            coverage = json.loads(args.project_coverage.read_text(encoding="utf-8"))
            if set(coverage) != {path.name for path in args.results}:
                raise ValueError("coverage must name exactly the supplied results files")
        except (OSError, ValueError) as error:
            print(f"ERROR: could not read project coverage ({error})")
            return 1
    bad = False
    for path in args.results:
        try:
            bad |= report(path, read_results(path), coverage[path.name] if coverage is not None else None)
        except (OSError, ValueError, ET.ParseError) as error:
            print(f"ERROR: {path}: could not read complete results ({error})")
            bad = True
    return 1 if bad else 0


if __name__ == "__main__":
    sys.exit(main())
