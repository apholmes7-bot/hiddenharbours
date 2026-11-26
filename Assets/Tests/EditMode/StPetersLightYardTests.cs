using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.App.Editor;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The keeper's yard as the kit dresses it: two beds inside the garden's pickets with a walk through its gate, the
    /// clothesline at true size set back in the yard, the fog bell's shadow, and every yard picture on the project's
    /// pixel grid (32 px a metre, point filtered, one sprite, never scaled).
    /// </summary>
    public class StPetersLightYardTests
    {
        const float GardenWest = 187.2f, GardenEast = 191.8f, GardenSouth = 58f, GardenNorth = 61.6f;
        const float GateWest = 189.1f, GateEast = 190.2f;

        static PropPieceKitDef Kit() => AssetDatabase.LoadAssetAtPath<PropPieceKitDef>(StPetersLightPiecesDefs.KitPath);

        static StPetersLayerRefresh.KeyScenePlacement Placed(string id) =>
            StPetersLayerRefresh.PlaceKeyScenes(StPetersLayerRefresh.LoadStPetersKeyScenes(), new StPetersLayerRefresh.EditorKeySceneAssets())
                .Single(p => p.Id == id);

        static bool Shows(StPetersLayerRefresh.KeyScenePart part, Sprite sprite) =>
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(sprite, out string guid, out long local) &&
            part.Sprite.Sprite.Guid == guid && part.Sprite.Sprite.FileId == local;

        [Test]
        public void TheGardenBeds_StandInsideItsPickets_AndItsWalkComesInThroughTheGate()
        {
            PropPieceKitDef kit = Kit();
            PropPieceDef bed = kit.EntryFor("gardenBed").Main;
            StPetersLayerRefresh.KeyScenePlacement garden = Placed(StPetersLightPiecesDefs.GardenFenceId);
            var beds = garden.Parts.Where(p => Shows(p, bed.Sprites[0])).ToList();
            Assert.AreEqual(2, beds.Count, "the garden holds two beds");
            Vector2[] foot = bed.Colliders.Single().Points;
            float hx = foot.Max(q => q.x), hy = foot.Max(q => q.y);
            foreach (var b in beds)
            {
                Assert.Greater(b.At.x - hx, GardenWest, $"{b.Name} reaches past the west pickets");
                Assert.Less(b.At.x + hx, GardenEast, $"{b.Name} reaches past the east pickets");
                Assert.Greater(b.At.y - hy, GardenSouth, $"{b.Name} reaches past the south pickets");
                Assert.Less(b.At.y + hy, GardenNorth, $"{b.Name} reaches past the north pickets");
                Assert.IsFalse(b.Floor, $"{b.Name} stands, and sorts by its ground line");
            }
            foreach (var b in beds)
                Assert.IsTrue(garden.Walls.Any(w => w.Length == 4 &&
                                                    Vector2.Distance(w.Aggregate((s, q) => s + q) / 4f + garden.At, b.At) < 0.01f),
                              $"{b.Name} does not block a walker");

            PropPieceExtra walk = kit.DressingFor(StPetersLightPiecesDefs.GardenFenceId).Extras.Single(x => x.Piece == "plank");
            float half = StPetersLayerRefresh.PlankWidthMetres / 2f;
            float wx = garden.At.x + walk.Path[0].x;
            Assert.Greater(wx - half, GateWest, "the walk's planks run into the gate post");
            Assert.Less(wx + half, GateEast, "the walk's planks run into the gate post");
            Assert.Less(garden.At.y + walk.Path[0].y, GardenSouth, "the walk starts outside the garden");
            Assert.Greater(garden.At.y + walk.Path[^1].y, GardenSouth, "the walk comes in through the gate");
            foreach (var b in beds)
                Assert.Greater(Mathf.Abs(b.At.x - wx), hx + half, $"{b.Name} stands on the walk");
            Assert.IsTrue(garden.Parts.Any(p => p.Floor), "the walk is laid");
        }

        [Test]
        public void TheClothesline_StandsBackInTheYard_TwoMetrePostsAndASixMetreLine()
        {
            StPetersLayerRefresh.KeyScenePlacement line = Placed(StPetersLightPiecesDefs.ClotheslineId);
            Assert.AreEqual(185.6f, line.At.x, 1e-4f);
            Assert.AreEqual(55.6f, line.At.y, 1e-4f);
            PropPieceDef def = Kit().EntryFor("clothesline").Main;
            Assert.AreEqual(2, def.Colliders.Length, "two posts block a walker; the line does not");
            float a = def.Colliders[0].Points.Average(q => q.x), b = def.Colliders[1].Points.Average(q => q.x);
            Assert.AreEqual(6f, Mathf.Abs(a - b), 0.01f, "the line runs six metres post to post");
            Assert.AreEqual(2f, def.HeightMetres, 0.01f, "a post stands two metres");
            // A two-metre post at the camera's 40 degrees stands 2 x cos 40 x 32 = 49 px tall.
            Assert.GreaterOrEqual(def.Sprites[0].rect.height, 49f, "the picture is shorter than a two-metre post");
        }

        [Test]
        public void TheFogBell_CastsTheSunsShadow()
        {
            Assert.IsTrue(Kit().EntryFor("fogBell").Main.CastsShadow);
            Assert.IsTrue(Placed("structure.stp_ne_fog_bell").CastsShadow);
            // The clothesline carries no baked caster; the runtime sun-caster installer gives it its shadow (the
            // garments' rectangles on the grass north of the line at noon), as it does every standing sprite.
            Assert.IsFalse(Placed(StPetersLightPiecesDefs.ClotheslineId).CastsShadow);
        }

        [Test]
        public void EveryYardPicture_IsOnTheProjectsPixelGrid_PointFilteredOneSpriteNoMips()
        {
            string[] pngs = Directory.GetFiles(StPetersLightPiecesDefs.ArtFolder, "*.png", SearchOption.AllDirectories);
            Assert.Greater(pngs.Length, 70);
            foreach (string f in pngs.Select(x => x.Replace('\\', '/')))
            {
                var imp = (TextureImporter)AssetImporter.GetAtPath(f);
                Assert.AreEqual(32f, imp.spritePixelsPerUnit, f);
                Assert.AreEqual(FilterMode.Point, imp.filterMode, f);
                Assert.IsFalse(imp.mipmapEnabled, f);
                Assert.AreEqual(SpriteImportMode.Single, imp.spriteImportMode, f);
                Assert.AreEqual(TextureImporterCompression.Uncompressed, imp.textureCompression, f);
            }
        }

        [Test]
        public void TheBakedFloorStrips_MatchTheirPlanks()
        {
            PropPieceKitDef kit = Kit();
            Assert.IsNotEmpty(kit.BakedRuns, "the floor runs are baked");
            foreach (string row in kit.BakedRuns.Select(b => b.Row).Distinct())
            {
                StPetersLayerRefresh.KeyScenePlacement p = Placed(row);
                var strips = p.Parts.Where(x => x.Floor).ToList();
                Assert.AreEqual(kit.BakedRuns.Count(b => b.Row == row), strips.Count, $"{row} draws one picture a strip");
                Assert.IsTrue(strips.All(s => s.Name.Contains("~")), $"{row}'s floor is its baked strips");
                foreach (var s in strips)
                {
                    Assert.AreEqual(0f, Mathf.Repeat(s.At.x * 32f, 1f) * (1f - Mathf.Repeat(s.At.x * 32f, 1f)), 1e-3f, $"{s.Name} is off the pixel grid");
                    Assert.AreEqual(0f, Mathf.Repeat(s.At.y * 32f, 1f) * (1f - Mathf.Repeat(s.At.y * 32f, 1f)), 1e-3f, $"{s.Name} is off the pixel grid");
                }
            }
        }
    }
}
