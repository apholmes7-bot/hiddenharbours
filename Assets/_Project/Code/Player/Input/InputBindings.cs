using UnityEngine;
using UnityEngine.InputSystem;
using HiddenHarbours.Core;

namespace HiddenHarbours.Player
{
    /// <summary>
    /// ⭐ <b>The bindings asset, reached from code</b> — <c>Assets/_Project/Data/Input/HiddenHarbours.inputactions</c>,
    /// the project-wide actions asset (Project Settings ▸ Input System Package ▸ Project-wide Actions;
    /// the reference lives in <c>ProjectSettings/EditorBuildSettings.asset</c> and ships as a preloaded
    /// asset). It IS the Def for bindings (rule 6): every key the game reads is declared there, per
    /// control mode, per control scheme, and an owner rebinds in the inspector with no code touched.
    ///
    /// <para><b>One map per control mode.</b> <see cref="WalkMap"/>, <see cref="DeckMap"/>,
    /// <see cref="HelmMap"/>, <see cref="DriveMap"/> and <see cref="UiMap"/> — the mode list ADR 0043
    /// fixes. A device-backed intent source finds its map here ONCE, at construction, and polls its
    /// actions every frame (<c>ReadValue</c> / <c>IsPressed</c> / <c>WasPressedThisFrame</c>): polling is
    /// the shipped shape and the tests' shape, and the Input System's callbacks are not used for the
    /// polled modes (rule 7 — no per-frame allocation, no callback plumbing).</para>
    ///
    /// <para><b>Where this lives.</b> In the Player lane with the other device reads, never in Core:
    /// <c>HiddenHarbours.Core</c> references no Input System assembly, ever (ADR 0043 §1). Core holds
    /// the intents; this is the device layer.</para>
    /// </summary>
    public static class InputBindings
    {
        public const string AssetName = "HiddenHarbours";

        public const string WalkMap = "Walk";
        public const string DeckMap = "Deck";
        public const string HelmMap = "Helm";
        public const string DriveMap = "Drive";
        public const string UiMap = "UI";

        /// <summary>The control scheme names, as the asset spells them.</summary>
        public const string KeyboardMouseScheme = "KeyboardMouse";
        public const string GamepadScheme = "Gamepad";

        private static bool _missingReported;

        /// <summary>The project-wide bindings asset, or null when none is configured — which is a broken
        /// project, reported once and loudly (a fisher who cannot walk is not a thing to be quiet about).</summary>
        public static InputActionAsset Asset
        {
            get
            {
                InputActionAsset asset = InputSystem.actions;
                if (asset == null && !_missingReported)
                {
                    _missingReported = true;
                    Debug.LogError("[InputBindings] No project-wide actions asset is configured — " +
                                   "Data/Input/" + AssetName + ".inputactions must be set under Project " +
                                   "Settings > Input System Package > Project-wide Actions. Every control " +
                                   "mode reads as nothing until it is.");
                }
                return asset;
            }
        }

        /// <summary>
        /// A control mode's map, ENABLED. Null when the asset is missing or the map is not in it (the
        /// asset pin in <c>ControlIntentSourceTests</c> reds first). Enabling is idempotent, and the
        /// polled maps stay enabled for the life of the session: a mode that is not active simply has
        /// nobody reading its map, which is what "not active" always meant for the keys.
        /// </summary>
        public static InputActionMap Map(string name)
        {
            InputActionAsset asset = Asset;
            if (asset == null) return null;
            InputActionMap map = asset.FindActionMap(name, throwIfNotFound: false);
            if (map == null)
            {
                Debug.LogError("[InputBindings] " + AssetName + ".inputactions has no '" + name + "' map.");
                return null;
            }
            if (!map.enabled) map.Enable();
            return map;
        }

        /// <summary>An action of a map, or null with the map. Found once at construction, never per frame.</summary>
        public static InputAction Action(InputActionMap map, string name)
            => map?.FindAction(name, throwIfNotFound: false);

        /// <summary>
        /// Which kind of device a control belongs to — the one place the project decides what counts
        /// as "the pad". Everything that is not a <see cref="Gamepad"/> is the keyboard-and-mouse
        /// scheme, which is what a box with nothing plugged in shows from the first frame.
        /// </summary>
        public static ControlDevice DeviceOf(InputControl control)
            => control != null && control.device is Gamepad ? ControlDevice.Gamepad : ControlDevice.KeyboardMouse;

        /// <summary>Tell Core which device just actuated <paramref name="action"/>, if any did this
        /// frame. <c>activeControl</c> is null while the action is idle, so an idle frame reports
        /// nothing and the last-used device stands.</summary>
        public static void ReportDevice(InputAction action)
        {
            InputControl control = action?.activeControl;
            if (control != null) ActiveControlDevice.Report(DeviceOf(control));
        }
    }
}
