#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using HiddenHarbours.App;
using HiddenHarbours.Core;
using HiddenHarbours.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

namespace HiddenHarbours.Tests.PlayMode
{
    public sealed class RicePointPlatePlayTests : RicePointSceneFixture
    {
        [Serializable] sealed class SourceViews { public SourceView[] keyscenes; }
        [Serializable] sealed class SourceView { public string id; public float[] box; }
        const int Width = 1920, Height = 1080;
        Camera _camera;
        RenderTexture _target;
        bool _shaderSettingCaptured, _asyncShaders;

        [UnityTest, Timeout(300000)]
        public IEnumerator Strand_Marsh_Gut_Ledges_AtThreeShippedTides()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("Plates need a graphics device.");
            _asyncShaders = ShaderUtil.allowAsyncCompilation;
            _shaderSettingCaptured = true;
            ShaderUtil.allowAsyncCompilation = false;
            yield return LoadDirect();
            _camera = Camera.main;
            Assert.That(_camera, Is.Not.Null);
            foreach (var component in _camera.GetComponents<Behaviour>())
                if (component is CameraFollow || component.GetType().Name == "PixelPerfectCamera") component.enabled = false;
            _target = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
            _target.Create();
            _camera.targetTexture = _target;
            _camera.aspect = (float)Width / Height;
            var views = JsonUtility.FromJson<SourceViews>(Plan.Source.text).keyscenes;
            string folder = Path.Combine(Evidence, "captured-plates");
            Directory.CreateDirectory(folder);
            var manifest = new StringBuilder("view,tide,day_index,hour,water_m,path,utc\n");
            var hours = new[] { 2 + 6 / 60d + 16 / 3600d, 5 + 12 / 60d + 37 / 3600d, 8 + 18 / 60d + 44 / 3600d };
            var tideNames = new[] { "spring-high", "mean-ebb", "spring-low" };
            var subjects = new[] { "strand_crossing", "cove_marsh", "gut", "point_ledges" };
            for (int tide = 0; tide < hours.Length; ++tide)
            {
                SetHour(hours[tide]);
                Assert.That(GameServices.Clock.DayIndex, Is.Zero);
                double water = GameServices.Environment.WaterLevelAt(GameServices.Clock.TotalSeconds);
                double expected = tide == 0 ? 2.1995 : tide == 1 ? 0 : -2.1927;
                Assert.That(water, Is.EqualTo(expected).Within(.02), "Shipped tide differs from the plate contract.");
                yield return new WaitForSecondsRealtime(2);
                foreach (string subject in subjects)
                {
                    var box = views.Single(v => v.id == "keyscene.rpt_" + subject).box;
                    Vector3 centre = new Vector3((box[0] + box[1]) / 2, (box[2] + box[3]) / 2, -10);
                    _camera.transform.position = centre;
                    _camera.orthographicSize = Mathf.Max((box[3] - box[2]) / 2,
                        (box[1] - box[0]) / (2 * _camera.aspect)) * 1.05f;
                    for (int frame = 0; frame < 30; ++frame) yield return null;
                    Assert.That(Vector3.Distance(_camera.transform.position, centre), Is.LessThan(.001f));
                    string name = subject + "-" + tideNames[tide] + ".png";
                    Capture(Path.Combine(folder, name));
                    manifest.AppendLine(FormattableString.Invariant(
                        $"{subject},{tideNames[tide]},0,{hours[tide]:F8},{water:F6},{name},{DateTime.UtcNow:O}"));
                }
            }
            File.WriteAllText(Path.Combine(folder, "plates.csv"), manifest.ToString());
            Debug.Log("[rice-point] PLATES OK: 12 saved in " + folder);
        }

        void Capture(string path)
        {
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
