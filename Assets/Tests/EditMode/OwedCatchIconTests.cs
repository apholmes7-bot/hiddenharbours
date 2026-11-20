using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Fishing;

namespace HiddenHarbours.Tests
{
    /// <summary>
    /// <b>The crustaceans and shellfish show their icon-scale plates</b> (ShellIcon_*.png, baked by
    /// docs/art/rigs/owed-icons/bakeOwedIcons.js): each Def's sprite and its icon library entry are the
    /// same plate, the plate imports under the art lock, and it is big enough to read in the card slot.
    /// Before these, the oyster, the mussel and the scallop had no icon at all.
    /// </summary>
    public class OwedCatchIconTests
    {
        const string Icons = "Assets/_Project/Art/Fishing/Icons/";

        static readonly (string def, string id, string leaf)[] Plates =
        {
            ("AmericanLobster", "fish.lobster", "lobster"),
            ("RockCrab", "fish.rock_crab", "rock_crab"),
            ("SoftShellClam", "fish.soft_shell_clam", "soft_shell_clam"),
            ("AmericanOyster", "fish.american_oyster", "american_oyster"),
            ("BlueMussel", "fish.blue_mussel", "blue_mussel"),
            ("SeaScallop", "fish.sea_scallop", "sea_scallop"),
        };

        /// <summary>The smallest plate, px wide, that still reads in the card's icon slot.</summary>
        const float MinPlateWidthPx = 30f;

        static string PlatePath(string leaf) => Icons + "ShellIcon_" + leaf + ".png";

        [Test]
        public void EveryOwedDef_PointsAtItsPlate_AndTheLibraryAgrees()
        {
            var lib = Resources.Load<IconLibrary>(IconRegistrar.ResourcesPath);
            Assert.IsNotNull(lib, "Resources/IconLibrary must ship");
            foreach (var (def, id, leaf) in Plates)
            {
                var d = AssetDatabase.LoadAssetAtPath<FishSpeciesDef>("Assets/_Project/Data/Fish/" + def + ".asset");
                Assert.IsNotNull(d, def);
                Assert.AreEqual(id, d.Id, def);
                var plate = AssetDatabase.LoadAssetAtPath<Sprite>(PlatePath(leaf));
                Assert.IsNotNull(plate, $"{PlatePath(leaf)}: missing or not a sprite");
                Assert.AreSame(plate, d.Sprite, $"{id}: the Def sprite must be its plate");

                Sprite entry = null;
                foreach (IconLibrary.Entry e in lib.Entries)
                    if (e.Id == id) entry = e.Icon;
                Assert.AreSame(plate, entry, $"{id}: the icon library entry must be its plate");
                Assert.GreaterOrEqual(plate.rect.width, MinPlateWidthPx, $"{id}: plate too small to read");
            }
        }

        [Test]
        public void EveryPlate_ImportsUnderTheArtLock()
        {
            foreach (var (_, _, leaf) in Plates)
            {
                string path = PlatePath(leaf);
                Assert.IsTrue(File.Exists(path), path);
                var imp = AssetImporter.GetAtPath(path) as TextureImporter;
                Assert.IsNotNull(imp, $"{path}: not imported as a texture");
                Assert.AreEqual(TextureImporterType.Sprite, imp.textureType, path);
                Assert.AreEqual(SpriteImportMode.Single, imp.spriteImportMode, path);
                Assert.AreEqual(FilterMode.Point, imp.filterMode, path);
                Assert.AreEqual(TextureImporterCompression.Uncompressed, imp.textureCompression, path);
                Assert.AreEqual(32f, imp.spritePixelsPerUnit, path);
                Assert.IsFalse(imp.mipmapEnabled, path);
            }
        }
    }
}
