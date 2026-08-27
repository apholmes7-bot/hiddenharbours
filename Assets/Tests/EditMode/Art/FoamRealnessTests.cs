using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using HiddenHarbours.Art;
using HiddenHarbours.Core;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// Guards for the 2026-08-05 FOAM REALNESS pass — the owner's *"the foam still doesn't get like
    /// real foam — it needs another pass"*.
    ///
    /// <para><b>What the pass found, and it is worse than "dialled down".</b> #383 shipped the ADR
    /// 0027 #6 advected foam buffer structurally complete — the cell law, the advect pass, the
    /// injector, the shader read, all tested — and then shipped it with <b>both ends disconnected</b>.
    /// <c>_WakeFoamStrength</c> was 0 on all nine water materials (the known half), and separately
    /// <b>no prefab and no scene carried a <see cref="FoamInjector"/> at all</b>, so the buffer had no
    /// source: there was nothing to dial in. Persistent, drifting foam has never once been drawn.
    /// That is the largest single reason the sea's foam does not behave like foam.</para>
    ///
    /// <para><b>The three things this pass changes,</b> each pinned below: the fleet gets a source
    /// (every mesh hull is fitted with an injector where it is built, beside the reflector — same
    /// argument, same file); the look is dialled in on all nine materials; and the buffer's read is
    /// TORN into lace so churn reads as a broken mat rather than as a solid patch with a clean
    /// outline.</para>
    ///
    /// <para>⚠️ <b>What CI can and cannot see.</b> Everything here except
    /// <see cref="Install_FitsAnInjectorToTheHull_OnAGpuMachine"/> is maths, data, wiring-by-source
    /// and material values, and runs on CI's GPU-less agent. The one test that builds a real hull
    /// self-skips on a Null Device like its siblings, so on CI the *behavioural* claim is carried by
    /// the source scrape and only proven on a real machine. Whether the foam LOOKS real is the
    /// owner's eyeball — no test asserts that, and none should.</para>
    /// </summary>
    public class FoamRealnessTests
    {
        private const string WaterShaderPath = "Assets/_Project/Art/Shaders/HiddenHarboursWater.shader";
        private const string ServicePath = "Assets/_Project/Code/Art/IsoFacetHullPresentationService.cs";
        private const string HullMeshFolder = "Assets/_Project/Data/Boats/HullMeshes";
        private const string LiveWaterMatPath = "Assets/_Project/Art/Materials/Water.mat";
        private const string PresetsFolder = "Assets/_Project/Art/Materials/WaterPresets";

        private static readonly string[] PresetNames =
        {
            "Water_DeepBlue", "Water_FoggySmother", "Water_GlassyCalm", "Water_NorthAtlantic",
            "Water_StirredBrown", "Water_StormGrey", "Water_Tropical", "Water_WarmShelter",
        };

        private static string Read(string repoRelative)
        {
            string path = Path.Combine(Application.dataPath, "..", repoRelative);
            Assert.IsTrue(File.Exists(path), $"missing: {repoRelative}");
            return File.ReadAllText(path);
        }

        private static float MatFloat(string matPath, string key)
        {
            var m = Regex.Match(Read(matPath), $@"-\s{Regex.Escape(key)}:\s*(-?[\d.eE+]+)");
            Assert.IsTrue(m.Success, $"'{matPath}' does not serialize {key}.");
            return float.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
        }

        // ==== 1. the buffer finally has a SOURCE ==================================================

        [Test]
        public void TheHullService_FitsAFoamInjector_WhereItBuildsTheHull()
        {
            // Source-scraped on purpose: this is the CI-runnable half of the wiring claim, because the
            // behavioural test below needs a graphics device and CI has none. It is also the exact
            // check that would have caught #383's real gap — the buffer was complete and connected to
            // nothing, and no value-based test can see an absent component.
            string src = Read(ServicePath);
            Assert.IsTrue(Regex.IsMatch(src, @"MakeChurn\(host,\s*def\);"),
                "Install must fit the hull with a FoamInjector — the buffer is a wake with no source " +
                "otherwise, which is the state #383 actually shipped.");
            Assert.IsTrue(Regex.IsMatch(src, @"host\.AddComponent<FoamInjector>\(\)"),
                "the injector goes on the HOST — it reads transform.position as the hull's way " +
                "through the water, and the overlay quad counter-rotates.");
            StringAssert.Contains("def.WatertightHalfBeamMeters", src,
                "the churned band's width must come from the hull's own authored half-beam (rule 6), " +
                "not from a constant repeated per boat.");
        }

        [Test]
        public void EveryHullMeshDef_AuthorsAHalfBeam_OrItsChurnSilentlyFallsBack()
        {
            // The data half of the same wiring. A def with no half-beam does not fail loudly — the
            // service leaves the injector's serialized default in place — so a tanker would churn a
            // dory's ribbon and nobody would be told. Cheap to assert, invisible otherwise.
            string folder = Path.Combine(Application.dataPath, "..", HullMeshFolder);
            Assert.IsTrue(Directory.Exists(folder), $"missing hull mesh folder: {HullMeshFolder}");
            string[] defs = Directory.GetFiles(folder, "*.asset");
            Assert.Greater(defs.Length, 0, "no hull mesh defs found");
            foreach (string path in defs)
            {
                var m = Regex.Match(File.ReadAllText(path), @"WatertightHalfBeamMeters:\s*(-?[\d.eE+]+)");
                Assert.IsTrue(m.Success, $"'{Path.GetFileName(path)}' has no WatertightHalfBeamMeters");
                float halfBeam = float.Parse(m.Groups[1].Value,
                                             System.Globalization.CultureInfo.InvariantCulture);
                Assert.Greater(halfBeam, 0f,
                    $"'{Path.GetFileName(path)}' authors a half-beam of {halfBeam} — her churned foam " +
                    "band would quietly fall back to the injector's default instead of being her own.");
            }
        }

        [Test]
        public void ConfigureRadius_TakesTheHullsBeam_AndFloorsADegenerateOne()
        {
            var go = new GameObject("hull");
            try
            {
                var injector = go.AddComponent<FoamInjector>();
                injector.ConfigureRadius(4.7f);
                Assert.AreEqual(4.7f, injector.RadiusMeters, 1e-4f,
                    "a trawler must churn a trawler's band");
                injector.ConfigureRadius(0f);
                Assert.Greater(injector.RadiusMeters, 0f,
                    "a zero half-beam must degrade to a thin ribbon, never to a divide or a hole");
            }
            finally { Object.DestroyImmediate(go); }
        }

        [Test]
        public void Install_FitsAnInjectorToTheHull_OnAGpuMachine()
        {
            // The behavioural half. Installing a mesh hull builds real renderers, so this is one of
            // the ~30 EditMode tests that only run on a machine with a graphics device — see the
            // class doc for the honest split.
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("no graphics device — installing a mesh hull needs one; the source " +
                              "scrape above carries this claim on CI.");

            var host = new GameObject("hull");
            HullMeshDef def = ScriptableObject.CreateInstance<HullMeshDef>();
            var mesh = new Mesh();
            try
            {
                def.Id = "hullmesh.foamtest";
                def.Mesh = mesh;
                def.Ramps = new[]
                {
                    new HullMeshDef.Ramp { Colors = new[] { new Color32(1, 2, 3, 255) }, Offset = 0 }
                };
                def.Bayer16 = new float[16];
                def.PxPerMetre = 32;
                def.CellW = 456; def.CellH = 420;
                def.WatertightHalfBeamMeters = 2.5f;

                var service = new IsoFacetHullPresentationService();
                Assert.IsNotNull(service.Install(host, def), "the mesh hull installed");

                var injector = host.GetComponent<FoamInjector>();
                Assert.IsNotNull(injector,
                    "every mesh hull the game builds must churn the foam buffer — otherwise the " +
                    "buffer has no source and persistent foam is never drawn at all");
                Assert.AreEqual(2.5f, injector.RadiusMeters, 1e-3f,
                    "the churned band is the hull's own half-beam");
            }
            finally
            {
                Object.DestroyImmediate(host);
                Object.DestroyImmediate(def);
                Object.DestroyImmediate(mesh);
            }
        }

        // ==== 2. the look is dialled in, on every material ========================================

        [Test]
        public void TheWakeFoam_ShipsOn_OrTheOwnerSeesNoChangeAtAll()
        {
            // #383's step one shipped at 0 by design. This is step two. A pass that left the dial at
            // its off position would be a refactor the owner cannot see, not the pass he asked for.
            Assert.Greater(MatFloat(LiveWaterMatPath, "_WakeFoamStrength"), 0f,
                "_WakeFoamStrength 0 skips the whole compose — the buffer would still draw nothing.");
            Assert.Greater(MatFloat(LiveWaterMatPath, "_WakeFoamLace"), 0f,
                "_WakeFoamLace 0 is the pre-pass read exactly — solid patches, clean outlines.");
        }

        [Test]
        public void EveryPreset_CarriesTheWakeFoamKeys_AndAgreesOnThem()
        {
            // The standing preset trap (the _RainRingStrength / _PixelsPerUnit precedents): "Water
            // Presets ▸ Apply" is a wholesale CopyPropertiesFromMaterial, so a preset missing a key
            // stamps the shader default over the live value the moment it is applied. Asserted on the
            // serialized TEXT, because a material that omits a key still ANSWERS from the shader
            // default — a value-based check passes vacuously for exactly the presets carrying the trap.
            // The five AGE keys joined this list in round 2 (2026-08-27) because they were serialized by
            // NO material at all — every one of the nine silently rode the shader Properties default, so
            // the owner could not tune the ramp from the material even though the tooltips said he could.
            // That is the _RippleWavelength trap verbatim: a key absent EVERYWHERE looks perfectly
            // consistent and is invisible to a value-based check.
            string[] keys = { "_WakeFoamStrength", "_WakeFoamLace", "_WakeFoamLaceScale",
                              "_WakeFoamThreshold", "_WakeFoamSoftness", "_WakeFoamBands",
                              "_WakeFoamAgeStrength", "_WakeFoamFreshFloor", "_WakeFoamWhiteHold",
                              "_WakeFoamBlueReach", "_WakeFoamDeepReach" };
            foreach (string name in PresetNames)
            {
                string path = $"{PresetsFolder}/{name}.mat";
                string yaml = Read(path);
                foreach (string key in keys)
                {
                    Assert.IsTrue(yaml.Contains($"- {key}:"),
                        $"'{name}.mat' does not serialize {key} — applying it would stamp the shader " +
                        "default over the owner's value.");
                    Assert.AreEqual(MatFloat(LiveWaterMatPath, key), MatFloat(path, key), 1e-4f,
                        $"'{name}.mat' disagrees with the live material on {key}. A preset may carry " +
                        "any MOOD it likes; the wake-foam structure is not a mood, and applying one " +
                        "must not silently retune the churn for the whole sea.");
                }
            }
        }

        // ==== 3. the read is TORN, in the right order, off the shared foam basis ==================

        [Test]
        public void TheLace_TearsTheStoredCoverage_BEFORE_ItIsThresholded()
        {
            // Order is the whole argument and it is easy to get subtly wrong. Tearing the STORED
            // coverage removes foam that was never there to begin with — the fringe thins and dies
            // sooner, the dense heart survives. Tearing the FINISHED coverage instead would punch
            // holes through an already-shaped, already-posterized patch: a stencil laid over a decal,
            // which is a different (and worse) artefact than the one being fixed.
            string src = Read(WaterShaderPath);
            int tearAt = src.IndexOf("stored *= lerp(1.0, saturate(torn * 2.0), saturate(_WakeFoamLace));",
                                     System.StringComparison.Ordinal);
            int thresholdAt = src.IndexOf("float cover = smoothstep(thr, min(thr + soft, 1.0), stored);",
                                          System.StringComparison.Ordinal);
            Assert.Greater(tearAt, 0, "the lace tear is not applied in the shader");
            Assert.Greater(thresholdAt, tearAt,
                "the tear must multiply the STORED buffer value before the threshold shapes it — " +
                "tearing afterwards stencils holes through a finished patch.");
        }

        [Test]
        public void TheLace_IsAnExactPassthroughAtZero()
        {
            // The revertibility contract every ADR-0010 addendum in this shader keeps: the owner can
            // always get back to the previous look with one slider, and the branch must be skipped
            // outright rather than multiplied by zero (a lerp to 1.0 is not free, and worse, it is not
            // obviously exact to a future reader).
            string src = Read(WaterShaderPath);
            StringAssert.Contains("if (_WakeFoamLace > 0.001)", src,
                "the lace block must be GUARDED, so 0 is an exact and obvious passthrough");
        }

        [Test]
        public void TheLace_ReusesTheSeasOwnFoamBasis_NotASecondOne()
        {
            // "One field, one meaning" — the standing rule that keeps this shader's layers reading as
            // one sea. The tear rides the SAME EvolvingField, the SAME wind stretch and the SAME
            // BandFreq sea-state scaling the whitecaps use, so churn boils, streaks and coarsens with
            // the rest of the foam instead of being an unrelated texture laid on top. A second stretch
            // or a second noise here would be a second sea.
            string src = Read(WaterShaderPath);
            int fnAt = src.IndexOf("float WakeFoamCoverage(", System.StringComparison.Ordinal);
            Assert.Greater(fnAt, 0, "WakeFoamCoverage not found");
            int fnEnd = src.IndexOf("\n            }", fnAt, System.StringComparison.Ordinal);
            string body = src.Substring(fnAt, fnEnd - fnAt);

            StringAssert.Contains("max(_FoamStreakStretch, 1.0)", body,
                "the tear must stretch along the wind on the whitecaps' own stretch knob");
            StringAssert.Contains("BandFreq(_NoiseScale) * 3.0 * _FoamBlobScale", body,
                "the tear's frequency must ride the shared foam blob scale through BandFreq, so it " +
                "coarsens with the growing sea like every other band (ADR 0027 #4)");
            StringAssert.Contains("EvolvingField(", body,
                "the tear must boil with the sea's own evolving foam field, not sit static");
        }

        [Test]
        public void TheWakeFoam_IsComposedInsideThePaletteGuardRail()
        {
            // The grade bound the brief names: foam whiteness must stay inside the bounded final grade
            // (ADR 0015), or dusk and night wear a white veil instead of moonlit churn. Composing the
            // buffer BEFORE PaletteGrade is what makes that true for free — and it is a one-line
            // mistake to "fix" a too-dim wake by moving it after the grade.
            string src = Read(WaterShaderPath);
            int composeAt = src.IndexOf("float wakeFoam = WakeFoamCoverage(worldXY, bay, wakeFresh);",
                                        System.StringComparison.Ordinal);
            int gradeAt = src.IndexOf("col.rgb = PaletteGrade(col.rgb, dayNightLuma);",
                                      System.StringComparison.Ordinal);
            Assert.Greater(composeAt, 0, "the wake foam compose was not found");
            Assert.Greater(gradeAt, composeAt,
                "wake foam must be laid down BEFORE the palette guard-rail, or the sea's brightest " +
                "layer is the one thing the guard-rail does not bound.");
        }

        // ==== 4. the buffer maths the pass leans on stay true =====================================

        [Test]
        public void ChurnedFoam_Lingers_AndTheDecayComposes()
        {
            // "A wake is a fading road, not a glow that vanishes with the source" — the brief's first
            // realness behaviour, restated as the property the buffer must have for it: exponential
            // decay, so ten small frames decay exactly as far as one big one and the trail's lifetime
            // does not change with frame rate.
            const float halfLife = 6f;
            float oneStep = FoamBuffer.DecayFactor(halfLife, 1f);
            float tenSteps = 1f;
            for (int i = 0; i < 10; i++) tenSteps *= FoamBuffer.DecayFactor(halfLife, 0.1f);
            Assert.AreEqual(oneStep, tenSteps, 1e-5f,
                "decay must compose, or the wake's life depends on the frame rate");
            Assert.AreEqual(0.5f, FoamBuffer.DecayFactor(halfLife, halfLife), 1e-5f,
                "a half-life must halve");
            Assert.Greater(FoamBuffer.Decay(1f, halfLife, 2f), 0.5f,
                "foam must still be on the water two seconds after it was laid — that persistence IS " +
                "the feature; a mark that dies with its source is the emitter's job, not the buffer's");
        }

        [Test]
        public void AMooredHullStillChurns_WhenTheSeaMovesUnderHer()
        {
            // The ask that ruled the buffer in (owner, 2026-08-01: "the hull doesn't create realistic
            // foam when bobbing"). At zero speed the wake channel is silent, so anything the sea gets
            // has to come from the bob channel — and it only exists because the hull LAGS the surface.
            float atRest = FoamBuffer.Injection01(
                horizontalSpeed: 0f, verticalRate: 0f,
                wakeKnee: 3f, wakeExponent: 1f, wakeWeight: 1f,
                slapKnee: 0.5f, slapExponent: 2.5f, slapWeight: 1f);
            Assert.AreEqual(0f, atRest, 1e-6f, "glass under a still hull churns nothing");

            float bobbing = FoamBuffer.Injection01(
                horizontalSpeed: 0f, verticalRate: 0.4f,
                wakeKnee: 3f, wakeExponent: 1f, wakeWeight: 1f,
                slapKnee: 0.5f, slapExponent: 2.5f, slapWeight: 1f);
            Assert.Greater(bobbing, 0f,
                "a hull with no way on, working against a chop, must still lay foam — this is the " +
                "case BoatWakeEmitter has no signal for and the reason the buffer exists");

            // and the slap must be SUPER-linear: half the rate churns much less than half the foam.
            float half = FoamBuffer.Injection01(0f, 0.2f, 3f, 1f, 1f, 0.5f, 2.5f, 1f);
            Assert.Less(half, bobbing * 0.5f,
                "a gentle rise-and-fall must churn disproportionately less than a slap, or every " +
                "swell froths and the sea reads as permanently foamy");
        }
    }
}
