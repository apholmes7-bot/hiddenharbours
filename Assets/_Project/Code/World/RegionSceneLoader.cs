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
    /// SCOPE (VS-22): this is the load mechanism + a stub for the Cove↔Greywick hop. It does NOT yet
    /// unload the origin region or carry the player across — the on-foot player/boat are authored into
    /// the origin (Cove) scene by the greybox builder, so persisting/repositioning them across a load
    /// belongs with a bootstrap/origin-scene change (GreyboxBuilder), which this task must not touch.
    /// Those steps are marked TODO. Until then this additively layers a region in for authoring/review.
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
        /// Additively load the target region's scene and make it active. No-op if it can't be loaded or
        /// we're already there (<see cref="RegionTravel.ShouldLoad"/>).
        /// TODO (needs bootstrap/GreyboxBuilder): unload the origin region and carry the player across.
        /// </summary>
        public void Travel(RegionDef to)
        {
            if (!RegionTravel.ShouldLoad(_currentSceneName, to))
                return;

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
                // TODO: SceneManager.UnloadSceneAsync(originScene) once the player persists across the hop.
            };
        }
    }
}
