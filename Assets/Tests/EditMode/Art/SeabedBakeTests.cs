using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Art;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// Headless tests for the <c>_SeabedTex</c> bake mapping (ADR 0027 #7) — the world ↔ texel
    /// contract the editor bake tool WRITES with and the water shader READS with. If the two ever
    /// disagree the bottom slides against the coast whose depth decides how much of it shows, which
    /// would read as a mysteriously misplaced seabed rather than as a bug — so the mapping is pinned
    /// here rather than trusted to two implementations.
    ///
    /// <para>The BAKE ITSELF is deliberately untested: it needs a graphics device (the sprite
    /// readback) and CI has none — rendering work there CRASHES the editor rather than failing
    /// cleanly. Only the pure mapping lives in <see cref="SeabedBake"/>, and that is what runs
    /// here.</para>
    ///
    /// Sabotage MEASURED (2026-07-29, headless run — see the PR): dropping the half-texel centre
    /// offset in <see cref="SeabedBake.TexelCenterWorld"/> (<c>(x + 0.5f)</c> → <c>x</c>, the
    /// classic off-by-half that shifts the whole bottom by half a texel — 0.16 m at the shipped
    /// 512² over 160 m) fails EXACTLY 2 of 8: <c>TexelCentre_IsTheShaderSampleThatFetchesIt</c>
    /// (uv 0.0000 for texel 0, which the shader's point fetch resolves to texel 0 only by the
    /// clamp — and uv 0.9980 for the last texel, still inside it, so the round trip survives at one
    /// end and not the other) and <c>TexelCentre_SpansTheRectSymmetrically</c> (first centre at
    /// world −80.00 instead of −79.84: the bake hangs half a texel off the rect's low edge).
    /// </summary>
    public class SeabedBakeTests
    {
        private static readonly Vector2 Min = new Vector2(-80f, -60f);    // the shipped St Peters rect
        private static readonly Vector2 Size = new Vector2(160f, 120f);

        [Test]
        public void TexelCentre_IsTheShaderSampleThatFetchesIt()
        {
            // The round trip that matters: bake texel (x,y) → world → the shader's uv → a POINT
            // fetch must land back on texel (x,y). Checked at both edges and the middle.
            const int res = 512;
            foreach (int i in new[] { 0, 1, 137, res / 2, res - 2, res - 1 })
            {
                Vector2 world = SeabedBake.TexelCenterWorld(i, i, res, res, Min, Size);
                Vector2 uv = SeabedBake.WorldToUv(world, Min, Size);
                Assert.AreEqual(i, Mathf.FloorToInt(uv.x * res),
                    $"texel {i} must fetch itself back (uv.x = {uv.x})");
                Assert.AreEqual(i, Mathf.FloorToInt(uv.y * res),
                    $"texel {i} must fetch itself back (uv.y = {uv.y})");
            }
        }

        [Test]
        public void TexelCentre_SpansTheRectSymmetrically()
        {
            const int res = 512;
            Vector2 first = SeabedBake.TexelCenterWorld(0, 0, res, res, Min, Size);
            Vector2 last = SeabedBake.TexelCenterWorld(res - 1, res - 1, res, res, Min, Size);
            float halfTexelX = Size.x / res * 0.5f;
            float halfTexelY = Size.y / res * 0.5f;
            Assert.AreEqual(Min.x + halfTexelX, first.x, 1e-3f);
            Assert.AreEqual(Min.y + halfTexelY, first.y, 1e-3f);
            Assert.AreEqual(Min.x + Size.x - halfTexelX, last.x, 1e-3f);
            Assert.AreEqual(Min.y + Size.y - halfTexelY, last.y, 1e-3f);
        }

        [Test]
        public void WorldToUv_MatchesTheShadersRectMapping()
        {
            // (worldXY − _HeightWorldMin) / _HeightWorldSize, exactly as the HLSL writes it.
            Assert.AreEqual(0f, SeabedBake.WorldToUv(Min, Min, Size).x, 1e-6f);
            Assert.AreEqual(1f, SeabedBake.WorldToUv(Min + Size, Min, Size).x, 1e-6f);
            Assert.AreEqual(0.5f, SeabedBake.WorldToUv(Min + Size * 0.5f, Min, Size).y, 1e-6f);
        }

        [Test]
        public void InRect_GatesTheCoverageOutsideTheBake()
        {
            // Off-rect the shader zeroes coverage rather than smearing the Clamp edge texel across
            // the whole sea — a region larger than its bake simply has no bottom outside it.
            Assert.IsTrue(SeabedBake.InRect(new Vector2(0f, 0f)));
            Assert.IsTrue(SeabedBake.InRect(new Vector2(1f, 1f)));
            Assert.IsFalse(SeabedBake.InRect(new Vector2(-0.001f, 0.5f)));
            Assert.IsFalse(SeabedBake.InRect(new Vector2(0.5f, 1.001f)));
        }

        [Test]
        public void DegenerateRect_NeverDividesByZero()
        {
            Vector2 uv = SeabedBake.WorldToUv(new Vector2(5f, 5f), Vector2.zero, Vector2.zero);
            Assert.IsFalse(float.IsNaN(uv.x) || float.IsInfinity(uv.x));
            Assert.IsFalse(float.IsNaN(uv.y) || float.IsInfinity(uv.y));
        }

        [Test]
        public void Build_WritesRowMajorBottomUp_AndSamplesEveryTexelCentre()
        {
            const int w = 8, h = 4;
            var seen = new System.Collections.Generic.List<Vector2>();
            Color32[] px = SeabedBake.Build(w, h, Min, Size, world =>
            {
                seen.Add(world);
                return new Color(1f, 0f, 0f, 1f);
            }, new Color(0f, 0f, 0f, 0f));

            Assert.AreEqual(w * h, px.Length);
            Assert.AreEqual(w * h, seen.Count, "one sample per texel, no more.");
            // Row-major, bottom-up (SetPixels32's order): the first w samples share the lowest y.
            for (int i = 1; i < w; i++)
                Assert.AreEqual(seen[0].y, seen[i].y, 1e-4f, "the first row must be one scanline.");
            Assert.Greater(seen[w].y, seen[0].y, "row 1 must sit ABOVE row 0 in world space.");
            Assert.AreEqual(255, px[0].r);
        }

        [Test]
        public void Build_UncoveredTexelsTakeTheFill_NotTheSamplersColour()
        {
            // A cell with no ground tile is NOT a black bottom — it is NO bottom. The sampler
            // returns alpha 0 there and the fill (transparent by default) is what lands, so the
            // shader composites nothing and open water is unchanged by construction.
            Color32[] px = SeabedBake.Build(4, 4, Min, Size,
                _ => new Color(1f, 1f, 1f, 0f), new Color(0f, 0f, 0f, 0f));
            foreach (Color32 c in px) Assert.AreEqual(0, c.a, "uncovered texels must bake coverage 0.");
        }

        [Test]
        public void Build_NullSampler_Throws()
        {
            Assert.Throws<System.ArgumentNullException>(
                () => SeabedBake.Build(4, 4, Min, Size, null, Color.clear));
        }
    }
}
