using System;
using System.Collections.Generic;
using HiddenHarbours.Art;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.Art.EditMode
{
    public class HullSunShadowCasterTests
    {
        GameObject _host;
        Mesh _mesh;
        HullSunShadowCaster _caster;
        Vector4 _sun;
        float _e, _strength;

        [SetUp]
        public void SetUp()
        {
            _sun = Shader.GetGlobalVector("_SunDir"); _e = Shader.GetGlobalFloat("_SunElevation");
            _strength = Shader.GetGlobalFloat("_ShadowStrength");
            Shader.SetGlobalVector("_SunDir", new Vector4(-1, 0, 0, 0));
            Shader.SetGlobalFloat("_SunElevation", 0.5f); Shader.SetGlobalFloat("_ShadowStrength", 0.5f);
            _mesh = HullSunShadowFixture.Box();
            _host = new GameObject("SunShadowFixture");
            _host.AddComponent<IsoFacetHullRenderer>().Configure(HullSunShadowFixture.Setup(_mesh));
            _caster = HullSunShadowCaster.Fit(_host);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_host); Object.DestroyImmediate(_mesh);
            Shader.SetGlobalVector("_SunDir", _sun); Shader.SetGlobalFloat("_SunElevation", _e);
            Shader.SetGlobalFloat("_ShadowStrength", _strength);
        }

        [Test]
        public void Profile_IsShared()
        {
            Assert.That(_caster.Profile, Is.SameAs(SpriteShadow.SharedProfile));
            Assert.That(_caster.ShadowRenderer.sharedMaterial, Is.SameAs(Resources.Load<Material>(HullSunShadowCaster.MaterialPath)));
            Assert.That(_caster.ShadowRenderer.sharedMaterial.GetFloat(HullSunShadowCaster.EnabledId), Is.EqualTo(1f));
            Mesh proxy = _caster.Proxy;
            _caster.Tick();
            Assert.That(_caster.Proxy, Is.SameAs(proxy));
            Assert.That(proxy.subMeshCount, Is.EqualTo(1));
            Assert.That(_caster.ShadowRenderer.sortingOrder, Is.EqualTo(HiddenHarbours.Core.SortingBands.SunShade));
            Material sprite = Resources.Load<Material>("SpriteShadowShade");
            foreach (string property in new[] {"_StencilRef","_StencilComp","_StencilPass","_SrcBlend","_DstBlend"})
                Assert.That(_caster.ShadowRenderer.sharedMaterial.GetFloat(property),Is.EqualTo(sprite.GetFloat(property)),
                    "The mesh and sprite are the same sun-shade stencil owner: "+property);
        }

        [Test]
        public void WarmUpdates_AllocateZeroManagedBytes()
        {
            for (int i = 0; i < 32; i++) _caster.Tick();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 256; i++) _caster.Tick();
            long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(bytes, Is.Zero, "Warm cached projection updates must not allocate managed memory.");
        }

        [Test]
        public void Disable_ReleasesOwnedResources()
        {
            Mesh source = _mesh;
            _caster.enabled = false; _caster.Tick();
            Assert.That(_caster.Proxy == null, Is.True);
            Assert.That(_caster.ShadowRenderer == null, Is.True);
            Assert.That(source != null, Is.True, "Borrowed hull geometry is never destroyed.");
        }

        [Test]
        public void DoorProxy_ContainsBothPosesInOneSubmesh()
        {
            Mesh proxy = HullSunShadowMesh.Build(_mesh, _mesh, _mesh);
            try
            {
                Assert.That(proxy.vertexCount, Is.EqualTo(_mesh.vertexCount * 3));
                Assert.That(proxy.subMeshCount, Is.EqualTo(1));
                var markers = new List<Vector2>(); proxy.GetUVs(1, markers);
                Assert.That(markers[0].x, Is.Zero);
                Assert.That(markers[_mesh.vertexCount].x, Is.EqualTo(1f));
                Assert.That(markers[_mesh.vertexCount * 2].x, Is.EqualTo(2f));
            }
            finally { Object.DestroyImmediate(proxy); }
        }
    }

    internal static class HullSunShadowFixture
    {
        public static Mesh Box()
        {
            var mesh = new Mesh();
            mesh.vertices = new[] { new Vector3(-0.6f,-0.6f,0),new Vector3(0.6f,-0.6f,0),
                new Vector3(0.6f,0.6f,0),new Vector3(-0.6f,0.6f,0),
                new Vector3(-0.6f,-0.6f,2),new Vector3(0.6f,-0.6f,2),new Vector3(0.6f,0.6f,2),new Vector3(-0.6f,0.6f,2) };
            mesh.triangles = new[] {0,2,1,0,3,2,4,5,6,4,6,7,0,1,5,0,5,4,1,2,6,1,6,5,2,3,7,2,7,6,3,0,4,3,4,7};
            mesh.SetUVs(0,new List<Vector4> {Vector4.zero,Vector4.zero,Vector4.zero,Vector4.zero,Vector4.zero,Vector4.zero,Vector4.zero,Vector4.zero});
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }

        public static IsoFacetHullSetup Setup(Mesh mesh) => new IsoFacetHullSetup
        {
            Mesh=mesh, Ramps=new[]{new[]{new Color32(180,155,110,255),new Color32(235,215,170,255)}},
            RampOffsets=new[]{0}, LightN=new Vector3(0.3f,-0.5f,0.8f).normalized, Gain=1, Bias=0,
            Bayer16=new[]{0.03125f,0.53125f,0.15625f,0.65625f,0.78125f,0.28125f,0.90625f,0.40625f,
                0.21875f,0.71875f,0.09375f,0.59375f,0.96875f,0.46875f,0.84375f,0.34375f},
            PxPerMetre=32, CellW=192, CellH=192, PivotPx=new Vector2(96,120), ElevationDeg=40,
        };
    }
}
