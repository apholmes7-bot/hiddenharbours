#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using HiddenHarbours.App;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Core;
using HiddenHarbours.Player;
using HiddenHarbours.Vehicles;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.PlayMode
{
    public class NmcDrivableTrucksPlayTests
    {
        WharfNightStage stage;
        ControlSwitcher switcher;
        PlayerWalkController player;
        HeldDriveInput input;
        static ParkedVehicle Truck(string name) => Object.FindObjectsByType<ParkedVehicle>(FindObjectsSortMode.None).Single(p => p.name == name);
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            input?.Release();
            if (switcher != null && switcher.Mode == ControlMode.Driving) switcher.LeaveDriving();
            if (stage != null) yield return stage.TearDown();
            stage = null; switcher = null; player = null; input = null;
        }
        IEnumerator Load()
        {
            stage = new WharfNightStage("NineMileCreek", "NmcDrivableTrucks");
            yield return stage.Load(); yield return stage.SetNight(12f);
            Time.timeScale = 1f;
            if (ShellFlow.WorldInputBlocked) ShellFlow.Reset();
            InteractionGate.Reset();
            switcher = Object.FindFirstObjectByType<ControlSwitcher>();
            player = Object.FindFirstObjectByType<PlayerWalkController>();
            Assert.That(switcher, Is.Not.Null); Assert.That(player, Is.Not.Null);
            input = new HeldDriveInput(); switcher.ConfigureDriveInput(input);
        }
        void Plate(string name)
        {
            stage.SavePlate(name, stage.Capture());
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../Evidence~/nmc-h6/trucks/plates"));
            Directory.CreateDirectory(folder);
            File.Copy(Path.Combine(Application.temporaryCachePath, "NmcDrivableTrucks", name), Path.Combine(folder, name), true);
        }
        static void OutsideOtherBodies(Vector2 at, ParkedVehicle own)
        {
            foreach (var other in Object.FindObjectsByType<ParkedVehicle>(FindObjectsSortMode.None))
            {
                if (other == own || other.Vehicle == null || other.Vehicle.Mesh == null || !other.Vehicle.Mesh.HasCollider) continue;
                Vector3 p = other.transform.InverseTransformPoint(at);
                var mesh = other.Vehicle.Mesh;
                Assert.That(p.x >= mesh.ColliderMinMeters.x && p.x <= mesh.ColliderMaxMeters.x &&
                    p.y >= mesh.ColliderMinMeters.y && p.y <= mesh.ColliderMaxMeters.y, Is.False,
                    "the door or exit is inside " + other.name + " at " + at);
            }
        }
        [UnityTest] public IEnumerator Modern350DrivesEightHeadings_OnTheWharfsGround() => Drive("vehicle.nmc_lot_a");
        [UnityTest] public IEnumerator Modern2500DrivesEightHeadings_OnTheWharfsGround() => Drive("vehicle.nmc_pickup_ross");
        [UnityTest] public IEnumerator Modern3500DrivesEightHeadings_OnTheWharfsGround() => Drive("vehicle.nmc_lot_c");
        IEnumerator Drive(string name)
        {
            WharfNightStage.RequireAGraphicsDevice();
            yield return Load(); var truck = Truck(name);
            Assert.That(truck.Controller, Is.Not.Null); Assert.That(truck.Door, Is.Not.Null);
            var body = truck.GetComponent<Rigidbody2D>();
            Vector2 start = NineMileCreekGroundPlan.TurningPadCentre;
            for (int heading = 0; heading < 8; heading++)
            {
                Time.timeScale = 1f;
                body.position = start; body.rotation = -45f * heading;
                body.linearVelocity = Vector2.zero; body.angularVelocity = 0;
                Physics2D.SyncTransforms(); yield return null;
                Vector2 door = truck.Door.DoorWorldPosition;
                player.transform.position = door; Physics2D.SyncTransforms();
                Assert.That(VehicleGrounding.IsDryLandNow(door), Is.True, name + " door is on ground");
                Assert.That(truck.Door.IsAvailable, Is.True);
                Assert.That(switcher.TryEnterDriving(truck.Door), Is.True, name + " enter heading " + heading);
                float began = truck.Controller.OdometerMeters; input.Set(1,0,false);
                int steps = 0;
                while (truck.Controller.OdometerMeters - began < 1.2f && steps++ < 500) yield return new WaitForFixedUpdate();
                input.Set(0,0,true); int braking = 0;
                while (Mathf.Abs(truck.Controller.SpeedMetersPerSecond) > 0.05f && braking++ < 250) yield return new WaitForFixedUpdate();
                input.Release();
                Assert.That(truck.Controller.OdometerMeters - began, Is.GreaterThanOrEqualTo(1.2f), name + " heading " + heading);
                Assert.That(Vector2.Distance(start, body.position), Is.GreaterThan(1f), name + " really moved");
                Assert.That(truck.Controller.IsAfloat, Is.False);
                Assert.That(VehicleGrounding.IsDryLandNow(body.position), Is.True);
                Assert.That(switcher.LeaveDriving(), Is.True, name + " exit heading " + heading);
                yield return null;
                Assert.That(switcher.Mode, Is.EqualTo(ControlMode.OnFoot));
                Assert.That(VehicleGrounding.IsDryLandNow(player.transform.position), Is.True);
                yield return stage.FrameOn(truck.transform.position);
                Plate(name.Replace("vehicle.nmc_", "") + "-heading-" + heading + ".png");
                TestContext.WriteLine(name + " heading " + heading + ": entered, drove " + (truck.Controller.OdometerMeters - began) + " m, exited onto ground");
            }
        }
        [UnityTest]
        public IEnumerator AtNoon_TheDuallyAnd350HaveReachableDoors_AndExitsOutsideOtherBodies()
        {
            WharfNightStage.RequireAGraphicsDevice(); yield return Load();
            Assert.That(Vector2.Distance(Truck(NineMileCreekWharfLandSide.BuyerRunRowId).transform.position, new Vector2(101,128.4f)), Is.LessThan(0.03f));
            foreach (string name in new[] { NineMileCreekTruckPark.TruckName, "vehicle.nmc_lot_a" })
            {
                Time.timeScale = 1f;var truck = Truck(name);Vector2 door = truck.Door.DoorWorldPosition;
                // Walk the published approach from the yard road to the real door; a neighbouring body cannot be crossed.
                Vector2 from = new Vector2(door.x, NineMileCreekGroundPlan.YardRoadY);
                float length = Vector2.Distance(from, door);
                for (float s=0;s<=length;s+=0.1f)
                {
                    Vector2 at=Vector2.Lerp(from,door,length==0?1:s/length);
                    OutsideOtherBodies(at,truck);
                    Assert.That(VehicleGrounding.IsDryLandNow(at), Is.True, name + " approach " + at);
                }
                player.transform.position = door; Physics2D.SyncTransforms();
                Assert.That(switcher.TryEnterDriving(truck.Door), Is.True, name + " enter at noon");yield return null;
                Assert.That(switcher.LeaveDriving(), Is.True, name + " exit at noon");yield return null;
                OutsideOtherBodies(player.transform.position,truck);
                Assert.That(VehicleGrounding.IsDryLandNow(player.transform.position), Is.True);
                yield return stage.FrameOn(new Vector2(101,128.4f));
                Plate(name == NineMileCreekTruckPark.TruckName ? "noon-dually-exit.png" : "noon-350-exit.png");
                TestContext.WriteLine(name + ": real door " + door + ", safe exit " + player.transform.position);
            }
        }
        [UnityTest]
        public IEnumerator TheBuyerLeavesBeforeDawn_ReturnsAtNight_AndNeverOffersHisTruck()
        {
            WharfNightStage.RequireAGraphicsDevice();yield return Load();
            var truck=Truck(NineMileCreekWharfLandSide.BuyerRunRowId);var trip=truck.GetComponent<ScheduledTrip>();
            Assert.That(trip, Is.Not.Null);Assert.That(trip.enabled, Is.True);
            foreach(float hour in new[]{3f,4.9f,12f,22f})
            {
                yield return stage.SetNight(hour);yield return null;
                Assert.That(truck.Door.IsAvailable, Is.False, "the trip owns her at " + hour);
                Assert.That(switcher.TryEnterDriving(truck.Door), Is.False);
                Vector2 at=truck.transform.position;
                if(hour==3||hour==22)Assert.That(Vector2.Distance(at,new Vector2(-131,78)),Is.LessThan(0.03f));
                else if(hour==12)Assert.That(Vector2.Distance(at,new Vector2(101,128.4f)),Is.LessThan(0.03f));
                else Assert.That(Vector2.Distance(at,new Vector2(-131,78)),Is.GreaterThan(1f),"she left before dawn");
                yield return stage.FrameOn(at);Plate("buyer-"+hour.ToString("0.0",System.Globalization.CultureInfo.InvariantCulture)+".png");
                TestContext.WriteLine("buyer " + hour + ": " + at + ", " + trip.Pose.Stage + ", player refused");
            }
        }
    }
}
#endif
