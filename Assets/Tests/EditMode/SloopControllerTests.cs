using System.Reflection;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>Controller wiring guards for the Phase B EditMode runner. Layer-isolated editor Box2D simulation;
    /// the audit-calibrated inertia and calm force fixture are explicit, not a St Peters replay.</summary>
    public class SloopControllerTests
    {
        sealed class Terrain : ITidalTerrain { public float ElevationAt(Vector2 p) => -4f; }
        sealed class Sea : IEnvironmentService
        {
            public int WorldSeed => 12345;
            public TideProfile ActiveTideProfile { get; set; }
            public EnvironmentSample Sample() => new EnvironmentSample(new Vector2(1.19f, 4.28f),
                new Vector2(.32f, .0056f), .75f, SeaState.Glass, 1f);
            public float TideHeightAt(double t) => .75f;
            public float WaterLevelAt(double t) => .75f;
        }

        [TestCase(30, 1f, 13f)] [TestCase(30, .75f, 20f)]
        [TestCase(30, .5f, 25f)] [TestCase(30, .25f, 30f)]
        [TestCase(88, 1f, 13f)] [TestCase(88, .75f, 20f)]
        [TestCase(88, .5f, 25f)] [TestCase(88, .25f, 30f)]
        [TestCase(30, 0f, 20f)] [TestCase(88, 0f, 20f)]
        public void Controller_PaysOffWithinProposedBar(int size, float helm, float barSeconds)
            => WithBoat(size, false, helm, barSeconds);

        [TestCase(30, 20f)] [TestCase(88, 25f)]
        public void Controller_UnmannedDoesNotSailOnForTwentySevenSeconds(int size, float barMetres)
            => WithBoat(size, true, 0f, barMetres);

        static void WithBoat(int size, bool unmanned, float helm, float bar)
        {
            var oldTerrain = GameServices.TidalTerrain; var oldSea = GameServices.Environment;
            // Runtime SceneManager.CreateScene(localPhysicsMode) is unavailable in EditMode.
            // Simulate only an unused layer, so no existing editor body or collider participates.
            int usedLayers = 0;
            foreach (var b in Object.FindObjectsByType<Rigidbody2D>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                usedLayers |= 1 << b.gameObject.layer;
            foreach (var c in Object.FindObjectsByType<Collider2D>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                usedLayers |= 1 << c.gameObject.layer;
            int layer = 31;
            while (layer >= 0 && (usedLayers & (1 << layer)) != 0) layer--;
            Assert.That(layer, Is.GreaterThanOrEqualTo(0), "an unused physics layer is required for isolation");
            var go = new GameObject("sloop controller probe"); go.layer = layer;
            go.hideFlags = HideFlags.DontSave;
            var oldSimulation = Physics2D.simulationMode;
            Physics2D.simulationMode = SimulationMode2D.Script;
            try
            {
                var sea = new Sea(); GameServices.Environment = sea; GameServices.TidalTerrain = new Terrain();
                var h = SloopMotionModelTests.Hull(size);
                var boat = go.AddComponent<BoatController>(); boat.SetHull(h);
                var body = go.GetComponent<Rigidbody2D>();
                body.mass = h.MassKg / 100f; body.inertia = size == 30 ? 45.62f : 832.16f;
                body.gravityScale = 0f; body.linearDamping = .2f; body.angularDamping = 2.5f;
                float from = BoatKinematics.BearingDegrees(-sea.Sample().WindVector);
                float heading = unmanned ? 260f : helm == 0f ? from : from - 55f;
                body.rotation = -heading; boat.enabled = !unmanned;
                boat.SetControl(0f, helm);
                body.linearVelocity = unmanned ? Vector2.zero : sea.Sample().CurrentVector
                    + SloopMotionModelTests.Forward(heading) * (helm == 0f ? 0f : size == 30 ? 2.314f : 3.634f);
                var tick = typeof(BoatController).GetMethod("StepPhysics", BindingFlags.NonPublic | BindingFlags.Instance);
                bool entered = false, escaped = false; float hang = 0f;
                const float dt = .02f;
                int steps = unmanned ? 1350 : 7500;
                for (int i = 0; i < steps; i++)
                {
                    if (unmanned) boat.TickUnmannedDrift(); else tick.Invoke(boat, null);
                    bool irons = boat.DrawnInIrons;
                    if (irons) { entered = true; hang += dt; }
                    if (entered && !irons && !unmanned) { escaped = true; break; }
                    Assert.That(Physics2D.Simulate(dt, 1 << layer), Is.True, "the native physics step must run");
                }
                Assert.That(boat.IsAground, Is.False, "4.75 m water clears both authored keels");
                if (unmanned) Assert.That(body.position.magnitude, Is.LessThan(bar));
                else { Assert.That(escaped, Is.True); Assert.That(hang, Is.LessThan(bar)); }
            }
            finally
            {
                Physics2D.simulationMode = oldSimulation;
                GameServices.TidalTerrain = oldTerrain; GameServices.Environment = oldSea;
                Object.DestroyImmediate(go);
            }
        }
    }
}
