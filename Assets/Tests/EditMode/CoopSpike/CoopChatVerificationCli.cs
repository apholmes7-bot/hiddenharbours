using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace HiddenHarbours.Tests.CoopSpike
{
    /// <summary>Focused native proof with compact per-case evidence; no full results XML.</summary>
    [InitializeOnLoad]
    public static class CoopChatVerificationCli
    {
        private const string Key = "CoopChatVerification.";
        private static TestRunnerApi _api;
        private static readonly MethodInfo Active = typeof(TestRunnerApi).GetMethod("IsRunActive", BindingFlags.Static | BindingFlags.NonPublic);
        private static string Output => System.Environment.GetEnvironmentVariable("HH_CHAT_TEST_REPORT");

        static CoopChatVerificationCli()
        {
            if (SessionState.GetBool(Key + "active", false)) Attach();
        }

        private static void Attach()
        {
            _api = ScriptableObject.CreateInstance<TestRunnerApi>();
            TestRunnerApi.RegisterTestCallback(new Results());
            EditorApplication.update -= Pump;
            EditorApplication.update += Pump;
        }

        public static void Run()
        {
            if (string.IsNullOrEmpty(Output)) throw new InvalidOperationException("Provide a compact report destination.");
            bool play = System.Environment.GetEnvironmentVariable("HH_CHAT_TEST_MODE") == "play";
            SessionState.SetBool(Key + "active", true);
            SessionState.SetBool(Key + "finished", false);
            SessionState.SetInt(Key + "exit", 3);
            Attach();
            _api.Execute(new ExecutionSettings(new Filter
            {
                testMode = play ? TestMode.PlayMode : TestMode.EditMode,
                groupNames = play ? new[] { "^HiddenHarbours.Tests.CoopSpike.CoopChatNetworkTests\\." } : new[]
                {
                    "^HiddenHarbours.Tests.CoopSpike.CoopChatTests\\.",
                    "^HiddenHarbours.Tests.CoopSpike.CoopChatInputTests\\.",
                    "^HiddenHarbours.Tests.CoopSpike.CoopSpikePlanTests\\."
                }
            }) { runSynchronously = !play });
        }

        private static void Pump()
        {
            if (!SessionState.GetBool(Key + "finished", false) || EditorApplication.isPlayingOrWillChangePlaymode ||
                EditorApplication.isCompiling || EditorApplication.isUpdating || (Active != null && (bool)Active.Invoke(null, null))) return;
            SessionState.SetBool(Key + "active", false);
            EditorApplication.Exit(SessionState.GetInt(Key + "exit", 3));
        }

        private sealed class Results : ICallbacks
        {
            public void RunStarted(ITestAdaptor test) { }
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result) { }
            public void RunFinished(ITestResultAdaptor result)
            {
                var lines = new List<string>();
                var counts = new Dictionary<string, int>();
                Collect(result, lines, counts);
                var expected = System.Environment.GetEnvironmentVariable("HH_CHAT_TEST_MODE") == "play"
                    ? new Dictionary<string, int> { { "CoopChatNetworkTests", 4 } }
                    : new Dictionary<string, int> { { "CoopChatTests", 26 }, { "CoopChatInputTests", 8 }, { "CoopSpikePlanTests", 21 } };
                bool exactCounts = counts.Count == expected.Count;
                foreach (var pair in expected)
                {
                    counts.TryGetValue(pair.Key, out int actual);
                    exactCounts &= actual == pair.Value;
                    lines.Add($"CLASS {pair.Key} passed={actual} expected={pair.Value}");
                }
                lines.Add($"TOTAL passed={result.PassCount} failed={result.FailCount} skipped={result.SkipCount} inconclusive={result.InconclusiveCount}");
                File.WriteAllLines(Output, lines);
                SessionState.SetInt(Key + "exit", exactCounts && result.FailCount == 0 &&
                    result.SkipCount == 0 && result.InconclusiveCount == 0 ? 0 : 2);
                SessionState.SetBool(Key + "finished", true);
            }

            private static void Collect(ITestResultAdaptor result, List<string> lines, Dictionary<string, int> counts)
            {
                if (result.HasChildren)
                {
                    foreach (var child in result.Children) Collect(child, lines, counts);
                }
                else
                {
                    lines.Add("CASE " + result.FullName + " " + result.ResultState);
                    const string prefix = "HiddenHarbours.Tests.CoopSpike.";
                    if (result.FullName.StartsWith(prefix, StringComparison.Ordinal) && result.ResultState == "Passed")
                    {
                        string name = result.FullName.Substring(prefix.Length).Split('.')[0];
                        counts.TryGetValue(name, out int count);
                        counts[name] = count + 1;
                    }
                    if (result.FailCount != 0) lines.Add("FAIL " + result.Message + " " + result.StackTrace);
                }
            }
        }
    }
}
