using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Art;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// NMC H4: the ground's road floor binding, short of the streamer. A ground no streamer feeds (every region but
    /// Nine Mile Creek) reads _RoadLoaded 0; a pool that fits reaches the ground whole, with the grid's corner, its
    /// pixels per metre and the index's size; a pool the shader would misread is refused; a pool taken away turns the
    /// road floor back off at once. The sizes are written out by hand from RoadFloor4.hlsl's header (RF4_CHUNK 256,
    /// RF4_RAMP_PARAMS 160), not read from the C# under test.
    /// </summary>
    public class RoadFloorBindingTests
    {
        const int Slot = 256, RampWidth = 161, Depth = 4, GridW = 95, GridH = 70;
        static readonly Vector2 Origin = new Vector2(-380f, 280f);
        const float Ppm = 32f;

        [Test]
        public void AGroundNoStreamerFeeds_ReadsTheRoadFloorOff()
        {
            RequireTextureArrays();
            var go = new GameObject("RoadFloorUnfed");
            try
            {
                var splat = go.AddComponent<TerrainSplatSurface>();
                Assert.IsFalse(splat.RoadFloorLoaded, "a ground nothing configured says it has a road floor");
                Assert.AreEqual(0f, Block(go).GetFloat("_RoadLoaded"),
                    "a ground no streamer feeds reads a road floor: every other region's picture would change");
            }
            finally { Destroy(go); }
        }

        [Test]
        public void APoolThatFits_ReachesTheGround_WithItsGrid()
        {
            RequireTextureArrays();
            Texture2DArray normal = NewArray(Slot, Depth), light = NewArray(Slot, Depth), detail = NewArray(Slot, Depth);
            Texture2D ramp = Ramp(RampWidth, Depth), index = Index(GridW, GridH);
            var go = new GameObject("RoadFloorFits");
            try
            {
                var splat = go.AddComponent<TerrainSplatSurface>();
                splat.ConfigureRoadFloor(normal, light, detail, ramp, index, Origin, Ppm);
                Assert.IsTrue(splat.RoadFloorLoaded, "a pool that fits was refused");

                var mpb = Block(go);
                Assert.AreEqual(1f, mpb.GetFloat("_RoadLoaded"), "a pool that fits did not turn the road floor on");
                Assert.AreSame(normal, mpb.GetTexture("_RoadNormal"), "the normal array did not reach the ground");
                Assert.AreSame(light, mpb.GetTexture("_RoadLight"), "the light array did not reach the ground");
                Assert.AreSame(detail, mpb.GetTexture("_RoadDetail"), "the detail array did not reach the ground");
                Assert.AreSame(ramp, mpb.GetTexture("_RoadRamp"), "the ramp did not reach the ground");
                Assert.AreSame(index, mpb.GetTexture("_RoadIndex"), "the index did not reach the ground");
                Assert.AreEqual(new Vector4(Origin.x, Origin.y, Ppm, 0f), mpb.GetVector("_RoadOrigin"),
                    "the grid's north-west corner and pixels per metre are not (x, y, ppm, 0)");
                Assert.AreEqual(new Vector4(GridW, GridH, 0f, 0f), mpb.GetVector("_RoadGrid"),
                    "the grid's size is not the index's");
            }
            finally { Destroy(go, normal, light, detail, ramp, index); }
        }

        [Test]
        public void APoolTheShaderWouldMisread_IsRefused()
        {
            RequireTextureArrays();
            Texture2DArray normal = NewArray(Slot, Depth), light = NewArray(Slot, Depth), detail = NewArray(Slot, Depth);
            Texture2DArray shallow = NewArray(Slot, Depth - 1), narrow = NewArray(Slot / 2, Depth);
            Texture2D ramp = Ramp(RampWidth, Depth), thinRamp = Ramp(RampWidth - 80, Depth), index = Index(GridW, GridH);
            var go = new GameObject("RoadFloorMisfits");
            try
            {
                go.SetActive(false);
                var splat = go.AddComponent<TerrainSplatSurface>();
                void Refused(string why)
                {
                    Assert.IsFalse(splat.RoadFloorLoaded, why);
                }
                splat.ConfigureRoadFloor(shallow, light, detail, ramp, index, Origin, Ppm);
                Refused("a normal array one slot short of the ramp's rows was accepted");
                splat.ConfigureRoadFloor(normal, narrow, detail, ramp, index, Origin, Ppm);
                Refused("a light array of 128-texel slots was accepted");
                splat.ConfigureRoadFloor(normal, light, null, ramp, index, Origin, Ppm);
                Refused("two arrays of three were accepted");
                splat.ConfigureRoadFloor(normal, light, detail, thinRamp, index, Origin, Ppm);
                Refused("a ramp of sixteen palettes (the relight's 81 columns), with the parameters at 80, was accepted");
                splat.ConfigureRoadFloor(normal, light, detail, ramp, null, Origin, Ppm);
                Refused("a pool with no index was accepted: the shader could not tell a chunk's slot");
                splat.ConfigureRoadFloor(normal, light, detail, ramp, index, Origin, 0f);
                Refused("a pool with no pixels per metre was accepted");

                go.SetActive(true);
                Assert.AreEqual(0f, Block(go).GetFloat("_RoadLoaded"), "a pool that does not fit turned the road floor on");
            }
            finally { Destroy(go, normal, light, detail, shallow, narrow, ramp, thinRamp, index); }
        }

        [Test]
        public void APoolTakenAway_TurnsTheRoadFloorOff_AtOnce()
        {
            RequireTextureArrays();
            Texture2DArray normal = NewArray(Slot, Depth), light = NewArray(Slot, Depth), detail = NewArray(Slot, Depth);
            Texture2D ramp = Ramp(RampWidth, Depth), index = Index(GridW, GridH);
            var go = new GameObject("RoadFloorTakenAway");
            try
            {
                var splat = go.AddComponent<TerrainSplatSurface>();
                splat.ConfigureRoadFloor(normal, light, detail, ramp, index, Origin, Ppm);
                Assert.AreEqual(1f, Block(go).GetFloat("_RoadLoaded"), "premise: a pool that fits turns the road floor on");

                splat.ConfigureRoadFloor(null, null, null, null, null, Vector2.zero, 0f);   // the streamer closing
                Assert.AreEqual(0f, Block(go).GetFloat("_RoadLoaded"),
                    "the road floor stayed on after its pool was taken away: the shader would load destroyed arrays");
            }
            finally { Destroy(go, normal, light, detail, ramp, index); }
        }

        // ============================ helpers ============================

        static void RequireTextureArrays()
        {
            if (!SystemInfo.supports2DArrayTextures)
                Assert.Ignore("this graphics device has no texture arrays, so the road floor's pool cannot be made here.");
        }

        static Texture2DArray NewArray(int size, int depth) =>
            new Texture2DArray(size, size, depth, TextureFormat.RGBA32, false, true) { name = "RoadFloorBindingProbe" };

        static Texture2D Ramp(int width, int rows) =>
            new Texture2D(width, rows, TextureFormat.RGBAFloat, false, true) { name = "RoadFloorBindingRamp" };

        static Texture2D Index(int w, int h) =>
            new Texture2D(w, h, TextureFormat.RFloat, false, true) { name = "RoadFloorBindingIndex" };

        static MaterialPropertyBlock Block(GameObject host)
        {
            var renderer = host.GetComponentInChildren<MeshRenderer>(true);
            Assert.IsNotNull(renderer, "the surface built no ground quad, so it pushed nothing to read");
            var mpb = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(mpb);
            return mpb;
        }

        static void Destroy(params Object[] objects)
        {
            foreach (Object o in objects)
                if (o != null) Object.DestroyImmediate(o);
        }
    }
}
