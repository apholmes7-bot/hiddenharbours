using UnityEngine;
using UnityEngine.SceneManagement;

namespace HiddenHarbours.World
{
    /// <summary>
    /// <b>Puts each store's front outside it</b>: the sign and the open-hours displays. Self-installing like
    /// <see cref="SoldOutSignsDirector"/>: after the first scene loads, and again whenever a region scene
    /// loads, it finds the building each <see cref="StoreFrontDef"/> names (one sweep per scene load, never per
    /// frame) and gives it a <see cref="StoreFront"/>. No scene file changes: the front is data.
    /// </summary>
    public sealed class StoreFrontsDirector : MonoBehaviour
    {
        private static StoreFrontsDirector _instance;
        private StoreFrontDef[] _defs;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;
            var go = new GameObject("[StoreFrontsDirector]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<StoreFrontsDirector>();
        }

        private void OnEnable()
        {
            if (_instance == null) _instance = this;
            SceneManager.sceneLoaded += OnSceneLoaded;
            DressAll();
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (_instance == this) _instance = null;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => DressAll();

        /// <summary>Gives every named building in the loaded scenes its front. Idempotent: a building that
        /// already has one keeps it. Public so a probe or a test can run it.</summary>
        public void DressAll()
        {
            if (_defs == null) _defs = Resources.LoadAll<StoreFrontDef>(StoreFrontDef.ResourcesFolder);
            if (_defs == null || _defs.Length == 0) return;
            BuildingInterior[] buildings =
                Object.FindObjectsByType<BuildingInterior>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (StoreFrontDef def in _defs)
            {
                if (def == null || string.IsNullOrEmpty(def.BuildingName)) continue;
                foreach (BuildingInterior building in buildings)
                {
                    if (building == null || building.gameObject.name != def.BuildingName) continue;
                    if (building.GetComponentInChildren<StoreFront>(true) != null) continue;
                    StoreFront.Build(def, building.transform);
                }
            }
        }
    }
}
