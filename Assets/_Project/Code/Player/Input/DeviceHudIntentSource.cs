using UnityEngine;
using UnityEngine.InputSystem;
using HiddenHarbours.Core;

namespace HiddenHarbours.Player
{
    /// <summary>
    /// <b>The bindings asset, as the HUD's intent source</b> (H2) — the hide-all press
    /// <c>BoatUiWindowInput</c> read inline off the keyboard (M), moved behind
    /// <see cref="IControlIntentSource{TIntents}"/> and re-expressed as the <c>Hud</c> map of
    /// <c>HiddenHarbours.inputactions</c>. One map, one action, read in every control mode: the watch
    /// face shows ashore and the hide-all restore is its recovery, so the press is not any one mode's.
    ///
    /// <para><b>Installed, not constructed by its reader.</b> The reader is in UI, which cannot
    /// reference Player (rule 4), so this source installs itself in Core's <see cref="HudInput"/> before
    /// the first scene loads, and the reader takes it from there. The action is found once, on the
    /// first read rather than at the install: the install runs before any scene, and the asset is not
    /// asked for until a frame needs it.</para>
    ///
    /// <para>The gates are the HUD's (<see cref="ControlIntentGates"/>): a stopped world and a text
    /// field that owns the keys each take the press.</para>
    /// </summary>
    public sealed class DeviceHudIntentSource : IControlIntentSource<HudIntents>
    {
        public const string HideAllAction = "HideAll";

        private InputAction _hideAll;
        private bool _resolved;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Install() => HudInput.InstallDevice(new DeviceHudIntentSource());

        /// <summary>Whether the asset carries <c>Hud/HideAll</c>. Resolves the action if no read has
        /// yet.</summary>
        public bool IsBound
        {
            get
            {
                Resolve();
                return _hideAll != null;
            }
        }

        public HudIntents Read()
        {
            if (!IsBound) return HudIntents.None;

            bool hideAll = _hideAll.WasPressedThisFrame();
            InputBindings.ReportDevice(_hideAll);

            return Map(hideAll, ControlIntentGates.WorldStopped, HelmKeyCapture.IsCapturing);
        }

        public static HudIntents Map(bool hideAll, bool worldStopped, bool keysCaptured)
            => ControlIntentGates.Apply(new HudIntents(hideAll), worldStopped, keysCaptured);

        private void Resolve()
        {
            if (_resolved) return;
            _resolved = true;
            _hideAll = InputBindings.Action(InputBindings.Map(InputBindings.HudMap), HideAllAction);
        }
    }
}
