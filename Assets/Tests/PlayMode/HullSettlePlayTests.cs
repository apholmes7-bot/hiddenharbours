using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.Art;
using HiddenHarbours.Boats;
using HiddenHarbours.Core;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// <b>THE DORY SETTLES AT HER WATERLINE, in the real player loop</b> — owner playtest
    /// 2026-08-07: <i>"generally they should level out at the boats water line."</i> The committed
    /// dory (the boat he actually starts the game in, and the hull the ADR 0023 notes single out for
    /// a look) is skinned as a real MESH hull through the real Art presentation service, and the
    /// waterline the shared z-buffer would draw on her planking is measured against the number on
    /// her def — at rest on a glass sea, and again across a moving one.
    ///
    /// <para><b>What is NOT under test:</b> the heave. She is meant to bounce, and B2.5's loft is
    /// meant to throw her clear of a rogue crest; the owner said so in the same sentence. Only the
    /// LEVEL is adjudicated here.</para>
    ///
    /// <para><b>Time discipline:</b> headless PlayMode frames are NOT wall time (a yielded frame is
    /// ~1 ms, and CI's frame budget is nobody's clock), so nothing here counts frames as seconds.
    /// The deterministic <see cref="IGameClock"/> is advanced EXPLICITLY per yielded frame, and the
    /// adjudicated read happens with the clock frozen (dt 0 ⇒ the field holds still), so the ride
    /// under the hull is a constant the assertion can compare exactly. See
    /// <c>SharedHeavePlayTests</c> for the same discipline on the lobster.</para>
    ///
    /// <para>Headless-safe: no camera, nothing renders (CI's Null Device would crash on a render) —
    /// the pixels are the EditMode GPU fixtures' business. What is read is the value the facet
    /// renderer is HANDED, which is the same number its screen lift and its calibrated waterline z
    /// are both built from.</para>
    /// </summary>
    public class HullSettlePlayTests
    {
        const string VisualPath = "Assets/_Project/Data/Boats/Visuals/DoryIso.asset";
        const float Dt = 1f / 60f;
        const float Exag = 1.6f;          // the shipped GameConfig.DisplacedWater value
        const float Band = 0.6f;

        readonly object _seaOwner = new object();
        GameObject _root;
        GameConfig _config;

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

        /// <summary>Sea state is settable so the same rig can be sailed on glass and on a swell.</summary>
        sealed class ScriptedSea : IEnvironmentService
        {
            public float SeaState01 = 0.75f;
            public int WorldSeed => 0;
            public TideProfile ActiveTideProfile { get; set; }
            public EnvironmentSample Sample() => new EnvironmentSample(
                new Vector2(6f, 3f), Vector2.zero, tideHeight: 0f,
                HiddenHarbours.Core.SeaState.Moderate, visibility: 1f, seaState01: SeaState01);
            public float TideHeightAt(double totalSeconds) => 0f;
            public float WaterLevelAt(double totalSeconds) => 0f;
        }

        [TearDown]
        public void TearDown()
        {
            DisplacedSea.Clear(_seaOwner);
            GameServices.Reset();
            if (_root != null) Object.DestroyImmediate(_root);
            _root = null;
            if (_config != null) Object.DestroyImmediate(_config);
            _config = null;
        }

        [UnityTest]
        public IEnumerator TheDory_AtRest_FloatsWithTheSeaAtHerDesignWaterline()
        {
#if !UNITY_EDITOR
            Assert.Ignore("Needs the AssetDatabase: this asserts the REAL committed dory.");
            yield break;
#else
            var visual = AssetDatabase.LoadAssetAtPath<BoatVisualDef>(VisualPath);
            Assert.IsNotNull(visual, $"missing {VisualPath}");
            Assert.IsNotNull(HullMeshPresentation.Service,
                "the Art presentation service must self-register at runtime load");
            Assert.IsTrue(visual.HasHullMesh(), "the dory is a mesh hull (ADR 0022 phase 6)");

            float waterline = visual.ResolveDesignWaterlineMeters();
            float elevation = visual.ResolveWaterlineElevationDegrees();
            int ppu = visual.HullMesh.PxPerMetre;
            float rockPx = visual.HullMesh.RockHeavePixels;
            Assert.Greater(waterline, 0f,
                "the committed dory must carry a design waterline, or she floats keel-on-the-surface");

            var clock = new ScriptedClock();
            var sea = new ScriptedSea { SeaState01 = 0f };      // GLASS — the field is exactly 0
            GameServices.Clock = clock;
            GameServices.Environment = sea;

            // Storm block OFF: B2.5's weight filter puts a gravity-capped spring between the surface
            // and the ride, and this fixture adjudicates the RESTING level. The storm's own
            // mean-level behaviour is measured in HullWaterlineSettleTests.
            _config = ScriptableObject.CreateInstance<GameConfig>();
            StormRockSettings storm = _config.StormRock;
            storm.Enabled = false;
            _config.StormRock = storm;
            GameServices.Config = _config;

            _root = new GameObject("Dory");
            BoatHullSkinner.Rig rig = BoatHullSkinner.Apply(_root, visual, boat: null);
            Assert.AreEqual(BoatHullVariant.Mesh, rig.Presenter.Variant);
            var renderer = rig.Visual.GetComponent<IsoFacetHullRenderer>();
            Assert.IsNotNull(renderer, "the mesh path must install the facet renderer");

            DisplacedSea.Publish(_seaOwner, new DisplacedSeaState(Exag, Band));

            // Let the chain settle. The CLOCK is what advances — the frame count is not time.
            for (int f = 0; f < 30; f++) { clock.Advance(Dt); yield return null; }

            // On glass the sea's lift under her is exactly 0, so the whole heave is her settle sink
            // and the waterline the shared z-buffer draws is the pure projection of it. (± the rig's
            // own canned rock heave, which is a hull animation, not a flotation level.)
            float rideMetres = renderer.HeavePixels / ppu;
            float drawn = HullSettleMath.DrawnWaterlineMeters(0f, rideMetres, elevation);
            float tolerance = rockPx / ppu * HullSettleMath.IsoWaterlineGain(elevation) + 5e-3f;

            Assert.AreEqual(waterline, drawn, tolerance,
                $"at rest the sea must stand at the dory's OWN datum ({waterline:0.###} m above her " +
                $"keel) on her planking — it stands at {drawn:0.###} m. Before the settle fix this " +
                $"read {waterline * HullSettleMath.IsoWaterlineGain(elevation):0.###} m: the sink " +
                "was applied raw, and the iso projection then drew 1.1457 rig-metres of planking " +
                "per metre of it (owner playtest 2026-08-07).");

            // …and the A/B contract still holds on the other side: clear the sea and she comes
            // straight back to the rock-only pose, no sink at all.
            DisplacedSea.Clear(_seaOwner);
            clock.Advance(Dt);
            yield return null;
            Assert.LessOrEqual(Mathf.Abs(renderer.HeavePixels), rockPx + 1e-3f,
                "clearing the displaced sea must drop the settle sink the very next frame — flat " +
                "water is the byte-identical side of the A/B");
#endif
        }

        [UnityTest]
        public IEnumerator TheDory_OnASwell_KeepsTheSeaAtHerWaterline_WhileSheHeaves()
        {
#if !UNITY_EDITOR
            Assert.Ignore("Needs the AssetDatabase: this asserts the REAL committed dory.");
            yield break;
#else
            var visual = AssetDatabase.LoadAssetAtPath<BoatVisualDef>(VisualPath);
            Assert.IsNotNull(visual, $"missing {VisualPath}");
            Assert.IsNotNull(HullMeshPresentation.Service);

            float waterline = visual.ResolveDesignWaterlineMeters();
            float elevation = visual.ResolveWaterlineElevationDegrees();
            int ppu = visual.HullMesh.PxPerMetre;
            float rockPx = visual.HullMesh.RockHeavePixels;

            var clock = new ScriptedClock();
            var sea = new ScriptedSea { SeaState01 = 0.75f };
            GameServices.Clock = clock;
            GameServices.Environment = sea;

            _config = ScriptableObject.CreateInstance<GameConfig>();
            StormRockSettings storm = _config.StormRock;
            storm.Enabled = false;
            _config.StormRock = storm;
            GameServices.Config = _config;

            _root = new GameObject("Dory");
            _root.transform.position = new Vector3(7.3f, -4.1f, 0f);   // off origin: exercise freqScale
            BoatHullSkinner.Rig rig = BoatHullSkinner.Apply(_root, visual, boat: null);
            var renderer = rig.Visual.GetComponent<IsoFacetHullRenderer>();
            Assert.IsNotNull(renderer);

            const float freqScale = 2.8f;                              // the owner's tuned materials
            DisplacedSea.Publish(_seaOwner, new DisplacedSeaState(Exag, Band, freqScale));

            var boatPos = new Vector2(_root.transform.position.x, _root.transform.position.y);
            float tolerance = rockPx / ppu * HullSettleMath.IsoWaterlineGain(elevation) + 2e-2f;
            float worstError = 0f;
            float biggestRide = 0f;
            int probes = 0;

            // Sail in bursts, then adjudicate on a FROZEN clock. The freeze is what makes the read
            // honest: a UnityTest coroutine resumes between Update and LateUpdate, so a running
            // clock would pair THIS frame's published field with LAST frame's pose. At dt 0 the
            // animator advances nothing — both the bridge's field and the hull's ride hold still —
            // and the pairing is exact. (Frames are not time here; the clock is. See the class doc.)
            for (int burst = 0; burst < 12; burst++)
            {
                for (int f = 0; f < 20; f++) { clock.Advance(Dt); yield return null; }   // 1/3 s of sail
                for (int f = 0; f < 3; f++) yield return null;                           // dt 0: settle

                // THE REAL BRIDGE is the publisher — this is the seam under test, not a stand-in.
                Assert.IsTrue(SharedWaveField.TryGet(out WaveTrains trains),
                    "WaveFieldBridge published nothing to the Core seam, so the hull fell back to " +
                    "her own animator and this measurement would be of the wrong sea entirely");

                // The lift the SHADER puts the water at under her: the published trains sampled the
                // way the displaced vertex stage does (position × freqScale, through the same fetch
                // envelope, × exaggeration; open water ⇒ shore fade exactly 1).
                float fetch = GameServices.FetchEnvelopeAt(boatPos);
                float lift = WaveMath.Sample(boatPos * freqScale, 0.0, in trains, fetch).Height * Exag;
                float ride = renderer.HeavePixels / ppu;

                worstError = Mathf.Max(worstError,
                    Mathf.Abs(HullSettleMath.DrawnWaterlineMeters(lift, ride, elevation) - waterline));
                biggestRide = Mathf.Max(biggestRide, Mathf.Abs(ride));
                probes++;
            }

            Assert.AreEqual(12, probes);
            Assert.Greater(biggestRide, 0.1f,
                "the dory never rode more than 10 cm across the whole sail — the sea is not " +
                "reaching her, so the waterline law below was measured on a boat that never moved");

            Assert.Less(worstError, tolerance,
                $"while she heaved by up to {biggestRide:0.00} m the sea wandered {worstError:0.000} m " +
                "off her design waterline. She is meant to bounce; she is not meant to stop " +
                "floating at her own waterline while she does it (owner playtest 2026-08-07).");
#endif
        }
    }
}
