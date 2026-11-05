using System.Linq;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Art;
using HiddenHarbours.World;
namespace HiddenHarbours.Tests.EditMode
{
    public class StPetersLandingPropsTests
    {
        static readonly string[] Ids={"prop.stp_landing_cart","prop.stp_landing_traps","prop.stp_landing_totes"};
        static readonly string[] Keys={"landingCart","landingTraps","landingTotes"};
        static KeySceneDef Scene(string id,int dir)
        {
            return KeySceneTestData.NewKeyScene("keyscene.stp_landing",KeySceneTestData.Piece(id,
                StPetersLayerRefresh.DecorKit,"dockCart",10,2,dir));
        }
        [Test] public void LandingIdsUseTheirOwnRecipeFrames()
        {
            for(int n=0;n<3;n++)for(int dir=0;dir<8;dir++)
            {
                var scene=Scene(Ids[n],dir);var assets=new FakeKeySceneAssets();
                assets.SetPieces.Add(Keys[n],FakeKeySceneAssets.SetPiece(Keys[n],Ids[n]));
                try
                {
                    var placed=StPetersLayerRefresh.PlaceKeyScenes(new[]{scene},assets).Single();
                    var expected=assets.SetPieces[Keys[n]].Frames[SetPieceDef.FrameForRigDir(dir)];
                    Assert.AreEqual(expected.Sprite.Guid,placed.Sprite.Sprite.Guid);
                    Assert.AreEqual(expected.Sprite.FileId,placed.Sprite.Sprite.FileId);
                    Assert.AreEqual(Ids[n],placed.Id);
                }
                finally{Object.DestroyImmediate(scene);}
            }
        }
        [Test] public void OtherDecorRetainsItsGenericSheet()
        {
            var scene=Scene("prop.test_other_cart",4);var assets=new FakeKeySceneAssets();
            try
            {
                var actual=StPetersLayerRefresh.PlaceKeyScenes(new[]{scene},assets).Single();
                var expected=FakeKeySceneAssets.SpriteOf(FakeKeySceneAssets.IsoKey(StPetersLayerRefresh.DecorKit, "dockCart", FakeKeySceneAssets.FacingOf(4)));
                Assert.AreEqual(expected.Sprite.Guid,actual.Sprite.Sprite.Guid);
                Assert.AreEqual($"cell {FakeKeySceneAssets.FacingOf(4)}",actual.Cell);
            }
            finally{Object.DestroyImmediate(scene);}
        }
        [TestCase("missing"),TestCase("wrong id"),TestCase("seven frames")]
        public void MissingOrMismatchedLandingRecipeRefuses(string fault)
        {
            var scene=Scene(Ids[0],4);var assets=new FakeKeySceneAssets();
            if(fault!="missing")
            {
                var p=FakeKeySceneAssets.SetPiece(Keys[0],fault=="wrong id"?"prop.test_other":Ids[0]);
                if(fault=="seven frames")p.Frames=p.Frames.Take(7).ToArray();
                assets.SetPieces.Add(Keys[0],p);
            }
            try{Assert.Throws<StPetersLayerRefresh.Refusal>(()=>StPetersLayerRefresh.PlaceKeyScenes(new[]{scene},assets));}
            finally{Object.DestroyImmediate(scene);}
        }
    }
}
