#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using static HiddenHarbours.App.Editor.StPetersLayerRefresh;

namespace HiddenHarbours.App.Editor
{
    public static partial class NineMileCreekWharfLandSide
    {
        public const string PlacementStep = "13-wharf-land-side";
        public const string RetireBuyerStep = "14-retire-fish-buyer-stall";
        public const string RetireOfficeStep = "15-retire-harbourmaster-office";
        public const long OldBuyerRoot = 1176619496, OldOfficeRoot = 2136210944;

        public static IEnumerable<(string step, Func<SceneYaml, LayerPatch> plan)> Planners()
        {
            yield return (PlacementStep, PlaceRows);
            yield return (PlacementStep, PlaceOfficeVendor);
            yield return (PlacementStep, PlaceMarketCounter);
            yield return (PlacementStep, MoveHydrant);
            yield return (PlacementStep, PlaceBuyerTruck);
            yield return (PlacementStep, MoveBuyer);
            yield return (PlacementStep, RetireShedLots);
            yield return (PlacementStep, RetireShopSites);
            yield return (RetireBuyerStep, RetireBuyer);
            yield return (RetireOfficeStep, RetireOffice);
        }

        public static void DryRunFromCommandLine() => NineMileCreekLayerRefresh.Run(false);
        public static void ApplyFromCommandLine() => NineMileCreekLayerRefresh.Run(true);

        public static void BakeAndDryRunFromCommandLine()
        {
            HiddenHarbours.Tools.RigBaking.NmcWharfBuildingBakeMenu.BakeFromCommandLine();
            DryRunFromCommandLine();
        }

        public static void ApplyTwiceAndWriteStorePatchesFromCommandLine()
        {
            if (UnityEngine.SceneManagement.SceneManager.GetSceneByPath(NineMileCreekLayerRefresh.ScenePath).isLoaded)
                UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,
                    UnityEditor.SceneManagement.NewSceneMode.Single);
            DryRunFromCommandLine();
            var receipts = "Evidence~/nmc-h6/pr2d-preapply-patches";
            System.IO.Directory.CreateDirectory(receipts);
            foreach (string path in System.IO.Directory.GetFiles(NineMileCreekLayerRefresh.PatchFolder, "*.patch.yaml"))
            {
                string receipt = System.IO.Path.Combine(receipts, System.IO.Path.GetFileName(path));
                if (!System.IO.File.Exists(receipt)) System.IO.File.Copy(path, receipt);
            }
            ApplyFromCommandLine();
            string Sha()
            {
                using var sha = System.Security.Cryptography.SHA256.Create();
                return string.Concat(sha.ComputeHash(System.IO.File.ReadAllBytes(NineMileCreekLayerRefresh.ScenePath)).Select(b => b.ToString("x2")));
            }
            string first = Sha();
            ApplyFromCommandLine();
            string second = Sha();
            System.IO.Directory.CreateDirectory("Evidence~/nmc-h6");
            System.IO.File.WriteAllText("Evidence~/nmc-h6/pr2-second-apply-hashes.txt", first + "\n" + second + "\n");
            if (first != second) throw new Refusal("H6 second Apply changed the scene bytes.");
            StoreWiringPatch.Run(false);
        }

        public static LayerPatch RetireBuyer(SceneYaml scene) =>
            Retire(scene, RetireBuyerStep, "FishBuyerStall", OldBuyerRoot);

        public static LayerPatch RetireOffice(SceneYaml scene) =>
            Retire(scene, RetireOfficeStep, "HarbourmasterOffice", OldOfficeRoot);

        static LayerPatch Retire(SceneYaml scene, string step, string name, long rootId)
        {
            var patch = LayerPatch.Retirement(step, name, rootId);
            if (!scene.Contains(rootId))
            {
                if (scene.Docs.Any(d => d.ClassId == 1 && d.Field("m_Name") == name))
                    throw new Refusal($"{step}: '{name}' stands under a different id; its retirement names &{rootId}.");
                return patch.Seal(scene);
            }
            Doc root = scene.Require(rootId, name);
            if (root.ClassId != 1 || root.Field("m_Name") != name)
                throw new Refusal($"{step}: &{rootId} is not the named '{name}' GameObject.");
            Doc transform = scene.TransformOf(root);
            if (transform.FieldRef("m_Father") != 0)
                throw new Refusal($"{step}: '{name}' is no longer a top-level root.");
            foreach (long id in scene.SubtreeOf(rootId).OrderBy(id => id))
            {
                Doc doc = scene.Require(id, name);
                patch.Delete(doc, $"{name}/&{id} class {doc.ClassId}");
            }
            Doc roots = SceneRootsOf(scene);
            string entry = "  - {fileID: " + transform.FileId.ToString(CultureInfo.InvariantCulture) + "}";
            var lines = roots.Text.Split('\n').ToList();
            if (lines.Count(line => line == entry) != 1)
                throw new Refusal($"{step}: '{name}' must be listed once in SceneRoots.");
            lines.Remove(entry);
            if (roots.FieldRefs("m_Roots").Count == 1)
                lines[lines.IndexOf("  m_Roots:")] = "  m_Roots: []";
            patch.Edit(roots, string.Join("\n", lines), "SceneRoots/unlist " + name);
            return patch.Seal(scene);
        }
    }
}
#endif
