using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using HiddenHarbours.Art;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Art.Editor
{
    /// <summary>
    /// Turns the baked fuel contract into content: one <see cref="FuelContainerDef"/> per container
    /// and one placeable prefab each.
    ///
    /// <para>Everything is DERIVED from the contract and the sliced sheets, so a re-bake at a
    /// different facing or fill count regenerates rather than leaving stale assets behind. Ids are
    /// stable and append-only, which is what lets this be re-run safely: an existing Def is UPDATED
    /// in place (same GUID, so every scene reference survives) rather than replaced.</para>
    /// </summary>
    public static class FuelContainerDefBuilder
    {
        public const string DefFolder = "Assets/_Project/Data/FuelContainers";
        public const string PrefabFolder = "Assets/_Project/Prefabs/FuelStorage";

        /// <summary>Id prefix. Append-only and never renamed — <c>fuelstore.gas_jerry_s20</c>.</summary>
        public const string IdPrefix = "fuelstore";

        /// <summary>
        /// The container's stable id. Append-only and never renamed.
        ///
        /// <para>⚠️ The size is LOWERCASED. The rig spells its non-numeric size keys in camelCase
        /// (<c>sAuto</c>, <c>sHigh</c>, <c>sSingle</c>, <c>sTwin</c>), and CLAUDE.md requires Def ids
        /// to be <c>type.snake_case</c> — so <c>sAuto</c> becomes <c>sauto</c> here. The rig's own
        /// spelling survives verbatim on <see cref="FuelContainerDef.SizeKey"/>, which is what any
        /// re-bake matches against; the id is an identifier, not a lookup key.</para>
        /// </summary>
        public static string IdFor(FuelStorageKit.SheetEntry s) =>
            $"{IdPrefix}.{s.grade}_{s.type}_{s.size}".ToLowerInvariant();

        [MenuItem("Hidden Harbours/Art/Build Fuel Container Defs + Prefabs", priority = 70)]
        public static void BuildAll()
        {
            var contract = FuelSheetSlicer.LoadContract();
            if (contract == null) return;

            Directory.CreateDirectory(DefFolder);
            Directory.CreateDirectory(PrefabFolder);

            int defs = 0, prefabs = 0;
            var problems = new List<string>();

            try
            {
                AssetDatabase.StartAssetEditing();
                foreach (var sheet in contract.sheets)
                {
                    var def = BuildDef(sheet, problems);
                    if (def == null) continue;
                    defs++;
                    if (BuildPrefab(def)) prefabs++;
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            string msg = $"[FuelContainerDefBuilder] {defs} Defs, {prefabs} prefabs.";
            if (problems.Count > 0)
                Debug.LogError($"{msg} {problems.Count} problems:\n  " + string.Join("\n  ", problems));
            else
                Debug.Log(msg);
        }

        static FuelContainerDef BuildDef(FuelStorageKit.SheetEntry sheet, List<string> problems)
        {
            string sheetPath = $"{FuelStorageKit.OutputFolder}/{sheet.key}.png";

            // ⚠️ spriteMode Multiple is NOT the same as sliced. A fresh import is Multiple with ZERO
            // rects, and LoadAllAssetsAtPath then returns nothing while everything still looks
            // imported. A Def built from that would carry an all-null frame table and draw nothing.
            var byName = AssetDatabase.LoadAllAssetsAtPath(sheetPath)
                                      .OfType<Sprite>()
                                      .ToDictionary(s => s.name, StringComparer.Ordinal);

            int expected = sheet.columns * sheet.rows;
            if (byName.Count != expected)
            {
                problems.Add($"{sheet.key}: {byName.Count} sprites on the sheet, expected {expected}. " +
                             "Slice the kit before building Defs.");
                return null;
            }

            string path = $"{DefFolder}/{sheet.key}.asset";
            var def = AssetDatabase.LoadAssetAtPath<FuelContainerDef>(path);
            bool created = def == null;
            if (created) def = ScriptableObject.CreateInstance<FuelContainerDef>();

            def.Id = IdFor(sheet);
            def.DisplayName = DisplayNameFor(sheet);
            def.VesselType = sheet.type;
            def.SizeKey = sheet.size;
            def.Grade = sheet.grade;
            def.LevelRead = sheet.read;
            def.Carriable = sheet.kind == "carry";
            def.CapacityLitres = sheet.litres;
            def.TareKg = sheet.tare;
            def.FullKg = sheet.fullKg;
            def.Facings = sheet.facings;
            def.FillFractions = sheet.fills.ToArray();

            // Row-major, fill × facings + facing — the order FuelContainerDef.Frame indexes.
            var frames = new Sprite[sheet.rows * sheet.columns];
            for (int row = 0; row < sheet.rows; row++)
                for (int col = 0; col < sheet.columns; col++)
                {
                    string name = sheet.SpriteName(row, col);
                    if (!byName.TryGetValue(name, out var sprite))
                    {
                        problems.Add($"{sheet.key}: missing sprite '{name}'.");
                        return null;
                    }
                    frames[row * sheet.columns + col] = sprite;
                }
            def.Frames = frames;

            if (created) AssetDatabase.CreateAsset(def, path);
            else EditorUtility.SetDirty(def);
            return def;
        }

        /// <summary>
        /// A readable name from the rig's own vocabulary — "20 L Gas Can", "1000 L Diesel Tote".
        /// Flavour only; <see cref="FuelContainerDef.Id"/> is canonical.
        /// </summary>
        static string DisplayNameFor(FuelStorageKit.SheetEntry s)
        {
            string grade = s.grade switch
            {
                "gas" => "Gas", "diesel" => "Diesel", "mixed" => "Mixed", "oil" => "Oil", _ => s.grade,
            };
            string vessel = s.type switch
            {
                "jug" => "Jug", "jerry" => "Can", "nozzle" => "Nozzle", "drum" => "Drum",
                "tote" => "Tote", "skid" => "Skid Tank", "bulk" => "Bulk Tank",
                "pump" => "Dispenser", _ => s.type,
            };

            if (s.litres <= 0) return $"{grade} {vessel}";              // nozzle, dispenser
            string litres = s.litres >= 1000
                ? (s.litres / 1000f).ToString("0.###", CultureInfo.InvariantCulture) + " kL"
                : s.litres.ToString("0.###", CultureInfo.InvariantCulture) + " L";
            return $"{litres} {grade} {vessel}";
        }

        /// <summary>
        /// One placeable prefab per container: a SpriteRenderer showing the mid-fill frame, a
        /// <see cref="FuelLevelPresenter"/>, and <c>YSortSprite</c> on its DEFAULTS.
        ///
        /// <para>⚠️ No sorting constants are parked here. The vessels pivot at their base centre on
        /// the ground plane — measured, and asserted by the slicer against the contract — so
        /// <c>SortPivotYOffset</c> stays 0 and the ADR 0032 band does the rest. Writing a literal
        /// order onto a prefab is what makes decor stop sorting the day the band moves.</para>
        ///
        /// <para><b>Carriables get the same treatment and NO pick-up wiring.</b> Picking a can up
        /// needs an interact verb that does not exist yet; the art is carry-ready and nothing reads
        /// <see cref="FuelContainerDef.Carriable"/>.</para>
        /// </summary>
        static bool BuildPrefab(FuelContainerDef def)
        {
            string path = $"{PrefabFolder}/{def.name}.prefab";
            var go = new GameObject(def.name);
            try
            {
                var sr = go.AddComponent<SpriteRenderer>();
                sr.spriteSortPoint = SpriteSortPoint.Pivot;

                var presenter = go.AddComponent<FuelLevelPresenter>();
                presenter.Container = def;
                presenter.Fill = def.HasLevel ? 0.5f : 0f;
                presenter.Facing = 0;

                go.AddComponent<YSortSprite>();

                // ⚠️ YSortSprite is [ExecuteAlways], so adding it sorts immediately and its own
                // output (1202 — the band's value at world Y = 0) is what gets serialised. That is
                // a CACHED BAND OUTPUT, not a parked constant: OnEnable recomputes it every time.
                // Zeroing it here does not stick — the save re-runs the sort — and chasing it would
                // be fighting the component for a cosmetic diff. The test asserts what matters
                // instead: every prefab carries the SAME order, so no one has authored one.

                PrefabUtility.SaveAsPrefabAsset(go, path);
                return true;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }
    }
}
