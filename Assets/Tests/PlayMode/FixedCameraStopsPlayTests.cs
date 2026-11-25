#if UNITY_EDITOR
using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Reflection;
using HiddenHarbours.App;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.PlayMode
{
    public class FixedCameraStopsPlayTests
    {
        static readonly Type PixelType = Type.GetType("UnityEngine.Rendering.Universal.PixelPerfectCamera, Unity.RenderPipelines.Universal.2D.Runtime", true);
        static string Evidence => System.Environment.GetEnvironmentVariable("CAMERA_EVIDENCE");
        static GameConfig Shipped() => AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/_Project/Data/Config/GameConfig.asset");
        static float Height(int step, int pixels) => step > 0 ? pixels / (32f * step) : pixels * -step / 32f;
        static int OffsetStep(int step, int offset)
        {
            int index = step <= -2 ? step + 2 : step;
            index += offset;
            return index <= 0 ? index - 2 : index;
        }

        [UnityTest, Timeout(120000)]
        public IEnumerator RealWheel_LandsOneStopPerNotch_OnLandAndAboard_AtEachScreen()
        {
            var settings = InputSystem.settings;
            var oldFocus = settings.editorInputBehaviorInPlayMode;
            var oldBackground = settings.backgroundBehavior;
            var oldUpdate = settings.updateMode;
            settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            settings.updateMode = InputSettings.UpdateMode.ProcessEventsInDynamicUpdate;
            var mouse = InputSystem.AddDevice<Mouse>();
            mouse.MakeCurrent();
            var config = Object.Instantiate(Shipped());
            Assert.IsNotNull(config);
            GameServices.Config = config;
            InteractionGate.Reset();
            using TextWriter rows = string.IsNullOrEmpty(Evidence) ? new StringWriter()
                : new StreamWriter(Path.Combine(Evidence, "fixed-device-gates.csv"));
            rows.WriteLine("row,frame,stage,phase,update_type,focused,editor_input,background,mouse_current,mouse_enabled,scroll,pending,delivered,wheel_enabled,modal,mode_known,mode,framing_known,framing,wheel_live,reader_active,reader_wheel,reader_camera,carry,units,step,band,tween,framing_ortho,lens_height,pixel_perfect");
            try
            {
                int[] screens = { 1080, 1440, 2160 };
                int[] homes = { 3, 4, 6 }, far = { 2, 3, 4 }, close = { 6, 8, 12 }, hullSteps = { 2, 3, 4 };
                for (int s = 0; s < screens.Length; s++)
                {
                    int height = screens[s];
                    var go = new GameObject("Fixed stop camera");
                    var camera = go.AddComponent<Camera>();
                    camera.orthographic = true;
                    camera.aspect = 16f / 9f;
                    var pixel = (Behaviour)go.AddComponent(PixelType);
                    PixelType.GetProperty("assetsPPU").SetValue(pixel, 32);
                    var follow = go.AddComponent<CameraFollow>();
                    follow.ScreenPixelsOverride = new Vector2Int(height * 16 / 9, height);
                    var reader = go.AddComponent<CameraZoomInput>();
                    var monitor = go.AddComponent<WheelGateMonitor>();
                    monitor.Begin(mouse, follow, reader, camera, rows);
                    RenderTexture target = null;
                    if (SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null)
                    {
                        target = RenderTexture.GetTemporary(height * 16 / 9, height, 24);
                        camera.targetTexture = target;
                    }
                    try
                    {
                        follow.OnControlModeChanged(new ControlModeChanged(ControlMode.OnFoot));
                        follow.TickZoom(100d);
                        yield return new WaitForSecondsRealtime(0.5f);
                        int expected = homes[s];
                        Assert.AreEqual(expected, follow.PlayerZoomStep);
                        Assert.AreEqual(Height(expected, height), camera.orthographicSize * 2f, 0.0001f);
                        foreach (int direction in new[] { -1, 1, -1 })
                        {
                            int count = direction < 0 ? expected - far[s] + 1 : close[s] - expected + 1;
                            for (int n = 0; n < count; n++)
                            {
                                expected = Mathf.Clamp(expected + direction, far[s], close[s]);
                                monitor.Phase = "land-" + height;
                                yield return Deliver(monitor, direction);
                                Assert.AreEqual(expected, follow.PlayerZoomStep, "one delivered detent must move one stop, saturating at the ends");
                                Assert.AreEqual(Height(expected, height), camera.orthographicSize * 2f, 0.0001f);
                                Assert.IsTrue(pixel.enabled);
                            }
                        }
                        follow.OnActiveBoatChanged(new ActiveBoatChanged("boat.test_hull", 17f));
                        follow.OnControlModeChanged(new ControlModeChanged(ControlMode.Aboard));
                        follow.TickZoom(200d);
                        yield return new WaitForSecondsRealtime(0.5f);
                        int offset = 0;
                        foreach (int direction in new[] { -1, 1, -1 })
                        {
                            int count = direction < 0 ? offset + 3 : 3 - offset;
                            for (int n = 0; n < count; n++)
                            {
                                offset = Mathf.Clamp(offset + direction, -2, 2);
                                expected = OffsetStep(hullSteps[s], offset);
                                monitor.Phase = "aboard-" + height;
                                yield return Deliver(monitor, direction);
                                Assert.AreEqual(offset, follow.PlayerBandOffset);
                                Assert.AreEqual(Height(expected, height), camera.orthographicSize * 2f, 0.0001f);
                                Assert.AreEqual(expected > 0, pixel.enabled);
                            }
                        }
                    }
                    finally
                    {
                        monitor.End();
                        camera.targetTexture = null;
                        Object.DestroyImmediate(go);
                        if (target != null) RenderTexture.ReleaseTemporary(target);
                    }
                }
            }
            finally
            {
                InputSystem.RemoveDevice(mouse);
                settings.editorInputBehaviorInPlayMode = oldFocus;
                settings.backgroundBehavior = oldBackground;
                settings.updateMode = oldUpdate;
                Object.DestroyImmediate(config);
                GameServices.Reset();
                InteractionGate.Reset();
            }
        }

        static IEnumerator Deliver(WheelGateMonitor monitor, int direction)
        {
            monitor.Arm(direction);
            for (int i = 0; i < 12 && !monitor.Delivered; i++) yield return null;
            Assert.IsTrue(monitor.Delivered, "the device did not see the detent; a direct camera call cannot substitute for this proof");
            yield return new WaitForSecondsRealtime(0.25f);
            monitor.Record("settled");
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator FullThrottleThenCoastToAStop_LeavesTheLensOnItsStop()
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                Assert.Ignore("Rendered lens evidence needs a graphics device.");
            var config = Object.Instantiate(Shipped());
            GameServices.Config = config;
            Assert.AreEqual(0f, config.Juice.SpeedPullBackFraction);
            Assert.AreEqual(0f, config.Juice.SpeedPullBackSeaStateFraction);
            var boatGo = new GameObject("Sailing boat");
            var boat = boatGo.AddComponent<BoatController>();
            var hull = AssetDatabase.LoadAssetAtPath<BoatHullDef>("Assets/_Project/Data/Boats/CapeIslander.asset");
            boat.SetHull(hull);
            boat.SetLocalSeabedDepth(100f);
            var camGo = new GameObject("Sailing camera");
            var camera = camGo.AddComponent<Camera>();
            camera.orthographic = true;
            var target = RenderTexture.GetTemporary(1920, 1080, 24);
            camera.targetTexture = target;
            var pixel = (Behaviour)camGo.AddComponent(PixelType);
            PixelType.GetProperty("assetsPPU").SetValue(pixel, 32);
            var follow = camGo.AddComponent<CameraFollow>();
            follow.Target = boat.transform;
            follow.OnActiveBoatChanged(new ActiveBoatChanged(hull.Id, hull.CameraWorldHeightMeters, hull.LengthMeters));
            follow.OnControlModeChanged(new ControlModeChanged(ControlMode.Aboard));
            follow.TickZoom(100d);
            yield return new WaitForSecondsRealtime(0.5f);
            float resting = camera.orthographicSize;
            using var recorder = new LensRecorder(camera, follow, "sail-frames.csv");
            try
            {
                boat.SetControl(1f, 0f);
                yield return new WaitForSecondsRealtime(10f);
                Assert.Greater(boat.Velocity.magnitude, 1f, "the full-throttle sail must actually gather way");
                boat.SetControl(0f, 0f);
                double deadline = Time.realtimeSinceStartupAsDouble + 140d;
                while (boat.Velocity.magnitude > 0.05f && Time.realtimeSinceStartupAsDouble < deadline)
                    yield return null;
                Assert.LessOrEqual(boat.Velocity.magnitude, 0.05f, "the measured coast did not reach a stop");
                yield return new WaitForSecondsRealtime(0.5f);
                Assert.AreEqual(resting, camera.orthographicSize, 0.0001f);
                Assert.AreEqual(0, recorder.UnaskedJumps);
                Assert.Greater(recorder.Frames, 10);
                Assert.IsTrue(pixel.enabled);
            }
            finally
            {
                camera.targetTexture = null;
                Object.DestroyImmediate(camGo);
                Object.DestroyImmediate(boatGo);
                Object.DestroyImmediate(config);
                RenderTexture.ReleaseTemporary(target);
                GameServices.Reset();
            }
        }

        [UnityTest]
        public IEnumerator CatchAndImpact_ReturnToTheSameRenderedStop_WithoutAHandbackStep()
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                Assert.Ignore("Rendered handback evidence needs a graphics device.");
            var config = Object.Instantiate(Shipped());
            GameServices.Config = config;
            var go = new GameObject("Beat camera");
            var camera = go.AddComponent<Camera>();
            camera.orthographic = true;
            var target = RenderTexture.GetTemporary(1920, 1080, 24);
            camera.targetTexture = target;
            var pixel = (Behaviour)go.AddComponent(PixelType);
            PixelType.GetProperty("assetsPPU").SetValue(pixel, 32);
            var follow = go.AddComponent<CameraFollow>();
            follow.OnControlModeChanged(new ControlModeChanged(ControlMode.OnFoot));
            follow.TickZoom(100d);
            yield return new WaitForSecondsRealtime(0.5f);
            float resting = camera.orthographicSize;
            using var recorder = new LensRecorder(camera, follow, "beat-frames.csv");
            try
            {
                recorder.Beat = true;
                follow.OnCatchLanded(new CatchLanded(new CatchItem("fish.test", "Test fish", FishCategory.InshoreGroundfish, 8f, 10, 0.5f)));
                yield return new WaitForSecondsRealtime(0.15f);
                Assert.Less(camera.orthographicSize, resting, "the catch push-in must remain visible");
                yield return new WaitForSecondsRealtime(0.7f);
                Assert.IsTrue(follow.Feel.AtRest);
                Assert.AreEqual(resting, camera.orthographicSize, 0.0001f);
                follow.OnControlModeChanged(new ControlModeChanged(ControlMode.Aboard));
                follow.TickZoom(200d);
                yield return new WaitForSecondsRealtime(0.5f);
                resting = camera.orthographicSize;
                follow.OnBoatGrounded(new BoatGrounded(null, 1f));
                yield return new WaitForSecondsRealtime(0.5f);
                Assert.IsTrue(follow.Feel.AtRest);
                Assert.AreEqual(resting, camera.orthographicSize, 0.0001f);
                Assert.Less(recorder.LargestHandbackFraction, 0.001f, "hand-back must not add a visible lens step");
                Assert.IsTrue(pixel.enabled);
            }
            finally
            {
                camera.targetTexture = null;
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(config);
                RenderTexture.ReleaseTemporary(target);
                GameServices.Reset();
            }
        }

        sealed class LensRecorder : IDisposable
        {
            readonly Camera _camera;
            readonly CameraFollow _follow;
            readonly TextWriter _rows;
            float _previous;
            bool _havePrevious, _wasApplied;
            public bool Beat;
            public int Frames, UnaskedJumps;
            public float LargestHandbackFraction;
            public LensRecorder(Camera camera, CameraFollow follow, string filename)
            {
                _camera = camera; _follow = follow;
                _rows = string.IsNullOrEmpty(Evidence) ? new StringWriter() : new StreamWriter(Path.Combine(Evidence, filename));
                _rows.WriteLine("row,frame,realtime,world_height,pixel_perfect,framing,feel_applied,push_in,pull_back,shake_x,shake_y,speed,beat");
                UnityEngine.Rendering.RenderPipelineManager.endCameraRendering += Rendered;
            }
            void Rendered(UnityEngine.Rendering.ScriptableRenderContext context, Camera camera)
            {
                if (camera != _camera) return;
                float size = camera.orthographicSize;
                float change = _havePrevious ? Mathf.Abs(size - _previous) / _previous : 0f;
                if (!Beat && change > 0.05f) UnaskedJumps++;
                if (_wasApplied && !_follow.FeelApplied) LargestHandbackFraction = Mathf.Max(LargestHandbackFraction, change);
                var pixel = camera.GetComponent(PixelType) as Behaviour;
                object speed = typeof(CameraFollow).GetField("_targetSpeedMps", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_follow);
                _rows.WriteLine(string.Format(CultureInfo.InvariantCulture, "{0},{1},{2:R},{3:R},{4},{5},{6},{7:R},{8:R},{9:R},{10:R},{11},{12}",
                    Frames++, Time.frameCount, Time.realtimeSinceStartupAsDouble, size * 2f, pixel != null && pixel.enabled,
                    _follow.Framing, _follow.FeelApplied, _follow.Feel.PushIn, _follow.Feel.PullBack,
                    _follow.Feel.ShakeOffset.x, _follow.Feel.ShakeOffset.y, speed, Beat));
                _rows.Flush(); _previous = size; _havePrevious = true; _wasApplied = _follow.FeelApplied;
            }
            public void Dispose() { UnityEngine.Rendering.RenderPipelineManager.endCameraRendering -= Rendered; _rows.Dispose(); }
        }
    }
}
#endif
