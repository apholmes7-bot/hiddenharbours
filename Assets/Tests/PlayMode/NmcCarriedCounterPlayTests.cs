#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HiddenHarbours.App;
using HiddenHarbours.Core;
using HiddenHarbours.Economy;
using HiddenHarbours.Fishing;
using HiddenHarbours.Player;
using HiddenHarbours.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// <b>THE DELIVERED PAIL</b> (owner ruling 2026-11-11, "pail first"). In the committed Nine Mile Creek scene the
    /// player lifts the creek's pail, walks it up to Wendell at the wharf market, and his sell row buys what is in it
    /// at the counter's own market price: the wallet rises by exactly that, the pail is empty and still in her hands,
    /// and the dory's hold is never read. With empty hands the same row sells nothing and the dory's hold is still
    /// untouched: catch reaches the counter in hand or not at all.
    ///
    /// <para>The dory's hold is a stand-in bound to the counter's own hold provider, and the wallet is a stand-in on
    /// <c>GameServices.Wallet</c> (what the counter's wallet provider reads), both restored after: nothing here can
    /// reach the save.</para>
    /// </summary>
    public class NmcCarriedCounterPlayTests
    {
        const string Wendell = "WendellArsenault";
        const string CounterName = "NmcWharfMarketCounter";
        const string SellerId = "seller.nmc_wharf_market";
        const string ClamPath = "Assets/_Project/Data/Fish/SoftShellClam.asset";
        const int Clams = 6, DoryCatch = 4;

        /// <summary>Distances are the player's transform to Wendell, which is what the interactor measures.</summary>
        const float Standoff = 2.6f, Arrived = 1.5f, Overshoot = 0.6f;

        WharfNightStage stage;
        PlayerWalkController player;
        HeldWalkIntents held;
        IWallet previousWallet;
        bool walletSwapped;

        IEnumerator Load()
        {
            Assert.That(System.Environment.GetEnvironmentVariable(SaveStore.TestRunSaveVariable), Is.Not.Null.And.Not.Empty,
                "the run must save under its own path, never the shared save");
            stage = new WharfNightStage("NineMileCreek", "NmcCarriedCounter");
            yield return stage.Load();
            if (ShellFlow.WorldInputBlocked) ShellFlow.Reset();
            InteractionGate.Reset();
            player = Object.FindAnyObjectByType<PlayerWalkController>();
            Assert.That(player, Is.Not.Null, "the creek has a player on foot");
            previousWallet = GameServices.Wallet;
            GameServices.Wallet = new TestWallet(); walletSwapped = true;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            held?.Release();
            if (player != null) player.ConfigureWalkInput(null);
            Time.timeScale = 1f;
            if (walletSwapped) GameServices.Wallet = previousWallet;
            if (stage != null) yield return stage.TearDown();
            stage = null; player = null; held = null; previousWallet = null; walletSwapped = false;
        }

        [UnityTest]
        public IEnumerator CarryThePailToWendell_HeBuysIt_AtTheCountersPrice_AndThePailStaysInHand_Empty()
        {
            yield return Load();
            Sale sale = Prepare();

            // ---- lift the creek's pail: one press on it, as the player does -----------------------------------
            CarriableBucket pail = Object.FindObjectsByType<CarriableBucket>(FindObjectsInactive.Exclude)
                .SingleOrDefault(b => b.Id == "container.nine_mile_creek.bucket");
            Assert.That(pail, Is.Not.Null, "the creek has its pail");
            player.GetComponent<Rigidbody2D>().position = (Vector2)pail.transform.position + Vector2.down * 0.8f;
            Physics2D.SyncTransforms(); yield return null;
            pail.Interact(default);
            yield return null;
            Assert.That(CarriedItem.IsHeld(GameServices.Hands, pail), Is.True, "the press lifts the pail into her hands");

            var clam = UnityEditor.AssetDatabase.LoadAssetAtPath<FishSpeciesDef>(ClamPath);
            Assert.That(clam, Is.Not.Null, $"the clam species at {ClamPath}");
            for (int i = 0; i < Clams; i++)
                Assert.That(pail.TryAdd(new CatchItem(clam.Id, clam.DisplayName, clam.Category, 0.05f, clam.BaseValue,
                    clam.SupplyElasticity)), Is.True, $"the pail takes clam {i + 1}");
            int expected = SellService.Quote(pail, sale.Market, clam.Id, Clams);
            Assert.That(expected, Is.GreaterThan(0), "a pail of clams is worth something at the creek");

            yield return WalkUpAndSell(sale);

            Assert.That(sale.Report.HasValue, Is.True, "the counter answered the sell row");
            TestContext.WriteLine($"sold {sale.Report.Value.UnitsSold} for ₲{sale.Report.Value.Payout} (quoted ₲{expected}); " +
                                  $"wallet ₲{sale.WalletBefore} -> ₲{sale.Wallet.Money}");
            Assert.That(sale.Report.Value.UnitsSold, Is.EqualTo(Clams), "every clam in the pail was bought");
            Assert.That(sale.Report.Value.Payout, Is.EqualTo(expected), "he pays the counter's own market price");
            Assert.That(sale.Wallet.Money - sale.WalletBefore, Is.EqualTo(expected), "the wallet rises by exactly the price");
            Assert.That(pail.UsedUnits, Is.EqualTo(0), "the pail is empty");
            Assert.That(CarriedItem.IsHeld(GameServices.Hands, pail), Is.True, "the empty pail stays in her hands");
            AssertDoryUntouched(sale);
        }

        [UnityTest]
        public IEnumerator EmptyHandsAtWendell_SellNothing_AndTheDorysHoldIsNeverTouched()
        {
            yield return Load();
            Sale sale = Prepare();
            CarriedItem.Both(GameServices.Hands, out ICarriable first, out ICarriable second);
            Assert.That(first == null && second == null, Is.True, "she starts with empty hands");

            yield return WalkUpAndSell(sale);

            Assert.That(sale.Report.HasValue, Is.True, "the counter answered the sell row");
            Assert.That(sale.Report.Value.UnitsSold, Is.EqualTo(0), "nothing was sold");
            Assert.That(sale.Report.Value.Payout, Is.EqualTo(0), "nothing was paid");
            Assert.That(sale.Wallet.Money, Is.EqualTo(sale.WalletBefore), "the wallet is unchanged");
            AssertDoryUntouched(sale);
        }

        // ---- the walk-up and the row ------------------------------------------------------------------------------

        IEnumerator WalkUpAndSell(Sale sale)
        {
            Interactable wendell = Object.FindObjectsByType<Interactable>(FindObjectsInactive.Exclude)
                .SingleOrDefault(i => i.gameObject.name == Wendell);
            Assert.That(wendell, Is.Not.Null, $"{Wendell} carries a live Interactable");
            var interactor = Object.FindAnyObjectByType<WorldInteractor>();
            var bubble = Object.FindAnyObjectByType<DialoguePresenter>();
            Assert.That(interactor != null && bubble != null, Is.True, "the creek has a dialogue driver and a bubble");

            var body = player.GetComponent<Rigidbody2D>();
            var foot = player.GetComponent<CircleCollider2D>();
            Vector2 at = wendell.transform.position;
            Vector2 toward = ClearApproach(at, wendell.transform, foot.offset, foot.radius + 0.05f);
            body.position = at - toward * Standoff; body.linearVelocity = Vector2.zero;
            Physics2D.SyncTransforms(); yield return null;
            Time.timeScale = 4f;
            held = new HeldWalkIntents(); player.ConfigureWalkInput(held);
            int steps = 0, still = 0;
            Vector2 last = body.position;
            while (Vector2.Distance(body.position, at) > Arrived && steps++ < 900 && still < 30)
            {
                held.Walk(toward); yield return new WaitForFixedUpdate();
                still = (body.position - last).sqrMagnitude < 1e-6f ? still + 1 : 0;
                last = body.position;
            }
            held.Release(); Time.timeScale = 1f;
            for (int i = 0; i < 3; i++) yield return null;
            float d = Vector2.Distance(player.transform.position, at);
            TestContext.WriteLine($"walked {steps} steps toward ({toward.x:0}, {toward.y:0}); {d:0.###} m from {Wendell}");
            Assert.That(interactor.Nearest, Is.SameAs(wendell), $"the interactor offers {Wendell} once the walk arrives ({d:0.###} m)");
            Assert.That(InteractActionClaim.IsClaimed, Is.True, "the conversation claims the press, so it never sets the pail down");

            Assert.That(interactor.BeginInteract(), Is.True, "the press is spent on Wendell");
            yield return null;
            Assert.That(bubble.IsShowing, Is.True, "the conversation is up");
            for (int press = 0; press < 12 && !bubble.IsChoosing; press++) { interactor.BeginInteract(); yield return null; }
            Assert.That(bubble.IsChoosing, Is.True, "the lines reach the rows");
            int row = bubble.Options.ToList().FindIndex(o => o.SellAtSellerId == SellerId);
            Assert.That(row, Is.GreaterThanOrEqualTo(0), $"a row sells at {SellerId}");
            for (int guard = 0; guard < 16 && bubble.SelectedOption != row; guard++)
            {
                bubble.MoveSelection(-1f); bubble.MoveSelection(0f);
            }
            Assert.That(bubble.SelectedOption, Is.EqualTo(row), "the cursor reaches the sell row");

            void Capture(CounterSellReported r) { if (r.SellerId == SellerId) sale.Report = r; }
            EventBus.Subscribe<CounterSellReported>(Capture);
            try { interactor.BeginInteract(); yield return null; yield return null; }
            finally { EventBus.Unsubscribe<CounterSellReported>(Capture); }
            Assert.That(bubble.IsShowing, Is.True, "Wendell answers the row in the bubble");
        }

        // ---- the counter, the dory's hold, the wallet ---------------------------------------------------------------

        sealed class Sale
        {
            public Market Market;
            public TestHold Dory;
            public int DoryBefore;
            public IWallet Wallet;
            public int WalletBefore;
            public CounterSellReported? Report;
        }

        /// <summary>The counter as the scene wired it: its market, its hold provider bound to a dory's hold with catch
        /// in it, its wallet provider reading the stand-in wallet.</summary>
        Sale Prepare()
        {
            WharfSellPoint counter = Object.FindObjectsByType<WharfSellPoint>(FindObjectsInactive.Include)
                .SingleOrDefault(p => p.gameObject.name == CounterName);
            Assert.That(counter, Is.Not.Null, $"the creek has {CounterName}");
            Assert.That(counter.SellsWhatIsCarried, Is.True, "Wendell's counter sells what is carried");
            Assert.That(counter.BuyOnlySellerId, Is.EqualTo(SellerId), "under the id his sell row names");

            var buyer = Field<FishBuyer>(counter, "_buyer");
            var market = Field<Market>(buyer, "_market");
            Assert.That(market, Is.Not.Null, "the counter's buyer prices at a market");

            var proxy = Field<GameObject>(counter, "_holdProvider").GetComponent<PersistentHoldProxy>();
            Assert.That(proxy, Is.Not.Null, "the counter still names the dory's hold provider");
            var dory = new TestHold(20);
            for (int i = 0; i < DoryCatch; i++)
                dory.TryAdd(new CatchItem("fish.soft_shell_clam", "Clam", FishCategory.Shellfish, 0.05f, 9, 0.3f));
            proxy.Bind(dory);

            IWallet wallet = Field<GameObject>(counter, "_walletProvider").GetComponent<IWallet>();
            Assert.That(wallet, Is.Not.Null, "the counter's wallet provider is a wallet");
            wallet.Add(50);
            return new Sale { Market = market, Dory = dory, DoryBefore = dory.UsedUnits, Wallet = wallet, WalletBefore = wallet.Money };
        }

        static void AssertDoryUntouched(Sale sale)
        {
            Assert.That(sale.Dory.UsedUnits, Is.EqualTo(sale.DoryBefore), "the dory's hold keeps its catch");
            Assert.That(sale.Dory.Clears, Is.EqualTo(0), "the dory's hold was never cleared");
        }

        static T Field<T>(object owner, string name) where T : class
        {
            FieldInfo f = owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(f, Is.Not.Null, $"{owner.GetType().Name}.{name}");
            return f.GetValue(owner) as T;
        }

        /// <summary>The first side (south, north, west, east) from which a body-wide walk to Wendell is clear of walls
        /// for the whole standoff, overshoot included (the vendor-anchor walk, unchanged).</summary>
        Vector2 ClearApproach(Vector2 at, Transform target, Vector2 footOffset, float radius)
        {
            foreach (Vector2 toward in new[] { Vector2.up, Vector2.down, Vector2.right, Vector2.left })
            {
                bool clear = true;
                for (float s = Arrived - Overshoot; s <= Standoff && clear; s += 0.1f)
                    clear = !Physics2D.OverlapCircleAll(at - toward * s + footOffset, radius)
                        .Any(c => !c.isTrigger && !c.transform.IsChildOf(player.transform) && !c.transform.IsChildOf(target));
                if (clear) return toward;
            }
            Assert.Fail($"no side of ({at.x:0.##}, {at.y:0.##}) has a clear {Standoff} m approach");
            return Vector2.zero;
        }

        sealed class TestHold : IHold
        {
            readonly List<CatchItem> _items = new List<CatchItem>();
            public TestHold(int capacity) => CapacityUnits = capacity;
            public int Clears { get; private set; }
            public int CapacityUnits { get; }
            public int UsedUnits => _items.Count;
            public IReadOnlyList<CatchItem> Items => _items;
            public bool TryAdd(CatchItem item) { if (_items.Count >= CapacityUnits) return false; _items.Add(item); return true; }
            public void Clear() { Clears++; _items.Clear(); }
        }

        sealed class TestWallet : IWallet
        {
            public int Money { get; private set; }
            public void Add(int amount) => Money += amount;
            public bool TrySpend(int amount) { if (amount > Money) return false; Money -= amount; return true; }
        }
    }
}
#endif
