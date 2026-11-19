using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The player's storage list (<see cref="PlayerStorage"/>) once containers have stable ids: a
    /// deterministic order (priority, then ordinal id, never registration order), a warning for a second
    /// container under one id, holds as an optional facet (a rack is listed but is not a catch hold), lookup
    /// by id, the old <c>Register(IHold)</c> still working, and <see cref="GameServices.Reset"/> clearing it.
    /// </summary>
    public class PlayerStorageOrderTests
    {
        class Named : IStorageContainer
        {
            public Named(string id, int priority) { StorageId = id; StoragePriority = priority; }
            public string StorageId { get; }
            public int StoragePriority { get; }
        }

        sealed class NamedHold : Named, IHold
        {
            public NamedHold(string id, int priority) : base(id, priority) { }
            public int CapacityUnits => 4;
            public int UsedUnits => 0;
            public IReadOnlyList<CatchItem> Items => System.Array.Empty<CatchItem>();
            public bool TryAdd(CatchItem item) => false;
            public void Clear() { }
        }

        sealed class PlainHold : IHold
        {
            public int CapacityUnits => 4;
            public int UsedUnits => 0;
            public IReadOnlyList<CatchItem> Items => System.Array.Empty<CatchItem>();
            public bool TryAdd(CatchItem item) => false;
            public void Clear() { }
        }

        [SetUp] public void SetUp() => PlayerStorage.Clear();
        [TearDown] public void TearDown() => PlayerStorage.Clear();

        static List<string> Ids()
        {
            var ids = new List<string>();
            for (int i = 0; i < PlayerStorage.Count; i++) ids.Add(PlayerStorage.GetContainer(i)?.StorageId ?? "-");
            return ids;
        }

        [Test]
        public void TheOrderIsPriorityThenId_WhateverOrderTheyRegisteredIn()
        {
            var freezer = new NamedHold(StorageIds.GinnyFreezer, PlayerStorage.PriorityHomeFreezer);
            var hold = new NamedHold(StorageIds.BoatHold, PlayerStorage.PriorityBoatHold);
            var locker = new Named("storage.stpeters.ginny_gear_locker", PlayerStorage.PriorityFixture);
            var rack = new Named("storage.stpeters.ginny_rod_rack", PlayerStorage.PriorityFixture);

            PlayerStorage.Register(rack);
            PlayerStorage.Register<NamedHold>(freezer);
            PlayerStorage.Register(locker);
            PlayerStorage.Register<NamedHold>(hold);
            List<string> first = Ids();

            PlayerStorage.Clear();
            PlayerStorage.Register<NamedHold>(hold);
            PlayerStorage.Register(locker);
            PlayerStorage.Register<NamedHold>(freezer);
            PlayerStorage.Register(rack);

            CollectionAssert.AreEqual(new[]
            {
                StorageIds.BoatHold, StorageIds.GinnyFreezer,
                "storage.stpeters.ginny_gear_locker", "storage.stpeters.ginny_rod_rack",
            }, first);
            CollectionAssert.AreEqual(first, Ids(), "the same list from the other registration order");
        }

        [Test]
        public void AHoldWithNoId_SortsAfterTheNamedOnes_AndTiesKeepRegistrationOrder()
        {
            var a = new PlainHold();
            var b = new PlainHold();
            PlayerStorage.Register(a);
            PlayerStorage.Register<NamedHold>(new NamedHold(StorageIds.BoatHold, PlayerStorage.PriorityBoatHold));
            PlayerStorage.Register(b);

            Assert.AreEqual(StorageIds.BoatHold, PlayerStorage.GetContainer(0).StorageId);
            Assert.AreSame(a, PlayerStorage.Get(1));
            Assert.AreSame(b, PlayerStorage.Get(2));
        }

        [Test]
        public void HoldsAreAFacet_ARackIsListedButIsNotACatchHold()
        {
            var rack = new Named("storage.stpeters.ginny_rod_rack", PlayerStorage.PriorityFixture);
            var hold = new NamedHold(StorageIds.BoatHold, PlayerStorage.PriorityBoatHold);
            PlayerStorage.Register(rack);
            PlayerStorage.Register<NamedHold>(hold);

            Assert.AreSame(hold, PlayerStorage.Get(0));
            Assert.IsNull(PlayerStorage.Get(1), "a reader of catch skips the rack");
            Assert.AreSame(rack, PlayerStorage.GetContainer(1));
            Assert.AreSame(rack, PlayerStorage.Find("storage.stpeters.ginny_rod_rack"));
            Assert.IsNull(PlayerStorage.Find("storage.nowhere"));
            Assert.IsNull(PlayerStorage.Find(""));
        }

        [Test]
        public void TheSameObjectTwiceIsListedOnce_AnotherUnderTheSameIdIsWarnedAbout()
        {
            var one = new Named("storage.stpeters.ginny_rod_rack", PlayerStorage.PriorityFixture);
            PlayerStorage.Register(one);
            PlayerStorage.Register(one);
            Assert.AreEqual(1, PlayerStorage.Count);

            LogAssert.Expect(LogType.Warning, new Regex("share the storage id 'storage.stpeters.ginny_rod_rack'"));
            PlayerStorage.Register(new Named("storage.stpeters.ginny_rod_rack", PlayerStorage.PriorityFixture));
            Assert.AreEqual(2, PlayerStorage.Count, "both stay listed: nothing is lost, the test of ids is what fails");
        }

        [Test]
        public void UnregisterTakesItOff_AndResetClearsTheList()
        {
            var rack = new Named("storage.stpeters.ginny_rod_rack", PlayerStorage.PriorityFixture);
            var hold = new PlainHold();
            PlayerStorage.Register(rack);
            PlayerStorage.Register(hold);
            PlayerStorage.Unregister(rack);
            Assert.AreEqual(1, PlayerStorage.Count);
            Assert.AreSame(hold, PlayerStorage.Get(0));

            PlayerStorage.Register(rack);
            GameServices.Reset();
            Assert.AreEqual(0, PlayerStorage.Count, "a Reset clears it: what outlives a Reset registers again");
        }

        [Test]
        public void TheFixedIdsAreStorageIds()
        {
            Assert.IsTrue(StorageIds.IsStorageId(StorageIds.BoatHold));
            Assert.IsTrue(StorageIds.IsStorageId(StorageIds.GinnyFreezer));
            Assert.IsFalse(StorageIds.IsStorageId("storage."));
            Assert.IsFalse(StorageIds.IsStorageId("stowage.rod_rack#3"));
            Assert.IsFalse(StorageIds.IsStorageId(null));
        }
    }
}
