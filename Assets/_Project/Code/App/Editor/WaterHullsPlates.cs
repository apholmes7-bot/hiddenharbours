using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using HiddenHarbours.Art;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using Unity.Profiling;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HiddenHarbours.App.Editor
{
    /// <summary>Paired runtime plates, with a before shader compiled directly from the measured base.
    /// Source is held in memory; no authored asset or scene is saved.</summary>
    public static class WaterHullsPlates
    {
        private const string Base = "3a0b19cf1e778ee43a5efbcf57a318f4f75ea53d";
        private static int _lastFrame = -1, _case = -1, _stageFrames, _costPhase;
        private static double _stageStart;
        private static string _output;
        private static Camera _camera;
        private static RenderTexture _target;
        private static WaterSurface _water;
        private static DisplacedWaterSurface _displaced;
        private static Material _waterMaterial, _displacedMaterial;
        private static Shader _beforeShader, _afterShader;
        private static string[] _keywords;
        private static GameObject[] _ambient;
        private static GameObject[] _boats;
        private static Vector3 _berth;
        private static readonly Weather _weather = new Weather();
        private static readonly List<PairProof> _pairs = new List<PairProof>();
        private static readonly List<double> _cpuOff = new List<double>(), _cpuOn = new List<double>();
        private static readonly List<double> _gpuOff = new List<double>(), _gpuOn = new List<double>();
        private static readonly FrameTiming[] _timings = new FrameTiming[1];
        private static ProfilerRecorder _mainThread;
        private static float _strength;

        public static bool Step(string output)
        {
            if (Time.frameCount == _lastFrame) return false;
            _lastFrame = Time.frameCount;
            if (_camera == null) Initialize(output);
            if (_case < 0) { BeginCase(0); return false; }
            if (_case < 13)
            {
                if (_case < 12 && ((_case / 3) % 2) == 1)
                {
                    _boats[_case / 6].transform.position += Vector3.right * (3f * Time.deltaTime);
                    Vector3 follow = _boats[_case / 6].transform.position;
                    _camera.transform.position = new Vector3(follow.x, follow.y, _camera.transform.position.z);
                }
                _stageFrames++;
                if (_stageFrames < 60 || EditorApplication.timeSinceStartup - _stageStart < 3
                    || ShaderUtil.anythingCompiling) return false;
                CapturePair(_case);
                if (_case == 12) { BeginCost(); return false; }
                BeginCase(_case + 1);
                return false;
            }
            return CostStep();
        }

        private static void Initialize(string output)
        {
            _output = output;
            _strength = GameServices.Config.HullCausticStrength;
            _camera = Camera.main;
            if (_camera == null) throw new InvalidOperationException("No production camera for plates.");
            foreach (Behaviour behaviour in _camera.GetComponents<Behaviour>())
                if (!(behaviour is Camera) && behaviour.GetType().Name != "UniversalAdditionalCameraData") behaviour.enabled = false;
            _target = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
            _target.Create(); _camera.targetTexture = _target; _camera.orthographic = true;
            var moored = Object.FindObjectsByType<MooredBoat>(FindObjectsSortMode.None)
                .Where(b => b.Owner != null && b.IsPresented).OrderBy(b => b.Owner.Id).ToArray();
            if (moored.Length == 0) throw new InvalidOperationException("No real berth found.");
            _berth = moored[0].transform.position;
            _ambient = moored.Select(b => b.gameObject).ToArray();
            foreach (GameObject boat in _ambient) boat.SetActive(false);
            _boats = new[] { MakeBoat("dory", "DoryIso"), MakeBoat("heavy", "SideDraggerIso") };
            foreach (GameObject boat in _boats) boat.SetActive(false);

            _water = Object.FindFirstObjectByType<WaterSurface>();
            _displaced = Object.FindFirstObjectByType<DisplacedWaterSurface>();
            if (_water == null || _displaced == null) throw new InvalidOperationException("The berth sea is absent.");
            var renderer = _water.GetComponent<Renderer>();
            _waterMaterial = Object.Instantiate(renderer.sharedMaterial);
            _waterMaterial.hideFlags = HideFlags.HideAndDontSave;
            renderer.sharedMaterial = _waterMaterial;
            _afterShader = _waterMaterial.shader;
            _keywords = _waterMaterial.shaderKeywords;
            _displacedMaterial = (Material)typeof(DisplacedWaterSurface).GetField("_displacedMaterial",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_displaced);
            _beforeShader = ShaderUtil.CreateShaderAsset(ReadBeforeShader().Replace(
                "Shader \"HiddenHarbours/Water\"", "Shader \"HiddenHarbours/WaterHullsBaseline\""));
            _beforeShader.hideFlags = HideFlags.HideAndDontSave;
            if (ShaderUtil.ShaderHasError(_beforeShader)) throw new InvalidOperationException("The before sea shader failed to compile.");
            GameServices.Environment = _weather;
        }

        private static GameObject MakeBoat(string name, string visualName)
        {
            var visual = AssetDatabase.LoadAssetAtPath<BoatVisualDef>(
                "Assets/_Project/Data/Boats/Visuals/" + visualName + ".asset");
            if (visual == null) throw new InvalidOperationException("The committed plate visual is absent: " + visualName);
            var root = new GameObject("WaterHullsPlate_" + name) { hideFlags = HideFlags.DontSave };
            root.transform.rotation = Quaternion.Euler(0, 0, -90);
            var rig = BoatHullSkinner.Apply(root, visual, null, new BoatHullSkinner.Options { SkipOars = true });
            if (!rig.Skinned || root.GetComponentInChildren<IsoFacetHullRenderer>() == null)
                throw new InvalidOperationException("The plate hull did not use the facet path: " + name);
            return root;
        }

        private static void BeginCase(int index)
        {
            _case = index; _stageFrames = 0; _stageStart = EditorApplication.timeSinceStartup;
            int hull = index < 12 ? index / 6 : 1;
            bool underway = index < 12 && ((index / 3) % 2) == 1;
            int condition = index < 12 ? index % 3 : 0;
            _weather.Wind = condition == 2 ? new Vector2(-15, -10) : new Vector2(1, .5f);
            _weather.Sea = condition == 2 ? .85f : .05f;
            _weather.Tide = .63f;
            GameServices.Clock.SeekTo(GameServices.SecondsPerDay * (condition == 1 ? 17d : 12d) / 24d);
            GameServices.Clock.TimeScale = 0;
            foreach (GameObject boat in _boats) boat.SetActive(false);
            _boats[hull].transform.position = _berth + (underway ? new Vector3(-8, -28, 0) : Vector3.zero);
            _boats[hull].SetActive(true);
            foreach (var listener in _boats[hull].GetComponentsInChildren<MonoBehaviour>())
                if (listener is IVesselWayListener way) way.OnVesselWayChanged(underway ? VesselWay.UnderWay : VesselWay.Moored);
            _camera.orthographicSize = hull == 0 ? 5 : 13;
            Vector3 centre = _boats[hull].transform.position;
            if (index == 12) centre += new Vector3(-14, -13, 0);
            _camera.transform.position = new Vector3(centre.x, centre.y, _camera.transform.position.z);
            foreach (var bridge in Object.FindObjectsByType<WaveFieldBridge>(FindObjectsSortMode.None))
            { bridge.enabled = false; bridge.enabled = true; }
            PushSea();
        }

        private static void PushSea()
        {
            typeof(WaterSurface).GetMethod("PushUniforms", BindingFlags.Instance | BindingFlags.NonPublic,
                null, Type.EmptyTypes, null).Invoke(_water, null);
            foreach (var sun in Object.FindObjectsByType<DayNightController>(FindObjectsSortMode.None))
                typeof(DayNightController).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(sun, null);
            typeof(DisplacedWaterSurface).GetMethod("SyncUniforms", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(_displaced, new object[] { true });
        }

        private static void Source(bool before)
        {
            Shader shader = before ? _beforeShader : _afterShader;
            _waterMaterial.shader = shader; _waterMaterial.shaderKeywords = _keywords;
            _displacedMaterial.shader = shader; _displacedMaterial.shaderKeywords = _keywords;
            GameServices.Config.HullCausticStrength = before ? 0 : _strength;
            PushSea();
            foreach (var hull in Object.FindObjectsByType<IsoFacetHullRenderer>(FindObjectsSortMode.None)) hull.ApplyPose();
        }

        private static Color32[] Read(RenderTexture target)
        {
            RenderTexture previous = RenderTexture.active;
            var texture = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
            try
            {
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
                texture.Apply();
                return texture.GetPixels32();
            }
            finally { RenderTexture.active = previous; Object.DestroyImmediate(texture); }
        }

        private static void WritePng(string name, Color32[] pixels)
        {
            var texture = new Texture2D(_target.width, _target.height, TextureFormat.RGBA32, false);
            try { texture.SetPixels32(pixels); texture.Apply(); File.WriteAllBytes(Path.Combine(_output, name), texture.EncodeToPNG()); }
            finally { Object.DestroyImmediate(texture); }
        }

        private static void CapturePair(int index)
        {
            int h = index < 12 ? index / 6 : 1;
            bool underway = index < 12 && ((index / 3) % 2) == 1;
            int condition = index < 12 ? index % 3 : 0;
            string name = index == 12 ? "open-sea-berth" : (h == 0 ? "dory" : "heavy")
                + "-" + (underway ? "underway" : "berth") + "-" + new[] { "noon-calm", "low-sun", "blow" }[condition];
            Source(true);
            // Stable feedback for the shipped resolved-image reflection. Repeated renders use dt=0 in foam.
            _camera.Render(); _camera.Render();
            _camera.Render(); Color32[] before = Read(_target);
            Source(false);
            _camera.Render(); Color32[] after = Read(_target);
            var bandTarget = Shader.GetGlobalTexture("_HHHullCausticTex") as RenderTexture;
            if (bandTarget == null) throw new InvalidOperationException("The facet band target was not published.");
            Color32[] band = Read(bandTarget);
            int inside = 0, outside = 0, bandPixels = 0;
            var mask = new Color32[band.Length];
            for (int i = 0; i < band.Length; i++)
            {
                bool allowed = band[i].a >= 128 && band[i].a < 255;
                if (allowed) bandPixels++;
                mask[i] = allowed ? new Color32(255,255,255,255) : new Color32(0,0,0,255);
                bool changed = before[i].r != after[i].r || before[i].g != after[i].g || before[i].b != after[i].b;
                if (changed) { if (allowed) inside++; else outside++; }
            }
            WritePng(name + "-before.png", before); WritePng(name + "-after.png", after);
            WritePng(name + "-band-mask.png", mask);
            var proof = new PairProof { hull = h == 0 ? "dory" : "side-dragger",
                state = underway ? "underway at 3 m/s" : "at berth", hour = GameServices.Clock.HourOfDay,
                wind = _weather.Wind, seaState = _weather.Sea, sha = System.Environment.GetEnvironmentVariable("HH_WATER_HULLS_SHA"),
                beforeSha = Base, name = name, changedInsideBand = inside, changedOutsideBand = outside,
                bandPixels = bandPixels, strength = _strength, width = GameServices.Config.HullCausticBandMeters,
                source = "before: measured base sea shader and band off; after: shared sea shader and band on" };
            _pairs.Add(proof);
            string json = JsonUtility.ToJson(proof, true);
            File.WriteAllText(Path.Combine(_output, name + "-before.json"), json);
            File.WriteAllText(Path.Combine(_output, name + "-after.json"), json);
            File.WriteAllText(Path.Combine(_output, "pixel-change-counts.json"), JsonUtility.ToJson(new PairList { pairs = _pairs.ToArray() }, true));
            if (outside != 0) throw new InvalidOperationException(name + ": " + outside + " pixels changed outside the bands.");
            if ((index == 0 || index == 6) && (bandPixels == 0 || inside == 0))
                throw new InvalidOperationException(name + ": the calm noon band produced no visible light.");
        }

        private static void BeginCost()
        {
            _case = 13; _costPhase = 0; _stageFrames = 0;
            foreach (GameObject boat in _boats) boat.SetActive(false);
            foreach (GameObject boat in _ambient) boat.SetActive(true);
            _weather.Wind = new Vector2(1, .5f); _weather.Sea = .05f;
            GameServices.Clock.SeekTo(GameServices.SecondsPerDay * .5);
            _camera.transform.position = new Vector3(_berth.x + 20, _berth.y - 12, _camera.transform.position.z);
            _camera.orthographicSize = 45;
            var fleet = Object.FindObjectsByType<IsoFacetHullRenderer>(FindObjectsSortMode.None);
            if (fleet.Length == 0) throw new InvalidOperationException("No fleet is visible for the cost measure.");
            Bounds bounds = fleet[0].PosedMesh.GetComponent<Renderer>().bounds;
            foreach (var hull in fleet) bounds.Encapsulate(hull.PosedMesh.GetComponent<Renderer>().bounds);
            _camera.transform.position = new Vector3(bounds.center.x, bounds.center.y, _camera.transform.position.z);
            _camera.orthographicSize = Mathf.Max(bounds.extents.y + 5, bounds.extents.x / (1280f / 720f) + 5);
            Source(false); GameServices.Config.HullCausticStrength = 0;
            _mainThread = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread");
        }

        private static bool CostStep()
        {
            FrameTimingManager.CaptureFrameTimings();
            _stageFrames++;
            if (_stageFrames <= 30) return false;
            double cpu = _mainThread.Valid ? _mainThread.LastValue / 1e6 : 0;
            double gpu = 0;
            if (FrameTimingManager.GetLatestTimings(1, _timings) > 0) gpu = _timings[0].gpuFrameTime;
            (_costPhase == 0 ? _cpuOff : _cpuOn).Add(cpu);
            (_costPhase == 0 ? _gpuOff : _gpuOn).Add(gpu);
            if (_stageFrames < 130) return false;
            if (_costPhase == 0)
            {
                _costPhase = 1; _stageFrames = 0;
                GameServices.Config.HullCausticStrength = _strength;
                return false;
            }
            _mainThread.Dispose();
            var fleet = Object.FindObjectsByType<IsoFacetHullRenderer>(FindObjectsSortMode.None);
            var watch = Stopwatch.StartNew();
            GameServices.Config.HullCausticStrength = 0;
            for (int i = 0; i < 100; i++) foreach (var hull in fleet) hull.ApplyPose();
            double packetOff = watch.Elapsed.TotalMilliseconds / 100;
            GameServices.Config.HullCausticStrength = _strength;
            watch.Restart();
            for (int i = 0; i < 100; i++) foreach (var hull in fleet) hull.ApplyPose();
            double packetOn = watch.Elapsed.TotalMilliseconds / 100;
            var cost = new CostProof { hulls = fleet.Length, width = _target.width, height = _target.height,
                cpuMainThreadOffMs = Median(_cpuOff), cpuMainThreadOnMs = Median(_cpuOn),
                gpuFrameOffMs = Median(_gpuOff), gpuFrameOnMs = Median(_gpuOn),
                gpuTimingAvailable = _gpuOff.Any(v => v > 0) && _gpuOn.Any(v => v > 0),
                mainThreadCounterAvailable = _cpuOff.Any(v => v > 0) && _cpuOn.Any(v => v > 0),
                fleetPoseOffMs = packetOff, fleetPoseOnMs = packetOn,
                retainedDarkTargetBytes = 4 * _target.width * _target.height,
                addedPasses = 0, addedMrtSlots = 0,
                note = "100 settled frames per mode in the batch editor; Main Thread includes editor/runtime overhead. Pose timings are 100 repeated fleet ApplyPose calls. Existing dark MRT is retained per camera for the main overlay; alpha stores the band weight. GPU zero means unavailable, never zero cost." };
            File.WriteAllText(Path.Combine(_output, "fleet-cost.json"), JsonUtility.ToJson(cost, true));
            _camera.Render(); WritePng("fleet-cost-view.png", Read(_target));
            return true;
        }

        private static double Median(List<double> values)
        { var sorted = values.OrderBy(v => v).ToArray(); return sorted[sorted.Length / 2]; }

        private static string ReadBeforeShader()
        {
            var start = new ProcessStartInfo("git", "show " + Base + ":Assets/_Project/Art/Shaders/HiddenHarboursWater.shader")
            { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
            start.EnvironmentVariables["GIT_NO_LAZY_FETCH"] = "1";
            start.EnvironmentVariables["GIT_OPTIONAL_LOCKS"] = "0";
            using Process process = Process.Start(start);
            string source = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd(); process.WaitForExit();
            if (process.ExitCode != 0) throw new InvalidOperationException("Before shader read failed: " + error);
            return source;
        }

        private sealed class Weather : IEnvironmentService, IWaveWindSource
        {
            public Vector2 Wind; public float Sea, Tide;
            public int WorldSeed => 12345;
            public TideProfile ActiveTideProfile { get; set; }
            public EnvironmentSample Sample() => new EnvironmentSample(Wind, Vector2.zero, Tide,
                (SeaState)Mathf.RoundToInt(Sea * 7), 1, Sea);
            public float TideHeightAt(double time) => Tide;
            public WaveWindSample WaveWindAt(double time) => new WaveWindSample(Wind, Sea);
        }
        [Serializable] private sealed class PairProof
        { public string name, hull, state, sha, beforeSha, source; public float hour, seaState, strength, width;
          public Vector2 wind; public int changedInsideBand, changedOutsideBand, bandPixels; }
        [Serializable] private sealed class PairList { public PairProof[] pairs; }
        [Serializable] private sealed class CostProof
        { public int hulls, width, height, retainedDarkTargetBytes, addedPasses, addedMrtSlots;
          public double cpuMainThreadOffMs, cpuMainThreadOnMs, gpuFrameOffMs, gpuFrameOnMs, fleetPoseOffMs, fleetPoseOnMs;
          public bool gpuTimingAvailable, mainThreadCounterAvailable; public string note; }
    }
}
