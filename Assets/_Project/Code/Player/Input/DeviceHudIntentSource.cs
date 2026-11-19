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

        /// <summary>Fold / open the TASKS note (2026-11-16): T, and the pad's west button.</summary>
        public const string ToggleTasksAction = "ToggleTasks";

        public const string PointAction = "Point";
        public const string ClickAction = "Click";

        private InputAction _hideAll, _toggleTasks, _point, _click;
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

            bool toggleTasks = _toggleTasks != null && _toggleTasks.WasPressedThisFrame();
            if (_toggleTasks != null) InputBindings.ReportDevice(_toggleTasks);

            // The pointer is the UI map's (the same UI/Point and UI/Click the chooser reads), hit-tested
            // by the HUD's reader against the TASKS note's own box, never by a raycaster.
            bool click = _click != null && _click.WasPressedThisFrame();
            Vector2 point = _point != null ? _point.ReadValue<Vector2>() : Vector2.zero;

            return Map(hideAll, toggleTasks, click, point, ControlIntentGates.WorldStopped,
                       HelmKeyCapture.IsCapturing);
        }

        public static HudIntents Map(bool hideAll, bool worldStopped, bool keysCaptured)
            => ControlIntentGates.Apply(new HudIntents(hideAll), worldStopped, keysCaptured);

        /// <summary>The pure assembly, through the HUD's two gates.</summary>
        public static HudIntents Map(bool hideAll, bool toggleTasks, bool click, Vector2 point,
                                     bool worldStopped, bool keysCaptured)
            => ControlIntentGates.Apply(new HudIntents(hideAll, toggleTasks, click, point), worldStopped,
                                        keysCaptured);

        private void Resolve()
        {
            if (_resolved) return;
            _resolved = true;
            InputActionMap hud = InputBindings.Map(InputBindings.HudMap);
            _hideAll = InputBindings.Action(hud, HideAllAction);
            _toggleTasks = InputBindings.Action(hud, ToggleTasksAction);

            InputActionMap ui = InputBindings.Map(InputBindings.UiMap);
            _point = InputBindings.Action(ui, PointAction);
            _click = InputBindings.Action(ui, ClickAction);
        }
    }
}
