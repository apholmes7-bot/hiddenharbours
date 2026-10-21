using System;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using HiddenHarbours.Art;
using HiddenHarbours.App.Editor;
using HiddenHarbours.World;
using NUnit.Framework;
using UnityEngine;
using static HiddenHarbours.App.Editor.StPetersLayerRefresh;

namespace HiddenHarbours.Tests.EditMode
{
    public class StPetersVillageRefreshTests
    {
        sealed class Assets : IVillageDressingAssets, IVillageLightAssets
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
            public SpriteRef Post => new SpriteRef(new ObjRef(21300000, "44444444444444444444444444444444", 3), new Vector2(1, 4));
            public ScriptRef ScriptFor(Type type) => new ScriptRef(Sorter.Script, type.Assembly.GetName().Name + "::" + type.FullName);
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
            long yardTransform = scene.TransformOf(scene.RootNamed("Yards")).FileId;
            foreach (var doc in scene.Docs.Where(d => allowed.Contains(d.FileId) && d.FileId != yardTransform))
                Assert.That(result.Get(doc.FileId).Text, Is.EqualTo(doc.Text));
            Assert.That(result.Docs.Any(d => d.Field("m_Name") == "test.held"), Is.False);
            Assert.That(result.Docs.Any(d => d.Field("m_Name") == "tree.stp_vl_square_maple_01"), Is.False);
            Assert.That(VillageDressing(result, rows, assets).All(p => p.IsEmpty), Is.True);
            foreach (var patch in patches) Assert.That(patch.ApplyTo(result.Render()), Is.EqualTo(result.Render()));
        }

        [Test]
        public void LightsPatch_EqualsAllSixteenDefs_LeavesTwoUnlit_AndReplansToNothing()
        {
            var scene = SceneYaml.Parse(File.ReadAllText("Assets/_Project/Scenes/StPeters.unity"));
            var defs = Directory.GetFiles("Assets/_Project/Data/Regions/StPetersVillage", "light.*.asset")
                .OrderBy(s => s, StringComparer.Ordinal).Select(path =>
                {
                    var doc = SceneYaml.Parse(File.ReadAllText(path)).Docs.Single();
                    var def = (LightPostDef)FormatterServices.GetUninitializedObject(typeof(LightPostDef));
                    def.Id = doc.Field("Id").Trim('"');
                    def.KitPiece = doc.Field("KitPiece").Trim('"');
                    def.Preset = doc.Field("Preset").Trim('"');
                    var at = doc.Field("Position").Replace("}", ", z: 0}");
                    def.Position = ParseVec3(at);
                    def.Reach = ParseFloat(doc.Field("Reach"));
                    def.CastsShadow = doc.Field("CastsShadow") == "1";
                    return def;
                }).ToArray();
            Assert.That(defs.Length, Is.EqualTo(16));
            var assets = new Assets();
            var patch = VillageLights(scene, defs, assets);
            var after = patch.Apply(scene);
            foreach (var doc in scene.Docs.Where(d => d.ClassId != 1660057539))
                Assert.That(after.Get(doc.FileId).Text, Is.EqualTo(doc.Text));
            var root = after.RootNamed(StPetersVillageLights.RootName);
            var owned = after.SubtreeOf(root.FileId);
            Assert.That(after.Docs.Count(d => owned.Contains(d.FileId) && d.IsScript(typeof(SceneLight).FullName)), Is.EqualTo(14));
            Assert.That(after.Docs.Count(d => owned.Contains(d.FileId) && d.IsScript(typeof(SpriteShadow).FullName)), Is.EqualTo(5));
            foreach (var def in defs)
            {
                var go = after.Docs.Single(d => d.ClassId == 1 && d.Field("m_Name") == def.Id);
                var tr = after.TransformOf(go);
                Assert.That(ParseVec3(tr.Field("m_LocalPosition")), Is.EqualTo(new Vector3(def.Position.x, def.Position.y, 0)));
                var components = go.FieldRefs("m_Component").Select(after.Get).ToArray();
                var light = components.SingleOrDefault(d => d.IsScript(typeof(SceneLight).FullName));
                var preset = components.SingleOrDefault(d => d.IsScript(typeof(PreconfiguredLight).FullName));
                if (def.Id == "light.st_peters.fire_pit_square" || def.Id == "light.st_peters.battery_shed_light_sage_cottage")
                {
                    Assert.That(light, Is.Null); Assert.That(preset, Is.Null);
                    continue;
                }
                Assert.That(ParseFloat(light.Field("_reachMetres")), Is.EqualTo(def.Reach));
                Assert.That(ParseFloat(preset.Field("_reachOverrideMetres")), Is.EqualTo(def.Reach));
                Assert.That(ParseInt(preset.Field("_preset")), Is.EqualTo((int)Enum.Parse(typeof(LightPresets.Kind), def.Preset)));
                Assert.That(light.Field("_castsShadows"), Is.EqualTo(def.CastsShadow ? "1" : "0"));
            }
            Assert.That(VillageLights(after, defs, assets).IsEmpty, Is.True);
            Assert.That(patch.ApplyTo(after.Render()), Is.EqualTo(after.Render()));
            defs.Single(d => d.Id == "light.st_peters.doorstep_lantern_school").Reach = float.NaN;
            Assert.Throws<Refusal>(() => VillageLights(scene, defs, assets));
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
