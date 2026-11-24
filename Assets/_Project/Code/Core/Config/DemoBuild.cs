using System;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HiddenHarbours.Core
{
    /// <summary>Read-only runtime view of the demo option selected by the player build entry point.</summary>
    public static class DemoBuild
    {
        public const string ScriptingDefine = "DEMO_BUILD";
        static string _bootScenePath;
        static string _bootSceneName;

#if UNITY_EDITOR
        /// <summary>Session-only override for editor checks. Restore after use.</summary>
        public static bool? Override { get; set; }

#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            _bootScenePath = null;
            _bootSceneName = null;
#if UNITY_EDITOR
            Override = null;
#endif
        }

        public static bool Enabled
        {
            get
            {
#if UNITY_EDITOR
                if (Override.HasValue) return Override.Value;
#endif
#if DEMO_BUILD
                return true;
#else
                return false;
#endif
            }
        }

        public static bool DevShortcutsAllowed => !Enabled;

        public static bool IncludesScene(string sceneName)
        {
            if (!Enabled) return true;
            if (_bootScenePath == null)
            {
                _bootScenePath = SceneUtility.GetScenePathByBuildIndex(0);
                _bootSceneName = Path.GetFileNameWithoutExtension(_bootScenePath);
            }
            return !string.IsNullOrWhiteSpace(sceneName) && !string.IsNullOrWhiteSpace(_bootScenePath)
                && (string.Equals(sceneName, _bootScenePath, StringComparison.Ordinal)
                    || string.Equals(sceneName, _bootSceneName, StringComparison.Ordinal));
        }

        /// <summary>The first player scene is the demo region; it is selected by the build's scene list.</summary>
        public static bool IncludesScene(bool demo, string sceneName, string bootScenePath)
        {
            if (!demo) return true;
            if (string.IsNullOrWhiteSpace(sceneName) || string.IsNullOrWhiteSpace(bootScenePath)) return false;
            return string.Equals(sceneName, bootScenePath, StringComparison.Ordinal)
                || string.Equals(sceneName, Path.GetFileNameWithoutExtension(bootScenePath), StringComparison.Ordinal);
        }
    }

    /// <summary>Published once per session after the qualifying saved sleep. Presentation listens here and uses DemoEnding.TextKey.</summary>
    public readonly struct DemoEnded { }
}
