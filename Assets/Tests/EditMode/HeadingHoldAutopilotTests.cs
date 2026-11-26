using System;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using HiddenHarbours.Player;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The autopilot's heading hold (first slice): the controller holds a heading in a simulated fixed
    /// step loop, the same inputs give the same bits, the rules that hand the helm back, the steer
    /// override, the switch's binding, and that a hull whose Def carries no autopilot is untouched.
    ///
    /// <para><b>What the step loop is and is not.</b> It is a one-axis yaw model built from the
    /// game's own rudder law (<see cref="BoatController.RudderTorque"/>, way-scaled), the controller's
    /// rudder feel scale, and Rigidbody2D's angular damping as BoatController sets it (2.5/s, applied
    /// the way Box2D applies it). The yaw inertia of a real hull comes from her collider, which EditMode
    /// cannot measure, so the hold is proved across a 40x spread of inertia instead. A running game is
    /// the only proof of the feel.</para>
    /// </summary>
    public class HeadingHoldAutopilotTests
    {
        // BoatController.RudderFeelScale (private) and the angular damping its Awake sets.
        private const float RudderFeelScale = 0.01f;
        private const float AngularDamping = 2.5f;
        private const float FixedDt = 0.02f;   // the project's fixed step (50 Hz)
        private const float RudderAuthority = 600f;   // BoatHullDef's default
        private const float WayMs = 3f;               // under way, past the rudder's full-authority way

        private static HelmAutopilotSettings Cfg => HelmAutopilotSettings.Default;

        private static AutopilotSensors Sensors(bool fitted = true, bool manned = true, bool aground = false,
                                                bool collided = false, float heading = 90f, float rate = 0f,
                                                float way = WayMs)
            => new AutopilotSensors(fitted, manned, aground, collided, heading, rate, way);

        /// <summary>Run a hold from a kicked heading under a steady yaw disturbance (a fraction of full
        /// rudder torque, the beam sea or wind) and a slow swell; return the worst error over the last
        /// <paramref name="tailSeconds"/> and the helm trace.</summary>
        private static float SimulateHold(float inertia, float kickDegrees, float disturbanceFraction,
                                          float seconds, float tailSeconds, out float[] helmTrace)
        {
            var ap = new HeadingHoldAutopilot();
            const float target = 90f;
            Assert.IsTrue(ap.TryEngage(Sensors(heading: target), 0f, Cfg), "premise: it engages under way");

            float heading = target + kickDegrees;
            float rate = 0f;
            int steps = Mathf.RoundToInt(seconds / FixedDt);
            helmTrace = new float[steps];
            float worstTail = 0f;
            for (int i = 0; i < steps; i++)
            {
                float t = i * FixedDt;
                Assert.IsTrue(ap.Tick(Sensors(heading: heading, rate: rate), FixedDt, Cfg, out float helm),
                    $"the hold let go at t={t:F2}s with nothing to hand it back");
                helmTrace[i] = helm;

                float disturbance = (disturbanceFraction + 0.05f * Mathf.Sin(t * 0.8f)) * RudderAuthority;
                float torque = (BoatController.RudderTorque(helm, RudderAuthority, WayMs) + disturbance)
                               * RudderFeelScale;
                float alphaDeg = torque / inertia * Mathf.Rad2Deg;
                rate = (rate + alphaDeg * FixedDt) / (1f + FixedDt * AngularDamping);
                heading += rate * FixedDt;

                if (t >= seconds - tailSeconds)
                    worstTail = Mathf.Max(worstTail, Mathf.Abs(Mathf.DeltaAngle(target, heading)));
            }
            return worstTail;
        }

        // =============================================================================================
        //  the hold
        // =============================================================================================

        [TestCase(5f, 15f)]
        [TestCase(5f, -15f)]
        [TestCase(40f, 15f)]
        [TestCase(40f, -15f)]
        [TestCase(200f, 15f)]
        [TestCase(200f, -15f)]
        public void HoldsTheHeadingWithinTolerance_InAFixedStepLoop(float inertia, float kick)
        {
            const float tolerance = 2f;
            float worst = SimulateHold(inertia, kick, disturbanceFraction: 0.2f, seconds: 120f,
                                       tailSeconds: 30f, out float[] helm);
            Assert.Less(worst, tolerance,
                $"inertia {inertia}: kicked {kick}° under a steady 20% yaw disturbance and a swell, the hold " +
                $"was still {worst:F2}° off over the last 30 s (tolerance {tolerance}°).");
            Assert.IsTrue(helm.All(h => Mathf.Abs(h) <= Cfg.MaxHelm + 1e-6f),
                "the autopilot used more helm than MaxHelm allows.");
        }

        [Test]
        public void TheSameInputsGiveTheSameBits()
        {
            SimulateHold(40f, 15f, 0.2f, 60f, 10f, out float[] a);
            SimulateHold(40f, 15f, 0.2f, 60f, 10f, out float[] b);
            Assert.AreEqual(a.Length, b.Length);
            for (int i = 0; i < a.Length; i++)
                Assert.AreEqual(BitConverter.SingleToInt32Bits(a[i]), BitConverter.SingleToInt32Bits(b[i]),
                    $"step {i}: the controller is not a pure function of its inputs.");
        }

        [Test]
        public void HeadingLeftOfTheTarget_AsksForStarboardHelm_AndTheSwingIsDamped()
        {
            var cfg = Cfg;
            cfg.HelmSlewPerSecond = 0f;          // look at the law, not the slew
            cfg.IntegralGainPerDegreeSecond = 0f;
            float integral = 0f;

            float left = HeadingHoldAutopilot.Step(90f, 100f, 0f, FixedDt, cfg, ref integral, 0f);
            Assert.Greater(left, 0f, "10° counter-clockwise of the target (to port) wants positive (starboard) helm.");
            float right = HeadingHoldAutopilot.Step(90f, 80f, 0f, FixedDt, cfg, ref integral, 0f);
            Assert.Less(right, 0f, "10° to starboard wants port helm.");

            float onCourseSwingingLeft = HeadingHoldAutopilot.Step(90f, 90f, 10f, FixedDt, cfg, ref integral, 0f);
            Assert.Greater(onCourseSwingingLeft, 0f, "on course but swinging to port: the damping checks it.");
        }

        [Test]
        public void TheErrorWrapsAcrossOneEighty()
        {
            var cfg = Cfg;
            cfg.HelmSlewPerSecond = 0f;
            cfg.IntegralGainPerDegreeSecond = 0f;
            float integral = 0f;
            // Holding 179°, she is at −179° (= 181°): 2° to port, not 358° to starboard.
            float helm = HeadingHoldAutopilot.Step(179f, -179f, 0f, FixedDt, cfg, ref integral, 0f);
            Assert.AreEqual(cfg.HeadingGainPerDegree * 2f, helm, 1e-5f);
        }

        [Test]
        public void TheHelmIsClampedAndSlewed_FromTheHelmItTookOver()
        {
            var ap = new HeadingHoldAutopilot();
            Assert.IsTrue(ap.TryEngage(Sensors(heading: 90f), currentHelm: -0.5f, Cfg));
            Assert.AreEqual(-0.5f, ap.Helm, "it takes over at the helm she had (no jerk on the wheel).");

            Assert.IsTrue(ap.Tick(Sensors(heading: 150f), FixedDt, Cfg, out float helm));
            Assert.AreEqual(-0.5f + Cfg.HelmSlewPerSecond * FixedDt, helm, 1e-5f,
                "60° off it winds toward the limit at the slew rate, not in one step.");

            for (int i = 0; i < 200; i++) ap.Tick(Sensors(heading: 150f), FixedDt, Cfg, out helm);
            Assert.AreEqual(Cfg.MaxHelm, helm, 1e-5f, "and never past MaxHelm.");
        }

        // =============================================================================================
        //  the rules that hand the helm back
        // =============================================================================================

        [Test]
        public void TheHandBackRules_InOrder()
        {
            Assert.AreEqual(AutopilotDisengage.None, HeadingHoldAutopilot.HandBackReason(Sensors(), Cfg));
            Assert.AreEqual(AutopilotDisengage.NotFitted, HeadingHoldAutopilot.HandBackReason(Sensors(fitted: false), Cfg));
            Assert.AreEqual(AutopilotDisengage.LeftHelm, HeadingHoldAutopilot.HandBackReason(Sensors(manned: false), Cfg));
            Assert.AreEqual(AutopilotDisengage.Aground, HeadingHoldAutopilot.HandBackReason(Sensors(aground: true), Cfg));
            Assert.AreEqual(AutopilotDisengage.Collision, HeadingHoldAutopilot.HandBackReason(Sensors(collided: true), Cfg));
            Assert.AreEqual(AutopilotDisengage.NoWay, HeadingHoldAutopilot.HandBackReason(Sensors(way: 0f), Cfg));
            Assert.AreEqual(AutopilotDisengage.NoWay,
                HeadingHoldAutopilot.HandBackReason(Sensors(way: -2f), Cfg), "going astern is no way ahead.");
            Assert.AreEqual(AutopilotDisengage.NoWay,
                HeadingHoldAutopilot.HandBackReason(Sensors(way: Cfg.MinWayMetresPerSecond * 0.99f), Cfg));
            Assert.AreEqual(AutopilotDisengage.None,
                HeadingHoldAutopilot.HandBackReason(Sensors(way: Cfg.MinWayMetresPerSecond), Cfg));
        }

        [TestCase(false, true, false, 3f, AutopilotDisengage.NotFitted)]
        [TestCase(true, false, false, 3f, AutopilotDisengage.LeftHelm)]
        [TestCase(true, true, true, 3f, AutopilotDisengage.Aground)]
        [TestCase(true, true, false, 0f, AutopilotDisengage.NoWay)]
        public void ItWillNotEngage_WhereItWouldHandStraightBack(bool fitted, bool manned, bool aground, float way,
                                                                  AutopilotDisengage expected)
        {
            var ap = new HeadingHoldAutopilot();
            Assert.IsFalse(ap.TryEngage(Sensors(fitted, manned, aground, way: way), 0.1f, Cfg));
            Assert.IsFalse(ap.Engaged);
            Assert.AreEqual(expected, ap.LastDisengage);
            Assert.IsFalse(ap.Tick(Sensors(), FixedDt, Cfg, out _), "off, it writes nothing.");
        }

        [Test]
        public void AKnockBeforeThePress_IsNotAReasonToRefuse()
        {
            var ap = new HeadingHoldAutopilot();
            Assert.IsTrue(ap.TryEngage(Sensors(collided: true), 0f, Cfg));
        }

        [TestCase(false, true, false, false, 3f, AutopilotDisengage.NotFitted)]
        [TestCase(true, false, false, false, 3f, AutopilotDisengage.LeftHelm)]
        [TestCase(true, true, true, false, 3f, AutopilotDisengage.Aground)]
        [TestCase(true, true, false, true, 3f, AutopilotDisengage.Collision)]
        [TestCase(true, true, false, false, 0.1f, AutopilotDisengage.NoWay)]
        public void EachHazard_HandsTheHelmBack_OnTheStepItAppears(bool fitted, bool manned, bool aground,
                                                                   bool collided, float way, AutopilotDisengage expected)
        {
            var ap = new HeadingHoldAutopilot();
            Assert.IsTrue(ap.TryEngage(Sensors(), 0f, Cfg));
            Assert.IsTrue(ap.Tick(Sensors(), FixedDt, Cfg, out _), "premise: it holds with nothing wrong.");

            Assert.IsFalse(ap.Tick(Sensors(fitted, manned, aground, collided, way: way), FixedDt, Cfg, out float helm));
            Assert.AreEqual(0f, helm, "a step that hands back writes no helm.");
            Assert.IsFalse(ap.Engaged);
            Assert.AreEqual(expected, ap.LastDisengage);
            Assert.IsFalse(ap.Tick(Sensors(), FixedDt, Cfg, out _), "and it stays off once the hazard is gone.");
        }

        [Test]
        public void TheSwitchFlicksOff_AndTheFirstReasonStands()
        {
            var ap = new HeadingHoldAutopilot();
            Assert.IsTrue(ap.TryEngage(Sensors(), 0f, Cfg));
            ap.Disengage(AutopilotDisengage.Toggled);
            ap.Disengage(AutopilotDisengage.LeftHelm);
            Assert.AreEqual(AutopilotDisengage.Toggled, ap.LastDisengage);
        }

        [Test]
        public void ImpactsBelowTheFloorAreFenderTouches()
        {
            Assert.IsFalse(HeadingHoldAutopilot.IsHazardousImpact(Cfg.CollisionMinImpactMetresPerSecond * 0.5f, Cfg));
            Assert.IsTrue(HeadingHoldAutopilot.IsHazardousImpact(Cfg.CollisionMinImpactMetresPerSecond, Cfg));
        }

        // =============================================================================================
        //  the steer override (the helm's key read)
        // =============================================================================================

        [Test]
        public void ASteerInputTakesTheHelmBack_AStickJitterDoesNot()
        {
            float ap = 0.37f;
            Assert.AreEqual(0f, DevBoatInput.ArbitrateAutopilotSteer(0f, false, ap, Cfg, out bool back));
            Assert.IsFalse(back, "off, nothing to take back.");

            Assert.AreEqual(0.5f, DevBoatInput.ArbitrateAutopilotSteer(0.5f, false, ap, Cfg, out back),
                "off, the helm's own read goes through.");
            Assert.IsFalse(back);

            Assert.AreEqual(ap, DevBoatInput.ArbitrateAutopilotSteer(0f, true, ap, Cfg, out back),
                "on, no input passes the autopilot's helm through: the key layer never fights it.");
            Assert.IsFalse(back);

            float jitter = Cfg.SteerOverrideThreshold * 0.5f;
            Assert.AreEqual(ap, DevBoatInput.ArbitrateAutopilotSteer(jitter, true, ap, Cfg, out back),
                "a resting stick's jitter is not a hand on the helm.");
            Assert.IsFalse(back);

            Assert.AreEqual(-1f, DevBoatInput.ArbitrateAutopilotSteer(-1f, true, ap, Cfg, out back),
                "a steer key takes the helm back and steers at once.");
            Assert.IsTrue(back);
        }

        // =============================================================================================
        //  the data flag: off by default, and a hull without it is untouched
        // =============================================================================================

        private readonly System.Collections.Generic.List<Object> _made = new System.Collections.Generic.List<Object>();
        private GameObject _go;

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
            foreach (Object o in _made)
                if (o != null) Object.DestroyImmediate(o);
            _made.Clear();
        }

        private BoatHullDef Hull(PropulsionType propulsion, bool hasAutopilot)
        {
            var hull = ScriptableObject.CreateInstance<BoatHullDef>();
            hull.Propulsion = propulsion;
            hull.AutopilotOffered = hasAutopilot;
            _made.Add(hull);
            return hull;
        }

        [Test]
        public void TheFlagIsOffByDefault_AndOnlyARudderHullWithItIsFitted()
        {
            var fresh = ScriptableObject.CreateInstance<BoatHullDef>();
            try { Assert.IsFalse(fresh.AutopilotOffered, "a hull carries no autopilot unless her Def says so."); }
            finally { Object.DestroyImmediate(fresh); }

            Assert.IsFalse(BoatController.AutopilotFitted(null));
            Assert.IsFalse(BoatController.AutopilotFitted(Hull(PropulsionType.Engine, false)), "no flag, no autopilot.");
            Assert.IsFalse(BoatController.AutopilotFitted(Hull(PropulsionType.Oars, true)), "oars have no rudder to hold.");
            Assert.IsTrue(BoatController.AutopilotFitted(Hull(PropulsionType.Engine, true)));
        }

        /// <summary>
        /// The OUTBOARD hulls of the shipped fleet. The owner's ruling (2026-11-16): the autopilot is
        /// offered on anything without an outboard. No hull field names the drive, so the drive is read
        /// from each hull's rig source (docs/art/rigs: the punts, skiffs, zodiacs, outboard dory and the
        /// tender hang an outboard; the bowrider, express cruiser and go-fast are sterndrives; the jet
        /// ski is a jet pump; everything else is an inboard or a sailing hull). A new hull fails the
        /// rule below until it is classified here or flagged.
        /// </summary>
        private static readonly string[] OutboardHullIds =
        {
            "boat.dory_outboard", "boat.punt", "boat.punt_upgraded", "boat.console_skiff",
            "boat.sport_skiff", "boat.sport_skiff_mk2", "boat.sport_skiff_twin",
            "boat.zodiac_frc", "boat.zodiac_hurricane", "boat.yacht_tender",
        };

        [Test]
        public void EveryHelmHullWithoutAnOutboard_OffersIt_AndNoOutboardHullDoes()
        {
            var hulls = UnityEditor.AssetDatabase.FindAssets("t:BoatHullDef", new[] { "Assets/_Project/Data/Boats" })
                .Select(g => UnityEditor.AssetDatabase.LoadAssetAtPath<BoatHullDef>(UnityEditor.AssetDatabase.GUIDToAssetPath(g)))
                .Where(h => h != null).OrderBy(h => h.Id).ToList();
            Assert.GreaterOrEqual(hulls.Count, 40, "the fleet's hull Defs did not load: wrong folder?");

            var ids = hulls.Select(h => h.Id).ToList();
            foreach (string id in OutboardHullIds)
                CollectionAssert.Contains(ids, id, $"{id} is listed as an outboard hull but no Def has that id.");

            foreach (BoatHullDef h in hulls)
            {
                bool outboard = OutboardHullIds.Contains(h.Id);
                bool helmHull = h.Propulsion != PropulsionType.Oars;   // the rowed dory has no rudder
                Assert.AreEqual(helmHull && !outboard, h.AutopilotOffered,
                    $"{h.Id}: offered on every helm hull without an outboard, never on an outboard or rowed one " +
                    $"(outboard {outboard}, propulsion {h.Propulsion}).");
                if (h.AutopilotOffered)
                    Assert.IsTrue(BoatController.AutopilotFitted(h),
                        $"{h.Id} offers the autopilot but has no rudder it can hold on (engine helm or usable sail plan).");

                // The drawn engine agrees: a helm hull whose art hangs an outboard must be on the list.
                if (helmHull && h.Visual != null && (h.Visual.HasMotor() || h.Visual.HasMotorMesh()))
                    Assert.IsTrue(outboard, $"{h.Id} draws an outboard but is not classified as one.");
            }
        }

        [Test]
        public void AHullWithoutTheFlag_TheSwitchDoesNothing_AndHerHelmIsUntouched()
        {
            _go = new GameObject("autopilot-unfitted");
            var boat = _go.AddComponent<BoatController>();
            boat.SetHull(Hull(PropulsionType.Engine, hasAutopilot: false));
            boat.SetControl(0.4f, 0.3f);

            Assert.IsFalse(boat.ToggleAutopilot(), "the switch must not engage on a hull without an autopilot.");
            Assert.IsFalse(boat.AutopilotEngaged);
            Assert.AreEqual(AutopilotDisengage.NotFitted, boat.AutopilotLastDisengage);
            Assert.AreEqual(0.3f, boat.Steer, "her helm is exactly as the player left it.");
            Assert.AreEqual(0.4f, boat.Throttle);
        }

        // =============================================================================================
        //  the switch: Helm/Autopilot in the bindings asset, through the intents seam
        // =============================================================================================

        [Test]
        public void TheSwitchIsP_OrThePadsY_InTheHelmMap_EachBoundOnce_UnderTheHudGates()
        {
            InputActionAsset asset = InputSystem.actions;
            Assert.IsNotNull(asset, "no project-wide actions asset is configured.");
            InputAction toggle = asset.FindActionMap(InputBindings.HelmMap, throwIfNotFound: true)
                                      .FindAction(DeviceAutopilotIntentSource.AutopilotAction, throwIfNotFound: false);
            Assert.IsNotNull(toggle, "the Helm map has no Autopilot action.");
            Assert.AreEqual(InputActionType.Button, toggle.type, "the switch is a press.");
            CollectionAssert.AreEqual(new[] { "<Keyboard>/p" },
                toggle.bindings.Where(b => b.groups == InputBindings.KeyboardMouseScheme).Select(b => b.path).ToArray(),
                "P on the keyboard.");
            CollectionAssert.AreEqual(new[] { "<Gamepad>/buttonNorth" },
                toggle.bindings.Where(b => b.groups == InputBindings.GamepadScheme).Select(b => b.path).ToArray(),
                "Y (north) on the pad: free at the helm, where A is the haul, B the neutral, the d-pad the detents " +
                "and the chooser, the left stick the steer, the shoulders the zoom and Start the shell.");
            // P and Y are bound twice in the whole asset (2026-11-26): once here, to the autopilot, and once
            // in the Camera map, to raising the camera. The camera answers only on foot and on a deck, the
            // autopilot only at the helm, so one press is never heard by both. Any third binding reds this.
            foreach (string path in new[] { "<Keyboard>/p", "<Gamepad>/buttonNorth" })
            {
                string[] where = asset.actionMaps
                    .SelectMany(m => m.bindings.Select(b => (map: m.name, binding: b)))
                    .Where(x => string.Equals(x.binding.path, path, StringComparison.OrdinalIgnoreCase))
                    .Select(x => x.map + "/" + x.binding.action)
                    .OrderBy(s => s, StringComparer.Ordinal)
                    .ToArray();
                CollectionAssert.AreEqual(new[] { "Camera/Raise", "Helm/Autopilot" }, where,
                    $"{path} is bound to the autopilot at the helm and to the camera ashore and on deck, nothing else.");
            }

            Assert.IsTrue(DeviceAutopilotIntentSource.Map(true, false, false).Toggle, "an open press arrives.");
            Assert.IsFalse(DeviceAutopilotIntentSource.Map(false, false, false).Toggle, "no press is no intent.");
            Assert.IsFalse(DeviceAutopilotIntentSource.Map(true, true, false).Toggle, "a stopped world takes it.");
            Assert.IsFalse(DeviceAutopilotIntentSource.Map(true, false, true).Toggle,
                "a text field that owns the keys takes it: a P in a waypoint name is a letter.");
        }

        [Test]
        public void TheHelmReadsTheAssetUntilHandedAnotherSource()
        {
            IControlIntentSource<AutopilotIntents> before = AutopilotInput.Device;
            _go = new GameObject("autopilot-switch");
            _go.AddComponent<BoatController>();
            var input = _go.AddComponent<DevBoatInput>();
            try
            {
                DeviceAutopilotIntentSource.Install();
                Assert.IsInstanceOf<DeviceAutopilotIntentSource>(AutopilotInput.Device);
                Assert.IsTrue(((DeviceAutopilotIntentSource)AutopilotInput.Device).IsBound,
                    "the installed source finds Helm/Autopilot.");
                Assert.AreSame(AutopilotInput.Device, input.AutopilotInputSource,
                    "with nothing configured the switch must read the bindings asset, not silence.");

                var held = new HeldAutopilotIntents();
                input.ConfigureAutopilotInput(held);
                Assert.AreSame(held, input.AutopilotInputSource);
                input.ConfigureAutopilotInput(null);
                Assert.AreSame(AutopilotInput.Device, input.AutopilotInputSource, "null did not restore the asset.");
            }
            finally
            {
                input.ConfigureAutopilotInput(null);
                AutopilotInput.InstallDevice(before);
            }
        }

        // =============================================================================================
        //  the Core signal: one AutopilotStateChanged per transition, none for a refusal or a repeat
        // =============================================================================================

        [Test]
        public void TheHoldAnnouncesEachTransition_AndNothingElse()
        {
            var ap = new HeadingHoldAutopilot();
            var seen = new System.Collections.Generic.List<(bool on, AutopilotDisengage why)>();
            ap.StateChanged += (on, why) => seen.Add((on, why));

            Assert.IsFalse(ap.TryEngage(Sensors(way: 0f), 0f, Cfg), "premise: refused at rest");
            CollectionAssert.IsEmpty(seen, "a refusal is not a transition: it was off and stays off.");

            Assert.IsTrue(ap.TryEngage(Sensors(), 0f, Cfg));
            ap.Tick(Sensors(), 0.02f, Cfg, out _);
            ap.Tick(Sensors(aground: true), 0.02f, Cfg, out _);
            ap.Disengage(AutopilotDisengage.Toggled);   // already off: a no-op, no second signal

            CollectionAssert.AreEqual(new[] { (true, AutopilotDisengage.None), (false, AutopilotDisengage.Aground) },
                seen, "on, then off for the rule that ended it; a steady hold and a repeat say nothing.");
        }

        [Test]
        public void TheBoatPublishesTheTransitionOnTheEventBus()
        {
            var got = new System.Collections.Generic.List<AutopilotStateChanged>();
            Action<AutopilotStateChanged> h = e => got.Add(e);
            EventBus.Subscribe(h);
            try
            {
                _go = new GameObject("autopilot-signal");
                var boat = _go.AddComponent<BoatController>();
                boat.SetHull(Hull(PropulsionType.Engine, hasAutopilot: true));
                boat.ToggleAutopilot();   // at rest, no body moving: refused, so nothing is published
                CollectionAssert.IsEmpty(got, "a refused press publishes nothing.");

                var rb = _go.GetComponent<Rigidbody2D>();
                rb.linearVelocity = (Vector2)_go.transform.up * 2f;   // way ahead, well over the floor
                Assert.IsTrue(boat.ToggleAutopilot(), "premise: under way it engages");
                boat.DisengageAutopilot(AutopilotDisengage.SteerInput);
                boat.DisengageAutopilot(AutopilotDisengage.Toggled);   // already off
                Assert.AreEqual(2, got.Count, "one signal per transition: " +
                    string.Join(", ", got.Select(e => e.Engaged + "/" + e.Reason)));
                Assert.IsTrue(got[0].Engaged); Assert.AreEqual(AutopilotDisengage.None, got[0].Reason);
                Assert.AreSame(boat, got[0].Boat, "the signal names the boat it is about.");
                Assert.IsFalse(got[1].Engaged); Assert.AreEqual(AutopilotDisengage.SteerInput, got[1].Reason);

                got.Clear();
                boat.SetHull(Hull(PropulsionType.Engine, hasAutopilot: false));
                CollectionAssert.IsEmpty(got, "a hull change while off publishes nothing.");
                Assert.AreEqual(nameof(AutopilotDisengage), typeof(AutopilotStateChanged)
                    .GetField(nameof(AutopilotStateChanged.Reason)).FieldType.Name);
                Assert.AreEqual("HiddenHarbours.Core", typeof(AutopilotDisengage).Namespace,
                    "the reason lives in Core, so UI and audio read it without the Boats module.");
            }
            finally { EventBus.Unsubscribe(h); }
        }
    }
}
