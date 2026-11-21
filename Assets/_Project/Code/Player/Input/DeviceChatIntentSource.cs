using HiddenHarbours.Core;
using UnityEngine.InputSystem;

namespace HiddenHarbours.Player
{
    /// <summary>ADR 0043 chat edges from the existing UI Submit/Cancel bindings (Enter/Escape).</summary>
    public sealed class DeviceChatIntentSource : IControlIntentSource<ChatIntents>
    {
        private readonly InputAction _submit, _cancel;

        public DeviceChatIntentSource()
        {
            var map = InputBindings.Map(InputBindings.UiMap);
            _submit = InputBindings.Action(map, "Submit");
            _cancel = InputBindings.Action(map, "Cancel");
        }

        // The owning presenter decides focus/availability before polling this source once per frame.
        public ChatIntents Read()
        {
            bool submit = _submit?.WasPressedThisFrame() ?? false;
            bool cancel = _cancel?.WasPressedThisFrame() ?? false;
            if (submit) InputBindings.ReportDevice(_submit);
            if (cancel) InputBindings.ReportDevice(_cancel);
            return new ChatIntents(submit, cancel);
        }
    }
}
