using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Art;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// The drifting seaweed, driven one DETERMINISTIC step at a time (owner ask 2026-07-08: "seaweed
    /// clumps that can get stuck on things and group together from the waves"; round 2: the art's own
    /// snag and drag-tail anchors reach the runtime).
    ///
    /// <para><b>Time is stepped, never waited on.</b> The live presenter paces its slow tick on the
    /// wall clock and reads its sea from the game clock, so two real runs never agree frame for frame.
    /// The fixture installs the presenter through its test seam and steps it with an explicit game-time
    /// delta and slow-tick flag, so a scripted 60 s is the same 60 s here and in CI — which is what lets
    /// a pose sheet be hashed and compared across builds (the round-1 ↔ round-2 knob-0 A/B).</para>
    ///
    /// <para>Everything the presenter reads is pinned: a stepped clock, a steady sea (fixed current,
    /// wind and sea state), a deep flat seabed, a fresh <c>GameConfig</c>, the shared wind and day-night
    /// shader globals, and a synthetic drift-weed kit with known anchors — no real region, no
    /// AssetDatabase.</para>
    /// </summary>
    public class SeaweedAnchorsPlayTests
    {
        private const float Dt = 1f / 30f;             // game seconds per step
        private const int StepsPerSlowTick = 15;        // 0.5 s — the presenter's own cadence
        private const int SixtySeconds = 1800;

        private sealed class DeepFlat : ITidalTerrain
        {
            public float ElevationAt(Vector2 worldPos) => -5f;
        }

        /// <summary>A steady working sea: a set to the east-north-east, a light breeze, a light sea.</summary>
        private sealed class SteadySea : IEnvironmentService
        {
            public Vector2 Current = new Vector2(0.25f, 0.1f);
            public Vector2 Wind = new Vector2(4f, 0f);
            public float SeaState01 = 0.3f;
            public int WorldSeed => 4242;
            public TideProfile ActiveTideProfile { get; set; }
            public EnvironmentSample Sample()
                => new EnvironmentSample(Wind, Current, 0f, SeaState.Light, 1f, SeaState01);
            public float TideHeightAt(double totalSeconds) => 0f;
            public float WaterLevelAt(double totalSeconds) => 0f;
        }

        private sealed class SteppedClock : IGameClock
        {
            public double TotalSeconds { get; set; }
            public GameTime Now => new GameTime(TotalSeconds);
            public Season Season => Season.EarlySpring;
            public int Year => 1;
            public int DayIndex => 0;
            public int DayOfSeason => 1;
            public Weekday Weekday => Weekday.Monday;
            public bool IsMarketDay => false;
            public float HourOfDay => 0f;
            public float DayFraction => 0f;
            public bool IsPaused { get; set; }
            public float TimeScale { get; set; } = 1f;
        }

        private readonly List<Object> _spawned = new();
        private SteppedClock _clock;
        private SteadySea _sea;
        private SeaweedPresenter _presenter;
        private readonly List<string> _publishedTraps = new();

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();

            _clock = new SteppedClock { TotalSeconds = 1000.0 };
            _sea = new SteadySea();
            GameServices.Clock = _clock;
            GameServices.Environment = _sea;
            GameServices.TidalTerrain = new DeepFlat();
            var config = ScriptableObject.CreateInstance<GameConfig>();
            _spawned.Add(config);
            GameServices.Config = config;

            // The shared globals the presenter reads — pinned, so a previous test's wind cannot leak in.
            Shader.SetGlobalVector("_WindWorld", new Vector4(0.3f, 0.05f, 0f, 0f));
            Shader.SetGlobalColor("_DayNightTint", Color.white);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (string id in _publishedTraps) EventBus.Publish(new TrapRemoved(id));
            _publishedTraps.Clear();
            if (_presenter != null) Object.DestroyImmediate(_presenter.gameObject);
            _presenter = null;
            foreach (var o in _spawned) if (o != null) Object.DestroyImmediate(o);
            _spawned.Clear();
            GameServices.Reset();
        }

        // ---- the synthetic bed ------------------------------------------------------------------

        private static Sprite MakeSprite(int w, int h)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false, true) { filterMode = FilterMode.Point };
            var px = new Color32[w * h];
            for (int i = 0; i < px.Length; i++) px[i] = new Color32(255, 255, 255, 255);
            tex.SetPixels32(px);
            tex.Apply(false, false);
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 32f);
        }

        /// <summary>Two clumps of one species, drawn at 0.5 m and 1.2 m — enough for the bed's three
        /// tiers to resolve to real painted entries.</summary>
        private DriftWeedKit MakeKit()
        {
            var kit = ScriptableObject.CreateInstance<DriftWeedKit>();
            _spawned.Add(kit);
            kit.Species = new[] { "TestKelp" };
            kit.Ramps = new[] { "living" };
            var small = MakeSprite(16, 12);
            var big = MakeSprite(40, 24);
            _spawned.Add(small.texture); _spawned.Add(small);
            _spawned.Add(big.texture); _spawned.Add(big);
            kit.Entries = new[]
            {
                new DriftWeedKit.Entry { Sprite = small, SizeMeters = 0.5f, SpeciesIndex = 0, RampIndex = 0, VariantCell = 0 },
                new DriftWeedKit.Entry { Sprite = big, SizeMeters = 1.2f, SpeciesIndex = 0, RampIndex = 0, VariantCell = 1 },
            };
            kit.BuiltFromRigSha256 = "test";
            kit.InvalidateViews();
            return kit;
        }

        private SeaweedDef MakeDef(DriftWeedKit kit)
        {
            var def = ScriptableObject.CreateInstance<SeaweedDef>();
            _spawned.Add(def);
            def.Id = "decor.seaweed_test_bed";
            def.RegionSceneName = "";              // any scene — the fixture's own
            def.BedCenter = Vector2.zero;
            def.BedSize = new Vector2(24f, 12f);
            def.PieceCount = 12;
            def.MinSpawnDepthMeters = 0.5f;
            def.RespawnSeconds = 20f;
            def.WeedArt = kit;
            def.RampWeights = new[] { 1f };
            def.FadeInSeconds = 0f;
            return def;
        }

        private SeaweedPresenter Install(SeaweedDef def)
        {
            var lib = ScriptableObject.CreateInstance<SeaweedLibrary>();
            _spawned.Add(lib);
            lib.Beds = new[] { def };
            if (_presenter != null) Object.DestroyImmediate(_presenter.gameObject);
            _presenter = SeaweedPresenter.InstallForTests(lib);
            return _presenter;
        }

        private void PlaceTrap(string id, float x, float y)
        {
            EventBus.Publish(new TrapPlaced(id, x, y));
            _publishedTraps.Add(id);
        }

        /// <summary>Advance the clock and step the presenter once; the slow tick fires on its own cadence.</summary>
        private void Step(int stepIndex)
        {
            _clock.TotalSeconds += Dt;
            _presenter.StepForTests(Dt, stepIndex % StepsPerSlowTick == 0);
        }

        // ---- the pose sheet (the A/B instrument) ----------------------------------------------

        /// <summary>
        /// Every piece's state, bed position and drawn transform, every step, as round-trip text. Two
        /// runs of the same scenario must produce the same bytes; round 2 at knob 0 must produce round
        /// 1's bytes.
        /// </summary>
        private string RunPoseSheet(SeaweedDef def, int steps, System.Action arrange = null)
        {
            var presenter = Install(def);
            arrange?.Invoke();
            var sb = new StringBuilder(steps * def.PieceCount * 64);
            for (int s = 0; s < steps; s++)
            {
                Step(s);
                var beds = presenter.BedsForTests;
                Assert.AreEqual(1, beds.Count, "the one authored bed activated");
                var bed = beds[0];
                for (int i = 0; i < bed.Pos.Length; i++)
                {
                    var t = bed.Renderers[i].transform;
                    sb.Append(s).Append(' ').Append(i).Append(' ').Append(bed.State[i]).Append(' ')
                      .Append(bed.Pos[i].x.ToString("R")).Append(' ').Append(bed.Pos[i].y.ToString("R")).Append(' ')
                      .Append(t.position.x.ToString("R")).Append(' ').Append(t.position.y.ToString("R")).Append(' ')
                      .Append(t.localEulerAngles.z.ToString("R")).Append('\n');
                }
            }
            return sb.ToString();
        }

        private static string Sha256Hex(string text)
        {
            using var sha = SHA256.Create();
            byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(text));
            var sb = new StringBuilder(hash.Length * 2);
            foreach (byte b in hash) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }

        /// <summary>
        /// The determinism arm: the same seed, the same scripted sea, the same 60 s → the same bytes,
        /// twice, with a trap buoy in the bed for pieces to snag on. The sheet's hash is logged (and the
        /// sheet written beside the player's cache) so a build can be compared against another build's
        /// run of this exact scenario — the round-1 ↔ round-2 knob-0 A/B is that comparison.
        /// </summary>
        [Test]
        public void SixtySeconds_SameSeedTwice_IsByteIdentical()
        {
            void Buoy() => PlaceTrap("test.buoy.a", 6f, 0.5f);

            string first = RunPoseSheet(MakeDef(MakeKit()), SixtySeconds, Buoy);
            string second = RunPoseSheet(MakeDef(MakeKit()), SixtySeconds, Buoy);

            Assert.AreEqual(first.Length, second.Length, "the two sheets differ in length");
            Assert.IsTrue(string.Equals(first, second, System.StringComparison.Ordinal),
                          "the same seed and the same scripted sea produced different poses");

            string path = Path.Combine(Application.temporaryCachePath, "seaweed-pose-sheet.txt");
            File.WriteAllText(path, first);
            Debug.Log($"[SeaweedPoseSheet] sha256={Sha256Hex(first)} steps={SixtySeconds} bytes={first.Length} path={path}");

            int live = 0;
            var bed = _presenter.BedsForTests[0];
            for (int i = 0; i < bed.State.Length; i++) if (bed.State[i] != SeaweedMath.StateDormant) live++;
            Assert.Greater(live, 0, "the bed never came alive — the sheet measured nothing");
        }
    }
}
