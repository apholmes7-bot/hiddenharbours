using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.Core;
using HiddenHarbours.Economy;

namespace HiddenHarbours.Tests.Economy
{
    /// <summary>
    /// <b>OWNING A PLACE TO LIVE</b> — the deed store (<see cref="HomeDeeds"/>) and the door that sells
    /// it (<see cref="HomeDoor"/>). The first ownership record for a home in this game, so the things
    /// worth pinning are the ones that would cost the player real money or real trust: you are never
    /// charged twice, you are never charged for nothing, and a home you own with no inside SAYS SO
    /// instead of pretending to be locked.
    ///
    /// <para>Pure logic over the Core contracts — no scene, no builder, runs headless. The lot that
    /// PLACES this door is <c>StPetersCamperLotTests</c>'s business.</para>
    /// </summary>
    public class HomeDeedTests
    {
        private sealed class FakeWallet : IWallet
        {
            public FakeWallet(int starting) { Money = starting; }
            public int Money { get; private set; }
            public void Add(int amount) => Money += amount;
            public bool TrySpend(int amount)
            {
                if (amount < 0 || amount > Money) return false;
                Money -= amount;
                return true;
            }
        }

        private sealed class FlagSaveService : ISaveService
        {
            private readonly Dictionary<string, bool> _flags = new();
            public FlagSaveService(SaveData data) { Current = data; }
            public SaveData Current { get; }
            public bool GetFlag(string key) => _flags.TryGetValue(key, out var v) && v;
            public void SetFlag(string key, bool value) { if (!string.IsNullOrEmpty(key)) _flags[key] = value; }
            public void Save() { }
            public int FlagCount => _flags.Count;
        }

        const string HomeId = "home.test_camper";

        private readonly List<Object> _spawned = new();
        private readonly List<HomePurchased> _events = new();
        private void OnPurchased(HomePurchased e) => _events.Add(e);

        private FlagSaveService _save;

        [SetUp]
        public void SetUp()
        {
            _events.Clear();
            EventBus.Clear<HomePurchased>();
            EventBus.Subscribe<HomePurchased>(OnPurchased);
            GameServices.Reset();
            _save = new FlagSaveService(new SaveData());
            GameServices.Save = _save;
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Unsubscribe<HomePurchased>(OnPurchased);
            EventBus.Clear<HomePurchased>();
            GameServices.Reset();
            foreach (var o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        private HomeDef MakeHome(int price, HomeDef.Mode mode = HomeDef.Mode.Purchasable,
                                 string id = HomeId)
        {
            var home = ScriptableObject.CreateInstance<HomeDef>();
            home.Id = id;
            home.DisplayName = "Test camper";
            home.Price = price;
            home.Acquisition = mode;
            home.Variant = "clipper";
            _spawned.Add(home);
            return home;
        }

        private HomeDoor MakeDoor(HomeDef home, bool interiorStood = false)
        {
            var go = new GameObject("HomeDoor");
            _spawned.Add(go);
            var door = go.AddComponent<HomeDoor>();
            // EditMode does not fire OnEnable for AddComponent, so Configure() explicitly — the public
            // driver the builder calls at bake time (the LicenseService.Register precedent).
            door.Configure(home, interiorStood);
            return door;
        }

        // =================================================================================
        //  THE DEED STORE
        // =================================================================================

        [Test]
        public void ADeed_IsNotHeldUntilItIsGranted_AndThenItIs()
        {
            Assert.IsFalse(HomeDeeds.IsOwned(_save, HomeId), "a new game owns no homes");

            Assert.IsTrue(HomeDeeds.Grant(_save, HomeId),
                "the first grant is what makes the home owned, so it reports true");
            Assert.IsTrue(HomeDeeds.IsOwned(_save, HomeId));
        }

        /// <summary>
        /// Idempotent, and it says which call did the work. The door leans on the return value to tell
        /// "bought it" from "already had it" without reading back, so a second grant reporting true
        /// would fire a purchase event for a home that changed hands once.
        /// </summary>
        [Test]
        public void GrantingTwice_ChangesNothingAndReportsSo()
        {
            HomeDeeds.Grant(_save, HomeId);
            int flagsAfterFirst = _save.FlagCount;

            Assert.IsFalse(HomeDeeds.Grant(_save, HomeId),
                "the second grant changed nothing, so it must not claim it did");
            Assert.IsTrue(HomeDeeds.IsOwned(_save, HomeId));
            Assert.AreEqual(flagsAfterFirst, _save.FlagCount, "a repeat grant wrote a second flag");
        }

        /// <summary>
        /// 🔴 <b>The opposite of <see cref="ILicenseService.IsLicensed"/>, deliberately.</b> An empty
        /// licence id means "no licence required" and answers TRUE; an empty home id must answer FALSE.
        /// Copy the licence convention here and every door with no home configured opens.
        /// </summary>
        [Test]
        public void AnEmptyHomeId_IsNotOwned_UnlikeAnEmptyLicenceId()
        {
            Assert.IsFalse(HomeDeeds.IsOwned(_save, null));
            Assert.IsFalse(HomeDeeds.IsOwned(_save, ""));
            Assert.IsFalse(HomeDeeds.Grant(_save, ""), "an empty id has nothing to record");
            Assert.AreEqual(0, _save.FlagCount);
        }

        /// <summary>No save service (EditMode rigs, pre-boot) reads as "owns nothing" rather than
        /// throwing — the greybox posture every other Core locker keeps.</summary>
        [Test]
        public void WithNoSaveService_ADeedIsUnownedAndAGrantIsSilent()
        {
            Assert.IsFalse(HomeDeeds.IsOwned((ISaveService)null, HomeId));
            Assert.DoesNotThrow(() => HomeDeeds.Grant((ISaveService)null, HomeId));
        }

        /// <summary>The key is prefixed and greppable, and it carries the home id verbatim so a save
        /// file can be read by a human.</summary>
        [Test]
        public void TheFlagKey_IsThePrefixPlusTheHomeId()
        {
            Assert.AreEqual("deed." + HomeId, HomeDeeds.KeyFor(HomeId));
            HomeDeeds.Grant(_save, HomeId);
            Assert.IsTrue(_save.GetFlag("deed." + HomeId),
                "the deed is not under the key HomeDeeds.KeyFor advertises, so nothing else can read it");
        }

        // =================================================================================
        //  THE DOOR
        // =================================================================================

        [Test]
        public void BuyingAHomeYouCanAfford_TakesTheMoney_RecordsTheDeed_AndSaysSo()
        {
            var wallet = new FakeWallet(1000);
            GameServices.Wallet = wallet;
            var door = MakeDoor(MakeHome(900));

            Assert.AreEqual(HomeDoor.Outcome.Bought, door.Interact());
            Assert.AreEqual(100, wallet.Money, "the price came out of the purse exactly once");
            Assert.IsTrue(HomeDeeds.IsOwned(_save, HomeId));
            Assert.IsTrue(door.IsOwned);

            Assert.AreEqual(1, _events.Count, "one home changed hands, so one signal");
            Assert.AreEqual(HomeId, _events[0].HomeId);
            Assert.AreEqual(900, _events[0].PricePaid);
        }

        [Test]
        public void BuyingAHomeYouCannotAfford_ChargesNothingAndGrantsNothing()
        {
            var wallet = new FakeWallet(899);
            GameServices.Wallet = wallet;
            var door = MakeDoor(MakeHome(900));

            Assert.AreEqual(HomeDoor.Outcome.CannotAfford, door.Interact());
            Assert.AreEqual(899, wallet.Money, "a failed purchase must be atomic — nothing left the purse");
            Assert.IsFalse(HomeDeeds.IsOwned(_save, HomeId));
            Assert.IsEmpty(_events);
        }

        /// <summary>
        /// 🔴 <b>The one that would cost the player a season's fishing.</b> A home costs more than
        /// anything else in the opening, so a door that re-charges on the second press is the most
        /// expensive bug this component could have. Ownership is checked BEFORE money for exactly this
        /// reason (the <see cref="LicenseVendor"/> double-charge guard).
        /// </summary>
        [Test]
        public void PressingTheDoorAgainAfterBuying_NeverChargesTwice()
        {
            var wallet = new FakeWallet(2000);
            GameServices.Wallet = wallet;
            var door = MakeDoor(MakeHome(900));

            Assert.AreEqual(HomeDoor.Outcome.Bought, door.Interact());
            Assert.AreEqual(1100, wallet.Money);

            Assert.AreEqual(HomeDoor.Outcome.OwnedButNotEnterable, door.Interact());
            Assert.AreEqual(HomeDoor.Outcome.OwnedButNotEnterable, door.Interact());
            Assert.AreEqual(1100, wallet.Money, "the door charged for a home the player already owns");
            Assert.AreEqual(1, _events.Count, "a re-press published a second purchase");
        }

        /// <summary>
        /// ⚠️⚠️ <b>THE SHELL.</b> The camper kit ships no interior ("No interior. This is the shell.")
        /// and <c>camperInteriorRig.js</c> is not in the drop, so an owned camper CANNOT be entered.
        /// This pins that as a state the door reports honestly rather than as a door that silently does
        /// nothing — and it is the assert that fires the day someone flags a shell enterable.
        /// </summary>
        [Test]
        public void AHomeYouOwnWithNoInterior_RefusesEntryRatherThanOpeningOntoNothing()
        {
            GameServices.Wallet = new FakeWallet(1000);
            var door = MakeDoor(MakeHome(0), interiorStood: false);

            Assert.IsFalse(door.InteriorStood,
                "the camper is a SHELL — flagging it enterable with no room baked is the sabotage this " +
                "test exists to catch");
            door.Interact();                                   // buy it (free)
            Assert.IsTrue(door.IsOwned);

            Assert.AreEqual(HomeDoor.Outcome.OwnedButNotEnterable, door.Interact());
        }

        /// <summary>And the other half: the seam is real, so the day a room IS stood the same door
        /// lets you in without a line changing.</summary>
        [Test]
        public void AHomeYouOwnWithAnInterior_LetsYouIn()
        {
            GameServices.Wallet = new FakeWallet(1000);
            var door = MakeDoor(MakeHome(0), interiorStood: true);

            door.Interact();
            Assert.AreEqual(HomeDoor.Outcome.Entered, door.Interact());
        }

        /// <summary>
        /// The owner's PENDING decision, built and inert. Flipping one field on one asset makes the
        /// camper his from the first morning; the price is then ignored and no money moves.
        /// </summary>
        [Test]
        public void AStarterHome_IsGrantedFreeAndIgnoresItsPrice()
        {
            var wallet = new FakeWallet(10);
            GameServices.Wallet = wallet;
            var door = MakeDoor(MakeHome(900, HomeDef.Mode.StarterHome));

            Assert.AreEqual(HomeDoor.Outcome.Bought, door.Interact());
            Assert.AreEqual(10, wallet.Money, "a starter home charged the player");
            Assert.IsTrue(HomeDeeds.IsOwned(_save, HomeId));
            Assert.AreEqual(1, _events.Count);
            Assert.AreEqual(0, _events[0].PricePaid,
                "a starter home publishes a price of 0, so a listener can tell a gift from a purchase");
        }

        /// <summary>A door with no home configured is inert and loud, not a free house.</summary>
        [Test]
        public void ADoorWithNoHome_DoesNothing()
        {
            GameServices.Wallet = new FakeWallet(10000);
            var door = MakeDoor(null);

            LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("HomeDoor"));
            Assert.AreEqual(HomeDoor.Outcome.None, door.Interact());
            Assert.IsFalse(door.IsOwned);
            Assert.IsEmpty(_events);
        }

        /// <summary>Two homes are two deeds. Trivially true today with one home in the world, and the
        /// thing that must stay true when the residence ladder arrives.</summary>
        [Test]
        public void BuyingOneHome_DoesNotGrantAnother()
        {
            GameServices.Wallet = new FakeWallet(5000);
            MakeDoor(MakeHome(900, HomeDef.Mode.Purchasable, "home.a")).Interact();

            Assert.IsTrue(HomeDeeds.IsOwned(_save, "home.a"));
            Assert.IsFalse(HomeDeeds.IsOwned(_save, "home.b"));
        }
    }
}
