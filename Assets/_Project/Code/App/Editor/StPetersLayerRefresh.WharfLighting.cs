#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using HiddenHarbours.Art;
using HiddenHarbours.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// <b>The St Peters pier lanterns' strength and pool, from the wharf lighting record</b>
    /// (<c>lighting.stp_wharf</c>, <see cref="WharfLightingPath"/>). The landing-dock step writes the lanterns
    /// (<c>DockLamps</c>) and reads the record; this applies just that step's lamp documents, so the record can be
    /// retuned without replaying the rest of the landing (the north pocket's ground write in particular).
    /// </summary>
    public static partial class StPetersLayerRefresh
    {
        public const string WharfLightingPath = "Assets/_Project/Data/Regions/StPeters/WharfLighting.asset";
        const string WharfLightingStep = "stp-wharf-lighting";

        /// <summary>The St Peters wharf's lighting record.</summary>
        public static WharfLightingDef WharfLighting() => AssetDatabase.LoadAssetAtPath<WharfLightingDef>(WharfLightingPath)
            ?? throw new Refusal(WharfLightingStep + ": no wharf lighting record at " + WharfLightingPath + ".");

        /// <summary>The landing-dock patch, refused unless every change it makes is an edit to a document under the
        /// pier's own <c>Lamps</c> root. Anything else means the landing has drifted from its plan, which is not this
        /// step's to settle.</summary>
        public static LayerPatch PlanWharfLighting(SceneYaml scene)
        {
            LayerPatch patch = LandingDock(scene);
            Doc root = scene.RootNamed(StPetersWharf.RootName);
            Doc lamps = scene.ChildNamed(scene.TransformOf(root), "Lamps");
            var owned = scene.SubtreeOf(lamps.FileId);
            foreach (var op in patch.Ops)
                if (op.Kind != OpKind.Edit || !owned.Contains(op.FileId))
                    throw new Refusal(WharfLightingStep + ": the landing-dock plan changes more than the pier lamps: " +
                                      op.Kind + " " + op.Name);
            return patch;
        }

        /// <summary>
        /// The record's harbour marks (<see cref="WharfLightingDef.HarbourMarkIds"/>) wear its marker look: each mark's
        /// prefab instance carries one override, its <see cref="NavLight"/>'s look, pointing at the record's
        /// <see cref="NavLightLookDef"/>. Every other mark keeps the coast-wide preset. Pure text in, text out;
        /// <paramref name="changed"/> counts the marks whose override was added or retargeted (0 = the scene already
        /// matches the record).
        /// </summary>
        public static string WithHarbourMarkerLooks(string text, out int changed)
        {
            WharfLightingDef rec = WharfLighting();
            changed = 0;
            string[] ids = rec.HarbourMarkIds ?? new string[0];
            if (ids.Length == 0) return text;
            var look = rec.HarbourMarkerLook as NavLightLookDef;
            if (look == null)
                throw new Refusal(WharfLightingStep + ": the record names harbour marks but its marker look is not a nav light look.");
            string lookGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(look));
            string prefabGuid = AssetDatabase.AssetPathToGUID(StPetersNavMarks.PrefabPath);
            long navLight = NavLightComponentId();
            string target = "    - target: {fileID: " + navLight + ", guid: " + prefabGuid + ", type: 3}\n";
            string wanted = target + "      propertyPath: _look\n      value: \n" +
                            "      objectReference: {fileID: 11400000, guid: " + lookGuid + ", type: 2}\n";
            text = text.Replace("\r\n", "\n");
            foreach (string id in ids)
            {
                string key = "      propertyPath: _markId\n      value: " + id + "\n";
                int at = text.IndexOf(key, StringComparison.Ordinal);
                if (at < 0 || text.IndexOf(key, at + key.Length, StringComparison.Ordinal) >= 0)
                    throw new Refusal(WharfLightingStep + ": harbour mark " + id + " is not on exactly one nav mark.");
                int start = text.LastIndexOf("\n--- !u!1001 &", at, StringComparison.Ordinal);
                int end = text.IndexOf("\n--- ", at, StringComparison.Ordinal);
                if (start < 0) throw new Refusal(WharfLightingStep + ": harbour mark " + id + " is not a prefab instance.");
                if (end < 0) end = text.Length;
                string doc = text.Substring(start, end - start);
                if (doc.IndexOf("m_SourcePrefab: {fileID: 100100000, guid: " + prefabGuid, StringComparison.Ordinal) < 0)
                    throw new Refusal(WharfLightingStep + ": harbour mark " + id + " is not a NavBuoy instance.");
                string fixedDoc;
                int old = doc.IndexOf(target + "      propertyPath: _look\n", StringComparison.Ordinal);
                if (old >= 0)
                {
                    int oldEnd = doc.IndexOf("\n", doc.IndexOf("objectReference:", old, StringComparison.Ordinal), StringComparison.Ordinal) + 1;
                    if (doc.Substring(old, oldEnd - old) == wanted) continue;
                    fixedDoc = doc.Substring(0, old) + wanted + doc.Substring(oldEnd);
                }
                else
                {
                    int removed = doc.IndexOf("    m_RemovedComponents:", StringComparison.Ordinal);
                    if (removed < 0) throw new Refusal(WharfLightingStep + ": harbour mark " + id + " has no modification list.");
                    fixedDoc = doc.Substring(0, removed) + wanted + doc.Substring(removed);
                }
                text = text.Substring(0, start) + fixedDoc + text.Substring(end);
                changed++;
            }
            return text;
        }

        /// <summary>The NavLight component's file id inside the NavBuoy prefab, read off the prefab rather than typed.</summary>
        static long NavLightComponentId()
        {
            string navLightGuid = NavLightScriptGuid();
            string prefab = File.ReadAllText(StPetersNavMarks.PrefabPath).Replace("\r\n", "\n");
            var m = System.Text.RegularExpressions.Regex.Match(prefab,
                @"--- !u!114 &(-?\d+)\n(?:(?!--- ).*\n)*?  m_Script: \{fileID: 11500000, guid: " + navLightGuid);
            if (!m.Success) throw new Refusal(WharfLightingStep + ": the NavBuoy prefab carries no NavLight.");
            return long.Parse(m.Groups[1].Value);
        }

        static string NavLightScriptGuid()
        {
            foreach (string guid in AssetDatabase.FindAssets("NavLight t:MonoScript"))
            {
                var ms = AssetDatabase.LoadAssetAtPath<MonoScript>(AssetDatabase.GUIDToAssetPath(guid));
                if (ms != null && ms.GetClass() == typeof(NavLight)) return guid;
            }
            throw new Refusal(WharfLightingStep + ": no NavLight script.");
        }

        /// <summary>The batch entry: applies the lamp edits, then plans again on what it wrote, which must be empty.
        /// Exits 1 on any refusal, so a half-written scene never reports success.</summary>
        public static void WharfLightingApplyCli()
        {
            try
            {
                for (int i = 0; i < SceneManager.sceneCount; i++)
                    if (SceneManager.GetSceneAt(i).path == ScenePath)
                        throw new Refusal("StPeters is open. Close it before applying text patches.");
                string before = File.ReadAllText(ScenePath);
                LayerPatch patch = PlanWharfLighting(SceneYaml.Parse(before));
                Debug.Log("[" + WharfLightingStep + "] " + patch.Summary());
                string after = patch.ApplyTo(before);
                after = WithHarbourMarkerLooks(after, out int marks);
                Debug.Log("[" + WharfLightingStep + "] harbour marks re-dressed: " + marks);
                if (after != before) File.WriteAllText(ScenePath, after, new UTF8Encoding(false));
                if (!PlanWharfLighting(SceneYaml.Parse(after)).IsEmpty)
                    throw new Refusal(WharfLightingStep + ": a second plan is not empty.");
                WithHarbourMarkerLooks(after, out int again);
                if (again != 0) throw new Refusal(WharfLightingStep + ": a second marker pass is not empty.");
                Debug.Log("[" + WharfLightingStep + "] APPLIED; a second plan is empty.");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }
    }
}
#endif
