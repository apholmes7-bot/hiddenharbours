#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using HiddenHarbours.Tools.RigBaking;
using HiddenHarbours.World;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.App.Editor
{
    public static partial class StPetersLayerRefresh
    {
        public interface IVillageDressingAssets
        {
            SpriteRef SpriteFor(StPetersVillageDressing.Row row);
            ObjRef Material { get; }
            ScriptRef Sorter { get; }
        }

        public sealed class EditorVillageDressingAssets : IVillageDressingAssets
        {
            readonly EditorKeySceneAssets _shared = new EditorKeySceneAssets();
            public ObjRef Material => _shared.SpriteMaterial;
            public ScriptRef Sorter => _shared.Sorter;
            public SpriteRef SpriteFor(StPetersVillageDressing.Row row)
            {
                var sprite = StPetersVillageDressing.SpriteFor(row);
                if (sprite == null) throw new Refusal("Village dressing has no baked sprite: " + row.Piece.Id);
                return EditorShoreRockSprites.RefOf(sprite);
            }
        }

        public interface IVillageLightAssets
        {
            SpriteRef Post { get; }
            ObjRef Material { get; }
            ScriptRef ScriptFor(Type type);
        }

        public sealed class EditorVillageLightAssets : IVillageLightAssets
        {
            readonly EditorKeySceneAssets _shared = new EditorKeySceneAssets();
            public ObjRef Material => _shared.SpriteMaterial;
            public SpriteRef Post
            {
                get
                {
                    var sprite = IsoPackSprites.Facing("wharfDecor", LampPosts.LanternPost,
                        IsoPackSprites.FacingForHeading("wharfDecor", 180f));
                    if (sprite == null) throw new Refusal("Village lantern post is not baked.");
                    return EditorShoreRockSprites.RefOf(sprite);
                }
            }
            public ScriptRef ScriptFor(Type type)
            {
                foreach (string guid in AssetDatabase.FindAssets("t:MonoScript " + type.Name))
                {
                    var script = AssetDatabase.LoadAssetAtPath<MonoScript>(AssetDatabase.GUIDToAssetPath(guid));
                    if (script != null && script.GetClass() == type &&
                        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(script, out string assetGuid, out long id))
                        return new ScriptRef(new ObjRef(id, assetGuid, 3), type.Assembly.GetName().Name + "::" + type.FullName);
                }
                throw new Refusal("Village light has no script: " + type.FullName);
            }
        }

        public static LayerPatch VillageLights(SceneYaml scene, IReadOnlyList<LightPostDef> defs, IVillageLightAssets assets)
        {
            if (defs.Select(d => d.Id).Distinct(StringComparer.Ordinal).Count() != defs.Count)
                throw new Refusal("Village lights have duplicate ids.");
            const string rootName = StPetersVillageLights.RootName;
            var root = scene.Docs.Any(d => d.ClassId == GameObjectClass && d.Field("m_Name") == rootName)
                ? scene.RootNamed(rootName) : null;
            var owned = root == null ? new HashSet<long>() : scene.SubtreeOf(root.FileId);
            var ids = new IdAllocator(scene, owned);
            long Next(string name, string kind) => ids.Next(rootName + "/" + name + "/" + kind);
            long rootGo = Next("", "GameObject"), rootTr = Next("", "Transform");
            if (root != null && root.FileId != rootGo) throw new Refusal("Village light root has unexpected ids.");
            var wanted = new List<Wanted>();
            var children = new List<long>();
            foreach (var def in defs.OrderBy(d => d.Id, StringComparer.Ordinal))
            {
                string name = def.Id;
                long go = Next(name, "GameObject"), tr = Next(name, "Transform");
                var components = new List<long> { tr };
                bool post = def.KitPiece == LampPosts.LanternPost;
                if (post)
                {
                    var sprite = assets.Post;
                    if (string.IsNullOrEmpty(sprite.Sprite.Guid)) throw new Refusal("Village lantern post has no sprite asset.");
                    long sr = Next(name, "SpriteRenderer"), sort = Next(name, "YSortSprite"), shadow = Next(name, "SpriteShadow");
                    components.AddRange(new[] { sr, sort, shadow });
                    int order = YSortSprite.OrderFor(def.Position.y, SortingBands.DecorBase, SortingBands.OrdersPerMetre,
                        SortingBands.DecorFloor, SortingBands.DecorCeiling);
                    wanted.Add(Want(SpriteRendererYaml(sr, go, assets.Material, sprite, order), name, "SpriteRenderer"));
                    wanted.Add(Want(MonoBehaviourYaml(sort, go, assets.ScriptFor(typeof(YSortSprite)), new[]
                    {
                        "  _dynamic: 0", "  _baseOrder: " + F(SortingBands.DecorBase),
                        "  _orderPerUnit: " + F(SortingBands.OrdersPerMetre), "  _minOrder: " + Int(SortingBands.DecorFloor),
                        "  _maxOrder: " + Int(SortingBands.DecorCeiling), "  _pivotYOffset: 0",
                    }), name, "YSortSprite"));
                    wanted.Add(Want(MonoBehaviourYaml(shadow, go, assets.ScriptFor(typeof(SpriteShadow)), new[]
                    {
                        "  _sortingOffset: -1", "  _pixelSnap: 1", "  _pixelsPerUnit: 32", "  _castsGroundContact: 0",
                        "  _footOffset: 0", "  _fallbackHour: 10", "  _refreshHz: 10", "  _profile: {fileID: 0}",
                    }), name, "SpriteShadow"));
                }
                if (StPetersVillageLights.IsLit(name))
                {
                    if (float.IsNaN(def.Reach) || float.IsInfinity(def.Reach) || def.Reach <= 0f)
                        throw new Refusal("Village light has invalid reach: " + name);
                    var kind = StPetersVillageLights.Preset(def.Preset);
                    var look = LightPresets.For(kind);
                    float width = post ? LampPosts.LanternPostFittingWidthMetres : 0f;
                    float height = StPetersVillageLights.FittingHeight(name, def.KitPiece);
                    long lamp = Next(name, "SceneLight"), preset = Next(name, "PreconfiguredLight");
                    components.AddRange(new[] { lamp, preset });
                    wanted.Add(Want(MonoBehaviourYaml(lamp, go, assets.ScriptFor(typeof(SceneLight)), new[]
                    {
                        "  _shape: " + Int((int)look.Shape), "  _coneHalfAngle: 30",
                        "  _range: " + F(width > 0f ? LightPresets.BloomForFitting(width) : look.Range),
                        "  _color: {r: " + F(look.Color.r) + ", g: " + F(look.Color.g) + ", b: " + F(look.Color.b) + ", a: " + F(look.Color.a) + "}",
                        "  _intensity: " + F(look.Intensity), "  _edgeSoftness: " + F(look.EdgeSoftness),
                        "  _angularSoftness: 0.4", "  _coreBoost: 1", "  _gateThreshold: 0.12", "  _gateSoftness: 0.35", "  _gateFallback: 1",
                        "  _flickerAmount: " + F(look.FlickerAmount), "  _flickerSpeed: 1", "  _sortingOrder: " + Int(SceneLight.MaxSortingOrder),
                        "  _cameraDepthOffset: " + F(SceneLight.DefaultCameraDepthOffset),
                        "  _castsShadows: " + (def.CastsShadow ? "1" : "0"), "  _lampHeightMeters: " + F(height),
                        "  _bloomLiftMetres: " + F(height), "  _reachMetres: " + F(def.Reach),
                        "  _originOffset: " + Vec2Yaml(look.OriginOffset), "  _refreshHz: 20",
                    }), name, "SceneLight"));
                    wanted.Add(Want(MonoBehaviourYaml(preset, go, assets.ScriptFor(typeof(PreconfiguredLight)), new[]
                    {
                        "  _preset: " + Int((int)kind), "  _intensityScale: 1", "  _fittingWidthMetres: " + F(width),
                        "  _reachOverrideMetres: " + F(def.Reach),
                    }), name, "PreconfiguredLight"));
                }
                wanted.Add(Want(GameObjectYaml(go, name, components), name, "GameObject"));
                wanted.Add(Want(TransformYaml(tr, go, def.Position, new List<long>(), rootTr), name, "Transform"));
                children.Add(tr);
            }
            wanted.Insert(0, Want(GameObjectYaml(rootGo, rootName, new[] { rootTr }), rootName, "GameObject"));
            wanted.Insert(1, Want(TransformYaml(rootTr, rootGo, Vector2.zero, children, 0), rootName, "Transform"));
            var patch = new LayerPatch("VillageLights", rootName, rootGo);
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

        public static List<LayerPatch> VillageDressing(SceneYaml scene,
            IReadOnlyList<StPetersVillageDressing.Row> rows, IVillageDressingAssets assets)
        {
            if (rows.Select(r => r.Piece.Id).Distinct(StringComparer.Ordinal).Count() != rows.Count)
                throw new Refusal("Village dressing has duplicate ids.");
            var active = rows.Where(StPetersVillageDressing.Places).ToArray();
            foreach (var row in active)
                if (StPetersVillageDressing.HoldsYard(row.Group) || StPetersVillageDressing.HoldsTree(row.Piece.Id))
                    throw new Refusal("Village dressing tried to place a held row: " + row.Piece.Id);
            var sprites = active.ToDictionary(r => r.Piece.Id, r => assets.SpriteFor(r), StringComparer.Ordinal);
            foreach (var row in active)
                if (string.IsNullOrEmpty(sprites[row.Piece.Id].Sprite.Guid))
                    throw new Refusal("Village dressing has no sprite asset: " + row.Piece.Id);
            var result = new List<LayerPatch>();
            foreach (string rootName in new[] { "Yards", StPetersVillageDressing.CommonsRoot })
            {
                bool yards = rootName == "Yards";
                var ownRows = rows.Where(r => r.Group.StartsWith("yard.", StringComparison.Ordinal) == yards &&
                    !StPetersVillageDressing.HoldsYard(r.Group)).ToArray();
                var patch = VillageDressingRoot(scene, rootName, ownRows, sprites, assets);
                result.Add(patch);
                scene = patch.Apply(scene);
            }
            return result;
        }

        static LayerPatch VillageDressingRoot(SceneYaml scene, string rootName,
            IReadOnlyList<StPetersVillageDressing.Row> rows, Dictionary<string, SpriteRef> sprites,
            IVillageDressingAssets assets)
        {
            var root = scene.Docs.Any(d => d.ClassId == GameObjectClass && d.Field("m_Name") == rootName)
                ? scene.RootNamed(rootName) : null;
            if (root == null && rootName == "Yards") throw new Refusal("Village dressing needs the existing Yards root.");
            Doc rootTransform = root == null ? null : scene.TransformOf(root);
            var groups = rows.Select(r => r.Group).Distinct(StringComparer.Ordinal).OrderBy(s => s, StringComparer.Ordinal).ToArray();
            var owned = new HashSet<long>();
            var keepChildren = rootTransform == null ? new List<long>() : rootTransform.FieldRefs("m_Children");
            foreach (string group in groups)
            {
                var found = scene.Docs.Where(d => d.ClassId == GameObjectClass && d.Field("m_Name") == group).ToArray();
                if (found.Length > 1) throw new Refusal("Village dressing has duplicate groups: " + group);
                if (found.Length == 0) continue;
                var tr = scene.TransformOf(found[0]);
                if (rootTransform == null || tr.FieldRef("m_Father") != rootTransform.FileId)
                    throw new Refusal("Village dressing group is outside its root: " + group);
                owned.UnionWith(scene.SubtreeOf(found[0].FileId));
                keepChildren.Remove(tr.FileId);
            }
            var ids = new IdAllocator(scene, owned);
            long Next(string group, string item, string kind) => ids.Next("VillageDressing/" + rootName + "/" + group + "/" + item + "/" + kind);
            long rootGo = root?.FileId ?? Next("", "", "GameObject");
            long rootTr = rootTransform?.FileId ?? Next("", "", "Transform");
            var wanted = new List<Wanted>();
            foreach (string group in groups)
            {
                var places = rows.Where(r => r.Group == group && StPetersVillageDressing.Places(r))
                    .OrderBy(r => r.Piece.Id, StringComparer.Ordinal).ToArray();
                if (places.Length == 0) continue;
                long groupGo = Next(group, "", "GameObject"), groupTr = Next(group, "", "Transform");
                var children = new List<long>();
                foreach (var row in places)
                {
                    string name = row.Piece.Id;
                    long go = Next(group, name, "GameObject"), tr = Next(group, name, "Transform");
                    long sr = Next(group, name, "SpriteRenderer"), sort = Next(group, name, "YSortSprite");
                    children.Add(tr);
                    wanted.Add(Want(GameObjectYaml(go, name, new[] { tr, sr, sort }), name, "GameObject"));
                    wanted.Add(Want(TransformYaml(tr, go, row.Position, new List<long>(), groupTr), name, "Transform"));
                    int order = YSortSprite.OrderFor(row.Position.y, SortingBands.DecorBase, SortingBands.OrdersPerMetre,
                        SortingBands.DecorFloor, SortingBands.DecorCeiling);
                    wanted.Add(Want(SpriteRendererYaml(sr, go, assets.Material, sprites[name], order), name, "SpriteRenderer"));
                    wanted.Add(Want(MonoBehaviourYaml(sort, go, assets.Sorter, new[]
                    {
                        "  _dynamic: 0", "  _baseOrder: " + F(SortingBands.DecorBase),
                        "  _orderPerUnit: " + F(SortingBands.OrdersPerMetre),
                        "  _minOrder: " + Int(SortingBands.DecorFloor), "  _maxOrder: " + Int(SortingBands.DecorCeiling),
                        "  _pivotYOffset: 0",
                    }), name, "YSortSprite"));
                }
                wanted.Add(Want(GameObjectYaml(groupGo, group, new[] { groupTr }), group, "GameObject"));
                wanted.Add(Want(TransformYaml(groupTr, groupGo, Vector2.zero, children, rootTr), group, "Transform"));
                keepChildren.Add(groupTr);
            }
            var patch = new LayerPatch("VillageDressing", rootName, rootGo);
            if (root == null)
            {
                wanted.Insert(0, Want(GameObjectYaml(rootGo, rootName, new[] { rootTr }), rootName, "GameObject"));
                wanted.Insert(1, Want(TransformYaml(rootTr, rootGo, Vector2.zero, keepChildren, 0), rootName, "Transform"));
                var list = SceneRootsOf(scene);
                patch.Edit(list, WithRootListed(list.Text, rootTr), rootName + " joins SceneRoots");
            }
            else patch.Edit(rootTransform, SetRefList(rootTransform.Text, "m_Children", keepChildren), rootName + " dressing groups");
            var wantedIds = new HashSet<long>(wanted.Select(w => w.FileId));
            foreach (Doc doc in scene.Docs.Where(d => owned.Contains(d.FileId) && !wantedIds.Contains(d.FileId)))
                patch.Delete(doc, NameOfExisting(scene, doc));
            foreach (var want in wanted)
            {
                var doc = scene.Get(want.FileId);
                if (doc == null) patch.Add(want.Text, want.Name);
                else
                {
                    if (!owned.Contains(doc.FileId)) throw new Refusal("Village dressing id belongs to another object: " + want.Name);
                    patch.Edit(doc, want.Text, want.Name);
                }
            }
            return patch.Seal(scene);
        }
    }
}
#endif
