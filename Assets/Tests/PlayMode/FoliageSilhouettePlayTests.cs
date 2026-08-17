using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using HiddenHarbours.Art;
using HiddenHarbours.Core;
using Object = UnityEngine.Object;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// <b>THE ADJUDICATOR for the foliage silhouette</b> (owner ruling, 2026-08-16: dense forest must
    /// never lose the player). Every other guard in this arc is CPU state — the dial reaches the
    /// global, the include carries the read, the feature is on the renderer. None of them can tell you
    /// whether a fisher standing behind a spruce is actually visible, because that is a question about
    /// PIXELS. This class renders the thing and looks.
    ///
    /// <para><b>⚠️ GPU-GATED, and that is not a weakness.</b> CI runs Unity with no graphics device and
    /// these skip loudly there ([[ci-unity-has-no-graphics-device]]); the RTX 4060 run cited on the PR
    /// is the adjudicator, exactly as it was for the mesh-hull acceptance. A green CI on this file
    /// means nothing was measured — read the skip count.</para>
    ///
    /// <para><b>The sabotage arm is the point.</b> <see cref="WithTheDialDown_TheFisherIsGenuinelyLost"/>
    /// takes the silhouette away and requires the canopy to swallow her completely. Without it, a test
    /// that merely asserts "she is visible" would pass just as happily against a canopy that was never
    /// in front of her in the first place — which is the commonest way a rendering guard stops
    /// guarding in this repo.</para>
    /// </summary>
    public class FoliageSilhouettePlayTests
    {
        const string LitSpriteShader = "HiddenHarbours/LitSprite";

        // A tint nothing else in the frame is near, so "did the silhouette land?" is answered by one
        // channel rather than by a delta against a near-white default.
        static readonly Color SilhouetteTint = new Color(1f, 0f, 0f, 1f);
        static readonly Color CanopyColour = new Color(0f, 0.6f, 0f, 1f);

        readonly List<Object> _cleanup = new List<Object>();
        GameConfig _prevConfig;
        Camera _camera;
        RenderTexture _target;
        GameConfig _cfg;             // this fixture's live dial, so a probe can flip it mid-scene
        Material _canopyMat;         // the canopy's shared material, so a dense stand batches
        GameObject _fisher;          // so the cost probe can add her silhouette AFTER its baseline

        [SetUp]
        public void SetUp()
        {
            _prevConfig = GameServices.Config;
        }

        [TearDown]
        public void TearDown()
        {
            GameServices.Config = _prevConfig;
            SilhouetteRegistry.BindIdle();
            if (_target != null) { _target.Release(); Object.Destroy(_target); _target = null; }
            foreach (Object o in _cleanup)
                if (o != null) Object.Destroy(o);
            _cleanup.Clear();
        }

        static void SkipIfNoGpu()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                Assert.Ignore("No graphics device (CI). The silhouette is adjudicated on the RTX 4060 " +
                              "run cited on the PR — a green here measured nothing.");
        }

        // =================================================================================
        //  the fixture: a fisher, and one leaf directly in front of her
        // =================================================================================

        /// <summary>
        /// Build the smallest scene that can answer the question: a solid body, and a canopy quad that
        /// covers her and SORTS IN FRONT. The canopy draws through the real production shader with the
        /// real production material set-up — a transient material of its own, never a shipped .mat
        /// (writing to a sharedMaterial dirties the ASSET).
        /// </summary>
        void BuildScene(bool silhouetteOn, float strength, bool attachSource = true)
        {
            var cfg = ScriptableObject.CreateInstance<GameConfig>();
            cfg.FoliageSilhouette = silhouetteOn;
            cfg.FoliageSilhouetteTint = SilhouetteTint;
            cfg.FoliageSilhouetteStrength = strength;
            _cleanup.Add(cfg);
            GameServices.Config = cfg;
            _cfg = cfg;

            // A listener, or a play-mode scene logs "There are no audio listeners" on EVERY frame and
            // a full suite writes it 1.6 million times.
            var rig = new GameObject("SilhouetteRig", typeof(AudioListener));
            _cleanup.Add(rig);

            _target = new RenderTexture(64, 64, 24, RenderTextureFormat.ARGB32) { name = "HHSilhouetteProbe" };
            _target.Create();

            var camGo = new GameObject("ProbeCamera");
            _cleanup.Add(camGo);
            _camera = camGo.AddComponent<Camera>();
            _camera.orthographic = true;
            _camera.orthographicSize = 1f;
            _camera.transform.position = new Vector3(0f, 0f, -10f);
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = Color.black;
            _camera.targetTexture = _target;

            // ---- the fisher -------------------------------------------------------------------
            var fisher = new GameObject("Player");
            _cleanup.Add(fisher);
            var body = fisher.AddComponent<SpriteRenderer>();
            body.sprite = SolidSprite(Color.white);
            body.sortingOrder = 0;
            _fisher = fisher;
            if (attachSource) PlayerSilhouetteInstaller.Attach(fisher);

            // ---- the canopy, covering her and drawn AFTER her ----------------------------------
            // Sorting is the whole "is it in front?" test — see SilhouetteThrough's header. A higher
            // sortingOrder draws later, which is what a tree standing between her and the camera does.
            var canopy = new GameObject("Canopy");
            _cleanup.Add(canopy);
            var leaf = canopy.AddComponent<SpriteRenderer>();
            leaf.sprite = SolidSprite(Color.white);
            leaf.sortingOrder = 10;
            var mat = new Material(Shader.Find(LitSpriteShader)) { hideFlags = HideFlags.HideAndDontSave };
            mat.SetColor("_Color", CanopyColour);
            _cleanup.Add(mat);
            leaf.sharedMaterial = mat;
            _canopyMat = mat;
        }

        /// <summary>
        /// Thicken the canopy to <paramref name="count"/> sprites, all on the SHARED material so they
        /// batch the way a real stand of one species does. Spread across the whole view and deliberately
        /// OVERLAPPING — overdraw is what a dense canopy actually costs, and a probe that laid them out
        /// side by side would measure the easy case.
        /// </summary>
        void ThickenCanopy(int count)
        {
            Sprite shared = SolidSprite(Color.white);   // ONE sprite for the whole stand, not one each
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject($"Leaf{i}");
                _cleanup.Add(go);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = shared;
                sr.sharedMaterial = _canopyMat;
                sr.sortingOrder = 10 + (i % 4);
                // A deterministic scatter across the 2-unit view; no Random, so the probe is repeatable.
                float fx = ((i * 37) % 101) / 101f - 0.5f;
                float fy = ((i * 53) % 97) / 97f - 0.5f;
                go.transform.position = new Vector3(fx * 2f, fy * 2f, 0f);
                go.transform.localScale = Vector3.one * 0.35f;
            }
        }

        Sprite SolidSprite(Color fill)
        {
            var tex = new Texture2D(8, 8, TextureFormat.RGBA32, mipChain: false) { filterMode = FilterMode.Point };
            _cleanup.Add(tex);
            var px = new Color[64];
            for (int i = 0; i < px.Length; i++) px[i] = fill;
            tex.SetPixels(px);
            tex.Apply(false);
            // Centre pivot and a PPU that makes the 8 px cell fill the 2-unit ortho view.
            var sprite = Sprite.Create(tex, new Rect(0, 0, 8, 8), new Vector2(0.5f, 0.5f), 4f);
            _cleanup.Add(sprite);
            return sprite;
        }

        Color CentrePixel()
        {
            var readback = new Texture2D(_target.width, _target.height, TextureFormat.RGBA32, false);
            _cleanup.Add(readback);
            RenderTexture prev = RenderTexture.active;
            RenderTexture.active = _target;
            readback.ReadPixels(new Rect(0, 0, _target.width, _target.height), 0, 0);
            readback.Apply(false);
            RenderTexture.active = prev;
            return readback.GetPixel(_target.width / 2, _target.height / 2);
        }

        // =================================================================================
        //  the acceptance
        // =================================================================================

        [UnityTest]
        public IEnumerator TheFisherReadsThroughACanopyThatIsInFrontOfHer()
        {
            SkipIfNoGpu();
            BuildScene(silhouetteOn: true, strength: 0.8f);

            // Two frames: one for the source to register and sync, one whose mask the canopy reads.
            yield return null;
            yield return null;

            Color pixel = CentrePixel();

            Assert.Greater(pixel.r, 0.4f,
                $"The fisher does not read through the canopy (pixel {pixel}). The canopy is drawn " +
                "AFTER her and covers her completely, so with the silhouette on this pixel must be " +
                "pulled a long way toward the tint. She is lost behind the trees — which is the one " +
                "thing the owner's ruling forbids.");
            Assert.Greater(pixel.r, pixel.g,
                $"the silhouette tint must DOMINATE the leaf colour at 0.8 strength (pixel {pixel})");
        }

        /// <summary>
        /// ⚠️ THE SABOTAGE ARM. Take the dial down and the canopy must swallow her completely — which
        /// proves the canopy really was in front of her, and therefore that the test above measured the
        /// silhouette rather than a gap in the leaves.
        /// </summary>
        [UnityTest]
        public IEnumerator WithTheDialDown_TheFisherIsGenuinelyLost()
        {
            SkipIfNoGpu();
            BuildScene(silhouetteOn: false, strength: 0.8f);

            yield return null;
            yield return null;

            Color pixel = CentrePixel();

            Assert.Less(pixel.r, 0.1f,
                $"SABOTAGE NOT DETECTED: with the silhouette OFF the canopy should hide the fisher " +
                $"completely, but this pixel still carries the tint (pixel {pixel}). Either the dial " +
                "is not reaching the shader, or the canopy was never in front of her — in which case " +
                "the acceptance above is measuring nothing.");
            Assert.Greater(pixel.g, 0.3f,
                $"…and what should be there is the LEAF (pixel {pixel}). A black pixel means the " +
                "canopy did not draw at all, which would make both arms of this pair meaningless.");
        }

        /// <summary>
        /// The other half of the ruling, and the half a "make her visible" implementation gets wrong:
        /// foliage stays OPAQUE. The silhouette must not turn the canopy transparent — the leaf's own
        /// colour has to survive in the mix, or the effect reads as a hole cut in the tree rather than
        /// as the fisher behind it.
        /// </summary>
        [UnityTest]
        public IEnumerator TheCanopyStaysOpaque_AtTheShippedStrength()
        {
            SkipIfNoGpu();
            BuildScene(silhouetteOn: true, strength: GameConfig.DefaultFoliageSilhouetteStrength);

            yield return null;
            yield return null;

            Color pixel = CentrePixel();

            Assert.Greater(pixel.r, 0.2f,
                $"at the SHIPPED strength she must still be findable at a glance (pixel {pixel})");
            Assert.Greater(pixel.g, 0.1f,
                $"…and the leaf must still be a leaf (pixel {pixel}). Foliage stays opaque and draws " +
                "in front; the silhouette reads THROUGH it. A canopy with none of its own colour left " +
                "is a hole, not a tree.");
        }

        // =================================================================================
        //  the cost, MEASURED (rule 7 — "performance budget is a feature")
        // =================================================================================

        /// <summary>Foliage sprites on screen for the cost probe. Comfortably past what either region
        /// puts in one camera frame, and deliberately overlapping, so the number below is measured on
        /// the hard case rather than the tidy one.</summary>
        const int CanopyCount = 1200;

        /// <summary>The claim, as a number. One <c>DrawRendererList</c> of one renderer is one draw
        /// call; the slack allows for the pass's own setup and for the editor counting the mask target's
        /// clear, and is still far below anything that scales with the forest.</summary>
        const int MaxExtraDrawCalls = 6;

        /// <summary>
        /// <b>The design claim, on the meter.</b> The silhouette masks the BODY rather than the FOLIAGE
        /// precisely so its cost cannot grow with the number of trees — which is the number this arc
        /// exists to raise. That is a claim about draw calls, so this counts them, on the same scene,
        /// with only the dial moved between the two readings.
        ///
        /// <para>The numbers are LOGGED as well as asserted, so the PR body quotes a measurement rather
        /// than an intention. Frame time is logged but deliberately NOT asserted: batchmode timings on a
        /// shared machine are noisy enough that a threshold would ship a flake, and the draw-call delta
        /// is the honest gate.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator AtForestDensity_TheSilhouetteDoesNotScaleWithTheForest()
        {
            SkipIfNoGpu();
#if UNITY_EDITOR
            // ⚠️ THE BASELINE IS "NO SOURCE AT ALL", not "the dial down". A scene that already carries a
            // SilhouetteMaskSource records the mask pass whatever the dial says — the dial gates the
            // foliage's READ, not the pass — so measuring dial-off against dial-on would compare two
            // scenes that both pay for the pass and would report a delta of 0 for the wrong reason.
            // This arm is the genuine before-this-PR forest: registry empty, feature enqueues nothing.
            BuildScene(silhouetteOn: true, strength: GameConfig.DefaultFoliageSilhouetteStrength,
                       attachSource: false);
            ThickenCanopy(CanopyCount);

            yield return null; yield return null; yield return null;
            int offCalls = UnityEditor.UnityStats.drawCalls;
            float offMs = Time.unscaledDeltaTime * 1000f;

            // The ONLY thing that changes between readings: the fisher gains her silhouette. Same
            // canopy, same sprites, same materials, same camera.
            PlayerSilhouetteInstaller.Attach(_fisher);
            yield return null; yield return null; yield return null;
            int onCalls = UnityEditor.UnityStats.drawCalls;
            float onMs = Time.unscaledDeltaTime * 1000f;

            Debug.Log($"[FoliageSilhouette perf] {CanopyCount} overlapping foliage sprites, RTX 4060, " +
                      $"batchmode. Draw calls: no-silhouette={offCalls} with-silhouette={onCalls} " +
                      $"(delta {onCalls - offCalls}). Frame: {offMs:0.00} ms -> {onMs:0.00} ms " +
                      "(indicative only — batchmode timings are noisy).");

            if (offCalls <= 0)
                Assert.Ignore("UnityStats.drawCalls reported 0 in batchmode, so there is nothing to " +
                              "compare. Re-measure in the editor before quoting a number on the PR — " +
                              "do NOT read this skip as a pass.");

            Assert.LessOrEqual(onCalls - offCalls, MaxExtraDrawCalls,
                $"The silhouette added {onCalls - offCalls} draw calls over {CanopyCount} foliage " +
                "sprites. The whole reason the BODY is masked and not the foliage is that the cost must " +
                "NOT scale with the forest — if this grows with the canopy, the mechanism has been " +
                "inverted and the density pass will pay for it twice.");
#else
            yield return null;
            Assert.Ignore("The draw-call counter is an editor-only measurement.");
#endif
        }
    }
}
