using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Core;
using HiddenHarbours.Economy;
using HiddenHarbours.Environment;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The slip dory, after the sale: the supplies he carries in his shopping bag are what the mending
    /// session may use; a press and a held key mend her job by job through the real session and the
    /// shipped data; a launch clears the slip flag exactly once, puts his dory afloat at her mooring and is
    /// never replayed by a load; nothing happens unless she is his and mended, and a guest writes nothing.
    /// Driven through the station's own press and tick, the Core launch step and the save seam; no scene.
    /// </summary>
    public class DoryMendAndLaunchTests
    {
        const string StationAsset = "Assets/_Project/Data/Regions/StPetersVillage/slipmend.basils_dory.asset";
        const string SaleAsset    = "Assets/_Project/Data/Regions/StPetersVillage/slipsale.basils_dory.asset";
        const string PrefabAsset  = "Assets/_Project/Prefabs/Boats/SlipDory.prefab";
        const string ConfigAsset  = "Assets/_Project/Data/Config/GameConfig.asset";
        const string RegionAsset  = "Assets/_Project/Data/Regions/StPeters.asset";
        const string DoryHull     = "Assets/_Project/Data/Boats/Dory.asset";
        const int BagCapacity = 20;
        const float Tick = 0.25f;
        const int MaxTicks = 10000;

        // ---- fakes ---------------------------------------------------------------------------------

        sealed class MemorySave : ISaveService
        {
            public MemorySave(SaveData data) { Current = data; }
            public SaveData Current { get; set; }
            public int Writes;
            int _depth;
            bool _pending;
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
                    if (Current.OnboardingFlags[i].Key == key) { Current.OnboardingFlags[i] = new SaveFlag(key, value); Save(); return; }
                Current.OnboardingFlags.Add(new SaveFlag(key, value));
                Save();
            }
            public void Save() { if (_depth > 0) _pending = true; else Writes++; }
            public void BeginNewGame() => Current = SaveMigration.NewGame();
            public IDisposable DeferWrites() { _depth++; return new Scope(this); }
            sealed class Scope : IDisposable
            {
                MemorySave _owner;
                public Scope(MemorySave owner) { _owner = owner; }
                public void Dispose()
                {
                    if (_owner == null) return;
                    var o = _owner; _owner = null;
                    if (--o._depth == 0 && o._pending) { o._pending = false; o.Save(); }
                }
            }
        }

        sealed class Clock : IGameClock
        {
            public double TotalSeconds { get; set; }
            public GameTime Now => default;
            public Season Season => default;
            public int Year => 1;
            public int DayIndex => 0;
            public int DayOfSeason => 1;
            public Weekday Weekday => default;
            public bool IsMarketDay => false;
            public float HourOfDay => 0;
            public float DayFraction => 0;
            public bool IsPaused { get; set; }
            public bool IsHeld { get; set; }
            public float TimeScale { get; set; } = 1;
            public bool FastForwardTo(double target)
            {
                if (IsHeld || IsPaused || target <= TotalSeconds) return false;
                TotalSeconds = target;
                return true;
            }
        }

        sealed class Tide : IEnvironmentService
        {
            public GameConfig Config;
            public int WorldSeed { get; set; }
            public TideProfile ActiveTideProfile { get; set; }
            public EnvironmentSample Sample() => default;
            public float TideHeightAt(double seconds) => TideModel.Height(seconds, ActiveTideProfile, Config);
        }

        sealed class Hands : IShoppingBagHands
        {
            public bool Holds = true;
            public bool HoldsBag => Holds;
            public bool BagIsSetDown => false;
            public ShoppingBagRefusal CanBag(ShoppingBagDef bag) => ShoppingBagRefusal.None;
            public ShoppingBagRefusal TryBag(ShoppingBagDef bag, string supplyId) => ShoppingBagRefusal.None;
            public bool TryPutAway(string storageId) => false;
        }

        sealed class Where : IWorldWhereabouts
        {
            public readonly List<Whereabouts> Placed = new List<Whereabouts>();
            public Whereabouts Player => default;
            public Whereabouts Boat => Placed.Count > 0 ? Placed[Placed.Count - 1] : default;
            public void RestoreBoat(Whereabouts boat) => Placed.Add(boat);
        }

        // ---- rig -----------------------------------------------------------------------------------

        readonly List<UnityEngine.Object> _spawned = new List<UnityEngine.Object>();
        readonly List<SlipDoryLaunched> _launched = new List<SlipDoryLaunched>();
        MemorySave _save;
        Clock _clock;
        Hands _hands;
        Where _where;
        DoryMendingStationDef _station;
        GameConfig _config;

        void OnLaunched(SlipDoryLaunched e) => _launched.Add(e);

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();
            Interactables.Clear();
            InteractionGate.Reset();
            ShoppingBagContents.Clear();
            SlipDoryStory.IsHost = null;
            _launched.Clear();
            EventBus.Subscribe<SlipDoryLaunched>(OnLaunched);

            _station = AssetDatabase.LoadAssetAtPath<DoryMendingStationDef>(StationAsset);
            Assert.IsNotNull(_station, StationAsset + " is missing");
            _config = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigAsset);
            Assert.IsNotNull(_config, ConfigAsset + " is missing");
            var region = AssetDatabase.LoadAssetAtPath<RegionDef>(RegionAsset);
            Assert.IsNotNull(region, RegionAsset + " is missing");

            _save = new MemorySave(SaveMigration.NewGame());
            SlipDoryStory.BeginForNewGame(_save);
            _clock = new Clock { TotalSeconds = _config.SecondsPerDay * 0.375 };
            _hands = new Hands();
            _where = new Where();
            GameServices.Save = _save;
            GameServices.Clock = _clock;
            GameServices.Config = _config;
            GameServices.Environment = new Tide
            {
                Config = _config,
                ActiveTideProfile = new TideProfile
                {
                    MeanLevel = region.TideMeanLevel, Amplitude = region.TideAmplitude, PhaseHours = region.TidePhaseHours,
                },
            };
            GameServices.Whereabouts = _where;
            GameServices.CurrentRegionId = _station.Mending.PlaceId;
            _save.Writes = 0;   // the new game's own write is not under test
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Unsubscribe<SlipDoryLaunched>(OnLaunched);
            foreach (var s in _stations) if (s != null) OnDisableMethod.Invoke(s, null);
            _stations.Clear();
            foreach (var o in _spawned) if (o != null) UnityEngine.Object.DestroyImmediate(o);
            _spawned.Clear();
            Interactables.Clear();
            InteractionGate.Reset();
            ShoppingBagContents.Clear();
            SlipDoryStory.IsHost = null;
            GameServices.Reset();
        }

        static readonly MethodInfo OnEnableMethod =
            typeof(DoryMendingStation).GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic);
        static readonly MethodInfo OnDisableMethod =
            typeof(DoryMendingStation).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic);
        readonly List<DoryMendingStation> _stations = new List<DoryMendingStation>();

        /// <summary>A station as the prefab gives one; EditMode does not run OnEnable, so it is called
        /// here, which subscribes it to the bus as play does.</summary>
        DoryMendingStation NewStation()
        {
            var go = new GameObject("SlipDory_MendTest");
            _spawned.Add(go);
            var station = go.AddComponent<DoryMendingStation>();
            station.Configure(_station);
            OnEnableMethod.Invoke(station, null);
            _stations.Add(station);
            return station;
        }

        static IEnumerable<DoryMendingJobDef> JobsInOrder(DoryMendingDef mending)
        {
            var jobs = new List<DoryMendingJobDef>(mending.Jobs);
            jobs.Sort((a, b) => a.Order.CompareTo(b.Order));
            return jobs;
        }

        string BoatId => _station.Mending.BoatId;

        void Own() { _save.Current.OwnedBoats.Add(BoatId); EventBus.Publish(new BoatPurchased(BoatId, 0, default)); }

        /// <summary>Buys every job's supplies into the bag in his hand, as the shelf does.</summary>
        void CarryAllSupplies()
        {
            foreach (var job in _station.Mending.Jobs)
                foreach (var m in job.Materials)
                    for (int n = 0; n < m.Count; n++)
                    {
                        SupplyLocker.Add(_save.Current, m.ItemId, 1);
                        Assert.IsTrue(ShoppingBagContents.TryPut(_save.Current, m.ItemId, BagCapacity), m.ItemId);
                    }
        }

        /// <summary>Press, then hold until the job ends. Returns the ticks it took.</summary>
        int WorkOneJob(DoryMendingStation station)
        {
            Assert.AreEqual(DoryMendingStationResult.Working, station.Press(null, _hands), station.LastReason.TextKey());
            int ticks = 0;
            while (station.IsWorking && ticks < MaxTicks) { station.TickHeld(true, true, Tick); ticks++; }
            Assert.Less(ticks, MaxTicks, "the job never ended");
            return ticks;
        }

        void MendHer(DoryMendingStation station)
        {
            for (int i = 0; i < _station.Mending.Jobs.Length; i++) WorkOneJob(station);
        }

        // ---- the carry -----------------------------------------------------------------------------

        [Test]
        public void TheCarry_CountsOnlyTheBagInHisHand()
        {
            var data = _save.Current;
            SupplyLocker.Add(data, "supply.oakum", 2);
            Assert.IsTrue(ShoppingBagContents.TryPut(data, "supply.oakum", BagCapacity));
            var carry = new ShoppingBagMendingCarry(_save, _hands);
            Assert.AreEqual(1, carry.Count("supply.oakum"), "one in the bag in his hand; the other is put away");

            _hands.Holds = false;
            Assert.AreEqual(0, carry.Count("supply.oakum"), "no bag in his hand, nothing carried");
            _hands.Holds = true;

            Assert.IsTrue(ShoppingBagContents.MarkSetDown(data, "region.st_peters", Vector2.zero, false));
            Assert.AreEqual(0, carry.Count("supply.oakum"), "a bag set down is not carried");
            ShoppingBagContents.MarkHeld();
            Assert.AreEqual(1, carry.Count("supply.oakum"));

            ShoppingBagContents.Release(data);
            Assert.AreEqual(0, carry.Count("supply.oakum"), "shopping put away at home is remote storage");
            Assert.AreEqual(0, new ShoppingBagMendingCarry(_save, null).Count("supply.oakum"), "no hands, nothing");
        }

        [Test]
        public void TheCarry_ReservesWithoutDebiting_AndConsumesAllOrNothing()
        {
            var data = _save.Current;
            SupplyLocker.Add(data, "supply.patch_plank", 1);
            SupplyLocker.Add(data, "supply.copper_nails", 1);
            Assert.IsTrue(ShoppingBagContents.TryPut(data, "supply.patch_plank", BagCapacity));
            var carry = new ShoppingBagMendingCarry(_save, _hands);
            var need = new[] { new MendingMaterial("supply.patch_plank", 1), new MendingMaterial("supply.copper_nails", 1) };

            Assert.IsFalse(carry.TryReserve(need, out var none), "the nails are at home, not in the bag");
            Assert.IsNull(none);
            Assert.AreEqual(1, carry.Count("supply.patch_plank"), "a refused reservation holds nothing");

            Assert.IsTrue(ShoppingBagContents.TryPut(data, "supply.copper_nails", BagCapacity));
            Assert.IsTrue(carry.TryReserve(need, out var held));
            Assert.AreEqual(0, carry.Count("supply.patch_plank"), "reserved goods are not counted again");
            Assert.AreEqual(1, SupplyLocker.Owned(data, "supply.patch_plank"), "a reservation debits nothing");
            Assert.AreEqual(1, ShoppingBagContents.InBag(data, "supply.patch_plank"));
            Assert.IsTrue(held.IsValid);

            held.Dispose();
            Assert.AreEqual(1, carry.Count("supply.patch_plank"), "a released reservation gives the goods back");
            Assert.IsFalse(held.IsValid);
            Assert.IsFalse(held.TryConsume(), "a released claim consumes nothing");

            Assert.IsTrue(carry.TryReserve(need, out held));
            ShoppingBagContents.Release(data);
            Assert.IsFalse(held.IsValid, "putting the shopping away mid-job loses the claim");
            Assert.IsFalse(held.TryConsume());
            Assert.AreEqual(1, SupplyLocker.Owned(data, "supply.patch_plank"), "a failed consume changes nothing");
            held.Dispose();

            Assert.IsTrue(ShoppingBagContents.TryPut(data, "supply.patch_plank", BagCapacity));
            Assert.IsTrue(ShoppingBagContents.TryPut(data, "supply.copper_nails", BagCapacity));
            Assert.IsTrue(carry.TryReserve(need, out held));
            Assert.IsTrue(held.TryConsume());
            Assert.AreEqual(0, SupplyLocker.Owned(data, "supply.patch_plank"), "consumed: spent from his supplies");
            Assert.AreEqual(0, SupplyLocker.Owned(data, "supply.copper_nails"));
            Assert.AreEqual(0, ShoppingBagContents.Total(data), "and out of the bag");
            Assert.IsFalse(held.TryConsume(), "exactly once");
        }

        [Test]
        public void TheCarry_IsBoundToTheSaveItReservedIn()
        {
            CarryAllSupplies();
            var carry = new ShoppingBagMendingCarry(_save, _hands);
            Assert.IsTrue(carry.TryReserve(_station.Mending.Jobs[0].Materials, out var held));
            _save.Current = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(_save.Current));
            Assert.IsFalse(held.IsValid, "another loaded save invalidates the claim");
            Assert.IsFalse(held.TryConsume());
        }

        // ---- the station: nothing unless his ------------------------------------------------------

        [Test]
        public void TheStation_OffersNothing_UntilSheIsHis()
        {
            var station = NewStation();
            Assert.IsFalse(station.IsAvailable, "Basil's dory is not his to mend");
            Assert.AreEqual(DoryMendingStationResult.NotAvailable, station.Press(null, _hands));
            CollectionAssert.DoesNotContain(Interactables.Active, station);

            Own();
            Assert.IsTrue(station.IsAvailable, "bought, she can be worked on");
            CollectionAssert.Contains(Interactables.Active, station);
            Assert.AreEqual(_station.Mending.Jobs[0].DisplayName, station.VerbLabel, "the verb names the first job");
            StringAssert.DoesNotContain("pail", _station.MissingLine.ToLowerInvariant());
        }

        [Test]
        public void TheStation_OffersNothing_InAnOldSave()
        {
            _save = new MemorySave(SaveMigration.NewGame());   // never set the story
            _save.Current.OwnedBoats.Add(BoatId);
            GameServices.Save = _save;
            var station = NewStation();
            Assert.IsFalse(station.IsAvailable, "an old save has no dory on the slip to mend");
            Assert.AreEqual(SlipDoryLaunchResult.NotOnTheSlip, SlipDoryStory.TryLaunch(_save, BoatId, true));
            Assert.AreEqual(0, _save.Writes);
        }

        [Test]
        public void ThePress_WithoutTheSupplies_RefusesInWords_AndSpendsNothing()
        {
            Own();
            var station = NewStation();
            Assert.AreEqual(DoryMendingStationResult.Refused, station.Press(null, _hands));
            Assert.AreEqual(DoryMendReason.MissingMaterials, station.LastReason);
            Assert.IsFalse(string.IsNullOrWhiteSpace(station.LastLine), "he says what he lacks");
            Assert.IsFalse(station.IsWorking);
            Assert.AreEqual(0, _save.Writes);
        }

        // ---- the station: the held work ------------------------------------------------------------

        [Test]
        public void HeldWork_MendsHerJobByJob_ThroughTheShippedData()
        {
            Own();
            CarryAllSupplies();
            var station = NewStation();
            double start = _clock.TotalSeconds;

            int expectedTicks = 0;
            foreach (var job in _station.Mending.Jobs)
            {
                double secondsHeld = job.WorkGameMinutes / _station.GameMinutesPerHeldSecond;
                expectedTicks += (int)Math.Ceiling(secondsHeld / Tick - 1e-9);
            }
            int ticks = 0;
            foreach (DoryMendingJobDef job in JobsInOrder(_station.Mending))
            {
                Assert.AreEqual(job.DisplayName, station.VerbLabel, "the verb names the job in hand");
                ticks += WorkOneJob(station);
                Assert.IsTrue(_save.GetFlag(job.CompletedFlag), "each finished job writes its own flag");
            }
            Assert.AreEqual(expectedTicks, ticks, "held seconds become game minutes at the data's rate");

            CollectionAssert.Contains(_save.Current.RepairedBoats, BoatId, "mended: the week's gate sees her repaired");
            double minutes = 0;
            foreach (var job in _station.Mending.Jobs) minutes += job.WorkGameMinutes;
            Assert.AreEqual(minutes * _config.SecondsPerDay / (24.0 * 60.0), _clock.TotalSeconds - start, 1e-3,
                "the clock moved by exactly the work done");
            foreach (var job in _station.Mending.Jobs)
                foreach (var m in job.Materials)
                    Assert.AreEqual(0, SupplyLocker.Owned(_save.Current, m.ItemId), m.ItemId + " is spent");
            Assert.AreEqual(_station.LaunchLabel, station.VerbLabel, "mended, the verb is the launch");
            Assert.AreEqual(_station.MendedLine, station.LastLine);
            Assert.IsTrue(SlipDoryStory.IsOnTheSlip(_save), "mending alone does not launch her");
        }

        [Test]
        public void LettingGo_PausesTheJob_AndTheNextPressResumesIt()
        {
            Own();
            CarryAllSupplies();
            var station = NewStation();
            DoryMendingJobDef first = _station.Mending.Jobs[0];
            Assert.AreEqual(DoryMendingStationResult.Working, station.Press(null, _hands));
            station.TickHeld(true, true, Tick);
            station.TickHeld(false, true, Tick);
            Assert.IsFalse(station.IsWorking, "letting go stops the work");
            Assert.AreEqual(_station.PausedLine, station.LastLine);
            double paused = _clock.TotalSeconds;
            station.TickHeld(true, true, Tick);
            Assert.AreEqual(paused, _clock.TotalSeconds, "no unattended work");

            Assert.AreEqual(DoryMendingStationResult.Working, station.Press(null, _hands));
            station.TickHeld(true, false, Tick);
            Assert.IsFalse(station.IsWorking, "walking off stops the work too");
            Assert.IsFalse(_save.GetFlag(first.CompletedFlag));
            Assert.AreEqual(1, ShoppingBagContents.InBag(_save.Current, first.Materials[0].ItemId),
                "an unfinished job spends nothing");
        }

        [Test]
        public void ALoad_DropsUnfinishedWork_ButNotTheGoods()
        {
            Own();
            CarryAllSupplies();
            var station = NewStation();
            Assert.AreEqual(DoryMendingStationResult.Working, station.Press(null, _hands));
            station.TickHeld(true, true, Tick);
            EventBus.Publish(new GameLoaded());
            Assert.IsFalse(station.IsWorking, "a load ends the session");
            string id = _station.Mending.Jobs[0].Materials[0].ItemId;
            Assert.AreEqual(1, ShoppingBagContents.InBag(_save.Current, id), "the goods are still his");
            Assert.AreEqual(1, SupplyLocker.Owned(_save.Current, id));
        }

        // ---- the launch ----------------------------------------------------------------------------

        [Test]
        public void TheLaunch_NeedsHerHisAndMended()
        {
            Assert.AreEqual(SlipDoryLaunchResult.NotHis, SlipDoryStory.TryLaunch(_save, BoatId, true));
            _save.Current.OwnedBoats.Add(BoatId);
            Assert.AreEqual(SlipDoryLaunchResult.NotMended, SlipDoryStory.TryLaunch(_save, BoatId, true));
            Assert.AreEqual(SlipDoryLaunchResult.NoGame, SlipDoryStory.TryLaunch(null, BoatId, true));
            Assert.IsTrue(SlipDoryStory.IsOnTheSlip(_save), "a refused launch keeps her on the slip");
            Assert.AreEqual(0, _save.Writes, "and writes nothing");
            Assert.IsEmpty(_launched);
            Assert.IsEmpty(_where.Placed);
        }

        [Test]
        public void TheLaunch_ClearsTheFlagOnce_AndPutsHerAfloatAtHerMooring()
        {
            Own();
            CarryAllSupplies();
            var station = NewStation();
            MendHer(station);
            int writes = _save.Writes;

            Assert.AreEqual(DoryMendingStationResult.Launched, station.Press(null, _hands));
            Assert.IsFalse(SlipDoryStory.IsOnTheSlip(_save), "launched: the slip flag is cleared");
            Assert.IsFalse(SlipDoryStory.WithholdsPlayersBoat(_save), "and his boat is no longer kept out");
            Assert.AreEqual(1, _launched.Count, "the launch is said once");
            Assert.AreEqual(BoatId, _launched[0].BoatId);
            Assert.AreEqual(writes + 1, _save.Writes, "one write for the launch");
            Assert.AreEqual(BoatId, _save.Current.ActiveHullId, "she is his boat");
            CollectionAssert.Contains(_save.Current.OwnedBoats, BoatId);
            CollectionAssert.Contains(_save.Current.RepairedBoats, BoatId);

            Assert.AreEqual(1, _where.Placed.Count, "his boat is placed once");
            Whereabouts at = _where.Placed[0];
            Assert.AreEqual(_station.LaunchRegionId, at.RegionId);
            Assert.AreEqual(_station.LaunchBerth.x, at.X, 1e-4);
            Assert.AreEqual(_station.LaunchBerth.y, at.Y, 1e-4);
            Assert.AreEqual(_station.LaunchHeadingDegrees, at.HeadingDegrees, 1e-4);
            Assert.AreEqual(_station.LaunchedLine, station.LastLine);

            Assert.IsFalse(station.IsAvailable, "the slip has nothing left to offer");
            CollectionAssert.DoesNotContain(Interactables.Active, station);
            Assert.AreEqual(DoryMendingStationResult.NotAvailable, station.Press(null, _hands));
            Assert.AreEqual(SlipDoryLaunchResult.NotOnTheSlip, SlipDoryStory.TryLaunch(_save, BoatId, true));
            Assert.AreEqual(1, _launched.Count, "never twice");
            Assert.AreEqual(1, _where.Placed.Count);
        }

        [Test]
        public void ALoadedGame_NeverReplaysTheLaunch()
        {
            Own();
            CarryAllSupplies();
            var station = NewStation();
            MendHer(station);
            Assert.AreEqual(DoryMendingStationResult.Launched, station.Press(null, _hands));

            // The same save, through a JSON round trip, as a load gives it back.
            var loaded = new MemorySave(JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(_save.Current)));
            GameServices.Save = loaded;
            EventBus.Publish(new GameLoaded());
            var again = NewStation();

            Assert.AreEqual(1, _launched.Count, "a load does not launch her again");
            Assert.IsFalse(SlipDoryStory.IsOnTheSlip(loaded));
            Assert.IsFalse(SlipDoryStory.WithholdsPlayersBoat(loaded), "his boat stays in the world");
            CollectionAssert.Contains(loaded.Current.RepairedBoats, BoatId);
            CollectionAssert.Contains(loaded.Current.OwnedBoats, BoatId);
            Assert.AreEqual(BoatId, loaded.Current.ActiveHullId);
            Assert.IsFalse(again.IsAvailable);
            Assert.AreEqual(0, loaded.Writes, "a load writes nothing for her");
        }

        [Test]
        public void AGuest_LaunchesNothing_AndWritesNothing()
        {
            Own();
            CarryAllSupplies();
            var station = NewStation();
            MendHer(station);
            int writes = _save.Writes;

            SlipDoryStory.IsHost = () => false;
            station.Refresh();
            Assert.IsFalse(station.IsAvailable, "the guest is offered nothing: the host owns the save");
            Assert.AreEqual(SlipDoryLaunchResult.NotHost,
                SlipDoryStory.TryLaunch(_save, BoatId, SlipDoryStory.HostHere));
            Assert.IsTrue(SlipDoryStory.IsOnTheSlip(_save));
            Assert.AreEqual(writes, _save.Writes);
            Assert.IsEmpty(_launched);
            Assert.IsEmpty(_where.Placed);
        }

        // ---- the content ---------------------------------------------------------------------------

        [Test]
        public void TheShippedStation_IsWiredToTheSlipDory_AndHerMendingData()
        {
            Assert.That(_station.Id, Does.Match(@"^slipmend\.[a-z0-9]+(_[a-z0-9]+)*$"));
            Assert.IsNotNull(_station.Mending, "the station names no mending sequence");
            Assert.DoesNotThrow(() => _station.Mending.Validate());
            var sale = AssetDatabase.LoadAssetAtPath<SlipBoatSaleDef>(SaleAsset);
            Assert.IsNotNull(sale);
            Assert.AreEqual(sale.BoatId, _station.Mending.BoatId, "the station mends the boat the slip sells");
            Assert.AreEqual(sale.RegionId, _station.Mending.PlaceId);
            Assert.AreEqual(sale.RegionId, _station.LaunchRegionId, "she is launched where she lies");
            Assert.Greater(_station.GameMinutesPerHeldSecond, 0f);
            Assert.Greater(_station.WorkTickSeconds, 0f);
            foreach (string line in new[] { _station.MendLabel, _station.LaunchLabel, _station.WorkLine, _station.PausedLine,
                                            _station.JobDoneLine, _station.MendedLine, _station.MissingLine, _station.LaunchedLine })
            {
                Assert.IsFalse(string.IsNullOrWhiteSpace(line), "a station word is empty");
                StringAssert.DoesNotContain("pail", line.ToLowerInvariant(), "the word is bucket");
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabAsset);
            Assert.IsNotNull(prefab, PrefabAsset + " is missing");
            var placed = prefab.GetComponent<DoryMendingStation>();
            Assert.IsNotNull(placed, "the slip dory prefab carries no mending station");
            Assert.AreSame(_station, placed.Station, "the prefab's station is the shipped one");
            Assert.IsNotNull(prefab.GetComponent<SlipDory>(), "and it sits on the slip dory itself");
        }

        [Test]
        public void TheLaunchBerth_IsHerMooring_WhichFloatsHerAtEveryTideOfTheFirstWeek()
        {
            Vector3 mooring = StPetersBuilder.DoryMooredPos;
            Assert.AreEqual(mooring.x, _station.LaunchBerth.x, 1e-3, "she is launched to the dory's own mooring");
            Assert.AreEqual(mooring.y, _station.LaunchBerth.y, 1e-3,
                "the rig puts her there after every load, so any other spot would not survive one");
            Assert.AreEqual(Mathf.DeltaAngle(StPetersBuilder.DoryMooredHeadingDegrees, _station.LaunchHeadingDegrees), 0f, 1e-3);

            var hull = AssetDatabase.LoadAssetAtPath<HiddenHarbours.Boats.BoatHullDef>(DoryHull);
            Assert.IsNotNull(hull, DoryHull + " is missing");
            var region = AssetDatabase.LoadAssetAtPath<RegionDef>(RegionAsset);
            var profile = new TideProfile
            {
                MeanLevel = region.TideMeanLevel, Amplitude = region.TideAmplitude, PhaseHours = region.TidePhaseHours,
            };
            float low = float.MaxValue;
            const int weekDays = 8;
            for (double t = 0; t <= _config.SecondsPerDay * weekDays; t += 5.0)
                low = Mathf.Min(low, TideModel.Height(t, profile, _config));

            var go = new GameObject("StPetersTerrain_LaunchTest");
            _spawned.Add(go);
            var terrain = go.AddComponent<TidalTerrain>();
            StPetersBuilder.ConfigureTidalTerrain(terrain);
            float bed = terrain.ElevationAt(_station.LaunchBerth);
            Assert.Less(bed + hull.DraughtMeters, low,
                $"the berth's bed ({bed:F2} m) would ground a {hull.DraughtMeters} m dory at {low:F2} m water");
        }
    }
}
