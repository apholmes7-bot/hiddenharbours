using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.World.EditMode
{
    /// <summary>
    /// Onboarding flags (VS-21): named accessors over a store, and — the key acceptance — that a
    /// PlayerPrefs-backed store PERSISTS, so a fresh instance (a stand-in for quit + reload) reads
    /// the flags back. Uses an isolated test prefix and deletes those keys in teardown so it never
    /// pollutes real prefs.
    /// </summary>
    public class OnboardingFlagsTests
    {
        private const string TestPrefix = "hh.test.flag.";

        [TearDown]
        public void TearDown()
        {
            // Remove every key this fixture could have written, under the isolated test prefix.
            foreach (var key in new[] { OnboardingFlags.MetGinnyKey, OnboardingFlags.ReadLogbookKey,
                                        OnboardingFlags.OnboardedKey, "custom_key" })
                PlayerPrefs.DeleteKey(TestPrefix + key);
            PlayerPrefs.Save();
        }

        [Test]
        public void Defaults_AreAllFalse()
        {
            var flags = new OnboardingFlags(new InMemoryFlagStore());
            Assert.IsFalse(flags.MetGinny);
            Assert.IsFalse(flags.ReadLogbook);
            Assert.IsFalse(flags.Onboarded);
        }

        [Test]
        public void NamedAccessors_RoundTrip_OnInMemoryStore()
        {
            var flags = new OnboardingFlags(new InMemoryFlagStore());

            flags.MetGinny = true;
            flags.Onboarded = true;

            Assert.IsTrue(flags.MetGinny);
            Assert.IsFalse(flags.ReadLogbook, "untouched flag stays false");
            Assert.IsTrue(flags.Onboarded);
        }

        [Test]
        public void GetSetByKey_IgnoresEmptyKeys()
        {
            var flags = new OnboardingFlags(new InMemoryFlagStore());

            flags.Set("custom_key", true);
            Assert.IsTrue(flags.Get("custom_key"));

            Assert.IsFalse(flags.Get(""), "empty key reads false");
            Assert.DoesNotThrow(() => flags.Set("", true), "empty key set is a no-op");
            Assert.DoesNotThrow(() => flags.Set(null, true));
            Assert.IsFalse(flags.Get(null));
        }

        [Test]
        public void PlayerPrefsStore_Persists_AcrossAFreshInstance()
        {
            // First "session": set the flags through a PlayerPrefs-backed store.
            var write = new OnboardingFlags(new PlayerPrefsFlagStore(TestPrefix));
            write.MetGinny = true;
            write.ReadLogbook = true;
            write.Onboarded = true;

            // Second "session": a brand-new store + flags reading the same prefs (simulates reload).
            var reload = new OnboardingFlags(new PlayerPrefsFlagStore(TestPrefix));
            Assert.IsTrue(reload.MetGinny,    "met_ginny should survive a reload");
            Assert.IsTrue(reload.ReadLogbook, "read_logbook should survive a reload");
            Assert.IsTrue(reload.Onboarded,   "onboarded should survive a reload — the opening must not re-trigger");
        }

        [Test]
        public void PlayerPrefsStore_ClearingAFlag_Persists()
        {
            var write = new OnboardingFlags(new PlayerPrefsFlagStore(TestPrefix));
            write.MetGinny = true;
            write.MetGinny = false;

            var reload = new OnboardingFlags(new PlayerPrefsFlagStore(TestPrefix));
            Assert.IsFalse(reload.MetGinny, "a cleared flag stays cleared across a reload");
        }
    }
}
