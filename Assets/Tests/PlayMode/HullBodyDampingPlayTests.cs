using System.Collections;
using System.Reflection;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>Native integration gates, to run only in Phase B. Each body lives in a local 2D
    /// physics scene; explicit simulation advances seconds, independent of render-frame load.</summary>
    public class HullBodyDampingPlayTests
    {
        Scene _scene;
        IEnvironmentService _oldSea;
        ITidalTerrain _oldTerrain;
        BoatHullDef _hull;
        const float Dt = .02f;

        sealed class Sea : IEnvironmentService
        {
            public Vector2 Wind;
            public int WorldSeed => 12345;
            public TideProfile ActiveTideProfile { get; set; }
            public EnvironmentSample Sample() => new EnvironmentSample(Wind,
                new Vector2(.32f, .0056f), 1f, SeaState.Glass, 1f);
            public float TideHeightAt(double time) => 1f;
            public float WaterLevelAt(double time) => 1f;
        }
        sealed class Deep : ITidalTerrain { public float ElevationAt(Vector2 p) => -20f; }

        [SetUp]
        public void SetUp()
        {
            _oldSea = GameServices.Environment; _oldTerrain = GameServices.TidalTerrain;
            GameServices.Environment = new Sea(); GameServices.TidalTerrain = new Deep();
            _scene = SceneManager.CreateScene("hull damping native gate",
                new CreateSceneParameters(LocalPhysicsMode.Physics2D));
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            GameServices.Environment = _oldSea; GameServices.TidalTerrain = _oldTerrain;
            if (_scene.IsValid()) yield return SceneManager.UnloadSceneAsync(_scene);
            if (_hull != null) Object.Destroy(_hull);
        }

        GameObject BodyObject()
        {
            var go = new GameObject("isolated hull");
            SceneManager.MoveGameObjectToScene(go, _scene);
            return go;
        }

        [Test]
        public void ConstantForce_NativeOrderIsForceThenImplicitDamping()
        {
            var body = BodyObject().AddComponent<Rigidbody2D>();
            body.gravityScale = 0f; body.mass = 2f; body.linearDamping = 1f;
            body.sleepMode = RigidbodySleepMode2D.NeverSleep;
            body.linearVelocity = new Vector2(2f, 0f);
            float forceThen = 2f, dampingThen = 2f;
            const float h = .1f, acceleration = 3f;
            for (int i = 0; i < 10; i++)
            {
                body.AddForce(new Vector2(6f, 0f));
                Assert.That(_scene.GetPhysicsScene2D().Simulate(h), Is.True);
                forceThen = (forceThen + h * acceleration) / (1f + h);
                dampingThen = dampingThen / (1f + h) + h * acceleration;
            }
            TestContext.WriteLine($"native={body.linearVelocity.x:R}, force-then={forceThen:R}, damping-then={dampingThen:R}");
            Assert.That(Mathf.Abs(forceThen - dampingThen), Is.GreaterThan(.1f), "the two oracles must discriminate");
            Assert.That(body.linearVelocity.x, Is.EqualTo(forceThen).Within(.0001f),
                "the proposed current correction is exact only with force-then-damping; revisit it if this fails");
        }

        [TestCase(PropulsionType.Oars, false)]
        [TestCase(PropulsionType.Engine, false)]
        [TestCase(PropulsionType.Oars, true)]
        public void UnpoweredController_SettlesWithTheCurrent(PropulsionType propulsion, bool unmanned)
        {
            var go = BodyObject();
            var boat = go.AddComponent<BoatController>();
            _hull = ScriptableObject.CreateInstance<BoatHullDef>();
            _hull.MassKg = 700f; _hull.Propulsion = propulsion;
            _hull.WindExposure = 0f; _hull.ForwardDrag = 140f; _hull.LateralDrag = 360f;
            boat.SetHull(_hull); boat.enabled = !unmanned;
            var body = go.GetComponent<Rigidbody2D>();
            body.sleepMode = RigidbodySleepMode2D.NeverSleep;
            var tick = typeof(BoatController).GetMethod("StepPhysics", BindingFlags.Instance | BindingFlags.NonPublic);
            for (int i = 0; i < 15000; i++)
            {
                if (unmanned) boat.TickUnmannedDrift(); else tick.Invoke(boat, null);
                Assert.That(_scene.GetPhysicsScene2D().Simulate(Dt), Is.True);
            }
            Assert.That((body.linearVelocity - GameServices.Environment.Sample().CurrentVector).magnitude,
                Is.LessThan(.00005f), "300 seconds: through-water drift is under 0.0001 kn");
        }

        [Test]
        public void DisembarkedHull_SlackPainter_SettlesWithTheCurrent()
        {
            var go = BodyObject();
            var boat = go.AddComponent<BoatController>();
            _hull = ScriptableObject.CreateInstance<BoatHullDef>();
            _hull.MassKg = 700f; _hull.WindExposure = 0f;
            boat.SetHull(_hull); boat.enabled = false;
            var body = go.GetComponent<Rigidbody2D>();
            body.sleepMode = RigidbodySleepMode2D.NeverSleep;
            var mooring = go.GetComponent<BoatMooring>();
            var hand = BodyObject().transform;
            mooring.Hold(hand);
            // Move the hand with the hull each tick so this tests the mooring's drift ownership,
            // not the rope limit. A separate existing mooring suite guards the taut restraint.
            var tick = typeof(BoatMooring).GetMethod("FixedUpdate", BindingFlags.Instance | BindingFlags.NonPublic);
            for (int i = 0; i < 15000; i++)
            {
                hand.position = body.position;
                tick.Invoke(mooring, null);
                Assert.That(_scene.GetPhysicsScene2D().Simulate(Dt), Is.True);
            }
            Assert.That((body.linearVelocity - GameServices.Environment.Sample().CurrentVector).magnitude,
                Is.LessThan(.00005f));
        }

        [TestCase(30)] [TestCase(88)]
        public void AuthoredSloop_UnmannedAtFiveMinutes_MeetsTheUnchangedBar(int size)
        {
#if UNITY_EDITOR
            var hull = UnityEditor.AssetDatabase.LoadAssetAtPath<BoatHullDef>(
                $"Assets/_Project/Data/Boats/Sloop{size}.asset");
            Assert.That(hull, Is.Not.Null); Assert.That(hull.HasSailPlan, Is.True);
            ((Sea)GameServices.Environment).Wind = new Vector2(1.19f, 4.28f);
            var go = BodyObject();
            var boat = go.AddComponent<BoatController>();
            boat.SetHull(hull); boat.enabled = false;
            var body = go.GetComponent<Rigidbody2D>();
            body.inertia = size == 30 ? 45.62f : 832.16f;
            body.rotation = -260f;
            body.sleepMode = RigidbodySleepMode2D.NeverSleep;
            for (int i = 0; i < 15000; i++)
            {
                boat.TickUnmannedDrift();
                Assert.That(_scene.GetPhysicsScene2D().Simulate(Dt), Is.True);
            }
            var sample = GameServices.Environment.Sample();
            float throughKn = SailDrive.ToKnots((body.linearVelocity - sample.CurrentVector).magnitude);
            var wind = SailDrive.ResolveWind(sample.WindVector, boat.transform.up, body.linearVelocity);
            TestContext.WriteLine($"Sloop {size}: throughKn={throughKn:R}, TWA={wind.TrueAngleDeg:R}");
            Assert.That(throughKn, Is.LessThan(.5f));
            Assert.That(Mathf.Abs(wind.TrueAngleDeg), Is.InRange(5f, 25f));
            Assert.That(SailDrive.TargetSpeedKn(hull.SailPolar, wind.TrueAngleDeg, wind.TrueKn,
                hull.NoGoTrueWindDeg), Is.Zero);
#else
            Assert.Ignore("Authored hull loading requires the editor test runner.");
#endif
        }
    }
}
