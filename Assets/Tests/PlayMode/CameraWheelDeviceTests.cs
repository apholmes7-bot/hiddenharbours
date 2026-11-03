#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using HiddenHarbours.App;
using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.PlayMode
{
    public class CameraWheelDeviceTests
    {
        sealed class MemorySave : ISaveService
        {
            readonly Dictionary<string, bool> _flags = new Dictionary<string, bool>();
            public SaveData Current { get; } = new SaveData();
            public bool GetFlag(string key) => _flags.TryGetValue(key, out bool value) && value;
            public void SetFlag(string key, bool value) => _flags[key] = value;
            public void Save() { }
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator TheDeviceReachesTheWalkingAndAboardReader()
        {
            var settings = InputSystem.settings;
            var oldFocus = settings.editorInputBehaviorInPlayMode;
            var oldBackground = settings.backgroundBehavior;
            var oldUpdate = settings.updateMode;
            var mouse = InputSystem.AddDevice<Mouse>();
            string evidence = System.Environment.GetEnvironmentVariable("CAMERA_EVIDENCE");
            using TextWriter rows = string.IsNullOrEmpty(evidence) ? new StringWriter()
                : new StreamWriter(Path.Combine(evidence, "device-gates.csv"));
            rows.WriteLine("row,frame,stage,phase,update_type,focused,editor_input,background,mouse_current,mouse_enabled,scroll,pending,delivered,wheel_enabled,modal,mode_known,mode,framing_known,framing,wheel_live,reader_active,reader_wheel,reader_camera,carry,units,step,band,tween,framing_ortho,lens_height,pixel_perfect");
            GameObject rig = null;
            try
            {
                GameServices.Reset();
                InteractionGate.Reset();
                rig = new GameObject("Device camera");
                var camera = rig.AddComponent<Camera>();
                camera.orthographic = true;
                camera.aspect = 16f / 9f;
                var follow = rig.AddComponent<CameraFollow>();
                follow.OnControlModeChanged(new ControlModeChanged(ControlMode.OnFoot));
                follow.TickZoom(100d);
                var reader = rig.AddComponent<CameraZoomInput>();
                var monitor = rig.AddComponent<WheelGateMonitor>();
                monitor.Begin(mouse, follow, reader, camera, rows);
                monitor.Phase = "original-focus";
                monitor.Arm(1f);
                for (int i = 0; i < 6; i++) yield return null;
                monitor.Record("original-delivery-result");

                settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                settings.updateMode = InputSettings.UpdateMode.ProcessEventsInDynamicUpdate;
                InputSystem.EnableDevice(mouse);
                mouse.MakeCurrent();
                monitor.Phase = "player-focus";
                foreach (bool aboard in new[] { false, true })
                {
                    follow.OnActiveBoatChanged(new ActiveBoatChanged("boat.punt", 17f));
                    follow.OnControlModeChanged(new ControlModeChanged(aboard ? ControlMode.Aboard : ControlMode.OnFoot));
                    follow.TickZoom(aboard ? 300d : 200d);
                    yield return new WaitForSecondsRealtime(0.5f);
                    foreach (float delta in new[] { 1f, -1f, -1f, 1f })
                    {
                        monitor.Phase = aboard ? "rig-aboard" : "rig-land";
                        monitor.Arm(delta);
                        for (int i = 0; i < 12 && !monitor.Delivered; i++) yield return null;
                        Assert.IsTrue(monitor.Delivered, "the queued wheel event never reached the device during a player update; see device-gates.csv");
                        yield return new WaitForSecondsRealtime(0.3f);
                        monitor.Record("settled");
                    }
                }
                monitor.End();
                Object.DestroyImmediate(rig);
                rig = null;

                ShellRestart.DestroyPersistentCore();
                GameServices.Reset();
                ShellFlow.Reset();
                var save = new MemorySave();
                save.SetFlag(ArrivalOpening.ArrivedFlagKey, true);
                GameServices.Save = save;
                yield return SceneManager.LoadSceneAsync("StPeters", LoadSceneMode.Single);
                yield return null;
                yield return null;
                ShellFlow.ContinueGame();
                yield return null;
                camera = Camera.main;
                Assert.IsNotNull(camera);
                follow = camera.GetComponent<CameraFollow>();
                reader = camera.GetComponent<CameraZoomInput>();
                Assert.IsNotNull(follow);
                Assert.IsNotNull(reader);
                monitor = camera.gameObject.AddComponent<WheelGateMonitor>();
                monitor.Begin(mouse, follow, reader, camera, rows);
                foreach (float delta in new[] { 1f, -1f, -1f, 1f })
                {
                    monitor.Phase = "scene-land";
                    monitor.Arm(delta);
                    for (int i = 0; i < 12 && !monitor.Delivered; i++) yield return null;
                    Assert.IsTrue(monitor.Delivered, "the scene's device did not receive its wheel event");
                    yield return new WaitForSecondsRealtime(0.3f);
                    monitor.Record("settled");
                }
                monitor.End();
            }
            finally
            {
                if (rig != null) Object.DestroyImmediate(rig);
                ShellRestart.DestroyPersistentCore();
                GameServices.Reset();
                ShellFlow.Reset();
                InteractionGate.Reset();
                InputSystem.RemoveDevice(mouse);
                settings.editorInputBehaviorInPlayMode = oldFocus;
                settings.backgroundBehavior = oldBackground;
                settings.updateMode = oldUpdate;
            }
        }
    }

    [DefaultExecutionOrder(-1000)]
    public sealed class WheelGateMonitor : MonoBehaviour
    {
        public string Phase;
        public bool Delivered { get; private set; }
        Mouse _mouse;
        CameraFollow _follow;
        CameraZoomInput _reader;
        Camera _camera;
        TextWriter _rows;
        int _row;
        float _delta;
        bool _pending, _armed;
        static object Read(object value, string name) => value.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(value);

        public void Begin(Mouse mouse, CameraFollow follow, CameraZoomInput reader, Camera camera, TextWriter rows)
        {
            _mouse = mouse; _follow = follow; _reader = reader; _camera = camera; _rows = rows;
            InputSystem.onBeforeUpdate += BeforeInput;
            InputSystem.onAfterUpdate += AfterInput;
        }

        public void Arm(float delta) { _delta = delta; _pending = true; _armed = true; Delivered = false; Record("armed"); }

        void BeforeInput()
        {
            if (!_pending || InputState.currentUpdateType != InputUpdateType.Dynamic) return;
            InputSystem.QueueDeltaStateEvent(_mouse.scroll, new Vector2(0f, _delta));
            _pending = false;
            Record("queued");
        }

        void AfterInput()
        {
            if (_armed && !_pending && InputState.currentUpdateType == InputUpdateType.Dynamic
                && Mathf.Abs(_mouse.scroll.ReadValue().y - _delta) < 0.0001f)
            {
                Delivered = true;
                _armed = false;
            }
            Record("after-input");
        }

        void Update() => Record("before-reader");
        void LateUpdate() => Record("after-reader");

        public void Record(string stage)
        {
            if (_rows == null || _follow == null || _reader == null) return;
            var zoom = GameServices.Config != null ? GameServices.Config.PlayerZoom.Sanitized() : PlayerZoomSettings.Default;
            var pixel = Array.Find(_camera.GetComponents<Behaviour>(), component => component.GetType().Name == "PixelPerfectCamera");
            object[] values = { _row++, Time.frameCount, stage, Phase, InputState.currentUpdateType,
                Application.isFocused, InputSystem.settings.editorInputBehaviorInPlayMode, InputSystem.settings.backgroundBehavior,
                Mouse.current == _mouse, _mouse.enabled, _mouse.scroll.ReadValue().y, _pending, Delivered,
                zoom.WheelEnabled, InteractionGate.IsBlocked, Read(_follow, "_modeKnown"), Read(_follow, "_mode"),
                _follow.FramingKnown, _follow.Framing, _follow.WheelIsLive, _reader.isActiveAndEnabled,
                Read(_reader, "_wheelSteps"), Read(_reader, "_camera") == _follow, Read(_reader, "_scrollCarry"),
                _follow.WheelUnitsPerNotch, _follow.PlayerZoomStep, _follow.PlayerBandOffset,
                Read(_follow, "_tweening"), Read(_follow, "_framingOrtho"), _camera.orthographicSize * 2f,
                pixel != null && pixel.enabled };
            _rows.WriteLine(string.Join(",", Array.ConvertAll(values, value => Convert.ToString(value, CultureInfo.InvariantCulture))));
            _rows.Flush();
        }

        public void End() { InputSystem.onBeforeUpdate -= BeforeInput; InputSystem.onAfterUpdate -= AfterInput; _rows = null; }
        void OnDestroy() => End();
    }
}
#endif
