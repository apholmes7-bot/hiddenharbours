using System.Collections;
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
    /// The per-passage arrival seam END TO END, across a real region hop: a real
    /// <see cref="RegionPassage"/> in the region you leave, a real <see cref="RegionSceneLoader"/>
    /// switching a real active scene, and a real <see cref="RegionAnchor"/> in the region you arrive in —
    /// the whole chain <c>passage → GameServices → activeSceneChanged → coordinator → anchor → the rig</c>.
    ///
    /// <para><b>Why this exists on top of the pure tests.</b> <c>PerPassageArrivalTests</c> proves the
    /// fallback rule and proves <c>ApplyArrival</c> puts the fisher where the key says. It cannot prove
    /// the key ever ARRIVES — that the string a passage publishes in one scene survives a scene switch
    /// and reaches the coordinator, once, before anything else reads it. That is a rig fact, and a rig
    /// that is not scene-faithful lies about it (the punt-steer lesson).</para>
    ///
    /// <para><b>Two loaded scenes, not one.</b> The coordinator DEACTIVATES the roots of the scene it
    /// leaves, so a test that travelled out of the test runner's own scene would switch the runner off
    /// mid-assert. Both ends of the hop are therefore scenes this test creates and owns.</para>
    ///
    /// <para><b>No frame-count-as-time assertions.</b> The single <c>yield return null</c> is there to let
    /// the scene event flush, not to measure anything — headless frames are nowhere near real ones.</para>
    /// </summary>
    public class PerPassageArrivalPlayTests
    {
        private const string SourceSceneName = "PerPassageArrival_Source";
        private const string DestSceneName = "PerPassageArrival_Dest";
        private const string BarKey = "bar";

        private static readonly Vector3 WharfBerth = new Vector3(101f, 84.5f, 0f);
        private static readonly Vector3 WharfDeck = new Vector3(98f, 88f, 0f);
        private static readonly Vector3 BarLanding = new Vector3(58f, -146f, 0f);

        private Scene _origin;
        private Scene _source;
        private Scene _dest;
        private RegionDef _destRegion;
        private GameObject _coordinatorGo;
        private GameObject _player;
        private GameObject _boat;

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();

            _origin = SceneManager.GetActiveScene();
            _source = SceneManager.CreateScene(SourceSceneName);
            _dest = SceneManager.CreateScene(DestSceneName);

            _destRegion = ScriptableObject.CreateInstance<RegionDef>();
            _destRegion.Id = "region.per_passage_dest";
            _destRegion.SceneName = DestSceneName;

            // The persistent rig lives OUTSIDE both region scenes, exactly as the real one does (it is
            // DontDestroyOnLoad), so the coordinator's root-toggling never touches it.
            _player = new GameObject("PersistentPlayer");
            _boat = new GameObject("PersistentBoat");

            // The destination's anchor, in the destination scene, authored the way the mainland builder
            // authors it: one default arrival at the wharf plus a named walk-in over the bar.
            var anchorGo = new GameObject("DestRegionAnchor");
            anchorGo.SetActive(false);                    // configure BEFORE enable, like the builder
            SceneManager.MoveGameObjectToScene(anchorGo, _dest);
            var anchor = anchorGo.AddComponent<RegionAnchor>();
            anchor.Configure(_destRegion.Id,
                             ChildAt(anchorGo, "Arrival", WharfBerth),
                             ChildAt(anchorGo, "Dock", new Vector3(98f, 84.5f, 0f)),
                             ChildAt(anchorGo, "Disembark", WharfDeck));
            anchor.ConfigureArrivals(
                new NamedArrival(BarKey, null, ChildAt(anchorGo, "BarLanding", BarLanding)));
            anchorGo.SetActive(true);

            // Stand in the SOURCE region before the coordinator starts listening, so its first event is
            // the crossing under test rather than our own setup.
            SceneManager.SetActiveScene(_source);

            _coordinatorGo = new GameObject("RegionTravelCoordinator");
            _coordinatorGo.SetActive(false);
            var coordinator = _coordinatorGo.AddComponent<RegionTravelCoordinator>();
            coordinator.Configure(_player.transform, _boat.transform, null, null);
            _coordinatorGo.SetActive(true);               // OnEnable subscribes
        }

        [UnityTearDown]
        public IEnumerator TearDownScenes()
        {
            // Stop listening BEFORE the scenes come apart — a coordinator still subscribed would react to
            // the teardown's own active-scene change and start toggling roots that are being destroyed.
            if (_coordinatorGo != null) Object.DestroyImmediate(_coordinatorGo);
            if (_player != null) Object.DestroyImmediate(_player);
            if (_boat != null) Object.DestroyImmediate(_boat);
            if (_destRegion != null) Object.DestroyImmediate(_destRegion);

            if (_origin.IsValid() && _origin.isLoaded) SceneManager.SetActiveScene(_origin);
            if (_dest.IsValid() && _dest.isLoaded) yield return SceneManager.UnloadSceneAsync(_dest);
            if (_source.IsValid() && _source.isLoaded) yield return SceneManager.UnloadSceneAsync(_source);

            GameServices.Reset();
        }

        private static Transform ChildAt(GameObject parent, string name, Vector3 position)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, worldPositionStays: false);
            go.transform.position = position;
            return go.transform;
        }

        /// <summary>Position assertions carry a tolerance because these points arrive through a parented
        /// transform — the claim is "the rig landed HERE", not "the float survived a matrix".</summary>
        private static void AssertAt(Vector3 expected, Vector3 actual, string because) =>
            Assert.That(Vector3.Distance(expected, actual), Is.LessThan(1e-3f),
                        $"{because}\n  expected {expected}, was {actual}");

        /// <summary>A passage in the source scene, wired to cross into the destination region.</summary>
        private RegionPassage MakePassage(string arrivalKey)
        {
            var loaderGo = new GameObject("RegionSceneLoader");
            loaderGo.SetActive(false);                    // so Awake reads the SOURCE scene's name…
            SceneManager.MoveGameObjectToScene(loaderGo, _source);
            var loader = loaderGo.AddComponent<RegionSceneLoader>();
            loaderGo.SetActive(true);                     // …not the test runner's

            var passageGo = new GameObject("Passage");
            SceneManager.MoveGameObjectToScene(passageGo, _source);
            var passage = passageGo.AddComponent<RegionPassage>();
            passage.Configure(_destRegion, loader, arrivalKey);
            return passage;
        }

        // =============================================================================================

        [UnityTest]
        public IEnumerator TheKeyThePassagePublishes_ReachesTheArrivalItNames()
        {
            // ⭐ The whole point of the seam, driven rather than reasoned about. Walk in over the bar and
            // the fisher must be standing on the bar when the scene settles — not on a wharf deck 400 m
            // away that they never walked to.
            MakePassage(BarKey).Activate();
            yield return null;

            Assert.AreEqual(DestSceneName, SceneManager.GetActiveScene().name,
                "precondition: the crossing actually happened");
            AssertAt(BarLanding, _player.transform.position,
                "the arrival key crossed the seam: passage → GameServices → coordinator → the named " +
                "arrival. A fisher on the wharf here means the key was published, dropped, or never read");
            AssertAt(WharfBerth, _boat.transform.position,
                "and the boat they did not bring stays at the region's own berth — the two points fall " +
                "back independently, or walking in drags your boat across the region after you");
        }

        [UnityTest]
        public IEnumerator AKeylessPassage_LandsAtTheRegionsOwnArrival()
        {
            // Every region built before the mainland has exactly this shape, so this is the additive
            // claim: a passage that names nothing behaves as it did before the seam existed.
            MakePassage("").Activate();
            yield return null;

            Assert.AreEqual(DestSceneName, SceneManager.GetActiveScene().name);
            AssertAt(WharfDeck, _player.transform.position, "a keyless passage lands at the default");
            AssertAt(WharfBerth, _boat.transform.position, "…and parks the boat at the default berth");
        }

        [UnityTest]
        public IEnumerator TheKeyIsSpentByTheCrossingItWasSetFor()
        {
            // A key that survives its own crossing is a key that steers the NEXT one. Consume-once is
            // asserted here rather than only on the accessor because the coordinator is what has to do it.
            MakePassage(BarKey).Activate();
            yield return null;

            Assert.IsNull(GameServices.PendingArrivalKey,
                "the coordinator must take the key on arrival, whether or not it recognised it");
        }

        [UnityTest]
        public IEnumerator ADeclinedTravel_TakesItsKeyBack()
        {
            // The one way a key could be left standing with nobody to consume it: a passage that fires
            // and does NOT travel (you are already there; the scene is not in Build Settings). Nothing
            // raises activeSceneChanged, so the passage has to clean up after itself.
            var alreadyHere = ScriptableObject.CreateInstance<RegionDef>();
            try
            {
                alreadyHere.Id = "region.per_passage_source";
                alreadyHere.SceneName = SourceSceneName;      // we are standing in it

                var loaderGo = new GameObject("RegionSceneLoader");
                loaderGo.SetActive(false);
                SceneManager.MoveGameObjectToScene(loaderGo, _source);
                var loader = loaderGo.AddComponent<RegionSceneLoader>();
                loaderGo.SetActive(true);

                var passageGo = new GameObject("PassageToNowhere");
                SceneManager.MoveGameObjectToScene(passageGo, _source);
                var passage = passageGo.AddComponent<RegionPassage>();
                passage.Configure(alreadyHere, loader, BarKey);

                passage.Activate();
                yield return null;

                Assert.AreEqual(SourceSceneName, SceneManager.GetActiveScene().name,
                    "precondition: this travel was declined, so no crossing happened");
                Assert.IsNull(GameServices.PendingArrivalKey,
                    "a passage whose travel was declined must take its key back — left standing, the " +
                    "next arrival in the game would be steered by a crossing that never happened");
            }
            finally { Object.DestroyImmediate(alreadyHere); }
        }
    }
}
