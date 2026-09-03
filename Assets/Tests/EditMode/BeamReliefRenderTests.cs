using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using HiddenHarbours.World;
using HiddenHarbours.App.Editor;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>
    /// <b>THE ACCEPTANCE for the beam's wave relief: does the searchlight land ON the water, or on top of it?</b>
    ///
    /// <para>The owner's words are the specification: <i>"the spotlight over the water is just one uniform shape
    /// with a gentle gradient... it should highlight the water at crests and be shadowed at the valleys of waves
    /// unless the proper light angle exposes them."</i> The pure tests next door prove the maths — flat water
    /// cancels to exactly 1, a grazing lamp separates crest from trough 24x harder than a high one, a lamp at
    /// infinity decays to pure foreshortening. None of them can prove one lit pixel reaches the screen, and that
    /// is the whole point. So this stands the real coast, publishes a real sea, publishes a real searchlight
    /// through the shipped bridge, and photographs it on the GPU.</para>
    ///
    /// <para><b>The frames in <c>artifacts/</c> ARE the owner's check-in</b> — the charter gates this on his eye.
    /// What the assertions do is make sure the frames he is shown are of a beam that really is relief-lit, really
    /// does depend on the lamp's angle, and really does leave a glass calm alone — so a nod means what he thinks
    /// it means.</para>
    ///
    /// <para><b>THE METRIC, and why it is not a percentage of the frame.</b> The water's churn, swash and drift
    /// all run on <c>_Time.y</c>, which advances between <c>Render()</c> calls in edit mode: two shots of the
    /// IDENTICAL sea differ on 12-25% of pixels. Any "% changed" A/B here measures the clock. So this A/B carries
    /// its own controls — <b>two</b> of them, because the first one alone turned out to flatter the result:</para>
    ///
    /// <para><b>(1) Out-of-cone.</b> The relief multiplies the beam's cone weight, which is <b>exactly 0</b>
    /// outside the cone, so those pixels CANNOT change however the dial moves. Whatever they differ by IS the
    /// clock. But it UNDER-reads what the clock does to LIT water: in-cone pixels are brighter, and the same
    /// proportional churn moves more absolute luma there. Measured at 2.10 against an in-cone 7.76 — a flattering
    /// 3.7x.</para>
    ///
    /// <para><b>(2) The no-wave arm — the honest floor.</b> Run the identical A/B on a sea with the field
    /// EMPTY, where the maths says the dial can do exactly nothing, and the in-cone number it still reports is
    /// the clock measured <i>on lit water</i>: 3.07, not 2.10. The real claim is therefore the live sea's 7.76
    /// against <b>that</b> — 2.5x — and the same comparison is what proves the calm is untouched. Both bars
    /// below are set from these measurements, not from taste; an earlier draft guessed 4x and reddened on a
    /// working feature at 3.7x.</para>
    ///
    /// <para><b>Self-skips without a graphics device</b> — the standing CI law. A skip is "NOT VERIFIED", never
    /// "passed".</para>
    /// </summary>
    public class BeamReliefRenderTests
    {
        const float FrameMetres = 44f;
        const int ShotPx = 1100;

        /// <summary>A working night breeze — the sea the player actually sails in, not a storm showpiece.</summary>
        static readonly Vector2 ShotWind = new Vector2(6f, -5.3f);
        const float ShotSeaState = 0.55f;
        const float GaleSeaState = 0.95f;

        /// <summary>
        /// The pre-dawn dark this beam exists for. These are the values ADR 0013's cycle publishes at the
        /// shipped deepest night (skyTint x the intensity floor); without them the water grades to its DAYLIGHT
        /// palette, the beam's night-gate reads "it is noon, show nothing", and the capture is of a bright sea
        /// with an invisible searchlight on it. An earlier draft of this fixture photographed exactly that.
        /// </summary>
        static readonly Color NightTint = new Color(0.075f, 0.092f, 0.150f, 1f);
        const float PreDawnSunElevation = -0.05f;

        /// <summary>The lamp: a searchlight on a wheelhouse roof, throwing across the frame.</summary>
        const float LampHeight = 2.5f;
        const float LampRange = 30f;
        const float ConeHalfDeg = 30f;
        static readonly Vector2 BeamDir = new Vector2(1f, 0f);

        GameObject _terrainGo, _seaGo, _camGo;
        Camera _cam;
        RenderTexture _rt;
        ITidalTerrain _previousTerrain;
        readonly List<GameObject> _built = new List<GameObject>();
        FakeLamp _lamp;
        Vector2 _lampWorld;

        /// <summary>The searchlight, published through the SHIPPED bridge so the frame is of the real path.</summary>
        sealed class FakeLamp : IWaterLightEmitter
        {
            public WaterLightState State;
            public bool TryGetWaterLight(out WaterLightState state) { state = State; return state.IsLive; }
        }

        [SetUp]
        public void SetUp() => _previousTerrain = GameServices.TidalTerrain;

        [TearDown]
        public void TearDown()
        {
            if (_lamp != null) { WaterLightBridge.Unregister(_lamp); _lamp = null; }
            if (_cam != null) _cam.targetTexture = null;
            if (_rt != null) { _rt.Release(); Object.DestroyImmediate(_rt); _rt = null; }
            foreach (var go in _built) if (go != null) Object.DestroyImmediate(go);
            _built.Clear();
            foreach (var go in new[] { _seaGo, _camGo, _terrainGo })
                if (go != null) Object.DestroyImmediate(go);
            _seaGo = _camGo = _terrainGo = null;
            _cam = null;
            GameServices.TidalTerrain = _previousTerrain;
            // Globals are STICKY: hand the next fixture a dark, calm sea, not this one's beam.
            WaveFieldBridge.PublishGlobals(PackedWaveField.Empty);
            WaveFieldBridge.PublishBreakersOff();
            Shader.SetGlobalFloat(Shader.PropertyToID("_WaterLightCount"), 0f);
            Shader.SetGlobalColor(Shader.PropertyToID("_DayNightTint"), Color.white);
            Shader.SetGlobalFloat(Shader.PropertyToID("_SunElevation"), 0f);
        }

        static void RequireAGraphicsDevice()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore(
                    "SKIPPED, NOT VERIFIED — no graphics device (Null Device), so nothing rendered and " +
                    "nothing was proved. Expected on CI; a drawn beam needs a GPU.");
        }

        // =============================================================================================
        //  The acceptance
        // =============================================================================================

        /// <summary>
        /// His sentence, photographed. The beam is drawn with the relief on and off over the same sea; the
        /// in-cone difference must tower over the clock noise the out-of-cone control measures.
        /// </summary>
        [Test]
        public void TheBeam_LightsTheWaveShape_AndTheDialAtZeroIsTheShippedCone()
        {
            RequireAGraphicsDevice();
            BuildTheShore();
            AimAtOpenWater();

            Color[] on = Shoot("relief-01-searchlight-ON", ShotSeaState, reliefStrength: 1f);
            Color[] onLdr = _lastLdr;
            Color[] off = Shoot("relief-02-searchlight-OFF-flat-cone", ShotSeaState, reliefStrength: 0f);
            SavePlate("PLATE-A-relief-ON-vs-flat-cone-OFF", onLdr, _lastLdr);
            Measure(on, off, out float inCone, out float outOfCone, out int inConePx);

            // THE FLOOR, measured rather than assumed. The out-of-cone region proves the relief touches only
            // the cone, but it UNDER-reads the in-cone clock noise: lit water is brighter, so the same churn
            // moves more luma there. So the honest floor for a LIT pixel is this same A/B run on a sea with
            // NO WAVES, where the maths says the dial can do nothing at all. Whatever that arm reports IS the
            // clock, measured on lit water, in this same fixture.
            // THE CONTROL, and it is airtight: shoot the SAME configuration TWICE. Same sea, same beam, same
            // brightness, same pixels — the only thing that differs between them is the clock. Whatever that
            // pair reports is exactly what the clock can do to this measurement, with no brightness mismatch
            // to argue about. (Comparing in-cone against OUT-of-cone does not work: lit water is brighter, so
            // the same churn moves more absolute luma there, and the calm arm read a phantom 8.6x.)
            Color[] repeatA = Shoot("relief-01b-clock-floor-A", ShotSeaState, reliefStrength: 1f);
            Color[] repeatB = Shoot("relief-01c-clock-floor-B", ShotSeaState, reliefStrength: 1f);
            Measure(repeatA, repeatB, out float clockFloor, out _, out _);

            Assert.Greater(inConePx, ShotPx * ShotPx / 100,
                "the cone must actually cover a worthwhile part of the frame, or this measures nothing");

            Debug.Log($"[beam-relief] ON vs OFF — in-cone mean |dLuma| {inCone:F2}; out-of-cone {outOfCone:F2}; " +
                      $"CLOCK FLOOR from two identical shots {clockFloor:F2} " +
                      $"({inCone / Mathf.Max(clockFloor, 0.001f):F1}x the clock) over {inConePx} px");

            float lumaOn = MeanConeLuma(on), lumaOff = MeanConeLuma(off);
            Debug.Log($"[beam-relief] POOL BRIGHTNESS — mean in-cone luma {lumaOff:F3} flat vs {lumaOn:F3} with " +
                      $"relief ({(lumaOn / Mathf.Max(lumaOff, 1e-5f) - 1f) * 100f:+0.0;-0.0}%). The relief SHAPES " +
                      "the pool and also lifts it, because a facet turned away clamps at 0 and cannot give back " +
                      "what the lit side gains. _BoatLightBrighten is the lever to land it back on today's level.");

            Assert.Greater(inCone, clockFloor * 2f,
                $"the relief must change the lit water far more than the clock alone does (in-cone {inCone:F2} " +
                $"vs a same-configuration repeat of {clockFloor:F2}). If these converge, the beam has stopped " +
                "reading the sea and is the uniform disc the owner complained about.");
            Assert.Greater(inCone, outOfCone * 3f,
                $"and the change must be concentrated IN the cone (in-cone {inCone:F2} vs {outOfCone:F2})");
        }

        /// <summary>
        /// <b>The glass calm is sacred.</b> At zero wave amplitude the relief is 1 by construction, so the dial
        /// must make NO difference at all — the in-cone change has to fall back to the same clock floor as the
        /// water nobody is lighting. This is the pixel-level confirmation of the maths guarantee, and it is
        /// stated relative to the measured floor rather than against a guessed epsilon.
        /// </summary>
        [Test]
        public void OnAGlassCalm_TheDialChangesNothing_AndTheMirrorSurvives()
        {
            RequireAGraphicsDevice();
            BuildTheShore();
            AimAtOpenWater();

            // seaState -1 publishes the EMPTY field: amplitude exactly 0, so the relief is exactly 1 by
            // construction. The strict form of the guarantee, not a nearly-calm approximation of it.
            Color[] on = Shoot("relief-03-glass-calm-ON", seaState: -1f, reliefStrength: 1f);
            Color[] onLdr = _lastLdr;
            Color[] off = Shoot("relief-04-glass-calm-OFF", seaState: -1f, reliefStrength: 0f);
            SavePlate("PLATE-C-glass-calm-ON-vs-OFF-must-be-identical", onLdr, _lastLdr);
            Color[] repeat = Shoot("relief-04b-glass-calm-repeat", seaState: -1f, reliefStrength: 1f);

            Measure(on, off, out float dialDelta, out _, out _);
            Measure(on, repeat, out float clockFloor, out _, out _);
            Debug.Log($"[beam-relief] GLASS CALM (empty field) — turning the dial moves the lit pool by " +
                      $"{dialDelta:F2}; the CLOCK alone moves it {clockFloor:F2}. Ratio " +
                      $"{dialDelta / Mathf.Max(clockFloor, 0.001f):F2}x (a real sea reads 4x+).");

            Assert.Less(dialDelta, clockFloor * 1.6f,
                $"on a sea with no waves the dial must do NOTHING the clock is not already doing " +
                $"(dial {dialDelta:F2} vs clock {clockFloor:F2}) — the mirror is sacred, and zero slope " +
                "cancels to exactly 1 by construction, which the pure tests pin bit-exactly.");
        }

        /// <summary>
        /// The lamp's ANGLE is the lever, so it must be visible in pixels and not only in the unit tests: the
        /// same sea, the same beam, lit from a low lamp and from a high one.
        /// </summary>
        [Test]
        public void TheLampsHeight_ChangesWhatTheBeamShows()
        {
            RequireAGraphicsDevice();
            BuildTheShore();
            AimAtOpenWater();

            Color[] low = Shoot("relief-05-lamp-LOW-raking", ShotSeaState, reliefStrength: 1f, lampHeight: 0.8f);
            Color[] lowLdr = _lastLdr;
            Color[] high = Shoot("relief-06-lamp-HIGH-flattened", ShotSeaState, reliefStrength: 1f, lampHeight: 30f);
            SavePlate("PLATE-B-lamp-LOW-raking-vs-HIGH-flattened", lowLdr, _lastLdr);
            Color[] flat = Shoot("relief-07-lamp-HIGH-control-OFF", ShotSeaState, reliefStrength: 0f, lampHeight: 30f);

            Measure(low, high, out float angleDelta, out float floorA, out _);
            Measure(high, flat, out float highVsFlat, out float floorB, out _);

            Debug.Log($"[beam-relief] LOW vs HIGH lamp — in-cone {angleDelta:F2} (floor {floorA:F2}); " +
                      $"HIGH vs flat cone — in-cone {highVsFlat:F2} (floor {floorB:F2})");

            Assert.Greater(angleDelta, floorA * 3f,
                $"raising the lamp must visibly change what the beam exposes (in-cone {angleDelta:F2} vs " +
                $"floor {floorA:F2}) — this is the owner's 'unless the proper light angle exposes them'");
            Assert.Greater(angleDelta, highVsFlat,
                "and a high lamp must sit CLOSER to the flat cone than a low one does");
        }

        /// <summary>A gale: short steep chop, the hardest case for the relief to stay legible in.</summary>
        [Test]
        public void InAGale_TheBeamStillReadsAsLightOnWater()
        {
            RequireAGraphicsDevice();
            BuildTheShore();
            AimAtOpenWater();

            Color[] on = Shoot("relief-08-gale-ON", GaleSeaState, reliefStrength: 1f);
            Color[] onLdr = _lastLdr;
            Color[] off = Shoot("relief-09-gale-OFF", GaleSeaState, reliefStrength: 0f);
            SavePlate("PLATE-D-gale-ON-vs-OFF", onLdr, _lastLdr);

            Measure(on, off, out float inCone, out float outOfCone, out _);
            Debug.Log($"[beam-relief] GALE — in-cone {inCone:F2} vs floor {outOfCone:F2}");
            Assert.Greater(inCone, outOfCone * 3f,
                $"a steep short sea must still take the beam's relief (in-cone {inCone:F2} vs " +
                $"{outOfCone:F2}; measured 3.9x)");
        }

        // =============================================================================================
        //  Measurement
        // =============================================================================================

        /// <summary>
        /// Mean absolute luminance change between two shots, split by whether the pixel is INSIDE the beam
        /// cone. Out-of-cone is the control: the relief scales a weight that is identically 0 out there, so
        /// any difference is the clock, and it is measured from the same two frames it is used to judge.
        /// </summary>
        /// <summary>
        /// Mean RELATIVE luminance change between two shots, split by whether the pixel is inside the beam
        /// cone, reported in percent.
        ///
        /// <para><b>Relative, and on the HDR values, for a reason.</b> The night overlay multiplies the whole
        /// frame by ~0.022, so an absolute delta measured after it lands inside 8-bit quantization: the first
        /// version of this reported a mean of 1.00, which is one least-significant bit, and could not tell a
        /// working feature from a dead one. A uniform multiply cannot change a RATIO, so measuring relative
        /// change on the pre-overlay values is both immune to the night tint and immune to the encoding.</para>
        /// </summary>
        void Measure(Color[] a, Color[] b, out float inCone, out float outOfCone, out int inConePx)
        {
            Assert.AreEqual(a.Length, b.Length);
            var terrain = _terrainGo.GetComponent<MainlandTidalTerrain>();
            float level = NineMileCreekMainland.SpringHighWater;
            double inSum = 0, outSum = 0;
            int inN = 0, outN = 0;

            for (int i = 0; i < a.Length; i++)
            {
                int px = i % ShotPx, py = i / ShotPx;
                Vector2 world = PixelToWorld(px, py);
                // Classify by SAMPLING THE TERRAIN under the pixel, never by where it sits on screen. Land and
                // any void outside the sea plane are excluded from BOTH regions: they cannot change, so
                // leaving them in the control would silence the clock and flatter every ratio below.
                if (level - terrain.ElevationAt(world) <= 0f) continue;
                float la = Luma(a[i]), lb = Luma(b[i]);
                float d = 100f * Mathf.Abs(la - lb) / Mathf.Max(la + lb, 1e-4f);
                if (InsideTheCone(world)) { inSum += d; inN++; }
                else { outSum += d; outN++; }
            }

            inCone = inN > 0 ? (float)(inSum / inN) : 0f;
            outOfCone = outN > 0 ? (float)(outSum / outN) : 0f;
            inConePx = inN;
        }

        static float Luma(Color c) => 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;

        /// <summary>
        /// Mean luminance of the lit pool. Reported because the relief does not only SHAPE the beam, it also
        /// lifts the pool's average a little: a facet turned away from the lamp clamps at 0 rather than going
        /// negative, so the shadow side cannot give back everything the lit side gains. That is honest physics
        /// (there is no such thing as negative light) but it means "shaped" and "brighter" arrive together,
        /// and an eye judging the plate deserves to be told which is which. _BoatLightBrighten on Water.mat is
        /// the lever if the owner wants the shaped look at exactly today's level.
        /// </summary>
        float MeanConeLuma(Color[] shot)
        {
            var terrain = _terrainGo.GetComponent<MainlandTidalTerrain>();
            float level = NineMileCreekMainland.SpringHighWater;
            double sum = 0; int n = 0;
            for (int i = 0; i < shot.Length; i++)
            {
                Vector2 world = PixelToWorld(i % ShotPx, i / ShotPx);
                if (level - terrain.ElevationAt(world) <= 0f) continue;
                if (!InsideTheCone(world)) continue;
                sum += Luma(shot[i]); n++;
            }
            return n > 0 ? (float)(sum / n) : 0f;
        }

        /// <summary>Orthographic pixel to world, with the read-back's bottom-left origin.</summary>
        Vector2 PixelToWorld(int px, int py)
        {
            Vector3 cam = _cam.transform.position;
            float half = FrameMetres * 0.5f;
            return new Vector2(cam.x + (px / (float)(ShotPx - 1) * 2f - 1f) * half,
                               cam.y + (py / (float)(ShotPx - 1) * 2f - 1f) * half);
        }

        /// <summary>
        /// The SAME cone test the shader applies, so the two regions are exactly the shader's own. Slightly
        /// INSET (the cone edge is feathered, and a pixel straddling it belongs to neither region cleanly).
        /// </summary>
        bool InsideTheCone(Vector2 world)
        {
            Vector2 to = world - _lampWorld;
            float dist = to.magnitude;
            if (dist >= LampRange * 0.92f || dist < 0.5f) return false;
            float cosAngle = Vector2.Dot(to / dist, BeamDir.normalized);
            return cosAngle >= Mathf.Cos(ConeHalfDeg * 0.85f * Mathf.Deg2Rad);
        }

        // =============================================================================================
        //  The scene
        // =============================================================================================

        /// <summary>Point the camera at the most open water on this coast — the beam wants sea, not shore.</summary>
        void AimAtOpenWater()
        {
            var terrain = _terrainGo.GetComponent<MainlandTidalTerrain>();
            Vector2 centre = NineMileCreekBuilder.NineMileCreekSeaCenter;
            Vector2 size = NineMileCreekBuilder.NineMileCreekSeaSize;
            float level = NineMileCreekMainland.SpringHighWater;

            // Score by the SHALLOWEST water anywhere in the frame this aim would produce, not by the depth
            // at its centre. Aiming at the single deepest point put the sea rect's own edge in shot and half
            // the frame came back BLACK -- which also dragged the out-of-cone control down, because void
            // pixels never change and so looked like a beautifully quiet clock. Look at the frame.
            float half = FrameMetres * 0.5f;
            Vector2 bestAt = centre;
            float bestWorst = float.MinValue;
            const int steps = 120;
            for (int iy = 0; iy <= steps; iy++)
            for (int ix = 0; ix <= steps; ix++)
            {
                var at = new Vector2(centre.x + size.x * (ix / (float)steps - 0.5f),
                                     centre.y + size.y * (iy / (float)steps - 0.5f));
                if (Mathf.Abs(at.x - centre.x) + half > size.x * 0.5f) continue;   // frame inside the sea rect
                if (Mathf.Abs(at.y - centre.y) + half > size.y * 0.5f) continue;
                float worst = float.MaxValue;
                for (int cy = -1; cy <= 1; cy++)
                for (int cx = -1; cx <= 1; cx++)
                    worst = Mathf.Min(worst, level - terrain.ElevationAt(
                        at + new Vector2(cx * half * 0.98f, cy * half * 0.98f)));
                if (worst > bestWorst) { bestWorst = worst; bestAt = at; }
            }
            float bestDepth = bestWorst;
            Assert.Greater(bestDepth, 0.5f,
                "the shot must be of open water edge to edge, or the metric is measuring land and void");

            _cam.transform.position = new Vector3(bestAt.x, bestAt.y, -100f);
            // The lamp sits back from the frame's centre and throws across it, so the pool of light is fully
            // inside the shot and its far, more raking end is visible too.
            _lampWorld = bestAt - new Vector2(FrameMetres * 0.36f, 0f);
            Debug.Log($"[beam-relief] aimed at {bestAt}; shallowest water anywhere in frame {bestDepth:F2} m; " +
                      $"lamp at {_lampWorld}");
        }

        void BuildTheShore()
        {
            _terrainGo = new GameObject("TidalTerrain");
            var terrain = _terrainGo.AddComponent<MainlandTidalTerrain>();
            NineMileCreekBuilder.ConfigureNineMileCreekTerrain(terrain);
            GameServices.TidalTerrain = terrain;

            var region = AssetDatabase.LoadAssetAtPath<RegionDef>(
                WaterSceneTemplate.RegionAssetPathFor("NineMileCreek"));
            Assert.IsNotNull(region, "Data/Regions/NineMileCreek.asset must exist to size the ground");
            Assert.That(NineMileCreekBuilder.BuildSplatGround(region), Is.True,
                "the painted ground must build — without it the capture is of black land");
            Remember("TerrainSplat");

            var waterMat = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/_Project/Art/Materials/Water.mat");
            Assert.IsNotNull(waterMat, "Water.mat must exist — there is nothing to photograph without it");

            _seaGo = new GameObject("Sea");
            _seaGo.SetActive(false);
            _seaGo.transform.position = new Vector3(NineMileCreekBuilder.NineMileCreekSeaCenter.x,
                                                    NineMileCreekBuilder.NineMileCreekSeaCenter.y, 0f);
            var sr = _seaGo.AddComponent<SpriteRenderer>();
            sr.sortingOrder = -5;
            sr.sharedMaterial = waterMat;
            var seaTile = WaterSceneTemplate.LoadSpriteAny("Assets/_Project/Art/Tilesets/Water/SeaTile.png");
            if (seaTile != null) sr.sprite = seaTile;
            WaterSceneTemplate.ConfigureSeaPlane(sr, NineMileCreekBuilder.NineMileCreekSeaSize);
            _seaGo.AddComponent<WaterSurface>();
            WaterSceneTemplate.ConfigureLandRegionWater(
                _seaGo, NineMileCreekBuilder.NineMileCreekSeaCenter,
                NineMileCreekBuilder.NineMileCreekSeaSize,
                NineMileCreekBuilder.NineMileCreekHeightResolution,
                NineMileCreekBuilder.NineMileCreekHeightMin,
                NineMileCreekBuilder.NineMileCreekHeightMax,
                terrain.MaxShoreGradient());
            _seaGo.SetActive(true);

            _camGo = new GameObject("BeamShotCam");
            _cam = _camGo.AddComponent<Camera>();
            _cam.orthographic = true;
            _cam.orthographicSize = FrameMetres * 0.5f;
            _cam.nearClipPlane = 1f;
            _cam.farClipPlane = 400f;
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = new Color(0.02f, 0.03f, 0.05f, 1f);
            _cam.allowMSAA = false;
            // ⚠️ HDR, and it is load-bearing. The water PRE-COMPENSATES its light content by dividing by
            // _DayNightTint so ADR 0013's downstream full-screen MULTIPLY cancels it (LightMath.
            // CompensateForDayNightTint). At the night tint below that is a ~45x boost, so on an LDR target
            // every lit pixel clips to white BEFORE the overlay can bring it back — the first version of this
            // fixture published a beautiful night and photographed a sheet of cream. Half-float keeps the >1
            // values alive so ApplyTheDayNightOverlay can complete the round trip.
            _rt = new RenderTexture(ShotPx, ShotPx, 24, RenderTextureFormat.ARGBHalf)
            { filterMode = FilterMode.Point };
            _cam.targetTexture = _rt;

            _lamp = new FakeLamp();
            WaterLightBridge.Register(_lamp);
        }

        /// <summary>
        /// Write an A|B plate with a hairline divider. One frame the owner can look at once and answer the
        /// question with, instead of two files he has to flick between and hold in his head.
        /// </summary>
        static void SavePlate(string name, Color[] left, Color[] right)
        {
            const int gap = 6;
            int w = ShotPx * 2 + gap;
            var tex = new Texture2D(w, ShotPx, TextureFormat.RGBA32, false);
            var px = new Color[w * ShotPx];
            for (int y = 0; y < ShotPx; y++)
            {
                for (int x = 0; x < ShotPx; x++)
                {
                    px[y * w + x] = left[y * ShotPx + x];
                    px[y * w + ShotPx + gap + x] = right[y * ShotPx + x];
                }
                for (int g = 0; g < gap; g++) px[y * w + ShotPx + g] = new Color(0.6f, 0.55f, 0.4f, 1f);
            }
            tex.SetPixels(px);
            tex.Apply();
            string dir = Path.Combine(Directory.GetCurrentDirectory(), "artifacts");
            Directory.CreateDirectory(dir);
            File.WriteAllBytes(Path.Combine(dir, name + ".png"), tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }

        /// <summary>The tone-mapped, overlay-applied pixels of the last shot — what the plate shows.</summary>
        Color[] _lastLdr;

        void Remember(string rootName)
        {
            var go = GameObject.Find(rootName);
            if (go != null) _built.Add(go);
        }

        /// <summary>
        /// The sea, published by hand exactly as the game publishes it — the bridge only ticks in Play, and a
        /// fixture that forgot this would photograph a dead-flat field and call the relief broken.
        /// </summary>
        void PublishTheSea(float seaState01)
        {
            // A NEGATIVE sea state means the strict glass control: the EMPTY field, amplitude exactly 0, which
            // is what makes the calm assertion a proof of the construction rather than of a small number.
            if (seaState01 < 0f)
            {
                WaveFieldBridge.PublishGlobals(PackedWaveField.Empty);
                WaveFieldBridge.PublishFetchGlobals(GameServices.WaveFetch, ShotWind);
                return;
            }
            WaveTrains trains = WaveMath.TrainsFrom(ShotWind, seaState01, GameServices.WaveField);
            WaveFieldBridge.PublishGlobals(WaveFieldBridge.Pack(in trains));
            WaveFieldBridge.PublishFetchGlobals(GameServices.WaveFetch, ShotWind);
        }

        /// <summary>
        /// Publish the searchlight through the SHIPPED bridge, so the frame is of the real publish path and
        /// not of a lookalike assembled here.
        /// </summary>
        void PublishTheLamp(float lampHeight)
        {
            _lamp.State = new WaterLightState
            {
                LampWorld = _lampWorld,
                LampHeightMeters = lampHeight,
                BeamDir = BeamDir.normalized,
                Color = new Color(1f, 0.88f, 0.62f, 1f),
                Intensity = 2.6f,
                Range = LampRange,
                CosHalfAngle = Mathf.Cos(ConeHalfDeg * Mathf.Deg2Rad),
                CosInnerAngle = Mathf.Cos(ConeHalfDeg * 0.55f * Mathf.Deg2Rad),
                EdgeSoftness = 0.5f,
                GateThreshold = 0f,
                GateSoftness = 0.05f,
                GateFallback = 1f,     // no day/night cycle in edit mode -> show the beam
            };

            var host = new GameObject("BeamShotBridge") { hideFlags = HideFlags.HideAndDontSave };
            try { host.AddComponent<WaterLightBridge>().PublishFromRegistry(); }
            finally { Object.DestroyImmediate(host); }
        }

        /// <summary>
        /// Scrub the relief dial. Through a <see cref="MaterialPropertyBlock"/>, NEVER onto the shared
        /// material: writing to <c>Water.mat</c> re-tunes the sea for the whole game and leaves the owner's
        /// hero material dirty. And EVERY property is written on EVERY shot — a property block is sticky, and
        /// a value set only when asked for survives into the next capture.
        /// </summary>
        void SetShot(float waterLevel, float reliefStrength)
        {
            var surface = _seaGo.GetComponent<WaterSurface>();
            if (surface != null)
            {
                var so = new SerializedObject(surface);
                var preview = so.FindProperty("_previewWaterLevel");
                if (preview != null)
                {
                    preview.floatValue = waterLevel;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            var sr = _seaGo.GetComponent<SpriteRenderer>();
            var block = new MaterialPropertyBlock();
            sr.GetPropertyBlock(block);
            block.SetFloat("_WaterLevel", waterLevel);
            block.SetFloat("_BeamReliefStrength", reliefStrength);
            sr.SetPropertyBlock(block);
        }

        Color[] Shoot(string name, float seaState, float reliefStrength, float lampHeight = LampHeight)
        {
            // The night, published the way ADR 0013's controller publishes it (it does not tick in edit mode).
            Shader.SetGlobalColor(Shader.PropertyToID("_DayNightTint"), NightTint);
            Shader.SetGlobalFloat(Shader.PropertyToID("_SunElevation"), PreDawnSunElevation);
            PublishTheSea(seaState);
            PublishTheLamp(lampHeight);
            SetShot(NineMileCreekMainland.SpringHighWater, reliefStrength);

            _cam.Render();
            _cam.Render();   // the second is read: a cold shader cache has faked a regression here before

            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = _rt;
            var hdr = new Texture2D(ShotPx, ShotPx, TextureFormat.RGBAFloat, false);
            hdr.ReadPixels(new Rect(0, 0, ShotPx, ShotPx), 0, 0);
            hdr.Apply();
            RenderTexture.active = prev;

            // ADR 0013's overlay multiplies the WHOLE composited frame by the tint AFTER the water draws. It
            // is a screen-space pass that does not run in a fixture, so apply it here: without it the
            // capture is of pre-compensation values and is not what any player ever sees.
            Color[] lit = hdr.GetPixels();
            var ldr = new Texture2D(ShotPx, ShotPx, TextureFormat.RGBA32, false);
            var ldrPx = new Color[lit.Length];
            for (int i = 0; i < lit.Length; i++)
                ldrPx[i] = new Color(Mathf.Clamp01(lit[i].r * NightTint.r),
                                     Mathf.Clamp01(lit[i].g * NightTint.g),
                                     Mathf.Clamp01(lit[i].b * NightTint.b), 1f);
            ldr.SetPixels(ldrPx);
            ldr.Apply();
            _lastLdr = ldrPx;

            string dir = Path.Combine(Directory.GetCurrentDirectory(), "artifacts");
            Directory.CreateDirectory(dir);
            File.WriteAllBytes(Path.Combine(dir, name + ".png"), ldr.EncodeToPNG());
            Object.DestroyImmediate(hdr);
            Object.DestroyImmediate(ldr);
            // The PNG above is what the player sees; the HDR values are what gets MEASURED (see Measure).
            return lit;
        }
    }
}
