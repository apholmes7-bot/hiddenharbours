#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.App.Editor;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Pins the ADR 0019 §1 wipe-warning seam: <see cref="RegionBuildGuard.ShouldWarn"/> must warn IFF the
    /// scene file exists on disk — the file-exists key the ADR chose so no adoption bookkeeping is needed.
    /// A first-ever build (no file) proceeds silently; a re-run over a committed/hand-edited scene warns, so
    /// a builder re-run can never silently wipe hand work (the boat-spotlight incident).
    ///
    /// <para>The batchmode refusal (ledger 09-19: a batchmode <c>NineMileCreekBuilder.Build</c> over an existing
    /// scene exited 0) is pinned two ways: the pure <see cref="RegionBuildGuard.Decide"/> over all four
    /// (sceneExists, isBatchMode) cases, and <see cref="RegionBuildGuard.ConfirmOverwrite"/> driven end to end
    /// with its batchmode probe and its exit replaced — CI runs EditMode in batchmode, so the real
    /// <c>EditorApplication.Exit</c> must never be reached from here. The interactive ASK path still shows an
    /// <c>EditorUtility.DisplayDialog</c>, which can't run headlessly; it's verified by the owner running Build
    /// on an existing scene and seeing the prompt.</para>
    /// </summary>
    public class RegionBuildGuardTests
    {
        Func<bool> _realIsBatchMode;
        Action<int> _realExit;
        List<int> _exitCalls;
        string _tempScene;

        [SetUp]
        public void SetUp()
        {
            _realIsBatchMode = RegionBuildGuard.IsBatchMode;
            _realExit = RegionBuildGuard.Exit;
            _exitCalls = new List<int>();
            RegionBuildGuard.Exit = code => _exitCalls.Add(code);
        }

        [TearDown]
        public void TearDown()
        {
            RegionBuildGuard.IsBatchMode = _realIsBatchMode;
            RegionBuildGuard.Exit = _realExit;
            if (_tempScene != null && File.Exists(_tempScene))
                File.Delete(_tempScene);
            _tempScene = null;
        }

        [Test]
        public void ShouldWarn_False_WhenSceneFileDoesNotExist()
        {
            // A path that cannot exist on disk → first-ever build, nothing to wipe → no warning.
            string missing = Path.Combine(Path.GetTempPath(), "hh_no_such_scene_" + Path.GetRandomFileName() + ".unity");
            Assert.IsFalse(File.Exists(missing), "test precondition: the path must not exist");

            Assert.IsFalse(RegionBuildGuard.ShouldWarn(missing));
        }

        [Test]
        public void ShouldWarn_True_WhenSceneFileExists()
        {
            // A real file on disk → a re-run would wipe it → must warn.
            string existing = Path.GetTempFileName(); // creates the file
            try
            {
                Assert.IsTrue(File.Exists(existing), "test precondition: the file must exist");
                Assert.IsTrue(RegionBuildGuard.ShouldWarn(existing));
            }
            finally
            {
                File.Delete(existing);
            }
        }

        [Test]
        public void ShouldWarn_False_WhenPathIsNullOrEmpty()
        {
            // Defensive: a null/empty path is never a real scene → don't warn (and don't throw).
            Assert.IsFalse(RegionBuildGuard.ShouldWarn(null));
            Assert.IsFalse(RegionBuildGuard.ShouldWarn(string.Empty));
        }

        // ---- the decision, all four cases ------------------------------------------------------------

        [Test]
        public void Decide_NoScene_Interactive_Proceeds()
        {
            Assert.AreEqual(RegionBuildGuard.Verdict.Proceed, RegionBuildGuard.Decide(false, false));
        }

        [Test]
        public void Decide_NoScene_Batchmode_Proceeds()
        {
            // A first-ever build has nothing to wipe, so batchmode may create it.
            Assert.AreEqual(RegionBuildGuard.Verdict.Proceed, RegionBuildGuard.Decide(false, true));
        }

        [Test]
        public void Decide_SceneExists_Interactive_Asks()
        {
            // The owner's menu path is unchanged: a person at the editor gets the wipe warning.
            Assert.AreEqual(RegionBuildGuard.Verdict.Ask, RegionBuildGuard.Decide(true, false));
        }

        [Test]
        public void Decide_SceneExists_Batchmode_RefusesAndFails()
        {
            // THE BUG: nobody can answer a dialog in batchmode, and the old path let the dialog say no and the
            // editor exit 0. An existing scene in batchmode must be refused AND fail the run.
            Assert.AreEqual(RegionBuildGuard.Verdict.RefuseAndFail, RegionBuildGuard.Decide(true, true));
        }

        [Test]
        public void RefusedRebuildExitCode_IsNonZero()
        {
            // A zero here would be the original false green under a new name.
            Assert.AreNotEqual(0, RegionBuildGuard.RefusedRebuildExitCode);
        }

        // ---- ConfirmOverwrite end to end, batchmode forced, exit stubbed -------------------------------

        [Test]
        public void ConfirmOverwrite_Batchmode_ExistingScene_LogsRegionAndPath_ExitsNonZero_AndRefuses()
        {
            RegionBuildGuard.IsBatchMode = () => true;
            _tempScene = Path.GetTempFileName(); // an existing "scene" on disk
            const string region = "Test Region Guard";

            LogAssert.Expect(LogType.Error,
                new Regex(Regex.Escape(region) + ".*" + Regex.Escape(_tempScene), RegexOptions.Singleline));

            bool proceed = RegionBuildGuard.ConfirmOverwrite(region, _tempScene);

            Assert.IsFalse(proceed, "a refused rebuild must tell the builder to touch nothing");
            Assert.AreEqual(1, _exitCalls.Count, "a refused batchmode rebuild must end the editor exactly once");
            Assert.AreNotEqual(0, _exitCalls[0], "exit 0 on a refused rebuild is the false green this guards");
            Assert.AreEqual(RegionBuildGuard.RefusedRebuildExitCode, _exitCalls[0],
                "the exit code must be the named constant, not a literal at the call site");
            Assert.IsTrue(File.Exists(_tempScene), "the guard must not touch the scene it refused to rebuild");
        }

        [Test]
        public void ConfirmOverwrite_Batchmode_NoScene_ProceedsWithoutExiting()
        {
            RegionBuildGuard.IsBatchMode = () => true;
            string missing = Path.Combine(Path.GetTempPath(), "hh_no_such_scene_" + Path.GetRandomFileName() + ".unity");
            Assert.IsFalse(File.Exists(missing), "test precondition: the path must not exist");

            bool proceed = RegionBuildGuard.ConfirmOverwrite("Test Region Guard", missing);

            Assert.IsTrue(proceed, "a first-ever batchmode build has nothing to wipe and must proceed");
            Assert.AreEqual(0, _exitCalls.Count, "a build that proceeds must not end the editor");
            LogAssert.NoUnexpectedReceived();
        }
    }
}
#endif
