using HiddenHarbours.Core;
using HiddenHarbours.World;
using NUnit.Framework;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The gift rule (gifting slice 1): (tastes, item, gifts today) → reaction, the line said, the
    /// relationship kind recorded, and the day ledger. Pure, so every case is a value in and a value out.
    /// </summary>
    public class GiftRuleTests
    {
        private const string Cod = "fish.atlantic_cod";
        private const string GreenCrab = "fish.green_crab";
        private const string Clam = "fish.soft_shell_clam";
        private const string Mackerel = "fish.mackerel";

        private static GiftItem Item(string id, FishCategory category, bool giftable = true)
            => new GiftItem(id, "Thing", new[] { GiftItem.CatchTag, GiftItem.CategoryTag(category) }, giftable);

        private static NpcGiftPreferences Prefs() => new NpcGiftPreferences
        {
            LovedItemIds = new[] { Cod },
            DislikedItemIds = new[] { GreenCrab },
            LikedTags = new[] { "shellfish" },
            DislikedTags = new[] { "pelagic" },
        };

        // ---- tastes -------------------------------------------------------------------------

        [Test]
        public void NoPreferences_TakesAnyGiftPolitely()
        {
            Assert.AreEqual(GiftReaction.Neutral, GiftRule.Evaluate(null, Item(Cod, FishCategory.InshoreGroundfish), 0, 1));
            Assert.AreEqual(GiftReaction.Neutral,
                GiftRule.Evaluate(new NpcGiftPreferences(), Item(Cod, FishCategory.InshoreGroundfish), 0, 1));
        }

        [Test]
        public void LovedId_IsLoved_AndUnlistedIsNeutral()
        {
            Assert.AreEqual(GiftReaction.Loved, GiftRule.Evaluate(Prefs(), Item(Cod, FishCategory.InshoreGroundfish), 0, 1));
            Assert.AreEqual(GiftReaction.Neutral,
                GiftRule.Evaluate(Prefs(), Item("fish.haddock", FishCategory.InshoreGroundfish), 0, 1));
        }

        [Test]
        public void Tags_MatchByCategory()
        {
            Assert.AreEqual(GiftReaction.Liked, GiftRule.Evaluate(Prefs(), Item(Clam, FishCategory.Shellfish), 0, 1));
            Assert.AreEqual(GiftReaction.Disliked, GiftRule.Evaluate(Prefs(), Item(Mackerel, FishCategory.Pelagic), 0, 1));
        }

        [Test]
        public void AnId_BeatsATag_BothWays()
        {
            // Green crab is shellfish (a liked tag) but its id is disliked: the id wins.
            Assert.AreEqual(GiftReaction.Disliked, GiftRule.Evaluate(Prefs(), Item(GreenCrab, FishCategory.Shellfish), 0, 1));

            // A loved id beats a disliked tag.
            var prefs = Prefs();
            prefs.LovedItemIds = new[] { Mackerel };
            Assert.AreEqual(GiftReaction.Loved, GiftRule.Evaluate(prefs, Item(Mackerel, FishCategory.Pelagic), 0, 1));
        }

        [Test]
        public void TwoTagsInTwoLists_TheWarmerWins()
        {
            var prefs = new NpcGiftPreferences { LovedTags = new[] { "catch" }, DislikedTags = new[] { "shellfish" } };
            Assert.AreEqual(GiftReaction.Loved, GiftRule.Preference(prefs, Item(Clam, FishCategory.Shellfish)));
        }

        // ---- refusals and the daily limit ---------------------------------------------------

        [Test]
        public void NotGiftable_IsRefused_EvenWhenLoved_AndBeforeTheDailyLimit()
        {
            GiftItem cod = Item(Cod, FishCategory.InshoreGroundfish, giftable: false);
            Assert.AreEqual(GiftReaction.NotGiftable, GiftRule.Evaluate(Prefs(), cod, 0, 1));
            Assert.AreEqual(GiftReaction.NotGiftable, GiftRule.Evaluate(Prefs(), cod, 5, 1));
            Assert.AreEqual(GiftReaction.NotGiftable,
                GiftRule.Evaluate(Prefs(), new GiftItem("", "x", null, true), 0, 1), "no id: nothing to give");
        }

        [Test]
        public void DailyLimit_DeclinesTheSecondGift()
        {
            GiftItem cod = Item(Cod, FishCategory.InshoreGroundfish);
            Assert.AreEqual(GiftReaction.Loved, GiftRule.Evaluate(Prefs(), cod, 0, 1));
            Assert.AreEqual(GiftReaction.AlreadyGiftedToday, GiftRule.Evaluate(Prefs(), cod, 1, 1));
            Assert.AreEqual(GiftReaction.Loved, GiftRule.Evaluate(Prefs(), cod, 1, 2), "the limit is data");
            Assert.AreEqual(GiftReaction.Loved, GiftRule.Evaluate(Prefs(), cod, 9, 0), "zero means no limit");
        }

        [Test]
        public void OnlyTheFourTastes_AreAccepted_AndEachRecordsItsOwnKind()
        {
            Assert.IsTrue(GiftRule.IsAccepted(GiftReaction.Loved));
            Assert.IsTrue(GiftRule.IsAccepted(GiftReaction.Liked));
            Assert.IsTrue(GiftRule.IsAccepted(GiftReaction.Neutral));
            Assert.IsTrue(GiftRule.IsAccepted(GiftReaction.Disliked));
            Assert.IsFalse(GiftRule.IsAccepted(GiftReaction.NotGiftable));
            Assert.IsFalse(GiftRule.IsAccepted(GiftReaction.AlreadyGiftedToday));

            Assert.IsTrue(GiftRule.TryRelationshipKind(GiftReaction.Loved, out RelationshipEventKind k));
            Assert.AreEqual(RelationshipEventKind.GiftLoved, k);
            Assert.IsTrue(GiftRule.TryRelationshipKind(GiftReaction.Liked, out k));
            Assert.AreEqual(RelationshipEventKind.GiftLiked, k);
            Assert.IsTrue(GiftRule.TryRelationshipKind(GiftReaction.Neutral, out k));
            Assert.AreEqual(RelationshipEventKind.GiftNeutral, k);
            Assert.IsTrue(GiftRule.TryRelationshipKind(GiftReaction.Disliked, out k));
            Assert.AreEqual(RelationshipEventKind.GiftDisliked, k);
            Assert.IsFalse(GiftRule.TryRelationshipKind(GiftReaction.NotGiftable, out _));
            Assert.IsFalse(GiftRule.TryRelationshipKind(GiftReaction.AlreadyGiftedToday, out _));
        }

        [Test]
        public void SameInputs_SameAnswer()
        {
            GiftItem clam = Item(Clam, FishCategory.Shellfish);
            GiftReaction first = GiftRule.Evaluate(Prefs(), clam, 0, 1);
            for (int i = 0; i < 10; i++) Assert.AreEqual(first, GiftRule.Evaluate(Prefs(), clam, 0, 1));
        }

        // ---- lines --------------------------------------------------------------------------

        [Test]
        public void OwnLines_BeatSharedLines_AndAnEmptyOwnListFallsBack()
        {
            var shared = new GiftLineSet { Loved = new[] { "shared loved" }, Neutral = new[] { "shared neutral" } };
            var own = new GiftLineSet { Loved = new[] { "own loved" } };
            Assert.AreEqual("own loved", GiftRule.Line(GiftReaction.Loved, own, shared, 0, "Cod"));
            Assert.AreEqual("shared neutral", GiftRule.Line(GiftReaction.Neutral, own, shared, 0, "Cod"));
            Assert.AreEqual("shared neutral", GiftRule.Line(GiftReaction.Neutral, null, shared, 0, "Cod"));
            Assert.IsNull(GiftRule.Line(GiftReaction.Disliked, own, shared, 0, "Cod"), "no line anywhere: say nothing");
            Assert.IsNull(GiftRule.Line(GiftReaction.Disliked, null, null, 0, "Cod"));
        }

        [Test]
        public void ItemToken_IsFilled()
        {
            var shared = new GiftLineSet { Liked = new[] { "A " + GiftRule.ItemToken + "! Thanks." } };
            Assert.AreEqual("A Haddock! Thanks.", GiftRule.Line(GiftReaction.Liked, null, shared, 0, "Haddock"));
            Assert.AreEqual("A " + GiftRule.FallbackItemName + "! Thanks.",
                GiftRule.Line(GiftReaction.Liked, null, shared, 0, null));
        }

        [Test]
        public void PickLine_FollowsTheDay_Wraps_AndSkipsBlanks()
        {
            string[] lines = { "a", "b", "c" };
            Assert.AreEqual("a", GiftRule.PickLine(lines, 0));
            Assert.AreEqual("b", GiftRule.PickLine(lines, 1));
            Assert.AreEqual("a", GiftRule.PickLine(lines, 3));
            Assert.AreEqual("c", GiftRule.PickLine(lines, -1), "a negative day wraps, never throws");
            Assert.AreEqual("c", GiftRule.PickLine(new[] { "", null, "c" }, 0));
            Assert.IsNull(GiftRule.PickLine(new[] { "", null }, 0));
            Assert.IsNull(GiftRule.PickLine(null, 0));
        }

        // ---- the item -----------------------------------------------------------------------

        private sealed class Facts : IGiftableFacts
        {
            private readonly string _id;
            private readonly bool _giftable;
            public Facts(string id, bool giftable) { _id = id; _giftable = giftable; }

            public bool TryIsGiftable(string itemId, out bool giftable)
            {
                bool known = itemId == _id;
                giftable = known && _giftable;
                return known;
            }
        }

        [Test]
        public void ACatch_CarriesItsIdNameAndTags()
        {
            var catchItem = new CatchItem(Cod, "Atlantic Cod", FishCategory.InshoreGroundfish, 3f, 12, 0.2f);
            GiftItem gift = GiftItem.FromCatch(catchItem, null);
            Assert.AreEqual(Cod, gift.Id);
            Assert.AreEqual("Atlantic Cod", gift.DisplayName);
            CollectionAssert.AreEqual(new[] { "catch", "inshore_groundfish" }, gift.Tags);
            Assert.IsTrue(gift.Giftable, "no facts published: a loose catch is giftable");
        }

        [Test]
        public void Giftable_IsTheSpeciesFlag_WhenTheFactsKnowIt()
        {
            var cod = new CatchItem(Cod, "Atlantic Cod", FishCategory.InshoreGroundfish, 3f, 12, 0.2f);
            Assert.IsFalse(GiftItem.FromCatch(cod, new Facts(Cod, false)).Giftable);
            Assert.IsTrue(GiftItem.FromCatch(cod, new Facts(Cod, true)).Giftable);
            Assert.IsTrue(GiftItem.FromCatch(cod, new Facts("fish.other", false)).Giftable, "unknown id: the default");
            Assert.IsFalse(GiftItem.IsGiftable(default(CatchItem), null), "no species: nothing to give");
        }

        [Test]
        public void CategoryTags_AreSnakeCase()
        {
            Assert.AreEqual("inshore_groundfish", GiftItem.CategoryTag(FishCategory.InshoreGroundfish));
            Assert.AreEqual("shellfish", GiftItem.CategoryTag(FishCategory.Shellfish));
            Assert.AreEqual("", GiftItem.SnakeCase(null));
        }

        // ---- the day ledger -----------------------------------------------------------------

        [Test]
        public void Ledger_CountsPerPersonPerDay_AndForgetsAtANewDay()
        {
            var ledger = new GiftLedger();
            Assert.AreEqual(0, ledger.GiftsToday("npc.a", 3));
            ledger.Count("npc.a", 3);
            Assert.AreEqual(1, ledger.GiftsToday("npc.a", 3));
            Assert.AreEqual(0, ledger.GiftsToday("npc.b", 3), "per person");
            Assert.AreEqual(0, ledger.GiftsToday("npc.a", 4), "a new day forgets");
            Assert.AreEqual(0, ledger.GiftsToday("npc.a", 3), "and does not come back");

            ledger.Count("npc.a", 0);
            ledger.Clear();
            Assert.AreEqual(0, ledger.GiftsToday("npc.a", 0));
            ledger.Count(null, 0);
            Assert.AreEqual(0, ledger.GiftsToday(null, 0));
        }
    }
}
