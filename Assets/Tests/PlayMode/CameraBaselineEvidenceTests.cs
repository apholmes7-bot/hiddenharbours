#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Reflection;
using HiddenHarbours.App;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using HiddenHarbours.UI;
using HiddenHarbours.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.PlayMode
{
    public class CameraRouteEvidenceTests
    {
        [UnityTest, Timeout(300000)]
        public IEnumerator IntroRoute()
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null || string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("CAMERA_EVIDENCE")))
                Assert.Ignore("Rendered route evidence requires a graphics device and an evidence destination.");
            var route = new ArrivalAtSeaPlayTests();
            route.PublishAFreshSave();
            var go = new GameObject("Camera evidence");
            var probe = go.AddComponent<OverlayEvidenceProbe>();
            probe.Begin();
            try { yield return route.NewGameOpensAtSeaCrossesWithoutASeatSnapAndStepsAshore(); }
            finally { probe.End(); Object.DestroyImmediate(go); }
            yield return route.TearDown();
        }

        sealed class OverlayEvidenceProbe : MonoBehaviour
        {
            static string Folder => System.Environment.GetEnvironmentVariable("CAMERA_EVIDENCE") + "/";
            static readonly FieldInfo OverlayField = typeof(DayNightController).GetField("_overlay", BindingFlags.Instance | BindingFlags.NonPublic);
            static readonly FieldInfo CoverField = typeof(RegionFadeOverlay).GetField("_black", BindingFlags.Instance | BindingFlags.NonPublic);
            StreamWriter _rows;
            DayNightController _dayNight;
            RegionFadeOverlay _cover;
            Camera _captureCamera;
            RenderTexture _captureTarget;
            int _previousRate, _previousVsync, _jumpPictures, _row, _cameraSequence;
            Camera _previousRenderedCamera;
            bool _started, _pictureWritten, _havePrevious;
            float _previousSize;
            System.Exception _failure;

            public void Begin()
            {
                _previousRate = Application.targetFrameRate;
                _previousVsync = QualitySettings.vSyncCount;
                Application.targetFrameRate = 60;
                QualitySettings.vSyncCount = 0;
                _rows = new StreamWriter(Folder + "baseline-overlay-frames.csv");
                _rows.WriteLine("row,frame,realtime,region,camera_id,measured,camera_x,camera_y,orthographic_size,aspect,overlay_scale_x,overlay_scale_y,overlay_x,overlay_y,cover_alpha,pixel_perfect_enabled,target_width,target_height,framing_known,framing,mode_known,mode,world_height,framing_ortho,tweening,speed,push_in,pull_back,shake_x,shake_y");
                SceneManager.activeSceneChanged += SceneChanged;
                UnityEngine.Rendering.RenderPipelineManager.endCameraRendering += CameraEnded;
            }

            void SceneChanged(Scene previous, Scene next)
            {
                if (next.name == EastWaterPlan.SceneName) _started = true;
                AttachTarget();
            }

            void Update() => AttachTarget();

            void AttachTarget()
            {
                if (!_started) return;
                var camera = Camera.main;
                if (camera == null) return;
                if (_captureTarget == null)
                    _captureTarget = RenderTexture.GetTemporary(1920, 1080, 24, RenderTextureFormat.ARGB32);
                if (_captureCamera != null && _captureCamera != camera && _captureCamera.targetTexture == _captureTarget)
                    _captureCamera.targetTexture = null;
                _captureCamera = camera;
                camera.targetTexture = _captureTarget;
            }

            void CameraEnded(UnityEngine.Rendering.ScriptableRenderContext context, Camera camera)
            {
                if (camera != Camera.main || _failure != null) return;
                try
                {
                    if (_dayNight == null) _dayNight = System.Array.Find(Resources.FindObjectsOfTypeAll<DayNightController>(), c => c.isActiveAndEnabled);
                    if (_cover == null) _cover = Object.FindFirstObjectByType<RegionFadeOverlay>();
                    var overlay = _dayNight != null ? (Transform)OverlayField.GetValue(_dayNight) : null;
                    var black = _cover != null ? (Image)CoverField.GetValue(_cover) : null;
                    var scene = SceneManager.GetActiveScene().name;
                    string region = scene == EastWaterPlan.SceneName ? "sea" : scene == "StPeters" ? "home" : scene;
                    Vector3 lens = camera.transform.position;
                    float alpha = black != null ? black.color.a : float.NaN;
                    Vector3 scale = overlay != null ? overlay.localScale : new Vector3(float.NaN, float.NaN, float.NaN);
                    Vector3 at = overlay != null ? overlay.position : new Vector3(float.NaN, float.NaN, float.NaN);
                    var pixel = System.Array.Find(camera.GetComponents<Behaviour>(), component => component != null && component.GetType().Name == "PixelPerfectCamera");
                    if (_previousRenderedCamera != camera) { _cameraSequence++; _previousRenderedCamera = camera; }
                    var target = camera.targetTexture;
                    var follow = camera.GetComponent<CameraFollow>();
                    _rows.WriteLine(string.Format(System.Globalization.CultureInfo.InvariantCulture,
                        "{0},{1},{2:R},{3},{4},{5},{6:R},{7:R},{8:R},{9:R},{10:R},{11:R},{12:R},{13:R},{14:R},{15},{16},{17}",
                        _row++, Time.frameCount, Time.realtimeSinceStartupAsDouble, region, _cameraSequence, _started,
                        lens.x, lens.y, camera.orthographicSize, camera.aspect, scale.x, scale.y, at.x, at.y, alpha,
                        pixel != null && pixel.enabled, target != null ? target.width : 0, target != null ? target.height : 0) + Audit(follow));
                    if (!_started) return;
                    Assert.IsNotNull(target, "the 1080p evidence target is absent");
                    Assert.AreEqual(1920, target.width);
                    Assert.AreEqual(1080, target.height);
                    if (!_pictureWritten && region == "sea" && alpha == 0f)
                    {
                        Capture(camera, "opening-first-uncovered-1080.png");
                        File.WriteAllText(Folder + "baseline-first-uncovered-frame.txt", Time.frameCount.ToString());
                        _pictureWritten = true;
                    }
                    float size = camera.orthographicSize;
                    if (_havePrevious && _previousSize > 0f && Mathf.Abs(size - _previousSize) / _previousSize > 0.05f
                        && (region == "sea" || region == "home") && _jumpPictures < 2)
                    {
                        Capture(camera, _jumpPictures++ == 0 ? "lens-jump-a.png" : "lens-jump-b.png");
                    }
                    _previousSize = size;
                    _havePrevious = true;
                }
                catch (System.Exception ex) { _failure = ex; }
            }

            static string Audit(CameraFollow follow)
            {
                if (follow == null) return ",False,none,False,none,0,0,False,0,0,0,0,0";
                object Read(string name) => typeof(CameraFollow).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(follow);
                object[] values = { follow.FramingKnown, follow.Framing, Read("_modeKnown"), Read("_mode"),
                    Read("_worldHeightMeters"), Read("_framingOrtho"), Read("_tweening"), Read("_targetSpeedMps"),
                    follow.Feel.PushIn, follow.Feel.PullBack, follow.Feel.ShakeOffset.x, follow.Feel.ShakeOffset.y };
                return "," + string.Join(",", System.Array.ConvertAll(values, value => System.Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture)));
            }
            static void Capture(Camera camera, string name)
            {
                var target = camera.targetTexture;
                var previous = RenderTexture.active;
                var texture = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
                try
                {
                    RenderTexture.active = target;
                    texture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                    texture.Apply();
                    File.WriteAllBytes(Folder + name, texture.EncodeToPNG());
                }
                finally { RenderTexture.active = previous; Object.Destroy(texture); }
            }

            public void End()
            {
                UnityEngine.Rendering.RenderPipelineManager.endCameraRendering -= CameraEnded;
                SceneManager.activeSceneChanged -= SceneChanged;
                _rows?.Dispose();
                if (_captureCamera != null && _captureCamera.targetTexture == _captureTarget)
                    _captureCamera.targetTexture = null;
                if (_captureTarget != null) RenderTexture.ReleaseTemporary(_captureTarget);
                Application.targetFrameRate = _previousRate;
                QualitySettings.vSyncCount = _previousVsync;
                File.WriteAllText(Folder + "baseline-restored-settings.json",
                    "{\"previous_target_frame_rate\":" + _previousRate + ",\"previous_vsync\":" + _previousVsync
                    + ",\"restored_target_frame_rate\":" + Application.targetFrameRate + ",\"restored_vsync\":" + QualitySettings.vSyncCount + "}");
                Assert.IsNull(_failure, _failure?.ToString());
                Assert.IsTrue(_pictureWritten, "no fully uncovered sea render was captured");
            }
        }


    }
}
#endif
