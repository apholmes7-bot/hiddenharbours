using System.Linq;
using HiddenHarbours.App.Editor;
using NUnit.Framework;

namespace HiddenHarbours.Tests.EditMode
{
    public sealed class StPetersVillageRoomTests
    {
        [Test]
        public void NewUpperRooms_UseTheirBakedSheetsWithoutGameFixtures()
        {
            foreach (string key in new[] { "harbourGable", "creamSaltbox", "whiteGable" })
            {
                Assert.That(StPetersInteriors.VillageUpperPlan(key), Is.EqualTo(key));
                Assert.That(StPetersInteriors.UpperLevelPlanKeys.Count(k => k == key), Is.EqualTo(1));
                var plan = StPetersInteriors.UpperLevelFor(key);
                Assert.That(plan.RoomKey, Is.EqualTo(key + "Upper"));
                Assert.That(plan.Furnishings, Is.Empty);
                Assert.That(plan.GroundAdditions, Is.Empty);
                Assert.That(plan.Partitions, Is.Empty);
            }
        }

        [Test]
        public void HeldHomes_DoNotGainAnUpperPlan()
        {
            foreach (string key in new[] { "redSaltbox", "whiteFarmhouse", "sageCottage", "school" })
                Assert.That(StPetersInteriors.VillageUpperPlan(key), Is.Null);
            Assert.That(StPetersInteriors.HasUpperLevel(StPetersInteriors.GinnyCottagePlanKey), Is.True);
        }
    }
}
