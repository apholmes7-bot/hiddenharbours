using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.EditMode
{
    /// <summary>B1: source/default guards run on CI. Render acceptance is deliberately NOT a
    /// C# reimplementation of the HLSL gate. Null-device cases skip, never certify the look.
    /// GPU measurements use the shipped shore fixture, actual production shaders and public maths.
    /// No dependency on the uncommitted B1Capture assembly or its evidence.</summary>
    public class SurfSequenceTests
    {
        const string WaterPath = "Assets/_Project/Art/Shaders/HiddenHarboursWater.shader";
        const string AdvectPath = "Assets/_Project/Art/Shaders/HiddenHarboursFoamBufferAdvect.shader";
        const string Dial = "_SurfFringeSequenceStrength";
        const string BaseWaterHash = "9684e894683c4e6197c0696f0e95783b69d2312bfd692f759dcb2ffe316bd953";
        // Re-approved at W1 Phase D (#932, owner d20 2026-10-07): the swell-read default and Water.mat's one line.
        const string LegacyDepth =
            "                float foamDepth  = depthC - lerp(BeachSwash(worldXY, depthC, t) * swashSlope * swashGate,  // local, foam-only\n" +
            "                                                 surfRunUpM, boreFoamBlend);   // …and the foam rides the bore's wash too\n";
        static string Read(string path) => File.ReadAllText(path, Encoding.UTF8).Replace("\r\n", "\n");
        static string Hash(string text)
        {
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-", "").ToLowerInvariant();
        }
        static string Code(string text) => Regex.Replace(
            Regex.Replace(text, @"/\*[\s\S]*?\*/|//[^\n]*", ""), @"\s+", "");
        static string Section(string text, string part)
        {
            var match = Regex.Match(text, @"(?m)^ *// B1 FRINGE " + part + @" BEGIN\n([\s\S]*?)^ *// B1 FRINGE " + part + @" END\n");
            Assert.That(match.Success, Is.True, "missing B1 block " + part);
            return match.Groups[1].Value;
        }
        static string MainSource(string source)
        {
            foreach (string part in new[] { "PROPERTY", "UNIFORM", "DEPTH", "COVER" })
            {
                Section(source, part); // A missing marker must not silently change the reconstruction.
                source = Regex.Replace(source, @"(?m)^ *// B1 FRINGE " + part + @" BEGIN\n[\s\S]*?^ *// B1 FRINGE " + part + @" END\n",
                    part == "DEPTH" ? LegacyDepth : "");
            }
            // Approved F2 declarations only: require each exact line before reconstructing B1's control.
            foreach (string line in new[] {
                "        // ADR 0027 F2: CPU-read owner policy. Zero leaves the existing history path untouched.",
                "        _FoamTransportStrain (\"Foam transport strain (m/s)\", Float) = 0",
                "        _FoamTransportCurl (\"Foam transport curl (m/s)\", Float) = 0",
                "        _FoamTransportCollection (\"Foam transport collection (m/s)\", Float) = 0",
                "                float _FoamTransportStrain, _FoamTransportCurl, _FoamTransportCollection;" })
            {
                Assert.That(Regex.Matches(source, "(?m)^" + Regex.Escape(line) + "$").Count, Is.EqualTo(1), line);
                source = source.Replace(line + "\n", "");
            }
            Assert.That(Hash(source), Is.EqualTo(BaseWaterHash),
                "The reconstructed control must be EXACTLY approved main, not another candidate arm.");
            return source;
        }

        sealed class Coast : ITidalTerrain
        {
            readonly int kind;
            public Coast(int kind) { this.kind = kind; }
            public float ElevationAt(Vector2 p) =>
                (kind == 0 ? .04f : kind == 1 ? .25f : .12f) * p.x - 3f +
                (kind == 2 ? .002f * p.y * p.y : 0f);
        }
        static WaveTrains Field(WaveTrain train) => WaveTrains.From(new[] { train }, 1, 2.6f, 0);

        [TestCase(0, TestName = "SurfSequence_ImpactAndResidueShareBorePhase_Spilling")]
        [TestCase(1, TestName = "SurfSequence_ImpactAndResidueShareBorePhase_Plunging")]
        [TestCase(2, TestName = "SurfSequence_ImpactAndResidueShareBorePhase_ObliqueCorner")]
        public void SurfSequence_ImpactAndResidueShareBorePhase(int kind)
        {
            var bed = new Coast(kind);
            var settings = BreakerSettings.Default;
            var train = new WaveTrain(kind == 2 ? new Vector2(1f, .5f).normalized : Vector2.right, 18f, .5f, 0f, 9.81f);
            var field = Field(train);
            var contour = BreakerMath.ContourFor(train, 1f, settings);
            Vector2 at = default;
            float best = 0f;
            for (float x = 0f; x < 75f; x += .125f)
            {
                var p = new Vector2(x, kind == 2 ? 4f : 0f);
                var s = BreakerMath.SurfAt(p, 0f, bed, contour, 1f, field, 9.81f, settings);
                float spatial = s.Breaking01 * s.Whitewater01;
                if (spatial > best) { best = spatial; at = p; }
            }
            Assert.That(best, Is.GreaterThan(.1f), "non-vacuous active coast");
            var first = BreakerMath.SurfAt(at, 0f, bed, contour, 1f, field, 9.81f, settings);
            train = new WaveTrain(train.Direction, train.Wavelength, train.Amplitude,
                train.PhaseOffset + Mathf.DeltaAngle(first.BorePhaseDegrees, 90f) * Mathf.Deg2Rad, 9.81f);
            field = Field(train);
            var crest = BreakerMath.SurfAt(at, 0f, bed, contour, 1f, field, 9.81f, settings);
            float period = BreakerMath.PeriodSeconds(train);
            var trough = BreakerMath.SurfAt(at, 0f, bed, contour, 1f, field, 9.81f, settings, 1f, period / 2);
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(crest.BorePhaseDegrees, 90f)), Is.LessThan(.05f));
            Assert.That(crest.Bore01, Is.GreaterThan(.9f));
            Assert.That(trough.Bore01, Is.LessThan(.001f));
            Assert.That(crest.Bore01, Is.EqualTo(BreakerMath.BorePulse01(crest.BorePhaseDegrees,
                settings.BorePulseSharpness) * crest.BirthEnergy01).Within(1e-6f));
            const double now = 17.25;
            var published = new WaveTrain(train.Direction, train.Wavelength, train.Amplitude,
                train.PhaseOffset - (float)(2 * Math.PI / train.Wavelength * train.PhaseSpeed * now), 9.81f);
            var a = BreakerMath.SurfAt(at, 0f, bed, contour, 1f, field, 9.81f, settings, 1f, now);
            var b = BreakerMath.SurfAt(at, 0f, bed, contour, 1f, Field(published), 9.81f, settings);
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(a.BorePhaseDegrees, b.BorePhaseDegrees)), Is.LessThan(.05f));
            Assert.That(a.Bore01, Is.EqualTo(b.Bore01).Within(.0001f));
            Assert.That(a.RunUpMeters, Is.EqualTo(b.RunUpMeters).Within(.0001f));
            string deposit = Code(Read(AdvectPath));
            foreach (string expression in new[] {
                "floatphase=SurfBorePhaseDeg(breakLinePt,travelS,fs);",
                "floatbore=SurfBorePulse01(phase,_BreakerBore.x)*birth;",
                "floatfront=breaking*alive*bore;",
                "foam+=front*depositStrength*SURF_DEPOSIT_RATE*max(_HHSurfDeposit.z,0.0);" })
                StringAssert.Contains(expression, deposit);
        }

        static bool CorrectConsumers(string source)
        {
            string depth = Code(Section(source, "DEPTH")), cover = Code(Section(source, "COVER"));
            return depth == "floatfringeDisplacement=lerp(BeachSwash(worldXY,depthC,t)*swashSlope*swashGate,surfRunUpM,boreFoamBlend);" +
                "floatfringeHandoff=saturate(_SurfFringeSequenceStrength)*saturate(_SurfBeatStrength)*boreEdgeBlend;" +
                "if(fringeHandoff>0.0)fringeDisplacement=lerp(fringeDisplacement,surfRunUpM,fringeHandoff);" +
                "floatfoamDepth=depthC-fringeDisplacement;" &&
                cover == "if(fringeHandoff>0.0){floatfrontPresence=saturate(surfBreaking*surfAlive*surfBore);" +
                "foamCoverage*=lerp(1.0,frontPresence,fringeHandoff);}";
        }
        [Test]
        public void SurfSequence_FringeUsesExistingBoreAndReferenceDomain()
        {
            string src = Read(WaterPath);
            Assert.That(CorrectConsumers(src), Is.True, "inspect actual executable producer AND consumer blocks");
            foreach (var mutation in new[] {
                src.Replace("* boreEdgeBlend;", "* boreFoamBlend;"),
                src.Replace("foamCoverage *= lerp(1.0, frontPresence", "unusedCoverage *= lerp(1.0, frontPresence"),
                src.Replace("float foamDepth = depthC - fringeDisplacement;", "float foamDepth = depthC;"),
                src.Replace("surfAlive * surfBore", "surfAlive * _Time.y") })
                Assert.That(CorrectConsumers(mutation), Is.False, "the wiring guard must reject disconnected/wrong-clock consumers");
            string code = Code(src);
            StringAssert.Contains("surfBore=SurfBorePulse01(borePhase,_BreakerBore.x)*birth;", code);
            StringAssert.Contains("col.rgb=lerp(col.rgb,_FoamColor.rgb,foamCoverage*_FoamColor.a);", code);
            StringAssert.Contains("col.a=max(col.a,foamCoverage*_FoamColor.a);", code);
            MainSource(src); // Also rules out additional hidden consumers, time reads, samples or loops.
        }

        [Test]
        public void SurfSequence_FringeDialShipsAtZeroOnAllNineMaterials()
        {
            string src = Read(WaterPath);
            StringAssert.Contains("Range(0,1))=0", Code(Section(src, "PROPERTY")));
            Assert.That(Code(Section(src, "UNIFORM")), Is.EqualTo("float_SurfFringeSequenceStrength;"));
            var files = new[] { "Assets/_Project/Art/Materials/Water.mat" }.Concat(
                Directory.GetFiles("Assets/_Project/Art/Materials/WaterPresets", "*.mat").OrderBy(p => p, StringComparer.Ordinal)).ToArray();
            string[] originalHashes = {
                "d660eea6fe569a17d6bebc74456494ce24d29ffc6ca9d84accd929258f8647db",
                "65c31ac5d408f3892c8351e47cfe48eeb1fa255df8c11a7c9783480a6eec44b6",
                "0754e4bed8cc712c95364c43bef405bb0a348cf704dd347b1d8f0c04a5cfa503",
                "f9db60937eebbb8fc0b27d09378c0c6a9e66f6b969ba7bb4aa35ebb0d8aa92c5",
                "a9e2471696ab937823f20b12355b87febbe273b14fa39d925745e6dde66b99b9",
                "232cffe5a1364ee88c0ca1a7d02067754891da1d551f23984d0994f6c5e2bf05",
                "949deddee6eb1b67532f3c14be7428c0b9ea27bb2ee41f741dab6bbe25e9be5d",
                "11bb592a9d9e82b2a7a39c987e645689b789b39e3e8abb21ff04358d9b89a38e",
                "09e80cfaa580ddcf4fa312eb8a1a2c41d0db212c2add5ed30791f17557bab090" };
            Assert.That(files.Length, Is.EqualTo(9));
            for (int i = 0; i < files.Length; i++)
            {
                string yaml = Read(files[i]);
                Assert.That(Regex.Matches(yaml, @"(?m)^    - " + Dial + @": 0$").Count, Is.EqualTo(1), files[i]);
                foreach (string key in new[] { "_FoamTransportStrain", "_FoamTransportCurl", "_FoamTransportCollection" })
                {
                    Assert.That(Regex.Matches(yaml, @"(?m)^    - " + key + @": 0$").Count, Is.EqualTo(1), files[i] + ": " + key);
                    yaml = yaml.Replace("    - " + key + ": 0\n", "");
                }
                Assert.That(Hash(yaml.Replace("    - " + Dial + ": 0\n", "")), Is.EqualTo(originalHashes[i]),
                    files[i] + ": only the new zero key is authorized");
            }
            StringAssert.DoesNotContain(Dial, Read("Assets/_Project/Code/Art/WaterSurface.cs"));
        }

        [Test]
        public void SurfSequence_ProtectedBorePathsRemainUnchanged()
        {
            MainSource(Read(WaterPath)); // Pins the edge, clip, sheet and both twins to approved main.
            Assert.That(Hash(Read(AdvectPath)), Is.EqualTo("25ab2f559ca81a4520452dcd11fec144d1e7157c348543f882194f1141456ad7"));
            Assert.That(Hash(Read("Assets/_Project/Code/Core/Environment/BreakerMath.cs")),
                Is.EqualTo("abce0b880d89e3976faba111960260c2e958a1444de6a8d81e1b70f9a7ffae07"));
            Assert.That(Hash(Read("Assets/_Project/Code/Art/WaterSurface.cs")),
                Is.EqualTo("0de81020278ca98d019d6401514377558390a1d0837dd20cd2593434242cbcf0"));
        }

        static void RequireGpu()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("SKIPPED, NOT VERIFIED: B1 sequence acceptance requires a GPU slot.");
        }
        readonly Dictionary<int, Measurements> measured = new Dictionary<int, Measurements>();
        Measurements Sequence(int site)
        {
            RequireGpu();
            if (!measured.TryGetValue(site, out var result))
            {
                using (var rig = new RenderRig()) { rig.Initialize(site); result = rig.Measure(); }
                measured.Add(site, result);
            }
            return result;
        }
        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void SurfSequence_FringeDrainsBetweenEvents(int site)
        {
            var m = Sequence(site);
            foreach (float quiet in m.FringeQuiet)
                Assert.That(quiet, Is.GreaterThanOrEqualTo(.1f), "<5% fringe cover must last .1T in [.35,.65] on BOTH arrivals");
        }
        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void SurfSequence_ArrivalReadableOverRetainedResidue(int site)
        {
            var m = Sequence(site);
            for (int eventIndex = 0; eventIndex < 2; eventIndex++)
            {
                Assert.That(m.ActiveBins[eventIndex], Is.GreaterThanOrEqualTo(3), "unverified: fewer than three active 1m bins");
                Assert.That(m.ContrastShare[eventIndex], Is.GreaterThanOrEqualTo(.75f), ">=.08 luminance rise in >=75% of active bins");
            }
        }
        [TestCase(0)] [TestCase(1)] [TestCase(2)]
        public void SurfSequence_FrontTracksBorePhase(int site)
        {
            var m = Sequence(site);
            for (int e = 0; e < 2; e++)
            {
                Assert.That(m.RidgeSamples[e], Is.GreaterThanOrEqualTo(8), "unverified: no sustained identifiable crest");
                Assert.That(m.RidgeShare[e], Is.GreaterThanOrEqualTo(.75f), "observed ridge must stay within .5m of its phase-90 front");
            }
        }
        [TestCase(0, TestName = "SurfSequence_FringeControlArmsPreserveMain_Glass")]
        [TestCase(1, TestName = "SurfSequence_FringeControlArmsPreserveMain_BeatOff")]
        [TestCase(2, TestName = "SurfSequence_FringeControlArmsPreserveMain_RunUpOff")]
        public void SurfSequence_FringeControlArmsPreserveMain(int control)
        {
            RequireGpu();
            using (var rig = new RenderRig())
            {
                rig.Initialize(0);
                rig.Control = control;
                for (int i = 0; i < 8; i++)
                {
                    rig.Publish(i * rig.Period / 4f);
                    var baseline = rig.Shot(true, 0f);
                    CollectionAssert.AreEqual(baseline, rig.Shot(false, 0f), "zero vs pinned MAIN");
                    CollectionAssert.AreEqual(baseline, rig.Shot(false, 1f), "disabled arm vs pinned MAIN");
                }
            }
        }

        sealed class Measurements
        {
            public readonly float[] FringeQuiet = new float[2], ContrastShare = new float[2], RidgeShare = new float[2];
            public readonly int[] ActiveBins = new int[2], RidgeSamples = new int[2];
        }
        sealed class BakedBed : ITidalTerrain
        {
            public Texture2D Texture;
            public Vector4 Rect, Range;
            public float ElevationAt(Vector2 p) => Mathf.Lerp(Range.x, Range.y,
                Texture.GetPixelBilinear((p.x - Rect.x) / Rect.z, (p.y - Rect.y) / Rect.w).r);
        }

        // The existing fixture builds the real NMC shore and publishes the production field. Reflection
        // keeps this PR within its approved files; missing private helpers fail loudly rather than skip.
        // Test-only shader instrumentation redirects time/history bindings and RGB consumers, never
        // phase, geometry, coverage, alpha or deposit maths. All variants are in-memory, not assets.
        sealed class RenderRig : IDisposable
        {
            const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
            const int Pixels = 1120, Steps = 128;
            const float Span = 35f, Level = -2.2f, Extent = 96f;
            static readonly Vector2 Wind = new Vector2(6f, -5.3f);
            static readonly Vector2[] Sites = { new Vector2(128f, -71f), new Vector2(109f, 157.5f), new Vector2(50f, -22f) };
            readonly List<Object> owned = new List<Object>();
            BreakerBoreLookTests fixture;
            GameConfig previousConfig;
            Camera camera;
            SpriteRenderer sprite;
            Material candidate, baseline, advect;
            MaterialPropertyBlock original;
            RenderTexture output, history, next;
            Texture2D readback;
            BakedBed bed;
            WaveTrains trains;
            BreakerContour contour;
            Vector2 aim, normal, origin, residual, drift;
            Vector4 historyWorld;
            float scale, halfLife, ageHalfLife, seconds, oldLook, oldDeposit, oldScale;
            bool registrySaved;
            int resolution, site;
            public int Control = -1;
            public float Period { get; private set; }
            T Own<T>(T value) where T : Object { owned.Add(value); return value; }
            T Get<T>(string name) => (T)typeof(BreakerBoreLookTests).GetField(name, Private).GetValue(fixture);
            object Call(string name, params object[] args) => typeof(BreakerBoreLookTests).GetMethod(name, Private).Invoke(fixture, args);

            static Shader Compile(string source, string name)
            {
                source = Regex.Replace(source, "Shader\\s+\"[^\"]+\"", "Shader \"Hidden/B1Sequence/" + name + "\"", RegexOptions.None);
                source = Regex.Replace(source, @"\b_Time\b", "_B1Time");
                source = source.Replace("_HHFoamBufferTex", "_B1HistoryTex").Replace("_HHFoamBufferWorld", "_B1HistoryWorld");
                source = source.Replace("CBUFFER_START(UnityPerMaterial)",
                    "float4 _B1Time; float4 _B1Fringe; float4 _B1Event;\nCBUFFER_START(UnityPerMaterial)");
                // Only the fringe's RGB target changes. _FoamColor also colours other layers: setting
                // that shared material colour would NOT isolate the fringe.
                source = source.Replace("col.rgb = lerp(col.rgb, _FoamColor.rgb, foamCoverage * _FoamColor.a);",
                    "col.rgb = lerp(col.rgb, lerp(_FoamColor.rgb, _B1Fringe.rgb, _B1Fringe.a), foamCoverage * _FoamColor.a);");
                source = source.Replace("col.rgb = lerp(col.rgb, surfFoam, cover * _SurfColor.a);",
                    "col.rgb = lerp(col.rgb, lerp(surfFoam, _B1Event.rgb, _B1Event.a), cover * _SurfColor.a);");
                source = source.Replace("col.rgb = lerp(col.rgb, lipFoam, lip * _SurfLipColor.a);",
                    "col.rgb = lerp(col.rgb, lerp(lipFoam, _B1Event.rgb, _B1Event.a), lip * _SurfLipColor.a);");
                var create = typeof(ShaderUtil).GetMethod("CreateShaderAsset", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
                    null, new[] { typeof(string), typeof(bool) }, null);
                Assert.That(create, Is.Not.Null, "Unity's in-memory shader compiler API is required; no fake render pass");
                var shader = (Shader)create.Invoke(null, new object[] { source, false });
                Assert.That(shader, Is.Not.Null);
                Assert.That(ShaderUtil.ShaderHasError(shader), Is.False, "instrumented production shader must compile");
                shader.hideFlags = HideFlags.HideAndDontSave;
                return shader;
            }

            public void Initialize(int index)
            {
                site = index; aim = Sites[index];
                previousConfig = GameServices.Config;
                GameServices.Config = AssetDatabase.LoadAssetAtPath<GameConfig>("Assets/_Project/Data/Config/GameConfig.asset");
                Assert.That(GameServices.Config, Is.Not.Null);
                fixture = new BreakerBoreLookTests(); fixture.SetUp(); Call("BuildTheShore"); Call("PublishTheSea", 0f);
                camera = Get<Camera>("_cam"); sprite = Get<GameObject>("_seaGo").GetComponent<SpriteRenderer>();
                original = new MaterialPropertyBlock(); sprite.GetPropertyBlock(original);
                var shipped = sprite.sharedMaterial;
                string src = Read(WaterPath), main = MainSource(src);
                candidate = Own(new Material(shipped) { shader = Own(Compile(src, "Candidate")), hideFlags = HideFlags.HideAndDontSave });
                baseline = Own(new Material(shipped) { shader = Own(Compile(main, "Main")), hideFlags = HideFlags.HideAndDontSave });
                bed = new BakedBed { Texture = (Texture2D)Shader.GetGlobalTexture(SeabedGlobals.Tex),
                    Rect = Shader.GetGlobalVector(SeabedGlobals.Rect), Range = Shader.GetGlobalVector(SeabedGlobals.Range) };
                Assert.That(bed.Texture, Is.Not.Null);
                trains = Get<WaveTrains>("_trainsNow"); Period = BreakerMath.PeriodSeconds(trains.Dominant);
                contour = BreakerMath.ContourFor(trains.Dominant, WaveFetch.Envelope01(0f, GameServices.WaveFetch), GameServices.Breakers);
                scale = shipped.GetFloat("_OceanSwellScale") / (float)typeof(DisplacedWaterSurface).GetField("WaveLegacyScaleRef",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).GetValue(null);
                normal = Surf(aim).ShorewardDirection;
                Assert.That(normal.sqrMagnitude, Is.GreaterThan(.5f));
                output = Get<RenderTexture>("_rt"); camera.targetTexture = null; output.Release();
                output.width = Pixels; output.height = Pixels; output.Create(); camera.targetTexture = output;
                camera.orthographicSize = Span / 2; camera.transform.position = new Vector3(aim.x, aim.y, -1000f);
                camera.nearClipPlane = 1f; camera.farClipPlane = 2000f;
                readback = Own(new Texture2D(Pixels, Pixels, TextureFormat.RGBA32, false));
                resolution = FoamBuffer.ResolutionForExtent(Extent);
                var format = FoamBuffer.SelectFormat(SystemInfo.SupportsRenderTextureFormat);
                history = MakeHistory(format); next = MakeHistory(format);
                origin = FoamBuffer.WorldCellOrigin(aim, Extent);
                historyWorld = new Vector4(origin.x, origin.y, Extent, 1f / Extent);
                advect = Own(new Material(Shader.Find("Hidden/HiddenHarbours/FoamBufferAdvect")) { hideFlags = HideFlags.HideAndDontSave });
                advect.SetVector(FoamShaderIds.Resolution, new Vector4(resolution, resolution, 1f / resolution, 1f / resolution));
                advect.SetVectorArray(FoamShaderIds.InjectSeg, new Vector4[FoamBuffer.MaxInjectors]);
                advect.SetVectorArray(FoamShaderIds.InjectShape, new Vector4[FoamBuffer.MaxInjectors]);
                advect.SetVector("_BlitScaleBias", new Vector4(1, 1, 0, 0));
                // Read shipped lifetime data without taking a URP assembly dependency or changing it.
                var renderer = AssetDatabase.LoadAllAssetsAtPath("Assets/Settings/Renderer2D.asset");
                Object feature = renderer.Single(x => x != null && x.GetType().Name == "IsoFacetHullFeature");
                halfLife = (float)feature.GetType().GetField("_foamHalfLifeSeconds", Private).GetValue(feature);
                ageHalfLife = (float)feature.GetType().GetField("_foamAgeHalfLifeSeconds", Private).GetValue(feature);
                drift = WaterSurface.FoamDriftDirection(Wind, Vector2.right, shipped.GetFloat("_FoamDriftWindVsCurrent")) * shipped.GetFloat("_Flow");
                oldLook = FoamInjectionRegistry.LookStrength; oldDeposit = FoamInjectionRegistry.SurfDepositStrength;
                oldScale = FoamInjectionRegistry.DrawnWaveScale; registrySaved = true;
                FoamInjectionRegistry.PublishLookStrength(0f); // Manual history pass, no automatic double step.
            }
            RenderTexture MakeHistory(RenderTextureFormat format)
            {
                var rt = Own(new RenderTexture(resolution, resolution, 0, format) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp });
                rt.Create(); var previous = RenderTexture.active; RenderTexture.active = rt; GL.Clear(false, true, Color.clear); RenderTexture.active = previous;
                return rt;
            }
            SurfState Surf(Vector2 p) => BreakerMath.SurfAt(p, Level, bed, contour,
                WaveFetch.EnvelopeAt(p, Wind, Level, bed, GameServices.WaveFetch), trains,
                GameServices.WaveField.Gravity, GameServices.Breakers, scale);
            public void Publish(float time)
            {
                seconds = time; Call("PublishTheSea", time); trains = Get<WaveTrains>("_trainsNow");
                if (Control == 0) { WaveFieldBridge.PublishGlobals(PackedWaveField.Empty); WaveFieldBridge.PublishBreakersOff(); }
                Shader.SetGlobalVector("_HHSeaLevelWorld", new Vector4(Level, 1, 1, 1));
            }
            void Step(float dt)
            {
                var cells = FoamBuffer.AdvectCells(ref residual, drift * dt);
                var offset = FoamBuffer.SourceOffsetCells(origin, origin, cells);
                var draw = FoamBuffer.DrawOrigin(origin, residual);
                historyWorld = new Vector4(draw.x, draw.y, Extent, 1f / Extent);
                advect.SetTexture(FoamShaderIds.Prev, history); advect.SetVector(FoamShaderIds.BufferWorld, historyWorld);
                advect.SetVector(FoamShaderIds.Shift, new Vector4(offset.x, offset.y, 0, 0));
                advect.SetFloat(FoamShaderIds.Decay, FoamBuffer.DecayFactor(halfLife, dt));
                advect.SetFloat(FoamShaderIds.AgeDecay, FoamBuffer.DecayFactor(ageHalfLife, dt));
                advect.SetVector(FoamShaderIds.SurfDeposit, new Vector4(candidate.GetFloat("_SurfDepositStrength"), scale, dt, 0));
                using (var cmd = new CommandBuffer { name = "B1 sequence history" })
                {
                    cmd.SetRenderTarget(next); cmd.DrawProcedural(Matrix4x4.identity, advect, 0, MeshTopology.Triangles, 3, 1);
                    Graphics.ExecuteCommandBuffer(cmd);
                }
                var swap = history; history = next; next = swap;
            }
            public Color32[] Shot(bool main, float dial, int isolation = 0)
            {
                var mat = main ? baseline : candidate; sprite.sharedMaterial = mat;
                mat.SetVector("_B1Time", new Vector4(seconds / 20, seconds, seconds * 2, seconds * 3));
                mat.SetTexture("_B1HistoryTex", history); mat.SetVector("_B1HistoryWorld", historyWorld);
                mat.SetVector("_B1Fringe", isolation == 1 ? new Vector4(0, 0, 0, 1) : isolation == 2 ? Vector4.one : Vector4.zero);
                mat.SetVector("_B1Event", isolation == 3 ? new Vector4(0, 0, 0, 1) : isolation == 4 ? Vector4.one : Vector4.zero);
                sprite.SetPropertyBlock(original); var block = new MaterialPropertyBlock(); sprite.GetPropertyBlock(block);
                block.SetFloat("_WaterLevel", Level); block.SetVector("_WindDir", new Vector4(Wind.normalized.x, Wind.normalized.y, 0, 0));
                block.SetFloat("_Flow", mat.GetFloat("_Flow"));
                if (!main) block.SetFloat(Dial, dial);
                if (Control == 1) block.SetFloat("_SurfBeatStrength", 0f);
                if (Control == 2) block.SetFloat("_SurfRunUpStrength", 0f);
                // Historical residue diagnostic, unchanged: residue-vs-bare, with surf disabled in BOTH.
                if (isolation == 5 || isolation == 6) block.SetFloat("_SurfStrength", 0f);
                if (isolation == 6) block.SetFloat("_WakeFoamStrength", 0f);
                sprite.SetPropertyBlock(block); camera.Render();
                var old = RenderTexture.active;
                try { RenderTexture.active = output; readback.ReadPixels(new Rect(0, 0, Pixels, Pixels), 0, 0); readback.Apply(); }
                finally { RenderTexture.active = old; }
                return readback.GetPixels32();
            }
            int Pixel(Vector2 p)
            {
                int x = Mathf.FloorToInt((p.x - aim.x + Span / 2) * Pixels / Span);
                int y = Mathf.FloorToInt((p.y - aim.y + Span / 2) * Pixels / Span);
                return x >= 0 && y >= 0 && x < Pixels && y < Pixels ? y * Pixels + x : -1;
            }
            static float Luma(Color32 c) => (.2126f * c.r + .7152f * c.g + .0722f * c.b) / 255f;
            static float Contribution(Color32 black, Color32 white) => Mathf.Max(0f, Luma(white) - Luma(black));
            static bool Visible(Color32 a, Color32 b) => Math.Abs(a.r - b.r) > 2 || Math.Abs(a.g - b.g) > 2 || Math.Abs(a.b - b.b) > 2;

            sealed class Bin
            {
                public Vector2 Point, Normal;
                public float Arrival;
                public readonly List<int> Pixels = new List<int>();
                public readonly List<Vector2> Samples = new List<Vector2>();
                public readonly List<float>[] Quiet = { new List<float>(), new List<float>() };
                public readonly float[] Peak = { float.NegativeInfinity, float.NegativeInfinity }, Active = new float[2];
            }
            float FirstArrival(Vector2 p, float after)
            {
                // Read the PUBLIC phase at the current published time, then follow that crest identity.
                float t = seconds + Mathf.Repeat(Surf(p).BorePhaseDegrees - 90f, 360f) / 360f * Period;
                while (t < after) t += Period;
                return t;
            }
            Vector2 Root(Vector2 guess)
            {
                for (int i = 0; i < 24; i++)
                {
                    Vector2 n = BreakerMath.ShorewardDirection(guess, GameServices.Breakers.SlopeProbeMeters, bed);
                    float env = WaveFetch.EnvelopeAt(guess, Wind, Level, bed, GameServices.WaveFetch);
                    float target = BreakerMath.DepthAtEnvelope(contour.BreakDepths, contour.LeeEnvelope, env);
                    float error = Level - bed.ElevationAt(guess) - target;
                    if (Mathf.Abs(error) < .001f) return guess;
                    float slope = BreakerMath.BedSlopeAlong(guess, n, GameServices.Breakers.SlopeProbeMeters, bed);
                    Assert.That(slope, Is.GreaterThan(.0001f), "contour lost on a flat bed");
                    guess += n * Mathf.Clamp(error / slope, -.5f, .5f);
                }
                Assert.Fail("Contour root did not converge; cannot score an invented break contour.");
                return guess;
            }
            List<Bin> Bins()
            {
                var points = new List<Vector2> { Root(aim) };
                // Small steps follow one connected contour; reject jumps instead of choosing another root.
                foreach (int direction in new[] { -1, 1 })
                {
                    Vector2 p = points[0]; float arc = 0f;
                    for (int i = 0; i < 160; i++)
                    {
                        Vector2 n = BreakerMath.ShorewardDirection(p, GameServices.Breakers.SlopeProbeMeters, bed);
                        Vector2 q = Root(p + direction * new Vector2(-n.y, n.x) * .125f);
                        float length = Vector2.Distance(p, q);
                        Assert.That(length, Is.LessThan(.5f), "disconnected contour root");
                        if (Pixel(q) < 0 || Mathf.Abs(q.x - aim.x) > Span / 2 - 1 || Mathf.Abs(q.y - aim.y) > Span / 2 - 1) break;
                        arc += length; p = q;
                        if (arc >= 1f) { arc -= 1f; points.Add(p); }
                    }
                }
                var bins = new List<Bin>();
                foreach (Vector2 p in points)
                {
                    Vector2 n = BreakerMath.ShorewardDirection(p, GameServices.Breakers.SlopeProbeMeters, bed);
                    var bin = new Bin { Point = p + n * .125f, Normal = n };
                    bin.Arrival = FirstArrival(bin.Point, 8.1f * Period);
                    Vector2 tangent = new Vector2(-n.y, n.x);
                    for (float across = -.5f; across < .5f; across += 1f / 32)
                    for (float shore = 0f; shore < .5f; shore += 1f / 32)
                    {
                        int px = Pixel(p + tangent * across + n * shore);
                        if (px >= 0) bin.Pixels.Add(px);
                    }
                    // Fixed 1/8m quadrature of the same ribbon for the public-maths activity gate.
                    for (float across = -.4375f; across < .5f; across += .125f)
                    for (float shore = .0625f; shore < .5f; shore += .125f)
                        bin.Samples.Add(p + tangent * across + n * shore);
                    Assert.That(bin.Pixels.Count, Is.GreaterThan(0)); bins.Add(bin);
                }
                return bins;
            }

            public Measurements Measure()
            {
                float dt = Period / Steps;
                for (int i = 0; i < 8 * Steps; i++) { Publish(i * dt); Step(dt); }
                Publish(8 * Period);
                float arrival = FirstArrival(aim, 8.1f * Period);
                var bins = Bins(); var roi = new List<int>();
                Vector2 tangent = new Vector2(-normal.y, normal.x);
                for (int y = 0; y < Pixels; y += 2)
                for (int x = 0; x < Pixels; x += 2)
                {
                    Vector2 p = aim + new Vector2((x + .5f) * Span / Pixels - Span / 2, (y + .5f) * Span / Pixels - Span / 2);
                    Vector2 d = p - aim;
                    if (Mathf.Abs(Vector2.Dot(d, tangent)) <= 4 && Vector2.Dot(d, normal) >= 0 && Vector2.Dot(d, normal) <= 4 && bed.ElevationAt(p) < Level)
                    {
                        float env = WaveFetch.EnvelopeAt(p, Wind, Level, bed, GameServices.WaveFetch);
                        Assert.That(BreakerMath.Breaking01FromContour(.02f, contour, env), Is.GreaterThan(.999f), "criterion requires full shore domain");
                        roi.Add(y * Pixels + x);
                    }
                }
                Assert.That(roi.Count, Is.GreaterThan(10));
                var m = new Measurements(); float[] streak = new float[2], oldStreak = new float[2], oldQuiet = new float[2];
                int[] ridgeGood = new int[2]; float[] lastRidge = { float.NaN, float.NaN };
                string dir = "Evidence~/b1-sequence-tests/" + new[] { "sand", "ledge", "corner" }[site];
                Directory.CreateDirectory(dir);
                File.WriteAllText(dir + "/provenance.txt", FormattableString.Invariant(
                    $"base=b5825e1f772b66adf0b7c4cdabc82416888c02d7\nwater={Hash(Read(WaterPath))}\nheight={bed.Texture.width}x{bed.Texture.height}:{bed.Texture.format}\nheightBytes={Hash(Convert.ToBase64String(bed.Texture.GetRawTextureData()))}\nGPU={SystemInfo.graphicsDeviceName}\nT={Period:R}\nstepsPerT={Steps}\nwarmupT=8\nrecordT=3\nsize={Pixels}\nspan={Span}\nflat NMC fixture; displaced/tide/mood/cost acceptance still requires Phase C\n"));
                // Warm both compiled arms, then prove the test time binding holds the same frame.
                Shot(true, 0f); Shot(false, 0f); Shot(false, 1f);
                CollectionAssert.AreEqual(Shot(false, 1f), Shot(false, 1f), "controlled render time must be stable");
                using (var csv = new StreamWriter(dir + "/frames.csv"))
                {
                    csv.WriteLine("seconds,phase,fringeCover,originalResidueCover,ridgeDistance,phaseFrontDistance");
                    // Three periods allow two complete arrival+quiet windows for every phase-offset bin.
                    for (int frame = 0; frame <= 3 * Steps; frame++)
                    {
                        Publish((8 * Steps + frame) * dt); Step(dt);
                        var dressed = Shot(false, 1f);
                        var black = Shot(false, 1f, 1); var white = Shot(false, 1f, 2);
                        float fringe = roi.Count(i => Contribution(black[i], white[i]) > .1f) / (float)roi.Count;
                        var residue = Shot(false, 1f, 5); var bare = Shot(false, 1f, 6);
                        float oldCover = roi.Count(i => Visible(residue[i], bare[i])) / (float)roi.Count;
                        var eventBlack = Shot(false, 1f, 3); var eventWhite = Shot(false, 1f, 4);
                        float ridge = float.NaN, predicted = float.NaN;
                        for (int e = 0; e < 2; e++)
                        {
                            float age = (seconds - arrival) / Period - e;
                            if (age >= .35f && age <= .65f)
                            {
                                streak[e] = fringe < .05f ? streak[e] + 1f / Steps : 0f;
                                oldStreak[e] = oldCover < .05f ? oldStreak[e] + 1f / Steps : 0f;
                                m.FringeQuiet[e] = Mathf.Max(m.FringeQuiet[e], streak[e]);
                                oldQuiet[e] = Mathf.Max(oldQuiet[e], oldStreak[e]);
                            }
                            if (age >= 0f && age <= .25f)
                            {
                                // All scheduled front samples count, including ambiguous/missing ridges.
                                m.RidgeSamples[e]++;
                                predicted = PredictedFront(arrival + e * Period);
                                ridge = ObservedRidge(dressed, eventBlack, eventWhite, lastRidge[e]);
                                if (!float.IsNaN(ridge)) lastRidge[e] = ridge;
                                if (!float.IsNaN(ridge) && !float.IsNaN(predicted) && Mathf.Abs(ridge - predicted) <= .5f) ridgeGood[e]++;
                            }
                            foreach (var bin in bins)
                            {
                                float u = (seconds - bin.Arrival) / Period - e;
                                if (u >= -.1f && u <= .1f)
                                {
                                    foreach (Vector2 p in bin.Samples)
                                    {
                                        var s = Surf(p);
                                        bin.Active[e] = Mathf.Max(bin.Active[e], s.Breaking01 * s.Whitewater01 * s.Bore01);
                                    }
                                    bin.Peak[e] = Mathf.Max(bin.Peak[e], bin.Pixels.Average(i => Luma(dressed[i])));
                                }
                                if (u >= .35f && u <= .65f) bin.Quiet[e].Add(bin.Pixels.Average(i => Luma(dressed[i])));
                            }
                        }
                        csv.WriteLine(FormattableString.Invariant($"{seconds:R},{Surf(aim).BorePhaseDegrees:R},{fringe:R},{oldCover:R},{ridge:R},{predicted:R}"));
                        if (frame % 32 == 0)
                        {
                            Shot(false, 1f); File.WriteAllBytes(dir + "/" + frame.ToString("D3") + ".png", readback.EncodeToPNG());
                            var main = Shot(true, 0f);
                            CollectionAssert.AreEqual(main, Shot(false, 0f), "zero passthrough against hash-pinned main, same frame/history");
                            csv.Flush(); Debug.Log($"B1_SEQUENCE_PROGRESS site={site} frame={frame}/384");
                        }
                    }
                }
                using (var csv = new StreamWriter(dir + "/bins.csv"))
                {
                    csv.WriteLine("event,x,y,arrival,active,contrast");
                    for (int e = 0; e < 2; e++)
                    {
                        int good = 0;
                        foreach (var bin in bins)
                        {
                            Assert.That(bin.Quiet[e].Count, Is.GreaterThan(0), "incomplete quiet window");
                            bin.Quiet[e].Sort(); float contrast = bin.Peak[e] - bin.Quiet[e][bin.Quiet[e].Count / 2];
                            if (bin.Active[e] >= .1f) { m.ActiveBins[e]++; if (contrast >= .08f) good++; }
                            csv.WriteLine(FormattableString.Invariant($"{e},{bin.Point.x:R},{bin.Point.y:R},{bin.Arrival + e * Period:R},{bin.Active[e]:R},{contrast:R}"));
                        }
                        m.ContrastShare[e] = good / (float)Mathf.Max(1, m.ActiveBins[e]);
                        m.RidgeShare[e] = ridgeGood[e] / (float)Mathf.Max(1, m.RidgeSamples[e]);
                        Debug.Log($"B1_SEQUENCE site={site} event={e}: fringe quiet={m.FringeQuiet[e]:F4}T; original residue quiet={oldQuiet[e]:F4}T ({(oldQuiet[e] >= .1f ? "PASS" : "FAIL")}); active bins={m.ActiveBins[e]}/{bins.Count}; contrast share={m.ContrastShare[e]:F4}; ridge share={m.RidgeShare[e]:F4}. Older foam diagnostic is NOT relabeled.");
                    }
                }
                return m;
            }
            float PredictedFront(float crestArrival)
            {
                // Follow the same unwrapped crest arrival-time field, not whichever 90-degree root is nearest.
                float best = float.MaxValue, distance = float.NaN;
                for (float s = -.5f; s <= 12f; s += 1f / 32)
                {
                    var state = Surf(aim + normal * s);
                    if (state.Breaking01 <= 0f) continue;
                    float localArrival = seconds + Mathf.DeltaAngle(90f, state.BorePhaseDegrees) / 360f * Period;
                    if (Mathf.Abs(localArrival - crestArrival) > Period * .49f) continue;
                    float error = Mathf.Abs(localArrival - seconds);
                    if (error < best) { best = error; distance = s; }
                }
                return best < Period / Steps * 2 ? distance : float.NaN;
            }
            float ObservedRidge(Color32[] full, Color32[] black, Color32[] white, float previous)
            {
                // Independent, fixed image rule: strongest shoreward falling luminance edge, with event
                // RGB contribution present. Association only uses last observed ridge, not the prediction.
                var peaks = new List<(float strength, float distance)>();
                for (float s = -.5f; s <= 12f; s += 1f / 32)
                {
                    if (!float.IsNaN(previous) && (s < previous - .25f || s > previous + .75f)) continue;
                    int a = Pixel(aim + normal * (s - .0625f)), b = Pixel(aim + normal * (s + .0625f));
                    if (a < 0 || b < 0 || Contribution(black[a], white[a]) < .02f) continue;
                    float gradient = Luma(full[a]) - Luma(full[b]);
                    if (gradient > .02f) peaks.Add((gradient, s));
                }
                if (peaks.Count == 0) return float.NaN;
                peaks.Sort((a, b) => b.strength.CompareTo(a.strength));
                var best = peaks[0];
                if (peaks.Any(p => Mathf.Abs(p.distance - best.distance) > .5f && p.strength >= .9f * best.strength)) return float.NaN;
                return best.distance;
            }
            public void Dispose()
            {
                try { if (fixture != null) fixture.TearDown(); }
                finally
                {
                    foreach (Object obj in owned.AsEnumerable().Reverse())
                    {
                        if (obj is RenderTexture rt) rt.Release();
                        if (obj != null) Object.DestroyImmediate(obj);
                    }
                    if (registrySaved) { FoamInjectionRegistry.PublishLookStrength(oldLook); FoamInjectionRegistry.PublishSurfDeposit(oldDeposit, oldScale); }
                    GameServices.Config = previousConfig;
                }
            }
        }
    }
}
