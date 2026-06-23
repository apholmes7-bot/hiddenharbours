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
            Assert.IsFalse(PlayerGear.CanRodFish(save), "still no rod — that's bought at Greywick");

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

        // A save stand-in that actually stores flags (StartingGear's once-guard needs Get/SetFlag).
        private sealed class FlagSaveService : ISaveService
        {
            private readonly Dictionary<string, bool> _flags = new();
            public FlagSaveService(SaveData data) { Current = data; }
            public SaveData Current { get; }
            public bool GetFlag(string key) => _flags.TryGetValue(key, out var v) && v;
            public void SetFlag(string key, bool value) { if (!string.IsNullOrEmpty(key)) _flags[key] = value; }
            public void Save() { }
        }
    }
}
