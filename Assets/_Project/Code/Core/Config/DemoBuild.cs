using System;
using System.IO;
using UnityEngine;

namespace HiddenHarbours.Core
{
    /// <summary>Read-only runtime view of the demo option selected by the player build entry point.</summary>
    public static class DemoBuild
    {
        public const string ScriptingDefine = "DEMO_BUILD";
        [Serializable]
        sealed class SceneList
        {
            public string[] scenes;
        }

        static string[] _scenePaths;

#if UNITY_EDITOR
        /// <summary>Session-only override for editor checks. Restore after use.</summary>
        public static bool? Override { get; set; }

#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Reset()
        {
            _scenePaths = null;
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

        static string[] ConfiguredScenePaths
        {
            get
            {
                if (_scenePaths != null) return _scenePaths;
                var source = Resources.Load<TextAsset>("DemoScenes");
                if (source == null) throw new InvalidOperationException("The demo scene list is missing.");
                var list = JsonUtility.FromJson<SceneList>(source.text);
                if (list?.scenes == null || list.scenes.Length == 0)
                    throw new InvalidOperationException("The demo scene list is empty.");
                _scenePaths = list.scenes;
                return _scenePaths;
            }
        }

        /// <summary>The same ordered scene list supplies packaging and runtime destination checks.</summary>
        public static string[] ScenePaths() => (string[])ConfiguredScenePaths.Clone();

        public static bool IncludesScene(string sceneName)
            => !Enabled || IncludesScene(true, sceneName, ConfiguredScenePaths);

        public static bool IncludesScene(bool demo, string sceneName, string[] scenePaths)
        {
            if (!demo) return true;
            if (string.IsNullOrWhiteSpace(sceneName) || scenePaths == null) return false;
            foreach (string path in scenePaths)
                if (!string.IsNullOrWhiteSpace(path) &&
                    (string.Equals(sceneName, path, StringComparison.Ordinal) ||
                     string.Equals(sceneName, Path.GetFileNameWithoutExtension(path), StringComparison.Ordinal)))
                    return true;
            return false;
        }
    }

    /// <summary>Reserved for a future ending presentation. No gameplay path publishes this yet.</summary>
    public readonly struct DemoEnded { }
}
