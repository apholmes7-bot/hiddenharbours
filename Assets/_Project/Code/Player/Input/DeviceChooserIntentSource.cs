using UnityEngine;
using UnityEngine.InputSystem;
using HiddenHarbours.Core;

namespace HiddenHarbours.Player
{
    /// <summary>
    /// <b>The bindings asset, as the interaction chooser's intent source</b> (owner ask 2026-11-15:
    /// "select the desired action with keys or mouse").
    ///
    /// <para><b>What it reads, and why these.</b> Everything but the number keys is an action the asset
    /// already has, so no shipped key changes meaning:
    /// <list type="bullet">
    ///   <item>Up / down: <c>UI/Navigate</c> (W/S and the arrows), the same keys a dialogue picker steers
    ///   on.</item>
    ///   <item>Confirm: <c>Walk/Interact</c> or <c>Deck/Interact</c> (E — the press that opened the list
    ///   picks from it) or <c>UI/Submit</c> (Enter, and a pad's south button through its usage
    ///   binding).</item>
    ///   <item>Cancel: <c>UI/Cancel</c> (Esc, and a pad's east button through its usage binding).</item>
    ///   <item>Mouse: <c>UI/Point</c> and <c>UI/Click</c>.</item>
    ///   <item>Number keys: <c>Chooser/Pick1</c>..<c>Pick9</c>, the one new map (1–9 were bound to nothing
    ///   in the asset or polled anywhere in gameplay, swept 2026-11-15).</item>
    /// </list>
    /// The pad (ADR 0043 PR 2, the chooser's slice): the left stick and the d-pad are
    /// <c>Chooser/PadNavigate</c>, in the asset's Gamepad scheme; the pad's south and east buttons already
    /// reach <c>UI/Submit</c> and <c>UI/Cancel</c> through their usage binding.</para>
    ///
    /// <para><b>Why the pad's up/down is its own action and not more rows on <c>UI/Navigate</c>.</b>
    /// UI/Navigate is a pass-through action the EventSystem also reads: a stick there would steer every
    /// uGUI screen too (not this slice), and a pass-through reports any raw change, so a resting stick's
    /// jitter would mask a held key and claim the pad was in hand. <c>PadNavigate</c> is a Value action:
    /// it is idle inside the stick's dead zone. The two axes are joined by <see cref="Strongest"/>, and a
    /// device is reported only for an axis actually pushed, which is what tells the view the pad is in
    /// hand so a held push may run on.</para>
    ///
    /// <para>Installed in Core's <see cref="ChooserInput"/> before the first scene, because the reader is
    /// in UI (rule 4). Actions are resolved once, on the first read; <see cref="Read"/> allocates
    /// nothing.</para>
    /// </summary>
    public sealed class DeviceChooserIntentSource : IControlIntentSource<ChooserIntents>
    {
        public const string NavigateAction = "Navigate";
        public const string SubmitAction = "Submit";
        public const string CancelAction = "Cancel";
        public const string PointAction = "Point";
        public const string ClickAction = "Click";
        public const string InteractAction = "Interact";

        /// <summary>The number-key actions are <c>Pick1</c>..<c>Pick9</c>.</summary>
        public const string PickActionPrefix = "Pick";

        /// <summary>The pad's up/down on the list (the stick and the d-pad), in the Chooser map.</summary>
        public const string PadNavigateAction = "PadNavigate";

        private InputAction _navigate, _padNavigate, _submit, _cancel, _point, _click, _walkInteract, _deckInteract;
        private readonly InputAction[] _picks = new InputAction[InteractChoiceList.HardCap];
        private bool _resolved;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Install() => ChooserInput.InstallDevice(new DeviceChooserIntentSource());

        /// <summary>Whether the asset carries the actions the list cannot work without (navigate,
        /// confirm, cancel).</summary>
        public bool IsBound
        {
            get
            {
                Resolve();
                return _navigate != null && (_submit != null || _walkInteract != null) && _cancel != null;
            }
        }

        public ChooserIntents Read()
        {
            if (!IsBound) return ChooserIntents.None;

            float keys = _navigate.ReadValue<Vector2>().y;
            float pad = _padNavigate != null ? _padNavigate.ReadValue<Vector2>().y : 0f;
            float navigate = Strongest(keys, pad);
            bool confirm = Pressed(_submit) || Pressed(_walkInteract) || Pressed(_deckInteract);
            bool cancel = Pressed(_cancel);
            bool click = Pressed(_click);
            Vector2 point = _point != null ? _point.ReadValue<Vector2>() : Vector2.zero;

            int pick = 0;
            for (int i = 0; i < _picks.Length; i++)
                if (Pressed(_picks[i])) { pick = i + 1; break; }

            // Only an axis that is actually pushed speaks for its device; the pad last, so a pushed stick is
            // this frame's device.
            if (keys != 0f) InputBindings.ReportDevice(_navigate);
            InputBindings.ReportDevice(_submit);
            InputBindings.ReportDevice(_cancel);
            if (pad != 0f) InputBindings.ReportDevice(_padNavigate);

            return Map(navigate, pick, confirm, cancel, click, point,
                       ControlIntentGates.WorldStopped, HelmKeyCapture.IsCapturing);
        }

        /// <summary>The pure assembly, through the chooser's gates.</summary>
        public static ChooserIntents Map(float navigate, int pick, bool confirm, bool cancel, bool click,
                                         Vector2 point, bool worldStopped, bool keysCaptured)
            => ControlIntentGates.Apply(new ChooserIntents(navigate, pick, confirm, cancel, click, point),
                                        worldStopped, keysCaptured);

        /// <summary>The keys' axis and the pad's, joined: whichever is pushed further wins, the keys on a
        /// tie, so a key held with the pad at rest reads exactly as it did before the pad had a row.</summary>
        public static float Strongest(float keys, float pad) => Mathf.Abs(pad) > Mathf.Abs(keys) ? pad : keys;

        private static bool Pressed(InputAction action) => action != null && action.WasPressedThisFrame();

        private void Resolve()
        {
            if (_resolved) return;
            _resolved = true;

            InputActionMap ui = InputBindings.Map(InputBindings.UiMap);
            _navigate = InputBindings.Action(ui, NavigateAction);
            _submit = InputBindings.Action(ui, SubmitAction);
            _cancel = InputBindings.Action(ui, CancelAction);
            _point = InputBindings.Action(ui, PointAction);
            _click = InputBindings.Action(ui, ClickAction);

            _walkInteract = InputBindings.Action(InputBindings.Map(InputBindings.WalkMap), InteractAction);
            _deckInteract = InputBindings.Action(InputBindings.Map(InputBindings.DeckMap), InteractAction);

            InputActionMap chooser = InputBindings.Map(InputBindings.ChooserMap);
            _padNavigate = InputBindings.Action(chooser, PadNavigateAction);
            for (int i = 0; i < _picks.Length; i++)
                _picks[i] = InputBindings.Action(chooser, PickActionPrefix + (i + 1));
        }
    }
}
