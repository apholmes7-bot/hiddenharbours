#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HiddenHarbours.Art.Editor
{
    /// <summary>
    /// Re-binds every Acadian tree standing in a scene to the kit as it is NOW, in place, through
    /// <see cref="AcadianTreeCatalog.Configure"/>, the one method the planters and the Tree Paint Tool
    /// build a tree through. A re-bake reaches the woods this way without re-planting them and without
    /// a hand edit (the pass-4 switch, 2026-09-27).
    ///
    /// <para><b>What a tree keeps:</b> its position, parent, name and sorting order, and the species,
    /// stage, season and variant its sprite names (<c>{species}_{stage}_{season}_v{n}</c>). <b>What it
    /// takes from the kit:</b> that variant's sprite, the species' trunk anchor, light sheets and wind
    /// maps, and the shared tree material, exactly as <c>Configure</c> sets them for a new tree.</para>
    ///
    /// <para><b>It refuses rather than guesses.</b> A tree whose sprite does not name a placement the
    /// contract publishes, whose variant has no sprite, that is part of a prefab instance, or that is
    /// scaled (<c>Configure</c> would reset the scale), is listed, and the scene is not touched. The
    /// headless run saves a scene only when every tree in it re-bound, and exits 1 otherwise.</para>
    /// </summary>
    public static class AcadianTreeRebind
    {
        const string MenuPath = "Hidden Harbours/Art/Re-bind Acadian Trees In Open Scenes";

        /// <summary>The region scenes the woods stand in: what the headless run re-binds.</summary>
        public static readonly string[] RegionScenes =
        {
            "Assets/_Project/Scenes/StPeters.unity",
            "Assets/_Project/Scenes/NineMileCreek.unity",
        };

        /// <summary>A kit sprite's name: <c>{species}_{stage}_{season}_v{n}</c>, and <c>_f{k}</c> on a
        /// swaying sheet's frame.</summary>
        static readonly Regex KitSpriteName = new Regex(
            @"^(?<species>[A-Za-z]+)_(?<stage>[a-z]+)_(?<season>[a-z]+)_v(?<variant>\d+)(?:_f\d+)?$",
            RegexOptions.CultureInvariant);

        public sealed class Result
        {
            public string Scene;
            public int Trees;
            public int Rebound;
            public int SpriteChanged;
            public int MapsBound;
            public readonly SortedDictionary<string, int> PerSpecies = new SortedDictionary<string, int>(StringComparer.Ordinal);
            public readonly SortedDictionary<string, string> AnchorBySpecies = new SortedDictionary<string, string>(StringComparer.Ordinal);
            public readonly List<string> Refused = new List<string>();
            public bool Clean => Refused.Count == 0;

            public override string ToString()
            {
                var sb = new StringBuilder();
                sb.AppendLine($"[tree-rebind] {Scene}: {Trees} tree(s), {Rebound} re-bound, {SpriteChanged} took a new " +
                              $"sprite, {MapsBound} carry the wind maps (_TreeMaps 1); refused {Refused.Count}");
                sb.AppendLine("  per species: " + string.Join(", ", PerSpecies.Select(kv => $"{kv.Key} {kv.Value}")));
                foreach (var kv in AnchorBySpecies) sb.AppendLine($"  _TrunkAnchor {kv.Key,-16} {kv.Value}");
                foreach (string r in Refused) sb.AppendLine("  REFUSED " + r);
                return sb.ToString().TrimEnd();
            }
        }

        /// <summary>Re-binds every tree in <paramref name="scene"/>, or none: the refusals are found
        /// first, so a refused scene is left exactly as it was.</summary>
        public static Result RebindScene(Scene scene, bool recordUndo)
        {
            var result = new Result { Scene = scene.path };
            var placements = AcadianTreeCatalog.Scan();
            if (placements.Count == 0)
            {
                result.Refused.Add($"the contract at {TreeKitCatalog.ContractPath} publishes no trees");
                return result;
            }
            Material material = AcadianTreeCatalog.LoadMaterial();
            if (material == null)
            {
                result.Refused.Add($"no tree material at {AcadianTreeCatalog.MaterialPath}");
                return result;
            }

            var anchors = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<TreeTrunkAnchor>(includeInactive: true))
                .ToList();
            result.Trees = anchors.Count;

            // Pass 1: resolve every tree, touch none.
            var plan = new List<(TreeTrunkAnchor anchor, SpriteRenderer sr, AcadianTreeCatalog.Placement placement, Sprite sprite)>();
            foreach (TreeTrunkAnchor anchor in anchors)
            {
                GameObject go = anchor.gameObject;
                string where = $"'{PathOf(go.transform)}'";
                var sr = go.GetComponent<SpriteRenderer>();
                if (sr == null || sr.sprite == null) { result.Refused.Add($"{where}: no sprite to read the tree from"); continue; }
                if (PrefabUtility.IsPartOfPrefabInstance(go)) { result.Refused.Add($"{where}: part of a prefab instance"); continue; }
                if (go.transform.localScale != Vector3.one)
                {
                    result.Refused.Add($"{where}: scaled {go.transform.localScale}, and Configure resets the scale");
                    continue;
                }
                Match m = KitSpriteName.Match(sr.sprite.name);
                if (!m.Success) { result.Refused.Add($"{where}: sprite '{sr.sprite.name}' is not a kit sprite name"); continue; }
                string species = m.Groups["species"].Value, stage = m.Groups["stage"].Value, season = m.Groups["season"].Value;
                int variant = int.Parse(m.Groups["variant"].Value, System.Globalization.CultureInfo.InvariantCulture);
                var placement = placements.FirstOrDefault(p => p.Species == species && p.Season == season &&
                                                               string.Equals(p.Entry.stage, stage, StringComparison.Ordinal));
                if (!placement.IsValid)
                {
                    result.Refused.Add($"{where}: the contract publishes no {species} {stage} in {season}");
                    continue;
                }
                Sprite sprite = AcadianTreeCatalog.LoadVariant(placement, variant);
                if (sprite == null)
                {
                    result.Refused.Add($"{where}: {placement.SheetPath} has no variant {variant}");
                    continue;
                }
                plan.Add((anchor, sr, placement, sprite));
            }
            if (!result.Clean) return result;

            // Pass 2: every tree resolved, so re-bind them all.
            foreach (var (anchor, sr, placement, sprite) in plan)
            {
                GameObject go = anchor.gameObject;
                if (recordUndo) Undo.RegisterFullObjectHierarchyUndo(go, "Re-bind Acadian tree");
                Vector3 position = go.transform.position;
                Transform parent = go.transform.parent;
                string name = go.name;
                bool newSprite = sr.sprite != sprite;

                AcadianTreeCatalog.Configure(go, placement, sprite, material, sr.sortingOrder);

                if (go.transform.position != position || go.transform.parent != parent || go.name != name)
                    throw new InvalidOperationException(
                        $"'{PathOf(go.transform)}' moved, changed parent or was renamed by Configure. A re-bind " +
                        "keeps where a tree stands; fix Configure before re-binding.");
                result.Rebound++;
                if (newSprite) result.SpriteChanged++;
                if (anchor.HasWindMaps) result.MapsBound++;
                result.PerSpecies[placement.Species] = result.PerSpecies.TryGetValue(placement.Species, out int n) ? n + 1 : 1;
                result.AnchorBySpecies[placement.Species] = anchor.Anchor.ToString("0.0000", System.Globalization.CultureInfo.InvariantCulture);
                EditorUtility.SetDirty(go);
            }
            if (result.Rebound > 0) EditorSceneManager.MarkSceneDirty(scene);
            return result;
        }

        /// <summary>Re-binds the trees in every open scene, with undo; saving is the owner's call.</summary>
        [MenuItem(MenuPath, priority = 52)]
        public static void RebindOpenScenes()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded) continue;
                Result r = RebindScene(scene, recordUndo: true);
                if (r.Clean) Debug.Log(r.ToString());
                else Debug.LogError(r.ToString());
            }
        }

        /// <summary>
        /// Headless entry point (<c>-executeMethod HiddenHarbours.Art.Editor.AcadianTreeRebind.RebindRegionScenesFromCommandLine</c>):
        /// opens each of <see cref="RegionScenes"/>, re-binds it, and saves it only if every tree re-bound.
        /// Exits 0 when every scene saved, 1 otherwise.
        /// </summary>
        public static void RebindRegionScenesFromCommandLine()
        {
            bool ok = true;
            try
            {
                foreach (string path in RegionScenes)
                {
                    Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                    Result r = RebindScene(scene, recordUndo: false);
                    if (!r.Clean || r.Rebound != r.Trees)
                    {
                        ok = false;
                        Debug.LogError(r + "\n[tree-rebind] NOT saved: " + path);
                        continue;
                    }
                    if (!EditorSceneManager.SaveScene(scene))
                    {
                        ok = false;
                        Debug.LogError(r + "\n[tree-rebind] the save FAILED: " + path);
                        continue;
                    }
                    Debug.Log(r + "\n[tree-rebind] saved: " + path);
                }
            }
            catch (Exception ex)
            {
                ok = false;
                Debug.LogError($"[tree-rebind] headless re-bind failed: {ex}");
            }
            EditorApplication.Exit(ok ? 0 : 1);
        }

        static string PathOf(Transform t)
        {
            var parts = new List<string>();
            for (; t != null; t = t.parent) parts.Add(t.name);
            parts.Reverse();
            return string.Join("/", parts);
        }
    }
}
#endif
