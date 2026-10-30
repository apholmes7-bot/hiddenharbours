#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Art;
using HiddenHarbours.World;

namespace HiddenHarbours.App.Editor
{
    /// <summary>Retire dry planting while preserving the shore plants that the sea draws through water.</summary>
    public static class StPetersPlantFieldPatch
    {
        public const string FieldRoot = "StPetersPlantField";
        public const string KeptShoreRoot = "ShorePlants";
        public static readonly string[] RetiredRoots = { "IslandGrass", "IslandShrubs", "IslandFlowers" };
        public sealed class Result
        {
            public string Text;
            public string[] DeletedObjects, AddedObjects;
            public long[] DeletedDocuments;
        }

        public static Result Apply(string beforeText, string additions, ISet<long> keptShoreObjects = null)
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
                if (keptShoreObjects != null)
                {
                    var keptRoot = before.RootNamed(KeptShoreRoot);
                    var members = before.TransformOf(keptRoot).FieldRefs("m_Children")
                        .Select(id => before.Require(id, "shore transform").FieldRef("m_GameObject"));
                    if (!keptShoreObjects.SetEquals(members))
                        throw new InvalidOperationException("The kept shore members changed after retirement");
                }
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
            StPetersLayerRefresh.Doc shoreTransform = null, shoreAfter = null;
            var shore = before.Docs.SingleOrDefault(d => d.ClassId == 1 && d.Field("m_Name") == KeptShoreRoot);
            if (shore != null)
            {
                if (keptShoreObjects == null || keptShoreObjects.Count == 0)
                    throw new InvalidOperationException("The wet shore planting must be named before retirement");
                shoreTransform = before.TransformOf(shore);
                var shoreIds = before.SubtreeOf(shore.FileId);
                var keep = new HashSet<long> { shore.FileId, shoreTransform.FileId };
                var keptTransforms = new HashSet<long>();
                foreach (long id in keptShoreObjects)
                {
                    var go = before.Require(id, "kept shore plant");
                    var tr = before.TransformOf(go);
                    if (!shoreIds.Contains(id) || tr.FieldRef("m_Father") != shoreTransform.FileId)
                        throw new InvalidOperationException("A kept shore plant is not a direct member of ShorePlants");
                    keep.UnionWith(before.SubtreeOf(id));
                    keptTransforms.Add(tr.FileId);
                }
                deleted.UnionWith(shoreIds.Where(id => !keep.Contains(id)));
                var lines = shoreTransform.Text.Split('\n').Where(line =>
                {
                    var child = Regex.Match(line, @"^  - \{fileID: (-?\d+)\}$");
                    return !child.Success || keptTransforms.Contains(long.Parse(child.Groups[1].Value));
                });
                shoreAfter = new StPetersLayerRefresh.Doc(string.Join("\n", lines));
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
                else if (shoreTransform != null && doc.FileId == shoreTransform.FileId) docs.Add(shoreAfter);
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
            foreach (var doc in before.Docs.Where(d => !deleted.Contains(d.FileId) && d.FileId != roots.FileId &&
                         (shoreTransform == null || d.FileId != shoreTransform.FileId)))
                if (after.Get(doc.FileId).Text != doc.Text) throw new InvalidOperationException("The plant patch changed an unrelated document");
            return new Result { Text = after.Render(), DeletedDocuments = deleted.OrderBy(x => x).ToArray(),
                DeletedObjects = ObjectPaths(before, deleted), AddedObjects = ObjectPaths(after, addedIds) };
        }

        public static Dictionary<long, Vector2> KeptStands(StPetersLayerRefresh.SceneYaml scene,
            PaintedHeightField height, float highWater)
        {
            var root = scene.TransformOf(scene.RootNamed(KeptShoreRoot));
            if (StPetersLayerRefresh.ParseVec3(root.Field("m_LocalPosition")) != Vector3.zero ||
                StPetersLayerRefresh.ParseVec3(root.Field("m_LocalScale")) != Vector3.one)
                throw new InvalidOperationException("The shore root must keep its ground frame");
            var kept = new Dictionary<long, Vector2>();
            foreach (long id in root.FieldRefs("m_Children"))
            {
                var tr = scene.Require(id, "shore member transform");
                Vector2 at = StPetersLayerRefresh.ParseVec3(tr.Field("m_LocalPosition"));
                if (height.ElevationAt(at) <= highWater) kept.Add(tr.FieldRef("m_GameObject"), at);
            }
            return kept;
        }

        public static Vector2Int FieldCell(PlantFieldDefinition definition, Vector2 at) =>
            new Vector2Int(Mathf.FloorToInt((at.x - definition.Origin.x) / definition.CellSize),
                           Mathf.FloorToInt((at.y - definition.Origin.y) / definition.CellSize));

        [Serializable] sealed class KeptMember { public long id; public string name; public Vector2 at; public float elevation; }
        [Serializable] sealed class Receipt
        {
            public float highWater;
            public KeptMember[] kept;
            public string[] deletedObjects, addedObjects, excludedAuthoredIds;
            public long[] deletedDocuments;
            public int authoredBefore, authoredAfter, wetAuthored, excludedGroundCells;
            public int sceneBytesBefore, sceneBytesAfter, keyRootIndex;
            public long[] rootsAfterKey;
        }

        // Batch caller: only the named scene and this field's own data can be written.
        public static void ReapplyKeepingWetShorePlants()
        {
            string Arg(string key)
            {
                var args = System.Environment.GetCommandLineArgs(); int at = Array.IndexOf(args, key);
                if (at < 0 || at + 1 >= args.Length) throw new InvalidOperationException("Missing " + key);
                return args[at + 1];
            }
            string original = File.ReadAllText(Arg("-plantPatchBase")).Replace("\r\n", "\n");
            string additions = File.ReadAllText(Arg("-plantPatchAdditions"));
            var before = StPetersLayerRefresh.SceneYaml.Parse(original);
            var region = AssetDatabase.LoadAssetAtPath<RegionDef>("Assets/_Project/Data/Regions/StPeters.asset");
            var ground = AssetDatabase.LoadAssetAtPath<PlantFieldGroundData>("Assets/_Project/Data/Plants5/StPetersPlantGround.asset");
            var definition = AssetDatabase.LoadAssetAtPath<PlantFieldDefinition>("Assets/_Project/Data/Plants5/StPetersPlantField.asset");
            float high = region.TideMeanLevel + Mathf.Abs(region.TideAmplitude);
            var kept = KeptStands(before, ground.HeightMap.Field, high);
            var patch = Apply(original, additions, new HashSet<long>(kept.Keys));
            if (Apply(patch.Text, additions, new HashSet<long>(kept.Keys)).Text != patch.Text)
                throw new InvalidOperationException("The wet-shore patch is not idempotent");
            var current = StPetersLayerRefresh.SceneYaml.Parse(File.ReadAllText(StPetersLayerRefresh.ScenePath));
            var after = StPetersLayerRefresh.SceneYaml.Parse(patch.Text);
            foreach (var doc in current.Docs.Where(d => d.ClassId != 1660057539))
                if (after.Get(doc.FileId)?.Text != doc.Text)
                    throw new InvalidOperationException("The patch changed an existing field-scene document");
            var cells = new HashSet<Vector2Int>(kept.Values.Select(at => FieldCell(definition, at)));
            var excluded = definition.Authored.Where(row => cells.Contains(FieldCell(definition, row.At))).ToArray();
            int authoredBefore = definition.Authored.Length;
            int wetAuthored = definition.Authored.Count(row => ground.HeightMap.Field.ElevationAt(row.At) <= high);
            byte[] map = ground.Decode(); int changed = 0;
            foreach (var cell in cells)
            {
                if (cell.x < 0 || cell.y < 0 || cell.x >= ground.Width || cell.y >= ground.Height)
                    throw new InvalidOperationException("A kept shore plant is outside the field grid");
                int at = cell.y * ground.Width + cell.x;
                if (map[at] != 255) { map[at] = 255; changed++; }
            }
            if (excluded.Length > 0)
            {
                var ids = new HashSet<string>(excluded.Select(row => row.Id));
                definition.Authored = definition.Authored.Where(row => !ids.Contains(row.Id)).ToArray();
                EditorUtility.SetDirty(definition); AssetDatabase.SaveAssetIfDirty(definition);
            }
            if (changed > 0)
            {
                using (var stream = new MemoryStream())
                {
                    for (int start = 0; start < map.Length;)
                    {
                        int end = start + 1;
                        while (end < map.Length && end - start < ushort.MaxValue && map[end] == map[start]) end++;
                        int length = end - start; stream.WriteByte((byte)length); stream.WriteByte((byte)(length >> 8));
                        stream.WriteByte(map[start]); start = end;
                    }
                    ground.Payload = Convert.ToBase64String(stream.ToArray());
                }
                EditorUtility.SetDirty(ground); AssetDatabase.SaveAssetIfDirty(ground);
            }
            int priorBytes = File.ReadAllBytes(StPetersLayerRefresh.ScenePath).Length;
            File.WriteAllText(StPetersLayerRefresh.ScenePath, patch.Text, new UTF8Encoding(false));
            var roots = after.Docs.Single(d => d.ClassId == 1660057539).FieldRefs("m_Roots");
            long key = after.TransformOf(after.RootNamed(StPetersLayerRefresh.KeyScenesRootName)).FileId;
            int index = roots.IndexOf(key);
            var receipt = new Receipt { highWater = high,
                kept = kept.Select(p => new KeptMember { id = p.Key, name = before.Require(p.Key, "kept plant").Field("m_Name"), at = p.Value, elevation = ground.HeightMap.Field.ElevationAt(p.Value) }).ToArray(),
                deletedObjects = patch.DeletedObjects, addedObjects = patch.AddedObjects, deletedDocuments = patch.DeletedDocuments,
                authoredBefore = authoredBefore, authoredAfter = definition.Authored.Length, wetAuthored = wetAuthored,
                excludedAuthoredIds = excluded.Select(row => row.Id).ToArray(), excludedGroundCells = changed,
                sceneBytesBefore = priorBytes, sceneBytesAfter = Encoding.UTF8.GetByteCount(patch.Text), keyRootIndex = index,
                rootsAfterKey = roots.Skip(index + 1).ToArray() };
            File.WriteAllText(Arg("-plantPatchReport"), JsonUtility.ToJson(receipt, true), new UTF8Encoding(false));
            Debug.Log("Kept wet shore plants: " + kept.Count + "; authored overlaps removed: " + excluded.Length);
            AssetDatabase.ImportAsset(StPetersLayerRefresh.ScenePath, ImportAssetOptions.ForceUpdate);
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
