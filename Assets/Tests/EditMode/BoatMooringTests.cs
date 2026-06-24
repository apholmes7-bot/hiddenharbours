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
    /// through the pure tether/drift helpers (no physics loop) and the tie/untie state machine on both
    /// <see cref="BoatMooring"/> and the Player-lane <see cref="ControlSwitcher"/>:
    ///
    ///   • TETHERED — a boat pushed by wind/tide is held within rope-length of the tie point (the leash):
    ///     the rope force is zero inside rope-length (bobs free) and pulls inward beyond it; an integrated
    ///     drift-under-constant-force never escapes the rope circle, while the same drift UNtied runs away.
    ///   • UNTIED  — the deterministic wind+tide drift force sets an idle hull moving (the teeth).
    ///   • STATE MACHINE — disembark ties (parks + tethered, the cozy default); the tie/untie key casts off
    ///     to drift and re-ties; re-boarding stows the rope.
    ///   • DETERMINISM — the tether + drift forces are bit-stable for identical inputs (no hidden RNG).
    /// </summary>
    public class BoatMooringTests
    {
        // A flat deterministic terrain + environment so the switcher's NearShore() depth read is stable.
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

        // ============ Part 1: the TETHER force (stays within rope length under force) ============

        [Test]
        public void TetherForce_SlackInsideRopeLength_IsZero()
        {
            var tie = new Vector2(0f, 0f);
            // Inside the rope circle the boat bobs/swings freely — no rope force at all.
            Assert.AreEqual(Vector2.zero, BoatMooring.TetherForce(new Vector2(0f, 0f), tie, 4f, 90f, Vector2.zero, 18f),
                            "at the tie point the rope is slack");
            Assert.AreEqual(Vector2.zero, BoatMooring.TetherForce(new Vector2(3.9f, 0f), tie, 4f, 90f, Vector2.zero, 18f),
                            "just inside rope-length the rope is still slack (free swing)");
            Assert.IsFalse(BoatMooring.IsBeyondRope(new Vector2(3.9f, 0f), tie, 4f), "3.9 m is within a 4 m rope");
        }

        [Test]
        public void TetherForce_BeyondRopeLength_PullsBackTowardTheTiePoint()
        {
            var tie = new Vector2(0f, 0f);
            // The boat has been blown out to 6 m on a 4 m rope → the rope goes taut and pulls inward.
            Vector2 f = BoatMooring.TetherForce(new Vector2(6f, 0f), tie, 4f, 90f, Vector2.zero, 18f);

            Assert.IsTrue(BoatMooring.IsBeyondRope(new Vector2(6f, 0f), tie, 4f), "6 m is past a 4 m rope (taut)");
            Assert.Less(f.x, 0f, "the rope pulls the boat back toward the tie point (inward = -x here)");
            Assert.AreEqual(0f, f.y, 1e-4f, "the pull is purely radial (along the rope)");
            // Spring magnitude = overshoot(2 m) * stiffness(90) = 180 (snub is 0 at rest).
            Assert.AreEqual(-180f, f.x, 1e-3f, "spring force = (dist - ropeLength) * stiffness");
        }

        [Test]
        public void TetherForce_NeverPushesOutward_RopeOnlyPulls()
        {
            var tie = new Vector2(0f, 0f);
            // A boat surging INWARD (toward the tie) past the rope: spring still pulls in, snub must NOT add
            // outward force (a rope can't shove the boat off its tie). Net radial force stays inward.
            Vector2 inwardVel = new Vector2(-5f, 0f);   // moving toward the tie
            Vector2 f = BoatMooring.TetherForce(new Vector2(6f, 0f), tie, 4f, 90f, inwardVel, 18f);
            Assert.LessOrEqual(f.x, 0f, "even with inward velocity the rope force is never outward (it only pulls)");

            // A boat surging OUTWARD past the rope: snub adds extra inward braking (eases onto the rope).
            Vector2 outwardVel = new Vector2(+5f, 0f);
            Vector2 fOut = BoatMooring.TetherForce(new Vector2(6f, 0f), tie, 4f, 90f, outwardVel, 18f);
            Assert.Less(fOut.x, f.x, "surging outward, the snub adds more inward braking than at rest/inbound");
        }

        [Test]
        public void TetherForce_StifferRope_PullsHarder()
        {
            var tie = Vector2.zero; var pos = new Vector2(6f, 0f);
            float soft = BoatMooring.TetherForce(pos, tie, 4f, 50f, Vector2.zero, 0f).magnitude;
            float stiff = BoatMooring.TetherForce(pos, tie, 4f, 150f, Vector2.zero, 0f).magnitude;
            Assert.Greater(stiff, soft, "a stiffer tether checks the boat harder (tunable feel, no magic number)");
        }

        [Test]
        public void TetheredBoat_UnderConstantDrift_StaysWithinRopeLength()
        {
            // Integrate a tethered boat under a CONSTANT outward drift force and confirm it never escapes the
            // rope circle — the core guarantee. Pure explicit-Euler over the helpers (deterministic, no Unity
            // physics needed). The same drift UNtied is shown to run away in the next test.
            var tie = Vector2.zero;
            float ropeLen = 4f, stiffness = 120f, snub = 45f;   // a snubbed rope eases her onto the leash
            float mass = 4f, dt = 0.02f;
            Vector2 driftForce = new Vector2(60f, 0f);   // a steady wind/tide shove outward

            Vector2 pos = tie, vel = Vector2.zero;
            float maxDist = 0f;
            for (int i = 0; i < 4000; i++)   // 80 s of sim
            {
                Vector2 tether = BoatMooring.TetherForce(pos, tie, ropeLen, stiffness, vel, snub);
                Vector2 a = (driftForce + tether) / mass;
                vel += a * dt;
                pos += vel * dt;
                maxDist = Mathf.Max(maxDist, (pos - tie).magnitude);
            }
            // Held on the leash: it reaches the rope and is checked, settling near rope-length, never far
            // past (a small spring overshoot is expected — that's the rope stretching, not escaping).
            Assert.Less(maxDist, ropeLen + 1.0f, "a tethered boat stays within (about) rope-length under steady drift");
            Assert.Greater((pos - tie).magnitude, ropeLen - 1.0f, "…and it DOES swing out to the end of its leash");
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
            Assert.Greater(pos.magnitude, 50f, "an UNTIED boat drifts well clear — the sea takes her");
        }

        // ============ Part 2: the deterministic DRIFT force (untied boat drifts) ============

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

        // ============ Part 3: the tie/untie STATE MACHINE (BoatMooring) ============

        private BoatMooring NewMooredBoat(BoatHullDef hull, out BoatController boat, out Rigidbody2D rb)
        {
            var go = new GameObject("Boat");
            _spawned.Add(go);
            boat = go.AddComponent<BoatController>();   // RequireComponent pulls in Rigidbody2D + BoatMooring
            boat.SetHull(hull);
            rb = go.GetComponent<Rigidbody2D>();
            return go.GetComponent<BoatMooring>();
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
        public void TieTo_Tethers_AndParksTheBoat()
        {
            var m = NewMooredBoat(Hull("boat.dory", PropulsionType.Oars), out _, out var rb);
            rb.linearVelocity = new Vector2(3f, -2f);   // coasting
            rb.angularVelocity = 40f;

            m.TieTo(new Vector2(7f, 7f));

            Assert.AreEqual(MooringState.Tethered, m.State, "tying makes her fast (tethered)");
            Assert.IsTrue(m.IsTethered);
            Assert.AreEqual(new Vector2(7f, 7f), m.TiePoint, "the rope is made fast where the player stood");
            Assert.AreEqual(Vector2.zero, rb.linearVelocity, "tying brings the boat to rest (parks where left)");
            Assert.AreEqual(0f, rb.angularVelocity, 1e-4f, "…and stops her spinning");
        }

        [Test]
        public void Untie_CastsOff_ToDriftFree()
        {
            var m = NewMooredBoat(Hull("boat.dory", PropulsionType.Oars), out _, out _);
            m.TieTo(Vector2.zero);
            m.Untie();
            Assert.AreEqual(MooringState.AdriftUntied, m.State, "untying casts her off to drift");
            Assert.IsTrue(m.IsAdrift);
            Assert.IsTrue(m.IsMoored, "she's still 'moored' in the sense the rope is in play (a loose end)");
        }

        [Test]
        public void ToggleTie_FlipsTetheredAndAdrift_AndStowIsDormant()
        {
            var m = NewMooredBoat(Hull("boat.dory", PropulsionType.Oars), out _, out _);
            m.TieTo(Vector2.zero);
            Assert.AreEqual(MooringState.AdriftUntied, m.ToggleTie(new Vector2(2f, 0f)), "tethered → cast off");
            Assert.AreEqual(MooringState.Tethered, m.ToggleTie(new Vector2(2f, 0f)), "adrift → tied again");
            Assert.AreEqual(new Vector2(2f, 0f), m.TiePoint, "re-tying makes fast at the new spot");

            m.Stow();
            Assert.AreEqual(MooringState.Stowed, m.State, "stow goes dormant (re-boarded)");
            Assert.AreEqual(MooringState.Stowed, m.ToggleTie(Vector2.zero), "toggling while stowed is a no-op");
        }

        // ============ Part 4: the ControlSwitcher wiring (disembark ties · key casts off · board stows) ============

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

        [Test]
        public void Disembark_NearShore_TiesTheBoat_NotJustParks()
        {
            // Shoaled terrain so the boat is "near shore" away from the dock.
            GameServices.TidalTerrain = new FlatTerrain { Elevation = 0.2f };
            GameServices.Environment = new WindEnv { Level = 0.6f };   // depth 0.4 m → near shore

            var (sw, _, _, mooring, boatGo) = BuildSwitcher(new Vector3(0f, -11.5f, 0f), new Vector3(0f, -13.8f, 0f),
                                                            Hull("boat.dory", PropulsionType.Oars));
            sw.TryInteract();                                  // board
            boatGo.transform.position = new Vector3(40f, 40f, 0f);   // sailed up to a shore
            Assert.IsTrue(sw.TryInteract(), "disembark near the shore succeeds");

            Assert.AreEqual(ControlMode.OnFoot, sw.Mode);
            Assert.AreEqual(MooringState.Tethered, mooring.State,
                            "disembark TIES the boat by default (parks + tethered, the cozy safety)");
        }

        [Test]
        public void ToggleMooring_OnFootBesideTheBoat_CastsOff_ThenReTies()
        {
            GameServices.TidalTerrain = new FlatTerrain { Elevation = 0.2f };
            GameServices.Environment = new WindEnv { Level = 0.6f };

            var (sw, walk, _, mooring, boatGo) = BuildSwitcher(new Vector3(0f, -11.5f, 0f), new Vector3(0f, -13.8f, 0f),
                                                               Hull("boat.dory", PropulsionType.Oars));
            sw.TryInteract();                                  // board
            boatGo.transform.position = new Vector3(40f, 40f, 0f);
            sw.TryInteract();                                  // disembark → tethered, player at the boat
            walk.transform.position = boatGo.transform.position;   // standing right by her

            Assert.IsTrue(sw.CanToggleMooring(), "on foot beside a moored boat, the rope is in reach");
            Assert.IsTrue(sw.ToggleMooring(), "cast off");
            Assert.AreEqual(MooringState.AdriftUntied, mooring.State, "Q casts her off → she'll drift (teeth)");

            Assert.IsTrue(sw.ToggleMooring(), "tie up again");
            Assert.AreEqual(MooringState.Tethered, mooring.State, "Q again makes her fast → held on the leash");
        }

        [Test]
        public void ToggleMooring_FarFromTheBoat_IsRefused()
        {
            GameServices.TidalTerrain = new FlatTerrain { Elevation = 0.2f };
            GameServices.Environment = new WindEnv { Level = 0.6f };

            var (sw, walk, _, mooring, boatGo) = BuildSwitcher(new Vector3(0f, -11.5f, 0f), new Vector3(0f, -13.8f, 0f),
                                                               Hull("boat.dory", PropulsionType.Oars));
            sw.TryInteract();
            boatGo.transform.position = new Vector3(40f, 40f, 0f);
            sw.TryInteract();                                  // tethered
            walk.transform.position = new Vector3(80f, 80f, 0f);   // walked well away

            Assert.IsFalse(sw.CanToggleMooring(), "you can't fiddle the rope from across the harbour");
            Assert.IsFalse(sw.ToggleMooring());
            Assert.AreEqual(MooringState.Tethered, mooring.State, "so an untouched boat stays tied (still safe)");
        }

        [Test]
        public void Board_StowsTheRope()
        {
            GameServices.TidalTerrain = new FlatTerrain { Elevation = 0.2f };
            GameServices.Environment = new WindEnv { Level = 0.6f };

            var (sw, walk, _, mooring, boatGo) = BuildSwitcher(new Vector3(0f, -11.5f, 0f), new Vector3(0f, -13.8f, 0f),
                                                               Hull("boat.dory", PropulsionType.Oars));
            sw.TryInteract(); boatGo.transform.position = new Vector3(40f, 40f, 0f); sw.TryInteract();   // tethered
            Assert.AreEqual(MooringState.Tethered, mooring.State);

            // Walk back to the boat and re-board (the boat is its own board zone proxy: place player at boat,
            // dock at boat so InBoardZone passes — we just need Board() to run and stow the rope).
            walk.transform.position = boatGo.transform.position;
            sw.SetDock(boatGo.transform, boatGo.transform);
            Assert.IsTrue(sw.TryInteract(), "re-board");
            Assert.AreEqual(ControlMode.Aboard, sw.Mode);
            Assert.AreEqual(MooringState.Stowed, mooring.State, "boarding stows the rope (the helm takes over)");
        }
    }
}
