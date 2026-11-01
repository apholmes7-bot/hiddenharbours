#if UNITY_EDITOR
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using HiddenHarbours.App;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using HiddenHarbours.Player;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>WestWaterSailPlayTests' two-scene rig, with the East Water's authored points and
    /// both named crossings. The persistent player's boat and control switcher survive the round trip.
    /// No bootstrap, opening or save writer is started by this fixture.</summary>
    public class EastWaterSailPlayTests
    {
        Scene _origin, _island, _water;
        GameObject _player, _boatGo, _rig;
        BoatController _boat;
        DevBoatInput _input;
        ControlSwitcher _switcher;
        RegionPassage _outward, _homeward;

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();
            _origin = SceneManager.GetActiveScene();
            _island = SceneManager.CreateScene("EastWaterSail_StPeters");
            _water = SceneManager.CreateScene("EastWaterSail_EastWater");
            var islandDef = AssetDatabase.LoadAssetAtPath<RegionDef>(WaterSceneTemplate.DataRegions + "/StPeters.asset");
            var waterDef = AssetDatabase.LoadAssetAtPath<RegionDef>(
                WaterSceneTemplate.RegionAssetPathFor(EastWaterPlan.RegionAssetName));
            AddAnchor(_island, islandDef.Id, StPetersBuilder.FromEastWaterArrivalKey, StPetersBuilder.FromEastWaterArrivalPos);
            AddAnchor(_water, waterDef.Id, EastWaterPlan.FromStPetersArrivalKey, EastWaterPlan.FromStPetersArrivalPos);
            _outward = AddDoor(_island, waterDef, EastWaterPlan.FromStPetersArrivalKey);
            _homeward = AddDoor(_water, islandDef, StPetersBuilder.FromEastWaterArrivalKey);
            // Fixture defs supply the loaded fixture scene names to the real loader, without editing assets.
            var outwardDef = ScriptableObject.Instantiate(waterDef);
            outwardDef.SceneName = _water.name;
            _outward.Configure(outwardDef, _outward.GetComponentInParent<RegionSceneLoader>(), EastWaterPlan.FromStPetersArrivalKey);
            var homewardDef = ScriptableObject.Instantiate(islandDef);
            homewardDef.SceneName = _island.name;
            _homeward.Configure(homewardDef, _homeward.GetComponentInParent<RegionSceneLoader>(), StPetersBuilder.FromEastWaterArrivalKey);
            _fixtureDefs = new[] { outwardDef, homewardDef };

            _player = new GameObject("PersistentPlayer");
            _player.transform.position = new Vector3(9f, 7f, 0f);
            _boatGo = new GameObject("PlayersBoat");
            _boat = _boatGo.AddComponent<BoatController>();
            _boat.SetHull(AssetDatabase.LoadAssetAtPath<BoatHullDef>("Assets/_Project/Data/Boats/Punt.asset"));
            _boatGo.GetComponent<CapsuleCollider2D>().size = new Vector2(1.7f, 4f);
            _input = _boatGo.AddComponent<DevBoatInput>();
            _rig = new GameObject("PersistentTravelRig");
            _rig.SetActive(false);
            _switcher = _rig.AddComponent<ControlSwitcher>();
            _switcher.Configure(null, _boat, _input, null, 8f, null);
            // Enter the existing helm transition; only the fixture skips walking to the station.
            typeof(ControlSwitcher).GetMethod("TakeHelm", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_switcher, null);
            SceneManager.SetActiveScene(_island);
            var coordinator = _rig.AddComponent<RegionTravelCoordinator>();
            coordinator.Configure(_player.transform, _boatGo.transform, _switcher, null);
            _rig.SetActive(true);
        }

        RegionDef[] _fixtureDefs;

        static void AddAnchor(Scene scene, string regionId, string key, Vector3 position)
        {
            var go = new GameObject("RegionAnchor");
            go.SetActive(false);
            SceneManager.MoveGameObjectToScene(go, scene);
            var arrival = new GameObject("NamedArrival");
            arrival.transform.SetParent(go.transform);
            arrival.transform.position = position;
            var anchor = go.AddComponent<RegionAnchor>();
            anchor.Configure(regionId, arrival.transform, null, null);
            anchor.ConfigureArrivals(new NamedArrival(key, arrival.transform, null));
            anchor.ConfigureExtent(EastWaterPlan.RegionWorldCenter, EastWaterPlan.RegionWorldSize);
            go.SetActive(true);
        }

        static RegionPassage AddDoor(Scene scene, RegionDef target, string key)
        {
            var go = new GameObject("RegionSceneLoader");
            go.SetActive(false);
            SceneManager.MoveGameObjectToScene(go, scene);
            var loader = go.AddComponent<RegionSceneLoader>();
            var passage = go.AddComponent<RegionPassage>();
            passage.Configure(target, loader, key);
            go.SetActive(true);
            return passage;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_rig != null) Object.DestroyImmediate(_rig);
            if (_player != null) Object.DestroyImmediate(_player);
            if (_boatGo != null) Object.DestroyImmediate(_boatGo);
            if (_fixtureDefs != null) foreach (var def in _fixtureDefs) Object.DestroyImmediate(def);
            if (_origin.IsValid() && _origin.isLoaded) SceneManager.SetActiveScene(_origin);
            if (_water.IsValid() && _water.isLoaded) yield return SceneManager.UnloadSceneAsync(_water);
            if (_island.IsValid() && _island.isLoaded) yield return SceneManager.UnloadSceneAsync(_island);
            GameServices.Reset();
        }

        void AssertLiveHelm(Vector3 arrival)
        {
            Assert.AreEqual(arrival, _boatGo.transform.position);
            Assert.AreEqual(ControlMode.Aboard, _switcher.Mode);
            Assert.IsTrue(_boat.enabled);
            Assert.IsTrue(_input.enabled);
            Assert.AreSame(_boat, GameServices.Helm.PilotedHull);
            Assert.AreSame(_boat, GameServices.Helm.PlayersBoat);
            Assert.IsNotNull(GameServices.HelmControl);
            Assert.IsTrue(GameServices.HelmControl.HasHelm);
            GameServices.HelmControl.DragDrive(0.6f);
            Assert.AreEqual(0.6f, _boat.Throttle, 1e-5f, "the arrived helm still drives this boat");
            Assert.IsNull(GameServices.PendingArrivalKey);
        }

        [UnityTest]
        public IEnumerator ThePlayersBoatSailsOutAndHomeWithTheHelmLiveAcrossBothCrossings()
        {
            var sameBoat = _boat;
            _boat.enabled = false;
            _input.enabled = false;
            _outward.Activate();
            AssertLiveHelm(EastWaterPlan.FromStPetersArrivalPos);
            Assert.AreEqual(_water, SceneManager.GetActiveScene());
            yield return null;

            _boat.enabled = false;
            _input.enabled = false;
            _homeward.Activate();
            AssertLiveHelm(StPetersBuilder.FromEastWaterArrivalPos);
            Assert.AreEqual(_island, SceneManager.GetActiveScene());
            Assert.AreSame(sameBoat, _boat);
            yield return null;
        }

        [UnityTest]
        public IEnumerator ArrivingAtSeaAddsNoLandingSnapOrHeadingChange()
        {
            Vector3 fisher = _player.transform.position;
            _boatGo.transform.rotation = Quaternion.Euler(0f, 0f, -90f);
            Quaternion heading = _boatGo.transform.rotation;
            _outward.Activate();
            Assert.AreEqual(fisher, _player.transform.position, "a water region has no disembark point");
            Assert.AreEqual(heading, _boatGo.transform.rotation);
            AssertLiveHelm(EastWaterPlan.FromStPetersArrivalPos);
            yield return null;
        }

        [UnityTest]
        public IEnumerator TheFlatBedRegistersAsTheRegionsLiveTerrain()
        {
            var go = new GameObject("EastWaterBed");
            go.SetActive(false);
            SceneManager.MoveGameObjectToScene(go, _water);
            var terrain = go.AddComponent<RectTidalTerrain>();
            EastWaterPlan.Plan.ConfigureTerrain(terrain);
            go.SetActive(true);
            _outward.Activate();
            yield return null;
            Assert.AreSame(terrain, GameServices.TidalTerrain);
            Assert.AreEqual(-4f, GameServices.TidalTerrain.ElevationAt(Vector2.zero), 1e-4f);
            _homeward.Activate();
            yield return null;
            Assert.IsNull(GameServices.TidalTerrain);
        }
    }
}
#endif
