using System.Collections.Generic;
using HiddenHarbours.Core;
using HiddenHarbours.UI;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.UI.EditMode
{
    /// <summary>The creator's trait picker as pure state (owner ruling 2026-11-14: exactly the allowance
    /// of named traits, read from whatever the catalog offers).</summary>
    public class TraitPickModelTests
    {
        private static readonly string[] Three = { "trait.a", "trait.b", "trait.c" };
        private readonly List<TraitDef> _made = new List<TraitDef>();

        [SetUp]
        public void SetUp() => GameServices.Reset();

        [TearDown]
        public void TearDown()
        {
            foreach (TraitDef def in _made) if (def != null) Object.DestroyImmediate(def);
            _made.Clear();
            GameServices.Reset();
        }

        [Test]
        public void CannotPickMoreThanTheAllowance()
        {
            var picks = new TraitPickModel(Three, 2);
            Assert.IsTrue(picks.Toggle("trait.a", out _));
            Assert.IsTrue(picks.Toggle("trait.b", out _));
            Assert.IsFalse(picks.Toggle("trait.c", out string refusal));
            Assert.IsNotNull(refusal);
            CollectionAssert.AreEqual(new[] { "trait.a", "trait.b" }, picks.Picked);
            Assert.IsTrue(picks.IsComplete);
        }

        [Test]
        public void CannotContinueWithFewer_AndSaysWhyInPlainWords()
        {
            var picks = new TraitPickModel(Three, 2);
            Assert.IsFalse(picks.IsComplete);
            Assert.AreEqual("Pick 2 traits to continue.", picks.Why);
            picks.Toggle("trait.c", out _);
            Assert.IsFalse(picks.IsComplete);
            Assert.AreEqual(1, picks.Remaining);
            Assert.AreEqual("Pick 1 more trait to continue.", picks.Why);
            var service = new RecordingTraits();
            Assert.IsFalse(picks.WriteTo(service, out string refusal));
            Assert.AreEqual(picks.Why, refusal);
            Assert.AreEqual(0, service.Calls, "an incomplete hand must never reach the trait service");
        }

        [Test]
        public void DeselectFreesASlot()
        {
            var picks = new TraitPickModel(Three, 2);
            picks.Toggle("trait.a", out _);
            picks.Toggle("trait.b", out _);
            Assert.IsTrue(picks.Toggle("trait.a", out _), "toggling a picked trait lets it go");
            Assert.IsFalse(picks.IsPicked("trait.a"));
            Assert.AreEqual(1, picks.Remaining);
            Assert.IsTrue(picks.Toggle("trait.c", out _));
            CollectionAssert.AreEqual(new[] { "trait.b", "trait.c" }, picks.Picked);
        }

        [Test]
        public void UnknownIdIsRefused()
        {
            var picks = new TraitPickModel(Three, 2);
            Assert.IsFalse(picks.Toggle("trait.retired", out string refusal));
            Assert.IsNotNull(refusal);
            Assert.IsFalse(picks.Toggle(null, out _));
            Assert.AreEqual(0, picks.Picked.Count);
        }

        [TestCase(0), TestCase(1), TestCase(2), TestCase(3), TestCase(4), TestCase(9)]
        public void AnyCatalogSize_AsksForTheAllowanceOrTheWholeCatalogIfSmaller(int size)
        {
            var ids = new List<string>();
            for (int i = 0; i < size; i++) ids.Add("trait.t" + i);
            var picks = new TraitPickModel(ids, 2);
            Assert.AreEqual(size, picks.Offered.Count);
            Assert.AreEqual(Mathf.Min(2, size), picks.Required);
            for (int i = 0; i < size; i++) picks.Toggle(ids[i], out _);
            Assert.IsTrue(picks.IsComplete);
            Assert.IsNull(picks.Why);
            Assert.AreEqual(picks.Required, picks.Picked.Count);
        }

        [Test]
        public void TheAllowanceIsReadNotAssumed()
        {
            var picks = new TraitPickModel(Three, 3);
            foreach (string id in Three) Assert.IsTrue(picks.Toggle(id, out _));
            Assert.IsTrue(picks.IsComplete);
        }

        [Test]
        public void HeldTraitsArePreselected_UnknownRepeatedAndExtraDropped_ResetReturnsToThem()
        {
            var picks = new TraitPickModel(Three, 2, new[] { "trait.gone", "trait.b", "trait.b", "trait.c", "trait.a" });
            CollectionAssert.AreEqual(new[] { "trait.b", "trait.c" }, picks.Picked);
            picks.Toggle("trait.b", out _);
            picks.Toggle("trait.a", out _);
            picks.Reset();
            CollectionAssert.AreEqual(new[] { "trait.b", "trait.c" }, picks.Picked);
        }

        [Test]
        public void ACharacterFromBeforeTraitsStartsEmpty()
        {
            var picks = new TraitPickModel(Three, 2, new string[0]);
            Assert.AreEqual(0, picks.Picked.Count);
            picks = new TraitPickModel(Three, 2, null);
            Assert.AreEqual(0, picks.Picked.Count);
        }

        [Test]
        public void TheResultIsWrittenToTheSaveThroughTheTraitService()
        {
            var save = new MemorySave();
            var service = new TraitService(() => save, () => Defs(Three));
            var picks = new TraitPickModel(Ids(service.All), GameServices.TraitPicksPerCharacter);
            picks.Toggle("trait.c", out _);
            picks.Toggle("trait.a", out _);
            Assert.IsTrue(picks.WriteTo(service, out string refusal), refusal);
            CollectionAssert.AreEqual(new[] { "trait.c", "trait.a" }, save.Current.PlayerTraits);
            Assert.AreEqual(1, save.Saves);
            CollectionAssert.AreEqual(new[] { "trait.c", "trait.a" }, service.PlayerTraitIds);
        }

        private List<TraitDef> Defs(IEnumerable<string> ids)
        {
            var defs = new List<TraitDef>();
            foreach (string id in ids)
            {
                var def = ScriptableObject.CreateInstance<TraitDef>();
                def.Id = id;
                def.DisplayName = id;
                _made.Add(def);
                defs.Add(def);
            }
            return defs;
        }

        private static List<string> Ids(IReadOnlyList<TraitDef> defs)
        {
            var ids = new List<string>();
            foreach (TraitDef def in defs) ids.Add(def.Id);
            return ids;
        }

        private sealed class MemorySave : ISaveService
        {
            public SaveData Current { get; } = SaveMigration.NewGame();
            public int Saves;
            public bool GetFlag(string key) => false;
            public void SetFlag(string key, bool value) { }
            public void Save() => Saves++;
        }

        private sealed class RecordingTraits : ITraitService
        {
            public int Calls;
            public IReadOnlyList<TraitDef> All => new TraitDef[0];
            public TraitDef Find(string id) => null;
            public IReadOnlyList<string> PlayerTraitIds => new string[0];
            public float PlayerMultiplier(TraitStat stat) => 1f;
            public float MultiplierFor(TraitStat stat, IReadOnlyList<string> traitIds) => 1f;
            public bool SetPlayerTraits(IReadOnlyList<string> traitIds, out string refusal)
            { Calls++; refusal = null; return true; }
        }
    }
}
