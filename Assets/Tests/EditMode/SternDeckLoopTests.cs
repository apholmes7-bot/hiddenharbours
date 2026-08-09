using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The stern-deck gear cycle's arithmetic — pure, so it is tested without a scene, a hull or a sprite.
    ///
    /// <para>Every capacity, tier step and slew fed in here is MEASURED from the rigs
    /// (<c>TrapIso.CAPS</c>, <c>TrapIso.STACK</c>) and passed in as DATA, exactly as production must. A
    /// capacity written inside <see cref="SternDeckLoop"/> would be the defect this suite exists to
    /// prevent, so nothing here asserts that the class knows any of them — the tests that pin the numbers
    /// pin them against the ASSETS, in <c>DeckGearContentValidationTests</c>.</para>
    /// </summary>
    public class SternDeckLoopTests
    {
        // ---- rig-measured fixtures. DATA, passed in — never constants inside the class. -------------

        /// <summary>TrapIso.CAPS.lobsterBoat — 5 on the deck, 2 on the washboard.</summary>
        const int LobsterDeckCap = 5, LobsterWashCap = 2;

        /// <summary>TrapIso.CAPS.capeIslander — a different hull, different numbers.</summary>
        const int CapeDeckCap = 3, CapeWashCap = 2;

        /// <summary>TrapIso.STACK.wood — a bow-top nests 0.40 m a tier, with a real stack's lean.</summary>
        const float WoodStep = 0.40f;
        static readonly int[] WoodSlew = { 0, -1, 1, -1, 0, 1 };

        /// <summary>TrapIso.STACK.crabCone — cones NEST at 0.52 m, not at their 1.18 m height, and they
        /// stack dead straight (the rig's slew is all zeros).</summary>
        const float ConeStep = 0.52f;
        static readonly int[] ConeSlew = { 0, 0, 0, 0, 0, 0 };

        const float SlewMetres = 0.04f;
        const float Tol = 1e-4f;

        // ---- the capacity rule: deck first, then washboard, then a REFUSAL -------------------------

        /// <summary>
        /// Pots fill the working deck before they go on the washboard — that is where the hands are, and
        /// it is what a working boat actually does.
        /// </summary>
        [Test]
        public void PotsFillTheDeckBeforeTheWashboard()
        {
            for (int on = 0; on < LobsterDeckCap; on++)
                Assert.AreEqual(DeckStackSurface.Deck,
                    SternDeckLoop.ChooseStackSurface(on, LobsterDeckCap, 0, LobsterWashCap),
                    $"pot {on + 1} should go on the deck while it has room");

            Assert.AreEqual(DeckStackSurface.Washboard,
                SternDeckLoop.ChooseStackSurface(LobsterDeckCap, LobsterDeckCap, 0, LobsterWashCap),
                "a full deck sends the next pot to the washboard");
        }

        /// <summary>
        /// <b>A full deck REFUSES, and the refusal is a real answer.</b> This is the seam's own discipline
        /// restated for gear: over-claiming silently is the defect, so the maths must have a word for
        /// "nowhere left" rather than wrapping round to the deck.
        /// </summary>
        [Test]
        public void AFullHullRefusesTheNextPot()
        {
            Assert.AreEqual(DeckStackSurface.None,
                SternDeckLoop.ChooseStackSurface(LobsterDeckCap, LobsterDeckCap,
                                                 LobsterWashCap, LobsterWashCap),
                "every surface full must refuse, not silently re-use the deck");
        }

        /// <summary>
        /// A capacity of 0 means "you cannot stack there" and is not a missing value to fall back from —
        /// a wharf's washboard is exactly that.
        /// </summary>
        [Test]
        public void ZeroCapacityIsARealAnswerNotAMissingOne()
        {
            Assert.AreEqual(DeckStackSurface.Deck,
                SternDeckLoop.ChooseStackSurface(0, 6, 0, 0), "the wharf's deck still takes pots");
            Assert.AreEqual(DeckStackSurface.None,
                SternDeckLoop.ChooseStackSurface(6, 6, 0, 0),
                "a washboard capacity of 0 must refuse rather than accept a pot");
        }

        /// <summary>
        /// <b>The two hulls do not agree, and nothing in the maths may assume they do.</b> The same
        /// question asked with each hull's own CAPS gives different answers at the same pot count — which
        /// is the whole reason capacities are data.
        /// </summary>
        [Test]
        public void TheTwoHullsDisagreeAtTheirOwnCapacities()
        {
            // Three pots aboard: still deck-room on a lobster boat, already onto the washboard on a Cape.
            Assert.AreEqual(DeckStackSurface.Deck,
                SternDeckLoop.ChooseStackSurface(3, LobsterDeckCap, 0, LobsterWashCap));
            Assert.AreEqual(DeckStackSurface.Washboard,
                SternDeckLoop.ChooseStackSurface(3, CapeDeckCap, 0, CapeWashCap));

            Assert.AreEqual(7, SternDeckLoop.TotalStackCapacity(LobsterDeckCap, LobsterWashCap));
            Assert.AreEqual(5, SternDeckLoop.TotalStackCapacity(CapeDeckCap, CapeWashCap));
        }

        // ---- the tier maths (DeckGearPlacement, driven with the rigs' own STACK data) ---------------

        /// <summary>
        /// A stack rises by the POT's own tier step, not the hull's — and a cone's 0.52 m nest is nothing
        /// like its 1.18 m height, which is exactly why the step is data.
        /// </summary>
        [Test]
        public void EachTierRisesByThePotsOwnStep()
        {
            Vector3 basePos = new Vector3(-0.3f, -5.3f, 0.5f);

            for (int tier = 0; tier < 5; tier++)
            {
                Vector3 wood = DeckGearPlacement.StackSlot(basePos, tier, WoodStep, WoodSlew, SlewMetres, 0);
                Assert.AreEqual(basePos.z + tier * WoodStep, wood.z, Tol,
                    $"wood tier {tier} sits {tier} × 0.40 m above the base");

                Vector3 cone = DeckGearPlacement.StackSlot(basePos, tier, ConeStep, ConeSlew, SlewMetres, 0);
                Assert.AreEqual(basePos.z + tier * ConeStep, cone.z, Tol,
                    $"cone tier {tier} NESTS at 0.52 m a tier");
            }
        }

        /// <summary>
        /// A real stack leans; a cone stack does not. Both come from the rig's own slew pattern, so this
        /// pins that the pattern is READ rather than invented.
        /// </summary>
        [Test]
        public void TheLeanComesFromThePotsOwnSlewPattern()
        {
            Vector3 basePos = Vector3.zero;

            // Wood: the pattern's own jogs, at dir 0 (no rotation) they land straight on x.
            for (int tier = 0; tier < WoodSlew.Length; tier++)
            {
                Vector3 p = DeckGearPlacement.StackSlot(basePos, tier, WoodStep, WoodSlew, SlewMetres, 0);
                Assert.AreEqual(WoodSlew[tier] * SlewMetres, p.x, Tol, $"wood tier {tier} jog");
            }

            // Cones: dead straight, every tier.
            for (int tier = 0; tier < 8; tier++)
            {
                Vector3 p = DeckGearPlacement.StackSlot(basePos, tier, ConeStep, ConeSlew, SlewMetres, 0);
                Assert.AreEqual(0f, p.x, Tol, $"cone tier {tier} must not jog");
                Assert.AreEqual(0f, p.y, Tol, $"cone tier {tier} must not jog");
            }
        }

        /// <summary>The slew pattern REPEATS when a stack outgrows it — an over-tall stack looks wrong
        /// rather than throwing.</summary>
        [Test]
        public void TheSlewPatternRepeatsOnAnOverTallStack()
        {
            Vector3 a = DeckGearPlacement.StackSlot(Vector3.zero, 1, WoodStep, WoodSlew, SlewMetres, 0);
            Vector3 b = DeckGearPlacement.StackSlot(Vector3.zero, 1 + WoodSlew.Length, WoodStep,
                                                    WoodSlew, SlewMetres, 0);
            Assert.AreEqual(a.x, b.x, Tol, "the jog repeats one pattern-length up");
            Assert.AreEqual(a.y, b.y, Tol);
            Assert.AreEqual(WoodSlew.Length * WoodStep, b.z - a.z, Tol, "…but the height keeps climbing");
        }

        /// <summary>
        /// The lean is rotated by the STACK's own bearing, so a stack leans along the hull rather than
        /// across her whatever way round it was set down.
        /// </summary>
        [Test]
        public void TheLeanTurnsWithTheStacksOwnBearing()
        {
            // dir 2 is a quarter turn on the kit's turntable: the jog that ran along +x now runs along +y.
            Vector3 p = DeckGearPlacement.StackSlot(Vector3.zero, 2, WoodStep, WoodSlew, SlewMetres, 2);
            Assert.AreEqual(0f, p.x, Tol, "a quarter-turned stack jogs across, not along");
            Assert.AreEqual(WoodSlew[2] * SlewMetres, p.y, Tol);
        }

        // ---- the clip rules ------------------------------------------------------------------------

        /// <summary>Each beat draws with the clip the rig authored for it, and the beats with no clip of
        /// their own say so rather than borrowing one.</summary>
        [Test]
        public void EachBeatDrawsWithItsOwnClip()
        {
            Assert.AreEqual(CharacterClip.Hauler, SternDeckLoop.ClipFor(SternDeckStage.Hauling));
            Assert.AreEqual(CharacterClip.Place, SternDeckLoop.ClipFor(SternDeckStage.Aboard));
            Assert.AreEqual(CharacterClip.Bench, SternDeckLoop.ClipFor(SternDeckStage.Emptying));
            Assert.AreEqual(CharacterClip.Chop, SternDeckLoop.ClipFor(SternDeckStage.Baiting));
            Assert.AreEqual(CharacterClip.Lift, SternDeckLoop.ClipFor(SternDeckStage.Stacking));
            Assert.AreEqual(CharacterClip.Toss, SternDeckLoop.ClipFor(SternDeckStage.Setting));
            Assert.AreEqual(CharacterClip.None, SternDeckLoop.ClipFor(SternDeckStage.None));
        }

        /// <summary>
        /// <b>The three driven beats are exactly the three LOOPING clips</b> — the #469 rule made
        /// checkable: a beat paced by a quantity loops, and a beat with a duration does not.
        /// </summary>
        [Test]
        public void OnlyTheQuantityPacedBeatsAreDriven()
        {
            Assert.IsTrue(SternDeckLoop.IsQuantityPaced(SternDeckStage.Hauling));
            Assert.IsTrue(SternDeckLoop.IsQuantityPaced(SternDeckStage.Emptying));
            Assert.IsTrue(SternDeckLoop.IsQuantityPaced(SternDeckStage.Baiting));

            Assert.IsFalse(SternDeckLoop.IsQuantityPaced(SternDeckStage.Aboard));
            Assert.IsFalse(SternDeckLoop.IsQuantityPaced(SternDeckStage.Stacking));
            Assert.IsFalse(SternDeckLoop.IsQuantityPaced(SternDeckStage.Setting));
            Assert.IsFalse(SternDeckLoop.IsQuantityPaced(SternDeckStage.None));
        }

        /// <summary>
        /// <b>The hands move with the WORK, and a clock never enters it.</b> Twice the quantity is twice
        /// the way round the clip, at any frame count — and a re-authored clip with more frames does not
        /// quietly change how many times round a whole beat turns the hands.
        /// </summary>
        [Test]
        public void TheDrivenPositionIsTheQuantityAndNothingElse()
        {
            const float cycles = 4f;

            Assert.AreEqual(0f, SternDeckLoop.DrivenFramePosition(0f, cycles, 8), Tol,
                "no work done, no frames advanced");
            Assert.AreEqual(8f * cycles, SternDeckLoop.DrivenFramePosition(1f, cycles, 8), Tol,
                "a whole beat is `cycles` times round the clip");
            Assert.AreEqual(2f * SternDeckLoop.DrivenFramePosition(0.25f, cycles, 8),
                            SternDeckLoop.DrivenFramePosition(0.5f, cycles, 8), Tol,
                "twice the work is twice the travel — linear in the quantity");

            // Same cycles-per-beat, different art: the number of cycles is preserved, not the frame count.
            Assert.AreEqual(cycles,
                SternDeckLoop.DrivenFramePosition(1f, cycles, 10) / 10f, Tol,
                "a 10-frame re-bake still turns the hands `cycles` times over a whole beat");
        }

        /// <summary>Nothing winds the hands backwards: a negative quantity clamps to the start.</summary>
        [Test]
        public void ANegativeQuantityDoesNotWindTheHandsBackwards()
            => Assert.AreEqual(0f, SternDeckLoop.DrivenFramePosition(-3f, 4f, 8), Tol);

        /// <summary>
        /// <b>An empty pot has been emptied.</b> A beat with nothing to work is COMPLETE rather than 0%,
        /// so the hands finish and stand up instead of freezing on frame 0 for ever.
        /// </summary>
        [Test]
        public void ABeatWithNothingToWorkIsComplete()
        {
            Assert.AreEqual(1f, SternDeckLoop.Worked01(0, 0), Tol);
            Assert.AreEqual(0f, SternDeckLoop.Worked01(0, 4), Tol);
            Assert.AreEqual(0.5f, SternDeckLoop.Worked01(2, 4), Tol);
            Assert.AreEqual(1f, SternDeckLoop.Worked01(4, 4), Tol);
            Assert.AreEqual(1f, SternDeckLoop.Worked01(9, 4), Tol, "over-worked clamps rather than overruns");
        }

        // ---- ⚠️ KNOWN OPEN: the seam's two rotations disagree in handedness -------------------------

        /// <summary>
        /// <b>⚠️ FINDING, pinned so it cannot be lost: <see cref="DeckGearPlacement"/> turns POSITIONS one
        /// way and HEADINGS the other.</b>
        ///
        /// <para><see cref="DeckGearPlacement.RotateOnDeck"/> is a standard counter-clockwise rotation of
        /// the (x, y) plane, while <see cref="DeckGearPlacement.OperatorHeading"/> advances a compass
        /// bearing, which is clockwise from North (the project's convention —
        /// <c>DeckWalkController.WorldToDeckFrame</c> states it). So for one and the same <c>dir</c>, an
        /// offset that rotates to PORT is paired with a heading that points to STARBOARD. They are mirror
        /// images about the fore-and-aft axis.</para>
        ///
        /// <para><b>What it costs.</b> A station whose stand is offset ACROSS the gear — the hauler's, the
        /// only one — can be given a <c>dir</c> that stands its operator inboard, or one that faces them
        /// outboard, but never both. This test enumerates all eight and shows the sets are disjoint. Every
        /// other station's stand is straight fore-and-aft, and the beam mirror leaves those alone, which is
        /// why nothing has caught it before.</para>
        ///
        /// <para><b>The fix is one sign, and it belongs to whoever owns Core</b> — not to a presentation
        /// slice, and not while <c>DeckGearPlacementTests</c> pins the current behaviour. Correcting
        /// <see cref="DeckGearPlacement.OperatorHeading"/> to turn with the rotation rather than against it
        /// makes the two agree, and the shipped hauler <c>dir</c> of 2 becomes right in BOTH senses at
        /// once: the operator already stands inboard, and would then also face outboard. Correcting
        /// <c>RotateOnDeck</c> instead would move every stand point, which is the wrong end to pull.</para>
        ///
        /// <para>Until then the shipped data is chosen on POSITION, because where the crew physically
        /// stands is the half that is load-bearing today: the deck-work clips are not baked yet, so no
        /// operator is drawn at any heading at all.</para>
        /// </summary>
        [Test]
        public void KnownOpen_TheStandRotationAndTheHeadingTurnOppositeWays()
        {
            var hauler = new DeckGearPlacement.Station(new Vector3(-0.30f, 0.44f, 0f), 4, 1.05f,
                                                       new Vector2(0.66f, 0.60f));
            Vector3 mount = new Vector3(1.34f, -1.50f, 1.44f);   // a starboard-rail hauler

            var standsInboard = new List<int>();
            var facesOutboard = new List<int>();
            for (int dir = 0; dir < DeckGearPlacement.Facings; dir++)
            {
                if (DeckGearPlacement.OperatorStand(mount, dir, hauler).x < mount.x) standsInboard.Add(dir);

                // Compass: 0 = North = the bow, 90 = East = starboard. Outboard on a starboard rail is +x.
                float h = DeckGearPlacement.OperatorHeading(0f, dir, hauler) * Mathf.Deg2Rad;
                if (Mathf.Sin(h) > 0.5f) facesOutboard.Add(dir);
            }

            CollectionAssert.IsNotEmpty(standsInboard, "some bearing must stand the worker inboard");
            CollectionAssert.IsNotEmpty(facesOutboard, "some bearing must face the worker outboard");
            foreach (int dir in standsInboard)
                CollectionAssert.DoesNotContain(facesOutboard, dir,
                    $"dir {dir}: the seam should allow standing inboard AND facing outboard. That the two " +
                    "sets are disjoint at every bearing IS the handedness defect — when OperatorHeading is " +
                    "corrected to turn with RotateOnDeck, this assertion is what must be deleted.");

            // The concrete case the shipped data uses, spelled out so the fix is checkable by hand.
            Assert.Less(DeckGearPlacement.OperatorStand(mount, 2, hauler).x, mount.x,
                "dir 2 stands the hauler's operator inboard of the rail — correct, and why it is shipped");
            Assert.AreEqual(270f, DeckGearPlacement.OperatorHeading(0f, 2, hauler), Tol,
                "…and today faces them INBOARD (270°). After the sign fix this reads 90° — outboard.");
        }

        // ---- the slot budget -----------------------------------------------------------------------

        /// <summary>
        /// <b>The worst beat of the loop must fit the hull's twelve slots</b> — and it only does because a
        /// STACK is one occupant rather than one per pot.
        ///
        /// <para>Every tier of a stack stands on the same ground point and differs only in height, so they
        /// share a view depth and one band hides all of them. That is also the budget #481 measured the
        /// twelve against: it counted the lobster boat's two trap STACKS as two occupants, not as the
        /// seven pots on them. Counting per pot would ask for 13 on a hull that has 12 — this test is what
        /// catches that if anyone ever re-opens it.</para>
        /// </summary>
        [Test]
        public void TheWorstBeatOfTheLoopFitsTheOccupantSlots()
        {
            // Four pieces of furniture (hauler, bench, chop board, bait bin), the deck stack and the
            // washboard stack, the pot in play, and two hands.
            int worst = SternDeckLoop.OccupantsNeeded(gearPieces: 4, stackedPots: 2,
                                                      potInPlay: true, hands: 2);
            Assert.AreEqual(9, worst, "four gear + two stacks + the pot in play + two hands");
            Assert.LessOrEqual(worst, 12,
                "the worst beat must fit the occupant seam's twelve, with room for the fish tray");

            // The same beat counted PER POT, which is the mistake: a full lobster boat is 5 + 2 pots.
            int perPot = SternDeckLoop.OccupantsNeeded(gearPieces: 4,
                                                       stackedPots: LobsterDeckCap + LobsterWashCap,
                                                       potInPlay: true, hands: 2);
            Assert.Greater(perPot, 12,
                "counting a slot per POT overruns the hull — the stack must claim once");
        }
    }
}
