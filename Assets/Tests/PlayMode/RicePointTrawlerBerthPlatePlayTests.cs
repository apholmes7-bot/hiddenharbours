#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Text;
using HiddenHarbours.App;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using HiddenHarbours.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// The plates for the trawler berths: a trawler lying in each berth at the lowest shipped tide,
    /// drawn with her real hull, so a person can see she is afloat with room round her. The berths
    /// and the hulls are read from their Defs; nothing here is a position of its own.
    /// </summary>
    public sealed class RicePointTrawlerBerthPlatePlayTests : RicePointSceneFixture
    {
        const int Width = 1920, Height = 1080;
        const string BerthFolder = "Assets/_Project/Data/Berths/";
        const string BoatFolder = "Assets/_Project/Data/Boats/";
        const double SpringLowHour = 8 + 18 / 60d + 44 / 3600d;
        Camera _camera;
        RenderTexture _target;
        bool _shaderSettingCaptured, _asyncShaders;

        [UnityTest, Timeout(300000)]
        public IEnumerator ATrawlerLiesInEachBerth_AtSpringLow()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("Plates need a graphics device.");
            _asyncShaders = ShaderUtil.allowAsyncCompilation;
            _shaderSettingCaptured = true;
            ShaderUtil.allowAsyncCompilation = false;
            yield return LoadDirect();
            SetHour(SpringLowHour);
            double water = GameServices.Environment.WaterLevelAt(GameServices.Clock.TotalSeconds);
            Assert.That(water, Is.EqualTo(-2.1927).Within(.02), "Shipped tide differs from the plate contract.");

            var face = AssetDatabase.LoadAssetAtPath<BerthDef>(BerthFolder + "RicePointTrawlerFace.asset");
            var outside = AssetDatabase.LoadAssetAtPath<BerthDef>(BerthFolder + "RicePointTrawlerOutside.asset");
            var old = AssetDatabase.LoadAssetAtPath<BoatHullDef>(BoatFolder + "SternTrawler.asset");
            var mk2 = AssetDatabase.LoadAssetAtPath<BoatHullDef>(BoatFolder + "SternTrawlerMk2.asset");
            Assert.That(face, Is.Not.Null); Assert.That(outside, Is.Not.Null);
            Assert.That(old, Is.Not.Null); Assert.That(mk2, Is.Not.Null);
            var report = new StringBuilder("berth,hull,draught_m,water_m,shallowest_bed_m,under_keel_m\n");
            Lay(mk2, face, water, report);
            Lay(old, outside, water, report);

            _camera = Camera.main;
            Assert.That(_camera, Is.Not.Null);
            foreach (var component in _camera.GetComponents<Behaviour>())
                if (component is CameraFollow || component.GetType().Name == "PixelPerfectCamera") component.enabled = false;
            _target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
            _target.Create();
            _camera.targetTexture = _target;
            _camera.aspect = (float)Width / Height;
            string folder = Path.Combine(Evidence, "trawler-berths");
            Directory.CreateDirectory(folder);
            yield return new WaitForSecondsRealtime(2);

            Vector2 faceMid = (face.Stern + face.Bow) / 2, outsideMid = (outside.Stern + outside.Bow) / 2;
            Vector2 both = (faceMid + outsideMid) / 2;
            Vector2 mouth = outside.Approach.Length > 0 ? outside.Approach[0] : both;
            yield return Shoot(both, 30f, Path.Combine(folder, "both-berths-spring-low.png"));
            yield return Shoot(faceMid, 22f, Path.Combine(folder, "face-berth-spring-low.png"));
            yield return Shoot(outsideMid, 22f, Path.Combine(folder, "outside-berth-spring-low.png"));
            yield return Shoot((both + mouth) / 2, 62f, Path.Combine(folder, "berths-and-the-way-in-spring-low.png"));
            File.WriteAllText(Path.Combine(folder, "berths.csv"), report.ToString());
            Debug.Log("[rice-point] BERTH PLATES OK: 4 saved in " + folder);
        }

        void Lay(BoatHullDef hull, BerthDef berth, double water, StringBuilder report)
        {
            Vector2 mid = (berth.Stern + berth.Bow) / 2, along = berth.Bow - berth.Stern;
            float heading = Mathf.Atan2(along.x, along.y) * Mathf.Rad2Deg;
            var boat = new GameObject("Plate_" + hull.Id + "_at_" + berth.Id);
            SceneManager.MoveGameObjectToScene(boat, RegionScene);
            boat.transform.SetPositionAndRotation(new Vector3(mid.x, mid.y, 0f), Quaternion.Euler(0f, 0f, -heading));
            var rig = BoatHullSkinner.Apply(boat, hull.Visual, boat: null, new BoatHullSkinner.Options { SkipOars = true });
            Assert.That(rig.Skinned, Is.True, hull.Id + " drew nothing: her real hull is not presented in this scene.");
            var ride = boat.GetComponent<HullTideRide>();
            if (ride != null) ride.Configure(0f, hull.DraughtMeters);
            float bed = berth.ShallowestBedInBerth(p => GameServices.TidalTerrain.ElevationAt(p), 0.5f);
            double under = water - hull.DraughtMeters - bed;
            Assert.That(under, Is.GreaterThan(0d), hull.Id + " touches the bed in " + berth.Id + " at spring low.");
            report.AppendLine(FormattableString.Invariant(
                $"{berth.Id},{hull.Id},{hull.DraughtMeters:F2},{water:F4},{bed:F4},{under:F4}"));
        }

        IEnumerator Shoot(Vector2 centre, float halfHeight, string path)
        {
            _camera.transform.position = new Vector3(centre.x, centre.y, -10f);
            _camera.orthographicSize = halfHeight;
            for (int frame = 0; frame < 30; ++frame) yield return null;
            RenderTexture previous = RenderTexture.active;
            Texture2D pixels = null;
            try
            {
                _camera.Render();
                RenderTexture.active = _target;
                pixels = new Texture2D(Width, Height, TextureFormat.RGB24, false);
                pixels.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
                pixels.Apply();
                File.WriteAllBytes(path, pixels.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                if (pixels != null) UnityEngine.Object.DestroyImmediate(pixels);
            }
        }

        [TearDown]
        public void ReleasePlateResources()
        {
            if (_camera != null) _camera.targetTexture = null;
            if (_target != null) { _target.Release(); UnityEngine.Object.DestroyImmediate(_target); }
            if (_shaderSettingCaptured) ShaderUtil.allowAsyncCompilation = _asyncShaders;
            _shaderSettingCaptured = false;
        }
    }
}
#endif
