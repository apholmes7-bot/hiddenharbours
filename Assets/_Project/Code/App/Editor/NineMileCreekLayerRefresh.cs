#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using HiddenHarbours.Art;
using HiddenHarbours.Tools.RigBaking;
using HiddenHarbours.Core;
using HiddenHarbours.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using static HiddenHarbours.App.Editor.StPetersLayerRefresh;
using Object = UnityEngine.Object;

namespace HiddenHarbours.App.Editor
{
    /// <summary>N1 decision 1(b): named text patches, never a whole-scene rebuild.
    /// The first step carries #765. #824's station interior remains the named next step:
    /// its prefab overrides, entry leaves, solid blocker and wall ring must move together.
    /// This step leaves every document outside QuayFace byte-identical, including #882's
    /// 298 tree placements and 562 shadow records, S1, seller ids, book and relights.</summary>
    public static class NineMileCreekLayerRefresh
    {
        public const string ScenePath = "Assets/_Project/Scenes/NineMileCreek.unity";
        public const string PatchFolder = "artifacts/nmc-n1";
        public const string QuayStep = "01-quay-face-765";
        public const string InteriorStep = "02-building-interiors-824-carried";
        public const string InteriorDebt = "#824: CreekShops already has the shared recipe's two rooms. " +
            "Carry the Route91Station room/door step: patch its prefab overrides, entry leaves, " +
            "solid blocker and wall ring together. A QuayFace patch cannot safely stand that room.";

        public interface IFaceAssets
        {
            SpriteRef SpriteFor(NineMileCreekDressing.FacePiece piece);
            ObjRef DefaultMaterial { get; }
            ObjRef TideMaterial { get; }
            ObjRef TideScript { get; }
        }

        public sealed class EditorFaceAssets : IFaceAssets
        {
            public ObjRef DefaultMaterial { get; }
            public ObjRef TideMaterial { get; }
            public ObjRef TideScript { get; }
            public EditorFaceAssets()
            {
                // The same SpriteRenderer default as PlaceFace, including the active render pipeline.
                var probe = new GameObject("N1 material probe") { hideFlags = HideFlags.HideAndDontSave };
                probe.SetActive(false);
                try { DefaultMaterial = AssetRef(probe.AddComponent<SpriteRenderer>().sharedMaterial, 2); }
                finally { Object.DestroyImmediate(probe); }
                TideMaterial = AssetRef(Resources.Load<Material>(TidalFaceWaterline.MaterialResourceName), 2);
                TideScript = AssetRef(AssetDatabase.LoadAssetAtPath<MonoScript>(
                    "Assets/_Project/Code/Art/TidalFaceWaterline.cs"), 3);
            }
            public SpriteRef SpriteFor(NineMileCreekDressing.FacePiece piece)
            {
                int facing = IsoPackSprites.FacingForHeading(NineMileCreekDressing.WharfFamily, piece.Heading);
                Sprite sprite = IsoPackSprites.Facing(NineMileCreekDressing.WharfFamily, piece.Key, facing);
                if (sprite == null) throw new Refusal($"{QuayStep}: missing {piece.Key}, facing {facing}.");
                return new SpriteRef(AssetRef(sprite, 3), sprite.rect.size / sprite.pixelsPerUnit);
            }
            static ObjRef AssetRef(Object asset, int type)
            {
                if (asset == null || !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string guid, out long id))
                    throw new Refusal($"{QuayStep}: missing or unimported asset '{asset?.name}'.");
                return new ObjRef(id, guid, type);
            }
        }

        sealed class Course
        {
            public Doc Go, Tr, Sr, Tide;
            public List<Doc> Components;
        }

        /// <summary>Derive only the face from the builder's public plan. No scene is opened or saved.
        /// Existing courses keep their ids; every retired component/course is named in the patch.
        /// Replanning the resulting text produces an empty patch, not just a replayable patch.</summary>
        public static LayerPatch QuayFace(SceneYaml scene, ITidalTerrain terrain, IFaceAssets assets)
        {
            if (terrain == null || assets == null) throw new ArgumentNullException();
            Doc dressing = scene.RootNamed(NineMileCreekDressing.RootName);
            Doc dressingTr = scene.TransformOf(dressing);
            RequireIdentity(dressingTr);
            Doc root = scene.ChildNamed(dressingTr, NineMileCreekDressing.FaceRootName);
            Doc rootTr = scene.TransformOf(root);
            RequireIdentity(rootTr);
            var patch = new LayerPatch(QuayStep, "NineMileCreekDressing/QuayFace", root.FileId);
            var old = new Dictionary<string, Course>(StringComparer.Ordinal);
            foreach (long trId in rootTr.FieldRefs("m_Children"))
            {
                Doc tr = scene.Require(trId, "face child");
                if (tr.Stripped || tr.FieldRefs("m_Children").Count != 0)
                    throw new Refusal($"{QuayStep}: a face course is a prefab or has children; name its treatment first.");
                Doc go = scene.Require(tr.FieldRef("m_GameObject"), "face course");
                var components = scene.ComponentsOf(go);
                foreach (Doc c in components)
                    if (c.ClassId != 4 && c.ClassId != 212 && !IsScript(c, "YSortSprite") && !IsScript(c, "TidalFaceWaterline"))
                        throw new Refusal($"{QuayStep}: {go.Field("m_Name")} has an unowned component &{c.FileId}.");
                string name = go.Field("m_Name");
                if (old.ContainsKey(name)) throw new Refusal($"{QuayStep}: duplicate course '{name}'.");
                old.Add(name, new Course { Go = go, Tr = tr,
                    Sr = components.Single(c => c.ClassId == 212),
                    Tide = components.SingleOrDefault(c => IsScript(c, "TidalFaceWaterline")), Components = components });
            }
            if (old.Count == 0) throw new Refusal($"{QuayStep}: no face course to supply the serialization template.");
            Course template = old.Values.First();
            var ids = new IdAllocator(scene);
            var desired = new HashSet<string>(StringComparer.Ordinal);
            var children = new List<long>();
            int index = 0;
            foreach (var piece in NineMileCreekDressing.FacePieces())
            {
                string name = $"{piece.Wall}_{piece.Key}_{index++}";
                desired.Add(name);
                old.TryGetValue(name, out Course current);
                SpriteRef sprite = assets.SpriteFor(piece);
                if (sprite.Sprite.FileId == 0 || string.IsNullOrEmpty(sprite.Sprite.Guid))
                    throw new Refusal($"{QuayStep}: '{name}' has no imported sprite.");
                bool cut = NineMileCreekQuayFace.DrawsAFaceAtThisCamera(NineMileCreekDressing.PlanDirectionOf(piece.Heading));
                long goId = current?.Go.FileId ?? ids.Next(QuayStep + "/" + name + "/go");
                long trId = current?.Tr.FileId ?? ids.Next(QuayStep + "/" + name + "/transform");
                long srId = current?.Sr.FileId ?? ids.Next(QuayStep + "/" + name + "/sprite");
                long tideId = cut ? current?.Tide?.FileId ?? ids.Next(QuayStep + "/" + name + "/waterline") : 0;
                children.Add(trId);
                string go = Header((current ?? template).Go.Text, goId);
                go = Field(go, "m_Name", name);
                go = List(go, "m_Component", new[] { trId, srId }.Concat(cut ? new[] { tideId } : Array.Empty<long>()), "component: ");
                string tr = Header((current ?? template).Tr.Text, trId);
                tr = Field(tr, "m_GameObject", Ref(goId));
                tr = Field(tr, "m_LocalPosition", $"{{x: {F(piece.Position.x)}, y: {F(piece.Position.y)}, z: 0}}");
                tr = Field(tr, "m_Father", Ref(rootTr.FileId));
                tr = List(tr, "m_Children", Array.Empty<long>());
                string sr = Header((current ?? template).Sr.Text, srId);
                sr = Field(sr, "m_GameObject", Ref(goId));
                sr = Field(sr, "m_Sprite", sprite.Sprite.Yaml);
                sr = Field(sr, "m_Size", $"{{x: {F(sprite.Size.x)}, y: {F(sprite.Size.y)}}}");
                sr = Field(sr, "m_SortingOrder", piece.SortingOrder.ToString(System.Globalization.CultureInfo.InvariantCulture));
                sr = Regex.Replace(sr, @"(?m)^  m_Materials:\n(?:  - .*\n)*", "  m_Materials:\n  - " + (cut ? assets.TideMaterial : assets.DefaultMaterial).Yaml + "\n");
                Put(patch, current?.Go, go, name);
                Put(patch, current?.Tr, tr, name + "/Transform");
                Put(patch, current?.Sr, sr, name + "/SpriteRenderer");
                if (cut)
                {
                    string tide = TideDoc(tideId, goId, assets.TideScript, piece.Lip.y,
                        NineMileCreekDressing.FaceLipElevation(piece, terrain));
                    Put(patch, current?.Tide, tide, name + "/TidalFaceWaterline");
                }
                if (current != null)
                    foreach (Doc component in current.Components)
                        if (IsScript(component, "YSortSprite") || (!cut && IsScript(component, "TidalFaceWaterline")))
                            patch.Delete(component, name + "/" + component.Field("m_EditorClassIdentifier"));
            }
            foreach (var pair in old.Where(p => !desired.Contains(p.Key)))
            {
                patch.Delete(pair.Value.Go, pair.Key);
                foreach (Doc component in pair.Value.Components)
                    patch.Delete(component, pair.Key + "/" + component.ClassId + " &" + component.FileId);
            }
            patch.Edit(rootTr, List(rootTr.Text, "m_Children", children), "QuayFace/Transform children");
            patch.Note("QuayFace", "builder face plan", $"{index} courses; #734/#755 spacing and sorting, #765 waterline only on visible faces");
            return patch.Seal(scene);
        }

        public static T WithPlanGround<T>(Func<ITidalTerrain, T> body)
        {
            // Inactive: never publishes GameServices.TidalTerrain. No asset or scene serialization.
            var probe = new GameObject("N1 authored ground probe") { hideFlags = HideFlags.HideAndDontSave };
            probe.SetActive(false);
            try
            {
                var ground = probe.AddComponent<MainlandTidalTerrain>();
                NineMileCreekBuilder.ConfigureNineMileCreekTerrain(ground);
                return body(ground);
            }
            finally { Object.DestroyImmediate(probe); }
        }

        [MenuItem("Hidden Harbours/Nine Mile Creek Layers/Write patches (dry run)")]
        public static void DryRun() => Run(false);
        [MenuItem("Hidden Harbours/Nine Mile Creek Layers/Apply named patches")]
        public static void Apply() => Run(true);
        public static LayerPatch Run(bool apply)
        {
            if (apply)
                for (int i = 0; i < SceneManager.sceneCount; i++)
                    if (SceneManager.GetSceneAt(i).path == ScenePath)
                        throw new Refusal("NineMileCreek is open. Close it before applying text patches.");
            string before = File.ReadAllText(ScenePath);
            var patch = WithPlanGround(ground => QuayFace(SceneYaml.Parse(before), ground, new EditorFaceAssets()));
            string after = patch.ApplyTo(before);
            Directory.CreateDirectory(PatchFolder);
            var utf8 = new UTF8Encoding(false);
            File.WriteAllText(Path.Combine(PatchFolder, QuayStep + ".patch.yaml"), patch.ToYaml(), utf8);
            File.WriteAllText(Path.Combine(PatchFolder, InteriorStep + ".txt"), InteriorDebt + "\n", utf8);
            if (apply && before != after) File.WriteAllText(ScenePath, after, utf8);
            Debug.Log($"[NineMileCreekLayerRefresh] {(apply ? "Applied" : "Dry run")}: {patch.Summary()}\n{InteriorStep}: {InteriorDebt}");
            return patch;
        }

        static bool IsScript(Doc doc, string name) => doc.ClassId == 114 &&
            (doc.Field("m_EditorClassIdentifier") ?? "").EndsWith("." + name, StringComparison.Ordinal);
        static void RequireIdentity(Doc tr)
        {
            if (tr.Field("m_LocalPosition") != "{x: 0, y: 0, z: 0}" ||
                tr.Field("m_LocalRotation") != "{x: 0, y: 0, z: 0, w: 1}" ||
                tr.Field("m_LocalScale") != "{x: 1, y: 1, z: 1}")
                throw new Refusal($"{QuayStep}: parent &{tr.FileId} is transformed; the builder places in world coordinates.");
        }
        static void Put(LayerPatch patch, Doc before, string after, string name)
        {
            if (before == null) patch.Add(after, name); else patch.Edit(before, after, name);
        }
        static string Header(string text, long id) => Regex.Replace(text, @"^--- !u!(\d+) &\d+", m => "--- !u!" + m.Groups[1].Value + " &" + id);
        static string Ref(long id) => "{fileID: " + id + "}";
        static string Field(string text, string key, string value)
        {
            string pattern = @"(?m)^  " + Regex.Escape(key) + @":.*$";
            if (!Regex.IsMatch(text, pattern)) throw new Refusal($"{QuayStep}: template is missing {key}.");
            return Regex.Replace(text, pattern, "  " + key + ": " + value);
        }
        static string List(string text, string key, IEnumerable<long> ids, string prefix = "")
        {
            var entries = ids.Select(id => "  - " + prefix + Ref(id)).ToArray();
            string value = entries.Length == 0 ? "  " + key + ": []" : "  " + key + ":\n" + string.Join("\n", entries);
            string pattern = @"(?m)^  " + Regex.Escape(key) + @":(?: \[\])?(?:\n  - [^\n]*)*";
            if (!Regex.IsMatch(text, pattern)) throw new Refusal($"{QuayStep}: template is missing {key}.");
            return Regex.Replace(text, pattern, value);
        }
        static string TideDoc(long id, long go, ObjRef script, float lip, float elevation) =>
            $"--- !u!114 &{id}\nMonoBehaviour:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {{fileID: 0}}\n" +
            $"  m_PrefabInstance: {{fileID: 0}}\n  m_PrefabAsset: {{fileID: 0}}\n  m_GameObject: {Ref(go)}\n" +
            $"  m_Enabled: 1\n  m_EditorHideFlags: 0\n  m_Script: {script.Yaml}\n  m_Name: \n" +
            "  m_EditorClassIdentifier: HiddenHarbours.Art::HiddenHarbours.Art.TidalFaceWaterline\n" +
            $"  _lipWorldY: {F(lip)}\n  _lipElevation: {F(elevation)}\n  _ridesTheTide: 1\n  _configured: 1";
    }
}
#endif
