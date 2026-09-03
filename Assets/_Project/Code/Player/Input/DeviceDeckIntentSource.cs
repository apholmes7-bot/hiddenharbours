using UnityEngine;
using UnityEngine.InputSystem;
using HiddenHarbours.Core;

namespace HiddenHarbours.Player
{
    /// <summary>
    /// <b>The bindings asset, as the aboard-on-foot intent source</b> — the read <c>DeckWalkController</c>
    /// and the arrival's cabin walk each made inline off <c>Keyboard.current</c> (the same four letters
    /// and four arrows as the walk, no sprint), moved behind
    /// <see cref="IControlIntentSource{TIntents}"/> and re-expressed as the <c>Deck</c> map of
    /// <c>HiddenHarbours.inputactions</c>. Two readers, one source, one map: the deck and the cabin are
    /// the same walk with a different floor, and the arrival's own doc said so.
    ///
    /// <para>Everything <see cref="DeviceWalkIntentSource"/> says about identity, the gates and the
    /// device signal holds here; the composite, its mode and its parts are pinned by the same test.</para>
    /// </summary>
    public sealed class DeviceDeckIntentSource : IControlIntentSource<DeckIntents>
    {
        public const string MoveAction = "Move";
        public const string InteractAction = "Interact";

        private readonly InputAction _move;
        private readonly InputAction _interact;

        public DeviceDeckIntentSource()
        {
            InputActionMap map = InputBindings.Map(InputBindings.DeckMap);
            _move = InputBindings.Action(map, MoveAction);
            _interact = InputBindings.Action(map, InteractAction);
        }

        public bool IsBound => _move != null && _interact != null;

        public DeckIntents Read()
        {
            if (!IsBound) return DeckIntents.None;

            Vector2 move = _move.ReadValue<Vector2>();
            bool interact = _interact.WasPressedThisFrame();

            InputBindings.ReportDevice(_move);
            InputBindings.ReportDevice(_interact);

            return Map(move, interact, ControlIntentGates.WorldStopped, ControlIntentGates.MoveClaimed);
        }

        public static DeckIntents Map(Vector2 move, bool interact, bool worldStopped, bool moveClaimed)
            => ControlIntentGates.Apply(new DeckIntents(move, interact), worldStopped, moveClaimed);
    }
}
