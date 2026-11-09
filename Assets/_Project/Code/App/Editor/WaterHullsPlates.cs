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
    public static partial class WaterHullsPlates
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
        private static float _veinGain;
        private static readonly bool SameGainControl =
            System.Environment.GetEnvironmentVariable("HH_WATER_HULLS_L4_CONTROL") == "1";
        private static readonly bool Realism =
            System.Environment.GetEnvironmentVariable("HH_WATER_HULLS_REALISM") == "1";
        private static readonly bool BearingStudy =
            System.Environment.GetEnvironmentVariable("HH_WATER_HULLS_BEARING") == "1";
        private static int PairCount => BearingStudy ? 16 : 13;
        private static int _bearingTriangle = -1;
        private static Vector3 _bearingRigNormal;
        private static bool _calibrated, _authoringLocked, _inMotion;
        private static float _gainBeforeTuning, _priorCaptureDelta;
        private static int _motionPose, _motionStartFrame = -1;
        private static readonly int[] MotionCases = { 7, 1, 6 };
        private static readonly List<Color32[]> MotionFrames = new List<Color32[]>();
        private static readonly List<float> MotionTimes = new List<float>();
        private static readonly List<double> MotionGameTimes = new List<double>();
        private static double _motionGameStart;

        public static bool Step(string output)
        {
            if (Time.frameCount == _lastFrame) return false;
            _lastFrame = Time.frameCount;
            if (_camera == null) Initialize(output);
            if (_inMotion) return MotionStep();
            if (_case < 0) { BeginCase(LadderStudy ? 6 : SameGainControl ? 8 : Realism ? 7 : 0); return false; }
            if (_case < PairCount)
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
                if (SameGainControl) { CaptureSameGainBlow(); return true; }
                if (LadderStudy)
                {
                    if (!_calibrated) { MeasurePhysicalRung(); _calibrated = true; }
                    if (_case == 6 || _case == 0 || _case == 7) CaptureGainLadder(_case);
                    CapturePair(_case);
                    int order = Array.IndexOf(LadderOrder, _case);
                    if (order == LadderOrder.Length-1) BeginMotion(0); else BeginCase(LadderOrder[order+1]);
                    return false;
                }
                if (Realism && !_calibrated) { if (BearingStudy) MeasureFixedGainReference(); else CalibrateGain(); _calibrated = true; }
                CapturePair(_case);
                if (Realism && _case == 7 && _pairs.Count == 1) { BeginCase(0); return false; }
                if (Realism && _case == 6) { BeginCase(8); return false; }
                if (_case == PairCount - 1) { if (Realism) BeginMotion(0); else BeginCost(); return false; }
                BeginCase(_case + 1);
                return false;
            }
            return CostStep();
        }

        private static void Initialize(string output)
        {
            _output = output;
            _strength = GameServices.Config.HullCausticStrength;
            _veinGain = GameServices.Config.HullCausticVeinGain;
            _gainBeforeTuning = _veinGain;
            if (Realism)
            {
                AssetDatabase.DisallowAutoRefresh();
                EditorApplication.LockReloadAssemblies();
                _authoringLocked = true;
            }
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
            if (LadderStudy) InitializeLadder();
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
            _geometricMask = null;
            int hull = index < 12 ? index / 6 : 1;
            bool underway = index < 12 && ((index / 3) % 2) == 1;
            int condition = index < 12 ? index % 3 : 0;
            _weather.Wind = condition == 2 ? new Vector2(-15, -10) : new Vector2(1, .5f);
            _weather.Sea = index == 15 ? 0 : condition == 2 ? .85f : .05f;
            if (index == 15) _weather.Wind = Vector2.zero;
            foreach (GameObject boat in _boats) boat.GetComponent<MeshHullDriver>().enabled = true;
            _weather.Tide = .63f;
            float hour = index >= 13 ? SolarHourAtElevation(20, true) : Realism ? SolarHour(condition == 1) : condition == 1 ? 17 : 12;
            GameServices.Clock.SeekTo(GameServices.SecondsPerDay * hour / 24d);
            GameServices.Clock.TimeScale = 0;
            foreach (GameObject boat in _boats) boat.SetActive(false);
            _boats[hull].transform.position = _berth + (underway ? new Vector3(-8, -28, 0) : Vector3.zero);
            _boats[hull].SetActive(true);
            foreach (var listener in _boats[hull].GetComponentsInChildren<MonoBehaviour>())
                if (listener is IVesselWayListener way) way.OnVesselWayChanged(underway ? VesselWay.UnderWay : VesselWay.Moored);
            _camera.orthographicSize = hull == 0 ? 5 : 13;
            if (Realism && hull == 0)
            {
                var zoom = GameServices.Config.PlayerZoom.Sanitized();
                int step = CameraZoomPolicy.StepForWorldHeight(zoom.DefaultWorldHeightMeters, CameraFollow.AssetsPPU, _target.height);
                _camera.orthographicSize = CameraFollow.OrthoSizeForWorldHeight(
                    CameraZoomPolicy.WorldHeightForStep(step, CameraFollow.AssetsPPU, _target.height));
            }
            Vector3 centre = _boats[hull].transform.position;
            if (index == 12) centre += new Vector3(-14, -13, 0);
            _camera.transform.position = new Vector3(centre.x, centre.y, _camera.transform.position.z);
            foreach (var bridge in Object.FindObjectsByType<WaveFieldBridge>(FindObjectsSortMode.None))
            { bridge.enabled = false; bridge.enabled = true; }
            PushSea();
            if (index >= 13) PoseBearingFace(index);
        }

        private static void PoseBearingFace(int index)
        {
            _boats[1].GetComponent<MeshHullDriver>().enabled = false;
            var hull = _boats[1].GetComponentInChildren<IsoFacetHullRenderer>();
            hull.transform.rotation = Quaternion.identity;
            hull.RollDegrees = hull.PitchDegrees = hull.HeadingDirUnits = 0;
            hull.ApplyPose();
            var mesh = hull.PosedMesh.GetComponent<MeshFilter>().sharedMesh;
            if (_bearingTriangle < 0)
            {
                Vector3[] vertices = mesh.vertices, normals = mesh.normals; int[] triangles = mesh.triangles;
                var attrs = new List<Vector4>(); mesh.GetUVs(0, attrs);
                var block = new MaterialPropertyBlock(); hull.PosedMesh.GetComponent<Renderer>().GetPropertyBlock(block);
                Vector4 plane = block.GetVector("_HHHullCausticPlane");
                float width = block.GetVector("_HHHullCausticSettings").x;
                float area = 0;
                for (int t = 0; t < triangles.Length; t += 3)
                {
                    int a = triangles[t], b = triangles[t+1], c = triangles[t+2];
                    if (Mathf.Abs(normals[a].z) >= .25f || Mathf.RoundToInt(attrs[a].w) == 1) continue;
                    float Height(int v) { Vector3 p = hull.PosedMesh.TransformPoint(vertices[v]); return Vector4.Dot(plane, new Vector4(p.x,p.y,p.z,1)); }
                    float ha=Height(a), hb=Height(b), hc=Height(c);
                    if (Mathf.Min(ha,Mathf.Min(hb,hc)) >= width || Mathf.Max(ha,Mathf.Max(hb,hc)) <= 0) continue;
                    float candidate = Vector3.Cross(vertices[b]-vertices[a],vertices[c]-vertices[a]).sqrMagnitude;
                    if (candidate <= area) continue;
                    area = candidate; _bearingTriangle = t / 3; _bearingRigNormal = normals[a];
                }
                if (_bearingTriangle < 0) throw new InvalidOperationException("No production hull side crosses the receiving band for bearing plates.");
            }
            // Keep this same physical hull face visible from the production camera, east/west with a south lean.
            Vector2 target = new Vector2(index == 13 ? 1 : -1, -.35f).normalized;
            hull.HeadingDirUnits = (Mathf.Atan2(target.y,target.x) - Mathf.Atan2(_bearingRigNormal.y,_bearingRigNormal.x)) * Mathf.Rad2Deg / 45;
            hull.ApplyPose();
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
            if (LadderStudy) { CaptureLadderPair(index); return; }
            int h = index < 12 ? index / 6 : 1;
            bool underway = index < 12 && ((index / 3) % 2) == 1;
            int condition = index < 12 ? index % 3 : 0;
            string name = index >= 13 ? new[] { "bearing-east-morning", "bearing-west-morning", "bearing-west-flat-calm" }[index-13]
                : index == 12 ? "open-sea-berth" : (h == 0 ? "dory" : "heavy")
                + "-" + (underway ? "underway" : "berth") + "-" + new[] { "noon-calm", "low-sun", "blow" }[condition];
            Source(true);
            // Stable feedback for the shipped resolved-image reflection. Repeated renders use dt=0 in foam.
            _camera.Render(); _camera.Render();
            _camera.Render(); Color32[] before = Read(_target);
            Source(false);
            _camera.Render(); Color32[] after = Read(_target);
            var bandTarget = BandTarget();
            if (bandTarget == null) throw new InvalidOperationException("The facet band target was not published.");
            Color32[] band = Read(bandTarget);
            // The gain must not alter even one byte of the existing dark target.
            GameServices.Config.HullCausticVeinGain = 1;
            foreach (var hull in Object.FindObjectsByType<IsoFacetHullRenderer>(FindObjectsSortMode.None)) hull.ApplyPose();
            _camera.Render();
            Color32[] priorBand = Read(BandTarget());
            for (int i = 0; i < band.Length; i++)
                if (!band[i].Equals(priorBand[i]))
                    throw new InvalidOperationException(name + ": vein gain changed the dark target.");
            GameServices.Config.HullCausticVeinGain = _veinGain;
            foreach (var hull in Object.FindObjectsByType<IsoFacetHullRenderer>(FindObjectsSortMode.None)) hull.ApplyPose();
            _camera.Render();
            int inside = 0, outside = 0, bandPixels = 0, maxOutsideDelta = 0;
            double shiftSum = 0, maxShift = 0;
            var mask = new Color32[band.Length];
            for (int i = 0; i < band.Length; i++)
            {
                bool allowed = band[i].a >= 128 && band[i].a < 255;
                if (allowed) bandPixels++;
                mask[i] = allowed ? new Color32(255,255,255,255) : new Color32(0,0,0,255);
                bool changed = before[i].r != after[i].r || before[i].g != after[i].g || before[i].b != after[i].b;
                if (changed)
                {
                    if (allowed) inside++;
                    else { outside++; maxOutsideDelta = Math.Max(maxOutsideDelta,
                        Math.Max(Math.Abs(after[i].r - before[i].r), Math.Max(Math.Abs(after[i].g - before[i].g), Math.Abs(after[i].b - before[i].b)))); }
                }
                if (allowed)
                {
                    double shift = LumaShift(before[i], after[i]);
                    shiftSum += shift; maxShift = Math.Max(maxShift, shift);
                }
            }
            WritePng(name + "-before.png", before); WritePng(name + "-after.png", after);
            WritePng(name + "-band-mask.png", mask);
            var proof = new PairProof { index = index, hull = h == 0 ? "dory" : "side-dragger",
                state = underway ? "underway at 3 m/s" : "at berth", hour = GameServices.Clock.HourOfDay,
                wind = _weather.Wind, seaState = _weather.Sea, sha = System.Environment.GetEnvironmentVariable("HH_WATER_HULLS_SHA"),
                beforeSha = Base, name = name, changedInsideBand = inside, changedOutsideBand = outside,
                bandPixels = bandPixels, strength = _strength, width = HullCausticLighting.BandWidth(GameServices.Config, HullCausticLighting.LiveChop),
                maxOutsideDelta = maxOutsideDelta, chop = HullCausticLighting.LiveChop,
                widthBase = GameServices.Config.HullCausticBandBaseMeters, widthPerChop = GameServices.Config.HullCausticBandPerChopMeters,
                sunElevation = Shader.GetGlobalFloat("_SunElevation"),
                sunBearing = Shader.GetGlobalVector("_SunDir"), shadowStrength = Shader.GetGlobalFloat("_ShadowStrength"),
                awayFloor = GameServices.Config.HullCausticAwayFloor,
                sourceTree = System.Environment.GetEnvironmentVariable("HH_WATER_HULLS_SOURCE_TREE"),
                tuningVeinGainBefore = _gainBeforeTuning, tuningVeinGainAfter = _veinGain,
                f0 = GameServices.Config.HullCausticFresnelF0,
                referenceElevationDegrees = GameServices.Config.HullCausticReferenceElevationDegrees,
                fresnelPower = GameServices.Config.HullCausticFresnelPower, chopDimming = GameServices.Config.HullCausticChopDimming,
                maxUpwardNormal = GameServices.Config.HullCausticMaxUpwardNormal,
                meanShift = bandPixels > 0 ? shiftSum / bandPixels : 0, maxShift = maxShift,
                darkTargetUnchangedByGain = true,
                source = "before: measured base sea shader and band off; after: shared sea shader and band on" };
            if (index >= 13)
            {
                var hull = _boats[1].GetComponentInChildren<IsoFacetHullRenderer>();
                var block = new MaterialPropertyBlock(); hull.PosedMesh.GetComponent<Renderer>().GetPropertyBlock(block);
                Vector4 basis = block.GetVector("_HHHullCausticBearing");
                Vector3 n = hull.PosedMesh.localToWorldMatrix.MultiplyVector(_bearingRigNormal).normalized;
                proof.witnessFaceNormal = new Vector2(n.x, n.y*basis.x+n.z*basis.y).normalized;
                proof.witnessFacing = HullCausticLighting.Facing(proof.witnessFaceNormal,
                    new Vector2(proof.sunBearing.x,proof.sunBearing.y), proof.awayFloor, proof.chop);
                proof.witnessTriangle = _bearingTriangle; proof.headingDirUnits = hull.HeadingDirUnits;
                proof.bearingFixture = "same production heavy hull and physical face; east/west bearing with south lean for camera visibility; 20-degree morning; west flat calm pins zero floor";
                if (index == 13 && proof.witnessFaceNormal.x <= 0 || index >= 14 && proof.witnessFaceNormal.x >= 0)
                    throw new InvalidOperationException("Bearing witness did not face the named compass side.");
                if (index >= 14 && proof.witnessFacing != proof.awayFloor * proof.chop)
                    throw new InvalidOperationException("Away-facing witness did not take only the chop floor.");
            }
            _pairs.Add(proof);
            string json = JsonUtility.ToJson(proof, true);
            File.WriteAllText(Path.Combine(_output, name + "-before.json"), json);
            File.WriteAllText(Path.Combine(_output, name + "-after.json"), json);
            File.WriteAllText(Path.Combine(_output, name + "-band-mask.json"), json);
            File.WriteAllText(Path.Combine(_output, "pixel-change-counts.json"), JsonUtility.ToJson(new PairList { pairs = _pairs.ToArray() }, true));
            WriteBandShifts();
            // seat ruling l6, 11-07: the shared vein path rounds differently from the inline original in the blow sea; max 2/255
            if (maxOutsideDelta > 2) throw new InvalidOperationException(name + ": outside max channel delta " + maxOutsideDelta);
            if (Realism && index == 7)
            {
                var calibration = JsonUtility.FromJson<CalibrationProof>(File.ReadAllText(Path.Combine(_output, BearingStudy ? "l7-reference-cap.json" : "l5-calibration-final.json")));
                if (maxShift > calibration.cap + 1e-7)
                    throw new InvalidOperationException("Reference plate peak exceeds the derived Fresnel cap.");
            }
            if (!Realism && (index == 0 || index == 6) && (bandPixels == 0 || inside == 0))
                throw new InvalidOperationException(name + ": the calm noon band produced no visible light.");
            if (Realism ? index == 1 || index == 6 || index == 7 : index == 0 || index == 6)
                WriteBandCrop(name + "-band-crop", before, after, mask, json);
            if (!Realism && index == 0) CaptureOnFootCrop();
        }

        private static float SolarHour(bool reference)
        {
            var sun = Object.FindFirstObjectByType<DayNightController>();
            var profile = sun != null ? typeof(DayNightController).GetField("_profile",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(sun) as DayNightProfile : null;
            float sunrise = profile != null ? profile.SunriseHour : DaylightHours.DefaultSunriseHour;
            float sunset = profile != null ? profile.SunsetHour : DaylightHours.DefaultSunsetHour;
            float noon = (sunrise + sunset) * .5f;
            return reference ? SolarHourAtElevation(GameServices.Config.HullCausticReferenceElevationDegrees, false) : noon;
        }

        private static float SolarHourAtElevation(float degrees, bool morning)
        {
            var sun = Object.FindFirstObjectByType<DayNightController>();
            var profile = sun != null ? typeof(DayNightController).GetField("_profile",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(sun) as DayNightProfile : null;
            float rise = profile != null ? profile.SunriseHour : DaylightHours.DefaultSunriseHour;
            float set = profile != null ? profile.SunsetHour : DaylightHours.DefaultSunsetHour;
            return (rise + set) * .5f + (morning ? -1 : 1) * (set - rise) * .5f * (1 - degrees / 90f);
        }

        private static void MeasureFixedGainReference()
        {
            Source(true); _camera.Render(); _camera.Render(); _camera.Render(); Color32[] before = Read(_target);
            Source(false); _camera.Render(); Color32[] band = Read(BandTarget()), after = Read(_target);
            int[] lit = {390,311,413,328}, shaded = {303,314,316,330};
            double litLuma = FacetLuma(before,band,lit), shadeLuma = FacetLuma(before,band,shaded);
            var config = GameServices.Config;
            float f = CausticMath.FresnelAtDegrees(config.HullCausticReferenceElevationDegrees,config.HullCausticFresnelF0,config.HullCausticFresnelPower);
            double cap = f*(litLuma-shadeLuma), peak = 0;
            for(int i=0;i<band.Length;i++) if(band[i].a>=128 && band[i].a<255) peak = Math.Max(peak,LumaShift(before[i],after[i]));
            if(cap<=0 || peak>cap) throw new InvalidOperationException("Unchanged l5 gain exceeds the measured reference cap with l7 factors.");
            var proof = new CalibrationProof { lawSha=System.Environment.GetEnvironmentVariable("HH_WATER_HULLS_SHA"),
                sourceTree=System.Environment.GetEnvironmentVariable("HH_WATER_HULLS_SOURCE_TREE"),
                editorPid=Process.GetCurrentProcess().Id, frame=Time.frameCount, hour=GameServices.Clock.HourOfDay,
                gainBefore=_veinGain,gain=_veinGain,cap=cap,referenceReflectance=f,litRoi=lit,shadedRoi=shaded,
                sunlitLuma=litLuma,shadedLuma=shadeLuma,peakAtCeiling=peak,peakAtDerivedGain=peak,
                sunElevation=Shader.GetGlobalFloat("_SunElevation"),sunBearing=Shader.GetGlobalVector("_SunDir"),
                shadowStrength=Shader.GetGlobalFloat("_ShadowStrength") };
            File.WriteAllText(Path.Combine(_output,"l7-reference-cap.json"),JsonUtility.ToJson(proof,true));
        }

        public static void ReleaseAuthoringLocks()
        {
            if (!_authoringLocked) return;
            _authoringLocked = false;
            AssetDatabase.AllowAutoRefresh();
            EditorApplication.UnlockReloadAssemblies();
        }

        private static void CalibrateGain()
        {
            var config = GameServices.Config;
            float expectedSun = Mathf.Sin(config.HullCausticReferenceElevationDegrees * Mathf.Deg2Rad);
            if (Mathf.Abs(Shader.GetGlobalFloat("_SunElevation") - expectedSun) > 1e-4f)
                throw new InvalidOperationException("Reference plate did not reach the configured sun elevation.");
            Source(true); _camera.Render(); _camera.Render(); _camera.Render();
            Color32[] before = Read(_target);
            Source(false); _camera.Render();
            Color32[] band = Read(BandTarget());
            // Two visible cabin facets, above the hull band, chosen before the numerical solve.
            int[] lit = { 390, 311, 413, 328 }, shaded = { 303, 314, 316, 330 };
            double litLuma = FacetLuma(before, band, lit), shadeLuma = FacetLuma(before, band, shaded);
            float referenceF = CausticMath.FresnelAtDegrees(config.HullCausticReferenceElevationDegrees,
                config.HullCausticFresnelF0, config.HullCausticFresnelPower);
            double cap = referenceF * (litLuma - shadeLuma);
            if (cap <= 0) throw new InvalidOperationException("The named sunlit/shaded facet samples do not define a positive Fresnel cap.");
            double Peak(float gain)
            {
                config.HullCausticVeinGain = gain;
                foreach (var hull in Object.FindObjectsByType<IsoFacetHullRenderer>(FindObjectsSortMode.None)) hull.ApplyPose();
                _camera.Render(); Color32[] image = Read(_target);
                double peak = 0;
                for (int i = 0; i < image.Length; i++)
                    if (band[i].a >= 128 && band[i].a < 255) peak = Math.Max(peak, LumaShift(before[i], image[i]));
                return peak;
            }
            float low = 0, high = _gainBeforeTuning;
            if (high > 4.4f) throw new InvalidOperationException("The calibration ceiling exceeds the owner's 4.4 bound.");
            if (Peak(0) > cap) throw new InvalidOperationException("The zero-gain comparison already exceeds the reference luminance cap.");
            double peakAtCeiling = Peak(high);
            if (peakAtCeiling <= cap) low = high;
            else for (int i = 0; i < 12; i++)
            {
                float middle = (low + high) * .5f;
                if (Peak(middle) <= cap) low = middle; else high = middle;
            }
            _veinGain = low; config.HullCausticVeinGain = low;
            double peakAtGain = Peak(low);
            var ready = new CalibrationProof {
                lawSha = System.Environment.GetEnvironmentVariable("HH_WATER_HULLS_SHA"),
                sourceTree = System.Environment.GetEnvironmentVariable("HH_WATER_HULLS_SOURCE_TREE"),
                editorPid = Process.GetCurrentProcess().Id, gainBefore = _gainBeforeTuning, gain = low,
                cap = cap, sunlitLuma = litLuma, shadedLuma = shadeLuma, referenceReflectance = referenceF,
                peakAtCeiling = peakAtCeiling, peakAtDerivedGain = peakAtGain,
                litRoi = lit, shadedRoi = shaded, frame = Time.frameCount, hour = GameServices.Clock.HourOfDay,
                sunElevation = Shader.GetGlobalFloat("_SunElevation"),
                sunBearing = Shader.GetGlobalVector("_SunDir"), shadowStrength = Shader.GetGlobalFloat("_ShadowStrength"), capBinding = peakAtCeiling > cap };
            File.WriteAllText(Path.Combine(_output, "l5-calibration-ready.json"), JsonUtility.ToJson(ready, true));
            WritePng("heavy-low-sun-calibration-before.png", before);
            File.WriteAllText(Path.Combine(_output, "heavy-low-sun-calibration-before.json"), JsonUtility.ToJson(ready, true));
            // Pause this same game frame while the parent commits only the measured gain data.
            // Auto-refresh/reload are locked; the live config already equals the final data value.
            string goPath = Path.Combine(_output, "l5-calibration-go.json");
            var deadline = Stopwatch.StartNew();
            while (deadline.Elapsed.TotalSeconds < 90)
            {
                if (File.Exists(goPath))
                {
                    var go = JsonUtility.FromJson<CalibrationGo>(File.ReadAllText(goPath));
                    if (go.editorPid == ready.editorPid && go.lawSha == ready.lawSha
                        && go.finalSha != null && go.finalSha.Length == 40 && go.gain == ready.gain)
                    {
                        System.Environment.SetEnvironmentVariable("HH_WATER_HULLS_SHA", go.finalSha);
                        File.WriteAllText(Path.Combine(_output, "l5-calibration-final.json"), JsonUtility.ToJson(ready, true));
                        return;
                    }
                }
                System.Threading.Thread.Sleep(25);
            }
            throw new TimeoutException("Measured gain was not committed within the calibration handshake.");
        }

        private static double FacetLuma(Color32[] before, Color32[] band, int[] roi)
        {
            double sum = 0; int count = 0;
            for (int y = roi[1]; y < roi[3]; y++) for (int x = roi[0]; x < roi[2]; x++)
            {
                int i = (_target.height - 1 - y) * _target.width + x;
                if (band[i].a >= 128 && band[i].a < 255)
                    throw new InvalidOperationException("A facet calibration sample overlaps the hull light band.");
                Color32 c = before[i]; sum += .299 * c.r + .587 * c.g + .114 * c.b; count++;
            }
            return sum / count;
        }

        private static void BeginMotion(int pose)
        {
            if (pose == 0) { _priorCaptureDelta = Time.captureDeltaTime; Time.captureDeltaTime = 1f / (8 * 7); }
            _inMotion = true; _motionPose = pose; _motionStartFrame = -1;
            MotionFrames.Clear(); MotionTimes.Clear(); MotionGameTimes.Clear();
            BeginCase(MotionCases[pose]);
        }

        private static bool MotionStep()
        {
            if (++_stageFrames < 60 || EditorApplication.timeSinceStartup - _stageStart < 3
                || ShaderUtil.anythingCompiling) return false;
            if (_motionStartFrame < 0) { _motionStartFrame = Time.frameCount; _motionGameStart = GameServices.Clock.TotalSeconds; }
            if (Time.frameCount < _motionStartFrame + MotionFrames.Count * 8) return false;
            GameServices.Clock.SeekTo(_motionGameStart + MotionFrames.Count / 7d);
            Source(false); _camera.Render();
            MotionFrames.Add(Read(_target)); MotionTimes.Add(Time.time); MotionGameTimes.Add(GameServices.Clock.TotalSeconds);
            string[] names = { "heavy-berth-low-sun", "dory-berth-low-sun", "heavy-berth-noon-calm" };
            string name = names[_motionPose]; int frame = MotionFrames.Count - 1;
            Directory.CreateDirectory(Path.Combine(_output, "motion", "raw"));
            WritePng("motion/raw/" + name + "-" + frame.ToString("D2") + ".png", MotionFrames[frame]);
            Color32[] band = Read(BandTarget());
            var mask = new Color32[band.Length];
            for (int i = 0; i < band.Length; i++) mask[i] = band[i].a >= 128 && band[i].a < 255
                ? new Color32(255,255,255,255) : new Color32(0,0,0,255);
            if (LadderStudy) { _geometricMask = null; mask = GeometricMask(); }
            WritePng("motion/raw/" + name + "-" + frame.ToString("D2") + "-mask.png", mask);
            if (MotionFrames.Count < 8) return false;
            File.WriteAllText(Path.Combine(_output, "motion", name + ".json"), JsonUtility.ToJson(new MotionProof {
                name = name, sha = System.Environment.GetEnvironmentVariable("HH_WATER_HULLS_SHA"),
                hour = GameServices.Clock.HourOfDay, wind = _weather.Wind, gain = _veinGain,
                times = MotionTimes.ToArray(), gameSeconds = MotionGameTimes.ToArray(),
                width = HullCausticLighting.BandWidth(GameServices.Config, HullCausticLighting.LiveChop),
                chop = HullCausticLighting.LiveChop, sunBearing = Shader.GetGlobalVector("_SunDir"),
                shadowStrength = Shader.GetGlobalFloat("_ShadowStrength"), nominalSeconds = 1, gifFps = 8 }, true));
            if (_motionPose < 2) BeginMotion(_motionPose + 1);
            else { _inMotion = false; Time.captureDeltaTime = _priorCaptureDelta; BeginCost(); }
            return false;
        }

        private static void CaptureSameGainBlow()
        {
            // Match CapturePair's settled stage and synchronous three-render/one-render sequence.
            // Both sides retain the shared sea, enabled band and the committed vein gain.
            Source(false);
            _camera.Render(); _camera.Render(); _camera.Render();
            Color32[] first = Read(_target);
            int frameA = Time.frameCount, renderedA = Time.renderedFrameCount;
            float timeA = Time.time;
            Source(false);
            _camera.Render();
            Color32[] second = Read(_target), band = Read(BandTarget());
            var proof = new SameGainProof {
                sha = System.Environment.GetEnvironmentVariable("HH_WATER_HULLS_SHA"),
                hull = "side-dragger", state = "at berth", hour = GameServices.Clock.HourOfDay,
                wind = _weather.Wind, seaState = _weather.Sea, gainA = _veinGain, gainB = _veinGain,
                frameA = frameA, frameB = Time.frameCount, renderedFrameA = renderedA,
                renderedFrameB = Time.renderedFrameCount, timeA = timeA, timeB = Time.time,
                source = "shared sea and enabled band at the same gain on both sides; original synchronous 3-render/1-render sequence after one 60-frame/3-second stage settle" };
            int minX = _target.width, minY = _target.height, maxX = -1, maxY = -1;
            var samples = new List<PixelSample>();
            var mask = new Color32[band.Length];
            for (int i = 0; i < first.Length; i++)
            {
                bool allowed = band[i].a >= 128 && band[i].a < 255;
                mask[i] = allowed ? new Color32(255,255,255,255) : new Color32(0,0,0,255);
                if (allowed) { proof.bandPixels++; continue; }
                int delta = Math.Max(Math.Abs(second[i].r - first[i].r),
                    Math.Max(Math.Abs(second[i].g - first[i].g), Math.Abs(second[i].b - first[i].b)));
                if (delta == 0) continue;
                proof.changedOutside++; proof.maxChannelDelta = Math.Max(proof.maxChannelDelta, delta);
                int x = i % _target.width, y = _target.height - 1 - i / _target.width;
                minX = Math.Min(minX, x); minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x); maxY = Math.Max(maxY, y);
                if (samples.Count < 8) samples.Add(new PixelSample { x = x, y = y, first = first[i], second = second[i] });
            }
            proof.bbox = proof.changedOutside > 0 ? new[] { minX, minY, maxX, maxY } : Array.Empty<int>();
            proof.samples = samples.ToArray();
            string json = JsonUtility.ToJson(proof, true);
            const string name = "heavy-berth-blow-same-gain";
            WritePng(name + "-a.png", first); WritePng(name + "-b.png", second);
            WritePng(name + "-band-mask.png", mask);
            File.WriteAllText(Path.Combine(_output, name + ".json"), json);
            foreach (string suffix in new[] { "a", "b", "band-mask" })
                File.WriteAllText(Path.Combine(_output, name + "-" + suffix + ".json"), json);
            File.AppendAllText(Path.Combine(_output, "band-shift.txt"), FormattableString.Invariant(
                $"L4 SAME-GAIN heavy-berth-blow | gain {proof.gainA} / {proof.gainB} | band px {proof.bandPixels} | changed outside {proof.changedOutside} | max channel delta {proof.maxChannelDelta} | frames {proof.frameA}/{proof.frameB} | times {proof.timeA}/{proof.timeB}\n"));
            if (proof.changedOutside == 0)
                throw new InvalidOperationException("L4 SAME-GAIN ZERO: outside-band images are byte-identical; do not amend the gate (ruling 2b).");
            if (proof.maxChannelDelta > 2)
                throw new InvalidOperationException("L4 SAME-GAIN UNSETTLED: outside max channel delta "
                    + proof.maxChannelDelta + ", count " + proof.changedOutside + " (ruling 2c).");
        }

        private static double LumaShift(Color32 before, Color32 after)
            => Math.Abs(.299 * (after.r - before.r) + .587 * (after.g - before.g) + .114 * (after.b - before.b));

        private static void WriteBandShifts()
        {
            if (LadderStudy) { WriteLadderBandShifts(); return; }
            var lines = new List<string> {
                "Exact RGB changed-pixel counts. Shift = abs(0.299*dR + 0.587*dG + 0.114*dB), in 0..255 units; mean over ALL band pixels.",
                "HullCausticVeinGain: " + _gainBeforeTuning + " -> " + _veinGain + "; HullCausticStrength unchanged " + _strength,
                "Fresnel F0=" + GameServices.Config.HullCausticFresnelF0 + "; reference degrees=" + GameServices.Config.HullCausticReferenceElevationDegrees
                    + "; power=" + GameServices.Config.HullCausticFresnelPower + "; chop dimming=" + GameServices.Config.HullCausticChopDimming
                    + "; hull slope=clamp01(1-absUp/" + GameServices.Config.HullCausticMaxUpwardNormal + ")",
                "Live reach = base " + GameServices.Config.HullCausticBandBaseMeters + " + per-chop " + GameServices.Config.HullCausticBandPerChopMeters + " * live chop; width and chop recorded per row.",
                "name | band px | changed inside | mean shift | max shift | changed outside | max outside delta | width m | chop | sun bearing x,y | ShadowStrength | dark RGB unchanged" };
            foreach (PairProof pair in _pairs.OrderBy(p => p.index))
                lines.Add(FormattableString.Invariant($"{pair.name} | {pair.bandPixels} | {pair.changedInsideBand} | {pair.meanShift:F4} | {pair.maxShift:F4} | {pair.changedOutsideBand} | {pair.maxOutsideDelta} | {pair.width:F4} | {pair.chop:F4} | {pair.sunBearing.x:F6},{pair.sunBearing.y:F6} | {pair.shadowStrength:F6} | {pair.darkRgbUnchanged}"));
            File.WriteAllLines(Path.Combine(_output, "band-shift.txt"), lines);
        }

        private static void WriteBandCrop(string name, Color32[] before, Color32[] after, Color32[] mask, string json)
        {
            int minX = _target.width, minY = _target.height, maxX = -1, maxY = -1;
            for (int y = 0; y < _target.height; y++) for (int x = 0; x < _target.width; x++)
                if (mask[y * _target.width + x].r > 0)
                { minX = Math.Min(minX, x); minY = Math.Min(minY, y); maxX = Math.Max(maxX, x); maxY = Math.Max(maxY, y); }
            bool emptyWeightedBand = maxX < 0;
            if (emptyWeightedBand)
            {
                // A faint/noon band may quantize to zero. Keep its native waterline context visible.
                int hullIndex = _case < 12 ? _case / 6 : 1;
                Bounds bounds = _boats[hullIndex].GetComponentInChildren<IsoFacetHullRenderer>().PosedMesh.GetComponent<Renderer>().bounds;
                Vector3 lo = _camera.WorldToScreenPoint(bounds.min), hi = _camera.WorldToScreenPoint(bounds.max);
                minX = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(lo.x, hi.x)), 0, _target.width - 1);
                maxX = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(lo.x, hi.x)), minX, _target.width - 1);
                minY = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(lo.y, hi.y)), 0, _target.height - 1);
                maxY = Mathf.Clamp(Mathf.CeilToInt((lo.y + hi.y) * .5f), minY, _target.height - 1);
            }
            minX = Math.Max(0, minX - 8); minY = Math.Max(0, minY - 8);
            maxX = Math.Min(_target.width - 1, maxX + 8); maxY = Math.Min(_target.height - 1, maxY + 8);
            int width = maxX - minX + 1, height = maxY - minY + 1;
            var pixels = new Color32[width * height * 2];
            for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
            {
                int source = (minY + y) * _target.width + minX + x;
                pixels[y * width + x] = after[source];
                pixels[(y + height) * width + x] = before[source];
            }
            var texture = new Texture2D(width, height * 2, TextureFormat.RGBA32, false);
            try { texture.SetPixels32(pixels); texture.Apply(); File.WriteAllBytes(Path.Combine(_output, name + ".png"), texture.EncodeToPNG()); }
            finally { Object.DestroyImmediate(texture); }
            File.WriteAllText(Path.Combine(_output, name + ".json"), json);
            File.WriteAllText(Path.Combine(_output, name + "-layout.txt"),
                $"Native pixels, no rescale. BEFORE above AFTER. Source bottom-left crop x={minX}, y={minY}, width={width}, height={height}. Camera ortho={_camera.orthographicSize}. Empty weighted band={emptyWeightedBand}; if empty, lower hull bounds provide waterline context.");
        }

        private static void CaptureOnFootCrop()
        {
            float priorOrtho = _camera.orthographicSize;
            var zoom = GameServices.Config.PlayerZoom.Sanitized();
            int ppu = CameraFollow.AssetsPPU;
            int step = CameraZoomPolicy.StepForWorldHeight(zoom.DefaultWorldHeightMeters, ppu, _target.height);
            _camera.orthographicSize = CameraFollow.OrthoSizeForWorldHeight(
                CameraZoomPolicy.WorldHeightForStep(step, ppu, _target.height));
            Source(true); _camera.Render(); _camera.Render(); _camera.Render();
            Color32[] before = Read(_target);
            Source(false); _camera.Render(); Color32[] after = Read(_target), band = Read(BandTarget());
            var mask = new Color32[band.Length]; double sum = 0, max = 0; int count = 0, inside = 0, outside = 0;
            for (int i = 0; i < band.Length; i++)
            {
                bool allowed = band[i].a >= 128 && band[i].a < 255;
                mask[i] = allowed ? new Color32(255,255,255,255) : new Color32(0,0,0,255);
                if (allowed) { count++; double shift = LumaShift(before[i], after[i]); sum += shift; max = Math.Max(max, shift);
                    if (before[i].r != after[i].r || before[i].g != after[i].g || before[i].b != after[i].b) inside++; }
                else if (before[i].r != after[i].r || before[i].g != after[i].g || before[i].b != after[i].b) outside++;
            }
            if (outside != 0) throw new InvalidOperationException("Shipped on-foot dory crop changed outside the band.");
            string json = JsonUtility.ToJson(new PairProof { name = "dory-noon-calm-shipped-on-foot", hull = "dory", state = "at berth; shipped on-foot zoom",
                sha = System.Environment.GetEnvironmentVariable("HH_WATER_HULLS_SHA"), beforeSha = Base, hour = 12, wind = _weather.Wind,
                strength = _strength, width = GameServices.Config.HullCausticBandMeters,
                tuningVeinGainBefore = 1, tuningVeinGainAfter = _veinGain, bandPixels = count, changedInsideBand = inside, changedOutsideBand = outside,
                seaState = _weather.Sea,
                meanShift = count > 0 ? sum / count : 0, maxShift = max,
                source = "Production camera orientation and shipped zoom policy at 1280x720; native crop, before above after." }, true);
            WritePng("dory-noon-calm-shipped-on-foot-before.png", before);
            WritePng("dory-noon-calm-shipped-on-foot-after.png", after);
            WritePng("dory-noon-calm-shipped-on-foot-band-mask.png", mask);
            foreach (string suffix in new[] { "before", "after", "band-mask" })
                File.WriteAllText(Path.Combine(_output, "dory-noon-calm-shipped-on-foot-" + suffix + ".json"), json);
            WriteBandCrop("dory-noon-calm-shipped-on-foot-band-crop", before, after, mask, json);
            _camera.orthographicSize = priorOrtho;
            Source(false); _camera.Render();
        }

        private static void BeginCost()
        {
            _case = PairCount; _costPhase = 0; _stageFrames = 0;
            foreach (GameObject boat in _boats) boat.SetActive(false);
            foreach (GameObject boat in _ambient) boat.SetActive(true);
            _weather.Wind = new Vector2(1, .5f); _weather.Sea = .05f;
            GameServices.Clock.SeekTo(GameServices.SecondsPerDay * (Realism ? SolarHour(false) : 12) / 24d);
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

        private static RenderTexture BandTarget()
        {
            // Command-buffer globals need not update Shader.GetGlobalTexture's CPU property sheet.
            // Read the actual retained target owned by this camera, without changing the renderer.
            const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
            var diagnostic = new List<string> {
                "strength=" + _strength, "configuredStrength=" + GameServices.Config.HullCausticStrength,
                "width=" + HullCausticLighting.BandWidth(GameServices.Config, HullCausticLighting.LiveChop),
                "liveChop=" + HullCausticLighting.LiveChop,
                "displacedSurfaces=" + DisplacedWaterRegistry.Count,
                "cpuGlobal=" + (Shader.GetGlobalTexture("_HHHullCausticTex") != null
                    ? Shader.GetGlobalTexture("_HHHullCausticTex").GetType().Name : "null") };
            foreach (var asset in Resources.FindObjectsOfTypeAll<ScriptableObject>())
            {
                if (asset.GetType().Name != "IsoFacetHullFeature") continue;
                object pass = asset.GetType().GetField("_pass", fields)?.GetValue(asset);
                if (pass == null) continue;
                var targets = pass.GetType().GetField("_causticDarkTargets", fields)?.GetValue(pass)
                    as System.Collections.IDictionary;
                diagnostic.Add("cameraTargets=" + (targets != null ? targets.Count : -1));
                if (targets == null || !targets.Contains(_camera.GetEntityId())) continue;
                object handle = targets[_camera.GetEntityId()];
                var texture = handle.GetType().GetProperty("rt")?.GetValue(handle) as RenderTexture;
                if (texture == null) continue;
                diagnostic.Add("retainedTarget=" + texture.name + " " + texture.width + "x" + texture.height);
                File.WriteAllLines(Path.Combine(_output, "band-target-readback.txt"), diagnostic);
                return texture;
            }
            File.WriteAllLines(Path.Combine(_output, "band-target-readback.txt"), diagnostic);
            return null;
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
        { public string name, hull, state, sha, beforeSha, source, sourceTree, bearingFixture;
          public Vector2 witnessFaceNormal; public float witnessFacing, headingDirUnits; public int witnessTriangle; public float hour, seaState, strength, width;
          public Vector2 wind; public int index, changedInsideBand, changedOutsideBand, bandPixels, maxOutsideDelta;
          public float chop, widthBase, widthPerChop, sunElevation, shadowStrength, awayFloor; public Vector4 sunBearing;
          public float f0, referenceElevationDegrees, fresnelPower, chopDimming, maxUpwardNormal;
          public float tuningVeinGainBefore, tuningVeinGainAfter; public double meanShift, maxShift;
          public bool darkTargetUnchangedByGain, darkRgbUnchanged;
          public int alphaChangedOutsideBand, clippedHullPixels; public double decodedMean, decodedMax, maximumLinearHullChannel;
          public float physicalGain, rungMultiplier, irradiancePower, irradianceFloor, facetTiltDegrees; }
        [Serializable] private sealed class PairList { public PairProof[] pairs; }
        [Serializable] private sealed class CalibrationProof
        { public string lawSha, sourceTree; public Vector4 sunBearing; public float shadowStrength; public int editorPid, frame; public float gainBefore, gain, hour, sunElevation;
          public double cap, sunlitLuma, shadedLuma, referenceReflectance, peakAtCeiling, peakAtDerivedGain;
          public int[] litRoi, shadedRoi; public bool capBinding; }
        [Serializable] private sealed class CalibrationGo
        { public string lawSha, finalSha; public int editorPid; public float gain; }
        [Serializable] private sealed class MotionProof
        { public string name, sha; public Vector4 sunBearing; public float shadowStrength; public float hour, gain, width, chop, nominalSeconds; public int gifFps;
          public Vector2 wind; public float[] times; public double[] gameSeconds; }
        [Serializable] private sealed class PixelSample
        { public int x, y; public Color32 first, second; }
        [Serializable] private sealed class SameGainProof
        {
            public string sha, hull, state, source;
            public float hour, seaState, gainA, gainB, timeA, timeB;
            public Vector2 wind;
            public int changedOutside, maxChannelDelta, bandPixels, frameA, frameB, renderedFrameA, renderedFrameB;
            public int[] bbox; public PixelSample[] samples;
        }
        [Serializable] private sealed class CostProof
        { public int hulls, width, height, retainedDarkTargetBytes, addedPasses, addedMrtSlots;
          public double cpuMainThreadOffMs, cpuMainThreadOnMs, gpuFrameOffMs, gpuFrameOnMs, fleetPoseOffMs, fleetPoseOnMs;
          public bool gpuTimingAvailable, mainThreadCounterAvailable; public string note; }
    }
}
