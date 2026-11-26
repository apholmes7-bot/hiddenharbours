using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using HiddenHarbours.Core;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The four dory supplies come from three people, found by asking around. Driven through the shipped
    /// run (Resources/NpcSupplies) and the same calls the conversation makes: the teller's lines, the rows
    /// a speaker offers, and what a row answers when it is picked. No scene.
    /// </summary>
    public class DorySupplyRunTests
    {
        const string RunAsset = "Assets/_Project/Data/Resources/NpcSupplies/SupplyRunStPetersDory.asset";
        const string NamedFlag = "st_peters.dory_supplies.named";
        const string Basil = "npc.basil_samson";
        const string Marguerite = "npc.marguerite_leblanc";
        const string Junior = "npc.junior_poirier";
        const string Eileen = "npc.eileen_doiron";
        const string Ginny = "npc.aunt_ginny";
        const string Rose = "npc.rose_macisaac";
        const string LightKeeper = "npc.alban_doucet";
        const string PhotoKeeper = "npc.odile_marchand";
        const int BagCapacity = 20;

        // ---- fakes ---------------------------------------------------------------------------------

        sealed class MemorySave : ISaveService
        {
            public MemorySave(SaveData data) { Current = data; }
            public SaveData Current { get; set; }
            public int Writes;
            public bool GetFlag(string key)
            {
                foreach (var f in Current.OnboardingFlags) if (f.Key == key) return f.Value;
                return false;
            }
            public void SetFlag(string key, bool value)
            {
                for (int i = 0; i < Current.OnboardingFlags.Count; i++)
                    if (Current.OnboardingFlags[i].Key == key) { Current.OnboardingFlags[i] = new SaveFlag(key, value); return; }
                Current.OnboardingFlags.Add(new SaveFlag(key, value));
            }
            public void Save() => Writes++;
        }

        /// <summary>Flags read and written straight through the save, as the world's store does.</summary>
        sealed class Flags : IFlagStore
        {
            readonly ISaveService _save;
            public Flags(ISaveService save) { _save = save; }
            public bool Get(string key) => _save.GetFlag(key);
            public void Set(string key, bool value) => _save.SetFlag(key, value);
        }

        /// <summary>Hands that really bag into the save's shopping bag, with a settable refusal.</summary>
        sealed class BagHands : IShoppingBagHands
        {
            readonly ISaveService _save;
            public ShoppingBagRefusal Refuse = ShoppingBagRefusal.None;
            public int RoomLeft = int.MaxValue;
            public BagHands(ISaveService save) { _save = save; }
            public bool HoldsBag => true;
            public bool BagIsSetDown => false;
            public ShoppingBagRefusal CanBag(ShoppingBagDef bag)
                => Refuse != ShoppingBagRefusal.None ? Refuse : RoomLeft > 0 ? ShoppingBagRefusal.None : ShoppingBagRefusal.BagFull;
            public ShoppingBagRefusal TryBag(ShoppingBagDef bag, string supplyId)
            {
                ShoppingBagRefusal can = CanBag(bag);
                if (can != ShoppingBagRefusal.None) return can;
                if (!ShoppingBagContents.TryPut(_save.Current, supplyId, BagCapacity)) return ShoppingBagRefusal.BagFull;
                RoomLeft--;
                return ShoppingBagRefusal.None;
            }
            public bool TryPutAway(string storageId) => false;
        }

        // ---- rig -----------------------------------------------------------------------------------

        SupplyRunDef _run;
        MemorySave _save;
        Flags _flags;
        BagHands _hands;

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();
            ShoppingBagContents.Clear();
            SlipDoryStory.IsHost = null;
            SupplyRunTalk.RunsSource = null;
            SupplyRunTalk.BagLookup = null;
            _run = AssetDatabase.LoadAssetAtPath<SupplyRunDef>(RunAsset);
            Assert.IsNotNull(_run, RunAsset + " is missing");
            _save = new MemorySave(SaveMigration.NewGame());
            SlipDoryStory.BeginForNewGame(_save);
            _flags = new Flags(_save);
            _hands = new BagHands(_save);
            GameServices.Save = _save;
        }

        [TearDown]
        public void TearDown()
        {
            ShoppingBagContents.Clear();
            SlipDoryStory.IsHost = null;
            SupplyRunTalk.RunsSource = null;
            SupplyRunTalk.BagLookup = null;
            GameServices.Reset();
        }

        void Own() => _save.Current.OwnedBoats.Add(_run.BoatId);
        void Named() => _save.SetFlag(NamedFlag, true);

        List<DialogueOption> Rows(string npc)
            => SupplyRunTalk.WithRows(npc, null, _save, _flags, () => _hands) ?? new List<DialogueOption>();

        DialogueOption Row(string npc, string optionId)
        {
            var rows = Rows(npc);
            int i = rows.FindIndex(r => r.Id == optionId);
            Assert.GreaterOrEqual(i, 0, npc + " offers no " + optionId);
            return rows[i];
        }

        SupplyGiftDef GiftOf(string giver) => _run.Gifts.Single(g => g.GiverNpcId == giver);

        string[] Ask(string npc) => Row(npc, _run.Topic.OptionId).ReplyAtPick();

        string Snapshot() => UnityEngine.JsonUtility.ToJson(_save.Current);

        static IEnumerable<string> AllLines(SupplyRunDef run)
        {
            foreach (var l in run.NamingLines.Concat(run.MissingLines).Concat(run.AllInHandLines)) yield return l;
            foreach (var g in run.Gifts)
            {
                yield return g.RowLabel;
                foreach (var l in g.ReplyLines.Concat(g.HandsFullLines).Concat(g.BagFullLines).Concat(g.BagElsewhereLines))
                    yield return l;
            }
            yield return run.Topic.RowLabel;
            foreach (var f in run.Topic.Fallbacks) yield return f.Line;
            foreach (var ladder in run.Topic.Ladders)
                foreach (var rung in ladder.Rungs)
                    foreach (var h in rung.Hints) yield return h.Line;
        }

        // ---- the shipped data -----------------------------------------------------------------------

        [Test]
        public void OnlyTheThree_GiveExactlyTheFourMendingSupplies()
        {
            var mending = AssetDatabase.LoadAssetAtPath<HiddenHarbours.Economy.DoryMendingDef>(
                "Assets/_Project/Data/Resources/DoryMending/StPetersDory.asset");
            Assert.IsNotNull(mending);
            var materials = mending.Jobs.SelectMany(j => j.Materials).Select(m => m.ItemId).ToList();
            var given = _run.Gifts.SelectMany(g => g.ItemIds).ToList();
            CollectionAssert.AreEquivalent(materials, given, "every material is given, once, and nothing else");
            CollectionAssert.AreEquivalent(new[] { Rose, LightKeeper, PhotoKeeper }, _run.Gifts.Select(g => g.GiverNpcId));
            Assert.AreEqual(new[] { "supply.patch_plank", "supply.copper_nails" }, GiftOf(PhotoKeeper).ItemIds,
                "plank and nails come on one row");
            foreach (var job in mending.Jobs)
                Assert.IsTrue(_run.Gifts.Any(g => g.ReceivedFlags.Contains(job.CompletedFlag)),
                    job.Id + ": a used supply must count as received");
            CollectionAssert.AreEquivalent(new[] { Marguerite, Junior, Eileen, Ginny, Rose },
                _run.Topic.Fallbacks.Select(f => f.NpcId), "the five who can be asked");
        }

        [Test]
        public void TheWords_SayBucketNeverPail_AndTheFirstRungsNameNoPlace()
        {
            foreach (string line in AllLines(_run))
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(line), "an empty line");
                StringAssert.DoesNotContain("pail", line.ToLowerInvariant(), line);
            }
            string[] places = { "rose", "macisaac", "whelps", "cliff", "cannery", "keeper", "square", "head", "light", "red house", "saltbox" };
            var unnamed = _run.NamingLines.Concat(_run.MissingLines).Concat(_run.Topic.Ladders.SelectMany(l => l.Rungs[0].Hints).Select(h => h.Line));
            foreach (string line in unnamed)
                foreach (string place in places)
                    StringAssert.DoesNotContain(place, line.ToLowerInvariant(), "names too much too soon: " + line);
            foreach (var ladder in _run.Topic.Ladders) Assert.AreEqual(3, ladder.Rungs.Length, ladder.Id);
        }

        // ---- the window -----------------------------------------------------------------------------

        [Test]
        public void NothingIsAdded_BeforeSheIsHis_BeforeBasilNames_OrAfterSheIsMended()
        {
            string[] mine = { "line" };
            Assert.AreSame(mine, SupplyRunTalk.WithTellerLines(Basil, mine, _save, _flags, out string none));
            Assert.IsNull(none);
            Assert.IsEmpty(Rows(Rose), "not his yet: no rows");

            Own();
            Assert.IsEmpty(Rows(Rose), "his, but Basil has not named what she wants: no rows");
            Assert.IsEmpty(Rows(Marguerite), "nor the question");

            Named();
            Assert.IsNotEmpty(Rows(Rose));

            _save.Current.RepairedBoats.Add(_run.BoatId);
            Assert.IsEmpty(Rows(Rose), "mended: nothing more to ask");
            Assert.AreSame(mine, SupplyRunTalk.WithTellerLines(Basil, mine, _save, _flags, out none));
        }

        [Test]
        public void Basil_NamesTheSupplies_ThenRemindsOnly_ThenSaysThatsTheLot()
        {
            Own();
            string[] first = SupplyRunTalk.WithTellerLines(Basil, new[] { "hello" }, _save, _flags, out string setWhenDone);
            Assert.AreEqual(new[] { "hello" }.Concat(_run.NamingLines).ToArray(), first, "appended to his own words");
            Assert.AreEqual(NamedFlag, setWhenDone, "the conversation's end records that he was told");
            Assert.IsFalse(_save.GetFlag(NamedFlag), "only the conversation's end sets it");

            Named();
            Assert.AreEqual(_run.MissingLines, SupplyRunTalk.WithTellerLines(Basil, null, _save, _flags, out setWhenDone));
            Assert.IsNull(setWhenDone);

            foreach (var g in _run.Gifts) SupplyRunTalk.Give(g, _save, _flags, _hands);
            Assert.AreEqual(_run.AllInHandLines, SupplyRunTalk.WithTellerLines(Basil, null, _save, _flags, out setWhenDone));
            Assert.AreSame(Array.Empty<string>(), SupplyRunTalk.WithTellerLines(Rose, Array.Empty<string>(), _save, _flags, out _),
                "only the teller tells");
        }

        // ---- the clue ladder ------------------------------------------------------------------------

        [Test]
        public void FourTalks_RoundUpTheClues_AndAFifthCompletesThem()
        {
            Own();
            Named();
            Assert.Greater(Ask(Marguerite).Length, 0);
            Assert.Greater(Ask(Junior).Length, 0);
            Assert.Greater(Ask(Eileen).Length, 0);
            string[] rose = Ask(Rose);

            Assert.IsTrue(_save.GetFlag("st_peters.dory_clue.oakum.3"), "four talks: Rose is named");
            Assert.IsTrue(_save.GetFlag("st_peters.dory_clue.paint.2"), "four talks: the light is pointed at");
            Assert.IsTrue(_save.GetFlag("st_peters.dory_clue.plank.3"), "four talks: the Whelps cottage is named");
            Assert.IsFalse(_save.GetFlag("st_peters.dory_clue.paint.3"));
            Assert.AreEqual(2, rose.Length, "Rose gives paint and plank, one line each");

            Ask(Ginny);
            Assert.IsTrue(_save.GetFlag("st_peters.dory_clue.paint.3"), "a fifth talk names the light keeper");
        }

        [Test]
        public void ARung_IsNeverSpokenBeforeTheOneBelowIt_AndOneLinePerSupplyPerAsk()
        {
            Own();
            Named();
            // Rose holds paint rung 2 and plank rungs 1 and 3. Asked first she can only give plank rung 1.
            string[] rose = Ask(Rose);
            Assert.AreEqual(1, rose.Length);
            Assert.IsTrue(_save.GetFlag("st_peters.dory_clue.plank.1"));
            Assert.IsFalse(_save.GetFlag("st_peters.dory_clue.plank.3"), "rung 3 waits for rung 2");
            Assert.IsFalse(_save.GetFlag("st_peters.dory_clue.paint.2"), "rung 2 waits for rung 1");

            // Asked again with nothing new she has, she gives her fallback and sets nothing.
            string before = Snapshot();
            string[] again = Ask(Rose);
            Assert.AreEqual(new[] { _run.Topic.FallbackFor(Rose) }, again);
            Assert.AreEqual(before, Snapshot());
        }

        [Test]
        public void ASupplyInHand_SilencesItsLadder_AndAllInHand_RemovesTheQuestion()
        {
            Own();
            Named();
            SupplyRunTalk.Give(GiftOf(Rose), _save, _flags, _hands);
            Ask(Marguerite);
            Assert.IsFalse(_save.GetFlag("st_peters.dory_clue.oakum.1"), "oakum in hand: nobody hints at it");
            Assert.IsTrue(_save.GetFlag("st_peters.dory_clue.plank.1"));

            SupplyRunTalk.Give(GiftOf(LightKeeper), _save, _flags, _hands);
            SupplyRunTalk.Give(GiftOf(PhotoKeeper), _save, _flags, _hands);
            Assert.IsFalse(Rows(Marguerite).Any(r => r.Id == _run.Topic.OptionId), "all in hand: nothing to ask");
        }

        // ---- the givers -----------------------------------------------------------------------------

        [Test]
        public void TalkingAndSayingGoodbye_GivesNothing()
        {
            Own();
            Named();
            string before = Snapshot();
            SupplyRunTalk.WithTellerLines(Rose, new[] { "hi" }, _save, _flags, out _);
            var rows = SupplyRunTalk.WithRows(Rose, DialogueOptionPicker.RowsFor(Array.Empty<DialogueOption>()),
                                              _save, _flags, () => _hands);
            Assert.AreEqual(DialogueOption.CloseId, rows[rows.Count - 1].Id, "the close row stays last");
            Assert.IsFalse(rows[rows.Count - 1].AnswersAtPick, "goodbye answers nothing");
            Assert.AreEqual(before, Snapshot(), "offering rows changes nothing");
            Assert.AreEqual(0, SupplyLocker.Owned(_save.Current, "supply.oakum"));
            Assert.AreEqual(0, _save.Writes);
        }

        [Test]
        public void PickingTheRow_GivesExactlyOne_Once()
        {
            Own();
            Named();
            DialogueOption row = Row(Rose, GiftOf(Rose).OptionId);
            string[] said = row.ReplyAtPick();
            Assert.AreEqual(GiftOf(Rose).ReplyLines, said);
            Assert.AreEqual(1, SupplyLocker.Owned(_save.Current, "supply.oakum"), "one hank");
            Assert.AreEqual(1, ShoppingBagContents.InBag(_save.Current, "supply.oakum"), "in the bag in his hand");
            Assert.AreEqual(1, _save.Writes, "one write");

            Assert.IsFalse(Rows(Rose).Any(r => r.Id == GiftOf(Rose).OptionId), "the row is gone once he has it");
            Assert.IsNull(row.ReplyAtPick(), "a stale row picked again gives nothing");
            Assert.AreEqual(1, SupplyLocker.Owned(_save.Current, "supply.oakum"), "still one");

            DialogueOption plank = Row(PhotoKeeper, GiftOf(PhotoKeeper).OptionId);
            plank.ReplyAtPick();
            Assert.AreEqual(1, SupplyLocker.Owned(_save.Current, "supply.patch_plank"));
            Assert.AreEqual(1, SupplyLocker.Owned(_save.Current, "supply.copper_nails"), "plank and nails on one row");
        }

        [Test]
        public void AnUnpromptedVisit_Works_WithNoClueHeard()
        {
            Own();
            Named();
            Assert.IsFalse(_save.Current.OnboardingFlags.Any(f => f.Key.StartsWith("st_peters.dory_clue.")));
            Row(Rose, GiftOf(Rose).OptionId).ReplyAtPick();
            Row(LightKeeper, GiftOf(LightKeeper).OptionId).ReplyAtPick();
            Assert.AreEqual(1, SupplyLocker.Owned(_save.Current, "supply.oakum"));
            Assert.AreEqual(1, SupplyLocker.Owned(_save.Current, "supply.linseed_paint"));
        }

        [Test]
        public void ARefusal_WithFullHands_ChangesNothing()
        {
            Own();
            Named();
            foreach (var (refusal, lines) in new[]
                     {
                         (ShoppingBagRefusal.HandsFull, GiftOf(Rose).HandsFullLines),
                         (ShoppingBagRefusal.BagFull, GiftOf(Rose).BagFullLines),
                         (ShoppingBagRefusal.BagElsewhere, GiftOf(Rose).BagElsewhereLines),
                     })
            {
                _hands.Refuse = refusal;
                string before = Snapshot();
                Assert.AreEqual(lines, Row(Rose, GiftOf(Rose).OptionId).ReplyAtPick(), refusal.ToString());
                Assert.AreEqual(before, Snapshot(), refusal + " changed the save");
                Assert.AreEqual(0, _save.Writes);
            }
            _hands.Refuse = ShoppingBagRefusal.None;
            Assert.IsTrue(Rows(Rose).Any(r => r.Id == GiftOf(Rose).OptionId), "the row is still there to ask again");
        }

        [Test]
        public void ATwoItemGift_IntoABagWithOneSpace_IsAllOrNone()
        {
            Own();
            Named();
            _hands.RoomLeft = 1;
            string before = Snapshot();
            Assert.AreEqual(GiftOf(PhotoKeeper).BagFullLines, Row(PhotoKeeper, GiftOf(PhotoKeeper).OptionId).ReplyAtPick());
            Assert.AreEqual(before, Snapshot(), "the plank came back out and neither is his");
            Assert.AreEqual(0, ShoppingBagContents.Total(_save.Current));
        }

        [Test]
        public void AGuest_IsGivenNothing()
        {
            Own();
            Named();
            SlipDoryStory.IsHost = () => false;
            string before = Snapshot();
            Assert.IsNull(Row(Rose, GiftOf(Rose).OptionId).ReplyAtPick());
            Assert.AreEqual(before, Snapshot());
        }

        [Test]
        public void TheRowsSitBeforeTheCloseRow_InTheSpeakersOwnList()
        {
            Own();
            Named();
            var authored = new[] { new DialogueOption { Id = "option.test", Label = "Test" } };
            var rows = SupplyRunTalk.WithRows(Rose, DialogueOptionPicker.RowsFor(authored), _save, _flags, () => _hands);
            Assert.AreEqual(new[] { "option.test", GiftOf(Rose).OptionId, _run.Topic.OptionId, DialogueOption.CloseId },
                            rows.Select(r => r.Id).ToArray());
        }

        // ---- the slip's missing line ----------------------------------------------------------------

        [Test]
        public void TheSlip_SaysBasilWouldKnow_UntilHeHasNamedThem()
        {
            var station = AssetDatabase.LoadAssetAtPath<HiddenHarbours.Economy.DoryMendingStationDef>(
                "Assets/_Project/Data/Regions/StPetersVillage/slipmend.basils_dory.asset");
            Assert.IsNotNull(station);
            Assert.AreEqual(NamedFlag, station.UnaskedFlag);
            Assert.AreEqual("Basil would know what she wants.", station.MissingLineFor("caulk the seam", _save));
            Named();
            string asked = station.MissingLineFor("caulk the seam", _save);
            StringAssert.Contains("caulk the seam", asked);
            StringAssert.DoesNotContain("bag", asked.ToLowerInvariant(), "supplies put away count too");
            StringAssert.DoesNotContain("pail", asked.ToLowerInvariant());
        }
    }
}
