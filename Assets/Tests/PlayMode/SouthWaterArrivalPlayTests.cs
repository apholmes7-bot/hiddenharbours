#if UNITY_EDITOR
using System.Collections;
using System.Linq;
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
    /// <summary>A boat arrives in the South Water from each of its three north edges, afloat and with
    /// the helm live. EastWaterSailPlayTests' two-scene rig: a fixture scene stands in for whichever
    /// neighbour sends her (no neighbour scene has its exit yet), and the South Water fixture carries
    /// the plan's own bed and named arrivals. No bootstrap, opening or save writer is started.</summary>
    public class SouthWaterArrivalPlayTests
    {
        static readonly string[] Keys =
        {
            SouthWaterPlan.FromRicePointArrivalKey, SouthWaterPlan.FromStPetersArrivalKey,
            SouthWaterPlan.FromEastWaterArrivalKey,
        };

        Scene _origin, _north, _water;
        GameObject _player, _boatGo, _rig;
        BoatController _boat;
        DevBoatInput _input;
        ControlSwitcher _switcher;
        RegionDef _waterDef;
        RectTidalTerrain _bed;

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();
            _origin = SceneManager.GetActiveScene();
            _north = SceneManager.CreateScene("SouthWaterArrival_Neighbour");
            _water = SceneManager.CreateScene("SouthWaterArrival_SouthWater");

            var committed = AssetDatabase.LoadAssetAtPath<RegionDef>(
                WaterSceneTemplate.RegionAssetPathFor(SouthWaterPlan.RegionAssetName));
            _waterDef = ScriptableObject.Instantiate(committed);
            _waterDef.SceneName = _water.name;

            var bedGo = new GameObject("SouthWaterBed");
            bedGo.SetActive(false);
            SceneManager.MoveGameObjectToScene(bedGo, _water);
            _bed = bedGo.AddComponent<RectTidalTerrain>();
            SouthWaterPlan.Plan.ConfigureTerrain(_bed);
            bedGo.SetActive(true);

            var anchorGo = new GameObject("RegionAnchor");
            anchorGo.SetActive(false);
            SceneManager.MoveGameObjectToScene(anchorGo, _water);
            var fallback = new GameObject("DefaultArrival");
            fallback.transform.SetParent(anchorGo.transform);
            fallback.transform.position = SouthWaterPlan.DefaultArrivalPos;
            var anchor = anchorGo.AddComponent<RegionAnchor>();
            anchor.Configure(committed.Id, fallback.transform, null, null);
            anchor.ConfigureArrivals(SouthWaterPlan.NamedArrivals.Select(a =>
            {
                var point = new GameObject("Arrival_" + a.Key);
                point.transform.SetParent(anchorGo.transform);
                point.transform.position = a.Position;
                return new NamedArrival(a.Key, point.transform, null);
            }).ToArray());
            anchor.ConfigureExtent(SouthWaterPlan.RegionWorldCenter, SouthWaterPlan.RegionWorldSize);
            anchorGo.SetActive(true);

            _player = new GameObject("PersistentPlayer");
            _boatGo = new GameObject("PlayersBoat");
            _boat = _boatGo.AddComponent<BoatController>();
            _boat.SetHull(AssetDatabase.LoadAssetAtPath<BoatHullDef>("Assets/_Project/Data/Boats/Punt.asset"));
            _input = _boatGo.AddComponent<DevBoatInput>();
            _rig = new GameObject("PersistentTravelRig");
            _rig.SetActive(false);
            _switcher = _rig.AddComponent<ControlSwitcher>();
            _switcher.Configure(null, _boat, _input, null, 8f, null);
            typeof(ControlSwitcher).GetMethod("TakeHelm", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(_switcher, null);
            SceneManager.SetActiveScene(_north);
            var coordinator = _rig.AddComponent<RegionTravelCoordinator>();
            coordinator.Configure(_player.transform, _boatGo.transform, _switcher, null);
            _rig.SetActive(true);
        }

        RegionPassage NeighbourDoor(string key)
        {
            var go = new GameObject("NeighbourExit_" + key);
            go.SetActive(false);
            SceneManager.MoveGameObjectToScene(go, _north);
            var loader = go.AddComponent<RegionSceneLoader>();
            var passage = go.AddComponent<RegionPassage>();
            passage.Configure(_waterDef, loader, key);
            go.SetActive(true);
            return passage;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_rig != null) Object.DestroyImmediate(_rig);
            if (_player != null) Object.DestroyImmediate(_player);
            if (_boatGo != null) Object.DestroyImmediate(_boatGo);
            if (_waterDef != null) Object.DestroyImmediate(_waterDef);
            if (_origin.IsValid() && _origin.isLoaded) SceneManager.SetActiveScene(_origin);
            if (_water.IsValid() && _water.isLoaded) yield return SceneManager.UnloadSceneAsync(_water);
            if (_north.IsValid() && _north.isLoaded) yield return SceneManager.UnloadSceneAsync(_north);
            GameServices.Reset();
        }

        [UnityTest]
        public IEnumerator ABoatArrivesFromEachNorthEdge_AfloatWithTheHelmLive([ValueSource(nameof(Keys))] string key)
        {
            Vector3 expected = SouthWaterPlan.NamedArrivals.Single(a => a.Key == key).Position;
            _boatGo.transform.rotation = Quaternion.Euler(0f, 0f, 180f);   // heading south, as she came
            Quaternion heading = _boatGo.transform.rotation;

            NeighbourDoor(key).Activate();
            Assert.AreEqual(_water, SceneManager.GetActiveScene());
            Assert.AreEqual(expected, _boatGo.transform.position, key);
            Assert.AreEqual(heading, _boatGo.transform.rotation, "arriving at sea turns nobody round");
            Assert.AreEqual(ControlMode.Aboard, _switcher.Mode);
            Assert.AreSame(_boat, GameServices.Helm.PilotedHull);
            Assert.IsTrue(GameServices.HelmControl.HasHelm);
            Assert.IsNull(GameServices.PendingArrivalKey);
            yield return null;

            Assert.AreSame(_bed, GameServices.TidalTerrain, "the South Water's own bed is the live ground");
            Vector2 at = _boatGo.transform.position;
            float water = SouthWaterPlan.SpringLowWater - GameServices.TidalTerrain.ElevationAt(at);
            Assert.That(water, Is.GreaterThan(10f), $"{key}: {water} m under her at the lowest tide");
            Assert.IsTrue(SouthWaterPlan.Plan.WorldBounds.Contains(at));
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Assert.That(Vector2.Distance(at, _boatGo.transform.position), Is.LessThan(1f),
                "she lies where she was put down: nothing grounds her or throws her");
        }

        [UnityTest]
        public IEnumerator AnUnknownKeyLandsAtTheDefaultArrival_StillAfloat()
        {
            NeighbourDoor("no_such_key").Activate();
            Assert.AreEqual(SouthWaterPlan.DefaultArrivalPos, _boatGo.transform.position);
            yield return null;
            Assert.That(SouthWaterPlan.SpringLowDepthAt(_boatGo.transform.position), Is.GreaterThan(10f));
        }
    }
}
#endif
