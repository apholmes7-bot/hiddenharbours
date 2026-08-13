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
    /// <b>An interior in a region you SAILED TO opens.</b> The whole chain, driven rather than reasoned
    /// about: a real <see cref="RegionPassage"/> in the region you leave, a real
    /// <see cref="RegionSceneLoader"/> switching a real active scene, a real
    /// <see cref="RegionTravelCoordinator"/> reacting to it, and a real
    /// <see cref="BuildingInterior"/> waiting in the region you arrive in.
    ///
    /// <para><b>The defect this pins.</b> <c>BuildingInterior._occupant</c> is a serialized
    /// <c>Transform</c>, and only a scene BUILDER ever set it. That works in the start scene, where the
    /// builder stands the persistent core up in the same scene and can name the real player. It cannot
    /// work anywhere else: a region scene is saved long before that player exists, and Unity does not
    /// serialize references across scenes at all — so a region's rooms were wired either to nothing or
    /// to the dev stand-in that <c>DevRegionBootstrap</c> DESTROYS on the travel path. Either way
    /// <c>Update</c> returned on its first line forever, and no door in Nine Mile Creek — or in any
    /// region added after it — ever opened for a player who arrived by sea. Canon since 2026-08-11 is
    /// that every building is enterable; before this, that was only true in the scene you booted into.</para>
    ///
    /// <para><b>Two loaded scenes, not one</b> — the <c>PerPassageArrivalPlayTests</c> precedent. The
    /// coordinator DEACTIVATES the roots of the scene it leaves, so a test that travelled out of the
    /// test runner's own scene would switch the runner off mid-assert. Both ends of the hop are scenes
    /// this test creates and owns, and teardown unloads both: a region left resident reds OTHER files'
    /// tests with failures that read like tide bugs.</para>
    ///
    /// <para><b>State, never pixels.</b> Every assertion here reads <see cref="BuildingInterior.IsInside"/>
    /// and renderer <c>enabled</c> flags. A PlayMode test that called <c>Camera.Render</c>/<c>ReadPixels</c>
    /// would kill the CI editor outright (Null GfxDevice — exit 1, no results XML).</para>
    ///
    /// <para><b>No frame-count-as-time assertions.</b> The <c>yield return null</c>s exist to let a scene
    /// event flush and to let <c>Update</c> run once on the new position — never to measure anything.
    /// Headless frames are nowhere near real ones.</para>
    /// </summary>
    public class InteriorTravelPlayTests
    {
        private const string SourceSceneName = "InteriorTravel_Source";
        private const string DestSceneName = "InteriorTravel_Dest";

        // The region you arrive in: you land on the wharf, and the shop is a walk away.
        private static readonly Vector3 ArrivalBerth = new Vector3(5f, 5f, 0f);
        private static readonly Vector3 DisembarkDeck = new Vector3(10f, 10f, 0f);
        private static readonly Vector3 ShopGround = new Vector3(40f, 40f, 0f);

        // The room, in the units the bake's own contract reports (ground metres, unsquashed).
        private const float FloorWidth = 6f;
        private const float FloorLength = 8f;
        private const float WallThickness = 0.3f;
        private const float DoorwayWidth = 1.4f;
        private const int Facing = 0;
        private const int Facings = 8;
        private const float GroundDepthScale = 0.6427876f;   // sin 40° at the shared bake camera

        private Scene _origin;
        private Scene _source;
        private Scene _dest;
        private RegionDef _destRegion;
        private GameObject _coordinatorGo;
        private GameObject _player;
        private GameObject _boat;
        private GameObject _devCore;
        private BuildingInterior _interior;
        private SpriteRenderer _shell;
        private SpriteRenderer _room;

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();

            _origin = SceneManager.GetActiveScene();
            _source = SceneManager.CreateScene(SourceSceneName);
            _dest = SceneManager.CreateScene(DestSceneName);

            _destRegion = ScriptableObject.CreateInstance<RegionDef>();
            _destRegion.Id = "region.interior_travel_dest";
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
            // ⚠️ OUTSIDE both region scenes, with the rest of the persistent rig. The real coordinator
            // is a DontDestroyOnLoad root, so the root-toggling it performs on a hop can never reach
            // itself. Left in the source scene it deactivates ITSELF mid-crossing — which is a fine way
            // to discover that a lifetime hook is wrong, but not a thing that can happen in the game.
            SceneManager.MoveGameObjectToScene(_coordinatorGo, _origin);
            var coordinator = _coordinatorGo.AddComponent<RegionTravelCoordinator>();
            coordinator.Configure(_player.transform, _boat.transform, null, null);
            _coordinatorGo.SetActive(true);               // OnEnable subscribes AND publishes the player
        }

        /// <summary>
        /// The arrived-in region: an anchor to land at, a shop with a baked room in it, and the DEV
        /// STAND-IN the region's builder had to point that shop at. This is Nine Mile Creek's shape —
        /// <c>NineMileCreekBuilder</c> hands <c>NineMileCreekShops.Place</c> the transform returned by
        /// its dev bootstrap, because the real player does not exist when the scene is authored.
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
            // the only transform its builder could name. Baked INACTIVE, as the real one is: on the
            // travel path it is destroyed having never awakened.
            _devCore = new GameObject("DevCore");
            SceneManager.MoveGameObjectToScene(_devCore, _dest);
            var devPlayer = new GameObject("DevPlayer");
            devPlayer.transform.SetParent(_devCore.transform, worldPositionStays: false);
            devPlayer.transform.position = DisembarkDeck;
            _devCore.SetActive(false);

            _interior = StandShop(devPlayer.transform);
        }

        /// <summary>A shop with a shell, a baked room and a <see cref="BuildingInterior"/> over both —
        /// wired the way <c>ShopPlacement.StandInterior</c> wires one.</summary>
        private BuildingInterior StandShop(Transform occupant)
        {
            var shopGo = new GameObject("Shop");
            shopGo.SetActive(false);                      // configure BEFORE enable, like the builder
            SceneManager.MoveGameObjectToScene(shopGo, _dest);
            shopGo.transform.position = ShopGround;

            var shellGo = new GameObject("Shell");
            shellGo.transform.SetParent(shopGo.transform, worldPositionStays: false);
            _shell = shellGo.AddComponent<SpriteRenderer>();

            var roomGo = new GameObject("Room");
            roomGo.transform.SetParent(shopGo.transform, worldPositionStays: false);
            _room = roomGo.AddComponent<SpriteRenderer>();

            var interior = shopGo.AddComponent<BuildingInterior>();
            interior.Configure(_shell, _room, props: null,
                               FloorWidth, FloorLength, Facing, Facings,
                               GroundDepthScale, WallThickness, DoorwayWidth);
            interior.SetOccupant(occupant);
            shopGo.SetActive(true);
            return interior;
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

            // ⚠️ Both regions MUST come out. A region left loaded stays resident for the whole run and
            // reds unrelated files with failures that read like gameplay bugs.
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
        /// ordering of this coroutine's resume against <c>BuildingInterior.Update</c> inside a single
        /// frame is not guaranteed — this is flake-avoidance, not a measurement.</summary>
        private IEnumerator WalkTo(Vector3 worldPos)
        {
            _player.transform.position = worldPos;
            yield return null;
            yield return null;
        }

        // =============================================================================================

        [UnityTest]
        public IEnumerator AnInteriorInATravelledToRegion_OpensForThePersistentPlayer()
        {
            // ⭐ THE SPINE. Sail in, walk to the shop, and the roof must come off for the player who
            // actually arrived — not for the stand-in the scene was built pointing at.
            yield return TravelInAndLoseTheDevCore();

            Assert.IsFalse(_interior.IsInside,
                "precondition: you land on the wharf, outside");

            // The threshold itself is NOT yet inside — Contains tests the INNER rectangle, so you are
            // inside once you are PAST the wall, which is the same moment the doorway lets you through.
            yield return WalkTo(_interior.DoorWorld);
            Assert.IsFalse(_interior.IsInside,
                "standing in the doorway is not yet standing inside — the inside test is the inner " +
                "rectangle, and a room that opened ON the threshold would strobe as you walked past it");

            yield return WalkTo(ShopGround);
            Assert.IsTrue(_interior.IsInside,
                "THE DEFECT: a room in a region you sailed to never opened. Its occupant was a " +
                "serialized reference to a dev stand-in that the travel path destroys, and nothing in " +
                "the repo ever re-bound it, so Update returned on its first line and the roof stayed on");
            Assert.IsFalse(_shell.enabled, "the shell yields to the room the player walked into");
            Assert.IsTrue(_room.enabled, "…and the room it was hiding is what draws instead");

            // And back out again: the swap is not a one-way trip.
            yield return WalkTo(DisembarkDeck);
            Assert.IsFalse(_interior.IsInside, "walking out puts the shell back");
            Assert.IsTrue(_shell.enabled);
            Assert.IsFalse(_room.enabled);
        }

        [UnityTest]
        public IEnumerator AnInteriorWiredToNobody_StillOpensAfterTravel()
        {
            // The other real shape, and the more general statement of the defect: a region scene with no
            // dev stand-in to name has interiors wired to NOTHING at all. Same fix, same seam.
            _interior.SetOccupant(null);
            yield return TravelInAndLoseTheDevCore();

            yield return WalkTo(ShopGround);
            Assert.IsTrue(_interior.IsInside,
                "an interior that was never wired to anyone must still find the player — a region " +
                "authored without a dev core is otherwise permanently sealed");
            Assert.IsTrue(_room.enabled);
        }

        [UnityTest]
        public IEnumerator ALiveSerializedOccupant_StillWins()
        {
            // The regression guard on the paths that already worked. The START scene's interiors name
            // the real persistent player, and a region played DIRECTLY for review names its own dev
            // stand-in; in both the serialized reference is alive and must remain the answer. If the
            // Core relay overrode it, reviewing a region scene on its own would stop working — and the
            // start scene's behaviour would no longer be the byte-equivalent it must stay.
            yield return TravelInAndLoseTheDevCore();   // ...but re-wire to a LIVE stand-in afterwards

            var standIn = new GameObject("LiveStandIn");
            try
            {
                standIn.transform.position = DisembarkDeck;
                _interior.SetOccupant(standIn.transform);

                yield return WalkTo(ShopGround);        // the PERSISTENT player walks in
                Assert.IsFalse(_interior.IsInside,
                    "the live serialized occupant is who this room watches — the persistent player " +
                    "walking in must not open a room that was explicitly pointed at somebody else");

                standIn.transform.position = ShopGround; // now the one it IS watching steps inside
                yield return null;
                yield return null;
                Assert.IsTrue(_interior.IsInside,
                    "…and it opens for them");
            }
            finally { Object.DestroyImmediate(standIn); }
        }

        [UnityTest]
        public IEnumerator TheTravelRig_PublishesThePlayerOnTheCoreRelay()
        {
            // The seam itself, named. World cannot reference App, so the persistent player reaches
            // region content through Core or not at all.
            Assert.AreSame(_player.transform, GameServices.PlayerTransform,
                "the coordinator publishes the persistent player on enable — which covers boot, every " +
                "crossing and every return trip, because the player it carries is the same transform " +
                "throughout and there is nothing for an arrival event to re-bind");

            yield return TravelInAndLoseTheDevCore();

            Assert.AreSame(_player.transform, GameServices.PlayerTransform,
                "and a hop does not disturb it");
        }

        [UnityTest]
        public IEnumerator ADisabledCoordinator_KeepsTheRelay_ADestroyedOneTakesItBack()
        {
            // The lifetime hook, pinned — and it is pinned because getting it wrong SEALED EVERY DOOR.
            // The registration was originally taken back in OnDisable, which reads as tidy and is not:
            // being disabled is not being gone, and this component is a root object like any other, so
            // anything that toggles roots switches it off. An interior treats a null relay as "there is
            // no player", so a momentarily-disabled coordinator silently closed every building in the
            // game. Destroyed is the only state that actually means the registration is over.
            _coordinatorGo.SetActive(false);
            yield return null;
            Assert.AreSame(_player.transform, GameServices.PlayerTransform,
                "a DISABLED coordinator still owns a player that plainly still exists");

            Object.DestroyImmediate(_coordinatorGo);
            Assert.IsNull(GameServices.PlayerTransform,
                "…but a DESTROYED one takes its registration back, so a torn-down core cannot leave a " +
                "dead player standing on the relay for whatever boots next");
        }
    }
}
