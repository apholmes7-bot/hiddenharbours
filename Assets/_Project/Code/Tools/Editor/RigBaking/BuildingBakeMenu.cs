#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>
    /// Menu entry points for baking the BUILDING rigs — the houses and the wharf's working buildings.
    ///
    /// <para>Menu: <c>Hidden Harbours ▸ Dev ▸ Bake Buildings (houses + wharf)</c>. Re-runnable; each
    /// preset overwrites its own sheet and sidecar and touches nothing else.</para>
    ///
    /// <para>The preset lists below are the ONLY hand-written thing here, and they are deliberately a
    /// subset: <see cref="BuildingRigBaker"/> reads every other axis — cell size, pivot, footprint,
    /// facing convention — from the rig itself at bake time. If a rig drop adds a preset, add its name
    /// here; nothing else needs to change.</para>
    /// </summary>
    public static class BuildingBakeMenu
    {
        /// <summary>Where the baked building sheets land (beside the existing greybox building art).</summary>
        public const string BuildingsFolder = "Assets/_Project/Art/Sprites/Buildings";

        /// <summary>
        /// The wharf's working buildings. Nine Mile Creek's brief calls for bait sheds, trap storage
        /// and fish holds — so the shacks and the storage barns are what that region needs.
        ///
        /// <para><b><c>fishPlant</c> and <c>cannery</c> are deliberately included but are NOT for Nine
        /// Mile Creek</b> — the owner's brief is explicit that there is "no processing here yet". They
        /// are baked because they are the largest builds in the kit and therefore the worst case for
        /// the crop and the texture cap: if the baker holds for a cannery it holds for everything.
        /// Where they get placed is world-content's call, not the baker's.</para>
        /// </summary>
        public static readonly string[] WharfPresets =
        {
            "netShed", "redShed", "tealShack", "gambrelBarn", "iceHouse", "fishPlant", "cannery",
        };

        /// <summary>
        /// The houses. St Peters wants four farmsteads and a one-room school reverting to forest;
        /// Nine Mile Creek and Nine Mile Creek want a working village. Five presets covers both.
        /// </summary>
        public static readonly string[] HousePresets =
        {
            "shingleCottage", "whiteFarmhouse", "redSaltbox", "gothicRevival", "dormerCape",
        };

        // This is the PACKER-PROOF bake (worst-case crop + texture cap), not the M1 set world-content
        // ships — so it lives under Dev/ rather than beside the active-pipeline bakes in Art/.
        // Priority 121 keeps it with the other proof bakes; Unity draws a separator at any priority
        // gap over 10, so the band matters — a number borrowed from a neighbouring group renders in
        // the wrong section entirely and reads as missing.
        [MenuItem("Hidden Harbours/Dev/Bake Buildings (houses + wharf)", priority = 121)]
        public static void BakeAllMenu()
        {
            var log = new StringBuilder();
            int ok = 0, failed = 0;

            long croppedBytes = 0, uncroppedBytes = 0;

            foreach (var (rig, preset) in AllBuilds())
            {
                try
                {
                    var r = Bake(rig, preset);
                    ok++;
                    croppedBytes += r.RuntimeBytesRgba32;
                    uncroppedBytes += r.UncroppedBytesRgba32;
                    log.AppendLine(Describe(r));
                }
                catch (Exception e)
                {
                    failed++;
                    log.AppendLine($"  ✗ {rig}/{preset}: {e.Message}");
                    Debug.LogError($"[building-baker] {rig}/{preset} FAILED:\n{e}");
                }
            }

            AssetDatabase.Refresh();

            log.AppendLine();
            log.AppendLine($"  {ok} baked, {failed} failed.");
            if (uncroppedBytes > 0)
                log.AppendLine($"  texture memory: {croppedBytes / 1024.0 / 1024.0:F1} MB cropped vs " +
                               $"{uncroppedBytes / 1024.0 / 1024.0:F0} MB uncropped " +
                               $"({(1.0 - croppedBytes / (double)uncroppedBytes):P0} saved).");

            Debug.Log($"[building-baker] Bake complete.\n{log}");

            if (failed > 0)
                Debug.LogError($"[building-baker] {failed} build(s) failed — see the errors above.");
        }

        /// <summary>Bake one preset. Exposed so a test or a one-off can bake a single build.</summary>
        public static BuildingBakeResult Bake(string rigKey, string preset)
        {
            string stem = $"{Prefix(rigKey)}_{preset}";
            return BuildingRigBaker.Bake(BuildingBakeRequest.FromPreset(
                rigKey, preset, RigCatalog.Get(rigKey).GlobalName, BuildingsFolder, stem));
        }

        /// <summary>Every (rig, preset) pair the menu bakes.</summary>
        public static IEnumerable<(string rig, string preset)> AllBuilds()
        {
            foreach (var p in HousePresets) yield return ("house", p);
            foreach (var p in WharfPresets) yield return ("wharfBuilding", p);
        }

        /// <summary>Sheet-name prefix per rig. Keeps houses and wharf buildings apart in one folder.</summary>
        public static string Prefix(string rigKey) => rigKey switch
        {
            "house" => "HouseIso",
            "wharfBuilding" => "WharfBuildingIso",
            _ => throw new ArgumentException($"'{rigKey}' is not a building rig."),
        };

        static string Describe(BuildingBakeResult r) =>
            $"  ✓ {r.Preset,-16} {r.CellWidth,4}×{r.CellHeight,-4} cell " +
            $"({r.Columns}×{r.Rows}) → {r.SheetWidth}×{r.SheetHeight}, " +
            $"{r.PngBytes / 1024.0:F0} KB png, crop saved {r.CropSaving:P0}, " +
            $"pivot ({r.PivotX:F0},{r.PivotY:F0}), {r.MeasuredConvention}";

        /// <summary>
        /// Headless entry point for <c>-executeMethod</c>. Exits non-zero if any build fails so a CI or
        /// batch bake fails loudly instead of leaving a half-baked folder.
        /// </summary>
        public static void BakeAllFromCommandLine()
        {
            int failed = 0;
            foreach (var (rig, preset) in AllBuilds())
            {
                try { Bake(rig, preset); }
                catch (Exception e)
                {
                    failed++;
                    Debug.LogError($"[building-baker] {rig}/{preset} FAILED:\n{e}");
                }
            }

            AssetDatabase.Refresh();

            if (failed > 0)
            {
                Debug.LogError($"[building-baker] {failed} build(s) failed.");
                EditorApplication.Exit(1);
            }
        }
    }
}
#endif
