using System.Collections.Generic;
using System.Globalization;
using System.IO;
using HiddenHarbours.Core;
using HiddenHarbours.Vehicles;
using NUnit.Framework;
using UnityEngine;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// ⭐ <b>THE OTTER SWIMS</b> (ADR 0035's amphibian follow-up) — the drive⇄swim swap, and the three
    /// things that would be worst to get wrong.
    ///
    /// <para><b>What is actually at risk, and why each of these is a test rather than a read-through.</b>
    /// The whole feature is ONE depth read given a second meaning, which is the shape that fails
    /// quietly:</para>
    /// <list type="bullet">
    /// <item><b>SABOTAGE ARM 1 — the Dually must not learn to swim.</b> A rule that was holding a
    /// three-and-a-half-tonne truck out of the harbour has just been given an arm that says "and for
    /// this other machine, drive on in". A regression here does not throw and does not look wrong
    /// until a truck is floating.</item>
    /// <item><b>SABOTAGE ARM 2 — the swap is deterministic (rule 5).</b> Tide and terrain are
    /// recomputed from <c>(worldSeed, gameTime)</c> and never saved, so the same machine at the same
    /// place at the same instant must reach the same medium, bit for bit. Anything that crept a
    /// frame-rate term or a stored value in would show as a machine that floats on one save-load and
    /// not the next.</item>
    /// <item><b>SABOTAGE ARM 3 — the waterline clamp is HERS, and only while she is in the water.</b>
    /// Ashore she must carry the road vehicle's 0/0 (the "clamp off" #560 ships), afloat her own
    /// published numbers, and never the HULL defaults — those are a boat's numbers about a boat's
    /// deck.</item>
    /// </list>
    ///
    /// <para>The defs are built in code (the <c>VehicleControllerTests</c> pattern) so these pin the
    /// MODEL rather than the owner's tuning — with two deliberate exceptions at the end, which pin the
    /// shipped ASSET, and the numbers this fixture uses, back to the art side's own published file.</para>
    ///
    /// <para><b>⚠️ Her mesh is not baked</b> (<c>VehicleRigFleet.NotBaked["otter8x8"]</c>: 17 materials
    /// against the facet shader's 16). Nothing below needs it — every number here is published in her
    /// sidecar, and none of the mechanics depends on her geometry being on disk. The pairing of "no
    /// mesh" with "excused bake" is pinned in both directions by
    /// <c>VehicleRigFleetTests.TheOttersDefIsMeshLessExactlyWhileHerBakeIsBlocked</c>, which lives
    /// beside the fleet table it reads.</para>
    /// </summary>
    public class AmphibiousVehicleTests
    {
        // ---- HER numbers, from the 2026-08-17 rig and sidecar -------------------------------------

        const float TrackWidth = 1.26f;      // WHEELS.track_width_m
        const float WheelRadius = 0.32f;     // WHEELS.radius_m
        const float FloatSink = 0.52f;       // FLOAT.sink_m_at_float_1 / G.sinkMax
        const float FloatDraft = 0.28f;      // FLOAT.at_float_1.draft_above_keel_m
        const float WaterlineHalfBeam = 0.687f;   // FLOAT.waterline_polygon_at_float_1.max_half_beam_m
        const float LowestGunwale = 0.82f;   // FLOAT.downflooding.lowest_gunwale_point.z
        const float TireShowsAfloat = 0.12f; // FLOAT.at_float_1.wheels — of a 0.64 m diameter
        const float Hysteresis = 0.06f;      // her def's tuned dead band
        const int PxPerMetre = 32;           // frame.scale_px_per_m

        const string SidecarPath =
            "docs/art/rigs/gameplay/vehicles/amphibIsoRig.otter8x8.gameplay.json";
        const string OtterDefPath = "Assets/_Project/Data/Vehicles/Otter8x8.asset";

        // ---- the world ---------------------------------------------------------------------------

        /// <summary>Flat authored seabed at a constant elevation — with the water level fixed, depth
        /// is one number everywhere and "is she afloat?" has an exact answer. The same shape
        /// <c>VehicleDriveModeTests</c> uses, so the two gates read the same world.</summary>
        sealed class FlatTerrain : ITidalTerrain
        {
            public float Elevation;
            public float ElevationAt(Vector2 worldPos) => Elevation;
        }

        /// <summary>A water level that is a pure function of the time asked for — so the determinism
        /// arm can re-ask the SAME instant and get the same answer, and a different instant and get a
        /// different one.</summary>
        sealed class TidalEnv : IEnvironmentService
        {
            public float Level;
            /// <summary>Metres of rise per second of game time. 0 = a scripted still level.</summary>
            public float RisePerSecond;
            public int WorldSeed => 4242;
            public TideProfile ActiveTideProfile { get; set; }
            public EnvironmentSample Sample() => default;
            public float TideHeightAt(double totalSeconds) => Level + RisePerSecond * (float)totalSeconds;
            public float WaterLevelAt(double totalSeconds) => TideHeightAt(totalSeconds);
        }

        /// <summary>Dry to the west of <see cref="ShoreX"/>, drowned to the east — the coastline
        /// <c>VehicleDriveModeTests</c> holds the Dually against, reused verbatim so the road gate is
        /// re-checked in exactly the world it was written for.</summary>
        sealed class HalfDrownedCoast : ITidalTerrain
        {
            public const float ShoreX = 10f;
            public float ElevationAt(Vector2 worldPos) => worldPos.x < ShoreX ? 6f : -6f;
        }

        /// <summary>A renderer that records every pose channel written to it — headless, draws
        /// nothing, and is the only way to assert the VALUES the flotation path passes without
        /// rendering a pixel.</summary>
        sealed class RecordingRenderer : IHullMeshRenderer
        {
            public float HeadingDirUnits { get; set; }
            public float RollDegrees { get; set; }
            public float PitchDegrees { get; set; }
            public float HeavePixels { get; set; }
            public float RidePixels { get; set; }
            public bool IsConfigured => true;
            public void SetSorting(int sortingLayerId, int sortingOrder) { }
            public void SetDeckOccupant(Vector3 rigLocalMeters, bool active) { }
            public float DeckOccluderId => 0f;
            public IDeckOccupantSlots DeckOccupants => NoDeckOccupantSlots.Instance;
        }

        /// <summary>A presentation service that records the waterline clamp it is handed and does
        /// nothing else. Installing a real renderer needs a GPU; the clamp is a pair of floats
        /// crossing a Core seam, and that is exactly what SABOTAGE ARM 3 is about.</summary>
        sealed class RecordingPresentation : IVehicleMeshPresentationService
        {
            public readonly List<(float deck, float halfBeam)> Clamps = new();
            public IHullMeshRenderer Install(GameObject host, VehicleMeshDef def) => null;
            public IHullPropRenderer AttachWheel(GameObject host, HullPropMeshDef def, string slot) => null;
            public void Remove(GameObject host) { }
            public void SetWaterlineClamp(GameObject host, float deckHeightMeters, float halfBeamMeters)
                => Clamps.Add((deckHeightMeters, halfBeamMeters));
        }

        // ---- the fixture -------------------------------------------------------------------------

        readonly List<Object> _spawned = new();
        readonly List<ControlModeChanged> _modes = new();
        IVehicleMeshPresentationService _service;

        VehicleDef _otter, _dually;
        VehicleMeshDef _otterMesh, _duallyMesh;

        void OnMode(ControlModeChanged e) => _modes.Add(e);

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();
            _service = VehicleMeshPresentation.Service;
            _modes.Clear();
            EventBus.Clear<ControlModeChanged>();
            EventBus.Subscribe<ControlModeChanged>(OnMode);
            SilenceTheSea();

            // HER rig, as her sidecar publishes it. Skid-steered is expressed the way the ART
            // expresses it: NO lock angle, because she has no steering axle to have one.
            _otterMesh = ScriptableObject.CreateInstance<VehicleMeshDef>();
            _otterMesh.Id = "vehiclemesh.otter_8x8";
            _otterMesh.WheelbaseMeters = 2.04f;          // WHEELS.axles_y, first to last
            _otterMesh.FrontTrackMeters = TrackWidth;
            _otterMesh.WheelRadiusMeters = WheelRadius;
            _otterMesh.MaxInnerSteerDegrees = 0f;        // ⭐ no steer axle — _excluded.steering_geometry
            _otterMesh.MaxOuterSteerDegrees = 0f;
            _otterMesh.FrontAxleY = 1.02f;
            _otterMesh.RearAxleY = -1.02f;
            _otterMesh.PxPerMetre = PxPerMetre;
            _otterMesh.FloatSinkMeters = FloatSink;
            _otterMesh.FloatDraftMeters = FloatDraft;
            _otterMesh.WatertightDeckHeightMeters = LowestGunwale;
            _otterMesh.WatertightHalfBeamMeters = WaterlineHalfBeam;
            _otterMesh.DriveDoorLocal = new Vector2(-1.4f, 0.2f);   // INTERACT "drive" reach_point

            _otter = ScriptableObject.CreateInstance<VehicleDef>();
            _otter.Id = "vehicle.otter_8x8";
            _otter.KindToken = "amphibious_vehicle";
            _otter.Mesh = _otterMesh;
            _otter.MaxSpeedMetersPerSecond = 8f;
            _otter.MaxReverseSpeedMetersPerSecond = 3f;
            _otter.AccelerationMetersPerSecondSquared = 3.5f;
            _otter.BrakingMetersPerSecondSquared = 6f;
            _otter.CoastDecelerationMetersPerSecondSquared = 2.6f;
            _otter.SteerRateFullLocksPerSecond = 3f;
            _otter.SteerReturnFullLocksPerSecond = 4f;
            _otter.SteerFalloffHalfSpeedMetersPerSecond = 0f;
            _otter.SpinRateDegreesPerSecond = 75f;
            _otter.AfloatSpinRateDegreesPerSecond = 30f;
            _otter.AfloatMaxSpeedMetersPerSecond = 1.6f;
            _otter.AfloatMaxReverseSpeedMetersPerSecond = 0.9f;
            _otter.AfloatAccelerationMetersPerSecondSquared = 0.8f;
            _otter.AfloatDragDecelerationMetersPerSecondSquared = 1.2f;
            _otter.SwimHysteresisMeters = Hysteresis;

            // The Dually, for the sabotage contrasts — her own published numbers.
            _duallyMesh = ScriptableObject.CreateInstance<VehicleMeshDef>();
            _duallyMesh.WheelbaseMeters = 4.30f;
            _duallyMesh.FrontTrackMeters = 1.80f;
            _duallyMesh.WheelRadiusMeters = 0.42f;
            _duallyMesh.MaxInnerSteerDegrees = 30f;
            _duallyMesh.MaxOuterSteerDegrees = 24.9372f;
            _duallyMesh.FrontAxleY = 2.18f;
            _duallyMesh.RearAxleY = -2.12f;
            _duallyMesh.PxPerMetre = PxPerMetre;

            _dually = ScriptableObject.CreateInstance<VehicleDef>();
            _dually.Id = "vehicle.dually_3500";
            _dually.KindToken = "road_vehicle";
            _dually.Mesh = _duallyMesh;
            _dually.MaxSpeedMetersPerSecond = 11f;
            _dually.MaxReverseSpeedMetersPerSecond = 4f;
            _dually.AccelerationMetersPerSecondSquared = 4.5f;
            _dually.BrakingMetersPerSecondSquared = 8f;
            _dually.CoastDecelerationMetersPerSecondSquared = 2.2f;
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Unsubscribe<ControlModeChanged>(OnMode);
            EventBus.Clear<ControlModeChanged>();
            VehicleMeshPresentation.Service = _service;
            SilenceTheSea();
            GameServices.Reset();
            foreach (Object o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
            Object.DestroyImmediate(_otter);
            Object.DestroyImmediate(_otterMesh);
            Object.DestroyImmediate(_dually);
            Object.DestroyImmediate(_duallyMesh);
        }

        /// <summary>
        /// ⭐ <b>Take ownership of the displaced-sea seams, then clear them</b>, so the flotation
        /// assertions below read a KNOWN flat sea.
        ///
        /// <para><c>DisplacedSea</c> and <c>SharedWaveField</c> are process-global statics that
        /// <c>GameServices.Reset()</c> deliberately does not touch (they are presentation wiring, not
        /// game state), and each will only let its CURRENT owner clear it — so a fixture that
        /// published and did not clean up would otherwise leak a live sea into this one, and
        /// <see cref="AfloatSheSinksByHerMeasuredFloatOffsetAndTakesNoSeakeeping"/> would read a ride
        /// it never asked for. Publishing first makes this fixture the owner, and the clear then
        /// works.</para>
        /// </summary>
        void SilenceTheSea()
        {
            DisplacedSea.Publish(this, default);
            DisplacedSea.Clear(this);
            SharedWaveField.Publish(this, WaveTrains.None);
            SharedWaveField.Clear(this);
        }

        /// <summary>A machine at a world point, nosed EAST (transform.up → +x). The transform is set
        /// BEFORE the body is added, which is the order <c>VehicleDriveModeTests</c> proved out.</summary>
        VehicleController MachinePointedEast(VehicleDef def, float x)
        {
            var go = new GameObject(def.Id);
            _spawned.Add(go);
            go.transform.position = new Vector3(x, 0f, 0f);
            go.transform.rotation = Quaternion.Euler(0f, 0f, -90f);
            go.AddComponent<Rigidbody2D>();
            var controller = go.AddComponent<VehicleController>();
            controller.SetVehicle(def);
            return controller;
        }

        /// <summary>Put a world under the fixture: flat ground at <paramref name="elevation"/> with the
        /// water at <paramref name="level"/>, so the depth everywhere is <c>level − elevation</c>.
        /// No clock is wired — the level here does not vary with time, and the live reads default to
        /// t = 0 when there is none.</summary>
        TidalEnv FlatWorld(float elevation, float level)
        {
            var env = new TidalEnv { Level = level };
            GameServices.TidalTerrain = new FlatTerrain { Elevation = elevation };
            GameServices.Environment = env;
            return env;
        }

        // =============================================================================================
        //  1. SABOTAGE ARM — THE DUALLY DOES NOT LEARN TO SWIM
        // =============================================================================================

        /// <summary>
        /// ⭐ <b>SABOTAGE ARM 1.</b> The road gate is untouched: a truck nosed at the water is still
        /// stopped dead by it, and can still always back off — the two claims #560 shipped, re-asserted
        /// through the kind-dispatched entry point that now sits in front of them.
        ///
        /// <para>If this ever goes green-then-red, the amphibian's arm has leaked into the road
        /// vehicle's, and nothing else in the suite would notice until a Dually was afloat.</para>
        /// </summary>
        [Test]
        public void ARoadVehicleAtTheWaterStillCapsAndCanStillBackOff()
        {
            GameServices.TidalTerrain = new HalfDrownedCoast();
            GameServices.Environment = new TidalEnv { Level = 0f };

            VehicleController truck = MachinePointedEast(_dually, HalfDrownedCoast.ShoreX);

            truck.Throttle = 1f;
            for (int i = 0; i < 100; i++) truck.StepPhysics(0.02f);
            Assert.That(truck.SpeedMetersPerSecond, Is.EqualTo(0f).Within(1e-4f),
                        "her leading wheel is in the water — she does not swim, and this PR must not " +
                        "have taught her to.");
            Assert.That(truck.IsAfloat, Is.False,
                        "a RoadVehicle can never be afloat. The flotation read must refuse her by " +
                        "KIND, before it ever looks at a depth.");

            truck.Throttle = -1f;
            for (int i = 0; i < 50; i++) truck.StepPhysics(0.02f);
            Assert.That(truck.SpeedMetersPerSecond, Is.LessThan(-0.5f),
                        "backing away must ALWAYS be allowed.");
        }

        /// <summary>The same claim at the arithmetic, so it holds for a machine the fixture never
        /// spawned: at ONE position, on ONE coastline, the road vehicle is held to what she can stop
        /// from and the amphibian is not held at all.</summary>
        [Test]
        public void TheKindDecidesWhetherWaterIsAWall()
        {
            var terrain = new HalfDrownedCoast();
            var env = new TidalEnv { Level = 0f };
            Vector2 approaching = new(HalfDrownedCoast.ShoreX - 4f, 0f);   // front axle 1.82 m short
            Vector2 east = Vector2.right;

            float road = VehicleGrounding.SpeedCapMetersPerSecond(
                VehicleKind.RoadVehicle, terrain, env, 0.0, approaching, east, speed: 5f,
                frontAxleY: 2.18f, rearAxleY: -2.12f, brakingMetersPerSecondSquared: 8f,
                wheelRadiusMeters: 0.42f);

            Assert.That(float.IsPositiveInfinity(road), Is.False,
                        "the road gate must still BE a gate");
            // √(2·8·1.82) = 5.40 m/s. The tolerance is the march's own resolution: it steps by her
            // wheel radius, so the clear road it reports is quantised to 0.42 m (#560's rationale).
            Assert.That(road, Is.EqualTo(Mathf.Sqrt(2f * 8f * 1.82f)).Within(0.6f),
                        "…and hold her to the speed the remaining gravel allows, not an arbitrary one");

            float amphibian = VehicleGrounding.SpeedCapMetersPerSecond(
                VehicleKind.AmphibiousVehicle, terrain, env, 0.0, approaching, east, speed: 5f,
                frontAxleY: 1.02f, rearAxleY: -1.02f, brakingMetersPerSecondSquared: 6f,
                wheelRadiusMeters: WheelRadius);
            Assert.That(float.IsPositiveInfinity(amphibian), Is.True,
                        "an amphibian must have NO cap — not a larger one she could still be held " +
                        "by. Water is simply not her wall.");
        }

        // =============================================================================================
        //  2. SABOTAGE ARM — THE SWAP IS DETERMINISTIC (rule 5)
        // =============================================================================================

        /// <summary>
        /// ⭐ <b>SABOTAGE ARM 2.</b> The same machine, at the same place, at the same instant, from the
        /// same prior state, reaches the same medium — <b>bit for bit</b>, twice, with no tolerance.
        /// The depth it is decided on is likewise bit-identical.
        ///
        /// <para>And the arm is not vacuous: moved to another instant of the SAME deterministic tide,
        /// the answer changes. Tide and terrain are recomputed from <c>(worldSeed, gameTime)</c> and
        /// never saved (rule 5), so a machine that floated before a save must float after it.</para>
        /// </summary>
        [Test]
        public void TheSwimTransitionIsBitIdenticalForTheSameSeedTimeAndPosition()
        {
            // Ground 0.2 m below datum, tide rising 1 cm of game-second: depth = 0.2 + 0.01·t.
            var terrain = new FlatTerrain { Elevation = -0.2f };
            var env = new TidalEnv { Level = 0f, RisePerSecond = 0.01f };
            Vector2 where = new(3.5f, -8.25f);
            const double afloatInstant = 137.0;   // depth 1.57 m — well past her 0.28 draft
            const double ashoreInstant = 1.0;     // depth 0.21 m — under draft + band

            float depthA = VehicleGrounding.DepthAt(terrain, env, afloatInstant, where);
            float depthB = VehicleGrounding.DepthAt(terrain, env, afloatInstant, where);
            Assert.That(depthB, Is.EqualTo(depthA),
                        "two reads of one instant disagreed. Something stateful, timed or random has " +
                        "got into a rule that must be a pure function of (worldSeed, gameTime, pos).");

            foreach (double instant in new[] { afloatInstant, ashoreInstant })
                foreach (bool wasAfloat in new[] { false, true })
                {
                    bool first = VehicleGrounding.IsAfloat(
                        VehicleKind.AmphibiousVehicle, wasAfloat, terrain, env, instant, where,
                        FloatDraft, Hysteresis);
                    bool second = VehicleGrounding.IsAfloat(
                        VehicleKind.AmphibiousVehicle, wasAfloat, terrain, env, instant, where,
                        FloatDraft, Hysteresis);
                    Assert.That(second, Is.EqualTo(first),
                                $"the medium at t={instant} was not reproducible from prior state " +
                                $"{wasAfloat}.");
                }

            // Not vacuous — the SAME deterministic tide gives different answers at different instants.
            Assert.That(VehicleGrounding.IsAfloat(VehicleKind.AmphibiousVehicle, false, terrain, env,
                                                  afloatInstant, where, FloatDraft, Hysteresis),
                        Is.True, "1.57 m of water is past her draft by any reading");
            Assert.That(VehicleGrounding.IsAfloat(VehicleKind.AmphibiousVehicle, false, terrain, env,
                                                  ashoreInstant, where, FloatDraft, Hysteresis),
                        Is.False, "0.21 m is under it, so the pair really does discriminate");
        }

        /// <summary>
        /// ⭐ <b>The water's edge is a line she crosses, not one she flickers on.</b> Walk the depth up
        /// through her draft and back down again and count the transitions: exactly one each way. The
        /// band's real work is then shown at ONE depth inside it, where the state HOLDS whichever way
        /// it came in — which is precisely what a naked threshold cannot express.
        /// </summary>
        [Test]
        public void TheWatersEdgeIsNotAFlickerLine()
        {
            // Enter afloat at draft + band = 0.34; take the beach again at draft − band = 0.22.
            // Every sample below is unambiguously one side or the other, so no float tie decides it.
            var depths = new List<float>
            {
                0.10f, 0.20f, 0.26f, 0.30f, 0.33f, 0.35f, 0.40f,   // wading in
                0.35f, 0.33f, 0.30f, 0.26f, 0.23f, 0.21f, 0.10f,   // and back out
            };

            bool afloat = false;
            int swaps = 0;
            foreach (float depth in depths)
            {
                bool next = VehicleGrounding.IsAfloatAtDepth(afloat, depth, FloatDraft, Hysteresis);
                if (next != afloat) swaps++;
                afloat = next;
            }

            Assert.That(swaps, Is.EqualTo(2),
                        "one transition each way is the whole point: she floats past draft + band and " +
                        "takes the beach again below draft − band, so nothing between the two can " +
                        "swap her.");
            Assert.That(afloat, Is.False, "she must end the walk back on her wheels");

            // The band's real work, at ONE depth: the state holds whichever way it came in.
            const float insideTheBand = 0.30f;
            Assert.That(VehicleGrounding.IsAfloatAtDepth(true, insideTheBand, FloatDraft, Hysteresis),
                        Is.True, "already swimming, she stays swimming inside the band");
            Assert.That(VehicleGrounding.IsAfloatAtDepth(false, insideTheBand, FloatDraft, Hysteresis),
                        Is.False, "still on her wheels, she stays on them at the SAME depth — which is " +
                                  "exactly what a naked threshold cannot express.");

            // And outside it there is no ambiguity at all, from either state.
            Assert.That(VehicleGrounding.IsAfloatAtDepth(false, 0.40f, FloatDraft, Hysteresis), Is.True);
            Assert.That(VehicleGrounding.IsAfloatAtDepth(true, 0.10f, FloatDraft, Hysteresis), Is.False);
        }

        /// <summary>No authored terrain means no water: an EditMode fixture, an art scene or a region
        /// with no tidal ground must leave her on her wheels rather than floating in a car park. The
        /// same self-disabling the road gate has, from the same read.</summary>
        [Test]
        public void NoMapMeansNoWater()
        {
            Assert.That(VehicleGrounding.IsAfloat(VehicleKind.AmphibiousVehicle, wasAfloat: false,
                                                  terrain: null, environment: null, 0.0, Vector2.zero,
                                                  FloatDraft, Hysteresis),
                        Is.False);
            Assert.That(VehicleGrounding.IsAfloat(VehicleKind.AmphibiousVehicle, wasAfloat: true,
                                                  terrain: null, environment: null, 0.0, Vector2.zero,
                                                  FloatDraft, Hysteresis),
                        Is.False, "and an unwired map must put a swimming machine back down, not " +
                                  "strand her afloat.");
        }

        /// <summary>A rig with no flotation data does not swim, whatever her kind says. The geometry
        /// is the authority — a def that declares itself amphibious and carries no draft is unbaked,
        /// not buoyant.</summary>
        [Test]
        public void AnAmphibianWithNoFlotationDataDoesNotSwim()
        {
            Assert.That(VehicleGrounding.IsAfloatAtDepth(false, 99f, floatDraftMeters: 0f, Hysteresis),
                        Is.False);
            Assert.That(VehicleGrounding.IsAfloatAtDepth(true, 99f, floatDraftMeters: 0f, Hysteresis),
                        Is.False);
        }

        // =============================================================================================
        //  3. THE SWAP, THROUGH THE LIVE CONTROLLER
        // =============================================================================================

        /// <summary>
        /// ⭐ <b>Past her draft she floats; back in the shallows she is on her wheels — and the PLAYER
        /// never changed mode.</b>
        ///
        /// <para>The machine changes medium, not the mode: nothing in the vehicle lane publishes a
        /// <see cref="ControlModeChanged"/>, so a driver stays in <c>ControlMode.Driving</c> from the
        /// yard to the far shore and back. Asserted by listening to the bus across the whole crossing,
        /// which is the headless way to pin a ruling about the player without spawning one.</para>
        /// </summary>
        [Test]
        public void SheSwapsMediumAndThePlayerNeverChangesMode()
        {
            TidalEnv sea = FlatWorld(elevation: 0f, level: 0f);       // dry land
            VehicleController otter = MachinePointedEast(_otter, 0f);

            otter.StepPhysics(0.02f);
            Assert.That(otter.IsAfloat, Is.False, "she starts on the gravel");

            sea.Level = FloatDraft + 0.2f;                            // the tide takes the flat
            otter.StepPhysics(0.02f);
            Assert.That(otter.IsAfloat, Is.True, "past her draft she is swimming");

            sea.Level = 0f;                                           // and it falls again
            otter.StepPhysics(0.02f);
            Assert.That(otter.IsAfloat, Is.False, "ashore where the depth allows, wheels down");

            CollectionAssert.IsEmpty(_modes,
                "the vehicle lane published a control-mode change. The swap is a state on the " +
                "MACHINE: the player is driving throughout, and a mode signal from here would fight " +
                "the switcher for a state it owns.");
        }

        /// <summary>Afloat the pedals mean her AFLOAT numbers — a different top speed off the same
        /// throttle, which is the whole of what the envelope swap buys. And she gets there by
        /// slowing down, not by being snapped: the ceiling moves, the integrator carries her.</summary>
        [Test]
        public void AfloatThePedalsMeanHerAfloatNumbers()
        {
            TidalEnv sea = FlatWorld(elevation: 0f, level: 0f);
            VehicleController otter = MachinePointedEast(_otter, 0f);

            otter.Throttle = 1f;
            for (int i = 0; i < 600; i++) otter.StepPhysics(0.02f);
            Assert.That(otter.SpeedMetersPerSecond,
                        Is.EqualTo(_otter.MaxSpeedMetersPerSecond).Within(0.05f),
                        "on the gravel she makes her land top speed");

            sea.Level = FloatDraft + 0.5f;
            otter.StepPhysics(0.02f);
            Assert.That(otter.IsAfloat, Is.True);
            Assert.That(otter.SpeedMetersPerSecond,
                        Is.GreaterThan(_otter.AfloatMaxSpeedMetersPerSecond * 2f),
                        "she must not be SNAPPED to her afloat ceiling on the tick she floats — the " +
                        "ceiling moves and her own deceleration carries her down to it.");

            for (int i = 0; i < 900; i++) otter.StepPhysics(0.02f);
            Assert.That(otter.SpeedMetersPerSecond,
                        Is.EqualTo(_otter.AfloatMaxSpeedMetersPerSecond).Within(0.05f),
                        "swimming, the same wide-open throttle must mean her afloat ceiling — eight " +
                        "tyres are a poor paddle.");
            Assert.That(otter.Envelope.MaxAheadMetersPerSecond,
                        Is.EqualTo(_otter.AfloatMaxSpeedMetersPerSecond));
        }

        /// <summary>She spins on the spot at a standstill, which is a thing the truck cannot do, and
        /// her two tracks wind in opposite directions while she does it — the per-side odometers the
        /// drawn wheels read.</summary>
        [Test]
        public void SheSpinsOnTheSpotAndHerTwoTracksWindOppositeWays()
        {
            FlatWorld(elevation: 0f, level: 0f);
            VehicleController otter = MachinePointedEast(_otter, 0f);

            Assert.That(otter.IsSkidSteered, Is.True,
                        "her rig publishes no lock angle, so she must take the skid path");

            otter.SteerDemand = 1f;
            for (int i = 0; i < 100; i++) otter.StepPhysics(0.02f);

            Assert.That(otter.SpeedMetersPerSecond, Is.EqualTo(0f).Within(1e-4f),
                        "no throttle: she turns without going anywhere");
            Assert.That(otter.YawRateDegreesPerSecond,
                        Is.EqualTo(_otter.SpinRateDegreesPerSecond).Within(1e-3f),
                        "and at exactly her tuned spin rate");
            Assert.That(otter.RightTrackOdometerMeters, Is.GreaterThan(0f),
                        "a LEFT spin drives the curb side forward");
            Assert.That(otter.LeftTrackOdometerMeters,
                        Is.EqualTo(-otter.RightTrackOdometerMeters).Within(1e-4f),
                        "…and the street side back by exactly as much. Driving both sides off the " +
                        "body odometer would draw a machine pivoting with her tyres in step.");
        }

        /// <summary>Her turn costs her forward speed by exactly the differential it spends — the skid
        /// trade, through the real <c>StepPhysics</c> so it pins the wiring as well as the
        /// arithmetic.</summary>
        [Test]
        public void HerTurnCostsForwardSpeedThroughTheLiveController()
        {
            FlatWorld(elevation: 0f, level: 0f);
            VehicleController otter = MachinePointedEast(_otter, 0f);

            otter.Throttle = 1f;
            otter.SteerDemand = 1f;
            for (int i = 0; i < 600; i++) otter.StepPhysics(0.02f);

            float expected = SkidSteerMath.MaxBodySpeedMetersPerSecond(
                _otter.SpinRateDegreesPerSecond, TrackWidth, _otter.MaxSpeedMetersPerSecond);
            Assert.That(otter.SpeedMetersPerSecond, Is.EqualTo(expected).Within(1e-3f));
            Assert.That(otter.SpeedMetersPerSecond, Is.LessThan(_otter.MaxSpeedMetersPerSecond));
            Assert.That(Mathf.Max(Mathf.Abs(otter.LeftTrackSpeedMetersPerSecond),
                                  Mathf.Abs(otter.RightTrackSpeedMetersPerSecond)),
                        Is.EqualTo(_otter.MaxSpeedMetersPerSecond).Within(1e-3f),
                        "the outer track must land ON her ceiling, not through it.");
        }

        /// <summary>An Ackermann machine keeps both sides on the BODY odometer — there is no per-side
        /// roll axis on her rig to drive, so publishing one would be arithmetic nothing can draw.</summary>
        [Test]
        public void AnAckermannMachineKeepsBothSidesOnTheBodyOdometer()
        {
            FlatWorld(elevation: 0f, level: 0f);
            VehicleController truck = MachinePointedEast(_dually, 0f);
            Assert.That(truck.IsSkidSteered, Is.False);

            truck.Throttle = 1f;
            truck.SteerDemand = 1f;
            for (int i = 0; i < 200; i++) truck.StepPhysics(0.02f);

            Assert.That(truck.OdometerMeters, Is.GreaterThan(0f));
            Assert.That(truck.LeftTrackOdometerMeters, Is.EqualTo(truck.OdometerMeters));
            Assert.That(truck.RightTrackOdometerMeters, Is.EqualTo(truck.OdometerMeters));
        }

        // =============================================================================================
        //  4. SABOTAGE ARM — THE CLAMP, AND WHAT SHE IS DRAWN AT
        // =============================================================================================

        /// <summary>Build the render chain headlessly: a root with a controller, a visual child, and a
        /// recording renderer wired through the real <c>VehicleMeshDriver.Configure</c>.</summary>
        (VehicleController controller, VehicleMeshDriver driver, RecordingRenderer renderer)
            SkinnedOtter(float x)
        {
            VehicleController controller = MachinePointedEast(_otter, x);
            var visual = new GameObject(VehicleSkinner.VisualChildName);
            visual.transform.SetParent(controller.transform, false);

            var renderer = new RecordingRenderer();
            var driver = controller.gameObject.AddComponent<VehicleMeshDriver>();
            driver.Configure(visual.transform, renderer, _otterMesh, controller,
                             _otterMesh.Wheels, System.Array.Empty<IHullPropRenderer>());
            return (controller, driver, renderer);
        }

        /// <summary>
        /// ⭐ <b>SABOTAGE ARM 3.</b> The waterline clamp is OFF ashore and ON afloat with HER OWN
        /// numbers — never the hull fleet's defaults, and never a value left standing after she takes
        /// the beach.
        ///
        /// <para>The VALUES crossing the seam are what is asserted, not pixels: what the render looks
        /// like is the coordinator's run on hardware and the owner's drive. What a headless fixture
        /// can prove is that the right two numbers go through the right door at the right moment.</para>
        /// </summary>
        [Test]
        public void TheWaterlineClampIsOffAshoreAndCarriesHerOwnNumbersAfloat()
        {
            var recorder = new RecordingPresentation();
            VehicleMeshPresentation.Service = recorder;

            TidalEnv sea = FlatWorld(elevation: 0f, level: 0f);
            (VehicleController controller, VehicleMeshDriver driver, _) = SkinnedOtter(0f);

            controller.StepPhysics(0.02f);
            driver.Drive();
            CollectionAssert.IsEmpty(recorder.Clamps,
                "ashore she is installed ashore-shaped (0/0, the road vehicle's clamp-off) and there " +
                "is nothing to change. A write here means the driver is fighting the install.");

            sea.Level = FloatDraft + 0.5f;
            controller.StepPhysics(0.02f);
            driver.Drive();
            Assert.That(recorder.Clamps.Count, Is.EqualTo(1), "one write, on the frame she floats");
            Assert.That(recorder.Clamps[0].deck, Is.EqualTo(LowestGunwale).Within(1e-4f),
                        "afloat the clamp must sit at HER lowest gunwale — her transom top, off her " +
                        "own downflooding block — and not at a hull's deck line.");
            Assert.That(recorder.Clamps[0].halfBeam, Is.EqualTo(WaterlineHalfBeam).Within(1e-4f),
                        "…and at her own published waterline half-beam.");

            driver.Drive();
            driver.Drive();
            Assert.That(recorder.Clamps.Count, Is.EqualTo(1),
                        "the toggle is dirty-checked: two writes per water's edge, not two a frame.");

            sea.Level = 0f;
            controller.StepPhysics(0.02f);
            driver.Drive();
            Assert.That(recorder.Clamps.Count, Is.EqualTo(2), "…and one more on the frame she lands");
            Assert.That(recorder.Clamps[1].deck, Is.EqualTo(0f),
                        "ashore the clamp must go fully OFF. A machine on gravel has no waterline to " +
                        "hold the sea below, and a value left standing would clamp a truck's render.");
            Assert.That(recorder.Clamps[1].halfBeam, Is.EqualTo(0f));
        }

        /// <summary>
        /// ⭐ <b>Afloat she sinks by the measured rigid float and NOTHING ELSE.</b>
        /// <c>[0, 0, −0.52]</c>, max per-vertex deviation 0 (measured in the rig probe), applied as a
        /// runtime offset on the DRY mesh — so no second bake, and the sea climbs to her waterline
        /// through the same channel that carries it.
        ///
        /// <para>With no displaced sea published there is no ride, so the heave is exactly the sink:
        /// the cleanest place to read the number. And the seakeeping channels are never written —
        /// <b>not</b> zeroed, never touched — because the ruled scope is displacement plus the shared
        /// heave, and a machine crossing a cove has none of a hull's tuned rock data.</para>
        /// </summary>
        [Test]
        public void AfloatSheSinksByHerMeasuredFloatOffsetAndTakesNoSeakeeping()
        {
            TidalEnv sea = FlatWorld(elevation: 0f, level: 0f);
            (VehicleController controller, VehicleMeshDriver driver, RecordingRenderer renderer) =
                SkinnedOtter(0f);

            controller.StepPhysics(0.02f);
            driver.Drive();
            Assert.That(renderer.HeavePixels, Is.EqualTo(0f).Within(1e-4f),
                        "ashore she stands on the ground her rig was baked at");

            sea.Level = FloatDraft + 0.5f;
            controller.StepPhysics(0.02f);
            driver.Drive();

            Assert.That(renderer.HeavePixels, Is.EqualTo(-FloatSink * PxPerMetre).Within(1e-3f),
                        "afloat she must drop by exactly her measured sink — 0.52 m at 32 px/m.");
            Assert.That(renderer.RidePixels, Is.EqualTo(0f).Within(1e-4f),
                        "with no displaced sea published there is no WORLD ride: the sink is an " +
                        "in-cell pose, exactly as the rig applies its own dz before projecting, and " +
                        "the compositing window must not travel with it.");
            Assert.That(renderer.RollDegrees, Is.EqualTo(0f),
                        "no rock amplitudes: seakeeping is out of the ruled scope and she has none.");
            Assert.That(renderer.PitchDegrees, Is.EqualTo(0f), "and no storm loft.");

            sea.Level = 0f;
            controller.StepPhysics(0.02f);
            driver.Drive();
            Assert.That(renderer.HeavePixels, Is.EqualTo(0f).Within(1e-4f),
                        "and she must come back UP when she takes the beach.");
        }

        /// <summary>A machine whose rig publishes no flotation never touches the heave channels at
        /// all — so the Dually's render is the one #560 shipped, to the bit.</summary>
        [Test]
        public void AMachineThatDoesNotFloatNeverTouchesTheFlotationChannels()
        {
            FlatWorld(elevation: 0f, level: 5f);   // deep water everywhere, to make the point

            VehicleController truck = MachinePointedEast(_dually, 0f);
            var visual = new GameObject(VehicleSkinner.VisualChildName);
            visual.transform.SetParent(truck.transform, false);
            var renderer = new RecordingRenderer { HeavePixels = 123f, RidePixels = 456f };
            var driver = truck.gameObject.AddComponent<VehicleMeshDriver>();
            driver.Configure(visual.transform, renderer, _duallyMesh, truck, _duallyMesh.Wheels,
                             System.Array.Empty<IHullPropRenderer>());

            truck.StepPhysics(0.02f);
            driver.Drive();

            Assert.That(renderer.HeavePixels, Is.EqualTo(123f),
                        "the sentinel was overwritten: the flotation path must return before touching " +
                        "a channel on a rig with no FloatSinkMeters.");
            Assert.That(renderer.RidePixels, Is.EqualTo(456f));
            Assert.That(truck.IsAfloat, Is.False, "…and she is emphatically not swimming.");
        }

        // =============================================================================================
        //  5. HER PUBLISHED NUMBERS, AND THE COMMITTED ASSET
        // =============================================================================================

        /// <summary>
        /// ⭐ <b>The tire check — the sidecar's own cross-reference on her sink.</b> Her rig publishes
        /// two independent facts about floating: the sink is 0.52 m, and afloat <i>"tires are
        /// submerged to 0.12 of their 0.64 diameter… the top 19% of each tire shows"</i>. They agree
        /// only if the sink and the wheel radius are both right.
        ///
        /// <para>The cheapest possible guard against a transcription slip in either number, and it
        /// costs nothing: <c>2r − sink</c> is exactly the tire showing.</para>
        /// </summary>
        [Test]
        public void HerSinkAndHerWheelsAgreeWithTheSidecarsTirePercentage()
        {
            float diameter = 2f * WheelRadius;
            Assert.That(diameter, Is.EqualTo(0.64f).Within(1e-6f), "her published tire diameter");

            float showing = diameter - FloatSink;
            Assert.That(showing, Is.EqualTo(TireShowsAfloat).Within(1e-5f),
                        "2r − sink is what stands above the water, and her sidecar publishes it as " +
                        "0.12. If this fails, her sink or her wheel radius has moved in the art.");
            Assert.That(showing / diameter, Is.EqualTo(0.19f).Within(0.005f),
                        "…which is the 19% of each tire her sidecar says shows.");
        }

        /// <summary>
        /// The numbers this fixture is written against are the art side's own, re-read from the
        /// sidecar rather than trusted — the discipline ADR 0035 §6 asks for, and the same shape
        /// <c>VehicleDriveModeTests.TheBakedDoorPointMatchesTheSidecar</c> uses. It reads the SIDECAR
        /// rather than a baked asset on purpose: hers is not baked yet, and this is the pin that will
        /// still be right when it is.
        /// </summary>
        [Test]
        public void HerPublishedGeometryIsWhatThisFixtureIsWrittenAgainst()
        {
            string json = ReadSidecar();

            Assert.That(Number(json, "\"sink_m_at_float_1\""), Is.EqualTo(FloatSink).Within(1e-6f));
            Assert.That(Number(json, "\"draft_above_keel_m\""), Is.EqualTo(FloatDraft).Within(1e-6f));
            Assert.That(Number(json, "\"max_half_beam_m\"", after: "\"waterline_polygon_at_float_1\""),
                        Is.EqualTo(WaterlineHalfBeam).Within(1e-6f));
            Assert.That(Number(json, "\"track_width_m\""), Is.EqualTo(TrackWidth).Within(1e-6f));
            Assert.That(Number(json, "\"radius_m\""), Is.EqualTo(WheelRadius).Within(1e-6f));
            Assert.That(Number(json, "\"rev_of_split_per_45deg\""), Is.EqualTo(0.2461f).Within(1e-6f));
            Assert.That(Number(json, "\"z\"", after: "\"lowest_gunwale_point\""),
                        Is.EqualTo(LowestGunwale).Within(1e-6f),
                        "the clamp line is her lowest gunwale — the transom top she would flood over.");
            Assert.That(Number(json, "\"scale_px_per_m\""), Is.EqualTo(PxPerMetre).Within(1e-6f));
        }

        /// <summary>Her sidecar's own <c>kind</c> token and the token on the committed def reach the
        /// SAME <see cref="VehicleKind"/> — through <see cref="VehicleKinds"/>, which is the point:
        /// the two files say different words on purpose (the art side's is never hand-edited) and one
        /// table reconciles them.</summary>
        [Test]
        public void HerDefsKindAgreesWithHerSidecarsThroughTheOneTable()
        {
            string json = ReadSidecar();
            int at = json.IndexOf("\"kind\"", System.StringComparison.Ordinal);
            Assert.That(at, Is.GreaterThanOrEqualTo(0), "her sidecar declares no top-level kind");
            int open = json.IndexOf('"', json.IndexOf(':', at) + 1);
            string shipped = json.Substring(open + 1, json.IndexOf('"', open + 1) - open - 1);

            Assert.That(VehicleKinds.TryFromToken(shipped, out VehicleKind fromSidecar), Is.True,
                        $"her sidecar ships '{shipped}', which VehicleKinds does not map.");

            VehicleDef def = LoadOtterDef();
            Assert.That(def.Kind, Is.EqualTo(fromSidecar),
                        "the committed def and the shipped sidecar must mean the same machine.");
            Assert.That(def.Kind, Is.EqualTo(VehicleKind.AmphibiousVehicle));
            Assert.That(def.KindToken, Is.EqualTo(VehicleKinds.CanonicalToken(def.Kind)),
                        "an asset WE write carries the repo's canonical token, never the shipped " +
                        "alias — VehicleKinds.CanonicalToken says so.");
        }

        /// <summary>Her committed def carries the owner's ruled id and a tunable for every handling
        /// figure (rule 6). Deliberately a SHAPE check and not a value check: the owner is meant to
        /// change every one of these without going red.</summary>
        [Test]
        public void HerDefCarriesTheOwnersRuledIdAndATunableForEveryHandlingFigure()
        {
            VehicleDef def = LoadOtterDef();

            Assert.That(def.Id, Is.EqualTo("vehicle.otter_8x8"),
                        "the owner's ruled id, and append-only once shipped");
            Assert.That(def.SpinRateDegreesPerSecond, Is.GreaterThan(0f));
            Assert.That(def.AfloatSpinRateDegreesPerSecond, Is.GreaterThan(0f));
            Assert.That(def.AfloatMaxSpeedMetersPerSecond, Is.GreaterThan(0f));
            Assert.That(def.AfloatMaxReverseSpeedMetersPerSecond, Is.GreaterThan(0f));
            Assert.That(def.AfloatAccelerationMetersPerSecondSquared, Is.GreaterThan(0f));
            Assert.That(def.AfloatDragDecelerationMetersPerSecondSquared, Is.GreaterThan(0f));
            Assert.That(def.SwimHysteresisMeters, Is.GreaterThan(0f),
                        "a zero band would make the water's edge a flicker line");

            Assert.That(def.AfloatMaxSpeedMetersPerSecond, Is.LessThan(def.MaxSpeedMetersPerSecond),
                        "she swims on her tyres — there is no prop, no jet and no rudder in the rig — " +
                        "so she must be slower in the water than on the gravel.");
            Assert.That(def.SteerFalloffHalfSpeedMetersPerSecond, Is.EqualTo(0f),
                        "the speed falloff corrects the geometric bicycle model, which she is not. " +
                        "0 is the documented 'off', and leaving it on would invent a lag she has not.");
        }

        /// <summary>The Dually's committed def declares her kind EXPLICITLY, rather than leaning on a
        /// field default. The discriminator decides whether water is a wall or a road; a load-bearing
        /// value that is only correct because nobody typed it is a value waiting to be wrong.</summary>
        [Test]
        public void TheDuallysCommittedDefDeclaresHerKindExplicitly()
        {
            var def = UnityEditor.AssetDatabase.LoadAssetAtPath<VehicleDef>(
                "Assets/_Project/Data/Vehicles/Dually3500.asset");
            Assert.That(def, Is.Not.Null);
            Assert.That(def.Kind, Is.EqualTo(VehicleKind.RoadVehicle));
            Assert.That(def.KindToken, Is.EqualTo("road_vehicle"));
        }

        /// <summary>An unrecognised kind token makes the whole def UNUSABLE — refused at install with
        /// a logged reason — rather than quietly becoming a road vehicle.</summary>
        [Test]
        public void AnUnrecognisedKindTokenRefusesTheWholeDef()
        {
            _otter.KindToken = "hovercraft";
            Assert.That(_otter.IsUsable(), Is.False,
                        "a machine whose kind nobody maps must be refused: guessing it is how " +
                        "something drowns.");

            _otter.KindToken = "amphibious_xtv";   // her SIDECAR's own shipped spelling
            Assert.That(_otter.Kind, Is.EqualTo(VehicleKind.AmphibiousVehicle),
                        "…and every spelling the art side actually ships must still resolve.");
        }

        // ---- helpers -----------------------------------------------------------------------------

        static VehicleDef LoadOtterDef()
        {
            var def = UnityEditor.AssetDatabase.LoadAssetAtPath<VehicleDef>(OtterDefPath);
            Assert.That(def, Is.Not.Null, $"her committed def is missing at {OtterDefPath}");
            return def;
        }

        static string ReadSidecar()
        {
            DirectoryInfo root = Directory.GetParent(Application.dataPath);
            Assert.IsNotNull(root, "cannot locate the repo root above Assets/");
            string full = Path.Combine(root.FullName, SidecarPath);
            FileAssert.Exists(full);
            return File.ReadAllText(full);
        }

        /// <summary>The first number after <paramref name="key"/> (optionally only after
        /// <paramref name="after"/>). A deliberately small scan rather than a JSON dependency — the
        /// same hand-rolled approach the rig probes take over these files.</summary>
        static float Number(string json, string key, string after = null)
        {
            int from = 0;
            if (after != null)
            {
                from = json.IndexOf(after, System.StringComparison.Ordinal);
                Assert.That(from, Is.GreaterThanOrEqualTo(0), $"the sidecar has no {after} block");
            }

            int at = json.IndexOf(key, from, System.StringComparison.Ordinal);
            Assert.That(at, Is.GreaterThanOrEqualTo(0), $"the sidecar publishes no {key}");

            int colon = json.IndexOf(':', at + key.Length);
            Assert.That(colon, Is.GreaterThanOrEqualTo(0), $"{key} has no value");

            int i = colon + 1;
            while (i < json.Length && char.IsWhiteSpace(json[i])) i++;
            int start = i;
            while (i < json.Length && (char.IsDigit(json[i]) || json[i] == '-' || json[i] == '+' ||
                                       json[i] == '.' || json[i] == 'e' || json[i] == 'E')) i++;
            Assert.That(i, Is.GreaterThan(start), $"{key} is not followed by a number");

            return float.Parse(json.Substring(start, i - start), CultureInfo.InvariantCulture);
        }
    }
}
