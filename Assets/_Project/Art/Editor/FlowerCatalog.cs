#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Art.Editor
{
    /// <summary>
    /// The one shared answer to "what flowers exist, and which sprite is which pose?" — read off the SHEETS ON
    /// DISK, never from a hard-coded species list. <see cref="DecorPrefabBuilder"/>, the
    /// <see cref="FlowerPaintTool"/> and the tests all go through here, so they cannot drift from each other or
    /// from the art director's next drop: add a <c>FoxgloveSingle.png</c> and it simply appears.
    ///
    /// <para><b>The Lupin irregularity is real and is handled explicitly.</b> Seven species ship a full
    /// Single/Clump/Patch set. Lupin does not: it ships FOUR COLOUR VARIANTS (<c>LupinBlue</c>, <c>LupinPink</c>,
    /// <c>LupinPurple</c>, <c>LupinWhite</c>) with a Single and a Clump each, but only ONE shared
    /// <c>LupinPatch</c> — no per-colour patch. So the colour axis exists for Lupin alone, and any
    /// "species x 3 tiers" grid has a hole in it. Rather than drop the patch or crash on the gap, a Lupin colour
    /// FALLS BACK to the shared LupinPatch for its Patch tier (<see cref="FlowerSpecies.PatchSheet"/>) — which is
    /// plainly what one shared patch was drawn for. <c>LupinPatch</c> itself is therefore NOT listed as a species
    /// of its own; it would be a phantom "Lupin" with no stem.</para>
    ///
    /// <para><b>Cell indexing.</b> The slicer names slices <c>&lt;Stem&gt;_&lt;index&gt;</c> row-major from the
    /// TOP-LEFT cell, so <c>index = row * Cols + col</c>. Columns are the 4 hand-drawn sway poses (the shader
    /// picks between them; see HiddenHarboursFlower.shader) and ROWS are what the author chooses: bloom stages on
    /// Single, variants on Patch. So authoring only ever picks a ROW, and always takes column 0 — the neutral
    /// pose, and the one the shader treats as phase zero.</para>
    /// </summary>
    public static class FlowerCatalog
    {
        public const string FlowersRoot = "Assets/_Project/Art/Foliage/Flowers";

        public const string SingleMaterialPath = "Assets/_Project/Art/Materials/Flower_Single.mat";
        public const string ClumpMaterialPath = "Assets/_Project/Art/Materials/Flower_Clump.mat";
        public const string PatchMaterialPath = "Assets/_Project/Art/Materials/Flower_Patch.mat";

        /// <summary>The shared Lupin patch — the one sheet with no species of its own (see the class doc).</summary>
        public const string SharedLupinPatchStem = "LupinPatch";

        public enum Tier { Single, Clump, Patch }

        /// <summary>The sheet grid for one tier — MUST agree with FoliageSheetSlicer's tier table (asserted by
        /// FlowerCatalogTests) and with the _Cols/_Rows on that tier's material (asserted by FlowerMaterialTests).</summary>
        public readonly struct TierGrid
        {
            public readonly int Cols, Rows, CellW, CellH;
            public TierGrid(int cols, int rows, int cellW, int cellH)
            { Cols = cols; Rows = rows; CellW = cellW; CellH = cellH; }
            public int Count => Cols * Rows;
        }

        /// <summary>Every tier is exactly 4 sway columns — that is what makes the shader's column select work.</summary>
        public static TierGrid GridFor(Tier tier) => tier switch
        {
            Tier.Single => new TierGrid(4, 3, 32, 48),
            Tier.Clump => new TierGrid(4, 1, 48, 46),
            Tier.Patch => new TierGrid(4, 2, 44, 26),
            _ => throw new ArgumentOutOfRangeException(nameof(tier)),
        };

        public static string MaterialPathFor(Tier tier) => tier switch
        {
            Tier.Single => SingleMaterialPath,
            Tier.Clump => ClumpMaterialPath,
            Tier.Patch => PatchMaterialPath,
            _ => throw new ArgumentOutOfRangeException(nameof(tier)),
        };

        public static Material MaterialFor(Tier tier) =>
            AssetDatabase.LoadAssetAtPath<Material>(MaterialPathFor(tier));

        /// <summary>One authorable flower: a species key (<c>WildRose</c>, <c>LupinBlue</c>) and the sheets it has.</summary>
        public sealed class FlowerSpecies
        {
            /// <summary>The key as it appears in filenames, e.g. <c>WildRose</c> or <c>LupinBlue</c>.</summary>
            public string Key;
            /// <summary>Human label for the tool's dropdown, e.g. "Wild Rose" / "Lupin (Blue)".</summary>
            public string Label;
            /// <summary>Sheet stem per tier, or null where the species has no sheet for it.</summary>
            public string SingleSheet, ClumpSheet, PatchSheet;
            /// <summary>True when <see cref="PatchSheet"/> is the SHARED LupinPatch rather than this colour's own.</summary>
            public bool PatchIsShared;

            public string SheetFor(Tier tier) => tier switch
            {
                Tier.Single => SingleSheet,
                Tier.Clump => ClumpSheet,
                Tier.Patch => PatchSheet,
                _ => null,
            };

            public bool Has(Tier tier) => SheetFor(tier) != null;
        }

        /// <summary>
        /// Scan <see cref="FlowersRoot"/> and build the species list. Ordered by label so the tool's dropdown is
        /// stable. Never throws on odd art: a sheet whose stem matches no tier suffix is simply ignored.
        /// </summary>
        public static List<FlowerSpecies> Scan()
        {
            var byKey = new Dictionary<string, FlowerSpecies>(StringComparer.Ordinal);
            bool sharedLupinPatch = false;

            foreach (string stem in SheetStems())
            {
                if (string.Equals(stem, SharedLupinPatchStem, StringComparison.Ordinal))
                {
                    // Not a species: it is the Patch every Lupin COLOUR shares. Wired in below.
                    sharedLupinPatch = true;
                    continue;
                }
                if (!TrySplit(stem, out string key, out Tier tier)) continue;

                if (!byKey.TryGetValue(key, out var sp))
                {
                    sp = new FlowerSpecies { Key = key, Label = Humanize(key) };
                    byKey.Add(key, sp);
                }
                switch (tier)
                {
                    case Tier.Single: sp.SingleSheet = stem; break;
                    case Tier.Clump: sp.ClumpSheet = stem; break;
                    case Tier.Patch: sp.PatchSheet = stem; break;
                }
            }

            // The Lupin colours have no patch of their own — lend them the shared one (see the class doc).
            if (sharedLupinPatch)
            {
                foreach (var sp in byKey.Values)
                {
                    if (!sp.Key.StartsWith("Lupin", StringComparison.Ordinal)) continue;
                    if (sp.PatchSheet != null) continue;   // a colour that later gets its own patch keeps it
                    sp.PatchSheet = SharedLupinPatchStem;
                    sp.PatchIsShared = true;
                }
            }

            return byKey.Values.OrderBy(s => s.Label, StringComparer.Ordinal).ToList();
        }

        /// <summary>Every sheet stem under <see cref="FlowersRoot"/>, sorted. Reads the FILES, not a list.</summary>
        public static List<string> SheetStems()
        {
            var stems = new List<string>();
            if (!Directory.Exists(FlowersRoot)) return stems;
            foreach (string path in Directory.GetFiles(FlowersRoot, "*.png", SearchOption.TopDirectoryOnly))
                stems.Add(Path.GetFileNameWithoutExtension(path));
            stems.Sort(StringComparer.Ordinal);
            return stems;
        }

        /// <summary>Split a sheet stem into its species key and tier: <c>WildRoseClump</c> -> (WildRose, Clump).</summary>
        public static bool TrySplit(string stem, out string key, out Tier tier)
        {
            foreach (Tier t in (Tier[])Enum.GetValues(typeof(Tier)))
            {
                string suffix = t.ToString();
                if (stem.Length > suffix.Length && stem.EndsWith(suffix, StringComparison.Ordinal))
                {
                    key = stem.Substring(0, stem.Length - suffix.Length);
                    tier = t;
                    return true;
                }
            }
            key = null;
            tier = default;
            return false;
        }

        public static string SheetPath(string stem) => $"{FlowersRoot}/{stem}.png";

        /// <summary>
        /// Load one CELL of a sheet by (row, column). The sheets import spriteMode MULTIPLE, so
        /// <c>LoadAssetAtPath&lt;Sprite&gt;</c> returns NULL for them — LoadAllAssetsAtPath is the only way in.
        /// Slices are matched by NAME (<c>&lt;stem&gt;_&lt;index&gt;</c>), never by array order, which
        /// LoadAllAssetsAtPath does not promise. Returns null (no throw) if the sheet or cell is missing, so a
        /// partial art drop still builds what it can.
        /// </summary>
        public static Sprite LoadCell(string stem, Tier tier, int row, int col)
        {
            var grid = GridFor(tier);
            if (row < 0 || row >= grid.Rows || col < 0 || col >= grid.Cols) return null;
            int index = row * grid.Cols + col;
            string want = $"{stem}_{index}";
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(SheetPath(stem)))
                if (o is Sprite s && string.Equals(s.name, want, StringComparison.Ordinal))
                    return s;
            return null;
        }

        /// <summary>The neutral (column 0) pose of a given row — what authoring places. See the class doc.</summary>
        public static Sprite LoadNeutral(string stem, Tier tier, int row) => LoadCell(stem, tier, row, 0);

        /// <summary>All slices of a sheet, ordered by their index (never by LoadAllAssetsAtPath's order).</summary>
        public static Sprite[] LoadAllCells(string stem, Tier tier)
        {
            var grid = GridFor(tier);
            var sprites = new Sprite[grid.Count];
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(SheetPath(stem)))
            {
                if (!(o is Sprite s)) continue;
                int us = s.name.LastIndexOf('_');
                if (us < 0) continue;
                if (!int.TryParse(s.name.Substring(us + 1), out int idx)) continue;
                if (idx >= 0 && idx < sprites.Length) sprites[idx] = s;
            }
            return sprites;
        }

        /// <summary>"LupinBlue" -> "Lupin (Blue)"; "WildRose" -> "Wild Rose". Display only.</summary>
        private static string Humanize(string key)
        {
            foreach (string colour in new[] { "Blue", "Pink", "Purple", "White" })
                if (key.StartsWith("Lupin", StringComparison.Ordinal) && key == "Lupin" + colour)
                    return $"Lupin ({colour})";

            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < key.Length; i++)
            {
                if (i > 0 && char.IsUpper(key[i]) && !char.IsUpper(key[i - 1])) sb.Append(' ');
                sb.Append(key[i]);
            }
            return sb.ToString();
        }
    }
}
#endif
