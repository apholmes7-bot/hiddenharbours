using System.Collections.Generic;
using HiddenHarbours.Core;
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
    /// SCOPE (VS-22): the load mechanism for the additive Cove↔Nine Mile Creek hop. Carrying the player across +
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
            if (_regions != null)
                foreach (var r in _regions)
                    if (r != null) GameServices.RegisterSwellClimate(r.Id, r.SwellClimate, r.SwellShelter);
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
        ///
        /// <para>Returns TRUE when a crossing actually began — the region was re-activated, or an additive
        /// load was started — and FALSE when it was declined (already there, unknown/unbuilt scene). The
        /// caller needs to know, because a declined travel raises no <c>activeSceneChanged</c>: anything a
        /// passage published for the arrival it expected (its arrival key) has to be taken back rather
        /// than left standing for the next one.</para>
        /// </summary>
        public bool Travel(RegionDef to)
        {
            // The active scene, never the cached name: another region's loader may have brought us home.
            if (!RegionTravel.ShouldLoad(SceneManager.GetActiveScene().name, to))
                return false;

            if (!RegionBuildAvailability.IsAvailable(to.Id, to.SceneName))
            {
                Refuse(to);
                return false;
            }

            // ⭐ PRELOAD STILL ON ITS WAY IN (PR 1223): the crossing came before the warm-up finished. Do not
            // start a second copy; the preload's completion makes it the active scene instead of parking it.
            if (_preloading != null && string.Equals(_preloading.SceneName, to.SceneName, System.StringComparison.Ordinal))
            {
                _preloading.TravelRequested = true;
                return true;
            }

            // Already loaded (re-visit, or warmed by Preload) → re-activate it, don't load a duplicate.
            var existing = SceneManager.GetSceneByName(to.SceneName);
            if (existing.IsValid() && existing.isLoaded)
            {
                SceneManager.SetActiveScene(existing);
                _currentSceneName = to.SceneName;
                return true;
            }

            // ⚠ ASK BEFORE ASKING THE ENGINE. SceneManager.LoadSceneAsync logs an ERROR of its own
            // for a scene that is not in the build profile and THEN hands back null — so a region
            // whose scene has not been built yet sprays the console every time a boat crosses its
            // seam, and this method's tidy "declined" is buried under an engine error the caller
            // cannot suppress. Asking first makes the whole refusal ours: one warning, no error.
            //
            // This is not hypothetical. A region's DOOR and its SCENE can legitimately land in
            // different PRs — St Peters' east door shipped one merge ahead of the east water, which
            // is the case that found this — and a seam that is merely not-built-yet must read as a
            // soft edge rather than as a fault (rule 10).
            var op = StartAdditiveLoad(to.SceneName);
            if (op == null)
            {
                // Belt and braces — the check above should have taken this, but a null op is still a
                // travel that did not happen, and the caller has to be told so it can take its key back.
                Refuse(to);
                return false;
            }

            string loaded = to.SceneName;
            op.completed += _ =>
            {
                var scene = SceneManager.GetSceneByName(loaded);
                if (scene.IsValid() && scene.isLoaded) SceneManager.SetActiveScene(scene);
                _currentSceneName = loaded;
            };
            return true;
        }

        void Refuse(RegionDef to)
        {
            EventBus.Publish(new RegionTravelRefused(to.Id, to.SceneName));
            Debug.LogWarning($"[RegionSceneLoader] Could not load scene '{to.SceneName}' for " +
                             $"{to.Id} — it is unavailable in this build.", this);
        }

        /// <summary>
        /// The one place this loader asks the engine for a scene: an additive async load. In the EDITOR only, the
        /// main thread is then held inside this frame until the load's threaded file read is done
        /// (<see cref="SceneLoadHold.HoldUntilRead(AsyncOperation)"/>, bounded by
        /// <see cref="SceneLoadHold.CapSeconds"/>), because a delayed Destroy of a sprite on the persistent hosts
        /// during that read can deadlock the editor (an engine lock-order bug in the editor-only asset-change
        /// notifier). Player builds compile the hold out and behave exactly as before.
        ///
        /// <para>What changes in the editor: the read, which used to overlap a few hundred milliseconds of normal
        /// frames, now happens inside this one frame, so that frame is longer and the scene activates (and
        /// <c>completed</c> fires) after fewer frames. The ORDER is unchanged: the load still activates on a
        /// later frame, never inside this call, so callers see the same sequence of events, and the seam fade
        /// is held black (<c>Arriving</c>) across a crossing's load and does not step on the long frame.</para>
        /// </summary>
        static AsyncOperation StartAdditiveLoad(string sceneName)
        {
            AsyncOperation op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
#if UNITY_EDITOR
            op = SceneLoadHold.HoldUntilRead(op);
#endif
            return op;
        }

        // ---- preload (PR 1223: the first-entry freeze) -------------------------------------------------

        /// <summary>A neighbour's scene on its way in ahead of the hop.</summary>
        sealed class PreloadingScene
        {
            public string SceneName;
            public bool TravelRequested;   // the hop came first: make it active when it lands, don't park it
        }

        PreloadingScene _preloading;

        /// <summary>The scene a preload is still bringing in, or null.</summary>
        public string PreloadingSceneName => _preloading?.SceneName;

        /// <summary>
        /// ⭐ <b>Warm a neighbour before the hop</b> (PR 1223). Loads <paramref name="to"/>'s scene additively
        /// and asynchronously, and when it lands PARKS it: its roots are put to sleep before it ever renders
        /// and the region the player is in re-announces itself (<see cref="ParkReleased"/>). The crossing then
        /// finds it loaded and only re-activates it, the same cheap toggle as a return to a region already
        /// visited. No-op (false) when it is the active scene, already loaded or on its way in, not in the
        /// build profile, or another preload is still running.
        ///
        /// <para>⚠ <b>Why it is not held short of activation.</b> The first cut held the load at
        /// <c>allowSceneActivation = false</c> so the hop had only to "switch it on". Measured (PR 1223 fix
        /// pass), switching on IS the cost: integrating the scene and running every component's Awake and
        /// OnEnable took 2.4 s of a 2.5 s hop frame in Nine Mile Creek, and a held load does that work on the
        /// release frame, i.e. on the seam. A held load also blocks every other scene load and unload until
        /// it is released. Letting it activate on arrival and parking it moves that whole frame off the seam
        /// to the moment the mover comes within <c>GameConfig.RegionPreloadDistanceMetres</c> of it, and
        /// blocks nothing.</para>
        /// </summary>
        public bool Preload(RegionDef to)
        {
            if (to == null || string.IsNullOrEmpty(to.SceneName) || _preloading != null) return false;
            if (!RegionTravel.ShouldLoad(SceneManager.GetActiveScene().name, to)) return false;
            if (!RegionBuildAvailability.IsAvailable(to.Id, to.SceneName)) return false;
            Scene existing = SceneManager.GetSceneByName(to.SceneName);
            if (existing.IsValid()) return false;   // loaded (a visited region) or already on its way in

            AsyncOperation op = StartAdditiveLoad(to.SceneName);
            if (op == null) return false;
            var preloading = new PreloadingScene { SceneName = to.SceneName };
            _preloading = preloading;
            op.completed += _ => OnPreloadLanded(preloading);
            return true;
        }

        void OnPreloadLanded(PreloadingScene preloading)
        {
            if (ReferenceEquals(_preloading, preloading)) _preloading = null;
            Scene scene = SceneManager.GetSceneByName(preloading.SceneName);
            if (preloading.TravelRequested)
            {
                if (scene.IsValid() && scene.isLoaded) SceneManager.SetActiveScene(scene);
                _currentSceneName = preloading.SceneName;
            }
            else ParkReleased(scene);
        }

        /// <summary>
        /// A scene that switched on without being travelled to: put its roots to sleep at once (before it
        /// ever renders), then re-wake the ACTIVE region's roots so everything the newcomer's components
        /// announced on enable (the current region, its bounds, shared shader state) is announced again by
        /// the region the player is actually in. The same toggle a hop does, in place.
        /// </summary>
        static void ParkReleased(Scene parked)
        {
            if (!parked.IsValid() || !parked.isLoaded) return;
            foreach (GameObject go in parked.GetRootGameObjects()) go.SetActive(false);
            Scene active = SceneManager.GetActiveScene();
            if (!active.IsValid() || !active.isLoaded || active == parked) return;
            var awake = new List<GameObject>();
            foreach (GameObject go in active.GetRootGameObjects())
                if (go.activeSelf) { go.SetActive(false); awake.Add(go); }
            foreach (GameObject go in awake) go.SetActive(true);
        }
    }
}
