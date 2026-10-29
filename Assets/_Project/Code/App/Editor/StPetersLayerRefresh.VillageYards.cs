#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using HiddenHarbours.Art;
using HiddenHarbours.Art.Editor;
using HiddenHarbours.Core;
using HiddenHarbours.World;
using UnityEngine;

namespace HiddenHarbours.App.Editor
{
    public static partial class StPetersLayerRefresh
    {
        public sealed class VillageYard
        {
            public Yard Yard;
            public YardDressing.BoundaryPlan Plan;
        }

        public interface IVillageBoundaryAssets
        {
            SpriteRef SpriteFor(YardDressing.PlannedPiece piece);
            ObjRef Material { get; }
            ScriptRef ScriptFor(Type type);
        }

        public sealed class EditorVillageBoundaryAssets : IVillageBoundaryAssets
        {
            readonly YardKit.Contract _kit = YardKit.Load();
            readonly EditorVillageLightAssets _common = new EditorVillageLightAssets();
            public ObjRef Material => _common.Material;
            public ScriptRef ScriptFor(Type type) => _common.ScriptFor(type);
            public SpriteRef SpriteFor(YardDressing.PlannedPiece piece)
            {
                var sprite = YardCatalog.LoadFacing(YardCatalog.Find(_kit, piece.Key), piece.Facing);
                if (sprite == null) throw new Refusal("Missing yard boundary cell: " + piece.Key + "/" + piece.Facing);
                return EditorShoreRockSprites.RefOf(sprite);
            }
        }

        public static VillageYard[] ReadVillageYards()
        {
            var kit = YardKit.Load();
            if (!YardCatalog.HasMeasuredBearings(kit)) throw new Refusal("Yard boundary bearings are not baked.");
            return StPetersYards.Yards.Where(y => StPetersVillageYards.TryKey(y.Name, out _))
                .Select(y => new VillageYard { Yard = y, Plan = YardDressing.PlanOne(y, kit) }).ToArray();
        }

        public static LayerPatch VillageYardBoundaries(SceneYaml scene, IReadOnlyList<VillageYard> rows, IVillageBoundaryAssets assets)
        {
            if (!rows.Select(r => r.Yard.Name).OrderBy(s => s, StringComparer.Ordinal).SequenceEqual(
                new[] { "GeneralStore", "HarbourGable", "SageCottage", "School", "WhiteGable" }))
                throw new Refusal("Boundary refresh needs exactly the five active yards.");
            var root = scene.RootNamed(YardDressing.RootName);
            var rootTr = scene.TransformOf(root);
            RequireIdentity(rootTr, YardDressing.RootName);
            var owned = new HashSet<long>();
            var keep = rootTr.FieldRefs("m_Children");
            foreach (var row in rows)
            {
                if (row.Plan.YardName != row.Yard.Name || row.Plan.Fence != row.Yard.Fence ||
                    row.Plan.Fenced != (row.Yard.Fence != YardFence.None) || row.Plan.MissingPieces.Count != 0)
                    throw new Refusal("Incomplete yard boundary plan: " + row.Yard.Name);
                var children = rootTr.FieldRefs("m_Children").Select(scene.Get).Select(t => scene.Get(t.FieldRef("m_GameObject")))
                    .Where(g => g.Field("m_Name") == row.Yard.Name).ToArray();
                if (children.Length > 1) throw new Refusal("Duplicate yard boundary group: " + row.Yard.Name);
                foreach (var child in children)
                {
                    owned.UnionWith(scene.SubtreeOf(child.FileId));
                    keep.Remove(scene.TransformOf(child).FileId);
                }
            }
            var ids = new IdAllocator(scene, owned);
            var wanted = new List<Wanted>();
            foreach (var row in rows.OrderBy(r => r.Yard.Name, StringComparer.Ordinal))
            {
                if (!row.Plan.Fenced) continue;
                string name = row.Yard.Name;
                long Next(string part, string type) => ids.Next("VillageYards/" + name + "/" + part + "/" + type);
                long go = Next("", "GameObject"), tr = Next("", "Transform");
                var children = new List<long>();
                int index = 0;
                foreach (var piece in row.Plan.Pieces)
                {
                    string part = piece.Key + "_" + (index++).ToString("000", System.Globalization.CultureInfo.InvariantCulture);
                    var sprite = assets.SpriteFor(piece);
                    if (sprite.Sprite.FileId == 0 || string.IsNullOrEmpty(sprite.Sprite.Guid)) throw new Refusal("Missing yard sprite: " + part);
                    long pg = Next(part, "GameObject"), pt = Next(part, "Transform"), sr = Next(part, "SpriteRenderer"), sort = Next(part, "YSortSprite");
                    children.Add(pt);
                    wanted.Add(Want(GameObjectYaml(pg, part, new[] { pt, sr, sort }), name + "/" + part, "GameObject"));
                    wanted.Add(Want(TransformYaml(pt, pg, piece.Position, new List<long>(), tr), name + "/" + part, "Transform"));
                    int order = YSortSprite.OrderFor(piece.Position.y, SortingBands.DecorBase, SortingBands.OrdersPerMetre, SortingBands.DecorFloor, SortingBands.DecorCeiling);
                    wanted.Add(Want(SpriteRendererYaml(sr, pg, assets.Material, sprite, order), name + "/" + part, "SpriteRenderer"));
                    wanted.Add(Want(MonoBehaviourYaml(sort, pg, assets.ScriptFor(typeof(YSortSprite)), new[]
                    {
                        "  _dynamic: 0", "  _baseOrder: " + F(SortingBands.DecorBase), "  _orderPerUnit: " + F(SortingBands.OrdersPerMetre),
                        "  _minOrder: " + Int(SortingBands.DecorFloor), "  _maxOrder: " + Int(SortingBands.DecorCeiling), "  _pivotYOffset: 0",
                    }), name + "/" + part, "YSortSprite"));
                }
                long bg = Next("Boundary", "GameObject"), bt = Next("Boundary", "Transform"), bc = Next("Boundary", "PropertyBoundary");
                long wg = Next("Walls", "GameObject"), wt = Next("Walls", "Transform");
                Vector2 centre = row.Yard.Polygon.Centre;
                var localRuns = row.Plan.Runs.Where(r => r != null && r.Length >= 2).Select(r => r.Select(p => p - centre).ToArray()).ToArray();
                var fields = new List<string> { localRuns.Length == 0 ? "  _runs: []" : "  _runs:" };
                var colliders = new List<long>();
                foreach (var run in localRuns)
                {
                    fields.Add("  - Points:");
                    foreach (var p in run) fields.Add("    - " + Vec2Yaml(p));
                    for (int i = 0; i + 1 < run.Length; i++)
                    {
                        Vector2 along = run[i + 1] - run[i];
                        float length = along.magnitude;
                        if (length <= .0001f) continue;
                        along /= length;
                        float half = YardPlan.FenceThicknessMetres * .5f;
                        Vector2 side = new Vector2(-along.y, along.x) * half, ext = along * half;
                        var quad = new[] { run[i] - ext + side, run[i + 1] + ext + side, run[i + 1] + ext - side, run[i] - ext - side };
                        long id = Next("Walls/" + colliders.Count, "PolygonCollider2D");
                        colliders.Add(id);
                        wanted.Add(Want(PolygonColliderYaml(id, wg, new[] { quad }), name + "/Walls/" + colliders.Count, "PolygonCollider2D"));
                    }
                }
                fields.Add("  _thicknessMetres: " + F(YardPlan.FenceThicknessMetres));
                string reason = name + ": " + row.Yard.Fence + " fence \u2014 " + row.Yard.Reason;
                fields.Add("  _reason: \"" + reason.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t") + "\"");
                children.Add(bt);
                wanted.Add(Want(GameObjectYaml(bg, "Boundary", new[] { bt, bc }), name + "/Boundary", "GameObject"));
                wanted.Add(Want(TransformYaml(bt, bg, centre, localRuns.Length == 0 ? new List<long>() : new List<long> { wt }, tr), name + "/Boundary", "Transform"));
                wanted.Add(Want(MonoBehaviourYaml(bc, bg, assets.ScriptFor(typeof(PropertyBoundary)), fields), name + "/Boundary", "PropertyBoundary"));
                if (localRuns.Length != 0)
                {
                    wanted.Add(Want(GameObjectYaml(wg, "Walls", new[] { wt }.Concat(colliders).ToArray()), name + "/Walls", "GameObject"));
                    wanted.Add(Want(TransformYaml(wt, wg, Vector2.zero, new List<long>(), bt), name + "/Walls", "Transform"));
                }
                wanted.Add(Want(GameObjectYaml(go, name, new[] { tr }), name, "GameObject"));
                wanted.Add(Want(TransformYaml(tr, go, Vector2.zero, children, rootTr.FileId), name, "Transform"));
                keep.Add(tr);
            }
            var patch = new LayerPatch("VillageYards", YardDressing.RootName, root.FileId);
            patch.Edit(rootTr, SetRefList(rootTr.Text, "m_Children", keep), "Yards active boundaries");
            var wantedIds = new HashSet<long>(wanted.Select(w => w.FileId));
            foreach (var doc in scene.Docs.Where(d => owned.Contains(d.FileId) && !wantedIds.Contains(d.FileId)))
                patch.Delete(doc, NameOfExisting(scene, doc));
            foreach (var want in wanted)
            {
                var doc = scene.Get(want.FileId);
                if (doc == null) patch.Add(want.Text, want.Name);
                else if (owned.Contains(doc.FileId)) patch.Edit(doc, want.Text, want.Name);
                else throw new Refusal("Yard boundary id belongs to another object: " + want.Name);
            }
            return patch.Seal(scene);
        }
    }
}
#endif
