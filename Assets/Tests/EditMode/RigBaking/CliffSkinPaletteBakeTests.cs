using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Art;
using HiddenHarbours.Art.Editor;
using HiddenHarbours.Tools.RigBaking;

namespace HiddenHarbours.Tests.RigBaking
{
    /// <summary>
    /// The cliff mix's palettes are the px kit's own procedure, not a hand-painted table (owner 11 25, the cliff
    /// mix). A committed skin palette is exactly what its Def bakes to, and a skin with no overrides is exactly the
    /// shipped palette, so the second look can never drift from the data it claims to come from.
    /// </summary>
    public class CliffSkinPaletteBakeTests
    {
        const string SkinsFolder = "Assets/_Project/Data/Terrain/CliffSkins";

        static CliffFaceSkinDef[] Skins() =>
            AssetDatabase.FindAssets("t:" + nameof(CliffFaceSkinDef), new[] { SkinsFolder })
                .Select(g => AssetDatabase.LoadAssetAtPath<CliffFaceSkinDef>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(s => s != null).ToArray();

        [Test]
        public void ASkinWithNoOverrides_BakesTheShippedPalette_PixelForPixel()
        {
            var plain = ScriptableObject.CreateInstance<CliffFaceSkinDef>();
            try
            {
                plain.Id = "cliffskin.test_plain";
                byte[] lut = CliffSkinPaletteBaker.BakeSkinLut(plain);
                Assert.IsTrue(CliffSkinPaletteBaker.PngEquals(CliffCatalog.PxPalettePath, lut),
                    "a skin that moves nothing does not bake the shipped palette: the override path changes the kit");
            }
            finally { Object.DestroyImmediate(plain); }
        }

        [Test]
        public void EveryCommittedSkinPalette_IsWhatItsDefBakesTo()
        {
            CliffFaceSkinDef[] skins = Skins().Where(s => !string.IsNullOrEmpty(s.PalettePath)).ToArray();
            Assert.IsNotEmpty(skins, "no baked cliff skins found: the board skin is missing");
            foreach (CliffFaceSkinDef s in skins)
            {
                byte[] lut = CliffSkinPaletteBaker.BakeSkinLut(s);
                Assert.IsTrue(CliffSkinPaletteBaker.PngEquals(s.PalettePath, lut),
                    $"{s.Id}: {s.PalettePath} is not what its Def bakes to. Run Hidden Harbours > Dev > Bake Cliff Skin " +
                    "Palettes and commit the PNG.");
            }
        }

        [Test]
        public void TheBoardSkin_MovesTheRocksItNames_AndLeavesBasaltAlone()
        {
            CliffFaceSkinDef board = Skins().FirstOrDefault(s => s.Id == "cliffskin.board_bedded");
            Assert.IsNotNull(board, "no cliffskin.board_bedded");
            var plain = ScriptableObject.CreateInstance<CliffFaceSkinDef>();
            try
            {
                byte[] a = CliffSkinPaletteBaker.BakeSkinLut(plain), b = CliffSkinPaletteBaker.BakeSkinLut(board);
                int w = CliffCatalog.PxPaletteWidth;
                bool RowSame(int row)
                {
                    for (int i = row * w * 4; i < (row + 1) * w * 4; i++) if (a[i] != b[i]) return false;
                    return true;
                }
                // Rows 0..5 sandstone, 6..11 till, 12..17 basalt (the kit's ROCKS order).
                Assert.IsFalse(RowSame(3), "the board skin did not move sandstone");
                Assert.IsFalse(RowSame(9), "the board skin did not move till");
                for (int r = 12; r < 18; r++) Assert.IsTrue(RowSame(r), $"the board skin moved basalt row {r}");
            }
            finally { Object.DestroyImmediate(plain); }
        }

        [Test]
        public void AnOverrideNamingNoKitRock_Fails_Loudly()
        {
            var bad = ScriptableObject.CreateInstance<CliffFaceSkinDef>();
            try
            {
                bad.Id = "cliffskin.test_bad";
                bad.Rocks = new[] { new CliffSkinRock { Name = "granite", BaseHex = "#808080", Contrast = 1f } };
                Assert.That(() => CliffSkinPaletteBaker.BakeSkinLut(bad), Throws.Exception);
                bad.Rocks = new[] { new CliffSkinRock { Name = "sandstone", BaseHex = "orange", Contrast = 1f } };
                Assert.That(() => CliffSkinPaletteBaker.OverrideScript(bad), Throws.ArgumentException);
            }
            finally { Object.DestroyImmediate(bad); }
        }
    }
}
