using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Art;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Terrain pass 9's Path slot (docs/design/st-peters-terrain-pass-9.md): the kit's Path is painted in
    /// the r channel of a sixth splat map, _SplatF.
    ///
    /// <para>The surface's half: a sixth map handed to the surface reaches the ground's renderer as
    /// _SplatF, and a missing one is pushed as "nothing painted", the transparent 1x1, rather than left to
    /// whatever the material holds. Without the push, Path painted on an island would never reach the
    /// shader, and nothing would say so.</para>
    /// </summary>
    public class TerrainSplatPathSlotTests
    {
        [Test]
        public void ASixthMap_ReachesTheGround_AsSplatF()
        {
            var f = new Texture2D(4, 4, TextureFormat.RGBA32, false) { name = "PathSlotProbeF" };
            var go = new GameObject("PathSlotSixMaps");
            try
            {
                go.SetActive(false);
                var splat = go.AddComponent<TerrainSplatSurface>();
                splat.ConfigureSplat(null, null, null, null, null, f);
                go.SetActive(true);   // OnEnable builds the quad and pushes every map

                Assert.AreSame(f, Pushed(go, "_SplatF"),
                    "the sixth splat map did not reach the ground as _SplatF, so Path painted in it never shows");
            }
            finally
            {
                Object.DestroyImmediate(go);
                Object.DestroyImmediate(f);
            }
        }

        [Test]
        public void NoSixthMap_IsPushedAsNothingPainted_NotLeftToTheMaterial()
        {
            var go = new GameObject("PathSlotNoSixthMap");
            try
            {
                go.SetActive(false);
                var splat = go.AddComponent<TerrainSplatSurface>();
                splat.ConfigureSplat(null, null, null, null, null, null);
                go.SetActive(true);

                var pushed = Pushed(go, "_SplatF") as Texture2D;
                Assert.IsNotNull(pushed, "no _SplatF was pushed, so the ground reads whatever the material holds");
                Assert.AreEqual(1, pushed.width, "a missing sixth map is not the 1x1 'nothing painted'");
                Assert.AreEqual(1, pushed.height, "a missing sixth map is not the 1x1 'nothing painted'");
                Color32 texel = pushed.GetPixels32()[0];
                Assert.AreEqual(0, texel.r, "a missing sixth map paints Path everywhere (its r is not 0)");
                Assert.AreEqual(0, texel.a, "a missing sixth map is not transparent");
            }
            finally { Object.DestroyImmediate(go); }
        }

        /// <summary>What the surface pushed to its ground quad's renderer for <paramref name="property"/>.</summary>
        private static Texture Pushed(GameObject host, string property)
        {
            var renderer = host.GetComponentInChildren<MeshRenderer>(true);
            Assert.IsNotNull(renderer, "the surface built no ground quad, so it pushed nothing to read");
            var mpb = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(mpb);
            return mpb.GetTexture(property);
        }
    }
}
