using System.IO;
using System.Linq;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Art;
using HiddenHarbours.World;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    public class StPetersKeySceneWaveTests
    {
        [Test]
        public void NoPieceThatComesAndGoes_IsPlaced()
        {
            var scenes = StPetersLayerRefresh.LoadStPetersKeyScenes();
            var conditional = scenes.SelectMany(s => s.Pieces.Select(p => (Scene: s, Piece: p)))
                .Where(r => !r.Piece.Variants.Contains(KeySceneDef.Today) || !string.IsNullOrEmpty(r.Piece.WhenTide)).ToArray();
            Assert.Greater(conditional.Length, 0);
            foreach (var row in conditional)
                Assert.IsFalse(StPetersLayerRefresh.Places(row.Scene, row.Piece), row.Piece.Id);
        }

        [Test]
        public void EveryFrozenPiece_HasOneOwner_AndIsNotPlacedTwice()
        {
            var scenes = StPetersLayerRefresh.LoadStPetersKeyScenes();
            var owned = scenes.SelectMany(s => s.Pieces.Select(p => (Scene: s, Piece: p)))
                .Where(r => !string.IsNullOrEmpty(r.Piece.Owner)).ToArray();
            Assert.Greater(owned.Length, 0);
            foreach (var row in owned)
            {
                Assert.AreEqual(1, scenes.Sum(s => s.Pieces.Count(p => p.Id == row.Piece.Id)), row.Piece.Id);
                Assert.IsFalse(StPetersLayerRefresh.Places(row.Scene, row.Piece), row.Piece.Id);
            }
        }

        [Test]
        public void EveryKitTheStepPlaces_IsOnMain()
        {
            var scenes = StPetersLayerRefresh.LoadStPetersKeyScenes();
            var placed = StPetersLayerRefresh.PlaceKeyScenes(scenes, new StPetersLayerRefresh.EditorKeySceneAssets());
            CollectionAssert.AreEquivalent(scenes.SelectMany(s => s.Pieces.Where(p => StPetersLayerRefresh.Places(s, p))).Select(p => p.Id),
                placed.Select(p => p.Id));
            foreach (var piece in placed.Where(p => !p.IsPerch))
                Assert.AreNotEqual(0, piece.Sprite.Sprite.FileId, piece.Id);
        }

        [Test]
        public void EveryPlacedBuilding_HasNoCommittedGrassInsideItsFootprint()
        {
            var scene = StPetersLayerRefresh.SceneYaml.Parse(File.ReadAllText(StPetersLayerRefresh.ScenePath));
            var sites = StPetersLayerRefresh.CommittedGrassSites(scene);
            var assets = new StPetersLayerRefresh.EditorKeySceneAssets();
            int checkedBuildings = 0;
            foreach (var ks in StPetersLayerRefresh.LoadStPetersKeyScenes())
            foreach (var p in ks.Pieces.Where(p => StPetersLayerRefresh.Places(ks, p) && p.Kind == KeySceneDef.KindBuilding))
            {
                Assert.IsTrue(assets.TryBuilding(p, out _, out Vector2 footprint), p.Id);
                Assert.AreEqual(0, StPetersLayerRefresh.GrassInsideBuilding(sites, p.At, footprint, p.Dir), p.Id);
                checkedBuildings++;
            }
            Assert.AreEqual(3, checkedBuildings, "the ice house and its two pieces wait for the grass refresh");
        }

        /// <summary>
        /// Ginny's cottage is placed by its plot's own code (its key-scene row has an owner), so the
        /// loop above never sees it. Its floor is read back from the scene's serialized interior, the
        /// same footprint the player walks inside. While he is inside, the room is drawn as a flat plan
        /// under every plant, so the ground kept clear is the floor grown by the wall's thickness, and
        /// behind the back wall one storey's drawn rise: a plant rooted there paints over the room.
        /// </summary>
        [Test]
        public void GinnysCottage_HasNoPlantOrGrassInsideItsRoom()
        {
            var scene = StPetersLayerRefresh.SceneYaml.Parse(File.ReadAllText(StPetersLayerRefresh.ScenePath));
            var root = scene.TransformOf(scene.RootNamed(StPetersGinnyPlot.RootName));
            var cottage = scene.ChildNamed(root, StPetersGinnyPlot.CottageKey);
            var interior = scene.ComponentsOf(cottage).SingleOrDefault(d => d.IsScript("HiddenHarbours.World.BuildingInterior"));
            Assert.IsNotNull(interior, "Ginny's cottage has no interior to keep clear");
            var floor = new InteriorFootprint(
                InteriorFixturePlacement.WorldOf(scene, scene.TransformOf(cottage)),
                StPetersLayerRefresh.ParseFloat(interior.Field("_widthMetres")),
                StPetersLayerRefresh.ParseFloat(interior.Field("_lengthMetres")),
                StPetersLayerRefresh.ParseInt(interior.Field("_facing")),
                StPetersLayerRefresh.ParseInt(interior.Field("_facings")),
                StPetersLayerRefresh.ParseFloat(interior.Field("_groundDepthScale")));
            Assert.That(Vector2.Distance(floor.Centre, StPetersGinnyPlot.CottagePos), Is.LessThan(0.01f));
            Assert.Greater(floor.WidthMetres * floor.LengthMetres, 1f, "an empty floor would pass vacuously");

            float wall = StPetersLayerRefresh.ParseFloat(interior.Field("_wallThicknessMetres"));
            float rise = StPetersLayerRefresh.ParseFloat(interior.Field("_upperLevelY"));
            Assert.Greater(wall, 0f);
            var corners = floor.Corners();
            var room = Rect.MinMaxRect(corners.Min(c => c.x) - wall, corners.Min(c => c.y) - wall,
                                       corners.Max(c => c.x) + wall, corners.Max(c => c.y) + Mathf.Max(wall, rise));

            var definition = UnityEditor.AssetDatabase.LoadAssetAtPath<PlantFieldDefinition>(StPetersGrassFieldTests.DefinitionPath);
            Assert.That(definition.HeightProjection, Is.Zero, "authored roots use their data coordinates");
            foreach (var site in definition.Authored)
                Assert.IsFalse(room.Contains(site.At), $"{site.Id} at {site.At} draws inside Ginny's cottage ({room})");

            var grass = StPetersLayerRefresh.CommittedGrassSites(scene);
            Assert.AreEqual(0, grass.Count(g => room.Contains(g)), "committed grass inside Ginny's cottage");
        }

        /// <summary>
        /// Every St Peters building with an interior, not only Ginny's: no plant site, scattered or
        /// authored, grass or flower, roots on ground the building keeps clear (its floor grown by
        /// <see cref="StPetersBuildingClearance.ClearanceMetres"/>, and one storey's rise behind it). The
        /// field draws a site by its root's y, so one rooted there paints over the wall or the room.
        /// Exceptions are named, with reasons, in <see cref="StPetersBuildingClearance.Exceptions"/>.
        /// </summary>
        [Test]
        public void EveryBuilding_HasNoPlantOnItsWallsOrInItsRoom()
        {
            var scene = StPetersLayerRefresh.SceneYaml.Parse(File.ReadAllText(StPetersLayerRefresh.ScenePath));
            var zones = StPetersBuildingClearance.ReadZones(scene);
            var held = zones.Where(z => !StPetersBuildingClearance.IsException(z.Path)).ToArray();
            Assert.GreaterOrEqual(held.Length, 9, "the village's houses, shops and school must all be read: " +
                string.Join(", ", zones.Select(z => z.Path)));

            var sites = StPetersLayerRefresh.CommittedPlantSites(scene);
            Assert.Greater(sites.Count, 1000, "an empty field would pass vacuously");
            var definition = UnityEditor.AssetDatabase.LoadAssetAtPath<PlantFieldDefinition>(StPetersGrassFieldTests.DefinitionPath);
            var failures = new System.Collections.Generic.List<string>();
            foreach (var zone in held)
            {
                int n = sites.Count(s => zone.Contains(s));
                if (n > 0) failures.Add($"{zone.Path}: {n} plant site(s) on its walls or in its room");
                foreach (var site in definition.Authored.Where(a => zone.Contains(a.At)))
                    failures.Add($"{zone.Path}: authored {site.Id} at {site.At}");
            }
            Assert.IsEmpty(failures, string.Join("\n", failures));
        }

        [Test]
        public void TheIceHouseAndItsTwoPieces_WaitTogether()
        {
            var scene = StPetersLayerRefresh.LoadStPetersKeyScenes().Single(s => s.Id == "keyscene.stp_east_end");
            Assert.AreEqual(22, scene.Pieces.Length, "the east end's 21 rows and the net shed's landing");
            CollectionAssert.AreEquivalent(new[] { "structure.stp_ice_house", "prop.stp_ice_wood", "prop.stp_ice_barrow" },
                scene.Pieces.Where(p => !StPetersLayerRefresh.Places(scene, p)).Select(p => p.Id));
        }

        [Test]
        public void TheLandingPerch_MatchesItsPilehead_AndCanneryMountsResolve()
        {
            var scenes = StPetersLayerRefresh.LoadStPetersKeyScenes();
            var landing = scenes.Single(s => s.Id == "keyscene.stp_landing");
            var perch = landing.Pieces.Single(p => p.Id == "prop.stp_landing_gull");
            var assets = new StPetersLayerRefresh.EditorKeySceneAssets();
            Assert.IsTrue(assets.TryMount(perch, out _, out _));
            Assert.AreEqual("northPilehead", perch.MountAnchor);
            // Host picture and height are checked independently: its pilehead is 0.37 m above the deck.
            float deck = StPetersLayerRefresh.KeySceneGroundAt(new Vector2(StPetersWharf.RootCellX + 0.5f, 0f));
            Assert.That(Mathf.Abs(perch.Z - (deck + 0.37f)), Is.LessThanOrEqualTo(0.05f));
            var cannery = scenes.Single(s => s.Id == "keyscene.stp_cannery");
            foreach (var p in cannery.Pieces.Where(p => p.StandsOn == KeySceneDef.StandsOnMount))
                Assert.IsTrue(assets.TryMount(p, out _, out _), p.Id + ": host resolution only; roof alignment requires plate acceptance");
        }

        [Test]
        public void APerchPoint_HasNoPerFrameCallback()
        {
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public;
            foreach (string name in new[] { "Update", "LateUpdate", "FixedUpdate" })
                Assert.IsNull(typeof(GullPerch).GetMethod(name, flags), name);
        }
    }
}
