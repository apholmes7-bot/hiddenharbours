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
    /// <b>THE PEOPLE IN A REGION YOU SAILED TO WILL TALK TO YOU.</b> The whole chain, driven rather than
    /// reasoned about: a real <see cref="RegionPassage"/> in the region you leave, a real
    /// <see cref="RegionSceneLoader"/> switching a real active scene, a real
    /// <see cref="RegionTravelCoordinator"/> reacting to it, and a real <see cref="WorldInteractor"/> +
    /// <see cref="DialoguePresenter"/> waiting in the region you arrive in with two people standing in
    /// front of them.
    ///
    /// <para><b>The defect this pins.</b> Nine Mile Creek's interactor and dialogue panel were built
    /// INSIDE the region's dev core, because an interactor needs a player to measure proximity from and
    /// the dev stand-in is the only player a region scene can name at build time. But
    /// <c>DevRegionBootstrap</c> DESTROYS that whole core the moment a real core travels in — so the
    /// driver, the panel and the player reference all went with it, and Wendell and Hector were mute,
    /// promptless and unspeakable-to for every player who actually arrived by sea, while working
    /// perfectly for the owner pressing Play in the scene. Two halves had to change and both are pinned
    /// here: the driver now lives on region ROOTS (it survives the arrival), and it resolves its player
    /// from Core when the stand-in it was wired to is gone.</para>
    ///
    /// <para><b>Sibling of <c>InteriorTravelPlayTests</c>, deliberately not merged with it.</b> That one
    /// pins the same Core relay carrying a room's occupant; this one pins a component that was not there
    /// at all after the hop. Same seam, different failure, and a rig that proves one says nothing about
    /// the other.</para>
    ///
    /// <para><b>Two loaded scenes, not one</b> — the <c>PerPassageArrivalPlayTests</c> precedent. The
    /// coordinator DEACTIVATES the roots of the scene it leaves, so a test that travelled out of the
    /// runner's own scene would switch the runner off mid-assert. Both ends of the hop are scenes this
    /// test creates and owns, and teardown unloads BOTH: a region left resident reds other files' tests
    /// with failures that read like gameplay bugs.</para>
    ///
    /// <para><b>State, never pixels — and no keyboard.</b> Assertions read
    /// <see cref="WorldInteractor.Nearest"/>, <see cref="DialoguePresenter.IsShowing"/> and the Core
    /// <see cref="InteractionGate"/>/<see cref="InteractActionClaim"/> statics. The press is delivered
    /// through <see cref="WorldInteractor.BeginInteract"/>, which is what <c>Update</c> itself calls on
    /// the key edge — the <c>InteractVerbPlayTests</c> precedent, and the reason nothing here has to be
    /// <c>Assert.Ignore</c>d when an unfocused batchmode runner declines to deliver a virtual key.</para>
    /// </summary>
    public class RegionDialogueTravelPlayTests
    {
        private const string SourceSceneName = "RegionDialogue_Source";
        private const string DestSceneName = "RegionDialogue_Dest";

        // The region you arrive in: you land on the wharf, and the buyer is up the yard from there.
        private static readonly Vector3 ArrivalBerth = new Vector3(5f, 5f, 0f);
        private static readonly Vector3 DisembarkDeck = new Vector3(10f, 10f, 0f);
        private static readonly Vector3 BuyerSpot = new Vector3(40f, 40f, 0f);

        private const float Reach = 1.8f;              // WorldInteractor's own authored default

        private Scene _origin;
        private Scene _source;
        private Scene _dest;
        private RegionDef _destRegion;
        private NpcDef _npc;
        private DialogueDef _dialogue;
        private GameObject _coordinatorGo;
        private GameObject _player;
        private GameObject _boat;
        private GameObject _devCore;
        private WorldInteractor _interactor;
        private DialoguePresenter _presenter;
        private Interactable _buyer;

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();
            InteractionGate.Reset();
            InteractActionClaim.Reset();
            InteractOffer.Reset();
            InteractActorProbe.Reset();

            _origin = SceneManager.GetActiveScene();
            _source = SceneManager.CreateScene(SourceSceneName);
            _dest = SceneManager.CreateScene(DestSceneName);

            _destRegion = ScriptableObject.CreateInstance<RegionDef>();
            _destRegion.Id = "region.dialogue_travel_dest";
            _destRegion.SceneName = DestSceneName;

            // The persistent rig lives OUTSIDE both region scenes, exactly as the real one does (it is
            // DontDestroyOnLoad), so the coordinator's root-toggling never touches it.
            _player = new GameObject("PersistentPlayer");
            _boat = new GameObject("PersistentBoat");

            BuildDestinationRegion();

            // Stand in the SOURCE region before the coordinator starts listening, so its first event is
            // the crossing under test rather than our own setup.
            SceneManager.SetActiveScene(_source);

            _coordinatorGo = new GameObject("RegionTravelCoordinator");
            _coordinatorGo.SetActive(false);
            // ⚠️ OUTSIDE both region scenes, with the rest of the persistent rig — the real coordinator
            // is a DontDestroyOnLoad root, so the root-toggling it performs can never reach itself.
            SceneManager.MoveGameObjectToScene(_coordinatorGo, _origin);
            var coordinator = _coordinatorGo.AddComponent<RegionTravelCoordinator>();
            coordinator.Configure(_player.transform, _boat.transform, null, null);
            _coordinatorGo.SetActive(true);               // OnEnable subscribes AND publishes the player
        }

        /// <summary>
        /// The arrived-in region, in Nine Mile Creek's shape: an anchor to land at, a person standing in
        /// the yard, the dialogue panel and the interact driver — and the DEV STAND-IN that the region's
        /// builder had to point that driver at, inside the dev core that travel destroys.
        /// </summary>
        private void BuildDestinationRegion()
        {
            var anchorGo = new GameObject("DestRegionAnchor");
            anchorGo.SetActive(false);                    // configure BEFORE enable, like the builder
            SceneManager.MoveGameObjectToScene(anchorGo, _dest);
            var anchor = anchorGo.AddComponent<RegionAnchor>();
            anchor.Configure(_destRegion.Id,
                             ChildAt(anchorGo, "Arrival", ArrivalBerth),
                             ChildAt(anchorGo, "Dock", new Vector3(9f, 9f, 0f)),
                             ChildAt(anchorGo, "Disembark", DisembarkDeck));
            anchorGo.SetActive(true);

            // The dev core, holding the stand-in player — the region scene's own review affordance, and
            // the only transform its builder could name. Baked INACTIVE, as the real one is.
            _devCore = new GameObject("DevCore");
            SceneManager.MoveGameObjectToScene(_devCore, _dest);
            var devPlayer = new GameObject("DevPlayer");
            devPlayer.transform.SetParent(_devCore.transform, worldPositionStays: false);
            devPlayer.transform.position = DisembarkDeck;
            _devCore.SetActive(false);

            _buyer = StandPerson("WendellArsenault", BuyerSpot);
            StandDialogueDriver(devPlayer.transform);
        }

        /// <summary>Somebody with words, wired the data-driven way the region builder wires one: an
        /// <see cref="NpcDef"/> carrying a <see cref="DialogueDef"/>, not a hard-coded string id.</summary>
        private Interactable StandPerson(string name, Vector3 where)
        {
            _dialogue = ScriptableObject.CreateInstance<DialogueDef>();
            _dialogue.Id = "dialogue.travel_test";
            _dialogue.FirstLines = new[] { "Bring her alongside.", "I'll weigh what you've got." };

            _npc = ScriptableObject.CreateInstance<NpcDef>();
            _npc.Id = "npc.travel_test";
            _npc.DisplayName = "Wendell Arsenault";
            _npc.Kind = InteractKind.Talk;
            _npc.Dialogue = _dialogue;
            _npc.CompletionFlag = "met_travel_test";

            var peopleRoot = new GameObject("CreekPeople");
            SceneManager.MoveGameObjectToScene(peopleRoot, _dest);

            var go = new GameObject(name);
            go.transform.SetParent(peopleRoot.transform, worldPositionStays: true);
            go.transform.position = where;
            var interactable = go.AddComponent<Interactable>();
            interactable.Configure(_npc);
            return interactable;
        }

        /// <summary>
        /// The panel and the proximity driver, as <c>NineMileCreekBuilder.PlaceDialogueDriver</c> now
        /// builds them: SCENE ROOTS, wired to the region's people and to the review stand-in.
        ///
        /// <para>⚠️ The parenting IS the fix, so it is reproduced here rather than assumed: these were
        /// children of the dev core, and that is why they did not exist after a hop. That the BUILDER
        /// puts them out here is pinned separately and statically, in
        /// <c>NineMileCreekDialogueDriverTests</c> — this rig could happily assert a shape the builder
        /// had stopped producing.</para>
        /// </summary>
        private void StandDialogueDriver(Transform reviewPlayer)
        {
            var panelGo = new GameObject("DialogueUI");
            panelGo.SetActive(false);                     // configure BEFORE enable, like the builder
            SceneManager.MoveGameObjectToScene(panelGo, _dest);
            _presenter = panelGo.AddComponent<DialoguePresenter>();
            panelGo.SetActive(true);

            var driverGo = new GameObject("WorldInteractor");
            driverGo.SetActive(false);
            SceneManager.MoveGameObjectToScene(driverGo, _dest);
            _interactor = driverGo.AddComponent<WorldInteractor>();
            _interactor.Configure(reviewPlayer, _presenter, new[] { _buyer }, Reach);
            driverGo.SetActive(true);
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
            if (_npc != null) Object.DestroyImmediate(_npc);
            if (_dialogue != null) Object.DestroyImmediate(_dialogue);

            // ⚠️ Both regions MUST come out. A region left loaded stays resident for the whole run and
            // reds unrelated files with failures that read like gameplay bugs.
            if (_origin.IsValid() && _origin.isLoaded) SceneManager.SetActiveScene(_origin);
            if (_dest.IsValid() && _dest.isLoaded) yield return SceneManager.UnloadSceneAsync(_dest);
            if (_source.IsValid() && _source.isLoaded) yield return SceneManager.UnloadSceneAsync(_source);

            InteractionGate.Reset();
            InteractActionClaim.Reset();
            InteractOffer.Reset();
            InteractActorProbe.Reset();
            GameServices.Reset();
        }

        private static Transform ChildAt(GameObject parent, string name, Vector3 position)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, worldPositionStays: false);
            go.transform.position = position;
            return go.transform;
        }

        /// <summary>A passage in the source scene, wired to cross into the destination region.</summary>
        private RegionPassage MakePassage()
        {
            var loaderGo = new GameObject("RegionSceneLoader");
            loaderGo.SetActive(false);                    // so Awake reads the SOURCE scene's name…
            SceneManager.MoveGameObjectToScene(loaderGo, _source);
            var loader = loaderGo.AddComponent<RegionSceneLoader>();
            loaderGo.SetActive(true);                     // …not the test runner's

            var passageGo = new GameObject("Passage");
            SceneManager.MoveGameObjectToScene(passageGo, _source);
            var passage = passageGo.AddComponent<RegionPassage>();
            passage.Configure(_destRegion, loader, "");
            return passage;
        }

        /// <summary>Cross into the destination region, then let the region's dev core die exactly as
        /// <c>DevRegionBootstrap.Start</c> kills it when a persistent core travels in.</summary>
        private IEnumerator TravelInAndLoseTheDevCore()
        {
            MakePassage().Activate();
            yield return null;

            Assert.AreEqual(DestSceneName, SceneManager.GetActiveScene().name,
                "precondition: the crossing actually happened");

            Object.DestroyImmediate(_devCore);            // DevRegionBootstrap.cs: Destroy(_devCoreRoot)
            yield return null;
        }

        /// <summary>Put the player somewhere and let one Update see them there. Two frames, because the
        /// ordering of this coroutine's resume against <c>WorldInteractor.Update</c> inside a single
        /// frame is not guaranteed — this is flake-avoidance, not a measurement.</summary>
        private IEnumerator WalkTo(Vector3 worldPos)
        {
            _player.transform.position = worldPos;
            yield return null;
            yield return null;
        }

        // =============================================================================================

        [UnityTest]
        public IEnumerator ThePeopleOfATravelledToRegion_SpeakToThePlayerWhoArrived()
        {
            // ⭐ THE SPINE. Sail in, walk up to the buyer, and he must have something to say to the
            // fisher who actually turned up — not to the stand-in the scene was built pointing at.
            yield return TravelInAndLoseTheDevCore();

            Assert.IsNull(_interactor.Nearest,
                "precondition: you land on the wharf, out of everyone's reach");
            Assert.IsFalse(InteractActionClaim.IsClaimed);

            yield return WalkTo(BuyerSpot);
            Assert.AreSame(_buyer, _interactor.Nearest,
                "THE DEFECT: the creek's people were mute for anyone who sailed in. The driver that " +
                "offers the prompt was built inside the dev core and destroyed with it on arrival, and " +
                "once it survived that, the player it measured from was the stand-in that died there");
            Assert.IsTrue(InteractActionClaim.IsClaimed,
                "…and standing in front of somebody claims the interact press, so it cannot also board " +
                "a boat you happen to be standing beside");

            Assert.IsTrue(_interactor.BeginInteract(), "the press was spent on the conversation");
            Assert.IsTrue(_presenter.IsShowing, "the panel is up and he is talking");
            Assert.IsTrue(InteractionGate.IsBlocked,
                "a conversation that is up owns the interact key outright (the Core gate the " +
                "ControlSwitcher honours)");

            // Second line, then the close — a conversation is a thing you walk out of, not a thing that
            // latches the world into a dialogue that never ends.
            //
            // ⚠️ CHOREOGRAPHY UPDATED for the anchored bubble (#557): a line FILLS a letter at a time
            // now, and an impatient press completes the fill rather than skipping the line — the
            // Animal Crossing read, pinned by DialogueTypewriterTests. Back-to-back presses with no
            // frames between them therefore spend one press per line on the fill. A real player whose
            // line has finished filling still advances in one press; only the impatient path pays two.
            Assert.IsTrue(_interactor.BeginInteract(), "the press mid-fill completes the first line");
            Assert.IsTrue(_presenter.IsShowing);
            Assert.IsTrue(_interactor.BeginInteract(), "the next press advances him");
            Assert.IsTrue(_presenter.IsShowing);
            Assert.IsTrue(_interactor.BeginInteract(), "…completes the second line's fill");
            Assert.IsTrue(_presenter.IsShowing);
            Assert.IsTrue(_interactor.BeginInteract(), "…and the last one closes the panel");
            Assert.IsFalse(_presenter.IsShowing);
            Assert.IsFalse(InteractionGate.IsBlocked, "the gate is released with it");

            yield return WalkTo(DisembarkDeck);
            Assert.IsNull(_interactor.Nearest, "walking back down the yard drops the prompt");
            Assert.IsFalse(InteractActionClaim.IsClaimed, "…and releases the claim on the press");
        }

        [UnityTest]
        public IEnumerator TheDriverAndItsPanel_OutliveTheDevCoreThatUsedToOwnThem()
        {
            // The structural half of the defect, stated as its own claim: these two were CHILDREN of the
            // dev core, so a live arrival did not leave a driver whose player was stale — it left no
            // driver at all, and no panel for one to talk through.
            yield return TravelInAndLoseTheDevCore();

            Assert.IsTrue(_interactor != null,
                "the interact driver must outlive the dev core — it is region content, not review kit");
            Assert.IsTrue(_presenter != null, "and so must the panel it speaks through");
            Assert.AreEqual(DestSceneName, _interactor.gameObject.scene.name,
                "it belongs to the REGION, so it is toggled with the region like every other root");
        }

        [UnityTest]
        public IEnumerator WithNobodyInReach_ThePressIsNotSpent()
        {
            // The other half of "context-aware by proximity": away from people, E is not this
            // component's press to take, and the ControlSwitcher's board/step-ashore must still get it.
            yield return TravelInAndLoseTheDevCore();

            yield return WalkTo(ArrivalBerth);
            Assert.IsFalse(_interactor.BeginInteract(),
                "a press with nobody in front of you belongs to whoever else wants it");
            Assert.IsFalse(_presenter.IsShowing);
            Assert.IsFalse(InteractionGate.IsBlocked);
        }

        [UnityTest]
        public IEnumerator JustOutsideReach_IsStillOutside()
        {
            // The edge, so the fallback cannot quietly widen it: resolving the player from Core must
            // change WHO is measured, never HOW FAR.
            yield return TravelInAndLoseTheDevCore();

            yield return WalkTo(BuyerSpot + new Vector3(Reach + 0.2f, 0f, 0f));
            Assert.IsNull(_interactor.Nearest, "just beyond his reach is out of reach");

            yield return WalkTo(BuyerSpot + new Vector3(Reach - 0.2f, 0f, 0f));
            Assert.AreSame(_buyer, _interactor.Nearest, "…and just inside it is in");
        }

        [UnityTest]
        public IEnumerator ALiveSerializedPlayer_StillWins()
        {
            // The regression guard on the path that already worked. Press Play in the region scene
            // directly and the dev core lives: its stand-in is the only player there is, nothing
            // publishes the Core relay at all, and the owner's review session must be untouched by this.
            // The relay overriding a live serialized reference would break exactly that.
            yield return TravelInAndLoseTheDevCore();   // …but re-wire to a LIVE stand-in afterwards

            var standIn = new GameObject("LiveStandIn");
            try
            {
                standIn.transform.position = DisembarkDeck;
                _interactor.Configure(standIn.transform, _presenter, new[] { _buyer }, Reach);

                yield return WalkTo(BuyerSpot);         // the PERSISTENT player walks up to him
                Assert.IsNull(_interactor.Nearest,
                    "the live serialized player is who this driver measures from — the persistent " +
                    "player walking up must not open a prompt on a driver pointed at somebody else");

                standIn.transform.position = BuyerSpot; // now the one it IS watching walks up
                yield return null;
                yield return null;
                Assert.AreSame(_buyer, _interactor.Nearest, "…and it offers the prompt for them");
                Assert.IsTrue(_interactor.BeginInteract());
                Assert.IsTrue(_presenter.IsShowing);
            }
            finally { Object.DestroyImmediate(standIn); }
        }

        [UnityTest]
        public IEnumerator TheTravelRig_PublishesThePlayerOnTheCoreRelay()
        {
            // The seam itself, named. World cannot reference App, so the persistent player reaches a
            // region's interact driver through Core or not at all.
            Assert.AreSame(_player.transform, GameServices.PlayerTransform,
                "the coordinator publishes the persistent player on enable");

            yield return TravelInAndLoseTheDevCore();

            Assert.AreSame(_player.transform, GameServices.PlayerTransform, "and a hop does not disturb it");
        }
    }
}
