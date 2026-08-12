using HiddenHarbours.App.Editor;
using HiddenHarbours.Art.Editor;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The general store's counter, where the baked fixture meets the five vendors standing on it.
    ///
    /// <para>#356 built the store's economy and #495 put a real shop behind it; the counter itself
    /// stayed a tinted rectangle scaled 1.6 × 0.9. These are the checks that the swap to the baked
    /// fixture kept the two things that were already right — the stand-point and the reach — and got
    /// the one new thing right: which way it faces.</para>
    /// </summary>
    public class StPetersStoreCounterTests
    {
        static ShopFixtureCatalog.Placement Counter => ShopFixtureCatalog.Find("counter", "generalStore");

        static ShopFixtureCatalog.Placement Baked()
        {
            var c = Counter;
            if (!c.IsValid)
                Assert.Ignore("no baked counter in the shop-fixture contract — run " +
                              "Hidden Harbours ▸ Art ▸ Bake Shop Fixtures");
            return c;
        }

        /// <summary>
        /// The counter must turn its SERVICE face toward the village green — the side the player walks
        /// up from, with the storekeeper behind it in her own dooryard.
        ///
        /// <para>Asserted as a bounded ERROR read off the bake's own anchors, never by re-deriving the
        /// choice and finding it agrees with itself. That is not a stylistic preference: the arithmetic
        /// version of this pointed a schoolhouse 92° away from the green it faces, and its test could
        /// not see it because the test restated the implementation's own formula.</para>
        /// </summary>
        [Test]
        public void The_counter_faces_the_green_the_customer_walks_up_from()
        {
            var counter = Baked();

            Vector2 at = StPetersBuilder.GeneralStoreCounterPos;
            Vector2 green = StPetersBuilder.VillageGreen;

            int facing = ShopFixtureCatalog.FacingToward(counter, at, green);
            float error = ShopFixtureCatalog.FacingErrorDegrees(counter, at, green, facing);
            int facings = counter.Entry.facings;
            float halfCell = 180f / facings;

            Assert.That(facing, Is.InRange(0, facings - 1));
            Assert.That(error, Is.LessThanOrEqualTo(halfCell + 0.001f),
                $"facing {facing} points the counter's service face {error:0.0}° from the green. With " +
                $"{facings} facings the worst a NEAREST choice can be is half a cell ({halfCell:0.0}°); " +
                "anything worse means the chosen facing is not the nearest one — a sign error rather " +
                "than a resolution limit.");

            // THE POSITIVE CONTROL: some other facing must score worse, or the bound above is vacuous
            // and would pass for a FacingToward that returned a constant.
            float worst = 0f;
            for (int f = 0; f < facings; f++)
                worst = Mathf.Max(worst, ShopFixtureCatalog.FacingErrorDegrees(counter, at, green, f));
            Assert.That(worst, Is.GreaterThan(error + 1f),
                "every facing scores the same error, so FacingToward is not choosing anything");
        }

        /// <summary>
        /// The swap must not move the counter. Its position is <c>GeneralStoreCounterPos</c>, derived
        /// from the store's own site, and the five vendors, the market, the buyer and the sell point are
        /// components on that same GameObject — so its transform IS every one of their stand-points.
        /// </summary>
        [Test]
        public void The_counters_stand_point_is_still_derived_from_the_stores_own_site()
        {
            Vector2 door = StPetersInhabitants.Dooryard(StPetersBuilder.GeneralStorePos);
            Vector2 at = StPetersBuilder.GeneralStoreCounterPos;

            Assert.That(Vector2.Distance(at, door),
                        Is.EqualTo(StPetersBuilder.StoreCounterStrideMetres).Within(1e-3f),
                "the counter stands one stride out the storekeeper's own doorstep. Nothing in the art " +
                "swap may offset it — the fixture's pivot is its GROUND CENTRE, which is exactly why " +
                "no offset is needed.");

            Vector2 green = StPetersBuilder.VillageGreen;
            Assert.That(Vector2.Distance(at, green), Is.LessThan(Vector2.Distance(door, green)),
                "and it stands on the GREEN side of the door, so you meet the counter and then her " +
                "behind it");
        }

        /// <summary>
        /// The real counter is 2.8 m of frontage against the placeholder's 1.6, so it is worth pinning
        /// that it still reads as ONE thing to walk up to beside its keeper rather than as a wall.
        /// </summary>
        [Test]
        public void The_baked_counter_still_reads_as_one_thing_to_walk_up_to()
        {
            var counter = Baked();

            float radius = ShopFixtureCatalog.FootprintRadiusMetres(counter);
            Assert.That(radius, Is.GreaterThan(0f));
            Assert.That(radius, Is.LessThan(StPetersBuilder.StoreCounterStrideMetres * 2f),
                $"the baked counter's half-diagonal is {radius:0.00} m and its stand-point sits " +
                $"{StPetersBuilder.StoreCounterStrideMetres:0.00} m out the store's door. A fixture much " +
                "bigger than that reaches back through the wall it stands in front of.");

            Assert.That(counter.FootprintMetres.x, Is.GreaterThan(counter.FootprintMetres.y),
                "a shop counter is wider across its service face than it is deep — if that has " +
                "flipped, the footprint is being read rotated and the facing maths is reading a " +
                "different face than it thinks");
        }

        /// <summary>
        /// The fixture is baked at the same trade AND the same size as the shell it belongs to. Two
        /// sizes would be two different weather-speckle streams dressing one shop.
        /// </summary>
        [Test]
        public void The_counter_is_dressed_as_the_shop_it_stands_outside()
        {
            var counter = Baked();

            Assert.That(counter.Entry.trade, Is.EqualTo("generalStore"));

            var shell = ShopCatalog.FindShell("generalStore");
            if (!shell.IsValid) Assert.Ignore("the general store's shell is not baked");

            Assert.That(counter.Entry.size, Is.EqualTo(shell.Entry.size).Within(1e-6f),
                $"the counter bakes at size {counter.Entry.size} and its shop at {shell.Entry.size}");
        }
    }
}
