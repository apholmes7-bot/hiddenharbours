#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HiddenHarbours.App;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.PlayMode
{
    public class CameraStopPlatesPlayTests
    {
        sealed class MemorySave : ISaveService
        {
            readonly Dictionary<string, bool> _flags = new Dictionary<string, bool>();
            public SaveData Current { get; } = new SaveData();
            public bool GetFlag(string key) => _flags.TryGetValue(key, out bool value) && value;
            public void SetFlag(string key, bool value) => _flags[key] = value;
            public void Save() { }
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator WalkingStopsInBothRegions_AndTheThreeRequiredHelmViews_RepeatWithinThisRun()
        {
            string evidence = System.Environment.GetEnvironmentVariable("CAMERA_EVIDENCE");
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null || string.IsNullOrEmpty(evidence))
                Assert.Ignore("Native plates require a graphics device and an evidence destination.");
            Directory.CreateDirectory(Path.Combine(evidence, "plates"));
            try
            {
                foreach (string region in new[] { "StPeters", "NineMileCreek" })
                {
                    ShellRestart.DestroyPersistentCore();
                    GameServices.Reset();
                    ShellFlow.Reset();
                    var save = new MemorySave();
                    save.SetFlag(ArrivalOpening.ArrivedFlagKey, true);
                    GameServices.Save = save;
                    yield return SceneManager.LoadSceneAsync(region, LoadSceneMode.Single);
                    yield return null;
                    yield return null;
                    ShellFlow.ContinueGame();
                    yield return null;
                    var camera = Camera.main;
                    Assert.IsNotNull(camera);
                    var follow = camera.GetComponent<CameraFollow>();
                    Assert.IsNotNull(follow);
                    follow.ScreenPixelsOverride = new Vector2Int(1920, 1080);
                    follow.OnControlModeChanged(new ControlModeChanged(ControlMode.OnFoot));
                    follow.TickZoom(100d);
                    var anchor = Object.FindObjectsByType<RegionAnchor>(FindObjectsSortMode.None)
                        .Single(a => a.gameObject.scene.name == region);
                    Assert.IsNotNull(anchor.DisembarkPoint, "the walking plate needs the authored dry landing");
                    Assert.IsNotNull(follow.Target);
                    var walkerBody = follow.Target.GetComponent<Rigidbody2D>();
                    follow.Target.position = anchor.DisembarkPoint.position;
                    if (walkerBody != null)
                    {
                        walkerBody.position = anchor.DisembarkPoint.position;
                        walkerBody.linearVelocity = Vector2.zero;
                    }
                    yield return new WaitForSecondsRealtime(0.5f);
                    for (int step = 2; step <= 6; step++)
                    {
                        follow.SetFraming(1080f / (32f * step), 0f);
                        yield return new WaitForSecondsRealtime(0.3f);
                        yield return PlatePair(camera, Path.Combine(evidence, "plates", region + "-walker-stop-" + step));
                    }
                }

                // The creek's standalone scene has no OwnedFleet. Boot the persistent core
                // from the home scene before carrying it to open water for the helm plates.
                ShellRestart.DestroyPersistentCore();
                GameServices.Reset();
                ShellFlow.Reset();
                var helmSave = new MemorySave();
                helmSave.SetFlag(ArrivalOpening.ArrivedFlagKey, true);
                GameServices.Save = helmSave;
                yield return SceneManager.LoadSceneAsync("StPeters", LoadSceneMode.Single);
                yield return null;
                yield return null;
                ShellFlow.ContinueGame();
                yield return null;
                yield return SceneManager.LoadSceneAsync(EastWaterPlan.SceneName, LoadSceneMode.Single);
                yield return null;
                yield return null;
                var hulls = AssetDatabase.FindAssets("t:BoatHullDef").Select(AssetDatabase.GUIDToAssetPath)
                    .Select(AssetDatabase.LoadAssetAtPath<BoatHullDef>).Where(h => h != null).ToArray();
                var longest = hulls.Where(h => h.Helm != null).OrderByDescending(h => h.LengthMeters).First();
                File.WriteAllText(Path.Combine(evidence, "longest-hull-plate.txt"), longest.Id + " " + longest.LengthMeters + " m\n");
                var selected = new[] { hulls.Single(h => h.Id == "boat.dory"), hulls.Single(h => h.Id == "boat.cape_islander"), longest };
                var fleet = Object.FindAnyObjectByType<OwnedFleet>(FindObjectsInactive.Include);
                Assert.IsNotNull(fleet, "the home scene must supply the persistent player fleet");
                fleet.gameObject.SetActive(true);
                var boat = fleet.GetComponent<BoatController>();
                Assert.IsNotNull(boat);
                fleet.Configure(hulls, boat, boat.GetComponent<ShipHold>(), boat.GetComponent<SpriteRenderer>());
                var body = boat.GetComponent<Rigidbody2D>();
                body.simulated = false;
                boat.enabled = true;
                Rect bounds = GameServices.CurrentRegionBounds;
                boat.transform.position = bounds.width > 0f ? (Vector3)bounds.center : Vector3.zero;
                var helmCamera = Camera.main;
                var helmFollow = helmCamera.GetComponent<CameraFollow>();
                helmFollow.ScreenPixelsOverride = new Vector2Int(1920, 1080);
                EventBus.Publish(new ControlModeChanged(ControlMode.Aboard));
                GameServices.Helm.SetPilotedHull(boat);
                foreach (var hull in selected)
                {
                    fleet.OnBoatPurchased(new BoatPurchased(hull.Id, 0));
                    helmFollow.Target = boat.transform;
                    helmFollow.OnActiveBoatChanged(new ActiveBoatChanged(hull.Id, hull.CameraWorldHeightMeters, hull.LengthMeters));
                    helmFollow.OnControlModeChanged(new ControlModeChanged(ControlMode.Aboard));
                    helmFollow.TickZoom(300d);
                    yield return new WaitForSecondsRealtime(1f);
                    yield return PlatePair(helmCamera, Path.Combine(evidence, "plates", hull.Id.Replace('.', '-') + "-helm"));
                }
            }
            finally
            {
                ShellRestart.DestroyPersistentCore();
                GameServices.Reset();
                ShellFlow.Reset();
                InteractionGate.Reset();
            }
        }

        static IEnumerator PlatePair(Camera camera, string path)
        {
            var previousTarget = camera.targetTexture;
            var target = RenderTexture.GetTemporary(1920, 1080, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = target;
            var overlays = new List<(Canvas canvas, Camera camera, float distance)>();
            foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            {
                if (!canvas.isActiveAndEnabled || !canvas.isRootCanvas || canvas.renderMode != RenderMode.ScreenSpaceOverlay) continue;
                overlays.Add((canvas, canvas.worldCamera, canvas.planeDistance));
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1f;
            }
            Canvas.ForceUpdateCanvases();
            float oldScale = Time.timeScale;
            bool oldPaused = GameServices.Clock != null && GameServices.Clock.IsPaused;
            Time.timeScale = 0f;
            if (GameServices.Clock != null) GameServices.Clock.IsPaused = true;
            try
            {
                for (int repeat = 0; repeat < 2; repeat++)
                {
                    bool captured = false;
                    Exception failure = null;
                    string filename = path + (repeat == 0 ? "-a.png" : "-b.png");
                    void Rendered(UnityEngine.Rendering.ScriptableRenderContext context, Camera rendered)
                    {
                        if (rendered != camera || captured) return;
                        captured = true;
                        var previous = RenderTexture.active;
                        var image = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
                        try
                        {
                            RenderTexture.active = target;
                            image.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
                            image.Apply();
                            File.WriteAllBytes(filename, image.EncodeToPNG());
                            File.WriteAllText(filename + ".json", "{\"frame\":" + Time.frameCount
                                + ",\"world_height\":" + (camera.orthographicSize * 2f).ToString("R", System.Globalization.CultureInfo.InvariantCulture) + "}");
                        }
                        catch (Exception ex) { failure = ex; }
                        finally { RenderTexture.active = previous; Object.Destroy(image); }
                    }
                    UnityEngine.Rendering.RenderPipelineManager.endCameraRendering += Rendered;
                    try
                    {
                        double deadline = Time.realtimeSinceStartupAsDouble + 10d;
                        while (!captured && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
                        Assert.IsTrue(captured, "the plate camera did not render");
                        Assert.IsNull(failure, failure?.ToString());
                    }
                    finally { UnityEngine.Rendering.RenderPipelineManager.endCameraRendering -= Rendered; }
                }
            }
            finally
            {
                Time.timeScale = oldScale;
                if (GameServices.Clock != null) GameServices.Clock.IsPaused = oldPaused;
                foreach (var overlay in overlays)
                {
                    if (overlay.canvas == null) continue;
                    overlay.canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                    overlay.canvas.worldCamera = overlay.camera;
                    overlay.canvas.planeDistance = overlay.distance;
                }
                camera.targetTexture = previousTarget;
                RenderTexture.ReleaseTemporary(target);
            }
        }
    }
}
#endif
