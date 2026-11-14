using System;
using NUnit.Framework;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace HiddenHarbours.Tests.CoopSpike
{
    /// <summary>Inject real key edges without resetting the session's project-wide actions.</summary>
    internal sealed class CoopKeyboard : IDisposable
    {
        private readonly InputActionAsset _actions = InputSystem.actions;
        private readonly Keyboard _previous = Keyboard.current;
        private readonly InputSettings _settings = InputSystem.settings;
        private readonly InputSettings.BackgroundBehavior _background;
        private readonly InputSettings.UpdateMode _update;
#if UNITY_EDITOR
        private readonly InputSettings.EditorInputBehaviorInPlayMode _focus;
#endif
        public Keyboard Keyboard { get; }

        public CoopKeyboard()
        {
            Assert.That(_actions, Is.Not.Null, "The real session must retain its project-wide actions.");
            _background = _settings.backgroundBehavior;
            _update = _settings.updateMode;
#if UNITY_EDITOR
            _focus = _settings.editorInputBehaviorInPlayMode;
            _settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            _settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            _settings.updateMode = InputSettings.UpdateMode.ProcessEventsInDynamicUpdate;
            Keyboard = InputSystem.AddDevice<Keyboard>();
            Keyboard.MakeCurrent();
        }

        public void PressF() => InputSystem.QueueStateEvent(Keyboard, new KeyboardState(Key.F));

        public void ReleaseF()
        {
            InputSystem.QueueStateEvent(Keyboard, new KeyboardState());
            InputSystem.Update();
        }

        public void Dispose()
        {
            InputSystem.RemoveDevice(Keyboard);
            if (_previous != null && _previous.added) _previous.MakeCurrent();
            _settings.backgroundBehavior = _background;
            _settings.updateMode = _update;
#if UNITY_EDITOR
            _settings.editorInputBehaviorInPlayMode = _focus;
#endif
            Assert.That(InputSystem.actions, Is.SameAs(_actions),
                "A trial key edge must not clear the next single-player boot's bindings.");
        }
    }
}
