using System.IO;
using HiddenHarbours.App.Editor;
using NUnit.Framework;
using UnityEngine;
using static HiddenHarbours.App.Editor.StPetersLayerRefresh;

namespace HiddenHarbours.Tests.EditMode
{
    public sealed class StPetersVillageSceneTests
    {
        [Test]
        public void CommittedBuildings_StandAtTheirActiveOrHeldSites()
        {
            var scene = SceneYaml.Parse(File.ReadAllText(ScenePath));
            foreach (var site in StPetersVillage.Sites)
            {
                var go = scene.ChildNamed(scene.TransformOf(scene.RootNamed(StPetersVillage.RootName)), site.Key);
                Assert.That(ParseVec3(scene.TransformOf(go).Field("m_LocalPosition")),
                    Is.EqualTo(new Vector3(site.Position.x,site.Position.y,0)),site.Key);
            }
            foreach (var site in StPetersShops.Sites)
            {
                var go = scene.ChildNamed(scene.TransformOf(scene.RootNamed(StPetersShops.RootName)), site.Key);
                Assert.That(ParseVec3(scene.TransformOf(go).Field("m_LocalPosition")),
                    Is.EqualTo(new Vector3(site.Position.x,site.Position.y,0)),site.Key);
            }
        }

        [Test]
        public void CommittedVillage_EqualsTheImportedPlan()
        {
            var scene = SceneYaml.Parse(File.ReadAllText(ScenePath));
            var plan = PlanVillage(scene,ReadVillageInputs());
            Assert.That(plan.IsEmpty,Is.True,plan.ToYaml());
        }
    }
}
