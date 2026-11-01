#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using HiddenHarbours.Art;
using HiddenHarbours.Art.Editor;
using HiddenHarbours.Core;
using HiddenHarbours.Tools.RigBaking;
using HiddenHarbours.World;
using UnityEditor;
using UnityEngine;
using static HiddenHarbours.App.Editor.StPetersLayerRefresh;

namespace HiddenHarbours.App.Editor
{
    public static partial class NineMileCreekWharfLandSide
    {
        public const string RootName = "NmcWharfLandSide";
        public const string OfficeVendorName = "NmcWharfOfficeVendor";
        public const string MarketCounterName = "NmcWharfMarketCounter";
        public const string BuyerTruckRowId = "vehicle.nmc_buyer_truck";
        public const string RecordPath = "Assets/_Project/Data/KeyScenes/KeyScene_nmc_wharf.owners.json";
        public const string KeyScenePath = "Assets/_Project/Data/KeyScenes/KeyScene_nmc_wharf.asset";
        [Serializable] sealed class OwnedRow { public string id, owner, waits; }
        [Serializable] sealed class Ownership { public OwnedRow[] rows; }

        static KeySceneDef KeyScene() => AssetDatabase.LoadAssetAtPath<KeySceneDef>(KeyScenePath)
            ?? throw new Refusal("The wharf key scene is unavailable.");
        public static KeyScenePiece[] PlacementRows()
        {
            var owned = JsonUtility.FromJson<Ownership>(File.ReadAllText(RecordPath)).rows;
            var ids = new HashSet<string>(owned.Where(r => r.owner == "H6" && string.IsNullOrEmpty(r.waits) &&
                r.id != "harbour.yard" && r.id != "boat.nmc_damaged_dory").Select(r => r.id));
            KeyScenePiece[] rows = KeyScene().Pieces.Where(p => ids.Contains(p.Id)).ToArray();
            if (rows.Length != ids.Count) throw new Refusal("The H6 placement subset names an absent row.");
            return rows;
        }
        static SetPieceDef Definition(string key) => AssetDatabase.LoadAssetAtPath<SetPieceDef>(
            "Assets/_Project/Data/SetPieces/SetPiece_" + key + ".asset") ?? throw new Refusal("Missing wharf Def: " + key);
        public static Vector3 AnchorWorld(string key, string anchor)
        {
            KeyScenePiece row = KeyScene().Pieces.Single(p => p.Kit == "nmcSetPieces" && p.Piece == key);
            SetPieceDef def = Definition(key);
            Vector3 local;
            if (anchor == "vendor" && def.HasVendor) local = def.Vendor.At;
            else if (!def.TryGetAnchor(anchor, out local)) throw new Refusal(key + " has no " + anchor + " anchor.");
            Vector2 at = row.At + SetPieceDef.ToWorld(new Vector2(local.x, local.y), row.Dir);
            return new Vector3(at.x, at.y, local.z);
        }
        public static Vector3 OfficeVendor => AnchorWorld("officeTrailer", "vendor");
        public static Vector3 OfficeCounter => AnchorWorld("officeTrailer", "counter");
        public static Vector3 MarketCounter => AnchorWorld("fishMarket", "counter");
        public static Vector3 BuyerTruck
        {
            get { Vector2 p = KeyScene().Pieces.Single(r => r.Id == BuyerTruckRowId).At; return new Vector3(p.x, p.y, 0f); }
        }
        public static Vector2 LicenceSpeaker => OfficeCounter;
        public static Vector2 ClamSpeaker => (Vector2)MarketCounter + new Vector2(0.7f, -0.7f);

        static string GuidAt(string path)
        {
            Match match = Regex.Match(File.ReadAllText(path + ".meta"), @"(?m)^guid: ([0-9a-f]{32})$");
            if (!match.Success) throw new Refusal("Missing asset GUID: " + path);
            return match.Groups[1].Value;
        }
        static ObjRef Asset(string path) => new ObjRef(11400000, GuidAt(path), 2);
        static ScriptRef Script(string path, string assembly, string name) =>
            new ScriptRef(new ObjRef(11500000, GuidAt(path), 3), assembly + "::" + name);
        static SpriteRef SpriteAsset(Sprite sprite)
        {
            if (sprite == null || !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(sprite, out string guid, out long id))
                throw new Refusal("A wharf placement has no imported sprite.");
            return new SpriteRef(new ObjRef(id, guid, 3), sprite.rect.size / sprite.pixelsPerUnit);
        }
        static long Allocate(IdAllocator ids, string root, string key) => ids.Next("nmc-h6/" + root + "/" + key);
        static Doc ExistingRoot(SceneYaml scene, string name) => scene.Docs.SingleOrDefault(d =>
            d.ClassId == 1 && !d.Stripped && d.Field("m_Name") == name && scene.TransformOf(d).FieldRef("m_Father") == 0);

        static LayerPatch WriteRoot(SceneYaml scene, string name, long rootId, List<(string name, string text)> wanted)
        {
            Doc root = ExistingRoot(scene, name);
            if (root != null && root.FileId != rootId) throw new Refusal(PlacementStep + ": a root has an unexpected identity: " + name);
            var mine = root == null ? new HashSet<long>() : scene.SubtreeOf(rootId);
            var wantIds = new HashSet<long>(wanted.Select(w => new Doc(w.text).FileId));
            var patch = new LayerPatch(PlacementStep, name, rootId);
            foreach (long id in mine.Where(id => !wantIds.Contains(id)).OrderBy(id => id))
                patch.Delete(scene.Require(id, name), name + "/retired &" + id);
            foreach (var w in wanted)
            {
                var doc = new Doc(w.text);
                Doc old = scene.Get(doc.FileId);
                if (old == null) patch.Add(w.text, w.name);
                else
                {
                    if (!mine.Contains(doc.FileId)) throw new Refusal(PlacementStep + ": an id is taken outside " + name);
                    patch.Edit(old, w.text, w.name);
                }
            }
            if (root == null)
            {
                Doc roots = SceneRootsOf(scene);
                Doc go = wanted.Select(w => new Doc(w.text)).Single(d => d.FileId == rootId);
                Doc tr = wanted.Select(w => new Doc(w.text)).Single(d => d.ClassId == 4 && d.FieldRef("m_GameObject") == go.FileId);
                patch.Edit(roots, WithRootListed(roots.Text, tr.FileId), "SceneRoots/list " + name);
            }
            return patch.Seal(scene);
        }

        public static LayerPatch PlaceRows(SceneYaml scene)
        {
            Doc current = ExistingRoot(scene, RootName);
            var ids = new IdAllocator(scene, current == null ? new HashSet<long>() : scene.SubtreeOf(current.FileId));
            long go = Allocate(ids, RootName, "go"), tr = Allocate(ids, RootName, "tr");
            var wanted = new List<(string name, string text)>();
            var children = new List<long>();
            var material = new EditorKeySceneAssets().SpriteMaterial;
            var buildings = NmcWharfBuildingBakeMenu.Load();
            var ground = NineMileCreekGround.Load();
            foreach (KeyScenePiece row in PlacementRows())
            {
                // The existing drivable Dually occupies this row; PlaceBuyerTruck reads the same bay.
                if (row.Id == BuyerTruckRowId) continue;
                if (ground.ElevationAt(row.At) <= NineMileCreekBuilder.SpringHighWater)
                    throw new Refusal(row.Id + ": H6 support is submerged at spring high.");
                long pieceGo = Allocate(ids, RootName, row.Id + "/go"), pieceTr = Allocate(ids, RootName, row.Id + "/tr");
                var comps = new List<long> { pieceTr };
                var parts = new List<(string name, string text)>();
                Vector2 at = row.At;
                string rotation = null;
                if (row.Kind == "vehicle")
                {
                    bool trailer = row.Kit == "TrailerIso";
                    string def = row.Kit switch
                    {
                        "VehicleIso" => "Assets/_Project/Data/Vehicles/Dually3500.asset",
                        "VanIso" => "Assets/_Project/Data/Vehicles/HightopVan.asset",
                        "AeroSemiIso" => "Assets/_Project/Data/Vehicles/AeroSemi.asset",
                        "TrailerIso" => "Assets/_Project/Data/Vehicles/Meshes/TrailerReefer53VehicleMesh.asset",
                        _ => throw new Refusal(row.Id + ": no existing vehicle route for " + row.Kit),
                    };
                    long parked = Allocate(ids, RootName, row.Id + "/parked"); comps.Add(parked);
                    var script = Script("Assets/_Project/Code/Vehicles/" + (trailer ? "ParkedTrailer" : "ParkedVehicle") + ".cs",
                        "HiddenHarbours.Vehicles", "HiddenHarbours.Vehicles." + (trailer ? "ParkedTrailer" : "ParkedVehicle"));
                    parts.Add((row.Id + "/parked", MonoBehaviourYaml(parked, pieceGo, script,
                        trailer ? new[] { "  _body: " + Asset(def).Yaml } : new[] { "  _vehicle: " + Asset(def).Yaml, "  _drivable: 0" })));
                    double angle = -KitDirHeading(row.Dir) * Math.PI / 360.0;
                    float z = (float)Math.Sin(angle), w = (float)Math.Cos(angle);
                    if (Math.Abs(z) < 1e-6f) z = 0f; if (Math.Abs(w) < 1e-6f) w = 0f;
                    rotation = "{x: 0, y: 0, z: " + F(z) + ", w: " + F(w) + "}";
                }
                else if (row.Kind == KeySceneDef.KindGull)
                {
                    at.y += (row.Z - ground.ElevationAt(row.At)) * IsoGround.HeightScale;
                    long perch = Allocate(ids, RootName, row.Id + "/perch"); comps.Add(perch);
                    parts.Add((row.Id + "/perch", MonoBehaviourYaml(perch, pieceGo,
                        Script("Assets/_Project/Code/World/GullPerch.cs", "HiddenHarbours.World", "HiddenHarbours.World.GullPerch"),
                        new[] { "  _id: " + row.Id, "  _groundPoint: " + Vec2Yaml(row.At), "  _sortY: " + F(row.At.y) })));
                }
                else
                {
                    SpriteRef sprite;
                    var walls = new List<Vector2[]>();
                    if (row.Kit == "nmcSetPieces")
                    {
                        SetPieceDef def = Definition(row.Piece);
                        if (def.Id != row.Id && !(row.Id == "structure.nmc_harbourmaster" && def.Id == "structure.nmc_wharf_office"))
                            throw new Refusal(row.Id + ": its Def identity is not the ruled identity.");
                        sprite = SpriteAsset(def.SpriteFor(SetPieceDef.FrameForRigDir(row.Dir)));
                        walls = WallsOf(def.Colliders, SetPieceDef.FrameForRigDir(row.Dir), row.Id);
                        long source = Allocate(ids, RootName, row.Id + "/source"); comps.Add(source);
                        parts.Add((row.Id + "/source", MonoBehaviourYaml(source, pieceGo,
                            Script("Assets/_Project/Code/Art/SetPiecePlacement.cs", "HiddenHarbours.Art", "HiddenHarbours.Art.SetPiecePlacement"),
                            new[] { "  _def: " + Asset("Assets/_Project/Data/SetPieces/SetPiece_" + row.Piece + ".asset").Yaml,
                                "  _definitionId: " + def.Id, "  _placementId: " + row.Id, "  _rigDir: " + row.Dir })));
                    }
                    else if (row.Kit == "wharfBuilding2")
                    {
                        var entry = buildings.buildings.Single(b => b.key == row.Id);
                        string spriteName = VillageBuildingKit.SpriteNameFor(entry.key, SetPieceDef.FrameForRigDir(row.Dir));
                        sprite = SpriteAsset(AssetDatabase.LoadAllAssetsAtPath(NmcWharfBuildingBakeMenu.Folder + "/" + entry.sheet)
                            .OfType<Sprite>().Single(s => s.name == spriteName));
                        float x = entry.footprintWidthMetres * 0.5f, y = entry.footprintLengthMetres * 0.5f;
                        Vector2[] points = { new Vector2(-x,-y), new Vector2(x,-y), new Vector2(x,y), new Vector2(-x,y) };
                        walls.Add(points.Select(p => { Vector2 v = SetPieceDef.ToWorld(p, row.Dir); v.y *= IsoGround.GroundDepthScale; return v; }).ToArray());
                    }
                    else if (row.Kit == "wharfDecor")
                        sprite = SpriteAsset(IsoPackSprites.Facing(row.Kit, row.Piece, IsoPackSprites.FacingForHeading(row.Kit, KitDirHeading(row.Dir))));
                    else throw new Refusal(row.Id + ": no placement route for " + row.Kit);
                    long renderer = Allocate(ids, RootName, row.Id + "/sprite"), sort = Allocate(ids, RootName, row.Id + "/sort");
                    comps.Add(renderer); comps.Add(sort);
                    int order = YSortSprite.OrderFor(row.At.y, SortingBands.DecorBase, SortingBands.OrdersPerMetre, SortingBands.DecorFloor, SortingBands.DecorCeiling);
                    parts.Add((row.Id + "/sprite", SpriteRendererYaml(renderer, pieceGo, material, sprite, order)));
                    parts.Add((row.Id + "/sort", MonoBehaviourYaml(sort, pieceGo,
                        Script("Assets/_Project/Code/Art/YSortSprite.cs", "HiddenHarbours.Art", "HiddenHarbours.Art.YSortSprite"),
                        new[] { "  _dynamic: 0", "  _baseOrder: " + SortingBands.DecorBase, "  _orderPerUnit: " + F(SortingBands.OrdersPerMetre),
                            "  _minOrder: " + SortingBands.DecorFloor, "  _maxOrder: " + SortingBands.DecorCeiling, "  _pivotYOffset: 0" })));
                    if (walls.Count > 0)
                    {
                        long collider = Allocate(ids, RootName, row.Id + "/walls"); comps.Add(collider);
                        parts.Add((row.Id + "/walls", PolygonColliderYaml(collider, pieceGo, walls)));
                    }
                }
                wanted.Add((row.Id + "/go", GameObjectYaml(pieceGo, row.Id, comps)));
                string transform = TransformYaml(pieceTr, pieceGo, at, Array.Empty<long>(), tr);
                if (rotation != null) transform = SetField(transform, "m_LocalRotation", rotation);
                wanted.Add((row.Id + "/tr", transform)); wanted.AddRange(parts); children.Add(pieceTr);
            }
            wanted.Insert(0, (RootName + "/go", GameObjectYaml(go, RootName, new[] { tr })));
            wanted.Insert(1, (RootName + "/tr", TransformYaml(tr, go, Vector2.zero, children, 0)));
            return WriteRoot(scene, RootName, go, wanted);
        }

        public static LayerPatch PlaceOfficeVendor(SceneYaml scene) =>
            PlaceVendor(scene, OfficeVendorName, OldOfficeRoot, OfficeVendor, new[] { "HiddenHarbours.Economy.LicenseVendor" });
        public static LayerPatch PlaceMarketCounter(SceneYaml scene) =>
            PlaceVendor(scene, MarketCounterName, OldBuyerRoot, MarketCounter,
                new[] { "HiddenHarbours.Economy.WharfSellPoint", "HiddenHarbours.Economy.FishBuyer", "HiddenHarbours.Economy.Market" });

        static LayerPatch PlaceVendor(SceneYaml scene, string name, long oldRoot, Vector3 at, string[] scripts)
        {
            Doc current = ExistingRoot(scene, name);
            var ids = new IdAllocator(scene, current == null ? new HashSet<long>() : scene.SubtreeOf(current.FileId));
            long go = Allocate(ids, name, "go"), tr = Allocate(ids, name, "tr");
            Doc template = current ?? scene.Require(oldRoot, "the old counter template");
            var monos = scripts.Select(script => scene.ComponentsOf(template).Single(d => d.IsScript(script))).ToArray();
            var map = monos.ToDictionary(d => d.FileId, d => Allocate(ids, name, d.Field("m_EditorClassIdentifier")));
            map[template.FileId] = go; map[scene.TransformOf(template).FileId] = tr;
            var wanted = new List<(string name, string text)>
            {
                (name + "/go", GameObjectYaml(go, name, new[] { tr }.Concat(monos.Select(d => map[d.FileId])).ToArray())),
                (name + "/tr", SetField(TransformYaml(tr, go, at, Array.Empty<long>(), 0), "m_LocalPosition", Vec3Yaml(at.x, at.y, at.z))),
            };
            foreach (Doc mono in monos)
            {
                string text = Regex.Replace(mono.Text, @"^--- !u!114 &-?\d+", "--- !u!114 &" + map[mono.FileId]);
                text = Regex.Replace(text, @"\{fileID: (-?\d+)\}", m => map.TryGetValue(long.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture), out long target)
                    ? "{fileID: " + target.ToString(CultureInfo.InvariantCulture) + "}" : m.Value);
                wanted.Add((name + "/" + mono.Field("m_EditorClassIdentifier"), text));
            }
            return WriteRoot(scene, name, go, wanted);
        }

        public static LayerPatch MoveBuyer(SceneYaml scene)
        {
            Doc go = scene.Docs.Single(d => d.ClassId == 1 && d.Field("m_Name") == "WendellArsenault");
            Doc tr = scene.TransformOf(go);
            Vector2 point = NineMileCreekPeople.People.Single(p => p.AssetName == "WendellArsenault").Position;
            var patch = new LayerPatch(PlacementStep, "WendellArsenault", go.FileId);
            patch.Edit(tr, SetField(tr.Text, "m_LocalPosition", Vec3Yaml(point.x, point.y, 0)), "WendellArsenault/market counter position");
            Doc sort = scene.ComponentsOf(go).Single(d => d.IsScript("HiddenHarbours.Art.YSortSprite"));
            patch.Edit(sort, SetField(sort.Text, "_dynamic", "0"), "WendellArsenault/sorting as on main");
            return patch.Seal(scene);
        }

        public static void RecordPlacement()
        {
            string text = File.ReadAllText(KeyScenePath);
            string[] ids = PlacementRows().Select(p => p.Id).ToArray();
            text = Regex.Replace(text, @"(?m)^  Placed: [01]$", "  Placed: 1");
            text = Regex.Replace(text, @"(?m)^  PlacementIds:(?: \[\])?(?:\n  - [^\n]+)*", "  PlacementIds:\n" + string.Join("\n", ids.Select(id => "  - " + id)));
            if (text != File.ReadAllText(KeyScenePath)) File.WriteAllText(KeyScenePath, text);
            AssetDatabase.ImportAsset(KeyScenePath, ImportAssetOptions.ForceUpdate);
        }
    }
}
#endif
