#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace HiddenHarbours.App.Editor
{
    /// <summary>A named, byte-preserving retirement of four roots and addition of one field root.</summary>
    public static class StPetersPlantFieldPatch
    {
        public const string FieldRoot = "StPetersPlantField";
        public static readonly string[] RetiredRoots = { "IslandGrass", "ShorePlants", "IslandShrubs", "IslandFlowers" };
        public sealed class Result
        {
            public string Text;
            public string[] DeletedObjects, AddedObjects;
            public long[] DeletedDocuments;
        }

        public static Result Apply(string beforeText, string additions)
        {
            var before = StPetersLayerRefresh.SceneYaml.Parse(beforeText);
            var added = StPetersLayerRefresh.SceneYaml.Parse("%YAML 1.1\n" + additions);
            var root = added.RootNamed(FieldRoot);
            long transform = added.TransformOf(root).FileId;
            var addedIds = new HashSet<long>(added.Docs.Select(d => d.FileId));
            if (!added.SubtreeOf(root.FileId).SetEquals(addedIds))
                throw new InvalidOperationException("All added documents must belong to the plant field");
            var roots = before.Docs.Single(d => d.ClassId == 1660057539);
            var names = before.Docs.Where(d => d.ClassId == 1).Select(d => d.Field("m_Name")).ToArray();
            if (names.Contains(FieldRoot))
            {
                if (RetiredRoots.Any(names.Contains) || added.Docs.Any(d => before.Get(d.FileId)?.Text != d.Text))
                    throw new InvalidOperationException("The plant field patch has conflicting input");
                return new Result { Text = beforeText, DeletedObjects = Array.Empty<string>(),
                    AddedObjects = Array.Empty<string>(), DeletedDocuments = Array.Empty<long>() };
            }
            var deleted = new HashSet<long>(); var removedTransforms = new HashSet<long>();
            foreach (string name in RetiredRoots)
            {
                var old = before.RootNamed(name);
                deleted.UnionWith(before.SubtreeOf(old.FileId));
                removedTransforms.Add(before.TransformOf(old).FileId);
            }
            if (addedIds.Any(before.Contains)) throw new InvalidOperationException("A plant field fileID already exists");
            var rootLines = roots.Text.Split('\n').ToList();
            rootLines.RemoveAll(line => removedTransforms.Any(id => line == "  - {fileID: " + id + "}"));
            rootLines.Add("  - {fileID: " + transform + "}");
            var rootAfter = new StPetersLayerRefresh.Doc(string.Join("\n", rootLines));
            var docs = new List<StPetersLayerRefresh.Doc>();
            foreach (var doc in before.Docs)
            {
                if (deleted.Contains(doc.FileId)) continue;
                if (doc.FileId == roots.FileId) { docs.AddRange(added.Docs); docs.Add(rootAfter); }
                else docs.Add(doc);
            }
            var after = new StPetersLayerRefresh.SceneYaml(before.Preamble, docs);
            var local = new Regex(@"\{fileID: (-?\d+)\}");
            foreach (var doc in after.Docs)
                foreach (Match reference in local.Matches(doc.Text))
                {
                    long id = long.Parse(reference.Groups[1].Value);
                    if (deleted.Contains(id) || (addedIds.Contains(doc.FileId) && id != 0 && !after.Contains(id)))
                        throw new InvalidOperationException("A plant patch reference has no target");
                }
            foreach (var doc in before.Docs.Where(d => !deleted.Contains(d.FileId) && d.FileId != roots.FileId))
                if (after.Get(doc.FileId).Text != doc.Text) throw new InvalidOperationException("The plant patch changed an unrelated document");
            return new Result { Text = after.Render(), DeletedDocuments = deleted.OrderBy(x => x).ToArray(),
                DeletedObjects = ObjectPaths(before, deleted), AddedObjects = ObjectPaths(after, addedIds) };
        }

        static string[] ObjectPaths(StPetersLayerRefresh.SceneYaml scene, HashSet<long> ids)
        {
            string PathOf(StPetersLayerRefresh.Doc go)
            {
                var tr = scene.TransformOf(go); long parent = tr.FieldRef("m_Father");
                return parent == 0 ? go.Field("m_Name") :
                    PathOf(scene.Require(scene.Require(parent, "plant parent").FieldRef("m_GameObject"), "plant parent object")) + "/" + go.Field("m_Name");
            }
            return scene.Docs.Where(d => ids.Contains(d.FileId) && d.ClassId == 1).Select(PathOf).OrderBy(x => x).ToArray();
        }
    }
}
#endif
