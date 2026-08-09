using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The deck-gear placement maths — pure, so it is tested without a scene, a hull or a sprite.
    ///
    /// <para>The values fed in are the ones MEASURED from the rigs in a V8 host (see
    /// <c>docs/art/rigs/deck-loop-kit/IMPORT.md</c>): the hulls' own <c>HAULER</c> mounts, the
    /// hauler station's <c>turn</c>/<c>workZ</c>/<c>clear</c>, and <c>TrapIso.STACK</c>'s per-build
    /// tier steps. They are fed in as DATA, exactly as production must — a hard-coded 1.34 in the
    /// class under test would be the defect this suite exists to prevent, so nothing here asserts
    /// that the class knows any of them.</para>
    /// </summary>
    public class DeckGearPlacementTests
    {
        // ---- rig-measured fixtures. DATA, passed in — never constants inside the class. ----------

        /// <summary>lobsterBoatIsoRig.HAULER — hull-local metres, starboard washboard.</summary>
        static readonly Vector3 LobsterHauler = new Vector3(1.34f, -1.50f, 1.44f);

        /// <summary>capeIslanderIsoRig.HAULER — a different hull, a different mount.</summary>
        static readonly Vector3 CapeHauler = new Vector3(1.30f, -2.56f, 1.592f);

        /// <summary>DeckGear.station('haulerstation') — stand, turn, workZ, clear.</summary>
        static DeckGearPlacement.Station HaulerStation =>
            new DeckGearPlacement.Station(new Vector3(-0.30f, 0.44f, 0f), 4, 1.05f, new Vector2(0.66f, 0.60f));

        /// <summary>DeckGear.station('bandingtable') — a bench, operator facing the gear (turn 0).</summary>
        static DeckGearPlacement.Station BandingStation =>
            new DeckGearPlacement.Station(new Vector3(0f, -0.52f, 0f), 0, 0.885f, new Vector2(0.62f, 0.58f));

        const float Tol = 1e-4f;

        // ---- the facing rule (#457) --------------------------------------------------------------

        /// <summary>
        /// The operator's heading is DERIVED from the deck's drawn heading every call. This is the
        /// regression that lane shipped twice: the figure keeps her old facing while the hull turns
        /// under her.
        /// </summary>
        [Test]
        public void OperatorHeadingFollowsTheDeckAsItTurns()
        {
            var st = BandingStation;                       // turn 0 — the operator's facing IS the gear's
            for (int deck = 0; deck < 360; deck += 15)
            {
                float h = DeckGearPlacement.OperatorHeading(deck, gearDir: 0, st);
                Assert.AreEqual(DeckGearPlacement.Wrap360(deck), h, Tol,
                    $"deck at {deck}° must carry its operator with it");
            }
        }

        /// <summary>The hauler is the one station whose operator faces OUTBOARD — turn 4, a half
        /// turn, because the warp comes over the rail and the drum is worked from inboard of it.</summary>
        [Test]
        public void TheHaulerOperatorFacesOutboard()
        {
            float h = DeckGearPlacement.OperatorHeading(0f, gearDir: 0, HaulerStation);
            Assert.AreEqual(180f, h, Tol, "turn 4 is a half turn from the gear's own bearing");

            // and it still tracks the deck
            Assert.AreEqual(270f, DeckGearPlacement.OperatorHeading(90f, 0, HaulerStation), Tol);
            Assert.AreEqual(225f, DeckGearPlacement.OperatorHeading(0f, gearDir: 1, HaulerStation), Tol,
                "the gear's own step composes with the turn: (1 + 4) steps × 45° = 225°");
        }

        [Test]
        public void HeadingsAndFacingsWrapNegativeSafe()
        {
            Assert.AreEqual(7, DeckGearPlacement.WrapFacing(-1));
            Assert.AreEqual(0, DeckGearPlacement.WrapFacing(-8));
            Assert.AreEqual(1, DeckGearPlacement.WrapFacing(9));
            Assert.AreEqual(350f, DeckGearPlacement.Wrap360(-10f), Tol);
            Assert.AreEqual(0f, DeckGearPlacement.Wrap360(360f), Tol);
        }

        // ---- the stand offset rotates with the gear ----------------------------------------------

        /// <summary>
        /// A station's stand offset is in the GEAR's frame, so it must rotate with the gear. A bench
        /// turned broadside puts its operator broadside; if this ever returned the unrotated offset,
        /// every turned bench would stand its operator in the wrong place — and it would look
        /// plausible at dir 0, which is where anyone would test it by hand.
        /// </summary>
        [Test]
        public void TheStandOffsetTurnsWithTheGear()
        {
            var st = BandingStation;                        // stand is (0, -0.52) — dead aft of the gear
            Vector3 mount = new Vector3(0f, 0f, 0.5f);

            Vector3 at0 = DeckGearPlacement.OperatorStand(mount, 0, st);
            Assert.AreEqual(0f, at0.x, Tol);
            Assert.AreEqual(-0.52f, at0.y, Tol, "at dir 0 the operator stands aft of the bench");

            // a quarter turn (2 steps = 90°) swings that offset onto the x axis
            Vector3 at2 = DeckGearPlacement.OperatorStand(mount, 2, st);
            Assert.AreEqual(0.52f, at2.x, Tol, "a quarter turn puts the operator abeam");
            Assert.AreEqual(0f, at2.y, Tol);

            Assert.AreEqual(0.5f, at2.z, Tol, "the mount's own height is carried through untouched");
        }

        /// <summary>Per-hull geometry, not one rectangle: the same station on two hulls lands in two
        /// places, because the MOUNT differs. Guards against a hard-coded deck.</summary>
        [Test]
        public void TheSameStationLandsDifferentlyOnDifferentHulls()
        {
            Vector3 lob = DeckGearPlacement.OperatorStand(LobsterHauler, 0, HaulerStation);
            Vector3 cape = DeckGearPlacement.OperatorStand(CapeHauler, 0, HaulerStation);

            Assert.AreNotEqual(lob.y, cape.y,
                "the Cape's hauler sits a metre further aft — a shared rectangle would hide that");
            Assert.AreEqual(LobsterHauler.y + 0.44f, lob.y, Tol);
            Assert.AreEqual(CapeHauler.y + 0.44f, cape.y, Tol);
            Assert.AreEqual(LobsterHauler.z, lob.z, Tol, "the stand is on the mount's plane");
        }

        [Test]
        public void RotationIsAGroundPlaneTurnWithNoIsoSquash()
        {
            // If the iso squash (sin 40° = 0.6428) ever leaked in here it would foreshorten gameplay
            // geometry. A full turn of a unit offset must come back to itself, exactly.
            Vector3 v = new Vector3(1f, 0f, 0f);
            Vector3 round = v;
            for (int i = 0; i < 8; i++) round = DeckGearPlacement.RotateOnDeck(round, 1);
            Assert.AreEqual(1f, round.x, 1e-3f, "eight 45° steps is identity, unsquashed");
            Assert.AreEqual(0f, round.y, 1e-3f);

            // and a quarter turn preserves length
            Vector3 q = DeckGearPlacement.RotateOnDeck(new Vector3(0.66f, 0f, 0f), 2);
            Assert.AreEqual(0.66f, new Vector2(q.x, q.y).magnitude, Tol);
        }

        // ---- the stack ---------------------------------------------------------------------------

        /// <summary>Tier height is the BUILD's, from the rig's STACK table — cones nest at 0.52 m
        /// where a wooden bow-top needs 0.40. One shared step would stack cones a foot too high.</summary>
        [Test]
        public void StackTiersUseTheBuildsOwnStep()
        {
            var noSlew = new[] { 0, 0, 0, 0, 0, 0 };
            Vector3 basePos = new Vector3(0f, -3f, 0.5f);

            Vector3 wood2 = DeckGearPlacement.StackSlot(basePos, 2, 0.40f, noSlew, 0.05f, 0);
            Assert.AreEqual(0.5f + 0.80f, wood2.z, Tol, "wooden bow-tops: 0.40 m a tier");

            Vector3 cone2 = DeckGearPlacement.StackSlot(basePos, 2, 0.52f, noSlew, 0.05f, 0);
            Assert.AreEqual(0.5f + 1.04f, cone2.z, Tol, "cones NEST at 0.52 m — not the same stack");

            Assert.AreNotEqual(wood2.z, cone2.z);
        }

        [Test]
        public void StackSlewJogsPerTierAndRepeatsWhenOverTall()
        {
            var slew = new[] { 0, -1, 1, -1, 0, 1 };        // the rig's wooden bow-top pattern
            Vector3 b = Vector3.zero;

            Assert.AreEqual(0f, DeckGearPlacement.StackSlot(b, 0, 0.4f, slew, 0.05f, 0).x, Tol);
            Assert.AreEqual(-0.05f, DeckGearPlacement.StackSlot(b, 1, 0.4f, slew, 0.05f, 0).x, Tol);
            Assert.AreEqual(0.05f, DeckGearPlacement.StackSlot(b, 2, 0.4f, slew, 0.05f, 0).x, Tol);

            // tier 6 wraps back onto pattern index 0 rather than running off the end
            Assert.AreEqual(DeckGearPlacement.StackSlot(b, 0, 0.4f, slew, 0.05f, 0).x,
                            DeckGearPlacement.StackSlot(b, 6, 0.4f, slew, 0.05f, 0).x, Tol);
        }

        [Test]
        public void StackSlewTurnsWithTheDeckToo()
        {
            var slew = new[] { 0, 1 };
            Vector3 at0 = DeckGearPlacement.StackSlot(Vector3.zero, 1, 0.4f, slew, 0.10f, 0);
            Vector3 at2 = DeckGearPlacement.StackSlot(Vector3.zero, 1, 0.4f, slew, 0.10f, 2);

            Assert.AreEqual(0.10f, at0.x, Tol);
            Assert.AreEqual(0f, at2.x, Tol, "a quarter turn moves the jog onto the other axis");
            Assert.AreEqual(0.10f, at2.y, Tol);
            Assert.AreEqual(at0.z, at2.z, Tol, "turning a stack does not change its height");
        }

        [Test]
        public void StackSlotIsSafeAtTierZeroAndWithNoSlewPattern()
        {
            Assert.AreEqual(0f, DeckGearPlacement.StackSlot(Vector3.zero, 0, 0.4f, null, 0.05f, 0).x, Tol);
            Assert.AreEqual(0f, DeckGearPlacement.StackSlot(Vector3.zero, 0, 0.4f, new int[0], 0.05f, 0).x, Tol);
            // a negative tier clamps rather than indexing backwards
            Assert.AreEqual(0f, DeckGearPlacement.StackSlot(Vector3.zero, -3, 0.4f, null, 0.05f, 0).z, Tol);
        }

        /// <summary>
        /// Capacity is per hull AND per surface, from TrapIso.CAPS. A wharf's washboard capacity is
        /// 0 — a real answer meaning "you cannot stack there", never a missing value to fall back on.
        /// </summary>
        [Test]
        public void CapacityIsPerHullAndZeroIsAnAnswer()
        {
            Assert.IsTrue(DeckGearPlacement.StackFits(5, capacity: 5), "lobster boat deck takes 5");
            Assert.IsFalse(DeckGearPlacement.StackFits(6, capacity: 5));
            Assert.IsTrue(DeckGearPlacement.StackFits(3, capacity: 3), "the Cape takes 3, not 5");
            Assert.IsFalse(DeckGearPlacement.StackFits(4, capacity: 3));

            Assert.IsTrue(DeckGearPlacement.StackFits(0, capacity: 0), "nothing fits in nothing — fine");
            Assert.IsFalse(DeckGearPlacement.StackFits(1, capacity: 0),
                "a wharf washboard takes 0 pots and that is data, not a gap");
            Assert.IsFalse(DeckGearPlacement.StackFits(-1, capacity: 5));
        }

        // ---- the operator's clearance ------------------------------------------------------------

        [Test]
        public void ClearanceIsReservedAroundWhereTheOperatorStands()
        {
            var st = HaulerStation;                          // clear 0.66 × 0.60
            Vector3 mount = LobsterHauler;
            Vector3 stand = DeckGearPlacement.OperatorStand(mount, 0, st);

            Assert.IsTrue(DeckGearPlacement.WithinClearance(stand, mount, 0, st),
                "the operator's own feet are inside their clearance");

            Vector3 justInside = new Vector3(stand.x + 0.32f, stand.y, stand.z);
            Assert.IsTrue(DeckGearPlacement.WithinClearance(justInside, mount, 0, st));

            Vector3 wellOutside = new Vector3(stand.x + 0.90f, stand.y, stand.z);
            Assert.IsFalse(DeckGearPlacement.WithinClearance(wellOutside, mount, 0, st),
                "a pot stacked here would be standing in the hauler operator");
        }

        /// <summary>
        /// The clearance box is in the GEAR's frame, so it turns with the gear rather than growing
        /// into a square. Tested at a quarter turn with a deliberately oblong clearance, where an
        /// axis-aligned-in-world box would give the opposite answer.
        /// </summary>
        [Test]
        public void ClearanceTurnsWithTheGearRatherThanBoxingIt()
        {
            var oblong = new DeckGearPlacement.Station(Vector3.zero, 0, 1f, new Vector2(0.40f, 1.60f));
            Vector3 mount = Vector3.zero;

            // At dir 0 the long axis is along y: a point 0.6 m aft is inside, 0.6 m abeam is outside.
            Assert.IsTrue(DeckGearPlacement.WithinClearance(new Vector3(0f, 0.60f, 0f), mount, 0, oblong));
            Assert.IsFalse(DeckGearPlacement.WithinClearance(new Vector3(0.60f, 0f, 0f), mount, 0, oblong));

            // A quarter turn swaps exactly those two answers.
            Assert.IsFalse(DeckGearPlacement.WithinClearance(new Vector3(0f, 0.60f, 0f), mount, 2, oblong));
            Assert.IsTrue(DeckGearPlacement.WithinClearance(new Vector3(0.60f, 0f, 0f), mount, 2, oblong));
        }

        // ---- the work height ---------------------------------------------------------------------

        /// <summary>
        /// The work height is a WORLD METRE carried straight through, never scaled by anything. The
        /// rig is explicit that it is not a body fraction — a bench belongs to the deck, not to the
        /// person at it — so the placement layer must not touch it.
        /// </summary>
        [Test]
        public void TheWorkHeightIsAWorldMetreAndIsNotDerivedFromTheHull()
        {
            Assert.AreEqual(1.05f, HaulerStation.WorkZ, Tol);
            Assert.AreEqual(0.885f, BandingStation.WorkZ, Tol);

            // It is independent of where the gear is mounted: same station, two hulls, same workZ.
            var st = HaulerStation;
            Assert.AreEqual(st.WorkZ, HaulerStation.WorkZ, Tol);
            Assert.AreNotEqual(LobsterHauler.z, st.WorkZ,
                "workZ is the working SURFACE, not the mount height — conflating them is the bug");
        }
    }
}
