#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using HiddenHarbours.Core;
using HiddenHarbours.Environment;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// <b>The seals.</b> One small host object, <see cref="SealsRootName"/>, carrying a <see cref="SealHaulout"/>
    /// that points at the Whelps haul-out (<c>haulout.st_peters.whelps</c>). Everything else (the seals, their
    /// renderers, who is out at this tide) the host makes at runtime from the data, so the scene gains three
    /// documents and never a seal.
    /// </summary>
    public static partial class StPetersLayerRefresh
    {
        public const string SealsStep = "Seals";
        public const string SealsRootName = "StPetersSeals";
        public const string WhelpsHauloutPath = "Assets/_Project/Data/Wildlife/SealHaulout_st_peters_whelps.asset";

        /// <summary>The patch that stands the haul-out host under <see cref="SealsRootName"/>.</summary>
        public static LayerPatch Seals(SceneYaml scene, ScriptRef hostScript, ObjRef haulout)
        {
            const string rootName = SealsRootName;
            var root = scene.Docs.Any(d => d.ClassId == GameObjectClass && d.Field("m_Name") == rootName)
                ? scene.RootNamed(rootName) : null;
            var owned = root == null ? new HashSet<long>() : scene.SubtreeOf(root.FileId);
            var ids = new IdAllocator(scene, owned);
            long Next(string name, string kind) => ids.Next(rootName + "/" + name + "/" + kind);
            long rootGo = Next("", "GameObject"), rootTr = Next("", "Transform"), host = Next("", "SealHaulout");
            if (root != null && root.FileId != rootGo) throw new Refusal($"{SealsStep}: the root has unexpected ids.");

            var wanted = new List<Wanted>
            {
                Want(GameObjectYaml(rootGo, rootName, new[] { rootTr, host }), rootName, "GameObject"),
                Want(TransformYaml(rootTr, rootGo, Vector2.zero, new List<long>(), 0), rootName, "Transform"),
                Want(MonoBehaviourYaml(host, rootGo, hostScript, new[] { "  _haulout: " + haulout.Yaml }), rootName, "SealHaulout"),
            };
            var patch = new LayerPatch(SealsStep, rootName, rootGo);
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

        [MenuItem("Hidden Harbours/World/St Peters Layer Refresh/Write the Seals Patch (dry run)")]
        static void WriteSealsMenu() { try { RunSeals(false); } catch (Refusal r) { Debug.LogError(r.Message); } }

        /// <summary>The apply for <c>-executeMethod</c>.</summary>
        public static void ApplySealsBatch() => RunSeals(apply: true);

        public static LayerPatch RunSeals(bool apply)
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (apply && string.Equals(SceneManager.GetSceneAt(i).path, ScenePath, StringComparison.Ordinal))
                    throw new Refusal("StPeters is open in the editor. Close it first: an open scene would overwrite the file on its next save.");

            var def = AssetDatabase.LoadAssetAtPath<SealHauloutDef>(WhelpsHauloutPath)
                ?? throw new Refusal($"{SealsStep}: {WhelpsHauloutPath} is not an imported haul-out.");
            if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(def, out string guid, out long local))
                throw new Refusal($"{SealsStep}: the haul-out has no guid.");
            ScriptRef script = new EditorVillageLightAssets().ScriptFor(typeof(SealHaulout));

            string text = File.ReadAllText(ScenePath);
            LayerPatch patch = Seals(SceneYaml.Parse(text), script, new ObjRef(local, guid, 2));
            Directory.CreateDirectory(PatchFolder);
            var utf8 = new UTF8Encoding(false);
            File.WriteAllText(Path.Combine(PatchFolder, patch.Step + ".patch.yaml"), patch.ToYaml(), utf8);
            string after = patch.ApplyTo(text);
            if (apply && after != text) File.WriteAllText(ScenePath, after, utf8);
            Debug.Log($"[StPetersLayerRefresh] {(apply ? "applied to " + ScenePath : "dry run, nothing written to the scene")} " +
                      $"(patch in {PatchFolder}):\n{patch.Summary()}");
            return patch;
        }
    }
}
#endif
