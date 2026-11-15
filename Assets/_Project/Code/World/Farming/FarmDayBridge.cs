using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.World
{
    /// <summary>
    /// <b>The farm's day tick</b> (farm slice 1). Hands each new day to <see cref="IFarmService.Observe"/>,
    /// which publishes <see cref="FarmPlotStageChanged"/> for every plot whose stage moved.
    ///
    /// <para><b>Once a day, never per frame (rule 7).</b> A plot's stage changes only at midnight, so this has
    /// no <c>Update</c>: it listens for <see cref="DayStarted"/> (published by the clock once per midnight,
    /// including every midnight a rest sleeps through) and <see cref="GameLoaded"/> (a restore SEEKS the clock
    /// without replaying the days, so a load re-baselines silently instead of announcing). The
    /// foliage snow bridge's self-installing host: nobody places anything, and no scene
    /// is touched.</para>
    ///
    /// <para>With no clock (EditMode, pre-boot, an art scene) it observes nothing.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FarmDayBridge : MonoBehaviour
    {
        private static bool _installed;
        private static bool _listening;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _listening = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            if (_installed) return;
            _installed = true;
            var host = new GameObject("FarmDayBridge") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(host);
            host.AddComponent<FarmDayBridge>();
        }

        private void OnEnable()
        {
            Listen();
            Baseline();
        }

        private void OnDisable() => StopListening();

        /// <summary>Subscribe the day events, once. Internal so a test can drive the event path without a frame.</summary>
        internal static void Listen()
        {
            if (_listening) return;
            _listening = true;
            EventBus.Subscribe<DayStarted>(OnDayStarted);
            EventBus.Subscribe<GameLoaded>(OnGameLoaded);
        }

        internal static void StopListening()
        {
            if (!_listening) return;
            _listening = false;
            EventBus.Unsubscribe<DayStarted>(OnDayStarted);
            EventBus.Unsubscribe<GameLoaded>(OnGameLoaded);
        }

        private static void OnDayStarted(DayStarted _)
        {
            IGameClock clock = GameServices.Clock;
            if (clock != null) GameServices.Farm.Observe(clock.DayIndex, announce: true);
        }

        private static void OnGameLoaded(GameLoaded _) => Baseline();

        private static void Baseline()
        {
            IGameClock clock = GameServices.Clock;
            if (clock != null) GameServices.Farm.Observe(clock.DayIndex, announce: false);
        }
    }
}
