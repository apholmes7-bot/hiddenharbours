#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.Presets;
using UnityEngine;

namespace HiddenHarbours.Art.Editor
{
    /// <summary>
    /// The <b>Water Preset Library</b> menu — the owner-facing way to switch the sea's MOOD and to bank his own
    /// tunes, without opening the shader or editing a single property by hand.
    ///
    /// <para><b>What a "preset" controls here.</b> At runtime the <see cref="HiddenHarbours.Art.WaterSurface"/>
    /// component OVERRIDES the sim-driven knobs — <c>_Chop</c>, <c>_Roughness</c>, <c>_Flow</c>, <c>_FlowDir</c>,
    /// <c>_WindDir</c> — from the deterministic sea-state every tick, so <i>calm vs storm happens automatically
    /// with the weather</i> (ADR 0010 / ADR 0013). A preset therefore expresses MOOD through the
    /// non-sim-overridden VISUAL knobs only: the palette (deep/shallow/foam/spec/caustic/reflection colours), the
    /// foam CHARACTER (density, threshold, whitecap lifecycle), the rolling SWELL, the SPECULAR, the CAUSTICS, and
    /// the sky REFLECTION. The structural knobs (height map, water level, the <c>_Use*</c> toggles, the texture
    /// refs, the shoreward bias) are identical across every variant — so applying one only changes the LOOK,
    /// never the gameplay waterline, and every variant stays a complete, valid <c>HiddenHarbours/Water</c>
    /// material (rule 5, P1 integrity).</para>
    ///
    /// <para><b>Three jobs (all under <c>Hidden Harbours ▸ Art ▸ Water Presets</c>):</b>
    /// <list type="number">
    /// <item><b>Apply a variant → the live <c>Water.mat</c></b> (the recommended non-dev path). Copies the chosen
    /// variant's shader properties onto the shipped <c>Water.mat</c> via
    /// <see cref="Material.CopyPropertiesFromMaterial"/>, then dirties + saves it. Because the St Peters Sea plane
    /// uses <c>Water.mat</c> (StPetersBuilder hard-sets <c>sharedMaterial = Water.mat</c>), this swaps the in-game
    /// look immediately AND survives a "Build St Peters Scene" re-run. Undo-able; asks before overwriting.</item>
    /// <item><b>Generate native Unity <c>.preset</c> assets</b> from the variants. For each variant material it
    /// creates a real <see cref="UnityEditor.Presets.Preset"/> (<c>new Preset(mat)</c>) next to it, so the owner
    /// gets genuine Unity "material presets" he can drag onto any material's Inspector — authored by Unity at
    /// runtime (reliable), never hand-written YAML.</item>
    /// <item><b>Save the current <c>Water.mat</c> as a new variant</b> — duplicates the live, tuned material into
    /// the WaterPresets folder under a name the owner picks, so he can bank his own tweaks as a preset.</item>
    /// </list></para>
    ///
    /// <para><b>Lane &amp; rules.</b> art-pipeline / tools-editor authoring aid (Art.Editor); visual-only — it
    /// never touches the sim or save (rule 5); every value lives on the material assets, not in code (rule 6). The
    /// shipped <c>Water.mat</c> is changed ONLY by the explicit "Apply" command (that IS the intent), with a
    /// confirm + Undo. Menu: <c>Hidden Harbours ▸ Art ▸ Water Presets</c>.</para>
    /// </summary>
    public static class WaterPresetMenu
    {
        /// <summary>The live, owner-art-directed material the St Peters Sea plane uses (StPetersBuilder assigns it).</summary>
        private const string LiveWaterMaterialPath = "Assets/_Project/Art/Materials/Water.mat";

        /// <summary>Where the variant library + the generated <c>.preset</c> assets live.</summary>
        private const string PresetsFolder = "Assets/_Project/Art/Materials/WaterPresets";

        // The shipped variant library. Names are the file stems under PresetsFolder. Each is a complete, valid
        // HiddenHarbours/Water material differing from the base ONLY in mood knobs (see the class summary).
        private const string NorthAtlantic = "Water_NorthAtlantic";
        private const string GlassyCalm    = "Water_GlassyCalm";
        private const string StormGrey     = "Water_StormGrey";
        private const string FoggySmother  = "Water_FoggySmother";
        private const string WarmShelter   = "Water_WarmShelter";

        // ---------------------------------------------------------------------------------------------------
        // 1) APPLY a variant onto the live Water.mat (the recommended non-dev path).
        //    One menu item per variant, grouped under "Apply to live Water". CopyPropertiesFromMaterial copies
        //    every shader property (colours, floats, textures, keywords) from the variant onto Water.mat, so the
        //    result is byte-identical in look to the variant while remaining the SAME asset the scene references.
        // ---------------------------------------------------------------------------------------------------

        [MenuItem("Hidden Harbours/Art/Water Presets/Apply to live Water/North Atlantic (default home)", priority = 0)]
        public static void ApplyNorthAtlantic() => ApplyVariant(NorthAtlantic);

        [MenuItem("Hidden Harbours/Art/Water Presets/Apply to live Water/Glassy Calm (mirror showcase)", priority = 1)]
        public static void ApplyGlassyCalm() => ApplyVariant(GlassyCalm);

        [MenuItem("Hidden Harbours/Art/Water Presets/Apply to live Water/Storm Grey (cold gloom, teeth)", priority = 2)]
        public static void ApplyStormGrey() => ApplyVariant(StormGrey);

        [MenuItem("Hidden Harbours/Art/Water Presets/Apply to live Water/Foggy Smother (pale, eerie)", priority = 3)]
        public static void ApplyFoggySmother() => ApplyVariant(FoggySmother);

        [MenuItem("Hidden Harbours/Art/Water Presets/Apply to live Water/Warm Shelter (gentle, warmer)", priority = 4)]
        public static void ApplyWarmShelter() => ApplyVariant(WarmShelter);

        /// <summary>
        /// Copies <paramref name="variantName"/>'s shader properties onto the live <c>Water.mat</c> (after a
        /// confirm), records Undo, dirties + saves. Validates the shaders match first so a stray material can't
        /// corrupt the look.
        /// </summary>
        public static void ApplyVariant(string variantName)
        {
            string variantPath = $"{PresetsFolder}/{variantName}.mat";
            var variant = AssetDatabase.LoadAssetAtPath<Material>(variantPath);
            if (variant == null)
            {
                EditorUtility.DisplayDialog(
                    "Water Presets",
                    $"Could not find the variant material at:\n{variantPath}\n\nMake sure the WaterPresets library is present.",
                    "OK");
                return;
            }

            var live = AssetDatabase.LoadAssetAtPath<Material>(LiveWaterMaterialPath);
            if (live == null)
            {
                EditorUtility.DisplayDialog(
                    "Water Presets",
                    $"Could not find the live water material at:\n{LiveWaterMaterialPath}",
                    "OK");
                return;
            }

            if (live.shader != variant.shader)
            {
                EditorUtility.DisplayDialog(
                    "Water Presets",
                    $"'{variantName}' uses a different shader ('{(variant.shader != null ? variant.shader.name : "null")}') " +
                    $"than the live material ('{(live.shader != null ? live.shader.name : "null")}'). Aborting so the live look isn't corrupted.",
                    "OK");
                return;
            }

            bool ok = EditorUtility.DisplayDialog(
                "Apply water preset",
                $"Overwrite the live Water.mat look with the '{variantName}' preset?\n\n" +
                "The in-game sea (and any scene using Water.mat) will switch to this mood. Calm/storm still " +
                "happens automatically with the weather — only the palette / foam / reflection mood changes.\n\n" +
                "This is Undo-able (Edit ▸ Undo).",
                "Apply", "Cancel");
            if (!ok) return;

            Undo.RecordObject(live, $"Apply water preset '{variantName}'");
            live.CopyPropertiesFromMaterial(variant);
            EditorUtility.SetDirty(live);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                $"[WaterPresetMenu] Applied '{variantName}' onto {LiveWaterMaterialPath}. The Sea now reads this mood " +
                "(survives Build St Peters Scene). Undo to revert.");
        }

        // ---------------------------------------------------------------------------------------------------
        // 2) GENERATE native Unity .preset assets from the variants (real, Unity-authored material presets).
        //    We do NOT hand-write .preset YAML (fragile) — Unity builds them at runtime from each variant.
        // ---------------------------------------------------------------------------------------------------

        [MenuItem("Hidden Harbours/Art/Water Presets/Generate native .preset assets", priority = 40)]
        public static void GeneratePresetAssets()
        {
            string[] variants = { NorthAtlantic, GlassyCalm, StormGrey, FoggySmother, WarmShelter };
            int made = 0;
            foreach (string variantName in variants)
            {
                string variantPath = $"{PresetsFolder}/{variantName}.mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(variantPath);
                if (mat == null)
                {
                    Debug.LogWarning($"[WaterPresetMenu] Skipping '{variantName}' — material not found at {variantPath}.");
                    continue;
                }

                var preset = new Preset(mat);
                string presetPath = $"{PresetsFolder}/{variantName}.preset";
                AssetDatabase.CreateAsset(preset, AssetDatabase.GenerateUniqueAssetPath(presetPath));
                made++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(
                $"[WaterPresetMenu] Generated {made} native Unity .preset asset(s) in {PresetsFolder}. Drag a .preset " +
                "onto any material's Inspector (or the preset selector) to apply that water mood.");
        }

        // ---------------------------------------------------------------------------------------------------
        // 3) SAVE the current (tuned) live Water.mat as a NEW variant in the library.
        // ---------------------------------------------------------------------------------------------------

        [MenuItem("Hidden Harbours/Art/Water Presets/Save current Water as new variant...", priority = 41)]
        public static void SaveCurrentAsVariant()
        {
            var live = AssetDatabase.LoadAssetAtPath<Material>(LiveWaterMaterialPath);
            if (live == null)
            {
                EditorUtility.DisplayDialog(
                    "Water Presets",
                    $"Could not find the live water material at:\n{LiveWaterMaterialPath}",
                    "OK");
                return;
            }

            // Suggest a "Water_MyTune" name under the WaterPresets folder; let the owner pick the file.
            string suggested = "Water_MyTune.mat";
            string chosen = EditorUtility.SaveFilePanelInProject(
                "Save current Water.mat as a new variant",
                suggested,
                "mat",
                "Bank the current Water.mat tune as a reusable preset variant.",
                PresetsFolder);
            if (string.IsNullOrEmpty(chosen)) return; // owner cancelled

            // Duplicate the live material asset to the chosen path (a full, valid material copy).
            if (!AssetDatabase.CopyAsset(LiveWaterMaterialPath, chosen))
            {
                EditorUtility.DisplayDialog(
                    "Water Presets",
                    $"Failed to copy the live material to:\n{chosen}",
                    "OK");
                return;
            }

            // Rename the copy's internal material name to match its file stem (so the Inspector reads cleanly).
            var copy = AssetDatabase.LoadAssetAtPath<Material>(chosen);
            if (copy != null)
            {
                copy.name = Path.GetFileNameWithoutExtension(chosen);
                EditorUtility.SetDirty(copy);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<Material>(chosen));
            Debug.Log(
                $"[WaterPresetMenu] Saved the current Water.mat tune as a new variant: {chosen}. It now appears in " +
                "the WaterPresets library (and 'Generate native .preset assets' will include it next run if you add " +
                "it to the list, or you can drag it onto a material directly).");
        }
    }
}
#endif
