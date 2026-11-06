using System;
using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    public class PlayerNameTextTests
    {
        private MemorySave _save;

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();
            _save = new MemorySave();
            GameServices.Save = _save;
        }

        [TearDown]
        public void TearDown() => GameServices.Reset();

        [TestCase("Rowan", "Hello, Rowan. Rowan!")]
        [TestCase("", "Hello, skipper. skipper!")]
        [TestCase(null, "Hello, skipper. skipper!")]
        [TestCase(" \t", "Hello, skipper. skipper!")]
        public void Resolve_ReadsTheNameOrFallbackAtTheDraw(string name, string expected)
        {
            _save.Current.PlayerName = name;
            Assert.AreEqual(expected, PlayerName.Resolve("Hello, {player}. {player}!"));
            Assert.AreEqual(0, _save.Saves);
        }

        [Test]
        public void Resolve_IsLiteralAndLeavesOtherBindingsAlone()
        {
            _save.Current.PlayerName = "{player}{payout}{0}";
            Assert.AreEqual("Hi {player}{payout}{0}; {payout} {units} {species1} {price1} {unknown}",
                PlayerName.Resolve("Hi {player}; {payout} {units} {species1} {price1} {unknown}"));
            Assert.AreEqual("skipper", PlayerName.Resolve("{player}", null));
        }

        [Test]
        public void FallbackComesFromConfig_WithOneDefaultForMissingOrBlankConfig()
        {
            var config = ScriptableObject.CreateInstance<GameConfig>();
            try
            {
                GameServices.Config = config;
                config.PlayerNameFallback = "friend";
                Assert.AreEqual("Hello, friend.", PlayerName.Resolve("Hello, {player}."));
                config.PlayerNameFallback = " ";
                Assert.AreEqual("skipper", PlayerName.Resolve("{player}"));
                UnityEngine.Object.DestroyImmediate(config);
                Assert.AreEqual("skipper", PlayerName.Resolve("{player}"));
            }
            finally { if (config != null) UnityEngine.Object.DestroyImmediate(config); }
        }

        [Test]
        public void ChangedEventComesAfterSaving_AndRefusalOrSameNameEmitsNothing()
        {
            int events = 0;
            int savedWhenPublished = 0;
            string previous = null, next = null;
            void Capture(PlayerNameChanged changed)
            {
                events++;
                savedWhenPublished = _save.Saves;
                previous = changed.PreviousName;
                next = changed.Name;
            }

            EventBus.Subscribe<PlayerNameChanged>(Capture);
            try
            {
                Assert.IsTrue(PlayerName.Set(" Rowan ", out string refusal), refusal);
                Assert.AreEqual(1, events);
                Assert.AreEqual(1, savedWhenPublished);
                Assert.AreEqual("", previous);
                Assert.AreEqual("Rowan", next);
                Assert.IsFalse(PlayerName.Set("Rowan", out refusal));
                Assert.IsFalse(PlayerName.Set(new string('x', GameServices.PlayerNameMaxLength + 1), out refusal));
                Assert.AreEqual(1, events);
                Assert.IsTrue(PlayerName.Set("", out refusal), refusal);
                Assert.AreEqual(2, events);
                Assert.AreEqual("Rowan", previous);
                Assert.AreEqual("", next);
                GameServices.Save = null;
                Assert.IsFalse(PlayerName.Set("River", out refusal));
                Assert.AreEqual(2, events);
            }
            finally { EventBus.Unsubscribe<PlayerNameChanged>(Capture); }
        }

        [Test]
        public void NoToken_ReturnsTheSameStringWithoutAllocating()
        {
            string line = new string("The tide is turning.".ToCharArray());
            Assert.AreSame(line, PlayerName.Resolve(line));
            Assert.IsNull(PlayerName.Resolve(null));
            for (int i = 0; i < 32; i++) PlayerName.Resolve(line);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++) PlayerName.Resolve(line);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.AreEqual(0L, allocated);
        }

        private sealed class MemorySave : ISaveService
        {
            public SaveData Current { get; } = SaveMigration.NewGame();
            public int Saves;
            public bool GetFlag(string key) => false;
            public void SetFlag(string key, bool value) { }
            public void Save() => Saves++;
        }
    }
}
