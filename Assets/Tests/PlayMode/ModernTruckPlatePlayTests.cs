#if UNITY_EDITOR
using System.Collections;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using HiddenHarbours.Player;
using HiddenHarbours.Vehicles;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.PlayMode
{
    public class ModernTruckPlatePlayTests
    {
        WharfNightStage stage;
        GameObject truck;
        ControlSwitcher switcher;
        HeldDriveInput held;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            held?.Release();
            if (switcher != null && switcher.Mode == ControlMode.Driving) switcher.LeaveDriving();
            if (truck != null) Object.Destroy(truck);
            if (stage != null) yield return stage.TearDown();
            stage = null;
            truck = null;
            switcher = null;
            held = null;
        }

        [UnityTest]
        public IEnumerator Modern350IsParkedThenDriven() => Drive("350");

        [UnityTest]
        public IEnumerator Modern2500IsParkedThenDriven() => Drive("2500");

        IEnumerator Drive(string number)
        {
            WharfNightStage.RequireAGraphicsDevice();
            string key = "modern" + number;
            stage = new WharfNightStage("NineMileCreek", "ModernTruckPlates");
            yield return stage.Load();
            yield return stage.SetNight(12f);

            var def = AssetDatabase.LoadAssetAtPath<VehicleDef>(
                "Assets/_Project/Data/Vehicles/Modern" + number + ".asset");
            Assert.That(def, Is.Not.Null);
            Assert.That(def.IsUsable(), Is.True);
            Assert.That(def.Id, Is.EqualTo("vehicle.modern_" + number));

            // Spawn north of the occupied bay, on the same dry strip used by the 3500's drive.
            Vector2 start = (Vector2)NineMileCreekMainland.Modern3500ParkPos + Vector2.up * 12f;
            truck = new GameObject("Modern" + number + "Plate", typeof(Rigidbody2D));
            truck.SetActive(false);
            truck.transform.position = start;
            truck.GetComponent<Rigidbody2D>().gravityScale = 0f;
            var parked = truck.AddComponent<ParkedVehicle>();
            parked.Configure(def, drivable: true);
            truck.SetActive(true);
            yield return null;
            Assert.That(parked.IsSkinned, Is.True);
            Assert.That(VehicleGrounding.IsDryLandNow(start), Is.True);
            var controller = parked.Controller;
            var door = parked.Door;
            Assert.That(controller, Is.Not.Null);
            Assert.That(door, Is.Not.Null);
            yield return stage.FrameOn(start);
            Capture(key + "-parked.png");

            Time.timeScale = 1f;
            yield return null;
            if (ShellFlow.WorldInputBlocked) ShellFlow.Reset();
            InteractionGate.Reset();
            switcher = Object.FindFirstObjectByType<ControlSwitcher>();
            Assert.That(switcher, Is.Not.Null);
            held = new HeldDriveInput();
            switcher.ConfigureDriveInput(held);
            Assert.That(switcher.TryEnterDriving(door), Is.True);
            yield return null;
            Assert.That(switcher.Mode, Is.EqualTo(ControlMode.Driving));
            Assert.That(switcher.DrivenSeat, Is.SameAs(door));
            var presenter = Object.FindFirstObjectByType<PlayerDrivePresenter>();
            bool driverDrawn = presenter != null && presenter.IsShowing;
            bool showsDriver = door.ShowsDriver;
            float cap = VehicleGrounding.SpeedCapNow(def.Kind, start, truck.transform.up,
                def.MaxSpeedMetersPerSecond, def.Mesh.FrontAxleY, def.Mesh.RearAxleY,
                def.BrakingMetersPerSecondSquared, def.Mesh.WheelRadiusMeters);
            Debug.Log($"[ModernTruckPlates] {key}: start={start}, dry={VehicleGrounding.IsDryLandNow(start)}, cap={cap}, mass={def.MassKg}.");

            float began = controller.OdometerMeters;
            held.Set(1f, 0f, false);
            int steps = 0;
            bool floated = false;
            while (controller.OdometerMeters - began < 12f && steps++ < 3000)
            {
                yield return new WaitForFixedUpdate();
                floated |= controller.IsAfloat;
            }
            float rode = controller.OdometerMeters - began;
            held.Set(0f, 0f, true);
            int braking = 0;
            while (Mathf.Abs(controller.SpeedMetersPerSecond) > 0.05f && braking++ < 400)
                yield return new WaitForFixedUpdate();
            float speed = Mathf.Abs(controller.SpeedMetersPerSecond);
            held.Release();
            Vector2 finish = truck.transform.position;
            Debug.Log($"[ModernTruckPlates] {key}: rode={rode}, steps={steps}, speed={speed}, finish={finish}, inputReads={held.Reads}, floated={floated}.");
            yield return stage.FrameOn(finish);
            Capture(key + "-driven.png");
            switcher.LeaveDriving();
            yield return null;

            Assert.That(rode, Is.GreaterThanOrEqualTo(11.5f));
            Assert.That(Vector2.Distance(start, finish), Is.GreaterThanOrEqualTo(11.5f));
            Assert.That(speed, Is.LessThanOrEqualTo(0.05f));
            Assert.That(floated, Is.False);
            Assert.That(showsDriver, Is.False);
            Assert.That(driverDrawn, Is.False);
            Assert.That(VehicleGrounding.IsDryLandNow(finish), Is.True);
        }

        void Capture(string name)
        {
            var facet = truck.GetComponentInChildren<IsoFacetHullRenderer>(true);
            Assert.That(facet, Is.Not.Null);
            Assert.That(facet.HullId, Is.GreaterThan(0), "Truck has no compositing slot.");
            Vector3 screen = stage.Camera.WorldToViewportPoint(truck.transform.position);
            Assert.That(screen.x, Is.InRange(0.03f, 0.97f));
            Assert.That(screen.y, Is.InRange(0.03f, 0.97f));
            stage.SavePlate(name, stage.Capture());
        }
    }
}
#endif
