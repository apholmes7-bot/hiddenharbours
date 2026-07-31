using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Art;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// ADR 0028's drift guard: the terrain splat shader is a second IMPLEMENTATION of the band look,
    /// but <see cref="StPetersShoreMap"/> stays the single source of truth — the builder pushes the
    /// CPU constants into the surface at build time, and THIS pins the shipped material/shader
    /// DEFAULTS to the same numbers, so an edit to either side that forgets the other fails red
    /// instead of silently splitting the picture from the classifier.
    ///
    /// <para>Also pins the sorting contract the tide reveal depends on: the ground quad must sit
    /// below the retained tile layers AND below the Sea plane at −5 (ADR 0012 — the water clips
    /// itself transparent over dry ground; anything at or above it would cover the reveal).</para>
    /// </summary>
    public class TerrainSplatBandPinTests
    {
        private const string SplatMaterialPath = "Assets/_Project/Art/Materials/TerrainSplat.mat";
        private const int SeaSortingOrder = -5;   // the builder's Sea plane slot (StPetersBuilder)

        private static Material LoadMat()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(SplatMaterialPath);
            Assert.IsNotNull(mat, $"'{SplatMaterialPath}' missing — the pin guards nothing.");
            return mat;
        }

        [Test]
        public void MaterialDefaults_MatchTheCpuClassifierConstants()
        {
            var mat = LoadMat();

            // The band ladder (StPetersShoreMap §floors).
            Assert.AreEqual(StPetersShoreMap.PaintFloorElevation, mat.GetFloat("_FloorPaint"), 1e-4f,
                "_FloorPaint drifted from StPetersShoreMap.PaintFloorElevation.");
            Assert.AreEqual(StPetersShoreMap.RippleFloorElevation, mat.GetFloat("_FloorRipple"), 1e-4f,
                "_FloorRipple drifted from StPetersShoreMap.RippleFloorElevation.");
            Assert.AreEqual(StPetersShoreMap.SandFloorElevation, mat.GetFloat("_FloorSand"), 1e-4f,
                "_FloorSand drifted from StPetersShoreMap.SandFloorElevation.");
            Assert.AreEqual(StPetersShoreMap.MarramFloorElevation, mat.GetFloat("_FloorMarram"), 1e-4f,
                "_FloorMarram drifted from StPetersShoreMap.MarramFloorElevation.");
            Assert.AreEqual(StPetersShoreMap.GrassFloorElevation, mat.GetFloat("_FloorGrass"), 1e-4f,
                "_FloorGrass drifted from StPetersShoreMap.GrassFloorElevation.");
            Assert.AreEqual(StPetersShoreMap.ShingleFloorElevation, mat.GetFloat("_FloorShingle"), 1e-4f,
                "_FloorShingle drifted from StPetersShoreMap.ShingleFloorElevation.");

            // The meander (same parameters as the CPU wiggle; the lattice hash is look-only free).
            Assert.AreEqual(StPetersShoreMap.BandWiggleMetres, mat.GetFloat("_BandWiggleMetres"), 1e-4f);
            Assert.AreEqual(StPetersShoreMap.BandWiggleScale, mat.GetFloat("_BandWiggleScale"), 1e-4f);
            Assert.AreEqual(StPetersShoreMap.BandDetailMetres, mat.GetFloat("_BandDetailMetres"), 1e-4f);
            Assert.AreEqual(StPetersShoreMap.BandDetailScale, mat.GetFloat("_BandDetailScale"), 1e-4f);

            // The sector + the bar's wiggle-exempt vocabulary.
            Assert.AreEqual(StPetersShoreMap.SectorFeather, mat.GetFloat("_SectorFeather"), 1e-4f);
            Assert.AreEqual(StPetersShoreMap.BarSpineHalfWidth, mat.GetFloat("_BarSpineHalfWidth"), 1e-4f);
            Assert.AreEqual(StPetersShoreMap.BarSpineFloorElevation, mat.GetFloat("_BarSpineFloor"), 1e-4f);
        }

        [Test]
        public void GroundSortsBelowTheTileBandAndTheSea()
        {
            Assert.Less(TerrainSplatSurface.DefaultSortingOrder, StPetersShorePainter.GroundSortingOrder,
                "The splat ground must render UNDER the retained tile layers.");
            Assert.Less(TerrainSplatSurface.DefaultSortingOrder, SeaSortingOrder,
                "The splat ground must render UNDER the Sea plane or the ADR 0012 tide reveal breaks.");
        }

        [Test]
        public void MaterialUsesTheTerrainSplatShader()
        {
            // By ASSET, not by Shader.name — the name is empty in batch mode (the water sweep's
            // path-based discovery exists for the same reason).
            var mat = LoadMat();
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(
                "Assets/_Project/Art/Shaders/HiddenHarboursTerrainSplat.shader");
            Assert.IsNotNull(shader, "HiddenHarboursTerrainSplat.shader missing.");
            Assert.AreEqual(shader, mat.shader,
                "TerrainSplat.mat is not on HiddenHarbours/TerrainSplat — the defaults pinned above " +
                "would be someone else's defaults.");
        }
    }
}
