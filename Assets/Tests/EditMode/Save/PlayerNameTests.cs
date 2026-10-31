using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    public class PlayerNameTests
    {
        private MemorySave _save;

        [SetUp]
        public void SetUp() { GameServices.Reset(); _save = new MemorySave(); GameServices.Save = _save; }

        [TearDown]
        public void TearDown() => GameServices.Reset();

        [Test]
        public void OfMissingSaveOrName_ReturnsEmpty()
        {
            Assert.AreEqual("", PlayerName.Of(null));
            Assert.AreEqual("", PlayerName.Of(new SaveData { PlayerName = null }));
        }

        [Test]
        public void Set_TrimsAndSavesOnce_AndSameNameWritesNothing()
        {
            Assert.IsTrue(PlayerName.Set("  Skipper  ", out string refusal), refusal);
            Assert.AreEqual("Skipper", _save.Current.PlayerName);
            Assert.IsFalse(PlayerName.Set(" Skipper ", out refusal));
            Assert.IsNull(refusal);
            Assert.AreEqual(1, _save.Saves);
        }

        [Test]
        public void BlankName_IsAllowedAndSavedAsNone()
        {
            _save.Current.PlayerName = "Skipper";
            Assert.IsTrue(PlayerName.Set(" \t ", out string refusal), refusal);
            Assert.AreEqual("", _save.Current.PlayerName);
            Assert.AreEqual(1, _save.Saves);
            Assert.IsFalse(PlayerName.Set(null, out refusal));
            Assert.IsNull(refusal);
            Assert.AreEqual(1, _save.Saves);
        }

        [Test]
        public void OverlongName_IsRefusedBeforeMutationOrSave()
        {
            Assert.IsFalse(PlayerName.Set(new string('x', GameServices.PlayerNameMaxLength + 1), out string refusal));
            Assert.IsNotEmpty(refusal);
            Assert.AreEqual("", _save.Current.PlayerName);
            Assert.AreEqual(0, _save.Saves);
        }

        [Test]
        public void LimitUsesConfig_AndChecksTrimmedLength()
        {
            var config = ScriptableObject.CreateInstance<GameConfig>();
            try
            {
                config.PlayerNameMaxLength = 3;
                GameServices.Config = config;
                Assert.IsNull(PlayerName.Check("  abc  "));
                Assert.IsNotNull(PlayerName.Check("abcd"));
            }
            finally { Object.DestroyImmediate(config); }
        }

        [Test]
        public void MissingSave_IsRefused()
        {
            GameServices.Save = null;
            Assert.IsFalse(PlayerName.Set("Skipper", out string refusal));
            Assert.IsNotEmpty(refusal);
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
