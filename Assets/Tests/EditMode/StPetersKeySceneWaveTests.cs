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

        [Test]
        public void TheIceHouseAndItsTwoPieces_WaitTogether()
        {
            var scene = StPetersLayerRefresh.LoadStPetersKeyScenes().Single(s => s.Id == "keyscene.stp_east_end");
            Assert.AreEqual(21, scene.Pieces.Length);
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
