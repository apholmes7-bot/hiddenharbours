using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.Core;
using HiddenHarbours.Economy;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// <b>THE WHOLE WALK UP TO A COUNTER</b> — talk to the clerk, browse her book, buy a thing, close
    /// the book, sell what you are carrying, and leave. One conversation, one person, both regions.
    ///
    /// <para><b>Real content, real runtime, built counter.</b> The conversations are the shipped
    /// <see cref="DialogueDef"/> assets (so re-authoring a row breaks this, which is the point), the
    /// stock is the shipped catalog listings, and the counter is stood up the way
    /// <c>StPetersBuilder</c> stands it up — five vendors and a sell stack on one GameObject, stamped
    /// with one seller id. A region SCENE is deliberately not loaded: it would poison every later test
    /// in the run (#499) and, until the scenes are re-banked off the builders, it does not carry the
    /// catalog runtime at all.</para>
    ///
    /// <para><b>⚠️ No key presses.</b> Headless input cannot deliver one and a self-skipping key-driven
    /// test is a hole (#555). Every step drives the seam a press calls — <c>Advance()</c>,
    /// <c>MoveSelection(axis)</c>, <c>CatalogBookPresenter.Confirm()</c>, <c>Close()</c>.</para>
    ///
    /// <para><b>⚠️ Headless-safe by construction.</b> Nothing renders, reads pixels or calls
    /// <c>Camera.Render</c>: CI runs with a null graphics device, where a ReadPixels PlayMode test does
    /// not fail — it kills the editor with no results XML at all.</para>
    /// </summary>
    public class StoreClerkJourneyPlayTests
    {
        const string LeBlancs = "seller.leblancs";
        const string NmcChandlery = "seller.nmc_chandlery";
        const string DialogueFolder = "Assets/_Project/Data/NPCs/Dialogue";
        const string CatalogFolder = "Assets/_Project/Data/Resources/Catalog";

        readonly List<Object> _spawned = new();

        DialoguePresenter _bubble;
        CatalogBookPresenter _book;
        Transform _speaker;
        TestWallet _wallet;
        SaveData _save;

        // ---- doubles (the two providers a counter resolves in Awake) -------------------------------

        sealed class TestWallet : MonoBehaviour, IWallet
        {
            public int Money { get; private set; }
            public int Credits, Debits;
            public void Seed(int money) => Money = money;
            public void Add(int amount) { Money += amount; Credits++; }
            public bool TrySpend(int amount)
            {
                if (amount > Money) return false;
                Money -= amount; Debits++; return true;
            }
        }

        sealed class TestHold : MonoBehaviour, IHold
        {
            readonly List<CatchItem> _items = new();
            public int CapacityUnits => 20;
            public int UsedUnits => _items.Count;
            public IReadOnlyList<CatchItem> Items => _items;
            public bool TryAdd(CatchItem item) { _items.Add(item); return true; }
            public void Clear() => _items.Clear();
        }

        sealed class TestSave : ISaveService
        {
            public TestSave(SaveData d) { Current = d; }
            public SaveData Current { get; }
            readonly Dictionary<string, bool> _flags = new();
            public int Writes;
            public bool GetFlag(string key) => _flags.TryGetValue(key, out bool v) && v;
            public void SetFlag(string key, bool value) { _flags[key] = value; Writes++; }
            public void Save() => Writes++;
        }

        // ---- fixture --------------------------------------------------------------------------------

        [SetUp]
        public void SetUp()
        {
            // ⚠️ ONE AUDIO LISTENER: Unity logs "There are no audio listeners in the scene" every frame
            // of a listener-less play-mode scene, and these tests advance real frames on purpose.
            Spawn("AudioListener").AddComponent<AudioListener>();

            InteractionGate.Reset();

            var camGo = Spawn("TestCamera");
            camGo.tag = "MainCamera";                       // the presenter's Camera.main fallback
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 6f;
            cam.transform.position = new Vector3(0f, 0f, -10f);

            _speaker = Spawn("Clerk").transform;
            _speaker.position = Vector3.zero;

            _save = new SaveData();
            GameServices.Save = new TestSave(_save);

            var walletGo = Spawn("Wallet");
            _wallet = walletGo.AddComponent<TestWallet>();
            _wallet.Seed(500);
            GameServices.Wallet = _wallet;

            // The bubble and the book ride ONE GameObject, exactly as both region builders wire them:
            // the book is opened by a conversation and sorts below it, so the speaker is never hidden
            // behind what she is holding out.
            var ui = Spawn("DialogueUI");
            _bubble = ui.AddComponent<DialoguePresenter>();
            _book = ui.AddComponent<CatalogBookPresenter>();

            CounterSellDesk.Install();
        }

        [TearDown]
        public void TearDown()
        {
            // ⚠️ Stand the desk down rather than EventBus.Clear<T>(): Clear drops EVERY handler on the
            // channel, which would silently disarm listeners these tests do not own.
            CounterSellDesk.Uninstall();

            GameServices.Save = null;
            GameServices.Wallet = null;

            // ⚠ DestroyImmediate, not Destroy: Destroy is deferred to end-of-frame, so the next
            // test's SetUp can run while this presenter is still alive AND STILL SUBSCRIBED - which
            // is precisely what the no-book-in-the-scene cases measure the absence of.
            foreach (Object o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
            InteractionGate.Reset();
        }

        GameObject Spawn(string name)
        {
            var go = new GameObject(name);
            _spawned.Add(go);
            return go;
        }

        static T Asset<T>(string path) where T : Object
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(path);
            Assert.IsNotNull(a, $"missing shipped asset: {path}");
            return a;
        }

        static DialogueDef DialogueNamed(string stem) => Asset<DialogueDef>($"{DialogueFolder}/{stem}.asset");

        static void SetPrivate(object target, string field, object value)
        {
            FieldInfo f = target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(f, $"field '{field}' not found on {target.GetType().Name}");
            f.SetValue(target, value);
        }

        /// <summary>
        /// St Peters' general store counter, as <c>StPetersBuilder</c> builds it: five vendors and a
        /// Market → FishBuyer → WharfSellPoint on one GameObject, all stamped <c>seller.leblancs</c>,
        /// with the pail you carry as the hold.
        ///
        /// <para><b>Built INACTIVE and activated last</b> — <c>WharfSellPoint.Awake</c> resolves its
        /// providers, and AddComponent on a live GameObject runs Awake before a field can be set, which
        /// would leave the counter with no hold and a sale that silently pays nothing.</para>
        /// </summary>
        TestHold BuildStPetersCounter()
        {
            var pail = Spawn("ClamBucket");
            TestHold hold = pail.AddComponent<TestHold>();

            var counter = Spawn("GeneralStoreCounter");
            counter.SetActive(false);

            void Vendor<T>(string offerField, Object offer) where T : Component
            {
                var v = counter.AddComponent<T>();
                SetPrivate(v, offerField, offer);
                SetPrivate(v, "_walletProvider", _wallet.gameObject);
                SetPrivate(v, "_sellerId", LeBlancs);
            }

            Vendor<GearShop>("_offer", Asset<GearOffer>($"{CatalogFolder}/Gear/Rod.asset"));
            Vendor<BaitShop>("_bait", Asset<BaitDef>($"{CatalogFolder}/Bait/Capelin.asset"));
            Vendor<SupplyShop>("_supply", Asset<SupplyDef>($"{CatalogFolder}/Supplies/Ice.asset"));
            Vendor<InstrumentShop>("_offer", Asset<InstrumentOffer>($"{CatalogFolder}/Instruments/DepthSounderOffer.asset"));
            Vendor<LicenseVendor>("_license", Asset<LicenseDef>($"{CatalogFolder}/Licenses/ClamLicense.asset"));

            var market = counter.AddComponent<Market>();
            SetPrivate(market, "_marketId", MarketId.StPetersStore);
            var buyer = counter.AddComponent<FishBuyer>();
            SetPrivate(buyer, "_market", market);
            var sell = counter.AddComponent<WharfSellPoint>();
            SetPrivate(sell, "_buyer", buyer);
            SetPrivate(sell, "_holdProvider", pail);
            SetPrivate(sell, "_walletProvider", _wallet.gameObject);

            counter.SetActive(true);
            return hold;
        }

        /// <summary>The creek's general store: one GearShop with the rod, and NOTHING that buys — which
        /// is exactly why its clerk has no sell row.</summary>
        void BuildCreekStore()
        {
            var store = Spawn("GeneralStore");
            store.SetActive(false);
            var gear = store.AddComponent<GearShop>();
            SetPrivate(gear, "_offer", Asset<GearOffer>($"{CatalogFolder}/Gear/Rod.asset"));
            SetPrivate(gear, "_walletProvider", _wallet.gameObject);
            SetPrivate(gear, "_sellerId", NmcChandlery);
            store.SetActive(true);
        }

        /// <summary>Walk up and talk: the request <c>WorldInteractor</c> builds, from the same asset.</summary>
        void WalkUpAndTalk(DialogueDef dialogue, string npcId)
        {
            _bubble.Play(new DialogueRequest(
                dialogue.Lines(metBefore: false).Select(l => new DialogueLine("Clerk", l)).ToList(),
                _speaker, new Vector3(0f, 2.1f, 0f), DialogueVoice.Default,
                DialogueOptionPicker.RowsFor(dialogue.Options), dialogue.Id, npcId));
        }

        /// <summary>Press Interact until the rows are up. Guarded so a change that stops the picker ever
        /// opening fails here rather than spinning.</summary>
        void PressThroughToTheRows()
        {
            for (int press = 0; press < 12 && !_bubble.IsChoosing; press++) _bubble.Advance();
            Assert.IsTrue(_bubble.IsChoosing, "her lines never reached the picker");
        }

        /// <summary>Put the cursor on a row by index and confirm it — the axis a key would feed, one
        /// latched step at a time, then the Interact press.</summary>
        void PickRow(int index)
        {
            for (int guard = 0; guard < 16 && _bubble.SelectedOption != index; guard++)
            {
                _bubble.MoveSelection(-1f);   // down the list
                _bubble.MoveSelection(0f);    // ⚠ the latch: the axis must fall back before it steps again
            }
            Assert.AreEqual(index, _bubble.SelectedOption, "the cursor never reached that row");
            _bubble.Advance();
        }

        static IEnumerator Settle() { yield return null; yield return null; }

        // =========================================================================================
        //  ST PETERS — the whole journey
        // =========================================================================================

        [UnityTest]
        public IEnumerator StPeters_BrowseThenSellThenLeave_IsOneConversationWithOnePerson()
        {
            TestHold pail = BuildStPetersCounter();
            pail.TryAdd(new CatchItem("fish.soft_shell_clam", "Clam", FishCategory.Shellfish, 0.4f, 9, 0.3f));
            pail.TryAdd(new CatchItem("fish.soft_shell_clam", "Clam", FishCategory.Shellfish, 0.5f, 9, 0.3f));

            DialogueDef d = DialogueNamed("MargueriteFirst");
            WalkUpAndTalk(d, "npc.marguerite_leblanc");
            yield return Settle();

            // --- the bubble comes up with the rows ---------------------------------------------
            PressThroughToTheRows();
            Assert.AreEqual(4, RowsShowing(), "three of hers and the appended way out");

            // --- browse: the book opens and the conversation HOLDS -----------------------------
            int purseBefore = _wallet.Money;
            PickRow(0);
            yield return Settle();

            Assert.IsTrue(CatalogBookPresenter.IsOpen, "her book did not open");
            Assert.AreEqual(LeBlancs, _book.SellerId, "somebody else's book");
            Assert.IsTrue(_bubble.IsAwaitingCatalog, "the conversation must hold, not end");
            Assert.IsTrue(_bubble.IsShowing, "she is still standing there with the bubble on her");
            Assert.IsFalse(_bubble.IsChoosing, "the rows go down while the book is up");
            Assert.IsTrue(InteractionGate.IsBlocked, "the player is mid-conversation and must not walk off");
            Assert.IsFalse(_bubble.Advance(), "an Interact press belongs to the book, not the bubble");

            Assert.IsNotNull(_book.Book);
            Assert.Greater(_book.Book.Shelf.Count, 0, "her book is not empty on day one");

            // --- buy a thing: money and save move EXACTLY once ---------------------------------
            BuyRow bought = FindAffordableRow();
            Assert.IsTrue(_book.Confirm(), $"'{bought.Id}' would not buy");

            Assert.AreEqual(1, _wallet.Debits, "the purse was charged exactly once");
            Assert.AreEqual(purseBefore - bought.Quote.Price, _wallet.Money, "charged the quoted price");
            Assert.AreEqual(1, OwnedCount(bought.Id), "the save records it exactly once");

            Assert.IsFalse(_book.Confirm(), "a second confirm on an owned row must not charge again");
            Assert.AreEqual(1, _wallet.Debits, "and it did not");
            Assert.AreEqual(1, OwnedCount(bought.Id));

            // --- close the book: the SAME rows come back ---------------------------------------
            _book.Close();
            yield return Settle();

            Assert.IsFalse(CatalogBookPresenter.IsOpen);
            Assert.IsFalse(_bubble.IsAwaitingCatalog, "the hold is released");
            Assert.IsTrue(_bubble.IsChoosing, "the picker re-arms — she is handed back, not an empty street");
            Assert.AreEqual(4, RowsShowing(), "the rows that came back are the rows that went down");

            // --- sell: the payout is spoken in HER bubble, and no screen opens ------------------
            int purseBeforeSale = _wallet.Money;
            PickRow(1);
            yield return Settle();

            Assert.AreEqual(0, pail.UsedUnits, "she took the lot");
            int paid = _wallet.Money - purseBeforeSale;
            Assert.Greater(paid, 0, "a sale that paid nothing is not a sale");
            Assert.AreEqual(1, _wallet.Credits, "paid out exactly once");

            Assert.IsTrue(_bubble.IsShowing, "she answers rather than vanishing");
            Assert.IsFalse(CatalogBookPresenter.IsOpen, "R7: a sale is spoken, never a screen");
            for (int fill = 0; fill < 3 && _bubble.IsFilling; fill++) _bubble.Advance();
            StringAssert.Contains(paid.ToString(), _bubble.VisibleText,
                                  "she counts the money out loud, and the figure is the one that moved");

            // --- and out ------------------------------------------------------------------------
            // ⚠ SHE ENDS IT, not the close row. A sell row is an ANSWERING row, and an answering row is
            // terminal: "a reply never leads to more options" (rule 8, and DialoguePresenter says so at
            // the reply arm). Only the CATALOG row is deferred-terminal, and only because R2 ruled it so.
            // The creek's case takes the other exit - picking "See you later." off a live picker - so
            // both ways out of a conversation are covered.
            for (int press = 0; press < 12 && _bubble.IsShowing; press++) _bubble.Advance();
            Assert.IsFalse(_bubble.IsShowing, "the conversation ends on her answer");
            Assert.IsFalse(InteractionGate.IsBlocked, "and you can walk away");
        }

        /// <summary>An empty pail is a different sentence, and it costs nothing.</summary>
        [UnityTest]
        public IEnumerator StPeters_SellingAnEmptyPail_SaysSo_AndChargesNobody()
        {
            TestHold pail = BuildStPetersCounter();
            Assert.AreEqual(0, pail.UsedUnits);

            WalkUpAndTalk(DialogueNamed("MargueriteFirst"), "npc.marguerite_leblanc");
            yield return Settle();
            PressThroughToTheRows();

            PickRow(1);
            yield return Settle();

            Assert.AreEqual(0, _wallet.Credits, "nothing was paid");
            Assert.IsTrue(_bubble.IsShowing, "she still says something");
            for (int fill = 0; fill < 3 && _bubble.IsFilling; fill++) _bubble.Advance();
            StringAssert.Contains("air", _bubble.VisibleText, "her empty-pail line, not her payout line");
        }

        // =========================================================================================
        //  NINE MILE CREEK — the same walk, one verb shorter
        // =========================================================================================

        [UnityTest]
        public IEnumerator Creek_BrowseThenLeave_AndTheBookHoldsWhatThatStoreActuallySells()
        {
            BuildCreekStore();

            DialogueDef d = DialogueNamed("ClaudetteFirst");
            WalkUpAndTalk(d, "npc.claudette_boudreau");
            yield return Settle();

            PressThroughToTheRows();
            Assert.AreEqual(3, RowsShowing(), "two of hers and the way out — no sell verb at this counter");

            PickRow(0);
            yield return Settle();

            Assert.IsTrue(CatalogBookPresenter.IsOpen, "the chandlery's book did not open");
            Assert.AreEqual(NmcChandlery, _book.SellerId);
            Assert.IsTrue(_bubble.IsAwaitingCatalog);

            CollectionAssert.AreEqual(new[] { "gear.rod" },
                _book.Book.Shelf.Select(r => r.Id).ToArray(),
                "what that store's one vendor already sells, and nothing invented for her");

            int purseBefore = _wallet.Money;
            BuyRow rod = _book.Book.Current;
            Assert.IsTrue(_book.Confirm(), "the rod would not buy");
            Assert.AreEqual(purseBefore - rod.Quote.Price, _wallet.Money);
            Assert.AreEqual(1, _wallet.Debits, "charged exactly once");

            _book.Close();
            yield return Settle();

            Assert.IsTrue(_bubble.IsChoosing, "the picker re-arms at the creek too");
            Assert.AreEqual(3, RowsShowing());
            Assert.AreEqual(0, _bubble.SelectedOption,
                "the re-armed cursor sits on the first row again — which here is BROWSE, so Interact " +
                "would open the book a second time rather than end the conversation");

            // --- and out, by choosing the way out ------------------------------------------------
            PickRow(2);                       // "See you later." — always appended, always last
            yield return Settle();

            Assert.IsFalse(_bubble.IsShowing, "the conversation ends when you pick the way out");
            Assert.IsFalse(InteractionGate.IsBlocked, "and you can walk away");
        }

        // =========================================================================================
        //  The degraded path — a scene that has not been re-banked off the builders
        // =========================================================================================

        /// <summary>
        /// <b>A browse row published into a scene with no book must not wedge the player.</b>
        ///
        /// <para>The hold is released by <c>CatalogClosed</c> and by nothing else, and <c>Advance()</c>
        /// refuses while it is on — so without the guard, a region scene banked before the catalog
        /// runtime existed traps the player in a dimmed bubble with no way out. That is not a
        /// hypothetical: <c>CatalogBookPresenter</c> is added by the region builders and the shipped
        /// scenes predate them.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator ABrowseRow_WithNoBookInTheScene_AnswersAndEnds_RatherThanHanging()
        {
            BuildStPetersCounter();
            Object.DestroyImmediate(_book);       // the region scene that has not been re-banked
            yield return Settle();
            Assert.IsFalse(CatalogBookPresenter.IsOpen, "no book in this scene");

            WalkUpAndTalk(DialogueNamed("MargueriteFirst"), "npc.marguerite_leblanc");
            yield return Settle();
            PressThroughToTheRows();

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(
                "offers .* wares book, but nothing"));
            PickRow(0);
            yield return Settle();

            Assert.IsFalse(_bubble.IsAwaitingCatalog, "it must not hold for a book that is never coming");
            for (int press = 0; press < 12 && _bubble.IsShowing; press++) _bubble.Advance();
            Assert.IsFalse(_bubble.IsShowing, "the conversation ends");
            Assert.IsFalse(InteractionGate.IsBlocked, "and the player can walk away");
        }

        /// <summary>A sell row at a counter that does not exist reads as an empty pail — never a lie
        /// about a payout, and never a hang.</summary>
        [UnityTest]
        public IEnumerator ASellRow_WithNoCounterInTheScene_ReadsAsNothingSold()
        {
            // No counter is built at all: nothing in the loaded scenes carries seller.leblancs.
            WalkUpAndTalk(DialogueNamed("MargueriteFirst"), "npc.marguerite_leblanc");
            yield return Settle();
            PressThroughToTheRows();

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex(
                "was asked to take a catch"));
            PickRow(1);
            yield return Settle();

            Assert.AreEqual(0, _wallet.Credits, "no payout was invented");
            for (int fill = 0; fill < 3 && _bubble.IsFilling; fill++) _bubble.Advance();
            StringAssert.Contains("air", _bubble.VisibleText, "the empty-pail line");
        }

        // ---- helpers -------------------------------------------------------------------------------

        int RowsShowing()
        {
            FieldInfo f = typeof(DialoguePresenter)
                .GetField("_options", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(f, "the presenter's row list moved");
            var rows = (IReadOnlyList<DialogueOption>)f.GetValue(_bubble);
            return rows?.Count ?? 0;
        }

        /// <summary>
        /// Put the book's cursor on the first GEAR row this purse can meet, and return it.
        ///
        /// <para>Found by walking the shelf rather than by index, so it survives a price or sort change.
        /// <b>Gear specifically</b>, because gear is presence-only ownership: bait, ice and pots are
        /// COUNTED stock and always re-buyable, so buying one twice charges twice — correctly — and the
        /// "a second confirm must not charge again" assertion would be false for a reason that is not a
        /// bug.</para>
        /// </summary>
        BuyRow FindAffordableRow()
        {
            for (int guard = 0; guard < 32; guard++)
            {
                BuyRow row = _book.Book.Current;
                if (row.Quote.Kind == BuyRowKind.Gear && row.Quote.CanBuy) return row;
                _book.Book.Move(-1f);
                _book.Book.Move(0f);   // ⚠ the latch: the axis must fall back before it steps again
            }
            Assert.Fail("no gear row on her counter is buyable and affordable with ₲500");
            return default;
        }

        int OwnedCount(string id)
        {
            int n = 0;
            if (_save.OwnedGear != null) n += _save.OwnedGear.Count(g => g == id);
            if (_save.OwnedBoats != null) n += _save.OwnedBoats.Count(b => b == id);
            return n;
        }
    }
}
