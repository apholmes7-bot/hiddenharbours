#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using HiddenHarbours.Art;
using HiddenHarbours.Art.Editor;
using HiddenHarbours.World;
using UnityEngine;

namespace HiddenHarbours.App.Editor
{
    public static partial class StPetersLayerRefresh
    {
        public sealed class VillageRoomProp
        {
            public string Name, Label;
            public Vector2 RoomMetres;
            public Vector2 LocalPosition;
            public SpriteRef Sprite;
            public Vector2[] Quad;
        }

        public sealed class VillageMove
        {
            public string Root, Key;
            public Vector2 Position;
            public SpriteRef Shell, Room;
            public InteriorFootprint Footprint;
            public VillageRoomProp[] Props = Array.Empty<VillageRoomProp>();
        }

        public static VillageMove[] ReadVillageMoves()
        {
            SpriteRef Sprite(Sprite sprite, string key)
            {
                if (sprite == null) throw new Refusal("Missing baked cell for moved building: " + key);
                return EditorShoreRockSprites.RefOf(sprite);
            }
            var rows = new List<VillageMove>();
            foreach (var site in StPetersVillage.Sites.Where(s => s.Key == "school" || s.Key == "sageCottage"))
            {
                var shell = VillageBuildingCatalog.Find(site.Key);
                var room = InteriorCatalog.FindRoom(site.Key);
                if (!shell.IsValid || !room.IsValid) throw new Refusal("Missing shell or room: " + site.Key);
                int facing = StPetersVillage.FacingFor(shell, site), inside = InteriorCatalog.InteriorFacingFor(facing);
                var door = InteriorCatalog.DoorModelMetres(room);
                var footprint = new InteriorFootprint(Vector2.zero, room.Entry.footprintWidthMetres, room.Entry.footprintLengthMetres,
                    inside, room.Entry.facings, SpriteLightMath.GroundDepthScale, door.y >= 0 ? 1 : -1, door.x);
                var props = new List<VillageRoomProp>();
                foreach (var furnishing in StPetersInteriors.FurnishingsFor(room.Entry.key))
                {
                    var prop = InteriorCatalog.FindProp(furnishing.PropKey);
                    if (!prop.IsValid) throw new Refusal("Missing room prop: " + furnishing.PropKey);
                    if (furnishing.Fixture != StPetersInteriors.InteriorFixture.None)
                        throw new Refusal("Moved village room has an unexpected interactive fixture.");
                    int cell = ((inside + furnishing.FacingOffset) % room.Entry.facings + room.Entry.facings) % room.Entry.facings;
                    Vector2 at = footprint.ModelToWorld(furnishing.RoomMetres);
                    props.Add(new VillageRoomProp
                    {
                        Name = string.Format(CultureInfo.InvariantCulture, "{0} ({1:0.#},{2:0.#})", prop.Entry.label, furnishing.RoomMetres.x, furnishing.RoomMetres.y),
                        Label = prop.Entry.label, RoomMetres = furnishing.RoomMetres,
                        LocalPosition = at, Sprite = Sprite(InteriorCatalog.LoadFacing(prop, cell), furnishing.PropKey),
                        Quad = BuildingInteriorStander.ToLocal(footprint.PropQuad(furnishing.RoomMetres,
                            prop.Entry.propFootprintWidth, prop.Entry.propFootprintDepth, furnishing.FacingOffset), at),
                    });
                }
                rows.Add(new VillageMove { Root = StPetersVillage.RootName, Key = site.Key, Position = site.Position,
                    Shell = Sprite(VillageBuildingCatalog.LoadFacing(shell, facing), site.Key),
                    Room = Sprite(InteriorCatalog.LoadFacing(room, inside), site.Key), Footprint = footprint, Props = props.ToArray() });
            }
            var store = StPetersShops.Sites.Single(s => s.Key == "generalStore");
            var shop = ShopCatalog.FindShell(store.Key);
            var level = ShopCatalog.FindLevel(store.Key, ShopKit.GroundLevel);
            if (!shop.IsValid || !level.IsValid) throw new Refusal("Missing store shell or room.");
            int outer = StPetersShops.FacingFor(shop, store), inner = ShopCatalog.InteriorFacingFor(outer);
            var entry = ShopCatalog.DoorModelMetres(level);
            rows.Add(new VillageMove { Root = StPetersShops.RootName, Key = store.Key, Position = store.Position,
                Shell = Sprite(ShopCatalog.LoadFacing(shop, outer), store.Key), Room = Sprite(ShopCatalog.LoadFacing(level, inner), store.Key),
                Footprint = new InteriorFootprint(Vector2.zero, level.FootprintMetres.x, level.FootprintMetres.y, inner,
                    ShopKit.Facings, SpriteLightMath.GroundDepthScale, entry.y >= 0 ? 1 : -1, entry.x) });
            return rows.ToArray();
        }

        static string VillagePolygonPoints(Doc old, Vector2[] quad)
        {
            var fresh = new Doc(PolygonColliderYaml(old.FileId, old.FieldRef("m_GameObject"), new[] { quad }));
            var lines = old.Lines.ToList();
            int start = lines.FindIndex(l => l == "  m_Points:");
            int finish = start + 1;
            while (finish < lines.Count && !lines[finish].StartsWith("  m_", StringComparison.Ordinal)) finish++;
            var wanted = fresh.Lines.SkipWhile(l => l != "  m_Points:").TakeWhile(l => l != "  m_UseDelaunayMesh: 1").ToArray();
            if (start < 0) throw new Refusal("Village wall has no polygon points.");
            lines.RemoveRange(start, finish - start); lines.InsertRange(start, wanted);
            return string.Join("\n", lines);
        }

        public static List<LayerPatch> MoveVillageBuildings(SceneYaml scene, IReadOnlyList<VillageMove> moves)
        {
            if (!moves.Select(m => m.Root + "/" + m.Key).OrderBy(s => s, StringComparer.Ordinal).SequenceEqual(new[]
                { "IslandShops/generalStore", "IslandVillage/sageCottage", "IslandVillage/school" }))
                throw new Refusal("Building moves must be the store, school and sage cottage only.");
            var result = new List<LayerPatch>();
            foreach (var group in moves.GroupBy(m => m.Root).OrderBy(g => g.Key, StringComparer.Ordinal))
            {
                var root = scene.RootNamed(group.Key);
                var patch = new LayerPatch("VillageMoves", group.Key, root.FileId);
                foreach (var move in group)
                {
                    var go = scene.ChildNamed(scene.TransformOf(root), move.Key);
                    var tr = scene.TransformOf(go);
                    var fp = move.Footprint;
                    if (fp.Centre != Vector2.zero) throw new Refusal("Moved room geometry must be local to its building.");
                    patch.Edit(tr, SetField(tr.Text, "m_LocalPosition", Vec3Yaml(move.Position.x, move.Position.y, 0)), move.Key + " site");
                    void SpriteAndSort(Doc obj, SpriteRef sprite, float worldY)
                    {
                        if (sprite.Sprite.FileId == 0 || string.IsNullOrEmpty(sprite.Sprite.Guid)) throw new Refusal("Moved building has no sprite asset.");
                        var sr = scene.ComponentsOf(obj).Single(d => d.ClassId == SpriteRendererClass);
                        string text = SetField(SetField(sr.Text, "m_Sprite", sprite.Sprite.Yaml), "m_Size", Vec2Yaml(sprite.Size));
                        var sort = scene.ComponentsOf(obj).SingleOrDefault(d => d.IsScript(typeof(YSortSprite).FullName));
                        if (sort != null) text = SetField(text, "m_SortingOrder", Int(SortingOrderFor(sort, worldY)));
                        patch.Edit(sr, text, move.Key + "/" + obj.Field("m_Name") + " sprite and sort");
                    }
                    SpriteAndSort(go, move.Shell, move.Position.y);
                    SpriteAndSort(scene.ChildNamed(tr, BuildingInteriorStander.RoomChildName), move.Room, move.Position.y);
                    var interior = scene.ComponentsOf(go).Single(d => d.IsScript(typeof(BuildingInterior).FullName));
                    float OldFloat(string field) => float.Parse(interior.Field(field), CultureInfo.InvariantCulture);
                    int OldInt(string field) => int.Parse(interior.Field(field), CultureInfo.InvariantCulture);
                    var oldFootprint = new InteriorFootprint(Vector2.zero, OldFloat("_widthMetres"), OldFloat("_lengthMetres"),
                        OldInt("_facing"), OldInt("_facings"), OldFloat("_groundDepthScale"),
                        OldInt("_doorOnPlusY") == 1 ? 1 : -1, OldFloat("_doorAcrossMetres"));
                    string updated = interior.Text;
                    foreach (var pair in new Dictionary<string, string>
                    {
                        ["_widthMetres"] = F(fp.WidthMetres), ["_lengthMetres"] = F(fp.LengthMetres),
                        ["_facing"] = Int(fp.Facing), ["_facings"] = Int(fp.Facings),
                        ["_groundDepthScale"] = F(fp.DepthScale), ["_doorOnPlusY"] = fp.DoorSign > 0 ? "1" : "0", ["_doorAcrossMetres"] = F(fp.DoorAcrossMetres),
                    }) updated = SetField(updated, pair.Key, pair.Value);
                    patch.Edit(interior, updated, move.Key + " room geometry");
                    var walls = scene.ChildNamed(tr, BuildingInteriorStander.WallsChildName);
                    var colliders = scene.ComponentsOf(walls).Where(d => d.ClassId == PolygonCollider2DClass).ToArray();
                    var quads = fp.WallQuads(BuildingInteriorStander.WallThicknessMetres, BuildingInteriorStander.DoorwayWidthMetres);
                    if (colliders.Length != quads.Length) throw new Refusal("Moved room has an unexpected wall count: " + move.Key);
                    for (int i = 0; i < quads.Length; i++) patch.Edit(colliders[i], VillagePolygonPoints(colliders[i], quads[i]), move.Key + " wall " + i);
                    if (move.Props.Length == 0) continue;
                    var furniture = scene.ChildNamed(tr, BuildingInteriorStander.PropsChildName);
                    var furnitureTr = scene.TransformOf(furniture);
                    var children = furnitureTr.FieldRefs("m_Children").Select(id => scene.Require(id, "room furnishing transform")).ToArray();
                    var attempts = new List<string>();
                    var matches = new List<(int Rotation, List<(VillageRoomProp Prop, Doc Tr)> Pairs, string Proof)>();
                    foreach (int rotation in new[] { 0, 90, 180, 270 })
                    {
                        var trialPairs = new List<(VillageRoomProp Prop, Doc Tr)>();
                        var used = new HashSet<long>();
                        var table = new List<string> { "Room furniture: " + move.Key + " | rotation " + rotation,
                            "child | fileID | label | old local | furnishing model | distance | new local | move" };
                        foreach (var prop in move.Props)
                        {
                            Vector2 model = prop.RoomMetres;
                            Vector2 turned = rotation == 0 ? model : rotation == 90 ? new Vector2(-model.y, model.x) :
                                rotation == 180 ? -model : new Vector2(model.y, -model.x);
                            Vector2 oldAt = oldFootprint.ModelToWorld(turned);
                            var hits = children.Where(child =>
                            {
                                var obj = scene.Require(child.FieldRef("m_GameObject"), "room furnishing");
                                return !string.IsNullOrEmpty(prop.Label) && obj.Field("m_Name").StartsWith(prop.Label + " (", StringComparison.Ordinal) &&
                                    Vector3.Distance(ParseVec3(child.Field("m_LocalPosition")), new Vector3(oldAt.x, oldAt.y, 0)) <= .01f;
                            }).ToArray();
                            if (hits.Length != 1)
                            {
                                table.Add(prop.Label + " | MATCHES " + hits.Length + " | expected " + Vec2Yaml(oldAt) +
                                    " | furnishing " + Vec2Yaml(prop.RoomMetres) + " | new " + Vec2Yaml(prop.LocalPosition));
                                continue;
                            }
                            var hit = hits[0];
                            var childGo = scene.Require(hit.FieldRef("m_GameObject"), "room furnishing");
                            var oldLocal = ParseVec3(hit.Field("m_LocalPosition"));
                            table.Add(childGo.Field("m_Name") + " | " + childGo.FileId + " / " + hit.FileId + " | " + prop.Label +
                                " | " + hit.Field("m_LocalPosition") + " | " + Vec2Yaml(prop.RoomMetres) + " | " +
                                F(Vector3.Distance(oldLocal, new Vector3(oldAt.x, oldAt.y, 0))) + " | " + Vec2Yaml(prop.LocalPosition) +
                                " | " + Vec2Yaml(prop.LocalPosition - new Vector2(oldLocal.x, oldLocal.y)));
                            if (used.Add(hit.FileId)) trialPairs.Add((prop, hit));
                        }
                        foreach (var child in children.Where(child => !used.Contains(child.FileId)))
                            table.Add("UNPAIRED | " + scene.Require(child.FieldRef("m_GameObject"), "room furnishing").Field("m_Name") +
                                " | " + child.FileId + " | " + child.Field("m_LocalPosition"));
                        string proof = string.Join("\n", table);
                        attempts.Add(proof);
                        if (trialPairs.Count == move.Props.Length && used.Count == children.Length)
                            matches.Add((rotation, trialPairs, proof));
                    }
                    if (matches.Count != 1)
                        throw new Refusal("Moved room furniture has " + matches.Count + " complete rotations.\n" + string.Join("\n\n", attempts));
                    Debug.Log(matches[0].Proof);
                    var pairs = matches[0].Pairs;
                    foreach (var pair in pairs)
                    {
                        var prop = pair.Prop;
                        var propTr = pair.Tr;
                        var obj = scene.Require(propTr.FieldRef("m_GameObject"), "room furnishing");
                        patch.Edit(propTr, SetField(propTr.Text, "m_LocalPosition", Vec3Yaml(prop.LocalPosition.x, prop.LocalPosition.y, 0)), move.Key + "/" + prop.Name + " position");
                        SpriteAndSort(obj, prop.Sprite, move.Position.y + prop.LocalPosition.y);
                        var collider = scene.ComponentsOf(obj).Single(d => d.ClassId == PolygonCollider2DClass);
                        patch.Edit(collider, VillagePolygonPoints(collider, prop.Quad), move.Key + "/" + prop.Name + " collider");
                    }
                }
                result.Add(patch.Seal(scene));
            }
            return result;
        }
    }
}
#endif
