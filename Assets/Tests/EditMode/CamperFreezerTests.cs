using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Player;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The camper's chest freezer (owner 2026-11-17, "bigger freezer"; the seat: landed catch goes in it as it
    /// does in Ginny's). A catch hold whose capacity and deed are its <see cref="FreezerDef"/>'s: catch goes in
    /// frozen and is written to the v25 storage rows under <see cref="StorageIds.CamperFreezer"/>; a save and
    /// load brings the same catch back with its clock; until the camper is his it takes nothing and shows
    /// nothing; the Give rows take a gift out of it surgically.
    /// </summary>
    public class CamperFreezerTests
    {
        const string CamperDeed = "home.ginny_lot_camper";
        const string CodId = "fish.cod";
        const string HaddockId = "fish.haddock";

        sealed class FlagSave : ISaveService
        {
            public SaveData Current { get; set; } = SaveMigration.NewGame();
            public bool GetFlag(string key)
            {
                if (Current?.OnboardingFlags == null) return false;
                foreach (SaveFlag f in Current.OnboardingFlags) if (f.Key == key) return f.Value;
                return false;
            }
            public void SetFlag(string key, bool value)
            {
                Current.OnboardingFlags ??= new List<SaveFlag>();
                Current.OnboardingFlags.RemoveAll(f => f.Key == key);
                Current.OnboardingFlags.Add(new SaveFlag(key, value));
            }
            public void Save() { }
        }

        sealed class FakeFactory : ICatchItemFactory
        {
            public bool TryCreate(string speciesId, out CatchItem item)
            {
                switch (speciesId)
                {
                    case CodId: item = Cod(); return true;
                    case HaddockId: item = Haddock(); return true;
                    default: item = default; return false;
                }
            }
        }

        static CatchItem Cod() => new CatchItem(CodId, "Cod", FishCategory.InshoreGroundfish, 4f, 20, 0.3f, 1f, default);
        static CatchItem Haddock() => new CatchItem(HaddockId, "Haddock", FishCategory.InshoreGroundfish, 2f, 12, 0.3f, 1f, default);

        readonly List<Object> _made = new List<Object>();
        FlagSave _save;

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();
            Interactables.Clear();
            _save = new FlagSave();
            GameServices.Save = _save;
            GameServices.CatchFactory = new FakeFactory();
        }

        [TearDown]
        public void TearDown()
        {
            Interactables.Clear();
            GameServices.Reset();
            foreach (Object o in _made) if (o != null) Object.DestroyImmediate(o);
            _made.Clear();
        }

        CamperFreezer MakeFreezer(int capacity = 40, string deed = CamperDeed)
        {
            var def = ScriptableObject.CreateInstance<FreezerDef>();
            def.Id = "freezer.camper_freezer";
            def.DisplayName = "camper's freezer";
            def.CapacityUnits = capacity;
            def.DeedHomeId = deed;
            _made.Add(def);

            var go = new GameObject("CamperFreezer");
            _made.Add(go);
            var freezer = go.AddComponent<CamperFreezer>();
            freezer.Configure(def);   // EditMode: OnEnable does not fire, so this lists it
            return freezer;
        }

        void GrantDeed() => HomeDeeds.Grant(_save, CamperDeed);

        static int Units(IReadOnlyList<HeldCatchDto> records)
        {
            int n = 0;
            if (records != null) foreach (HeldCatchDto r in records) n += r.Quantity;
            return n;
        }

        // ---- catch in --------------------------------------------------------------------------------------

        [Test]
        public void CatchGoesInFrozen_AndIsWrittenUnderItsId()
        {
            GrantDeed();
            CamperFreezer freezer = MakeFreezer();

            Assert.IsTrue(freezer.TryAdd(Cod()));
            Assert.AreEqual(1, freezer.UsedUnits);
            Assert.AreEqual(StorageMode.Frozen, freezer.Items[0].Freshness.Mode, "frozen stops rot, as at Ginny's");
            Assert.AreEqual(1, Units(StoredContainers.CatchIn(_save.Current, StorageIds.CamperFreezer)));
            Assert.AreEqual("freezer.camper_freezer",
                            StoredContainers.Find(_save.Current, StorageIds.CamperFreezer).ContainerDefId);
        }

        [Test]
        public void TheCapacityIsTheDefs()
        {
            GrantDeed();
            CamperFreezer freezer = MakeFreezer(capacity: 2);
            Assert.AreEqual(2, freezer.CapacityUnits);
            Assert.IsTrue(freezer.TryAdd(Cod()));
            Assert.IsTrue(freezer.TryAdd(Cod()));
            Assert.IsFalse(freezer.TryAdd(Cod()), "full");
            Assert.AreEqual(2, Units(StoredContainers.CatchIn(_save.Current, StorageIds.CamperFreezer)));
        }

        // ---- save and load ---------------------------------------------------------------------------------

        [Test]
        public void ASaveAndLoadBringsTheSameCatchBack_StillFrozen()
        {
            GrantDeed();
            CamperFreezer freezer = MakeFreezer();
            Assert.IsTrue(freezer.TryAdd(Cod()));
            Assert.IsTrue(freezer.TryAdd(Cod()));
            Assert.IsTrue(freezer.TryAdd(Haddock()));

            SaveData loaded = SaveSerialization.FromJson(SaveSerialization.ToJson(_save.Current));
            Assert.IsNotNull(loaded);
            _save.Current = loaded;

            Object.DestroyImmediate(freezer.gameObject);
            CamperFreezer again = MakeFreezer();
            again.BindToSave();

            Assert.AreEqual(3, again.UsedUnits);
            Assert.AreEqual(CodId, again.Items[0].SpeciesId);
            Assert.AreEqual(HaddockId, again.Items[2].SpeciesId);
            foreach (CatchItem c in again.Items)
                Assert.AreEqual(StorageMode.Frozen, c.Freshness.Mode, "restored exactly, never re-moded");
        }

        [Test]
        public void ANewGameLeavesItEmpty_AndAnEmptiedFreezerLeavesNoRow()
        {
            GrantDeed();
            CamperFreezer freezer = MakeFreezer();
            Assert.IsTrue(freezer.TryAdd(Cod()));
            Assert.IsTrue(freezer.TryRemoveAt(0));
            Assert.IsNull(StoredContainers.Find(_save.Current, StorageIds.CamperFreezer), "nothing in it, no row");

            Assert.IsTrue(freezer.TryAdd(Cod()));
            _save.Current = SaveMigration.NewGame();
            freezer.BindToSave();
            Assert.AreEqual(0, freezer.UsedUnits, "another save, where nothing was ever frozen");
        }

        [Test]
        public void DamagedCatchRecordsAreRepaired_AndRepeatedKeysFold()
        {
            SaveData save = SaveMigration.NewGame();
            save.StoredContainers = new List<StoredContainerDto>
            {
                new StoredContainerDto
                {
                    Key = StorageIds.CamperFreezer,
                    Catch = { new HeldCatchDto { SpeciesId = CodId, Quantity = 2 }, new HeldCatchDto { SpeciesId = "", Quantity = 1 } },
                },
                new StoredContainerDto
                {
                    Key = StorageIds.CamperFreezer, Catch = { new HeldCatchDto { SpeciesId = HaddockId, Quantity = 0 } },
                },
                new StoredContainerDto { Key = StorageIds.CamperFreezer, Catch = null },
                new StoredContainerDto
                {
                    Key = StorageIds.CamperFreezer, Catch = { new HeldCatchDto { SpeciesId = HaddockId, Quantity = 1 } },
                },
            };

            SaveMigration.Migrate(save);

            Assert.AreEqual(1, save.StoredContainers.Count);
            Assert.AreEqual(3, Units(save.StoredContainers[0].Catch), "two cod and a haddock: nothing good is lost");
            SaveMigration.Migrate(save);
            Assert.AreEqual(3, Units(save.StoredContainers[0].Catch), "idempotent");
        }

        // ---- the deed ---------------------------------------------------------------------------------------

        [Test]
        public void ItTakesNothingUntilTheCamperIsHis()
        {
            CamperFreezer freezer = MakeFreezer();
            Assert.IsFalse(freezer.IsOpen);
            Assert.IsFalse(freezer.TryAdd(Cod()));
            Assert.AreEqual(0, freezer.UsedUnits);
            Assert.IsNull(StoredContainers.Find(_save.Current, StorageIds.CamperFreezer), "a refusal writes nothing");

            GrantDeed();
            Assert.IsTrue(freezer.IsOpen);
            Assert.IsTrue(freezer.TryAdd(Cod()));
        }

        [Test]
        public void AClosedFreezerShowsNothing_SoNothingIsTakenOrLost()
        {
            GrantDeed();
            CamperFreezer freezer = MakeFreezer();
            Assert.IsTrue(freezer.TryAdd(Cod()));

            _save.SetFlag(HomeDeeds.KeyFor(CamperDeed), false);
            Assert.AreEqual(0, freezer.Items.Count, "a reader sees nothing in a freezer that is not his");
            Assert.IsFalse(freezer.TryRemoveAt(0));
            freezer.Clear();
            Assert.AreEqual(1, freezer.UsedUnits, "and nothing is emptied through a closed door");
            Assert.AreEqual(1, Units(StoredContainers.CatchIn(_save.Current, StorageIds.CamperFreezer)));
        }

        // ---- one of his containers: the Give rows -----------------------------------------------------------

        [Test]
        public void ItIsListedUnderItsFixedId_AtTheHomeFreezersPriority()
        {
            CamperFreezer freezer = MakeFreezer();
            Assert.AreSame(freezer, PlayerStorage.Find(StorageIds.CamperFreezer));
            Assert.AreEqual(PlayerStorage.PriorityHomeFreezer, freezer.StoragePriority);
            Assert.AreSame(freezer, PlayerStorage.Get(0), "a catch hold: the gift rows read it");
        }

        [Test]
        public void GiveTakesTheOneGiftFromIt_AndTheSaveFollows()
        {
            GrantDeed();
            CamperFreezer freezer = MakeFreezer();
            Assert.IsTrue(freezer.TryAdd(Haddock()));
            Assert.IsTrue(freezer.TryAdd(Cod()));

            var gifts = new GiftInventory();
            gifts.Gather(null, null, null, null);
            Assert.IsTrue(gifts.TryTakeOne(CodId, out CatchItem taken));

            Assert.AreEqual(CodId, taken.SpeciesId);
            Assert.AreEqual(1, freezer.UsedUnits);
            Assert.AreEqual(HaddockId, freezer.Items[0].SpeciesId, "the haddock was not touched");
            IReadOnlyList<HeldCatchDto> saved = StoredContainers.CatchIn(_save.Current, StorageIds.CamperFreezer);
            Assert.AreEqual(1, Units(saved));
            Assert.AreEqual(HaddockId, saved[0].SpeciesId);
        }

        [Test]
        public void GiveFindsNothingInAFreezerThatIsNotHis()
        {
            GrantDeed();
            CamperFreezer freezer = MakeFreezer();
            Assert.IsTrue(freezer.TryAdd(Cod()));
            _save.SetFlag(HomeDeeds.KeyFor(CamperDeed), false);

            var gifts = new GiftInventory();
            gifts.Gather(null, null, null, null);
            Assert.IsFalse(gifts.TryTakeOne(CodId, out _));
            Assert.AreEqual(1, freezer.UsedUnits);
        }
    }
}
