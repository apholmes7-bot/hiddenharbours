using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using HiddenHarbours.App;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// ⭐ <b>A SLOOP ANSWERS HER HELM — and only from the helm.</b> The shipped Sloop30, in the real
    /// physics loop, with the real key layer (<see cref="DevBoatInput"/>) on her.
    ///
    /// <para><b>What was wrong before this PR.</b> The key layer branched Engine → the engine helm,
    /// anything else → the OARS. A sail hull is not an engine hull, so she was read as a rowed one:
    /// <c>ReadOars</c> writes oar strokes and never the steer, so no key, stick or zero ever reached
    /// her rudder. Whatever steer sat on the controller STAYED there — including one carried in from
    /// an engine hull by the dev F swap (<c>SetHull</c> resets neither steer nor throttle), which
    /// kept a sloop circling with nobody touching anything. The first two tests below are red on
    /// that main for exactly that reason.</para>
    ///
    /// <para><b>Headless.</b> A virtual keyboard with nothing pressed makes the key layer's
    /// <c>Update</c> run (HelmDashPlayTests' pattern); presses themselves are undeliverable, so the
    /// gate is driven through <see cref="DevBoatInput.DriveSailHelm"/>, the one write every read goes
    /// through, exactly as DoryAboardPlayTests drives <c>DriveOars</c>.</para>
    ///
    /// <para><b>The sea is glass</b> (seaState01 = 0), so no seakeeping yaw can be mistaken for the
    /// rudder's. Physics steps are counted as steps, never frames as seconds.</para>
    /// </summary>
    public class SailHelmPlayTests
    {
        const string Sloop30 = "Assets/_Project/Data/Boats/Sloop30.asset";

        /// <summary>The player's hull capsule (PersistentCoreBuilder; ArrivalOpening's greybox hull is
        /// the same) — <c>SetHull</c> never resizes a collider, so this is the yaw inertia a sloop
        /// swapped onto the player's hull actually has.</summary>
        static readonly Vector2 PlayersHullCapsule = new Vector2(1.7f, 4.0f);

        const float TwsKn = 12f;

        private readonly List<Object> _spawned = new();
        private Keyboard _testKeyboard;

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();
            HelmKeyCapture.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            GameServices.Reset();
            HelmKeyCapture.Reset();
            if (_testKeyboard != null)
            {
                InputSystem.RemoveDevice(_testKeyboard);
                _testKeyboard = null;
            }
            foreach (var o in _spawned)
                if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        sealed class GlassSea : IEnvironmentService
        {
            public Vector2 Wind;
            public int WorldSeed => 0;
            public TideProfile ActiveTideProfile { get; set; }
            public EnvironmentSample Sample() => new EnvironmentSample(Wind, Vector2.zero, tideHeight: 0f,
                HiddenHarbours.Core.SeaState.Calm, visibility: 1f, seaState01: 0f);
            public float TideHeightAt(double totalSeconds) => 0f;
            public float WaterLevelAt(double totalSeconds) => 0f;
        }

        /// <summary>A true wind of <paramref name="twsKn"/> blowing FROM compass north.</summary>
        static Vector2 NortherlyWind(float twsKn) => Vector2.down * SailDrive.ToMetresPerSecond(twsKn);

        static float HeadingOf(Transform t) => BoatKinematics.BearingDegrees(t.up);

        private void ConfigWithSteerWind(float seconds)
        {
            var cfg = ScriptableObject.CreateInstance<GameConfig>();
            HelmWheelSettings wheel = cfg.HelmWheel;
            wheel.SteerEaseSeconds = seconds;
            cfg.HelmWheel = wheel;
            _spawned.Add(cfg);
            GameServices.Config = cfg;
        }

#if UNITY_EDITOR
        static BoatHullDef LoadSloop30()
        {
            var hull = AssetDatabase.LoadAssetAtPath<BoatHullDef>(Sloop30);
            Assert.IsNotNull(hull, $"{Sloop30} is missing — this fixture asserts the REAL shipped sloop.");
            Assert.IsTrue(hull.HasSailPlan,
                $"premise: {Sloop30} declares a sail plan. Without one she is not a sail hull, and every " +
                "test here would be measuring the oar branch.");
            return hull;
        }

        /// <summary>
        /// A Sloop30 on the player's hull capsule, heading <paramref name="headingDeg"/>, optionally
        /// carrying the key layer. The body and collider are RESOLVED from the controller's
        /// RequireComponent, never added a second time.
        /// </summary>
        IEnumerator BuildSloop(string name, float headingDeg, bool withKeyLayer,
                               System.Action<BoatController, DevBoatInput> ready, float eastMetres = 0f)
        {
            var go = new GameObject(name);
            _spawned.Add(go);
            go.transform.position = new Vector3(eastMetres, 0f, 0f);
            go.transform.rotation = Quaternion.Euler(0f, 0f, -headingDeg);
            var boat = go.AddComponent<BoatController>();
            Assert.AreEqual(1, go.GetComponents<Rigidbody2D>().Length, "premise: one body");
            var cols = go.GetComponents<CapsuleCollider2D>();
            Assert.AreEqual(1, cols.Length, "premise: one hull collider");
            cols[0].direction = CapsuleDirection2D.Vertical;
            cols[0].size = PlayersHullCapsule;
            DevBoatInput input = withKeyLayer ? go.AddComponent<DevBoatInput>() : null;
            yield return null;                    // Awake caches the body
            boat.SetHull(LoadSloop30());
            yield return null;
            ready(boat, input);
        }

        /// <summary>Put her under way on <paramref name="headingDeg"/> at <paramref name="speedMs"/>,
        /// with no turn on.</summary>
        static void UnderWay(BoatController boat, float headingDeg, float speedMs)
        {
            var rb = boat.GetComponent<Rigidbody2D>();
            float r = headingDeg * Mathf.Deg2Rad;
            rb.linearVelocity = new Vector2(Mathf.Sin(r), Mathf.Cos(r)) * speedMs;
            rb.angularVelocity = 0f;
        }

        static IEnumerator Steps(int n)
        {
            for (int i = 0; i < n; i++) yield return new WaitForFixedUpdate();
        }
#endif

        // ---- 🔴 red on main: no input read reached a sail hull's rudder ---------------------------

        /// <summary>
        /// 🔴 <b>At the helm, the key layer owns her rudder:</b> a steer left standing on the controller
        /// (the dev F swap carries one in from the last hull) centres the moment the key layer runs
        /// with nothing held, and a stale throttle goes with it.
        ///
        /// <para>On main this stays at 0.7 for ever: the sloop was read as an OAR hull, and the oar
        /// read writes strokes, never steer.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator SailHull_AtTheHelm_TheKeyLayerOwnsHerRudder_AStaleSteerCentres()
        {
#if !UNITY_EDITOR
            Assert.Ignore("Needs the AssetDatabase: this asserts the REAL committed sloop.");
            yield break;
#else
            ConfigWithSteerWind(0.0001f);   // the ease ON (the shipped path), settling inside a frame
            GameServices.Environment = new GlassSea { Wind = Vector2.zero };
            BoatController boat = null; DevBoatInput input = null;
            yield return BuildSloop("SailHelmManned", 0f, withKeyLayer: true, (b, i) => { boat = b; input = i; });
            _testKeyboard = InputSystem.AddDevice<Keyboard>();   // headless has no device otherwise
            GameServices.Helm.SetPilotedHull(boat);

            Assert.IsTrue(input.enabled && input.HelmKeysLive, "premise: the key layer is live");
            Assert.IsTrue(input.RowingStationManned, "premise: the helm slot names her — she is at the helm");

            boat.SetControl(0.6f, 0.7f);                  // what an engine hull leaves behind on F
            Assert.AreEqual(0.7f, boat.Steer, 1e-5f, "premise: the stale steer landed");
            yield return null;
            yield return null;
            yield return null;

            Assert.AreEqual(0f, boat.Steer, 1e-5f,
                "no keys held at a sloop's helm must centre her rudder. A steer still standing means no " +
                "input read reaches a sail hull's rudder at all (she was being read as an OAR hull, and " +
                "the oar read never writes steer) — a sloop that circles on a rudder nobody is holding");
            Assert.AreEqual(0f, boat.Throttle, 1e-5f,
                "and the throttle the last engine hull left must be cleared: the wind is her drive");
#endif
        }

        /// <summary>
        /// 🔴 <b>Unmanned, her helm writes ZERO</b> — the DriveOars rule on a sail hull. The same
        /// fixture as the test above with ONE thing moved: the helm slot does not name her.
        /// </summary>
        [UnityTest]
        public IEnumerator SailHull_Unmanned_HerHelmWritesZero()
        {
#if !UNITY_EDITOR
            Assert.Ignore("Needs the AssetDatabase: this asserts the REAL committed sloop.");
            yield break;
#else
            ConfigWithSteerWind(0.0001f);
            GameServices.Environment = new GlassSea { Wind = Vector2.zero };
            BoatController boat = null; DevBoatInput input = null;
            yield return BuildSloop("SailHelmUnmanned", 0f, withKeyLayer: true, (b, i) => { boat = b; input = i; });
            _testKeyboard = InputSystem.AddDevice<Keyboard>();
            GameServices.Helm.SetPilotedHull(null);

            Assert.IsTrue(input.enabled && input.HelmKeysLive, "premise: the key layer is live");
            Assert.IsFalse(input.RowingStationManned, "premise: nobody is at her helm");

            boat.SetControl(0.6f, 0.7f);
            Assert.AreEqual(0.7f, boat.Steer, 1e-5f, "premise: the stale steer landed");
            yield return null;
            yield return null;
            yield return null;

            Assert.AreEqual(0f, boat.Steer, 1e-5f,
                "an unmanned sloop's helm must write the rudder to centre, not decline to write — " +
                "declining leaves the last rudder standing and she keeps turning with nobody aboard her helm");
            Assert.AreEqual(0f, boat.Throttle, 1e-5f, "and the throttle with it");
#endif
        }

        // ---- the gate, driven directly, under way ---------------------------------------------------

        /// <summary>
        /// <b>Manned, <see cref="DevBoatInput.DriveSailHelm"/> reaches the rudder, and the rudder turns
        /// her.</b> Starboard helm, under way on a beam reach, and her compass heading climbs.
        /// </summary>
        [UnityTest]
        public IEnumerator DriveSailHelm_Manned_ReachesSteer_AndTheRudderYawsHerUnderWay()
        {
#if !UNITY_EDITOR
            Assert.Ignore("Needs the AssetDatabase: this asserts the REAL committed sloop.");
            yield break;
#else
            GameServices.Environment = new GlassSea { Wind = NortherlyWind(TwsKn) };
            BoatController boat = null; DevBoatInput input = null;
            yield return BuildSloop("SailHelmYaw", 90f, withKeyLayer: true, (b, i) => { boat = b; input = i; });
            input.enabled = false;    // no device read in the loop: the write under test is the only writer
            GameServices.Helm.SetPilotedHull(boat);
            UnderWay(boat, 90f, SailDrive.ToMetresPerSecond(6f));

            boat.SetControl(0.6f, 0f);
            input.DriveSailHelm(1f);
            Assert.AreEqual(1f, boat.Steer, 1e-5f, "manned, the helm's steer must reach her controller");
            Assert.AreEqual(0f, boat.Throttle, 1e-5f, "and the throttle is written 0 — the wind is her drive");

            float before = HeadingOf(boat.transform);
            yield return Steps(50);
            float turned = Mathf.DeltaAngle(before, HeadingOf(boat.transform));
            float way = Vector2.Dot(boat.GetComponent<Rigidbody2D>().linearVelocity, boat.transform.up);

            Assert.Greater(way, 2f, "premise: she still has full steerage way (RudderTorque saturates at 2 m/s)");
            Assert.Greater(turned, 2f,
                $"full starboard helm under way for 50 physics steps must swing her bow to starboard " +
                $"(compass heading up); she turned {turned:0.00}°");
#endif
        }

        /// <summary>
        /// <b>Unmanned, the same write centres the rudder and she holds her heading</b> — then, on the
        /// SAME hull, the slot is handed to her and the same write turns her: the gate is the only
        /// thing that moved.
        /// </summary>
        [UnityTest]
        public IEnumerator DriveSailHelm_Unmanned_WritesZero_AndHerHeadingHolds()
        {
#if !UNITY_EDITOR
            Assert.Ignore("Needs the AssetDatabase: this asserts the REAL committed sloop.");
            yield break;
#else
            GameServices.Environment = new GlassSea { Wind = NortherlyWind(TwsKn) };
            BoatController boat = null; DevBoatInput input = null;
            yield return BuildSloop("SailHelmHold", 90f, withKeyLayer: true, (b, i) => { boat = b; input = i; });
            input.enabled = false;
            GameServices.Helm.SetPilotedHull(null);
            UnderWay(boat, 90f, SailDrive.ToMetresPerSecond(6f));

            boat.SetControl(0.6f, 0.7f);
            input.DriveSailHelm(1f);
            Assert.AreEqual(0f, boat.Steer, 1e-5f, "unmanned, the helm writes the rudder to centre");
            Assert.AreEqual(0f, boat.Throttle, 1e-5f, "and the throttle to 0");

            float before = HeadingOf(boat.transform);
            yield return Steps(50);
            float held = Mathf.DeltaAngle(before, HeadingOf(boat.transform));
            Assert.AreEqual(0f, held, 0.5f, $"with her rudder centred she must hold her heading; she turned {held:0.00}°");

            // The positive, on the same hull: hand her the slot and the same write turns her.
            GameServices.Helm.SetPilotedHull(boat);
            input.DriveSailHelm(1f);
            before = HeadingOf(boat.transform);
            yield return Steps(50);
            float turned = Mathf.DeltaAngle(before, HeadingOf(boat.transform));
            Assert.Greater(turned, 2f,
                $"manned, the same helm must turn her — otherwise the hold above proves nothing; she turned {turned:0.00}°");
#endif
        }

        // ---- 📌 pin: a sloop has no engine helm (PR 3's auxiliary moves this, by name) -------------

        /// <summary>
        /// 📌 <b>A sail hull shows no engine helm</b>: the relay reports no helm, no control style, and
        /// declines a wheel steer session — the arbitration premise <see cref="DevBoatInput.DriveSailHelm"/>
        /// states. Green on main by design; it exists so the auxiliary (PR 3) moves a NAMED premise.
        /// The positive is the F cycle on the same hull: an engine hull swapped on shows a helm.
        /// </summary>
        [UnityTest]
        public IEnumerator SailHull_HasNoEngineHelm()
        {
#if !UNITY_EDITOR
            Assert.Ignore("Needs the AssetDatabase: this asserts the REAL committed sloop.");
            yield break;
#else
            GameServices.Environment = new GlassSea { Wind = Vector2.zero };
            BoatController boat = null;
            yield return BuildSloop("SailHelmRelay", 0f, withKeyLayer: false, (b, _) => boat = b);
            GameServices.Helm.SetPilotedHull(boat);
            var relay = boat.GetComponent<HelmControlRelay>();
            Assert.IsNotNull(relay, "premise: BoatController self-installs a relay on every hull");

            var engine = ScriptableObject.CreateInstance<BoatHullDef>();
            engine.Id = "boat.test_sail_helm_engine";
            engine.Propulsion = PropulsionType.Engine;
            engine.MassKg = 700f;
            _spawned.Add(engine);
            boat.SetHull(engine);
            Assert.IsTrue(relay.HasHelm, "positive: an engine hull on the same controller shows a helm");
            Assert.AreEqual(HelmControlStyle.Tiller, relay.Style, "…the bare outboard's tiller");

            boat.SetHull(LoadSloop30());
            boat.SetControl(0f, 0f);
            Assert.IsFalse(relay.HasHelm, "a sloop has no engine helm (her auxiliary is PR 3's)");
            Assert.AreEqual(HelmControlStyle.None, relay.Style, "and shows no tiller or lever");
            relay.DragSteer(0.5f);
            Assert.IsFalse(relay.SteerDragActive,
                "and opens no wheel steer session, so the key layer's arbitration passes her keys straight through");
            Assert.AreEqual(0f, boat.Steer, 1e-5f, "the declined session wrote nothing");
#endif
        }

        // ---- condition (a): the unmanned zero write cannot reach a hull another writer drives ------

        /// <summary>
        /// ⭐ <b>The zero write stays on its own hull.</b> Two sloops, nobody at either helm (the
        /// arrival's passenger state: the slot is empty). A carries the key layer, live; B is driven by
        /// a skipper through <see cref="HelmedBoat"/> — the arrival opening's own adapter — and carries
        /// no key layer, as the arrival's hull does not. A's unmanned zero write runs (the positive) and
        /// B keeps the skipper's rudder.
        /// </summary>
        [UnityTest]
        public IEnumerator TheUnmannedZeroWrite_CannotReachAHullAnotherWriterDrives()
        {
#if !UNITY_EDITOR
            Assert.Ignore("Needs the AssetDatabase: this asserts the REAL committed sloop.");
            yield break;
#else
            ConfigWithSteerWind(0.0001f);
            GameServices.Environment = new GlassSea { Wind = Vector2.zero };
            BoatController playersHull = null; DevBoatInput input = null;
            yield return BuildSloop("PlayersSloop", 0f, withKeyLayer: true, (b, i) => { playersHull = b; input = i; });
            BoatController skippersHull = null;
            yield return BuildSloop("SkippersSloop", 0f, withKeyLayer: false, (b, _) => skippersHull = b,
                                    eastMetres: 20f);   // clear of A's hull: no contact between them
            _testKeyboard = InputSystem.AddDevice<Keyboard>();
            GameServices.Helm.SetPilotedHull(null);

            Assert.IsNull(skippersHull.GetComponent<DevBoatInput>(), "premise: a skipper's hull carries no key layer");
            Assert.IsTrue(input.enabled && !input.RowingStationManned, "premise: A's key layer is live and unmanned");

            var skipper = new HelmedBoat(skippersHull, skippersHull.transform);
            playersHull.SetControl(0f, 0.7f);
            skipper.SetControl(0f, 0.7f);
            yield return null;
            yield return null;
            yield return null;

            Assert.AreEqual(0f, playersHull.Steer, 1e-5f,
                "positive: the unmanned zero write ran on the key layer's own hull this frame");
            Assert.AreEqual(0.7f, skippersHull.Steer, 1e-5f,
                "the skipper's rudder must survive another hull's unmanned helm: the zero write reaches " +
                "only the controller on its own GameObject");
#endif
        }

        /// <summary>
        /// ⭐ <b>The switcher's passenger state holds too.</b> On the player's OWN hull, with her key
        /// layer DISABLED and the slot empty — what <c>ControlSwitcher</c> sets on every path that is
        /// not Aboard (board to deck, leave the helm, disembark, a region hop onto the deck, the shell's
        /// park) — a writer steering her keeps his rudder. The positive on the same hull: enable the
        /// layer and the zero write lands, so the enabled flag is the thing that held it.
        /// </summary>
        [UnityTest]
        public IEnumerator WhileHerKeyLayerIsDisabled_AnotherWriterOnHerHullKeepsHisRudder()
        {
#if !UNITY_EDITOR
            Assert.Ignore("Needs the AssetDatabase: this asserts the REAL committed sloop.");
            yield break;
#else
            ConfigWithSteerWind(0.0001f);
            GameServices.Environment = new GlassSea { Wind = Vector2.zero };
            BoatController boat = null; DevBoatInput input = null;
            yield return BuildSloop("PassengerSloop", 0f, withKeyLayer: true, (b, i) => { boat = b; input = i; });
            _testKeyboard = InputSystem.AddDevice<Keyboard>();
            GameServices.Helm.SetPilotedHull(null);
            input.enabled = false;                       // the switcher's non-Aboard state

            var skipper = new HelmedBoat(boat, boat.transform);
            skipper.SetControl(0f, 0.7f);
            yield return null;
            yield return null;
            yield return null;
            Assert.AreEqual(0.7f, boat.Steer, 1e-5f,
                "with her key layer disabled, nothing of hers may touch a rudder somebody else is holding");

            input.enabled = true;                        // the positive: the one flag moved
            yield return null;
            yield return null;
            yield return null;
            Assert.AreEqual(0f, boat.Steer, 1e-5f,
                "enabled and unmanned, the zero write lands — so the enabled flag is what held it above");
#endif
        }
    }
}
