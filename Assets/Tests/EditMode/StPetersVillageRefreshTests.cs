using System;
using System.IO;
using System.Linq;
using HiddenHarbours.App.Editor;
using HiddenHarbours.World;
using NUnit.Framework;
using UnityEngine;
using static HiddenHarbours.App.Editor.StPetersLayerRefresh;

namespace HiddenHarbours.Tests.EditMode
{
    public class StPetersVillageRefreshTests
    {
        sealed class Assets : IVillageDressingAssets
        {
            public int Reads;
            public SpriteRef SpriteFor(StPetersVillageDressing.Row row)
            {
                Assert.That(StPetersVillageDressing.Places(row), Is.True);
                Reads++;
                return new SpriteRef(new ObjRef(21300000, "11111111111111111111111111111111", 3), new Vector2(2, 2));
            }
            public ObjRef Material => new ObjRef(2100000, "22222222222222222222222222222222", 2);
            public ScriptRef Sorter => new ScriptRef(new ObjRef(11500000, "33333333333333333333333333333333", 3),
                "HiddenHarbours.Art::HiddenHarbours.Art.YSortSprite");
        }

        static StPetersVillageDressing.Row Row(string group, string id, bool held = false) =>
            StPetersVillageDressing.Resolve(group, new VillagePiece { Id = id, Kit = "yardIso", Piece = "barrelPlanter",
                Position = new Vector2(3, 19) }, held);

        [Test]
        public void DressingPatch_HoldsOtherRootsAndHeldRows_AndReplansToNothing()
        {
            var scene = SceneYaml.Parse(File.ReadAllText("Assets/_Project/Scenes/StPeters.unity"));
            var rows = new[] { Row("yard.st_peters.school", "test.planter"),
                Row("commons.st_peters.square", "test.bench"),
                Row("yard.st_peters.cream_saltbox", "test.held", true),
                Row("commons.st_peters.square", "tree.stp_vl_square_maple_01") };
            var assets = new Assets();
            var patches = VillageDressing(scene, rows, assets);
            Assert.That(assets.Reads, Is.EqualTo(2));
            Assert.That(patches.Select(p => p.Root), Is.EqualTo(new[] { "Yards", StPetersVillageDressing.CommonsRoot }));
            Assert.That(patches.SelectMany(p => p.Ops).Count(o => o.Before != null && new Doc(o.Before).ClassId == 1660057539), Is.EqualTo(1));
            var allowed = scene.SubtreeOf(scene.RootNamed("Yards").FileId);
            var result = scene;
            foreach (var patch in patches) result = patch.Apply(result);
            foreach (var doc in scene.Docs.Where(d => !allowed.Contains(d.FileId) && d.ClassId != 1660057539))
                Assert.That(result.Get(doc.FileId).Text, Is.EqualTo(doc.Text));
            foreach (var doc in scene.Docs.Where(d => allowed.Contains(d.FileId) && d.ClassId != 4))
                Assert.That(result.Get(doc.FileId).Text, Is.EqualTo(doc.Text));
            Assert.That(result.Docs.Any(d => d.Field("m_Name") == "test.held"), Is.False);
            Assert.That(result.Docs.Any(d => d.Field("m_Name") == "tree.stp_vl_square_maple_01"), Is.False);
            Assert.That(VillageDressing(result, rows, assets).All(p => p.IsEmpty), Is.True);
            foreach (var patch in patches) Assert.That(patch.ApplyTo(result.Render()), Is.EqualTo(result.Render()));
        }

        [Test]
        public void DressingPatch_NamesDeletions_AndRefusesHeldActivation()
        {
            var scene = SceneYaml.Parse(File.ReadAllText("Assets/_Project/Scenes/StPeters.unity"));
            var assets = new Assets();
            var old = Row("commons.st_peters.square", "test.old");
            foreach (var patch in VillageDressing(scene, new[] { old }, assets)) scene = patch.Apply(scene);
            var next = Row("commons.st_peters.square", "test.new");
            var patches = VillageDressing(scene, new[] { next }, assets);
            var deleted = patches.SelectMany(p => p.Deletions).ToArray();
            Assert.That(deleted.Length, Is.EqualTo(4));
            Assert.That(deleted.All(o => o.Name.Contains("test.old")), Is.True);
            var held = Row("yard.st_peters.post_office", "test.held", true);
            held.Status = "place";
            Assert.Throws<Refusal>(() => VillageDressing(scene, new[] { held }, assets));
            Assert.Throws<Refusal>(() => VillageDressing(scene, new[] { next, next }, assets));
        }
    }
}
