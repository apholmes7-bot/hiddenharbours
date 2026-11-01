using System;
using System.IO;
using HiddenHarbours.Tools.RigBaking;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tests.RigBaking
{
    public class StPetersLandingDeckBakeTests
    {
        const string FixturePath = "Assets/Tests/EditMode/RigBaking/Fixtures/stpLandingDeck.node.json";
        [Serializable] sealed class NodeFixture
        {
            public string schema, recipeSha256, albedoSha256, heightSha256;
            public int width, height;
            public float pivotX, pivotY;
        }

        static string Abs(string path) => Path.Combine(RigCatalog.RepoRoot, path);
        static NodeFixture Fixture() => JsonUtility.FromJson<NodeFixture>(File.ReadAllText(Abs(FixturePath)));

        static byte[] ReadPng(string path)
        {
            var image = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
            try
            {
                Assert.That(image.LoadImage(File.ReadAllBytes(Abs(path)), false), Is.True);
                var colors = image.GetPixels32(); var bytes = new byte[colors.Length * 4];
                for (int y = 0; y < image.height; y++)
                for (int x = 0; x < image.width; x++)
                {
                    Color32 pixel = colors[(image.height - y - 1) * image.width + x];
                    int i = (y * image.width + x) * 4;
                    bytes[i] = pixel.r; bytes[i + 1] = pixel.g; bytes[i + 2] = pixel.b; bytes[i + 3] = pixel.a;
                }
                return bytes;
            }
            finally { UnityEngine.Object.DestroyImmediate(image); }
        }

        [Test]
        public void RecipeAndInstalledSourcesAreTheInputsPinnedByTheContract()
        {
            var c = StPetersLandingDeckBaker.ReadContract(); var node = Fixture();
            Assert.That(node.schema, Is.EqualTo("hidden-harbours/stp-landing-deck-node@1"));
            string recipeHash = StPetersLandingDeckBaker.Sha(File.ReadAllBytes(Abs(StPetersLandingDeckBaker.RecipePath)));
            Assert.That(c.recipeSha256, Is.EqualTo(recipeHash)); Assert.That(node.recipeSha256, Is.EqualTo(recipeHash));
            Assert.That(c.scripts.Length, Is.EqualTo(6));
            foreach (var script in c.scripts)
                Assert.That(RigRecipe.Sha256OfSource(script.path), Is.EqualTo(script.sha256), script.path);
            Assert.That(c.tideFinding, Does.Contain("occlusion is absent"));
        }

        [Test]
        public void AlbedoAndRigHeightAreTheRealNodeFixturePixelForPixel()
        {
            var node = Fixture(); var c = StPetersLandingDeckBaker.ReadContract();
            Assert.That(c.width, Is.EqualTo(node.width)); Assert.That(c.height, Is.EqualTo(node.height));
            Assert.That(c.pivotX, Is.EqualTo(node.pivotX)); Assert.That(c.pivotY, Is.EqualTo(node.pivotY));
            Assert.That(StPetersLandingDeckBaker.Sha(ReadPng(StPetersLandingDeckBaker.SheetPath)), Is.EqualTo(node.albedoSha256));
            Assert.That(StPetersLandingDeckBaker.Sha(ReadPng(StPetersLandingDeckBaker.HeightPath)), Is.EqualTo(node.heightSha256));
            using var host = RigScriptHostFactory.Create();
            StPetersLandingDeckBaker.Install(host);
            var live = StPetersLandingDeckBaker.Render(host, StPetersLandingDeckBaker.ReadRecipe(), c.bed);
            Assert.That(StPetersLandingDeckBaker.Sha(live.Albedo), Is.EqualTo(node.albedoSha256));
            Assert.That(StPetersLandingDeckBaker.Sha(live.HeightMap), Is.EqualTo(node.heightSha256));
        }

        [Test]
        public void NativeFrameAndPivotUseTheSetPieceCapAndExplicitChannelFormats()
        {
            var c = StPetersLandingDeckBaker.ReadContract();
            Assert.That(c.importSizeCap, Is.EqualTo(2048)); Assert.That(c.width, Is.LessThanOrEqualTo(2048));
            Assert.That(c.height, Is.LessThanOrEqualTo(2048)); Assert.That(c.pixelsPerUnit, Is.EqualTo(32));
            foreach (string path in new[] { StPetersLandingDeckBaker.SheetPath, StPetersLandingDeckBaker.HeightPath })
            {
                bool data = path == StPetersLandingDeckBaker.HeightPath;
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point)); Assert.That(importer.mipmapEnabled, Is.False);
                Assert.That(importer.sRGBTexture, Is.EqualTo(!data));
                Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.Uncompressed));
                Assert.That(importer.GetDefaultPlatformTextureSettings().format,
                    Is.EqualTo(data ? TextureImporterFormat.R8 : TextureImporterFormat.RGBA32));
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                Assert.That(texture.width, Is.EqualTo(c.width)); Assert.That(texture.height, Is.EqualTo(c.height));
                if (!data)
                {
                    var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                    Assert.That(sprite.pivot.x, Is.EqualTo(c.pivotX).Within(0.001f));
                    Assert.That(sprite.pivot.y, Is.EqualTo(c.height - c.pivotY).Within(0.001f));
                }
            }
        }
    }
}
