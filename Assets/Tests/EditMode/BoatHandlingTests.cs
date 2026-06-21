using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Boats;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Boat handling — astern/reverse, the per-oar rowing input mapping (the owner's table), the
    /// differential-rowing physics (per-oar thrust → yaw, brace drag), and the hull collider. The thrust,
    /// yaw, and combo-mapping logic are pure static helpers, so they're tested without the physics/input
    /// loop; the collider existence is asserted on a freshly-built BoatController (RequireComponent).
    /// </summary>
    public class BoatHandlingTests
    {
        private readonly List<Object> _spawned = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _spawned)
                if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        // ---- Part 1: ASTERN / REVERSE -------------------------------------------------------

        [Test]
        public void EngineThrust_Ahead_IsFullPower()
        {
            Assert.AreEqual(1200f, BoatController.EngineThrust(1f, 1200f, 0.4f), 1e-3f, "full ahead = full engine power");
            Assert.AreEqual(600f, BoatController.EngineThrust(0.5f, 1200f, 0.4f), 1e-3f, "half ahead = half power");
            Assert.AreEqual(0f, BoatController.EngineThrust(0f, 1200f, 0.4f), 1e-3f, "no throttle = no thrust");
        }

        [Test]
        public void EngineThrust_Astern_IsNegative_AndWeakerThanAhead()
        {
            float ahead  = BoatController.EngineThrust(1f, 1200f, 0.4f);
            float astern = BoatController.EngineThrust(-1f, 1200f, 0.4f);

            Assert.Less(astern, 0f, "astern thrust pushes backward (negative)");
            Assert.Less(Mathf.Abs(astern), Mathf.Abs(ahead), "astern must be weaker than ahead, like a real prop");
            Assert.AreEqual(-480f, astern, 1e-3f, "astern = -(power * asternFactor) = -(1200 * 0.4)");
            Assert.AreEqual(0.4f, Mathf.Abs(astern) / Mathf.Abs(ahead), 1e-3f, "astern is ~40% of ahead at full reverse");
        }

        [Test]
        public void EngineThrust_ClampsThrottleToRange()
        {
            Assert.AreEqual(1200f, BoatController.EngineThrust(2f, 1200f, 0.4f), 1e-3f, "throttle clamps to +1");
            Assert.AreEqual(-480f, BoatController.EngineThrust(-2f, 1200f, 0.4f), 1e-3f, "throttle clamps to -1");
        }

        // ---- Part 2: PER-OAR INPUT MAPPING (the owner's rowing table) -----------------------

        // (left, right) per-oar state expected for a key combo. forward +1 / back -1 / idle 0.
        static void AssertOar(string combo, (float left, float right) got, float eLeft, float eRight)
        {
            Assert.AreEqual(eLeft,  got.left,  1e-4f, $"{combo}: port oar");
            Assert.AreEqual(eRight, got.right, 1e-4f, $"{combo}: starboard oar");
        }

        [Test]
        public void OarMapping_MatchesTheOwnerTable()
        {
            //                                            ahead  astern port   stbd        port  stbd
            AssertOar("W",   DevBoatInput.OarStateFor(true,  false, false, false),  1f,  1f);
            AssertOar("S",   DevBoatInput.OarStateFor(false, true,  false, false), -1f, -1f);
            AssertOar("A",   DevBoatInput.OarStateFor(false, false, true,  false),  1f, -1f);
            AssertOar("D",   DevBoatInput.OarStateFor(false, false, false, true ), -1f,  1f);
            AssertOar("W+A", DevBoatInput.OarStateFor(true,  false, true,  false),  1f,  0f);
            AssertOar("W+D", DevBoatInput.OarStateFor(true,  false, false, true ),  0f,  1f);
            AssertOar("S+A", DevBoatInput.OarStateFor(false, true,  true,  false), -1f,  0f);
            AssertOar("S+D", DevBoatInput.OarStateFor(false, true,  false, true ),  0f, -1f);
        }

        [Test]
        public void OarMapping_NeutralAndCancellingCombos_AreIdle()
        {
            AssertOar("(none)", DevBoatInput.OarStateFor(false, false, false, false), 0f, 0f);
            AssertOar("A+D",    DevBoatInput.OarStateFor(false, false, true,  true ), 0f, 0f);  // opposing oar keys cancel
            AssertOar("W+S",    DevBoatInput.OarStateFor(true,  true,  false, false), 0f, 0f);  // ahead + astern cancel
        }

        [Test]
        public void OarMapping_FeedsThePhysicsForCorrectYaw()
        {
            // The mapped per-oar state, run through the (unchanged) #26 yaw, must turn the right way.
            var a = DevBoatInput.OarStateFor(false, false, true, false);   // A: stationary pivot
            Assert.Less(BoatController.OarYawTorque(a.left, a.right, P, O), 0f, "A spins the bow to starboard (right)");

            var d = DevBoatInput.OarStateFor(false, false, false, true);   // D: stationary pivot
            Assert.Greater(BoatController.OarYawTorque(d.left, d.right, P, O), 0f, "D spins the bow to port (left)");

            var wa = DevBoatInput.OarStateFor(true, false, true, false);   // W+A: port oar only, ahead
            Assert.Less(BoatController.OarYawTorque(wa.left, wa.right, P, O), 0f, "rowing only the port oar swings the bow starboard");
            Assert.Greater(BoatController.OarThrust(wa.left, wa.right, P), 0f, "…while still making headway");
        }

        // ---- Part 3: COZY COLLISION (the boat has a collider) ------------------------------

        [Test]
        public void BoatController_HasAHullCollider()
        {
            var go = new GameObject("Boat");
            _spawned.Add(go);
            go.AddComponent<BoatController>();   // RequireComponent auto-adds Rigidbody2D + CapsuleCollider2D

            var col = go.GetComponent<Collider2D>();
            Assert.IsNotNull(col, "the boat must have a Collider2D so it bumps the shore + dock, not sails through");
            Assert.IsFalse(col.isTrigger, "the hull collider is a solid bump, not a trigger");
        }

        // ---- Part 4: DIFFERENTIAL HAND-ROWING (the dory) -----------------------------------

        const float P = 300f;    // oar power (design units)
        const float O = 0.6f;    // oar lateral offset (m) — the yaw moment arm

        [Test]
        public void Oar_LeftOnlyStroke_GivesForwardThrust_AndYawsToStarboard()
        {
            // Negative yaw torque = clockwise = bow swings right (starboard), matching the steer convention.
            Assert.Greater(BoatController.OarThrust(1f, 0f, P), 0f, "a left-oar pull still drives the boat forward");
            Assert.Less(BoatController.OarYawTorque(1f, 0f, P, O), 0f, "left oar forward → bow yaws right (starboard)");
        }

        [Test]
        public void Oar_RightOnlyStroke_YawsToPort()
        {
            Assert.Greater(BoatController.OarThrust(0f, 1f, P), 0f, "a right-oar pull also drives forward");
            Assert.Greater(BoatController.OarYawTorque(0f, 1f, P, O), 0f, "right oar forward → bow yaws left (port)");
        }

        [Test]
        public void Oar_BothOars_GiveStraightThrust_NoNetYaw()
        {
            Assert.AreEqual(2f * P, BoatController.OarThrust(1f, 1f, P), 1e-3f, "both oars = twice a single oar, full ahead");
            Assert.AreEqual(0f, BoatController.OarYawTorque(1f, 1f, P, O), 1e-3f, "both oars equal → no net yaw (tracks straight)");
        }

        [Test]
        public void Oar_Astern_IsNegativeThrust()
        {
            Assert.Less(BoatController.OarThrust(-1f, -1f, P), 0f, "both oars back-watering = astern (negative thrust)");
            Assert.AreEqual(-2f * P, BoatController.OarThrust(-1f, -1f, P), 1e-3f);
            Assert.AreEqual(0f, BoatController.OarYawTorque(-1f, -1f, P, O), 1e-3f, "backing both oars still tracks straight");
        }

        [Test]
        public void Oar_Brace_AddsDragOpposingMotion()
        {
            var through = new Vector2(0f, 2f);                       // making way ahead
            Vector2 drag = BoatController.BraceDragForce(through, 400f);

            Assert.Less(Vector2.Dot(drag, through), 0f, "brace drag opposes motion through the water (a brake)");
            Assert.Greater(drag.magnitude, 0f, "bracing adds real drag");
            Assert.AreEqual(0f, BoatController.BraceDragForce(through, 0f).magnitude, 1e-4f, "no brake force with zero brace drag");
            Assert.Greater(BoatController.BraceDragForce(through, 800f).magnitude,
                           BoatController.BraceDragForce(through, 400f).magnitude, "stronger brace = more drag");
        }

        [Test]
        public void Oar_SetOarInput_StoresClampedPerOarState()
        {
            var go = new GameObject("RowBoat");
            _spawned.Add(go);
            var boat = go.AddComponent<BoatController>();

            boat.SetOarInput(1f, 1f, false);
            Assert.AreEqual(1f, boat.LeftOar, 1e-4f, "both oars ahead → port forward");
            Assert.AreEqual(1f, boat.RightOar, 1e-4f, "both oars ahead → starboard forward");
            boat.SetOarInput(1f, -1f, false);
            Assert.AreEqual(1f, boat.LeftOar, 1e-4f, "port forward");
            Assert.AreEqual(-1f, boat.RightOar, 1e-4f, "starboard back-water (a pivot)");
            boat.SetOarInput(2f, -2f, false);
            Assert.AreEqual(1f, boat.LeftOar, 1e-4f, "oar input clamps to +1");
            Assert.AreEqual(-1f, boat.RightOar, 1e-4f, "oar input clamps to -1");
        }

        [Test]
        public void Propulsion_DefaultsToOars_AndEngineIsSelectable()
        {
            var hull = ScriptableObject.CreateInstance<BoatHullDef>();
            _spawned.Add(hull);
            Assert.AreEqual(PropulsionType.Oars, hull.Propulsion, "the default hull (the dory template) is hand-rowed");
            hull.Propulsion = PropulsionType.Engine;
            Assert.AreEqual(PropulsionType.Engine, hull.Propulsion, "a bought boat can be an engine hull");
            // Engine boats are unaffected by the oar additions: the engine thrust path is unchanged
            // (see the EngineThrust tests above) and runs only on the Engine propulsion branch.
        }
    }
}
