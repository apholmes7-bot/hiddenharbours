using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Core;
using HiddenHarbours.Vehicles;
using static HiddenHarbours.App.Editor.StPetersLayerRefresh;

namespace HiddenHarbours.Tests.EditMode
{
    public class NmcDrivableTruckTests
    {
        static readonly string[] Rows = { "vehicle.nmc_lot_e", "vehicle.nmc_lot_a", "vehicle.nmc_lot_c",
            "vehicle.nmc_pickup_leo", "vehicle.nmc_pickup_ross", "vehicle.nmc_pickup_marie" };
        static SceneYaml Scene() => SceneYaml.Parse(File.ReadAllText(NineMileCreekLayerRefresh.ScenePath));
        static Doc Named(SceneYaml scene, string name) => scene.Docs.Single(d => d.ClassId == 1 && d.Field("m_Name") == name);

        [Test]
        public void EveryPlacedPickup_HasItsBodyControllerAndDoor()
        {
            var scene = Scene();
            foreach (string row in Rows)
            {
                Doc go = Named(scene, row);
                var parts = scene.ComponentsOf(go).ToArray();
                var parked = parts.Single(d => d.IsScript("HiddenHarbours.Vehicles.ParkedVehicle"));
                var controller = parts.Single(d => d.IsScript("HiddenHarbours.Vehicles.VehicleController"));
                Assert.That(parts.Count(d => d.ClassId == 50), Is.EqualTo(1), row);
                Assert.That(parts.Single(d => d.ClassId == 50).Field("m_GravityScale"), Is.EqualTo("0"), row);
                Assert.That(parts.Count(d => d.IsScript("HiddenHarbours.Vehicles.VehicleDoor")), Is.EqualTo(1), row);
                Assert.That(controller.Field("_vehicle"), Is.EqualTo(parked.Field("_vehicle")), row);
                Assert.That(parked.Field("_drivable"), Is.EqualTo("1"), row);
                Assert.That(parts.Length, Is.EqualTo(row == "vehicle.nmc_lot_e" ? 7 : 6), row);
            }
        }

        [Test]
        public void TheBuyerOwnsTheExisting2500_AndTheDuallyKeepsHerSeparateBay()
        {
            var scene = Scene();
            Doc buyer = Named(scene, "vehicle.nmc_lot_e");
            var machine = scene.ComponentsOf(buyer).Single(d => d.IsScript("HiddenHarbours.Vehicles.ParkedVehicle"));
            var trip = scene.ComponentsOf(buyer).Single(d => d.IsScript("HiddenHarbours.Vehicles.ScheduledTrip"));
            StringAssert.Contains(AssetDatabase.AssetPathToGUID("Assets/_Project/Data/Vehicles/Modern2500.asset"), machine.Field("_vehicle"));
            Assert.That(trip.FieldRef("_machine"), Is.EqualTo(machine.FileId));
            StringAssert.Contains(AssetDatabase.AssetPathToGUID("Assets/_Project/Data/Vehicles/Trips/FishBuyerRun.asset"), trip.Field("_trip"));
            Doc driver = Named(scene, NineMileCreekTrips.FishBuyerAssetName);
            Assert.That(trip.FieldRef("_driver"), Is.EqualTo(scene.TransformOf(driver).FileId));
            Assert.That(scene.ComponentsOf(driver).Single(d => d.IsScript("HiddenHarbours.Art.YSortSprite")).Field("_dynamic"), Is.EqualTo("1"));
            Doc dually = Named(scene, NineMileCreekTruckPark.TruckName);
            Assert.That(scene.ComponentsOf(dually).Any(d => d.IsScript("HiddenHarbours.Vehicles.ScheduledTrip")), Is.False);
            Assert.That(NineMileCreekWharfLandSide.BuyerTruck.x, Is.EqualTo(98.2f).Within(0.001f));
            Assert.That(NineMileCreekWharfLandSide.BuyerRunBay.x, Is.EqualTo(101f));
            Assert.That(NineMileCreekMainland.ParkingPos, Is.EqualTo(NineMileCreekWharfLandSide.BuyerRunBay));
            Assert.That(NineMileCreekMainland.FishBuyerTruckPos, Is.EqualTo(NineMileCreekWharfLandSide.BuyerRunBay));
        }

        [Test]
        public void TheAssignedWestLotBays_KeepThePublishedBodiesApart()
        {
            var scene = Scene();
            var boxes = new List<Rect>();
            foreach (var truck in new[]
            {
                (name: NineMileCreekTruckPark.TruckName, width: 2.72f, length: 6.76f),
                (name: "vehicle.nmc_lot_e", width: 2.24f, length: 6.62f),
                (name: "vehicle.nmc_lot_a", width: 2.41f, length: 6.92f),
            })
            {
                Vector3 p = ParseVec3(scene.TransformOf(Named(scene, truck.name)).Field("m_LocalPosition"));
                var box = new Rect(p.x - truck.width * 0.5f, p.y - truck.length * 0.5f, truck.width, truck.length);
                Rect lot = NineMileCreekRoads.ParkingArea();
                Assert.That(lot.Contains(box.min) && lot.Contains(box.max), Is.True, truck.name);
                foreach (var other in boxes) Assert.That(box.Overlaps(other), Is.False, truck.name);
                boxes.Add(box);
            }
        }

        [TestCase("Modern350"), TestCase("Modern2500"), TestCase("Modern3500")]
        public void EachAssignedModel_HasUsableDrivingAndItsPublishedDoor(string model)
        {
            var def = AssetDatabase.LoadAssetAtPath<VehicleDef>("Assets/_Project/Data/Vehicles/" + model + ".asset");
            Assert.That(def, Is.Not.Null);
            Assert.That(def.IsUsable(), Is.True, model);
            Assert.That(def.Mesh.DriveDoorLocal, Is.Not.EqualTo(Vector2.zero), model);
        }

        [Test]
        public void ATripOwnsItsDoor_EvenWhenNobodyHoldsTheWheel()
        {
            var def = AssetDatabase.LoadAssetAtPath<VehicleDef>("Assets/_Project/Data/Vehicles/Modern2500.asset");
            var go = new GameObject("TripDoorFixture", typeof(Rigidbody2D));
            go.SetActive(false);
            go.GetComponent<Rigidbody2D>().gravityScale = 0;
            try
            {
                go.AddComponent<VehicleController>().SetVehicle(def);
                var door = go.AddComponent<VehicleDoor>();
                Assert.That(door.IsDrivable && door.IsAvailable, Is.True, "positive control: the unassigned machine offers its door");
                var trip = go.AddComponent<ScheduledTrip>();
                Assert.That(DriveSeats.IsOccupied(door), Is.False, "the resting wheel is unclaimed");
                Assert.That(door.IsDrivable, Is.False, "the direct drive-seat gate refuses the trip-owned machine too");
                Assert.That(door.IsAvailable, Is.False, "resting does not offer somebody else's machine");
                Assert.That(DriveSeats.TryClaim(door, trip), Is.True, "the trip can still claim its own wheel");
                DriveSeats.ReleaseAllFor(trip);
                trip.enabled = false;
                Assert.That(door.IsAvailable, Is.True, "a disabled trip gives the machine back");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void TheTruckAndBuyerPatches_AppliedTwice_AreByteIdentical()
        {
            string Apply(string text)
            {
                text = NineMileCreekWharfLandSide.PlaceRows(SceneYaml.Parse(text)).ApplyTo(text);
                return NineMileCreekWharfLandSide.MoveBuyer(SceneYaml.Parse(text)).ApplyTo(text);
            }
            string first = Apply(File.ReadAllText(NineMileCreekLayerRefresh.ScenePath));
            Assert.That(Apply(first), Is.EqualTo(first));
        }
    }
}
