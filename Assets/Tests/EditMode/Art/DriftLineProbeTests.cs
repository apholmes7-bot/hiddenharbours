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
    /// TEMPORAL FLOOR first and states every claim against it.
    ///
    /// <para>So: BYTE-identity at a fixed clock is established by <c>WaterDriftLinesTests</c>'
    /// EXACT-equality assertions plus the two unreachable <c>if</c> guards in the shader. This probe
    /// proves the complementary thing a twin cannot — that on a GPU, through the real material, the
    /// defaults produce no VISIBLE change beyond the clock, and that the dials produce a large one.
    /// The shipped look needs neither argument: <c>_DriftLineStrength</c> is 0, so the whole helper
    /// returns before it reaches any new code.</para></para>
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

            // (0) the shipped state: the layer is OFF, so the whole helper returns early.
            Color32[] off = Shoot("0-off");

            // ---- THE TEMPORAL NOISE FLOOR, measured first ---------------------------------------
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
            _sea.SetFloat("_DriftLineStrength", 0.6f);
            Color32[] shipped = Shoot("1-dialled-in-defaults");
            Color32[] again = Shoot(null);                       // nothing changed: pure clock
            int temporalFloor = DifferingPixels(shipped, again);

            // (1) the three NEW knobs, set explicitly to their own defaults, one frame later — so the
            // clock gap matches the floor's. Any difference must sit inside the floor.
            _sea.SetFloat("_DriftLineFoamDrift", 0f);
            _sea.SetFloat("_DriftLineConvergence", 0f);
            _sea.SetFloat("_DriftLineGrid", 1f);
            Color32[] explicitDefaults = Shoot(null);
            int defaultsDelta = DifferingPixels(again, explicitDefaults);
            Assert.LessOrEqual(defaultsDelta, temporalFloor + 64,
                $"the three new knobs at their defaults changed {defaultsDelta} px against a measured " +
                $"temporal floor of {temporalFloor} px — that is more than the clock, so the " +
                "passthrough is not clean.");

            long shippedEnergy = LaneEnergy(shipped, off);
            Assert.Greater(shippedEnergy, 500,
                "dialling the layer in must actually put lanes on screen, or the probe is measuring " +
                "the gates rather than the lanes.");

            // (2) the SHARED-DRIFT basis. The wind is 90° off the current here, so swinging the basis
            // onto FoamDriftDir must visibly re-orient the lanes.
            _sea.SetFloat("_DriftLineFoamDrift", 1f);
            Color32[] sharedDrift = Shoot("2-shared-drift");
            int basisDelta = DifferingPixels(explicitDefaults, sharedDrift);
            Assert.Greater(basisDelta, temporalFloor * 3 + 512,
                $"swinging the basis from the raw current onto the shared foam drift (90° apart here) " +
                $"changed {basisDelta} px against a {temporalFloor} px temporal floor — not enough to " +
                "be a real re-orientation rather than the clock.");
            _sea.SetFloat("_DriftLineFoamDrift", 0f);

            // (3) the CONVERGENCE gate. It is a GATE: it may only ever remove lane energy, never add.
            _sea.SetFloat("_DriftLineConvergence", 1f);
            Color32[] gathered = Shoot("3-convergence");
            long gatheredEnergy = LaneEnergy(gathered, off);
            TestContext.WriteLine(
                $"[drift-line probe] PPU {Ppu}, {Size}x{Size}, chop 0.35. Temporal floor {temporalFloor} px; " +
                $"knobs-at-defaults delta {defaultsDelta} px (inside the floor); shared-drift basis " +
                $"delta {basisDelta} px. Lane energy — shipped basis {shippedEnergy}, " +
                $"convergence-gated {gatheredEnergy} " +
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
