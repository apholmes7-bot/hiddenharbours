using HiddenHarbours.Art;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// <see cref="WaterLightBridge"/>: the array of water lights the ADR 0016 single-light note reserved.
    /// These pin the three things that decide whether the sea is lit by the right lamps — that a DARK lamp
    /// cannot hold one of the few slots, that when more lamps exist than slots the NEAREST ones win, and that
    /// the lamp HEIGHT (the lever the wave relief turns a flat disc into a raking beam with) actually reaches
    /// the shader.
    ///
    /// <para><b>Teardown matters here.</b> These are GLOBAL shader uniforms, and a global is sticky: leaving
    /// <c>_WaterLightCount</c> above zero would hand a phantom beam to every render fixture that draws water
    /// afterwards. The count is zeroed in <c>TearDown</c> and the registry emptied, so nothing this file does
    /// escapes it.</para>
    /// </summary>
    public sealed class WaterLightBridgeTests
    {
        private static readonly int IdPos = Shader.PropertyToID("_WaterLightPos");
        private static readonly int IdParams = Shader.PropertyToID("_WaterLightParams");
        private static readonly int IdCount = Shader.PropertyToID("_WaterLightCount");

        private GameObject _host;
        private WaterLightBridge _bridge;
        private readonly System.Collections.Generic.List<FakeLamp> _registered =
            new System.Collections.Generic.List<FakeLamp>();

        /// <summary>A water-light emitter with no scene behind it — the interface is all the bridge needs.</summary>
        private sealed class FakeLamp : IWaterLightEmitter
        {
            public WaterLightState State;
            public bool TryGetWaterLight(out WaterLightState state)
            {
                state = State;
                return state.IsLive;
            }
        }

        private FakeLamp Lamp(float x, float y, float intensity = 1f, float height = 2.5f)
        {
            var lamp = new FakeLamp
            {
                State = new WaterLightState
                {
                    LampWorld = new Vector2(x, y),
                    LampHeightMeters = height,
                    BeamDir = Vector2.up,
                    Color = Color.white,
                    Intensity = intensity,
                    Range = 30f,
                    CosHalfAngle = 0.7f,
                    CosInnerAngle = 0.9f,
                    EdgeSoftness = 0.5f,
                    GateThreshold = 0.4f,
                    GateSoftness = 0.2f,
                    GateFallback = 1f,
                },
            };
            WaterLightBridge.Register(lamp);
            _registered.Add(lamp);
            return lamp;
        }

        [SetUp]
        public void SetUp()
        {
            _host = new GameObject("WaterLightBridgeTestHost") { hideFlags = HideFlags.HideAndDontSave };
            _bridge = _host.AddComponent<WaterLightBridge>();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (FakeLamp lamp in _registered) WaterLightBridge.Unregister(lamp);
            _registered.Clear();
            if (_host != null) Object.DestroyImmediate(_host);
            // A global is STICKY: hand the next fixture a dark sea, not this one's beams.
            Shader.SetGlobalFloat(IdCount, 0f);
        }

        [Test]
        public void NoLamps_PublishesCountZero_SoTheWaterFallsBackToTheShippedSingletonPath()
        {
            _bridge.PublishFromRegistry();
            Assert.AreEqual(0f, Shader.GetGlobalFloat(IdCount), 1e-6f);
        }

        [Test]
        public void ADarkLamp_DoesNotHoldASlot()
        {
            Lamp(0f, 0f, intensity: 0f);
            Lamp(5f, 0f, intensity: 1f);
            _bridge.PublishFromRegistry();

            Assert.AreEqual(1f, Shader.GetGlobalFloat(IdCount), 1e-6f,
                "a lamp that is switched off lights nothing and must not consume one of the four slots");
            Vector4[] pos = Shader.GetGlobalVectorArray(IdPos);
            Assert.AreEqual(5f, pos[0].x, 1e-4f, "the LIVE lamp must be the one that took the slot");
        }

        [Test]
        public void TheLampHeight_ReachesTheShader_BecauseTheReliefIsNothingWithoutIt()
        {
            Lamp(3f, 4f, height: 7.25f);
            _bridge.PublishFromRegistry();

            Vector4[] pos = Shader.GetGlobalVectorArray(IdPos);
            Assert.AreEqual(3f, pos[0].x, 1e-4f);
            Assert.AreEqual(4f, pos[0].y, 1e-4f);
            Assert.AreEqual(7.25f, pos[0].z, 1e-4f,
                "pos.z is the lamp height — with it at 0 the shader skips the relief and draws the flat cone");
        }

        [Test]
        public void MoreLampsThanSlots_KeepsTheNearestOnes()
        {
            // No camera in an EditMode fixture, so the bridge measures from the origin — which is exactly what
            // makes this orderable and deterministic. Register FAR-to-NEAR so a bridge that simply kept the
            // first four it saw would fail.
            Lamp(500f, 0f);
            Lamp(400f, 0f);
            Lamp(300f, 0f);
            Lamp(200f, 0f);
            Lamp(1f, 0f);
            Lamp(2f, 0f);
            _bridge.PublishFromRegistry();

            Assert.AreEqual(WaterLightBridge.MaxLights, Shader.GetGlobalFloat(IdCount), 1e-6f,
                "the count must saturate at the slot budget, never overflow it");

            Vector4[] pos = Shader.GetGlobalVectorArray(IdPos);
            Assert.AreEqual(1f, pos[0].x, 1e-4f, "nearest first");
            Assert.AreEqual(2f, pos[1].x, 1e-4f, "then the next nearest");
            Assert.AreEqual(200f, pos[2].x, 1e-4f);
            Assert.AreEqual(300f, pos[3].x, 1e-4f);
        }

        [Test]
        public void EveryLampsParameters_SurviveThePacking()
        {
            Lamp(6f, -2f);
            _bridge.PublishFromRegistry();

            Vector4[] prm = Shader.GetGlobalVectorArray(IdParams);
            Assert.AreEqual(1f, prm[0].x, 1e-4f, "intensity");
            Assert.AreEqual(30f, prm[0].y, 1e-4f, "range");
            Assert.AreEqual(0.7f, prm[0].z, 1e-4f, "cos(half angle)");
            Assert.AreEqual(0.9f, prm[0].w, 1e-4f, "cos(inner angle)");
        }

        [Test]
        public void UnregisteringALamp_ReleasesItsSlot()
        {
            FakeLamp lamp = Lamp(4f, 0f);
            _bridge.PublishFromRegistry();
            Assert.AreEqual(1f, Shader.GetGlobalFloat(IdCount), 1e-6f, "precondition: she is lit");

            WaterLightBridge.Unregister(lamp);
            _registered.Remove(lamp);
            _bridge.PublishFromRegistry();
            Assert.AreEqual(0f, Shader.GetGlobalFloat(IdCount), 1e-6f,
                "a destroyed or disabled lamp must not leave a beam burning on the water");
        }

        [Test]
        public void RegisteringTwice_DoesNotDoubleTheLamp()
        {
            FakeLamp lamp = Lamp(4f, 0f);
            WaterLightBridge.Register(lamp);          // a second OnEnable without an OnDisable
            _bridge.PublishFromRegistry();
            Assert.AreEqual(1f, Shader.GetGlobalFloat(IdCount), 1e-6f,
                "one lamp is one light — a duplicate registration would double its brightness on the sea");
        }
    }
}
