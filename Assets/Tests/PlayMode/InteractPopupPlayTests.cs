using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using HiddenHarbours.Core;
using HiddenHarbours.UI;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// <b>The one popup, in a running scene.</b> A real <c>WorldInteractor</c> computing candidacy from a
    /// real player transform, the real Core offer channel between them, and the real
    /// <see cref="InteractPopup"/> drawing the answer — end to end, with nothing stubbed between the
    /// facing and the words on screen.
    ///
    /// <para><b>What this proves that EditMode cannot.</b> Three things. (1) The modules are actually
    /// wired: World publishes, Core arbitrates, UI answers, and no module references another. A seam that
    /// compiles is not a seam that connects. (2) <c>Awake</c>/<c>OnEnable</c> run — so the popup's
    /// subscription, its self-built canvas and the interactor's per-frame offer are the real ones.
    /// (3) The offer is recomputed FRAME BY FRAME as the player turns, which is the whole behaviour the
    /// ruling asked for and is not observable without a frame.</para>
    ///
    /// <para><b>⚠ Driven through component APIs, never through keys.</b> A virtual key press is
    /// undeliverable to the new Input System's device state in a headless run, so a test that "pressed E"
    /// would assert on a press that never happened. Facing is set through
    /// <see cref="InteractActorProbe"/> — which is exactly what <c>ControlSwitcher</c> writes every frame
    /// — and the interactor's own <c>Update</c> does the rest.</para>
    ///
    /// <para><b>⚠ The popup under test is the INSTALLED one.</b> It self-installs at
    /// <c>AfterSceneLoad</c> onto a <c>DontDestroyOnLoad</c> host, so a second one stood up here would be
    /// a rival subscriber to the same channel. <see cref="InteractPopup.Instance"/> is that one, and if
    /// the run has not installed it yet this stands one up and claims the slot.</para>
    ///
    /// <para>The scene is created here and unloaded in teardown — a scene left resident reds other files
    /// with failures that read like gameplay bugs.</para>
    /// </summary>
    public class InteractPopupPlayTests
    {
        const string SceneName = "InteractPopup_Test";
        const float Reach = 1.8f;

        readonly List<Object> _spawned = new();
        Scene _scene;
        InteractPopup _popup;
        GameObject _popupHost;          // only set when THIS test had to create it
        Transform _player;
        WorldInteractor _interactor;
        Interactable _ginny, _bram;

        [SetUp]
        public void SetUp()
        {
            _scene = SceneManager.CreateScene(SceneName);
            SceneManager.SetActiveScene(_scene);

            InteractOffer.Reset();
            InteractActorProbe.Reset();
            InteractionGate.Reset();
            InteractActionClaim.Reset();
            InteractVerb.Reset();
            Interactables.Clear();
            GameServices.Reset();

            _popup = InteractPopup.Instance;
            if (_popup == null)
            {
                _popupHost = new GameObject("InteractPopup_UnderTest");
                _popup = _popupHost.AddComponent<InteractPopup>();
            }

            // The fisher, and two neighbours a pace either side of her — the case the ruling is about.
            var playerGo = Spawn("Player");
            _player = playerGo.transform;
            _player.position = Vector3.zero;

            _ginny = MakeInteractable("Ginny", new Vector3(1.0f, 0f, 0f),
                                      InteractKind.Talk, WorldStrings.GinnyName, "ginny");
            _bram  = MakeInteractable("Bram", new Vector3(0f, 1.0f, 0f),
                                      InteractKind.Talk, WorldStrings.NeighbourName, "neighbour");

            _interactor = Spawn("WorldInteractor").AddComponent<WorldInteractor>();
            _interactor.Configure(_player, null, new[] { _ginny, _bram }, Reach);
        }

        /// <summary>
        /// ⚠ <b><see cref="UnityTearDownAttribute"/>, and the unload is AWAITED.</b>
        /// <see cref="SceneManager.UnloadSceneAsync"/> is asynchronous: a plain <c>[TearDown]</c> that
        /// fired it and returned left the scene still resident when the next test's <c>SetUp</c> ran, and
        /// <see cref="SceneManager.CreateScene"/> threw <i>"Scene with name … already exists"</i> on every
        /// test after the first. Yielding on it is the convention every other scene-building suite here
        /// already uses (<c>WardrobePickerPlayTests</c>, <c>RegionDialogueTravelPlayTests</c>) — and it is
        /// not optional politeness, it is the difference between one test passing and six failing.
        /// </summary>
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            InteractOffer.Reset();
            InteractActorProbe.Reset();
            InteractionGate.Reset();
            InteractActionClaim.Reset();
            InteractVerb.Reset();
            Interactables.Clear();
            GameServices.Reset();

            foreach (var o in _spawned) if (o != null) Object.Destroy(o);
            _spawned.Clear();
            if (_popupHost != null) { Object.Destroy(_popupHost); _popupHost = null; }

            if (_scene.IsValid() && _scene.isLoaded) yield return SceneManager.UnloadSceneAsync(_scene);
        }

        GameObject Spawn(string name)
        {
            var go = new GameObject(name);
            SceneManager.MoveGameObjectToScene(go, _scene);
            _spawned.Add(go);
            return go;
        }

        Interactable MakeInteractable(string name, Vector3 at, InteractKind kind, string speaker, string convo)
        {
            var go = Spawn(name);
            go.transform.position = at;
            var it = go.AddComponent<Interactable>();
            it.Configure(kind, speaker, convo, "");
            return it;
        }

        /// <summary>Point the fisher somewhere, exactly as <c>ControlSwitcher</c> does each frame — the
        /// same <see cref="InteractActor"/> it composes, published on the same Core probe.</summary>
        void Face(Vector2 facing, ControlMode mode = ControlMode.OnFoot)
        {
            _lastFacing = facing;
            InteractActorProbe.Set(InteractActor.For(_player.position, facing, mode));
        }

        /// <summary>Put the fisher somewhere else without turning her — the mode travels on the actor, so
        /// this is the same call.</summary>
        void EnterMode(ControlMode mode) => Face(_lastFacing, mode);

        Vector2 _lastFacing = Vector2.right;

        // =====================================================================================
        //  IT APPEARS, AND IT NAMES THE RIGHT ONE
        // =====================================================================================

        [UnityTest]
        public IEnumerator FacingAFixture_ThePopupAppearsAndNamesTheVerb()
        {
            Face(Vector2.right);                 // toward Ginny
            yield return null;                   // the interactor's Update computes and offers
            yield return null;                   // the popup's Apply draws it

            Assert.IsTrue(_popup.IsShowing, "the popup shows when there is a facing candidate");
            Assert.AreEqual(WorldStrings.OfferLabel(InteractKind.Talk, WorldStrings.GinnyName),
                            _popup.ShownLabel);
            Assert.AreEqual(InteractOfferSource.Conversation, _popup.Offer.Source);
        }

        [UnityTest]
        public IEnumerator StandingBetweenTwo_ItNamesTheONEYouAreLookingAt()
        {
            // The whole reason facing had to be added: both are the same distance away, so proximity alone
            // would be a coin toss dressed up as an affordance.
            Face(Vector2.right);
            yield return null; yield return null;
            Assert.AreEqual(WorldStrings.OfferLabel(InteractKind.Talk, WorldStrings.GinnyName),
                            _popup.ShownLabel, "looking east, it is Ginny");

            Face(Vector2.up);
            yield return null; yield return null;
            Assert.AreEqual(WorldStrings.OfferLabel(InteractKind.Talk, WorldStrings.NeighbourName),
                            _popup.ShownLabel, "looking north, it is Bram");
        }

        [UnityTest]
        public IEnumerator TurningAway_ClearsIt()
        {
            Face(Vector2.right);
            yield return null; yield return null;
            Assert.IsTrue(_popup.IsShowing);

            Face(Vector2.left);                  // both neighbours are now behind her
            yield return null; yield return null;

            Assert.IsFalse(_popup.IsShowing, "nothing in front means nothing shown");
            Assert.AreEqual("", _popup.ShownLabel);
        }

        [UnityTest]
        public IEnumerator WalkingOutOfReach_ClearsIt()
        {
            Face(Vector2.right);
            yield return null; yield return null;
            Assert.IsTrue(_popup.IsShowing);

            _player.position = new Vector3(-50f, 0f, 0f);
            Face(Vector2.right);                 // still looking the right way, simply far away
            yield return null; yield return null;

            Assert.IsFalse(_popup.IsShowing, "facing is a filter on top of reach, not instead of it");
        }

        [UnityTest]
        public IEnumerator ItNeverStacks_HoweverManyThingsAreAround()
        {
            // "It shows at most ONE line. It never stacks and never queues." Both neighbours are in reach
            // and inside a 120° arc centred on the diagonal; exactly one line comes out.
            Face(new Vector2(1f, 1f).normalized);
            yield return null; yield return null;

            Assert.IsTrue(_popup.IsShowing);
            Assert.IsFalse(_popup.ShownLabel.Contains("\n"), "one line, never two");
            Assert.IsTrue(_popup.ShownLabel.Length <= InteractPopupLayout.MaxCharacters);
        }

        // =====================================================================================
        //  THE SUPPRESSIONS
        // =====================================================================================

        [UnityTest]
        public IEnumerator AModalIsUp_SoNoPopup()
        {
            Face(Vector2.right);
            yield return null; yield return null;
            Assert.IsTrue(_popup.IsShowing);

            InteractionGate.IsBlocked = true;
            yield return null; yield return null;

            Assert.IsFalse(_popup.IsShowing,
                           "an offer that outlives its own actionability teaches the player a lie");

            InteractionGate.IsBlocked = false;
            yield return null; yield return null;
            Assert.IsTrue(_popup.IsShowing, "and it comes back when the modal closes");
        }

        [UnityTest]
        public IEnumerator AtTheHelmAndBehindAWheel_NothingIsShown_BecauseTheInstrumentsRule()
        {
            Face(Vector2.right);
            yield return null; yield return null;
            Assert.IsTrue(_popup.IsShowing);

            EnterMode(ControlMode.Aboard);
            yield return null; yield return null;
            Assert.IsFalse(_popup.IsShowing, "the dash owns the helm view");

            EnterMode(ControlMode.Driving);
            yield return null; yield return null;
            Assert.IsFalse(_popup.IsShowing, "and the cab");

            EnterMode(ControlMode.OnDeck);
            yield return null; yield return null;
            Assert.IsTrue(_popup.IsShowing,
                          "the DECK is not suppressed — it is a place you work and there is no dash up");
        }

        [UnityTest]
        public IEnumerator TheSuppressionHidesTheDRAWING_NotTheOffer()
        {
            // The distinction matters: the offer channel stays true (the affordance lane may still want
            // it), and only this surface stands down.
            Face(Vector2.right);
            yield return null; yield return null;

            EnterMode(ControlMode.Aboard);
            yield return null; yield return null;

            Assert.IsFalse(_popup.IsShowing);
            Assert.IsTrue(_popup.Offer.Has, "the offer is still standing; only the popup stood down");
        }

        // =====================================================================================
        //  THE DEGRADED MODE — no probe means no facing, and it must FAIL OPEN
        // =====================================================================================

        [UnityTest]
        public IEnumerator WithNobodyPublishingAnActor_ItFallsBackToProximityAlone()
        {
            // A region scene played directly has no persistent rig, so nothing writes the probe. The popup
            // must still work — the old behaviour — rather than going silent for want of a facing.
            InteractActorProbe.Reset();
            yield return null; yield return null;

            Assert.IsTrue(_popup.IsShowing, "unknown facing passes everything, as the contract says");
            Assert.IsTrue(_popup.Offer.Has);
        }

        [UnityTest]
        public IEnumerator ASwitchedOffNeighbourIsNotOffered()
        {
            // A villager whose routine took her inside a house switches her Interactable off with her
            // renderer. You cannot be offered a conversation through a wall.
            Face(Vector2.right);
            yield return null; yield return null;
            Assert.AreEqual(WorldStrings.OfferLabel(InteractKind.Talk, WorldStrings.GinnyName),
                            _popup.ShownLabel);

            _ginny.enabled = false;
            yield return null; yield return null;

            Assert.IsFalse(_popup.IsShowing, "she is not there, so there is nothing to offer");
        }

        // =====================================================================================
        //  TEARDOWN IS NOT A LEAK
        // =====================================================================================

        [UnityTest]
        public IEnumerator ATornDownInteractorReleasesItsOffer()
        {
            Face(Vector2.right);
            yield return null; yield return null;
            Assert.IsTrue(_popup.IsShowing);

            _interactor.enabled = false;         // OnDisable: the region going away
            yield return null; yield return null;

            Assert.IsFalse(_popup.IsShowing,
                           "a dead reader must not leave the popup naming somebody who is gone");
            Assert.IsFalse(InteractActionClaim.IsClaimed, "and it hands the interact key back");
        }

        [UnityTest]
        public IEnumerator ThePopupIsBuiltOnce_AndSitsWhereTheLayoutSaysItDoes()
        {
            yield return null;
            Assert.IsNotNull(_popup.Panel, "the panel is built in Awake, not on first offer");
            Assert.AreEqual(InteractPopupLayout.PanelAnchoredPosition(), _popup.Panel.anchoredPosition);
            Assert.AreEqual(InteractPopupLayout.PanelSize(), _popup.Panel.sizeDelta);
            Assert.IsNotNull(_popup.CanvasObject);
        }
    }
}
