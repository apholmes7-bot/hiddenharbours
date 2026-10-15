using System.IO;
using NUnit.Framework;

namespace HiddenHarbours.Tests.Art.EditMode
{
    public class PlantShaderGlobalsTests
    {
        [Test] public void WindAndSnowUseTheSharedGlobalsAndCellRoot()
        {
            string shader = File.ReadAllText("Assets/_Project/Art/Shaders/HiddenHarboursPlant.shader");
            Assert.That(shader, Does.Contain("_WindWorld.xy")); Assert.That(shader, Does.Contain("saturate(_FoliageSnow)"));
            Assert.That(shader, Does.Contain("float bend = v.root.z * v.root.z"));
            Assert.That(shader, Does.Not.Contain("v.uv.y * v.uv.y"));
            Assert.That(shader, Does.Contain("#define WALKERS_N 8")); Assert.That(shader, Does.Contain("#define POINTS_N 24"));
            Assert.That(shader, Does.Contain("_GrassTrail[w * POINTS_N + p]"));
            Assert.That(shader, Does.Contain("walker.z < 0"));
        }
    }
}
