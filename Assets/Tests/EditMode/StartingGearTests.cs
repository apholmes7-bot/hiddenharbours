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
                    sg.Configure(new[] { PlayerGear.ShovelId, PlayerGear.BucketId }, new string[0], "flag.granted");

                    Assert.AreEqual(2, sg.GrantOnce(), "first call grants the kit");
                    Assert.IsTrue(fake.GetFlag("flag.granted"), "the grant flag is set so it won't re-run");
                    Assert.AreEqual(0, sg.GrantOnce(), "the flag short-circuits a second grant");
                }
                finally { UnityEngine.Object.DestroyImmediate(go); }
            }
            finally { GameServices.Reset(); }
        }

        // ---- the headlamp: carried from the start, recorded as owned --------------------------

        [Test]
        public void ANewGame_RecordsTheHeadlampAsOwned_WithTheStartingKit()
        {
            var save = NewSave();
            var fake = new FlagSaveService(save);
            GameServices.Reset();
            GameServices.Save = fake;
            var go = new UnityEngine.GameObject("StartingGear");
            try
            {
                var sg = go.AddComponent<StartingGear>();   // the shipped defaults, as on the player
                Assert.IsFalse(PlayerGear.HasHeadlamp(save), "nothing granted yet");

                Assert.AreEqual(3, sg.GrantOnce(), "shovel, bucket and the headlamp from his pocket");
                Assert.IsTrue(PlayerGear.HasHeadlamp(save), "the headlamp is owned");
                Assert.IsTrue(PlayerGear.Owns(save, PlayerGear.HeadlampId));
                Assert.IsTrue(PlayerGear.CanDig(save) && PlayerGear.HasBucket(save), "the kit still lands");

                Assert.AreEqual(0, sg.GrantOnce(), "a second load adds nothing");
                Assert.AreEqual(1, save.OwnedGear.FindAll(id => id == PlayerGear.HeadlampId).Count, "recorded once");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
                GameServices.Reset();
            }
        }

        [Test]
        public void AGameBegunBeforeTheHeadlampWasRecorded_GainsItOnLoad_AndIsSaved()
        {
            // The once-flag was set long ago with only the shovel and bucket.
            var save = NewSave();
            save.OwnedGear.AddRange(new[] { PlayerGear.ShovelId, PlayerGear.BucketId, PlayerGear.RodId });
            var fake = new FlagSaveService(save);
            fake.SetFlag("st_peters_starting_gear_granted", true);
            GameServices.Reset();
            GameServices.Save = fake;
            var go = new UnityEngine.GameObject("StartingGear");
            try
            {
                var sg = go.AddComponent<StartingGear>();

                Assert.AreEqual(1, sg.GrantOnce(), "only the headlamp is new");
                Assert.IsTrue(PlayerGear.HasHeadlamp(save));
                Assert.AreEqual(1, fake.Saves, "the new record is written to disk");
                CollectionAssert.AreEqual(
                    new[] { PlayerGear.ShovelId, PlayerGear.BucketId, PlayerGear.RodId, PlayerGear.HeadlampId },
                    save.OwnedGear, "nothing stripped, nothing doubled");

                Assert.AreEqual(0, sg.GrantOnce(), "and the next load is quiet");
                Assert.AreEqual(1, fake.Saves, "no further writes");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
                GameServices.Reset();
            }
        }

        [Test]
        public void TheOwnedHeadlamp_SurvivesASaveRoundTrip_InTheCurrentFormat()
        {
            var save = NewSave();
            StartingGear.Grant(save, new[] { PlayerGear.ShovelId, PlayerGear.BucketId, PlayerGear.HeadlampId });

            SaveData reloaded = SaveSerialization.FromJson(SaveSerialization.ToJson(save));

            Assert.IsNotNull(reloaded);
            Assert.AreEqual(SaveMigration.CurrentVersion, reloaded.SchemaVersion, "no new save version");
            Assert.IsTrue(PlayerGear.HasHeadlamp(reloaded), "the headlamp is still owned after a reload");
            CollectionAssert.AreEqual(save.OwnedGear, reloaded.OwnedGear);
        }

        [Test]
        public void TheKitPage_ListsTheOwnedHeadlamp()
        {
            var save = NewSave();
            StartingGear.Grant(save, new[] { PlayerGear.ShovelId, PlayerGear.BucketId, PlayerGear.HeadlampId });
            SaveData reloaded = SaveSerialization.FromJson(SaveSerialization.ToJson(save));

            var held = new List<HiddenHarbours.World.NotebookGearHolding>();
            HiddenHarbours.World.SaveGearSource.Collect(reloaded, null, held);
            Assert.IsTrue(held.Exists(h => h.Id == PlayerGear.HeadlampId), "the save source reads the headlamp");

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
