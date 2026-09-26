using System.Reflection;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.Art.EditMode
{
    public class HullReflectionPivotTests
    {
        GameObject _root;
        IsoFacetHullRenderer _hull;
        MeshRenderer _mesh, _leaf, _overlay;

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();
            _root = new GameObject("HullReflectionTest");
            _root.transform.position = new Vector3(7f, -11f, 0f);
            _hull = _root.AddComponent<IsoFacetHullRenderer>();
            _mesh = Child("FacetMesh", _root.transform);
            _leaf = Child("DoorLeaf", _mesh.transform);
            _overlay = Child("HullOverlay", _root.transform);
            // Supply only ApplyPose's CPU state: no Configure, material, shader, mesh or camera.
            Set(_hull, "_setup", new IsoFacetHullSetup { PxPerMetre = 32, ElevationDeg = 40f });
            Set(_hull, "_meshChild", _mesh.transform);
            Set(_hull, "_meshRenderer", _mesh);
            Set(_hull, "_leafRenderer", _leaf);
            Set(_hull, "_overlayChild", _overlay.transform);
            Set(_hull, "_overlayRenderer", _overlay);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
            GameServices.Reset();
        }

        static MeshRenderer Child(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go.AddComponent<MeshRenderer>();
        }

        static void Set(object target, string name, object value)
        {
            var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, name);
            field.SetValue(target, value);
        }

        static MaterialPropertyBlock Read(Renderer renderer)
        {
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            return block;
        }

        [Test]
        public void HullOverlayPose_PreservesReflectionOriginAndLitFlag()
        {
            var reflector = _overlay.gameObject.AddComponent<ReflectiveObject>();
            Set(reflector, "_pivotOffset", new Vector2(0.25f, -0.5f));
            foreach (bool lit in new[] { true, false })
            {
                Set(reflector, "_nightLitSource", lit);
                reflector.Refresh();
                var expected = new Vector4(7.25f, -11.5f, 0f, 1f);
                Assert.AreEqual(expected, ReflectiveObject.OriginOn(_overlay), "precondition: published pivot");
                _hull.HeadingDirUnits += 0.5f;
                _hull.PitchDegrees = 3f;
                _hull.ApplyPose();
                _hull.ApplyPose(); // also the clean-pose frame, with no reflector refresh
                var block = Read(_overlay);
                Assert.Multiple(() =>
                {
                    Assert.AreEqual(expected, block.GetVector(ReflectionShaderIds.ReflectOrigin),
                        "HullOverlay ApplyPose erased the published reflection origin.");
                    Assert.AreEqual(lit ? 1f : 0f, block.GetFloat(ReflectionShaderIds.ReflectLit),
                        "HullOverlay ApplyPose erased the published reflection lit flag.");
                });
            }
        }

        [Test]
        public void HullPose_KeepsEachRenderersOwnBlock_WithoutSiblingLeaks()
        {
            var renderers = new[] { _mesh, _leaf, _overlay };
            int marker = Shader.PropertyToID("_HullReflectionTestMarker");
            for (int i = 0; i < renderers.Length; i++)
            {
                var reflector = renderers[i].gameObject.AddComponent<ReflectiveObject>();
                Set(reflector, "_pivotOffset", new Vector2(i + 1f, -i));
                Set(reflector, "_nightLitSource", i == 1);
                reflector.Refresh();
                var block = Read(renderers[i]);
                block.SetFloat(marker, i + 10f);
                renderers[i].SetPropertyBlock(block);
            }
            _hull.ApplyPose();
            for (int i = 0; i < renderers.Length; i++)
            {
                var block = Read(renderers[i]);
                Assert.AreEqual(new Vector4(8f + i, -11f - i, 0f, 1f),
                    block.GetVector(ReflectionShaderIds.ReflectOrigin), "Each renderer keeps its own pivot.");
                Assert.AreEqual(i == 1 ? 1f : 0f, block.GetFloat(ReflectionShaderIds.ReflectLit));
                Assert.AreEqual(i + 10f, block.GetFloat(marker), "Unowned properties also stay local.");
            }
            // Clearing a sibling must not let the reused scratch block bring its keys back.
            _leaf.SetPropertyBlock(null);
            _hull.ApplyPose();
            _hull.ApplyPose();
            Assert.IsFalse(Read(_leaf).HasVector(ReflectionShaderIds.ReflectOrigin), "No pivot leaks into a cleared sibling.");
            Assert.IsFalse(Read(_leaf).HasFloat(marker), "No unowned value leaks into a cleared sibling.");
        }

        [Test]
        public void HullPose_WithoutReflectors_PublishesPoseAndDeckSlotsOnly()
        {
            _hull.SetDeckOccupant(new Vector3(0.5f, 1f, 0.25f), true);
            _hull.PitchDegrees = 4f;
            _hull.HeavePixels = 8f;
            _hull.ApplyPose();
            foreach (var renderer in new[] { _mesh, _leaf, _overlay })
            {
                var block = Read(renderer);
                Assert.AreEqual(new Vector4(7f, -11f, 0f, 0f), block.GetVector(IsoFacetShaderIds.HullOrigin));
                Assert.AreEqual(_hull.HullId / 255f, block.GetFloat(IsoFacetShaderIds.HullId));
                Assert.AreEqual(1f, block.GetFloat(IsoFacetShaderIds.DeckOccupantCount));
                Vector4 slot = block.GetVectorArray(IsoFacetShaderIds.DeckOccupant)[0];
                Assert.AreEqual(_mesh.transform.TransformPoint(new Vector3(0.5f, 1f, 0.25f)).z, slot.x);
                Assert.AreEqual(1f, slot.w);
                Assert.IsFalse(block.HasVector(ReflectionShaderIds.ReflectOrigin));
                Assert.IsFalse(block.HasFloat(ReflectionShaderIds.ReflectLit));
            }
            _hull.SetDeckOccupant(Vector3.zero, false);
            _hull.ApplyPose();
            foreach (var renderer in new[] { _mesh, _leaf, _overlay })
            {
                var block = Read(renderer);
                Assert.AreEqual(0f, block.GetFloat(IsoFacetShaderIds.DeckOccupantCount));
                Assert.AreEqual(Vector4.zero, block.GetVectorArray(IsoFacetShaderIds.DeckOccupant)[0]);
            }
        }
    }
}
