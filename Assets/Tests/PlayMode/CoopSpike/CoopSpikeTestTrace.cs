#if UNITY_EDITOR
using System;
using System.IO;
using NUnit.Framework.Interfaces;
using UnityEngine;
using UnityEngine.TestRunner;

[assembly: TestRunCallback(typeof(HiddenHarbours.Tests.CoopSpike.CoopSpikeTestTrace))]

namespace HiddenHarbours.Tests.CoopSpike
{
    /// <summary>Opt-in per-case evidence, including the last entered case if a run stalls.</summary>
    public sealed class CoopSpikeTestTrace : ITestRunCallback
    {
        [Serializable]
        private sealed class Entry
        {
            public string utc, phase, name, result, message;
            public float timeScale;
        }

        public void RunStarted(ITest testsToRun) { }
        public void RunFinished(ITestResult testResults) { }
        public void TestStarted(ITest test) => Record(test, "START", string.Empty, string.Empty);
        public void TestFinished(ITestResult result) => Record(result.Test, "END", result.ResultState.ToString(), result.Message);

        private static void Record(ITest test, string phase, string result, string message)
        {
            string directory = System.Environment.GetEnvironmentVariable("HH_COOP_TEST_TRACE");
            if (string.IsNullOrEmpty(directory) || test.IsSuite
                || !test.FullName.StartsWith("HiddenHarbours.Tests.CoopSpike.", StringComparison.Ordinal)) return;
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "playmode-cases-" + System.Diagnostics.Process.GetCurrentProcess().Id + ".jsonl");
            File.AppendAllText(path, JsonUtility.ToJson(new Entry
            {
                utc = DateTime.UtcNow.ToString("o"), phase = phase, name = test.FullName,
                result = result, message = message, timeScale = Time.timeScale
            }) + "\n");
        }
    }
}
#endif
