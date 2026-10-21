using System.IO;
using NUnit.Framework;
using HiddenHarbours.Art.Editor;

namespace HiddenHarbours.Tests.EditMode
{
    public class TerrainArrayTreeTests
    {
        [Test]
        public void DerivedOutputsAndRetiredWriters_DoNotReturn()
        {
            const string derived = "Assets/_Project/Art/Terrain/Derived";
            Assert.IsFalse(Directory.Exists(derived), "Ignored packed files must not be recreated in Assets.");
            Assert.IsFalse(File.Exists(derived + ".meta"), "The retired folder metadata returned.");
            Assert.IsTrue(File.Exists(TerrainArrayAssets.DetailRecipePath));
            Assert.IsTrue(File.Exists(TerrainArrayAssets.RelightRecipePath));
            foreach (string path in new[]
            {
                "Assets/_Project/Art/Editor/TerrainTexArrayBuilder.cs",
                "Assets/_Project/Art/Editor/TerrainArrayImporter.cs",
                "Assets/_Project/Art/Editor/TerrainArrayAssets.cs",
                "Assets/_Project/Code/App/Editor/StPetersBuilder.cs",
                "Assets/_Project/Code/App/Editor/NineMileCreekBuilder.cs",
                "Assets/_Project/Code/App/Editor/TerrainPass9Plate.cs",
            })
            {
                string source = File.ReadAllText(path);
                foreach (string retired in new[] { "TerrainTexArrayBuilder.Build(", "TerrainTexArrayBuilder.BuildRelight(",
                    "SaveInPlace", "SetRelightImports", "Array256Path", "Array512Path", "RelightArrayPaths", "RelightRampPath",
                    "/TerrainDetail256.asset", "/TerrainRelightNormal.asset", "/TerrainRelightLight.asset",
                    "/TerrainRelightDetail.asset", "/TerrainRelightRamp.asset" })
                    StringAssert.DoesNotContain(retired, source, path + " still has a retired writer/path.");
            }
            string ignore = File.ReadAllText(".gitignore");
            StringAssert.Contains("/[Aa]ssets/_Project/Art/Terrain/Derived/", ignore);
            StringAssert.Contains("/[Aa]ssets/_Project/Art/Terrain/Derived.meta", ignore);
            string attributes = File.ReadAllText(".gitattributes");
            StringAssert.Contains("Assets/_Project/Art/Terrain/Derived/*.asset filter=lfs diff=lfs merge=lfs -text", attributes);
            StringAssert.Contains("*.hhterrain text eol=lf", attributes);
        }
    }
}
