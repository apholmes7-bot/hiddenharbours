using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HiddenHarbours.World
{
    /// <summary>
    /// The scene-load path for regions (VS-22): loads a <see cref="RegionDef"/>'s scene <b>additively</b>
    /// (CLAUDE.md §3 "scene per region, loaded additively") and makes it the active scene. A thin
    /// wrapper over <see cref="SceneManager"/> driven by the pure <see cref="RegionTravel"/> decisions
    /// and a <see cref="RegionRegistry"/> built from the wired regions.
    ///
    /// SCOPE (VS-22): the load mechanism for the additive Cove↔Greywick hop. Carrying the player across +
    /// re-showing a region is owned by the persistent core (App: PersistentObject + RegionTravelCoordinator,
    /// GreyboxBuilder) — flagged for lead-architect. To keep the persistent core from being duplicated, a
    /// region scene is loaded once and then TOGGLED by the coordinator rather than unloaded/reloaded; so
    /// <see cref="Travel"/> re-activates an already-loaded region instead of loading a second copy.
    /// </summary>
    public sealed class RegionSceneLoader : MonoBehaviour
    {
        [Tooltip("Regions this loader knows about (built into a RegionRegistry on Awake).")]
        [SerializeField] private RegionDef[] _regions = new RegionDef[0];

        [Tooltip("Scene the player currently occupies (so we don't reload the region we're already in). " +
                 "Defaults to this loader's own scene name at runtime if left blank.")]
        [SerializeField] private string _currentSceneName = "";

        private RegionRegistry _registry;

        public RegionRegistry Registry => _registry ??= new RegionRegistry(_regions);

        /// <summary>The scene the player currently occupies (the active region).</summary>
        public string CurrentSceneName => _currentSceneName;

        private void Awake()
        {
            _registry = new RegionRegistry(_regions);
            if (string.IsNullOrEmpty(_currentSceneName))
                _currentSceneName = gameObject.scene.name;
        }

        /// <summary>Travel to the region with this id (no-op if unknown / already there).</summary>
        public void TravelTo(string regionId)
        {
            if (Registry.TryGet(regionId, out var region)) Travel(region);
            else Debug.LogWarning($"[RegionSceneLoader] Unknown region id '{regionId}'.", this);
        }

        /// <summary>
        /// Make the target region the active scene, carrying the persistent player/boat across. No-op if
        /// it can't be loaded or we're already there (<see cref="RegionTravel.ShouldLoad"/>). If the region
        /// scene is ALREADY loaded (a region we visited earlier and the coordinator only toggled off), we
        /// just re-activate it — loading a second copy would duplicate the scene. Setting the active scene
        /// raises <see cref="SceneManager.activeSceneChanged"/>, which the persistent RegionTravelCoordinator
        /// listens to in order to show the region, silence its stray camera, and rebind the rig.
        /// </summary>
        public void Travel(RegionDef to)
        {
            if (!RegionTravel.ShouldLoad(_currentSceneName, to))
                return;

            // Already loaded (re-visit) → re-activate it, don't load a duplicate.
            var existing = SceneManager.GetSceneByName(to.SceneName);
            if (existing.IsValid() && existing.isLoaded)
            {
                SceneManager.SetActiveScene(existing);
                _currentSceneName = to.SceneName;
                return;
            }

            var op = SceneManager.LoadSceneAsync(to.SceneName, LoadSceneMode.Additive);
            if (op == null)
            {
                // Scene not in Build Settings (e.g. the region scene hasn't been generated yet).
                Debug.LogWarning($"[RegionSceneLoader] Could not load scene '{to.SceneName}' for " +
                                 $"{to.Id} — is it built and in Build Settings?", this);
                return;
            }

            string loaded = to.SceneName;
            op.completed += _ =>
            {
                var scene = SceneManager.GetSceneByName(loaded);
                if (scene.IsValid() && scene.isLoaded) SceneManager.SetActiveScene(scene);
                _currentSceneName = loaded;
            };
        }
    }
}
