#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HiddenHarbours.App;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Art;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using HiddenHarbours.Vehicles;
using NUnit.Framework;
using Unity.Profiling;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.PlayMode
{
    public class VehicleSunShadowPlayTests
    {
        const string Output = "artifacts/shadows/b";
        readonly List<Object> _owned = new();
        readonly HashSet<GameObject> _resident = new();
        readonly List<GameObject> _sleepingResidents = new();
        WharfNightStage _stage;
        bool _ignore;
        float _timeScale;
        ISaveService _save;

        static GameObject[] PersistentRoots()
        {
            var probe = new GameObject("VehicleShadowLifetimeProbe");
            Object.DontDestroyOnLoad(probe);
            var scene = probe.scene;
            Object.DestroyImmediate(probe);
            return scene.GetRootGameObjects();
        }

        [SetUp] public void SetUp()
        {
            _ignore = LogAssert.ignoreFailingMessages;
            _timeScale = Time.timeScale;
            _save = GameServices.Save;
            _resident.Clear();
            foreach (var root in PersistentRoots()) _resident.Add(root);
        }

        [UnityTearDown] public IEnumerator TearDown()
        {
            Time.timeScale = 1f;
            foreach (var item in _owned) if (item != null) Object.DestroyImmediate(item);
            _owned.Clear();
            // Remove only roots this case introduced, including promoted region actors/coordinators.
            foreach (var root in PersistentRoots())
                if (root != null && !_resident.Contains(root))
                { root.SetActive(false); Object.DestroyImmediate(root); }
            if (_stage != null) yield return _stage.TearDown();
            _stage = null;
            foreach (var root in _sleepingResidents) if (root != null) root.SetActive(true);
            _sleepingResidents.Clear();
            GameServices.Save = _save;
            ShellFlow.Reset();
            LogAssert.ignoreFailingMessages = _ignore;
            Time.timeScale = _timeScale;
            yield return null;
        }

        IEnumerator Load()
        {
            Time.timeScale = 1f;
            // GPU fixtures before this class can retain a real persistent fleet. Keep that fleet
            // resident but asleep, so this region has its own camera and unshared facet ids.
            foreach (var root in _resident)
                if (root != null && root.activeSelf && root.GetComponentInChildren<PersistentObject>(true) != null)
                { root.SetActive(false); _sleepingResidents.Add(root); }
            GameServices.Clock = null;
            GameServices.Environment = null;
            _stage = new WharfNightStage("NineMileCreek", "VehicleShadowProof");
            yield return _stage.Load();
            Directory.CreateDirectory(Output);
        }

        IEnumerator Hour(float hour)
        {
            Assert.That(GameServices.Clock, Is.Not.Null);
            Assert.That(GameServices.Config, Is.Not.Null);
            Time.timeScale = 1f;
            GameServices.Clock.SeekTo((1.0 + hour / 24.0) * GameServices.Config.SecondsPerDay);
            GameServices.Clock.TimeScale = 0f;
            float until = Time.realtimeSinceStartup + 1.1f;
            while (Time.realtimeSinceStartup < until) yield return null;
            Assert.That(GameServices.Clock.HourOfDay, Is.EqualTo(hour).Within(0.02f));
            Assert.That(Shader.GetGlobalFloat("_SunElevation") > 0f, Is.EqualTo(hour < 20f),
                "The published sun, not a guessed lamp gate, must wear the requested hour.");
        }

        static ParkedVehicle[] Placed()
            => Object.FindObjectsByType<ParkedVehicle>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(p => p.gameObject.scene.name == "NineMileCreek").ToArray();

        static IsoFacetHullRenderer Body(ParkedVehicle vehicle)
        {
            var body = vehicle.GetComponentInChildren<IsoFacetHullRenderer>(true);
            Assert.That(body, Is.Not.Null, vehicle.name + ": the vehicle must be skinned.");
            Assert.That(body.IsConfigured, Is.True);
            Assert.That(body.HullId, Is.GreaterThan(0));
            return body;
        }

        ParkedVehicle Quad(Vector2 at)
        {
            var def = AssetDatabase.LoadAssetAtPath<VehicleDef>("Assets/_Project/Data/Vehicles/UtilityQuad.asset");
            Assert.That(def, Is.Not.Null);
            var go = new GameObject("VehicleShadowCatalogQuad", typeof(Rigidbody2D)); _owned.Add(go);
            go.SetActive(false);
            go.transform.position = at;
            go.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
            var parked = go.AddComponent<ParkedVehicle>();
            parked.Configure(def, drivable: false);
            go.SetActive(true);
            return parked;
        }

        static void AssertDayCaster(ParkedVehicle vehicle)
        {
            var body = Body(vehicle);
            var sun = body.GetComponent<HullSunShadowCaster>();
            Assert.That(sun, Is.Not.Null, vehicle.name);
            Assert.That(body.GetComponent<HullLampShadowCaster>(), Is.Not.Null, vehicle.name);
            sun.Tick();
            Assert.That(sun.ShadowRenderer, Is.Not.Null);
            Assert.That(sun.ShadowRenderer.enabled, Is.True, vehicle.name + ": daylight shadow stood down.");
        }

        [UnityTest] public IEnumerator PlacedTrucksAndCatalogAtv_CastByDay()
        {
            yield return Load();
            yield return Hour(12f);
            var placed = Placed();
            Assert.That(placed.Length, Is.GreaterThanOrEqualTo(2), "The two authored truck-park machines are the floor.");
            Assert.That(placed.Any(p => p.name == NineMileCreekTruckPark.TruckName), Is.True);
            Assert.That(placed.Any(p => p.name == NineMileCreekTruckPark.Modern3500Name), Is.True);
            foreach (var truck in placed) AssertDayCaster(truck);
            var quad = Quad(NineMileCreekMainland.WharfRoad[2]);
            yield return null;
            AssertDayCaster(quad);
            var ids = placed.Concat(new[] { quad }).Select(p => Body(p).HullId).ToArray();
            Assert.That(ids.Distinct().Count(), Is.EqualTo(ids.Length), "This fixture must not share overflow facet ids.");
            File.WriteAllLines(Path.Combine(Output, "vehicle-census.tsv"),
                new[] { "name\thullId\theavePx\tridePx\tppu\tfoot" }.Concat(placed.Concat(new[] { quad }).Select(p =>
                {
                    var body = Body(p); var state = body.SunShadowState;
                    Assert.That(body.GetComponent<HullLampShadowCaster>().TryGetLampShadowCaster(out var lamp), Is.True);
                    return $"{p.name}\t{body.HullId}\t{state.HeavePixels}\t{state.RidePixels}\t{state.PixelsPerMetre}\t{lamp.Foot}";
                })));
        }

        static SceneLight LampPost()
        {
            var lamp = LampShadowSystem.LiveLights.FirstOrDefault(l => l != null && l.isActiveAndEnabled &&
                l.gameObject.scene.name == "NineMileCreek" && l.Shape == SceneLight.LightShape.Radial &&
                l.Intensity > 0f && l.ReachMetres > 0f && l.GetComponent<PreconfiguredLight>() != null &&
                l.GetComponent<PreconfiguredLight>().Preset == LightPresets.Kind.Lightpost);
            Assert.That(lamp, Is.Not.Null, "The scene must carry a live radial lamp post.");
            return lamp;
        }

        static void Park(ParkedVehicle vehicle, Vector2 at)
        {
            if (vehicle.Controller != null) vehicle.Controller.enabled = false;
            var rb = vehicle.GetComponent<Rigidbody2D>();
            if (rb != null) { rb.bodyType = RigidbodyType2D.Kinematic; rb.position = at; }
            vehicle.transform.position = new Vector3(at.x, at.y, 0f);
            Physics2D.SyncTransforms();
        }

        [UnityTest] public IEnumerator TruckAtTwentyOne_IsRegisteredUnderALampPost()
        {
            yield return Load();
            yield return Hour(21f);
            var truck = Placed().First(p => p.name == NineMileCreekTruckPark.TruckName);
            var lamp = LampPost();
            Park(truck, (Vector2)lamp.WorldOrigin + Vector2.right * 2f);
            yield return null;
            var caster = Body(truck).GetComponent<HullLampShadowCaster>();
            Assert.That(caster.TryGetLampShadowCaster(out var state), Is.True);
            Assert.That(state.Foot, Is.EqualTo((Vector2)Body(truck).transform.position));
            int count = LampShadowSystem.LiveCasterCount;
            caster.enabled = false;
            Assert.That(LampShadowSystem.LiveCasterCount, Is.EqualTo(count - 1));
            caster.enabled = true;
            Assert.That(LampShadowSystem.LiveCasterCount, Is.EqualTo(count));
        }

        [UnityTest] public IEnumerator ProofFrames_OffOn()
        {
            if (System.Environment.GetEnvironmentVariable("HH_SHADOWS_B_PLATES") != "1" ||
                SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("Opt-in GPU proof; presence and lamp registration run on the Null device.");
            yield return Load();
            var truck = Placed().First(p => p.name == NineMileCreekTruckPark.TruckName);
            Vector2 park = truck.transform.position;
            var quad = Quad(NineMileCreekMainland.WharfRoad[2] + Vector2.right * 14f);
            yield return null;
            yield return Pair("truck-park-1700", park, 17f);
            Vector2 road = NineMileCreekMainland.WharfRoad[2];
            Park(truck, road);
            yield return Pair("truck-road-0800", road, 8f);
            yield return Pair("truck-road-1700", road, 17f);
            yield return Pair("atv-1700", quad.transform.position, 17f);
            SpawnMooredHull();
            Park(truck, new Vector2(120f, 87.7f));
            yield return Pair("truck-and-hull-1700", new Vector2(120f, 83f), 17f);
            var lamp = LampPost();
            Park(truck, (Vector2)lamp.WorldOrigin + Vector2.right * 2f);
            yield return Pair("truck-lamp-2100", truck.transform.position, 21f);
        }

        void SpawnMooredHull()
        {
            var hull = AssetDatabase.LoadAssetAtPath<BoatHullDef>("Assets/_Project/Data/Boats/Dory.asset");
            Assert.That(hull, Is.Not.Null);
            var go = new GameObject("VehicleShadowMooredDory"); _owned.Add(go);
            go.transform.position = new Vector3(122f, 77f, 0f);
            var boat = go.AddComponent<BoatController>();
            boat.SetHull(hull);
            BoatHullSkinner.Apply(go, hull.Visual, boat,
                new BoatHullSkinner.Options { SkipWaveMotion = true, SkipOars = true });
            boat.enabled = false;
            go.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
        }

        static IsoFacetHullRenderer[] VehicleBodies()
            => Placed().Select(Body).Distinct().ToArray();

        static void Casting(bool enabled)
        {
            foreach (var body in VehicleBodies())
            {
                var sun = body.GetComponent<HullSunShadowCaster>();
                var lamp = body.GetComponent<HullLampShadowCaster>();
                Assert.That(sun, Is.Not.Null); Assert.That(lamp, Is.Not.Null);
                sun.enabled = enabled; lamp.enabled = enabled;
                if (enabled) sun.Tick();
            }
            if (LampShadowSystem.Instance != null && Camera.main != null)
                LampShadowSystem.Instance.PublishFrame(Camera.main);
        }

        IEnumerator Pair(string name, Vector2 centre, float hour)
        {
            yield return Hour(hour);
            yield return _stage.FrameOn(centre);
            Casting(false);
            for (int i = 0; i < 3; i++) yield return null;
            byte[] before = _stage.Capture();
            long[] off = new long[2], on = new long[2];
            yield return Counters(off);
            Casting(true);
            for (int i = 0; i < 3; i++) yield return null;
            byte[] after = _stage.Capture();
            yield return Counters(on);
            int changed = 0;
            for (int i = 0; i < before.Length; i += 4)
                if (before[i] != after[i] || before[i + 1] != after[i + 1] || before[i + 2] != after[i + 2]) changed++;
            Assert.That(changed, Is.GreaterThan(0), name + ": OFF | ON must visibly differ.");
            int width = _stage.Width, height = _stage.Height;
            var pixels = new byte[before.Length * 2];
            for (int y = 0; y < height; y++)
            {
                Array.Copy(before, y * width * 4, pixels, y * width * 8, width * 4);
                Array.Copy(after, y * width * 4, pixels, y * width * 8 + width * 4, width * 4);
            }
            Directory.CreateDirectory(Path.Combine(Output, "plates"));
            var sheet = new Texture2D(width * 2, height, TextureFormat.RGBA32, false);
            sheet.LoadRawTextureData(pixels); sheet.Apply();
            File.WriteAllBytes(Path.Combine(Output, "plates", "compare-" + name + ".png"), sheet.EncodeToPNG());
            Object.DestroyImmediate(sheet);
            File.WriteAllText(Path.Combine(Output, "plates", name + ".txt"),
                $"OFF left | ON right; frozen world/camera/station/hour.\ncentre={centre}; hour={hour}; changedPixels={changed}; nativePanel={width}x{height}\n" +
                $"Draws OFF/ON={off[0]}/{on[0]}; batches OFF/ON={off[1]}/{on[1]}; -1 means counter not offered.\n");
        }

        static IEnumerator Counters(long[] into)
        {
            using var draws = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
            using var batches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count");
            for (int i = 0; i < 3; i++) yield return null;
            into[0] = draws.Valid ? draws.LastValue : -1;
            into[1] = batches.Valid ? batches.LastValue : -1;
        }
    }
}
#endif

