using System;
using System.IO;
using HiddenHarbours.Core;
using UnityEngine;
using UnityEngine.TestTools;

// The runner reads assembly-level setup and cleanup from every loaded assembly whose metadata references
// UnityEditor.TestRunner (see TestRunSave.RunnerAnchor), and runs them in the editor: the setup before it
// enters play mode, the cleanup after it has left.
[assembly: PrebuildSetupWithTestData(typeof(HiddenHarbours.Tests.PlayMode.TestRunSave))]
[assembly: PostBuildCleanupWithTestData(typeof(HiddenHarbours.Tests.PlayMode.TestRunSave))]

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// Gives every PlayMode run a save of its own, so no test reads or writes the player's
    /// (<c>persistentDataPath/savegame.json</c>, which every checkout on a machine shares).
    ///
    /// <para><b>When.</b> <see cref="SaveService"/> reads its file in Awake, at BeforeSceneLoad of the play
    /// session, before any test, SetUpFixture or run callback exists. Entering play mode reloads the scripting
    /// domain, so no static can carry the redirect across. The runner's setup runs in the editor before it
    /// enters play mode: it sets a process environment variable (<see cref="SaveStore.TestRunSaveVariable"/>),
    /// which survives the reload, and <see cref="SaveStore.ActivePath"/> reads it in that Awake. The service
    /// keeps the file it opened, so its last write, on the runner's exit from play mode (OnApplicationQuit),
    /// lands in the run's save too.</para>
    ///
    /// <para><b>Where.</b> <c>&lt;project&gt;/Temp/TestRunSaves/&lt;guid&gt;/savegame.json</c>. Temp is this
    /// checkout's own, unlike <c>temporaryCachePath</c>, which every checkout shares, and Unity empties it when
    /// the editor closes; the guid makes the file this run's. It starts absent, so every run begins on a new
    /// game, as CI always has, whatever this machine's save holds.</para>
    ///
    /// <para><b>Ending.</b> Disarmed when the editor is back in edit mode (<see cref="DisarmOnEditMode"/>)
    /// and again by the runner's cleanup, whichever comes first. A cancelled run skips its cleanup but still
    /// leaves play mode, so the next Play opens the real save. EditMode runs are not armed: nothing in them
    /// starts the service, and a cancelled one would leave the variable set with no play session to end
    /// it.</para>
    /// </summary>
    public sealed class TestRunSave : IPrebuildSetupWithTestData, IPostbuildCleanupWithTestData
    {
        /// <summary>The folder under the project's Temp that holds each run's save folder.</summary>
        public const string RunsFolder = "TestRunSaves";

        /// <summary><c>&lt;project&gt;/Temp/TestRunSaves</c>, as a full path.</summary>
        public static string RunsRoot =>
            Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp", RunsFolder));

        public void Setup(TestData testData)
        {
            if ((testData.TestMode & TestMode.PlayMode) == 0) return;

            Disarm();
            string run = Path.Combine(RunsRoot, Guid.NewGuid().ToString("N"));
            System.Environment.SetEnvironmentVariable(SaveStore.TestRunSaveVariable,
                                               Path.Combine(run, SaveStore.DefaultFileName));
        }

        public void Cleanup(TestData testData) => Disarm();

        /// <summary>
        /// Clear the variable and delete the run's folder, but only a folder directly under
        /// <see cref="RunsRoot"/>. Safe to call when nothing is armed. The delete is best effort: a file held
        /// open must not fail a finished run, and Unity empties Temp when the editor closes anyway.
        /// </summary>
        public static void Disarm()
        {
            string save = System.Environment.GetEnvironmentVariable(SaveStore.TestRunSaveVariable);
            if (string.IsNullOrEmpty(save)) return;
            System.Environment.SetEnvironmentVariable(SaveStore.TestRunSaveVariable, null);

            try
            {
                string run = Path.GetDirectoryName(Path.GetFullPath(save));
                if (run != null && Directory.Exists(run)
                    && string.Equals(Path.GetDirectoryName(run), RunsRoot, StringComparison.OrdinalIgnoreCase))
                    Directory.Delete(run, recursive: true);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

#if UNITY_EDITOR
        /// <summary>
        /// The runner only reads assembly-level attributes from assemblies whose METADATA references
        /// UnityEditor.TestRunner, and the compiler keeps a reference only where code uses a type from it.
        /// This field is that use, so the hook does not depend on another test in this assembly happening to
        /// use one.
        /// </summary>
        internal static readonly Type RunnerAnchor = typeof(UnityEditor.TestTools.TestRunner.Api.TestRunnerApi);
#endif
    }

#if UNITY_EDITOR
    /// <summary>
    /// Disarms <see cref="TestRunSave"/> when the editor is back in edit mode. It subscribes again after every
    /// domain reload, including the one on entering play mode, so it is listening when a run leaves play
    /// mode, cancelled or not. Once the play session is over, nothing is left to read the variable.
    /// </summary>
    [UnityEditor.InitializeOnLoad]
    internal static class DisarmOnEditMode
    {
        static DisarmOnEditMode()
        {
            UnityEditor.EditorApplication.playModeStateChanged += state =>
            {
                if (state == UnityEditor.PlayModeStateChange.EnteredEditMode) TestRunSave.Disarm();
            };
        }
    }
#endif
}
