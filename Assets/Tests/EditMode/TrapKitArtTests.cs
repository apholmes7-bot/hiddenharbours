using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Import-settings guard for the owner's Tier-1 trap-fishing art kit (Art/Fishing) — the
    /// <c>FishingBoatFacingArtTests</c> discipline applied to the new sheets. The artist's README is the
    /// contract: 32 px = 1 m (PPU 32), no AA (point filter, uncompressed), sheets slice into equal
    /// one-row cells left-to-right with the documented pivots. A re-export or a re-slice that drifts any
    /// of this fails RED here before it can silently shrink a pot or float a buoy off its waterline.
    /// </summary>
    public class TrapKitArtTests
    {
        private const string ArtDir = "Assets/_Project/Art/Fishing/";
        private const float Ppu = 32f;
        private const float PivotTol = 0.01f;   // pivots are exact ratios; tolerance covers float text

        private struct SheetSpec
        {
            public string File;
            public int CellW, CellH, Count;   // Count 0 = Single-mode sprite
            public Vector2 PivotPx;           // expected sprite.pivot, in pixels within the cell
        }

        // The artist's README, as data: cell sizes, counts, and pivots (top-left pixel coords converted
        // to Unity's bottom-left pixel pivots: pivotY = cellH - readmeY).
        private static readonly SheetSpec[] Kit =
        {
            new SheetSpec { File = "Lobster.png",      CellW = 48, CellH = 32, Count = 0, PivotPx = new Vector2(24f, 16f) },
            new SheetSpec { File = "RockCrab.png",     CellW = 48, CellH = 32, Count = 0, PivotPx = new Vector2(24f, 16f) },
            new SheetSpec { File = "PotWood.png",      CellW = 44, CellH = 36, Count = 0, PivotPx = new Vector2(22f, 4f) },
            new SheetSpec { File = "PotWoodWet.png",   CellW = 44, CellH = 36, Count = 0, PivotPx = new Vector2(22f, 4f) },
            new SheetSpec { File = "PotWire.png",      CellW = 44, CellH = 36, Count = 0, PivotPx = new Vector2(22f, 4f) },
            new SheetSpec { File = "PotWireWet.png",   CellW = 44, CellH = 36, Count = 0, PivotPx = new Vector2(22f, 4f) },
            new SheetSpec { File = "LobsterDeck.png",  CellW = 48, CellH = 48, Count = 8, PivotPx = new Vector2(24f, 24f) },
            new SheetSpec { File = "RockCrabDeck.png", CellW = 48, CellH = 48, Count = 8, PivotPx = new Vector2(24f, 24f) },
            new SheetSpec { File = "SplashBurst.png",  CellW = 48, CellH = 48, Count = 8, PivotPx = new Vector2(24f, 14f) },
            new SheetSpec { File = "LobsterBuoys.png", CellW = 24, CellH = 48, Count = 8, PivotPx = new Vector2(12f, 2f) },
            new SheetSpec { File = "RopeProps.png",    CellW = 40, CellH = 32, Count = 3, PivotPx = new Vector2(20f, 2f) },
        };

        [Test]
        public void EveryKitTexture_ImportsOnTheProjectPixelArtSettings()
        {
            foreach (var spec in Kit)
            {
                string path = ArtDir + spec.File;
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                Assert.IsNotNull(importer, $"{path}: TextureImporter (missing PNG or meta?)");

                Assert.AreEqual(Ppu, importer.spritePixelsPerUnit, $"{path}: 32 px = 1 m — the kit's scale");
                Assert.AreEqual(FilterMode.Point, importer.filterMode, $"{path}: no AA — point filter");
                Assert.AreEqual(TextureImporterCompression.Uncompressed, importer.textureCompression,
                    $"{path}: pixel art ships uncompressed");
                Assert.AreEqual(spec.Count == 0 ? SpriteImportMode.Single : SpriteImportMode.Multiple,
                    importer.spriteImportMode, $"{path}: sprite mode per the README");
            }
        }

        [Test]
        public void EverySheet_SlicesIntoItsReadmeCells_LeftToRight()
        {
            foreach (var spec in Kit)
            {
                if (spec.Count == 0) continue;
                string path = ArtDir + spec.File;
                string baseName = spec.File.Substring(0, spec.File.Length - 4);

                // Multiple-mode: LoadAssetAtPath<Sprite> returns null — LoadAllAssetsAtPath is the rule.
                var sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>()
                    .OrderBy(s => s.rect.x).ToArray();
                Assert.AreEqual(spec.Count, sprites.Length, $"{path}: cell count per the README");

                for (int i = 0; i < sprites.Length; i++)
                {
                    Sprite s = sprites[i];
                    Assert.AreEqual($"{baseName}_{i}", s.name,
                        $"{path}: cell {i} keeps the _N naming the sheet loaders sort by");
                    Assert.AreEqual(i * spec.CellW, (int)s.rect.x, $"{path}[{i}]: left-to-right, no gaps");
                    Assert.AreEqual(spec.CellW, (int)s.rect.width, $"{path}[{i}]: cell width");
                    Assert.AreEqual(spec.CellH, (int)s.rect.height, $"{path}[{i}]: cell height");
                }
            }
        }

        [Test]
        public void EverySprite_CarriesTheArtistsPivot()
        {
            foreach (var spec in Kit)
            {
                string path = ArtDir + spec.File;
                var sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToArray();
                Assert.IsTrue(sprites.Length > 0, $"{path}: at least one sprite imports");

                foreach (Sprite s in sprites)
                {
                    Assert.AreEqual(spec.PivotPx.x, s.pivot.x, PivotTol,
                        $"{path}/{s.name}: pivot.x per the README — off-pivot art pops when it swaps in");
                    Assert.AreEqual(spec.PivotPx.y, s.pivot.y, PivotTol,
                        $"{path}/{s.name}: pivot.y per the README");
                    Assert.AreEqual(Ppu, s.pixelsPerUnit, $"{path}/{s.name}: sprite PPU");
                }
            }
        }
    }
}
