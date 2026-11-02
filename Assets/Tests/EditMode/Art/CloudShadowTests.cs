using System.IO;
using System.Text.RegularExpressions;
using HiddenHarbours.Art;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace HiddenHarbours.Tests.Art.EditMode
{
    public sealed class CloudShadowTests
    {
        CloudShadowDef _def;
        GameObject _host, _cameraHost;
        CloudShadowSystem _system;
        Camera _camera;

        [SetUp] public void SetUp()
        {
            _def = ScriptableObject.CreateInstance<CloudShadowDef>();
            _def.Shader = Shader.Find("HiddenHarbours/CloudShadow");
            _host = new GameObject("CloudGuard"); _system = _host.AddComponent<CloudShadowSystem>(); _system.Configure(_def);
            _cameraHost = new GameObject("CloudGuardCamera"); _camera = _cameraHost.AddComponent<Camera>(); _camera.orthographic = true;
            _camera.transform.position = new Vector3(0, 0, -10);
        }

        [TearDown] public void TearDown()
        { Object.DestroyImmediate(_host); Object.DestroyImmediate(_cameraHost); Object.DestroyImmediate(_def); }

        [TestCase(0f, .12f)] [TestCase(.25f, .18f)] [TestCase(.5f, .28f)]
        [TestCase(.85f, .08f)] [TestCase(1f, 0f)]
        public void Strength_FollowsTheFiveWeatherPoints(float overcast, float expected)
            => Assert.That(CloudShadowMath.Strength(_def, overcast, 1), Is.EqualTo(expected).Within(.000001));

        [TestCase(0f)] [TestCase(-1f)]
        public void SunBelowTheHorizon_CastsNothing(float elevation)
            => Assert.That(CloudShadowMath.Strength(_def, .5f, elevation), Is.Zero);

        [Test] public void Overcast_NormalizesTheLightingProfilesWeatherCap()
        {
            var profile = Resources.Load<DayNightProfile>("DayNightProfile"); Assert.That(profile, Is.Not.Null);
            Assert.That(CloudShadowMath.Overcast(1, 0, profile), Is.Zero);
            Assert.That(CloudShadowMath.Overcast(1, 1, profile), Is.EqualTo(1));
            Assert.That(CloudShadowMath.Overcast(0, 0, profile), Is.EqualTo(1));
        }

        [Test] public void Drift_IsDownwind_AndScalesWithWind()
        {
            var one = CloudShadowMath.Advance(Vector2.zero, new Vector2(3, 4), 20, _def.DriftMetresPerGameSecondPerMps, _def.ScaleMetres);
            var two = CloudShadowMath.Advance(Vector2.zero, new Vector2(6, 8), 20, _def.DriftMetresPerGameSecondPerMps, _def.ScaleMetres);
            Assert.That(one.x, Is.EqualTo(.06f).Within(.000001)); Assert.That(one.y, Is.EqualTo(.08f).Within(.000001));
            Assert.That(Vector2.Dot(one, new Vector2(3, 4)), Is.GreaterThan(0));
            Assert.That(two.x, Is.EqualTo(one.x * 2).Within(.000001)); Assert.That(two.y, Is.EqualTo(one.y * 2).Within(.000001));
        }

        [Test] public void SeedAndGameTime_ReproduceTheSameOffset_WithoutSaving()
        {
            var a = CloudShadowMath.Offset(123, 98765, new Vector2(8, -2), _def);
            Assert.That(CloudShadowMath.Offset(123, 98765, new Vector2(8, -2), _def), Is.EqualTo(a));
            Assert.That(CloudShadowMath.Offset(124, 98765, new Vector2(8, -2), _def), Is.Not.EqualTo(a));
        }

        [Test] public void LiveWind_CarriesTheExistingShape_AndTwoCamerasDoNotAdvanceItTwice()
        {
            _system.Prepare(_camera, new CloudShadowFrame(123, 0, Vector2.right, 0, 1));
            Vector4 origin = _system.Offsets;
            _system.Prepare(_camera, new CloudShadowFrame(123, 20, new Vector2(0, 5), 0, 1));
            Vector4 after = _system.Offsets;
            Assert.That(after.x, Is.EqualTo(origin.x).Within(.0001));
            Assert.That(after.y, Is.EqualTo(origin.y + .1f).Within(.0001));
            _system.Prepare(_camera, new CloudShadowFrame(123, 20, new Vector2(0, 5), 0, 1));
            Assert.That(_system.Offsets, Is.EqualTo(after));
        }

        [Test] public void Off_ReleasesTheRenderer_AndCostsNoDraw()
        {
            _system.Prepare(_camera, new CloudShadowFrame(1, 0, Vector2.right, 0, 1));
            Assert.That(_system.DrawCount, Is.EqualTo(1));
            Assert.That(_host.GetComponentsInChildren<MeshRenderer>(), Has.Length.EqualTo(1));
            var renderer = _system.ShadowRenderer;
            _system.Prepare(_camera, new CloudShadowFrame(1, 1, Vector2.right, 0, 1));
            Assert.That(_system.ShadowRenderer, Is.SameAs(renderer));
            _system.SetCasting(false);
            Assert.That(_system.DrawCount, Is.Zero); Assert.That(_system.ShadowRenderer, Is.Null);
            Assert.That(_host.GetComponentsInChildren<MeshRenderer>(), Is.Empty);
        }

        [Test] public void DisabledDef_DoesNotCreateARenderer()
        {
            _def.Enabled = false; _system.Configure(_def);
            _system.Prepare(_camera, new CloudShadowFrame(1, 0, Vector2.right, 0, 1));
            Assert.That(_system.ShadowRenderer, Is.Null); Assert.That(_system.DrawCount, Is.Zero);
        }

        [Test] public void SteadyCameraPreparation_AllocatesNoManagedBytes()
        {
            var frame = new CloudShadowFrame(1, 0, new Vector2(3, 4), .5f, 1);
            for (int i = 0; i < 100; i++) _system.Prepare(_camera, frame);
            System.GC.GetAllocatedBytesForCurrentThread();
            long before = System.GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++) _system.Prepare(_camera, frame);
            long bytes = System.GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(bytes, Is.Zero, "the warmed camera path reuses its noise, material, quad and property block");
        }

        [Test] public void FullOvercastAndNight_DisableTheCachedDraw()
        {
            _system.Prepare(_camera, new CloudShadowFrame(1, 0, Vector2.right, 0, 1)); Assert.That(_system.DrawCount, Is.EqualTo(1));
            _system.Prepare(_camera, new CloudShadowFrame(1, 0, Vector2.right, 1, 1)); Assert.That(_system.DrawCount, Is.Zero);
            _system.Prepare(_camera, new CloudShadowFrame(1, 0, Vector2.right, 0, -1)); Assert.That(_system.DrawCount, Is.Zero);
        }

        [Test] public void OverlayAndNonWorldCameras_AreExcluded()
        {
            Assert.That(CloudShadowSystem.IsWorldCamera(_camera), Is.True);
            _camera.GetUniversalAdditionalCameraData().renderType = CameraRenderType.Overlay;
            Assert.That(CloudShadowSystem.IsWorldCamera(_camera), Is.False);
            _system.Prepare(_camera, new CloudShadowFrame(1, 0, Vector2.zero, 0, 1)); Assert.That(_system.DrawCount, Is.Zero);
        }

        [Test] public void DefaultAsset_Validates_AndHasAStableTypedId()
        {
            var def = Resources.Load<CloudShadowDef>(nameof(CloudShadowDef));
            Assert.That(def, Is.Not.Null); Assert.That(def.IsValid(), Is.True); Assert.That(def.Id, Is.EqualTo("cloud_shadows.default"));
            Assert.That(def.Enabled, Is.True);
        }

        [Test] public void InvalidData_IsRejected()
        {
            Assert.That(_def.IsValid(), Is.True);
            _def.ScaleMetres = float.NaN; Assert.That(_def.IsValid(), Is.False);
            _def.ScaleMetres = 1024; _def.NoiseSize = 3; Assert.That(_def.IsValid(), Is.False);
            _def.NoiseSize = 64; _def.Id = ""; Assert.That(_def.IsValid(), Is.False);
        }

        [Test] public void TheCloudShader_NeitherUsesTheStencil_NorMultipliesTheDayNightTintAgain()
        {
            string shader = File.ReadAllText("Assets/_Project/Art/Shaders/HiddenHarboursCloudShadow.shader");
            Assert.That(Regex.IsMatch(shader, @"(?m)^\s*Stencil\s*[\r\n{]"), Is.False);
            Assert.That(shader, Does.Not.Contain("_DayNightTint"));
            Assert.That(shader, Does.Contain("Blend DstColor Zero"));
            Assert.That(shader, Does.Contain("UNITY_MATRIX_I_VP"));
        }
    }
}
