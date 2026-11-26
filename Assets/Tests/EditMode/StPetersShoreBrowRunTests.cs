using HiddenHarbours.Art;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>The bank-face and turf-lip strips follow the brow traced inland from spring high. Where the trace
    /// folds, the fold is rejected and short scraps of brow were left behind; a strip four metres wide on a scrap
    /// drew a half disc of bank face, rings and all, cut off square by its neighbours, on the open grass above the
    /// north shore (it read as an unfinished ploughed field). These pin St Peters' own brow.</summary>
    public sealed class StPetersShoreBrowRunTests
    {
        const string Terrain = "Assets/_Project/Data/Terrain/";
        const string DefPath = "Assets/_Project/Data/Resources/TerrainShoreLines/StPetersShoreLines.asset";

        static ShoreLineDerivation.Result _result;
        static TerrainShoreLinesDef _def;

        static ShoreLineDerivation.Result Build()
        {
            if (_result != null) return _result;
            _def = AssetDatabase.LoadAssetAtPath<TerrainShoreLinesDef>(DefPath);
            Assert.IsNotNull(_def, DefPath + " is missing");
            var seabed = AssetDatabase.LoadAssetAtPath<HiddenHarbours.World.PaintedHeightMap>(Terrain + "StPetersSeabed.asset");
            Assert.IsNotNull(seabed, "St Peters' painted height is missing");
            Texture2D height = _def.HeightTexture;
            Assert.IsNotNull(height, "the shore lines name no height texture");
            Assert.IsTrue(height.isReadable, "the height texture must be readable");
            var splats = new Texture2D[6];
            for (int i = 0; i < 6; i++)
            {
                splats[i] = AssetDatabase.LoadAssetAtPath<Texture2D>(Terrain + "StPetersSplat" + "ABCDEF"[i] + ".png");
                Assert.IsNotNull(splats[i], "splat " + "ABCDEF"[i] + " is missing");
            }
            float min = seabed.MinElevation, max = seabed.MaxElevation;
            var elevations = new float[height.width * height.height];
            if (height.format == TextureFormat.R16)
            {
                var raw = height.GetPixelData<ushort>(0);
                for (int i = 0; i < elevations.Length; i++) elevations[i] = Mathf.Lerp(min, max, raw[i] / 65535f);
            }
            else
            {
                Color[] pixels = height.GetPixels();
                for (int i = 0; i < elevations.Length; i++) elevations[i] = Mathf.Lerp(min, max, pixels[i].r);
            }
            var reader = new ShoreLineDerivation.SplatReader(splats);
            Vector2 size = seabed.WorldSize, worldMin = seabed.WorldCenter - size * .5f;
            var grid = new ShoreLineDerivation.Grid(elevations, height.width, height.height, worldMin, size,
                p => reader.At(new Vector2((p.x - worldMin.x) / size.x, (p.y - worldMin.y) / size.y)));
            return _result = ShoreLineDerivation.Build(grid, _def);
        }

        [Test] public void TheShoreLines_DropBrowScrapsShorterThanTheirMinimum()
        {
            Assert.That(AssetDatabase.LoadAssetAtPath<TerrainShoreLinesDef>(DefPath).BrowMinimumRunMetres,
                Is.GreaterThanOrEqualTo(ShoreLineDerivation.StripMetres), "a run shorter than the strip is wide draws a half disc");
        }

        [TestCase(0, TestName = "EveryTurfLipRun_IsLongEnoughToReadAsABank")]
        [TestCase(1, TestName = "EveryBankFaceRun_IsLongEnoughToReadAsABank")]
        public void EveryBrowRun_IsLongEnough(int kind)
        {
            var result = Build();
            Assert.Greater(result.Lines[kind].Length, 0, "the island's real banks must keep their strips");
            foreach (float length in ShoreLineDerivation.RunLengths(result.Lines[kind]))
                Assert.That(length, Is.GreaterThanOrEqualTo(_def.BrowMinimumRunMetres - 1e-3f), $"a {length:0.00} m scrap of brow");
        }

        // The owner's 'field': open turf at +5.5 to +6 m, eight metres inland of the north shore's spring high.
        [TestCase(127f, 58.5f)]
        [TestCase(129f, 58.5f)]
        [TestCase(124.5f, 58f)]
        [TestCase(126f, 57f)]
        public void TheGrassAboveTheNorthShore_HasNoBankFace(float x, float y)
        {
            var result = Build();
            var p = new Vector2(x, y);
            Assert.IsFalse(ShoreLineDerivation.InBand(1, result.Sample(1, p)), $"a bank face is drawn on the grass at {p}");
            Assert.IsFalse(ShoreLineDerivation.InBand(0, result.Sample(0, p)), $"a turf lip is drawn on the grass at {p}");
        }
    }
}
