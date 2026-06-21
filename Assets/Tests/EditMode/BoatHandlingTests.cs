using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Boats;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Boat handling pass — astern/reverse, the rowing-animation frame mapping, and the hull collider.
    /// The thrust and frame/tempo logic are pure static helpers, so they're tested without the physics
    /// step; the collider existence is asserted on a freshly-built BoatController (RequireComponent).
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

        // ---- Part 2: ROWING ANIMATION (frame selection) ------------------------------------

        [Test]
        public void RowFrame_ForPhase_CyclesAndWraps_Ahead()
        {
            Assert.AreEqual(0, BoatRowAnimator.FrameForPhase(0f, 6, false));
            Assert.AreEqual(0, BoatRowAnimator.FrameForPhase(0.9f, 6, false), "within a frame stays on it");
            Assert.AreEqual(1, BoatRowAnimator.FrameForPhase(1.2f, 6, false));
            Assert.AreEqual(5, BoatRowAnimator.FrameForPhase(5.5f, 6, false));
            Assert.AreEqual(0, BoatRowAnimator.FrameForPhase(6f, 6, false), "the 6-frame cycle wraps");
            Assert.AreEqual(1, BoatRowAnimator.FrameForPhase(7f, 6, false), "and keeps wrapping");
        }

        [Test]
        public void RowFrame_ForPhase_Astern_RunsReversed()
        {
            Assert.AreEqual(5, BoatRowAnimator.FrameForPhase(0f, 6, true), "astern starts at the far end of the cycle");
            Assert.AreEqual(4, BoatRowAnimator.FrameForPhase(1f, 6, true));
            Assert.AreEqual(0, BoatRowAnimator.FrameForPhase(5f, 6, true), "astern reverses 0..5 to 5..0");
        }

        [Test]
        public void RowFrame_ForPhase_IsNegativeSafe()
        {
            Assert.AreEqual(5, BoatRowAnimator.FrameForPhase(-1f, 6, false), "negative phase wraps within range");
            Assert.AreEqual(0, BoatRowAnimator.FrameForPhase(0f, 0, false), "no frames → frame 0, never throws");
        }

        [Test]
        public void RowCycleFps_ScalesWithSpeed_AndIsCapped()
        {
            Assert.AreEqual(0f, BoatRowAnimator.CycleFps(0f, 4f, 12f), 1e-4f, "at rest the oars don't move");
            Assert.AreEqual(4f, BoatRowAnimator.CycleFps(1f, 4f, 12f), 1e-4f, "faster boat → faster oars");
            Assert.AreEqual(8f, BoatRowAnimator.CycleFps(2f, 4f, 12f), 1e-4f);
            Assert.AreEqual(8f, BoatRowAnimator.CycleFps(-2f, 4f, 12f), 1e-4f, "tempo uses speed magnitude (astern same rate)");
            Assert.AreEqual(12f, BoatRowAnimator.CycleFps(100f, 4f, 12f), 1e-4f, "capped so it never strobes");
        }

        [Test]
        public void RowMakingWay_HasAtRestThreshold()
        {
            Assert.IsFalse(BoatRowAnimator.IsMakingWay(0f, 0.15f), "still = at rest");
            Assert.IsFalse(BoatRowAnimator.IsMakingWay(0.1f, 0.15f), "a creep below threshold still idles");
            Assert.IsTrue(BoatRowAnimator.IsMakingWay(0.2f, 0.15f), "above threshold = making way");
            Assert.IsTrue(BoatRowAnimator.IsMakingWay(-0.2f, 0.15f), "astern is making way too");
        }

        [Test]
        public void RowFrame_MapsSpeedToFrame_Deterministically()
        {
            // Accumulating phase from a constant speed over fixed steps must give a fixed frame sequence,
            // and a faster boat must reach later frames in fewer steps. (speed → frame, deterministic.)
            int FrameAfter(float speed, float dt, int steps, bool astern)
            {
                float phase = 0f;
                for (int i = 0; i < steps; i++)
                    phase = BoatRowAnimator.AdvancePhase(phase, speed, 4f, 12f, dt);
                return BoatRowAnimator.FrameForPhase(phase, 6, astern);
            }

            // Repeatable: identical inputs → identical frame.
            Assert.AreEqual(FrameAfter(1.5f, 0.1f, 7, false), FrameAfter(1.5f, 0.1f, 7, false), "deterministic in its inputs");

            // speed 1 m/s, fps 4 → +0.4 phase/step; after 3 steps phase=1.2 → frame 1.
            Assert.AreEqual(1, FrameAfter(1f, 0.1f, 3, false));
            // Twice the speed reaches frame 1 in half the steps (phase 0.8 → 1.6 after the same 3 steps → frame... ).
            Assert.AreEqual(1, FrameAfter(2f, 0.1f, 2, false), "twice the speed → frame 1 in fewer steps (phase 1.6)");
            // Same motion astern lands on the reversed frame.
            Assert.AreEqual(BoatRowAnimator.FrameForPhase(1.2f, 6, true), FrameAfter(1f, 0.1f, 3, true), "astern reverses the same phase");
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
        public void Oar_SetOarInput_StoresClampedRowDrive()
        {
            var go = new GameObject("RowBoat");
            _spawned.Add(go);
            var boat = go.AddComponent<BoatController>();

            boat.SetOarInput(1f, 1f, false);
            Assert.AreEqual(1f, boat.RowDrive, 1e-4f, "both oars ahead → full ahead rowing activity");
            boat.SetOarInput(1f, -1f, false);
            Assert.AreEqual(0f, boat.RowDrive, 1e-4f, "opposite oars → no net drive (a pivot)");
            boat.SetOarInput(-1f, -1f, true);
            Assert.AreEqual(-1f, boat.RowDrive, 1e-4f, "both oars astern → astern rowing activity");
            boat.SetOarInput(2f, -2f, false);
            Assert.AreEqual(0f, boat.RowDrive, 1e-4f, "oar input clamps to ±1 (2 and -2 → 1 and -1 → net 0)");
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
