#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Art;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using static HiddenHarbours.App.Editor.StPetersLayerRefresh;
using Refresh = HiddenHarbours.App.Editor.NineMileCreekLayerRefresh;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.EditMode
{
    public class NineMileCreekLayerRefreshTests
    {
        string Text => File.ReadAllText(Refresh.ScenePath);
        static LayerPatch Plan(string text, Refresh.IFaceAssets assets = null) =>
            Refresh.WithPlanGround(ground => Refresh.QuayFace(SceneYaml.Parse(text), ground,
                assets ?? new Refresh.EditorFaceAssets()));
        static Doc Face(SceneYaml scene) => scene.ChildNamed(
            scene.TransformOf(scene.RootNamed(NineMileCreekDressing.RootName)), NineMileCreekDressing.FaceRootName);

        [Test]
        public void ASecondRunChangesNothing()
        {
            string once = Plan(Text).ApplyTo(Text);
            LayerPatch second = Plan(once);
            Assert.IsTrue(second.IsEmpty, second.Summary());
            Assert.AreEqual(once, second.ApplyTo(once));
        }

        [Test]
        public void EveryDocumentOutsideQuayFaceIsByteIdentical()
        {
            string before = Text;
            SceneYaml source = SceneYaml.Parse(before);
            var owned = source.SubtreeOf(Face(source).FileId);
            SceneYaml after = SceneYaml.Parse(Plan(before).ApplyTo(before));
            foreach (Doc doc in source.Docs.Where(d => !owned.Contains(d.FileId)))
                Assert.AreEqual(doc.Text, after.Require(doc.FileId, "protected document").Text, "&" + doc.FileId);
            var trees = source.SubtreeOf(source.RootNamed("CreekTrees").FileId);
            Assert.AreEqual(298, source.Docs.Count(d => trees.Contains(d.FileId) &&
                (d.Field("m_EditorClassIdentifier") ?? "").EndsWith(".TreeTrunkAnchor", StringComparison.Ordinal)));
            Assert.AreEqual(562, source.Docs.Count(d =>
                (d.Field("m_EditorClassIdentifier") ?? "").EndsWith(".SpriteShadow", StringComparison.Ordinal)));
        }

        [Test]
        public void TheFaceMatchesTheBuilderIncludingItsWaterline()
        {
            string before = Text;
            Refresh.WithPlanGround(ground =>
            {
                SceneYaml after = SceneYaml.Parse(Refresh.QuayFace(SceneYaml.Parse(before), ground,
                    new Refresh.EditorFaceAssets()).ApplyTo(before));
                Doc face = Face(after);
                Doc faceTr = after.TransformOf(face);
                var root = new GameObject("N1 builder parity");
                try
                {
                    int count = NineMileCreekDressing.ReplaceFace(root, ground);
                    Assert.AreEqual(count, faceTr.FieldRefs("m_Children").Count);
                    foreach (SpriteRenderer sr in root.GetComponentsInChildren<SpriteRenderer>(true))
                    {
                        Doc go = after.ChildNamed(faceTr, sr.gameObject.name);
                        var components = after.ComponentsOf(go);
                        Doc renderer = components.Single(d => d.ClassId == 212);
                        Assert.AreEqual(sr.transform.position, ParseVec3(after.TransformOf(go).Field("m_LocalPosition")), go.Field("m_Name"));
                        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(sr.sprite, out string guid, out long id);
                        ObjRef sprite = ObjRef.Parse(renderer.Field("m_Sprite"));
                        Assert.AreEqual(guid, sprite.Guid);
                        Assert.AreEqual(id, sprite.FileId);
                        Assert.AreEqual(sr.sortingOrder, ParseInt(renderer.Field("m_SortingOrder")));
                        var tide = sr.GetComponent<TidalFaceWaterline>();
                        Doc water = components.SingleOrDefault(d =>
                            (d.Field("m_EditorClassIdentifier") ?? "").EndsWith(".TidalFaceWaterline", StringComparison.Ordinal));
                        Assert.AreEqual(tide != null, water != null, go.Field("m_Name"));
                        if (water != null)
                        {
                            Assert.AreEqual(tide.LipWorldY, ParseFloat(water.Field("_lipWorldY")));
                            Assert.AreEqual(tide.LipElevation, ParseFloat(water.Field("_lipElevation")));
                            Assert.AreEqual("1", water.Field("_configured"));
                        }
                        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(sr.sharedMaterial, out string matGuid, out long matId);
                        StringAssert.Contains(new ObjRef(matId, matGuid, 2).Yaml, renderer.Text);
                        Assert.IsFalse(components.Any(d => (d.Field("m_EditorClassIdentifier") ?? "").EndsWith(".YSortSprite", StringComparison.Ordinal)));
                    }
                    return true;
                }
                finally { Object.DestroyImmediate(root); }
            });
        }

        [Test]
        public void AnUnownedCourseComponentStopsThePatch()
        {
            string before = Text;
            SceneYaml scene = SceneYaml.Parse(before);
            var owned = scene.SubtreeOf(Face(scene).FileId);
            Doc script = scene.Docs.First(d => owned.Contains(d.FileId) && d.ClassId == 114);
            string changed = before.Replace(script.Text, script.Text.Replace("--- !u!114", "--- !u!61"));
            Assert.Throws<Refusal>(() => Plan(changed));
        }

        [Test]
        public void ATransformedParentStopsThePatch()
        {
            string before = Text;
            SceneYaml scene = SceneYaml.Parse(before);
            Doc tr = scene.TransformOf(Face(scene));
            string changed = before.Replace(tr.Text, tr.Text.Replace("m_LocalPosition: {x: 0, y: 0, z: 0}",
                "m_LocalPosition: {x: 1, y: 0, z: 0}"));
            Assert.Throws<Refusal>(() => Plan(changed));
        }

        sealed class MissingSprite : Refresh.IFaceAssets
        {
            readonly Refresh.EditorFaceAssets source = new Refresh.EditorFaceAssets();
            public ObjRef DefaultMaterial => source.DefaultMaterial;
            public ObjRef TideMaterial => source.TideMaterial;
            public ObjRef TideScript => source.TideScript;
            public SpriteRef SpriteFor(NineMileCreekDressing.FacePiece piece) => default;
        }

        [Test]
        public void AMissingSpriteStopsThePatchBeforeAnyWrite() =>
            Assert.Throws<Refusal>(() => Plan(Text, new MissingSprite()));
    }
}
#endif
