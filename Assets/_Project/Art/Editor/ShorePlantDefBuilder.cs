#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Art;

namespace HiddenHarbours.Art.Editor
{
    /// <summary>
    /// Builds the <see cref="ShorePlantDef"/> assets the runtime reads — one per species — from the
    /// committed contract and the sliced tide-axis sheets.
    ///
    /// <para><b>⭐ WHY A BUILDER AND NOT SIXTEEN HAND-AUTHORED ASSETS.</b> Two of the numbers on a Def
    /// are not opinions: the tide LADDER (what water-over-own-ground each baked column was rendered
    /// at) and the species' true standing height. Both come straight out of the contract, and both
    /// silently break the art if they drift — a ladder one rung off makes every plant of that species
    /// wear the wrong tide, and it looks like an art bug, not a data one. So they are derived, every
    /// time, from the same contract the bake was checked against. Everything an owner might actually
    /// want to tune (sorting orders) is left alone on a re-run.</para>
    ///
    /// <para><b>The ladder, precisely.</b> The contract publishes tide steps as ABSOLUTE water levels
    /// (metres above chart datum) and each zone's nominal base. The rig resolves a plant's state from
    /// <c>tideM − zone.base</c>, so the ladder this writes is exactly that difference per column. At
    /// runtime the live value is <c>WaterLevelAt(t) − ElevationAt(pos)</c> — the plant's REAL ground,
    /// not its nominal zone — and the nearest rung wins. That is what makes a plant on a shelf swap
    /// state on its own depth instead of on the tide clock.</para>
    ///
    /// <para><b>Sway row 0.</b> Only the resting row is wired (see <see cref="ShorePlantDef"/>); the
    /// other three are baked and on disk for a later wind pass.</para>
    /// </summary>
    public static class ShorePlantDefBuilder
    {
        public const string DefFolder = "Assets/_Project/Data/Decor/ShorePlants";

        /// <summary>The sheet slice the runtime wires: summer, full growth, variant 0 — the same
        /// default <c>ShorePlantBakeMenu</c> bakes, and the gameplay-minimum set.</summary>
        public const string Season = ShorePlantBaker_Defaults.Season;

        public const string Stage = ShorePlantBaker_Defaults.Stage;
        public const int Variant = ShorePlantBaker_Defaults.Variant;

        /// <summary>The resting sway row — a static plant is one standing still, not one frozen
        /// mid-gust.</summary>
        public const int RestSwayRow = 0;

        /// <summary>Sorting defaults. The submerged order must land in the gap between the painted
        /// seabed (−21…−18) and the Sea plane (−5): that gap is where the sea's own shallow
        /// transparency reveals a bed. Owner-tunable per asset afterwards; only written on create.</summary>
        public const int SubmergedSortingOrder = -8;

        public const int EmergentSortingOrder = 3;

        [MenuItem("Hidden Harbours/Dev/Build Shore Plant Defs (16)", priority = 146)]
        public static void BuildAllMenu()
        {
            int built = BuildAll(out int skipped, out var problems);
            string msg = $"[ShorePlantDefBuilder] Built/updated {built} shore plant Def(s) " +
                         $"({skipped} skipped — no sliced sheet on disk yet).";
            if (problems.Count > 0)
                Debug.LogWarning(msg + "\n  " + string.Join("\n  ", problems));
            else
                Debug.Log(msg);
        }

        /// <summary>
        /// Create or refresh a Def per species. A species whose sheet has not been baked is SKIPPED,
        /// not stubbed: this kit bakes to order, so a missing sheet is its normal resting state, and
        /// a Def with null sprites would fail much later as an invisible plant.
        /// </summary>
        public static int BuildAll(out int skipped, out List<string> problems)
        {
            skipped = 0;
            problems = new List<string>();

            var contract = ShorePlantCatalog.Load();
            if (contract == null)
            {
                problems.Add("the contract did not load — nothing built.");
                return 0;
            }

            Directory.CreateDirectory(DefFolder);

            int built = 0;
            foreach (string key in ShorePlantCatalog.SpeciesKeys)
            {
                var entry = contract.Find(key);
                if (entry == null) { problems.Add($"{key}: not in the contract."); continue; }

                string stem = ShorePlantCatalog.TideSheetStem(key, Season, Stage, Variant);
                string sheetPath = $"{ShorePlantCatalog.PlantsRoot}{stem}.png";
                if (!File.Exists(sheetPath)) { skipped++; continue; }

                var sprites = LoadTideRow(sheetPath, stem, out string why);
                if (sprites == null) { problems.Add($"{key}: {why}"); continue; }

                if (BuildOne(key, entry, contract, sprites, stem)) built++;
            }

            AssetDatabase.SaveAssets();
            return built;
        }

        /// <summary>
        /// One baked DATA sheet as a whole texture, or null when it was never baked.
        ///
        /// <para>⚠️ <b>A Texture2D, not a Sprite, and that is the contract.</b> The shared lit path
        /// samples every sheet at the ALBEDO's uv through the ALBEDO's mesh — so these are texture
        /// lookups, never their own sprites. A sliced sheet imported Tight has a SMALLER mesh than the
        /// albedo (the keyline ring has coverage but no relief), and sampling through it drops the
        /// outline. Loading the texture asset side-steps the whole question.</para>
        /// </summary>
        public static Texture2D LoadSheet(string stem, ShorePlantCatalog.Channel channel)
        {
            string path = ShorePlantCatalog.SheetPath(stem, channel);
            return File.Exists(path) ? AssetDatabase.LoadAssetAtPath<Texture2D>(path) : null;
        }

        /// <summary>
        /// The five tide columns of the RESTING sway row, in low→high order. Name-matched
        /// (<c>&lt;stem&gt;_c{col}_f0</c>), never index-ordered: on a Multiple-mode re-import
        /// sub-sprite enumeration order is unstable (the sprite-refs trap), and a shuffled tide axis
        /// would make plants wear the wrong water with nothing to point at.
        /// </summary>
        public static Sprite[] LoadTideRow(string sheetPath, string stem, out string why)
        {
            why = null;
            var all = AssetDatabase.LoadAllAssetsAtPath(sheetPath).OfType<Sprite>().ToArray();
            if (all.Length == 0)
            {
                why = $"'{sheetPath}' yielded no sprites. A fresh Multiple-mode import has EMPTY " +
                      "rects — run Hidden Harbours ▸ Dev ▸ Slice Shore Plant Sheets first.";
                return null;
            }

            var row = new Sprite[ShorePlantTideMath.TideStates];
            for (int c = 0; c < row.Length; c++)
            {
                string name = $"{stem}_c{c}_f{RestSwayRow}";
                row[c] = all.FirstOrDefault(s => s.name == name);
                if (row[c] == null)
                {
                    why = $"'{sheetPath}' has no sprite named '{name}' ({all.Length} sprite(s) found). " +
                          "The slicer names by GEOMETRY — column then sway frame.";
                    return null;
                }
            }
            return row;
        }

        static bool BuildOne(string key, ShorePlantCatalog.SpeciesEntry entry,
                             ShorePlantCatalog.Contract contract, Sprite[] sprites, string stem)
        {
            string assetPath = $"{DefFolder}/{key}.asset";
            var def = AssetDatabase.LoadAssetAtPath<ShorePlantDef>(assetPath);
            bool created = def == null;
            if (created)
            {
                def = ScriptableObject.CreateInstance<ShorePlantDef>();
                def.SubmergedSortingOrder = SubmergedSortingOrder;
                def.EmergentSortingOrder = EmergentSortingOrder;
            }

            def.Id = $"shoreplant.{ToSnake(key)}";
            def.SpeciesKey = key;
            def.Zone = entry.Zone;
            // The species entry carries its OWN zone base, so the zone table is only cross-checked
            // here — a disagreement means the contract was hand-edited, which is the one thing its
            // own header forbids.
            def.ZoneBaseM = entry.BaseM;
            float fromZoneTable = ZoneBase(contract, entry.Zone);
            if (!Mathf.Approximately(entry.BaseM, fromZoneTable))
                Debug.LogWarning(
                    $"[ShorePlantDefBuilder] {key}: species base {entry.BaseM} m disagrees with the " +
                    $"'{entry.Zone}' zone's {fromZoneTable} m. Re-export the contract from the rig — " +
                    "do not hand-edit it.");
            def.StandingHeightM = entry.HeightM;
            def.Algae = entry.Algae;
            def.TideSprites = sprites;
            def.TideLadderOverM = Ladder(contract, def.ZoneBaseM);

            // ⚠ ShorePlantDef.PlantedScale is deliberately NOT written here. Everything above is
            // DERIVED from the contract and is rewritten on every run; PlantedScale is AUTHORED — how
            // big a TYPICAL individual of this species is drawn, which is a placement judgment with a
            // real plant behind it and not something the rig knows (the rig bakes full growth, and it
            // is right to). Re-running this builder after a re-bake must not silently reset the whole
            // shore to full growth. Same reason the two sorting orders are only set on CREATE.

            // The baked light channels, as whole TEXTURES rather than sprites. That is not a shortcut:
            // the shared lit path samples every sheet at the ALBEDO's uv through the ALBEDO's mesh, so
            // giving these their own sprites would give them their own (Tight, and therefore SMALLER)
            // meshes and lose the outline. See SpriteLitDecor.hlsl's header.
            //
            // A species whose sheets were baked before the light pass existed simply has none, and the
            // plant draws flat — LoadSheet returns null and the Def stays valid (see IsComplete).
            def.LightSheet = LoadSheet(stem, ShorePlantCatalog.Channel.Light);
            def.TideStateSheet = LoadSheet(stem, ShorePlantCatalog.Channel.State);
            if (def.LightSheet == null)
                Debug.LogWarning(
                    $"[ShorePlantDefBuilder] {key}: no '{ShorePlantCatalog.SheetPath(stem, ShorePlantCatalog.Channel.Light)}' " +
                    "on disk, so this species will draw UNLIT. Re-bake the kit (Hidden Harbours ▸ Dev ▸ " +
                    "Bake Shore Plant Sheets) to emit the light channel.");

            if (created) AssetDatabase.CreateAsset(def, assetPath);
            else EditorUtility.SetDirty(def);
            return true;
        }

        /// <summary>Each baked column's water-over-own-ground, low → high. The whole reason the Def
        /// exists: the runtime cannot read the contract, and this is the only shape of it the runtime
        /// needs.</summary>
        public static float[] Ladder(ShorePlantCatalog.Contract contract, float zoneBaseM)
        {
            var ladder = new float[ShorePlantTideMath.TideStates];
            for (int c = 0; c < ladder.Length; c++)
            {
                var step = contract.Tide.Steps.FirstOrDefault(s => s.Key == ShorePlantCatalog.TideKeys[c]);
                ladder[c] = (step?.WaterM ?? 0f) - zoneBaseM;
            }
            return ladder;
        }

        public static float ZoneBase(ShorePlantCatalog.Contract contract, string zoneKey) =>
            contract.Zones.FirstOrDefault(z => z.Key == zoneKey)?.BaseM ?? 0f;

        /// <summary>`SugarKelp` → `sugar_kelp`, for the append-only Def id (CLAUDE.md §5).</summary>
        public static string ToSnake(string pascal)
        {
            var sb = new System.Text.StringBuilder(pascal.Length + 4);
            for (int i = 0; i < pascal.Length; i++)
            {
                if (i > 0 && char.IsUpper(pascal[i])) sb.Append('_');
                sb.Append(char.ToLowerInvariant(pascal[i]));
            }
            return sb.ToString();
        }
    }

    /// <summary>The bake slice the Defs are wired against, in one place so the builder and the tests
    /// cannot pick different sheets. Mirrors <c>ShorePlantBaker</c>'s defaults, which live in the
    /// Tools assembly and are not referenced from here.</summary>
    internal static class ShorePlantBaker_Defaults
    {
        public const string Season = "summer";
        public const string Stage = "full";
        public const int Variant = 0;
    }
}
#endif
