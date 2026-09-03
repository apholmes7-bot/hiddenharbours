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
            SnagTargets.Clear();

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
            SnagTargets.Clear();
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
            // Anchors in the sprite frame (metres from the pivot, +y up) — the shape the builder writes
            // from the sidecar: 2–3 snag tips out on the fronds, one drag tail at the far end.
            kit.Entries = new[]
            {
                new DriftWeedKit.Entry
                {
                    Sprite = small, SizeMeters = 0.5f, SpeciesIndex = 0, RampIndex = 0, VariantCell = 0,
                    Snags = new[] { new Vector2(-0.2f, 0.05f), new Vector2(0.15f, 0.2f), new Vector2(0.1f, -0.2f) },
                    DragTail = new Vector2(0.25f, 0f),
                },
                new DriftWeedKit.Entry
                {
                    Sprite = big, SizeMeters = 1.2f, SpeciesIndex = 0, RampIndex = 0, VariantCell = 1,
                    Snags = new[] { new Vector2(-0.5f, 0.1f), new Vector2(0.3f, 0.4f) },
                    DragTail = new Vector2(0.6f, -0.05f),
                },
            };
            kit.BuiltFromRigSha256 = "test";
            kit.InvalidateViews();
            return kit;
        }

        /// <param name="roundTwo">true = the shipped round-2 knobs; false = every round-2 knob at 0 /
        /// off, which must reproduce round 1 byte for byte.</param>
        private SeaweedDef MakeDef(DriftWeedKit kit, bool roundTwo = true)
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
            if (!roundTwo)
            {
                def.SnagByFrondTip = false;
                def.DragAlignDegreesPerSecond = 0f;
                def.SnagSwayDegrees = 0f;
                def.SnagBreakWaveMeters = 0f;
            }
            return def;
        }

        /// <summary>Dead glass with a steady set: no wind, sea state 0 → the field is exactly flat, so
        /// the transport is the current alone and every alignment target is exact.</summary>
        private void MakeGlass(Vector2 current)
        {
            _sea.Current = current;
            _sea.Wind = Vector2.zero;
            _sea.SeaState01 = 0f;
            Shader.SetGlobalVector("_WindWorld", Vector4.zero);
        }

        private void Run(int steps) { for (int s = 0; s < steps; s++) Step(s); }

        private static float Angle(Vector2 a, Vector2 b) => Vector2.Angle(a, b);

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
        private int _snagsInLastSheet;

        /// <param name="arrange">Runs AFTER the presenter is installed and BEFORE the first step — the
        /// place to publish trap signals, which a presenter can only hear once it exists.</param>
        private string RunPoseSheet(SeaweedDef def, int steps, System.Action arrange = null)
        {
            var presenter = Install(def);
            arrange?.Invoke();
            _snagsInLastSheet = 0;
            var sb = new StringBuilder(steps * def.PieceCount * 64);
            for (int s = 0; s < steps; s++)
            {
                Step(s);
                var beds = presenter.BedsForTests;
                Assert.AreEqual(1, beds.Count, "the one authored bed activated");
                var bed = beds[0];
                for (int i = 0; i < bed.Pos.Length; i++)
                {
                    if (bed.State[i] == SeaweedMath.StateSnagged) _snagsInLastSheet++;
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
            int snagsFirst = _snagsInLastSheet;
            string second = RunPoseSheet(MakeDef(MakeKit()), SixtySeconds, Buoy);

            Assert.Greater(snagsFirst, 0, "nothing ever snagged on the buoy — the sheet did not exercise the snag path");
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

        /// <summary>
        /// The knob-0 arm of the A/B: every round-2 knob at 0 / off must be round 1 exactly — no piece
        /// ever hooks, a snagged piece rests on the radius around the buoy, and the hashed rotation never
        /// eases. The logged hash is compared by hand against the sheet the seam commit recorded
        /// (sha256 ae4a8b5b…) — a literal is not asserted because a trig ULP can differ per platform,
        /// but the structural facts that make the bytes agree are.
        /// </summary>
        [Test]
        public void KnobZero_SixtySeconds_IsRoundOne_NoHook_RadiusRest_HashedRotationStands()
        {
            var def = MakeDef(MakeKit(), roundTwo: false);
            var presenter = Install(def);
            PlaceTrap("test.buoy.a", 6f, 0.5f);            // after Install: a presenter hears only what it is there for
            var buoy = new Vector2(6f, 0.5f);

            var rotAtBirth = new float[def.PieceCount];
            var bornAt = new double[def.PieceCount];
            int snaggedSeen = 0;
            var sb = new StringBuilder();
            for (int s = 0; s < SixtySeconds; s++)
            {
                Step(s);
                var bed = presenter.BedsForTests[0];
                for (int i = 0; i < bed.Pos.Length; i++)
                {
                    var t = bed.Renderers[i].transform;
                    sb.Append(s).Append(' ').Append(i).Append(' ').Append(bed.State[i]).Append(' ')
                      .Append(bed.Pos[i].x.ToString("R")).Append(' ').Append(bed.Pos[i].y.ToString("R")).Append(' ')
                      .Append(t.position.x.ToString("R")).Append(' ').Append(t.position.y.ToString("R")).Append(' ')
                      .Append(t.localEulerAngles.z.ToString("R")).Append('\n');

                    if (bed.State[i] == SeaweedMath.StateDormant) continue;
                    Assert.IsFalse(bed.Hooked[i], $"step {s} piece {i}: hooked with SnagByFrondTip off");
                    if (bed.BornAt[i] != bornAt[i]) { bornAt[i] = bed.BornAt[i]; rotAtBirth[i] = bed.BaseRotDeg[i]; }
                    Assert.AreEqual(rotAtBirth[i], bed.BaseRotDeg[i], $"step {s} piece {i}: the hashed rotation eased at knob 0");
                    if (bed.State[i] == SeaweedMath.StateSnagged)
                    {
                        snaggedSeen++;
                        Assert.AreEqual(def.BuoyRestRadiusMeters, (bed.Pos[i] - buoy).magnitude, 1e-3f,
                                        $"step {s} piece {i}: a round-1 snag rests on the radius");
                    }
                }
            }
            Assert.Greater(snaggedSeen, 0, "nothing snagged — the round-1 path was not exercised");
            Debug.Log($"[SeaweedPoseSheet:knob0] sha256={Sha256Hex(sb.ToString())} steps={SixtySeconds} bytes={sb.Length}");
        }

        // ---- round 2: the tail trails the sea ---------------------------------------------------

        [Test]
        public void Drifter_EasesUntilItsTailTrailsBehindTheTransport()
        {
            MakeGlass(new Vector2(0.25f, 0.1f));
            var def = MakeDef(MakeKit());
            var presenter = Install(def);
            Run(900);                                                    // 30 s at 25 deg/s: any start closes

            var bed = presenter.BedsForTests[0];
            var kit = def.WeedArt;
            // On glass the transport IS the set: current × FlowResponse (the shared wind global is 0).
            Vector2 transport = _sea.Current * def.FlowResponse;
            int judged = 0;
            for (int i = 0; i < bed.Pos.Length; i++)
            {
                if (bed.State[i] != SeaweedMath.StateDrifting) continue;
                if (_clock.TotalSeconds - bed.BornAt[i] < 20.0) continue;   // give a respawn time to turn
                Assert.GreaterOrEqual(bed.ArtIndex[i], 0, $"piece {i} wears no painted clump");
                Vector2 tail = kit.Entries[bed.ArtIndex[i]].DragTail;
                float drawn = bed.Renderers[i].transform.localEulerAngles.z;   // glass: no wobble
                Vector2 tailWorld = SeaweedMath.Rotate(tail, drawn);
                Assert.Less(Angle(tailWorld, -transport), 0.5f,
                            $"piece {i}: its tail points {tailWorld} — it should trail behind the set {transport}");
                judged++;
            }
            Assert.Greater(judged, 0, "no drifter old enough to judge");
        }

        // ---- round 2: the frond hooks the line -------------------------------------------------

        /// <summary>Lines across the set's path so several drifters must meet one within 60 s.</summary>
        private static readonly Vector2[] LineFence =
        {
            new Vector2(6f, -4.8f), new Vector2(6f, -3.6f), new Vector2(6f, -2.4f), new Vector2(6f, -1.2f),
            new Vector2(6f, 0f), new Vector2(6f, 1.2f), new Vector2(6f, 2.4f), new Vector2(6f, 3.6f), new Vector2(6f, 4.8f),
        };

        private void AssertHungByATip(SeaweedPresenter presenter, SeaweedDef def, Vector2 transport,
                                      System.Func<Vector2, bool> contactIsOnATarget, out int hooked)
        {
            var bed = presenter.BedsForTests[0];
            var kit = def.WeedArt;
            hooked = 0;
            for (int i = 0; i < bed.Pos.Length; i++)
            {
                if (bed.State[i] != SeaweedMath.StateSnagged) continue;
                Assert.IsTrue(bed.Hooked[i], $"piece {i} snagged but rests on the radius — the art carries anchors, it should hang by one");
                var t = bed.Renderers[i].transform;
                float drawn = t.localEulerAngles.z;                         // glass: the hang, no sway
                Vector2 anchor = kit.Entries[bed.ArtIndex[i]].Snags[bed.SnagAnchorIndex[i]];
                Vector2 tipWorld = bed.Pos[i] + SeaweedMath.Rotate(anchor, drawn);
                Assert.AreEqual(bed.SnagPoint[i].x, tipWorld.x, 1e-3f, $"piece {i}: the tip is not on the line (x)");
                Assert.AreEqual(bed.SnagPoint[i].y, tipWorld.y, 1e-3f, $"piece {i}: the tip is not on the line (y)");
                Assert.IsTrue(contactIsOnATarget(bed.SnagPoint[i]), $"piece {i}: hangs from {bed.SnagPoint[i]}, which is no target");
                Vector2 body = bed.Pos[i] - bed.SnagPoint[i];             // tip -> pivot
                Assert.Less(Angle(body, transport), 0.5f, $"piece {i}: the body should stream down-transport from the tip");
                Assert.AreEqual(bed.Pos[i].x, t.position.x, 1e-5f, "the transform draws at the pivot");
                hooked++;
            }
        }

        [Test]
        public void Drifter_HooksThePlayersLine_ByTheFrondTipThatMetIt_AndHangsDownTransport()
        {
            MakeGlass(new Vector2(0.25f, 0f));
            var def = MakeDef(MakeKit());
            var presenter = Install(def);
            for (int k = 0; k < LineFence.Length; k++) PlaceTrap($"test.fence.{k}", LineFence[k].x, LineFence[k].y);
            Run(SixtySeconds);

            AssertHungByATip(presenter, def, _sea.Current,
                             p => System.Array.Exists(LineFence, l => (l - p).sqrMagnitude < 1e-8f), out int hooked);
            Assert.Greater(hooked, 0, "no drifter met the fence in 60 s of a 0.25 m/s set");
        }

        [Test]
        public void FleetBuoy_ThroughTheCoreRegistry_IsHooked_AndLetsGoWhenWithdrawn()
        {
            MakeGlass(new Vector2(0.25f, 0f));
            for (int k = 0; k < LineFence.Length; k++) SnagTargets.Set($"fleet.test.buoy{k}", LineFence[k], 0f);
            var def = MakeDef(MakeKit());
            var presenter = Install(def);
            Run(SixtySeconds);

            AssertHungByATip(presenter, def, _sea.Current,
                             p => System.Array.Exists(LineFence, l => (l - p).sqrMagnitude < 1e-8f), out int hooked);
            Assert.Greater(hooked, 0, "no drifter hooked an NPC line published through Core SnagTargets");

            var bed = presenter.BedsForTests[0];
            for (int i = 0; i < bed.Pos.Length; i++)
                if (bed.State[i] == SeaweedMath.StateSnagged)
                    Assert.IsTrue(bed.SnagId[i].StartsWith("fleet.test.buoy"), $"piece {i} hangs on '{bed.SnagId[i]}'");

            // The fisher hauls her gear: the registry entries go, and on the next slow tick the wrack is adrift again.
            SnagTargets.Clear();
            Run(StepsPerSlowTick + 1);
            for (int i = 0; i < bed.Pos.Length; i++)
            {
                Assert.AreNotEqual(SeaweedMath.StateSnagged, bed.State[i], $"piece {i} still hangs on a line that was hauled");
                Assert.IsFalse(bed.Hooked[i]);
            }
        }

        [Test]
        public void LyingToHull_ThroughTheCoreRegistry_IsHookedOnItsRim()
        {
            MakeGlass(new Vector2(0.25f, 0f));
            const float halfBeam = 1.2f;
            var hulls = new[] { new Vector2(6f, -3f), new Vector2(6f, 0f), new Vector2(6f, 3f) };
            for (int k = 0; k < hulls.Length; k++) SnagTargets.Set($"fleet.test.boat{k}", hulls[k], halfBeam);
            var def = MakeDef(MakeKit());
            var presenter = Install(def);
            Run(SixtySeconds);

            AssertHungByATip(presenter, def, _sea.Current,
                             p => System.Array.Exists(hulls, h => Mathf.Abs((h - p).magnitude - halfBeam) < 1e-3f),
                             out int hooked);
            Assert.Greater(hooked, 0, "no drifter fouled on a lying-to hull");
        }

        [Test]
        public void SwellRelease_TearsAHungClumpFree_AndItClearsTheLine()
        {
            MakeGlass(new Vector2(0.25f, 0f));
            var def = MakeDef(MakeKit());
            def.SnagBreakWaveMeters = 0.05f;       // any real swell tears it off
            def.SnagBreakFreeSeconds = 30f;
            var presenter = Install(def);
            for (int k = 0; k < LineFence.Length; k++) PlaceTrap($"test.fence.{k}", LineFence[k].x, LineFence[k].y);
            Run(SixtySeconds);
            var bed = presenter.BedsForTests[0];
            int hookedOnGlass = 0;
            for (int i = 0; i < bed.Pos.Length; i++) if (bed.State[i] == SeaweedMath.StateSnagged) hookedOnGlass++;
            Assert.Greater(hookedOnGlass, 0, "nothing hooked on glass to release");

            // The sea gets up: a fresh breeze and a lively state put real height under every anchor.
            _sea.Wind = new Vector2(9f, 0f);
            _sea.SeaState01 = 0.6f;
            Run(StepsPerSlowTick * 20);            // 10 s — the animator eases the field up
            int stillHooked = 0;
            for (int i = 0; i < bed.Pos.Length; i++) if (bed.State[i] == SeaweedMath.StateSnagged && bed.Hooked[i]) stillHooked++;
            Assert.AreEqual(0, stillHooked, "the swell should have torn every hung clump off its line");
            for (int i = 0; i < bed.Pos.Length; i++)
                if (bed.State[i] == SeaweedMath.StateDrifting && bed.NoSnagUntil[i] > 0.0)
                    Assert.Greater(bed.NoSnagUntil[i], _clock.TotalSeconds - 30.0, $"piece {i}: the break-free immunity was set");
        }
    }
}
