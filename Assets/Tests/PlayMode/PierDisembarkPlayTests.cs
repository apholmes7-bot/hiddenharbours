using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.Core;
using HiddenHarbours.World;
using HiddenHarbours.Player;
using HiddenHarbours.Boats;
using HiddenHarbours.Art;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// <b>SAIL HOME AND STEP ASHORE — in a real frame loop.</b> The beat that was broken: St Peters is
    /// tide-gated and its one dock stands over a dredged −1.0 m slip, so the on-foot sim called the ratified
    /// disembark point open water and the fisher crawled across her own pier at the slow-swim factor with
    /// the submerge shader painting her neck-deep.
    ///
    /// <para><b>What only PlayMode can show.</b> The EditMode suite owns the geometry and the maths
    /// (<c>StPetersWharfWalkabilityTests</c> asserts the real authored terrain; <c>StandableSurfacesTests</c>
    /// asserts the seam). What is left is the JOIN: that a <see cref="StandablePlatform"/> in a running
    /// scene actually registers itself, that <see cref="ControlSwitcher"/>'s disembark lands the player on
    /// it, and that <see cref="PlayerWalkController"/>'s own <c>FixedUpdate</c> — reading the LIVE registry
    /// through <c>TidalWalkability.DepthNow</c> — then calls her dry. Nothing here is faked past the seam:
    /// the depth probe is the shipped one.</para>
    ///
    /// <para><b>The numbers here are a stand-in, deliberately.</b> App.Editor (and therefore
    /// <c>StPetersWharf</c>/<c>StPetersBuilder</c>) is not reachable from a PlayMode assembly, so this rig
    /// restates the SHAPE of the berth — a flat −1.0 m bed, a 3.5 m spring high water, a deck measured at
    /// the pier root's 5.35 m — rather than the authored region. The real values are asserted against the
    /// real terrain in EditMode; this test is about the wiring, and would fail identically for any of them.</para>
    ///
    /// <para>⚠️ Headless batch mode resets every input device each frame, so a HELD key is unreachable
    /// (see <c>PlayerSprintPlayTests</c>' note). The walk is therefore driven through the controller's own
    /// pure resolve fed by the LIVE depth probe, which is the same call its <c>FixedUpdate</c> makes.</para>
    /// </summary>
    public class PierDisembarkPlayTests
    {
        // The berth's shape (see the class note): the dredged slip, the tide, and the measured deck.
        const float SlipBedElevation = -1.0f;
        const float SpringHighWater  = 3.5f;
        const float DeckElevation    = 5.35f;

        // A 31 x 6 m finger of deck running out along the slip, with the disembark point on the planks —
        // the St Peters pier's proportions (as re-sited by the 2026-07-30 island shrink; still a
        // stand-in per the class note — the wiring under test is identical for any berth shape).
        static readonly Rect DeckFootprint = new Rect(183f, -3f, 31f, 6f);
        static readonly Vector3 MooringPos   = new Vector3(215f, 0f, 0f);   // the boat lies off the head
        static readonly Vector3 DisembarkPos = new Vector3(213f, 0f, 0f);   // ratified: ON the planks
        static readonly Vector3 InTheSlipPos = new Vector3(217f, 0f, 0f);   // a metre off the head, in water

        private sealed class FlatBed : ITidalTerrain
        {
            public float Elevation = SlipBedElevation;
            public float ElevationAt(Vector2 worldPos) => Elevation;
        }

        private sealed class FixedTide : IEnvironmentService
        {
            public float Level = SpringHighWater;
            public int WorldSeed => 0;
            public TideProfile ActiveTideProfile { get; set; }
            public EnvironmentSample Sample() => default;
            public float TideHeightAt(double totalSeconds) => Level;
            public float WaterLevelAt(double totalSeconds) => Level;
        }

        private readonly List<Object> _spawned = new List<Object>();
        private readonly List<OnFootWaterStateChanged> _waterEvents = new List<OnFootWaterStateChanged>();
        private void OnWater(OnFootWaterStateChanged e) => _waterEvents.Add(e);

        private FixedTide _tide;
        private PlayerWalkController _walk;
        private Rigidbody2D _playerBody;
        private ControlSwitcher _switcher;
        private StandablePlatform _pier;

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();
            StandableSurfaces.Clear();
            _waterEvents.Clear();
            EventBus.Clear<OnFootWaterStateChanged>();
            EventBus.Subscribe<OnFootWaterStateChanged>(OnWater);

            _tide = new FixedTide();
            GameServices.Environment = _tide;
            GameServices.TidalTerrain = new FlatBed();

            // THE PIER, as the wharf builder registers it: a real StandablePlatform in a live scene, so its
            // OnEnable registration is what the depth probe below finds.
            var pierGo = Spawn("StPetersWharf");
            _pier = pierGo.AddComponent<StandablePlatform>();
            _pier.Configure("wharf.st_peters", DeckFootprint, DeckElevation);

            // The player, on foot, with the tide gate ON (as the St Peters builder sets it).
            var playerGo = Spawn("Player");
            playerGo.AddComponent<SpriteRenderer>();
            _walk = playerGo.AddComponent<PlayerWalkController>();
            _playerBody = playerGo.GetComponent<Rigidbody2D>();
            SetTideGatedWalk(_walk, true);

            // The boat, moored off the pier head, and the switcher wired to this berth.
            var boatGo = Spawn("Dory");
            boatGo.transform.position = MooringPos;
            var boat = boatGo.AddComponent<BoatController>();
            var input = boatGo.AddComponent<DevBoatInput>();
            var hull = ScriptableObject.CreateInstance<BoatHullDef>();
            hull.Id = "boat.dory"; hull.DraughtMeters = 0.3f; hull.CameraWorldHeightMeters = 14f;
            hull.Propulsion = PropulsionType.Oars;
            _spawned.Add(hull);
            boat.SetHull(hull);
            boat.enabled = false; input.enabled = false;

            var dockZone = Spawn("DockZone"); dockZone.transform.position = MooringPos;
            var disembark = Spawn("Disembark"); disembark.transform.position = DisembarkPos;
            _switcher = Spawn("Switcher").AddComponent<ControlSwitcher>();
            _switcher.Configure(_walk, boat, input, dockZone.transform, 3.5f, disembark.transform);
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Unsubscribe<OnFootWaterStateChanged>(OnWater);
            EventBus.Clear<OnFootWaterStateChanged>();
            StandableSurfaces.Clear();
            GameServices.Reset();
            foreach (var o in _spawned)
                if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        private GameObject Spawn(string name)
        {
            var go = new GameObject(name);
            _spawned.Add(go);
            return go;
        }

        /// <summary>Turn the region's tide gate on for the controller (a private serialized tunable — the
        /// St Peters builder sets it the same way).</summary>
        private static void SetTideGatedWalk(PlayerWalkController walk, bool on)
        {
#if UNITY_EDITOR
            var so = new SerializedObject(walk);
            var prop = so.FindProperty("_tideGatedWalk");
            Assert.IsNotNull(prop, "PlayerWalkController._tideGatedWalk was renamed — re-point this write");
            prop.boolValue = on;
            so.ApplyModifiedPropertiesWithoutUndo();
#else
            Assert.Ignore("needs the editor to set the serialized tide-gate tunable");
#endif
        }

        /// <summary>Read an owner tunable off the REAL controller rather than restating it (the
        /// <c>PlayerSprintPlayTests</c> convention).</summary>
        private float Tunable(string field)
        {
#if UNITY_EDITOR
            var prop = new SerializedObject(_walk).FindProperty(field);
            Assert.IsNotNull(prop, $"PlayerWalkController.{field} was renamed — re-point this read");
            return prop.floatValue;
#else
            Assert.Ignore("needs the editor to read serialized tunables");
            return 0f;
#endif
        }

        /// <summary>Put the fisher somewhere and let the physics loop tick, so her own
        /// <c>FixedUpdate</c> — not the test — decides what water she is in.</summary>
        private IEnumerator StandAt(Vector3 pos)
        {
            _playerBody.position = pos;
            _walk.transform.position = pos;
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
        }

        // =================================================================================
        //  THE JOIN
        // =================================================================================

        [UnityTest]
        public IEnumerator TheWharfRegistersItselfInARunningScene_AndRelinquishesWhenTornDown()
        {
            // ⚠ This can ONLY be asserted in PlayMode. StandablePlatform carries no [ExecuteAlways] — on
            // purpose, mirroring TidalTerrain: claiming the seam is a RUNTIME act, so OnEnable does not fire
            // in edit mode and an EditMode test of it would assert something the shipped game never does.
            Assert.AreEqual(1, StandableSurfaces.Count,
                "the pier's StandablePlatform registered itself when the scene came up (OnEnable)");
            Assert.IsTrue(StandableSurfaces.TryGetDeckElevationNow(DisembarkPos, out float deck),
                "…and the live registry finds its deck at the disembark point");
            Assert.AreEqual(DeckElevation, deck, 1e-4f);

            // Tear the region down: the deck must go with it, or the next region inherits dry water.
            _pier.gameObject.SetActive(false);
            yield return null;

            Assert.AreEqual(0, StandableSurfaces.Count, "disabling the pier relinquishes the registration");
            Assert.AreEqual(SpringHighWater - SlipBedElevation, TidalWalkability.DepthNow(DisembarkPos), 1e-4f,
                "and with no deck the disembark point is back to being the dredged slip under 4.5 m of " +
                "water — which is exactly the state the whole fix is about");
        }

        [UnityTest]
        public IEnumerator Before_WithNoDeckRegistered_SheSwamAcrossThePlanks_ItNeverBlockedHer()
        {
            // ⚠ WHAT ACTUALLY HAPPENED TODAY, in play, at high water — the question the handoff asked
            // (blocks? wades? falls through?). Answer: NONE of those. The soft wall is lifted the moment the
            // fisher's ORIGIN is already past the wade band (the never-trap rule, P5), so with 4.5 m of
            // simulated sea over the planks she was free to move — at the SLOW-SWIM crawl, sprint refused,
            // announcing herself as swimming. A cozy failure that reads as an art bug rather than a wall,
            // which is exactly why it survived to the ratified disembark point.
            _pier.gameObject.SetActive(false);            // the pre-seam world
            yield return null;

            yield return StandAt(DisembarkPos);
            Assert.AreEqual(OnFootWaterState.Swim, _walk.WaterState,
                "on the planks, the pre-seam sim called her a SWIMMER");

            float wade = Tunable("_wadeDepth"), swim = Tunable("_swimLimit");
            float wadeSlow = Tunable("_wadeSlowFactor"), swimSlow = Tunable("_swimSlowFactor");
            float speed = Tunable("_moveSpeed");

            var v = PlayerWalkController.ApplyWaterEdge(new Vector2(-speed, 0f), DisembarkPos,
                        TidalWalkability.DepthNow, Tunable("_wadeProbeDistance"),
                        wade, swim, wadeSlow, swimSlow);
            Assert.AreNotEqual(0f, v.x,
                "…and she was NOT blocked — the never-trap rule lifts the wall once you are already deep");
            Assert.AreEqual(-speed * swimSlow, v.x, 1e-4f,
                $"she crawled the pier at the slow-swim factor ({swimSlow:0.00}× walking speed)");
            Assert.AreEqual(speed, PlayerWalkController.SpeedFor(true, speed, Tunable("_sprintSpeed"),
                                                                 TidalWalkability.DepthNow(DisembarkPos), wade),
                "and could not run on her own dock, because you don't sprint out of the sea");
            Assert.Greater(PlayerSubmergeMath.WaterlineFraction(TidalWalkability.DepthNow(DisembarkPos), 1.8f, 0.85f),
                           0.8f,
                "with the waterline up at the neck cap — the 'underwater on the dock' look");
        }

        [UnityTest]
        public IEnumerator SteppingOffTheBoatAtTheBerth_LandsYouOnDryPlanksAtHighWater()
        {
            // Start in the slip beside the pier head, so the fisher is genuinely in the water first: that
            // makes the Dry that follows a real transition rather than the controller's initial value.
            yield return StandAt(InTheSlipPos);
            Assert.AreEqual(OnFootWaterState.Swim, _walk.WaterState,
                "a metre off the pier head at spring high water is 4.5 m of sea — she is swimming");

            // Board (→ the deck), then step ashore. The boat is inside the dock radius, so this is the tidy
            // dock landing: the ratified disembark point, on the planks.
            Assert.IsTrue(_switcher.TryInteract(), "boarding from beside the boat succeeds");
            Assert.AreEqual(ControlMode.OnDeck, _switcher.Mode, "boarding lands on the deck");
            Assert.IsTrue(_switcher.TryInteract(), "and E from the deck steps ashore at the dock");
            Assert.AreEqual(ControlMode.OnFoot, _switcher.Mode);
            Assert.Less(Vector3.Distance(_walk.transform.position, DisembarkPos), 0.01f,
                $"the tidy dock landing puts her on the ratified disembark point, not at " +
                $"{_walk.transform.position}");

            _waterEvents.Clear();
            yield return StandAt(DisembarkPos);

            Assert.AreEqual(OnFootWaterState.Dry, _walk.WaterState,
                "standing on the wharf at spring high water, the fisher's own controller must call her DRY — " +
                "this is the whole fix. Before the standable-surface seam she read Swim on the planks.");
            Assert.IsTrue(_waterEvents.Count > 0 && _waterEvents[^1].State == OnFootWaterState.Dry,
                "and the on-foot water-state signal announced the transition out of the water, so the HUD/VFX " +
                "stop treating the pier as sea");
            Assert.Less(TidalWalkability.DepthNow(DisembarkPos), 0f,
                "the LIVE depth probe (the registry, in a running scene) reads dry over the planks");
        }

        [UnityTest]
        public IEnumerator WalkingThePier_KeepsFullSpeedTheWholeLength_AndSoftWallsAtTheHead()
        {
            yield return StandAt(DisembarkPos);

            float wade = Tunable("_wadeDepth"), swim = Tunable("_swimLimit");
            float wadeSlow = Tunable("_wadeSlowFactor"), swimSlow = Tunable("_swimSlowFactor");
            float probe = Tunable("_wadeProbeDistance");
            float speed = Tunable("_moveSpeed");

            // Walk the pier from the head back to the root, resolving each step through the controller's own
            // pure helper fed by the LIVE depth probe — the same call its FixedUpdate makes.
            for (float x = DeckFootprint.xMin + 1f; x <= DeckFootprint.xMax - 1f; x += 1f)
            {
                var at = new Vector2(x, 0f);
                var v = PlayerWalkController.ApplyWaterEdge(new Vector2(-speed, 0f), at,
                            TidalWalkability.DepthNow, probe, wade, swim, wadeSlow, swimSlow);
                Assert.AreEqual(-speed, v.x, 1e-4f,
                    $"walking west along the deck at x={x} was slowed — the planks must carry full walking " +
                    "speed end to end, not the wade factor");
            }

            // At the head, the step OFF the planks into the slip is soft-walled — the deck's dryness must
            // not leak into the berth beside it.
            var atHead = new Vector2(DeckFootprint.xMax - 0.1f, 0f);
            var offEnd = PlayerWalkController.ApplyWaterEdge(new Vector2(speed, 0f), atHead,
                             TidalWalkability.DepthNow, probe, wade, swim, wadeSlow, swimSlow);
            Assert.AreEqual(0f, offEnd.x, 1e-5f,
                "walking east off the pier head stops at the edge — the boat-only slip is still boat-only");

            // And sliding ALONG the pier from that same spot still works (the axes gate independently).
            var along = PlayerWalkController.ApplyWaterEdge(new Vector2(speed, -speed), atHead,
                            TidalWalkability.DepthNow, probe, wade, swim, wadeSlow, swimSlow);
            Assert.AreEqual(0f, along.x, 1e-5f, "the into-water component is cut");
            Assert.AreEqual(-speed, along.y, 1e-4f, "but she can still walk down the deck");
        }

        [UnityTest]
        public IEnumerator OnThePlanks_SprintIsAllowedAndTheBodyIsNotSubmerged()
        {
            yield return StandAt(DisembarkPos);

            float walkSpeed = Tunable("_moveSpeed"), sprintSpeed = Tunable("_sprintSpeed");
            float wade = Tunable("_wadeDepth");

            float onDeck = TidalWalkability.DepthNow(DisembarkPos);
            float inSlip = TidalWalkability.DepthNow(InTheSlipPos);

            Assert.AreEqual(sprintSpeed, PlayerWalkController.SpeedFor(true, walkSpeed, sprintSpeed, onDeck, wade),
                "you can run down a wharf — sprint is a dry-ground privilege and the planks are dry ground");
            Assert.AreEqual(walkSpeed, PlayerWalkController.SpeedFor(true, walkSpeed, sprintSpeed, inSlip, wade),
                "…and it is still refused in the slip beside it (P5: you don't sprint out of the sea)");

            // The body's waterline reads through the SAME composition now, so it cannot disagree with the
            // gate. (The pure maths, not the material — a missing shader would make a material assertion
            // pass vacuously.)
            Assert.AreEqual(0f, PlayerSubmergeMath.WaterlineFraction(onDeck, 1.8f, 0.85f), 1e-6f,
                "no waterline on the body while she stands on the planks — the 'underwater on the dock' look " +
                "was the same divergence, read from a second copy of the depth rule");
            Assert.Greater(PlayerSubmergeMath.WaterlineFraction(inSlip, 1.8f, 0.85f), 0f,
                "…and she is still visibly in the water when she is actually in it");
        }
    }
}
