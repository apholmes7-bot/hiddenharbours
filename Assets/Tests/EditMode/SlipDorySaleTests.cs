using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Boats;
using HiddenHarbours.Player;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The broken dory on the St Peters slip: a new game has no usable boat, the slip dory is Basil's,
    /// the sale refuses a short purse in plain words, takes the data price once, survives a save round
    /// trip, and an old save sees none of it. Driven through the pure sale and the save seam; no scene.
    /// </summary>
    public class SlipDorySaleTests
    {
        const string SaleAsset   = "Assets/_Project/Data/Regions/StPetersVillage/slipsale.basils_dory.asset";
        const string PrefabAsset = "Assets/_Project/Prefabs/Boats/SlipDory.prefab";
        const string BasilAsset  = "Assets/_Project/Data/NPCs/BasilSamson.asset";

        /// <summary>A save that keeps its blob and flags in memory, counting writes.</summary>
        sealed class MemorySave : ISaveService
        {
            public MemorySave(SaveData data) { Current = data; }
            public SaveData Current { get; private set; }
            public int Writes;
            public bool GetFlag(string key)
            {
                if (Current?.OnboardingFlags == null) return false;
                foreach (var f in Current.OnboardingFlags) if (f.Key == key) return f.Value;
                return false;
            }
            public void SetFlag(string key, bool value)
            {
                Current.OnboardingFlags ??= new List<SaveFlag>();
                for (int i = 0; i < Current.OnboardingFlags.Count; i++)
                    if (Current.OnboardingFlags[i].Key == key) { Current.OnboardingFlags[i] = new SaveFlag(key, value); return; }
                Current.OnboardingFlags.Add(new SaveFlag(key, value));
            }
            public void Save() => Writes++;
            public void BeginNewGame() => Current = SaveMigration.NewGame();
        }

        sealed class Purse : IWallet
        {
            public Purse(int money) { Money = money; }
            public int Money { get; private set; }
            public void Add(int amount) => Money += amount;
            public bool TrySpend(int amount)
            {
                if (amount < 0 || amount > Money) return false;
                Money -= amount;
                return true;
            }
        }

        readonly List<BoatPurchased> _bought = new();
        readonly List<Object> _spawned = new();

        void OnBought(BoatPurchased e) => _bought.Add(e);

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();
            ShellFlow.Reset();
            Interactables.Clear();
            _bought.Clear();
            EventBus.Subscribe<BoatPurchased>(OnBought);
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Unsubscribe<BoatPurchased>(OnBought);
            foreach (var o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
            Interactables.Clear();
            ShellFlow.Reset();
            GameServices.Reset();
        }

        static SlipBoatSaleDef Sale()
        {
            var sale = AssetDatabase.LoadAssetAtPath<SlipBoatSaleDef>(SaleAsset);
            Assert.IsNotNull(sale, SaleAsset + " is missing");
            return sale;
        }

        static MemorySave NewGameOnTheSlip()
        {
            var save = new MemorySave(SaveMigration.NewGame());
            SlipDoryStory.BeginForNewGame(save);
            return save;
        }

        // ---- a new game ----------------------------------------------------------------------

        [Test]
        public void ANewGame_HasNoUsableDory_AndTheSlipDoryIsBasils()
        {
            var save = new MemorySave(SaveMigration.NewGame());
            save.Current.OwnedBoats.Add("boat.stale_from_the_last_game");
            GameServices.Save = save;

            ShellFlow.StartNewGame();   // the title page's New Game, the one path that sets the story

            SlipBoatSaleDef sale = Sale();
            Assert.IsTrue(SlipDoryStory.IsOnTheSlip(save), "a new game must start with the dory on the slip");
            Assert.IsTrue(SlipDoryStory.WithholdsPlayersBoat(save),
                "a new game must keep the sailing boat out of the world: he has no usable boat yet");
            CollectionAssert.DoesNotContain(save.Current.OwnedBoats, sale.BoatId, "he must not own a dory yet");
            CollectionAssert.DoesNotContain(save.Current.RepairedBoats, sale.BoatId, "nor a mended one");
            Assert.IsFalse(SlipBoatSale.IsHis(sale, save));

            var basil = AssetDatabase.LoadAssetAtPath<NpcDef>(BasilAsset);
            Assert.IsNotNull(basil, BasilAsset + " is missing");
            Assert.AreEqual(basil.Id, sale.SellerNpcId, "the slip dory is Basil's to sell");
        }

        [Test]
        public void AContinue_DoesNotPutADoryOnTheSlip()
        {
            var save = new MemorySave(SaveMigration.NewGame());
            GameServices.Save = save;
            ShellFlow.ContinueGame();
            Assert.IsFalse(SlipDoryStory.IsOnTheSlip(save), "only the explicit New Game sets the story");
        }

        // ---- the sale ------------------------------------------------------------------------

        [Test]
        public void TooLittleMoney_Refuses_WithAPlainReason()
        {
            SlipBoatSaleDef sale = Sale();
            var save = NewGameOnTheSlip();
            var purse = new Purse(sale.Price - 1);

            SlipBoatSaleResult result = SlipBoatSale.TryBuy(sale, save, purse);

            Assert.AreEqual(SlipBoatSaleResult.TooDear, result);
            Assert.AreEqual(sale.Price - 1, purse.Money, "a refused sale must spend nothing");
            CollectionAssert.DoesNotContain(save.Current.OwnedBoats, sale.BoatId);
            Assert.IsEmpty(_bought, "a refused sale must announce no purchase");
            Assert.AreEqual(0, save.Writes, "a refused sale must write nothing");

            string reason = SlipBoatSale.LineFor(sale, result);
            Assert.IsFalse(string.IsNullOrWhiteSpace(reason), "the refusal must say why");
            StringAssert.Contains(MoneyFormat.Balance(sale.Price), reason, "the reason must name the price");
            StringAssert.DoesNotContain(SlipBoatSaleDef.PriceToken, reason, "the price token must be written in");
        }

        [Test]
        public void EnoughMoney_DropsThePurseByTheDataPrice_AndSheIsHis_Once()
        {
            SlipBoatSaleDef sale = Sale();
            var save = NewGameOnTheSlip();
            int start = sale.Price + 7;
            var purse = new Purse(start);

            Assert.AreEqual(SlipBoatSaleResult.Bought, SlipBoatSale.TryBuy(sale, save, purse));
            Assert.AreEqual(start - sale.Price, purse.Money, "the purse must drop by exactly the data price");
            Assert.AreEqual(1, CountOf(save.Current.OwnedBoats, sale.BoatId), "she is his, listed once");
            CollectionAssert.DoesNotContain(save.Current.RepairedBoats, sale.BoatId, "she is still broken");
            Assert.AreEqual(1, _bought.Count);
            Assert.AreEqual(sale.BoatId, _bought[0].BoatId);
            Assert.AreEqual(sale.Price, _bought[0].PricePaid);
            Assert.AreEqual(sale.SellerNpcId, _bought[0].Counterparty.MerchantId, "Basil took the money");
            Assert.IsTrue(SlipDoryStory.IsOnTheSlip(save), "bought, she still lies on the slip");
            Assert.IsTrue(SlipDoryStory.WithholdsPlayersBoat(save), "and she cannot be launched yet");

            // The second press does nothing: no spend, no second listing, no second purchase.
            Assert.AreEqual(SlipBoatSaleResult.AlreadyHis, SlipBoatSale.TryBuy(sale, save, purse));
            Assert.AreEqual(start - sale.Price, purse.Money);
            Assert.AreEqual(1, CountOf(save.Current.OwnedBoats, sale.BoatId));
            Assert.AreEqual(1, _bought.Count);
            Assert.IsFalse(string.IsNullOrWhiteSpace(SlipBoatSale.LineFor(sale, SlipBoatSaleResult.AlreadyHis)),
                "pressing on her once she is his says she needs mending");
        }

        [Test]
        public void ThePress_OnTheComponent_BuysThroughTheLiveServices()
        {
            SlipBoatSaleDef sale = Sale();
            var save = NewGameOnTheSlip();
            var purse = new Purse(sale.Price);
            GameServices.Save = save;
            GameServices.Wallet = purse;

            var go = new GameObject("SlipDory"); _spawned.Add(go);
            var body = new GameObject("Body"); body.transform.SetParent(go.transform); body.SetActive(false);
            var dory = go.AddComponent<SlipDory>();
            dory.Configure(sale, body);

            Assert.IsTrue(dory.IsShown && body.activeSelf, "a new game shows her on the slip");
            Assert.IsTrue(dory.IsAvailable);
            StringAssert.Contains(MoneyFormat.Balance(sale.Price), dory.VerbLabel, "the verb shows her price");

            Assert.AreEqual(SlipBoatSaleResult.Bought, dory.Press());
            Assert.AreEqual(0, purse.Money);
            Assert.AreEqual(sale.WithPrice(sale.OwnedLabel), dory.VerbLabel, "once his, the verb changes");
            Assert.AreEqual(SlipBoatSaleResult.AlreadyHis, dory.Press());
            Assert.AreEqual(1, _bought.Count);
        }

        // ---- the save ------------------------------------------------------------------------

        [Test]
        public void TheState_SurvivesAJsonRoundTrip()
        {
            SlipBoatSaleDef sale = Sale();

            // Before the sale: she lies on the slip, unsold.
            var fresh = NewGameOnTheSlip();
            var reread = new MemorySave(SaveSerialization.FromJson(SaveSerialization.ToJson(fresh.Current)));
            Assert.IsTrue(SlipDoryStory.IsOnTheSlip(reread));
            Assert.AreEqual(SlipBoatSaleResult.Bought, SlipBoatSale.Peek(sale, reread, new Purse(sale.Price)));

            // After the sale: his, broken, still on the slip.
            var save = NewGameOnTheSlip();
            Assert.AreEqual(SlipBoatSaleResult.Bought, SlipBoatSale.TryBuy(sale, save, new Purse(sale.Price)));
            var back = new MemorySave(SaveSerialization.FromJson(SaveSerialization.ToJson(save.Current)));
            Assert.IsTrue(SlipDoryStory.IsOnTheSlip(back));
            Assert.IsTrue(SlipDoryStory.WithholdsPlayersBoat(back));
            Assert.IsTrue(SlipBoatSale.IsHis(sale, back));
            CollectionAssert.DoesNotContain(back.Current.RepairedBoats, sale.BoatId);
            Assert.AreEqual(SlipBoatSaleResult.AlreadyHis, SlipBoatSale.TryBuy(sale, back, new Purse(1000)));
            Assert.AreEqual(SaveMigration.CurrentVersion, back.Current.SchemaVersion, "no save step was added");
        }

        [Test]
        public void AnOldSave_KeepsItsBoat_AndSeesNoSecondDoryAndNoSale()
        {
            SlipBoatSaleDef sale = Sale();
            // A version-1 save from before the repair list existed: the dory he already sails.
            string old = "{\"SchemaVersion\":1,\"Money\":500,\"OwnedBoats\":[\"" + sale.BoatId + "\"],\"ActiveHullId\":\"" + sale.BoatId + "\"}";
            var save = new MemorySave(SaveSerialization.FromJson(old));
            Assert.IsNotNull(save.Current);

            Assert.IsFalse(SlipDoryStory.IsOnTheSlip(save), "an old save has no dory on the slip");
            Assert.IsFalse(SlipDoryStory.WithholdsPlayersBoat(save), "an old save keeps its own boat in the world");
            CollectionAssert.AreEqual(new[] { sale.BoatId }, save.Current.OwnedBoats, "its boat is kept exactly");
            CollectionAssert.Contains(save.Current.RepairedBoats, sale.BoatId, "and is still seaworthy");
            Assert.AreEqual(sale.BoatId, save.Current.ActiveHullId);

            var purse = new Purse(1000);
            Assert.AreEqual(SlipBoatSaleResult.NotForSale, SlipBoatSale.TryBuy(sale, save, purse), "no sale");
            Assert.AreEqual(1000, purse.Money);
            Assert.IsEmpty(_bought);

            GameServices.Save = save;
            var go = new GameObject("SlipDory"); _spawned.Add(go);
            var body = new GameObject("Body"); body.transform.SetParent(go.transform);
            var dory = go.AddComponent<SlipDory>();
            dory.Configure(sale, body);
            Assert.IsFalse(body.activeSelf, "an old save sees no second dory");
            Assert.IsFalse(dory.IsAvailable, "and nothing to press");
        }

        [Test]
        public void AHiddenBoat_CannotBeBoarded()
        {
            var playerGo = new GameObject("Player"); _spawned.Add(playerGo);
            playerGo.transform.position = new Vector3(0f, -11.5f, 0f);
            var walk = playerGo.AddComponent<PlayerWalkController>();
            var boatGo = new GameObject("Boat"); _spawned.Add(boatGo);
            boatGo.transform.position = new Vector3(0f, -13.8f, 0f);
            var boat = boatGo.AddComponent<BoatController>();
            var input = boatGo.AddComponent<DevBoatInput>();
            var hull = ScriptableObject.CreateInstance<BoatHullDef>(); _spawned.Add(hull);
            hull.Id = "boat.dory"; hull.CameraWorldHeightMeters = 14f;
            boat.SetHull(hull);
            walk.enabled = true; boat.enabled = false; input.enabled = false;
            var dock = new GameObject("Dock"); _spawned.Add(dock);
            var off = new GameObject("Disembark"); _spawned.Add(off);
            var swGo = new GameObject("Switcher"); _spawned.Add(swGo);
            var sw = swGo.AddComponent<ControlSwitcher>();
            sw.Configure(walk, boat, input, dock.transform, 3f, off.transform);

            Assert.IsTrue(sw.WithinBoardReach(), "fixture: a shown boat at this distance is in reach");
            boatGo.SetActive(false);
            Assert.IsFalse(sw.WithinBoardReach(), "a boat kept out of the world must not be boardable");
        }

        // ---- content -------------------------------------------------------------------------

        /// <summary>The price is the first week's purse arithmetic: a player who digs with care nets about
        /// 36 a day and a careless one about 16, both from 5. The window in which both can buy her on days
        /// three to five is 78 to 85.</summary>
        const int FirstWeekPriceLow = 78;
        const int FirstWeekPriceHigh = 85;

        [Test]
        public void TheSaleAsset_IsValidContent()
        {
            SlipBoatSaleDef sale = Sale();
            StringAssert.IsMatch(@"^[a-z][a-z0-9_]*\.[a-z][a-z0-9_]*$", sale.Id);
            Assert.That(sale.Price, Is.InRange(FirstWeekPriceLow, FirstWeekPriceHigh),
                "the price must sit in the first week's purse window");
            Assert.That(sale.ReachMeters, Is.GreaterThan(0f));

            Assert.IsTrue(HasDef<BoatHullDef>(sale.BoatId, d => d.Id), $"no hull with id {sale.BoatId}");
            Assert.IsTrue(HasDef<NpcDef>(sale.SellerNpcId, d => d.Id), $"no NPC with id {sale.SellerNpcId}");
            Assert.IsTrue(HasDef<RegionDef>(sale.RegionId, d => d.Id), $"no region with id {sale.RegionId}");
            Assert.IsTrue(HasDef<DialogueVoiceDef>(sale.VoiceId, d => d.Id), $"no voice with id {sale.VoiceId}");

            foreach (string text in new[] { sale.BuyLabel, sale.OwnedLabel, sale.TooDearLine, sale.BoughtLine, sale.NeedsMendingLine })
                Assert.IsFalse(string.IsNullOrWhiteSpace(text), "every label and line must be written");
            StringAssert.Contains(SlipBoatSaleDef.PriceToken, sale.BuyLabel, "the verb must show the price");
            StringAssert.Contains(SlipBoatSaleDef.PriceToken, sale.TooDearLine, "the refusal must name the price");
        }

        [Test]
        public void ThePrefab_CarriesTheSale_TheDoryArt_AndStartsHidden()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabAsset);
            Assert.IsNotNull(prefab, PrefabAsset + " is missing");
            var dory = prefab.GetComponent<SlipDory>();
            Assert.IsNotNull(dory);
            Assert.AreSame(Sale(), dory.Sale, "the prefab must carry the slip sale");

            var renderer = prefab.GetComponentInChildren<SpriteRenderer>(true);
            Assert.IsNotNull(renderer, "the prefab must draw a hull");
            Assert.IsNotNull(renderer.sprite, "with the existing dory art");
            Assert.IsFalse(renderer.gameObject.activeSelf, "the body starts hidden: only a game with her shows it");
            Assert.IsNotNull(renderer.GetComponent<Collider2D>(), "she is solid where she lies");
        }

        static bool HasDef<T>(string id, System.Func<T, string> idOf) where T : ScriptableObject
        {
            if (string.IsNullOrEmpty(id)) return false;
            foreach (string guid in AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { "Assets/_Project/Data" }))
            {
                var def = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
                if (def != null && idOf(def) == id) return true;
            }
            return false;
        }

        static int CountOf(List<string> list, string id)
        {
            int n = 0;
            if (list != null) foreach (var s in list) if (s == id) n++;
            return n;
        }
    }
}
