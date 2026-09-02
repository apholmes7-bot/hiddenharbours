using UnityEngine;
using UnityEngine.InputSystem;
using HiddenHarbours.Core;

namespace HiddenHarbours.Player
{
    /// <summary>
    /// <b>The bindings asset, as the on-foot intent source</b> — the read <c>PlayerWalkController</c>
    /// made inline off <c>Keyboard.current</c> before the seam existed (W/A/S/D and the arrows summed
    /// per axis, either Shift a sprint), moved behind <see cref="IControlIntentSource{TIntents}"/>
    /// and re-expressed as the <c>Walk</c> map of <c>HiddenHarbours.inputactions</c>.
    ///
    /// <para><b>Byte-identical to what it replaces, and pinned twice.</b> The KEYS are pinned by
    /// <c>ControlIntentSourceTests</c> reading the asset: the move is one <c>2DVector</c> composite in
    /// <b>Digital</b> mode (no normalisation — a diagonal arrives as (±1, ±1) and the controller clamps
    /// it, exactly as it clamped the summed keys) with each part OR-ing its letter and its arrow, the
    /// sprint is either Shift. The SENSE is pinned by <see cref="Map"/>, a pure function of the action
    /// values so the gates and the struct's assembly are a test and not a thing read off a screen.</para>
    ///
    /// <para><b>The gates live here</b> (<see cref="ControlIntentGates"/>), not in the consumer: a
    /// stopped world reads as <see cref="WalkIntents.None"/>, a claimed move axis as a zero move with the
    /// presses intact. No keyboard at all is the same as no key held: the composite reads zero.</para>
    ///
    /// <para><b>The device signal.</b> On a frame a bound control is actuated the source reports which
    /// device it belonged to (<see cref="ActiveControlDevice"/>); the intent it hands on does not say.
    /// With the Gamepad scheme empty (PR 0) only the keyboard can ever report; a filled scheme (PR 2)
    /// reports the pad through the very same line.</para>
    /// </summary>
    public sealed class DeviceWalkIntentSource : IControlIntentSource<WalkIntents>
    {
        public const string MoveAction = "Move";
        public const string SprintAction = "Sprint";
        public const string InteractAction = "Interact";
        public const string CancelAction = "Cancel";

        private readonly InputAction _move;
        private readonly InputAction _sprint;
        private readonly InputAction _interact;
        private readonly InputAction _cancel;

        public DeviceWalkIntentSource()
        {
            InputActionMap map = InputBindings.Map(InputBindings.WalkMap);
            _move = InputBindings.Action(map, MoveAction);
            _sprint = InputBindings.Action(map, SprintAction);
            _interact = InputBindings.Action(map, InteractAction);
            _cancel = InputBindings.Action(map, CancelAction);
        }

        /// <summary>Is every action this source needs present in the asset? False is a broken asset,
        /// already reported by <see cref="InputBindings"/>; the read then answers <c>None</c>.</summary>
        public bool IsBound => _move != null && _sprint != null && _interact != null && _cancel != null;

        public WalkIntents Read()
        {
            if (!IsBound) return WalkIntents.None;

            Vector2 move = _move.ReadValue<Vector2>();
            bool sprint = _sprint.IsPressed();
            bool interact = _interact.WasPressedThisFrame();
            bool cancel = _cancel.WasPressedThisFrame();

            InputBindings.ReportDevice(_move);
            InputBindings.ReportDevice(_sprint);
            InputBindings.ReportDevice(_interact);
            InputBindings.ReportDevice(_cancel);

            return Map(move, sprint, interact, cancel,
                       ControlIntentGates.WorldStopped, ControlIntentGates.MoveClaimed);
        }

        /// <summary>The action values, as intents, through the gates. Pure, so the sense of every value
        /// and the reach of every gate is a testable claim.</summary>
        public static WalkIntents Map(Vector2 move, bool sprint, bool interact, bool cancel,
                                      bool worldStopped, bool moveClaimed)
            => ControlIntentGates.Apply(new WalkIntents(move, sprint, interact, cancel), worldStopped, moveClaimed);
    }
}
