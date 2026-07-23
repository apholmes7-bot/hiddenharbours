using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Fishing;
using HiddenHarbours.Player;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The PURE deck-angle maths (Rod Fishing v2 Wave 4 — design §4.2, the owner's "light real
    /// factor"): the deck-frame transform (pinned numerically identical to the Player lane's deck-walk
    /// transform, so the fight grades the very rectangle the player walks), the across-the-hull read
    /// (0 at the clean rail → 1 at the worst stance, continuous and monotone as you walk — the "walk
    /// the rail" relief IS the gradient), and the tension term with its owner off-switch (factor 0 =
    /// dock-parity). Engine-light, headless, NaN-safe (rule 5).
    /// </summary>
    public class DeckAngleMathTests
    {
        // The greybox dory deck (DeckWalkController defaults): half-beam 0.7, half-length 1.6.
        static readonly Vector2 Half = new Vector2(0.7f, 1.6f);
        static readonly Vector2 Center = Vector2.zero;

        static float Across(Vector2 angler, Vector2 fish)
            => DeckAngleMath.AcrossHull01(angler, fish, Center, Half);

        // ---- the deck-frame transform is THE deck-walk frame (parity across the module boundary) ---

        [Test]
        public void WorldToDeckFrame_MatchesTheDeckWalkTransform_Exactly()
        {
            // Rule 4 keeps DeckAngleMath from referencing the Player module, so the four-line rotation
            // lives twice — this parity test is what makes that duplication safe: the fight must grade
            // stances in the SAME frame the player is clamped in, at every heading.
            foreach (float heading in new[] { 0f, 45f, 90f, 137.5f, 180f, 270f, 315f })
            foreach (var offset in new[] { new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(-2.3f, 0.8f) })
            {
                Vector2 fight = DeckAngleMath.WorldToDeckFrame(offset, heading);
                Vector2 walk = DeckWalkController.WorldToDeckFrame(offset, heading);
                Assert.AreEqual(walk.x, fight.x, 1e-6f, $"x diverged at heading {heading}, offset {offset}");
                Assert.AreEqual(walk.y, fight.y, 1e-6f, $"y diverged at heading {heading}, offset {offset}");
            }
        }

        [Test]
        public void WorldToDeckFrame_MapsTheDrawnBowToDeckPlusY()
        {
            // Heading 90 = the picture points East: a world-east offset is dead ahead in the deck frame.
            Vector2 deck = DeckAngleMath.WorldToDeckFrame(new Vector2(1f, 0f), 90f);
            Assert.AreEqual(0f, deck.x, 1e-5f);
            Assert.AreEqual(1f, deck.y, 1e-5f, "world-east reads as the deck's bow (+Y) on an east-drawn hull");
        }

        // ---- the across-the-hull read (the heart) ---------------------------------------------

        [Test]
        public void StandingAtTheFishSideRail_ReadsClean()
        {
            // Angler ON the starboard rail, fish abeam to starboard: the line drops straight over the
            // side — no hull crossed, 0 (exactly: exit distance is zero).
            Assert.AreEqual(0f, Across(new Vector2(0.7f, 0f), new Vector2(5f, 0f)), 1e-5f);
        }

        [Test]
        public void StandingAtTheOppositeRail_ReadsTheWorstStance()
        {
            // Angler on the PORT rail, fish abeam to STARBOARD: the whole beam lies between — 1.
            Assert.AreEqual(1f, Across(new Vector2(-0.7f, 0f), new Vector2(5f, 0f)), 1e-5f);
        }

        [Test]
        public void Amidships_ReadsHalfway()
        {
            Assert.AreEqual(0.5f, Across(new Vector2(0f, 0f), new Vector2(5f, 0f)), 1e-5f);
        }

        [Test]
        public void FishOffTheBow_SternStanceIsBad_BowStanceIsClean()
        {
            // The "fish astern of you on the wrong rail" analogue along the keel: she's off the bow,
            // standing at the stern runs the line the length of the deck; standing at the bow is clean.
            Vector2 fishAhead = new Vector2(0f, 9f);
            Assert.Greater(Across(new Vector2(0f, -1.6f), fishAhead), 0.95f, "stern stance, fish off the bow = bad");
            Assert.AreEqual(0f, Across(new Vector2(0f, 1.6f), fishAhead), 1e-5f, "bow stance, fish off the bow = clean");
        }

        [Test]
        public void WalkingTheRailTowardTheFish_RelievesMonotonically()
        {
            // The cozy prompt IS the gradient: every step abeam toward the fish's rail strictly eases
            // the read — so the player can FEEL their way to the clean angle, no HUD needed.
            Vector2 fish = new Vector2(6f, 0f);
            float prev = float.PositiveInfinity;
            for (float x = -0.7f; x <= 0.7f + 1e-4f; x += 0.1f)
            {
                float now = Across(new Vector2(x, 0.4f), fish);
                Assert.Less(now, prev, $"walking to x={x:F1} must strictly relieve the stance");
                prev = now;
            }
        }

        [Test]
        public void TheReadIsScaleFree_APuntAndADraggerGradeAlike()
        {
            // Same relative stance on a hull 3× the size → the same read (exit/chord is a fraction).
            float small = Across(new Vector2(-0.35f, 0f), new Vector2(4f, 0f));
            float big = DeckAngleMath.AcrossHull01(new Vector2(-1.05f, 0f), new Vector2(12f, 0f),
                                                   Center, Half * 3f);
            Assert.AreEqual(small, big, 1e-4f);
        }

        [Test]
        public void AnglerReadOutsideTheRectangle_IsClampedNotSpiked()
        {
            // A transient rounding/clamp excursion off the deck must grade like the rail it left, never
            // blow up the slab casts.
            float onRail = Across(new Vector2(0.7f, 0f), new Vector2(5f, 0f));
            float outside = Across(new Vector2(0.9f, 0f), new Vector2(5f, 0f));
            Assert.AreEqual(onRail, outside, 1e-5f);
        }

        [Test]
        public void DegenerateInputs_ReadNeutral()
        {
            Assert.AreEqual(0f, Across(new Vector2(0.2f, 0.2f), new Vector2(0.2f, 0.2f)),
                "fish on top of the angler → no direction → neutral");
            Assert.AreEqual(0f, DeckAngleMath.AcrossHull01(Vector2.zero, new Vector2(3f, 0f),
                Center, Vector2.zero), "a collapsed deck grades nothing");
            Assert.AreEqual(0f, DeckAngleMath.AcrossHull01(new Vector2(float.NaN, 0f),
                new Vector2(float.NaN, float.NaN), Center, Half), "NaN anywhere reads the safe neutral (rule 5)");
            Assert.IsFalse(float.IsNaN(DeckAngleMath.AcrossHull01(new Vector2(0.1f, float.NaN),
                new Vector2(5f, 0f), new Vector2(float.NaN, 0f), new Vector2(0.7f, float.NaN))));
        }

        [Test]
        public void TheFrameRotatesWithTheDrawnHeading_TheWeathervaneChangesTheRead()
        {
            // The moving-platform beat (design §4.2 decision #3): same world stance, same world fish —
            // the hull swings 90° under the angler and a clean line becomes a line across the deck.
            Vector2 hull = Vector2.zero;
            Vector2 anglerWorld = new Vector2(0.7f, 0f);   // standing at what is the starboard rail at heading 0
            Vector2 fishWorld = new Vector2(6f, 0f);       // fish out east

            float north = DeckAngleMath.AcrossHull01(
                DeckAngleMath.WorldToDeckFrame(anglerWorld - hull, 0f),
                DeckAngleMath.WorldToDeckFrame(fishWorld - hull, 0f), Center, Half);
            float east = DeckAngleMath.AcrossHull01(
                DeckAngleMath.WorldToDeckFrame(anglerWorld - hull, 90f),
                DeckAngleMath.WorldToDeckFrame(fishWorld - hull, 90f), Center, Half);

            Assert.AreEqual(0f, north, 1e-5f, "hull drawn north: the fish is straight off the rail you stand at");
            // After the swing the angler stands 0.9 m from the (new) bow with the fish dead ahead — the
            // line now runs that stretch of deck (exit 0.9 of a 3.2 chord ≈ 0.28): a real, felt pressure
            // where there was none, purely from the hull turning under a stationary player.
            Assert.Greater(east, 0.25f, "the hull weathervanes east under you: the same line now runs up the deck");
            Assert.Greater(east, north, "the swing must strictly worsen this stance");
        }

        // ---- the tension term + the owner's off-switch -----------------------------------------

        [Test]
        public void TensionPerSec_ScalesTheFactor_AndZeroIsOff()
        {
            Assert.AreEqual(0.15f, DeckAngleMath.TensionPerSec(1f, 0.15f), 1e-6f, "worst stance = the full factor");
            Assert.AreEqual(0.075f, DeckAngleMath.TensionPerSec(0.5f, 0.15f), 1e-6f, "half across = half the pressure");
            Assert.AreEqual(0f, DeckAngleMath.TensionPerSec(1f, 0f), "factor 0 = OFF — the dock-parity switch");
            Assert.AreEqual(0f, DeckAngleMath.TensionPerSec(1f, -0.3f), "a negative factor clamps to off, never pays");
            Assert.AreEqual(0f, DeckAngleMath.TensionPerSec(float.NaN, float.NaN), "NaN-safe (rule 5)");
        }
    }
}
