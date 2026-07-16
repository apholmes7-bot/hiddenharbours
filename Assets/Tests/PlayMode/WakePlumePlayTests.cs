using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using HiddenHarbours.Boats;

namespace HiddenHarbours.Tests.PlayMode
{
    /// <summary>
    /// Runtime proof for the graded-wake wiring the EditMode math can't cover: the graded plume sprites actually
    /// LOAD at runtime through the spriteMode-Multiple trap. The Wake PNGs auto-slice into disconnected
    /// sub-sprites, so the naive <c>Resources.Load&lt;Sprite&gt;</c> path returns null — this test proves the
    /// emitter's chosen path (a <see cref="WakeSpriteLibrary"/> of TEXTURES in Resources, one full-image sprite
    /// built per texture in code) resolves to four non-null, FULL-image sprites. If this ever regresses the wake
    /// silently vanishes, so it is worth a runtime guard. (The size/weight/speed → tier selection is covered
    /// headless in <c>WakeGradingTests</c>.)
    /// </summary>
    public class WakePlumePlayTests
    {
        [UnityTest]
        public IEnumerator WakeSpriteLibrary_LoadsFourFullImageSprites_ThroughTheMultipleSpriteTrap()
        {
            yield return null;

            var lib = Resources.Load<WakeSpriteLibrary>(WakeSpriteLibrary.ResourcesPath);
            Assert.IsNotNull(lib, "the WakeSpriteLibrary must be present at Resources/WakeSpriteLibrary");

            var textures = lib.Ordered();
            Assert.AreEqual(WakeGrading.TierCount, textures.Length, "one texture per graded tier");

            for (int i = 0; i < textures.Length; i++)
            {
                Texture2D tex = textures[i];
                Assert.IsNotNull(tex, $"tier {i} texture reference resolves (the always-present main asset)");

                // Build the sprite EXACTLY as the emitter does — the whole authored plume, not an auto-sliced
                // fragment: its rect must span the full texture.
                var full = new Rect(0f, 0f, tex.width, tex.height);
                var sprite = Sprite.Create(tex, full, new Vector2(0.5f, 1f), 32);
                Assert.IsNotNull(sprite, $"tier {i} full-image sprite builds from the texture");
                Assert.AreEqual(tex.width, sprite.rect.width, 0.5f, $"tier {i} sprite spans the full texture width");
                Assert.AreEqual(tex.height, sprite.rect.height, 0.5f, $"tier {i} sprite spans the full texture height");
                Object.Destroy(sprite);
            }

            // The tiers grow in size (Small < Medium < Large < Huge), so the wake genuinely scales.
            var ordered = lib.Ordered();
            for (int i = 1; i < ordered.Length; i++)
                Assert.Greater(ordered[i].height, ordered[i - 1].height,
                    "each graded tier texture is taller than the previous — a bigger authored wake");
        }

        /// <summary>
        /// The same runtime guard for the graded BOW SPRAY: the four BowSpray textures resolve through the
        /// library (they import spriteMode-Multiple exactly like the Wake PNGs, so the naive Sprite load would
        /// return null) and build full-image sprites with the emitter's impact pivot. If this regresses the
        /// spray silently vanishes.
        /// </summary>
        [UnityTest]
        public IEnumerator BowSprayLibrary_LoadsFourFullImageSprites_ThroughTheMultipleSpriteTrap()
        {
            yield return null;

            var lib = Resources.Load<WakeSpriteLibrary>(WakeSpriteLibrary.ResourcesPath);
            Assert.IsNotNull(lib, "the WakeSpriteLibrary must be present at Resources/WakeSpriteLibrary");

            var textures = lib.OrderedSpray();
            Assert.AreEqual(WakeGrading.TierCount, textures.Length, "one spray texture per graded tier");

            for (int i = 0; i < textures.Length; i++)
            {
                Texture2D tex = textures[i];
                Assert.IsNotNull(tex, $"spray tier {i} texture reference resolves (the always-present main asset)");

                // Build the sprite EXACTLY as the emitter does — impact pivot at bottom-centre (the art's dense
                // cutwater churn, pixel-verified in WakeArtOrientationTests), spanning the full texture.
                var full = new Rect(0f, 0f, tex.width, tex.height);
                var sprite = Sprite.Create(tex, full, new Vector2(0.5f, 0f), 32);
                Assert.IsNotNull(sprite, $"spray tier {i} full-image sprite builds from the texture");
                Assert.AreEqual(tex.width, sprite.rect.width, 0.5f, $"spray tier {i} sprite spans the full width");
                Assert.AreEqual(tex.height, sprite.rect.height, 0.5f, $"spray tier {i} sprite spans the full height");
                Object.Destroy(sprite);
            }

            // The spray tiers grow too (Small < Medium < Large < Huge).
            var ordered = lib.OrderedSpray();
            for (int i = 1; i < ordered.Length; i++)
                Assert.Greater(ordered[i].height, ordered[i - 1].height,
                    "each graded spray texture is taller than the previous — a bigger authored sheet");
        }
    }
}
