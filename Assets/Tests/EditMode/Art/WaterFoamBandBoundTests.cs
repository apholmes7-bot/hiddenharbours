using System.IO;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Art;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The shore foam band, bounded on the ground (2026-11-26, the Whelps shelf sheets): <c>_FoamWidth</c> is a DEPTH,
    /// so on a near-flat shelf the band drew tens of metres wide. <see cref="WaterSurface.FoamBandDepth"/> is the
    /// shader's <c>foamBandDepth</c>; these pin the twin, the shipped dials and the shader text together.
    /// </summary>
    public class WaterFoamBandBoundTests
    {
        const string ShaderPath = "Assets/_Project/Art/Shaders/HiddenHarboursWater.shader";
        const string MaterialDir = "Assets/_Project/Art/Materials";
        const float ShippedMax = 2.5f, ShippedFloor = 0.02f;
        const float PresetWidth = 0.45f;          // the presets' _FoamWidth, the mood blend's anchors
        const float SwirlGuardCoast = 0.18f;      // WaterWhiteoutShoreSwirlAcceptanceTests.BeachSlope

        [Test]
        public void Off_IsTheOldDepthOnlyBand()
        {
            foreach (float slope in new[] { 0f, 0.02f, 0.18f, 1f })
                Assert.AreEqual(PresetWidth, WaterSurface.FoamBandDepth(PresetWidth, 0f, slope, ShippedFloor), 0f,
                    "_FoamBandMaxMetres = 0 must be the old band to the bit");
        }

        [Test]
        public void TheReferenceCoastAndAnythingSteeper_KeepTheirBand()
        {
            foreach (float slope in new[] { SwirlGuardCoast, 0.3f, 0.6f, 1f })
                Assert.AreEqual(PresetWidth, WaterSurface.FoamBandDepth(PresetWidth, ShippedMax, slope, ShippedFloor), 1e-6f,
                    $"a {slope} m/m coast lost foam: the bound must only bite on shelves flatter than the reference coast");
        }

        [Test]
        public void AFlatShelf_IsBoundToTheGroundWidth()
        {
            foreach (float slope in new[] { 0.02f, 0.05f, 0.1f })
            {
                float before = PresetWidth / slope;
                float after = WaterSurface.FoamBandDepth(PresetWidth, ShippedMax, slope, ShippedFloor) / slope;
                Assert.LessOrEqual(after, ShippedMax + 1e-4f,
                    $"on a {slope} m/m shelf the band is still {after:F1} m across the ground (was {before:F1} m)");
            }
        }

        [Test]
        public void AFlatTexelRun_StillKeepsAThinLine()
        {
            float depth = WaterSurface.FoamBandDepth(PresetWidth, ShippedMax, 0f, ShippedFloor);
            Assert.AreEqual(ShippedMax * ShippedFloor, depth, 1e-6f,
                "a slope of 0 (a flat run of the 8-bit height) must fall back to the floor, never to no foam");
        }

        [Test]
        public void EveryWaterMaterial_ShipsTheBound()
        {
            int seen = 0;
            foreach (string path in Directory.GetFiles(MaterialDir, "*.mat", SearchOption.AllDirectories))
            {
                string text = File.ReadAllText(path);
                if (!text.Contains("60e801f535ab482ca783280c9f07d6cb")) continue;    // HiddenHarboursWater.shader
                seen++;
                StringAssert.Contains($"- _FoamBandMaxMetres: {ShippedMax.ToString(System.Globalization.CultureInfo.InvariantCulture)}", text,
                    $"{path} omits or moved _FoamBandMaxMetres: a preset Apply would stamp the shader's 0 (off) over it");
                StringAssert.Contains($"- _FoamBandSlopeFloor: {ShippedFloor.ToString(System.Globalization.CultureInfo.InvariantCulture)}", text,
                    $"{path} omits or moved _FoamBandSlopeFloor");
            }
            Assert.GreaterOrEqual(seen, 9, "fewer water materials than the live one and the eight presets");
        }

        [Test]
        public void TheShader_DeclaresAndBoundsTheBand()
        {
            string src = File.ReadAllText(ShaderPath);
            StringAssert.Contains("_FoamBandMaxMetres  (", src);
            StringAssert.Contains("_FoamBandSlopeFloor (", src);
            StringAssert.Contains("float  _FoamBandMaxMetres;", src);
            StringAssert.Contains("float  _FoamBandSlopeFloor;", src);
            StringAssert.Contains("* max(shoreSlope, saturate(_FoamBandSlopeFloor))), 1e-3);", src);
            StringAssert.Contains("float foamEdge = 1.0 - smoothstep(0.0, foamBandDepth, foamDepth + foamEdgeDither);", src);
        }
    }
}
