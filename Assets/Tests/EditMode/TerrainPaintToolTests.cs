using NUnit.Framework;
using HiddenHarbours.App.Editor;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The PURE terrain-TYPE → tile/height resolution rules of the Terrain Paint Tool (ADR 0014). These guard
    /// the headline coupling: each default type maps to the closest GENERATED terrain tile (or to height-only
    /// for the underwater types), and the height-only classification (no tile / clear-tile → erase any tile so
    /// the water shows) is correct. The scene-view brush + tile stamping + overlay draw are editor-API and not
    /// headless-testable; this exercises the resolution LOGIC the stamping branches on.
    ///
    /// <para>Lives in the TOP-LEVEL EditMode folder (not World/) because it references the editor-only
    /// <c>HiddenHarbours.App.Editor</c> assembly (the narrow per-module test asmdefs don't) — see the project
    /// note on EditMode asmdef references.</para>
    /// </summary>
    public class TerrainPaintToolTests
    {
        // ---- default type → closest generated terrain tile ----------------------------------------------

        [Test]
        public void ResolveDefaultTileName_UnderwaterTypes_AreHeightOnly()
        {
            Assert.IsNull(TerrainPaintTool.ResolveDefaultTileName("Deep"), "Deep paints no land tile");
            Assert.IsNull(TerrainPaintTool.ResolveDefaultTileName("Channel"), "Channel paints no land tile");
        }

        [Test]
        public void ResolveDefaultTileName_LandTypes_MapToTheGeneratedTiles()
        {
            // These names must match TileAssetBuilder.Terrain's generated tile names.
            Assert.AreEqual("Sand",  TerrainPaintTool.ResolveDefaultTileName("Beach"));
            Assert.AreEqual("Foam",  TerrainPaintTool.ResolveDefaultTileName("Sandbar"));  // closest "wet sand"
            Assert.AreEqual("Grass", TerrainPaintTool.ResolveDefaultTileName("Grass"));
            Assert.AreEqual("Rock",  TerrainPaintTool.ResolveDefaultTileName("Cliff"));
        }

        [Test]
        public void ResolveDefaultTileName_UnknownType_IsHeightOnly()
        {
            Assert.IsNull(TerrainPaintTool.ResolveDefaultTileName("SomethingCustom"),
                "an unknown/custom type resolves to height-only unless the owner assigns a tile");
        }

        // ---- the resolved tile names are members of the real generated tile set -------------------------

        [Test]
        public void ResolveDefaultTileName_ResolvedNames_ExistInTheGeneratedTerrainSet()
        {
            foreach (string type in new[] { "Beach", "Sandbar", "Grass", "Cliff" })
            {
                string tileName = TerrainPaintTool.ResolveDefaultTileName(type);
                Assert.IsNotNull(tileName, $"{type} maps to a tile");
                bool found = false;
                foreach (var (_, generated) in HiddenHarbours.Art.Editor.TileAssetBuilder.Terrain)
                    if (generated == tileName) { found = true; break; }
                Assert.IsTrue(found, $"'{tileName}' (for {type}) is one of the generated terrain tiles");
            }
        }

        // ---- height-only classification (the underwater "clear the tile so water shows" rule) ------------

        [Test]
        public void IsHeightOnly_ClearTile_IsAlwaysHeightOnly()
        {
            Assert.IsTrue(TerrainPaintTool.IsHeightOnly(hasGroundTile: true, clearTile: true),
                "clearTile forces height-only even with a tile assigned (underwater types)");
            Assert.IsTrue(TerrainPaintTool.IsHeightOnly(hasGroundTile: false, clearTile: true));
        }

        [Test]
        public void IsHeightOnly_NoTile_IsHeightOnly()
        {
            Assert.IsTrue(TerrainPaintTool.IsHeightOnly(hasGroundTile: false, clearTile: false),
                "no ground tile → height-only (clears any tile)");
        }

        [Test]
        public void IsHeightOnly_HasTileAndNotCleared_PaintsTheTile()
        {
            Assert.IsFalse(TerrainPaintTool.IsHeightOnly(hasGroundTile: true, clearTile: false),
                "a land type with a tile and clearTile off paints the tile (look + height)");
        }
    }
}
