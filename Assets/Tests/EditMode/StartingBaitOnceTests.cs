using System.Collections.Generic;
using HiddenHarbours.Core;
using HiddenHarbours.Fishing;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    public class StartingBaitOnceTests
    {
        private GameObject _object;

        private sealed class MemorySave : ISaveService
        {
            public SaveData Current { get; } = new SaveData();
            public readonly Dictionary<string, bool> Flags = new();
            public int FlagWrites;
            public int Saves;
            public bool GetFlag(string key) => Flags.TryGetValue(key, out bool value) && value;
            public void SetFlag(string key, bool value) { Flags[key] = value; FlagWrites++; }
            public void Save() => Saves++;
        }

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();
            _object = new GameObject("Bait grant fixture");
            _object.SetActive(false);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_object);
            GameServices.Reset();
        }

        private StartingBait Bait(string flag, int count = 2)
        {
            var grant = _object.AddComponent<StartingBait>();
            grant.Configure(new[] { new StartingBait.BaitGrant { BaitId = "bait.fixture", Count = count } }, flag);
            return grant;
        }

        [Test]
        public void FlaggedGrant_HappensOnceAcrossComponents_AndDoesNotCallSaveDirectly()
        {
            var save = new MemorySave();
            GameServices.Save = save;
            Assert.AreEqual(1, Bait("fixture.granted").GrantOnce());
            Assert.AreEqual(0, Bait("fixture.granted").GrantOnce());
            Assert.AreEqual(1, save.Current.BaitStock.Count);
            Assert.AreEqual(2, save.Current.BaitStock[0].Count);
            Assert.AreEqual(1, save.FlagWrites);
            Assert.IsTrue(save.GetFlag("fixture.granted"));
            Assert.AreEqual(0, save.Saves);
        }

        [Test]
        public void MissingFlagKey_LeavesTheGrantRepeatable_AndSavesEachAcceptedGrant()
        {
            var save = new MemorySave();
            GameServices.Save = save;
            var grant = Bait("");
            Assert.AreEqual(1, grant.GrantOnce());
            Assert.AreEqual(1, grant.GrantOnce());
            Assert.AreEqual(4, save.Current.BaitStock[0].Count);
            Assert.AreEqual(0, save.FlagWrites);
            Assert.AreEqual(2, save.Saves);
        }

        [Test]
        public void EmptyGrant_StillConsumesANamedOnceFlag()
        {
            var save = new MemorySave();
            GameServices.Save = save;
            Assert.AreEqual(0, Bait("fixture.granted", 0).GrantOnce());
            Assert.AreEqual(1, save.FlagWrites);
            Assert.IsTrue(save.GetFlag("fixture.granted"));
            Assert.IsEmpty(save.Current.BaitStock);
            Assert.AreEqual(0, save.Saves);
        }

        [Test]
        public void MissingSave_RefusesWithoutUsingTheSceneLifecycle()
            => Assert.AreEqual(0, Bait("fixture.granted").GrantOnce());
    }
}
