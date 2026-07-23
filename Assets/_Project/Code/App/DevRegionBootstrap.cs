using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.App
{
    /// <summary>
    /// DEV-ONLY bootstrap for pressing Play directly in a REGION scene (Greywick etc. — scenes that
    /// normally receive the persistent core from the origin/start scene and therefore contain no
    /// player). The owner's iteration bug: "when building the greywick scene and starting there no
    /// character loads". The scene builder bakes a minimal, self-contained DEV CORE (services + camera
    /// + a walkable, fishing-capable player at a sensible spawn) as an <b>inactive</b> root and points
    /// this component at it; on Play this component decides its fate:
    ///
    /// <list type="bullet">
    ///   <item><b>Editor, no persistent core present</b> (the scene was opened and played directly) —
    ///   the dev core is ACTIVATED (its GameRoot wires the services exactly like the real boot) and the
    ///   scene's standalone review camera is silenced in favour of the dev core's follow camera. The
    ///   owner walks and fishes immediately.</item>
    ///   <item><b>A persistent core already exists</b> (arrived via VS-22 travel from the start scene) —
    ///   the dev core is DESTROYED, still inactive. Because it never activated, none of its Awakes ran:
    ///   no service got stomped, no second player/camera ever existed, and (Unity contract) no OnDestroy
    ///   fires on never-awakened components — the live core is untouched. The real boot and travel
    ///   wiring behave exactly as if this component didn't exist.</item>
    ///   <item><b>Not the editor</b> — destroyed. This is an owner-iteration affordance, never a
    ///   shipping path.</item>
    /// </list>
    ///
    /// <para>Runs in <c>Start</c> (after every scene Awake/OnEnable, so <see cref="GameServices.Ready"/>
    /// reflects reality) and destroys itself afterwards either way. Re-run-builder-safe: the builder
    /// recreates both the dev core and this component from scratch each build.</para>
    /// </summary>
    public sealed class DevRegionBootstrap : MonoBehaviour
    {
        [Tooltip("The baked, INACTIVE dev-core root (services + camera + player). Activated only when " +
                 "playing this region scene directly in the editor; destroyed (never awakened) otherwise.")]
        [SerializeField] private GameObject _devCoreRoot;

        [Tooltip("The scene's standalone review camera — silenced when the dev core takes over (its own " +
                 "follow camera becomes the live one). Left alone on the travel path (the coordinator " +
                 "silences region cameras itself).")]
        [SerializeField] private Camera _sceneReviewCamera;

        /// <summary>The whole decision, input-free and EditMode-testable: seed the dev core only in the
        /// EDITOR and only when no persistent core is already live.</summary>
        public static bool ShouldSeed(bool coreReady, bool isEditor) => isEditor && !coreReady;

        /// <summary>Wire in one call (the scene builder / tests).</summary>
        public void Configure(GameObject devCoreRoot, Camera sceneReviewCamera)
        {
            _devCoreRoot = devCoreRoot;
            _sceneReviewCamera = sceneReviewCamera;
        }

        private void Start()
        {
            if (!ShouldSeed(GameServices.Ready, Application.isEditor))
            {
                // Arrived via travel (or a build): the dev core dies INACTIVE — its Awakes never ran, so
                // nothing was wired and nothing needs unwinding. The live persistent core is untouched.
                if (_devCoreRoot != null) Destroy(_devCoreRoot);
                Destroy(gameObject);
                return;
            }

            if (_devCoreRoot == null)
            {
                Debug.LogWarning("[DevRegionBootstrap] No dev core wired — re-run the region's scene builder.");
                Destroy(gameObject);
                return;
            }

            // The dev core's follow camera becomes the live one; quiet the standalone review camera.
            if (_sceneReviewCamera != null)
            {
                _sceneReviewCamera.enabled = false;
                var listener = _sceneReviewCamera.GetComponent<AudioListener>();
                if (listener != null) listener.enabled = false;
            }

            _devCoreRoot.SetActive(true);   // GameRoot wires GameServices; the player wakes at the dev spawn
            Debug.Log("[DevRegionBootstrap] No persistent core in play — seeded the DEV core for this " +
                      "region scene (editor-only). Walk with WASD, fish with Space/left-mouse. Start from " +
                      "the St Peters scene for the real boot.");
            Destroy(gameObject);
        }
    }
}
