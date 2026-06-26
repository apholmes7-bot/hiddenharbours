using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.Core;
using HiddenHarbours.Boats;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// On-water integration for the DIRECTIONAL FISHING-BOAT SKIN's propulsion (#97). PR #94 put the owner's
    /// 4-way fishing-boat picture on the St Peters playable boat, but the hull stayed the rowed Dory (Oars),
    /// so the player drove a powerboat with rowboat mechanics. The skin now drives on a dedicated Engine hull
    /// (<c>boat.fishing_skiff</c>, <see cref="PersistentCoreBuilder.ApplyDirectionalFishingBoatVisual"/>), so
    /// the controls MATCH the picture — "a power boat skin, not a rowboat" (owner).
    ///
    /// <para>Scene-faithful: the hull under test is the REAL authored <c>FishingSkiff.asset</c> the builder
    /// assigns (loaded from disk in the editor; mirrored in-code only as a player-build fallback), driven on a
    /// boat with the same hull collider the builder gives the playable boat (1.7×4.0, so the auto-computed
    /// rotational inertia matches the boat the owner actually drives — the seam the punt-steer fix exposed).
    /// The pure thrust/rudder math is covered headless in EditMode (BoatHandlingTests); this proves the LIVE
    /// rigidbody on the SKIN's hull responds as an ENGINE boat:</para>
    /// <list type="number">
    ///   <item><b>THROTTLE makes way</b> — ahead throttle drives the boat forward along its bow.</item>
    ///   <item><b>The RUDDER bites with way</b> — full ahead + full helm turns the bow clearly while making
    ///   way, but helm hard over DEAD IN THE WATER barely pivots (≈no yaw authority at rest — the outboard
    ///   seamanship fantasy, boats-and-navigation.md §2).</item>
    ///   <item><b>Differential-oar is NOT the active scheme</b> — a one-sided <see cref="BoatController.SetOarInput"/>
    ///   (which yaws a rowed Dory) does nothing on the Engine hull: no thrust, no yaw. The Engine branch reads
    ///   SetControl, not the oars.</item>
    /// </list>
    /// Environment is left null (GameServices.Reset), so wind/current/tide are zero and the motion under test
    /// is pure helm + engine.
    /// </summary>
    public class FishingSkiffEnginePlayTests
    {
        // The on-disk Def the StPeters builder assigns to the directional-skin boat (one source of truth).
        const string FishingSkiffPath = "Assets/_Project/Data/Boats/FishingSkiff.asset";

        private readonly List<Object> _spawned = new();

        [SetUp]
        public void SetUp() => GameServices.Reset();   // null environment → zero wind/current/tide

        [TearDown]
        public void TearDown()
        {
            GameServices.Reset();
            foreach (var o in _spawned)
                if (o != null) Object.Destroy(o);
            _spawned.Clear();
        }

        // The hull the skin drives on. In the editor (and CI's editor-run PlayMode) this is the REAL authored
        // FishingSkiff.asset — so the test asserts the actual Def the builder ships is an Engine hull. In a
        // built player (no AssetDatabase) it falls back to an in-code mirror of the same stats.
        private BoatHullDef LoadSkiffHull()
        {
#if UNITY_EDITOR
            var asset = AssetDatabase.LoadAssetAtPath<BoatHullDef>(FishingSkiffPath);
            if (asset != null) return asset;   // the real, authored skin hull
#endif
            var h = ScriptableObject.CreateInstance<BoatHullDef>();
            h.Id = "boat.fishing_skiff"; h.DisplayName = "Fishing Skiff";
            h.Propulsion = PropulsionType.Engine;
            h.LengthMeters = 4.5f; h.DraughtMeters = 0.3f; h.MassKg = 400f;
            h.HoldUnits = 6; h.CrewSlots = 1;
            h.EnginePower = 500f; h.RudderAuthority = 600f;
            h.OarPower = 300f; h.OarLateralOffset = 0.6f; h.OarBraceDrag = 400f;
            h.ForwardDrag = 120f; h.LateralDrag = 320f; h.WindExposure = 0.6f;
            h.MaxSafeSeaState = SeaState.Lively; h.CameraWorldHeightMeters = 14f;
            _spawned.Add(h);
            return h;
        }

        private (GameObject go, BoatController boat, Rigidbody2D rb) NewBoat(Vector3 pos)
        {
            var go = new GameObject("FishingSkiff");
            go.transform.position = pos;
            var boat = go.AddComponent<BoatController>();   // RequireComponent → Rigidbody2D + CapsuleCollider2D
            // Mirror the builder's hull collider so the auto-computed rotational INERTIA matches the playable
            // boat (a 4 m hull, not the unit-default capsule) — the rudder-vs-inertia balance the helm reads.
            var col = go.GetComponent<CapsuleCollider2D>();
            col.direction = CapsuleDirection2D.Vertical;
            col.size = new Vector2(1.7f, 4.0f);
            col.offset = Vector2.zero;
            _spawned.Add(go);
            return (go, boat, go.GetComponent<Rigidbody2D>());
        }

        // ---- The skin's hull IS an Engine hull (the authored Def the builder assigns) -------------

        [Test]
        public void SkiffHull_IsAnEngineHull_NotOars()
        {
            var hull = LoadSkiffHull();
            Assert.AreEqual("boat.fishing_skiff", hull.Id, "the skin drives on the dedicated fishing-skiff hull");
            Assert.AreEqual(PropulsionType.Engine, hull.Propulsion,
                "the directional fishing-boat skin must drive on an ENGINE hull (a power boat, not a rowboat)");
            Assert.IsTrue(BoatController.UsesEngineHelm(hull.Propulsion),
                "the controller takes the outboard helm for this hull (throttle + speed-scaled rudder)");
            Assert.Greater(hull.EnginePower, 0f, "an engine hull needs real thrust");
            Assert.Greater(hull.RudderAuthority, 0f, "an engine hull needs real rudder authority");
        }

        // ---- (1) THROTTLE makes way -------------------------------------------------------------

        [UnityTest]
        public IEnumerator Skiff_AheadThrottle_MakesWay()
        {
            var (go, boat, rb) = NewBoat(Vector3.zero);
            yield return null;                          // Awake caches the rigidbody
            boat.SetHull(LoadSkiffHull());

            Vector2 bow = go.transform.up;              // heading 0 → bow points +Y
            Vector2 startPos = rb.position;
            for (int i = 0; i < 40; i++)
            {
                boat.SetControl(1f, 0f);                // full ahead, helm amidships
                yield return new WaitForFixedUpdate();
            }

            Vector2 disp = rb.position - startPos;
            Assert.Greater(rb.linearVelocity.magnitude, 0.5f, "ahead throttle must build way on the engine hull");
            Assert.Greater(Vector2.Dot(disp, bow), 0f,
                "ahead throttle drives the skiff forward along its bow (throttle makes way)");
        }

        // ---- (2) The RUDDER bites with way; ≈no authority dead in the water ----------------------

        [UnityTest]
        public IEnumerator Skiff_FullAheadFullHelm_TurnsTheBowWhileMakingWay()
        {
            var (go, boat, rb) = NewBoat(Vector3.zero);
            yield return null;
            boat.SetHull(LoadSkiffHull());

            for (int i = 0; i < 60; i++)                // build way first (no helm)
            {
                boat.SetControl(1f, 0f);
                yield return new WaitForFixedUpdate();
            }
            float headingAfterStraight = rb.rotation;

            for (int i = 0; i < 90; i++)                // full ahead + helm hard to starboard
            {
                boat.SetControl(1f, 1f);
                yield return new WaitForFixedUpdate();
            }
            float headingAfterHelm = rb.rotation;

            Assert.Greater(rb.linearVelocity.magnitude, 0.5f, "the skiff should be making way under full throttle");
            // Helm to starboard turns the bow right; in Unity 2D a clockwise (starboard) turn DECREASES
            // rb.rotation (negative torque, matching RudderTorque). A clearly-perceptible swing (≥10°).
            float turned = headingAfterHelm - headingAfterStraight;
            Assert.Less(turned, -10f,
                $"full helm while making way must turn the bow clearly (got {turned:F2}°) — the rudder bites with way");
        }

        [UnityTest]
        public IEnumerator Skiff_HelmAtRest_DoesNotPivot()
        {
            var (go, boat, rb) = NewBoat(Vector3.zero);
            yield return null;
            boat.SetHull(LoadSkiffHull());

            float startHeading = rb.rotation;
            for (int i = 0; i < 60; i++)                // helm hard over with NO throttle
            {
                boat.SetControl(0f, 1f);
                yield return new WaitForFixedUpdate();
            }
            float turned = Mathf.Abs(rb.rotation - startHeading);
            Assert.Less(turned, 2f,
                $"the engine skiff must NOT pivot dead in the water (turned {turned:F2}°) — no rudder authority at rest, §2");
        }

        // ---- (3) Differential-OAR is NOT the active scheme on the engine hull --------------------

        [UnityTest]
        public IEnumerator Skiff_OarInput_DoesNothing_EngineSchemeIsActive()
        {
            var (go, boat, rb) = NewBoat(Vector3.zero);
            yield return null;
            boat.SetHull(LoadSkiffHull());

            // A one-sided oar stroke YAWS + drives a rowed Dory. On the Engine hull the controller ignores
            // SetOarInput entirely (it reads SetControl), so this must produce neither thrust nor yaw — proving
            // the powerboat skin is NOT on rowboat mechanics. Throttle/helm are left at zero (SetControl(0,0)
            // is the default), so any motion could only come from the oar path.
            Vector2 startPos = rb.position;
            for (int i = 0; i < 60; i++)
            {
                boat.SetOarInput(1f, -1f, false);       // a hard differential — a Dory would spin in place
                yield return new WaitForFixedUpdate();
            }

            Assert.Less(Mathf.Abs(rb.angularVelocity), 1e-2f,
                "differential oar input must NOT yaw the engine skiff (oars are not its control scheme)");
            Assert.Less((rb.position - startPos).magnitude, 1e-2f,
                "oar input must NOT drive the engine skiff (it ignores SetOarInput; the engine helm is active)");
        }
    }
}
