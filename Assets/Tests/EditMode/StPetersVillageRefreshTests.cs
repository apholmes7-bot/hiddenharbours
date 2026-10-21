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
        public void NewHomesPatch_PreservesEveryExistingBuilding_AndKeepsRoomsEmpty()
        {
            var scene = SceneYaml.Parse(File.ReadAllText("Assets/_Project/Scenes/StPeters.unity"));
            var assets = new Assets();
            var homes = StPetersVillage.Sites.Where(s => s.Key == "creamSaltbox" || s.Key == "harbourGable" || s.Key == "whiteGable")
                .Select(s => new VillageHome
                {
                    Key = s.Key, Position = s.Position, Shell = assets.Post, Room = assets.Post, UpperRoom = assets.Post,
                    ShellMaterial = assets.Material, RoomMaterial = assets.Material, Mask = assets.Post.Sprite,
                    Normal = assets.Post.Sprite, Emit = assets.Post.Sprite,
                    Width = 7, Length = 8, DoorSign = 1, DoorAcross = .2f, StoreyHeight = 3.1f, RoomFacing = 4, Facings = 8,
                }).ToArray();
            var patch = NewVillageHomes(scene, homes, assets);
            var after = patch.Apply(scene);
            long rootTr = scene.TransformOf(scene.RootNamed(StPetersVillage.RootName)).FileId;
            foreach (var doc in scene.Docs.Where(d => d.FileId != rootTr))
                Assert.That(after.Get(doc.FileId).Text, Is.EqualTo(doc.Text));
            foreach (var home in homes)
            {
                var go = after.Docs.Single(d => d.ClassId == 1 && d.Field("m_Name") == home.Key);
                var tr = after.TransformOf(go);
                Assert.That(ParseVec3(tr.Field("m_LocalPosition")), Is.EqualTo(new Vector3(home.Position.x, home.Position.y, 0)));
                var interior = after.ComponentsOf(go).Single(d => d.IsScript(typeof(BuildingInterior).FullName));
                Assert.That(interior.FieldRef("_occupant"), Is.Zero);
                Assert.That(ParseFloat(interior.Field("_upperLevelY")), Is.EqualTo(3.1f * .76604444f).Within(.000001f));
                foreach (string part in new[] { "Furniture", "FurnitureUpper" })
                {
                    var child = after.ChildNamed(tr, part);
                    Assert.That(after.TransformOf(child).FieldRefs("m_Children"), Is.Empty);
                    Assert.That(child.Field("m_IsActive"), Is.EqualTo("0"));
                }
                var inside = after.SubtreeOf(go.FileId);
                Assert.That(after.Docs.Count(d => inside.Contains(d.FileId) && d.ClassId == 60), Is.EqualTo(6));
                Assert.That(after.Docs.Count(d => inside.Contains(d.FileId) && d.ClassId == 212), Is.EqualTo(3));
                Assert.That(inside.Select(after.Get).Any(d => d.IsScript(typeof(PreconfiguredLight).FullName)), Is.False);
            }
            Assert.That(NewVillageHomes(after, homes, assets).IsEmpty, Is.True);
            Assert.That(patch.ApplyTo(after.Render()), Is.EqualTo(after.Render()));
            homes[0].StoreyHeight = 0;
            Assert.Throws<Refusal>(() => NewVillageHomes(scene, homes, assets));
        }

        [Test]
        public void BuildingMoves_KeepIdsAndLinks_ReframeFurniture_AndReplanToNothing()
        {
            var scene = SceneYaml.Parse(File.ReadAllText("Assets/_Project/Scenes/StPeters.unity"));
            var assets = new Assets();
            var moves = new[]
            {
                new VillageMove { Root = "IslandVillage", Key = "school", Position = StPetersVillage.Sites.Single(s => s.Key == "school").Position },
                new VillageMove { Root = "IslandVillage", Key = "sageCottage", Position = StPetersVillage.Sites.Single(s => s.Key == "sageCottage").Position },
                new VillageMove { Root = "IslandShops", Key = "generalStore", Position = StPetersShops.Sites.Single(s => s.Key == "generalStore").Position },
            };
            var owned = new System.Collections.Generic.HashSet<long>();
            foreach (var move in moves)
            {
                var go = scene.ChildNamed(scene.TransformOf(scene.RootNamed(move.Root)), move.Key);
                owned.UnionWith(scene.SubtreeOf(go.FileId));
                move.Shell = assets.Post; move.Room = assets.Post;
                move.Footprint = new InteriorFootprint(Vector2.zero, 7, 8, 4, 8, SpriteLightMath.GroundDepthScale, 1, .25f);
                if (move.Key == "generalStore") continue;
                var furniture = scene.ChildNamed(scene.TransformOf(go), "Furniture");
                move.Props = scene.TransformOf(furniture).FieldRefs("m_Children").Select((id, i) =>
                {
                    var prop = scene.Get(scene.Get(id).FieldRef("m_GameObject"));
                    return new VillageRoomProp { Name = prop.Field("m_Name"), LocalPosition = new Vector2(i * .1f, -1),
                        Sprite = assets.Post, Quad = new[] { new Vector2(-.2f, -.3f), new Vector2(.2f, -.3f),
                            new Vector2(.2f, .3f), new Vector2(-.2f, .3f) } };
                }).ToArray();
            }
            var patches = MoveVillageBuildings(scene, moves);
            var after = scene;
            foreach (var patch in patches) after = patch.Apply(after);
            Assert.That(after.Docs.Select(d => d.FileId), Is.EqualTo(scene.Docs.Select(d => d.FileId)));
            foreach (var doc in scene.Docs.Where(d => !owned.Contains(d.FileId)))
                Assert.That(after.Get(doc.FileId).Text, Is.EqualTo(doc.Text));
            foreach (var move in moves)
            {
                var go = scene.ChildNamed(scene.TransformOf(scene.RootNamed(move.Root)), move.Key);
                Assert.That(ParseVec3(after.TransformOf(go).Field("m_LocalPosition")), Is.EqualTo(new Vector3(move.Position.x, move.Position.y, 0)));
                var before = scene.ComponentsOf(go).Single(d => d.IsScript(typeof(BuildingInterior).FullName));
                var updated = after.Get(before.FileId);
                foreach (string field in new[] { "_shell", "_room", "_props", "_occupant", "_upperRoom", "_upperProps", "_upperWalls" })
                    Assert.That(updated.Field(field), Is.EqualTo(before.Field(field)));
                Assert.That(updated.Field("_facing"), Is.EqualTo("4"));
                Assert.That(updated.Field("_doorOnPlusY"), Is.EqualTo("1"));
                var walls = scene.ChildNamed(scene.TransformOf(go), "Walls");
                foreach (var wall in scene.ComponentsOf(walls).Where(d => d.ClassId == 60))
                    Assert.That(after.Get(wall.FileId).Text, Is.Not.EqualTo(wall.Text));
                if (move.Key == "generalStore") continue;
                var furniture = scene.ChildNamed(scene.TransformOf(go), "Furniture");
                foreach (var prop in move.Props)
                {
                    var obj = after.ChildNamed(after.TransformOf(furniture), prop.Name);
                    Assert.That(ParseVec3(after.TransformOf(obj).Field("m_LocalPosition")), Is.EqualTo(new Vector3(prop.LocalPosition.x, prop.LocalPosition.y, 0)));
                }
            }
            Assert.That(MoveVillageBuildings(after, moves).All(p => p.IsEmpty), Is.True);
            foreach (var patch in patches) Assert.That(patch.ApplyTo(after.Render()), Is.EqualTo(after.Render()));
            moves[0].Key = "redSaltbox";
            Assert.Throws<Refusal>(() => MoveVillageBuildings(scene, moves));
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
