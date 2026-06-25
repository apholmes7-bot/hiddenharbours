using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Boats;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// Boat handling — astern/reverse, the per-oar rowing input mapping (the owner's table), the
    /// differential-rowing physics (per-oar thrust → yaw, brace drag), the hull collider, and the
    /// data-driven PROPULSION BRANCH (the Dory rows; the Punt drives like an outboard — throttle + a
    /// speed-scaled rudder that can't pivot dead in the water). The thrust, yaw, rudder, and combo-mapping
    /// logic are pure static helpers, so they're tested without the physics/input loop; the collider
    /// existence is asserted on a freshly-built BoatController (RequireComponent).
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

        // ---- Part 5: PROPULSION BRANCH (the Dory rows · the Punt drives like an outboard) ----

        [Test]
        public void PropulsionBranch_EngineHullTakesTheHelm_OarsHullRows()
        {
            // The single source of truth the controller (FixedUpdate) AND the input layer (DevBoatInput)
            // both branch on — so input + physics can never disagree about a hull.
            Assert.IsTrue(BoatController.UsesEngineHelm(PropulsionType.Engine), "an Engine hull (the Punt) uses the outboard helm");
            Assert.IsFalse(BoatController.UsesEngineHelm(PropulsionType.Oars), "an Oars hull (the Dory) keeps per-oar rowing");

            var hull = ScriptableObject.CreateInstance<BoatHullDef>();   // template default = Oars (the dory)
            _spawned.Add(hull);
            Assert.IsFalse(BoatController.UsesEngineHelm(hull.Propulsion), "the Dory hull rows, not helms");
            hull.Propulsion = PropulsionType.Engine;                     // a Punt-configured hull
            Assert.IsTrue(BoatController.UsesEngineHelm(hull.Propulsion), "a Punt-configured (Engine) hull helms, no oars");
        }

        [Test]
        public void EngineRudder_NoAuthorityAtRest_GrowsAndSaturatesWithWay()
        {
            const float auth = 600f;   // the Punt's RudderAuthority
            // Dead in the water → no rudder authority → an outboard can't pivot at rest.
            Assert.AreEqual(0f, BoatController.RudderTorque(1f, auth, 0f), 1e-4f, "no pivot dead in the water");
            // Making way: helm-to-starboard turns the bow right (negative torque, the oar-yaw sign); helm-to-port left.
            Assert.Less(BoatController.RudderTorque(1f, auth, 3f), 0f, "helm to starboard → bow right while making way");
            Assert.Greater(BoatController.RudderTorque(-1f, auth, 3f), 0f, "helm to port → bow left");
            // Authority rises with way, then saturates by ~2 m/s.
            Assert.Greater(Mathf.Abs(BoatController.RudderTorque(1f, auth, 1f)),
                           Mathf.Abs(BoatController.RudderTorque(1f, auth, 0.2f)), "more way → more authority");
            Assert.AreEqual(-auth, BoatController.RudderTorque(1f, auth, 2f), 1e-3f, "full authority by 2 m/s");
            Assert.AreEqual(-auth, BoatController.RudderTorque(1f, auth, 50f), 1e-3f, "…and stays saturated");
        }

        [Test]
        public void AtRest_TheDoryPivots_ButTheOutboardCannot()
        {
            // The acceptance contrast: at zero way the Dory spins in place by working the oars
            // differentially (no speed needed), but the Punt's outboard rudder gives nothing — it must
            // make way to turn. This is "rows like a dory" vs "drives like a motorboat, no zero-speed pivot".
            Assert.AreNotEqual(0f, BoatController.OarYawTorque(1f, -1f, P, O), "the Dory pivots at rest (differential oars)");
            Assert.AreEqual(0f, BoatController.RudderTorque(1f, 600f, 0f), 1e-4f, "the Punt cannot pivot at rest (no oars; speed-scaled rudder)");
        }

        // ---- Part 6: GROUNDING IS NON-KILLING (shallows SLOW the boat; the helm never cuts out) ----
        // Latent issue flagged in #85: grounding used to ZERO thrust/oar drive and damp the rudder, so
        // St Peters' big tide (region-wide TideHeight, not per-position) could trip a transient "aground"
        // at low water and KILL the helm purely from the tide phase. Grounding must SLOW, never KILL.

        [Test]
        public void Grounded_RudderKeepsFullAuthority_HelmNeverCutByGrounding()
        {
            const float auth = 600f;
            // The rudder is a pure function of helm + way; grounding no longer enters the formula at all.
            // Making way over a soft bottom, the helm answers with FULL authority — it is not damped/cut.
            float makingWay = BoatController.RudderTorque(1f, auth, 3f);
            Assert.AreEqual(-auth, makingWay, 1e-3f,
                "aground but making way, the rudder keeps full authority — grounding never cuts the helm");
            Assert.Less(makingWay, 0f, "helm to starboard still turns the bow right while aground");
        }

        [Test]
        public void Grounded_ThrustStaysLive_InputIsNotZeroed()
        {
            // The engine/oar drive is applied regardless of grounding (the helm never cuts out). The static
            // thrust helpers carry no grounding term — full throttle/oar input always yields full thrust, so
            // the player can always power/row their way back to deep water (P5, never-punishing).
            Assert.AreEqual(1200f, BoatController.EngineThrust(1f, 1200f, 0.4f), 1e-3f,
                "full throttle yields full thrust — grounding does not zero engine input");
            Assert.AreEqual(2f * P, BoatController.OarThrust(1f, 1f, P), 1e-3f,
                "full oar stroke yields full thrust — grounding does not zero oar input");
            Assert.AreNotEqual(0f, BoatController.OarYawTorque(1f, -1f, P, O),
                "the dory can still yaw with the oars while aground (work off a soft bottom)");
        }

        [Test]
        public void Grounded_SlowdownDrag_OpposesMotion_ButOnlyWhenAground()
        {
            var through = new Vector2(0f, 2f);   // making way ahead, through the water
            const float drag = 900f;

            // Aground → a real through-water drag that OPPOSES motion (heavy, sluggish shallows = the teeth).
            Vector2 slow = BoatController.GroundedSlowdownForce(through, aground: true, drag);
            Assert.Less(Vector2.Dot(slow, through), 0f, "the grounded slowdown opposes motion through the water");
            Assert.Greater(slow.magnitude, 0f, "aground adds real drag — the boat feels heavy in the shallows");
            // Magnitude scales linearly with the through-water speed × the tunable strength: |F| = drag * |v|.
            Assert.AreEqual(drag * through.magnitude, slow.magnitude, 1e-3f,
                "drag scales linearly with the through-water speed and the tunable strength");

            // Afloat → zero (no penalty in deep water); and a non-positive drag is a no-op (tunable off).
            Assert.AreEqual(0f, BoatController.GroundedSlowdownForce(through, aground: false, drag).magnitude, 1e-4f,
                "afloat → no slowdown; the penalty exists only in the shallows");
            Assert.AreEqual(0f, BoatController.GroundedSlowdownForce(through, aground: true, 0f).magnitude, 1e-4f,
                "zero drag strength → no force (owner can tune the teeth all the way off)");

            // Symmetric: it slows you whichever way you move (heavy, not a one-way wall) — you can still
            // retreat to deep water, just sluggishly. Stronger tuning → more drag (it's a real slider).
            Vector2 retreating = BoatController.GroundedSlowdownForce(new Vector2(0f, -2f), aground: true, drag);
            Assert.Greater(retreating.magnitude, 0f, "the slowdown resists retreat too — sluggish, but never blocks it");
            Assert.Greater(BoatController.GroundedSlowdownForce(through, true, 1800f).magnitude,
                           BoatController.GroundedSlowdownForce(through, true, 900f).magnitude,
                           "stronger tuning = heavier shallows (the owner-tunable amount really scales)");
        }
    }
}
