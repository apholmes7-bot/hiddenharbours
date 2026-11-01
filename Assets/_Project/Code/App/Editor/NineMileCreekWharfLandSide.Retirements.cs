#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using static HiddenHarbours.App.Editor.StPetersLayerRefresh;

namespace HiddenHarbours.App.Editor
{
    public static partial class NineMileCreekWharfLandSide
    {
        public static LayerPatch RetireShedLots(SceneYaml scene) => RemoveChildren(scene, NineMileCreekLots.RootName,
            name => name.StartsWith("Lot_Shed_", StringComparison.Ordinal) || name.StartsWith("Lot_BaitShed_", StringComparison.Ordinal), 10);
        public static LayerPatch RetireShopSites(SceneYaml scene) => RemoveChildren(scene, NineMileCreekShops.RootName,
            name => name == "fishMarket" || name == "restaurant", 2);

        static LayerPatch RemoveChildren(SceneYaml scene, string rootName, Func<string, bool> selected, int expected)
        {
            Doc root = scene.RootNamed(rootName);
            HashSet<long> mine = scene.SubtreeOf(root.FileId);
            var children = scene.Docs.Where(d => mine.Contains(d.FileId) && d.ClassId == 1 && !d.Stripped && selected(d.Field("m_Name"))).ToArray();
            if (children.Length != 0 && children.Length != expected)
                throw new Refusal(PlacementStep + ": " + rootName + " has an unexpected retirement count: " + children.Length);
            var patch = new LayerPatch(PlacementStep, rootName, root.FileId);
            var deleted = new HashSet<long>();
            var parents = new Dictionary<long, List<long>>();
            foreach (Doc child in children)
            {
                Doc transform = scene.TransformOf(child);
                long parent = transform.FieldRef("m_Father");
                if (!parents.TryGetValue(parent, out List<long> refs)) parents.Add(parent, refs = new List<long>());
                refs.Add(transform.FileId);
                foreach (long id in scene.SubtreeOf(child.FileId)) deleted.Add(id);
            }
            foreach (long id in deleted.OrderBy(id => id))
            {
                Doc doc = scene.Require(id, rootName);
                string objectName = doc.ClassId == 1 ? doc.Field("m_Name") :
                    doc.FieldRef("m_GameObject") != 0 ? scene.Require(doc.FieldRef("m_GameObject"), rootName).Field("m_Name") : "prefab";
                patch.Delete(doc, rootName + "/" + objectName + "/&" + id + " class " + doc.ClassId);
            }
            foreach (var parent in parents)
            {
                if (deleted.Contains(parent.Key)) continue;
                Doc transform = scene.Require(parent.Key, rootName + " parent");
                long[] keep = transform.FieldRefs("m_Children").Where(id => !parent.Value.Contains(id)).ToArray();
                string value = keep.Length == 0 ? "  m_Children: []" : "  m_Children:\n" + string.Join("\n", keep.Select(id => "  - {fileID: " + id + "}"));
                string text = Regex.Replace(transform.Text, @"(?m)^  m_Children:(?: \[\])?(?:\n  - [^\n]*)*", _ => value);
                patch.Edit(transform, text, rootName + "/children after named retirements");
            }
            return patch.Seal(scene);
        }
    }
}
#endif
