using NUnit.Framework;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The one storage-acceptance rule (<see cref="StowageRule"/>), headless: a rod goes only where a
    /// container is flagged for rods, a rod rack takes nothing else, capacity is a hard stop, and every
    /// container that predates the rule answers non-rod items exactly as it did before.
    /// </summary>
    public class StowageRuleTests
    {
        private const StowageAccepts RodRack = StowageAccepts.Rods;
        private const StowageAccepts GearLocker = StowageAccepts.General | StowageAccepts.Rods;

        // ---- accept -------------------------------------------------------------------------------------

        [Test]
        public void A_rod_goes_in_a_rod_rack_with_room()
        {
            Assert.AreEqual(StowRefusal.None, StowageRule.Check(StowageClass.Rod, RodRack, used: 0, capacity: 4));
            Assert.AreEqual(StowRefusal.None, StowageRule.Check(StowageClass.Rod, RodRack, used: 3, capacity: 4));
        }

        [Test]
        public void A_locker_flagged_for_rods_and_general_takes_both()
        {
            Assert.AreEqual(StowRefusal.None, StowageRule.Check(StowageClass.Rod, GearLocker, 0, 2));
            Assert.AreEqual(StowRefusal.None, StowageRule.Check(StowageClass.General, GearLocker, 0, 2));
        }

        // ---- refuse -------------------------------------------------------------------------------------

        [Test]
        public void A_rod_is_refused_by_every_existing_container()
        {
            // The fish hold, the cooler, the pail, the freezer: all General only.
            Assert.AreEqual(StowRefusal.WrongContainer,
                StowageRule.Check(StowageClass.Rod, StowageRule.ExistingContainer, used: 0, capacity: 100));
        }

        [Test]
        public void A_rod_rack_refuses_anything_that_is_not_a_rod()
        {
            Assert.AreEqual(StowRefusal.WrongContainer, StowageRule.Check(StowageClass.General, RodRack, 0, 4));
        }

        [Test]
        public void A_container_with_no_flags_refuses_everything()
        {
            Assert.AreEqual(StowRefusal.WrongContainer, StowageRule.Check(StowageClass.Rod, StowageAccepts.None, 0, 4));
            Assert.AreEqual(StowRefusal.WrongContainer, StowageRule.Check(StowageClass.General, StowageAccepts.None, 0, 4));
        }

        [Test]
        public void Wrong_container_is_said_before_full()
        {
            // A full fish hold is not the reason a rod does not fit in it.
            Assert.AreEqual(StowRefusal.WrongContainer,
                StowageRule.Check(StowageClass.Rod, StowageRule.ExistingContainer, used: 20, capacity: 20));
        }

        // ---- capacity -----------------------------------------------------------------------------------

        [Test]
        public void A_full_rod_rack_refuses_another_rod()
        {
            Assert.AreEqual(StowRefusal.Full, StowageRule.Check(StowageClass.Rod, RodRack, used: 4, capacity: 4));
            Assert.AreEqual(StowRefusal.Full, StowageRule.Check(StowageClass.Rod, RodRack, used: 5, capacity: 4));
        }

        [Test]
        public void Zero_capacity_holds_nothing_rather_than_everything()
        {
            Assert.AreEqual(StowRefusal.Full, StowageRule.Check(StowageClass.Rod, RodRack, 0, 0));
            Assert.AreEqual(StowRefusal.Full, StowageRule.Check(StowageClass.Rod, RodRack, 0, -1));
        }

        // ---- existing containers unchanged for non-rod items ---------------------------------------------

        [Test]
        public void Existing_containers_answer_general_items_exactly_as_the_old_full_check_did()
        {
            // The rule replaced "UsedUnits >= CapacityUnits means no" in CarryHands.TryGiveCatchTo. Swept
            // so a change to the rule that moved that line by one shows here.
            for (int capacity = 0; capacity <= 6; capacity++)
                for (int used = 0; used <= 7; used++)
                {
                    bool oldRuleTakes = !(used >= capacity);
                    StowRefusal got = StowageRule.Check(StowageClass.General, StowageRule.ExistingContainer, used, capacity);
                    Assert.AreEqual(oldRuleTakes, got == StowRefusal.None, $"used {used} of {capacity}");
                    if (!oldRuleTakes) Assert.AreEqual(StowRefusal.Full, got, $"used {used} of {capacity}");
                }
        }

        // ---- serialized values are pinned -----------------------------------------------------------------

        [Test]
        public void Serialized_enum_values_are_append_only()
        {
            // Def assets store these as ints (Rod.asset: Stowage 1; RodRack.asset: Accepts 2).
            Assert.AreEqual(0, (int)StowageClass.General);
            Assert.AreEqual(1, (int)StowageClass.Rod);
            Assert.AreEqual(0, (int)StowageAccepts.None);
            Assert.AreEqual(1, (int)StowageAccepts.General);
            Assert.AreEqual(2, (int)StowageAccepts.Rods);
            Assert.AreEqual(StowageAccepts.General, StowageRule.ExistingContainer);
        }

        [Test]
        public void Each_class_maps_to_its_own_flag()
        {
            Assert.AreEqual(StowageAccepts.Rods, StowageRule.FlagFor(StowageClass.Rod));
            Assert.AreEqual(StowageAccepts.General, StowageRule.FlagFor(StowageClass.General));
        }
    }
}
