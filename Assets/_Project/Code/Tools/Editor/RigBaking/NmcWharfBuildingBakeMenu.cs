#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using HiddenHarbours.Art.Editor;
using HiddenHarbours.World;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Tools.RigBaking
{
    public static class NmcWharfBuildingBakeMenu
    {
        public const string Folder = "Assets/_Project/Art/Sprites/Buildings/NineMileCreek";
        public const string ContractPath = Folder + "/Buildings.json";
        public const string ScenePath = "Assets/_Project/Data/KeyScenes/KeyScene_nmc_wharf.asset";
        public const int Facings = 8, ImportCap = 4096;

        public static string Stem(KeyScenePiece row) => "NmcWharf_" + row.Id.Substring("structure.nmc_".Length);

        public static string Options(KeyScenePiece row)
        {
            string Value(string value)
            {
                if (value == "true" || value == "false" || value == "null" ||
                    double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out _)) return value;
                return "'" + value.Replace("\\", "\\\\").Replace("'", "\\'") + "'";
            }
            var fields = row.Options.Where(o => o.Key != "preset" && !o.Key.StartsWith("nmc.", StringComparison.Ordinal))
                .Select(o => o.Key + ":" + Value(o.Value));
            return "Object.assign({},WharfBuilding2.PRESETS['" + row.Piece + "'],{" + string.Join(",", fields) + "})";
        }

        public static VillageBuildingKit.Contract Load() =>
            JsonUtility.FromJson<VillageBuildingKit.Contract>(File.ReadAllText(ContractPath));

        [MenuItem("Hidden Harbours/Art/Bake Nine Mile Creek Wharf Buildings (pass 2)")]
        public static void BakeFromCommandLine()
        {
            var scene = AssetDatabase.LoadAssetAtPath<KeySceneDef>(ScenePath);
            if (scene == null) throw new InvalidOperationException("The wharf key scene is missing.");
            KeyScenePiece[] rows = scene.Pieces.Where(p => p.Kit == "wharfBuilding2").ToArray();
            if (rows.Length != 11) throw new InvalidOperationException("The wharf building bake requires its eleven recorded rows.");
            var entries = new List<VillageBuildingKit.Entry>();
            int ppu = 0;
            foreach (KeyScenePiece row in rows)
            {
                string options = Options(row);
                BuildingBakeResult r = BuildingRigBaker.Bake(BuildingBakeRequest.FromOptions(
                    "wharfBuilding2", options, row.Id, Folder, Stem(row), Facings,
                    requireDistinctFromDefault: true, maxSheetDimension: ImportCap));
                var entry = new VillageBuildingKit.Entry
                {
                    key = row.Id, label = row.Id, rig = "wharfBuilding2",
                    rigScript = RigCatalog.Get("wharfBuilding2").ScriptPath, rigGlobal = "WharfBuilding2",
                    sheet = Path.GetFileName(r.AssetPath), preset = row.Piece, fromPreset = true,
                    optionsJs = options, importCap = ImportCap, facings = r.Facings, cols = r.Columns, rows = r.Rows,
                    cellW = r.CellWidth, cellH = r.CellHeight, nativeCellW = r.NativeCellWidth, nativeCellH = r.NativeCellHeight,
                    cropX = r.CropX, cropY = r.CropY, pivotX = (float)r.PivotX, pivotY = (float)r.PivotY,
                    sheetW = r.SheetWidth, sheetH = r.SheetHeight,
                    footprintWidthMetres = (float)r.FootprintWidthMeters, footprintLengthMetres = (float)r.FootprintLengthMeters,
                    convention = r.MeasuredConvention.ToString(), pngBytes = r.PngBytes,
                    runtimeBytesRgba32 = r.RuntimeBytesRgba32, cropSaving = (float)r.CropSaving,
                    doorX = (r.DoorX ?? Array.Empty<double>()).Select(v => (float)v).ToArray(), doorY = (r.DoorY ?? Array.Empty<double>()).Select(v => (float)v).ToArray(),
                    phase = "", decay = "", state = "finished",
                };
                Vector2 pivot = VillageBuildingKit.NormalizedPivot(entry);
                entry.unityPivotX = pivot.x; entry.unityPivotY = pivot.y;
                entries.Add(entry); ppu = r.PixelsPerMetre;
                Debug.Log($"[nmc-wharf-buildings] {row.Id}: {r.SheetWidth}x{r.SheetHeight}; {options}");
            }
            Directory.CreateDirectory(Folder);
            var contract = new VillageBuildingKit.Contract
            {
                note = "Generated from the wharf key scene's eleven pass-2 buildings. Re-bake; do not edit measurements.",
                writtenBy = nameof(NmcWharfBuildingBakeMenu), ppu = ppu, facings = Facings,
                conventionNote = "Measured by BuildingRigAzimuthProbe; clockwise sheet cells.",
                pivotNote = "Ground centre, cropped-cell pixels from top left; Unity uses the measured normalized pivot.",
                buildings = entries.ToArray(),
            };
            File.WriteAllText(ContractPath, JsonUtility.ToJson(contract, true) + "\n");
            AssetDatabase.Refresh();
            foreach (var entry in entries)
                if (!VillageBuildingSheetSlicer.SliceOne(Folder + "/" + entry.sheet, Path.GetFileNameWithoutExtension(entry.sheet), entry))
                    throw new InvalidOperationException("The NMC sheet did not slice: " + entry.sheet);
            Debug.Log("[nmc-wharf-buildings] PASS: eleven sheets baked and sliced in their own folder.");
        }
    }
}
#endif
