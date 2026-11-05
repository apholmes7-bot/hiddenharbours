using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>Run both acceptance modes in one editor process, including domain reloads.</summary>
    [InitializeOnLoad]
    public static class SeatedHelmAcceptanceCli
    {
        const string Key = "SeatedHelmAcceptance.";
        static readonly MethodInfo Active = typeof(TestRunnerApi).GetMethod("IsRunActive",
            BindingFlags.Static | BindingFlags.NonPublic);
        static TestRunnerApi _api;
        static double _idleSince;
        static string Output => Path.Combine(Directory.GetParent(Application.dataPath).FullName,
            "Evidence~", "rig-facts", "production");

        static SeatedHelmAcceptanceCli()
        {
            if (SessionState.GetBool(Key + "active", false)) Attach();
        }

        static void Attach()
        {
            _api = ScriptableObject.CreateInstance<TestRunnerApi>();
            TestRunnerApi.RegisterTestCallback(new Results());
            EditorApplication.update -= Pump;
            EditorApplication.update += Pump;
        }

        public static void Run()
        {
            Directory.CreateDirectory(Output);
            SessionState.SetBool(Key + "active", true);
            SessionState.SetString(Key + "phase", "edit");
            SessionState.SetString(Key + "started", DateTime.UtcNow.ToString("O"));
            SessionState.SetInt(Key + "exit", 0);
            if (_api == null) Attach();
            try
            {
                if (Active == null) throw new InvalidOperationException("Test runner activity probe missing");
                _api.Execute(new ExecutionSettings(new Filter {
                    testMode = TestMode.EditMode,
                    groupNames = new[] {
                        "^HiddenHarbours.Tests.RigBaking.SeatedHelmContractTests\\.",
                        "^HiddenHarbours.Tests.EditMode.SeatedHelmPlacementTests\\.",
                        "^HiddenHarbours.Tests.EditMode.CharacterClipStateMapContentTests\\.",
                        "^HiddenHarbours.Tests.EditMode.CharacterSkinDefContentTests\\.",
                        "^HiddenHarbours.Tests.RigBaking.CharacterSkinCastBakeTests\\."
                    }
                }) { runSynchronously = true });
            }
            catch (Exception error) { Abort(error); }
        }

        static void Abort(Exception error)
        {
            File.WriteAllText(Path.Combine(Output, "launch-3-harness-error.txt"), error.ToString());
            Debug.LogException(error);
            SessionState.SetInt(Key + "exit", 3);
            SessionState.SetString(Key + "phase", "exit");
        }

        static void Pump()
        {
            if (!SessionState.GetBool(Key + "active", false)) return;
            if (DateTime.TryParse(SessionState.GetString(Key + "started", ""), out var start)
                && DateTime.UtcNow - start.ToUniversalTime() > TimeSpan.FromMinutes(20))
            {
                SessionState.SetBool(Key + "active", false);
                File.WriteAllText(Path.Combine(Output, "launch-3-timeout.txt"), "Acceptance exceeded twenty minutes");
                EditorApplication.Exit(4);
                return;
            }
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling
                || EditorApplication.isUpdating || (bool)Active.Invoke(null, null))
            { _idleSince = 0; return; }
            if (_idleSince == 0) { _idleSince = EditorApplication.timeSinceStartup; return; }
            if (EditorApplication.timeSinceStartup - _idleSince < 2) return;
            string phase = SessionState.GetString(Key + "phase", "");
            if (phase == "play-ready")
            {
                SessionState.SetString(Key + "phase", "play");
                _idleSince = 0;
                try
                {
                    _api.Execute(new ExecutionSettings(new Filter {
                        testMode = TestMode.PlayMode,
                        groupNames = new[] {
                            "^HiddenHarbours.Tests.PlayMode.GoFastHelmPlacementPlayTests\\.",
                            "^HiddenHarbours.Tests.PlayMode.SeatedHelmScenePlayTests\\."
                        }
                    }));
                }
                catch (Exception error) { Abort(error); }
            }
            else if (phase == "exit")
            {
                SessionState.SetBool(Key + "active", false);
                EditorApplication.Exit(SessionState.GetInt(Key + "exit", 3));
            }
        }

        sealed class Results : ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun) { }
            public void TestStarted(ITestAdaptor test) { }
            public void TestFinished(ITestResultAdaptor result) { }
            public void RunFinished(ITestResultAdaptor result)
            {
                string phase = SessionState.GetString(Key + "phase", "");
                if (phase != "edit" && phase != "play") return;
                TestRunnerApi.SaveResultToFile(result, Path.Combine(Output, "launch-3-" + phase + ".xml"));
                int expected = phase == "edit" ? 23 : 10;
                bool pass = result.PassCount == expected && result.FailCount == 0
                    && result.SkipCount == 0 && result.InconclusiveCount == 0;
                Debug.Log($"[SeatedHelmAcceptance] {phase}: {result.PassCount} passed, {result.FailCount} failed, {result.SkipCount} skipped");
                if (!pass) SessionState.SetInt(Key + "exit", 2);
                SessionState.SetString(Key + "phase", pass && phase == "edit" ? "play-ready" : "exit");
            }
        }
    }
}
