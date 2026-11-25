#if UNITY_EDITOR
using HiddenHarbours.App;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Core;
using HiddenHarbours.World;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HiddenHarbours.Tests.EditMode
{
    public class ArrivalSeaTests
    {
        [TestCase(false, false, true)]
        [TestCase(true, false, false)]
        [TestCase(false, true, false)]
        [TestCase(true, true, false)]
        public void OnlyTheExistingFreshGameGateTakesTheSeaHop(bool anchor, bool arrived, bool hop)
        {
            Assert.AreEqual(hop, ArrivalOpening.ShouldHopToSea(anchor, arrived, true));
            Assert.IsFalse(ArrivalOpening.ShouldHopToSea(anchor, arrived, false));
        }

        [Test]
        public void ContinuationKeepsEveryRemainingFairwayMark()
        {
            var authored = StPetersArrivalOpening.Route();
            Vector2 entry = StPetersBuilder.FromEastWaterArrivalPos;
            var continuation = ArrivalOpening.ContinuationRoute(authored, entry);
            Assert.AreEqual(entry, continuation[0]);
            Assert.AreNotSame(authored, continuation);
            for (int i = 1; i < authored.Length; i++) Assert.AreEqual(authored[i], continuation[i]);
            Assert.AreEqual(StPetersBuilder.EntranceLandfall, authored[0]);
        }

        [Test]
        public void AuthoredSeaDataNamesTheOpeningAndTheWayHome()
        {
            var def = AssetDatabase.LoadAssetAtPath<ArrivalSeaDef>(ArrivalSeaAuthoring.DefPath);
            Assert.IsNotNull(def);
            Assert.AreEqual(EastWaterPlan.RegionId, def.SeaRegion.Id);
            Assert.AreEqual(EastWaterPlan.OpeningArrivalKey, def.OpeningArrivalKey);
            Assert.AreEqual(StPetersBuilder.FromEastWaterArrivalKey, def.HomeArrivalKey);
            Assert.AreEqual((Vector2)EastWaterPlan.OpeningLiePos, def.LiePosition);
            Assert.AreEqual(EastWaterPlan.OpeningExitTarget, def.ExitTarget);
        }

        [Test]
        public void DemoSceneListContainsEveryRegionTheOpeningLoads()
        {
            var def = AssetDatabase.LoadAssetAtPath<ArrivalSeaDef>(ArrivalSeaAuthoring.DefPath);
            Assert.IsNotNull(def);
            Assert.IsNotNull(def.SeaRegion);
            Assert.IsNotNull(def.HomeRegion);
            var builder = System.Type.GetType("HiddenHarbours.Tools.Editor.PlayerBuild, HiddenHarbours.Tools.Editor", true);
            var scenes = (string[])builder.GetMethod("ScenePaths").Invoke(null, new object[] { true });
            Assert.AreEqual(def.HomeRegion.SceneName, System.IO.Path.GetFileNameWithoutExtension(scenes[0]));
            var before = DemoBuild.Override;
            var availability = RegionBuildAvailability.Override;
            try
            {
                DemoBuild.Override = true;
                RegionBuildAvailability.Override = (_, __) => true;
                foreach (var region in new[] { def.SeaRegion, def.HomeRegion })
                {
                    CollectionAssert.Contains(scenes, "Assets/_Project/Scenes/" + region.SceneName + ".unity");
                    Assert.IsTrue(RegionBuildAvailability.IsAvailable(region.Id, region.SceneName), region.SceneName);
                }
            }
            finally
            {
                DemoBuild.Override = before;
                RegionBuildAvailability.Override = availability;
            }
        }

        [Test]
        public void UnityReadsTheTwoAddedSceneReferencesWithoutSavingTheScene()
        {
            var before = System.IO.File.ReadAllBytes("Assets/_Project/Scenes/StPeters.unity");
            var scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/StPeters.unity", OpenSceneMode.Additive);
            try
            {
                ArrivalOpening opening = null;
                foreach (var root in scene.GetRootGameObjects())
                    if (root.TryGetComponent(out ArrivalOpening candidate)) opening = candidate;
                Assert.IsNotNull(opening);
                var serialized = new SerializedObject(opening);
                Assert.AreSame(AssetDatabase.LoadAssetAtPath<ArrivalSeaDef>(ArrivalSeaAuthoring.DefPath),
                    serialized.FindProperty("_seaOpening").objectReferenceValue);
                var loader = serialized.FindProperty("_loader").objectReferenceValue as RegionSceneLoader;
                Assert.IsNotNull(loader);
                Assert.AreEqual(174273176UL, GlobalObjectId.GetGlobalObjectIdSlow(loader).targetObjectId);
                Assert.AreEqual(scene, loader.gameObject.scene);
            }
            finally { EditorSceneManager.CloseScene(scene, true); }
            CollectionAssert.AreEqual(before, System.IO.File.ReadAllBytes("Assets/_Project/Scenes/StPeters.unity"));
        }
    }
}
#endif
