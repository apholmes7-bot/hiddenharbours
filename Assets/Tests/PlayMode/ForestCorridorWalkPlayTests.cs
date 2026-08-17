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
    /// ⭐ <b>THE WALK — she comes down a cut corridor, steps off into the trees, and stays readable.</b>
    ///
    /// <para>This is the test PR 1 (#550) deferred on purpose: the silhouette shipped, but there was
    /// nothing dense enough to walk into and no corridor to walk down, so the only honest thing to
    /// measure was a single fisher behind a single leaf
    /// (<see cref="FoliageSilhouettePlayTests"/> — still the shader's adjudicator). The 2026-08-16 density
    /// ruling built the woods and cut the lanes, so the walk can now happen.</para>
    ///
    /// <para><b>What this adds that a static probe cannot.</b> Three things, and each is a way the
    /// silhouette could be broken while #550's tests stayed green:
    /// <list type="number">
    /// <item><b>She MOVES.</b> #550 never moved her, so a mask source that registered her position once
    /// at spawn and never synced again would pass it. Here the mask has to follow her.</item>
    /// <item><b>She is off-centre in the world.</b> A mask that is right only where she happened to
    /// spawn — a viewport/world mix-up, say — reads correctly at the origin and wrongly everywhere
    /// else.</item>
    /// <item><b>The canopy thickens under her as she goes.</b> The claim is not "one leaf is survivable"
    /// but "the deepest thing the lots plant is", which is the claim the owner's ruling actually
    /// makes.</item>
    /// </list></para>
    ///
    /// <para><b>⚠️ GPU-GATED.</b> CI has no graphics device and these skip loudly there; the RTX 4060 run
    /// cited on the PR is the adjudicator. A green CI on this file measured nothing — read the skips.</para>
    ///
    /// <para><b>⚠️ THE STAND IS DENSER THAN THE SHIPPED LOTS, deliberately, and here is the seam.</b> The
    /// real spacing is <c>StPetersWoodlandZones.LotCoreSpacingMetres</c> (3.6 m) and the real tread is
    /// <c>StPetersWoods.PathClearance</c> (4 m) — but both live in <c>HiddenHarbours.App.Editor</c>, and a
    /// PlayMode test assembly must not reference an EDITOR assembly (it has no
    /// <c>includePlatforms</c>, so that reference would follow a player build to a machine where
    /// App.Editor does not exist). So this fixture declares its own numbers, and
    /// <see cref="TrunkSpacingMetres"/> is set BELOW the shipped one on purpose: passing here is a
    /// conservative bound on the density that actually ships, and the EditMode class
    /// <c>StPetersWoodlandZoneTests</c> is what holds the shipped numbers to the table.</para>
    /// </summary>
    public class ForestCorridorWalkPlayTests
    {
        const string LitSpriteShader = "HiddenHarbours/LitSprite";

        /// <summary>Trunk spacing (m) of the stand she walks into. <b>Below</b> the shipped
        /// <c>StPetersWoodlandZones.LotCoreSpacingMetres</c> of 3.6 m — see the class remarks: a denser
        /// stand here makes this a conservative bound on the one that ships.</summary>
        const float TrunkSpacingMetres = 3.0f;

        /// <summary>Half-width (m) of the cut tread she starts on. The shipped corridor is
        /// <c>StPetersWoods.PathClearance</c>, 4 m, which "leaves trunks a stride either side with the
        /// crowns still closing overhead" — so a NARROWER tread here is again the harder case.</summary>
        const float TreadHalfWidthMetres = 3.0f;

        /// <summary>How far a crown reaches from its trunk (m). Wider than half the spacing, so crowns
        /// OVERLAP — that is what a closed canopy is, and it means even the tread has leaves over it,
        /// exactly as <c>PathClearance</c>'s own note says it should.</summary>
        const float CrownRadiusMetres = 2.4f;

        /// <summary>How far into the trees she walks, in metres off the tread's centre-line.</summary>
        const float WalkDepthMetres = 18f;

        /// <summary>How many positions the walk is sampled at. Enough that the canopy over her changes
        /// between them rather than being one reading twice.</summary>
        const int WalkSteps = 7;

        // A tint nothing else in the frame is near, so "did the silhouette land?" is one channel.
        static readonly Color SilhouetteTint = new Color(1f, 0f, 0f, 1f);
        static readonly Color CanopyColour = new Color(0f, 0.6f, 0f, 1f);

        readonly List<Object> _cleanup = new List<Object>();
        GameConfig _prevConfig;
        Camera _camera;
        RenderTexture _target;
        GameObject _fisher;
        GameObject _stand;           // the wood's root, so the cost probe can thin it in place
        Sprite _shared;

        [SetUp]
        public void SetUp() => _prevConfig = GameServices.Config;

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
                Assert.Ignore("No graphics device (CI). The walk is adjudicated on the RTX 4060 run cited " +
                              "on the PR — a green here measured nothing.");
        }

        // =================================================================================
        //  the fixture: a stand with a lane cut through it, and a fisher to walk it
        // =================================================================================

        /// <summary>
        /// A wood with a corridor cut through it, drawn through the real production shader.
        ///
        /// <para>The camera FOLLOWS her, so the pixel under examination is always the centre one — the
        /// same trick #550's probe uses, and the reason this fixture can ask about six different world
        /// positions without six different viewport calculations to get wrong.</para>
        /// </summary>
        void BuildStand(bool silhouetteOn)
        {
            var cfg = ScriptableObject.CreateInstance<GameConfig>();
            cfg.FoliageSilhouette = silhouetteOn;
            cfg.FoliageSilhouetteTint = SilhouetteTint;
            cfg.FoliageSilhouetteStrength = 0.8f;
            _cleanup.Add(cfg);
            GameServices.Config = cfg;

            // ⚠ A listener, or a play-mode scene logs "There are no audio listeners" on EVERY frame and a
            // full suite writes it 1.6 million times.
            _cleanup.Add(new GameObject("WalkRig", typeof(AudioListener)));

            _target = new RenderTexture(64, 64, 24, RenderTextureFormat.ARGB32) { name = "HHWalkProbe" };
            _target.Create();

            var camGo = new GameObject("ProbeCamera");
            _cleanup.Add(camGo);
            _camera = camGo.AddComponent<Camera>();
            _camera.orthographic = true;
            _camera.orthographicSize = 4f;
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = Color.black;
            _camera.targetTexture = _target;

            _shared = SolidSprite();

            // ---- the fisher, on the tread's centre-line ----------------------------------------
            // ⚠ Deliberately NOT at the world origin — see the class remarks: a mask that confuses
            // viewport space for world space is right at the origin and wrong everywhere else.
            _fisher = new GameObject("Player");
            _cleanup.Add(_fisher);
            var body = _fisher.AddComponent<SpriteRenderer>();
            body.sprite = _shared;
            body.color = Color.white;
            body.sortingOrder = 0;
            _fisher.transform.position = new Vector3(37f, -11f, 0f);
            _fisher.transform.localScale = Vector3.one * 1.6f;
            PlayerSilhouetteInstaller.Attach(_fisher);

            // ---- the wood, with the lane cut out of it -----------------------------------------
            var mat = new Material(Shader.Find(LitSpriteShader)) { hideFlags = HideFlags.HideAndDontSave };
            mat.SetColor("_Color", CanopyColour);
            _cleanup.Add(mat);

            _stand = new GameObject("WoodLot");
            _cleanup.Add(_stand);

            int n = Mathf.CeilToInt((WalkDepthMetres + 8f) / TrunkSpacingMetres);
            for (int ix = -n; ix <= n; ix++)
            for (int iy = -n; iy <= n; iy++)
            {
                // The tread runs along X, so the cut is a band of constant Y — the same shape a capsule
                // lane has, and the same rule the real cut applies: no TRUNK in the tread. Crowns still
                // close over it, which is what PathClearance's own note says a lane in woods looks like.
                float offY = iy * TrunkSpacingMetres;
                if (Mathf.Abs(offY) < TreadHalfWidthMetres) continue;

                var go = new GameObject($"Tree_{ix}_{iy}");
                go.transform.SetParent(_stand.transform, worldPositionStays: false);
                go.transform.position = new Vector3(37f + ix * TrunkSpacingMetres, -11f + offY, 0f);
                go.transform.localScale = Vector3.one * (CrownRadiusMetres * 2f);

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = _shared;                 // ONE sprite for the stand, so it batches
                sr.sharedMaterial = mat;
                // Sorting IS the "is it in front?" question (SilhouetteThrough's header). Everything here
                // draws AFTER her, which is what a tree between her and the camera does.
                sr.sortingOrder = 10 + ((ix + iy) & 3);
            }
        }

        /// <summary>Where the walk samples: from the middle of the tread straight off into the trees.
        /// Step 0 is on the centre-line, the last step is <see cref="WalkDepthMetres"/> deep.</summary>
        static IEnumerable<float> WalkOffsets()
        {
            for (int i = 0; i < WalkSteps; i++)
                yield return WalkDepthMetres * i / (WalkSteps - 1f);
        }

        Sprite SolidSprite()
        {
            var tex = new Texture2D(8, 8, TextureFormat.RGBA32, mipChain: false) { filterMode = FilterMode.Point };
            _cleanup.Add(tex);
            var px = new Color[64];
            for (int i = 0; i < px.Length; i++) px[i] = Color.white;
            tex.SetPixels(px);
            tex.Apply(false);
            var sprite = Sprite.Create(tex, new Rect(0, 0, 8, 8), new Vector2(0.5f, 0.5f), 8f);
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

        /// <summary>Put her one step along the walk, point the camera at her, and let the mask catch up.
        /// Two frames: one for the source to register and sync, one whose mask the canopy reads — #550's
        /// own note, and the reason a single-frame version of this test reads a stale mask.</summary>
        IEnumerator StepTo(float offsetMetres)
        {
            Vector3 at = new Vector3(37f, -11f + offsetMetres, 0f);
            _fisher.transform.position = at;
            _camera.transform.position = new Vector3(at.x, at.y, -10f);
            yield return null;
            yield return null;
        }

        // =================================================================================
        //  the acceptance
        // =================================================================================

        [UnityTest]
        public IEnumerator SheReadsAtEveryStepOfTheWalkFromTheLaneIntoTheTrees()
        {
            SkipIfNoGpu();
            BuildStand(silhouetteOn: true);

            var readings = new List<string>();
            foreach (float off in WalkOffsets())
            {
                yield return StepTo(off);
                Color pixel = CentrePixel();
                readings.Add($"{off:0.#} m → {pixel}");

                Assert.Greater(pixel.r, 0.4f,
                    $"At {off:0.#} m off the lane's centre-line the fisher does not read (pixel {pixel}). " +
                    $"The stand around her is at {TrunkSpacingMetres:0.#} m spacing with " +
                    $"{CrownRadiusMetres:0.#} m crowns, all sorted in front of her — denser than anything " +
                    "the shipped wood lots plant. There is no build in which deep woods may lose her " +
                    $"(owner ruling, 2026-08-16). Walk so far: {string.Join(" | ", readings)}");
            }

            Debug.Log("[ForestCorridorWalkPlayTests] the walk: " + string.Join(" | ", readings));
        }

        /// <summary>
        /// ⚠️ THE SABOTAGE ARM, and it is what makes the walk mean anything. Take the dial away and the
        /// canopy must SWALLOW her over most of the walk. If she stays visible with the silhouette off,
        /// then the trees were never actually over her and the acceptance above is measuring an empty
        /// wood — the commonest way a rendering guard in this repo stops guarding.
        /// </summary>
        [UnityTest]
        public IEnumerator Sabotage_WithTheDialDown_TheStandSwallowsHer()
        {
            SkipIfNoGpu();
            BuildStand(silhouetteOn: false);

            int lost = 0, total = 0;
            var readings = new List<string>();
            foreach (float off in WalkOffsets())
            {
                yield return StepTo(off);
                Color pixel = CentrePixel();
                readings.Add($"{off:0.#} m → {pixel}");
                total++;

                // Lost = the LEAF is what the pixel carries, and none of the tint.
                if (pixel.r < 0.1f && pixel.g > 0.3f) lost++;
            }

            Assert.That(lost * 2, Is.GreaterThan(total),
                $"SABOTAGE NOT DETECTED: with the silhouette OFF the fisher was swallowed at only {lost} " +
                $"of {total} steps, so for most of this walk the canopy is not in front of her at all and " +
                $"the acceptance is measuring an empty wood. Readings: {string.Join(" | ", readings)}");

            Debug.Log($"[ForestCorridorWalkPlayTests] sabotage arm: swallowed at {lost}/{total} steps — " +
                      string.Join(" | ", readings));
        }

        // =================================================================================
        //  the cost of the new density, MEASURED (rule 7 — "performance budget is a feature")
        // =================================================================================

        /// <summary>
        /// The draw-call ceiling at the FULL stand. Foliage of one species shares one material, so a stand
        /// is supposed to BATCH — draw calls must be governed by how many distinct (material, sorting)
        /// groups there are, not by how many trees. Generous, because the editor also counts the probe
        /// camera's clear and the URP passes themselves.
        /// </summary>
        const int MaxDrawCallsAtFullDensity = 60;

        /// <summary>
        /// <b>What the denser woods cost, on the meter.</b> The handoff asks for foliage draw calls
        /// before/after at the new density on this machine, so this measures them rather than reasoning
        /// about them. The stand is thinned in place — same scene, same material, same camera, only the
        /// number of visible trees moving — so the readings differ by exactly the thing being measured.
        ///
        /// <para><b>The load-bearing claim is that foliage BATCHES.</b> Both regions' trees draw from one
        /// shared Acadian material, so multiplying the trees must not multiply the draw calls. If it does,
        /// the density ruling is unaffordable at any radius and the number to change is
        /// <c>StPetersWoodlandZones.LotCoreSpacingMetres</c>, not this ceiling.</para>
        ///
        /// <para>Frame time is logged but deliberately NOT asserted: batchmode timings on a shared machine
        /// are noisy enough that a threshold would ship a flake — #550's own note, and its own reasoning.</para>
        /// </summary>
        [UnityTest]
        public IEnumerator TheDenserWoodsStillBatch()
        {
            SkipIfNoGpu();
#if UNITY_EDITOR
            BuildStand(silhouetteOn: true);

            var trees = new List<GameObject>();
            foreach (Transform child in _stand.transform) trees.Add(child.gameObject);
            Assert.That(trees.Count, Is.GreaterThan(100),
                $"the probe stand is only {trees.Count} trees, which is fewer than either region plants — " +
                "there is nothing here to measure a density claim against");

            // Descending, so the full stand (the case that matters) is measured on a warm frame rather
            // than on the first one after the scene is built.
            var readings = new List<string>();
            int atFull = 0, atQuarter = 0;

            foreach (int visible in new[] { trees.Count, trees.Count / 2, trees.Count / 4 })
            {
                for (int i = 0; i < trees.Count; i++) trees[i].SetActive(i < visible);
                yield return null; yield return null; yield return null;

                int calls = UnityEditor.UnityStats.drawCalls;
                readings.Add($"{visible} foliage sprites → {calls} draw calls, " +
                             $"{Time.unscaledDeltaTime * 1000f:0.00} ms");
                if (visible == trees.Count) atFull = calls;
                if (visible == trees.Count / 4) atQuarter = calls;
            }

            Debug.Log("[ForestDensity perf] RTX 4060, batchmode, one shared foliage material, " +
                      $"{TrunkSpacingMetres:0.#} m spacing with overlapping {CrownRadiusMetres:0.#} m " +
                      "crowns: " + string.Join(" | ", readings) +
                      ". Frame times are indicative only — batchmode timings are noisy.");

            if (atFull <= 0)
                Assert.Ignore("UnityStats.drawCalls reported 0 in batchmode, so there is nothing to " +
                              "compare. Re-measure in the editor before quoting a number on the PR — do " +
                              "NOT read this skip as a pass.");

            Assert.LessOrEqual(atFull, MaxDrawCallsAtFullDensity,
                $"{trees.Count} foliage sprites on ONE shared material cost {atFull} draw calls. Foliage " +
                "is supposed to batch — if the count scales with the trees then the 2026-08-16 density " +
                "ruling cannot be afforded at any radius, and the honest fix is to thin the lots rather " +
                "than to raise this ceiling.");

            // ⭐ THE ARM THAT MAKES THE CEILING MEAN SOMETHING: four times the trees must not cost four
            // times the calls. Without this, a ceiling of 60 would pass just as happily on a scene that
            // never batched at all but happened to be small.
            Assert.Less(atFull, atQuarter * 2,
                $"quadrupling the stand ({trees.Count / 4} → {trees.Count} sprites) took draw calls from " +
                $"{atQuarter} to {atFull}. That is not batching, and a forest that costs a draw call a " +
                "tree is a forest this project cannot have (rule 7).");
#else
            yield return null;
            Assert.Ignore("The draw-call counter is an editor-only measurement.");
#endif
        }
    }
}
