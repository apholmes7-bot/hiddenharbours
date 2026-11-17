using UnityEngine;
using UnityEngine.InputSystem;
using HiddenHarbours.Core;

namespace HiddenHarbours.Player
{
    /// <summary>
    /// <b>The bindings asset, as the autopilot switch's intent source</b>: the <c>Helm/Autopilot</c>
    /// action (P on the keyboard), read once per frame by the helm. The <see cref="DeviceHudIntentSource"/>
    /// shape: the reader is in Boats, which cannot reference Player (rule 4), so this source installs
    /// itself in Core's <see cref="AutopilotInput"/> before the first scene loads and the reader takes
    /// it from there. The action is found on the first read, not at the install.
    ///
    /// <para>The gates are the HUD's (<see cref="ControlIntentGates"/>): a stopped world and a text
    /// field that owns the keys each take the press.</para>
    /// </summary>
    public sealed class DeviceAutopilotIntentSource : IControlIntentSource<AutopilotIntents>
    {
        public const string AutopilotAction = "Autopilot";

        private InputAction _toggle;
        private bool _resolved;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Install() => AutopilotInput.InstallDevice(new DeviceAutopilotIntentSource());

        /// <summary>Whether the asset carries <c>Helm/Autopilot</c>. Resolves the action if no read
        /// has yet.</summary>
        public bool IsBound
        {
            get
            {
                Resolve();
                return _toggle != null;
            }
        }

        public AutopilotIntents Read()
        {
            if (!IsBound) return AutopilotIntents.None;

            bool toggle = _toggle.WasPressedThisFrame();
            InputBindings.ReportDevice(_toggle);

            return Map(toggle, ControlIntentGates.WorldStopped, HelmKeyCapture.IsCapturing);
        }

        public static AutopilotIntents Map(bool toggle, bool worldStopped, bool keysCaptured)
            => ControlIntentGates.Apply(new AutopilotIntents(toggle), worldStopped, keysCaptured);

        private void Resolve()
        {
            if (_resolved) return;
            _resolved = true;
            _toggle = InputBindings.Action(InputBindings.Map(InputBindings.HelmMap), AutopilotAction);
        }
    }
}
