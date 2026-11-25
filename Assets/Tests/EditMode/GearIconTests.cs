using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests
{
    /// <summary>
    /// <b>The fishing gear has catalog icons</b> (GearIcon_*.png, baked by
    /// docs/art/rigs/gear-icons/bakeGearIcons.js): each gear id's icon library entry is its own icon,
    /// the icon imports under the art lock, and the id is one the shipped data really declares, so an
    /// entry cannot outlive its gear.
    /// </summary>
    public class GearIconTests
    {
        const string Icons = "Assets/_Project/Art/Fishing/Icons/";

        static readonly (string id, string leaf)[] Gear =
        {
            ("gear.rod", "rod"),
            ("gear.oyster_tongs", "oyster_tongs"),
            ("gear.shellfish_rake", "shellfish_rake"),
            ("gear.mussel_sack", "mussel_sack"),
            ("gear.shellfish_basket", "shellfish_basket"),
            ("tackle.cod_jig", "cod_jig"),
            ("tackle.mackerel_feathers", "mackerel_feathers"),
            ("tackle.soft_bait", "soft_bait"),
            ("tackle.spinner", "spinner"),
            ("tackle.spoon", "spoon"),
            ("longline.groundfish_tub", "longline_tub"),
            ("trawl_gear.bottom_otter_prototype", "trawl_net"),
            // pass 2: the ids the Notebook's KIT page still lettered with a placeholder
            ("watch.harbour_digital", "harbour_watch"),
            ("gear.rod_rack", "rod_rack"),
            ("trap.lobster", "lobster_pot"),
            ("trap.crab", "crab_pot"),
            ("bait.herring", "bait_herring"),
            ("bait.mackerel", "bait_mackerel"),
            ("bait.capelin", "bait_capelin"),
            ("bait.squid_strip", "bait_squid_strip"),
            ("bait.sea_worm", "bait_sea_worm"),
            ("bait.shucked_clam", "bait_shucked_clam"),
            ("bait.fish_scrap", "bait_fish_scrap"),
            ("instrument.depth_sounder", "depth_sounder"),
            ("instrument.fish_finder", "fish_finder"),
            ("instrument.loran", "loran"),
            ("instrument.gps", "gps"),
            // pass 3: the headlamp he has from the first morning
            ("gear.headlamp", "headlamp"),
        };

        /// <summary>The smallest icon, px on its long side, that still reads in the card's icon slot.</summary>
        const int MinLongSidePx = 24;

        static string IconPath(string leaf) => Icons + "GearIcon_" + leaf + ".png";

        [Test]
        public void EveryGearId_HasItsOwnIcon_InTheLibrary()
        {
            var lib = Resources.Load<IconLibrary>(IconRegistrar.ResourcesPath);
            Assert.IsNotNull(lib, "Resources/IconLibrary must ship");
            foreach (var (id, leaf) in Gear)
            {
                var icon = AssetDatabase.LoadAssetAtPath<Sprite>(IconPath(leaf));
                Assert.IsNotNull(icon, $"{IconPath(leaf)}: missing or not a sprite");
                IconLibrary.Entry[] hits = lib.Entries.Where(e => e.Id == id).ToArray();
                Assert.AreEqual(1, hits.Length, $"{id}: exactly one icon library entry");
                Assert.AreSame(icon, hits[0].Icon, $"{id}: the entry must be its gear icon");
                Assert.GreaterOrEqual(Mathf.Max(icon.rect.width, icon.rect.height), MinLongSidePx, $"{id}: icon too small to read");
            }
        }

        [Test]
        public void EveryGearId_IsDeclaredByTheShippedData()
        {
            string[] text = Directory.EnumerateFiles("Assets/_Project/Data", "*.asset", SearchOption.AllDirectories)
                .Select(File.ReadAllText).ToArray();
            foreach (var (id, _) in Gear)
                Assert.IsTrue(text.Any(t => t.Contains("Id: " + id + "\n") || t.Contains("Id: " + id + "\r\n")),
                    $"{id}: no Def or offer declares this id; the icon entry would point at nothing");
        }

        [Test]
        public void EveryGearIcon_ImportsUnderTheArtLock()
        {
            foreach (var (_, leaf) in Gear)
            {
                string path = IconPath(leaf);
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
