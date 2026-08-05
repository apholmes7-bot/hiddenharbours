using System.Linq;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Art;
using HiddenHarbours.Art.Editor;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// The <b>tide story</b>, driven as a table: walk the water down a plant's own ground and assert
    /// it wears the states the kit baked, in order — submerged, then a dripping fringe, then bone dry.
    ///
    /// <para><b>Why a table and not a screenshot.</b> The five baked states differ by <i>lay</i>,
    /// <i>wetness</i>, <i>submerged colour</i> and <i>sway</i> all at once, and every one of those is
    /// already resolved inside the pixels. The only thing the engine decides is WHICH COLUMN, and
    /// that decision is a function of one number. So the thing worth pinning is the function: given
    /// water at height H over this species' ground, which column, and is it under water. A
    /// screenshot proves a frame; this proves the whole ladder, including the rungs the owner will
    /// never happen to stand on.</para>
    ///
    /// <para><b>⚠ The submerged test is about HEIGHT, not depth.</b> A 2.75 m sugar kelp with 1 m of
    /// water over its holdfast is lying across the surface, not under it — testing <c>overM &gt; 0</c>
    /// would sink every tall plant the moment its foot got wet. That distinction is the one this file
    /// exists to keep honest, because it looks correct either way in a still.</para>
    /// </summary>
    public class ShorePlantTideTableTests
    {
        static ShorePlantCatalog.Contract Contract()
        {
            var c = ShorePlantCatalog.Load();
            Assert.IsNotNull(c, $"The shore plant contract at '{ShorePlantCatalog.ContractPath}' did " +
                                "not load — it ships with the kit and is the oracle for everything here.");
            return c;
        }

        static float[] LadderFor(string species, out ShorePlantCatalog.SpeciesEntry entry)
        {
            var c = Contract();
            entry = c.Find(species);
            Assert.IsNotNull(entry, $"The contract declares no species '{species}'.");
            return ShorePlantDefBuilder.Ladder(c, entry.BaseM);
        }

        // =====================================================================================

        [Test]
        public void TheLadder_RisesMonotonically_AndHasOneRungPerBakedColumn()
        {
            var contract = Contract();

            foreach (string key in ShorePlantCatalog.SpeciesKeys)
            {
                var entry = contract.Find(key);
                float[] ladder = ShorePlantDefBuilder.Ladder(contract, entry.BaseM);

                Assert.AreEqual(ShorePlantTideMath.TideStates, ladder.Length,
                    $"{key}: the ladder must have one rung per baked tide column.");

                for (int i = 1; i < ladder.Length; i++)
                    Assert.Greater(ladder[i], ladder[i - 1],
                        $"{key}: the ladder is not rising at rung {i} ({ladder[i - 1]} → {ladder[i]}). " +
                        "The columns are baked low→high; a ladder out of order maps a rising tide onto " +
                        "a draining plant.");
            }
        }

        [Test]
        public void SugarKelp_WalksTheWholeTideStory_SubmergedToBoneDry()
        {
            float[] ladder = LadderFor("SugarKelp", out var kelp);

            // The fringe sits at 0.15 m above chart datum, so its own ground is under water for most
            // of the range — this is the species the ocean floor gets planted with.
            Assert.AreEqual(4, ShorePlantTideMath.StateFor(ladder[4] + 0.4f, ladder),
                "Water above the highest baked rung must hold at the HIGH state, not wrap.");
            Assert.AreEqual(4, ShorePlantTideMath.StateFor(ladder[4], ladder));
            Assert.AreEqual(2, ShorePlantTideMath.StateFor(ladder[2], ladder));
            Assert.AreEqual(0, ShorePlantTideMath.StateFor(ladder[0], ladder));
            Assert.AreEqual(0, ShorePlantTideMath.StateFor(ladder[0] - 3f, ladder),
                "Water below the lowest baked rung must hold at the LOW state — a drained kelp is " +
                "already collapsed onto its rock; there is no drier state to reach for.");

            // Between two rungs, nearest wins. Deliberately sampled off the midpoint: the real ladder
            // is 1.2 m apart and the exact midpoint is not exactly representable, so pinning the
            // tie-break on THESE numbers would test float rounding rather than the rule. The rule
            // itself is pinned on an exact ladder below.
            Assert.AreEqual(1, ShorePlantTideMath.StateFor(ladder[1] + 0.4f, ladder));
            Assert.AreEqual(2, ShorePlantTideMath.StateFor(ladder[2] - 0.4f, ladder));
        }

        [Test]
        public void AnExactTieResolvesToTheDrierState_SoTheChoiceIsTotal()
        {
            // Whole numbers, so the midpoint IS exactly representable and this tests the rule and not
            // the arithmetic: 1.5 is equidistant from rungs 1 and 2, and the drier one must win.
            var ladder = new[] { 0f, 1f, 2f, 3f, 4f };

            Assert.AreEqual(1, ShorePlantTideMath.StateFor(1.5f, ladder),
                "A plant exactly between two baked states reads better standing than drowned — and a " +
                "rule that picks one of them always is what stops the same input having two answers.");
            Assert.AreEqual(0, ShorePlantTideMath.StateFor(0.5f, ladder));
            Assert.AreEqual(3, ShorePlantTideMath.StateFor(3.5f, ladder));
        }

        [Test]
        public void Submergence_IsAboutTheWholePlantsHeight_NotJustAWetFoot()
        {
            var contract = Contract();
            var kelp = contract.Find("SugarKelp");     // 2.75 m — the tallest thing in the kit
            var moss = contract.Find("IrishMoss");     // 0.53 m — a mat

            // The trap, stated as a test: a metre of water over a 2.75 m kelp is NOT submergence.
            Assert.IsFalse(ShorePlantTideMath.IsSubmerged(1.0f, kelp.HeightM),
                "1 m of water over a 2.75 m kelp is a plant lying across the surface, not under it. " +
                "Sinking it here would draw its emergent half beneath the sea.");
            Assert.IsTrue(ShorePlantTideMath.IsSubmerged(2.75f, kelp.HeightM),
                "Water at exactly the plant's standing height is the moment it goes under.");
            Assert.IsTrue(ShorePlantTideMath.IsSubmerged(4.0f, kelp.HeightM));

            // The same metre drowns the mat, because the mat is 0.53 m tall.
            Assert.IsTrue(ShorePlantTideMath.IsSubmerged(1.0f, moss.HeightM),
                "The same water that leaves a kelp emergent must submerge a half-metre mat.");

            // Drained is never submerged, whatever the plant.
            Assert.IsFalse(ShorePlantTideMath.IsSubmerged(-0.2f, moss.HeightM));
            Assert.IsFalse(ShorePlantTideMath.IsSubmerged(0f, moss.HeightM),
                "Water level exactly at the plant's foot is a wet foot, not a submerged plant.");
        }

        [Test]
        public void ASubmergedPlantDrawsUnderTheSea_AndAnEmergentOneGoesBackToTheDecorBand()
        {
            const int submergedOrder = -8, emergentOrder = 3;

            int under = ShorePlantTideMath.SortingOrderFor(
                true, submergedOrder, emergentOrder, out bool ySortedUnder);
            int above = ShorePlantTideMath.SortingOrderFor(
                false, submergedOrder, emergentOrder, out bool ySortedAbove);

            Assert.AreEqual(submergedOrder, under);
            Assert.AreEqual(emergentOrder, above);

            // The whole submerged-rendering trick, pinned: BELOW the Sea plane (−5) so the water's own
            // shallow transparency reveals the bed, and ABOVE the painted seabed (−21…−18) so the bed
            // is not buried under the ground it grows on.
            Assert.Less(under, -5,
                "A submerged plant must sort BELOW the Sea plane (−5) or the water cannot be seen " +
                "through to it — that transparency is the mechanism, and there is no second one.");
            Assert.Greater(under, -18,
                "A submerged plant must sort ABOVE the painted shore tiles (−18) and the splat ground " +
                "(−21), or the seabed is drawn over the bed growing on it.");

            Assert.IsFalse(ySortedUnder,
                "YSortSprite must stand DOWN while a plant is under water: its band is 2…40, so left " +
                "armed it would immediately float the bed up through the sea.");
            Assert.IsTrue(ySortedAbove,
                "Above water the plant is decor and must sort against the player like grass.");
        }

        [Test]
        public void TheZoneStaircase_PutsEachSpeciesWhereItsTideStoryDiffers()
        {
            var contract = Contract();

            // The staircase is the reason a shore reads as zoned rather than as one wet band: at any
            // one water level the species are in DIFFERENT states, and that is what the eye picks up.
            float[] bases = ShorePlantCatalog.ZoneKeys
                .Select(z => contract.Zones.First(x => x.Key == z).BaseM).ToArray();
            for (int i = 1; i < bases.Length; i++)
                Assert.Greater(bases[i], bases[i - 1],
                    $"The zone staircase is not rising at '{ShorePlantCatalog.ZoneKeys[i]}'.");

            // Half tide: the fringe is drowned and the upland has never been wet. If this ever came
            // out equal, the five zones would be decoration.
            var half = contract.Tide.Steps.First(s => s.Key == "half");
            var fringe = contract.Find("SugarKelp");
            var upland = contract.Find("Bayberry");

            Assert.Greater(half.WaterM - fringe.BaseM, 0f,
                "At half tide the subtidal fringe must be under water.");
            Assert.Less(half.WaterM - upland.BaseM, 0f,
                "At half tide the upland must be dry — an uplander that gets wet has no dry state to " +
                "wear and the whole staircase collapses into one band.");
        }

        [Test]
        public void StateFor_SurvivesAHalfBuiltDef_WithoutTakingTheSceneDown()
        {
            // A Def mid-authoring must not throw: an art asset in a broken state is a normal editor
            // moment, and a plant that hard-fails on it takes the whole scene view with it.
            Assert.AreEqual(0, ShorePlantTideMath.StateFor(1.5f, null));
            Assert.AreEqual(0, ShorePlantTideMath.StateFor(1.5f, new float[0]));
            Assert.AreEqual(0, ShorePlantTideMath.StateFor(99f, new[] { 0.5f }),
                "A one-rung ladder has exactly one answer, whatever the water is doing.");
        }
    }
}
