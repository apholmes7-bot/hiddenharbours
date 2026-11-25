using System.Collections.Generic;
using NUnit.Framework;
using HiddenHarbours.Core;
using HiddenHarbours.Player;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// St Peters STARTING GEAR + capability mapping: the player wakes already owning the shovel + bucket
    /// (granted, not bought), and the owned-gear list maps to on-foot capabilities (dig / bucket / rod).
    /// Pure logic over the save DTO — no scene.
    /// </summary>
    public class StartingGearTests
    {
        private static SaveData NewSave() => SaveMigration.NewGame();

        // ---- the grant ------------------------------------------------------------------------

        [Test]
        public void Grant_AddsStartingGear_Idempotently()
        {
            var save = NewSave();
            var ids = new[] { PlayerGear.ShovelId, PlayerGear.BucketId };

            int first = StartingGear.Grant(save, ids);
            Assert.AreEqual(2, first, "both starting items are newly granted");
            CollectionAssert.Contains(save.OwnedGear, PlayerGear.ShovelId);
            CollectionAssert.Contains(save.OwnedGear, PlayerGear.BucketId);

            int second = StartingGear.Grant(save, ids);
            Assert.AreEqual(0, second, "re-granting adds nothing (idempotent)");
            Assert.AreEqual(2, save.OwnedGear.Count, "no duplicates");
        }

        [Test]
        public void Grant_DoesNotStripGearTheyAlreadyBought()
        {
            var save = NewSave();
            save.OwnedGear.Add(PlayerGear.RodId);   // bought the rod already

            StartingGear.Grant(save, new[] { PlayerGear.ShovelId, PlayerGear.BucketId });

            CollectionAssert.Contains(save.OwnedGear, PlayerGear.RodId, "the bought rod survives the grant");
            Assert.AreEqual(3, save.OwnedGear.Count);
        }

        // ---- capability mapping ---------------------------------------------------------------

        [Test]
        public void Capabilities_FollowOwnedGear()
        {
            var save = NewSave();
            Assert.IsFalse(PlayerGear.CanDig(save), "no shovel → can't dig");
            Assert.IsFalse(PlayerGear.HasBucket(save), "no bucket yet");
            Assert.IsFalse(PlayerGear.CanRodFish(save), "no rod yet");

            StartingGear.Grant(save, new[] { PlayerGear.ShovelId, PlayerGear.BucketId });
            Assert.IsTrue(PlayerGear.CanDig(save), "shovel granted → can dig");
            Assert.IsTrue(PlayerGear.HasBucket(save), "bucket granted → on-foot clam hold available");
            Assert.IsFalse(PlayerGear.CanRodFish(save), "still no rod — that's bought at Nine Mile Creek");

            save.OwnedGear.Add(PlayerGear.RodId);
            Assert.IsTrue(PlayerGear.CanRodFish(save), "rod bought → rod-fishing enabled");
        }

        [Test]
        public void Owns_NullSaveOrId_IsFalse()
        {
            Assert.IsFalse(PlayerGear.Owns((SaveData)null, PlayerGear.ShovelId));
            Assert.IsFalse(PlayerGear.Owns(NewSave(), null));
            Assert.IsFalse(PlayerGear.Owns(NewSave(), ""));
        }

        [Test]
        public void GrantOnce_OverFakeSave_GuardedByFlag()
        {
            var save = NewSave();
            var fake = new FlagSaveService(save);
            GameServices.Reset();
            GameServices.Save = fake;
            try
            {
                var go = new UnityEngine.GameObject("StartingGear");
                try
                {
                    var sg = go.AddComponent<StartingGear>();
                    sg.Configure(new[] { PlayerGear.ShovelId, PlayerGear.BucketId }, "flag.granted");

                    Assert.AreEqual(2, sg.GrantOnce(), "first call grants the kit");
                    Assert.IsTrue(fake.GetFlag("flag.granted"), "the grant flag is set so it won't re-run");
                    Assert.AreEqual(0, sg.GrantOnce(), "the flag short-circuits a second grant");
                }
                finally { UnityEngine.Object.DestroyImmediate(go); }
            }
            finally { GameServices.Reset(); }
        }

        // ---- the headlamp: his from the start, owned without a save write ---------------------

        [Test]
        public void ANewGame_OwnsTheHeadlamp_WithoutItBeingWrittenToTheSave()
        {
            GameServices.Reset();
            try
            {
                var save = NewSave();
                Assert.IsTrue(PlayerGear.HasHeadlamp(save), "in his pocket from the start");
                Assert.IsTrue(PlayerGear.Owns(save, PlayerGear.HeadlampId));
                CollectionAssert.DoesNotContain(save.OwnedGear, PlayerGear.HeadlampId, "derived, never written");
                Assert.IsFalse(PlayerGear.HasHeadlamp((SaveData)null), "no save holds nothing");
            }
            finally { GameServices.Reset(); }
        }

        [Test]
        public void AContinuedOldSave_OwnsTheHeadlamp_WithNoGrantNoWriteAndAnUnchangedSnapshot()
        {
            // The once-flag was set long ago with only the shovel and bucket.
            var save = NewSave();
            save.OwnedGear.AddRange(new[] { PlayerGear.ShovelId, PlayerGear.BucketId, PlayerGear.RodId });
            var fake = new FlagSaveService(save);
            fake.SetFlag("st_peters_starting_gear_granted", true);
            GameServices.Reset();
            GameServices.Save = fake;
            string before = SaveSerialization.ToJson(save);
            var go = new UnityEngine.GameObject("StartingGear");
            try
            {
                var sg = go.AddComponent<StartingGear>();   // the shipped defaults, as on the player

                Assert.AreEqual(0, sg.GrantOnce(), "nothing is granted on continue");
                Assert.AreEqual(0, fake.Saves, "and nothing is written");
                Assert.AreEqual(before, SaveSerialization.ToJson(save), "the snapshot is untouched");
                Assert.IsTrue(PlayerGear.HasHeadlamp(), "yet the headlamp is his");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
                GameServices.Reset();
            }
        }

        [Test]
        public void TheHeadlampStaysOwnedAcrossASaveRoundTrip_InTheCurrentFormat()
        {
            GameServices.Reset();
            try
            {
                var save = NewSave();
                StartingGear.Grant(save, new[] { PlayerGear.ShovelId, PlayerGear.BucketId });
                string json = SaveSerialization.ToJson(save);
                SaveData reloaded = SaveSerialization.FromJson(json);

                Assert.IsNotNull(reloaded);
                Assert.AreEqual(SaveMigration.CurrentVersion, reloaded.SchemaVersion, "no new save version");
                Assert.AreEqual(json, SaveSerialization.ToJson(reloaded), "no new field, nothing added");
                Assert.IsTrue(PlayerGear.HasHeadlamp(reloaded), "still his after a reload");
            }
            finally { GameServices.Reset(); }
        }

        [Test]
        public void WhatIsCarriedFromTheStart_IsConfigData()
        {
            var config = UnityEngine.ScriptableObject.CreateInstance<GameConfig>();
            try
            {
                CollectionAssert.AreEqual(GameConfig.DefaultCarriedFromStartGearIds, config.CarriedFromStartGearIds);
                CollectionAssert.Contains(GameConfig.DefaultCarriedFromStartGearIds, PlayerGear.HeadlampId);

                GameServices.Reset();
                GameServices.Config = config;
                config.CarriedFromStartGearIds = new string[0];
                Assert.IsFalse(PlayerGear.HasHeadlamp(NewSave()), "an emptied list carries nothing");
                config.CarriedFromStartGearIds = null;
                Assert.IsFalse(PlayerGear.HasHeadlamp(NewSave()), "a null list carries nothing");
            }
            finally
            {
                GameServices.Reset();
                UnityEngine.Object.DestroyImmediate(config);
            }
        }

        [Test]
        public void TheKitPage_ListsTheHeadlamp_FromTheCarriedList()
        {
            var save = NewSave();
            StartingGear.Grant(save, new[] { PlayerGear.ShovelId, PlayerGear.BucketId });
            SaveData reloaded = SaveSerialization.FromJson(SaveSerialization.ToJson(save));

            var held = new List<HiddenHarbours.World.NotebookGearHolding>();
            HiddenHarbours.World.SaveGearSource.Collect(reloaded, null, GameConfig.CarriedFromStart(null), held);
            Assert.IsTrue(held.Exists(h => h.Id == PlayerGear.HeadlampId), "the save source lists the headlamp");
            Assert.AreEqual(1, held.FindAll(h => h.Id == PlayerGear.HeadlampId).Count, "once");

            HiddenHarbours.World.NotebookGearPageDef page = HiddenHarbours.World.NotebookContentSource.GearPage();
            Assert.IsNotNull(page, "the shipped KIT page Def loads");
            List<NotebookBlock> blocks = HiddenHarbours.World.NotebookGearPage.Blocks(held, page, KitPageCols);

            NotebookBlock lamp = blocks.Find(b => b.SourceId == HiddenHarbours.World.NotebookGearPage.SourcePrefix
                                                              + PlayerGear.HeadlampId);
            Assert.IsNotNull(lamp, "the KIT page has a block for the headlamp");
            bool named = false;
            for (int i = 0; i < lamp.Rows.Count; i++) named |= lamp.Rows[i].Text == "Headlamp";
            Assert.IsTrue(named, "lettered 'Headlamp'");
        }

        private const int KitPageCols = 31;

        // A save stand-in that actually stores flags (StartingGear's once-guard needs Get/SetFlag).
        private sealed class FlagSaveService : ISaveService
        {
            private readonly Dictionary<string, bool> _flags = new();
            public FlagSaveService(SaveData data) { Current = data; }
            public SaveData Current { get; }
            public int Saves { get; private set; }
            public bool GetFlag(string key) => _flags.TryGetValue(key, out var v) && v;
            public void SetFlag(string key, bool value) { if (!string.IsNullOrEmpty(key)) _flags[key] = value; }
            public void Save() => Saves++;
        }
    }
}
