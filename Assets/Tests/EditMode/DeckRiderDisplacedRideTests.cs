using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using HiddenHarbours.Player;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>THE FISHER'S BOOTS STAY ON THE SAME PLANK</b> — owner, 2026-08-11: a character standing on
    /// deck <i>"stays static in space with a rock animation but is not fixed to the same point on the
    /// bobbing boat deck"</i>.
    ///
    /// <para><b>The defect was a missing channel, not a wrong number.</b> A hull's drawn motion has two
    /// parts. The ROCK CYCLE — the in-place lean and heave her art draws — reaches her passengers as a
    /// published wave phase, and <c>DeckRideMathTests</c> pins that. The DISPLACED RIDE — the whole boat
    /// translated bodily by the water's lift under her (ADR 0023, the default sea) — never reached them
    /// at all. It is the larger of the two by a factor of tens, so the fisher kept her physics-root
    /// altitude while the drawn deck went up and down past her.</para>
    ///
    /// <para><b>What is pinned here, and why it is pinned this way.</b> Every assertion below compares
    /// the rider against <i>what the hull's DRAWER was actually handed</i>
    /// (<c>IHullMeshRenderer.RidePixels</c>) — never against a freshly-sampled sea. That is the whole
    /// point of the fix and the whole point of the test: the ride passes through a STATEFUL
    /// spring-damper in a storm (<see cref="StormRockMath.StepHeaveWeight"/> — hulls unweight and fall
    /// at g over a sharpened crest), and a second copy of a stateful smoother agrees only with itself.
    /// A test that re-derived the expected ride would pass against a rider that also re-derived it, and
    /// both would drift off the boat together in the sea where it matters most.</para>
    ///
    /// <para><b>The A/B is measured by DIFFERENCE.</b> Each frame is posed twice at the same instant —
    /// once with the new term switched off, once on — with nothing ticked in between, so the rock cycle
    /// is bit-identical across the pair and cancels exactly. What is left is the ride and only the ride.
    /// No expected value is ever restated from the formula.</para>
    ///
    /// <para>Headless and GPU-free: the mesh renderer behind the Core seam is a test double, the clock
    /// and the sea are scripted, and the real components run their real tick bodies in their real
    /// production order (wave −120 → driver −110 → rider +100). Same harness pattern as
    /// <c>SharedHeaveTests</c> and <c>DeckRiderHullSwapTests</c>.</para>
    /// </summary>
    public class DeckRiderDisplacedRideTests
    {
        const float Dt = 1f / 60f;
        const int PxPerMetre = 32;
        const float Elevation = 40f;      // every boat rig's bake elevation
        const float Draft = 0.5f;         // her DESIGN WATERLINE, metres above the keel-origin rig
        const float Band = 0.6f;
        const float Exag = 1.5f;

        /// <summary>What the driver actually subtracts for <see cref="Draft"/> — the datum through the
        /// iso projection (<see cref="HullSettleMath"/>). Derived, never re-typed: a test that re-typed
        /// it would stop measuring the law and start measuring itself.</summary>
        static readonly float Sink = HullSettleMath.AppliedSinkMeters(Draft, Elevation);

        readonly List<Object> _spawned = new List<Object>();
        readonly object _seaOwner = new object();
        IHullMeshPresentationService _previousService;

        [SetUp]
        public void SetUp() => _previousService = HullMeshPresentation.Service;

        [TearDown]
        public void TearDown()
        {
            DisplacedSea.Clear(_seaOwner);        // never leak an active sea into another fixture
            HullMeshPresentation.Service = _previousService;
            GameServices.Reset();
            foreach (var o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        // ---- doubles (the Core seams; no URP, no GPU, no wall clock) -------------------------------

        sealed class ScriptedClock : IGameClock
        {
            public double TotalSeconds { get; private set; }
            public void Advance(double dt) => TotalSeconds += dt;
            public GameTime Now => new GameTime(TotalSeconds);
            public bool IsPaused { get; set; }
            public float TimeScale { get; set; } = 1f;
            public int DayIndex => 0;
            public Season Season => Season.EarlySpring;
            public int Year => 1;
            public int DayOfSeason => 1;
            public Weekday Weekday => Weekday.Monday;
            public bool IsMarketDay => false;
            public float DayFraction => 0f;
            public float HourOfDay => 12f;
            public void SeekTo(double totalSeconds) => TotalSeconds = totalSeconds;
        }

        sealed class ScriptedSea : IEnvironmentService
        {
            public Vector2 Wind = new Vector2(6f, 3f);
            public float SeaState01 = 0.75f;
            public int WorldSeed => 0;
            public TideProfile ActiveTideProfile { get; set; }
            public EnvironmentSample Sample() => new EnvironmentSample(
                Wind, Vector2.zero, tideHeight: 0f, HiddenHarbours.Core.SeaState.Moderate,
                visibility: 1f, seaState01: SeaState01);
            public float TideHeightAt(double totalSeconds) => 0f;
            public float WaterLevelAt(double totalSeconds) => 0f;
        }

        /// <summary>The hull's drawer, recording. <see cref="RidePixels"/> is the load-bearing member:
        /// it is the WORLD translation the driver applied to her image this frame, and therefore the
        /// exact distance anything standing on her deck has to travel.</summary>
        sealed class FakeRenderer : IHullMeshRenderer, IDeckOccupantSlots
        {
            public float HeadingDirUnits { get; set; }
            public float RollDegrees { get; set; }
            public float PitchDegrees { get; set; }
            public float HeavePixels { get; set; }
            public float RidePixels { get; set; }
            public bool IsConfigured => true;
            public void SetSorting(int layerId, int order) { }
            public void SetDeckOccupant(Vector3 rigLocalMeters, bool active) { }
            public float DeckOccluderId => 7f / 255f;
            public IDeckOccupantSlots DeckOccupants => this;

            public int Capacity => 1;
            public int ActiveCount => 1;
            public int Claim(object owner) => 0;
            public void Release(int slot, object owner) { }
            public void Set(int slot, object owner, Vector3 rigLocalMeters, bool active) { }
            public float OccluderId(int slot) => DeckOccluderId;
            public float OccluderIdTop => DeckOccluderId;
        }

        sealed class FakeService : IHullMeshPresentationService
        {
            public readonly FakeRenderer Renderer = new FakeRenderer();
            public IHullMeshRenderer Install(GameObject host, HullMeshDef def,
                                             HullPaintSchemeDef scheme = null) => Renderer;
            public IHullPropRenderer AttachProp(GameObject host, HullPropMeshDef def, string slot) => null;
            public void DetachProps(GameObject host) { }
            public void DetachProp(GameObject host, string slot) { }
            public void Remove(GameObject host) { }
        }

        // ---- the rig: a real mesh hull, skinned the production way, with a fisher on her deck ------

        /// <summary>A <see cref="GameConfig"/> whose B2.5 storm block is on or off. Off is the calm
        /// A/B side (the weight spring is bypassed exactly); on is where the spring lives.</summary>
        GameConfig MakeConfig(bool storm)
        {
            var config = ScriptableObject.CreateInstance<GameConfig>();
            _spawned.Add(config);
            StormRockSettings settings = config.StormRock;
            settings.Enabled = storm;
            config.StormRock = settings;
            return config;
        }

        BoatVisualDef MakeMeshVisual(float rockHeavePixels)
        {
            var mesh = new Mesh(); _spawned.Add(mesh);
            var def = ScriptableObject.CreateInstance<HullMeshDef>(); _spawned.Add(def);
            def.Id = "hullmesh.rider_ride";
            def.Mesh = mesh;
            def.Ramps = new[] { new HullMeshDef.Ramp { Colors = new[] { new Color32(1, 2, 3, 255) }, Offset = 0 } };
            def.Bayer16 = new float[16];
            def.PxPerMetre = PxPerMetre;
            def.CellW = 456; def.CellH = 420;
            def.ElevationDeg = Elevation;
            def.AzimuthCounterClockwise = true;
            def.RockRollDegrees = 0f;
            def.RockPitchDegrees = 0f;
            def.RockHeavePixels = rockHeavePixels;
            def.RestingDraftMeters = Draft;

            var v = ScriptableObject.CreateInstance<BoatVisualDef>(); _spawned.Add(v);
            v.Id = "visual.rider_ride_mesh";
            v.Variant = BoatHullVariant.Mesh;
            v.HullMesh = def;
            v.ArtBakeElevationDegrees = Elevation;
            return v;
        }

        sealed class Rig
        {
            public FakeService Service;
            public ScriptedClock Clock;
            public GameObject Root;
            public BoatWaveMotion Wave;
            public MeshHullDriver Driver;
            public DeckRiderVisual Rider;
            public Transform RiderChild;
            public Vector3 RiderChildBase;

            /// <summary>The world metres the hull's DRAWER was told to translate her image by this
            /// frame. The one number the fisher has to match.</summary>
            public float DrawnRideMeters => Service.Renderer.RidePixels / PxPerMetre;

            /// <summary>One production frame, in production order: the wave motion samples and weights
            /// the sea (−120), the driver settles and applies it (−110), and only then does anyone
            /// standing on the deck read it (+100). Getting this order wrong is how a rider ends up a
            /// frame behind the boat, which on a metre-scale ride is a fisher standing in mid-air.</summary>
            public void Step(float dt = Dt)
            {
                Clock.Advance(dt);
                Wave.Tick();
                Driver.Drive();
                Pump();
            }

            /// <summary>Re-pose the rider without advancing anything — the EditMode stand-in for its
            /// LateUpdate (<see cref="DeckRiderVisual.SetMode"/> states the context and applies, which
            /// is exactly what the frame does). Same trick <c>DeckRiderHullSwapTests</c> uses.</summary>
            public void Pump() => Rider.SetMode(ControlMode.OnDeck, Root.transform);
        }

        Rig NewRig(float seaState01, bool storm = false, float rockHeavePixels = 0f)
        {
            var service = new FakeService();
            HullMeshPresentation.Service = service;

            var clock = new ScriptedClock();
            GameServices.Clock = clock;
            GameServices.Environment = new ScriptedSea { SeaState01 = seaState01 };
            GameServices.Config = MakeConfig(storm);

            var root = new GameObject("Boat"); _spawned.Add(root);
            BoatHullSkinner.Apply(root, MakeMeshVisual(rockHeavePixels), boat: null,
                                  new BoatHullSkinner.Options { SkipOars = true });

            var wave = root.GetComponent<BoatWaveMotion>();
            var driver = root.GetComponent<MeshHullDriver>();
            Assert.IsNotNull(wave, "harness: the skinner must have installed the wave motion");
            Assert.IsNotNull(driver, "harness: the skinner must have taken the MESH branch");

            // The player, standing on her. Only what the rider needs: a body to mirror and a child to
            // lean and lift — the same minimal rig DeckRiderHullSwapTests builds.
            var playerGo = new GameObject("Player"); _spawned.Add(playerGo);
            var body = playerGo.AddComponent<SpriteRenderer>();
            var character = playerGo.AddComponent<IsoCharacterSprite>();
            var childGo = new GameObject("DeckRider");
            childGo.transform.SetParent(playerGo.transform, false);
            var childSr = childGo.AddComponent<SpriteRenderer>();
            childSr.enabled = false;

            var rider = playerGo.AddComponent<DeckRiderVisual>();
            rider.Configure(childSr, body, character);

            var rig = new Rig
            {
                Service = service, Clock = clock, Root = root, Wave = wave, Driver = driver,
                Rider = rider, RiderChild = childGo.transform,
                RiderChildBase = childGo.transform.localPosition,
            };
            rig.Pump();   // bind: resolve the boat's rock, helm and skin once
            return rig;
        }

        /// <summary>The lift the rider takes with the displaced ride switched OFF and then ON, at the
        /// SAME instant — nothing is ticked between the two poses, so the rock cycle is bit-identical
        /// across the pair and subtracting them leaves the ride and nothing else.</summary>
        static void PoseBothWays(Rig rig, out float without, out float with, out float drawnChildDelta)
        {
            Tune(rig, hullRide: 0f);
            rig.Pump();
            without = rig.Rider.Pose.LiftMeters;
            float childWithout = rig.RiderChild.localPosition.y;

            Tune(rig, hullRide: 1f);
            rig.Pump();
            with = rig.Rider.Pose.LiftMeters;
            drawnChildDelta = rig.RiderChild.localPosition.y - childWithout;
        }

        static void Tune(Rig rig, float hullRide, float rideStrength = 1f)
            => rig.Rider.ConfigureRide(rideStrength, deckRollDegrees: 5f, deckHeavePixels: 1.6f,
                                       deckPitchLiftMeters: 0.02f, pixelsPerUnit: 32f, footing: 0.6f,
                                       hullRide: hullRide);

        // ---- the headline ---------------------------------------------------------------------------

        [Test]
        public void TheFisher_RidesTheHeaveTheHullSaysItDREW_EveryFrameOfASail()
        {
            // THE DEFECT, measured. Pre-fix the difference below is exactly 0 on every frame: the rider
            // read only the rock phase, so a metre of swell moved the boat and not the fisher on it.
            using var sea = Sea();
            var rig = NewRig(seaState01: 0.75f);
            for (int w = 0; w < 5; w++) rig.Step();          // animator warm-up (the snap frame)

            int meaningful = 0;
            for (int f = 0; f < 120; f++)
            {
                rig.Step();
                float drawn = rig.DrawnRideMeters;
                PoseBothWays(rig, out float without, out float with, out float childDelta);

                Assert.AreEqual(drawn, with - without, 1e-4f,
                    $"frame {f}: the fisher must move by the ride the hull's DRAWER was handed — not " +
                    "by a second read of the sea, and not by a fraction of it.");
                Assert.AreEqual(drawn, childDelta, 1e-4f,
                    $"frame {f}: …and it must reach the CHILD transform that is actually drawn, not " +
                    "stop at the reported pose.");
                if (Mathf.Abs(drawn) > 0.05f) meaningful++;
            }
            Assert.Greater(meaningful, 30,
                "the reference sail barely moved — the ride never passed 5 cm, so the identity above " +
                "was vacuous. Did the boat stop riding the sea?");
        }

        [Test]
        public void TheRiderPublishesTheSameNumberTheDrawerGot_SoNothingCanReadAThirdOpinion()
        {
            // The seam itself: hull.DrawnRideMeters and the drawer's RidePixels are the same float told
            // twice. If these ever part company, everything above is measuring a copy.
            using var sea = Sea();
            var rig = NewRig(seaState01: 0.75f);
            for (int f = 0; f < 40; f++)
            {
                rig.Step();
                Assert.AreEqual(rig.DrawnRideMeters, rig.Rider.HullDrawnRideMeters, 1e-6f, $"frame {f}");
            }
        }

        // ---- the negative controls ------------------------------------------------------------------

        [Test]
        public void DisplacedSeaOFF_TheFisherIsBitIdenticalToBefore()
        {
            // The A/B contract, extended to the boat's passengers: with no displaced sea published there
            // is no ride to take, and "no ride" is exactly 0 rather than nearly 0.
            var rig = NewRig(seaState01: 0.75f);          // note: no Sea() — nothing published
            for (int f = 0; f < 60; f++)
            {
                rig.Step();
                Assert.AreEqual(0f, rig.DrawnRideMeters, 0f, $"frame {f}: the hull draws no ride");
                Assert.AreEqual(0f, rig.Rider.HullDrawnRideMeters, 0f, $"frame {f}: and reports none");

                PoseBothWays(rig, out float without, out float with, out float childDelta);
                Assert.AreEqual(without, with, 0f,
                    $"frame {f}: switching the new term on must change NOTHING with the sea off");
                Assert.AreEqual(0f, childDelta, 0f, $"frame {f}: and move the drawn child not at all");
            }
        }

        [Test]
        public void MasterRideStrengthZero_StillRestoresTheBoltUprightRead_RideOrNoRide()
        {
            // The owner's existing A/B must survive the new term: master 0 is the whole feature off,
            // including a metre-scale ride the hull is genuinely drawing.
            using var sea = Sea();
            var rig = NewRig(seaState01: 0.75f);
            for (int w = 0; w < 10; w++) rig.Step();

            Tune(rig, hullRide: 1f, rideStrength: 0f);
            for (int f = 0; f < 30; f++)
            {
                rig.Step();
                Assert.AreEqual(0f, rig.Rider.Pose.LiftMeters, 0f,
                    $"frame {f}: master 0 is level, not nearly level");
                Assert.AreEqual(rig.RiderChildBase.y, rig.RiderChild.localPosition.y, 0f,
                    $"frame {f}: and the drawn child sits exactly where it was built");
            }
        }

        // ---- the settle level reaches her passengers too ---------------------------------------------

        [Test]
        public void OnGlassWater_TheFisherStandsOnTheDeckAsDRAWN_NotAboveIt()
        {
            // A mesh hull is sunk to her design waterline whenever the displaced sea is live — a
            // CONSTANT, and one applied inside MeshHullDriver where nothing else can see it. Before
            // this, the fisher floated that constant above the planking on every calm day, on every
            // hull in the fleet, in proportion to how big the boat was (the dory ~11 cm; a tanker
            // metres). Reading the applied ride buys it for nothing.
            using var sea = Sea();
            var rig = NewRig(seaState01: 0f);             // glass: the field's amplitudes are exactly 0
            for (int w = 0; w < 10; w++) rig.Step();

            for (int f = 0; f < 20; f++)
            {
                rig.Step();
                Assert.AreEqual(-Sink, rig.DrawnRideMeters, 1e-3f,
                    "glass + displaced ON: the whole of the hull's ride is her settle sink");
                PoseBothWays(rig, out float without, out float with, out _);
                Assert.AreEqual(-Sink, with - without, 1e-3f,
                    "so the fisher stands that much lower — on the deck rather than above it");
            }
        }

        // ---- the storm: the spring is the reason this is READ and not recomputed ---------------------

        [Test]
        public void InAStorm_TheFisherFollowsTheWEIGHTEDRide_TheOneTheSpringActuallyProduced()
        {
            // ACCEPTANCE 3. In a storm the ride passes through a stateful, gravity-capped spring-damper
            // before it is drawn: the hull unweights off a sharpened crest and falls at g. A rider that
            // recomputed the ride would run a SECOND spring, seeded at a different moment, and the two
            // would part company exactly when the sea is worst. Reading the applied value makes this
            // free — which is why the assertion is against what the drawer got, at the same tolerance
            // as the calm case, with no storm-specific slack.
            using var sea = Sea();
            var rig = NewRig(seaState01: 0.95f, storm: true);
            for (int w = 0; w < 20; w++) rig.Step();

            var weighted = new List<float>();
            for (int f = 0; f < 120; f++)
            {
                rig.Step();
                float drawn = rig.DrawnRideMeters;
                weighted.Add(drawn);
                PoseBothWays(rig, out float without, out float with, out _);
                Assert.AreEqual(drawn, with - without, 1e-4f,
                    $"frame {f}: the fisher takes the ride the hull APPLIED, spring and all");
            }

            // …and the spring was genuinely in the path, or the claim above is only the calm claim
            // wearing a storm's name. The same sail with the storm block OFF bypasses the weight
            // filter exactly (B2.5's own negative control), so the two ride histories must differ.
            var unweighted = SailRides(seaState01: 0.95f, storm: false, frames: 120, warmUp: 20);
            float maxDivergence = 0f;
            for (int f = 0; f < weighted.Count; f++)
                maxDivergence = Mathf.Max(maxDivergence, Mathf.Abs(weighted[f] - unweighted[f]));
            Assert.Greater(maxDivergence, 0.02f,
                "the weight filter never bent the ride, so this test proved nothing about the spring — " +
                "check the storm block is actually engaging at this sea state.");
        }

        List<float> SailRides(float seaState01, bool storm, int frames, int warmUp)
        {
            var rig = NewRig(seaState01, storm);
            for (int w = 0; w < warmUp; w++) rig.Step();
            var rides = new List<float>(frames);
            for (int f = 0; f < frames; f++) { rig.Step(); rides.Add(rig.DrawnRideMeters); }
            return rides;
        }

        // ---- the ride is ON TOP of the rock, never instead of it -------------------------------------

        [Test]
        public void TheRide_ComposesWithTheRockCycle_RatherThanReplacingIt()
        {
            // Two channels, both alive: the hull's own baked rock (which the fisher already took) and
            // the sea moving the whole boat (which she now takes as well). A fix that replaced one with
            // the other would read as "better" on a rough day and lose the lean entirely.
            using var sea = Sea();
            var rig = NewRig(seaState01: 0.75f, rockHeavePixels: 1.2f);
            for (int w = 0; w < 20; w++) rig.Step();

            int leaning = 0, rocking = 0;
            for (int f = 0; f < 60; f++)
            {
                rig.Step();
                PoseBothWays(rig, out float without, out float with, out _);
                Assert.AreEqual(rig.DrawnRideMeters, with - without, 1e-4f, $"frame {f}");
                if (Mathf.Abs(rig.Rider.Pose.RollDegrees) > 1e-3f) leaning++;
                if (Mathf.Abs(without) > 1e-4f) rocking++;
            }
            Assert.Greater(rocking, 20,
                "the rock cycle's own lift vanished — the ride replaced it instead of adding to it");
            Assert.Greater(leaning, 20,
                "and she stopped leaning: the ride must not have reached the ROLL channel");
        }

        // ---- helpers --------------------------------------------------------------------------------

        /// <summary>Publish the displaced sea for the life of a test, and clear it afterwards however
        /// the test leaves.</summary>
        SeaScope Sea(float exaggeration = Exag) => new SeaScope(this, exaggeration);

        readonly struct SeaScope : System.IDisposable
        {
            readonly DeckRiderDisplacedRideTests _owner;
            public SeaScope(DeckRiderDisplacedRideTests owner, float exaggeration)
            {
                _owner = owner;
                DisplacedSea.Publish(owner._seaOwner, new DisplacedSeaState(exaggeration, Band));
            }
            public void Dispose() => DisplacedSea.Clear(_owner._seaOwner);
        }
    }
}
