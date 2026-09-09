using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Environment;
using HiddenHarbours.Fishing;
using HiddenHarbours.World;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// 🐟 <b>"I WANT TO SEE THEM" (the owner, 2026-09-09) — the duty cycle of a fish in his frame at the
    /// St Peters landing, measured rather than argued.</b>
    ///
    /// <para><b>What PR 2 (#771) fixed and what it did not.</b> #771 fixed the PICTURE: a shoal now swims
    /// at its species' <c>ShoalSpreadMetres</c> (1.2–4.0 m) instead of a 17–44 m figure-eight, so the fish
    /// of a school you are sitting on are all in the frame. It did not fix the FEEL, because it did not
    /// move the thing that decides whether there is a school in your frame at all: <b>the lattice</b>.
    /// <see cref="FishSchoolModel"/> holds at most ONE school per <c>(cell, slot)</c>, so
    /// <c>CellSizeMetres</c> is a hard ceiling on how many school ANCHORS a square kilometre of water can
    /// hold — 1/(0.120 km)² = 69/km² at the retired 120 m cell. The fish are drawn AT the anchor (within
    /// ~7 m of it), and the boat camera is 24.9 × 14 m: against a 14 400 m² cell that is a 6 % chance the
    /// anchor is anywhere he can see, before the window and the water have their say.</para>
    ///
    /// <para><b>The instrument.</b> The real <see cref="FishSchoolModel"/> over the real
    /// <c>GameConfig</c>, the real <see cref="FishSpeciesDef"/> assets, St Peters' own painted seabed and
    /// its own tide and wind, sampled every ten in-game minutes through the daylight of a whole game day,
    /// for <b>nine world seeds</b> — a fixture that hard-codes one seed inside a seeded window tests the
    /// seed, not the rule. A fish counts as SEEN only when the pose <see cref="ShoalMath"/> gives it lands
    /// inside the camera rect and the presenter's own gates
    /// (<see cref="SwimmerVisibility"/>, the baked swim sheet, <c>FishSchoolPresenter.SpreadMetresFor</c>)
    /// let it be drawn at all.</para>
    ///
    /// <para><b>It carries its own negative control</b> (<see cref="TheLattice_IsTheTermThatCapsIt"/>):
    /// the same measurement with every shipped value except a <b>120 m</b> cell. One literal, one claim —
    /// and the test cannot go green on a revert of the lattice.</para>
    ///
    /// <para><b>What it is NOT.</b> Not a claim about the plate (a drawn count is not an on-screen count
    /// until a PNG is opened) and not a claim about any region but St Peters. The settings it measures are
    /// GLOBAL, so every region's sea moved with them; the PR body carries the region-wide number.</para>
    /// </summary>
    public class FishDutyCycleAtTheLandingTests
    {
        private const string RegionId = "region.st_peters";
        private const string ConfigPath = "Assets/_Project/Data/Config/GameConfig.asset";
        private const string SeabedPath = "Assets/_Project/Data/Terrain/StPetersSeabed.asset";
        private const string FishDir = "Assets/_Project/Data/Fish/";

        /// <summary>The pool <c>StPetersBuilder</c> wires into the persistent <c>FishingController</c> —
        /// the SAME array the catch resolver rolls from, which is what makes a drawn fish a fish that
        /// bites.</summary>
        private static readonly string[] PoolAssets =
        {
            "SoftShellClam", "AtlanticCod", "Haddock", "Mackerel", "Pollock",
            "StripedBass", "AtlanticHerring", "WinterFlounder",
        };

        // St Peters' own environment, from StPeters.unity's EnvironmentService.
        private const int SceneSeed = 12345;
        private static readonly TideProfile Tide = new TideProfile { MeanLevel = 0f, Amplitude = 2.2f, PhaseHours = 1f };
        private static readonly WindProfile Wind = new WindProfile
        {
            PrevailingDirectionDeg = 45f, DirectionWanderDeg = 35f, MeanStrength = 3f,
            StrengthVariability = 1.3f, GustStrength = 1.2f, GustVeerDeg = 10f,
            ChangeHours = 6f, GustChangeHours = 0.4f, CalmMaxStrength = 5.7f,
        };

        /// <summary>Nine seeds. The berth pocket is one or two cells wide and a daylight day holds about
        /// six slots, so ONE seed is about six independent draws — the spread across seeds is real and
        /// the bar is read off the pooled figure, never off a lucky seed.</summary>
        private static readonly int[] Seeds = { 12345, 1, 7, 99, 2026, 31337, 555, 8080, 424242 };

        // The game's own cameras, restated from CameraFollow's constants rather than read off it (a bar
        // fetched from the code under test is a mirror).
        private const float BoatCameraHeightM = 14f;
        private const float FootCameraHeightM = 9f;
        private const float Aspect = 16f / 9f;

        // Daylight — the hours he actually plays.
        private const float DawnHour = 6f, DuskHour = 20f;

        /// <summary>Where he stands and floats at the landing (world metres).</summary>
        private static readonly Spot[] Spots =
        {
            new Spot("berth pocket, afloat",  211.5f, -5.8f, BoatCameraHeightM),
            new Spot("approach head, afloat", 206f,    0f,   BoatCameraHeightM),
            new Spot("off the wharf, afloat", 225f,    0f,   BoatCameraHeightM),
            new Spot("out the approach",      245f,    0f,   BoatCameraHeightM),
            new Spot("berth pocket, on foot", 211.5f, -5.8f, FootCameraHeightM),
            new Spot("off the wharf, on foot",225f,    0f,   FootCameraHeightM),
        };

        private readonly struct Spot
        {
            public readonly string Name;
            public readonly Vector2 Pos;
            public readonly float CameraHeightM;
            public Spot(string name, float x, float y, float h) { Name = name; Pos = new Vector2(x, y); CameraHeightM = h; }
            public Rect View => new Rect(Pos.x - CameraHeightM * Aspect * 0.5f,
                                         Pos.y - CameraHeightM * 0.5f,
                                         CameraHeightM * Aspect, CameraHeightM);
        }

        // ---- the world the sim asks --------------------------------------------------------------------

        private sealed class Clock : IGameClock
        {
            public GameConfig Config;
            public double TotalSeconds { get; set; }
            public GameTime Now => default;
            public Weekday Weekday => default;
            public Season Season => (Season)(int)((long)System.Math.Floor(TotalSeconds / Config.SecondsPerDay)
                                                  / Mathf.Max(1, Config.DaysPerSeason) % 4);
            public int Year => 1;
            public int DayIndex => (int)(TotalSeconds / Config.SecondsPerDay);
            public int DayOfSeason => 1;
            public bool IsMarketDay => false;
            public float HourOfDay => (float)(TotalSeconds % Config.SecondsPerDay / Config.SecondsPerHour);
            public float DayFraction => (float)(TotalSeconds % Config.SecondsPerDay / Config.SecondsPerDay);
            public bool IsPaused { get; set; }
            public float TimeScale { get; set; } = 1f;
        }

        /// <summary>St Peters' tide and weather off the SHIPPED models, at whatever instant is asked —
        /// so the water column the location gate reads is the one the hulls float in.</summary>
        private sealed class Env : IEnvironmentService
        {
            public GameConfig Config;
            public int Seed;
            public TideProfile ActiveTideProfile { get => Tide; set { } }
            public int WorldSeed => Seed;
            public EnvironmentSample Sample() => default;
            public float TideHeightAt(double t) => TideModel.Height(t, Tide, Config);
            public float WaterLevelAt(double t) => TideModel.Height(t, Tide, Config);
            public float SeaState01At(double t)
                => WeatherModel.SeaState01(WeatherModel.SampleWind(t, Seed, Config.SecondsPerHour, Wind).magnitude);
        }

        private sealed class Terrain : ITidalTerrain
        {
            public PaintedHeightField Field;
            public float ElevationAt(Vector2 p) => Field.ElevationAt(p);
        }

        private GameConfig _config;
        private PaintedHeightField _seabed;
        private FishSwimSpriteLibrary _library;
        private readonly List<FishSpeciesDef> _pool = new List<FishSpeciesDef>();

        [SetUp]
        public void SetUp()
        {
            GameServices.Reset();

            _config = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath);
            Assert.IsNotNull(_config, ConfigPath + " is missing — the owner's tuning is the subject here");

            var map = AssetDatabase.LoadAssetAtPath<PaintedHeightMap>(SeabedPath);
            Assert.IsNotNull(map, SeabedPath + " is missing — this measures the AUTHORED seabed, not a fake");
            _seabed = map.Field;
            Assert.IsNotNull(_seabed, "the painted seabed did not decode — is its height PNG still isReadable?");

            _library = Resources.Load<FishSwimSpriteLibrary>(FishSwimSpriteLibrary.ResourcesPath);
            Assert.IsNotNull(_library, "no baked swim library — with no art the presenter draws nothing at all");

            _pool.Clear();
            foreach (string name in PoolAssets)
            {
                var def = AssetDatabase.LoadAssetAtPath<FishSpeciesDef>(FishDir + name + ".asset");
                Assert.IsNotNull(def, FishDir + name + ".asset is missing from St Peters' pool");
                _pool.Add(def);
                FishSpeciesRegistry.Register(def);     // FishSchoolPresenter.SpreadMetresFor reads this
            }

            GameServices.Config = _config;
            GameServices.TidalTerrain = new Terrain { Field = _seabed };
            GameServices.CurrentRegionId = RegionId;
        }

        [TearDown]
        public void TearDown()
        {
            // ⚠ Nothing here writes to a loaded .asset: the negative control moves a COPY of the
            // settings struct, never the owner's config or a species Def.
            GameServices.Reset();
        }

        // ---- 1. the targets ----------------------------------------------------------------------------

        /// <summary>
        /// <b>THE RULING, AS A NUMBER.</b> Over the daylight of a whole game day, on nine seeds, at the
        /// berth he keeps his boat at: a drawn fish is in the frame for at least 40 % of the samples
        /// afloat and 20 % on foot, and a finder MARK is in frame for at least 60 % — the mark leads the
        /// fish, which is the rule that keeps the sounder worth owning.
        /// </summary>
        [Test]
        public void FishAreCommonWhereHePlays_AfloatAndOnFoot()
        {
            Row[] rows = Measure(_config.FishSchools, _pool, "SHIPPED");

            Assert.GreaterOrEqual(rows[0].FishFraction, 0.40f,
                $"from the deck at the berth a fish is on screen only {rows[0].FishFraction:P1} of the " +
                "daylight — the owner's 2026-09-09 ruling is that they are COMMON where he plays");
            Assert.GreaterOrEqual(rows[0].MarkFraction, 0.60f,
                $"the finder shows a school at the berth only {rows[0].MarkFraction:P1} of the daylight — " +
                "the mark must lead the fish");
            Assert.GreaterOrEqual(rows[4].FishFraction, 0.20f,
                $"standing on the wharf at the berth pocket a fish is on screen only " +
                $"{rows[4].FishFraction:P1} of the daylight — he stands there more than he floats off it");

            // The two further-out viewpoints are not targets; they must simply not have got worse, and
            // the measured 'today' for them is in the PR body.
            foreach (Row r in rows)
                Assert.Greater(r.FishFraction, 0.05f,
                    $"{r.Name}: {r.FishFraction:P1} — no viewpoint at the landing may be back where PR 2 left it");
        }

        // ---- 2. the negative control -------------------------------------------------------------------

        /// <summary>
        /// <b>THE LATTICE IS THE TERM THAT CAPS IT</b> — the same measurement, the same authored
        /// densities, the same windows, the same disc, and the one retired number put back: a
        /// <b>120 m</b> cell.
        ///
        /// <para>A <c>(cell, slot)</c> holds at most ONE school by construction, so the cell size is a
        /// hard ceiling on school anchors per square kilometre — 69/km² at 120 m against 2 066/km² at the
        /// shipped 22 m — and <c>SchoolsPerSquareKilometre</c> is clamped to it
        /// (<see cref="FishSchoolMath.BaseChanceForDensity"/> caps at 1 per cell). This is why no density
        /// number alone could ever have reached the target, and why this test cannot go green on a revert
        /// of the lattice.</para>
        /// </summary>
        [Test]
        public void TheLattice_IsTheTermThatCapsIt()
        {
            FishSchoolSettings retired = _config.FishSchools;
            retired.CellSizeMetres = 120f;                 // the one retired number

            Row[] rows = Measure(retired, _pool, "the retired 120 m lattice");

            Assert.Less(rows[0].FishFraction, 0.10f,
                $"on a 120 m lattice a fish was on screen {rows[0].FishFraction:P1} of the daylight from " +
                "the deck — if that is no longer true the cell was not the term that capped this, and the " +
                "whole argument of this PR needs re-deriving");

            float ceiling120 = 1f / (0.120f * 0.120f);
            float ceilingNow = 1f / ((_config.FishSchools.CellSizeMetres / 1000f)
                                     * (_config.FishSchools.CellSizeMetres / 1000f));
            Assert.Greater(ceilingNow, ceiling120 * 4f,
                $"the shipped cell must buy real headroom: {ceilingNow:F0}/km^2 against {ceiling120:F0}/km^2");
        }

        // ---- 3. the disc's floor comes from what it guards ----------------------------------------------

        /// <summary>
        /// <b>THE DISC MAY NEVER BE SMALLER THAN THE SHOAL IT HOLDS.</b> A school is admitted to a view
        /// query only when its DISC overlaps the camera rect (<c>FishSchoolModel.OverlapsRect</c>), but
        /// the fish are drawn at OFFSETS from the anchor. If the smallest disc were smaller than the
        /// furthest a drawn fish sits from its anchor, a school whose fish are on screen could be culled
        /// before it is drawn — fish that pop in at the edge of the frame.
        ///
        /// <para>The bar is brute-forced from the real <see cref="ShoalMath"/> over every authored school
        /// size and spread, not asserted from a formula.</para>
        /// </summary>
        [Test]
        public void TheDisc_IsNeverSmallerThanTheShoalItHolds()
        {
            var buf = new ShoalMath.Swimmer[64];
            float worst = 0f;
            string worstId = null;

            foreach (FishSpeciesDef def in _pool)
            {
                if (def.IsShellfish || !_library.TrySwimKindFor(def.Id, out string kind)) continue;
                float lengthM = _library.LengthMetresFor(kind);
                float spread = FishSchoolPresenter.SpreadMetresFor(def.Id);
                int lo = Mathf.Max(1, def.MinSchoolMarks), hi = Mathf.Max(lo, def.MaxSchoolMarks);

                for (int n = lo; n <= hi; n++)
                for (int seed = 1; seed <= 120; seed++)
                for (double t = 0; t < 20; t += 0.31)
                {
                    int solved = ShoalMath.Fill(lengthM, n, t, seed, spread,
                                                ShoalMath.DefaultSpeedMetresPerSecond, 1f,
                                                ShoalMath.DefaultZMetres, buf);
                    for (int i = 0; i < solved; i++)
                    {
                        float d = Mathf.Sqrt(buf[i].X * buf[i].X + buf[i].Y * buf[i].Y);
                        if (d > worst) { worst = d; worstId = def.Id; }
                    }
                }
            }

            Debug.Log($"[fish] the furthest a drawn fish sits from its anchor: {worst:F2} m ({worstId})");
            Assert.GreaterOrEqual(_config.FishSchools.MinRadiusMetres, worst,
                $"the smallest school disc is {_config.FishSchools.MinRadiusMetres} m but a drawn fish " +
                $"reaches {worst:F2} m from its anchor ({worstId}) — a school whose fish are on screen " +
                "would be culled before it is drawn");
        }

        // ---- the measurement ----------------------------------------------------------------------------

        private readonly struct Row
        {
            public readonly string Name;
            public readonly float FishFraction;      // samples with >= 1 DRAWN fish inside the camera rect
            public readonly float MarkFraction;      // samples with >= 1 school in view (the finder's mark)
            public readonly float MeanFish;
            public readonly int MaxSprites;
            public Row(string name, float fish, float mark, float meanFish, int maxSprites)
            { Name = name; FishFraction = fish; MarkFraction = mark; MeanFish = meanFish; MaxSprites = maxSprites; }
        }

        /// <summary>
        /// The table. One row per viewpoint, pooled over every seed: what share of the daylight samples
        /// put a DRAWN fish inside the camera rect, what share put a school in view at all, and how many
        /// fish are on screen on average.
        /// </summary>
        private Row[] Measure(in FishSchoolSettings settings, List<FishSpeciesDef> pool, string label)
        {
            var rows = new Row[Spots.Length];
            var clock = new Clock { Config = _config };
            GameServices.Clock = clock;

            var log = new System.Text.StringBuilder();
            log.AppendLine($"[fish] duty cycle at the St Peters landing — {label}: cell " +
                           $"{settings.CellSizeMetres} m, window {settings.MinWindowHours}-" +
                           $"{settings.MaxWindowHours} h of {settings.SlotHours}, disc " +
                           $"{settings.MinRadiusMetres}-{settings.MaxRadiusMetres} m, min column " +
                           $"{settings.MinWaterColumnMetres} m, {Seeds.Length} seeds, daylight " +
                           $"{DawnHour}-{DuskHour} h");
            log.AppendLine("viewpoint                 | fish on screen | mark in frame | mean fish | max sprites");

            var schools = new List<FishSchool>(FishSchoolModel.ViewCells);
            var swimmers = new ShoalMath.Swimmer[64];

            for (int si = 0; si < Spots.Length; si++)
            {
                Spot spot = Spots[si];
                Rect view = spot.View;
                int samples = 0, withFish = 0, withMark = 0, fishTotal = 0, maxSprites = 0;

                foreach (int seed in Seeds)
                {
                    GameServices.Environment = new Env { Config = _config, Seed = seed };
                    var world = new LiveFishSchoolWorld(_config, RegionId);
                    var model = new FishSchoolModel(world, pool, in settings, _config.DepthDrop,
                                                    _config.SecondsPerHour);

                    double step = _config.SecondsPerHour / 6.0;          // every ten in-game minutes
                    for (double t = 0; t < _config.SecondsPerDay; t += step)
                    {
                        float hour = (float)(t / _config.SecondsPerHour);
                        if (hour < DawnHour || hour >= DuskHour) continue;
                        clock.TotalSeconds = t;
                        samples++;

                        int n = model.SchoolsInView(view, t, schools);
                        if (n > 0) withMark++;

                        int onScreen = 0, sprites = 0;
                        for (int i = 0; i < n; i++)
                        {
                            FishSchool s = schools[i];
                            if (SwimmerVisibility.For(s.DepthMetres, _config.DepthDrop) == SwimmerDraw.None)
                                continue;                                 // too deep to show on the water

                            string primary = s.SpeciesIds != null && s.SpeciesIds.Count > 0 ? s.SpeciesIds[0] : null;
                            if (string.IsNullOrEmpty(primary)) continue;
                            if (!_library.TrySwimKindFor(primary, out string kind)) continue;   // no baked art

                            int marks = Mathf.Clamp(s.MarkCount, 0, 32);
                            if (marks <= 0) continue;

                            int solved = ShoalMath.Fill(_library.LengthMetresFor(kind), marks, t,
                                                        ShoalMath.SeedFor(FishSchoolPresenter.SchoolKey(s)),
                                                        FishSchoolPresenter.SpreadMetresFor(primary),
                                                        ShoalMath.DefaultSpeedMetresPerSecond, 1f,
                                                        ShoalMath.DefaultZMetres, swimmers);
                            sprites += solved;
                            for (int k = 0; k < solved; k++)
                            {
                                float x = s.Centre.x + swimmers[k].X, y = s.Centre.y + swimmers[k].Y;
                                if (x >= view.xMin && x <= view.xMax && y >= view.yMin && y <= view.yMax)
                                    onScreen++;
                            }
                        }

                        if (sprites > maxSprites) maxSprites = sprites;
                        if (onScreen > 0) withFish++;
                        fishTotal += onScreen;
                    }
                }

                float f = samples == 0 ? 0f : (float)withFish / samples;
                float m = samples == 0 ? 0f : (float)withMark / samples;
                float mean = samples == 0 ? 0f : (float)fishTotal / samples;
                rows[si] = new Row(spot.Name, f, m, mean, maxSprites);
                log.AppendLine($"{spot.Name,-25} | {f,13:P1} | {m,12:P1} | {mean,9:0.00} | {maxSprites,11}");
            }

            Debug.Log(log.ToString());
            return rows;
        }
    }
}
