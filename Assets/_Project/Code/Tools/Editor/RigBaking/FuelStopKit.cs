using System;
using System.Collections.Generic;
using System.Linq;
using HiddenHarbours.Art.Editor;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>
    /// Nine Mile Creek's fuel stop (pass 6) on the shared set-piece route: seventeen kinds, one SetPieceDef each.
    /// The sheets are baked headless outside the editor (docs/art/rigs/fuel-stop/README.md) and committed, so this
    /// entry imports, slices and builds; it never re-bakes and never writes a scene.
    /// </summary>
    public static class FuelStopKit
    {
        public const string Folder = "docs/art/rigs/fuel-stop";
        public const string SheetFolder = "Assets/_Project/Art/Sprites/NineMileCreek/FuelStop";
        public const string TodayPath = SheetFolder + "/fuelStop.today.json";
        public const string ContractPath = SheetFolder + "/fuelStop.contract.json";
        public const int ImportSizeCap = 4096, Facings = 8;

        /// <summary>The kinds in the today file's order; a row's Def is its kind's (NineMileCreekFuelStopPlan.DefinitionId).</summary>
        public static readonly IReadOnlyList<KeyValuePair<string, string>> Pieces = new[]
        {
            Kind("store", "station.nmc_store"), Kind("canopy", "station.nmc_canopy"), Kind("truck", "station.nmc_semi_lane"),
            Kind("pylon", "station.nmc_pylon"), Kind("iceBox", "prop.nmc_ice_box"), Kind("propane", "station.nmc_propane"),
            Kind("air", "station.nmc_air"), Kind("vacuum", "station.nmc_vacuum"), Kind("dumpster", "prop.nmc_dumpster"),
            Kind("bench", "station.nmc_bench"), Kind("firewood", "station.nmc_firewood"), Kind("lampPole", "prop.nmc_lamp_pole"),
            Kind("barSign", "prop.nmc_bar_sign"), Kind("stopSign", "prop.nmc_stop_sign"), Kind("picnic", "prop.nmc_picnic"),
            Kind("wheelStop", "prop.nmc_wheel_stop"), Kind("bin", "prop.nmc_bin"),
        };

        public static readonly SetPieceKit Definition = new SetPieceKit
        {
            Folder = Folder, GlobalName = "FuelStopGameplay", Version = "1.0.0",
            SidecarSchema = "hidden-harbours/fuel-stop-piece@1", TodaySchema = "hidden-harbours/fuel-stop-today@1",
            ImportSizeCap = ImportSizeCap, Facings = Facings,
            Scripts = new[] { "../nmc-set-pieces-kit/lib/weatherSky.js", "../nmc-set-pieces-kit/lib/coastalPass.js",
                "fuelStopRig.js", "fuelStopGameplay.js" },
            Pieces = Pieces,
            ExtraGameplayMembers = new HashSet<string>(StringComparer.Ordinal),
            ExtraAnchorMembers = new HashSet<string>(StringComparer.Ordinal),
            Sheets = new SetPieceSheetSlicer.Kit(SheetFolder, TodayPath, ContractPath, "hidden-harbours/fuel-stop-contract@1"),
        };

        static KeyValuePair<string, string> Kind(string key, string id) => new KeyValuePair<string, string>(key, id);

        /// <summary>Imports the committed sheets synchronously, slices them and builds every Def. Returns the problems.</summary>
        public static List<string> SliceAndBuild()
        {
            var problems = new List<string>();
            var contract = SetPieceSheetSlicer.LoadContract(Definition.Sheets, out string error);
            if (contract == null) return new List<string> { error };
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (var piece in contract.pieces)
                AssetDatabase.ImportAsset(SetPieceSheetSlicer.SheetPath(Definition.Sheets, piece.key),
                                          ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(ContractPath, ImportAssetOptions.ForceSynchronousImport);
            int sliced = SetPieceSheetSlicer.SliceAll(Definition.Sheets, problems);
            if (sliced != Pieces.Count) problems.Add($"sliced {sliced} of {Pieces.Count} fuel stop sheets.");
            if (problems.Count > 0) return problems;
            problems.AddRange(SetPieceDefBuilder.BuildAll(Definition));
            return problems;
        }

        [MenuItem("Hidden Harbours/Art/Build Nine Mile Creek Fuel Stop Set Piece Defs", priority = 83)]
        public static void SliceAndBuildMenu()
        {
            var problems = SliceAndBuild();
            foreach (string p in problems) Debug.LogError("[FuelStopKit] " + p);
            if (problems.Count == 0) Debug.Log($"[FuelStopKit] {Pieces.Count} Defs in {SetPieceDefBuilder.DefFolder}.");
        }

        /// <summary>Headless entry (-executeMethod): exit 1 on ANY problem.</summary>
        public static void SliceAndBuildFromCommandLine()
        {
            int code = 1;
            try
            {
                var problems = SliceAndBuild();
                foreach (string p in problems) Debug.LogError("[FuelStopKit] " + p);
                if (problems.Count == 0)
                    Debug.Log("[FuelStopKit] FUEL_STOP_DEFS_BUILT " + string.Join(",", Pieces.Select(p => p.Key)));
                code = problems.Count == 0 ? 0 : 1;
            }
            catch (Exception ex) { Debug.LogError("[FuelStopKit] headless build failed: " + ex); }
            EditorApplication.Exit(code);
        }
    }
}
