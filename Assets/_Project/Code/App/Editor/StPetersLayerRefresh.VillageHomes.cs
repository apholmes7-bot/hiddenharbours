#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using HiddenHarbours.Art;
using HiddenHarbours.Art.Editor;
using HiddenHarbours.Core;
using HiddenHarbours.World;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.App.Editor
{
    public static partial class StPetersLayerRefresh
    {
        public sealed class VillageHome
        {
            public string Key;
            public Vector2 Position;
            public SpriteRef Shell, Room, UpperRoom;
            public ObjRef ShellMaterial, RoomMaterial, Mask, Normal, Emit;
            public float Width, Length, DoorSign, DoorAcross, StoreyHeight;
            public int RoomFacing, Facings;
        }

        public static VillageHome[] ReadNewVillageHomes()
        {
            var roomAssets = new EditorKeySceneAssets();
            ObjRef Asset(UnityEngine.Object value)
            {
                if (value == null || !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(value, out string guid, out long id))
                    throw new Refusal("A village home needs a baked and imported asset.");
                return new ObjRef(id, guid, value is Material ? 2 : 3);
            }
            return StPetersVillage.Sites.Where(s => StPetersInteriors.VillageUpperPlan(s.Key) != null).Select(site =>
            {
                var shell = VillageBuildingCatalog.Find(site.Key);
                var room = InteriorCatalog.FindRoom(site.Key);
                var upper = InteriorCatalog.FindRoom(site.Key + "Upper");
                if (!shell.IsValid || !room.IsValid || !upper.IsValid)
                    throw new Refusal("Village home needs its shell and both rooms: " + site.Key);
                int facing = StPetersVillage.FacingFor(shell, site);
                int roomFacing = InteriorCatalog.InteriorFacingFor(facing);
                var door = InteriorCatalog.DoorModelMetres(room);
                SpriteRef Sprite(Sprite sprite)
                {
                    if (sprite == null) throw new Refusal("Village home has an unsliced sheet: " + site.Key);
                    return EditorShoreRockSprites.RefOf(sprite);
                }
                return new VillageHome
                {
                    Key = site.Key, Position = site.Position, Shell = Sprite(VillageBuildingCatalog.LoadFacing(shell, facing)),
                    Room = Sprite(InteriorCatalog.LoadFacing(room, roomFacing)), UpperRoom = Sprite(InteriorCatalog.LoadFacing(upper, roomFacing)),
                    ShellMaterial = Asset(VillageBuildingCatalog.LoadLitMaterial()), RoomMaterial = roomAssets.SpriteMaterial,
                    Mask = Asset(AssetDatabase.LoadAssetAtPath<Texture2D>(VillageBuildingKit.SheetPath(site.Key, VillageBuildingKit.Channel.Mask))),
                    Normal = Asset(AssetDatabase.LoadAssetAtPath<Texture2D>(VillageBuildingKit.SheetPath(site.Key, VillageBuildingKit.Channel.Normal))),
                    Emit = Asset(AssetDatabase.LoadAssetAtPath<Texture2D>(VillageBuildingKit.SheetPath(site.Key, VillageBuildingKit.Channel.Emit))),
                    Width = room.Entry.footprintWidthMetres, Length = room.Entry.footprintLengthMetres,
                    DoorSign = door.y >= 0 ? 1 : -1, DoorAcross = door.x, StoreyHeight = room.Entry.storeyHeightMetres,
                    RoomFacing = roomFacing, Facings = room.Entry.facings,
                };
            }).ToArray();
        }

        public static LayerPatch NewVillageHomes(SceneYaml scene, IReadOnlyList<VillageHome> homes, IVillageLightAssets scripts)
        {
            var expected = new[] { "creamSaltbox", "harbourGable", "whiteGable" };
            if (!homes.Select(h => h.Key).OrderBy(s => s, StringComparer.Ordinal).SequenceEqual(expected))
                throw new Refusal("The new-home patch needs exactly the three ruled homes.");
            var root = scene.RootNamed(StPetersVillage.RootName);
            var rootTr = scene.TransformOf(root);
            var owned = new HashSet<long>();
            var keep = rootTr.FieldRefs("m_Children");
            foreach (var home in homes)
            {
                var old = scene.Docs.Where(d => d.ClassId == GameObjectClass && d.Field("m_Name") == home.Key).ToArray();
                if (old.Length > 1) throw new Refusal("Duplicate village home: " + home.Key);
                if (old.Length == 0) continue;
                var tr = scene.TransformOf(old[0]);
                if (tr.FieldRef("m_Father") != rootTr.FileId) throw new Refusal("Village home is outside IslandVillage: " + home.Key);
                owned.UnionWith(scene.SubtreeOf(old[0].FileId));
                keep.Remove(tr.FileId);
            }
            var ids = new IdAllocator(scene, owned);
            var want = new List<Wanted>();
            foreach (var home in homes.OrderBy(h => h.Key, StringComparer.Ordinal))
            {
                if (!(home.Width > 0 && home.Length > 0) || home.Facings <= 0 ||
                    home.RoomFacing < 0 || home.RoomFacing >= home.Facings ||
                    InteriorLevelLayout.DrawsOverGroundFloor(InteriorLevelLayout.UpperLevelY(home.StoreyHeight)))
                    throw new Refusal("Invalid village room geometry: " + home.Key);
                foreach (var asset in new[] { home.Shell.Sprite, home.Room.Sprite, home.UpperRoom.Sprite,
                    home.Mask, home.Normal, home.Emit, home.ShellMaterial, home.RoomMaterial })
                    if (asset.FileId == 0 || string.IsNullOrEmpty(asset.Guid)) throw new Refusal("Incomplete village home art: " + home.Key);
                long Next(string part, string kind) => ids.Next("VillageHomes/" + home.Key + "/" + part + "/" + kind);
                long go = Next("", "GameObject"), tr = Next("", "Transform"), sr = Next("", "SpriteRenderer");
                long sort = Next("", "YSortSprite"), binder = Next("", "SpriteLightBinder"), interior = Next("", "BuildingInterior");
                var childTransforms = new List<long>();
                var partGos = new Dictionary<string, long>();
                var partTrs = new Dictionary<string, long>();
                var partSprites = new Dictionary<string, long>();
                float rise = InteriorLevelLayout.UpperLevelY(home.StoreyHeight);
                var footprint = new InteriorFootprint(Vector2.zero, home.Width, home.Length, home.RoomFacing,
                    home.Facings, SpriteLightMath.GroundDepthScale, home.DoorSign, home.DoorAcross);
                var upstairs = new InteriorFootprint(new Vector2(0, rise), home.Width, home.Length, home.RoomFacing,
                    home.Facings, SpriteLightMath.GroundDepthScale, home.DoorSign, home.DoorAcross);
                foreach (string part in new[] { "Interior", "Walls", "Furniture", "InteriorUpper", "FurnitureUpper", "WallsUpper" })
                {
                    long pgo = Next(part, "GameObject"), ptr = Next(part, "Transform");
                    partGos.Add(part, pgo); partTrs.Add(part, ptr); childTransforms.Add(ptr);
                    var components = new List<long> { ptr };
                    Vector2 at = part == "InteriorUpper" ? new Vector2(0, rise) : Vector2.zero;
                    if (part == "Interior" || part == "InteriorUpper")
                    {
                        long psr = Next(part, "SpriteRenderer"); components.Add(psr); partSprites.Add(part, psr);
                        int order = part == "Interior" ? InteriorCatalog.RoomSortingOrder : InteriorLevelLayout.UpperRoomSortingOrder(
                            new InteriorFootprint(home.Position + new Vector2(0, rise), home.Width, home.Length, home.RoomFacing,
                                home.Facings, SpriteLightMath.GroundDepthScale, home.DoorSign, home.DoorAcross));
                        string sprite = SpriteRendererYaml(psr, pgo, home.RoomMaterial,
                            part == "Interior" ? home.Room : home.UpperRoom, order);
                        want.Add(Want(SetField(sprite, "m_Enabled", "0"), home.Key + "/" + part, "SpriteRenderer"));
                    }
                    if (part == "Walls" || part == "WallsUpper")
                    {
                        var quads = part == "Walls" ? footprint.WallQuads(BuildingInteriorStander.WallThicknessMetres, BuildingInteriorStander.DoorwayWidthMetres)
                            : new[] { upstairs.DoorwayPlugQuad(BuildingInteriorStander.WallThicknessMetres, BuildingInteriorStander.DoorwayWidthMetres) };
                        for (int i = 0; i < quads.Length; i++)
                        {
                            long wall = Next(part, "PolygonCollider2D/" + i); components.Add(wall);
                            want.Add(Want(PolygonColliderYaml(wall, pgo, new[] { quads[i] }), home.Key + "/" + part, "PolygonCollider2D/" + i));
                        }
                    }
                    string obj = GameObjectYaml(pgo, part, components);
                    if (part == "Furniture" || part == "FurnitureUpper" || part == "WallsUpper") obj = SetField(obj, "m_IsActive", "0");
                    want.Add(Want(obj, home.Key + "/" + part, "GameObject"));
                    want.Add(Want(TransformYaml(ptr, pgo, at, new List<long>(), tr), home.Key + "/" + part, "Transform"));
                }
                want.Add(Want(GameObjectYaml(go, home.Key, new[] { tr, sr, sort, binder, interior }), home.Key, "GameObject"));
                want.Add(Want(TransformYaml(tr, go, home.Position, childTransforms, rootTr.FileId), home.Key, "Transform"));
                int shellOrder = YSortSprite.OrderFor(home.Position.y, SortingBands.DecorBase, SortingBands.OrdersPerMetre, SortingBands.DecorFloor, SortingBands.DecorCeiling);
                want.Add(Want(SpriteRendererYaml(sr, go, home.ShellMaterial, home.Shell, shellOrder), home.Key, "SpriteRenderer"));
                want.Add(Want(MonoBehaviourYaml(sort, go, scripts.ScriptFor(typeof(YSortSprite)), new[]
                {
                    "  _dynamic: 0", "  _baseOrder: " + F(SortingBands.DecorBase), "  _orderPerUnit: " + F(SortingBands.OrdersPerMetre),
                    "  _minOrder: " + Int(SortingBands.DecorFloor), "  _maxOrder: " + Int(SortingBands.DecorCeiling), "  _pivotYOffset: 0",
                }), home.Key, "YSortSprite"));
                want.Add(Want(MonoBehaviourYaml(binder, go, scripts.ScriptFor(typeof(SpriteLightBinder)), new[]
                {
                    "  _lightSheet: " + home.Mask.Yaml, "  _normalSheet: " + home.Normal.Yaml,
                    "  _rimGateSheet: {fileID: 0}", "  _rimGateChannel: 0", "  _emitSheet: " + home.Emit.Yaml,
                }), home.Key, "SpriteLightBinder"));
                string Link(long id) => "{fileID: " + Id(id) + "}";
                want.Add(Want(MonoBehaviourYaml(interior, go, scripts.ScriptFor(typeof(BuildingInterior)), new[]
                {
                    "  _shell: " + Link(sr), "  _room: " + Link(partSprites["Interior"]), "  _props: " + Link(partTrs["Furniture"]),
                    "  _upperRoom: " + Link(partSprites["InteriorUpper"]), "  _upperProps: " + Link(partTrs["FurnitureUpper"]),
                    "  _upperWalls: " + Link(partTrs["WallsUpper"]), "  _upperLevelY: " + F(rise), "  _occupant: {fileID: 0}",
                    "  _widthMetres: " + F(home.Width), "  _lengthMetres: " + F(home.Length), "  _facing: " + Int(home.RoomFacing),
                    "  _facings: " + Int(home.Facings), "  _groundDepthScale: " + F(SpriteLightMath.GroundDepthScale),
                    "  _wallThicknessMetres: " + F(BuildingInteriorStander.WallThicknessMetres), "  _hysteresisMetres: 0.15",
                    "  _doorwayWidthMetres: " + F(BuildingInteriorStander.DoorwayWidthMetres), "  _doorOnPlusY: " + (home.DoorSign > 0 ? "1" : "0"),
                    "  _doorAcrossMetres: " + F(home.DoorAcross),
                }), home.Key, "BuildingInterior"));
                keep.Add(tr);
            }
            var patch = new LayerPatch("VillageNewHomes", StPetersVillage.RootName, root.FileId);
            patch.Edit(rootTr, SetRefList(rootTr.Text, "m_Children", keep), "IslandVillage new homes");
            var wanted = new HashSet<long>(want.Select(w => w.FileId));
            foreach (var doc in scene.Docs.Where(d => owned.Contains(d.FileId) && !wanted.Contains(d.FileId))) patch.Delete(doc, NameOfExisting(scene, doc));
            foreach (var item in want)
            {
                var doc = scene.Get(item.FileId);
                if (doc == null) patch.Add(item.Text, item.Name);
                else patch.Edit(doc, item.Text, item.Name);
            }
            return patch.Seal(scene);
        }
    }
}
#endif
