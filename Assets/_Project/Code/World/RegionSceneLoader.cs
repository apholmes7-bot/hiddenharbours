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

            // ⭐ PRELOADED (PR 1223): the scene is already read in, held one step short of switching on.
            // The hop only lets it switch on; the completion handler laid down by Preload makes it active.
            if (_held != null && string.Equals(_held.SceneName, to.SceneName, System.StringComparison.Ordinal))
            {
                _held.TravelRequested = true;
                _held.Op.allowSceneActivation = true;
                return true;
            }
            // A preload of some OTHER region would block this load (Unity runs nothing behind a held
            // scene): let it finish out of sight first, then travel.
            if (_held != null)
            {
                _travelAfterRelease = to;
                Release();
                return true;
            }

            // Already loaded (re-visit) → re-activate it, don't load a duplicate.
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
            if (!Application.CanStreamedLevelBeLoaded(to.SceneName))
            {
                Debug.LogWarning($"[RegionSceneLoader] Could not load scene '{to.SceneName}' for " +
                                 $"{to.Id} — it is not in the build profile. Is the region built and " +
                                 "added to Build Settings?", this);
                return false;
            }

            var op = SceneManager.LoadSceneAsync(to.SceneName, LoadSceneMode.Additive);
            if (op == null)
            {
                // Belt and braces — the check above should have taken this, but a null op is still a
                // travel that did not happen, and the caller has to be told so it can take its key back.
                Debug.LogWarning($"[RegionSceneLoader] Could not load scene '{to.SceneName}' for " +
                                 $"{to.Id} — is it built and in Build Settings?", this);
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

        // ---- preload (PR 1223: the first-entry freeze) -------------------------------------------------

        /// <summary>A neighbour's scene read in ahead of the hop and held one step short of switching on.</summary>
        sealed class HeldLoad
        {
            public string SceneName;
            public AsyncOperation Op;
            public float Since;            // unscaled time the hold began
            public float HoldSeconds;      // how long it may wait for the hop
            public bool TravelRequested;   // the hop asked for it: make it active when it lands
        }

        HeldLoad _held;
        RegionDef _travelAfterRelease;
        readonly HashSet<string> _unloadable = new HashSet<string>();

        /// <summary>The scene being held for a hop, or null.</summary>
        public string HeldSceneName => _held?.SceneName;

        /// <summary>
        /// ⭐ <b>Start reading a neighbour in before the hop</b> (PR 1223). Loads <paramref name="to"/>'s scene
        /// additively and asynchronously but holds it short of activation
        /// (<c>allowSceneActivation = false</c>), so the crossing has only to switch it on. No-op (false)
        /// when it is the active scene, already loaded, already held, not in the build profile, or another
        /// scene is being held or loaded.
        ///
        /// <para>⚠ <b>A held scene blocks every other scene load and unload</b> until it is let go — that is
        /// Unity's rule, not ours. So the hold is bounded: <see cref="Travel"/> elsewhere lets it go first,
        /// the loader's own tick lets it go after <paramref name="holdSeconds"/>, and so does this loader
        /// being disabled. A scene let go without a hop is switched on, its roots put straight back to sleep
        /// and the region the player is in re-woken (<see cref="ParkReleased"/>), so it is kept warm, exactly
        /// as a region already visited is.</para>
        /// </summary>
        public bool Preload(RegionDef to, float holdSeconds)
        {
            if (to == null || string.IsNullOrEmpty(to.SceneName) || _held != null || _travelAfterRelease != null) return false;
            if (!RegionTravel.ShouldLoad(SceneManager.GetActiveScene().name, to)) return false;
            Scene existing = SceneManager.GetSceneByName(to.SceneName);
            if (existing.IsValid()) return false;   // loaded (a visited region) or already on its way in
            if (_unloadable.Contains(to.SceneName)) return false;
            if (!Application.CanStreamedLevelBeLoaded(to.SceneName))
            {
                _unloadable.Add(to.SceneName);      // asked once: the travel itself will say why, as before
                return false;
            }

            AsyncOperation op = SceneManager.LoadSceneAsync(to.SceneName, LoadSceneMode.Additive);
            if (op == null) { _unloadable.Add(to.SceneName); return false; }
            op.allowSceneActivation = false;
            var held = new HeldLoad { SceneName = to.SceneName, Op = op, Since = Time.unscaledTime,
                                      HoldSeconds = Mathf.Max(0f, holdSeconds) };
            _held = held;
            op.completed += _ => OnHeldLanded(held);
            return true;
        }

        /// <summary>The hold is the loader's own to bound — not its caller's — so a preload is let go on
        /// time even if whatever asked for it has been switched off (a fixture's teardown, a shell exit).</summary>
        void Update()
        {
            if (_held == null || _held.TravelRequested) return;
            if (Time.unscaledTime - _held.Since >= _held.HoldSeconds) Release();
        }

        void Release()
        {
            if (_held == null || _held.TravelRequested) return;
            _held.Op.allowSceneActivation = true;   // it lands as a parked region (OnHeldLanded)
        }

        void OnHeldLanded(HeldLoad held)
        {
            if (ReferenceEquals(_held, held)) _held = null;
            Scene scene = SceneManager.GetSceneByName(held.SceneName);
            if (held.TravelRequested)
            {
                if (scene.IsValid() && scene.isLoaded) SceneManager.SetActiveScene(scene);
                _currentSceneName = held.SceneName;
            }
            else ParkReleased(scene);

            if (_travelAfterRelease != null)
            {
                RegionDef next = _travelAfterRelease;
                _travelAfterRelease = null;
                Travel(next);
            }
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

        void OnDisable() => Release();
    }
}
