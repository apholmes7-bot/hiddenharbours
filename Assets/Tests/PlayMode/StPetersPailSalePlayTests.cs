#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
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
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// <b>The first week's third step, played in the committed St Peters scene on a new game.</b> He holds the clam
    /// licence, takes the shovel and the island's pail in his hands, digs clams at low water with the real press,
    /// stows each one with the real press, walks up to Marguerite at the store and picks her sell row. What she buys
    /// is what is in the pail he carries: the purse rises by the counter's own quote from the data, the pail is empty
    /// and still in his hands, and the belt pail is untouched. Without the pail the belt pail sells as it always did.
    ///
    /// <para><b>Over a save and a relaunch</b>, the belt pail's clams come back (its saved row) and the carried pail's
    /// do not: the carried pail has no saved home, and this states that as it is.</para>
    ///
    /// <para>Every step writes what happened (which hold the dig filled, what the counter read, what she said, the
    /// purse) before it asserts, so a failing run still records the whole walk. The save is a fresh file under the
    /// run's own redirect, never the shared one.</para>
    /// </summary>
    public class StPetersPailSalePlayTests
    {
        const string Region = "StPeters";
        const string Cleanup = "StPetersPailSaleCleanup";
        const string SaveFile = "stp-pail-sale.json";
        const string Marguerite = "MargueriteLeBlanc";
        const string SellerId = "seller.leblancs";
        const string CounterName = "GeneralStoreCounter";
        const string PailId = "container.st_peters.bucket";
        const string ShovelInstanceId = "tool.st_peters.shovel";
        const string ClamLicence = "license.clam";
        const string ClamId = "fish.soft_shell_clam";
        const float LowWaterStepSeconds = 600f;
        const float LowWaterSearchSeconds = 26f * 3600f;
        /// <summary>Distances are the player's transform to Marguerite, which is what the interactor measures.</summary>
        const float Standoff = 2.6f, Arrived = 1.5f, Overshoot = 0.6f;
        const int SquareUpTries = 6;

        readonly HashSet<GameObject> _residentBefore = new HashSet<GameObject>();
        SaveService _save;
        ISaveService _previousSave;
        string _previousPath, _path;
        float _timeScale;
        bool _loaded;
        HeldWalkIntents _held;
        PlayerWalkController _walker;

        sealed class Rig
        {
            public PlayerWalkController Walker;
            public Rigidbody2D Body;
            public ControlSwitcher Switcher;
            public CarryHands Hands;
            public IHold Belt;
            public CarriableBucket Pail;
            public CarriableTool Shovel;
            public List<ClamDig> Holes;
        }

        // ---- set-up: a new game on a fresh file under this run's redirect ----------------------------------------

        static List<GameObject> PersistentRoots()
        {
            var probe = new GameObject("pail sale persistent probe");
            Object.DontDestroyOnLoad(probe);
            Scene scene = probe.scene;
            Object.DestroyImmediate(probe);
            return scene.GetRootGameObjects().ToList();
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            _loaded = false;
            _timeScale = Time.timeScale;
            _residentBefore.Clear();
            foreach (GameObject root in PersistentRoots()) _residentBefore.Add(root);
            string run = System.Environment.GetEnvironmentVariable(SaveStore.TestRunSaveVariable);
            Assert.That(run, Is.Not.Null.And.Not.Empty, "TestRunSave must redirect before entering Play");
            Assert.That(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetFullPath(run))),
                Is.EqualTo(TestRunSave.RunsRoot), "the save belongs to this checkout's test run");
            Assert.That(SaveStore.ActivePath, Is.EqualTo(run));
            _save = Object.FindAnyObjectByType<SaveService>();
            Assert.That(_save, Is.Not.Null, "the BeforeSceneLoad save bootstrap ran");
            _previousPath = _save.SavePath;
            Assert.That(_previousPath, Is.EqualTo(run), "the shared save must never be opened");
            _previousSave = GameServices.Save;
            _path = Path.Combine(Path.GetDirectoryName(run), SaveFile);
            if (File.Exists(_path)) File.Delete(_path);
            _save.Open(_path);
            Assert.That(_save.LoadedExistingSave, Is.False, "a new game: nothing on disk at the test's own path");
            GameServices.Save = _save;
            GameServices.Save.SetFlag(ArrivalOpening.ArrivedFlagKey, true);   // he has landed and woken on the island
            Time.timeScale = 1f;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            _held?.Release();
            if (_walker != null) _walker.ConfigureWalkInput(null);
            LogAssert.ignoreFailingMessages = false;
            if (_loaded) yield return UnloadRegion();
            foreach (GameObject root in PersistentRoots())
                if (root != null && !_residentBefore.Contains(root)) Object.DestroyImmediate(root);
            GameServices.Reset();
            if (_save != null && _previousPath != null) _save.Open(_previousPath);
            GameServices.Save = _previousSave;
            Time.timeScale = _timeScale;
            _held = null; _walker = null; _save = null; _previousPath = null;
            yield return null;
        }

        IEnumerator UnloadRegion()
        {
            Scene clean = SceneManager.GetSceneByName(Cleanup);
            if (!clean.IsValid()) clean = SceneManager.CreateScene(Cleanup);
            SceneManager.SetActiveScene(clean);
            Scene region = SceneManager.GetSceneByName(Region);
            if (region.IsValid() && region.isLoaded) yield return HiddenHarbours.Tests.Support.SceneSettle.Unload(region);
        }

        IEnumerator Boot(Rig rig)
        {
            LogAssert.ignoreFailingMessages = true;
            _loaded = true;
            yield return HiddenHarbours.Tests.Support.SceneSettle.Load(Region, LoadSceneMode.Single);
            for (int frame = 0; frame < 4; frame++) yield return null;
            if (ShellFlow.WorldInputBlocked) ShellFlow.Reset();
            ShellFlow.ContinueGame();
            for (int frame = 0; frame < 6; frame++) yield return null;
            InteractionGate.Reset();
            Assert.That(ShellFlow.WorldInputBlocked, Is.False, "the world takes input");
            Assert.That(GameServices.OpeningCinematicRunning, Is.False, "the arrival must not take the walker");

            rig.Walker = Object.FindObjectsByType<PlayerWalkController>(FindObjectsSortMode.None)
                .Single(p => p.isActiveAndEnabled && p.transform == GameServices.PlayerTransform);
            _walker = rig.Walker;
            rig.Body = rig.Walker.GetComponent<Rigidbody2D>();
            rig.Switcher = Object.FindObjectsByType<ControlSwitcher>(FindObjectsSortMode.None).Single(s => s.isActiveAndEnabled);
            rig.Hands = GameServices.Hands as CarryHands;
            Assert.That(rig.Hands, Is.Not.Null, "his hands are published");
            rig.Belt = GameServices.PlayerHold;
            Assert.That(rig.Belt, Is.Not.Null, "his belt pail is published");
            rig.Pail = Object.FindObjectsByType<CarriableBucket>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .SingleOrDefault(b => b.Id == PailId);
            Assert.That(rig.Pail, Is.Not.Null, $"the island's pail {PailId}");
            rig.Shovel = Object.FindObjectsByType<CarriableTool>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .SingleOrDefault(t => t.Id == ShovelInstanceId);
            Assert.That(rig.Shovel, Is.Not.Null, $"the island's shovel {ShovelInstanceId}");
            rig.Holes = Object.FindObjectsByType<ClamDig>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Where(h => h.gameObject.scene.name == Region).OrderBy(h => h.Id, System.StringComparer.Ordinal).ToList();
            Assert.That(rig.Holes.Count, Is.GreaterThan(0), "the flats have clam holes");

            // The licence service is a run-long bootstrap; a GameServices.Reset (an earlier test's teardown, or the
            // relaunch below) drops its registration, so it is asked to register again, reading the live save.
            if (GameServices.Licenses == null)
            {
                LicenseService licences = Object.FindAnyObjectByType<LicenseService>();
                Assert.That(licences, Is.Not.Null, "the licence service bootstrap ran");
                licences.Register();
            }
            // The counter desk is the same kind of run-long bootstrap, and another fixture may have uninstalled it
            // in its own teardown; a real launch always has it installed.
            if (!CounterSellDesk.IsInstalled) CounterSellDesk.Install();
            GameServices.Licenses.Grant(ClamLicence);
            Assert.That(GameServices.Licenses.IsLicensed(ClamLicence), Is.True, "he holds the clam licence");
            SaveData data = GameServices.Save.Current;
            TestContext.WriteLine($"NEW GAME: save v{data.SchemaVersion}, money {data.Money}, gear [{string.Join(",", data.OwnedGear)}], " +
                                  $"{rig.Holes.Count} holes, belt {rig.Belt.UsedUnits}/{rig.Belt.CapacityUnits}, " +
                                  $"pail {rig.Pail.UsedUnits}/{rig.Pail.CapacityUnits} at ({rig.Pail.transform.position.x:0.##}, {rig.Pail.transform.position.y:0.##})");
            LogAssert.ignoreFailingMessages = false;
        }

        // ---- the walk ---------------------------------------------------------------------------------------------

        [UnityTest]
        public IEnumerator ThePailHeCarries_DugFullAtLowWater_IsWhatMargueriteBuys()
        {
            var rig = new Rig();
            yield return Boot(rig);
            yield return PickUp(rig, rig.Shovel, "the shovel");
            yield return PickUp(rig, rig.Pail, "the pail");
            yield return LowWater(rig, 3);

            const int clams = 3;
            for (int i = 0; i < clams; i++) yield return DigAndStow(rig, $"clam {i + 1}");
            TestContext.WriteLine($"AFTER DIGGING: carried pail {rig.Pail.UsedUnits}, belt pail {rig.Belt.UsedUnits}");
            Assert.That(rig.Pail.UsedUnits, Is.EqualTo(clams), "the clams went into the pail he carries");
            Assert.That(rig.Belt.UsedUnits, Is.EqualTo(0), "and not into the belt pail");

            Sale sale = Prepare(rig.Pail, clams);
            yield return WalkUpAndSell(rig, sale);
            Report(rig, sale);

            Assert.That(sale.Report.HasValue, Is.True, "the counter answered the sell row");
            Assert.That(sale.Report.Value.UnitsSold, Is.EqualTo(clams), "every clam in the pail he carries was bought");
            Assert.That(sale.Report.Value.Payout, Is.EqualTo(sale.Quote), "at the counter's own quote from the data");
            Assert.That(sale.Wallet.Money - sale.WalletBefore, Is.EqualTo(sale.Quote), "the purse rises by exactly that");
            Assert.That(rig.Pail.UsedUnits, Is.EqualTo(0), "the pail is empty");
            Assert.That(CarriedItem.IsHeld(GameServices.Hands, rig.Pail), Is.True, "and still in his hands");
            Assert.That(rig.Belt.UsedUnits, Is.EqualTo(0), "the belt pail was never filled and is still empty");
        }

        [UnityTest]
        public IEnumerator WithoutThePail_TheBeltPailStillSellsAtMargueritesCounter()
        {
            var rig = new Rig();
            yield return Boot(rig);
            yield return PickUp(rig, rig.Shovel, "the shovel");
            yield return LowWater(rig, 2);

            const int clams = 2;
            for (int i = 0; i < clams; i++) yield return DigAndStow(rig, $"clam {i + 1}");
            TestContext.WriteLine($"AFTER DIGGING: carried pail {rig.Pail.UsedUnits} (on the ground), belt pail {rig.Belt.UsedUnits}");
            Assert.That(rig.Belt.UsedUnits, Is.EqualTo(clams), "with no pail in hand the clams go on his belt");

            Sale sale = Prepare(rig.Belt, clams);
            yield return WalkUpAndSell(rig, sale);
            Report(rig, sale);

            Assert.That(sale.Report.HasValue, Is.True, "the counter answered the sell row");
            Assert.That(sale.Report.Value.UnitsSold, Is.EqualTo(clams), "the belt pail's clams were bought");
            Assert.That(sale.Report.Value.Payout, Is.EqualTo(sale.Quote), "at the counter's own quote");
            Assert.That(sale.Wallet.Money - sale.WalletBefore, Is.EqualTo(sale.Quote), "the purse rises by exactly that");
            Assert.That(rig.Belt.UsedUnits, Is.EqualTo(0), "the belt pail is empty");
        }

        [UnityTest]
        public IEnumerator OverASaveAndARelaunch_TheBeltPailKeepsItsClams_AndTheCarriedPailComesBackEmpty()
        {
            var rig = new Rig();
            yield return Boot(rig);
            yield return PickUp(rig, rig.Shovel, "the shovel");
            yield return LowWater(rig, 3);
            yield return DigAndStow(rig, "belt clam");
            Assert.That(rig.Belt.UsedUnits, Is.EqualTo(1), "one clam on his belt");
            yield return PickUp(rig, rig.Pail, "the pail");
            yield return DigAndStow(rig, "pail clam 1");
            yield return DigAndStow(rig, "pail clam 2");
            Assert.That(rig.Pail.UsedUnits, Is.EqualTo(2), "two clams in the pail he carries");
            int money = GameServices.Wallet.Money;

            GameServices.Save.Save();
            SaveData disk = SaveStore.Read(_path);
            Assert.That(disk, Is.Not.Null, "the save was written to the test's own file");
            List<string> rows = (disk.StoredContainers ?? new List<StoredContainerDto>()).Select(c => c.Key).ToList();
            TestContext.WriteLine($"ON DISK: v{disk.SchemaVersion}, BucketCatch {disk.BucketCatch?.Count ?? 0} record(s), " +
                                  $"StoredContainers [{string.Join(",", rows)}], money {disk.Money}");
            Assert.That(rows.Contains(PailId), Is.False, "the carried pail has no saved row");

            // A relaunch: the region goes, the services go, the save is read back from disk, the region boots again.
            yield return UnloadRegion();
            foreach (GameObject root in PersistentRoots())
                if (root != null && !_residentBefore.Contains(root)) Object.DestroyImmediate(root);
            GameServices.Reset();
            // What a real launch does at BeforeSceneLoad (FishSpeciesRegistrar): the species library and the saved
            // catch's resolver. In a full run an earlier class resets the species registry, and a launch fills it
            // again before any restore, so the relaunch here must too or no saved clam can be rebuilt.
            Resources.Load<FishSpeciesLibrary>(FishSpeciesRegistrar.ResourcesPath)?.RegisterAll();
            GameServices.CatchFactory ??= new SpeciesCatchItemFactory();
            Assert.That(FishSpeciesRegistry.Has(ClamId), Is.True, "the relaunch can resolve the saved clam's species");
            _save.Open(_path);
            Assert.That(_save.LoadedExistingSave, Is.True, "the relaunch reads the saved game");
            GameServices.Save = _save;
            yield return null;
            var again = new Rig();
            yield return Boot(again);

            CarriedItem.Both(GameServices.Hands, out ICarriable first, out ICarriable second);
            TestContext.WriteLine($"AFTER RELAUNCH: belt pail {again.Belt.UsedUnits}, carried pail {again.Pail.UsedUnits} " +
                                  $"at ({again.Pail.transform.position.x:0.##}, {again.Pail.transform.position.y:0.##}), " +
                                  $"held: {first?.DefId ?? "nothing"} / {second?.DefId ?? "nothing"}, money {GameServices.Wallet.Money} (was {money})");
            Assert.That(again.Belt.UsedUnits, Is.EqualTo(1), "the belt pail's clam comes back from its saved row");
            Assert.That(GameServices.Wallet.Money, Is.EqualTo(money), "the purse comes back as it was");
            Assert.That(again.Pail.UsedUnits, Is.EqualTo(0),
                "TODAY: the carried pail's contents have no saved home, so its two clams do not come back. " +
                "If this fails because they do, a save step has landed: update this test.");
        }

        // ---- steps ------------------------------------------------------------------------------------------------

        IEnumerator PickUp(Rig rig, IInteractable thing, string what)
        {
            Place(rig, thing.WorldPosition + Vector2.down * 0.6f);
            yield return null;
            thing.Interact(default);
            yield return null;
            Assert.That(CarriedItem.IsHeld(GameServices.Hands, (ICarriable)thing), Is.True, $"he picks up {what}");
            TestContext.WriteLine($"PICKED UP {what}");
        }

        /// <summary>Move the clock forward to the first moment at least <paramref name="wanted"/> unspent holes are bared,
        /// searching the next day in fixed steps from the new game's own start, so the hour is the tide's, not a guess.</summary>
        IEnumerator LowWater(Rig rig, int wanted)
        {
            IGameClock clock = GameServices.Clock;
            Assert.That(clock, Is.Not.Null, "the island has a clock");
            double start = clock.TotalSeconds;
            int bared = 0;
            double at = start;
            for (double t = 0; t <= LowWaterSearchSeconds; t += LowWaterStepSeconds)
            {
                at = start + t;
                clock.SeekTo(at);
                bared = rig.Holes.Count(h => !h.Consumed && h.IsExposedNow());
                if (bared >= wanted + 2) break;
            }
            double day = GameServices.Config != null ? GameServices.Config.SecondsPerDay : 86400.0;
            TestContext.WriteLine($"LOW WATER: {bared} of {rig.Holes.Count} holes bared at hour {((at % day) / day * 24.0):0.00} " +
                                  $"({(at - start) / 3600.0:0.0} h after the new game began)");
            Assert.That(bared, Is.GreaterThanOrEqualTo(wanted), "the tide bares enough holes within a day");
            yield return null;
        }

        /// <summary>Stand on the next bared hole, press (the dig), then press again (the stow), and say where it went.</summary>
        IEnumerator DigAndStow(Rig rig, string label)
        {
            ClamDig hole = rig.Holes.FirstOrDefault(h => !h.Consumed && h.IsExposedNow());
            Assert.That(hole, Is.Not.Null, $"{label}: a bared hole to dig");
            Place(rig, hole.WorldPosition);
            yield return null;
            yield return null;

            int pailBefore = rig.Pail.UsedUnits, beltBefore = rig.Belt.UsedUnits;
            bool pressed = rig.Switcher.BeginInteract();
            yield return null;
            ICarriable inHand = CarriedItem.Held(GameServices.Hands, HandLoad.Catch);
            TestContext.WriteLine($"DIG {label} at {hole.Id}: press {(pressed ? "taken" : "not taken")}, hole spent {hole.Consumed}, " +
                                  $"catch in hand {(inHand != null)}, slung {rig.Hands.Slung?.DefId ?? "nothing"}");
            Assert.That(inHand, Is.Not.Null, $"{label}: the dug clam is in his hand");

            pressed = rig.Switcher.BeginInteract();
            yield return null;
            string went = rig.Pail.UsedUnits > pailBefore ? "the pail he carries"
                        : rig.Belt.UsedUnits > beltBefore ? "the belt pail" : "nowhere";
            TestContext.WriteLine($"STOW {label}: press {(pressed ? "taken" : "not taken")}, went into {went} " +
                                  $"(pail {rig.Pail.UsedUnits}, belt {rig.Belt.UsedUnits}), " +
                                  $"pail carried {CarriedItem.IsHeld(GameServices.Hands, rig.Pail)}");
            Assert.That(CarriedItem.Held(GameServices.Hands, HandLoad.Catch), Is.Null, $"{label}: the clam left his hand");
            Assert.That(went, Is.Not.EqualTo("nowhere"), $"{label}: the clam went into a pail");
        }

        static void Place(Rig rig, Vector2 at)
        {
            rig.Body.position = at;
            rig.Body.linearVelocity = Vector2.zero;
            rig.Walker.transform.position = new Vector3(at.x, at.y, rig.Walker.transform.position.z);
            Physics2D.SyncTransforms();
        }

        // ---- the counter, the row ---------------------------------------------------------------------------------

        sealed class Sale
        {
            public WharfSellPoint Counter;
            public Market Market;
            public IWallet Wallet;
            public int WalletBefore;
            public int Quote;
            public string Told = "";
            public CounterSellReported? Report;
        }

        Sale Prepare(IHold expectedHold, int clams)
        {
            WharfSellPoint counter = Object.FindObjectsByType<WharfSellPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .SingleOrDefault(p => p.gameObject.name == CounterName && p.gameObject.scene.name == Region);
            Assert.That(counter, Is.Not.Null, $"St Peters has {CounterName}");
            var buyer = Field<FishBuyer>(counter, "_buyer");
            var market = Field<Market>(buyer, "_market");
            Assert.That(market, Is.Not.Null, "the counter's buyer prices at a market");
            IWallet wallet = Field<GameObject>(counter, "_walletProvider").GetComponent<IWallet>();
            Assert.That(wallet, Is.Not.Null, "the counter's wallet provider is a wallet");
            int quote = SellService.Quote(expectedHold, market, ClamId, clams);
            TestContext.WriteLine($"COUNTER: sells what is carried {counter.SellsWhatIsCarried}, market {market.Id} " +
                                  $"(price level {market.PriceLevel}), quote for {clams} clam(s) {quote}, purse {wallet.Money}");
            Assert.That(quote, Is.GreaterThan(0), "the clams are worth something at the island store");
            return new Sale { Counter = counter, Market = market, Wallet = wallet, WalletBefore = wallet.Money, Quote = quote };
        }

        static void Report(Rig rig, Sale sale)
        {
            string r = sale.Report.HasValue ? $"{sale.Report.Value.UnitsSold} sold for {sale.Report.Value.Payout}" : "no report";
            TestContext.WriteLine($"SALE: {r}; purse {sale.WalletBefore} -> {sale.Wallet.Money}; " +
                                  $"carried pail {rig.Pail.UsedUnits} (held {CarriedItem.IsHeld(GameServices.Hands, rig.Pail)}), " +
                                  $"belt pail {rig.Belt.UsedUnits}; she said: \"{sale.Told}\"");
        }

        IEnumerator WalkUpAndSell(Rig rig, Sale sale)
        {
            Interactable marguerite = Object.FindObjectsByType<Interactable>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .SingleOrDefault(i => i.gameObject.name == Marguerite);
            Assert.That(marguerite, Is.Not.Null, $"{Marguerite} carries a live Interactable");
            var interactor = Object.FindAnyObjectByType<WorldInteractor>();
            var bubble = Object.FindAnyObjectByType<DialoguePresenter>();
            Assert.That(interactor != null && bubble != null, Is.True, "the island has a dialogue driver and a bubble");

            var foot = rig.Walker.GetComponents<CircleCollider2D>().First(c => !c.isTrigger && c.enabled);
            Vector2 at = marguerite.transform.position;
            Vector2 toward = ClearApproach(rig, at, marguerite.transform, foot.offset, foot.radius + 0.05f);
            Place(rig, at - toward * Standoff);
            yield return null;
            Time.timeScale = 4f;
            _held = new HeldWalkIntents(); rig.Walker.ConfigureWalkInput(_held);
            int steps = 0, still = 0;
            Vector2 last = rig.Body.position;
            while (Vector2.Distance(rig.Body.position, at) > Arrived && steps++ < 900 && still < 30)
            {
                _held.Walk(toward); yield return new WaitForFixedUpdate();
                still = (rig.Body.position - last).sqrMagnitude < 1e-6f ? still + 1 : 0;
                last = rig.Body.position;
            }
            _held.Release(); Time.timeScale = 1f;
            for (int i = 0; i < 3; i++) yield return null;
            // Square up to her before the press, as a player does: at the 4x walk a slow machine can stop him a
            // step off her line, facing past her, and the interactor offers only who he is looking at. One short
            // step toward where she stands now turns him to her; the offer itself is still what is asserted.
            int squared = 0;
            for (; squared < SquareUpTries && interactor.Nearest != marguerite; squared++)
            {
                Vector2 toHer = (Vector2)marguerite.transform.position - rig.Body.position;
                if (toHer.sqrMagnitude > 1e-6f) _held.Walk(toHer.normalized);
                yield return new WaitForFixedUpdate();
                _held.Release();
                yield return null;
            }
            float d = Vector2.Distance(rig.Walker.transform.position, marguerite.transform.position);
            TestContext.WriteLine($"WALKED {steps} steps up to {Marguerite}, {d:0.###} m away (squared up {squared}x), carrying the pail: " +
                                  $"{CarriedItem.IsHeld(GameServices.Hands, rig.Pail)} (pail {rig.Pail.UsedUnits}, belt {rig.Belt.UsedUnits})");
            if (interactor.Nearest != marguerite)
            {
                Vector2 facing = InteractActorProbe.Has ? InteractActorProbe.Current.Facing : Vector2.zero;
                TestContext.WriteLine($"NOT OFFERED: nearest {(interactor.Nearest != null ? interactor.Nearest.name : "none")}, " +
                                      $"her interactable active {marguerite.isActiveAndEnabled}, moved {Vector2.Distance(at, marguerite.transform.position):0.###} m, " +
                                      $"facing ({facing.x:0.##}, {facing.y:0.##}), gate blocked {InteractionGate.IsBlocked}, bubble up {bubble.IsShowing}");
            }
            Assert.That(interactor.Nearest, Is.SameAs(marguerite), $"the interactor offers {Marguerite} once the walk arrives ({d:0.###} m)");

            Assert.That(interactor.BeginInteract(), Is.True, "the press is spent on Marguerite");
            yield return null;
            Assert.That(bubble.IsShowing, Is.True, "the conversation is up");
            for (int press = 0; press < 16 && !bubble.IsChoosing; press++) { interactor.BeginInteract(); yield return null; }
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
            Assert.That(bubble.IsShowing, Is.True, "Marguerite answers the row in the bubble");
            for (int i = 0; i < 240 && bubble.IsShowing; i++)
            {
                string seen = bubble.VisibleText;
                if (!string.IsNullOrEmpty(seen) && seen.Length > sale.Told.Length) sale.Told = seen;
                yield return null;
                if (i > 60 && sale.Told.Length > 0 && bubble.VisibleText == sale.Told) break;
            }
        }

        Vector2 ClearApproach(Rig rig, Vector2 at, Transform target, Vector2 footOffset, float radius)
        {
            foreach (Vector2 toward in new[] { Vector2.up, Vector2.down, Vector2.right, Vector2.left })
            {
                bool clear = true;
                for (float s = Arrived - Overshoot; s <= Standoff && clear; s += 0.1f)
                    clear = !Physics2D.OverlapCircleAll(at - toward * s + footOffset, radius)
                        .Any(c => !c.isTrigger && !c.transform.IsChildOf(rig.Walker.transform) && !c.transform.IsChildOf(target));
                if (clear) return toward;
            }
            Assert.Fail($"no side of ({at.x:0.##}, {at.y:0.##}) has a clear {Standoff} m approach");
            return Vector2.zero;
        }

        static T Field<T>(object owner, string name) where T : class
        {
            FieldInfo f = owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(f, Is.Not.Null, $"{owner.GetType().Name}.{name}");
            return f.GetValue(owner) as T;
        }
    }
}
#endif
