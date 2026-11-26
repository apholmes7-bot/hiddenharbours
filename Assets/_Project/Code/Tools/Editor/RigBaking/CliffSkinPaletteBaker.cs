using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Art;
using HiddenHarbours.Art.Editor;

namespace HiddenHarbours.Tools.RigBaking
{
    /// <summary>
    /// <b>A CLIFF SKIN'S PALETTE, CUT BY THE KIT ITSELF.</b> Bakes a <see cref="CliffFaceSkinDef"/>'s 8 x 32 LUT
    /// by moving the px rig's rock and accessory bases (<c>PxCliffFace.DEF[rock].base</c> and <c>.c</c>,
    /// <c>PxCliffFace.ACC</c>) to the skin's in a fresh rig host, then running the same
    /// <see cref="CliffBaker.BakePaletteLut"/> the shipped palette comes from. So every tier still leans to the
    /// kit's cold ambient and warm key by the kit's own amounts, and nothing about a tier is restated here.
    ///
    /// <para>The rig file is executed unmodified (ADR 0021 §5): the override is a host-side assignment to the
    /// rig's exported tables, in a host that is thrown away after the bake.</para>
    ///
    /// <para>Writes only when the pixels differ, like <see cref="CliffBaker.WritePaletteLut"/>, so a re-bake
    /// never dirties a committed binary. <c>CliffSkinPaletteBakeTests</c> holds every committed skin palette
    /// equal to a fresh bake of its Def.</para>
    /// </summary>
    public static class CliffSkinPaletteBaker
    {
        const string PG = CliffCatalog.PxRigGlobalName;

        static readonly Regex HexRx = new Regex("^#[0-9a-fA-F]{6}$");
        static readonly Regex NameRx = new Regex("^[a-z_]+$");

        /// <summary>The script that moves the rig's bases to a skin's. Throws on a name the rig does not have.</summary>
        public static string OverrideScript(CliffFaceSkinDef skin)
        {
            if (skin == null) throw new ArgumentNullException(nameof(skin));
            var sb = new StringBuilder();
            foreach (CliffSkinRock r in skin.Rocks ?? new CliffSkinRock[0])
            {
                Check(skin, r);
                sb.Append($"(function(){{ var d = {PG}.DEF['{r.Name}']; " +
                          $"if (!d) throw new Error('cliff skin {skin.Id}: the kit has no rock {r.Name}'); " +
                          $"d.base = '{r.BaseHex.ToLowerInvariant()}'; d.c = {Num(r.Contrast)}; }})();\n");
            }
            foreach (CliffSkinRock a in skin.Accessories ?? new CliffSkinRock[0])
            {
                Check(skin, a);
                sb.Append($"(function(){{ var a = {PG}.ACC.find(function(x){{ return x[0] === '{a.Name}'; }}); " +
                          $"if (!a) throw new Error('cliff skin {skin.Id}: the kit has no accessory {a.Name}'); " +
                          $"a[1] = '{a.BaseHex.ToLowerInvariant()}'; a[2] = {Num(a.Contrast)}; }})();\n");
            }
            return sb.ToString();
        }

        static void Check(CliffFaceSkinDef skin, CliffSkinRock r)
        {
            if (string.IsNullOrEmpty(r.Name) || !NameRx.IsMatch(r.Name))
                throw new ArgumentException($"[cliff-skin] {skin.Id}: '{r.Name}' is not a kit rock or accessory name.");
            if (string.IsNullOrEmpty(r.BaseHex) || !HexRx.IsMatch(r.BaseHex))
                throw new ArgumentException($"[cliff-skin] {skin.Id}: {r.Name}'s base '{r.BaseHex}' is not #rrggbb.");
            if (!(r.Contrast > 0f))
                throw new ArgumentException($"[cliff-skin] {skin.Id}: {r.Name}'s contrast must be above zero.");
        }

        static string Num(float v) => v.ToString("R", CultureInfo.InvariantCulture);

        /// <summary>The skin's LUT, top row first, from a fresh rig host. A skin with no overrides bakes the
        /// shipped palette exactly.</summary>
        public static byte[] BakeSkinLut(CliffFaceSkinDef skin)
        {
            using (IRigScriptHost host = RigScriptHostFactory.Create())
            {
                CliffBaker.InstallPxRig(host);
                string script = OverrideScript(skin);
                if (script.Length > 0) host.Execute(script);
                return CliffBaker.BakePaletteLut(host);
            }
        }

        /// <summary>Writes the skin's palette to its <see cref="CliffFaceSkinDef.PalettePath"/> when its pixels
        /// differ. Returns whether it wrote. Does nothing for the material's own palette.</summary>
        public static bool WriteSkinPalette(CliffFaceSkinDef skin)
        {
            if (skin == null || string.IsNullOrEmpty(skin.PalettePath)) return false;
            int w = CliffCatalog.PxPaletteWidth, h = CliffCatalog.PxPaletteHeight;
            byte[] lut = BakeSkinLut(skin);
            string abs = Path.Combine(RigCatalog.RepoRoot, skin.PalettePath);
            if (File.Exists(abs) && CliffBaker.SamePixels(File.ReadAllBytes(abs), lut, w, h)) return false;
            Directory.CreateDirectory(Path.GetDirectoryName(abs));
            File.WriteAllBytes(abs, CliffBaker.EncodePng(lut, w, h));
            return true;
        }

        /// <summary>Whether a committed PNG is exactly this LUT (for the test).</summary>
        public static bool PngEquals(string assetPath, byte[] lut)
        {
            string abs = Path.Combine(RigCatalog.RepoRoot, assetPath);
            return File.Exists(abs) &&
                   CliffBaker.SamePixels(File.ReadAllBytes(abs), lut, CliffCatalog.PxPaletteWidth, CliffCatalog.PxPaletteHeight);
        }

        /// <summary>Bakes every skin in the project, imports its palette as a palette, and hands it to the Def.</summary>
        [MenuItem("Hidden Harbours/Dev/Bake Cliff Skin Palettes")]
        public static void BakeAll()
        {
            int wrote = 0, skins = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:" + nameof(CliffFaceSkinDef)))
            {
                var skin = AssetDatabase.LoadAssetAtPath<CliffFaceSkinDef>(AssetDatabase.GUIDToAssetPath(guid));
                if (skin == null || string.IsNullOrEmpty(skin.PalettePath)) continue;
                skins++;
                if (WriteSkinPalette(skin)) wrote++;
                AssetDatabase.ImportAsset(skin.PalettePath, ImportAssetOptions.ForceUpdate);
                CliffBaker.ApplyImportSettings(skin.PalettePath, CliffAssetKind.Palette, "");
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(skin.PalettePath);
                if (tex != null && skin.Palette != tex)
                {
                    skin.Palette = tex;
                    EditorUtility.SetDirty(skin);
                }
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[cliff-skin] baked {skins} skin palette(s), wrote {wrote}.");
        }

        /// <summary>The batchmode entry: <c>-executeMethod HiddenHarbours.Tools.RigBaking.CliffSkinPaletteBaker.BakeAllBatch</c>.</summary>
        public static void BakeAllBatch()
        {
            try { BakeAll(); EditorApplication.Exit(0); }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }
    }
}
