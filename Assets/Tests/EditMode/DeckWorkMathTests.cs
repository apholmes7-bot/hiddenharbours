using NUnit.Framework;
using HiddenHarbours.Fishing;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The Build-7 deck-work derivations are PURE and DETERMINISTIC (rule 5): per-animal size, berried
    /// flag, and per-grab nip rolls are functions of the trap's placement facts (the SAME seed lineage as
    /// the catch roll — worldSeed + instanceId + placement time) plus the animal's resolved identity
    /// (species id + index) and a channel salt. Same inputs ⇒ bit-identical outputs, every run — never
    /// Time, never the wave field, no hidden global RNG.
    /// </summary>
    public class DeckWorkMathTests
    {
        private const int Seed = 4242;
        private const string Instance = "trap.lobster#5000.1";
        private const double Placed = 5000.0;

        // ---- determinism ---------------------------------------------------------------------

        [Test]
        public void AnimalHash_IsStable_ForTheSameFacts()
        {
            uint a = DeckWork.AnimalHash(Seed, Instance, Placed, "fish.lobster", 0, DeckWork.SizeChannel);
            uint b = DeckWork.AnimalHash(Seed, Instance, Placed, "fish.lobster", 0, DeckWork.SizeChannel);
            Assert.AreEqual(a, b, "same placement facts + identity + channel ⇒ the same hash, every run");
        }

        [Test]
        public void AnimalHash_Differs_AcrossIdentityAndChannelAndAttempt()
        {
            uint baseline = DeckWork.AnimalHash(Seed, Instance, Placed, "fish.lobster", 0, DeckWork.NipChannel, 0);

            Assert.AreNotEqual(baseline,
                DeckWork.AnimalHash(Seed + 1, Instance, Placed, "fish.lobster", 0, DeckWork.NipChannel, 0),
                "a different world seed is a different stream");
            Assert.AreNotEqual(baseline,
                DeckWork.AnimalHash(Seed, Instance + "x", Placed, "fish.lobster", 0, DeckWork.NipChannel, 0),
                "a different trap instance is a different stream");
            Assert.AreNotEqual(baseline,
                DeckWork.AnimalHash(Seed, Instance, Placed, "fish.rock_crab", 0, DeckWork.NipChannel, 0),
                "a different species identity is a different stream");
            Assert.AreNotEqual(baseline,
                DeckWork.AnimalHash(Seed, Instance, Placed, "fish.lobster", 1, DeckWork.NipChannel, 0),
                "a different animal index is a different stream");
            Assert.AreNotEqual(baseline,
                DeckWork.AnimalHash(Seed, Instance, Placed, "fish.lobster", 0, DeckWork.SizeChannel, 0),
                "the size channel never reuses the nip stream");
            Assert.AreNotEqual(baseline,
                DeckWork.AnimalHash(Seed, Instance, Placed, "fish.lobster", 0, DeckWork.NipChannel, 1),
                "a re-try (next attempt) draws a fresh — still deterministic — roll");
        }

        [Test]
        public void SizeMm_IsDeterministic_AndInsideTheWindow()
        {
            for (int i = 0; i < 32; i++)
            {
                uint h = DeckWork.AnimalHash(Seed, Instance, Placed, "fish.lobster", i, DeckWork.SizeChannel);
                float size = DeckWork.SizeMm(h, 62f, 140f);
                Assert.AreEqual(size, DeckWork.SizeMm(h, 62f, 140f), "same hash ⇒ same size");
                Assert.GreaterOrEqual(size, 62f, "size never leaves the Def's window (low)");
                Assert.LessOrEqual(size, 140f, "size never leaves the Def's window (high)");
            }
        }

        [Test]
        public void SizeMm_CollapsesADegenerateWindow_ToTheMin()
        {
            uint h = DeckWork.AnimalHash(Seed, Instance, Placed, "fish.lobster", 0, DeckWork.SizeChannel);
            Assert.AreEqual(90f, DeckWork.SizeMm(h, 90f, 90f), 1e-4f, "a zero-width window is the one value");
        }

        // ---- the berried flag ------------------------------------------------------------------

        [Test]
        public void RollBerried_RespectsTheSpeciesGate_AndTheChanceEnds()
        {
            uint h = DeckWork.AnimalHash(Seed, Instance, Placed, "fish.lobster", 0, DeckWork.BerriedChannel);
            Assert.IsFalse(DeckWork.RollBerried(h, canBeBerried: false, berriedChance01: 1f),
                "a species that can't be berried never is (the crab rule)");
            Assert.IsTrue(DeckWork.RollBerried(h, canBeBerried: true, berriedChance01: 1f),
                "chance 1 ⇒ always berried");
            Assert.IsFalse(DeckWork.RollBerried(h, canBeBerried: true, berriedChance01: 0f),
                "chance 0 ⇒ never berried");
        }

        // ---- the care read (the nip eases with a fuller hold) -----------------------------------

        [Test]
        public void Care01_MapsTheHoldWindow()
        {
            Assert.AreEqual(0f, DeckWork.Care01(0.15f, 0.15f, 0.9f), 1e-5f, "a release at the quick mark is zero care");
            Assert.AreEqual(1f, DeckWork.Care01(0.9f, 0.15f, 0.9f), 1e-5f, "a full hold is full care");
            Assert.AreEqual(1f, DeckWork.Care01(5f, 0.15f, 0.9f), 1e-5f, "holding past full stays full (clamped)");
            float mid = DeckWork.Care01(0.525f, 0.15f, 0.9f);
            Assert.AreEqual(0.5f, mid, 1e-4f, "half the window is half the care (linear)");
        }

        [Test]
        public void Care01_DegenerateTuning_ReadsFullCare()
        {
            Assert.AreEqual(1f, DeckWork.Care01(0.2f, 0.5f, 0.5f), 1e-5f,
                "full ≤ quick collapses to always-careful (the forgiving default, never a divide-by-zero)");
        }

        [Test]
        public void NipChance_EasesFromRushedToCareful()
        {
            Assert.AreEqual(0.55f, DeckWork.NipChance01(0f, 0.55f, 0.06f), 1e-5f, "zero care risks the rushed chance");
            Assert.AreEqual(0.06f, DeckWork.NipChance01(1f, 0.55f, 0.06f), 1e-5f, "full care eases to the careful floor");
            Assert.Less(DeckWork.NipChance01(0.5f, 0.55f, 0.06f), 0.55f, "…monotonically easing between");
            Assert.Greater(DeckWork.NipChance01(0.5f, 0.55f, 0.06f), 0.06f);
        }

        [Test]
        public void RollNip_IsDeterministic_AndObeysTheChanceEnds()
        {
            uint h = DeckWork.AnimalHash(Seed, Instance, Placed, "fish.lobster", 0, DeckWork.NipChannel, 0);
            Assert.AreEqual(DeckWork.RollNip(h, 0.5f), DeckWork.RollNip(h, 0.5f), "same hash + chance ⇒ same nip");
            Assert.IsTrue(DeckWork.RollNip(h, 1f), "chance 1 always nips");
            Assert.IsFalse(DeckWork.RollNip(h, 0f), "chance 0 never nips");
        }

        // ---- the sort verdict --------------------------------------------------------------------

        [Test]
        public void IsKeeper_GaugesSize_AndBerriedAlwaysGoesBack()
        {
            Assert.IsTrue(DeckWork.IsKeeper(90f, berried: false, minKeepSizeMm: 83f), "legal size, not berried → keeper");
            Assert.IsFalse(DeckWork.IsKeeper(74f, berried: false, minKeepSizeMm: 83f), "a short goes back");
            Assert.IsFalse(DeckWork.IsKeeper(120f, berried: true, minKeepSizeMm: 83f),
                "a berried hen goes back regardless of size (the honest-fishery read)");
        }

        [Test]
        public void U01_StaysInTheHalfOpenUnitInterval()
        {
            Assert.GreaterOrEqual(DeckWork.U01(0u), 0f);
            Assert.Less(DeckWork.U01(uint.MaxValue), 1f, "even the max hash maps under 1");
        }
    }
}
