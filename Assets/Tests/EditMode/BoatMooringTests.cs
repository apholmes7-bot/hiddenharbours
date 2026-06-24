using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Boats;
using HiddenHarbours.Player;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// The rope / mooring mechanic — "tie up your boat so the sea doesn't take it" (P1 + P5). Exercised
    /// through the pure tether/drift helpers (no physics loop) and the hold/root/board state machine on both
    /// <see cref="BoatMooring"/> and the Player-lane <see cref="ControlSwitcher"/>:
    ///
    ///   • ROPE PHYSICS (firm limit, not a rubber band) — inside rope-length the rope is SLACK (zero force,
    ///     the boat bobs free); past it a FIRM near-inextensible limit checks her, and a hard positional
    ///     clamp guarantees she can never sit more than the tiny give past rope-length. An integrated
    ///     drift-under-constant-force is held essentially AT the rope's end (no big springy overshoot),
    ///     while the same drift untethered runs away.
    ///   • DRIFT — the deterministic wind+tide drift force sets an idle hull moving on its leash.
    ///   • STATE MACHINE — disembark HOLDS the rope (boat tethered to the player); the root key roots it to
    ///     the ground and back to hand; re-boarding stows the rope.
    ///   • DETERMINISM — the tether + drift forces are bit-stable for identical inputs (no hidden RNG).
    /// </summary>
    public class BoatMooringTests
    {
        // A flat deterministic terrain + environment so the switcher's OnLand() depth read is stable.
        private sealed class FlatTerrain : ITidalTerrain
        {
            public float Elevation;
            public float ElevationAt(Vector2 worldPos) => Elevation;
        }

        private sealed class WindEnv : IEnvironmentService
        {
            public Vector2 Wind, Current;
            public float Level;
            public int WorldSeed => 0;
            public TideProfile ActiveTideProfile { get; set; }
            public EnvironmentSample Sample()
                => new EnvironmentSample(Wind, Current, Level, SeaState.Calm, 1f);
            public float TideHeightAt(double totalSeconds) => Level;
            public float WaterLevelAt(double totalSeconds) => Level;
        }

        private readonly List<Object> _spawned = new();

        [TearDown]
        public void TearDown()
        {
            GameServices.Reset();
            foreach (var o in _spawned)
                if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        private BoatHullDef Hull(string id, PropulsionType propulsion)
        {
            var hull = ScriptableObject.CreateInstance<BoatHullDef>();
            hull.Id = id; hull.Propulsion = propulsion; hull.DraughtMeters = 0.3f;
            hull.CameraWorldHeightMeters = 14f;
            _spawned.Add(hull);
            return hull;
        }

        // Firm-limit tunables used across the rope-physics tests (owner-editable in the real component).
        private const float RopeLen = 4f;
        private const float Give = 0.15f;
        private const float Stiff = 1200f;
        private const float Damp = 120f;

        // ============ Part 1: the FIRM-LIMIT rope (a rope, not a rubber band) ============

        [Test]
        public void TetherForce_SlackInsideRopeLength_IsZero()
        {
            var tie = Vector2.zero;
            // Inside the rope circle the boat bobs/swings freely — no rope force at all.
            Assert.AreEqual(Vector2.zero, BoatMooring.TetherForce(Vector2.zero, tie, RopeLen, Stiff, Vector2.zero, Damp, Give),
                            "at the tie point the rope is slack");
            Assert.AreEqual(Vector2.zero, BoatMooring.TetherForce(new Vector2(3.9f, 0f), tie, RopeLen, Stiff, Vector2.zero, Damp, Give),
                            "just inside rope-length the rope is still slack (free swing)");
            Assert.IsFalse(BoatMooring.IsBeyondRope(new Vector2(3.9f, 0f), tie, RopeLen), "3.9 m is within a 4 m rope");
        }

        [Test]
        public void TetherForce_WithinTheGive_IsStillZero()
        {
            var tie = Vector2.zero;
            // The rope's tiny give (0.15 m) reads as the rope's own minimal stretch — no checking force yet.
            Vector2 f = BoatMooring.TetherForce(new Vector2(RopeLen + 0.1f, 0f), tie, RopeLen, Stiff, Vector2.zero, Damp, Give);
            Assert.AreEqual(Vector2.zero, f, "within the small give past rope-length the rope hasn't bitten yet");
        }

        [Test]
        public void TetherForce_PastTheGive_ChecksFirmly_AndOnlyOnTheExcess()
        {
            var tie = Vector2.zero;
            // 0.4 m past rope-length: that's 0.25 m past the 0.15 m give → the FIRM limit acts on the EXCESS
            // only (0.25 m), not the whole stretch. Force = 0.25 * 1200 = 300, pulling straight back in.
            Vector2 f = BoatMooring.TetherForce(new Vector2(RopeLen + 0.4f, 0f), tie, RopeLen, Stiff, Vector2.zero, Damp, Give);
            Assert.Less(f.x, 0f, "the taut rope pulls the boat back toward the tie point");
            Assert.AreEqual(0f, f.y, 1e-4f, "the pull is purely radial (along the rope)");
            Assert.AreEqual(-300f, f.x, 1e-3f, "firm-limit force = (overshoot − give) * stiffness, on the excess only");
        }

        [Test]
        public void TetherForce_IsFarStiffer_ThanASoftRubberBand()
        {
            var tie = Vector2.zero;
            // A firm rope at the SAME small overshoot delivers a much larger checking force than a soft,
            // rubber-band-like spring would — the difference between "hits a firm stop" and "stretches".
            var pos = new Vector2(RopeLen + 0.3f, 0f);
            float firm = BoatMooring.TetherForce(pos, tie, RopeLen, Stiff, Vector2.zero, Damp, Give).magnitude;
            float soft = BoatMooring.TetherForce(pos, tie, RopeLen, /*soft*/90f, Vector2.zero, 0f, Give).magnitude;
            Assert.Greater(firm, soft * 5f, "the firm limit is dramatically stiffer than a rubber-band pull");
        }

        [Test]
        public void TetherForce_NeverPushesOutward_RopeOnlyPulls()
        {
            var tie = Vector2.zero;
            var pos = new Vector2(RopeLen + 0.5f, 0f);   // taut, past the give
            // A boat surging INWARD (toward the tie): spring still pulls in, the damper must NOT add outward
            // force (a rope can't shove the boat off its tie). Net radial force stays inward.
            Vector2 fIn = BoatMooring.TetherForce(pos, tie, RopeLen, Stiff, new Vector2(-5f, 0f), Damp, Give);
            Assert.LessOrEqual(fIn.x, 0f, "even with inward velocity the rope force is never outward (it only pulls)");

            // A boat surging OUTWARD: the damper adds extra inward braking (arrests her at the limit).
            Vector2 fOut = BoatMooring.TetherForce(pos, tie, RopeLen, Stiff, new Vector2(+5f, 0f), Damp, Give);
            Assert.Less(fOut.x, fIn.x, "surging outward, the damper adds more inward braking than at rest/inbound");
        }

        [Test]
        public void ConstrainToRope_ClampsBeyondTheGive_LeavesWithinAlone()
        {
            var tie = Vector2.zero;
            // Within rope+give → untouched.
            Assert.AreEqual(new Vector2(RopeLen + 0.1f, 0f),
                            BoatMooring.ConstrainToRope(new Vector2(RopeLen + 0.1f, 0f), tie, RopeLen, Give),
                            "a boat within rope-length + give isn't moved (the rope is slack/just taut)");
            // Way past → snapped back onto the limit circle (rope is inextensible).
            Vector2 clamped = BoatMooring.ConstrainToRope(new Vector2(20f, 0f), tie, RopeLen, Give);
            Assert.AreEqual(RopeLen + Give, clamped.magnitude, 1e-4f,
                            "an over-stretched boat is hard-clamped onto rope-length + give (inextensible)");
            Assert.AreEqual(0f, clamped.y, 1e-4f, "the clamp keeps the same bearing from the tie point");
        }

        [Test]
        public void TetheredBoat_UnderConstantDrift_IsHeldAtTheRopesEnd_NotStretchedFar()
        {
            // Integrate a tethered boat under a CONSTANT outward drift force WITH the hard positional clamp,
            // and confirm it is held essentially AT the rope's end — never stretched far past it (the firm,
            // near-inextensible limit, NOT a rubber band that lets her surge way out and twang back). Pure
            // explicit-Euler over the helpers (deterministic, no Unity physics). The same drift untethered
            // runs away in the next test.
            var tie = Vector2.zero;
            float mass = 4f, dt = 0.02f;
            Vector2 driftForce = new Vector2(60f, 0f);   // a steady wind/tide shove outward

            Vector2 pos = tie, vel = Vector2.zero;
            float maxDist = 0f;
            for (int i = 0; i < 4000; i++)   // 80 s of sim
            {
                Vector2 tether = BoatMooring.TetherForce(pos, tie, RopeLen, Stiff, vel, Damp, Give);
                Vector2 a = (driftForce + tether) / mass;
                vel += a * dt;
                pos += vel * dt;
                // The inextensible clamp (mirrors BoatMooring.FixedUpdate): she can't sit past rope+give.
                Vector2 clamped = BoatMooring.ConstrainToRope(pos, tie, RopeLen, Give);
                if (clamped != pos)
                {
                    pos = clamped;
                    Vector2 outward = (pos - tie).normalized;
                    float outwardSpeed = Vector2.Dot(vel, outward);
                    if (outwardSpeed > 0f) vel -= outward * outwardSpeed;
                }
                maxDist = Mathf.Max(maxDist, (pos - tie).magnitude);
            }
            // Held firmly on the leash: she reaches the rope's end and is checked there, sitting right at
            // rope-length + give and never stretching meaningfully past it (a firm stop, not a rubber band).
            Assert.LessOrEqual(maxDist, RopeLen + Give + 1e-3f, "a tethered boat is held AT the rope's end (inextensible)");
            Assert.Greater((pos - tie).magnitude, RopeLen - 0.5f, "…and it DOES swing out to the end of its leash");
        }

        [Test]
        public void UntiedBoat_UnderTheSameDrift_RunsAway()
        {
            // The contrast (the teeth): with NO tether, the identical drift force carries the boat far away.
            float mass = 4f, dt = 0.02f;
            Vector2 driftForce = new Vector2(60f, 0f);
            Vector2 pos = Vector2.zero, vel = Vector2.zero;
            for (int i = 0; i < 4000; i++)
            {
                Vector2 a = driftForce / mass;   // no tether — free drift
                vel += a * dt;
                pos += vel * dt;
            }
            Assert.Greater(pos.magnitude, 50f, "an UNTETHERED boat drifts well clear — the sea takes her");
        }

        // ============ Part 1b: the SLACK / catenary visual helpers ============

        [Test]
        public void Slack01_IsOneAtTheTie_ZeroAtTheLimit()
        {
            Assert.AreEqual(1f, BoatMooring.Slack01(0f, RopeLen), 1e-4f, "on top of the tie the rope is fully slack");
            Assert.AreEqual(0f, BoatMooring.Slack01(RopeLen, RopeLen), 1e-4f, "at rope-length the rope is taut (no slack)");
            Assert.AreEqual(0.5f, BoatMooring.Slack01(RopeLen * 0.5f, RopeLen), 1e-4f, "halfway out it's half slack");
            Assert.AreEqual(0f, BoatMooring.Slack01(RopeLen * 2f, RopeLen), 1e-4f, "past the limit clamps to taut (no negative slack)");
        }

        [Test]
        public void SampleRopeCurve_SlackRopeDroops_TautRopeIsStraight()
        {
            var tie = new Vector2(0f, 5f);
            var buffer = new Vector2[9];

            // SLACK (boat sits on the tie): the rope bellies DOWN — the midpoint sags below the straight line.
            BoatMooring.SampleRopeCurve(tie, tie, RopeLen, /*maxSag*/0.8f, buffer);
            Vector2 mid = buffer[buffer.Length / 2];
            Assert.Less(mid.y, tie.y, "a slack rope droops (the belly sags below the endpoints)");

            // TAUT (boat at the rope's end): the line is straight — no sag.
            var taut = tie + new Vector2(RopeLen, 0f);
            BoatMooring.SampleRopeCurve(tie, taut, RopeLen, 0.8f, buffer);
            Vector2 straightMid = Vector2.Lerp(tie, taut, 0.5f);
            Assert.AreEqual(straightMid.y, buffer[buffer.Length / 2].y, 1e-4f, "a taut rope is a straight line (no droop)");
            // Endpoints always anchor exactly at the tie and the boat.
            Assert.AreEqual(tie, buffer[0]);
            Assert.AreEqual(taut, buffer[buffer.Length - 1]);
        }

        // ============ Part 2: the deterministic DRIFT force (moored boat drifts on its leash) ============

        [Test]
        public void DriftForce_AtRest_PushesWithWindAndCurrent()
        {
            var hull = Hull("boat.dory", PropulsionType.Oars);
            // Idle hull, a wind from the west and a current to the north: the net drift force has a component
            // along each (the boat sets with the weather — P1).
            Vector2 wind = new Vector2(5f, 0f), current = new Vector2(0f, 2f);
            Vector2 f = BoatMooring.DriftForce(Vector2.zero, Vector2.up, wind, current,
                                               hull.ForwardDrag, hull.LateralDrag, hull.WindExposure);
            Assert.Greater(f.x, 0f, "wind from the west shoves the idle boat east (sets with the wind)");
            Assert.Greater(f.y, 0f, "the tidal current carries the idle boat north (sets with the tide)");
        }

        [Test]
        public void DriftForce_NoWeather_IsZero()
        {
            var hull = Hull("boat.dory", PropulsionType.Oars);
            Vector2 f = BoatMooring.DriftForce(Vector2.zero, Vector2.up, Vector2.zero, Vector2.zero,
                                               hull.ForwardDrag, hull.LateralDrag, hull.WindExposure);
            Assert.AreEqual(Vector2.zero, f, "dead calm slack water → an idle boat doesn't drift");
        }

        [Test]
        public void DriftForce_DragOpposesMotionThroughTheWater()
        {
            var hull = Hull("boat.dory", PropulsionType.Oars);
            // Moving with no wind/current: drag (relative to still water) opposes the motion → a brake.
            Vector2 vel = new Vector2(0f, 3f);   // making way north
            Vector2 f = BoatMooring.DriftForce(vel, Vector2.up, Vector2.zero, Vector2.zero,
                                               hull.ForwardDrag, hull.LateralDrag, hull.WindExposure);
            Assert.Less(f.y, 0f, "hull drag opposes motion through the water (the boat coasts to a stop)");
        }

        [Test]
        public void DriftForce_IsBitStable_ForIdenticalInputs()
        {
            var hull = Hull("boat.dory", PropulsionType.Oars);
            Vector2 wind = new Vector2(3.3f, -1.7f), current = new Vector2(-0.5f, 0.9f);
            Vector2 a = BoatMooring.DriftForce(new Vector2(0.2f, -0.4f), Vector2.up, wind, current,
                                               hull.ForwardDrag, hull.LateralDrag, hull.WindExposure);
            Vector2 b = BoatMooring.DriftForce(new Vector2(0.2f, -0.4f), Vector2.up, wind, current,
                                               hull.ForwardDrag, hull.LateralDrag, hull.WindExposure);
            Assert.AreEqual(a, b, "the drift force is deterministic — identical inputs, identical output (no RNG)");
        }

        [Test]
        public void TetherForce_IsBitStable_ForIdenticalInputs()
        {
            var tie = new Vector2(1f, 2f);
            var pos = new Vector2(7f, 2f);
            var vel = new Vector2(2f, -1f);
            Vector2 a = BoatMooring.TetherForce(pos, tie, RopeLen, Stiff, vel, Damp, Give);
            Vector2 b = BoatMooring.TetherForce(pos, tie, RopeLen, Stiff, vel, Damp, Give);
            Assert.AreEqual(a, b, "the firm-limit tether force is deterministic — no hidden RNG");
        }

        // ============ Part 3: the hold/root STATE MACHINE (BoatMooring) ============

        private BoatMooring NewMooredBoat(BoatHullDef hull, out BoatController boat, out Rigidbody2D rb)
        {
            var go = new GameObject("Boat");
            _spawned.Add(go);
            boat = go.AddComponent<BoatController>();   // RequireComponent pulls in Rigidbody2D + BoatMooring
            boat.SetHull(hull);
            rb = go.GetComponent<Rigidbody2D>();
            return go.GetComponent<BoatMooring>();
        }

        private Transform NewPlayerAt(Vector3 pos)
        {
            var go = new GameObject("PlayerAnchor"); go.transform.position = pos; _spawned.Add(go);
            return go.transform;
        }

        [Test]
        public void BoatController_RequiresAMooring()
        {
            var go = new GameObject("Boat");
            _spawned.Add(go);
            go.AddComponent<BoatController>();
            Assert.IsNotNull(go.GetComponent<BoatMooring>(),
                             "every boat gets a rope — BoatController RequireComponent(BoatMooring)");
        }

        [Test]
        public void Mooring_StartsStowed()
        {
            var m = NewMooredBoat(Hull("boat.dory", PropulsionType.Oars), out _, out _);
            Assert.AreEqual(MooringState.Stowed, m.State, "a boat starts with its rope stowed (under way / aboard)");
            Assert.IsFalse(m.IsMoored);
        }

        [Test]
        public void Hold_TethersToThePlayer_AndParksTheBoat()
        {
            var m = NewMooredBoat(Hull("boat.dory", PropulsionType.Oars), out _, out var rb);
            rb.linearVelocity = new Vector2(3f, -2f);   // coasting
            rb.angularVelocity = 40f;
            var player = NewPlayerAt(new Vector3(7f, 7f, 0f));

            m.Hold(player);

            Assert.AreEqual(MooringState.HeldByPlayer, m.State, "holding takes the line in hand");
            Assert.IsTrue(m.IsHeld);
            Assert.AreEqual(new Vector2(7f, 7f), m.TiePoint, "the line is made fast to the player's hand (their position)");
            Assert.AreEqual(Vector2.zero, rb.linearVelocity, "holding brings the boat to rest (parks where left)");
            Assert.AreEqual(0f, rb.angularVelocity, 1e-4f, "…and stops her spinning");
        }

        [Test]
        public void Hold_TracksTheMovingPlayer()
        {
            var m = NewMooredBoat(Hull("boat.dory", PropulsionType.Oars), out _, out _);
            var player = NewPlayerAt(new Vector3(1f, 1f, 0f));
            m.Hold(player);
            Assert.AreEqual(new Vector2(1f, 1f), m.TiePoint, "the tie point starts at the player");
            player.position = new Vector3(9f, 3f, 0f);   // the player walks off
            Assert.AreEqual(new Vector2(9f, 3f), m.TiePoint, "the held line follows the player's live position");
        }

        [Test]
        public void Root_MakesFastToAFixedGroundSpot()
        {
            var m = NewMooredBoat(Hull("boat.dory", PropulsionType.Oars), out _, out _);
            m.Hold(NewPlayerAt(Vector3.zero));
            m.Root(new Vector2(5f, -2f));
            Assert.AreEqual(MooringState.RootedToGround, m.State, "rooting drops the line to the ground");
            Assert.IsTrue(m.IsRooted);
            Assert.AreEqual(new Vector2(5f, -2f), m.TiePoint, "the line is made fast to the fixed ground spot");
            Assert.IsTrue(m.IsMoored);
        }

        [Test]
        public void ToggleRoot_FlipsHeldAndRooted_AndStowIsDormant()
        {
            var m = NewMooredBoat(Hull("boat.dory", PropulsionType.Oars), out _, out _);
            var player = NewPlayerAt(new Vector3(2f, 0f, 0f));
            m.Hold(player);
            Assert.AreEqual(MooringState.RootedToGround, m.ToggleRoot(new Vector2(2f, 0f), player), "held → rooted at the feet");
            Assert.AreEqual(new Vector2(2f, 0f), m.TiePoint, "rooting makes fast at the player's feet");
            Assert.AreEqual(MooringState.HeldByPlayer, m.ToggleRoot(new Vector2(2f, 0f), player), "rooted → back in hand");

            m.Stow();
            Assert.AreEqual(MooringState.Stowed, m.State, "stow goes dormant (re-boarded)");
            Assert.AreEqual(MooringState.Stowed, m.ToggleRoot(Vector2.zero, player), "toggling while stowed is a no-op");
        }

        // ============ Part 4: the ControlSwitcher wiring (disembark holds · root key · board stows) ============

        private (ControlSwitcher sw, PlayerWalkController walk, BoatController boat, BoatMooring mooring, GameObject boatGo)
            BuildSwitcher(Vector3 playerPos, Vector3 boatPos, BoatHullDef hull)
        {
            var playerGo = new GameObject("Player"); playerGo.transform.position = playerPos; _spawned.Add(playerGo);
            var walk = playerGo.AddComponent<PlayerWalkController>();
            var boatGo = new GameObject("Boat"); boatGo.transform.position = boatPos; _spawned.Add(boatGo);
            var boat = boatGo.AddComponent<BoatController>();   // pulls in BoatMooring
            var input = boatGo.AddComponent<DevBoatInput>();
            var mooring = boatGo.GetComponent<BoatMooring>();
            boat.SetHull(hull);
            walk.enabled = true; boat.enabled = false; input.enabled = false;

            var dock = new GameObject("Dock"); dock.transform.position = new Vector3(0f, -12f, 0f); _spawned.Add(dock);
            var dis = new GameObject("Dis"); dis.transform.position = new Vector3(0f, -10.5f, 0f); _spawned.Add(dis);
            var swGo = new GameObject("Switcher"); _spawned.Add(swGo);
            var sw = swGo.AddComponent<ControlSwitcher>();
            sw.Configure(walk, boat, input, dock.transform, 3f, dis.transform);
            return (sw, walk, boat, mooring, boatGo);
        }

        // Exposed land so the boat counts as 'over land' (the disembark gate): ground 0.2 m, water level 0.
        private void WireExposedLand()
        {
            GameServices.TidalTerrain = new FlatTerrain { Elevation = 0.2f };
            GameServices.Environment = new WindEnv { Level = 0f };   // depth -0.2 m ≤ 0 = standable land
        }

        [Test]
        public void Disembark_OnLand_HoldsTheRope_TetheredToThePlayer()
        {
            WireExposedLand();

            var (sw, walk, _, mooring, boatGo) = BuildSwitcher(new Vector3(0f, -11.5f, 0f), new Vector3(0f, -13.8f, 0f),
                                                               Hull("boat.dory", PropulsionType.Oars));
            sw.TryInteract();                                  // board (within reach of the boat)
            boatGo.transform.position = new Vector3(40f, 40f, 0f);   // sailed up onto an exposed flat
            Assert.IsTrue(sw.TryInteract(), "disembark onto land succeeds");

            Assert.AreEqual(ControlMode.OnFoot, sw.Mode);
            Assert.AreEqual(MooringState.HeldByPlayer, mooring.State,
                            "disembark HOLDS the rope by default (boat tethered to the player's hand)");
            // The player stepped off at the boat, so the line is made fast right there.
            Assert.AreEqual((Vector2)walk.transform.position, mooring.TiePoint, "the held line follows the player");
        }

        [Test]
        public void ToggleMooring_OnFootBesideTheBoat_Roots_ThenTakesBackInHand()
        {
            WireExposedLand();

            var (sw, walk, _, mooring, boatGo) = BuildSwitcher(new Vector3(0f, -11.5f, 0f), new Vector3(0f, -13.8f, 0f),
                                                               Hull("boat.dory", PropulsionType.Oars));
            sw.TryInteract();                                  // board
            boatGo.transform.position = new Vector3(40f, 40f, 0f);
            sw.TryInteract();                                  // disembark → held, player at the boat
            walk.transform.position = boatGo.transform.position;   // standing right by her

            Assert.IsTrue(sw.CanToggleMooring(), "on foot beside a moored boat, the rope is in reach");
            Assert.IsTrue(sw.ToggleMooring(), "root it to the ground");
            Assert.AreEqual(MooringState.RootedToGround, mooring.State, "Q roots the line at the feet → the player can roam");
            Assert.AreEqual((Vector2)walk.transform.position, mooring.TiePoint, "rooted at the player's feet");

            Assert.IsTrue(sw.ToggleMooring(), "take the rope back in hand");
            Assert.AreEqual(MooringState.HeldByPlayer, mooring.State, "Q again takes the line back in hand");
        }

        [Test]
        public void ToggleMooring_FarFromTheBoat_IsRefused()
        {
            WireExposedLand();

            var (sw, walk, _, mooring, boatGo) = BuildSwitcher(new Vector3(0f, -11.5f, 0f), new Vector3(0f, -13.8f, 0f),
                                                               Hull("boat.dory", PropulsionType.Oars));
            sw.TryInteract();
            boatGo.transform.position = new Vector3(40f, 40f, 0f);
            sw.TryInteract();                                  // held
            sw.ToggleMooring();                                // root it so it stays put when the player leaves
            walk.transform.position = new Vector3(80f, 80f, 0f);   // walked well away (the rope is rooted)

            Assert.IsFalse(sw.CanToggleMooring(), "you can't fiddle the rope from across the harbour");
            Assert.IsFalse(sw.ToggleMooring());
            Assert.AreEqual(MooringState.RootedToGround, mooring.State, "so the rooted line stays as it was (still safe)");
        }

        [Test]
        public void Board_StowsTheRope()
        {
            WireExposedLand();

            var (sw, walk, _, mooring, boatGo) = BuildSwitcher(new Vector3(0f, -11.5f, 0f), new Vector3(0f, -13.8f, 0f),
                                                               Hull("boat.dory", PropulsionType.Oars));
            sw.TryInteract(); boatGo.transform.position = new Vector3(40f, 40f, 0f); sw.TryInteract();   // held
            Assert.AreEqual(MooringState.HeldByPlayer, mooring.State);

            // Walk back to the boat and re-board (board from anywhere within reach of the boat).
            walk.transform.position = boatGo.transform.position;
            Assert.IsTrue(sw.TryInteract(), "re-board");
            Assert.AreEqual(ControlMode.Aboard, sw.Mode);
            Assert.AreEqual(MooringState.Stowed, mooring.State, "boarding stows the rope (the helm takes over)");
        }
    }
}
