using NUnit.Framework;
using UnityEngine;
using HiddenHarbours.Core;
using HiddenHarbours.Art;

namespace HiddenHarbours.Tests.Art.EditMode
{
    /// <summary>
    /// Headless guard for the Y → sorting-order mapping behind <see cref="YSortSprite"/> — the contract that
    /// makes grass/trees auto-layer with the player: lower world Y draws in front, clamped into a band so it
    /// never slips below the wharf deck / sea / seabed, nor over the ropes and rain that must clear all decor.
    ///
    /// <para><b>⚠ These assert against the SHIPPED band (<see cref="SortingBands"/>), not against literals.</b>
    /// They used to pass <c>10f, 4f, 2, 40</c> in by hand, which meant they went on passing unchanged while
    /// the thing they were guarding drifted: those were the real defaults until 2026-08-05, when the band was
    /// found to resolve only 9.5 m of a 520 m region — 98% of St Peters' ground cover, and the player standing
    /// in it, sat on a saturated order and interleaved by draw order instead of by position (ADR 0032). A test
    /// that hard-codes the numbers it is guarding cannot notice that. The clamp itself was never the bug and
    /// is still the intended behaviour; its BOUNDS were simply far too close together.</para>
    ///
    /// <para>Band-vs-region reach — the claim that actually failed — is guarded in <c>SortingBandsTests</c>.</para>
    /// </summary>
    public class YSortSpriteTests
    {
        /// <summary>The mapping as it actually ships.</summary>
        static int Order(float worldY) =>
            YSortSprite.OrderFor(worldY, SortingBands.DecorBase, SortingBands.OrdersPerMetre,
                                 SortingBands.DecorFloor, SortingBands.DecorCeiling);

        [Test]
        public void OrderFor_AtZeroY_IsBaseOrder()
        {
            Assert.AreEqual(SortingBands.DecorBase, Order(0f));
        }

        [Test]
        public void OrderFor_LowerY_SortsInFront()
        {
            // A sprite lower on the screen (smaller Y) must get a HIGHER order (drawn in front).
            int front = Order(-1f);
            int back = Order(1f);
            Assert.Greater(front, back, "Lower Y must sort in front of higher Y.");
            Assert.AreEqual(SortingBands.DecorBase + 4, front);   // base − (−1)·4
            Assert.AreEqual(SortingBands.DecorBase - 4, back);    // base − 1·4
        }

        [Test]
        public void OrderFor_MonotonicNonIncreasingInY()
        {
            int prev = int.MaxValue;
            // Sweep the WHOLE resolved span and out past both clamps — not a ±10 m window round the base,
            // which is the blind spot that let the old band's reach go unmeasured.
            for (float y = -SortingBands.DecorHalfExtentMetres * 1.2f;
                 y <= SortingBands.DecorHalfExtentMetres * 1.2f;
                 y += 5f)
            {
                int o = Order(y);
                Assert.LessOrEqual(o, prev, $"Order must not increase as Y increases (at Y = {y} m).");
                prev = o;
            }
        }

        [Test]
        public void OrderFor_ClampsToSafeBand()
        {
            // Far 'down' saturates at the ceiling; far 'up' at the floor — so a Y-sorted sprite can never
            // cross out of the decor band into the deck/sea/seabed below, or the ropes and rain above.
            // ⚠ Saturating is the clamp DOING ITS JOB out beyond the world's edge. Saturating INSIDE a
            // region is the 2026-08-05 defect, and SortingBandsTests is what keeps the two apart.
            float wayPast = SortingBands.DecorHalfExtentMetres * 2f;
            Assert.AreEqual(SortingBands.DecorCeiling, Order(-wayPast), "Far-down clamps to the ceiling.");
            Assert.AreEqual(SortingBands.DecorFloor, Order(wayPast), "Far-up clamps to the floor.");
        }

        [Test]
        public void OrderFor_ResolvesTheWholeSpanItPromises()
        {
            // The floor and ceiling are reached EXACTLY at the declared half-extent: the band is centred on
            // the origin, and every metre it claims to resolve actually resolves.
            Assert.AreEqual(SortingBands.DecorFloor, Order(SortingBands.DecorHalfExtentMetres));
            Assert.AreEqual(SortingBands.DecorCeiling, Order(-SortingBands.DecorHalfExtentMetres));
            Assert.Greater(Order(-SortingBands.DecorHalfExtentMetres + 1f), SortingBands.DecorFloor,
                "A metre inside the south edge must already be off the ceiling, or the span is a lie.");
        }

        [Test]
        public void OrderFor_PlayerAndGrass_InterleaveOnSameScale()
        {
            // Same parameters (the shared default) → a grass tuft just below the player draws over it, and a
            // tuft just above draws under it. This is the whole point: consistent scale = automatic layering.
            // ⚠ Checked UP IN THE VILLAGE (Y ≈ +14, Ginny's cottage) rather than at Y = 0. The old band
            // still layered correctly at the origin — which is precisely why the defect survived so long:
            // the start spawn sits at Y = 0, dead centre of the only window that still worked.
            float playerY = 14f;
            int player = Order(playerY);
            int tuftInFront = Order(playerY - 0.5f);
            int tuftBehind = Order(playerY + 0.5f);
            Assert.Greater(tuftInFront, player, "A tuft below the player must draw in front of the player.");
            Assert.Less(tuftBehind, player, "A tuft above the player must draw behind the player.");
        }
    }
}
