using System.Diagnostics;
using System.Threading;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// THE LOAD-BEARING ASSUMPTION BEHIND THE FLOWER SHADER — that a <see cref="SpriteRenderer"/> keeps its own
    /// per-object matrix (<c>unity_ObjectToWorld</c>) when the URP 2D renderer draws it, rather than having its
    /// geometry baked into world space with an identity matrix.
    ///
    /// <para><b>Why the flowers depend on it.</b> HiddenHarboursFlower.shader picks WHICH of the 4 hand-drawn sway
    /// poses to sample (a quantised <c>frac(uv.x + k/_Cols)</c> column select). That frame index <b>must be
    /// identical for all four vertices of a flower's quad</b> — if two vertices disagree, the flower renders half
    /// in one pose and half in another: a torn flower. The only per-sprite-CONSTANT value available to the vertex
    /// stage is the object origin, so the shader derives its sway phase from
    /// <c>mul(unity_ObjectToWorld, float4(0,0,0,1)).xy</c> rather than from the vertex world position (which is
    /// what the older grass/tree shaders use — they only ever feed it into SMOOTH functions, so an intra-sprite
    /// spread costs them nothing; the tree shader's "no hard phase seam down its trunk" comment is that same
    /// problem dodged rather than solved).</para>
    ///
    /// <para><b>What breaks if this fails.</b> Every flower would read root (0,0), share one phase, and the whole
    /// meadow would flip poses in LOCKSTEP — subtly wrong, easy to misread as "the sway looks cheap", and
    /// invisible to every other test. Hence this guard: it renders two sprites at KNOWN, DIFFERENT world X
    /// through a probe shader and reads the origin back off the frame. Sabotage it by changing the shader to
    /// output a constant and this test fails.</para>
    ///
    /// <para><b>Why it SKIPS on CI, and why that is the right trade.</b> This is the only test in the suite that
    /// actually RENDERS. CI's Unity runs with <c>Renderer: Null Device</c> (no graphics device at all), where
    /// <c>Camera.Render()</c> does not fail a test — it takes the whole editor down with a native crash inside
    /// the render pipeline (<c>RenderTexture.Create failed</c> → <c>CameraScripting::Render</c> → exit code 1 →
    /// NO editmode-results.xml is produced at all). A native crash cannot be caught, so the ONLY fix is to check
    /// for the device and never allocate. It follows that <b>a green CI run does NOT mean this was verified —
    /// it means it was skipped.</b> That is acceptable because the artefact it guards (a torn or lockstep
    /// meadow) is a thing you can only SEE on a machine with a GPU, which is exactly where this test does run
    /// and does bite. Do NOT "fix" the skip by weakening the assertions so they run headless: a probe that does
    /// not render proves nothing at all.</para>
    ///
    /// <para><b>The cold-shader-cache trap (diagnosed 2026-07-19).</b> This test used to FALSE-RED on any machine
    /// whose <c>Library/ShaderCache</c> was cold. The probe shader has <c>Fallback Off</c>, so on the very first
    /// draw — before its variant has finished compiling — URP takes the async-compile placeholder path, which does
    /// NOT supply a per-object matrix. The left probe then decodes as <c>0.0289</c>: EXACTLY what a genuine
    /// regression produces, so the two were indistinguishable and the misleading lockstep-meadow message sent two
    /// agents hunting a bug that was not there. The fixture therefore now WAITS for the variant to compile before
    /// the measuring render (see <see cref="EnsureProbeVariantCompiled"/>) and, if it never does, fails with its
    /// own distinct "probe shader never compiled" message instead of blaming the flowers.</para>
    /// </summary>
    public class SpriteObjectMatrixGuardTests
    {
        private const string ProbeShader = "HiddenHarbours/_SpriteObjectMatrixProbe";

        // The two probe sprites' world X. Chosen far apart and asymmetric so a bug that collapses them to 0,
        // to each other, or swaps them cannot coincidentally pass.
        private const float LeftX = -2f;
        private const float RightX = 3f;

        // A camera renders the whole scene, and EditMode fixtures share one — other tests' leftover GameObjects
        // sat in front of the probe sprites and this read THEIR pixels (it passed alone and failed in-suite until
        // the probe was isolated onto its own layer). Layer 31 is unused by the game; nothing else is on it.
        private const int ProbeLayer = 31;

        /// <summary>
        /// Bail out BEFORE allocating a camera or a <see cref="RenderTexture"/> when there is no graphics device.
        /// This must be the first statement in the test: on a Null Device the crash happens inside the render
        /// pipeline's native code, which no try/catch and no NUnit assertion can intercept — by the time anything
        /// has been allocated it is already too late to fail gracefully.
        /// </summary>
        private static void RequireAGraphicsDevice()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                Assert.Ignore(
                    "SKIPPED, NOT VERIFIED — this run has no graphics device (Renderer: Null Device), so the " +
                    "per-object-matrix guard could not render and proved nothing. This is expected on CI and " +
                    "means a green CI run carries NO evidence about the flower shader's per-instance sway phase. " +
                    "Run the EditMode suite on a machine with a GPU to actually exercise this guard.");
            }
        }

        [Test]
        public void SpriteRenderer_KeepsItsPerObjectMatrix_SoFlowersCanPhasePerInstance()
        {
            RequireAGraphicsDevice();

            var shader = Shader.Find(ProbeShader);
            Assert.IsNotNull(shader, $"The probe shader '{ProbeShader}' is missing. Without it this guard proves " +
                                     "NOTHING about the flower shader's per-instance phase — treat it as a failure.");

            var mat = new Material(shader);
            var sprite = MakeWhiteSprite();

            var camGo = new GameObject("ProbeCam");
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 5f;              // 10 world units tall; the RT is square so 10 wide too
            cam.transform.position = new Vector3(0f, 0f, -10f);
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.cullingMask = 1 << ProbeLayer;      // see ProbeLayer: ignore whatever else the suite left lying about

            var rt = new RenderTexture(64, 64, 0, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;

            // BOTH sprites share ONE material — this is exactly the batched case the flowers ship in.
            var left = MakeProbeSprite(sprite, mat, LeftX);
            var right = MakeProbeSprite(sprite, mat, RightX);

            Texture2D read = null;
            try
            {
                // MUST happen before the measuring render — see the cold-cache paragraph in the class doc.
                EnsureProbeVariantCompiled(cam, mat);

                cam.Render();
                read = ReadBack(rt);

                float leftRoot = DecodeRootX(read, LeftX);
                float rightRoot = DecodeRootX(read, RightX);

                Assert.AreEqual(LeftX, leftRoot, 0.06f,
                    $"The left sprite sits at world x={LeftX} but its shader read its object origin as " +
                    $"{leftRoot:F3}. If this reads ~0, Unity is baking sprite geometry to world space and the " +
                    "flower shader has NO per-flower constant to phase from — the whole meadow will sway in " +
                    "lockstep. See the class doc.");
                Assert.AreEqual(RightX, rightRoot, 0.06f,
                    $"The right sprite sits at world x={RightX} but its shader read its object origin as " +
                    $"{rightRoot:F3}. See the class doc.");
                Assert.AreNotEqual(leftRoot, rightRoot,
                    "Two sprites at different positions read the SAME object origin — per-instance phase is " +
                    "impossible and every flower would sway identically.");
            }
            finally
            {
                RenderTexture.active = null;
                cam.targetTexture = null;
                Object.DestroyImmediate(left);
                Object.DestroyImmediate(right);
                Object.DestroyImmediate(camGo);
                if (read != null) Object.DestroyImmediate(read);
                rt.Release();
                Object.DestroyImmediate(rt);
                Object.DestroyImmediate(mat);
            }
        }

        /// <summary>
        /// Block until the probe shader's variant is actually compiled, so the measuring render cannot land on
        /// URP's async-compile placeholder (which supplies no per-object matrix and decodes as ~0 — the exact value
        /// a genuine regression gives). See the cold-cache paragraph in the class doc.
        ///
        /// <para>Two mechanisms, because neither alone is sufficient. <c>ShaderUtil.CompilePass</c> compiles the
        /// variant implied by the material's CURRENT keyword set, which is not necessarily the one URP picks at draw
        /// time; so after that we also RENDER and then wait for <c>anythingCompiling</c> to settle, repeating until
        /// a render triggers no further compilation. That second loop is what actually pins the render-time variant.
        /// These are warm-up renders into the same target — the measuring render happens after we return.</para>
        /// </summary>
        private static void EnsureProbeVariantCompiled(Camera cam, Material mat)
        {
            const double TimeoutSeconds = 120.0;
            const int MaxWarmUpRenders = 8;

            var clock = Stopwatch.StartNew();

            // 1. Force the material's own variant through the compiler synchronously.
            int passes = mat.passCount > 0 ? mat.passCount : 1;
            for (int pass = 0; pass < passes; pass++)
                ShaderUtil.CompilePass(mat, pass, true);
            WaitForCompilerToSettle(clock, TimeoutSeconds);

            // 2. Warm-up renders until one of them stops asking the compiler for anything new.
            int renders = 0;
            for (; renders < MaxWarmUpRenders; renders++)
            {
                cam.Render();
                if (!ShaderUtil.anythingCompiling) break;
                WaitForCompilerToSettle(clock, TimeoutSeconds);
            }

            if (ShaderUtil.anythingCompiling || renders >= MaxWarmUpRenders)
            {
                Assert.Fail(
                    "PROBE SHADER NEVER COMPILED — this is NOT a flower-shader regression. After " +
                    $"{renders} warm-up render(s) and {clock.Elapsed.TotalSeconds:F1}s of waiting, Unity was still " +
                    "compiling shader variants, so the measuring render would have gone through URP's " +
                    "async-compile placeholder path (no per-object matrix, decodes as ~0.029 — identical to the " +
                    "real bug). The per-object-matrix guard proved NOTHING this run. Re-run the suite once the " +
                    "shader cache is warm; if it never settles, the probe shader " +
                    "(Assets/Tests/EditMode/Art/Shaders/SpriteObjectMatrixProbe.shader) is failing to compile at " +
                    "all — check the console for its compiler errors. Do NOT go looking at " +
                    "HiddenHarboursFlower.shader on the strength of this message.");
            }
        }

        /// <summary>
        /// Spin until <c>ShaderUtil.anythingCompiling</c> goes false (the compiler runs in its own worker
        /// processes, so sleeping here does not starve it) or the shared budget runs out. Returning on timeout is
        /// deliberate — the caller re-checks and owns the failure message.
        /// </summary>
        private static void WaitForCompilerToSettle(Stopwatch clock, double timeoutSeconds)
        {
            while (ShaderUtil.anythingCompiling && clock.Elapsed.TotalSeconds < timeoutSeconds)
                Thread.Sleep(25);
        }

        /// <summary>
        /// Read the encoded origin back out of the rendered frame at <paramref name="worldX"/>. The probe writes
        /// a LINEAR value; the ARGB32 render target stores it sRGB-ENCODED (this project renders in linear colour
        /// space), so the byte must be linearised before it is decoded — skipping that step silently skews the
        /// answer (root.x = -2 reads back as 0.84).
        /// </summary>
        private static float DecodeRootX(Texture2D frame, float worldX)
        {
            // world x -> pixel: (x / 10 + 0.5) * 64. The sprite is 1 unit (6.4 px) wide, so the centre is inside it.
            int px = Mathf.RoundToInt((worldX / 10f + 0.5f) * 64f);
            Color c = frame.GetPixel(px, 32);
            float linear = Mathf.GammaToLinearSpace(c.r);
            return (linear - 0.5f) * 10f;
        }

        private static Texture2D ReadBack(RenderTexture rt)
        {
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            return tex;
        }

        private static Sprite MakeWhiteSprite()
        {
            var tex = new Texture2D(4, 4);
            var px = new Color[16];
            for (int i = 0; i < px.Length; i++) px[i] = Color.white;
            tex.SetPixels(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);   // 1 world unit wide
        }

        private static GameObject MakeProbeSprite(Sprite sprite, Material mat, float x)
        {
            var go = new GameObject("ProbeSprite");
            go.layer = ProbeLayer;
            go.transform.position = new Vector3(x, 0f, 0f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sharedMaterial = mat;
            return go;
        }
    }
}
