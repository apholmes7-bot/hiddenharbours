using System.IO;
using UnityEditor;
using UnityEngine;

namespace HiddenHarbours.Art.Editor
{
    /// <summary>
    /// Bakes <c>Assets/_Project/Art/Textures/Water/Whitecaps.png</c> — the open-water whitecap STAMP
    /// SHEET the water shader samples — from the owner's single hand-painted mark in
    /// <c>WhitecapSource~/WhitecapMark.png</c>. All the reasoning lives on
    /// <see cref="WhitecapStampSheetMath"/>; this is the thin editor shell around it.
    ///
    /// <para>The source sits in a <c>~</c> folder so Unity never imports it: it is not art the game
    /// draws, it is the ONE mark this sheet is made of, kept in the repo verbatim (the file imported
    /// in #87, byte for byte) so a re-bake always starts from the owner's own pixels.</para>
    ///
    /// <para>Re-running is safe and idempotent in PIXELS, though the encoder may write different
    /// BYTES; <c>WhitecapStampSheetTests</c> compares pixels, never bytes, for exactly that reason.
    /// Re-run it after editing the mark or re-rolling <see cref="WhitecapStampSheetMath.Seed"/>.</para>
    /// </summary>
    public static class WhitecapStampSheetBaker
    {
        public const string SourcePath =
            "Assets/_Project/Art/Textures/Water/WhitecapSource~/WhitecapMark.png";

        public const string SheetPath =
            "Assets/_Project/Art/Textures/Water/Whitecaps.png";

        [MenuItem("Hidden Harbours/Art/Bake Whitecap Stamp Sheet (16 stamps from the owner's mark)")]
        public static void Bake()
        {
            string sheet = BakeTo(SourcePath, SheetPath);
            AssetDatabase.ImportAsset(sheet, ImportAssetOptions.ForceUpdate);
            Debug.Log($"[WhitecapStampSheet] baked {WhitecapStampSheetMath.MarkCount} stamps into " +
                      $"{WhitecapStampSheetMath.SheetSize}x{WhitecapStampSheetMath.SheetSize} at '{sheet}'.");
        }

        /// <summary>Reads the mark, composes the sheet, writes the PNG. Returns the written path.</summary>
        public static string BakeTo(string sourcePath, string sheetPath)
        {
            Color32[] cell = ReadMarkCell(sourcePath);
            Color32[] pixels = WhitecapStampSheetMath.Compose(cell);

            int n = WhitecapStampSheetMath.SheetSize;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, mipChain: false, linear: true);
            tex.SetPixels32(pixels);
            tex.Apply(false, false);
            File.WriteAllBytes(sheetPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            return sheetPath;
        }

        /// <summary>
        /// Decodes the source mark to a <see cref="WhitecapStampSheetMath.MarkCellSize"/>-square
        /// Color32 cell in texture space. Read as raw BYTES through <c>ImageConversion.LoadImage</c>
        /// rather than through the AssetDatabase: the source is in a <c>~</c> folder, so there is no
        /// importer, no Read/Write flag and no GPU involved — which is also what lets the test do this
        /// headless on CI.
        /// </summary>
        public static Color32[] ReadMarkCell(string sourcePath)
        {
            if (!File.Exists(sourcePath))
                throw new FileNotFoundException(
                    $"the owner's whitecap mark is missing — the sheet cannot be baked without it",
                    sourcePath);

            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false, linear: true);
            if (!ImageConversion.LoadImage(tex, File.ReadAllBytes(sourcePath)))
            {
                Object.DestroyImmediate(tex);
                throw new IOException($"'{sourcePath}' did not decode as a PNG");
            }

            int n = WhitecapStampSheetMath.MarkCellSize;
            if (tex.width != n || tex.height != n)
            {
                int w = tex.width, h = tex.height;
                Object.DestroyImmediate(tex);
                throw new System.ArgumentException(
                    $"'{sourcePath}' is {w}x{h}; the mark cell must be {n}x{n}");
            }

            Color32[] cell = tex.GetPixels32();
            Object.DestroyImmediate(tex);
            return cell;
        }
    }
}
