using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Art;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// The ADR 0027 #8 reflector CONTRACT, asserted against the renderer rather than against the
    /// fields that feed it — because "the value reached the property block" is exactly what silently
    /// does not happen when a shader tries to read a sprite's pivot from
    /// <c>unity_ObjectToWorld</c>.
    ///
    /// <para>These are headless: they create renderers, enable components and read back property
    /// blocks and layer masks. Nothing renders, so nothing here needs a graphics device — the pass
    /// itself is GPU work and is deliberately not adjudicated here (CI has no device; a render there
    /// CRASHES the editor rather than failing cleanly, which is why
    /// <c>IsoFacetUrpPassTests</c> gates on a device first, and this fixture avoids the question by
    /// testing only what is genuinely headless).</para>
    ///
    /// <para><b>Zero-cost-when-idle IS the passthrough proof for this feature.</b> There is no
    /// "reflections off" branch to keep byte-identical — with no <see cref="ReflectiveObject"/>
    /// alive the registry count is 0, <c>IsoFacetHullFeature.AddRenderPasses</c> enqueues nothing,
    /// and the water's block returns at strength 0. The count contract is asserted here; the
    /// enqueue-nothing half is asserted by <c>IsoFacetFeatureInstallTests</c> owning the feature
    /// asset and by the count being the feature's only gate.</para>
    /// </summary>
    public class ReflectiveObjectTests
    {
        GameObject _go;

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
            _go = null;
        }

        SpriteRenderer MakeReflector(float y, out ReflectiveObject reflector)
        {
            _go = new GameObject("Reflector");
            _go.transform.position = new Vector3(4f, y, 0f);
            var sr = _go.AddComponent<SpriteRenderer>();
            reflector = _go.AddComponent<ReflectiveObject>();
            reflector.Refresh();
            return sr;
        }

        [Test]
        public void Reflector_PublishesItsGroundContactPivot_NotTheObjectOrigin()
        {
            // 🔴 THE SPRITE TRAP. A SpriteRenderer's mesh arrives in WORLD space with an identity
            // object-to-world matrix, so a shader reading the object origin gets (0,0) for every
            // sprite. The pivot therefore has to arrive as a published per-renderer property, and
            // this asserts it reached the block with w = 1 ("published") and the RIGHT value.
            SpriteRenderer sr = MakeReflector(6f, out _);
            Vector4 origin = ReflectiveObject.OriginOn(sr);
            Assert.AreEqual(1f, origin.w, 1e-5f, "w = 1 is the shader's 'somebody published a pivot' flag.");
            Assert.AreEqual(4f, origin.x, 1e-4f);
            Assert.AreEqual(6f, origin.y, 1e-4f);
            Assert.AreNotEqual(0f, origin.y,
                "publishing (0,0) would be indistinguishable from the trap this exists to avoid.");
        }

        [Test]
        public void UnpublishedRenderer_ReadsBackWZero_SoTheShaderCanRefuseToMirror()
        {
            // A renderer with no ReflectiveObject must read w = 0, which is what lets the HLSL
            // collapse the geometry instead of mirroring about the world origin. A missing
            // reflection is a bug you go and find; a reflection pinned to the origin is a bug that
            // looks like a haunted sea.
            _go = new GameObject("Bare");
            var sr = _go.AddComponent<SpriteRenderer>();
            Assert.AreEqual(0f, ReflectiveObject.OriginOn(sr).w);
        }

        [Test]
        public void Reflector_JoinsTheReflectionLayer_AndLeavesItOnDisable()
        {
            // Membership is the LAYER BIT, not the shader tag: every tree shares the tree shader, so
            // tag-only filtering would sweep the whole treeline into the pass. And leaving must put
            // the mask back as it was found, not blindly clear the bit.
            SpriteRenderer sr = MakeReflector(1f, out ReflectiveObject reflector);
            Assert.IsTrue(ReflectiveObject.IsInReflectionLayer(sr));
            uint before = sr.renderingLayerMask;

            reflector.enabled = false;
            Assert.IsFalse(ReflectiveObject.IsInReflectionLayer(sr),
                "a disabled reflector must not stay in the filtered list.");
            reflector.enabled = true;
            Assert.AreEqual(before, sr.renderingLayerMask,
                "re-enabling must restore exactly the mask the reflector had.");
        }

        [Test]
        public void DistanceGate_ReadsTerrainELEVATION_NotWorldY()
        {
            // ⚠️ The bug this test found, pinned. World Y in a top-down game is a GROUND-PLANE
            // coordinate (north), not height — the art fakes height by drawing up the screen. A gate
            // written against transform.position.y excludes everything at the north end of the map
            // and admits every clifftop in the south. Height above water is
            // ITidalTerrain.ElevationAt(pivot) − waterLevel, off the one height map (§4).
            //
            // With no terrain service both elevation and water level are 0 (the "unset" convention),
            // so world Y must make NO difference at all — which is exactly what a world-Y gate could
            // never satisfy.
            SpriteRenderer far = MakeReflector(40f, out ReflectiveObject farNorth);
            Assert.IsTrue(farNorth.WithinDistanceGate,
                "40 m NORTH is not 40 m UP — the gate must not read world Y.");
            Assert.IsTrue(ReflectiveObject.IsInReflectionLayer(far));

            Object.DestroyImmediate(_go);
            SpriteRenderer near = MakeReflector(0.5f, out ReflectiveObject nearShore);
            Assert.IsTrue(nearShore.WithinDistanceGate);
            Assert.IsTrue(ReflectiveObject.IsInReflectionLayer(near));
        }

        [Test]
        public void Registry_CountsOnlyLiveReflectors_TheFeaturesZeroCostGate()
        {
            int before = ReflectionRegistry.Count;
            MakeReflector(1f, out ReflectiveObject reflector);
            Assert.AreEqual(before + 1, ReflectionRegistry.Count,
                "the feature's ONLY gate is this count — it must track reality.");
            reflector.enabled = false;
            Assert.AreEqual(before, ReflectionRegistry.Count,
                "no live reflector ⇒ no pass enqueued ⇒ the zero-cost guarantee.");
        }

        [Test]
        public void ReflectionLayer_DoesNotCollideWithTheDisplacedWaterLayer()
        {
            // Two features filter renderer lists by rendering layer in the same feature. Sharing a
            // bit would put every reflector into the displaced-water pass and vice versa — a
            // failure that would look like the sea drawing boats.
            Assert.AreNotEqual(DisplacedWaterRegistry.RenderingLayer,
                               ReflectionRegistry.RenderingLayer);
            Assert.AreEqual(0u, DisplacedWaterRegistry.RenderingLayer & ReflectionRegistry.RenderingLayer);
        }

        [Test]
        public void NightLitFlag_ReachesTheRenderer()
        {
            // The §11.6 routing switch: 1 makes the HHReflect pass pre-compensate for the day/night
            // multiply, so the water's split sends this source to the post-grade bucket.
            SpriteRenderer sr = MakeReflector(1f, out ReflectiveObject reflector);
            Assert.IsFalse(reflector.NightLitSource, "ordinary surfaces are the default.");
            var block = new MaterialPropertyBlock();
            sr.GetPropertyBlock(block);
            Assert.AreEqual(0f, block.GetFloat(ReflectionShaderIds.ReflectLit), 1e-5f);
        }
    }
}
