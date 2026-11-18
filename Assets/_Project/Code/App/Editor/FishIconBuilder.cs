#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Art.Editor;   // ArtImportPipeline.ApplyLockedSettings — the one import lock
using HiddenHarbours.Core;
using HiddenHarbours.Fishing;

namespace HiddenHarbours.App.Editor
{
    /// <summary>
    /// <b>The fish icons: the new fish art, trimmed tight</b> so the fish fills the catch card's and the
    /// sell screen's icon slot the way the old flat 48x32 icons did.
    ///
    /// <para><b>Why a separate file and not a sprite rect on the deck sheet.</b> The icon is the large
    /// rung's deck lay (<c>Fish_&lt;sp&gt;_lg_deck</c>, cell <see cref="SourceCell"/>), but that cell is
    /// 64x80 with the fish drawn in the middle, so in a square slot with preserveAspect the fish came out
    /// at about 40% of the slot's width. A tighter rect on the deck sheet would fix it, but the deck sheet
    /// belongs to the slicer: its sprite count and names are pinned by the slice tests and a re-slice
    /// rewrites every rect. So the icon is its own small PNG, cut from that cell to the fish's opaque
    /// bounds, imported with the locked art settings as one Single sprite.</para>
    ///
    /// <para><b>Read, never typed.</b> The species come from the baked sheets on disk, the cell from the
    /// sheet's own slice. Re-run this after a fish re-bake; the tests compare every committed icon with
    /// the cell it was cut from, so a stale icon fails CI rather than drifting.</para>
    ///
    /// <para><b>It also wires them</b>: <c>Resources/IconLibrary</c> (the catch card, the sell screen) and
    /// each species def's <c>Sprite</c> point at the icon, matched by the rig's species key (the id's last
    /// word: <c>fish.atlantic_cod</c> is <c>cod</c>).</para>
    /// </summary>
    public static class FishIconBuilder
    {
        const string MenuPath = "Hidden Harbours/Art/Import (after a new drop)/Build Fish Icons";
        const string DataFish = "Assets/_Project/Data/Fish";
        const string IconLibraryPath = "Assets/_Project/Resources/IconLibrary.asset";

        /// <summary>Where the baked fish sheets live.</summary>
        public const string FishIso = "Assets/_Project/Art/Fishing/Iso";

        /// <summary>Where the trimmed icons land.</summary>
        public const string IconFolder = "Assets/_Project/Art/Fishing/Icons";

        /// <summary>The size rung the icon is cut from: the largest, the most detail.</summary>
        public const string SourceRung = "_lg";

        /// <summary>The cell of the deck sheet the icon is cut from: direction row 2, frame 0, the fish
        /// lying side on (the first of the tote's deck lays).</summary>
        public const string SourceCell = "d2_f0";

        /// <summary>The deck sheet an icon is cut from.</summary>
        public static string SourceSheet(string rigKey) => $"{FishIso}/Fish_{rigKey}{SourceRung}_deck.png";

        /// <summary>The sprite name of the cell inside <see cref="SourceSheet"/>.</summary>
        public static string SourceSprite(string rigKey) => $"Fish_{rigKey}{SourceRung}_deck_{SourceCell}";

        /// <summary>The committed icon for a rig species.</summary>
        public static string IconPath(string rigKey) => $"{IconFolder}/FishIcon_{rigKey}.png";

        /// <summary>'fish.atlantic_cod' → 'cod': the rig's species key, the id's last word.</summary>
        public static string RigKey(string speciesId)
        {
            if (string.IsNullOrEmpty(speciesId)) return speciesId;
            string leaf = speciesId.Substring(speciesId.LastIndexOf('.') + 1);
            return leaf.Substring(leaf.LastIndexOf('_') + 1);
        }

        /// <summary>
        /// The icon pixels for one rig species: its source cell trimmed to the fish. Empty when the sheet
        /// or the cell is missing (the caller reports it).
        /// </summary>
        public static FishSheetPixels.Image TrimmedIcon(string rigKey)
        {
            string sheet = SourceSheet(rigKey);
            if (!FishSheetPixels.TryCellRect(sheet, SourceSprite(rigKey), out RectInt cell)) return default;
            FishSheetPixels.Image img = FishSheetPixels.Read(sheet);
            if (img.IsEmpty) return default;
            RectInt tight = FishSheetPixels.OpaqueBounds(img, cell);
            return tight.width == 0 ? default : FishSheetPixels.Crop(img, tight);
        }

        /// <summary>The rig species with a large-rung deck sheet on disk, ordinal-sorted.</summary>
        public static string[] RigKeys()
        {
            var keys = new List<string>();
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { FishIso }))
            {
                string name = Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(guid));
                string tail = SourceRung + "_deck";
                if (!name.StartsWith("Fish_", System.StringComparison.Ordinal) ||
                    !name.EndsWith(tail, System.StringComparison.Ordinal)) continue;
                string key = name.Substring(5, name.Length - 5 - tail.Length);
                if (key.Length > 0 && !key.Contains("_")) keys.Add(key);
            }
            keys.Sort(System.StringComparer.Ordinal);
            return keys.ToArray();
        }

        [MenuItem(MenuPath, priority = 235)]
        public static void Build()
        {
            if (BuildIcons(out string summary)) Debug.Log($"[FishIconBuilder] {summary}");
            else Debug.LogError($"[FishIconBuilder] {summary}");
        }

        /// <summary>Cut, import and wire every fish icon. False, with nothing wired, when no icon could
        /// be cut at all.</summary>
        public static bool BuildIcons(out string summary)
        {
            if (!AssetDatabase.IsValidFolder(IconFolder))
                AssetDatabase.CreateFolder(Path.GetDirectoryName(IconFolder).Replace('\\', '/'),
                                           Path.GetFileName(IconFolder));

            var cut = new Dictionary<string, Sprite>(System.StringComparer.Ordinal);
            var missing = new List<string>();
            foreach (string key in RigKeys())
            {
                FishSheetPixels.Image icon = TrimmedIcon(key);
                if (icon.IsEmpty) { missing.Add(key); continue; }

                var tex = new Texture2D(icon.Width, icon.Height, TextureFormat.RGBA32, false);
                try
                {
                    tex.SetPixels32(icon.Pixels);
                    File.WriteAllBytes(IconPath(key), tex.EncodeToPNG());
                }
                finally { Object.DestroyImmediate(tex); }

                AssetDatabase.ImportAsset(IconPath(key), ImportAssetOptions.ForceSynchronousImport);
                if (AssetImporter.GetAtPath(IconPath(key)) is TextureImporter imp)
                {
                    ArtImportPipeline.ApplyLockedSettings(imp, IconPath(key));
                    imp.spriteImportMode = SpriteImportMode.Single;
                    imp.SaveAndReimport();
                }
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(IconPath(key));
                if (sprite != null) cut[key] = sprite;
                else missing.Add(key);
            }
            if (cut.Count == 0)
            {
                summary = "No fish icon could be cut (deck sheets missing or unsliced): nothing wired.";
                return false;
            }

            int wiredDefs = 0, wiredIcons = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:FishSpeciesDef", new[] { DataFish }))
            {
                var def = AssetDatabase.LoadAssetAtPath<FishSpeciesDef>(AssetDatabase.GUIDToAssetPath(guid));
                if (def == null || def.Category == FishCategory.Shellfish) continue;
                if (!cut.TryGetValue(RigKey(def.Id), out Sprite icon)) continue;
                if (def.Sprite != icon) { def.Sprite = icon; EditorUtility.SetDirty(def); }
                wiredDefs++;
            }

            var lib = AssetDatabase.LoadAssetAtPath<IconLibrary>(IconLibraryPath);
            if (lib != null && lib.Entries != null)
            {
                for (int i = 0; i < lib.Entries.Length; i++)
                {
                    string id = lib.Entries[i].Id;
                    // fish.* ids and the cod licence (its icon IS the cod).
                    string key = id == "license.cod" ? "cod" : id != null && id.StartsWith("fish.", System.StringComparison.Ordinal) ? RigKey(id) : null;
                    if (key == null || !cut.TryGetValue(key, out Sprite icon)) continue;
                    lib.Entries[i].Icon = icon;
                    wiredIcons++;
                }
                EditorUtility.SetDirty(lib);
            }
            AssetDatabase.SaveAssets();

            summary = $"Cut {cut.Count} fish icons ({string.Join(", ", cut.Keys)}) into '{IconFolder}'; " +
                      $"wired {wiredDefs} species defs and {wiredIcons} icon library entries" +
                      (missing.Count > 0 ? $"; NOT cut (no sheet or cell): {string.Join(", ", missing)}" : "") +
                      ". Commit them.";
            return true;
        }
    }
}
#endif
