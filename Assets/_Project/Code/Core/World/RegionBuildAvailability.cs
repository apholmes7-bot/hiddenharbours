using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HiddenHarbours.Core
{
    /// <summary>The shared question before loading or warming a region.</summary>
    public static class RegionBuildAvailability
    {
#if UNITY_EDITOR
        /// <summary>Overrides scene inclusion for an editor review or test. Restore after use.</summary>
        public static Func<string, string, bool> Override { get; set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOverride() => Override = null;
#endif

        public static bool IsAvailable(string regionId, string sceneName)
        {
            if (string.IsNullOrWhiteSpace(sceneName)) return false;
            if (!DemoBuild.IncludesScene(sceneName)) return false;
#if UNITY_EDITOR
            if (Override != null) return Override(regionId, sceneName);
            // Scenes created by an editor fixture can be revisited without a build entry.
            Scene scene = SceneManager.GetSceneByName(sceneName);
            if (scene.IsValid() && scene.isLoaded) return true;
#endif
            return Application.CanStreamedLevelBeLoaded(sceneName);
        }
    }

    /// <summary>A requested destination is unavailable; no arrival will follow this refusal.</summary>
    public readonly struct RegionTravelRefused
    {
        public readonly string RegionId;
        public readonly string SceneName;

        public RegionTravelRefused(string regionId, string sceneName)
        {
            RegionId = regionId;
            SceneName = sceneName;
        }
    }
}
