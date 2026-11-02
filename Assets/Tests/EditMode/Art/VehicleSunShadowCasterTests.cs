using System.Collections.Generic;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.Art.EditMode
{
    [SetUpFixture]
    public sealed class SunCasterInstallerDiagnosticScope
    {
        readonly List<(SunCasterInstaller host, bool enabled, bool casting)> _changed = new();

        [OneTimeSetUp] public void BeforeCases()
        {
            string mode = System.Environment.GetEnvironmentVariable("HH_SHADOWS_B_INSTALLER_DIAGNOSTIC");
            if (mode != "as-stands" && mode != "off") return;
            var hosts = Resources.FindObjectsOfTypeAll<SunCasterInstaller>();
            int installed = 0;
            foreach (var host in hosts)
            {
                installed += host.Installed.Count;
                if (mode != "off") continue;
                var field = typeof(SunCasterInstaller).GetField("_castingEnabled",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                Assert.That(field, Is.Not.Null);
                _changed.Add((host, host.enabled, (bool)field.GetValue(host)));
                host.enabled = false;
                host.SetCasting(false);
            }
            Debug.Log($"[shade-installer-probe] mode={mode}; hosts={hosts.Length}; installed={installed}; OFF=SunCasterInstaller.SetCasting(false)");
        }

        [OneTimeTearDown] public void AfterCases()
        {
            foreach (var prior in _changed)
                if (prior.host != null) { prior.host.SetCasting(prior.casting); prior.host.enabled = prior.enabled; }
            _changed.Clear();
        }
    }

    public class VehicleSunShadowCasterTests
    {
        const string Truck = "Assets/_Project/Data/Vehicles/Meshes/Dually3500VehicleMesh.asset";
        const string Atv = "Assets/_Project/Data/Vehicles/Meshes/UtilityQuadVehicleMesh.asset";
        readonly List<Object> _owned = new();
        Vector4 _sun;
        float _elevation, _strength;

        [SetUp] public void SetUp()
        {
            _sun = Shader.GetGlobalVector("_SunDir");
            _elevation = Shader.GetGlobalFloat("_SunElevation");
            _strength = Shader.GetGlobalFloat("_ShadowStrength");
            Shader.SetGlobalVector("_SunDir", new Vector4(-1f, 0f, 0f, 0f));
            Shader.SetGlobalFloat("_SunElevation", 0.5f);
            Shader.SetGlobalFloat("_ShadowStrength", 0.5f);
        }

        [TearDown] public void TearDown()
        {
            for (int i = _owned.Count - 1; i >= 0; i--) if (_owned[i] != null) Object.DestroyImmediate(_owned[i]);
            _owned.Clear();
            Shader.SetGlobalVector("_SunDir", _sun);
            Shader.SetGlobalFloat("_SunElevation", _elevation);
            Shader.SetGlobalFloat("_ShadowStrength", _strength);
        }

        GameObject Install(string path, out IsoFacetHullRenderer renderer)
        {
            var def = AssetDatabase.LoadAssetAtPath<VehicleMeshDef>(path);
            Assert.That(def, Is.Not.Null);
            var host = new GameObject("VehicleShadowFixture"); _owned.Add(host);
            host.transform.position = new Vector3(11f, 7f, 0f);
            renderer = (IsoFacetHullRenderer)new IsoFacetVehiclePresentationService().Install(host, def);
            Assert.That(renderer.IsConfigured, Is.True);
            Assert.That(renderer.HullId, Is.GreaterThan(0));
            return host;
        }

        [TestCase(Truck), TestCase(Atv)]
        public void Install_AttachesBothCastersExactlyOnce(string path)
        {
            var host = Install(path, out _);
            var sun = host.GetComponent<HullSunShadowCaster>();
            var lamp = host.GetComponent<HullLampShadowCaster>();
            Assert.That(sun, Is.Not.Null); Assert.That(lamp, Is.Not.Null);
            new IsoFacetVehiclePresentationService().Install(host, AssetDatabase.LoadAssetAtPath<VehicleMeshDef>(path));
            Assert.That(host.GetComponents<HullSunShadowCaster>(), Has.Length.EqualTo(1));
            Assert.That(host.GetComponents<HullLampShadowCaster>(), Has.Length.EqualTo(1));
            Assert.That(host.GetComponent<HullSunShadowCaster>(), Is.SameAs(sun));
            Assert.That(host.GetComponent<HullLampShadowCaster>(), Is.SameAs(lamp));
        }

        [TestCase(Truck), TestCase(Atv)]
        public void VehicleInstall_AddsNoWaterOrDeckComponents(string path)
        {
            var host = Install(path, out _);
            Assert.That(host.GetComponentsInChildren<FoamInjector>(true), Is.Empty);
            Assert.That(host.GetComponentsInChildren<ReflectiveObject>(true), Is.Empty);
            Assert.That(host.GetComponentsInChildren<HullDeckShadowCaster>(true), Is.Empty);
        }

        [TestCase(Truck), TestCase(Atv)]
        public void LampFoot_IsTheGroundOrigin_NotTheCellCentre(string path)
        {
            var host = Install(path, out var renderer);
            Assert.That(host.GetComponent<HullLampShadowCaster>().TryGetLampShadowCaster(out var state), Is.True);
            Assert.That(state.Foot, Is.EqualTo((Vector2)host.transform.position));
            Assert.That(renderer.OverlayRenderer.transform.position, Is.EqualTo(host.transform.position));
            Assert.That(state.RectMax.y, Is.GreaterThan(state.Foot.y));
            Assert.That(state.RectMin.y, Is.LessThan(state.Foot.y));
            Assert.That(((state.RectMin + state.RectMax) / 2f).y, Is.Not.EqualTo(state.Foot.y));
        }

        [TestCase(Truck), TestCase(Atv)]
        public void SunProjection_IsFinite_AndKeepsTheGroundOrigin(string path)
        {
            var host = Install(path, out var renderer);
            var state = renderer.SunShadowState;
            Assert.That(state.HeavePixels, Is.Zero); Assert.That(state.RidePixels, Is.Zero);
            Assert.That(state.DeckHeight, Is.Zero);
            var projection = HullSunShadowMath.Projection(Vector2.right, 2f, state.Elevation,
                state.HeavePixels, state.RidePixels, state.PixelsPerMetre);
            for (int i = 0; i < 4; i++)
                Assert.That(float.IsNaN(projection[i]) || float.IsInfinity(projection[i]), Is.False);
            Assert.That(HullSunShadowMath.Project(Vector3.zero,
                HullSunShadowMath.Pose(state.Heading, state.Elevation, state.Roll, state.Pitch), projection, 0f),
                Is.EqualTo(Vector3.zero));
            host.GetComponent<HullSunShadowCaster>().Tick();
            Assert.That(host.GetComponent<HullSunShadowCaster>().ShadowRenderer.enabled, Is.True);
        }

        [Test] public void InstallingAVehicle_LeavesABoatsCastersAndProjectionUnchanged()
        {
            var mesh = HullSunShadowFixture.Box(); _owned.Add(mesh);
            var boat = new GameObject("UnchangedBoat"); _owned.Add(boat);
            boat.AddComponent<IsoFacetHullRenderer>().Configure(HullSunShadowFixture.Setup(mesh));
            var sun = HullSunShadowCaster.Fit(boat);
            var lamp = HullLampShadowCaster.Fit(boat);
            var before = new MaterialPropertyBlock(); sun.ShadowRenderer.GetPropertyBlock(before);
            var proxy = sun.Proxy;
            Assert.That(lamp.TryGetLampShadowCaster(out var footBefore), Is.True);
            Install(Truck, out _);
            sun.Tick();
            var after = new MaterialPropertyBlock(); sun.ShadowRenderer.GetPropertyBlock(after);
            Assert.That(boat.GetComponent<HullSunShadowCaster>(), Is.SameAs(sun));
            Assert.That(boat.GetComponent<HullLampShadowCaster>(), Is.SameAs(lamp));
            Assert.That(sun.Proxy, Is.SameAs(proxy));
            Assert.That(after.GetVector("_HHSunProjection"), Is.EqualTo(before.GetVector("_HHSunProjection")));
            Assert.That(lamp.TryGetLampShadowCaster(out var footAfter), Is.True);
            Assert.That(footAfter.Foot, Is.EqualTo(footBefore.Foot));
        }
    }
}
