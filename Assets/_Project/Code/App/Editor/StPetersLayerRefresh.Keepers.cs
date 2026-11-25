#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using HiddenHarbours.Art;
using HiddenHarbours.Art.Editor;
using HiddenHarbours.Core;
using HiddenHarbours.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HiddenHarbours.App.Editor
{
    // The two keepers who live away from the village: the light's keeper at the north-east light, and the
    // bird watcher in the cottage above the Whelps. Each has a post of their own: a few places a few steps apart,
    // carried on the keeper, so their days never touch the village's stations or its lane tree.
    public static partial class StPetersLayerRefresh
    {
        public const string KeepersStep = "Keepers";
        public const string KeepersRegisterStep = "KeepersRegister";
        public const string KeepersRootName = "StPetersKeepers";
        public const string KeepersFolder = "Assets/_Project/Data/Regions/StPetersKeepers";
        public const string KeepersNpcFolder = "Assets/_Project/Data/NPCs";
        public const string KeepersInteractorName = "WorldInteractor";
        const string KeepersCharacterMaterialGuid = "a97c105638bdf8b4a8650670310a4cd3";
        /// <summary>The cottage's build in the village kit: its shell, and the sheets that say how it takes the light.</summary>
        public const string KeepersCottageBuild = "sageCottage";

        // ---- the north-east light: from the tower's ground, metres across and away (already as drawn) ----
        public const string LightKeeperAnchorId = "anchor.stp_ne_light.tower_door";
        public const string LightTowerDoorStation = "station.st_peters.light_tower_door";
        public const string LightYardStation = "station.st_peters.light_yard";
        public const string LightHouseStation = "station.st_peters.light_keepers_house";
        static readonly Vector2 LightTowerDoorFromTower = new Vector2(0.9f, -1.9f);
        static readonly Vector2 LightYardFromTower = new Vector2(-2.6f, -2.9f);
        static readonly Vector2 LightHouseDoorFromTower = new Vector2(-5.7f, -2.5f);

        // ---- the cottage above the Whelps ----
        public const string WhelpsKeySceneId = "keyscene.stp_whelps_lookout";
        public const string WhelpsBenchPieceId = "prop.stp_whelps_lookout_bench";
        public const string WhelpsCottageAnchorId = "anchor.stp_whelps.cottage_door";
        public const string WhelpsCottageDoorStation = "station.st_peters.whelps_cottage_door";
        public const string WhelpsCottageInsideStation = "station.st_peters.whelps_cottage_inside";
        public const string WhelpsBenchStation = "station.st_peters.whelps_lookout_bench";
        /// <summary>The cottage's ground centre from the lookout bench: inland of the cliff walk, north of the bench.</summary>
        static readonly Vector2 WhelpsCottageFromBench = new Vector2(-3.4f, 9.3f);
        /// <summary>The house rig's facing with the door on the gable toward the path and the sea (cell i is 45 i degrees).</summary>
        public const int WhelpsCottageFacing = 3;
        const int HouseFacingDoorEast = 2;
        const float HouseFacingStepDegrees = 45f;
        /// <summary>The cottage's plan, metres: across and along (the baked sheet's contract).</summary>
        const float WhelpsCottageWidth = 6.6f, WhelpsCottageLength = 8.05f;
        /// <summary>A step out from the door, where a caller stands.</summary>
        const float WhelpsDoorstepMetres = 0.9f;
        /// <summary>Where the watcher stands by the bench, from the bench.</summary>
        static readonly Vector2 WhelpsBenchStandFromBench = new Vector2(-1.1f, 0.1f);

        public sealed class KeeperStop
        {
            public string StationId, Why;
            public Vector2 At;
            public float HeadingDegrees;
            public bool OutOfSight;
        }

        public sealed class KeeperPost
        {
            public string Name, NpcAsset, RoutineAsset, AnchorId;
            public Vector2 AnchorAt;
            public KeeperStop[] Stops;
            public bool HasCottage;
            public Vector2 CottageAt;
        }

        /// <summary>The lamp over the cottage door: how high it hangs and how far it lights the step, metres.</summary>
        public const float WhelpsDoorLampHeightMetres = 2f, WhelpsDoorLampReachMetres = 3.4f;
        public const string DoorLampName = "DoorLamp";

        /// <summary>The cottage's plan on the ground as drawn, about <paramref name="centre"/>.</summary>
        public static Vector2[] WhelpsCottagePlan(Vector2 centre)
        {
            Vector2 u = WhelpsDoorAxis() * (WhelpsCottageLength * 0.5f);
            Vector2 v = new Vector2(-WhelpsDoorAxis().y, WhelpsDoorAxis().x) * (WhelpsCottageWidth * 0.5f);
            return new[] { centre + Drawn(-u - v), centre + Drawn(u - v), centre + Drawn(u + v), centre + Drawn(-u + v) };
        }

        /// <summary>The ground each keeper's cottage in the scene stands on, for the rule that keeps planting off
        /// buildings: a cottage drawn whole has no room to read, so its plan is given here.</summary>
        public static List<StPetersBuildingClearance.Zone> KeeperCottageZones(SceneYaml scene)
        {
            var zones = new List<StPetersBuildingClearance.Zone>();
            if (!scene.Docs.Any(d => d.ClassId == GameObjectClass && !d.Stripped && d.Field("m_Name") == KeepersRootName)) return zones;
            var owned = scene.SubtreeOf(scene.RootNamed(KeepersRootName).FileId);
            foreach (var go in scene.Docs.Where(d => owned.Contains(d.FileId) && d.ClassId == GameObjectClass &&
                         (d.Field("m_Name") ?? "").EndsWith("Cottage", StringComparison.Ordinal)))
                zones.Add(new StPetersBuildingClearance.Zone
                {
                    Path = KeepersRootName + "/" + go.Field("m_Name"),
                    Corners = WhelpsCottagePlan(InteriorFixturePlacement.WorldOf(scene, scene.TransformOf(go))),
                });
            return zones;
        }

        /// <summary>The cottage's long axis on the ground: toward its door.</summary>
        static Vector2 WhelpsDoorAxis()
        {
            // Facing 2 has the door due east and facing 4 due south: each facing turns it an eighth, clockwise.
            float a = -HouseFacingStepDegrees * (WhelpsCottageFacing - HouseFacingDoorEast) * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(a), Mathf.Sin(a));
        }

        static Vector2 Drawn(Vector2 ground) => new Vector2(ground.x, ground.y * IsoGround.GroundDepthScale);

        /// <summary>The two posts, worked out from where the tower and the lookout bench stand.</summary>
        public static KeeperPost[] KeeperPosts(Vector2 towerAt, Vector2 benchAt)
        {
            Vector2 cottage = benchAt + WhelpsCottageFromBench;
            Vector2 axis = WhelpsDoorAxis();
            Vector2 door = cottage + Drawn(axis * (WhelpsCottageLength * 0.5f));
            Vector2 doorstep = cottage + Drawn(axis * (WhelpsCottageLength * 0.5f + WhelpsDoorstepMetres));
            Vector2 benchStand = benchAt + WhelpsBenchStandFromBench;
            return new[]
            {
                new KeeperPost
                {
                    Name = "AlbanDoucet", NpcAsset = "AlbanDoucet", RoutineAsset = "AlbanDoucetRoutine",
                    AnchorId = LightKeeperAnchorId, AnchorAt = towerAt + LightTowerDoorFromTower,
                    Stops = new[]
                    {
                        new KeeperStop { StationId = LightTowerDoorStation, At = towerAt + LightTowerDoorFromTower,
                            HeadingDegrees = 180f, Why = "at the foot of the tower, where a caller finds the keeper" },
                        new KeeperStop { StationId = LightYardStation, At = towerAt + LightYardFromTower,
                            HeadingDegrees = 90f, Why = "the yard between the house and the tower, looking to seaward" },
                        new KeeperStop { StationId = LightHouseStation, At = towerAt + LightHouseDoorFromTower,
                            HeadingDegrees = 180f, OutOfSight = true, Why = "indoors in the keeper's house" },
                    },
                },
                new KeeperPost
                {
                    Name = "OdileMarchand", NpcAsset = "OdileMarchand", RoutineAsset = "OdileMarchandRoutine",
                    AnchorId = WhelpsCottageAnchorId, AnchorAt = doorstep, HasCottage = true, CottageAt = cottage,
                    Stops = new[]
                    {
                        new KeeperStop { StationId = WhelpsCottageDoorStation, At = doorstep,
                            HeadingDegrees = 135f, Why = "her own doorstep, where a caller finds her" },
                        new KeeperStop { StationId = WhelpsBenchStation, At = benchStand,
                            HeadingDegrees = 90f, Why = "beside the lookout bench, watching the Whelps" },
                        new KeeperStop { StationId = WhelpsCottageInsideStation, At = door,
                            HeadingDegrees = 135f, OutOfSight = true, Why = "indoors in the cottage" },
                    },
                },
            };
        }

        public interface IKeeperAssets
        {
            ObjRef Asset(string path);
            /// <summary>A cell of the keeper's own sheet for the renderer to hold until the first frame.</summary>
            SpriteRef FirstSpriteOf(string npcPath, out ObjRef visual);
            SpriteRef Cottage(int facing);
            /// <summary>The material every village house draws with, and one of the cottage's own light sheets.</summary>
            ObjRef CottageMaterial { get; }
            ObjRef CottageSheet(VillageBuildingKit.Channel channel);
            ObjRef Material { get; }
            ScriptRef ScriptFor(Type type);
        }

        public sealed class EditorKeeperAssets : IKeeperAssets
        {
            readonly EditorKeySceneAssets _shared = new EditorKeySceneAssets();
            readonly EditorVillageLightAssets _scripts = new EditorVillageLightAssets();
            public ObjRef Material => _shared.SpriteMaterial;
            public ScriptRef ScriptFor(Type type) => _scripts.ScriptFor(type);

            public ObjRef Asset(string path)
            {
                var asset = AssetDatabase.LoadMainAssetAtPath(path);
                if (asset == null || !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string guid, out long id))
                    throw new Refusal($"{KeepersStep}: no asset at {path}.");
                return new ObjRef(id, guid, 2);
            }

            public SpriteRef FirstSpriteOf(string npcPath, out ObjRef visual)
            {
                var npc = AssetDatabase.LoadAssetAtPath<NpcDef>(npcPath);
                if (npc == null || npc.Build == null || npc.Build.Visual == null)
                    throw new Refusal($"{KeepersStep}: {npcPath} has no build to draw.");
                if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(npc.Build.Visual, out string guid, out long id))
                    throw new Refusal($"{KeepersStep}: {npcPath}'s visual is not an asset.");
                visual = new ObjRef(id, guid, 2);
                Sprite first = EditorUtility.CollectDependencies(new UnityEngine.Object[] { npc.Build.Visual }).OfType<Sprite>()
                    .OrderBy(s => AssetDatabase.GetAssetPath(s), StringComparer.Ordinal).ThenBy(s => s.name, StringComparer.Ordinal)
                    .FirstOrDefault();
                if (first == null) throw new Refusal($"{KeepersStep}: {npcPath}'s visual holds no sprite.");
                return EditorShoreRockSprites.RefOf(first);
            }

            public SpriteRef Cottage(int facing)
            {
                var shell = VillageBuildingCatalog.Find(KeepersCottageBuild);
                Sprite cell = shell.IsValid ? VillageBuildingCatalog.LoadFacing(shell, facing) : null;
                if (cell == null) throw new Refusal($"{KeepersStep}: the village kit has no {KeepersCottageBuild} facing {facing}.");
                return EditorShoreRockSprites.RefOf(cell);
            }

            public ObjRef CottageMaterial => Imported(VillageBuildingCatalog.LoadLitMaterial(), 2, "the lit house material");

            public ObjRef CottageSheet(VillageBuildingKit.Channel channel) => Imported(
                AssetDatabase.LoadAssetAtPath<Texture2D>(VillageBuildingKit.SheetPath(KeepersCottageBuild, channel)), 3,
                "the cottage's " + channel + " sheet");

            static ObjRef Imported(UnityEngine.Object value, int type, string what)
            {
                if (value == null || !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(value, out string guid, out long id))
                    throw new Refusal($"{KeepersStep}: {what} is not baked and imported.");
                return new ObjRef(id, guid, type);
            }
        }

        static string Quoted(string s) => "'" + s.Replace("'", "''") + "'";

        /// <summary>The patch that stands the keepers, their posts and the cottage under <see cref="KeepersRootName"/>.</summary>
        public static LayerPatch Keepers(SceneYaml scene, IReadOnlyList<KeeperPost> posts, IKeeperAssets assets)
        {
            const string rootName = KeepersRootName;
            var root = scene.Docs.Any(d => d.ClassId == GameObjectClass && d.Field("m_Name") == rootName)
                ? scene.RootNamed(rootName) : null;
            var owned = root == null ? new HashSet<long>() : scene.SubtreeOf(root.FileId);
            var ids = new IdAllocator(scene, owned);
            long Next(string name, string kind) => ids.Next(rootName + "/" + name + "/" + kind);
            long rootGo = Next("", "GameObject"), rootTr = Next("", "Transform");
            if (root != null && root.FileId != rootGo) throw new Refusal($"{KeepersStep}: the root has unexpected ids.");

            var wanted = new List<Wanted>();
            var rootChildren = new List<long>();
            var none = new List<long>();
            var material = new ObjRef(2100000, KeepersCharacterMaterialGuid, 2);
            foreach (KeeperPost post in posts)
            {
                string name = post.Name;
                // ---- the place a caller is sent to: a named point and nothing else ----
                {
                    long ag = Next(post.AnchorId, "GameObject"), at = Next(post.AnchorId, "Transform");
                    wanted.Add(Want(GameObjectYaml(ag, post.AnchorId, new[] { at }), post.AnchorId, "GameObject"));
                    wanted.Add(Want(TransformYaml(at, ag, post.AnchorAt, none, rootTr), post.AnchorId, "Transform"));
                    rootChildren.Add(at);
                }

                // ---- the cottage, where there is one ----
                if (post.HasCottage)
                {
                    string child = name + "Cottage";
                    long cg = Next(child, "GameObject"), ct = Next(child, "Transform"), sr = Next(child, "SpriteRenderer");
                    long sort = Next(child, "YSortSprite"), walls = Next(child, "PolygonCollider2D");
                    long binder = Next(child, "SpriteLightBinder");
                    string lampChild = child + "/" + DoorLampName;
                    long lg = Next(lampChild, "GameObject"), lt = Next(lampChild, "Transform");
                    long light = Next(lampChild, "SceneLight"), preset = Next(lampChild, "PreconfiguredLight");
                    var plan = new List<Vector2[]> { WhelpsCottagePlan(Vector2.zero) };
                    int order = YSortSprite.OrderFor(post.CottageAt.y, SortingBands.DecorBase, SortingBands.OrdersPerMetre,
                        SortingBands.DecorFloor, SortingBands.DecorCeiling);
                    wanted.Add(Want(GameObjectYaml(cg, child, new[] { ct, sr, sort, binder, walls }), child, "GameObject"));
                    wanted.Add(Want(TransformYaml(ct, cg, post.CottageAt, new[] { lt }, rootTr), child, "Transform"));
                    wanted.Add(Want(SpriteRendererYaml(sr, cg, assets.CottageMaterial, assets.Cottage(WhelpsCottageFacing), order), child, "SpriteRenderer"));
                    wanted.Add(Want(MonoBehaviourYaml(sort, cg, assets.ScriptFor(typeof(YSortSprite)), new[]
                    {
                        "  _dynamic: 0", "  _baseOrder: " + F(SortingBands.DecorBase),
                        "  _orderPerUnit: " + F(SortingBands.OrdersPerMetre), "  _minOrder: " + Int(SortingBands.DecorFloor),
                        "  _maxOrder: " + Int(SortingBands.DecorCeiling), "  _pivotYOffset: 0",
                    }), child, "YSortSprite"));
                    // A village house, placed as every village house is: the lit house material, and the binder that
                    // hands the renderer its own mask, normal and window sheets.
                    wanted.Add(Want(MonoBehaviourYaml(binder, cg, assets.ScriptFor(typeof(SpriteLightBinder)), new[]
                    {
                        "  _lightSheet: " + assets.CottageSheet(VillageBuildingKit.Channel.Mask).Yaml,
                        "  _normalSheet: " + assets.CottageSheet(VillageBuildingKit.Channel.Normal).Yaml,
                        "  _rimGateSheet: {fileID: 0}", "  _rimGateChannel: 0",
                        "  _emitSheet: " + assets.CottageSheet(VillageBuildingKit.Channel.Emit).Yaml,
                    }), child, "SpriteLightBinder"));
                    wanted.Add(Want(PolygonColliderYaml(walls, cg, plan), child, "PolygonCollider2D"));

                    // ---- the lamp over the door, a lantern like any doorstep's: lit when the night comes ----
                    var look = LightPresets.For(LightPresets.Kind.Lantern);
                    Vector2 doorOnTheGround = Drawn(WhelpsDoorAxis() * (WhelpsCottageLength * 0.5f));
                    wanted.Add(Want(GameObjectYaml(lg, DoorLampName, new[] { lt, light, preset }), lampChild, "GameObject"));
                    wanted.Add(Want(TransformYaml(lt, lg, doorOnTheGround, none, ct), lampChild, "Transform"));
                    wanted.Add(Want(MonoBehaviourYaml(light, lg, assets.ScriptFor(typeof(SceneLight)), SceneLightFields(look.Shape, 30f,
                        look.Range, look.Color, look.Intensity, look.EdgeSoftness, look.FlickerAmount, WhelpsDoorLampHeightMetres,
                        WhelpsDoorLampHeightMetres * IsoGround.HeightScale, WhelpsDoorLampReachMetres, 20f)), lampChild, "SceneLight"));
                    wanted.Add(Want(MonoBehaviourYaml(preset, lg, assets.ScriptFor(typeof(PreconfiguredLight)), new[]
                    {
                        "  _preset: " + Int((int)LightPresets.Kind.Lantern), "  _intensityScale: 1", "  _fittingWidthMetres: 0",
                        "  _reachOverrideMetres: " + F(WhelpsDoorLampReachMetres),
                    }), lampChild, "PreconfiguredLight"));
                    rootChildren.Add(ct);
                }

                // ---- the keeper, built the way every islander is ----
                {
                    string npcPath = KeepersNpcFolder + "/" + post.NpcAsset + ".asset";
                    SpriteRef cell = assets.FirstSpriteOf(npcPath, out ObjRef visual);
                    KeeperStop first = post.Stops[0];
                    long kg = Next(name, "GameObject"), kt = Next(name, "Transform"), sr = Next(name, "SpriteRenderer");
                    long iso = Next(name, "IsoCharacterSprite"), sort = Next(name, "YSortSprite");
                    long talk = Next(name, "Interactable"), routine = Next(name, "KeeperRound");
                    int order = YSortSprite.OrderFor(first.At.y, SortingBands.DecorBase, SortingBands.OrdersPerMetre,
                        SortingBands.DecorFloor, SortingBands.DecorCeiling);
                    wanted.Add(Want(GameObjectYaml(kg, name, new[] { kt, sr, iso, sort, talk, routine }), name, "GameObject"));
                    wanted.Add(Want(TransformYaml(kt, kg, first.At, none, rootTr), name, "Transform"));
                    wanted.Add(Want(SpriteRendererYaml(sr, kg, material, cell, order), name, "SpriteRenderer"));
                    wanted.Add(Want(MonoBehaviourYaml(iso, kg, assets.ScriptFor(typeof(IsoCharacterSprite)), new[]
                    {
                        "  _visual: " + visual.Yaml, "  _initialHeadingDegrees: " + F(first.HeadingDegrees),
                        "  _speedSmoothingSeconds: 0.08", "  _headingMinSpeed: 0.05",
                    }), name, "IsoCharacterSprite"));
                    wanted.Add(Want(MonoBehaviourYaml(sort, kg, assets.ScriptFor(typeof(YSortSprite)), new[]
                    {
                        "  _dynamic: 1", "  _baseOrder: " + F(SortingBands.DecorBase),
                        "  _orderPerUnit: " + F(SortingBands.OrdersPerMetre), "  _minOrder: " + Int(SortingBands.DecorFloor),
                        "  _maxOrder: " + Int(SortingBands.DecorCeiling), "  _pivotYOffset: 0",
                    }), name, "YSortSprite"));
                    wanted.Add(Want(MonoBehaviourYaml(talk, kg, assets.ScriptFor(typeof(Interactable)), new[]
                    {
                        "  _npc: " + assets.Asset(npcPath).Yaml, "  _bubbleAnchor: {fileID: 0}", "  _kind: 0", "  _speaker: ",
                        "  _conversationId: ", "  _completionFlag: ",
                    }), name, "Interactable"));
                    var roundFields = new List<string>
                    {
                        "  _routine: " + assets.Asset(KeepersFolder + "/" + post.RoutineAsset + ".asset").Yaml, "  _stops:",
                    };
                    foreach (KeeperStop stop in post.Stops)
                    {
                        roundFields.Add("  - Id: " + stop.StationId);
                        roundFields.Add("    Position: " + Vec2Yaml(stop.At));
                        roundFields.Add("    StandHeadingDegrees: " + F(stop.HeadingDegrees));
                        roundFields.Add("    Interior: {fileID: 0}");
                        roundFields.Add("    OutOfSight: " + (stop.OutOfSight ? "1" : "0"));
                        roundFields.Add("    LaneNodeName: ");
                        roundFields.Add("    Why: " + Quoted(stop.Why));
                    }
                    wanted.Add(Want(MonoBehaviourYaml(routine, kg, assets.ScriptFor(typeof(KeeperRound)), roundFields), name, "KeeperRound"));
                    rootChildren.Add(kt);
                }
            }

            wanted.Insert(0, Want(GameObjectYaml(rootGo, rootName, new[] { rootTr }), rootName, "GameObject"));
            wanted.Insert(1, Want(TransformYaml(rootTr, rootGo, Vector2.zero, rootChildren, 0), rootName, "Transform"));
            var patch = new LayerPatch(KeepersStep, rootName, rootGo);
            var desired = new HashSet<long>(wanted.Select(w => w.FileId));
            foreach (var doc in scene.Docs.Where(d => owned.Contains(d.FileId) && !desired.Contains(d.FileId)))
                patch.Delete(doc, NameOfExisting(scene, doc));
            foreach (var want in wanted)
            {
                var doc = scene.Get(want.FileId);
                if (doc == null) patch.Add(want.Text, want.Name);
                else patch.Edit(doc, want.Text, want.Name);
            }
            if (root == null)
            {
                var list = SceneRootsOf(scene);
                patch.Edit(list, WithRootListed(list.Text, rootTr), rootName + " joins SceneRoots");
            }
            return patch.Seal(scene);
        }

        /// <summary>The keepers' Interactables in the scene, in the posts' order.</summary>
        public static List<long> KeeperInteractables(SceneYaml scene, IReadOnlyList<KeeperPost> posts)
        {
            Doc root = scene.RootNamed(KeepersRootName);
            HashSet<long> owned = scene.SubtreeOf(root.FileId);
            var found = new List<long>();
            foreach (KeeperPost post in posts)
            {
                Doc go = scene.Docs.Single(d => owned.Contains(d.FileId) && d.ClassId == GameObjectClass && d.Field("m_Name") == post.Name);
                found.Add(scene.ComponentsOf(go).Single(d => d.IsScript(typeof(Interactable).FullName)).FileId);
            }
            return found;
        }

        /// <summary>The region's interactor talks to the keepers: each is appended to its list once, after those already there.</summary>
        public static LayerPatch RegisterKeepers(SceneYaml scene, IReadOnlyList<KeeperPost> posts)
        {
            Doc go = scene.RootNamed(KeepersInteractorName);
            Doc interactor = scene.ComponentsOf(go).Single(d => d.IsScript(typeof(WorldInteractor).FullName));
            var patch = new LayerPatch(KeepersRegisterStep, KeepersInteractorName, go.FileId);
            List<long> listed = interactor.FieldRefs("_interactables");
            List<long> missing = KeeperInteractables(scene, posts).Where(id => !listed.Contains(id)).ToList();
            if (missing.Count == 0) return patch.Seal(scene);
            var lines = interactor.Text.Split('\n').ToList();
            int head = lines.FindIndex(l => l == "  _interactables:" || l == "  _interactables: []");
            if (head < 0 || lines.Count(l => l.StartsWith("  _interactables:", StringComparison.Ordinal)) != 1)
                throw new Refusal(KeepersRegisterStep + ": the interactor's list is not one plain block.");
            lines[head] = "  _interactables:";
            int end = head + 1;
            while (end < lines.Count && lines[end].StartsWith("  - ", StringComparison.Ordinal)) end++;
            lines.InsertRange(end, missing.Select(id => "  - {fileID: " + Id(id) + "}"));
            patch.Edit(interactor, string.Join("\n", lines), KeepersInteractorName + "/talks to the keepers");
            return patch.Seal(scene);
        }

        /// <summary>The posts as the scene's own data places them: the light's tower row and the lookout's bench row.</summary>
        public static KeeperPost[] LoadKeeperPosts()
        {
            var stations = LoadLightStations();
            if (stations.Count != 1) throw new Refusal($"{KeepersStep}: expected one light station, found {stations.Count}.");
            KeySceneDef whelps = null;
            foreach (string guid in AssetDatabase.FindAssets("t:" + nameof(KeySceneDef), new[] { KeySceneFolder }))
            {
                var k = AssetDatabase.LoadAssetAtPath<KeySceneDef>(AssetDatabase.GUIDToAssetPath(guid));
                if (k != null && k.Id == WhelpsKeySceneId) whelps = k;
            }
            if (whelps == null) throw new Refusal($"{KeepersStep}: no key scene {WhelpsKeySceneId}.");
            KeyScenePiece bench = whelps.Pieces.SingleOrDefault(p => p.Id == WhelpsBenchPieceId)
                ?? throw new Refusal($"{KeepersStep}: the lookout has no bench row ({WhelpsBenchPieceId}).");
            return KeeperPosts(stations[0].TowerAt, bench.At);
        }

        [MenuItem("Hidden Harbours/World/St Peters Layer Refresh/Write the Keepers Patch (dry run)")]
        static void WriteKeepersMenu() { try { RunKeepers(false); } catch (Refusal r) { Debug.LogError(r.Message); } }

        /// <summary>The dry run for <c>-executeMethod</c>.</summary>
        public static void WriteKeepersPatchBatch() => RunKeepers(apply: false);

        /// <summary>The apply for <c>-executeMethod</c>.</summary>
        public static void ApplyKeepersPatchBatch() => RunKeepers(apply: true);

        public static void RunKeepers(bool apply)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (apply && string.Equals(SceneManager.GetSceneAt(i).path, ScenePath, StringComparison.Ordinal))
                    throw new Refusal("StPeters is open in the editor. Close it first: an open scene would overwrite the file on its next save.");

            string text = File.ReadAllText(ScenePath);
            KeeperPost[] posts = LoadKeeperPosts();
            Directory.CreateDirectory(PatchFolder);
            var utf8 = new UTF8Encoding(false);
            LayerPatch stand = Keepers(SceneYaml.Parse(text), posts, new EditorKeeperAssets());
            File.WriteAllText(Path.Combine(PatchFolder, stand.Step + ".patch.yaml"), stand.ToYaml(), utf8);
            string after = stand.ApplyTo(text);
            LayerPatch register = RegisterKeepers(SceneYaml.Parse(after), posts);
            File.WriteAllText(Path.Combine(PatchFolder, register.Step + ".patch.yaml"), register.ToYaml(), utf8);
            after = register.ApplyTo(after);
            if (apply && after != text) File.WriteAllText(ScenePath, after, utf8);
            Debug.Log($"[StPetersLayerRefresh] {(apply ? "applied to " + ScenePath : "dry run, nothing written to the scene")} " +
                      $"(patches in {PatchFolder}):\n{stand.Summary()}\n{register.Summary()}");
        }
    }
}
#endif
