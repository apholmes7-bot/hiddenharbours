using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Economy;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>THE PEOPLE BEHIND THE COUNTERS, AS CONTENT</b> — the two clerks' authored rows, checked
    /// against the stock they claim to sell and against the rules a row has to keep.
    ///
    /// <para><b>Why the content and not the panel.</b> The book, the hold and the picker are covered
    /// where they live (<c>CatalogBookTests</c>, <c>DialogueCatalogHoldPlayTests</c>). What is new here
    /// is <i>authoring</i>: a seller id typed into a dialogue asset that no listing stocks opens an empty
    /// book, and an empty book is silent — no exception, no red, just a shopkeeper handing you a blank
    /// page. That is the exact "silent-empty-tab failure" the design's own validation clause names
    /// (§10), and it is only catchable by reading both sides at once, which is what this does.</para>
    ///
    /// <para><b>It sweeps the whole dialogue tree, not just these two.</b> Every rule below is stated
    /// over every <see cref="DialogueDef"/> in <c>Data/NPCs</c>, so the third clerk cannot ship a book
    /// nobody stocks either. The two named cases are pinned separately because their <i>words</i> are the
    /// deliverable and a silent re-authoring should be a red, not a surprise in a playtest.</para>
    /// </summary>
    public class StoreClerkContentTests
    {
        const string DialogueFolder = "Assets/_Project/Data/NPCs/Dialogue";
        const string NpcFolder = "Assets/_Project/Data/NPCs";
        const string RoutineFolder = "Assets/_Project/Data/Routines";

        const string LeBlancs = "seller.leblancs";
        const string NmcChandlery = "seller.nmc_chandlery";

        static List<DialogueDef> AllDialogue() => Load<DialogueDef>(DialogueFolder);
        static List<NpcDef> AllNpcs() => Load<NpcDef>(NpcFolder);

        static List<T> Load<T>(string folder) where T : Object
        {
            var found = new List<T>();
            foreach (string guid in AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { folder }))
            {
                var a = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
                if (a != null) found.Add(a);
            }
            return found;
        }

        static DialogueDef Dialogue(string id)
        {
            DialogueDef d = AllDialogue().FirstOrDefault(x => x.Id == id);
            Assert.IsNotNull(d, $"no DialogueDef with id '{id}' under {DialogueFolder}");
            return d;
        }

        static DialogueOption Row(DialogueDef d, string optionId)
        {
            int i = System.Array.FindIndex(d.Options, o => o.Id == optionId);
            Assert.GreaterOrEqual(i, 0, $"'{d.Id}' has no row '{optionId}' (rows: " +
                                        $"{string.Join(", ", d.Options.Select(o => o.Id))})");
            return d.Options[i];
        }

        /// <summary>Every seller id any listing in the shipped catalog names, whatever its kind.</summary>
        static HashSet<string> SellersWithStock()
        {
            var sellers = new HashSet<string>(System.StringComparer.Ordinal);
            void Take(IEnumerable<ICatalogListing> listings)
            {
                foreach (ICatalogListing l in listings)
                {
                    CatalogListing tag = l.Catalog;
                    if (!tag.Listed || tag.Sellers == null) continue;
                    foreach (string s in tag.Sellers)
                        if (!string.IsNullOrEmpty(s)) sellers.Add(s);
                }
            }
            Take(CatalogSource.Boats());
            Take(CatalogSource.Gear());
            Take(CatalogSource.Pots());
            Take(CatalogSource.Bait());
            Take(CatalogSource.Supplies());
            Take(CatalogSource.Instruments());
            Take(CatalogSource.Licenses());
            return sellers;
        }

        // =========================================================================================
        //  The rules, over every conversation in the tree
        // =========================================================================================

        /// <summary>
        /// A browse row's seller must actually stock something Listed.
        ///
        /// <para>This is the clerk-side half of the design's validation clause: it already fails a
        /// listing whose sellers name nobody, and this fails the mirror image — a seller named by a
        /// conversation that no listing names back. Either way the symptom is the same blank page.</para>
        /// </summary>
        [Test]
        public void EveryBrowseRow_NamesASellerSomethingIsActuallyStockedTo()
        {
            HashSet<string> stocked = SellersWithStock();
            Assert.IsNotEmpty(stocked, "no listing in Data/Resources/Catalog is Listed to any seller — " +
                                       "the sweep found nothing, so the rule below would pass vacuously");

            var offenders = new List<string>();
            int browseRows = 0;
            foreach (DialogueDef d in AllDialogue())
                foreach (DialogueOption o in d.Options)
                {
                    if (!o.OpensCatalog) continue;
                    browseRows++;
                    if (!stocked.Contains(o.CatalogSellerId))
                        offenders.Add($"{d.Id}/{o.Id} → '{o.CatalogSellerId}'");
                }

            Assert.Greater(browseRows, 0, "no conversation opens a wares book at all — the rule would " +
                                          "pass by measuring an empty room");
            Assert.IsEmpty(offenders,
                "these rows open a book nothing is stocked to, which shows the player a blank page and " +
                "says nothing: " + string.Join(" · ", offenders));
        }

        /// <summary>A row browses or it sells; carrying both ids is an authoring mistake the presenter
        /// resolves silently in favour of browsing, so it is caught here instead.</summary>
        [Test]
        public void NoRow_BothBrowsesAndSells()
        {
            var offenders = (from d in AllDialogue()
                             from o in d.Options
                             where o.OpensCatalog && o.SellsAtCounter
                             select $"{d.Id}/{o.Id}").ToList();

            Assert.IsEmpty(offenders, "a row may open a book or take a catch, not both: " +
                                      string.Join(" · ", offenders));
        }

        /// <summary>A sell row must be able to say a number. Without the payout token the sale happens
        /// and the speaker announces nothing about it, which reads as the row doing nothing.</summary>
        [Test]
        public void EverySellRow_SpeaksItsPayout_AndHasAnEmptyPailLine()
        {
            var noFigure = new List<string>();
            var noEmpty = new List<string>();
            int sellRows = 0;

            foreach (DialogueDef d in AllDialogue())
                foreach (DialogueOption o in d.Options)
                {
                    if (!o.SellsAtCounter) continue;
                    sellRows++;

                    bool saysTheNumber = o.ReplyLines != null &&
                        o.ReplyLines.Any(l => l != null && l.Contains(DialogueOption.PayoutToken));
                    if (!saysTheNumber) noFigure.Add($"{d.Id}/{o.Id}");

                    if (o.NothingToSellLines == null || o.NothingToSellLines.Length == 0)
                        noEmpty.Add($"{d.Id}/{o.Id}");
                }

            Assert.Greater(sellRows, 0, "no conversation sells over a counter — the rule would pass " +
                                        "by measuring an empty room");
            Assert.IsEmpty(noFigure, $"a sell row whose lines never carry {DialogueOption.PayoutToken} " +
                                     "sells your catch and tells you nothing: " + string.Join(" · ", noFigure));
            Assert.IsEmpty(noEmpty, "an empty pail is a different sentence from a sale of nought, and " +
                                    "these rows have only the one: " + string.Join(" · ", noEmpty));
        }

        /// <summary>
        /// No row is half-typed.
        ///
        /// <para><b>The arm <c>NpcContentValidationTests</c> cannot make.</b> Its id/uniqueness rule
        /// <i>skips</i> a half-typed row (<c>if (!o.IsAuthored) continue;</c>) because the picker skips
        /// it too — which is right for that rule and is exactly the hole here: a clerk whose sell row
        /// lost its label still has a conversation, still has a book, and has simply stopped being able
        /// to take your catch, with nothing red anywhere.</para>
        ///
        /// <para>The namespaced-id, per-conversation-uniqueness and reserved-<c>option.close</c> rules
        /// are NOT restated here — <c>NpcContentValidationTests</c> owns them, and one fact asserted in
        /// two files is one fact that can disagree with itself.</para>
        /// </summary>
        [Test]
        public void NoAuthoredRow_IsHalfTyped()
        {
            var halfTyped = new List<string>();
            foreach (DialogueDef d in AllDialogue())
                for (int i = 0; i < d.Options.Length; i++)
                    if (!d.Options[i].IsAuthored)
                        halfTyped.Add($"{d.Id}[{i}] id='{d.Options[i].Id}' label='{d.Options[i].Label}'");

            Assert.IsEmpty(halfTyped, "a row needs an id AND a label or the picker drops it silently, " +
                                      "which takes a verb off a counter without saying so: " +
                                      string.Join(" · ", halfTyped));
        }

        // =========================================================================================
        //  Marguerite — St Peters
        // =========================================================================================

        [Test]
        public void Marguerite_HasBrowseSellAndOneQuestion_AndThePickerAddsTheWayOut()
        {
            DialogueDef d = Dialogue("dialogue.marguerite_first");

            CollectionAssert.AreEqual(
                new[] { "option.browse_wares", "option.sell_catch", "option.ask_about_store" },
                d.Options.Select(o => o.Id).ToArray(),
                "the storekeeper's three rows, in the order she offers them");

            List<DialogueOption> shown = DialogueOptionPicker.RowsFor(d.Options);
            Assert.IsNotNull(shown, "her conversation ends in a choice");
            Assert.AreEqual(4, shown.Count, "three authored rows plus the appended way out");
            Assert.AreEqual(DialogueOption.CloseId, shown[shown.Count - 1].Id, "the way out is always last");
        }

        [Test]
        public void Marguerite_BrowseRow_OpensHerOwnCounterOnItsOnlyShelf()
        {
            DialogueOption browse = Row(Dialogue("dialogue.marguerite_first"), "option.browse_wares");

            Assert.IsTrue(browse.OpensCatalog);
            Assert.AreEqual(LeBlancs, browse.CatalogSellerId);
            Assert.IsTrue(CatalogSections.TryParse(browse.CatalogSection, out CatalogSection on),
                          $"'{browse.CatalogSection}' is not a section the book knows, so it would open on " +
                          "whatever shelf happened to be first");
            Assert.AreEqual(CatalogSection.Gear, on, "everything on her counter is the everyday stock");
        }

        /// <summary>
        /// Her five vendors' stock is really tagged to her — the rod, the bait, the ice, the sounder and
        /// the licence, which is exactly what #356 put on that counter.
        ///
        /// <para>Named one by one rather than counted, because "five things" would stay green if the ice
        /// were swapped for a second rod.</para>
        /// </summary>
        [Test]
        public void Marguerite_Book_ListsTheFiveThingsOnHerCounter()
        {
            var ids = new List<string>();
            ids.AddRange(CatalogSource.For<GearOffer>(LeBlancs).Select(o => o.Id));
            ids.AddRange(CatalogSource.For<BaitDef>(LeBlancs).Select(o => o.Id));
            ids.AddRange(CatalogSource.For<SupplyDef>(LeBlancs).Select(o => o.Id));
            ids.AddRange(CatalogSource.For<InstrumentOffer>(LeBlancs).Select(o => o.Id));
            ids.AddRange(CatalogSource.For<LicenseDef>(LeBlancs).Select(o => o.Id));

            CollectionAssert.AreEquivalent(
                new[] { "gear.rod", "bait.capelin", "supply.ice", "instrument.depth_sounder", "license.clam" },
                ids, "her book on day one is the counter #356 built, item for item");
        }

        [Test]
        public void Marguerite_SellRow_TakesTheCatchOverHerOwnCounter()
        {
            DialogueOption sell = Row(Dialogue("dialogue.marguerite_first"), "option.sell_catch");

            Assert.IsTrue(sell.SellsAtCounter);
            Assert.IsFalse(sell.OpensCatalog, "the sell row is not a second book");
            Assert.AreEqual(LeBlancs, sell.SellAtSellerId,
                            "she takes it over the counter she sells from — one seller id answers both");
            Assert.IsTrue(sell.ReplyLines.Any(l => l.Contains(DialogueOption.PayoutToken)),
                          "she counts the money out loud");
            Assert.IsNotEmpty(sell.NothingToSellLines, "and has something to say to an empty pail");
        }

        // =========================================================================================
        //  Claudette — Nine Mile Creek
        // =========================================================================================

        [Test]
        public void Claudette_Exists_AndHerNpcDefIsWholeEnoughToStandUp()
        {
            NpcDef npc = AllNpcs().FirstOrDefault(n => n.Id == "npc.claudette_boudreau");
            Assert.IsNotNull(npc, "the creek's storekeeper has no NpcDef, so NineMileCreekPeople.Place " +
                                  "would skip her with a warning and the store would have nobody at it");
            Assert.AreEqual("Claudette Boudreau", npc.DisplayName);
            Assert.IsNotNull(npc.Dialogue, "an NpcDef with no dialogue is a mute standee");
            Assert.AreEqual("dialogue.claudette_first", npc.Dialogue.Id);
            Assert.AreEqual("met_claudette", npc.CompletionFlag, "her own flag, not a shared one");
            Assert.IsNotNull(npc.Build, "she wears the packer body Marguerite wears — different region, " +
                                        "never on screen together");
        }

        [Test]
        public void Claudette_BrowsesTheChandlery_AndHerBookHasTheRodInIt()
        {
            DialogueOption browse = Row(Dialogue("dialogue.claudette_first"), "option.browse_wares");

            Assert.IsTrue(browse.OpensCatalog);
            Assert.AreEqual(NmcChandlery, browse.CatalogSellerId,
                            "the general store at the creek is the chandlery lot — one building, and the " +
                            "GearShop on it carries this id");

            CollectionAssert.AreEquivalent(
                new[] { "gear.rod" },
                CatalogSource.For<GearOffer>(NmcChandlery).Select(o => o.Id).ToArray(),
                "what that store's one vendor already sells, and nothing invented for her");
        }

        /// <summary>
        /// <b>She has NO sell row, and that is a decision.</b>
        ///
        /// <para>R7 says the sell verb fronts a counter's EXISTING sell components. The creek's general
        /// store has a <c>GearShop</c> and nothing else — no Market, no FishBuyer, no WharfSellPoint — so a
        /// sell row on her would mean writing new sell economics, which this slice does not do. Fish is
        /// sold at the buyer's truck on the wharf, which is Wendell's. Pinned so the omission reads as a
        /// decision rather than as something that fell off.</para>
        /// </summary>
        [Test]
        public void Claudette_HasNoSellRow_BecauseHerCounterHasNothingThatBuys()
        {
            DialogueDef d = Dialogue("dialogue.claudette_first");

            CollectionAssert.AreEqual(
                new[] { "option.browse_wares", "option.ask_about_prices" },
                d.Options.Select(o => o.Id).ToArray());
            Assert.IsFalse(d.Options.Any(o => o.SellsAtCounter),
                           "nothing on the chandlery lot buys a catch; a sell row there would be a promise " +
                           "no component keeps");
        }

        /// <summary>The two storekeepers are different places, in the only channel they have — their
        /// words. They share a body by necessity (there is one counter build in the rig), so a copied
        /// line would make them one person.</summary>
        [Test]
        public void TheTwoStorekeepers_DoNotShareALine()
        {
            var hers = new HashSet<string>(Lines(Dialogue("dialogue.marguerite_first")),
                                           System.StringComparer.Ordinal);
            var theirs = Lines(Dialogue("dialogue.claudette_first")).ToList();

            var shared = theirs.Where(hers.Contains).ToList();
            Assert.IsEmpty(shared, "one voice in two mouths: " + string.Join(" · ", shared));
        }

        static IEnumerable<string> Lines(DialogueDef d)
            => d.FirstLines.Concat(d.RepeatLines)
                .Concat(d.Options.SelectMany(o => (o.ReplyLines ?? System.Array.Empty<string>())
                    .Concat(o.NothingToSellLines ?? System.Array.Empty<string>())))
                .Where(l => !string.IsNullOrWhiteSpace(l));

        // =========================================================================================
        //  The closed store
        // =========================================================================================

        /// <summary>
        /// <b>A closed store is a clerk who is not there</b> — no signage system, no shutter, no
        /// separate "open" state to keep in sync with anything.
        ///
        /// <para>Read off the shipped routine through the engine's own block rule
        /// (<see cref="RoutineSchedule.BlockIndexAt"/>), not a transcription of it: at nine at night she
        /// is upstairs over the shop, and the counter she was standing at is empty ground.</para>
        /// </summary>
        [Test]
        public void AfterNine_TheStorekeeperIsUpstairs_AndTheCounterIsEmpty()
        {
            var routine = AssetDatabase.LoadAssetAtPath<RoutineDef>(
                $"{RoutineFolder}/MargueriteLeBlancRoutine.asset");
            Assert.IsNotNull(routine, "the storekeeper's day is what makes the shop dark at night");

            float[] departures = routine.Entries.Select(e => e.StartHour).ToArray();
            string StationAt(float hour) => routine.Entries[RoutineSchedule.BlockIndexAt(hour, departures)].StationId;

            Assert.AreEqual("station.st_peters.store_counter", StationAt(9f),
                            "mid-morning she is behind the counter");
            foreach (float hour in new[] { 21.5f, 23f, 2f, 5f })
                Assert.AreEqual("station.st_peters.home_store", StationAt(hour),
                                $"at {hour:0.0} she is in, upstairs, and the storefront is dark");
        }

        /// <summary>
        /// And with her upstairs there is no other way in: exactly one conversation in the whole tree
        /// opens her counter's book, and it is hers.
        ///
        /// <para>This is the assertion the dev keys used to make false. P opened any stall's book from
        /// the pavement at any hour; it is gone, and the only door is the person.</para>
        /// </summary>
        [Test]
        public void HerCounter_HasExactlyOneWayIn_AndItIsHer()
        {
            var openers = (from d in AllDialogue()
                           from o in d.Options
                           where o.OpensCatalog && o.CatalogSellerId == LeBlancs
                           select d.Id).ToList();

            CollectionAssert.AreEqual(new[] { "dialogue.marguerite_first" }, openers,
                "the counter is reached through the storekeeper and nothing else — so when her day " +
                "takes her upstairs, the shop is shut");
        }
    }
}
