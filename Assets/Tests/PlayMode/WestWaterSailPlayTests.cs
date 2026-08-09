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
    /// ⭐ <b>THE SAIL</b> — the west water's rig claims, driven rather than reasoned about: a boat leaves
    /// a region, crosses a real passage, and has to be in the right scene at the right point when the
    /// world settles.
    ///
    /// <para><b>What is new here, on top of <c>PerPassageArrivalPlayTests</c>.</b> That test proved the
    /// arrival key survives a scene switch into a region with a WHARF — a default arrival, a dock zone
    /// and a disembark point. The west water is the first region with none of those: it is open water, it
    /// has two doors of equal standing rather than one front door and a side entrance, and there is
    /// nothing anywhere in it to step ashore onto. Every one of those is a null the arrival path has
    /// never been handed before, and a null that arrives quietly is how a fisher ends up standing on the
    /// sea bed 700 m from their boat.</para>
    ///
    /// <para><b>And the registration, which EditMode cannot claim at all.</b> A region's height source
    /// installs itself into <c>GameServices.TidalTerrain</c> in <c>OnEnable</c> — and EditMode never
    /// enables anything, so "the sounder reads a real bottom out here" (world-map-plan §2.2's ruling) is
    /// only ever true if something drives it. That is this file's other half.</para>
    ///
    /// <para><b>The fixtures below are a MODEL of the west water's shape, not its numbers.</b> A PlayMode
    /// assembly cannot reference the editor assembly the region's plan lives in, so the metres here are
    /// deliberately arbitrary and prove nothing about the real region — the actual extent, bed and seam
    /// elevations are asserted against <c>WestWaterPlan</c> in <c>WestWaterTests</c>, where they belong.
    /// What this file proves is the RIG: that a dockless two-door water region behaves.</para>
    ///
    /// <para><b>Two loaded scenes, not one</b> — the coordinator deactivates the roots of the scene it
    /// leaves, so a test travelling out of the runner's own scene would switch the runner off mid-assert.
    /// <b>No frame-count-as-time assertions</b> — the single <c>yield return null</c> lets the scene event
    /// flush, it does not measure anything.</para>
    /// </summary>
    public class WestWaterSailPlayTests
    {
        private const string HarbourSceneName = "WestWaterSail_Harbour";
        private const string WaterSceneName = "WestWaterSail_Water";

        // The two doors, named the way the region names them. Equal standing: neither is a fallback.
        private const string FromEastKey = "st_peters";
        private const string FromWestKey = "nine_mile_creek";

        // Model geometry — see the class note. Arbitrary metres in the shape of a two-door run.
        private static readonly Vector3 EastEndArrival = new Vector3(316f, 0f, 0f);
        private static readonly Vector3 WestEndArrival = new Vector3(-316f, 0f, 0f);
        private static readonly Vector3 WhereTheFisherWasStanding = new Vector3(98f, 88f, 0f);
        private static readonly Vector2 RegionRectangle = new Vector2(760f, 520f);

        private Scene _origin;
        private Scene _harbour;
        private Scene _water;
        private RegionDef _waterRegion;
        private GameObject _coordinatorGo;
        private GameObject _player;
        private GameObject _boat;

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();

            _origin = SceneManager.GetActiveScene();
            _harbour = SceneManager.CreateScene(HarbourSceneName);
            _water = SceneManager.CreateScene(WaterSceneName);

            _waterRegion = ScriptableObject.CreateInstance<RegionDef>();
            _waterRegion.Id = "region.west_water_sail_fixture";
            _waterRegion.SceneName = WaterSceneName;

            // The persistent rig lives OUTSIDE both region scenes, exactly as the real one does (it is
            // DontDestroyOnLoad), so the coordinator's root-toggling never touches it.
            _player = new GameObject("PersistentPlayer");
            _player.transform.position = WhereTheFisherWasStanding;
            _boat = new GameObject("PersistentBoat");

            // ⭐ THE WATER REGION'S ANCHOR, authored the way WaterSceneTemplate authors one: a default
            // arrival, both doors named — and NO DOCK AND NO DISEMBARK, because there is nothing out
            // there to step onto.
            var anchorGo = new GameObject("WaterRegionAnchor");
            anchorGo.SetActive(false);                    // configure BEFORE enable, like the builder
            SceneManager.MoveGameObjectToScene(anchorGo, _water);
            var anchor = anchorGo.AddComponent<RegionAnchor>();
            anchor.Configure(_waterRegion.Id,
                             ChildAt(anchorGo, "Arrival", WestEndArrival),
                             dockZone: null, disembarkPoint: null);
            anchor.ConfigureArrivals(
                new NamedArrival(FromEastKey, ChildAt(anchorGo, "FromEast", EastEndArrival), null),
                new NamedArrival(FromWestKey, ChildAt(anchorGo, "FromWest", WestEndArrival), null));
            anchor.ConfigureExtent(Vector2.zero, RegionRectangle);
            anchorGo.SetActive(true);

            // Stand in the HARBOUR before the coordinator starts listening, so its first event is the
            // crossing under test rather than our own setup.
            SceneManager.SetActiveScene(_harbour);

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
            if (_waterRegion != null) Object.DestroyImmediate(_waterRegion);

            if (_origin.IsValid() && _origin.isLoaded) SceneManager.SetActiveScene(_origin);
            if (_water.IsValid() && _water.isLoaded) yield return SceneManager.UnloadSceneAsync(_water);
            if (_harbour.IsValid() && _harbour.isLoaded) yield return SceneManager.UnloadSceneAsync(_harbour);

            GameServices.Reset();
        }

        private static Transform ChildAt(GameObject parent, string name, Vector3 position)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, worldPositionStays: false);
            go.transform.position = position;
            return go.transform;
        }

        private static void AssertAt(Vector3 expected, Vector3 actual, string because) =>
            Assert.That(Vector3.Distance(expected, actual), Is.LessThan(1e-3f),
                        $"{because}\n  expected {expected}, was {actual}");

        /// <summary>A passage in the harbour scene, wired to sail out into the water region.</summary>
        private RegionPassage MakeSeaDoor(string arrivalKey)
        {
            var loaderGo = new GameObject("RegionSceneLoader");
            loaderGo.SetActive(false);                    // so Awake reads the HARBOUR's scene name…
            SceneManager.MoveGameObjectToScene(loaderGo, _harbour);
            var loader = loaderGo.AddComponent<RegionSceneLoader>();
            loaderGo.SetActive(true);                     // …not the test runner's

            var passageGo = new GameObject("PassageToWestWater");
            SceneManager.MoveGameObjectToScene(passageGo, _harbour);
            var passage = passageGo.AddComponent<RegionPassage>();
            passage.Configure(_waterRegion, loader, arrivalKey);
            return passage;
        }

        // =============================================================================================
        //  the sail
        // =============================================================================================

        [UnityTest]
        public IEnumerator SailingOutFromTheWest_LandsAtTheWestEndOfTheRun()
        {
            // ⭐ THE FIRST SAIL, driven. Leave Nine Mile Creek heading east and the boat must be at the
            // MAINLAND end of the water with the whole run ahead of her — not at the island end, which is
            // where a dropped key would put her, 632 m away and pointing at the door she just came out of.
            MakeSeaDoor(FromWestKey).Activate();
            yield return null;

            Assert.AreEqual(WaterSceneName, SceneManager.GetActiveScene().name,
                "precondition: the crossing actually happened");
            AssertAt(WestEndArrival, _boat.transform.position,
                "the door you came out of decides where you turn up. A boat at the far end here means " +
                "the key was published, dropped, or never read");
        }

        [UnityTest]
        public IEnumerator SailingOutFromTheEast_LandsAtTheEastEndOfTheRun()
        {
            // The other door, and it is the point of naming BOTH: the west water is the first region
            // where neither entrance is the region's "real" one that the other falls back to. If the two
            // keys resolved to the same place, one of these two tests would still pass.
            MakeSeaDoor(FromEastKey).Activate();
            yield return null;

            Assert.AreEqual(WaterSceneName, SceneManager.GetActiveScene().name);
            AssertAt(EastEndArrival, _boat.transform.position,
                "arriving from St Peters lands at the island end of the run");
        }

        [UnityTest]
        public IEnumerator ArrivingAtSea_LeavesTheFisherWhereTheyWere()
        {
            // ⚠ THE DOCKLESS NULL, which no region before this one has handed the arrival path. A water
            // region authors no disembark point, so there is no landing to place the fisher at — and the
            // right behaviour is to leave them exactly where they are, aboard, rather than to teleport
            // them to the origin because a Transform came back null.
            MakeSeaDoor(FromWestKey).Activate();
            yield return null;

            AssertAt(WhereTheFisherWasStanding, _player.transform.position,
                "the fisher is aboard; a region with nothing to step onto must not move them, and must " +
                "certainly not drop them at (0,0)");
        }

        [UnityTest]
        public IEnumerator TheKeyIsSpentByTheCrossingItWasSetFor()
        {
            // A key that survives its own crossing steers the NEXT one — and out here the next one is the
            // sail back, which would then land at the end you just left.
            MakeSeaDoor(FromEastKey).Activate();
            yield return null;

            Assert.IsNull(GameServices.PendingArrivalKey,
                "the coordinator must take the key on arrival, whether or not it recognised it");
        }

        [UnityTest]
        public IEnumerator TheRegionPublishesItsBoundsSoTheCameraCanBeHeld()
        {
            // At 760 m across an unclamped camera sails clean off the region, and open water is where
            // that bites hardest: there is no coastline to tell you the view has left the world. The
            // anchor relays the authored rectangle through Core, which is what the persistent camera's
            // bounds clamp reads.
            MakeSeaDoor(FromWestKey).Activate();
            yield return null;

            Assert.AreEqual(WaterSceneName, SceneManager.GetActiveScene().name,
                "precondition: the crossing actually happened");
            Assert.AreEqual(RegionRectangle, GameServices.CurrentRegionBounds.size,
                "a water region must publish its rectangle or the camera has nothing to hold it inside");
        }

        // =============================================================================================
        //  the sounder reads a real bottom — the claim EditMode cannot make
        // =============================================================================================

        [UnityTest]
        public IEnumerator TheOpenWaterBedInstallsItselfAsTheRegionsTerrain()
        {
            // ⭐ world-map-plan §2.2, as a rig fact: offshore scenes carry NO painted seabed but FULL
            // height data, "because the depth sounder and the fish finder read real depth". They read it
            // through GameServices.TidalTerrain, which a region's height source fills in on ENABLE — so
            // in EditMode, where nothing enables, this is unassertable and therefore untested unless it
            // is tested here.
            Assert.IsNull(GameServices.TidalTerrain, "precondition: no region's bed is installed yet");

            var terrainGo = new GameObject("WestWaterBed");
            SceneManager.MoveGameObjectToScene(terrainGo, _water);
            var terrain = terrainGo.AddComponent<RectTidalTerrain>();
            // A model of the shape: a deep floor with one shoal on it. The numbers are arbitrary (see the
            // class note); what is under test is that the thing registers and answers.
            terrain.Configure(-6f, new[]
            {
                new RectTidalTerrain.LandZone(new Vector2(380f, 0f), new Vector2(60f, 260f), -4f, 160f),
            });
            yield return null;

            Assert.AreSame(terrain, GameServices.TidalTerrain,
                "the bed must install itself through Core on enable — nothing else tells the sounder " +
                "where the bottom is");
            Assert.AreEqual(-6f, GameServices.TidalTerrain.ElevationAt(new Vector2(-316f, 0f)), 1e-4f,
                "…and answer with real depth out in the middle of open water, where there is no paint " +
                "and never will be");
            Assert.AreEqual(-4f, GameServices.TidalTerrain.ElevationAt(new Vector2(356f, 0f)), 1e-4f,
                "…including up on the shoal at the far end");

            terrainGo.SetActive(false);
            yield return null;

            Assert.IsNull(GameServices.TidalTerrain,
                "and it relinquishes on the way out, so a torn-down region leaves open water behind " +
                "rather than a stale bottom the next region would read");

            Object.DestroyImmediate(terrainGo);
        }
    }
}
