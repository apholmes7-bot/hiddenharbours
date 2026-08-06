using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Art.Editor;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>The meadow must not read as one sprite tiled.</b> The owner's 2026-08-06 playtest verdict on the
    /// retuned ground cover was that the grass "looks better" and the vertical tufts "look great", but that
    /// the wide low clumps repeat visibly. That was the LIBRARY, not the scatter: only five of the 29 baked
    /// variants are two cells wide, so the island's fill cores ran on the two tagged <c>meadow</c>, and the
    /// whole path VERGE — a ribbon either side of both walked lines — was baked with exactly those two and
    /// nothing else.
    ///
    /// <para>These tests pin the floor that fixes it: every habitat this island actually resolves must be
    /// able to draw from at least <see cref="StPetersGrass.MinHabitatVariety"/> distinct variants, in BOTH
    /// its normal and its broad pool. The chooser reaches that by borrowing from documented allies
    /// (<see cref="StPetersGrass.AlliesFor"/>) when a baked pool is too thin.</para>
    ///
    /// <para><b>⚠ This is a floor on VARIETY, not a licence to stop baking.</b> Borrowed art is art the
    /// manifest tagged for other ground; it is a better answer than the same two sprites forever, and a
    /// worse one than more wide clumps. The rig is the art-director's lane and this pass deliberately did
    /// not reach into it — if these tests ever start failing because a habitat lost its allies, bake, do
    /// not lower the floor.</para>
    /// </summary>
    public class StPetersGrassVarietyTests
    {
        /// <summary>The habitats <see cref="StPetersGrass.HabitatAt"/> can return — every one of them is
        /// ground the owner walks on, so every one of them has to carry a field's worth of art.</summary>
        static readonly string[] IslandHabitats =
        {
            StPetersGrass.HabitatSward,
            StPetersGrass.HabitatMeadow,
            StPetersGrass.HabitatFringe,
            StPetersGrass.HabitatDune,
            StPetersGrass.HabitatHeadland,
            StPetersGrass.HabitatVerge,
        };

        static List<GrassLibraryCatalog.Entry> Imported()
        {
            var imported = GrassLibraryCatalog.Imported(GrassLibraryCatalog.Load());
            if (imported.Count == 0)
                Assert.Ignore(
                    "SKIP(grass-library-not-imported): the grass library resolved no imported sprites, so " +
                    "there is no art to measure variety against. Bake it (Hidden Harbours ▸ Dev ▸ Bake Grass " +
                    "Library) and `git lfs pull` if the PNGs are still pointers.");
            return imported;
        }

        [Test]
        public void EveryHabitat_CanDrawFromEnoughVariants_ToReadAsAFieldRatherThanAPattern()
        {
            var chooser = new StPetersWoodsPlanter.GrassArtChooser(Imported());
            var thin = new List<string>();

            foreach (string habitat in IslandHabitats)
            {
                var normal = chooser.For(habitat);
                var broad = chooser.BroadFor(habitat);
                Report(habitat, normal, broad);

                if (Distinct(normal) < StPetersGrass.MinHabitatVariety)
                    thin.Add($"{habitat} normal pool has {Distinct(normal)} variants " +
                             $"({Names(normal)})");
                if (Distinct(broad) < StPetersGrass.MinHabitatVariety)
                    thin.Add($"{habitat} BROAD pool has {Distinct(broad)} variants ({Names(broad)})");
            }

            Assert.IsEmpty(
                thin,
                $"A habitat cannot reach {StPetersGrass.MinHabitatVariety} distinct variants even after " +
                "borrowing from its allies, so that ground draws the same few sprites over and over — the " +
                "'saggy clumps are all the same sprite' defect. Either bake more art for it or give it an " +
                "ally that has some (StPetersGrass.AlliesFor); do NOT lower MinHabitatVariety:\n  " +
                string.Join("\n  ", thin));
        }

        /// <summary>
        /// The two pools the owner's complaint was actually about. Stated separately from the sweep above
        /// so a regression names the ground he was standing on rather than a habitat list.
        /// </summary>
        [Test]
        public void TheFillCoresAndTheVerge_NoLongerRunOnTwoSprites()
        {
            var chooser = new StPetersWoodsPlanter.GrassArtChooser(Imported());

            var cores = chooser.BroadFor(StPetersGrass.HabitatMeadow);
            Assert.Greater(
                Distinct(cores), 2,
                "The meadow's BROAD pool is still the two ClumpWide sprites. That pool draws the island's " +
                $"fill cores, and two silhouettes (four with the mirror) is what the owner read as tiling: " +
                $"{Names(cores)}");

            var verge = chooser.For(StPetersGrass.HabitatVerge);
            Assert.Greater(
                Distinct(verge), 2,
                "The path verge is still drawn from the two variants baked with the verge tag. It is a " +
                $"continuous ribbon either side of both walked paths, so it tiles more visibly than " +
                $"anywhere else on the island: {Names(verge)}");
        }

        /// <summary>
        /// A borrow must not change what the ground IS. The verge is trodden and both its baked variants
        /// are short; lending it the meadow's tall timothy would make the walked tracks run through
        /// knee-high grass.
        /// </summary>
        [Test]
        public void ABorrowedVariant_MatchesAHeightClassTheHabitatAlreadyHad()
        {
            var imported = Imported();
            var chooser = new StPetersWoodsPlanter.GrassArtChooser(imported);
            var wrong = new List<string>();

            foreach (string habitat in IslandHabitats)
            {
                var baked = chooser.BakedFor(habitat);
                var bakedClasses = new HashSet<string>(
                    baked.Select(e => e.HeightClass).Where(c => !string.IsNullOrEmpty(c)),
                    System.StringComparer.OrdinalIgnoreCase);
                if (bakedClasses.Count == 0) continue;

                var bakedNames = new HashSet<string>(baked.Select(e => e.Name), System.StringComparer.Ordinal);
                foreach (var e in chooser.For(habitat).Concat(chooser.BroadFor(habitat)))
                {
                    if (bakedNames.Contains(e.Name)) continue;               // baked for this ground, not borrowed
                    if (!bakedClasses.Contains(e.HeightClass ?? ""))
                        wrong.Add($"{habitat} borrowed '{e.Name}' ({e.HeightClass}); it only ever bakes " +
                                  $"[{string.Join(", ", bakedClasses)}]");
                }
            }

            Assert.IsEmpty(
                wrong,
                "A habitat borrowed art of a height class it never bakes, which changes how that ground " +
                "reads rather than just how varied it is:\n  " + string.Join("\n  ", wrong));
        }

        /// <summary>
        /// Rule 5. The pools are built by walking the manifest and then an ordered ally list, so two
        /// choosers must agree exactly — a site's roll indexes into this list, and a reordering would
        /// silently redraw the whole island on the next build.
        /// </summary>
        [Test]
        public void ThePools_AreBuiltInAStableOrder_SoARebuildDrawsTheSameIsland()
        {
            var imported = Imported();
            var a = new StPetersWoodsPlanter.GrassArtChooser(imported);
            var b = new StPetersWoodsPlanter.GrassArtChooser(imported);

            foreach (string habitat in IslandHabitats)
            {
                CollectionAssert.AreEqual(
                    a.For(habitat).Select(e => e.Name).ToList(),
                    b.For(habitat).Select(e => e.Name).ToList(),
                    $"'{habitat}' built a different normal pool the second time — the same metre of ground " +
                    "would grow a different blade after a rebuild.");
                CollectionAssert.AreEqual(
                    a.BroadFor(habitat).Select(e => e.Name).ToList(),
                    b.BroadFor(habitat).Select(e => e.Name).ToList(),
                    $"'{habitat}' built a different BROAD pool the second time.");
            }
        }

        /// <summary>A pool rich enough on its own must be left exactly as baked — borrowing is for thin
        /// pools, and a rule that widened everything would dissolve the habitat split the green-over
        /// exists for.</summary>
        [Test]
        public void ARichPool_BorrowsNothing()
        {
            var chooser = new StPetersWoodsPlanter.GrassArtChooser(Imported());

            var baked = chooser.BakedFor(StPetersGrass.HabitatMeadow);
            Assume.That(
                baked.Count, Is.GreaterThanOrEqualTo(StPetersGrass.MinHabitatVariety),
                "The meadow is the island's rich pool; if it ever drops below the floor this test is " +
                "asserting the wrong thing.");

            CollectionAssert.AreEqual(
                baked.Select(e => e.Name).ToList(),
                chooser.For(StPetersGrass.HabitatMeadow).Select(e => e.Name).ToList(),
                "The meadow's normal pool borrowed even though it bakes plenty of its own.");
        }

        static int Distinct(List<GrassLibraryCatalog.Entry> entries) =>
            entries.Select(e => e.Name).Distinct(System.StringComparer.Ordinal).Count();

        static string Names(List<GrassLibraryCatalog.Entry> entries) =>
            string.Join(", ", entries.Select(e => e.Name));

        /// <summary>The pools CI reports, so a reviewer reads the retune without opening Unity.</summary>
        static void Report(string habitat, List<GrassLibraryCatalog.Entry> normal,
                           List<GrassLibraryCatalog.Entry> broad) =>
            Debug.Log($"[GrassVariety] {habitat}: {Distinct(normal)} normal ({Names(normal)}) · " +
                      $"{Distinct(broad)} broad ({Names(broad)})");
    }
}
