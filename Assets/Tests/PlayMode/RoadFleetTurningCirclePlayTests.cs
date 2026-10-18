#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using HiddenHarbours.Core;
using HiddenHarbours.Vehicles;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// R1: each committed road truck drives a complete circle at crawl and above-crawl speeds.
    /// The measurement comes from three body positions and the remaining samples check the fit;
    /// neither the controller's yaw value nor the steering helper supplies the measured radius.
    /// </summary>
    public class RoadFleetTurningCirclePlayTests
    {
        private sealed class FlatGround : ITidalTerrain
        {
            public float ElevationAt(Vector2 worldPos) => 10f;
        }

        private sealed class StillWater : IEnvironmentService
        {
            public int WorldSeed => 7;
            public TideProfile ActiveTideProfile { get; set; }
            public EnvironmentSample Sample() => default;
            public float TideHeightAt(double totalSeconds) => 0f;
            public float WaterLevelAt(double totalSeconds) => 0f;
        }

        private GameObject _truck;
        private ITidalTerrain _previousTerrain;
        private IEnvironmentService _previousEnvironment;
        private float _previousTimeScale;
        private float _previousFixedDeltaTime;

        [SetUp]
        public void SetUp()
        {
            _previousTerrain = GameServices.TidalTerrain;
            _previousEnvironment = GameServices.Environment;
            _previousTimeScale = Time.timeScale;
            _previousFixedDeltaTime = Time.fixedDeltaTime;
            GameServices.TidalTerrain = new FlatGround();
            GameServices.Environment = new StillWater();
            Time.timeScale = 1f;
            Time.fixedDeltaTime = 0.02f;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (_truck != null) Object.Destroy(_truck);
            _truck = null;
            yield return null;
            GameServices.TidalTerrain = _previousTerrain;
            GameServices.Environment = _previousEnvironment;
            Time.fixedDeltaTime = _previousFixedDeltaTime;
            Time.timeScale = _previousTimeScale;
        }

        [UnityTest]
        public IEnumerator TheDually3500TurnsThePredictedCircles() => DriveCircles("Dually3500");
        [UnityTest]
        public IEnumerator TheModern350TurnsThePredictedCircles() => DriveCircles("Modern350");
        [UnityTest]
        public IEnumerator TheModern2500TurnsThePredictedCircles() => DriveCircles("Modern2500");
        [UnityTest]
        public IEnumerator TheModern3500TurnsThePredictedCircles() => DriveCircles("Modern3500");
        [UnityTest]
        public IEnumerator TheHightopVanTurnsThePredictedCircles() => DriveCircles("HightopVan");
        [UnityTest]
        public IEnumerator TheCaboverBoxTurnsThePredictedCircles() => DriveCircles("CaboverBox");
        [UnityTest]
        public IEnumerator TheConvBoxTurnsThePredictedCircles() => DriveCircles("ConvBox");
        [UnityTest]
        public IEnumerator TheClassicSemiTurnsThePredictedCircles() => DriveCircles("ClassicSemi");
        [UnityTest]
        public IEnumerator TheAeroSemiTurnsThePredictedCircles() => DriveCircles("AeroSemi");

        private IEnumerator DriveCircles(string name)
        {
            var def = AssetDatabase.LoadAssetAtPath<VehicleDef>(
                "Assets/_Project/Data/Vehicles/" + name + ".asset");
            Assert.That(def, Is.Not.Null);
            Assert.That(def.Mesh, Is.Not.Null);
            _truck = new GameObject(name + " turning circle", typeof(Rigidbody2D), typeof(VehicleController));
            // The empty test scene needs one listener to avoid a warning on every rendered frame.
            if (Object.FindFirstObjectByType<AudioListener>() == null)
                _truck.AddComponent<AudioListener>();
            var body = _truck.GetComponent<Rigidbody2D>();
            var controller = _truck.GetComponent<VehicleController>();
            var step = new WaitForFixedUpdate();

            foreach (float speed in new[] { 2f, 6f })
            {
                controller.SetVehicle(def);
                body.position = Vector2.zero;
                body.rotation = 0f;
                body.linearVelocity = Vector2.zero;
                body.angularVelocity = 0f;
                controller.SteerDemand = 1f;
                controller.Throttle = speed / def.MaxSpeedMetersPerSecond;

                int settling = 0;
                while ((Mathf.Abs(controller.SpeedMetersPerSecond - speed) > 0.001f ||
                        Mathf.Abs(controller.Steer - 1f) > 0.001f) && settling++ < 1000)
                    yield return step;
                Assert.That(controller.SpeedMetersPerSecond, Is.EqualTo(speed).Within(0.001f));
                Assert.That(controller.Steer, Is.EqualTo(1f).Within(0.001f));
                for (int i = 0; i < 10; i++) yield return step;

                // Predict directly from the committed geometry and falloff data, independently
                // of the production helper. The controller applies its bicycle yaw to this body.
                double divisor = def.SteerFalloffHalfSpeedMetersPerSecond <= 0f ? 1.0 :
                    1.0 + Math.Max(0.0, speed - def.SteerFalloffStartMetersPerSecond) /
                    def.SteerFalloffHalfSpeedMetersPerSecond;
                double innerRadians = def.Mesh.MaxInnerSteerDegrees * Math.PI / 180.0 / divisor;
                double expectedRadius = def.Mesh.WheelbaseMeters / Math.Tan(innerRadians) +
                                        def.Mesh.FrontTrackMeters * 0.5;

                var samples = new List<Vector2> { body.position };
                Vector2 first = body.position, second = default, third = default;
                float previousRotation = body.rotation;
                float swept = 0f, maximumSpeedError = 0f, maximumLockError = 0f;
                bool hasSecond = false, hasThird = false;
                int steps = 0;
                while (swept < 360f && steps++ < 6000)
                {
                    yield return step;
                    float rotation = body.rotation;
                    swept += Mathf.DeltaAngle(previousRotation, rotation);
                    previousRotation = rotation;
                    samples.Add(body.position);
                    maximumSpeedError = Mathf.Max(maximumSpeedError,
                        Mathf.Abs(controller.SpeedMetersPerSecond - speed));
                    maximumLockError = Mathf.Max(maximumLockError, Mathf.Abs(controller.Steer - 1f));
                    if (!hasSecond && swept >= 120f) { second = body.position; hasSecond = true; }
                    if (!hasThird && swept >= 240f) { third = body.position; hasThird = true; }
                }
                Assert.That(swept, Is.GreaterThanOrEqualTo(360f), name + " did not complete her circle.");
                Assert.That(maximumSpeedError, Is.LessThan(0.001f), "speed changed during the measurement");
                Assert.That(maximumLockError, Is.LessThan(0.001f), "full steering demand was not held");
                Assert.That(hasSecond && hasThird, Is.True);

                // Circumcentre of the three measured points, with double precision for the fit.
                double ux = second.x - first.x, uy = second.y - first.y;
                double vx = third.x - first.x, vy = third.y - first.y;
                double cross = ux * vy - uy * vx;
                Assert.That(Math.Abs(cross), Is.GreaterThan(1.0), "the measured path is degenerate");
                double u2 = ux * ux + uy * uy, v2 = vx * vx + vy * vy;
                double cx = first.x + (vy * u2 - uy * v2) / (2.0 * cross);
                double cy = first.y + (ux * v2 - vx * u2) / (2.0 * cross);
                double dx = first.x - cx, dy = first.y - cy;
                double measuredRadius = Math.Sqrt(dx * dx + dy * dy);
                double maximumRadialError = 0.0;
                foreach (Vector2 sample in samples)
                {
                    dx = sample.x - cx;
                    dy = sample.y - cy;
                    maximumRadialError = Math.Max(maximumRadialError,
                        Math.Abs(Math.Sqrt(dx * dx + dy * dy) - measuredRadius));
                }
                TestContext.WriteLine($"{name}, {speed:F1} m/s: predicted {expectedRadius:F4} m, " +
                    $"measured {measuredRadius:F4} m, maximum radial error {maximumRadialError:F4} m, " +
                    $"{samples.Count} body-position samples.");
                Assert.That(measuredRadius, Is.EqualTo(expectedRadius).Within(expectedRadius * 0.05),
                    name + " body-position circle differs from the committed geometry and falloff.");
                Assert.That(maximumRadialError, Is.LessThan(measuredRadius * 0.05),
                    "the sampled path does not remain on the measured circle");
            }
        }
    }
}
#endif
