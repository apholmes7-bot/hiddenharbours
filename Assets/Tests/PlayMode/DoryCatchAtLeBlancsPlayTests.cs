using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using HiddenHarbours.App;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using HiddenHarbours.Economy;
using HiddenHarbours.Fishing;
using HiddenHarbours.Player;
using HiddenHarbours.World;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// <b>THE REAL DORY, THE REAL COUNTER, THE REAL ROW.</b> The EditMode cases
    /// (<c>DoryCatchAtTheCounterTests</c>) prove the rule on built objects. These two load the shipped
    /// St Peters scene and ask the question the rule exists for: does a fish in the starter dory's hold
    /// sell at LeBlanc's when she lies where the game leaves her?
    ///
    /// <para><b>What is real.</b> The scene, the dory and her <see cref="ShipHold"/>, the wharf's
    /// fittings in <see cref="MooringCleats"/>, the store counter's <see cref="WharfSellPoint"/> and
    /// <see cref="Market"/>, the player's pail and wallet, the bubble, the clerk's shipped
    /// <see cref="DialogueDef"/>, the owner's reach in the shipped <see cref="GameConfig"/>, and the fish
    /// (built from the shipped species assets, so no price is written here).</para>
    ///
    /// <para><b>What is not.</b> No key is pressed (headless input cannot deliver one): the row is taken
    /// through the seams a press calls, exactly as <c>StoreClerkJourneyPlayTests</c> does. The player is
    /// PLACED at the counter rather than walked there in the first case; the boat is never placed at
    /// all. The save is an in-memory one that has already made landfall, so the arrival opening is not
    /// replayed and no save file is read or written.</para>
    ///
    /// <para><b>⚠ Needs a graphics device.</b> Every fixture that loads a region scene in this suite
    /// stands down on the null device CI runs with, and so does this one: it says SKIPPED, NOT RUN,
    /// rather than risk the whole PlayMode run on a scene load nobody has proven there.</para>
    /// </summary>
    public class DoryCatchAtLeBlancsPlayTests
    {
        const string Region = "StPeters";
        const string DoryName = "Dory";
        const string CounterName = "GeneralStoreCounter";
        const string ClerkName = "MargueriteLeBlanc";
        const string ClerkId = "npc.marguerite_leblanc";
        const string ClerkDialogue = "Assets/_Project/Data/NPCs/Dialogue/MargueriteFirst.asset";
        const string CodAsset = "Assets/_Project/Data/Fish/AtlanticCod.asset";
        const string ClamAsset = "Assets/_Project/Data/Fish/SoftShellClam.asset";

        /// <summary>The clerk's sell row, by position — the same row <c>StoreClerkJourneyPlayTests</c>
        /// picks. Re-authoring her rows breaks this on purpose.</summary>
        const int SellRow = 1;

        const int FishInTheBoat = 3;
        const int ClamsInThePail = 2;

        /// <summary>Harness: how near the counter counts as standing at it.</summary>
        const float AtTheCounterMetres = 1.5f;

        /// <summary>Harness: fixed steps the scene is given to come to rest before anything is read.</summary>
        const int SettleFixedSteps = 25;

        sealed class MemorySave : ISaveService
        {
            readonly Dictionary<string, bool> _flags = new Dictionary<string, bool>();
            public SaveData Current { get; } = new SaveData();
            public bool GetFlag(string key) => _flags.TryGetValue(key, out bool value) && value;
            public void SetFlag(string key, bool value) => _flags[key] = value;
            public void Save() { }
        }

        ISaveService _previousSave;
        MemorySave _save;

        readonly List<CatchSold> _sold = new List<CatchSold>();
        readonly List<CounterSellReported> _reported = new List<CounterSellReported>();
        int _leftAboardNotices;

        // the scene's own objects, resolved after the load
        ShipHold _boatHold;
        BoatMooring _mooring;
        Rigidbody2D _boatBody;
        WharfSellPoint _counter;
        Market _market;
        DialoguePresenter _bubble;
        Transform _player;
        IHold _pail;
        IWallet _wallet;

        void OnSold(CatchSold e) => _sold.Add(e);
        void OnReported(CounterSellReported e) => _reported.Add(e);
        void OnNotice(DevNotice e) { if (e.Text == BerthedCatch.NoticeBoatNotBerthed) _leftAboardNotices++; }

        [SetUp]
        public void SetUp()
        {
            _previousSave = GameServices.Save;
            _sold.Clear();
            _reported.Clear();
            _leftAboardNotices = 0;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            EventBus.Unsubscribe<CatchSold>(OnSold);
            EventBus.Unsubscribe<CounterSellReported>(OnReported);
            EventBus.Unsubscribe<DevNotice>(OnNotice);

            ShellRestart.DestroyPersistentCore();
            yield return null;
            var empty = SceneManager.GetSceneByName("AfterTheDorysCatch");
            if (!empty.IsValid() || !empty.isLoaded) empty = SceneManager.CreateScene("AfterTheDorysCatch");
            SceneManager.SetActiveScene(empty);
            var region = SceneManager.GetSceneByName(Region);
            if (region.IsValid() && region.isLoaded) yield return SceneManager.UnloadSceneAsync(region);

            GameServices.Reset();
            ShellFlow.Reset();
            InteractionGate.Reset();
            GameServices.Save = _previousSave;
        }

        // ---- the load ---------------------------------------------------------------------------

        IEnumerator LoadStPetersWithAFishInTheBoat()
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                Assert.Ignore("SKIPPED, NOT RUN: loading the St Peters scene needs a graphics device.");

            ShellRestart.DestroyPersistentCore();
            GameServices.Reset();
            ShellFlow.Reset();
            InteractionGate.Reset();
            _save = new MemorySave();
            _save.SetFlag(ArrivalOpening.ArrivedFlagKey, true);
            GameServices.Save = _save;

            yield return SceneManager.LoadSceneAsync(Region, LoadSceneMode.Single);
            yield return null;
            yield return null;
            ShellFlow.ContinueGame();
            yield return null;

            Assert.AreSame(_save, GameServices.Save,
                "harness: the scene replaced the in-memory save, so this fixture would touch a save file");

            // The desk is installed after every scene load in the game; a neighbouring fixture may have
            // stood it down, so stand it up the way the game does.
            CounterSellDesk.Install();

            var wait = new WaitForFixedUpdate();
            for (int i = 0; i < SettleFixedSteps; i++) yield return wait;

            ShipHold[] dories = Object.FindObjectsByType<ShipHold>(FindObjectsSortMode.None)
                .Where(h => h.gameObject.name == DoryName).ToArray();
            Assert.AreEqual(1, dories.Length, $"the scene must carry exactly one '{DoryName}' with a hold");
            _boatHold = dories[0];
            _mooring = _boatHold.GetComponent<BoatMooring>();
            _boatBody = _boatHold.GetComponent<Rigidbody2D>();
            Assert.IsNotNull(_mooring, "the dory carries her painter");
            Assert.IsNotNull(_boatBody, "the dory is a body");
            Assert.Contains(_boatHold, BoatHolds.Active.ToList(),
                "the real dory's hold is in the registry the counter asks");

            GameObject counterGo = GameObject.Find(CounterName);
            Assert.IsNotNull(counterGo, $"the scene must carry '{CounterName}'");
            _counter = counterGo.GetComponent<WharfSellPoint>();
            _market = counterGo.GetComponent<Market>();
            Assert.IsNotNull(_counter, "the store counter sells");
            Assert.IsNotNull(_market, "the store counter has its market");

            _bubble = Object.FindAnyObjectByType<DialoguePresenter>();
            Assert.IsNotNull(_bubble, "the scene carries the bubble");

            _player = GameServices.PlayerTransform;
            _pail = GameServices.PlayerHold;
            _wallet = GameServices.Wallet;
            Assert.IsNotNull(_player, "the player is in the world");
            Assert.IsNotNull(_pail, "the player's pail is published");
            Assert.IsNotNull(_wallet, "the player's wallet is published");
            Assert.IsNotNull(GameServices.Config, "the shipped config is live, so the reach is the owner's number");

            // ---- the catch: built from the shipped species, added through each hold's own TryAdd ----
            FishSpeciesDef cod = Species(CodAsset);
            for (int i = 0; i < FishInTheBoat; i++)
                Assert.IsTrue(_boatHold.TryAdd(Landed(cod)), "the dory's hold takes a cod");
            Assert.AreEqual(FishInTheBoat, _boatHold.UsedUnits);

            EventBus.Subscribe<CatchSold>(OnSold);
            EventBus.Subscribe<CounterSellReported>(OnReported);
            EventBus.Subscribe<DevNotice>(OnNotice);
        }

        /// <summary>A fresh player owns no pail (its capacity is zero until the bucket is bought), so the
        /// pail half of a sale needs the bucket granted the way the shop grants it: its id in the save.</summary>
        void FillThePail()
        {
            if (!_save.Current.OwnedGear.Contains(PlayerGear.BucketId))
                _save.Current.OwnedGear.Add(PlayerGear.BucketId);
            FishSpeciesDef clam = Species(ClamAsset);
            for (int i = 0; i < ClamsInThePail; i++)
                Assert.IsTrue(_pail.TryAdd(Landed(clam)), "the pail takes a clam once the bucket is owned");
            Assert.AreEqual(ClamsInThePail, _pail.UsedUnits);
        }

        static FishSpeciesDef Species(string path)
        {
            var def = AssetDatabase.LoadAssetAtPath<FishSpeciesDef>(path);
            Assert.IsNotNull(def, $"missing shipped species: {path}");
            return def;
        }

        static CatchItem Landed(FishSpeciesDef fish)
        {
            double now = GameServices.Clock != null ? GameServices.Clock.TotalSeconds : 0d;
            return new CatchItem(fish.Id, fish.DisplayName, fish.Category,
                                 (fish.MinWeightKg + fish.MaxWeightKg) * 0.5f, fish.BaseValue,
                                 fish.SupplyElasticity, fish.SpoilPerDay, Freshness.Landed(now));
        }

        // ---- where she lies ---------------------------------------------------------------------

        /// <summary>Her root, the nearest shore fitting in the whole registry, and how far it is.</summary>
        string WhereSheLies(out float metresToFitting, out bool berthed)
        {
            Vector2 at = _boatBody.position;
            float reach = BerthedCatch.ReachMeters;
            bool any = MooringCleats.TryFindNearest(MooringCleats.Active, at, CleatSide.Shore,
                                                    float.MaxValue, out IMooringCleat nearest);
            metresToFitting = any ? Vector2.Distance(at, nearest.WorldPosition) : float.PositiveInfinity;
            berthed = BerthedCatch.IsBerthed(at, MooringCleats.Active, reach);
            return $"dory root ({at.x:F2}, {at.y:F2}), painter {_mooring.State}, nearest shore fitting " +
                   (any ? $"'{nearest.Id}' at ({nearest.WorldPosition.x:F2}, {nearest.WorldPosition.y:F2}), " +
                          $"{metresToFitting:F2} m off"
                        : "NONE REGISTERED") +
                   $", CounterBerthReachMeters {reach:F2}, berthed {berthed}";
        }

        void Place(Vector2 at)
        {
            var body = _player.GetComponent<Rigidbody2D>();
            _player.position = new Vector3(at.x, at.y, _player.position.z);
            if (body != null)
            {
                body.position = at;
                body.linearVelocity = Vector2.zero;
            }
        }

        // ---- the row ----------------------------------------------------------------------------

        static IEnumerator Settle() { yield return null; yield return null; }

        /// <summary>Walk up, talk, and take the sell row: the request <c>WorldInteractor</c> builds from
        /// the shipped asset, then the seams a press calls.</summary>
        IEnumerator TakeTheSellRow()
        {
            var dialogue = AssetDatabase.LoadAssetAtPath<DialogueDef>(ClerkDialogue);
            Assert.IsNotNull(dialogue, $"missing shipped dialogue: {ClerkDialogue}");
            GameObject clerk = GameObject.Find(ClerkName);
            Transform speaker = clerk != null ? clerk.transform : _counter.transform;

            _bubble.Play(new DialogueRequest(
                dialogue.Lines(metBefore: false).Select(l => new DialogueLine("Clerk", l)).ToList(),
                speaker, new Vector3(0f, 2.1f, 0f), DialogueVoice.Default,
                DialogueOptionPicker.RowsFor(dialogue.Options), dialogue.Id, ClerkId));
            yield return Settle();

            for (int press = 0; press < 12 && !_bubble.IsChoosing; press++) _bubble.Advance();
            Assert.IsTrue(_bubble.IsChoosing, "her lines never reached the picker");

            for (int guard = 0; guard < 16 && _bubble.SelectedOption != SellRow; guard++)
            {
                _bubble.MoveSelection(-1f);
                _bubble.MoveSelection(0f);   // the latch: the axis must fall back before it steps again
            }
            Assert.AreEqual(SellRow, _bubble.SelectedOption, "the cursor never reached the sell row");
            _bubble.Advance();
            yield return Settle();
        }

        IEnumerator LeaveTheConversation()
        {
            for (int press = 0; press < 12 && _bubble.IsShowing; press++) _bubble.Advance();
            yield return null;
        }

        // =========================================================================================
        //  1. She lies where the game leaves her, and her catch sells
        // =========================================================================================

        [UnityTest, Timeout(180000)]
        public IEnumerator TheDorysCatch_SellsAtLeBlancs_WhenSheLiesAtTheWharf()
        {
            yield return LoadStPetersWithAFishInTheBoat();
            FillThePail();

            string lies = WhereSheLies(out float metresToFitting, out bool berthed);
            Debug.Log($"[DoryCatch] AT HER START BERTH: {lies}");
            Assert.IsTrue(berthed,
                "the starter dory, left exactly where a fresh game leaves her, is not within the owner's " +
                $"reach of any shore fitting, so her catch cannot be landed from her own berth. {lies}");

            Place((Vector2)_counter.transform.position + Vector2.down);
            yield return Settle();

            // The pricing's own figures, asked before the sale. The clam and the cod are different
            // categories, so neither lot moves the other's price and each quote stands alone: the
            // pail's is, by construction, what the pail pays with no boat in the world.
            string clamId = _pail.Items[0].SpeciesId, codId = _boatHold.Items[0].SpeciesId;
            int pailQuote = SellService.Quote(_pail, _market, clamId, ClamsInThePail);
            int boatQuote = SellService.Quote(_boatHold, _market, codId, FishInTheBoat);
            Assert.AreNotEqual(_pail.Items[0].Category, _boatHold.Items[0].Category,
                "premise: two categories, so the two quotes are independent");
            Assert.Greater(boatQuote, 0, "premise: the store pays something for a cod");
            int before = _wallet.Money;

            yield return TakeTheSellRow();

            Debug.Log($"[DoryCatch] THE SALE: pail quote {pailQuote} for {ClamsInThePail}, boat quote " +
                      $"{boatQuote} for {FishInTheBoat}, coins {before} -> {_wallet.Money}, CatchSold " +
                      $"events {_sold.Count} [{string.Join(", ", _sold.Select(s => $"{s.TotalPaid} for {s.Count}"))}], " +
                      $"left-aboard notices {_leftAboardNotices}. {lies}");

            Assert.AreEqual(pailQuote + boatQuote, _wallet.Money - before,
                $"coins rose by the pricing's own figure for the pail plus the hold. {lies}");
            Assert.AreEqual(0, _boatHold.UsedUnits, "the dory's hold is empty");
            Assert.AreEqual(0, _pail.UsedUnits, "the pail is empty");
            Assert.AreEqual(0, _leftAboardNotices, "nothing was left aboard, so nothing says so");

            Assert.AreEqual(2, _sold.Count, "one sale for the pail, then one for the boat");
            Assert.AreEqual(pailQuote, _sold[0].TotalPaid, "the pail paid what it pays with no boat");
            Assert.AreEqual(ClamsInThePail, _sold[0].Count);
            Assert.AreEqual(boatQuote, _sold[1].TotalPaid, "the boat's lot paid its own quote");
            Assert.AreEqual(FishInTheBoat, _sold[1].Count);

            Assert.AreEqual(1, _reported.Count, "the counter answered the row once");
            Assert.AreEqual(pailQuote + boatQuote, _reported[0].Payout, "and reported the two together");
            Assert.AreEqual(ClamsInThePail + FishInTheBoat, _reported[0].UnitsSold);

            for (int fill = 0; fill < 3 && _bubble.IsFilling; fill++) _bubble.Advance();
            StringAssert.Contains((pailQuote + boatQuote).ToString(), _bubble.VisibleText,
                "the clerk says the figure that moved");
            yield return LeaveTheConversation();
        }

        // =========================================================================================
        //  2. The painter in hand, all the way to the store
        // =========================================================================================

        /// <summary>
        /// The first-hour case: step off with the painter in hand, never make fast, walk to the store.
        ///
        /// <para>The step ashore is the two things <c>ControlSwitcher.Disembark</c> does to the boat
        /// (her controller off, <see cref="BoatMooring.Hold"/> on the player), from the planks beside
        /// her and inside the rope so nothing snatches. The walk is a straight line at the walk
        /// controller's own speed with no pathfinding: the painter's limit is a positional clamp to the
        /// hand, so the route does not change where she ends up relative to the player.</para>
        ///
        /// <para><b>It asserts the law, not an outcome.</b> Wherever she ends up, the counter must obey
        /// it: within reach of a fitting her catch sells, out of reach it stays aboard and the notice
        /// says so. Which of the two happens, and at what distance, is logged for the owner; nothing
        /// here tunes the painter or the reach to choose it.</para>
        /// </summary>
        [UnityTest, Timeout(300000)]
        public IEnumerator ThePainterInHand_WalkedToTheStore_TheCounterObeysWhereSheEndsUp()
        {
            yield return LoadStPetersWithAFishInTheBoat();
            FillThePail();

            string atBerth = WhereSheLies(out float berthMetres, out _);
            Vector2 boatAt = _boatBody.position;
            Assert.IsTrue(MooringCleats.TryFindNearest(MooringCleats.Active, boatAt, CleatSide.Shore,
                                                       float.MaxValue, out IMooringCleat fitting),
                "the wharf has a shore fitting to step toward");

            // Onto the planks beside her, half way to her nearest fitting: inside the rope.
            Place(Vector2.Lerp(boatAt, fitting.WorldPosition, 0.5f));
            var controller = _boatHold.GetComponent<BoatController>();
            if (controller != null) controller.enabled = false;
            _mooring.Hold(_player);
            Assert.IsTrue(_mooring.IsHeld, "the painter is in the player's hand");
            yield return Settle();

            float speed = WalkSpeed();
            Vector2 counterAt = _counter.transform.position;
            float walk = Vector2.Distance(_player.position, counterAt);
            int guard = Mathf.CeilToInt(walk / (speed * Time.fixedDeltaTime)) + 500;
            var wait = new WaitForFixedUpdate();
            for (int i = 0; i < guard && Vector2.Distance(_player.position, counterAt) > AtTheCounterMetres; i++)
            {
                Place(Vector2.MoveTowards(_player.position, counterAt, speed * Time.fixedDeltaTime));
                yield return wait;
            }
            Assert.LessOrEqual(Vector2.Distance(_player.position, counterAt), AtTheCounterMetres + 0.01f,
                "harness: the player reached the counter");
            for (int i = 0; i < SettleFixedSteps; i++) yield return wait;

            string atStore = WhereSheLies(out float storeMetres, out bool berthed);
            float toPlayer = Vector2.Distance(_boatBody.position, _player.position);
            Debug.Log($"[DoryCatch] PAINTER IN HAND, PLAYER AT THE COUNTER after {walk:F1} m at {speed:F1} m/s: " +
                      $"{atStore}; dory to player {toPlayer:F2} m. She started: {atBerth}");

            int pailQuote = SellService.Quote(_pail, _market, _pail.Items[0].SpeciesId, ClamsInThePail);
            int boatQuote = SellService.Quote(_boatHold, _market, _boatHold.Items[0].SpeciesId, FishInTheBoat);
            int before = _wallet.Money;

            yield return TakeTheSellRow();

            Debug.Log($"[DoryCatch] PAINTER IN HAND, THE SALE: berthed {berthed}, coins {before} -> " +
                      $"{_wallet.Money} (pail quote {pailQuote}, boat quote {boatQuote}), hold " +
                      $"{_boatHold.UsedUnits}/{FishInTheBoat}, CatchSold events {_sold.Count}, " +
                      $"left-aboard notices {_leftAboardNotices}");

            Assert.AreEqual(0, _pail.UsedUnits, "the pail sells either way");
            if (berthed)
            {
                Assert.AreEqual(pailQuote + boatQuote, _wallet.Money - before, $"in reach: both lots. {atStore}");
                Assert.AreEqual(0, _boatHold.UsedUnits, "in reach: her hold is empty");
                Assert.AreEqual(0, _leftAboardNotices, "in reach: nothing to say");
            }
            else
            {
                Assert.AreEqual(pailQuote, _wallet.Money - before, $"out of reach: the pail only. {atStore}");
                Assert.AreEqual(FishInTheBoat, _boatHold.UsedUnits, "out of reach: her catch stays aboard");
                Assert.AreEqual(1, _leftAboardNotices, "out of reach: the player is told, once");
                Assert.AreEqual(1, _sold.Count, "out of reach: one sale, the pail's");
            }
            yield return LeaveTheConversation();
        }

        /// <summary>The walk controller's own walking speed (m/s), read off the scene's player so the
        /// harness leads her at the pace the game walks.</summary>
        float WalkSpeed()
        {
            var walker = _player.GetComponent<PlayerWalkController>();
            Assert.IsNotNull(walker, "the player walks");
            FieldInfo field = typeof(PlayerWalkController).GetField("_moveSpeed",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, "harness: PlayerWalkController no longer has a _moveSpeed to read");
            float speed = (float)field.GetValue(walker);
            Assert.Greater(speed, 0f, "harness: a walking speed of zero never arrives");
            return speed;
        }
    }
}
