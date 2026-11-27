#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using HiddenHarbours.Art.Editor;
using HiddenHarbours.Environment;
using HiddenHarbours.World;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// Builds the seal content from what is already authored: slices <c>Art/Sprites/Creatures/Seal.png</c>,
    /// wires its frames into <c>wildlife.grey_seal</c>, and writes <c>haulout.st_peters.whelps</c> from the
    /// Whelps key scene's <c>SealIso.HAULOUT</c> rows (where each seal lies, how high its rock is, which way
    /// it faces) and its swimming rows (the open water). Idempotent: a second run rewrites the same values
    /// and keeps every sprite id. The species' tunables are left as the owner last set them once it exists.
    /// <c>-executeMethod HiddenHarbours.App.Editor.SealContentSetup.BuildBatch</c>.
    /// </summary>
    public static class SealContentSetup
    {
        public const string SheetPath = "Assets/_Project/Art/Sprites/Creatures/Seal.png";
        public const string DataFolder = "Assets/_Project/Data/Wildlife";
        public const string SpeciesPath = DataFolder + "/WildlifeSpecies_grey_seal.asset";
        public const string KeyScenePath = "Assets/_Project/Data/KeyScenes/KeyScene_stp_whelps_lookout.asset";
        public const string SpeciesId = "wildlife.grey_seal";
        public const string HauloutId = "haulout.st_peters.whelps";

        // The sheet's grid: 6 x 5 cells of 80 x 40 px (2.5 x 1.25 m) holding a ~2 m seal nose to tail; the pivot
        // is the belly's ground contact / the waterline, 32 px down from the cell top, centred.
        public const int Columns = 6, Rows = 5, CellW = 80, CellH = 40, PixelsPerUnit = 32;
        public static readonly Vector2 PivotFromTopLeft = new Vector2(40f, 32f);

        /// <summary>How many real frames each row holds (the rest of the row is blank).</summary>
        public static readonly int[] FramesPerRow = { 6, 4, 6, 4, 5 };

        /// <summary>The dry slide, frame for frame with row 2, as (row, col): its first two frames are row 2's own
        /// (still on the rock, no water drawn), the rest sit in the sheet's spare cells.</summary>
        public static readonly Vector2Int[] SlideDryCells =
            { new Vector2Int(2, 0), new Vector2Int(2, 1), new Vector2Int(1, 4), new Vector2Int(1, 5), new Vector2Int(3, 4), new Vector2Int(3, 5) };

        public const string HauloutRule = "SealIso.HAULOUT";

        /// <summary>Coats by the key scene's coat names (multiplied over the grey sheet).</summary>
        public static readonly string[] CoatNames = { "grey", "brown", "dark" };

        public static string SliceName(int row, int col) => $"Seal_r{row}_f{col}";

        /// <summary>The content, then the scene's host: <c>-executeMethod ...SealContentSetup.BuildAndPlaceBatch</c>.</summary>
        public static void BuildAndPlaceBatch()
        {
            BuildBatch();
            StPetersLayerRefresh.RunSeals(apply: true);
        }

        [MenuItem("Hidden Harbours/Wildlife/Build Seal Content")]
        public static void BuildBatch()
        {
            if (!ApplianceSheetSlicer.Slice(SheetPath, Rows, Columns, CellW, CellH, PivotFromTopLeft, PixelsPerUnit, SliceName))
                throw new InvalidOperationException("The seal sheet did not slice.");
            Directory.CreateDirectory(DataFolder);
            AssetDatabase.Refresh();

            var sprites = AssetDatabase.LoadAllAssetsAtPath(SheetPath).OfType<Sprite>().ToDictionary(s => s.name);
            Sprite[] Row(int r)
            {
                var row = new Sprite[FramesPerRow[r]];
                for (int c = 0; c < row.Length; c++)
                    if (!sprites.TryGetValue(SliceName(r, c), out row[c]))
                        throw new InvalidOperationException($"The seal sheet has no {SliceName(r, c)}.");
                return row;
            }
            Sprite Cell(int r, int c) => sprites.TryGetValue(SliceName(r, c), out Sprite s)
                ? s : throw new InvalidOperationException($"The seal sheet has no {SliceName(r, c)}.");

            var species = AssetDatabase.LoadAssetAtPath<WildlifeSpeciesDef>(SpeciesPath);
            bool made = species == null;
            if (made)
            {
                species = ScriptableObject.CreateInstance<WildlifeSpeciesDef>();
                species.Id = SpeciesId;
                species.DisplayName = "Grey seal";
                species.Coats = new[] { new Color(1f, 1f, 1f), new Color(0.86f, 0.76f, 0.64f), new Color(0.62f, 0.62f, 0.68f) };
                AssetDatabase.CreateAsset(species, SpeciesPath);
            }
            species.RestFrames = Row(0);
            species.AlertFrames = Row(1);
            species.SlideFrames = Row(2);
            species.SlideDryFrames = SlideDryCells.Select(rc => Cell(rc.x, rc.y)).ToArray();
            species.SwimFrames = Row(3);
            species.DiveFrames = Row(4);
            EditorUtility.SetDirty(species);

            var scene = AssetDatabase.LoadAssetAtPath<KeySceneDef>(KeyScenePath)
                ?? throw new InvalidOperationException($"{KeyScenePath} is not an imported key scene.");
            string hauloutPath = StPetersLayerRefresh.WhelpsHauloutPath;
            var haulout = AssetDatabase.LoadAssetAtPath<SealHauloutDef>(hauloutPath);
            if (haulout == null)
            {
                haulout = ScriptableObject.CreateInstance<SealHauloutDef>();
                AssetDatabase.CreateAsset(haulout, hauloutPath);
            }
            haulout.Id = HauloutId;
            haulout.DisplayName = "The Whelps";
            haulout.RegionId = scene.RegionId;
            haulout.KeySceneId = scene.Id;
            haulout.Species = species;
            haulout.Spots = HauloutSpots(scene);
            haulout.SwimPoints = SwimPoints(scene);
            EditorUtility.SetDirty(haulout);
            AssetDatabase.SaveAssets();
            Debug.Log($"[SealContentSetup] {(made ? "made" : "kept")} {SpeciesId}; {HauloutId} has {haulout.Spots.Count} spots, " +
                      $"{haulout.SwimPoints.Length} swim points.");
        }

        /// <summary>True for a key-scene row that is a seal hauled out under the tide rule.</summary>
        public static bool IsHaulOutRow(KeyScenePiece p)
            => p != null && p.Kind == KeySceneDef.KindWildlife && p.StandsOn == KeySceneDef.StandsOnGround
               && p.WhenTide != null && p.WhenTide.Contains(HauloutRule);

        /// <summary>True for a key-scene row that is a seal in the water.</summary>
        public static bool IsSwimRow(KeyScenePiece p)
            => p != null && p.Kind == KeySceneDef.KindWildlife && p.StandsOn == KeySceneDef.StandsOnWater;

        /// <summary>The spot id a row keeps: its id after the key scene's <c>wildlife.stp_seal_</c>.</summary>
        public static string SpotIdOf(string rowId)
        {
            const string prefix = "wildlife.stp_seal_";
            return rowId.StartsWith(prefix, StringComparison.Ordinal) ? rowId.Substring(prefix.Length) : rowId;
        }

        public static string OptionOf(KeyScenePiece p, string key)
            => p.Options?.FirstOrDefault(o => o.Key == key)?.Value ?? "";

        /// <summary>The reef's rocks (key-scene rock rows, the ledges and their outliers _b/_c): a seal lying north of
        /// any one's foot must sort over it.</summary>
        public const string LedgeIdPrefix = "rock.stp_whelp_";

        public static bool IsLedge(KeyScenePiece p) => p.Id != null && p.Id.StartsWith(LedgeIdPrefix, StringComparison.Ordinal);

        /// <summary>How far past the ledge's foot a seal on it sorts (m).</summary>
        public const float SortPastFootMetres = 0.05f;

        /// <summary>How near (m, east-west) a ledge must be for a spot to count as lying on it.</summary>
        public const float LedgeReachMetres = 4f;

        public static List<SealSpot> HauloutSpots(KeySceneDef scene)
        {
            var spots = new List<SealSpot>();
            Vector2[] ledges = scene.Pieces.Where(IsLedge).Select(r => r.At).ToArray();
            foreach (KeyScenePiece p in scene.Pieces.Where(IsHaulOutRow))
            {
                int coat = Array.IndexOf(CoatNames, OptionOf(p, "coat"));
                float sortBelow = 0f;
                foreach (Vector2 at in ledges)
                    if (Mathf.Abs(at.x - p.At.x) <= LedgeReachMetres && p.At.y > at.y && p.At.y - at.y < LedgeReachMetres)
                        sortBelow = Mathf.Max(sortBelow, p.At.y - at.y + SortPastFootMetres);
                spots.Add(new SealSpot
                {
                    Id = SpotIdOf(p.Id),
                    At = p.At,
                    LieMetres = p.Z,
                    Facing = ((p.Dir % 8) + 8) % 8,
                    Coat = coat < 0 ? 0 : coat,
                    SortBelowMetres = (float)Math.Round(sortBelow, 3),
                });
            }
            return spots;
        }

        public static Vector2[] SwimPoints(KeySceneDef scene)
            => scene.Pieces.Where(IsSwimRow).Select(p => p.At).ToArray();

        /// <summary>The ebb and flood heights a haul-out row's tide rule names (absolute metres).</summary>
        public static bool TryReadTideRule(string whenTide, out float ebbBelow, out float floodBelow)
        {
            ebbBelow = floodBelow = 0f;
            return TryReadNumber(whenTide, "\"ebbBelow\":", out ebbBelow) && TryReadNumber(whenTide, "\"floodBelow\":", out floodBelow);
        }

        static bool TryReadNumber(string json, string key, out float value)
        {
            value = 0f;
            if (string.IsNullOrEmpty(json)) return false;
            int i = json.IndexOf(key, StringComparison.Ordinal);
            if (i < 0) return false;
            i += key.Length;
            int j = i;
            while (j < json.Length && "+-.0123456789eE".IndexOf(json[j]) >= 0) j++;
            return float.TryParse(json.Substring(i, j - i), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }
    }
}
#endif
