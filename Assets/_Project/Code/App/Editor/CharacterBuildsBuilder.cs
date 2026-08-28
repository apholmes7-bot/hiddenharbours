#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.World;

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// <b>Gives the cast bodies.</b> Two jobs, in order:
    ///
    /// <list type="number">
    ///   <item>One <see cref="CharacterBuildDef"/> per baked preset, wired to that preset's
    ///   <see cref="CharacterVisualDef"/> — so the world layer asks an NpcDef who it is and never
    ///   holds an art path (rule 2).</item>
    ///   <item>The default CAST MAPPING onto the <see cref="NpcDef"/> assets: who wears which build.</item>
    /// </list>
    ///
    /// <para><b>The mapping lives HERE, in one table, and that is on purpose.</b> It could have lived
    /// on the NpcDef assets alone, but then "who is the storekeeper" would be spread across nine YAML
    /// files and the owner's veto would be nine edits. One table, one re-run, one line in the PR body
    /// for the owner to argue with. ⚠️ Consequently a hand edit to <c>NpcDef.Build</c> in the
    /// inspector is OVERWRITTEN by the next run — change the table, not the asset.</para>
    ///
    /// <para><b>Why the assignments are what they are:</b> the rig's cast is nine bodies and the
    /// island's cast is eight people, but they do not line up by sex — the rig has three grown men
    /// (skipper · deckboss · hand) and the world has four. Basil and Hector therefore share
    /// <c>skipper</c>; they live in different regions and are never on screen together. The two child
    /// builds (<c>boy</c> · <c>girl</c>) go unworn: there are no children in the M1 cast yet, and
    /// inventing one to use a preset would be content the roadmap has not asked for.</para>
    /// </summary>
    public static class CharacterBuildsBuilder
    {
        const string MenuPath = "Hidden Harbours/Art/Import (after a new drop)/Build Character Builds + Cast Mapping";
        const string BuildsFolder = "Assets/_Project/Data/Characters/Builds";
        const string VisualsFolder = "Assets/_Project/Data/Characters";
        const string DataNpcs = "Assets/_Project/Data/NPCs";

        /// <summary>One baked preset → one build asset. Mirrors the bake menu's cast.</summary>
        public static readonly (string preset, string stem)[] Presets =
        {
            ("ginny", "Ginny"), ("skipper", "Skipper"), ("nan", "Nan"),
            ("deckboss", "DeckBoss"), ("packer", "Packer"), ("cutter", "Cutter"),
            ("hand", "Hand"), ("boy", "Boy"), ("girl", "Girl"),
        };

        /// <summary>
        /// <b>THE CAST MAPPING — the owner's to veto.</b> Which of the island's people wears which of
        /// the rig's bodies, and the one-line reason. Matched on role and bearing, not on names:
        /// what makes a schoolhouse read as a schoolhouse is a woman in a shawl standing at it, and
        /// what makes a wharf read as a working wharf is an old man in oilskins watching the water.
        /// </summary>
        public static readonly (string npcAsset, string preset, string why)[] CastMapping =
        {
            ("AuntGinny", "ginny",
             "the rig's own preset is named for her — she is why the cast has a face at all"),

            ("RoseMacIsaac", "cutter",
             "the young neighbour on the green, in a plant apron — the first face you meet, and " +
             "someone who plainly works for a living"),

            ("MargueriteLeBlanc", "packer",
             "the storekeeper: a grown woman in an apron and a kerchief, dressed to stand behind a " +
             "counter all day"),

            ("EileenDoiron", "nan",
             "the schoolteacher — the one woman in the cast NOT dressed for the wet, in a skirt and " +
             "shawl. A one-room school reads as a school the moment she is at its door"),

            ("JuniorPoirier", "hand",
             "Junior digs clams: a young man in a work shirt and a ball cap, which is what a clam " +
             "digger coming up off the flats looks like"),

            ("BasilSamson", "skipper",
             "up the wharf watching the water in oilskins and a sou'wester — the man who has been " +
             "going out longer than you have been alive"),

            ("WendellArsenault", "deckboss",
             "the fish buyer at his truck: a grown man in a quilted vest and a flat cap, the one " +
             "person on the wharf not dressed to get wet because he never leaves it"),

            ("HectorBernard", "skipper",
             "the old boat-yard man among his tired outboards. SHARES the skipper body with Basil — " +
             "different region, never on screen together (see the class note)"),

            ("ClaudetteBoudreau", "packer",
             "the mainland storekeeper, in the same apron and kerchief as the island's. SHARES the " +
             "packer body with Marguerite on Hector's rule — different region, never on screen " +
             "together — and for the same reason: a second counter body is art nobody has cut yet. " +
             "⚠ THE TWO STOREKEEPERS ARE THE ONLY SHARE WHERE BOTH FIGURES ARE FOREGROUND, talked to " +
             "and bought from. Their voices carry the difference until a body can"),
        };

        [MenuItem(MenuPath, priority = 233)]
        public static void Build()
        {
            EnsureFolder(BuildsFolder);

            // ---- 1. a build per baked preset ---------------------------------------------------
            var builds = new Dictionary<string, CharacterBuildDef>(StringComparer.Ordinal);
            int bodied = 0;
            foreach (var (preset, stem) in Presets)
            {
                var def = BuildOne(preset, stem);
                builds[preset] = def;
                if (def.HasBakedBody) bodied++;
            }

            // ---- 2. the cast mapping -------------------------------------------------------------
            var wore = new List<string>();
            var missing = new List<string>();
            foreach (var (npcAsset, preset, _) in CastMapping)
            {
                var npc = AssetDatabase.LoadAssetAtPath<NpcDef>($"{DataNpcs}/{npcAsset}.asset");
                if (npc == null) { missing.Add(npcAsset); continue; }

                if (!builds.TryGetValue(preset, out var build))
                    throw new InvalidOperationException(
                        $"The cast mapping sends '{npcAsset}' to preset '{preset}', which is not in " +
                        $"Presets. Known: {string.Join(", ", Presets.Select(p => p.preset))}. A " +
                        "mapping to a body that does not exist would leave that person invisible.");

                npc.Build = build;
                EditorUtility.SetDirty(npc);
                wore.Add($"{npc.DisplayName} → {preset}");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[CharacterBuildsBuilder] {Presets.Length} builds in {BuildsFolder} " +
                      $"({bodied} with baked sheets behind them). Dressed {wore.Count}/" +
                      $"{CastMapping.Length}: {string.Join(" · ", wore)}. " +
                      "Commit the build assets AND the NpcDef assets they were written onto.");

            if (missing.Count > 0)
                Debug.LogWarning(
                    $"[CharacterBuildsBuilder] No NpcDef for {string.Join(", ", missing)} under " +
                    $"{DataNpcs} — those people keep whatever drew them before. That is the safe " +
                    "outcome, not a silent one: a build wired onto nothing would place an invisible " +
                    "person.");

            if (bodied < Presets.Length)
                Debug.LogWarning(
                    $"[CharacterBuildsBuilder] {Presets.Length - bodied} build(s) have no sheets yet. " +
                    "Run Hidden Harbours ▸ Art ▸ Bake Character Sheets — the cast, then ▸ Import " +
                    "(after a new drop) ▸ Build Character Visual Defs, then re-run this.");
        }

        static CharacterBuildDef BuildOne(string preset, string stem)
        {
            string path = $"{BuildsFolder}/{stem}Build.asset";
            var def = AssetDatabase.LoadAssetAtPath<CharacterBuildDef>(path);
            bool created = def == null;
            if (created) def = ScriptableObject.CreateInstance<CharacterBuildDef>();

            def.Id = $"char_build.{preset}";
            def.Preset = preset;
            def.Visual = AssetDatabase.LoadAssetAtPath<CharacterVisualDef>(
                $"{VisualsFolder}/{CharacterVisualLibraryBuilder.CastAssetName(stem)}.asset");

            // Colour is left EMPTY on every generated build, and that is the correct default rather
            // than an omission: an empty key means "the preset's own", so each of these reads exactly
            // as the art director drew them. The colour fields exist for the wardrobe and the
            // creator to write into — a recolour is a runtime ramp swap, which is the whole point of
            // ADR 0029's split.

            if (created) AssetDatabase.CreateAsset(def, path);
            else EditorUtility.SetDirty(def);
            return def;
        }

        /// <summary>
        /// Headless entry for <c>-executeMethod</c>: the full character data chain in dependency
        /// order — ramps, visual defs, then builds + the mapping (which needs the defs to point at).
        ///
        /// <para>⚠️ Stops short of the SCENE builders, which must run in a real editor.</para>
        /// </summary>
        public static void BuildCharacterDataFromCommandLine()
        {
            try
            {
                CharacterRampsBuilder.Build();
                CharacterVisualLibraryBuilder.Build();
                Build();
                AssetDatabase.SaveAssets();
                EditorApplication.Exit(0);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[CharacterBuildsBuilder] headless character data build failed: {ex}");
                EditorApplication.Exit(1);
            }
        }

        static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;
            string parent = System.IO.Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(folder));
        }
    }
}
#endif
