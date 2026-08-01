using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Environment;
using HiddenHarbours.Player;

namespace HiddenHarbours.App
{
    /// <summary>
    /// The composition root. Lives on a persistent object in Bootstrap.unity, wires the services
    /// into <see cref="GameServices"/>, and survives scene loads. This is the only place allowed
    /// to know about concrete services from multiple modules (tech-architecture.md §2).
    ///
    /// Setup: put this on a "GameRoot" object in Bootstrap.unity with a GameClock and an
    /// EnvironmentService component, and (optionally) a PlayerWallet, then assign them below.
    ///
    /// <para><b>Load-restore (VS-08).</b> <see cref="Awake"/> wires the services; the save is then
    /// re-applied through <see cref="SaveRestore"/> (clock seeked to the saved instant, wallet brought to
    /// the saved balance, licences granted) and <see cref="GameLoaded"/> published so the owned fleet
    /// re-grants its hull. That restore is now the last step of <see cref="ShellFlow"/>'s "enter the
    /// world", so the title page and this root drive one identical sequence. It is triggered from
    /// <c>Start</c> — not <c>Awake</c> — so it lands AFTER every scene object's <c>Awake</c> (the fleet has
    /// subscribed) and after the self-installing <c>LicenseService</c> registers (AfterSceneLoad). It runs
    /// once at launch (GameRoot is persistent), not on a region hop.</para>
    ///
    /// <para><b>The shell (M1 §7.8).</b> Boot ends at the TITLE, not in the world: <c>Start</c> hands to
    /// <see cref="ShellFlow.EnterTitle"/>, which stops the clock and leaves the save unapplied until the
    /// player picks New Game or Continue. This is ONE boot path — the title is a state of the existing
    /// persistent core, never a second scene, so the persistent-core + additive-region contract
    /// (ADR 0004) does not fork. <see cref="_bootToTitle"/> turns it off for a core that must land
    /// straight in the world (the dev/region iteration cores), which is exactly the pre-shell behaviour.</para>
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class GameRoot : MonoBehaviour
    {
        [SerializeField] private GameClock _clock;
        [SerializeField] private EnvironmentService _environment;
        [SerializeField] private PlayerWallet _wallet;

        [Tooltip("The owner's tuning asset. Optional — every block falls back to its own Default when " +
                 "unwired — but it is what carries GameConfig.WaveField to the sea, so the shader " +
                 "bridge, the hull rocking and the seakeeping forces all read the ONE derivation " +
                 "(ADR 0018 §(5)).")]
        [SerializeField] private GameConfig _config;

        [Tooltip("Boot to the TITLE (New Game / Continue / Quit) instead of straight into play — the " +
                 "M1 §7.8 shell. Untick for a core that must land in the world immediately (the dev " +
                 "region-iteration cores): the save is then applied at Start exactly as it was before " +
                 "the shell existed.")]
        [SerializeField] private bool _bootToTitle = true;

        /// <summary>The config this root actually published — which is not always <see cref="_config"/>
        /// (an unwired root borrows the clock's). Remembered at <see cref="Awake"/> because
        /// <see cref="OnDestroy"/> must know what it gave without re-reading <c>_clock.Config</c> off a
        /// component that is already going away.</summary>
        private GameConfig _publishedConfig;

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);

            GameServices.Clock = _clock;
            GameServices.Environment = _environment;
            GameServices.Wallet = _wallet;   // optional in the greybox
            // The explicit reference wins; an unwired scene borrows the clock's own config (the clock
            // cannot run without one), so a stale scene still reaches the owner's tunables rather than
            // silently running every block on its C# Default. Unity fake-null: `!= null`, never `??`.
            _publishedConfig = _config != null ? _config
                             : _clock != null ? _clock.Config : null;
            GameServices.Config = _publishedConfig;

            if (!GameServices.Ready)
                Debug.LogError("[GameRoot] Services not wired — assign a GameClock and an " +
                               "EnvironmentService in the Inspector.", this);
            else
                Debug.Log("[GameRoot] Hidden Harbours services online. Fair winds.");
        }

        private void Start()
        {
            // The shell decides when the world is entered. At the title the clock stops and the save is
            // held unapplied until the player chooses; ContinueGame() is the same load-restore this method
            // used to run inline (resumed blob or null for a fresh game, then GameLoaded either way).
            if (_bootToTitle) ShellFlow.EnterTitle();
            else ShellFlow.ContinueGame();
        }

        /// <summary>
        /// Take back exactly what this root registered — <b>no more</b>.
        ///
        /// <para>A wholesale <see cref="GameServices.Reset"/> here would also null the slots this root never
        /// filled: <see cref="GameServices.Save"/>, <see cref="GameServices.Licenses"/>,
        /// <see cref="GameServices.AudioMix"/> (the self-installing singletons) and
        /// <see cref="GameServices.CatchFactory"/> (registered once per launch at
        /// <c>BeforeSceneLoad</c>). None of them re-register for an object that SURVIVED the teardown —
        /// their bootstraps run once per launch, and their registrations happen in <c>Awake</c>/<c>OnEnable</c>,
        /// which do not run again. Quit-to-title (<see cref="ShellRestart"/>) destroys this root and keeps
        /// those alive on purpose, so a blanket reset would leave the rest of the launch with no save service
        /// at all — no Continue on the title the player just quit to, nothing able to write to disk again,
        /// and a settings sheet insisting there is no sound while the director plays on.</para>
        ///
        /// <para>So: the same "whoever registers, unregisters" symmetry the singletons already keep for
        /// themselves, <c>ReferenceEquals</c> and all — never <c>==</c>, which a destroyed
        /// <c>UnityEngine.Object</c> would satisfy against null and turn into "someone else's registration is
        /// mine to clear". The guard also means a REPLACEMENT root that has already wired itself is never
        /// stomped by the outgoing one's teardown. <see cref="GameServices.Reset"/> stays for tests, which
        /// call it explicitly.</para>
        /// </summary>
        private void OnDestroy()
        {
            if (ReferenceEquals(GameServices.Clock, _clock)) GameServices.Clock = null;
            if (ReferenceEquals(GameServices.Environment, _environment)) GameServices.Environment = null;
            if (ReferenceEquals(GameServices.Wallet, _wallet)) GameServices.Wallet = null;
            if (ReferenceEquals(GameServices.Config, _publishedConfig)) GameServices.Config = null;

            ShellFlow.Reset();   // the shell belongs to this boot; don't leave a phase behind for the next
        }
    }
}
