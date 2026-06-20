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

        private void OnDestroy() => GameServices.Reset();
    }
}
