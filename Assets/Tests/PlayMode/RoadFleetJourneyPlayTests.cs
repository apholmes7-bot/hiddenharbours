#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.Core;
using HiddenHarbours.Player;
using HiddenHarbours.Vehicles;
using HiddenHarbours.World;
using HiddenHarbours.App.Editor;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// ⭐⭐ <b>THE ROAD FLEET JOURNEY — every driven machine, through the real switcher, on the built
    /// Nine Mile Creek data, the way a player does it</b> (driveable charter, PR 0).
    ///
    /// <para><i>Walk up to her door → E → she is yours → pull out of the bay → drive the spur down to
    /// Wharf Road → park → E → step out on land.</i> Seven machines: the five of the laydown, the Dually
    /// at the truck park and the Otter at her landing. The two semis add the coupling loop in the
    /// yard: pull clear, back under a trailer, get out and work the release, tow her thirty metres, wind
    /// her legs down at her own crank, pull the pin, and drive away bobtail.</para>
    ///
    /// <para><b>Why this file exists when <c>NineMileCreekLaydownPlayTests</c> already pulls five
    /// machines out of their bays.</b> That test drives <c>VehicleController.Throttle</c> directly —
    /// it had to, because the switcher's keyboard read zeroed any demand a fixture set, every frame
    /// (memory <c>driveinput-is-zeroed-by-the-keyboard-read</c>). So it proved the GROUND and not the
    /// PLAYER's path. Here the demand goes where the player's does: into <see cref="ControlSwitcher"/>
    /// through <see cref="IDriveInputSource"/>, read every frame, handed to the seat, integrated by the
    /// machine. A <see cref="HeldDriveInput"/> is the scripted driver; nothing here touches a controller.</para>
    ///
    /// <para><b>The world is the REAL one:</b> the region's own tidal terrain
    /// (<see cref="NineMileCreekMainland.ConfigureTerrain"/>) registered in Core, so the grounding gate
    /// and the exit-afloat rule are live rather than self-disabled, and the builders' own
    /// <c>Place()</c> calls stand every machine where the owner's Build click stands them. ⚠️ Nothing
    /// here is in HIS scene until that click (paint passes are not idempotent) — this proves the built
    /// data, and says so.</para>
    ///
    /// <para><b>The scripted driver is pure pursuit with two rules that matter.</b> A waypoint counts as
    /// reached when she passes its perpendicular, not only when she touches it (a turning circle wider
    /// than the reach otherwise orbits an overshot point for ever), and the last leg follows the road's
    /// centre-line by lookahead rather than aiming at one point on it, so she converges onto the road
    /// and parks ON it whatever angle she arrived at.</para>
    ///
    /// <para><b>Frames buy hardware, not time.</b> Every drive step waits on
    /// <see cref="WaitForFixedUpdate"/>, and every loop is bounded and fails loudly. ⚠️ Nothing in the
    /// fleet carries a collider, so "without contact" is not a thing this file can measure; what it
    /// measures instead is the law the yard is sited on — every metre she covers is EXPOSED ground —
    /// and that the journey ends ON the road rather than across its mouth.</para>
    /// </summary>
    public class RoadFleetJourneyPlayTests
    {
        // ---- the world -----------------------------------------------------------------------------

        /// <summary>A still sea at the region's mean tide, so every claim below is about the ground and
        /// not about a tide that happened to be running.</summary>
        private sealed class StillWater : IEnvironmentService
        {
            public int WorldSeed => 7;
            public TideProfile ActiveTideProfile { get; set; }
            public EnvironmentSample Sample() => default;
            public float TideHeightAt(double totalSeconds) => NineMileCreekMainland.TideMean;
            public float WaterLevelAt(double totalSeconds) => NineMileCreekMainland.TideMean;
        }

        // ---- the scripted driver's numbers ---------------------------------------------------------
        // Test-driver feel, not game tunables: nothing here is a claim about how a machine drives, only
        // about how this driver asks. Cruise sits well under the fleet's ceiling and turns are taken
        // slowly because speed-sensitive steering widens every machine's circle at speed
        // (VehicleController.EffectiveSteer) — a driver hunting a waypoint at full throttle orbits it.
        private const float CruiseThrottle = 0.6f;
        private const float TurnThrottle = 0.15f;
        private const float CreepReverseThrottle = 0.25f;
        private const float SlowForTurnDegrees = 12f;
        private const float SlowNearTargetMetres = 10f;
        private const float SteerGainDegrees = 20f;
        private const float WaypointReachMetres = 3f;
        private const float RoadLookaheadMetres = 12f;

        /// <summary>
        /// The numbers above, as the one struct the SHIPPED maths reads. The driver's arithmetic used to
        /// live privately in this file; it is now Core's <see cref="RouteFollowMath"/> and the game's own
        /// RouteDriver reads the same functions, so the fixture and the village cannot disagree about how
        /// a machine follows a road. What stays here is only the FEEL — these are test-driver numbers,
        /// deliberately not the fleet's own tunables, so a def the owner retunes cannot silently move
        /// what this file measures.
        /// </summary>
        private static RouteFollowMath.RouteFollowTuning TestDriver => new(
            WaypointReachMetres, RoadLookaheadMetres, CruiseThrottle, TurnThrottle,
            SlowForTurnDegrees, SteerGainDegrees);
        private const float AlongTheRoadMetres = 20f;
        private const float TowMetres = 30f;
        private const float BobtailMetres = 10f;
        private const float ClearOfTheTrailerMetres = 15f;
        private const float MinimumPullOutMetres = 10f;
        private const float OtterReverseMetres = 12f;
        private const int MaxStepsPerLeg = 3000;       // 60 s of physics — fails loudly, never hangs
        private const int MaxStepsToStop = 400;
        private const int MaxStepsForTheCrank = 600;   // 12 s; the published crank is 2.4 s
        private const int ControlSteps = 150;          // the A/B arms: 3 s of physics

        private const string PupMesh = "Assets/_Project/Data/Vehicles/Meshes/TrailerReefer28VehicleMesh.asset";

        private readonly List<Object> _spawned = new();
        private MainlandTidalTerrain _terrain;
        private ControlSwitcher _switcher;
        private PlayerWalkController _walk;
        private GameObject _playerGo;
        private HeldDriveInput _held;
        private int _wetSamples;
        private Vector2 _firstWetSample;

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();
            InteractionGate.Reset();
            Interactables.Clear();
            InteractVerb.Reset();
            EventBus.Clear<ControlModeChanged>();
            EventBus.Clear<ActiveVehicleChanged>();
            EventBus.Clear<DriveSeatRequested>();

            GameServices.Environment = new StillWater();

            // The region's own ground, registered into Core by its enable hook — the SAME terrain the
            // EditMode yard tests measure the apron against, so "dry" here means what it means there.
            var terrainGo = new GameObject("NineMileCreekMainland_Journey");
            _spawned.Add(terrainGo);
            _terrain = terrainGo.AddComponent<MainlandTidalTerrain>();
            NineMileCreekMainland.ConfigureTerrain(_terrain);
            Assert.That(GameServices.TidalTerrain, Is.SameAs(_terrain),
                "the mainland terrain did not register itself — the grounding gate would be OFF and " +
                "every dry-ground claim below vacuous.");

            // One listener, or Unity logs "no audio listeners" on every frame of every drive.
            _spawned.Add(new GameObject("Ears", typeof(AudioListener)));

            _wetSamples = 0;
            _firstWetSample = Vector2.zero;
        }

        [TearDown]
        public void TearDown()
        {
            EventBus.Clear<ControlModeChanged>();
            EventBus.Clear<ActiveVehicleChanged>();
            EventBus.Clear<DriveSeatRequested>();
            Interactables.Clear();
            InteractVerb.Reset();
            InteractionGate.Reset();
            for (int i = 0; i < _spawned.Count; i++)
                if (_spawned[i] != null) Object.DestroyImmediate(_spawned[i]);
            _spawned.Clear();
            GameServices.Reset();
        }

        // =============================================================================================
        //  THE JOURNEYS — one test per machine, so a red names her
        // =============================================================================================

        [UnityTest]
        public IEnumerator TheHightopVanDrivesFromHerBayToWharfRoad() =>
            LaydownJourney("HightopVanAtTheLaydown");

        [UnityTest]
        public IEnumerator TheCaboverBoxDrivesFromHerBayToWharfRoad() =>
            LaydownJourney("CaboverBoxAtTheLaydown");

        [UnityTest]
        public IEnumerator TheConventionalBoxDrivesFromHerBayToWharfRoad() =>
            LaydownJourney("ConvBoxAtTheLaydown");

        [UnityTest]
        public IEnumerator TheAeroSemiDrivesFromHerBayToWharfRoad() =>
            LaydownJourney("AeroSemiAtTheLaydown");

        [UnityTest]
        public IEnumerator TheClassicSemiDrivesFromHerBayToWharfRoad() =>
            LaydownJourney("ClassicSemiAtTheLaydown");

        /// <summary>The Dually starts on the park rather than in a bay, facing north with the road
        /// behind her — so hers is the one journey that opens with a U-turn.</summary>
        [UnityTest]
        public IEnumerator TheDuallyDrivesFromTheParkToWharfRoad()
        {
            var world = new List<GameObject>();
            yield return StandTheWorld(world);

            const string who = NineMileCreekTruckPark.TruckName;
            GameObject go = Named(world, who);
            VehicleController ctl = ControllerOf(go, who);
            VehicleDoor door = DoorOf(go, who);

            StandTheFisherAt(door.DoorWorldPosition);
            yield return null;
            yield return ClimbIn(door, who);

            Vector2 startedAt = go.transform.position;
            Vector2 join = WhereTheSpurMeetsTheRoad();
            yield return DriveTo(go, ctl, join, WaypointReachMetres, false, who);
            Assert.That(Vector2.Distance(startedAt, go.transform.position),
                Is.GreaterThanOrEqualTo(MinimumPullOutMetres),
                $"{who}: reached the road join having covered under {MinimumPullOutMetres} m.");

            yield return DriveAlongTheRoad(go, ctl, TheRoadTheSpurJoins(), join, AlongTheRoadMetres, who);
            yield return Park(ctl, who);
            AssertTheDriveWasHonest(go, who);

            yield return ClimbOut(door, who);
        }

        /// <summary>
        /// The Otter is staged pointing down the boat ramp, so "drive her on land" is BACKING up the
        /// shore: astern is always allowed, the ground behind her is the plateau, and she must never
        /// float on the way. The machine that swims is the one whose land journey is the reverse gear.
        /// </summary>
        [UnityTest]
        public IEnumerator TheOtterBacksUpTheShoreAndStaysOnLand()
        {
            var world = new List<GameObject>();
            yield return StandTheWorld(world);

            const string who = NineMileCreekOtterLanding.OtterName;
            GameObject go = Named(world, who);
            VehicleController ctl = ControllerOf(go, who);
            VehicleDoor door = DoorOf(go, who);

            StandTheFisherAt(door.DoorWorldPosition);
            yield return null;
            yield return ClimbIn(door, who);

            Vector2 startedAt = go.transform.position;
            bool floated = false;
            float from = ctl.OdometerMeters;
            int steps = 0;
            _held.Set(-1f, 0f, false);
            while (from - ctl.OdometerMeters < OtterReverseMetres)
            {
                Assert.That(steps++, Is.LessThan(MaxStepsPerLeg),
                    $"{who}: {MaxStepsPerLeg} physics steps astern and she has backed only " +
                    $"{from - ctl.OdometerMeters:0.##} m — the seam is not carrying the demand.");
                yield return new WaitForFixedUpdate();
                SampleTheGroundUnder(go);
                floated |= ctl.IsAfloat;
            }
            yield return Park(ctl, who);

            Assert.That(Vector2.Distance(startedAt, go.transform.position),
                Is.GreaterThanOrEqualTo(MinimumPullOutMetres),
                $"{who}: backed {OtterReverseMetres} m of odometer and moved under {MinimumPullOutMetres} m.");
            Assert.That(floated, Is.False, $"{who}: she floated while backing up the shore.");
            Assert.That(_held.Reads, Is.GreaterThan(0), "the switcher never asked the source.");
            Assert.That(_wetSamples, Is.EqualTo(0),
                $"{who}: {_wetSamples} physics steps stood on drowned ground, the first at {_firstWetSample}.");

            yield return ClimbOut(door, who);
        }

        [UnityTest]
        public IEnumerator TheAeroSemiCouplesTowsAndReleasesInTheYard() =>
            SemiJourney("AeroSemiAtTheLaydown", "Flatbed53AtTheLaydown", null);

        [UnityTest]
        public IEnumerator TheClassicSemiCouplesTowsAndReleasesInTheYard() =>
            SemiJourney("ClassicSemiAtTheLaydown", null, PupMesh);

        // =============================================================================================
        //  THE A/B ARMS — a switch wired to nothing returns the same metres either way
        // =============================================================================================

        /// <summary>The dead control: in the cab, asked for nothing, she covers exactly nothing.</summary>
        [UnityTest]
        public IEnumerator AMachineAskedForNothingCoversNoGround()
        {
            var world = new List<GameObject>();
            yield return StandTheWorld(world);

            const string who = "HightopVanAtTheLaydown";
            GameObject go = Named(world, who);
            VehicleDoor door = DoorOf(go, who);
            StandTheFisherAt(door.DoorWorldPosition);
            yield return null;
            yield return ClimbIn(door, who);

            Vector2 startedAt = go.transform.position;
            _held.Release();
            for (int i = 0; i < ControlSteps; i++) yield return new WaitForFixedUpdate();

            Assert.That(_held.Reads, Is.GreaterThan(0), "the switcher never asked the source.");
            Assert.That(Vector2.Distance(startedAt, go.transform.position), Is.LessThanOrEqualTo(1e-4f),
                $"{who} moved with nothing asked of her — something drives a machine nobody is driving.");
        }

        /// <summary>A full-throttle demand with nobody in the seat moves nothing — and the seam is not
        /// even consulted on foot. The seam is inert until E puts a driver behind a wheel.</summary>
        [UnityTest]
        public IEnumerator ADemandNobodyIsSeatedForMovesNothing()
        {
            var world = new List<GameObject>();
            yield return StandTheWorld(world);

            const string who = "HightopVanAtTheLaydown";
            GameObject go = Named(world, who);
            VehicleDoor door = DoorOf(go, who);
            StandTheFisherAt(door.DoorWorldPosition);
            yield return null;

            Vector2 startedAt = go.transform.position;
            _held.Set(1f, 0f, false);
            for (int i = 0; i < ControlSteps; i++) yield return new WaitForFixedUpdate();

            Assert.That(_switcher.Mode, Is.EqualTo(ControlMode.OnFoot), "fixture: nobody climbed in.");
            Assert.That(_held.Reads, Is.EqualTo(0),
                "the switcher read the wheel while the fisher was on foot — the seam leaks out of the cab.");
            Assert.That(Vector2.Distance(startedAt, go.transform.position), Is.LessThanOrEqualTo(1e-4f),
                $"{who} moved on a demand nobody was seated for.");
        }

        /// <summary>
        /// The positive control, twice: the same demand for the same number of physics steps covers the
        /// same metres from a fresh yard — and covers them, which is what the two arms above are a
        /// control FOR.
        ///
        /// <para>⚠️ The demand is LANDED before the steps are counted. The switcher reads the source in
        /// <c>Update</c>, and a frame runs its physics steps BEFORE its Update — so a demand set in a
        /// coroutine reaches the seat one frame later, and how many physics steps that frame carries is
        /// a fact about the machine's load, not about the drive. Measured in the full sweep: 19.67 m
        /// against 19.45 m, exactly one step of travel apart, from a fixture that counted steps from the
        /// instant it set the demand. One <c>yield return null</c> puts the demand on the seat first;
        /// then every counted step runs under it, and the metres are a function of the inputs again.
        /// (The keyboard has the same one-frame latency; it is the honest shape of the read.)</para>
        /// </summary>
        [UnityTest]
        public IEnumerator TheSameDemandReproducesTheSameMetres()
        {
            const string who = "HightopVanAtTheLaydown";
            float[] covered = new float[2];

            for (int run = 0; run < 2; run++)
            {
                var world = new List<GameObject>();
                yield return StandTheWorld(world);

                GameObject go = Named(world, who);
                VehicleDoor door = DoorOf(go, who);
                StandTheFisherAt(door.DoorWorldPosition);
                yield return null;
                yield return ClimbIn(door, who);

                _held.Set(1f, 0f, false);
                yield return null;   // the read is in Update: land the demand on the seat, THEN count
                Vector2 startedAt = go.transform.position;
                for (int i = 0; i < ControlSteps; i++) yield return new WaitForFixedUpdate();
                covered[run] = Vector2.Distance(startedAt, go.transform.position);

                // Tear everything down between runs so the second yard is as fresh as the first — the
                // terrain and the listener stay, everything the run stood goes.
                for (int i = 0; i < _spawned.Count; i++)
                {
                    Object o = _spawned[i];
                    if (o == null || o == _terrain.gameObject) continue;
                    if (o is GameObject g && g.GetComponent<AudioListener>() != null) continue;
                    Object.DestroyImmediate(o);
                }
                _spawned.RemoveAll(o => o == null);
                Interactables.Clear();
                InteractVerb.Reset();
                yield return null;
            }

            Assert.That(covered[0], Is.GreaterThanOrEqualTo(MinimumPullOutMetres),
                $"{who} covered {covered[0]:0.##} m in {ControlSteps} steps of full throttle through the " +
                "switcher — the seam is not carrying the demand, and the two control arms above would " +
                "agree with this for the wrong reason.");
            Assert.That(covered[1], Is.EqualTo(covered[0]).Within(1e-4f),
                $"the same demand covered {covered[0]:0.####} m and then {covered[1]:0.####} m — the " +
                "drive is not a function of its inputs.");
        }

        // =============================================================================================
        //  THE JOURNEY SHAPES
        // =============================================================================================

        /// <summary>The five of the laydown: bay → lane → the yard's gate → the park's spare bay → the
        /// road join → along the road → park → out.</summary>
        private IEnumerator LaydownJourney(string who)
        {
            var world = new List<GameObject>();
            yield return StandTheWorld(world);

            GameObject go = Named(world, who);
            VehicleController ctl = ControllerOf(go, who);
            VehicleDoor door = DoorOf(go, who);
            int bay = PlacementOf(who).Unit.Bay;

            StandTheFisherAt(door.DoorWorldPosition);
            yield return null;
            yield return ClimbIn(door, who);

            Vector2 startedAt = go.transform.position;
            yield return DriveTo(go, ctl, LaneCentreUnderBay(bay), WaypointReachMetres, false, who);
            yield return DriveTo(go, ctl, TheYardsGate(), WaypointReachMetres, false, who);

            float pulledOut = Vector2.Distance(startedAt, go.transform.position);
            Assert.That(pulledOut, Is.GreaterThanOrEqualTo(MinimumPullOutMetres),
                $"{who}: at the yard's gate having covered only {pulledOut:0.##} m.");
            Assert.That(NineMileCreekLaydown.BayArea(bay).Contains((Vector2)go.transform.position),
                Is.False, $"{who} is still inside her own bay.");

            yield return DriveTo(go, ctl, TheParksSpareBay(), WaypointReachMetres, false, who);
            Vector2 join = WhereTheSpurMeetsTheRoad();
            yield return DriveTo(go, ctl, join, WaypointReachMetres, false, who);
            yield return DriveAlongTheRoad(go, ctl, TheRoadTheSpurJoins(), join, AlongTheRoadMetres, who);
            yield return Park(ctl, who);
            AssertTheDriveWasHonest(go, who);

            yield return ClimbOut(door, who);
        }

        /// <summary>
        /// The semis: pull clear of the yard's line, back under a trailer standing on it — the aero's own
        /// flatbed, or a pup the test stands on the classic's line — get out, work the release to
        /// couple, wait for her legs, get in, tow, get out, wind her legs down at her own crank, pull
        /// the pin, get in, drive away bobtail. Every press is the interact verb at a published point.
        /// </summary>
        private IEnumerator SemiJourney(string who, string yardTrailerName, string pupMeshPath)
        {
            var world = new List<GameObject>();
            yield return StandTheWorld(world);

            GameObject go = Named(world, who);
            VehicleController ctl = ControllerOf(go, who);
            VehicleDoor door = DoorOf(go, who);
            var hitch = go.GetComponent<VehicleHitch>();
            Assert.That(hitch, Is.Not.Null,
                $"{who} grew no hitch — her def publishes a plate and the skinner installs one for it.");
            NineMileCreekLaydown.Placement placement = PlacementOf(who);

            StandTheFisherAt(door.DoorWorldPosition);
            yield return null;
            yield return ClimbIn(door, who);

            // ---- 1. Pull clear, straight ahead, so what is behind her is a trailer and nothing else.
            Vector2 bayPose = go.transform.position;
            yield return DriveStraight(ctl, CruiseThrottle, ClearOfTheTrailerMetres, who);
            yield return Park(ctl, who);
            Assert.That(hitch.CapturedTrailer(), Is.Null,
                $"{who}: {ClearOfTheTrailerMetres} m clear of her bay and a trailer is still in her slot.");

            // ---- 2. The trailer she is going to back under.
            GameObject trailerGo;
            if (pupMeshPath == null)
            {
                trailerGo = Named(world, yardTrailerName);
            }
            else
            {
                var pupMesh = UnityEditor.AssetDatabase.LoadAssetAtPath<VehicleMeshDef>(pupMeshPath);
                Assert.That(pupMesh, Is.Not.Null, $"{pupMeshPath} did not load — re-run the vehicle bake.");
                var unit = new NineMileCreekLaydown.Unit("PupOnHerLine", null, pupMeshPath,
                                                         placement.Unit.Bay, seatsOnTheTractorAhead: true);
                NineMileCreekLaydown.Placement pup =
                    NineMileCreekLaydown.CoupleReadyTrailer(placement, unit, pupMesh);
                trailerGo = NineMileCreekLaydown.PlaceOne(pup);
                _spawned.Add(trailerGo);
                yield return null;
            }
            var trailer = trailerGo.GetComponent<TowedBody>();
            Assert.That(trailer, Is.Not.Null, $"{trailerGo.name} has no TowedBody.");
            Assert.That(trailer.IsCoupled, Is.False, $"{trailerGo.name} is already on somebody's plate.");
            Assert.That(trailer.LegsAreDown, Is.True, $"{trailerGo.name} is standing on her kingpin.");
            var trailerDoors = trailerGo.GetComponent<VehicleDoors>();
            Assert.That(trailerDoors, Is.Not.Null, $"{trailerGo.name} has no doors — no crank to work.");

            // ---- 3. Back under her: fast until near the bay, then a creep so the pin cannot overshoot
            // the seat before the brake bites.
            int steps = 0;
            while (hitch.CapturedTrailer() == null)
            {
                Assert.That(steps++, Is.LessThan(MaxStepsPerLeg),
                    $"{who}: backed for {MaxStepsPerLeg} steps and no pin entered the slot — she is at " +
                    $"{(Vector2)go.transform.position}, {Vector2.Distance(go.transform.position, bayPose):0.##} m " +
                    $"from her bay pose; {Window(hitch, trailer)}.");
                bool near = Vector2.Distance(go.transform.position, bayPose) < SlowNearTargetMetres;
                _held.Set(near ? -CreepReverseThrottle : -1f, 0f, false);
                yield return new WaitForFixedUpdate();
                SampleTheGroundUnder(go);
            }
            yield return Park(ctl, who);
            TowedBody inTheSlot = hitch.CapturedTrailer();
            Assert.That(inTheSlot, Is.SameAs(trailer),
                $"{who}: stopped with " + (inTheSlot == null ? "nothing" : inTheSlot.name) +
                $" in the slot rather than {trailerGo.name} — {Window(hitch, trailer)}.");

            // ---- 4. Out, and the release handle couples her: "Couple the trailer".
            yield return ClimbOut(door, who);
            Assert.That(hitch.IsAvailable, Is.True,
                $"{who}: pin in the slot and the release handle offers nothing.");
            Assert.That(hitch.VerbLabel, Is.EqualTo("Couple the trailer"));
            yield return PressEOnFootAt(hitch.WorldPosition, $"{who}'s release handle");
            Assert.That(hitch.IsCoupled, Is.True, $"{who}: E at the handle did not couple her.");
            Assert.That(trailer.CoupledTo, Is.SameAs(hitch), $"{trailerGo.name} does not know her tractor.");
            yield return WaitForTheCrank(trailer, trailerDoors, legsUp: true, who);

            // ---- 5. In, and tow her thirty metres through the lane, pin on the plate the whole way.
            yield return ClimbIn(door, who);
            Vector2 trailerStart = trailerGo.transform.position;
            Vector3 pinLocal = new Vector3(trailer.Kingpin.CouplingPointLocal.x,
                                           trailer.Kingpin.CouplingPointLocal.y, 0f);
            float odometerAtHitch = ctl.OdometerMeters;
            float worstPinOffPlate = 0f, worstDrawnPinOffPlate = 0f, worstFold = 0f;
            Vector2 laneCentre = LaneCentreUnderBay(placement.Unit.Bay);
            Vector2 laneEnd = TheLanesFarEnd();
            bool onTheLane = false;
            steps = 0;
            while (ctl.OdometerMeters - odometerAtHitch < TowMetres)
            {
                Assert.That(steps++, Is.LessThan(MaxStepsPerLeg),
                    $"{who}: {MaxStepsPerLeg} steps and the tow covered " +
                    $"{ctl.OdometerMeters - odometerAtHitch:0.##} m of {TowMetres}.");
                if (!onTheLane && Vector2.Distance(go.transform.position, laneCentre) <= WaypointReachMetres)
                    onTheLane = true;
                _held.Set(Toward(go.transform, onTheLane ? laneEnd : laneCentre, false));
                yield return new WaitForFixedUpdate();
                yield return null;   // the follow runs in LateUpdate, after the physics step
                SampleTheGroundUnder(go);

                worstPinOffPlate = Mathf.Max(worstPinOffPlate,
                    Vector2.Distance(trailer.KingpinWorld, hitch.CouplingPointWorld));
                worstDrawnPinOffPlate = Mathf.Max(worstDrawnPinOffPlate,
                    Vector2.Distance(trailerGo.transform.TransformPoint(pinLocal), hitch.CouplingPointWorld));
                worstFold = Mathf.Max(worstFold, Mathf.Abs(trailer.ArticulationAgainst(hitch.HeadingDegrees)));
            }
            yield return Park(ctl, who);

            Assert.That(trailer.IsCoupled, Is.True, $"{who}: the trailer came off during the tow.");
            Assert.That(worstPinOffPlate, Is.LessThan(0.05f),
                $"{who}: her pin came {worstPinOffPlate:0.###} m off the plate during the tow — she was " +
                "dragged alongside rather than pulled by the coupling.");
            Assert.That(worstDrawnPinOffPlate, Is.LessThan(0.05f),
                $"{who}: the PICTURE drew her pin {worstDrawnPinOffPlate:0.###} m off the plate — the " +
                "follow places her in a different frame from the one she is drawn in.");
            Assert.That(worstFold, Is.LessThanOrEqualTo(hitch.JackknifeCapDegrees + 0.01f),
                $"{who}: folded {worstFold:0.#}°, past the cap the geometry allows.");
            Assert.That(Vector2.Distance(trailerStart, trailerGo.transform.position), Is.GreaterThan(20f),
                $"{who}: towed {TowMetres} m and the trailer moved " +
                $"{Vector2.Distance(trailerStart, trailerGo.transform.position):0.##} m.");

            // ---- 6. Out; the release refuses on raised legs; wind them down at HER crank; pull the pin.
            yield return ClimbOut(door, who);
            yield return PressEOnFootAt(hitch.WorldPosition, $"{who}'s release handle, legs up");
            Assert.That(hitch.IsCoupled, Is.True,
                $"{who}: the pin was pulled with her legs up — that drops her nose in the yard.");

            VehicleDoorHandle crank = HandleOn(trailerGo, "gear");
            yield return PressEOnFootAt(crank.WorldPosition, $"{trailerGo.name}'s landing-gear crank");
            yield return WaitForTheCrank(trailer, trailerDoors, legsUp: false, who);

            Vector2 restingAt = trailerGo.transform.position;
            float restingHeading = trailer.HeadingDegrees;
            yield return PressEOnFootAt(hitch.WorldPosition, $"{who}'s release handle, legs down");
            Assert.That(hitch.IsCoupled, Is.False, $"{who}: legs down and the release still refused.");
            Assert.That(trailer.IsCoupled, Is.False, $"{trailerGo.name} still thinks she is on.");

            // ---- 7. In, and away bobtail: the trailer stays exactly where she was dropped.
            yield return ClimbIn(door, who);
            yield return DriveStraight(ctl, CruiseThrottle, BobtailMetres, who);
            yield return Park(ctl, who);

            Assert.That(Vector2.Distance(restingAt, trailerGo.transform.position), Is.LessThan(1e-3f),
                $"{who}: drove away and {trailerGo.name} came with her — the release did not let go of " +
                "the follow.");
            Assert.That(trailer.HeadingDegrees, Is.EqualTo(restingHeading).Within(1e-3f),
                $"{trailerGo.name} turned on her own after the tractor left.");
            Assert.That(hitch.IsAvailable, Is.False,
                $"{who}: bobtail and clear of the trailer, the handle still offers something.");
            Assert.That(_wetSamples, Is.EqualTo(0),
                $"{who}: {_wetSamples} physics steps stood on drowned ground, the first at {_firstWetSample}.");

            yield return ClimbOut(door, who);
        }

        // =============================================================================================
        //  THE WORLD, THE FISHER, THE PRESS
        // =============================================================================================

        /// <summary>Stand everything the builders stand — the laydown's nine, the Dually, the Otter —
        /// and give them the frames to skin. Eleven, asserted, so a def missing from the bake fails
        /// here rather than as a machine that was never driven.</summary>
        private IEnumerator StandTheWorld(List<GameObject> into)
        {
            into.AddRange(NineMileCreekLaydown.Place());
            GameObject dually = NineMileCreekTruckPark.Place();
            Assert.That(dually, Is.Not.Null, "the truck park stood no Dually — her def is missing.");
            into.Add(dually);
            GameObject otter = NineMileCreekOtterLanding.Place();
            Assert.That(otter, Is.Not.Null, "the landing stood no Otter — her def is missing.");
            into.Add(otter);

            for (int i = 0; i < into.Count; i++) _spawned.Add(into[i]);
            Assert.That(into.Count, Is.EqualTo(11), $"the builders stood {into.Count} of 11 machines.");

            yield return null;
            yield return null;
        }

        private static GameObject Named(List<GameObject> world, string name)
        {
            GameObject go = world.Find(g => g != null && g.name == name);
            Assert.That(go, Is.Not.Null, $"{name} was never placed.");
            return go;
        }

        private static VehicleController ControllerOf(GameObject go, string who)
        {
            var ctl = go.GetComponent<VehicleController>();
            Assert.That(ctl, Is.Not.Null, $"{who} grew no VehicleController — she is scenery.");
            return ctl;
        }

        private static VehicleDoor DoorOf(GameObject go, string who)
        {
            var door = go.GetComponent<VehicleDoor>();
            Assert.That(door, Is.Not.Null, $"{who} grew no driver's door at play.");
            return door;
        }

        private static NineMileCreekLaydown.Placement PlacementOf(string name)
        {
            foreach (NineMileCreekLaydown.Placement p in NineMileCreekLaydown.Solve())
                if (p.Unit.Name == name) return p;
            Assert.Fail($"no machine named '{name}' solves into the yard.");
            return default;
        }

        /// <summary>The fisher, on foot, with a switcher whose wheel is read from a HELD source — the
        /// one line that makes this file's drives survive a frame.</summary>
        private void StandTheFisherAt(Vector2 at)
        {
            _playerGo = new GameObject("Player", typeof(SpriteRenderer), typeof(Rigidbody2D));
            _spawned.Add(_playerGo);
            _walk = _playerGo.AddComponent<PlayerWalkController>();
            MoveTheFisherTo(at);

            var switcherGo = new GameObject("Switcher");
            _spawned.Add(switcherGo);
            _switcher = switcherGo.AddComponent<ControlSwitcher>();
            _switcher.Configure(_walk, null, null, null, 0f, null);

            _held = new HeldDriveInput();
            _switcher.ConfigureDriveInput(_held);
        }

        /// <summary>Walking is not this file's subject, so a walk is a move: transform AND body
        /// (autoSyncTransforms is off), and the body is stilled.</summary>
        private void MoveTheFisherTo(Vector2 at)
        {
            _playerGo.transform.position = new Vector3(at.x, at.y, _playerGo.transform.position.z);
            var rb = _playerGo.GetComponent<Rigidbody2D>();
            rb.position = at;
            rb.linearVelocity = Vector2.zero;
        }

        /// <summary>The E press on foot, without a keyboard (memory
        /// <c>playmode-virtual-keypress-is-undeliverable</c>): the verb resolved at a world point exactly
        /// as the key's own handler resolves it.</summary>
        private IEnumerator PressEOnFootAt(Vector2 at, string what)
        {
            Assert.That(_switcher.Mode, Is.EqualTo(ControlMode.OnFoot), $"fixture: not on foot at {what}.");
            MoveTheFisherTo(at);
            yield return null;
            var actor = new InteractActor(at, Vector2.zero, InteractContext.OnFoot);
            Assert.That(InteractVerb.TryPerform(actor, 180f), Is.True,
                $"standing on {what} at {at}, E resolved nothing.");
            yield return null;
        }

        private IEnumerator ClimbIn(VehicleDoor door, string who)
        {
            Assert.That(door.IsDrivable, Is.True,
                $"{who}: her door reports NOT drivable with the shipped assets — E is dead at her.");
            yield return PressEOnFootAt(door.DoorWorldPosition, $"{who}'s driver's door");
            Assert.That(_switcher.Mode, Is.EqualTo(ControlMode.Driving),
                $"{who}: E at her door and nobody took the wheel.");
            Assert.That(_switcher.DrivenSeat, Is.SameAs(door), $"{who}: the wheel taken is not hers.");
        }

        /// <summary>E in the cab, through the key's own handler: the fisher is set down at her door, on
        /// their feet, on ground a person can stand on.</summary>
        private IEnumerator ClimbOut(VehicleDoor door, string who)
        {
            Assert.That(_switcher.Mode, Is.EqualTo(ControlMode.Driving),
                $"{who}: not driving, so there is nothing to get out of.");
            Vector2 landing = door.DoorWorldPosition;

            Assert.That(_switcher.BeginInteract(), Is.True, $"{who}: E in the cab was not answered.");
            yield return null;

            Assert.That(_switcher.Mode, Is.EqualTo(ControlMode.OnFoot),
                $"{who}: E in the cab did not put the fisher on their feet — the door refused " +
                $"(\"{ControlSwitcher.NoticeTooDeepToStepOut}\"?) or the press went elsewhere.");
            float apart = Vector2.Distance(_playerGo.transform.position, landing);
            Assert.That(apart, Is.LessThan(0.05f), $"{who}: set down {apart:0.###} m from her door.");
            Assert.That(VehicleGrounding.IsDryLandNow(landing), Is.True,
                $"{who}: her door opened onto water at {landing} — the fisher is standing in the sea.");
            Assert.That(_walk.enabled, Is.True, $"{who}: on foot, and the walk is dead.");
        }

        // =============================================================================================
        //  THE SCRIPTED DRIVER
        // =============================================================================================

        /// <summary>Pure pursuit in compass terms. The error is the target's bearing less her heading,
        /// positive when it lies clockwise (to her RIGHT) — and right is −1 on the wheel, the rig's own
        /// sense, so the steer is the negated, gained error.</summary>
        private static DriveDemand Toward(Transform machine, Vector2 target, bool slow) =>
            RouteFollowMath.Toward(BoatKinematics.BearingDegrees(machine.up), machine.position, target,
                                   slow, TestDriver);

        /// <summary>
        /// A waypoint is reached when she is within <paramref name="reach"/> of it — OR when she has passed
        /// the plane through it perpendicular to the leg. The second clause is the pure-pursuit switching
        /// rule, and it is not optional: a machine whose turning circle is wider than the reach cannot come
        /// back to a waypoint she overshot at an angle, and orbits it for ever. Measured before this clause
        /// existed — five machines circling the yard's gate at 1.65 m/s, 3000 steps each.
        /// </summary>
        private static bool Reached(Vector2 from, Vector2 target, Vector2 pos, float reach) =>
            RouteFollowMath.HasReached(from, target, pos, reach);

        /// <summary>Drive her to a waypoint (see <see cref="Reached"/>), through the switcher, one physics
        /// step at a time, sampling the ground under her every step.</summary>
        private IEnumerator DriveTo(GameObject machine, VehicleController ctl, Vector2 target, float reach,
                                    bool slowIn, string who)
        {
            Vector2 from = machine.transform.position;
            int steps = 0;
            while (!Reached(from, target, machine.transform.position, reach))
            {
                Assert.That(steps++, Is.LessThan(MaxStepsPerLeg),
                    $"{who}: {MaxStepsPerLeg} physics steps and she has not reached {target} — she is at " +
                    $"{(Vector2)machine.transform.position} on " +
                    $"{BoatKinematics.BearingDegrees(machine.transform.up):0.#}° doing " +
                    $"{ctl.SpeedMetersPerSecond:0.##} m/s. The route is not driveable, or the seam is not " +
                    "carrying the demand.");
                bool near = slowIn && Vector2.Distance(machine.transform.position, target) < SlowNearTargetMetres;
                _held.Set(Toward(machine.transform, target, near));
                yield return new WaitForFixedUpdate();
                SampleTheGroundUnder(machine);
            }
        }

        /// <summary>Hold a fixed demand until her odometer has moved <paramref name="metres"/>.</summary>
        private IEnumerator DriveStraight(VehicleController ctl, float throttle, float metres, string who)
        {
            float from = ctl.OdometerMeters;
            int steps = 0;
            _held.Set(throttle, 0f, false);
            while (Mathf.Abs(ctl.OdometerMeters - from) < metres)
            {
                Assert.That(steps++, Is.LessThan(MaxStepsPerLeg),
                    $"{who}: {MaxStepsPerLeg} physics steps at throttle {throttle} and the odometer moved " +
                    $"{ctl.OdometerMeters - from:0.##} m of {metres} — the seam is not carrying the demand.");
                yield return new WaitForFixedUpdate();
                SampleTheGroundUnder(ctl.gameObject);
            }
        }

        /// <summary>
        /// Follow the road's own centre-line — the last leg of every road journey. Not a waypoint: the
        /// target is re-derived every step as the point on the line a lookahead ahead of her own
        /// projection, so she CONVERGES onto the road rather than passing through one point on it at
        /// whatever angle she arrived. The leg ends when she is at least <paramref name="metres"/> past the
        /// join along the road AND inside the carriageway's own half-width of its centre-line — the second
        /// clause is what makes "parked on the road" a thing she has done rather than a thing she was
        /// near. Slow throughout.
        /// </summary>
        private IEnumerator DriveAlongTheRoad(GameObject machine, VehicleController ctl, Vector2[] road,
                                              Vector2 join, float metres, string who)
        {
            Vector2 dirAtJoin = RoadDirectionAt(road, join);
            int steps = 0;
            while (true)
            {
                Vector2 pos = machine.transform.position;
                Vector2 onLine = NineMileCreekRoads.NearestPointOnRoute(road, pos);
                float along = Vector2.Dot(pos - join, dirAtJoin);
                float off = Vector2.Distance(pos, onLine);
                if (along >= metres && off <= NineMileCreekRoads.CarriagewayHalfWidthMetres) break;

                Assert.That(steps++, Is.LessThan(MaxStepsPerLeg),
                    $"{who}: {MaxStepsPerLeg} physics steps along the road and she is {along:0.##} m past " +
                    $"the join, {off:0.##} m off the centre-line — at {pos} on " +
                    $"{BoatKinematics.BearingDegrees(machine.transform.up):0.#}°. She is not converging " +
                    "onto the road.");
                Vector2 target = onLine + RoadDirectionAt(road, onLine) * RoadLookaheadMetres;
                _held.Set(Toward(machine.transform, target, true));
                yield return new WaitForFixedUpdate();
                SampleTheGroundUnder(machine);
            }
        }

        private IEnumerator Park(VehicleController ctl, string who)
        {
            _held.Set(0f, 0f, true);
            for (int i = 0; i < MaxStepsToStop && Mathf.Abs(ctl.SpeedMetersPerSecond) > 0.05f; i++)
                yield return new WaitForFixedUpdate();
            Assert.That(Mathf.Abs(ctl.SpeedMetersPerSecond), Is.LessThan(0.05f),
                $"{who}: on the brake for {MaxStepsToStop} steps and still doing " +
                $"{ctl.SpeedMetersPerSecond:0.##} m/s.");
            _held.Release();
            yield return null;
        }

        /// <summary>The law the yard is sited on, sampled every physics step: a road machine's origin
        /// is on EXPOSED ground. Counted, not asserted here, so the assertion can name the first miss.</summary>
        private void SampleTheGroundUnder(GameObject machine)
        {
            Vector2 at = machine.transform.position;
            if (VehicleGrounding.IsDryLandNow(at)) return;
            if (_wetSamples++ == 0) _firstWetSample = at;
        }

        /// <summary>What every road journey must show at its end: the source was consulted, no metre of
        /// it was under water, and she is ON the road — within the carriageway's own half-width of its
        /// centre-line, not stopped across its mouth.</summary>
        private void AssertTheDriveWasHonest(GameObject go, string who)
        {
            Assert.That(_held.Reads, Is.GreaterThan(0), "the switcher never asked the source — the seam is not wired.");
            Assert.That(_wetSamples, Is.EqualTo(0),
                $"{who}: {_wetSamples} physics steps stood on drowned ground, the first at {_firstWetSample} — " +
                "the route is not dry.");

            Vector2 parked = go.transform.position;
            float offCentre = Vector2.Distance(parked, NineMileCreekRoads.NearestPointOnRoute(TheRoadTheSpurJoins(), parked));
            Assert.That(offCentre, Is.LessThanOrEqualTo(NineMileCreekRoads.CarriagewayHalfWidthMetres),
                $"{who} parked {offCentre:0.##} m off the road's centre-line — not on the road.");
        }

        private IEnumerator WaitForTheCrank(TowedBody trailer, VehicleDoors doors, bool legsUp, string who)
        {
            for (int i = 0; i < MaxStepsForTheCrank && (doors.IsMoving || trailer.LegsAreDown == legsUp); i++)
                yield return new WaitForFixedUpdate();
            Assert.That(doors.IsMoving, Is.False, $"{who}: the crank is still turning after {MaxStepsForTheCrank} steps.");
            Assert.That(trailer.LegsAreDown, Is.EqualTo(!legsUp),
                $"{who}: the crank ran and her legs are " + (trailer.LegsAreDown ? "still down." : "still up."));
        }

        private static VehicleDoorHandle HandleOn(GameObject machine, string groupId)
        {
            foreach (VehicleDoorHandle h in machine.GetComponentsInChildren<VehicleDoorHandle>())
                if (h.Id.Contains("." + groupId + "#")) return h;
            Assert.Fail($"{machine.name} has no '{groupId}' handle — the art publishes no reach point for " +
                        "it, or the skinner stood none.");
            return null;
        }

        private static string Window(VehicleHitch hitch, TowedBody trailer)
        {
            VehicleFifthWheel w = hitch.FifthWheel;
            Vector2 pinWorld = trailer.KingpinWorld;
            Vector3 local = hitch.transform.InverseTransformPoint(new Vector3(pinWorld.x, pinWorld.y, 0f));
            float aft = Mathf.Min(w.RampMouthY, w.SlotSeatY), fore = Mathf.Max(w.RampMouthY, w.SlotSeatY);
            return $"pin local ({local.x:0.###}, {local.y:0.###}); slot x {w.CouplingPointLocal.x:0.###}" +
                   $"±{w.SlotHalfWidthMeters:0.###}, y [{aft:0.###} … {fore:0.###}]; heading Δ " +
                   $"{Mathf.DeltaAngle(hitch.HeadingDegrees, trailer.HeadingDegrees):0.##}° of " +
                   $"{VehicleCouplingMath.CaptureHeadingToleranceDegrees(w):0.##}°";
        }

        // =============================================================================================
        //  THE ROUTE — every point derived from the plan's published constants, never typed
        // =============================================================================================

        private static Vector2 LaneCentreUnderBay(int bay) =>
            new Vector2(NineMileCreekLaydown.BayCentreX(bay), NineMileCreekLaydown.LaneArea().center.y);

        /// <summary>The lane's far end — its east end, set in by half its own width.</summary>
        private static Vector2 TheLanesFarEnd() =>
            new Vector2(NineMileCreekLaydown.ApronArea().xMax - NineMileCreekLaydown.LaneWidthMetres * 0.5f,
                        NineMileCreekLaydown.LaneArea().center.y);

        /// <summary>Where the laydown spur crosses the apron's south edge — the yard's gate, derived from
        /// the same two constants the spur is.</summary>
        private static Vector2 TheYardsGate()
        {
            Rect apron = NineMileCreekLaydown.ApronArea();
            Vector2 yard = NineMileCreekMainland.LaydownPos;
            Vector2 park = NineMileCreekMainland.TruckParkPos;
            float t = (yard.y - apron.yMin) / (yard.y - park.y);
            return Vector2.Lerp(yard, park, Mathf.Clamp01(t));
        }

        /// <summary>The park bay beside the Dually's — through the park, not through her.</summary>
        private static Vector2 TheParksSpareBay() =>
            (Vector2)NineMileCreekMainland.TruckParkPos
            + new Vector2(NineMileCreekRoads.ParkedVehicleLengthMetres, 0f);

        private static Vector2 WhereTheSpurMeetsTheRoad() => NineMileCreekRoads.ParkSpurRoute()[0];

        /// <summary>Whichever of the two carriageways the park spur joins — the one the join point lies
        /// on — so a walk that moves the park nearer the through-road moves this with it.</summary>
        private static Vector2[] TheRoadTheSpurJoins()
        {
            Vector2 join = WhereTheSpurMeetsTheRoad();
            Vector2[] wharf = NineMileCreekMainland.WharfRoad, through = NineMileCreekMainland.ThroughRoad;
            float onWharf = Vector2.Distance(join, NineMileCreekRoads.NearestPointOnRoute(wharf, join));
            float onThrough = Vector2.Distance(join, NineMileCreekRoads.NearestPointOnRoute(through, join));
            return onWharf <= onThrough ? wharf : through;
        }

        /// <summary>The road's direction at a point on (or near) it — the segment whose nearest point to
        /// <paramref name="at"/> is closest, taken in the order the polyline is published.</summary>
        private static Vector2 RoadDirectionAt(Vector2[] road, Vector2 at) =>
            RouteFollowMath.DirectionAt(road, 0, road.Length, at);
    }
}
#endif
