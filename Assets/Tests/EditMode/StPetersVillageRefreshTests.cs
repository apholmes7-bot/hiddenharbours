using System;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using HiddenHarbours.Art;
using HiddenHarbours.Art.Editor;
using HiddenHarbours.App.Editor;
using HiddenHarbours.World;
using NUnit.Framework;
using UnityEngine;
using static HiddenHarbours.App.Editor.StPetersLayerRefresh;
using VillageStation = HiddenHarbours.App.Editor.StPetersLayerRefresh.VillageStation;

namespace HiddenHarbours.Tests.EditMode
{
    public class StPetersVillageRefreshTests
    {
        sealed class Assets : IVillageDressingAssets, IVillageLightAssets, IVillageBoundaryAssets
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
            public SpriteRef SpriteFor(YardDressing.PlannedPiece piece) => Post;
            public ScriptRef ScriptFor(Type type) => new ScriptRef(Sorter.Script, type.Assembly.GetName().Name + "::" + type.FullName);
        }

        static StPetersVillageDressing.Row Row(string group, string id, bool held = false) =>
            StPetersVillageDressing.Resolve(group, new VillagePiece { Id = id, Kit = "yardIso", Piece = "barrelPlanter",
                Position = new Vector2(3, 19) }, held);

        [Test]
        public void VillagePass_ComposesRootsOnce_KeepsCounterData_AndReplansToNothing()
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
            var kit = new YardKit.Contract
            {
                facings = 8, ppu = 32, cellFaceBearings = Enumerable.Range(0, 8).Select(i => i * 45f).ToArray(),
                cellRunBearings = Enumerable.Range(0, 8).Select(i => (i * 45f + 90f) % 360f).ToArray(),
                pieces = YardKit.Builds.Select(b => new YardKit.Entry { key = b.Key, role = b.Part.ToString(), facings = 8,
                    columns = 8, rows = 1, cellW = 96, cellH = 96, footprintWidthMetres = 2, footprintDepthMetres = .4f }).ToArray(),
            };
            var rows = new[] { "GeneralStore", "HarbourGable", "SageCottage", "School", "WhiteGable" }.Select((name, i) =>
            {
                float x = i * 20;
                var yard = new Yard(name, new Vector2(x + 5, 5), 1,
                    new GroundPolygon(new[] { new Vector2(x, 0), new Vector2(x + 10, 0), new Vector2(x + 10, 10), new Vector2(x, 10) }),
                    i == 0 ? YardFence.None : YardFence.PostRail, new[] { new Vector2(x + 5, 0) }, "test boundary");
                return new VillageYard { Yard = yard, Plan = YardDressing.PlanOne(yard, kit) };
            }).ToArray();
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

            var stands = StPetersVillageStations.Current;
            var data = new VillageInputs
            {
                Moves = moves, Homes = homes, Yards = rows, Lights = defs,
                Dressing = new[] { Row("commons.st_peters.square", "prop.test.square"), Row("yard.st_peters.school", "prop.test.school") },
                Stations = VillageStationRows(moves),
                Lanes = new System.Collections.Generic.Dictionary<string,Vector2>
                {
                    [StPetersRoutines.LaneSchool] = stands[StPetersRoutines.LaneSchool].Position,
                    [StPetersRoutines.LaneStore] = stands[StPetersRoutines.StationStoreCounter].Position,
                    [StPetersRoutines.LaneSageCottage] = stands[StPetersRoutines.StationSageDooryard].Position,
                },
                People = StPetersInhabitants.People.Where(p => p.Position == stands[StPetersRoutines.LaneSchool].Position ||
                    p.Position == stands[StPetersRoutines.StationStoreCounter].Position).ToDictionary(p => p.AssetName, p => p.Position),
                Counter = StPetersBuilder.GeneralStoreCounterPos,
                Machines = new[] { new Vector2(1,27), new Vector2(-1,27), new Vector2(-3,27) },
                MachineHeading = new Quaternion(0,0,1,0), DressingAssets = assets, LightAssets = assets, BoundaryAssets = assets,
            };
            var result = PlanVillage(scene,data);
            Assert.That(result.IsEmpty, Is.False);
            Assert.That(result.Net.Select(o => o.FileId).Distinct().Count(), Is.EqualTo(result.Net.Count));
            Assert.That(result.Net.Count(o => o.Before != null && new Doc(o.Before).ClassId == 1660057539), Is.EqualTo(1));
            Assert.That(result.Net.Where(o => o.Kind == OpKind.Delete).All(o => !string.IsNullOrWhiteSpace(o.Name)), Is.True);
            var originalRoots = scene.Docs.Single(d => d.ClassId == 1660057539).FieldRefs("m_Roots");
            var finalRoots = result.After.Docs.Single(d => d.ClassId == 1660057539).FieldRefs("m_Roots");
            Assert.That(finalRoots.Take(originalRoots.Count), Is.EqualTo(originalRoots));
            Assert.That(finalRoots.Count, Is.EqualTo(originalRoots.Count+2));
            Assert.That(PlanVillage(result.After,data).IsEmpty, Is.True);
            var counter = scene.RootNamed("GeneralStoreCounter");
            foreach (var component in scene.ComponentsOf(counter).Where(d => d.ClassId != 4 && d.ClassId != 212))
                Assert.That(result.After.Get(component.FileId).Text, Is.EqualTo(component.Text));
            foreach (var root in new[] { ("IslandVillage","redSaltbox"), ("IslandVillage","whiteFarmhouse"), ("IslandShops","postOffice") })
            {
                var child = scene.ChildNamed(scene.TransformOf(scene.RootNamed(root.Item1)),root.Item2);
                foreach (long id in scene.SubtreeOf(child.FileId)) Assert.That(result.After.Get(id).Text, Is.EqualTo(scene.Get(id).Text));
            }
            Assert.That(result.ToYaml(), Does.Contain("seller ids and wares book preserved"));
        }

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
        public void YardBoundaries_KeepHeldGroups_UseOneColliderPerSegment_AndReplanToNothing()
        {
            var scene = SceneYaml.Parse(File.ReadAllText("Assets/_Project/Scenes/StPeters.unity"));
            var assets = new Assets();
            var kit = new YardKit.Contract
            {
                facings = 8, ppu = 32, cellFaceBearings = Enumerable.Range(0, 8).Select(i => i * 45f).ToArray(),
                cellRunBearings = Enumerable.Range(0, 8).Select(i => (i * 45f + 90f) % 360f).ToArray(),
                pieces = YardKit.Builds.Select(b => new YardKit.Entry { key = b.Key, role = b.Part.ToString(), facings = 8,
                    columns = 8, rows = 1, cellW = 96, cellH = 96, footprintWidthMetres = 2, footprintDepthMetres = .4f }).ToArray(),
            };
            var rows = new[] { "GeneralStore", "HarbourGable", "SageCottage", "School", "WhiteGable" }.Select((name, i) =>
            {
                float x = i * 20;
                var yard = new Yard(name, new Vector2(x + 5, 5), 1,
                    new GroundPolygon(new[] { new Vector2(x, 0), new Vector2(x + 10, 0), new Vector2(x + 10, 10), new Vector2(x, 10) }),
                    i == 0 ? YardFence.None : YardFence.PostRail, new[] { new Vector2(x + 5, 0) }, "test boundary");
                return new VillageYard { Yard = yard, Plan = YardDressing.PlanOne(yard, kit) };
            }).ToArray();
            var patch = VillageYardBoundaries(scene, rows, assets);
            var after = patch.Apply(scene);
            var root = scene.RootNamed("Yards");
            var changed = new System.Collections.Generic.HashSet<long> { scene.TransformOf(root).FileId };
            foreach (string name in new[] { "School", "SageCottage" })
                changed.UnionWith(scene.SubtreeOf(scene.ChildNamed(scene.TransformOf(root), name).FileId));
            foreach (var doc in scene.Docs.Where(d => !changed.Contains(d.FileId)))
                Assert.That(after.Get(doc.FileId).Text, Is.EqualTo(doc.Text));
            foreach (var row in rows)
            {
                if (!row.Plan.Fenced)
                {
                    Assert.That(after.Docs.Any(d => d.ClassId == 1 && d.Field("m_Name") == row.Yard.Name), Is.False);
                    continue;
                }
                var group = after.ChildNamed(after.TransformOf(root), row.Yard.Name);
                var boundary = after.ChildNamed(after.TransformOf(group), "Boundary");
                var walls = after.ChildNamed(after.TransformOf(boundary), "Walls");
                var colliders = after.ComponentsOf(walls).Where(d => d.ClassId == 60).ToArray();
                Assert.That(colliders.Length, Is.EqualTo(row.Plan.Runs.Sum(r => r.Length - 1)));
                Assert.That(after.ComponentsOf(boundary).Count(d => d.IsScript(typeof(PropertyBoundary).FullName)), Is.EqualTo(1));
                foreach (var collider in colliders)
                    Assert.That(collider.Lines.Count(l => l == "    - - {x: " || l.StartsWith("    - - {x:", StringComparison.Ordinal)), Is.EqualTo(1));
            }
            Assert.That(VillageYardBoundaries(after, rows, assets).IsEmpty, Is.True);
            Assert.That(patch.ApplyTo(after.Render()), Is.EqualTo(after.Render()));
            rows[0].Yard = new Yard("WhiteFarmhouse", Vector2.zero, 1, rows[0].Yard.Polygon, YardFence.None, null, "test");
            Assert.Throws<Refusal>(() => VillageYardBoundaries(scene, rows, assets));
        }

        [Test]
        public void RoutinePatch_KeepsHeldStationsAndLinks_AndReplansToNothing()
        {
            var scene = SceneYaml.Parse(File.ReadAllText("Assets/_Project/Scenes/StPeters.unity"));
            var stations = new[] { new VillageStation(StPetersRoutines.StationStoreCounter, new Vector2(2.032163f, 27.905405f), 180),
                new VillageStation(StPetersRoutines.StationSageDooryard, new Vector2(-21.5f, 35.38f), 180) };
            var lanes = new System.Collections.Generic.Dictionary<string, Vector2> { [StPetersRoutines.LaneStore] = stations[0].Position,
                [StPetersRoutines.LaneSageCottage] = stations[1].Position };
            var patch = VillageRoutines(scene, stations, lanes);
            var after = patch.Apply(scene);
            Assert.That(patch.Ops.Count, Is.EqualTo(2));
            var changed = patch.Ops.Select(o => new Doc(o.Before).FileId).ToArray();
            foreach (var doc in scene.Docs.Where(d => !changed.Contains(d.FileId)))
                Assert.That(after.Get(doc.FileId).Text, Is.EqualTo(doc.Text));
            var oldStations = scene.Docs.Single(d => d.IsScript(typeof(RoutineStations).FullName));
            var newStations = after.Get(oldStations.FileId);
            Assert.That(newStations.Lines.Length, Is.EqualTo(oldStations.Lines.Length));
            string id = "";
            for (int i = 0; i < oldStations.Lines.Length; i++)
            {
                string line = oldStations.Lines[i];
                if (line.StartsWith("  - Id: ", StringComparison.Ordinal)) id = line.Substring(8);
                bool moves = stations.Any(s => s.Id == id) && (line.StartsWith("    Position:", StringComparison.Ordinal) || line.StartsWith("    StandHeadingDegrees:", StringComparison.Ordinal));
                if (!moves) Assert.That(newStations.Lines[i], Is.EqualTo(line));
            }
            var oldLanes = scene.Docs.Single(d => d.IsScript(typeof(RoutineLanes).FullName));
            var newLanes = after.Get(oldLanes.FileId);
            foreach (string field in new[] { "_nodeParents", "_viaStart", "_viaCount" })
                Assert.That(newLanes.Field(field), Is.EqualTo(oldLanes.Field(field)));
            Assert.That(VillageRoutines(after, stations, lanes).IsEmpty, Is.True);
            Assert.That(patch.ApplyTo(after.Render()), Is.EqualTo(after.Render()));
            Assert.Throws<Refusal>(() => VillageRoutines(scene, new[] { new VillageStation(StPetersRoutines.StationFarmhouseDooryard, Vector2.zero, 0) }, lanes));
        }

        [Test]
        public void PeopleCounterAndMachines_KeepComponentsAndIds_AndReplanToNothing()
        {
            var scene = SceneYaml.Parse(File.ReadAllText("Assets/_Project/Scenes/StPeters.unity"));
            var root = scene.RootNamed(StPetersInhabitants.RootName);
            var people = scene.TransformOf(root).FieldRefs("m_Children").Select(scene.Get).ToDictionary(
                t => scene.Get(t.FieldRef("m_GameObject")).Field("m_Name"), t => (Vector2)ParseVec3(t.Field("m_LocalPosition")) + Vector2.right);
            var counter = new Vector2(1.9385955f, 26.408327f);
            var places = StPetersMachines.Row.Select(r => (Vector2)ParseVec3(scene.TransformOf(scene.RootNamed(r.Name)).Field("m_LocalPosition")) + Vector2.right).ToArray();
            var patches = new[] { VillageInhabitants(scene, people), VillageCounter(scene, counter) }
                .Concat(VillageMachines(scene, places, new Quaternion(0, 0, 1, 0))).ToArray();
            var after = scene;
            foreach (var patch in patches) after = patch.Apply(after);
            Assert.That(after.Docs.Select(d => d.FileId), Is.EqualTo(scene.Docs.Select(d => d.FileId)));
            foreach (var doc in scene.Docs.Where(d => d.ClassId != 4 && d.ClassId != 212))
                Assert.That(after.Get(doc.FileId).Text, Is.EqualTo(doc.Text));
            Assert.That(VillageInhabitants(after, people).IsEmpty, Is.True);
            Assert.That(VillageCounter(after, counter).IsEmpty, Is.True);
            Assert.That(VillageMachines(after, places, new Quaternion(0, 0, 1, 0)).All(p => p.IsEmpty), Is.True);
            foreach (var patch in patches) Assert.That(patch.ApplyTo(after.Render()), Is.EqualTo(after.Render()));
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
