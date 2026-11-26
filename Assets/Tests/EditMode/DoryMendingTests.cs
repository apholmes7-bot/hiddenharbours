using System;
using System.Collections.Generic;
using HiddenHarbours.Core;
using HiddenHarbours.Economy;
using HiddenHarbours.Environment;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    public sealed class DoryMendingTests
    {
        private const string Place = "region.st_peters";
        private sealed class Clock : IGameClock
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
            public bool Refuse;
            public Action DuringAdvance;
            public bool FastForwardTo(double target)
            {
                if (Refuse || IsHeld || IsPaused || target <= TotalSeconds) return false;
                DuringAdvance?.Invoke();
                TotalSeconds = target;
                return true;
            }
        }

        private sealed class Tide : IEnvironmentService
        {
            public GameConfig Config;
            public int WorldSeed { get; set; }
            public TideProfile ActiveTideProfile { get; set; } = TideProfile.CoddleCove;
            public EnvironmentSample Sample() => default;
            public float TideHeightAt(double seconds) => TideModel.Height(seconds, ActiveTideProfile, Config);
        }

        private sealed class Saved : ISaveService
        {
            public SaveData Current { get; set; } = new SaveData();
            public int Writes;
            public Action OnWrite;
            private int _depth;
            private bool _pending;
            public bool GetFlag(string key)
            {
                foreach (var flag in Current.OnboardingFlags) if (flag.Key == key) return flag.Value;
                return false;
            }
            public void SetFlag(string key, bool value)
            {
                for (int i = 0; i < Current.OnboardingFlags.Count; i++)
                    if (Current.OnboardingFlags[i].Key == key)
                    { Current.OnboardingFlags[i] = new SaveFlag(key, value); Save(); return; }
                Current.OnboardingFlags.Add(new SaveFlag(key, value));
                Save();
            }
            public void Save()
            {
                if (_depth > 0) { _pending = true; return; }
                Writes++;
                OnWrite?.Invoke();
            }
            public IDisposable DeferWrites() { _depth++; return new Scope(this); }
            private sealed class Scope : IDisposable
            {
                private Saved _owner;
                public Scope(Saved owner) { _owner = owner; }
                public void Dispose()
                {
                    if (_owner == null) return;
                    var owner = _owner; _owner = null;
                    if (--owner._depth == 0 && owner._pending)
                    { owner._pending = false; owner.Save(); }
                }
            }
        }

        private sealed class Carry : IMendingCarry
        {
            public readonly Dictionary<string, int> Stock = new Dictionary<string, int>();
            public bool Refuse;
            public bool RefuseCommit;
            public int Consumes;
            public int Reservations;
            private readonly Dictionary<string, int> _reserved = new Dictionary<string, int>();
            public int Owned(string itemId) => Stock.TryGetValue(itemId, out int count) ? count : 0;
            public int Count(string itemId) => Owned(itemId) - (_reserved.TryGetValue(itemId, out int n) ? n : 0);
            public bool TryReserve(IReadOnlyList<MendingMaterial> materials, out IMendingReservation reservation)
            {
                reservation = null;
                if (Refuse) return false;
                foreach (var material in materials) if (Count(material.ItemId) < material.Count) return false;
                var copy = new List<MendingMaterial>(materials);
                foreach (var material in copy)
                    _reserved[material.ItemId] = (_reserved.TryGetValue(material.ItemId, out int n) ? n : 0) + material.Count;
                reservation = new Reservation(this, copy);
                Reservations++;
                return true;
            }
            private sealed class Reservation : IMendingReservation
            {
                private readonly Carry _owner;
                private readonly List<MendingMaterial> _materials;
                private bool _closed;
                public Reservation(Carry owner, List<MendingMaterial> materials) { _owner = owner; _materials = materials; }
                public bool IsValid
                {
                    get
                    {
                        if (_closed) return false;
                        foreach (var material in _materials)
                            if (_owner.Owned(material.ItemId) < _owner._reserved[material.ItemId]) return false;
                        return true;
                    }
                }
                public bool TryConsume()
                {
                    if (!IsValid || _owner.RefuseCommit) return false;
                    foreach (var material in _materials) _owner.Stock[material.ItemId] -= material.Count;
                    _owner.Consumes++;
                    Dispose();
                    return true;
                }
                public void Dispose()
                {
                    if (_closed) return;
                    _closed = true;
                    foreach (var material in _materials) _owner._reserved[material.ItemId] -= material.Count;
                }
            }
        }

        private readonly List<UnityEngine.Object> _objects = new List<UnityEngine.Object>();
        private GameConfig _config;
        private DoryMendingDef _def;
        private Clock _clock;
        private Tide _tide;
        private Saved _save;
        private Carry _carry;
        private DoryMendingSession _session;
        private readonly List<string> _events = new List<string>();
        private int _ready;
        private double Hour => _config.SecondsPerDay / 24.0;
        private double Minute => Hour / 60;

        private T Make<T>() where T : ScriptableObject
        {
            var value = ScriptableObject.CreateInstance<T>(); _objects.Add(value); return value;
        }

        [SetUp]
        public void SetUp()
        {
            _config = Make<GameConfig>();
            _def = Make<DoryMendingDef>();
            _def.Id = "mending.st_peters_dory"; _def.BoatId = "boat.dory"; _def.PlaceId = Place;
            _def.WorkBelowMetres = -0.2f; _def.LaunchAboveMetres = 0.8f;
            _def.Jobs = new DoryMendingJobDef[3];
            string[] keys = { "patch_plank", "caulk_seam", "paint" };
            string[] materials = { "supply.patch_plank", "supply.oakum", "supply.linseed_paint" };
            for (int i = 0; i < 3; i++)
            {
                var job = Make<DoryMendingJobDef>();
                job.Id = "mendjob." + keys[i]; job.DisplayName = keys[i]; job.Order = i;
                job.CompletedFlag = "st_peters.dory_mend." + keys[i]; job.WorkGameMinutes = 30;
                job.Materials = new[] { new MendingMaterial(materials[i], 1) };
                _def.Jobs[i] = job;
            }
            _def.Jobs[0].Materials = new[] { new MendingMaterial(materials[0], 1), new MendingMaterial("supply.copper_nails", 1) };
            _save = new Saved(); _save.Current.OwnedBoats.Add(_def.BoatId);
            _clock = new Clock { TotalSeconds = 9 * Hour };
            _tide = new Tide { Config = _config };
            _carry = new Carry();
            foreach (var job in _def.Jobs) foreach (var material in job.Materials) _carry.Stock[material.ItemId] = material.Count;
            _session = NewSession();
            _events.Clear(); _ready = 0;
            EventBus.Subscribe<DoryMendingJobStarted>(Started);
            EventBus.Subscribe<DoryMendingJobDone>(Done);
            EventBus.Subscribe<DoryMended>(Mended);
            EventBus.Subscribe<BoatRepaired>(Repaired);
            EventBus.Subscribe<DoryReadyToLaunch>(Ready);
        }

        [TearDown]
        public void TearDown()
        {
            _session?.Dispose();
            EventBus.Unsubscribe<DoryMendingJobStarted>(Started);
            EventBus.Unsubscribe<DoryMendingJobDone>(Done);
            EventBus.Unsubscribe<DoryMended>(Mended);
            EventBus.Unsubscribe<BoatRepaired>(Repaired);
            EventBus.Unsubscribe<DoryReadyToLaunch>(Ready);
            foreach (var value in _objects) UnityEngine.Object.DestroyImmediate(value);
            _objects.Clear();
        }

        private DoryMendingSession NewSession() => new DoryMendingSession(_def, _save, _clock, _tide, _carry, _config);
        private void Started(DoryMendingJobStarted e) => _events.Add("start:" + e.JobId);
        private void Done(DoryMendingJobDone e) => _events.Add("done:" + e.JobId);
        private void Mended(DoryMended e) => _events.Add("mended:" + e.BoatId);
        private void Repaired(BoatRepaired e) => _events.Add("repaired:" + e.BoatId + ":" + e.PricePaid);
        private void Ready(DoryReadyToLaunch e) { _ready++; }
        private void Start(int index = 0)
        {
            Assert.That(_session.TryStart(_def.Jobs[index].Id, Place, out var why), Is.True, why.TextKey());
        }
        private void Finish(int index = 0)
        {
            Start(index);
            Assert.That(_session.WorkFor(30, Place, out var why), Is.True, why.TextKey());
        }

        // An in-memory snapshot of the test carrier and clock in the existing save rows. This is
        // deliberately not a production bag adapter or a disk-save integration test.
        private string CaptureState()
        {
            var snapshot = JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(_save.Current));
            snapshot.GameTimeSeconds = _clock.TotalSeconds;
            snapshot.SupplyStock.Clear();
            foreach (var entry in _carry.Stock)
                if (entry.Value > 0) snapshot.SupplyStock.Add(new SupplyStock(entry.Key, entry.Value));
            return JsonUtility.ToJson(snapshot);
        }

        private void RestoreState(string json)
        {
            _session.Dispose();
            _save.Current = JsonUtility.FromJson<SaveData>(json);
            _clock.TotalSeconds = _save.Current.GameTimeSeconds;
            _carry = new Carry();
            foreach (var row in _save.Current.SupplyStock) _carry.Stock[row.SupplyId] = row.Count;
            _session = NewSession();
        }

        [Test]
        public void StartReservesWithoutDebitAndAnotherSessionCannotSpendTheClaim()
        {
            Start();
            Assert.That(_session.Snapshot.MaterialsReserved, Is.True);
            Assert.That(_carry.Owned("supply.patch_plank"), Is.EqualTo(1));
            Assert.That(_carry.Owned("supply.copper_nails"), Is.EqualTo(1));
            Assert.That(_carry.Count("supply.patch_plank"), Is.Zero);
            Assert.That(_carry.Count("supply.copper_nails"), Is.Zero);
            Assert.That(_carry.Consumes, Is.Zero);
            Assert.That(_carry.Reservations, Is.EqualTo(1));
            Assert.That(_save.Writes, Is.Zero);
            using (var other = NewSession())
            {
                Assert.That(other.TryStart(_def.Jobs[0].Id, Place, out var why), Is.False);
                Assert.That(why, Is.EqualTo(DoryMendReason.MissingMaterials));
                Assert.That(other.Snapshot.MaterialsReserved, Is.False);
            }
            _session.Dispose();
            _session.Dispose();
            Assert.That(_carry.Count("supply.patch_plank"), Is.EqualTo(1));
            Assert.That(_carry.Count("supply.copper_nails"), Is.EqualTo(1));
            Assert.That(_carry.Consumes, Is.Zero);
            Assert.That(_session.TryStart(_def.Jobs[0].Id, Place, out var closed), Is.False);
            Assert.That(closed, Is.EqualTo(DoryMendReason.SessionChanged));
        }

        [TestCase(0, false)]
        [TestCase(10, false)]
        [TestCase(10, true)]
        public void RestartDropsOnlyUnfinishedTimeAndKeepsEveryReservedMaterial(int minutes, bool paused)
        {
            Start();
            if (minutes > 0) Assert.That(_session.WorkFor(minutes, Place, out _), Is.True);
            if (paused) _session.Pause();
            Assert.That(_session.Snapshot.WorkedSeconds, Is.EqualTo(minutes * Minute));
            Assert.That(_carry.Consumes, Is.Zero);
            string saved = CaptureState();
            RestoreState(saved);
            Assert.That(_session.Snapshot.JobId, Is.Null);
            Assert.That(_session.Snapshot.WorkedSeconds, Is.Zero);
            Assert.That(_session.Snapshot.MaterialsReserved, Is.False);
            Assert.That(_save.Current.OnboardingFlags, Is.Empty);
            foreach (var job in _def.Jobs)
                foreach (var material in job.Materials)
                    Assert.That(_carry.Count(material.ItemId), Is.EqualTo(material.Count));
            double before = _clock.TotalSeconds;
            Finish();
            Assert.That(_clock.TotalSeconds - before, Is.EqualTo(30 * Minute));
            Assert.That(_carry.Consumes, Is.EqualTo(1));
            Assert.That(_carry.Owned("supply.patch_plank"), Is.Zero);
            Assert.That(_carry.Owned("supply.copper_nails"), Is.Zero);
            Assert.That(_save.GetFlag(_def.Jobs[0].CompletedFlag), Is.True);
        }

        [Test]
        public void PauseKeepsTheSameReservationUntilCompletion()
        {
            Start();
            Assert.That(_session.WorkFor(10, Place, out _), Is.True);
            _session.Pause();
            Assert.That(_carry.Consumes, Is.Zero);
            Assert.That(_carry.Owned("supply.patch_plank"), Is.EqualTo(1));
            Assert.That(_carry.Count("supply.patch_plank"), Is.Zero);
            Start();
            Assert.That(_carry.Reservations, Is.EqualTo(1));
            Assert.That(_session.WorkFor(20, Place, out _), Is.True);
            Assert.That(_carry.Consumes, Is.EqualTo(1));
            Assert.That(_session.Snapshot.MaterialsReserved, Is.False);
        }

        [Test]
        public void ARefusedCompletionDebitsNothingAndCanRetryWithoutRepeatingTheWork()
        {
            Start();
            _carry.RefuseCommit = true;
            Assert.That(_session.WorkFor(30, Place, out var why), Is.False);
            Assert.That(why, Is.EqualTo(DoryMendReason.MissingMaterials));
            Assert.That(_session.Snapshot.WorkedSeconds, Is.EqualTo(30 * Minute));
            Assert.That(_session.Snapshot.Working, Is.False);
            Assert.That(_carry.Consumes, Is.Zero);
            Assert.That(_carry.Owned("supply.patch_plank"), Is.EqualTo(1));
            Assert.That(_carry.Owned("supply.copper_nails"), Is.EqualTo(1));
            Assert.That(_save.Current.OnboardingFlags, Is.Empty);
            Assert.That(_save.Current.RepairedBoats, Is.Empty);
            CollectionAssert.AreEqual(new[] { "start:mendjob.patch_plank" }, _events);
            _carry.RefuseCommit = false;
            double before = _clock.TotalSeconds;
            Start();
            Assert.That(_session.WorkFor(1, Place, out why), Is.True);
            Assert.That(why, Is.EqualTo(DoryMendReason.None));
            Assert.That(_clock.TotalSeconds, Is.EqualTo(before));
            Assert.That(_carry.Consumes, Is.EqualTo(1));
            Assert.That(_carry.Reservations, Is.EqualTo(1));
            CollectionAssert.AreEqual(new[] { "start:mendjob.patch_plank", "done:mendjob.patch_plank" }, _events);
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void ClockAutosaveSeesCompletedFlagsAndMatchingGoodsTogether(int completedJobs)
        {
            string stored = null;
            _save.OnWrite = () => stored = CaptureState();
            _clock.DuringAdvance = () => _save.Save();
            for (int i = 0; i < completedJobs; i++) Finish(i);
            Assert.That(_save.Writes, Is.EqualTo(completedJobs));
            Assert.That(_carry.Consumes, Is.EqualTo(completedJobs));
            Assert.That(stored, Is.Not.Null);
            RestoreState(stored);
            Assert.That(_save.Current.OnboardingFlags.Count, Is.EqualTo(completedJobs));
            Assert.That(RepairLedger.IsRepaired(_save.Current, _def.BoatId), Is.EqualTo(completedJobs == 3));
            Assert.That(_clock.TotalSeconds, Is.EqualTo(9 * Hour + completedJobs * 30 * Minute));
            for (int i = 0; i < _def.Jobs.Length; i++)
                foreach (var material in _def.Jobs[i].Materials)
                    Assert.That(_carry.Owned(material.ItemId), Is.EqualTo(i < completedJobs ? 0 : material.Count));
            Assert.That(_session.NextJob, Is.EqualTo(completedJobs == 3 ? null : _def.Jobs[completedJobs]));
            Assert.That(_session.Snapshot.MaterialsReserved, Is.False);
        }

        [Test]
        public void FailedSnapshotWriteLeavesMatchingCompletionAndDebitWithoutASecondCharge()
        {
            Start();
            _save.OnWrite = () => throw new InvalidOperationException("snapshot unavailable");
            Assert.Throws<InvalidOperationException>(() => _session.WorkFor(30, Place, out _));
            Assert.That(_save.GetFlag(_def.Jobs[0].CompletedFlag), Is.True);
            Assert.That(_carry.Consumes, Is.EqualTo(1));
            Assert.That(_carry.Owned("supply.patch_plank"), Is.Zero);
            Assert.That(_carry.Owned("supply.copper_nails"), Is.Zero);
            Assert.That(_session.Snapshot.MaterialsReserved, Is.False);
            Assert.That(_session.Snapshot.JobId, Is.Null);
            CollectionAssert.AreEqual(new[] { "start:mendjob.patch_plank" }, _events);
            Assert.That(_session.TryStart(_def.Jobs[0].Id, Place, out var why), Is.False);
            Assert.That(why, Is.EqualTo(DoryMendReason.JobAlreadyDone));
            string stored = null;
            _save.OnWrite = () => stored = CaptureState();
            _save.Save();
            RestoreState(stored);
            Assert.That(_save.GetFlag(_def.Jobs[0].CompletedFlag), Is.True);
            Assert.That(_carry.Owned("supply.patch_plank"), Is.Zero);
            Assert.That(_carry.Owned("supply.copper_nails"), Is.Zero);
        }

        [Test]
        public void DisposeDuringClockCallbacksReleasesGoodsAndCannotFinishTheJob()
        {
            Start();
            _clock.DuringAdvance = () => _session.Dispose();
            Assert.That(_session.WorkFor(30, Place, out var why), Is.False);
            Assert.That(why, Is.EqualTo(DoryMendReason.SessionChanged));
            Assert.That(_carry.Consumes, Is.Zero);
            Assert.That(_carry.Count("supply.patch_plank"), Is.EqualTo(1));
            Assert.That(_carry.Count("supply.copper_nails"), Is.EqualTo(1));
            Assert.That(_save.Current.OnboardingFlags, Is.Empty);
        }

        [Test]
        public void EndingPlayOnTheSameSaveReleasesTheClaimAndDiscardsPartialTime()
        {
            Start();
            Assert.That(_session.WorkFor(10, Place, out _), Is.True);
            _session.Pause();
            var sameSave = _save.Current;
            _session.Dispose();
            _session = NewSession();
            Assert.That(_save.Current, Is.SameAs(sameSave));
            Assert.That(_session.Snapshot.WorkedSeconds, Is.Zero);
            Assert.That(_carry.Count("supply.patch_plank"), Is.EqualTo(1));
            Assert.That(_carry.Count("supply.copper_nails"), Is.EqualTo(1));
            Finish();
            Assert.That(_carry.Consumes, Is.EqualTo(1));
        }

        [Test]
        public void InvalidatedCarrierCannotAdvanceTimeOrRecordCompletion()
        {
            Start();
            _carry.Stock["supply.copper_nails"] = 0;
            double before = _clock.TotalSeconds;
            Assert.That(_session.WorkFor(30, Place, out var why), Is.False);
            Assert.That(why, Is.EqualTo(DoryMendReason.MissingMaterials));
            Assert.That(_clock.TotalSeconds, Is.EqualTo(before));
            Assert.That(_carry.Owned("supply.patch_plank"), Is.EqualTo(1));
            Assert.That(_carry.Consumes, Is.Zero);
            Assert.That(_save.Current.OnboardingFlags, Is.Empty);
        }

        [Test]
        public void AChangedTideConfigurationCannotReuseTheOldSafetyBound()
        {
            Start();
            _config.TidalPeriodHours /= 2;
            double before = _clock.TotalSeconds;
            Assert.That(_session.WorkFor(30, Place, out var why), Is.False);
            Assert.That(why, Is.EqualTo(DoryMendReason.SessionChanged));
            Assert.That(_clock.TotalSeconds, Is.EqualTo(before));
            Assert.That(_carry.Count("supply.patch_plank"), Is.EqualTo(1));
            Assert.That(_carry.Count("supply.copper_nails"), Is.EqualTo(1));
            Assert.That(_carry.Consumes, Is.Zero);
        }

        [Test]
        public void OwnershipLostDuringClockCallbacksCannotDebitMaterials()
        {
            Start();
            _clock.DuringAdvance = () => _save.Current.OwnedBoats.Clear();
            Assert.That(_session.WorkFor(30, Place, out var why), Is.False);
            Assert.That(why, Is.EqualTo(DoryMendReason.BoatNotOwned));
            Assert.That(_carry.Count("supply.patch_plank"), Is.EqualTo(1));
            Assert.That(_carry.Count("supply.copper_nails"), Is.EqualTo(1));
            Assert.That(_save.Current.OnboardingFlags, Is.Empty);
            Assert.That(_carry.Consumes, Is.Zero);
        }

        [TestCase("lunar_zero")]
        [TestCase("lunar_infinite")]
        [TestCase("neap_negative")]
        [TestCase("neap_above_one")]
        [TestCase("amplitude_nan")]
        public void InvalidTideParametersCannotCreateASafetyBound(string fault)
        {
            switch (fault)
            {
                case "lunar_zero": _config.LunarMonthDays = 0; break;
                case "lunar_infinite": _config.LunarMonthDays = float.PositiveInfinity; break;
                case "neap_negative": _config.NeapAmplitudeFraction = -1; break;
                case "neap_above_one": _config.NeapAmplitudeFraction = 2; break;
                case "amplitude_nan": _tide.ActiveTideProfile = new TideProfile { Amplitude = float.NaN }; break;
            }
            Assert.Throws<ArgumentException>(() => NewSession());
            Assert.That(_carry.Consumes, Is.Zero);
        }

        [TestCase(DoryMendReason.WrongPlace)]
        [TestCase(DoryMendReason.BoatNotOwned)]
        [TestCase(DoryMendReason.BoatAlreadyRepaired)]
        [TestCase(DoryMendReason.UnknownJob)]
        [TestCase(DoryMendReason.EarlierJobsIncomplete)]
        [TestCase(DoryMendReason.MissingMaterials)]
        [TestCase(DoryMendReason.LowWaterRequired)]
        [TestCase(DoryMendReason.ClockUnavailable)]
        [TestCase(DoryMendReason.SessionChanged)]
        [TestCase(DoryMendReason.InvalidProgress)]
        public void StartRefusalsChangeNothing(DoryMendReason expected)
        {
            string job = _def.Jobs[0].Id, place = Place;
            switch (expected)
            {
                case DoryMendReason.WrongPlace: place = "region.elsewhere"; break;
                case DoryMendReason.BoatNotOwned: _save.Current.OwnedBoats.Clear(); break;
                case DoryMendReason.BoatAlreadyRepaired: RepairLedger.MarkRepaired(_save.Current, _def.BoatId); break;
                case DoryMendReason.UnknownJob: job = "mendjob.unknown"; break;
                case DoryMendReason.EarlierJobsIncomplete: job = _def.Jobs[1].Id; break;
                case DoryMendReason.MissingMaterials: _carry.Stock["supply.copper_nails"] = 0; break;
                case DoryMendReason.LowWaterRequired: _clock.TotalSeconds = 3 * Hour; break;
                case DoryMendReason.ClockUnavailable: _clock.IsHeld = true; break;
                case DoryMendReason.SessionChanged: _save.Current = new SaveData(); break;
                case DoryMendReason.InvalidProgress: _save.SetFlag(_def.Jobs[1].CompletedFlag, true); break;
            }
            double time = _clock.TotalSeconds;
            string saveBefore = JsonUtility.ToJson(_save.Current);
            Assert.That(_session.TryStart(job, place, out var reason), Is.False);
            Assert.That(reason, Is.EqualTo(expected));
            Assert.That(_clock.TotalSeconds, Is.EqualTo(time));
            Assert.That(JsonUtility.ToJson(_save.Current), Is.EqualTo(saveBefore));
            Assert.That(_carry.Consumes, Is.Zero);
            Assert.That(_events, Is.Empty);
        }

        [Test]
        public void MissingSecondMaterialAndAtomicCarryRefusalConsumeNothing()
        {
            _carry.Refuse = true;
            Assert.That(_session.CheckStart(_def.Jobs[0].Id, Place), Is.EqualTo(DoryMendReason.None));
            Assert.That(_session.TryStart(_def.Jobs[0].Id, Place, out var why), Is.False);
            Assert.That(why, Is.EqualTo(DoryMendReason.MissingMaterials));
            Assert.That(_carry.Count("supply.patch_plank"), Is.EqualTo(1));
            Assert.That(_carry.Count("supply.copper_nails"), Is.EqualTo(1));
            Assert.That(_session.Snapshot.MaterialsReserved, Is.False);
        }

        [Test]
        public void WholeSequenceOverThreeLowWatersMarksRepairedExactlyOnce()
        {
            double worked = 0;
            for (int i = 0; i < 3; i++)
            {
                _clock.TotalSeconds = (9 + i * _config.TidalPeriodHours) * Hour;
                double before = _clock.TotalSeconds;
                Finish(i);
                worked += _clock.TotalSeconds - before;
                Assert.That(_save.GetFlag(_def.Jobs[i].CompletedFlag), Is.True);
                Assert.That(RepairLedger.IsRepaired(_save.Current, _def.BoatId), Is.EqualTo(i == 2));
            }
            Assert.That(worked, Is.EqualTo(90 * Minute).Within(1e-9));
            Assert.That(_carry.Consumes, Is.EqualTo(3));
            Assert.That(_save.Current.RepairedBoats, Is.EqualTo(new[] { "boat.dory" }));
            Assert.That(_save.Current.OnboardingFlags.Count, Is.EqualTo(3));
            CollectionAssert.AreEqual(new[] {
                "start:mendjob.patch_plank", "done:mendjob.patch_plank",
                "start:mendjob.caulk_seam", "done:mendjob.caulk_seam",
                "start:mendjob.paint", "done:mendjob.paint", "repaired:boat.dory:0", "mended:boat.dory"
            }, _events);
            Assert.That(_session.NextJob, Is.Null);
            Assert.That(_session.TryStart(_def.Jobs[2].Id, Place, out var why), Is.False);
            Assert.That(why, Is.EqualTo(DoryMendReason.BoatAlreadyRepaired));
            Assert.That(_events.Count, Is.EqualTo(8));
        }

        [Test]
        public void ShippedJobsFinishAcrossThreeLowsOfTheShippedIslandProfile()
        {
            _def = UnityEditor.AssetDatabase.LoadAssetAtPath<DoryMendingDef>(
                "Assets/_Project/Data/Resources/DoryMending/StPetersDory.asset");
            var region = UnityEditor.AssetDatabase.LoadAssetAtPath<HiddenHarbours.World.RegionDef>(
                "Assets/_Project/Data/Regions/StPeters.asset");
            Assert.That(_def, Is.Not.Null); Assert.That(region, Is.Not.Null);
            _tide.ActiveTideProfile = new TideProfile {
                MeanLevel = region.TideMeanLevel, Amplitude = region.TideAmplitude, PhaseHours = region.TidePhaseHours
            };
            _session = NewSession();
            double totalWork = 0;
            for (int i = 0; i < 3; i++)
            {
                _clock.TotalSeconds = (9 + i * _config.TidalPeriodHours - region.TidePhaseHours) * Hour;
                double before = _clock.TotalSeconds;
                Start(i);
                Assert.That(_session.WorkFor(10000, Place, out var why), Is.True, why.TextKey());
                totalWork += _clock.TotalSeconds - before;
                Assert.That(_save.GetFlag(_def.Jobs[i].CompletedFlag), Is.True);
            }
            Assert.That(totalWork, Is.EqualTo(75 * Minute).Within(1e-8));
            Assert.That(_save.Current.RepairedBoats, Is.EqualTo(new[] { "boat.dory" }));
        }

        [Test]
        public void ShippedDryHullFinishesAtHighWaterButStillNeedsASupportedLaunch()
        {
            _def = UnityEditor.AssetDatabase.LoadAssetAtPath<DoryMendingDef>(
                "Assets/_Project/Data/Resources/DoryMending/StPetersDory.asset");
            var region = UnityEditor.AssetDatabase.LoadAssetAtPath<HiddenHarbours.World.RegionDef>(
                "Assets/_Project/Data/Regions/StPeters.asset");
            Assert.That(_def, Is.Not.Null); Assert.That(region, Is.Not.Null);
            _tide.ActiveTideProfile = new TideProfile {
                MeanLevel = region.TideMeanLevel, Amplitude = region.TideAmplitude, PhaseHours = region.TidePhaseHours
            };
            _session = NewSession();
            _clock.TotalSeconds = (_config.TidalPeriodHours / 4 - region.TidePhaseHours) * Hour;
            double before = _clock.TotalSeconds;
            foreach (var job in _def.Jobs)
            {
                Assert.That(job.NeedsLowWater, Is.False);
                Assert.That(_tide.TideHeightAt(_clock.TotalSeconds), Is.GreaterThan(_def.WorkBelowMetres));
                Assert.That(_session.TryStart(job.Id, Place, out var why), Is.True, why.TextKey());
                Assert.That(_session.WorkFor(10000, Place, out why), Is.True, why.TextKey());
                Assert.That(_save.GetFlag(job.CompletedFlag), Is.True);
            }
            Assert.That(_clock.TotalSeconds - before, Is.EqualTo(75 * Minute).Within(1e-8));
            Assert.That(_carry.Consumes, Is.EqualTo(3));
            foreach (var job in _def.Jobs)
                foreach (var material in job.Materials) Assert.That(_carry.Owned(material.ItemId), Is.Zero);
            Assert.That(_save.Current.RepairedBoats, Is.EqualTo(new[] { "boat.dory" }));
            Assert.That(_events, Is.EqualTo(new[] {
                "start:mendjob.patch_plank", "done:mendjob.patch_plank",
                "start:mendjob.caulk_seam", "done:mendjob.caulk_seam",
                "start:mendjob.paint", "done:mendjob.paint", "repaired:boat.dory:0", "mended:boat.dory"
            }));
            string afterRepair = JsonUtility.ToJson(_save.Current);
            Assert.That(_session.CheckLaunch(Place), Is.EqualTo(DoryMendReason.SupportedLaunchRequired));
            Assert.That(_session.CheckLaunch(Place).TextKey(), Is.EqualTo("dory_mend.bring_hull_to_water"));
            _session.PollLaunch(Place); _session.PollLaunch(Place);
            Assert.That(_ready, Is.Zero);
            Assert.That(JsonUtility.ToJson(_save.Current), Is.EqualTo(afterRepair));
        }

        [TestCase(1f, -0.2f)]
        [TestCase(720f, -0.2f)]
        [TestCase(7200f, -0.2f)]
        [TestCase(720f, 0.9999f)]
        public void CoarseSamplesCannotSkipTheFirstFlood(float stepMinutes, float workLine)
        {
            _config.TidalPeriodHours = 12;
            _config.NeapAmplitudeFraction = 1;
            _tide.ActiveTideProfile = new TideProfile { MeanLevel = 0, Amplitude = 1, PhaseHours = 0 };
            _def.WorkBelowMetres = workLine;
            _def.LaunchAboveMetres = 2;
            _def.SearchStepGameMinutes = stepMinutes;
            _def.Jobs[0].WorkGameMinutes = 600;
            _session = NewSession();
            _clock.TotalSeconds = 9 * Hour;
            double start = _clock.TotalSeconds;
            double firstFlood = (12 + Math.Asin(workLine) * 12 / (2 * Math.PI)) * Hour;
            Start();
            Assert.That(_session.WorkFor(600, Place, out var why), Is.True, why.TextKey());
            // Float-returning heights put a very narrow high-water crossing within 0.02 seconds
            // of its double-precision closed form; the ordinary work line is much tighter.
            double tolerance = workLine > 0 ? 0.02 : 0.00001;
            Assert.That(_clock.TotalSeconds, Is.EqualTo(firstFlood).Within(tolerance));
            Assert.That(_clock.TotalSeconds, Is.LessThanOrEqualTo(firstFlood));
            Assert.That(_session.Snapshot.WorkedSeconds, Is.EqualTo(_clock.TotalSeconds - start).Within(1e-9));
            Assert.That(_session.Snapshot.Working, Is.False);
            Assert.That(_session.Snapshot.MaterialsReserved, Is.True);
            Assert.That(_save.GetFlag(_def.Jobs[0].CompletedFlag), Is.False);
            Assert.That(_carry.Consumes, Is.Zero);
            Assert.That(_carry.Owned("supply.patch_plank"), Is.EqualTo(1));
            Assert.That(_carry.Owned("supply.copper_nails"), Is.EqualTo(1));
        }

        [Test]
        public void RapidLunarEnvelopeCannotHideAnEarlierFloodInsideTheCarrierLow()
        {
            _config.TidalPeriodHours = 12;
            _config.NeapAmplitudeFraction = 0;
            _config.LunarMonthDays = 1.5f;
            _tide.ActiveTideProfile = new TideProfile { MeanLevel = 0, Amplitude = 1, PhaseHours = 0 };
            _def.WorkBelowMetres = -0.001f;
            _def.SearchStepGameMinutes = 720;
            _def.Jobs[0].WorkGameMinutes = 600;
            _session = NewSession();
            _clock.TotalSeconds = 7 * Hour;
            var windows = CrossingMath.FindWindows(_tide.TideHeightAt, _def.WorkBelowMetres,
                0, _config.SecondsPerDay, Minute / 100, _config);
            var expected = windows.Find(w => w.StartSeconds <= _clock.TotalSeconds && _clock.TotalSeconds < w.EndSeconds);
            Assert.That(expected.EndSeconds, Is.GreaterThan(7 * Hour));
            Assert.That(expected.EndSeconds, Is.LessThan(9 * Hour));
            Start();
            Assert.That(_session.WorkFor(600, Place, out _), Is.True);
            Assert.That(_clock.TotalSeconds, Is.EqualTo(expected.EndSeconds).Within(1e-5));
            Assert.That(_save.GetFlag(_def.Jobs[0].CompletedFlag), Is.False);
            Assert.That(_carry.Consumes, Is.Zero);
        }

        [Test]
        public void TideInterruptsLargeRequestAndResumeUsesNoMoreMaterials()
        {
            var windows = CrossingMath.FindWindows(_tide.TideHeightAt, _def.WorkBelowMetres,
                                                   0, _config.SecondsPerDay, Minute, _config);
            double closing = windows[0].EndSeconds;
            _clock.TotalSeconds = closing - 10 * Minute;
            Start();
            Assert.That(_session.WorkFor(5000, Place, out _), Is.True);
            Assert.That(_clock.TotalSeconds, Is.EqualTo(closing).Within(1e-5));
            Assert.That(_session.Snapshot.WorkedSeconds, Is.EqualTo(10 * Minute).Within(1e-5));
            Assert.That(_session.Snapshot.Working, Is.False);
            Assert.That(_save.GetFlag(_def.Jobs[0].CompletedFlag), Is.False);
            _clock.TotalSeconds = closing + Minute;
            Assert.That(_session.TryStart(_def.Jobs[0].Id, Place, out var why), Is.False);
            Assert.That(why, Is.EqualTo(DoryMendReason.LowWaterRequired));
            _clock.TotalSeconds = (9 + _config.TidalPeriodHours) * Hour;
            Start();
            double before = _clock.TotalSeconds;
            Assert.That(_session.WorkFor(5000, Place, out _), Is.True);
            Assert.That(_clock.TotalSeconds - before, Is.EqualTo(20 * Minute).Within(1e-5));
            Assert.That(_carry.Consumes, Is.EqualTo(1));
            CollectionAssert.AreEqual(new[] { "start:mendjob.patch_plank", "done:mendjob.patch_plank" }, _events);
        }

        [Test]
        public void WaitingAcrossATideDoesNotDoUnattendedWork()
        {
            Start();
            Assert.That(_session.WorkFor(5, Place, out _), Is.True);
            _clock.TotalSeconds += _config.TidalPeriodHours * Hour;
            Assert.That(_session.Snapshot.WorkedSeconds, Is.EqualTo(5 * Minute));
            Assert.That(_save.GetFlag(_def.Jobs[0].CompletedFlag), Is.False);
            Assert.That(_session.WorkFor(25, Place, out _), Is.True);
            Assert.That(_save.GetFlag(_def.Jobs[0].CompletedFlag), Is.True);
        }

        [Test]
        public void ClockRefusalAndPauseKeepReservedProgress()
        {
            Start();
            _clock.Refuse = true;
            Assert.That(_session.WorkFor(10, Place, out var why), Is.False);
            Assert.That(why, Is.EqualTo(DoryMendReason.ClockUnavailable));
            Assert.That(_session.Snapshot.WorkedSeconds, Is.Zero);
            _clock.Refuse = false;
            Assert.That(_session.WorkFor(10, Place, out _), Is.True);
            _session.Pause();
            Assert.That(_session.WorkFor(10, Place, out why), Is.False);
            Assert.That(why, Is.EqualTo(DoryMendReason.NotStarted));
            Start();
            Assert.That(_session.WorkFor(20, Place, out _), Is.True);
            Assert.That(_carry.Consumes, Is.EqualTo(1));
        }

        [Test]
        public void WorkCannotBeginOrContinueThroughARefusal()
        {
            Assert.That(_session.WorkFor(10, Place, out var why), Is.False);
            Assert.That(why, Is.EqualTo(DoryMendReason.NotStarted));
            Start();
            _clock.IsPaused = true;
            Assert.That(_session.WorkFor(10, Place, out why), Is.False);
            Assert.That(why, Is.EqualTo(DoryMendReason.ClockUnavailable));
            _clock.IsPaused = false;
            Assert.That(_session.WorkFor(10, "region.elsewhere", out why), Is.False);
            Assert.That(why, Is.EqualTo(DoryMendReason.WrongPlace));
            _clock.TotalSeconds = (3 + _config.TidalPeriodHours) * Hour;
            Assert.That(_session.WorkFor(10, Place, out why), Is.False);
            Assert.That(why, Is.EqualTo(DoryMendReason.LowWaterRequired));
            Assert.That(_session.Snapshot.WorkedSeconds, Is.Zero);
        }

        [Test]
        public void ALoadDuringClockEventsCannotCompleteAJobInAnotherSave()
        {
            Start();
            var original = _save.Current;
            _clock.DuringAdvance = () => _save.Current = new SaveData();
            Assert.That(_session.WorkFor(30, Place, out var why), Is.False);
            Assert.That(why, Is.EqualTo(DoryMendReason.SessionChanged));
            Assert.That(original.OnboardingFlags, Is.Empty);
            Assert.That(_save.Current.OnboardingFlags, Is.Empty);
            Assert.That(_session.Snapshot.Working, Is.False);
            Assert.That(_carry.Consumes, Is.Zero);
            Assert.That(_carry.Count("supply.patch_plank"), Is.EqualTo(1));
            Assert.That(_carry.Count("supply.copper_nails"), Is.EqualTo(1));
        }

        [Test]
        public void OutOfOrderAndRepeatedJobsCannotSpendOrAdvance()
        {
            Start();
            Assert.That(_session.CheckStart(_def.Jobs[0].Id, Place), Is.EqualTo(DoryMendReason.JobInProgress));
            Assert.That(_session.CheckStart(_def.Jobs[2].Id, Place), Is.EqualTo(DoryMendReason.EarlierJobsIncomplete));
            Assert.That(_session.WorkFor(30, Place, out _), Is.True);
            Assert.That(_session.CheckStart(_def.Jobs[0].Id, Place), Is.EqualTo(DoryMendReason.JobAlreadyDone));
            Assert.That(_carry.Consumes, Is.EqualTo(1));
        }

        [Test]
        public void ReentrantRequestsAreRefusedUntilCommitFinishes()
        {
            DoryMendReason nested = DoryMendReason.None;
            Start();
            _clock.DuringAdvance = () => nested = _session.CheckStart(_def.Jobs[0].Id, Place);
            Assert.That(_session.WorkFor(30, Place, out _), Is.True);
            Assert.That(nested, Is.EqualTo(DoryMendReason.Busy));
            Assert.That(_carry.Consumes, Is.EqualTo(1));
        }

        [Test]
        public void LaunchNeedsRepairAndStrictHighWaterAndAnnouncesOnlyReadinessEdges()
        {
            Assert.That(_session.CheckLaunch(Place), Is.EqualTo(DoryMendReason.NotRepaired));
            for (int i = 0; i < 3; i++) Finish(i);
            Assert.That(_session.CheckLaunch(Place), Is.EqualTo(DoryMendReason.HighWaterRequired));
            _session.PollLaunch(Place); Assert.That(_ready, Is.Zero);
            _clock.TotalSeconds = (3 + _config.TidalPeriodHours) * Hour;
            Assert.That(_session.CheckLaunch(Place), Is.EqualTo(DoryMendReason.None));
            _session.PollLaunch(Place); _session.PollLaunch(Place);
            Assert.That(_ready, Is.EqualTo(1));
            _clock.TotalSeconds = (9 + _config.TidalPeriodHours) * Hour;
            _session.PollLaunch(Place);
            _clock.TotalSeconds = (3 + 2 * _config.TidalPeriodHours) * Hour;
            _session.PollLaunch(Place);
            Assert.That(_ready, Is.EqualTo(2));
        }

        [Test]
        public void ExactHeightIsClosedAndAChangedPlaceCannotReuseTheSession()
        {
            _def.WorkBelowMetres = _tide.TideHeightAt(_clock.TotalSeconds);
            Assert.That(_session.CheckStart(_def.Jobs[0].Id, Place), Is.EqualTo(DoryMendReason.LowWaterRequired));
            RepairLedger.MarkRepaired(_save.Current, _def.BoatId);
            _def.LaunchAboveMetres = _tide.TideHeightAt(_clock.TotalSeconds);
            Assert.That(_session.CheckLaunch(Place), Is.EqualTo(DoryMendReason.HighWaterRequired));
            _tide.ActiveTideProfile = TideProfile.FundyRips;
            Assert.That(_session.CheckLaunch(Place), Is.EqualTo(DoryMendReason.WrongPlace));
        }

        [Test]
        public void CompletedFlagsRoundTripThroughTheExistingSaveList()
        {
            Finish();
            string json = JsonUtility.ToJson(_save.Current);
            _save.Current = JsonUtility.FromJson<SaveData>(json);
            _session = NewSession();
            Assert.That(_session.NextJob.Id, Is.EqualTo(_def.Jobs[1].Id));
            Assert.That(_save.Current.OnboardingFlags.Count, Is.EqualTo(1));
            Assert.That(_save.Current.OnboardingFlags[0].Key, Is.EqualTo("st_peters.dory_mend.patch_plank"));
            Assert.That(_save.Current.OnboardingFlags[0].Value, Is.True);
            Assert.That(_session.Snapshot.MaterialsReserved, Is.False);
        }

        [TestCase(0)]
        [TestCase(-1)]
        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        public void InvalidWorkDurationIsARefusal(double minutes)
        {
            Start();
            double before = _clock.TotalSeconds;
            Assert.That(_session.WorkFor(minutes, Place, out var why), Is.False);
            Assert.That(why, Is.EqualTo(DoryMendReason.InvalidWorkDuration));
            Assert.That(_clock.TotalSeconds, Is.EqualTo(before));
            Assert.That(_session.Snapshot.WorkedSeconds, Is.Zero);
        }

        [Test]
        public void NonTidalJobStillNeedsMaterialsAndClockTime()
        {
            _def.Jobs[0].NeedsLowWater = false;
            _clock.TotalSeconds = 3 * Hour;
            double before = _clock.TotalSeconds;
            Finish();
            Assert.That(_clock.TotalSeconds - before, Is.EqualTo(30 * Minute));
            Assert.That(_carry.Consumes, Is.EqualTo(1));
        }

        [TestCase(0)]
        [TestCase(741)]
        public void RepeatedInputProducesIdenticalSnapshotTimeAndEventSequence(int seed)
        {
            _tide.WorldSeed = seed;
            Start(); _session.WorkFor(10, Place, out _);
            var first = _session.Snapshot;
            double firstTime = _clock.TotalSeconds;
            string[] firstEvents = _events.ToArray();
            _clock.TotalSeconds = 9 * Hour;
            _save = new Saved(); _save.Current.OwnedBoats.Add(_def.BoatId);
            _carry.Stock["supply.patch_plank"] = 1; _carry.Stock["supply.copper_nails"] = 1;
            _session.Dispose();
            _events.Clear(); _session = NewSession();
            Start(); _session.WorkFor(10, Place, out _);
            Assert.That(_session.Snapshot, Is.EqualTo(first));
            Assert.That(_clock.TotalSeconds, Is.EqualTo(firstTime));
            CollectionAssert.AreEqual(firstEvents, _events);
        }

        [Test]
        public void JobWithoutAnAuthoredTideRuleKeepsTheLowWaterDefault()
        {
            var job = Make<DoryMendingJobDef>();
            Assert.That(job.NeedsLowWater, Is.True);
            JsonUtility.FromJsonOverwrite("{\"Id\":\"mendjob.legacy\"}", job);
            Assert.That(job.NeedsLowWater, Is.True);
        }

        [Test]
        public void UnauthoredLaunchContractKeepsTheExistingWetSlipDefault()
        {
            var def = Make<DoryMendingDef>();
            Assert.That(def.RequiresSupportedLaunch, Is.False);
            JsonUtility.FromJsonOverwrite("{\"Id\":\"mending.legacy\"}", def);
            Assert.That(def.RequiresSupportedLaunch, Is.False);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void JobTideRuleSurvivesDataSerialization(bool needsLowWater)
        {
            var source = Make<DoryMendingJobDef>();
            source.NeedsLowWater = needsLowWater;
            var restored = Make<DoryMendingJobDef>();
            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(source), restored);
            Assert.That(restored.NeedsLowWater, Is.EqualTo(needsLowWater));
        }

        [TestCase(-1f)]
        [TestCase(0f)]
        [TestCase(1f)]
        public void DryJobCanStartAndFinishBelowAtOrAboveTheWorkLine(float metresAboveLine)
        {
            var job = _def.Jobs[0];
            job.NeedsLowWater = false;
            _def.WorkBelowMetres = _tide.TideHeightAt(_clock.TotalSeconds) - metresAboveLine;
            _def.LaunchAboveMetres = _def.WorkBelowMetres + 1f;
            _session = NewSession();
            double before = _clock.TotalSeconds;

            Assert.That(_session.CheckStart(job.Id, Place), Is.EqualTo(DoryMendReason.None));
            Start();
            Assert.That(_session.WorkFor(job.WorkGameMinutes, Place, out var reason), Is.True);
            Assert.That(reason, Is.EqualTo(DoryMendReason.None));
            Assert.That(_clock.TotalSeconds - before, Is.EqualTo(job.WorkGameMinutes * Minute).Within(1e-9));
            Assert.That(_save.GetFlag(job.CompletedFlag), Is.True);
            Assert.That(_carry.Consumes, Is.EqualTo(1));
            Assert.That(_carry.Count("supply.patch_plank"), Is.Zero);
            Assert.That(_carry.Count("supply.copper_nails"), Is.Zero);
            CollectionAssert.AreEqual(new[] { "start:mendjob.patch_plank", "done:mendjob.patch_plank" }, _events);
        }

        [TestCase(0f)]
        [TestCase(1f)]
        public void LowWaterJobRefusesAtOrAboveTheLineWithTheExistingReason(float metresAboveLine)
        {
            var job = _def.Jobs[0];
            Assert.That(job.NeedsLowWater, Is.True);
            _def.WorkBelowMetres = _tide.TideHeightAt(_clock.TotalSeconds) - metresAboveLine;
            _def.LaunchAboveMetres = _def.WorkBelowMetres + 1f;
            _session = NewSession();
            double before = _clock.TotalSeconds;
            string saveBefore = JsonUtility.ToJson(_save.Current);

            Assert.That(_session.CheckStart(job.Id, Place), Is.EqualTo(DoryMendReason.LowWaterRequired));
            Assert.That(_session.TryStart(job.Id, Place, out var reason), Is.False);
            Assert.That(reason, Is.EqualTo(DoryMendReason.LowWaterRequired));
            Assert.That(reason.TextKey(), Is.EqualTo("dory_mend.wait_for_low_water"));
            Assert.That(_clock.TotalSeconds, Is.EqualTo(before));
            Assert.That(JsonUtility.ToJson(_save.Current), Is.EqualTo(saveBefore));
            Assert.That(_save.Writes, Is.Zero);
            Assert.That(_carry.Consumes, Is.Zero);
            Assert.That(_session.Snapshot.MaterialsReserved, Is.False);
            Assert.That(_events, Is.Empty);
        }

        [Test]
        public void DryJobContinuesAcrossFloodAndStopsAtItsOwnDuration()
        {
            var job = _def.Jobs[0];
            job.NeedsLowWater = false;
            job.WorkGameMinutes = _config.TidalPeriodHours * 60f * 2f;
            var windows = CrossingMath.FindWindows(_tide.TideHeightAt, _def.WorkBelowMetres,
                                                   0, _config.SecondsPerDay, Minute, _config);
            _clock.TotalSeconds = windows[0].EndSeconds - 10 * Minute;
            double before = _clock.TotalSeconds;
            Start();

            Assert.That(_session.WorkFor(job.WorkGameMinutes + 1, Place, out var reason), Is.True);
            Assert.That(reason, Is.EqualTo(DoryMendReason.None));
            Assert.That(_clock.TotalSeconds - before, Is.EqualTo(job.WorkGameMinutes * Minute).Within(1e-8));
            Assert.That(_save.GetFlag(job.CompletedFlag), Is.True);
            Assert.That(_session.Snapshot.Working, Is.False);
            Assert.That(_carry.Consumes, Is.EqualTo(1));
            Assert.That(_session.NextJob.Id, Is.EqualTo(_def.Jobs[1].Id));
        }

        [Test]
        public void DryJobCanPauseAtLowWaterAndResumeAtHighWithoutMoreMaterials()
        {
            var job = _def.Jobs[0];
            job.NeedsLowWater = false;
            Start();
            Assert.That(_session.WorkFor(10, Place, out _), Is.True);
            _session.Pause();
            _clock.TotalSeconds = (3 + _config.TidalPeriodHours) * Hour;
            Assert.That(_tide.TideHeightAt(_clock.TotalSeconds), Is.GreaterThan(_def.WorkBelowMetres));
            Assert.That(_session.Snapshot.WorkedSeconds, Is.EqualTo(10 * Minute));
            double before = _clock.TotalSeconds;

            Start();
            Assert.That(_session.WorkFor(20, Place, out var reason), Is.True);
            Assert.That(reason, Is.EqualTo(DoryMendReason.None));
            Assert.That(_clock.TotalSeconds - before, Is.EqualTo(20 * Minute).Within(1e-9));
            Assert.That(_carry.Consumes, Is.EqualTo(1));
            CollectionAssert.AreEqual(new[] { "start:mendjob.patch_plank", "done:mendjob.patch_plank" }, _events);
        }

        [Test]
        public void DryJobDoesNotRemoveTheNextJobsLowWaterRule()
        {
            _def.Jobs[0].NeedsLowWater = false;
            _clock.TotalSeconds = 3 * Hour;
            Finish();
            Assert.That(_tide.TideHeightAt(_clock.TotalSeconds), Is.GreaterThan(_def.WorkBelowMetres));
            Assert.That(_def.Jobs[1].NeedsLowWater, Is.True);
            double before = _clock.TotalSeconds;

            Assert.That(_session.TryStart(_def.Jobs[1].Id, Place, out var reason), Is.False);
            Assert.That(reason, Is.EqualTo(DoryMendReason.LowWaterRequired));
            Assert.That(reason.TextKey(), Is.EqualTo("dory_mend.wait_for_low_water"));
            Assert.That(_clock.TotalSeconds, Is.EqualTo(before));
            Assert.That(_carry.Consumes, Is.EqualTo(1));
            Assert.That(_carry.Count("supply.oakum"), Is.EqualTo(1));
            Assert.That(_save.GetFlag(_def.Jobs[1].CompletedFlag), Is.False);
        }

        [TestCase(DoryMendReason.MissingMaterials)]
        [TestCase(DoryMendReason.ClockUnavailable)]
        [TestCase(DoryMendReason.BoatNotOwned)]
        [TestCase(DoryMendReason.WrongPlace)]
        public void DryJobStillHonoursTheOtherStartRules(DoryMendReason expected)
        {
            _def.Jobs[0].NeedsLowWater = false;
            _clock.TotalSeconds = 3 * Hour;
            string place = Place;
            switch (expected)
            {
                case DoryMendReason.MissingMaterials: _carry.Stock["supply.copper_nails"] = 0; break;
                case DoryMendReason.ClockUnavailable: _clock.IsHeld = true; break;
                case DoryMendReason.BoatNotOwned: _save.Current.OwnedBoats.Clear(); break;
                case DoryMendReason.WrongPlace: place = "region.elsewhere"; break;
            }
            double before = _clock.TotalSeconds;
            string saveBefore = JsonUtility.ToJson(_save.Current);
            Assert.That(_session.TryStart(_def.Jobs[0].Id, place, out var reason), Is.False);
            Assert.That(reason, Is.EqualTo(expected));
            Assert.That(_clock.TotalSeconds, Is.EqualTo(before));
            Assert.That(JsonUtility.ToJson(_save.Current), Is.EqualTo(saveBefore));
            Assert.That(_carry.Consumes, Is.Zero);
            Assert.That(_session.Snapshot.MaterialsReserved, Is.False);
            Assert.That(_events, Is.Empty);
        }

        [Test]
        public void EveryReasonHasAUniqueStableTextKey()
        {
            var keys = new HashSet<string>();
            foreach (DoryMendReason reason in Enum.GetValues(typeof(DoryMendReason)))
            {
                Assert.That(keys.Add(reason.TextKey()), Is.True, reason.ToString());
                StringAssert.StartsWith("dory_mend.", reason.TextKey());
            }
            Assert.That(keys.Count, Is.EqualTo(19));
        }
    }
}
