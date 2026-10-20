using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using HiddenHarbours.App;
using HiddenHarbours.Core;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// <b>The opening's guide travels with the persistent core</b>, so it is still listening when the dory is
    /// bought and repaired a region away — and quit to title still takes it down.
    ///
    /// <para><b>The bug this pins.</b> Most of the guide's beats are heard AWAY from St Peters: the licence,
    /// the rod, buying the dory and repairing her all happen at the Nine Mile Creek shipwright. A crossing
    /// switches off every root of the region left behind (<see cref="RegionTravelCoordinator"/>), and a guide
    /// left in St Peters' scene went with them: disabled, it unsubscribed, never heard the repair, and the
    /// opening never closed. On a <see cref="PersistentObject"/> root, Awake promotes it into
    /// DontDestroyOnLoad, which no crossing touches. That the boot scene and its builder put it there is
    /// <c>OnboardingSurvivesTravelTests</c>' (EditMode); this file is the travel.</para>
    ///
    /// <para><b>⚠ The save.</b> The guide keeps <c>onboarded</c> through <see cref="SaveFlagStore"/>, which
    /// writes through <see cref="GameServices.Save"/>: here a double that holds flags in memory and writes
    /// nothing. The runner's own <see cref="SaveService"/> (installed before any scene, DontDestroyOnLoad)
    /// hears BoatPurchased too, and would record the dory and write; the dory case holds it shut and proves
    /// its file untouched.</para>
    /// </summary>
    public class OnboardingSurvivesTravelPlayTests
    {
        private const string HomeSceneName = "OnboardingTravel_Home";
        private const string AwaySceneName = "OnboardingTravel_Away";
        private const string DoryId = "boat.dory";

        /// <summary>The flags in memory; nothing on disk.</summary>
        private sealed class FlagSave : ISaveService
        {
            private readonly Dictionary<string, bool> _flags = new();
            public SaveData Current { get; } = SaveMigration.NewGame();
            public bool GetFlag(string key) => _flags.TryGetValue(key, out bool v) && v;
            public void SetFlag(string key, bool value) { if (!string.IsNullOrEmpty(key)) _flags[key] = value; }
            public void Save() { }
        }

        private Scene _origin;
        private Scene _home;
        private Scene _away;
        private FlagSave _save;
        private GameObject _player;
        private GameObject _boat;
        private GameObject _coordinatorGo;
        private GameObject _homeRoot;
        private GameObject _guide;
        private QuestPanelPresenter _note;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            GameServices.Reset();
            _save = new FlagSave();
            GameServices.Save = _save;

            _origin = SceneManager.GetActiveScene();
            _home = SceneManager.CreateScene(HomeSceneName);
            _away = SceneManager.CreateScene(AwaySceneName);

            // The rest of the persistent rig, OUTSIDE both regions as the real one is.
            _player = new GameObject("PersistentPlayer");
            _boat = new GameObject("PersistentBoat");

            // Boot: the guide is authored in the home region, as St Peters authors it, beside a root that is
            // not persistent. Its Awake promotes it out of the scene on the spot.
            SceneManager.SetActiveScene(_home);
            _homeRoot = new GameObject("HomeRegionRoot");
            _guide = new GameObject("Onboarding", typeof(OnboardingDirector), typeof(PersistentObject));

            // The coordinator listens from outside both regions too (RegionDialogueTravelPlayTests' order).
            _coordinatorGo = new GameObject("RegionTravelCoordinator");
            _coordinatorGo.SetActive(false);
            SceneManager.MoveGameObjectToScene(_coordinatorGo, _origin);
            _coordinatorGo.AddComponent<RegionTravelCoordinator>()
                          .Configure(_player.transform, _boat.transform, null, null);
            _coordinatorGo.SetActive(true);

            yield return null;   // the guide's first Update puts step one on its note
            _note = _guide.GetComponent<QuestPanelPresenter>();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            // Stop listening BEFORE the scenes come apart, or the teardown's own active-scene change toggles roots.
            if (_coordinatorGo != null) Object.DestroyImmediate(_coordinatorGo);
            if (_guide != null) Object.DestroyImmediate(_guide);
            if (_player != null) Object.DestroyImmediate(_player);
            if (_boat != null) Object.DestroyImmediate(_boat);

            if (_origin.IsValid() && _origin.isLoaded) SceneManager.SetActiveScene(_origin);
            if (_away.IsValid() && _away.isLoaded) yield return SceneManager.UnloadSceneAsync(_away);
            if (_home.IsValid() && _home.isLoaded) yield return SceneManager.UnloadSceneAsync(_home);
            GameServices.Reset();
        }

        [UnityTest]
        public IEnumerator TheGuide_StaysOnAcrossACrossing()
        {
            Assert.AreEqual(HomeSceneName, _guide.GetComponent<PersistentObject>().AuthoredInScene,
                "sanity: authored in the home region, as the boot scene authors it in St Peters");

            yield return CrossToTheAwayRegion();

            Assert.That(_guide.scene, Is.EqualTo(PersistentScene()), "it lives in DontDestroyOnLoad, no region's root");
            Assert.IsTrue(_guide.activeInHierarchy,
                "⭐ the crossing that switched the home region off left the guide on");
            Assert.IsTrue(_guide.GetComponent<OnboardingDirector>().isActiveAndEnabled, "and listening");
            Assert.IsTrue(_note.IsShowing, "and its note still draws, a region away from home");
        }

        [UnityTest]
        public IEnumerator TheDoryBoughtAndRepairedAway_SetsOnboarded()
        {
            yield return CrossToTheAwayRegion();
            Assert.IsFalse(_save.GetFlag(OnboardingFlags.OnboardedKey), "sanity: a fresh opening");

            // ⚠ Before any signal: a live save service writes the run's own file under Temp, never the player's.
            var live = Object.FindAnyObjectByType<SaveService>();
            string liveFile = live != null ? live.SavePath : null;
            if (live != null)
                Assert.That(liveFile, Is.Not.Empty.And.EqualTo(System.Environment.GetEnvironmentVariable(SaveStore.TestRunSaveVariable)),
                    "the runner's save is not on TestRunSave's redirect: refusing to raise a purchase it would hear");
            System.DateTime? writtenBefore = WrittenAt(liveFile);

            RaiseTheDoryBeats_WithTheRunnersSaveHeldShut(live);

            Assert.IsTrue(_save.GetFlag(OnboardingFlags.OnboardedKey),
                "⭐ bought and repaired at the shipwright, a region away: the opening closes");
            Assert.That(WrittenAt(liveFile), Is.EqualTo(writtenBefore), "the runner's save file was not written");

            yield return null;   // the guide's Update
            // First the warm close, then the sail-home nudge; a slow first frame may already be on the second.
            Assert.That(_note.Text, Is.EqualTo(WorldStrings.OnboardDone).Or.EqualTo(WorldStrings.OnboardSailHome),
                "the guide says so, away from home");
        }

        [UnityTest]
        public IEnumerator QuitToTitle_DestroyPersistentCore_LeavesNoGuide()
        {
            yield return CrossToTheAwayRegion();

            // Like ShellRestartPlayTests', this takes down EVERY persistent root resident in the run.
            Assert.GreaterOrEqual(ShellRestart.DestroyPersistentCore(), 1, "the guide is one of the core's roots");
            yield return null;   // Destroy lands at the end of the frame

            Assert.IsTrue(_guide == null, "⭐ quit to title takes the guide down with the rest of the core");
            Assert.IsFalse(PersistentRoots().Any(go => go.GetComponentInChildren<OnboardingDirector>(true) != null),
                "no guide is left in DontDestroyOnLoad, so the boot reload makes the only one");
        }

        // ---- helpers --------------------------------------------------------------------------------

        /// <summary>Sail out of the home region: the away scene becomes active and the coordinator switches
        /// the home region's roots off — what silenced the guide while it lived there.</summary>
        private IEnumerator CrossToTheAwayRegion()
        {
            SceneManager.SetActiveScene(_away);
            yield return null;
            Assert.IsFalse(_homeRoot.activeSelf, "control: the crossing switched the home region's roots off");
        }

        /// <summary>
        /// The shipwright's two dory signals, in one frame, with the runner's SaveService held shut. It hears
        /// BoatPurchased (RecordActiveBoat), which adds the dory to its blob and calls Save. So it scribbles on
        /// a COPY of its blob, and the shell stands at the title for the frame, where Save refuses
        /// (<see cref="SaveService.WritesAllowed"/>). Both go back in the finally, before anything else runs.
        /// </summary>
        private static void RaiseTheDoryBeats_WithTheRunnersSaveHeldShut(SaveService live)
        {
            SaveData blob = live != null ? live.Current : null;
            ShellPhase phase = ShellFlow.Phase;
            try
            {
                if (blob != null) SetCurrent(live, JsonUtility.FromJson<SaveData>(JsonUtility.ToJson(blob)));
                SetPhase(ShellPhase.Title);
                EventBus.Publish(new BoatPurchased(DoryId, 0));
                EventBus.Publish(new BoatRepaired(DoryId, 0));
            }
            finally
            {
                SetPhase(phase);
                if (blob != null) SetCurrent(live, blob);
            }
        }

        private static System.DateTime? WrittenAt(string file) =>
            !string.IsNullOrEmpty(file) && File.Exists(file) ? File.GetLastWriteTimeUtc(file) : (System.DateTime?)null;

        private static void SetCurrent(SaveService service, SaveData data) =>
            typeof(SaveService).GetProperty(nameof(SaveService.Current)).SetValue(service, data);

        private static void SetPhase(ShellPhase phase) =>
            typeof(ShellFlow).GetProperty(nameof(ShellFlow.Phase)).SetValue(null, phase);

        /// <summary>The DontDestroyOnLoad scene, reached the only way Unity offers: put something in it and ask
        /// what scene it landed in (StPetersEastDoorPlayTests').</summary>
        private static Scene PersistentScene()
        {
            var probe = new GameObject("__ddolProbe");
            Object.DontDestroyOnLoad(probe);
            Scene s = probe.scene;
            Object.DestroyImmediate(probe);
            return s;
        }

        private static List<GameObject> PersistentRoots()
        {
            Scene s = PersistentScene();
            return s.IsValid() ? s.GetRootGameObjects().ToList() : new List<GameObject>();
        }
    }
}
