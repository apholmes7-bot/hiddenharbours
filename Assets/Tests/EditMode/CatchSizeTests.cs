using NUnit.Framework;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>How long the fish in your hands is</b> — the one quantity the hand-slot size rule asks a catch
    /// for, and the only place in this feature where a number is DERIVED rather than measured.
    ///
    /// <para>So it is held to the art rig it came from. <c>fishIsoRig.js</c> declares four species with a
    /// length and a girth and computes mass as <c>len·girth²·scale³·390</c>; this law is that inversion,
    /// and if it drifts from those four the fisher will one-hand a fish the picture cradles (or refuse one
    /// it does not) with nothing else in the game noticing.</para>
    /// </summary>
    public class CatchSizeTests
    {
        // fishIsoRig.js SPECIES — len in metres, and the mass its own hold() computes at scale 1.
        // Restated here rather than read, deliberately: the point is to catch DRIFT between the two, and a
        // test that read the rig would move with it.
        private static readonly (string name, float lengthM, float massKg)[] RigSpecies =
        {
            ("cod",      0.70f, 3.0f),
            ("haddock",  0.55f, 1.7f),
            ("pollock",  0.60f, 2.0f),
            ("mackerel", 0.45f, 0.8f),
        };

        private const float Coefficient = GameConfig.DefaultFishLengthPerKgCubeRootMeters;

        [Test]
        public void TheCubeRootLaw_ReproducesTheRigsOwnFourSpecies()
        {
            foreach ((string name, float lengthM, float massKg) in RigSpecies)
            {
                float derived = CatchSize.LengthMeters(massKg, Coefficient);
                float error = RelativeError(derived, lengthM);
                Assert.That(error, Is.LessThan(0.04f),
                            $"{name}: {massKg} kg → {derived:0.000} m, but the rig draws it at " +
                            $"{lengthM:0.000} m ({error:P1} out). The relation is len ∝ ∛mass because " +
                            "girth tracks length across the roster — if the rig's proportions moved, move " +
                            "GameConfig.FishLengthPerKgCubeRootMeters with them.");
            }
        }

        [Test]
        public void TheShippedThreshold_SplitsTheRosterExactlyWhereTheRigDoes()
        {
            // ⭐ The claim that makes the dial trustworthy: fishIsoRig.hold() cradles at 2.2 kg and hangs
            // below it, so cod is the ONE shipped species that needs both hands at reference size. The
            // length threshold must agree — otherwise the hands and the picture disagree about the same
            // fish, which is the worst shape of wrong here (it looks like a bug in whichever you notice
            // second).
            const float max = GameConfig.DefaultMaxOneHandFishLengthMeters;

            foreach ((string name, float _, float massKg) in RigSpecies)
            {
                bool rigCradles = massKg >= 2.2f;
                bool weCradle = HandSlots.NeedsBothHands(CatchSize.LengthMeters(massKg, Coefficient), max);
                Assert.That(weCradle, Is.EqualTo(rigCradles),
                            $"{name} at {massKg} kg: the rig says {(rigCradles ? "cradle" : "one hand")}");
            }
        }

        [Test]
        public void TheThreshold_IsTheRigsOwnCradlePoint_ExpressedAsALength()
        {
            // Not a number matched by eye: 2.2 kg through the same law IS the shipped default.
            float atTheRigsCradlePoint = CatchSize.LengthMeters(2.2f, Coefficient);
            Assert.That(atTheRigsCradlePoint,
                        Is.EqualTo(GameConfig.DefaultMaxOneHandFishLengthMeters).Within(0.01f),
                        "the dial and the rig's mass threshold are the same fish");
        }

        [Test]
        public void ABigCodNeedsBothHands_AndASmallOneDoesNot_WhichIsTheWholePointOfALengthRule()
        {
            // AtlanticCod.asset ships MinWeightKg 2, MaxWeightKg 12 — so the rule bites WITHIN a species,
            // which a per-species length field could never do.
            const float max = GameConfig.DefaultMaxOneHandFishLengthMeters;
            Assert.That(HandSlots.NeedsBothHands(CatchSize.LengthMeters(2f, Coefficient), max), Is.False,
                        "a small cod hangs from a fist");
            Assert.That(HandSlots.NeedsBothHands(CatchSize.LengthMeters(12f, Coefficient), max), Is.True,
                        "a twelve-kilo one does not");
        }

        [Test]
        public void AnUnweighedCatch_MeasuresZero_SoItNeverJamsTheHands()
        {
            Assert.That(CatchSize.LengthMeters(0f, Coefficient), Is.EqualTo(0f));
            Assert.That(CatchSize.LengthMeters(-1f, Coefficient), Is.EqualTo(0f), "no imaginary fish");
            Assert.That(CatchSize.LengthMeters(3f, 0f), Is.EqualTo(0f), "no coefficient, no length");
        }

        [Test]
        public void LengthRisesWithWeight_Monotonically()
        {
            float previous = 0f;
            for (float kg = 0.1f; kg <= 20f; kg += 0.1f)
            {
                float now = CatchSize.LengthMeters(kg, Coefficient);
                Assert.That(now, Is.GreaterThan(previous), $"at {kg:0.0} kg");
                previous = now;
            }
        }

        /// <summary>Relative error, |derived − actual| / actual — the form the assertions above read in.</summary>
        private static float RelativeError(float derived, float actual)
            => actual == 0f ? 0f : System.Math.Abs(derived - actual) / actual;
    }
}
