using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// The ADR 0027 #10 ripple band, RENDERED — because "the shipped sea is byte-identical until the
    /// owner dials it in" is a claim about pixels, and <c>WaterRippleTests</c> only proves the
    /// arithmetic.
    ///
    /// <para><b>What it renders.</b> The real <c>Water.mat</c> on a quad through <c>Camera.Render()</c> at
    /// PPU 32, four times: the shipped state (band OFF), the band dialled in, the band at a WIDE framing,
    /// and the band with no wind. It writes each to <c>artifacts/</c> so the owner can compare by eye,
    /// and asserts the things a screenshot cannot — that the shipped defaults change nothing beyond the
    /// clock, that dialling it in puts real texture on screen, that the framing fade REMOVES energy and
    /// never adds it, and that a windless sea shows none of it.</para>
    ///
    /// <para>⚠️ <b>Why every claim is stated against a measured clock floor.</b> This water is
    /// TIME-DRIVEN and <c>_Time</c> advances between <c>Camera.Render()</c> calls, so two frames of the
    /// SAME material differ. A naive byte-comparison reports that as a broken passthrough. The floor is
    /// therefore sampled over several consecutive pairs (one loaded frame cannot speak for all of them)
    /// and the margin is ADDITIVE, never a multiple — the hardening <c>DriftLineProbeTests</c> paid for
    /// on 2026-07-30, whose reasoning applies verbatim here and is not repeated.</para>
    ///
    /// <para><b>CI cannot adjudicate this.</b> No graphics device there — a render CRASHES the editor
    /// rather than failing cleanly. Gated on <see cref="RequireAGraphicsDevice"/> first, skipping loudly,
    /// the same discipline <c>IsoFacetUrpPassTests</c> and <c>DriftLineProbeTests</c> keep. The band's
    /// laws are adjudicated headless by <c>WaterRippleTests</c>; the tier-invariance the framing fade
    /// rests on is pinned headless by <c>RipplePixelFootprintTests</c>.</para>
    /// </summary>
    public class RippleBandProbeTests
    {
        const int ProbeLayer = 31;
        const int Size = 192;
        const float Ppu = 32f;

        /// <summary>How many consecutive frame pairs the temporal floor is measured over.</summary>
        const int FloorSamples = 5;

        /// <summary>How far a delta must sit from the measured clock-noise band before it means
        /// anything: a QUARTER of the frame, in absolute pixels. Additive, never a multiple — see the
        /// class note and <c>DriftLineProbeTests.ClockMarginPx</c>, which this mirrors.</summary>
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
            // The framing global is a GLOBAL: leaving this fixture's value set would follow the editor
            // into any later probe on the same shader.
            Shader.SetGlobalFloat("_SeaFramingHeight", 0f);
        }

        static void RequireAGraphicsDevice()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore(
                    "SKIPPED, NOT VERIFIED — no graphics device (Null Device), so nothing rendered and " +
                    "nothing was proved. Expected on CI; the ripple probe needs a GPU.");
        }

        void BuildSea()
        {
            var source = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/_Project/Art/Materials/Water.mat");
            Assert.IsNotNull(source, "the hero water material must exist for this probe.");
            _sea = new Material(source) { hideFlags = HideFlags.HideAndDontSave };
            // A breezy, lively sea: the ripple band's wind gate opens by _RippleWindFull (0.45), and a
            // glassy sea deliberately shows none, so a probe at zero wind would measure the gate.
            _sea.SetFloat("_Chop", 0.35f);
            _sea.SetFloat("_Roughness", 0.5f);
            _sea.SetFloat("_Flow", 0.2f);
            _sea.SetVector("_FlowDir", new Vector4(1f, 0f, 0f, 0f));
            _sea.SetVector("_WindDir", new Vector4(0f, 1f, 0f, 0f));
            _sea.SetFloat("_WaterLevel", 0f);
            _sea.SetFloat("_UseHeightTex", 0f);
            _sea.DisableKeyword("_USE_HEIGHTTEX");
            _sea.SetFloat("_PaletteGradeStrength", 0f);   // the guard-rail would compress the deltas
            _sea.SetFloat("_ObjectReflectStrength", 0f);
            _sea.SetFloat("_RippleStrength", 0f);         // the SHIPPED state

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
                File.WriteAllBytes(Path.Combine(dir, $"ripples-{name}.png"), tex.EncodeToPNG());
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

        /// <summary>Total absolute difference from the bare sea — how much ripple is on screen.</summary>
        static long BandEnergy(Color32[] lit, Color32[] bare)
        {
            long e = 0;
            for (int i = 0; i < lit.Length; i++)
                e += Mathf.Abs(lit[i].r - bare[i].r) + Mathf.Abs(lit[i].g - bare[i].g)
                   + Mathf.Abs(lit[i].b - bare[i].b);
            return e;
        }

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

        static int Median(int[] values)
        {
            var sorted = (int[])values.Clone();
            System.Array.Sort(sorted);
            return sorted[sorted.Length / 2];
        }

        static int Max(int[] values)
        {
            int m = values[0];
            for (int i = 1; i < values.Length; i++) if (values[i] > m) m = values[i];
            return m;
        }

        [Test]
        public void RippleBand_PassesThrough_ThenTexturesTheSeaWhenAsked()
        {
            RequireAGraphicsDevice();
            BuildSea();
            Shader.SetGlobalFloat("_SeaFramingHeight", 0f);   // unset = no framing fade

            // WARM THE CACHE and throw the frame away — a cold ShaderCache both inflates the floor and
            // suppresses the signal, so the fixture's first draw must not be one a claim rests on.
            Shoot(null);

            // (0) THE SHIPPED STATE: _RippleStrength is 0 in Water.mat, so the whole block is skipped.
            Color32[] off = Shoot("0-shipped-off");

            // ---- the temporal noise floor, sampled first -----------------------------------------
            int[] floorSamples = SampleTemporalFloor(out Color32[] again);
            int floorTypical = Median(floorSamples);
            int floorWorst = Max(floorSamples);

            // (1) THE PASSTHROUGH PROOF. Set every new knob explicitly to its shipped default — including
            // the strength, which is 0 — one frame on, so the clock gap matches a floor sample's. If any
            // of the new code runs at the defaults, this lands far outside the clock band.
            _sea.SetFloat("_RippleStrength", 0f);
            _sea.SetFloat("_RippleWavelength", 0.12f);
            _sea.SetFloat("_RippleSpeed", 0.09f);
            _sea.SetFloat("_DispersionRippleMult", 0.06f);
            _sea.SetFloat("_RippleWindOnset", 0.05f);
            _sea.SetFloat("_RippleWindFull", 0.45f);
            _sea.SetFloat("_RippleWindwardGate", 0.7f);
            _sea.SetFloat("_RippleLeeFloor", 0.15f);
            _sea.SetFloat("_RippleBands", 3f);
            _sea.SetFloat("_RippleDitherWin", 0.5f);
            _sea.SetFloat("_RippleFadeNear", 16f);
            _sea.SetFloat("_RippleFadeFar", 30f);
            _sea.SetFloat("_RippleFadeFloor", 0.2f);
            Color32[] explicitDefaults = Shoot(null);
            int defaultsDelta = DifferingPixels(again, explicitDefaults);
            Assert.LessOrEqual(defaultsDelta, floorWorst + ClockMarginPx,
                $"the ripple knobs at their shipped defaults changed {defaultsDelta} px, more than " +
                $"{ClockMarginPx} px beyond the worst measured clock frame of {floorWorst} px " +
                $"(samples: {string.Join(", ", floorSamples)}) — that is more than the clock, so the " +
                "passthrough is not clean and the owner's tuned sea has moved.");

            // (2) DIALLED IN — it must actually put texture on the water.
            _sea.SetFloat("_RippleStrength", 0.45f);
            Color32[] dialled = Shoot("1-dialled-in");
            long dialledEnergy = BandEnergy(dialled, off);
            int dialledDelta = DifferingPixels(explicitDefaults, dialled);
            Assert.Greater(dialledDelta, floorTypical + ClockMarginPx,
                $"dialling the band in changed only {dialledDelta} px against a {floorTypical} px typical " +
                $"clock frame — the layer is not reaching the surface.");

            // (3) THE FRAMING FADE. Shot back-to-back so the two frames carry comparable clock terms —
            // 6 m is the deck framing (inside _RippleFadeNear, full band), 90 m is the Coastal Packet's
            // (well past _RippleFadeFar, so the 0.2 floor). Three claims: the fade DOES something beyond
            // the clock, it only ever REMOVES band energy, and the floor stops it deleting the layer.
            Shader.SetGlobalFloat("_SeaFramingHeight", 6f);
            Color32[] tight = Shoot("2-tight-framing");
            Shader.SetGlobalFloat("_SeaFramingHeight", 90f);
            Color32[] wide = Shoot("3-wide-framing");
            long tightEnergy = BandEnergy(tight, off);
            long wideEnergy = BandEnergy(wide, off);
            int fadeDelta = DifferingPixels(tight, wide);
            Assert.Greater(fadeDelta, floorTypical + ClockMarginPx,
                $"swinging the published framing from the 6 m deck view to the 90 m packet view changed " +
                $"{fadeDelta} px, which does not clear the {floorTypical} px typical clock frame by the " +
                $"required {ClockMarginPx} px — the density fade is not reaching the surface.");
            Assert.Less(wideEnergy, tightEnergy,
                "a wide framing must THIN the ripple field — that is the whole point of the density fade.");
            Assert.Greater(wideEnergy, 0,
                "…but the 0.2 floor must leave some of it: the fade thins the band, it does not delete it.");

            // (4) NO WIND, NO RIPPLES. Glass stays glass, whatever the framing.
            Shader.SetGlobalFloat("_SeaFramingHeight", 0f);
            _sea.SetFloat("_Roughness", 0f);
            Color32[] windless = Shoot("4-no-wind");
            _sea.SetFloat("_RippleStrength", 0f);
            Color32[] windlessOff = Shoot(null);
            int windlessDelta = DifferingPixels(windless, windlessOff);

            TestContext.WriteLine(
                $"[ripple probe] PPU {Ppu}, {Size}x{Size} ({Size * Size} px), roughness 0.5, chop 0.35. " +
                $"Temporal floor over {FloorSamples} pairs: {string.Join(", ", floorSamples)} px " +
                $"(typical {floorTypical}, worst {floorWorst}); knobs-at-defaults delta {defaultsDelta} px; " +
                $"dialled-in delta {dialledDelta} px (framing unset, energy {dialledEnergy}). " +
                $"Framing fade — tight (6 m) energy {tightEnergy}, wide (90 m) {wideEnergy} " +
                $"({100.0 * wideEnergy / Mathf.Max(tightEnergy, 1):0.#}% retained), tight-vs-wide delta " +
                $"{fadeDelta} px; windless delta {windlessDelta} px against a {floorWorst} px worst " +
                "clock frame. " +
                "Frames in artifacts/ripples-*.png");

            Assert.LessOrEqual(windlessDelta, floorWorst + ClockMarginPx,
                $"with the wind at zero the band must be gone — glass stays glass — but turning the " +
                $"strength off changed {windlessDelta} px, more than the clock explains.");
        }
    }
}
