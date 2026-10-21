using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.App.Editor;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>SHE TRIMS TO HER SPEED — the wiring</b> (owner, 2026-09-21: <i>"Also i want trim added to
    /// the boats depending on speed and deacceleration"</i>; design <c>boats-and-navigation.md</c>
    /// §2.7.3). <see cref="BoatController"/> reads the forces its own physics tick applied and
    /// publishes the bow angle they ask for; these tests run that tick by hand
    /// (<see cref="BoatController.TickUnmannedDrift"/>, the same force pass the manned helm runs).
    ///
    /// <para>Every expected angle is worked by hand in its test from the hull below and the policy
    /// stated in <see cref="Policy"/>, never read back from the code. The test hull: a 4.5 m,
    /// 1000 kg outboard boat (body mass 1000/100 = 10), 1200 of thrust (12 after the 0.01 feel
    /// scale), 100 of forward drag (1 per m/s after the scale), the body's damping 0.2. Full ahead
    /// she balances at 12 / (1 + 0.2 × 10) = 4 m/s. Her trim: a 1° rise, no planing settle, a squat
    /// and a dip of 1.5° per m/s², a 0.4 s lag, her limits left to the policy.</para>
    ///
    /// <para>EditMode runs no physics step, so the body holds the velocity it is given and the tick
    /// reads exactly that; every velocity is read back first as a precondition.</para>
    /// </summary>
    public class HullTrimControllerTests
    {
        const float Tol = 1e-4f;
        const string DataBoats = "Assets/_Project/Data/Boats";
        const string ConfigAssetPath = "Assets/_Project/Data/Config/GameConfig.asset";

        // The five ships trim by a deliberate zero: at full ahead they make Fn 0.08–0.2, nowhere near
        // a hump, and a nod they could not make would be a lie (§2.7.3).
        static readonly string[] DeliberatelyLevel =
            { "SideDragger", "SternTrawler", "SternTrawlerMk2", "CoastalPacket", "Tanker" };

        sealed class ScriptedSea : IEnvironmentService
        {
            public Vector2 Wind, Current;
            public int WorldSeed => 0;
            public TideProfile ActiveTideProfile { get; set; }
            public EnvironmentSample Sample() => new EnvironmentSample(
                Wind, Current, tideHeight: 0f, SeaState.Calm, visibility: 1f, seaState01: 0f);
            public float TideHeightAt(double totalSeconds) => 0f;
            public float WaterLevelAt(double totalSeconds) => 0f;
        }

        readonly List<Object> _spawned = new();
        ScriptedSea _sea;
        GameConfig _config;

        // The policy these tests run under, stated here so no expectation leans on the shipped
        // defaults: the hump at Fn 0.40, the planing settle complete at Fn 0.55, and a hull that
        // leaves her limits or her lag at 0 gets +8° / −4° and 0.9 s.
        static HullTrimSettings Policy() => new HullTrimSettings
        {
            Enabled = true, HumpFroude = 0.4f, PlaningFroude = 0.55f,
            DefaultMaxBowUpDegrees = 8f, DefaultMaxBowDownDegrees = 4f, DefaultResponseSeconds = 0.9f,
        };

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();
            _sea = new ScriptedSea();
            GameServices.Environment = _sea;
            _config = ScriptableObject.CreateInstance<GameConfig>();
            _spawned.Add(_config);
            _config.HullTrim = Policy();
            GameServices.Config = _config;
        }

        [TearDown]
        public void TearDown()
        {
            GameServices.Reset();
            foreach (var o in _spawned)
                if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        BoatHullDef Hull(float humpDegrees = 1f, float accel = 1.5f, float decel = 1.5f,
                         float responseSeconds = 0.4f)
        {
            var hull = ScriptableObject.CreateInstance<BoatHullDef>();
            _spawned.Add(hull);
            hull.Id = "boat.trim_test";
            hull.Propulsion = PropulsionType.Engine;
            hull.EnginePower = 1200f; hull.MassKg = 1000f;
            hull.ForwardDrag = 100f; hull.LateralDrag = 240f;
            hull.LengthMeters = 4.5f; hull.DraughtMeters = 0.3f;
            hull.TrimHumpDegrees = humpDegrees; hull.TrimPlaningDropDegrees = 0f;
            hull.TrimAccelDegreesPerMps2 = accel; hull.TrimDecelDegreesPerMps2 = decel;
            hull.TrimMaxBowUpDegrees = 0f; hull.TrimMaxBowDownDegrees = 0f;
            hull.TrimResponseSeconds = responseSeconds;
            return hull;
        }

        BoatController Boat(BoatHullDef hull, out Rigidbody2D rb)
        {
            var go = new GameObject("TrimTestBoat");
            _spawned.Add(go);
            var boat = go.AddComponent<BoatController>();   // brings her body, collider and mooring
            boat.enabled = false;   // the helm is left, so TickUnmannedDrift runs her force pass
            rb = go.GetComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.linearDamping = BoatController.HullLinearDamping;   // what Awake sets; EditMode runs none
            boat.SetHull(hull);
            Assert.AreEqual(10f, rb.mass, Tol, "precondition: 1000 kg → body mass 10");
            return boat;
        }

        // One tick of her force pass at this helm and this velocity; the bow it asks for.
        static float TickAt(BoatController boat, Rigidbody2D rb, float throttle, Vector2 velocity)
        {
            boat.SetControl(throttle, 0f);
            rb.linearVelocity = velocity;
            Assert.AreEqual(velocity, rb.linearVelocity,
                "precondition: the body holds the velocity it is given (EditMode runs no physics step)");
            boat.TickUnmannedDrift();
            return boat.TrimTargetDegrees;
        }

        // ------------------------------------------------------------------ the target from her forces

        [Test]
        public void AtRest_UnderFullPower_SheSquatsOnHerDriveAlone()
        {
            var boat = Boat(Hull(), out var rb);
            // 12 of thrust on a body of 10: 1.2 m/s². No way on, so no drag, no damping and no rise.
            // 1.5 °/(m/s²) × 1.2 = 1.8°.
            Assert.AreEqual(1.8f, TickAt(boat, rb, 1f, Vector2.zero), Tol);
        }

        [Test]
        public void AtHerFullSpeed_TheSquatIsGone_AndSheHoldsHerRise()
        {
            var boat = Boat(Hull(), out var rb);
            // At 4 m/s: 12 − 4 of drag = 8 over 10, less 0.2 × 4 of damping = 0 m/s², so no squat.
            // Her drive holds 12 / (1 of drag + 0.2 × 10 of damping, per m/s) = 4 m/s, and
            // Fn = 4 / √(9.81 × 4.5) = 0.602 is over her hump, so her full 1° rise.
            Assert.AreEqual(1.0f, TickAt(boat, rb, 1f, new Vector2(0f, 4f)), Tol);
        }

        [Test]
        public void WhenTheThrottleIsCut_TheNoseDips()
        {
            var boat = Boat(Hull(), out var rb);
            // At 4 m/s with nothing driving her: −4 of drag over 10, less 0.2 × 4 = −1.2 m/s². She
            // carries way over her hump (Fn 0.602), so the dip is in full: 1.5 × −1.2 = −1.8°. Her
            // drive holds no speed, so there is no rise under it.
            Assert.AreEqual(-1.8f, TickAt(boat, rb, 0f, new Vector2(0f, 4f)), Tol);
        }

        [Test]
        public void Astern_FromRest_SheSitsLevel()
        {
            var boat = Boat(Hull(), out var rb);
            // Astern thrust (−1200 × 0.4 × 0.01 = −4.8) reads as braking (−0.48 m/s²), and a dip is
            // weighted by the way she carries ahead: none, so 0. Astern holds no speed ahead: no rise.
            Assert.AreEqual(0f, TickAt(boat, rb, -1f, Vector2.zero), Tol);
        }

        [Test]
        public void ATideUnderHer_DoesNotTrimHer()
        {
            var boat = Boat(Hull(), out var rb);
            _sea.Current = new Vector2(0f, 2f);
            // She drifts with a 2 m/s stream along her keel: no way through the water, so no drag, no
            // rise, and the body's own damping (−0.4 m/s²) is a dip weighted by no way at all.
            Assert.AreEqual(0f, TickAt(boat, rb, 0f, new Vector2(0f, 2f)), Tol);
        }

        [Test]
        public void HerTrim_IsReadFromHerDriveAndDrag_NotFromTheWind()
        {
            var boat = Boat(Hull(), out var rb);
            // Half ahead at 3 m/s: 6 of thrust, 3 of drag, over 10, less 0.2 × 3 = −0.3 m/s², so she
            // is slowing to the 2 m/s her half throttle holds (6 / 3). The rise is read there: Fn =
            // 2 / √(9.81 × 4.5) = 0.30102, x = Fn / 0.40 = 0.75254, x²(3 − 2x) = 0.84660°. The dip at
            // the 3 m/s she carries (Fn 0.45, over her hump): 1.5 × −0.3 = −0.45°. Sum 0.39660°.
            float calm = TickAt(boat, rb, 0.5f, new Vector2(0f, 3f));
            Assert.AreEqual(0.39660f, calm, Tol);

            _sea.Wind = new Vector2(6f, -4f);   // shoves her, but it is not her drive or her drag
            float windy = TickAt(boat, rb, 0.5f, new Vector2(0f, 3f));
            Assert.AreEqual(calm, windy, "the wind is left out of the trim on purpose (§2.7.3)");
        }

        [Test]
        public void AHullThatAuthorsNoTrim_SitsExactlyLevel()
        {
            var boat = Boat(Hull(humpDegrees: 0f, accel: 0f, decel: 0f), out var rb);
            Assert.AreEqual(0f, TickAt(boat, rb, 1f, Vector2.zero));
            Assert.AreEqual(0f, TickAt(boat, rb, 0f, new Vector2(0f, 4f)));
        }

        [Test]
        public void HerLag_IsHerOwn_ElseThePolicys()
        {
            var boat = Boat(Hull(responseSeconds: 0.4f), out var rb);
            TickAt(boat, rb, 1f, Vector2.zero);
            Assert.AreEqual(0.4f, boat.TrimResponseSeconds, Tol);

            boat.SetHull(Hull(responseSeconds: 0f));
            TickAt(boat, rb, 1f, Vector2.zero);
            Assert.AreEqual(0.9f, boat.TrimResponseSeconds, Tol, "a hull that leaves it at 0 takes the policy's");
        }

        // ------------------------------------------------------------------ the physics never reads it

        [Test]
        public void HerPhysics_IsIdentical_WithTrimOnOrOff()
        {
            var boat = Boat(Hull(), out var rb);
            boat.transform.rotation = Quaternion.Euler(0f, 0f, 30f);
            Vector2 fwd = boat.transform.up;
            _sea.Wind = new Vector2(6f, -4f);
            _sea.Current = new Vector2(0.5f, 0.25f);

            (Vector2 force, float torque, float trim) Tick(bool trimOn)
            {
                _config.HullTrim.Enabled = trimOn;
                boat.SetControl(1f, 0.5f);   // full ahead with the helm over: thrust, drag, wind, rudder
                rb.linearVelocity = new Vector2(1.5f, 2.5f);
                rb.angularVelocity = 0f;
                rb.totalForce = Vector2.zero;
                rb.totalTorque = 0f;
                boat.TickUnmannedDrift();
                return (rb.totalForce, rb.totalTorque, boat.TrimTargetDegrees);
            }

            var on = Tick(true);
            var off = Tick(false);

            // Positive controls: the tick really put its forces on the body, and trim really was on.
            Assert.Greater(Vector2.Dot(on.force, fwd), 0f, "full ahead pushes her along her keel");
            Assert.AreNotEqual(0f, on.torque, "the helm is over with way on: the rudder turns her");
            Assert.AreNotEqual(0f, on.trim, "with trim on she asks for a bow angle");
            Assert.AreEqual(0f, off.trim, "with trim off she asks for none");

            Assert.AreEqual(on.force.x, off.force.x, "the force on her is the same to the bit");
            Assert.AreEqual(on.force.y, off.force.y, "the force on her is the same to the bit");
            Assert.AreEqual(on.torque, off.torque, "the torque on her is the same to the bit");
        }

        // ------------------------------------------------------------------ a stop, a swap, the helm left

        [Test]
        public void AStopOrAHullSwap_PutsHerLevelAtOnce()
        {
            var boat = Boat(Hull(), out var rb);
            Assert.AreEqual(1.8f, TickAt(boat, rb, 1f, Vector2.zero), Tol, "precondition: she squats");
            int serial = boat.TrimRestSerial;

            boat.Stop();
            Assert.AreEqual(0f, boat.TrimTargetDegrees, "a stop (and a teleport) asks for level");
            Assert.AreEqual(serial + 1, boat.TrimRestSerial, "…and tells the drawer to be there at once");

            Assert.AreEqual(1.8f, TickAt(boat, rb, 1f, Vector2.zero), Tol, "precondition: she squats again");
            boat.SetHull(Hull());
            Assert.AreEqual(0f, boat.TrimTargetDegrees, "a new hull (a swap, a load) starts level");
            Assert.AreEqual(serial + 2, boat.TrimRestSerial, "…at once, not eased from the old hull's bow");
        }

        [Test]
        public void LeavingTheHelm_AsksForLevel()
        {
            var boat = Boat(Hull(), out var rb);
            Assert.AreEqual(1.8f, TickAt(boat, rb, 1f, Vector2.zero), Tol, "precondition: she squats");

            // EditMode does not call OnDisable when a component is disabled; call it as Unity would.
            MethodInfo onDisable = typeof(BoatController).GetMethod(
                "OnDisable", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(onDisable, "BoatController has an OnDisable");
            onDisable.Invoke(boat, null);

            Assert.AreEqual(0f, boat.TrimTargetDegrees,
                "the squat goes with the hand on the throttle; the drift tick republishes her target");
        }

        [Test]
        public void LettingGoTheHelmUnderWay_AsksForLevel_WithoutPuttingHerThereAtOnce()
        {
            var boat = Boat(Hull(), out var rb);
            Assert.AreEqual(1.8f, TickAt(boat, rb, 1f, Vector2.zero), Tol, "precondition: she squats");
            rb.linearVelocity = new Vector2(0f, 3f);
            Assert.AreEqual(new Vector2(0f, 3f), rb.linearVelocity, "precondition: she has way on");
            int serial = boat.TrimRestSerial;

            boat.Stop(levelAtOnce: false);   // what ControlSwitcher.LeaveHelm calls

            Assert.AreEqual(0f, boat.TrimTargetDegrees, "the helm let go asks for level");
            Assert.AreEqual(serial, boat.TrimRestSerial,
                "…but does not tell the drawer to be there at once: the bow she carried settles on her lag");
            Assert.AreEqual(Vector2.zero, rb.linearVelocity, "the physics is the same stop: her way goes");
            Assert.AreEqual(0f, boat.Throttle, "…and the hand comes off the throttle");

            boat.TickUnmannedDrift();
            Assert.AreEqual(0f, boat.TrimTargetDegrees, Tol,
                "her drift tick asks for level too: the squat went with the throttle");
        }

        // ------------------------------------------------------------------ the shipped data

        [Test]
        public void TheConfigAsset_SerializesEveryTrimKey_AndSwitchesItOn()
        {
            // The #420 trap: a key the YAML omits deserializes as 0, not as HullTrimSettings.Default,
            // and a GameConfig with no block at all reads the switch OFF.
            string path = Path.Combine(Application.dataPath, "..", ConfigAssetPath);
            Assert.IsTrue(File.Exists(path), $"missing: {ConfigAssetPath}");
            var block = Regex.Match(File.ReadAllText(path), @"^  HullTrim:\r?\n((?:    .*\r?\n)+)",
                                    RegexOptions.Multiline);
            Assert.IsTrue(block.Success, "GameConfig.asset does not serialize a HullTrim block at all");

            float Value(string key)
            {
                var m = Regex.Match(block.Groups[1].Value, $@"^\s*{key}:\s*(\S+)\s*$", RegexOptions.Multiline);
                Assert.IsTrue(m.Success, $"GameConfig.asset's HullTrim block does not list {key}");
                Assert.IsTrue(float.TryParse(m.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture,
                                             out float v), $"{key} is not a number: '{m.Groups[1].Value}'");
                return v;
            }

            Assert.AreEqual(1f, Value(nameof(HullTrimSettings.Enabled)), "the shipped switch is ON");
            float hump = Value(nameof(HullTrimSettings.HumpFroude));
            float planing = Value(nameof(HullTrimSettings.PlaningFroude));
            Assert.Greater(hump, 0f);
            Assert.Greater(planing, hump, "the planing settle sits above the hump");
            Assert.Greater(Value(nameof(HullTrimSettings.DefaultMaxBowUpDegrees)), 0f);
            Assert.Greater(Value(nameof(HullTrimSettings.DefaultMaxBowDownDegrees)), 0f);
            Assert.Greater(Value(nameof(HullTrimSettings.DefaultResponseSeconds)), 0f);
        }

        [Test]
        public void EveryHullAtTheHelm_TrimsOnHerMesh_OrIsLevelOnPurpose()
        {
            var roster = new HashSet<string>(StPetersBuilder.DevPickerRosterFiles);
            foreach (string ship in DeliberatelyLevel)
                Assert.IsTrue(roster.Contains(ship), $"{ship} is on the zero list but not at the helm");

            var level = new HashSet<string>(DeliberatelyLevel);
            foreach (string file in StPetersBuilder.DevPickerRosterFiles)
            {
                var hull = AssetDatabase.LoadAssetAtPath<BoatHullDef>($"{DataBoats}/{file}.asset");
                Assert.IsNotNull(hull, $"{file}: the helm's roster names a hull that does not load");
                Assert.LessOrEqual(hull.TrimPlaningDropDegrees, hull.TrimHumpDegrees,
                    $"{file}: a planing hull gives back no more than her rise");

                if (level.Contains(file))
                {
                    Assert.AreEqual(0f, hull.TrimHumpDegrees, $"{file}: a ship's trim is a deliberate 0");
                    Assert.AreEqual(0f, hull.TrimPlaningDropDegrees, $"{file}: a ship's trim is a deliberate 0");
                    Assert.AreEqual(0f, hull.TrimAccelDegreesPerMps2, $"{file}: a ship's trim is a deliberate 0");
                    Assert.AreEqual(0f, hull.TrimDecelDegreesPerMps2, $"{file}: a ship's trim is a deliberate 0");
                    Assert.AreEqual(0f, hull.TrimMaxBowUpDegrees, $"{file}: a ship's trim is a deliberate 0");
                    Assert.AreEqual(0f, hull.TrimMaxBowDownDegrees, $"{file}: a ship's trim is a deliberate 0");
                    Assert.AreEqual(0f, hull.TrimResponseSeconds, $"{file}: a ship's trim is a deliberate 0");
                    continue;
                }

                Assert.Greater(hull.TrimHumpDegrees, 0f, $"{file}: a working hull rises to her hump");
                Assert.Greater(hull.TrimDecelDegreesPerMps2, 0f, $"{file}: a working hull dips on a cut");
                // Only a mesh can draw a bow rising; a sprite would have to be moved whole, which
                // fakes the pitch (the charter forbids it).
                Assert.IsTrue(hull.Visual != null, $"{file} trims, so she must have a visual to draw it on");
                Assert.AreEqual(BoatHullVariant.Mesh, hull.Visual.Variant, $"{file} trims, so she draws as a mesh");
            }
        }
    }
}
