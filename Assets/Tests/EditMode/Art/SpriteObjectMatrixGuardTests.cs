using NUnit.Framework;
using UnityEngine;

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

        [Test]
        public void SpriteRenderer_KeepsItsPerObjectMatrix_SoFlowersCanPhasePerInstance()
        {
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
