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
    /// <b>ADR 0023 phase 3 step 2 in the real player loop:</b> the committed lobster boat, skinned
    /// as a MESH hull through the real Art presentation service, visually rides the displaced sea
    /// while it is active — the heave the facet renderer is actually handed (its screen lift IS
    /// heave / PxPerMetre, and the calibrated waterline z carries the same value) follows the
    /// shared displaced-height rule, sits at the committed resting draft, and returns to the
    /// rock-only pose the moment the sea clears (the A/B contract).
    ///
    /// <para><b>Time discipline:</b> headless PlayMode frames are NOT wall time (a yielded frame
    /// is ~1 ms), so everything here drives the DETERMINISTIC clock: the scripted
    /// <see cref="IGameClock"/> is advanced explicitly per yielded frame, and the adjudicated
    /// reads happen on a FROZEN clock (dt 0 ⇒ the wave field holds still), so the ride under the
    /// hull is a constant the assertions can difference exactly.</para>
    ///
    /// <para>Headless-safe: no camera, nothing renders (CI's Null Device would crash on a render);
    /// the pixels are the EditMode GPU fixtures' business.</para>
    /// </summary>
    public class SharedHeavePlayTests
    {
        const string VisualPath = "Assets/_Project/Data/Boats/Visuals/LobsterBoatIso.asset";
        const float Dt = 1f / 60f;

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

        sealed class ScriptedSea : IEnvironmentService
        {
            public int WorldSeed => 0;
            public TideProfile ActiveTideProfile { get; set; }
            public EnvironmentSample Sample() => new EnvironmentSample(
                new Vector2(6f, 3f), Vector2.zero, tideHeight: 0f,
                HiddenHarbours.Core.SeaState.Moderate, visibility: 1f, seaState01: 0.75f);
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
        public IEnumerator MeshHull_VisualY_FollowsTheDisplacedHeight_WhileTheSeaIsOn()
        {
#if !UNITY_EDITOR
            Assert.Ignore("Needs the AssetDatabase: this asserts the REAL committed lobster.");
            yield break;
#else
            var visual = AssetDatabase.LoadAssetAtPath<BoatVisualDef>(VisualPath);
            Assert.IsNotNull(visual, $"missing {VisualPath}");
            Assert.IsNotNull(HullMeshPresentation.Service,
                "the Art presentation service must self-register at runtime load");

            float waterline = visual.HullMesh.RestingDraftMeters;
            int ppu = visual.HullMesh.PxPerMetre;
            float rockPx = visual.HullMesh.RockHeavePixels;
            Assert.Greater(waterline, 0f,
                "the committed lobster HullMeshDef must carry a design waterline — the step-2 data " +
                "decision (0.5 m, the spike's fixed-mid-draft framing). Zero means the data " +
                "edit was lost and she floats keel-on-the-surface again.");
            // What the driver actually SUBTRACTS: the datum through the iso projection
            // (HullSettleMath — owner playtest 2026-08-07), so the sea draws her waterline AT the
            // datum instead of 14.57 % deeper. Derived from the def's own bake elevation, never
            // re-typed here.
            float sink = HullSettleMath.AppliedSinkMeters(waterline, visual.HullMesh.ElevationDeg);

            var clock = new ScriptedClock();
            GameServices.Clock = clock;
            GameServices.Environment = new ScriptedSea();

            // Pin the ADR 0023 laws on the STORM-OFF side (ADR 0018 B2.5): at sea 0.75 the weight
            // filter would sit a gravity-capped spring between the surface and the ride, and this
            // fixture's frozen-clock differencing needs the ride surface-exact. B2.5's own negative
            // control guarantees OFF is byte-identical to pre-B2.5; the storm-ON laws live in
            // StormSeakeepingReadTests.
            _config = ScriptableObject.CreateInstance<GameConfig>();
            StormRockSettings storm = _config.StormRock;
            storm.Enabled = false;
            _config.StormRock = storm;
            GameServices.Config = _config;

            _root = new GameObject("LobsterBoat");
            var rig = BoatHullSkinner.Apply(_root, visual, boat: null);
            Assert.AreEqual(BoatHullVariant.Mesh, rig.Presenter.Variant);
            var renderer = rig.Visual.GetComponent<IsoFacetHullRenderer>();
            Assert.IsNotNull(renderer);

            // --- displaced OFF: today's pose, exactly (the A/B contract) --------------------
            for (int f = 0; f < 10; f++) { clock.Advance(Dt); yield return null; }
            Assert.LessOrEqual(Mathf.Abs(renderer.HeavePixels), rockPx + 1e-3f,
                "with no displaced sea the heave must be the rig's own rock alone — no draft, " +
                "no ride (the OFF side must stay byte-identical to before this step)");

            // --- displaced ON: build a swell, then adjudicate on a FROZEN clock -------------
            DisplacedSea.Publish(_seaOwner, new DisplacedSeaState(1.5f, 0.6f));
            for (int f = 0; f < 30; f++) { clock.Advance(Dt); yield return null; }

            // Find a frozen instant where the swell under the hull is meaningfully non-zero
            // (deterministic scan of the same clock the sim runs on — never wall time).
            float h1 = 0f, h0 = 0f;
            bool found = false;
            for (int attempt = 0; attempt < 240 && !found; attempt++)
            {
                clock.Advance(Dt);
                // ⚠️ TWO frames, not one — the settle fix (2026-08-07) put the wave field on a Core
                // seam, so the sea the hull rides is now published by WaveFieldBridge in UPDATE while
                // the hull poses in LateUpdate. A UnityTest coroutine resumes BETWEEN those two, so
                // this Advance lands after the bridge has already ticked: the bridge only catches up
                // on the NEXT frame's Update. One yield would leave h1 sitting on the pre-advance sea
                // while h0/h2 below sit on the post-advance one, and the linearity law would be
                // comparing two different seas (measured: 0.4–0.6 % — small, and exactly the size of
                // one frame of swell). The second yield lets the bridge publish the advanced field and
                // the hull pose on it, so all three reads below are the SAME frozen instant.
                yield return null;                               // the bridge catches up to the clock
                yield return null;                               // …and the hull poses on that field
                h1 = renderer.HeavePixels;

                DisplacedSea.Publish(_seaOwner, new DisplacedSeaState(0f, 0.6f));
                yield return null;                               // SAME instant, ride exactly 0
                h0 = renderer.HeavePixels;
                DisplacedSea.Publish(_seaOwner, new DisplacedSeaState(1.5f, 0.6f));

                found = Mathf.Abs(h1 - h0) > 4f;                 // > 4 px of genuine ride
            }
            Assert.IsTrue(found,
                "over 4 s of the reference sea the hull never rode more than 4 px above her " +
                "settle level — the displaced heave is not reaching the mesh renderer");

            // The E=0 read pins the SETTLE LEVEL: heave = rock − sink·ppu, and the rock term
            // is bounded by the rig's own amplitude.
            Assert.LessOrEqual(Mathf.Abs(h0 + sink * ppu), rockPx + 1e-2f,
                $"at exaggeration 0 the hull must sit at her settle level ({sink:0.###} m = " +
                $"{sink * ppu:0.#} px down, ± the {rockPx} px rock): got {h0} px");

            // …and the point of it: with the sea flat under her, the water the shared z-buffer
            // draws on her planking is her DATUM — not 1.1457 × it. This is the assertion the
            // pre-fix suite could not make, because it pinned the sink against itself.
            Assert.AreEqual(waterline,
                HullSettleMath.DrawnWaterlineMeters(0f, h0 / ppu, visual.HullMesh.ElevationDeg),
                rockPx / ppu + 1e-2f,
                "the committed lobster must DRAW her waterline at the number on her def " +
                "(owner playtest 2026-08-07: 'they should level out at the boats water line')");

            // The linearity law on the frozen sea: double the published exaggeration, the ride
            // doubles — the boat reads the surface's LIVE value every frame (the shared-constant
            // law; this is exactly the owner's in-Play tune reaching the hull).
            DisplacedSea.Publish(_seaOwner, new DisplacedSeaState(3f, 0.6f));
            yield return null;
            float h2 = renderer.HeavePixels;
            Assert.AreEqual((h1 - h0) * 2f, h2 - h0, 0.15f,
                "on a frozen sea, re-publishing 2× the exaggeration must exactly double the " +
                "ride (rock and draft cancel in the difference) — the hull is not reading the " +
                "shared displaced-height rule live");

            // --- displaced cleared: the pose comes straight back (the A/B contract) ---------
            DisplacedSea.Clear(_seaOwner);
            yield return null;
            Assert.LessOrEqual(Mathf.Abs(renderer.HeavePixels), rockPx + 1e-3f,
                "clearing the displaced sea must drop ride AND draft the very next frame — " +
                "the flat water's fleet pose is the byte-identity side of the A/B");
#endif
        }
    }
}
