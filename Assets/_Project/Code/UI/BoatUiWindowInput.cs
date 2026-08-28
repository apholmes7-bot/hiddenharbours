using UnityEngine;
using UnityEngine.InputSystem;
using HiddenHarbours.Core;

namespace HiddenHarbours.UI
{
    /// <summary>
    /// The HIDE-ALL key (2026-08-07 windowing ruling: "one input hides/restores every boat-UI window
    /// at once"). One press hides the helm card, every expanded instrument and the watch face; the
    /// next press restores them all — and clears any INDIVIDUAL hides too, which is the guaranteed
    /// recovery path back to a window (the watch included) whose own chrome is no longer on screen.
    ///
    /// <para><b>The binding: M</b> — swept free on 2026-08-07 against every <c>Key.*</c> read in the
    /// project (WASD/arrows helm, Space brace/haul, E interact, Q mooring, T trap-drop,
    /// G grant, H haul, Y auto-yaw, L spotlight/ice, F fleet/freezer/bucket, V variant, I icebox,
    /// O displaced-water, N tide table, R anchor/rode, X dump-spoiled, Z neutral, K brow-cycle,
    /// J/U spike rig, C/1/2/Enter/LeftShift in InputSystem_Actions). Serialized so the owner can
    /// rebind without touching code (the TidePanelInput courtesy).</para>
    ///
    /// <para>Deaf while a text field owns the keyboard (<see cref="HelmKeyCapture"/> — "M" belongs
    /// in a waypoint name) and while the shell has the world stopped
    /// (<see cref="ShellFlow.WorldInputBlocked"/> — the pause menu is not the place to relayout the
    /// helm). Self-installing (the HelmOverlayHost pattern), headless-safe, and inert with no
    /// keyboard.</para>
    /// </summary>
    public sealed class BoatUiWindowInput : MonoBehaviour
    {
        [Tooltip("Hides/restores every boat-UI window at once (the restore also clears individual " +
                 "hides). M — audited free of every other binding in the project on 2026-08-07. " +
                 "Rebindable here without touching code.")]
        [SerializeField] private Key _hideAllKey = Key.M;

        private static BoatUiWindowInput _instance;

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
            var kb = Keyboard.current;
            if (kb == null) return;
            if (kb[_hideAllKey].wasPressedThisFrame) BoatUiWindows.ToggleHideAll();
        }
    }
}
