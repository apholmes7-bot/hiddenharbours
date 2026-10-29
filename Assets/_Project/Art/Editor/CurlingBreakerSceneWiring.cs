using System.Collections.Generic;
using HiddenHarbours.Core;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HiddenHarbours.Art.Editor
{
    /// <summary>
    /// <b>ADR 0040 B2 — installs a region's <see cref="CurlingBreakerRegion"/> as its scene is built or
    /// played</b>, from the picked sites in the region's data folder
    /// (<c>Data/Regions/&lt;scene&gt;/BreakerSites</c>). No scene file changes: a region without that
    /// folder, or without a displaced sea, gets nothing, and the host it adds lives only in the
    /// built and the played copy of the scene. Idempotent: a scene that has a host keeps it.
    /// </summary>
    [InitializeOnLoad]
    public sealed class CurlingBreakerSceneWiring : IProcessSceneWithReport
    {
        public const string RegionsFolder = "Assets/_Project/Data/Regions";
        public const string SitesFolderName = "BreakerSites";
        public const string ShaderPath = "Assets/_Project/Art/Shaders/HiddenHarboursCurlingBreaker.shader";
        public const string HostName = "CurlingBreakers";

        static CurlingBreakerSceneWiring()
        {
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        public int callbackOrder => 0;

        public void OnProcessScene(Scene scene, BuildReport report) => Wire(scene);

        private static void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.EnteredPlayMode) return;
            for (int i = 0; i < SceneManager.sceneCount; i++) Wire(SceneManager.GetSceneAt(i));
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (EditorApplication.isPlaying) Wire(scene);
        }

        /// <summary>The host added to <paramref name="scene"/>, or null when it has one already or its
        /// region picks no site.</summary>
        public static CurlingBreakerRegion Wire(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded) return null;
            string folder = SitesFolder(scene.name);
            if (!AssetDatabase.IsValidFolder(folder)) return null;

            DisplacedWaterSurface surface = null;
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i].GetComponentInChildren<CurlingBreakerRegion>(true) != null) return null;
                if (surface == null) surface = roots[i].GetComponentInChildren<DisplacedWaterSurface>(true);
            }
            Renderer sea = surface != null ? surface.GetComponent<Renderer>() : null;
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            CurlingBreakerSiteDef[] sites = LoadSites(folder);
            if (sea == null || shader == null || sites.Length == 0) return null;

            var go = new GameObject(HostName);
            SceneManager.MoveGameObjectToScene(go, scene);
            var host = go.AddComponent<CurlingBreakerRegion>();
            host.Configure(sites, sea, shader);
            return host;
        }

        public static string SitesFolder(string sceneName)
            => RegionsFolder + "/" + sceneName + "/" + SitesFolderName;

        /// <summary>The folder's enabled sites, in id order.</summary>
        public static CurlingBreakerSiteDef[] LoadSites(string folder)
        {
            var sites = new List<CurlingBreakerSiteDef>();
            string[] guids = AssetDatabase.FindAssets("t:" + nameof(CurlingBreakerSiteDef), new[] { folder });
            for (int i = 0; i < guids.Length; i++)
            {
                var site = AssetDatabase.LoadAssetAtPath<CurlingBreakerSiteDef>(
                    AssetDatabase.GUIDToAssetPath(guids[i]));
                if (site != null && site.Enabled) sites.Add(site);
            }
            sites.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            return sites.ToArray();
        }
    }
}
