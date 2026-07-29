using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Art;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// Headless twin tests for the ADR 0027 #8 reflection geometry — <see cref="WaterReflectionWarp"/>,
    /// the C# mirror of <c>Include/ReflectMirror.hlsl</c> and the water shader's
    /// <c>ObjectReflection()</c> (change one, change BOTH in the same PR). Contracts pinned:
    ///
    /// <list type="bullet">
    /// <item><b>the mirror axis is the ground-contact pivot</b> (ADR 0026): mirroring is an
    /// involution, the pivot is its ONLY fixed point (so the reflection meets the source exactly at
    /// the waterline instead of floating off it), and X is never touched;</item>
    /// <item><b>the lookup does not depend on the camera</b> — the whole point of snapping in world
    /// space. The crawl test sweeps the camera through a full pixel in sub-pixel steps and asserts
    /// the sampled world cell never moves;</item>
    /// <item><b>and the screen-snapped version DOES crawl</b>, measured as a camera-offset-dependent
    /// sample delta rather than asserted as an adjective — that is what makes the world snap a
    /// finding instead of a preference;</item>
    /// <item>the §11.6 pre/post-grade split recovers exactly the light content and nothing else, and
    /// is a no-op for ordinary premultiplied reflections at any coverage.</item>
    /// </list>
    ///
    /// Sabotage MEASURED (2026-07-29, headless runs over this fixture, 12 tests — see the PR), three
    /// arms, each targeted:
    /// <list type="number">
    /// <item><b>mirror about the WRONG AXIS</b> (<c>MirrorY</c> → <c>−worldY</c>, i.e. the world
    /// origin instead of the published pivot — precisely what the sprite-origin trap produces when a
    /// shader reads <c>unity_ObjectToWorld</c>): see the class doc's measured table in the PR;</item>
    /// <item><b>drop the world snap</b> (<c>WarpedSampleWorld</c> returns the un-Pixelized position):
    /// the crawl arrives, and the fixture reports the sample delta it causes;</item>
    /// <item><b>swap the split's min/max</b> in <c>SplitLitShare</c>: the lit share and the ordinary
    /// share change places, which is a lit wheelhouse rendered as an ordinary surface AND an ordinary
    /// hull rendered as compensated light.</item>
    /// </list>
    /// </summary>
    public class WaterReflectionWarpTests
    {
        const float Ppu = 32f;          // the project's pixel grid: 1 px = 3.125 cm

        // ===== the mirror axis (ADR 0026 ground contact) ==================================

        [Test]
        public void Mirror_LeavesThePivotItselfFixed()
        {
            // The reflection must MEET the source at the waterline contact. The pivot being the
            // only fixed point is what guarantees that; any other axis floats the reflection off.
            foreach (float pivotY in new[] { -3.5f, 0f, 2f, 11.25f })
                Assert.AreEqual(pivotY, WaterReflectionWarp.MirrorY(pivotY, pivotY), 1e-5f);
        }

        [Test]
        public void Mirror_IsAnInvolution()
        {
            const float pivotY = 4.25f;
            foreach (float y in new[] { -2f, 0f, 1f, 4.25f, 9f })
                Assert.AreEqual(y, WaterReflectionWarp.MirrorY(WaterReflectionWarp.MirrorY(y, pivotY), pivotY),
                                1e-4f);
        }

        [Test]
        public void Mirror_ReflectsBelowThePivotByTheHeightAboveIt()
        {
            // A 5.5 m tree standing at y = 2 reflects to y = −3.5: as far below the contact point
            // as its crown is above it.
            Assert.AreEqual(-3.5f, WaterReflectionWarp.MirrorY(7.5f, 2f), 1e-4f);
            Assert.AreEqual(7.5f, WaterReflectionWarp.MirrorY(-3.5f, 2f), 1e-4f);
        }

        [Test]
        public void Mirror_NeverTouchesX()
        {
            // The mirror plane is horizontal. An X term here would slide every reflection sideways
            // off its source — the failure that reads as "the reflection belongs to another boat".
            var pivot = new Vector2(-11f, 3f);
            foreach (float x in new[] { -20f, 0f, 7.5f })
                Assert.AreEqual(x, WaterReflectionWarp.Mirror(new Vector2(x, 6f), pivot).x, 1e-5f);
        }

        [Test]
        public void Mirror_UsesThePublishedPivot_NotTheWorldOrigin()
        {
            // 🔴 The sprite trap, pinned as a VALUE. unity_ObjectToWorld is identity for a
            // SpriteRenderer, so a shader that mirrors about "the object origin" mirrors about
            // (0,0) — this asserts the two answers are far apart, so the trap cannot be
            // reintroduced silently.
            const float pivotY = 6f, y = 8f;
            float correct = WaterReflectionWarp.MirrorY(y, pivotY);   // 4
            float originBug = -y;                                     // -8: the trap's answer
            Assert.AreEqual(4f, correct, 1e-5f);
            Assert.Greater(Mathf.Abs(correct - originBug), 10f,
                "mirroring about the world origin must be measurably, grossly different.");
        }

        // ===== the world-snapped lookup (the crawl law) ====================================

        [Test]
        public void WarpedSample_SnapsOntoTheWorldPixelGrid()
        {
            Vector2 s = WaterReflectionWarp.WarpedSampleWorld(
                new Vector2(3.3123f, -1.2871f), new Vector2(0.4f, -0.2f), 0.35f, Ppu);
            Assert.AreEqual(Mathf.Round(s.x * Ppu), s.x * Ppu, 1e-3f);
            Assert.AreEqual(Mathf.Round(s.y * Ppu), s.y * Ppu, 1e-3f);
        }

        [Test]
        public void WarpedSample_ZeroWarpIsTheFragmentsOwnCell()
        {
            // Warp 0 = a flat mirror: the reflection is read from directly under the fragment.
            var world = new Vector2(3.3123f, -1.2871f);
            Assert.AreEqual(WaterReflectionWarp.Pixelize(world, Ppu),
                            WaterReflectionWarp.WarpedSampleWorld(world, new Vector2(9f, -9f), 0f, Ppu));
        }

        [Test]
        public void WarpedSample_QuantizesToWholeWorldCells_NotSubPixelPositions()
        {
            // The quantization has to be REAL, not decorative: every fragment inside one world cell
            // must read the SAME reflection cell (that is what "a reflection cell belongs to a place
            // on the water" means), and a fragment a cell away must read a different one. Asserting
            // this rather than "it ignores the camera" — the lookup takes no camera argument, so
            // that would be a tautology about the signature, not a property of the law.
            var slope = new Vector2(0.31f, -0.17f);
            const float cell = 1f / Ppu;
            var baseWorld = new Vector2(3.3123f, -1.2871f);
            Vector2 home = WaterReflectionWarp.WarpedSampleWorld(baseWorld, slope, 0.35f, Ppu);

            // …anywhere inside the same cell (the snapped position plus a sliver) reads it too.
            for (int i = 1; i < 8; i++)
            {
                var inside = home + new Vector2(cell * i / 8f, cell * i / 8f);
                Assert.AreEqual(home, WaterReflectionWarp.WarpedSampleWorld(inside, slope, 0.35f, Ppu),
                    "fragments within one world cell must share a reflection cell.");
            }
            // …and a whole cell over is a different cell, by exactly one cell.
            Vector2 next = WaterReflectionWarp.WarpedSampleWorld(
                home + new Vector2(cell, 0f), slope, 0.35f, Ppu);
            Assert.AreEqual(cell, next.x - home.x, 1e-5f);
        }

        [Test]
        public void ScreenSnappedSample_CRAWLS_AndHereIsHowFar()
        {
            // The wrong answer, MEASURED. Snapping in camera-relative (screen/RT) space makes the
            // sampled world position slide as the camera pans by less than one pixel — the fragment
            // has not moved, the water has not moved, and yet it reads a different cell. This is
            // the crawl ADR 0027's Pixelation section names for this exact item.
            var world = new Vector2(3.3123f, -1.2871f);
            var slope = new Vector2(0.31f, -0.17f);
            const float cell = 1f / Ppu;

            float minX = float.MaxValue, maxX = float.MinValue;
            int distinct = 0;
            float last = float.NaN;
            for (int i = 0; i < 40; i++)
            {
                var cam = new Vector2(i * cell / 40f, 0f);   // a sub-pixel pan, 1/40th of a cell a step
                float x = WaterReflectionWarp.ScreenSnappedSampleWorld(world, cam, slope, 0.35f, Ppu).x;
                minX = Mathf.Min(minX, x);
                maxX = Mathf.Max(maxX, x);
                if (!Mathf.Approximately(x, last)) { distinct++; last = x; }
            }

            float travelPx = (maxX - minX) * Ppu;
            Assert.Greater(travelPx, 0.5f,
                $"the screen-snapped lookup must visibly crawl; it travelled {travelPx:0.###} px " +
                "across a sub-pixel camera pan.");
            Assert.Greater(distinct, 1,
                "the screen-snapped lookup must change cell mid-pan — that flicker IS the crawl.");

            // And the world-snapped one, over the SAME sweep, does not move at all.
            float worldSnapped = WaterReflectionWarp.WarpedSampleWorld(world, slope, 0.35f, Ppu).x;
            for (int i = 0; i < 40; i++)
                Assert.AreEqual(worldSnapped,
                    WaterReflectionWarp.WarpedSampleWorld(world, slope, 0.35f, Ppu).x);
        }

        // ===== the §11.6 pre/post-grade split ==============================================

        [Test]
        public void Split_OrdinaryReflection_IsEntirelyPreGrade()
        {
            // Premultiplied colour can never exceed its coverage for an ordinary surface, so there
            // is nothing for the compensated bucket to take — at ANY coverage.
            foreach (float cov in new[] { 0.05f, 0.4f, 1f })
            {
                var rgb = new Vector3(0.8f, 0.6f, 0.35f) * cov;   // premultiplied
                WaterReflectionWarp.SplitLitShare(rgb, cov, out Vector3 pre, out Vector3 post);
                Assert.AreEqual(rgb.x, pre.x, 1e-5f);
                Assert.AreEqual(0f, post.x, 1e-5f, $"nothing may leak into the lit bucket at cov {cov}");
                Assert.AreEqual(0f, post.y, 1e-5f);
                Assert.AreEqual(0f, post.z, 1e-5f);
            }
        }

        [Test]
        public void Split_NightLitSource_SendsExactlyTheExcessToTheCompensatedBucket()
        {
            // A lit wheelhouse writes its colour ALREADY divided by the day/night tint, so at night
            // it lands far above coverage. The excess over coverage is precisely the light content.
            const float cov = 0.5f;
            var rgb = new Vector3(4.0f, 3.0f, 1.5f);   // compensated, premultiplied
            WaterReflectionWarp.SplitLitShare(rgb, cov, out Vector3 pre, out Vector3 post);
            Assert.AreEqual(cov, pre.x, 1e-5f, "the pre-grade share is capped at coverage.");
            Assert.AreEqual(rgb.x - cov, post.x, 1e-5f);
            Assert.AreEqual(rgb.y - cov, post.y, 1e-5f);
            Assert.AreEqual(rgb.z - cov, post.z, 1e-5f);
            // and the two shares reconstruct the sample exactly — nothing is lost or doubled.
            Assert.AreEqual(rgb.x, pre.x + post.x, 1e-5f);
        }

        [Test]
        public void Split_IsLossless_AtEveryCoverage()
        {
            for (float cov = 0f; cov <= 1f; cov += 0.05f)
            {
                var rgb = new Vector3(0.9f, 2.4f, 0.1f);
                WaterReflectionWarp.SplitLitShare(rgb, cov, out Vector3 pre, out Vector3 post);
                Assert.AreEqual(rgb.x, pre.x + post.x, 1e-5f);
                Assert.AreEqual(rgb.y, pre.y + post.y, 1e-5f);
                Assert.AreEqual(rgb.z, pre.z + post.z, 1e-5f);
                Assert.GreaterOrEqual(post.x, 0f);
                Assert.GreaterOrEqual(post.y, 0f);
            }
        }
    }
}
