using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using HiddenHarbours.Environment;
using HiddenHarbours.World;
using HiddenHarbours.App.Editor;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// ⭐ <b>THE PLATE SWEEP — the instrument behind <c>docs/design/water-fidelity-register.md</c></b>
    /// (water-fidelity charter 2026-09-01, PR 0; the ADR 0027 judge pass owed since August, done as an
    /// instrument rather than an opinion).
    ///
    /// <para>It stands each of four world-locked viewpoints in a real scene — Nine Mile Creek's one steep
    /// stretch, a Nine Mile Creek sand shoal, the St Peters arrival, and the West Water open-water CONTROL —
    /// and photographs every cell of <b>{glass, light, blow, gale} × {spring low, mean, spring high} ×
    /// {noon, golden hour, night with the searchlight}</b> on the GPU, through the shipped components: the
    /// shipped <see cref="WaterSurface"/> pushes the weather and the tide exactly as it does in Play (fed by a
    /// fake environment), the shipped bridge publishes the wave field, the fetch and the breaker contour, and
    /// the shipped day/night maths publishes the hour. It then writes every plate, a contact sheet per viewpoint,
    /// and a MANIFEST that records every uniform each plate was pinned to — read BACK from the globals and the
    /// property block after the push, never assumed.</para>
    ///
    /// <para><b>Re-run it before and after every later PR of the charter: the A/B pair IS the evidence.</b>
    /// The sheets live under <c>artifacts/water-plates/</c> (gitignored).</para>
    ///
    /// <para><b>What this deliberately is NOT.</b> It is the flat Universal2D pass: <c>DisplacedWaterSurface</c>
    /// registers and ticks only in Play, so the vertex lift of the displaced sea (and the hull waterline that
    /// rides it) is absent from these plates. The fragment — every colour, foam, surf, caustic, reflection and
    /// light term — is the same program the displaced pass runs on lifted geometry. Themes that judge the swell's
    /// GEOMETRY under the relief light need a Play-mode plate, and the register says so where it matters.</para>
    ///
    /// <para><b>The laws this fixture is built under</b> (each one has already cost a PR):
    /// a per-pixel diff of two shots of the same sea measures <c>_Time</c>, not the change — so the sweep asserts
    /// on the MECHANISM (wet fraction, published globals, the file's orientation) and leaves judgement to the eye;
    /// a <see cref="MaterialPropertyBlock"/> is sticky — every property scrubbed is written on every shot;
    /// a cold shader cache fakes a regression — the sweep warms until nothing is compiling before its first plate;
    /// the night is captured in HDR and the day/night multiply is applied on readback, or a night photographs
    /// as cream; the published FILE is asserted, not the buffer it came from; and nothing here writes to
    /// <c>Water.mat</c>.</para>
    ///
    /// <para><b>Self-skips without a graphics device</b> — the standing CI law. A skip is "NOT VERIFIED".</para>
    /// </summary>
    public class WaterFidelityPlateSweepTests
    {
        // =============================================================================================
        //  The frame
        // =============================================================================================

        /// <summary>40 m across at 960 px = 24 px/m — one plate pixel per cell of the shader's own world
        /// pixelize grid (<c>_PixelsPerUnit</c> ships at 24 on all nine materials, an owner ruling), so the
        /// dither reads as the dither and nothing aliases against it.</summary>
        const float FrameMetres = 40f;
        const int ShotPx = 960;
        const int ThumbPx = 320;      // 3 x 3 box down from the plate
        const string OutRoot = "artifacts/water-plates";

        // =============================================================================================
        //  The matrix
        // =============================================================================================

        enum Weather { Glass, Light, Blow, Gale }
        enum Tide { Low, Mean, High }
        enum Hour { Noon, Golden, Night }

        static readonly string[] WeatherName = { "glass", "light", "blow", "gale" };
        static readonly string[] TideName = { "low", "mean", "high" };
        static readonly string[] HourName = { "noon", "golden", "night" };

        /// <summary>The continuous sea state each weather stands for. Glass is the strict 0 (amplitudes are
        /// exactly 0 — ADR 0018 §(1)); blow is #680's "ordinary working day" sea; gale is #691's gale, just
        /// under the Storm edge the dev override reaches at 1.</summary>
        static readonly float[] SeaStateOf = { 0f, 0.25f, 0.55f, 0.95f };

        /// <summary>The wind's heading — the onshore breeze #680 and #691 both shot under, so these plates
        /// are comparable with those. Its STRENGTH is derived from the sea state through the sim's own inverse
        /// (<see cref="WeatherModel.WindStrengthFor"/>), so a plate's wind and sea state are the pair the
        /// weather model would actually produce together.</summary>
        static readonly Vector2 WindHeading = new Vector2(6f, -5.3f).normalized;

        static float SeaStateFor(Weather w) => SeaStateOf[(int)w];
        static Vector2 WindFor(Weather w) => WindHeading * WeatherModel.WindStrengthFor(SeaStateFor(w));

        static float HourFor(Hour h, DayNightProfile profile) => h switch
        {
            Hour.Noon => 12f,
            Hour.Golden => GoldenHourFor(profile),
            _ => 2f,                                     // the dead of night
        };

        /// <summary>
        /// The golden hour is FOUND on the shipped profile, not assumed from the sunset hour: the afternoon
        /// hour at which the published tint is warmest (largest red-over-blue) while still bright. The first
        /// draft took "sunset minus three quarters" — 19:15 on the default profile — and photographed a tint
        /// of (0.087, 0.070, 0.117), which is dusk purple at night-depth intensity: the gradient's sunset key
        /// sits at day fraction 0.74 (17:46) and the intensity curve is already at 0.30 by 0.80, so the
        /// warm light lives an hour and a half before the sun actually goes.
        /// </summary>
        static float GoldenHourFor(DayNightProfile profile)
        {
            float best = 12f, bestWarmth = float.MinValue;
            for (float hour = 12f; hour <= profile.SunsetHour; hour += 0.05f)
            {
                Color tint = DayNightMath.DayNightTint(hour, profile, 1f, 0f);
                float luma = 0.299f * tint.r + 0.587f * tint.g + 0.114f * tint.b;
                if (luma < 0.35f) continue;                 // still bright enough to read the water by
                float warmth = tint.r - tint.b;
                if (warmth > bestWarmth) { bestWarmth = warmth; best = hour; }
            }
            return best;
        }

        /// <summary>
        /// The moon for the night plates: a NEW moon, below the horizon — published explicitly, because the
        /// shader treats an UNSET phase state as a FULL moon at full presence (its fallback for a scene with
        /// no cycle) and drew a moon disc with a glitter path over the first draft's "moonless" night.
        /// <c>MoonCycle</c> is a Play-only host, so nothing else publishes it here. The night is therefore
        /// the darkest the profile makes (moonlight lift 0), which is the honest control for the lamp; a
        /// lit-moon row is one publish away.
        /// </summary>
        static void PublishTheNewMoon()
        {
            // x = phase01 (a hair past new, so the packed state reads as SET rather than unset), y = the
            // signed terminator, z = brightness 0, w = above-horizon 0 — MoonCycle.ComputeState's packing.
            Shader.SetGlobalVector(Shader.PropertyToID("_MoonDir"), Vector4.zero);
            Shader.SetGlobalVector(Shader.PropertyToID("_MoonPhaseState"), new Vector4(0.02f, -1f, 0f, 0f));
        }

        /// <summary>
        /// The searchlight for the night plates: <see cref="BoatSpotlight"/>'s SERIALIZED DEFAULTS, which are
        /// what the hull presentation service mints for the cape when her def declares a spotlight. The water
        /// term publishes intensity × the component's water-side strength (0.8). #691's PLATE-A stand-in used
        /// 2.6 / 30 m; this is the lamp that ships, so a small pool is the honest picture.
        /// </summary>
        static class Searchlight
        {
            public const float HeightMeters = 2.5f;
            public static readonly Color Colour = new Color(1f, 0.88f, 0.62f, 1f);
            public const float Intensity = 1.5f;
            public const float WaterStrength = 0.8f;
            public const float Range = 9f;
            public const float ConeHalfDeg = 26f;
            public const float AngularSoftness = 0.45f;
            public const float EdgeSoftness = 0.7f;
            public const float GateThreshold = 0.12f, GateSoftness = 0.35f, GateFallback = 1f;
            /// <summary>Where the lamp sits relative to the frame's centre, throwing +x across it.</summary>
            public static readonly Vector2 Offset = new Vector2(-6f, 0f);
            public static readonly Vector2 BeamDir = Vector2.right;
        }

        // =============================================================================================
        //  The fakes — the sim the shipped components read, under the sweep's control
        // =============================================================================================

        /// <summary>
        /// The environment the shipped <see cref="WaterSurface"/> samples. Exactly what the game's service
        /// would hand it for this weather and this tide, held still: the component then pushes <c>_Chop</c>,
        /// <c>_Roughness</c>, <c>_Flow</c>, <c>_WindDir</c>, <c>_WaterLevel</c>, <c>_RainIntensity</c>, the
        /// mood blend and the palette seam itself — the sweep never writes those, it only sets the weather.
        /// The tidal CURRENT is zero (stated in the manifest): a plate is one instant of the tide, and the
        /// set's only visible effect is the flow scroll rate.
        /// </summary>
        sealed class PlateEnvironment : IEnvironmentService
        {
            public Vector2 Wind;
            public float SeaState01;
            public float WaterLevel;
            public float Visibility = 1f;

            public int WorldSeed => 7;
            public TideProfile ActiveTideProfile { get; set; }
            public EnvironmentSample Sample() => new EnvironmentSample(
                Wind, Vector2.zero, WaterLevel, WeatherModel.SeaFromWind(Wind.magnitude), Visibility, SeaState01);
            public float TideHeightAt(double totalSeconds) => WaterLevel;
        }

        /// <summary>The searchlight, published through the SHIPPED bridge (the #691 pattern) — and, beside
        /// it, the SINGLETON globals <c>BoatSpotlight</c> publishes for the same lamp. Both, because that is
        /// what the shipped component does: the bridge fills the array the water sums its cone weight from,
        /// and the singleton is what <c>SpriteLitDecor</c> reads. A fixture that published only the array
        /// left <c>_BoatLightColor</c> unset, and every night plate this file has ever taken was therefore
        /// missing the beam's warm tint — a shipped term, absent from the evidence.</summary>
        sealed class FakeLamp : IWaterLightEmitter
        {
            public WaterLightState State;
            public bool TryGetWaterLight(out WaterLightState state) { state = State; return state.IsLive; }
        }

        /// <summary>A built viewpoint: the region's terrain and sea, and where the camera looks.</summary>
        sealed class Stage
        {
            public string Name;
            public string Title;
            public ITidalTerrain Terrain;
            public GameObject SeaGo;
            public Vector2 Aim;
            public float TideMean, TideAmplitude;
            public bool OpenWater;
        }

        /// <summary>One plate's pinned facts, read back after the push — the manifest row.</summary>
        struct PlateRecord
        {
            public string File;
            public Weather Weather; public Tide Tide; public Hour Hour;
            public float SeaState, WindSpeed, WaterLevel, HourOfDay;
            public Color Tint; public Vector2 SunDir; public float SunElevation;
            public bool Breaks; public float BreakDepth, OuterDepth;
            public float Chop, Roughness, Flow, PushedLevel;    // read back from the property block
            public float WaveCount;                             // _WaveFieldParams.x read back
            public float LightCount;                            // _WaterLightCount read back
            public float WetFraction, MeanLumaWet, StdLumaWet;
        }

        // =============================================================================================
        //  Fixture state
        // =============================================================================================

        GameObject _camGo;
        Camera _cam;
        RenderTexture _rt;
        readonly List<GameObject> _built = new List<GameObject>();
        ITidalTerrain _previousTerrain;
        IEnvironmentService _previousEnvironment;
        GameConfig _previousConfig;
        PlateEnvironment _env;
        FakeLamp _lamp;
        DayNightProfile _profile;
        bool _profileIsOurs;          // false when _profile is the SHIPPED asset — never destroy that
        Texture2D _reflectFallback;

        static readonly MethodInfo PushUniformsSnap = typeof(WaterSurface).GetMethod(
            "PushUniforms", BindingFlags.Instance | BindingFlags.NonPublic, null, Type.EmptyTypes, null);

        [SetUp]
        public void SetUp()
        {
            _previousTerrain = GameServices.TidalTerrain;
            _previousEnvironment = GameServices.Environment;
            _previousConfig = GameServices.Config;
        }

        [TearDown]
        public void TearDown()
        {
            if (_lamp != null) { WaterLightBridge.Unregister(_lamp); _lamp = null; }
            if (_cam != null) _cam.targetTexture = null;
            if (_rt != null) { _rt.Release(); Object.DestroyImmediate(_rt); _rt = null; }
            foreach (var go in _built) if (go != null) Object.DestroyImmediate(go);
            _built.Clear();
            if (_camGo != null) Object.DestroyImmediate(_camGo);
            _camGo = null; _cam = null;
            if (_profile != null && _profileIsOurs) Object.DestroyImmediate(_profile);
            _profile = null; _profileIsOurs = false;
            if (_reflectFallback != null) { Object.DestroyImmediate(_reflectFallback); _reflectFallback = null; }

            GameServices.TidalTerrain = _previousTerrain;
            GameServices.Environment = _previousEnvironment;
            GameServices.Config = _previousConfig;

            // Globals are STICKY: hand the next fixture a silent, calm, daylit sea with no lamp in it.
            WaveFieldBridge.PublishGlobals(PackedWaveField.Empty);
            WaveFieldBridge.PublishFetchOff();
            WaveFieldBridge.PublishBreakersOff();
            Shader.SetGlobalFloat(Shader.PropertyToID("_WaterLightCount"), 0f);
            Shader.SetGlobalColor(Shader.PropertyToID("_BoatLightColor"), Color.clear);
            Shader.SetGlobalVector(Shader.PropertyToID("_BoatLightParams"), Vector4.zero);
            Shader.SetGlobalColor(Shader.PropertyToID("_DayNightTint"), Color.white);
            Shader.SetGlobalVector(Shader.PropertyToID("_SunDir"), Vector4.zero);
            Shader.SetGlobalFloat(Shader.PropertyToID("_SunElevation"), 0f);
            Shader.SetGlobalVector(Shader.PropertyToID("_MoonDir"), Vector4.zero);
            Shader.SetGlobalVector(Shader.PropertyToID("_MoonPhaseState"), Vector4.zero);
        }

        static void RequireAGraphicsDevice()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore(
                    "SKIPPED, NOT VERIFIED — no graphics device (Null Device), so nothing rendered and " +
                    "nothing was proved. Expected on CI; the plates need a GPU.");
        }

        // =============================================================================================
        //  The four viewpoints
        // =============================================================================================

        [Test]
        public void ThePlateSweep_NineMileCreek_TheSteepStretch()
        {
            RequireAGraphicsDevice();
            Prepare();
            Stage stage = BuildNineMileCreek();
            stage.Name = "nmc-steep";
            stage.Title = "NINE MILE CREEK - THE STEEP STRETCH (XI EARNS A LIP)";
            stage.Aim = AimAtTheSteepestBreak(stage);
            Sweep(stage);
        }

        [Test]
        public void ThePlateSweep_NineMileCreek_TheSandShoal()
        {
            RequireAGraphicsDevice();
            Prepare();
            Stage stage = BuildNineMileCreek();
            stage.Name = "nmc-sand";
            stage.Title = "NINE MILE CREEK - THE SAND SHOAL (SPILLING SURF)";
            stage.Aim = AimAtTheLongestSurfRun(stage);
            Sweep(stage);
        }

        [Test]
        public void ThePlateSweep_StPeters_TheArrival()
        {
            RequireAGraphicsDevice();
            Prepare();
            Stage stage = BuildStPeters();
            stage.Name = "stp-arrival";
            stage.Title = "ST PETERS - THE ARRIVAL (REEF DOOR AND APRON)";
            // The reef shelf runs from the island's edge (IslandCenter.x + IslandRadius = 190) out
            // ReefShelfWidth (25 m) to the drop-off; the dredged approach cuts through it on y = 0.
            // Centred on the shelf so the frame holds apron, drop-off and channel at every tide.
            stage.Aim = new Vector2(StPetersBuilder.IslandCenter.x + StPetersBuilder.IslandRadius
                                    + StPetersBuilder.ReefShelfWidth * 0.5f + 4f, 0f);
            Sweep(stage);
        }

        [Test]
        public void ThePlateSweep_WestWater_TheOpenWaterControl()
        {
            RequireAGraphicsDevice();
            Prepare();
            Stage stage = BuildWestWater();
            stage.Name = "ww-open";
            stage.Title = "WEST WATER - OPEN WATER CONTROL (NO SEABED IN FRAME)";
            stage.Aim = WestWaterPlan.RegionWorldCenter;
            Sweep(stage);
        }

        // =============================================================================================
        //  Headless guards on the instrument itself (run on CI too)
        // =============================================================================================


        // =============================================================================================
        //  ⭐⭐ THE NIGHT (water-fidelity PR 4) — the owner's 2026-09-02 ruling, measured
        // =============================================================================================
        //
        //  *"It should be dark enough at night that the player feels the need to use radar and the
        //   lighting, a clear calm night with moonlight should be brighter if not cloudy."*
        //
        //  Two instruments, because the ruling has two halves that pull opposite ways. The MOON half is
        //  judged with no lamp in the frame (a lamp would light the very thing being measured); the LAMP
        //  half is judged on the blackest corner the moon half produced, which is where a searchlight is
        //  for. Both report the same two numbers per plate: the mean luma of wet pixels (how dark the sea
        //  photographs) and the SURF CONTRAST — the mean luma inside the breaker contour minus the mean
        //  outside it, which is "can you see the break line?" written as a number.

        /// <summary>One corner of the night: what the moon is doing, and whether there is cloud in the way.
        /// DECLARED rather than looked up off a clock — a corner is a controlled experiment, and the plate's
        /// own label says which corner it is. The same numbers drive the tint's moonlight lift and the moon
        /// state the shader draws its disc and glitter from, so the sky and the light agree.</summary>
        readonly struct NightCorner
        {
            public readonly string Key, Label;
            public readonly float Illumination, Elevation, Visibility;
            public NightCorner(string key, string label, float illum, float elev, float visibility)
            { Key = key; Label = label; Illumination = illum; Elevation = elev; Visibility = visibility; }
        }

        /// <summary>Visibility below the profile's <c>FogVisibilityForFullDim</c> (0.15), so the overcast
        /// corners carry the FULL weather dim — which is what "cloudy" means to <c>DayNightMath</c> for
        /// M1: there is no cloud-cover axis in <c>EnvironmentSample</c> and this PR does not invent one.</summary>
        const float OvercastVisibility = 0.10f;

        static readonly NightCorner[] NightCorners =
        {
            new NightCorner("newmoon-clear",  "NEW MOON - CLEAR",     0f, 0f, 1f),
            new NightCorner("fullmoon-clear", "FULL MOON - CLEAR",    1f, 1f, 1f),
            new NightCorner("fullmoon-cloud", "FULL MOON - OVERCAST", 1f, 1f, OvercastVisibility),
            new NightCorner("newmoon-cloud",  "NEW MOON - OVERCAST",  0f, 0f, OvercastVisibility),
        };

        /// <summary>What one night plate is worth: how dark it photographed, and whether the break line
        /// survived.</summary>
        struct NightRecord
        {
            public string File, Corner, Weather;
            public Color Tint;
            public float MeanLumaWet, SurfLuma, BodyLuma, SurfContrast;
            public float InBeamLuma, OutBeamLuma;
        }

        [Test]
        public void TheNight_IsDarkUnlessTheMoonIsUpAndTheSkyIsClear()
        {
            // ⭐ The MOON half of the ruling, with no lamp in the frame. Four corners x {glass, blow} at
            // 02:00 spring low over the sand shoal, which is the viewpoint register row 12 cites.
            RequireAGraphicsDevice();
            Prepare();
            Stage stage = BuildNineMileCreek();
            stage.Name = "night-corners";
            stage.Title = "THE NIGHT, NO LAMP - MOON x CLOUD AT 02:00, SPRING LOW";
            stage.Aim = AimAtTheLongestSurfRun(stage);

            string dir = Path.Combine(Directory.GetCurrentDirectory(), OutRoot, "night");
            Directory.CreateDirectory(dir);
            BuildCamera();
            _cam.transform.position = new Vector3(stage.Aim.x, stage.Aim.y, -100f);
            WarmTheShaderCache(stage);

            var records = new List<NightRecord>();
            var thumbs = new Color[2][][];
            for (int r = 0; r < 2; r++) thumbs[r] = new Color[NightCorners.Length][];

            var weathers = new[] { Weather.Glass, Weather.Blow };
            for (int r = 0; r < weathers.Length; r++)
            for (int c = 0; c < NightCorners.Length; c++)
            {
                NightCorner corner = NightCorners[c];
                string file = $"night-{corner.Key}-{WeatherName[(int)weathers[r]]}";
                Color[] ldr = ShootNight(stage, weathers[r], corner, lamp: false, beamLit: null,
                                         Path.Combine(dir, file + ".png"), out NightRecord rec);
                rec.File = file + ".png";
                records.Add(rec);
                thumbs[r][c] = Thumbnail(ldr);
            }

            var top = new string[NightCorners.Length];
            var bottom = new string[NightCorners.Length];
            for (int c = 0; c < NightCorners.Length; c++) { top[c] = NightCorners[c].Label; bottom[c] = ""; }
            string sheet = Path.Combine(Directory.GetCurrentDirectory(), OutRoot, "SHEET-night-corners.png");
            ContactSheet.Write(sheet, stage.Title, top, bottom, new[] { "GLASS", "BLOW" }, thumbs, ThumbPx);
            File.WriteAllText(Path.Combine(dir, "NIGHT.txt"), NightReport(records));
            Debug.Log("[water-plates] night corners\n" + NightReport(records));

            Assert.IsTrue(File.Exists(sheet), "the night sheet must be written — the FILE is the evidence");

            // ⚠️ The TINT comparisons take the glass column (the tint is the sky, not the sea), but every
            // SURF number must come from the BLOW column: at glass the contour does not break at all, so
            // there are no pixels inside it and "surf luma" is a mean of nothing. The first run of this
            // test asserted on the glass column and was comparing 0.0000 against 0.0000.
            NightRecord newClear = Find(records, "newmoon-clear", "blow");
            NightRecord fullClear = Find(records, "fullmoon-clear", "blow");
            NightRecord fullCloud = Find(records, "fullmoon-cloud", "glass");
            NightRecord newCloud = Find(records, "newmoon-cloud", "glass");

            // ⭐ "if not cloudy" — CLOUD HIDES THE MOON EXACTLY. Every factor of the lift multiplies, and
            // (1 - weatherDim) is one of them, so at full overcast a full moon and a new moon are not
            // merely similar: they are the same computation. Pinned as an equality, because an
            // approximately-equal here would let the lift leak back in through a later edit.
            Assert.AreEqual(fullCloud.Tint.r, newCloud.Tint.r, 1e-6f, "cloud must hide the moon (r)");
            Assert.AreEqual(fullCloud.Tint.g, newCloud.Tint.g, 1e-6f, "cloud must hide the moon (g)");
            Assert.AreEqual(fullCloud.Tint.b, newCloud.Tint.b, 1e-6f, "cloud must hide the moon (b)");

            // ⭐ "dark enough that the player feels the need to use radar and the lighting" — on a moonless
            // night the break line must not be readable by eye. Judged as CONTRAST, not brightness: a sea
            // you cannot navigate is one where the surf does not stand out from the water beside it.
            Assert.Less(newClear.SurfContrast, 0.03f,
                $"a moonless night must not show you the break line — the surf stood {newClear.SurfContrast:F4} " +
                "of luma clear of the body, which is a sea you could steer by without the radar");

            // ⭐ "a clear calm night with moonlight should be brighter" — and by enough to matter.
            Assert.Greater(fullClear.MeanLumaWet, newClear.MeanLumaWet * 3f,
                $"a clear full-moon night must READ as lit: {fullClear.MeanLumaWet:F4} against the moonless " +
                $"{newClear.MeanLumaWet:F4}. If these are close, MoonlightLiftMax is back at its old 0.05.");
            Assert.Greater(fullClear.SurfContrast, newClear.SurfContrast * 4f,
                $"…and the break line must come back with it ({fullClear.SurfContrast:F4} against " +
                $"{newClear.SurfContrast:F4}) — brightness alone is not navigability");

            // …and the moonless floor must not have been raised to buy any of it (the ruling's other half).
            Assert.Less(newClear.MeanLumaWet, 0.06f,
                $"the moonless night must stay dark — it photographed {newClear.MeanLumaWet:F4}");
        }

        [Test]
        public void TheLamp_LIGHTS_TheWater_WhereAMultiplyCouldNot()
        {
            // ⭐ The LAMP half, on the blackest corner the moon half produced — a moonless clear night,
            // which is exactly the water register row 12 says the shipped searchlight "lights nothing you
            // can name" on. The A/B is one dial: _BeamLitStrength 0 is the shipped multiply-only reveal.
            RequireAGraphicsDevice();
            Prepare();
            Stage stage = BuildNineMileCreek();
            stage.Name = "night-lamp";
            stage.Title = "THE SHIPPED SEARCHLIGHT ON A MOONLESS NIGHT - LIT WATER OFF vs ON";
            stage.Aim = AimAtTheLongestSurfRun(stage);

            string dir = Path.Combine(Directory.GetCurrentDirectory(), OutRoot, "night");
            Directory.CreateDirectory(dir);
            BuildCamera();
            _cam.transform.position = new Vector3(stage.Aim.x, stage.Aim.y, -100f);
            WarmTheShaderCache(stage);

            NightCorner blackest = NightCorners[0];   // new moon, clear
            var records = new List<NightRecord>();
            var thumbs = new Color[2][][];
            for (int r = 0; r < 2; r++) thumbs[r] = new Color[2][];

            var weathers = new[] { Weather.Glass, Weather.Blow };
            float shipped = ShippedBeamLitStrength(stage);
            for (int r = 0; r < weathers.Length; r++)
            {
                for (int c = 0; c < 2; c++)
                {
                    float lit = c == 0 ? 0f : shipped;
                    string file = $"lamp-{WeatherName[(int)weathers[r]]}-lit{(c == 0 ? "0" : "on")}";
                    Color[] ldr = ShootNight(stage, weathers[r], blackest, lamp: true, beamLit: lit,
                                             Path.Combine(dir, file + ".png"), out NightRecord rec);
                    rec.File = file + ".png";
                    rec.Corner = c == 0 ? "lit 0 (as shipped)" : $"lit {shipped:F3} (ruled)";
                    records.Add(rec);
                    thumbs[r][c] = Thumbnail(ldr);
                }
            }

            string sheet = Path.Combine(Directory.GetCurrentDirectory(), OutRoot, "SHEET-night-lamp.png");
            ContactSheet.Write(sheet, stage.Title, new[] { "LIT WATER 0", "LIT WATER ON" }, new[] { "", "" },
                               new[] { "GLASS", "BLOW" }, thumbs, ThumbPx);
            File.WriteAllText(Path.Combine(dir, "LAMP.txt"), NightReport(records));
            Debug.Log("[water-plates] the lamp\n" + NightReport(records));

            Assert.IsTrue(File.Exists(sheet), "the lamp sheet must be written — the FILE is the evidence");
            Assert.Greater(shipped, 0f, "the shipped _BeamLitStrength must be non-zero, or this PR ships nothing");

            NightRecord off = records[0], on = records[1];          // glass, the sacred state
            NightRecord offBlow = records[2], onBlow = records[3];

            // ⭐ THE POINT. The shipped reveal MULTIPLIES the water inside the cone, and the frame is
            // multiplied again by a ~(0.016, 0.020, 0.040) night tint downstream: a dark sea times 3.5
            // times 0.02 is a dark sea. The lit term ADDS in the compensated bucket, so the pool of light
            // is a pool of light.
            Assert.Greater(on.InBeamLuma, off.InBeamLuma * 1.5f,
                $"the lamp must LIGHT the water it is pointed at — inside the cone the sea read " +
                $"{on.InBeamLuma:F4} with the term on against {off.InBeamLuma:F4} with it off. If these " +
                "are close, the additive term is not reaching the compensated bucket.");
            Assert.Greater(onBlow.InBeamLuma, offBlow.InBeamLuma * 1.5f,
                $"…in a blow as well ({onBlow.InBeamLuma:F4} against {offBlow.InBeamLuma:F4})");

            // …and ONLY the water it is pointed at: the term carries the cone weight, so outside the cone
            // nothing moves. This is the floodlamp complaint (owner, 2026-07-05) answered by construction.
            Assert.AreEqual(off.OutBeamLuma, on.OutBeamLuma, 0.002f,
                $"outside the cone the sea must be untouched ({off.OutBeamLuma:F4} vs {on.OutBeamLuma:F4}) — " +
                "a lamp that lifts the whole frame is the flood the reveal was built to stop being");
        }

        /// <summary>The <c>_BeamLitStrength</c> the sea actually ships with — the property block's where the
        /// push wrote one, else the material's, which is the precedence the GPU applies. Read, never typed:
        /// the plate must be of the shipped tuning.</summary>
        float ShippedBeamLitStrength(Stage stage)
        {
            var sr = stage.SeaGo.GetComponent<SpriteRenderer>();
            var block = new MaterialPropertyBlock();
            sr.GetPropertyBlock(block);
            Material mat = sr.sharedMaterial;
            Assert.IsTrue(mat.HasProperty("_BeamLitStrength"), "_BeamLitStrength must be a water-shader property");
            return block.HasFloat("_BeamLitStrength") ? block.GetFloat("_BeamLitStrength")
                                                      : mat.GetFloat("_BeamLitStrength");
        }

        /// <summary>One night plate: publish the sea for this weather at 02:00 spring low, then OVERRIDE the
        /// hour's moonless tint and moon state with the corner's declared moon, put the lamp in or take it
        /// out, optionally override the lit-water dial, shoot, and measure.</summary>
        Color[] ShootNight(Stage stage, Weather w, NightCorner corner, bool lamp, float? beamLit,
                           string path, out NightRecord rec)
        {
            _env.Visibility = corner.Visibility;   // read by the push AND by the tint below
            Publish(stage, w, Tide.Low, Hour.Night, out _, out _, out _, out _, out float hourOfDay);

            // The tint, recomputed with the corner's moon through the SHIPPED six-argument overload — the
            // same call DayNightController makes in Play. Every corner therefore differs only by what it
            // declares about the sky.
            Color tint = DayNightMath.DayNightTint(hourOfDay, _profile, corner.Visibility, _env.SeaState01,
                                                   corner.Illumination, corner.Elevation);
            Shader.SetGlobalColor(Shader.PropertyToID("_DayNightTint"), tint);
            PublishTheMoon(corner);
            RepublishTheLamp(stage, lamp);

            var sr = stage.SeaGo.GetComponent<SpriteRenderer>();
            var block = new MaterialPropertyBlock();
            sr.GetPropertyBlock(block);
            float restore = ShippedBeamLitStrength(stage);
            if (beamLit.HasValue) { block.SetFloat("_BeamLitStrength", beamLit.Value); sr.SetPropertyBlock(block); }

            Color[] ldr = Capture(tint, path);

            if (beamLit.HasValue) { block.SetFloat("_BeamLitStrength", restore); sr.SetPropertyBlock(block); }

            WetStatistics(stage, _env.WaterLevel, ldr, out _, out float meanLumaWet);
            float outerDepth = Shader.GetGlobalVector(Shader.PropertyToID("_BreakerOuter")).x;
            SurfContrast(stage, _env.WaterLevel, outerDepth, ldr, out float surfLuma, out float bodyLuma);
            BeamStatistics(stage, _env.WaterLevel, ldr, out float inBeam, out float outBeam);

            rec = new NightRecord
            {
                Corner = corner.Label, Weather = WeatherName[(int)w], Tint = tint,
                MeanLumaWet = meanLumaWet, SurfLuma = surfLuma, BodyLuma = bodyLuma,
                SurfContrast = surfLuma - bodyLuma, InBeamLuma = inBeam, OutBeamLuma = outBeam,
            };
            return ldr;
        }

        /// <summary>Publish the corner's moon the way <c>MoonCycle</c> packs it (x = phase, y = signed
        /// terminator, z = brightness, w = above-horizon presence). ⚠ An UNSET <c>_MoonPhaseState</c> is a
        /// FULL moon to this shader — its fallback for a scene with no cycle — so both ends are published
        /// explicitly and neither is left to a default.</summary>
        static void PublishTheMoon(NightCorner corner)
        {
            bool up = corner.Illumination > 0f && corner.Elevation > 0f;
            Shader.SetGlobalVector(Shader.PropertyToID("_MoonDir"),
                up ? new Vector4(-0.6f, 0.8f, 0f, 0f) : Vector4.zero);
            Shader.SetGlobalVector(Shader.PropertyToID("_MoonPhaseState"),
                up ? new Vector4(0.5f, 0f, corner.Illumination * corner.Elevation, corner.Elevation)
                   : new Vector4(0.02f, -1f, 0f, 0f));
        }

        /// <summary>Put the searchlight in the frame or take it out, through the SHIPPED bridge — the same
        /// publish <see cref="Publish"/> makes, re-run because the corner may want the sea unlit.</summary>
        void RepublishTheLamp(Stage stage, bool on) => PublishTheLamp(stage, on);

        /// <summary>"Can you see the break line?" as a number: the mean luma of wet pixels INSIDE the
        /// breaker contour (shallower than its outer depth — where the sea is breaking) against the mean
        /// outside it. A sea whose surf does not stand clear of the water beside it is one you cannot
        /// navigate by eye, whatever its average brightness.</summary>
        void SurfContrast(Stage stage, float level, float outerDepth, Color[] ldr,
                          out float surfLuma, out float bodyLuma)
        {
            double inSum = 0, outSum = 0;
            int inN = 0, outN = 0;
            for (int py = 0; py < ShotPx; py += 2)
            for (int px = 0; px < ShotPx; px += 2)
            {
                float depth = level - stage.Terrain.ElevationAt(PixelToWorld(px, py));
                if (depth <= 0f) continue;
                Color c = ldr[py * ShotPx + px];
                double l = 0.299 * c.r + 0.587 * c.g + 0.114 * c.b;
                if (depth <= outerDepth) { inSum += l; inN++; } else { outSum += l; outN++; }
            }
            surfLuma = inN > 0 ? (float)(inSum / inN) : 0f;
            bodyLuma = outN > 0 ? (float)(outSum / outN) : 0f;
        }

        /// <summary>The sea INSIDE the searchlight's cone against the sea outside it — the two numbers that
        /// say whether a lamp lights the water or merely lifts the frame. The cone is the shipped lamp's own
        /// geometry (range, half-angle) evaluated in world space, not a guess at where the pool looks like
        /// it is.</summary>
        void BeamStatistics(Stage stage, float level, Color[] ldr, out float inBeam, out float outBeam)
        {
            Vector2 lamp = stage.Aim + Searchlight.Offset;
            float cosHalf = Mathf.Cos(Searchlight.ConeHalfDeg * Mathf.Deg2Rad);
            double inSum = 0, outSum = 0;
            int inN = 0, outN = 0;
            for (int py = 0; py < ShotPx; py += 2)
            for (int px = 0; px < ShotPx; px += 2)
            {
                Vector2 world = PixelToWorld(px, py);
                if (level - stage.Terrain.ElevationAt(world) <= 0f) continue;
                Color c = ldr[py * ShotPx + px];
                double l = 0.299 * c.r + 0.587 * c.g + 0.114 * c.b;
                Vector2 d = world - lamp;
                float dist = d.magnitude;
                bool lit = dist > 0.05f && dist <= Searchlight.Range
                        && Vector2.Dot(d / dist, Searchlight.BeamDir.normalized) >= cosHalf;
                if (lit) { inSum += l; inN++; } else { outSum += l; outN++; }
            }
            inBeam = inN > 0 ? (float)(inSum / inN) : 0f;
            outBeam = outN > 0 ? (float)(outSum / outN) : 0f;
        }

        static NightRecord Find(List<NightRecord> records, string cornerKey, string weather)
        {
            foreach (NightRecord r in records)
                if (r.File.StartsWith($"night-{cornerKey}-{weather}", StringComparison.Ordinal)) return r;
            Assert.Fail($"no night plate for {cornerKey} / {weather}");
            return default;
        }

        static string NightReport(List<NightRecord> records)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# The night, measured. tint = the day/night MULTIPLY the frame is graded by; " +
                          "meanLumaWet = how dark the sea photographed; surf/body = mean luma inside vs " +
                          "outside the breaker contour, and their difference is 'can you see the break " +
                          "line?'; inBeam/outBeam = the sea inside vs outside the searchlight's cone.");
            sb.AppendLine("plate                              tint(rgb)              meanWet  surf   body   " +
                          "contrast  inBeam  outBeam");
            foreach (NightRecord r in records)
                sb.AppendLine($"{r.File,-34} {r.Tint.r:F3},{r.Tint.g:F3},{r.Tint.b:F3}   " +
                              $"{r.MeanLumaWet:F4}  {r.SurfLuma:F4} {r.BodyLuma:F4} {r.SurfContrast,8:F4}  " +
                              $"{r.InBeamLuma:F4}  {r.OutBeamLuma:F4}   [{r.Corner}]");
            return sb.ToString();
        }


        // =============================================================================================
        //  ⭐⭐ THE MIRROR (water-fidelity PR 5) — register rows 5 + 13, the owner's 2026-09-02 ruling
        // =============================================================================================
        //
        //  *"glass calm, i trust your judgement for the game, but we need reflections on water."*
        //
        //  The reflected CONTENT was never the complaint and does not move. The FORM did: at calm the sheen
        //  was a sin() of world-Y at a fixed 1.6 m wavelength, cubed — the striped rug of register row 5.
        //  It is now the SURFACE's own tilt.
        //
        //  Two numbers say whether that worked, and they must move in OPPOSITE directions or the change is
        //  not what it claims: the ROW-BAND CONTRAST (the standard deviation of the per-row mean luma —
        //  horizontal stripes and nothing else score on it) must collapse, while the MEAN LUMA must not,
        //  because the diagnostic already proved the stripes ARE the reflection (`_ReflectionStrength = 0`
        //  took the glass sea from 0.46 to 0.05). A change that killed the stripes AND the light would be
        //  the reflection deleted, wearing a mirror's name — so this shoots that arm too, as the control.

        /// <summary>One arm of the mirror A/B, and what it was worth.</summary>
        struct MirrorRecord
        {
            public string File, Arm, Viewpoint;
            public float MeanLumaWet, RowBandContrast, ColBandContrast;
        }

        [Test]
        public void TheGlassCalm_IsAMirror_AndNotAStripedRug()
        {
            RequireAGraphicsDevice();
            Prepare();

            string dir = Path.Combine(Directory.GetCurrentDirectory(), OutRoot, "mirror");
            Directory.CreateDirectory(dir);

            var records = new List<MirrorRecord>();
            var thumbs = new Color[3][][];
            for (int r = 0; r < 3; r++) thumbs[r] = new Color[2][];

            // Both glass viewpoints the charter names: the open-water CONTROL (nothing but sea and sky) and
            // the sand shoal (a shore in the frame, so the mirror is judged against something).
            var stages = new (string key, string title, Func<Stage> build)[]
            {
                ("ww-open", "WEST WATER - THE OPEN-WATER CONTROL", () =>
                {
                    Stage s = BuildWestWater();
                    s.Name = "ww-open"; s.Aim = WestWaterPlan.RegionWorldCenter;
                    return s;
                }),
                ("nmc-sand", "NINE MILE CREEK - THE SAND SHOAL", () =>
                {
                    Stage s = BuildNineMileCreek();
                    s.Name = "nmc-sand"; s.Aim = AimAtTheLongestSurfRun(s);
                    return s;
                }),
            };

            for (int v = 0; v < stages.Length; v++)
            {
                Stage stage = stages[v].build();
                BuildCamera();
                _cam.transform.position = new Vector3(stage.Aim.x, stage.Aim.y, -100f);
                WarmTheShaderCache(stage);

                // Three arms on ONE frozen scene: the shipped stripe, the mirror, and — the control that
                // makes the pair mean something — the reflection removed entirely.
                var arms = new (string key, string label, float form, bool killReflection)[]
                {
                    ("stripe",  "MIRROR FORM 0 (the shipped stripe)", 0f, false),
                    ("mirror",  "MIRROR FORM 1 (the surface's tilt)", 1f, false),
                    ("norefl",  "REFLECTION OFF (the control)",       1f, true),
                };
                for (int a = 0; a < arms.Length; a++)
                {
                    string file = $"mirror-{stages[v].key}-{arms[a].key}";
                    Color[] ldr = ShootMirror(stage, arms[a].form, arms[a].killReflection,
                                              Path.Combine(dir, file + ".png"), out MirrorRecord rec);
                    rec.File = file + ".png";
                    rec.Arm = arms[a].label;
                    rec.Viewpoint = stages[v].key;
                    records.Add(rec);
                    thumbs[a][v] = Thumbnail(ldr);
                }
            }

            string sheet = Path.Combine(Directory.GetCurrentDirectory(), OutRoot, "SHEET-mirror.png");
            ContactSheet.Write(sheet, "THE GLASS CALM - THE SHIPPED STRIPE, THE MIRROR, AND NO REFLECTION AT ALL",
                               new[] { "WEST WATER (OPEN)", "NINE MILE CREEK (SAND)" }, new[] { "", "" },
                               new[] { "STRIPE", "MIRROR", "NO REFL" }, thumbs, ThumbPx);
            File.WriteAllText(Path.Combine(dir, "MIRROR.txt"), MirrorReport(records));
            Debug.Log("[water-plates] the mirror\n" + MirrorReport(records));

            Assert.IsTrue(File.Exists(sheet), "the mirror sheet must be written — the FILE is the evidence");

            foreach (string key in new[] { "ww-open", "nmc-sand" })
            {
                MirrorRecord stripe = FindMirror(records, key, "stripe");
                MirrorRecord mirror = FindMirror(records, key, "mirror");
                MirrorRecord none = FindMirror(records, key, "norefl");

                // ⭐ THE STRIPES GO. Row-band contrast is the standard deviation of the per-row mean luma:
                // a rug of horizontal bands is the only thing that scores highly on it, and a sheen of sky
                // scores near zero however bright it is.
                Assert.Less(mirror.RowBandContrast, stripe.RowBandContrast * 0.5f,
                    $"{key}: the 1.6 m banding must collapse — row-band contrast {mirror.RowBandContrast:F4} " +
                    $"against the stripe's {stripe.RowBandContrast:F4}");

                // ⭐ …AND THE REFLECTION STAYS. The control is what makes that assertion mean something:
                // with the reflection removed the glass sea is nearly black, so a mirror arm that landed
                // near the control would be the reflection deleted rather than re-formed.
                Assert.Greater(mirror.MeanLumaWet, none.MeanLumaWet * 3f,
                    $"{key}: the sea must still be lit by a reflection — the mirror read " +
                    $"{mirror.MeanLumaWet:F4} against {none.MeanLumaWet:F4} with the reflection off");
                Assert.Greater(mirror.MeanLumaWet, stripe.MeanLumaWet * 0.6f,
                    $"{key}: …and by about as much light as the stripe put there ({mirror.MeanLumaWet:F4} " +
                    $"against {stripe.MeanLumaWet:F4}) — this PR changes the FORM, not the exposure");
                Assert.Less(mirror.MeanLumaWet, stripe.MeanLumaWet * 1.6f,
                    $"{key}: …and not by dramatically more ({mirror.MeanLumaWet:F4} against " +
                    $"{stripe.MeanLumaWet:F4}) — a blown-out calm is not a mirror either");

                // Anti-vacuous: the control must actually have removed something, or every ratio above is
                // measured against a number that was never the reflection.
                Assert.Less(none.MeanLumaWet, stripe.MeanLumaWet * 0.5f,
                    $"{key}: zeroing _ReflectionStrength must visibly darken the glass sea " +
                    $"({none.MeanLumaWet:F4} against {stripe.MeanLumaWet:F4}), or this control controls nothing");
            }
        }

        /// <summary>One mirror plate: the GLASS sea at mean tide and noon — the sacred state — with the form
        /// dial (and, for the control arm, the reflection master) overridden on the property block after the
        /// shipped push, exactly as the knob diagnostic does it.</summary>
        Color[] ShootMirror(Stage stage, float form, bool killReflection, string path, out MirrorRecord rec)
        {
            Publish(stage, Weather.Glass, Tide.Mean, Hour.Noon, out _, out Color tint, out _, out _, out _);

            var sr = stage.SeaGo.GetComponent<SpriteRenderer>();
            Material mat = sr.sharedMaterial;
            var block = new MaterialPropertyBlock();
            sr.GetPropertyBlock(block);
            Assert.IsTrue(mat.HasProperty("_MirrorForm"), "_MirrorForm must be a water-shader property");

            float restoreForm = block.HasFloat("_MirrorForm") ? block.GetFloat("_MirrorForm")
                                                              : mat.GetFloat("_MirrorForm");
            float restoreRefl = block.HasFloat("_ReflectionStrength") ? block.GetFloat("_ReflectionStrength")
                                                                      : mat.GetFloat("_ReflectionStrength");
            block.SetFloat("_MirrorForm", form);
            if (killReflection) block.SetFloat("_ReflectionStrength", 0f);
            sr.SetPropertyBlock(block);

            Color[] ldr = Capture(tint, path);

            block.SetFloat("_MirrorForm", restoreForm);
            block.SetFloat("_ReflectionStrength", restoreRefl);
            sr.SetPropertyBlock(block);

            WetStatistics(stage, _env.WaterLevel, ldr, out _, out float meanLumaWet);
            BandContrast(ldr, out float rowStd, out float colStd);
            rec = new MirrorRecord { MeanLumaWet = meanLumaWet, RowBandContrast = rowStd, ColBandContrast = colStd };
            return ldr;
        }

        /// <summary>The standard deviation of the per-ROW and per-COLUMN mean luma. A rug of horizontal
        /// bands scores on the first and almost nothing else does — which is why it, and not a brightness,
        /// is what the mirror has to collapse.</summary>
        static void BandContrast(Color[] px, out float rowStd, out float colStd)
        {
            var rowMean = new double[ShotPx];
            var colMean = new double[ShotPx];
            for (int y = 0; y < ShotPx; y++)
            for (int x = 0; x < ShotPx; x++)
            {
                Color c = px[y * ShotPx + x];
                double l = 0.299 * c.r + 0.587 * c.g + 0.114 * c.b;
                rowMean[y] += l; colMean[x] += l;
            }
            for (int i = 0; i < ShotPx; i++) { rowMean[i] /= ShotPx; colMean[i] /= ShotPx; }
            rowStd = (float)StdDev(rowMean);
            colStd = (float)StdDev(colMean);
        }

        static double StdDev(double[] values)
        {
            double mean = 0;
            for (int i = 0; i < values.Length; i++) mean += values[i];
            mean /= values.Length;
            double sq = 0;
            for (int i = 0; i < values.Length; i++) sq += (values[i] - mean) * (values[i] - mean);
            return System.Math.Sqrt(sq / values.Length);
        }

        static MirrorRecord FindMirror(List<MirrorRecord> records, string viewpoint, string arm)
        {
            foreach (MirrorRecord r in records)
                if (r.File == $"mirror-{viewpoint}-{arm}.png") return r;
            Assert.Fail($"no mirror plate for {viewpoint} / {arm}");
            return default;
        }

        static string MirrorReport(List<MirrorRecord> records)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# The glass calm at mean tide, noon. rowBand = std-dev of the per-row mean luma " +
                          "(horizontal stripes, and almost nothing else, score on it); colBand = the same " +
                          "down the columns; meanWet = how much light the reflection is putting on the sea.");
            sb.AppendLine("plate                              meanWet  rowBand  colBand   [arm]");
            foreach (MirrorRecord r in records)
                sb.AppendLine($"{r.File,-34} {r.MeanLumaWet:F4}   {r.RowBandContrast:F4}   " +
                              $"{r.ColBandContrast:F4}   [{r.Arm}]");
            return sb.ToString();
        }


        // =============================================================================================
        //  ⭐⭐ THE WHITECAPS (water-fidelity PR 7) — register row 7, the owner's 2026-09-02 ruling
        // =============================================================================================
        //
        //  *"let the procedural field replace the caps."*
        //
        //  `capField = lerp(capField, capPat, _WhitecapTexStrength)` — at the 0.865 every shipped material
        //  carried, the painted sheet does not decorate the cap field, it REPLACES it. The sheet is 16
        //  mirrored copies of one mark, so the sea wore one silhouette over and over and the caps could
        //  not go where the field said the crests were.
        //
        //  COVERAGE is the number, and it is measured against the plate's OWN distribution rather than an
        //  absolute white: a bar at p50 + half the way to p95 sits between the water and the foam however
        //  bright or dark that particular sea is. (PR 6 spent a cycle on an absolute luma floor that a
        //  legitimately darker sea walked under — the same lesson, one PR earlier.)

        /// <summary>One arm of the whitecap A/B, and what the sea was wearing.</summary>
        struct CapRecord
        {
            public string File, Arm, Cell;
            public float Coverage, MeanLumaWet;
            /// <summary>The mean colour of the BRIGHTEST decile of open water — where the foam is, at
            /// whatever exposure this sea happens to have. It is the only way to see what colour the caps
            /// are when the caps themselves sit at 0.02–0.09 luma.</summary>
            public Color FoamTint;
        }

        [Test]
        public void TheWhitecaps_ArePlacedByTheField_NotByAStampSheet()
        {
            RequireAGraphicsDevice();
            Prepare();

            string dir = Path.Combine(Directory.GetCurrentDirectory(), OutRoot, "caps");
            Directory.CreateDirectory(dir);

            var records = new List<CapRecord>();
            var thumbs = new Color[3][][];
            for (int r = 0; r < 3; r++) thumbs[r] = new Color[3][];

            // The two cells the charter names, plus glass as the control that must stay bare.
            //
            // ⚠️ GALE AND GLASS ARE SHOT ON THE OPEN-WATER CONTROL, and the first draft of this test got
            // that wrong: it put all three on Nine Mile Creek's steep stretch, where the beach and the
            // surf band own most of the frame and open-water caps are a detail at the edge. Row 7 is
            // about the caps on the OPEN sea — the "dark shards" were seen on `ww-open-gale-mean-noon` —
            // so the gale is judged where there is nothing else in the picture. The blow keeps the steep
            // stretch, which is the other plate the charter names.
            var cells = new (string key, string label, bool openWater, Weather weather, Tide tide)[]
            {
                ("gale",  "GALE - OPEN WATER",   true,  Weather.Gale,  Tide.Mean),
                ("blow",  "BLOW - NMC STEEP",    false, Weather.Blow,  Tide.Low),
                ("glass", "GLASS - OPEN WATER",  true,  Weather.Glass, Tide.Mean),
            };

            // The third arm is the AGEING dial, not the placement one: the caps take the wake's colour
            // walk at _CapAgeStrength, and its value has to be SHOWN before it can be shipped. The caps
            // sit at 0.02–0.09 luma in the only weathers that have caps, so the eye cannot judge it off a
            // plate — the foamTint column can.
            var arms = new (string key, string label, float texStrength, float agePassthrough)[]
            {
                ("sheet", "PAINTED SHEET (as shipped)",         0.865f, -1f),
                ("field", "THE PROCEDURAL FIELD (ruled)",       0f,     -1f),
                ("aged",  "THE FIELD + AGEING (row 2's candidate)", 0f, 0.75f),
            };
            for (int c = 0; c < cells.Length; c++)
            {
                Stage stage;
                if (cells[c].openWater)
                {
                    stage = BuildWestWater();
                    stage.Name = "ww-open";
                    stage.Aim = WestWaterPlan.RegionWorldCenter;
                }
                else
                {
                    stage = BuildNineMileCreek();
                    stage.Name = "nmc-steep";
                    stage.Aim = AimAtTheSteepestBreak(stage);
                }
                BuildCamera();
                _cam.transform.position = new Vector3(stage.Aim.x, stage.Aim.y, -100f);
                WarmTheShaderCache(stage);

                for (int a = 0; a < arms.Length; a++)
                {
                    string file = $"caps-{cells[c].key}-{arms[a].key}";
                    Color[] ldr = ShootCaps(stage, cells[c].weather, cells[c].tide, arms[a].texStrength,
                                            arms[a].agePassthrough,
                                            Path.Combine(dir, file + ".png"), out CapRecord rec);
                    rec.File = file + ".png";
                    rec.Arm = arms[a].label;
                    rec.Cell = cells[c].key;
                    records.Add(rec);
                    thumbs[a][c] = Thumbnail(ldr);
                }
            }

            string sheetPath = Path.Combine(Directory.GetCurrentDirectory(), OutRoot, "SHEET-caps.png");
            ContactSheet.Write(sheetPath, "THE WHITECAPS - THE PAINTED SHEET vs THE PROCEDURAL FIELD",
                               new[] { "GALE - OPEN WATER", "BLOW - NMC STEEP", "GLASS - OPEN WATER" },
                               new[] { "", "", "" }, new[] { "SHEET", "FIELD", "+AGEING" }, thumbs, ThumbPx);
            File.WriteAllText(Path.Combine(dir, "CAPS.txt"), CapReport(records));
            Debug.Log("[water-plates] the whitecaps\n" + CapReport(records));

            Assert.IsTrue(File.Exists(sheetPath), "the whitecap sheet must be written — the FILE is the evidence");

            CapRecord glassSheet = FindCap(records, "glass", "sheet");
            CapRecord glassField = FindCap(records, "glass", "field");

            // ⭐ THE ONE THING A PLATE CAN SETTLE HERE, and it is worth settling: on a GLASS CALM the
            // switch must be INERT. Zero wave amplitude is zero cap opacity by construction (the wave
            // gate), so whichever source is placing the caps there is nothing to place — and the sacred
            // state must come through this PR untouched. Both arms, same frame, to two thousandths.
            Assert.AreEqual(glassSheet.MeanLumaWet, glassField.MeanLumaWet, 0.002f,
                $"a glass calm must not notice this change at all — the sheet arm read " +
                $"{glassSheet.MeanLumaWet:F4} and the field arm {glassField.MeanLumaWet:F4}. A mirror " +
                "with caps on it is not a mirror.");

            // ⚠️⚠️ THE CAP AGEING (_CapAgeStrength) SHIPS AT 0 — the mechanism, not the value — because
            // this instrument could not show its effect reliably, and a look change nobody can see is not
            // a look change anybody should ship.
            //
            // The caps sit at 0.02–0.09 luma in the only weathers that have caps, so no eye can judge a
            // colour off these plates. The brightest decile of open water is where the foam is at any
            // exposure, and in an ISOLATED run it read exactly as intended — deeper and bluer, red:blue
            // 0.62 vs 0.67 in a gale and 0.35 vs 0.42 in a blow. In the FULL SUITE the same comparison
            // read 0.467 vs 0.470. The direction survived; the magnitude did not. What moved between the
            // runs was _Time: which crests happen to be breaking decides which pixels are in the decile,
            // and that swamps a subtle colour walk on foam this dark.
            //
            // So the ageing is built, gated, and OFF: `capAge01` comes out of the lifecycle's own two
            // ends, the caps compose through the WAKE's ramp and knots, and row 2 — the foam-language
            // unification, which judges wake, surf, fringe and caps together against one palette — turns
            // it on with the rest and judges it where a foam palette can actually be judged. The arm
            // below stays as a reported diagnostic so row 2 inherits the numbers as well as the code.
            CapRecord glassFlat = FindCap(records, "glass", "aged");
            Assert.AreEqual(glassFlat.FoamTint.b, glassField.FoamTint.b, 0.002f,
                "the cap ageing must not touch a sea with no caps in it, at any dial setting");

            // ⚠️⚠️ AND THE COVERAGE NUMBERS ARE REPORTED, NOT ASSERTED — a plate cannot carry that claim,
            // and two drafts of this test proved it rather than assuming it:
            //
            //   · a DISTRIBUTION-relative bar (p50 + half the way to p95) reported 18.65 % coverage on a
            //     GLASS CALM, which wears no caps at all. Every histogram has a bright tail.
            //   · an ABSOLUTE white bar reported 0.00 % in a GALE, where the whole frame including its
            //     foam sits under 0.03 luma. Foam is only white relative to the sea it is on.
            //   · inside the breaker contour the SURF's own whitewater is white by design, so both arms
            //     read 65–83 % there. That is the surf, not caps.
            //   · and the blow cell's mean luma is not even stationary between two shots of the same sea
            //     (0.106 and 0.145 across two runs), so an arm-to-arm difference at this frame size would
            //     be measuring _Time.
            //
            // What the change IS, is asserted where it is exactly true — on the assets and the shader, in
            // WhitecapStampSheetTests.TheProceduralField_PlacesTheCaps_NotTheStampSheet. The 1.35 % vs
            // 9.95 % coverage figures come from the 2026-08-05 measurement of the cap FIELD, which is the
            // instrument that can separate the layers; the charter says not to re-chase it. This sheet is
            // for the owner's eye, which is the acceptance the charter actually asks for.
        }

        /// <summary>One whitecap plate: the sea as published for this cell, with the painted slot's blend
        /// overridden on the property block after the shipped push (the knob-diagnostic pattern), then
        /// measured for how much of the water is wearing foam.</summary>
        Color[] ShootCaps(Stage stage, Weather w, Tide t, float texStrength, float agePassthrough,
                          string path, out CapRecord rec)
        {
            Publish(stage, w, t, Hour.Noon, out _, out Color tint, out _, out _, out _);

            var sr = stage.SeaGo.GetComponent<SpriteRenderer>();
            Material mat = sr.sharedMaterial;
            var block = new MaterialPropertyBlock();
            sr.GetPropertyBlock(block);
            Assert.IsTrue(mat.HasProperty("_WhitecapTexStrength"), "_WhitecapTexStrength must be a water-shader property");
            Assert.IsTrue(mat.HasProperty("_CapAgeStrength"), "_CapAgeStrength must be a water-shader property");
            float restore = block.HasFloat("_WhitecapTexStrength") ? block.GetFloat("_WhitecapTexStrength")
                                                                   : mat.GetFloat("_WhitecapTexStrength");
            float restoreAge = block.HasFloat("_CapAgeStrength") ? block.GetFloat("_CapAgeStrength")
                                                                 : mat.GetFloat("_CapAgeStrength");
            block.SetFloat("_WhitecapTexStrength", texStrength);
            if (agePassthrough >= 0f) block.SetFloat("_CapAgeStrength", agePassthrough);
            sr.SetPropertyBlock(block);

            Color[] ldr = Capture(tint, path);

            block.SetFloat("_WhitecapTexStrength", restore);
            block.SetFloat("_CapAgeStrength", restoreAge);
            sr.SetPropertyBlock(block);

            WetStatistics(stage, _env.WaterLevel, ldr, out _, out float meanLumaWet);
            float outerDepth = Shader.GetGlobalVector(Shader.PropertyToID("_BreakerOuter")).x;
            CapCoverage(stage, _env.WaterLevel, outerDepth, ldr, out float coverage, out Color foamTint);
            rec = new CapRecord { Coverage = coverage, MeanLumaWet = meanLumaWet, FoamTint = foamTint };
            return ldr;
        }

        /// <summary>
        /// How much of the wet frame is wearing FOAM, measured OUTSIDE the breaking water.
        ///
        /// <para><b>Foam is WHITE, and white is <c>min(r, g, b)</c>.</b> The sea is blue even when it is
        /// bright — a lit mirror-calm reads about (0.35, 0.40, 0.45), so its minimum channel stays low —
        /// while foam is near-neutral at the top of the range. A LUMA bar cannot tell those apart and a
        /// distribution-relative bar cannot either: the first draft of this used p50 + half the way to p95
        /// and reported <b>18.65 % coverage on a GLASS CALM</b>, which wears no caps at all. Every
        /// histogram has a bright tail; only the neutral one is foam.</para>
        ///
        /// <para>⚠️ Absolute, and legitimately so — every cell here is NOON, so the day/night tint is ~1
        /// across all of them and the bar is comparing like with like. (This is not PR 6's mistake in
        /// reverse: that was an absolute floor on a tripwire that had to survive the art changing under
        /// it. Here the bar defines what the word "foam" means, and foam is defined by <c>_FoamColor</c>,
        /// which is white by authorship.)</para>
        ///
        /// <para><b>The breaking water is EXCLUDED, not measured.</b> Inside the contour the surf's own
        /// whitewater is white by design, so every pixel there scores as foam whatever the caps are doing
        /// — the first draft reported 65–83 % "cap coverage" in the surf zone on both arms, which is the
        /// surf, not caps. That whitecaps never reach the surf zone was measured at 0.00 % on 2026-08-05
        /// and the charter says do not re-chase it; a plate cannot separate the two layers, so this one
        /// does not pretend to.</para></summary>
        void CapCoverage(Stage stage, float level, float outerDepth, Color[] ldr,
                         out float coverage, out Color foamTint)
        {
            const float WhiteBar = 0.55f;   // min-channel; _FoamColor is ~0.95 white, the lit sea ~0.35
            int capped = 0, wet = 0;
            var open = new List<Color>();
            for (int py = 0; py < ShotPx; py += 2)
            for (int px = 0; px < ShotPx; px += 2)
            {
                float depth = level - stage.Terrain.ElevationAt(PixelToWorld(px, py));
                if (depth <= 0f) continue;                    // dry
                if (depth <= outerDepth) continue;            // breaking water — the surf's foam, not caps
                Color c = ldr[py * ShotPx + px];
                wet++;
                open.Add(c);
                if (Mathf.Min(c.r, Mathf.Min(c.g, c.b)) > WhiteBar) capped++;
            }
            coverage = wet > 0 ? capped / (float)wet : 0f;

            // The BRIGHTEST DECILE of the open water is where the foam is, whatever this sea's exposure.
            // An absolute "is it white" bar cannot find the caps in a gale (the whole frame, foam
            // included, sits under 0.03 luma); a decile can, because foam is always the top of its own
            // sea's range even when that range is dark.
            foamTint = Color.black;
            if (open.Count == 0) return;
            open.Sort((a, b) => (0.299f * a.r + 0.587f * a.g + 0.114f * a.b)
                        .CompareTo(0.299f * b.r + 0.587f * b.g + 0.114f * b.b));
            int from = Mathf.Max(0, (int)(open.Count * 0.9f));
            float r = 0f, g = 0f, bl = 0f;
            for (int i = from; i < open.Count; i++) { r += open[i].r; g += open[i].g; bl += open[i].b; }
            int n = open.Count - from;
            foamTint = new Color(r / n, g / n, bl / n, 1f);
        }

        static CapRecord FindCap(List<CapRecord> records, string cell, string arm)
        {
            foreach (CapRecord r in records)
                if (r.File == $"caps-{cell}-{arm}.png") return r;
            Assert.Fail($"no whitecap plate for {cell} / {arm}");
            return default;
        }

        static string CapReport(List<CapRecord> records)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# The whitecaps at noon — the gale and glass cells on the OPEN-WATER control " +
                          "(nothing but sea in the frame), the blow on NMC's steep stretch. coverage = " +
                          "share of OPEN wet " +
                          "pixels (the breaking water excluded — its whitewater is white by design) whose " +
                          "MIN CHANNEL exceeds 0.55: foam is neutral white, and a lit sea is blue even " +
                          "when it is bright, which is the only way to tell the two apart.");
            sb.AppendLine("plate                        coverage   meanWet   foamTint(rgb, brightest " +
                          "decile of open water)   [arm]");
            foreach (CapRecord r in records)
                sb.AppendLine($"{r.File,-28} {r.Coverage,8:P2} {r.MeanLumaWet,9:F4}   " +
                              $"({r.FoamTint.r:F4},{r.FoamTint.g:F4},{r.FoamTint.b:F4})   [{r.Arm}]");
            return sb.ToString();
        }

        [Test]
        public void TheWeatherLadder_IsTheSimsOwnPairing_AndClimbs()
        {
            // Each weather's wind is the strength the weather model itself would blow to produce that sea
            // state, so a plate never pairs a gale's foam with a zephyr's wind. And the ladder climbs.
            float lastSea = -1f, lastWind = -1f;
            foreach (Weather w in Enum.GetValues(typeof(Weather)))
            {
                float sea = SeaStateFor(w);
                float wind = WindFor(w).magnitude;
                Assert.Greater(sea, lastSea, $"{w}: the sea state must climb");
                Assert.GreaterOrEqual(wind, lastWind, $"{w}: the wind must not fall as the sea rises");
                Assert.AreEqual(sea, WeatherModel.SeaState01(wind), 1e-4f,
                    $"{w}: the wind must round-trip through the sim's own sea-state relation");
                lastSea = sea; lastWind = wind;
            }
            Assert.AreEqual(0f, WindFor(Weather.Glass).magnitude, 1e-6f, "glass is a flat calm");
            Assert.That(WeatherModel.SeaFromWind(WindFor(Weather.Gale).magnitude),
                        Is.GreaterThanOrEqualTo(SeaState.Rough), "a gale plate must read as at least Rough on the canon scale");
        }

        [Test]
        public void ThePlateFont_DrawsEveryGlyphTheSheetsUse()
        {
            // The contact sheets label themselves with a 5x7 bitmap font. A missing glyph would silently
            // draw nothing, so every character the sheet titles and labels can contain must have ink.
            const string used = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-:./+%() ";
            foreach (char c in used)
            {
                if (c == ' ') continue;
                int w = 5 * 2 + 2, h = 7 * 2 + 2;
                var px = new Color[w * h];
                PixelFont.Draw(px, w, h, 1, 1, c.ToString(), 2, Color.white);
                int ink = 0;
                for (int i = 0; i < px.Length; i++) if (px[i].a > 0.5f) ink++;
                Assert.Greater(ink, 0, $"glyph '{c}' has no ink");
            }
            Assert.AreEqual(5 * 2 + 1, PixelFont.Advance(2), "the advance is one glyph plus a one-cell gap");
        }

        // =============================================================================================
        //  The sweep
        // =============================================================================================

        void Prepare()
        {
            // The shipped GameConfig is the settings instance every accessor resolves through: the plates
            // must be of the asset the owner tunes, not of the code defaults.
            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(WaterSceneTemplate.DataConfig + "/GameConfig.asset");
            Assert.IsNotNull(config, "Data/Config/GameConfig.asset must exist — the plates are of the shipped tuning");
            GameServices.Config = config;

            _env = new PlateEnvironment();
            GameServices.Environment = _env;

            // The shipped day/night profile: Resources/DayNightProfile.asset when it exists, else the same
            // CreateDefault() the controller falls back to in the game.
            // ⚠️ The profile is a SHIPPED ASSET as of water-fidelity PR 4, and an asset must not be
            // destroyed in TearDown ("Destroying assets is not permitted to avoid data loss" — an editor
            // ERROR, which the test framework turns into a failure on EVERY test in this file). Only the
            // in-memory fallback is ours to clean up, so remember which one we got.
            _profile = Resources.Load<DayNightProfile>("DayNightProfile");
            _profileIsOurs = _profile == null;
            if (_profileIsOurs) _profile = DayNightProfile.CreateDefault();

            _lamp = new FakeLamp();
            WaterLightBridge.Register(_lamp);

            // The object-reflection target, bound to a CLEAR 1x1 exactly as ReflectionRegistry binds it
            // before its pass has ever run. The water composites that target by its sampled ALPHA, and
            // Unity's grey unbound placeholder carries alpha ~0.5 — a half-mirror smeared across the sea
            // that no player ever sees. No reflector registers in a fixture, so the registry's own
            // fallback never fires here.
            _reflectFallback = new Texture2D(1, 1, TextureFormat.RGBAHalf, false, true)
            {
                name = "PlateReflectFallback", hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp,
            };
            _reflectFallback.SetPixel(0, 0, Color.clear);
            _reflectFallback.Apply(false, true);
            Shader.SetGlobalTexture(Shader.PropertyToID("_HHReflectTex"), _reflectFallback);

            PublishTheNewMoon();

            Assert.IsNotNull(PushUniformsSnap,
                "WaterSurface.PushUniforms() (private, parameterless — the OnValidate/OnEnable snap push) was not " +
                "found by reflection; the sweep needs the shipped push path. Update the fixture to whatever it " +
                "was renamed to.");
        }

        void Sweep(Stage stage)
        {
            string dir = Path.Combine(Directory.GetCurrentDirectory(), OutRoot, stage.Name);
            Directory.CreateDirectory(dir);

            BuildCamera();
            _cam.transform.position = new Vector3(stage.Aim.x, stage.Aim.y, -100f);
            WarmTheShaderCache(stage);

            var records = new List<PlateRecord>();
            var thumbs = new Color[3][][];   // [hour][column]
            for (int h = 0; h < 3; h++) thumbs[h] = new Color[12][];

            Color[] referencePlate = null;
            int column = 0;
            foreach (Weather w in Enum.GetValues(typeof(Weather)))
            foreach (Tide t in Enum.GetValues(typeof(Tide)))
            {
                foreach (Hour h in Enum.GetValues(typeof(Hour)))
                {
                    string file = $"{stage.Name}-{WeatherName[(int)w]}-{TideName[(int)t]}-{HourName[(int)h]}";
                    Color[] ldr = Shoot(stage, w, t, h, Path.Combine(dir, file + ".png"), out PlateRecord rec);
                    rec.File = file + ".png";
                    records.Add(rec);
                    thumbs[(int)h][column] = Thumbnail(ldr);
                    if (w == Weather.Blow && t == Tide.Low && h == Hour.Noon) referencePlate = ldr;
                }
                column++;
            }

            // The sheet: one frame the owner reads at a glance — weather across, tide within each weather,
            // hour down. Written as a file, and it is the FILE that is asserted below.
            string[] columnTop = new string[12];
            string[] columnBottom = new string[12];
            for (int c = 0; c < 12; c++)
            {
                columnTop[c] = c % 3 == 1 ? WeatherName[c / 3].ToUpperInvariant() : "";
                columnBottom[c] = TideName[c % 3].ToUpperInvariant();
            }
            string[] rows = { "NOON", "GOLDEN", "NIGHT+LAMP" };
            string sheetPath = Path.Combine(Directory.GetCurrentDirectory(), OutRoot, $"SHEET-{stage.Name}.png");
            ContactSheet.Write(sheetPath, stage.Title, columnTop, columnBottom, rows, thumbs, ThumbPx);

            WriteManifest(stage, dir, records);
            AssertTheInstrumentIsHonest(stage, records, referencePlate, sheetPath, dir);

            Debug.Log($"[water-plates] {stage.Name}: {records.Count} plates at {stage.Aim} -> {dir}; " +
                      $"sheet {sheetPath}");
        }

        /// <summary>
        /// A cold <c>Library/ShaderCache</c> makes a render read the async-compile placeholder, which has
        /// faked a regression on this repo before. Render throwaway frames until nothing is compiling, at
        /// the blow / spring-low / noon reference, before the first plate is kept.
        /// </summary>
        void WarmTheShaderCache(Stage stage)
        {
            Publish(stage, Weather.Blow, Tide.Low, Hour.Noon, out _, out _, out _, out _, out _);
            int renders = 0;
            for (int i = 0; i < 24; i++)
            {
                _cam.Render();
                renders++;
                if (!ShaderUtil.anythingCompiling) break;
            }
            // And the night variant, which compiles the lamp branch.
            Publish(stage, Weather.Blow, Tide.Low, Hour.Night, out _, out _, out _, out _, out _);
            for (int i = 0; i < 24; i++)
            {
                _cam.Render();
                renders++;
                if (!ShaderUtil.anythingCompiling) break;
            }
            Assert.IsFalse(ShaderUtil.anythingCompiling,
                "the water shader was still compiling after the warm-up budget — a cold cache would fake every plate");
            Debug.Log($"[water-plates] {stage.Name}: shader warm-up took {renders} throwaway renders");
        }

        /// <summary>
        /// Set the whole world for one plate: the environment (weather + tide), the wave field / fetch /
        /// breaker globals the bridge publishes in Play, the hour's day/night globals, the lamp, and then the
        /// shipped <see cref="WaterSurface"/> push that turns the environment into uniforms.
        /// </summary>
        void Publish(Stage stage, Weather w, Tide t, Hour h,
                     out BreakerContour contour, out Color tint, out Vector2 sunDir, out float sunElevation,
                     out float hourOfDay)
        {
            _env.Wind = WindFor(w);
            _env.SeaState01 = SeaStateFor(w);
            _env.WaterLevel = LevelFor(stage, t);

            // The sea, published by hand the way the bridge publishes it (it only ticks in Play): the same
            // trains, the same packing, the same fetch and breaker calls, the same GameConfig instance.
            WaveTrains trains = WaveMath.TrainsFrom(_env.Wind, _env.SeaState01, GameServices.WaveField);
            WaveFieldBridge.PublishGlobals(WaveFieldBridge.Pack(in trains));
            WaveFieldBridge.PublishFetchGlobals(GameServices.WaveFetch, _env.Wind);
            WaveFieldBridge.PublishBreakerGlobals(trains.Dominant, GameServices.WaveFetch, GameServices.Breakers);
            contour = BreakerMath.ContourFor(trains.Dominant, WaveFetch.Envelope01(0f, GameServices.WaveFetch),
                                             GameServices.Breakers);

            // The hour, published the way ADR 0013's controller publishes it (it does not tick in edit mode).
            // Moonless: the moon needs a clock for its phase, and a plate is an instant.
            hourOfDay = HourFor(h, _profile);
            tint = DayNightMath.DayNightTint(hourOfDay, _profile, _env.Visibility, _env.SeaState01);
            sunDir = DayNightMath.SunDirection(hourOfDay, _profile.SunriseHour, _profile.SunsetHour,
                                               _profile.ShadowSouthBias, _profile.ShadowNoonLift);
            sunElevation = DayNightMath.SunElevation(hourOfDay, _profile.SunriseHour, _profile.SunsetHour);
            Shader.SetGlobalColor(Shader.PropertyToID("_DayNightTint"), tint);
            Shader.SetGlobalVector(Shader.PropertyToID("_SunDir"), new Vector4(sunDir.x, sunDir.y, 0f, 0f));
            Shader.SetGlobalFloat(Shader.PropertyToID("_SunElevation"), sunElevation);

            // The searchlight, on at night only, through the shipped bridge AND the singleton beside it.
            PublishTheLamp(stage, h == Hour.Night);

            // The push. The shipped component reads the fake environment and writes every sim-driven
            // uniform, the mood blend and the palette seam onto its own property block — a zero-dt push
            // snaps every eased value to its target, so the plate is the settled sea for this weather.
            var surface = stage.SeaGo.GetComponent<WaterSurface>();
            Assert.IsNotNull(surface, "the stage's Sea must carry the shipped WaterSurface");
            PushUniformsSnap.Invoke(surface, null);
        }

        /// <summary>Put the searchlight in the frame or take it out, the way <c>BoatSpotlight</c> does it:
        /// the bridge's ARRAY (which the water sums its cone weight from) and the SINGLETON globals beside
        /// it (which carry the lamp's colour to every other lit path). Publishing only one of the two is how
        /// a shipped term goes missing from a plate.</summary>
        void PublishTheLamp(Stage stage, bool on)
        {
            WaterLightState state = on ? LampState(stage) : default;
            _lamp.State = state;
            var host = new GameObject("PlateLightBridge") { hideFlags = HideFlags.HideAndDontSave };
            try { host.AddComponent<WaterLightBridge>().PublishFromRegistry(); }
            finally { Object.DestroyImmediate(host); }

            Shader.SetGlobalVector(Shader.PropertyToID("_BoatLightPos"),
                new Vector4(state.LampWorld.x, state.LampWorld.y, on ? state.LampHeightMeters : 0f, 0f));
            Shader.SetGlobalVector(Shader.PropertyToID("_BoatLightDir"),
                new Vector4(state.BeamDir.x, state.BeamDir.y, 0f, 0f));
            Shader.SetGlobalColor(Shader.PropertyToID("_BoatLightColor"), on ? state.Color : Color.clear);
            Shader.SetGlobalVector(Shader.PropertyToID("_BoatLightParams"),
                new Vector4(Mathf.Max(0f, state.Intensity), Mathf.Max(0.01f, state.Range),
                            state.CosHalfAngle, state.CosInnerAngle));
            Shader.SetGlobalVector(Shader.PropertyToID("_BoatLightParams2"),
                new Vector4(state.EdgeSoftness, state.GateThreshold, state.GateSoftness, state.GateFallback));
        }

        WaterLightState LampState(Stage stage) => new WaterLightState
        {
            LampWorld = stage.Aim + Searchlight.Offset,
            LampHeightMeters = Searchlight.HeightMeters,
            BeamDir = Searchlight.BeamDir,
            Color = Searchlight.Colour,
            Intensity = Searchlight.Intensity * Searchlight.WaterStrength,
            Range = Searchlight.Range,
            CosHalfAngle = Mathf.Cos(Searchlight.ConeHalfDeg * Mathf.Deg2Rad),
            CosInnerAngle = Mathf.Cos(Searchlight.ConeHalfDeg * (1f - Searchlight.AngularSoftness) * Mathf.Deg2Rad),
            EdgeSoftness = Searchlight.EdgeSoftness,
            GateThreshold = Searchlight.GateThreshold,
            GateSoftness = Searchlight.GateSoftness,
            GateFallback = Searchlight.GateFallback,
        };

        static float LevelFor(Stage stage, Tide t) => t switch
        {
            Tide.Low => stage.TideMean - stage.TideAmplitude,
            Tide.High => stage.TideMean + stage.TideAmplitude,
            _ => stage.TideMean,
        };

        Color[] Shoot(Stage stage, Weather w, Tide t, Hour h, string path, out PlateRecord rec)
        {
            Publish(stage, w, t, h, out BreakerContour contour, out Color tint, out Vector2 sunDir,
                    out float sunElevation, out float hourOfDay);
            Color[] ldr = Capture(tint, path);

            // The record: everything the plate was pinned to, READ BACK rather than assumed.
            var sr = stage.SeaGo.GetComponent<SpriteRenderer>();
            var block = new MaterialPropertyBlock();
            sr.GetPropertyBlock(block);
            Vector4 outer = Shader.GetGlobalVector(Shader.PropertyToID("_BreakerOuter"));
            Vector4 depths = Shader.GetGlobalVector(Shader.PropertyToID("_BreakerDepths"));
            Vector4 fieldParams = Shader.GetGlobalVector(Shader.PropertyToID("_WaveFieldParams"));
            WetStatistics(stage, _env.WaterLevel, ldr, out float wet, out float meanLumaWet,
                          out float stdLumaWet);

            rec = new PlateRecord
            {
                Weather = w, Tide = t, Hour = h,
                SeaState = _env.SeaState01, WindSpeed = _env.Wind.magnitude, WaterLevel = _env.WaterLevel,
                HourOfDay = hourOfDay, Tint = tint, SunDir = sunDir, SunElevation = sunElevation,
                Breaks = outer.w > 0.5f, BreakDepth = depths.x, OuterDepth = outer.x,
                Chop = block.GetFloat("_Chop"), Roughness = block.GetFloat("_Roughness"),
                Flow = block.GetFloat("_Flow"), PushedLevel = block.GetFloat("_WaterLevel"),
                WaveCount = fieldParams.x,
                LightCount = Shader.GetGlobalFloat(Shader.PropertyToID("_WaterLightCount")),
                WetFraction = wet, MeanLumaWet = meanLumaWet, StdLumaWet = stdLumaWet,
            };
            Assert.IsTrue(rec.Breaks == contour.Breaks,
                "the breaker globals the shader reads must be the contour the C# solved for this plate");
            return ldr;
        }

        /// <summary>Render the world as published, read it back in HDR, apply the day/night multiply, write the
        /// PNG and return the tone-mapped pixels — the one capture path every plate and every diagnostic
        /// shot goes through.</summary>
        Color[] Capture(Color tint, string path)
        {
            _cam.Render();
            _cam.Render();   // the second is read: a cold shader cache has faked a regression here before

            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = _rt;
            var hdr = new Texture2D(ShotPx, ShotPx, TextureFormat.RGBAFloat, false);
            hdr.ReadPixels(new Rect(0, 0, ShotPx, ShotPx), 0, 0);
            hdr.Apply();
            RenderTexture.active = prev;

            // ADR 0013's overlay multiplies the WHOLE composited frame by the tint AFTER the water draws. It
            // is a screen-space pass that does not run in a fixture, so it is applied here — without it a
            // night is a capture of the pre-compensated light values, which is not what any player sees.
            Color[] lit = hdr.GetPixels();
            var ldr = new Color[lit.Length];
            for (int i = 0; i < lit.Length; i++)
                ldr[i] = new Color(Mathf.Clamp01(lit[i].r * tint.r),
                                   Mathf.Clamp01(lit[i].g * tint.g),
                                   Mathf.Clamp01(lit[i].b * tint.b), 1f);
            Object.DestroyImmediate(hdr);

            var tex = new Texture2D(ShotPx, ShotPx, TextureFormat.RGBA32, false);
            tex.SetPixels(ldr);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            return ldr;
        }

        // =============================================================================================
        //  The knob-by-knob diagnostic — which layer makes what the plates show
        // =============================================================================================

        /// <summary>The layers that can be alive on a glass calm at noon — the suspects for the stripes.</summary>
        static readonly string[] GlassKnobs =
        {
            "_ReflectionStrength", "_ReflectionSunStreak", "_SkyReflectionStrength", "_CloudStrength",
            "_SunGlitterStrength", "_SpecAmount", "_SparkleTexStrength", "_SurfaceTexStrength",
            "_FbmStrength", "_SwellReadStrength", "_RippleStrength", "_PaletteGradeStrength",
        };

        /// <summary>The layers alive in a gale — the suspects for the bands and the hard dark shards.</summary>
        static readonly string[] GaleKnobs =
        {
            "_Roughness", "_WhitecapTexStrength", "_CapSalienceStrength", "_WhitecapPeakDensity",
            "_FoamClumpStrength", "_StormFoamLaneStrength", "_ObjectReflectStrength", "_SwellReadStrength",
            "_SwellFaceShade", "_OceanSwellStrength", "_EnvelopeBandStrength", "_DriftLineStrength",
            "_FoamConvergenceStrength", "_RippleStrength", "_PaletteGradeStrength",
        };

        /// <summary>
        /// ⭐ <b>Name the mechanism, never guess it.</b> The open-water control at mean tide and noon, shot
        /// once as shipped and then once per layer with that ONE property zeroed through the property block
        /// after the shipped push. Whatever a plate shows, the row whose zeroing removes it is the layer
        /// that draws it — the "zero the suspects and re-read" method that settled the drift-line probe.
        ///
        /// <para>Three numbers per shot, each on the tone-mapped frame: the mean luma; the standard deviation
        /// of the per-ROW mean (horizontal band contrast); of the per-COLUMN mean (vertical streak contrast);
        /// and the share of pixels that are near-black beside a lit neighbour (a hard dark edge — the shards).
        /// A per-pixel diff would measure the clock; these are structural and survive it.</para>
        ///
        /// <para>Every override is restored after its shot — a property block is sticky, and a mood-eased key
        /// is re-pushed every shot while a material-level key is restored from the material.</para>
        /// </summary>
        [Test]
        public void TheGlassStripesAndTheGaleShards_MeasuredKnobByKnob()
        {
            RequireAGraphicsDevice();
            Prepare();
            Stage stage = BuildWestWater();
            stage.Name = "ww-open";
            stage.Aim = WestWaterPlan.RegionWorldCenter;
            BuildCamera();
            _cam.transform.position = new Vector3(stage.Aim.x, stage.Aim.y, -100f);
            WarmTheShaderCache(stage);

            string dir = Path.Combine(Directory.GetCurrentDirectory(), OutRoot, "diagnostic");
            Directory.CreateDirectory(dir);
            var sb = new StringBuilder();
            sb.AppendLine("# Knob-by-knob: West Water open-water control, mean tide, noon. Each row zeroes ONE " +
                          "property through the property block AFTER the shipped push; the baseline is the " +
                          "plate as shipped. Columns: meanLuma | rowStd (horizontal band contrast) | colStd " +
                          "(vertical streak contrast) | hardDarkEdge% (near-black pixels beside a lit neighbour)");
            Diagnose(stage, Weather.Glass, GlassKnobs, dir, sb);
            Diagnose(stage, Weather.Gale, GaleKnobs, dir, sb);
            File.WriteAllText(Path.Combine(dir, "DIAGNOSTIC.txt"), sb.ToString());
            Debug.Log("[water-plates] diagnostic\n" + sb);
            Assert.IsTrue(File.Exists(Path.Combine(dir, "glass-baseline.png")), "the glass baseline must be written");
            Assert.IsTrue(File.Exists(Path.Combine(dir, "gale-baseline.png")), "the gale baseline must be written");
        }

        void Diagnose(Stage stage, Weather w, string[] knobs, string dir, StringBuilder sb)
        {
            string cond = WeatherName[(int)w];
            sb.AppendLine($"## {cond}");
            Color[] baseline = ShootDiagnostic(stage, w, null, Path.Combine(dir, $"{cond}-baseline.png"));
            sb.AppendLine(DiagnosticRow("baseline (as shipped)", baseline));
            foreach (string knob in knobs)
            {
                Color[] px = ShootDiagnostic(stage, w, knob, Path.Combine(dir, $"{cond}-{knob.TrimStart('_')}-0.png"));
                sb.AppendLine(DiagnosticRow($"{knob} = 0", px));
            }
        }

        Color[] ShootDiagnostic(Stage stage, Weather w, string knob, string path)
        {
            Publish(stage, w, Tide.Mean, Hour.Noon, out _, out Color tint, out _, out _, out _);
            var sr = stage.SeaGo.GetComponent<SpriteRenderer>();
            Material mat = sr.sharedMaterial;
            var block = new MaterialPropertyBlock();
            sr.GetPropertyBlock(block);

            float restore = 0f;
            if (knob != null)
            {
                Assert.IsTrue(mat.HasProperty(knob), $"{knob} is not a property of the water shader");
                // The EFFECTIVE value: the block's where the push wrote one, else the material's — which is
                // exactly the precedence the GPU applies. Restored after the shot, so the next shot starts
                // from the shipped look and not from this override (the sticky-block law).
                restore = block.HasFloat(knob) ? block.GetFloat(knob) : mat.GetFloat(knob);
                block.SetFloat(knob, 0f);
                sr.SetPropertyBlock(block);
            }

            Color[] ldr = Capture(tint, path);

            if (knob != null)
            {
                block.SetFloat(knob, restore);
                sr.SetPropertyBlock(block);
            }
            return ldr;
        }

        static string DiagnosticRow(string label, Color[] px)
        {
            var rowMean = new double[ShotPx];
            var colMean = new double[ShotPx];
            double sum = 0;
            for (int y = 0; y < ShotPx; y++)
            for (int x = 0; x < ShotPx; x++)
            {
                Color c = px[y * ShotPx + x];
                double l = 0.299 * c.r + 0.587 * c.g + 0.114 * c.b;
                rowMean[y] += l; colMean[x] += l; sum += l;
            }
            for (int i = 0; i < ShotPx; i++) { rowMean[i] /= ShotPx; colMean[i] /= ShotPx; }
            double mean = sum / px.Length;
            double rowStd = Std(rowMean), colStd = Std(colMean);

            int hard = 0;
            for (int y = 1; y < ShotPx - 1; y++)
            for (int x = 1; x < ShotPx - 1; x++)
            {
                if (Luma(px[y * ShotPx + x]) >= 0.006f) continue;
                if (Luma(px[y * ShotPx + x + 1]) > 0.04f || Luma(px[y * ShotPx + x - 1]) > 0.04f
                    || Luma(px[(y + 1) * ShotPx + x]) > 0.04f || Luma(px[(y - 1) * ShotPx + x]) > 0.04f)
                    hard++;
            }
            return $"{label,-34} | {mean:F4} | {rowStd:F4} | {colStd:F4} | {100.0 * hard / px.Length:F3}%";
        }

        static float Luma(Color c) => 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;

        static double Std(double[] v)
        {
            double m = 0; foreach (double x in v) m += x; m /= v.Length;
            double s = 0; foreach (double x in v) s += (x - m) * (x - m);
            return Math.Sqrt(s / v.Length);
        }

        /// <summary>Wet fraction of the frame from the TERRAIN under each pixel (never from the picture),
        /// and the mean luma over those wet pixels.</summary>
        void WetStatistics(Stage stage, float level, Color[] ldr, out float wetFraction, out float meanLumaWet)
            => WetStatistics(stage, level, ldr, out wetFraction, out meanLumaWet, out _);

        /// <summary>…and the STANDARD DEVIATION of that luma, which is the honest answer to "did
        /// anything draw".
        ///
        /// <para>⚠️ The mean was doing that job and it is not up to it. A floor on BRIGHTNESS conflates
        /// "the render is black" with "this sea is dark" — and the sea legitimately got darker when the
        /// 2026-09-03 swell-scale ruling lengthened the waves ×2.8, because a fixed 40 m frame then holds
        /// 2.8× fewer lit crests. `nmc-steep-blow-high-noon` came in at 0.0197 against a 0.02 floor: a
        /// perfectly good picture of a dark blow at high water, failing a tripwire meant to catch a dead
        /// render. A black frame has NO VARIANCE; a dark sea has plenty, and no amount of art direction
        /// can take that away without the picture genuinely being gone.</para></summary>
        void WetStatistics(Stage stage, float level, Color[] ldr, out float wetFraction,
                           out float meanLumaWet, out float stdLumaWet)
        {
            int wet = 0, n = 0;
            double luma = 0, lumaSq = 0;
            for (int py = 0; py < ShotPx; py += 4)
            for (int px = 0; px < ShotPx; px += 4)
            {
                n++;
                if (level - stage.Terrain.ElevationAt(PixelToWorld(px, py)) <= 0f) continue;
                wet++;
                Color c = ldr[py * ShotPx + px];
                double l = 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;
                luma += l;
                lumaSq += l * l;
            }
            wetFraction = n > 0 ? wet / (float)n : 0f;
            double mean = wet > 0 ? luma / wet : 0.0;
            meanLumaWet = (float)mean;
            stdLumaWet = wet > 0 ? (float)System.Math.Sqrt(System.Math.Max(0.0, lumaSq / wet - mean * mean)) : 0f;
        }

        /// <summary>Orthographic pixel to world, with the read-back's bottom-left origin.</summary>
        Vector2 PixelToWorld(int px, int py)
        {
            Vector3 cam = _cam.transform.position;
            float half = FrameMetres * 0.5f;
            return new Vector2(cam.x + (px / (float)(ShotPx - 1) * 2f - 1f) * half,
                               cam.y + (py / (float)(ShotPx - 1) * 2f - 1f) * half);
        }

        static Color[] Thumbnail(Color[] plate)
        {
            const int k = ShotPx / ThumbPx;   // 3
            var thumb = new Color[ThumbPx * ThumbPx];
            for (int y = 0; y < ThumbPx; y++)
            for (int x = 0; x < ThumbPx; x++)
            {
                float r = 0, g = 0, b = 0;
                for (int dy = 0; dy < k; dy++)
                for (int dx = 0; dx < k; dx++)
                {
                    Color c = plate[(y * k + dy) * ShotPx + x * k + dx];
                    r += c.r; g += c.g; b += c.b;
                }
                float inv = 1f / (k * k);
                thumb[y * ThumbPx + x] = new Color(r * inv, g * inv, b * inv, 1f);
            }
            return thumb;
        }

        void WriteManifest(Stage stage, string dir, List<PlateRecord> records)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"# {stage.Title}");
            sb.AppendLine($"# viewpoint {stage.Name}: camera at ({stage.Aim.x:F2}, {stage.Aim.y:F2}), " +
                          $"{FrameMetres} m across, {ShotPx} px ({ShotPx / FrameMetres:F0} px/m), " +
                          $"tide mean {stage.TideMean:F2} amplitude {stage.TideAmplitude:F2}");
            sb.AppendLine("# the flat Universal2D pass (DisplacedWaterSurface ticks only in Play): the fragment is " +
                          "the displaced pass's fragment, the vertex lift is absent");
            sb.AppendLine("# tidal current 0; visibility 1; NEW moon below the horizon (MoonCycle is Play-only; " +
                          "the shader's unset fallback would be a full moon); _HHReflectTex bound clear (no " +
                          "reflector in a fixture); wind heading " +
                          $"({WindHeading.x:F3}, {WindHeading.y:F3}); lamp = BoatSpotlight defaults x water strength " +
                          $"{Searchlight.WaterStrength} at aim + ({Searchlight.Offset.x}, {Searchlight.Offset.y}) throwing +x");
            sb.AppendLine("file | weather sea01 wind_mps | tide level_m pushed_level | hour tint sunDir sunElev | " +
                          "breaks breakDepth outerDepth | _Chop _Roughness _Flow | trains lights | wet% lumaWet stdWet");
            foreach (PlateRecord r in records)
            {
                sb.AppendLine(
                    $"{r.File} | {WeatherName[(int)r.Weather]} {r.SeaState:F2} {r.WindSpeed:F2} | " +
                    $"{TideName[(int)r.Tide]} {r.WaterLevel:F2} {r.PushedLevel:F2} | " +
                    $"{HourName[(int)r.Hour]} {r.HourOfDay:F2} ({r.Tint.r:F3},{r.Tint.g:F3},{r.Tint.b:F3}) " +
                    $"({r.SunDir.x:F2},{r.SunDir.y:F2}) {r.SunElevation:F2} | " +
                    $"{(r.Breaks ? "yes" : "no")} {r.BreakDepth:F2} {r.OuterDepth:F2} | " +
                    $"{r.Chop:F3} {r.Roughness:F3} {r.Flow:F3} | {r.WaveCount:F0} {r.LightCount:F0} | " +
                    $"{r.WetFraction:P1} {r.MeanLumaWet:F3} {r.StdLumaWet:F3}");
            }
            File.WriteAllText(Path.Combine(dir, "MANIFEST.txt"), sb.ToString());
        }

        /// <summary>
        /// The instrument's own honesty checks — on the mechanism, never on a per-pixel diff (that measures
        /// the clock). The eye judges the look; these make sure what it judges is what it thinks it is.
        /// </summary>
        void AssertTheInstrumentIsHonest(Stage stage, List<PlateRecord> records, Color[] referencePlate,
                                         string sheetPath, string dir)
        {
            Assert.AreEqual(36, records.Count, "every cell of the matrix must have a plate");
            foreach (PlateRecord r in records)
                Assert.IsTrue(File.Exists(Path.Combine(dir, r.File)), $"{r.File} was not written");

            // 1. The pinned uniforms took: the pushed water level is the tide asked for, the sea state
            //    reached _Chop (glass pushes 0), and a lamp is in the array only at night.
            foreach (PlateRecord r in records)
            {
                Assert.AreEqual(r.WaterLevel, r.PushedLevel, 1e-3f,
                    $"{r.File}: the shipped push did not carry the tide ({r.PushedLevel} vs {r.WaterLevel})");
                if (r.Weather == Weather.Glass)
                    Assert.AreEqual(0f, r.Chop, 1e-4f, $"{r.File}: glass must push _Chop 0 — the mirror is sacred");
                else
                    Assert.Greater(r.Chop, 0f, $"{r.File}: a working sea must push a non-zero _Chop");
                Assert.AreEqual(r.Hour == Hour.Night ? 1f : 0f, r.LightCount, 1e-4f,
                    $"{r.File}: the searchlight must be in the array at night and only at night");
                Assert.AreEqual(r.Weather != Weather.Glass, r.Breaks,
                    $"{r.File}: a working sea must publish a breaking contour and glass must not");
            }

            // 2. The frame is of what it claims: a coast plate holds coast AND water; the control holds
            //    nothing but water. Judged from the terrain under the pixels, never from the picture.
            PlateRecord reference = records.Find(r => r.Weather == Weather.Blow && r.Tide == Tide.Low && r.Hour == Hour.Noon);
            if (stage.OpenWater)
                Assert.Greater(reference.WetFraction, 0.999f, "the control must be open water edge to edge");
            else
            {
                Assert.Greater(reference.WetFraction, 0.25f, $"{stage.Name}: the frame must hold real water at spring low");
                PlateRecord high = records.Find(r => r.Weather == Weather.Blow && r.Tide == Tide.High && r.Hour == Hour.Noon);
                Assert.GreaterOrEqual(high.WetFraction, reference.WetFraction,
                    "the tide must flood the frame between spring low and spring high");
            }

            // 3. Something drew. A noon plate of a lit sea is not black; a night plate is dark but the lamp
            //    lights something. (Luma is the only picture statistic used, and only as a floor.)
            foreach (PlateRecord r in records)
            {
                if (r.Hour == Hour.Noon)
                {
                    // ⚠️ STRUCTURE, not brightness — see WetStatistics. This floor used to be
                    // MeanLumaWet > 0.02, and the 2026-09-03 swell-scale ruling walked a real plate
                    // under it (nmc-steep-blow-high-noon, 0.0197): 2.8× longer waves put 2.8× fewer lit
                    // crests in a fixed frame, so a working picture of a dark blow tripped a tripwire
                    // meant for a dead render. Variance cannot be argued with the same way.
                    Assert.Greater(r.StdLumaWet, 0.005f,
                        $"{r.File}: the noon sea has no STRUCTURE — a black or single-colour frame is " +
                        "the only thing that reads this flat, so nothing drew");
                    Assert.Greater(r.MeanLumaWet, 0.005f,
                        $"{r.File}: the noon sea photographed black ({r.MeanLumaWet:F4} mean luma). " +
                        "How DARK a lit sea should be is register row 6's question, not this tripwire's; " +
                        "this floor only says a picture exists.");
                }
            }

            // 4. ⭐ THE PUBLISHED FILE, not the buffer. Re-load the reference plate from disk and check its
            //    orientation against the terrain: whichever half the bathymetry says is wetter must be the
            //    half the FILE shows as sea. A pack shipped upside down with every buffer assertion green is
            //    exactly what #688 did; this reads the artifact the eye reads.
            if (!stage.OpenWater)
            {
                string refPath = Path.Combine(dir, reference.File);
                var loaded = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                Assert.IsTrue(loaded.LoadImage(File.ReadAllBytes(refPath)), "the reference plate must decode");
                Assert.AreEqual(ShotPx, loaded.width);
                Assert.AreEqual(ShotPx, loaded.height);
                Color32[] filePx = loaded.GetPixels32();
                Object.DestroyImmediate(loaded);
                AssertOrientationAgrees(stage, reference.WaterLevel, filePx);
            }

            // 5. The sheet exists, decodes, and is the size the composer promised.
            var sheet = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            Assert.IsTrue(sheet.LoadImage(File.ReadAllBytes(sheetPath)), "the contact sheet must decode");
            Assert.AreEqual(ContactSheet.Width(12, ThumbPx), sheet.width, "sheet width");
            Assert.AreEqual(ContactSheet.Height(3, ThumbPx), sheet.height, "sheet height");
            Object.DestroyImmediate(sheet);
        }

        void AssertOrientationAgrees(Stage stage, float level, Color32[] filePx)
        {
            // Terrain: wet share of the top half vs the bottom half, and of the left vs the right.
            float wetTop = 0, wetBottom = 0, wetLeft = 0, wetRight = 0;
            int nHalf = 0;
            for (int py = 0; py < ShotPx; py += 4)
            for (int px = 0; px < ShotPx; px += 4)
            {
                bool wet = level - stage.Terrain.ElevationAt(PixelToWorld(px, py)) > 0f;
                if (py >= ShotPx / 2) { if (wet) wetTop++; } else { if (wet) wetBottom++; }
                if (px >= ShotPx / 2) { if (wet) wetRight++; } else { if (wet) wetLeft++; }
                nHalf++;
            }
            nHalf /= 2;
            wetTop /= nHalf; wetBottom /= nHalf; wetLeft /= nHalf; wetRight /= nHalf;

            // The file: "sea-ish" share (blue over red, not foam-white) of the same halves. LoadImage
            // returns bottom-left origin like the readback, so row 0 is the frame's bottom on both sides.
            float seaTop = 0, seaBottom = 0, seaLeft = 0, seaRight = 0;
            for (int py = 0; py < ShotPx; py += 4)
            for (int px = 0; px < ShotPx; px += 4)
            {
                Color32 c = filePx[py * ShotPx + px];
                bool sea = c.b > c.r + 8 && c.r < 225;
                if (py >= ShotPx / 2) { if (sea) seaTop++; } else { if (sea) seaBottom++; }
                if (px >= ShotPx / 2) { if (sea) seaRight++; } else { if (sea) seaLeft++; }
            }
            seaTop /= nHalf; seaBottom /= nHalf; seaLeft /= nHalf; seaRight /= nHalf;

            float vertical = wetTop - wetBottom, horizontal = wetRight - wetLeft;
            Debug.Log($"[water-plates] {stage.Name} orientation: terrain wet top/bottom {wetTop:P0}/{wetBottom:P0} " +
                      $"left/right {wetLeft:P0}/{wetRight:P0}; file sea top/bottom {seaTop:P0}/{seaBottom:P0} " +
                      $"left/right {seaLeft:P0}/{seaRight:P0}");

            if (Mathf.Abs(vertical) >= Mathf.Abs(horizontal) && Mathf.Abs(vertical) > 0.15f)
                Assert.AreEqual(Mathf.Sign(vertical), Mathf.Sign(seaTop - seaBottom),
                    "the published plate's sea is on the wrong side vertically — the file is upside down or mirrored");
            else if (Mathf.Abs(horizontal) > 0.15f)
                Assert.AreEqual(Mathf.Sign(horizontal), Mathf.Sign(seaRight - seaLeft),
                    "the published plate's sea is on the wrong side horizontally — the file is mirrored");
            else
                Debug.LogWarning($"[water-plates] {stage.Name}: the frame is too evenly wet to adjudicate " +
                                 "orientation from terrain alone (both asymmetries under 15 points)");
        }

        // =============================================================================================
        //  The scenes — the region as it ships, through the builders' own public helpers
        // =============================================================================================

        void BuildCamera()
        {
            _camGo = new GameObject("PlateCam");
            _cam = _camGo.AddComponent<Camera>();
            _cam.orthographic = true;
            _cam.orthographicSize = FrameMetres * 0.5f;
            _cam.nearClipPlane = 1f;
            _cam.farClipPlane = 400f;
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = new Color(0.02f, 0.03f, 0.05f, 1f);
            _cam.allowMSAA = false;
            // HDR, and it is load-bearing: the water PRE-COMPENSATES its light content by dividing by
            // _DayNightTint so ADR 0013's downstream multiply cancels it; at the deepest night that is a
            // ~45x boost and an LDR target clips every lit pixel to white before the readback can bring it
            // back (#691's first capture was a sheet of cream).
            _rt = new RenderTexture(ShotPx, ShotPx, 24, RenderTextureFormat.ARGBHalf) { filterMode = FilterMode.Point };
            _cam.targetTexture = _rt;
        }

        Stage BuildNineMileCreek()
        {
            var terrainGo = new GameObject("TidalTerrain");
            _built.Add(terrainGo);
            var terrain = terrainGo.AddComponent<MainlandTidalTerrain>();
            NineMileCreekBuilder.ConfigureNineMileCreekTerrain(terrain);
            // ⚠ Registered by hand: the terrain components are not [ExecuteAlways], so OnEnable never fires
            // in edit mode and the accessor stays null — whereupon WaterSurface bakes a flat height field
            // and the sea draws opaque over everything. Cost the #680 pass a capture.
            GameServices.TidalTerrain = terrain;

            var region = AssetDatabase.LoadAssetAtPath<RegionDef>(WaterSceneTemplate.RegionAssetPathFor("NineMileCreek"));
            Assert.IsNotNull(region, "Data/Regions/NineMileCreek.asset must exist to size the ground");
            Assert.That(NineMileCreekBuilder.BuildSplatGround(region), Is.True,
                "the painted ground must build — without it the capture is of black land");
            Remember("TerrainSplat");

            GameObject sea = BuildLandRegionSea(NineMileCreekBuilder.NineMileCreekSeaCenter,
                                                NineMileCreekBuilder.NineMileCreekSeaSize,
                                                NineMileCreekBuilder.NineMileCreekHeightResolution,
                                                NineMileCreekBuilder.NineMileCreekHeightMin,
                                                NineMileCreekBuilder.NineMileCreekHeightMax,
                                                terrain.MaxShoreGradient());
            return new Stage
            {
                Terrain = terrain, SeaGo = sea,
                TideMean = NineMileCreekMainland.TideMean, TideAmplitude = NineMileCreekMainland.TideAmplitude,
            };
        }

        Stage BuildStPeters()
        {
            var terrainGo = new GameObject("TidalTerrain");
            _built.Add(terrainGo);
            var terrain = terrainGo.AddComponent<TidalTerrain>();
            StPetersBuilder.ConfigureTidalTerrain(terrain);
            GameServices.TidalTerrain = terrain;

            BuildStPetersGround();

            GameObject sea = BuildLandRegionSea(StPetersBuilder.RegionWorldCenter, StPetersBuilder.RegionWorldSize,
                                                StPetersBuilder.WaterHeightBakeResolution,
                                                StPetersBuilder.DeepHarbourElevation, StPetersBuilder.IslandElevation,
                                                StPetersBuilder.ShoreGradient);
            return new Stage
            {
                Terrain = terrain, SeaGo = sea,
                TideMean = StPetersBuilder.TideMean, TideAmplitude = StPetersBuilder.TideAmplitude,
            };
        }

        /// <summary>
        /// St Peters' painted ground, wired exactly as <c>StPetersBuilder.Build()</c> wires it (that block is
        /// inline in the builder and the builder itself refuses batch mode — ADR 0019's overwrite guard).
        /// Created INACTIVE, configured, then activated: TerrainSplatSurface is [ExecuteAlways] and builds
        /// its quad on the first OnEnable from whatever extent it is carrying at that instant.
        /// </summary>
        void BuildStPetersGround()
        {
            var splatGo = new GameObject("TerrainSplat");
            _built.Add(splatGo);
            splatGo.SetActive(false);
            var splat = splatGo.AddComponent<TerrainSplatSurface>();
            splat.Configure(StPetersBuilder.RegionWorldCenter, StPetersBuilder.RegionWorldSize,
                AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Materials/TerrainSplat.mat"),
                TerrainSplatSurface.DefaultSortingOrder);

            var heightMap = AssetDatabase.LoadAssetAtPath<PaintedHeightMap>(
                "Assets/_Project/Data/Terrain/" + TerrainPaintTool.StPetersSeabedName + ".asset");
            Assert.IsNotNull(heightMap,
                "Data/Terrain/StPetersSeabed.asset (the owner's painted seabed, committed) must exist — the " +
                "ground quad clips itself empty without it and the arrival plates would show black land");
            splat.ConfigureHeightMap(heightMap.HeightTexture, heightMap.MinElevation, heightMap.MaxElevation);
            splat.ConfigureBands(
                StPetersShoreMap.PaintFloorElevation, StPetersShoreMap.RippleFloorElevation,
                StPetersShoreMap.SandFloorElevation, StPetersShoreMap.MarramFloorElevation,
                StPetersShoreMap.GrassFloorElevation, StPetersShoreMap.ShingleFloorElevation,
                StPetersShoreMap.BandWiggleMetres, StPetersShoreMap.BandWiggleScale,
                StPetersShoreMap.BandDetailMetres, StPetersShoreMap.BandDetailScale);
            splat.ConfigureWeatherSector(StPetersBuilder.IslandCenter,
                StPetersBuilder.IslandRadius / StPetersBuilder.IslandRadiusY,
                StPetersShoreMap.WeatherCoastFacing, StPetersShoreMap.SectorFeather);
            splat.ConfigureSandbar(StPetersBuilder.SandbarFrom, StPetersBuilder.SandbarTo,
                StPetersBuilder.SandbarHalfWidth,
                StPetersShoreMap.BarSpineHalfWidth, StPetersShoreMap.BarSpineFloorElevation);
            HiddenHarbours.Art.Editor.TerrainTexArrayBuilder.Build();
            splat.ConfigureDetail(
                AssetDatabase.LoadAssetAtPath<Texture2DArray>(HiddenHarbours.Art.Editor.TerrainTexArrayBuilder.Array256Path),
                AssetDatabase.LoadAssetAtPath<Texture2DArray>(HiddenHarbours.Art.Editor.TerrainTexArrayBuilder.Array512Path));
            var splatMaps = new Texture2D[TerrainSplatBrush.TextureCount];
            for (int i = 0; i < splatMaps.Length; i++)
                splatMaps[i] = AssetDatabase.LoadAssetAtPath<Texture2D>(TerrainSplatAssets.PathOf(i));
            splat.ConfigureSplat(splatMaps[0], splatMaps[1], splatMaps[2], splatMaps[3], splatMaps[4]);
            splatGo.SetActive(true);
        }

        Stage BuildWestWater()
        {
            WaterScenePlan plan = WestWaterPlan.Plan;
            RectTidalTerrain terrain = WaterSceneTemplate.AddTerrain(plan);
            Remember("TidalTerrain");
            GameServices.TidalTerrain = terrain;

            var config = AssetDatabase.LoadAssetAtPath<GameConfig>(WaterSceneTemplate.DataConfig + "/GameConfig.asset");
            GameObject sea = WaterSceneTemplate.AddSea(plan, config, null);
            Remember("Sea");
            Assert.IsNotNull(sea.GetComponent<WaterSurface>(), "Water.mat must exist for the open-water control");
            // AddSea creates the Sea ACTIVE and configures the surface afterwards, so its first bake ran at
            // the component's default extent. One toggle re-runs OnEnable at the plan's extent.
            sea.SetActive(false);
            sea.SetActive(true);

            return new Stage
            {
                Terrain = terrain, SeaGo = sea, OpenWater = true,
                TideMean = WestWaterPlan.TideMean, TideAmplitude = WestWaterPlan.TideAmplitude,
            };
        }

        /// <summary>The #680 / #691 sea: a quad carrying Water.mat and the shipped WaterSurface, wired through
        /// the same shared template call both land builders use.</summary>
        GameObject BuildLandRegionSea(Vector2 centre, Vector2 size, int resolution, float heightMin, float heightMax,
                                      float maxShoreGradient)
        {
            var waterMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Materials/Water.mat");
            Assert.IsNotNull(waterMat, "Water.mat must exist — there is nothing to photograph without it");

            var seaGo = new GameObject("Sea");
            _built.Add(seaGo);
            seaGo.SetActive(false);
            seaGo.transform.position = new Vector3(centre.x, centre.y, 0f);
            var sr = seaGo.AddComponent<SpriteRenderer>();
            sr.sortingOrder = SortingBands.Sea;
            sr.sharedMaterial = waterMat;
            var seaTile = WaterSceneTemplate.LoadSpriteAny("Assets/_Project/Art/Tilesets/Water/SeaTile.png");
            if (seaTile != null) sr.sprite = seaTile;
            WaterSceneTemplate.ConfigureSeaPlane(sr, size);
            seaGo.AddComponent<WaterSurface>();
            WaterSceneTemplate.ConfigureLandRegionWater(seaGo, centre, size, resolution, heightMin, heightMax,
                                                        maxShoreGradient);
            seaGo.SetActive(true);
            return seaGo;
        }

        void Remember(string rootName)
        {
            var go = GameObject.Find(rootName);
            if (go != null && !_built.Contains(go)) _built.Add(go);
        }

        // =============================================================================================
        //  The aims — derived from the shipped terrain at the reference sea, and logged
        // =============================================================================================

        /// <summary>The reference sea the aims are solved on: the blow at spring low, where #680 measured
        /// the surf covering 5.09 % of its frame.</summary>
        static BreakerContour ReferenceContour(out WaveTrains trains)
        {
            trains = WaveMath.TrainsFrom(WindFor(Weather.Blow), SeaStateFor(Weather.Blow), GameServices.WaveField);
            return BreakerMath.ContourFor(trains.Dominant, WaveFetch.Envelope01(0f, GameServices.WaveFetch),
                                          GameServices.Breakers);
        }

        /// <summary>#680's plunge scan: the highest surf-similarity number on the break contour — Nine Mile
        /// Creek's one steep stretch, where the lip and barrel are drawn.</summary>
        Vector2 AimAtTheSteepestBreak(Stage stage)
        {
            BreakerContour contour = ReferenceContour(out WaveTrains trains);
            Assert.IsTrue(contour.Breaks, "the reference sea must break somewhere");
            float level = stage.TideMean - stage.TideAmplitude;
            var settings = GameServices.Breakers;
            float h0 = 2f * trains.Dominant.Amplitude;
            Vector2 centre = NineMileCreekBuilder.NineMileCreekSeaCenter;
            Vector2 size = NineMileCreekBuilder.NineMileCreekSeaSize;

            float bestXi = -1f;
            Vector2 bestAt = centre;
            const int steps = 160;
            for (int iy = 0; iy <= steps; iy++)
            for (int ix = 0; ix <= steps; ix++)
            {
                var at = new Vector2(centre.x + size.x * (ix / (float)steps - 0.5f),
                                     centre.y + size.y * (iy / (float)steps - 0.5f));
                if (!FrameInsideTheSea(at, centre, size)) continue;
                float depth = level - stage.Terrain.ElevationAt(at);
                if (depth <= 0f || Mathf.Abs(depth - contour.BreakDepths.x) > 0.08f) continue;
                float sx = BreakerMath.BedSlopeAlong(at, Vector2.right, settings.SlopeProbeMeters, stage.Terrain);
                float sy = BreakerMath.BedSlopeAlong(at, Vector2.up, settings.SlopeProbeMeters, stage.Terrain);
                float xi = BreakerMath.Iribarren(Mathf.Sqrt(sx * sx + sy * sy), h0, trains.Dominant.Wavelength);
                if (xi > bestXi) { bestXi = xi; bestAt = at; }
            }
            Assert.Greater(bestXi, 0f, "no point of the break contour lies inside the sea rect");
            Debug.Log($"[water-plates] steepest break-contour point: xi = {bestXi:F3} " +
                      $"({BreakerMath.ClassFor(bestXi, in settings)}, plunging weight " +
                      $"{BreakerMath.PlungingWeight01(bestXi, in settings):F3}) at {bestAt}; break depth " +
                      $"{contour.BreakDepths.x:F2} m at spring low {level:F2} m");
            return bestAt;
        }

        /// <summary>#680's surf-zone finder: the point on the break contour from which the surf RUNS
        /// furthest shoreward — the widest spilling beach on the coast.</summary>
        Vector2 AimAtTheLongestSurfRun(Stage stage)
        {
            BreakerContour contour = ReferenceContour(out _);
            Assert.IsTrue(contour.Breaks, "the reference sea must break somewhere");
            float level = stage.TideMean - stage.TideAmplitude;
            Vector2 centre = NineMileCreekBuilder.NineMileCreekSeaCenter;
            Vector2 size = NineMileCreekBuilder.NineMileCreekSeaSize;

            Vector2 best = centre;
            float bestScore = -1f;
            int atBreakDepth = 0;
            const int steps = 128;
            for (int iy = 0; iy <= steps; iy++)
            for (int ix = 0; ix <= steps; ix++)
            {
                var p = new Vector2(centre.x + size.x * (ix / (float)steps - 0.5f),
                                    centre.y + size.y * (iy / (float)steps - 0.5f));
                if (!FrameInsideTheSea(p, centre, size)) continue;
                float depth = level - stage.Terrain.ElevationAt(p);
                if (depth <= 0f || Mathf.Abs(depth - contour.BreakDepths.x) > 0.06f) continue;
                atBreakDepth++;

                float run = 0f;
                Vector2 walk = p;
                for (int i = 0; i < 60; i++)
                {
                    Vector2 grad = ShoreGradient(stage.Terrain, walk);
                    if (grad.sqrMagnitude < 1e-8f) break;
                    walk += grad;
                    if (level - stage.Terrain.ElevationAt(walk) <= 0f) break;
                    run += 1f;
                }
                if (run > bestScore) { bestScore = run; best = p; }
            }
            Assert.Greater(atBreakDepth, 0, "no water in the region sits at the break depth with a whole frame of sea around it");

            // Bias the aim SHOREWARD along the run so the frame holds the whole surf zone AND the beach it
            // runs onto: centred on the break line, the first draft's 23 m run put the sand just outside a
            // 20 m half-frame and photographed a frame that was 100 % water — the surf with nothing to
            // break against. Inside the sea rect still, or the frame would show the plane's edge.
            Vector2 shoreward = ShoreGradient(stage.Terrain, best);
            Vector2 aim = best + shoreward * Mathf.Min(0.45f * bestScore, FrameMetres * 0.3f);
            if (!FrameInsideTheSea(aim, centre, size)) aim = best;
            Debug.Log($"[water-plates] longest surf run: {bestScore:F0} m shoreward from {best}; aimed at {aim}; " +
                      $"break depth {contour.BreakDepths.x:F2} m at spring low {level:F2} m");
            return aim;
        }

        static bool FrameInsideTheSea(Vector2 at, Vector2 centre, Vector2 size)
        {
            float half = FrameMetres * 0.5f;
            return Mathf.Abs(at.x - centre.x) + half <= size.x * 0.5f
                && Mathf.Abs(at.y - centre.y) + half <= size.y * 0.5f;
        }

        static Vector2 ShoreGradient(ITidalTerrain terrain, Vector2 at)
        {
            const float h = 1f;
            float ex = terrain.ElevationAt(at + new Vector2(h, 0f)) - terrain.ElevationAt(at - new Vector2(h, 0f));
            float ey = terrain.ElevationAt(at + new Vector2(0f, h)) - terrain.ElevationAt(at - new Vector2(0f, h));
            var g = new Vector2(ex, ey);
            return g.sqrMagnitude > 1e-10f ? g.normalized : Vector2.zero;
        }

        // =============================================================================================
        //  The contact sheet and its font
        // =============================================================================================

        /// <summary>A grid of thumbnails with a title band, two rows of column labels and a row-label
        /// gutter. Pure pixel work, so the layout is testable headless.</summary>
        internal static class ContactSheet
        {
            const int TitleBand = 40;
            const int ColumnBand = 44;
            const int RowGutter = 150;
            const int Gutter = 4;
            const int Scale = 2;
            static readonly Color Paper = new Color(0.10f, 0.10f, 0.11f, 1f);
            static readonly Color Ink = new Color(0.92f, 0.90f, 0.82f, 1f);
            static readonly Color Rule = new Color(0.45f, 0.42f, 0.35f, 1f);

            public static int Width(int columns, int thumb) => RowGutter + columns * (thumb + Gutter);
            public static int Height(int rows, int thumb) => TitleBand + ColumnBand + rows * (thumb + Gutter);

            public static void Write(string path, string title, string[] columnTop, string[] columnBottom,
                                     string[] rowLabels, Color[][][] thumbs, int thumb)
            {
                int columns = columnTop.Length, rows = rowLabels.Length;
                int w = Width(columns, thumb), h = Height(rows, thumb);
                var px = new Color[w * h];
                for (int i = 0; i < px.Length; i++) px[i] = Paper;

                // Pixel rows run bottom-up (Texture2D convention); the title sits at the TOP of the image.
                PixelFont.Draw(px, w, h, 8, h - TitleBand + 10, title, Scale, Ink);
                for (int c = 0; c < columns; c++)
                {
                    int x0 = RowGutter + c * (thumb + Gutter);
                    PixelFont.Draw(px, w, h, x0 + 4, h - TitleBand - 18, columnTop[c], Scale, Ink);
                    PixelFont.Draw(px, w, h, x0 + 4, h - TitleBand - ColumnBand + 6, columnBottom[c], Scale, Ink);
                    if (c % 3 == 0)
                        for (int y = 0; y < h - TitleBand; y++) px[y * w + x0 - Gutter / 2] = Rule;
                }
                for (int r = 0; r < rows; r++)
                {
                    int yTop = h - TitleBand - ColumnBand - r * (thumb + Gutter);   // this row's top edge
                    PixelFont.Draw(px, w, h, 8, yTop - thumb / 2 - 7, rowLabels[r], Scale, Ink);
                    for (int c = 0; c < columns; c++)
                    {
                        Color[] t = thumbs[r][c];
                        if (t == null) continue;
                        int x0 = RowGutter + c * (thumb + Gutter);
                        int y0 = yTop - thumb;
                        for (int y = 0; y < thumb; y++)
                        for (int x = 0; x < thumb; x++)
                            px[(y0 + y) * w + x0 + x] = t[y * thumb + x];
                    }
                }

                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                tex.SetPixels(px);
                tex.Apply();
                File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
            }
        }

        /// <summary>A 5×7 bitmap font — capitals, digits and a little punctuation — so the sheets can label
        /// themselves without a font asset or a text renderer.</summary>
        internal static class PixelFont
        {
            const int GlyphW = 5, GlyphH = 7;
            public static int Advance(int scale) => (GlyphW + 1) * scale - (scale - 1);

            static readonly Dictionary<char, string[]> Glyphs = Build();

            static Dictionary<char, string[]> Build()
            {
                var d = new Dictionary<char, string[]>();
                void G(char c, string rows) => d[c] = rows.Split('|');
                G('A', ".###.|#...#|#...#|#####|#...#|#...#|#...#");
                G('B', "####.|#...#|#...#|####.|#...#|#...#|####.");
                G('C', ".###.|#...#|#....|#....|#....|#...#|.###.");
                G('D', "####.|#...#|#...#|#...#|#...#|#...#|####.");
                G('E', "#####|#....|#....|####.|#....|#....|#####");
                G('F', "#####|#....|#....|####.|#....|#....|#....");
                G('G', ".###.|#...#|#....|#.###|#...#|#...#|.####");
                G('H', "#...#|#...#|#...#|#####|#...#|#...#|#...#");
                G('I', ".###.|..#..|..#..|..#..|..#..|..#..|.###.");
                G('J', "..###|...#.|...#.|...#.|#..#.|#..#.|.##..");
                G('K', "#...#|#..#.|#.#..|##...|#.#..|#..#.|#...#");
                G('L', "#....|#....|#....|#....|#....|#....|#####");
                G('M', "#...#|##.##|#.#.#|#.#.#|#...#|#...#|#...#");
                G('N', "#...#|##..#|#.#.#|#..##|#...#|#...#|#...#");
                G('O', ".###.|#...#|#...#|#...#|#...#|#...#|.###.");
                G('P', "####.|#...#|#...#|####.|#....|#....|#....");
                G('Q', ".###.|#...#|#...#|#...#|#.#.#|#..#.|.##.#");
                G('R', "####.|#...#|#...#|####.|#.#..|#..#.|#...#");
                G('S', ".####|#....|#....|.###.|....#|....#|####.");
                G('T', "#####|..#..|..#..|..#..|..#..|..#..|..#..");
                G('U', "#...#|#...#|#...#|#...#|#...#|#...#|.###.");
                G('V', "#...#|#...#|#...#|#...#|#...#|.#.#.|..#..");
                G('W', "#...#|#...#|#...#|#.#.#|#.#.#|##.##|#...#");
                G('X', "#...#|#...#|.#.#.|..#..|.#.#.|#...#|#...#");
                G('Y', "#...#|#...#|.#.#.|..#..|..#..|..#..|..#..");
                G('Z', "#####|....#|...#.|..#..|.#...|#....|#####");
                G('0', ".###.|#...#|#..##|#.#.#|##..#|#...#|.###.");
                G('1', "..#..|.##..|..#..|..#..|..#..|..#..|.###.");
                G('2', ".###.|#...#|....#|...#.|..#..|.#...|#####");
                G('3', "#####|...#.|..#..|...#.|....#|#...#|.###.");
                G('4', "...#.|..##.|.#.#.|#..#.|#####|...#.|...#.");
                G('5', "#####|#....|####.|....#|....#|#...#|.###.");
                G('6', "..##.|.#...|#....|####.|#...#|#...#|.###.");
                G('7', "#####|....#|...#.|..#..|.#...|.#...|.#...");
                G('8', ".###.|#...#|#...#|.###.|#...#|#...#|.###.");
                G('9', ".###.|#...#|#...#|.####|....#|...#.|.##..");
                G('-', ".....|.....|.....|#####|.....|.....|.....");
                G(':', ".....|..#..|..#..|.....|..#..|..#..|.....");
                G('.', ".....|.....|.....|.....|.....|.##..|.##..");
                G('/', "....#|....#|...#.|..#..|.#...|#....|#....");
                G('+', ".....|..#..|..#..|#####|..#..|..#..|.....");
                G('%', "#...#|....#|...#.|..#..|.#...|#....|#...#");
                G('(', "...#.|..#..|.#...|.#...|.#...|..#..|...#.");
                G(')', ".#...|..#..|...#.|...#.|...#.|..#..|.#...");
                G(' ', ".....|.....|.....|.....|.....|.....|.....");
                return d;
            }

            /// <summary>Draw <paramref name="text"/> with its baseline-bottom at (<paramref name="x"/>,
            /// <paramref name="y"/>) in bottom-left pixel coordinates. Unknown characters draw as a box so a
            /// missing glyph is visible rather than silent.</summary>
            public static void Draw(Color[] px, int w, int h, int x, int y, string text, int scale, Color ink)
            {
                if (string.IsNullOrEmpty(text)) return;
                int cx = x;
                foreach (char raw in text)
                {
                    char c = char.ToUpperInvariant(raw);
                    if (!Glyphs.TryGetValue(c, out string[] rows))
                        rows = new[] { "#####", "#...#", "#...#", "#...#", "#...#", "#...#", "#####" };
                    for (int gy = 0; gy < GlyphH; gy++)
                    {
                        string row = rows[gy];
                        for (int gx = 0; gx < GlyphW; gx++)
                        {
                            if (row[gx] != '#') continue;
                            int x0 = cx + gx * scale;
                            int y0 = y + (GlyphH - 1 - gy) * scale;   // row 0 of the glyph is its TOP
                            for (int sy = 0; sy < scale; sy++)
                            for (int sx = 0; sx < scale; sx++)
                            {
                                int xx = x0 + sx, yy = y0 + sy;
                                if (xx < 0 || yy < 0 || xx >= w || yy >= h) continue;
                                px[yy * w + xx] = ink;
                            }
                        }
                    }
                    cx += Advance(scale);
                }
            }
        }
    }
}
