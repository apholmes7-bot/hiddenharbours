using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>Optional native proof entry point. Writes compact case/class counts, never a full XML report.</summary>
    public sealed class SailRaceVerificationCli : ICallbacks
    {
        private static readonly Dictionary<string, int> RaceCounts = new Dictionary<string, int>
        {
            { nameof(SailRaceCourseTests), 8 }, { nameof(SailRaceTrackTests), 11 },
            { nameof(SailRaceRoundingTests), 9 }, { nameof(SailRaceRightOfWayTests), 6 },
            { nameof(SailRaceNpcTests), 14 }
        };
        private readonly List<string> _lines = new List<string>();
        private readonly HashSet<string> _seen = new HashSet<string>(StringComparer.Ordinal);
        private ITestResultAdaptor _result;
        private bool _countsMatch = true;

        public static void Run()
        {
            string output = System.Environment.GetEnvironmentVariable("HH_RACE_TEST_SUMMARY");
            if (string.IsNullOrEmpty(output)) throw new InvalidOperationException("Set HH_RACE_TEST_SUMMARY for the native proof.");
            var callback = new SailRaceVerificationCli();
            var runner = ScriptableObject.CreateInstance<TestRunnerApi>();
            var names = new List<string>();
            foreach (string name in RaceCounts.Keys) names.Add(typeof(SailRaceVerificationCli).Namespace + "." + name);
            names.Add(typeof(SailDriveTests).FullName);
            names.Add(typeof(ModuleBoundaryTests).FullName);
            names.Add(typeof(DefIdentityContractTests).FullName);
            runner.RegisterCallbacks(callback);
            runner.Execute(new ExecutionSettings(new Filter { testMode = TestMode.EditMode, testNames = names.ToArray() })
                { runSynchronously = true });
            var result = callback._result;
            foreach (string name in names)
                if (!callback._seen.Contains(name)) { callback._countsMatch = false; callback._lines.Add("MISSING CLASS " + name); }
            callback._lines.Add(result == null ? "NO RESULTS" :
                $"TOTAL passed={result.PassCount} failed={result.FailCount} skipped={result.SkipCount} inconclusive={result.InconclusiveCount}");
            callback._lines.Add("EXPECTED RACE CLASS COUNTS MATCH=" + callback._countsMatch);
            File.WriteAllLines(output, callback._lines);
            EditorApplication.Exit(result != null && callback._countsMatch && result.FailCount == 0 &&
                result.SkipCount == 0 && result.InconclusiveCount == 0 ? 0 : 2);
        }

        public void RunStarted(ITestAdaptor testsToRun) { }
        public void RunFinished(ITestResultAdaptor result) { _result = result; }
        public void TestStarted(ITestAdaptor test) { }
        public void TestFinished(ITestResultAdaptor result)
        {
            if (result.HasChildren)
            {
                string prefix = typeof(SailRaceVerificationCli).Namespace + ".";
                if (!result.FullName.StartsWith(prefix, StringComparison.Ordinal)) return;
                string name = result.FullName.Substring(prefix.Length);
                if (!RaceCounts.ContainsKey(name) && name != nameof(SailDriveTests) && name != nameof(ModuleBoundaryTests)
                    && name != nameof(DefIdentityContractTests)) return;
                _seen.Add(result.FullName);
                _lines.Add($"CLASS {result.FullName} passed={result.PassCount} failed={result.FailCount} skipped={result.SkipCount}");
                if (RaceCounts.TryGetValue(name, out int expected) && result.PassCount != expected) _countsMatch = false;
            }
            else
            {
                _lines.Add($"CASE {result.FullName} {result.ResultState}");
                if (result.FailCount > 0) _lines.Add(result.Message);
            }
        }
    }
}
