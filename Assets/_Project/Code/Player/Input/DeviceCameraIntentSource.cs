using UnityEngine;
using UnityEngine.InputSystem;
using HiddenHarbours.Core;

namespace HiddenHarbours.Player
{
    /// <summary>
    /// <b>The bindings asset, as the camera's intent source</b>: the <c>Camera</c> map's Raise (P / the
    /// pad's Y), Shutter (Space / the right trigger) and Aim (the move keys / the left stick), read once
    /// per frame by the camera. The <see cref="DeviceAutopilotIntentSource"/> shape: installs itself in
    /// Core's <see cref="CameraInput"/> before the first scene loads; the actions are found on the first
    /// read, not at the install.
    ///
    /// <para>The gates are the HUD's (<see cref="ControlIntentGates"/>): a stopped world and a text field
    /// that owns the keys each take everything.</para>
    /// </summary>
    public sealed class DeviceCameraIntentSource : IControlIntentSource<CameraIntents>
    {
        public const string CameraMap = InputBindings.CameraMap;
        public const string RaiseAction = "Raise";
        public const string ShutterAction = "Shutter";
        public const string AimAction = "Aim";

        private InputAction _raise;
        private InputAction _shutter;
        private InputAction _aim;
        private bool _resolved;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Install() => CameraInput.InstallDevice(new DeviceCameraIntentSource());

        /// <summary>Whether the asset carries the Camera map's three actions.</summary>
        public bool IsBound
        {
            get
            {
                Resolve();
                return _raise != null && _shutter != null && _aim != null;
            }
        }

        public CameraIntents Read()
        {
            if (!IsBound) return CameraIntents.None;

            bool raise = _raise.WasPressedThisFrame();
            bool shutter = _shutter.WasPressedThisFrame();
            Vector2 aim = _aim.ReadValue<Vector2>();
            InputBindings.ReportDevice(_raise);
            InputBindings.ReportDevice(_shutter);
            InputBindings.ReportDevice(_aim);

            return Map(raise, shutter, aim, ControlIntentGates.WorldStopped, HelmKeyCapture.IsCapturing);
        }

        public static CameraIntents Map(bool raise, bool shutter, Vector2 aim, bool worldStopped, bool keysCaptured)
            => ControlIntentGates.Apply(new CameraIntents(raise, shutter, aim), worldStopped, keysCaptured);

        private void Resolve()
        {
            if (_resolved) return;
            _resolved = true;
            InputActionMap map = InputBindings.Map(CameraMap);
            _raise = InputBindings.Action(map, RaiseAction);
            _shutter = InputBindings.Action(map, ShutterAction);
            _aim = InputBindings.Action(map, AimAction);
        }
    }
}
