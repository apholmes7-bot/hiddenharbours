#if UNITY_EDITOR
using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using HiddenHarbours.App;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// The plates for the beached trawler: her from the shore (close, at the lowest and the highest
    /// shipped tide) and from the water (wide, from the trawler basin). She is found in the scene,
    /// not placed here, so the plates show what the game shows.
    ///
    /// <para>Set <c>HH_WRECK_HEEL_SWEEP</c> to a list of angles ("-60;-40;40;60") to also shoot her
    /// at each, for choosing the Def's heel by eye. The sweep changes nothing that is saved.</para>
    /// </summary>
    public sealed class RicePointBeachedTrawlerPlatePlayTests : RicePointSceneFixture
    {
        const int Width = 1920, Height = 1080;
        const string SweepVariable = "HH_WRECK_HEEL_SWEEP";
        const double MeanEbbHour = 5 + 12 / 60d + 37 / 3600d,
            SpringLowHour = 8 + 18 / 60d + 44 / 3600d;
        const float ShoreHalfHeight = 16f, WaterHalfHeight = 45f;
        Camera _camera;
        RenderTexture _target;
        bool _shaderSettingCaptured, _asyncShaders;

        [UnityTest, Timeout(300000)]
        public IEnumerator TheWreckFromTheShore_AndFromTheWater()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("Plates need a graphics device.");
            _asyncShaders = ShaderUtil.allowAsyncCompilation;
            _shaderSettingCaptured = true;
            ShaderUtil.allowAsyncCompilation = false;
            yield return LoadDirect();

            var she = RegionScene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<BeachedWreck>(true)).Single();
            var wreck = she.Wreck;
            Assert.That(wreck, Is.Not.Null);
            Assert.That(she.IsDrawn, Is.True, "her real hull is drawn in the game's own scene");
            Assert.That(she.Way, Is.EqualTo(VesselWay.Derelict), "a wreck shows no lamps");
            Assert.That(she.DrawnHeelDegrees, Is.EqualTo(wreck.HeelDegrees).Within(0.01f), "she lies over as far as the Def says");

            _camera = Camera.main;
            Assert.That(_camera, Is.Not.Null);
            foreach (var component in _camera.GetComponents<Behaviour>())
                if (component is CameraFollow || component.GetType().Name == "PixelPerfectCamera") component.enabled = false;
            _target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
            _target.Create();
            _camera.targetTexture = _target;
            _camera.aspect = (float)Width / Height;
            string folder = Path.Combine(Evidence, "beached-trawler");
            Directory.CreateDirectory(folder);
            var report = new StringBuilder("plate,hour,water_m,heel_deg\n");
            Vector2 basin = Vector2.zero;
            int berths = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:BerthDef"))
            {
                var berth = AssetDatabase.LoadAssetAtPath<HiddenHarbours.World.BerthDef>(AssetDatabase.GUIDToAssetPath(guid));
                if (berth.RegionId != wreck.RegionId) continue;
                basin += (berth.Stern + berth.Bow) / 2;
                berths++;
            }
            Vector2 fromWater = berths > 0 ? (wreck.Position + basin / berths) / 2 : wreck.Position;

            yield return Shoot("from-the-shore-spring-low", SpringLowHour, wreck.Position, ShoreHalfHeight, folder, report, wreck.HeelDegrees);
            yield return Shoot("from-the-shore-daylight-high", DaylightHighHour(), wreck.Position, ShoreHalfHeight, folder, report, wreck.HeelDegrees);
            yield return Shoot("from-the-water-mean-ebb", MeanEbbHour, fromWater, WaterHalfHeight, folder, report, wreck.HeelDegrees);
            yield return Shoot("from-the-water-spring-low", SpringLowHour, fromWater, WaterHalfHeight, folder, report, wreck.HeelDegrees);

            string sweep = System.Environment.GetEnvironmentVariable(SweepVariable);
            if (!string.IsNullOrEmpty(sweep))
            {
                var driver = she.GetComponent<MeshHullDriver>();
                Assert.That(driver, Is.Not.Null);
                foreach (string part in sweep.Split(';'))
                {
                    float heel = float.Parse(part, CultureInfo.InvariantCulture);
                    driver.VisualTiltDegrees = heel;
                    yield return Shoot("sweep-heel-" + part, SpringLowHour, wreck.Position, ShoreHalfHeight, folder, report, heel);
                }
                driver.VisualTiltDegrees = wreck.HeelDegrees;
            }
            File.WriteAllText(Path.Combine(folder, "plates.csv"), report.ToString());
            Debug.Log("[rice-point] WRECK PLATES OK in " + folder);
        }

        /// <summary>The hour of the highest water between noon and dusk on the day the plates use.</summary>
        static double DaylightHighHour()
        {
            double best = 12, bestWater = double.NegativeInfinity;
            for (double hour = 12; hour <= 18; hour += 1 / 60d)
            {
                double water = GameServices.Environment.WaterLevelAt(hour * 3600d);
                if (water > bestWater) { bestWater = water; best = hour; }
            }
            return best;
        }

        IEnumerator Shoot(string name, double hour, Vector2 centre, float halfHeight, string folder, StringBuilder report, float heel)
        {
            SetHour(hour);
            double water = GameServices.Environment.WaterLevelAt(GameServices.Clock.TotalSeconds);
            _camera.transform.position = new Vector3(centre.x, centre.y, -10f);
            _camera.orthographicSize = halfHeight;
            yield return new WaitForSecondsRealtime(1);
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
                File.WriteAllBytes(Path.Combine(folder, name + ".png"), pixels.EncodeToPNG());
            }
            finally
            {
                RenderTexture.active = previous;
                if (pixels != null) UnityEngine.Object.DestroyImmediate(pixels);
            }
            report.AppendLine(FormattableString.Invariant($"{name},{hour:F6},{water:F4},{heel:F1}"));
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
