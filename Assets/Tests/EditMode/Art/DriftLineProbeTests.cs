using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// The §18 drift-line upgrade, rendered — because "the lanes now follow the shared drift and gather
    /// on convergence lines" is a claim about pixels, and the twin tests only prove the arithmetic.
    ///
    /// <para><b>What it renders.</b> The real <c>Water.mat</c> on a quad through <c>Camera.Render()</c>
    /// on the project's own 2D renderer, at PPU 32, four times: drift lines OFF, then dialled in at
    /// today's settings, then with the shared-drift basis, then with the convergence gate. It writes all
    /// four to <c>artifacts/</c> so they can be compared by eye, and asserts the things a screenshot
    /// cannot: that the new knobs at their defaults change nothing beyond the clock, that swinging the
    /// basis onto the shared drift really re-orients the lanes, and that the convergence gate only ever
    /// REMOVES lane energy.</para>
    ///
    /// <para>⚠️ <b>What this probe can and cannot prove.</b> This water is TIME-DRIVEN and <c>_Time</c>
    /// advances between <c>Camera.Render()</c> calls, so two frames of the SAME material differ —
    /// measured at 9366 px on this test's first draft, which is exactly the false negative a naive
    /// byte-comparison would have reported as a broken passthrough. The probe therefore measures the
    /// TEMPORAL FLOOR first and states every claim against it.</para>
    ///
    /// <para>⚠️⚠️ <b>Why the floor is SAMPLED, and why the margin is additive (hardened 2026-07-30).</b>
    /// This test cried wolf in four separate full-suite runs over two days — including at
    /// <c>origin/main</c> with no lane code present at all. It was measuring the floor from ONE frame
    /// pair and then demanding <c>3 x floor + 512</c>. Measured here over four isolated warm runs
    /// (192x192 = 36864 px/frame):</para>
    /// <list type="bullet">
    /// <item>the SIGNAL is tight within a given load — basis delta 26041 / 26107 / 27957 / 28398 px
    /// (71-77% of frame, spread 8.5%);</item>
    /// <item>the FLOOR, as a single sample, is not — 4932 / 7675 / 7730 / 8740 px (13-24% of frame,
    /// spread 3808 px, i.e. 77% of its own minimum) — and it varies WITHIN one run too: a later run's
    /// five samples spanned 7345-12682 px, so which single frame you happened to catch decided the
    /// verdict.</item>
    /// </list>
    /// <para>Multiplying the NOISY quantity by three swung the threshold 15308 → 26732 px underneath a
    /// stable ~27000 px signal, so the same green test passed by +70.1%, +20.7%, +10.1% and +4.6% on
    /// four consecutive runs — a coin flip, not a measurement. Applying the old rule to six full-suite
    /// runs logged during this hardening, THREE would have been red (thresholds 54269 / 40019 / 40388
    /// against signals of 36148 / 35992 / 36216).</para>
    ///
    /// <para><b>Why a multiplier can never work here, and a difference can.</b> Under load the clock
    /// advances further between draws, which inflates the floor AND the basis delta — but not in step,
    /// because the basis delta is mostly real signal. Measured across isolated and full-suite runs the
    /// signal-to-floor RATIO ranges <b>2.02 to 14.2</b>, so no fixed multiplier sits below all of it and
    /// above a sabotaged run. The DIFFERENCE does: signal minus typical floor stayed inside
    /// <b>18229-27854</b> px on every honest run, while a sabotaged one collapsed to <b>1672</b>. So the
    /// floor is now the MEDIAN of <see cref="FloorSamples"/> consecutive pairs (one loaded frame can no
    /// longer speak for all of them), and the margin is ADDITIVE — see <see cref="ClockMarginPx"/>,
    /// which sits an order of magnitude clear of both ends.</para>
    ///
    /// <para>⚠️ <b>The cold ShaderCache is a SECOND, opposite failure mode</b> — and the reason for the
    /// discarded warm-up draw. On a cold cache this probe measured basis delta <b>17692</b> px against a
    /// floor of 10055: the cache does not merely inflate the floor, it SUPPRESSES the signal to a third
    /// below its warm value, which no margin can be widened far enough to survive without going blind.
    /// <c>Shoot</c> renders twice for the same reason, but the FIRST shot of the fixture is the one that
    /// pays for compilation, so it must not be one this test counts.</para>
    ///
    /// <para><b>Sabotage MEASURED (2026-07-30, this fixture, 1 test).</b> Zeroing the basis blend inside
    /// the shader's guard (<c>saturate(_DriftLineFoamDrift)</c> → <c>saturate(_DriftLineFoamDrift * 0.0)</c>,
    /// which leaves the guard's source text intact so the twin's text assert stays green and only this
    /// probe can bite): <b>1 of 1</b> — the basis delta collapses to clock noise. That is the defect this
    /// probe exists for: the §18 upgrade silently not applying, lanes still combed along the raw current
    /// while the foam they are made of drifts elsewhere.</para>
    ///
    /// <para>So: BYTE-identity at a fixed clock is established by <c>WaterDriftLinesTests</c>'
    /// EXACT-equality assertions plus the two unreachable <c>if</c> guards in the shader. This probe
    /// proves the complementary thing a twin cannot — that on a GPU, through the real material, the
    /// defaults produce no VISIBLE change beyond the clock, and that the dials produce a large one.
    /// The shipped look needs neither argument: <c>_DriftLineStrength</c> is 0, so the whole helper
    /// returns before it reaches any new code.</para>
    ///
    /// <para><b>CI cannot adjudicate this.</b> No graphics device there — a render CRASHES the editor
    /// rather than failing. Gated on <see cref="RequireAGraphicsDevice"/> first, skipping loudly, the
    /// same discipline <c>IsoFacetUrpPassTests</c> keeps.</para>
    /// </summary>
    public class DriftLineProbeTests
    {
        const int ProbeLayer = 31;
        const int Size = 192;
        const float Ppu = 32f;

        /// <summary>How many consecutive frame pairs the temporal floor is measured over.</summary>
        const int FloorSamples = 5;

        /// <summary>
        /// How far a delta must sit from the measured clock-noise band before it means anything:
        /// a QUARTER of the frame, in absolute pixels (9216). Additive, never a multiple — see the
        /// class note. Both claims use it, from opposite sides: a passthrough delta must stay within
        /// this of the WORST clock frame, a re-orientation must clear the TYPICAL one by it.
        ///
        /// <para>Sized off the measured populations rather than picked, over eleven runs (isolated, cold
        /// and full-suite): a real re-orientation clears the TYPICAL floor by 18229-27854 px, whereas a
        /// SABOTAGED one clears it by 1672. At 9216 this margin sits 5.5x above the sabotaged gap and
        /// still only half the smallest honest one — an order of magnitude of daylight on both sides.
        /// In absolute terms the signal ran 24869-36367 px and the floor 1648-18721 px, which is why the
        /// bar must be pinned to the floor MEASURED IN THAT RUN rather than to any fixed pixel count.</para>
        ///
        /// <para>⚠️ It must stay much larger than the SAMPLING spread, which is the trap the first cut
        /// of this hardening fell into. A passthrough delta is simply one more draw from the same
        /// distribution as the floor samples, so it routinely lands a little ABOVE their maximum: with
        /// the old ±64 tolerance that cost 3639-vs-3608 px (passed by 33) on one run and 7441-vs-7186
        /// (failed by 191) on the next. Anything of the order of the noise itself is not a margin.</para>
        /// </summary>
        const int ClockMarginPx = Size * Size / 4;

        GameObject _seaGo, _camGo;
        Camera _cam;
        RenderTexture _rt;
        Material _sea;

        [TearDown]
        public void TearDown()
        {
            if (_cam != null) _cam.targetTexture = null;
            if (_rt != null) { _rt.Release(); Object.DestroyImmediate(_rt); _rt = null; }
            if (_seaGo != null) Object.DestroyImmediate(_seaGo);
            if (_camGo != null) Object.DestroyImmediate(_camGo);
            if (_sea != null) Object.DestroyImmediate(_sea);
            _seaGo = _camGo = null; _cam = null; _sea = null;
        }

        static void RequireAGraphicsDevice()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore(
                    "SKIPPED, NOT VERIFIED — no graphics device (Null Device), so nothing rendered and " +
                    "nothing was proved. Expected on CI; the drift-line probe needs a GPU.");
        }

        void BuildSea()
        {
            var source = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/_Project/Art/Materials/Water.mat");
            Assert.IsNotNull(source, "the hero water material must exist for this probe.");
            _sea = new Material(source) { hideFlags = HideFlags.HideAndDontSave };
            // A lively-but-not-storm sea: the drift lines' own bell peaks in the middle of _Chop, and
            // a glassy or storming sea deliberately shows none (§18.3), so a probe at either end would
            // measure the gate rather than the lanes.
            _sea.SetFloat("_Chop", 0.35f);
            _sea.SetFloat("_Roughness", 0.1f);
            _sea.SetFloat("_Flow", 0.3f);
            _sea.SetVector("_FlowDir", new Vector4(1f, 0f, 0f, 0f));
            _sea.SetVector("_WindDir", new Vector4(0f, 1f, 0f, 0f));   // 90° off the current, so a
            _sea.SetFloat("_FoamDriftWindVsCurrent", 0.6f);            // basis change is unmistakable
            _sea.SetFloat("_WaterLevel", 0f);
            _sea.SetFloat("_UseHeightTex", 0f);
            _sea.DisableKeyword("_USE_HEIGHTTEX");
            _sea.SetFloat("_PaletteGradeStrength", 0f);
            _sea.SetFloat("_ObjectReflectStrength", 0f);
            _sea.SetFloat("_DriftLineStrength", 0f);

            _seaGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
            _seaGo.name = "ProbeSea";
            _seaGo.layer = ProbeLayer;
            Object.DestroyImmediate(_seaGo.GetComponent<Collider>());
            _seaGo.transform.position = new Vector3(0f, 0f, 0.5f);
            _seaGo.transform.localScale = new Vector3(Size / Ppu * 2f, Size / Ppu * 2f, 1f);
            _seaGo.GetComponent<MeshRenderer>().sharedMaterial = _sea;

            _camGo = new GameObject("ProbeCam") { layer = ProbeLayer };
            _cam = _camGo.AddComponent<Camera>();
            _cam.orthographic = true;
            _cam.orthographicSize = Size / (2f * Ppu);
            _cam.transform.position = new Vector3(0f, 0f, -100f);
            _cam.nearClipPlane = 1f;
            _cam.farClipPlane = 400f;
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = Color.clear;
            _cam.cullingMask = 1 << ProbeLayer;
            _cam.allowHDR = true;
            _cam.allowMSAA = false;
            _rt = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGBHalf)
            { filterMode = FilterMode.Point };
            _cam.targetTexture = _rt;
        }

        Color32[] Shoot(string name)
        {
            _cam.Render();
            _cam.Render();   // the second is read: a cold shader cache has faked a regression here before
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = _rt;
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            Color32[] px = tex.GetPixels32();

            if (name != null)
            {
                string dir = Path.Combine(Directory.GetCurrentDirectory(), "artifacts");
                Directory.CreateDirectory(dir);
                File.WriteAllBytes(Path.Combine(dir, $"driftlines-{name}.png"), tex.EncodeToPNG());
            }
            Object.DestroyImmediate(tex);
            return px;
        }

        static int DifferingPixels(Color32[] a, Color32[] b)
        {
            int n = 0;
            for (int i = 0; i < a.Length; i++)
                if (a[i].r != b[i].r || a[i].g != b[i].g || a[i].b != b[i].b) n++;
            return n;
        }

        /// <summary>
        /// The clock-only noise between consecutive frames of an UNCHANGED material, measured
        /// <see cref="FloorSamples"/> times so that one loaded frame cannot speak for all of them.
        /// Hands back the last frame shot, so the caller's next shot is exactly ONE frame on — the same
        /// gap every sample here spans, which is what makes the comparison honest.
        /// </summary>
        int[] SampleTemporalFloor(out Color32[] last)
        {
            var deltas = new int[FloorSamples];
            last = Shoot(null);
            for (int i = 0; i < FloorSamples; i++)
            {
                Color32[] next = Shoot(null);
                deltas[i] = DifferingPixels(last, next);
                last = next;
            }
            return deltas;
        }

        /// <summary>The typical clock frame — robust to a single load spike, unlike a lone sample.</summary>
        static int Median(int[] values)
        {
            var sorted = (int[])values.Clone();
            System.Array.Sort(sorted);
            return sorted[sorted.Length / 2];
        }

        /// <summary>The worst clock frame seen.</summary>
        static int Max(int[] values)
        {
            int m = values[0];
            for (int i = 1; i < values.Length; i++)
                if (values[i] > m) m = values[i];
            return m;
        }

        /// <summary>Total lane brightness above the bare sea — how much drift line is on screen.</summary>
        static long LaneEnergy(Color32[] lit, Color32[] bare)
        {
            long e = 0;
            for (int i = 0; i < lit.Length; i++)
                e += Mathf.Max(0, lit[i].r - bare[i].r) + Mathf.Max(0, lit[i].g - bare[i].g)
                   + Mathf.Max(0, lit[i].b - bare[i].b);
            return e;
        }

        [Test]
        public void DriftLineUpgrade_PassesThrough_ThenChangesTheSeaWhenAsked()
        {
            RequireAGraphicsDevice();
            BuildSea();

            // WARM THE CACHE, and throw the frame away. A cold ShaderCache does not just inflate the
            // floor here — it SUPPRESSES the basis delta to 17692 px against a warm ~27000 (see the
            // class note), so the first draw of this fixture must not be one that any claim rests on.
            Shoot(null);

            // (0) the shipped state: the layer is OFF, so the whole helper returns early.
            Color32[] off = Shoot("0-off");

            // ---- THE TEMPORAL NOISE FLOOR, sampled first ----------------------------------------
            // ⚠️ This water is TIME-DRIVEN (the lanes advance with _Flow, the foam evolves, the
            // whitecaps live and die), and _Time advances between Camera.Render() calls. So two frames
            // of the SAME material differ, and a naive byte-comparison of two shots "proves" a change
            // that is nothing but the clock — measured at 9366 px on the first draft of this test.
            // The floor is therefore measured explicitly and every claim below is stated against it.
            //
            // What this means for the passthrough claim, precisely: BYTE-identity at a fixed clock is
            // established by WaterDriftLinesTests' EXACT-equality assertions plus the two unreachable
            // `if` guards in the shader. This probe proves the weaker but complementary thing a twin
            // cannot — that at the defaults there is no VISIBLE change beyond the clock, on a GPU,
            // through the real material.
            //
            // The floor is sampled FloorSamples times rather than once, and the two claims below read
            // OPPOSITE ends of that distribution — which is precisely why one number could not serve
            // both, and why this test used to flake. The passthrough claim is broken by a floor sampled
            // too LOW, so it reads the worst clock frame; the re-orientation claim is broken by a floor
            // sampled too HIGH, so it reads the typical one.
            _sea.SetFloat("_DriftLineStrength", 0.6f);
            Color32[] shipped = Shoot("1-dialled-in-defaults");
            int[] floorSamples = SampleTemporalFloor(out Color32[] again);
            int floorTypical = Median(floorSamples);
            int floorWorst = Max(floorSamples);

            // (1) the three NEW knobs, set explicitly to their own defaults, one frame later — so the
            // clock gap matches a floor sample's. Any difference must sit within ClockMarginPx of the
            // WORST clock frame we saw — and NOT merely at it: this delta is one more draw from the very
            // distribution those samples came from, so landing a little above their maximum is normal,
            // not evidence (measured: 3639 against a 3608 max on one run, 7441 against 7186 on the
            // next). This end is generous on purpose. BYTE-identity at a fixed clock is already
            // established exactly by WaterDriftLinesTests, so nothing rests on this being tight, while a
            // real default-changing regression lands out at ~28000 px — far outside either bar.
            _sea.SetFloat("_DriftLineFoamDrift", 0f);
            _sea.SetFloat("_DriftLineConvergence", 0f);
            _sea.SetFloat("_DriftLineGrid", 1f);
            Color32[] explicitDefaults = Shoot(null);
            int defaultsDelta = DifferingPixels(again, explicitDefaults);
            Assert.LessOrEqual(defaultsDelta, floorWorst + ClockMarginPx,
                $"the three new knobs at their defaults changed {defaultsDelta} px, more than " +
                $"{ClockMarginPx} px beyond the worst measured clock frame of {floorWorst} px " +
                $"(samples: {string.Join(", ", floorSamples)}) — that is more than the clock, so the " +
                "passthrough is not clean.");

            long shippedEnergy = LaneEnergy(shipped, off);
            Assert.Greater(shippedEnergy, 500,
                "dialling the layer in must actually put lanes on screen, or the probe is measuring " +
                "the gates rather than the lanes.");

            // (2) the SHARED-DRIFT basis. The wind is 90° off the current here, so swinging the basis
            // onto FoamDriftDir must visibly re-orient the lanes.
            //     Stated against the TYPICAL floor plus an ABSOLUTE margin. This is the assert that
            //     cried wolf four times in two days, and the multiplier was the reason: load raises the
            //     floor and the basis delta by the same clock term, so at multiplier one it cancels
            //     instead of being tripled against a signal that barely moves.
            _sea.SetFloat("_DriftLineFoamDrift", 1f);
            Color32[] sharedDrift = Shoot("2-shared-drift");
            int basisDelta = DifferingPixels(explicitDefaults, sharedDrift);
            Assert.Greater(basisDelta, floorTypical + ClockMarginPx,
                $"swinging the basis from the raw current onto the shared foam drift (90° apart here) " +
                $"changed {basisDelta} px, which does not clear the {floorTypical} px typical clock " +
                $"frame by the required {ClockMarginPx} px (samples: " +
                $"{string.Join(", ", floorSamples)}) — not enough to be a real re-orientation rather " +
                "than the clock.");
            _sea.SetFloat("_DriftLineFoamDrift", 0f);

            // (3) the CONVERGENCE gate. It is a GATE: it may only ever remove lane energy, never add.
            _sea.SetFloat("_DriftLineConvergence", 1f);
            Color32[] gathered = Shoot("3-convergence");
            long gatheredEnergy = LaneEnergy(gathered, off);
            TestContext.WriteLine(
                $"[drift-line probe] PPU {Ppu}, {Size}x{Size} ({Size * Size} px), chop 0.35. " +
                $"Temporal floor over {FloorSamples} pairs: {string.Join(", ", floorSamples)} px " +
                $"(typical {floorTypical}, worst {floorWorst}); knobs-at-defaults delta " +
                $"{defaultsDelta} px (inside the worst clock frame); shared-drift basis delta " +
                $"{basisDelta} px, clearing {floorTypical} + {ClockMarginPx} by " +
                $"{basisDelta - floorTypical - ClockMarginPx} px. Lane energy — shipped basis " +
                $"{shippedEnergy}, convergence-gated {gatheredEnergy} " +
                $"({100.0 * gatheredEnergy / Mathf.Max(shippedEnergy, 1):0.#}% retained). " +
                "Frames in artifacts/driftlines-*.png");
            Assert.LessOrEqual(gatheredEnergy, shippedEnergy,
                "the convergence weight is a GATE — it must never brighten a lane beyond the noise " +
                "that drew it, or gathered scum reads as a paint splash.");
            Assert.Greater(gatheredEnergy, 0,
                "…but it must not erase every lane either: a converging sea still has drift lines.");
        }
    }
}
