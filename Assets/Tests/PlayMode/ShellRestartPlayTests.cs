using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.App;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// M1 §7.8 — what quit-to-title is allowed to take down with it.
    ///
    /// <para>Going back to the title rebuilds the world rather than drawing a page over it, which means
    /// destroying the persistent core (<see cref="ShellRestart.DestroyPersistentCore"/>). Some services
    /// deliberately do NOT go with it: the save service, the audio mix, the licence wallet and the species
    /// factory are launch-scoped singletons whose bootstraps run <b>once per launch</b> and whose
    /// registrations happen in <c>Awake</c>/<c>OnEnable</c> — neither of which runs again for an object that
    /// survived. So anything the teardown unregisters on their behalf is unregistered <b>for the rest of the
    /// launch</b>: a title with no Continue on a game that was saved seconds ago, nothing able to write to
    /// disk again, and a settings sheet insisting there is no sound while the director plays on. That is the
    /// property these tests pin, and it is pinnable without a scene reload — which is why
    /// <see cref="ShellRestart.DestroyPersistentCore"/> is public and static.</para>
    ///
    /// <para>The survivors are stood in for by in-file fakes rather than the real singletons: the real
    /// <c>SaveService</c> reads and writes the machine's actual save file, and the real
    /// <c>AudioDirector</c> starts a mix. The property under test is "the teardown does not null a slot it
    /// did not fill", and a fake occupies a slot exactly as well as the real thing does.</para>
    /// </summary>
    public class ShellRestartPlayTests
    {
        // ---- stand-ins for the singletons that outlive the core ------------------------------

        private sealed class FakeSave : ISaveService
        {
            public SaveData Current { get; } = SaveMigration.NewGame();
            public bool LoadedExistingSave => true;      // a game the player could go back to
            public bool GetFlag(string key) => false;
            public void SetFlag(string key, bool value) { }
            public void Save() { }
        }

        private sealed class FakeMix : IAudioMix
        {
            public float MasterVolume { get; set; } = 1f;
            public float AmbienceVolume { get; set; } = 0.8f;
            public float SfxVolume { get; set; } = 1f;
            public float MusicVolume { get; set; } = 0.6f;
        }

        private sealed class FakeLicences : ILicenseService
        {
            public bool IsLicensed(string licenseId) => true;
            public void Grant(string licenseId) { }
            public int Count => 0;
        }

        private sealed class FakeCatchFactory : ICatchItemFactory
        {
            public bool TryCreate(string speciesId, out CatchItem item)
            {
                item = default;
                return false;
            }
        }

        private sealed class FakeClock : IGameClock
        {
            public double TotalSeconds { get; private set; }
            public GameTime Now => new GameTime(TotalSeconds);
            public Season Season => Season.EarlySpring;
            public int Year => 1;
            public int DayIndex => 0;
            public int DayOfSeason => 1;
            public Weekday Weekday => Weekday.Monday;
            public bool IsMarketDay => false;
            public float HourOfDay => 6f;
            public float DayFraction => 0.25f;
            public bool IsPaused { get; set; }
            public float TimeScale { get; set; } = 1f;
            public void SeekTo(double totalSeconds) => TotalSeconds = totalSeconds;
        }

        private readonly List<GameObject> _spawned = new();

        [SetUp]
        public void SetUp()
        {
            HiddenHarbours.UI.TitleScreen.CloseIfOpen();
            GameServices.Reset();
            ShellFlow.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _spawned.Count; i++)
                if (_spawned[i] != null) Object.Destroy(_spawned[i]);
            _spawned.Clear();

            HiddenHarbours.UI.TitleScreen.CloseIfOpen();
            GameServices.Reset();
            ShellFlow.Reset();
        }

        /// <summary>Stand up a persistent core: a root tagged <see cref="PersistentObject"/> carrying the
        /// real <see cref="GameRoot"/>, exactly what the teardown goes looking for.</summary>
        private GameObject GivenAPersistentCore()
        {
            var go = new GameObject("ServicesRoot");
            _spawned.Add(go);
            go.AddComponent<PersistentObject>();

            // A bare root has no clock or environment wired — those are Inspector references on the real
            // core — and it says so on the way up. Nothing to do with what is under test here.
            LogAssert.Expect(LogType.Error, new Regex("Services not wired"));
            go.AddComponent<GameRoot>();
            return go;
        }

        // ---- the survivors survive -----------------------------------------------------------

        [UnityTest]
        public IEnumerator QuitToTitleTeardown_LeavesTheSurvivingServicesRegistered()
        {
            var save = new FakeSave();
            var mix = new FakeMix();
            var licences = new FakeLicences();
            var catches = new FakeCatchFactory();
            GameServices.Save = save;
            GameServices.AudioMix = mix;
            GameServices.Licenses = licences;
            GameServices.CatchFactory = catches;

            GameObject core = GivenAPersistentCore();
            yield return null;                       // Awake + Start, as at boot

            Assert.GreaterOrEqual(ShellRestart.DestroyPersistentCore(), 1,
                "the teardown found a persistent core to take down");
            yield return null;                       // Destroy resolves — GameRoot.OnDestroy runs here

            Assert.IsTrue(core == null, "the persistent core is gone");

            Assert.AreSame(save, GameServices.Save,
                "the save service outlived the core, so it is still the save service — nothing " +
                "re-registers it, and without it nothing can ever write to disk again this launch");
            Assert.AreSame(mix, GameServices.AudioMix,
                "the mix is still there, or the settings sheet says there is no sound while it plays");
            Assert.AreSame(licences, GameServices.Licenses, "and the licence wallet");
            Assert.AreSame(catches, GameServices.CatchFactory,
                "and the species factory, registered once per launch at BeforeSceneLoad");

            Assert.IsTrue(ShellFlow.HasGameToContinue,
                "which is what the player sees: the title they just quit TO offers Continue on the " +
                "game the quit had just saved");
        }

        // ---- and the core takes back only its own --------------------------------------------

        [UnityTest]
        public IEnumerator Teardown_DoesNotStompAReplacementRootsWiring()
        {
            GameObject core = GivenAPersistentCore();
            yield return null;

            ShellRestart.DestroyPersistentCore();

            // The replacement core wires itself. In the real quit-to-title the boot scene is loaded a frame
            // AFTER the destroy precisely so this cannot overlap — but the outgoing root's teardown is
            // guarded on identity as well, so the ordering is belt AND braces rather than the only thing
            // standing between a rebuilt world and a null clock.
            var replacement = new FakeClock();
            GameServices.Clock = replacement;

            yield return null;                       // the outgoing root's OnDestroy lands here

            Assert.IsTrue(core == null);
            Assert.AreSame(replacement, GameServices.Clock,
                "the outgoing root unregisters what IT registered, not whatever happens to be in the slot");
        }
    }
}
