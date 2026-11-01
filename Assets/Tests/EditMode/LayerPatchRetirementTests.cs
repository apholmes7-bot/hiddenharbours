using NUnit.Framework;
using System.Reflection;
using HiddenHarbours.App.Editor;
using static HiddenHarbours.App.Editor.StPetersLayerRefresh;

namespace HiddenHarbours.Tests.EditMode
{
    public class LayerPatchRetirementTests
    {
        const string RootName = "Retired";
        const long RootId = 10, TransformId = 11, RootsId = 99;
        const string Root = "--- !u!1 &10\nGameObject:\n  m_Component:\n  - component: {fileID: 11}\n  m_Name: Retired\n" +
            "--- !u!4 &11\nTransform:\n  m_GameObject: {fileID: 10}\n  m_Children: []\n  m_Father: {fileID: 0}\n";
        const string Other = "--- !u!1 &20\nGameObject:\n  m_Component:\n  - component: {fileID: 21}\n  m_Name: Kept\n" +
            "--- !u!4 &21\nTransform:\n  m_GameObject: {fileID: 20}\n  m_Children: []\n  m_Father: {fileID: 0}\n";
        const string Roots = "--- !u!1660057539 &99\nSceneRoots:\n  m_Roots:\n  - {fileID: 11}\n  - {fileID: 21}";
        const string RootsAfter = "--- !u!1660057539 &99\nSceneRoots:\n  m_Roots:\n  - {fileID: 21}";
        const string Before = "%YAML 1.1\n" + Root + Other + Roots + "\n";
        const string After = "%YAML 1.1\n" + Other + RootsAfter + "\n";

        static void Delete(LayerPatch patch, Doc doc, string name) =>
            typeof(LayerPatch).GetMethod("Delete", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(patch, new object[] { doc, name });
        static void Edit(LayerPatch patch, Doc doc, string after, string name) =>
            typeof(LayerPatch).GetMethod("Edit", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(patch, new object[] { doc, after, name });

        static LayerPatch Plan(SceneYaml scene)
        {
            var patch = LayerPatch.Retirement("retire", RootName, RootId);
            if (scene.Contains(RootId))
            {
                Delete(patch, scene.Require(RootId, "root"), "Retired/GameObject");
                Delete(patch, scene.Require(TransformId, "transform"), "Retired/Transform");
                Edit(patch, scene.Require(RootsId, "roots"), RootsAfter, "SceneRoots unlist Retired");
            }
            return patch.Seal(scene);
        }

        [Test]
        public void OrdinaryPatches_KeepTheirSceneRootsAndWholeRootGuards()
        {
            SceneYaml scene = SceneYaml.Parse(Before);
            var edit = new LayerPatch("ordinary-edit", RootName, RootId);
            Edit(edit, scene.Require(RootsId, "roots"), RootsAfter, "SceneRoots");
            StringAssert.Contains("is outside the 'Retired' root", Assert.Throws<Refusal>(() => edit.Seal(scene)).Message);
            var deletion = new LayerPatch("ordinary-delete", RootName, RootId);
            Delete(deletion, scene.Require(RootId, "root"), "Retired/GameObject");
            Delete(deletion, scene.Require(TransformId, "transform"), "Retired/Transform");
            StringAssert.Contains("the step's root", Assert.Throws<Refusal>(() => deletion.Seal(scene)).Message);
        }

        [Test]
        public void Retirement_DeletesTheWholeRoot_AndPreservesEveryOtherByte()
        {
            SceneYaml scene = SceneYaml.Parse(Before);
            LayerPatch patch = Plan(scene);
            Assert.AreEqual(After, patch.ApplyTo(Before));
            Assert.IsFalse(patch.Apply(scene).Contains(RootId));
            var addition = LayerPatch.Retirement("bad-add", RootName, RootId);
            typeof(LayerPatch).GetMethod("Add", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(addition, new object[] { "--- !u!1 &30\nGameObject:\n  m_Name: Added", "Added" });
            StringAssert.Contains("a retirement cannot add", Assert.Throws<Refusal>(() => addition.Seal(scene)).Message);
        }

        [Test]
        public void APartialRetirement_RefusesTheDocumentLeftStanding()
        {
            SceneYaml scene = SceneYaml.Parse(Before);
            var patch = LayerPatch.Retirement("partial", RootName, RootId);
            Delete(patch, scene.Require(RootId, "root"), "Retired/GameObject");
            Edit(patch, scene.Require(RootsId, "roots"), RootsAfter, "SceneRoots unlist Retired");
            StringAssert.Contains("the retirement of 'Retired' leaves &11 standing", Assert.Throws<Refusal>(() => patch.Seal(scene)).Message);
        }

        [Test]
        public void ARetirement_RefusesADeletionOutsideItsSubtree()
        {
            SceneYaml scene = SceneYaml.Parse(Before);
            var patch = LayerPatch.Retirement("outside", RootName, RootId);
            Delete(patch, scene.Require(RootId, "root"), "Retired/GameObject");
            Delete(patch, scene.Require(TransformId, "transform"), "Retired/Transform");
            Delete(patch, scene.Require(20, "kept"), "Kept/GameObject");
            Edit(patch, scene.Require(RootsId, "roots"), RootsAfter, "SceneRoots unlist Retired");
            StringAssert.Contains("is outside the 'Retired' root", Assert.Throws<Refusal>(() => patch.Seal(scene)).Message);
        }

        [Test]
        public void ARetirement_RefusesASurvivingReferenceToADeletedDocument()
        {
            string before = Before.Replace("  m_Name: Kept", "  m_Name: Kept\n  target: {fileID: 10}");
            SceneYaml scene = SceneYaml.Parse(before);
            StringAssert.Contains("&20 still points at the deleted &10", Assert.Throws<Refusal>(() => Plan(scene)).Message);
        }

        [Test]
        public void ARetirement_RefusesMoreThanItsOneSceneRootsRemoval()
        {
            SceneYaml scene = SceneYaml.Parse(Before);
            var patch = LayerPatch.Retirement("roots", RootName, RootId);
            Delete(patch, scene.Require(RootId, "root"), "Retired/GameObject");
            Delete(patch, scene.Require(TransformId, "transform"), "Retired/Transform");
            Edit(patch, scene.Require(RootsId, "roots"), RootsAfter.Replace("fileID: 21", "fileID: 22"), "SceneRoots changed another root");
            StringAssert.Contains("does more than unlist 'Retired'", Assert.Throws<Refusal>(() => patch.Seal(scene)).Message);
        }

        [Test]
        public void AnAbsentRoot_RefusesNonEmptyRetirement_AndAnEmptyReplanSeals()
        {
            SceneYaml absent = SceneYaml.Parse(After);
            var patch = LayerPatch.Retirement("absent", RootName, RootId);
            Delete(patch, SceneYaml.Parse(Before).Require(RootId, "root"), "Retired/GameObject");
            StringAssert.Contains("the scene holds no 'Retired' (&10) to retire", Assert.Throws<Refusal>(() => patch.Seal(absent)).Message);
            LayerPatch empty = Plan(absent);
            Assert.IsTrue(empty.IsEmpty);
            Assert.AreEqual(After, empty.ApplyTo(After));
        }

        [Test]
        public void ASealedRetirement_AppliedTwice_ChangesNoByteTheSecondTime()
        {
            LayerPatch patch = Plan(SceneYaml.Parse(Before));
            string once = patch.ApplyTo(Before);
            Assert.AreEqual(After, once);
            Assert.AreEqual(once, patch.ApplyTo(once));
        }
    }
}
