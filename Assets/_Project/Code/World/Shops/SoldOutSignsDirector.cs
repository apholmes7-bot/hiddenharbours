using UnityEngine;
using UnityEngine.SceneManagement;

namespace HiddenHarbours.World
{
    /// <summary>
    /// <b>Hangs the sold-out cards in every store that has shelf slots</b> (store sold-out slice).
    /// Self-installing: after the first scene loads, and again whenever a region scene loads, it finds the
    /// building each <see cref="ShopShelfSlotsDef"/> names (one sweep per scene load, never per frame) and
    /// gives it a <see cref="SoldOutSigns"/>. No scene file or builder changes: the slots are data.
    /// </summary>
    public sealed class SoldOutSignsDirector : MonoBehaviour
    {
        private static SoldOutSignsDirector _instance;
        private ShopShelfSlotsDef[] _defs;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;
            var go = new GameObject("[SoldOutSignsDirector]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<SoldOutSignsDirector>();
        }

        private void OnEnable()
        {
            if (_instance == null) _instance = this;
            SceneManager.sceneLoaded += OnSceneLoaded;
            HangAll();
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (_instance == this) _instance = null;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => HangAll();

        /// <summary>Gives every named building in the loaded scenes its cards. Idempotent: a building that
        /// already has them keeps them. Public so a probe or a test can run it.</summary>
        public void HangAll()
        {
            if (_defs == null) _defs = Resources.LoadAll<ShopShelfSlotsDef>(ShopShelfSlotsDef.ResourcesFolder);
            if (_defs == null || _defs.Length == 0) return;
            BuildingInterior[] buildings =
                Object.FindObjectsByType<BuildingInterior>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (ShopShelfSlotsDef def in _defs)
            {
                if (def == null || string.IsNullOrEmpty(def.BuildingName)) continue;
                foreach (BuildingInterior building in buildings)
                {
                    if (building == null || building.gameObject.name != def.BuildingName) continue;
                    if (building.GetComponentInChildren<SoldOutSigns>(true) != null) continue;
                    Hang(def, building);
                }
            }
        }

        /// <summary>One building's cards, under a child of the building so they move and unload with it.</summary>
        public static SoldOutSigns Hang(ShopShelfSlotsDef def, BuildingInterior building)
        {
            var go = new GameObject("SoldOutSigns");
            go.transform.SetParent(building.transform, false);
            var signs = go.AddComponent<SoldOutSigns>();
            signs.Configure(def, building);
            return signs;
        }
    }
}
