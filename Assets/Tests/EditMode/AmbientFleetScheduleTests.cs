using NUnit.Framework;
using HiddenHarbours.Boats;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The ambient fisher's place → soak → haul SCHEDULE (canon M2-33): a pure function of the game
    /// clock (rule 5 — recomputed, never saved), so joining a session at any moment shows the same
    /// fleet state the clock implies. Exercises the slot round-robin, the work window, and the
    /// buoy-parity state machine, headless.
    /// </summary>
    public class AmbientFleetScheduleTests
    {
        private const int K = 2;                    // spots per boat
        private const float WorkStart = 0.55f;
        private const float WorkEnd = 0.8f;
        private const float Flip = 0.675f;          // the work window's midpoint

        [Test]
        public void WorkFlip_IsTheWindowMidpoint()
        {
            Assert.AreEqual(Flip, AmbientFleetSchedule.WorkFlipFraction(WorkStart, WorkEnd), 1e-5f);
        }

        [Test]
        public void SlotPosition_AdvancesWithTheDay_AndCarriesThePhase()
        {
            Assert.AreEqual(0.0, AmbientFleetSchedule.SlotPosition(0f, 12, 0f), 1e-9);
            Assert.AreEqual(6.0, AmbientFleetSchedule.SlotPosition(0.5f, 12, 0f), 1e-6);
            Assert.AreEqual(8.5, AmbientFleetSchedule.SlotPosition(0.5f, 12, 2.5f), 1e-6);
        }

        [Test]
        public void Spots_AreWorkedRoundRobin()
        {
            Assert.AreEqual(0, AmbientFleetSchedule.SpotForSlot(0, K));
            Assert.AreEqual(1, AmbientFleetSchedule.SpotForSlot(1, K));
            Assert.AreEqual(0, AmbientFleetSchedule.SpotForSlot(2, K));
            Assert.AreEqual(1, AmbientFleetSchedule.SpotForSlot(5, K));
            Assert.AreEqual(1, AmbientFleetSchedule.SpotForSlot(-1, K), "negative slots must stay in range");
        }

        [Test]
        public void TargetSpot_HoldsTheCurrentSpot_ThenBearsAwayForTheNext()
        {
            Assert.AreEqual(0, AmbientFleetSchedule.TargetSpot(0.50, K, WorkEnd), "in transit/working slot 0 → spot 0");
            Assert.AreEqual(0, AmbientFleetSchedule.TargetSpot(0.80, K, WorkEnd), "work window closes AT the fraction");
            Assert.AreEqual(1, AmbientFleetSchedule.TargetSpot(0.85, K, WorkEnd), "work done → head for the next spot");
            Assert.AreEqual(0, AmbientFleetSchedule.TargetSpot(1.90, K, WorkEnd), "slot 1 done → wrap to spot 0");
        }

        [Test]
        public void IsWorking_OnlyInsideTheWindow()
        {
            Assert.IsFalse(AmbientFleetSchedule.IsWorking(0.50, WorkStart, WorkEnd));
            Assert.IsTrue(AmbientFleetSchedule.IsWorking(0.60, WorkStart, WorkEnd));
            Assert.IsTrue(AmbientFleetSchedule.IsWorking(3.70, WorkStart, WorkEnd), "any slot, same window");
            Assert.IsFalse(AmbientFleetSchedule.IsWorking(0.85, WorkStart, WorkEnd));
        }

        [Test]
        public void Buoy_DoesNotExist_BeforeTheFirstWorkOfTheDay()
        {
            Assert.IsFalse(AmbientFleetSchedule.BuoyPresent(0.50, 0, K, Flip), "spot 0: not yet worked");
            Assert.IsFalse(AmbientFleetSchedule.BuoyPresent(1.60, 1, K, Flip), "spot 1: worked in slot 1, flip not reached");
        }

        [Test]
        public void Buoy_Appears_AtThePlaceFlip_AndSoaks_WhileTheBoatWorksElsewhere()
        {
            Assert.IsTrue(AmbientFleetSchedule.BuoyPresent(0.70, 0, K, Flip), "spot 0 placed at s=0.675");
            Assert.IsTrue(AmbientFleetSchedule.BuoyPresent(1.50, 0, K, Flip), "still soaking while she works spot 1");
            Assert.IsTrue(AmbientFleetSchedule.BuoyPresent(2.60, 0, K, Flip), "soaks right up to the haul flip");
            Assert.IsTrue(AmbientFleetSchedule.BuoyPresent(1.70, 1, K, Flip), "spot 1 placed on its own beat");
        }

        [Test]
        public void Buoy_Vanishes_AtTheHaulFlip_ThenIsPlacedAgainNextVisit()
        {
            Assert.IsFalse(AmbientFleetSchedule.BuoyPresent(2.70, 0, K, Flip), "hauled on the second visit (odd parity)");
            Assert.IsFalse(AmbientFleetSchedule.BuoyPresent(3.90, 0, K, Flip), "stays aboard until the next place visit");
            Assert.IsTrue(AmbientFleetSchedule.BuoyPresent(4.70, 0, K, Flip), "third visit places again (even parity)");
        }

        [Test]
        public void OverAFullDay_TheBuoyAlternates_PlaceHaulPlaceHaul()
        {
            // 12 slots, K=2 → spot 0 is visited in slots 0,2,4,6,8,10 → exactly 6 state flips,
            // starting with a PLACE (false → true) and alternating strictly.
            const int slotsPerDay = 12;
            bool last = AmbientFleetSchedule.BuoyPresent(0.0, 0, K, Flip);
            Assert.IsFalse(last, "a day starts with no buoys out");
            int flips = 0;
            bool firstFlipWasPlace = false;
            for (double s = 0.0; s <= slotsPerDay; s += 0.005)
            {
                bool now = AmbientFleetSchedule.BuoyPresent(s, 0, K, Flip);
                if (now != last)
                {
                    flips++;
                    if (flips == 1) firstFlipWasPlace = now;
                    last = now;
                }
            }
            Assert.AreEqual(6, flips, "six visits to spot 0 in a 12-slot day ⇒ six flips");
            Assert.IsTrue(firstFlipWasPlace, "the first work of the day at a spot is a PLACE");
        }

        [Test]
        public void Schedule_IsAPureFunctionOfTheClock()
        {
            // The determinism anchor: any instant re-queried gives the identical answer — there is no
            // hidden state to drift (rule 5: recompute, don't save).
            for (double s = 0.0; s < 24.0; s += 0.37)
            {
                Assert.AreEqual(AmbientFleetSchedule.BuoyPresent(s, 0, K, Flip),
                                AmbientFleetSchedule.BuoyPresent(s, 0, K, Flip));
                Assert.AreEqual(AmbientFleetSchedule.TargetSpot(s, K, WorkEnd),
                                AmbientFleetSchedule.TargetSpot(s, K, WorkEnd));
            }
        }
    }
}
