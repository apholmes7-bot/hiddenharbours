using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Player;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The carried pail keeps its catch over a relaunch (owner 2026-11-24). A pail with an authored id writes
    /// its whole catch to the v25 storage rows under that id on every change, and reads it back on bind; save
    /// v26 gains no field. Its place and whether it is in his hands are not saved. Every case goes through a
    /// real JSON write and read (<see cref="SaveSerialization"/>).
    /// </summary>
    public class CarriedPailSavedTests
    {
        const string PailKey = "container.st_peters.bucket";
        const string HodKey = "container.st_peters.hod";
        const string ClamId = "shellfish.soft_shell_clam";
        const int Capacity = 20;

        sealed class TestSave : ISaveService
        {
            public SaveData Current { get; set; } = SaveMigration.NewGame();
            public bool GetFlag(string key) => false;
            public void SetFlag(string key, bool value) { }
            public void Save() { }
        }

        sealed class ClamFactory : ICatchItemFactory
        {
            public bool TryCreate(string speciesId, out CatchItem item)
            {
                if (speciesId == ClamId) { item = Clam(); return true; }
                item = default;
                return false;
            }
        }

        static CatchItem Clam() => new CatchItem(ClamId, "Soft-shell clam", FishCategory.InshoreGroundfish, 1f, 1, 0.3f, 1f, default);

        readonly List<Object> _made = new List<Object>();
        TestSave _save;

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();
            Interactables.Clear();
            _save = new TestSave();
            GameServices.Save = _save;
            GameServices.CatchFactory = new ClamFactory();
        }

        [TearDown]
        public void TearDown()
        {
            Interactables.Clear();
            GameServices.Reset();
            foreach (Object o in _made) if (o != null) Object.DestroyImmediate(o);
            _made.Clear();
        }

        CarriableBucket MakePail(string id)
        {
            var go = new GameObject("Pail " + id);
            _made.Add(go);
            var pail = go.AddComponent<CarriableBucket>();
            pail.Configure(Capacity, null, id);
            return pail;
        }

        static void Fill(IHold hold, int n)
        {
            for (int i = 0; i < n; i++) Assert.That(hold.TryAdd(Clam()), Is.True, "clam " + i + " goes in");
        }

        static int Units(IReadOnlyList<HeldCatchDto> records)
        {
            int n = 0;
            if (records != null) foreach (HeldCatchDto r in records) n += r.Quantity;
            return n;
        }

        /// <summary>Write the save to JSON, read it back, and make that the live save: a relaunch.</summary>
        SaveData Relaunch()
        {
            string json = SaveSerialization.ToJson(_save.Current);
            _save.Current = SaveSerialization.FromJson(json);
            return _save.Current;
        }

        [Test]
        public void ClamsInTheCarriedPail_ComeBackInTheSamePail_AfterARealJsonRoundTrip()
        {
            CarriableBucket before = MakePail(PailKey);
            Fill(before, 3);
            Assert.That(Units(StoredContainers.CatchIn(_save.Current, PailKey)), Is.EqualTo(3), "each clam is written as it goes in");

            SaveData loaded = Relaunch();
            Assert.That(loaded.SchemaVersion, Is.EqualTo(SaveMigration.CurrentVersion));
            Assert.That(SaveMigration.CurrentVersion, Is.EqualTo(26), "the save version is unchanged");
            Assert.That(Units(loaded.BucketCatch), Is.EqualTo(0), "the belt pail is not credited with them");

            CarriableBucket after = MakePail(PailKey);   // the scene's pail, built fresh at its spot
            after.BindToSave(force: true);
            Assert.That(after.UsedUnits, Is.EqualTo(3));
            foreach (CatchItem it in after.Items) Assert.That(it.SpeciesId, Is.EqualTo(ClamId));
        }

        [Test]
        public void AnOldSaveWithNoRowForThePail_LoadsAsAnEmptyPail_AndBindingWritesNothing()
        {
            SaveData loaded = Relaunch();   // a new game's save, no StoredContainers row
            CarriableBucket pail = MakePail(PailKey);
            pail.BindToSave(force: true);

            Assert.That(pail.UsedUnits, Is.EqualTo(0));
            Assert.That(StoredContainers.Find(loaded, PailKey), Is.Null, "a bind is a read, never a write");
        }

        [Test]
        public void APailEmptiedBySale_WritesNoStaleClams()
        {
            CarriableBucket pail = MakePail(PailKey);
            Fill(pail, 3);

            pail.Clear();   // what the sale does (SellService rebuilds the hold with its keepers)
            Assert.That(StoredContainers.Find(_save.Current, PailKey), Is.Null, "an emptied pail leaves no row");

            SaveData loaded = Relaunch();
            Assert.That(StoredContainers.Find(loaded, PailKey), Is.Null);
            CarriableBucket after = MakePail(PailKey);
            after.BindToSave(force: true);
            Assert.That(after.UsedUnits, Is.EqualTo(0));
        }

        [Test]
        public void EachChangeWritesTheWholeCatch_SoNothingIsCountedTwice()
        {
            CarriableBucket pail = MakePail(PailKey);
            Fill(pail, 2);
            Fill(pail, 1);
            Assert.That(Units(StoredContainers.CatchIn(_save.Current, PailKey)), Is.EqualTo(3));

            pail.Clear();          // a sale that kept one back: clear, then put the keeper back
            Fill(pail, 1);
            Assert.That(Units(StoredContainers.CatchIn(_save.Current, PailKey)), Is.EqualTo(1));
            Assert.That(_save.Current.StoredContainers.FindAll(c => c.Key == PailKey).Count, Is.EqualTo(1), "one row per pail");
        }

        [Test]
        public void EveryAuthoredPailKeepsItsOwnRow_AndAPailWithNoAuthoredIdKeepsNone()
        {
            CarriableBucket bucket = MakePail(PailKey);
            CarriableBucket hod = MakePail(HodKey);
            CarriableBucket loose = MakePail(null);
            Fill(bucket, 2);
            Fill(hod, 4);
            Fill(loose, 1);

            SaveData loaded = Relaunch();
            Assert.That(Units(StoredContainers.CatchIn(loaded, PailKey)), Is.EqualTo(2));
            Assert.That(Units(StoredContainers.CatchIn(loaded, HodKey)), Is.EqualTo(4));
            Assert.That(loaded.StoredContainers.Count, Is.EqualTo(2), "a pail with a run-made id writes nothing");
            Assert.That(loose.UsedUnits, Is.EqualTo(1), "and still holds its catch for the session");
        }

        [Test]
        public void WithNoCatchFactory_ASavedRowIsLeftAlone_AndChangesAreRefused()
        {
            CarriableBucket before = MakePail(PailKey);
            Fill(before, 3);
            SaveData loaded = Relaunch();

            GameServices.CatchFactory = null;
            CarriableBucket pail = MakePail(PailKey);
            pail.BindToSave(force: true);
            Assert.That(pail.UsedUnits, Is.EqualTo(0), "nothing can be rebuilt yet");
            Assert.That(pail.TryAdd(Clam()), Is.False, "a change would write over clams it cannot see");
            pail.Clear();
            Assert.That(Units(StoredContainers.CatchIn(loaded, PailKey)), Is.EqualTo(3), "the row is untouched");

            GameServices.CatchFactory = new ClamFactory();
            Assert.That(pail.IsBound(), Is.True);
            Assert.That(pail.UsedUnits, Is.EqualTo(3), "a later bind finds them all");
        }

        [Test]
        public void TheSamePailAndClams_WriteTheSameSave_EveryTime()
        {
            string Write()
            {
                _save.Current = SaveMigration.NewGame();
                CarriableBucket pail = MakePail(PailKey);
                Fill(pail, 3);
                return JsonUtility.ToJson(StoredContainers.Find(_save.Current, PailKey));
            }
            Assert.That(Write(), Is.EqualTo(Write()));
        }
    }
}
