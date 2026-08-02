using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using HiddenHarbours.Core;

namespace HiddenHarbours.App
{
    /// <summary>
    /// Puts the world back to its boot state when the player quits to the title (M1 §7.8).
    ///
    /// <para><b>Why anything has to happen at all.</b> A title page drawn over a live session is a trap:
    /// New Game from it would mint a fresh save onto a world where the boat is three miles out with a hold
    /// full of fish and the player is standing on a wharf in another region. Going back to the title has to
    /// mean going back to the state the game boots in — which means rebuilding the persistent core, which
    /// only App can do (rule 4: Core asks, App carries it out).</para>
    ///
    /// <para><b>How, exactly.</b> Destroy every root marked <see cref="PersistentObject"/> — the services
    /// root (which carries <c>GameRoot</c> and the HUD), the player, the boat, the camera, the control
    /// switcher, the region loader, the travel coordinator — let their <c>OnDestroy</c>s run (GameRoot's
    /// clears <see cref="GameServices"/>), then reload the BOOT scene single, which unloads every
    /// additively-loaded region with it. The rebuilt core's <c>GameRoot.Start</c> then calls
    /// <see cref="ShellFlow.EnterTitle"/> like any other launch: <b>one path to the title</b>, not a second
    /// one that has to be kept in step.</para>
    ///
    /// <para><b>What deliberately survives:</b> the self-installing service singletons — <c>SaveService</c>
    /// (holding the game we just wrote), <c>AudioDirector</c> (so the mix does not blink), the licence
    /// wallet (which re-seeds itself on the next <c>GameLoaded</c>), the shell's own presenter and the
    /// EventSystem. None of them are scene objects, and none would ever come back: their
    /// <c>RuntimeInitializeOnLoadMethod</c> bootstraps run once per launch, not once per scene load.</para>
    ///
    /// <para>Self-installing and NOT itself a <see cref="PersistentObject"/> — it has to outlive the very
    /// teardown it is running.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ShellRestart : MonoBehaviour
    {
        private static ShellRestart _instance;

        /// <summary>The scene the game booted into — captured at install, because by the time anyone quits
        /// to the title the ACTIVE scene is whichever region they sailed to.</summary>
        private int _bootSceneBuildIndex = -1;

        private bool _running;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;
            var go = new GameObject("[ShellRestart]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<ShellRestart>();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;

            _bootSceneBuildIndex = SceneManager.GetActiveScene().buildIndex;
            EventBus.Subscribe<ReturnToTitleRequested>(OnReturnToTitle);
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<ReturnToTitleRequested>(OnReturnToTitle);
            if (_instance == this) _instance = null;
        }

        private void OnReturnToTitle(ReturnToTitleRequested e)
        {
            if (_running) return;   // a double-click on "Quit to title" is one quit

            // buildIndex is -1 for a scene that is not in the build settings — a PlayMode test's generated
            // scene, or an unlisted scene opened by hand. There is then no boot scene to go back to, and
            // silently tearing the core down would leave an empty world with no way out. Say so and stay.
            if (_bootSceneBuildIndex < 0)
            {
                Debug.LogWarning("[ShellRestart] No boot scene in the build settings to return to — " +
                                 "staying in the world. (Add the start scene to File ▸ Build Settings.)");
                return;
            }

            _running = true;
            StartCoroutine(ReturnToTitleRoutine());
        }

        private IEnumerator ReturnToTitleRoutine()
        {
            DestroyPersistentCore();

            // Destroy() resolves at end of frame; wait for it so every OnDestroy has run (GameRoot's clears
            // GameServices and the shell phase) BEFORE the replacement core's Awake wires new ones. Loading
            // first would let the outgoing root's teardown null the incoming root's services.
            yield return null;

            SceneManager.LoadScene(_bootSceneBuildIndex, LoadSceneMode.Single);
            _running = false;
        }

        /// <summary>Destroy every persistent root. Public + static so the teardown can be exercised
        /// without a scene reload; returns how many roots it took down.</summary>
        public static int DestroyPersistentCore()
        {
            PersistentObject[] roots = FindObjectsByType<PersistentObject>(
                FindObjectsInactive.Include);

            int destroyed = 0;
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i] == null) continue;
                Destroy(roots[i].gameObject);
                destroyed++;
            }
            return destroyed;
        }
    }
}
