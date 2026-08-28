using System.Collections.Generic;
using NUnit.Framework;
using HiddenHarbours.Economy;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>The wares book as STATE</b> — stubs, the open shelf, the cursor, the page it puts you on, and
    /// what each row says about itself.
    ///
    /// <para>Pure and headless, like <c>NotebookLayoutTests</c>: no canvas, no sprites, no frames. The
    /// presenter draws this and decides nothing, so everything worth getting right is asserted here — a
    /// cursor that wraps, a page that follows it, an empty shelf that shows no stub, and a price you
    /// cannot meet that names the shortfall rather than refusing.</para>
    /// </summary>
    public class CatalogBookTests
    {
        const int Leaf = 4;   // a short leaf, so paging is reachable in a test

        static BuyRow Row(string id, int price, CatalogSection section, int money,
                          bool owned = false, BuyRowKind kind = BuyRowKind.Gear)
        {
            BuyQuote q = kind switch
            {
                BuyRowKind.Boat => BuyLogic.Boat(price, 0, money, owned, false, false),
                BuyRowKind.License => BuyLogic.License(price, money, owned),
                BuyRowKind.Instrument => BuyLogic.Instrument(price, money, owned, true),
                _ => BuyLogic.Gear(price, money, owned),
            };
            return new BuyRow(null, null, id, id, "flavour of " + id, "", q, section);
        }

        static CatalogBook BookOf(params BuyRow[] rows)
        {
            var b = new CatalogBook(Leaf);
            b.SetRows(rows, "MacAulay & Son", 100);
            return b;
        }

        /// <summary>One fresh push: RELEASE first, then push. Releasing afterwards instead would let a
        /// push made while the axis was already held be eaten by the latch — which is correct behaviour
        /// and a wrong fixture.</summary>
        static void Push(CatalogBook b, float axis) { b.Move(0f); b.Move(axis); }

        // ---- the stubs ---------------------------------------------------------------------------

        [Test]
        public void Stubs_AreOnlyTheShelvesThisSellerStocks_InShelfOrder()
        {
            CatalogBook b = BookOf(
                Row("gear.rod", 60, CatalogSection.Gear, 100),
                Row("boat.punt", 1800, CatalogSection.Boats, 100));

            Assert.AreEqual(2, b.Stubs.Count, "a seller with two shelves shows two stubs, not a fixed six");
            Assert.AreEqual(CatalogSection.Boats, b.Stubs[0], "shelf order, not the order rows arrived in");
            Assert.AreEqual(CatalogSection.Gear, b.Stubs[1]);
        }

        [Test]
        public void ASellerWithOneShelf_ShowsOneStub()
        {
            CatalogBook b = BookOf(Row("gear.rod", 60, CatalogSection.Gear, 100));

            Assert.AreEqual(1, b.Stubs.Count);
            Assert.AreEqual(CatalogSection.Gear, b.Section);
        }

        [Test]
        public void AnEmptyBook_HasNoStubs_NoRows_AndNoCursor()
        {
            var b = new CatalogBook(Leaf);
            b.SetRows(new List<BuyRow>(), "Nobody", 0);

            Assert.IsEmpty(b.Stubs);
            Assert.IsFalse(b.HasRows);
            Assert.AreEqual(-1, b.Index, "no row to be on");
            Assert.AreEqual(1, b.PageCount, "still one (blank) page, never zero");
        }

        [Test]
        public void OpeningOnAShelfSheDoesNotStock_FallsBackToHerFirstStub()
        {
            var b = new CatalogBook(Leaf);
            b.SetRows(new[] { Row("gear.rod", 60, CatalogSection.Gear, 100) },
                      "MacAulay & Son", 100, CatalogSection.Boats);

            Assert.AreEqual(CatalogSection.Gear, b.Section,
                            "a dialogue row pointing at an empty shelf opens the first real one");
        }

        [Test]
        public void SteppingStubs_Wraps()
        {
            CatalogBook b = BookOf(
                Row("gear.rod", 60, CatalogSection.Gear, 100),
                Row("boat.punt", 1800, CatalogSection.Boats, 100));
            Assert.AreEqual(CatalogSection.Boats, b.Section);

            Assert.IsTrue(b.StepSection(1));
            Assert.AreEqual(CatalogSection.Gear, b.Section);
            Assert.IsTrue(b.StepSection(1));
            Assert.AreEqual(CatalogSection.Boats, b.Section, "off the end and back onto the first");
        }

        [Test]
        public void OpeningAShelf_PutsTheCursorOnItsFirstRow()
        {
            CatalogBook b = BookOf(
                Row("gear.rod", 60, CatalogSection.Gear, 100),
                Row("gear.gaff", 25, CatalogSection.Gear, 100),
                Row("boat.punt", 1800, CatalogSection.Boats, 100));

            b.OpenSection(CatalogSection.Gear);
            Assert.AreEqual(0, b.Index);
            Assert.AreEqual("gear.rod", b.Current.Id);
            Assert.AreEqual(2, b.Shelf.Count, "only that shelf's rows");
        }

        // ---- the cursor --------------------------------------------------------------------------

        [Test]
        public void TheCursor_StepsOncePerPush_AndWrapsBothWays()
        {
            CatalogBook b = BookOf(
                Row("gear.a", 10, CatalogSection.Gear, 100),
                Row("gear.b", 10, CatalogSection.Gear, 100),
                Row("gear.c", 10, CatalogSection.Gear, 100));

            Assert.IsTrue(b.Move(-1f), "first frame of the push steps");
            Assert.AreEqual(1, b.Index);
            Assert.IsFalse(b.Move(-1f), "a HELD key does not rip through the list");
            Assert.AreEqual(1, b.Index);

            b.Move(0f);                       // released
            Assert.IsTrue(b.Move(-1f));
            Assert.AreEqual(2, b.Index);

            Push(b, -1f);
            Assert.AreEqual(0, b.Index, "off the bottom onto the top");

            Push(b, 1f);
            Assert.AreEqual(2, b.Index, "and off the top onto the bottom");
        }

        [Test]
        public void ASingleRow_DoesNotMove_ButStillAbsorbsThePush()
        {
            CatalogBook b = BookOf(Row("gear.rod", 60, CatalogSection.Gear, 100));

            Assert.IsFalse(b.Move(-1f));
            Assert.AreEqual(0, b.Index, "there is nowhere to go");
        }

        // ---- paging ------------------------------------------------------------------------------

        [Test]
        public void ThePage_FollowsTheCursor_RatherThanBeingTurnedSeparately()
        {
            var rows = new List<BuyRow>();
            for (int i = 0; i < Leaf + 2; i++) rows.Add(Row($"gear.{i}", 10, CatalogSection.Gear, 100));
            var b = new CatalogBook(Leaf);
            b.SetRows(rows, "MacAulay & Son", 100);

            Assert.AreEqual(2, b.PageCount);
            Assert.AreEqual(0, b.Page);

            for (int i = 0; i < Leaf; i++) Push(b, -1f);       // step onto the first row of page 2
            Assert.AreEqual(Leaf, b.Index);
            Assert.AreEqual(1, b.Page, "the leaf turned because the cursor left it");
            Assert.AreEqual(0, b.IndexOnPage, "and the cursor is at the top of the new leaf");
        }

        [Test]
        public void VisibleRows_AreTheCursorsPage_Only()
        {
            var rows = new List<BuyRow>();
            for (int i = 0; i < Leaf + 2; i++) rows.Add(Row($"gear.{i}", 10, CatalogSection.Gear, 100));
            var b = new CatalogBook(Leaf);
            b.SetRows(rows, "MacAulay & Son", 100);

            var page = new List<BuyRow>();
            b.VisibleRows(page);
            Assert.AreEqual(Leaf, page.Count);
            Assert.AreEqual("gear.0", page[0].Id);

            for (int i = 0; i < Leaf; i++) Push(b, -1f);
            b.VisibleRows(page);
            Assert.AreEqual(2, page.Count, "the last leaf holds what is left, not a padded four");
            Assert.AreEqual($"gear.{Leaf}", page[0].Id);
        }

        // ---- rebuilding --------------------------------------------------------------------------

        [Test]
        public void ARebuild_KeepsTheOpenShelfAndTheCursor()
        {
            // A purchase must not throw you back to the first tab and the top of the list — the page
            // must not move under the hand that just made it.
            var b = new CatalogBook(Leaf);
            BuyRow[] before =
            {
                Row("gear.rod", 60, CatalogSection.Gear, 100),
                Row("gear.gaff", 25, CatalogSection.Gear, 100),
                Row("boat.punt", 1800, CatalogSection.Boats, 100),
            };
            b.SetRows(before, "MacAulay & Son", 100);
            b.OpenSection(CatalogSection.Gear);
            Push(b, -1f);
            Assert.AreEqual("gear.gaff", b.Current.Id);

            // ...the gaff is bought, so the same row comes back OWNED.
            BuyRow[] after =
            {
                Row("gear.rod", 60, CatalogSection.Gear, 75),
                Row("gear.gaff", 25, CatalogSection.Gear, 75, owned: true),
                Row("boat.punt", 1800, CatalogSection.Boats, 75),
            };
            b.SetRows(after, "MacAulay & Son", 75);

            Assert.AreEqual(CatalogSection.Gear, b.Section, "still her gear shelf");
            Assert.AreEqual("gear.gaff", b.Current.Id, "still on the row you just bought");
            Assert.AreEqual(75, b.Purse, "and the head shows the new balance");
        }

        [Test]
        public void ARebuild_ThatLosesTheRow_LandsSomewhereReal()
        {
            var b = new CatalogBook(Leaf);
            b.SetRows(new[] { Row("gear.rod", 60, CatalogSection.Gear, 100),
                              Row("gear.gaff", 25, CatalogSection.Gear, 100) }, "S", 100);
            Push(b, -1f);
            Assert.AreEqual("gear.gaff", b.Current.Id);

            b.SetRows(new[] { Row("gear.rod", 60, CatalogSection.Gear, 100) }, "S", 100);

            Assert.IsTrue(b.HasRows);
            Assert.AreEqual(0, b.Index, "the cursor lands on a real row, never off the end");
            Assert.AreEqual("gear.rod", b.Current.Id);
        }

        // ---- what a row says about itself --------------------------------------------------------

        [Test]
        public void TheThreeStates_AreOwned_TooDear_AndYours()
        {
            BuyRow owned = Row("gear.rod", 60, CatalogSection.Gear, 1000, owned: true);
            BuyRow dear = Row("gear.sounder", 640, CatalogSection.Gear, 100);
            BuyRow yours = Row("gear.gaff", 25, CatalogSection.Gear, 100);

            Assert.AreEqual(CatalogRowState.Owned, CatalogBook.StateOf(owned));
            Assert.AreEqual(CatalogRowState.TooDear, CatalogBook.StateOf(dear));
            Assert.AreEqual(CatalogRowState.Affordable, CatalogBook.StateOf(yours));
        }

        [Test]
        public void TooDear_NamesTheShortfall_BecauseThatIsANumberYouCanPlanAgainst()
        {
            CatalogBook b = BookOf(Row("gear.sounder", 640, CatalogSection.Gear, 100));

            // P5: a price you cannot meet is a plain, kind sentence, not a locked door.
            Assert.AreEqual("You're ₲ 540 short.", b.StatusFor(b.Current));
        }

        [Test]
        public void Owned_SaysSo_AndAffordable_SaysTheSellersVerb()
        {
            CatalogBook owned = BookOf(Row("gear.rod", 60, CatalogSection.Gear, 100, owned: true));
            Assert.AreEqual("Already yours.", owned.StatusFor(owned.Current));

            CatalogBook yours = BookOf(Row("gear.gaff", 25, CatalogSection.Gear, 100));
            Assert.AreEqual("Take it", yours.StatusFor(yours.Current));
        }

        [Test]
        public void TheVerbs_AreThePerKindOnes()
        {
            // Owner ruling R6: one switch, and most of the voice.
            Assert.AreEqual("Buy her", CatalogBook.VerbFor(
                Row("boat.punt", 10, CatalogSection.Boats, 100, kind: BuyRowKind.Boat)));
            Assert.AreEqual("Sign for it", CatalogBook.VerbFor(
                Row("license.cod", 10, CatalogSection.Gear, 100, kind: BuyRowKind.License)));
            Assert.AreEqual("Fit it", CatalogBook.VerbFor(
                Row("instrument.sounder", 10, CatalogSection.Gear, 100, kind: BuyRowKind.Instrument)));
            Assert.AreEqual("Take it", CatalogBook.VerbFor(
                Row("gear.rod", 10, CatalogSection.Gear, 100)));
        }

        [Test]
        public void ARepairRow_SaysPutHerRight()
        {
            // Owned-but-damaged: BuyLogic turns the row into the repair, and the verb follows it.
            BuyQuote q = BuyLogic.Boat(400, 300, 1000, owned: true, startsDamaged: true, repaired: false);
            var row = new BuyRow(null, null, "boat.dory", "Dory", "", "", q, CatalogSection.Boats);

            Assert.AreEqual(BuyRowKind.BoatRepair, row.Quote.Kind);
            Assert.AreEqual("Put her right", CatalogBook.VerbFor(row));
        }

        [Test]
        public void Prices_AreWrittenTheOneWay()
        {
            Assert.AreEqual("₲ 60", CatalogBook.Price(60));
            Assert.AreEqual("₲ 1,800", CatalogBook.Price(1800), "grouped, so a four-figure hull reads");
            Assert.AreEqual("₲ 0", CatalogBook.Price(0));
        }
    }
}
