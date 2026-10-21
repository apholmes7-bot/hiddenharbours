using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.UI
{
    /// <summary>
    /// The HIDE-ALL press (2026-08-07 windowing ruling: "one input hides/restores every boat-UI window
    /// at once"). One press hides the helm band, any raised instrument card and the watch face; the
    /// next press restores them all — and clears any INDIVIDUAL hide too (the watch's ×), which is the
    /// guaranteed recovery path back to a window whose own chrome is no longer on screen.
    ///
    /// <para><b>The binding: M</b>, in the <c>Hud</c> map of <c>HiddenHarbours.inputactions</c> (H2;
    /// ADR 0043), read through Core's <see cref="HudInput"/> because the device layer is Player's and
    /// this reader is UI's. M was swept free on 2026-08-07 against every key the project read, and the
    /// asset is now the ledger of every declared key. The owner rebinds it there.</para>
    ///
    /// <para>Deaf while a text field owns the keyboard (<see cref="HelmKeyCapture"/>: "M" belongs in
    /// a waypoint name) and while the shell has the world stopped
    /// (<see cref="ShellFlow.WorldInputBlocked"/>: the pause menu is not the place to relayout the
    /// helm). The asset's source applies the same two gates; they stand here too, so a scripted source
    /// is held to them as the keys are. Self-installing (the HelmOverlayHost pattern), headless-safe,
    /// and inert with no source.</para>
    /// </summary>
    public sealed class BoatUiWindowInput : MonoBehaviour
    {
        private static BoatUiWindowInput _instance;
        private static IControlIntentSource<HudIntents> _configured;

        /// <summary>
        /// Where the press comes from: the bindings asset's source, as the device layer installed it in
        /// <see cref="HudInput"/>, until a test or a scripted driver hands in another
        /// (<see cref="ConfigureHudInput"/>). Static, like the reader: there is one HUD.
        /// </summary>
        public static IControlIntentSource<HudIntents> HudInputSource => _configured ?? HudInput.Device;

        /// <summary>Hand the reader another source; null restores the asset's.</summary>
        public static void ConfigureHudInput(IControlIntentSource<HudIntents> source) => _configured = source;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => _configured = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;
            var go = new GameObject("BoatUiWindowInput");
            _instance = go.AddComponent<BoatUiWindowInput>();
            DontDestroyOnLoad(go);
        }

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private void Update()
        {
            if (HelmKeyCapture.IsCapturing) return;    // typing a name, not asking for a clean sea
            if (ShellFlow.WorldInputBlocked) return;   // title page / pause menu own the keyboard
            IControlIntentSource<HudIntents> source = HudInputSource;
            if (source == null) return;
            if (source.Read().HideAll) BoatUiWindows.ToggleHideAll();
        }
    }
}
