using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
using Is = UnityEngine.TestTools.Constraints.Is;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>SHE TRIMS TO HER SPEED — the picture</b> (owner, 2026-09-21: <i>"Also i want trim added to
    /// the boats depending on speed and deacceleration"</i>; design <c>boats-and-navigation.md</c>
    /// §2.7.3). <see cref="MeshHullDriver"/> is the ONE writer of her drawn attitude: it eases the
    /// target her physics publishes (<see cref="IHullTrimSource"/>) over the game clock and folds it
    /// into the pitch it hands the renderer, so the deck rider and the wake, which read that pitch
    /// back, cannot disagree with the picture.
    ///
    /// <para>Headless and deterministic (rule 5): scripted clock, recording renderer, a stub trim
    /// source, the real <see cref="MeshHullDriver.Drive"/> body. Every expected angle and offset is
    /// worked by hand in its test — the lag from <c>1 − e^(−dt/τ)</c>, the wake and the deck from the
    /// rig's own rotation — never read back from the code.</para>
    /// </summary>
    public class HullTrimDriverTests
    {
        const float Tol = 1e-4f;

        // The shape every rig here is baked at: the fleet's 40° camera, 32 px a metre, her design
        // waterline 0.3 m up the planking and her transom 2 m aft of the origin.
        const float RigElevationDegrees = 40f;
        const float WaterlineMeters = 0.3f;
        const float SternOffsetMeters = 2f;

        static object _sink;

        readonly List<Object> _spawned = new();
        ScriptedClock _clock;
        GameConfig _config;

        // ------------------------------------------------------------------ doubles

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
            public int WorldSeed => 0;
            public TideProfile ActiveTideProfile { get; set; }
            public EnvironmentSample Sample() => new EnvironmentSample(
                Vector2.zero, Vector2.zero, tideHeight: 0f, SeaState.Calm, visibility: 1f, seaState01: 0f);
            public float TideHeightAt(double totalSeconds) => 0f;
            public float WaterLevelAt(double totalSeconds) => 0f;
        }

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

        // Stands where her BoatController stands: on her root, found by the driver at Configure.
        private sealed class StubTrimSource : MonoBehaviour, IHullTrimSource
        {
            public float Target, Response;
            public int Serial;
            public float TrimTargetDegrees => Target;
            public float TrimResponseSeconds => Response;
            public int TrimRestSerial => Serial;
        }

        // What the skinner installs her picture through, handing back one recording renderer.
        sealed class OneRendererService : IHullMeshPresentationService
        {
            readonly IHullMeshRenderer _renderer;
            public OneRendererService(IHullMeshRenderer renderer) => _renderer = renderer;
            public IHullMeshRenderer Install(GameObject host, HullMeshDef def, HullPaintSchemeDef scheme = null)
                => _renderer;
            public IHullPropRenderer AttachProp(GameObject host, HullPropMeshDef def, string slot) => null;
            public void DetachProps(GameObject host) { }
            public void DetachProp(GameObject host, string slot) { }
            public void Remove(GameObject host) { }
        }

        sealed class DrawnHull
        {
            public GameObject Root;
            public Transform Visual;
            public HullMeshDef Def;
            public RecordingRenderer Renderer;
            public MeshHullDriver Driver;
            public StubTrimSource Source;   // null: nothing on her root publishes a trim
        }

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();
            _clock = new ScriptedClock();
            GameServices.Clock = _clock;
            GameServices.Environment = new ScriptedSea();
            _config = ScriptableObject.CreateInstance<GameConfig>();
            _spawned.Add(_config);
            _config.HullTrim = new HullTrimSettings
            {
                Enabled = true, HumpFroude = 0.4f, PlaningFroude = 0.55f,
                DefaultMaxBowUpDegrees = 8f, DefaultMaxBowDownDegrees = 4f, DefaultResponseSeconds = 0.9f,
            };
            GameServices.Config = _config;
        }

        [TearDown]
        public void TearDown()
        {
            GameServices.Reset();
            foreach (var o in _spawned)
                if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
        }

        HullMeshDef Def(float rockRoll = 0f, float rockPitch = 0f, float rockHeave = 0f)
        {
            var def = ScriptableObject.CreateInstance<HullMeshDef>();
            _spawned.Add(def);
            def.ElevationDeg = RigElevationDegrees;
            def.AzimuthCounterClockwise = true;
            def.PxPerMetre = 32;
            def.RestingDraftMeters = WaterlineMeters;
            def.WakeSternOffsetMeters = SternOffsetMeters;
            def.RockRollDegrees = rockRoll;
            def.RockPitchDegrees = rockPitch;
            def.RockHeavePixels = rockHeave;
            return def;
        }

        DrawnHull Rig(bool withSource, Vector2 bow, Vector2 position,
                      float rockRoll = 0f, float rockPitch = 0f, float rockHeave = 0f)
        {
            var root = new GameObject("TrimDrawnHull");
            _spawned.Add(root);
            root.transform.position = position;
            root.transform.up = new Vector3(bow.x, bow.y, 0f).normalized;
            var visual = new GameObject("Visual").transform;
            visual.SetParent(root.transform, false);
            var hull = new DrawnHull
            {
                Root = root, Visual = visual, Def = Def(rockRoll, rockPitch, rockHeave),
                Renderer = new RecordingRenderer(),
            };
            // Before Configure: that is where the driver finds her source.
            if (withSource) hull.Source = root.AddComponent<StubTrimSource>();
            hull.Driver = root.AddComponent<MeshHullDriver>();
            hull.Driver.Configure(visual, hull.Renderer, hull.Def, zeroHeadingDegrees: 0f);
            return hull;
        }

        DrawnHull Trimming(float target, float responseSeconds, Vector2 bow, Vector2 position)
        {
            var hull = Rig(withSource: true, bow, position);
            hull.Source.Target = target;
            hull.Source.Response = responseSeconds;
            return hull;
        }

        // ------------------------------------------------------------------ the ease

        [Test]
        public void SheStartsLevel_ThenEasesToHerTarget_OnHerOwnLag()
        {
            var hull = Trimming(3f, 0.5f, Vector2.up, Vector2.zero);

            hull.Driver.Drive();   // the first drive after Configure is level, and starts her clock
            Assert.AreEqual(0f, hull.Driver.TrimDegrees, 0f, "the first drive draws her level");
            Assert.AreEqual(0f, hull.Renderer.PitchDegrees, 0f);

            // 3° asked for, a 0.5 s lag, 0.1 s a frame: each frame closes 1 − e^(−0.2) of the gap, so
            // after n frames she stands at 3·(1 − e^(−0.2·n)).
            float[] expected = { 0.5438077f, 0.9890399f, 1.3535651f, 1.6520131f, 1.8963617f };
            for (int n = 0; n < expected.Length; n++)
            {
                _clock.Advance(0.1);
                hull.Driver.Drive();
                Assert.AreEqual(expected[n], hull.Driver.TrimDegrees, Tol, $"frame {n + 1}");
                // One pitch, drawn once: with no rock and no storm the trim IS the pitch the renderer
                // got, and the pitch the deck and the wake read back.
                Assert.AreEqual(hull.Driver.TrimDegrees, hull.Driver.AppliedPitchDegrees, 0f);
                Assert.AreEqual(hull.Driver.AppliedPitchDegrees, hull.Renderer.PitchDegrees, 0f);
            }

            // Never faked by moving the picture: the visual child stays at screen identity and her
            // body where it was put.
            Assert.Less(Quaternion.Angle(Quaternion.identity, hull.Visual.rotation), 1e-3f);
            Assert.AreEqual(Vector3.zero, hull.Visual.localPosition);
            Assert.AreEqual(Vector3.zero, hull.Root.transform.position);
        }

        [Test]
        public void ARestFromHerSource_PutsHerLevelAtOnce_AndSheEasesFromThere()
        {
            var hull = Trimming(3f, 0.5f, Vector2.up, Vector2.zero);
            hull.Driver.Drive();
            for (int n = 0; n < 3; n++) { _clock.Advance(0.1); hull.Driver.Drive(); }
            Assert.AreEqual(1.3535651f, hull.Driver.TrimDegrees, Tol, "harness: three frames in, 3·(1 − e^(−0.6))");

            hull.Source.Serial++;   // a stop, a teleport, a hull swap, a load
            _clock.Advance(0.1);
            hull.Driver.Drive();
            Assert.AreEqual(0f, hull.Driver.TrimDegrees, 0f, "a rest is honoured at once, never eased");
            Assert.AreEqual(0f, hull.Renderer.PitchDegrees, 0f);

            _clock.Advance(0.1);
            hull.Driver.Drive();
            Assert.AreEqual(0.5438077f, hull.Driver.TrimDegrees, Tol, "…and she eases up again from level");
        }

        [Test]
        public void AReskin_PutsHerLevelAtOnce()
        {
            var hull = Trimming(3f, 0.5f, Vector2.up, Vector2.zero);
            hull.Driver.Drive();
            for (int n = 0; n < 3; n++) { _clock.Advance(0.1); hull.Driver.Drive(); }
            Assert.AreEqual(1.3535651f, hull.Driver.TrimDegrees, Tol, "harness: three frames in");

            // The skinner's apply (a hull swap) re-configures the driver; she starts level on it.
            hull.Driver.Configure(hull.Visual, hull.Renderer, hull.Def, zeroHeadingDegrees: 0f);
            Assert.AreEqual(0f, hull.Driver.TrimDegrees, 0f, "level from the moment she is re-skinned");
            _clock.Advance(0.1);
            hull.Driver.Drive();
            Assert.AreEqual(0f, hull.Driver.TrimDegrees, 0f, "the first drive after it is level too");

            _clock.Advance(0.1);
            hull.Driver.Drive();
            Assert.AreEqual(0.5438077f, hull.Driver.TrimDegrees, Tol, "…and she eases up from there");
        }

        [Test]
        public void WithNoTrim_SheDrawsThePreTrimPose_BitForBit()
        {
            // Rock, storm and all. A hull with nothing publishing a trim, and one whose source asks
            // for level, hand the renderer the same floats: the trim is composed onto the pitch only
            // when it is not zero, so a boat that is not trimming draws exactly what she drew before.
            var bow = new Vector2(0.6f, 0.8f);
            var bare = Rig(withSource: false, bow, Vector2.zero, rockRoll: 2.8f, rockPitch: 1.6f, rockHeave: 1.5f);
            var level = Rig(withSource: true, bow, Vector2.zero, rockRoll: 2.8f, rockPitch: 1.6f, rockHeave: 1.5f);
            level.Source.Target = 0f;
            level.Source.Response = 0.5f;

            for (int f = 0; f < 12; f++)
            {
                _clock.Advance(1.0 / 60.0);
                foreach (var h in new[] { bare, level })
                {
                    h.Driver.SetRockPhaseDegrees(37f * f);
                    h.Driver.SetStormRock(1.3f, 0.4f, 0.7f);
                    h.Driver.Drive();
                }
                Assert.AreEqual(0f, level.Driver.TrimDegrees, 0f, $"frame {f}: a level target is no trim");
                Assert.AreEqual(bare.Renderer.PitchDegrees, level.Renderer.PitchDegrees, 0f, $"frame {f}: pitch");
                Assert.AreEqual(bare.Renderer.RollDegrees, level.Renderer.RollDegrees, 0f, $"frame {f}: roll");
                Assert.AreEqual(bare.Renderer.HeavePixels, level.Renderer.HeavePixels, 0f, $"frame {f}: heave");
                Assert.AreEqual(bare.Renderer.HeadingDirUnits, level.Renderer.HeadingDirUnits, 0f, $"frame {f}: heading");
            }
            Assert.AreNotEqual(0f, bare.Renderer.PitchDegrees, "harness: the pose compared is not trivially level");
        }

        // ------------------------------------------------------------------ the clock

        [Test]
        public void APausedClock_HoldsHerBow_AndAClockSteppedBack_HoldsForOneDrive()
        {
            var hull = Trimming(3f, 0.5f, Vector2.up, Vector2.zero);
            hull.Driver.Drive();
            _clock.Advance(0.1);
            hull.Driver.Drive();
            float held = hull.Driver.TrimDegrees;
            Assert.AreEqual(0.5438077f, held, Tol, "harness: one frame in");

            hull.Driver.Drive();
            hull.Driver.Drive();   // paused: the game clock does not move, and neither does her bow
            Assert.AreEqual(held, hull.Driver.TrimDegrees, 0f, "a paused clock is no time: her bow holds");

            _clock.SeekTo(_clock.TotalSeconds - 5.0);   // a clock stepped backwards
            hull.Driver.Drive();
            Assert.AreEqual(held, hull.Driver.TrimDegrees, 0f,
                "a backwards step holds her; the lag never runs in reverse");

            _clock.Advance(0.1);
            hull.Driver.Drive();
            // One ordinary frame on from the new time: 3 − (3 − 0.5438)·e^(−0.2) = 3·(1 − e^(−0.4)).
            Assert.AreEqual(0.9890399f, hull.Driver.TrimDegrees, Tol, "…and she eases on from the new time");
        }

        [Test]
        public void TwoHalfFrames_EaseHerAsFarAsOneWholeFrame()
        {
            var whole = Trimming(3f, 0.5f, Vector2.up, Vector2.zero);
            var halves = Trimming(3f, 0.5f, Vector2.up, new Vector2(10f, 0f));
            whole.Driver.Drive();
            halves.Driver.Drive();

            _clock.Advance(0.05);
            halves.Driver.Drive();
            _clock.Advance(0.05);
            halves.Driver.Drive();
            whole.Driver.Drive();

            Assert.AreEqual(0.5438077f, whole.Driver.TrimDegrees, Tol, "harness: 3·(1 − e^(−0.2))");
            // e^(−0.05/0.5)·e^(−0.05/0.5) = e^(−0.1/0.5): the lag is a law of time, not of frames.
            Assert.AreEqual(whole.Driver.TrimDegrees, halves.Driver.TrimDegrees, 1e-5f,
                "the frame rate cannot change where her bow is");
        }

        // ------------------------------------------------------------------ what reads the picture

        // Three hulls on one spot and one heading: trimmed 2.5° bow up (a zero lag follows the target
        // exactly), pitched 2.5° through the storm channel (the pitch the wake and the deck already
        // honour), and level.
        void ThreeHulls(out DrawnHull trimmed, out DrawnHull stormPitched, out DrawnHull level)
        {
            var bow = new Vector2(0.6f, 0.8f);
            var at = new Vector2(3f, -2f);
            trimmed = Trimming(2.5f, 0f, bow, at);
            stormPitched = Rig(withSource: false, bow, at);
            stormPitched.Driver.SetStormRock(1f, 0f, 2.5f);
            level = Rig(withSource: false, bow, at);
            for (int pass = 0; pass < 2; pass++)
            {
                if (pass > 0) _clock.Advance(0.1);
                trimmed.Driver.Drive();
                stormPitched.Driver.Drive();
                level.Driver.Drive();
            }
            Assert.AreEqual(2.5f, trimmed.Driver.TrimDegrees, 0f, "harness: a zero lag follows the target exactly");
            Assert.AreEqual(2.5f, trimmed.Driver.AppliedPitchDegrees, 0f, "harness: 2.5° of trim drawn");
            Assert.AreEqual(2.5f, stormPitched.Driver.AppliedPitchDegrees, 0f, "harness: 2.5° of storm pitch drawn");
            Assert.AreEqual(0f, level.Driver.AppliedPitchDegrees, 0f, "harness: level");
        }

        [Test]
        public void HerWake_IsBornAtTheTransomSheIsDrawnWith()
        {
            ThreeHulls(out var trimmed, out var stormPitched, out var level);

            Assert.IsTrue(trimmed.Driver.TryGetWakePose(out HullWakePose t));
            Assert.IsTrue(stormPitched.Driver.TryGetWakePose(out HullWakePose s));
            Assert.IsTrue(level.Driver.TryGetWakePose(out HullWakePose l));

            // The wake reads the one attitude the drawer drew: 2.5° of trim puts her transom exactly
            // where 2.5° of any other pitch does…
            Assert.AreEqual(s.DrawnStern, t.DrawnStern, "the wake is born at the drawn transom");
            Assert.AreEqual(s.Heading, t.Heading);
            Assert.AreEqual(s.TideRise, t.TideRise);

            // …and it is not where it was: bow up is transom down. Her transom (2 m aft, 0.3 m up)
            // turns through 2.5° to 8.75 cm lower and 1.1 cm further aft, which the 40° camera draws
            // between 6.0 and 7.4 cm lower on screen, whatever her heading.
            Assert.That(t.DrawnStern.y - l.DrawnStern.y, Is.InRange(-0.0745f, -0.0595f),
                "her wake is born where her squatting transom is drawn");
        }

        [Test]
        public void HerDeckRider_StandsOnTheDeckSheIsDrawnWith()
        {
            ThreeHulls(out var trimmed, out var stormPitched, out var level);
            IBoatHullPresenter pt = new MeshHullPresenter(trimmed.Driver);
            IBoatHullPresenter ps = new MeshHullPresenter(stormPitched.Driver);
            IBoatHullPresenter pl = new MeshHullPresenter(level.Driver);

            Assert.AreEqual(trimmed.Renderer.PitchDegrees, pt.AppliedPitchDegrees, 0f,
                "the rider reads the pitch the picture got");

            // Somebody on her foredeck, 1.5 m forward of her origin and 0.5 m up, mirrored the way
            // DeckRiderVisual mirrors any mesh hull.
            var foredeck = new Vector3(0f, 1.5f, 0.5f);
            DeckRidePose rt = Mirror(pt, foredeck), rs = Mirror(ps, foredeck), rl = Mirror(pl, foredeck);
            Assert.AreEqual(rs.LiftMeters, rt.LiftMeters, 0f, "trim moves the rider as any pitch does");
            Assert.AreEqual(rs.SwayMeters, rt.SwayMeters, 0f);
            Assert.AreEqual(rs.RollDegrees, rt.RollDegrees, 0f);
            Assert.AreEqual(0f, rl.LiftMeters, 0f, "harness: level is no lift");

            // Bow up lifts her: the foredeck point turns through 2.5° to 6.5 cm higher and 2.3 cm aft,
            // which the 40° camera draws between 3.5 and 6.5 cm up the screen, whatever her heading.
            Assert.That(rt.LiftMeters - rl.LiftMeters, Is.InRange(0.0345f, 0.0650f),
                "the rider rises with her bow");
        }

        static DeckRidePose Mirror(IBoatHullPresenter hull, Vector3 deckPoint) =>
            MountedRockPoseMath.MirrorHull(deckPoint, hull.DrawnHeadingDegrees(), hull.AppliedRollDegrees,
                                           hull.AppliedPitchDegrees, hull.AppliedHeaveMeters - hull.DrawnRideMeters,
                                           hull.BakeElevationDegrees, 1f);

        sealed class GearedHull
        {
            public GameObject Root;
            public Transform Visual;
            public MeshHullDriver Driver;
            public SternDeckGearPresenter Deck;
        }

        // A hull as BoatHullSkinner leaves her, a hauler bolted at her transom (2 m aft, 0.3 m up: the
        // very point her wake is born at) and her stern gear on the same root. The gear finds her the
        // way it does in play, through the presenter host the skinner writes.
        GearedHull Geared(Vector2 bow, Vector2 position, float trim,
                          float rockRoll = 0f, float rockPitch = 0f, float rockHeave = 0f)
        {
            var def = Def(rockRoll, rockPitch, rockHeave);
            def.Mesh = new Mesh();
            _spawned.Add(def.Mesh);
            def.Ramps = new[] { new HullMeshDef.Ramp { Colors = new[] { new Color32(1, 2, 3, 255) } } };
            def.Bayer16 = new float[16];
            def.CellW = 456; def.CellH = 420;
            var visual = ScriptableObject.CreateInstance<BoatVisualDef>();
            _spawned.Add(visual);
            visual.Id = "visual.trim_gear_test";
            visual.Variant = BoatHullVariant.Mesh;
            visual.HullMesh = def;

            var root = new GameObject("TrimGearedHull");
            _spawned.Add(root);
            root.transform.position = position;
            root.transform.up = new Vector3(bow.x, bow.y, 0f).normalized;
            var source = root.AddComponent<StubTrimSource>();   // before the skin: Configure finds it there
            source.Target = trim;
            source.Response = 0f;                                // a zero lag follows the target exactly

            BoatHullSkinner.Rig rig;
            IHullMeshPresentationService previous = HullMeshPresentation.Service;
            try
            {
                HullMeshPresentation.Service = new OneRendererService(new RecordingRenderer());
                rig = BoatHullSkinner.Apply(root, visual, boat: null,
                    new BoatHullSkinner.Options { SkipWaveMotion = true, SkipOars = true });
            }
            finally { HullMeshPresentation.Service = previous; }
            Assert.IsTrue(rig.Skinned, "harness: she is skinned as a mesh");

            var gear = ScriptableObject.CreateInstance<BoatDeckGearDef>();
            _spawned.Add(gear);
            gear.Id = "deckgear.trim_test";
            gear.Mounts = new[]
            {
                new BoatDeckGearMount { Kind = DeckGearKind.HaulerStation,
                                        MountLocalMeters = new Vector3(0f, -SternOffsetMeters, WaterlineMeters),
                                        Dir = 2 },
            };
            var kit = ScriptableObject.CreateInstance<DeckGearKitDef>();
            _spawned.Add(kit);
            kit.Id = "deckgearkit.trim_test";
            kit.FacingCount = 8;
            // No art: the stand-in draws at exactly the measured point, which is all this reads.
            kit.Entries = new[] { new DeckGearKitEntry { Kind = DeckGearKind.HaulerStation,
                                                         Facings = System.Array.Empty<Sprite>() } };
            var deck = root.AddComponent<SternDeckGearPresenter>();
            deck.Configure(gear, kit, null);

            return new GearedHull
            {
                Root = root, Visual = rig.Visual, Driver = root.GetComponent<MeshHullDriver>(), Deck = deck,
            };
        }

        // Two drives a frame apart (the first after a skin is level and starts her clock; a zero lag
        // then follows the target exactly), then her gear, which in play draws after her in the frame
        // (its LateUpdate at order 100, her driver at −110).
        Transform DrawTheHauler(GearedHull hull)
        {
            hull.Driver.Drive();
            _clock.Advance(0.1);
            hull.Driver.Drive();
            hull.Deck.Draw();
            Assert.AreEqual(1, hull.Deck.DrawnCount, "harness: her one mount drew");
            Transform piece = hull.Root.transform.Find("SternDeckGear/DeckGear_0");
            Assert.IsNotNull(piece, "harness: the hauler's renderer");
            return piece;
        }

        [Test]
        public void HerSternGear_StandsWhereSheIsDrawn_NotWhereSheWouldBeLevel()
        {
            // Bow north, so every term works by hand. At y −2.1 so that the drawn and the level points
            // fall in different sort orders; at −2 they share one and the order check could not tell.
            var hull = Geared(Vector2.up, new Vector2(3f, -2.1f), trim: 3.5f);
            // The wave's bob moves the PICTURE, not the boat: BoatWaveMotion writes exactly this child.
            hull.Visual.localPosition = new Vector3(0f, 0.21f, 0f);
            Transform piece = DrawTheHauler(hull);
            Assert.AreEqual(3.5f, hull.Driver.AppliedPitchDegrees, 0f, "harness: 3.5° of trim drawn");

            // Her transom (2 m aft, 0.3 m up) turns through 3.5° of bow-up to 2.01458 m aft and
            // 0.17734 m up, which the 40° camera draws 1.15910 m below her picture's origin (level, it
            // drew 1.05576 m below). The picture is bobbed 0.21 m up off her root. So the hauler stands
            // at −2.1 + 0.21 − 1.15910 = −3.04910, where the level placement stood it at
            // −2.1 − 1.05576 = −3.15576: 10.67 cm, 5 px at the default helm camera.
            Assert.AreEqual(3f, piece.position.x, Tol, "bow north: on her centreline");
            Assert.AreEqual(-3.04910f, piece.position.y, Tol, "the hauler stands on the transom she is drawn with");
            Assert.AreEqual(0f, piece.position.z, 0f, "on her root's plane");

            // …and SORTS where the level placement did, so a heave or a trim never re-sorts the gear
            // against the figures working among it. The decor band's rule, at each point:
            int levelOrder = Mathf.RoundToInt(SortingBands.DecorBase + 3.15576f * SortingBands.OrdersPerMetre);
            int drawnOrder = Mathf.RoundToInt(SortingBands.DecorBase + 3.04910f * SortingBands.OrdersPerMetre);
            Assert.AreNotEqual(levelOrder, drawnOrder, "harness: the two points sort apart");
            Assert.AreEqual(levelOrder, piece.GetComponent<SpriteRenderer>().sortingOrder,
                "the gear keeps the level point's order");
        }

        [Test]
        public void HerSternGear_IsBoltedToTheTransomHerWakeIsBornAt_ThroughTrimRockAndHeave()
        {
            // Everything the drawn hull has and a level placement lacks, at once: her hump trim, a
            // rolling, pitching, heaving sea, an oblique heading, and the picture bobbed off her root.
            var hull = Geared(new Vector2(0.6f, 0.8f), new Vector2(3f, -2f), trim: 3.5f,
                              rockRoll: 2.8f, rockPitch: 1.6f, rockHeave: 1.2f);
            hull.Visual.localPosition = new Vector3(0f, 0.21f, 0f);
            hull.Driver.SetRockPhaseDegrees(60f);
            Transform piece = DrawTheHauler(hull);

            // 60° into the rig's cycle she rolls 2.8·sin 60° = 2.425°, pitches 1.6·cos 60° = 0.8° over
            // her 3.5° of trim, and heaves 1.2·sin 60° = 1.04 px.
            Assert.AreEqual(2.4249f, hull.Driver.AppliedRollDegrees, 1e-3f, "harness: rolled");
            Assert.AreEqual(4.3f, hull.Driver.AppliedPitchDegrees, 1e-3f, "harness: trimmed and pitched");
            Assert.That(hull.Driver.AppliedHeaveMeters, Is.Not.EqualTo(0f), "harness: heaved");

            // The hauler stands on the transom the driver draws her wake from: the one drawn attitude,
            // read twice.
            Assert.IsTrue(hull.Driver.TryGetWakePose(out HullWakePose wake));
            Assert.AreEqual(wake.DrawnStern.x, piece.position.x, Tol, "bolted to her drawn transom (x)");
            Assert.AreEqual(wake.DrawnStern.y, piece.position.y, Tol, "bolted to her drawn transom (y)");
        }

        // ------------------------------------------------------------------ at the helm, end to end

        [Test]
        public void AtTheHelm_HerDrawnBowFollowsHerPhysics_AndAStopOrASwapLevelsIt()
        {
            // Her BoatController and her drawer on one root, as the skinner leaves them. The hull is
            // the controller tests' 4.5 m outboard boat: body mass 10, 12 of thrust after the 0.01 feel
            // scale, a squat of 1.5° per m/s² and her own 0.4 s lag (the policy's is 0.9).
            var root = new GameObject("TrimHelmBoat");
            _spawned.Add(root);
            var boat = root.AddComponent<BoatController>();
            boat.enabled = false;   // the helm is left, so TickUnmannedDrift runs her force pass
            var rb = root.GetComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.linearDamping = BoatController.HullLinearDamping;   // what Awake sets; EditMode runs none
            var hullDef = ScriptableObject.CreateInstance<BoatHullDef>();
            _spawned.Add(hullDef);
            hullDef.Id = "boat.trim_test";
            hullDef.Propulsion = PropulsionType.Engine;
            hullDef.EnginePower = 1200f; hullDef.MassKg = 1000f;
            hullDef.ForwardDrag = 100f; hullDef.LateralDrag = 240f;
            hullDef.LengthMeters = 4.5f; hullDef.DraughtMeters = 0.3f;
            hullDef.TrimHumpDegrees = 1f; hullDef.TrimPlaningDropDegrees = 0f;
            hullDef.TrimAccelDegreesPerMps2 = 1.5f; hullDef.TrimDecelDegreesPerMps2 = 1.5f;
            hullDef.TrimMaxBowUpDegrees = 0f; hullDef.TrimMaxBowDownDegrees = 0f;
            hullDef.TrimResponseSeconds = 0.4f;
            boat.SetHull(hullDef);
            Assert.AreEqual(10f, rb.mass, Tol, "precondition: 1000 kg → body mass 10");

            var visual = new GameObject("Visual").transform;
            visual.SetParent(root.transform, false);
            var renderer = new RecordingRenderer();
            var driver = root.AddComponent<MeshHullDriver>();
            driver.Configure(visual, renderer, Def(), zeroHeadingDegrees: 0f);

            // Full ahead from rest: 12 of thrust on a body of 10 is 1.2 m/s², no way on yet, so the
            // squat alone: 1.5 × 1.2 = 1.8°.
            boat.SetControl(1f, 0f);
            rb.linearVelocity = Vector2.zero;
            boat.TickUnmannedDrift();
            Assert.AreEqual(1.8f, boat.TrimTargetDegrees, Tol, "harness: her physics asks for 1.8°");

            driver.Drive();
            Assert.AreEqual(0f, driver.TrimDegrees, 0f, "the first drive is level");
            _clock.Advance(0.1);
            driver.Drive();
            // Her own 0.4 s lag, not the policy's: 1.8·(1 − e^(−0.1/0.4)).
            Assert.AreEqual(0.3981586f, driver.TrimDegrees, Tol, "the drawer eases after her physics, on her lag");
            Assert.AreEqual(driver.TrimDegrees, renderer.PitchDegrees, 0f, "…and draws exactly that");

            boat.Stop();   // a stop or a teleport
            _clock.Advance(0.1);
            driver.Drive();
            Assert.AreEqual(0f, driver.TrimDegrees, 0f, "a stop levels her at once");

            boat.SetControl(1f, 0f);
            rb.linearVelocity = Vector2.zero;
            boat.TickUnmannedDrift();
            _clock.Advance(0.1);
            driver.Drive();
            Assert.AreEqual(0.3981586f, driver.TrimDegrees, Tol, "…and she trims again from level");

            boat.SetHull(hullDef);   // a swap, or a load
            _clock.Advance(0.1);
            driver.Drive();
            Assert.AreEqual(0f, driver.TrimDegrees, 0f, "a hull swap levels her at once");
        }

        [Test]
        public void TheHelmLetGoUnderWay_SheEasesLevel_OnHerOwnLag()
        {
            // The same boat as above, her controller and her drawer on one root: body mass 10, 12 of
            // thrust, a squat of 1.5° per m/s² and her own 0.4 s lag (the policy's is 0.9).
            var root = new GameObject("TrimHelmBoat");
            _spawned.Add(root);
            var boat = root.AddComponent<BoatController>();
            boat.enabled = false;   // the helm is left, so TickUnmannedDrift runs her force pass
            var rb = root.GetComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.linearDamping = BoatController.HullLinearDamping;   // what Awake sets; EditMode runs none
            var hullDef = ScriptableObject.CreateInstance<BoatHullDef>();
            _spawned.Add(hullDef);
            hullDef.Id = "boat.trim_test";
            hullDef.Propulsion = PropulsionType.Engine;
            hullDef.EnginePower = 1200f; hullDef.MassKg = 1000f;
            hullDef.ForwardDrag = 100f; hullDef.LateralDrag = 240f;
            hullDef.LengthMeters = 4.5f; hullDef.DraughtMeters = 0.3f;
            hullDef.TrimHumpDegrees = 1f; hullDef.TrimPlaningDropDegrees = 0f;
            hullDef.TrimAccelDegreesPerMps2 = 1.5f; hullDef.TrimDecelDegreesPerMps2 = 1.5f;
            hullDef.TrimMaxBowUpDegrees = 0f; hullDef.TrimMaxBowDownDegrees = 0f;
            hullDef.TrimResponseSeconds = 0.4f;
            boat.SetHull(hullDef);
            Assert.AreEqual(10f, rb.mass, Tol, "precondition: 1000 kg → body mass 10");

            var visual = new GameObject("Visual").transform;
            visual.SetParent(root.transform, false);
            var renderer = new RecordingRenderer();
            var driver = root.AddComponent<MeshHullDriver>();
            driver.Configure(visual, renderer, Def(), zeroHeadingDegrees: 0f);

            // Full ahead from rest asks for 1.8° (1.5 × 1.2 m/s²); half a second on her 0.4 s lag
            // draws 1.8·(1 − e^(−1.25)) = 1.2842914°.
            boat.SetControl(1f, 0f);
            rb.linearVelocity = Vector2.zero;
            boat.TickUnmannedDrift();
            Assert.AreEqual(1.8f, boat.TrimTargetDegrees, Tol, "harness: her physics asks for 1.8°");
            driver.Drive();   // the first drive is level
            for (int n = 0; n < 5; n++) { _clock.Advance(0.1); driver.Drive(); }
            Assert.AreEqual(1.2842914f, driver.TrimDegrees, Tol, "harness: she is carrying her bow");

            boat.Stop(levelAtOnce: false);   // ControlSwitcher.LeaveHelm
            boat.TickUnmannedDrift();        // the deck's drift tick runs on
            Assert.AreEqual(0f, boat.TrimTargetDegrees, Tol, "harness: let go, she asks for level");
            _clock.Advance(0.1);
            driver.Drive();
            // Eased, not snapped: a tenth of a second on her own lag leaves 1.2842914·e^(−0.25). The
            // policy's 0.9 s would leave 1.1492343; a snap would leave 0.
            Assert.AreEqual(1.0002071f, driver.TrimDegrees, Tol, "her bow settles on her own lag");
            Assert.AreEqual(driver.TrimDegrees, renderer.PitchDegrees, 0f, "…and draws exactly that");

            float last = driver.TrimDegrees;
            for (int frame = 2; frame <= 40; frame++)
            {
                boat.TickUnmannedDrift();
                _clock.Advance(0.1);
                driver.Drive();
                Assert.Less(driver.TrimDegrees, last, $"frame {frame}: she only ever settles toward level");
                Assert.Greater(driver.TrimDegrees, 0f, $"frame {frame}: …from above, never through it");
                Assert.AreEqual(driver.TrimDegrees, renderer.PitchDegrees, 0f, $"frame {frame}: drawn as eased");
                last = driver.TrimDegrees;
            }
            Assert.Less(driver.TrimDegrees, 1e-3f, "ten of her lags on (4 s), she is level to the eye");
        }

        // ------------------------------------------------------------------ rule 7

        [Test]
        public void HerDrawnTrim_AllocatesNothing_PerFrame()
        {
            var hull = Rig(withSource: true, new Vector2(0.6f, 0.8f), Vector2.zero,
                           rockRoll: 2.8f, rockPitch: 1.6f, rockHeave: 1.5f);
            hull.Source.Target = 3f;
            hull.Source.Response = 0.5f;
            hull.Driver.SetRockPhaseDegrees(40f);
            hull.Driver.SetStormRock(1.3f, 0.4f, 0.7f);
            MeshHullDriver driver = hull.Driver;
            ScriptedClock clock = _clock;
            // A TestDelegate, not an Action: the constraint throws on any other delegate type.
            TestDelegate frame = () => { clock.Advance(1.0 / 60.0); driver.Drive(); };
            frame();
            frame();   // warm-up: JIT, type initialisation and the first drive's level snap are not a frame
            Assert.That(() => { _sink = new object(); }, Is.AllocatingGCMemory(),
                "positive control: the recorder must see an allocation when there is one");
            Assert.That(frame, Is.Not.AllocatingGCMemory());
            Assert.Greater(driver.TrimDegrees, 0f, "harness: the measured frame really trimmed her");
        }
    }
}
