using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HiddenHarbours.Art;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HiddenHarbours.App.Editor
{
    /// <summary>One leased, evidence-only baseline. The process must inherit its fixture save path.
    /// Never called through Pipeline; never saves a scene or an authored asset.</summary>
    [InitializeOnLoad]
    public static class WaterHullsMeasureCapture
    {
        private const string Active = "WaterHullsMeasure.Active";
        private const string Phase = "WaterHullsMeasure.Phase";
        private const string ExitCode = "WaterHullsMeasure.ExitCode";
        private static string Root => Path.GetFullPath("Evidence~/water-hulls");
        private static bool Plates => System.Environment.GetEnvironmentVariable("HH_WATER_HULLS_PLATES") == "1";
        private static string Output => Path.Combine(Root, Plates ? "plates" : "l1-capture");
        private static string Fixture => Path.GetFullPath(Path.Combine(Root, "fixture", "savegame.json"));
        private static int _frames;
        private static double _deadline;
        private static double _settleUntil;

        static WaterHullsMeasureCapture()
        {
            if (System.Environment.GetEnvironmentVariable("HH_WATER_HULLS_L1_CAPTURE") == "1"
                && SessionState.GetBool(Active, false))
                EditorApplication.delayCall += Resume;
        }

        public static void RunBatch()
        {
            try
            {
                Directory.CreateDirectory(Output);
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                    throw new InvalidOperationException("Capture must start outside Play.");
                AssertSavePath("before-play", SaveStore.ActivePath);
                if (Plates) PlayerSettings.enableFrameTimingStats = true;
                SaveData fixture = SaveStore.Read(Fixture);
                if (fixture == null || fixture.WorldSeed != 12345)
                    throw new InvalidOperationException("The evidence fixture must exist with seed 12345.");
                SessionState.SetBool(Active, true);
                SessionState.SetInt(Phase, 0);
                SessionState.SetInt(ExitCode, 0);
                EditorSceneManager.OpenScene("Assets/_Project/Scenes/NineMileCreek.unity",
                    OpenSceneMode.Single);
                Resume();
                EditorApplication.EnterPlaymode();
            }
            catch (Exception exception) { Fail(exception); }
        }

        private static void Resume()
        {
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            _deadline = EditorApplication.timeSinceStartup + (Plates ? 1800 : 240);
        }

        private static void Tick()
        {
            try
            {
                int phase = SessionState.GetInt(Phase, 0);
                if (phase == 2)
                {
                    if (!EditorApplication.isPlayingOrWillChangePlaymode)
                    {
                        EditorApplication.update -= Tick;
                        SessionState.SetBool(Active, false);
                        EditorApplication.Exit(SessionState.GetInt(ExitCode, 1));
                    }
                    return;
                }
                if (EditorApplication.timeSinceStartup > _deadline)
                    throw new TimeoutException("The berth capture did not become ready within 240 seconds.");
                if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
                if (phase == 0)
                {
                    var save = Object.FindFirstObjectByType<SaveService>();
                    if (save == null || GameServices.Clock == null || GameServices.Environment == null) return;
                    string opened = (string)typeof(SaveService).GetField("_path",
                        BindingFlags.Instance | BindingFlags.NonPublic).GetValue(save);
                    AssertSavePath("opened-in-play", opened);
                    GameServices.Clock.SeekTo(GameServices.SecondsPerDay * 0.5);
                    GameServices.Clock.TimeScale = 0;
                    SessionState.SetInt(Phase, 1);
                    _frames = 0;
                    _settleUntil = EditorApplication.timeSinceStartup + 3;
                    return;
                }
                // Let the production presentation, waves and day/night pushes settle. One PNG only.
                if (++_frames < 120 || EditorApplication.timeSinceStartup < _settleUntil
                    || ShaderUtil.anythingCompiling) return;
                if (Plates)
                {
                    if (WaterHullsPlates.Step(Output)) Finish(0);
                    return;
                }
                Capture();
                Finish(0);
            }
            catch (Exception exception) { Fail(exception); }
        }

        private static void AssertSavePath(string stage, string actual)
        {
            bool matches = !string.IsNullOrWhiteSpace(actual)
                && string.Equals(Path.GetFullPath(actual), Fixture, StringComparison.OrdinalIgnoreCase)
                && File.Exists(Fixture);
            File.WriteAllText(Path.Combine(Output, "save-path-" + stage + ".json"),
                JsonUtility.ToJson(new SavePathProof { stage = stage, expected = Fixture,
                    actual = actual, passed = matches }, true));
            if (!matches) throw new InvalidOperationException("Fixture save path assertion failed at " + stage);
        }

        private static void Capture()
        {
            var boats = Object.FindObjectsByType<MooredBoat>(FindObjectsSortMode.None)
                .Where(boat => boat.Owner != null && boat.IsPresented).OrderBy(boat => boat.Owner.Id).ToArray();
            MooredBoat berth = boats.FirstOrDefault(boat => boat.GetComponentInChildren<IsoFacetHullRenderer>() != null);
            if (berth == null) throw new InvalidOperationException("No presented mesh hull exists at a berth.");
            IsoFacetHullRenderer hull = berth.GetComponentInChildren<IsoFacetHullRenderer>();
            Camera camera = Camera.main;
            if (camera == null) throw new InvalidOperationException("No production camera exists.");
            if (GameServices.Clock.HourOfDay < 11.99f || GameServices.Clock.HourOfDay > 12.01f
                || Shader.GetGlobalFloat("_SunElevation") <= 0)
                throw new InvalidOperationException("The capture is not a live daytime frame.");
            if (!DisplacedWaterRegistry.TryGetIsoDepthFrame(out WaterIsoDepthFrame frame))
                throw new InvalidOperationException("The production displaced sea has no live depth frame.");

            foreach (Behaviour behaviour in camera.GetComponents<Behaviour>())
                if (!(behaviour is Camera)
                    && behaviour.GetType().Name != "UniversalAdditionalCameraData") behaviour.enabled = false;
            camera.transform.position = new Vector3(berth.transform.position.x,
                berth.transform.position.y, camera.transform.position.z);
            camera.orthographic = true;
            camera.orthographicSize = 14;
            var target = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
            RenderTexture previous = RenderTexture.active;
            RenderTexture previousTarget = camera.targetTexture;
            Texture2D picture = null;
            try
            {
                target.Create();
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                picture = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
                picture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                picture.Apply();
                File.WriteAllBytes(Path.Combine(Output, "berth-noon-before.png"), picture.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previous;
                if (picture != null) Object.DestroyImmediate(picture);
                target.Release();
                Object.DestroyImmediate(target);
            }

            var water = Object.FindFirstObjectByType<WaterSurface>();
            if (water == null) throw new InvalidOperationException("The live sea surface is absent.");
            Renderer waterRenderer = water.GetComponent<Renderer>();
            Material material = waterRenderer.sharedMaterial;
            var block = new MaterialPropertyBlock();
            waterRenderer.GetPropertyBlock(block);
            var readings = new List<Reading>();
            foreach (string name in new[] { "_WaterLevel", "_CausticAmount", "_CausticScale",
                "_CausticDepth", "_CausticDayGate", "_CausticTexStrength", "_CausticCurvatureBlend",
                "_CausticCurvatureStep", "_CausticCurvatureGain", "_PixelsPerUnit", "_Chop", "_Flow" })
            {
                int id = Shader.PropertyToID(name);
                readings.Add(new Reading { name = name, value = block.HasFloat(id)
                    ? block.GetFloat(id) : material.GetFloat(id) });
            }
            int tintId = Shader.PropertyToID("_CausticColor");
            Color tint = block.HasVector(tintId) ? block.GetColor(tintId) : material.GetColor(tintId);
            Mesh mesh = hull.PosedMesh.GetComponent<MeshFilter>().sharedMesh;
            var attrs = new List<Vector4>();
            mesh.GetUVs(0, attrs);
            Vector3[] vertices = mesh.vertices;
            Vector3[] normals = mesh.normals;
            var points = new List<FacePoint>();
            for (int i = 0; i < Math.Min(vertices.Length, 32); i++)
                points.Add(new FacePoint { vertex = i, world = hull.PosedMesh.TransformPoint(vertices[i]),
                    normal = hull.PosedMesh.localToWorldMatrix.MultiplyVector(normals[i]).normalized,
                    attrs = attrs[i] });
            EnvironmentSample environment = GameServices.Environment.Sample();
            var proof = new CaptureProof
            {
                sha = System.Environment.GetEnvironmentVariable("HH_WATER_HULLS_SHA"),
                scene = berth.gameObject.scene.path, owner = berth.Owner.Id,
                hull = berth.Owner.Boat.Id, state = "at berth", hour = GameServices.Clock.HourOfDay,
                wind = environment.WindVector, seaState = environment.SeaState01,
                tide = environment.TideHeight, causticTint = tint,
                sunElevation = Shader.GetGlobalFloat("_SunElevation"),
                sun = Shader.GetGlobalVector("_SunDir"), fleetHulls = boats.Length,
                root = berth.transform.position, heavePixels = hull.HeavePixels,
                meshVertices = mesh.vertexCount, meshTriangles = mesh.triangles.Length / 3,
                frame = new Vector4(frame.ReferenceY, frame.CosElev, frame.SinElev, frame.BaseZ),
                exaggeration = frame.Exaggeration, frequencyScale = frame.FreqScale,
                uniforms = readings.ToArray(), points = points.ToArray(),
                note = "Baseline only. Source cost audit is in l1-measure.md; no frame-time delta claimed."
            };
            File.WriteAllText(Path.Combine(Output, "berth-noon-before.json"), JsonUtility.ToJson(proof, true));
        }

        private static void Fail(Exception exception)
        {
            Directory.CreateDirectory(Output);
            File.WriteAllText(Path.Combine(Output, "failure.txt"), exception.ToString());
            Debug.LogException(exception);
            Finish(1);
        }

        private static void Finish(int code)
        {
            SessionState.SetInt(ExitCode, code);
            SessionState.SetInt(Phase, 2);
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                SessionState.SetBool(Active, true);
                EditorApplication.isPlaying = false;
            }
            else
            {
                SessionState.SetBool(Active, false);
                EditorApplication.Exit(code);
            }
        }

        [Serializable] private sealed class SavePathProof
        { public string stage, expected, actual; public bool passed; }
        [Serializable] private sealed class Reading { public string name; public float value; }
        [Serializable] private sealed class FacePoint
        { public int vertex; public Vector3 world, normal; public Vector4 attrs; }
        [Serializable] private sealed class CaptureProof
        {
            public string sha, scene, owner, hull, state, note;
            public float hour, seaState, tide, sunElevation, heavePixels, exaggeration, frequencyScale;
            public int fleetHulls, meshVertices, meshTriangles;
            public Vector2 wind;
            public Vector3 root;
            public Vector4 sun, frame;
            public Color causticTint;
            public Reading[] uniforms;
            public FacePoint[] points;
        }
    }
}
