#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using static HiddenHarbours.App.Editor.StPetersLayerRefresh;

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// NR1a: the creek's dev core one level below an active envelope root, as Rice Point's stands
    /// (<see cref="LandSceneTemplate"/>). Travel wakes a destination's roots before their Start runs
    /// (<c>RegionTravelCoordinator</c>), so a core baked as an inactive ROOT woke on every arrival: its GameRoot set
    /// the services over the persistent core's, <see cref="DevRegionBootstrap"/> then destroyed it, and its OnDestroy
    /// cleared them, so the next region seeded a second core. One level below an active envelope, waking the roots
    /// never reaches it, and the bootstrap's choice stands.
    ///
    /// <para><see cref="NineMileCreekBuilder"/> builds the envelope; <see cref="Plan"/> is step 13, the same move as a
    /// text patch to the committed scene (N1's rule: the baked scene changes by named steps, never by a rebuild). It
    /// adds the envelope's GameObject and Transform, hangs the core's Transform under it, and lists the envelope among
    /// the scene's roots where the core stood. <see cref="LayerPatch.Seal"/> holds a patch to one root, and a root
    /// that moves under a new one is two, so the step keeps its own gate: those four documents, each edit one line,
    /// and every other document as it was, where it was.</para>
    /// </summary>
    public static class NineMileCreekDevCoreEnvelope
    {
        public const string Step = "13-dev-core-envelope";
        public const string EnvelopeName = "DevCoreEnvelope";
        public const string CoreName = "DevCore";

        /// <summary>A Transform's field naming the Transform it hangs under (<c>{fileID: 0}</c> at the top).</summary>
        public const string ParentKey = "m_Father";

        const int GameObjectClass = 1, TransformClass = 4;
        const string Unlinked = "  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n" +
                                "  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}";
        static readonly Regex LocalRef = new Regex(@"\{fileID: (-?\d+)\}", RegexOptions.CultureInvariant);

        /// <summary>Step 13 on <paramref name="sceneText"/>: the patch, and in <paramref name="after"/> the scene it
        /// writes. On a scene whose core already hangs under the envelope the patch is empty, and <paramref name="after"/>
        /// is <paramref name="sceneText"/>.</summary>
        public static LayerPatch Plan(string sceneText, out string after)
        {
            SceneYaml scene = SceneYaml.Parse(sceneText);
            Doc core = CoreOf(scene), coreTr = scene.TransformOf(core);
            RequireIdentity(coreTr, CoreName);
            after = sceneText;
            long above = coreTr.FieldRef(ParentKey);
            if (above != 0) return new LayerPatch(Step, EnvelopeName, EnvelopeOf(scene, coreTr, above).FileId);
            if (scene.Docs.Any(d => d.ClassId == GameObjectClass && !d.Stripped && d.Field("m_Name") == EnvelopeName))
                throw new Refusal($"{Step}: the scene holds a '{EnvelopeName}' that '{CoreName}' does not hang under; it was changed by hand.");

            var ids = new IdAllocator(scene);
            long go = ids.Next($"{Step}/{EnvelopeName}/GameObject"), tr = ids.Next($"{Step}/{EnvelopeName}/Transform");
            Doc roots = SceneRootsOf(scene);
            var patch = new LayerPatch(Step, EnvelopeName, go);
            patch.Add(GameObjectYaml(go, tr), EnvelopeName + " (GameObject)");
            patch.Add(TransformYaml(tr, go, coreTr.FileId), EnvelopeName + " (Transform)");
            patch.Edit(coreTr, OneLine(coreTr, $"  {ParentKey}: {Ref(0)}", $"  {ParentKey}: {Ref(tr)}"), CoreName + " (Transform)");
            patch.Edit(roots, OneLine(roots, "  - " + Ref(coreTr.FileId), "  - " + Ref(tr)), "SceneRoots");
            patch.Note(EnvelopeName, "added: an active root at the origin, the core its one child");
            patch.Note(CoreName, "hangs under the envelope, inactive as it was");
            patch.Note("SceneRoots", "lists the envelope where the core stood");

            SceneYaml next = patch.Apply(scene);
            Gate(scene, next, patch);
            EnvelopeOf(next, next.Require(coreTr.FileId, CoreName), tr);
            after = next.Render();
            return patch;
        }

        /// <summary>The core the scene's one dev bootstrap wakes: a GameObject of the builder's name, baked inactive.</summary>
        static Doc CoreOf(SceneYaml scene)
        {
            List<Doc> bootstraps = scene.Docs.Where(d => d.IsScript(typeof(DevRegionBootstrap).FullName)).ToList();
            if (bootstraps.Count != 1) throw new Refusal($"{Step}: the scene holds {bootstraps.Count} dev bootstraps; the step needs one.");
            Doc core = scene.Require(bootstraps[0].FieldRef("_devCoreRoot"), "the bootstrap's core");
            if (core.ClassId != GameObjectClass || core.Stripped || core.Field("m_Name") != CoreName)
                throw new Refusal($"{Step}: the bootstrap's core (&{core.FileId}) is not a GameObject named '{CoreName}'.");
            if (core.Field("m_IsActive") != "0")
                throw new Refusal($"{Step}: '{CoreName}' is baked active; the bootstrap is the only thing that may wake it.");
            return core;
        }

        /// <summary>The envelope <paramref name="coreTr"/> hangs under, held to what the step writes: an active root of its
        /// name at the origin, its Transform its one component and the core its one child, listed among the scene's roots
        /// where the core is not. Anything else was changed by hand, and is refused.</summary>
        static Doc EnvelopeOf(SceneYaml scene, Doc coreTr, long above)
        {
            Doc tr = scene.Require(above, $"the Transform '{CoreName}' hangs under");
            Doc go = tr.Stripped ? null : scene.Get(tr.FieldRef("m_GameObject"));
            List<long> roots = SceneRootsOf(scene).FieldRefs("m_Roots");
            bool holds = go != null && tr.ClassId == TransformClass && go.Field("m_Name") == EnvelopeName &&
                         go.Field("m_IsActive") == "1" && tr.FieldRef(ParentKey) == 0 &&
                         go.FieldRefs("m_Component").SequenceEqual(new[] { tr.FileId }) &&
                         tr.FieldRefs("m_Children").SequenceEqual(new[] { coreTr.FileId }) &&
                         roots.Contains(tr.FileId) && !roots.Contains(coreTr.FileId);
            if (!holds)
                throw new Refusal($"{Step}: '{CoreName}' hangs under &{above}, which is not the envelope the step writes; it was changed by hand.");
            RequireIdentity(tr, EnvelopeName);
            return go;
        }

        /// <summary>The step's gate, in place of <see cref="LayerPatch.Seal"/>: the patch adds the envelope's two documents
        /// and edits two, the core's Transform and the roots list, one line each; every other document is written as it
        /// was, in its order; and everything the four point at is in the scene.</summary>
        static void Gate(SceneYaml before, SceneYaml after, LayerPatch patch)
        {
            if (patch.Ops.Count != 4 || patch.Ops.Count(o => o.Kind == OpKind.Add) != 2 || patch.Ops.Count(o => o.Kind == OpKind.Edit) != 2)
                throw new Refusal($"{Step}: the patch is not two additions and two edits.");
            foreach (Op op in patch.Ops.Where(o => o.Kind == OpKind.Edit))
            {
                string[] was = op.Before.Split('\n'), now = op.After.Split('\n');
                if (was.Length != now.Length || was.Where((line, i) => line != now[i]).Count() != 1)
                    throw new Refusal($"{Step}: the edit of {op.Name} changes other than one line.");
            }
            var touched = new HashSet<long>(patch.Ops.Select(o => o.FileId));
            IEnumerable<string> Kept(SceneYaml s) => s.Docs.Where(d => !touched.Contains(d.FileId)).Select(d => d.Text);
            if (after.Docs.Count != before.Docs.Count + 2 || !Kept(after).SequenceEqual(Kept(before)))
                throw new Refusal($"{Step}: the patch changes a document beyond its four.");
            foreach (Op op in patch.Ops)
                foreach (Match m in LocalRef.Matches(op.After))
                {
                    long id = long.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
                    if (id != 0 && !after.Contains(id)) throw new Refusal($"{Step}: {op.Name} points at &{id}, which the scene does not hold.");
                }
        }

        /// <summary>The document with its one line <paramref name="from"/> written <paramref name="to"/>. Refuses a document
        /// that holds the line other than once.</summary>
        static string OneLine(Doc doc, string from, string to)
        {
            var lines = (string[])doc.Lines.Clone();
            int[] at = Enumerable.Range(0, lines.Length).Where(i => lines[i] == from).ToArray();
            if (at.Length != 1) throw new Refusal($"{Step}: &{doc.FileId} holds '{from.Trim()}' {at.Length} times; the step needs it once.");
            lines[at[0]] = to;
            return string.Join("\n", lines);
        }

        // ---- the documents, as the editor writes them in RicePoint.unity's envelope ----

        static string GameObjectYaml(long id, long transform) => string.Join("\n",
            Header(GameObjectClass, id, "GameObject"), Unlinked, "  serializedVersion: 6", "  m_Component:",
            "  - component: " + Ref(transform), "  m_Layer: 0", "  m_Name: " + EnvelopeName, "  m_TagString: Untagged",
            "  m_Icon: {fileID: 0}", "  m_NavMeshLayer: 0", "  m_StaticEditorFlags: 0", "  m_IsActive: 1");

        static string TransformYaml(long id, long go, long child) => string.Join("\n",
            Header(TransformClass, id, "Transform"), Unlinked, "  m_GameObject: " + Ref(go), "  serializedVersion: 2",
            "  m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}", "  m_LocalPosition: {x: 0, y: 0, z: 0}",
            "  m_LocalScale: {x: 1, y: 1, z: 1}", "  m_ConstrainProportionsScale: 0", "  m_Children:", "  - " + Ref(child),
            $"  {ParentKey}: {Ref(0)}", "  m_LocalEulerAnglesHint: {x: 0, y: 0, z: 0}");

        static string Header(int classId, long id, string kind) =>
            $"--- !u!{classId.ToString(CultureInfo.InvariantCulture)} &{id.ToString(CultureInfo.InvariantCulture)}\n{kind}:";

        static string Ref(long id) => "{fileID: " + id.ToString(CultureInfo.InvariantCulture) + "}";

        // =====================================================================================================
        //  The menu and the bake
        // =====================================================================================================

        [MenuItem("Hidden Harbours/Nine Mile Creek Layers/Dev core envelope (dry run)")]
        public static void DryRun() => Run(apply: false);

        [MenuItem("Hidden Harbours/Nine Mile Creek Layers/Apply dev core envelope")]
        public static void Apply() => Run(apply: true);

        /// <summary>The bake's batch entry (Phase D): applies step 13, then plans it again on what it wrote, which must be
        /// empty. Exits 1 on any refusal, so a half-baked scene never reports success.</summary>
        public static void ApplyCli()
        {
            try
            {
                Run(apply: true);
                if (!Plan(File.ReadAllText(NineMileCreekLayerRefresh.ScenePath), out _).IsEmpty)
                    throw new Refusal($"{Step}: a second plan is not empty.");
                Debug.Log($"[{Step}] APPLIED; a second plan is empty.");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        /// <summary>Plans step 13 on the committed scene and writes its patch, and the step's summary, under
        /// <see cref="NineMileCreekRicePointDoors.PatchFolder"/>; with <paramref name="apply"/>, writes the scene too (it
        /// must be closed).</summary>
        public static LayerPatch Run(bool apply)
        {
            string path = NineMileCreekLayerRefresh.ScenePath;
            if (apply)
                for (int i = 0; i < SceneManager.sceneCount; i++)
                    if (SceneManager.GetSceneAt(i).path == path)
                        throw new Refusal("NineMileCreek is open. Close it before applying text patches.");
            string before = File.ReadAllText(path);
            LayerPatch patch = Plan(before, out string after);
            Write(NineMileCreekRicePointDoors.PatchFolder, patch);
            if (apply && after != before) File.WriteAllText(path, after, new UTF8Encoding(false));
            Debug.Log($"[{Step}] {(apply ? "Applied" : "Dry run")}:\n" + patch.Summary());
            return patch;
        }

        /// <summary>The patch as its own file under <paramref name="folder"/>, and the step's summary beside it.</summary>
        public static void Write(string folder, LayerPatch patch)
        {
            Directory.CreateDirectory(folder);
            var utf8 = new UTF8Encoding(false);
            File.WriteAllText(Path.Combine(folder, NineMileCreekGroundSteps.PatchFileName(patch)), patch.ToYaml(), utf8);
            File.WriteAllText(Path.Combine(folder, Step + ".summary.txt"), patch.Summary() + "\n", utf8);
        }
    }
}
#endif
