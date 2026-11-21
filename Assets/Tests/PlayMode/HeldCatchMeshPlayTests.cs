using System.Collections;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using HiddenHarbours.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// <b>A fish in his hand is drawn as its mesh in the real scene</b> (fish art pass 2, section 3): the
    /// real player at Rice Point is handed a cod, and the mesh draws on his hand while the carried sprite
    /// stands down; a tomcod (the coast species kit's, its own kind) is its own mesh too; a green crab
    /// (no held-mesh pattern for a crustacean yet) keeps its sprite; the switch off hands the cod back to
    /// the sprite; and the fish he holds up at the landing is the mesh too.
    /// </summary>
    public sealed class HeldCatchMeshPlayTests : RicePointSceneFixture
    {
        [UnityTest]
        public IEnumerator ACodAndATomcodInHisHand_AreTheirMeshes_ACrabKeepsItsSprite_AndTheLandingIsTheMesh()
        {
            yield return LoadDirect();
            SetHour(12);
            StageStrand();
            for (int i = 0; i < 30; i++) yield return null;

            var hands = Walk.GetComponent<CarryHands>();
            var rod = Walk.GetComponent<RodFightPresenter>();
            Assert.IsNotNull(hands, "the player carries no CarryHands");
            GameConfig config = GameServices.Config;
            Assert.IsTrue(config.MeshCatchInHand, "the shipped switch must be on");
            CatchItemLibrary art = hands.EffectiveCatchArt;
            Assert.IsNotNull(art, "the hands have no catch art");

            // A cod: the mesh, at its rung's scale, and no sprite.
            const float codKg = 5.59f;
            Assert.IsTrue(hands.TryPutInHand(new CatchItem("fish.atlantic_cod", "Atlantic Cod",
                                                           FishCategory.InshoreGroundfish, codKg, 10, 0.1f)));
            for (int i = 0; i < 6; i++) yield return null;
            var cod = hands.Carried as CarriableCatch;
            Assert.IsNotNull(cod, "the cod is not carried as a catch");
            Assert.IsTrue(hands.CatchMesh != null && hands.CatchMesh.Drawing,
                          $"the cod is not drawn as its mesh ({hands.CatchMesh?.LastRefusal})");
            Assert.AreEqual("cod", hands.CatchMesh.Def.Variant);
            Assert.IsNotNull(art.HeldMeshFor("fish.atlantic_cod", cod.ArtKind, out float scale));
            Assert.AreEqual(scale, hands.CatchMesh.Scale, 1e-5f, "the mesh is not at the cod's rung scale");
            Assert.IsFalse(cod.SpriteVisible, "the mesh draws, yet the carried sprite still shows");

            // The switch off: the sprite again.
            config.MeshCatchInHand = false;
            try
            {
                for (int i = 0; i < 3; i++) yield return null;
                Assert.IsFalse(hands.CatchMesh.Drawing, "switched off, the mesh still draws");
                Assert.AreEqual(HeldCatchMesh.Refusal.SwitchOff, hands.CatchMesh.LastRefusal);
                Assert.IsTrue(cod.SpriteVisible, "switched off, the sprite does not come back");
            }
            finally { config.MeshCatchInHand = true; }
            Assert.IsTrue(hands.TryTakeFromHand(out _));
            for (int i = 0; i < 3; i++) yield return null;
            Assert.IsFalse(hands.CatchMesh.Drawing, "an empty hand still draws the fish");

            // A tomcod is its own kind and its own mesh, never the cod's.
            Assert.IsTrue(hands.TryPutInHand(new CatchItem("fish.atlantic_tomcod", "Atlantic Tomcod",
                                                           FishCategory.InshoreGroundfish, 0.2f, 10, 0.1f)));
            for (int i = 0; i < 6; i++) yield return null;
            var tomcod = hands.Carried as CarriableCatch;
            Assert.IsNotNull(tomcod);
            Assert.IsTrue(hands.CatchMesh.Drawing, $"the tomcod is not drawn as its mesh ({hands.CatchMesh.LastRefusal})");
            Assert.AreEqual("tomcod", hands.CatchMesh.Def.Variant);
            Assert.IsFalse(tomcod.SpriteVisible, "the tomcod's mesh draws, yet its sprite still shows");
            Assert.IsTrue(hands.TryTakeFromHand(out _));
            for (int i = 0; i < 3; i++) yield return null;

            // A green crab: no held-mesh pattern for a crustacean yet, so it keeps its sprite.
            Assert.IsTrue(hands.TryPutInHand(new CatchItem("fish.green_crab", "Green Crab",
                                                           FishCategory.Shellfish, 0.1f, 10, 0.1f)));
            for (int i = 0; i < 6; i++) yield return null;
            var crab = hands.Carried as CarriableCatch;
            Assert.IsNotNull(crab);
            Assert.IsFalse(hands.CatchMesh.Drawing, "a crab has no held mesh and must not be drawn as one");
            Assert.AreEqual(HeldCatchMesh.Refusal.NoMesh, hands.CatchMesh.LastRefusal);
            Assert.IsTrue(crab.SpriteVisible, "the crab's sprite is hidden");
            Assert.IsTrue(hands.TryTakeFromHand(out _));
            for (int i = 0; i < 3; i++) yield return null;

            // The landing: the fish held up is the mesh.
            Assert.IsNotNull(rod, "the player has no fight presenter");
            var landed = new FishingState(FishingPhase.Landed, 0f, 0.3f, "fish.atlantic_cod", "Atlantic Cod",
                                          FishCategory.InshoreGroundfish, codKg, 0.4f, false,
                                          0f, 0f, 0f, 0f, 0f, -4f, 0f);
            EventBus.Publish(new FishingStateChanged(landed));
            try
            {
                for (int i = 0; i < 9; i++) yield return null;
                Assert.IsTrue(rod.LandMesh != null && rod.LandMesh.Drawing,
                              $"the landed cod is not drawn as its mesh ({rod.LandMesh?.LastRefusal})");
                Assert.AreEqual("cod", rod.LandMesh.Def.Variant);
            }
            finally { EventBus.Publish(new FishingStateChanged(FishingState.Idle)); }
            for (int i = 0; i < 6; i++) yield return null;
            Assert.IsFalse(rod.LandMesh.Drawing, "the landing is over, yet the fish still draws");
        }
    }
}
