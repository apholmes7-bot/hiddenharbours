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
    /// <para><b>Load-restore (VS-08).</b> <see cref="Awake"/> wires the services; <see cref="Start"/> then
    /// re-applies the loaded save through <see cref="SaveRestore"/> (clock seeked to the saved instant,
    /// wallet brought to the saved balance, licences granted) and publishes <see cref="GameLoaded"/> so the
    /// owned fleet re-grants its hull. Restore runs in <c>Start</c> — not <c>Awake</c> — so it lands AFTER
    /// every scene object's <c>Awake</c> (the fleet has subscribed) and after the self-installing
    /// <c>LicenseService</c> registers (AfterSceneLoad). It runs once at launch (GameRoot is persistent), not
    /// on a region hop.</para>
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class GameRoot : MonoBehaviour
    {
        [SerializeField] private GameClock _clock;
        [SerializeField] private EnvironmentService _environment;
        [SerializeField] private PlayerWallet _wallet;

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);

            GameServices.Clock = _clock;
            GameServices.Environment = _environment;
            GameServices.Wallet = _wallet;   // optional in the greybox

            if (!GameServices.Ready)
                Debug.LogError("[GameRoot] Services not wired — assign a GameClock and an " +
                               "EnvironmentService in the Inspector.", this);
            else
                Debug.Log("[GameRoot] Hidden Harbours services online. Fair winds.");
        }

        private void Start()
        {
            // Re-apply the loaded save into the live services, then announce GameLoaded (VS-08 load-restore).
            // Only a RESUMED game (an existing save on disk) feeds its blob in — a new game passes null so its
            // authored start hour stands (a fresh blob's gameTime is 0; seeking to it would reset to midnight).
            // Either way GameLoaded fires, so subscribers (the owned fleet) have one code path.
            bool resumed = GameServices.Save != null && GameServices.Save.LoadedExistingSave;
            SaveData data = resumed ? GameServices.Save.Current : null;

            SaveRestore.ApplyToLiveServices(
                data,
                GameServices.Clock,
                GameServices.Wallet,
                GameServices.Licenses);
        }

        private void OnDestroy() => GameServices.Reset();
    }
}
